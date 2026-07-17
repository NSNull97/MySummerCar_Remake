using System;

namespace MSC.Weather.Presentation
{
    public enum EnvironmentQualityTier
    {
        Low = 0,
        High = 1
    }

    public enum EnvironmentCloudType
    {
        Clear = 0,
        Scattered = 1,
        Overcast = 2,
        Storm = 3
    }

    public enum EnvironmentPrecipitationType
    {
        None = 0,
        Rain = 1
    }

    public enum EnvironmentPresentationPresetKind
    {
        Clear = 0,
        Overcast = 1,
        Rain = 2,
        Storm = 3,
        Night = 4,
        Mist = 5
    }

    [Flags]
    public enum EnvironmentPresentationCapabilities
    {
        None = 0,
        TimeOfDay = 1 << 0,
        Sky = 1 << 1,
        SunMoonLighting = 1 << 2,
        Clouds = 1 << 3,
        Precipitation = 1 << 4,
        Fog = 1 << 5,
        Wind = 1 << 6,
        LightningVisual = 1 << 7,
        EnvironmentRefresh = 1 << 8,
        QualityTiers = 1 << 9
    }

    public enum EnvironmentPresentationState
    {
        Detached = 0,
        Disabled = 1,
        Ready = 2,
        Degraded = 3,
        Faulted = 4
    }

    public enum EnvironmentDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    [Flags]
    public enum EnvironmentRefreshTarget
    {
        None = 0,
        Sky = 1 << 0,
        Ambient = 1 << 1,
        Reflections = 1 << 2
    }
}
