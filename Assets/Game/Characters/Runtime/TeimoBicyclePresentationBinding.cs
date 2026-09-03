using System;
using MSC.Core.Time;
using UnityEngine;

namespace MSC.Characters
{
    /// <summary>
    /// Replaceable Phase 1 presentation for Teimo's moving bicycle parts and
    /// greeting. Route, schedule, player visibility and persisted cooldown
    /// authority remain project-owned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TeimoBicyclePresentationBinding : MonoBehaviour
    {
        public const string GreetingCooldownLineId =
            "line.teimo.bicycle-wave-greeting";

        private const double GameSecondsPerDay = 86400d;
        private const float MaximumTrackedFrameTravelMeters = 2f;

        [SerializeField] private LegacyCharacterPresentationBinding
            characterPresentation;
        [SerializeField] private Transform pedals;
        [SerializeField] private Transform frontWheel;
        [SerializeField] private Transform rearWheel;
        [SerializeField] private Transform leftHip;
        [SerializeField] private Transform leftKnee;
        [SerializeField] private Transform leftAnkle;
        [SerializeField] private Transform rightHip;
        [SerializeField] private Transform rightKnee;
        [SerializeField] private Transform rightAnkle;
        [SerializeField] private float pedalHalfWidthMeters = 0.158972f;
        [SerializeField] private float pedalRadiusMeters = 0.180836f;
        [SerializeField] private float wheelDegreesPerMeter = 170f;
        [SerializeField] private float pedalSpeedDivisor = 3.5f;
        [SerializeField] private float greetingDistanceMeters = 5f;
        [SerializeField] private float groundContactCalibrationMeters;

        private CharacterInstance characterInstance;
        private Transform player;
        private IGameTimeService gameTime;
        private Vector3 previousPosition;
        private bool hasPreviousPosition;

        public Transform Pedals => pedals;
        public Transform FrontWheel => frontWheel;
        public Transform RearWheel => rearWheel;
        public Transform LeftHip => leftHip;
        public Transform LeftKnee => leftKnee;
        public Transform LeftAnkle => leftAnkle;
        public Transform RightHip => rightHip;
        public Transform RightKnee => rightKnee;
        public Transform RightAnkle => rightAnkle;
        public float PedalHalfWidthMeters => pedalHalfWidthMeters;
        public float PedalRadiusMeters => pedalRadiusMeters;
        public float WheelDegreesPerMeter => wheelDegreesPerMeter;
        public float PedalSpeedDivisor => pedalSpeedDivisor;
        public float GreetingDistanceMeters => greetingDistanceMeters;
        public float GroundContactCalibrationMeters =>
            groundContactCalibrationMeters;

        public void ConfigureRuntime(
            CharacterInstance configuredCharacterInstance,
            Transform configuredPlayer,
            IGameTimeService configuredGameTime)
        {
            characterInstance = configuredCharacterInstance ??
                throw new ArgumentNullException(
                    nameof(configuredCharacterInstance));
            player = configuredPlayer;
            gameTime = configuredGameTime ??
                throw new ArgumentNullException(nameof(configuredGameTime));
            previousPosition = transform.position;
            hasPreviousPosition = true;
        }

        public bool TryValidate(out string failure)
        {
            if (characterPresentation == null ||
                characterPresentation.gameObject != gameObject)
            {
                failure =
                    "Teimo bicycle motion requires its project-owned character presenter on the same wrapper.";
                return false;
            }

            if (pedals == null || frontWheel == null || rearWheel == null ||
                pedals == frontWheel || pedals == rearWheel ||
                frontWheel == rearWheel ||
                !pedals.IsChildOf(transform) ||
                !frontWheel.IsChildOf(transform) ||
                !rearWheel.IsChildOf(transform))
            {
                failure =
                    "Teimo bicycle motion has missing, duplicate or external rotating transforms.";
                return false;
            }

            if (!IsValidLegChain(leftHip, leftKnee, leftAnkle) ||
                !IsValidLegChain(rightHip, rightKnee, rightAnkle) ||
                leftHip == rightHip || leftKnee == rightKnee ||
                leftAnkle == rightAnkle)
            {
                failure =
                    "Teimo bicycle motion has an invalid or shared rider leg chain.";
                return false;
            }

            if (!float.IsFinite(wheelDegreesPerMeter) ||
                wheelDegreesPerMeter <= 0f ||
                !float.IsFinite(pedalSpeedDivisor) ||
                pedalSpeedDivisor <= 0f ||
                !float.IsFinite(pedalHalfWidthMeters) ||
                pedalHalfWidthMeters <= 0f ||
                !float.IsFinite(pedalRadiusMeters) ||
                pedalRadiusMeters <= 0f ||
                !float.IsFinite(greetingDistanceMeters) ||
                greetingDistanceMeters <= 0f ||
                !float.IsFinite(groundContactCalibrationMeters) ||
                groundContactCalibrationMeters < 0f ||
                groundContactCalibrationMeters > 1f)
            {
                failure =
                    "Teimo bicycle motion contains invalid speed, greeting or ground-contact calibration.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public void PresentTravelDistance(float distanceMeters)
        {
            if (!float.IsFinite(distanceMeters) || distanceMeters < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distanceMeters));
            }

            float wheelDegrees = distanceMeters * wheelDegreesPerMeter;
            float pedalDegrees = wheelDegrees / pedalSpeedDivisor;
            frontWheel.Rotate(Vector3.right, wheelDegrees, Space.Self);
            rearWheel.Rotate(Vector3.right, wheelDegrees, Space.Self);
            pedals.Rotate(Vector3.right, pedalDegrees, Space.Self);
            PresentPedalingPose();
        }

        public bool TryPresentGreeting()
        {
            if (characterInstance == null || player == null ||
                gameTime == null || characterPresentation == null ||
                characterPresentation.CurrentState !=
                    CharacterActivityState.VehicleSeated ||
                string.IsNullOrEmpty(characterInstance.CurrentRouteId))
            {
                return false;
            }

            GameTimeSnapshot snapshot = gameTime.Snapshot;
            if (!characterInstance.IsDialogueLineEligible(
                    GreetingCooldownLineId,
                    snapshot.ElapsedGameSeconds))
            {
                return false;
            }

            Vector3 sightOrigin = transform.position + Vector3.up * 1.45f;
            Vector3 playerTarget = player.position + Vector3.up * 1.45f;
            if (Vector3.Distance(sightOrigin, playerTarget) >
                    greetingDistanceMeters ||
                IsOccluded(sightOrigin, playerTarget) ||
                !characterPresentation.TryPlayOneShot(
                    CharacterActivityState.Talking))
            {
                return false;
            }

            double untilNextDay = Math.Max(
                0.001d,
                GameSecondsPerDay - snapshot.SecondsOfDay);
            characterInstance.RecordDialogueLine(
                GreetingCooldownLineId,
                snapshot.ElapsedGameSeconds,
                untilNextDay);
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            LegacyCharacterPresentationBinding configuredPresentation,
            Transform configuredPedals,
            Transform configuredFrontWheel,
            Transform configuredRearWheel,
            Transform configuredLeftHip,
            Transform configuredLeftKnee,
            Transform configuredLeftAnkle,
            Transform configuredRightHip,
            Transform configuredRightKnee,
            Transform configuredRightAnkle,
            float configuredPedalHalfWidthMeters,
            float configuredPedalRadiusMeters,
            float configuredWheelDegreesPerMeter,
            float configuredPedalSpeedDivisor,
            float configuredGreetingDistanceMeters,
            float configuredGroundContactCalibrationMeters)
        {
            characterPresentation = configuredPresentation;
            pedals = configuredPedals;
            frontWheel = configuredFrontWheel;
            rearWheel = configuredRearWheel;
            leftHip = configuredLeftHip;
            leftKnee = configuredLeftKnee;
            leftAnkle = configuredLeftAnkle;
            rightHip = configuredRightHip;
            rightKnee = configuredRightKnee;
            rightAnkle = configuredRightAnkle;
            pedalHalfWidthMeters = configuredPedalHalfWidthMeters;
            pedalRadiusMeters = configuredPedalRadiusMeters;
            wheelDegreesPerMeter = configuredWheelDegreesPerMeter;
            pedalSpeedDivisor = configuredPedalSpeedDivisor;
            greetingDistanceMeters = configuredGreetingDistanceMeters;
            groundContactCalibrationMeters =
                configuredGroundContactCalibrationMeters;
        }
#endif

        private void OnEnable()
        {
            previousPosition = transform.position;
            hasPreviousPosition = true;
        }

        private void LateUpdate()
        {
            Vector3 currentPosition = transform.position;
            if (hasPreviousPosition && characterPresentation != null &&
                characterPresentation.CurrentState ==
                    CharacterActivityState.VehicleSeated &&
                (gameTime == null || !gameTime.Snapshot.IsPaused))
            {
                float distance = Vector3.Distance(
                    previousPosition,
                    currentPosition);
                if (distance > 0f &&
                    distance <= MaximumTrackedFrameTravelMeters)
                {
                    PresentTravelDistance(distance);
                }
            }

            previousPosition = currentPosition;
            hasPreviousPosition = true;
            if (characterPresentation != null &&
                characterPresentation.CurrentState ==
                    CharacterActivityState.VehicleSeated)
            {
                PresentPedalingPose();
            }

            TryPresentGreeting();
        }

        private void PresentPedalingPose()
        {
            if (pedals == null ||
                !IsValidLegChain(leftHip, leftKnee, leftAnkle) ||
                !IsValidLegChain(rightHip, rightKnee, rightAnkle))
            {
                return;
            }

            Vector3 pedalA = pedals.TransformPoint(
                new Vector3(
                    pedalHalfWidthMeters,
                    0f,
                    pedalRadiusMeters));
            Vector3 pedalB = pedals.TransformPoint(
                new Vector3(
                    -pedalHalfWidthMeters,
                    0f,
                    -pedalRadiusMeters));
            bool aBelongsToLeft =
                (leftHip.position - pedalA).sqrMagnitude +
                (rightHip.position - pedalB).sqrMagnitude <=
                (leftHip.position - pedalB).sqrMagnitude +
                (rightHip.position - pedalA).sqrMagnitude;

            SolveLeg(
                leftHip,
                leftKnee,
                leftAnkle,
                aBelongsToLeft ? pedalA : pedalB);
            SolveLeg(
                rightHip,
                rightKnee,
                rightAnkle,
                aBelongsToLeft ? pedalB : pedalA);
        }

        private static void SolveLeg(
            Transform hip,
            Transform knee,
            Transform ankle,
            Vector3 target)
        {
            Vector3 hipPosition = hip.position;
            Vector3 kneePosition = knee.position;
            Vector3 anklePosition = ankle.position;
            float upperLength = Vector3.Distance(hipPosition, kneePosition);
            float lowerLength = Vector3.Distance(kneePosition, anklePosition);
            Vector3 toTarget = target - hipPosition;
            float targetDistance = Mathf.Clamp(
                toTarget.magnitude,
                Mathf.Abs(upperLength - lowerLength) + 0.001f,
                upperLength + lowerLength - 0.001f);
            if (upperLength <= 0.001f || lowerLength <= 0.001f ||
                toTarget.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 forward = toTarget.normalized;
            Vector3 currentBend = kneePosition - hipPosition;
            Vector3 bendNormal = Vector3.Cross(
                anklePosition - hipPosition,
                currentBend);
            if (bendNormal.sqrMagnitude <= 0.000001f)
            {
                bendNormal = Vector3.Cross(forward, hip.forward);
            }

            if (bendNormal.sqrMagnitude <= 0.000001f)
            {
                bendNormal = Vector3.Cross(forward, Vector3.up);
            }

            bendNormal.Normalize();
            Vector3 bendDirection = Vector3.Cross(bendNormal, forward).normalized;
            float adjacent =
                (upperLength * upperLength - lowerLength * lowerLength +
                 targetDistance * targetDistance) /
                (2f * targetDistance);
            float height = Mathf.Sqrt(Mathf.Max(
                0f,
                upperLength * upperLength - adjacent * adjacent));
            Vector3 desiredKnee =
                hipPosition + forward * adjacent + bendDirection * height;

            Quaternion hipDelta = Quaternion.FromToRotation(
                kneePosition - hipPosition,
                desiredKnee - hipPosition);
            hip.rotation = hipDelta * hip.rotation;

            kneePosition = knee.position;
            anklePosition = ankle.position;
            Quaternion kneeDelta = Quaternion.FromToRotation(
                anklePosition - kneePosition,
                target - kneePosition);
            knee.rotation = kneeDelta * knee.rotation;
        }

        private bool IsValidLegChain(
            Transform hip,
            Transform knee,
            Transform ankle) =>
            hip != null && knee != null && ankle != null &&
            hip.IsChildOf(transform) &&
            knee.IsChildOf(hip) &&
            ankle.IsChildOf(knee);

        private static bool IsOccluded(Vector3 origin, Vector3 target)
        {
            int worldSurface = LayerMask.NameToLayer("WorldSurface");
            int worldSolid = LayerMask.NameToLayer("WorldSolid");
            int layerMask = LayerBit(worldSurface) | LayerBit(worldSolid);
            return layerMask != 0 && Physics.Linecast(
                origin,
                target,
                layerMask,
                QueryTriggerInteraction.Ignore);
        }

        private static int LayerBit(int layer) =>
            layer >= 0 ? 1 << layer : 0;
    }
}
