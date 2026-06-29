using System;
using PuzzleFramework.CoreBoard;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned board-local continuous position expressed in cell-space units.
    /// This stays separate from framework grid coordinates because the movement helper
    /// works on freeform drag positions rather than snapped cell anchors.
    /// </summary>
    internal readonly struct BoardLocalContinuousPosition : IEquatable<BoardLocalContinuousPosition>
    {
        public BoardLocalContinuousPosition(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public static BoardLocalContinuousPosition FromWorld(
            Vector3 worldPosition,
            GridWorldLayout worldLayout)
        {
            Vector2 boardLocalPosition = worldLayout.WorldToBoardLocal(worldPosition);
            return new BoardLocalContinuousPosition(
                boardLocalPosition.x,
                boardLocalPosition.y);
        }

        public static BoardLocalContinuousPosition Lerp(
            BoardLocalContinuousPosition from,
            BoardLocalContinuousPosition to,
            float t)
        {
            return new BoardLocalContinuousPosition(
                Mathf.Lerp(from.X, to.X, t),
                Mathf.Lerp(from.Y, to.Y, t));
        }

        public Vector3 ToWorld(GridWorldLayout worldLayout)
        {
            return worldLayout.BoardLocalToWorld(new Vector2(X, Y));
        }

        public float DistanceTo(BoardLocalContinuousPosition other)
        {
            return Vector2.Distance(new Vector2(X, Y), new Vector2(other.X, other.Y));
        }

        public bool Equals(BoardLocalContinuousPosition other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is BoardLocalContinuousPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override string ToString()
        {
            return $"({X:0.###}, {Y:0.###})";
        }
    }
}
