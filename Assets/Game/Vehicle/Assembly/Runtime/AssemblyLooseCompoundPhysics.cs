using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [Serializable]
    public sealed class AssemblyCompoundShapeBinding
    {
        [SerializeField] private PartInstance part;
        [SerializeField] private Collider[] shapes = Array.Empty<Collider>();
        [SerializeField] private Vector3 ownCenterOfMass;
        public PartInstance Part => part;
        public Collider[] Shapes => shapes ?? Array.Empty<Collider>();
        public Vector3 OwnCenterOfMass => ownCenterOfMass;
        public AssemblyCompoundShapeBinding(PartInstance owner, Collider[] ownedShapes)
        {
            part = owner;
            shapes = ownedShapes ?? Array.Empty<Collider>();
            // Authored before runtime installation disables the original solids.
            // Never capture a body's already-accumulated compound center at startup.
            ownCenterOfMass = owner != null && owner.Body != null ? owner.Body.centerOfMass : Vector3.zero;
        }
    }

    /// <summary>
    /// Opt-in assembly physics for authored loose engine parts. Nested installed
    /// Rigidbodies remain kinematic/query-only; their solid shapes are represented
    /// on the actual outer loose Rigidbody, never as separate kinematic obstacles.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class AssemblyLooseCompoundPhysics : MonoBehaviour
    {
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private AssemblyCompoundShapeBinding[] bindings =
            Array.Empty<AssemblyCompoundShapeBinding>();
        private readonly List<AssemblyCompoundShapeBinding> runtimeBindings =
            new List<AssemblyCompoundShapeBinding>();
        private AssemblyCompoundShapeBinding[] allBindings = Array.Empty<AssemblyCompoundShapeBinding>();
        private readonly List<ShapeProxy> proxies = new List<ShapeProxy>();
        private readonly Dictionary<PartInstance, float> masses = new Dictionary<PartInstance, float>();
        private readonly Dictionary<PartInstance, Vector3> centers = new Dictionary<PartInstance, Vector3>();
        private readonly Dictionary<PartInstance, Vector3> ownCenters = new Dictionary<PartInstance, Vector3>();
        private readonly Dictionary<Transform, PartInstance> boundPartTransforms =
            new Dictionary<Transform, PartInstance>();
        private readonly Dictionary<PartInstance, PartInstance> looseOwners = new Dictionary<PartInstance, PartInstance>();
        private bool initialized;
        private int lastMutation = int.MinValue;
        private VehicleAssemblyController subscribed;

        public VehicleAssemblyController Assembly => assembly;
        public AssemblyCompoundShapeBinding[] Bindings => bindings;
        public int ActiveProxyCount { get; private set; }

        public void Configure(VehicleAssemblyController controller, AssemblyCompoundShapeBinding[] shapes)
        {
            if (initialized) throw new InvalidOperationException("Cannot reconfigure active compound physics.");
            assembly = controller ?? throw new ArgumentNullException(nameof(controller));
            bindings = shapes ?? throw new ArgumentNullException(nameof(shapes));
            RebuildBindingCache();
            Bind();
        }

        public bool HasRuntimeBinding(PartInstance part)
        {
            for (int index = 0; index < runtimeBindings.Count; index++)
                if (runtimeBindings[index].Part == part) return true;
            return false;
        }

        /// <summary>
        /// Extends physical ownership after graph registration. Authored bindings,
        /// existing proxies and previously captured own centers remain unchanged.
        /// </summary>
        public bool TryRegisterRuntimeBinding(AssemblyCompoundShapeBinding binding, out string error)
        {
            error = string.Empty;
            if (assembly == null)
            {
                error = "Compound physics has no assembly controller.";
                return false;
            }
            assembly.Initialize();
            RebuildBindingCache();
            var proposed = new AssemblyCompoundShapeBinding[allBindings.Length + 1];
            Array.Copy(allBindings, proposed, allBindings.Length);
            proposed[allBindings.Length] = binding;
            try
            {
                ValidateBindings(proposed);
            }
            catch (InvalidOperationException exception)
            {
                error = exception.Message;
                return false;
            }
            if (!initialized) InitializeProxies();
            AddBindingProxies(binding);
            runtimeBindings.Add(binding);
            allBindings = proposed;
            if (isActiveAndEnabled) Refresh(true);
            return true;
        }

        /// <summary>Remove only a detached runtime binding before graph unregistration.</summary>
        public bool TryUnregisterRuntimeBinding(PartInstance part, out string error)
        {
            error = string.Empty;
            if (part == null || !HasRuntimeBinding(part))
            {
                error = "No runtime compound binding exists for this part.";
                return false;
            }
            if (part.IsInstalled)
            {
                error = "Detach the part before removing its compound binding.";
                return false;
            }
            foreach (MountPointRuntime mount in assembly.Graph.Mounts)
            {
                if (mount != null && mount.IsOccupied && mount.Authoring != null &&
                    mount.Authoring.GetComponent<AssemblyOwnedMountAuthoring>()?.OwnerPart == part)
                {
                    error = "Detach owned children before removing their compound owner.";
                    return false;
                }
            }
            for (int index = proxies.Count - 1; index >= 0; index--)
            {
                if (proxies[index].Part != part) continue;
                DestroyProxy(proxies[index]);
                proxies.RemoveAt(index);
            }
            if (part.Body != null && part.Definition != null && ownCenters.TryGetValue(part, out Vector3 center))
            {
                part.Body.mass = part.Definition.MassKilograms;
                part.Body.centerOfMass = center;
                part.Body.ResetInertiaTensor();
            }
            ownCenters.Remove(part);
            boundPartTransforms.Remove(part.transform);
            looseOwners.Remove(part);
            masses.Remove(part);
            centers.Remove(part);
            for (int index = runtimeBindings.Count - 1; index >= 0; index--)
                if (runtimeBindings[index].Part == part) runtimeBindings.RemoveAt(index);
            RebuildBindingCache();
            if (isActiveAndEnabled) Refresh(true);
            return true;
        }

        /// <summary>Refresh a resized presentation collider without changing graph or physical ownership.</summary>
        public bool TryRefreshRuntimeBindingShapes(PartInstance part, out string error)
        {
            error = string.Empty;
            if (part == null || !HasRuntimeBinding(part))
            {
                error = "No runtime compound binding exists for this part.";
                return false;
            }
            try
            {
                ValidateBindings(allBindings);
            }
            catch (InvalidOperationException exception)
            {
                error = exception.Message;
                return false;
            }
            foreach (ShapeProxy proxy in proxies)
                if (proxy.Part == part) CopyShapeProperties(proxy.Source, proxy.Shape);
            if (isActiveAndEnabled) Refresh(true);
            return true;
        }

        private void RebuildBindingCache()
        {
            allBindings = new AssemblyCompoundShapeBinding[bindings.Length + runtimeBindings.Count];
            Array.Copy(bindings, allBindings, bindings.Length);
            runtimeBindings.CopyTo(allBindings, bindings.Length);
        }

        private void OnEnable() => Bind();
        private void Start() => Refresh(true);
        private void FixedUpdate() => Refresh();

        private void Bind()
        {
            if (subscribed == assembly) return;
            if (subscribed != null) subscribed.ActionCompleted -= OnAssemblyAction;
            subscribed = assembly;
            if (subscribed != null) subscribed.ActionCompleted += OnAssemblyAction;
        }

        private void OnAssemblyAction(AssemblyActionCompleted action)
        {
            if (isActiveAndEnabled) Refresh(true);
        }

        public void Refresh(bool force = false)
        {
            if (assembly == null) return;
            Bind();
            assembly.Initialize();
            if (!initialized) InitializeProxies();
            // Force/manual refresh and installation events may occur before the
            // controller's next FixedUpdate. Nested kinematic bodies must first
            // agree with their mount poses, including engine-to-car handoff.
            assembly.SynchronizeInstalledParts();
            bool changed = force || lastMutation != assembly.GraphMutationCount;
            masses.Clear();
            centers.Clear();
            looseOwners.Clear();
            foreach (AssemblyCompoundShapeBinding binding in allBindings)
            {
                PartInstance part = binding.Part;
                if (part == null || part.Definition == null || part.Body == null) continue;
                PartInstance owner = ResolveLooseOwner(part);
                looseOwners[part] = owner;
                if (owner == null) continue;
                // Definitions contain own-part mass, never a prior aggregate.
                float mass = part.Definition.MassKilograms;
                masses.TryGetValue(owner, out float total);
                centers.TryGetValue(owner, out Vector3 center);
                masses[owner] = total + mass;
                GetRelativePose(part.transform, owner.transform, out Matrix4x4 relative, out Quaternion rotation);
                // Rigidbody.centerOfMass uses rotation/translation but not scale.
                // Compose in the owner's frame, never subtract large world
                // positions: moving a rigid engine must not move its own CoM.
                Vector3 relativeCenter = Vector3.Scale(owner.transform.lossyScale,
                    relative.MultiplyPoint3x4(Vector3.zero)) + rotation * ownCenters[part];
                centers[owner] = center + relativeCenter * mass;
            }

            ActiveProxyCount = 0;
            foreach (ShapeProxy proxy in proxies)
            {
                if (proxy.Shape == null) continue;
                looseOwners.TryGetValue(proxy.Part, out PartInstance owner);
                bool enabled = owner != null && owner != proxy.Part && proxy.Source != null &&
                    proxy.Source.gameObject.activeInHierarchy;
                if (!enabled)
                {
                    if (proxy.Shape.gameObject.activeSelf)
                    {
                        proxy.Shape.gameObject.SetActive(false);
                        changed = true;
                    }
                    proxy.Marker.SetLooseOwner(null);
                    continue;
                }
                Transform pose = proxy.Shape.transform;
                Transform parent = owner.Body.transform;
                if (pose.parent != parent) { pose.SetParent(parent, false); changed = true; }
                GetRelativePose(proxy.Source.transform, parent,
                    out Matrix4x4 sourceToOwner, out Quaternion localRotation);
                Vector3 localPosition = sourceToOwner.MultiplyPoint3x4(Vector3.zero);
                Vector3 localScale = sourceToOwner.lossyScale;
                if (!ValidScale(parent.lossyScale) || !ValidScale(localScale))
                    throw new InvalidOperationException("Compound collider has a degenerate or non-finite scale.");
                if ((pose.localPosition - localPosition).sqrMagnitude > 1e-12f ||
                    Quaternion.Angle(pose.localRotation, localRotation) > .0001f ||
                    (pose.localScale - localScale).sqrMagnitude > 1e-12f)
                {
                    pose.SetLocalPositionAndRotation(localPosition, localRotation);
                    pose.localScale = localScale;
                    changed = true;
                }
                proxy.Shape.enabled = true;
                if (!proxy.Shape.gameObject.activeSelf) { proxy.Shape.gameObject.SetActive(true); changed = true; }
                proxy.Marker.SetLooseOwner(owner);
                ActiveProxyCount++;
            }
            foreach (AssemblyCompoundShapeBinding binding in allBindings)
            {
                PartInstance part = binding.Part;
                if (part == null || part.Body == null || part.Definition == null) continue;
                bool compound = masses.TryGetValue(part, out float mass);
                if (!compound) mass = part.Definition.MassKilograms;
                Vector3 center = compound ? centers[part] / mass : ownCenters[part];
                if (Mathf.Abs(part.Body.mass - mass) > .00001f ||
                    (part.Body.centerOfMass - center).sqrMagnitude > 1e-12f)
                {
                    part.Body.mass = mass;
                    part.Body.centerOfMass = center;
                    changed = true;
                }
            }
            lastMutation = assembly.GraphMutationCount;
            if (changed)
            {
                foreach (PartInstance owner in masses.Keys)
                {
                    owner.Body.ResetInertiaTensor();
                    if (!owner.Body.isKinematic) owner.Body.WakeUp();
                }
            }
        }

        private void GetRelativePose(Transform source, Transform owner,
            out Matrix4x4 matrix, out Quaternion rotation)
        {
            matrix = Matrix4x4.identity;
            rotation = Quaternion.identity;
            for (Transform current = source; current != owner; current = current.parent)
            {
                if (current == null)
                    throw new InvalidOperationException("Compound source is outside its explicit loose owner's hierarchy.");
                Vector3 localPosition = current.localPosition;
                Quaternion localRotation = current.localRotation;
                if (boundPartTransforms.TryGetValue(current, out PartInstance part) &&
                    part.IsInstalled && !part.UsesDynamicInstalledPhysics &&
                    current.parent == part.InstalledPose)
                {
                    // PartInstance synchronizes a kinematic child using world
                    // Rigidbody/Transform poses. At home-sized coordinates the
                    // round trip can leave tens of micrometres in its local pose.
                    // Its exact contract is position/rotation identity at the
                    // occupied socket. Preserve scale and all real mount and
                    // presentation transforms; do not propagate that roundoff
                    // into moving PhysX shapes and repeated inertia resets.
                    localPosition = Vector3.zero;
                    localRotation = Quaternion.identity;
                }
                matrix = Matrix4x4.TRS(localPosition, localRotation, current.localScale) * matrix;
                rotation = localRotation * rotation;
            }
        }

        private PartInstance ResolveLooseOwner(PartInstance part)
        {
            for (int depth = 0; depth < assembly.AllRuntimeParts.Length; depth++)
            {
                if (part == null || part.IsAssemblyRoot || part.UsesDynamicInstalledPhysics ||
                    part.Body == null || !ownCenters.ContainsKey(part)) return null;
                if (!part.IsInstalled) return part;
                MountPointRuntime mount = assembly.Graph.FindMountForPart(part);
                AssemblyOwnedMountAuthoring owner = mount?.Authoring != null
                    ? mount.Authoring.GetComponent<AssemblyOwnedMountAuthoring>() : null;
                if (owner == null || owner.OwnerPart == part) return null;
                part = owner.OwnerPart;
            }
            return null;
        }

        private void InitializeProxies()
        {
            RebuildBindingCache();
            ValidateBindings(allBindings);
            // Validation is complete before populating caches or allocating contacts.
            foreach (AssemblyCompoundShapeBinding binding in allBindings)
                AddBindingProxies(binding);
            initialized = true;
        }

        private void ValidateBindings(AssemblyCompoundShapeBinding[] candidates)
        {
            var parts = new HashSet<PartInstance>();
            var shapes = new HashSet<Collider>();
            foreach (AssemblyCompoundShapeBinding binding in candidates)
            {
                if (binding?.Part == null || binding.Part.Body == null || binding.Part.Definition == null ||
                    binding.Part.IsAssemblyRoot || Array.IndexOf(assembly.AllRuntimeParts, binding.Part) < 0 ||
                    !parts.Add(binding.Part) || !IsFinite(binding.OwnCenterOfMass) ||
                    !float.IsFinite(binding.Part.Definition.MassKilograms) || binding.Part.Definition.MassKilograms <= 0f)
                    throw new InvalidOperationException("Invalid or duplicate explicit compound part.");
                foreach (Collider source in binding.Shapes)
                {
                    if (source == null || source.isTrigger || !shapes.Add(source) ||
                        source.GetComponentInParent<PartInstance>(includeInactive: true) != binding.Part ||
                        // Initialization may follow a save restore/install which
                        // has already disabled the original solid. Validate its
                        // explicit owner, including an inactive aggregate waiting
                        // for restore, not a possibly absent PhysX attachment.
                        source.GetComponentInParent<Rigidbody>(includeInactive: true) != binding.Part.Body ||
                        source.GetComponent<AssemblyCompoundColliderProxy>() != null ||
                        !(source is BoxCollider || source is SphereCollider || source is CapsuleCollider ||
                          source is MeshCollider mesh && mesh.convex && mesh.sharedMesh != null))
                        throw new InvalidOperationException("Invalid explicit convex compound shape.");
                }
            }
        }

        private void AddBindingProxies(AssemblyCompoundShapeBinding binding)
        {
            int previousProxyCount = proxies.Count;
            GameObject pendingContact = null;
            try
            {
                ownCenters.Add(binding.Part, binding.OwnCenterOfMass);
                boundPartTransforms.Add(binding.Part.transform, binding.Part);
                foreach (Collider source in binding.Shapes)
                {
                    var go = new GameObject("Assembly compound contact");
                    pendingContact = go;
                    go.SetActive(false);
                    go.hideFlags = HideFlags.DontSave;
                    go.transform.SetParent(transform, false);
                    go.layer = source.gameObject.layer;
                    AssemblyCompoundColliderProxy marker = go.AddComponent<AssemblyCompoundColliderProxy>();
                    marker.Configure(binding.Part, source);
                    Collider shape = CopyShape(source, go);
                    shape.enabled = false;
                    proxies.Add(new ShapeProxy(binding.Part, source, shape, marker));
                    pendingContact = null;
                }
            }
            catch
            {
                if (pendingContact != null)
                {
                    pendingContact.SetActive(false);
                    if (Application.isPlaying) Destroy(pendingContact);
                    else DestroyImmediate(pendingContact);
                }
                for (int index = proxies.Count - 1; index >= previousProxyCount; index--)
                {
                    DestroyProxy(proxies[index]);
                    proxies.RemoveAt(index);
                }
                ownCenters.Remove(binding.Part);
                boundPartTransforms.Remove(binding.Part.transform);
                throw;
            }
        }

        private static Collider CopyShape(Collider source, GameObject destination)
        {
            Collider copy;
            switch (source)
            {
                case BoxCollider box:
                    copy = destination.AddComponent<BoxCollider>();
                    break;
                case SphereCollider sphere:
                    copy = destination.AddComponent<SphereCollider>();
                    break;
                case CapsuleCollider capsule:
                    copy = destination.AddComponent<CapsuleCollider>();
                    break;
                case MeshCollider mesh when mesh.convex && mesh.sharedMesh != null:
                    copy = destination.AddComponent<MeshCollider>();
                    break;
                default:
                    throw new InvalidOperationException("Compound physics requires a supported convex shape.");
            }
            CopyShapeProperties(source, copy);
            return copy;
        }

        private static void CopyShapeProperties(Collider source, Collider destination)
        {
            switch (source)
            {
                case BoxCollider box when destination is BoxCollider boxCopy:
                    boxCopy.center = box.center; boxCopy.size = box.size;
                    break;
                case SphereCollider sphere when destination is SphereCollider sphereCopy:
                    sphereCopy.center = sphere.center; sphereCopy.radius = sphere.radius;
                    break;
                case CapsuleCollider capsule when destination is CapsuleCollider capsuleCopy:
                    capsuleCopy.center = capsule.center; capsuleCopy.radius = capsule.radius;
                    capsuleCopy.height = capsule.height; capsuleCopy.direction = capsule.direction;
                    break;
                case MeshCollider mesh when destination is MeshCollider meshCopy:
                    meshCopy.cookingOptions = mesh.cookingOptions;
                    meshCopy.convex = true; meshCopy.sharedMesh = mesh.sharedMesh;
                    break;
                default:
                    throw new InvalidOperationException("Compound source and proxy shape types differ.");
            }
            destination.sharedMaterial = source.sharedMaterial;
            destination.contactOffset = source.contactOffset;
        }

        private static void DestroyProxy(ShapeProxy proxy)
        {
            if (proxy.Shape == null) return;
            proxy.Shape.gameObject.SetActive(false);
            proxy.Marker.SetLooseOwner(null);
            if (Application.isPlaying) Destroy(proxy.Shape.gameObject);
            else DestroyImmediate(proxy.Shape.gameObject);
        }

        private void OnDisable()
        {
            if (subscribed != null) subscribed.ActionCompleted -= OnAssemblyAction;
            subscribed = null;
            foreach (ShapeProxy proxy in proxies)
                if (proxy.Shape != null)
                {
                    proxy.Shape.gameObject.SetActive(false);
                    proxy.Marker.SetLooseOwner(null);
                }
            foreach (KeyValuePair<PartInstance, Vector3> entry in ownCenters)
            {
                if (entry.Key == null || entry.Key.Body == null || entry.Key.Definition == null) continue;
                entry.Key.Body.mass = entry.Key.Definition.MassKilograms;
                entry.Key.Body.centerOfMass = entry.Value;
            }
            lastMutation = int.MinValue;
            ActiveProxyCount = 0;
        }

        private void OnDestroy()
        {
            foreach (ShapeProxy proxy in proxies)
                DestroyProxy(proxy);
        }

        private sealed class ShapeProxy
        {
            public readonly PartInstance Part;
            public readonly Collider Source;
            public readonly Collider Shape;
            public readonly AssemblyCompoundColliderProxy Marker;
            public ShapeProxy(PartInstance part, Collider source, Collider shape, AssemblyCompoundColliderProxy marker)
            { Part = part; Source = source; Shape = shape; Marker = marker; }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static bool ValidScale(Vector3 value) => IsFinite(value) &&
            Mathf.Abs(value.x) > 1e-6f && Mathf.Abs(value.y) > 1e-6f && Mathf.Abs(value.z) > 1e-6f;
    }
}
