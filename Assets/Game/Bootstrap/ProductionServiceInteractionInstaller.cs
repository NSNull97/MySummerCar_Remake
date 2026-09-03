using System;
using System.Collections.Generic;
using MSC.Core.Lifecycle;
using MSC.Core.Identity;
using MSC.Interaction.Query;
using MSC.Items;
using MSC.Items.Presentation;
using MSC.Services;
using MSC.Services.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Materializes the bounded Phase-1 service fixtures from project-owned
    /// catalog IDs and audited world coordinates. Donor hierarchy names are
    /// evidence only and are never queried at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionServiceInteractionInstaller : MonoBehaviour
    {
        private const string StoreLocationId =
            "service.location.teimo-store";
        private const string PubLocationId =
            "service.location.teimo-pub";
        private const string GasolineLocationId =
            "service.location.teimo-fuel";
        private const string DieselLocationId =
            "service.location.teimo-fuel-diesel";
        private const string FuelOilLocationId =
            "service.location.teimo-fuel-oil";
        private const string WorkshopLocationId =
            "service.location.workshop.fleetari";
        private const string InspectionLocationId =
            "service.location.inspection-station";

        private const string GasolineOfferId =
            "service.fuel.gasoline98";
        private const string DieselOfferId =
            "service.fuel.diesel";
        private const string FuelOilOfferId =
            "service.fuel.fuel-oil";
        private const string InspectionOfferId =
            "service.offer.inspection.vehicle";

        private const int ExpectedStoreShelfOfferCount = 60;
        private const int ExpectedExpandedShopOfferCount = 19;
        private const int ExpectedExpandedShopPhysicalGroupCount = 21;
        private const float FixtureRadius = 0.14f;
        private const string HomePartsMagazineStableId =
            "b4eb2f73c8b85239a718a4153e4f903a";
        private static readonly Vector3 FleetariBrochurePosition =
            new(1724.3868f, 7.0918975f, -301.79944f);
        private static readonly Vector3 HomePartsCatalogPosition =
            new(155.2289f, 2.0370998f, -1040.5181f);
        private static readonly Vector3 MailOrderBoxPosition =
            new(-1378.4673f, 6.2990003f, 138.4563f);
        private static readonly Vector3 MailOrderPaymentPosition =
            new(-1381.2434f, 6.2384853f, 142.10864f);
        private static readonly Vector3 MailOrderDeliveryOrigin =
            new(-1384.95f, 5.62f, 143.25f);

        private static readonly OfferFixtureSeed[] StoreOfferFixtures =
        {
            new("service.store.yeast", -1378.4702f, 6.8252463f, 140.91614f, "9fa9d26ce57c0e6775c90fdfce2530b0"),
            new("service.store.pizza", -1379.5189f, 5.9512467f, 142.96606f, "b7d7e69245a16ed3794eb248cf5b5df6"),
            new("service.store.milk", -1375.2006f, 6.661248f, 141.3905f, "88fbbbfda04358457d017b1a85de3601"),
            new("service.store.sugar", -1379.0153f, 6.846246f, 140.5675f, "178d680e9b718c03979eea513634e100"),
            new("service.store.chips", -1377.7456f, 6.765248f, 143.4878f, "75328d26a6fcc0409587d05ca70726c7"),
            new("service.store.macaron-box", -1379.7692f, 5.9582467f, 142.80603f, "bd256db3f9ac9b0fe610de05b0f4c14d"),
            new("service.store.cigarettes", -1380.4926f, 6.341246f, 142.43005f, "89695cb6739c2e88aaad1adcbcb37465"),
            new("service.store.juice", -1377.7311f, 6.841247f, 141.86719f, "ed06ad6a86007275428fb05001534a4f"),
            new("service.store.fuse-package", -1377.5304f, 6.337246f, 139.67139f, "e972fb12ac58a286767a3ce219d86ffb"),
            new("service.store.charcoal", -1376.013f, 5.6182475f, 140.74158f, "7e1f3a72e872cfedcd503c140fb26e2f"),
            new("service.store.sausages", -1380.069f, 5.958246f, 142.58813f, "c9b76e29e65ec368417b1c2796ae8abd"),
            new("service.store.r20-battery-box", -1377.3004f, 6.3372464f, 139.81848f, "01c36e91901ae9e40d041ad5cd0ea02e"),
            new("service.store.mosquito-spray", -1378.2347f, 6.6852455f, 139.26746f, "bfdf6ef3c7639cc5d26fb7dd60001547"),
            new("service.store.coffee", -1379.4562f, 6.5552454f, 140.34009f, "69cd14fe7e4196c5323ad85635c8e1e0"),
            new("service.store.beer", -1375.8824f, 5.8462486f, 142.90588f, "b4f49514d369345971faede999e6717e"),
            new("service.store.brake-fluid", -1380.3457f, 6.772775f, 138.5935f, "560cb2b3026ddd3cfc04b571243db9a5"),
            new("service.store.car-battery", -1381.0619f, 6.3480144f, 139.71338f, "62d74ab813fed798aa96a80bd10583cd"),
            new("service.store.coolant", -1380.49f, 6.395463f, 138.85767f, "4c999d9cfb6d8dd748eb6206c5c21209"),
            new("service.store.extinguisher", -1380.3799f, 7.1706486f, 138.6991f, "2afa801a1d6aa37512a68f37bc71e028"),
            new("service.store.fanbelt", -1380.2786f, 7.128347f, 138.08704f, "ddbc3fa3b71117133d9d4a0638b49edc"),
            new("service.store.lightbulb", -1381.5082f, 7.060901f, 140.47412f, "5c490b3ad5545dee4c25b5a91c286783"),
            new("service.store.motor-oil", -1380.7574f, 6.773319f, 139.29419f, "f03bdd19a2c8e165afdd324b0b447f9d"),
            new("service.store.oilfilter", -1381.3983f, 6.3209105f, 140.25916f, "9a3dedf636b6dbcf5915ab6cce9c995c"),
            new("service.store.sparkplugs", -1381.5491f, 6.3196125f, 140.526f, "91608d8f17f3756857cc0696a73c564e"),
            new("service.store.two-stroke", -1381.3124f, 6.7729273f, 140.1289f, "760659f56c75280698d060ccc6a9fb4a"),
            new("service.store.suomi-dashboard-cover", -1381.5914f, 6.171699f, 140.98108f, "", FixtureRadius, "2238c9b8ea9d8b15c16afa3827474162"),
            new("service.store.suomi-seat-cover", -1381.8539f, 6.231999f, 141.05298f, "", FixtureRadius, "52da5d2e3aab4bd2e809fea7849291c4"),
            new("service.store.suomi-steering-wheel-cover", -1381.7806f, 6.123999f, 141.21777f, "", FixtureRadius, "fd4e463798b0f67a57d8929964ec7400"),
            new("service.store.spray01", -1380.5374f, 7.1362443f, 139.04443f, "56cdb23fcb57765c84ba2b530cf223ad", 0.045f),
            new("service.store.spray02", -1380.5912f, 7.1362443f, 139.12866f, "45a15030ff0d45822140f7d01ef4e34c", 0.045f),
            new("service.store.spray03", -1380.6451f, 7.1362443f, 139.21289f, "bba5083ff2ae34120eacb00fb0ed7cf6", 0.045f),
            new("service.store.spray04", -1380.699f, 7.136245f, 139.29712f, "2e287e01944bea88e6a0ffa5fb83d771", 0.045f),
            new("service.store.spray05", -1380.7528f, 7.136245f, 139.38147f, "1d03ec3a6b37494de09ccaa1cc119658", 0.045f),
            new("service.store.spray06", -1380.8068f, 7.136245f, 139.46558f, "4ea2956f64cbc6815f63a1a214e0c7bd", 0.045f),
            new("service.store.spray07", -1380.8607f, 7.136245f, 139.5498f, "3e63733ef3d45f5b73aefd44dfe35fe7", 0.045f),
            new("service.store.spray08", -1380.9146f, 7.136245f, 139.63416f, "1c04158ddf02bcd909c3c3c058a32da7", 0.045f),
            new("service.store.spray09", -1380.9685f, 7.1362453f, 139.71838f, "fa222f825fde44f6285c5175019f83e9", 0.045f),
            new("service.store.spray10", -1381.0223f, 7.1362453f, 139.80261f, "9382006ff478a61617ae47e30fd18682", 0.045f),
            new("service.store.spray11", -1381.0762f, 7.1362453f, 139.88684f, "1bdda23ec38dd78974c45282fdb5da9b", 0.045f),
            new("service.store.spray12", -1381.1301f, 7.1362453f, 139.97107f, "88276f527276a2503d4768d74bc03870", 0.045f),
            new("service.store.spray-matte01", -1381.184f, 7.1362453f, 140.0553f, "6f5c98f1b08688da57c2d4ea1f990722", 0.045f),
        };

        private static readonly OfferFixtureSeed[] PubOfferFixtures =
        {
            new("service.pub.beer", -1375.8091f, 6.4245496f, 145.48267f, 0.055f),
            new("service.pub.vodka-shot", -1375.797f, 6.349149f, 145.46765f, 0.055f),
            new("service.pub.sausage-and-fries", -1375.7788f, 6.2742496f, 145.44507f, 0.055f),
            new("service.pub.coffee", -1375.8229f, 6.5011497f, 145.49963f, 0.055f),
            new("service.pub.cigarettes", -1376.4518f, 6.3772497f, 145.0304f, 0.07f),
        };

        [SerializeField] private ServiceCatalog serviceCatalog;
        [SerializeField]
        private ExpandedShopShelfLayoutCatalog expandedShopShelfLayoutCatalog;

        private readonly List<string> materializedBindingIds = new();
        private readonly List<string> authoringBlockers = new();
        private Transform targetRoot;
        private bool initialized;

        public bool IsInitialized => initialized;
        public Transform TargetRoot => targetRoot;
        public int MaterializedTargetCount => materializedBindingIds.Count;
        public IReadOnlyList<string> MaterializedBindingIds =>
            materializedBindingIds;
        public IReadOnlyList<string> AuthoringBlockers => authoringBlockers;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(ServiceCatalog configuredCatalog)
        {
            serviceCatalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
        }
#endif

        public void Initialize(
            Transform compositionRoot,
            ServiceCatalog configuredCatalog,
            ServiceRuntime runtime,
            ServiceInteractionFeedbackHandler feedback = null,
            TeimoServicePresentationDirector teimoPresentation = null,
            Camera catalogCamera = null,
            IGameplayInputGate gameplayInputGate = null,
            ItemWorldRuntime configuredItemRuntime = null)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Production service interaction installer is already initialized.");
            }

            if (compositionRoot == null)
            {
                throw new ArgumentNullException(nameof(compositionRoot));
            }

            if (configuredCatalog == null)
            {
                throw new ArgumentNullException(nameof(configuredCatalog));
            }

            if (runtime == null || !runtime.IsInitialized)
            {
                throw new ArgumentException(
                    "Service runtime must be initialized before service fixtures are installed.",
                    nameof(runtime));
            }

            if (!configuredCatalog.TryValidate(out string catalogFailure))
            {
                throw new ArgumentException(
                    catalogFailure,
                    nameof(configuredCatalog));
            }

            if (!ReferenceEquals(runtime.Catalog, configuredCatalog))
            {
                throw new ArgumentException(
                    "Service fixture catalog must be the catalog bound to ServiceRuntime.",
                    nameof(configuredCatalog));
            }

            if (serviceCatalog != null &&
                !ReferenceEquals(serviceCatalog, configuredCatalog))
            {
                throw new InvalidOperationException(
                    "Authored service fixture catalog differs from the runtime catalog.");
            }

            serviceCatalog = configuredCatalog;
            ServiceInteractionFeedbackHandler routedFeedback = value =>
            {
                feedback?.Invoke(value);
                teimoPresentation?.HandleServiceFeedback(value);
            };

            ServiceLocationDefinition store = RequireLocation(
                StoreLocationId,
                ServiceLocationKind.Store);
            ServiceLocationDefinition pub = RequireLocation(
                PubLocationId,
                ServiceLocationKind.Pub);
            ServiceLocationDefinition gasoline = RequireLocation(
                GasolineLocationId,
                ServiceLocationKind.FuelStation);
            ServiceLocationDefinition diesel = RequireLocation(
                DieselLocationId,
                ServiceLocationKind.FuelStation);
            ServiceLocationDefinition fuelOil = RequireLocation(
                FuelOilLocationId,
                ServiceLocationKind.FuelStation);
            ServiceLocationDefinition workshop = RequireLocation(
                WorkshopLocationId,
                ServiceLocationKind.Workshop);
            ServiceLocationDefinition inspection = RequireLocation(
                InspectionLocationId,
                ServiceLocationKind.Inspection);

            ServiceOfferDefinition gasolineOffer = RequireFuelOffer(
                GasolineOfferId,
                gasoline.LocationId,
                FuelGrade.Gasoline98);
            ServiceOfferDefinition dieselOffer = RequireFuelOffer(
                DieselOfferId,
                diesel.LocationId,
                FuelGrade.Diesel);
            ServiceOfferDefinition fuelOilOffer = RequireFuelOffer(
                FuelOilOfferId,
                fuelOil.LocationId,
                FuelGrade.FuelOil);
            ServiceOfferDefinition inspectionOffer = RequireOffer(
                InspectionOfferId,
                inspection.LocationId,
                ServiceOfferKind.Inspection);

            var rootObject = new GameObject(
                "12A S1 Service Interaction Targets");
            targetRoot = rootObject.transform;
            targetRoot.SetParent(compositionRoot, false);

            CreateStoreOfferTargets(
                store,
                runtime,
                routedFeedback);
            CreateExpandedShopOfferTargets(
                store,
                runtime,
                routedFeedback);
            CreateCheckoutTarget(store, runtime, routedFeedback);
            CreatePubTargets(pub, runtime, routedFeedback);
            CreateFuelNozzle(
                gasoline,
                gasolineOffer,
                "4f8b8c39268e4f3eb398152a3aab25a1",
                runtime,
                routedFeedback);
            CreateFuelNozzle(
                diesel,
                dieselOffer,
                "2de41ca2e84143d29646d7a448639f11",
                runtime,
                routedFeedback);
            CreateFuelNozzle(
                fuelOil,
                fuelOilOffer,
                "6bc20f933af944cc9b16b103fed87704",
                runtime,
                routedFeedback);
            CreateWorkshopTarget(
                workshop,
                runtime,
                routedFeedback,
                catalogCamera,
                gameplayInputGate);
            CreateInspectionTarget(
                inspection,
                inspectionOffer,
                runtime,
                routedFeedback);
            CreateHomeCatalogTarget(
                runtime,
                configuredItemRuntime,
                routedFeedback,
                catalogCamera,
                gameplayInputGate);
            if (configuredItemRuntime != null)
            {
                CreateHomeMailOrderTargets(
                    runtime,
                    configuredItemRuntime,
                    routedFeedback);
            }

            BuildFailVisibleAuthoringReport();
            for (int index = 0; index < authoringBlockers.Count; index++)
            {
                Debug.LogWarning(authoringBlockers[index], this);
            }

            initialized = true;
        }

        private void CreateCheckoutTarget(
            ServiceLocationDefinition location,
            ServiceRuntime runtime,
            ServiceInteractionFeedbackHandler feedback)
        {
            GameObject targetObject = CreateTargetObject(
                "Teimo Store Checkout",
                location.WorldPosition);
            TeimoStoreCheckoutInteractionTarget target =
                targetObject.AddComponent<TeimoStoreCheckoutInteractionTarget>();
            target.ConfigureForAuthoring(
                location.LocationId,
                location.InteractionAnchorId);
            target.Bind(runtime, feedback);
            RecordValidatedBinding(target, location, string.Empty);
        }

        private void CreateStoreOfferTargets(
            ServiceLocationDefinition location,
            ServiceRuntime runtime,
            ServiceInteractionFeedbackHandler feedback)
        {
            for (int index = 0; index < StoreOfferFixtures.Length; index++)
            {
                OfferFixtureSeed seed = StoreOfferFixtures[index];
                ServiceOfferDefinition offer = RequireOffer(
                    seed.OfferId,
                    location.LocationId,
                    ServiceOfferKind.RetailItem);
                GameObject targetObject = CreateTargetObject(
                    "Teimo Store Shelf " + offer.DisplayName,
                    seed.WorldPosition,
                    seed.Radius);
                TeimoStoreOfferInteractionTarget target =
                    targetObject.AddComponent<TeimoStoreOfferInteractionTarget>();
                target.ConfigureForAuthoring(
                    location.LocationId,
                    location.InteractionAnchorId,
                    offer.OfferId);
                target.Bind(runtime, feedback);
                StoreShelfStockPresentation stockPresentation =
                    targetObject.AddComponent<StoreShelfStockPresentation>();
                SphereCollider fallbackCollider =
                    targetObject.GetComponent<SphereCollider>();
                stockPresentation.Configure(
                    runtime,
                    offer.OfferId,
                    seed.SourceVisualGroupStableId,
                    seed.SourceVisualUnitStableId,
                    fallbackCollider);
                RecordValidatedBinding(target, location, offer.OfferId);
            }
        }

        private void CreateExpandedShopOfferTargets(
            ServiceLocationDefinition location,
            ServiceRuntime runtime,
            ServiceInteractionFeedbackHandler feedback)
        {
            ExpandedShopShelfLayoutCatalog catalog =
                expandedShopShelfLayoutCatalog ??
                Resources.Load<ExpandedShopShelfLayoutCatalog>(
                    ExpandedShopShelfLayoutCatalog.ResourceName);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    "Expanded Shop exact shelf layout is missing. Rebuild " +
                    "Phase-1 item legacy presentation before entering play.");
            }

            if (!catalog.TryValidate(out string failure))
            {
                throw new InvalidOperationException(failure);
            }

            expandedShopShelfLayoutCatalog = catalog;
            ValidateExpandedShopStockCoverage(catalog, location);
            for (int index = 0; index < catalog.Groups.Count; index++)
            {
                ExpandedShopShelfGroupDefinition group =
                    catalog.Groups[index];
                ServiceOfferDefinition offer = RequireOffer(
                    group.OfferId,
                    location.LocationId,
                    ServiceOfferKind.RetailItem);
                GameObject targetObject = CreateTargetObject(
                    "Teimo Store Expanded Shelf " + group.GroupId,
                    catalog.GetGroupWorldPosition(group),
                    FixtureRadius);
                targetObject.transform.rotation =
                    catalog.GetGroupWorldRotation(group);
                SetWorldScale(
                    targetObject.transform,
                    catalog.GetGroupWorldScale(group));

                TeimoStoreOfferInteractionTarget target =
                    targetObject.AddComponent<
                        TeimoStoreOfferInteractionTarget>();
                target.ConfigureForAuthoring(
                    location.LocationId,
                    location.InteractionAnchorId,
                    offer.OfferId);
                target.Bind(runtime, feedback);

                var colliderObject = new GameObject("Interaction Collider");
                colliderObject.transform.SetParent(
                    targetObject.transform,
                    false);
                colliderObject.transform.localPosition =
                    group.ColliderLocalPosition;
                colliderObject.transform.localRotation =
                    group.ColliderLocalRotation;
                colliderObject.transform.localScale =
                    group.ColliderLocalScale;
                BoxCollider interactionCollider =
                    colliderObject.AddComponent<BoxCollider>();
                interactionCollider.center = group.ColliderCenter;
                interactionCollider.size = group.ColliderSize;
                interactionCollider.isTrigger = group.ColliderIsTrigger;

                SphereCollider pointFallback =
                    targetObject.GetComponent<SphereCollider>();
                pointFallback.enabled = false;

                IReadOnlyList<GameObject> stockUnits =
                    Array.Empty<GameObject>();
                Renderer[] outlineRenderers = Array.Empty<Renderer>();
                if (group.PresentationPrefab != null)
                {
                    GameObject presentationObject = Instantiate(
                        group.PresentationPrefab,
                        targetObject.transform,
                        false);
                    presentationObject.name = "Sanitized shelf presentation";
                    ExpandedShopShelfGroupPresentation presentation =
                        presentationObject.GetComponent<
                            ExpandedShopShelfGroupPresentation>();
                    if (presentation == null ||
                        !presentation.TryValidate(out failure) ||
                        presentation.StockUnits.Count !=
                        group.StockUnitCount)
                    {
                        throw new InvalidOperationException(
                            string.IsNullOrEmpty(failure)
                                ? $"Expanded Shop shelf group " +
                                  $"'{group.GroupId}' presentation mismatch."
                                : failure);
                    }

                    stockUnits = presentation.StockUnits;
                    outlineRenderers = presentationObject
                        .GetComponentsInChildren<Renderer>(true);
                }

                InteractionTargetHost host =
                    targetObject.GetComponent<InteractionTargetHost>();
                host.ConfigureOutlineRenderers(outlineRenderers);
                host.ConfigureSelectionPriority(20);

                StoreShelfStockPresentation stockPresentation =
                    targetObject.AddComponent<
                        StoreShelfStockPresentation>();
                stockPresentation.ConfigureExactProjectOwnedLayout(
                    runtime,
                    offer.OfferId,
                    stockUnits,
                    interactionCollider,
                    group.StockIndexOffset);
                RecordValidatedBinding(target, location, offer.OfferId);
            }
        }

        private void ValidateExpandedShopStockCoverage(
            ExpandedShopShelfLayoutCatalog catalog,
            ServiceLocationDefinition location)
        {
            var stockIndices = new Dictionary<string, HashSet<int>>(
                StringComparer.Ordinal);
            for (int groupIndex = 0;
                 groupIndex < catalog.Groups.Count;
                 groupIndex++)
            {
                ExpandedShopShelfGroupDefinition group =
                    catalog.Groups[groupIndex];
                ServiceOfferDefinition offer = RequireOffer(
                    group.OfferId,
                    location.LocationId,
                    ServiceOfferKind.RetailItem);
                if (!stockIndices.TryGetValue(
                        offer.OfferId,
                        out HashSet<int> indices))
                {
                    indices = new HashSet<int>();
                    stockIndices.Add(offer.OfferId, indices);
                }

                for (int unitIndex = 0;
                     unitIndex < group.StockUnitCount;
                     unitIndex++)
                {
                    indices.Add(group.StockIndexOffset + unitIndex);
                }
            }

            if (stockIndices.Count != ExpectedExpandedShopOfferCount)
            {
                throw new InvalidOperationException(
                    $"Expanded Shop shelf layout must bind exactly " +
                    $"{ExpectedExpandedShopOfferCount} " +
                    "project-owned retail offers.");
            }

            foreach (KeyValuePair<string, HashSet<int>> entry in stockIndices)
            {
                ServiceOfferDefinition offer = RequireOffer(
                    entry.Key,
                    location.LocationId,
                    ServiceOfferKind.RetailItem);
                if (string.Equals(
                        offer.OfferId,
                        "service.store.expanded-shop-sausage",
                        StringComparison.Ordinal))
                {
                    if (entry.Value.Count != 0)
                    {
                        throw new InvalidOperationException(
                            "Expanded Shop sausage shelf evidence must not " +
                            "invent stock visuals absent from the mod.");
                    }

                    continue;
                }

                if (entry.Value.Count != offer.StockCapacity)
                {
                    throw new InvalidOperationException(
                        $"Expanded Shop shelf offer '{offer.OfferId}' maps " +
                        $"{entry.Value.Count} visual units but its project " +
                        $"stock capacity is {offer.StockCapacity}.");
                }

                for (int index = 0; index < offer.StockCapacity; index++)
                {
                    if (!entry.Value.Contains(index))
                    {
                        throw new InvalidOperationException(
                            $"Expanded Shop shelf offer '{offer.OfferId}' " +
                            $"does not cover stock index {index}.");
                    }
                }
            }
        }

        private static void SetWorldScale(
            Transform target,
            Vector3 worldScale)
        {
            Vector3 parentScale = target.parent == null
                ? Vector3.one
                : target.parent.lossyScale;
            target.localScale = new Vector3(
                worldScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                worldScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                worldScale.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        }

        private void CreatePubTargets(
            ServiceLocationDefinition location,
            ServiceRuntime runtime,
            ServiceInteractionFeedbackHandler feedback)
        {
            for (int index = 0; index < PubOfferFixtures.Length; index++)
            {
                OfferFixtureSeed seed = PubOfferFixtures[index];
                ServiceOfferDefinition offer = RequireOffer(
                    seed.OfferId,
                    location.LocationId,
                    ServiceOfferKind.PubItem);
                GameObject targetObject = CreateTargetObject(
                    "Teimo Pub Order " + offer.DisplayName,
                    seed.WorldPosition,
                    seed.Radius);
                TeimoPubOfferInteractionTarget target =
                    targetObject.AddComponent<TeimoPubOfferInteractionTarget>();
                target.ConfigureForAuthoring(
                    location.LocationId,
                    location.InteractionAnchorId,
                    offer.OfferId);
                target.Bind(runtime, feedback);
                RecordValidatedBinding(target, location, offer.OfferId);
            }
        }

        private void CreateFuelNozzle(
            ServiceLocationDefinition location,
            ServiceOfferDefinition offer,
            string stableId,
            ServiceRuntime runtime,
            ServiceInteractionFeedbackHandler feedback)
        {
            GameObject targetObject = CreateTargetObject(
                offer.DisplayName + " Fuel Nozzle",
                location.WorldPosition,
                0.10f);
            Rigidbody body = targetObject.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            targetObject.AddComponent<MSC.Core.Identity.StableEntityIdAuthoring>();
            ServiceFuelNozzle target =
                targetObject.AddComponent<ServiceFuelNozzle>();
            target.ConfigureForAuthoring(
                location.LocationId,
                location.InteractionAnchorId,
                offer.OfferId,
                stableId,
                location.WorldPosition,
                Quaternion.identity);
            target.Bind(runtime, feedback);
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Project-owned Fuel Nozzle Visual";
            visual.transform.SetParent(targetObject.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            visual.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            visual.transform.localScale = new Vector3(0.09f, 0.22f, 0.07f);
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(visualCollider);
                }
                else
                {
                    DestroyImmediate(visualCollider);
                }
            }
            RecordValidatedBinding(target, location, offer.OfferId);
        }

        private void CreateWorkshopTarget(
            ServiceLocationDefinition location,
            ServiceRuntime runtime,
            ServiceInteractionFeedbackHandler feedback,
            Camera catalogCamera,
            IGameplayInputGate gameplayInputGate)
        {
            GameObject targetObject = CreateTargetObject(
                "Fleetari Service Brochure",
                FleetariBrochurePosition,
                0.22f);
            FleetariWorkshopCatalogInteractionTarget target =
                targetObject.AddComponent<
                    FleetariWorkshopCatalogInteractionTarget>();
            target.ConfigureForAuthoring(
                location.LocationId,
                location.InteractionAnchorId);
            target.Bind(runtime, feedback);
            GameObject closedVisual = null;
            LegacyServiceCatalogPresentationProvider provider =
                LegacyServiceCatalogPresentationProvider.Current;
            provider?.TryCreate(
                LegacyServiceCatalogKind.FleetariServices,
                targetObject.transform,
                out closedVisual);
            PhysicalServiceCatalogController book = targetObject.AddComponent<
                PhysicalServiceCatalogController>();
            book.Configure(
                LegacyServiceCatalogKind.FleetariServices,
                target,
                catalogCamera,
                gameplayInputGate,
                closedVisual,
                provider?.FleetariServicePages,
                provider?.FleetariOrderPage);
            target.BindBookPresenter(book);
            RecordValidatedBinding(target, location, string.Empty);
        }

        private void CreateInspectionTarget(
            ServiceLocationDefinition location,
            ServiceOfferDefinition offer,
            ServiceRuntime runtime,
            ServiceInteractionFeedbackHandler feedback)
        {
            GameObject targetObject = CreateTargetObject(
                "Vehicle Inspection Desk (Unavailable Shell)",
                location.WorldPosition);
            VehicleInspectionInteractionTarget target =
                targetObject.AddComponent<VehicleInspectionInteractionTarget>();
            target.ConfigureForAuthoring(
                location.LocationId,
                location.InteractionAnchorId,
                offer.OfferId);
            target.Bind(runtime, feedback);
            RecordValidatedBinding(target, location, offer.OfferId);
        }

        private void CreateHomeCatalogTarget(
            ServiceRuntime runtime,
            ItemWorldRuntime itemRuntime,
            ServiceInteractionFeedbackHandler feedback,
            Camera catalogCamera,
            IGameplayInputGate gameplayInputGate)
        {
            GameObject targetObject = CreateTargetObject(
                "Home Parts Catalog",
                HomePartsCatalogPosition,
                0.22f);
            HomePartsCatalogInteractionTarget target =
                targetObject.AddComponent<HomePartsCatalogInteractionTarget>();
            target.ConfigureForAuthoring(
                HomePartsMailOrderCatalog.Offers,
                message => feedback?.Invoke(
                    new ServiceInteractionFeedback(
                        ServiceInteractionKind.WorkshopSelection,
                        "service.location.home-parts-catalog",
                        target.CurrentEntryId,
                        new ServiceResult(true, ServiceFailureReason.None),
                        message,
                        target.transform.position,
                        ServiceInteractionFeedbackCue.Confirm)));
            GameObject fallbackVisual = null;
            LegacyServiceCatalogPresentationProvider provider =
                LegacyServiceCatalogPresentationProvider.Current;
            provider?.TryCreate(
                LegacyServiceCatalogKind.HomeParts,
                targetObject.transform,
                out fallbackVisual);
            HomePartsCatalogPhysicalBinding physicalBinding =
                targetObject.AddComponent<HomePartsCatalogPhysicalBinding>();
            physicalBinding.Configure(
                HomePartsMagazineStableId,
                fallbackVisual,
                target);
            if (itemRuntime != null)
            {
                target.BindMailOrder(
                    runtime,
                    plan => TryMaterializeHomeMailOrderEnvelope(
                        itemRuntime,
                        physicalBinding,
                        plan));
            }
            PhysicalServiceCatalogController book = targetObject.AddComponent<
                PhysicalServiceCatalogController>();
            book.Configure(
                LegacyServiceCatalogKind.HomeParts,
                target,
                catalogCamera,
                gameplayInputGate,
                fallbackVisual,
                provider?.HomePartsPages,
                provider?.HomePartsOrderPage,
                physicalBinding);
            target.BindBookPresenter(book);
            materializedBindingIds.Add(
                "service.location.home-parts-catalog|anchor.home.parts-catalog");
        }

        private void CreateHomeMailOrderTargets(
            ServiceRuntime runtime,
            ItemWorldRuntime itemRuntime,
            ServiceInteractionFeedbackHandler feedback)
        {
            Action<string> relay = message => feedback?.Invoke(
                new ServiceInteractionFeedback(
                    ServiceInteractionKind.StoreCheckout,
                    StoreLocationId,
                    "home.mail-order",
                    new ServiceResult(true, ServiceFailureReason.None),
                    message,
                    MailOrderPaymentPosition,
                    ServiceInteractionFeedbackCue.Confirm));

            GameObject boxObject = CreateTargetObject(
                "Teimo Mail Order Box",
                MailOrderBoxPosition,
                0.22f);
            HomeMailOrderBoxInteractionTarget box =
                boxObject.AddComponent<HomeMailOrderBoxInteractionTarget>();
            box.Bind(runtime, itemRuntime, relay);
            materializedBindingIds.Add(
                "service.location.teimo-mailbox|anchor.teimo.mail-order-box");

            GameObject paymentObject = CreateTargetObject(
                "Teimo Post Order Payment",
                MailOrderPaymentPosition,
                0.15f);
            HomeMailOrderPaymentInteractionTarget payment = paymentObject
                .AddComponent<HomeMailOrderPaymentInteractionTarget>();
            payment.Bind(
                runtime,
                plan => TryMaterializeHomeMailOrderDelivery(itemRuntime, plan),
                relay);
            materializedBindingIds.Add(
                "service.location.teimo-store|anchor.teimo.post-order-payment");
        }

        private bool TryMaterializeHomeMailOrderEnvelope(
            ItemWorldRuntime itemRuntime,
            HomePartsCatalogPhysicalBinding physicalBinding,
            HomeMailOrderEnvelopePlan plan)
        {
            if (itemRuntime == null || !itemRuntime.IsInitialized ||
                !StableEntityId.TryParse(
                    plan.EnvelopeStableEntityId,
                    out StableEntityId stableId) ||
                !itemRuntime.Definitions.TryGet(
                    HomePartsMailOrderCatalog.EnvelopeDefinitionId,
                    out _))
            {
                return false;
            }

            Transform catalogTransform = physicalBinding != null
                ? physicalBinding.PhysicalCatalogTransform
                : null;
            Vector3 position = (catalogTransform != null
                    ? catalogTransform.position
                    : HomePartsCatalogPosition) +
                Vector3.up * 0.14f +
                (catalogTransform != null
                    ? catalogTransform.right
                    : Vector3.right) * 0.28f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, 0f);
            itemRuntime.SpawnDynamic(
                HomePartsMailOrderCatalog.EnvelopeDefinitionId,
                stableId,
                position,
                rotation,
                gameObject.scene);
            return true;
        }

        private bool TryMaterializeHomeMailOrderDelivery(
            ItemWorldRuntime itemRuntime,
            HomeMailOrderDeliveryPlan plan)
        {
            if (itemRuntime == null || !itemRuntime.IsInitialized ||
                plan.Offers.Count == 0)
            {
                return false;
            }

            for (int index = 0; index < plan.Offers.Count; index++)
            {
                if (!itemRuntime.Definitions.TryGet(
                        plan.Offers[index].ItemDefinitionId,
                        out _))
                {
                    return false;
                }
            }

            Scene scene = gameObject.scene;
            for (int index = 0; index < plan.Offers.Count; index++)
            {
                HomePartsMailOrderOffer offer = plan.Offers[index];
                StableEntityId stableId = ItemStableIdUtility.CreateDeterministic(
                    $"{plan.OrderId}|delivered|{index}|{offer.ItemDefinitionId}");
                if (itemRuntime.TryGetInstance(stableId.Value, out _))
                {
                    continue;
                }

                int row = index / 6;
                int column = index % 6;
                Vector3 position = MailOrderDeliveryOrigin +
                    new Vector3(column * 0.5f, 0f, row * 0.5f);
                itemRuntime.SpawnDynamic(
                    offer.ItemDefinitionId,
                    stableId,
                    position,
                    Quaternion.identity,
                    scene);
            }

            return true;
        }

        private GameObject CreateTargetObject(
            string displayName,
            Vector3 worldPosition,
            float radius = FixtureRadius)
        {
            var targetObject = new GameObject("12A S1 " + displayName);
            targetObject.transform.SetParent(targetRoot, false);
            targetObject.transform.position = worldPosition;

            SphereCollider collider =
                targetObject.AddComponent<SphereCollider>();
            collider.radius = radius;
            collider.isTrigger = false;
            targetObject.AddComponent<InteractionTargetHost>();
            return targetObject;
        }

        private void RecordValidatedBinding(
            ServiceInteractionTargetBase target,
            ServiceLocationDefinition location,
            string offerId)
        {
            if (!target.TryValidateBinding(out string failure))
            {
                throw new InvalidOperationException(
                    $"Service fixture '{location.LocationId}' failed binding: {failure}");
            }

            materializedBindingIds.Add(
                string.IsNullOrEmpty(offerId)
                    ? $"{location.LocationId}|{location.InteractionAnchorId}"
                    : $"{location.LocationId}|{location.InteractionAnchorId}|{offerId}");
        }

        private ServiceLocationDefinition RequireLocation(
            string locationId,
            ServiceLocationKind expectedKind)
        {
            if (!serviceCatalog.TryGetLocation(
                    locationId,
                    out ServiceLocationDefinition location) ||
                location.Kind != expectedKind)
            {
                throw new InvalidOperationException(
                    $"Required service location '{locationId}' is missing or has the wrong kind.");
            }

            return location;
        }

        private ServiceOfferDefinition RequireOffer(
            string offerId,
            string expectedLocationId,
            ServiceOfferKind expectedKind)
        {
            if (!serviceCatalog.TryGetOffer(
                    offerId,
                    out ServiceOfferDefinition offer) ||
                offer.Kind != expectedKind ||
                !string.Equals(
                    offer.LocationId,
                    expectedLocationId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Required service offer '{offerId}' is missing or bound to the wrong location.");
            }

            return offer;
        }

        private ServiceOfferDefinition RequireFuelOffer(
            string offerId,
            string expectedLocationId,
            FuelGrade expectedGrade)
        {
            ServiceOfferDefinition offer = RequireOffer(
                offerId,
                expectedLocationId,
                ServiceOfferKind.Fuel);
            if (offer.FuelGrade != expectedGrade)
            {
                throw new InvalidOperationException(
                    $"Fuel offer '{offerId}' has grade '{offer.FuelGrade}', expected '{expectedGrade}'.");
            }

            return offer;
        }

        private void BuildFailVisibleAuthoringReport()
        {
            int storeOffers = 0;
            int pubOffers = 0;
            int workshopOffers = 0;
            for (int index = 0; index < serviceCatalog.Offers.Count; index++)
            {
                ServiceOfferDefinition offer = serviceCatalog.Offers[index];
                if (offer.Kind == ServiceOfferKind.RetailItem &&
                    string.Equals(
                        offer.LocationId,
                        StoreLocationId,
                        StringComparison.Ordinal))
                {
                    storeOffers++;
                }
                else if (offer.Kind == ServiceOfferKind.PubItem &&
                         string.Equals(
                             offer.LocationId,
                             PubLocationId,
                             StringComparison.Ordinal))
                {
                    pubOffers++;
                }
                else if (offer.Kind == ServiceOfferKind.Workshop &&
                         string.Equals(
                             offer.LocationId,
                             WorkshopLocationId,
                             StringComparison.Ordinal))
                {
                    workshopOffers++;
                }
            }

            var expandedOfferIds = new HashSet<string>(
                StringComparer.Ordinal);
            if (expandedShopShelfLayoutCatalog != null)
            {
                for (int index = 0;
                     index < expandedShopShelfLayoutCatalog.Groups.Count;
                     index++)
                {
                    expandedOfferIds.Add(
                        expandedShopShelfLayoutCatalog.Groups[index].OfferId);
                }
            }

            int authoredLogicalStoreOffers = StoreOfferFixtures.Length +
                expandedOfferIds.Count;
            int authoredPhysicalStoreGroups = StoreOfferFixtures.Length +
                (expandedShopShelfLayoutCatalog?.Groups.Count ?? 0);
            int expectedPhysicalStoreGroups = StoreOfferFixtures.Length +
                ExpectedExpandedShopPhysicalGroupCount;
            if (storeOffers != ExpectedStoreShelfOfferCount ||
                authoredLogicalStoreOffers != ExpectedStoreShelfOfferCount ||
                expandedOfferIds.Count != ExpectedExpandedShopOfferCount ||
                expandedShopShelfLayoutCatalog?.Groups.Count !=
                ExpectedExpandedShopPhysicalGroupCount)
            {
                authoringBlockers.Add(
                    "SERVICE-WORLD-BLOCKER shelf-offers: " +
                    $"catalog has {storeOffers} logical offers; authoring " +
                    $"has {authoredLogicalStoreOffers} logical offers across " +
                    $"{authoredPhysicalStoreGroups} physical groups. Expected " +
                    $"{ExpectedStoreShelfOfferCount} logical offers and " +
                    $"{expectedPhysicalStoreGroups} exact groups.");
            }

            if (pubOffers != PubOfferFixtures.Length)
            {
                authoringBlockers.Add(
                    "SERVICE-WORLD-BLOCKER pub-menu: " +
                    $"catalog has {pubOffers} offers and authoring has " +
                    $"{PubOfferFixtures.Length} exact order-list bindings.");
            }

            authoringBlockers.Add(
                "SERVICE-WORLD-BLOCKER fleetari-order: " +
                $"all {workshopOffers} brochure offers are browsable and their selection " +
                "is save-backed, but a paid order remains disabled until a project-owned " +
                "service vehicle and outcome backend exist.");
            authoringBlockers.Add(
                "SERVICE-WORLD-BLOCKER inspection-backend: the exact inspection order fixture " +
                "is wired, but no project-owned inspected vehicle or assessment backend exists. " +
                "The target intentionally remains unavailable.");
        }

        private readonly struct OfferFixtureSeed
        {
            public OfferFixtureSeed(
                string offerId,
                float x,
                float y,
                float z,
                float radius)
                : this(
                    offerId,
                    x,
                    y,
                    z,
                    string.Empty,
                    radius,
                    string.Empty)
            {
            }

            public OfferFixtureSeed(
                string offerId,
                float x,
                float y,
                float z,
                string sourceVisualGroupStableId = "",
                float radius = FixtureRadius,
                string sourceVisualUnitStableId = "")
            {
                OfferId = offerId;
                WorldPosition = new Vector3(x, y, z);
                Radius = radius;
                SourceVisualGroupStableId =
                    sourceVisualGroupStableId ?? string.Empty;
                SourceVisualUnitStableId =
                    sourceVisualUnitStableId ?? string.Empty;
            }

            public string OfferId { get; }
            public Vector3 WorldPosition { get; }
            public float Radius { get; }
            public string SourceVisualGroupStableId { get; }
            public string SourceVisualUnitStableId { get; }
        }
    }
}
