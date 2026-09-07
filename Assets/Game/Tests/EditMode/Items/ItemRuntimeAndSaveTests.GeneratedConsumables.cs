using System;
using System.Linq;
using System.Reflection;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Items.Presentation;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.ItemsIntegration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Items.Tests.EditMode
{
    public sealed partial class ItemRuntimeAndSaveTests
    {
        private const string GeneratedConsumableVehicleRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        private const string GeneratedConsumableItemsRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items";

        [TestCase("item.spark-plug", "mount.satsuma.cylinder-head.spark-plug-1", 2)]
        [TestCase("item.light-bulb", "mount.satsuma.headlight-left.light-bulb", 1)]
        public void GeneratedConsumableUnitKeepsRealPresentationAndWrapperInCanonicalSocket(
            string itemId, string mountId, int expectedRendererCount)
        {
            GameObject vehiclePrefab = RequireGeneratedConsumableAsset<GameObject>(
                GeneratedConsumableVehicleRoot + "/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab");
            GameObject unitPrefab = RequireGeneratedConsumableAsset<GameObject>(
                GeneratedConsumableItemsRoot + "/Prefabs/LegacyItemVisual_" + itemId.Replace('.', '_') + ".prefab");
            ItemDefinitionCatalog definitions = RequireGeneratedConsumableAsset<ItemDefinitionCatalog>(DefinitionCatalogPath);
            ItemDefinitionRecord itemDefinition = definitions.Definitions.Single(value => value.DefinitionId == itemId);
            PartDefinition partDefinition = RequireGeneratedConsumableAsset<PartDefinition>(
                GeneratedConsumableVehicleRoot + "/LoosePartDefinitions/" +
                VehicleItemPartCatalog.ExpectedPartDefinitionId(itemId) + ".asset");
            MountPointDefinition mountDefinition = RequireGeneratedConsumableAsset<MountPointDefinition>(
                GeneratedConsumableVehicleRoot + "/MountDefinitions/" + mountId + ".asset");

            var inactiveRoot = new GameObject("Generated consumable isolated fixture");
            inactiveRoot.SetActive(false);
            FieldInfo providerField = typeof(ItemWorldRuntime).GetField(
                "presentationProvider", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(providerField, Is.Not.Null);
            object previousProvider = providerField.GetValue(fixture.Runtime);
            IItemPresentationProvider previousHubProvider = ItemPresentationProviderHub.Current;
            VehicleAssemblyController assembly = null;
            WorldItemInstance item = null;
            try
            {
                // Keep the real aggregate and provider inactive: no scene-global Awake/OnEnable ownership.
                GameObject vehicle = Object.Instantiate(vehiclePrefab, inactiveRoot.transform, false);
                assembly = vehicle.GetComponent<VehicleAssemblyController>();
                Assert.That(assembly, Is.Not.Null);
                assembly.Initialize();
                Assert.That(assembly.Parts, Has.Length.EqualTo(126));
                Assert.That(assembly.MountPoints, Has.Length.EqualTo(124));
                Assert.That(assembly.Graph.Mounts.Sum(value => value.Fasteners.Length), Is.EqualTo(294));
                MountPointAuthoring mount = assembly.MountPoints.SingleOrDefault(value => value.MountId == mountId);
                Assert.That(mount, Is.Not.Null, "Regenerate the canonical Satsuma prefab with its purchased-item sockets.");
                Assert.That(mount.Definition, Is.SameAs(mountDefinition));
                Assert.That(mount.GetComponent<AssemblyOwnedMountAuthoring>().OwnerPart.Definition.DefinitionId,
                    Is.EqualTo(mountDefinition.OwnerPartDefinitionId));
                VehicleItemAssemblyBridge bridge = vehicle.GetComponent<VehicleItemAssemblyBridge>();
                Assert.That(bridge, Is.Not.Null, "The canonical prefab must own the actual item bridge.");
                Assert.That(bridge.Catalog, Is.SameAs(RequireGeneratedConsumableAsset<VehicleItemPartCatalog>(
                    GeneratedConsumableVehicleRoot + "/VehicleItemPartCatalog.asset")));
                bridge.BindRuntime(fixture.Runtime);

                ItemPresentationProvider provider = inactiveRoot.AddComponent<ItemPresentationProvider>();
                var binding = new ItemPresentationBinding();
                binding.Configure(itemId, itemDefinition.ReplacementKey, unitPrefab);
                provider.Configure("39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31", new[] { binding });
                providerField.SetValue(fixture.Runtime, provider);
                Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousHubProvider));
                StableEntityId purchasedId = ItemStableIdUtility.CreateDeterministic("test.generated-consumable." + itemId);
                item = fixture.Runtime.SpawnDynamic(itemId, purchasedId, mount.Pose.position, mount.Pose.rotation,
                    SceneManager.GetActiveScene());
                PartInstance part = item.GetComponent<PartInstance>();
                Rigidbody originalBody = item.GetComponent<Rigidbody>();
                PhysicsPickupTarget originalPickup = item.GetComponent<PhysicsPickupTarget>();
                StableEntityIdAuthoring originalIdentity = item.GetComponent<StableEntityIdAuthoring>();
                GameObject visual = item.PresentationRoot;
                Assert.That(part, Is.Not.Null);
                Assert.That(part.Definition, Is.SameAs(partDefinition));
                Assert.That(part.StableId, Is.EqualTo(purchasedId));
                Assert.That(part.Body, Is.SameAs(originalBody));
                Assert.That(part.PickupTarget, Is.SameAs(originalPickup));
                Assert.That(visual, Is.Not.Null, "The real item provider must materialize the unit presentation.");
                Assert.That(visual.transform.parent, Is.SameAs(item.transform));
                AssertGeneratedConsumablePresentation(itemId, visual, expectedRendererCount);
                Assert.That(item.GetComponentsInChildren<ItemProxyPresentation>(true), Is.Empty);
                Assert.That(assembly.Parts.Contains(part), Is.False);
                Assert.That(assembly.AllRuntimeParts.Count(value => value.StableId == purchasedId), Is.EqualTo(1));

                AssemblyOperationResult installed = assembly.TryInstall(part, mount);
                Assert.That(installed.Succeeded, Is.True, installed.Message);
                Assert.That(assembly.ResolveMount(mount).InstalledPart, Is.SameAs(part));
                Assert.That(part.IsInstalled, Is.True);
                Assert.That(part.RuntimeState.InstalledMountId, Is.EqualTo(mountId));
                Assert.That(Vector3.Distance(part.transform.position, mount.Pose.position), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(part.transform.rotation, mount.Pose.rotation), Is.LessThan(.001f));
                Assert.That(fixture.Runtime.TryGetInstance(purchasedId.Value, out WorldItemInstance retained), Is.True);
                Assert.That(retained, Is.SameAs(item));
                Assert.That(item.GetComponent<StableEntityIdAuthoring>(), Is.SameAs(originalIdentity));
                Assert.That(item.GetComponent<Rigidbody>(), Is.SameAs(originalBody));
                Assert.That(item.GetComponent<PhysicsPickupTarget>(), Is.SameAs(originalPickup));
                Assert.That(item.PresentationRoot, Is.SameAs(visual));
                Assert.That(item.GetComponent<AssemblyInstalledPartInteractionTarget>(), Is.Not.Null);
                AssemblyInstalledPartInteractionProxy proxy = item.GetComponentInChildren<AssemblyInstalledPartInteractionProxy>(true);
                Assert.That(proxy, Is.Not.Null);
                Assert.That(proxy.InteractionCollider.enabled, Is.True);
                Assert.That(item.GetComponent<ItemPartPresentationBinding>(), Is.InstanceOf<IAssemblyItemCondition>());
                Assert.That(inactiveRoot.activeInHierarchy, Is.False);
            }
            finally
            {
                if (assembly != null)
                    assembly.TrySetDynamicPartRegistrationsForRestore(Array.Empty<DynamicPartRegistration>(), out _);
                if (item != null) fixture.Runtime.TryRemoveDynamic(item);
                providerField.SetValue(fixture.Runtime, previousProvider);
                Object.DestroyImmediate(inactiveRoot);
            }
        }

        private static void AssertGeneratedConsumablePresentation(string itemId, GameObject visual, int expectedRendererCount)
        {
            string[] meshIds = itemId == "item.spark-plug"
                ? new[] { "ade5d406ceb80bb43a221e997175ae61", "9bd5285a7f9085248bb12fbedf0feb42" }
                : new[] { "a90bdae7134d57e4bac2be1dee2f2335" };
            Mesh[] expectedMeshes = meshIds.Select(id => RequireGeneratedConsumableAsset<Mesh>(
                GeneratedConsumableItemsRoot + "/Meshes/LegacyItemMesh_" + id + ".asset")).ToArray();
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            MeshFilter[] meshFilters = visual.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(renderers, Has.Length.EqualTo(expectedRendererCount));
            Assert.That(meshFilters, Has.Length.EqualTo(expectedRendererCount));
            CollectionAssert.AreEquivalent(expectedMeshes, meshFilters.Select(value => value.sharedMesh));
            Assert.That(expectedMeshes.All(mesh => mesh.vertexCount > 0), Is.True);
            Material expectedMaterial = RequireGeneratedConsumableAsset<Material>(
                GeneratedConsumableItemsRoot + "/Materials/LegacyItemMaterial_ad2f7b6e8cc080845a7a7fd4264fbb83.mat");
            Texture expectedTexture = RequireGeneratedConsumableAsset<Texture>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/" +
                "M06B2_f4795e5371f75b74e910997a99c764b7_color.png");
            Assert.That(expectedMaterial.shader, Is.Not.Null);
            Assert.That(expectedMaterial.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
            Assert.That(expectedMaterial.GetTexture("_BaseColorMap"), Is.SameAs(expectedTexture));
            foreach (Renderer renderer in renderers)
                Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { expectedMaterial }));
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
        }

        private static T RequireGeneratedConsumableAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Required private generated asset is missing: " + path);
            return asset;
        }
    }
}
