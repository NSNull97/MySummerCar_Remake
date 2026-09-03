using System;
using System.Collections.Generic;

namespace MSC.Weather.Persistence
{
    [Serializable]
    public sealed class WeatherOverrideSaveDto
    {
        public string OverrideId;
        public string Owner;
        public string Reason;
        public int Priority;
        public double StartSimulationSeconds;
        public double EndSimulationSeconds;
        public string RequestedProfileId;
        public int SerializationPolicy;
        public long SequenceBits;
    }

    [Serializable]
    public sealed class WeatherSaveDto
    {
        public const int CurrentSchemaVersion = 2;
        public const int LegacySchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string ConfigId;
        public double SimulationSeconds;
        public bool IsScheduleFrozen;
        public string CurrentProfileId;
        public string TargetProfileId;
        public string PreviousProfileId;
        public double FrontDurationSeconds;
        public double TransitionDurationSeconds;
        public double ElapsedSeconds;
        public long TimelineCursor;
        public int RandomVersion;
        public long RandomStateBits;
        public long RandomIncrementBits;
        public List<string> RecentProfileIds = new List<string>();
        public int HistoryCount;
        public int HistoryWriteIndex;
        public long NextOverrideSequenceBits;
        public uint Revision;
        public List<WeatherOverrideSaveDto> Overrides = new List<WeatherOverrideSaveDto>();
    }

    [Serializable]
    public sealed class WetnessSaveDto
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string ConfigId;
        public float GroundWetness01;
        public float RoadWetness01;
        public float PuddleAmount01;
        public float VegetationWetness01;
        public uint Revision;
    }

    [Serializable]
    public sealed class LightningCandidateSaveDto
    {
        public string StableId;
        public double CooldownUntilSeconds;
        public uint LastStrikeSequence;
        public uint StrikeCount;
    }

    [Serializable]
    public sealed class LightningSaveDto
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string ConfigId;
        public double SimulationSeconds;
        public double GlobalCooldownUntilSeconds;
        public double RestoreGraceUntilSeconds;
        public uint Sequence;
        public bool NonLethalMode;
        public int RandomVersion;
        public long RandomStateBits;
        public long RandomIncrementBits;
        public List<LightningCandidateSaveDto> CandidateStates = new List<LightningCandidateSaveDto>();
    }

    [Serializable]
    public sealed class WeatherDomainSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public const string StableConfigId = "weather.domain.save.v1";

        public int SchemaVersion = CurrentSchemaVersion;
        public string ConfigId = StableConfigId;
        public WeatherSaveDto Weather;
        public WetnessSaveDto Wetness;
        public LightningSaveDto Lightning;
    }
}
