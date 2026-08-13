using System;
using System.Collections.Generic;
using PuzzleFramework.CoreBoard;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned helper that enumerates deterministic swept footprint contact groups
    /// for freeform drag validation in board-local continuous cell space.
    /// This helper does not own color rules, collection, capacity, or outcome logic.
    /// It also does not own committed board state. Callers must decide how swept contact groups
    /// relate to structural occupancy, drag-session state, and release-time commit.
    /// </summary>
    internal sealed class SweptFootprintHelper
    {
        private const float EventEpsilon = 0.0001f;
        private const float EqualityEpsilon = 0.00001f;

        /// <summary>
        /// Enumerates ordered contact groups between the previous accepted position and the
        /// current candidate position without relying on final-position-only sampling.
        /// </summary>
        public IReadOnlyList<SweptFootprintContactGroup> EnumerateContactGroups(
            SweptFootprintRequest request)
        {
            BoardLocalContinuousPosition previousAccepted =
                BoardLocalContinuousPosition.FromWorld(
                    request.PreviousAcceptedWorldPosition,
                    request.WorldLayout);
            BoardLocalContinuousPosition candidate =
                BoardLocalContinuousPosition.FromWorld(
                    request.CandidateWorldPosition,
                    request.WorldLayout);

            IReadOnlyList<FootprintCellRectangle> footprint = request.FootprintRectangles;
            List<float> eventTimes = BuildEventTimes(previousAccepted, candidate, footprint);
            float sweepDistance = previousAccepted.DistanceTo(candidate);
            HashSet<GridCoordinate> acceptedOverlap = ResolveOverlap(previousAccepted, footprint);
            List<SweptFootprintContactGroup> groups = new();

            for (int i = 0; i < eventTimes.Count; i++)
            {
                float currentT = eventTimes[i];
                float nextT = i < eventTimes.Count - 1 ? eventTimes[i + 1] : 1f;
                float sampleT = currentT < 1f
                    ? Mathf.Min(1f, currentT + ((nextT - currentT) * 0.5f))
                    : 1f;

                BoardLocalContinuousPosition sampledPosition =
                    BoardLocalContinuousPosition.Lerp(previousAccepted, candidate, sampleT);
                HashSet<GridCoordinate> overlap = ResolveOverlap(sampledPosition, footprint);
                List<GridCoordinate> newlyEntered = BuildNewlyEnteredCells(acceptedOverlap, overlap);

                if (newlyEntered.Count == 0 && SetEquals(acceptedOverlap, overlap))
                {
                    continue;
                }

                SortCoordinates(newlyEntered);
                List<GridCoordinate> overlappedCells = new(overlap);
                SortCoordinates(overlappedCells);

                float groupDistance = sweepDistance * currentT;
                List<SweptCellCandidate> orderedCandidates =
                    BuildOrderedCandidates(newlyEntered, currentT, groupDistance);

                groups.Add(
                    new SweptFootprintContactGroup(
                        currentT,
                        groupDistance,
                        sampledPosition,
                        overlappedCells,
                        newlyEntered,
                        orderedCandidates));

                acceptedOverlap = overlap;
            }

            return groups;
        }

        private static List<float> BuildEventTimes(
            BoardLocalContinuousPosition previousAccepted,
            BoardLocalContinuousPosition candidate,
            IReadOnlyList<FootprintCellRectangle> footprint)
        {
            List<float> eventTimes = new();
            float deltaX = candidate.X - previousAccepted.X;
            float deltaY = candidate.Y - previousAccepted.Y;

            for (int i = 0; i < footprint.Count; i++)
            {
                AddAxisCrossingTimes(previousAccepted.X + footprint[i].MinX, deltaX, eventTimes);
                AddAxisCrossingTimes(previousAccepted.X + footprint[i].MaxX, deltaX, eventTimes);
                AddAxisCrossingTimes(previousAccepted.Y + footprint[i].MinY, deltaY, eventTimes);
                AddAxisCrossingTimes(previousAccepted.Y + footprint[i].MaxY, deltaY, eventTimes);
            }

            eventTimes.Add(1f);
            eventTimes.Sort();
            return DeduplicateTimes(eventTimes);
        }

        private static void AddAxisCrossingTimes(
            float edgeStart,
            float delta,
            ICollection<float> eventTimes)
        {
            if (Mathf.Abs(delta) <= EqualityEpsilon)
            {
                return;
            }

            float edgeEnd = edgeStart + delta;
            float lower = Mathf.Min(edgeStart, edgeEnd) + EventEpsilon;
            float upper = Mathf.Max(edgeStart, edgeEnd) - EventEpsilon;

            if (upper < lower)
            {
                return;
            }

            int firstLine = Mathf.CeilToInt(lower);
            int lastLine = Mathf.FloorToInt(upper);

            for (int line = firstLine; line <= lastLine; line++)
            {
                float t = (line - edgeStart) / delta;
                if (t > EqualityEpsilon && t < 1f - EqualityEpsilon)
                {
                    eventTimes.Add(t);
                }
            }
        }

        private static List<float> DeduplicateTimes(IReadOnlyList<float> sortedTimes)
        {
            List<float> result = new(sortedTimes.Count);

            for (int i = 0; i < sortedTimes.Count; i++)
            {
                if (result.Count == 0 ||
                    Mathf.Abs(result[result.Count - 1] - sortedTimes[i]) > EqualityEpsilon)
                {
                    result.Add(sortedTimes[i]);
                }
            }

            return result;
        }

        private static HashSet<GridCoordinate> ResolveOverlap(
            BoardLocalContinuousPosition position,
            IReadOnlyList<FootprintCellRectangle> footprint)
        {
            HashSet<GridCoordinate> overlap = new();

            for (int i = 0; i < footprint.Count; i++)
            {
                float minX = position.X + footprint[i].MinX;
                float maxX = position.X + footprint[i].MaxX;
                float minY = position.Y + footprint[i].MinY;
                float maxY = position.Y + footprint[i].MaxY;

                int xStart = Mathf.FloorToInt(minX);
                int xEndExclusive = Mathf.CeilToInt(maxX);
                int yStart = Mathf.FloorToInt(minY);
                int yEndExclusive = Mathf.CeilToInt(maxY);

                for (int x = xStart; x < xEndExclusive; x++)
                {
                    if (!HasPositiveOverlap(minX, maxX, x, x + 1f))
                    {
                        continue;
                    }

                    for (int y = yStart; y < yEndExclusive; y++)
                    {
                        if (HasPositiveOverlap(minY, maxY, y, y + 1f))
                        {
                            overlap.Add(new GridCoordinate(x, y));
                        }
                    }
                }
            }

            return overlap;
        }

        private static bool HasPositiveOverlap(
            float rectMin,
            float rectMax,
            float cellMin,
            float cellMax)
        {
            return Mathf.Min(rectMax, cellMax) - Mathf.Max(rectMin, cellMin) > EqualityEpsilon;
        }

        private static List<GridCoordinate> BuildNewlyEnteredCells(
            HashSet<GridCoordinate> acceptedOverlap,
            HashSet<GridCoordinate> overlap)
        {
            List<GridCoordinate> newlyEntered = new();

            foreach (GridCoordinate coordinate in overlap)
            {
                if (!acceptedOverlap.Contains(coordinate))
                {
                    newlyEntered.Add(coordinate);
                }
            }

            return newlyEntered;
        }

        private static List<SweptCellCandidate> BuildOrderedCandidates(
            IReadOnlyList<GridCoordinate> newlyEntered,
            float parametricT,
            float sweepDistance)
        {
            List<SweptCellCandidate> candidates = new(newlyEntered.Count);

            for (int i = 0; i < newlyEntered.Count; i++)
            {
                candidates.Add(
                    new SweptCellCandidate(
                        newlyEntered[i],
                        parametricT,
                        sweepDistance));
            }

            candidates.Sort(CompareCandidates);
            return candidates;
        }

        private static int CompareCandidates(
            SweptCellCandidate left,
            SweptCellCandidate right)
        {
            int distanceComparison = left.SweepDistance.CompareTo(right.SweepDistance);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            int yComparison = left.Coordinate.Y.CompareTo(right.Coordinate.Y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return left.Coordinate.X.CompareTo(right.Coordinate.X);
        }

        private static void SortCoordinates(List<GridCoordinate> coordinates)
        {
            coordinates.Sort(
                static (left, right) =>
                {
                    int yComparison = left.Y.CompareTo(right.Y);
                    if (yComparison != 0)
                    {
                        return yComparison;
                    }

                    return left.X.CompareTo(right.X);
                });
        }

        private static bool SetEquals(
            HashSet<GridCoordinate> left,
            HashSet<GridCoordinate> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (GridCoordinate coordinate in left)
            {
                if (!right.Contains(coordinate))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
