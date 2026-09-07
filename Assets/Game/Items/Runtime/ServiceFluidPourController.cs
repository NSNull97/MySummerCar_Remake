using System;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Items
{
    public readonly struct ServiceFluidPourProfile
    {
        public ServiceFluidPourProfile(Vector3 outlet, float rate, float speed)
        { LocalOutlet = outlet; LitresPerSecond = rate; OutletSpeedMetersPerSecond = speed; }
        public Vector3 LocalOutlet { get; }
        public float LitresPerSecond { get; }
        public float OutletSpeedMetersPerSecond { get; }
    }

    /// <summary>Gravity stream from three reviewed service cans; petrol and sauna are separate systems.</summary>
    [DisallowMultipleComponent]
    public sealed class ServiceFluidPourController : MonoBehaviour
    {
        private const float TransferIntervalSeconds = .1f;
        private const float SegmentSeconds = .05f;
        private const int SegmentCount = 14;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Collider[] overlaps = new Collider[32];
        private WorldItemInstance item;
        private ServiceFluidPourProfile profile;
        private float pendingSeconds;
        public bool IsConfigured => item != null;
        public bool IsPouring { get; private set; }
        public float Flow01 { get; private set; }
        public Vector3 OutletWorldPosition => transform.TransformPoint(profile.LocalOutlet);
        public Vector3 OutletWorldDirection => transform.forward;
        public float OutletSpeedMetersPerSecond => profile.OutletSpeedMetersPerSecond;
        public Vector3 LastSpillPosition { get; private set; }
        public bool LastStreamHitSurface { get; private set; }

        public static bool TryGetProfile(string definitionId, out ServiceFluidPourProfile result)
        {
            // Frozen *0.prefab fluid_particle offsets. All three mouths face local +Z.
            // Discharge rates come from the matching CapTrigger_* receivers.
            result = definitionId switch
            {
                "item.motor-oil" => new(new(-.00055918f, -.06413196f, .19270726f), .1f, .2f),
                "item.coolant" => new(new(-.000027501277f, .07163249f, .11214107f), .2f, .45f),
                "item.brake-fluid" => new(new(-.00004186542f, .000037354956f, .08690715f), .1f, .35f),
                _ => default,
            };
            return result.LitresPerSecond > 0f;
        }
        public void Configure(WorldItemInstance owner)
        {
            if (item != null) throw new InvalidOperationException("Service pour is already configured.");
            if (owner == null || owner.gameObject != gameObject || !TryGetProfile(owner.DefinitionId, out profile))
                throw new ArgumentException("Service pour requires one of the three reviewed item wrappers.");
            item = owner;
            item.StateRestored += HandleRestore;
        }
        public static float EvaluateFlow(float mouthUpDot, float fillRatio)
        {
            if (!float.IsFinite(mouthUpDot) || !float.IsFinite(fillRatio) || fillRatio <= 0f) return 0f;
            // Orientation-independent adaptation of the donor Euler-angle check.
            // A nearly empty can must be tipped further. No liquid leaves an upright mouth.
            float start = Mathf.Lerp(-.05f, .34f, Mathf.Clamp01(fillRatio));
            if (mouthUpDot >= start) return 0f;
            return Mathf.Lerp(.25f, 1f, Mathf.Clamp01((start - mouthUpDot) / (start + 1f)));
        }

        public void Tick(float elapsedSeconds)
        {
            if (item == null || !isActiveAndEnabled || !item.IsOpen || item.IsConsumed || item.IsBroken ||
                !float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f || item.LiquidAmountLitres <= .0001f)
            { ResetFlow(); return; }
            Flow01 = EvaluateFlow(Vector3.Dot(OutletWorldDirection, Vector3.up),
                item.LiquidAmountLitres / Mathf.Max(.0001f, item.Definition.MaximumContent));
            IsPouring = Flow01 > 0f;
            if (!IsPouring) { pendingSeconds = 0f; return; }
            // Bound a resumed/hitched frame; do not spill seconds of accumulated pause time.
            pendingSeconds += Mathf.Min(elapsedSeconds, TransferIntervalSeconds);
            if (pendingSeconds < TransferIntervalSeconds - .00001f) return;
            float offered = Mathf.Min(item.LiquidAmountLitres, profile.LitresPerSecond * Flow01 * pendingSeconds);
            pendingSeconds = 0f;
            ILiquidContainerTarget receiver = TraceStream(out Vector3 impact, out bool hitSurface);
            LastSpillPosition = impact; LastStreamHitSurface = hitSurface;
            float transferred = 0f;
            if (receiver != null) item.TryPourLiquidTo(receiver, offered, out transferred);
            float missed = Mathf.Max(0f, offered - transferred);
            if (missed > .000001f) item.TrySpillLiquid(missed, out _);
            if (item.LiquidAmountLitres <= .0001f) ResetFlow();
        }

        /// <summary>Bounded ballistic segment casts; first solid surface wins, never a receiver through a wall.</summary>
        public ILiquidContainerTarget TraceStream(out Vector3 impact, out bool hitSurface)
        {
            Vector3 origin = OutletWorldPosition;
            Vector3 velocity = OutletWorldDirection * profile.OutletSpeedMetersPerSecond;
            Vector3 previous = origin; impact = origin; hitSurface = false;
            // SphereCast does not report shapes containing its origin. A dipped
            // mouth may start inside a receiver, but never pour through a solid.
            int overlapCount = Physics.OverlapSphereNonAlloc(origin, .003f, overlaps, ~0, QueryTriggerInteraction.Collide);
            if (overlapCount == overlaps.Length) { hitSurface = true; return null; }
            ILiquidContainerTarget overlappingReceiver = null;
            for (int i = 0; i < overlapCount; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                ILiquidContainerTarget candidate = collider.GetComponent<ILiquidContainerTarget>();
                if (candidate == null && collider.isTrigger) continue;
                hitSurface = true;
                if (candidate == null || overlappingReceiver != null && !ReferenceEquals(overlappingReceiver, candidate)) return null;
                overlappingReceiver = candidate;
            }
            if (overlappingReceiver != null) return overlappingReceiver;
            for (int segment = 1; segment <= SegmentCount; segment++)
            {
                float time = segment * SegmentSeconds;
                Vector3 next = origin + velocity * time + .5f * Physics.gravity * time * time;
                Vector3 delta = next - previous; float length = delta.magnitude;
                if (length <= .00001f) continue;
                int count = Physics.SphereCastNonAlloc(previous, .003f, delta / length, hits, length, ~0, QueryTriggerInteraction.Collide);
                // A saturated query is ambiguous: stop instead of filling through an omitted obstacle.
                if (count == hits.Length) { impact = previous; hitSurface = true; return null; }
                float nearest = float.PositiveInfinity; ILiquidContainerTarget receiver = null; bool found = false;
                for (int i = 0; i < count; i++)
                {
                    Collider collider = hits[i].collider;
                    if (collider == null || collider.transform.IsChildOf(transform)) continue;
                    ILiquidContainerTarget candidate = collider.GetComponent<ILiquidContainerTarget>();
                    if (collider.isTrigger && candidate == null || hits[i].distance >= nearest) continue;
                    nearest = hits[i].distance; receiver = candidate; impact = hits[i].point; found = true;
                }
                if (found) { hitSurface = true; return receiver; }
                previous = next;
            }
            impact = previous; return null;
        }
        private void Update() => Tick(Time.deltaTime);
        private void HandleRestore(WorldItemInstance _) => ResetFlow();
        private void OnDestroy() { if (item != null) item.StateRestored -= HandleRestore; }
        private void OnDisable() => ResetFlow();
        private void ResetFlow() { IsPouring = false; Flow01 = 0f; pendingSeconds = 0f; }
    }
}
