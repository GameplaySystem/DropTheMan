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
        {
            Offset = offset;
            MinX = offset.X;
            MinY = offset.Y;
            MaxX = offset.X + 1f;
            MaxY = offset.Y + 1f;
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
    /// </summary>
    internal readonly struct SweptFootprintRequest
    {
        public SweptFootprintRequest(
            Vector3 previousAcceptedWorldPosition,
            Vector3 candidateWorldPosition,
            GridWorldLayout worldLayout,
            IReadOnlyList<GridCoordinate> footprintOffsets)
        {
            PreviousAcceptedWorldPosition = previousAcceptedWorldPosition;
            CandidateWorldPosition = candidateWorldPosition;
            WorldLayout = worldLayout;
            FootprintOffsets = footprintOffsets ?? Array.Empty<GridCoordinate>();
        }

        public Vector3 PreviousAcceptedWorldPosition { get; }
        public Vector3 CandidateWorldPosition { get; }
        public GridWorldLayout WorldLayout { get; }
        public IReadOnlyList<GridCoordinate> FootprintOffsets { get; }
    }
}
