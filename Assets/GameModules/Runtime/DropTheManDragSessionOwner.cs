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
            IReadOnlyList<StickmanRuntimeState> newlyReservedStickmen,
            IReadOnlyList<StickmanRuntimeState> newlyCollectingStickmen,
            DropTheManFullHoleCompletionResult fullHoleCompletionResult,
            string failureReason)
        {
            Success = success;
            AuthoritativeWorldPosition = authoritativeWorldPosition;
            SessionIsActive = sessionIsActive;
            ShouldStopDragging = shouldStopDragging;
            HoleBecameFull = holeBecameFull;
            NewlyReservedStickmen = newlyReservedStickmen ??
                                     Array.Empty<StickmanRuntimeState>();
            NewlyCollectingStickmen = newlyCollectingStickmen ??
                                      Array.Empty<StickmanRuntimeState>();
            FullHoleCompletionResult = fullHoleCompletionResult;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public Vector3 AuthoritativeWorldPosition { get; }
        public bool SessionIsActive { get; }
        public bool ShouldStopDragging { get; }
        public bool HoleBecameFull { get; }
        public IReadOnlyList<StickmanRuntimeState> NewlyReservedStickmen { get; }
        public IReadOnlyList<StickmanRuntimeState> NewlyCollectingStickmen { get; }
        /// <summary>
        /// Compatibility field from the immediate-fill flow. The collection-timing path reports
        /// full completion through DropTheManCollectionPresentationCompletionResult instead.
        /// </summary>
        public DropTheManFullHoleCompletionResult FullHoleCompletionResult { get; }
        public string FailureReason { get; }

        public static DropTheManDragSessionUpdateResult Evaluated(
            Vector3 authoritativeWorldPosition,
            bool sessionIsActive,
            bool shouldStopDragging,
            bool holeBecameFull,
            IList<StickmanRuntimeState> newlyReservedStickmen,
            IList<StickmanRuntimeState> newlyCollectingStickmen,
            DropTheManFullHoleCompletionResult fullHoleCompletionResult)
        {
            return new DropTheManDragSessionUpdateResult(
                true,
                authoritativeWorldPosition,
                sessionIsActive,
                shouldStopDragging,
                holeBecameFull,
                new ReadOnlyCollection<StickmanRuntimeState>(
                    new List<StickmanRuntimeState>(
                        newlyReservedStickmen ?? throw new ArgumentNullException(nameof(newlyReservedStickmen)))),
                new ReadOnlyCollection<StickmanRuntimeState>(
                    new List<StickmanRuntimeState>(
                        newlyCollectingStickmen ?? throw new ArgumentNullException(nameof(newlyCollectingStickmen)))),
                fullHoleCompletionResult,
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
                Array.Empty<StickmanRuntimeState>(),
                DropTheManFullHoleCompletionResult.NotTriggered(),
                failureReason);
        }

        public static DropTheManDragSessionUpdateResult FailedAfterFullHoleStop(
            Vector3 authoritativeWorldPosition,
            IList<StickmanRuntimeState> newlyReservedStickmen,
            IList<StickmanRuntimeState> newlyCollectingStickmen,
            DropTheManFullHoleCompletionResult fullHoleCompletionResult,
            string failureReason)
        {
            return new DropTheManDragSessionUpdateResult(
                false,
                authoritativeWorldPosition,
                false,
                true,
                true,
                new ReadOnlyCollection<StickmanRuntimeState>(
                    new List<StickmanRuntimeState>(
                        newlyReservedStickmen ?? throw new ArgumentNullException(nameof(newlyReservedStickmen)))),
                new ReadOnlyCollection<StickmanRuntimeState>(
                    new List<StickmanRuntimeState>(
                        newlyCollectingStickmen ?? throw new ArgumentNullException(nameof(newlyCollectingStickmen)))),
                fullHoleCompletionResult,
                failureReason);
        }
    }

    /// <summary>
    /// Result for converting one synchronously completed collection presentation into fill.
    /// Full-hole completion remains an explicit nested result for outcome routing.
    /// </summary>
    public readonly struct DropTheManCollectionPresentationCompletionResult
    {
        private DropTheManCollectionPresentationCompletionResult(
            bool success,
            bool holeBecameFull,
            string holeId,
            string stickmanId,
            DropTheManFullHoleCompletionResult fullHoleCompletionResult,
            string failureReason)
        {
            Success = success;
            HoleBecameFull = holeBecameFull;
            HoleId = holeId ?? string.Empty;
            StickmanId = stickmanId ?? string.Empty;
            FullHoleCompletionResult = fullHoleCompletionResult;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public bool HoleBecameFull { get; }
        public string HoleId { get; }
        public string StickmanId { get; }
        public DropTheManFullHoleCompletionResult FullHoleCompletionResult { get; }
        public string FailureReason { get; }

        public static DropTheManCollectionPresentationCompletionResult Completed(
            HoleRuntimeState hole,
            StickmanRuntimeState stickman,
            bool holeBecameFull,
            DropTheManFullHoleCompletionResult fullHoleCompletionResult)
        {
            return new DropTheManCollectionPresentationCompletionResult(
                true,
                holeBecameFull,
                hole.Id,
                stickman.Id,
                fullHoleCompletionResult,
                string.Empty);
        }

        public static DropTheManCollectionPresentationCompletionResult Failed(
            string failureReason)
        {
            return new DropTheManCollectionPresentationCompletionResult(
                false,
                false,
                string.Empty,
                string.Empty,
                DropTheManFullHoleCompletionResult.NotTriggered(),
                failureReason);
        }

        public static DropTheManCollectionPresentationCompletionResult FailedAfterFull(
            HoleRuntimeState hole,
            StickmanRuntimeState stickman,
            DropTheManFullHoleCompletionResult fullHoleCompletionResult)
        {
            return new DropTheManCollectionPresentationCompletionResult(
                false,
                true,
                hole.Id,
                stickman.Id,
                fullHoleCompletionResult,
                fullHoleCompletionResult.FailureReason);
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
        /// <summary>
        /// Legacy handoff flag from the earlier deferred-release slice.
        /// The current implementation performs non-full release commit immediately through the
        /// dedicated release-commit service, so successful release now returns false here.
        /// </summary>
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
    /// <see cref="HoleRuntimeState.CurrentCoordinate"/> remains committed board state during drag
    /// even though non-full release later updates it through the dedicated release-commit service.
    /// Full holes are handled separately by the dedicated completion service and do not enter the
    /// normal non-full release snap/commit path.
    /// </summary>
    public sealed class DropTheManDragSessionOwner
    {
        private static readonly IReadOnlyList<GridCoordinate> EmptyFootprint =
            Array.Empty<GridCoordinate>();

        private readonly DropTheManMovementCoordinator _movementCoordinator = new();
        private readonly DropTheManReleaseCommitService _releaseCommitService = new();
        private readonly DropTheManFullHoleCompletionService _fullHoleCompletionService = new();

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
        public DropTheManDragSessionUpdateResult UpdateDrag(
            Vector3 candidateWorldPosition,
            float collectionTriggerRadiusInCells,
            float dragClearanceInsetCells)
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
                        _worldLayout,
                        collectionTriggerRadiusInCells,
                        dragClearanceInsetCells));

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
                coordinatorResult.NewlyReservedStickmen as IList<StickmanRuntimeState> ??
                new List<StickmanRuntimeState>(coordinatorResult.NewlyReservedStickmen),
                coordinatorResult.NewlyCollectingStickmen as IList<StickmanRuntimeState> ??
                new List<StickmanRuntimeState>(coordinatorResult.NewlyCollectingStickmen),
                DropTheManFullHoleCompletionResult.NotTriggered());
        }

        /// <summary>
        /// Converts one triggered collection into fill after its presentation hook returns.
        /// The current placeholder is synchronous; a future animation slice may defer this call.
        /// </summary>
        public DropTheManCollectionPresentationCompletionResult CompleteCollectionPresentation(
            StickmanRuntimeState stickman)
        {
            if (_activeHole == null || _runtimeModel == null)
            {
                return DropTheManCollectionPresentationCompletionResult.Failed(
                    "No drag session exists for collection presentation completion.");
            }

            if (stickman == null ||
                !ContainsStickmanReference(_runtimeModel.Stickmen, stickman))
            {
                return DropTheManCollectionPresentationCompletionResult.Failed(
                    "Collection presentation stickman is not part of the active runtime model.");
            }

            if (stickman.LifecycleState != StickmanLifecycleState.Collecting ||
                !string.Equals(
                    stickman.ReservedHoleId,
                    _activeHole.Id,
                    StringComparison.Ordinal))
            {
                return DropTheManCollectionPresentationCompletionResult.Failed(
                    $"Stickman '{stickman.Id}' is not collecting for active hole '{_activeHole.Id}'.");
            }

            if (!_activeHole.TryCompleteReservedCollectible())
            {
                return DropTheManCollectionPresentationCompletionResult.Failed(
                    $"Hole '{_activeHole.Id}' could not convert a reserved slot into fill.");
            }

            stickman.MarkCollected();

            bool holeBecameFull =
                _activeHole.LifecycleState == HoleLifecycleState.Full;
            if (!holeBecameFull)
            {
                return DropTheManCollectionPresentationCompletionResult.Completed(
                    _activeHole,
                    stickman,
                    false,
                    DropTheManFullHoleCompletionResult.NotTriggered());
            }

            _isSessionActive = false;
            _sessionEndedBecauseHoleBecameFull = true;
            DropTheManFullHoleCompletionResult completionResult =
                _fullHoleCompletionService.BeginFullHoleCompletion(_runtimeModel, _activeHole);

            return completionResult.Success
                ? DropTheManCollectionPresentationCompletionResult.Completed(
                    _activeHole,
                    stickman,
                    true,
                    completionResult)
                : DropTheManCollectionPresentationCompletionResult.FailedAfterFull(
                    _activeHole,
                    stickman,
                    completionResult);
        }

        /// <summary>
        /// Finalizes a hole after its direct completion-presentation callback. This operation is
        /// independent of active drag-session context because the drag is cleared before the
        /// closing presentation starts.
        /// </summary>
        public DropTheManFullHoleCompletionResult FinalizeFullHoleCompletion(
            DropTheManRuntimeModel runtimeModel,
            HoleRuntimeState hole)
        {
            return _fullHoleCompletionService.FinalizeFullHoleCompletion(
                runtimeModel,
                hole);
        }

        /// <summary>
        /// Ends the current drag session.
        /// Non-full active holes delegate release-time snap and occupancy commit to the dedicated
        /// release-commit service. A hole that already stopped because it became full does not
        /// route through the normal non-full release path.
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
            Vector3 authoritativeWorldPosition = PreviousAcceptedWorldPosition;

            if (!endedBecauseHoleBecameFull && _isSessionActive && _runtimeModel != null)
            {
                DropTheManReleaseCommitResult commitResult =
                    _releaseCommitService.CommitRelease(
                        _runtimeModel,
                        _activeHole,
                        authoritativeWorldPosition,
                        _worldLayout);

                authoritativeWorldPosition = commitResult.AuthoritativeWorldPosition;
                ClearSession();

                return commitResult.Success
                    ? DropTheManDragSessionReleaseResult.Released(
                        authoritativeWorldPosition,
                        false,
                        false)
                    : DropTheManDragSessionReleaseResult.Failed(
                        authoritativeWorldPosition,
                        commitResult.FailureReason);
            }

            ClearSession();

            return DropTheManDragSessionReleaseResult.Released(
                authoritativeWorldPosition,
                false,
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

        private static bool ContainsStickmanReference(
            IReadOnlyList<StickmanRuntimeState> stickmen,
            StickmanRuntimeState stickman)
        {
            for (int i = 0; i < stickmen.Count; i++)
            {
                if (ReferenceEquals(stickmen[i], stickman))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
