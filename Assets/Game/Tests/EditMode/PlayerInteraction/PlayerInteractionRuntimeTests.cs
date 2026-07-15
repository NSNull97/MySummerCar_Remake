using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Prototype;
using MSC.Interaction.Query;
using MSC.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.PlayerInteraction
{
    public sealed class PlayerInteractionRuntimeTests
    {
        private GameObject owner;
        private GameObject item;
        private PhysicalCarryController carryController;
        private PhysicsPickupTarget pickupTarget;
        private InteractionContext context;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("TestInteractor");
            BoxCollider ownerCollider = owner.AddComponent<BoxCollider>();
            GameObject anchor = new GameObject("CarryAnchor");
            anchor.transform.SetParent(owner.transform, false);
            anchor.transform.localPosition = Vector3.forward;

            carryController = owner.AddComponent<PhysicalCarryController>();
            carryController.Configure(anchor.transform, ownerCollider);
            context = new InteractionContext(owner, owner.transform.position, Vector3.forward);

            item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "TestPickup";
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.mass = 4f;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            StableEntityIdAuthoring identity = item.AddComponent<StableEntityIdAuthoring>();
            SetStableId(identity, "db3f0cf92afd4c4c84b144592220cd21");
            pickupTarget = item.AddComponent<PhysicsPickupTarget>();
            pickupTarget.Configure(body, identity, "Поднять", 35f);
        }

        [TearDown]
        public void TearDown()
        {
            if (item != null)
            {
                Object.DestroyImmediate(item);
            }

            if (owner != null)
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PickupAndDropRestoresPhysicalState()
        {
            Rigidbody body = pickupTarget.Body;

            Assert.That(carryController.TryPickup(pickupTarget, context), Is.True);
            Assert.That(carryController.HasHeldObject, Is.True);
            Assert.That(pickupTarget.IsCarried, Is.True);
            Assert.That(body.useGravity, Is.False);

            Assert.That(carryController.Drop(), Is.True);
            Assert.That(carryController.HasHeldObject, Is.False);
            Assert.That(pickupTarget.IsCarried, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.Continuous));
        }

        [Test]
        public void CandidateBecomesInvalidWhenTargetIsDestroyed()
        {
            var capability = item.AddComponent<ContextToggleTarget>();
            InteractionTargetHost host = item.AddComponent<InteractionTargetHost>();
            host.Configure(capability);
            var candidate = new InteractionCandidate(host, Vector3.zero, Vector3.up, 1f);

            Assert.That(candidate.IsValid, Is.True);
            Object.DestroyImmediate(item);
            item = null;

            Assert.That(candidate.IsValid, Is.False);
            Assert.That(candidate.TryGetCapability<IContextInteractionTarget>(out _), Is.False);
        }

        [Test]
        public void RaycastQueryRejectsColliderWithoutCapabilityHost()
        {
            item.transform.position = new Vector3(0f, 0f, 1f);
            Physics.SyncTransforms();
            RaycastInteractionCandidateSource query =
                owner.AddComponent<RaycastInteractionCandidateSource>();
            query.Configure(owner.transform, 2f, ~0);

            InteractionCandidate candidate = query.Query();

            Assert.That(query.HasLastHit, Is.True);
            Assert.That(candidate.IsValid, Is.False);
        }

        [Test]
        public void CarriedObjectCanCrossMountHandoffBoundary()
        {
            Assert.That(carryController.TryPickup(pickupTarget, context), Is.True);
            var mountObject = new GameObject("Mount");
            var poseObject = new GameObject("Pose");
            poseObject.transform.SetParent(mountObject.transform, false);
            poseObject.transform.position = new Vector3(2f, 1f, 3f);
            PrototypeMountHandoffTarget mount = mountObject.AddComponent<PrototypeMountHandoffTarget>();
            mount.Configure(poseObject.transform);

            try
            {
                Assert.That(carryController.TryHandoff(mount, context), Is.True);
                Assert.That(carryController.HasHeldObject, Is.False);
                Assert.That(mount.HasMountedTarget, Is.True);
                Assert.That(pickupTarget.Body.isKinematic, Is.True);
                Assert.That(pickupTarget.Body.position, Is.EqualTo(poseObject.transform.position));
            }
            finally
            {
                Object.DestroyImmediate(mountObject);
            }
        }

        [Test]
        public void CarriedObjectSnapshotUsesStableIdentityAndRoundTripsThroughJson()
        {
            Assert.That(carryController.TryPickup(pickupTarget, context), Is.True);

            CarriedObjectSaveState captured = carryController.CaptureSaveState();
            string json = JsonUtility.ToJson(captured);
            CarriedObjectSaveState restored = JsonUtility.FromJson<CarriedObjectSaveState>(json);

            Assert.That(captured.HasCarriedObject, Is.True);
            Assert.That(restored.IsValid, Is.True);
            Assert.That(restored.SchemaVersion, Is.EqualTo(CarriedObjectSaveState.CurrentSchemaVersion));
            Assert.That(restored.StableEntityId, Is.EqualTo(pickupTarget.StableId.Value));
        }

        [Test]
        public void ToolTargetActivatesOnlyThroughExplicitCapability()
        {
            ToolActivationCounterTarget toolTarget = item.AddComponent<ToolActivationCounterTarget>();

            Assert.That(toolTarget.CanActivateTool(context), Is.True);
            toolTarget.ActivateTool(context);

            Assert.That(toolTarget.ActivationCount, Is.EqualTo(1));
        }

        [Test]
        public void LookInputScalingUsesDeltaForPointerAndTimeForRateInput()
        {
            Vector2 pointerDegrees = LookInputScaling.ToRotationDegrees(
                new Vector2(10f, -5f),
                isPointerDelta: true,
                pointerDegreesPerPixel: 0.2f,
                rateDegreesPerSecond: 90f,
                unscaledDeltaTime: 0.5f);
            Vector2 stickAtThirtyFps = LookInputScaling.ToRotationDegrees(
                Vector2.one,
                isPointerDelta: false,
                pointerDegreesPerPixel: 0.2f,
                rateDegreesPerSecond: 90f,
                unscaledDeltaTime: 1f / 30f);
            Vector2 stickAtSixtyFps = LookInputScaling.ToRotationDegrees(
                Vector2.one,
                isPointerDelta: false,
                pointerDegreesPerPixel: 0.2f,
                rateDegreesPerSecond: 90f,
                unscaledDeltaTime: 1f / 60f);

            Assert.That(pointerDegrees, Is.EqualTo(new Vector2(2f, -1f)));
            Assert.That(stickAtThirtyFps.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(stickAtThirtyFps.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(stickAtSixtyFps.x * 2f, Is.EqualTo(stickAtThirtyFps.x).Within(0.0001f));
            Assert.That(stickAtSixtyFps.y * 2f, Is.EqualTo(stickAtThirtyFps.y).Within(0.0001f));
        }

        [Test]
        public void CrossdotRectStaysCenteredAtDifferentResolutions()
        {
            Rect fullHd = CrossdotPresenter.CalculateCenteredRect(1920f, 1080f, 6f);
            Rect ultrawide = CrossdotPresenter.CalculateCenteredRect(3440f, 1440f, 6f);

            Assert.That(fullHd.center, Is.EqualTo(new Vector2(960f, 540f)));
            Assert.That(fullHd.size, Is.EqualTo(new Vector2(6f, 6f)));
            Assert.That(ultrawide.center, Is.EqualTo(new Vector2(1720f, 720f)));
        }

        private static void SetStableId(StableEntityIdAuthoring authoring, string stableId)
        {
            var serializedObject = new SerializedObject(authoring);
            serializedObject.FindProperty("stableId").stringValue = stableId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
