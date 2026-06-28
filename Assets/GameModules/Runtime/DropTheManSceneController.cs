using PuzzleFramework.CoreBoard;
using PuzzleFramework.RuntimeFlow;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Thin Unity scene-facing composition adapter for pre-placed Drop The Man views.
    /// A future dev bootstrapper supplies the already-built runtime model; this component does
    /// not load levels, spawn prefabs, or own gameplay rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManSceneController : MonoBehaviour
    {
        [SerializeField] private DropTheManHoleView[] holeViews;
        [SerializeField] private DropTheManStickmanView[] stickmanViews;
        [SerializeField] private DropTheManPointerInputAdapter pointerInputAdapter;
        [SerializeField] private bool startGameplayOnInitialize = true;
        [SerializeField] private bool tickTimerInUpdate = true;

        private DropTheManRuntimeController _runtimeController;
        private bool _isInitialized;
        private bool _inputEnabled;

        public DropTheManRuntimeController RuntimeController => _runtimeController;
        public bool IsInitialized => _isInitialized;
        public bool InputEnabled =>
            _isInitialized &&
            _inputEnabled &&
            _runtimeController != null &&
            !_runtimeController.TerminalOutcomeAccepted;

        private void Awake()
        {
            if (pointerInputAdapter == null)
            {
                TryGetComponent(out pointerInputAdapter);
            }

            if (pointerInputAdapter != null)
            {
                pointerInputAdapter.Bind(this);
            }
        }

        private void Update()
        {
            if (!_isInitialized ||
                !tickTimerInUpdate ||
                _runtimeController == null ||
                _runtimeController.TerminalOutcomeAccepted)
            {
                return;
            }

            DropTheManRuntimeControllerResult result =
                _runtimeController.TickTimer(Time.deltaTime);
            HandleRuntimeResult(result);
        }

        public DropTheManRuntimeControllerResult Initialize(
            DropTheManRuntimeModel runtimeModel,
            GridWorldLayout worldLayout,
            TimerSystem timerSystem = null)
        {
            DisableInput();
            _isInitialized = false;
            _runtimeController = null;

            DropTheManRuntimeBootstrapResult bootstrapResult =
                new DropTheManRuntimeBootstrapper().CreateController(
                    runtimeModel,
                    worldLayout,
                    holeViews,
                    stickmanViews,
                    timerSystem);

            if (!bootstrapResult.Success)
            {
                Debug.LogError(bootstrapResult.FailureReason, this);
                return DropTheManRuntimeControllerResult.Failed(
                    bootstrapResult.FailureReason);
            }

            _runtimeController = bootstrapResult.Controller;
            _isInitialized = true;

            if (!startGameplayOnInitialize)
            {
                return DropTheManRuntimeControllerResult.NoAction(
                    "Runtime controller initialized; gameplay start is deferred.");
            }

            DropTheManRuntimeControllerResult startResult =
                _runtimeController.StartGameplay();
            HandleRuntimeResult(startResult);

            if (startResult.Success)
            {
                EnableInput();
            }

            return startResult;
        }

        public bool TryBeginDrag(DropTheManHoleView holeView)
        {
            if (!InputEnabled || holeView == null || !holeView.IsSelectable)
            {
                return false;
            }

            DropTheManRuntimeControllerResult result =
                _runtimeController.BeginDragByHoleView(holeView);
            HandleRuntimeResult(result);
            return result.Success && result.DragBegan;
        }

        public bool TryUpdateDrag(Vector3 worldPosition)
        {
            if (!InputEnabled)
            {
                return false;
            }

            DropTheManRuntimeControllerResult result =
                _runtimeController.UpdateDragWorldPosition(worldPosition);
            HandleRuntimeResult(result);
            return result.Success && result.DragUpdated;
        }

        public void ReleaseDrag()
        {
            if (_runtimeController == null)
            {
                return;
            }

            if (!InputEnabled)
            {
                ClearInputOnly();
                return;
            }

            DropTheManRuntimeControllerResult result =
                _runtimeController.ReleaseDrag();
            HandleRuntimeResult(result);
        }

        public void CancelDrag()
        {
            _runtimeController?.CancelDrag();
        }

        public void EnableInput()
        {
            _inputEnabled = true;
        }

        public void DisableInput()
        {
            _inputEnabled = false;
        }

        private void HandleRuntimeResult(
            DropTheManRuntimeControllerResult result)
        {
            if (!result.Success && !string.IsNullOrEmpty(result.FailureReason))
            {
                Debug.LogWarning(result.FailureReason, this);
            }

            if (result.TerminalOutcomeAccepted ||
                (_runtimeController != null && _runtimeController.TerminalOutcomeAccepted))
            {
                DisableInput();
                ClearInputOnly();
            }
        }

        private void ClearInputOnly()
        {
            pointerInputAdapter?.ClearLocalPointerState();
        }
    }
}
