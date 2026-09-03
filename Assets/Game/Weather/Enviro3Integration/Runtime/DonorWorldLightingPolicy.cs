using System;
using UnityEngine;

namespace MSC.Weather.Enviro3Integration
{
    /// <summary>
    /// Project-owned transfer of the donor GAME.unity SUN/RenderSettings values.
    /// The donor used Gamma color space, so interpolation stays in donor gamma
    /// space and is converted once before being written into the Linear HDRP scene.
    /// </summary>
    public static class DonorWorldLightingPolicy
    {
        public const float DonorLegacySunIntensity = 1.75f;
        public const float DonorSunShadowStrength = 0.8f;
        public const float DonorAmbientIntensity = 1f;

        private static readonly Color BlackGamma =
            new Color(0f, 0f, 0f, 1f);
        private static readonly Color SunriseGamma =
            new Color(0.42647058f, 0.27595153f, 0.2790658f, 1f);
        private static readonly Color SunsetGamma =
            new Color(0.41911763f, 0.3197791f, 0.24345803f, 1f);
        private static readonly Color TransitionOrangeGamma =
            new Color(0.875f, 0.59847873f, 0.34742647f, 1f);
        private static readonly Color WhiteGamma =
            new Color(0.9852941f, 0.95731413f, 0.8838668f, 1f);
        private static readonly Color AmbientShadowGamma =
            new Color(0.28222317f, 0.28222317f, 0.42647058f, 1f);

        public static Color EvaluateSunColor(float normalizedTimeOfDay01)
        {
            float hour = ResolveHour(normalizedTimeOfDay01);
            if (hour < 2f)
            {
                return BlackGamma.linear;
            }

            if (hour < 4f)
            {
                return EvaluateDonorGammaSegment(
                    hour,
                    2f,
                    4f,
                    BlackGamma,
                    SunriseGamma);
            }

            if (hour < 6f)
            {
                return EvaluateDonorGammaSegment(
                    hour,
                    4f,
                    6f,
                    SunriseGamma,
                    TransitionOrangeGamma);
            }

            if (hour < 8f)
            {
                return EvaluateDonorGammaSegment(
                    hour,
                    6f,
                    8f,
                    TransitionOrangeGamma,
                    WhiteGamma);
            }

            if (hour < 18f)
            {
                return WhiteGamma.linear;
            }

            if (hour < 20f)
            {
                return EvaluateDonorGammaSegment(
                    hour,
                    18f,
                    20f,
                    WhiteGamma,
                    TransitionOrangeGamma);
            }

            if (hour < 22f)
            {
                return EvaluateDonorGammaSegment(
                    hour,
                    20f,
                    22f,
                    TransitionOrangeGamma,
                    SunsetGamma);
            }

            return EvaluateDonorGammaSegment(
                hour,
                22f,
                24f,
                SunsetGamma,
                BlackGamma);
        }

        public static Color EvaluateAmbientColor(float normalizedTimeOfDay01)
        {
            float hour = ResolveHour(normalizedTimeOfDay01);
            if (hour < 4f)
            {
                return BlackGamma.linear;
            }

            if (hour < 6f)
            {
                return EvaluateDonorGammaSegment(
                    hour,
                    4f,
                    6f,
                    BlackGamma,
                    AmbientShadowGamma);
            }

            if (hour < 22f)
            {
                return AmbientShadowGamma.linear;
            }

            return EvaluateDonorGammaSegment(
                hour,
                22f,
                24f,
                AmbientShadowGamma,
                BlackGamma);
        }

        private static float ResolveHour(float normalizedTimeOfDay01)
        {
            if (!float.IsFinite(normalizedTimeOfDay01))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedTimeOfDay01));
            }

            return Mathf.Repeat(normalizedTimeOfDay01, 1f) * 24f;
        }

        private static Color EvaluateDonorGammaSegment(
            float hour,
            float startHour,
            float endHour,
            Color fromGamma,
            Color toGamma)
        {
            float progress = Mathf.InverseLerp(startHour, endHour, hour);
            return Color.LerpUnclamped(fromGamma, toGamma, progress).linear;
        }
    }
}
