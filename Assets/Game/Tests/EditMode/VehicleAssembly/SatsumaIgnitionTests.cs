using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaIgnitionTests
    {
        private Fixture fixture;

        [SetUp]
        public void SetUp()
        {
            fixture = new Fixture();
        }

        [TearDown]
        public void TearDown()
        {
            fixture?.Dispose();
            fixture = null;
        }

        [Test]
        public void KeyGestureFeedbackDoesNotReplayDuringRestoreOrAvailabilityRefresh()
        {
            var requests = new List<bool>();
            fixture.Controller.KeySoundRequested += requests.Add;
            Assert.That(fixture.Controller.TryBeginPrimaryHold(1d), Is.False);
            Assert.That(requests, Is.Empty);
            fixture.InstallColumn();
            fixture.Controller.RestorePersistentState(true);
            fixture.Controller.RefreshAvailability();
            fixture.Controller.enabled = false;
            fixture.Controller.enabled = true;
            Assert.That(requests, Is.Empty);
            Assert.That(fixture.Controller.TryBeginPrimaryHold(2d), Is.True);
            Assert.That(fixture.Controller.TryBeginPrimaryHold(2.01d), Is.False);
            fixture.Controller.ReleasePrimaryHold(2.1d);
            Assert.That(requests, Is.EqualTo(new[] { true, false }));
            fixture.Controller.RestorePersistentState(false);
            fixture.Controller.RefreshAvailability();
            Assert.That(requests, Has.Count.EqualTo(2));
        }

        [Test]
        public void InstalledButUnboltedColumnAndOwnedKeyExposePhysicalLock()
        {
            Assert.That(fixture.Controller.ColumnInstalled, Is.False);
            Assert.That(fixture.Controller.CanOperate, Is.False);
            Assert.That(fixture.Presentation.activeSelf, Is.False);
            Assert.That(fixture.InteractionCollider.enabled, Is.False);

            fixture.InstallColumn();
            fixture.RefreshTarget();

            Assert.That(fixture.ColumnMount.IsOccupied, Is.True);
            Assert.That(fixture.ColumnMount.FastenerGroup.IsBolted, Is.False,
                "The donor ignition lock requires the column to be installed, not tightened.");
            Assert.That(fixture.Controller.ColumnInstalled, Is.True);
            Assert.That(fixture.Controller.CanOperate, Is.True);
            Assert.That(fixture.Presentation.activeSelf, Is.True);
            Assert.That(fixture.InteractionCollider.enabled, Is.True);
        }

        [Test]
        public void PhysicalTargetIsPrimaryMouseHoldOnly()
        {
            fixture.InstallColumn();
            fixture.RefreshTarget();
            var context = new InteractionContext(
                fixture.Root,
                fixture.Root.transform.position,
                fixture.Root.transform.forward);

            Assert.That(fixture.Target, Is.InstanceOf<IContinuousContextInteractionTarget>());
            Assert.That(fixture.Target, Is.Not.InstanceOf<IContextInteractionTarget>(),
                "The physical ignition must not expose the ordinary F interaction.");
            Assert.That(fixture.Target.UsesDirectionalHold, Is.True);
            Assert.That(fixture.Target.CanBeginContinuousInteraction(
                context, ContinuousContextInteractionDirection.Primary), Is.True);
            Assert.That(fixture.Target.CanBeginContinuousInteraction(
                context, ContinuousContextInteractionDirection.Secondary), Is.False,
                "The donor ignition has no RMB branch.");

            Assert.That(fixture.Controller.TryBeginPrimaryHold(1d), Is.True);
            Assert.That(fixture.Target.CanBeginContinuousInteraction(
                context, ContinuousContextInteractionDirection.Primary), Is.True,
                "The active hold must retain its release hint in the world action snapshot.");
            fixture.Controller.CancelHold();

            fixture.KeyAccess.SetAccess(false);
            fixture.Controller.RefreshAvailability();
            fixture.RefreshTarget();
            Assert.That(fixture.Target.CanBeginContinuousInteraction(
                context, ContinuousContextInteractionDirection.Primary), Is.False);
            Assert.That(fixture.InteractionCollider.enabled, Is.False);
        }

        [Test]
        public void OffShortPressEntersAccessoryAndStaysThereOnRelease()
        {
            fixture.InstallColumn();
            Assert.That(fixture.Controller.TryBeginPrimaryHold(10d), Is.True);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(fixture.Controller.IgnitionOn, Is.True);
            Assert.That(fixture.Controller.Starting, Is.False);
            Assert.That(fixture.Controller.HasAttemptedStart, Is.False);
            Assert.That(fixture.Key.activeSelf, Is.True);
            fixture.AssertKeyAngle(SatsumaIgnitionController.AccessoryAngleDegrees);

            Assert.That(fixture.Controller.ContinuePrimaryHold(10.399d), Is.True);
            fixture.Controller.ReleasePrimaryHold(10.399d);

            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(fixture.Controller.IsHeld, Is.False);
            Assert.That(fixture.Controller.HasAttemptedStart, Is.False);
            fixture.AssertKeyAngle(SatsumaIgnitionController.AccessoryAngleDegrees);
        }

        [TestCase(20d)]
        [TestCase(36000d)]
        public void RealtimeThresholdIsExactlyPointFourSeconds(double startedAt)
        {
            fixture.InstallColumn();
            Assert.That(SatsumaIgnitionController.StartHoldSeconds, Is.EqualTo(0.4d));
            Assert.That(fixture.Controller.TryBeginPrimaryHold(startedAt), Is.True);
            Assert.That(fixture.Controller.ContinuePrimaryHold(startedAt + 0.399d), Is.True);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(fixture.Controller.HasAttemptedStart, Is.False);

            Assert.That(fixture.Controller.ContinuePrimaryHold(startedAt + 0.4d), Is.True);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Starting));
            Assert.That(fixture.Controller.Starting, Is.True);
            Assert.That(fixture.Controller.HasAttemptedStart, Is.True);
            fixture.AssertKeyAngle(SatsumaIgnitionController.StartingAngleDegrees);

            fixture.Controller.ReleasePrimaryHold(startedAt + 0.4d);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(fixture.Controller.HasAttemptedStart, Is.True,
                "MotorOn is a donor-local attempted-start latch, not engine feedback.");
            Assert.That(fixture.Controller.IsHeld, Is.False);
        }

        [Test]
        public void ReleaseAtThresholdSamplesRealtimeEvenWithoutFinalHeldFrame()
        {
            fixture.InstallColumn();
            Assert.That(fixture.Controller.TryBeginPrimaryHold(30d), Is.True);

            fixture.Controller.ReleasePrimaryHold(30.4d);

            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(fixture.Controller.HasAttemptedStart, Is.True,
                "A skipped held callback must still traverse START before springing back to ACC.");
            Assert.That(fixture.Controller.IsHeld, Is.False);
        }

        [Test]
        public void AccessoryWithoutAttemptUsesShortOffAndLongStartBranches()
        {
            fixture.InstallColumn();
            fixture.Controller.RestorePersistentState(true);
            Assert.That(fixture.Controller.HasAttemptedStart, Is.False);

            Assert.That(fixture.Controller.TryBeginPrimaryHold(40d), Is.True);
            fixture.Controller.ReleasePrimaryHold(40.1d);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Off));
            Assert.That(fixture.Key.activeSelf, Is.False);
            fixture.AssertKeyAngle(0f);

            fixture.Controller.RestorePersistentState(true);
            Assert.That(fixture.Controller.TryBeginPrimaryHold(41d), Is.True);
            Assert.That(fixture.Controller.ContinuePrimaryHold(41.4d), Is.True);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Starting));
            Assert.That(fixture.Controller.HasAttemptedStart, Is.True);
        }

        [Test]
        public void FailedStartAttemptMakesNextDownTurnIgnitionOff()
        {
            fixture.InstallColumn();
            Assert.That(fixture.Electrical.ElectricsOk, Is.False);
            Assert.That(fixture.Controller.TryBeginPrimaryHold(50d), Is.True);
            Assert.That(fixture.Controller.ContinuePrimaryHold(50.4d), Is.True);
            Assert.That(fixture.Controller.Starting, Is.True,
                "Physical key travel is independent from electrical power.");
            Assert.That(fixture.Controller.StarterRequested, Is.False);
            fixture.Controller.ReleasePrimaryHold(50.5d);

            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(fixture.Controller.HasAttemptedStart, Is.True);
            Assert.That(fixture.Controller.TryBeginPrimaryHold(51d), Is.True);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Off));
            Assert.That(fixture.Controller.IsHeld, Is.False,
                "The attempted-start branch turns OFF on the down edge; it does not begin another hold.");
            Assert.That(fixture.Controller.HasAttemptedStart, Is.False);
        }

        [Test]
        public void StarterCircuitRequiresStarterPartWireAndFullCableStage()
        {
            fixture.InstallColumn();
            fixture.PrepareBaseElectrics();
            Assert.That(fixture.Electrical.ElectricsOk, Is.True);
            Assert.That(fixture.Electrical.StarterCircuitReady, Is.False);

            Assert.That(fixture.Electrical.TryInstallConnection(
                SatsumaElectricalConnection.Starter), Is.True);
            fixture.Tighten(SatsumaElectricalFastener.StarterCable, 7);
            Assert.That(fixture.Electrical.StarterCircuitReady, Is.False);
            Assert.That(fixture.Electrical.TryTurnFastener(
                SatsumaElectricalFastener.StarterCable, 1f), Is.True);
            Assert.That(fixture.Electrical.StarterCircuitReady, Is.False,
                "A fully wired positive lead cannot crank a missing starter motor.");

            fixture.InstallElectricalPart(SatsumaElectricalSystem.StarterPartDefinitionId);
            Assert.That(fixture.Electrical.StarterCircuitReady, Is.True);

            Assert.That(fixture.Electrical.TryTurnFastener(
                SatsumaElectricalFastener.StarterCable, -1f), Is.True);
            Assert.That(fixture.Electrical.StarterCircuitReady, Is.False);
        }

        [Test]
        public void RawStartingPoseAndEffectiveStarterDemandAreSeparate()
        {
            fixture.InstallColumn();
            fixture.StartAt(60d);
            Assert.That(fixture.Controller.Starting, Is.True);
            Assert.That(fixture.Controller.StarterRequested, Is.False);
            Assert.That(fixture.Adapter.ConsumeFixedInput(0).StarterRequested, Is.False);

            fixture.PrepareBaseElectrics();
            Assert.That(fixture.Electrical.TryInstallConnection(
                SatsumaElectricalConnection.Starter), Is.True);
            fixture.Tighten(
                SatsumaElectricalFastener.StarterCable,
                SatsumaElectricalSystem.FastenerMaximumStage);
            fixture.InstallElectricalPart(SatsumaElectricalSystem.StarterPartDefinitionId);

            Assert.That(fixture.Controller.Starting, Is.True);
            Assert.That(fixture.Controller.StarterRequested, Is.True);
            VehicleInputState input = fixture.Adapter.ConsumeFixedInput(0);
            Assert.That(input.IgnitionOn, Is.True);
            Assert.That(input.StarterRequested, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void KeyOrColumnLossCancelsTransientStartButPreservesAccessory(bool loseKey)
        {
            fixture.InstallColumn();
            fixture.StartAt(70d);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Starting));

            if (loseKey)
            {
                fixture.KeyAccess.SetAccess(false);
            }
            else
            {
                fixture.Column.RuntimeState.SetLoose(
                    fixture.Column.transform.position,
                    fixture.Column.transform.rotation);
            }

            VehicleInputState exported = fixture.Adapter.ConsumeFixedInput(0);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(fixture.Controller.IgnitionOn, Is.True,
                "Capability loss cancels START; it must not rewrite persistent ACC to OFF.");
            Assert.That(fixture.Controller.IsHeld, Is.False);
            Assert.That(exported.IgnitionOn, Is.True);
            Assert.That(exported.StarterRequested, Is.False);
            Assert.That(fixture.Presentation.activeSelf, Is.EqualTo(loseKey));
            Assert.That(fixture.Key.activeSelf, Is.EqualTo(loseKey));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PhysicalTargetCanBeginAgainAfterExternalCancellation(bool restore)
        {
            fixture.InstallColumn();
            var context = default(InteractionContext);
            fixture.Target.BeginContinuousInteraction(
                context, ContinuousContextInteractionDirection.Primary);
            Assert.That(fixture.Controller.IsHeld, Is.True);
            fixture.Target.BeginContinuousInteraction(
                context, ContinuousContextInteractionDirection.Primary);
            Assert.That(fixture.Controller.IsHeld, Is.True,
                "Duplicate begin must not interrupt a still-owned gesture.");

            if (restore)
            {
                fixture.Controller.RestorePersistentState(false);
            }
            else
            {
                fixture.KeyAccess.SetAccess(false);
                fixture.Adapter.ConsumeFixedInput(0);
                fixture.KeyAccess.SetAccess(true);
            }

            Assert.That(fixture.Controller.IsHeld, Is.False);
            fixture.Target.BeginContinuousInteraction(
                context, ContinuousContextInteractionDirection.Primary);
            Assert.That(fixture.Controller.IsHeld, Is.True,
                "No stale target-owned latch may block the next down edge.");
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            fixture.Target.EndContinuousInteraction();
        }

        [Test]
        public void DisableCallbackAndRestoreClearTransientIntentSafely()
        {
            fixture.InstallColumn();
            fixture.StartAt(80d);

            fixture.Controller.enabled = false;
            VehicleInputState disabledInput = fixture.Adapter.ConsumeFixedInput(0);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(fixture.Controller.IsHeld, Is.False);
            Assert.That(disabledInput.StarterRequested, Is.False);
            fixture.Controller.enabled = true;

            Assert.That(fixture.Controller.TryBeginPrimaryHold(81d), Is.True,
                "The attempted-start latch turns the ignition off on this down edge.");
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Off));
            Assert.That(fixture.Controller.TryBeginPrimaryHold(82d), Is.True);
            Assert.That(fixture.Controller.ContinuePrimaryHold(82.4d), Is.True);
            fixture.Controller.RestorePersistentState(true);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(fixture.Controller.IsHeld, Is.False);
            Assert.That(fixture.Controller.HasAttemptedStart, Is.False);

            fixture.KeyAccess.SetAccess(false);
            fixture.Controller.RestorePersistentState(false);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Off));
            fixture.Controller.RestorePersistentState(true);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory),
                "Restore must preserve the accepted ignition bool without requiring current key access.");
        }

        [Test]
        public void InvalidOrBackwardsRealtimeCancelsHoldWithoutStarting()
        {
            fixture.InstallColumn();
            Assert.That(fixture.Controller.TryBeginPrimaryHold(90d), Is.True);
            Assert.That(fixture.Controller.ContinuePrimaryHold(89.9d), Is.False);
            Assert.That(fixture.Controller.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(fixture.Controller.IsHeld, Is.False);
            Assert.That(fixture.Controller.HasAttemptedStart, Is.False);

            Assert.That(fixture.Controller.TryBeginPrimaryHold(double.NaN), Is.False);
            Assert.That(fixture.Controller.TryBeginPrimaryHold(double.PositiveInfinity), Is.False);
            Assert.That(fixture.Controller.TryBeginPrimaryHold(-1d), Is.False);
        }

        [Test]
        public void AdapterDelegatesEveryNonIgnitionChannelAndConsumptiveEdges()
        {
            fixture.InstallColumn();
            fixture.SetRouterField("throttle01", 0.75f);
            fixture.SetRouterField("clutchPedal01", 0.25f);
            fixture.SetRouterField("brake01", 0.5f);
            fixture.SetRouterField("steering", -0.4f);
            fixture.SetRouterField("ignitionOn", true);
            fixture.SetRouterField("starterRequested", true);
            fixture.SetRouterField("pendingGearDelta", 1);
            fixture.SetRouterField("resetRequested", true);
            fixture.Controller.RestorePersistentState(false);
            bool wasEnabled = fixture.Router.enabled;

            VehicleInputState input = fixture.Adapter.ConsumeFixedInput(2);

            Assert.That(input.Throttle01, Is.EqualTo(0.75f));
            Assert.That(input.ClutchPedal01, Is.EqualTo(0.25f));
            Assert.That(input.Brake01, Is.EqualTo(0.5f));
            Assert.That(input.SteeringMinusOneToOne, Is.EqualTo(-0.4f));
            Assert.That(input.GearChangeRequested, Is.True);
            Assert.That(input.RequestedGear, Is.EqualTo(3));
            Assert.That(input.IgnitionOn, Is.False,
                "The physical controller, not the legacy router toggle, owns ignition.");
            Assert.That(input.StarterRequested, Is.False);
            Assert.That(fixture.Adapter.ConsumeFixedInput(2).GearChangeRequested, Is.False,
                "The delegated gear edge must be consumed exactly once.");
            Assert.That(fixture.Adapter.ConsumeResetRequest(), Is.True);
            Assert.That(fixture.Adapter.ConsumeResetRequest(), Is.False,
                "The delegated reset edge must be consumed exactly once.");
            Assert.That(fixture.Router.enabled, Is.EqualTo(wasEnabled),
                "The adapter must not enable or otherwise own the Input System map.");
        }

        private sealed class Fixture : IDisposable
        {
            private static readonly BindingFlags PrivateInstance =
                BindingFlags.Instance | BindingFlags.NonPublic;

            private readonly PartInstance[] ownedParts;
            private readonly VehicleAssemblySaveData emptyAssembly;
            private readonly Quaternion keyRestRotation;

            public Fixture()
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
                Assert.That(prefab, Is.Not.Null,
                    "The private generated Satsuma baseline is required for C2 runtime tests.");
                Root = Object.Instantiate(prefab);
                Assembly = Root.GetComponent<VehicleAssemblyController>();
                Electrical = Root.GetComponent<SatsumaElectricalSystem>();
                Simulation = Root.GetComponent<VehicleSimulationHost>();
                Router = Root.GetComponent<VehicleInputRouter>();
                Assert.That(Assembly, Is.Not.Null);
                Assert.That(Electrical, Is.Not.Null);
                Assert.That(Simulation, Is.Not.Null);
                Assert.That(Router, Is.Not.Null);
                ownedParts = Assembly.Parts.ToArray();
                Assembly.Initialize();
                emptyAssembly = Assembly.CaptureSaveData();

                Presentation = new GameObject("Test ignition presentation");
                Presentation.transform.SetParent(Root.transform, false);
                KeyPivot = new GameObject("Test ignition key pivot").transform;
                KeyPivot.SetParent(Presentation.transform, false);
                keyRestRotation = Quaternion.Euler(7f, -11f, 19f);
                KeyPivot.localRotation = keyRestRotation;
                Key = new GameObject("Test visible key");
                Key.transform.SetParent(KeyPivot, false);

                Controller = Root.GetComponent<SatsumaIgnitionController>() ??
                    Root.AddComponent<SatsumaIgnitionController>();
                Controller.Configure(
                    Assembly,
                    Electrical,
                    Simulation,
                    KeyPivot,
                    Key,
                    Presentation);
                KeyAccess = new SatsumaKeyAccessState();
                Controller.BindKeyAccess(KeyAccess);

                Adapter = Root.GetComponent<SatsumaIgnitionInputAdapter>() ??
                    Root.AddComponent<SatsumaIgnitionInputAdapter>();
                Adapter.Configure(Router, Controller);

                GameObject targetObject = new GameObject("Test ignition target");
                targetObject.transform.SetParent(Root.transform, false);
                InteractionCollider = targetObject.AddComponent<SphereCollider>();
                InteractionCollider.isTrigger = true;
                Target = targetObject.AddComponent<SatsumaIgnitionInteractionTarget>();
                Target.Configure(Controller, InteractionCollider);

                Column = Assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    SatsumaIgnitionController.ColumnPartDefinitionId);
                Assert.That(ColumnMount.IsOccupied, Is.False,
                    "The generated steering column mount must begin empty.");
            }

            public GameObject Root { get; }
            public VehicleAssemblyController Assembly { get; }
            public SatsumaElectricalSystem Electrical { get; }
            public VehicleSimulationHost Simulation { get; }
            public VehicleInputRouter Router { get; }
            public SatsumaIgnitionController Controller { get; }
            public SatsumaIgnitionInputAdapter Adapter { get; }
            public SatsumaIgnitionInteractionTarget Target { get; }
            public SatsumaKeyAccessState KeyAccess { get; }
            public Transform KeyPivot { get; }
            public GameObject Key { get; }
            public GameObject Presentation { get; }
            public Collider InteractionCollider { get; }
            public PartInstance Column { get; }

            public MountPointRuntime ColumnMount
            {
                get
                {
                    Assert.That(Assembly.Graph.TryGetMount(
                        SatsumaIgnitionController.ColumnMountId,
                        out MountPointRuntime mount), Is.True);
                    return mount;
                }
            }

            public void InstallColumn()
            {
                var data = JsonUtility.FromJson<VehicleAssemblySaveData>(
                    JsonUtility.ToJson(emptyAssembly));
                MountPointAuthoring mount = Assembly.MountPoints.Single(value =>
                    value.MountId == SatsumaIgnitionController.ColumnMountId);
                MountSaveDto mountDto = data.mounts.Single(value =>
                    value.mountId == mount.MountId);
                PartSaveDto part = data.parts.Single(value =>
                    value.partDefinitionId ==
                    SatsumaIgnitionController.ColumnPartDefinitionId);
                part.lifecycleState = PartLifecycleState.Installed;
                part.installedMountId = mount.MountId;
                part.worldPosition = mount.Pose.position;
                part.worldRotation = mount.Pose.rotation;
                mountDto.installedPartStableEntityId = part.stableEntityId;

                Assert.That(Assembly.ValidateSaveDataForRestore(data).Succeeded, Is.True);
                AssemblyOperationResult restored = Assembly.RestoreSaveData(data);
                Assert.That(restored.Succeeded, Is.True, restored.Message);
                Controller.RefreshAvailability();
            }

            public void StartAt(double realtime)
            {
                Assert.That(Controller.TryBeginPrimaryHold(realtime), Is.True);
                Assert.That(Controller.ContinuePrimaryHold(
                    realtime + SatsumaIgnitionController.StartHoldSeconds), Is.True);
                Assert.That(Controller.State, Is.EqualTo(SatsumaIgnitionState.Starting));
            }

            public void PrepareBaseElectrics()
            {
                Assert.That(Simulation.TryInitialize(out string failure), Is.True, failure);
                InstallElectricalPart(SatsumaElectricalSystem.BatteryPartDefinitionId);
                Assert.That(Electrical.TryInstallConnection(
                    SatsumaElectricalConnection.BatteryHarness), Is.True);
                Assert.That(Electrical.TryInstallConnection(
                    SatsumaElectricalConnection.GroundBattery), Is.True);
                Assert.That(Electrical.TryInstallConnection(
                    SatsumaElectricalConnection.Ignition), Is.True);
                Tighten(
                    SatsumaElectricalFastener.BatteryPositiveTerminal,
                    SatsumaElectricalSystem.TerminalMaximumStage);
                Tighten(
                    SatsumaElectricalFastener.BatteryNegativeTerminal,
                    SatsumaElectricalSystem.TerminalMaximumStage);
                Assert.That(Electrical.BatteryVoltage,
                    Is.GreaterThan(SatsumaElectricalSystem.DonorMinimumUsableVoltage));
                Assert.That(Electrical.ElectricsOk, Is.True);
            }

            public void InstallElectricalPart(string definitionId)
            {
                PartInstance part = Assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId == definitionId);
                part.RuntimeState.SetInstalled("test.electrical." + definitionId, false);
            }

            public void Tighten(SatsumaElectricalFastener fastener, int stages)
            {
                for (int stage = 0; stage < stages; stage++)
                {
                    Assert.That(Electrical.TryTurnFastener(fastener, 1f), Is.True,
                        fastener + " stage " + stage);
                }
            }

            public void AssertKeyAngle(float angle)
            {
                Quaternion expected = keyRestRotation *
                    Quaternion.AngleAxis(angle, Vector3.up);
                Assert.That(Quaternion.Angle(KeyPivot.localRotation, expected),
                    Is.LessThan(0.001f));
            }

            public void RefreshTarget()
            {
                typeof(SatsumaIgnitionInteractionTarget)
                    .GetMethod("Update", PrivateInstance)
                    .Invoke(Target, null);
            }

            public void SetRouterField(string fieldName, object value)
            {
                FieldInfo field = typeof(VehicleInputRouter)
                    .GetField(fieldName, PrivateInstance);
                Assert.That(field, Is.Not.Null, fieldName);
                field.SetValue(Router, value);
            }

            public void Dispose()
            {
                foreach (PartInstance part in ownedParts)
                {
                    if (part != null && !part.transform.IsChildOf(Root.transform))
                    {
                        Object.DestroyImmediate(part.gameObject);
                    }
                }

                Object.DestroyImmediate(Root);
            }
        }
    }
}
