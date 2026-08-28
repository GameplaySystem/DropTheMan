using PuzzleFramework.Content;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.Presentation;
using PuzzleFramework.RuntimeConstruction;
using PuzzleFramework.RuntimeFlow;
using DropAwayPrototype.Editor;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using System;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Dev-only gameplay-scene bridge that builds a Drop The Man runtime model, initializes
    /// the pre-wired scene controller, and can drive a Resources-backed level catalog plus
    /// temporary result HUD. This is not production level loading or progression.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManDevSceneBootstrapper : MonoBehaviour
    {
        private const int ResultWindowId = 104201;
        private static readonly Rect DefaultResultWindowRect = new(20f, 20f, 260f, 150f);

        [SerializeField] private DropTheManSceneController sceneController;
        [SerializeField] private bool bootstrapOnStart = true;
        [SerializeField] private DropTheManDevLevelSource levelSource =
            DropTheManDevLevelSource.ResourcesCatalog;
        [SerializeField] private string resourcesLevelCatalogPath = "DropTheMan/Levels";
        [SerializeField, Min(0)] private int startingLevelIndex;
        [FormerlySerializedAs("boardWorldOrigin")]
        [SerializeField] private Vector3 boardWorldCenter = Vector3.zero;
        [SerializeField] private Vector2 cellSize = Vector2.one;
        [SerializeField] private Vector3 boardGridXAxis = Vector3.right;
        [SerializeField] private Vector3 boardGridYAxis = Vector3.forward;
        [SerializeField] private DropTheManEditorConfig visualConfig;
        [SerializeField] private Transform runtimeViewSpawnRoot;
        [Header("Camera Framing")]
        [SerializeField, Min(1f)] private float cameraFramingPadding = 1.15f;
        [SerializeField, Min(0.01f)] private float minimumCameraDistance = 1f;
        [Header("Generated Board Visuals")]
        [SerializeField] private ModularBoardCellView boardCellVisualPrefab;
        [SerializeField] private Transform boardVisualSpawnRoot;
        [SerializeField] private float boardVisualNormalOffset;
        [SerializeField] private DropTheManDevLevelData levelData =
            DropTheManDevLevelData.CreateDefault();

        public DropTheManRuntimeModel RuntimeModel { get; private set; }
        public RuntimeLevelContext FrameworkContext { get; private set; }

        private LevelCatalog _levelCatalog;
        private int _currentLevelIndex = -1;
        private string _currentLevelId = string.Empty;
        private string _currentDisplayName = string.Empty;
        private LevelResultState _resultState;
        private bool _resultWindowVisible;
        private Rect _resultWindowRect = DefaultResultWindowRect;

        private void Awake()
        {
            if (sceneController == null)
            {
                TryGetComponent(out sceneController);
            }
        }

        private void Start()
        {
            if (bootstrapOnStart)
            {
                Bootstrap();
            }
        }

        private void Update()
        {
            if (_resultWindowVisible || sceneController == null)
            {
                return;
            }

            DropTheManRuntimeController controller = sceneController.RuntimeController;
            if (controller == null || !controller.TerminalOutcomeAccepted)
            {
                return;
            }

            if (controller.CurrentGameState == GameState.Won)
            {
                _resultState = LevelResultState.Completed;
                _resultWindowVisible = true;
                return;
            }

            if (controller.CurrentGameState == GameState.Lost)
            {
                _resultState = LevelResultState.Failed;
                _resultWindowVisible = true;
            }
        }

        private void OnGUI()
        {
            if (!_resultWindowVisible)
            {
                return;
            }

            _resultWindowRect = GUI.Window(
                ResultWindowId,
                _resultWindowRect,
                DrawResultWindow,
                ResolveResultWindowTitle());
        }

        [ContextMenu("Bootstrap Drop The Man Dev Scene")]
        public void Bootstrap()
        {
            if (!TryBootstrap(out string failureReason))
            {
                Debug.LogError(failureReason, this);
            }
        }

        public bool TryBootstrap(out string failureReason)
        {
            if (levelSource == DropTheManDevLevelSource.ResourcesCatalog)
            {
                if (!CanUseLevelCatalog(out failureReason) ||
                    !TryDiscoverLevelCatalog(out failureReason))
                {
                    return false;
                }

                return TryLoadLevelAtIndex(ResolveStartingLevelIndex(), out failureReason);
            }

            _levelCatalog = null;
            _currentLevelIndex = -1;
            return TryBootstrapConfiguredSource(out failureReason);
        }

        /// <summary>
        /// Reloads the current gameplay level through the same runtime bootstrap path.
        /// Clean reload currently requires runtime-spawned views.
        /// </summary>
        public bool TryRestartCurrentLevel(out string failureReason)
        {
            if (!CanReloadCurrentLevel(out failureReason))
            {
                return false;
            }

            if (levelSource == DropTheManDevLevelSource.ResourcesCatalog)
            {
                if (!HasLevelCatalog())
                {
                    failureReason = "No level catalog has been discovered.";
                    return false;
                }

                int restartIndex = IsValidLevelCatalogIndex(_currentLevelIndex)
                    ? _currentLevelIndex
                    : ResolveStartingLevelIndex();
                return TryLoadLevelAtIndex(restartIndex, out failureReason);
            }

            return TryBootstrapConfiguredSource(out failureReason);
        }

        /// <summary>
        /// Loads the next JSON level from the discovered Resources catalog.
        /// This remains prototype-owned test-scene flow rather than framework progression.
        /// </summary>
        public bool TryLoadNextLevel(out string failureReason)
        {
            if (!CanUseLevelCatalog(out failureReason))
            {
                return false;
            }

            if (!HasLevelCatalog())
            {
                failureReason = "No level catalog has been discovered.";
                return false;
            }

            int nextLevelIndex = _currentLevelIndex + 1;
            if (!IsValidLevelCatalogIndex(nextLevelIndex))
            {
                failureReason = "No next level exists in the discovered catalog.";
                return false;
            }

            return TryLoadLevelAtIndex(nextLevelIndex, out failureReason);
        }

        private bool TryLoadLevelAtIndex(int levelIndex, out string failureReason)
        {
            if (!HasLevelCatalog())
            {
                failureReason = "No level catalog has been discovered.";
                return false;
            }

            if (!IsValidLevelCatalogIndex(levelIndex))
            {
                failureReason =
                    $"Level catalog index {levelIndex} is outside the discovered range.";
                return false;
            }

            if (!_levelCatalog.TryGetEntry(levelIndex, out LevelCatalogEntry catalogEntry) ||
                catalogEntry.Asset == null)
            {
                failureReason = $"Level catalog entry {levelIndex} is unavailable.";
                return false;
            }

            bool loaded = TryBootstrapFromProvider(
                new DropTheManJsonLevelDefinitionProvider(catalogEntry.Asset),
                out LevelDefinition levelDefinition,
                out failureReason);
            if (!loaded)
            {
                return false;
            }

            _currentLevelIndex = levelIndex;
            CaptureLoadedLevelMetadata(levelDefinition);
            return true;
        }

        private bool TryBootstrapConfiguredSource(out string failureReason)
        {
            IDropTheManLevelDefinitionProvider provider = CreateLevelDefinitionProvider();
            bool loaded = TryBootstrapFromProvider(
                provider,
                out LevelDefinition levelDefinition,
                out failureReason);
            if (!loaded)
            {
                return false;
            }

            CaptureLoadedLevelMetadata(levelDefinition);
            return true;
        }

        private bool TryBootstrapFromProvider(
            IDropTheManLevelDefinitionProvider provider,
            out LevelDefinition levelDefinition,
            out string failureReason)
        {
            ResetLoadState();

            if (sceneController == null)
            {
                levelDefinition = null;
                failureReason = "Drop The Man scene controller reference is required.";
                return false;
            }

            if (cellSize.x <= 0f || cellSize.y <= 0f)
            {
                levelDefinition = null;
                failureReason = "Dev scene cell size must be positive.";
                return false;
            }

            if (!provider.TryGetLevelDefinition(
                    out levelDefinition,
                    out failureReason))
            {
                return false;
            }

            RuntimeLevelBuildResult frameworkBuildResult =
                new LevelRuntimeBuilder(new LevelRuntimeConstructionValidator())
                    .Build(levelDefinition);
            if (!frameworkBuildResult.Success || frameworkBuildResult.Context == null)
            {
                failureReason = frameworkBuildResult.FailureReason;
                return false;
            }

            DropTheManRuntimeModelBuildResult modelBuildResult =
                new DropTheManRuntimeModelBuilder().Build(
                    levelDefinition,
                    frameworkBuildResult.Context);
            if (!modelBuildResult.Success || modelBuildResult.RuntimeModel == null)
            {
                failureReason = modelBuildResult.FailureReason;
                return false;
            }

            if (!TryCreateTimer(
                    levelDefinition.FrameworkData.Timer,
                    out TimerSystem timerSystem,
                    out failureReason))
            {
                return false;
            }

            GridWorldLayout worldLayout;
            try
            {
                worldLayout = GridWorldLayout.CreateCentered(
                    boardWorldCenter,
                    frameworkBuildResult.Context.BoardData.Width,
                    frameworkBuildResult.Context.BoardData.Height,
                    cellSize,
                    boardGridXAxis,
                    boardGridYAxis);
            }
            catch (System.Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }

            PositionGameplayCamera(
                worldLayout,
                frameworkBuildResult.Context.BoardData.Width,
                frameworkBuildResult.Context.BoardData.Height);

            if (!TryRebuildBoardVisuals(
                    frameworkBuildResult.Context.BoardData,
                    worldLayout,
                    out failureReason))
            {
                return false;
            }

            DropTheManRuntimeControllerResult sceneInitializeResult =
                InitializeSceneController(
                    modelBuildResult.RuntimeModel,
                    worldLayout,
                    timerSystem,
                    out failureReason);
            if (!sceneInitializeResult.Success)
            {
                return false;
            }

            FrameworkContext = frameworkBuildResult.Context;
            RuntimeModel = modelBuildResult.RuntimeModel;
            failureReason = string.Empty;
            return true;
        }

        [ContextMenu("Log Drop The Man Inspector Level JSON")]
        public void LogInspectorLevelJson()
        {
            if (!TryExportInspectorLevelJson(out string jsonText, out string failureReason))
            {
                Debug.LogError(failureReason, this);
                return;
            }

            Debug.Log(jsonText, this);
        }

        public bool TryExportInspectorLevelJson(
            out string jsonText,
            out string failureReason)
        {
            return DropTheManJsonLevelDefinitionProvider.TryExportDevLevelDataToJson(
                levelData,
                out jsonText,
                out failureReason);
        }

        private DropTheManRuntimeControllerResult InitializeSceneController(
            DropTheManRuntimeModel runtimeModel,
            GridWorldLayout worldLayout,
            TimerSystem timerSystem,
            out string failureReason)
        {
            if (!TrySpawnRuntimeViews(
                    runtimeModel,
                    worldLayout,
                    out DropTheManHoleView[] runtimeHoleViews,
                    out DropTheManStickmanView[] runtimeStickmanViews,
                    out failureReason))
            {
                return DropTheManRuntimeControllerResult.Failed(failureReason);
            }

            sceneController.SetRuntimeViews(runtimeHoleViews, runtimeStickmanViews);

            DropTheManRuntimeControllerResult initializeResult =
                sceneController.Initialize(
                    runtimeModel,
                    worldLayout,
                    timerSystem);
            failureReason = initializeResult.FailureReason;
            return initializeResult;
        }

        private void PositionGameplayCamera(
            GridWorldLayout worldLayout,
            int boardWidth,
            int boardHeight)
        {
            Camera targetCamera = sceneController != null
                ? sceneController.InputCamera
                : null;
            if (!DropTheManBoardCameraPositioner.TryPositionCamera(
                    targetCamera,
                    worldLayout,
                    boardWidth,
                    boardHeight,
                    cameraFramingPadding,
                    minimumCameraDistance,
                    out string failureReason))
            {
                Debug.LogWarning(
                    $"Drop The Man gameplay camera was not repositioned: {failureReason}",
                    this);
            }
        }

        private bool TryRebuildBoardVisuals(
            RuntimeConstructionBoardData boardData,
            GridWorldLayout worldLayout,
            out string failureReason)
        {
            if (boardCellVisualPrefab == null)
            {
                failureReason = string.Empty;
                return true;
            }

            try
            {
                List<GridCoordinate> participatingCoordinates =
                    DropTheManBoardVisualParticipationMapper.CreateFromRuntimeData(boardData);
                WallGenerationResult boundary =
                    new WallGenerationSystem().Generate(participatingCoordinates);
                ModularBoardVisualPlan plan =
                    new ModularBoardVisualPlanner().CreatePlan(boundary);
                Transform root = EnsureBoardVisualSpawnRoot();

                return new ModularBoardVisualBuilder().TryRebuild(
                    plan,
                    boardCellVisualPrefab,
                    root,
                    worldLayout,
                    boardVisualNormalOffset,
                    out _,
                    out failureReason);
            }
            catch (Exception exception)
            {
                failureReason =
                    $"Drop The Man board visual construction failed: {exception.Message}";
                return false;
            }
        }

        private Transform EnsureBoardVisualSpawnRoot()
        {
            if (boardVisualSpawnRoot != null)
            {
                return boardVisualSpawnRoot;
            }

            Transform existing = transform.Find("GeneratedBoardVisuals");
            if (existing != null)
            {
                boardVisualSpawnRoot = existing;
                return boardVisualSpawnRoot;
            }

            GameObject rootObject = new("GeneratedBoardVisuals");
            rootObject.transform.SetParent(transform, false);
            boardVisualSpawnRoot = rootObject.transform;
            return boardVisualSpawnRoot;
        }

        private bool TrySpawnRuntimeViews(
            DropTheManRuntimeModel runtimeModel,
            GridWorldLayout worldLayout,
            out DropTheManHoleView[] runtimeHoleViews,
            out DropTheManStickmanView[] runtimeStickmanViews,
            out string failureReason)
        {
            if (visualConfig == null)
            {
                runtimeHoleViews = null;
                runtimeStickmanViews = null;
                failureReason =
                    "Runtime spawning requires a Drop The Man visual config.";
                return false;
            }

            DropTheManStickmanView collectablePrefab = visualConfig.collectableViewPrefab;
            if (collectablePrefab == null)
            {
                runtimeHoleViews = null;
                runtimeStickmanViews = null;
                failureReason =
                    "Runtime spawning requires a collectable view prefab in the Drop The Man visual config.";
                return false;
            }

            if (!TryCreateHolePrefabDefinitions(
                    visualConfig,
                    out IReadOnlyList<DropTheManHolePrefabDefinition> holePrefabDefinitions,
                    out failureReason))
            {
                runtimeHoleViews = null;
                runtimeStickmanViews = null;
                return false;
            }

            Transform spawnRoot = runtimeViewSpawnRoot != null
                ? runtimeViewSpawnRoot
                : transform;

            return new DropTheManRuntimeLevelViewSpawner().TrySpawnViews(
                runtimeModel,
                worldLayout,
                holePrefabDefinitions,
                collectablePrefab,
                spawnRoot,
                out runtimeHoleViews,
                out runtimeStickmanViews,
                out failureReason);
        }

        private static bool TryCreateHolePrefabDefinitions(
            DropTheManEditorConfig config,
            out IReadOnlyList<DropTheManHolePrefabDefinition> definitions,
            out string failureReason)
        {
            definitions = null;

            if (config.holePaletteEntries == null ||
                config.holePaletteEntries.Count == 0)
            {
                failureReason =
                    "Runtime spawning requires at least one hole palette entry in the " +
                    "Drop The Man visual config.";
                return false;
            }

            List<DropTheManHolePrefabDefinition> resolvedDefinitions =
                new(config.holePaletteEntries.Count);
            for (int i = 0; i < config.holePaletteEntries.Count; i++)
            {
                DropTheManEditorHolePaletteEntry entry = config.holePaletteEntries[i];
                if (entry == null)
                {
                    failureReason = $"Hole palette entry at index {i} is missing.";
                    return false;
                }

                if (entry.previewPrefab == null)
                {
                    failureReason =
                        $"Hole palette entry '{entry.id}' requires a preview prefab.";
                    return false;
                }

                if (!entry.previewPrefab.TryGetComponent(out DropTheManHoleView holeView))
                {
                    failureReason =
                        $"Hole palette prefab '{entry.previewPrefab.name}' for entry " +
                        $"'{entry.id}' requires DropTheManHoleView on its root.";
                    return false;
                }

                List<GridCoordinate> footprintOffsets = new();
                if (entry.footprintOffsets != null)
                {
                    for (int offsetIndex = 0;
                         offsetIndex < entry.footprintOffsets.Count;
                         offsetIndex++)
                    {
                        Vector2Int offset = entry.footprintOffsets[offsetIndex];
                        footprintOffsets.Add(new GridCoordinate(offset.x, offset.y));
                    }
                }

                resolvedDefinitions.Add(new DropTheManHolePrefabDefinition(
                    entry.id,
                    holeView,
                    footprintOffsets));
            }

            definitions = resolvedDefinitions;
            failureReason = string.Empty;
            return true;
        }

        private IDropTheManLevelDefinitionProvider CreateLevelDefinitionProvider()
        {
            return new DropTheManDevLevelDefinitionProvider(levelData);
        }

        private static bool TryCreateTimer(
            TimerDefinitionData timerDefinition,
            out TimerSystem timerSystem,
            out string failureReason)
        {
            timerSystem = null;

            if (timerDefinition == null || !timerDefinition.IsEnabled)
            {
                failureReason = string.Empty;
                return true;
            }

            if (timerDefinition.Mode != TimerMode.Countdown)
            {
                failureReason = "Dev scene bootstrapper supports countdown timers only.";
                return false;
            }

            float? warningThreshold = timerDefinition.WarningThresholdSeconds > 0f
                ? timerDefinition.WarningThresholdSeconds
                : null;

            try
            {
                timerSystem = new TimerSystem(
                    timerDefinition.DurationSeconds,
                    warningThreshold);
                failureReason = string.Empty;
                return true;
            }
            catch (System.Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }
        }

        private enum DropTheManDevLevelSource
        {
            InspectorDevData = 0,
            ResourcesCatalog = 1
        }

        private enum LevelResultState
        {
            None = 0,
            Completed = 1,
            Failed = 2
        }

        private void ResetLoadState()
        {
            sceneController?.DisableInput();
            sceneController?.CancelDrag();
            sceneController?.ClearPointerState();

            RuntimeModel = null;
            FrameworkContext = null;
            _resultState = LevelResultState.None;
            _resultWindowVisible = false;
        }

        private bool HasLevelCatalog()
        {
            return _levelCatalog != null && _levelCatalog.Count > 0;
        }

        private int ResolveStartingLevelIndex()
        {
            if (!HasLevelCatalog())
            {
                return -1;
            }

            return Mathf.Clamp(startingLevelIndex, 0, _levelCatalog.Count - 1);
        }

        private bool IsValidLevelCatalogIndex(int levelIndex)
        {
            return HasLevelCatalog() &&
                   levelIndex >= 0 &&
                   levelIndex < _levelCatalog.Count;
        }

        private bool HasNextLevel()
        {
            return IsValidLevelCatalogIndex(_currentLevelIndex + 1);
        }

        private bool CanUseLevelCatalog(out string failureReason)
        {
            if (levelSource != DropTheManDevLevelSource.ResourcesCatalog)
            {
                failureReason = "The Resources level catalog source is not selected.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private bool TryDiscoverLevelCatalog(out string failureReason)
        {
            LevelCatalogBuildResult catalogResult = ResourcesLevelCatalogLoader.Load(
                resourcesLevelCatalogPath,
                new DropTheManLevelCatalogMetadataReader());
            if (!catalogResult.Success)
            {
                _levelCatalog = null;
                failureReason = catalogResult.FailureReason;
                return false;
            }

            _levelCatalog = catalogResult.Catalog;
            for (int i = 0; i < catalogResult.Warnings.Count; i++)
            {
                Debug.LogWarning(catalogResult.Warnings[i], this);
            }

            failureReason = string.Empty;
            return true;
        }

        private bool CanReloadCurrentLevel(out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        private void CaptureLoadedLevelMetadata(LevelDefinition levelDefinition)
        {
            _currentLevelId = levelDefinition?.Metadata?.LevelId ?? string.Empty;
            _currentDisplayName = levelDefinition?.Metadata?.DisplayName ?? _currentLevelId;
        }

        private string ResolveResultWindowTitle()
        {
            return _resultState switch
            {
                LevelResultState.Completed => "Level Complete",
                LevelResultState.Failed => "Level Failed",
                _ => "Level Result"
            };
        }

        private void DrawResultWindow(int windowId)
        {
            GUILayout.Label(string.IsNullOrWhiteSpace(_currentDisplayName)
                ? "Unnamed level"
                : _currentDisplayName);

            if (!string.IsNullOrWhiteSpace(_currentLevelId))
            {
                GUILayout.Label($"Id: {_currentLevelId}");
            }

            GUILayout.Space(8f);

            if (_resultState == LevelResultState.Completed)
            {
                GUI.enabled = HasNextLevel();
                if (GUILayout.Button(HasNextLevel() ? "Next Level" : "No Next Level"))
                {
                    if (!TryLoadNextLevel(out string failureReason))
                    {
                        Debug.LogError(failureReason, this);
                    }
                }

                GUI.enabled = true;
                if (GUILayout.Button("Restart"))
                {
                    if (!TryRestartCurrentLevel(out string failureReason))
                    {
                        Debug.LogError(failureReason, this);
                    }
                }
            }
            else if (_resultState == LevelResultState.Failed)
            {
                if (GUILayout.Button("Restart"))
                {
                    if (!TryRestartCurrentLevel(out string failureReason))
                    {
                        Debug.LogError(failureReason, this);
                    }
                }
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }
    }

    internal sealed class DropTheManRuntimeLevelViewSpawner
    {
        public bool TrySpawnViews(
            DropTheManRuntimeModel runtimeModel,
            GridWorldLayout worldLayout,
            IReadOnlyList<DropTheManHolePrefabDefinition> holePrefabDefinitions,
            DropTheManStickmanView collectablePrefab,
            Transform spawnRoot,
            out DropTheManHoleView[] spawnedHoleViews,
            out DropTheManStickmanView[] spawnedStickmanViews,
            out string failureReason)
        {
            spawnedHoleViews = null;
            spawnedStickmanViews = null;

            if (runtimeModel == null)
            {
                failureReason = "Runtime model is required for view spawning.";
                return false;
            }

            if (!DropTheManHolePrefabResolver.TryCreate(
                    holePrefabDefinitions,
                    out DropTheManHolePrefabResolver holePrefabResolver,
                    out failureReason))
            {
                return false;
            }

            if (collectablePrefab == null)
            {
                failureReason = "A collectable view prefab is required for runtime spawning.";
                return false;
            }

            DropTheManHolePrefabResolution[] holePrefabResolutions =
                new DropTheManHolePrefabResolution[runtimeModel.Holes.Count];
            for (int i = 0; i < runtimeModel.Holes.Count; i++)
            {
                HoleRuntimeState hole = runtimeModel.Holes[i];
                if (!holePrefabResolver.TryResolve(
                        hole.Footprint.Offsets,
                        out holePrefabResolutions[i],
                        out string resolutionFailureReason))
                {
                    failureReason =
                        $"Runtime hole '{hole.Id}' could not resolve a view prefab: " +
                        resolutionFailureReason;
                    return false;
                }
            }

            Transform resolvedRoot = EnsureSpawnRoot(spawnRoot);
            Transform holesRoot = EnsureChildRoot(resolvedRoot, "SpawnedHoleViews");
            Transform stickmenRoot = EnsureChildRoot(resolvedRoot, "SpawnedStickmanViews");
            DestroyChildren(holesRoot);
            DestroyChildren(stickmenRoot);

            spawnedHoleViews = new DropTheManHoleView[runtimeModel.Holes.Count];
            for (int i = 0; i < runtimeModel.Holes.Count; i++)
            {
                HoleRuntimeState hole = runtimeModel.Holes[i];
                DropTheManHolePrefabResolution prefabResolution =
                    holePrefabResolutions[i];
                Vector3 worldPosition = ToWorldPosition(
                    worldLayout,
                    hole.CurrentCoordinate,
                    prefabResolution.Prefab.transform.position.y);

                DropTheManHoleView holeView = UnityEngine.Object.Instantiate(
                    prefabResolution.Prefab,
                    holesRoot);
                holeView.name = $"Hole_{hole.Id}";
                holeView.transform.rotation = BuildHoleRotation(
                    worldLayout,
                    prefabResolution.QuarterTurns);
                holeView.ConfigureSpawnedView(hole.Id, worldPosition, hole.ColorIdentity);
                spawnedHoleViews[i] = holeView;
            }

            spawnedStickmanViews = new DropTheManStickmanView[runtimeModel.Stickmen.Count];
            for (int i = 0; i < runtimeModel.Stickmen.Count; i++)
            {
                StickmanRuntimeState stickman = runtimeModel.Stickmen[i];
                Vector3 worldPosition = ToWorldPosition(
                    worldLayout,
                    stickman.Coordinate,
                    collectablePrefab.transform.position.y);

                DropTheManStickmanView stickmanView =
                    UnityEngine.Object.Instantiate(collectablePrefab, stickmenRoot);
                stickmanView.name = $"Stickman_{stickman.Id}";
                stickmanView.ConfigureSpawnedView(
                    stickman.Id,
                    worldPosition,
                    stickman.ColorIdentity);
                spawnedStickmanViews[i] = stickmanView;
            }

            failureReason = string.Empty;
            return true;
        }

        private static Vector3 ToWorldPosition(
            GridWorldLayout worldLayout,
            GridCoordinate coordinate,
            float visualHeight)
        {
            Vector3 worldPosition =
                worldLayout.BoardLocalToWorld(new Vector2(coordinate.X, coordinate.Y));
            worldPosition.y = visualHeight;
            return worldPosition;
        }

        private static Quaternion BuildHoleRotation(
            GridWorldLayout worldLayout,
            int quarterTurns)
        {
            Vector3 boardNormal =
                Vector3.Cross(worldLayout.BoardYAxis, worldLayout.BoardXAxis).normalized;
            Quaternion boardRotation =
                Quaternion.LookRotation(worldLayout.BoardYAxis, boardNormal);
            return boardRotation * Quaternion.Euler(0f, quarterTurns * 90f, 0f);
        }

        private static Transform EnsureSpawnRoot(Transform spawnRoot)
        {
            if (spawnRoot != null)
            {
                return spawnRoot;
            }

            GameObject rootObject = GameObject.Find("DropTheManSpawnedViews");
            if (rootObject == null)
            {
                rootObject = new GameObject("DropTheManSpawnedViews");
            }

            return rootObject.transform;
        }

        private static Transform EnsureChildRoot(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing;
            }

            GameObject rootObject = new(name);
            rootObject.transform.SetParent(parent, false);
            return rootObject.transform;
        }

        private static void DestroyChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }
    }
}
