using System;
using System.Linq;
using MSC.Core.Identity;
using MSC.Bootstrap;
using MSC.Core.Time;
using MSC.Items;
using MSC.Interaction.Capabilities;
using MSC.Presentation.Fluid;
using MSC.World.Streaming;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaOperatingSourceTests
    {
        [Test]
        public void CanonicalHasOneExplicitSourceAndNinePartOwnedConditions_IdempotentAuthoring()
        {
            using var f = new Fixture(false);
            Assert.That(f.Source.ValidateBindings(out string failure), Is.True, failure);
            Assert.That(f.Host.SatsumaOperatingSourceComponent, Is.SameAs(f.Source));
            Assert.That(f.Root.GetComponentsInChildren<AssemblyMechanicalConditionState>(true), Has.Length.EqualTo(9));
            string before = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            Assert.That(Phase1SatsumaOperatingAuthoring.ApplyToInstance(f.Assembly), Is.Zero);
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(before));
            Assert.That(f.Host.State.SatsumaOperating, Is.Not.Null);
        }

        [Test]
        public void ActualMixtureScrollAndRussianPromptAgreeWithOperatingAirFuelRatio()
        {
            using var f = new Fixture();
            var mixture = f.Part("carburetor").GetComponent<AssemblyEngineAdjustmentState>();
            var target = f.Root.GetComponentsInChildren<AssemblyEngineAdjustmentTarget>(true).Single(value => value.State == mixture);
            var model = f.Host.Root.SatsumaOperatingModel;
            var input = VehicleInputState.Neutral(true);
            float initialAfr = model.Evaluate(f.Host.State, input, f.Source.CaptureConditions(), .02f).AirFuelRatio;
            Assert.That(mixture.TryAdjust(1f), Is.True);
            float leanAfr = model.Evaluate(f.Host.State, input, f.Source.CaptureConditions(), .02f).AirFuelRatio;
            Assert.That(leanAfr, Is.GreaterThan(initialAfr));
            Assert.That(target.GetHeldToolScrollPrompt(InteractionScrollDirection.Positive), Is.EqualTo("ОБЕДНИТЬ СМЕСЬ"));
            Assert.That(mixture.TryAdjust(-1f), Is.True);
            Assert.That(mixture.TryAdjust(-1f), Is.True);
            float richAfr = model.Evaluate(f.Host.State, input, f.Source.CaptureConditions(), .02f).AirFuelRatio;
            Assert.That(richAfr, Is.LessThan(initialAfr));
            Assert.That(target.GetHeldToolScrollPrompt(InteractionScrollDirection.Negative), Is.EqualTo("ОБОГАТИТЬ СМЕСЬ"));
        }

        [Test]
        public void RealGraphSettingsCapsAndFittingsFeedTypedOperatingInputs()
        {
            using var f = new Fixture();
            SatsumaOperatingInputs input = f.Source.CaptureConditions();
            Assert.That(input.IsFinite, Is.True);
            Assert.That(input.FiringCylinderMask, Is.EqualTo(15));
            Assert.That(input.Ancillary.BatteryConnected && input.Ancillary.BatteryIgnitionPowered, Is.True);
            Assert.That(input.Ancillary.AlternatorIgnitionWired && input.Ancillary.BeltUsable && input.Ancillary.WaterPumpUsable, Is.True);
            Assert.That(input.Fluids.OilPanInstalled && input.Fluids.HeadGasketHealthy && input.Fluids.HosesInstalled, Is.True);
            Assert.That(input.Fluids.FilterTightness, Is.EqualTo(8));
            Assert.That(input.Fluids.PanTightness, Is.EqualTo(64));
            Assert.That(input.Fluids.CoverTightness, Is.EqualTo(48));
            Assert.That(input.Fluids.HoseTightness, Is.EqualTo(40));
            Assert.That(input.Fluids.BrakeLineSeal01, Is.EqualTo(1f));
            Assert.That(input.Fluids.ClutchLineSeal01, Is.EqualTo(1f));
            var cap = f.Part("radiator").GetComponent<AssemblyServiceCapState>();
            for (int i = 0; i < 11; i++) Assert.That(cap.TryAdjust(0, -1f), Is.True);
            Assert.That(f.Source.CaptureConditions().Fluids.RadiatorCapClosed, Is.False);
            f.Source.SetAmbientTemperature(-12f);
            Assert.That(f.Source.CaptureConditions().AmbientCelsius, Is.EqualTo(-12f));
            f.Source.ClearEnvironmentBinding(); Assert.That(f.Source.HasEnvironmentBinding, Is.False);
        }

        [Test]
        public void RealHostCranksStartsAndKeepsRunningOnAlternator_BeltLossThenStopsIt()
        {
            using var f = new Fixture();
            bool cranked = false;
            for (int tick = 0; tick < 600 && f.Host.State.EngineStatus != VehicleEngineStatus.Running; tick++)
            {
                f.Host.Root.Tick(.02f, Crank);
                cranked |= f.Host.State.EngineStatus == VehicleEngineStatus.Cranking;
            }
            Assert.That(cranked, Is.True);
            Assert.That(f.Host.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running), f.Describe());
            for (int i = 0; i < 200; i++) f.Host.Root.Tick(.02f, VehicleInputState.Neutral(true));
            Assert.That(f.Host.Root.SatsumaOperatingModel.LastPoint.AlternatorGenerating, Is.True, f.Describe());
            Assert.That(f.Assembly.TryBreakInstalledPart(f.Part("battery")).Succeeded, Is.True);
            for (int i = 0; i < 150; i++) f.Host.Root.Tick(.02f, VehicleInputState.Neutral(true));
            Assert.That(f.Host.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running), f.Describe());
            Assert.That(f.Source.CaptureConditions().Ancillary.BatteryConnected, Is.False);
            Assert.That(f.Host.Root.Prerequisites.CanCrank, Is.False);
            Assert.That(f.Assembly.TryBreakInstalledPart(f.Belt).Succeeded, Is.True);
            for (int i = 0; i < 500; i++) f.Host.Root.Tick(.02f, VehicleInputState.Neutral(true));
            Assert.That(f.Host.State.EngineStatus, Is.Not.EqualTo(VehicleEngineStatus.Running), f.Describe());
            Assert.That(f.Host.Root.SatsumaOperatingModel.LastPoint.IgnitionPowered, Is.False);
        }

        [Test]
        public void WearCommitsToInstalledPartsAndPurchasedSink_AndSurvivesRoundTrip()
        {
            using var f = new Fixture();
            f.Source.ApplyWear(new SatsumaWearDelta(2f, 3f, 4f, 5f, 6f, 7f));
            Assert.That(f.Part("piston1").GetComponent<AssemblyMechanicalConditionState>().ConditionPercent, Is.EqualTo(98));
            Assert.That(f.Part("crankshaft").GetComponent<AssemblyMechanicalConditionState>().ConditionPercent, Is.EqualTo(97));
            Assert.That(f.Part("rocker-shaft").GetComponent<AssemblyMechanicalConditionState>().ConditionPercent, Is.EqualTo(93));
            Assert.That(f.Belt.GetComponent<OperatingTestConsumable>().ConditionPercent, Is.EqualTo(95));
            Assert.That(f.Belt.GetComponent<AssemblyMechanicalConditionState>(), Is.Null);
            var save = f.Assembly.CaptureSaveData();
            f.Source.ApplyWear(new SatsumaWearDelta(10f, 0f, 0f, 0f, 0f));
            var result = f.Assembly.RestoreSaveData(save); Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(f.Part("piston1").GetComponent<AssemblyMechanicalConditionState>().ConditionPercent, Is.EqualTo(98));
            Assert.That(f.Assembly.TryBreakInstalledPart(f.Part("water-pump")).Succeeded, Is.True);
            float health = f.Part("water-pump").GetComponent<AssemblyMechanicalConditionState>().ConditionPercent;
            f.Source.ApplyWear(new SatsumaWearDelta(0f, 0f, 0f, 0f, 50f));
            Assert.That(f.Part("water-pump").GetComponent<AssemblyMechanicalConditionState>().ConditionPercent, Is.EqualTo(health));
            Assert.That(f.Source.CaptureConditions().Ancillary.WaterPumpUsable, Is.False);
        }

        [Test]
        public void FuelOnlyDebugRetainsOilWarningsAndNoFluidRefill_AutochargeDoesNotCreateBattery()
        {
            using var f = new Fixture();
            VehicleSimulationStateDto dto = f.Host.State.CaptureDto(); dto.fuelLiters = dto.oilLiters = dto.coolantLiters = 0f;
            Assert.That(f.Host.TryRestoreSimulationState(dto, out _), Is.True);
            f.Prerequisites.SetFuelReadinessTestOverride(true); f.Source.SetDebugAutocharge(true);
            f.Host.Root.Tick(.02f, Crank);
            Assert.That(f.Host.State.FuelLiters + f.Host.State.OilLiters + f.Host.State.CoolantLiters, Is.Zero);
            Assert.That(f.Host.Root.Prerequisites.FailureFlags.HasFlag(VehicleSimulationPrerequisiteFailure.FuelUnavailable), Is.False);
            Assert.That(f.Host.Root.Prerequisites.FailureFlags.HasFlag(VehicleSimulationPrerequisiteFailure.OilUnavailable), Is.True);
            Assert.That(f.Assembly.TryBreakInstalledPart(f.Part("battery")).Succeeded, Is.True);
            dto = f.Host.State.CaptureDto(); dto.batteryVoltage = 4f;
            Assert.That(f.Host.TryRestoreSimulationState(dto, out _), Is.True);
            f.Host.Root.Tick(.02f, Crank);
            Assert.That(f.Host.State.BatteryVoltage, Is.EqualTo(4f)); Assert.That(f.Host.Root.Prerequisites.CanCrank, Is.False);
        }

        private static VehicleInputState Crank => new(0f, 1f, 0f, 0f, true, true, true, 0);

        [Test]
        public void LooseSiblingReservoirsAreComposedBeforeAssemblyAndOnlyOnce()
        {
            using var f = new Fixture(false, physicalScene: true);
            Transform[] parents = f.Assembly.Parts.Select(part => part.transform.parent).ToArray();
            try
            {
                // Mirror LegacySatsumaLoosePartsRoot.Awake outside PlayMode.
                foreach (PartInstance part in f.Assembly.Parts)
                    if (!part.IsAssemblyRoot) part.transform.SetParent(null, true);
                Assert.That(f.Root.GetComponentsInChildren<SatsumaServiceFluidReceiver>(true), Is.Empty);
                SatsumaServiceLevelComposition.Configure(f.Root);
                SatsumaServiceLevelComposition.Configure(f.Root);
                var levels = f.Assembly.Parts.SelectMany(part => part.GetComponentsInChildren<ServiceReservoirLevelPresenter>(true)).ToArray();
                Assert.That(levels, Has.Length.EqualTo(4));
                foreach (ServiceReservoirLevelPresenter level in levels)
                { level.RefreshPresentation(); Assert.That(level.IsConfigured, Is.True); Assert.That(level.IsVisible, Is.False); }
            }
            finally
            {
                for (int i = 0; i < f.Assembly.Parts.Length; i++)
                    f.Assembly.Parts[i].transform.SetParent(parents[i], true);
            }
        }

        [Test]
        public void FourReservoirLevelsFollowRealLitresCapsAndRestore_NoFalseOilPool()
        {
            using var f = new Fixture();
            SatsumaServiceLevelComposition.Configure(f.Root);
            SatsumaServiceLevelComposition.Configure(f.Root);
            Assert.That(f.Root.GetComponentsInChildren<ServiceReservoirLevelPresenter>(true), Has.Length.EqualTo(4));
            var receiver = f.Root.GetComponentsInChildren<SatsumaServiceFluidReceiver>(true).Single(value => value.Fluid == SatsumaServiceFluid.Clutch);
            var level = receiver.GetComponent<ServiceReservoirLevelPresenter>();
            level.RefreshPresentation(); Assert.That(level.IsVisible, Is.False);
            for (int i = 0; i < 11; i++) receiver.Caps.TryAdjust(receiver.CapIndex, -1f);
            Assert.That(receiver.TryAcceptLiquid("liquid.brake-fluid", .25f, out _), Is.True);
            level.RefreshPresentation(); Assert.That(level.IsVisible, Is.True);
            Assert.That(level.SurfaceTransform.localPosition.z, Is.EqualTo(-.039f).Within(.00001f));
            Assert.That(Vector3.Dot(level.SurfaceTransform.forward, Vector3.up), Is.GreaterThan(.999f));
            var saved = f.Host.State.CaptureDto();
            receiver.TryAcceptLiquid("liquid.brake-fluid", .25f, out _);
            level.RefreshPresentation(); Assert.That(level.SurfaceTransform.localPosition.z, Is.EqualTo(-.008f).Within(.00001f));
            Assert.That(f.Host.TryRestoreSimulationState(saved, out _), Is.True);
            level.RefreshPresentation(); Assert.That(level.SurfaceTransform.localPosition.z, Is.EqualTo(-.039f).Within(.00001f));
            receiver.Caps.TryAdjust(receiver.CapIndex, 1f);
            level.RefreshPresentation(); Assert.That(level.IsVisible, Is.False);
            Assert.That(f.Root.GetComponentsInChildren<SatsumaServiceFluidReceiver>(true).Single(value => value.Fluid == SatsumaServiceFluid.MotorOil)
                .GetComponent<ServiceReservoirLevelPresenter>(), Is.Null);
        }

        [Test]
        public void AllFiveRealServiceOpeningsAcceptARealCanThroughTheirActualCollision()
        {
            using var f = new Fixture(physicalScene: true);
            foreach (SatsumaServiceFluidReceiver check in f.Root.GetComponentsInChildren<SatsumaServiceFluidReceiver>(true))
            {
                Assert.That(f.Assembly.Parts.Contains(check.Caps.Part), Is.True, "Cap points outside instantiated registry: " + check.Fluid);
                Assert.That(check.Caps.Part.IsInstalled, Is.True, "Cap owner not installed before servicing: " + check.Fluid);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>("Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset");
            var placements = ScriptableObject.CreateInstance<ItemPlacementCatalog>();
            var manifest = ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
            var owner = new GameObject("TEST ONLY service items");
            try
            {
                placements.ConfigureForAuthoring("test.service", "msc-world-baseline-04a1.1-c3f2f337", Array.Empty<ItemPlacementRecord>());
                manifest.ConfigureForAuthoring(512f, 0, 1, new[] { new ProductionWorldCellScene("test.service", 10, 10, 999, "Assets/Tests/Unused.unity") });
                var streaming = owner.AddComponent<ProductionWorldStreamingService>(); streaming.ConfigureForAuthoring(manifest);
                var runtime = owner.AddComponent<ItemWorldRuntime>();
                runtime.Initialize(catalog, placements, streaming, SceneManager.GetActiveScene(), -10f, new GameTimeService());
                var state = f.Host.State.CaptureDto(); state.oilLiters = state.coolantLiters = 0f;
                Assert.That(f.Host.TryRestoreSimulationState(state, out _), Is.True);
                // Open bonnet is an actual assembly access requirement, not a raycast exception.
                Assert.That(f.Assembly.TryBreakInstalledPart(f.Part("hood")).Succeeded, Is.True);
                Assert.That(f.Part("rocker-cover").IsInstalled, Is.True, "Hood break detached the cover");
                f.Part("hood").gameObject.SetActive(false);
                foreach (SatsumaServiceFluidReceiver receiver in f.Root.GetComponentsInChildren<SatsumaServiceFluidReceiver>(true))
                {
                    for (int notch = 0; notch < 11; notch++) Assert.That(receiver.Caps.TryAdjust(receiver.CapIndex, -1f), Is.True,
                        receiver.Fluid + " cap not available: installed=" + receiver.Caps.Part.IsInstalled + "; enabled=" + receiver.Caps.enabled +
                        "; active=" + receiver.Caps.gameObject.activeInHierarchy + "; root=" + f.Root.activeInHierarchy + "; angle=" + receiver.Caps.Angle(receiver.CapIndex));
                    Assert.That(receiver.IsOpen, Is.True, receiver.Fluid + " receiver not open");
                    string itemId = receiver.Fluid == SatsumaServiceFluid.MotorOil ? "item.motor-oil" :
                        receiver.Fluid == SatsumaServiceFluid.Coolant ? "item.coolant" : "item.brake-fluid";
                    WorldItemInstance can = runtime.SpawnDynamic(itemId, StableEntityId.New(), Vector3.zero, Quaternion.identity, SceneManager.GetActiveScene());
                    can.transform.SetParent(owner.transform, true);
                    Assert.That(can.TryPerformPrimaryAction(default), Is.True, "Can primary open action");
                    Assert.That(can.IsOpen, Is.True);
                    Assert.That(receiver.CanAcceptLiquid(can.LiquidId, .01f), Is.True, receiver.Fluid + " rejected liquid " + can.LiquidId);
                    can.transform.rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.down);
                    var pour = can.GetComponent<ServiceFluidPourController>();
                    can.transform.position += receiver.transform.position + Vector3.up * .2f - pour.OutletWorldPosition;
                    Physics.SyncTransforms();
                    Assert.That(pour.TraceStream(out Vector3 impact, out bool hit), Is.SameAs(receiver), receiver.Fluid + " blocked at " + impact);
                    Assert.That(hit, Is.True);
                    float before = can.LiquidAmountLitres; pour.Tick(.1f);
                    Assert.That(pour.IsPouring, Is.True, "can flow inactive: " + can.IsConsumed + "," + can.IsBroken + "," + can.LiquidAmountLitres);
                    Assert.That(receiver.ContentLiters, Is.GreaterThan(0f), receiver.Fluid.ToString());
                    Assert.That(can.LiquidAmountLitres + receiver.ContentLiters, Is.EqualTo(before).Within(.0001f));
                    can.gameObject.SetActive(false);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(placements); UnityEngine.Object.DestroyImmediate(manifest); }
        }

        [Test]
        public void RecordRealCapOpeningMeshDepthsForLevelPresentation()
        {
            using var f = new Fixture();
            foreach (AssemblyServiceCapTarget cap in f.Root.GetComponentsInChildren<AssemblyServiceCapTarget>(true))
            {
                float nearest = .4f;
                Vector3 axis = cap.transform.forward;
                var ray = new Ray(cap.transform.position + axis * .04f, -axis);
                foreach (MeshFilter filter in cap.State.Part.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || filter.GetComponentInParent<PartInstance>() != cap.State.Part || filter.transform == cap.CapRenderer.transform) continue;
                    var shape = new GameObject("TEST ONLY cavity geometry probe");
                    try
                    {
                        shape.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                        shape.transform.localScale = filter.transform.lossyScale;
                        var mesh = shape.AddComponent<MeshCollider>(); mesh.sharedMesh = filter.sharedMesh;
                        Physics.SyncTransforms();
                        if (mesh.Raycast(ray, out RaycastHit hit, .4f)) nearest = Mathf.Min(nearest, hit.distance);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(shape); }
                }
                Debug.Log("SERVICE_OPENING_DEPTH kind=" + cap.State.Kind(cap.CapIndex) + " meters=" + (nearest - .04f).ToString("F5", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        internal sealed class Fixture : IDisposable
        {
            public GameObject Root { get; }
            public VehicleAssemblyController Assembly { get; }
            public VehicleSimulationHost Host { get; }
            public SatsumaEngineOperatingSource Source { get; }
            public AssemblyVehiclePrerequisiteAdapter Prerequisites { get; }
            public PartInstance Belt { get; }
            private readonly bool physicalScene;
            public Fixture(bool assembled = true, bool physicalScene = false)
            {
                this.physicalScene = physicalScene;
                Root = physicalScene ? UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath)) :
                    PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
                Assembly = Root.GetComponent<VehicleAssemblyController>(); Host = Root.GetComponent<VehicleSimulationHost>();
                Source = Root.GetComponent<SatsumaEngineOperatingSource>(); Prerequisites = Root.GetComponent<AssemblyVehiclePrerequisiteAdapter>();
                Host.Configure(Host.Config, Root.AddComponent<CapTestWheels>(), Prerequisites, null);
                Assert.That(Host.TryInitialize(out string failure), Is.True, failure);
                if (!assembled) return;
                // Synthetic assembly in a preview scene. No native save, prefab or generated asset is written.
                VehicleAssemblySaveData data = Assembly.CaptureSaveData();
                // The baseline also includes a loose GT cover sharing this
                // socket. This packet exercises the explicitly authored stock cap.
                foreach (PartSaveDto part in data.parts.OrderBy(value => value.partDefinitionId == "vehicle.satsuma.part.rocker-cover" ? 0 : 1))
                {
                    if (part.lifecycleState == PartLifecycleState.Installed) continue;
                    MountPointAuthoring mount = Assembly.MountPoints.FirstOrDefault(value => value.Definition.AcceptedPartDefinitionIds.Contains(part.partDefinitionId) &&
                        string.IsNullOrEmpty(data.mounts.Single(m => m.mountId == value.MountId).installedPartStableEntityId));
                    if (mount == null) continue;
                    part.lifecycleState = PartLifecycleState.Installed; part.installedMountId = mount.MountId;
                    part.worldPosition = mount.Pose.position; part.worldRotation = mount.Pose.rotation;
                    data.mounts.Single(value => value.mountId == mount.MountId).installedPartStableEntityId = part.stableEntityId;
                }
                data = Phase1SatsumaNativeStartSmoke.CreateTightenedCopy(data, Assembly.MountPoints);
                var result = Assembly.RestoreSaveData(data); Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(Part("rocker-cover").IsInstalled, Is.True, "Synthetic cover after first restore");
                for (int i = 1; i <= 4; i++) AddConsumable(SatsumaConsumableAssemblyRules.SparkPlugPartId,
                    "item.spark-plug", SatsumaConsumableAssemblyRules.SparkPlugMountId(i));
                Belt = AddConsumable(SatsumaConsumableAssemblyRules.BeltPartId, "item.alternator-belt", SatsumaConsumableAssemblyRules.BeltMountId);
                Assert.That(Part("rocker-cover").IsInstalled, Is.True, "Synthetic cover after consumable installs");
                result = Assembly.RestoreSaveData(Phase1SatsumaNativeStartSmoke.CreateTightenedCopy(Assembly.CaptureSaveData(), Assembly.MountPoints));
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(Part("rocker-cover").IsInstalled, Is.True, "Synthetic cover after second restore");
                foreach (AssemblyEngineAdjustmentState setting in Root.GetComponentsInChildren<AssemblyEngineAdjustmentState>(true))
                    setting.RestoreValidated(new AssemblyEngineAdjustmentSaveDto { kind = setting.Kind,
                        value = setting.Kind == SatsumaEngineAdjustmentKind.Alternator ? 7f : setting.Kind == SatsumaEngineAdjustmentKind.OilFilter ? 8f : 15f });
                Part("camshaft-gear").GetComponent<AssemblyCamshaftTimingState>().RestoreValidated(new AssemblyCamshaftTimingSaveDto());
                var electrical = Root.GetComponent<SatsumaElectricalSystem>();
                Assert.That(electrical.TryRestore(new SatsumaElectricalSaveDto
                { installedConnectionIds = Enum.GetNames(typeof(SatsumaElectricalConnection)), batteryPlusStage = 8, batteryMinusStage = 8, starterCableStage = 8 }, out failure), Is.True, failure);
                VehicleSimulationStateDto dto = Host.State.CaptureDto(); dto.fuelLiters = 5f; dto.oilLiters = 3f; dto.coolantLiters = 5.4f;
                dto.batteryVoltage = 12.6f; dto.engineTemperatureCelsius = 80f;
                Assert.That(Host.TryRestoreSimulationState(dto, out failure), Is.True, failure);
                Assert.That(Part("rocker-cover").IsInstalled, Is.True, "Synthetic cover after simulation restore");
            }
            public PartInstance Part(string suffix) => Assembly.Parts.Single(value => value.Definition.DefinitionId == "vehicle.satsuma.part." + suffix);
            private PartInstance AddConsumable(string definitionId, string itemId, string mountId)
            {
                Assert.That(Assembly.TryGetDynamicPartDefinition(definitionId, out PartDefinition definition), Is.True);
                var go = new GameObject("TEST ONLY operating-source consumable"); go.transform.SetParent(Root.transform, false);
                var identity = go.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = go.AddComponent<Rigidbody>(); body.useGravity = false;
                var part = go.AddComponent<PartInstance>(); part.Configure(definition, identity, body, null, false, string.Empty);
                go.AddComponent<OperatingTestConsumable>();
                Assert.That(Assembly.TryRegisterDynamicPart(part, itemId, out string failure), Is.True, failure);
                var mount = Assembly.MountPoints.Single(value => value.MountId == mountId);
                go.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                var result = Assembly.TryInstall(part, mount); Assert.That(result.Succeeded, Is.True, result.Message);
                return part;
            }
            public string Describe() => Host.State.EngineRpm + " RPM; " + Host.Root.Prerequisites.FailureFlags + "; " +
                "AFR=" + Host.Root.SatsumaOperatingModel.LastPoint.AirFuelRatio + "; ignition=" + Host.Root.SatsumaOperatingModel.LastPoint.IgnitionPowered;
            public void Dispose()
            {
                // EditMode manual presentation calls do not receive player-loop
                // lifecycle callbacks when preview prefab contents are unloaded.
                Root.GetComponent<SatsumaEngineMechanicalMotion>()?.ResetPresentation();
                Root.GetComponent<SatsumaFlexibleConnectionPresenter>()?.ResetPresentation();
                if (physicalScene) UnityEngine.Object.DestroyImmediate(Root); else PrefabUtility.UnloadPrefabContents(Root);
            }
        }
    }
    public sealed class OperatingTestConsumable : MonoBehaviour, IAssemblyItemCondition, IAssemblyWearSink
    {
        public float ConditionPercent { get; private set; } = 100f;
        public bool IsBroken => ConditionPercent <= 0f;
        public bool TryApplyWear(float loss) { ConditionPercent = Mathf.Max(0f, ConditionPercent - loss); return loss > 0f; }
    }
}
