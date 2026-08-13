using PuzzleFramework.Content;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.RuntimeConstruction;
using PuzzleFramework.RuntimeFlow;
using UnityEngine;
using System.Collections.Generic;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Dev-only bridge that builds a minimal Drop The Man runtime model and initializes
    /// a pre-wired scene controller. This is not production level loading.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManDevSceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private DropTheManSceneController sceneController;
        [SerializeField] private bool bootstrapOnStart = true;
        [SerializeField] private DropTheManDevLevelSource levelSource =
            DropTheManDevLevelSource.InspectorDevData;
        [SerializeField] private TextAsset jsonLevelAsset;
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
            RuntimeModel = null;
            FrameworkContext = null;

            if (sceneController == null)
            {
                failureReason = "Drop The Man scene controller reference is required.";
                return false;
            }

            if (cellSize.x <= 0f || cellSize.y <= 0f)
            {
                failureReason = "Dev scene cell size must be positive.";
                return false;
            }

            IDropTheManLevelDefinitionProvider provider = CreateLevelDefinitionProvider();
            if (!provider.TryGetLevelDefinition(
                    out LevelDefinition levelDefinition,
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
            DestroyChildren(resolvedRoot);

            Transform holesRoot = EnsureChildRoot(resolvedRoot, "SpawnedHoleViews");
            Transform stickmenRoot = EnsureChildRoot(resolvedRoot, "SpawnedStickmanViews");

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

                DropTheManHoleView holeView = Object.Instantiate(holeTemplate, holesRoot);
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
                    Object.Instantiate(stickmanTemplate, stickmenRoot);
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
                Object.Destroy(root.GetChild(i).gameObject);
            }
        }
    }
}
