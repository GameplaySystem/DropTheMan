using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PuzzleFramework.CoreBoard;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Result for attempting to begin a prototype-owned drag session.
    /// </summary>
    public readonly struct DropTheManDragSessionBeginResult
    {
        private DropTheManDragSessionBeginResult(
            bool success,
            bool sessionIsActive,
            Vector3 authoritativeWorldPosition,
            string failureReason)
        {
            Success = success;
            SessionIsActive = sessionIsActive;
            AuthoritativeWorldPosition = authoritativeWorldPosition;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public bool SessionIsActive { get; }
        public Vector3 AuthoritativeWorldPosition { get; }
        public string FailureReason { get; }

        public static DropTheManDragSessionBeginResult Started(
            Vector3 authoritativeWorldPosition)
        {
            return new DropTheManDragSessionBeginResult(
                true,
                true,
                authoritativeWorldPosition,
                string.Empty);
        }

        public static DropTheManDragSessionBeginResult Failed(
            Vector3 authoritativeWorldPosition,
            string failureReason)
        {
            return new DropTheManDragSessionBeginResult(
                false,
                false,
                authoritativeWorldPosition,
                failureReason);
        }
    }

    /// <summary>
    /// Result for one drag-session update.
    /// </summary>
    public readonly struct DropTheManDragSessionUpdateResult
    {
        private DropTheManDragSessionUpdateResult(
            bool success,
            Vector3 authoritativeWorldPosition,
            bool sessionIsActive,
            bool shouldStopDragging,
            bool holeBecameFull,
            IReadOnlyList<StickmanRuntimeState> newlyCollectingStickmen,
            string failureReason)
        {
            Success = success;
            AuthoritativeWorldPosition = authoritativeWorldPosition;
            SessionIsActive = sessionIsActive;
            ShouldStopDragging = shouldStopDragging;
            HoleBecameFull = holeBecameFull;
            NewlyCollectingStickmen = newlyCollectingStickmen ??
                                      Array.Empty<StickmanRuntimeState>();
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public Vector3 AuthoritativeWorldPosition { get; }
        public bool SessionIsActive { get; }
        public bool ShouldStopDragging { get; }
        public bool HoleBecameFull { get; }
        public IReadOnlyList<StickmanRuntimeState> NewlyCollectingStickmen { get; }
        public string FailureReason { get; }

        public static DropTheManDragSessionUpdateResult Evaluated(
            Vector3 authoritativeWorldPosition,
            bool sessionIsActive,
            bool shouldStopDragging,
            bool holeBecameFull,
            IList<StickmanRuntimeState> newlyCollectingStickmen)
        {
            return new DropTheManDragSessionUpdateResult(
                true,
                authoritativeWorldPosition,
                sessionIsActive,
                shouldStopDragging,
                holeBecameFull,
                new ReadOnlyCollection<StickmanRuntimeState>(
                    new List<StickmanRuntimeState>(
                        newlyCollectingStickmen ?? throw new ArgumentNullException(nameof(newlyCollectingStickmen)))),
                string.Empty);
        }

        public static DropTheManDragSessionUpdateResult Failed(
            Vector3 authoritativeWorldPosition,
            string failureReason)
        {
            return new DropTheManDragSessionUpdateResult(
                false,
                authoritativeWorldPosition,
                false,
                false,
                false,
                Array.Empty<StickmanRuntimeState>(),
                failureReason);
        }
    }

    /// <summary>
    /// Result for ending or releasing a prototype-owned drag session.
    /// </summary>
    public readonly struct DropTheManDragSessionReleaseResult
    {
        private DropTheManDragSessionReleaseResult(
            bool success,
            Vector3 authoritativeWorldPosition,
            bool requiresFutureSnapCommit,
            bool endedBecauseHoleBecameFull,
            string failureReason)
        {
            Success = success;
            AuthoritativeWorldPosition = authoritativeWorldPosition;
            RequiresFutureSnapCommit = requiresFutureSnapCommit;
            EndedBecauseHoleBecameFull = endedBecauseHoleBecameFull;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public Vector3 AuthoritativeWorldPosition { get; }
        public bool RequiresFutureSnapCommit { get; }
        public bool EndedBecauseHoleBecameFull { get; }
        public string FailureReason { get; }

        public static DropTheManDragSessionReleaseResult Released(
            Vector3 authoritativeWorldPosition,
            bool requiresFutureSnapCommit,
            bool endedBecauseHoleBecameFull)
        {
            return new DropTheManDragSessionReleaseResult(
                true,
                authoritativeWorldPosition,
                requiresFutureSnapCommit,
                endedBecauseHoleBecameFull,
                string.Empty);
        }

        public static DropTheManDragSessionReleaseResult Failed(
            Vector3 authoritativeWorldPosition,
            string failureReason)
        {
            return new DropTheManDragSessionReleaseResult(
                false,
                authoritativeWorldPosition,
                false,
                false,
                failureReason);
        }
    }

    /// <summary>
    /// Prototype-owned owner for one active hole drag session.
    /// This treats <see cref="DropTheManMovementCoordinator.EvaluateAndApply"/> as a mutating
    /// apply step rather than a harmless preview query.
    /// The caller must not invoke <see cref="UpdateDrag"/> twice for the same pointer sample.
    /// Visual movement must follow the returned authoritative world position instead of raw
    /// pointer position.
    /// <see cref="HoleRuntimeState.CurrentCoordinate"/> remains committed board state during drag,
    /// framework occupancy remains committed to the pre-drag footprint during drag, and snap or
    /// committed occupancy updates are intentionally deferred.
    /// </summary>
    public sealed class DropTheManDragSessionOwner
    {
        private static readonly IReadOnlyList<GridCoordinate> EmptyFootprint =
            Array.Empty<GridCoordinate>();

        private readonly DropTheManMovementCoordinator _movementCoordinator = new();

        private DropTheManRuntimeModel _runtimeModel;
        private HoleRuntimeState _activeHole;
        private GridWorldLayout _worldLayout;
        private bool _isSessionActive;
        private bool _sessionEndedBecauseHoleBecameFull;

        public HoleRuntimeState ActiveHole => _activeHole;
        public bool HasSessionContext => _activeHole != null;
        public bool IsSessionActive => _isSessionActive;
        public GridCoordinate DragStartCommittedCoordinate { get; private set; }
        public IReadOnlyList<GridCoordinate> DragStartCommittedFootprint { get; private set; } =
            EmptyFootprint;
        public Vector3 PreviousAcceptedWorldPosition { get; private set; }

        /// <summary>
        /// Begins a new drag session for one active hole.
        /// No gameplay mutation happens here beyond capturing session state.
        /// </summary>
        public DropTheManDragSessionBeginResult TryBeginDrag(
            DropTheManRuntimeModel runtimeModel,
            HoleRuntimeState hole,
            Vector3 initialWorldPosition,
            GridWorldLayout worldLayout)
        {
            if (_activeHole != null)
            {
                return DropTheManDragSessionBeginResult.Failed(
                    PreviousAcceptedWorldPosition,
                    "A drag session is already active or awaiting release cleanup.");
            }

            if (runtimeModel == null)
            {
                return DropTheManDragSessionBeginResult.Failed(
                    initialWorldPosition,
                    "Runtime model is required.");
            }

            if (hole == null)
            {
                return DropTheManDragSessionBeginResult.Failed(
                    initialWorldPosition,
                    "Hole runtime state is required.");
            }

            if (!ContainsHoleReference(runtimeModel.Holes, hole))
            {
                return DropTheManDragSessionBeginResult.Failed(
                    initialWorldPosition,
                    $"Hole '{hole.Id}' is not part of the provided runtime model.");
            }

            if (!hole.IsDraggable)
            {
                return DropTheManDragSessionBeginResult.Failed(
                    initialWorldPosition,
                    $"Hole '{hole.Id}' is not draggable in state {hole.LifecycleState}.");
            }

            if (worldLayout.CellSize.x <= 0f || worldLayout.CellSize.y <= 0f)
            {
                return DropTheManDragSessionBeginResult.Failed(
                    initialWorldPosition,
                    "A positive grid world layout is required to begin drag.");
            }

            _runtimeModel = runtimeModel;
            _activeHole = hole;
            _worldLayout = worldLayout;
            _isSessionActive = true;
            _sessionEndedBecauseHoleBecameFull = false;
            DragStartCommittedCoordinate = hole.CurrentCoordinate;
            DragStartCommittedFootprint = new ReadOnlyCollection<GridCoordinate>(
                new List<GridCoordinate>(hole.ResolveFootprintCoordinates()));
            PreviousAcceptedWorldPosition = initialWorldPosition;

            return DropTheManDragSessionBeginResult.Started(initialWorldPosition);
        }

        /// <summary>
        /// Applies one drag update through the movement coordinator.
        /// This method must be the only owner that calls the mutating coordinator for the active
        /// session so accidental preview or double-apply misuse does not fan out across the codebase.
        /// The caller remains responsible for not submitting the same pointer sample twice.
        /// </summary>
        public DropTheManDragSessionUpdateResult UpdateDrag(Vector3 candidateWorldPosition)
        {
            if (!_isSessionActive || _activeHole == null || _runtimeModel == null)
            {
                return DropTheManDragSessionUpdateResult.Failed(
                    PreviousAcceptedWorldPosition,
                    "No active drag session exists.");
            }

            DropTheManMovementCoordinatorResult coordinatorResult =
                _movementCoordinator.EvaluateAndApply(
                    new DropTheManMovementCoordinatorRequest(
                        _runtimeModel,
                        _activeHole,
                        PreviousAcceptedWorldPosition,
                        candidateWorldPosition,
                        _worldLayout));

            PreviousAcceptedWorldPosition = coordinatorResult.AuthoritativeWorldPosition;

            if (!coordinatorResult.Success)
            {
                Vector3 authoritativeWorldPosition = PreviousAcceptedWorldPosition;
                ClearSession();
                return DropTheManDragSessionUpdateResult.Failed(
                    authoritativeWorldPosition,
                    coordinatorResult.FailureReason);
            }

            if (coordinatorResult.ShouldStopDragging)
            {
                _isSessionActive = false;
                _sessionEndedBecauseHoleBecameFull = coordinatorResult.HoleBecameFull;
            }

            return DropTheManDragSessionUpdateResult.Evaluated(
                PreviousAcceptedWorldPosition,
                _isSessionActive,
                coordinatorResult.ShouldStopDragging,
                coordinatorResult.HoleBecameFull,
                coordinatorResult.NewlyCollectingStickmen as IList<StickmanRuntimeState> ??
                new List<StickmanRuntimeState>(coordinatorResult.NewlyCollectingStickmen));
        }

        /// <summary>
        /// Ends the current drag session without performing snap or committed occupancy updates.
        /// A non-full hole reports that future snap or commit work is still required.
        /// A hole that already stopped because it became full does not route through normal
        /// release-time snap behavior in this slice.
        /// </summary>
        public DropTheManDragSessionReleaseResult Release()
        {
            if (_activeHole == null)
            {
                return DropTheManDragSessionReleaseResult.Failed(
                    PreviousAcceptedWorldPosition,
                    "No drag session exists to release.");
            }

            bool endedBecauseHoleBecameFull = _sessionEndedBecauseHoleBecameFull;
            bool requiresFutureSnapCommit = _isSessionActive && _activeHole.IsDraggable;
            Vector3 authoritativeWorldPosition = PreviousAcceptedWorldPosition;

            ClearSession();

            return DropTheManDragSessionReleaseResult.Released(
                authoritativeWorldPosition,
                requiresFutureSnapCommit,
                endedBecauseHoleBecameFull);
        }

        /// <summary>
        /// Clears the current drag-session state without invoking release-time gameplay.
        /// </summary>
        public void Cancel()
        {
            ClearSession();
        }

        private void ClearSession()
        {
            _runtimeModel = null;
            _activeHole = null;
            _worldLayout = default;
            _isSessionActive = false;
            _sessionEndedBecauseHoleBecameFull = false;
            DragStartCommittedCoordinate = default;
            DragStartCommittedFootprint = EmptyFootprint;
            PreviousAcceptedWorldPosition = default;
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
    }
}
