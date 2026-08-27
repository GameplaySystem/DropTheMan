using System;
using System.Collections.Generic;
using PuzzleFramework.CoreBoard;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned mapping from authored hole footprints to their runtime view prefabs.
    /// Shape identity is derived from exact coordinate sets because level payloads intentionally
    /// carry structural footprints rather than presentation prefab identifiers.
    /// </summary>
    internal sealed class DropTheManHolePrefabResolver
    {
        private readonly IReadOnlyList<DropTheManHolePrefabDefinition> _definitions;

        private DropTheManHolePrefabResolver(
            IReadOnlyList<DropTheManHolePrefabDefinition> definitions)
        {
            _definitions = definitions;
        }

        public static bool TryCreate(
            IReadOnlyList<DropTheManHolePrefabDefinition> definitions,
            out DropTheManHolePrefabResolver resolver,
            out string failureReason)
        {
            resolver = null;

            if (definitions == null || definitions.Count == 0)
            {
                failureReason = "At least one hole prefab definition is required.";
                return false;
            }

            HashSet<string> ids = new(StringComparer.Ordinal);
            for (int i = 0; i < definitions.Count; i++)
            {
                DropTheManHolePrefabDefinition definition = definitions[i];
                if (string.IsNullOrWhiteSpace(definition.Id))
                {
                    failureReason = $"Hole prefab definition at index {i} requires an id.";
                    return false;
                }

                if (!ids.Add(definition.Id))
                {
                    failureReason =
                        $"Hole prefab definition id '{definition.Id}' is duplicated.";
                    return false;
                }

                if (definition.Prefab == null)
                {
                    failureReason =
                        $"Hole prefab definition '{definition.Id}' requires a prefab.";
                    return false;
                }

                if (!TryBuildCoordinateSet(
                        definition.FootprintOffsets,
                        out HashSet<GridCoordinate> definitionOffsets,
                        out string offsetFailureReason))
                {
                    failureReason =
                        $"Hole prefab definition '{definition.Id}' is invalid: " +
                        offsetFailureReason;
                    return false;
                }

                if (!definitionOffsets.Contains(new GridCoordinate(0, 0)))
                {
                    failureReason =
                        $"Hole prefab definition '{definition.Id}' must include origin offset (0, 0).";
                    return false;
                }
            }

            for (int leftIndex = 0; leftIndex < definitions.Count; leftIndex++)
            {
                for (int rightIndex = leftIndex + 1;
                     rightIndex < definitions.Count;
                     rightIndex++)
                {
                    DropTheManHolePrefabDefinition left = definitions[leftIndex];
                    DropTheManHolePrefabDefinition right = definitions[rightIndex];
                    if (AreRotationallyEquivalent(
                            left.FootprintOffsets,
                            right.FootprintOffsets))
                    {
                        failureReason =
                            $"Hole prefab definitions '{left.Id}' and '{right.Id}' describe " +
                            "the same shape under rotation. Runtime footprint mapping would be ambiguous.";
                        return false;
                    }
                }
            }

            resolver = new DropTheManHolePrefabResolver(definitions);
            failureReason = string.Empty;
            return true;
        }

        public bool TryResolve(
            IReadOnlyList<GridCoordinate> runtimeFootprintOffsets,
            out DropTheManHolePrefabResolution resolution,
            out string failureReason)
        {
            resolution = default;

            if (!TryBuildCoordinateSet(
                    runtimeFootprintOffsets,
                    out HashSet<GridCoordinate> runtimeOffsets,
                    out failureReason))
            {
                return false;
            }

            for (int definitionIndex = 0;
                 definitionIndex < _definitions.Count;
                 definitionIndex++)
            {
                DropTheManHolePrefabDefinition definition = _definitions[definitionIndex];
                for (int quarterTurns = 0; quarterTurns < 4; quarterTurns++)
                {
                    if (!MatchesRotatedOffsets(
                            definition.FootprintOffsets,
                            runtimeOffsets,
                            quarterTurns))
                    {
                        continue;
                    }

                    resolution = new DropTheManHolePrefabResolution(
                        definition.Prefab,
                        quarterTurns);
                    failureReason = string.Empty;
                    return true;
                }
            }

            failureReason =
                $"No configured hole prefab matches footprint " +
                $"{FormatCoordinates(runtimeFootprintOffsets)}.";
            return false;
        }

        private static bool AreRotationallyEquivalent(
            IReadOnlyList<GridCoordinate> left,
            IReadOnlyList<GridCoordinate> right)
        {
            if (!TryBuildCoordinateSet(right, out HashSet<GridCoordinate> rightSet, out _))
            {
                return false;
            }

            for (int quarterTurns = 0; quarterTurns < 4; quarterTurns++)
            {
                if (MatchesRotatedOffsets(left, rightSet, quarterTurns))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesRotatedOffsets(
            IReadOnlyList<GridCoordinate> sourceOffsets,
            HashSet<GridCoordinate> targetOffsets,
            int quarterTurns)
        {
            if (sourceOffsets.Count != targetOffsets.Count)
            {
                return false;
            }

            for (int i = 0; i < sourceOffsets.Count; i++)
            {
                GridCoordinate rotated = Rotate(sourceOffsets[i], quarterTurns);
                if (!targetOffsets.Contains(rotated))
                {
                    return false;
                }
            }

            return true;
        }

        private static GridCoordinate Rotate(GridCoordinate coordinate, int quarterTurns)
        {
            GridCoordinate rotated = coordinate;
            for (int turn = 0; turn < quarterTurns; turn++)
            {
                rotated = new GridCoordinate(rotated.Y, -rotated.X);
            }

            return rotated;
        }

        private static bool TryBuildCoordinateSet(
            IReadOnlyList<GridCoordinate> offsets,
            out HashSet<GridCoordinate> coordinateSet,
            out string failureReason)
        {
            coordinateSet = null;

            if (offsets == null || offsets.Count == 0)
            {
                failureReason = "A non-empty footprint is required.";
                return false;
            }

            HashSet<GridCoordinate> resolvedSet = new();
            for (int i = 0; i < offsets.Count; i++)
            {
                if (!resolvedSet.Add(offsets[i]))
                {
                    failureReason = $"Footprint offset {offsets[i]} is duplicated.";
                    return false;
                }
            }

            coordinateSet = resolvedSet;
            failureReason = string.Empty;
            return true;
        }

        private static string FormatCoordinates(IReadOnlyList<GridCoordinate> coordinates)
        {
            if (coordinates == null || coordinates.Count == 0)
            {
                return "[]";
            }

            string[] formatted = new string[coordinates.Count];
            for (int i = 0; i < coordinates.Count; i++)
            {
                formatted[i] = coordinates[i].ToString();
            }

            return $"[{string.Join(", ", formatted)}]";
        }
    }

    internal readonly struct DropTheManHolePrefabDefinition
    {
        public DropTheManHolePrefabDefinition(
            string id,
            DropTheManHoleView prefab,
            IReadOnlyList<GridCoordinate> footprintOffsets)
        {
            Id = id;
            Prefab = prefab;
            FootprintOffsets = footprintOffsets;
        }

        public string Id { get; }
        public DropTheManHoleView Prefab { get; }
        public IReadOnlyList<GridCoordinate> FootprintOffsets { get; }
    }

    internal readonly struct DropTheManHolePrefabResolution
    {
        public DropTheManHolePrefabResolution(
            DropTheManHoleView prefab,
            int quarterTurns)
        {
            Prefab = prefab;
            QuarterTurns = quarterTurns;
        }

        public DropTheManHoleView Prefab { get; }
        public int QuarterTurns { get; }
    }
}
