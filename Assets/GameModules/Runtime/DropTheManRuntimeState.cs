using System;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.Presentation;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned mutable runtime state for a hole.
    /// This carries puzzle-specific meaning and stays outside the framework.
    /// </summary>
    public sealed class HoleRuntimeState
    {
        public HoleRuntimeState(
            string id,
            GridCoordinate currentCoordinate,
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
            ColorIdentity = colorIdentity;
        }

        public string Id { get; }
        public GridCoordinate CurrentCoordinate { get; private set; }
        public ColorIdentity ColorIdentity { get; }

        /// <summary>
        /// Updates the authoritative hole grid coordinate after a game-module-approved
        /// coordinate change.
        /// </summary>
        public void MoveTo(GridCoordinate coordinate)
        {
            CurrentCoordinate = coordinate;
        }
    }

    /// <summary>
    /// Prototype-owned runtime state for a collectible stickman.
    /// Stickmen are tracked separately from structural occupancy blocking.
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
        public bool IsCollected { get; private set; }

        /// <summary>
        /// Marks the stickman as collected after game-module rules approve a matching overlap.
        /// </summary>
        public void MarkCollected()
        {
            IsCollected = true;
        }
    }
}
