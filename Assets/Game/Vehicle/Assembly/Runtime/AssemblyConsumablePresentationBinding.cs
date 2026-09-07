using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>Explicit aggregate-side presentation callback; never owns item identity or colliders.</summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyConsumablePresentationBinding : MonoBehaviour, IAssemblyItemPartPresentationBinding
    {
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private GameObject installedBeltPresentation;
        public VehicleAssemblyController Assembly => assembly;
        public GameObject InstalledBeltPresentation => installedBeltPresentation;

        public void ConfigureBeltPresentation(GameObject template)
        {
            if (template == null || template.GetComponentInChildren<SkinnedMeshRenderer>(true) == null ||
                template.GetComponentsInChildren<Collider>(true).Length != 0 ||
                template.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                template.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new ArgumentException("The installed belt must be a reviewed presentation-only rig.");
            installedBeltPresentation = template;
        }

        public void Configure(VehicleAssemblyController configuredAssembly)
        {
            if (configuredAssembly == null) throw new ArgumentNullException(nameof(configuredAssembly));
            assembly = configuredAssembly;
        }

        public void BindPartPresentation(PartInstance part, Transform visualRoot)
        {
            if (part == null || part.Definition == null) return;
            if (part.Definition.DefinitionId == SatsumaConsumableAssemblyRules.BeltPartId)
            {
                if (installedBeltPresentation == null)
                    throw new InvalidOperationException("The purchased belt has no reviewed mounted presentation.");
                var belt = part.GetComponent<AssemblyAlternatorBeltPresentation>();
                if (belt == null) belt = part.gameObject.AddComponent<AssemblyAlternatorBeltPresentation>();
                belt.Configure(assembly, part, visualRoot, installedBeltPresentation);
                return;
            }
            if (part.Definition.DefinitionId != SatsumaConsumableAssemblyRules.SparkPlugPartId) return;
            if (assembly == null || visualRoot == null || visualRoot == part.transform ||
                !visualRoot.IsChildOf(part.transform))
                throw new InvalidOperationException("A spark-plug binding requires the item's existing visual child.");
            AssemblySparkPlugStagePresentation binding = part.GetComponent<AssemblySparkPlugStagePresentation>();
            if (binding == null) binding = part.gameObject.AddComponent<AssemblySparkPlugStagePresentation>();
            binding.Configure(assembly, part, visualRoot);
        }
    }
}
