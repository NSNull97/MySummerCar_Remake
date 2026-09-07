using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Items;
using MSC.Vehicle.Assembly;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Vehicle.ItemsIntegration
{
    /// <summary>One physical item wrapper, with Items state and assembly-owned transforms.</summary>
    [DisallowMultipleComponent]
    public sealed class VehicleItemAssemblyBridge : MonoBehaviour, IItemExternalPhysicsOwner
    {
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private VehicleItemPartCatalog catalog;
        private ItemWorldRuntime runtime;
        private AssemblyLooseCompoundPhysics compoundPhysics;
        private IAssemblyItemPartPresentationBinding[] aggregatePresentationBindings =
            Array.Empty<IAssemblyItemPartPresentationBinding>();

        public VehicleAssemblyController Assembly => assembly;
        public VehicleItemPartCatalog Catalog => catalog;
        public ItemWorldRuntime Runtime => runtime;

        public void Configure(VehicleAssemblyController configuredAssembly, VehicleItemPartCatalog configuredCatalog)
        {
            if (configuredAssembly == null) throw new ArgumentNullException(nameof(configuredAssembly));
            if (configuredCatalog == null || !configuredCatalog.TryValidate(out string failure))
                throw new ArgumentException("A reviewed item/part catalog is required.", nameof(configuredCatalog));
            if (runtime != null && assembly != configuredAssembly)
                throw new InvalidOperationException("Cannot move a live item bridge to another aggregate.");
            assembly = configuredAssembly;
            catalog = configuredCatalog;
            assembly.ConfigureDynamicPartDefinitions(catalog.GetPartDefinitions());
            compoundPhysics = assembly.GetComponent<AssemblyLooseCompoundPhysics>();
            CacheAggregatePresentationBindings();
        }

        public void BindRuntime(ItemWorldRuntime configuredRuntime)
        {
            if (configuredRuntime == null || !configuredRuntime.IsInitialized)
                throw new ArgumentException("The bridge requires an initialized item runtime.", nameof(configuredRuntime));
            if (assembly == null || catalog == null)
                throw new InvalidOperationException("Configure the aggregate and catalog before binding items.");
            if (runtime != null && runtime != configuredRuntime)
                throw new InvalidOperationException("Cannot replace a live bridge's item runtime.");
            // Serialized authoring survives a prefab load; these runtime caches do not.
            assembly.ConfigureDynamicPartDefinitions(catalog.GetPartDefinitions());
            compoundPhysics = assembly.GetComponent<AssemblyLooseCompoundPhysics>();
            CacheAggregatePresentationBindings();
            runtime = configuredRuntime;
            runtime.RegisterExternalPhysicsOwner(this);
            ReconcileBindings();
        }

        public bool TryResolveDefinition(string itemDefinitionId, out PartDefinition definition)
        {
            if (catalog != null) return catalog.TryResolve(itemDefinitionId, out definition);
            definition = null;
            return false;
        }

        public bool OwnsDefinition(string itemDefinitionId) => TryResolveDefinition(itemDefinitionId, out _);

        public void ReconcileBindings() => ReconcileBindings(null);

        public void ReconcileBindings(IReadOnlyCollection<string> instanceIds)
        {
            if (runtime == null) return;
            HashSet<string> selected = instanceIds != null
                ? new HashSet<string>(instanceIds, StringComparer.Ordinal) : null;
            foreach (WorldItemInstance item in runtime.LoadedInstances.ToArray())
            {
                if (item == null || item.State.isConsumed || !OwnsDefinition(item.Definition.DefinitionId) ||
                    selected != null && !selected.Contains(item.StableId.Value)) continue;
                BindMaterialized(item);
                GameObject visual = item.PresentationRoot;
                if (visual != null) ReconcilePresentation(item, visual);
            }
        }

        public void BindMaterialized(WorldItemInstance item)
        {
            if (item == null || item.State.isConsumed ||
                !TryResolveDefinition(item.Definition.DefinitionId, out PartDefinition definition)) return;
            StableEntityIdAuthoring identity = item.GetComponent<StableEntityIdAuthoring>();
            Rigidbody body = item.GetComponent<Rigidbody>();
            PhysicsPickupTarget pickup = item.GetComponent<PhysicsPickupTarget>();
            if (identity == null || body == null || pickup == null)
                throw new InvalidOperationException("An assembly item must retain its identity, body and pickup wrapper.");
            ItemPartPresentationBinding condition = item.GetComponent<ItemPartPresentationBinding>();
            if (condition == null) condition = item.gameObject.AddComponent<ItemPartPresentationBinding>();
            condition.BindCondition(item);
            PartInstance part = item.GetComponent<PartInstance>();
            if (part == null)
            {
                part = item.gameObject.AddComponent<PartInstance>();
                part.Configure(definition, identity, body, pickup, false, string.Empty);
            }
            else if (part.Definition != definition || part.StableId != item.StableId || part.Body != body || part.PickupTarget != pickup)
                throw new InvalidOperationException("Existing item/part wrapper bindings disagree.");

            if (assembly.Graph.TryGetPartByStableId(item.StableId.Value, out PartInstance registered))
            {
                if (registered != part) throw new InvalidOperationException("The item ID is registered by another physical part.");
            }
            else if (!assembly.TryRegisterDynamicPart(part, item.Definition.DefinitionId, out string failure))
                throw new InvalidOperationException("Cannot bind the purchased assembly part: " + failure);

            ConfigureInteractionCapabilities(part);
            runtime?.RetainExternalOwnership(item);
            // Source cell is provenance. The aggregate retains even loose replacement wrappers.
            if (item.transform.parent == null && item.gameObject.scene != assembly.gameObject.scene)
                SceneManager.MoveGameObjectToScene(item.gameObject, assembly.gameObject.scene);
        }

        public void ReconcilePresentation(WorldItemInstance item, GameObject presentationRoot)
        {
            if (item == null || presentationRoot == null || !OwnsDefinition(item.Definition.DefinitionId)) return;
            PartInstance part = item.GetComponent<PartInstance>();
            if (part == null) return;
            // Aggregate presenters own stage/rest-pose handling. The bridge passes
            // the existing visual and never resets it on repeated reconciliation.
            foreach (IAssemblyItemPartPresentationBinding presenter in aggregatePresentationBindings)
                if (!(presenter is UnityEngine.Object component) || component != null)
                    presenter.BindPartPresentation(part, presentationRoot.transform);
            Bounds presentationBounds = ConfigureInstalledInteractionProxy(part, presentationRoot, item.Definition.ProxySize);
            if (!item.Definition.HasAuthoredColliderShapes &&
                presentationRoot.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            {
                // Generic item fitting runs before this callback and trusts the
                // imported renderer AABB. Reuse the baked pose for the default
                // physical box as well, before compound shapes are captured.
                BoxCollider box = item.GetComponent<BoxCollider>();
                if (box != null && !box.isTrigger && box.attachedRigidbody == part.Body)
                {
                    box.center = presentationBounds.center;
                    box.size = Vector3.Max(Vector3.one * .02f, presentationBounds.size);
                }
            }
            if (compoundPhysics != null)
            {
                bool hadBinding = compoundPhysics.HasRuntimeBinding(part);
                string failure;
                // The item runtime has already fitted the final wrapper collider.
                // Capture own center of mass here, before the first installation.
                bool ready = hadBinding
                    ? compoundPhysics.TryRefreshRuntimeBindingShapes(part, out failure)
                    : compoundPhysics.TryRegisterRuntimeBinding(new AssemblyCompoundShapeBinding(part,
                        item.GetComponents<Collider>().Where(shape => !shape.isTrigger &&
                            shape.attachedRigidbody == part.Body).ToArray()), out failure);
                if (!ready) throw new InvalidOperationException("Cannot bind the item compound shape: " + failure);
            }
            if (item.Definition.DefinitionId != "item.oil-filter") return;
            ItemPartPresentationBinding binding = item.GetComponent<ItemPartPresentationBinding>();
            if (binding == null) binding = item.gameObject.AddComponent<ItemPartPresentationBinding>();
            if (binding.PresentationRoot == presentationRoot) return;
            binding.Configure(part, assembly, presentationRoot);
        }

        public bool TryReleaseInstance(WorldItemInstance item, out string failure)
        {
            failure = string.Empty;
            if (item == null || !OwnsDefinition(item.Definition.DefinitionId)) return true;
            PartInstance part = item.GetComponent<PartInstance>();
            if (part == null) return true;
            if (part.IsInstalled || part.IsAssemblyRoot)
            {
                failure = "Detach the item through assembly before removing its wrapper.";
                return false;
            }
            if (compoundPhysics != null && compoundPhysics.HasRuntimeBinding(part) &&
                !compoundPhysics.TryUnregisterRuntimeBinding(part, out failure)) return false;
            if (assembly.Graph.TryGetPartByStableId(part.StableId.Value, out PartInstance registered))
            {
                if (registered != part) { failure = "The item identity belongs to another part."; return false; }
                return assembly.TryUnregisterDynamicPart(part, out failure);
            }
            return true;
        }

        private void ConfigureInteractionCapabilities(PartInstance part)
        {
            InteractionTargetHost host = part.GetComponent<InteractionTargetHost>();
            if (host == null) throw new InvalidOperationException("The item pickup host is missing.");
            host.AddCapabilityFirst(part);
            AssemblySurfaceMountHandoffTarget handoff = part.GetComponent<AssemblySurfaceMountHandoffTarget>();
            if (handoff == null) handoff = part.gameObject.AddComponent<AssemblySurfaceMountHandoffTarget>();
            handoff.Configure(assembly, part);
            host.AddCapabilityFirst(handoff);
            AssemblyInstalledPartInteractionTarget removal = part.GetComponent<AssemblyInstalledPartInteractionTarget>();
            if (removal == null) removal = part.gameObject.AddComponent<AssemblyInstalledPartInteractionTarget>();
            removal.Configure(assembly, part);
            host.AddCapabilityFirst(removal);
            AssemblySubassemblyPickupTarget pickup = part.GetComponent<AssemblySubassemblyPickupTarget>();
            if (pickup == null) pickup = part.gameObject.AddComponent<AssemblySubassemblyPickupTarget>();
            pickup.Configure(assembly, part);
            host.AddCapabilityFirst(pickup);
            host.ConfigureSelectionPriority(15);
        }

        private void CacheAggregatePresentationBindings()
        {
            aggregatePresentationBindings = assembly.GetComponents<MonoBehaviour>()
                .OfType<IAssemblyItemPartPresentationBinding>().ToArray();
        }

        private static Bounds ConfigureInstalledInteractionProxy(PartInstance part, GameObject visual, Vector3 fallbackSize)
        {
            bool hasBounds = ItemPartInteractionBounds.TryGetPartLocalBounds(
                part.transform, visual, out Bounds bounds);
            if (!hasBounds) bounds = new Bounds(Vector3.zero, fallbackSize);
            AssemblyInstalledPartInteractionProxy proxy = part.GetComponentInChildren<AssemblyInstalledPartInteractionProxy>(true);
            if (proxy == null)
            {
                var target = new GameObject("Project-owned installed item interaction");
                target.layer = part.gameObject.layer;
                target.transform.SetParent(part.transform, false);
                proxy = target.AddComponent<AssemblyInstalledPartInteractionProxy>();
            }
            // Match the existing generated part proxy padding; solids stay excluded when installed.
            proxy.Configure(part, bounds.center, bounds.size + Vector3.one * .035f);
            return bounds;
        }

        private void OnDestroy()
        {
            if (runtime != null) runtime.UnregisterExternalPhysicsOwner(this);
        }
    }

}
