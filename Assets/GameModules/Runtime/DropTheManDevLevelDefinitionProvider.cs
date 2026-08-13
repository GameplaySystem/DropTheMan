using System.Collections.Generic;
using PuzzleFramework.Content;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Dev-only level provider for manual scene wiring.
    /// It creates a normal framework LevelDefinition with an opaque Drop The Man payload.
    /// </summary>
    public sealed class DropTheManDevLevelDefinitionProvider : IDropTheManLevelDefinitionProvider
    {
        private readonly DropTheManDevLevelData _levelData;

        public DropTheManDevLevelDefinitionProvider(DropTheManDevLevelData levelData)
        {
            _levelData = levelData ?? DropTheManDevLevelData.CreateDefault();
        }

        public bool TryGetLevelDefinition(
            out LevelDefinition levelDefinition,
            out string failureReason)
        {
            levelDefinition = null;

            if (!TryValidate(_levelData, out failureReason))
            {
                return false;
            }

            DropTheManLevelContentPayload payload = BuildPayload(_levelData);

            levelDefinition = new LevelDefinition
            {
                Metadata =
                {
                    LevelId = _levelData.LevelId,
                    DisplayName = _levelData.DisplayName,
                    Version = 1
                },
                FrameworkData =
                {
                    Board = BuildBoard(_levelData),
                    Timer =
                    {
                        IsEnabled = _levelData.TimerEnabled,
                        Mode = TimerMode.Countdown,
                        DurationSeconds = _levelData.TimerDurationSeconds,
                        WarningThresholdSeconds = _levelData.TimerWarningThresholdSeconds
                    }
                },
                ContentPayload =
                {
                    ContentTypeId = DropTheManLevelContentPayload.ExpectedContentTypeId,
                    PayloadJson = JsonUtility.ToJson(payload)
                }
            };

            failureReason = string.Empty;
            return true;
        }

        private static bool TryValidate(
            DropTheManDevLevelData levelData,
            out string failureReason)
        {
            if (levelData.BoardWidth <= 0 || levelData.BoardHeight <= 0)
            {
                failureReason = "Dev board dimensions must be positive.";
                return false;
            }

            if (levelData.Holes == null || levelData.Holes.Count == 0)
            {
                failureReason = "At least one dev hole is required.";
                return false;
            }

            if (levelData.Stickmen == null || levelData.Stickmen.Count == 0)
            {
                failureReason = "At least one dev stickman is required.";
                return false;
            }

            if (levelData.TimerEnabled && levelData.TimerDurationSeconds <= 0f)
            {
                failureReason = "Enabled dev timer requires a positive duration.";
                return false;
            }

            if (!TryValidateBlockedCells(levelData, out failureReason))
            {
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static BoardDefinitionData BuildBoard(DropTheManDevLevelData levelData)
        {
            BoardDefinitionData board = new()
            {
                Width = levelData.BoardWidth,
                Height = levelData.BoardHeight
            };

            HashSet<Vector2Int> blockedCells = levelData.BlockedCells != null
                ? new HashSet<Vector2Int>(levelData.BlockedCells)
                : null;

            for (int y = 0; y < levelData.BoardHeight; y++)
            {
                for (int x = 0; x < levelData.BoardWidth; x++)
                {
                    board.Cells.Add(
                        new CellDefinitionData
                        {
                            Coordinate = new CellCoordinateData { X = x, Y = y },
                            CellState = blockedCells != null && blockedCells.Contains(new Vector2Int(x, y))
                                ? AuthoredCellState.Blocked
                                : AuthoredCellState.Active
                        });
                }
            }

            return board;
        }

        private static bool TryValidateBlockedCells(
            DropTheManDevLevelData levelData,
            out string failureReason)
        {
            if (levelData.BlockedCells == null)
            {
                failureReason = string.Empty;
                return true;
            }

            HashSet<Vector2Int> uniqueBlockedCells = new();
            for (int i = 0; i < levelData.BlockedCells.Count; i++)
            {
                Vector2Int coordinate = levelData.BlockedCells[i];
                if (coordinate.x < 0 || coordinate.x >= levelData.BoardWidth ||
                    coordinate.y < 0 || coordinate.y >= levelData.BoardHeight)
                {
                    failureReason =
                        $"Blocked cell at index {i} is outside the declared board dimensions.";
                    return false;
                }

                if (!uniqueBlockedCells.Add(coordinate))
                {
                    failureReason =
                        $"Blocked cells contain a duplicate authored coordinate at ({coordinate.x}, {coordinate.y}).";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private static DropTheManLevelContentPayload BuildPayload(
            DropTheManDevLevelData levelData)
        {
            DropTheManLevelContentPayload payload = new();

            for (int i = 0; i < levelData.Holes.Count; i++)
            {
                DropTheManDevHoleData hole = levelData.Holes[i];
                DropTheManHoleDefinition holeDefinition = new()
                {
                    Id = hole.Id,
                    Coordinate =
                        new CellCoordinateData
                        {
                            X = hole.Coordinate.x,
                            Y = hole.Coordinate.y
                        },
                    ColorIdentity = hole.ColorIdentity
                };

                IReadOnlyList<Vector2Int> offsets =
                    hole.FootprintOffsets == null || hole.FootprintOffsets.Count == 0
                        ? new[] { Vector2Int.zero }
                        : hole.FootprintOffsets;
                for (int offsetIndex = 0; offsetIndex < offsets.Count; offsetIndex++)
                {
                    holeDefinition.FootprintOffsets.Add(
                        new CellCoordinateData
                        {
                            X = offsets[offsetIndex].x,
                            Y = offsets[offsetIndex].y
                        });
                }

                payload.Holes.Add(holeDefinition);
            }

            for (int i = 0; i < levelData.Stickmen.Count; i++)
            {
                DropTheManDevStickmanData stickman = levelData.Stickmen[i];
                payload.Stickmen.Add(
                    new DropTheManStickmanDefinition
                    {
                        Id = stickman.Id,
                        Coordinate =
                            new CellCoordinateData
                            {
                                X = stickman.Coordinate.x,
                                Y = stickman.Coordinate.y
                            },
                        ColorIdentity = stickman.ColorIdentity
                    });
            }

            return payload;
        }
    }
}
