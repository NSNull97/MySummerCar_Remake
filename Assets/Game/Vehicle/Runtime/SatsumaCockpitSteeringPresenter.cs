using System;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    [Serializable]
    public sealed class SatsumaCockpitSteeringBinding
    {
        [SerializeField] private PartInstance owner;
        [SerializeField] private Transform leaf;
        [SerializeField] private PartInstance alternativeOwner;
        public PartInstance Owner => owner;
        public Transform Leaf => leaf;
        public PartInstance AlternativeOwner => alternativeOwner;
        public bool IsInstalled => owner != null && owner.IsInstalled || alternativeOwner != null && alternativeOwner.IsInstalled;
        [NonSerialized] internal Vector3 BasePosition;
        [NonSerialized] internal Quaternion BaseRotation;
        [NonSerialized] internal bool Applied;
        public SatsumaCockpitSteeringBinding(PartInstance part, Transform visual, PartInstance alternate = null)
        { owner = part; leaf = visual; alternativeOwner = alternate; }
    }

    /// <summary>Donor-evidenced raw input x450 degrees. Render-only; no mount, collider or save pose rotates.</summary>
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class SatsumaCockpitSteeringPresenter : MonoBehaviour
    {
        public const float MaximumAngleDegrees = 450f;
        [SerializeField] private VehicleInputRouter input;
        [SerializeField] private Vector3 localPivot;
        [SerializeField] private Vector3 localAxis = Vector3.forward;
        [SerializeField] private SatsumaCockpitSteeringBinding[] bindings = Array.Empty<SatsumaCockpitSteeringBinding>();
        public SatsumaCockpitSteeringBinding[] Bindings => bindings;
        public Vector3 LocalPivot => localPivot;
        public Vector3 LocalAxis => localAxis;
        public float AppliedAngleDegrees { get; private set; }

        public void Configure(VehicleInputRouter router, Vector3 pivot, Vector3 axis, SatsumaCockpitSteeringBinding[] visuals)
        {
            if (router == null || visuals == null || visuals.Length == 0 || axis.sqrMagnitude < .99f)
                throw new ArgumentException("Explicit cockpit steering input, pivot, axis and visual bindings required.");
            foreach (var binding in visuals)
                if (binding == null || binding.Owner == null || !SatsumaEngineVisualVibration.IsSafeVisualLeaf(binding.Leaf))
                    throw new ArgumentException("Cockpit steering must never drive an assembly/physics transform.");
            RestoreVisuals(); input = router; localPivot = pivot; localAxis = axis.normalized; bindings = visuals;
        }
        private void Update() => RestoreVisuals();
        private void LateUpdate() => ApplyFrame(input != null ? input.SteeringInputMinusOneToOne : 0f);
        public void ApplyFrame(float steering)
        {
            RestoreVisuals();
            if (!float.IsFinite(steering)) return;
            AppliedAngleDegrees = Mathf.Clamp(steering,-1f,1f) * MaximumAngleDegrees;
            Quaternion rotation = Quaternion.AngleAxis(AppliedAngleDegrees,transform.TransformDirection(localAxis));
            Vector3 pivot = transform.TransformPoint(localPivot);
            foreach (var binding in bindings)
            {
                if (binding?.Leaf == null || !binding.IsInstalled) continue;
                Transform leaf = binding.Leaf;
                binding.BasePosition = leaf.localPosition; binding.BaseRotation = leaf.localRotation; binding.Applied = true;
                leaf.SetPositionAndRotation(pivot + rotation * (leaf.position-pivot),rotation*leaf.rotation);
            }
        }
        public void RestoreVisuals()
        {
            foreach (var binding in bindings)
            {
                if (binding == null || !binding.Applied) continue;
                if (binding.Leaf != null) binding.Leaf.SetLocalPositionAndRotation(binding.BasePosition,binding.BaseRotation);
                binding.Applied = false;
            }
            AppliedAngleDegrees = 0f;
        }
        private void OnDisable() => RestoreVisuals();
    }
}
