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
        public ColorIdentity ColorIdentity = ColorIdentity.None;

        /// <summary>
        /// Converts authored coordinate data into a runtime grid coordinate.
        /// </summary>
        public GridCoordinate ToGridCoordinate()
        {
            return new GridCoordinate(Coordinate.X, Coordinate.Y);
        }
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
