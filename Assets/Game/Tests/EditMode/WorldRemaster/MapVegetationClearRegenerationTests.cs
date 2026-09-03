using System;
using System.IO;
using MSC.Editor.Vegetation;
using MSC.World.Partition;
using MSC.World.Streaming;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationClearRegenerationTests
    {
        [Test]
        public void FullRegenerationVisitsPreviouslyGeneratedCellsAfterGroundScopeShrinks()
        {
            var surviving = new WorldCellIndex(0, 0);
            var stale = new WorldCellIndex(-3, 2);
            var otherLayer = new WorldCellIndex(7, 7);
            var layers = new[]
            {
                new ProductionWorldCellLayerScene("vegetation", stale, 1, MapVegetationRebuild.CellScenePath(stale)),
                new ProductionWorldCellLayerScene("lighting", otherLayer, 2, "Assets/OtherLayer.unity"),
                new ProductionWorldCellLayerScene("vegetation", surviving, 3, MapVegetationRebuild.CellScenePath(surviving))
            };
            WorldCellIndex[] cells = MapVegetationRebuild.CollectFullRebuildCells(new[] { surviving }, layers);
            Assert.That(cells, Is.EqualTo(new[] { surviving, stale }),
                "An existing vegetation cell must be rewritten even if new source/ground evidence has no candidates there; otherwise unrelated categories disappear from streaming.");
            Assert.That(Array.IndexOf(cells, otherLayer), Is.EqualTo(-1));
            Array.Reverse(layers);
            Assert.That(MapVegetationRebuild.CollectFullRebuildCells(new[] { surviving, surviving }, layers), Is.EqualTo(cells));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SavedClearedCellCanBeRegeneratedWithoutLosingManualRoots(bool retainManualRoot)
        {
            var cell = new WorldCellIndex(Guid.NewGuid().GetHashCode(), 918273);
            string path = MapVegetationRebuild.CellScenePath(cell);
            Assert.That(File.Exists(path), Is.False, "A test never overwrites an existing generated cell.");
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = default;
            ProductionWorldStreamingManifest manifest = ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
            bool saved = false;
            try
            {
                CopyFixtureScene(path);
                saved = true;
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                RemoveCopiedFixtureRoots(scene);
                SceneManager.SetActiveScene(scene);
                var generated = new GameObject("OriginalTrees");
                generated.AddComponent<GeneratedVegetationGroup>().Configure(MapVegetationRebuildOptions.GeneratorId,
                    cell.Id, MapVegetationCategories.OriginalTrees.ToString(), "before-clear");
                if (retainManualRoot) new GameObject("UserAuthoredPlant");
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                saved = true;

                // This is the saved scene state produced by Clear: matching
                // generated roots removed, unrelated manual roots untouched.
                UnityEngine.Object.DestroyImmediate(generated);
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                EditorSceneManager.CloseScene(scene, true);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                if (retainManualRoot)
                    manifest.ConfigureCellLayersForAuthoring(new[]
                    {
                        new ProductionWorldCellLayerScene("vegetation", cell, 0, path)
                    });

                Assert.DoesNotThrow(() => MapVegetationRebuild.ValidateExistingCellSceneForRegeneration(scene, cell, manifest));
                SceneManager.SetActiveScene(scene);
                var replacement = new GameObject("OriginalTrees");
                replacement.AddComponent<GeneratedVegetationGroup>().Configure(MapVegetationRebuildOptions.GeneratorId,
                    cell.Id, MapVegetationCategories.OriginalTrees.ToString(), "after-regenerate");
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                Assert.That(scene.GetRootGameObjects().Length, Is.EqualTo(retainManualRoot ? 2 : 1));
                if (retainManualRoot)
                    Assert.That(Array.Exists(scene.GetRootGameObjects(), root => root.name == "UserAuthoredPlant"), Is.True);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                UnityEngine.Object.DestroyImmediate(manifest);
                if (saved) AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void UnregisteredNonemptySceneIsStillRejectedWithoutDeletingItsObjects()
        {
            var cell = new WorldCellIndex(Guid.NewGuid().GetHashCode(), 918274);
            string path = MapVegetationRebuild.CellScenePath(cell);
            Assert.That(File.Exists(path), Is.False);
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = default;
            bool saved = false;
            try
            {
                CopyFixtureScene(path);
                saved = true;
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                RemoveCopiedFixtureRoots(scene);
                SceneManager.SetActiveScene(scene);
                var manual = new GameObject("DoNotOverwrite");
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                saved = true;
                Assert.Throws<InvalidOperationException>(() =>
                    MapVegetationRebuild.ValidateExistingCellSceneForRegeneration(scene, cell, null));
                Assert.That(manual != null, Is.True);
                Assert.That(scene.GetRootGameObjects().Length, Is.EqualTo(1));
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (saved) AssetDatabase.DeleteAsset(path);
            }
        }

        private static void CopyFixtureScene(string path)
        {
            // EditMode's runner owns an untitled scene. A second untitled scene
            // cannot be created additively, but a saved fixture can be opened
            // without saving, closing or otherwise changing the runner's scene.
            const string template = "Assets/Game/World/Debug/Streaming/CellLayerBaseFixture.unity";
            Assert.That(AssetDatabase.CopyAsset(template, path), Is.True);
        }

        private static void RemoveCopiedFixtureRoots(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
