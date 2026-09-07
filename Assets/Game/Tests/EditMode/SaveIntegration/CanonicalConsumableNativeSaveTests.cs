using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Items;
using MSC.Items.Presentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.ItemsIntegration;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CanonicalConsumableNativeSaveTests
    {
        private const string VehicleRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        private const string ItemsRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items";
        private const string DefinitionCatalogPath = "Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset";
        private const string PurchaseCell = "test.consumable.purchase-cell";

        [TestCase("item.spark-plug", "mount.satsuma.cylinder-head.spark-plug-1")]
        [TestCase("item.light-bulb", "mount.satsuma.headlight-left.light-bulb")]
        public void PurchasedGeneratedUnitRoundTripsThroughNativeDocumentIntoFreshCanonicalCar(string itemId, string mountId)
        {
            AssertPurchasedGeneratedUnitRoundTrip(itemId, mountId, usePreviewScene: true);
        }

        [UnityTest]
        public IEnumerator PurchasedGeneratedSparkPlugRoundTripsThroughNativeDocumentIntoFreshCanonicalCarInPlayMode()
        {
            return AssertPurchasedGeneratedUnitRoundTripInPlayMode("item.spark-plug", "mount.satsuma.cylinder-head.spark-plug-1");
        }

        [UnityTest]
        public IEnumerator PurchasedGeneratedLightBulbRoundTripsThroughNativeDocumentIntoFreshCanonicalCarInPlayMode()
        {
            return AssertPurchasedGeneratedUnitRoundTripInPlayMode("item.light-bulb", "mount.satsuma.headlight-left.light-bulb");
        }

        private static IEnumerator AssertPurchasedGeneratedUnitRoundTripInPlayMode(string itemId, string mountId)
        {
            yield return new EnterPlayMode();
            ExceptionDispatchInfo failure = null;
            try
            {
                AssertPurchasedGeneratedUnitRoundTrip(itemId, mountId, usePreviewScene: false);
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            yield return new ExitPlayMode();
            failure?.Throw();
        }

        [UnityTest]
        public IEnumerator InactiveRigidbodyPoseWritesRecoverExactPoseWhenProbeIsReactivated()
        {
            yield return new EnterPlayMode();
            ExceptionDispatchInfo failure = null;
            try
            {
                AssertInactiveRigidbodyPoseLifecycle();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            yield return new ExitPlayMode();
            failure?.Throw();
        }

        private static void AssertInactiveRigidbodyPoseLifecycle()
        {
            Scene scene = SceneManager.CreateScene("InactiveRigidbodyPoseProbe_" + Guid.NewGuid().ToString("N"));
            GameObject parent = null;
            GameObject probe = null;
            try
            {
                parent = new GameObject("Inactive pose probe parent");
                SceneManager.MoveGameObjectToScene(parent, scene);
                parent.SetActive(false);
                probe = new GameObject("Isolated Rigidbody pose probe");
                SceneManager.MoveGameObjectToScene(probe, scene);
                Vector3 initialPosition = new Vector3(2.25f, 3.5f, -4.75f);
                Quaternion initialRotation = Quaternion.Euler(10f, 20f, 30f);
                probe.transform.SetPositionAndRotation(initialPosition, initialRotation);
                probe.AddComponent<BoxCollider>();
                Rigidbody body = probe.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.position = initialPosition;
                body.rotation = initialRotation;
                LogRigidbodyPose("active before parenting", body);
                Assert.That(Vector3.Distance(body.position, initialPosition), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(body.rotation, initialRotation), Is.LessThan(.001f));

                probe.transform.SetParent(parent.transform, true);
                LogRigidbodyPose("inactive immediately after parenting", body);
                Vector3 installedPosition = new Vector3(7.25f, -3.5f, 2f);
                Quaternion installedRotation = Quaternion.Euler(35f, 15f, 65f);
                probe.transform.SetPositionAndRotation(installedPosition, installedRotation);
                body.position = installedPosition;
                body.rotation = installedRotation;
                LogRigidbodyPose("inactive after Transform and sequential Rigidbody pose writes", body);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
                body.isKinematic = true;
                body.detectCollisions = true;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                LogRigidbodyPose("inactive after kinematic transition", body);
                probe.transform.SetPositionAndRotation(installedPosition, installedRotation);
                body.position = installedPosition;
                body.rotation = installedRotation;
                LogRigidbodyPose("inactive after post-kinematic pose writes", body);

                parent.SetActive(true);
                LogRigidbodyPose("active after reactivating only isolated probe", body);
                Assert.That(probe.activeInHierarchy, Is.True);
                Assert.That(Vector3.Distance(probe.transform.position, installedPosition), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(probe.transform.rotation, installedRotation), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(body.position, installedPosition), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(body.rotation, installedRotation), Is.LessThan(.001f));
            }
            finally
            {
                if (probe != null) Object.DestroyImmediate(probe);
                if (parent != null) Object.DestroyImmediate(parent);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static void LogRigidbodyPose(string stage, Rigidbody body)
        {
            TestContext.WriteLine(stage + ": active=" + body.gameObject.activeInHierarchy +
                ", kinematic=" + body.isKinematic + ", transform.position=" + body.transform.position.ToString("R") +
                ", body.position=" + body.position.ToString("R") + ", transform.rotation=" +
                body.transform.rotation.ToString("R") + ", body.rotation=" + body.rotation.ToString("R"));
        }

        private static void AssertPurchasedGeneratedUnitRoundTrip(string itemId, string mountId, bool usePreviewScene)
        {
            IItemPresentationProvider previousHub = ItemPresentationProviderHub.Current;
            StableEntityId purchasedId = ItemStableIdUtility.CreateDeterministic("test.native-generated-consumable." + itemId);
            var codec = new SaveDocumentCodec();
            string documentJson;
            string logicalStateJson;
            string vehicleId;
            Vector3 savedVisualPosition;
            Quaternion savedVisualRotation;
            Vector3 savedInstalledPosition;
            Quaternion savedInstalledRotation;
            WorldItemInstance original;
            using (var source = new Fixture(itemId, mountId, usePreviewScene))
            {
                original = source.Runtime.SpawnDynamic(itemId, purchasedId, source.Mount.Pose.position,
                    source.Mount.Pose.rotation, source.Scene, PurchaseCell);
                ItemInstanceState state = original.CaptureState();
                state.condition = 63.25f;
                original.ApplyState(state);
                PartInstance part = original.GetComponent<PartInstance>();
                Assert.That(part, Is.Not.Null);
                Vector3 visualRestPosition = original.PresentationRoot.transform.localPosition;
                Quaternion visualRestRotation = original.PresentationRoot.transform.localRotation;
                part.transform.SetPositionAndRotation(source.Mount.Pose.position, source.Mount.Pose.rotation);
                part.Body.position = source.Mount.Pose.position;
                part.Body.rotation = source.Mount.Pose.rotation;
                AssemblyOperationResult installed = source.Assembly.TryInstall(part, source.Mount);
                Assert.That(installed.Succeeded, Is.True, installed.Message);
                if (itemId == "item.spark-plug")
                {
                    ToolDefinition tool = RequireAsset<ToolDefinition>(VehicleRoot + "/ToolDefinitions/spark-plug-wrench.asset");
                    for (int turn = 0; turn < 3; turn++)
                    {
                        AssemblyOperationResult tightened = source.Assembly.TryTurnFastener(mountId,
                            SatsumaConsumableAssemblyRules.SparkPlugFastenerId(1), tool, FastenerRotationDirection.Clockwise);
                        Assert.That(tightened.Succeeded, Is.True, tightened.Message);
                    }
                    RefreshPlugPresentation(original);
                    Assert.That(Vector3.Distance(original.PresentationRoot.transform.localPosition,
                        visualRestPosition + Vector3.back * .0075f), Is.LessThan(.00001f));
                    Assert.That(Quaternion.Angle(original.PresentationRoot.transform.localRotation,
                        Quaternion.AngleAxis(135f, Vector3.forward) * visualRestRotation), Is.LessThan(.001f));
                }
                AssertLiveOwnership(source, original, purchasedId, itemId, mountId);
                savedInstalledPosition = original.transform.position;
                savedInstalledRotation = original.transform.rotation;
                savedVisualPosition = original.PresentationRoot.transform.localPosition;
                savedVisualRotation = original.PresentationRoot.transform.localRotation;
                logicalStateJson = JsonUtility.ToJson(original.CaptureState());
                vehicleId = source.Persistence.StableVehicleId;
                SaveDocument document = NewDocument(source.Registry.CaptureDomains());
                AssertDomainOwnership(document, purchasedId.Value, itemId, mountId, vehicleId, logicalStateJson,
                    savedInstalledPosition, savedInstalledRotation);
                documentJson = codec.Serialize(document);
            }

            Assert.That(original == null, Is.True, "The original physical wrapper must be destroyed before loading.");
            Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousHub));
            using (var restored = new Fixture(itemId, mountId, usePreviewScene))
            {
                Assert.That(restored.Runtime.LoadedInstances, Is.Empty);
                Assert.That(restored.Assembly.CaptureDynamicPartRegistrations(), Is.Empty);
                Assert.That(restored.Persistence.StableVehicleId, Is.EqualTo(vehicleId));
                int materializations = 0;
                int presentations = 0;
                restored.Runtime.InstanceMaterialized += item => { if (item.StableId == purchasedId) materializations++; };
                restored.Runtime.PresentationAttached += (item, visual) => { if (item.StableId == purchasedId) presentations++; };
                SaveDocument decoded = codec.Deserialize(documentJson, requireCurrentVersion: true);
                string untouchedInput = codec.Serialize(decoded);
                var staged = new DeferredStableEntityStore();
                var unresolved = new UnresolvedContentReport();
                PreparedSaveRestore prepared = restored.Registry.PrepareRestore(decoded, unresolved, staged);
                Assert.That(restored.Runtime.LoadedInstances, Is.Empty, "Preflight must not materialize the purchase.");
                restored.Registry.ApplyRestore(prepared, unresolved, staged);

                Assert.That(restored.Runtime.TryGetInstance(purchasedId.Value, out WorldItemInstance item), Is.True);
                Assert.That(ReferenceEquals(item, original), Is.False);
                Assert.That(materializations, Is.EqualTo(1));
                Assert.That(presentations, Is.EqualTo(1), "A fresh load must instantiate the real unit visual through its provider.");
                Assert.That(staged.Snapshot(), Is.Empty, "The loaded aggregate owns its purchase even though the source cell is unloaded.");
                Assert.That(codec.Serialize(decoded), Is.EqualTo(untouchedInput), "Restore preparation must not alter the source document.");
                Assert.That(JsonUtility.ToJson(item.CaptureState()), Is.EqualTo(logicalStateJson));
                AssertLiveOwnership(restored, item, purchasedId, itemId, mountId);
                if (itemId == "item.spark-plug") RefreshPlugPresentation(item);
                Assert.That(Vector3.Distance(item.PresentationRoot.transform.localPosition, savedVisualPosition), Is.LessThan(.00001f));
                Assert.That(Quaternion.Angle(item.PresentationRoot.transform.localRotation, savedVisualRotation), Is.LessThan(.001f));
                AssertDomainOwnership(NewDocument(restored.Registry.CaptureDomains()), purchasedId.Value,
                    itemId, mountId, vehicleId, logicalStateJson, savedInstalledPosition, savedInstalledRotation);
            }
            Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousHub));
        }

        private static void AssertLiveOwnership(Fixture fixture, WorldItemInstance item, StableEntityId id, string itemId, string mountId)
        {
            PartInstance part = item.GetComponent<PartInstance>();
            Assert.That(part, Is.Not.Null);
            Assert.That(part.gameObject, Is.SameAs(item.gameObject));
            Assert.That(part.StableId, Is.EqualTo(id));
            Assert.That(item.StableId, Is.EqualTo(id));
            Assert.That(part.Definition, Is.SameAs(RequireAsset<PartDefinition>(VehicleRoot + "/LoosePartDefinitions/" +
                VehicleItemPartCatalog.ExpectedPartDefinitionId(itemId) + ".asset")));
            Assert.That(part.Body, Is.SameAs(item.GetComponent<Rigidbody>()));
            Assert.That(part.PickupTarget, Is.SameAs(item.GetComponent<PhysicsPickupTarget>()));
            Assert.That(fixture.Assembly.Graph.TryGetPartByStableId(id.Value, out PartInstance registered), Is.True);
            Assert.That(registered, Is.SameAs(part));
            Assert.That(fixture.Assembly.AllRuntimeParts.Count(value => value.StableId == id), Is.EqualTo(1));
            Assert.That(fixture.Assembly.Parts.Contains(part), Is.False);
            Assert.That(fixture.Assembly.ResolveMount(fixture.Mount).InstalledPart, Is.SameAs(part));
            Assert.That(part.RuntimeState.InstalledMountId, Is.EqualTo(mountId));
            Assert.That(part.IsInstalled, Is.True);
            Assert.That(Vector3.Distance(part.transform.position, fixture.Mount.Pose.position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(part.transform.rotation, fixture.Mount.Pose.rotation), Is.LessThan(.001f));
            // Unity 6.6 removes the native actor when this aggregate is inactive.
            // Transform remains authoritative until activation; active handoff
            // tests separately require the real Rigidbody pose to match the mount.
            if (part.gameObject.activeInHierarchy)
            {
                Assert.That(Vector3.Distance(part.Body.position, fixture.Mount.Pose.position), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(part.Body.rotation, fixture.Mount.Pose.rotation), Is.LessThan(.001f));
            }
            Assert.That(fixture.Runtime.TryGetSourceCellId(id.Value, out string sourceCell), Is.True);
            Assert.That(sourceCell, Is.EqualTo(PurchaseCell));
            Assert.That(fixture.Runtime.IsCanonicalPlacement(id.Value), Is.False);
            Assert.That(fixture.World.TryResolve(id.Value, out _), Is.False);
            Assert.That(item.GetComponentsInChildren<ItemProxyPresentation>(true), Is.Empty);
            Assert.That(item.PresentationRoot.transform.parent, Is.SameAs(item.transform));
            AssertGeneratedPresentation(itemId, item.PresentationRoot);
            if (itemId == "item.spark-plug")
                Assert.That(fixture.Assembly.ResolveMount(fixture.Mount).Fasteners.Single().Stage, Is.EqualTo(3));
        }

        private static void AssertDomainOwnership(SaveDocument document, string id, string itemId, string mountId,
            string vehicleId, string logicalStateJson, Vector3 expectedInstalledPosition, Quaternion expectedInstalledRotation)
        {
            ItemDomainSaveDto items = SaveParticipantJson.Deserialize<ItemDomainSaveDto>(
                document.Domains.Single(value => value.DomainId == ItemSaveParticipant.DomainId).PayloadJson);
            ItemRuntimeSaveRecord item = items.instances.Single(value => value.state.stableEntityId == id);
            Assert.That(items.instances, Has.Length.EqualTo(1));
            Assert.That(item.state.definitionId, Is.EqualTo(itemId));
            Assert.That(JsonUtility.ToJson(item.state), Is.EqualTo(logicalStateJson));
            Assert.That(item.sourceCellId, Is.EqualTo(PurchaseCell));
            Assert.That(item.isCanonicalPlacement, Is.False);
            WorldEntityDomainSaveDto world = SaveParticipantJson.Deserialize<WorldEntityDomainSaveDto>(
                document.Domains.Single(value => value.DomainId == WorldEntitySaveParticipant.DomainId).PayloadJson);
            Assert.That(world.entities.Any(value => value.stableEntityId == id), Is.False);
            VehicleSaveRecordDto vehicle = SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(
                document.Domains.Single(value => value.DomainId == VehicleSaveParticipant.DomainId).PayloadJson).vehicles.Single();
            Assert.That(vehicle.stableVehicleId, Is.EqualTo(vehicleId));
            Assert.That(vehicle.assembly.parts, Has.Length.EqualTo(126));
            Assert.That(vehicle.assembly.mounts, Has.Length.EqualTo(124));
            Assert.That(vehicle.assembly.fasteners, Has.Length.EqualTo(294));
            DynamicAssemblyPartSaveDto part = vehicle.assembly.dynamicParts.Single();
            Assert.That(part.itemDefinitionId, Is.EqualTo(itemId));
            Assert.That(part.part.stableEntityId, Is.EqualTo(id));
            Assert.That(part.part.partDefinitionId, Is.EqualTo(VehicleItemPartCatalog.ExpectedPartDefinitionId(itemId)));
            Assert.That(part.part.lifecycleState, Is.EqualTo(PartLifecycleState.Installed));
            Assert.That(part.part.installedMountId, Is.EqualTo(mountId));
            Assert.That(Vector3.Distance(part.part.worldPosition, expectedInstalledPosition), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(part.part.worldRotation, expectedInstalledRotation), Is.LessThan(.001f));
            Assert.That(vehicle.assembly.mounts.Single(value => value.mountId == mountId).installedPartStableEntityId, Is.EqualTo(id));
            if (itemId == "item.spark-plug")
                Assert.That(vehicle.assembly.fasteners.Single(value => value.mountId == mountId).stage, Is.EqualTo(3));
        }

        private static void RefreshPlugPresentation(WorldItemInstance item)
        {
            AssemblySparkPlugStagePresentation presentation = item.GetComponent<AssemblySparkPlugStagePresentation>();
            Assert.That(presentation, Is.Not.Null, "The canonical aggregate must bind the purchased plug's stage presenter.");
            Assert.That(presentation.Visual, Is.SameAs(item.PresentationRoot.transform));
            presentation.RefreshPresentation();
        }

        private static void AssertGeneratedPresentation(string itemId, GameObject visual)
        {
            string[] meshIds = itemId == "item.spark-plug"
                ? new[] { "ade5d406ceb80bb43a221e997175ae61", "9bd5285a7f9085248bb12fbedf0feb42" }
                : new[] { "a90bdae7134d57e4bac2be1dee2f2335" };
            Mesh[] meshes = meshIds.Select(id => RequireAsset<Mesh>(ItemsRoot + "/Meshes/LegacyItemMesh_" + id + ".asset")).ToArray();
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(meshes.Length));
            CollectionAssert.AreEquivalent(meshes, visual.GetComponentsInChildren<MeshFilter>(true).Select(value => value.sharedMesh));
            Assert.That(meshes.All(value => value.vertexCount > 0), Is.True);
            Material material = RequireAsset<Material>(ItemsRoot + "/Materials/LegacyItemMaterial_ad2f7b6e8cc080845a7a7fd4264fbb83.mat");
            Texture texture = RequireAsset<Texture>("Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/" +
                "M06B2_f4795e5371f75b74e910997a99c764b7_color.png");
            Assert.That(material.shader, Is.Not.Null);
            Assert.That(material.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
            Assert.That(material.GetTexture("_BaseColorMap"), Is.SameAs(texture));
            foreach (Renderer renderer in renderers) Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { material }));
            Assert.That(visual.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
        }

        private static T RequireAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Required generated content is missing; rebuild the canonical baseline: " + path);
            return asset;
        }

        private static SaveDocument NewDocument(SaveDomainEnvelope[] domains)
        {
            string utc = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero).ToString("O", CultureInfo.InvariantCulture);
            return new SaveDocument
            {
                Header = new SaveHeader { SaveId = Guid.NewGuid().ToString("N"), SlotId = "slot-isolated-consumable",
                    BuildId = "generated-consumable-tests", CreatedUtc = utc, UpdatedUtc = utc },
                Domains = domains,
            };
        }

        private sealed class Fixture : IDisposable
        {
            private GameObject holder;
            private GameObject runtimeObject;
            private GameObject streamingObject;
            private ItemPlacementCatalog placements;
            private ProductionWorldStreamingManifest manifest;
            private readonly bool usePreviewScene;
            private readonly bool useCurrentEditorScene;
            public Scene Scene { get; private set; }
            public ItemWorldRuntime Runtime { get; private set; }
            public VehicleAssemblyController Assembly { get; private set; }
            public VehiclePersistenceBinding Persistence { get; private set; }
            public MountPointAuthoring Mount { get; private set; }
            public WorldEntitySaveParticipant World { get; private set; }
            public SaveParticipantRegistry Registry { get; private set; }
            public DeferredStableEntityStore Deferred { get; private set; }
            public VehicleSaveParticipant Vehicles { get; private set; }
            public ISaveRestorePlanFactory RestorePlan { get; private set; }

            public Fixture(string itemId, string mountId, bool usePreviewScene = true,
                bool allEngineConsumables = false, bool deferVehicle = false, bool useCurrentEditorScene = false)
            {
                this.usePreviewScene = usePreviewScene;
                this.useCurrentEditorScene = useCurrentEditorScene;
                try
                {
                    if (useCurrentEditorScene && (Application.isPlaying || usePreviewScene))
                        throw new ArgumentException("Current editor scene is an explicit graphics-only fixture choice.");
                    Scene = useCurrentEditorScene ? SceneManager.GetActiveScene() : usePreviewScene
                        ? EditorSceneManager.NewPreviewScene()
                        : SceneManager.CreateScene("CanonicalConsumableNativeSaveTests_" + Guid.NewGuid().ToString("N"));
                    holder = CreateRoot("Inactive canonical consumable save fixture");
                    holder.SetActive(false);
                    GameObject car = Object.Instantiate(RequireAsset<GameObject>(VehicleRoot +
                        "/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab"), holder.transform, false);
                    Assembly = car.GetComponent<VehicleAssemblyController>();
                    Assert.That(Assembly, Is.Not.Null);
                    Assembly.Initialize();
                    Assert.That(Assembly.Parts, Has.Length.EqualTo(126));
                    Assert.That(Assembly.MountPoints, Has.Length.EqualTo(124), "Canonical prefab is stale; expected 124 authored sockets.");
                    Assert.That(Assembly.Graph.Mounts.Sum(value => value.Fasteners.Length), Is.EqualTo(294));
                    Mount = Assembly.MountPoints.Single(value => value.MountId == mountId);
                    Assert.That(Mount.Definition, Is.SameAs(RequireAsset<MountPointDefinition>(VehicleRoot + "/MountDefinitions/" + mountId + ".asset")));
                    VehicleItemAssemblyBridge bridge = car.GetComponent<VehicleItemAssemblyBridge>();
                    Assert.That(bridge, Is.Not.Null, "Canonical prefab must author its actual item bridge.");
                    Assert.That(bridge.Catalog, Is.SameAs(RequireAsset<VehicleItemPartCatalog>(VehicleRoot + "/VehicleItemPartCatalog.asset")));
                    Assert.That(car.GetComponent<AssemblyConsumablePresentationBinding>(), Is.Not.Null);
                    VehicleSimulationHost simulation = car.GetComponent<VehicleSimulationHost>();
                    Assert.That(simulation, Is.Not.Null);
                    Assert.That(simulation.TryInitialize(out string failure), Is.True, failure);
                    Persistence = car.GetComponent<VehiclePersistenceBinding>();
                    Assert.That(Persistence, Is.Not.Null);

                    ItemDefinitionCatalog definitions = RequireAsset<ItemDefinitionCatalog>(DefinitionCatalogPath);
                    ItemPresentationProvider provider = holder.AddComponent<ItemPresentationProvider>();
                    string[] presentationIds = allEngineConsumables
                        ? new[] { "item.spark-plug", "item.light-bulb", "item.alternator-belt", "item.oil-filter" }
                        : new[] { itemId };
                    var presentationBindings = presentationIds.Select(id =>
                    {
                        Assert.That(definitions.TryGet(id, out ItemDefinitionRecord definition), Is.True);
                        var binding = new ItemPresentationBinding();
                        binding.Configure(id, definition.ReplacementKey,
                            RequireAsset<GameObject>(ItemsRoot + "/Prefabs/LegacyItemVisual_" + id.Replace('.', '_') + ".prefab"));
                        return binding;
                    }).ToArray();
                    provider.Configure("39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31", presentationBindings);
                    placements = ScriptableObject.CreateInstance<ItemPlacementCatalog>();
                    placements.ConfigureForAuthoring("test.native-consumable.placements", "msc-world-baseline-04a1.1-c3f2f337", Array.Empty<ItemPlacementRecord>());
                    manifest = ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
                    manifest.ConfigureForAuthoring(512f, 0, 1, new[]
                    { new ProductionWorldCellScene(PurchaseCell, 10, 10, 999, "Assets/Tests/Scenes/UnloadedConsumablePurchaseCell.unity") });
                    streamingObject = CreateRoot("Isolated consumable streaming service");
                    ProductionWorldStreamingService streaming = streamingObject.AddComponent<ProductionWorldStreamingService>();
                    streaming.ConfigureForAuthoring(manifest);
                    runtimeObject = CreateRoot("Isolated consumable item runtime");
                    Runtime = runtimeObject.AddComponent<ItemWorldRuntime>();
                    Runtime.Initialize(definitions, placements, streaming, Scene, -10000f);
                    FieldInfo providerField = typeof(ItemWorldRuntime).GetField("presentationProvider", BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.That(providerField, Is.Not.Null);
                    // Instance injection preserves the real provider and leaves the scene-global hub untouched.
                    providerField.SetValue(Runtime, provider);
                    bridge.BindRuntime(Runtime);

                    var deferred = new DeferredStableEntityStore();
                    Deferred = deferred;
                    var items = new ItemSaveParticipant(Runtime, deferred);
                    World = new WorldEntitySaveParticipant(deferred);
                    World.ConfigureExternalOwnership(Runtime.IsExternallyOwned);
                    var vehicle = new VehicleSaveParticipant(deferred, null, null, -64f, null, Runtime);
                    Vehicles = vehicle;
                    var plan = new VehicleItemSaveRestorePlanFactory(Runtime, vehicle, World, deferred, items.NormalizeLegacyFoodState);
                    RestorePlan = plan;
                    items.ConfigureAssemblyOwnerAvailability(plan.IsOwnerUnavailable);
                    vehicle.RegisterHierarchy(car);
                    // Remember the real catalog, then emulate the aggregate's unavailable
                    // gameplay layer. Registration later must consume its deferred payload.
                    if (deferVehicle) vehicle.CaptureAndUnregisterScene(Scene);
                    Runtime.InstanceMaterialized += item => World.RegisterTarget(item.GetComponent<PhysicsPickupTarget>());
                    Registry = new SaveParticipantRegistry(new ISaveParticipant[] { vehicle, World, items }, plan);
                    Assert.That(holder.activeInHierarchy, Is.False);
                }
                catch { Dispose(); throw; }
            }

            private GameObject CreateRoot(string name)
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, Scene);
                return root;
            }

            public void Dispose()
            {
                try
                {
                    if (Assembly != null) Assembly.TrySetDynamicPartRegistrationsForRestore(Array.Empty<DynamicPartRegistration>(), out _);
                    if (Runtime != null)
                        foreach (WorldItemInstance item in Runtime.LoadedInstances.ToArray()) Runtime.TryRemoveDynamic(item);
                }
                finally
                {
                    if (runtimeObject != null) Object.DestroyImmediate(runtimeObject);
                    if (streamingObject != null) Object.DestroyImmediate(streamingObject);
                    if (holder != null) Object.DestroyImmediate(holder);
                    if (placements != null) Object.DestroyImmediate(placements);
                    if (manifest != null) Object.DestroyImmediate(manifest);
                    if (!useCurrentEditorScene && Scene.IsValid() && Scene.isLoaded)
                    {
                        if (usePreviewScene) EditorSceneManager.ClosePreviewScene(Scene);
                        else SceneManager.UnloadSceneAsync(Scene);
                    }
                }
            }
        }
    }
}
