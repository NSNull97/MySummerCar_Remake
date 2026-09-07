using System;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    public enum SatsumaReciprocatingKind { Piston, Rockers, Valves, ValveScrew }

    [Serializable]
    public sealed class SatsumaReciprocatingVisualBinding
    {
        [SerializeField] private PartInstance owner;
        [SerializeField] private MeshFilter filter;
        [SerializeField] private SatsumaReciprocatingKind kind;
        [SerializeField] private int cylinder;
        [SerializeField] private int[] vertexGroups;
        [SerializeField] private Vector3 pivot;
        public PartInstance Owner => owner;
        public MeshFilter Filter => filter;
        public SatsumaReciprocatingKind Kind => kind;
        public int Cylinder => cylinder;
        public int[] VertexGroups => vertexGroups;
        public Vector3 Pivot => pivot;
        public SatsumaReciprocatingVisualBinding(PartInstance part, MeshFilter visual, SatsumaReciprocatingKind motion,
            int cylinderIndex, int[] groups, Vector3 partLocalPivot)
        { owner = part; filter = visual; kind = motion; cylinder = cylinderIndex; vertexGroups = groups; pivot = partLocalPivot; }

        public void Validate()
        {
            if (owner == null || filter == null || !SatsumaEngineVisualVibration.IsSafeVisualLeaf(filter.transform) ||
                !filter.transform.IsChildOf(owner.transform) || filter.sharedMesh == null || !filter.sharedMesh.isReadable ||
                vertexGroups == null || vertexGroups.Length != filter.sharedMesh.vertexCount || cylinder < 0 || cylinder > 3)
                throw new ArgumentException("Reciprocating motion requires a readable explicit visual mesh and complete vertex mapping.");
            foreach (int group in vertexGroups)
                if (group < -1 || group >= (kind == SatsumaReciprocatingKind.Piston ? 2 : 8))
                    throw new ArgumentException("Invalid mechanical vertex group.");
        }
    }

    /// <summary>
    /// Rigid motion of reviewed disconnected islands. Existing MeshRenderer,
    /// material, outline and transform bindings remain in place. Only an owned
    /// transient mesh is deformed; the shared imported mesh and collision are untouched.
    /// </summary>
    public sealed class SatsumaReciprocatingMesh : IDisposable
    {
        // Reviewed installed piston1/2 Z separation is .050017 m. This is a
        // donor-geometry fit, deliberately not the real A10/A12 specification.
        public const float CrankRadiusMeters = .0250085f;
        public const float RodLengthMeters = .13097f;
        public const float PistonPinZ = .005665f;
        // Bounded visible-lift calibration; not a transferred cam-lobe profile.
        public const float ValveLiftMeters = .007f;
        private readonly SatsumaReciprocatingVisualBinding binding;
        private readonly Mesh original;
        private Mesh owned;
        private readonly Vector3[] vertices, normals, outputVertices, outputNormals;
        private readonly Vector4[] tangents, outputTangents;
        private readonly Matrix4x4[] groupMatrices = new Matrix4x4[8];
        private readonly Quaternion[] groupRotations = new Quaternion[8];
        public Mesh OwnedMesh => owned;

        public SatsumaReciprocatingMesh(SatsumaReciprocatingVisualBinding source)
        {
            source.Validate(); binding = source; original = source.Filter.sharedMesh;
            vertices = original.vertices; normals = original.normals; tangents = original.tangents;
            outputVertices = new Vector3[vertices.Length]; outputNormals = new Vector3[normals.Length];
            outputTangents = new Vector4[tangents.Length];
            owned = UnityEngine.Object.Instantiate(original); owned.name = "Project-owned transient mechanical motion";
            owned.hideFlags = HideFlags.HideAndDontSave; owned.MarkDynamic();
            Bounds bounds = original.bounds; bounds.Expand(.14f); owned.bounds = bounds;
            source.Filter.sharedMesh = owned;
        }

        public static float PistonDisplacement(float crankDegrees, int cylinder)
        {
            float offset = cylinder == 0 || cylinder == 3 ? 0f : 180f;
            float angle = (crankDegrees + offset) * Mathf.Deg2Rad;
            float sideways = CrankRadiusMeters * Mathf.Sin(angle);
            return CrankRadiusMeters * Mathf.Cos(angle) + Mathf.Sqrt(RodLengthMeters * RodLengthMeters - sideways * sideways) -
                (RodLengthMeters + (offset == 0f ? CrankRadiusMeters : -CrankRadiusMeters));
        }
        public static float RodAngle(float crankDegrees, int cylinder)
        {
            float offset = cylinder == 0 || cylinder == 3 ? 0f : 180f;
            return Mathf.Asin(-CrankRadiusMeters * Mathf.Sin((crankDegrees + offset) * Mathf.Deg2Rad) / RodLengthMeters) * Mathf.Rad2Deg;
        }
        public static float ValveLift01(float crankCycleDegrees, int valve)
        {
            // Four-stroke 1-3-4-2. Exhaust peak at 270, intake at 450 degrees
            // after the cylinder's firing TDC; smooth, bounded 240-degree lobes.
            int cylinder = valve / 2;
            float firing = cylinder == 0 ? 0f : cylinder == 1 ? 540f : cylinder == 2 ? 180f : 360f;
            float cycle = Mathf.Repeat(crankCycleDegrees - firing, 720f);
            float distance = Mathf.Abs(cycle - ((valve & 1) == 0 ? 450f : 270f));
            if (distance >= 120f) return 0f;
            float sine = Mathf.Cos(distance / 120f * Mathf.PI * .5f); return sine * sine;
        }

        public void Apply(float crankDegrees, float valveCycleDegrees)
        {
            if (owned == null || binding.Filter == null || binding.Filter.sharedMesh != owned) return;
            Matrix4x4 toPart = binding.Owner.transform.worldToLocalMatrix * binding.Filter.transform.localToWorldMatrix;
            Matrix4x4 fromPart = toPart.inverse;
            Quaternion meshToPartRotation = Quaternion.Inverse(binding.Owner.transform.rotation) * binding.Filter.transform.rotation;
            for (int group = 0; group < 8; group++)
            {
                Quaternion rotation = Quaternion.identity;
                Vector3 translation = Vector3.zero, pivot = binding.Pivot;
                if (binding.Kind == SatsumaReciprocatingKind.Piston)
                {
                    translation = Vector3.forward * PistonDisplacement(crankDegrees, binding.Cylinder);
                    if (group == 1) rotation = Quaternion.AngleAxis(RodAngle(crankDegrees, binding.Cylinder), Vector3.right);
                }
                else
                {
                    float lift = ValveLift01(valveCycleDegrees, group);
                    if (binding.Kind == SatsumaReciprocatingKind.Valves) translation = Vector3.back * (lift * ValveLiftMeters);
                    else rotation = Quaternion.AngleAxis(lift * 10.5f, Vector3.right);
                }
                groupMatrices[group] = fromPart * Matrix4x4.TRS(pivot + translation, rotation, Vector3.one) *
                    Matrix4x4.Translate(-pivot) * toPart;
                groupRotations[group] = Quaternion.Inverse(meshToPartRotation) * rotation * meshToPartRotation;
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                int group = binding.VertexGroups[i];
                outputVertices[i] = group < 0 ? vertices[i] : groupMatrices[group].MultiplyPoint3x4(vertices[i]);
                if (i < normals.Length) outputNormals[i] = group < 0 ? normals[i] : groupRotations[group] * normals[i];
                if (i < tangents.Length)
                {
                    Vector4 tangent = tangents[i];
                    Vector3 direction = group < 0 ? (Vector3)tangent : groupRotations[group] * (Vector3)tangent;
                    outputTangents[i] = new Vector4(direction.x, direction.y, direction.z, tangent.w);
                }
            }
            owned.SetVertices(outputVertices);
            if (outputNormals.Length > 0) owned.SetNormals(outputNormals);
            if (outputTangents.Length > 0) owned.SetTangents(outputTangents);
        }

        public void Dispose()
        {
            if (owned == null) return;
            if (binding.Filter != null && binding.Filter.sharedMesh == owned) binding.Filter.sharedMesh = original;
            if (Application.isPlaying) UnityEngine.Object.Destroy(owned); else UnityEngine.Object.DestroyImmediate(owned);
            owned = null;
        }
    }
}
