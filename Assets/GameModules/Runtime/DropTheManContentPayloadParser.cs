using System.Collections.Generic;
using PuzzleFramework.Content;
using PuzzleFramework.Presentation;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Parses and validates the `DropTheMan` game-module payload from a framework-loaded level definition.
    /// This owns payload-shape validation only and does not perform runtime construction or gameplay checks.
    /// </summary>
    public sealed class DropTheManContentPayloadParser
    {
        /// <summary>
        /// Parses the opaque framework content payload into prototype-owned authored data.
        /// </summary>
        public DropTheManPayloadParseResult Parse(LevelDefinition levelDefinition)
        {
            if (levelDefinition == null)
            {
                return DropTheManPayloadParseResult.Failed("Level definition is required.");
            }

            if (levelDefinition.ContentPayload == null)
            {
                return DropTheManPayloadParseResult.Failed("Content payload is required.");
            }

            if (levelDefinition.ContentPayload.ContentTypeId !=
                DropTheManLevelContentPayload.ExpectedContentTypeId)
            {
                return DropTheManPayloadParseResult.Failed(
                    $"Content payload type id must be '{DropTheManLevelContentPayload.ExpectedContentTypeId}'.");
            }

            if (string.IsNullOrWhiteSpace(levelDefinition.ContentPayload.PayloadJson))
            {
                return DropTheManPayloadParseResult.Failed(
                    "Drop The Man content payload JSON is required.");
            }

            DropTheManLevelContentPayload payload =
                JsonUtility.FromJson<DropTheManLevelContentPayload>(
                    levelDefinition.ContentPayload.PayloadJson);

            if (payload == null)
            {
                return DropTheManPayloadParseResult.Failed(
                    "Drop The Man content payload JSON could not be parsed.");
            }

            if (!TryValidatePayload(payload, out string failureReason))
            {
                return DropTheManPayloadParseResult.Failed(failureReason);
            }

            return DropTheManPayloadParseResult.Successful(payload);
        }

        private static bool TryValidatePayload(
            DropTheManLevelContentPayload payload,
            out string failureReason)
        {
            if (payload.Holes == null)
            {
                failureReason = "Drop The Man holes collection is required.";
                return false;
            }

            if (payload.Stickmen == null)
            {
                failureReason = "Drop The Man stickmen collection is required.";
                return false;
            }

            if (!TryValidateHoles(payload.Holes, out failureReason))
            {
                return false;
            }

            if (!TryValidateStickmen(payload.Stickmen, out failureReason))
            {
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryValidateHoles(
            IReadOnlyList<DropTheManHoleDefinition> holes,
            out string failureReason)
        {
            HashSet<string> ids = new();
            HashSet<string> coordinates = new();

            for (int i = 0; i < holes.Count; i++)
            {
                DropTheManHoleDefinition hole = holes[i];
                if (hole == null)
                {
                    failureReason = $"Hole definition at index {i} is null.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(hole.Id))
                {
                    failureReason = $"Hole definition at index {i} must include an id.";
                    return false;
                }

                if (!ids.Add(hole.Id))
                {
                    failureReason = $"Hole definitions contain duplicate id '{hole.Id}'.";
                    return false;
                }

                if (hole.Coordinate == null)
                {
                    failureReason = $"Hole definition '{hole.Id}' is missing its coordinate.";
                    return false;
                }

                if (hole.ColorIdentity == ColorIdentity.None)
                {
                    failureReason = $"Hole definition '{hole.Id}' must include a color identity.";
                    return false;
                }

                if (!TryValidateFootprintOffsets(hole, out failureReason))
                {
                    return false;
                }

                string coordinateKey = $"{hole.Coordinate.X},{hole.Coordinate.Y}";
                if (!coordinates.Add(coordinateKey))
                {
                    failureReason =
                        $"Hole definitions contain duplicate coordinate {coordinateKey}.";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryValidateFootprintOffsets(
            DropTheManHoleDefinition hole,
            out string failureReason)
        {
            if (hole.FootprintOffsets == null || hole.FootprintOffsets.Count == 0)
            {
                failureReason = string.Empty;
                return true;
            }

            bool containsOriginOffset = false;
            HashSet<string> offsets = new();

            for (int i = 0; i < hole.FootprintOffsets.Count; i++)
            {
                CellCoordinateData offset = hole.FootprintOffsets[i];
                if (offset == null)
                {
                    failureReason =
                        $"Hole definition '{hole.Id}' contains a null footprint offset at index {i}.";
                    return false;
                }

                string offsetKey = $"{offset.X},{offset.Y}";
                if (!offsets.Add(offsetKey))
                {
                    failureReason =
                        $"Hole definition '{hole.Id}' contains duplicate footprint offset {offsetKey}.";
                    return false;
                }

                if (offset.X == 0 && offset.Y == 0)
                {
                    containsOriginOffset = true;
                }
            }

            if (!containsOriginOffset)
            {
                failureReason =
                    $"Hole definition '{hole.Id}' footprint must include the origin offset 0,0.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryValidateStickmen(
            IReadOnlyList<DropTheManStickmanDefinition> stickmen,
            out string failureReason)
        {
            HashSet<string> ids = new();
            HashSet<string> coordinates = new();

            for (int i = 0; i < stickmen.Count; i++)
            {
                DropTheManStickmanDefinition stickman = stickmen[i];
                if (stickman == null)
                {
                    failureReason = $"Stickman definition at index {i} is null.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(stickman.Id))
                {
                    failureReason = $"Stickman definition at index {i} must include an id.";
                    return false;
                }

                if (!ids.Add(stickman.Id))
                {
                    failureReason = $"Stickman definitions contain duplicate id '{stickman.Id}'.";
                    return false;
                }

                if (stickman.Coordinate == null)
                {
                    failureReason =
                        $"Stickman definition '{stickman.Id}' is missing its coordinate.";
                    return false;
                }

                if (stickman.ColorIdentity == ColorIdentity.None)
                {
                    failureReason =
                        $"Stickman definition '{stickman.Id}' must include a color identity.";
                    return false;
                }

                string coordinateKey = $"{stickman.Coordinate.X},{stickman.Coordinate.Y}";
                if (!coordinates.Add(coordinateKey))
                {
                    failureReason =
                        $"Stickman definitions contain duplicate coordinate {coordinateKey}.";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Result for prototype payload parsing where failure is part of normal content-validation flow.
    /// </summary>
    public readonly struct DropTheManPayloadParseResult
    {
        public DropTheManPayloadParseResult(
            bool success,
            DropTheManLevelContentPayload payload,
            string failureReason)
        {
            Success = success;
            Payload = payload;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public DropTheManLevelContentPayload Payload { get; }
        public string FailureReason { get; }

        public static DropTheManPayloadParseResult Successful(
            DropTheManLevelContentPayload payload)
        {
            return new DropTheManPayloadParseResult(true, payload, string.Empty);
        }

        public static DropTheManPayloadParseResult Failed(string failureReason)
        {
            return new DropTheManPayloadParseResult(false, null, failureReason);
        }
    }
}
