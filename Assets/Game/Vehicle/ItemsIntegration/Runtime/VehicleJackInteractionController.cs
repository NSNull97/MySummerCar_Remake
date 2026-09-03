using System;
using System.Collections.Generic;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Items;
using MSC.Items.Presentation;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle.ItemsIntegration
{
    /// <summary>
    /// Project-owned physical jack behavior reconstructed from the donor Use
    /// FSM. Core item state remains in WorldItemInstance; no donor FSM or
    /// hierarchy lookup is used at runtime.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    [DisallowMultipleComponent]
    public sealed class VehicleJackInteractionController : MonoBehaviour,
        IWorldItemPresentationBinding,
        IContextInteractionTarget,
        IContinuousContextInteractionTarget,
        IToolActivationTarget
    {
        public const string CarJackDefinitionId = "item.car-jack";
        public const string FloorJackDefinitionId = "item.floor-jack";
        public const string LiftHeightStateId = "lift-height";

        private const float LoweredToleranceMeters = 0.001f;
        private const float DragMaximumSpeedMetersPerSecond = 6.5f;
        private const float DragGroundProbeHeightMeters = 0.8f;
        private const float DragGroundProbeDistanceMeters = 1.8f;
        private const float VehicleCollisionDiscoveryRadiusMeters = 3.5f;
        private const float LiftVerticalCaptureToleranceMeters = 0.025f;
        private const float LiftSpringNewtonsPerMeter = 42000f;
        private const float LiftDampingNewtonSecondsPerMeter = 2600f;
        private const float MaximumLiftWeightMultiplier = 2.25f;
        private const float LiftPointReleaseMarginMeters = 0.08f;
        private const float FloorJackHandlingMassKilograms = 30f;
        private const float DonorFloorJackDamping = 9999f;
        private const float DonorFloorJackMaximumLiftMeters = 0.38f;
        private const float DonorFloorJackStepMeters = 0.015f;
        private const float DonorFloorJackHeadSpeedMetersPerSecond = 0.32f;
        private const float DonorFloorJackRaiseCommandSeconds = 0.5f;
        private const float DonorFloorJackLowerCommandSeconds = 0.4f;
        private const float DonorFloorJackMechanismRatio = 2.7f;
        private const float DonorFloorJackMoveLockHeightMeters = 0.01f;
        private const float DonorFloorJackPumpDurationSeconds = 0.6666667f;
        private const float DonorFloorJackPumpPeakSeconds = 0.3333334f;
        private const float DonorFloorJackPumpPeakDegrees = 40.18f;

        private static readonly Collider[] OverlapBuffer = new Collider[64];
        private static readonly RaycastHit[] GroundHits = new RaycastHit[16];

        [SerializeField] private string definitionId = FloorJackDefinitionId;
        [SerializeField] private Transform liftHead;
        [SerializeField] private Rigidbody liftHeadBody;
        [SerializeField] private BoxCollider liftHeadCollider;
        [SerializeField] private Vector3 liftHeadBaseLocalPosition;
        [SerializeField, Min(0.01f)] private float maximumLiftMeters = 0.38f;
        [SerializeField, Min(0.001f)] private float liftStepMeters = 0.015f;
        [SerializeField, Min(0.01f)] private float loweringSpeedMetersPerSecond =
            0.32f;
        [SerializeField] private bool supportsGroundDrag;
        [SerializeField] private Transform[] articulatedMembers =
            Array.Empty<Transform>();
        [SerializeField] private Vector3[] articulatedFullLiftEulerDegrees =
            Array.Empty<Vector3>();
        [SerializeField] private Vector3[] articulatedTravelPerLiftMeter =
            Array.Empty<Vector3>();
        [SerializeField] private Transform pumpLever;
        [SerializeField] private Quaternion pumpLeverRestLocalRotation =
            Quaternion.identity;

        private readonly List<CollisionPair> ignoredVehicleCollisions =
            new List<CollisionPair>();
        private readonly List<InternalCollisionPair> ignoredInternalCollisions =
            new List<InternalCollisionPair>();
        private WorldItemInstance owner;
        private Rigidbody body;
        private BoxCollider floorJackBaseCollider;
        private PhysicsPickupTarget pickup;
        private Collider[] itemColliders = Array.Empty<Collider>();
        private Transform dragInteractor;
        private Transform dragAimSource;
        private float dragAimDistanceMeters;
        private float dragGroundClearance;
        private bool dragging;
        private bool lowering;
        private float liftHeightMeters;
        private float liftTargetMarkerMeters;
        private float commandedLiftHeightMeters;
        private float commandStartMarkerMeters;
        private float commandElapsedSeconds;
        private float commandDurationSeconds;
        private VehicleJackLiftPoint activeLiftPoint;
        private Vector3[] articulatedBaseLocalPositions =
            Array.Empty<Vector3>();
        private Quaternion[] articulatedBaseLocalRotations =
            Array.Empty<Quaternion>();
        private float pumpLeverElapsedSeconds;
        private bool pumpLeverAnimating;

        public string InteractionPrompt
        {
            get
            {
                if (Mathf.Max(
                        liftHeightMeters,
                        commandedLiftHeightMeters) >
                    LoweredToleranceMeters)
                {
                    return UsesDonorPhysicalLiftHead()
                        ? "Опустить полностью (ПКМ) / качнуть ещё (F)"
                        : "Опустить домкрат (удерж. ПКМ) / поднять ещё (F)";
                }

                return supportsGroundDrag
                    ? "Катить домкрат (удерж. ЛКМ) / поднять (F)"
                    : "Поднять домкрат (F)";
            }
        }

        public bool UsesDirectionalHold =>
            supportsGroundDrag || liftHeightMeters > LoweredToleranceMeters;

        public string DefinitionId => definitionId;
        public string ToolPrompt => InteractionPrompt;
        public Transform LiftHead => liftHead;
        public Rigidbody LiftHeadBody => liftHeadBody;
        public BoxCollider LiftHeadCollider => liftHeadCollider;
        public float MaximumLiftMeters => maximumLiftMeters;
        public bool SupportsGroundDrag => supportsGroundDrag;
        public float LiftHeightMeters => liftHeightMeters;
        public float TargetLiftHeightMeters => commandedLiftHeightMeters;
        public bool IsDragging => dragging;
        public int ArticulatedMemberCount => articulatedMembers?.Length ?? 0;
        public Transform PumpLever => pumpLever;
        public bool IsPumpLeverAnimating => pumpLeverAnimating;

        public void ConfigureForAuthoring(
            string configuredDefinitionId,
            Transform configuredLiftHead,
            float configuredMaximumLiftMeters,
            float configuredLiftStepMeters,
            float configuredLoweringSpeedMetersPerSecond,
            bool configuredGroundDrag)
        {
            definitionId = configuredDefinitionId ?? string.Empty;
            liftHead = configuredLiftHead;
            liftHeadBody = liftHead != null
                ? liftHead.GetComponent<Rigidbody>()
                : null;
            liftHeadCollider = liftHead != null
                ? liftHead.GetComponent<BoxCollider>()
                : null;
            liftHeadBaseLocalPosition = liftHead != null
                ? liftHead.localPosition
                : Vector3.zero;
            maximumLiftMeters = Mathf.Max(
                0.01f,
                configuredMaximumLiftMeters);
            liftStepMeters = Mathf.Clamp(
                configuredLiftStepMeters,
                0.001f,
                maximumLiftMeters);
            loweringSpeedMetersPerSecond = Mathf.Max(
                0.01f,
                configuredLoweringSpeedMetersPerSecond);
            supportsGroundDrag = configuredGroundDrag;
            if (IsDonorPhysicalFloorJack())
            {
                maximumLiftMeters = DonorFloorJackMaximumLiftMeters;
                liftStepMeters = DonorFloorJackStepMeters;
                loweringSpeedMetersPerSecond =
                    DonorFloorJackHeadSpeedMetersPerSecond;
            }
            CaptureArticulationRestPose(force: false);
        }

        public void ConfigureArticulationForAuthoring(
            Transform[] configuredMembers,
            Vector3[] configuredFullLiftEulerDegrees,
            Vector3[] configuredTravelPerLiftMeter)
        {
            articulatedMembers = configuredMembers ?? Array.Empty<Transform>();
            articulatedFullLiftEulerDegrees =
                configuredFullLiftEulerDegrees ?? Array.Empty<Vector3>();
            articulatedTravelPerLiftMeter =
                configuredTravelPerLiftMeter ?? Array.Empty<Vector3>();
            if (articulatedMembers.Length !=
                    articulatedFullLiftEulerDegrees.Length ||
                articulatedMembers.Length !=
                    articulatedTravelPerLiftMeter.Length)
            {
                throw new ArgumentException(
                    "Jack articulation arrays must have equal lengths.");
            }

            CaptureArticulationRestPose(force: true);
            ApplyLiftPresentation();
        }

        public void ConfigurePumpLeverForAuthoring(
            Transform configuredPumpLever)
        {
            pumpLever = configuredPumpLever;
            pumpLeverRestLocalRotation = pumpLever != null
                ? pumpLever.localRotation
                : Quaternion.identity;
            ResetPumpLeverPresentation();
        }

        public void Bind(WorldItemInstance itemOwner)
        {
            RestoreIgnoredInternalCollisions();
            if (owner != null)
            {
                owner.StateRestored -= HandleStateRestored;
            }

            owner = itemOwner;
            body = owner != null ? owner.GetComponent<Rigidbody>() : null;
            floorJackBaseCollider = owner != null
                ? owner.GetComponent<BoxCollider>()
                : null;
            liftHeadBody = liftHead != null
                ? liftHead.GetComponent<Rigidbody>()
                : liftHeadBody;
            liftHeadCollider = liftHead != null
                ? liftHead.GetComponent<BoxCollider>()
                : liftHeadCollider;
            pickup = owner != null
                ? owner.GetComponent<PhysicsPickupTarget>()
                : null;
            itemColliders = owner != null
                ? owner.GetComponentsInChildren<Collider>(true)
                : Array.Empty<Collider>();
            InteractionTargetHost host = owner != null
                ? owner.GetComponent<InteractionTargetHost>()
                : null;
            host?.AddCapabilityFirst(this);
            if (owner != null)
            {
                owner.StateRestored += HandleStateRestored;
            }

            ConfigurePhysicalBody();
            ConfigurePhysicalLiftHead();
            ConfigureMovingArticulationColliders();
            ConfigureInternalLiftHeadCollisions();
            RestoreLiftHeadVehicleCollisions();
            CaptureArticulationRestPose(force: false);
            RestoreLiftState();
        }

        public bool CanInteract(in InteractionContext context)
        {
            if (!IsAvailable(context))
            {
                return false;
            }

            return UsesDonorPhysicalLiftHead()
                ? commandDurationSeconds <= 0f &&
                    commandedLiftHeightMeters <
                    maximumLiftMeters - 0.0001f
                : liftHeightMeters < maximumLiftMeters - 0.0001f;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            if (UsesDonorPhysicalLiftHead())
            {
                RestoreLiftHeadVehicleCollisions();
                BeginPumpLeverPresentation();
                BeginLiftCommand(
                    Mathf.Min(
                        maximumLiftMeters,
                        commandedLiftHeightMeters + liftStepMeters),
                    DonorFloorJackRaiseCommandSeconds);
                return;
            }

            SetLiftHeightImmediate(Mathf.Min(
                maximumLiftMeters,
                liftHeightMeters + liftStepMeters));
        }

        public bool CanActivateTool(in InteractionContext context) =>
            CanInteract(context);

        public void ActivateTool(in InteractionContext context)
        {
            Interact(context);
        }

        public bool CanBeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction)
        {
            if (!IsAvailable(context))
            {
                return false;
            }

            return direction switch
            {
                ContinuousContextInteractionDirection.Primary =>
                    supportsGroundDrag && IsFullyLowered(),
                ContinuousContextInteractionDirection.Secondary =>
                    Mathf.Max(
                        liftHeightMeters,
                        commandedLiftHeightMeters) >
                    LoweredToleranceMeters,
                _ => false,
            };
        }

        public void BeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction)
        {
            EndContinuousInteraction();
            if (!CanBeginContinuousInteraction(context, direction))
            {
                return;
            }

            if (direction == ContinuousContextInteractionDirection.Secondary)
            {
                lowering = true;
                if (UsesDonorPhysicalLiftHead())
                {
                    BeginLiftCommand(
                        0f,
                        DonorFloorJackLowerCommandSeconds);
                }
                return;
            }

            dragInteractor = context.Interactor != null
                ? context.Interactor.transform
                : null;
            if (dragInteractor == null || body == null)
            {
                return;
            }

            dragAimSource = ResolveDragAimSource(context);
            Vector3 aimOrigin = dragAimSource != null
                ? dragAimSource.position
                : context.Origin;
            Vector3 aimForward = dragAimSource != null
                ? dragAimSource.forward
                : context.Direction;
            Vector3 planarAimForward = ResolvePlanarAimForward(aimForward);
            Vector3 planarToJack = body.position - aimOrigin;
            planarToJack.y = 0f;
            float forwardDistance = Vector3.Dot(
                planarToJack,
                planarAimForward);
            dragAimDistanceMeters = Mathf.Max(
                0.35f,
                forwardDistance > 0.1f
                    ? forwardDistance
                    : planarToJack.magnitude);
            dragGroundClearance = ResolveGroundClearance(body.position);
            dragging = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            RefreshIgnoredVehicleCollisions();
        }

        public bool ContinueContinuousInteraction(float deltaTime)
        {
            if (lowering)
            {
                if (UsesDonorPhysicalLiftHead())
                {
                    return liftHeightMeters > LoweredToleranceMeters ||
                        commandedLiftHeightMeters > LoweredToleranceMeters;
                }

                if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
                {
                    return true;
                }

                SetLiftHeightImmediate(Mathf.Max(
                    0f,
                    liftHeightMeters -
                    loweringSpeedMetersPerSecond * deltaTime));
                return liftHeightMeters > LoweredToleranceMeters;
            }

            return dragging && dragInteractor != null && body != null;
        }

        public void EndContinuousInteraction()
        {
            dragging = false;
            lowering = false;
            dragInteractor = null;
            dragAimSource = null;
            RestoreLiftHeadVehicleCollisions();
            TryRestoreIgnoredVehicleCollisions(force: false);
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                if (!supportsGroundDrag)
                {
                    body.Sleep();
                }
            }
        }

        private void FixedUpdate()
        {
            UpdateLiftMotion(Time.fixedDeltaTime);
            if (dragging)
            {
                UpdateGroundDrag(Time.fixedDeltaTime);
            }

            // The pairs must stay ignored until the drag has actually ended.
            // Restoring them here, before the next PhysX solve, lets the moved
            // base shove the vehicle despite RefreshIgnoredVehicleCollisions()
            // having run a few lines earlier.
            if (!dragging)
            {
                TryRestoreIgnoredVehicleCollisions(force: false);
            }
            if (!UsesDonorPhysicalLiftHead())
            {
                ApplyVirtualLiftPointForce();
            }
        }

        private void Update()
        {
            UpdatePumpLeverPresentation(Time.deltaTime);
        }

        private void UpdateGroundDrag(float fixedDeltaTime)
        {
            if (body == null || dragInteractor == null || body.isKinematic)
            {
                EndContinuousInteraction();
                return;
            }

            RefreshIgnoredVehicleCollisions();
            Vector3 aimOrigin = dragAimSource != null
                ? dragAimSource.position
                : dragInteractor.position;
            Vector3 aimForward = dragAimSource != null
                ? dragAimSource.forward
                : dragInteractor.forward;
            Vector3 planarAimForward = ResolvePlanarAimForward(aimForward);
            Vector3 desired = aimOrigin +
                planarAimForward *
                dragAimDistanceMeters;
            if (TryResolveGroundHeight(desired, out float groundHeight))
            {
                desired.y = groundHeight + dragGroundClearance;
            }
            else
            {
                desired.y = body.position.y;
            }

            body.MovePosition(Vector3.MoveTowards(
                body.position,
                desired,
                DragMaximumSpeedMetersPerSecond * fixedDeltaTime));
            // The donor saddle sits on the root's local -Z side. Keep that
            // front pointed with the camera while moving the root to the
            // crosshair target; changing aim rotates the jack in place rather
            // than orbiting its position around the player.
            Quaternion desiredRotation = Quaternion.LookRotation(
                -planarAimForward,
                Vector3.up);
            body.angularVelocity = Vector3.zero;
            body.MoveRotation(desiredRotation);
        }

        private Transform ResolveDragAimSource(in InteractionContext context)
        {
            if (context.Interactor == null)
            {
                return null;
            }

            Camera[] cameras = context.Interactor
                .GetComponentsInChildren<Camera>(true);
            Transform best = null;
            float bestScore = float.PositiveInfinity;
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera candidate = cameras[index];
                if (candidate == null)
                {
                    continue;
                }

                Transform candidateTransform = candidate.transform;
                float positionError = Vector3.SqrMagnitude(
                    candidateTransform.position - context.Origin);
                float directionError = 1f - Mathf.Clamp01(Vector3.Dot(
                    candidateTransform.forward,
                    context.Direction));
                float score = positionError + directionError * 4f;
                if (candidate.enabled &&
                    candidate.gameObject.activeInHierarchy)
                {
                    score -= 0.25f;
                }

                if (score >= bestScore)
                {
                    continue;
                }

                best = candidateTransform;
                bestScore = score;
            }

            return best != null ? best : context.Interactor.transform;
        }

        private Vector3 ResolvePlanarAimForward(Vector3 aimForward)
        {
            aimForward.y = 0f;
            if (aimForward.sqrMagnitude > 0.0001f)
            {
                return aimForward.normalized;
            }

            Vector3 fallback = dragInteractor != null
                ? dragInteractor.forward
                : transform.forward;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f
                ? fallback.normalized
                : Vector3.forward;
        }

        private void ApplyVirtualLiftPointForce()
        {
            if (liftHead == null ||
                liftHeightMeters <= LoweredToleranceMeters)
            {
                activeLiftPoint = null;
                return;
            }

            if (!IsLiftPointStillReachable(activeLiftPoint))
            {
                activeLiftPoint = FindNearestLiftPoint();
            }

            if (activeLiftPoint == null)
            {
                return;
            }

            float penetration = liftHead.position.y -
                activeLiftPoint.ContactPosition.y;
            if (penetration < -LiftVerticalCaptureToleranceMeters)
            {
                return;
            }

            Rigidbody chassis = activeLiftPoint.Chassis;
            float gravity = Mathf.Max(0.1f, Mathf.Abs(Physics.gravity.y));
            float pointVelocity = Vector3.Dot(
                chassis.GetPointVelocity(activeLiftPoint.ContactPosition),
                Vector3.up);
            float contactBlend = Mathf.InverseLerp(
                -LiftVerticalCaptureToleranceMeters,
                0.005f,
                penetration);
            float supportForce = chassis.mass * gravity * 1.025f *
                    contactBlend +
                Mathf.Max(0f, penetration) * LiftSpringNewtonsPerMeter -
                pointVelocity * LiftDampingNewtonSecondsPerMeter;
            float maximumForce = chassis.mass * gravity *
                MaximumLiftWeightMultiplier;
            supportForce = Mathf.Clamp(supportForce, 0f, maximumForce);
            chassis.AddForceAtPosition(
                Vector3.up * supportForce,
                activeLiftPoint.ContactPosition,
                ForceMode.Force);
            chassis.WakeUp();
            if (body != null && !body.isKinematic)
            {
                body.AddForceAtPosition(
                    Vector3.down * supportForce,
                    liftHead.position,
                    ForceMode.Force);
            }
        }

        private VehicleJackLiftPoint FindNearestLiftPoint()
        {
            int count = Physics.OverlapSphereNonAlloc(
                liftHead.position,
                0.35f,
                OverlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide);
            VehicleJackLiftPoint best = null;
            float bestHorizontalDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                VehicleJackLiftPoint point = OverlapBuffer[index] != null
                    ? OverlapBuffer[index]
                        .GetComponentInParent<VehicleJackLiftPoint>()
                    : null;
                if (point == null || point.Chassis == null ||
                    point.Chassis == body || point.Chassis.isKinematic)
                {
                    continue;
                }

                Vector3 offset = point.ContactPosition - liftHead.position;
                float horizontal = new Vector2(offset.x, offset.z).magnitude;
                if (horizontal <= point.HorizontalCaptureRadius &&
                    horizontal < bestHorizontalDistance)
                {
                    best = point;
                    bestHorizontalDistance = horizontal;
                }
            }

            return best;
        }

        private bool IsLiftPointStillReachable(VehicleJackLiftPoint point)
        {
            if (point == null || point.Chassis == null ||
                point.Chassis == body || point.Chassis.isKinematic)
            {
                return false;
            }

            Vector3 offset = point.ContactPosition - liftHead.position;
            float horizontal = new Vector2(offset.x, offset.z).magnitude;
            return horizontal <= point.HorizontalCaptureRadius +
                LiftPointReleaseMarginMeters &&
                offset.y <= LiftVerticalCaptureToleranceMeters +
                LiftPointReleaseMarginMeters;
        }

        private bool IsAvailable(in InteractionContext context)
        {
            if (owner == null || body == null || liftHead == null ||
                dragging || lowering)
            {
                return false;
            }

            PhysicalCarryController carry = context.Interactor != null
                ? context.Interactor.GetComponent<PhysicalCarryController>()
                : null;
            return carry == null || !carry.HasHeldObject;
        }

        private bool IsFullyLowered()
        {
            float threshold = UsesDonorPhysicalLiftHead()
                ? DonorFloorJackMoveLockHeightMeters
                : LoweredToleranceMeters;
            return liftHeightMeters <= threshold &&
            (!UsesDonorPhysicalLiftHead() ||
             commandedLiftHeightMeters <= threshold &&
             liftTargetMarkerMeters <= threshold);
        }

        private void ConfigurePhysicalBody()
        {
            if (body == null || !supportsGroundDrag)
            {
                return;
            }

            // The vanilla donor's serialized 9999 kg was an immovability hack,
            // not a credible transport mass. With a modern dynamic MovePosition
            // body it turns the jack into a vehicle-towing bulldozer. The live
            // MOPR/BetterMSC handling profile uses 30 kg; lift authority still
            // belongs to the separate kinematic saddle, so this does not reduce
            // the load the jack can raise.
            body.mass = FloorJackHandlingMassKilograms;
            body.useGravity = true;
            body.isKinematic = false;
            body.linearDamping = DonorFloorJackDamping;
            body.angularDamping = DonorFloorJackDamping;
            body.constraints = RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            if (floorJackBaseCollider != null)
            {
                floorJackBaseCollider.center = new Vector3(
                    0f,
                    -0.07f,
                    0f);
                floorJackBaseCollider.size = new Vector3(
                    0.17f,
                    0.04f,
                    0.7f);
            }
        }

        private void ConfigurePhysicalLiftHead()
        {
            if (!IsDonorPhysicalFloorJack() || liftHead == null)
            {
                return;
            }

            if (liftHeadBody == null)
            {
                liftHeadBody = liftHead.gameObject.AddComponent<Rigidbody>();
            }

            liftHeadBody.mass = 0.0000001f;
            liftHeadBody.useGravity = false;
            liftHeadBody.isKinematic = true;
            liftHeadBody.interpolation = RigidbodyInterpolation.None;
            // ContinuousSpeculative is Unity 6's supported continuous mode
            // for kinematic bodies; the legacy donor stored Continuous here.
            liftHeadBody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;

            if (liftHeadCollider == null)
            {
                liftHeadCollider = liftHead.gameObject
                    .AddComponent<BoxCollider>();
            }

            liftHeadCollider.isTrigger = false;
            liftHeadCollider.center = Vector3.zero;
            liftHeadCollider.size = new Vector3(0.1f, 0.09f, 0.1f);
        }

        private void ConfigureMovingArticulationColliders()
        {
            if (!IsDonorPhysicalFloorJack() || articulatedMembers == null)
            {
                return;
            }

            // Only the saddle is load-bearing. A solid collider on an animated
            // child becomes part of the dynamic base's compound shape; changing
            // that child's transform while it overlaps a car makes PhysX eject
            // and yaw the entire base. Keep the reviewed arm volume as a
            // trigger for queries, but remove it from contact resolution.
            for (int colliderIndex = 0;
                 colliderIndex < itemColliders.Length;
                 colliderIndex++)
            {
                Collider candidate = itemColliders[colliderIndex];
                if (candidate == null ||
                    candidate == floorJackBaseCollider ||
                    candidate == liftHeadCollider ||
                    candidate.isTrigger)
                {
                    continue;
                }

                Transform candidateTransform = candidate.transform;
                for (int memberIndex = 0;
                     memberIndex < articulatedMembers.Length;
                     memberIndex++)
                {
                    Transform member = articulatedMembers[memberIndex];
                    if (member == null ||
                        candidateTransform != member &&
                        !candidateTransform.IsChildOf(member))
                    {
                        continue;
                    }

                    candidate.isTrigger = true;
                    break;
                }
            }
        }

        private void ConfigureInternalLiftHeadCollisions()
        {
            RestoreIgnoredInternalCollisions();
            if (!UsesDonorPhysicalLiftHead() || owner == null || body == null)
            {
                return;
            }

            // The donor saddle is a separate kinematic actor. At its lowered
            // pose it intentionally occupies some of the dynamic base's volume.
            // Letting those two actors resolve contacts makes PhysX eject the
            // Y-locked base sideways. Ignore only same-item compound colliders;
            // the saddle must still collide with the car and the world.
            itemColliders = owner.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < itemColliders.Length; index++)
            {
                Collider candidate = itemColliders[index];
                if (candidate == null ||
                    candidate == liftHeadCollider ||
                    candidate.isTrigger ||
                    candidate.attachedRigidbody != body)
                {
                    continue;
                }

                Physics.IgnoreCollision(
                    liftHeadCollider,
                    candidate,
                    true);
                ignoredInternalCollisions.Add(new InternalCollisionPair(
                    liftHeadCollider,
                    candidate));
            }
        }

        private void RestoreIgnoredInternalCollisions()
        {
            for (int index = ignoredInternalCollisions.Count - 1;
                 index >= 0;
                 index--)
            {
                InternalCollisionPair pair =
                    ignoredInternalCollisions[index];
                if (pair.Head != null && pair.BodyCollider != null)
                {
                    Physics.IgnoreCollision(
                        pair.Head,
                        pair.BodyCollider,
                        false);
                }

                ignoredInternalCollisions.RemoveAt(index);
            }
        }

        private void BeginLiftCommand(float heightMeters, float durationSeconds)
        {
            commandStartMarkerMeters = liftTargetMarkerMeters;
            commandedLiftHeightMeters = Mathf.Clamp(
                heightMeters,
                0f,
                maximumLiftMeters);
            commandElapsedSeconds = 0f;
            commandDurationSeconds = Mathf.Max(0.0001f, durationSeconds);
        }

        private void UpdateLiftMotion(float fixedDeltaTime)
        {
            if (!UsesDonorPhysicalLiftHead() ||
                !float.IsFinite(fixedDeltaTime) ||
                fixedDeltaTime <= 0f)
            {
                return;
            }

            if (commandDurationSeconds > 0f)
            {
                commandElapsedSeconds += fixedDeltaTime;
                float progress = Mathf.Clamp01(
                    commandElapsedSeconds / commandDurationSeconds);
                liftTargetMarkerMeters = Mathf.Lerp(
                    commandStartMarkerMeters,
                    commandedLiftHeightMeters,
                    progress);
                if (progress >= 1f)
                {
                    commandDurationSeconds = 0f;
                    liftTargetMarkerMeters = commandedLiftHeightMeters;
                }
            }

            float nextHeight = Mathf.MoveTowards(
                liftHeightMeters,
                liftTargetMarkerMeters,
                loweringSpeedMetersPerSecond * fixedDeltaTime);
            bool moved = !Mathf.Approximately(nextHeight, liftHeightMeters);
            liftHeightMeters = nextHeight;
            MovePhysicalLiftHead();
            ApplyArticulationPresentation();
            if (moved)
            {
                PersistLiftHeight();
                RefreshPickupAvailability();
            }
        }

        private void SetLiftHeightImmediate(float heightMeters)
        {
            liftHeightMeters = Mathf.Clamp(
                heightMeters,
                0f,
                maximumLiftMeters);
            liftTargetMarkerMeters = liftHeightMeters;
            commandedLiftHeightMeters = liftHeightMeters;
            commandDurationSeconds = 0f;
            PersistLiftHeight();
            ApplyLiftPresentation(teleportPhysicalHead: true);
            RefreshPickupAvailability();
            if (IsFullyLowered())
            {
                activeLiftPoint = null;
            }
        }

        private void RestoreLiftState()
        {
            liftHeightMeters = owner != null && owner.TryGetScalar(
                    LiftHeightStateId,
                    out float restored)
                ? Mathf.Clamp(restored, 0f, maximumLiftMeters)
                : 0f;
            liftTargetMarkerMeters = liftHeightMeters;
            commandedLiftHeightMeters = liftHeightMeters;
            commandDurationSeconds = 0f;
            ApplyLiftPresentation(teleportPhysicalHead: true);
            RefreshPickupAvailability();
        }

        private void PersistLiftHeight()
        {
            if (owner != null)
            {
                owner.TrySetScalar(LiftHeightStateId, liftHeightMeters);
            }
        }

        private void ApplyLiftPresentation(bool teleportPhysicalHead = false)
        {
            if (liftHead != null)
            {
                if (UsesDonorPhysicalLiftHead())
                {
                    if (teleportPhysicalHead)
                    {
                        Vector3 target = ResolveLiftHeadWorldPosition();
                        liftHead.position = target;
                        liftHeadBody.position = target;
                    }
                }
                else
                {
                    liftHead.localPosition = liftHeadBaseLocalPosition +
                        Vector3.up * liftHeightMeters;
                }
            }

            ApplyArticulationPresentation();
        }

        private void ApplyArticulationPresentation()
        {
            int memberCount = articulatedMembers?.Length ?? 0;
            if (memberCount == 0 ||
                articulatedBaseLocalPositions.Length != memberCount ||
                articulatedBaseLocalRotations.Length != memberCount)
            {
                return;
            }

            float normalizedLift = maximumLiftMeters > 0.0001f
                ? liftHeightMeters / maximumLiftMeters
                : 0f;
            for (int index = 0; index < memberCount; index++)
            {
                Transform member = articulatedMembers[index];
                if (member == null)
                {
                    continue;
                }

                member.localPosition = articulatedBaseLocalPositions[index] +
                    articulatedTravelPerLiftMeter[index] * liftHeightMeters;
                Quaternion liftDelta = Quaternion.Euler(
                    articulatedFullLiftEulerDegrees[index] * normalizedLift);
                member.localRotation = UsesDonorPhysicalLiftHead()
                    ? articulatedBaseLocalRotations[index] * liftDelta
                    : liftDelta * articulatedBaseLocalRotations[index];
            }
        }

        private void BeginPumpLeverPresentation()
        {
            if (!UsesDonorPhysicalLiftHead() || pumpLever == null)
            {
                return;
            }

            pumpLeverElapsedSeconds = 0f;
            pumpLeverAnimating = true;
            ApplyPumpLeverAngle(0f);
        }

        private void UpdatePumpLeverPresentation(float deltaTime)
        {
            if (!pumpLeverAnimating || pumpLever == null ||
                !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            pumpLeverElapsedSeconds = Mathf.Min(
                DonorFloorJackPumpDurationSeconds,
                pumpLeverElapsedSeconds + deltaTime);
            float angleDegrees = pumpLeverElapsedSeconds <=
                DonorFloorJackPumpPeakSeconds
                ? Mathf.Lerp(
                    0f,
                    DonorFloorJackPumpPeakDegrees,
                    pumpLeverElapsedSeconds /
                    DonorFloorJackPumpPeakSeconds)
                : Mathf.Lerp(
                    DonorFloorJackPumpPeakDegrees,
                    0f,
                    (pumpLeverElapsedSeconds -
                     DonorFloorJackPumpPeakSeconds) /
                    (DonorFloorJackPumpDurationSeconds -
                     DonorFloorJackPumpPeakSeconds));
            ApplyPumpLeverAngle(angleDegrees);

            if (pumpLeverElapsedSeconds >=
                DonorFloorJackPumpDurationSeconds)
            {
                ResetPumpLeverPresentation();
            }
        }

        private void ApplyPumpLeverAngle(float angleDegrees)
        {
            if (pumpLever != null)
            {
                pumpLever.localRotation = pumpLeverRestLocalRotation *
                    Quaternion.Euler(0f, 0f, angleDegrees);
            }
        }

        private void ResetPumpLeverPresentation()
        {
            pumpLeverElapsedSeconds = 0f;
            pumpLeverAnimating = false;
            if (pumpLever != null)
            {
                pumpLever.localRotation = pumpLeverRestLocalRotation;
            }
        }

        private void MovePhysicalLiftHead()
        {
            if (!UsesDonorPhysicalLiftHead())
            {
                return;
            }

            liftHeadBody.MovePosition(ResolveLiftHeadWorldPosition());
        }

        private Vector3 ResolveLiftHeadWorldPosition()
        {
            Transform parent = liftHead != null ? liftHead.parent : null;
            Vector3 local = liftHeadBaseLocalPosition +
                Vector3.up * liftHeightMeters;
            return parent != null ? parent.TransformPoint(local) : local;
        }

        private bool IsDonorPhysicalFloorJack() =>
            supportsGroundDrag &&
            string.Equals(
                definitionId,
                FloorJackDefinitionId,
                StringComparison.Ordinal);

        private bool UsesDonorPhysicalLiftHead() =>
            IsDonorPhysicalFloorJack() &&
            liftHeadBody != null &&
            liftHeadCollider != null &&
            !liftHeadCollider.isTrigger;

        private void CaptureArticulationRestPose(bool force)
        {
            int count = articulatedMembers?.Length ?? 0;
            if (!force &&
                articulatedBaseLocalPositions.Length == count &&
                articulatedBaseLocalRotations.Length == count)
            {
                return;
            }

            articulatedBaseLocalPositions = new Vector3[count];
            articulatedBaseLocalRotations = new Quaternion[count];
            for (int index = 0; index < count; index++)
            {
                Transform member = articulatedMembers[index];
                articulatedBaseLocalPositions[index] = member != null
                    ? member.localPosition
                    : Vector3.zero;
                articulatedBaseLocalRotations[index] = member != null
                    ? member.localRotation
                    : Quaternion.identity;
            }
        }

        private void RefreshPickupAvailability()
        {
            if (pickup != null)
            {
                pickup.SetPickupEnabled(
                    !supportsGroundDrag && IsFullyLowered());
            }
        }

        private float ResolveGroundClearance(Vector3 position)
        {
            return TryResolveGroundHeight(position, out float groundHeight)
                ? position.y - groundHeight
                : 0f;
        }

        private bool TryResolveGroundHeight(
            Vector3 position,
            out float groundHeight)
        {
            int count = Physics.RaycastNonAlloc(
                position + Vector3.up * DragGroundProbeHeightMeters,
                Vector3.down,
                GroundHits,
                DragGroundProbeDistanceMeters,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            groundHeight = 0f;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = GroundHits[index];
                if (hit.collider == null ||
                    hit.collider.attachedRigidbody == body ||
                    hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                groundHeight = hit.point.y;
            }

            return bestDistance < float.PositiveInfinity;
        }

        private void RefreshIgnoredVehicleCollisions()
        {
            if (body == null)
            {
                return;
            }

            int count = Physics.OverlapSphereNonAlloc(
                body.position,
                VehicleCollisionDiscoveryRadiusMeters,
                OverlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int index = 0; index < count; index++)
            {
                VehicleAssemblyController assembly = OverlapBuffer[index] != null
                    ? OverlapBuffer[index]
                        .GetComponentInParent<VehicleAssemblyController>()
                    : null;
                Rigidbody chassis = assembly != null
                    ? assembly.GetComponent<Rigidbody>()
                    : null;
                if (chassis == null)
                {
                    continue;
                }

                Collider[] vehicleColliders = chassis
                    .GetComponentsInChildren<Collider>(true);
                for (int itemIndex = 0;
                     itemIndex < itemColliders.Length;
                     itemIndex++)
                {
                    Collider itemCollider = itemColliders[itemIndex];
                    if (itemCollider == null || itemCollider.isTrigger)
                    {
                        continue;
                    }

                    for (int vehicleIndex = 0;
                         vehicleIndex < vehicleColliders.Length;
                         vehicleIndex++)
                    {
                        Collider vehicleCollider =
                            vehicleColliders[vehicleIndex];
                        if (vehicleCollider == null ||
                            vehicleCollider.isTrigger)
                        {
                            continue;
                        }

                        if (ContainsIgnoredPair(
                                itemCollider,
                                vehicleCollider))
                        {
                            continue;
                        }

                        Physics.IgnoreCollision(
                            itemCollider,
                            vehicleCollider,
                            true);
                        ignoredVehicleCollisions.Add(new CollisionPair(
                            itemCollider,
                            vehicleCollider));
                    }
                }
            }
        }

        private void RestoreLiftHeadVehicleCollisions()
        {
            if (liftHeadCollider == null)
            {
                return;
            }

            // Restore every pair we actually disabled, even if the saddle is
            // already touching the underside. Deferring a penetrating pair is
            // correct for the heavy base, but would leave the lift contact a
            // permanent ghost exactly when the player starts pumping.
            for (int index = ignoredVehicleCollisions.Count - 1;
                 index >= 0;
                 index--)
            {
                CollisionPair pair = ignoredVehicleCollisions[index];
                if (pair.Item != liftHeadCollider)
                {
                    continue;
                }

                if (pair.Vehicle != null)
                {
                    Physics.IgnoreCollision(
                        liftHeadCollider,
                        pair.Vehicle,
                        false);
                }

                ignoredVehicleCollisions.RemoveAt(index);
            }

            if (body == null)
            {
                return;
            }

            int count = Physics.OverlapSphereNonAlloc(
                body.position,
                VehicleCollisionDiscoveryRadiusMeters,
                OverlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int index = 0; index < count; index++)
            {
                VehicleAssemblyController assembly = OverlapBuffer[index] != null
                    ? OverlapBuffer[index]
                        .GetComponentInParent<VehicleAssemblyController>()
                    : null;
                Rigidbody chassis = assembly != null
                    ? assembly.GetComponent<Rigidbody>()
                    : null;
                if (chassis == null)
                {
                    continue;
                }

                Collider[] vehicleColliders = chassis
                    .GetComponentsInChildren<Collider>(true);
                for (int vehicleIndex = 0;
                     vehicleIndex < vehicleColliders.Length;
                     vehicleIndex++)
                {
                    Collider vehicleCollider = vehicleColliders[vehicleIndex];
                    if (vehicleCollider == null || vehicleCollider.isTrigger)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(
                        liftHeadCollider,
                        vehicleCollider,
                        false);
                    RemoveIgnoredPairTracking(
                        liftHeadCollider,
                        vehicleCollider);
                }
            }
        }

        private void RemoveIgnoredPairTracking(
            Collider item,
            Collider vehicle)
        {
            for (int index = ignoredVehicleCollisions.Count - 1;
                 index >= 0;
                 index--)
            {
                CollisionPair pair = ignoredVehicleCollisions[index];
                if (pair.Item == item && pair.Vehicle == vehicle)
                {
                    ignoredVehicleCollisions.RemoveAt(index);
                }
            }
        }

        private bool ContainsIgnoredPair(Collider item, Collider vehicle)
        {
            for (int index = 0;
                 index < ignoredVehicleCollisions.Count;
                 index++)
            {
                CollisionPair pair = ignoredVehicleCollisions[index];
                if (pair.Item == item && pair.Vehicle == vehicle)
                {
                    return true;
                }
            }

            return false;
        }

        private void TryRestoreIgnoredVehicleCollisions(bool force)
        {
            for (int index = ignoredVehicleCollisions.Count - 1;
                 index >= 0;
                 index--)
            {
                CollisionPair pair = ignoredVehicleCollisions[index];
                if (!force && PairIsPenetrating(pair))
                {
                    continue;
                }

                if (pair.Item != null && pair.Vehicle != null)
                {
                    Physics.IgnoreCollision(
                        pair.Item,
                        pair.Vehicle,
                        false);
                }

                ignoredVehicleCollisions.RemoveAt(index);
            }
        }

        private static bool PairIsPenetrating(CollisionPair pair)
        {
            if (pair.Item == null || pair.Vehicle == null ||
                !pair.Item.enabled || !pair.Vehicle.enabled ||
                !pair.Item.gameObject.activeInHierarchy ||
                !pair.Vehicle.gameObject.activeInHierarchy)
            {
                return false;
            }

            return Physics.ComputePenetration(
                pair.Item,
                pair.Item.transform.position,
                pair.Item.transform.rotation,
                pair.Vehicle,
                pair.Vehicle.transform.position,
                pair.Vehicle.transform.rotation,
                out _,
                out float distance) && distance > 0.0005f;
        }

        private void HandleStateRestored(WorldItemInstance restoredOwner)
        {
            if (restoredOwner == owner)
            {
                RestoreLiftState();
            }
        }

        private void OnEnable()
        {
            ConfigureInternalLiftHeadCollisions();
        }

        private void OnDisable()
        {
            EndContinuousInteraction();
            ResetPumpLeverPresentation();
        }

        private void OnDestroy()
        {
            if (owner != null)
            {
                owner.StateRestored -= HandleStateRestored;
            }

            TryRestoreIgnoredVehicleCollisions(force: true);
            RestoreIgnoredInternalCollisions();
        }

        private readonly struct CollisionPair
        {
            public CollisionPair(Collider item, Collider vehicle)
            {
                Item = item;
                Vehicle = vehicle;
            }

            public Collider Item { get; }
            public Collider Vehicle { get; }
        }

        private readonly struct InternalCollisionPair
        {
            public InternalCollisionPair(
                Collider head,
                Collider bodyCollider)
            {
                Head = head;
                BodyCollider = bodyCollider;
            }

            public Collider Head { get; }
            public Collider BodyCollider { get; }
        }
    }
}
