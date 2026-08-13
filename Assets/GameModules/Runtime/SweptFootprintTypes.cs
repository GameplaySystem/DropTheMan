using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PuzzleFramework.CoreBoard;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned unit rectangle for one footprint cell in board-local cell space.
    /// Rectangles use half-open intervals during overlap evaluation.
    /// </summary>
    internal readonly struct FootprintCellRectangle
    {
        public FootprintCellRectangle(GridCoordinate offset)
            : this(
                offset,
                offset.X,
                offset.Y,
                offset.X + 1f,
                offset.Y + 1f)
        {
        }

        public FootprintCellRectangle(
            GridCoordinate offset,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            Offset = offset;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public GridCoordinate Offset { get; }
        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }
    }

    /// <summary>
    /// Ordered cell candidate produced by the swept footprint helper.
    /// Distance and parametric time are exposed for deterministic higher-level processing.
    /// </summary>
    internal readonly struct SweptCellCandidate
    {
        public SweptCellCandidate(
            GridCoordinate coordinate,
            float parametricT,
            float sweepDistance)
        {
            Coordinate = coordinate;
            ParametricT = parametricT;
            SweepDistance = sweepDistance;
        }

        public GridCoordinate Coordinate { get; }
        public float ParametricT { get; }
        public float SweepDistance { get; }
    }

    /// <summary>
    /// One deterministic contact group along a freeform movement sweep.
    /// The group represents a set of cells first reached at the same sweep distance.
    /// </summary>
    internal sealed class SweptFootprintContactGroup
    {
        public SweptFootprintContactGroup(
            float parametricT,
            float sweepDistance,
            BoardLocalContinuousPosition sampledPosition,
            IList<GridCoordinate> overlappedCells,
            IList<GridCoordinate> newlyEnteredCells,
            IList<SweptCellCandidate> orderedCandidates)
        {
            ParametricT = parametricT;
            SweepDistance = sweepDistance;
            SampledPosition = sampledPosition;
            OverlappedCells = new ReadOnlyCollection<GridCoordinate>(
                new List<GridCoordinate>(overlappedCells ?? throw new ArgumentNullException(nameof(overlappedCells))));
            NewlyEnteredCells = new ReadOnlyCollection<GridCoordinate>(
                new List<GridCoordinate>(newlyEnteredCells ?? throw new ArgumentNullException(nameof(newlyEnteredCells))));
            OrderedCandidates = new ReadOnlyCollection<SweptCellCandidate>(
                new List<SweptCellCandidate>(orderedCandidates ?? throw new ArgumentNullException(nameof(orderedCandidates))));
        }

        public float ParametricT { get; }
        public float SweepDistance { get; }
        public BoardLocalContinuousPosition SampledPosition { get; }
        public IReadOnlyList<GridCoordinate> OverlappedCells { get; }
        public IReadOnlyList<GridCoordinate> NewlyEnteredCells { get; }
        public IReadOnlyList<SweptCellCandidate> OrderedCandidates { get; }
    }

    /// <summary>
    /// Request for prototype-owned swept footprint enumeration.
    /// World positions are converted into board-local continuous cell space internally.
    /// The request models freeform drag movement only. It does not imply snap, committed
    /// occupancy changes, or any release-time board-state update.
    /// </summary>
    internal readonly struct SweptFootprintRequest
    {
        public SweptFootprintRequest(
            Vector3 previousAcceptedWorldPosition,
            Vector3 candidateWorldPosition,
            GridWorldLayout worldLayout,
            IReadOnlyList<FootprintCellRectangle> footprintRectangles)
        {
            PreviousAcceptedWorldPosition = previousAcceptedWorldPosition;
            CandidateWorldPosition = candidateWorldPosition;
            WorldLayout = worldLayout;
            FootprintRectangles = footprintRectangles ?? Array.Empty<FootprintCellRectangle>();
        }

        public Vector3 PreviousAcceptedWorldPosition { get; }
        public Vector3 CandidateWorldPosition { get; }
        public GridWorldLayout WorldLayout { get; }
        public IReadOnlyList<FootprintCellRectangle> FootprintRectangles { get; }
    }

    /// <summary>
    /// Pure geometry helper for the actively dragged hole's shape-aware query footprint.
    /// It preserves the authored footprint cell topology while insetting only exposed outer
    /// edges so narrow-corridor drag feel can be tuned without changing committed gameplay
    /// footprint truth.
    /// </summary>
    internal sealed class ShapeAwareDragFootprint
    {
        private static readonly GridCoordinate[] SingleCellOffsets =
        {
            new(0, 0)
        };

        public ShapeAwareDragFootprint(
            IReadOnlyList<GridCoordinate> footprintOffsets,
            float dragClearanceInsetCells)
        {
            float inset = Mathf.Clamp(dragClearanceInsetCells, 0f, 0.45f);
            IReadOnlyList<GridCoordinate> offsets =
                footprintOffsets != null && footprintOffsets.Count > 0
                    ? footprintOffsets
                    : SingleCellOffsets;

            HashSet<GridCoordinate> offsetSet = new(offsets);
            List<FootprintCellRectangle> rectangles = new(offsets.Count);

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < offsets.Count; i++)
            {
                GridCoordinate offset = offsets[i];

                float rectMinX = offset.X +
                                 (offsetSet.Contains(new GridCoordinate(offset.X - 1, offset.Y))
                                     ? 0f
                                     : inset);
                float rectMaxX = offset.X + 1f -
                                 (offsetSet.Contains(new GridCoordinate(offset.X + 1, offset.Y))
                                     ? 0f
                                     : inset);
                float rectMinY = offset.Y +
                                 (offsetSet.Contains(new GridCoordinate(offset.X, offset.Y - 1))
                                     ? 0f
                                     : inset);
                float rectMaxY = offset.Y + 1f -
                                 (offsetSet.Contains(new GridCoordinate(offset.X, offset.Y + 1))
                                     ? 0f
                                     : inset);

                FootprintCellRectangle rectangle = new(
                    offset,
                    rectMinX,
                    rectMinY,
                    rectMaxX,
                    rectMaxY);
                rectangles.Add(rectangle);

                minX = Mathf.Min(minX, rectMinX);
                minY = Mathf.Min(minY, rectMinY);
                maxX = Mathf.Max(maxX, rectMaxX);
                maxY = Mathf.Max(maxY, rectMaxY);
            }

            Rectangles = new ReadOnlyCollection<FootprintCellRectangle>(rectangles);
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public IReadOnlyList<FootprintCellRectangle> Rectangles { get; }
        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }
    }
}
