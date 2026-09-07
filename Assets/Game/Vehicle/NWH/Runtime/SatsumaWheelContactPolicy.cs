using System.Threading;
using Unity.Collections;
using UnityEngine;

namespace MSC.Vehicle.NWH
{
    // Satsuma-only integration of auxiliary wheel shapes with the existing
    // suspension force owner. No scene lookup or NWH vendor modification.
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class SatsumaWheelContactPolicy : MonoBehaviour
    {
        [SerializeField] private NwhWheelPhysicsBackend backend;
        private readonly Shape[] shapes = new Shape[4];
        private readonly object sync = new object();
        private int seen, changed;
        private Vector3 diagnosticPoint, diagnosticNormal;
        private float diagnosticTravel;
        private bool diagnosticGrounded;
        public string ContactDiagnostics { get { lock (sync) return "point=" + diagnosticPoint + " normal=" + diagnosticNormal + " travel=" + diagnosticTravel + " grounded=" + diagnosticGrounded; } }
        public int SeenContacts => Volatile.Read(ref seen);
        public int ChangedContacts => Volatile.Read(ref changed);
        public NwhWheelPhysicsBackend Backend => backend;
        public void Configure(NwhWheelPhysicsBackend value) { backend = value; Capture(); }
        private struct Shape
        {
            public EntityId Id;
            public float Radius, Travel, EdgeImpulseLimit;
            public bool Grounded;
        }
        private void OnEnable()
        {
            Physics.ContactModifyEvent += Modify;
            Physics.ContactModifyEventCCD += Modify;
        }
        private void OnDisable()
        {
            Physics.ContactModifyEvent -= Modify;
            Physics.ContactModifyEventCCD -= Modify;
        }
        private void FixedUpdate() => Capture();
        private void Capture()
        {
            if (backend == null) return;
            lock (sync)
                for (int i = 0; i < shapes.Length && i < backend.Wheels.Length; i++)
                {
                    var wheel = backend.Wheels[i];
                    if (wheel == null) { shapes[i] = default; continue; }
                    var collider = wheel.wheel.meshCollider;
                    shapes[i] = new Shape { Id = collider != null ? collider.GetEntityId() : default,
                        Radius = collider != null ? wheel.Radius : 0, Travel = wheel.SpringLength, Grounded = wheel.IsGrounded,
                        EdgeImpulseLimit = BottomedEdgeImpulseLimit(wheel.SpringMaxForce, Time.fixedDeltaTime) };
                }
        }
        private void Modify(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
        {
            lock (sync)
            foreach (var pair in pairs)
            foreach (var shape in shapes)
            {
                if (shape.Radius <= 0) continue;
                bool first = pair.colliderEntityId == shape.Id;
                if (!first && pair.otherColliderEntityId != shape.Id) continue;
                Quaternion rotation = first ? pair.rotation : pair.otherRotation;
                Vector3 centre = first ? pair.position : pair.otherPosition;
                Quaternion inverse = Quaternion.Inverse(rotation);
                for (int i = 0; i < pair.contactCount; i++)
                {
                    Interlocked.Increment(ref seen);
                    Vector3 local = inverse * (pair.GetPoint(i) - centre);
                    Vector3 normal = inverse * pair.GetNormal(i);
                    if (Mathf.Abs(normal.z) > .3f)
                    {
                        diagnosticPoint = local; diagnosticNormal = normal;
                        diagnosticTravel = shape.Travel; diagnosticGrounded = shape.Grounded;
                    }
                    if (!HasLowerTreadEdgeContact(shape.Grounded, shape.Radius, local, normal)) continue;
                    if (shape.Travel <= .001f)
                    {
                        // A bottomed spring keeps its vertical solid stop. The
                        // lower fore/aft tire edge must not introduce an
                        // effectively infinite stiffness in the travel direction.
                        // Bound that edge by the authored spring force's impulse
                        // budget for this step; do not turn it into a launch.
                        pair.SetMaxImpulse(i, Mathf.Min(pair.GetMaxImpulse(i), shape.EdgeImpulseLimit));
                        Interlocked.Increment(ref changed);
                        continue;
                    }
                    // The raycast spring already owns support here. Rotating
                    // this impulse upward would turn a rail-edge brake into an
                    // artificial launch impulse at one corner of the chassis.
                    pair.IgnoreContact(i);
                    Interlocked.Increment(ref changed);
                }
            }
        }

        public static bool HasCompliantLowerTreadContact(bool grounded, float remainingTravel,
            float radius, Vector3 localPoint, Vector3 localNormal)
        {
            // Millimetre numerical guard, not a new suspension bottom-out stage.
            // At the real stop the solid collider remains authoritative. While
            // travel remains, NWH's ground/spring force owns the lower tread.
            return remainingTravel > .001f && HasLowerTreadEdgeContact(grounded, radius, localPoint, localNormal);
        }

        private static bool HasLowerTreadEdgeContact(bool grounded, float radius, Vector3 localPoint, Vector3 localNormal)
        {
            return grounded && radius > 0 &&
                localPoint.y < -radius * .35f && Mathf.Abs(localNormal.x) <= .3f &&
                Mathf.Abs(localNormal.z) >= .3f;
        }

        public static float BottomedEdgeImpulseLimit(float authoredSpringForceNewtons, float fixedStepSeconds) =>
            float.IsFinite(authoredSpringForceNewtons) && authoredSpringForceNewtons > 0 &&
            float.IsFinite(fixedStepSeconds) && fixedStepSeconds > 0
                ? authoredSpringForceNewtons * fixedStepSeconds
                : float.PositiveInfinity;
    }
}
