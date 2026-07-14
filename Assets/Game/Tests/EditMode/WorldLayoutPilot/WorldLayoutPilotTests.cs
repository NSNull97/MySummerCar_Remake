using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.Editor.WorldLayoutPilot;
using MSC.LegacyImport.Editor.Configuration;
using MSC.LegacyImport.Editor.Pipeline;
using MSC.World.LayoutPilot;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldLayoutPilot
{
    public sealed class WorldLayoutPilotTests
    {
        [Test]
        public void Validator_ReportsNoMilestone04AErrors()
        {
            Assert.That(WorldLayoutPilotValidator.Validate(), Is.Empty);
        }

        [Test]
        public void DurableJson_RoundTripsWithoutChangingMeasuredLayout()
        {
            WorldLayoutPilotData source = LoadData();
            string roundTripJson = JsonUtility.ToJson(source);
            WorldLayoutPilotData roundTrip = WorldLayoutPilotData.FromJson(roundTripJson);

            Assert.That(roundTrip.SchemaVersion, Is.EqualTo(source.SchemaVersion));
            Assert.That(roundTrip.StableId, Is.EqualTo(source.StableId));
            Assert.That(roundTrip.RoadSamples.Count, Is.EqualTo(7));
            Assert.That(
                WorldLayoutCoordinateConverter.CalculatePolylineLength(roundTrip.RoadSamples),
                Is.EqualTo(source.MeasuredPolylineLengthMeters).Within(0.01f));
            Assert.That(
                WorldLayoutCoordinateConverter.DistanceToNearestSample(Vector3.zero, roundTrip.RoadSamples),
                Is.EqualTo(source.GarageToNearestRoadSampleMeters).Within(0.01f));
        }

        [Test]
        public void Coordinates_RoundTripAndGarageAnchorMapsToOrigin()
        {
            WorldLayoutPilotData data = LoadData();
            Assert.That(
                WorldLayoutCoordinateConverter.DonorWorldToProjectLocal(
                    data.GarageAnchorDonorWorldMeters,
                    data.GarageAnchorDonorWorldMeters),
                Is.EqualTo(Vector3.zero));

            foreach (WorldLayoutRoadSample sample in data.RoadSamples)
            {
                Vector3 projectLocal = WorldLayoutCoordinateConverter.DonorWorldToProjectLocal(
                    sample.DonorWorldMeters,
                    data.GarageAnchorDonorWorldMeters);
                Vector3 donorWorld = WorldLayoutCoordinateConverter.ProjectLocalToDonorWorld(
                    projectLocal,
                    data.GarageAnchorDonorWorldMeters);
                Assert.That(
                    WorldLayoutCoordinateConverter.MaximumComponentDelta(
                        projectLocal,
                        sample.ProjectLocalMeters),
                    Is.LessThanOrEqualTo(0.0001f));
                Assert.That(
                    WorldLayoutCoordinateConverter.MaximumComponentDelta(
                        donorWorld,
                        sample.DonorWorldMeters),
                    Is.LessThanOrEqualTo(0.0001f));
            }
        }

        [Test]
        public void ExternalManifest_HashAndJsonRoundTripRemainValid()
        {
            WorldLayoutPilotData data = LoadData();
            DonorPathConfiguration configuration = DonorPathConfiguration.LoadFromFile(
                Path.GetFullPath(WorldLayoutPilotPaths.LocalConfiguration));
            string manifestPath = Path.Combine(
                configuration.DonorStagingDirectory,
                data.StagingManifestRelativePath);

            Assert.That(File.Exists(manifestPath), Is.True);
            Assert.That(Sha256FileHasher.Matches(manifestPath, data.StagingManifestSha256), Is.True);

            StagingManifestDto source = JsonUtility.FromJson<StagingManifestDto>(
                File.ReadAllText(manifestPath));
            Assert.That(source, Is.Not.Null);
            Assert.That(source.schemaVersion, Is.EqualTo(1));
            Assert.That(source.sessionId, Is.EqualTo("milestone-04a-world-layout-pilot"));
            Assert.That(source.donorReadOnly, Is.True);
            Assert.That(source.massExportPerformed, Is.False);
            Assert.That(source.sourceContainers, Has.Length.EqualTo(2));
            Assert.That(source.roadSamples, Has.Length.EqualTo(7));

            StagingManifestDto roundTrip = JsonUtility.FromJson<StagingManifestDto>(
                JsonUtility.ToJson(source));
            Assert.That(roundTrip.schemaVersion, Is.EqualTo(source.schemaVersion));
            Assert.That(roundTrip.sessionId, Is.EqualTo(source.sessionId));
            Assert.That(roundTrip.sourceContainers.Select(item => item.sha256),
                Is.EqualTo(source.sourceContainers.Select(item => item.sha256)));
            Assert.That(roundTrip.roadSamples.Select(item => item.waypointIndex),
                Is.EqualTo(source.roadSamples.Select(item => item.waypointIndex)));
        }

        [Test]
        public void StableIds_AreCanonicalAndUnique()
        {
            WorldLayoutPilotData data = LoadData();
            IEnumerable<string> values = new[] { data.StableId }
                .Concat(data.SourceRecords.Select(record => record.StableId))
                .Concat(data.RoadSamples.Select(sample => sample.StableId));
            string[] ids = values.ToArray();

            Assert.That(ids, Has.All.Matches<string>(value => StableEntityId.TryParse(value, out _)));
            Assert.That(ids.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(ids.Length));
        }

        [Test]
        public void TerrainTransfer_IsExplicitlyBlockedAndNoPayloadDestinationExists()
        {
            WorldLayoutSourceRecord terrain = LoadData().SourceRecords.Single(
                record => record.Name == "bounded-terrain-mesh-subset");

            Assert.That(terrain.TransferClassification, Is.EqualTo("Blocked"));
            Assert.That(terrain.Status, Is.EqualTo("BlockedNoBoundedMeshExport"));
            Assert.That(terrain.DestinationPath, Is.Empty);
        }

        private static WorldLayoutPilotData LoadData()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(WorldLayoutPilotPaths.DataAsset);
            Assert.That(asset, Is.Not.Null);
            return WorldLayoutPilotData.FromJson(asset.text);
        }

        [Serializable]
        private sealed class StagingManifestDto
        {
            public int schemaVersion;
            public string sessionId = string.Empty;
            public bool donorReadOnly;
            public bool massExportPerformed;
            public StagingSourceContainerDto[] sourceContainers =
                Array.Empty<StagingSourceContainerDto>();
            public StagingRoadSampleDto[] roadSamples = Array.Empty<StagingRoadSampleDto>();
        }

        [Serializable]
        private sealed class StagingSourceContainerDto
        {
            public string relativePath = string.Empty;
            public string sha256 = string.Empty;
        }

        [Serializable]
        private sealed class StagingRoadSampleDto
        {
            public string stableId = string.Empty;
            public int waypointIndex;
        }
    }
}
