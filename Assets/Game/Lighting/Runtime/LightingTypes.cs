using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Lighting
{
    public enum LightFixtureCategory
    {
        DomesticIncandescent,
        EnclosedCeiling,
        Fluorescent,
        TeimoShop,
        TeimoPub,
        FleetariWorkshop,
        StreetLamp,
        ExteriorBuilding,
        VehicleLowBeam,
        VehicleHighBeam,
        VehicleTail,
        VehicleBrake,
        VehicleIndicator,
        VehicleReverse,
        VehicleLicensePlate,
        VehicleDashboard,
        VehicleInterior,
        PlayerFlashlight,
        Spotlight,
        Sign,
        Technical,
        RefrigeratedDisplay,
        // Appended to preserve every existing serialized enum value.
        HomeExterior,
    }

    public enum LightFixtureShape
    {
        Point,
        SpotCone,
        SpotPyramid,
        SpotBox,
        AreaRectangle,
        AreaTube,
    }

    public enum LightingQualityTier
    {
        Low,
        Medium,
        High,
        Ultra,
    }

    public enum VolumetricBeamQuality
    {
        Off,
        StandardDefinition,
        HighDefinition,
    }

    public enum LightPowerPolicyKind
    {
        ManualSwitch,
        DuskToDawn,
        BusinessOpenAndOwnerPresent,
        VehicleElectrical,
        AlwaysWhenPowered,
        Scripted,
    }

    public enum LightingZoneType
    {
        Exterior,
        DomesticRoom,
        TeimoShop,
        TeimoPub,
        FleetariWorkshop,
        OtherInterior,
        VehicleInterior,
    }

    [Serializable]
    public struct LightFixtureQualitySettings
    {
        [SerializeField, Min(0f)] private float lightDistanceMeters;
        [SerializeField, Min(0f)] private float shadowDistanceMeters;
        [SerializeField] private bool shadowsEnabled;
        [SerializeField] private bool contactShadowsEnabled;
        [SerializeField] private ShadowUpdateMode shadowUpdateMode;
        [SerializeField, Min(16)] private int shadowResolution;
        [SerializeField] private VolumetricBeamQuality beamQuality;
        [SerializeField, Min(0f)] private float beamDistanceMeters;

        public float LightDistanceMeters => lightDistanceMeters;
        public float ShadowDistanceMeters => shadowDistanceMeters;
        public bool ShadowsEnabled => shadowsEnabled;
        public bool ContactShadowsEnabled => contactShadowsEnabled;
        public ShadowUpdateMode ShadowUpdateMode => shadowUpdateMode;
        public int ShadowResolution => shadowResolution;
        public VolumetricBeamQuality BeamQuality => beamQuality;
        public float BeamDistanceMeters => beamDistanceMeters;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            float lightDistance,
            float shadowDistance,
            bool shadows,
            bool contactShadows,
            ShadowUpdateMode updateMode,
            int resolution,
            VolumetricBeamQuality configuredBeamQuality,
            float beamDistance)
        {
            lightDistanceMeters = Mathf.Max(0f, lightDistance);
            shadowDistanceMeters = Mathf.Max(0f, shadowDistance);
            shadowsEnabled = shadows;
            contactShadowsEnabled = contactShadows;
            shadowUpdateMode = updateMode;
            shadowResolution = Mathf.Max(16, resolution);
            beamQuality = configuredBeamQuality;
            beamDistanceMeters = Mathf.Max(0f, beamDistance);
        }
#endif
    }

    public readonly struct LightingAtmosphereState
    {
        public LightingAtmosphereState(
            float fog,
            float precipitation,
            float humidity,
            bool snow)
        {
            Fog = Mathf.Clamp01(fog);
            Precipitation = Mathf.Clamp01(precipitation);
            Humidity = Mathf.Clamp01(humidity);
            Snow = snow;
        }

        public float Fog { get; }
        public float Precipitation { get; }
        public float Humidity { get; }
        public bool Snow { get; }

        public static LightingAtmosphereState Clear =>
            new LightingAtmosphereState(0f, 0f, 0.25f, false);
    }

    public interface ILightingAtmosphereProvider
    {
        LightingAtmosphereState Current { get; }
    }

    public interface IBusinessPresenceProvider
    {
        bool IsBusinessOpenAndOwnerPresent(string businessId);
    }

    public interface IVehicleElectricalLightingSource
    {
        bool HasUsablePower { get; }
        bool IsLightingChannelOn(string channelId);
    }
}
