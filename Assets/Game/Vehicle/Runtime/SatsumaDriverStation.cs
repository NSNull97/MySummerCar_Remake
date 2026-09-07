using System;
using MSC.Core.Identity;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Explicit, interior-only driver station. Occupancy is a capsule overlap,
    /// never a door/name lookup or a long-distance interaction ray.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SatsumaDriverStation : MonoBehaviour
    {
        public const string StockStationId = "vehicle.satsuma.driver-station.stock";
        public const string StockSeatMountId = "mount.satsuma.seat-driver";
        [SerializeField] private string stationId = StockStationId;
        [SerializeField] private string vehicleStableId;
        [SerializeField] private CapsuleCollider activationTrigger;
        [SerializeField] private Transform driverEyeAnchor;
        [SerializeField] private PartInstance driverSeat;
        [SerializeField] private Rigidbody vehicleBody;
        [SerializeField, Min(0f)] private float maximumExitVelocityComponent = .15f;

        public string StationId => stationId;
        public string VehicleStableId => vehicleStableId;
        public CapsuleCollider ActivationTrigger => activationTrigger;
        public Transform DriverEyeAnchor => driverEyeAnchor;
        public PartInstance DriverSeat => driverSeat;
        public Rigidbody VehicleBody => vehicleBody;
        public bool IsAvailable => isActiveAndEnabled && driverSeat != null &&
            driverSeat.IsInstalled && activationTrigger != null && driverEyeAnchor != null && vehicleBody != null;
        public bool CanReleaseWhileStationary => vehicleBody != null &&
            IsWithinExitVelocity(vehicleBody.linearVelocity, maximumExitVelocityComponent);

        public void Configure(string identity, CapsuleCollider trigger, Transform eyes,
            PartInstance seat, Rigidbody body)
        {
            if (!StableEntityId.TryParse(identity, out _) || trigger == null || !trigger.isTrigger ||
                eyes == null || seat == null || body == null || !eyes.IsChildOf(transform) ||
                !trigger.transform.IsChildOf(transform))
                throw new ArgumentException("The driver station requires explicit owned geometry, seat and vehicle identity.");
            vehicleStableId = identity;
            activationTrigger = trigger;
            driverEyeAnchor = eyes;
            driverSeat = seat;
            vehicleBody = body;
        }

        public bool ContainsPlayer(CharacterController player)
        {
            if (!isActiveAndEnabled || activationTrigger == null || !activationTrigger.enabled ||
                !activationTrigger.gameObject.activeInHierarchy || player == null || !player.enabled ||
                !player.gameObject.activeInHierarchy) return false;
            return ContainsPlayerPose(player, player.transform.position, player.transform.rotation);
        }

        public bool ContainsPlayerPose(CharacterController player, Vector3 position, Quaternion rotation)
        {
            if (player == null || activationTrigger == null) return false;
            Vector3 scale = player.transform.lossyScale;
            float radius = player.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            Vector3 center = position + rotation * Vector3.Scale(player.center, scale);
            Vector3 half = rotation * Vector3.up * Mathf.Max(0f, player.height * Mathf.Abs(scale.y) * .5f - radius);
            GetTriggerSegment(out Vector3 start, out Vector3 end, out float triggerRadius);
            float combinedRadius = radius + triggerRadius;
            return SegmentDistanceSquared(center - half, center + half, start, end) <=
                combinedRadius * combinedRadius;
        }

        private void GetTriggerSegment(out Vector3 start, out Vector3 end, out float radius)
        {
            Transform frame = activationTrigger.transform;
            Vector3 scale = frame.lossyScale;
            int axis = activationTrigger.direction;
            float axialScale = Mathf.Abs(scale[axis]);
            float radialScale = Mathf.Max(Mathf.Abs(scale[(axis + 1) % 3]), Mathf.Abs(scale[(axis + 2) % 3]));
            radius = activationTrigger.radius * radialScale;
            Vector3 direction = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            Vector3 half = frame.rotation * direction * Mathf.Max(0f, activationTrigger.height * axialScale * .5f - radius);
            Vector3 center = frame.TransformPoint(activationTrigger.center);
            start = center - half;
            end = center + half;
        }

        public static bool IsWithinExitVelocity(Vector3 velocity, float maximum) =>
            float.IsFinite(velocity.x) && float.IsFinite(velocity.y) && float.IsFinite(velocity.z) &&
            float.IsFinite(maximum) && maximum >= 0f && Mathf.Abs(velocity.x) <= maximum &&
            Mathf.Abs(velocity.y) <= maximum && Mathf.Abs(velocity.z) <= maximum;

        // Closest points of two bounded segments; handles parallel and degenerate
        // capsules without physics callbacks or per-frame query allocations.
        public static float SegmentDistanceSquared(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-8f && e <= 1e-8f) return r.sqrMagnitude;
            if (a <= 1e-8f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-8f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denominator = a * e - b * b;
                    s = denominator > 1e-8f ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return (p1 + d1 * s - p2 - d2 * t).sqrMagnitude;
        }
    }
}
