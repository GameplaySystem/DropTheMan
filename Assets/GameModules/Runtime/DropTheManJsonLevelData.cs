using System;
using System.Collections.Generic;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned JSON authoring DTO for the first Drop The Man playable level pipeline.
    /// It is converted into the existing framework LevelDefinition plus opaque Drop The Man payload.
    /// </summary>
    [Serializable]
    public sealed class DropTheManJsonLevelData
    {
        public int FormatVersion = 1;
        public string LevelId = string.Empty;
        public string DisplayName = string.Empty;
        public DropTheManJsonBoardData Board = new();
        public DropTheManJsonTimerData Timer = new();
        public List<DropTheManJsonHoleData> Holes = new();
        public List<DropTheManJsonStickmanData> Stickmen = new();
    }

    [Serializable]
    public sealed class DropTheManJsonBoardData
    {
        public int Width;
        public int Height;
    }

    [Serializable]
    public sealed class DropTheManJsonTimerData
    {
        public bool Enabled;
        public float DurationSeconds = 60f;
        public float WarningThresholdSeconds;
    }

    [Serializable]
    public sealed class DropTheManJsonHoleData
    {
        public string Id = string.Empty;
        public string Color = string.Empty;
        public DropTheManJsonCoordinateData Coordinate = new();
        public List<DropTheManJsonCoordinateData> FootprintOffsets = new();
    }

    [Serializable]
    public sealed class DropTheManJsonStickmanData
    {
        public string Id = string.Empty;
        public string Color = string.Empty;
        public DropTheManJsonCoordinateData Coordinate = new();
    }

    [Serializable]
    public sealed class DropTheManJsonCoordinateData
    {
        public int X;
        public int Y;
    }
}
