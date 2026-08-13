using System;
using System.Collections.Generic;
using PuzzleFramework.Content;
using PuzzleFramework.Presentation;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned JSON level source for Drop The Man.
    /// It converts readable game-module JSON into the existing framework LevelDefinition path.
    /// </summary>
    public sealed class DropTheManJsonLevelDefinitionProvider : IDropTheManLevelDefinitionProvider
    {
        public const int SupportedFormatVersion = 1;

        private readonly string _jsonText;

        public DropTheManJsonLevelDefinitionProvider(TextAsset jsonAsset)
            : this(jsonAsset != null ? jsonAsset.text : null)
        {
        }

        public DropTheManJsonLevelDefinitionProvider(string jsonText)
        {
            _jsonText = jsonText;
        }

        public bool TryGetLevelDefinition(
            out LevelDefinition levelDefinition,
            out string failureReason)
        {
            levelDefinition = null;

            if (!TryParseJson(_jsonText, out DropTheManJsonLevelData jsonData, out failureReason))
            {
                return false;
            }

            if (!TryConvertToDevLevelData(jsonData, out DropTheManDevLevelData devLevelData, out failureReason))
            {
                return false;
            }

            DropTheManDevLevelDefinitionProvider provider = new(devLevelData);
            return provider.TryGetLevelDefinition(out levelDefinition, out failureReason);
        }

        public static bool TryExportDevLevelDataToJson(
            DropTheManDevLevelData levelData,
            out string jsonText,
            out string failureReason)
        {
            jsonText = string.Empty;

            if (levelData == null)
            {
                failureReason = "Drop The Man dev level data is required.";
                return false;
            }

            DropTheManDevLevelDefinitionProvider validationProvider = new(levelData);
            if (!validationProvider.TryGetLevelDefinition(out _, out failureReason))
            {
                return false;
            }

            DropTheManJsonLevelData jsonData = ConvertToJsonData(levelData);
            jsonText = JsonUtility.ToJson(jsonData, true);
            failureReason = string.Empty;
            return true;
        }

        public static bool TryImportJsonToDevLevelData(
            string jsonText,
            out DropTheManDevLevelData devLevelData,
            out string failureReason)
        {
            devLevelData = null;

            if (!TryParseJson(jsonText, out DropTheManJsonLevelData jsonData, out failureReason))
            {
                return false;
            }

            if (!TryConvertToDevLevelData(jsonData, out devLevelData, out failureReason))
            {
                return false;
            }

            DropTheManDevLevelDefinitionProvider validationProvider = new(devLevelData);
            if (!validationProvider.TryGetLevelDefinition(out _, out failureReason))
            {
                devLevelData = null;
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryParseJson(
            string jsonText,
            out DropTheManJsonLevelData jsonData,
            out string failureReason)
        {
            jsonData = null;

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                failureReason = "Drop The Man JSON level text is required.";
                return false;
            }

            try
            {
                jsonData = JsonUtility.FromJson<DropTheManJsonLevelData>(jsonText);
            }
            catch (Exception exception)
            {
                failureReason = $"Drop The Man JSON level could not be parsed: {exception.Message}";
                return false;
            }

            if (jsonData == null)
            {
                failureReason = "Drop The Man JSON level could not be parsed.";
                return false;
            }

            return TryValidateJsonData(jsonData, out failureReason);
        }

        private static bool TryValidateJsonData(
            DropTheManJsonLevelData jsonData,
            out string failureReason)
        {
            if (jsonData.FormatVersion != SupportedFormatVersion)
            {
                failureReason =
                    $"Drop The Man JSON format version {jsonData.FormatVersion} is not supported.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(jsonData.LevelId))
            {
                failureReason = "Drop The Man JSON level id is required.";
                return false;
            }

            if (jsonData.Board == null ||
                jsonData.Board.Width <= 0 ||
                jsonData.Board.Height <= 0)
            {
                failureReason = "Drop The Man JSON board dimensions must be positive.";
                return false;
            }

            if (!TryValidateBoardCoordinateList(
                    jsonData.Board.BlockedCells,
                    jsonData.Board.Width,
                    jsonData.Board.Height,
                    "blocked cell",
                    out failureReason))
            {
                return false;
            }

            if (jsonData.Timer != null &&
                jsonData.Timer.Enabled &&
                jsonData.Timer.DurationSeconds <= 0f)
            {
                failureReason = "Enabled Drop The Man JSON timer requires a positive duration.";
                return false;
            }

            if (jsonData.Holes == null || jsonData.Holes.Count == 0)
            {
                failureReason = "Drop The Man JSON level requires at least one hole.";
                return false;
            }

            if (jsonData.Stickmen == null || jsonData.Stickmen.Count == 0)
            {
                failureReason = "Drop The Man JSON level requires at least one stickman.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryConvertToDevLevelData(
            DropTheManJsonLevelData jsonData,
            out DropTheManDevLevelData devLevelData,
            out string failureReason)
        {
            devLevelData = new DropTheManDevLevelData
            {
                LevelId = jsonData.LevelId,
                DisplayName = string.IsNullOrWhiteSpace(jsonData.DisplayName)
                    ? jsonData.LevelId
                    : jsonData.DisplayName,
                BoardWidth = jsonData.Board.Width,
                BoardHeight = jsonData.Board.Height,
                TimerEnabled = jsonData.Timer != null && jsonData.Timer.Enabled,
                TimerDurationSeconds = jsonData.Timer != null
                    ? jsonData.Timer.DurationSeconds
                    : 0f,
                TimerWarningThresholdSeconds = jsonData.Timer != null
                    ? jsonData.Timer.WarningThresholdSeconds
                    : 0f
            };

            if (jsonData.Board.BlockedCells != null)
            {
                for (int i = 0; i < jsonData.Board.BlockedCells.Count; i++)
                {
                    DropTheManJsonCoordinateData blockedCell = jsonData.Board.BlockedCells[i];
                    devLevelData.BlockedCells.Add(new Vector2Int(blockedCell.X, blockedCell.Y));
                }
            }

            if (!TryConvertHoles(jsonData.Holes, devLevelData.Holes, out failureReason))
            {
                return false;
            }

            if (!TryConvertStickmen(jsonData.Stickmen, devLevelData.Stickmen, out failureReason))
            {
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryValidateBoardCoordinateList(
            IReadOnlyList<DropTheManJsonCoordinateData> coordinates,
            int boardWidth,
            int boardHeight,
            string label,
            out string failureReason)
        {
            if (coordinates == null)
            {
                failureReason = string.Empty;
                return true;
            }

            HashSet<string> uniqueCoordinates = new();
            for (int i = 0; i < coordinates.Count; i++)
            {
                DropTheManJsonCoordinateData coordinate = coordinates[i];
                if (coordinate == null)
                {
                    failureReason = $"Drop The Man JSON {label} at index {i} is null.";
                    return false;
                }

                if (coordinate.X < 0 || coordinate.X >= boardWidth ||
                    coordinate.Y < 0 || coordinate.Y >= boardHeight)
                {
                    failureReason =
                        $"Drop The Man JSON {label} at index {i} is outside the declared board dimensions.";
                    return false;
                }

                string coordinateKey = $"{coordinate.X},{coordinate.Y}";
                if (!uniqueCoordinates.Add(coordinateKey))
                {
                    failureReason =
                        $"Drop The Man JSON {label} list contains duplicate coordinate {coordinateKey}.";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryConvertHoles(
            IReadOnlyList<DropTheManJsonHoleData> jsonHoles,
            ICollection<DropTheManDevHoleData> devHoles,
            out string failureReason)
        {
            for (int i = 0; i < jsonHoles.Count; i++)
            {
                DropTheManJsonHoleData jsonHole = jsonHoles[i];
                if (jsonHole == null)
                {
                    failureReason = $"Drop The Man JSON hole at index {i} is null.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(jsonHole.Id))
                {
                    failureReason = $"Drop The Man JSON hole at index {i} must include an id.";
                    return false;
                }

                if (jsonHole.Coordinate == null)
                {
                    failureReason = $"Drop The Man JSON hole '{jsonHole.Id}' is missing its coordinate.";
                    return false;
                }

                if (!TryParseColor(jsonHole.Color, out ColorIdentity colorIdentity, out failureReason))
                {
                    failureReason = $"Drop The Man JSON hole '{jsonHole.Id}' is invalid: {failureReason}";
                    return false;
                }

                List<Vector2Int> footprintOffsets = new();
                if (jsonHole.FootprintOffsets == null || jsonHole.FootprintOffsets.Count == 0)
                {
                    footprintOffsets.Add(Vector2Int.zero);
                }
                else
                {
                    for (int offsetIndex = 0; offsetIndex < jsonHole.FootprintOffsets.Count; offsetIndex++)
                    {
                        DropTheManJsonCoordinateData offset = jsonHole.FootprintOffsets[offsetIndex];
                        if (offset == null)
                        {
                            failureReason =
                                $"Drop The Man JSON hole '{jsonHole.Id}' has a null footprint offset at index {offsetIndex}.";
                            return false;
                        }

                        footprintOffsets.Add(new Vector2Int(offset.X, offset.Y));
                    }
                }

                devHoles.Add(
                    new DropTheManDevHoleData
                    {
                        Id = jsonHole.Id,
                        Coordinate = new Vector2Int(jsonHole.Coordinate.X, jsonHole.Coordinate.Y),
                        ColorIdentity = colorIdentity,
                        FootprintOffsets = footprintOffsets
                    });
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryConvertStickmen(
            IReadOnlyList<DropTheManJsonStickmanData> jsonStickmen,
            ICollection<DropTheManDevStickmanData> devStickmen,
            out string failureReason)
        {
            for (int i = 0; i < jsonStickmen.Count; i++)
            {
                DropTheManJsonStickmanData jsonStickman = jsonStickmen[i];
                if (jsonStickman == null)
                {
                    failureReason = $"Drop The Man JSON stickman at index {i} is null.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(jsonStickman.Id))
                {
                    failureReason = $"Drop The Man JSON stickman at index {i} must include an id.";
                    return false;
                }

                if (jsonStickman.Coordinate == null)
                {
                    failureReason =
                        $"Drop The Man JSON stickman '{jsonStickman.Id}' is missing its coordinate.";
                    return false;
                }

                if (!TryParseColor(jsonStickman.Color, out ColorIdentity colorIdentity, out failureReason))
                {
                    failureReason =
                        $"Drop The Man JSON stickman '{jsonStickman.Id}' is invalid: {failureReason}";
                    return false;
                }

                devStickmen.Add(
                    new DropTheManDevStickmanData
                    {
                        Id = jsonStickman.Id,
                        Coordinate = new Vector2Int(
                            jsonStickman.Coordinate.X,
                            jsonStickman.Coordinate.Y),
                        ColorIdentity = colorIdentity
                    });
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryParseColor(
            string color,
            out ColorIdentity colorIdentity,
            out string failureReason)
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                colorIdentity = ColorIdentity.None;
                failureReason = "Color is required.";
                return false;
            }

            if (!Enum.TryParse(color, ignoreCase: true, out colorIdentity) ||
                colorIdentity == ColorIdentity.None)
            {
                failureReason = $"Color '{color}' is not a supported ColorIdentity.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static DropTheManJsonLevelData ConvertToJsonData(
            DropTheManDevLevelData levelData)
        {
            DropTheManJsonLevelData jsonData = new()
            {
                FormatVersion = SupportedFormatVersion,
                LevelId = levelData.LevelId,
                DisplayName = levelData.DisplayName,
                Board =
                {
                    Width = levelData.BoardWidth,
                    Height = levelData.BoardHeight
                },
                Timer =
                {
                    Enabled = levelData.TimerEnabled,
                    DurationSeconds = levelData.TimerDurationSeconds,
                    WarningThresholdSeconds = levelData.TimerWarningThresholdSeconds
                }
            };

            if (levelData.BlockedCells != null)
            {
                for (int i = 0; i < levelData.BlockedCells.Count; i++)
                {
                    Vector2Int blockedCell = levelData.BlockedCells[i];
                    jsonData.Board.BlockedCells.Add(
                        new DropTheManJsonCoordinateData
                        {
                            X = blockedCell.x,
                            Y = blockedCell.y
                        });
                }
            }

            for (int i = 0; i < levelData.Holes.Count; i++)
            {
                DropTheManDevHoleData hole = levelData.Holes[i];
                DropTheManJsonHoleData jsonHole = new()
                {
                    Id = hole.Id,
                    Color = hole.ColorIdentity.ToString(),
                    Coordinate =
                    {
                        X = hole.Coordinate.x,
                        Y = hole.Coordinate.y
                    }
                };

                IReadOnlyList<Vector2Int> offsets =
                    hole.FootprintOffsets == null || hole.FootprintOffsets.Count == 0
                        ? new[] { Vector2Int.zero }
                        : hole.FootprintOffsets;
                for (int offsetIndex = 0; offsetIndex < offsets.Count; offsetIndex++)
                {
                    jsonHole.FootprintOffsets.Add(
                        new DropTheManJsonCoordinateData
                        {
                            X = offsets[offsetIndex].x,
                            Y = offsets[offsetIndex].y
                        });
                }

                jsonData.Holes.Add(jsonHole);
            }

            for (int i = 0; i < levelData.Stickmen.Count; i++)
            {
                DropTheManDevStickmanData stickman = levelData.Stickmen[i];
                jsonData.Stickmen.Add(
                    new DropTheManJsonStickmanData
                    {
                        Id = stickman.Id,
                        Color = stickman.ColorIdentity.ToString(),
                        Coordinate =
                        {
                            X = stickman.Coordinate.x,
                            Y = stickman.Coordinate.y
                        }
                    });
            }

            return jsonData;
        }
    }
}
