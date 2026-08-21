using System;
using System.Collections.Generic;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.RuntimeConstruction;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Maps Drop The Man's blocked-cell meaning into the framework's explicit visual mask.
    /// </summary>
    internal static class DropTheManBoardVisualParticipationMapper
    {
        public static List<GridCoordinate> CreateFromEditorData(
            int width,
            int height,
            IEnumerable<Vector2Int> blockedCoordinates)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            HashSet<GridCoordinate> blocked = new();
            if (blockedCoordinates != null)
            {
                foreach (Vector2Int coordinate in blockedCoordinates)
                {
                    blocked.Add(new GridCoordinate(coordinate.x, coordinate.y));
                }
            }

            List<GridCoordinate> participating = new(width * height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GridCoordinate coordinate = new(x, y);
                    if (!blocked.Contains(coordinate))
                    {
                        participating.Add(coordinate);
                    }
                }
            }

            return participating;
        }

        public static List<GridCoordinate> CreateFromRuntimeData(
            RuntimeConstructionBoardData boardData)
        {
            if (boardData == null)
            {
                throw new ArgumentNullException(nameof(boardData));
            }

            HashSet<GridCoordinate> blocked = new(boardData.BlockedCoordinates);
            List<GridCoordinate> participating = new(boardData.StructuralCoordinates.Count);
            for (int i = 0; i < boardData.StructuralCoordinates.Count; i++)
            {
                GridCoordinate coordinate = boardData.StructuralCoordinates[i];
                if (!blocked.Contains(coordinate))
                {
                    participating.Add(coordinate);
                }
            }

            return participating;
        }
    }
}
