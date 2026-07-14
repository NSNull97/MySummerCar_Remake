using System;
using System.Linq;
using MSC.Editor.GaragePrototype;
using MSC.World.GaragePrototype;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Tests.EditMode.GaragePrototype
{
    public sealed class GaragePrototypeContentTests
    {
        [Test]
        public void Validator_ReportsNoMilestoneThreeErrors()
        {
            Assert.That(GaragePrototypeValidator.Validate(), Is.Empty);
        }

        [Test]
        public void ProductionScene_HasScopedMarkerAndNoDonorDependencies()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GaragePrototypePaths.ProductionScene,
                OpenSceneMode.Single);
            GaragePrototypeSceneMarker marker = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GaragePrototypeSceneMarker>(true))
                .Single();

            Assert.That(marker.SchemaVersion, Is.EqualTo(GaragePrototypeSceneMarker.CurrentSchemaVersion));
            Assert.That(marker.ProductionContentOnly, Is.True);
            Assert.That(marker.ReconstructedRoadLengthMeters, Is.InRange(100f, 500f));
            Assert.That(
                AssetDatabase.GetDependencies(GaragePrototypePaths.ProductionScene, true),
                Has.None.StartsWith("Assets/Game/LegacyImport/ReferenceOnly/"));
            Assert.That(
                AssetDatabase.GetDependencies(GaragePrototypePaths.ProductionScene, true),
                Has.None.StartsWith("Assets/Game/Imported/DonorGenerated/"));
        }

        [Test]
        public void RoofRecord_PreservesMappedBoundsAndPivotWithinFiveMillimeters()
        {
            GarageScalePivotRecord record =
                AssetDatabase.LoadAssetAtPath<GarageScalePivotRecord>(GaragePrototypePaths.ScalePivotRecord);

            Assert.That(record, Is.Not.Null);
            Assert.That(record.IsWithinTolerance, Is.True);
            Assert.That(record.PivotDeltaMeters, Is.LessThanOrEqualTo(0.005f));
            Assert.That(record.MaximumBoundsDeltaMeters, Is.LessThanOrEqualTo(0.005f));
            Assert.That(record.ProductionRoofPrefab, Is.Not.Null);
        }

        [Test]
        public void GaragePrefabs_ContainDoorsWorkbenchShelvesCollisionAndLods()
        {
            GameObject shell = AssetDatabase.LoadAssetAtPath<GameObject>(GaragePrototypePaths.GarageShellPrefab);
            GameObject interior = AssetDatabase.LoadAssetAtPath<GameObject>(GaragePrototypePaths.GarageInteriorPrefab);
            GameObject road = AssetDatabase.LoadAssetAtPath<GameObject>(GaragePrototypePaths.RoadPrefab);

            string[] shellNames = shell.GetComponentsInChildren<Transform>(true).Select(item => item.name).ToArray();
            string[] interiorNames = interior.GetComponentsInChildren<Transform>(true).Select(item => item.name).ToArray();
            Assert.That(shellNames, Does.Contain("VehicleDoor_Left"));
            Assert.That(shellNames, Does.Contain("VehicleDoor_Right"));
            Assert.That(shellNames, Does.Contain("SideDoor"));
            Assert.That(interiorNames, Does.Contain("Workbench"));
            Assert.That(interiorNames, Does.Contain("Shelves"));
            Assert.That(interiorNames, Does.Contain("ToolCabinet"));
            Assert.That(shell.GetComponentsInChildren<Collider>(true), Is.Not.Empty);
            Assert.That(shell.GetComponentsInChildren<LODGroup>(true), Is.Not.Empty);
            Assert.That(road.GetComponentsInChildren<MeshCollider>(true), Is.Not.Empty);
            Assert.That(road.GetComponentsInChildren<LODGroup>(true), Is.Not.Empty);
        }

        [Test]
        public void MaterialLibrary_UsesHdrpPbrTextureSets()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { GaragePrototypePaths.MaterialRoot });
            Assert.That(guids.Length, Is.GreaterThanOrEqualTo(10));
            foreach (string guid in guids)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(material.shader.name, Does.StartWith("HDRP/"));
                Assert.That(material.GetTexture("_BaseColorMap"), Is.Not.Null);
                Assert.That(material.GetTexture("_NormalMap"), Is.Not.Null);
                Assert.That(material.GetTexture("_MaskMap"), Is.Not.Null);
            }
        }

        [TestCase(GaragePrototypePaths.NeutralVolumeProfile)]
        [TestCase(GaragePrototypePaths.LateDayVolumeProfile)]
        public void LightingPreset_UsesPhysicalSkyFogFixedExposureAndAces(string profilePath)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TryGet(out VisualEnvironment environment), Is.True);
            Assert.That(environment.skyType.value, Is.EqualTo((int)SkyType.PhysicallyBased));
            Assert.That(profile.TryGet(out PhysicallyBasedSky _), Is.True);
            Assert.That(profile.TryGet(out Exposure exposure), Is.True);
            Assert.That(exposure.mode.value, Is.EqualTo(ExposureMode.Fixed));
            Assert.That(profile.TryGet(out Fog fog), Is.True);
            Assert.That(fog.enabled.value, Is.True);
            Assert.That(fog.enableVolumetricFog.value, Is.True);
            Assert.That(profile.TryGet(out Tonemapping tonemapping), Is.True);
            Assert.That(tonemapping.mode.value, Is.EqualTo(TonemappingMode.ACES));
        }

        [Test]
        public void StaticCapture_RemainsInsideProvisionalBudgets()
        {
            GaragePrototypeMetricsCapture capture =
                GaragePrototypeMetrics.CollectProductionScene(writeCapture: false);

            Assert.That(capture.triangles, Is.LessThanOrEqualTo(GaragePrototypeMetrics.TriangleBudget));
            Assert.That(capture.renderers, Is.LessThanOrEqualTo(GaragePrototypeMetrics.RendererBudget));
            Assert.That(capture.lights, Is.LessThanOrEqualTo(GaragePrototypeMetrics.LightBudget));
            Assert.That(capture.estimatedTextureBytes,
                Is.LessThanOrEqualTo(GaragePrototypeMetrics.EstimatedTextureBudgetBytes));
            Assert.That(capture.colliders, Is.GreaterThanOrEqualTo(12));
            Assert.That(capture.lodGroups, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void ComparisonScene_IsPresentAndExcludedFromBuild()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(GaragePrototypePaths.ComparisonScene),
                Is.Not.Null);
            Assert.That(
                EditorBuildSettings.scenes.Any(scene => string.Equals(
                    scene.path,
                    GaragePrototypePaths.ComparisonScene,
                    StringComparison.OrdinalIgnoreCase)),
                Is.False);
        }
    }
}
