using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.World.Streaming
{
    /// <summary>
    /// Project-owned, runtime-validated placement boundary for important
    /// objects recovered from invalid below-world poses. Callers provide
    /// deterministic domain/cell batches, stable-ID sorted inside each batch;
    /// every accepted pose has static support and an exclusive reservation in
    /// the formation in front of the player home.
    /// </summary>
    public sealed class ImportantObjectRecoveryFormation
    {
        public const string AnchorId =
            "anchor.home.important-object-recovery";
        private const int Columns = 20;
        private const int Rows = 20;
        private const int MaximumCandidateCount = Columns * Rows;
        private const float CandidateSpacingMeters = 0.75f;
        private const float SurfaceClearanceMeters = 0.04f;
        private const float ProbeHeightMeters = 24f;
        private const float ProbeDistanceMeters = 80f;

        private readonly Vector3 origin;
        private readonly List<Bounds> reservations = new List<Bounds>();
        private int nextCandidateIndex;

        public ImportantObjectRecoveryFormation(Vector3 configuredOrigin)
        {
            if (!IsFinite(configuredOrigin))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredOrigin),
                    "Important-object recovery origin must be finite.");
            }

            origin = configuredOrigin;
        }

        public Vector3 Origin => origin;

        public int ReservationCount => reservations.Count;

        public void Reset()
        {
            reservations.Clear();
            nextCandidateIndex = 0;
        }

        public bool TryReserve(
            GameObject target,
            Quaternion desiredRotation,
            out Pose pose,
            out string failure)
        {
            pose = default;
            if (target == null || !IsFinite(desiredRotation))
            {
                failure =
                    "Recovery target or desired rotation is invalid.";
                return false;
            }

            Rigidbody body = target.GetComponentInParent<Rigidbody>();
            Vector3 currentPivot = body != null
                ? body.position
                : target.transform.position;
            Quaternion currentRotation = body != null
                ? body.rotation
                : target.transform.rotation;
            Collider[] ownedColliders = target
                .GetComponentsInChildren<Collider>(true)
                .Where(candidate =>
                    candidate != null &&
                    candidate.enabled &&
                    (body == null || candidate.attachedRigidbody == body))
                .ToArray();
            Bounds currentBounds = ResolveBounds(
                ownedColliders,
                currentPivot);
            Quaternion normalizedDesiredRotation = desiredRotation.normalized;
            Quaternion rotationDelta = normalizedDesiredRotation *
                Quaternion.Inverse(currentRotation.normalized);
            Vector3 centerOffset = rotationDelta *
                (currentBounds.center - currentPivot);
            Vector3 halfExtents = RotateBoundsExtents(
                currentBounds.extents,
                rotationDelta);
            float pivotAboveBottom = Mathf.Max(
                0.05f,
                halfExtents.y - centerOffset.y);
            halfExtents.x = Mathf.Max(0.12f, halfExtents.x);
            halfExtents.y = Mathf.Max(0.08f, halfExtents.y);
            halfExtents.z = Mathf.Max(0.12f, halfExtents.z);

            int supportMask = ResolveSupportMask();
            if (supportMask == 0)
            {
                failure =
                    "WorldSurface/WorldSolid layers are unavailable for recovery.";
                return false;
            }

            for (int attempt = 0;
                 attempt < MaximumCandidateCount &&
                 nextCandidateIndex < MaximumCandidateCount;
                 attempt++)
            {
                int candidateIndex = nextCandidateIndex++;
                int row = candidateIndex / Columns;
                int column = candidateIndex % Columns;
                // Start beside the configured centre and expand symmetrically.
                // This keeps small recoveries close to the front-door anchor
                // while retaining deterministic stable-ID ordering.
                float centeredColumn = column % 2 == 0
                    ? -(column / 2f + 0.5f)
                    : column / 2f + 0.5f;
                Vector3 probeOrigin = new Vector3(
                    origin.x + centeredColumn * CandidateSpacingMeters,
                    origin.y + ProbeHeightMeters,
                    origin.z + row * CandidateSpacingMeters);
                if (!Physics.Raycast(
                        probeOrigin,
                        Vector3.down,
                        out RaycastHit supportHit,
                        ProbeDistanceMeters,
                        supportMask,
                        QueryTriggerInteraction.Ignore) ||
                    supportHit.collider == null ||
                    supportHit.rigidbody != null)
                {
                    continue;
                }

                Vector3 targetPosition = new Vector3(
                    probeOrigin.x,
                    supportHit.point.y + pivotAboveBottom +
                    SurfaceClearanceMeters,
                    probeOrigin.z);
                Bounds plannedBounds = new Bounds(
                    targetPosition + centerOffset,
                    halfExtents * 2f);
                if (reservations.Any(existing =>
                        existing.Intersects(plannedBounds)))
                {
                    continue;
                }

                Collider[] overlaps = Physics.OverlapBox(
                    plannedBounds.center,
                    plannedBounds.extents * 0.96f,
                    Quaternion.identity,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                bool obstructed = overlaps.Any(candidate =>
                    candidate != null &&
                    candidate != supportHit.collider &&
                    !ownedColliders.Contains(candidate));
                if (obstructed)
                {
                    continue;
                }

                reservations.Add(plannedBounds);
                pose = new Pose(
                    targetPosition,
                    normalizedDesiredRotation);
                failure = string.Empty;
                return true;
            }

            failure =
                $"No supported, unobstructed slot was found inside the " +
                $"bounded {Columns}x{Rows} home-front formation at " +
                $"'{AnchorId}'.";
            return false;
        }

        private static Bounds ResolveBounds(
            IReadOnlyList<Collider> colliders,
            Vector3 pivot)
        {
            if (colliders == null || colliders.Count == 0)
            {
                return new Bounds(pivot, Vector3.one * 0.5f);
            }

            Bounds bounds = colliders[0].bounds;
            for (int index = 1; index < colliders.Count; index++)
            {
                bounds.Encapsulate(colliders[index].bounds);
            }

            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents) ||
                bounds.extents.sqrMagnitude <= 0.000001f)
            {
                return new Bounds(pivot, Vector3.one * 0.5f);
            }

            return bounds;
        }

        private static Vector3 RotateBoundsExtents(
            Vector3 extents,
            Quaternion rotation)
        {
            Vector3 axisX = rotation * new Vector3(extents.x, 0f, 0f);
            Vector3 axisY = rotation * new Vector3(0f, extents.y, 0f);
            Vector3 axisZ = rotation * new Vector3(0f, 0f, extents.z);
            return new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
        }

        private static int ResolveSupportMask()
        {
            int surfaceLayer = LayerMask.NameToLayer("WorldSurface");
            int solidLayer = LayerMask.NameToLayer("WorldSolid");
            int mask = 0;
            if (surfaceLayer >= 0)
            {
                mask |= 1 << surfaceLayer;
            }

            if (solidLayer >= 0)
            {
                mask |= 1 << solidLayer;
            }

            return mask;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w) &&
            value.x * value.x + value.y * value.y +
            value.z * value.z + value.w * value.w > 0.000001f;
    }
}
