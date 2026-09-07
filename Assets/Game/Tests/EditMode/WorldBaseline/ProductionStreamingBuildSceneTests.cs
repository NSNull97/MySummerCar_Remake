using System.Linq;
using MSC.Editor.WorldBaseline;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace MSC.Tests.EditMode.WorldBaseline
{
    [Category("LocalDonorBaseline")]
    public sealed class ProductionStreamingBuildSceneTests
    {
        private ProductionWorldStreamingManifest manifest;

        [SetUp]
        public void RequireLocalProductionManifest()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldBaseline06B2Paths.GlobalScene) == null)
                Assert.Ignore("Generate the local donor baseline before validating its build scene addresses.");

            manifest = AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(
                WorldBaseline06B2Paths.ActiveManifest);
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.ValidateConfiguration(), Is.Empty);
        }

        [Test]
        public void EnabledBuildScenes_StartWithBootstrapAndAllResolveToExistingAssets()
        {
            EditorBuildSettingsScene[] enabled = EditorBuildSettings.scenes
                .Where(scene => scene.enabled).ToArray();
            Assert.That(enabled, Is.Not.Empty);
            Assert.That(enabled[0].path, Is.EqualTo("Assets/Game/Bootstrap/Bootstrap.unity"));
            Assert.That(enabled.Select(scene => scene.path).Distinct().Count(), Is.EqualTo(enabled.Length));
            for (int index = 0; index < enabled.Length; index++)
            {
                AssertSceneAddress(index, enabled[index].path);
                Assert.That(AssetDatabase.AssetPathToGUID(enabled[index].path),
                    Is.EqualTo(enabled[index].guid.ToString()), "Stale scene GUID: " + enabled[index].path);
            }
        }

        [Test]
        public void GlobalSceneAddresses_MatchUnityBuildIndices()
        {
            Assert.That(manifest.GlobalScenes.Count, Is.GreaterThan(0));
            foreach (ProductionWorldGlobalScene scene in manifest.GlobalScenes)
                AssertSceneAddress(scene.BuildIndex, scene.ScenePath);
        }

        [Test]
        public void CellSceneAddresses_MatchUnityBuildIndices()
        {
            Assert.That(manifest.Cells.Count, Is.GreaterThan(0));
            foreach (ProductionWorldCellScene scene in manifest.Cells)
                AssertSceneAddress(scene.BuildIndex, scene.ScenePath);
        }

        [Test]
        public void CellLayerAddresses_MatchUnityBuildIndices()
        {
            Assert.That(manifest.CellLayers.Count, Is.GreaterThan(0));
            foreach (ProductionWorldCellLayerScene scene in manifest.CellLayers)
                AssertSceneAddress(scene.BuildIndex, scene.ScenePath);
        }

        private static void AssertSceneAddress(int index, string path)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
            Assert.That(SceneUtility.GetScenePathByBuildIndex(index), Is.EqualTo(path),
                "Production streaming build address changed at index " + index);
        }
    }
}
