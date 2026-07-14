using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.World.LayoutPilot
{
    [Serializable]
    public sealed class WorldLayoutPilotData
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion;
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string name = string.Empty;
        [SerializeField] private string reviewStatus = string.Empty;
        [SerializeField] private WorldLayoutBoundsRecord donorWorldBounds = new WorldLayoutBoundsRecord();
        [SerializeField] private WorldLayoutBoundsRecord projectLocalBounds = new WorldLayoutBoundsRecord();
        [SerializeField] private Vector3 garageAnchorDonorWorldMeters;
        [SerializeField] private string coordinateConversion = string.Empty;
        [SerializeField] private string meshLocalException = string.Empty;
        [SerializeField] private string stagingManifestRelativePath = string.Empty;
        [SerializeField] private string stagingManifestSha256 = string.Empty;
        [SerializeField] private string comparisonScenePath = string.Empty;
        [SerializeField] private WorldLayoutSourceRecord[] sourceRecords = Array.Empty<WorldLayoutSourceRecord>();
        [SerializeField] private WorldLayoutRoadSample[] roadSamples = Array.Empty<WorldLayoutRoadSample>();
        [SerializeField] private float measuredPolylineLengthMeters;
        [SerializeField] private float garageToNearestRoadSampleMeters;
        [SerializeField] private string[] landmarkAllowList = Array.Empty<string>();
        [SerializeField] private string[] knownLimitations = Array.Empty<string>();

        public int SchemaVersion => schemaVersion;
        public string StableId => stableId;
        public string Name => name;
        public string ReviewStatus => reviewStatus;
        public WorldLayoutBoundsRecord DonorWorldBounds => donorWorldBounds;
        public WorldLayoutBoundsRecord ProjectLocalBounds => projectLocalBounds;
        public Vector3 GarageAnchorDonorWorldMeters => garageAnchorDonorWorldMeters;
        public string CoordinateConversion => coordinateConversion;
        public string MeshLocalException => meshLocalException;
        public string StagingManifestRelativePath => stagingManifestRelativePath;
        public string StagingManifestSha256 => stagingManifestSha256;
        public string ComparisonScenePath => comparisonScenePath;
        public IReadOnlyList<WorldLayoutSourceRecord> SourceRecords => sourceRecords;
        public IReadOnlyList<WorldLayoutRoadSample> RoadSamples => roadSamples;
        public float MeasuredPolylineLengthMeters => measuredPolylineLengthMeters;
        public float GarageToNearestRoadSampleMeters => garageToNearestRoadSampleMeters;
        public IReadOnlyList<string> LandmarkAllowList => landmarkAllowList;
        public IReadOnlyList<string> KnownLimitations => knownLimitations;

        public static WorldLayoutPilotData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("World-layout pilot JSON must not be empty.", nameof(json));
            }

            WorldLayoutPilotData data = JsonUtility.FromJson<WorldLayoutPilotData>(json);
            if (data == null)
            {
                throw new FormatException("World-layout pilot JSON could not be parsed.");
            }

            return data;
        }
    }

    [Serializable]
    public sealed class WorldLayoutBoundsRecord
    {
        [SerializeField] private Vector3 minMeters;
        [SerializeField] private Vector3 maxMeters;

        public Vector3 MinMeters => minMeters;
        public Vector3 MaxMeters => maxMeters;

        public bool Contains(Vector3 point, float toleranceMeters = 0f)
        {
            return point.x >= minMeters.x - toleranceMeters &&
                   point.y >= minMeters.y - toleranceMeters &&
                   point.z >= minMeters.z - toleranceMeters &&
                   point.x <= maxMeters.x + toleranceMeters &&
                   point.y <= maxMeters.y + toleranceMeters &&
                   point.z <= maxMeters.z + toleranceMeters;
        }

        public bool IsOrdered =>
            minMeters.x <= maxMeters.x &&
            minMeters.y <= maxMeters.y &&
            minMeters.z <= maxMeters.z;
    }

    [Serializable]
    public sealed class WorldLayoutSourceRecord
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string name = string.Empty;
        [SerializeField] private string sourceRelativeContainerPath = string.Empty;
        [SerializeField] private string sourceObjectIdentity = string.Empty;
        [SerializeField] private string sourceSha256 = string.Empty;
        [SerializeField] private string transferClassification = string.Empty;
        [SerializeField] private string status = string.Empty;
        [SerializeField] private string coordinateConversion = string.Empty;
        [SerializeField] private string destinationPath = string.Empty;
        [SerializeField] private string toolId = string.Empty;
        [SerializeField] private string toolVersion = string.Empty;
        [SerializeField] private string dependencies = string.Empty;
        [SerializeField] private string knownDifferences = string.Empty;

        public string StableId => stableId;
        public string Name => name;
        public string SourceRelativeContainerPath => sourceRelativeContainerPath;
        public string SourceObjectIdentity => sourceObjectIdentity;
        public string SourceSha256 => sourceSha256;
        public string TransferClassification => transferClassification;
        public string Status => status;
        public string CoordinateConversion => coordinateConversion;
        public string DestinationPath => destinationPath;
        public string ToolId => toolId;
        public string ToolVersion => toolVersion;
        public string Dependencies => dependencies;
        public string KnownDifferences => knownDifferences;
    }

    [Serializable]
    public sealed class WorldLayoutRoadSample
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string sourceRecordStableId = string.Empty;
        [SerializeField] private int waypointIndex;
        [SerializeField] private long gameObjectPathId;
        [SerializeField] private long transformPathId;
        [SerializeField] private string sourceRelativeContainerPath = string.Empty;
        [SerializeField] private string sourceSha256 = string.Empty;
        [SerializeField] private string transferClassification = string.Empty;
        [SerializeField] private string coordinateConversion = string.Empty;
        [SerializeField] private string destinationPath = string.Empty;
        [SerializeField] private string toolId = string.Empty;
        [SerializeField] private string toolVersion = string.Empty;
        [SerializeField] private string dependencies = string.Empty;
        [SerializeField] private string knownDifferences = string.Empty;
        [SerializeField] private Vector3 donorWorldMeters;
        [SerializeField] private Vector3 projectLocalMeters;

        public string StableId => stableId;
        public string SourceRecordStableId => sourceRecordStableId;
        public int WaypointIndex => waypointIndex;
        public long GameObjectPathId => gameObjectPathId;
        public long TransformPathId => transformPathId;
        public string SourceRelativeContainerPath => sourceRelativeContainerPath;
        public string SourceSha256 => sourceSha256;
        public string TransferClassification => transferClassification;
        public string CoordinateConversion => coordinateConversion;
        public string DestinationPath => destinationPath;
        public string ToolId => toolId;
        public string ToolVersion => toolVersion;
        public string Dependencies => dependencies;
        public string KnownDifferences => knownDifferences;
        public Vector3 DonorWorldMeters => donorWorldMeters;
        public Vector3 ProjectLocalMeters => projectLocalMeters;
    }

    public static class WorldLayoutCoordinateConverter
    {
        public const float UnitScaleMeters = 1f;

        public static Vector3 DonorWorldToProjectLocal(
            Vector3 donorWorldMeters,
            Vector3 garageAnchorDonorWorldMeters)
        {
            return (donorWorldMeters - garageAnchorDonorWorldMeters) * UnitScaleMeters;
        }

        public static Vector3 ProjectLocalToDonorWorld(
            Vector3 projectLocalMeters,
            Vector3 garageAnchorDonorWorldMeters)
        {
            return projectLocalMeters / UnitScaleMeters + garageAnchorDonorWorldMeters;
        }

        public static float MaximumComponentDelta(Vector3 left, Vector3 right)
        {
            Vector3 delta = left - right;
            return Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z));
        }

        public static float CalculatePolylineLength(IReadOnlyList<WorldLayoutRoadSample> samples)
        {
            if (samples == null || samples.Count < 2)
            {
                return 0f;
            }

            float lengthMeters = 0f;
            for (int index = 1; index < samples.Count; index++)
            {
                lengthMeters += Vector3.Distance(
                    samples[index - 1].ProjectLocalMeters,
                    samples[index].ProjectLocalMeters);
            }

            return lengthMeters;
        }

        public static float DistanceToNearestSample(
            Vector3 projectLocalPoint,
            IReadOnlyList<WorldLayoutRoadSample> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                return float.PositiveInfinity;
            }

            float nearestMeters = float.PositiveInfinity;
            for (int index = 0; index < samples.Count; index++)
            {
                nearestMeters = Mathf.Min(
                    nearestMeters,
                    Vector3.Distance(projectLocalPoint, samples[index].ProjectLocalMeters));
            }

            return nearestMeters;
        }
    }
}
