using System;
using System.Linq;
using MSC.Core.Identity;
using MSC.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Items.Tests.EditMode
{
    public sealed class ItemCatalogAndStateTests
    {
        [Test]
        public void SpannerSetIsAnOpenableCaseWithExactDonorWrenchSizes()
        {
            ItemDefinitionRecord spanner = ByFeature("P1.ITEM.138");

            Assert.That(spanner.ToolType, Is.Empty);
            Assert.That(
                spanner.ToolVariants,
                Is.EqualTo(new[]
                {
                    "5", "6", "7", "8", "9", "10", "11",
                    "12", "13", "14", "15",
                }));
            Assert.That(
                spanner.PrimaryAction,
                Is.EqualTo(ItemPrimaryAction.ToggleOpen));
            Assert.That(spanner.CanOpen, Is.True);
            Assert.That(spanner.StartsOpen, Is.False);
        }

        private const string DefinitionCatalogPath =
            "Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset";
        private const string PlacementCatalogPath =
            "Assets/Game/Items/Content/Placements/Phase1ItemPlacementCatalog.asset";

        private ItemDefinitionCatalog definitions;
        private ItemPlacementCatalog placements;

        [SetUp]
        public void SetUp()
        {
            definitions = AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                DefinitionCatalogPath);
            placements = AssetDatabase.LoadAssetAtPath<ItemPlacementCatalog>(
                PlacementCatalogPath);
            Assert.That(definitions, Is.Not.Null, DefinitionCatalogPath);
            Assert.That(placements, Is.Not.Null, PlacementCatalogPath);
        }

        [Test]
        public void LockedCatalogsCloseRequiredRosterAndCanonicalPlacements()
        {
            Assert.That(
                definitions.ValidateConfiguration(),
                Is.Empty,
                string.Join("\n", definitions.ValidateConfiguration()));
            Assert.That(
                placements.ValidateConfiguration(definitions),
                Is.Empty,
                string.Join("\n", placements.ValidateConfiguration(definitions)));
            Assert.That(definitions.SchemaVersion,
                Is.EqualTo(ItemDefinitionCatalog.CurrentSchemaVersion));
            Assert.That(definitions.CatalogId, Is.EqualTo("phase1.items.09b.v1"));
            Assert.That(definitions.Definitions.Count, Is.EqualTo(152));
            Assert.That(
                definitions.Definitions.Count(definition => definition.Required),
                Is.EqualTo(126));
            Assert.That(
                definitions.TryGet(
                    HomePartsMailOrderCatalog.EnvelopeDefinitionId,
                    out ItemDefinitionRecord envelope),
                Is.True);
            Assert.That(envelope.Category, Is.EqualTo("MailOrderEnvelope"));
            Assert.That(
                HomePartsMailOrderCatalog.Offers.All(offer =>
                    definitions.TryGet(offer.ItemDefinitionId, out _)),
                Is.True);
            Assert.That(placements.Placements.Count, Is.EqualTo(43));
            Assert.That(
                placements.DonorRevision,
                Is.EqualTo("msc-world-baseline-04a1.1-c3f2f337"));
            Assert.That(
                placements.Placements.Select(value => value.StableEntityId).Distinct().Count(),
                Is.EqualTo(43));
        }

        [Test]
        public void CanonicalPlacementsLockDonorTransformParentAndPhysicsMetadata()
        {
            Assert.That(
                placements.Placements.Count(value => value.DonorHasRigidbody),
                Is.EqualTo(42));
            Assert.That(
                placements.Placements.Count(value => value.InitialIsKinematic),
                Is.EqualTo(6));
            Assert.That(
                placements.Placements.Count(value => value.DonorLayer == 19),
                Is.EqualTo(40));
            Assert.That(
                placements.Placements.Count(value => value.DonorLayer == 15),
                Is.EqualTo(2));
            Assert.That(
                placements.Placements.Count(value => value.DonorLayer == 0),
                Is.EqualTo(1));

            foreach (ItemPlacementRecord placement in placements.Placements)
            {
                Assert.That(placement.InitialLocalScale, Is.EqualTo(Vector3.one));
                Assert.That(
                    placement.DonorParentStableId,
                    Is.EqualTo("9c88c287a45c52e91f6ac6b345b20e10"));
                Assert.That(placement.InitialUseGravity, Is.True);
                Assert.That(placement.InitialDetectCollisions, Is.True);
                Assert.That(
                    float.IsFinite(placement.WorldPosition.x) &&
                    float.IsFinite(placement.WorldPosition.y) &&
                    float.IsFinite(placement.WorldPosition.z),
                    Is.True,
                    placement.PlacementId);
                Assert.That(
                    Quaternion.Angle(
                        placement.WorldRotation,
                        placement.WorldRotation.normalized),
                    Is.LessThan(0.001f),
                    placement.PlacementId);
            }
        }

        [Test]
        public void EvidenceBackedConsumablesPackagesFluidsAndToolsKeepLockedValues()
        {
            ItemDefinitionRecord sausages = ByFeature("P1.ITEM.101");
            Assert.That(sausages.ContentMeasure, Is.EqualTo(ItemContentMeasure.Units));
            Assert.That(sausages.MaximumContent, Is.EqualTo(4f));
            Assert.That(sausages.InitialContent, Is.EqualTo(4f));
            Assert.That(sausages.UseAmount, Is.EqualTo(1f));
            Assert.That(sausages.InitialChildCount, Is.EqualTo(4));
            Assert.That(sausages.ChildDefinitionId, Is.EqualTo("item.loose-sausage"));
            Assert.That(
                sausages.PrimaryAction,
                Is.EqualTo(ItemPrimaryAction.DispenseChild));
            Assert.That(sausages.Food.Edible, Is.False);
            Assert.That(sausages.Food.Perishable, Is.True);
            Assert.That(sausages.HungerEffect, Is.EqualTo(-100f));
            Assert.That(sausages.ThirstEffect, Is.EqualTo(12f));
            AssertScalar(sausages, "spoil-rate", 0.034f);
            AssertScalar(sausages, "fridge-rate", 0.0005f);

            ItemDefinitionRecord macaroni = ByFeature("P1.ITEM.102");
            Assert.That(macaroni.Food.Edible, Is.True);
            Assert.That(macaroni.Food.ConsumeWhole, Is.True);
            Assert.That(macaroni.Food.Cookable, Is.False);

            ItemDefinitionRecord looseSausage = ByFeature("P1.ITEM.116");
            Assert.That(looseSausage.Food.Edible, Is.True);
            Assert.That(looseSausage.Food.Perishable, Is.True);
            Assert.That(looseSausage.Food.Cookable, Is.True);
            Assert.That(looseSausage.Food.CookedAfterSeconds, Is.EqualTo(30f));
            Assert.That(
                looseSausage.Food.BurnedAfterAdditionalSeconds,
                Is.EqualTo(10f));

            ItemDefinitionRecord milk = ByFeature("P1.ITEM.105");
            Assert.That(
                milk.Food.ConsumptionPresentation,
                Is.EqualTo(ItemConsumptionPresentation.Drink));

            ItemDefinitionRecord juiceConcentrate = ByFeature("P1.ITEM.108");
            Assert.That(juiceConcentrate.Food.Edible, Is.True);
            Assert.That(juiceConcentrate.Food.ConsumeWhole, Is.True);
            Assert.That(
                juiceConcentrate.Food.ConsumptionPresentation,
                Is.EqualTo(ItemConsumptionPresentation.Drink));

            ItemDefinitionRecord beerCase = ByFeature("P1.ITEM.113");
            Assert.That(beerCase.DisplayName, Is.EqualTo("Ящик пива"));
            Assert.That(beerCase.InitialChildCount, Is.EqualTo(24));
            Assert.That(beerCase.InitialContent, Is.EqualTo(24f));
            Assert.That(beerCase.ChildDefinitionId, Is.EqualTo("item.beer-bottle"));
            Assert.That(
                beerCase.PrimaryAction,
                Is.EqualTo(ItemPrimaryAction.DispenseChild));
            Assert.That(
                ByFeature("P1.ITEM.114").DisplayName,
                Is.EqualTo("Бутылка пива"));

            AssertPackage("P1.ITEM.126", 4, "item.spark-plug");
            AssertPackage("P1.ITEM.127", 1, "item.light-bulb");
            AssertPackage("P1.ITEM.128", 5, "item.fuse-unit");
            AssertPackage("P1.ITEM.129", 4, "item.r20-battery-unit");

            ItemDefinitionRecord gasoline = ByFeature("P1.ITEM.133");
            Assert.That(gasoline.MaximumContent, Is.EqualTo(20f));
            Assert.That(gasoline.InitialContent, Is.EqualTo(2f));
            Assert.That(gasoline.InitialLiquidId, Is.EqualTo("liquid.gasoline"));
            Assert.That(gasoline.SupportsLiquidTransfer, Is.True);
            Assert.That(gasoline.CriticalRecovery, Is.True);

            ItemDefinitionRecord diesel = ByFeature("P1.ITEM.134");
            Assert.That(diesel.MaximumContent, Is.EqualTo(20f));
            Assert.That(diesel.InitialContent, Is.EqualTo(4f));
            Assert.That(diesel.InitialLiquidId, Is.EqualTo("liquid.diesel"));

            ItemDefinitionRecord spanner = ByFeature("P1.ITEM.138");
            Assert.That(spanner.ToolType, Is.Empty);
            Assert.That(
                spanner.ToolVariants,
                Is.EqualTo(new[]
                {
                    "5", "6", "7", "8", "9", "10", "11",
                    "12", "13", "14", "15",
                }));
            Assert.That(
                spanner.PrimaryAction,
                Is.EqualTo(ItemPrimaryAction.ToggleOpen));
            Assert.That(spanner.CanOpen, Is.True);
            Assert.That(spanner.StartsOpen, Is.False);

            ItemDefinitionRecord garbageBarrel = ByFeature("P1.ITEM.148");
            Assert.That(garbageBarrel.MassKilograms, Is.EqualTo(30f));
            Assert.That(
                garbageBarrel.PrimaryAction,
                Is.EqualTo(ItemPrimaryAction.Ignite));
            Assert.That(garbageBarrel.CanOpen, Is.False);
            Assert.That(
                garbageBarrel.ProxySize,
                Is.EqualTo(new Vector3(0.592f, 0.592f, 0.876f)));

            foreach (string featureId in new[] { "P1.ITEM.145", "P1.ITEM.146" })
            {
                ItemDefinitionRecord saunaContainer = ByFeature(featureId);
                Assert.That(saunaContainer.SupportsLiquidTransfer, Is.True);
                Assert.That(saunaContainer.CanOpen, Is.False);
                Assert.That(
                    saunaContainer.PrimaryAction,
                    Is.EqualTo(ItemPrimaryAction.None));
            }
        }

        [Test]
        public void ExpandedShopItems_AreSeparateDataDrivenPhysicalDefinitions()
        {
            string[] expectedDefinitionIds =
            {
                "item.buttermilk",
                "item.orange-juice",
                "item.bug-spray",
                "item.laundry-detergent",
                "item.sponge",
                "item.shampoo",
                "item.hand-soap",
                "item.dish-soap",
                "item.soap",
                "item.wheat-flour",
                "item.rye-flour",
                "item.mustard",
                "item.ketchup",
                "item.meat-soup",
                "item.pea-soup",
                "item.canned-meatballs",
                "item.fishstick-box",
                "item.can-opener",
                "item.fishstick",
            };

            Assert.That(
                definitions.Definitions.Count(value =>
                    value.FeatureId.StartsWith(
                        "EXT.EXPANDED_SHOP.ITEM.",
                        StringComparison.Ordinal)),
                Is.EqualTo(expectedDefinitionIds.Length));
            foreach (string definitionId in expectedDefinitionIds)
            {
                Assert.That(
                    definitions.TryGet(
                        definitionId,
                        out ItemDefinitionRecord definition),
                    Is.True,
                    definitionId);
                Assert.That(definition.ProxySize.x, Is.GreaterThan(0f));
                Assert.That(definition.MassKilograms, Is.GreaterThan(0f));
            }

            Assert.That(definitions.TryGet(
                "item.buttermilk",
                out ItemDefinitionRecord buttermilk), Is.True);
            Assert.That(buttermilk.Food.Edible, Is.True);
            Assert.That(buttermilk.Food.Perishable, Is.True);
            Assert.That(
                buttermilk.Food.AmbientDecayPerGameMinute,
                Is.EqualTo(0.04f));
            Assert.That(
                buttermilk.Food.RefrigeratedDecayPerGameMinute,
                Is.EqualTo(0.0009f));
            foreach (string drinkDefinitionId in new[]
                     {
                         "item.buttermilk",
                         "item.orange-juice",
                         "item.mustard",
                         "item.ketchup",
                     })
            {
                Assert.That(
                    definitions.TryGet(
                        drinkDefinitionId,
                        out ItemDefinitionRecord drink),
                    Is.True,
                    drinkDefinitionId);
                Assert.That(
                    drink.Food.ConsumptionPresentation,
                    Is.EqualTo(ItemConsumptionPresentation.Drink),
                    drinkDefinitionId);
            }

            Assert.That(definitions.TryGet(
                "item.fishstick-box",
                out ItemDefinitionRecord fishstickBox), Is.True);
            Assert.That(fishstickBox.InitialChildCount, Is.EqualTo(10));
            Assert.That(fishstickBox.ChildDefinitionId, Is.EqualTo(
                "item.fishstick"));
            Assert.That(fishstickBox.PrimaryAction, Is.EqualTo(
                ItemPrimaryAction.DispenseChild));

            Assert.That(definitions.TryGet(
                "item.fishstick",
                out ItemDefinitionRecord fishstick), Is.True);
            Assert.That(fishstick.Food.Cookable, Is.True);
            Assert.That(fishstick.Food.CookedAfterSeconds, Is.EqualTo(40f));
            Assert.That(
                fishstick.Food.BurnedAfterAdditionalSeconds,
                Is.EqualTo(10f));

            Assert.That(definitions.TryGet(
                "item.can-opener",
                out ItemDefinitionRecord canOpener), Is.True);
            Assert.That(canOpener.ToolType, Is.EqualTo("CanOpener"));
        }

        [Test]
        public void PortableGrillUsesDonorFuelAndCombustionConfiguration()
        {
            ItemDefinitionRecord charcoal = ByFeature("P1.ITEM.110");
            Assert.That(charcoal.DefinitionId, Is.EqualTo("item.charcoal"));
            Assert.That(charcoal.PrimaryAction, Is.EqualTo(ItemPrimaryAction.None));
            Assert.That(charcoal.MaximumContent, Is.EqualTo(140f));
            Assert.That(charcoal.InitialContent, Is.EqualTo(140f));
            Assert.That(charcoal.UseAmount, Is.EqualTo(12f));
            Assert.That(charcoal.MassKilograms, Is.EqualTo(1.8f));

            ItemDefinitionRecord grill = ByFeature("P1.ITEM.149");
            Assert.That(
                grill.PrimaryAction,
                Is.EqualTo(ItemPrimaryAction.IgniteFuel));
            Assert.That(grill.CanOpen, Is.True);
            Assert.That(grill.StartsOpen, Is.False);
            Assert.That(grill.MaximumContent, Is.EqualTo(100f));
            Assert.That(grill.InitialContent, Is.Zero);
            Assert.That(grill.HeatSource.ProvidesCookingHeat, Is.True);
            Assert.That(grill.HeatSource.RequiresItemEnabled, Is.True);
            Assert.That(
                grill.HeatSource.LocalCenter,
                Is.EqualTo(new Vector3(-0.017f, 0f, 0.111f)));
            Assert.That(
                grill.HeatSource.LocalSize,
                Is.EqualTo(new Vector3(0.5f, 0.5f, 0.24f)));
            Assert.That(grill.HeatSource.LocalUp, Is.EqualTo(Vector3.forward));
            Assert.That(grill.Combustion.IsConfigured, Is.True);
            Assert.That(
                grill.Combustion.AcceptedFuelDefinitionId,
                Is.EqualTo("item.charcoal"));
            Assert.That(grill.Combustion.BurnTimeStateId, Is.EqualTo("burn-time"));
            Assert.That(grill.Combustion.WetStateId, Is.EqualTo("wet"));
            Assert.That(grill.Combustion.MinimumFuelToIgnite, Is.EqualTo(10f));
            Assert.That(grill.Combustion.ExtinguishFuelThreshold, Is.EqualTo(5f));
            Assert.That(grill.Combustion.ActiveDurationSeconds, Is.EqualTo(120f));
            Assert.That(grill.Combustion.ActiveFuelPerSecond, Is.EqualTo(0.1f));
            Assert.That(grill.Combustion.EmberFuelPerSecond, Is.EqualTo(0.04f));
            Assert.That(grill.Combustion.FuelPourRatePerSecond, Is.EqualTo(12f));
            Assert.That(
                grill.Combustion.MinimumFuelPourTiltDegrees,
                Is.EqualTo(80f));
            Assert.That(
                grill.Combustion.FirePresentationScale,
                Is.EqualTo(0.48f));
            Assert.That(
                grill.Combustion.FireEmissionMultiplier,
                Is.EqualTo(0.16f));
            Assert.That(
                grill.Combustion.FireLightIntensity,
                Is.EqualTo(145f));
            Assert.That(
                grill.Combustion.FireLightRange,
                Is.EqualTo(1.65f));
            AssertScalar(grill, "burn-time", 0f);

            ItemDefinitionRecord bucket = ByFeature("P1.ITEM.143");
            Assert.That(bucket.CanOpen, Is.True);
            Assert.That(bucket.PrimaryAction, Is.EqualTo(ItemPrimaryAction.None));
        }

        [Test]
        public void GarbageBarrelPhysicsShapeKeepsAnOpenEightSegmentShell()
        {
            var root = new GameObject("Garbage barrel physics test");
            var body = root.AddComponent<Rigidbody>();
            var bottom = root.AddComponent<BoxCollider>();
            var shape = root.AddComponent<GarbageBarrelPhysicsShape>();
            try
            {
                shape.Configure(
                    bottom,
                    new Bounds(
                        new Vector3(0.1f, -0.2f, 0.3f),
                        new Vector3(0.592f, 0.592f, 0.876f)),
                    null);

                Assert.That(
                    shape.GeneratedWallPartCount,
                    Is.EqualTo(GarbageBarrelPhysicsShape.WallSegmentCount));
                Assert.That(
                    root.GetComponentsInChildren<BoxCollider>(true),
                    Has.Length.EqualTo(
                        GarbageBarrelPhysicsShape.WallSegmentCount + 1));
                Assert.That(bottom.size.z, Is.LessThan(0.06f));
                Assert.That(body.centerOfMass.z, Is.LessThan(0.3f));
                Assert.That(
                    root.transform.Cast<Transform>().All(child =>
                        child.GetComponent<BoxCollider>() != null),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InitialContainerStateUsesDeterministicUniqueChildIdentities()
        {
            ItemDefinitionRecord beerCase = ByFeature("P1.ITEM.113");
            StableEntityId stableId =
                ItemStableIdUtility.CreateDeterministic("test.beer-case");

            ItemInstanceState first = ItemInstanceState.CreateInitial(
                beerCase,
                stableId);
            ItemInstanceState second = ItemInstanceState.CreateInitial(
                beerCase,
                stableId);

            Assert.That(first.TryValidate(beerCase, out string failure), Is.True, failure);
            Assert.That(first.containedStableIds, Has.Length.EqualTo(24));
            Assert.That(first.containedStableIds.Distinct().Count(), Is.EqualTo(24));
            Assert.That(first.containedStableIds, Is.EqualTo(second.containedStableIds));
            Assert.That(first.containedStableIds, Does.Not.Contain(stableId.Value));
            Assert.That(
                first.containedStableIds.All(value =>
                    StableEntityId.TryParse(value, out _)),
                Is.True);
        }

        [Test]
        public void ItemStateAndSaveDtoDeepCloneWithoutAliasingAndRejectDuplicateIds()
        {
            ItemDefinitionRecord definition = ByFeature("P1.ITEM.143");
            StableEntityId stableId =
                ItemStableIdUtility.CreateDeterministic("test.kilju-bucket");
            ItemInstanceState state = ItemInstanceState.CreateInitial(
                definition,
                stableId);
            ItemInstanceState clone = state.DeepClone();
            clone.scalarStates[0].value = 3f;
            clone.flagStates[0].value = true;

            Assert.That(state.scalarStates[0].value, Is.Not.EqualTo(3f));
            Assert.That(state.flagStates[0].value, Is.False);

            var record = new ItemRuntimeSaveRecord
            {
                state = state,
                sourceCellId = "cell_0_0",
                materializationPosition = new Vector3(1f, 2f, 3f),
                materializationRotation = Quaternion.Euler(0f, 45f, 0f),
            };
            var domain = new ItemDomainSaveDto
            {
                instances = new[] { record },
            };
            string json = JsonUtility.ToJson(domain);
            ItemDomainSaveDto restored =
                JsonUtility.FromJson<ItemDomainSaveDto>(json);

            Assert.That(
                restored.TryValidate(definitions, out string failure),
                Is.True,
                failure);
            Assert.That(restored.instances[0].state.stableEntityId,
                Is.EqualTo(stableId.Value));

            restored.instances = new[]
            {
                restored.instances[0],
                restored.instances[0].DeepClone(),
            };
            Assert.That(restored.TryValidate(definitions, out _), Is.False);
        }

        [Test]
        public void SaveDomainRejectsCrossContainerIdentityCollisionsAndCountMismatch()
        {
            ItemDefinitionRecord packageDefinition = ByFeature("P1.ITEM.126");
            StableEntityId firstPackageId =
                ItemStableIdUtility.CreateDeterministic("test.package.first");
            StableEntityId secondPackageId =
                ItemStableIdUtility.CreateDeterministic("test.package.second");
            ItemInstanceState firstPackage = ItemInstanceState.CreateInitial(
                packageDefinition,
                firstPackageId);
            ItemInstanceState secondPackage = ItemInstanceState.CreateInitial(
                packageDefinition,
                secondPackageId);
            secondPackage.containedStableIds[0] =
                firstPackage.containedStableIds[0];
            var duplicateContained = new ItemDomainSaveDto
            {
                instances = new[]
                {
                    CreateRecord(firstPackage),
                    CreateRecord(secondPackage),
                },
            };

            Assert.That(
                duplicateContained.TryValidate(definitions, out _),
                Is.False);

            Assert.That(StableEntityId.TryParse(
                firstPackage.containedStableIds[0],
                out StableEntityId childId), Is.True);
            Assert.That(definitions.TryGet(
                packageDefinition.ChildDefinitionId,
                out ItemDefinitionRecord childDefinition), Is.True);
            ItemInstanceState topLevelChild = ItemInstanceState.CreateInitial(
                childDefinition,
                childId);
            var childAlsoTopLevel = new ItemDomainSaveDto
            {
                instances = new[]
                {
                    CreateRecord(firstPackage),
                    CreateRecord(topLevelChild),
                },
            };

            Assert.That(childAlsoTopLevel.TryValidate(definitions, out _), Is.False);

            ItemInstanceState mismatchedCount = firstPackage.DeepClone();
            mismatchedCount.content -= 1f;
            Assert.That(
                mismatchedCount.TryValidate(packageDefinition, out _),
                Is.False);
        }

        [Test]
        public void DeterministicIdentityDependsOnProjectOwnedKeyNotCallOrder()
        {
            StableEntityId first = ItemStableIdUtility.CreateDeterministic(
                "placement.item.gasoline-can.01");
            _ = ItemStableIdUtility.CreateDeterministic("unrelated-key");
            StableEntityId repeated = ItemStableIdUtility.CreateDeterministic(
                "placement.item.gasoline-can.01");
            StableEntityId different = ItemStableIdUtility.CreateDeterministic(
                "placement.item.gasoline-can.02");

            Assert.That(first.IsValid, Is.True);
            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(different, Is.Not.EqualTo(first));
        }

        private ItemDefinitionRecord ByFeature(string featureId) =>
            definitions.Definitions.Single(definition =>
                string.Equals(
                    definition.FeatureId,
                    featureId,
                    StringComparison.Ordinal));

        private static ItemRuntimeSaveRecord CreateRecord(
            ItemInstanceState state) => new ItemRuntimeSaveRecord
        {
            state = state,
            materializationPosition = Vector3.zero,
            materializationRotation = Quaternion.identity,
        };

        private void AssertPackage(
            string featureId,
            int expectedCount,
            string expectedChildDefinitionId)
        {
            ItemDefinitionRecord package = ByFeature(featureId);
            Assert.That(package.InitialChildCount, Is.EqualTo(expectedCount));
            Assert.That(package.InitialContent, Is.EqualTo(expectedCount));
            Assert.That(package.ChildDefinitionId,
                Is.EqualTo(expectedChildDefinitionId));
            Assert.That(package.PrimaryAction,
                Is.EqualTo(ItemPrimaryAction.DispenseChild));
        }

        private static void AssertScalar(
            ItemDefinitionRecord definition,
            string stateId,
            float expectedInitial)
        {
            ItemScalarDefinition scalar = definition.ScalarStates.Single(value =>
                string.Equals(value.StateId, stateId, StringComparison.Ordinal));
            Assert.That(scalar.InitialValue, Is.EqualTo(expectedInitial).Within(0.000001f));
        }
    }
}
