using System;
using MSC.Weather.Presentation;
using MSC.Weather.Wetness;
using UnityEngine;

namespace MSC.Development.WeatherLab
{
    /// <summary>
    /// WeatherLab-only HDRP/Lit proof. It uses shared shader globals and one reused
    /// property block; it never requests Renderer.material.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeatherLabWetnessMaterialBridge : MonoBehaviour, IWetnessShaderBridge
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int PuddleAmountId = Shader.PropertyToID("_MSC_PuddleAmount");
        private static readonly int PuddleRipplePhaseId = Shader.PropertyToID("_MSC_PuddleRipplePhase");
        private const float RippleUpdateIntervalSeconds = 1f / 12f;

        [SerializeField] private Renderer[] groundSamples = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] roadSamples = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] exteriorSamples = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] vehicleSamples = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] vegetationSamples = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] puddleSamples = Array.Empty<Renderer>();

        private readonly GlobalWetnessShaderBridge globalBridge = new GlobalWetnessShaderBridge();
        private MaterialPropertyBlock propertyBlock;
        private Sample[] ground;
        private Sample[] road;
        private Sample[] exterior;
        private Sample[] vehicle;
        private Sample[] vegetation;
        private Sample[] puddle;
        private uint lastAppliedRevision;
        private uint rippleUpdateCount;
        private float puddleAmount01;
        private float ripplePhase01;
        private float nextRippleUpdateTime;

        public uint LastAppliedRevision => lastAppliedRevision;

        public int ConfiguredRendererCount =>
            Count(groundSamples) + Count(roadSamples) + Count(exteriorSamples) +
            Count(vehicleSamples) + Count(vegetationSamples) + Count(puddleSamples);

        public uint RippleUpdateCount => rippleUpdateCount;

        public float PuddleRipplePhase01 => ripplePhase01;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            Renderer[] authoredGround,
            Renderer[] authoredRoad,
            Renderer[] authoredExterior,
            Renderer[] authoredVehicle,
            Renderer[] authoredVegetation,
            Renderer[] authoredPuddle)
        {
            groundSamples = authoredGround ?? Array.Empty<Renderer>();
            roadSamples = authoredRoad ?? Array.Empty<Renderer>();
            exteriorSamples = authoredExterior ?? Array.Empty<Renderer>();
            vehicleSamples = authoredVehicle ?? Array.Empty<Renderer>();
            vegetationSamples = authoredVegetation ?? Array.Empty<Renderer>();
            puddleSamples = authoredPuddle ?? Array.Empty<Renderer>();
        }
