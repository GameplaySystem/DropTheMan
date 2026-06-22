using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PuzzleFramework.RuntimeConstruction;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned runtime handoff model layered on top of the framework runtime context.
    /// This groups puzzle-specific runtime state without leaking puzzle meaning into the framework.
    /// </summary>
    public sealed class DropTheManRuntimeModel
    {
        public DropTheManRuntimeModel(
            RuntimeLevelContext frameworkContext,
            IList<HoleRuntimeState> holes,
            IList<StickmanRuntimeState> stickmen,
            StickmanCoordinateIndex stickmanIndex)
        {
            FrameworkContext = frameworkContext ?? throw new ArgumentNullException(nameof(frameworkContext));
            Holes = new ReadOnlyCollection<HoleRuntimeState>(
                new List<HoleRuntimeState>(holes ?? throw new ArgumentNullException(nameof(holes))));
            Stickmen = new ReadOnlyCollection<StickmanRuntimeState>(
                new List<StickmanRuntimeState>(stickmen ?? throw new ArgumentNullException(nameof(stickmen))));
            StickmanIndex = stickmanIndex ?? throw new ArgumentNullException(nameof(stickmanIndex));
        }

        /// <summary>
        /// Shared framework-owned runtime context for the level.
        /// </summary>
        public RuntimeLevelContext FrameworkContext { get; }

        /// <summary>
        /// Prototype-owned holes participating in the level.
        /// </summary>
        public IReadOnlyList<HoleRuntimeState> Holes { get; }

        /// <summary>
        /// Prototype-owned stickmen authored for the level.
        /// </summary>
        public IReadOnlyList<StickmanRuntimeState> Stickmen { get; }

        /// <summary>
        /// Active stickman lookup that stays separate from structural occupancy.
        /// </summary>
        public StickmanCoordinateIndex StickmanIndex { get; }
    }
}
