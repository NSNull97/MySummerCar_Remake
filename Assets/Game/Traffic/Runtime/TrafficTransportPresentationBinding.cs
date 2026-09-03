using System;
using MSC.Audio;
using MSC.Characters;
using UnityEngine;

namespace MSC.Traffic
{
    [DisallowMultipleComponent]
    public sealed class TrafficTransportPresentationBinding : MonoBehaviour
    {
        [SerializeField] private TrafficTransportKind kind;
        [SerializeField] private Rigidbody body;
        [SerializeField] private StoryTrafficVehiclePresentationBinding roadMotion;
        [SerializeField] private Transform passengerSeat;
        [SerializeField] private Transform passengerExit;

        [Header("Bus terminal abandonment")]
        [SerializeField] private bool
            busTerminalAbandonmentPresentationAuthored;
        [SerializeField] private GameObject seatedDriverRoot;
        [SerializeField] private LegacyCharacterPresentationBinding
            driverWalkerPresentation;
        [SerializeField] private Transform driverExit;
        [SerializeField] private Transform serviceDoorPivot;
        [SerializeField] private Vector3 serviceDoorOpenLocalPosition =
            new(0f, -0.606f, 0f);
        [SerializeField] private Quaternion serviceDoorOpenLocalRotation =
            new(0f, 0f, -0.66568637f, 0.7462316f);
        [SerializeField, Min(0.01f)]
        private float serviceDoorOpenDurationRealSeconds = 1f;
        [SerializeField] private GameObject drivingLightsRoot;
        [SerializeField] private GameObject deadLightsRoot;
        [SerializeField, Min(0.1f)] private float driverWalkSpeedMetersPerSecond =
            1.2f;

        private readonly RaycastHit[] driverGroundHits = new RaycastHit[12];
        private Vector3 serviceDoorClosedLocalPosition;
        private Quaternion serviceDoorClosedLocalRotation = Quaternion.identity;
        private Transform driverWalkerFixtureParent;
        private Vector3 driverWalkerFixtureLocalPosition;
        private Quaternion driverWalkerFixtureLocalRotation =
            Quaternion.identity;
        private Transform driverPersistentWorldRoot;
        private bool drivingLightsInitiallyActive;
        private bool deadLightsInitiallyActive;
        private MonoBehaviour driverAudioBackendComponent;
        private IAudioBackend driverAudioBackend;
        private AudioEmitterAuthoring driverAudioEmitter;
        private float driverCurseCooldownRealSeconds;
        private bool busPresentationPoseCaptured;

        public TrafficTransportKind Kind => kind;
        public Rigidbody Body => body;
        public StoryTrafficVehiclePresentationBinding RoadMotion => roadMotion;
        public bool PassengerAboard { get; internal set; }
        public bool HasBusDriverWorldPose => kind == TrafficTransportKind.Bus &&
                                             driverWalkerPresentation != null &&
                                             driverWalkerPresentation.gameObject
                                                 .activeSelf;
        public Vector3 BusDriverWorldPosition => driverWalkerPresentation != null
            ? driverWalkerPresentation.transform.position
            : transform.position;
        public Quaternion BusDriverWorldRotation =>
            driverWalkerPresentation != null
                ? driverWalkerPresentation.transform.rotation
                : transform.rotation;
        public float DriverCurseCooldownRealSeconds =>
            driverCurseCooldownRealSeconds;
        public bool SupportsBusTerminalAbandonment =>
            kind == TrafficTransportKind.Bus &&
            busTerminalAbandonmentPresentationAuthored;

        public void ConfigureForAuthoring(
            TrafficTransportKind configuredKind,
            Rigidbody configuredBody,
            StoryTrafficVehiclePresentationBinding configuredRoadMotion,
            Transform configuredPassengerSeat,
            Transform configuredPassengerExit,
            GameObject configuredSeatedDriverRoot = null,
            LegacyCharacterPresentationBinding configuredDriverWalker = null,
            Transform configuredDriverExit = null,
            Transform configuredServiceDoorPivot = null,
            GameObject configuredDrivingLightsRoot = null,
            GameObject configuredDeadLightsRoot = null,
            Vector3 configuredServiceDoorOpenLocalPosition = default,
            Quaternion configuredServiceDoorOpenLocalRotation = default,
            float configuredServiceDoorOpenDurationRealSeconds = 1f)
        {
            kind = configuredKind;
            body = configuredBody;
            roadMotion = configuredRoadMotion;
            passengerSeat = configuredPassengerSeat;
            passengerExit = configuredPassengerExit;
            // Old generated Phase 1 bus wrappers predate terminal-abandonment
            // presentation. Keeping this authored bit explicit lets those
            // wrappers boot and drive until the deterministic importer is
            // rerun, while newly generated wrappers still fail closed if any
            // required reference is missing.
            busTerminalAbandonmentPresentationAuthored =
                configuredKind == TrafficTransportKind.Bus;
            seatedDriverRoot = configuredSeatedDriverRoot;
            driverWalkerPresentation = configuredDriverWalker;
            driverExit = configuredDriverExit;
            serviceDoorPivot = configuredServiceDoorPivot;
            drivingLightsRoot = configuredDrivingLightsRoot;
            deadLightsRoot = configuredDeadLightsRoot;
            serviceDoorOpenLocalPosition =
                configuredServiceDoorOpenLocalPosition;
            serviceDoorOpenLocalRotation =
                configuredServiceDoorOpenLocalRotation;
            serviceDoorOpenDurationRealSeconds =
                configuredServiceDoorOpenDurationRealSeconds;
            CaptureBusPresentationPose();
        }

