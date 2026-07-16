using System;
using System.Linq;
using MSC.Development.WorldBaseline;
using MSC.Development.WorldBaseline.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Tests.EditMode.WorldBaseline
{
    public sealed class WorldBaselineVehicleTraversalHarnessEditModeTests
    {
        [Test]
        public void DevelopmentHarness_IsIsolatedConfiguredAndExcludedFromBuild()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    WorldBaselineVehicleTraversalHarness
                        .BootstrapScenePath),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    WorldBaselineVehicleTraversalHarness
                        .VehiclePrototypeScenePath),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    WorldBaselineVehicleTraversalHarness
                        .HarnessScenePath),
                Is.Not.Null);
            Assert.That(
                EditorBuildSettings.scenes.Any(scene =>
                    string.Equals(
                        scene.path,
                        WorldBaselineVehicleTraversalHarness
                            .HarnessScenePath,
                        StringComparison.Ordinal)),
                Is.False);

            string[] expectedColliderIds =
                WorldBaselineVehicleTraversalHarnessBuilder
                    .ReadTraversalColliderIds();
            Assert.That(expectedColliderIds, Has.Length.EqualTo(6));
            Assert.That(
                expectedColliderIds.Distinct(
                    StringComparer.Ordinal).Count(),
                Is.EqualTo(expectedColliderIds.Length));

            Scene scene =
                EditorSceneManager.OpenPreviewScene(
                    WorldBaselineVehicleTraversalHarness
                        .HarnessScenePath);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(1));
                WorldBaselineVehicleTraversalHarness[] harnesses =
                    roots[0].GetComponentsInChildren<
                        WorldBaselineVehicleTraversalHarness>(true);
                Assert.That(harnesses, Has.Length.EqualTo(1));
                Assert.That(
                    harnesses[0].AllowedTraversalColliderIds,
                    Is.EqualTo(expectedColliderIds));
                Assert.That(
                    roots[0].GetComponentsInChildren<
                        Camera>(true),
                    Is.Empty);
                Assert.That(
                    roots[0].GetComponentsInChildren<
                        Light>(true),
                    Is.Empty);
                Assert.That(
                    roots[0].GetComponentsInChildren<
                        Collider>(true),
                    Is.Empty);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
