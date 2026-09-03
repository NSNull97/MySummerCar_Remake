using UnityEngine;

namespace MSC.World.Presentation
{
    /// <summary>
    /// Publishes one project-owned GPU wind signal for all Phase 1 spruce
    /// foliage. It consumes an existing directional WindZone but never creates
    /// or mutates one, so Enviro remains the sole wind owner.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class Phase1SpruceWindController : MonoBehaviour
    {
        public const string ShaderName = "MSC/HDRP/Spruce Wind";
        public const string DirectionProperty =
            "_MSC_SpruceWindDirection";
        public const string IntensityProperty =
            "_MSC_SpruceWindIntensity";
        public const string TurbulenceProperty =
            "_MSC_SpruceWindTurbulence";

        private static readonly int DirectionId =
            Shader.PropertyToID(DirectionProperty);
        private static readonly int IntensityId =
            Shader.PropertyToID(IntensityProperty);
        private static readonly int TurbulenceId =
            Shader.PropertyToID(TurbulenceProperty);

        [SerializeField] private Vector3 fallbackDirection =
            new Vector3(0.82f, 0f, 0.57f);
        [SerializeField, Range(0f, 1f)] private float fallbackIntensity =
            0.18f;
        [SerializeField, Range(0f, 1f)] private float fallbackTurbulence =
            0.22f;
        [SerializeField, Min(0.05f)] private float evaluationIntervalSeconds =
            0.2f;
        [SerializeField, Min(0.05f)] private float signalResponseSeconds =
            0.65f;

        private WindZone resolvedWindZone;
        private float nextEvaluationTime;
        private Vector3 smoothedDirection;
        private float smoothedIntensity;
        private float smoothedTurbulence;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveWindZone();
            ReadTargetSignal(
                out smoothedDirection,
                out smoothedIntensity,
                out smoothedTurbulence);
            PublishSignal(
                smoothedDirection,
                smoothedIntensity,
                smoothedTurbulence);
            nextEvaluationTime =
                Time.unscaledTime + evaluationIntervalSeconds;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Time.unscaledTime >= nextEvaluationTime)
            {
                if (!IsUsable(resolvedWindZone))
                {
                    ResolveWindZone();
                }
                nextEvaluationTime =
                    Time.unscaledTime + evaluationIntervalSeconds;
            }

            ReadTargetSignal(
                out Vector3 targetDirection,
                out float targetIntensity,
                out float targetTurbulence);
            float blend = CalculateExponentialBlend(
                Time.unscaledDeltaTime,
                signalResponseSeconds);
            smoothedDirection = NormalizeHorizontal(
                Vector3.Lerp(
                    smoothedDirection,
                    targetDirection,
                    blend));
            smoothedIntensity = Mathf.Lerp(
                smoothedIntensity,
                targetIntensity,
                blend);
            smoothedTurbulence = Mathf.Lerp(
                smoothedTurbulence,
                targetTurbulence,
                blend);
            PublishSignal(
                smoothedDirection,
                smoothedIntensity,
                smoothedTurbulence);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Shader.SetGlobalVector(
                DirectionId,
                NormalizeHorizontal(fallbackDirection));
            Shader.SetGlobalFloat(IntensityId, 0f);
            Shader.SetGlobalFloat(TurbulenceId, 0f);
        }

        private void ResolveWindZone()
        {
            resolvedWindZone = null;
            WindZone[] candidates = FindObjectsByType<WindZone>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < candidates.Length; index++)
            {
                WindZone candidate = candidates[index];
                if (!IsUsable(candidate))
                {
                    continue;
                }

                if (resolvedWindZone == null ||
                    candidate.windMain > resolvedWindZone.windMain)
                {
                    resolvedWindZone = candidate;
                }
            }
        }

        private void ReadTargetSignal(
            out Vector3 direction,
            out float intensity,
            out float turbulence)
        {
            if (IsUsable(resolvedWindZone))
            {
                direction = NormalizeHorizontal(
                    resolvedWindZone.transform.forward);
                intensity = Mathf.Clamp01(resolvedWindZone.windMain);
                turbulence = Mathf.Clamp01(
                    resolvedWindZone.windTurbulence);
            }
            else
            {
                direction = NormalizeHorizontal(fallbackDirection);
                intensity = fallbackIntensity;
                turbulence = fallbackTurbulence;
            }
        }

        private static void PublishSignal(
            Vector3 direction,
            float intensity,
            float turbulence)
        {
            Shader.SetGlobalVector(
                DirectionId,
                NormalizeHorizontal(direction));
            Shader.SetGlobalFloat(IntensityId, intensity);
            Shader.SetGlobalFloat(TurbulenceId, turbulence);
        }

        internal static float CalculateExponentialBlend(
            float deltaTime,
            float responseSeconds)
        {
            if (deltaTime <= 0f)
            {
                return 0f;
            }

            float safeResponse = Mathf.Max(responseSeconds, 0.0001f);
            return 1f - Mathf.Exp(-deltaTime / safeResponse);
        }

        private static bool IsUsable(WindZone windZone)
        {
            return windZone != null &&
                   windZone.gameObject.activeInHierarchy &&
                   windZone.mode == WindZoneMode.Directional;
        }

        private static Vector3 NormalizeHorizontal(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return Vector3.forward;
            }
            return direction.normalized;
        }
    }
}
