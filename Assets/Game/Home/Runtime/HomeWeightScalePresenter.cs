using System;
using MSC.Needs;
using UnityEngine;

namespace MSC.Home
{
    /// <summary>
    /// Project-owned bathroom-scale behavior. Gameplay weight comes from the
    /// needs domain; a removable legacy mesh is only the current presenter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeWeightScalePresenter : MonoBehaviour
    {
        public const float DonorGaugeDegreesPerKilogram = -2.78f;

        [SerializeField, Min(0.01f)]
        private float horizontalDetectionRadiusMeters = 0.24f;

        [SerializeField, Min(0.01f)]
        private float verticalDetectionToleranceMeters = 0.45f;

        [SerializeField, Min(0f)]
        private float gaugeTransitionSeconds = 2f;

        private PlayerNeedsRuntime needs;
        private Transform player;
        private Transform gaugePivot;
        private Vector3 detectionCenterWorld;
        private Quaternion gaugeBaseLocalRotation = Quaternion.identity;
        private float currentGaugeAngleDegrees;
        private float transitionStartAngleDegrees;
        private float transitionTargetAngleDegrees;
        private float transitionElapsedSeconds;
        private float weightKilograms = 83f;
        private bool initialized;

        public bool IsOccupied { get; private set; }
        public bool HasGaugeBinding => gaugePivot != null;
        public float CurrentGaugeAngleDegrees => currentGaugeAngleDegrees;

        public void Initialize(
            PlayerNeedsRuntime configuredNeeds,
            Transform configuredPlayer,
            Vector3 configuredDetectionCenterWorld)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Home weight scale presenter is already initialized.");
            }

            needs = configuredNeeds ??
                throw new ArgumentNullException(nameof(configuredNeeds));
            player = configuredPlayer ??
                throw new ArgumentNullException(nameof(configuredPlayer));
            detectionCenterWorld = configuredDetectionCenterWorld;
            weightKilograms = needs.Snapshot.WeightKilograms;
            needs.StateChanged += HandleNeedsChanged;
            initialized = true;
            RefreshOccupancyAndTarget(forceTransition: true);
        }

        public void BindGauge(Transform configuredGaugePivot)
        {
            if (configuredGaugePivot == null)
            {
                throw new ArgumentNullException(nameof(configuredGaugePivot));
            }

            gaugePivot = configuredGaugePivot;
            gaugeBaseLocalRotation = gaugePivot.localRotation;
            currentGaugeAngleDegrees = 0f;
            transitionStartAngleDegrees = 0f;
            transitionTargetAngleDegrees = 0f;
            transitionElapsedSeconds = 0f;
            RefreshOccupancyAndTarget(forceTransition: true);
            ApplyGaugeRotation();
        }

        public void UnbindGauge(Transform expectedGaugePivot = null)
        {
            if (expectedGaugePivot != null && gaugePivot != expectedGaugePivot)
            {
                return;
            }

            gaugePivot = null;
        }

        public static float CalculateGaugeAngleDegrees(float kilograms) =>
            Mathf.Max(0f, kilograms) * DonorGaugeDegreesPerKilogram;

        public static bool IsPlayerWithinDetectionVolume(
            Vector3 playerPosition,
            Vector3 detectionCenter,
            float horizontalRadiusMeters,
            float verticalToleranceMeters)
        {
            Vector2 horizontalDelta = new Vector2(
                playerPosition.x - detectionCenter.x,
                playerPosition.z - detectionCenter.z);
            return horizontalDelta.sqrMagnitude <=
                    horizontalRadiusMeters * horizontalRadiusMeters &&
                Mathf.Abs(playerPosition.y - detectionCenter.y) <=
                    verticalToleranceMeters;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            RefreshOccupancyAndTarget(forceTransition: false);
            if (Mathf.Approximately(
                    currentGaugeAngleDegrees,
                    transitionTargetAngleDegrees))
            {
                return;
            }

            transitionElapsedSeconds += Mathf.Max(0f, Time.deltaTime);
            float progress = gaugeTransitionSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(
                    transitionElapsedSeconds / gaugeTransitionSeconds);
            currentGaugeAngleDegrees = Mathf.Lerp(
                transitionStartAngleDegrees,
                transitionTargetAngleDegrees,
                progress);
            ApplyGaugeRotation();
        }

        private void HandleNeedsChanged(PlayerNeedsSnapshot snapshot)
        {
            if (Mathf.Approximately(
                    weightKilograms,
                    snapshot.WeightKilograms))
            {
                return;
            }

            weightKilograms = snapshot.WeightKilograms;
            if (IsOccupied)
            {
                BeginTransition(CalculateGaugeAngleDegrees(weightKilograms));
            }
        }

        private void RefreshOccupancyAndTarget(bool forceTransition)
        {
            bool occupied = player != null &&
                IsPlayerWithinDetectionVolume(
                    player.position,
                    detectionCenterWorld,
                    horizontalDetectionRadiusMeters,
                    verticalDetectionToleranceMeters);
            if (!forceTransition && occupied == IsOccupied)
            {
                return;
            }

            IsOccupied = occupied;
            BeginTransition(
                occupied
                    ? CalculateGaugeAngleDegrees(weightKilograms)
                    : 0f);
        }

        private void BeginTransition(float targetAngleDegrees)
        {
            transitionStartAngleDegrees = currentGaugeAngleDegrees;
            transitionTargetAngleDegrees = targetAngleDegrees;
            transitionElapsedSeconds = 0f;
        }

        private void ApplyGaugeRotation()
        {
            if (gaugePivot != null)
            {
                gaugePivot.localRotation =
                    gaugeBaseLocalRotation *
                    Quaternion.Euler(0f, 0f, currentGaugeAngleDegrees);
            }
        }

        private void OnDestroy()
        {
            if (needs != null)
            {
                needs.StateChanged -= HandleNeedsChanged;
            }
        }
    }
}
