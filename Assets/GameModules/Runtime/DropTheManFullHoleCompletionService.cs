using System;
using System.Collections.Generic;
using PuzzleFramework.CoreBoard;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Result for prototype-owned full-hole completion.
    /// This reports whether completion was evaluated for the current update, whether it succeeded,
    /// and whether later outcome routing should treat the hole as newly completed.
    /// </summary>
    public readonly struct DropTheManFullHoleCompletionResult
    {
        private DropTheManFullHoleCompletionResult(
            bool wasEvaluated,
            bool success,
            bool holeCompleted,
            bool shouldNotifyHoleCompleted,
            HoleRuntimeState hole,
            string failureReason)
        {
            WasEvaluated = wasEvaluated;
            Success = success;
            HoleCompleted = holeCompleted;
            ShouldNotifyHoleCompleted = shouldNotifyHoleCompleted;
            Hole = hole;
            FailureReason = failureReason ?? string.Empty;
        }

        /// <summary>
        /// True when the full-hole completion service actually evaluated the current update.
        /// Non-full drag updates carry a NotTriggered result.
        /// </summary>
        public bool WasEvaluated { get; }

        /// <summary>
        /// True when the evaluated completion flow finished without integrity failure.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// True when the hole finished the MVP completion flow and is now Completed.
        /// </summary>
        public bool HoleCompleted { get; }

        /// <summary>
        /// True only when the current evaluation newly completed a hole and future outcome routing
        /// may consume that fact.
        /// </summary>
        public bool ShouldNotifyHoleCompleted { get; }

        /// <summary>
        /// Hole reference evaluated by the completion flow.
        /// This remains null when completion was never triggered.
        /// </summary>
        public HoleRuntimeState Hole { get; }

        /// <summary>
        /// Stable hole identifier for callers that prefer id-based routing.
        /// </summary>
        public string HoleId => Hole?.Id ?? string.Empty;

        public string FailureReason { get; }

        public static DropTheManFullHoleCompletionResult NotTriggered()
        {
            return new DropTheManFullHoleCompletionResult(
                false,
                false,
                false,
                false,
                null,
                string.Empty);
        }

        public static DropTheManFullHoleCompletionResult Completed(HoleRuntimeState hole)
        {
            return new DropTheManFullHoleCompletionResult(
                true,
                true,
                true,
                true,
                hole ?? throw new ArgumentNullException(nameof(hole)),
                string.Empty);
        }

        public static DropTheManFullHoleCompletionResult Failed(
            HoleRuntimeState hole,
            string failureReason)
        {
            return new DropTheManFullHoleCompletionResult(
                true,
                false,
                false,
                false,
                hole,
                failureReason);
        }
    }

    /// <summary>
    /// Prototype-owned completion owner for holes that already reached Full during drag.
    /// This releases the stale committed occupancy footprint, transitions Full to Closing to
    /// Completed synchronously for MVP, and exposes a narrow hole-completed result for future
    /// outcome routing.
    /// This service must not request win/loss, run timer arbitration, or perform presentation work.
    /// </summary>
    public sealed class DropTheManFullHoleCompletionService
    {
        /// <summary>
        /// Completes a hole that already became Full during drag-time collection.
        /// The hole must still be part of the provided runtime model and must currently be in the
        /// Full state. Active, Closing, and Completed holes are rejected.
        /// If releasing stale committed occupancy fails, the hole remains Full and completion fails.
        /// </summary>
        public DropTheManFullHoleCompletionResult CompleteFullHole(
            DropTheManRuntimeModel runtimeModel,
            HoleRuntimeState hole)
        {
            if (runtimeModel == null)
            {
                return DropTheManFullHoleCompletionResult.Failed(
                    null,
                    "Runtime model is required.");
            }

            if (hole == null)
            {
                return DropTheManFullHoleCompletionResult.Failed(
                    null,
                    "Hole runtime state is required.");
            }

            if (!ContainsHoleReference(runtimeModel.Holes, hole))
            {
                return DropTheManFullHoleCompletionResult.Failed(
                    hole,
                    $"Hole '{hole.Id}' is not part of the provided runtime model.");
            }

            if (hole.LifecycleState != HoleLifecycleState.Full)
            {
                return DropTheManFullHoleCompletionResult.Failed(
                    hole,
                    $"Hole '{hole.Id}' is not eligible for full-hole completion in state {hole.LifecycleState}.");
            }

            IReadOnlyList<GridCoordinate> committedFootprint = hole.ResolveFootprintCoordinates();
            CellOccupancySystem occupancySystem = runtimeModel.FrameworkContext.CellOccupancySystem;
            List<GridCoordinate> releasedCoordinates = new(committedFootprint.Count);

            for (int i = 0; i < committedFootprint.Count; i++)
            {
                CellOccupancyOperationResult releaseResult =
                    occupancySystem.Release(committedFootprint[i]);
                if (!releaseResult.Success)
                {
                    string rollbackFailure =
                        TryRestoreCommittedFootprint(occupancySystem, releasedCoordinates);
                    return DropTheManFullHoleCompletionResult.Failed(
                        hole,
                        BuildFailureReason(
                            $"Failed to release committed occupancy for full hole '{hole.Id}': {releaseResult.FailureReason}",
                            rollbackFailure));
                }

                releasedCoordinates.Add(committedFootprint[i]);
            }

            hole.BeginClosing();
            hole.MarkCompleted();
            return DropTheManFullHoleCompletionResult.Completed(hole);
        }

        private static string TryRestoreCommittedFootprint(
            CellOccupancySystem occupancySystem,
            IReadOnlyList<GridCoordinate> releasedCoordinates)
        {
            List<string> restoreFailures = new();

            for (int i = 0; i < releasedCoordinates.Count; i++)
            {
                CellOccupancyOperationResult occupyResult =
                    occupancySystem.Occupy(releasedCoordinates[i]);
                if (!occupyResult.Success)
                {
                    restoreFailures.Add(
                        $"Failed to restore committed coordinate {releasedCoordinates[i]}: {occupyResult.FailureReason}");
                }
            }

            return restoreFailures.Count == 0
                ? string.Empty
                : string.Join(" | ", restoreFailures);
        }

        private static bool ContainsHoleReference(
            IReadOnlyList<HoleRuntimeState> holes,
            HoleRuntimeState hole)
        {
            for (int i = 0; i < holes.Count; i++)
            {
                if (ReferenceEquals(holes[i], hole))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildFailureReason(
            string primaryFailure,
            string rollbackFailure)
        {
            return string.IsNullOrEmpty(rollbackFailure)
                ? primaryFailure
                : $"{primaryFailure} Rollback issue: {rollbackFailure}";
        }
    }
}
