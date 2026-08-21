using System;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.Interaction;
using PuzzleFramework.RuntimeFlow;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Result for the prototype-owned runtime integration controller.
    /// It reports orchestration effects without hiding the underlying service results.
    /// </summary>
    public readonly struct DropTheManRuntimeControllerResult
    {
        private DropTheManRuntimeControllerResult(
            bool success,
            bool dragBegan,
            bool dragUpdated,
            bool releaseOccurred,
            bool terminalOutcomeAccepted,
            bool hasAuthoritativeWorldPosition,
            Vector3 authoritativeWorldPosition,
            DropTheManOutcomeRoutingResult outcomeRoutingResult,
            TimerAdvanceResult timerAdvanceResult,
            string failureReason)
        {
            Success = success;
            DragBegan = dragBegan;
            DragUpdated = dragUpdated;
            ReleaseOccurred = releaseOccurred;
            TerminalOutcomeAccepted = terminalOutcomeAccepted;
            HasAuthoritativeWorldPosition = hasAuthoritativeWorldPosition;
            AuthoritativeWorldPosition = authoritativeWorldPosition;
            OutcomeRoutingResult = outcomeRoutingResult;
            TimerAdvanceResult = timerAdvanceResult;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public bool DragBegan { get; }
        public bool DragUpdated { get; }
        public bool ReleaseOccurred { get; }
        public bool TerminalOutcomeAccepted { get; }
        public bool HasAuthoritativeWorldPosition { get; }
        public Vector3 AuthoritativeWorldPosition { get; }
        public DropTheManOutcomeRoutingResult OutcomeRoutingResult { get; }
        public TimerAdvanceResult TimerAdvanceResult { get; }
        public string FailureReason { get; }

        public static DropTheManRuntimeControllerResult StartedGameplay()
        {
            return new DropTheManRuntimeControllerResult(
                true,
                false,
                false,
                false,
                false,
                false,
                default,
                default,
                default,
                string.Empty);
        }

        public static DropTheManRuntimeControllerResult DragStarted(
            Vector3 authoritativeWorldPosition)
        {
            return new DropTheManRuntimeControllerResult(
                true,
                true,
                false,
                false,
                false,
                true,
                authoritativeWorldPosition,
                default,
                default,
                string.Empty);
        }

        public static DropTheManRuntimeControllerResult UpdatedDrag(
            bool success,
            Vector3 authoritativeWorldPosition,
            bool terminalOutcomeAccepted,
            DropTheManOutcomeRoutingResult outcomeRoutingResult,
            string failureReason)
        {
            return new DropTheManRuntimeControllerResult(
                success,
                false,
                true,
                false,
                terminalOutcomeAccepted,
                true,
                authoritativeWorldPosition,
                outcomeRoutingResult,
                default,
                failureReason);
        }

        public static DropTheManRuntimeControllerResult Released(
            bool success,
            Vector3 authoritativeWorldPosition,
            string failureReason)
        {
            return new DropTheManRuntimeControllerResult(
                success,
                false,
                false,
                true,
                false,
                true,
                authoritativeWorldPosition,
                default,
                default,
                failureReason);
        }

        public static DropTheManRuntimeControllerResult TimerAdvanced(
            TimerAdvanceResult timerAdvanceResult,
            bool terminalOutcomeAccepted,
            DropTheManOutcomeRoutingResult outcomeRoutingResult,
            string failureReason)
        {
            return new DropTheManRuntimeControllerResult(
                timerAdvanceResult.IsSuccess && string.IsNullOrEmpty(failureReason),
                false,
                false,
                false,
                terminalOutcomeAccepted,
                false,
                default,
                outcomeRoutingResult,
                timerAdvanceResult,
                failureReason);
        }

        public static DropTheManRuntimeControllerResult TerminalOutcome(
            bool terminalOutcomeAccepted,
            DropTheManOutcomeRoutingResult outcomeRoutingResult,
            string failureReason)
        {
            return new DropTheManRuntimeControllerResult(
                outcomeRoutingResult.Success,
                false,
                false,
                false,
                terminalOutcomeAccepted,
                false,
                default,
                outcomeRoutingResult,
                default,
                failureReason);
        }

        public static DropTheManRuntimeControllerResult Cancelled()
        {
            return new DropTheManRuntimeControllerResult(
                true,
                false,
                false,
                false,
                false,
                false,
                default,
                default,
                default,
                string.Empty);
        }

        public static DropTheManRuntimeControllerResult NoAction(string reason)
        {
            return new DropTheManRuntimeControllerResult(
                true,
                false,
                false,
                false,
                false,
                false,
                default,
                default,
                default,
                reason);
        }

        public static DropTheManRuntimeControllerResult Failed(string failureReason)
        {
            return new DropTheManRuntimeControllerResult(
                false,
                false,
                false,
                false,
                false,
                false,
                default,
                default,
                default,
                failureReason);
        }
    }

    /// <summary>
    /// Prototype-owned runtime orchestration layer for a future scene adapter.
    /// It receives already-converted world positions, delegates gameplay to existing services,
    /// routes views through the registry, and sends terminal facts to the outcome router.
    /// </summary>
    public sealed class DropTheManRuntimeController
    {
        private readonly DropTheManRuntimeModel _runtimeModel;
        private readonly GridWorldLayout _worldLayout;
        private readonly DropTheManViewRegistry _viewRegistry;
        private readonly DropTheManDragSessionOwner _dragSessionOwner;
        private readonly DropTheManOutcomeRouter _outcomeRouter;
        private readonly IGridSnapSystem _fullHoleVisualSnapSystem;
        private readonly GameStateSystem _gameStateSystem;
        private readonly TimerSystem _timerSystem;
        private readonly float _collectionTriggerRadiusInCells;
        private readonly float _dragClearanceInsetCells;
        private readonly bool _snapFullHolesToNearestCellBeforeClosing;

        private string _activeHoleId = string.Empty;
        private bool _terminalOutcomeAccepted;

        public DropTheManRuntimeController(
            DropTheManRuntimeModel runtimeModel,
            GridWorldLayout worldLayout,
            DropTheManViewRegistry viewRegistry,
            GameStateSystem gameStateSystem,
            DropTheManDragSessionOwner dragSessionOwner = null,
            DropTheManOutcomeRouter outcomeRouter = null,
            TimerSystem timerSystem = null,
            float collectionTriggerRadiusInCells = 0.35f,
            float dragClearanceInsetCells = 0.08f,
            bool snapFullHolesToNearestCellBeforeClosing = true,
            IGridSnapSystem fullHoleVisualSnapSystem = null)
        {
            if (collectionTriggerRadiusInCells <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(collectionTriggerRadiusInCells),
                    "Collection trigger radius must be positive.");
            }

            if (dragClearanceInsetCells < 0f || dragClearanceInsetCells > 0.45f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dragClearanceInsetCells),
                    "Drag clearance inset must stay between 0 and 0.45 cells.");
            }

            _runtimeModel = runtimeModel ?? throw new ArgumentNullException(nameof(runtimeModel));
            _worldLayout = worldLayout;
            _viewRegistry = viewRegistry ?? throw new ArgumentNullException(nameof(viewRegistry));
            _gameStateSystem = gameStateSystem ?? throw new ArgumentNullException(nameof(gameStateSystem));
            _dragSessionOwner = dragSessionOwner ?? new DropTheManDragSessionOwner();
            _outcomeRouter = outcomeRouter ?? new DropTheManOutcomeRouter();
            _fullHoleVisualSnapSystem = fullHoleVisualSnapSystem ?? new GridSnapSystem();
            _timerSystem = timerSystem;
            _collectionTriggerRadiusInCells = collectionTriggerRadiusInCells;
            _dragClearanceInsetCells = dragClearanceInsetCells;
            _snapFullHolesToNearestCellBeforeClosing =
                snapFullHolesToNearestCellBeforeClosing;
        }

        public bool HasActiveDrag => _dragSessionOwner.HasSessionContext;
        public bool TerminalOutcomeAccepted => _terminalOutcomeAccepted || IsTerminal(_gameStateSystem.CurrentState);
        public GameState CurrentGameState => _gameStateSystem.CurrentState;

        /// <summary>
        /// Starts gameplay through the framework game-state system and starts the optional
        /// countdown timer immediately when it is present and stopped.
        /// </summary>
        public DropTheManRuntimeControllerResult StartGameplay()
        {
            if (TerminalOutcomeAccepted)
            {
                return DropTheManRuntimeControllerResult.Failed(
                    $"Cannot start gameplay from terminal state {_gameStateSystem.CurrentState}.");
            }

            GameStateTransitionResult gameStateResult =
                _gameStateSystem.TryTransitionTo(GameState.Playing);
            if (!gameStateResult.IsSuccess)
            {
                return DropTheManRuntimeControllerResult.Failed(gameStateResult.FailureReason);
            }

            if (_timerSystem != null && _timerSystem.Status == TimerStatus.Stopped)
            {
                TimerOperationResult timerResult = _timerSystem.Start();
                if (!timerResult.IsSuccess)
                {
                    return DropTheManRuntimeControllerResult.Failed(timerResult.FailureReason);
                }
            }

            return DropTheManRuntimeControllerResult.StartedGameplay();
        }

        /// <summary>
        /// Begins dragging by runtime hole id. The caller owns screen-to-world and hit-test logic.
        /// </summary>
        public DropTheManRuntimeControllerResult BeginDragByHoleId(string holeId)
        {
            if (!CanAcceptInput(out string inputFailureReason))
            {
                return DropTheManRuntimeControllerResult.NoAction(inputFailureReason);
            }

            if (!_viewRegistry.TryGetHoleView(holeId, out IDropTheManHoleView view))
            {
                return DropTheManRuntimeControllerResult.Failed(
                    $"No hole view is registered for runtime id '{holeId}'.");
            }

            if (!_viewRegistry.TryResolveHoleState(
                    _runtimeModel,
                    holeId,
                    out HoleRuntimeState hole,
                    out string failureReason))
            {
                return DropTheManRuntimeControllerResult.Failed(failureReason);
            }

            DropTheManDragSessionBeginResult beginResult =
                _dragSessionOwner.TryBeginDrag(
                    _runtimeModel,
                    hole,
                    view.WorldPosition,
                    _worldLayout);

            if (!beginResult.Success)
            {
                return DropTheManRuntimeControllerResult.Failed(beginResult.FailureReason);
            }

            _activeHoleId = holeId;
            view.ApplyWorldPosition(beginResult.AuthoritativeWorldPosition);
            return DropTheManRuntimeControllerResult.DragStarted(
                beginResult.AuthoritativeWorldPosition);
        }

        /// <summary>
        /// Begins dragging from a selected hole view without requiring the caller to read ids itself.
        /// </summary>
        public DropTheManRuntimeControllerResult BeginDragByHoleView(
            IDropTheManHoleView holeView)
        {
            if (holeView == null)
            {
                return DropTheManRuntimeControllerResult.Failed("Hole view is required.");
            }

            return BeginDragByHoleId(holeView.RuntimeId);
        }

        /// <summary>
        /// Applies exactly one world-position drag update to the active drag session.
        /// This method is the intended call point for a future input adapter after pointer-to-world conversion.
        /// </summary>
        public DropTheManRuntimeControllerResult UpdateDragWorldPosition(
            Vector3 candidateWorldPosition)
        {
            if (!CanAcceptInput(out string inputFailureReason))
            {
                CancelActiveDragWithoutReleaseCommit();
                return DropTheManRuntimeControllerResult.NoAction(inputFailureReason);
            }

            if (!_dragSessionOwner.HasSessionContext || string.IsNullOrWhiteSpace(_activeHoleId))
            {
                return DropTheManRuntimeControllerResult.NoAction("No active drag session exists.");
            }

            DropTheManDragSessionUpdateResult updateResult =
                _dragSessionOwner.UpdateDrag(
                    candidateWorldPosition,
                    _collectionTriggerRadiusInCells,
                    _dragClearanceInsetCells);

            DropTheManViewRegistryResult applyResult =
                _viewRegistry.ApplyHoleWorldPosition(
                    _activeHoleId,
                    updateResult.AuthoritativeWorldPosition);
            if (!applyResult.Success)
            {
                CancelActiveDragWithoutReleaseCommit();
                return DropTheManRuntimeControllerResult.UpdatedDrag(
                    false,
                    updateResult.AuthoritativeWorldPosition,
                    false,
                    default,
                    applyResult.FailureReason);
            }

            if (!TryCompleteTriggeredCollections(
                    updateResult,
                    out DropTheManFullHoleCompletionResult fullHoleCompletionResult,
                    out string collectionFailureReason))
            {
                CancelActiveDragWithoutReleaseCommit();
                return DropTheManRuntimeControllerResult.UpdatedDrag(
                    false,
                    updateResult.AuthoritativeWorldPosition,
                    false,
                    default,
                    collectionFailureReason);
            }

            DropTheManOutcomeRoutingResult outcomeResult = default;
            bool terminalAccepted = false;
            bool outcomeWasHandled = false;

            if (fullHoleCompletionResult.Success &&
                fullHoleCompletionResult.PresentationPending)
            {
                if (!TryBeginFullHoleCompletionPresentation(
                        fullHoleCompletionResult,
                        out outcomeResult,
                        out outcomeWasHandled,
                        out terminalAccepted,
                        out string presentationFailureReason))
                {
                    CancelActiveDragWithoutReleaseCommit();
                    return DropTheManRuntimeControllerResult.UpdatedDrag(
                        false,
                        updateResult.AuthoritativeWorldPosition,
                        terminalAccepted,
                        outcomeResult,
                        presentationFailureReason);
                }
            }

            if (!updateResult.Success)
            {
                CancelActiveDragWithoutReleaseCommit();
                return DropTheManRuntimeControllerResult.UpdatedDrag(
                    false,
                    updateResult.AuthoritativeWorldPosition,
                    terminalAccepted,
                    outcomeResult,
                    updateResult.FailureReason);
            }

            if (outcomeWasHandled && !outcomeResult.Success)
            {
                return DropTheManRuntimeControllerResult.UpdatedDrag(
                    false,
                    updateResult.AuthoritativeWorldPosition,
                    terminalAccepted,
                    outcomeResult,
                    outcomeResult.Reason);
            }

            return DropTheManRuntimeControllerResult.UpdatedDrag(
                true,
                updateResult.AuthoritativeWorldPosition,
                terminalAccepted,
                outcomeResult,
                outcomeWasHandled ? outcomeResult.Reason : string.Empty);
        }

        /// <summary>
        /// Releases the active drag session. Normal non-full release commit remains owned by
        /// DropTheManDragSessionOwner and is not reimplemented here.
        /// </summary>
        public DropTheManRuntimeControllerResult ReleaseDrag()
        {
            if (TerminalOutcomeAccepted)
            {
                CancelActiveDragWithoutReleaseCommit();
                return DropTheManRuntimeControllerResult.NoAction(
                    "Terminal outcome is already accepted; release commit is skipped.");
            }

            if (!_dragSessionOwner.HasSessionContext || string.IsNullOrWhiteSpace(_activeHoleId))
            {
                return DropTheManRuntimeControllerResult.NoAction("No active drag session exists.");
            }

            string releasedHoleId = _activeHoleId;
            DropTheManDragSessionReleaseResult releaseResult = _dragSessionOwner.Release();
            _activeHoleId = string.Empty;

            DropTheManViewRegistryResult applyResult =
                _viewRegistry.ApplyHoleWorldPosition(
                    releasedHoleId,
                    releaseResult.AuthoritativeWorldPosition);
            if (!applyResult.Success)
            {
                return DropTheManRuntimeControllerResult.Released(
                    false,
                    releaseResult.AuthoritativeWorldPosition,
                    applyResult.FailureReason);
            }

            return DropTheManRuntimeControllerResult.Released(
                releaseResult.Success,
                releaseResult.AuthoritativeWorldPosition,
                releaseResult.FailureReason);
        }

        /// <summary>
        /// Clears the active drag session without running normal non-full release commit.
        /// This is used for terminal-state cancellation and future input-cancel adapters.
        /// </summary>
        public DropTheManRuntimeControllerResult CancelDrag()
        {
            CancelActiveDragWithoutReleaseCommit();
            return DropTheManRuntimeControllerResult.Cancelled();
        }

        /// <summary>
        /// Directly routes a timer-expired fact to the prototype outcome router.
        /// </summary>
        public DropTheManRuntimeControllerResult HandleTimerExpired()
        {
            if (TerminalOutcomeAccepted)
            {
                return DropTheManRuntimeControllerResult.NoAction(
                    "Terminal outcome is already accepted.");
            }

            DropTheManOutcomeRoutingResult outcomeResult =
                _outcomeRouter.HandleTimerExpired(_gameStateSystem);

            bool terminalAccepted = outcomeResult.OutcomeWasAccepted;
            if (terminalAccepted)
            {
                AcceptTerminalOutcomeAndStopInput();
            }

            return DropTheManRuntimeControllerResult.TerminalOutcome(
                terminalAccepted,
                outcomeResult,
                outcomeResult.Reason);
        }

        /// <summary>
        /// Advances the optional countdown timer while Playing and routes ExpiredRaised facts.
        /// This uses the existing framework TimerSystem without adding CountUp support.
        /// </summary>
        public DropTheManRuntimeControllerResult TickTimer(float deltaTimeSeconds)
        {
            if (_timerSystem == null)
            {
                return DropTheManRuntimeControllerResult.NoAction("No timer system is configured.");
            }

            if (TerminalOutcomeAccepted)
            {
                return DropTheManRuntimeControllerResult.NoAction(
                    "Terminal outcome is already accepted.");
            }

            if (_gameStateSystem.CurrentState != GameState.Playing)
            {
                return DropTheManRuntimeControllerResult.NoAction(
                    $"Timer is not advanced while game state is {_gameStateSystem.CurrentState}.");
            }

            TimerAdvanceResult timerResult = _timerSystem.Advance(deltaTimeSeconds);
            if (!timerResult.IsSuccess)
            {
                return DropTheManRuntimeControllerResult.TimerAdvanced(
                    timerResult,
                    false,
                    default,
                    timerResult.FailureReason);
            }

            if (!timerResult.ExpiredRaised)
            {
                return DropTheManRuntimeControllerResult.TimerAdvanced(
                    timerResult,
                    false,
                    default,
                    string.Empty);
            }

            DropTheManOutcomeRoutingResult outcomeResult =
                _outcomeRouter.HandleTimerExpired(_gameStateSystem);

            bool terminalAccepted = outcomeResult.OutcomeWasAccepted;
            if (terminalAccepted)
            {
                AcceptTerminalOutcomeAndStopInput();
            }

            return DropTheManRuntimeControllerResult.TimerAdvanced(
                timerResult,
                terminalAccepted,
                outcomeResult,
                outcomeResult.Reason);
        }

        private bool TryCompleteTriggeredCollections(
            DropTheManDragSessionUpdateResult updateResult,
            out DropTheManFullHoleCompletionResult fullHoleCompletionResult,
            out string failureReason)
        {
            fullHoleCompletionResult = DropTheManFullHoleCompletionResult.NotTriggered();

            for (int i = 0; i < updateResult.NewlyCollectingStickmen.Count; i++)
            {
                DropTheManViewRegistryResult notifyResult =
                    _viewRegistry.NotifyCollectionStarted(
                        updateResult.NewlyCollectingStickmen[i]);
                if (!notifyResult.Success)
                {
                    failureReason = notifyResult.FailureReason;
                    return false;
                }

                DropTheManCollectionPresentationCompletionResult completionResult =
                    _dragSessionOwner.CompleteCollectionPresentation(
                        updateResult.NewlyCollectingStickmen[i]);
                if (!completionResult.Success)
                {
                    failureReason = completionResult.FailureReason;
                    return false;
                }

                if (completionResult.HoleBecameFull)
                {
                    fullHoleCompletionResult =
                        completionResult.FullHoleCompletionResult;
                    break;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private bool CanAcceptInput(out string failureReason)
        {
            if (TerminalOutcomeAccepted)
            {
                failureReason = "Terminal outcome is already accepted.";
                return false;
            }

            if (_gameStateSystem.CurrentState != GameState.Playing)
            {
                failureReason = $"Input is ignored while game state is {_gameStateSystem.CurrentState}.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private bool TryBeginFullHoleCompletionPresentation(
            DropTheManFullHoleCompletionResult beginResult,
            out DropTheManOutcomeRoutingResult outcomeResult,
            out bool outcomeWasHandled,
            out bool terminalAccepted,
            out string failureReason)
        {
            outcomeResult = default;
            outcomeWasHandled = false;
            terminalAccepted = false;
            failureReason = string.Empty;

            HoleRuntimeState hole = beginResult.Hole;
            if (hole == null)
            {
                failureReason = "A closing hole is required to begin completion presentation.";
                return false;
            }

            if (string.Equals(_activeHoleId, hole.Id, StringComparison.Ordinal))
            {
                CancelActiveDragWithoutReleaseCommit();
            }

            if (_snapFullHolesToNearestCellBeforeClosing)
            {
                AlignClosingHoleViewToNearestCell(hole);
            }

            DropTheManOutcomeRoutingResult callbackOutcomeResult = default;
            bool callbackOutcomeWasHandled = false;
            bool callbackTerminalAccepted = false;
            string callbackFailureReason = string.Empty;
            bool callbackInvoked = false;

            void FinalizeFromPresentation()
            {
                if (callbackInvoked)
                {
                    return;
                }

                callbackInvoked = true;
                if (!TryFinalizeFullHoleCompletion(
                        hole,
                        out callbackOutcomeResult,
                        out callbackOutcomeWasHandled,
                        out callbackTerminalAccepted,
                        out callbackFailureReason))
                {
                    Debug.LogError(callbackFailureReason);
                }
            }

            DropTheManViewRegistryResult presentationResult =
                _viewRegistry.BeginHoleCompletionPresentation(
                    hole.Id,
                    FinalizeFromPresentation);
            if (!presentationResult.Success)
            {
                Debug.LogWarning(
                    $"Hole '{hole.Id}' is using immediate completion fallback: " +
                    presentationResult.FailureReason);
                FinalizeFromPresentation();
            }

            if (!callbackInvoked)
            {
                return true;
            }

            outcomeResult = callbackOutcomeResult;
            outcomeWasHandled = callbackOutcomeWasHandled;
            terminalAccepted = callbackTerminalAccepted;
            failureReason = callbackFailureReason;
            return string.IsNullOrEmpty(failureReason);
        }

        private void AlignClosingHoleViewToNearestCell(HoleRuntimeState hole)
        {
            if (!_viewRegistry.TryGetHoleView(hole.Id, out IDropTheManHoleView view) ||
                view == null ||
                (view is UnityEngine.Object unityObject && unityObject == null))
            {
                return;
            }

            GridSnapResult snapResult = _fullHoleVisualSnapSystem.Evaluate(
                new GridSnapRequest(
                    view.WorldPosition,
                    hole.CurrentCoordinate,
                    hole.Footprint.Offsets,
                    _worldLayout,
                    _runtimeModel.FrameworkContext.GridBoard,
                    _runtimeModel.FrameworkContext.CellOccupancySystem));

            if (!snapResult.IsValid)
            {
                Debug.LogWarning(
                    $"Full hole '{hole.Id}' could not use its nearest closing cell and will " +
                    $"align to its previous committed cell instead: {snapResult.FailureReason}");
            }

            DropTheManViewRegistryResult applyResult =
                _viewRegistry.ApplyHoleWorldPosition(
                    hole.Id,
                    snapResult.SnappedWorldPosition);
            if (!applyResult.Success)
            {
                Debug.LogWarning(
                    $"Full hole '{hole.Id}' could not apply closing alignment: " +
                    applyResult.FailureReason);
            }
        }

        private bool TryFinalizeFullHoleCompletion(
            HoleRuntimeState hole,
            out DropTheManOutcomeRoutingResult outcomeResult,
            out bool outcomeWasHandled,
            out bool terminalAccepted,
            out string failureReason)
        {
            outcomeResult = default;
            outcomeWasHandled = false;
            terminalAccepted = false;

            DropTheManFullHoleCompletionResult completionResult =
                _dragSessionOwner.FinalizeFullHoleCompletion(_runtimeModel, hole);
            if (!completionResult.Success)
            {
                failureReason = completionResult.FailureReason;
                return false;
            }

            DropTheManViewRegistryResult selectableResult =
                _viewRegistry.SetHoleSelectable(hole.Id, false);
            if (!selectableResult.Success)
            {
                failureReason = selectableResult.FailureReason;
                return false;
            }

            outcomeResult = _outcomeRouter.HandleHoleCompleted(
                _runtimeModel,
                completionResult,
                _gameStateSystem);
            outcomeWasHandled = true;
            terminalAccepted = outcomeResult.OutcomeWasAccepted;
            if (terminalAccepted)
            {
                AcceptTerminalOutcomeAndStopInput();
            }

            failureReason = outcomeResult.Success
                ? string.Empty
                : outcomeResult.Reason;
            return outcomeResult.Success;
        }

        private void AcceptTerminalOutcomeAndStopInput()
        {
            _terminalOutcomeAccepted = true;
            CancelActiveDragWithoutReleaseCommit();
        }

        private void CancelActiveDragWithoutReleaseCommit()
        {
            if (_dragSessionOwner.HasSessionContext)
            {
                _dragSessionOwner.Cancel();
            }

            _activeHoleId = string.Empty;
        }

        private static bool IsTerminal(GameState gameState)
        {
            return gameState == GameState.Won || gameState == GameState.Lost;
        }
    }
}
