#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Items;
using MSC.Items.Presentation;
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
    /// <summary>
    /// Exercises an actual old cube, not a renamed generated bulb. This is the
    /// Items record materialization boundary; native multi-domain coordination
    /// is covered separately by CanonicalConsumableNativeSaveTests. No user files.
    /// </summary>
    public sealed class PurchasedBulbProxyRestorePlayModeTests
    {
        private const string VehicleRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        private const string ItemsRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items";
        private const string BulbId = "item.light-bulb";
        private const string SocketId = "mount.satsuma.headlight-left.light-bulb";
        private readonly List<Fixture> fixtures = new();
        private readonly List<ScriptableObject> assets = new();
        private Scene scene;
        private float previousTimeScale;
        private IItemPresentationProvider previousHub;
        private bool started;

        [UnityTest]
        public IEnumerator ActualCubeRecordRestoresFreshRealBulbWithSameIdentityConditionAndCanonicalSocket()
        {
            StartFixture();
            Fixture source = CreateFixture(useRealProvider: false);
            StableEntityId id = ItemStableIdUtility.CreateDeterministic("test.real-cube-bulb.fresh-restore");
            WorldItemInstance original = SpawnCube(source, id);
            yield return null; // The real proxy path defers removal of its primitive collider.
            AssertCube(original);
            ItemInstanceState state = original.CaptureState();
            state.condition = 41.25f;
            original.ApplyState(state);
            string stateJson = JsonUtility.ToJson(original.CaptureState());
            string recordJson = JsonUtility.ToJson(new ItemRuntimeSaveRecord
            {
                state = original.CaptureState(),
                isCanonicalPlacement = false,
                sourceCellId = "test.old-bulb.purchase",
                materializationPosition = original.transform.position,
                materializationRotation = original.transform.rotation,
            });
            CloseFixture(source);
            yield return null;
            Assert.That(original == null, Is.True, "Fresh restore must not reuse the old cube's wrapper.");

            Fixture restored = CreateFixture(useRealProvider: true);
            Assert.That(restored.Runtime.LoadedInstances, Is.Empty);
            int materialized = 0;
            int presented = 0;
            restored.Runtime.InstanceMaterialized += item => { if (item.StableId == id) materialized++; };
            restored.Runtime.PresentationAttached += (item, _) => { if (item.StableId == id) presented++; };
            ItemRuntimeSaveRecord record = JsonUtility.FromJson<ItemRuntimeSaveRecord>(recordJson);
            WorldItemInstance bulb = restored.Runtime.MaterializeForRestore(record, scene);
            yield return null;
            Assert.That(materialized, Is.EqualTo(1));
            Assert.That(presented, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(recordJson), "Restore must not mutate the input record.");
            Assert.That(JsonUtility.ToJson(bulb.CaptureState()), Is.EqualTo(stateJson));
            Assert.That(restored.Runtime.TryGetSourceCellId(id.Value, out string sourceCell), Is.True);
            Assert.That(sourceCell, Is.EqualTo("test.old-bulb.purchase"));
            AssertRealBulb(restored, bulb, id);
            AssertInstallableWithoutReplacingWrapper(restored, bulb, id, stateJson);
            Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousHub));
        }

        [UnityTest]
        public IEnumerator LateRealProviderCallbackReplacesExistingCubeWithoutReplacingItsWrapperOrState()
        {
            StartFixture();
            Fixture fixture = CreateFixture(useRealProvider: false);
            StableEntityId id = ItemStableIdUtility.CreateDeterministic("test.real-cube-bulb.late-provider");
            WorldItemInstance bulb = SpawnCube(fixture, id);
            yield return null;
            AssertCube(bulb);
            ItemInstanceState state = bulb.CaptureState();
            state.condition = 58.5f;
            bulb.ApplyState(state);
            string stateJson = JsonUtility.ToJson(bulb.CaptureState());
            PartInstance part = bulb.GetComponent<PartInstance>();
            Rigidbody body = bulb.GetComponent<Rigidbody>();
            GameObject oldCube = bulb.PresentationRoot;
            // Invoke the exact subscribed instance callback. Do not register a
            // second global provider or disturb another fixture's hub owner.
            MethodInfo callback = typeof(ItemWorldRuntime).GetMethod("HandlePresentationProviderChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(callback, Is.Not.Null);
            callback.Invoke(fixture.Runtime, new object[] { fixture.Provider });
            yield return null;
            Assert.That(oldCube == null, Is.True, "The actual delayed proxy destruction must finish.");
            Assert.That(bulb.GetComponent<PartInstance>(), Is.SameAs(part));
            Assert.That(bulb.GetComponent<Rigidbody>(), Is.SameAs(body));
            Assert.That(JsonUtility.ToJson(bulb.CaptureState()), Is.EqualTo(stateJson));
            AssertRealBulb(fixture, bulb, id);
            AssertInstallableWithoutReplacingWrapper(fixture, bulb, id, stateJson);
            Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousHub));
        }

        private void StartFixture()
        {
            previousTimeScale = Time.timeScale;
            previousHub = ItemPresentationProviderHub.Current;
            started = true;
            Time.timeScale = 0f;
            scene = SceneManager.CreateScene("Old purchased bulb " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        }

        private Fixture CreateFixture(bool useRealProvider)
        {
            var fixture = new Fixture();
            fixtures.Add(fixture); // Teardown also owns partially initialized fixtures.
            fixture.Root = new GameObject("Isolated cube restoration fixture");
            SceneManager.MoveGameObjectToScene(fixture.Root, scene);
            var inactive = new GameObject("Inactive canonical car and provider");
            inactive.transform.SetParent(fixture.Root.transform, false);
            inactive.SetActive(false);
            GameObject car = Object.Instantiate(RequireAsset<GameObject>(VehicleRoot +
                "/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab"), inactive.transform, false);
            fixture.Assembly = car.GetComponent<VehicleAssemblyController>();
            fixture.Assembly.Initialize();
            Assert.That(fixture.Assembly.Parts, Has.Length.EqualTo(126));
            fixture.Mount = fixture.Assembly.MountPoints.Single(value => value.MountId == SocketId);
            ItemDefinitionCatalog definitions = RequireAsset<ItemDefinitionCatalog>(
                "Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset");
            Assert.That(definitions.TryGet(BulbId, out ItemDefinitionRecord definition), Is.True);
            fixture.Provider = inactive.AddComponent<ItemPresentationProvider>();
            var binding = new ItemPresentationBinding();
            binding.Configure(BulbId, definition.ReplacementKey,
                RequireAsset<GameObject>(ItemsRoot + "/Prefabs/LegacyItemVisual_item_light-bulb.prefab"));
            fixture.Provider.Configure("39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31", new[] { binding });
            ItemPlacementCatalog placements = NewAsset<ItemPlacementCatalog>();
            placements.ConfigureForAuthoring("test.old-bulb.placements", "msc-world-baseline-04a1.1-c3f2f337",
                Array.Empty<ItemPlacementRecord>());
            ProductionWorldStreamingManifest manifest = NewAsset<ProductionWorldStreamingManifest>();
            manifest.ConfigureForAuthoring(512f, 0, 1, Array.Empty<ProductionWorldCellScene>());
            var streaming = fixture.Root.AddComponent<ProductionWorldStreamingService>();
            streaming.enabled = false;
            streaming.ConfigureForAuthoring(manifest);
            fixture.Runtime = fixture.Root.AddComponent<ItemWorldRuntime>();
            fixture.Runtime.Initialize(definitions, placements, streaming, scene, -10000f);
            FieldInfo field = typeof(ItemWorldRuntime).GetField("presentationProvider", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(fixture.Runtime, useRealProvider ? fixture.Provider : null);
            Assert.That(field.GetValue(fixture.Runtime), Is.SameAs(useRealProvider ? fixture.Provider : null));
            VehicleItemAssemblyBridge bridge = car.GetComponent<VehicleItemAssemblyBridge>();
            Assert.That(bridge.Catalog, Is.SameAs(RequireAsset<VehicleItemPartCatalog>(VehicleRoot + "/VehicleItemPartCatalog.asset")));
            bridge.BindRuntime(fixture.Runtime);
            return fixture;
        }

        private WorldItemInstance SpawnCube(Fixture fixture, StableEntityId id) => fixture.Runtime.SpawnDynamic(
            BulbId, id, fixture.Mount.Pose.position, fixture.Mount.Pose.rotation, scene);

        private static void AssertCube(WorldItemInstance bulb)
        {
            ItemProxyPresentation[] proxies = bulb.GetComponentsInChildren<ItemProxyPresentation>(true);
            Assert.That(proxies, Has.Length.EqualTo(1));
            Assert.That(bulb.PresentationRoot, Is.SameAs(proxies[0].gameObject));
            Assert.That(proxies[0].GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(proxies[0].GetComponent<MeshFilter>().sharedMesh,
                Is.Not.SameAs(RequireAsset<Mesh>(ItemsRoot + "/Meshes/LegacyItemMesh_a90bdae7134d57e4bac2be1dee2f2335.asset")));
            Assert.That(proxies[0].GetComponent<Collider>(), Is.Null);
        }

        private static void AssertRealBulb(Fixture fixture, WorldItemInstance bulb, StableEntityId id)
        {
            Assert.That(fixture.Runtime.TryGetInstance(id.Value, out WorldItemInstance registered), Is.True);
            Assert.That(registered, Is.SameAs(bulb));
            Assert.That(fixture.Runtime.LoadedInstances.Count(value => value.StableId == id), Is.EqualTo(1));
            PartInstance part = bulb.GetComponent<PartInstance>();
            Assert.That(part.StableId, Is.EqualTo(id));
            Assert.That(part.Body, Is.SameAs(bulb.GetComponent<Rigidbody>()));
            Assert.That(part.PickupTarget, Is.SameAs(bulb.GetComponent<PhysicsPickupTarget>()));
            Assert.That(fixture.Assembly.AllRuntimeParts.Count(value => value.StableId == id), Is.EqualTo(1));
            Assert.That(bulb.GetComponent<ItemPartPresentationBinding>(), Is.InstanceOf<IAssemblyItemCondition>());
            Assert.That(bulb.GetComponentsInChildren<ItemProxyPresentation>(true), Is.Empty);
            MeshFilter[] meshes = bulb.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(meshes, Has.Length.EqualTo(1), "Replacing a cube must not leave duplicate visuals.");
            Assert.That(meshes[0].sharedMesh, Is.SameAs(RequireAsset<Mesh>(ItemsRoot +
                "/Meshes/LegacyItemMesh_a90bdae7134d57e4bac2be1dee2f2335.asset")));
            Assert.That(bulb.PresentationRoot.transform.parent, Is.SameAs(bulb.transform));
            Assert.That(bulb.PresentationRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        private static void AssertInstallableWithoutReplacingWrapper(Fixture fixture, WorldItemInstance bulb,
            StableEntityId id, string stateJson)
        {
            PartInstance part = bulb.GetComponent<PartInstance>();
            GameObject visual = bulb.PresentationRoot;
            AssemblyOperationResult result = fixture.Assembly.TryInstall(part, fixture.Mount);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fixture.Assembly.ResolveMount(fixture.Mount).InstalledPart, Is.SameAs(part));
            Assert.That(part.IsInstalled, Is.True);
            Assert.That(part.RuntimeState.InstalledMountId, Is.EqualTo(SocketId));
            Assert.That(bulb.PresentationRoot, Is.SameAs(visual));
            Assert.That(JsonUtility.ToJson(bulb.CaptureState()), Is.EqualTo(stateJson));
            AssertRealBulb(fixture, bulb, id);
        }

        private static void CloseFixture(Fixture fixture)
        {
            if (fixture.Assembly != null)
                Assert.That(fixture.Assembly.TrySetDynamicPartRegistrationsForRestore(Array.Empty<DynamicPartRegistration>(),
                    out string failure), Is.True, failure);
            if (fixture.Runtime != null)
                foreach (WorldItemInstance item in fixture.Runtime.LoadedInstances.ToArray())
                    Assert.That(fixture.Runtime.TryRemoveDynamic(item), Is.True);
            if (fixture.Root != null) { fixture.Root.SetActive(false); Object.Destroy(fixture.Root); }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var failures = new List<Exception>();
            AsyncOperation unload = null;
            try
            {
                foreach (Fixture fixture in fixtures)
                    try { CloseFixture(fixture); } catch (Exception failure) { failures.Add(failure); }
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            finally { if (started) Time.timeScale = previousTimeScale; }
            if (unload != null) yield return unload;
            yield return null;
            foreach (ScriptableObject asset in assets) if (asset != null) Object.Destroy(asset);
            fixtures.Clear(); assets.Clear();
            if (started) Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousHub));
            if (failures.Count > 0) throw new AggregateException("Cube bulb fixture cleanup failed.", failures);
        }

        private T NewAsset<T>() where T : ScriptableObject
        { T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
        private static T RequireAsset<T>(string path) where T : Object
        { T value = AssetDatabase.LoadAssetAtPath<T>(path); Assert.That(value, Is.Not.Null, path); return value; }
        private sealed class Fixture
        {
            public GameObject Root;
            public VehicleAssemblyController Assembly;
            public MountPointAuthoring Mount;
            public ItemWorldRuntime Runtime;
            public ItemPresentationProvider Provider;
        }
    }
}
#endif
