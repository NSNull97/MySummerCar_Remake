#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Items;
using MSC.Items.Presentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.ItemsIntegration;
using MSC.Vehicle.NWH;
using MSC.Vehicle.Simulation;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    /// <summary>
    /// Stationary startup integration, not an assembly recipe or road-physics
    /// comparison. Stock parts are prepared by an in-memory native graph DTO;
    /// purchased plugs and belt use real Items wrappers/provider/bridge and handoff.
    /// After preparation only the real ignition/throttle capabilities and host
    /// player-loop lifecycle drive the engine. No native files or Bootstrap.
    /// </summary>
    public sealed class CanonicalPurchasedPlugIgnitionPlayModeTests
    {
        private const string VehicleRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        private const string ItemsRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items";
        private const string PlugItemId = "item.spark-plug";
        private const string BeltItemId = "item.alternator-belt";
        private const string PartPrefix = "vehicle.satsuma.part.";
        private static readonly string[] StockParts =
        {
            "sub-frame", "engine-block", "crankshaft", "main-bearing1", "main-bearing2", "main-bearing3",
            "piston1", "piston2", "piston3", "piston4", "camshaft", "camshaft-gear", "timing-chain",
            "timing-cover", "crankshaft-pulley", "water-pump", "water-pump-pulley", "engine-plate",
            "flywheel", "clutch-pressure-plate", "clutch", "clutch-cover-plate", "gearbox", "drive-gear",
            "inspection-cover", "starter", "oilpan", "head-gasket", "cylinder-head", "rocker-shaft",
            "rocker-cover", "headers", "carburetor", "airfilter", "alternator", "distributor", "fuel-pump",
            "radiator", "radiator-hose1", "radiator-hose2", "radiator-hose3", "oilfilter0", "electrics", "battery",
            "fuel-tank", "fuel-strainer", "steering-column",
        };

        private readonly List<ScriptableObject> ownedAssets = new();
        private readonly List<WorldItemInstance> plugs = new();
        private Scene scene;
        private GameObject vehicle;
        private GameObject interactor;
        private VehicleAssemblyController assembly;
        private VehicleSimulationHost simulation;
        private VehicleSimulationConfig originalConfig;
        private NwhWheelPhysicsBackend originalBackend;
        private SatsumaIgnitionInputAdapter input;
        private SatsumaIgnitionController ignition;
        private SatsumaIgnitionInteractionTarget keyTarget;
        private AssemblyCarburetorThrottleTarget throttleTarget;
        private AssemblyVehiclePrerequisiteAdapter prerequisites;
        private SatsumaEngineOperatingSource operatingSource;
        private ItemWorldRuntime items;
        private PhysicalCarryController carry;
        private GameObject plugVisualPrefab;
        private IItemPresentationProvider previousProviderHub;
        private float previousTimeScale;
        private float previousCaptureDeltaTime;
        private float previousFixedDeltaTime;
        private bool fixtureStarted;

        [UnityTest]
        public IEnumerator RealPurchasedPlugsAndIgnitionHoldSustainIdleRevAndKeyOffThroughHostFrames()
        {
            CreateFixture();
            yield return null;
            Assert.That(SatsumaEngineAssemblyReadiness.IsStructuralCombustionReady(assembly.Graph), Is.True);
            Assert.That(SatsumaEngineAssemblyReadiness.IsStockFuelDeliveryReady(assembly.Graph), Is.True);
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(assembly.Graph), Is.Zero,
                "A complete stock engine with empty purchased sockets must not invent spark plugs.");
            yield return InstallAndTensionPurchasedBelt();
            for (int cylinder = 1; cylinder <= 4; cylinder++) yield return InstallPurchasedPlug(cylinder);
            Assert.That(plugs.Select(item => item.StableId.Value).Distinct().Count(), Is.EqualTo(4));
            Assert.That(assembly.Parts, Has.Length.EqualTo(126));
            Assert.That(assembly.AllRuntimeParts, Has.Length.EqualTo(131));
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(assembly.Graph), Is.EqualTo(15));
            Assert.That(ignition.CanOperate, Is.True);
            Assert.That(ignition.Electrical.StarterCircuitReady, Is.True);
            Assert.That(prerequisites.IgnoreFluidReadinessForTesting, Is.False);
            Assert.That(prerequisites.IgnoreFuelReadinessForTesting, Is.True);
            AssertPreparedServiceVolumes();
            SatsumaOperatingInputs conditions = operatingSource.CaptureConditions();
            Assert.That(conditions.Ancillary.BeltUsable, Is.True);
            Assert.That(conditions.Ancillary.BatteryIgnitionPowered, Is.True);
            Assert.That(conditions.Ancillary.AlternatorChargeWired, Is.True);
            Assert.That(conditions.Tuning.CamDegrees, Is.Zero);
            Assert.That(conditions.Tuning.AlternatorAngle, Is.EqualTo(7f));
            Assert.That(conditions.Fluids.FilterTightness, Is.EqualTo(8f));
            Assert.That(conditions.Fluids.HosesInstalled, Is.True);
            Assert.That(conditions.Fluids.RadiatorCapClosed && conditions.Fluids.OilCapClosed, Is.True);
            Assert.That(originalBackend.TryValidate(out string failure), Is.True, failure);

            // Preparation was paused. Keep the authored source, config, NWH
            // backend and all prerequisite gates; now the real FixedUpdate runs.
            int firstTick = simulation.FixedTickCount;
            Time.timeScale = 1f;
            simulation.enabled = true;
            yield return Frames(2);
            Assert.That(simulation.FixedTickCount, Is.GreaterThan(firstTick));
            Assert.That(simulation.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Off));
            Assert.That(simulation.State.EngineRpm, Is.Zero);
            Assert.That(simulation.LastInput.IgnitionOn, Is.False);

            InteractionContext keyContext = ContextNear(keyTarget.transform);
            Assert.That(keyTarget.CanBeginContinuousInteraction(keyContext, ContinuousContextInteractionDirection.Secondary), Is.False);
            Assert.That(keyTarget.CanBeginContinuousInteraction(keyContext, ContinuousContextInteractionDirection.Primary), Is.True);
            double heldAt = Time.realtimeSinceStartupAsDouble;
            keyTarget.BeginContinuousInteraction(keyContext, ContinuousContextInteractionDirection.Primary);
            Assert.That(ignition.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            double keyDeadline = heldAt + SatsumaIgnitionController.StartHoldSeconds + 2d;
            while (!ignition.Starting && Time.realtimeSinceStartupAsDouble < keyDeadline)
            {
                Assert.That(keyTarget.ContinueContinuousInteraction(Time.unscaledDeltaTime), Is.True);
                if (!ignition.Starting) yield return null;
            }
            Assert.That(ignition.Starting, Is.True, Describe("key START deadline"));
            Assert.That(Time.realtimeSinceStartupAsDouble - heldAt,
                Is.GreaterThanOrEqualTo(SatsumaIgnitionController.StartHoldSeconds));
            Assert.That(ignition.StarterRequested, Is.True);

            bool observedCranking = false;
            float crankStart = simulation.State.ElapsedSeconds;
            double crankDeadline = Time.realtimeSinceStartupAsDouble + 10d;
            while (simulation.State.EngineStatus != VehicleEngineStatus.Running &&
                   simulation.State.ElapsedSeconds - crankStart < 20f && Time.realtimeSinceStartupAsDouble < crankDeadline)
            {
                Assert.That(keyTarget.ContinueContinuousInteraction(Time.unscaledDeltaTime), Is.True);
                yield return null;
                observedCranking |= simulation.State.EngineStatus == VehicleEngineStatus.Cranking;
                AssertNativeAuthority();
            }
            Assert.That(observedCranking, Is.True, Describe("never observed Cranking"));
            Assert.That(simulation.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running), Describe("start deadline"));
            Assert.That(simulation.Root.Prerequisites.CanRun, Is.True);
            keyTarget.EndContinuousInteraction();
            Assert.That(ignition.State, Is.EqualTo(SatsumaIgnitionState.Accessory));
            Assert.That(ignition.IsHeld, Is.False);
            Assert.That(ignition.StarterRequested, Is.False);
            yield return Frames(2);
            Assert.That(simulation.LastInput.StarterRequested, Is.False);

            // Catch alone is insufficient: the released key must leave the
            // engine alive for three simulated seconds without throttle/starter.
            int idleFirstTick = simulation.FixedTickCount;
            yield return RunForSeconds(3f, 10d, "released-key idle", () =>
            {
                Assert.That(simulation.LastInput.StarterRequested, Is.False);
                Assert.That(simulation.LastInput.Throttle01, Is.Zero);
                Assert.That(simulation.State.EngineRpm, Is.GreaterThan(0f));
            });
            Assert.That(simulation.FixedTickCount - idleFirstTick, Is.GreaterThanOrEqualTo(100));
            float idleRpm = simulation.State.EngineRpm;
            Assert.That(idleRpm, Is.InRange(originalConfig.Engine.IdleTargetRpm * .5f,
                originalConfig.Engine.IdleTargetRpm * 1.5f), Describe("idle range"));

            InteractionContext throttleContext = ContextNear(throttleTarget.transform);
            Assert.That(throttleTarget.CanBeginContinuousInteraction(throttleContext,
                ContinuousContextInteractionDirection.Primary), Is.True);
            throttleTarget.BeginContinuousInteraction(throttleContext, ContinuousContextInteractionDirection.Primary);
            Assert.That(throttleTarget.RequestedThrottle01, Is.EqualTo(1f));
            float peakRpm = idleRpm;
            yield return RunForSeconds(2f, 10d, "stock carburetor rev", () =>
            {
                Assert.That(throttleTarget.ContinueContinuousInteraction(Time.deltaTime), Is.True);
                peakRpm = Mathf.Max(peakRpm, simulation.State.EngineRpm);
            });
            Assert.That(simulation.LastInput.Throttle01, Is.EqualTo(1f));
            Assert.That(simulation.State.FilteredThrottle01, Is.GreaterThan(.9f));
            Assert.That(peakRpm, Is.GreaterThan(idleRpm + 500f), Describe("throttle did not raise RPM"));
            throttleTarget.EndContinuousInteraction();
            Assert.That(throttleTarget.RequestedThrottle01, Is.Zero);
            yield return RunForSeconds(5f, 12d, "return to idle", null);
            Assert.That(simulation.LastInput.Throttle01, Is.Zero);
            Assert.That(simulation.State.EngineRpm, Is.LessThan(peakRpm - 300f));
            Assert.That(simulation.State.EngineRpm, Is.InRange(originalConfig.Engine.IdleTargetRpm * .5f,
                originalConfig.Engine.IdleTargetRpm * 1.5f), Describe("returned idle range"));

            keyContext = ContextNear(keyTarget.transform);
            keyTarget.BeginContinuousInteraction(keyContext, ContinuousContextInteractionDirection.Primary);
            keyTarget.EndContinuousInteraction();
            Assert.That(ignition.State, Is.EqualTo(SatsumaIgnitionState.Off));
            yield return Frames(2);
            Assert.That(simulation.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Off));
            Assert.That(simulation.LastInput.IgnitionOn, Is.False);
            Assert.That(simulation.LastInput.StarterRequested, Is.False);
            float stopStart = simulation.State.ElapsedSeconds;
            double stopDeadline = Time.realtimeSinceStartupAsDouble + 10d;
            while (simulation.State.EngineRpm > 0f && simulation.State.ElapsedSeconds - stopStart < 15f &&
                   Time.realtimeSinceStartupAsDouble < stopDeadline)
            {
                Assert.That(simulation.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Off));
                AssertNativeAuthority();
                yield return null;
            }
            Assert.That(simulation.State.EngineRpm, Is.Zero, Describe("key-off rundown deadline"));
            AssertPreparedServiceVolumes();
            AssertNativeAuthority();
            foreach (WorldItemInstance plug in plugs) AssertRealPlugOwnership(plug);
            Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousProviderHub));
        }

        private void CreateFixture()
        {
            previousTimeScale = Time.timeScale;
            previousCaptureDeltaTime = Time.captureDeltaTime;
            previousFixedDeltaTime = Time.fixedDeltaTime;
            previousProviderHub = ItemPresentationProviderHub.Current;
            fixtureStarted = true;
            Time.timeScale = 0f;
            Time.captureDeltaTime = .02f;
            Time.fixedDeltaTime = .02f;
            scene = SceneManager.CreateScene("Purchased plug ignition " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            GameObject holder = SceneRoot("Inactive canonical engine fixture");
            holder.SetActive(false);
            holder.transform.position = new Vector3(100f, 100f, 100f);
            vehicle = Object.Instantiate(RequireAsset<GameObject>(VehicleRoot +
                "/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab"), holder.transform, false);
            assembly = vehicle.GetComponent<VehicleAssemblyController>();
            assembly.Initialize();
            Assert.That(assembly.Parts, Has.Length.EqualTo(126));
            Assert.That(assembly.MountPoints, Has.Length.EqualTo(124));
            Assert.That(assembly.Graph.Mounts.Sum(mount => mount.Fasteners.Length), Is.EqualTo(294));
            simulation = vehicle.GetComponent<VehicleSimulationHost>();
            simulation.enabled = false;
            Assert.That(simulation.TryInitialize(out string failure), Is.True, failure);
            operatingSource = vehicle.GetComponent<SatsumaEngineOperatingSource>();
            Assert.That(operatingSource, Is.Not.Null);
            Assert.That(simulation.SatsumaOperatingSourceComponent, Is.SameAs(operatingSource));
            Assert.That(simulation.Root.SatsumaOperatingModel, Is.Not.Null);
            originalConfig = simulation.Config;
            originalBackend = simulation.BackendComponent as NwhWheelPhysicsBackend;
            Assert.That(originalBackend, Is.Not.Null, "Do not replace the authored wheel backend with a smoke stub.");
            input = vehicle.GetComponent<SatsumaIgnitionInputAdapter>();
            ignition = vehicle.GetComponent<SatsumaIgnitionController>();
            keyTarget = vehicle.GetComponentsInChildren<SatsumaIgnitionInteractionTarget>(true).Single();
            throttleTarget = input.CarburetorThrottle;
            Assert.That(throttleTarget, Is.Not.Null);
            prerequisites = simulation.PrerequisiteSource;
            Assert.That(prerequisites.UsesSatsumaAssemblyRequirements, Is.True);
            PrepareStockGraph();
            foreach (PartInstance part in assembly.Parts)
                if (!part.IsAssemblyRoot && !part.IsInstalled) part.gameObject.SetActive(false);
            // NWH requires a dynamic chassis. No local PhysicsScene.Simulate is
            // invoked; FreezeAll is an extra stationary-fixture constraint, not
            // a replacement engine/wheel backend or accepted driving behavior.
            Rigidbody chassis = vehicle.GetComponent<Rigidbody>();
            Assert.That(chassis.isKinematic, Is.False);
            chassis.constraints = RigidbodyConstraints.FreezeAll;
            chassis.useGravity = false;
            input.Router.enabled = false; // Foot controls remain neutral; the real carb target supplies throttle.
            foreach (MonoBehaviour component in vehicle.GetComponents<MonoBehaviour>())
                if (component != null && component.GetType().FullName == "MSC.Weather.Production.VehicleGlassRainPresenter")
                    component.enabled = false; // No GPU rain atlas in this headless mechanical fixture.
            vehicle.GetComponent<VehiclePersistenceBinding>().BindKeyAccess(new SatsumaKeyAccessState());
            VehicleSimulationStateDto initial = simulation.State.CaptureDto();
            Assert.That(initial.engineStatus, Is.EqualTo(VehicleEngineStatus.Off));
            Assert.That(initial.engineRpm, Is.Zero);
            initial.fuelLiters = 0f;
            initial.oilLiters = 3f;
            initial.coolantLiters = 5.4f;
            initial.engineTemperatureCelsius = 80f; // Serviced warm-start fixture, not an edited user save.
            Assert.That(simulation.TryRestoreSimulationState(initial, out failure), Is.True, failure);
            Assert.That(ignition.Electrical.TryRestore(new SatsumaElectricalSaveDto
            {
                installedConnectionIds = new[] { "BatteryHarness", "GroundBattery", "Ignition", "Starter",
                    "CoilHarness", "Alternator", "RegulatorHarness", "RadiatorFan" },
                batteryPlusStage = SatsumaElectricalSystem.FastenerMaximumStage,
                batteryMinusStage = SatsumaElectricalSystem.FastenerMaximumStage,
                starterCableStage = SatsumaElectricalSystem.FastenerMaximumStage,
            }, out failure), Is.True, failure);
            prerequisites.SetFuelReadinessTestOverride(true); // Petrol-only scope boundary; operating fluids stay authoritative.
            ConfigurePurchasedItemRuntime();
            holder.SetActive(true); // Actual prefab Awake/OnEnable, including NWH/lock/input adapter.
            Assert.That(simulation.IsInitialized, Is.True);
            Assert.That(assembly.isActiveAndEnabled, Is.True);
            AssemblyEngineAdjustmentState filter = assembly.Parts.Single(value =>
                value.Definition.DefinitionId == PartPrefix + "oilfilter0").GetComponent<AssemblyEngineAdjustmentState>();
            for (int stage = 0; stage < 8; stage++) Assert.That(filter.TryAdjust(1f), Is.True);
            interactor = SceneRoot("Physical plug handoff and engine action context");
            Transform anchor = new GameObject("Carry anchor").transform;
            anchor.SetParent(interactor.transform, false);
            anchor.localPosition = Vector3.forward * .3f;
            carry = interactor.AddComponent<PhysicalCarryController>();
            carry.Configure(anchor, null);
        }

        private void PrepareStockGraph()
        {
            VehicleAssemblySaveData data = assembly.CaptureSaveData();
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (string suffix in StockParts)
            {
                string id = PartPrefix + suffix;
                MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                    value.Definition.AcceptedPartDefinitionIds.Contains(id));
                PartSaveDto part = data.parts.Single(value => value.partDefinitionId == id);
                part.lifecycleState = PartLifecycleState.Installed;
                part.installedMountId = mount.MountId;
                part.worldPosition = mount.Pose.position;
                part.worldRotation = mount.Pose.rotation;
                if (id == AssemblyCamshaftTimingState.PartDefinitionId)
                {
                    part.hasCamshaftTiming = true;
                    part.camshaftTiming = new AssemblyCamshaftTimingSaveDto { angleDegrees = 0f };
                }
                data.mounts.Single(value => value.mountId == mount.MountId).installedPartStableEntityId = part.stableEntityId;
                occupied.Add(mount.MountId);
            }
            foreach (FastenerSaveDto bolt in data.fasteners)
            {
                if (!occupied.Contains(bolt.mountId)) continue;
                Assert.That(assembly.Graph.TryGetMount(bolt.mountId, out MountPointRuntime mount), Is.True);
                Assert.That(mount.TryGetFastener(bolt.fastenerDefinitionId, out FastenerInstance fastener), Is.True);
                bolt.inserted = bolt.seated = true;
                bolt.stage = fastener.Definition.MaximumStage;
            }
            foreach (FastenerGroupSaveDto savedGroup in data.fastenerGroups)
            {
                if (!occupied.Contains(savedGroup.mountId)) continue;
                Assert.That(assembly.Graph.TryGetMount(savedGroup.mountId, out MountPointRuntime mount), Is.True);
                FastenerGroupDefinition definition = mount.FastenerGroup.Definition;
                int tightness = data.fasteners.Where(value => value.mountId == mount.MountId &&
                    definition.FastenerDefinitionIds.Contains(value.fastenerDefinitionId)).Sum(value => value.stage);
                savedGroup.isBolted = definition.HasFasteners && tightness >= definition.BoltedOnThreshold;
                Assert.That(definition.IsLatchConsistent(tightness, savedGroup.isBolted, true), Is.True, mount.MountId);
            }
            AssemblyOperationResult restored = assembly.RestoreSaveData(data);
            Assert.That(restored.Succeeded, Is.True, restored.Message);
            Assert.That(assembly.Graph.IsPartDefinitionBolted(SatsumaEngineAssemblyReadiness.BlockId), Is.True);
        }

        private void ConfigurePurchasedItemRuntime()
        {
            ItemDefinitionCatalog definitions = RequireAsset<ItemDefinitionCatalog>(
                "Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset");
            Assert.That(definitions.TryGet(PlugItemId, out ItemDefinitionRecord definition), Is.True);
            GameObject providerRoot = SceneRoot("Inactive instance-only purchased plug provider");
            providerRoot.SetActive(false);
            var provider = providerRoot.AddComponent<ItemPresentationProvider>();
            plugVisualPrefab = RequireAsset<GameObject>(ItemsRoot + "/Prefabs/LegacyItemVisual_item_spark-plug.prefab");
            var binding = new ItemPresentationBinding();
            binding.Configure(PlugItemId, definition.ReplacementKey, plugVisualPrefab);
            Assert.That(definitions.TryGet(BeltItemId, out ItemDefinitionRecord beltDefinition), Is.True);
            var beltBinding = new ItemPresentationBinding();
            beltBinding.Configure(BeltItemId, beltDefinition.ReplacementKey,
                RequireAsset<GameObject>(ItemsRoot + "/Prefabs/LegacyItemVisual_item_alternator-belt.prefab"));
            provider.Configure("39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31", new[] { binding, beltBinding });
            var placements = NewAsset<ItemPlacementCatalog>();
            placements.ConfigureForAuthoring("test.playmode.engine-plugs.placements", "msc-world-baseline-04a1.1-c3f2f337",
                Array.Empty<ItemPlacementRecord>());
            var manifest = NewAsset<ProductionWorldStreamingManifest>();
            manifest.ConfigureForAuthoring(512f, 0, 1, Array.Empty<ProductionWorldCellScene>());
            var streaming = SceneRoot("Disabled isolated item streaming").AddComponent<ProductionWorldStreamingService>();
            streaming.ConfigureForAuthoring(manifest);
            streaming.enabled = false;
            items = SceneRoot("Real purchased plug item runtime").AddComponent<ItemWorldRuntime>();
            items.Initialize(definitions, placements, streaming, scene, -10000f);
            FieldInfo providerField = typeof(ItemWorldRuntime).GetField("presentationProvider", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(providerField, Is.Not.Null);
            // Existing fixture-only injection avoids replacing the global hub;
            // the provider, wrapper definitions and bridge are actual runtime types.
            providerField.SetValue(items, provider);
            VehicleItemAssemblyBridge bridge = vehicle.GetComponent<VehicleItemAssemblyBridge>();
            Assert.That(bridge.Catalog, Is.SameAs(RequireAsset<VehicleItemPartCatalog>(VehicleRoot + "/VehicleItemPartCatalog.asset")));
            bridge.BindRuntime(items);
        }

        private IEnumerator InstallAndTensionPurchasedBelt()
        {
            MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                value.MountId == SatsumaConsumableAssemblyRules.BeltMountId);
            StableEntityId identity = ItemStableIdUtility.CreateDeterministic("test.playmode.engine.belt");
            WorldItemInstance belt = items.SpawnDynamic(BeltItemId, identity, mount.Pose.position, mount.Pose.rotation, scene);
            PartInstance part = belt.GetComponent<PartInstance>();
            Assert.That(part, Is.Not.Null);
            Assert.That(part.Definition.DefinitionId, Is.EqualTo(SatsumaConsumableAssemblyRules.BeltPartId));
            Assert.That(part.StableId, Is.EqualTo(belt.StableId));
            Assert.That(part.GetComponent<IAssemblyItemCondition>(), Is.SameAs(belt.GetComponent<ItemPartPresentationBinding>()));
            Assert.That(belt.PresentationRoot.GetComponentsInChildren<ItemProxyPresentation>(true), Is.Empty);
            Assert.That(belt.PresentationRoot.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
            InteractionContext context = ContextNear(mount.Pose);
            var handoff = mount.GetComponent<AssemblyMountHandoffTarget>();
            Assert.That(carry.TryPickup(belt.GetComponent<PhysicsPickupTarget>(), context), Is.True);
            Assert.That(handoff.CanAccept(carry.HeldTarget, context), Is.True, handoff.HandoffPrompt);
            Assert.That(carry.TryHandoff(handoff, context), Is.True);
            double deadline = Time.realtimeSinceStartupAsDouble + VehicleAssemblyController.InstallTransitionDurationSeconds + 3d;
            while (!part.IsInstalled && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(part.IsInstalled, Is.True, assembly.LastOperationResult.Message);
            Assert.That(assembly.ResolveMount(mount).InstalledPart, Is.SameAs(part));

            AssemblyEngineAdjustmentState alternator = assembly.Parts.Single(value =>
                value.Definition.DefinitionId == PartPrefix + "alternator").GetComponent<AssemblyEngineAdjustmentState>();
            string clampId = SatsumaEngineAdjustmentRules.ClampFastenerId(SatsumaEngineAdjustmentKind.Alternator);
            Assert.That(assembly.Graph.TryGetMount(SatsumaConsumableAssemblyRules.AlternatorMountId, out MountPointRuntime bracket), Is.True);
            Assert.That(bracket.TryGetFastener(clampId, out FastenerInstance clamp), Is.True);
            ToolDefinition tool = assembly.Tools.First(value => clamp.Definition.ToolRule.Matches(value));
            Assert.That(assembly.TryOperateFastener(bracket.MountId, clampId, tool, false).Succeeded, Is.True);
            Assert.That(clamp.Stage, Is.EqualTo(7));
            Assert.That(alternator.Setting, Is.EqualTo(2f));
            for (int notch = 0; notch < 10; notch++) Assert.That(alternator.TryAdjust(-1f), Is.True);
            Assert.That(alternator.Setting, Is.EqualTo(7f));
            Assert.That(assembly.TryOperateFastener(bracket.MountId, clampId, tool, true).Succeeded, Is.True);
            Assert.That(clamp.Stage, Is.EqualTo(8));
            Assert.That(belt.ConditionPercent, Is.EqualTo(100f));
        }

        private IEnumerator InstallPurchasedPlug(int cylinder)
        {
            MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                value.MountId == SatsumaConsumableAssemblyRules.SparkPlugMountId(cylinder));
            StableEntityId identity = ItemStableIdUtility.CreateDeterministic("test.playmode.engine.plug." + cylinder);
            WorldItemInstance item = items.SpawnDynamic(PlugItemId, identity, mount.Pose.position,
                mount.Pose.rotation, scene);
            plugs.Add(item);
            AssertRealPlugOwnership(item);
            var pickup = item.GetComponent<PhysicsPickupTarget>();
            var part = item.GetComponent<PartInstance>();
            var handoff = mount.GetComponent<AssemblyMountHandoffTarget>();
            Assert.That(handoff, Is.Not.Null);
            InteractionContext context = ContextNear(mount.Pose);
            Assert.That(carry.TryPickup(pickup, context), Is.True);
            Assert.That(handoff.CanAccept(carry.HeldTarget, context), Is.True, handoff.HandoffPrompt);
            Assert.That(carry.TryHandoff(handoff, context), Is.True);
            Assert.That(carry.HasHeldObject, Is.False);
            double deadline = Time.realtimeSinceStartupAsDouble + VehicleAssemblyController.InstallTransitionDurationSeconds + 3d;
            while (!part.IsInstalled && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(part.IsInstalled, Is.True, assembly.LastOperationResult.Message);
            Assert.That(assembly.ResolveMount(mount).InstalledPart, Is.SameAs(part));
            AssertRealPlugOwnership(item);
            ToolDefinition tool = assembly.Tools.Single(value => value.DefinitionId ==
                SatsumaAuxiliaryAssemblyTools.SparkPlugWrenchDefinitionId);
            for (int stage = 0; stage < SatsumaConsumableAssemblyRules.SparkPlugMaximumStage; stage++)
            {
                AssemblyOperationResult result = assembly.TryTurnFastener(mount.MountId,
                    SatsumaConsumableAssemblyRules.SparkPlugFastenerId(cylinder), tool, FastenerRotationDirection.Clockwise);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
            MountPointRuntime installed = assembly.ResolveMount(mount);
            Assert.That(installed.Fasteners.Single().Stage, Is.EqualTo(SatsumaConsumableAssemblyRules.SparkPlugMaximumStage));
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(assembly.Graph), Is.EqualTo((1 << cylinder) - 1));
        }

        private void AssertRealPlugOwnership(WorldItemInstance item)
        {
            Assert.That(item.Definition.DefinitionId, Is.EqualTo(PlugItemId));
            var part = item.GetComponent<PartInstance>();
            Assert.That(part, Is.Not.Null);
            Assert.That(part.Definition, Is.SameAs(RequireAsset<PartDefinition>(VehicleRoot +
                "/LoosePartDefinitions/vehicle.satsuma.part.spark-plug.asset")));
            Assert.That(part.Body, Is.SameAs(item.GetComponent<Rigidbody>()));
            Assert.That(part.PickupTarget, Is.SameAs(item.GetComponent<PhysicsPickupTarget>()));
            Assert.That(part.StableId, Is.EqualTo(item.StableId));
            Assert.That(part.GetComponent<IAssemblyItemCondition>(), Is.SameAs(item.GetComponent<ItemPartPresentationBinding>()));
            Assert.That(item.GetComponent<ItemPartPresentationBinding>(), Is.Not.Null);
            Assert.That(item.ConditionPercent, Is.GreaterThanOrEqualTo(1f));
            Assert.That(item.IsBroken, Is.False);
            Assert.That(item.PresentationRoot, Is.Not.Null);
            Assert.That(item.PresentationRoot.GetComponentsInChildren<MeshFilter>(true).Select(value => value.sharedMesh),
                Is.EquivalentTo(plugVisualPrefab.GetComponentsInChildren<MeshFilter>(true).Select(value => value.sharedMesh)));
            Assert.That(item.PresentationRoot.GetComponentsInChildren<MeshFilter>(true), Is.Not.Empty);
            Assert.That(item.PresentationRoot.GetComponentsInChildren<ItemProxyPresentation>(true), Is.Empty);
            Assert.That(items.TryGetInstance(item.StableId.Value, out WorldItemInstance registered), Is.True);
            Assert.That(registered, Is.SameAs(item));
            Assert.That(assembly.AllRuntimeParts.Count(value => value.StableId == item.StableId), Is.EqualTo(1));
            Assert.That(assembly.Parts.Contains(part), Is.False);
            if (part.IsInstalled)
            {
                MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                    value.MountId == part.RuntimeState.InstalledMountId);
                Assert.That(part.gameObject.activeInHierarchy, Is.True,
                    "Native pose coverage requires an active purchased plug actor.");
                Assert.That(Vector3.Distance(part.Body.position, mount.Pose.position), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(part.Body.rotation, mount.Pose.rotation), Is.LessThan(.001f));
            }
        }

        private IEnumerator RunForSeconds(float seconds, double maximumRealtime, string stage, Action sample)
        {
            float start = simulation.State.ElapsedSeconds;
            double deadline = Time.realtimeSinceStartupAsDouble + maximumRealtime;
            while (simulation.State.ElapsedSeconds - start < seconds && Time.realtimeSinceStartupAsDouble < deadline)
            {
                Assert.That(simulation.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running), Describe(stage));
                AssertNativeAuthority();
                sample?.Invoke();
                yield return null;
            }
            Assert.That(simulation.State.ElapsedSeconds - start, Is.GreaterThanOrEqualTo(seconds), Describe(stage + " deadline"));
            Assert.That(simulation.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running), Describe(stage));
        }

        private void AssertNativeAuthority()
        {
            Assert.That(simulation.Config, Is.SameAs(originalConfig));
            Assert.That(simulation.BackendComponent, Is.SameAs(originalBackend));
            Assert.That(simulation.Backend, Is.SameAs(originalBackend));
            Assert.That(simulation.InputSourceComponent, Is.SameAs(input));
            Assert.That(simulation.InputSource, Is.SameAs(input));
            Assert.That(simulation.PrerequisiteSource, Is.SameAs(prerequisites));
            Assert.That(simulation.SatsumaOperatingSourceComponent, Is.SameAs(operatingSource));
            Assert.That(simulation.Root.SatsumaOperatingModel, Is.Not.Null);
            Assert.That(operatingSource.DebugAutocharge, Is.False);
            Assert.That(input.CarburetorThrottle, Is.SameAs(throttleTarget));
            Assert.That(input.Ignition, Is.SameAs(ignition));
            Assert.That(simulation.Root.LastTickWasFinite, Is.True, Describe("nonfinite host tick"));
            Assert.That(simulation.State.SelectedGear, Is.Zero);
            Assert.That(originalBackend.Chassis.isKinematic, Is.False);
            Assert.That(originalBackend.Chassis.constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll));
        }

        private void AssertPreparedServiceVolumes()
        {
            Assert.That(simulation.State.FuelLiters, Is.Zero);
            Assert.That(simulation.State.OilLiters, Is.InRange(2.99f, 3f));
            Assert.That(simulation.State.CoolantLiters, Is.InRange(5.39f, 5.4f));
        }

        private InteractionContext ContextNear(Transform target)
        {
            interactor.transform.position = target.position + Vector3.up * .15f + Vector3.back * .3f;
            interactor.transform.LookAt(target.position);
            return new InteractionContext(interactor, interactor.transform.position, interactor.transform.forward);
        }

        private string Describe(string stage) => stage + ": lock=" + ignition.State +
            ", engine=" + simulation.State.EngineStatus + ", rpm=" + simulation.State.EngineRpm +
            ", ticks=" + simulation.FixedTickCount + ", failures=" + simulation.Root.Prerequisites.FailureFlags;

        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        private GameObject SceneRoot(string name)
        {
            var result = new GameObject(name);
            SceneManager.MoveGameObjectToScene(result, scene);
            return result;
        }
        private T NewAsset<T>() where T : ScriptableObject
        {
            T result = ScriptableObject.CreateInstance<T>();
            ownedAssets.Add(result);
            return result;
        }
        private static T RequireAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Required private generated asset: " + path);
            return asset;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var failures = new List<Exception>();
            void TryCleanup(Action action) { try { action(); } catch (Exception failure) { failures.Add(failure); } }
            AsyncOperation unload = null;
            try
            {
                if (simulation != null) simulation.enabled = false;
                TryCleanup(() => keyTarget?.EndContinuousInteraction());
                TryCleanup(() => throttleTarget?.EndContinuousInteraction());
                TryCleanup(() => prerequisites?.SetFuelReadinessTestOverride(false));
                TryCleanup(() => { if (carry != null && carry.HasHeldObject) carry.Drop(); });
                TryCleanup(() =>
                {
                    if (assembly != null)
                        Assert.That(assembly.TrySetDynamicPartRegistrationsForRestore(Array.Empty<DynamicPartRegistration>(),
                            out string failure), Is.True, failure);
                });
                if (items != null)
                    foreach (WorldItemInstance item in items.LoadedInstances.ToArray())
                        TryCleanup(() => Assert.That(items.TryRemoveDynamic(item), Is.True));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    foreach (GameObject root in scene.GetRootGameObjects()) TryCleanup(() => root.SetActive(false));
                    TryCleanup(() => unload = SceneManager.UnloadSceneAsync(scene));
                }
                if (fixtureStarted)
                {
                    Time.timeScale = previousTimeScale;
                    Time.captureDeltaTime = previousCaptureDeltaTime;
                    Time.fixedDeltaTime = previousFixedDeltaTime;
                }
            }
            try
            {
                if (unload != null) yield return unload;
                yield return null;
            }
            finally
            {
                foreach (ScriptableObject asset in ownedAssets) TryCleanup(() => { if (asset != null) Object.Destroy(asset); });
                ownedAssets.Clear();
                if (fixtureStarted) TryCleanup(() => Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousProviderHub)));
                if (failures.Count > 0) throw new AggregateException("Purchased plug engine fixture cleanup failed.", failures);
            }
        }
    }
}
#endif
