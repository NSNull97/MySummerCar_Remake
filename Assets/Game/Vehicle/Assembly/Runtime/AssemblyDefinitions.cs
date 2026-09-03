using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    public enum PartCategory
    {
        Structural = 0,
        Suspension = 1,
        Brake = 2,
        Wheel = 3,
        Engine = 4,
        Electrical = 5,
        Cooling = 6,
        Body = 7,
        Interior = 8,
        Exhaust = 9
    }

    public enum FastenerSize
    {
        None = 0,
        Millimeter5 = 5,
        Millimeter6 = 6,
        Millimeter7 = 7,
        Millimeter8 = 8,
        Millimeter9 = 9,
        Millimeter10 = 10,
        Millimeter11 = 11,
        Millimeter12 = 12,
        Millimeter13 = 13,
        Millimeter14 = 14,
        Millimeter15 = 15,
        Millimeter16 = 16,
        Millimeter17 = 17
    }

    public enum FastenerDirection
    {
        ClockwiseToTighten = 0,
        CounterClockwiseToTighten = 1
    }

    public enum FastenerRotationDirection
    {
        Clockwise = 0,
        CounterClockwise = 1
    }

    public enum FastenerSpeedRetentionPolicy
    {
        None = 0,
        DonorWheelBoltCheck = 1
    }

    public enum FastenerBreakAction
    {
        None = 0,
        DetachInstalledPart = 1
    }

    /// <summary>
    /// Donor BoltCheck aggregate authored for one mount. Individual fasteners
    /// still own their insertion and turn stages; this definition restores the
    /// separate latched Bolted state and optional speed-driven BREAK policy.
    /// </summary>
    [Serializable]
    public sealed class FastenerGroupDefinition
    {
        [SerializeField]
        private string[] fastenerDefinitionIds = Array.Empty<string>();

        [SerializeField, Min(0)]
        private int aggregateMaximumTightness;

        [SerializeField, Min(0)]
        private int boltedOnThreshold = 1;

        [SerializeField, Min(0)]
        private int boltedOffThreshold;

        [SerializeField]
        private FastenerSpeedRetentionPolicy speedRetentionPolicy;

        [SerializeField, Min(0f)]
        private float looseBreakSpeedKph;

        [SerializeField, Min(0f)]
        private float partialCheckSpeedKph;

        [SerializeField, Min(0.0001f)]
        private float chanceDivisor = 100f;

        [SerializeField]
        private FastenerBreakAction breakAction;

        public string[] FastenerDefinitionIds =>
            fastenerDefinitionIds ?? Array.Empty<string>();

        public int AggregateMaximumTightness => aggregateMaximumTightness;

        public int BoltedOnThreshold => boltedOnThreshold;

        public int BoltedOffThreshold => boltedOffThreshold;

        public FastenerSpeedRetentionPolicy SpeedRetentionPolicy =>
            speedRetentionPolicy;

        public float LooseBreakSpeedKph => looseBreakSpeedKph;

        public float PartialCheckSpeedKph => partialCheckSpeedKph;

        public float ChanceDivisor => chanceDivisor;

        public FastenerBreakAction BreakAction => breakAction;

        public bool HasFasteners => FastenerDefinitionIds.Length > 0;

        public void Configure(
            string[] configuredFastenerIds,
            int maximumTightness,
            int onThreshold,
            int offThreshold,
            FastenerSpeedRetentionPolicy retentionPolicy =
                FastenerSpeedRetentionPolicy.None,
            float configuredLooseBreakSpeedKph = 0f,
            float configuredPartialCheckSpeedKph = 0f,
            float configuredChanceDivisor = 100f,
            FastenerBreakAction configuredBreakAction =
                FastenerBreakAction.None)
        {
            fastenerDefinitionIds = configuredFastenerIds ??
                Array.Empty<string>();
            aggregateMaximumTightness = Mathf.Max(0, maximumTightness);
            boltedOnThreshold = Mathf.Clamp(
                onThreshold,
                0,
                aggregateMaximumTightness);
            boltedOffThreshold = Mathf.Clamp(
                offThreshold,
                0,
                boltedOnThreshold);
            speedRetentionPolicy = retentionPolicy;
            looseBreakSpeedKph = Mathf.Max(
                0f,
                configuredLooseBreakSpeedKph);
            partialCheckSpeedKph = Mathf.Max(
                looseBreakSpeedKph,
                configuredPartialCheckSpeedKph);
            chanceDivisor = Mathf.Max(0.0001f, configuredChanceDivisor);
            breakAction = configuredBreakAction;
        }

        public static FastenerGroupDefinition CreateCompatibility(
            FastenerDefinition[] fasteners)
        {
            fasteners = fasteners ?? Array.Empty<FastenerDefinition>();
            int maximum = 0;
            int count = 0;
            for (int index = 0; index < fasteners.Length; index++)
            {
                FastenerDefinition fastener = fasteners[index];
                if (fastener != null && fastener.RequiredForRemoval)
                {
                    maximum += fastener.MaximumStage;
                    count++;
                }
            }

            var ids = new string[count];
            int writeIndex = 0;
            for (int index = 0; index < fasteners.Length; index++)
            {
                FastenerDefinition fastener = fasteners[index];
                if (fastener != null && fastener.RequiredForRemoval)
                {
                    ids[writeIndex++] = fastener.DefinitionId;
                }
            }

            var definition = new FastenerGroupDefinition();
            definition.Configure(
                ids,
                maximum,
                maximum > 0 ? 1 : 0,
                0);
            return definition;
        }

        public float CalculateBreakChance01(
            int aggregateTightness,
            float speedKph)
        {
            if (speedRetentionPolicy !=
                    FastenerSpeedRetentionPolicy.DonorWheelBoltCheck ||
                breakAction == FastenerBreakAction.None ||
                aggregateMaximumTightness <= 0 ||
                aggregateTightness >= aggregateMaximumTightness ||
                speedKph <= looseBreakSpeedKph)
            {
                return 0f;
            }

            // Donor wheel FSM: an entirely loose wheel BREAKs immediately
            // above 5 km/h. A partially secured wheel enters the Chance state
            // only above 33 km/h, computes (Max-Tightness)/100 and feeds that
            // value beside weight 1 into SendRandomEvent. PlayMaker normalizes
            // those weights, hence chanceWeight / (chanceWeight + 1).
            if (aggregateTightness <= boltedOffThreshold)
            {
                return 1f;
            }

            if (speedKph <= partialCheckSpeedKph)
            {
                return 0f;
            }

            float chanceWeight = Mathf.Max(
                    0,
                    aggregateMaximumTightness - aggregateTightness) /
                chanceDivisor;
            return chanceWeight <= 0f
                ? 0f
                : chanceWeight / (chanceWeight + 1f);
        }

        public bool IsLatchConsistent(
            int aggregateTightness,
            bool restoredBolted,
            bool mountOccupied)
        {
            if (!mountOccupied || !HasFasteners)
            {
                return !restoredBolted;
            }

            return restoredBolted
                ? aggregateTightness > boltedOffThreshold
                : aggregateTightness < boltedOnThreshold;
        }

        public bool ShouldBreak(
            int aggregateTightness,
            float speedKph,
            float deterministicSample01) =>
            Mathf.Clamp01(deterministicSample01) <
            CalculateBreakChance01(aggregateTightness, speedKph);

        public static float Sample01(uint seed)
        {
            uint value = seed;
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777216f;
        }
    }

    [Serializable]
    public sealed class PartCompatibilityRule
    {
        [SerializeField]
        private string mountSocketType = string.Empty;

        [SerializeField]
        private string requiredOwnerPartDefinitionId = string.Empty;

        public string MountSocketType => mountSocketType;

        public string RequiredOwnerPartDefinitionId => requiredOwnerPartDefinitionId;

        public bool Matches(MountPointDefinition mount)
        {
            if (mount == null || !string.Equals(
                    mountSocketType,
                    mount.SocketType,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return string.IsNullOrEmpty(requiredOwnerPartDefinitionId) || string.Equals(
                requiredOwnerPartDefinitionId,
                mount.OwnerPartDefinitionId,
                StringComparison.Ordinal);
        }

        public static PartCompatibilityRule Create(string socketType, string requiredOwnerId = "")
        {
            return new PartCompatibilityRule
            {
                mountSocketType = socketType ?? string.Empty,
                requiredOwnerPartDefinitionId = requiredOwnerId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ToolCompatibilityRule
    {
        [SerializeField]
        private string toolType = "Wrench";

        [SerializeField]
        private FastenerSize fastenerSize = FastenerSize.None;

        public string ToolType => toolType;

        public FastenerSize FastenerSize => fastenerSize;

        public bool Matches(ToolDefinition tool)
        {
            return tool != null &&
                string.Equals(toolType, tool.ToolType, StringComparison.Ordinal) &&
                (fastenerSize == FastenerSize.None || fastenerSize == tool.Size);
        }

        public static ToolCompatibilityRule Create(string type, FastenerSize size)
        {
            return new ToolCompatibilityRule
            {
                toolType = type ?? string.Empty,
                fastenerSize = size
            };
        }
    }

    [Serializable]
    public struct MountConstraint
    {
        [SerializeField, Min(0.001f)]
        private float positionToleranceMeters;

        [SerializeField, Range(0f, 180f)]
        private float angularToleranceDegrees;

        [SerializeField, Min(0.01f)]
        private float previewDistanceMeters;

        [SerializeField, Min(0f)]
        private float obstructionRadiusMeters;

        public MountConstraint(
            float positionToleranceMeters,
            float angularToleranceDegrees,
            float previewDistanceMeters,
            float obstructionRadiusMeters)
        {
            this.positionToleranceMeters = Mathf.Max(0.001f, positionToleranceMeters);
            this.angularToleranceDegrees = Mathf.Clamp(angularToleranceDegrees, 0f, 180f);
            this.previewDistanceMeters = Mathf.Max(0.01f, previewDistanceMeters);
            this.obstructionRadiusMeters = Mathf.Max(0f, obstructionRadiusMeters);
        }

        public float PositionToleranceMeters => positionToleranceMeters;

        public float AngularToleranceDegrees => angularToleranceDegrees;

        public float PreviewDistanceMeters => previewDistanceMeters;

        public float ObstructionRadiusMeters => obstructionRadiusMeters;
    }

    [Serializable]
    public struct MountPose
    {
        [SerializeField]
        private Vector3 localPosition;

        [SerializeField]
        private Vector3 localEulerAngles;

        public MountPose(Vector3 localPosition, Vector3 localEulerAngles)
        {
            this.localPosition = localPosition;
            this.localEulerAngles = localEulerAngles;
        }

        public Vector3 LocalPosition => localPosition;

        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);

        public static MountPose Capture(Transform transform)
        {
            return transform == null
                ? new MountPose(Vector3.zero, Vector3.zero)
                : new MountPose(transform.localPosition, transform.localEulerAngles);
        }
    }
}
