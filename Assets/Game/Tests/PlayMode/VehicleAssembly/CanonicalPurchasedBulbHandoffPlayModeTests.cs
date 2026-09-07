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
using MSC.Interaction.Query;
using MSC.Items;
using MSC.Items.Presentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.ItemsIntegration;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    // Editor PlayMode consumes the private generated assets in an empty scene.
    // It never opens Bootstrap, creates a native save session or edits asset bindings.
    public sealed class CanonicalPurchasedBulbHandoffPlayModeTests
    {
        private const string VehicleRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        private const string ItemsRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items";
        private const string BulbId = "item.light-bulb";
        private const string SocketId = "mount.satsuma.headlight-left.light-bulb";
        private readonly List<ScriptableObject> ownedAssets = new List<ScriptableObject>();
        private Scene scene;
        private float previousTimeScale;
        private bool fixtureStarted;
        private IItemPresentationProvider previousHub;
        private GameObject vehicle;
        private GameObject player;
        private VehicleAssemblyController assembly;
        private ItemWorldRuntime runtime;
        private PhysicalCarryController carry;
        private RaycastInteractionCandidateSource query;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var failures = new List<Exception>();
            AsyncOperation unload = null;
            void CleanupStep(Action action)
            {
                try { action(); }
                catch (Exception exception) { failures.Add(exception); }
            }

            try
            {
                CleanupStep(() => { if (carry != null && carry.HasHeldObject) carry.Drop(); });
                CleanupStep(() =>
                {
                    if (assembly != null)
                        assembly.TrySetDynamicPartRegistrationsForRestore(Array.Empty<DynamicPartRegistration>(), out _);
                });
                CleanupStep(() =>
                {
                    if (runtime != null)
                        foreach (WorldItemInstance item in runtime.LoadedInstances.ToArray())
                            CleanupStep(() => runtime.TryRemoveDynamic(item));
                });
            }
            finally
            {
                // A failed item cleanup must not strand the active car or its
                // detached loose parts. Awaiting the operation happens below.
                if (scene.IsValid() && scene.isLoaded)
                {
                    CleanupStep(() =>
                    {
                        foreach (GameObject root in scene.GetRootGameObjects())
                            CleanupStep(() => root.SetActive(false));
                    });
                    CleanupStep(() => unload = SceneManager.UnloadSceneAsync(scene));
                }
                if (fixtureStarted) Time.timeScale = previousTimeScale;
            }

            try
            {
                if (unload != null) yield return unload;
                yield return null;
            }
            finally
            {
                foreach (ScriptableObject asset in ownedAssets)
                    CleanupStep(() => { if (asset != null) Object.Destroy(asset); });
                ownedAssets.Clear();
                if (fixtureStarted)
                {
                    Time.timeScale = previousTimeScale;
                    CleanupStep(() => Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousHub)));
                }
                // Keep failures visible even if the runner disposes this iterator
                // while waiting for the scene unload rather than resuming it.
                if (failures.Count == 1)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
                if (failures.Count > 1)
                    throw new AggregateException("Purchased bulb fixture cleanup failed.", failures);
            }
        }

        [UnityTest]
        public IEnumerator CarriedRealBulbUsesCanonicalRayHandoffAndLiveHeadlightCondition()
        {
            CreateFixture();
            InstallCanonicalPart("vehicle.satsuma.part.headlight-left", "mount.satsuma.headlight-left");
            InstallCanonicalPart("vehicle.satsuma.part.headlight-right", "mount.satsuma.headlight-right");
            InstallCanonicalPart("vehicle.satsuma.part.battery", "mount.satsuma.battery");
            InstallCanonicalPart("vehicle.satsuma.part.dashboard", "mount.satsuma.dashboard");
            InstallCanonicalPart("vehicle.satsuma.part.dashboard-meters", "mount.satsuma.dashboard.meters");
            TightenCanonicalMount("mount.satsuma.dashboard.meters");
            foreach (PartInstance part in assembly.Parts)
                if (!part.IsAssemblyRoot && !part.IsInstalled) part.gameObject.SetActive(false);

            var electrical = vehicle.GetComponent<SatsumaElectricalSystem>();
            Assert.That(electrical.TryRestore(new SatsumaElectricalSaveDto
            {
                installedConnectionIds = new[] { "BatteryHarness", "GroundBattery", "Ignition",
                    "FrontLightsHarness", "SwitchLights", "HeadlightLeft", "HeadlightRight" },
                batteryPlusStage = SatsumaElectricalSystem.FastenerMaximumStage,
                batteryMinusStage = SatsumaElectricalSystem.FastenerMaximumStage,
            }, out string failure), Is.True, failure);
            Assert.That(electrical.ElectricsOk, Is.True);
            var controls = vehicle.GetComponent<SatsumaDashboardControlsController>();
            Assert.That(controls.CanOperate, Is.True);
            Assert.That(controls.HeadlightsMode, Is.EqualTo(SatsumaHeadlightsMode.Off));
            Assert.That(controls.TryCycleLights(), Is.True);
            Assert.That(controls.TryCycleLights(), Is.True);
            var lighting = vehicle.GetComponent<SatsumaDashboardLightingPresenter>();
            SatsumaDashboardLampBinding leftLamp = lighting.Lamps.Single(value =>
                value.StableLampId == "presentation.satsuma.headlight.left");
            SatsumaDashboardLampBinding rightLamp = lighting.Lamps.Single(value =>
                value.StableLampId == "presentation.satsuma.headlight.right");
            Assert.That(leftLamp.BulbMountId, Is.EqualTo(SocketId));
            Assert.That(leftLamp.RequiresBoltedOwner, Is.True);
            Assert.That(leftLamp.Light, Is.Not.Null);
            yield return null;
            yield return null;
            Assert.That(leftLamp.Light.enabled, Is.False, "The real lamp cannot light an empty socket.");

            MountPointAuthoring socket = assembly.MountPoints.Single(value => value.MountId == SocketId);
            var identity = ItemStableIdUtility.CreateDeterministic("test.playmode.real-bulb.handoff");
            Vector3 carriedPosition = socket.Pose.position + vehicle.transform.forward * .15f;
            WorldItemInstance item = runtime.SpawnDynamic(BulbId, identity, carriedPosition,
                socket.Pose.rotation * Quaternion.Euler(0f, 35f, 0f), scene);
            PartInstance wrapper = item.GetComponent<PartInstance>();
            Rigidbody body = item.GetComponent<Rigidbody>();
            PhysicsPickupTarget pickup = item.GetComponent<PhysicsPickupTarget>();
            StableEntityIdAuthoring stableIdentity = item.GetComponent<StableEntityIdAuthoring>();
            GameObject visual = item.PresentationRoot;
            Assert.That(wrapper.Definition, Is.SameAs(RequireAsset<PartDefinition>(VehicleRoot +
                "/LoosePartDefinitions/vehicle.satsuma.part.light-bulb.asset")));
            AssertRealPresentation(visual);
            AssertWrapperOwnership(item, wrapper, body, pickup, stableIdentity, visual, identity);

            SphereCollider marker = socket.GetComponent<SphereCollider>();
            Assert.That(marker, Is.Not.Null, "Use the authored socket marker, not a test-created ray target.");
            Assert.That(marker.enabled && marker.isTrigger, Is.True);
            Vector3 aim = marker.transform.TransformPoint(marker.center);
            player.transform.position = aim + vehicle.transform.forward * .4f + vehicle.transform.up * .15f;
            player.transform.LookAt(aim);
            var context = new InteractionContext(player, player.transform.position, player.transform.forward);
            Assert.That(carry.TryPickup(pickup, context), Is.True);
            Assert.That(carry.HeldTarget, Is.SameAs(pickup));
            Assert.That(carry.HeldBody, Is.SameAs(body));
            query.SetCarriedObjectTargetsEnabled(true);
            query.SetCarriedObjectTarget(carry.HeldTarget);
            query.SetIgnoredBody(carry.HeldBody);
            Physics.SyncTransforms();
            InteractionCandidate candidate = query.Query();
            Assert.That(candidate.TryGetCapability(out IMountHandoffTarget handoff), Is.True,
                "Actual ray hit: " + (query.HasLastHit ? query.LastHit.collider.name : "none"));
            Assert.That(handoff, Is.SameAs(socket.GetComponent<AssemblyMountHandoffTarget>()));
            Assert.That(handoff.CanAccept(carry.HeldTarget, context), Is.True, handoff.HandoffPrompt);
            var installedActions = new List<AssemblyActionCompleted>();
            assembly.ActionCompleted += action =>
            {
                if (action.Action == AssemblyActionKind.PartInstalled && action.Part == wrapper) installedActions.Add(action);
            };
            int firstFrame = Time.frameCount;
            Assert.That(carry.TryHandoff(handoff, context), Is.True);
            Assert.That(carry.HasHeldObject, Is.False);
            Assert.That(assembly.LastOperationResult.Succeeded, Is.True, assembly.LastOperationResult.Message);
            Assert.That(wrapper.IsInstalled, Is.False, "An active PlayMode aggregate must start its transition coroutine.");
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.detectCollisions, Is.False);
            Assert.That(assembly.ResolveMount(socket).IsOccupied, Is.False);

            // Unscaled rendered frames keep the actual transition live while unrelated car physics is paused.
            float deadline = Time.realtimeSinceStartup + VehicleAssemblyController.InstallTransitionDurationSeconds + 2f;
            while (!wrapper.IsInstalled && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(firstFrame));
            Assert.That(wrapper.IsInstalled, Is.True, assembly.LastOperationResult.Message);
            Assert.That(assembly.ResolveMount(socket).InstalledPart, Is.SameAs(wrapper));
            Assert.That(wrapper.RuntimeState.InstalledMountId, Is.EqualTo(SocketId));
            Assert.That(Vector3.Distance(wrapper.transform.position, socket.Pose.position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(wrapper.transform.rotation, socket.Pose.rotation), Is.LessThan(.001f));
            Assert.That(body.gameObject.activeInHierarchy, Is.True,
                "Native pose coverage requires an active purchased bulb actor.");
            Assert.That(Vector3.Distance(body.position, socket.Pose.position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(body.rotation, socket.Pose.rotation), Is.LessThan(.001f));
            Assert.That(installedActions, Has.Count.EqualTo(1));
            AssertWrapperOwnership(item, wrapper, body, pickup, stableIdentity, visual, identity);
            Assert.That(leftLamp.Light.enabled, Is.False, "A bulb cannot bypass the housing's real fastener group.");

            TightenCanonicalMount("mount.satsuma.headlight-left");
            TightenCanonicalMount("mount.satsuma.headlight-right");
            FastenerSaveDto[] headBolts = assembly.CaptureSaveData().fasteners.Where(value =>
                value.mountId == "mount.satsuma.headlight-left" || value.mountId == "mount.satsuma.headlight-right").ToArray();
            Assert.That(headBolts, Has.Length.EqualTo(4));
            Assert.That(headBolts.All(value => value.stage == 8 && value.inserted && value.seated), Is.True);
            yield return null;
            yield return null;
            Assert.That(leftLamp.Light.enabled, Is.True, "Existing LateUpdate must observe the installed real bulb and bolted housing.");
            Assert.That(rightLamp.Light.enabled, Is.False, "The wired, bolted opposite housing still has no bulb.");

            SetCondition(item, 6.99f, false);
            yield return null;
            yield return null;
            Assert.That(leftLamp.Light.enabled, Is.False, "Items condition below 7 must turn the output off.");
            SetCondition(item, 7f, false);
            yield return null;
            yield return null;
            Assert.That(leftLamp.Light.enabled, Is.True, "The donor threshold is inclusive at 7.");
            SetCondition(item, 100f, true);
            yield return null;
            yield return null;
            Assert.That(leftLamp.Light.enabled, Is.False, "A broken real item must stay dark even at condition 100.");
            SetCondition(item, 100f, false);
            yield return null;
            yield return null;
            Assert.That(leftLamp.Light.enabled, Is.True);
            AssemblyOperationResult removed = assembly.TryRemove(wrapper);
            Assert.That(removed.Succeeded, Is.True, removed.Message);
            yield return null;
            yield return null;
            Assert.That(leftLamp.Light.enabled, Is.False, "Removing the same wrapper clears the live lamp output.");
            Assert.That(assembly.ResolveMount(socket).IsOccupied, Is.False);
            Assert.That(wrapper.IsInstalled, Is.False);
            AssertWrapperOwnership(item, wrapper, body, pickup, stableIdentity, visual, identity);
            Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousHub));
        }

        private void CreateFixture()
        {
            previousTimeScale = Time.timeScale;
            previousHub = ItemPresentationProviderHub.Current;
            fixtureStarted = true;
            Time.timeScale = 0f;
            scene = SceneManager.CreateScene("Isolated purchased bulb handoff " + Guid.NewGuid().ToString("N"));
            GameObject holder = SceneRoot("Canonical vehicle fixture");
            holder.SetActive(false);
            holder.transform.position = new Vector3(100f, 100f, 100f);
            vehicle = Object.Instantiate(RequireAsset<GameObject>(VehicleRoot +
                "/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab"), holder.transform, false);
            assembly = vehicle.GetComponent<VehicleAssemblyController>();
            Assert.That(assembly, Is.Not.Null);
            assembly.Initialize();
            Assert.That(assembly.Parts, Has.Length.EqualTo(126));
            Assert.That(assembly.MountPoints, Has.Length.EqualTo(124));
            Assert.That(assembly.Graph.Mounts.Sum(value => value.Fasteners.Length), Is.EqualTo(294));
            VehicleSimulationHost simulation = vehicle.GetComponent<VehicleSimulationHost>();
            simulation.enabled = false;
            Assert.That(simulation.TryInitialize(out string failure), Is.True, failure);
            vehicle.GetComponent<Rigidbody>().useGravity = false;
            vehicle.GetComponent<VehicleInputRouter>().enabled = false;
            // Weather is outside this test assembly's dependency boundary. Disable
            // only the clone's rain renderer before it allocates its live atlas.
            foreach (MonoBehaviour component in vehicle.GetComponents<MonoBehaviour>())
                if (component != null && component.GetType().FullName == "MSC.Weather.Production.VehicleGlassRainPresenter")
                    component.enabled = false;

            ItemDefinitionCatalog definitions = RequireAsset<ItemDefinitionCatalog>(
                "Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset");
            Assert.That(definitions.TryGet(BulbId, out ItemDefinitionRecord definition), Is.True);
            GameObject providerRoot = SceneRoot("Inactive isolated real item provider");
            providerRoot.SetActive(false);
            ItemPresentationProvider provider = providerRoot.AddComponent<ItemPresentationProvider>();
            var binding = new ItemPresentationBinding();
            binding.Configure(BulbId, definition.ReplacementKey,
                RequireAsset<GameObject>(ItemsRoot + "/Prefabs/LegacyItemVisual_item_light-bulb.prefab"));
            provider.Configure("39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31", new[] { binding });
            ItemPlacementCatalog placements = NewAsset<ItemPlacementCatalog>();
            placements.ConfigureForAuthoring("test.playmode.bulb.placements", "msc-world-baseline-04a1.1-c3f2f337",
                Array.Empty<ItemPlacementRecord>());
            ProductionWorldStreamingManifest manifest = NewAsset<ProductionWorldStreamingManifest>();
            manifest.ConfigureForAuthoring(512f, 0, 1, Array.Empty<ProductionWorldCellScene>());
            var streaming = SceneRoot("Isolated disabled streaming service").AddComponent<ProductionWorldStreamingService>();
            streaming.ConfigureForAuthoring(manifest);
            streaming.enabled = false;
            runtime = SceneRoot("Isolated item runtime").AddComponent<ItemWorldRuntime>();
            runtime.Initialize(definitions, placements, streaming, scene, -10000f);
            FieldInfo providerField = typeof(ItemWorldRuntime).GetField("presentationProvider", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(providerField, Is.Not.Null);
            // Instance-only injection uses the real provider without replacing the production hub.
            providerField.SetValue(runtime, provider);
            var bridge = vehicle.GetComponent<VehicleItemAssemblyBridge>();
            Assert.That(bridge, Is.Not.Null);
            Assert.That(bridge.Catalog, Is.SameAs(RequireAsset<VehicleItemPartCatalog>(VehicleRoot + "/VehicleItemPartCatalog.asset")));
            bridge.BindRuntime(runtime);
            holder.SetActive(true);
            Assert.That(assembly.isActiveAndEnabled, Is.True);

            player = SceneRoot("Bulb carry and ray fixture");
            Transform anchor = new GameObject("Carry anchor").transform;
            anchor.SetParent(player.transform, false);
            anchor.localPosition = Vector3.forward * .3f;
            carry = player.AddComponent<PhysicalCarryController>();
            carry.Configure(anchor, null);
            query = player.AddComponent<RaycastInteractionCandidateSource>();
            query.Configure(player.transform, 2.25f, ~0);
        }

        private void InstallCanonicalPart(string partId, string mountId)
        {
            PartInstance part = assembly.Parts.Single(value => value.Definition.DefinitionId == partId);
            MountPointAuthoring mount = assembly.MountPoints.Single(value => value.MountId == mountId);
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            part.Body.position = mount.Pose.position;
            part.Body.rotation = mount.Pose.rotation;
            AssemblyOperationResult installed = assembly.TryInstall(part, mount);
            Assert.That(installed.Succeeded, Is.True, partId + ": " + installed.Message);
        }

        private void TightenCanonicalMount(string mountId)
        {
            Assert.That(assembly.Graph.TryGetMount(mountId, out MountPointRuntime mount), Is.True);
            foreach (FastenerInstance fastener in mount.Fasteners)
            {
                ToolDefinition tool = assembly.Tools.First(value => fastener.Definition.ToolRule.Matches(value));
                while (fastener.Stage < fastener.Definition.MaximumStage)
                {
                    AssemblyOperationResult result = assembly.TryOperateFastener(mountId, fastener.Definition.DefinitionId, tool, true);
                    Assert.That(result.Succeeded, Is.True, result.Message);
                }
            }
            Assert.That(mount.FastenerGroup.IsBolted, Is.True);
        }

        private void AssertWrapperOwnership(WorldItemInstance item, PartInstance wrapper, Rigidbody body,
            PhysicsPickupTarget pickup, StableEntityIdAuthoring stableIdentity, GameObject visual, StableEntityId id)
        {
            Assert.That(runtime.TryGetInstance(id.Value, out WorldItemInstance current), Is.True);
            Assert.That(current, Is.SameAs(item));
            Assert.That(item.GetComponent<PartInstance>(), Is.SameAs(wrapper));
            Assert.That(item.GetComponent<Rigidbody>(), Is.SameAs(body));
            Assert.That(wrapper.Body, Is.SameAs(body));
            Assert.That(item.GetComponent<PhysicsPickupTarget>(), Is.SameAs(pickup));
            Assert.That(wrapper.PickupTarget, Is.SameAs(pickup));
            Assert.That(item.GetComponent<StableEntityIdAuthoring>(), Is.SameAs(stableIdentity));
            Assert.That(wrapper.StableId, Is.EqualTo(id));
            Assert.That(item.StableId, Is.EqualTo(id));
            Assert.That(item.PresentationRoot, Is.SameAs(visual));
            Assert.That(assembly.AllRuntimeParts.Count(value => value.StableId == id), Is.EqualTo(1));
            Assert.That(assembly.Parts.Contains(wrapper), Is.False);
            Assert.That(runtime.LoadedInstances.Count(value => value.StableId == id), Is.EqualTo(1));
        }

        private static void SetCondition(WorldItemInstance item, float condition, bool broken)
        {
            ItemInstanceState state = item.CaptureState();
            state.condition = condition;
            state.isBroken = broken;
            item.ApplyState(state);
        }

        private static void AssertRealPresentation(GameObject visual)
        {
            Assert.That(visual, Is.Not.Null);
            MeshFilter[] filters = visual.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(filters, Has.Length.EqualTo(1));
            Mesh mesh = RequireAsset<Mesh>(ItemsRoot + "/Meshes/LegacyItemMesh_a90bdae7134d57e4bac2be1dee2f2335.asset");
            Assert.That(filters[0].sharedMesh, Is.SameAs(mesh));
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(1));
            Assert.That(renderers[0].sharedMaterials, Is.EqualTo(new[] { RequireAsset<Material>(ItemsRoot +
                "/Materials/LegacyItemMaterial_ad2f7b6e8cc080845a7a7fd4264fbb83.mat") }));
            Assert.That(visual.GetComponentsInChildren<ItemProxyPresentation>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        }

        private GameObject SceneRoot(string name)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private T NewAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            ownedAssets.Add(asset);
            return asset;
        }

        private static T RequireAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Required private generated asset is missing: " + path);
            return asset;
        }
    }
}
#endif
