using MSC.World.Partition;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class ProductionWorldCellLayerTests
    {
        [Test]
        public void OptionalLayersAcceptCellsWithoutLegacySceneAndPreserveBaseIdentity()
        {
            ProductionWorldStreamingManifest manifest = CreateManifest();
            try
            {
                Assert.That(manifest.CellLayers, Is.Empty);
                Assert.That(manifest.ValidateConfiguration(), Is.Empty);
                manifest.ConfigureCellLayersForAuthoring(new[]
                {
                    new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(-7, 4),
                        2, "Assets/Forest.unity", 2, 3)
                });

                Assert.That(manifest.ValidateConfiguration(), Is.Empty);
                Assert.That(manifest.Cells.Count, Is.EqualTo(1));
                Assert.That(manifest.Cells[0].CellId, Is.EqualTo("cell_0_0"));
                Assert.That(manifest.TryGetCell("cell_-7_4", out _), Is.False,
                    "Presentation-only cells must not silently create gameplay/save identities.");
                Assert.That(manifest.SchemaVersion, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void LayerDuplicateIdentityOrReuseOfBaseSceneAddressIsRejected()
        {
            ProductionWorldStreamingManifest manifest = CreateManifest();
            try
            {
                manifest.ConfigureCellLayersForAuthoring(new[]
                {
                    new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(0, 0),
                        1, "Assets/Base.unity")
                });
                Assert.That(manifest.ValidateConfiguration(), Is.Not.Empty);
                manifest.ConfigureCellLayersForAuthoring(new[]
                {
                    new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(1, 0),
                        2, "Assets/Forest.unity"),
                    new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(1, 0),
                        3, "Assets/ForestDuplicate.unity")
                });
                Assert.That(manifest.ValidateConfiguration(), Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void LayerRadiusPreservesVehiclePreloadAndCannotUnloadInsideItsLoadRadius()
        {
            var layer = new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(1, 0),
                2, "Assets/Forest.unity", 3, 1);
            Assert.That(layer.GetLoadingRadius(1), Is.EqualTo(3));
            Assert.That(layer.GetLoadingRadius(4), Is.EqualTo(4));
            Assert.That(layer.GetUnloadingRadius(2), Is.EqualTo(3));
            Assert.That(layer.GetUnloadingRadius(5), Is.EqualTo(5));
        }

        [Test]
        public void DeferredInitialLoadIsAdditiveAndDefaultsOff()
        {
            var existingLayer = new ProductionWorldCellLayerScene(
                "vegetation", new WorldCellIndex(0, 0), 1,
                "Assets/NearForest.unity");
            var backdropLayer = new ProductionWorldCellLayerScene(
                "vegetation-backdrop", new WorldCellIndex(0, 0), 2,
                "Assets/FarForest.unity", 2, 3,
                deferInitialLoad: true);

            Assert.That(existingLayer.DeferInitialLoad, Is.False,
                "Existing serialized layers must preserve their startup behavior.");
            Assert.That(backdropLayer.DeferInitialLoad, Is.True);
        }

        private static ProductionWorldStreamingManifest CreateManifest()
        {
            var manifest = ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
            manifest.ConfigureForAuthoring(512f, 0, 1, new[]
            {
                new ProductionWorldCellScene("cell_0_0", 0, 0, 1, "Assets/Base.unity")
            });
            return manifest;
        }
    }
}
