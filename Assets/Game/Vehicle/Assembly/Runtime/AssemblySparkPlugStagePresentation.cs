using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>The plug itself turns; its persistent wrapper and rigidbody remain at the socket.</summary>
    [DisallowMultipleComponent]
    public sealed class AssemblySparkPlugStagePresentation : MonoBehaviour
    {
        private VehicleAssemblyController assembly;
        private PartInstance part;
        private Transform visual;
        private Vector3 restPosition;
        private Quaternion restRotation;
        public Transform Visual => visual;

        public void Configure(VehicleAssemblyController configuredAssembly, PartInstance configuredPart, Transform configuredVisual)
        {
            if (configuredAssembly == null || configuredPart == null || configuredVisual == null ||
                configuredPart.Definition == null ||
                configuredPart.Definition.DefinitionId != SatsumaConsumableAssemblyRules.SparkPlugPartId ||
                configuredVisual.parent != configuredPart.transform)
                throw new ArgumentException("Spark-plug stage presentation needs the direct item visual root.");
            if (visual == configuredVisual && part == configuredPart && assembly == configuredAssembly)
            {
                RefreshPresentation();
                return;
            }
            RestoreRest();
            assembly = configuredAssembly;
            part = configuredPart;
            visual = configuredVisual;
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            if (visual == null || part == null || assembly == null) return;
            int stage = 0;
            if (part.IsInstalled && SatsumaConsumableAssemblyRules.IsSparkPlugMount(part.RuntimeState.InstalledMountId) &&
                assembly.Graph.TryGetMount(part.RuntimeState.InstalledMountId, out MountPointRuntime mount) &&
                mount.InstalledPart == part && mount.Fasteners.Length == 1)
                stage = mount.Fasteners[0].Stage;
            // sparkplug0 Screw states 0..8: Self Z=-.0025*stage,
            // Self EulerZ=45*stage. No extra bolt geometry or physical body.
            visual.localPosition = Vector3.back * (0.0025f * stage) + restPosition;
            visual.localRotation = Quaternion.AngleAxis(45f * stage, Vector3.forward) * restRotation;
        }

        private void LateUpdate() => RefreshPresentation();
        private void OnDisable() => RestoreRest();
        private void RestoreRest()
        {
            if (visual == null) return;
            visual.localPosition = restPosition;
            visual.localRotation = restRotation;
        }
    }
}
