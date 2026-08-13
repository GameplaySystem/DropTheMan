using System.Collections.Generic;
using PuzzleFramework.Content;
using PuzzleFramework.Presentation;
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
            if (levelData == null)
            {
                failureReason = "Drop The Man dev level data is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(levelData.LevelId))
            {
                failureReason = "Drop The Man dev level id is required.";
                return false;
            }

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

            if (levelData.TimerWarningThresholdSeconds < 0f)
            {
                failureReason = "Dev timer warning threshold cannot be negative.";
                return false;
            }

            if (!TryValidateBlockedCells(levelData, out HashSet<Vector2Int> blockedCells, out failureReason))
            {
                return false;
            }

            if (!TryValidateStickmen(
                    levelData,
                    blockedCells,
                    out HashSet<Vector2Int> stickmanCoordinates,
                    out failureReason))
            {
                return false;
            }

            if (!TryValidateHoles(
                    levelData,
                    blockedCells,
                    stickmanCoordinates,
                    out failureReason))
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
            out HashSet<Vector2Int> blockedCells,
            out string failureReason)
        {
            blockedCells = new HashSet<Vector2Int>();

            if (levelData.BlockedCells == null)
            {
                failureReason = string.Empty;
                return true;
            }

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

                if (!blockedCells.Add(coordinate))
                {
                    failureReason =
                        $"Blocked cells contain a duplicate authored coordinate at ({coordinate.x}, {coordinate.y}).";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryValidateStickmen(
            DropTheManDevLevelData levelData,
            HashSet<Vector2Int> blockedCells,
            out HashSet<Vector2Int> stickmanCoordinates,
            out string failureReason)
        {
            stickmanCoordinates = new HashSet<Vector2Int>();
            HashSet<string> uniqueStickmanIds = new();

            for (int i = 0; i < levelData.Stickmen.Count; i++)
            {
                DropTheManDevStickmanData stickman = levelData.Stickmen[i];
                if (stickman == null)
                {
                    failureReason = $"Dev stickman at index {i} is null.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(stickman.Id))
                {
                    failureReason = $"Dev stickman at index {i} must include an id.";
                    return false;
                }

                if (!uniqueStickmanIds.Add(stickman.Id))
                {
                    failureReason = $"Dev stickmen contain a duplicate id '{stickman.Id}'.";
                    return false;
                }

                if (stickman.ColorIdentity == ColorIdentity.None)
                {
                    failureReason = $"Dev stickman '{stickman.Id}' must use a supported color identity.";
                    return false;
                }

                if (!IsWithinBoard(stickman.Coordinate, levelData))
                {
                    failureReason =
                        $"Dev stickman '{stickman.Id}' is outside the declared board dimensions.";
                    return false;
                }

                if (blockedCells.Contains(stickman.Coordinate))
                {
                    failureReason =
                        $"Dev stickman '{stickman.Id}' is authored on blocked coordinate ({stickman.Coordinate.x}, {stickman.Coordinate.y}).";
                    return false;
                }

                if (!stickmanCoordinates.Add(stickman.Coordinate))
                {
                    failureReason =
                        $"Dev stickmen contain a duplicate coordinate at ({stickman.Coordinate.x}, {stickman.Coordinate.y}).";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryValidateHoles(
            DropTheManDevLevelData levelData,
            HashSet<Vector2Int> blockedCells,
            HashSet<Vector2Int> stickmanCoordinates,
            out string failureReason)
        {
            HashSet<string> uniqueHoleIds = new();
            Dictionary<Vector2Int, string> occupiedHoleCells = new();

            for (int i = 0; i < levelData.Holes.Count; i++)
            {
                DropTheManDevHoleData hole = levelData.Holes[i];
                if (hole == null)
                {
                    failureReason = $"Dev hole at index {i} is null.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(hole.Id))
                {
                    failureReason = $"Dev hole at index {i} must include an id.";
                    return false;
                }

                if (!uniqueHoleIds.Add(hole.Id))
                {
                    failureReason = $"Dev holes contain a duplicate id '{hole.Id}'.";
                    return false;
                }

                if (hole.ColorIdentity == ColorIdentity.None)
                {
                    failureReason = $"Dev hole '{hole.Id}' must use a supported color identity.";
                    return false;
                }

                IReadOnlyList<Vector2Int> footprintOffsets =
                    ResolveFootprintOffsets(hole.FootprintOffsets);
                for (int offsetIndex = 0; offsetIndex < footprintOffsets.Count; offsetIndex++)
                {
                    Vector2Int coordinate = hole.Coordinate + footprintOffsets[offsetIndex];
                    if (!IsWithinBoard(coordinate, levelData))
                    {
                        failureReason =
                            $"Dev hole '{hole.Id}' has footprint cell ({coordinate.x}, {coordinate.y}) outside the declared board dimensions.";
                        return false;
                    }

                    if (blockedCells.Contains(coordinate))
                    {
                        failureReason =
                            $"Dev hole '{hole.Id}' overlaps blocked coordinate ({coordinate.x}, {coordinate.y}).";
                        return false;
                    }

                    if (stickmanCoordinates.Contains(coordinate))
                    {
                        failureReason =
                            $"Dev hole '{hole.Id}' overlaps stickman coordinate ({coordinate.x}, {coordinate.y}).";
                        return false;
                    }

                    if (occupiedHoleCells.TryGetValue(coordinate, out string overlappingHoleId))
                    {
                        failureReason =
                            $"Dev hole '{hole.Id}' overlaps hole '{overlappingHoleId}' at coordinate ({coordinate.x}, {coordinate.y}).";
                        return false;
                    }

                    occupiedHoleCells.Add(coordinate, hole.Id);
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private static IReadOnlyList<Vector2Int> ResolveFootprintOffsets(
            IReadOnlyList<Vector2Int> sourceOffsets)
        {
            HashSet<Vector2Int> uniqueOffsets = new();
            List<Vector2Int> offsets = new();

            if (sourceOffsets != null)
            {
                for (int i = 0; i < sourceOffsets.Count; i++)
                {
                    if (uniqueOffsets.Add(sourceOffsets[i]))
                    {
                        offsets.Add(sourceOffsets[i]);
                    }
                }
            }

            if (!uniqueOffsets.Contains(Vector2Int.zero))
            {
                offsets.Insert(0, Vector2Int.zero);
            }

            if (offsets.Count == 0)
            {
                offsets.Add(Vector2Int.zero);
            }

            return offsets;
        }

        private static bool IsWithinBoard(
            Vector2Int coordinate,
            DropTheManDevLevelData levelData)
        {
            return coordinate.x >= 0 &&
                   coordinate.x < levelData.BoardWidth &&
                   coordinate.y >= 0 &&
                   coordinate.y < levelData.BoardHeight;
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
