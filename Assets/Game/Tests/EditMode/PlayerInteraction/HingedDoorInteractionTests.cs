using MSC.Core.Identity;
using MSC.Bootstrap;
using MSC.Interaction;
using MSC.Interaction.Architecture;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Player;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.PlayerInteraction
{
    public sealed class HingedDoorInteractionTests
    {
        [Test]
        public void WorldCatalogContainsAllConfirmedHandOperatedDoors()
        {
            var installerObject = new GameObject("World Door Installer");
            try
            {
                ProductionWorldDoorInstaller installer =
                    installerObject.AddComponent<ProductionWorldDoorInstaller>();

                Assert.That(installer.ExpectedDoorCount, Is.EqualTo(32));
                Assert.That(
                    ProductionWorldDoorInstaller.DoorAngularSpeedDegrees,
                    Is.EqualTo(200f));
                Assert.That(
                    ProductionWorldDoorInstaller.DoorSwingAngleDegrees /
                    ProductionWorldDoorInstaller.DoorAngularSpeedDegrees,
                    Is.LessThan(0.5f));
                Assert.That(
                    ProductionWorldDoorInstaller.GarageDoorSwingAngleDegrees /
                    ProductionWorldDoorInstaller.GarageDoorAngularSpeedDegrees,
                    Is.GreaterThan(
                        ProductionWorldDoorInstaller.DoorSwingAngleDegrees /
                        ProductionWorldDoorInstaller.DoorAngularSpeedDegrees));
                Assert.That(
                    ProductionWorldDoorInstaller.HandleZoneRadiusMeters,
                    Is.EqualTo(0.24f));
            }
            finally
            {
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void GarageDoorUsesDirectionalHoldAndCoastsAfterRelease()
        {
            var pivotObject = new GameObject("Garage Door Pivot");
            try
            {
                HingedDoorInteractionTarget door =
                    pivotObject.AddComponent<HingedDoorInteractionTarget>();
                door.Configure(
                    pivotObject.transform,
                    Quaternion.identity,
                    Vector3.up,
                    ProductionWorldDoorInstaller.GarageDoorSwingAngleDegrees,
                    ProductionWorldDoorInstaller.GarageDoorAngularSpeedDegrees,
                    "ЛКМ: открыть / ПКМ: закрыть",
                    "ЛКМ: открыть / ПКМ: закрыть",
                    1f,
                    true,
                    ProductionWorldDoorInstaller.GarageDoorAccelerationDegrees,
                    ProductionWorldDoorInstaller.GarageDoorReleaseDecelerationDegrees);
                var context = new InteractionContext(
                    pivotObject,
                    Vector3.back,
                    Vector3.forward);

                Assert.That(door.CanInteract(context), Is.False);
                Assert.That(
                    door.CanBeginContinuousInteraction(
                        context,
                        ContinuousContextInteractionDirection.Primary),
                    Is.True);

                door.BeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Primary);
                door.ContinueContinuousInteraction(0.25f);
                door.EndContinuousInteraction();
                float releasePoint = door.OpenNormalized;
                door.ContinueContinuousInteraction(0.5f);

                Assert.That(releasePoint, Is.GreaterThan(0f));
                Assert.That(releasePoint, Is.LessThan(1f));
                Assert.That(door.OpenNormalized, Is.EqualTo(releasePoint));

                door.AdvanceReleasedMotion(0.05f);
                Assert.That(door.OpenNormalized, Is.GreaterThan(releasePoint));
                door.AdvanceReleasedMotion(0.2f);
                float coastedOpen = door.OpenNormalized;
                door.AdvanceReleasedMotion(0.2f);
                Assert.That(door.OpenNormalized, Is.EqualTo(coastedOpen));

                door.BeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Secondary);
                door.ContinueContinuousInteraction(0.5f);
                door.EndContinuousInteraction();

                Assert.That(door.TargetOpen, Is.False);
                Assert.That(door.OpenNormalized, Is.Zero.Within(0.001f));
                Assert.That(
                    door.EstimatedTravelSeconds,
                    Is.EqualTo(
                        ProductionWorldDoorInstaller.GarageDoorSwingAngleDegrees /
                        ProductionWorldDoorInstaller.GarageDoorAngularSpeedDegrees));
            }
            finally
            {
                Object.DestroyImmediate(pivotObject);
            }
        }

        [Test]
        public void DoorCompletesFasterThanHalfSecondAndClosesOnSecondUse()
        {
            var pivotObject = new GameObject("Door Pivot");
            HingedDoorInteractionTarget door =
                pivotObject.AddComponent<HingedDoorInteractionTarget>();
            Quaternion closedRotation = Quaternion.identity;
                door.Configure(
                    pivotObject.transform,
                    closedRotation,
                    Vector3.up,
                    95f,
                    ProductionWorldDoorInstaller.DoorAngularSpeedDegrees,
                "Открыть дверь",
                "Закрыть дверь");

            try
            {
                var context = new InteractionContext(
                    pivotObject,
                    pivotObject.transform.position - Vector3.forward,
                    Vector3.forward);

                door.Interact(context);
                door.Advance(0.48f);

                Assert.That(door.TargetOpen, Is.True);
                Assert.That(door.OpenNormalized, Is.EqualTo(1f));
                Assert.That(door.EstimatedTravelSeconds, Is.LessThan(0.5f));
                Assert.That(
                    Quaternion.Angle(
                        closedRotation,
                        pivotObject.transform.localRotation),
                    Is.EqualTo(95f).Within(0.05f));
                Assert.That(door.InteractionPrompt, Is.EqualTo("Закрыть дверь"));

                door.Interact(context);
                door.Advance(0.48f);

                Assert.That(door.TargetOpen, Is.False);
                Assert.That(door.OpenNormalized, Is.Zero);
                Assert.That(
                    Quaternion.Angle(
                        closedRotation,
                        pivotObject.transform.localRotation),
                    Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(pivotObject);
            }
        }

        [Test]
        public void DoorLeafColliderBlocksClosedOpeningAndMovesWithLeaf()
        {
            var pivotObject = new GameObject("Door Pivot");
            var leafObject = new GameObject("Door Leaf");
            try
            {
                leafObject.transform.SetParent(pivotObject.transform, false);
                leafObject.transform.localPosition =
                    new Vector3(0.5f, 1f, 0f);
                BoxCollider leafCollider =
                    leafObject.AddComponent<BoxCollider>();
                leafCollider.size = new Vector3(1f, 2f, 0.1f);

                HingedDoorInteractionTarget door =
                    pivotObject.AddComponent<HingedDoorInteractionTarget>();
                door.Configure(
                    pivotObject.transform,
                    Quaternion.identity,
                    Vector3.up,
                    95f,
                    ProductionWorldDoorInstaller.DoorAngularSpeedDegrees,
                    "Открыть дверь",
                    "Закрыть дверь");

                var doorwayRay = new Ray(
                    new Vector3(0.5f, 1f, -2f),
                    Vector3.forward);
                Physics.SyncTransforms();
                Assert.That(
                    leafCollider.Raycast(doorwayRay, out _, 4f),
                    Is.True);

                door.SetOpen(true, immediate: true);
                Physics.SyncTransforms();

                Assert.That(
                    leafCollider.Raycast(doorwayRay, out _, 4f),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(pivotObject);
            }
        }

        [Test]
        public void DoorKeepsAuthoredDirectionFromEitherSide()
        {
            var pivotObject = new GameObject("Door Pivot");
            try
            {
                HingedDoorInteractionTarget door =
                    pivotObject.AddComponent<HingedDoorInteractionTarget>();
                door.Configure(
                    pivotObject.transform,
                    Quaternion.identity,
                    Vector3.up,
                    95f,
                    ProductionWorldDoorInstaller.DoorAngularSpeedDegrees,
                    "Открыть дверь",
                    "Закрыть дверь",
                    -1f);

                door.Interact(new InteractionContext(
                    pivotObject,
                    Vector3.forward,
                    Vector3.back));
                door.Advance(0.48f);
                Quaternion firstSideRotation =
                    pivotObject.transform.localRotation;

                door.SetOpen(false, immediate: true);
                door.Interact(new InteractionContext(
                    pivotObject,
                    Vector3.back,
                    Vector3.forward));
                door.Advance(0.48f);

                Assert.That(door.OpeningSign, Is.EqualTo(-1f));
                Assert.That(
                    Quaternion.Angle(
                        firstSideRotation,
                        pivotObject.transform.localRotation),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(pivotObject);
            }
        }

        [Test]
        public void PrimaryClickUsesHandleWithoutDroppingCarriedObject()
        {
            var player = new GameObject("Player");
            var view = new GameObject("View");
            var carryAnchor = new GameObject("Carry Anchor");
            var heldObject = new GameObject("Held Object");
            var doorPivot = new GameObject("Door Pivot");
            var handle = new GameObject("Door Handle Interaction Zone");
            var unrelatedTrigger = new GameObject("Unregistered Trigger");

            try
            {
                view.transform.SetParent(player.transform, false);
                carryAnchor.transform.SetParent(player.transform, false);
                carryAnchor.transform.localPosition = Vector3.forward;
                BoxCollider playerCollider = player.AddComponent<BoxCollider>();
                PhysicalCarryController carry =
                    player.AddComponent<PhysicalCarryController>();
                carry.Configure(carryAnchor.transform, playerCollider);

                heldObject.transform.position = new Vector3(3f, 0f, 0f);
                BoxCollider heldCollider = heldObject.AddComponent<BoxCollider>();
                Rigidbody heldBody = heldObject.AddComponent<Rigidbody>();
                heldBody.useGravity = false;
                StableEntityIdAuthoring identity =
                    heldObject.AddComponent<StableEntityIdAuthoring>();
                Assert.That(
                    StableEntityId.TryParse(
                        "d718888aa2d14f1da9193e11839a5884",
                        out StableEntityId heldStableId),
                    Is.True);
                identity.InitializeExplicitRuntimeId(heldStableId);
                PhysicsPickupTarget pickup =
                    heldObject.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(heldBody, identity, "Поднять", 35f);
                Assert.That(
                    carry.TryPickup(
                        pickup,
                        new InteractionContext(
                            player,
                            view.transform.position,
                            view.transform.forward)),
                    Is.True);

                doorPivot.transform.position = new Vector3(0f, 0f, 2f);
                HingedDoorInteractionTarget door =
                    doorPivot.AddComponent<HingedDoorInteractionTarget>();
                door.Configure(
                    doorPivot.transform,
                    Quaternion.identity,
                    Vector3.up,
                    95f,
                    ProductionWorldDoorInstaller.DoorAngularSpeedDegrees,
                    "Открыть дверь",
                    "Закрыть дверь");

                handle.transform.SetParent(doorPivot.transform, false);
                handle.transform.position = new Vector3(0f, 0f, 1.8f);
                SphereCollider handleCollider =
                    handle.AddComponent<SphereCollider>();
                handleCollider.radius =
                    ProductionWorldDoorInstaller.HandleZoneRadiusMeters;
                handleCollider.isTrigger = true;
                InteractionTargetHost handleHost =
                    handle.AddComponent<InteractionTargetHost>();
                handleHost.Configure(door);

                unrelatedTrigger.transform.position =
                    new Vector3(0f, 0f, 0.75f);
                SphereCollider unrelatedTriggerCollider =
                    unrelatedTrigger.AddComponent<SphereCollider>();
                unrelatedTriggerCollider.radius = 0.2f;
                unrelatedTriggerCollider.isTrigger = true;

                RaycastInteractionCandidateSource source =
                    player.AddComponent<RaycastInteractionCandidateSource>();
                source.Configure(view.transform, 2.25f, ~0);
                PlayerInteractionController interaction =
                    player.AddComponent<PlayerInteractionController>();
                interaction.Configure(source, carry, view.transform, ~0);

                Physics.SyncTransforms();
                interaction.RefreshCandidate();

                Assert.That(interaction.HasCandidate, Is.True);
                Assert.That(interaction.TryBeginPrimaryInteraction(), Is.True);
                Assert.That(door.TargetOpen, Is.True);
                Assert.That(carry.HasHeldObject, Is.True);
                Assert.That(heldCollider.enabled, Is.True);

                door.SetOpen(false, immediate: true);
                Assert.That(interaction.TryToolActivation(), Is.False);
                Assert.That(door.TargetOpen, Is.False);
                Assert.That(carry.HasHeldObject, Is.True);

                door.Configure(
                    doorPivot.transform,
                    Quaternion.identity,
                    Vector3.up,
                    ProductionWorldDoorInstaller.GarageDoorSwingAngleDegrees,
                    ProductionWorldDoorInstaller.GarageDoorAngularSpeedDegrees,
                    "ЛКМ: открыть / ПКМ: закрыть",
                    "ЛКМ: открыть / ПКМ: закрыть",
                    1f,
                    true,
                    ProductionWorldDoorInstaller.GarageDoorAccelerationDegrees,
                    ProductionWorldDoorInstaller.GarageDoorReleaseDecelerationDegrees);

                Assert.That(
                    interaction.TryBeginPrimaryInteraction(),
                    Is.True);
                interaction.ContinuePrimaryInteraction(0.2f);
                interaction.EndPrimaryInteraction();
                Assert.That(door.OpenNormalized, Is.GreaterThan(0f));
                Assert.That(carry.HasHeldObject, Is.True);

                door.AdvanceReleasedMotion(0.2f);

                Assert.That(
                    interaction.TryBeginSecondaryInteraction(),
                    Is.True);
                interaction.ContinueSecondaryInteraction(0.5f);
                interaction.EndSecondaryInteraction();
                Assert.That(door.OpenNormalized, Is.Zero.Within(0.001f));
                Assert.That(carry.HasHeldObject, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(unrelatedTrigger);
                Object.DestroyImmediate(handle);
                Object.DestroyImmediate(doorPivot);
                Object.DestroyImmediate(heldObject);
                Object.DestroyImmediate(player);
            }
        }
    }
}
