using System.Text;
using MSC.Weather.Domain;
using UnityEngine;

namespace MSC.Weather.Production
{
    /// <summary>Editor/development-only ownership and zone diagnostics.</summary>
    [DisallowMultipleComponent]
    public sealed class HybridEnvironmentDebugOverlay : MonoBehaviour
    {
        [SerializeField] private ProductionWeatherStateSource weatherStateSource;
        [SerializeField] private WeatherExposureResolver exposureResolver;
        [SerializeField] private NativeHdrpWeatherBridge hdrpBridge;
        [SerializeField] private bool visible;

        private readonly StringBuilder text = new StringBuilder(1024);
        private GUIStyle style;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            ProductionWeatherStateSource authoredSource,
            WeatherExposureResolver authoredResolver,
            NativeHdrpWeatherBridge authoredBridge)
        {
            weatherStateSource = authoredSource;
            exposureResolver = authoredResolver;
            hdrpBridge = authoredBridge;
        }
#endif

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GUI.Button(new Rect(12f, 12f, 165f, 24f),
                    visible ? "Hide Environment Debug" : "Show Environment Debug"))
            {
                visible = !visible;
            }

            if (!visible)
            {
                return;
            }

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 12,
                    wordWrap = false,
                };
            }

            text.Clear();
            text.AppendLine("MSC Hybrid Environment");
            if (weatherStateSource != null && weatherStateSource.IsReady)
            {
                WeatherRuntimeState weather = weatherStateSource.Current;
                text.Append("Weather: ").Append(weather.CurrentWeatherId)
                    .Append(" -> ").Append(weather.TargetWeatherId)
                    .Append("  t=").Append(weather.WeatherTransition01.ToString("F2"))
                    .AppendLine();
                text.Append("Rain/Fog/Wind: ")
                    .Append(weather.Rain01.ToString("F2")).Append(" / ")
                    .Append(weather.FogIntensity01.ToString("F2")).Append(" / ")
                    .Append(weather.WindSpeed01.ToString("F2")).AppendLine();
            }

            if (exposureResolver != null)
            {
                WeatherExposureState exposure = exposureResolver.Current;
                text.Append("Zone: ")
                    .Append(exposureResolver.CurrentZone != null
                        ? exposureResolver.CurrentZone.StableId
                        : "exterior")
                    .Append(" portals=")
                    .Append(exposureResolver.ActivePortalCount)
                    .Append(" open=")
                    .Append(exposureResolver.StrongestPortalOpenness.ToString("F2"))
                    .AppendLine();
                text.Append("Enclosure/Shelter: ")
                    .Append(exposure.EnclosureFactor.ToString("F2")).Append(" / ")
                    .Append(exposure.ShelterFactor.ToString("F2")).AppendLine();
                text.Append("Exposure rain/fog/wind/audio/thunder: ")
                    .Append(exposure.PrecipitationExposure.ToString("F2")).Append(" / ")
                    .Append(exposure.FogExposure.ToString("F2")).Append(" / ")
                    .Append(exposure.WindExposure.ToString("F2")).Append(" / ")
                    .Append(exposure.WeatherAudioExposure.ToString("F2")).Append(" / ")
                    .Append(exposure.ThunderExposure.ToString("F2")).AppendLine();
                text.Append("Camera: ").Append(exposureResolver.VisualAnchorPosition)
                    .Append(" Listener: ").Append(exposureResolver.AudioAnchorPosition)
                    .AppendLine();
            }

            if (hdrpBridge != null)
            {
                text.Append("Fog owner: ").Append(hdrpBridge.FogOwner)
                    .Append(" mfp=")
                    .Append(hdrpBridge.CurrentFogMeanFreePathMeters.ToString("F1"))
                    .AppendLine();
                text.Append("Exposure owner: ").Append(hdrpBridge.ExposureOwner)
                    .Append(" fixedEV=")
                    .Append(hdrpBridge.CurrentFixedExposureEv.ToString("F2"))
                    .Append(" indirect=")
                    .Append(hdrpBridge.CurrentIndirectDiffuseMultiplier.ToString("F2"))
                    .AppendLine();
            }

            GUI.Box(new Rect(12f, 42f, 680f, 230f), text.ToString(), style);
#endif
        }
    }
}
