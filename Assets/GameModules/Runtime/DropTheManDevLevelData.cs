using System;
using System.Collections.Generic;
using PuzzleFramework.Presentation;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Dev-only authored data for the first ugly Drop The Man test scene.
    /// This is not production content schema; it only feeds the dev bootstrapper.
    /// </summary>
    [Serializable]
    public sealed class DropTheManDevLevelData
    {
        public string LevelId = "Level 1";
        public string DisplayName = "Level 1";
        public int BoardWidth = 5;
        public int BoardHeight = 5;
        public List<Vector2Int> BlockedCells = new();
        public bool TimerEnabled;
        public float TimerDurationSeconds = 60f;
        public float TimerWarningThresholdSeconds;
        public List<DropTheManDevHoleData> Holes = new();
        public List<DropTheManDevStickmanData> Stickmen = new();

        public static DropTheManDevLevelData CreateDefault()
        {
            DropTheManDevLevelData data = new();

            data.Holes.Add(
                new DropTheManDevHoleData
                {
                    Id = "hole_red_01",
                    Coordinate = new Vector2Int(1, 2),
                    ColorIdentity = ColorIdentity.Red,
                    FootprintOffsets = new List<Vector2Int> { Vector2Int.zero }
                });

            data.Stickmen.Add(
                new DropTheManDevStickmanData
                {
                    Id = "stickman_red_01",
                    Coordinate = new Vector2Int(3, 2),
                    ColorIdentity = ColorIdentity.Red
                });

            return data;
        }
    }

    /// <summary>
    /// Dev-only hole definition that maps to the existing Drop The Man payload model.
    /// </summary>
    [Serializable]
    public sealed class DropTheManDevHoleData
    {
        public string Id = string.Empty;
        public Vector2Int Coordinate;
        public ColorIdentity ColorIdentity = ColorIdentity.Red;
        public List<Vector2Int> FootprintOffsets = new() { Vector2Int.zero };
    }

    /// <summary>
    /// Dev-only stickman definition that maps to the existing Drop The Man payload model.
    /// </summary>
    [Serializable]
    public sealed class DropTheManDevStickmanData
    {
        public string Id = string.Empty;
        public Vector2Int Coordinate;
        public ColorIdentity ColorIdentity = ColorIdentity.Red;
    }
}
