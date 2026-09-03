using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Lighting
{
    /// <summary>
    /// Version-tolerant boundary over the public VLB 2.x API. No runtime
    /// assembly depends on the vendor's predefined Assembly-CSharp output.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VolumetricBeamAdapter : MonoBehaviour
    {
        private static bool typesResolved;
        private static Type hdType;
        private static Type sdType;
        private static Type hdShadowType;
        private static Type sdOcclusionType;
        private static int vendorVersion;
        private static bool compatibilityWarningLogged;

        // VLB 2.2.4 moved SRP depth-camera capture from Update to LateUpdate.
        // Earlier versions can recursively render while HDRP is preparing the
        // main camera, corrupting the cached-shadow atlas and fog draw list.
        private const int SafeSrpDepthCameraVersion = 20204;

        [SerializeField] private Component hdBeam;
        [SerializeField] private Component sdBeam;
        [SerializeField] private Component occlusion;
        [SerializeField] private VolumetricBeamQuality activeQuality;

        private bool hasApplied;
        private float appliedIntensity;
        private float appliedRange;
        private float appliedSpotAngle;
        private bool appliedOcclusion;

        public VolumetricBeamQuality ActiveQuality => activeQuality;
        public bool PackageAvailable
        {
            get
            {
                ResolveTypes();
                return hdType != null && sdType != null;
            }
        }

        public void Apply(
            VolumetricBeamQuality quality,
            float intensity,
            float rangeMeters,
            float spotAngleDegrees,
            bool enableOcclusion)
        {
            ResolveTypes();
            bool applyDepthCameraOcclusion = enableOcclusion &&
                SupportsDepthCameraOcclusion();
            if (hasApplied && activeQuality == quality &&
                Mathf.Approximately(appliedIntensity, intensity) &&
                Mathf.Approximately(appliedRange, rangeMeters) &&
                Mathf.Approximately(appliedSpotAngle, spotAngleDegrees) &&
                appliedOcclusion == applyDepthCameraOcclusion)
            {
                return;
            }

            if (hdType == null || sdType == null)
            {
                activeQuality = VolumetricBeamQuality.Off;
                Remember(
                    VolumetricBeamQuality.Off,
                    intensity,
                    rangeMeters,
                    spotAngleDegrees,
                    applyDepthCameraOcclusion);
                return;
            }

            if (enableOcclusion && !applyDepthCameraOcclusion &&
                !compatibilityWarningLogged)
            {
                compatibilityWarningLogged = true;
                Debug.LogWarning(
                    "[MSC] VLB depth-camera occlusion is disabled under SRP " +
                    "for this vendor version. HDRP light shadows and the " +
                    "volumetric beam remain enabled; this prevents recursive " +
                    "Camera.Render from corrupting HDRP cached shadows.");
            }

            if (quality == VolumetricBeamQuality.HighDefinition)
            {
                hdBeam = EnsureComponent(hdBeam, hdType);
                ConfigureHd(hdBeam, intensity, rangeMeters, spotAngleDegrees);
                SetEnabled(hdBeam, true);
                SetEnabled(sdBeam, false);
                if (applyDepthCameraOcclusion && hdShadowType != null)
                {
                    occlusion = EnsureComponent(occlusion, hdShadowType);
                }
            }
            else if (quality == VolumetricBeamQuality.StandardDefinition)
            {
                sdBeam = EnsureComponent(sdBeam, sdType);
                ConfigureSd(sdBeam, intensity, rangeMeters, spotAngleDegrees);
                SetEnabled(sdBeam, true);
                SetEnabled(hdBeam, false);
                if (applyDepthCameraOcclusion && sdOcclusionType != null)
                {
                    occlusion = EnsureComponent(occlusion, sdOcclusionType);
                }
            }
            else
            {
                SetEnabled(hdBeam, false);
                SetEnabled(sdBeam, false);
                SetEnabled(occlusion, false);
            }

            if (quality != VolumetricBeamQuality.Off)
            {
                SetEnabled(occlusion, applyDepthCameraOcclusion);
            }

            Remember(
                quality,
                intensity,
                rangeMeters,
                spotAngleDegrees,
                applyDepthCameraOcclusion);
        }

        private static bool SupportsDepthCameraOcclusion()
        {
            bool usesScriptableRenderPipeline =
                GraphicsSettings.currentRenderPipeline != null ||
                GraphicsSettings.defaultRenderPipeline != null;
            return !usesScriptableRenderPipeline ||
                   vendorVersion >= SafeSrpDepthCameraVersion;
        }

        private void Remember(
            VolumetricBeamQuality quality,
            float intensity,
            float range,
            float spotAngle,
            bool enableOcclusion)
        {
            activeQuality = quality;
            appliedIntensity = intensity;
            appliedRange = range;
            appliedSpotAngle = spotAngle;
            appliedOcclusion = enableOcclusion;
            hasApplied = true;
        }

        private Component EnsureComponent(Component existing, Type type)
        {
            if (existing != null && type.IsInstanceOfType(existing))
            {
                return existing;
            }

            Component found = GetComponent(type);
            return found != null ? found : gameObject.AddComponent(type);
        }

        private static void ConfigureHd(
            Component beam,
            float intensity,
            float range,
            float spotAngle)
        {
            SetMember(beam, "colorFromLight", true);
            SetMember(beam, "useIntensityFromAttachedLightSpot", false);
            SetMember(beam, "useSpotAngleFromAttachedLightSpot", true);
            SetMember(beam, "useFallOffEndFromAttachedLightSpot", true);
            SetMember(beam, "intensity", Mathf.Max(0f, intensity));
            SetMember(beam, "intensityMultiplier", -1f);
            SetMember(beam, "fallOffEnd", Mathf.Max(0.1f, range));
            SetMember(beam, "spotAngle", Mathf.Clamp(spotAngle, 0.1f, 179f));
            SetMember(beam, "hdrpExposureWeight", 0.45f);
            Invoke(beam, "UpdateAfterManualPropertyChange");
        }

        private static void ConfigureSd(
            Component beam,
            float intensity,
            float range,
            float spotAngle)
        {
            SetMember(beam, "colorFromLight", true);
            SetMember(beam, "intensityFromLight", false);
            SetMember(beam, "spotAngleFromLight", true);
            SetMember(beam, "fallOffEndFromLight", true);
            SetMember(beam, "intensityGlobal", Mathf.Max(0f, intensity));
            SetMember(beam, "intensityMultiplier", 1f);
            SetMember(beam, "fallOffEnd", Mathf.Max(0.1f, range));
            SetMember(beam, "spotAngle", Mathf.Clamp(spotAngle, 0.1f, 179f));
            SetMember(beam, "hdrpExposureWeight", 0.45f);
            SetMember(beam, "trackChangesDuringPlaytime", false);
            Invoke(beam, "UpdateAfterManualPropertyChange");
        }

        private static void SetEnabled(Component component, bool value)
        {
            if (component is Behaviour behaviour)
            {
                behaviour.enabled = value;
            }
        }

        private static void SetMember(Component component, string name, object value)
        {
            if (component == null)
            {
                return;
            }

            Type type = component.GetType();
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            if (property?.CanWrite == true)
            {
                property.SetValue(component, value);
                return;
            }

            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            field?.SetValue(component, value);
        }

        private static void Invoke(Component component, string methodName)
        {
            component?.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null)?.Invoke(component, null);
        }

        private static void ResolveTypes()
        {
            if (typesResolved)
            {
                return;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Assembly assembly = assemblies[index];
                hdType ??= assembly.GetType("VLB.VolumetricLightBeamHD", false);
                sdType ??= assembly.GetType("VLB.VolumetricLightBeamSD", false);
                hdShadowType ??= assembly.GetType("VLB.VolumetricShadowHD", false);
                sdOcclusionType ??= assembly.GetType(
                    "VLB.DynamicOcclusionDepthBuffer",
                    false);
                Type versionType = assembly.GetType("VLB.Version", false);
                if (versionType != null)
                {
                    FieldInfo current = versionType.GetField(
                        "Current",
                        BindingFlags.Public | BindingFlags.Static);
                    if (current?.GetRawConstantValue() is int resolvedVersion)
                    {
                        vendorVersion = resolvedVersion;
                    }
                }
            }

            typesResolved = true;
        }
    }
}
