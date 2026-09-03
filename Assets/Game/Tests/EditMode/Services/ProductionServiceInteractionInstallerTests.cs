using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MSC.Bootstrap;
using MSC.Core.Identity;
using MSC.Core.Time;
using MSC.Economy;
using MSC.Editor.Services;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Items.Presentation;
using MSC.LegacyImport;
using MSC.Services.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Services.Tests.EditMode
{
    public sealed class ProductionServiceInteractionInstallerTests
    {
        private const string EconomyCatalogPath =
            "Assets/Game/Economy/Content/Phase1/EconomyPriceCatalog.asset";

        private GameObject compositionRoot;
        private GameObject runtimeObject;
        private ServiceCatalog catalog;
        private readonly List<Material> transientMaterials = new();

        [OneTimeSetUp]
        public void BuildServiceCatalog()
        {
            Milestone12AS1ServiceCatalogBuilder.Build();
        }

        [SetUp]
        public void SetUp()
        {
            compositionRoot = new GameObject("ServiceFixtureCompositionRoot");
            runtimeObject = new GameObject("ServiceFixtureRuntime");
            catalog = AssetDatabase.LoadAssetAtPath<ServiceCatalog>(
                Milestone12AS1ServiceCatalogBuilder.CatalogPath);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(runtimeObject);
            UnityEngine.Object.DestroyImmediate(compositionRoot);
            foreach (Material material in transientMaterials)
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            transientMaterials.Clear();
        }

        [Test]
        public void Initialize_MaterializesAuditedOfferFixtures_AndBindsRuntime()
        {
            Assert.That(catalog, Is.Not.Null);
            EconomyPriceCatalog economyCatalog =
                AssetDatabase.LoadAssetAtPath<EconomyPriceCatalog>(
                    EconomyCatalogPath);
            Assert.That(economyCatalog, Is.Not.Null);

            var gameTime = new GameTimeService(
                GameTimeConfig.RemakeDesignTargetDefaults);
            EconomyRuntime economy =
                runtimeObject.AddComponent<EconomyRuntime>();
            economy.Initialize(economyCatalog, gameTime);
            ServiceRuntime services =
                runtimeObject.AddComponent<ServiceRuntime>();
            services.Initialize(catalog, economy, gameTime);
            CreateShelfStockVisuals(
                "9fa9d26ce57c0e6775c90fdfce2530b0",
                8,
                new Vector3(-1378.4702f, 6.8252463f, 140.91614f));
            CreateBeerShelfAssemblies(
                "b4f49514d369345971faede999e6717e",
                5,
                new Vector3(-1375.84f, 5.84f, 142.95f));

            ProductionServiceInteractionInstaller installer =
                runtimeObject.AddComponent<ProductionServiceInteractionInstaller>();

            ExpectAuthoringBlocker("fleetari-order");
            ExpectAuthoringBlocker("inspection-backend");
            installer.Initialize(
                compositionRoot.transform,
                catalog,
                services);

            Assert.That(installer.IsInitialized, Is.True);
            Assert.That(installer.MaterializedTargetCount, Is.EqualTo(74));
            Assert.That(installer.TargetRoot, Is.Not.Null);
            Assert.That(installer.TargetRoot.parent,
                Is.SameAs(compositionRoot.transform));
            Assert.That(installer.AuthoringBlockers, Has.Count.EqualTo(2));

            ServiceInteractionTargetBase[] targets =
                installer.TargetRoot.GetComponentsInChildren<
                    ServiceInteractionTargetBase>(true);
            Assert.That(targets, Has.Length.EqualTo(73));
            Assert.That(installer.TargetRoot.GetComponentsInChildren<
                TeimoStoreOfferInteractionTarget>(true), Has.Length.EqualTo(62));
            StoreShelfStockPresentation[] stockPresentations =
                installer.TargetRoot.GetComponentsInChildren<
                    StoreShelfStockPresentation>(true);
            Assert.That(stockPresentations, Has.Length.EqualTo(62));
            Assert.That(
                stockPresentations.All(value =>
                    !string.IsNullOrWhiteSpace(value.OfferId) &&
                    (!string.IsNullOrWhiteSpace(value.SourceGroupStableId) ||
                     !string.IsNullOrWhiteSpace(value.SourceUnitStableId) ||
                     value.UsesProjectOwnedVisuals && value.IsBound)),
                Is.True,
                "Every physical offer must bind donor evidence or a " +
                "project-owned extension visual.");
            StoreShelfStockPresentation[] expandedShopPresentations =
                stockPresentations.Where(value => value.OfferId.StartsWith(
                    "service.store.expanded-shop-",
                    StringComparison.Ordinal)).ToArray();
            Assert.That(expandedShopPresentations, Has.Length.EqualTo(21));
            Assert.That(
                expandedShopPresentations.All(value =>
                    value.UsesProjectOwnedVisuals && value.IsBound &&
                    value.UsesExactAuthoredCollider &&
                    value.GetComponent<SphereCollider>() is
                        { enabled: false } &&
                    value.GetComponentInChildren<BoxCollider>(true) is
                        { enabled: true }),
                Is.True,
                "Every physical Expanded Shop group must use its extracted " +
                "mod collider instead of a guessed point or renderer bound.");
            ILookup<string, StoreShelfStockPresentation> expandedByOffer =
                expandedShopPresentations.ToLookup(
                    value => value.OfferId,
                    StringComparer.Ordinal);
            Assert.That(expandedByOffer.Count, Is.EqualTo(19));
            foreach (IGrouping<string, StoreShelfStockPresentation> group in
                     expandedByOffer)
            {
                Assert.That(
                    catalog.TryGetOffer(
                        group.Key,
                        out ServiceOfferDefinition extensionOffer),
                    Is.True);
                int visualCount = group.Sum(value => value.BoundUnitCount);
                Assert.That(
                    visualCount,
                    string.Equals(
                        group.Key,
                        "service.store.expanded-shop-sausage",
                        StringComparison.Ordinal)
                        ? Is.Zero
                        : Is.EqualTo(extensionOffer.StockCapacity),
                    group.Key);
            }

            StoreShelfStockPresentation[] mustardGroups =
                expandedByOffer[
                    "service.store.expanded-shop-mustard"].ToArray();
            Assert.That(mustardGroups, Has.Length.EqualTo(2));
            Assert.That(
                mustardGroups.Select(value => value.StockIndexOffset),
                Is.EquivalentTo(new[] { 0, 5 }));
            StoreShelfStockPresentation buttermilkGroup =
                expandedByOffer[
                    "service.store.expanded-shop-buttermilk"].Single();
            Assert.That(
                Vector3.Distance(
                    buttermilkGroup.transform.position,
                    new Vector3(-1374.9468f, 5.9942f, 141.3476f)),
                Is.LessThan(0.0002f));
            Assert.That(
                Quaternion.Angle(
                    buttermilkGroup.transform.rotation,
                    new Quaternion(0f, 0.48098883f, 0f, 0.87672675f)),
                Is.LessThan(0.001f));
            foreach (StoreShelfStockPresentation expandedPresentation in
                     expandedShopPresentations)
            {
                BoxCollider exactCollider = expandedPresentation
                    .GetComponentInChildren<BoxCollider>(true);
                InteractionTargetHost exactHost = exactCollider
                    .GetComponentInParent<InteractionTargetHost>();
                Assert.That(exactHost, Is.Not.Null);
                Assert.That(
                    exactHost.TryGetCapability(
                        out TeimoStoreOfferInteractionTarget capability),
                    Is.True,
                    expandedPresentation.OfferId);
                Assert.That(
                    capability.OfferId,
                    Is.EqualTo(expandedPresentation.OfferId));
                if (!string.Equals(
                        expandedPresentation.OfferId,
                        "service.store.expanded-shop-sausage",
                        StringComparison.Ordinal))
                {
                    Assert.That(
                        exactHost.HasOutlineRendererOverride,
                        Is.True,
                        expandedPresentation.OfferId);
                }
            }

            StoreShelfStockPresentation mustardPrimary = mustardGroups.Single(
                value => value.StockIndexOffset == 0);
            StoreShelfStockPresentation mustardSecondary = mustardGroups.Single(
                value => value.StockIndexOffset == 5);
            Assert.That(
                services.TryAddStoreItem(
                    "service.store.expanded-shop-mustard").Succeeded,
                Is.True);
            Assert.That(
                CountActiveGeneratedShelfUnits(mustardPrimary),
                Is.EqualTo(5));
            Assert.That(
                CountActiveGeneratedShelfUnits(mustardSecondary),
                Is.EqualTo(2));
            Assert.That(
                services.TryRemoveStoreItem(
                    "service.store.expanded-shop-mustard").Succeeded,
                Is.True);
            Assert.That(
                CountActiveGeneratedShelfUnits(mustardSecondary),
                Is.EqualTo(3));
            StoreShelfStockPresentation yeastStock =
                stockPresentations.Single(value => string.Equals(
                    value.OfferId,
                    "service.store.yeast",
                    StringComparison.Ordinal));
            Assert.That(yeastStock.IsBound, Is.True);
            Assert.That(yeastStock.BoundUnitCount, Is.EqualTo(8));
            Assert.That(yeastStock.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(
                yeastStock.GetComponent<SphereCollider>().enabled,
                Is.False,
                "The full shelf bounds must replace the point fallback after binding.");
            Assert.That(
                services.TryAddStoreItem("service.store.yeast").Succeeded,
                Is.True);
            Assert.That(
                CountActiveShelfUnits(
                    "9fa9d26ce57c0e6775c90fdfce2530b0"),
                Is.EqualTo(7));
            DestroyShelfStockVisuals(
                "9fa9d26ce57c0e6775c90fdfce2530b0");
            CreateShelfStockVisuals(
                "9fa9d26ce57c0e6775c90fdfce2530b0",
                8,
                new Vector3(-1378.4702f, 6.8252463f, 140.91614f));
            typeof(StoreShelfStockPresentation).GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(yeastStock, null);
            Assert.That(yeastStock.IsBound, Is.True);
            Assert.That(
                CountActiveShelfUnits(
                    "9fa9d26ce57c0e6775c90fdfce2530b0"),
                Is.EqualTo(7),
                "Reloaded streaming visuals must immediately reapply runtime stock.");
            Assert.That(
                services.TryRemoveStoreItem("service.store.yeast").Succeeded,
                Is.True);
            Assert.That(
                CountActiveShelfUnits(
                    "9fa9d26ce57c0e6775c90fdfce2530b0"),
                Is.EqualTo(8));
            StoreShelfStockPresentation beerStock =
                stockPresentations.Single(value => string.Equals(
                    value.OfferId,
                    "service.store.beer",
                    StringComparison.Ordinal));
            Assert.That(beerStock.IsBound, Is.True);
            Assert.That(beerStock.BoundUnitCount, Is.EqualTo(5));
            Assert.That(
                services.TryAddStoreItem("service.store.beer").Succeeded,
                Is.True);
            Assert.That(
                CountActiveShelfUnits(
                    "b4f49514d369345971faede999e6717e"),
                Is.EqualTo(4));
            Assert.That(
                compositionRoot.GetComponentsInChildren<
                        DonorWorldBaselineEntityMetadata>(true)
                    .Count(value => value.name.StartsWith(
                        "Beer bottle visual",
                        StringComparison.Ordinal) &&
                        value.gameObject.activeSelf),
                Is.EqualTo(4),
                "A consumed beer stock unit must hide its flattened bottle " +
                "renderer together with the crate.");
            Assert.That(installer.TargetRoot.GetComponentsInChildren<
                TeimoStoreCheckoutInteractionTarget>(true), Has.Length.EqualTo(1));
            Assert.That(installer.TargetRoot.GetComponentsInChildren<
                TeimoPubOfferInteractionTarget>(true), Has.Length.EqualTo(5));
            Assert.That(installer.TargetRoot.GetComponentsInChildren<
                ServiceFuelDispenseInteractionTarget>(true), Is.Empty);
            Assert.That(installer.TargetRoot.GetComponentsInChildren<
                ServiceFuelNozzle>(true), Has.Length.EqualTo(3));
            Assert.That(installer.TargetRoot.GetComponentsInChildren<
                FleetariWorkshopInteractionTarget>(true), Is.Empty);
            Assert.That(installer.TargetRoot.GetComponentsInChildren<
                FleetariWorkshopCatalogInteractionTarget>(true), Has.Length.EqualTo(1));
            Assert.That(installer.TargetRoot.GetComponentsInChildren<
                VehicleInspectionInteractionTarget>(true), Has.Length.EqualTo(1));
            HomePartsCatalogInteractionTarget homeCatalog = installer.TargetRoot
                .GetComponentInChildren<HomePartsCatalogInteractionTarget>(true);
            Assert.That(homeCatalog, Is.Not.Null);
            Assert.That(homeCatalog.EntryCount, Is.EqualTo(46));
            Assert.That(homeCatalog, Is.InstanceOf<IToolActivationTarget>());
            Assert.That(homeCatalog, Is.Not.InstanceOf<IContextInteractionTarget>(),
                "The physical catalog must reserve LMB for pickup and F for reading.");
            Assert.That(homeCatalog.transform.position, Is.EqualTo(
                new Vector3(155.2289f, 2.0370998f, -1040.5181f)));
            Assert.That(homeCatalog.GetComponent<
                HomePartsCatalogPhysicalBinding>(), Is.Not.Null);

            for (int index = 0; index < targets.Length; index++)
            {
                ServiceInteractionTargetBase target = targets[index];
                Assert.That(
                    target.TryValidateBinding(out string failure),
                    Is.True,
                    failure);
                Assert.That(
                    catalog.TryGetLocation(
                        target.LocationId,
                        out ServiceLocationDefinition location),
                    Is.True,
                    target.LocationId);
                if (!(target is TeimoStoreOfferInteractionTarget) &&
                    !(target is TeimoPubOfferInteractionTarget) &&
                    !(target is FleetariWorkshopCatalogInteractionTarget))
                {
                    Assert.That(
                        Vector3.Distance(
                            target.transform.position,
                            location.WorldPosition),
                        Is.LessThan(0.0001f),
                        target.LocationId);
                }

                if (target is FleetariWorkshopCatalogInteractionTarget)
                {
                    Assert.That(target.transform.position, Is.EqualTo(
                        new Vector3(
                            1724.3868f,
                            7.0918975f,
                            -301.79944f)));
                }

                SphereCollider collider =
                    target.GetComponent<SphereCollider>();
                Assert.That(collider, Is.Not.Null, target.LocationId);
                Assert.That(collider.isTrigger, Is.False, target.LocationId);
                InteractionTargetHost host =
                    target.GetComponent<InteractionTargetHost>();
                Assert.That(host, Is.Not.Null, target.LocationId);
                Assert.That(host.HasCapabilities, Is.True, target.LocationId);
            }

            ServiceFuelNozzle[] fuelTargets =
                installer.TargetRoot.GetComponentsInChildren<
                    ServiceFuelNozzle>(true);
            for (int index = 0; index < fuelTargets.Length; index++)
            {
                Assert.That(fuelTargets[index].StableId.IsValid, Is.True);
                Assert.That(fuelTargets[index].FillVolume, Is.Not.Null);
                Assert.That(fuelTargets[index].FillVolume.Trigger.isTrigger,
                    Is.True);
            }

            FleetariWorkshopCatalogInteractionTarget workshop =
                installer.TargetRoot.GetComponentInChildren<
                    FleetariWorkshopCatalogInteractionTarget>(true);
            Assert.That(workshop.OfferCount, Is.EqualTo(32));
            Assert.That(workshop, Is.InstanceOf<IToolActivationTarget>());
            var context = new InteractionContext(
                runtimeObject,
                runtimeObject.transform.position,
                Vector3.forward);
            TeimoStoreOfferInteractionTarget[] sprayTargets = installer.TargetRoot
                .GetComponentsInChildren<TeimoStoreOfferInteractionTarget>(true)
                .Where(value => value.OfferId.StartsWith(
                    "service.store.spray",
                    StringComparison.Ordinal))
                .OrderBy(value => value.OfferId, StringComparer.Ordinal)
                .ToArray();
            Assert.That(sprayTargets, Has.Length.EqualTo(13));
            foreach (TeimoStoreOfferInteractionTarget sprayTarget in sprayTargets)
            {
                sprayTarget.Interact(context);
                Assert.That(
                    services.GetBasketQuantity(sprayTarget.OfferId),
                    Is.EqualTo(1),
                    "Every physical spray-can variant must enter the " +
                    "checkout basket: " + sprayTarget.OfferId);
            }
            string firstWorkshopOffer = workshop.CurrentOfferId;
            workshop.TryActivateEntry(0);
            Assert.That(
                services.CaptureWorkshopSelection().offerIds,
                Does.Contain(firstWorkshopOffer));
            workshop.TryActivateEntry(0);
            Assert.That(
                services.CaptureWorkshopSelection().offerIds,
                Does.Not.Contain(firstWorkshopOffer));

            string firstHomeEntry = homeCatalog.CurrentEntryId;
            homeCatalog.TryActivateEntry(0);
            Assert.That(homeCatalog.SelectedCount, Is.EqualTo(1));
            Assert.That(homeCatalog.SelectedTotalMinorUnits, Is.GreaterThan(0));
            homeCatalog.TryActivateEntry(1);
            Assert.That(homeCatalog.CurrentEntryId, Is.Not.EqualTo(firstHomeEntry));
            Assert.That(installer.TargetRoot.GetComponentsInChildren<
                PhysicalServiceCatalogController>(true), Has.Length.EqualTo(2));

            ServiceFuelNozzle gasolineNozzle = fuelTargets.Single(target =>
                string.Equals(
                    target.OfferId,
                    "service.fuel.gasoline98",
                    StringComparison.Ordinal));
            var receiverObject = new GameObject("Open gasoline can probe");
            receiverObject.transform.SetParent(compositionRoot.transform);
            BoxCollider receiverCollider =
                receiverObject.AddComponent<BoxCollider>();
            LiquidContainerProbe receiver =
                receiverObject.AddComponent<LiquidContainerProbe>();
            Assert.That(gasolineNozzle.CanPickup(context), Is.True);
            gasolineNozzle.NotifyPickedUp(context);
            Assert.That(
                gasolineNozzle.FillVolume.TryFill(receiverCollider, 0.5f),
                Is.True);
            Assert.That(receiver.LiquidId, Is.EqualTo("liquid.gasoline"));
            Assert.That(receiver.AmountLitres, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(
                services.CaptureDto().fuelDebts.Single().dispensedMilliliters,
                Is.EqualTo(400));
            gasolineNozzle.NotifyReleased(PickupReleaseReason.Dropped);

            GameObject fridgeVisual = CreateKitchenEntity(
                "Fridge door visual",
                "aa1770c98926f9b7819fec2fdb814c5b",
                "dba046cb262c15b7563e3db01d958d8d",
                "STORE/LOD/GFX_Pub/FridgeNappo/Pivot/mesh",
                new Vector3(-1378.7239f, 6.346943f, 143.88892f),
                new Quaternion(
                    0.6202418f,
                    0.33955863f,
                    -0.33955848f,
                    0.62024194f));
            GameObject microwaveVisual = CreateKitchenEntity(
                "Microwave door visual",
                "fb8faf3860928e4e72742595f8eb0152",
                "e987cbf980f7f153ad92a0f7db863845",
                "STORE/LOD/GFX_Pub/Microwave/microwave_door/mesh",
                new Vector3(-1377.9819f, 7.1120005f, 143.89807f),
                new Quaternion(
                    0.33483747f,
                    0.6228033f,
                    0.6228034f,
                    -0.33483735f));
            GameObject microwaveFood = CreateKitchenEntity(
                "Microwave food visual",
                "30dcc88157750906751cee877504b5d6",
                "6d3921a4d6b394be9ab7030ca2c3ca91",
                "STORE/LOD/GFX_Pub/Microwave/GrillboxMicro",
                new Vector3(-1377.961f, 7.0442004f, 143.91187f),
                Quaternion.identity);
            MethodInfo bindKitchen = typeof(TeimoServicePresentationDirector)
                .GetMethod(
                    "BindKitchenFixtures",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(bindKitchen, Is.Not.Null);
            object[] kitchenArguments = { null, null, null };
            bindKitchen.Invoke(null, kitchenArguments);
            Transform fridgePivot = (Transform)kitchenArguments[0];
            Transform microwavePivot = (Transform)kitchenArguments[1];
            Assert.That(fridgePivot, Is.Not.Null);
            Assert.That(microwavePivot, Is.Not.Null);
            Assert.That(
                Vector3.Distance(
                    fridgePivot.position,
                    new Vector3(-1379.0602f, 6.772943f, 143.67981f)),
                Is.LessThan(0.0002f));
            Assert.That(
                Vector3.Distance(
                    microwavePivot.position,
                    new Vector3(-1377.9106f, 7.1120143f, 144.1311f)),
                Is.LessThan(0.0002f));
            Assert.That(fridgeVisual.transform.parent, Is.SameAs(fridgePivot));
            Assert.That(microwaveVisual.transform.parent,
                Is.SameAs(microwavePivot));
            Assert.That(kitchenArguments[2], Is.SameAs(microwaveFood));
            AssertPrivateVector3(
                typeof(TeimoServicePresentationDirector),
                "DonorKitchenMotionStart",
                new Vector3(-6.5f, 0f, 0f));
            AssertPrivateVector3(
                typeof(TeimoServicePresentationDirector),
                "BoxedMealInItemPivotPosition",
                new Vector3(0.0149f, -0.0625f, 0.0112f));
            AssertPrivateVector3(
                typeof(TeimoServicePresentationDirector),
                "RightHandMealPosition",
                new Vector3(-0.165f, -0.033f, -0.078f));
            AssertPrivateQuaternion(
                typeof(TeimoServicePresentationDirector),
                "BoxedMealInItemPivotRotation",
                new Quaternion(
                    -0.1372799f,
                    0.18210278f,
                    0.65559274f,
                    -0.71985483f));
            MethodInfo spawnOffset = typeof(ServiceItemHandoffBackend)
                .GetMethod(
                    "SpawnOffset",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(spawnOffset, Is.Not.Null);
            Vector3 pubOffset = (Vector3)spawnOffset.Invoke(
                null,
                new object[] { "service.location.teimo-pub", 0, 0 });
            Assert.That(pubOffset, Is.EqualTo(Vector3.zero),
                "The first pub item must not be lifted above the exact donor pivot.");
            MethodInfo resolveSpawnPosition = typeof(ServiceItemHandoffBackend)
                .GetMethod(
                    "ResolveSpawnPosition",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolveSpawnPosition, Is.Not.Null);
            Vector3 resolvedBeerPosition = (Vector3)resolveSpawnPosition.Invoke(
                null,
                new object[]
                {
                    "service.location.teimo-pub",
                    "item.beer-bottle",
                    Vector3.one * 999f,
                    0,
                    0,
                });
            Assert.That(
                Vector3.Distance(
                    resolvedBeerPosition,
                    new Vector3(-1375.5087f, 6.305682f, 145.8352f)),
                Is.LessThan(0.0002f),
                "Pub handoff must use the donor object pivot without a second vertical lift.");
            Assert.That(
                Vector3.Distance(
                    ServiceHandoffPlacementPolicy.GetCounterPosition(
                        "service.location.teimo-pub",
                        "item.coffee-cup",
                        Vector3.zero),
                    new Vector3(-1375.5087f, 6.305682f, 145.8352f)),
                Is.LessThan(0.0002f),
                "Ordinary goods must use the donor DrinkSpawnPoint.");
            Assert.That(
                Vector3.Distance(
                    ServiceHandoffPlacementPolicy.GetCounterPosition(
                        "service.location.teimo-pub",
                        "item.sausage-and-potatoes-meal",
                        Vector3.zero),
                    new Vector3(-1375.6229f, 6.333628f, 145.78503f)),
                Is.LessThan(0.0002f),
                "The prepared meal must use the donor FoodSpawnPoint.");
            Assert.That(
                Quaternion.Angle(
                    ServiceHandoffPlacementPolicy.GetCounterRotation(
                        "service.location.teimo-pub",
                        "item.sausage-and-potatoes-meal"),
                    new Quaternion(
                        -0.30169034f,
                        0.64968413f,
                        0.48720184f,
                        0.49952763f)),
                Is.LessThan(0.001f),
                "The meal must use the donor FoodSpawnPoint rotation.");
            Assert.That(
                Quaternion.Angle(
                    ServiceHandoffPlacementPolicy.GetCounterRotation(
                        "service.location.teimo-pub",
                        "item.coffee-cup"),
                    new Quaternion(
                        0.22377104f,
                        0.6889811f,
                        0.6330759f,
                        -0.27284887f)),
                Is.LessThan(0.001f),
                "Under-counter goods must use the donor DrinkSpawnPoint rotation.");
            Assert.That(services.WorkshopOrderingAvailable, Is.False);
            Assert.That(services.InspectionOrderingAvailable, Is.False);
        }

        [Test]
        public void HomeCatalogBinding_PreservesPickup_AndFollowsPhysicalItem()
        {
            const string stableId = "b4eb2f73c8b85239a718a4153e4f903a";
            GameObject itemObject = new GameObject("Physical parts magazine");
            GameObject anchorObject = new GameObject("Catalog interaction anchor");
            itemObject.transform.SetParent(compositionRoot.transform);
            anchorObject.transform.SetParent(compositionRoot.transform);

            Rigidbody body = itemObject.AddComponent<Rigidbody>();
            body.useGravity = true;
            BoxCollider itemCollider = itemObject.AddComponent<BoxCollider>();
            StableEntityIdAuthoring identity = itemObject.AddComponent<
                StableEntityIdAuthoring>();
            Assert.That(StableEntityId.TryParse(
                stableId,
                out StableEntityId parsedId), Is.True);
            identity.InitializeExplicitRuntimeId(parsedId);
            PhysicsPickupTarget pickup = itemObject.AddComponent<
                PhysicsPickupTarget>();
            pickup.Configure(body, identity, "Поднять", 35f, true);
            MSC.Items.WorldItemInstance item = itemObject.AddComponent<
                MSC.Items.WorldItemInstance>();
            typeof(MSC.Items.WorldItemInstance)
                .GetField("identity", BindingFlags.Instance |
                                      BindingFlags.NonPublic)
                ?.SetValue(item, identity);
            InteractionTargetHost itemHost = itemObject.AddComponent<
                InteractionTargetHost>();
            itemHost.Configure(pickup);

            SphereCollider anchorCollider = anchorObject.AddComponent<
                SphereCollider>();
            InteractionTargetHost anchorHost = anchorObject.AddComponent<
                InteractionTargetHost>();
            HomePartsCatalogInteractionTarget target = anchorObject.AddComponent<
                HomePartsCatalogInteractionTarget>();
            target.ConfigureForAuthoring(new[]
            {
                new HomePartsMailOrderOffer("part.test", "Test part", 100L),
            });
            HomePartsCatalogPhysicalBinding binding = anchorObject.AddComponent<
                HomePartsCatalogPhysicalBinding>();
            Assert.That(item.StableId.Value, Is.EqualTo(stableId));
            Assert.That(UnityEngine.Object.FindObjectsByType<
                MSC.Items.WorldItemInstance>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None), Does.Contain(item));
            binding.Configure(stableId, null, target);

            var context = new InteractionContext(
                runtimeObject,
                Vector3.zero,
                Vector3.forward);
            Assert.That(pickup.CanPickup(context), Is.True);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(anchorCollider.enabled, Is.False);
            Assert.That(itemHost.TryGetCapability(
                out IPickupTarget registeredPickup), Is.True);
            Assert.That(registeredPickup, Is.SameAs(pickup));
            Assert.That(itemHost.TryGetCapability(
                out IToolActivationTarget readTarget), Is.True);
            Assert.That(readTarget, Is.SameAs(target));

            itemObject.transform.position = new Vector3(4f, 2f, -7f);
            itemObject.transform.rotation = Quaternion.Euler(25f, 63f, 17f);
            binding.SynchronizePose();
            Assert.That(anchorObject.transform.position,
                Is.EqualTo(itemObject.transform.position));
            Assert.That(
                Vector3.Distance(anchorObject.transform.up, Vector3.up),
                Is.LessThan(0.0001f));

            var candidate = new InteractionCandidate(
                itemHost,
                itemObject.transform.position,
                Vector3.up,
                1f,
                itemCollider);
            Assert.That(candidate.GetPrompt(false, context),
                Does.Contain("ЛКМ").And.Contain("F"));
        }

        [Test]
        public void PhysicalCatalogs_KeepAuditedPageCounts_WhenArtIsUnavailable()
        {
            GameObject homeObject = new GameObject("Home catalog pages");
            GameObject fleetariObject = new GameObject("Fleetari catalog pages");
            homeObject.transform.SetParent(compositionRoot.transform);
            fleetariObject.transform.SetParent(compositionRoot.transform);
            var selection = new BookSelectionStub();

            PhysicalServiceCatalogController home = homeObject.AddComponent<
                PhysicalServiceCatalogController>();
            home.Configure(
                LegacyServiceCatalogKind.HomeParts,
                selection,
                null,
                null,
                null,
                Array.Empty<Material>(),
                null);
            PhysicalServiceCatalogController fleetari =
                fleetariObject.AddComponent<PhysicalServiceCatalogController>();
            fleetari.Configure(
                LegacyServiceCatalogKind.FleetariServices,
                selection,
                null,
                null,
                null,
                Array.Empty<Material>(),
                null);

            Assert.That(home.PageCount, Is.EqualTo(5));
            Assert.That(fleetari.PageCount, Is.EqualTo(7));
        }

        [Test]
        public void HomeCatalog_ForwardTurn_LiftsRightSheetTowardLeft()
        {
            GameObject catalogObject = new GameObject("Home page turn");
            catalogObject.transform.SetParent(compositionRoot.transform);
            PhysicalServiceCatalogController catalog = catalogObject.AddComponent<
                PhysicalServiceCatalogController>();
            catalog.Configure(
                LegacyServiceCatalogKind.HomeParts,
                new BookSelectionStub(),
                null,
                null,
                null,
                Array.Empty<Material>(),
                null);

            MethodInfo beginTurn = typeof(PhysicalServiceCatalogController)
                .GetMethod(
                    "BeginPageTurn",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo updateTurn = typeof(PhysicalServiceCatalogController)
                .GetMethod(
                    "UpdatePageTurn",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(beginTurn, Is.Not.Null);
            Assert.That(updateTurn, Is.Not.Null);

            beginTurn.Invoke(catalog, new object[] { 1 });
            Transform hinge = catalogObject.transform.Find(
                "Physical catalog reading view/Catalog turning page hinge");
            Transform sheet = hinge?.Find("Catalog turning page");
            Assert.That(hinge, Is.Not.Null);
            Assert.That(sheet, Is.Not.Null);
            Assert.That(sheet.localPosition.x, Is.GreaterThan(0f));

            updateTurn.Invoke(catalog, new object[] { 0.12f });
            Assert.That(
                Mathf.DeltaAngle(0f, hinge.localEulerAngles.z),
                Is.GreaterThan(0f),
                "Forward browsing must lift the right page and carry it left.");
        }

        [Test]
        public void PhysicalCatalog_BindsLateProviderPages_AndCreatesFleetariVisual()
        {
            GameObject catalogObject = new GameObject("Late Fleetari catalog");
            catalogObject.transform.SetParent(compositionRoot.transform);
            var selection = new BookSelectionStub();
            PhysicalServiceCatalogController catalogController =
                catalogObject.AddComponent<PhysicalServiceCatalogController>();
            catalogController.Configure(
                LegacyServiceCatalogKind.FleetariServices,
                selection,
                null,
                null,
                null,
                Array.Empty<Material>(),
                null);

            Material homeCover = CreateTestMaterial("Home cover");
            Material fleetariCover = CreateTestMaterial("Fleetari cover");
            Material[] homePages = CreateTestMaterials("Home page", 8);
            Material homeOrder = CreateTestMaterial("Home order");
            Material[] fleetariPages = CreateTestMaterials("Fleetari page", 6);
            Material fleetariOrder = CreateTestMaterial("Fleetari order");

            GameObject providerObject = new GameObject("Late catalog provider");
            providerObject.transform.SetParent(compositionRoot.transform);
            providerObject.SetActive(false);
            LegacyServiceCatalogPresentationProvider provider =
                providerObject.AddComponent<
                    LegacyServiceCatalogPresentationProvider>();
            provider.ConfigureForAuthoring(
                homeCover,
                fleetariCover,
                homePages,
                homeOrder,
                fleetariPages,
                fleetariOrder);
            providerObject.SetActive(true);
            typeof(LegacyServiceCatalogPresentationProvider)
                .GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(provider, null);

            MethodInfo refresh = typeof(PhysicalServiceCatalogController)
                .GetMethod(
                    "TryRefreshPresentation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refresh, Is.Not.Null);
            Assert.That(refresh.Invoke(
                catalogController,
                new object[] { true }), Is.EqualTo(true));

            Material[] boundPages = (Material[])typeof(
                    PhysicalServiceCatalogController)
                .GetField(
                    "contentMaterials",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(catalogController);
            Assert.That(boundPages, Is.EqualTo(fleetariPages));
            Assert.That(catalogObject.transform.Find(
                    "Temporary reviewed Fleetari service brochure"),
                Is.Not.Null,
                "A provider arriving after service installation must restore " +
                "the previously invisible Fleetari brochure.");

            Renderer pageRenderer = catalogObject
                .GetComponentsInChildren<Renderer>(true)
                .Single(value => value.gameObject.name ==
                                 "Catalog product page");
            Assert.That(pageRenderer.sharedMaterial,
                Is.SameAs(fleetariPages[0]));
        }

        private Material CreateTestMaterial(string materialName)
        {
            Shader shader = Shader.Find("HDRP/Lit") ??
                            Shader.Find("Hidden/InternalErrorShader");
            var material = new Material(shader)
            {
                name = materialName,
            };
            transientMaterials.Add(material);
            return material;
        }

        private Material[] CreateTestMaterials(string prefix, int count)
        {
            var result = new Material[count];
            for (int index = 0; index < count; index++)
            {
                result[index] = CreateTestMaterial($"{prefix} {index + 1}");
            }

            return result;
        }

        private static void AssertPrivateVector3(
            Type ownerType,
            string fieldName,
            Vector3 expected)
        {
            FieldInfo field = ownerType.GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Vector3 actual = (Vector3)field.GetValue(null);
            Assert.That(
                Vector3.Distance(actual, expected),
                Is.LessThan(0.0002f),
                fieldName + " drifted from the audited Teimo service choreography.");
        }

        private static void AssertPrivateQuaternion(
            Type ownerType,
            string fieldName,
            Quaternion expected)
        {
            FieldInfo field = ownerType.GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Quaternion actual = (Quaternion)field.GetValue(null);
            Assert.That(
                Quaternion.Angle(actual, expected),
                Is.LessThan(0.001f),
                fieldName + " drifted from the audited Teimo service choreography.");
        }

        private static void ExpectAuthoringBlocker(string blockerId)
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex(
                    "SERVICE-WORLD-BLOCKER " +
                    Regex.Escape(blockerId) +
                    ":.*"));
        }

        private void CreateShelfStockVisuals(
            string sourceGroupStableId,
            int count,
            Vector3 center)
        {
            for (int index = 0; index < count; index++)
            {
                GameObject unit = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                unit.name = "Reviewed shelf stock unit " + index;
                unit.transform.SetParent(compositionRoot.transform);
                unit.transform.position = center +
                    Vector3.right * (index - (count - 1) * 0.5f) * 0.08f;
                unit.transform.localScale = new Vector3(0.06f, 0.12f, 0.06f);
                UnityEngine.Object.DestroyImmediate(
                    unit.GetComponent<BoxCollider>());
                DonorWorldBaselineEntityMetadata metadata =
                    unit.AddComponent<DonorWorldBaselineEntityMetadata>();
                metadata.Configure(
                    index.ToString("x32"),
                    index + 1L,
                    sourceGroupStableId,
                    "STORE/LOD/PRODUCTS/Yeast/" + (index + 1),
                    string.Empty,
                    "cell_-3_0",
                    "BuildingInterior",
                    "23;33",
                    "TemporaryDirectImport",
                    "Test shelf unit",
                    true,
                    true,
                    true);
            }
        }

        private int CountActiveShelfUnits(string sourceGroupStableId) =>
            compositionRoot.GetComponentsInChildren<
                    DonorWorldBaselineEntityMetadata>(true)
                .Count(value => string.Equals(
                    value.SourceParentStableId,
                    sourceGroupStableId,
                    StringComparison.Ordinal) &&
                    value.gameObject.activeSelf);

        private static int CountActiveGeneratedShelfUnits(
            StoreShelfStockPresentation presentation)
        {
            ExpandedShopShelfGroupPresentation generated = presentation
                .GetComponentInChildren<
                    ExpandedShopShelfGroupPresentation>(true);
            Assert.That(generated, Is.Not.Null, presentation.OfferId);
            return generated.StockUnits.Count(value => value.activeSelf);
        }

        private void CreateBeerShelfAssemblies(
            string sourceGroupStableId,
            int count,
            Vector3 center)
        {
            for (int index = 0; index < count; index++)
            {
                string crateStableId = (0x1000 + index).ToString("x32");
                GameObject crate = CreateShelfMember(
                    "Beer crate visual " + index,
                    crateStableId,
                    sourceGroupStableId,
                    center + Vector3.up * index * 0.2f,
                    "STORE/LOD/PRODUCTS/Beer/" + (index + 1));
                crate.transform.localScale = new Vector3(0.3f, 0.08f, 0.22f);

                GameObject bottles = CreateShelfMember(
                    "Beer bottle visual " + index,
                    (0x2000 + index).ToString("x32"),
                    crateStableId,
                    crate.transform.position + Vector3.up * 0.08f,
                    "STORE/LOD/PRODUCTS/Beer/" + (index + 1) +
                    "/beer_bottles");
                bottles.transform.localScale = new Vector3(
                    0.28f,
                    0.12f,
                    0.2f);
            }
        }

        private GameObject CreateShelfMember(
            string displayName,
            string stableId,
            string parentStableId,
            Vector3 position,
            string hierarchyPath)
        {
            GameObject member = GameObject.CreatePrimitive(PrimitiveType.Cube);
            member.name = displayName;
            member.transform.SetParent(compositionRoot.transform);
            member.transform.position = position;
            UnityEngine.Object.DestroyImmediate(
                member.GetComponent<BoxCollider>());
            DonorWorldBaselineEntityMetadata metadata =
                member.AddComponent<DonorWorldBaselineEntityMetadata>();
            metadata.Configure(
                stableId,
                1L,
                parentStableId,
                hierarchyPath,
                string.Empty,
                "cell_-3_0",
                "BuildingInterior",
                "23;33",
                "TemporaryDirectImport",
                "Flattened beer shelf assembly test member",
                true,
                true,
                true);
            return member;
        }

        private void DestroyShelfStockVisuals(string sourceGroupStableId)
        {
            DonorWorldBaselineEntityMetadata[] units = compositionRoot
                .GetComponentsInChildren<DonorWorldBaselineEntityMetadata>(true)
                .Where(value => string.Equals(
                    value.SourceParentStableId,
                    sourceGroupStableId,
                    StringComparison.Ordinal))
                .ToArray();
            foreach (DonorWorldBaselineEntityMetadata unit in units)
            {
                UnityEngine.Object.DestroyImmediate(unit.gameObject);
            }
        }

        private GameObject CreateKitchenEntity(
            string displayName,
            string stableId,
            string parentStableId,
            string hierarchyPath,
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            var entityObject = new GameObject(displayName);
            entityObject.transform.SetParent(compositionRoot.transform);
            entityObject.transform.SetPositionAndRotation(
                worldPosition,
                worldRotation);
            DonorWorldBaselineEntityMetadata metadata =
                entityObject.AddComponent<DonorWorldBaselineEntityMetadata>();
            metadata.Configure(
                stableId,
                1L,
                parentStableId,
                hierarchyPath,
                string.Empty,
                "cell_-3_0",
                "BuildingExterior",
                "4;23;33",
                "RendererAccepted",
                "Test fixture",
                true,
                true,
                true);
            return entityObject;
        }

        private sealed class LiquidContainerProbe :
            MonoBehaviour,
            ILiquidContainerTarget
        {
            public string LiquidId { get; private set; } = string.Empty;
            public float AmountLitres { get; private set; }

            public bool CanAcceptLiquid(
                string liquidId,
                float requestedLitres) =>
                requestedLitres > 0f &&
                (string.IsNullOrEmpty(LiquidId) ||
                 string.Equals(LiquidId, liquidId, StringComparison.Ordinal));

            public bool TryAcceptLiquid(
                string liquidId,
                float requestedLitres,
                out float acceptedLitres)
            {
                acceptedLitres = 0f;
                if (!CanAcceptLiquid(liquidId, requestedLitres))
                {
                    return false;
                }

                LiquidId = liquidId;
                AmountLitres += requestedLitres;
                acceptedLitres = requestedLitres;
                return true;
            }
        }

        private sealed class BookSelectionStub :
            IServiceCatalogBookSelection
        {
            public int EntryCount => 1;
            public int CurrentIndex => 0;
            public int SelectedCount => 0;
            public long SelectedTotalMinorUnits => 0L;

            public ServiceCatalogBookEntry GetEntry(int index) =>
                new("entry.test", "Test entry", 100L);

            public bool IsEntrySelected(int index) => false;

            public bool TryActivateEntry(
                int index,
                float variant = float.NaN) => false;

            public bool TryConfirm() => false;
        }
    }
}
