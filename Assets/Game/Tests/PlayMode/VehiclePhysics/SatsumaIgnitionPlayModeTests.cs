using System.Collections;
using System.Linq;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaIgnitionPlayModeTests
    {
        private GameObject instance;
        private PartInstance[] ownedParts;
        private float oldTimeScale;

        [SetUp]
        public void PausePhysicsNotRealtime()
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (ownedParts != null)
                foreach (PartInstance part in ownedParts)
                    if (part != null && (instance == null || !part.transform.IsChildOf(instance.transform)))
                        Object.Destroy(part.gameObject);
            if (instance != null) Object.Destroy(instance);
            yield return null;
            yield return null;
            Time.timeScale = oldTimeScale;
        }

        [UnityTest]
        public IEnumerator PhysicalLmbUsesRealtimeAndSaveDuringStartRestoresOnlyAccessory()
        {
            CreateFixture();
            InstallHistoricalColumn();
            var binding = instance.GetComponent<VehiclePersistenceBinding>();
            var access = new SatsumaKeyAccessState();
            binding.BindKeyAccess(access);
            var controller = binding.IgnitionController;
            var target = instance.GetComponentsInChildren<SatsumaIgnitionInteractionTarget>(true).Single();
            var context = default(InteractionContext);
            yield return null;
            Assert.That(controller.ColumnInstalled, Is.True);
            Assert.That(controller.CanOperate, Is.True, "Column Installed is sufficient; its two bolts remain at zero.");
            Assert.That(target.GetComponent<Collider>().enabled, Is.True);
            Assert.That(target.CanBeginContinuousInteraction(context, ContinuousContextInteractionDirection.Secondary), Is.False);

            target.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
            Assert.That(controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(controller.VisibleKey.activeInHierarchy, Is.True);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(target.ContinueContinuousInteraction(0f), Is.True);
            Assert.That(controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            yield return new WaitForSecondsRealtime(0.23f);
            Assert.That(target.ContinueContinuousInteraction(0f), Is.True);
            Assert.That(controller.State, Is.EqualTo(SatsumaIgnitionState.Starting));
            Assert.That(Quaternion.Angle(controller.KeyPivot.localRotation,
                Quaternion.Euler(0f, -60f, 0f)), Is.LessThan(0.001f));
            Assert.That(controller.StarterRequested, Is.False, "An unpowered lock rotates but cannot crank.");

            Assert.That(binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            Assert.That(record.ignitionOn, Is.True);
            Assert.That(binding.TryRestore(record, out failure), Is.True, failure);
            Assert.That(controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(controller.IsHeld, Is.False);
            Assert.That(controller.HasAttemptedStart, Is.False, "The local gesture latch is not persistent.");
            Assert.That(controller.StarterRequested, Is.False);
            target.EndContinuousInteraction();
            Assert.That(controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(instance.GetComponent<VehicleInputRouter>().enabled, Is.False);

            target.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
            target.EndContinuousInteraction();
            Assert.That(controller.State, Is.EqualTo(SatsumaIgnitionState.Off));
            Assert.That(controller.VisibleKey.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator KeyLossAndTargetDisableCancelStartWithoutRewritingAccessory()
        {
            CreateFixture();
            InstallHistoricalColumn();
            var binding = instance.GetComponent<VehiclePersistenceBinding>();
            var access = new SatsumaKeyAccessState();
            binding.BindKeyAccess(access);
            var controller = binding.IgnitionController;
            var adapter = instance.GetComponent<SatsumaIgnitionInputAdapter>();
            var target = instance.GetComponentsInChildren<SatsumaIgnitionInteractionTarget>(true).Single();
            var context = default(InteractionContext);
            yield return null;
            target.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
            yield return new WaitForSecondsRealtime(0.42f);
            target.ContinueContinuousInteraction(0f);
            Assert.That(controller.Starting, Is.True);
            access.SetAccess(false);
            Assert.That(adapter.ConsumeFixedInput(0).StarterRequested, Is.False);
            Assert.That(controller.Starting, Is.False, "Fixed input catches access loss without waiting for the next Update.");
            Assert.That(controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(controller.IsHeld, Is.False);
            yield return null;
            Assert.That(target.GetComponent<Collider>().enabled, Is.False);
            Assert.That(controller.InstalledPresentation.activeInHierarchy, Is.True,
                "A missing logical key does not erase the installed socket.");

            access.SetAccess(true);
            controller.RestorePersistentState(false);
            yield return null;
            target.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
            yield return new WaitForSecondsRealtime(0.42f);
            target.ContinueContinuousInteraction(0f);
            Assert.That(controller.Starting, Is.True);
            target.enabled = false;
            Assert.That(controller.IsHeld, Is.False);
            Assert.That(controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(target.GetComponent<Collider>().enabled, Is.False);
            Assert.That(adapter.ConsumeFixedInput(0).StarterRequested, Is.False);
            target.enabled = true;
            Assert.That(controller.IsHeld, Is.False);
        }

        private void CreateFixture()
        {
            GameObject prefab = Resources.Load<GameObject>("Phase1Vehicles/Satsuma_Phase1_V1a");
            Assert.That(prefab, Is.Not.Null);
            instance = Object.Instantiate(prefab, new Vector3(0f, 80f, 0f), Quaternion.identity);
            var assembly = instance.GetComponent<VehicleAssemblyController>();
            ownedParts = assembly.Parts.ToArray();
            assembly.Initialize();
            instance.GetComponent<Rigidbody>().useGravity = false;
            instance.GetComponent<VehicleSimulationHost>().enabled = false;
            Assert.That(instance.GetComponent<VehicleSimulationHost>().TryInitialize(out string failure), Is.True, failure);
            Assert.That(instance.GetComponent<VehiclePersistenceBinding>().IgnitionController, Is.Not.Null);
        }

        private void InstallHistoricalColumn()
        {
            var assembly = instance.GetComponent<VehicleAssemblyController>();
            VehicleAssemblySaveData data = assembly.CaptureSaveData();
            MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                value.MountId == SatsumaIgnitionController.ColumnMountId);
            PartSaveDto column = data.parts.Single(value =>
                value.partDefinitionId == SatsumaIgnitionController.ColumnPartDefinitionId);
            column.lifecycleState = PartLifecycleState.Installed;
            column.installedMountId = mount.MountId;
            column.worldPosition = mount.Pose.position;
            column.worldRotation = mount.Pose.rotation;
            data.mounts.Single(value => value.mountId == mount.MountId).installedPartStableEntityId = column.stableEntityId;
            foreach (FastenerSaveDto fastener in data.fasteners.Where(value => value.mountId == mount.MountId))
            {
                fastener.inserted = true;
                fastener.seated = true;
                fastener.stage = 0;
            }
            data.fastenerGroups.Single(value => value.mountId == mount.MountId).isBolted = false;
            AssemblyOperationResult restored = assembly.RestoreSaveData(data);
            Assert.That(restored.Succeeded, Is.True, restored.Message);
        }
    }
}
