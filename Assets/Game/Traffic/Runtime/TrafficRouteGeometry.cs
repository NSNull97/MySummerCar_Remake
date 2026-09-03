using System;
using UnityEngine;

namespace MSC.Traffic
{
    public readonly struct TrafficRouteSample
    {
        public TrafficRouteSample(
            Vector3 position,
            Quaternion rotation,
            float progress01,
            int segmentIndex)
        {
            Position = position;
            Rotation = rotation;
            Progress01 = progress01;
            SegmentIndex = segmentIndex;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float Progress01 { get; }
        public int SegmentIndex { get; }
    }

    /// <summary>
    /// Allocation-free linear evaluator for the dense locked donor routes.
    /// The source waypoints are already sampled at road-following density, so
    /// physical steering uses a long pure-pursuit target instead of inventing
    /// a second smoothing spline that could cut across intersections.
    /// </summary>
    public sealed class TrafficRouteGeometry
    {
        private const int ProjectionSearchAheadSegments = 64;
        private const int ProjectionSearchBehindSegments = 12;

        private readonly Vector3[] points;
        private readonly float[] cumulativeDistances;
        private readonly bool closesLoop;
        private readonly int segmentCount;

        public TrafficRouteGeometry(TrafficRouteDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!definition.TryValidate(out string failure))
            {
                throw new ArgumentException(failure, nameof(definition));
            }

            RouteId = definition.RouteId;
            closesLoop = definition.ClosesLoop;
            points = new Vector3[definition.WorldPoints.Count];
            for (int index = 0; index < points.Length; index++)
            {
                points[index] = definition.WorldPoints[index];
            }

            segmentCount = closesLoop ? points.Length : points.Length - 1;
            cumulativeDistances = new float[segmentCount + 1];
            for (int index = 0; index < segmentCount; index++)
            {
                cumulativeDistances[index + 1] =
                    cumulativeDistances[index] +
                    Vector3.Distance(
                        points[index],
                        points[(index + 1) % points.Length]);
            }

            if (!float.IsFinite(TotalLengthMeters) || TotalLengthMeters <= 0.1f)
            {
                throw new ArgumentException(
                    $"Traffic route '{RouteId}' has no measurable length.",
                    nameof(definition));
            }
        }

        public string RouteId { get; }
        public float TotalLengthMeters =>
            cumulativeDistances[cumulativeDistances.Length - 1];
        public int PointCount => points.Length;
        public int SegmentCount => segmentCount;
        public bool ClosesLoop => closesLoop;

        public float ProgressAtPointIndex(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= points.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex));
            }

            if (pointIndex >= cumulativeDistances.Length)
            {
                return 1f;
            }

            return cumulativeDistances[pointIndex] / TotalLengthMeters;
        }

        public TrafficRouteSample ProjectNearest(
            Vector3 worldPosition,
            bool forward)
        {
            if (!IsFinite(worldPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(worldPosition));
            }

            int nearestSegment = 0;
            float nearestT = 0f;
            float nearestSqr = float.PositiveInfinity;
            for (int segment = 0; segment < segmentCount; segment++)
            {
                Vector3 a = points[segment];
                Vector3 delta = points[(segment + 1) % points.Length] - a;
                float denominator = delta.sqrMagnitude;
                float t = denominator > 0.000001f
                    ? Mathf.Clamp01(
                        Vector3.Dot(worldPosition - a, delta) / denominator)
                    : 0f;
                float sqr = (worldPosition - (a + delta * t)).sqrMagnitude;
                if (sqr >= nearestSqr)
                {
                    continue;
                }

                nearestSqr = sqr;
                nearestSegment = segment;
                nearestT = t;
            }

            float distance = Mathf.LerpUnclamped(
                cumulativeDistances[nearestSegment],
                cumulativeDistances[nearestSegment + 1],
                nearestT);
            return Resolve(distance / TotalLengthMeters, forward);
        }

        /// <summary>
        /// Resolves the nearest route branch whose authored travel direction
        /// agrees with the physical vehicle heading. This is intentionally a
        /// full-route query: it is used only by bounded deadlock recovery on
        /// donor routes that contain near-overlapping outbound and return
        /// branches, where a local segment hint can keep steering at a target
        /// behind the vehicle indefinitely.
        /// </summary>
        public bool TryProjectNearestAligned(
            Vector3 worldPosition,
            Vector3 worldForward,
            bool forward,
            float maximumDistanceMeters,
            float minimumAlignment,
            out TrafficRouteSample projection)
        {
            if (!IsFinite(worldPosition) ||
                !IsFinite(worldForward) ||
                !float.IsFinite(maximumDistanceMeters) ||
                maximumDistanceMeters <= 0f ||
                !float.IsFinite(minimumAlignment) ||
                minimumAlignment < -1f ||
                minimumAlignment > 1f)
            {
                projection = default;
                return false;
            }

            worldForward.y = 0f;
            if (worldForward.sqrMagnitude <= 0.000001f)
            {
                projection = default;
                return false;
            }

            worldForward.Normalize();
            int nearestSegment = -1;
            float nearestT = 0f;
            float nearestSqr = maximumDistanceMeters *
                               maximumDistanceMeters;
            for (int segment = 0; segment < segmentCount; segment++)
            {
                Vector3 a = points[segment];
                Vector3 delta = points[(segment + 1) % points.Length] - a;
                float denominator = delta.sqrMagnitude;
                if (denominator <= 0.000001f)
                {
                    continue;
                }

                Vector3 travelDirection = delta;
                travelDirection.y = 0f;
                if (!forward)
                {
                    travelDirection = -travelDirection;
                }

                if (travelDirection.sqrMagnitude <= 0.000001f ||
                    Vector3.Dot(
                        worldForward,
                        travelDirection.normalized) < minimumAlignment)
                {
                    continue;
                }

                float t = Mathf.Clamp01(
                    Vector3.Dot(worldPosition - a, delta) / denominator);
                float sqr = (worldPosition - (a + delta * t)).sqrMagnitude;
                if (sqr > nearestSqr)
                {
                    continue;
                }

                nearestSqr = sqr;
                nearestSegment = segment;
                nearestT = t;
            }

            if (nearestSegment < 0)
            {
                projection = default;
                return false;
            }

            float distance = Mathf.LerpUnclamped(
                cumulativeDistances[nearestSegment],
                cumulativeDistances[nearestSegment + 1],
                nearestT);
            projection = Resolve(distance / TotalLengthMeters, forward);
            return true;
        }

        public TrafficRouteSample Resolve(float progress01, bool forward)
        {
            float normalized = closesLoop
                ? Repeat01(progress01)
                : Mathf.Clamp01(progress01);
            float distance = normalized * TotalLengthMeters;
            int upper = Array.BinarySearch(cumulativeDistances, distance);
            if (upper < 0)
            {
                upper = ~upper;
            }

            upper = Mathf.Clamp(upper, 1, cumulativeDistances.Length - 1);
            int lower = upper - 1;
            float span = cumulativeDistances[upper] -
                         cumulativeDistances[lower];
            float t = span > 0.000001f
                ? (distance - cumulativeDistances[lower]) / span
                : 0f;
            Vector3 a = points[lower];
            Vector3 b = points[(lower + 1) % points.Length];
            Vector3 direction = b - a;
            direction.y = 0f;
            if (!forward)
            {
                direction = -direction;
            }

            Quaternion rotation = direction.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            return new TrafficRouteSample(
                Vector3.LerpUnclamped(a, b, t),
                rotation,
                normalized,
                lower);
        }

        public bool TryProjectAndResolveAhead(
            Vector3 physicalPosition,
            float retainedProgress01,
            bool forward,
            float lookAheadMeters,
            ref int segmentHint,
            out TrafficRouteSample projection,
            out TrafficRouteSample guidance)
        {
            if (!IsFinite(physicalPosition) ||
                !float.IsFinite(retainedProgress01) ||
                !float.IsFinite(lookAheadMeters) ||
                lookAheadMeters <= 0f)
            {
                projection = default;
                guidance = default;
                return false;
            }

            if (segmentHint < 0 || segmentHint >= segmentCount)
            {
                segmentHint = Mathf.Clamp(
                    Mathf.FloorToInt(
                        Mathf.Clamp01(retainedProgress01) * segmentCount),
                    0,
                    segmentCount - 1);
            }

            FindNearestProjection(
                physicalPosition,
                segmentHint,
                forward,
                out int nearestSegment,
                out float nearestT);
            segmentHint = nearestSegment;
            float projectedDistance = Mathf.LerpUnclamped(
                cumulativeDistances[nearestSegment],
                cumulativeDistances[nearestSegment + 1],
                nearestT);
            float projectedProgress = projectedDistance / TotalLengthMeters;
            projection = Resolve(projectedProgress, forward);

            float targetDistance = projectedDistance +
                                   (forward ? lookAheadMeters : -lookAheadMeters);
            if (closesLoop)
            {
                targetDistance %= TotalLengthMeters;
                if (targetDistance < 0f)
                {
                    targetDistance += TotalLengthMeters;
                }
            }
            else
            {
                targetDistance = Mathf.Clamp(
                    targetDistance,
                    0f,
                    TotalLengthMeters);
            }

            guidance = Resolve(targetDistance / TotalLengthMeters, forward);
            return true;
        }

        /// <summary>
        /// Projects onto an inclusive authored point range and treats the last
        /// point-to-first point edge as that range's closure. This preserves a
        /// donor sub-loop embedded in a larger serialized route without ever
        /// steering through the excluded prefix or the route asset's ordinary
        /// last-to-zero closure.
        /// </summary>
        public bool TryProjectAndResolveAheadWithinClosedPointRange(
            Vector3 physicalPosition,
            float retainedProgress01,
            bool forward,
            float lookAheadMeters,
            int firstPointIndex,
            int lastPointIndex,
            ref int segmentHint,
            out TrafficRouteSample projection,
            out TrafficRouteSample guidance)
        {
            if (!IsFinite(physicalPosition) ||
                !float.IsFinite(retainedProgress01) ||
                !float.IsFinite(lookAheadMeters) ||
                lookAheadMeters <= 0f ||
                !TryResolveClosedRange(
                    firstPointIndex,
                    lastPointIndex,
                    out float mainLength,
                    out float closureLength,
                    out float rangeLength))
            {
                projection = default;
                guidance = default;
                return false;
            }

            int rangeSegmentCount = lastPointIndex - firstPointIndex + 1;
            int localHint = segmentHint >= firstPointIndex &&
                            segmentHint <= lastPointIndex
                ? segmentHint - firstPointIndex
                : Mathf.Clamp(
                    Mathf.FloorToInt(
                        Mathf.Clamp01(retainedProgress01) * segmentCount) -
                    firstPointIndex,
                    0,
                    rangeSegmentCount - 1);
            FindNearestClosedRangeProjection(
                physicalPosition,
                firstPointIndex,
                lastPointIndex,
                localHint,
                forward,
                out int nearestLocalSegment,
                out float nearestT);
            int nearestSegment = firstPointIndex + nearestLocalSegment;
            segmentHint = nearestSegment;
            float segmentStartDistance = nearestSegment == lastPointIndex
                ? mainLength
                : cumulativeDistances[nearestSegment] -
                  cumulativeDistances[firstPointIndex];
            float segmentLength = nearestSegment == lastPointIndex
                ? closureLength
                : cumulativeDistances[nearestSegment + 1] -
                  cumulativeDistances[nearestSegment];
            float projectedDistance = segmentStartDistance +
                                      segmentLength * nearestT;
            projection = ResolveClosedRangeDistance(
                projectedDistance,
                forward,
                firstPointIndex,
                lastPointIndex,
                mainLength,
                closureLength,
                rangeLength);
            float targetDistance = RepeatDistance(
                projectedDistance +
                (forward ? lookAheadMeters : -lookAheadMeters),
                rangeLength);
            guidance = ResolveClosedRangeDistance(
                targetDistance,
                forward,
                firstPointIndex,
                lastPointIndex,
                mainLength,
                closureLength,
                rangeLength);
            return true;
        }

        /// <summary>
        /// Full-range aligned recovery companion to the bounded projection
        /// above. It changes only the chosen route target; callers remain
        /// responsible for physically steering the Rigidbody back to it.
        /// </summary>
        public bool TryProjectNearestAlignedWithinClosedPointRange(
            Vector3 worldPosition,
            Vector3 worldForward,
            bool forward,
            float maximumDistanceMeters,
            float minimumAlignment,
            float lookAheadMeters,
            int firstPointIndex,
            int lastPointIndex,
            out TrafficRouteSample projection,
            out TrafficRouteSample guidance)
        {
            if (!IsFinite(worldPosition) ||
                !IsFinite(worldForward) ||
                !float.IsFinite(maximumDistanceMeters) ||
                maximumDistanceMeters <= 0f ||
                !float.IsFinite(minimumAlignment) ||
                minimumAlignment < -1f ||
                minimumAlignment > 1f ||
                !float.IsFinite(lookAheadMeters) ||
                lookAheadMeters <= 0f ||
                !TryResolveClosedRange(
                    firstPointIndex,
                    lastPointIndex,
                    out float mainLength,
                    out float closureLength,
                    out float rangeLength))
            {
                projection = default;
                guidance = default;
                return false;
            }

            worldForward.y = 0f;
            if (worldForward.sqrMagnitude <= 0.000001f)
            {
                projection = default;
                guidance = default;
                return false;
            }

            worldForward.Normalize();
            int nearestSegment = -1;
            float nearestT = 0f;
            float nearestSqr = maximumDistanceMeters * maximumDistanceMeters;
            for (int segment = firstPointIndex;
                 segment <= lastPointIndex;
                 segment++)
            {
                Vector3 a = points[segment];
                Vector3 delta = points[segment == lastPointIndex
                    ? firstPointIndex
                    : segment + 1] - a;
                float denominator = delta.sqrMagnitude;
                if (denominator <= 0.000001f)
                {
                    continue;
                }

                Vector3 travelDirection = delta;
                travelDirection.y = 0f;
                if (!forward)
                {
                    travelDirection = -travelDirection;
                }

                if (travelDirection.sqrMagnitude <= 0.000001f ||
                    Vector3.Dot(
                        worldForward,
                        travelDirection.normalized) < minimumAlignment)
                {
                    continue;
                }

                float t = Mathf.Clamp01(
                    Vector3.Dot(worldPosition - a, delta) / denominator);
                float sqr = (worldPosition - (a + delta * t)).sqrMagnitude;
                if (sqr > nearestSqr)
                {
                    continue;
                }

                nearestSqr = sqr;
                nearestSegment = segment;
                nearestT = t;
            }

            if (nearestSegment < 0)
            {
                projection = default;
                guidance = default;
                return false;
            }

            float segmentStartDistance = nearestSegment == lastPointIndex
                ? mainLength
                : cumulativeDistances[nearestSegment] -
                  cumulativeDistances[firstPointIndex];
            float segmentLength = nearestSegment == lastPointIndex
                ? closureLength
                : cumulativeDistances[nearestSegment + 1] -
                  cumulativeDistances[nearestSegment];
            float projectedDistance = segmentStartDistance +
                                      segmentLength * nearestT;
            projection = ResolveClosedRangeDistance(
                projectedDistance,
                forward,
                firstPointIndex,
                lastPointIndex,
                mainLength,
                closureLength,
                rangeLength);
            guidance = ResolveClosedRangeDistance(
                RepeatDistance(
                    projectedDistance +
                    (forward ? lookAheadMeters : -lookAheadMeters),
                    rangeLength),
                forward,
                firstPointIndex,
                lastPointIndex,
                mainLength,
                closureLength,
                rangeLength);
            return true;
        }

        public bool TryResolveAheadWithinClosedPointRange(
            float progress01,
            bool forward,
            float distanceMeters,
            int firstPointIndex,
            int lastPointIndex,
            out TrafficRouteSample sample)
        {
            if (!float.IsFinite(progress01) ||
                !float.IsFinite(distanceMeters) ||
                distanceMeters < 0f ||
                !TryResolveClosedRange(
                    firstPointIndex,
                    lastPointIndex,
                    out float mainLength,
                    out float closureLength,
                    out float rangeLength))
            {
                sample = default;
                return false;
            }

            float globalDistance = Mathf.Clamp01(progress01) *
                                   TotalLengthMeters;
            float localDistance = Mathf.Clamp(
                globalDistance - cumulativeDistances[firstPointIndex],
                0f,
                mainLength);
            sample = ResolveClosedRangeDistance(
                RepeatDistance(
                    localDistance +
                    (forward ? distanceMeters : -distanceMeters),
                    rangeLength),
                forward,
                firstPointIndex,
                lastPointIndex,
                mainLength,
                closureLength,
                rangeLength);
            return true;
        }

        public float AdvanceProgress(
            float progress01,
            bool forward,
            float distanceMeters,
            out bool completedCircuit)
        {
            completedCircuit = false;
            if (!float.IsFinite(distanceMeters) || distanceMeters < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distanceMeters));
            }

            float delta = distanceMeters / TotalLengthMeters *
                          (forward ? 1f : -1f);
            float next = progress01 + delta;
            if (closesLoop)
            {
                completedCircuit = next >= 1f || next < 0f;
                return Repeat01(next);
            }

            float clamped = Mathf.Clamp01(next);
            completedCircuit = !Mathf.Approximately(next, clamped);
            return clamped;
        }

        public bool CanCommitPhysicalProjection(
            float retainedProgress01,
            float projectedProgress01,
            bool forward,
            float maximumBackwardMeters = 2f)
        {
            if (!float.IsFinite(retainedProgress01) ||
                !float.IsFinite(projectedProgress01) ||
                !float.IsFinite(maximumBackwardMeters) ||
                maximumBackwardMeters < 0f)
            {
                return false;
            }

            float delta = projectedProgress01 - retainedProgress01;
            if (closesLoop)
            {
                if (forward && delta < -0.5f)
                {
                    delta += 1f;
                }
                else if (!forward && delta > 0.5f)
                {
                    delta -= 1f;
                }
            }

            float directedMeters = delta * TotalLengthMeters *
                                   (forward ? 1f : -1f);
            return directedMeters >= -maximumBackwardMeters;
        }

        private void FindNearestProjection(
            Vector3 position,
            int hint,
            bool forward,
            out int nearestSegment,
            out float nearestT)
        {
            nearestSegment = hint;
            nearestT = 0f;
            float nearestSqr = float.PositiveInfinity;
            int searchSpan = ProjectionSearchAheadSegments +
                             ProjectionSearchBehindSegments + 1;
            int searchCount = Mathf.Min(segmentCount, searchSpan);
            int start = segmentCount <= searchSpan
                ? 0
                : forward
                    ? hint - ProjectionSearchBehindSegments
                    : hint - ProjectionSearchAheadSegments;
            for (int offset = 0; offset < searchCount; offset++)
            {
                int segment = start + offset;
                if (closesLoop)
                {
                    segment %= segmentCount;
                    if (segment < 0)
                    {
                        segment += segmentCount;
                    }
                }
                else if (segment < 0 || segment >= segmentCount)
                {
                    continue;
                }

                Vector3 a = points[segment];
                Vector3 delta = points[(segment + 1) % points.Length] - a;
                float denominator = delta.sqrMagnitude;
                float t = denominator > 0.000001f
                    ? Mathf.Clamp01(
                        Vector3.Dot(position - a, delta) / denominator)
                    : 0f;
                float sqr = (position - (a + delta * t)).sqrMagnitude;
                if (sqr >= nearestSqr)
                {
                    continue;
                }

                nearestSqr = sqr;
                nearestSegment = segment;
                nearestT = t;
            }
        }

        private bool TryResolveClosedRange(
            int firstPointIndex,
            int lastPointIndex,
            out float mainLength,
            out float closureLength,
            out float rangeLength)
        {
            mainLength = 0f;
            closureLength = 0f;
            rangeLength = 0f;
            if (firstPointIndex < 0 ||
                lastPointIndex >= points.Length ||
                firstPointIndex >= lastPointIndex)
            {
                return false;
            }

            mainLength = cumulativeDistances[lastPointIndex] -
                         cumulativeDistances[firstPointIndex];
            closureLength = Vector3.Distance(
                points[lastPointIndex],
                points[firstPointIndex]);
            rangeLength = mainLength + closureLength;
            return float.IsFinite(rangeLength) && rangeLength > 0.1f;
        }

        private void FindNearestClosedRangeProjection(
            Vector3 position,
            int firstPointIndex,
            int lastPointIndex,
            int localHint,
            bool forward,
            out int nearestLocalSegment,
            out float nearestT)
        {
            int rangeSegmentCount = lastPointIndex - firstPointIndex + 1;
            nearestLocalSegment = Mathf.Clamp(
                localHint,
                0,
                rangeSegmentCount - 1);
            nearestT = 0f;
            float nearestSqr = float.PositiveInfinity;
            int searchSpan = ProjectionSearchAheadSegments +
                             ProjectionSearchBehindSegments + 1;
            int searchCount = Mathf.Min(rangeSegmentCount, searchSpan);
            int start = rangeSegmentCount <= searchSpan
                ? 0
                : forward
                    ? localHint - ProjectionSearchBehindSegments
                    : localHint - ProjectionSearchAheadSegments;
            for (int offset = 0; offset < searchCount; offset++)
            {
                int localSegment = (start + offset) % rangeSegmentCount;
                if (localSegment < 0)
                {
                    localSegment += rangeSegmentCount;
                }

                int segment = firstPointIndex + localSegment;
                Vector3 a = points[segment];
                Vector3 delta = points[segment == lastPointIndex
                    ? firstPointIndex
                    : segment + 1] - a;
                float denominator = delta.sqrMagnitude;
                float t = denominator > 0.000001f
                    ? Mathf.Clamp01(
                        Vector3.Dot(position - a, delta) / denominator)
                    : 0f;
                float sqr = (position - (a + delta * t)).sqrMagnitude;
                if (sqr >= nearestSqr)
                {
                    continue;
                }

                nearestSqr = sqr;
                nearestLocalSegment = localSegment;
                nearestT = t;
            }
        }

        private TrafficRouteSample ResolveClosedRangeDistance(
            float distanceMeters,
            bool forward,
            int firstPointIndex,
            int lastPointIndex,
            float mainLength,
            float closureLength,
            float rangeLength)
        {
            float localDistance = RepeatDistance(distanceMeters, rangeLength);
            if (localDistance <= mainLength || closureLength <= 0.000001f)
            {
                float globalDistance = cumulativeDistances[firstPointIndex] +
                                       Mathf.Min(localDistance, mainLength);
                return Resolve(globalDistance / TotalLengthMeters, forward);
            }

            float t = Mathf.Clamp01(
                (localDistance - mainLength) / closureLength);
            Vector3 a = points[lastPointIndex];
            Vector3 b = points[firstPointIndex];
            Vector3 direction = b - a;
            direction.y = 0f;
            if (!forward)
            {
                direction = -direction;
            }

            Quaternion rotation = direction.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            return new TrafficRouteSample(
                Vector3.LerpUnclamped(a, b, t),
                rotation,
                t < 0.5f
                    ? ProgressAtPointIndex(lastPointIndex)
                    : ProgressAtPointIndex(firstPointIndex),
                lastPointIndex);
        }

        private static float RepeatDistance(float value, float length)
        {
            value %= length;
            return value < 0f ? value + length : value;
        }

        private static float Repeat01(float value)
        {
            value %= 1f;
            return value < 0f ? value + 1f : value;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
