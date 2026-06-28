using System;
using PuzzleFramework.CoreBoard;
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
        private readonly GameStateSystem _gameStateSystem;
        private readonly TimerSystem _timerSystem;

        private string _activeHoleId = string.Empty;
        private bool _terminalOutcomeAccepted;

        public DropTheManRuntimeController(
            DropTheManRuntimeModel runtimeModel,
            GridWorldLayout worldLayout,
            DropTheManViewRegistry viewRegistry,
            GameStateSystem gameStateSystem,
            DropTheManDragSessionOwner dragSessionOwner = null,
            DropTheManOutcomeRouter outcomeRouter = null,
            TimerSystem timerSystem = null)
        {
            _runtimeModel = runtimeModel ?? throw new ArgumentNullException(nameof(runtimeModel));
            _worldLayout = worldLayout;
            _viewRegistry = viewRegistry ?? throw new ArgumentNullException(nameof(viewRegistry));
            _gameStateSystem = gameStateSystem ?? throw new ArgumentNullException(nameof(gameStateSystem));
            _dragSessionOwner = dragSessionOwner ?? new DropTheManDragSessionOwner();
            _outcomeRouter = outcomeRouter ?? new DropTheManOutcomeRouter();
            _timerSystem = timerSystem;
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
                _dragSessionOwner.UpdateDrag(candidateWorldPosition);

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

            DropTheManViewRegistryResult collectionNotifyResult =
                NotifyCollectingStickmen(updateResult);
            if (!collectionNotifyResult.Success)
            {
                CancelActiveDragWithoutReleaseCommit();
                return DropTheManRuntimeControllerResult.UpdatedDrag(
                    false,
                    updateResult.AuthoritativeWorldPosition,
                    false,
                    default,
                    collectionNotifyResult.FailureReason);
            }

            DropTheManOutcomeRoutingResult outcomeResult = default;
            bool terminalAccepted = false;
            bool outcomeWasHandled = false;

            if (updateResult.FullHoleCompletionResult.Success &&
                updateResult.FullHoleCompletionResult.HoleCompleted &&
                updateResult.FullHoleCompletionResult.ShouldNotifyHoleCompleted)
            {
                DropTheManViewRegistryResult selectableResult =
                    _viewRegistry.SetHoleSelectable(
                        updateResult.FullHoleCompletionResult.HoleId,
                        false);
                if (!selectableResult.Success)
                {
                    CancelActiveDragWithoutReleaseCommit();
                    return DropTheManRuntimeControllerResult.UpdatedDrag(
                        false,
                        updateResult.AuthoritativeWorldPosition,
                        false,
                        default,
                        selectableResult.FailureReason);
                }

                outcomeResult = _outcomeRouter.HandleHoleCompleted(
                    _runtimeModel,
                    updateResult.FullHoleCompletionResult,
                    _gameStateSystem);
                outcomeWasHandled = true;

                terminalAccepted = outcomeResult.OutcomeWasAccepted;
                if (terminalAccepted)
                {
                    AcceptTerminalOutcomeAndStopInput();
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

        private DropTheManViewRegistryResult NotifyCollectingStickmen(
            DropTheManDragSessionUpdateResult updateResult)
        {
            for (int i = 0; i < updateResult.NewlyCollectingStickmen.Count; i++)
            {
                DropTheManViewRegistryResult notifyResult =
                    _viewRegistry.NotifyCollectionStarted(
                        updateResult.NewlyCollectingStickmen[i]);
                if (!notifyResult.Success)
                {
                    return notifyResult;
                }
            }

            return DropTheManViewRegistryResult.Successful();
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