        public bool TryValidate(out string failure)
        {
            if (!Enum.IsDefined(typeof(TrafficTransportKind), kind) ||
                body == null ||
                kind == TrafficTransportKind.Bus &&
                (roadMotion == null || passengerSeat == null ||
                 passengerExit == null))
            {
                failure = $"Transport presentation '{name}' is incomplete.";
                return false;
            }

            if (SupportsBusTerminalAbandonment &&
                (seatedDriverRoot == null ||
                 driverWalkerPresentation == null || driverExit == null ||
                 serviceDoorPivot == null || drivingLightsRoot == null ||
                 deadLightsRoot == null ||
                 !IsFinite(serviceDoorOpenLocalPosition) ||
                 !IsFinite(serviceDoorOpenLocalRotation) ||
                 !float.IsFinite(serviceDoorOpenDurationRealSeconds) ||
                 serviceDoorOpenDurationRealSeconds <= 0f))
            {
                failure = $"Transport presentation '{name}' is incomplete.";
                return false;
            }

            if (kind == TrafficTransportKind.Bus &&
                !roadMotion.TryValidate(out failure))
            {
                return false;
            }

            if (SupportsBusTerminalAbandonment &&
                (!driverWalkerPresentation.TryValidate(out failure) ||
                 !driverWalkerPresentation.HasStateBinding(
                     CharacterActivityState.Idle) ||
                 !driverWalkerPresentation.HasStateBinding(
                     CharacterActivityState.Walking) ||
                 driverWalkerPresentation.transform == transform ||
                 !driverWalkerPresentation.transform.IsChildOf(transform) ||
                 !seatedDriverRoot.transform.IsChildOf(transform) ||
                 !driverExit.IsChildOf(transform) ||
                 !serviceDoorPivot.IsChildOf(transform) ||
                 !drivingLightsRoot.transform.IsChildOf(transform) ||
                 !deadLightsRoot.transform.IsChildOf(transform)))
            {
                if (string.IsNullOrWhiteSpace(failure))
                {
                    failure =
                        $"Transport presentation '{name}' has bus-driver references outside its wrapper.";
                }
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public void ConfigureBusDriverRuntime(
            MonoBehaviour configuredAudioBackendComponent,
            float savedCurseCooldownRealSeconds,
            Transform configuredPersistentWorldRoot)
        {
            if (!SupportsBusTerminalAbandonment ||
                driverWalkerPresentation == null)
            {
                return;
            }

            if (configuredPersistentWorldRoot == null)
            {
                throw new ArgumentNullException(
                    nameof(configuredPersistentWorldRoot));
            }

            driverPersistentWorldRoot = configuredPersistentWorldRoot;
            driverCurseCooldownRealSeconds = Mathf.Max(
                0f,
                savedCurseCooldownRealSeconds);
            driverAudioBackendComponent = configuredAudioBackendComponent;
            driverAudioBackend = configuredAudioBackendComponent as
                IAudioBackend;
            driverAudioEmitter = driverWalkerPresentation.GetComponent<
                                     AudioEmitterAuthoring>() ??
                                 driverWalkerPresentation.gameObject
                                     .AddComponent<AudioEmitterAuthoring>();
            driverAudioEmitter.Configure(
                "audio.emitter.npc.latanen.bus-abandonment",
                driverAudioBackendComponent,
                driverWalkerPresentation.transform);
        }

        public void ApplyBusAbandonmentPhase(
            BusTerminalAbandonmentPhase phase,
            float shutdownDelayRealSecondsRemaining,
            bool hasSavedDriverPose,
            Vector3 savedDriverWorldPosition,
            Quaternion savedDriverWorldRotation)
        {
            if (!SupportsBusTerminalAbandonment)
            {
                roadMotion?.SetTerminalAbandonmentHold(false);
                return;
            }

            CaptureBusPresentationPose();
            bool terminal = phase >=
                            BusTerminalAbandonmentPhase.ShutdownDelay;
            roadMotion?.SetTerminalAbandonmentHold(terminal);
            if (!terminal)
            {
                seatedDriverRoot.SetActive(true);
                driverWalkerPresentation.ApplyState(
                    CharacterActivityState.Hidden);
                RestoreWalkerToFixture();
                serviceDoorPivot.localPosition =
                    serviceDoorClosedLocalPosition;
                serviceDoorPivot.localRotation =
                    serviceDoorClosedLocalRotation;
                drivingLightsRoot.SetActive(
                    drivingLightsInitiallyActive);
                deadLightsRoot.SetActive(deadLightsInitiallyActive);
                return;
            }

            float doorOpen01 = TrafficTransportBehaviorRules
                .ResolveBusServiceDoorOpen01(
                    phase,
                    shutdownDelayRealSecondsRemaining,
                    serviceDoorOpenDurationRealSeconds);
            serviceDoorPivot.localPosition = Vector3.LerpUnclamped(
                serviceDoorClosedLocalPosition,
                serviceDoorOpenLocalPosition,
                doorOpen01);
            serviceDoorPivot.localRotation = Quaternion.SlerpUnclamped(
                serviceDoorClosedLocalRotation,
                serviceDoorOpenLocalRotation,
                doorOpen01);
            drivingLightsRoot.SetActive(false);
            deadLightsRoot.SetActive(true);

            if (phase == BusTerminalAbandonmentPhase.ShutdownDelay)
            {
                seatedDriverRoot.SetActive(true);
                driverWalkerPresentation.ApplyState(
                    CharacterActivityState.Hidden);
                return;
            }

            seatedDriverRoot.SetActive(false);
            Transform walker = driverWalkerPresentation.transform;
            Transform persistentRoot = driverPersistentWorldRoot;
            if (persistentRoot == null)
            {
                Debug.LogError(
                    "Bus driver cannot leave the bus without an explicit persistent world root.",
                    this);
                return;
            }

            walker.SetParent(persistentRoot, true);
            if (hasSavedDriverPose && IsFinite(savedDriverWorldPosition) &&
                IsFinite(savedDriverWorldRotation))
            {
                walker.SetPositionAndRotation(
                    savedDriverWorldPosition,
                    savedDriverWorldRotation);
            }
            else
            {
                walker.SetPositionAndRotation(
                    driverExit.position,
                    driverExit.rotation);
            }

            if (!driverWalkerPresentation.ApplyState(
                    CharacterActivityState.Walking))
            {
                Debug.LogError(
                    "Bus driver walker is missing the explicit Walking clip binding.",
                    this);
            }
        }

        public bool AdvanceAbandonedBusDriver(
            float realDeltaSeconds,
            Transform player)
        {
            if (!HasBusDriverWorldPose)
            {
                return false;
            }

            float delta = Mathf.Max(0f, realDeltaSeconds);
            Transform walker = driverWalkerPresentation.transform;
            Vector3 forward = Vector3.ProjectOnPlane(
                walker.forward,
                Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            if (IsDriverPathBlocked(walker.position, forward))
            {
                walker.rotation = Quaternion.AngleAxis(55f, Vector3.up) *
                                  walker.rotation;
                forward = Vector3.ProjectOnPlane(
                    walker.forward,
                    Vector3.up).normalized;
            }

            Vector3 candidate = walker.position +
                                forward *
                                (driverWalkSpeedMetersPerSecond * delta);
            if (TryResolveDriverGround(candidate, out Vector3 grounded))
            {
                candidate = grounded;
            }

            walker.position = candidate;

            driverCurseCooldownRealSeconds = Mathf.Max(
                0f,
                driverCurseCooldownRealSeconds - delta);
            if (player == null || driverCurseCooldownRealSeconds > 0f ||
                (player.position - walker.position).sqrMagnitude >
                TrafficTransportBehaviorRules.BusDriverCurseDistanceMeters *
                TrafficTransportBehaviorRules.BusDriverCurseDistanceMeters ||
                driverAudioBackend == null ||
                !driverAudioBackend.IsReady || driverAudioEmitter == null)
            {
                return false;
            }

            var request = new AudioEventRequest(
                AudioProjectIds.Events.BusDriverStuckCurse,
                driverAudioEmitter,
                volume01: 1f,
                allowMultiple: false);
            IAudioEventHandle handle = driverAudioBackend.PostEvent(in request);
            if (handle == null || !handle.IsValid)
            {
                return false;
            }

            driverCurseCooldownRealSeconds =
                TrafficTransportBehaviorRules.BusDriverCurseRepeatRealSeconds;
            return true;
        }

        private void Awake()
        {
            CaptureBusPresentationPose();
        }

        private void CaptureBusPresentationPose()
        {
            if (busPresentationPoseCaptured ||
                kind != TrafficTransportKind.Bus ||
                serviceDoorPivot == null || driverExit == null)
            {
                return;
            }

            serviceDoorClosedLocalRotation =
                serviceDoorPivot.localRotation;
            serviceDoorClosedLocalPosition =
                serviceDoorPivot.localPosition;
            driverWalkerFixtureParent = driverWalkerPresentation != null
                ? driverWalkerPresentation.transform.parent
                : null;
            if (driverWalkerPresentation != null)
            {
                driverWalkerFixtureLocalPosition =
                    driverWalkerPresentation.transform.localPosition;
                driverWalkerFixtureLocalRotation =
                    driverWalkerPresentation.transform.localRotation;
            }
            drivingLightsInitiallyActive =
                drivingLightsRoot != null && drivingLightsRoot.activeSelf;
            deadLightsInitiallyActive =
                deadLightsRoot != null && deadLightsRoot.activeSelf;
            busPresentationPoseCaptured = true;
        }

        private void RestoreWalkerToFixture()
        {
            if (driverWalkerPresentation == null ||
                driverWalkerFixtureParent == null)
            {
                return;
            }

            Transform walker = driverWalkerPresentation.transform;
            walker.SetParent(driverWalkerFixtureParent, false);
            walker.localPosition = driverWalkerFixtureLocalPosition;
            walker.localRotation = driverWalkerFixtureLocalRotation;
        }

        private void OnDestroy()
        {
            if (driverWalkerPresentation == null)
            {
                return;
            }

            Transform walker = driverWalkerPresentation.transform;
            if (walker == null || walker.IsChildOf(transform))
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(walker.gameObject);
            }
            else
            {
                DestroyImmediate(walker.gameObject);
            }
        }

        private bool IsDriverPathBlocked(Vector3 position, Vector3 forward)
        {
            int count = Physics.SphereCastNonAlloc(
                position + Vector3.up * 0.75f,
                0.28f,
                forward,
                driverGroundHits,
                0.8f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Collider collider = driverGroundHits[index].collider;
                if (collider != null &&
                    !collider.transform.IsChildOf(transform) &&
                    !collider.transform.IsChildOf(
                        driverWalkerPresentation.transform))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveDriverGround(
            Vector3 candidate,
            out Vector3 grounded)
        {
            int count = Physics.RaycastNonAlloc(
                candidate + Vector3.up * 2.5f,
                Vector3.down,
                driverGroundHits,
                6f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Vector3 point = candidate;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = driverGroundHits[index];
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.transform.IsChildOf(
                        driverWalkerPresentation.transform) ||
                    hit.distance >= nearest)
                {
                    continue;
                }

                nearest = hit.distance;
                point = hit.point + Vector3.up * 0.03f;
            }

            grounded = point;
            return float.IsFinite(nearest);
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w) &&
            value.x * value.x + value.y * value.y + value.z * value.z +
            value.w * value.w > 0.0001f;

        public void SetKinematicPose(Vector3 position, Quaternion rotation)
        {
            if (body == null)
            {
                return;
            }

            // Donor train SpawnEast/SpawnWest rotations point its visible
            // front exactly opposite the route velocity. The route direction
            // is already correct; only the imported presentation requires the
            // original 180-degree local yaw.
            if (kind == TrafficTransportKind.Train)
            {
                rotation *= Quaternion.Euler(0f, 180f, 0f);
            }

            body.MovePosition(position);
            body.MoveRotation(rotation);
        }

        public void DriveBoatTowards(
            Vector3 guidancePosition,
            Quaternion guidanceRotation,
            float targetSpeedMetersPerSecond)
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            Vector3 desiredForward = guidanceRotation * Vector3.forward;
            desiredForward.y = 0f;
            if (desiredForward.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            desiredForward.Normalize();
            Vector3 horizontalVelocity = body.linearVelocity;
            horizontalVelocity.y = 0f;
            float forwardSpeed = Vector3.Dot(horizontalVelocity, transform.forward);
            float speedError = targetSpeedMetersPerSecond - forwardSpeed;
            body.AddForce(
                transform.forward * speedError * body.mass * 1.8f,
                ForceMode.Force);

            float signedYaw = Vector3.SignedAngle(
                transform.forward,
                desiredForward,
                Vector3.up);
            body.AddTorque(
                Vector3.up * signedYaw * body.mass * 0.035f -
                Vector3.up * body.angularVelocity.y * body.mass * 0.8f,
                ForceMode.Force);

            float verticalError = guidancePosition.y - body.position.y;
            body.AddForce(
                Vector3.up * (verticalError * body.mass * 8f -
                              body.linearVelocity.y * body.mass * 3f),
                ForceMode.Force);
        }

        internal Transform PassengerSeat => passengerSeat;
        internal Transform PassengerExit => passengerExit;
    }

}
