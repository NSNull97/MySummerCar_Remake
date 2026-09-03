using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Explicit world-space interior fog void. It uses HDRP 17 Min blending and
    /// never changes the global Fog override, preserving exterior fog through
    /// windows and open doors.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LocalVolumetricFog))]
    public sealed class WeatherFogVoidAuthoring : MonoBehaviour
    {
        [SerializeField] private WeatherZone weatherZone;
        [SerializeField] private Vector3 sizeMeters = new Vector3(4f, 2.5f, 4f);
        [SerializeField, Min(0f)] private float blendDistanceMeters = 0.8f;
        [SerializeField, Min(1f)] private float clearMeanFreePathMeters = 1000000f;

        private LocalVolumetricFog localFog;

        public WeatherZone WeatherZone => weatherZone;
        public Vector3 SizeMeters => sizeMeters;
        public float BlendDistanceMeters => blendDistanceMeters;

        private void OnEnable()
        {
            localFog = GetComponent<LocalVolumetricFog>();
            ApplyParameters();
        }

        private void OnValidate()
        {
            sizeMeters.x = Mathf.Max(0.1f, sizeMeters.x);
            sizeMeters.y = Mathf.Max(0.1f, sizeMeters.y);
            sizeMeters.z = Mathf.Max(0.1f, sizeMeters.z);
            blendDistanceMeters = Mathf.Max(0f, blendDistanceMeters);
            clearMeanFreePathMeters = Mathf.Max(1f, clearMeanFreePathMeters);
            localFog = GetComponent<LocalVolumetricFog>();
            ApplyParameters();
        }

        public void ApplyParameters()
        {
            if (localFog == null)
            {
                return;
            }

            LocalVolumetricFogArtistParameters parameters = localFog.parameters;
            parameters.blendingMode = LocalVolumetricFogBlendingMode.Min;
            parameters.meanFreePath = clearMeanFreePathMeters;
            parameters.albedo = Color.white;
            parameters.size = sizeMeters;
            parameters.scaleMode = LocalVolumetricFogScaleMode.ScaleInvariant;
            parameters.falloffMode = LocalVolumetricFogFalloffMode.Exponential;
            Vector3 fade = new Vector3(
                Mathf.Clamp01(blendDistanceMeters / sizeMeters.x),
                Mathf.Clamp01(blendDistanceMeters / sizeMeters.y),
                Mathf.Clamp01(blendDistanceMeters / sizeMeters.z));
            parameters.positiveFade = fade;
            parameters.negativeFade = fade;
            parameters.distanceFadeStart = 10000f;
            parameters.distanceFadeEnd = 10000f;
            localFog.parameters = parameters;
        }

        public void Configure(
            WeatherZone authoredZone,
            Vector3 authoredSizeMeters,
            float authoredBlendDistanceMeters)
        {
            weatherZone = authoredZone;
            sizeMeters = authoredSizeMeters;
            blendDistanceMeters = authoredBlendDistanceMeters;
            localFog = GetComponent<LocalVolumetricFog>();
            ApplyParameters();
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            WeatherZone authoredZone,
            Vector3 authoredSizeMeters,
            float authoredBlendDistanceMeters) =>
            Configure(
                authoredZone,
                authoredSizeMeters,
                authoredBlendDistanceMeters);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 1f, 0.35f);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, sizeMeters);
            Gizmos.matrix = previous;
        }
#endif
    }
}
