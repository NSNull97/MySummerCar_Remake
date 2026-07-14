using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.LegacyImport.Editor.Configuration;
using MSC.LegacyImport.Editor.Pipeline;
using MSC.LegacyImport.Editor.Validation;
using MSC.World.LayoutPilot;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.WorldLayoutPilot
{
    public static class WorldLayoutPilotValidator
    {
        private const float CoordinateToleranceMeters = 0.01f;
        private static readonly int[] ExpectedWaypointIndices =
        {
            1655, 1660, 1665, 1670, 1675, 1680, 1685
        };

        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            WorldLayoutPilotData data = LoadData(errors);
            if (data == null)
            {
                return errors;
            }

            ValidateHeader(data, errors);
            ValidateSourceRecords(data, errors);
            ValidateRoadSamples(data, errors);
            ValidateMeasurements(data, errors);
            ValidateExternalManifest(data, errors);
            ValidateAssetBoundaries(data, errors);
            return errors;
        }

        public static WorldLayoutPilotData LoadDataOrThrow()
        {
            var errors = new List<string>();
            WorldLayoutPilotData data = LoadData(errors);
            if (data == null)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }

            return data;
        }

        [MenuItem("Tools/My Summer Car/Milestone 04A/Validate World Layout Pilot")]
        public static void ValidateMenu()
        {
            ThrowIfInvalid();
            Debug.Log("M04A_WORLD_LAYOUT_VALIDATION_OK");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Milestone 04A batch validation requires batch mode.");
            }

            ThrowIfInvalid();
            Debug.Log("M04A_WORLD_LAYOUT_VALIDATION_OK");
        }

        private static void ThrowIfInvalid()
        {
            IReadOnlyList<string> errors = Validate();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Milestone 04A validation failed:\n- " + string.Join("\n- ", errors));
            }
        }

        private static WorldLayoutPilotData LoadData(List<string> errors)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(WorldLayoutPilotPaths.DataAsset);
            if (asset == null)
            {
                errors.Add("Missing durable world-layout data: " + WorldLayoutPilotPaths.DataAsset);
                return null;
            }

            try
            {
                return WorldLayoutPilotData.FromJson(asset.text);
            }
            catch (Exception exception)
            {
                errors.Add("World-layout JSON is invalid: " + exception.Message);
                return null;
            }
        }

        private static void ValidateHeader(WorldLayoutPilotData data, List<string> errors)
        {
            if (data.SchemaVersion != WorldLayoutPilotData.CurrentSchemaVersion)
            {
                errors.Add($"Unsupported schema version: {data.SchemaVersion}.");
            }

            if (!StableEntityId.TryParse(data.StableId, out _))
            {
                errors.Add("Pilot zone has no canonical project-owned stable ID.");
            }

            if (!string.Equals(data.Name, "garage-road-layout-pilot", StringComparison.Ordinal) ||
                !string.Equals(data.ReviewStatus, "ReviewedBoundedPilot", StringComparison.Ordinal))
            {
                errors.Add("Pilot name or review status is not the approved bounded-pilot value.");
            }

            if (data.DonorWorldBounds == null || !data.DonorWorldBounds.IsOrdered ||
                data.ProjectLocalBounds == null || !data.ProjectLocalBounds.IsOrdered)
            {
                errors.Add("Pilot bounds are absent or unordered.");
            }

            if (data.LandmarkAllowList == null || data.LandmarkAllowList.Count != 4)
            {
                errors.Add("Pilot must retain exactly four declared allow-list entries.");
            }

            if (string.IsNullOrWhiteSpace(data.CoordinateConversion) ||
                string.IsNullOrWhiteSpace(data.MeshLocalException) ||
                data.KnownLimitations == null || data.KnownLimitations.Count < 5)
            {
                errors.Add("Coordinate convention, mesh-local exception, or known limitations are incomplete.");
            }

            if (!IsProjectAssetPath(data.ComparisonScenePath) ||
                !string.Equals(data.ComparisonScenePath, WorldLayoutPilotPaths.ComparisonScene, StringComparison.Ordinal))
            {
                errors.Add("Comparison scene path is not the approved ReferenceOnly path.");
            }
        }

        private static void ValidateSourceRecords(WorldLayoutPilotData data, List<string> errors)
        {
            if (data.SourceRecords == null || data.SourceRecords.Count != 3)
            {
                errors.Add("Pilot must contain exactly three bounded source records.");
                return;
            }

            var stableIds = new HashSet<string>(StringComparer.Ordinal) { data.StableId };
            foreach (WorldLayoutSourceRecord record in data.SourceRecords)
            {
                if (record == null)
                {
                    errors.Add("Source records contain a null item.");
                    continue;
                }

                ValidateStableId(record.StableId, "source record", stableIds, errors);
                ValidateCommonProvenance(
                    record.SourceRelativeContainerPath,
                    record.SourceSha256,
                    record.CoordinateConversion,
                    record.ToolId,
                    record.ToolVersion,
                    record.Dependencies,
                    record.KnownDifferences,
                    "source record " + record.Name,
                    errors);

                if (string.IsNullOrWhiteSpace(record.Name) ||
                    string.IsNullOrWhiteSpace(record.SourceObjectIdentity) ||
                    string.IsNullOrWhiteSpace(record.Status))
                {
                    errors.Add("Source record identity or status is incomplete: " + record.StableId);
                }

                bool isBlockedTerrain = string.Equals(
                    record.TransferClassification,
                    "Blocked",
                    StringComparison.Ordinal);
                bool isReviewedReference = string.Equals(
                    record.TransferClassification,
                    "WorldLayoutReference",
                    StringComparison.Ordinal);
                if (!isBlockedTerrain && !isReviewedReference)
                {
                    errors.Add("Unsupported transfer classification on source record: " + record.Name);
                }

                if (isBlockedTerrain)
                {
                    if (!string.IsNullOrEmpty(record.DestinationPath) ||
                        !string.Equals(record.Status, "BlockedNoBoundedMeshExport", StringComparison.Ordinal))
                    {
                        errors.Add("Blocked terrain record must have no destination and an explicit blocked status.");
                    }
                }
                else if (!string.Equals(record.DestinationPath, WorldLayoutPilotPaths.DataAsset, StringComparison.Ordinal))
                {
                    errors.Add("Reviewed source record has an unexpected destination: " + record.Name);
                }
            }
        }

        private static void ValidateRoadSamples(WorldLayoutPilotData data, List<string> errors)
        {
            if (data.RoadSamples == null || data.RoadSamples.Count != ExpectedWaypointIndices.Length)
            {
                errors.Add($"Pilot must contain exactly {ExpectedWaypointIndices.Length} road samples.");
                return;
            }

            var stableIds = new HashSet<string>(StringComparer.Ordinal) { data.StableId };
            foreach (WorldLayoutSourceRecord source in data.SourceRecords)
            {
                if (source != null)
                {
                    stableIds.Add(source.StableId);
                }
            }

            string roadSourceId = data.SourceRecords
                .Where(record => record != null && record.Name == "adjacent-dirt-road-route-segment")
                .Select(record => record.StableId)
                .SingleOrDefault();

            for (int index = 0; index < data.RoadSamples.Count; index++)
            {
                WorldLayoutRoadSample sample = data.RoadSamples[index];
                if (sample == null)
                {
                    errors.Add("Road samples contain a null item.");
                    continue;
                }

                ValidateStableId(sample.StableId, "road sample", stableIds, errors);
                if (sample.WaypointIndex != ExpectedWaypointIndices[index])
                {
                    errors.Add($"Unexpected waypoint at sample {index}: {sample.WaypointIndex}.");
                }

                if (sample.GameObjectPathId <= 0 || sample.TransformPathId <= 0)
                {
                    errors.Add("Road sample is missing donor object PathIDs: " + sample.WaypointIndex);
                }

                if (!string.Equals(sample.SourceRecordStableId, roadSourceId, StringComparison.Ordinal) ||
                    !string.Equals(sample.TransferClassification, "WorldLayoutReference", StringComparison.Ordinal) ||
                    !string.Equals(sample.DestinationPath, WorldLayoutPilotPaths.DataAsset, StringComparison.Ordinal))
                {
                    errors.Add("Road sample provenance link is invalid: " + sample.WaypointIndex);
                }

                ValidateCommonProvenance(
                    sample.SourceRelativeContainerPath,
                    sample.SourceSha256,
                    sample.CoordinateConversion,
                    sample.ToolId,
                    sample.ToolVersion,
                    sample.Dependencies,
                    sample.KnownDifferences,
                    "road sample " + sample.WaypointIndex,
                    errors);

                Vector3 converted = WorldLayoutCoordinateConverter.DonorWorldToProjectLocal(
                    sample.DonorWorldMeters,
                    data.GarageAnchorDonorWorldMeters);
                if (WorldLayoutCoordinateConverter.MaximumComponentDelta(
                        converted,
                        sample.ProjectLocalMeters) > CoordinateToleranceMeters)
                {
                    errors.Add("Coordinate conversion mismatch at waypoint " + sample.WaypointIndex);
                }

                if (!data.DonorWorldBounds.Contains(sample.DonorWorldMeters, CoordinateToleranceMeters) ||
                    !data.ProjectLocalBounds.Contains(sample.ProjectLocalMeters, CoordinateToleranceMeters))
                {
                    errors.Add("Road sample lies outside the approved pilot bounds: " + sample.WaypointIndex);
                }
            }
        }

        private static void ValidateMeasurements(WorldLayoutPilotData data, List<string> errors)
        {
            float polylineLength = WorldLayoutCoordinateConverter.CalculatePolylineLength(data.RoadSamples);
            if (Mathf.Abs(polylineLength - data.MeasuredPolylineLengthMeters) > CoordinateToleranceMeters ||
                polylineLength < 150f || polylineLength > 250f)
            {
                errors.Add($"Road polyline measurement is invalid: {polylineLength:F3} m.");
            }

            float nearestDistance = WorldLayoutCoordinateConverter.DistanceToNearestSample(
                Vector3.zero,
                data.RoadSamples);
            if (Mathf.Abs(nearestDistance - data.GarageToNearestRoadSampleMeters) > CoordinateToleranceMeters ||
                nearestDistance < 180f || nearestDistance > 250f)
            {
                errors.Add($"Garage-to-road measurement is invalid: {nearestDistance:F3} m.");
            }

            Vector3 anchorProjectLocal = WorldLayoutCoordinateConverter.DonorWorldToProjectLocal(
                data.GarageAnchorDonorWorldMeters,
                data.GarageAnchorDonorWorldMeters);
            if (anchorProjectLocal != Vector3.zero ||
                !data.DonorWorldBounds.Contains(data.GarageAnchorDonorWorldMeters) ||
                !data.ProjectLocalBounds.Contains(anchorProjectLocal))
            {
                errors.Add("Garage anchor does not map to project-local origin inside the pilot bounds.");
            }
        }

        private static void ValidateExternalManifest(WorldLayoutPilotData data, List<string> errors)
        {
            string configPath = Path.GetFullPath(WorldLayoutPilotPaths.LocalConfiguration);
            if (!File.Exists(configPath))
            {
                errors.Add("Missing ignored local donor-path configuration: " + WorldLayoutPilotPaths.LocalConfiguration);
                return;
            }

            try
            {
                DonorPathConfiguration configuration = DonorPathConfiguration.LoadFromFile(configPath);
                errors.AddRange(DonorPipelineRootValidator.Validate(
                    configuration.OriginalGameDirectory,
                    configuration.DonorStagingDirectory,
                    configuration.UnityProjectDirectory));

                if (!IsPortableRelativePath(data.StagingManifestRelativePath) ||
                    !IsCanonicalSha256(data.StagingManifestSha256))
                {
                    errors.Add("Staging-manifest relative path or hash is invalid.");
                    return;
                }

                string manifestPath = Path.GetFullPath(Path.Combine(
                    configuration.DonorStagingDirectory,
                    data.StagingManifestRelativePath));
                string stagingRoot = DonorPathConfiguration.NormalizeDirectoryPath(
                    configuration.DonorStagingDirectory) + Path.DirectorySeparatorChar;
                if (!manifestPath.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Staging manifest resolves outside the configured staging root.");
                }
                else if (!File.Exists(manifestPath))
                {
                    errors.Add("Missing external bounded-inspection manifest.");
                }
                else if (!Sha256FileHasher.Matches(manifestPath, data.StagingManifestSha256))
                {
                    errors.Add("External bounded-inspection manifest hash does not match durable data.");
                }
            }
            catch (Exception exception)
            {
                errors.Add("Could not validate external staging manifest: " + exception.Message);
            }
        }

        private static void ValidateAssetBoundaries(WorldLayoutPilotData data, List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(data.ComparisonScenePath) == null)
            {
                errors.Add("Missing ignored ReferenceOnly comparison scene.");
            }

            if (EditorBuildSettings.scenes.Any(scene => string.Equals(
                    scene.path,
                    data.ComparisonScenePath,
                    StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("ReferenceOnly comparison scene must not be listed in Build Settings.");
            }

            string[] productionAssets =
            {
                WorldLayoutPilotPaths.DataAsset,
                WorldLayoutPilotPaths.GarageShellPrefab,
                WorldLayoutPilotPaths.RoadPrefab,
                WorldLayoutPilotPaths.ProductionScene
            };
            foreach (string assetPath in productionAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
                {
                    errors.Add("Missing required project-owned asset: " + assetPath);
                    continue;
                }

                foreach (string dependency in AssetDatabase.GetDependencies(assetPath, true))
                {
                    if (IsDonorDependency(dependency))
                    {
                        errors.Add($"Project-owned asset '{assetPath}' depends on donor/reference content: {dependency}");
                    }
                }
            }

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes.Where(scene => scene.enabled))
            {
                foreach (string dependency in AssetDatabase.GetDependencies(scene.path, true))
                {
                    if (IsDonorDependency(dependency))
                    {
                        errors.Add($"Enabled build scene '{scene.path}' depends on donor/reference content: {dependency}");
                    }
                }
            }
        }

        private static void ValidateStableId(
            string value,
            string role,
            HashSet<string> stableIds,
            List<string> errors)
        {
            if (!StableEntityId.TryParse(value, out _))
            {
                errors.Add($"Invalid project-owned stable ID for {role}: {value}");
            }
            else if (!stableIds.Add(value))
            {
                errors.Add($"Duplicate project-owned stable ID for {role}: {value}");
            }
        }

        private static void ValidateCommonProvenance(
            string sourcePath,
            string sourceHash,
            string conversion,
            string toolId,
            string toolVersion,
            string dependencies,
            string differences,
            string role,
            List<string> errors)
        {
            if (!IsPortableRelativePath(sourcePath) ||
                !IsCanonicalSha256(sourceHash) ||
                string.IsNullOrWhiteSpace(conversion) ||
                string.IsNullOrWhiteSpace(toolId) ||
                string.IsNullOrWhiteSpace(toolVersion) ||
                string.IsNullOrWhiteSpace(dependencies) ||
                string.IsNullOrWhiteSpace(differences))
            {
                errors.Add("Incomplete or machine-specific provenance on " + role + ".");
            }
        }

        private static bool IsPortableRelativePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   !Path.IsPathRooted(path) &&
                   path.IndexOf(':') < 0 &&
                   !path.Split('/', '\\').Any(part => part == "..");
        }

        private static bool IsProjectAssetPath(string path)
        {
            return IsPortableRelativePath(path) &&
                   path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static bool IsCanonicalSha256(string value)
        {
            return value != null && value.Length == 64 &&
                   value.All(character =>
                       character >= '0' && character <= '9' ||
                       character >= 'a' && character <= 'f');
        }

        private static bool IsDonorDependency(string path)
        {
            return path.StartsWith(
                       "Assets/Game/LegacyImport/ReferenceOnly/",
                       StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(
                       "Assets/Game/Imported/DonorGenerated/",
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
