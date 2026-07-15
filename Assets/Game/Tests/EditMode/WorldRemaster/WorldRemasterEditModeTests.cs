using System;
using System.Linq;
using MSC.World.Partition;
using MSC.World.Remaster;
using MSC.World.Remaster.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class WorldRemasterEditModeTests
    {
        [Test]
        public void Registry_SerializesAllReferenceRecords()
        {
            WorldProductionAssetRegistry registry = LoadRegistry();
            string json = JsonUtility.ToJson(registry);
            Assert.That(json, Does.Contain("\"pilotZoneId\":\"cell_0_-3\""));
            Assert.That(registry.Records.Count, Is.EqualTo(13509));
            Assert.That(WorldProductionRegistryValidation.Validate(registry), Is.Empty);
        }

        [Test]
        public void Registry_HasStableGarageDoorBindings()
        {
            WorldProductionAssetRegistry registry = LoadRegistry();
            WorldProductionAssetRecord left = registry.Records.Single(record =>
                record.StableWorldId == "250d1cb74558e7c7e27e5e860981c938");
            WorldProductionAssetRecord right = registry.Records.Single(record =>
                record.StableWorldId == "e8660bda40e1d2946e004456e245c803");
            Assert.That(left.ProductionPrefab, Is.EqualTo(WorldRemasterPaths.GaragePrefab));
            Assert.That(right.ProductionPrefab, Is.EqualTo(WorldRemasterPaths.GaragePrefab));
            Assert.That(left.ReplacementStatus, Is.EqualTo(WorldReplacementStatus.ProductionCandidate));
            Assert.That(right.ReplacementStatus, Is.EqualTo(WorldReplacementStatus.ProductionCandidate));
        }

        [Test]
        public void ProductionAssets_DoNotDependOnDonorMeshesOrTextures()
        {
            Assert.That(ProductionCellValidationTool.FindDonorDependencies(), Is.Empty);
        }

        [Test]
        public void ProductionMaterials_AreHdrpAndDonorIndependent()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { WorldRemasterPaths.MaterialRoot });
            Assert.That(guids.Length, Is.GreaterThanOrEqualTo(14));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material, Is.Not.Null, path);
                Assert.That(material.shader.name, Does.StartWith("HDRP/"), path);
                string[] dependencies = AssetDatabase.GetDependencies(path, true);
                Assert.That(dependencies.Any(dependency =>
                    dependency.Contains("/LegacyImport/ReferenceOnly/", StringComparison.OrdinalIgnoreCase) ||
                    dependency.Contains("/Imported/DonorGenerated/", StringComparison.OrdinalIgnoreCase)), Is.False, path);
            }
        }

        [Test]
        public void BoundsParity_ReturnsMaximumCenterOrSizeDeviation()
        {
            var expected = new Bounds(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f));
            var measured = new Bounds(new Vector3(1.1f, 2.3f, 3f), new Vector3(4f, 4.6f, 6f));
            Assert.That(WorldProductionRegistryValidation.MaximumBoundsDeviation(expected, measured), Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void PivotParity_UsesMetricDistance()
        {
            Assert.That(
                WorldProductionRegistryValidation.PivotDeviation(Vector3.zero, new Vector3(0.3f, 0.4f, 0f)),
                Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void RoadDeviation_RejectsDifferentSampleCounts()
        {
            Vector3[] reference = { Vector3.zero, Vector3.forward };
            Vector3[] production = { Vector3.zero };
            Assert.That(WorldProductionRegistryValidation.MaximumRoadDeviation(reference, production), Is.EqualTo(float.PositiveInfinity));
            Assert.That(
                WorldProductionRegistryValidation.MaximumRoadDeviation(
                    reference,
                    new[] { Vector3.zero, new Vector3(0.2f, 0f, 1f) }),
                Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void PilotAnchor_AssignsToCellZeroMinusThree()
        {
            Assert.That(
                WorldCellMembershipUtility.FromPosition(WorldRemasterPaths.HomeGarageAnchor, 512f).Id,
                Is.EqualTo(WorldRemasterPaths.PilotZoneId));
        }

        [Test]
        public void GeneratedCell_DependencyHashIsStableWithinUnchangedInputs()
        {
            Hash128 first = AssetDatabase.GetAssetDependencyHash(WorldRemasterPaths.PilotCellScene);
            Hash128 second = AssetDatabase.GetAssetDependencyHash(WorldRemasterPaths.PilotCellScene);
            Assert.That(first.isValid, Is.True);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void PilotPrefab_HasMaterialLodAndCollisionCoverage()
        {
            GameObject pilot = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.PilotZonePrefab);
            Assert.That(pilot, Is.Not.Null);
            Assert.That(pilot.GetComponentsInChildren<LODGroup>(true).Length, Is.GreaterThanOrEqualTo(64));
            Assert.That(pilot.GetComponentsInChildren<MeshCollider>(true).Count(collider => collider.sharedMesh != null), Is.GreaterThanOrEqualTo(3));
            Assert.That(pilot.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.sharedMaterial != null), Is.True);
        }

        [Test]
        public void Registry_ReportsEveryMissingReplacementThroughBacklog()
        {
            WorldProductionAssetRegistry registry = LoadRegistry();
            WorldProductionAssetRecord[] missing = registry.Records.Where(record => !record.HasProductionReplacement).ToArray();
            Assert.That(missing.Length, Is.EqualTo(13485));
            Assert.That(missing.All(record => !string.IsNullOrWhiteSpace(record.ManualArtDependency)), Is.True);
            Assert.That(registry.ArtTasks.Count, Is.EqualTo(263));
        }

        [Test]
        public void PilotZone_StatusKeepsExactRawCoverage()
        {
            WorldProductionZoneRecord pilot = LoadRegistry().Zones.Single(zone => zone.ZoneId == WorldRemasterPaths.PilotZoneId);
            Assert.That(pilot.Status, Is.EqualTo(WorldReplacementStatus.ProductionCandidate));
            Assert.That(pilot.ReferenceRecordCount, Is.EqualTo(671));
            Assert.That(pilot.MappedRecordCount, Is.EqualTo(24));
            Assert.That(pilot.CoveragePercent, Is.EqualTo(3.57675f).Within(0.0001f));
            Assert.That(pilot.ManualValidation, Is.EqualTo("Pending"));
        }

        [Test]
        public void InvalidProductionDependency_IsRejected()
        {
            var record = new WorldProductionAssetRecord();
            record.Configure(
                "test-invalid",
                "donor",
                "StaticProp",
                "Assets/Game/LegacyImport/ReferenceOnly/invalid.prefab",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                default,
                default,
                0f,
                Vector3.zero,
                WorldReplacementStatus.ProductionCandidate,
                "Test",
                "Test",
                "cell_0_0",
                "Test",
                "Test",
                "Test",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "1");
            var zone = new WorldProductionZoneRecord();
            zone.Configure("cell_0_0", 1, 1, WorldReplacementStatus.ProductionCandidate, "Test", "Pending", string.Empty, string.Empty);
            var registry = ScriptableObject.CreateInstance<WorldProductionAssetRegistry>();
            try
            {
                registry.Configure("test", "test", "cell_0_0", new[] { record }, new[] { zone }, Array.Empty<WorldArtTaskRecord>());
                Assert.That(
                    WorldProductionRegistryValidation.Validate(registry).Any(error => error.Contains("donor/reference", StringComparison.OrdinalIgnoreCase)),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(registry);
            }
        }

        private static WorldProductionAssetRegistry LoadRegistry()
        {
            WorldProductionAssetRegistry registry =
                AssetDatabase.LoadAssetAtPath<WorldProductionAssetRegistry>(WorldRemasterPaths.RegistryAsset);
            Assert.That(registry, Is.Not.Null);
            return registry;
        }
    }
}
