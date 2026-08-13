using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PuzzleFramework.CoreBoard;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned request for drag-time rule coordination.
    /// This keeps movement-rule inputs explicit without moving puzzle meaning into framework types.
    /// The caller must provide freeform drag positions because visual drag state is intentionally
    /// tracked outside <see cref="HoleRuntimeState"/>.
    /// During drag, the caller should preserve both the previous accepted world position and the
    /// new candidate world position for the current drag update.
    /// </summary>
    public readonly struct DropTheManMovementCoordinatorRequest
    {
        public DropTheManMovementCoordinatorRequest(
            DropTheManRuntimeModel runtimeModel,
            HoleRuntimeState hole,
            Vector3 previousAcceptedWorldPosition,
            Vector3 candidateWorldPosition,
            GridWorldLayout worldLayout,
            float collectionTriggerRadiusInCells)
        {
            RuntimeModel = runtimeModel;
            Hole = hole;
            PreviousAcceptedWorldPosition = previousAcceptedWorldPosition;
            CandidateWorldPosition = candidateWorldPosition;
            WorldLayout = worldLayout;
            CollectionTriggerRadiusInCells = collectionTriggerRadiusInCells;
        }

        public DropTheManRuntimeModel RuntimeModel { get; }
        public HoleRuntimeState Hole { get; }
        /// <summary>
        /// Last gameplay-accepted freeform world position for the moving hole during the
        /// current drag session.
        /// </summary>
        public Vector3 PreviousAcceptedWorldPosition { get; }
        /// <summary>
        /// New freeform candidate world position requested by the current drag update.
        /// </summary>
        public Vector3 CandidateWorldPosition { get; }
        public GridWorldLayout WorldLayout { get; }
        public float CollectionTriggerRadiusInCells { get; }
    }

    /// <summary>
    /// Prototype-owned result for one drag-time movement and collection evaluation.
    /// This reports authoritative movement, accepted collections, and whether dragging should stop.
    /// It does not represent committed board state because release-time snap and structural
    /// occupancy commit are intentionally not implemented by this slice yet.
    /// </summary>
    public readonly struct DropTheManMovementCoordinatorResult
    {
        public DropTheManMovementCoordinatorResult(
            bool success,
            Vector3 authoritativeWorldPosition,
            bool wasBlocked,
            bool shouldStopDragging,
            bool holeBecameFull,
            IReadOnlyList<StickmanRuntimeState> newlyReservedStickmen,
            IReadOnlyList<StickmanRuntimeState> newlyCollectingStickmen,
            string failureReason)
        {
            Success = success;
            AuthoritativeWorldPosition = authoritativeWorldPosition;
            WasBlocked = wasBlocked;
            ShouldStopDragging = shouldStopDragging;
            HoleBecameFull = holeBecameFull;
            NewlyReservedStickmen = newlyReservedStickmen ??
                                     Array.Empty<StickmanRuntimeState>();
            NewlyCollectingStickmen = newlyCollectingStickmen ??
                                      Array.Empty<StickmanRuntimeState>();
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        /// <summary>
        /// Authoritative freeform world position accepted by drag-time gameplay logic for this update.
        /// This is not the same thing as a committed board origin coordinate.
        /// </summary>
        public Vector3 AuthoritativeWorldPosition { get; }
        public bool WasBlocked { get; }
        /// <summary>
        /// Future drag-session integration must treat this as authoritative.
        /// A full hole must stop normal dragging immediately.
        /// </summary>
        public bool ShouldStopDragging { get; }
        public bool HoleBecameFull { get; }
        public IReadOnlyList<StickmanRuntimeState> NewlyReservedStickmen { get; }
        public IReadOnlyList<StickmanRuntimeState> NewlyCollectingStickmen { get; }
        public string FailureReason { get; }

        public static DropTheManMovementCoordinatorResult Evaluated(
            Vector3 authoritativeWorldPosition,
            bool wasBlocked,
            bool shouldStopDragging,
            bool holeBecameFull,
            IList<StickmanRuntimeState> newlyReservedStickmen,
            IList<StickmanRuntimeState> newlyCollectingStickmen,
            string failureReason)
        {
            return new DropTheManMovementCoordinatorResult(
                true,
                authoritativeWorldPosition,
                wasBlocked,
                shouldStopDragging,
                holeBecameFull,
                new ReadOnlyCollection<StickmanRuntimeState>(
                    new List<StickmanRuntimeState>(
                        newlyReservedStickmen ?? throw new ArgumentNullException(nameof(newlyReservedStickmen)))),
                new ReadOnlyCollection<StickmanRuntimeState>(
                    new List<StickmanRuntimeState>(
                        newlyCollectingStickmen ?? throw new ArgumentNullException(nameof(newlyCollectingStickmen)))),
                failureReason);
        }

        public static DropTheManMovementCoordinatorResult Failed(
            Vector3 authoritativeWorldPosition,
            string failureReason,
            bool shouldStopDragging = false)
        {
            return new DropTheManMovementCoordinatorResult(
                false,
                authoritativeWorldPosition,
                false,
                shouldStopDragging,
                false,
                Array.Empty<StickmanRuntimeState>(),
                Array.Empty<StickmanRuntimeState>(),
                failureReason);
        }
    }

    /// <summary>
    /// Prototype-owned rule coordinator for freeform drag-time enterability, collection,
    /// and capacity interruption.
    /// This consumes generic framework board data plus the swept-footprint helper without
    /// moving gameplay meaning into framework drag or occupancy systems.
    /// During drag, <see cref="PuzzleFramework.CoreBoard.CellOccupancySystem"/> remains committed
    /// to the hole's pre-drag footprint. This coordinator ignores only that committed self-footprint
    /// and does not update structural occupancy during drag.
    /// </summary>
    public sealed class DropTheManMovementCoordinator
    {
        private static readonly GridCoordinate[] SingleCellOffsets =
        {
            new(0, 0)
        };

        private readonly SweptFootprintHelper _sweptFootprintHelper = new();

        /// <summary>
        /// Evaluates one drag update and applies drag-time reservation and trigger truth.
        /// This is a mutating apply step, not a pure preview query.
        /// Successful execution may:
        /// - reserve matching stickmen and remove them from the active coordinate lookup
        /// - change reserved stickmen to Collecting when the trigger threshold is reached
        /// Capacity fill and Full transition wait for presentation completion.
        /// The result's authoritative world position is still not committed board state because
        /// release-time snap and occupancy commit are intentionally deferred.
        /// Snap must not trigger collection; collection truth is resolved here during drag.
        /// </summary>
        public DropTheManMovementCoordinatorResult EvaluateAndApply(
            DropTheManMovementCoordinatorRequest request)
        {
            if (request.RuntimeModel == null)
            {
                return DropTheManMovementCoordinatorResult.Failed(
                    request.PreviousAcceptedWorldPosition,
                    "Runtime model is required.");
            }

            if (request.Hole == null)
            {
                return DropTheManMovementCoordinatorResult.Failed(
                    request.PreviousAcceptedWorldPosition,
                    "Hole runtime state is required.");
            }

            if (!request.Hole.IsDraggable)
            {
                return DropTheManMovementCoordinatorResult.Failed(
                    request.PreviousAcceptedWorldPosition,
                    $"Hole '{request.Hole.Id}' is not draggable in state {request.Hole.LifecycleState}.",
                    true);
            }

            if (request.CollectionTriggerRadiusInCells <= 0f)
            {
                return DropTheManMovementCoordinatorResult.Failed(
                    request.PreviousAcceptedWorldPosition,
                    "Collection trigger radius must be positive.");
            }

            List<StickmanRuntimeState> newlyReservedStickmen = new();
            List<StickmanRuntimeState> newlyCollectingStickmen = new();
            HashSet<GridCoordinate> committedHoleFootprint =
                BuildCommittedHoleFootprint(request.Hole);
            Vector3 boundedCandidateWorldPosition =
                ClampCandidateToBoardBounds(request);
            IReadOnlyList<SweptFootprintContactGroup> contactGroups =
                _sweptFootprintHelper.EnumerateContactGroups(
                    new SweptFootprintRequest(
                        request.PreviousAcceptedWorldPosition,
                        boundedCandidateWorldPosition,
                        request.WorldLayout,
                        request.Hole.Footprint.Offsets));

            Vector3 authoritativeWorldPosition = request.PreviousAcceptedWorldPosition;

            for (int groupIndex = 0; groupIndex < contactGroups.Count; groupIndex++)
            {
                SweptFootprintContactGroup contactGroup = contactGroups[groupIndex];
                if (TryGetBlockingReason(
                        request,
                        committedHoleFootprint,
                        contactGroup,
                        out string blockingReason))
                {
                    return BuildEvaluatedResult(
                        request,
                        authoritativeWorldPosition,
                        true,
                        newlyReservedStickmen,
                        newlyCollectingStickmen,
                        blockingReason);
                }

                authoritativeWorldPosition =
                    contactGroup.SampledPosition.ToWorld(request.WorldLayout);

                for (int candidateIndex = 0;
                     candidateIndex < contactGroup.OrderedCandidates.Count;
                     candidateIndex++)
                {
                    GridCoordinate coordinate =
                        contactGroup.OrderedCandidates[candidateIndex].Coordinate;
                    if (!request.RuntimeModel.StickmanIndex.TryGet(coordinate, out StickmanRuntimeState stickman) ||
                        stickman.ColorIdentity != request.Hole.ColorIdentity)
                    {
                        continue;
                    }

                    if (!request.Hole.HasUnreservedCapacity)
                    {
                        return BuildEvaluatedResult(
                            request,
                            authoritativeWorldPosition,
                            true,
                            newlyReservedStickmen,
                            newlyCollectingStickmen,
                            $"Hole '{request.Hole.Id}' has no unreserved capacity for collectible at {coordinate}.");
                    }

                    if (!request.Hole.TryReserveCollectible())
                    {
                        return DropTheManMovementCoordinatorResult.Failed(
                            authoritativeWorldPosition,
                            $"Hole '{request.Hole.Id}' could not reserve capacity for stickman '{stickman.Id}'.");
                    }

                    if (!request.RuntimeModel.StickmanIndex.TryReserve(
                            coordinate,
                            request.Hole.Id,
                            out stickman))
                    {
                        request.Hole.TryCancelReservedCollectible();
                        continue;
                    }

                    newlyReservedStickmen.Add(stickman);
                }
            }

            return BuildEvaluatedResult(
                request,
                boundedCandidateWorldPosition,
                false,
                newlyReservedStickmen,
                newlyCollectingStickmen,
                string.Empty);
        }

        private static DropTheManMovementCoordinatorResult BuildEvaluatedResult(
            DropTheManMovementCoordinatorRequest request,
            Vector3 authoritativeWorldPosition,
            bool wasBlocked,
            IList<StickmanRuntimeState> newlyReservedStickmen,
            IList<StickmanRuntimeState> newlyCollectingStickmen,
            string failureReason)
        {
            TriggerReservedStickmen(
                request,
                authoritativeWorldPosition,
                newlyCollectingStickmen);

            return DropTheManMovementCoordinatorResult.Evaluated(
                authoritativeWorldPosition,
                wasBlocked,
                false,
                false,
                newlyReservedStickmen,
                newlyCollectingStickmen,
                failureReason);
        }

        private static void TriggerReservedStickmen(
            DropTheManMovementCoordinatorRequest request,
            Vector3 authoritativeWorldPosition,
            ICollection<StickmanRuntimeState> newlyCollectingStickmen)
        {
            BoardLocalContinuousPosition holeOrigin =
                BoardLocalContinuousPosition.FromWorld(
                    authoritativeWorldPosition,
                    request.WorldLayout);
            float thresholdSquared =
                request.CollectionTriggerRadiusInCells *
                request.CollectionTriggerRadiusInCells;

            for (int i = 0; i < request.RuntimeModel.Stickmen.Count; i++)
            {
                StickmanRuntimeState stickman = request.RuntimeModel.Stickmen[i];
                if (stickman.LifecycleState != StickmanLifecycleState.Reserved ||
                    !string.Equals(
                        stickman.ReservedHoleId,
                        request.Hole.Id,
                        StringComparison.Ordinal) ||
                    !IsWithinCollectionThreshold(
                        request.Hole,
                        holeOrigin,
                        stickman.Coordinate,
                        thresholdSquared))
                {
                    continue;
                }

                stickman.BeginCollection();
                newlyCollectingStickmen.Add(stickman);
            }
        }

        private static bool IsWithinCollectionThreshold(
            HoleRuntimeState hole,
            BoardLocalContinuousPosition holeOrigin,
            GridCoordinate stickmanCoordinate,
            float thresholdSquared)
        {
            IReadOnlyList<GridCoordinate> offsets = hole.Footprint.Offsets.Count > 0
                ? hole.Footprint.Offsets
                : SingleCellOffsets;

            for (int i = 0; i < offsets.Count; i++)
            {
                float deltaX = holeOrigin.X + offsets[i].X - stickmanCoordinate.X;
                float deltaY = holeOrigin.Y + offsets[i].Y - stickmanCoordinate.Y;
                if (deltaX * deltaX + deltaY * deltaY <= thresholdSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<GridCoordinate> BuildCommittedHoleFootprint(HoleRuntimeState hole)
        {
            // Self-occupancy ignore is intentionally based on the committed board footprint only.
            // The hole's freeform visual drag position is external to HoleRuntimeState, and
            // structural occupancy is not updated during drag in this slice.
            return new HashSet<GridCoordinate>(hole.ResolveFootprintCoordinates());
        }

        private static Vector3 ClampCandidateToBoardBounds(
            DropTheManMovementCoordinatorRequest request)
        {
            IReadOnlyList<GridCoordinate> offsets = request.Hole.Footprint.Offsets.Count > 0
                ? request.Hole.Footprint.Offsets
                : SingleCellOffsets;

            int minOffsetX = offsets[0].X;
            int minOffsetY = offsets[0].Y;
            int maxOffsetXExclusive = offsets[0].X + 1;
            int maxOffsetYExclusive = offsets[0].Y + 1;

            for (int i = 1; i < offsets.Count; i++)
            {
                minOffsetX = Mathf.Min(minOffsetX, offsets[i].X);
                minOffsetY = Mathf.Min(minOffsetY, offsets[i].Y);
                maxOffsetXExclusive = Mathf.Max(maxOffsetXExclusive, offsets[i].X + 1);
                maxOffsetYExclusive = Mathf.Max(maxOffsetYExclusive, offsets[i].Y + 1);
            }

            float minX = -minOffsetX;
            float minY = -minOffsetY;
            float maxX = request.RuntimeModel.FrameworkContext.GridBoard.Width - maxOffsetXExclusive;
            float maxY = request.RuntimeModel.FrameworkContext.GridBoard.Height - maxOffsetYExclusive;

            if (minX > maxX || minY > maxY)
            {
                return request.PreviousAcceptedWorldPosition;
            }

            BoardLocalContinuousPosition candidate =
                BoardLocalContinuousPosition.FromWorld(
                    request.CandidateWorldPosition,
                    request.WorldLayout);
            BoardLocalContinuousPosition clampedCandidate =
                new(
                    Mathf.Clamp(candidate.X, minX, maxX),
                    Mathf.Clamp(candidate.Y, minY, maxY));

            Vector3 candidateBoardPlaneWorld = candidate.ToWorld(request.WorldLayout);
            Vector3 clampedBoardPlaneWorld = clampedCandidate.ToWorld(request.WorldLayout);
            Vector3 heightOffset = request.CandidateWorldPosition - candidateBoardPlaneWorld;
            return clampedBoardPlaneWorld + heightOffset;
        }

        private static bool TryGetBlockingReason(
            DropTheManMovementCoordinatorRequest request,
            HashSet<GridCoordinate> committedHoleFootprint,
            SweptFootprintContactGroup contactGroup,
            out string blockingReason)
        {
            for (int i = 0; i < contactGroup.OverlappedCells.Count; i++)
            {
                GridCoordinate coordinate = contactGroup.OverlappedCells[i];
                if (!request.RuntimeModel.FrameworkContext.GridBoard.IsWithinBounds(coordinate))
                {
                    blockingReason =
                        $"Movement footprint reached out-of-bounds coordinate {coordinate}.";
                    return true;
                }

                if (!request.RuntimeModel.FrameworkContext.GridBoard.ContainsCell(coordinate))
                {
                    blockingReason =
                        $"Movement footprint reached inactive structural coordinate {coordinate}.";
                    return true;
                }

                if (request.RuntimeModel.FrameworkContext.GridBoard.IsBlocked(coordinate))
                {
                    blockingReason =
                        $"Movement footprint reached blocked structural coordinate {coordinate}.";
                    return true;
                }

                if (request.RuntimeModel.FrameworkContext.CellOccupancySystem.IsReserved(coordinate) &&
                    !committedHoleFootprint.Contains(coordinate))
                {
                    blockingReason =
                        $"Movement footprint reached reserved coordinate {coordinate}.";
                    return true;
                }

                if (request.RuntimeModel.FrameworkContext.CellOccupancySystem.IsOccupied(coordinate) &&
                    !committedHoleFootprint.Contains(coordinate))
                {
                    blockingReason =
                        $"Movement footprint reached occupied coordinate {coordinate}.";
                    return true;
                }

                if (request.RuntimeModel.StickmanIndex.TryGet(coordinate, out StickmanRuntimeState stickman) &&
                    stickman.ColorIdentity != request.Hole.ColorIdentity)
                {
                    blockingReason =
                        $"Movement footprint reached wrong-color collectible coordinate {coordinate}.";
                    return true;
                }
            }

            blockingReason = string.Empty;
            return false;
        }
    }
}
