using PuzzleFramework.RuntimeFlow;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Result for prototype-owned outcome routing.
    /// This reports whether a terminal outcome was requested, whether the framework game state
    /// accepted it, and why a fact did not produce an outcome when no request was made.
    /// </summary>
    public readonly struct DropTheManOutcomeRoutingResult
    {
        private DropTheManOutcomeRoutingResult(
            bool success,
            bool outcomeWasRequested,
            bool outcomeWasAccepted,
            bool ignoredBecauseTerminalAlreadyAccepted,
            GameState? requestedOutcome,
            GameState currentState,
            string reason)
        {
            Success = success;
            OutcomeWasRequested = outcomeWasRequested;
            OutcomeWasAccepted = outcomeWasAccepted;
            IgnoredBecauseTerminalAlreadyAccepted = ignoredBecauseTerminalAlreadyAccepted;
            RequestedOutcome = requestedOutcome;
            CurrentState = currentState;
            Reason = reason ?? string.Empty;
        }

        /// <summary>
        /// True when the router handled the input fact without an internal or framework-transition failure.
        /// Ignored and no-outcome facts are successful routing results.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// True when this routing step attempted to request a terminal framework game state.
        /// </summary>
        public bool OutcomeWasRequested { get; }

        /// <summary>
        /// True when the requested terminal outcome was accepted by the framework game state system.
        /// </summary>
        public bool OutcomeWasAccepted { get; }

        /// <summary>
        /// True when a later fact was ignored because a terminal outcome had already been accepted.
        /// </summary>
        public bool IgnoredBecauseTerminalAlreadyAccepted { get; }

        /// <summary>
        /// Requested terminal state, when this routing step made or ignored a terminal request.
        /// </summary>
        public GameState? RequestedOutcome { get; }

        /// <summary>
        /// Framework game state after the routing step, or the current known state for no-op results.
        /// </summary>
        public GameState CurrentState { get; }

        /// <summary>
        /// Reason no outcome was requested or why a requested outcome failed.
        /// Empty for successful accepted terminal requests.
        /// </summary>
        public string Reason { get; }

        public static DropTheManOutcomeRoutingResult NoOutcome(
            GameState currentState,
            string reason)
        {
            return new DropTheManOutcomeRoutingResult(
                true,
                false,
                false,
                false,
                null,
                currentState,
                reason);
        }

        public static DropTheManOutcomeRoutingResult IgnoredAfterTerminalAccepted(
            GameState acceptedTerminalState,
            GameState requestedOutcome,
            string reason)
        {
            return new DropTheManOutcomeRoutingResult(
                true,
                false,
                false,
                true,
                requestedOutcome,
                acceptedTerminalState,
                reason);
        }

        public static DropTheManOutcomeRoutingResult Accepted(
            GameStateTransitionResult transitionResult)
        {
            return new DropTheManOutcomeRoutingResult(
                true,
                true,
                true,
                false,
                transitionResult.RequestedState,
                transitionResult.CurrentState,
                string.Empty);
        }

        public static DropTheManOutcomeRoutingResult Rejected(
            GameStateTransitionResult transitionResult)
        {
            return new DropTheManOutcomeRoutingResult(
                false,
                true,
                false,
                false,
                transitionResult.RequestedState,
                transitionResult.CurrentState,
                transitionResult.FailureReason);
        }

        public static DropTheManOutcomeRoutingResult Failed(
            GameState currentState,
            string reason)
        {
            return new DropTheManOutcomeRoutingResult(
                false,
                false,
                false,
                false,
                null,
                currentState,
                reason);
        }
    }

    /// <summary>
    /// Prototype-owned outcome router for Drop The Man terminal outcomes.
    /// It consumes completed-hole and timer-expired facts, evaluates puzzle-specific outcome
    /// meaning, and requests generic terminal states through the framework GameStateSystem.
    /// </summary>
    public sealed class DropTheManOutcomeRouter
    {
        private bool _terminalOutcomeAccepted;
        private GameState? _acceptedTerminalOutcome;

        /// <summary>
        /// Handles a completed-hole fact from the full-hole completion flow.
        /// Victory is requested only after all required holes are Completed. For MVP, every runtime
        /// hole in the model is required.
        /// </summary>
        public DropTheManOutcomeRoutingResult HandleHoleCompleted(
            DropTheManRuntimeModel runtimeModel,
            DropTheManFullHoleCompletionResult completionResult,
            GameStateSystem gameStateSystem)
        {
            if (gameStateSystem == null)
            {
                return DropTheManOutcomeRoutingResult.Failed(
                    default,
                    "Game state system is required.");
            }

            if (!completionResult.Success ||
                !completionResult.HoleCompleted ||
                !completionResult.ShouldNotifyHoleCompleted)
            {
                return DropTheManOutcomeRoutingResult.NoOutcome(
                    gameStateSystem.CurrentState,
                    "Completion result did not report a newly completed hole.");
            }

            if (TryGetAcceptedTerminalState(gameStateSystem, out GameState acceptedTerminalState))
            {
                return DropTheManOutcomeRoutingResult.IgnoredAfterTerminalAccepted(
                    acceptedTerminalState,
                    GameState.Won,
                    $"Terminal outcome {acceptedTerminalState} is already accepted.");
            }

            if (runtimeModel == null)
            {
                return DropTheManOutcomeRoutingResult.Failed(
                    gameStateSystem.CurrentState,
                    "Runtime model is required.");
            }

            if (!AreAllRequiredHolesCompleted(runtimeModel))
            {
                return DropTheManOutcomeRoutingResult.NoOutcome(
                    gameStateSystem.CurrentState,
                    "Not all required holes are completed.");
            }

            return RequestTerminalOutcome(gameStateSystem, GameState.Won);
        }

        /// <summary>
        /// Handles a timer-expired fact. The timer owns only the expiration fact; this router owns
        /// the Drop The Man consequence of requesting Lost.
        /// </summary>
        public DropTheManOutcomeRoutingResult HandleTimerExpired(
            GameStateSystem gameStateSystem)
        {
            if (gameStateSystem == null)
            {
                return DropTheManOutcomeRoutingResult.Failed(
                    default,
                    "Game state system is required.");
            }

            if (TryGetAcceptedTerminalState(gameStateSystem, out GameState acceptedTerminalState))
            {
                return DropTheManOutcomeRoutingResult.IgnoredAfterTerminalAccepted(
                    acceptedTerminalState,
                    GameState.Lost,
                    $"Terminal outcome {acceptedTerminalState} is already accepted.");
            }

            return RequestTerminalOutcome(gameStateSystem, GameState.Lost);
        }

        private DropTheManOutcomeRoutingResult RequestTerminalOutcome(
            GameStateSystem gameStateSystem,
            GameState requestedTerminalState)
        {
            GameStateTransitionResult transitionResult =
                gameStateSystem.TryTransitionTo(requestedTerminalState);

            if (!transitionResult.IsSuccess)
            {
                return DropTheManOutcomeRoutingResult.Rejected(transitionResult);
            }

            _terminalOutcomeAccepted = true;
            _acceptedTerminalOutcome = transitionResult.CurrentState;
            return DropTheManOutcomeRoutingResult.Accepted(transitionResult);
        }

        private bool TryGetAcceptedTerminalState(
            GameStateSystem gameStateSystem,
            out GameState acceptedTerminalState)
        {
            if (_terminalOutcomeAccepted && _acceptedTerminalOutcome.HasValue)
            {
                acceptedTerminalState = _acceptedTerminalOutcome.Value;
                return true;
            }

            if (IsTerminal(gameStateSystem.CurrentState))
            {
                _terminalOutcomeAccepted = true;
                _acceptedTerminalOutcome = gameStateSystem.CurrentState;
                acceptedTerminalState = gameStateSystem.CurrentState;
                return true;
            }

            acceptedTerminalState = default;
            return false;
        }

        private static bool AreAllRequiredHolesCompleted(
            DropTheManRuntimeModel runtimeModel)
        {
            if (runtimeModel.Holes.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < runtimeModel.Holes.Count; i++)
            {
                if (runtimeModel.Holes[i].LifecycleState != HoleLifecycleState.Completed)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsTerminal(GameState gameState)
        {
            return gameState == GameState.Won || gameState == GameState.Lost;
        }
    }
}
