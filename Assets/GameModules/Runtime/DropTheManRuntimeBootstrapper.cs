using System.Collections.Generic;
using PuzzleFramework.Content;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.RuntimeFlow;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Narrow dev-time level source seam for a future scene composition root.
    /// This slice does not implement level-loading UX or persistence behavior.
    /// </summary>
    public interface IDropTheManLevelDefinitionProvider
    {
        bool TryGetLevelDefinition(
            out LevelDefinition levelDefinition,
            out string failureReason);
    }

    /// <summary>
    /// Result for composing the first runtime integration controller.
    /// </summary>
    public readonly struct DropTheManRuntimeBootstrapResult
    {
        private DropTheManRuntimeBootstrapResult(
            bool success,
            DropTheManRuntimeController controller,
            DropTheManViewRegistry viewRegistry,
            string failureReason)
        {
            Success = success;
            Controller = controller;
            ViewRegistry = viewRegistry;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public DropTheManRuntimeController Controller { get; }
        public DropTheManViewRegistry ViewRegistry { get; }
        public string FailureReason { get; }

        public static DropTheManRuntimeBootstrapResult Successful(
            DropTheManRuntimeController controller,
            DropTheManViewRegistry viewRegistry)
        {
            return new DropTheManRuntimeBootstrapResult(
                true,
                controller,
                viewRegistry,
                string.Empty);
        }

        public static DropTheManRuntimeBootstrapResult Failed(string failureReason)
        {
            return new DropTheManRuntimeBootstrapResult(
                false,
                null,
                null,
                failureReason);
        }
    }

    /// <summary>
    /// Prototype-owned composition helper for wiring a runtime model to pre-placed view adapters.
    /// It does not build levels, spawn prefabs, search scenes, or own gameplay rules.
    /// </summary>
    public sealed class DropTheManRuntimeBootstrapper
    {
        public DropTheManRuntimeBootstrapResult CreateController(
            DropTheManRuntimeModel runtimeModel,
            GridWorldLayout worldLayout,
            IEnumerable<IDropTheManHoleView> holeViews,
            IEnumerable<IDropTheManStickmanView> stickmanViews,
            TimerSystem timerSystem = null,
            float collectionTriggerRadiusInCells = 0.35f,
            float dragClearanceInsetCells = 0.08f,
            bool snapFullHolesToNearestCellBeforeClosing = true)
        {
            if (runtimeModel == null)
            {
                return DropTheManRuntimeBootstrapResult.Failed("Runtime model is required.");
            }

            if (collectionTriggerRadiusInCells <= 0f)
            {
                return DropTheManRuntimeBootstrapResult.Failed(
                    "Collection trigger radius must be positive.");
            }

            if (dragClearanceInsetCells < 0f || dragClearanceInsetCells > 0.45f)
            {
                return DropTheManRuntimeBootstrapResult.Failed(
                    "Drag clearance inset must stay between 0 and 0.45 cells.");
            }

            DropTheManViewRegistry viewRegistry = new();
            DropTheManViewRegistryResult registerResult =
                RegisterHoleViews(viewRegistry, holeViews);
            if (!registerResult.Success)
            {
                return DropTheManRuntimeBootstrapResult.Failed(registerResult.FailureReason);
            }

            registerResult = RegisterStickmanViews(viewRegistry, stickmanViews);
            if (!registerResult.Success)
            {
                return DropTheManRuntimeBootstrapResult.Failed(registerResult.FailureReason);
            }

            DropTheManViewRegistryResult mappingResult =
                viewRegistry.ValidateRuntimeMappings(runtimeModel);
            if (!mappingResult.Success)
            {
                return DropTheManRuntimeBootstrapResult.Failed(mappingResult.FailureReason);
            }

            DropTheManRuntimeController controller = new(
                runtimeModel,
                worldLayout,
                viewRegistry,
                new GameStateSystem(),
                new DropTheManDragSessionOwner(),
                new DropTheManOutcomeRouter(),
                timerSystem,
                collectionTriggerRadiusInCells,
                dragClearanceInsetCells,
                snapFullHolesToNearestCellBeforeClosing);

            return DropTheManRuntimeBootstrapResult.Successful(
                controller,
                viewRegistry);
        }

        private static DropTheManViewRegistryResult RegisterHoleViews(
            DropTheManViewRegistry viewRegistry,
            IEnumerable<IDropTheManHoleView> holeViews)
        {
            if (holeViews == null)
            {
                return DropTheManViewRegistryResult.Failed("Hole views are required.");
            }

            foreach (IDropTheManHoleView holeView in holeViews)
            {
                DropTheManViewRegistryResult result =
                    viewRegistry.RegisterHoleView(holeView);
                if (!result.Success)
                {
                    return result;
                }
            }

            return DropTheManViewRegistryResult.Successful();
        }

        private static DropTheManViewRegistryResult RegisterStickmanViews(
            DropTheManViewRegistry viewRegistry,
            IEnumerable<IDropTheManStickmanView> stickmanViews)
        {
            if (stickmanViews == null)
            {
                return DropTheManViewRegistryResult.Failed("Stickman views are required.");
            }

            foreach (IDropTheManStickmanView stickmanView in stickmanViews)
            {
                DropTheManViewRegistryResult result =
                    viewRegistry.RegisterStickmanView(stickmanView);
                if (!result.Success)
                {
                    return result;
                }
            }

            return DropTheManViewRegistryResult.Successful();
        }
    }
}
