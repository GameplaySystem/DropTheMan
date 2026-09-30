using PuzzleFramework.CoreBoard;
using PuzzleFramework.Presentation;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Positions a perspective camera so the complete logical board rectangle fits while
    /// preserving the camera's authored rotation and lens settings.
    /// </summary>
    internal static class DropTheManBoardCameraPositioner
    {
        /// <summary>
        /// Moves <paramref name="targetCamera"/> along its current forward axis to frame the board.
        /// This is scene presentation only and does not change the supplied world layout.
        /// </summary>
        internal static bool TryPositionCamera(
            Camera targetCamera,
            GridWorldLayout worldLayout,
            int boardWidth,
            int boardHeight,
            float framingPadding,
            float minimumDistance,
            out string failureReason)
        {
            return PerspectiveBoardCameraPositioner.TryPosition(
                targetCamera,
                worldLayout,
                boardWidth,
                boardHeight,
                framingPadding,
                minimumDistance,
                out failureReason);
        }
    }
}