#endif

        public void Apply(in WetnessEnvironmentOutputs outputs)
        {
            EnsureInitialized();
            if (outputs.Revision == 0U || outputs.Revision == lastAppliedRevision)
            {
                return;
            }

            globalBridge.Apply(outputs);
            ApplySamples(ground, outputs.GroundWetness01, 0.72f, 0.62f);
            ApplySamples(road, outputs.RoadWetness01, 0.78f, 0.52f);
            ApplySamples(exterior, outputs.GroundWetness01 * 0.72f, 0.7f, 0.72f);
            ApplySamples(vehicle, outputs.GroundWetness01 * 0.55f, 0.86f, 0.82f);
            ApplySamples(vegetation, outputs.VegetationWetness01, 0.58f, 0.68f);
            puddleAmount01 = Mathf.Clamp01(outputs.PuddleAmount01);
            ApplyPuddleSamples(puddleAmount01, ripplePhase01);
            lastAppliedRevision = outputs.Revision;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            if (puddleAmount01 <= 0.001f || Time.unscaledTime < nextRippleUpdateTime)
            {
                return;
            }

            nextRippleUpdateTime = Time.unscaledTime + RippleUpdateIntervalSeconds;
            ripplePhase01 = Mathf.Repeat(
                ripplePhase01 + RippleUpdateIntervalSeconds * 0.7f,
                1f);
            ApplyPuddleSamples(puddleAmount01, ripplePhase01);
            rippleUpdateCount++;
        }

        private void OnDisable()
        {
            Clear(ground);
            Clear(road);
            Clear(exterior);
            Clear(vehicle);
            Clear(vegetation);
            Clear(puddle);
            globalBridge.Reset();
            lastAppliedRevision = 0U;
            rippleUpdateCount = 0U;
            puddleAmount01 = 0f;
            ripplePhase01 = 0f;
            nextRippleUpdateTime = 0f;
        }

        private void EnsureInitialized()
        {
            if (propertyBlock != null)
            {
                return;
            }

            propertyBlock = new MaterialPropertyBlock();
            ground = BuildSamples(groundSamples);
            road = BuildSamples(roadSamples);
            exterior = BuildSamples(exteriorSamples);
            vehicle = BuildSamples(vehicleSamples);
            vegetation = BuildSamples(vegetationSamples);
            puddle = BuildSamples(puddleSamples);
        }

        private void ApplySamples(
            Sample[] samples,
            float wetness01,
            float wetSmoothness,
            float darkening,
            bool scaleAlpha = false)
        {
            if (samples == null)
            {
                return;
            }

            wetness01 = Mathf.Clamp01(wetness01);
            for (int index = 0; index < samples.Length; index++)
            {
                Sample sample = samples[index];
                if (sample.Renderer == null)
                {
                    continue;
                }

                Color dark = sample.BaseColor * darkening;
                dark.a = sample.BaseColor.a;
                Color color = Color.Lerp(sample.BaseColor, dark, wetness01);
                if (scaleAlpha)
                {
                    color.a = sample.BaseColor.a * wetness01;
                }

                propertyBlock.Clear();
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetFloat(
                    SmoothnessId,
                    Mathf.Lerp(sample.Smoothness, Mathf.Max(sample.Smoothness, wetSmoothness), wetness01));
                sample.Renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void ApplyPuddleSamples(float amount01, float phase01)
        {
            if (puddle == null)
            {
                return;
            }

            float wave01 = 0.5f + 0.5f * Mathf.Sin(phase01 * Mathf.PI * 2f);
            float rippleVisibility = Mathf.Lerp(0.92f, 1f, wave01);
            for (int index = 0; index < puddle.Length; index++)
            {
                Sample sample = puddle[index];
                if (sample.Renderer == null)
                {
                    continue;
                }

                Color dark = sample.BaseColor * 0.8f;
                Color color = Color.Lerp(sample.BaseColor, dark, amount01);
                color.a = sample.BaseColor.a * amount01 * rippleVisibility;
                float wetSmoothness = Mathf.Lerp(
                    sample.Smoothness,
                    Mathf.Max(sample.Smoothness, 0.96f),
                    amount01);

                propertyBlock.Clear();
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetFloat(
                    SmoothnessId,
                    wetSmoothness * Mathf.Lerp(0.985f, 1f, wave01));
                propertyBlock.SetFloat(PuddleAmountId, amount01);
                propertyBlock.SetFloat(PuddleRipplePhaseId, phase01);
                sample.Renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static Sample[] BuildSamples(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
            {
                return Array.Empty<Sample>();
            }

            var result = new Sample[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                Material shared = renderer != null ? renderer.sharedMaterial : null;
                Color baseColor = shared != null && shared.HasProperty(BaseColorId)
                    ? shared.GetColor(BaseColorId)
                    : Color.white;
                float smoothness = shared != null && shared.HasProperty(SmoothnessId)
                    ? shared.GetFloat(SmoothnessId)
                    : 0f;
                result[index] = new Sample(renderer, baseColor, smoothness);
            }

            return result;
        }

        private static void Clear(Sample[] samples)
        {
            if (samples == null)
            {
                return;
            }

            for (int index = 0; index < samples.Length; index++)
            {
                if (samples[index].Renderer != null)
                {
                    samples[index].Renderer.SetPropertyBlock(null);
                }
            }
        }

        private static int Count(Renderer[] values) => values?.Length ?? 0;

        private readonly struct Sample
        {
            public Sample(Renderer renderer, Color baseColor, float smoothness)
            {
                Renderer = renderer;
                BaseColor = baseColor;
                Smoothness = smoothness;
            }

            public Renderer Renderer { get; }
            public Color BaseColor { get; }
            public float Smoothness { get; }
        }
    }
}
