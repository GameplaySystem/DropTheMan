using PuzzleFramework.Content;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.RuntimeConstruction;
using PuzzleFramework.RuntimeFlow;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Dev-only gameplay-scene bridge that builds a Drop The Man runtime model, initializes
    /// the pre-wired scene controller, and can drive a prototype-only level sequence plus
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
            DropTheManDevLevelSource.InspectorDevData;
        [SerializeField] private TextAsset jsonLevelAsset;
        [SerializeField] private TextAsset[] levelSequence = Array.Empty<TextAsset>();
        [SerializeField, Min(0)] private int startingLevelIndex;
        [SerializeField] private Vector3 boardWorldOrigin = Vector3.zero;
        [SerializeField] private Vector2 cellSize = Vector2.one;
        [SerializeField] private Vector3 boardGridXAxis = Vector3.right;
        [SerializeField] private Vector3 boardGridYAxis = Vector3.forward;
        [SerializeField] private bool spawnRuntimeViews = true;
        [SerializeField] private Transform runtimeViewSpawnRoot;
        [SerializeField] private DropTheManDevLevelData levelData =
            DropTheManDevLevelData.CreateDefault();

        public DropTheManRuntimeModel RuntimeModel { get; private set; }
        public RuntimeLevelContext FrameworkContext { get; private set; }

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
            if (HasLevelSequence())
            {
                if (!CanUseLevelSequence(out failureReason))
                {
                    return false;
                }

                return TryLoadLevelAtIndex(ResolveStartingLevelIndex(), out failureReason);
            }

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

            if (HasLevelSequence() && IsValidLevelSequenceIndex(_currentLevelIndex))
            {
                return TryLoadLevelAtIndex(_currentLevelIndex, out failureReason);
            }

            return TryBootstrapConfiguredSource(out failureReason);
        }

        /// <summary>
        /// Loads the next JSON level from the configured scene-local sequence.
        /// This remains prototype-owned test-scene flow rather than framework progression.
        /// </summary>
        public bool TryLoadNextLevel(out string failureReason)
        {
            if (!CanUseLevelSequence(out failureReason))
            {
                return false;
            }

            int nextLevelIndex = _currentLevelIndex + 1;
            if (!IsValidLevelSequenceIndex(nextLevelIndex))
            {
                failureReason = "No next level exists in the configured sequence.";
                return false;
            }

            return TryLoadLevelAtIndex(nextLevelIndex, out failureReason);
        }

        private bool TryLoadLevelAtIndex(int levelIndex, out string failureReason)
        {
            if (!HasLevelSequence())
            {
                failureReason = "No level sequence is configured.";
                return false;
            }

            if (!IsValidLevelSequenceIndex(levelIndex))
            {
                failureReason =
                    $"Level sequence index {levelIndex} is outside the configured range.";
                return false;
            }

            TextAsset levelAsset = levelSequence[levelIndex];
            if (levelAsset == null)
            {
                failureReason = $"Level sequence entry {levelIndex} is null.";
                return false;
            }

            bool loaded = TryBootstrapFromProvider(
                new DropTheManJsonLevelDefinitionProvider(levelAsset),
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
                worldLayout = new GridWorldLayout(
                    boardWorldOrigin,
                    cellSize,
                    boardGridXAxis,
                    boardGridYAxis);
            }
            catch (System.Exception exception)
            {
                failureReason = exception.Message;
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
            if (spawnRuntimeViews)
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
            }
            else
            {
                sceneController.SetRuntimeViews(
                    new List<DropTheManHoleView>(sceneController.HoleViewTemplates).ToArray(),
                    new List<DropTheManStickmanView>(sceneController.StickmanViewTemplates).ToArray());
            }

            DropTheManRuntimeControllerResult initializeResult =
                sceneController.Initialize(
                    runtimeModel,
                    worldLayout,
                    timerSystem);
            failureReason = initializeResult.FailureReason;
            return initializeResult;
        }

        private bool TrySpawnRuntimeViews(
            DropTheManRuntimeModel runtimeModel,
            GridWorldLayout worldLayout,
            out DropTheManHoleView[] runtimeHoleViews,
            out DropTheManStickmanView[] runtimeStickmanViews,
            out string failureReason)
        {
            DropTheManHoleView holeTemplate = ResolveFirstTemplate(sceneController.HoleViewTemplates);
            if (holeTemplate == null)
            {
                runtimeHoleViews = null;
                runtimeStickmanViews = null;
                failureReason =
                    "Runtime spawning requires at least one hole view template on the scene controller.";
                return false;
            }

            DropTheManStickmanView stickmanTemplate =
                ResolveFirstTemplate(sceneController.StickmanViewTemplates);
            if (stickmanTemplate == null)
            {
                runtimeHoleViews = null;
                runtimeStickmanViews = null;
                failureReason =
                    "Runtime spawning requires at least one stickman view template on the scene controller.";
                return false;
            }

            Transform spawnRoot = runtimeViewSpawnRoot != null
                ? runtimeViewSpawnRoot
                : transform;

            return new DropTheManRuntimeLevelViewSpawner().TrySpawnViews(
                runtimeModel,
                worldLayout,
                holeTemplate,
                stickmanTemplate,
                spawnRoot,
                out runtimeHoleViews,
                out runtimeStickmanViews,
                out failureReason);
        }

        private static T ResolveFirstTemplate<T>(IReadOnlyList<T> templates)
            where T : UnityEngine.Object
        {
            if (templates == null)
            {
                return null;
            }

            for (int i = 0; i < templates.Count; i++)
            {
                if (templates[i] != null)
                {
                    return templates[i];
                }
            }

            return null;
        }

        private IDropTheManLevelDefinitionProvider CreateLevelDefinitionProvider()
        {
            return levelSource == DropTheManDevLevelSource.JsonTextAsset
                ? new DropTheManJsonLevelDefinitionProvider(jsonLevelAsset)
                : new DropTheManDevLevelDefinitionProvider(levelData);
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
            JsonTextAsset = 1
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

        private bool HasLevelSequence()
        {
            return levelSequence != null && levelSequence.Length > 0;
        }

        private int ResolveStartingLevelIndex()
        {
            if (!HasLevelSequence())
            {
                return -1;
            }

            return Mathf.Clamp(startingLevelIndex, 0, levelSequence.Length - 1);
        }

        private bool IsValidLevelSequenceIndex(int levelIndex)
        {
            return HasLevelSequence() &&
                   levelIndex >= 0 &&
                   levelIndex < levelSequence.Length;
        }

        private bool HasNextLevel()
        {
            return IsValidLevelSequenceIndex(_currentLevelIndex + 1);
        }

        private bool CanUseLevelSequence(out string failureReason)
        {
            if (!HasLevelSequence())
            {
                failureReason = "No level sequence is configured.";
                return false;
            }

            if (!spawnRuntimeViews)
            {
                failureReason =
                    "Level sequencing requires runtime view spawning so levels can reload cleanly.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private bool CanReloadCurrentLevel(out string failureReason)
        {
            if (!spawnRuntimeViews)
            {
                failureReason =
                    "Restart requires runtime view spawning because pre-placed views do not yet reset cleanly.";
                return false;
            }

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
            DropTheManHoleView holeTemplate,
            DropTheManStickmanView stickmanTemplate,
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

            if (holeTemplate == null)
            {
                failureReason = "A hole view template is required for runtime spawning.";
                return false;
            }

            if (stickmanTemplate == null)
            {
                failureReason = "A stickman view template is required for runtime spawning.";
                return false;
            }

            Transform resolvedRoot = EnsureSpawnRoot(spawnRoot);
            Transform holesRoot = EnsureChildRoot(resolvedRoot, "SpawnedHoleViews");
            Transform stickmenRoot = EnsureChildRoot(resolvedRoot, "SpawnedStickmanViews");
            DestroyChildren(holesRoot);
            DestroyChildren(stickmenRoot);

            holeTemplate.SetTemplateHidden(true);
            stickmanTemplate.SetTemplateHidden(true);

            spawnedHoleViews = new DropTheManHoleView[runtimeModel.Holes.Count];
            for (int i = 0; i < runtimeModel.Holes.Count; i++)
            {
                HoleRuntimeState hole = runtimeModel.Holes[i];
                Vector3 worldPosition = ToWorldPosition(
                    worldLayout,
                    hole.CurrentCoordinate,
                    holeTemplate.transform.position.y);

                DropTheManHoleView holeView = UnityEngine.Object.Instantiate(
                    holeTemplate,
                    holesRoot);
                holeView.name = $"Hole_{hole.Id}";
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
                    stickmanTemplate.transform.position.y);

                DropTheManStickmanView stickmanView =
                    UnityEngine.Object.Instantiate(stickmanTemplate, stickmenRoot);
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
