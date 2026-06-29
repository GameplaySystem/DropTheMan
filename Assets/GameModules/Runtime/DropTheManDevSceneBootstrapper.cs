using PuzzleFramework.Content;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.RuntimeConstruction;
using PuzzleFramework.RuntimeFlow;
using UnityEngine;

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
        [SerializeField] private Vector3 boardWorldOrigin = Vector3.zero;
        [SerializeField] private Vector2 cellSize = Vector2.one;
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

            DropTheManDevLevelDefinitionProvider provider = new(levelData);
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
                worldLayout = new GridWorldLayout(boardWorldOrigin, cellSize);
            }
            catch (System.Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }

            DropTheManRuntimeControllerResult sceneInitializeResult =
                sceneController.Initialize(
                    modelBuildResult.RuntimeModel,
                    worldLayout,
                    timerSystem);
            if (!sceneInitializeResult.Success)
            {
                failureReason = sceneInitializeResult.FailureReason;
                return false;
            }

            FrameworkContext = frameworkBuildResult.Context;
            RuntimeModel = modelBuildResult.RuntimeModel;
            failureReason = string.Empty;
            return true;
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
    }
}
