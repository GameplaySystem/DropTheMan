using System.Collections.Generic;
using PuzzleFramework.Content;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.RuntimeConstruction;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned builder that turns validated framework context plus parsed game payload
    /// into puzzle-specific runtime state.
    /// This slice stops before scene-object creation and gameplay rule orchestration.
    /// </summary>
    public sealed class DropTheManRuntimeModelBuilder
    {
        private readonly DropTheManContentPayloadParser _payloadParser = new();

        /// <summary>
        /// Builds the prototype runtime model from a framework-loaded level definition and runtime context.
        /// </summary>
        public DropTheManRuntimeModelBuildResult Build(
            LevelDefinition levelDefinition,
            RuntimeLevelContext frameworkContext)
        {
            if (frameworkContext == null)
            {
                return DropTheManRuntimeModelBuildResult.Failed(
                    "Framework runtime context is required.");
            }

            DropTheManPayloadParseResult payloadResult = _payloadParser.Parse(levelDefinition);
            if (!payloadResult.Success || payloadResult.Payload == null)
            {
                return DropTheManRuntimeModelBuildResult.Failed(payloadResult.FailureReason);
            }

            if (!TryValidatePrototypePlacement(payloadResult.Payload, frameworkContext, out string failureReason))
            {
                return DropTheManRuntimeModelBuildResult.Failed(failureReason);
            }

            if (!TryBuildHoles(
                    payloadResult.Payload.Holes,
                    frameworkContext,
                    out List<HoleRuntimeState> holes,
                    out string holeFailureReason))
            {
                return DropTheManRuntimeModelBuildResult.Failed(holeFailureReason);
            }

            List<StickmanRuntimeState> stickmen = BuildStickmen(payloadResult.Payload.Stickmen);
            StickmanCoordinateIndex stickmanIndex = new(stickmen);

            DropTheManRuntimeModel runtimeModel = new(
                frameworkContext,
                holes,
                stickmen,
                stickmanIndex);

            return DropTheManRuntimeModelBuildResult.Successful(runtimeModel);
        }

        private static bool TryValidatePrototypePlacement(
            DropTheManLevelContentPayload payload,
            RuntimeLevelContext frameworkContext,
            out string failureReason)
        {
            HashSet<GridCoordinate> holeCoordinates = new();
            HashSet<GridCoordinate> stickmanCoordinates = new();

            for (int i = 0; i < payload.Holes.Count; i++)
            {
                GridCoordinate coordinate = payload.Holes[i].ToGridCoordinate();
                if (!TryValidateStructuralCoordinate(
                        coordinate,
                        frameworkContext.GridBoard,
                        out failureReason))
                {
                    failureReason = $"Hole '{payload.Holes[i].Id}' is invalid: {failureReason}";
                    return false;
                }

                if (frameworkContext.CellOccupancySystem.IsInUse(coordinate))
                {
                    failureReason =
                        $"Hole '{payload.Holes[i].Id}' starts on occupied or reserved coordinate {coordinate}.";
                    return false;
                }

                holeCoordinates.Add(coordinate);
            }

            for (int i = 0; i < payload.Stickmen.Count; i++)
            {
                GridCoordinate coordinate = payload.Stickmen[i].ToGridCoordinate();
                if (!TryValidateStructuralCoordinate(
                        coordinate,
                        frameworkContext.GridBoard,
                        out failureReason))
                {
                    failureReason =
                        $"Stickman '{payload.Stickmen[i].Id}' is invalid: {failureReason}";
                    return false;
                }

                if (!stickmanCoordinates.Add(coordinate))
                {
                    failureReason =
                        $"Stickman '{payload.Stickmen[i].Id}' duplicates coordinate {coordinate}.";
                    return false;
                }

                // The first slice does not support unresolved start-on-target states.
                if (holeCoordinates.Contains(coordinate))
                {
                    failureReason =
                        $"Stickman '{payload.Stickmen[i].Id}' starts on hole coordinate {coordinate}.";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryValidateStructuralCoordinate(
            GridCoordinate coordinate,
            GridBoard gridBoard,
            out string failureReason)
        {
            if (!gridBoard.IsWithinBounds(coordinate))
            {
                failureReason = $"Coordinate {coordinate} is outside the board bounds.";
                return false;
            }

            if (!gridBoard.ContainsCell(coordinate))
            {
                failureReason = $"Coordinate {coordinate} is not a registered structural cell.";
                return false;
            }

            if (gridBoard.IsBlocked(coordinate))
            {
                failureReason = $"Coordinate {coordinate} is structurally blocked.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryBuildHoles(
            IReadOnlyList<DropTheManHoleDefinition> holeDefinitions,
            RuntimeLevelContext frameworkContext,
            out List<HoleRuntimeState> holes,
            out string failureReason)
        {
            holes = new List<HoleRuntimeState>(holeDefinitions.Count);

            for (int i = 0; i < holeDefinitions.Count; i++)
            {
                GridCoordinate coordinate = holeDefinitions[i].ToGridCoordinate();
                CellOccupancyOperationResult occupancyResult =
                    frameworkContext.CellOccupancySystem.Occupy(coordinate);
                if (!occupancyResult.Success)
                {
                    failureReason =
                        $"Failed to occupy start coordinate for hole '{holeDefinitions[i].Id}': {occupancyResult.FailureReason}";
                    return false;
                }

                holes.Add(
                    new HoleRuntimeState(
                        holeDefinitions[i].Id,
                        coordinate,
                        holeDefinitions[i].ColorIdentity));
            }

            failureReason = string.Empty;
            return true;
        }

        private static List<StickmanRuntimeState> BuildStickmen(
            IReadOnlyList<DropTheManStickmanDefinition> stickmanDefinitions)
        {
            List<StickmanRuntimeState> stickmen = new(stickmanDefinitions.Count);

            for (int i = 0; i < stickmanDefinitions.Count; i++)
            {
                stickmen.Add(
                    new StickmanRuntimeState(
                        stickmanDefinitions[i].Id,
                        stickmanDefinitions[i].ToGridCoordinate(),
                        stickmanDefinitions[i].ColorIdentity));
            }

            return stickmen;
        }
    }

    /// <summary>
    /// Result for prototype runtime-model building where failure is part of normal setup flow.
    /// </summary>
    public readonly struct DropTheManRuntimeModelBuildResult
    {
        public DropTheManRuntimeModelBuildResult(
            bool success,
            DropTheManRuntimeModel runtimeModel,
            string failureReason)
        {
            Success = success;
            RuntimeModel = runtimeModel;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public DropTheManRuntimeModel RuntimeModel { get; }
        public string FailureReason { get; }

        public static DropTheManRuntimeModelBuildResult Successful(
            DropTheManRuntimeModel runtimeModel)
        {
            return new DropTheManRuntimeModelBuildResult(true, runtimeModel, string.Empty);
        }

        public static DropTheManRuntimeModelBuildResult Failed(string failureReason)
        {
            return new DropTheManRuntimeModelBuildResult(false, null, failureReason);
        }
    }
}
