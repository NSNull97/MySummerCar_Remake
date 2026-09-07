using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Keeps the dynamic chassis mass aligned with the assembly graph. Loose
    /// parts retain their own Rigidbody mass. Installed kinematic presentation
    /// parts contribute mass to the chassis, while structural parts attached
    /// by a physical joint retain their mass in that connected body.
    /// </summary>
    [DefaultExecutionOrder(-65)]
    [DisallowMultipleComponent]
    public sealed class AssemblyChassisMassController : MonoBehaviour
    {
        [SerializeField] private Rigidbody chassis;
        [SerializeField] private VehicleAssemblyController assemblyController;
        [SerializeField, Min(0.01f)] private float bareChassisMassKilograms = 389f;
        [SerializeField] private Vector3 bareChassisCenterOfMass;

        private int appliedMutationCount = int.MinValue;
        private VehicleAssemblyController subscribedController;

        public float BareChassisMassKilograms => bareChassisMassKilograms;

        public void Configure(
            Rigidbody targetChassis,
            VehicleAssemblyController controller,
            float bareMassKilograms)
        {
            chassis = targetChassis;
            assemblyController = controller;
            bareChassisMassKilograms = Mathf.Max(0.01f, bareMassKilograms);
            bareChassisCenterOfMass = targetChassis != null
                ? targetChassis.centerOfMass
                : Vector3.zero;
            appliedMutationCount = int.MinValue;
            BindController();
            RefreshMass(force: true);
        }

        private void Awake()
        {
            BindController();
            RefreshMass(force: true);
        }

        private void OnEnable()
        {
            BindController();
        }

        private void OnDisable()
        {
            UnbindController();
        }

        private void FixedUpdate()
        {
            RefreshMass(force: false);
        }

        public void RefreshMass(bool force = false)
        {
            if (chassis == null || assemblyController == null)
            {
                return;
            }

            assemblyController.Initialize();
            if (!force &&
                appliedMutationCount == assemblyController.GraphMutationCount)
            {
                return;
            }

            float total = bareChassisMassKilograms;
            Vector3 weightedCenter =
                bareChassisCenterOfMass * bareChassisMassKilograms;
            PartInstance[] parts = assemblyController.Graph.AllRuntimeParts;
            for (int index = 0; index < parts.Length; index++)
            {
                PartInstance part = parts[index];
                if (part == null || part.IsAssemblyRoot || !part.IsInstalled ||
                    part.Definition == null || part.UsesDynamicInstalledPhysics ||
                    !HasInstalledPathToChassis(part))
                {
                    continue;
                }

                float partMass = Mathf.Max(
                    0f,
                    part.Definition.MassKilograms);
                total += partMass;
                Vector3 worldCenter = part.Body != null
                    ? part.Body.worldCenterOfMass
                    : part.transform.position;
                weightedCenter += chassis.transform.InverseTransformPoint(
                    worldCenter) * partMass;
            }

            chassis.mass = Mathf.Max(0.01f, total);
            chassis.centerOfMass = weightedCenter / chassis.mass;
            chassis.ResetInertiaTensor();
            if (!chassis.isKinematic)
            {
                chassis.WakeUp();
            }

            appliedMutationCount = assemblyController.GraphMutationCount;
        }

        private void BindController()
        {
            if (subscribedController == assemblyController)
            {
                return;
            }

            UnbindController();
            subscribedController = assemblyController;
            if (subscribedController != null)
            {
                subscribedController.ActionCompleted += HandleAssemblyAction;
            }
        }

        private void UnbindController()
        {
            if (subscribedController != null)
            {
                subscribedController.ActionCompleted -= HandleAssemblyAction;
            }

            subscribedController = null;
        }

        private void HandleAssemblyAction(AssemblyActionCompleted notification)
        {
            RefreshMass(force: true);
            if (chassis == null || chassis.isKinematic ||
                notification.Action != AssemblyActionKind.PartInstalled ||
                notification.TransferredMassKilograms <= 0f ||
                !HasInstalledPathToChassis(notification.Part))
            {
                return;
            }

            Vector3 momentum = notification.SourceLinearVelocity *
                notification.TransferredMassKilograms;
            if (momentum.sqrMagnitude > 0.000001f)
            {
                chassis.AddForceAtPosition(
                    momentum,
                    notification.WorldPosition,
                    ForceMode.Impulse);
            }
        }

        private bool HasInstalledPathToChassis(PartInstance part)
        {
            // Installed describes one occupied socket, including assemblies on
            // the floor. A retained child contributes to the car only while its
            // complete owner chain reaches this chassis. Evaluate on every
            // aggregate refresh: children's own Installed flags need not change
            // when their complete engine is installed or removed.
            PartInstance current = part;
            for (int depth = 0; depth < assemblyController.AllRuntimeParts.Length; depth++)
            {
                if (current == null)
                {
                    return false;
                }
                if (current.IsAssemblyRoot)
                {
                    return current.Body == chassis;
                }
                if (!current.IsInstalled)
                {
                    return false;
                }
                MountPointRuntime installedAt = assemblyController.Graph.FindMountForPart(current);
                if (installedAt?.Authoring == null)
                {
                    return false;
                }
                AssemblyOwnedMountAuthoring owner = installedAt.Authoring
                    .GetComponent<AssemblyOwnedMountAuthoring>();
                // Older chassis-owned mounts have no explicit loose-owner
                // component. Their authored pose is already bound to its logical
                // owner by VehicleAssemblyController, as in pose synchronization.
                current = owner != null ? owner.OwnerPart :
                    installedAt.Authoring.Pose.GetComponentInParent<PartInstance>();
            }
            return false;
        }
    }
}
