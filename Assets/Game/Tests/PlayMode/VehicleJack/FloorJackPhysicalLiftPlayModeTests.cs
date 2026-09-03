using System;
using System.Collections;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Items;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.ItemsIntegration;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehicleJack
{
    public sealed class FloorJackPhysicalLiftPlayModeTests
    {
        private const string DefinitionCatalogPath =
            "Assets/Game/Items/Content/Definitions/" +
            "Phase1ItemDefinitionCatalog.asset";
        private const float DonorMaximumLiftMeters = 0.38f;
        private const float DonorLiftStepMeters = 0.015f;
        private const float LiftHeadBaseLocalY = -0.07f;
        private const float LiftHeadLocalZ = -0.248f;
        private const float FixtureGroundY = 100f;
        private const float JackRootGroundOffset = 0.115f;
        private const float DonorPumpPeakDegrees = 40.18f;
        private const float ReviewedHandlingMassKilograms = 30f;

        [UnityTest]
        public IEnumerator PhysicalFloorJackPump_AnimatesLeverOutAndBack_WhileHeadAdvancesOneStep()
        {
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            FloorJackFixture fixture = null;
            try
            {
                fixture = CreateFixture();
                yield return WaitForFixedSteps(8);

                Quaternion restRotation = fixture.PumpLever.localRotation;
                var context = new InteractionContext(
                    fixture.Interactor,
                    fixture.Controller.LiftHead.position,
                    Vector3.up);

                Assert.That(
                    fixture.Controller.PumpLever,
                    Is.SameAs(fixture.PumpLever));
                Assert.That(fixture.Controller.IsPumpLeverAnimating, Is.False);
                Assert.That(fixture.Controller.CanInteract(context), Is.True);

                fixture.Controller.Interact(context);

                Assert.That(fixture.Controller.IsPumpLeverAnimating, Is.True);
                Assert.That(
                    fixture.Controller.TargetLiftHeightMeters,
                    Is.EqualTo(DonorLiftStepMeters).Within(0.0001f));

                float maximumExcursionDegrees = 0f;
                float maximumLocalZDegrees = 0f;
                float deadline = Time.time + 1.5f;
                while (fixture.Controller.IsPumpLeverAnimating &&
                       Time.time < deadline)
                {
                    Quaternion localDelta = Quaternion.Inverse(restRotation) *
                        fixture.PumpLever.localRotation;
                    maximumExcursionDegrees = Mathf.Max(
                        maximumExcursionDegrees,
                        Quaternion.Angle(Quaternion.identity, localDelta));
                    maximumLocalZDegrees = Mathf.Max(
                        maximumLocalZDegrees,
                        Mathf.DeltaAngle(0f, localDelta.eulerAngles.z));
                    yield return null;
                }

                maximumExcursionDegrees = Mathf.Max(
                    maximumExcursionDegrees,
                    Quaternion.Angle(
                        restRotation,
                        fixture.PumpLever.localRotation));
                Assert.That(
                    fixture.Controller.IsPumpLeverAnimating,
                    Is.False,
                    "The donor pump-lever transient did not finish.");
                Assert.That(
                    maximumExcursionDegrees,
                    Is.GreaterThan(DonorPumpPeakDegrees - 8f),
                    "The pump command raised the head without visibly " +
                    "swinging the authored lever toward its donor peak.");
                Assert.That(
                    maximumExcursionDegrees,
                    Is.LessThanOrEqualTo(DonorPumpPeakDegrees + 1f),
                    "The transient pump lever overshot its donor peak angle.");
                Assert.That(
                    maximumLocalZDegrees,
                    Is.GreaterThan(DonorPumpPeakDegrees - 8f),
                    "The donor pump stroke was not applied around the " +
                    "lever's authored local Z axis.");
                Assert.That(
                    Quaternion.Angle(
                        restRotation,
                        fixture.PumpLever.localRotation),
                    Is.LessThan(0.1f),
                    "The pump lever did not return to its authored rest pose.");

                yield return WaitForFixedSteps(2);
                Assert.That(
                    fixture.Controller.LiftHeightMeters,
                    Is.EqualTo(DonorLiftStepMeters).Within(0.001f),
                    "Lever presentation completed, but the physical lift " +
                    "head did not complete the same pump step.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                fixture?.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PhysicalFloorJackHead_LiftsSatsumaThroughInstalledSubframe()
        {
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 10f;
            FloorJackFixture fixture = null;
            GameObject satsuma = null;
            try
            {
                fixture = CreateFixture();
                fixture.LoadBody.position += Vector3.right * 2f;
                yield return WaitForFixedSteps(4);

                GameObject prefab = Resources.Load<GameObject>(
                    "Phase1Vehicles/Satsuma_Phase1_V1a");
                Assert.That(prefab, Is.Not.Null);
                satsuma = UnityEngine.Object.Instantiate(
                    prefab,
                    fixture.Controller.LiftHead.position + Vector3.up,
                    Quaternion.identity);
                VehicleAssemblyController assembly = satsuma
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = satsuma.GetComponent<Rigidbody>();
                chassis.constraints = RigidbodyConstraints.FreezeRotation;
                PartInstance subframe = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.sub-frame");
                MountPointAuthoring subframeMount = assembly.MountPoints
                    .Single(value =>
                        value.MountId == "mount.satsuma.sub-frame");
                subframe.transform.SetPositionAndRotation(
                    subframeMount.Pose.position,
                    subframeMount.Pose.rotation);
                subframe.Body.position = subframeMount.Pose.position;
                subframe.Body.rotation = subframeMount.Pose.rotation;
                AssemblyOperationResult install = assembly.TryInstall(
                    subframe,
                    subframeMount);
                Assert.That(install.Succeeded, Is.True, install.Message);
                yield return new WaitForFixedUpdate();

                Collider subframeCollider = subframe
                    .GetComponentsInChildren<Collider>(true)
                    .FirstOrDefault(value =>
                        value != null && value.enabled && !value.isTrigger &&
                        value.attachedRigidbody == subframe.Body);
                Assert.That(subframeCollider, Is.Not.Null);
                Assert.That(subframe.UsesDynamicInstalledPhysics, Is.True);
                Assert.That(
                    subframe.GetComponent<ConfigurableJoint>().connectedBody,
                    Is.SameAs(chassis));

                Bounds headBounds = fixture.Controller.LiftHeadCollider.bounds;
                Bounds subframeBounds = subframeCollider.bounds;
                Vector3 alignment = new Vector3(
                    headBounds.center.x - subframeBounds.center.x,
                    headBounds.max.y + 0.003f - subframeBounds.min.y,
                    headBounds.center.z - subframeBounds.center.z);
                Vector3 chassisPosition = chassis.position;
                Vector3 subframePosition = subframe.Body.position;
                chassis.position = chassisPosition + alignment;
                subframe.Body.position = subframePosition + alignment;
                chassis.linearVelocity = Vector3.zero;
                chassis.angularVelocity = Vector3.zero;
                subframe.Body.linearVelocity = Vector3.zero;
                subframe.Body.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();
                yield return WaitForFixedSteps(8);

                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.Controller.LiftHeadCollider,
                        subframeCollider),
                    Is.False,
                    "The jack saddle ignored the installed subframe instead " +
                    "of using it as a structural lift path.");
                float startY = chassis.position.y;
                var context = new InteractionContext(
                    fixture.Interactor,
                    fixture.Controller.LiftHead.position,
                    Vector3.up);
                const int pumpCount = 14;
                for (int pump = 0; pump < pumpCount; pump++)
                {
                    float expectedTarget = Mathf.Min(
                        DonorMaximumLiftMeters,
                        (pump + 1) * DonorLiftStepMeters);
                    Assert.That(
                        fixture.Controller.CanInteract(context),
                        Is.True,
                        $"Subframe lift pump {pump + 1} was rejected.");
                    fixture.Controller.Interact(context);
                    yield return WaitForCondition(
                        () => Mathf.Abs(
                            fixture.Controller.LiftHeightMeters -
                            expectedTarget) < 0.0006f &&
                            fixture.Controller.CanInteract(context),
                        2f,
                        $"Subframe lift pump {pump + 1} did not finish.");
                }

                yield return WaitForFixedSteps(6);
                Assert.That(
                    chassis.position.y,
                    Is.GreaterThan(startY + 0.08f),
                    "The saddle physically raised the subframe, but the fixed " +
                    "structural link failed to lift the Satsuma shell.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                if (satsuma != null)
                {
                    UnityEngine.Object.DestroyImmediate(satsuma);
                }

                fixture?.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PhysicalFloorJackHead_LiftsArbitraryDynamicLoad_AndLowers()
        {
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 10f;
            FloorJackFixture fixture = null;
            try
            {
                fixture = CreateFixture();
                yield return WaitForFixedSteps(8);

                Assert.That(
                    fixture.Controller.GetComponentInChildren<
                        VehicleJackLiftPoint>(true),
                    Is.Null,
                    "The physical floor-jack path must not require an " +
                    "authored Satsuma lift-point trigger.");
                Assert.That(fixture.JackBody.isKinematic, Is.False);
                Assert.That(
                    fixture.JackBody.mass,
                    Is.EqualTo(ReviewedHandlingMassKilograms).Within(0.001f),
                    "The donor scene's stability-only mass must not leak into " +
                    "the player-movable floor-jack handling body.");
                Assert.That(
                    fixture.JackBody.constraints,
                    Is.EqualTo(
                        RigidbodyConstraints.FreezePositionY |
                        RigidbodyConstraints.FreezeRotationX |
                        RigidbodyConstraints.FreezeRotationZ));
                Assert.That(fixture.Controller.LiftHeadBody, Is.Not.Null);
                Assert.That(fixture.Controller.LiftHeadBody.isKinematic, Is.True);
                Assert.That(fixture.Controller.LiftHeadCollider, Is.Not.Null);
                Assert.That(fixture.Controller.LiftHeadCollider.isTrigger, Is.False);

                InteractionContext dragContext = CreateDragContext(fixture);
                fixture.Controller.BeginContinuousInteraction(
                    dragContext,
                    ContinuousContextInteractionDirection.Primary);
                yield return new WaitForFixedUpdate();
                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.Controller.LiftHeadCollider,
                        fixture.LoadCollider),
                    Is.True,
                    "The kinematic saddle stayed solid while being dragged " +
                    "and can tow the vehicle before the player releases it.");
                fixture.Controller.EndContinuousInteraction();
                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.Controller.LiftHeadCollider,
                        fixture.LoadCollider),
                    Is.False,
                    "Releasing the jack did not immediately restore the " +
                    "saddle contact needed by the next pump command.");

                float loadStartY = fixture.LoadBody.position.y;
                var context = new InteractionContext(
                    fixture.Interactor,
                    fixture.Controller.LiftHead.position,
                    Vector3.up);

                int pumpCount = Mathf.CeilToInt(
                    DonorMaximumLiftMeters / DonorLiftStepMeters);
                for (int pump = 0; pump < pumpCount; pump++)
                {
                    float expectedTarget = Mathf.Min(
                        DonorMaximumLiftMeters,
                        pump * DonorLiftStepMeters + DonorLiftStepMeters);
                    Assert.That(
                        fixture.Controller.CanInteract(context),
                        Is.True,
                        $"Pump {pump + 1} was rejected before maximum lift.");
                    fixture.Controller.Interact(context);
                    Assert.That(
                        fixture.Controller.TargetLiftHeightMeters,
                        Is.EqualTo(expectedTarget).Within(0.0001f));

                    yield return WaitForCondition(
                        () =>
                            Mathf.Abs(
                                fixture.Controller.LiftHeightMeters -
                                expectedTarget) < 0.0006f &&
                            (expectedTarget >=
                                DonorMaximumLiftMeters - 0.0001f ||
                             fixture.Controller.CanInteract(context)),
                        2f,
                        $"Floor-jack pump {pump + 1} did not finish.");
                }

                yield return WaitForFixedSteps(4);
                float raisedLoadY = fixture.LoadBody.position.y;
                Assert.That(
                    fixture.Controller.MaximumLiftMeters,
                    Is.EqualTo(DonorMaximumLiftMeters).Within(0.0001f));
                Assert.That(
                    fixture.Controller.TargetLiftHeightMeters,
                    Is.EqualTo(DonorMaximumLiftMeters).Within(0.0001f));
                Assert.That(
                    fixture.Controller.LiftHeightMeters,
                    Is.EqualTo(DonorMaximumLiftMeters).Within(0.001f));
                Assert.That(
                    fixture.Controller.LiftHead.localPosition.y,
                    Is.EqualTo(
                        LiftHeadBaseLocalY + DonorMaximumLiftMeters)
                        .Within(0.002f));
                Assert.That(
                    raisedLoadY,
                    Is.GreaterThan(loadStartY + 0.25f),
                    "A solid kinematic pad reached full travel but failed " +
                    "to lift a plain dynamic Rigidbody through PhysX contact.");

                Assert.That(
                    fixture.Controller.CanBeginContinuousInteraction(
                        context,
                        ContinuousContextInteractionDirection.Secondary),
                    Is.True);
                fixture.Controller.BeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Secondary);
                yield return WaitForCondition(
                    () =>
                    {
                        fixture.Controller.ContinueContinuousInteraction(
                            Time.fixedDeltaTime);
                        return fixture.Controller.LiftHeightMeters <= 0.001f &&
                            fixture.Controller.TargetLiftHeightMeters <= 0.001f;
                    },
                    2f,
                    "Floor jack did not complete its donor full-lower command.");
                fixture.Controller.EndContinuousInteraction();

                yield return WaitForFixedSteps(20);
                Assert.That(
                    fixture.Controller.LiftHeightMeters,
                    Is.EqualTo(0f).Within(0.001f));
                Assert.That(
                    fixture.Controller.LiftHead.localPosition.y,
                    Is.EqualTo(LiftHeadBaseLocalY).Within(0.002f));
                Assert.That(
                    fixture.LoadBody.position.y,
                    Is.LessThan(raisedLoadY - 0.2f),
                    "The load stayed suspended after the physical pad lowered.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                fixture?.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator GroundDrag_CrosshairMovesJack_AndKeepsSaddleFacingAim()
        {
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            FloorJackFixture fixture = null;
            try
            {
                fixture = CreateFixture();
                yield return WaitForFixedSteps(8);

                InteractionContext context = CreateDragContext(fixture);
                Assert.That(
                    fixture.Controller.CanBeginContinuousInteraction(
                        context,
                        ContinuousContextInteractionDirection.Primary),
                    Is.True);

                Vector3 startPosition = fixture.JackBody.position;
                fixture.Controller.BeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Primary);
                Assert.That(fixture.Controller.IsDragging, Is.True);

                Vector3 requestedTranslation = new Vector3(0.8f, 0f, 0.6f);
                fixture.Interactor.transform.position += requestedTranslation;
                // Fifteen fixed steps give the reviewed 6.5 m/s handling cap
                // enough time to cover this one-metre move, while the old
                // 2.4 m/s cap deterministically remains far behind the player.
                for (int step = 0; step < 15; step++)
                {
                    Assert.That(
                        fixture.Controller.ContinueContinuousInteraction(
                            Time.fixedDeltaTime),
                        Is.True);
                    yield return new WaitForFixedUpdate();
                }

                Vector3 translated = fixture.JackBody.position - startPosition;
                float verticalTranslation = translated.y;
                translated.y = 0f;
                Assert.That(
                    translated.magnitude,
                    Is.GreaterThan(0.7f),
                    "Moving the player on the ground did not roll the lowered " +
                    "floor jack along both horizontal axes.");
                Assert.That(
                    Vector3.Distance(translated, requestedTranslation),
                    Is.LessThan(0.12f),
                    "The lowered floor jack did not preserve its usable " +
                    "planar drag offset from the player.");
                Assert.That(
                    Mathf.Abs(verticalTranslation),
                    Is.LessThan(0.02f),
                    "Ground drag changed the jack's vertical placement.");

                Vector3 positionBeforeAimTurn = fixture.JackBody.position;
                float planarAimDistance = Vector3.Distance(
                    new Vector3(
                        fixture.AimSource.position.x,
                        0f,
                        fixture.AimSource.position.z),
                    new Vector3(
                        fixture.JackBody.position.x,
                        0f,
                        fixture.JackBody.position.z));
                fixture.AimSource.localRotation = Quaternion.Euler(
                    0f,
                    90f,
                    0f);
                for (int step = 0; step < 60; step++)
                {
                    Assert.That(
                        fixture.Controller.ContinueContinuousInteraction(
                            Time.fixedDeltaTime),
                        Is.True);
                    yield return new WaitForFixedUpdate();
                }

                Vector3 planarAimForward = fixture.AimSource.forward;
                planarAimForward.y = 0f;
                planarAimForward.Normalize();
                Vector3 expectedCrosshairPosition =
                    fixture.AimSource.position +
                    planarAimForward * planarAimDistance;
                expectedCrosshairPosition.y = fixture.JackBody.position.y;
                Vector3 saddleForward =
                    -(fixture.JackBody.rotation * Vector3.forward);
                saddleForward.y = 0f;
                saddleForward.Normalize();
                Assert.That(
                    Vector3.Dot(saddleForward, planarAimForward),
                    Is.GreaterThan(0.995f),
                    "The floor jack did not keep its saddle/front pointed in " +
                    "the same planar direction as the camera.");
                Assert.That(
                    Vector3.Distance(
                        fixture.JackBody.position,
                        expectedCrosshairPosition),
                    Is.LessThan(0.12f),
                    "The floor jack did not follow the updated crosshair " +
                    "position on the ground plane.");
                Assert.That(
                    Vector3.Distance(
                        fixture.JackBody.position,
                        positionBeforeAimTurn),
                    Is.GreaterThan(0.5f),
                    "Turning the aim left the jack at the old world offset " +
                    "instead of moving it under the crosshair.");
                Assert.That(
                    fixture.ArmCollider.isTrigger,
                    Is.True,
                    "The animated floor-jack arm remained a solid child of " +
                    "the dynamic base and can inject depenetration torque " +
                    "while lifting a vehicle.");
                fixture.Controller.EndContinuousInteraction();
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                fixture?.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator GroundDrag_CannotTowNearbyVehicle_AndRestoresSaddleContactAfterRelease()
        {
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            FloorJackFixture fixture = null;
            GameObject vehicleObject = null;
            try
            {
                fixture = CreateFixture();
                yield return WaitForFixedSteps(8);

                vehicleObject = new GameObject(
                    "Floor jack drag collision vehicle");
                vehicleObject.transform.position = fixture.JackBody.position +
                    new Vector3(0.5f, 0f, 0f);
                Rigidbody vehicleBody = vehicleObject.AddComponent<Rigidbody>();
                vehicleBody.mass = 800f;
                vehicleBody.useGravity = false;
                vehicleBody.constraints =
                    RigidbodyConstraints.FreezePositionY |
                    RigidbodyConstraints.FreezeRotation;
                BoxCollider vehicleCollider =
                    vehicleObject.AddComponent<BoxCollider>();
                vehicleCollider.size = new Vector3(0.36f, 0.18f, 0.36f);
                vehicleObject.AddComponent<VehicleAssemblyController>();
                Physics.SyncTransforms();

                Vector3 vehicleStartPosition = vehicleBody.position;
                InteractionContext context = CreateDragContext(fixture);
                fixture.Controller.BeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Primary);
                Assert.That(fixture.Controller.IsDragging, Is.True);
                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.BaseCollider,
                        vehicleCollider),
                    Is.True,
                    "The moving base must not push or tow the vehicle while " +
                    "the player rolls the lowered jack into position.");
                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.Controller.LiftHeadCollider,
                        vehicleCollider),
                    Is.True,
                    "The kinematic saddle must not tow the vehicle while the " +
                    "player is still rolling the jack into position.");

                fixture.Interactor.transform.position += Vector3.right;
                for (int step = 0; step < 70; step++)
                {
                    Assert.That(
                        fixture.Controller.ContinueContinuousInteraction(
                            Time.fixedDeltaTime),
                        Is.True);
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.BaseCollider,
                        vehicleCollider),
                    Is.True,
                    "Vehicle collision filtering ended before the continuous " +
                    "drag interaction was released.");
                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.Controller.LiftHeadCollider,
                        vehicleCollider),
                    Is.True,
                    "Saddle collision filtering ended before the player " +
                    "released the jack and let it become load-bearing.");

                Vector3 vehicleDisplacement =
                    vehicleBody.position - vehicleStartPosition;
                vehicleDisplacement.y = 0f;
                Assert.That(
                    vehicleDisplacement.magnitude,
                    Is.LessThan(0.03f),
                    "Rolling the lowered floor jack dragged the vehicle with " +
                    "its physical handling body.");

                fixture.Controller.EndContinuousInteraction();
                yield return WaitForFixedSteps(3);
                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.BaseCollider,
                        vehicleCollider),
                    Is.False,
                    "Base-to-vehicle collision stayed disabled after the " +
                    "drag operation ended and the pair separated.");
                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.Controller.LiftHeadCollider,
                        vehicleCollider),
                    Is.False,
                    "The physical saddle did not restore external vehicle " +
                    "contact after the lowered drag operation ended.");
                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.Controller.LiftHeadCollider,
                        fixture.LoadCollider),
                    Is.False,
                    "Drag collision filtering leaked into the saddle's normal " +
                    "external load-bearing contact.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                if (vehicleObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(vehicleObject);
                }

                fixture?.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PhysicalFloorJackAtRest_InternalHeadContactCannotSelfPropelBase()
        {
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            FloorJackFixture fixture = null;
            try
            {
                fixture = CreateFixture();
                yield return WaitForFixedSteps(8);

                Assert.That(
                    fixture.Controller.LiftHeadCollider.isTrigger,
                    Is.False,
                    "The saddle must remain a solid external load-bearing " +
                    "contact; making it a trigger only hides the drift bug.");
                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.Controller.LiftHeadCollider,
                        fixture.LoadCollider),
                    Is.False,
                    "The no-drift fix must not disable saddle contact with " +
                    "the external load.");
                Assert.That(
                    fixture.BaseCollider.bounds.Intersects(
                        fixture.Controller.LiftHeadCollider.bounds),
                    Is.True,
                    "The fixture no longer reproduces the donor-authored " +
                    "base/saddle overlap that caused self-propulsion.");

                Vector3 startPosition = fixture.JackBody.position;
                Quaternion startRotation = fixture.JackBody.rotation;
                float maximumPlanarDisplacement = 0f;
                float maximumYawExcursion = 0f;
                for (int step = 0; step < 120; step++)
                {
                    yield return new WaitForFixedUpdate();
                    Vector3 displacement =
                        fixture.JackBody.position - startPosition;
                    displacement.y = 0f;
                    maximumPlanarDisplacement = Mathf.Max(
                        maximumPlanarDisplacement,
                        displacement.magnitude);
                    maximumYawExcursion = Mathf.Max(
                        maximumYawExcursion,
                        Quaternion.Angle(
                            startRotation,
                            fixture.JackBody.rotation));
                }

                Assert.That(
                    Physics.GetIgnoreCollision(
                        fixture.BaseCollider,
                        fixture.Controller.LiftHeadCollider),
                    Is.True,
                    "The kinematic saddle overlaps the dynamic base at the " +
                    "authored rest pose. Their internal collision must be " +
                    "ignored while saddle-to-world contact stays solid, or " +
                    "PhysX continuously pushes the jack away from itself.");
                Assert.That(
                    maximumPlanarDisplacement,
                    Is.LessThan(0.01f),
                    "An untouched floor jack propelled itself across the " +
                    "ground through its internal base/saddle contact.");
                Assert.That(
                    maximumYawExcursion,
                    Is.LessThan(0.5f),
                    "An untouched floor jack yawed under its own internal " +
                    "base/saddle contact.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                fixture?.Dispose();
            }
        }

        private static FloorJackFixture CreateFixture()
        {
            ItemDefinitionCatalog definitions =
                AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                    DefinitionCatalogPath);
            Assert.That(definitions, Is.Not.Null);
            Assert.That(
                definitions.TryGet(
                    VehicleJackInteractionController.FloorJackDefinitionId,
                    out _),
                Is.True);

            var placements =
                ScriptableObject.CreateInstance<ItemPlacementCatalog>();
            placements.ConfigureForAuthoring(
                "test.floor-jack.playmode.placements",
                "test.floor-jack.donor-runtime-capture.v2",
                Array.Empty<ItemPlacementRecord>());
            var manifest = ScriptableObject.CreateInstance<
                ProductionWorldStreamingManifest>();
            manifest.ConfigureForAuthoring(
                512f,
                0,
                1,
                new[]
                {
                    new ProductionWorldCellScene(
                        "test.floor-jack.unused-cell",
                        99,
                        99,
                        999,
                        "Assets/Tests/Scenes/UnusedFloorJackCell.unity"),
                });

            var streamingObject = new GameObject(
                "Floor jack playmode streaming");
            ProductionWorldStreamingService streaming =
                streamingObject.AddComponent<ProductionWorldStreamingService>();
            streaming.ConfigureForAuthoring(manifest);
            var runtimeObject = new GameObject("Floor jack playmode runtime");
            ItemWorldRuntime runtime =
                runtimeObject.AddComponent<ItemWorldRuntime>();
            runtime.Initialize(
                definitions,
                placements,
                streaming,
                SceneManager.GetActiveScene(),
                -64f);

            Vector3 jackPosition = new Vector3(
                7000f,
                FixtureGroundY + JackRootGroundOffset,
                7000f);
            WorldItemInstance jack = runtime.SpawnDynamic(
                VehicleJackInteractionController.FloorJackDefinitionId,
                ItemStableIdUtility.CreateDeterministic(
                    "test.floor-jack.physical-lift"),
                jackPosition,
                Quaternion.identity,
                SceneManager.GetActiveScene());
            Rigidbody jackBody = jack.GetComponent<Rigidbody>();
            Assert.That(jackBody, Is.Not.Null);

            var headObject = new GameObject("Donor floor-jack lift pad");
            headObject.transform.SetParent(jack.transform, false);
            headObject.transform.localPosition = new Vector3(
                0f,
                LiftHeadBaseLocalY,
                LiftHeadLocalZ);
            Rigidbody headBody = headObject.AddComponent<Rigidbody>();
            headBody.useGravity = false;
            headBody.isKinematic = true;
            BoxCollider headCollider = headObject.AddComponent<BoxCollider>();
            headCollider.isTrigger = false;

            var pumpLeverObject = new GameObject(
                "Donor floor-jack pump lever pivot");
            pumpLeverObject.transform.SetParent(jack.transform, false);
            pumpLeverObject.transform.localPosition = new Vector3(
                0f,
                0.2f,
                0.35f);
            pumpLeverObject.transform.localRotation = Quaternion.Euler(
                -7f,
                12f,
                19f);

            var armObject = new GameObject("Donor floor-jack moving arm");
            armObject.transform.SetParent(jack.transform, false);
            armObject.transform.localPosition = new Vector3(0f, 0.04f, -0.1f);
            BoxCollider armCollider = armObject.AddComponent<BoxCollider>();
            armCollider.size = new Vector3(0.12f, 0.05f, 0.35f);

            VehicleJackInteractionController controller =
                jack.gameObject.AddComponent<VehicleJackInteractionController>();
            controller.ConfigureForAuthoring(
                VehicleJackInteractionController.FloorJackDefinitionId,
                headObject.transform,
                DonorMaximumLiftMeters,
                DonorLiftStepMeters,
                0.32f,
                configuredGroundDrag: true);
            controller.ConfigureArticulationForAuthoring(
                new[] { armObject.transform },
                new[] { new Vector3(40f, 0f, 0f) },
                new[] { Vector3.zero });
            controller.ConfigurePumpLeverForAuthoring(
                pumpLeverObject.transform);
            controller.Bind(jack);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Floor jack test ground";
            ground.transform.position = new Vector3(
                jackPosition.x,
                FixtureGroundY - 0.05f,
                jackPosition.z);
            ground.transform.localScale = new Vector3(4f, 0.1f, 4f);

            float loweredPadTopY = headObject.transform.position.y +
                controller.LiftHeadCollider.center.y +
                controller.LiftHeadCollider.size.y * 0.5f;
            var load = GameObject.CreatePrimitive(PrimitiveType.Cube);
            load.name = "Floor jack arbitrary dynamic load";
            load.transform.localScale = new Vector3(0.1f, 0.08f, 0.1f);
            load.transform.position = new Vector3(
                headObject.transform.position.x,
                loweredPadTopY + 0.041f,
                headObject.transform.position.z);
            Rigidbody loadBody = load.AddComponent<Rigidbody>();
            loadBody.mass = 20f;
            loadBody.useGravity = true;
            loadBody.isKinematic = false;
            loadBody.constraints = RigidbodyConstraints.FreezeRotation;
            loadBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            loadBody.interpolation = RigidbodyInterpolation.None;
            load.AddComponent<VehicleAssemblyController>();

            var interactor = new GameObject("Floor jack test interactor");
            // Start with the camera already looking along the donor saddle's
            // local -Z nose. This keeps the lift fixture centred until the
            // dedicated drag test deliberately changes camera yaw.
            interactor.transform.position = jackPosition + Vector3.forward;
            interactor.transform.rotation = Quaternion.LookRotation(
                Vector3.back,
                Vector3.up);
            var aimObject = new GameObject("Floor jack test viewpoint");
            aimObject.transform.SetParent(interactor.transform, false);
            aimObject.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            Camera aimCamera = aimObject.AddComponent<Camera>();
            aimCamera.enabled = false;
            Physics.SyncTransforms();
            return new FloorJackFixture(
                placements,
                manifest,
                streamingObject,
                runtimeObject,
                jack.gameObject,
                jackBody,
                controller,
                pumpLeverObject.transform,
                armCollider,
                jack.GetComponent<BoxCollider>(),
                load,
                load.GetComponent<Collider>(),
                loadBody,
                ground,
                interactor,
                aimObject.transform);
        }

        private static InteractionContext CreateDragContext(
            FloorJackFixture fixture)
        {
            return new InteractionContext(
                fixture.Interactor,
                fixture.AimSource.position,
                fixture.AimSource.forward);
        }

        private static IEnumerator WaitForFixedSteps(int count)
        {
            for (int index = 0; index < count; index++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator WaitForCondition(
            Func<bool> condition,
            float timeoutGameSeconds,
            string failureMessage)
        {
            float deadline = Time.time + timeoutGameSeconds;
            while (!condition() && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(condition(), Is.True, failureMessage);
        }

        private sealed class FloorJackFixture : IDisposable
        {
            private readonly ItemPlacementCatalog placements;
            private readonly ProductionWorldStreamingManifest manifest;
            private readonly GameObject streamingObject;
            private readonly GameObject runtimeObject;
            private readonly GameObject jackObject;
            private readonly GameObject loadObject;
            private readonly GameObject groundObject;

            public FloorJackFixture(
                ItemPlacementCatalog configuredPlacements,
                ProductionWorldStreamingManifest configuredManifest,
                GameObject configuredStreamingObject,
                GameObject configuredRuntimeObject,
                GameObject configuredJackObject,
                Rigidbody configuredJackBody,
                VehicleJackInteractionController configuredController,
                Transform configuredPumpLever,
                BoxCollider configuredArmCollider,
                BoxCollider configuredBaseCollider,
                GameObject configuredLoadObject,
                Collider configuredLoadCollider,
                Rigidbody configuredLoadBody,
                GameObject configuredGroundObject,
                GameObject configuredInteractor,
                Transform configuredAimSource)
            {
                placements = configuredPlacements;
                manifest = configuredManifest;
                streamingObject = configuredStreamingObject;
                runtimeObject = configuredRuntimeObject;
                jackObject = configuredJackObject;
                JackBody = configuredJackBody;
                Controller = configuredController;
                PumpLever = configuredPumpLever;
                ArmCollider = configuredArmCollider;
                BaseCollider = configuredBaseCollider;
                loadObject = configuredLoadObject;
                LoadCollider = configuredLoadCollider;
                LoadBody = configuredLoadBody;
                groundObject = configuredGroundObject;
                Interactor = configuredInteractor;
                AimSource = configuredAimSource;
            }

            public Rigidbody JackBody { get; }
            public VehicleJackInteractionController Controller { get; }
            public Transform PumpLever { get; }
            public BoxCollider ArmCollider { get; }
            public BoxCollider BaseCollider { get; }
            public Collider LoadCollider { get; }
            public Rigidbody LoadBody { get; }
            public GameObject Interactor { get; }
            public Transform AimSource { get; }

            public void Dispose()
            {
                DestroyImmediateSafe(Interactor);
                DestroyImmediateSafe(loadObject);
                DestroyImmediateSafe(groundObject);
                DestroyImmediateSafe(jackObject);
                DestroyImmediateSafe(runtimeObject);
                DestroyImmediateSafe(streamingObject);
                DestroyImmediateSafe(placements);
                DestroyImmediateSafe(manifest);
            }

            private static void DestroyImmediateSafe(UnityEngine.Object value)
            {
                if (value != null)
                {
                    UnityEngine.Object.DestroyImmediate(value);
                }
            }
        }
    }
}
