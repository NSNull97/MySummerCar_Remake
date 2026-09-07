using System;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    [Serializable]
    public sealed class SatsumaFlexibleConnectionBinding
    {
        [SerializeField] private PartInstance owner, neighbour;
        [SerializeField] private MeshFilter filter;
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 endCenter, targetPoint;
        [SerializeField] private float rigidRadius, fadeRadius;
        public PartInstance Owner => owner;
        public PartInstance Neighbour => neighbour;
        public MeshFilter Filter => filter;
        public Transform Target => target;
        public Vector3 EndCenter => endCenter;
        public Vector3 TargetPoint => targetPoint;
        public float RigidRadius => rigidRadius;
        public float FadeRadius => fadeRadius;
        public SatsumaFlexibleConnectionBinding(PartInstance part, PartInstance other, MeshFilter mesh,
            Transform anchor, Vector3 end, Vector3 connection, float rigid, float fade)
        {
            owner = part; neighbour = other; filter = mesh; target = anchor; endCenter = end;
            targetPoint = connection; rigidRadius = rigid; fadeRadius = fade; Validate();
        }
        public void Validate()
        {
            if (owner == null || neighbour == null || filter == null || target == null || filter.sharedMesh == null ||
                filter.GetComponent<Collider>() != null || filter.GetComponent<Rigidbody>() != null ||
                filter.GetComponent<PartInstance>() != null || rigidRadius <= 0f || fadeRadius <= rigidRadius ||
                !float.IsFinite(fadeRadius) || !float.IsFinite(endCenter.sqrMagnitude) || !float.IsFinite(targetPoint.sqrMagnitude))
                throw new ArgumentException("A flexible connection requires explicit render-only mesh and reviewed end anchors.");
        }
    }

    /// <summary>Small presentation-only end corrections after engine vibration.
    /// Source meshes, assembly roots, physical hose connections and saves stay authoritative.</summary>
    [DefaultExecutionOrder(225), DisallowMultipleComponent]
    public sealed class SatsumaFlexibleConnectionPresenter : MonoBehaviour
    {
        [SerializeField] private SatsumaFlexibleConnectionBinding[] bindings = Array.Empty<SatsumaFlexibleConnectionBinding>();
        private FlexibleMesh[] meshes = Array.Empty<FlexibleMesh>();
        public SatsumaFlexibleConnectionBinding[] Bindings => bindings;
        public int OutOfRangeConnections { get; private set; }

        public void Configure(SatsumaFlexibleConnectionBinding[] connections)
        {
            if (connections == null) throw new ArgumentNullException(nameof(connections));
            foreach (var binding in connections) binding.Validate();
            ResetPresentation(); bindings = (SatsumaFlexibleConnectionBinding[])connections.Clone();
        }
        private void LateUpdate() => RefreshOutputs();
        public void RefreshOutputs()
        {
            if (meshes.Length != bindings.Length) meshes = new FlexibleMesh[bindings.Length];
            OutOfRangeConnections = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                bool attached = b.Owner != null && b.Neighbour != null && b.Owner.IsInstalled && b.Neighbour.IsInstalled;
                if (!attached || b.Filter == null || b.Target == null)
                { meshes[i]?.Dispose(); meshes[i] = null; continue; }
                Vector3 delta = b.Filter.transform.InverseTransformPoint(b.Target.TransformPoint(b.TargetPoint)) - b.EndCenter;
                // A removed engine can retain locally installed children. Never
                // stretch a still-installed body hose across the whole workshop.
                if (b.Filter.transform.TransformVector(delta).sqrMagnitude > .075f * .075f)
                { meshes[i]?.Dispose(); meshes[i] = null; OutOfRangeConnections++; continue; }
                if (!b.Filter.gameObject.activeInHierarchy) continue;
                meshes[i] ??= new FlexibleMesh(b);
                meshes[i].Apply(delta);
            }
        }
        public void ResetPresentation()
        { foreach (var mesh in meshes) mesh?.Dispose(); meshes = Array.Empty<FlexibleMesh>(); OutOfRangeConnections = 0; }
        private void OnDisable() => ResetPresentation();
        private void OnDestroy() => ResetPresentation();

        private sealed class FlexibleMesh : IDisposable
        {
            private readonly SatsumaFlexibleConnectionBinding binding;
            private readonly Mesh original, owned;
            private readonly Vector3[] vertices, normals, output, outputNormals, gradients;
            private readonly float[] weights;
            private bool disposed;
            public FlexibleMesh(SatsumaFlexibleConnectionBinding source)
            {
                binding = source; original = source.Filter.sharedMesh;
                vertices = original.vertices; normals = original.normals;
                output = new Vector3[vertices.Length]; outputNormals = new Vector3[normals.Length];
                weights = new float[vertices.Length]; gradients = new Vector3[vertices.Length];
                float span = source.FadeRadius - source.RigidRadius;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 offset = vertices[i] - source.EndCenter; float distance = offset.magnitude;
                    float t = Mathf.Clamp01((distance - source.RigidRadius) / span);
                    weights[i] = 1f - t * t * (3f - 2f * t);
                    gradients[i] = distance > 1e-6f ? offset / distance * (-6f * t * (1f - t) / span) : Vector3.zero;
                }
                owned = Instantiate(original); owned.name = "Project-owned transient flexible connection";
                owned.hideFlags = HideFlags.HideAndDontSave; owned.MarkDynamic();
                Bounds bounds = original.bounds; bounds.Expand(.15f); owned.bounds = bounds;
                source.Filter.sharedMesh = owned;
            }
            public void Apply(Vector3 delta)
            {
                if (disposed || binding.Filter == null || binding.Filter.sharedMesh != owned) return;
                for (int i = 0; i < vertices.Length; i++)
                {
                    output[i] = vertices[i] + delta * weights[i];
                    // Inverse-transpose of I + delta * gradient(weight), keeping
                    // tube shading coherent without per-frame topology/GC work.
                    if (i < normals.Length)
                        outputNormals[i] = (normals[i] - gradients[i] *
                            (Vector3.Dot(delta, normals[i]) / Mathf.Max(.1f, 1f + Vector3.Dot(gradients[i], delta)))).normalized;
                }
                owned.SetVertices(output); if (normals.Length > 0) owned.SetNormals(outputNormals);
            }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                if (binding.Filter != null && binding.Filter.sharedMesh == owned) binding.Filter.sharedMesh = original;
                if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned);
            }
        }
    }
}
