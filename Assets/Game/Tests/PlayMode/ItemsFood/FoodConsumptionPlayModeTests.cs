using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.Bootstrap;
using MSC.Core.Identity;
using MSC.Core.Time;
using MSC.Home;
using MSC.Interaction;
using MSC.Interaction.Architecture;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.LegacyImport;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MSC.Items.Food.Tests.PlayMode
{
    public sealed class FoodConsumptionPlayModeTests
    {
        private const string BootstrapSceneName = "Bootstrap";
        private const string DefinitionCatalogPath =
            "Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset";

        [UnityTest]
        public IEnumerator ProductionBootstrapBindsWorkingFridgeAndCookingVolumes()
        {
            if (GameCompositionRoot.ActiveRoot != null)
            {
                UnityEngine.Object.Destroy(
                    GameCompositionRoot.ActiveRoot.gameObject);
                yield return null;
            }

            AsyncOperation load = SceneManager.LoadSceneAsync(
                BootstrapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null,
                "Bootstrap is missing from Build Settings.");
            yield return load;
            yield return null;

            ProductionWorldStreamingInstaller installer =
                UnityEngine.Object.FindFirstObjectByType<
                    ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            Assert.That(installer, Is.Not.Null);
            Assert.That(
                installer.TryBeginGameplayPreparation(
                    out string preparationFailure),
                Is.True,
                preparationFailure);

            double timeout = Time.realtimeSinceStartupAsDouble + 120d;
            while (Time.realtimeSinceStartupAsDouble < timeout &&
                   (!installer.IsReady ||
                    installer.FoodApplianceInstaller == null ||
                    installer.FoodApplianceInstaller.FridgeDoor == null))
            {
                yield return null;
            }

            Assert.That(
                Time.realtimeSinceStartupAsDouble,
                Is.LessThan(timeout),
                "Production fridge binding timed out.");
            Assert.That(installer.IsReady, Is.True);
            Assert.That(installer.ItemWorldRuntime, Is.Not.Null);
            Assert.That(installer.HomeRuntime, Is.Not.Null);
            Assert.That(installer.FoodApplianceInstaller, Is.Not.Null);
            Assert.That(
                installer.FoodApplianceInstaller.IsInitialized,
                Is.True);
            HomeWeightScalePresenter weightScale =
                UnityEngine.Object.FindFirstObjectByType<
                    HomeWeightScalePresenter>(FindObjectsInactive.Include);
            Assert.That(weightScale, Is.Not.Null);
            Assert.That(
                weightScale.HasGaugeBinding,
                Is.True,
                "The streamed bathroom gauge was not resolved by stable ID.");

            ProductionFoodApplianceInstaller appliances =
                installer.FoodApplianceInstaller;
            FridgeDoorInteractionTarget door = appliances.FridgeDoor;
            Assert.That(door, Is.Not.Null);
            Assert.That(door.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(
                door.GetComponentsInChildren<BoxCollider>(true)
                    .Any(value => value.enabled && !value.isTrigger),
                Is.True);
            Assert.That(
                door.GetComponentsInChildren<SphereCollider>(true)
                    .Any(value => value.enabled && value.isTrigger),
                Is.True);

            HomeSystemRuntime home = installer.HomeRuntime;
            home.SetElectricityAvailable(true);
            home.SetFridgeDoorOpen(false);
            door.RestoreFromHome(immediate: true);
            Assert.That(home.FridgeCoolingActive, Is.True);
            Assert.That(
                appliances.IsRefrigerated(
                    appliances.RefrigeratedWorldBounds.center),
                Is.True);

            HingedDoorInteractionTarget hinge =
                door.GetComponent<HingedDoorInteractionTarget>();
            Assert.That(hinge, Is.Not.Null);
            var context = new InteractionContext(
                installer.gameObject,
                door.transform.position,
                door.transform.forward);
            door.Interact(context);
            Assert.That(home.Snapshot.FridgeDoorOpen, Is.True);
            Assert.That(home.FridgeCoolingActive, Is.False);
            hinge.Advance(hinge.EstimatedTravelSeconds + 0.1f);
            Assert.That(hinge.OpenNormalized, Is.GreaterThan(0.98f));

            WorldItemInstance fridgeFood = installer.ItemWorldRuntime
                .LoadedInstances
                .Single(value => string.Equals(
                    value.Definition.DefinitionId,
                    "item.macaroni-box",
                    StringComparison.Ordinal));
            Collider foodCollider = fridgeFood.GetComponent<Collider>();
            Assert.That(foodCollider, Is.Not.Null);
            var aimObject = new GameObject("Fridge food interaction probe");
            Vector3 aimPoint = foodCollider.bounds.center;
            aimObject.transform.position = aimPoint + Vector3.back * 1.25f;
            aimObject.transform.rotation = Quaternion.LookRotation(
                aimPoint - aimObject.transform.position,
                Vector3.up);
            RaycastInteractionCandidateSource candidateSource =
                aimObject.AddComponent<RaycastInteractionCandidateSource>();
            candidateSource.Configure(aimObject.transform, 2.25f, ~0);
            Physics.SyncTransforms();
            InteractionCandidate foodCandidate = candidateSource.Query();
            string collisionTrace = string.Join(
                " | ",
                Physics.RaycastAll(
                        aimObject.transform.position,
                        aimObject.transform.forward,
                        2.25f,
                        ~0,
                        QueryTriggerInteraction.Collide)
                    .OrderBy(hit => hit.distance)
                    .Select(hit =>
                    {
                        DonorWorldBaselineColliderMetadata colliderMetadata =
                            hit.collider.GetComponent<
                                DonorWorldBaselineColliderMetadata>();
                        DonorWorldBaselineEntityMetadata entityMetadata =
                            hit.collider.GetComponentInParent<
                                DonorWorldBaselineEntityMetadata>();
                        string stableId = colliderMetadata?.EntityStableId ??
                            entityMetadata?.StableId ?? "project-owned";
                        return $"{hit.distance:0.000}m " +
                               $"{hit.collider.name} [{stableId}]";
                    }));
            Assert.That(
                foodCandidate.Host,
                Is.EqualTo(fridgeFood.GetComponent<InteractionTargetHost>()),
                "An opened fridge must not leave a hidden closed-door or " +
                "oversized shelf collider in front of its food. Hits: " +
                collisionTrace);
            Assert.That(
                foodCandidate.TryGetCapability(out IPickupTarget pickupTarget),
                Is.True);
            Assert.That(pickupTarget.CanPickup(context), Is.True);
            UnityEngine.Object.Destroy(aimObject);
            yield return null;

            door.Interact(context);
            hinge.Advance(hinge.EstimatedTravelSeconds + 0.1f);
            Assert.That(home.Snapshot.FridgeDoorOpen, Is.False);
            Assert.That(hinge.OpenNormalized, Is.LessThan(0.02f));
            Assert.That(home.FridgeCoolingActive, Is.True);

            home.SetElectricityAvailable(false);
            Assert.That(home.FridgeCoolingActive, Is.False);
            Assert.That(
                appliances.IsRefrigerated(
                    appliances.RefrigeratedWorldBounds.center),
                Is.False);
            home.SetElectricityAvailable(true);

            ItemHeatSourceVolume[] heatSources =
                UnityEngine.Object.FindObjectsByType<ItemHeatSourceVolume>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(
                heatSources.Any(value =>
                    value.name == "Home stove cooking volume"),
                Is.True);
            Assert.That(
                heatSources.Any(value =>
                    value.name == "Cottage mangal cooking volume"),
                Is.True);
        }

        [UnityTest]
        public IEnumerator WholeFoodStaysPhysicalUntilTimedActionCompletes()
        {
            ItemDefinitionCatalog definitions =
                AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                    DefinitionCatalogPath);
            var placements = ScriptableObject.CreateInstance<ItemPlacementCatalog>();
            placements.ConfigureForAuthoring(
                "test.food-playmode.placements",
                "msc-world-baseline-04a1.1-c3f2f337",
                Array.Empty<ItemPlacementRecord>());
            var manifest =
                ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
            manifest.ConfigureForAuthoring(
                512f,
                0,
                1,
                new[]
                {
                    new ProductionWorldCellScene(
                        "test.cell.99.99",
                        99,
                        99,
                        999,
                        "Assets/Tests/Scenes/UnusedFoodPlayModeCell.unity"),
                });
            var streamingObject = new GameObject("Food playmode streaming");
            var runtimeObject = new GameObject("Food playmode runtime");
            ProductionWorldStreamingService streaming =
                streamingObject.AddComponent<ProductionWorldStreamingService>();
            streaming.ConfigureForAuthoring(manifest);
            ItemWorldRuntime runtime =
                runtimeObject.AddComponent<ItemWorldRuntime>();
            runtime.Initialize(
                definitions,
                placements,
                streaming,
                SceneManager.GetActiveScene(),
                -64f,
                new GameTimeService());
            ItemDefinitionRecord macaroni = definitions.Definitions.Single(
                value => string.Equals(
                    value.FeatureId,
                    "P1.ITEM.102",
                    StringComparison.Ordinal));
            WorldItemInstance item = runtime.SpawnDynamic(
                macaroni.DefinitionId,
                ItemStableIdUtility.CreateDeterministic(
                    "test.food-playmode.whole-macaroni"),
                Vector3.zero,
                Quaternion.identity,
                SceneManager.GetActiveScene());
            var actions = new List<ItemActionCompleted>();
            runtime.ActionCompleted += actions.Add;
            var context = new InteractionContext(
                runtimeObject,
                Vector3.zero,
                Vector3.forward);

            try
            {
                Assert.That(item.TryPerformPrimaryAction(context), Is.True);
                Assert.That(item.gameObject.activeSelf, Is.True);
                Assert.That(item.State.isConsumed, Is.False);
                Assert.That(
                    actions.Count(value =>
                        value.Action == ItemActionKind.ConsumptionStarted),
                    Is.EqualTo(1));
                Assert.That(
                    actions.Any(value => value.Action == ItemActionKind.Used),
                    Is.False);

                yield return new WaitForSeconds(
                    macaroni.Food.ConsumptionDurationSeconds + 0.1f);

                Assert.That(item.State.isConsumed, Is.True);
                Assert.That(item.gameObject.activeSelf, Is.False);
                Assert.That(
                    actions.Count(value => value.Action == ItemActionKind.Used),
                    Is.EqualTo(1));
            }
            finally
            {
                runtime.ActionCompleted -= actions.Add;
                if (item != null)
                {
                    UnityEngine.Object.Destroy(item.gameObject);
                }

                UnityEngine.Object.Destroy(runtimeObject);
                UnityEngine.Object.Destroy(streamingObject);
                UnityEngine.Object.Destroy(placements);
                UnityEngine.Object.Destroy(manifest);
            }
        }

        [UnityTearDown]
        public IEnumerator DestroyPersistentProductionSession()
        {
            ProductionWorldStreamingService[] services =
                UnityEngine.Object.FindObjectsByType<
                    ProductionWorldStreamingService>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (ProductionWorldStreamingService service in services)
            {
                service.enabled = false;
                double timeout =
                    Time.realtimeSinceStartupAsDouble + 30d;
                while (service.IsStreaming &&
                       Time.realtimeSinceStartupAsDouble < timeout)
                {
                    yield return null;
                }

                if (!service.IsStreaming)
                {
                    yield return service.UnloadOwnedScenes();
                }
            }

            if (GameCompositionRoot.ActiveRoot != null)
            {
                UnityEngine.Object.Destroy(
                    GameCompositionRoot.ActiveRoot.gameObject);
                yield return null;
            }
        }
    }
}
