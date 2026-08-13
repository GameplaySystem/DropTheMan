using System;
using System.Collections.Generic;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.Presentation;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned lifecycle for a draggable collector.
    /// This stays game-specific because the meaning of each state is puzzle-owned.
    /// </summary>
    public enum HoleLifecycleState
    {
        Active = 0,
        Full = 1,
        Closing = 2,
        Completed = 3
    }

    /// <summary>
    /// Prototype-owned lifecycle for collectible targets.
    /// This separates immediate gameplay acceptance from later visual completion.
    /// </summary>
    public enum StickmanLifecycleState
    {
        Available = 0,
        Reserved = 1,
        Collecting = 2,
        Collected = 3
    }

    /// <summary>
    /// Prototype-owned mutable runtime state for a hole.
    /// This carries puzzle-specific meaning and stays outside the framework.
    /// CurrentCoordinate is the committed board origin for the hole footprint, not the live
    /// freeform visual drag position. Do not update it on every drag sample.
    /// A later release/snap/commit owner should update it only when committed board state changes.
    /// </summary>
    public sealed class HoleRuntimeState
    {
        public HoleRuntimeState(
            string id,
            GridCoordinate currentCoordinate,
            ShapeFootprint footprint,
            ColorIdentity colorIdentity)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Hole runtime state id is required.", nameof(id));
            }

            if (colorIdentity == ColorIdentity.None)
            {
                throw new ArgumentException(
                    "Hole runtime state requires a color identity.",
                    nameof(colorIdentity));
            }

            Id = id;
            CurrentCoordinate = currentCoordinate;
            Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            ColorIdentity = colorIdentity;
            LifecycleState = HoleLifecycleState.Active;
        }

        public string Id { get; }
        /// <summary>
        /// Committed board origin for the hole footprint.
        /// This is not the same thing as the live freeform drag position during interaction.
        /// </summary>
        public GridCoordinate CurrentCoordinate { get; private set; }
        public ShapeFootprint Footprint { get; }
        public ColorIdentity ColorIdentity { get; }
        public HoleLifecycleState LifecycleState { get; private set; }
        public int FillCount { get; private set; }
        public int ReservedCollectionCount { get; private set; }
        public int Capacity => Footprint.CellCount;
        public bool IsDraggable => LifecycleState == HoleLifecycleState.Active;
        public bool HasUnreservedCapacity =>
            LifecycleState == HoleLifecycleState.Active &&
            FillCount + ReservedCollectionCount < Capacity;

        /// <summary>
        /// Updates the committed hole board coordinate after a later game-module-approved
        /// board-state commit.
        /// This should not be used for every drag sample because visual/freeform drag state
        /// is intentionally tracked outside this runtime state object.
        /// </summary>
        public void MoveTo(GridCoordinate coordinate)
        {
            CurrentCoordinate = coordinate;
        }

        /// <summary>
        /// Resolves the current footprint coordinates from the committed origin cell.
        /// </summary>
        public IReadOnlyList<GridCoordinate> ResolveFootprintCoordinates()
        {
            return Footprint.ResolveCoordinates(CurrentCoordinate);
        }

        /// <summary>
        /// Reserves one capacity slot after a matching drag-time target is accepted.
        /// Reservation prevents over-collection but does not fill or complete the hole.
        /// </summary>
        public bool TryReserveCollectible()
        {
            if (!HasUnreservedCapacity)
            {
                return false;
            }

            ReservedCollectionCount++;
            return true;
        }

        /// <summary>
        /// Rolls back a capacity reservation if the paired stickman reservation cannot be applied.
        /// </summary>
        public bool TryCancelReservedCollectible()
        {
            if (LifecycleState != HoleLifecycleState.Active || ReservedCollectionCount <= 0)
            {
                return false;
            }

            ReservedCollectionCount--;
            return true;
        }

        /// <summary>
        /// Converts one reserved slot into fill after collection presentation completes.
        /// The hole becomes Full only when completed presentation has filled every slot.
        /// </summary>
        public bool TryCompleteReservedCollectible()
        {
            if (LifecycleState != HoleLifecycleState.Active || ReservedCollectionCount <= 0)
            {
                return false;
            }

            ReservedCollectionCount--;
            FillCount++;
            if (FillCount >= Capacity)
            {
                LifecycleState = HoleLifecycleState.Full;
            }

            return true;
        }

        /// <summary>
        /// Moves a full hole into its completion sequence once presentation begins.
        /// </summary>
        public void BeginClosing()
        {
            if (LifecycleState != HoleLifecycleState.Full)
            {
                throw new InvalidOperationException(
                    $"Hole '{Id}' can only begin closing from the Full state.");
            }

            LifecycleState = HoleLifecycleState.Closing;
        }

        /// <summary>
        /// Marks the hole as fully completed and removed from active gameplay.
        /// </summary>
        public void MarkCompleted()
        {
            if (LifecycleState != HoleLifecycleState.Closing &&
                LifecycleState != HoleLifecycleState.Full)
            {
                throw new InvalidOperationException(
                    $"Hole '{Id}' can only complete after it is full.");
            }

            LifecycleState = HoleLifecycleState.Completed;
        }
    }

    /// <summary>
    /// Prototype-owned runtime state for a collectible stickman.
    /// Stickmen are tracked separately from structural occupancy blocking.
    /// A stickman remains visually present while Reserved, but gameplay truth no longer treats it
    /// as an active blocking or collectible target after reservation.
    /// </summary>
    public sealed class StickmanRuntimeState
    {
        public StickmanRuntimeState(
            string id,
            GridCoordinate coordinate,
            ColorIdentity colorIdentity)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Stickman runtime state id is required.", nameof(id));
            }

            if (colorIdentity == ColorIdentity.None)
            {
                throw new ArgumentException(
                    "Stickman runtime state requires a color identity.",
                    nameof(colorIdentity));
            }

            Id = id;
            Coordinate = coordinate;
            ColorIdentity = colorIdentity;
        }

        public string Id { get; }
        public GridCoordinate Coordinate { get; }
        public ColorIdentity ColorIdentity { get; }
        public StickmanLifecycleState LifecycleState { get; private set; }
        public string ReservedHoleId { get; private set; } = string.Empty;
        public bool IsCollected => LifecycleState == StickmanLifecycleState.Collected;

        /// <summary>
        /// Assigns the stickman to one hole after matching drag-time overlap is accepted.
        /// </summary>
        public void ReserveFor(string holeId)
        {
            if (string.IsNullOrWhiteSpace(holeId))
            {
                throw new ArgumentException("Reserved hole id is required.", nameof(holeId));
            }

            if (LifecycleState != StickmanLifecycleState.Available)
            {
                throw new InvalidOperationException(
                    $"Stickman '{Id}' can only be reserved from the Available state.");
            }

            ReservedHoleId = holeId;
            LifecycleState = StickmanLifecycleState.Reserved;
        }

        /// <summary>
        /// Starts collection presentation after the assigned hole reaches the trigger threshold.
        /// </summary>
        public void BeginCollection()
        {
            if (LifecycleState != StickmanLifecycleState.Reserved)
            {
                throw new InvalidOperationException(
                    $"Stickman '{Id}' can only begin collection from the Reserved state.");
            }

            LifecycleState = StickmanLifecycleState.Collecting;
        }

        /// <summary>
        /// Marks the stickman as fully collected after presentation or cleanup completes.
        /// </summary>
        public void MarkCollected()
        {
            if (LifecycleState == StickmanLifecycleState.Collected)
            {
                return;
            }

            if (LifecycleState != StickmanLifecycleState.Collecting)
            {
                throw new InvalidOperationException(
                    $"Stickman '{Id}' can only complete collection from the Collecting state.");
            }

            LifecycleState = StickmanLifecycleState.Collected;
        }
    }
}
