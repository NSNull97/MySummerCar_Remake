using UnityEngine;

namespace MSC.Audio
{
    /// <summary>Project-owned acoustic surface metadata for footsteps and impacts.</summary>
    [DisallowMultipleComponent]
    public sealed class AudioSurfaceAuthoring : MonoBehaviour,
        IAudioSurfaceMetadataProvider
    {
        [SerializeField] private AudioSurfaceKind surfaceKind = AudioSurfaceKind.Unknown;
        [SerializeField, Range(0f, 1f)] private float wetness01;
        [SerializeField, Range(0f, 1f)] private float roughness01 = 0.5f;

        public AudioSurfaceMetadata AudioSurface => new AudioSurfaceMetadata(
            surfaceKind,
            wetness01,
            roughness01);

        public AudioSurfaceContext Context =>
            AudioSurfaceMapping.ToSurfaceContext(AudioSurface);

        public void Configure(
            AudioSurfaceKind kind,
            float configuredWetness01 = 0f,
            float configuredRoughness01 = 0.5f)
        {
            surfaceKind = kind;
            wetness01 = AudioMath.Clamp01(configuredWetness01);
            roughness01 = AudioMath.Clamp01(configuredRoughness01);
        }

        public void SetWetness(float value) => wetness01 = AudioMath.Clamp01(value);
    }
}
