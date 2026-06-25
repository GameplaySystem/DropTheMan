using System;
using System.Collections.Generic;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.Interaction;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Result for prototype-owned release-time snap and occupancy commit.
    /// This reports whether release completed successfully, whether a new committed coordinate was
    /// written, and which world position should be treated as authoritative after release.
    /// Snap remains alignment and structural commit only; it does not own collection, color
    /// matching, capacity fill, or outcome routing.
    /// </summary>
    public readonly struct DropTheManReleaseCommitResult
    {
        private DropTheManReleaseCommitResult(
            bool success,
            bool wasCommitted,
            bool requiresVisualSnap,
            GridCoordinate committedOriginCoordinate,
            Vector3 authoritativeWorldPosition,
            string failureReason)
        {
            Success = success;
            WasCommitted = wasCommitted;
            RequiresVisualSnap = requiresVisualSnap;
            CommittedOriginCoordinate = committedOriginCoordinate;
            AuthoritativeWorldPosition = authoritativeWorldPosition;
            FailureReason = failureReason ?? string.Empty;
        }

        /// <summary>
        /// True when the release operation completed without structural commit failure.
        /// Invalid snap or rollback failure returns false.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// True only when release moved the hole to a new committed board origin.
        /// Same-origin release alignment is considered a successful no-op commit.
        /// </summary>
        public bool WasCommitted { get; }

        /// <summary>
        /// True when the visual hole should align to <see cref="AuthoritativeWorldPosition"/>.
        /// This is true for successful release-time snap even when the committed coordinate did not change.
        /// </summary>
        public bool RequiresVisualSnap { get; }

        /// <summary>
        /// Authoritative committed origin after the release operation finishes.
        /// On failure this remains the old committed origin.
        /// </summary>
        public GridCoordinate CommittedOriginCoordinate { get; }

        /// <summary>
        /// Authoritative world position after release.
        /// On failure this returns the old committed world position.
        /// </summary>
        public Vector3 AuthoritativeWorldPosition { get; }

        public string FailureReason { get; }

        public static DropTheManReleaseCommitResult Successful(
            bool wasCommitted,
            bool requiresVisualSnap,
            GridCoordinate committedOriginCoordinate,
            Vector3 authoritativeWorldPosition)
        {
            return new DropTheManReleaseCommitResult(
                true,
                wasCommitted,
                requiresVisualSnap,
                committedOriginCoordinate,
                authoritativeWorldPosition,
                string.Empty);
        }

        public static DropTheManReleaseCommitResult Failed(
            GridCoordinate committedOriginCoordinate,
            Vector3 authoritativeWorldPosition,
            string failureReason)
        {
            return new DropTheManReleaseCommitResult(
                false,
                false,
                false,
                committedOriginCoordinate,
                authoritativeWorldPosition,
                failureReason);
        }
    }

    /// <summary>
    /// Prototype-owned release service for non-full hole drag completion.
    /// This converts the last authoritative drag world position into a framework-valid snapped
    /// coordinate, transfers structural occupancy if the committed origin changes, and updates
    /// <see cref="HoleRuntimeState.CurrentCoordinate"/> only after the new footprint is fully occupied.
    /// This service must not trigger collection, color matching, capacity fill, full-hole closing,
    /// timer routing, or outcome logic.
    /// </summary>
    public sealed class DropTheManReleaseCommitService
    {
        private readonly IGridSnapSystem _gridSnapSystem;

        public DropTheManReleaseCommitService()
            : this(new GridSnapSystem())
        {
        }

        public DropTheManReleaseCommitService(IGridSnapSystem gridSnapSystem)
        {
            _gridSnapSystem = gridSnapSystem ?? throw new ArgumentNullException(nameof(gridSnapSystem));
        }

        /// <summary>
        /// Attempts release-time snap and committed occupancy transfer for one non-full active hole.
        /// Structural occupancy is mutated only during this release path, never during drag.
        /// If occupancy transfer fails after old occupancy is released, the service rolls back by
        /// clearing newly occupied cells, restoring old occupancy, keeping the old committed origin,
        /// and returning a failure result with the old committed world position.
        /// </summary>
        public DropTheManReleaseCommitResult CommitRelease(
            DropTheManRuntimeModel runtimeModel,
            HoleRuntimeState hole,
            Vector3 authoritativeDragWorldPosition,
            GridWorldLayout worldLayout)
        {
            if (runtimeModel == null)
            {
                return DropTheManReleaseCommitResult.Failed(
                    default,
                    authoritativeDragWorldPosition,
                    "Runtime model is required.");
            }

            if (hole == null)
            {
                return DropTheManReleaseCommitResult.Failed(
                    default,
                    authoritativeDragWorldPosition,
                    "Hole runtime state is required.");
            }

            GridCoordinate oldCommittedOrigin = hole.CurrentCoordinate;
            Vector3 oldCommittedWorldPosition = ToWorld(oldCommittedOrigin, worldLayout);

            if (!ContainsHoleReference(runtimeModel.Holes, hole))
            {
                return DropTheManReleaseCommitResult.Failed(
                    oldCommittedOrigin,
                    oldCommittedWorldPosition,
                    $"Hole '{hole.Id}' is not part of the provided runtime model.");
            }

            if (!hole.IsDraggable)
            {
                return DropTheManReleaseCommitResult.Failed(
                    oldCommittedOrigin,
                    oldCommittedWorldPosition,
                    $"Hole '{hole.Id}' is not eligible for non-full release in state {hole.LifecycleState}.");
            }

            if (worldLayout.CellSize.x <= 0f || worldLayout.CellSize.y <= 0f)
            {
                return DropTheManReleaseCommitResult.Failed(
                    oldCommittedOrigin,
                    oldCommittedWorldPosition,
                    "A positive grid world layout is required for release commit.");
            }

            GridSnapResult snapResult = _gridSnapSystem.Evaluate(
                new GridSnapRequest(
                    authoritativeDragWorldPosition,
                    oldCommittedOrigin,
                    hole.Footprint.Offsets,
                    worldLayout,
                    runtimeModel.FrameworkContext.GridBoard,
                    runtimeModel.FrameworkContext.CellOccupancySystem));

            if (!snapResult.IsValid || !snapResult.ResolvedOriginCell.HasValue)
            {
                return DropTheManReleaseCommitResult.Failed(
                    oldCommittedOrigin,
                    oldCommittedWorldPosition,
                    snapResult.FailureReason);
            }

            GridCoordinate snappedOrigin = snapResult.ResolvedOriginCell.Value;
            Vector3 snappedWorldPosition = snapResult.SnappedWorldPosition;
            if (snappedOrigin == oldCommittedOrigin)
            {
                return DropTheManReleaseCommitResult.Successful(
                    false,
                    true,
                    oldCommittedOrigin,
                    snappedWorldPosition);
            }

            IReadOnlyList<GridCoordinate> oldFootprint = hole.ResolveFootprintCoordinates();
            IReadOnlyList<GridCoordinate> newFootprint = hole.Footprint.ResolveCoordinates(snappedOrigin);
            CellOccupancySystem occupancySystem = runtimeModel.FrameworkContext.CellOccupancySystem;

            List<GridCoordinate> releasedOldCoordinates = new(oldFootprint.Count);
            for (int i = 0; i < oldFootprint.Count; i++)
            {
                CellOccupancyOperationResult releaseResult = occupancySystem.Release(oldFootprint[i]);
                if (!releaseResult.Success)
                {
                    string rollbackFailure =
                        TryRestoreOldFootprint(occupancySystem, releasedOldCoordinates);
                    return DropTheManReleaseCommitResult.Failed(
                        oldCommittedOrigin,
                        oldCommittedWorldPosition,
                        BuildFailureReason(
                            $"Failed to release old committed footprint for hole '{hole.Id}': {releaseResult.FailureReason}",
                            rollbackFailure));
                }

                releasedOldCoordinates.Add(oldFootprint[i]);
            }

            List<GridCoordinate> occupiedNewCoordinates = new(newFootprint.Count);
            for (int i = 0; i < newFootprint.Count; i++)
            {
                CellOccupancyOperationResult occupyResult = occupancySystem.Occupy(newFootprint[i]);
                if (!occupyResult.Success)
                {
                    string rollbackFailure = TryRollbackFailedTransfer(
                        occupancySystem,
                        releasedOldCoordinates,
                        occupiedNewCoordinates);

                    return DropTheManReleaseCommitResult.Failed(
                        oldCommittedOrigin,
                        oldCommittedWorldPosition,
                        BuildFailureReason(
                            $"Failed to occupy new snapped footprint for hole '{hole.Id}': {occupyResult.FailureReason}",
                            rollbackFailure));
                }

                occupiedNewCoordinates.Add(newFootprint[i]);
            }

            hole.MoveTo(snappedOrigin);
            return DropTheManReleaseCommitResult.Successful(
                true,
                true,
                snappedOrigin,
                snappedWorldPosition);
        }

        private static string TryRollbackFailedTransfer(
            CellOccupancySystem occupancySystem,
            IReadOnlyList<GridCoordinate> releasedOldCoordinates,
            IReadOnlyList<GridCoordinate> occupiedNewCoordinates)
        {
            List<string> rollbackFailures = new();

            for (int i = occupiedNewCoordinates.Count - 1; i >= 0; i--)
            {
                CellOccupancyOperationResult releaseResult =
                    occupancySystem.Release(occupiedNewCoordinates[i]);
                if (!releaseResult.Success)
                {
                    rollbackFailures.Add(
                        $"Failed to release partially occupied coordinate {occupiedNewCoordinates[i]}: {releaseResult.FailureReason}");
                }
            }

            string restoreFailure = TryRestoreOldFootprint(
                occupancySystem,
                releasedOldCoordinates);
            if (!string.IsNullOrEmpty(restoreFailure))
            {
                rollbackFailures.Add(restoreFailure);
            }

            return rollbackFailures.Count == 0
                ? string.Empty
                : string.Join(" | ", rollbackFailures);
        }

        private static string TryRestoreOldFootprint(
            CellOccupancySystem occupancySystem,
            IReadOnlyList<GridCoordinate> releasedOldCoordinates)
        {
            List<string> restoreFailures = new();

            for (int i = 0; i < releasedOldCoordinates.Count; i++)
            {
                CellOccupancyOperationResult occupyResult =
                    occupancySystem.Occupy(releasedOldCoordinates[i]);
                if (!occupyResult.Success)
                {
                    restoreFailures.Add(
                        $"Failed to restore old committed coordinate {releasedOldCoordinates[i]}: {occupyResult.FailureReason}");
                }
            }

            return restoreFailures.Count == 0
                ? string.Empty
                : string.Join(" | ", restoreFailures);
        }

        private static Vector3 ToWorld(
            GridCoordinate coordinate,
            GridWorldLayout worldLayout)
        {
            return new Vector3(
                worldLayout.BoardOrigin.x + (coordinate.X * worldLayout.CellSize.x),
                worldLayout.BoardOrigin.y + (coordinate.Y * worldLayout.CellSize.y),
                worldLayout.BoardOrigin.z);
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
