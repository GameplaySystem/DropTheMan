using System;
using System.Collections.Generic;
using DropAwayPrototype.Runtime;
using PuzzleFramework.Content;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.Presentation;
using UnityEngine;

namespace DropAwayPrototype.Editor
{
    /// <summary>
    /// Scene-attachable visual authoring shell for Drop The Man.
    /// Its live framework session owns board structure and footprint placement. Serialized DTM
    /// data retains game payload and JSON ownership; SceneView tooling stays in an editor wrapper.
    /// </summary>
    [AddComponentMenu("Drop Away/Drop The Man Editor Board Controller")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class DropTheManEditorBoardController : MonoBehaviour
    {
        private static readonly Color LightCellColor = new(0.92f, 0.93f, 0.95f, 1f);
        private static readonly Color DarkCellColor = new(0.80f, 0.84f, 0.90f, 1f);
        private static readonly Color BlockedFallbackColor = new(0.35f, 0.17f, 0.17f, 1f);
        private static readonly Color[] SlotColors =
        {
            new(0.89f, 0.25f, 0.24f, 1f),
            new(0.17f, 0.48f, 0.92f, 1f),
            new(0.17f, 0.67f, 0.34f, 1f),
            new(0.92f, 0.74f, 0.17f, 1f),
            new(0.86f, 0.38f, 0.13f, 1f),
            new(0.55f, 0.29f, 0.74f, 1f),
            new(0.15f, 0.69f, 0.74f, 1f),
            new(0.90f, 0.44f, 0.64f, 1f),
            new(0.47f, 0.62f, 0.20f, 1f),
            new(0.36f, 0.36f, 0.40f, 1f)
        };

        [SerializeField] private DropTheManEditorConfig config;
        [SerializeField] private Vector2 cellSize = Vector2.one;
        [SerializeField] private Vector3 boardGridXAxis = Vector3.right;
        [SerializeField] private Vector3 boardGridYAxis = Vector3.forward;
        [SerializeField] private DropTheManDevLevelData levelData = new();
        [SerializeField] private DropTheManEditorPlacementMode currentMode =
            DropTheManEditorPlacementMode.Obstacle;
        [SerializeField] private ColorIdentity selectedColor = ColorIdentity.Slot0;
        [SerializeField, Min(0)] private int selectedHolePaletteIndex;
        [SerializeField, Range(0, 3)] private int selectedHoleQuarterTurns;
        [SerializeField] private bool captureSceneInputWhenSelected = true;
        [SerializeField] private bool showSceneOverlay = true;
        [Header("Camera Framing")]
        [SerializeField] private Camera framingCamera;
        [SerializeField, Min(1f)] private float cameraFramingPadding = 1.15f;
        [SerializeField, Min(0.01f)] private float minimumCameraDistance = 1f;
        [SerializeField, HideInInspector] private Transform boardVisualRoot;
        [SerializeField, HideInInspector] private Transform placementVisualRoot;

        private Vector2Int _lastBoardSize = new(-1, -1);
        private bool _refreshQueued;
        private LevelAuthoringCore _authoringSession;

        public DropTheManEditorConfig Config => config;
        public DropTheManDevLevelData LevelData => levelData;
        public DropTheManEditorPlacementMode CurrentMode => currentMode;
        public ColorIdentity SelectedColor => selectedColor;
        public int SelectedHolePaletteIndex => selectedHolePaletteIndex;
        public int SelectedHoleQuarterTurns => selectedHoleQuarterTurns;
        public int SelectedHoleRotationDegrees => selectedHoleQuarterTurns * 90;
        public bool CaptureSceneInputWhenSelected => captureSceneInputWhenSelected;
        public bool ShowSceneOverlay => showSceneOverlay;
        /// <summary>Selected structural item ID, shared by click-to-rotate and erase behavior.</summary>
        public string SelectedAuthoredItemId => _authoringSession?.SelectedItemId;

        private void OnEnable()
        {
            _authoringSession = null;
            if (Application.isPlaying)
            {
                EnsurePlayModeRuntimeShell();
                return;
            }

            ScheduleRefreshInEditor();
        }

        private void OnValidate()
        {
            // Inspector edits change serialized game data outside the live session.
            _authoringSession = null;
            if (Application.isPlaying)
            {
                return;
            }

            ScheduleRefreshInEditor();
        }

        private void ScheduleRefreshInEditor()
        {
#if UNITY_EDITOR
            if (_refreshQueued)
            {
                return;
            }

            _refreshQueued = true;
            UnityEditor.EditorApplication.delayCall += RunQueuedEditorRefresh;
#else
            RefreshVisuals();
#endif
        }

#if UNITY_EDITOR
        private void RunQueuedEditorRefresh()
        {
            _refreshQueued = false;

            if (this == null || Application.isPlaying)
            {
                return;
            }

            RefreshVisuals();
        }
#endif

        [ContextMenu("Refresh Editor Board")]
        public void RefreshVisuals()
        {
            EnsureLevelDataInitialized();
            ClampSelection();

            if (!TryCreateWorldLayout(out GridWorldLayout worldLayout, out string failureReason))
            {
                Debug.LogWarning(failureReason, this);
                return;
            }

            if (!TryEnsureAuthoringSession(out failureReason))
            {
                Debug.LogWarning($"Drop The Man editor session could not be restored: {failureReason}", this);
                return;
            }
            EnsureVisualRoots();
            RebuildBoardVisuals(worldLayout);
            RebuildPlacementVisuals(worldLayout);
            PositionEditorCamera(worldLayout);
        }

        internal void SetFramingCamera(Camera targetCamera)
        {
            framingCamera = targetCamera;
        }

        public void SetPlacementMode(DropTheManEditorPlacementMode mode)
        {
            currentMode = mode;
        }

        public void SetSelectedColor(ColorIdentity colorIdentity)
        {
            selectedColor = NormalizeColorIdentity(colorIdentity);
        }

        public int ResolveHolePaletteEntryCount()
        {
            return config != null && config.holePaletteEntries != null
                ? config.holePaletteEntries.Count
                : 0;
        }

        public void CycleHolePalette(int direction)
        {
            int paletteCount = ResolveHolePaletteEntryCount();
            if (paletteCount <= 0)
            {
                selectedHolePaletteIndex = 0;
                return;
            }

            int step = direction == 0 ? 0 : Math.Sign(direction);
            selectedHolePaletteIndex =
                (selectedHolePaletteIndex + step + paletteCount) % paletteCount;
        }

        public void RotateSelectedHoleClockwise()
        {
            selectedHoleQuarterTurns = (selectedHoleQuarterTurns + 1) % 4;
        }

        public string ResolveSelectedHolePaletteDisplayName()
        {
            DropTheManEditorHolePaletteEntry entry = ResolveSelectedHolePaletteEntry();
            if (entry == null)
            {
                return "None";
            }

            return string.IsNullOrWhiteSpace(entry.displayName)
                ? entry.id
                : entry.displayName;
        }

        public Color ResolveSelectedColorPreview()
        {
            return ResolveSlotColor(selectedColor);
        }

        public bool TryGetCellFromRay(Ray ray, out Vector2Int coordinate)
        {
            coordinate = default;
            if (!TryEnsureAuthoringSession(out _) ||
                !TryCreateWorldLayout(out GridWorldLayout worldLayout, out _))
            {
                return false;
            }

            if (!BoardAuthoringPicker.TryPickCell(ray, worldLayout, transform.position,
                    out GridCoordinate picked))
            {
                return false;
            }

            coordinate = new Vector2Int(picked.X, picked.Y);
            return IsWithinBoard(coordinate);
        }

        public bool ApplyPrimaryActionAt(Vector2Int coordinate)
        {
            if (!TryEnsureAuthoringSession(out string failureReason))
            {
                Debug.LogWarning($"Cannot edit Drop The Man level: {failureReason}", this);
                return false;
            }
            return currentMode switch
            {
                DropTheManEditorPlacementMode.Obstacle => ToggleBlockedCell(coordinate),
                DropTheManEditorPlacementMode.Stickman => PlaceStickman(coordinate),
                DropTheManEditorPlacementMode.Hole => PlaceHole(coordinate),
                DropTheManEditorPlacementMode.HoleRotation => RotateHoleAtOrContaining(coordinate),
                _ => false
            };
        }

        public bool ApplyEraseActionAt(Vector2Int coordinate)
        {
            if (!TryEnsureAuthoringSession(out string failureReason))
            {
                Debug.LogWarning($"Cannot erase Drop The Man content: {failureReason}", this);
                return false;
            }
            return currentMode switch
            {
                DropTheManEditorPlacementMode.Obstacle => EraseBlockedCell(coordinate),
                DropTheManEditorPlacementMode.Stickman => RemoveStickmanAt(coordinate),
                DropTheManEditorPlacementMode.Hole => RemoveHoleAtOrContaining(coordinate),
                DropTheManEditorPlacementMode.HoleRotation => RemoveHoleAtOrContaining(coordinate),
                _ => false
            };
        }

        public bool ApplyBoardDimensions(int width, int height)
        {
            if (!TryEnsureAuthoringSession(out string failureReason))
            {
                Debug.LogWarning($"Cannot resize Drop The Man board: {failureReason}", this);
                return false;
            }

            int clampedWidth = Mathf.Max(1, width);
            int clampedHeight = Mathf.Max(1, height);
            if (levelData.BoardWidth == clampedWidth && levelData.BoardHeight == clampedHeight)
            {
                return false;
            }

            if (!TryResizeAuthoringSession(clampedWidth, clampedHeight)) return false;
            RefreshVisuals();
            return true;
        }

        public bool TryExportCurrentLevel(
            string requestedPath,
            out string resolvedAbsolutePath,
            out string jsonText,
            out string failureReason)
        {
            if (!TryEnsureAuthoringSession(out failureReason))
            {
                resolvedAbsolutePath = string.Empty;
                jsonText = string.Empty;
                return false;
            }

            return DropTheManEditorJsonExportUtility.TryExportToFile(
                levelData,
                requestedPath,
                out resolvedAbsolutePath,
                out jsonText,
                out failureReason);
        }

        public bool TryImportLevelFromPath(
            string requestedPath,
            out string resolvedAbsolutePath,
            out string failureReason)
        {
            if (!DropTheManEditorJsonExportUtility.TryImportFromFile(
                    requestedPath,
                    out DropTheManDevLevelData importedLevelData,
                    out resolvedAbsolutePath,
                    out failureReason))
            {
                return false;
            }

            DropTheManDevLevelData stagedData = CloneLevelData(importedLevelData);
            if (!TryCreateAuthoringSession(stagedData, out LevelAuthoringCore stagedSession,
                    out failureReason)) return false;
            ApplyImportedLevelData(stagedData, stagedSession);
            failureReason = string.Empty;
            return true;
        }

        public void SetLevelMetadata(string levelId, string displayName)
        {
            EnsureLevelDataInitialized();

            if (!string.IsNullOrWhiteSpace(levelId))
            {
                levelData.LevelId =
                    DropTheManEditorJsonExportUtility.NormalizeLevelId(levelId);
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                levelData.DisplayName = displayName.Trim();
            }
            if (TryEnsureAuthoringSession(out _))
                _authoringSession.SetMetadata(levelData.LevelId, levelData.DisplayName, 1);
        }

        public void SetTimerSettings(bool enabled, float durationSeconds, float warningThresholdSeconds)
        {
            EnsureLevelDataInitialized();
            levelData.TimerEnabled = enabled;
            levelData.TimerDurationSeconds = enabled
                ? Mathf.Max(0.01f, durationSeconds)
                : Mathf.Max(0f, durationSeconds);
            levelData.TimerWarningThresholdSeconds = Mathf.Max(0f, warningThresholdSeconds);
            if (TryEnsureAuthoringSession(out _))
                _authoringSession.SetTimer(levelData.TimerEnabled, TimerMode.Countdown,
                    levelData.TimerDurationSeconds, levelData.TimerWarningThresholdSeconds);
        }

        private void EnsureLevelDataInitialized()
        {
            if (levelData == null)
            {
                levelData = new DropTheManDevLevelData();
            }

            if (levelData.BoardWidth <= 0)
            {
                levelData.BoardWidth = config != null
                    ? Mathf.Max(1, config.defaultBoardWidth)
                    : 5;
            }

            if (levelData.BoardHeight <= 0)
            {
                levelData.BoardHeight = config != null
                    ? Mathf.Max(1, config.defaultBoardHeight)
                    : 5;
            }

            levelData.BlockedCells ??= new List<Vector2Int>();
            levelData.Holes ??= new List<DropTheManDevHoleData>();
            levelData.Stickmen ??= new List<DropTheManDevStickmanData>();

            if (string.IsNullOrWhiteSpace(levelData.LevelId))
            {
                levelData.LevelId = "Level 1";
            }

            if (string.IsNullOrWhiteSpace(levelData.DisplayName))
            {
                levelData.DisplayName = levelData.LevelId;
            }
        }

        private void ApplyImportedLevelData(DropTheManDevLevelData importedLevelData,
            LevelAuthoringCore importedSession)
        {
            levelData = importedLevelData;
            _authoringSession = importedSession;
            _lastBoardSize = new Vector2Int(levelData.BoardWidth, levelData.BoardHeight);
            RefreshVisuals();
        }

        private bool TryEnsureAuthoringSession(out string failureReason)
        {
            if (_authoringSession != null)
            {
                failureReason = string.Empty;
                return true;
            }

            EnsureLevelDataInitialized();
            // This path also covers direct Inspector resizing of serialized scene data.
            HandleResizeCleanup();
            if (!TryCreateAuthoringSession(levelData, out LevelAuthoringCore session,
                    out failureReason)) return false;
            _authoringSession = session;
            return true;
        }

        private static bool TryCreateAuthoringSession(DropTheManDevLevelData data,
            out LevelAuthoringCore session, out string failureReason)
        {
            // Construct a complete candidate first, so a failed import never replaces live state.
            session = null;
            if (data == null || data.BoardWidth <= 0 || data.BoardHeight <= 0)
            {
                failureReason = "Board dimensions must be positive.";
                return false;
            }

            try
            {
                HashSet<Vector2Int> blocked = new();
                foreach (Vector2Int coordinate in data.BlockedCells)
                    if (coordinate.x < 0 || coordinate.x >= data.BoardWidth ||
                        coordinate.y < 0 || coordinate.y >= data.BoardHeight || !blocked.Add(coordinate))
                    {
                        failureReason = $"Invalid or duplicate blocked coordinate {coordinate}.";
                        return false;
                    }

                BoardDefinitionData board = new() { Width = data.BoardWidth, Height = data.BoardHeight };
                for (int y = 0; y < data.BoardHeight; y++)
                    for (int x = 0; x < data.BoardWidth; x++)
                    {
                        Vector2Int coordinate = new(x, y);
                        board.Cells.Add(new CellDefinitionData
                        {
                            Coordinate = new CellCoordinateData { X = x, Y = y },
                            CellState = blocked.Contains(coordinate)
                                ? AuthoredCellState.Blocked : AuthoredCellState.Active
                        });
                    }

                List<AuthoredFootprint> items = new();
                foreach (DropTheManDevStickmanData stickman in data.Stickmen)
                    items.Add(new AuthoredFootprint(stickman.Id,
                        new GridCoordinate(stickman.Coordinate.x, stickman.Coordinate.y),
                        new[] { new GridCoordinate(0, 0) }));
                foreach (DropTheManDevHoleData hole in data.Holes)
                    items.Add(new AuthoredFootprint(hole.Id,
                        new GridCoordinate(hole.Coordinate.x, hole.Coordinate.y),
                        ToGridOffsets(ResolveFootprintOffsets(hole.FootprintOffsets))));

                return LevelAuthoringCore.TryRestore(board, items,
                    new LevelMetadata { LevelId = data.LevelId, DisplayName = data.DisplayName },
                    new TimerDefinitionData
                    {
                        IsEnabled = data.TimerEnabled,
                        Mode = TimerMode.Countdown,
                        DurationSeconds = data.TimerDurationSeconds,
                        WarningThresholdSeconds = data.TimerWarningThresholdSeconds
                    }, out session, out failureReason);
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }
        }

        private bool TryResizeAuthoringSession(int width, int height)
        {
            // DTM explicitly prunes reported conflicts; the framework itself never chooses that policy.
            AuthoringStructuralImpact impact = _authoringSession.InspectResize(width, height);
            if (!string.IsNullOrEmpty(impact.Reason))
            {
                Debug.LogWarning($"Cannot resize Drop The Man board: {impact.Reason}", this);
                return false;
            }

            DropTheManDevLevelData original = CloneLevelData(levelData);
            HashSet<string> removedIds = new(impact.AffectedItemIds);
            int removedStickmen = levelData.Stickmen.RemoveAll(item => removedIds.Contains(item.Id));
            int removedHoles = levelData.Holes.RemoveAll(item => removedIds.Contains(item.Id));
            foreach (string id in removedIds) _authoringSession.Erase(id);

            int removedBlockedCells = 0;
            foreach (GridCoordinate cell in impact.AffectedCells)
            {
                _authoringSession.TrySetCellState(cell, AuthoredCellState.Active);
                if (levelData.BlockedCells.Remove(new Vector2Int(cell.X, cell.Y))) removedBlockedCells++;
            }

            AuthoringEditResult resized = _authoringSession.TryResize(width, height);
            if (!resized.Success)
            {
                // Defensive rollback: a failed shared resize must not leave payload and session apart.
                levelData = original;
                TryCreateAuthoringSession(original, out _authoringSession, out _);
                Debug.LogWarning($"Cannot resize Drop The Man board: {resized.Reason}", this);
                return false;
            }

            levelData.BoardWidth = width;
            levelData.BoardHeight = height;
            _lastBoardSize = new Vector2Int(width, height);
            if (removedBlockedCells + removedStickmen + removedHoles > 0)
                Debug.LogWarning($"Drop The Man editor resized to {width}x{height} and " +
                    $"removed {removedBlockedCells} blocked cells, {removedStickmen} stickmen, " +
                    $"and {removedHoles} holes that no longer fit the board.", this);
            return true;
        }

        private static List<GridCoordinate> ToGridOffsets(IReadOnlyList<Vector2Int> offsets)
        {
            List<GridCoordinate> result = new(offsets.Count);
            foreach (Vector2Int offset in offsets)
                result.Add(new GridCoordinate(offset.x, offset.y));
            return result;
        }

        private static List<Vector2Int> ToVectorOffsets(IReadOnlyList<GridCoordinate> offsets)
        {
            List<Vector2Int> result = new(offsets.Count);
            foreach (GridCoordinate offset in offsets)
                result.Add(new Vector2Int(offset.X, offset.Y));
            return result;
        }

        private void ClampSelection()
        {
            selectedHoleQuarterTurns = Mathf.Clamp(selectedHoleQuarterTurns, 0, 3);
            selectedColor = NormalizeColorIdentity(selectedColor);

            int paletteCount = config != null && config.holePaletteEntries != null
                ? config.holePaletteEntries.Count
                : 0;
            selectedHolePaletteIndex = paletteCount <= 0
                ? 0
                : Mathf.Clamp(selectedHolePaletteIndex, 0, paletteCount - 1);
        }

        private void HandleResizeCleanup()
        {
            Vector2Int currentBoardSize = new(levelData.BoardWidth, levelData.BoardHeight);
            if (_lastBoardSize.x <= 0 || _lastBoardSize.y <= 0)
            {
                _lastBoardSize = currentBoardSize;
                return;
            }

            if (_lastBoardSize == currentBoardSize)
            {
                return;
            }

            int removedBlockedCells = RemoveBlockedCellsOutsideBoard();
            int removedStickmen = RemoveStickmenOutsideBoard();
            int removedHoles = RemoveHolesOutsideBoard();
            _lastBoardSize = currentBoardSize;

            if (removedBlockedCells == 0 && removedStickmen == 0 && removedHoles == 0)
            {
                return;
            }

            Debug.LogWarning(
                $"Drop The Man editor resized to {currentBoardSize.x}x{currentBoardSize.y} and " +
                $"removed {removedBlockedCells} blocked cells, {removedStickmen} stickmen, and " +
                $"{removedHoles} holes that no longer fit the board.",
                this);
        }

        private int RemoveBlockedCellsOutsideBoard()
        {
            int removed = 0;
            for (int i = levelData.BlockedCells.Count - 1; i >= 0; i--)
            {
                if (!IsWithinBoard(levelData.BlockedCells[i]))
                {
                    levelData.BlockedCells.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        private int RemoveStickmenOutsideBoard()
        {
            int removed = 0;
            for (int i = levelData.Stickmen.Count - 1; i >= 0; i--)
            {
                if (!IsWithinBoard(levelData.Stickmen[i].Coordinate))
                {
                    levelData.Stickmen.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        private int RemoveHolesOutsideBoard()
        {
            int removed = 0;
            for (int i = levelData.Holes.Count - 1; i >= 0; i--)
            {
                if (!DoesHoleFit(levelData.Holes[i], null))
                {
                    levelData.Holes.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        private void EnsureVisualRoots()
        {
            boardVisualRoot = EnsureChildRoot(boardVisualRoot, "BoardVisuals");
            placementVisualRoot = EnsureChildRoot(placementVisualRoot, "PlacementVisuals");
        }

        private void EnsurePlayModeRuntimeShell()
        {
            if (!TryGetComponent(out DropTheManLevelEditorRuntimeController runtimeController))
            {
                runtimeController = gameObject.AddComponent<DropTheManLevelEditorRuntimeController>();
            }

            runtimeController.BindToBoardController(this);
        }

        private Transform EnsureChildRoot(Transform currentRoot, string rootName)
        {
            if (currentRoot != null)
            {
                currentRoot.SetParent(transform, worldPositionStays: false);
                currentRoot.localPosition = Vector3.zero;
                currentRoot.localRotation = Quaternion.identity;
                currentRoot.localScale = Vector3.one;
                return currentRoot;
            }

            Transform existing = transform.Find(rootName);
            if (existing != null)
            {
                return existing;
            }

            GameObject rootObject = new(rootName);
            rootObject.transform.SetParent(transform, worldPositionStays: false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            return rootObject.transform;
        }

        private void RebuildBoardVisuals(GridWorldLayout worldLayout)
        {
            if (config != null && config.editorCellPrefab != null)
            {
                RebuildModularBoardVisuals(worldLayout);
                return;
            }

            DestroyChildren(boardVisualRoot);

            Quaternion boardRotation = BuildBoardRotation(worldLayout);
            Vector3 rootScale = new(cellSize.x, 1f, cellSize.y);

            for (int y = 0; y < _authoringSession.Height; y++)
            {
                for (int x = 0; x < _authoringSession.Width; x++)
                {
                    GameObject cellObject = CreateCellVisualObject();
                    cellObject.transform.SetParent(boardVisualRoot, worldPositionStays: false);

                    DropTheManEditorCellView cellView =
                        cellObject.GetComponent<DropTheManEditorCellView>() ??
                        cellObject.AddComponent<DropTheManEditorCellView>();

                    Vector2Int coordinate = new(x, y);
                    Vector3 center = GetCellCenterWorld(worldLayout, coordinate);
                    cellView.Initialize(coordinate, center, boardRotation, rootScale);
                    cellView.ApplyVisuals(
                        IsBlocked(coordinate),
                        IsDarkCheckerCell(coordinate) ? DarkCellColor : LightCellColor,
                        BlockedFallbackColor,
                        config != null ? config.blockedCellMaterial : null);
                }
            }
        }

        private GameObject CreateCellVisualObject()
        {
            return new GameObject("EditorCell");
        }

        private void RebuildModularBoardVisuals(GridWorldLayout worldLayout)
        {
            try
            {
                WallGenerationResult boundary = _authoringSession.CreateBoundary(
                    state => state == AuthoredCellState.Active);
                ModularBoardVisualPlan plan =
                    new ModularBoardVisualPlanner().CreatePlan(boundary);
                ModularBoardVisualBuilder builder = new();

                if (!builder.TryRebuild(
                        plan,
                        config.editorCellPrefab,
                        boardVisualRoot,
                        worldLayout,
                        0f,
                        out _,
                        out string failureReason))
                {
                    Debug.LogWarning(
                        $"Drop The Man editor board visual rebuild failed: {failureReason}",
                        this);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Drop The Man editor board visual rebuild failed: {exception.Message}",
                    this);
            }
        }

        private void RebuildPlacementVisuals(GridWorldLayout worldLayout)
        {
            DestroyChildren(placementVisualRoot);
            RebuildStickmanVisuals(worldLayout);
            RebuildHoleVisuals(worldLayout);
        }

        private void RebuildStickmanVisuals(GridWorldLayout worldLayout)
        {
            for (int i = 0; i < levelData.Stickmen.Count; i++)
            {
                DropTheManDevStickmanData stickman = levelData.Stickmen[i];
                GameObject rootObject = new($"Stickman_{stickman.Id}");
                rootObject.transform.SetParent(placementVisualRoot, worldPositionStays: false);
                rootObject.transform.SetPositionAndRotation(
                    GetCellCenterWorld(worldLayout, stickman.Coordinate) +
                    GetBoardNormal(worldLayout) * 0.20f,
                    Quaternion.identity);

                if (config != null && config.collectableViewPrefab != null)
                {
                    GameObject preview = Instantiate(config.collectableViewPrefab.gameObject);
                    preview.transform.SetParent(rootObject.transform, worldPositionStays: false);
                    preview.transform.localPosition = Vector3.zero;
                    preview.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    GameObject preview = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    preview.name = "StickmanPreview";
                    preview.transform.SetParent(rootObject.transform, worldPositionStays: false);
                    preview.transform.localPosition = Vector3.zero;
                    preview.transform.localRotation = Quaternion.identity;
                    preview.transform.localScale = new Vector3(0.35f, 0.55f, 0.35f);

                    Collider collider = preview.GetComponent<Collider>();
                    if (collider != null)
                    {
                        collider.enabled = false;
                    }
                }

                ApplyColorToRenderers(
                    rootObject.GetComponentsInChildren<Renderer>(includeInactive: true),
                    stickman.ColorIdentity);
            }
        }

        private void RebuildHoleVisuals(GridWorldLayout worldLayout)
        {
            for (int i = 0; i < levelData.Holes.Count; i++)
            {
                DropTheManDevHoleData hole = levelData.Holes[i];
                if (TryCreateConcreteHolePreview(hole, worldLayout, out GameObject holePreview))
                {
                    holePreview.name = $"Hole_{hole.Id}";
                    holePreview.transform.SetParent(placementVisualRoot, worldPositionStays: true);
                    continue;
                }

                CreateFallbackHolePreview(hole, worldLayout);
            }
        }

        private void ApplyColorToRenderers(Renderer[] renderers, ColorIdentity colorIdentity)
        {
            if (renderers == null)
            {
                return;
            }

            ColorIdentity normalized = NormalizeColorIdentity(colorIdentity);
            Material material = ResolveColorMaterial(normalized);
            Color fallbackColor = ResolveSlotColor(normalized);
            MaterialPropertyBlock propertyBlock = new();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (material != null)
                {
                    renderer.sharedMaterial = material;
                    renderer.SetPropertyBlock(null);
                    continue;
                }

                propertyBlock.Clear();
                propertyBlock.SetColor("_BaseColor", fallbackColor);
                propertyBlock.SetColor("_Color", fallbackColor);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void DestroyChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                    continue;
                }

                DestroyImmediate(child);
            }
        }

        private bool ToggleBlockedCell(Vector2Int coordinate)
        {
            GridCoordinate cell = new(coordinate.x, coordinate.y);
            if (_authoringSession.GetCellState(cell) == AuthoredCellState.Blocked)
            {
                if (!_authoringSession.TrySetCellState(cell, AuthoredCellState.Active).Success)
                    return false;
                levelData.BlockedCells.Remove(coordinate);
                return true;
            }

            AuthoringEditResult result = _authoringSession.TrySetCellState(
                cell, AuthoredCellState.Blocked);
            if (result.Success)
            {
                levelData.BlockedCells.Add(coordinate);
                return true;
            }
            Debug.LogWarning($"Cannot block coordinate {coordinate}: {result.Reason} " +
                $"Affected: {string.Join(", ", result.AffectedItemIds)}.", this);
            return false;
        }

        private bool EraseBlockedCell(Vector2Int coordinate)
        {
            if (_authoringSession.GetCellState(new GridCoordinate(coordinate.x, coordinate.y)) !=
                AuthoredCellState.Blocked) return false;
            AuthoringEditResult result = _authoringSession.TrySetCellState(
                new GridCoordinate(coordinate.x, coordinate.y), AuthoredCellState.Active);
            return result.Success && levelData.BlockedCells.Remove(coordinate);
        }

        private bool PlaceStickman(Vector2Int coordinate)
        {
            DropTheManDevStickmanData existingStickman = FindStickmanAt(coordinate);
            if (existingStickman != null)
            {
                existingStickman.ColorIdentity = NormalizeColorIdentity(selectedColor);
                _authoringSession.SelectItem(existingStickman.Id);
                return true;
            }

            string id = GenerateUniqueId("stickman");
            AuthoringEditResult result = _authoringSession.TryPlaceOrMove(new AuthoredFootprint(
                id, new GridCoordinate(coordinate.x, coordinate.y),
                new[] { new GridCoordinate(0, 0) }));
            if (!result.Success)
            {
                Debug.LogWarning($"Cannot place a stickman at {coordinate}: {result.Reason}", this);
                return false;
            }
            levelData.Stickmen.Add(new DropTheManDevStickmanData
            {
                Id = id,
                Coordinate = coordinate,
                ColorIdentity = NormalizeColorIdentity(selectedColor)
            });
            _authoringSession.SelectItem(id);
            return true;
        }

        private bool PlaceHole(Vector2Int coordinate)
        {
            DropTheManEditorHolePaletteEntry paletteEntry = ResolveSelectedHolePaletteEntry();
            if (paletteEntry == null)
            {
                Debug.LogWarning(
                    "Cannot place a hole because no hole palette entries are configured.",
                    this);
                return false;
            }

            List<Vector2Int> rotatedOffsets = ResolveFootprintOffsets(paletteEntry.footprintOffsets);

            DropTheManDevHoleData existingOriginHole = FindHoleByOrigin(coordinate);
            string id = existingOriginHole != null
                ? existingOriginHole.Id : GenerateUniqueId("hole");
            AuthoringEditResult placement = _authoringSession.TryPlaceOrMove(new AuthoredFootprint(
                id, new GridCoordinate(coordinate.x, coordinate.y), ToGridOffsets(rotatedOffsets)));
            if (!placement.Success)
            {
                Debug.LogWarning($"Cannot place hole at {coordinate}: {placement.Reason}", this);
                return false;
            }

            if (existingOriginHole != null)
            {
                existingOriginHole.ColorIdentity = NormalizeColorIdentity(selectedColor);
                existingOriginHole.FootprintOffsets = rotatedOffsets;
                _authoringSession.SelectItem(id);
                return true;
            }

            levelData.Holes.Add(
                new DropTheManDevHoleData
                {
                    Id = id,
                    Coordinate = coordinate,
                    ColorIdentity = NormalizeColorIdentity(selectedColor),
                    FootprintOffsets = rotatedOffsets
                });
            _authoringSession.SelectItem(id);
            return true;
        }

        private bool RotateHoleAtOrContaining(Vector2Int coordinate)
        {
            GridCoordinate cell = new(coordinate.x, coordinate.y);
            if (!_authoringSession.TryFindItemAtCell(cell, out AuthoredFootprint selected))
                return false;
            DropTheManDevHoleData hole = FindHoleById(selected.Id);
            if (hole == null)
            {
                return false;
            }

            _authoringSession.SelectItem(selected.Id);
            AuthoringEditResult rotation = _authoringSession.TryRotateItemClockwise(selected.Id);
            if (!rotation.Success)
            {
                Debug.LogWarning($"Cannot rotate hole '{hole.Id}': {rotation.Reason}", this);
                return false;
            }
            _authoringSession.TryGetItem(selected.Id, out AuthoredFootprint rotated);
            hole.FootprintOffsets = ToVectorOffsets(rotated.Offsets);
            return true;
        }

        private bool RemoveStickmanAt(Vector2Int coordinate)
        {
            for (int i = 0; i < levelData.Stickmen.Count; i++)
            {
                if (levelData.Stickmen[i].Coordinate == coordinate)
                {
                    if (!_authoringSession.Erase(levelData.Stickmen[i].Id)) return false;
                    levelData.Stickmen.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private bool RemoveHoleAtOrContaining(Vector2Int coordinate)
        {
            if (!_authoringSession.TryFindItemAtCell(new GridCoordinate(coordinate.x, coordinate.y),
                    out AuthoredFootprint item)) return false;
            DropTheManDevHoleData hole = FindHoleById(item.Id);
            if (hole == null || !_authoringSession.Erase(item.Id)) return false;
            levelData.Holes.Remove(hole);
            return true;
        }

        private DropTheManDevStickmanData FindStickmanAt(Vector2Int coordinate)
        {
            for (int i = 0; i < levelData.Stickmen.Count; i++)
            {
                if (levelData.Stickmen[i].Coordinate == coordinate)
                {
                    return levelData.Stickmen[i];
                }
            }

            return null;
        }

        private DropTheManDevHoleData FindHoleByOrigin(Vector2Int coordinate)
        {
            for (int i = 0; i < levelData.Holes.Count; i++)
            {
                if (levelData.Holes[i].Coordinate == coordinate)
                {
                    return levelData.Holes[i];
                }
            }

            return null;
        }

        private DropTheManDevHoleData FindHoleById(string id)
        {
            foreach (DropTheManDevHoleData hole in levelData.Holes)
                if (hole.Id == id) return hole;
            return null;
        }

        private DropTheManDevHoleData FindHoleContaining(
            Vector2Int coordinate,
            DropTheManDevHoleData ignoredHole = null)
        {
            for (int i = 0; i < levelData.Holes.Count; i++)
            {
                if (ReferenceEquals(levelData.Holes[i], ignoredHole))
                {
                    continue;
                }

                if (HoleContainsCoordinate(levelData.Holes[i], coordinate))
                {
                    return levelData.Holes[i];
                }
            }

            return null;
        }

        private bool HoleContainsCoordinate(DropTheManDevHoleData hole, Vector2Int coordinate)
        {
            IReadOnlyList<Vector2Int> offsets = ResolveFootprintOffsets(hole.FootprintOffsets);
            for (int i = 0; i < offsets.Count; i++)
            {
                if (hole.Coordinate + offsets[i] == coordinate)
                {
                    return true;
                }
            }

            return false;
        }

        private bool DoesHoleFit(DropTheManDevHoleData hole, DropTheManDevHoleData ignoredHole)
        {
            IReadOnlyList<Vector2Int> offsets = ResolveFootprintOffsets(hole.FootprintOffsets);
            for (int i = 0; i < offsets.Count; i++)
            {
                Vector2Int coordinate = hole.Coordinate + offsets[i];
                if (!IsWithinBoard(coordinate))
                {
                    return false;
                }

                if (FindHoleContaining(coordinate, ignoredHole) is { } overlapping &&
                    !ReferenceEquals(overlapping, hole))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsBlocked(Vector2Int coordinate)
        {
            return _authoringSession != null
                ? _authoringSession.GetCellState(new GridCoordinate(coordinate.x, coordinate.y)) ==
                  AuthoredCellState.Blocked
                : levelData.BlockedCells.Contains(coordinate);
        }

        private bool IsWithinBoard(Vector2Int coordinate)
        {
            return coordinate.x >= 0 &&
                   coordinate.x < (_authoringSession?.Width ?? levelData.BoardWidth) &&
                   coordinate.y >= 0 &&
                   coordinate.y < (_authoringSession?.Height ?? levelData.BoardHeight);
        }

        private bool TryCreateWorldLayout(
            out GridWorldLayout worldLayout,
            out string failureReason)
        {
            try
            {
                worldLayout = new GridWorldLayout(
                    transform.position,
                    cellSize,
                    boardGridXAxis,
                    boardGridYAxis,
                    GridCellAnchor.Center);
                failureReason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                worldLayout = default;
                failureReason = $"Drop The Man editor board layout is invalid: {exception.Message}";
                return false;
            }
        }

        private Vector3 GetCellCenterWorld(GridWorldLayout worldLayout, Vector2Int coordinate)
        {
            return worldLayout.CellCenterToWorld(new GridCoordinate(coordinate.x, coordinate.y));
        }

        private void PositionEditorCamera(GridWorldLayout worldLayout)
        {
            Camera targetCamera = framingCamera != null
                ? framingCamera
                : Camera.main;
            if (targetCamera == null)
            {
                return;
            }

            if (!DropTheManBoardCameraPositioner.TryPositionCamera(
                    targetCamera,
                    worldLayout,
                    levelData.BoardWidth,
                    levelData.BoardHeight,
                    cameraFramingPadding,
                    minimumCameraDistance,
                    out string failureReason))
            {
                Debug.LogWarning(
                    $"Drop The Man editor camera was not repositioned: {failureReason}",
                    this);
            }
        }

        private Vector3 BuildHoleWorldPosition(
            GridWorldLayout worldLayout,
            Vector2Int coordinate,
            float visualHeight)
        {
            Vector3 worldPosition = GetCellCenterWorld(worldLayout, coordinate);
            worldPosition.y = visualHeight;
            return worldPosition;
        }

        private static Quaternion BuildBoardRotation(GridWorldLayout worldLayout)
        {
            Vector3 normal = GetBoardNormal(worldLayout);
            return Quaternion.LookRotation(worldLayout.BoardYAxis, normal);
        }

        private static Quaternion BuildHoleRotation(
            GridWorldLayout worldLayout,
            int quarterTurns)
        {
            return BuildBoardRotation(worldLayout) * Quaternion.Euler(0f, quarterTurns * 90f, 0f);
        }

        private static Vector3 GetBoardNormal(GridWorldLayout worldLayout)
        {
            return Vector3.Cross(worldLayout.BoardYAxis, worldLayout.BoardXAxis).normalized;
        }

        private static bool IsDarkCheckerCell(Vector2Int coordinate)
        {
            return ((coordinate.x + coordinate.y) & 1) == 1;
        }

        private static bool OffsetSetsMatch(
            IReadOnlyList<Vector2Int> left,
            IReadOnlyList<Vector2Int> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            HashSet<Vector2Int> rightOffsets = new();
            for (int i = 0; i < right.Count; i++)
            {
                rightOffsets.Add(right[i]);
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!rightOffsets.Contains(left[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private Material ResolveColorMaterial(ColorIdentity colorIdentity)
        {
            if (config == null || config.colorSlotMaterials == null)
            {
                return null;
            }

            int slotIndex = GetSlotIndex(NormalizeColorIdentity(colorIdentity));
            return slotIndex >= 0 && slotIndex < config.colorSlotMaterials.Count
                ? config.colorSlotMaterials[slotIndex]
                : null;
        }

        private DropTheManEditorHolePaletteEntry ResolveSelectedHolePaletteEntry()
        {
            if (config == null || config.holePaletteEntries == null || config.holePaletteEntries.Count == 0)
            {
                return null;
            }

            selectedHolePaletteIndex = Mathf.Clamp(
                selectedHolePaletteIndex,
                0,
                config.holePaletteEntries.Count - 1);
            return config.holePaletteEntries[selectedHolePaletteIndex];
        }

        private bool TryCreateConcreteHolePreview(
            DropTheManDevHoleData hole,
            GridWorldLayout worldLayout,
            out GameObject holePreview)
        {
            holePreview = null;

            if (!TryResolveHolePaletteEntryForAuthoredHole(
                    hole,
                    out DropTheManEditorHolePaletteEntry paletteEntry,
                    out int quarterTurns))
            {
                return false;
            }

            if (paletteEntry.previewPrefab == null)
            {
                Debug.LogWarning(
                    $"Drop The Man editor hole palette entry '{paletteEntry.id}' is missing a preview prefab.",
                    this);
                return false;
            }

            holePreview = Instantiate(paletteEntry.previewPrefab);
            holePreview.transform.SetPositionAndRotation(
                BuildHoleWorldPosition(
                    worldLayout,
                    hole.Coordinate,
                    paletteEntry.previewPrefab.transform.position.y),
                BuildHoleRotation(worldLayout, quarterTurns));
            DropTheManViewPresentationUtility.ApplyColor(
                holePreview.GetComponentsInChildren<Renderer>(includeInactive: true),
                hole.ColorIdentity);
            return true;
        }

        private void CreateFallbackHolePreview(
            DropTheManDevHoleData hole,
            GridWorldLayout worldLayout)
        {
            Quaternion boardRotation = BuildBoardRotation(worldLayout);
            GameObject holeRoot = new($"Hole_{hole.Id}");
            holeRoot.transform.SetParent(placementVisualRoot, worldPositionStays: false);
            holeRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            IReadOnlyList<Vector2Int> footprintOffsets = ResolveFootprintOffsets(hole.FootprintOffsets);
            for (int offsetIndex = 0; offsetIndex < footprintOffsets.Count; offsetIndex++)
            {
                Vector2Int cellCoordinate = hole.Coordinate + footprintOffsets[offsetIndex];
                GameObject footprintTile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                footprintTile.name = $"Footprint_{offsetIndex}";
                footprintTile.transform.SetParent(holeRoot.transform, worldPositionStays: false);
                footprintTile.transform.SetPositionAndRotation(
                    GetCellCenterWorld(worldLayout, cellCoordinate) +
                    GetBoardNormal(worldLayout) * 0.08f,
                    boardRotation);
                footprintTile.transform.localScale =
                    new Vector3(cellSize.x * 0.72f, 0.06f, cellSize.y * 0.72f);

                Collider collider = footprintTile.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }

            ApplyColorToRenderers(
                holeRoot.GetComponentsInChildren<Renderer>(includeInactive: true),
                hole.ColorIdentity);
        }

        private bool TryResolveHolePaletteEntryForAuthoredHole(
            DropTheManDevHoleData hole,
            out DropTheManEditorHolePaletteEntry paletteEntry,
            out int quarterTurns)
        {
            paletteEntry = null;
            quarterTurns = 0;

            if (hole == null || config == null || config.holePaletteEntries == null)
            {
                return false;
            }

            List<Vector2Int> authoredOffsets = ResolveFootprintOffsets(hole.FootprintOffsets);
            for (int entryIndex = 0; entryIndex < config.holePaletteEntries.Count; entryIndex++)
            {
                DropTheManEditorHolePaletteEntry candidate = config.holePaletteEntries[entryIndex];
                if (candidate == null)
                {
                    continue;
                }

                List<Vector2Int> candidateOffsets =
                    ResolveFootprintOffsets(candidate.footprintOffsets);
                for (int candidateQuarterTurns = 0; candidateQuarterTurns < 4; candidateQuarterTurns++)
                {
                    if (!OffsetSetsMatch(
                            RotateOffsets(candidateOffsets, candidateQuarterTurns),
                            authoredOffsets))
                    {
                        continue;
                    }

                    paletteEntry = candidate;
                    quarterTurns = candidateQuarterTurns;
                    return true;
                }
            }

            Debug.LogWarning(
                $"Drop The Man editor could not resolve a concrete hole preview for '{hole.Id}'. Falling back to footprint blocks.",
                this);
            return false;
        }

        private static List<Vector2Int> ResolveFootprintOffsets(IReadOnlyList<Vector2Int> sourceOffsets)
        {
            HashSet<Vector2Int> uniqueOffsets = new();
            List<Vector2Int> offsets = new();

            if (sourceOffsets != null)
            {
                for (int i = 0; i < sourceOffsets.Count; i++)
                {
                    if (uniqueOffsets.Add(sourceOffsets[i]))
                    {
                        offsets.Add(sourceOffsets[i]);
                    }
                }
            }

            if (!uniqueOffsets.Contains(Vector2Int.zero))
            {
                offsets.Insert(0, Vector2Int.zero);
            }

            if (offsets.Count == 0)
            {
                offsets.Add(Vector2Int.zero);
            }

            return offsets;
        }

        private static List<Vector2Int> RotateOffsets(
            IReadOnlyList<Vector2Int> offsets,
            int quarterTurns)
        {
            IReadOnlyList<Vector2Int> normalized = ResolveFootprintOffsets(offsets);
            List<GridCoordinate> rotated = new(normalized.Count);
            foreach (Vector2Int offset in normalized)
                rotated.Add(new GridCoordinate(offset.x, offset.y));
            for (int turn = 0; turn < quarterTurns; turn++)
                rotated = new List<GridCoordinate>(LevelAuthoringCore.RotateClockwise(rotated));
            List<Vector2Int> result = new(rotated.Count);
            foreach (GridCoordinate offset in rotated)
            {
                result.Add(new Vector2Int(offset.X, offset.Y));
            }
            return result;
        }

        private string GenerateUniqueId(string prefix)
        {
            int suffix = 1;
            string candidate = $"{prefix}_{suffix:000}";
            while (_authoringSession.TryGetItem(candidate, out _))
            {
                suffix++;
                candidate = $"{prefix}_{suffix:000}";
            }

            return candidate;
        }

        private static DropTheManDevLevelData CloneLevelData(DropTheManDevLevelData source)
        {
            DropTheManDevLevelData clone = new()
            {
                LevelId = source?.LevelId ?? "Level 1",
                DisplayName = source?.DisplayName ?? source?.LevelId ?? "Level 1",
                BoardWidth = Mathf.Max(1, source?.BoardWidth ?? 5),
                BoardHeight = Mathf.Max(1, source?.BoardHeight ?? 5),
                TimerEnabled = source != null && source.TimerEnabled,
                TimerDurationSeconds = source?.TimerDurationSeconds ?? 60f,
                TimerWarningThresholdSeconds = source?.TimerWarningThresholdSeconds ?? 0f,
                BlockedCells = new List<Vector2Int>(),
                Holes = new List<DropTheManDevHoleData>(),
                Stickmen = new List<DropTheManDevStickmanData>()
            };

            if (source?.BlockedCells != null)
            {
                clone.BlockedCells.AddRange(source.BlockedCells);
            }

            if (source?.Holes != null)
            {
                for (int i = 0; i < source.Holes.Count; i++)
                {
                    DropTheManDevHoleData sourceHole = source.Holes[i];
                    if (sourceHole == null)
                    {
                        continue;
                    }

                    clone.Holes.Add(
                        new DropTheManDevHoleData
                        {
                            Id = sourceHole.Id,
                            Coordinate = sourceHole.Coordinate,
                            ColorIdentity = sourceHole.ColorIdentity,
                            FootprintOffsets = sourceHole.FootprintOffsets != null
                                ? new List<Vector2Int>(sourceHole.FootprintOffsets)
                                : new List<Vector2Int> { Vector2Int.zero }
                        });
                }
            }

            if (source?.Stickmen != null)
            {
                for (int i = 0; i < source.Stickmen.Count; i++)
                {
                    DropTheManDevStickmanData sourceStickman = source.Stickmen[i];
                    if (sourceStickman == null)
                    {
                        continue;
                    }

                    clone.Stickmen.Add(
                        new DropTheManDevStickmanData
                        {
                            Id = sourceStickman.Id,
                            Coordinate = sourceStickman.Coordinate,
                            ColorIdentity = sourceStickman.ColorIdentity
                        });
                }
            }

            return clone;
        }

        public static bool TryMapKeyCodeToColorSlot(KeyCode keyCode, out ColorIdentity colorIdentity)
        {
            int digit = keyCode switch
            {
                KeyCode.Alpha0 => 0,
                KeyCode.Alpha1 => 1,
                KeyCode.Alpha2 => 2,
                KeyCode.Alpha3 => 3,
                KeyCode.Alpha4 => 4,
                KeyCode.Alpha5 => 5,
                KeyCode.Alpha6 => 6,
                KeyCode.Alpha7 => 7,
                KeyCode.Alpha8 => 8,
                KeyCode.Alpha9 => 9,
                KeyCode.Keypad0 => 0,
                KeyCode.Keypad1 => 1,
                KeyCode.Keypad2 => 2,
                KeyCode.Keypad3 => 3,
                KeyCode.Keypad4 => 4,
                KeyCode.Keypad5 => 5,
                KeyCode.Keypad6 => 6,
                KeyCode.Keypad7 => 7,
                KeyCode.Keypad8 => 8,
                KeyCode.Keypad9 => 9,
                _ => -1
            };

            if (digit < 0)
            {
                colorIdentity = ColorIdentity.None;
                return false;
            }

            colorIdentity = (ColorIdentity)((int)ColorIdentity.Slot0 + digit);
            return true;
        }

        private static ColorIdentity NormalizeColorIdentity(ColorIdentity colorIdentity)
        {
            if (colorIdentity == ColorIdentity.None)
            {
                return ColorIdentity.Slot0;
            }

            int slotIndex = Mathf.Clamp(GetSlotIndex(colorIdentity), 0, SlotColors.Length - 1);
            return (ColorIdentity)((int)ColorIdentity.Slot0 + slotIndex);
        }

        private static int GetSlotIndex(ColorIdentity colorIdentity)
        {
            int rawValue = (int)colorIdentity;
            return Mathf.Clamp(rawValue - (int)ColorIdentity.Slot0, 0, SlotColors.Length - 1);
        }

        private static Color ResolveSlotColor(ColorIdentity colorIdentity)
        {
            return SlotColors[GetSlotIndex(colorIdentity)];
        }
    }
}
