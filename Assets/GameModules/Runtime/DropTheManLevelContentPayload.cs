using System;
using System.Collections.Generic;
using PuzzleFramework.Content;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.Presentation;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned payload model transported through the framework's opaque content payload.
    /// This stays at game-module content ownership and does not alter framework schema.
    /// </summary>
    [Serializable]
    public sealed class DropTheManLevelContentPayload
    {
        public const string ExpectedContentTypeId = "drop-the-man";

        public List<DropTheManHoleDefinition> Holes = new();
        public List<DropTheManStickmanDefinition> Stickmen = new();
    }

    /// <summary>
    /// Authored hole entry for the first `DropTheMan` prototype slice.
    /// </summary>
    [Serializable]
    public sealed class DropTheManHoleDefinition
    {
        public string Id = string.Empty;
        public CellCoordinateData Coordinate = new();
        public List<CellCoordinateData> FootprintOffsets = new();
        public ColorIdentity ColorIdentity = ColorIdentity.None;

        /// <summary>
        /// Converts authored coordinate data into a runtime grid coordinate.
        /// </summary>
        public GridCoordinate ToGridCoordinate()
        {
            return new GridCoordinate(Coordinate.X, Coordinate.Y);
        }

        /// <summary>
        /// Converts authored footprint offsets into runtime grid offsets.
        /// Empty authored offsets fall back to a single-cell footprint for compatibility.
        /// </summary>
        public IReadOnlyList<GridCoordinate> ToFootprintOffsetsOrDefault()
        {
            if (FootprintOffsets == null || FootprintOffsets.Count == 0)
            {
                return DefaultSingleCellOffsets;
            }

            List<GridCoordinate> offsets = new(FootprintOffsets.Count);

            for (int i = 0; i < FootprintOffsets.Count; i++)
            {
                offsets.Add(new GridCoordinate(FootprintOffsets[i].X, FootprintOffsets[i].Y));
            }

            return offsets;
        }

        private static readonly GridCoordinate[] DefaultSingleCellOffsets =
        {
            new GridCoordinate(0, 0)
        };
    }

    /// <summary>
    /// Authored stickman entry for the first `DropTheMan` prototype slice.
    /// </summary>
    [Serializable]
    public sealed class DropTheManStickmanDefinition
    {
        public string Id = string.Empty;
        public CellCoordinateData Coordinate = new();
        public ColorIdentity ColorIdentity = ColorIdentity.None;

        /// <summary>
        /// Converts authored coordinate data into a runtime grid coordinate.
        /// </summary>
        public GridCoordinate ToGridCoordinate()
        {
            return new GridCoordinate(Coordinate.X, Coordinate.Y);
        }
    }
}
