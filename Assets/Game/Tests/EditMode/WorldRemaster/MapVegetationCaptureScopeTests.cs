using System;
using System.Collections.Generic;
using MSC.Editor.Vegetation;
using MSC.World.Partition;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationCaptureScopeTests
    {
        private readonly List<Object> owned = new List<Object>();
        private readonly WorldCellIndex cell = new WorldCellIndex(17, -9);

        [TearDown]
        public void TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void GrassRegenerationUsesItsNewFingerprintWithoutChangingPreservedTrees()
        {
            GameObject trees = Root(MapVegetationCategories.OriginalTrees, "old-trees", 2);
            GameObject boundary = Root(MapVegetationCategories.BoundaryForest, "old-boundary", 3);
            GameObject shrubs = Root(MapVegetationCategories.ShrubsAndUndergrowth, "old-shrubs", 1);
            GameObject grass = Root(MapVegetationCategories.GrassCoverage, "new-grass", 4);
            var scope = MapVegetationCaptureScope.Evaluate(MapVegetationCategories.GrassCoverage,
                cell.Id, new[] { trees, boundary, shrubs, grass });
            Assert.That(scope.IsValid, Is.True, string.Join("; ", scope.Errors));
            Assert.That(scope.Fingerprint, Is.EqualTo("new-grass"));
            Assert.That(scope.PreservedCategoryFingerprints, Has.Count.EqualTo(3));
            Assert.That(scope.RequiredViewCount, Is.EqualTo(4), "Preserved boundary scenery must not require a new boundary acceptance view.");
            Assert.That(trees.GetComponent<GeneratedVegetationGroup>().Fingerprint, Is.EqualTo("old-trees"));
            Assert.That(boundary.transform.childCount, Is.EqualTo(3));
            Assert.That(grass.GetComponent<VegetationWorldRenderer>().enabled, Is.True);
        }

        [TestCase(MapVegetationCategories.GrassCoverage, 4)]
        [TestCase(MapVegetationCategories.OriginalTrees, 4)]
        [TestCase(MapVegetationCategories.ShrubsAndUndergrowth, 4)]
        [TestCase(MapVegetationCategories.BoundaryForest, 5)]
        public void SingleCategoryPilotRequiresOnlyItsOwnContent(MapVegetationCategories category, int expectedViews)
        {
            var scope = MapVegetationCaptureScope.Evaluate(category, cell.Id, new[] { Root(category, "current", 2) });
            Assert.That(scope.IsValid, Is.True, string.Join("; ", scope.Errors));
            Assert.That(scope.RequiredViewCount, Is.EqualTo(expectedViews));
        }

        [TestCase(MapVegetationCategories.GrassCoverage)]
        [TestCase(MapVegetationCategories.OriginalTrees)]
        [TestCase(MapVegetationCategories.ShrubsAndUndergrowth)]
        [TestCase(MapVegetationCategories.BoundaryForest)]
        public void EmptySelectedCategoryCannotBorrowEvidenceFromPreservedContent(MapVegetationCategories category)
        {
            MapVegetationCategories preserved = category == MapVegetationCategories.GrassCoverage
                ? MapVegetationCategories.OriginalTrees : MapVegetationCategories.GrassCoverage;
            var scope = MapVegetationCaptureScope.Evaluate(category, cell.Id,
                new[] { Root(preserved, "preserved", 3), Root(category, "current", 0) });
            Assert.That(scope.IsValid, Is.False);
            Assert.That(scope.Errors, Has.Some.Contains(category.ToString()));
        }

        [Test]
        public void SelectedCategoriesWithDifferentFingerprintsFailEvenWhenBothContainInstances()
        {
            var scope = MapVegetationCaptureScope.Evaluate(MapVegetationCategories.OriginalTrees | MapVegetationCategories.GrassCoverage,
                cell.Id, new[] { Root(MapVegetationCategories.OriginalTrees, "new", 2), Root(MapVegetationCategories.GrassCoverage, "stale", 4) });
            Assert.That(scope.IsValid, Is.False);
        }

        [Test]
        public void MissingDuplicateOrForeignCellSelectedRootsCannotPass()
        {
            GameObject grass = Root(MapVegetationCategories.GrassCoverage, "current", 2);
            Assert.That(MapVegetationCaptureScope.Evaluate(MapVegetationCategories.OriginalTrees, cell.Id, new[] { grass }).IsValid, Is.False);
            Assert.That(MapVegetationCaptureScope.Evaluate(MapVegetationCategories.GrassCoverage, cell.Id,
                new[] { grass, Root(MapVegetationCategories.GrassCoverage, "current", 2) }).IsValid, Is.False);
            grass.GetComponent<GeneratedVegetationGroup>().Configure(MapVegetationRebuildOptions.GeneratorId,
                "cell_999_999", MapVegetationCategories.GrassCoverage.ToString(), "current");
            Assert.That(MapVegetationCaptureScope.Evaluate(MapVegetationCategories.GrassCoverage, cell.Id, new[] { grass }).IsValid, Is.False);
        }

        [Test]
        public void ExplicitCellContextAllowsEmptyBoundaryButDoesNotClaimARepresentativeAllCategoryPilot()
        {
            var roots = new[]
            {
                Root(MapVegetationCategories.OriginalTrees, "current", 1),
                Root(MapVegetationCategories.BoundaryForest, "current", 0),
                Root(MapVegetationCategories.ShrubsAndUndergrowth, "current", 0),
                Root(MapVegetationCategories.GrassCoverage, "current", 2)
            };
            var context = MapVegetationCaptureScope.Evaluate(MapVegetationCategories.All, cell.Id, roots, requireRepresentative: false);
            Assert.That(context.IsValid, Is.True, string.Join("; ", context.Errors));
            Assert.That(context.RequiredViewCount, Is.EqualTo(4));
            Assert.That(MapVegetationCaptureScope.Evaluate(MapVegetationCategories.All, cell.Id, roots).IsValid, Is.False);
        }

        [Test]
        public void GrassAuditSelectionTargetsDensestDeterministicNeighbourhood()
        {
            var points = new List<Vector3>
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(0.5f, 1f, 0.5f)
            };
            for (int z = -2; z <= 2; z++)
            for (int x = -2; x <= 2; x++)
                points.Add(new Vector3(40f + x * 0.5f, 3f,
                    40f + z * 0.5f));

            MapVegetationVisualAudit.GrassAuditSelection forward =
                MapVegetationVisualAudit.SelectDenseGrassAuditPoint(points,
                    Vector3.zero, new Vector3(99f, 7f, 99f), 3f);
            points.Reverse();
            MapVegetationVisualAudit.GrassAuditSelection reverse =
                MapVegetationVisualAudit.SelectDenseGrassAuditPoint(points,
                    Vector3.zero, new Vector3(99f, 7f, 99f), 3f);

            Assert.That(forward.Position.x, Is.GreaterThan(30f),
                "The nearest isolated record must not pose as a carpet view.");
            Assert.That(forward.NearbyInstances, Is.EqualTo(25));
            Assert.That(forward.Position, Is.EqualTo(reverse.Position));
            Assert.That(forward.NearbyInstances,
                Is.EqualTo(reverse.NearbyInstances));
            Assert.That(forward.LookDirection,
                Is.EqualTo(reverse.LookDirection));
            Assert.That(forward.LookDirection.sqrMagnitude,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(forward.ForwardInstances, Is.GreaterThan(0));
            Assert.That(forward.ForwardInstances,
                Is.EqualTo(reverse.ForwardInstances));
            Assert.That(forward.NearestForwardInstanceMeters,
                Is.InRange(0.01f, 0.8f));
            Assert.That(forward.NearestForwardInstanceMeters,
                Is.EqualTo(reverse.NearestForwardInstanceMeters)
                    .Within(0.0001f));
        }

        [Test]
        public void GrassAuditSelectionEmptyInputUsesExplicitFallback()
        {
            Vector3 fallback = new Vector3(8f, 4f, -3f);
            MapVegetationVisualAudit.GrassAuditSelection selection =
                MapVegetationVisualAudit.SelectDenseGrassAuditPoint(
                    Array.Empty<Vector3>(), Vector3.zero, fallback);

            Assert.That(selection.Position, Is.EqualTo(fallback));
            Assert.That(selection.NearbyInstances, Is.Zero);
            Assert.That(selection.LookDirection, Is.EqualTo(Vector3.forward));
            Assert.That(selection.ForwardInstances, Is.Zero);
            Assert.That(selection.NearestForwardInstanceMeters,
                Is.EqualTo(-1f));
            Assert.That(MapVegetationVisualAudit
                .GrassAuditMinimumNearbyInstances, Is.GreaterThan(0));
        }

        private GameObject Root(MapVegetationCategories category, string fingerprint, int count)
        {
            var root = new GameObject(category.ToString()) { hideFlags = HideFlags.HideAndDontSave };
            owned.Add(root);
            root.AddComponent<GeneratedVegetationGroup>().Configure(MapVegetationRebuildOptions.GeneratorId, cell.Id, category.ToString(), fingerprint);
            if (category != MapVegetationCategories.GrassCoverage)
            {
                for (int i = 0; i < count; i++) new GameObject("SavedPlant_" + i).transform.SetParent(root.transform, false);
                return root;
            }
            var data = ScriptableObject.CreateInstance<VegetationCellAsset>();
            var catalog = ScriptableObject.CreateInstance<VegetationCellCatalog>();
            data.hideFlags = catalog.hideFlags = HideFlags.HideAndDontSave;
            owned.Add(data); owned.Add(catalog);
            var records = new VegetationInstanceRecord[count];
            for (int i = 0; i < count; i++) records[i] = new VegetationInstanceRecord(new Vector3(i, 0f, 0f), Vector3.up, 0f, 0.3f, 0, 0, 0);
            data.ConfigureForAuthoring(cell.Id, cell, new Bounds(Vector3.zero, Vector3.one * 512f), 16, 32f, null);
            data.ReplaceTileForAuthoring(new VegetationTileRecord(0, 0, data.WorldBounds,
                new[] { new VegetationProfileTileInstances(0, records) }, Array.Empty<Vector3>(), Array.Empty<VegetationRejectedSample>()));
            catalog.ConfigureForAuthoring(new[] { data }, Array.Empty<VegetationProfile>(), ~0, 2048f, 4096f);
            root.AddComponent<VegetationWorldRenderer>().ConfigureForAuthoring(catalog);
            return root;
        }
    }
}
