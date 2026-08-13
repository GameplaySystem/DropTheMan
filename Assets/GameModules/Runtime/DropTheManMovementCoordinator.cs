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
            float collectionTriggerRadiusInCells,
            float dragClearanceInsetCells)
        {
            RuntimeModel = runtimeModel;
            Hole = hole;
            PreviousAcceptedWorldPosition = previousAcceptedWorldPosition;
            CandidateWorldPosition = candidateWorldPosition;
            WorldLayout = worldLayout;
            CollectionTriggerRadiusInCells = collectionTriggerRadiusInCells;
            DragClearanceInsetCells = dragClearanceInsetCells;
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
        public float DragClearanceInsetCells { get; }
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

            if (request.DragClearanceInsetCells < 0f || request.DragClearanceInsetCells > 0.45f)
            {
                return DropTheManMovementCoordinatorResult.Failed(
                    request.PreviousAcceptedWorldPosition,
                    "Drag clearance inset must stay between 0 and 0.45 cells.");
            }

            List<StickmanRuntimeState> newlyReservedStickmen = new();
            List<StickmanRuntimeState> newlyCollectingStickmen = new();
            HashSet<GridCoordinate> committedHoleFootprint =
                BuildCommittedHoleFootprint(request.Hole);
            ShapeAwareDragFootprint dragFootprint =
                new(request.Hole.Footprint.Offsets, request.DragClearanceInsetCells);
            Vector3 boundedCandidateWorldPosition =
                ClampCandidateToBoardBounds(request, dragFootprint);
            Vector3 authoritativeWorldPosition =
                ResolveAuthoritativeWorldPosition(
                    request,
                    committedHoleFootprint,
                    dragFootprint,
                    boundedCandidateWorldPosition,
                    out bool wasBlocked,
                    out string blockingReason);

            IReadOnlyList<SweptFootprintContactGroup> contactGroups =
                _sweptFootprintHelper.EnumerateContactGroups(
                    new SweptFootprintRequest(
                        request.PreviousAcceptedWorldPosition,
                        authoritativeWorldPosition,
                        request.WorldLayout,
                        dragFootprint.Rectangles));

            for (int groupIndex = 0; groupIndex < contactGroups.Count; groupIndex++)
            {
                SweptFootprintContactGroup contactGroup = contactGroups[groupIndex];
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
                authoritativeWorldPosition,
                wasBlocked,
                newlyReservedStickmen,
                newlyCollectingStickmen,
                blockingReason);
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

        private Vector3 ResolveAuthoritativeWorldPosition(
            DropTheManMovementCoordinatorRequest request,
            HashSet<GridCoordinate> committedHoleFootprint,
            ShapeAwareDragFootprint dragFootprint,
            Vector3 boundedCandidateWorldPosition,
            out bool wasBlocked,
            out string blockingReason)
        {
            if (IsPathClear(
                    request,
                    committedHoleFootprint,
                    dragFootprint,
                    request.PreviousAcceptedWorldPosition,
                    boundedCandidateWorldPosition,
                    out blockingReason))
            {
                wasBlocked = false;
                return boundedCandidateWorldPosition;
            }

            wasBlocked = true;
            Vector3 bestWorldPosition = request.PreviousAcceptedWorldPosition;
            float bestProgressSquared = 0f;

            BoardLocalContinuousPosition previous =
                BoardLocalContinuousPosition.FromWorld(
                    request.PreviousAcceptedWorldPosition,
                    request.WorldLayout);
            BoardLocalContinuousPosition candidate =
                BoardLocalContinuousPosition.FromWorld(
                    boundedCandidateWorldPosition,
                    request.WorldLayout);

            Vector3 horizontalSlideWorld =
                CreateWorldPositionFromBoardLocal(
                    request,
                    new BoardLocalContinuousPosition(candidate.X, previous.Y),
                    boundedCandidateWorldPosition);
            TrySelectBetterSlidingCandidate(
                request,
                committedHoleFootprint,
                dragFootprint,
                horizontalSlideWorld,
                ref bestWorldPosition,
                ref bestProgressSquared);

            Vector3 verticalSlideWorld =
                CreateWorldPositionFromBoardLocal(
                    request,
                    new BoardLocalContinuousPosition(previous.X, candidate.Y),
                    boundedCandidateWorldPosition);
            TrySelectBetterSlidingCandidate(
                request,
                committedHoleFootprint,
                dragFootprint,
                verticalSlideWorld,
                ref bestWorldPosition,
                ref bestProgressSquared);

            return bestWorldPosition;
        }

        private static Vector3 ClampCandidateToBoardBounds(
            DropTheManMovementCoordinatorRequest request,
            ShapeAwareDragFootprint dragFootprint)
        {
            float minX = -dragFootprint.MinX;
            float minY = -dragFootprint.MinY;
            float maxX = request.RuntimeModel.FrameworkContext.GridBoard.Width - dragFootprint.MaxX;
            float maxY = request.RuntimeModel.FrameworkContext.GridBoard.Height - dragFootprint.MaxY;

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

        private bool IsPathClear(
            DropTheManMovementCoordinatorRequest request,
            HashSet<GridCoordinate> committedHoleFootprint,
            ShapeAwareDragFootprint dragFootprint,
            Vector3 fromWorldPosition,
            Vector3 toWorldPosition,
            out string blockingReason)
        {
            IReadOnlyList<SweptFootprintContactGroup> contactGroups =
                _sweptFootprintHelper.EnumerateContactGroups(
                    new SweptFootprintRequest(
                        fromWorldPosition,
                        toWorldPosition,
                        request.WorldLayout,
                        dragFootprint.Rectangles));

            for (int groupIndex = 0; groupIndex < contactGroups.Count; groupIndex++)
            {
                if (TryGetBlockingReason(
                        request,
                        committedHoleFootprint,
                        contactGroups[groupIndex],
                        out blockingReason))
                {
                    return false;
                }
            }

            blockingReason = string.Empty;
            return true;
        }

        private void TrySelectBetterSlidingCandidate(
            DropTheManMovementCoordinatorRequest request,
            HashSet<GridCoordinate> committedHoleFootprint,
            ShapeAwareDragFootprint dragFootprint,
            Vector3 candidateWorldPosition,
            ref Vector3 bestWorldPosition,
            ref float bestProgressSquared)
        {
            if (!IsPathClear(
                    request,
                    committedHoleFootprint,
                    dragFootprint,
                    request.PreviousAcceptedWorldPosition,
                    candidateWorldPosition,
                    out _))
            {
                return;
            }

            float progressSquared =
                (candidateWorldPosition - request.PreviousAcceptedWorldPosition).sqrMagnitude;
            if (progressSquared <= bestProgressSquared)
            {
                return;
            }

            bestWorldPosition = candidateWorldPosition;
            bestProgressSquared = progressSquared;
        }

        private static Vector3 CreateWorldPositionFromBoardLocal(
            DropTheManMovementCoordinatorRequest request,
            BoardLocalContinuousPosition boardLocalPosition,
            Vector3 referenceWorldPosition)
        {
            Vector3 referenceBoardPlaneWorld =
                BoardLocalContinuousPosition.FromWorld(
                    referenceWorldPosition,
                    request.WorldLayout).ToWorld(request.WorldLayout);
            Vector3 targetBoardPlaneWorld = boardLocalPosition.ToWorld(request.WorldLayout);
            Vector3 heightOffset = referenceWorldPosition - referenceBoardPlaneWorld;
            return targetBoardPlaneWorld + heightOffset;
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
