using System;
using System.Linq;
using System.Reflection;
using MSC.Core.Identity;
using MSC.Core.Time;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Items.Presentation;
using MSC.Save;
using MSC.Save.Integration;
using MSC.Vehicle.ItemsIntegration;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Items.Tests.EditMode
{
    public sealed class ItemRuntimeAndSaveTests
    {
        private const string DefinitionCatalogPath =
            "Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset";
        private const string PlacementCatalogPath =
            "Assets/Game/Items/Content/Placements/Phase1ItemPlacementCatalog.asset";

        private RuntimeFixture fixture;

        [SetUp]
        public void SetUp()
        {
            fixture = new RuntimeFixture();
        }

        [TearDown]
        public void TearDown()
        {
            fixture?.Dispose();
            fixture = null;
        }

        [Test]
        public void GeneratedPubMealUsesBoundedReviewedMeshInsteadOfCubeProxy()
        {
            const string mealPrefabPath =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                "Prefabs/LegacyItemVisual_" +
                "item_sausage-and-potatoes-meal.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                mealPrefabPath);
            Assert.That(prefab, Is.Not.Null, mealPrefabPath);

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Renderer[] renderers =
                    instance.GetComponentsInChildren<Renderer>(true);
                MeshFilter[] meshFilters =
                    instance.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(renderers, Has.Length.EqualTo(2));
                Assert.That(meshFilters, Has.Length.EqualTo(2));
                string[] meshNames = meshFilters
                    .Select(value => value.sharedMesh != null
                        ? value.sharedMesh.name
                        : string.Empty)
                    .ToArray();
                Assert.That(
                    meshNames.Any(value => string.Equals(
                        value,
                        "LegacyItemMesh_9fb50be10853e4049b0bc2980585fb93",
                        StringComparison.Ordinal)),
                    Is.True,
                    "The pub meal lost its cardboard tray mesh.");
                Assert.That(
                    meshNames.Any(value => string.Equals(
                        value,
                        "LegacyItemMesh_d7da12041a31de744bd02e1eca47b27e",
                        StringComparison.Ordinal)),
                    Is.True,
                    "The pub meal lost its food insert mesh.");
                Bounds bounds = renderers[0].bounds;
                for (int index = 1; index < renderers.Length; index++)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }

                Assert.That(bounds.size.magnitude, Is.GreaterThan(0.02f));
                Assert.That(
                    Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z),
                    Is.LessThan(1f),
                    "The prepared meal must not become an oversized handoff proxy.");
                Assert.That(
                    instance.GetComponentsInChildren<ItemProxyPresentation>(
                        true),
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedRetailHandoffVisualsAreDetachedAndBounded()
        {
            string[] prefabNames =
            {
                "item_potato-chips",
                "item_yeast",
                "item_sugar",
                "item_juice-concentrate",
                "item_coffee-package",
                "item_charcoal",
                "item_cigarettes",
                "item_mosquito-spray",
                "item_brake-fluid",
                "item_coolant",
                "item_motor-oil",
                "item_two-stroke-oil",
                "item_alternator-belt",
                "item_oil-filter",
                "item_sparkplug-box",
                "item_lightbulb-box",
                "item_fuse-package",
                "item_r20-battery",
                "item_car-battery",
                "item_fire-extinguisher",
                "item_spray-paint-variants_variant_00",
                "item_spray-paint-variants_variant_12",
            };

            foreach (string prefabName in prefabNames)
            {
                string path =
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/" +
                    "Items/Prefabs/LegacyItemVisual_" + prefabName +
                    ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    path);
                Assert.That(prefab, Is.Not.Null, path);
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    Renderer[] renderers = instance
                        .GetComponentsInChildren<Renderer>(true);
                    Assert.That(renderers, Has.Length.GreaterThan(0), path);
                    Bounds bounds = renderers[0].bounds;
                    for (int index = 1; index < renderers.Length; index++)
                    {
                        bounds.Encapsulate(renderers[index].bounds);
                    }

                    Assert.That(bounds.size.magnitude,
                        Is.InRange(0.01f, 1.5f),
                        "Retail handoff must be one detached item, not a " +
                        "Combined Mesh store interior: " + path);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void FlashlightAndPartsMagazineUseReviewedSurfaceMaterials()
        {
            foreach (string definitionName in new[]
                     {
                         "item_flashlight",
                         "item_parts-magazine",
                     })
            {
                string path =
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/" +
                    "Items/Prefabs/LegacyItemVisual_" + definitionName +
                    ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    path);
                Assert.That(prefab, Is.Not.Null, path);
                Material[] materials = prefab
                    .GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .ToArray();
                Assert.That(materials, Is.Not.Empty, path);
                Assert.That(
                    materials.All(material => !material.name.Contains(
                        "Fallback",
                        StringComparison.Ordinal)),
                    Is.True,
                    path);
            }
        }

        [Test]
        public void SpannerPresentationRebuildsSanitizedToolBodiesSafely()
        {
            const string path =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                "Prefabs/LegacyItemVisual_item_spanner-set.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            WorldItemInstance owner = fixture.Spawn(
                "P1.ITEM.138",
                "spanner-presentation-sanitization",
                Vector3.zero);
            GameObject visual = UnityEngine.Object.Instantiate(
                prefab,
                owner.transform,
                false);

            try
            {
                SpannerSetPresentationController controller =
                    visual.GetComponentInChildren<
                        SpannerSetPresentationController>(true);
                Assert.That(controller, Is.Not.Null);
                Assert.DoesNotThrow(() => controller.Bind(owner));
                Assert.That(controller.PickupTargets.Count, Is.EqualTo(11));
                Assert.That(
                    controller.PickupTargets.All(target =>
                        target != null &&
                        target.GetComponent<Rigidbody>() == null),
                    Is.True,
                    "Spanners are selectable tool modes, never Rigidbody props.");
                Assert.That(
                    controller.PickupTargets.All(target =>
                        target.GetComponent<BoxCollider>() is BoxCollider collider &&
                        collider.isTrigger &&
                        collider.size.x >= 0.02f &&
                        collider.size.y >= 0.02f &&
                        collider.size.z >= 0.02f),
                    Is.True,
                    "Docked wrench triggers must remain queryable without colliding with the moving case.");
                Assert.That(
                    controller.PickupTargets.All(target =>
                        target.GetComponent<InteractionTargetHost>() is
                            InteractionTargetHost host &&
                        host.SelectionPriority > 0 &&
                        target is IParentColliderOcclusionBypass),
                    Is.True,
                    "Nested wrench targets must outrank and explicitly bypass only their own toolbox shell in the bounded ray resolver.");
                Assert.That(
                    owner.TryPerformPrimaryAction(fixture.Context),
                    Is.True);
                Assert.That(
                    controller.PickupTargets.All(target =>
                        target.CanSelect(fixture.Context)),
                    Is.True,
                    "Every wrench must become selectable while the case is open.");
                Vector3 smallestIdle =
                    controller.PickupTargets[0].IdleLocalPosition;
                Vector3 largestIdle =
                    controller.PickupTargets[^1].IdleLocalPosition;
                Assert.That(smallestIdle.z, Is.GreaterThan(0.03f));
                Assert.That(
                    smallestIdle.x / smallestIdle.z,
                    Is.EqualTo(largestIdle.x / largestIdle.z)
                        .Within(0.0001f));
                Assert.That(
                    smallestIdle.y / smallestIdle.z,
                    Is.EqualTo(largestIdle.y / largestIdle.z)
                        .Within(0.0001f));
                float smallestApparentScale =
                    controller.PickupTargets[0].transform.lossyScale.x /
                    smallestIdle.z;
                float largestApparentScale =
                    controller.PickupTargets[^1].transform.lossyScale.x /
                    largestIdle.z;
                Assert.That(
                    smallestApparentScale,
                    Is.LessThan(largestApparentScale * 0.6f),
                    "Partial distance compensation must preserve the visible size difference between 5 mm and 15 mm keys.");
                Assert.That(
                    controller.PickupTargets.All(target =>
                        target is IFirstPersonToolSelectionTransition),
                    Is.True);
                SpannerSetToolPickupTarget first =
                    controller.PickupTargets[0];
                first.NotifySelected(fixture.Context);
                Assert.That(first.IsDocked, Is.False);
                Assert.That(
                    first.GetComponent<BoxCollider>().enabled,
                    Is.False);
                Assert.That(
                    first,
                    Is.AssignableTo<IFirstPersonToolSelectionTarget>());
                first.NotifyDeselected();
                Assert.That(first.IsDocked, Is.True);
                Assert.That(
                    first.GetComponent<BoxCollider>().enabled,
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        [Test]
        public void GeneratedJacksExposeReviewedLiftHeadsAndDistinctHandling()
        {
            AssertJackPresentation(
                "item_car-jack",
                VehicleJackInteractionController.CarJackDefinitionId,
                expectsGroundDrag: false,
                expectedMaximumLift: 0.48f);
            AssertJackPresentation(
                "item_floor-jack",
                VehicleJackInteractionController.FloorJackDefinitionId,
                expectsGroundDrag: true,
                expectedMaximumLift: 0.38f);
        }

        [Test]
        public void GeneratedFloorJackArmComposesLiftDeltaAfterDonorRestRotation()
        {
            const string path =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                "Prefabs/LegacyItemVisual_item_floor-jack.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);

            try
            {
                VehicleJackInteractionController controller = instance
                    .GetComponentInChildren<
                        VehicleJackInteractionController>(true);
                Assert.That(controller, Is.Not.Null);

                FieldInfo membersField = typeof(
                    VehicleJackInteractionController).GetField(
                    "articulatedMembers",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(membersField, Is.Not.Null);
                Transform[] members =
                    (Transform[])membersField.GetValue(controller);
                Assert.That(members, Has.Length.EqualTo(2));
                Transform arm = members[1];
                Assert.That(arm, Is.Not.Null);

                Assert.That(arm.childCount, Is.EqualTo(1));
                Transform armBase = arm.GetChild(0);
                MeshRenderer[] armLeafRenderers = armBase
                    .GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(armLeafRenderers, Has.Length.EqualTo(1));
                Transform armLeaf = armLeafRenderers[0].transform;
                Assert.That(
                    Vector3.Distance(
                        armLeaf.localPosition,
                        new Vector3(0f, 0f, 0.2f)),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        armLeaf.localRotation,
                        new Quaternion(
                            0.000000145778557f,
                            0.7071066f,
                            0.707107f,
                            -0.000000476837158f)),
                    Is.LessThan(0.01f),
                    "Reparenting must not bake the lower Arm wrapper angle into the donor mesh leaf.");
                Assert.That(
                    Vector3.Distance(armLeaf.localScale, Vector3.one),
                    Is.LessThan(0.0001f));

                Transform saddleVisualPivot = controller.LiftHead
                    .GetComponentsInChildren<Transform>(true)
                    .Single(value => string.Equals(
                        value.name,
                        "Project-owned floor jack saddle visual pivot",
                        StringComparison.Ordinal));
                Assert.That(
                    Vector3.Distance(
                        saddleVisualPivot.localPosition,
                        Vector3.zero),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        saddleVisualPivot.localRotation,
                        Quaternion.Euler(0f, 90f, 0f)),
                    Is.LessThan(0.01f));
                Assert.That(
                    Vector3.Distance(
                        saddleVisualPivot.localScale,
                        Vector3.one),
                    Is.LessThan(0.0001f));

                Quaternion expectedLower = new(
                    -0.0466433465f,
                    -0.705553353f,
                    -0.0466416553f,
                    0.7055803f);
                Assert.That(
                    Quaternion.Angle(arm.localRotation, expectedLower),
                    Is.LessThan(0.01f),
                    "The authored lower pose must retain the vanilla donor Arm quaternion.");

                MethodInfo captureRestPose = typeof(
                    VehicleJackInteractionController).GetMethod(
                    "CaptureArticulationRestPose",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo setLiftHeight = typeof(
                    VehicleJackInteractionController).GetMethod(
                    "SetLiftHeightImmediate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(captureRestPose, Is.Not.Null);
                Assert.That(setLiftHeight, Is.Not.Null);
                captureRestPose.Invoke(controller, new object[] { true });
                setLiftHeight.Invoke(
                    controller,
                    new object[] { controller.MaximumLiftMeters });

                Quaternion expectedMaximum = new(
                    -0.285207778f,
                    0.647022069f,
                    -0.2851801f,
                    -0.6470636f);
                Assert.That(
                    Quaternion.Angle(arm.localRotation, expectedMaximum),
                    Is.LessThan(0.01f),
                    "The lift delta must be post-multiplied onto the donor rest pose; reversing the order twists the Arm around the wrong local axes.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void FloorJackPumpLeverReachesDonorPeakAndReturnsToRest()
        {
            const string path =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                "Prefabs/LegacyItemVisual_item_floor-jack.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            WorldItemInstance owner = fixture.Spawn(
                "P1.ITEM.141",
                "floor-jack-pump-lever",
                Vector3.zero);
            GameObject visual = UnityEngine.Object.Instantiate(
                prefab,
                owner.transform,
                false);

            try
            {
                VehicleJackInteractionController controller = visual
                    .GetComponentInChildren<
                        VehicleJackInteractionController>(true);
                Assert.That(controller, Is.Not.Null);
                controller.Bind(owner);
                Assert.That(controller.PumpLever, Is.Not.Null);
                Assert.That(controller.IsPumpLeverAnimating, Is.False);
                Quaternion expectedRest = Quaternion.identity;
                Assert.That(
                    Quaternion.Angle(
                        controller.PumpLever.localRotation,
                        expectedRest),
                    Is.LessThan(0.01f));

                controller.Interact(fixture.Context);
                Assert.That(controller.IsPumpLeverAnimating, Is.True);

                MethodInfo updatePumpLever = typeof(
                    VehicleJackInteractionController).GetMethod(
                    "UpdatePumpLeverPresentation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(updatePumpLever, Is.Not.Null);
                updatePumpLever.Invoke(
                    controller,
                    new object[] { 0.3333334f });

                Quaternion expectedPeak = new(
                    0f,
                    0f,
                    0.34347698f,
                    0.93916106f);
                Assert.That(
                    Quaternion.Angle(
                        controller.PumpLever.localRotation,
                        expectedPeak),
                    Is.LessThan(0.01f));
                Assert.That(controller.IsPumpLeverAnimating, Is.True);

                updatePumpLever.Invoke(
                    controller,
                    new object[] { 0.3333334f });
                Assert.That(controller.IsPumpLeverAnimating, Is.False);
                Assert.That(
                    Quaternion.Angle(
                        controller.PumpLever.localRotation,
                        expectedRest),
                    Is.LessThan(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        [Test]
        public void LoweredCarJackCanBePickedUpBeforeItsDirectionalLiftConsumesLmb()
        {
            const string path =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                "Prefabs/LegacyItemVisual_item_car-jack.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            WorldItemInstance owner = fixture.Spawn(
                "P1.ITEM.140",
                "car-jack-pickup",
                Vector3.zero);
            GameObject visual = UnityEngine.Object.Instantiate(
                prefab,
                owner.transform,
                false);

            try
            {
                VehicleJackInteractionController controller = visual
                    .GetComponentInChildren<
                        VehicleJackInteractionController>(true);
                PhysicsPickupTarget pickup =
                    owner.GetComponent<PhysicsPickupTarget>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(pickup, Is.Not.Null);
                controller.Bind(owner);
                Assert.That(controller.UsesDirectionalHold, Is.False);
                Assert.That(pickup.CanPickup(fixture.Context), Is.True);

                controller.Interact(fixture.Context);
                Assert.That(controller.LiftHeightMeters,
                    Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(controller.UsesDirectionalHold, Is.True);
                Assert.That(pickup.CanPickup(fixture.Context), Is.False);
                Vector3 liftedHeadPosition =
                    controller.LiftHead.localPosition;
                controller.Bind(owner);
                Assert.That(
                    Vector3.Distance(
                        controller.LiftHead.localPosition,
                        liftedHeadPosition),
                    Is.LessThan(0.0001f),
                    "Rebinding after cell rematerialization must not treat the raised pose as a new rest pose.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        [Test]
        public void FloorJackQueuesDonorPumpStepAndUsesDynamicPhysicalContract()
        {
            const string path =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                "Prefabs/LegacyItemVisual_item_floor-jack.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            WorldItemInstance owner = fixture.Spawn(
                "P1.ITEM.141",
                "floor-jack-state",
                Vector3.zero);
            GameObject visual = UnityEngine.Object.Instantiate(
                prefab,
                owner.transform,
                false);

            try
            {
                VehicleJackInteractionController controller = visual
                    .GetComponentInChildren<
                        VehicleJackInteractionController>(true);
                Assert.That(controller, Is.Not.Null);
                Assert.DoesNotThrow(() => controller.Bind(owner));
                Assert.That(owner.TryGetScalar("lift-height", out float initial),
                    Is.True);
                Assert.That(initial, Is.Zero);
                controller.Interact(fixture.Context);
                Assert.That(controller.LiftHeightMeters,
                    Is.Zero,
                    "The physical head must follow the donor pump command over fixed steps, not teleport.");
                Assert.That(controller.TargetLiftHeightMeters,
                    Is.EqualTo(0.015f).Within(0.0001f));
                Assert.That(owner.TryGetScalar("lift-height", out float raised),
                    Is.True);
                Assert.That(raised, Is.Zero,
                    "Only physically completed lift travel is persistent state.");

                Rigidbody rootBody = owner.GetComponent<Rigidbody>();
                Assert.That(rootBody.mass,
                    Is.EqualTo(30f).Within(0.0001f));
                Assert.That(rootBody.isKinematic, Is.False);
                Assert.That(rootBody.useGravity, Is.True);
                Assert.That(rootBody.constraints,
                    Is.EqualTo(
                        RigidbodyConstraints.FreezePositionY |
                        RigidbodyConstraints.FreezeRotationX |
                        RigidbodyConstraints.FreezeRotationZ));
                Assert.That(
                    controller.CanBeginContinuousInteraction(
                        fixture.Context,
                        ContinuousContextInteractionDirection.Secondary),
                    Is.True);
                controller.BeginContinuousInteraction(
                    fixture.Context,
                    ContinuousContextInteractionDirection.Secondary);
                Assert.That(controller.ContinueContinuousInteraction(1f),
                    Is.False);
                Assert.That(controller.LiftHeightMeters, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        private static void AssertJackPresentation(
            string prefabName,
            string expectedDefinitionId,
            bool expectsGroundDrag,
            float expectedMaximumLift)
        {
            string path =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                "Prefabs/LegacyItemVisual_" + prefabName + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            VehicleJackInteractionController controller = prefab
                .GetComponentInChildren<VehicleJackInteractionController>(
                    true);
            Assert.That(controller, Is.Not.Null, path);
            Assert.That(controller.DefinitionId,
                Is.EqualTo(expectedDefinitionId));
            Assert.That(controller.LiftHead, Is.Not.Null);
            Assert.That(controller.SupportsGroundDrag,
                Is.EqualTo(expectsGroundDrag));
            Assert.That(controller.MaximumLiftMeters,
                Is.EqualTo(expectedMaximumLift).Within(0.0001f));
            Assert.That(
                controller.ArticulatedMemberCount,
                Is.EqualTo(expectsGroundDrag ? 2 : 4),
                "The jack must move its mechanism, not levitate only the contact pad.");

            if (!expectsGroundDrag)
            {
                return;
            }

            Assert.That(controller.LiftHeadBody, Is.Not.Null);
            Assert.That(controller.LiftHeadBody.isKinematic, Is.True);
            Assert.That(controller.LiftHeadBody.useGravity, Is.False);
            Assert.That(controller.LiftHeadBody.mass,
                Is.EqualTo(0.0000001f).Within(0.000000001f));
            Assert.That(controller.LiftHeadCollider, Is.Not.Null);
            Assert.That(controller.LiftHeadCollider.isTrigger, Is.False);
            Assert.That(
                Vector3.Distance(
                    controller.LiftHeadCollider.center,
                    Vector3.zero),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(
                    controller.LiftHeadCollider.size,
                    new Vector3(0.1f, 0.09f, 0.1f)),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void BeerCasePresentationRetainsDonorUprightRotation()
        {
            const string path =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                "Prefabs/LegacyItemVisual_item_beer-case.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Quaternion expected = new(
                -0.70150095f,
                -0.08886231f,
                -0.088862255f,
                0.70150083f);
            Assert.That(
                Quaternion.Angle(prefab.transform.localRotation, expected),
                Is.LessThan(0.1f));
        }

        [Test]
        public void SprayPaintCheckoutVariantsMaterializeValidItemState()
        {
            string[] expectedVariants =
            {
                "matte01",
                "spray01",
                "spray02",
                "spray03",
                "spray04",
                "spray05",
                "spray06",
                "spray07",
                "spray08",
                "spray09",
                "spray10",
                "spray11",
                "spray12",
            };

            for (int variantIndex = 0;
                 variantIndex < expectedVariants.Length;
                 variantIndex++)
            {
                WorldItemInstance spray = fixture.Spawn(
                    "P1.ITEM.132",
                    "spray-checkout-" + variantIndex,
                    Vector3.right * variantIndex,
                    variantIndex: variantIndex);
                Assert.That(spray.State.variantIndex, Is.EqualTo(variantIndex));
                Assert.That(
                    spray.State.TryValidate(
                        spray.Definition,
                        out string failure),
                    Is.True,
                    failure);
                Assert.That(
                    spray.ActiveToolVariant,
                    Is.EqualTo(expectedVariants[variantIndex]));
            }
        }

        [Test]
        public void LiquidTransferRequiresOpenSourceAndPreservesLiquidIdentity()
        {
            WorldItemInstance source = fixture.Spawn(
                "P1.ITEM.133",
                "liquid-source",
                Vector3.zero);
            WorldItemInstance target = fixture.Spawn(
                "P1.ITEM.145",
                "liquid-target",
                Vector3.right);

            Assert.That(source.TryTransferLiquidTo(target, 0.5f, out _), Is.False);
            Assert.That(source.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(
                source.TryTransferLiquidTo(target, 0.5f, out float transferred),
                Is.True);

            Assert.That(transferred, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(source.State.content, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(target.State.content, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(target.State.liquidId, Is.EqualTo("liquid.gasoline"));
            Assert.That(source.State.liquidId, Is.EqualTo("liquid.gasoline"));
        }

        [Test]
        public void BasketballUsesDynamicRollingBallPhysics()
        {
            WorldItemInstance basketball = fixture.Spawn(
                "P1.ITEM.171",
                "basketball-physics",
                new Vector3(0f, 2f, 0f));
            Rigidbody body = basketball.GetComponent<Rigidbody>();
            SphereCollider collider = basketball.GetComponent<SphereCollider>();
            PhysicsPickupTarget pickup =
                basketball.GetComponent<PhysicsPickupTarget>();

            Assert.That(body, Is.Not.Null);
            Assert.That(body.useGravity, Is.True);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.detectCollisions, Is.True);
            Assert.That(body.linearDamping, Is.EqualTo(0.015f).Within(0.0001f));
            Assert.That(body.angularDamping, Is.EqualTo(0.035f).Within(0.0001f));
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.sharedMaterial, Is.Not.Null);
            Assert.That(
                collider.sharedMaterial.bounciness,
                Is.EqualTo(0.68f).Within(0.0001f));
            Assert.That(
                collider.sharedMaterial.bounceCombine,
                Is.EqualTo(PhysicsMaterialCombine.Maximum));
            Assert.That(pickup.UsesGravityWhenLoose, Is.True);
        }

        [Test]
        public void CanonicalNewGameMaterializationAppliesAuditedPhysicsWithoutImpulse()
        {
            ItemDefinitionCatalog definitions =
                AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                    DefinitionCatalogPath);
            ItemPlacementCatalog canonicalPlacements =
                AssetDatabase.LoadAssetAtPath<ItemPlacementCatalog>(
                    PlacementCatalogPath);
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
                        "Assets/Tests/Scenes/UnusedCanonicalItemCell.unity"),
                });
            var serviceObject = new GameObject("Canonical item audit streaming");
            var runtimeObject = new GameObject("Canonical item audit runtime");
            ProductionWorldStreamingService streaming =
                serviceObject.AddComponent<ProductionWorldStreamingService>();
            streaming.ConfigureForAuthoring(manifest);
            ItemWorldRuntime runtime =
                runtimeObject.AddComponent<ItemWorldRuntime>();

            try
            {
                runtime.Initialize(
                    definitions,
                    canonicalPlacements,
                    streaming,
                    SceneManager.GetActiveScene(),
                    -64f,
                    new GameTimeService());

                Assert.That(runtime.LoadedInstances.Count, Is.EqualTo(43));
                foreach (ItemPlacementRecord placement in
                         canonicalPlacements.Placements)
                {
                    Assert.That(
                        runtime.TryGetInstance(
                            placement.StableEntityId,
                            out WorldItemInstance instance),
                        Is.True,
                        placement.PlacementId);
                    Assert.That(
                        Vector3.Distance(
                            instance.transform.position,
                            placement.WorldPosition),
                        Is.LessThan(0.0001f),
                        placement.PlacementId);
                    Assert.That(
                        Quaternion.Angle(
                            instance.transform.rotation,
                            placement.WorldRotation),
                        Is.LessThan(0.001f),
                        placement.PlacementId);
                    Assert.That(
                        instance.transform.localScale,
                        Is.EqualTo(placement.InitialLocalScale));
                    Assert.That(instance.gameObject.layer,
                        Is.EqualTo(ItemWorldRuntime.WorldItemCollisionLayer));

                    Rigidbody body = instance.GetComponent<Rigidbody>();
                    if (!placement.DonorHasRigidbody)
                    {
                        Assert.That(body, Is.Null, placement.PlacementId);
                        Assert.That(
                            instance.GetComponent<PhysicsPickupTarget>(),
                            Is.Null,
                            placement.PlacementId);
                    }
                    else
                    {
                        Assert.That(body, Is.Not.Null, placement.PlacementId);
                        Assert.That(body.useGravity,
                            Is.EqualTo(placement.InitialUseGravity));
                        Assert.That(body.isKinematic,
                            Is.EqualTo(placement.InitialIsKinematic));
                        Assert.That(body.detectCollisions,
                            Is.EqualTo(placement.InitialDetectCollisions));
                        Assert.That(body.interpolation,
                            Is.EqualTo(placement.InitialInterpolation));
                        Assert.That(body.constraints,
                            Is.EqualTo(placement.InitialConstraints));
                        Assert.That(body.linearDamping,
                            Is.EqualTo(placement.InitialLinearDamping)
                                .Within(0.0001f));
                        Assert.That(body.angularDamping,
                            Is.EqualTo(placement.InitialAngularDamping)
                                .Within(0.0001f));
                        Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
                        Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
                    }

                    Collider[] colliders = instance.GetComponents<Collider>();
                    Assert.That(colliders, Is.Not.Empty, placement.PlacementId);
                    Collider[] physicalColliders = colliders
                        .Where(collider => !collider.isTrigger)
                        .ToArray();
                    Assert.That(
                        physicalColliders.All(collider => collider.enabled),
                        Is.True,
                        placement.PlacementId);
                    if (instance.Definition.HasAuthoredColliderShapes)
                    {
                        Assert.That(
                            physicalColliders,
                            Has.Length.EqualTo(
                                instance.Definition.ColliderShapes.Count(shape =>
                                    !shape.IsTrigger)),
                            placement.PlacementId);
                    }
                }

                ItemPlacementRecord savedPlacement =
                    canonicalPlacements.Placements.Single(value =>
                        string.Equals(
                            value.PlacementId,
                            "placement.axe.01",
                            StringComparison.Ordinal));
                Assert.That(runtime.TryGetInstance(
                    savedPlacement.StableEntityId,
                    out WorldItemInstance savedItem), Is.True);
                Rigidbody savedBody = savedItem.GetComponent<Rigidbody>();
                Vector3 savedPosition =
                    savedPlacement.WorldPosition + new Vector3(7f, 2f, -4f);
                Quaternion savedRotation = Quaternion.Euler(17f, 83f, 29f);
                savedBody.position = savedPosition;
                savedBody.rotation = savedRotation;

                var deferred = new DeferredStableEntityStore();
                var items = new ItemSaveParticipant(runtime, deferred);
                var world = new WorldEntitySaveParticipant(
                    deferred,
                    streaming,
                    SceneManager.GetActiveScene());
                foreach (WorldItemInstance instance in runtime.LoadedInstances)
                {
                    PhysicsPickupTarget target =
                        instance.GetComponent<PhysicsPickupTarget>();
                    if (target != null)
                    {
                        world.RegisterTarget(target);
                    }
                }

                string itemPayload = items.CapturePayload();
                string worldPayload = world.CapturePayload();
                savedBody.position = savedPlacement.WorldPosition;
                savedBody.rotation = savedPlacement.WorldRotation;

                var unresolved = new UnresolvedContentReport();
                var preparation = new SaveRestorePreparationContext(
                    unresolved,
                    deferred);
                var restore = new SaveRestoreContext(unresolved, deferred);
                var itemEnvelope = new SaveDomainEnvelope
                {
                    DomainId = ItemSaveParticipant.DomainId,
                    SchemaVersion = ItemDomainSaveDto.CurrentSchemaVersion,
                    Required = true,
                    PayloadJson = itemPayload,
                };
                var worldEnvelope = new SaveDomainEnvelope
                {
                    DomainId = WorldEntitySaveParticipant.DomainId,
                    SchemaVersion =
                        WorldEntityDomainSaveDto.CurrentSchemaVersion,
                    Required = true,
                    PayloadJson = worldPayload,
                };
                items.ApplyPreparedRestore(
                    items.PrepareRestore(itemEnvelope, preparation),
                    restore);
                world.ApplyPreparedRestore(
                    world.PrepareRestore(worldEnvelope, preparation),
                    restore);

                Assert.That(
                    Vector3.Distance(savedBody.position, savedPosition),
                    Is.LessThan(0.0001f),
                    "A saved pose must override the canonical New Game pose.");
                Assert.That(
                    Quaternion.Angle(savedBody.rotation, savedRotation),
                    Is.LessThan(0.001f),
                    "A saved rotation must override the canonical New Game rotation.");
            }
            finally
            {
                foreach (WorldItemInstance instance in
                         runtime.LoadedInstances.ToArray())
                {
                    if (instance != null)
                    {
                        UnityEngine.Object.DestroyImmediate(instance.gameObject);
                    }
                }

                UnityEngine.Object.DestroyImmediate(runtimeObject);
                UnityEngine.Object.DestroyImmediate(serviceObject);
                UnityEngine.Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void ItemHudMetadata_SeparatesTitleFromPickupAction()
        {
            WorldItemInstance item = fixture.Spawn(
                "P1.ITEM.101",
                "hud-prompt-separation",
                Vector3.zero);
            PhysicsPickupTarget pickup =
                item.GetComponent<PhysicsPickupTarget>();
            IInteractionDisplayTarget display = item;

            Assert.That(
                display.InteractionDisplayName,
                Is.EqualTo(item.Definition.DisplayName));
            Assert.That(pickup.PickupPrompt, Is.EqualTo("Поднять"));
            Assert.That(
                pickup.PickupPrompt,
                Is.Not.EqualTo(display.InteractionDisplayName));
        }

        [Test]
        public void HelmetPaintStateIsProjectOwnedAndRoundTripsWithItemState()
        {
            WorldItemInstance helmet = fixture.Spawn(
                "P1.ITEM.155",
                "helmet-paint-state",
                Vector3.zero);

            Assert.That(
                helmet.TryGetPaint(
                    out _,
                    out bool initialMatte,
                    out bool initialApplied),
                Is.True);
            Assert.That(initialApplied, Is.False);
            Assert.That(initialMatte, Is.False);

            Color painted = new Color(0.15f, 0.65f, 0.3f, 1f);
            Assert.That(helmet.TryApplyPaint(painted, matte: true), Is.True);
            Assert.That(
                helmet.TryGetPaint(
                    out Color restored,
                    out bool matte,
                    out bool applied),
                Is.True);
            Assert.That(applied, Is.True);
            Assert.That(matte, Is.True);
            Assert.That(restored.r, Is.EqualTo(painted.r).Within(0.0001f));
            Assert.That(restored.g, Is.EqualTo(painted.g).Within(0.0001f));
            Assert.That(restored.b, Is.EqualTo(painted.b).Within(0.0001f));

            ItemInstanceState captured = helmet.CaptureState();
            Assert.That(
                captured.scalarStates.Any(state =>
                    state.stateId == ItemPaintStateIds.ColorRed),
                Is.True);
            Assert.That(
                captured.flagStates.Single(state =>
                    state.stateId == ItemPaintStateIds.Applied).value,
                Is.True);
        }

        [Test]
        public void HeldContainerRoutesTargetedLiquidActionThroughCarryCapabilities()
        {
            WorldItemInstance source = fixture.Spawn(
                "P1.ITEM.133",
                "held-liquid-source",
                Vector3.zero);
            WorldItemInstance target = fixture.Spawn(
                "P1.ITEM.145",
                "held-liquid-target",
                Vector3.right);
            source.TryPerformPrimaryAction(fixture.Context);

            var player = new GameObject("Item carry fixture");
            BoxCollider playerCollider = player.AddComponent<BoxCollider>();
            var anchor = new GameObject("Carry anchor");
            anchor.transform.SetParent(player.transform, false);
            anchor.transform.localPosition = Vector3.forward;
            PhysicalCarryController carry =
                player.AddComponent<PhysicalCarryController>();
            carry.Configure(anchor.transform, playerCollider);
            PhysicsPickupTarget pickup =
                source.GetComponent<PhysicsPickupTarget>();
            var context = new InteractionContext(
                player,
                player.transform.position,
                player.transform.forward);

            try
            {
                Assert.That(carry.TryPickup(pickup, context), Is.True);
                Assert.That(
                    carry.TryGetHeldCapability(out IHeldTargetActivationSource heldSource),
                    Is.True);
                Assert.That(heldSource, Is.SameAs(source));
                Assert.That(carry.TryActivateHeldOn(target, context), Is.True);
                Assert.That(target.State.content, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(target.State.liquidId, Is.EqualTo("liquid.gasoline"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void SaunaContainersFillAndSpillWithoutOpenCloseState()
        {
            WorldItemInstance bucket = fixture.Spawn(
                "P1.ITEM.145",
                "sauna-bucket-liquid",
                Vector3.zero);
            ItemActionCompleted observed = default;
            fixture.Runtime.ActionCompleted += value => observed = value;

            Assert.That(bucket.Definition.CanOpen, Is.False);
            Assert.That(
                bucket.Definition.PrimaryAction,
                Is.EqualTo(ItemPrimaryAction.None));
            Assert.That(
                bucket.TryFillLiquid(
                    LiquidTypeIds.Water,
                    0.5f,
                    out float filled),
                Is.True);
            Assert.That(filled, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(observed.Action, Is.EqualTo(ItemActionKind.LiquidFilled));
            Assert.That(bucket.CanActivateHeld(fixture.Context), Is.True);

            bucket.ActivateHeld(fixture.Context);

            Assert.That(observed.Action, Is.EqualTo(ItemActionKind.LiquidSpilled));
            Assert.That(observed.AffectedAmount, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(observed.LiquidId, Is.EqualTo(LiquidTypeIds.Water));
            Assert.That(bucket.State.content, Is.Zero);
            Assert.That(bucket.State.liquidId, Is.Empty);
        }

        [Test]
        public void TiltedSaunaBucketDrainsAuthoritativeWaterAndPublishesSpill()
        {
            WorldItemInstance bucket = fixture.Spawn(
                "P1.ITEM.145",
                "sauna-bucket-tilt-spill",
                Vector3.zero);
            LiquidContainerTiltSpiller spiller =
                bucket.GetComponent<LiquidContainerTiltSpiller>();
            Assert.That(spiller, Is.Not.Null);

            bucket.transform.rotation = Quaternion.FromToRotation(
                Vector3.back,
                Vector3.up);
            Assert.That(
                bucket.TryFillLiquid(
                    LiquidTypeIds.Water,
                    4f,
                    out float filled),
                Is.True);
            Assert.That(filled, Is.EqualTo(4f).Within(0.0001f));

            ItemActionCompleted spill = default;
            fixture.Runtime.ActionCompleted += action =>
            {
                if (action.Action == ItemActionKind.LiquidSpilled)
                {
                    spill = action;
                }
            };

            spiller.Tick(0.5f);
            Assert.That(bucket.LiquidAmountLitres,
                Is.EqualTo(4f).Within(0.0001f));
            Assert.That(spiller.IsSpilling, Is.False);

            bucket.transform.rotation = Quaternion.FromToRotation(
                Vector3.back,
                Vector3.down);
            spiller.Tick(0.12f);

            Assert.That(spiller.IsSpilling, Is.True);
            Assert.That(bucket.LiquidAmountLitres, Is.LessThan(4f));
            Assert.That(spill.Action, Is.EqualTo(ItemActionKind.LiquidSpilled));
            Assert.That(spill.AffectedAmount, Is.GreaterThan(0f));
            Assert.That(spill.LiquidId, Is.EqualTo(LiquidTypeIds.Water));
        }

        [Test]
        public void GarbageBarrelIgnitesOnceAndBurnsNonCriticalItems()
        {
            WorldItemInstance barrel = fixture.Spawn(
                "P1.ITEM.148",
                "garbage-barrel-fire",
                Vector3.zero);
            WorldItemInstance rubbish = fixture.Spawn(
                "P1.ITEM.103",
                "garbage-barrel-rubbish",
                Vector3.right);

            Assert.That(barrel.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(barrel.IsIgnited, Is.True);
            Assert.That(barrel.TryPerformPrimaryAction(fixture.Context), Is.False);
            Assert.That(barrel.CanInteract(fixture.Context), Is.False);
            Assert.That(
                barrel.GetComponent<GarbageBarrelBurner>(),
                Is.Not.Null);

            Assert.That(rubbish.TryBurnInGarbageBarrel(), Is.True);
            Assert.That(rubbish.State.isConsumed, Is.True);
            Assert.That(rubbish.gameObject.activeSelf, Is.False);
            Assert.That(rubbish.CaptureState().isConsumed, Is.True);
        }

        [Test]
        public void PortableGrillFuelIgnitionCombustionAndStateRoundTrip()
        {
            WorldItemInstance charcoal = fixture.Spawn(
                "P1.ITEM.110",
                "grill-charcoal-source",
                Vector3.zero);
            WorldItemInstance grill = fixture.Spawn(
                "P1.ITEM.149",
                "fuelled-portable-grill",
                Vector3.right);

            Assert.That(
                grill.CanInteract(fixture.Context),
                Is.False,
                "An unavailable empty-fuel action must not become a HUD hint.");
            Assert.That(grill.TryPerformPrimaryAction(fixture.Context), Is.False);
            Assert.That(
                charcoal.CanActivateHeldOn(grill, fixture.Context),
                Is.False,
                "Charcoal is poured by physical tilt, not an F shortcut.");
            ItemFuelPourReceiver pourReceiver =
                grill.GetComponent<ItemFuelPourReceiver>();
            Assert.That(pourReceiver, Is.Not.Null);
            Assert.That(
                pourReceiver.TryPourFrom(
                    charcoal,
                    1f,
                    out _),
                Is.False,
                "An upright package must not leak charcoal.");
            charcoal.transform.SetPositionAndRotation(
                grill.transform.TransformPoint(
                    grill.Definition.HeatSource.LocalCenter),
                Quaternion.Euler(90f, 0f, 0f));
            Physics.SyncTransforms();
            pourReceiver.Tick(1f);
            Assert.That(pourReceiver.IsPouring, Is.True);
            Assert.That(pourReceiver.ActiveSource, Is.SameAs(charcoal));
            Assert.That(grill.ContentAmount, Is.EqualTo(12f));
            Assert.That(
                pourReceiver.TryPourFrom(
                    charcoal,
                    1f,
                    out float secondPour),
                Is.True);
            Assert.That(secondPour, Is.EqualTo(12f));
            Assert.That(charcoal.ContentAmount, Is.EqualTo(116f));
            Assert.That(grill.ContentAmount, Is.EqualTo(24f));

            Assert.That(grill.TryToggleOpen(), Is.True);
            Assert.That(grill.IsOpen, Is.True);
            Assert.That(grill.CanInteract(fixture.Context), Is.True);
            Assert.That(grill.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(grill.IsIgnited, Is.True);
            Assert.That(grill.RemainingBurnTime, Is.EqualTo(120f));
            Assert.That(
                pourReceiver.TryPourFrom(charcoal, 1f, out _),
                Is.False,
                "The donor grill cannot be refuelled while it is burning.");

            fixture.Runtime.AdvanceCombustionSimulation(30f);
            Assert.That(grill.ContentAmount, Is.EqualTo(21f).Within(0.0001f));
            Assert.That(grill.RemainingBurnTime, Is.EqualTo(90f).Within(0.0001f));
            ItemInstanceState saved = grill.CaptureState();

            fixture.Runtime.AdvanceCombustionSimulation(400f);
            Assert.That(grill.IsEnabled, Is.False);
            Assert.That(grill.TryToggleOpen(), Is.True);
            Assert.That(grill.IsOpen, Is.False);

            grill.ApplyState(saved);
            Assert.That(grill.ContentAmount, Is.EqualTo(21f).Within(0.0001f));
            Assert.That(grill.RemainingBurnTime, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(grill.IsOpen, Is.True);
            Assert.That(grill.IsIgnited, Is.True);

            fixture.Runtime.AdvanceCombustionSimulation(90f);
            Assert.That(grill.ContentAmount, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(grill.RemainingBurnTime, Is.Zero.Within(0.0001f));
            Assert.That(grill.IsEmbering, Is.True);

            fixture.Runtime.AdvanceCombustionSimulation(175f);
            Assert.That(grill.ContentAmount, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(grill.IsEnabled, Is.False);
            Assert.That(grill.IsIgnited, Is.False);
            Assert.That(grill.IsEmbering, Is.False);
        }

        [Test]
        public void WetPortableGrillCannotIgniteAndExtinguishesActiveFire()
        {
            WorldItemInstance charcoal = fixture.Spawn(
                "P1.ITEM.110",
                "wet-grill-charcoal",
                Vector3.zero);
            WorldItemInstance grill = fixture.Spawn(
                "P1.ITEM.149",
                "wet-portable-grill",
                Vector3.right);
            charcoal.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Assert.That(
                grill.GetComponent<ItemFuelPourReceiver>().TryPourFrom(
                    charcoal,
                    1f,
                    out _),
                Is.True);

            Assert.That(grill.TrySetFlag("wet", true), Is.True);
            Assert.That(grill.TryPerformPrimaryAction(fixture.Context), Is.False);
            Assert.That(grill.TrySetFlag("wet", false), Is.True);
            Assert.That(grill.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(grill.IsIgnited, Is.True);

            Assert.That(grill.TrySetFlag("wet", true), Is.True);
            fixture.Runtime.AdvanceCombustionSimulation(0.02f);
            Assert.That(grill.IsEnabled, Is.False);
            Assert.That(grill.RemainingBurnTime, Is.Zero);
        }

        [Test]
        public void GrillAndKiljuBucketExposeIndependentLidsAndStateDrivenContents()
        {
            const string grillPath =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                "Prefabs/LegacyItemVisual_item_portable-grill.prefab";
            const string bucketPath =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                "Prefabs/LegacyItemVisual_item_kilju-bucket.prefab";
            GameObject grillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                grillPath);
            GameObject bucketPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                bucketPath);
            Assert.That(grillPrefab, Is.Not.Null, grillPath);
            Assert.That(bucketPrefab, Is.Not.Null, bucketPath);

            WorldItemInstance grill = fixture.Spawn(
                "P1.ITEM.149",
                "grill-lid-presentation",
                Vector3.zero);
            WorldItemInstance bucket = fixture.Spawn(
                "P1.ITEM.143",
                "bucket-lid-presentation",
                Vector3.right);
            WorldItemInstance charcoal = fixture.Spawn(
                "P1.ITEM.110",
                "grill-content-presentation",
                Vector3.left);
            GameObject grillVisual = UnityEngine.Object.Instantiate(
                grillPrefab,
                grill.transform,
                false);
            GameObject bucketVisual = UnityEngine.Object.Instantiate(
                bucketPrefab,
                bucket.transform,
                false);

            try
            {
                HingedItemCoverPresentationController grillLid = grillVisual
                    .GetComponentInChildren<
                        HingedItemCoverPresentationController>(true);
                ItemContentsPresentationController grillContents = grillVisual
                    .GetComponentInChildren<
                        ItemContentsPresentationController>(true);
                HingedItemCoverPresentationController bucketLid = bucketVisual
                    .GetComponentInChildren<
                        HingedItemCoverPresentationController>(true);
                ItemContentsPresentationController bucketContents = bucketVisual
                    .GetComponentInChildren<
                        ItemContentsPresentationController>(true);
                Assert.That(grillLid, Is.Not.Null);
                Assert.That(grillContents, Is.Not.Null);
                Assert.That(bucketLid, Is.Not.Null);
                Assert.That(bucketContents, Is.Not.Null);

                grillLid.Bind(grill);
                grillContents.Bind(grill);
                bucketLid.Bind(bucket);
                bucketContents.Bind(bucket);
                Assert.That(
                    grillContents.LiquidSurfaceRenderers.All(value => !value.enabled),
                    Is.True);
                Assert.That(
                    bucketContents.LiquidSurfaceRenderers.All(value => !value.enabled),
                    Is.True,
                    "An empty kilju bucket must not show the donor blue surface.");

                Assert.That(grillLid.CanInteract(fixture.Context), Is.True);
                grillLid.Interact(fixture.Context);
                Assert.That(grill.IsOpen, Is.True);
                Assert.That(grillLid.TargetOpen, Is.True);
                Assert.That(grill.IsEnabled, Is.False);

                Assert.That(bucket.TryPerformPrimaryAction(fixture.Context), Is.False);
                Assert.That(bucketLid.CanInteract(fixture.Context), Is.True);
                bucketLid.Interact(fixture.Context);
                Assert.That(bucket.IsOpen, Is.True);
                Assert.That(bucketLid.TargetOpen, Is.True);

                charcoal.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Assert.That(
                    charcoal.TryTransferFuelTo(grill, 12f, out _),
                    Is.True);
                Assert.That(
                    grillContents.LiquidSurfaceRenderers.All(value => value.enabled),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(grillVisual);
                UnityEngine.Object.DestroyImmediate(bucketVisual);
            }
        }

        [Test]
        public void LitGarbageBarrelScansFullCavityAndBurnsThrownItem()
        {
            WorldItemInstance barrel = fixture.Spawn(
                "P1.ITEM.148",
                "garbage-barrel-volume",
                Vector3.zero);
            WorldItemInstance rubbish = fixture.Spawn(
                "P1.ITEM.103",
                "garbage-barrel-thrown-rubbish",
                new Vector3(0.2f, 0f, 0f));
            Assert.That(barrel.TryPerformPrimaryAction(fixture.Context), Is.True);
            GarbageBarrelBurner burner =
                barrel.GetComponent<GarbageBarrelBurner>();
            Assert.That(burner, Is.Not.Null);

            Physics.SyncTransforms();
            burner.Tick(1.25f);
            Assert.That(rubbish.State.isConsumed, Is.False);
            burner.Tick(1.25f);

            Assert.That(rubbish.State.isConsumed, Is.True);
            Assert.That(rubbish.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ConsumePublishesAuthoredUseEffectsWithoutNeedsDependency()
        {
            WorldItemInstance item = fixture.Spawn(
                "P1.ITEM.116",
                "effect-source",
                Vector3.zero);
            ItemActionCompleted observed = default;
            fixture.Runtime.ActionCompleted += value => observed = value;

            Assert.That(item.TryPerformPrimaryAction(fixture.Context), Is.True);

            Assert.That(observed.Action, Is.EqualTo(ItemActionKind.Used));
            Assert.That(observed.UseEffects.HasAnyEffect, Is.True);
            Assert.That(observed.UseEffects.Hunger, Is.EqualTo(-33.3f));
            Assert.That(observed.UseEffects.Thirst, Is.EqualTo(3f));
            Assert.That(observed.UseEffects.Stress, Is.Zero);
            Assert.That(observed.UseEffects.Weight, Is.EqualTo(0.034f));
            Assert.That(item.State.isConsumed, Is.True);
            Assert.That(item.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SausagePackageDispensesExactlyFourPhysicalSausages()
        {
            WorldItemInstance package = fixture.Spawn(
                "P1.ITEM.101",
                "four-sausage-package",
                Vector3.zero);
            string[] expectedIds = package.State.containedStableIds.ToArray();

            Assert.That(expectedIds, Has.Length.EqualTo(4));
            for (int index = 0; index < expectedIds.Length; index++)
            {
                Assert.That(
                    package.TryPerformPrimaryAction(fixture.Context),
                    Is.True);
                Assert.That(
                    fixture.Runtime.TryGetInstance(
                        expectedIds[index],
                        out WorldItemInstance child),
                    Is.True);
                Assert.That(child.DefinitionId, Is.EqualTo("item.loose-sausage"));
                Assert.That(child.Freshness, Is.EqualTo(package.Freshness));
                Assert.That(
                    package.State.content,
                    Is.EqualTo(3 - index));
            }

            Assert.That(package.State.containedStableIds, Is.Empty);
            Assert.That(
                package.TryPerformPrimaryAction(fixture.Context),
                Is.False);
        }

        [Test]
        public void GameTimeFreshnessUsesAmbientAndRefrigeratedRates()
        {
            WorldItemInstance ambient = fixture.Spawn(
                "P1.ITEM.116",
                "ambient-sausage",
                Vector3.zero);
            WorldItemInstance refrigerated = fixture.Spawn(
                "P1.ITEM.116",
                "refrigerated-sausage",
                Vector3.right * 10f);
            fixture.Runtime.SetFoodEnvironment(
                new PositionFoodEnvironment(position => position.x > 5f));

            fixture.GameTime.Advance(5d);
            double elapsedGameSeconds = fixture.GameTime.CurrentGameTimeSeconds;

            Assert.That(
                ambient.Freshness,
                Is.EqualTo(
                    100f - (float)(elapsedGameSeconds / 60d) * 0.034f)
                    .Within(0.0001f));
            Assert.That(
                refrigerated.Freshness,
                Is.EqualTo(
                    100f - (float)(elapsedGameSeconds / 60d) * 0.0005f)
                    .Within(0.0001f));
            Assert.That(ambient.Freshness, Is.LessThan(refrigerated.Freshness));
        }

        [Test]
        public void HeatContactAdvancesRawCookedAndBurnedSausageStates()
        {
            WorldItemInstance sausage = fixture.Spawn(
                "P1.ITEM.116",
                "cooking-sausage",
                Vector3.zero);
            var heat = new TestHeatSource(
                new Bounds(Vector3.zero, Vector3.one * 2f));
            fixture.Runtime.RegisterHeatSource(heat);

            heat.IsHeating = false;
            fixture.Runtime.AdvanceCookingSimulation(12f);
            Assert.That(sausage.CookingSeconds, Is.Zero);

            heat.IsHeating = true;
            fixture.Runtime.AdvanceCookingSimulation(29.9f);
            Assert.That(sausage.CookState, Is.EqualTo(FoodCookState.Raw));
            fixture.Runtime.AdvanceCookingSimulation(0.1f);
            Assert.That(sausage.CookState, Is.EqualTo(FoodCookState.Cooked));
            fixture.Runtime.AdvanceCookingSimulation(10f);
            Assert.That(sausage.CookState, Is.EqualTo(FoodCookState.Burned));
        }

        [Test]
        public void SausageCountFreshnessAndCookStateRoundTripThroughNativeItems()
        {
            WorldItemInstance package = fixture.Spawn(
                "P1.ITEM.101",
                "saved-sausage-package",
                Vector3.zero);
            fixture.GameTime.Advance(1d);
            string cookedChildId = package.State.containedStableIds[0];
            Assert.That(package.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(fixture.Runtime.TryGetInstance(
                cookedChildId,
                out WorldItemInstance cookedChild), Is.True);

            var heat = new TestHeatSource(
                new Bounds(cookedChild.transform.position, Vector3.one * 2f))
            {
                IsHeating = true,
            };
            fixture.Runtime.RegisterHeatSource(heat);
            fixture.Runtime.AdvanceCookingSimulation(30f);
            float savedPackageFreshness = package.Freshness;
            float savedChildFreshness = cookedChild.Freshness;
            string[] savedContainedIds =
                package.State.containedStableIds.ToArray();

            var deferred = new DeferredStableEntityStore();
            var participant = new ItemSaveParticipant(fixture.Runtime, deferred);
            string payload = participant.CapturePayload();

            string extraChildId = package.State.containedStableIds[0];
            Assert.That(package.TryPerformPrimaryAction(fixture.Context), Is.True);
            ItemInstanceState corrupted = cookedChild.CaptureState();
            corrupted.condition = 1f;
            corrupted.cookingSeconds = 40f;
            cookedChild.ApplyState(corrupted);

            var envelope = new SaveDomainEnvelope
            {
                DomainId = ItemSaveParticipant.DomainId,
                SchemaVersion = ItemDomainSaveDto.CurrentSchemaVersion,
                Required = true,
                PayloadJson = payload,
            };
            var unresolved = new UnresolvedContentReport();
            var preparation = new SaveRestorePreparationContext(
                unresolved,
                deferred);
            participant.ApplyPreparedRestore(
                participant.PrepareRestore(envelope, preparation),
                new SaveRestoreContext(unresolved, deferred));

            Assert.That(package.State.content, Is.EqualTo(3f));
            Assert.That(
                package.State.containedStableIds,
                Is.EqualTo(savedContainedIds));
            Assert.That(
                package.Freshness,
                Is.EqualTo(savedPackageFreshness).Within(0.0001f));
            Assert.That(fixture.Runtime.TryGetInstance(
                cookedChildId,
                out WorldItemInstance restoredChild), Is.True);
            Assert.That(restoredChild.CookState, Is.EqualTo(FoodCookState.Cooked));
            Assert.That(restoredChild.CookingSeconds, Is.EqualTo(30f));
            Assert.That(
                restoredChild.Freshness,
                Is.EqualTo(savedChildFreshness).Within(0.0001f));
            Assert.That(fixture.Runtime.TryGetInstance(extraChildId, out _), Is.False);
        }

        [TestCase(0f, 100f, -33.3f, 3f, 0f)]
        [TestCase(30f, 100f, -42f, 4f, -15f)]
        [TestCase(40f, 100f, -5f, 12f, 10f)]
        [TestCase(30f, 0f, 12f, 18f, 25f)]
        public void SausageFoodStateSelectsAuthoredConsumptionConsequences(
            float cookingSeconds,
            float freshness,
            float expectedHunger,
            float expectedThirst,
            float expectedStress)
        {
            WorldItemInstance sausage = fixture.Spawn(
                "P1.ITEM.116",
                $"food-effects-{cookingSeconds}-{freshness}",
                Vector3.zero);
            ItemInstanceState state = sausage.CaptureState();
            state.cookingSeconds = cookingSeconds;
            state.condition = freshness;
            sausage.ApplyState(state);
            ItemActionCompleted observed = default;
            fixture.Runtime.ActionCompleted += value =>
            {
                if (value.StableId == sausage.StableId &&
                    value.Action == ItemActionKind.Used)
                {
                    observed = value;
                }
            };

            Assert.That(sausage.TryPerformPrimaryAction(fixture.Context), Is.True);

            Assert.That(observed.UseEffects.Hunger, Is.EqualTo(expectedHunger));
            Assert.That(observed.UseEffects.Thirst, Is.EqualTo(expectedThirst));
            Assert.That(observed.UseEffects.Stress, Is.EqualTo(expectedStress));
        }

        [Test]
        public void HeldBeerConsumesOnlyWhileContinuedAndRetainsPhysicalBottle()
        {
            WorldItemInstance bottle = fixture.Spawn(
                "P1.ITEM.114",
                "held-continuous-beer",
                Vector3.zero);
            var player = new GameObject("Continuous drink carry fixture");
            BoxCollider playerCollider = player.AddComponent<BoxCollider>();
            var anchor = new GameObject("Carry anchor");
            anchor.transform.SetParent(player.transform, false);
            anchor.transform.localPosition = Vector3.forward;
            PhysicalCarryController carry =
                player.AddComponent<PhysicalCarryController>();
            carry.Configure(anchor.transform, playerCollider);
            var context = new InteractionContext(
                player,
                player.transform.position,
                player.transform.forward);
            int startedCount = 0;
            int endedCount = 0;
            float observedThirst = 0f;
            float observedIntoxication = 0f;
            fixture.Runtime.HeldUseStateChanged += value =>
            {
                if (value.Phase == ItemHeldUsePhase.Started)
                {
                    startedCount++;
                }
                else
                {
                    endedCount++;
                }
            };
            fixture.Runtime.ActionCompleted += value =>
            {
                if (value.Action != ItemActionKind.Used ||
                    value.StableId != bottle.StableId)
                {
                    return;
                }

                observedThirst += value.UseEffects.Thirst;
                observedIntoxication += value.UseEffects.Intoxication;
            };

            try
            {
                Assert.That(
                    carry.TryPickup(
                        bottle.GetComponent<PhysicsPickupTarget>(),
                        context),
                    Is.True);
                Assert.That(carry.TryActivateHeld(context), Is.False);
                Assert.That(
                    carry.TryPrepareHeldContinuousActivation(context),
                    Is.True);
                Assert.That(carry.IsHeldUseReady, Is.True);
                Assert.That(
                    carry.TryBeginContinuousHeldActivation(context),
                    Is.True);
                Assert.That(
                    carry.ContinueContinuousHeldActivation(0.5f),
                    Is.True);
                Assert.That(
                    bottle.State.content,
                    Is.EqualTo(0.301875f).Within(0.0001f));
                Assert.That(carry.HasHeldObject, Is.True);

                carry.EndContinuousHeldActivation();

                Assert.That(carry.HasHeldObject, Is.True);
                float contentAfterRelease = bottle.State.content;
                Assert.That(
                    carry.ContinueContinuousHeldActivation(0.5f),
                    Is.False);
                Assert.That(
                    bottle.State.content,
                    Is.EqualTo(contentAfterRelease).Within(0.0001f));
                Assert.That(startedCount, Is.EqualTo(1));
                Assert.That(endedCount, Is.EqualTo(1));
                Assert.That(
                    observedThirst,
                    Is.EqualTo(-1.704545f).Within(0.001f));
                Assert.That(
                    observedIntoxication,
                    Is.EqualTo(0.681818f).Within(0.001f));

                Assert.That(
                    carry.TryBeginContinuousHeldActivation(context),
                    Is.True);
                Assert.That(
                    carry.ContinueContinuousHeldActivation(6f),
                    Is.False);

                Assert.That(bottle.State.content, Is.Zero);
                Assert.That(bottle.State.liquidId, Is.Empty);
                Assert.That(carry.HasHeldObject, Is.True);
                Assert.That(startedCount, Is.EqualTo(2));
                Assert.That(endedCount, Is.EqualTo(2));
                Assert.That(observedThirst, Is.EqualTo(-20f).Within(0.001f));
                Assert.That(
                    observedIntoxication,
                    Is.EqualTo(8f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void CaptureMidSipFlushesMatchingDoseBeforeStateSnapshot()
        {
            WorldItemInstance bottle = fixture.Spawn(
                "P1.ITEM.114",
                "mid-sip-save-boundary",
                Vector3.zero);
            float observedThirst = 0f;
            fixture.Runtime.ActionCompleted += value =>
            {
                if (value.Action == ItemActionKind.Used &&
                    value.StableId == bottle.StableId)
                {
                    observedThirst += value.UseEffects.Thirst;
                }
            };

            Assert.That(
                bottle.CanBeginContinuousHeldActivation(fixture.Context),
                Is.True);
            bottle.BeginContinuousHeldActivation(fixture.Context);
            Assert.That(
                bottle.ContinueContinuousHeldActivation(0.05f),
                Is.True);
            Assert.That(observedThirst, Is.Zero);

            ItemInstanceState captured = bottle.CaptureState();

            Assert.That(
                captured.content,
                Is.EqualTo(0.3271875f).Within(0.0001f));
            Assert.That(
                observedThirst,
                Is.EqualTo(-0.1704545f).Within(0.001f));
            Assert.That(
                bottle.IsContinuousHeldActivationActive,
                Is.True);
            bottle.EndContinuousHeldActivation();
        }

        [Test]
        public void SpannerCaseTogglesOpenInsteadOfActingAsMagicWrench()
        {
            WorldItemInstance spanner = fixture.Spawn(
                "P1.ITEM.138",
                "spanner",
                Vector3.zero);
            IHeldToolIdentity identity = spanner;

            Assert.That(identity.ToolType, Is.Empty);
            Assert.That(spanner.State.isOpen, Is.False);
            Assert.That(spanner.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(spanner.State.isOpen, Is.True);
            Assert.That(spanner.State.variantIndex, Is.Zero);
        }

        [Test]
        public void PackageDispensesDeterministicPhysicalChild()
        {
            WorldItemInstance package = fixture.Spawn(
                "P1.ITEM.126",
                "spark-plug-package",
                Vector3.zero);
            string expectedChildId = package.State.containedStableIds[0];

            Assert.That(package.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(package.State.content, Is.EqualTo(3f));
            Assert.That(package.State.containedStableIds, Has.Length.EqualTo(3));
            Assert.That(fixture.Runtime.TryGetInstance(
                expectedChildId,
                out WorldItemInstance child), Is.True);
            Assert.That(child.DefinitionId, Is.EqualTo("item.spark-plug"));
            Assert.That(child.StableId.Value, Is.EqualTo(expectedChildId));
        }

        [Test]
        public void PackageDoesNotMutateWhenContainedIdentityAlreadyExistsInWorld()
        {
            WorldItemInstance package = fixture.Spawn(
                "P1.ITEM.126",
                "duplicate-child-package",
                Vector3.zero);
            ItemInstanceState before = package.CaptureState();
            fixture.SpawnWithStableId(
                "P1.ITEM.126.child",
                before.containedStableIds[0],
                Vector3.right,
                string.Empty);

            Assert.That(package.TryPerformPrimaryAction(fixture.Context), Is.False);
            Assert.That(package.State.content, Is.EqualTo(before.content));
            Assert.That(
                package.State.containedStableIds,
                Is.EqualTo(before.containedStableIds));
        }

        [Test]
        public void CriticalItemRecoversToRegisteredPoseWithoutPerItemUpdate()
        {
            Vector3 recoveryPosition = new Vector3(3f, 2f, 5f);
            WorldItemInstance item = fixture.Spawn(
                "P1.ITEM.133",
                "recoverable-gas-can",
                recoveryPosition);
            Rigidbody body = item.GetComponent<Rigidbody>();
            body.position = new Vector3(50f, -100f, 70f);
            body.linearVelocity = Vector3.one * 10f;
            body.angularVelocity = Vector3.one * 4f;
            ItemActionCompleted observed = default;
            fixture.Runtime.ActionCompleted += value => observed = value;

            MethodInfo fixedUpdate = typeof(ItemWorldRuntime).GetMethod(
                "FixedUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fixedUpdate, Is.Not.Null);
            fixedUpdate.Invoke(fixture.Runtime, null);

            Assert.That(body.position, Is.EqualTo(recoveryPosition));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(observed.Action, Is.EqualTo(ItemActionKind.Recovered));
            Assert.That(observed.StableId, Is.EqualTo(item.StableId));
        }

        [Test]
        public void DynamicItemCanBeSettledExactlyOnHorizontalServiceSurface()
        {
            WorldItemInstance item = fixture.Spawn(
                "P1.ITEM.151",
                "settled-coffee-cup",
                new Vector3(0f, 10f, 0f));
            const float surfaceY = 2.25f;
            const float clearance = 0.005f;

            Assert.That(
                fixture.Runtime.TrySettleDynamicOnHorizontalSurface(
                    item,
                    surfaceY,
                    clearance),
                Is.True);

            Collider collider = item.GetComponent<Collider>();
            Rigidbody body = item.GetComponent<Rigidbody>();
            Assert.That(
                collider.bounds.min.y,
                Is.EqualTo(surfaceY + clearance).Within(0.0002f));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.IsSleeping(), Is.True);
        }

        [Test]
        public void ItemSaveParticipantRestoresRemovedDynamicInstanceAndLogicalState()
        {
            WorldItemInstance original = fixture.Spawn(
                "P1.ITEM.138",
                "saved-spanner",
                new Vector3(4f, 1f, -2f),
                "cell_9_9");
            Assert.That(original.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(original.State.isOpen, Is.True);

            var deferred = new DeferredStableEntityStore();
            var participant = new ItemSaveParticipant(fixture.Runtime, deferred);
            string payload = participant.CapturePayload();
            string stableId = original.StableId.Value;
            UnityEngine.Object.DestroyImmediate(original.gameObject);
            Assert.That(fixture.Runtime.TryGetInstance(stableId, out _), Is.False);

            var envelope = new SaveDomainEnvelope
            {
                DomainId = ItemSaveParticipant.DomainId,
                SchemaVersion = ItemDomainSaveDto.CurrentSchemaVersion,
                Required = true,
                PayloadJson = payload,
            };
            var unresolved = new UnresolvedContentReport();
            object prepared = participant.PrepareRestore(
                envelope,
                new SaveRestorePreparationContext(unresolved, deferred));
            participant.ApplyPreparedRestore(
                prepared,
                new SaveRestoreContext(unresolved, deferred));

            Assert.That(fixture.Runtime.TryGetInstance(
                stableId,
                out WorldItemInstance restored), Is.True);
            Assert.That(restored.State.isOpen, Is.True);
            Assert.That(
                fixture.Runtime.TryGetSourceCellId(stableId, out string sourceCellId),
                Is.True);
            Assert.That(sourceCellId, Is.EqualTo("cell_9_9"));
            Assert.That(participant.Descriptor.RestorePhase,
                Is.EqualTo(SaveRestorePhase.ItemInstances));
        }

        [Test]
        public void LoadingOlderStateRemovesDynamicChildrenAbsentFromSave()
        {
            WorldItemInstance package = fixture.Spawn(
                "P1.ITEM.126",
                "package-before-child",
                Vector3.zero);
            var deferred = new DeferredStableEntityStore();
            var participant = new ItemSaveParticipant(fixture.Runtime, deferred);
            string payloadBeforeDispense = participant.CapturePayload();
            string firstChildId = package.State.containedStableIds[0];

            Assert.That(package.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(fixture.Runtime.TryGetInstance(firstChildId, out _), Is.True);

            var envelope = new SaveDomainEnvelope
            {
                DomainId = ItemSaveParticipant.DomainId,
                SchemaVersion = ItemDomainSaveDto.CurrentSchemaVersion,
                Required = true,
                PayloadJson = payloadBeforeDispense,
            };
            var unresolved = new UnresolvedContentReport();
            object prepared = participant.PrepareRestore(
                envelope,
                new SaveRestorePreparationContext(unresolved, deferred));
            participant.ApplyPreparedRestore(
                prepared,
                new SaveRestoreContext(unresolved, deferred));

            Assert.That(fixture.Runtime.TryGetInstance(firstChildId, out _), Is.False);
            Assert.That(package.State.containedStableIds[0], Is.EqualTo(firstChildId));
            Assert.That(package.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(fixture.Runtime.TryGetInstance(firstChildId, out _), Is.True);
        }

        [Test]
        public void DeferredDynamicSceneStateRematerializesWithOwningCell()
        {
            WorldItemInstance original = fixture.Spawn(
                "P1.ITEM.138",
                "streamed-spanner",
                Vector3.zero,
                "cell_2_3");
            Assert.That(original.TryPerformPrimaryAction(fixture.Context), Is.True);
            string stableId = original.StableId.Value;

            var deferred = new DeferredStableEntityStore();
            var participant = new ItemSaveParticipant(fixture.Runtime, deferred);
            participant.CaptureScene(SceneManager.GetActiveScene());
            Assert.That(deferred.TryPeek(
                ItemSaveParticipant.DomainId,
                stableId,
                out _), Is.True);

            UnityEngine.Object.DestroyImmediate(original.gameObject);
            Assert.That(fixture.Runtime.TryGetInstance(stableId, out _), Is.False);

            participant.RegisterSceneForCell(
                SceneManager.GetActiveScene(),
                "cell_2_3");

            Assert.That(fixture.Runtime.TryGetInstance(
                stableId,
                out WorldItemInstance returned), Is.True);
            Assert.That(returned.State.isOpen, Is.True);
            Assert.That(deferred.TryPeek(
                ItemSaveParticipant.DomainId,
                stableId,
                out _), Is.False);
        }

        [Test]
        public void FreshRestoreArchivesDynamicItemUntilItsUnloadedCellReturns()
        {
            var deferred = new DeferredStableEntityStore();
            var world = new WorldEntitySaveParticipant(
                deferred,
                fixture.Streaming,
                SceneManager.GetActiveScene());
            void Register(WorldItemInstance instance) =>
                world.RegisterTarget(instance.GetComponent<PhysicsPickupTarget>());
            void Unregister(WorldItemInstance instance) =>
                world.UnregisterTarget(instance.GetComponent<PhysicsPickupTarget>());
            fixture.Runtime.InstanceMaterialized += Register;
            fixture.Runtime.InstanceRemoved += Unregister;
            var items = new ItemSaveParticipant(fixture.Runtime, deferred);
            WorldItemInstance original = fixture.Spawn(
                "P1.ITEM.138",
                "fresh-unloaded-cell",
                new Vector3(5122f, 1f, 5122f),
                RuntimeFixture.UnloadedCellId);
            Assert.That(original.TryPerformPrimaryAction(fixture.Context), Is.True);
            string stableId = original.StableId.Value;
            string itemPayload = items.CapturePayload();
            string worldPayload = world.CapturePayload();
            UnityEngine.Object.DestroyImmediate(original.gameObject);

            try
            {
                var unresolved = new UnresolvedContentReport();
                var preparation = new SaveRestorePreparationContext(
                    unresolved,
                    deferred);
                var restore = new SaveRestoreContext(unresolved, deferred);
                var itemEnvelope = new SaveDomainEnvelope
                {
                    DomainId = ItemSaveParticipant.DomainId,
                    SchemaVersion = ItemDomainSaveDto.CurrentSchemaVersion,
                    Required = true,
                    PayloadJson = itemPayload,
                };
                var worldEnvelope = new SaveDomainEnvelope
                {
                    DomainId = WorldEntitySaveParticipant.DomainId,
                    SchemaVersion = WorldEntityDomainSaveDto.CurrentSchemaVersion,
                    Required = true,
                    PayloadJson = worldPayload,
                };

                items.ApplyPreparedRestore(
                    items.PrepareRestore(itemEnvelope, preparation),
                    restore);
                world.ApplyPreparedRestore(
                    world.PrepareRestore(worldEnvelope, preparation),
                    restore);

                Assert.That(fixture.Runtime.TryGetInstance(stableId, out _), Is.False);
                Assert.That(deferred.TryPeek(
                    ItemSaveParticipant.DomainId,
                    stableId,
                    out _), Is.True);
                Assert.That(deferred.TryPeek(
                    WorldEntitySaveParticipant.DomainId,
                    stableId,
                    out _), Is.True);

                items.RegisterSceneForCell(
                    SceneManager.GetActiveScene(),
                    RuntimeFixture.UnloadedCellId);

                Assert.That(fixture.Runtime.TryGetInstance(
                    stableId,
                    out WorldItemInstance restored), Is.True);
                Assert.That(restored.State.isOpen, Is.True);
                Assert.That(restored.GetComponent<Rigidbody>().position,
                    Is.EqualTo(new Vector3(5122f, 1f, 5122f)));
                Assert.That(deferred.TryPeek(
                    ItemSaveParticipant.DomainId,
                    stableId,
                    out _), Is.False);
                Assert.That(deferred.TryPeek(
                    WorldEntitySaveParticipant.DomainId,
                    stableId,
                    out _), Is.False);
            }
            finally
            {
                fixture.Runtime.InstanceMaterialized -= Register;
                fixture.Runtime.InstanceRemoved -= Unregister;
            }
        }

        [Test]
        public void RollbackKeepsOriginallyDeferredDynamicItemArchived()
        {
            WorldItemInstance original = fixture.Spawn(
                "P1.ITEM.138",
                "rollback-deferred-cell",
                new Vector3(5122f, 1f, 5122f),
                RuntimeFixture.UnloadedCellId);
            string stableId = original.StableId.Value;
            var deferred = new DeferredStableEntityStore();
            var participant = new ItemSaveParticipant(fixture.Runtime, deferred);
            participant.CaptureScene(SceneManager.GetActiveScene());
            UnityEngine.Object.DestroyImmediate(original.gameObject);
            object checkpoint = participant.CaptureCheckpoint();

            participant.Rollback(checkpoint);

            Assert.That(fixture.Runtime.TryGetInstance(stableId, out _), Is.False);
            Assert.That(deferred.TryPeek(
                ItemSaveParticipant.DomainId,
                stableId,
                out _), Is.True);
        }

        [Test]
        public void MaterializedDynamicItemCanJoinAndLeaveWorldEntityRegistry()
        {
            var deferred = new DeferredStableEntityStore();
            var world = new WorldEntitySaveParticipant(deferred);
            void Register(WorldItemInstance instance) =>
                world.RegisterTarget(instance.GetComponent<PhysicsPickupTarget>());
            void Unregister(WorldItemInstance instance) =>
                world.UnregisterTarget(instance.GetComponent<PhysicsPickupTarget>());
            fixture.Runtime.InstanceMaterialized += Register;
            fixture.Runtime.InstanceRemoved += Unregister;

            WorldItemInstance item = fixture.Spawn(
                "P1.ITEM.138",
                "world-registry",
                Vector3.zero);
            string stableId = item.StableId.Value;
            try
            {
                Assert.That(world.TryResolve(stableId, out PhysicsPickupTarget target),
                    Is.True);
                Assert.That(target, Is.SameAs(item.GetComponent<PhysicsPickupTarget>()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(item.gameObject);
                fixture.Runtime.InstanceMaterialized -= Register;
                fixture.Runtime.InstanceRemoved -= Unregister;
            }

            Assert.That(world.TryResolve(stableId, out _), Is.False);
        }

        [Test]
        public void RestoringPreConsumedStateReactivatesDisposablePresentation()
        {
            WorldItemInstance item = fixture.Spawn(
                "P1.ITEM.102",
                "reactivated-consumable",
                Vector3.zero);
            ItemInstanceState beforeConsumption = item.CaptureState();
            FieldInfo retainWhenEmpty = typeof(ItemDefinitionRecord).GetField(
                "retainWhenEmpty",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(retainWhenEmpty, Is.Not.Null);
            bool authoredValue = (bool)retainWhenEmpty.GetValue(item.Definition);
            retainWhenEmpty.SetValue(item.Definition, false);

            try
            {
                Assert.That(item.TryPerformPrimaryAction(fixture.Context), Is.True);

                Assert.That(item.State.isConsumed, Is.True);
                Assert.That(item.gameObject.activeSelf, Is.False);

                item.ApplyState(beforeConsumption);

                Assert.That(item.State.isConsumed, Is.False);
                Assert.That(item.gameObject.activeSelf, Is.True);
            }
            finally
            {
                retainWhenEmpty.SetValue(item.Definition, authoredValue);
                item.gameObject.SetActive(true);
            }
        }

        private sealed class RuntimeFixture : IDisposable
        {
            public const string UnloadedCellId = "test.cell.10.10";

            private readonly ItemDefinitionCatalog definitions;
            private readonly ItemPlacementCatalog placements;
            private readonly ProductionWorldStreamingManifest manifest;
            private readonly FieldInfo presentationProviderField;
            private readonly TestPresentationProvider presentationProvider;
            private readonly GameObject serviceObject;
            private readonly GameObject runtimeObject;

            public RuntimeFixture()
            {
                definitions = AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                    DefinitionCatalogPath);
                Assert.That(definitions, Is.Not.Null, DefinitionCatalogPath);

                presentationProviderField = typeof(ItemPresentationProviderHub)
                    .GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(presentationProviderField, Is.Not.Null);
                presentationProvider = new TestPresentationProvider();
                presentationProviderField.SetValue(null, presentationProvider);

                placements = ScriptableObject.CreateInstance<ItemPlacementCatalog>();
                placements.ConfigureForAuthoring(
                    "test.items.placements",
                    "msc-world-baseline-04a1.1-c3f2f337",
                    Array.Empty<ItemPlacementRecord>());
                manifest =
                    ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
                manifest.ConfigureForAuthoring(
                    512f,
                    0,
                    1,
                    new[]
                    {
                        new ProductionWorldCellScene(
                            UnloadedCellId,
                            10,
                            10,
                            999,
                            "Assets/Tests/Scenes/UnloadedItemCell.unity"),
                    });
                serviceObject = new GameObject("Item test streaming service");
                Streaming =
                    serviceObject.AddComponent<ProductionWorldStreamingService>();
                Streaming.ConfigureForAuthoring(manifest);
                runtimeObject = new GameObject("Item world runtime fixture");
                Runtime = runtimeObject.AddComponent<ItemWorldRuntime>();
                GameTime = new GameTimeService();
                Runtime.Initialize(
                    definitions,
                    placements,
                    Streaming,
                    SceneManager.GetActiveScene(),
                    -10f,
                    GameTime);
                Context = new InteractionContext(
                    runtimeObject,
                    Vector3.zero,
                    Vector3.forward);
            }

            public ItemWorldRuntime Runtime { get; }

            public GameTimeService GameTime { get; }

            public ProductionWorldStreamingService Streaming { get; }

            public InteractionContext Context { get; }

            public WorldItemInstance Spawn(
                string featureId,
                string key,
                Vector3 position,
                string sourceCellId = "",
                int variantIndex = 0)
            {
                ItemDefinitionRecord definition =
                    definitions.Definitions.Single(value =>
                        string.Equals(
                            value.FeatureId,
                            featureId,
                            StringComparison.Ordinal));
                return Runtime.SpawnDynamic(
                    definition.DefinitionId,
                    ItemStableIdUtility.CreateDeterministic("test." + key),
                    position,
                    Quaternion.identity,
                    SceneManager.GetActiveScene(),
                    sourceCellId,
                    variantIndex);
            }

            public WorldItemInstance SpawnWithStableId(
                string featureId,
                string stableId,
                Vector3 position,
                string sourceCellId)
            {
                ItemDefinitionRecord definition =
                    definitions.Definitions.Single(value =>
                        string.Equals(
                            value.FeatureId,
                            featureId,
                            StringComparison.Ordinal));
                Assert.That(StableEntityId.TryParse(
                    stableId,
                    out StableEntityId parsed), Is.True);
                return Runtime.SpawnDynamic(
                    definition.DefinitionId,
                    parsed,
                    position,
                    Quaternion.identity,
                    SceneManager.GetActiveScene(),
                    sourceCellId);
            }

            public void Dispose()
            {
                WorldItemInstance[] instances = Runtime != null
                    ? Runtime.LoadedInstances.Where(value => value != null).ToArray()
                    : Array.Empty<WorldItemInstance>();
                for (int index = 0; index < instances.Length; index++)
                {
                    UnityEngine.Object.DestroyImmediate(instances[index].gameObject);
                }

                if (runtimeObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(runtimeObject);
                }

                if (serviceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(serviceObject);
                }

                if (manifest != null)
                {
                    UnityEngine.Object.DestroyImmediate(manifest);
                }

                if (placements != null)
                {
                    UnityEngine.Object.DestroyImmediate(placements);
                }

                presentationProviderField?.SetValue(null, null);
            }
        }

        private sealed class TestPresentationProvider : IItemPresentationProvider
        {
            public bool TryInstantiate(
                ItemDefinitionRecord definition,
                Transform parent,
                out GameObject visualRoot)
            {
                visualRoot = new GameObject("In-memory item test visual");
                visualRoot.transform.SetParent(parent, false);
                return true;
            }
        }

        private sealed class PositionFoodEnvironment : IItemFoodEnvironment
        {
            private readonly Func<Vector3, bool> predicate;

            public PositionFoodEnvironment(Func<Vector3, bool> configuredPredicate)
            {
                predicate = configuredPredicate;
            }

            public bool IsRefrigerated(WorldItemInstance item) =>
                item != null && predicate(item.transform.position);

            public bool IsRefrigerated(Vector3 worldPosition) =>
                predicate(worldPosition);
        }

        private sealed class TestHeatSource : IItemHeatSource
        {
            public TestHeatSource(Bounds bounds)
            {
                WorldBounds = bounds;
            }

            public bool IsHeating { get; set; }
            public float CookingRate => 1f;
            public Bounds WorldBounds { get; }
        }
    }
}
