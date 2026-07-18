using System;
using MSC.Core.Time;
using MSC.Weather.Persistence;
using MSC.Weather.Presentation;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Session-level environment DTO. File storage and migration hardening remain
    /// owned by Milestone 09; this object never serializes Enviro references.
    /// </summary>
    [Serializable]
    public sealed class ProductionEnvironmentSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public const string StableConfigId = "environment.production.save.v1";

        public int SchemaVersion = CurrentSchemaVersion;
        public string ConfigId = StableConfigId;
        public GameTimeSaveDto GameTime;
        public WeatherDomainSaveDto WeatherDomain;
        public int QualityTier = (int)EnvironmentQualityTier.Medium;
    }
}
