using System;
using System.Collections.Generic;
using PuzzleFramework.CoreBoard;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Coordinate-based stickman lookup separate from structural occupancy blocking.
    /// This lets collection target queries coexist with framework-owned placement validation.
    /// This index represents only currently available/blocking collectibles.
    /// Stickmen that have entered Collecting are intentionally removed immediately even if their
    /// visuals remain present, so they no longer block movement or collect again.
    /// </summary>
    public sealed class StickmanCoordinateIndex
    {
        private readonly Dictionary<GridCoordinate, StickmanRuntimeState> _stickmenByCoordinate = new();

        public StickmanCoordinateIndex(IEnumerable<StickmanRuntimeState> stickmen)
        {
            if (stickmen == null)
            {
                throw new ArgumentNullException(nameof(stickmen));
            }

            foreach (StickmanRuntimeState stickman in stickmen)
            {
                if (stickman == null)
                {
                    throw new ArgumentException(
                        "Stickman coordinate index cannot contain null entries.",
                        nameof(stickmen));
                }

                if (!_stickmenByCoordinate.TryAdd(stickman.Coordinate, stickman))
                {
                    throw new ArgumentException(
                        $"Stickman coordinate index contains duplicate coordinate {stickman.Coordinate}.",
                        nameof(stickmen));
                }
            }
        }

        /// <summary>
        /// Number of currently indexed uncollected stickmen.
        /// </summary>
        public int Count => _stickmenByCoordinate.Count;

        /// <summary>
        /// Returns true when an uncollected stickman is currently indexed at the coordinate.
        /// </summary>
        public bool Contains(GridCoordinate coordinate)
        {
            return _stickmenByCoordinate.ContainsKey(coordinate);
        }

        /// <summary>
        /// Attempts to resolve the currently indexed stickman at the coordinate.
        /// </summary>
        public bool TryGet(GridCoordinate coordinate, out StickmanRuntimeState stickman)
        {
            return _stickmenByCoordinate.TryGetValue(coordinate, out stickman);
        }

        /// <summary>
        /// Removes a stickman from the active coordinate index after a higher-level rule owner
        /// has decided that a matching drag-time overlap should begin collecting it.
        /// This immediate removal is intentional because Collecting stickmen are non-blocking
        /// and should not be discovered through the active coordinate lookup anymore.
        /// </summary>
        public bool TryBeginCollection(GridCoordinate coordinate, out StickmanRuntimeState stickman)
        {
            if (!_stickmenByCoordinate.TryGetValue(coordinate, out stickman) ||
                !_stickmenByCoordinate.Remove(coordinate))
            {
                return false;
            }

            stickman.BeginCollection();
            return true;
        }
    }
}
