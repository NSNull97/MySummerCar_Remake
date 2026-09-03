using System.Collections.Generic;
using System.Linq;
using MSC.Editor.Vegetation;
using MSC.LegacyImport;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldBaseline
{
    public sealed class MapVegetationDonorSourceEditModeTests
    {
        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void CrossedCards_RecoverIntersectionInsteadOfAveragingOffsetCardCenters()
        {
            GameObject root = MakeSource("source-a", "MAP/MESH/FOLIAGE/TREES1", new[]
            {
                new Vector3(-4f, 0f, 0f), new Vector3(6f, 0f, 0f),
                new Vector3(-4f, 10f, 0f), new Vector3(6f, 10f, 0f),
                new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 4f),
                new Vector3(0f, 10f, -6f), new Vector3(0f, 10f, 4f)
            });
            MapVegetationSourceSnapshot snapshot = MapVegetationDonorSource.Read(new[] { root });
            Assert.That(snapshot.Trees.Count, Is.EqualTo(1));
            Assert.That(snapshot.Trees[0].Position, Is.EqualTo(Vector3.zero));
            Assert.That(snapshot.Trees[0].Method, Is.EqualTo("CrossedCardRootReconstructed"));
            Assert.That(snapshot.PairedCardCount, Is.EqualTo(2));
        }

        [Test]
        public void PairAcrossMeshSplit_KeepsMappedCoordinatesAndStableOrder()
        {
            GameObject a = MakeSource("source-a", "MAP/MESH/FOLIAGE/TREES_SMALL1", CardX());
            GameObject b = MakeSource("source-b", "MAP/MESH/FOLIAGE/TREES_SMALL2", CardZ());
            a.transform.position = b.transform.position = new Vector3(-513f, 12f, -1f);
            MapVegetationSourceSnapshot first = MapVegetationDonorSource.Read(new[] { a, b });
            MapVegetationSourceSnapshot second = MapVegetationDonorSource.Read(new[] { b, a, a });
            Assert.That(first.PositionsAreProjectSpace, Is.True);
            Assert.That(first.Trees.Count, Is.EqualTo(1));
            Assert.That(first.Trees[0].Position, Is.EqualTo(new Vector3(-513f, 12f, -1f)));
            Assert.That(first.Trees[0].CellId, Is.EqualTo("cell_-2_-1"));
            Assert.That(second.Trees.Count, Is.EqualTo(1));
            Assert.That(second.Trees[0].Id, Is.EqualTo(first.Trees[0].Id));
        }

        [Test]
        public void ParallelCards_DoNotMergeNearbyIndependentTreeEvidence()
        {
            GameObject a = MakeSource("a", "MAP/MESH/FOLIAGE/TREES1", CardX());
            GameObject b = MakeSource("b", "MAP/MESH/FOLIAGE/TREES2", CardX());
            b.transform.position = new Vector3(0f, 0f, 0.1f);
            MapVegetationSourceSnapshot snapshot = MapVegetationDonorSource.Read(new[] { a, b });
            Assert.That(snapshot.Trees.Count, Is.EqualTo(2));
            Assert.That(snapshot.PairedCardCount, Is.Zero);
            Assert.That(snapshot.UnpairedCardCount, Is.EqualTo(2));
            Assert.That(snapshot.Diagnostics.Count, Is.EqualTo(2));
        }

        [Test]
        public void CrossingCardsWithDifferentHeights_AreNotOneDonorTree()
        {
            GameObject a = MakeSource("a", "MAP/MESH/FOLIAGE/TREES1", CardX());
            GameObject b = MakeSource("b", "MAP/MESH/FOLIAGE/TREES2", CardZ());
            b.transform.localScale = new Vector3(1f, 2f, 1f);
            MapVegetationSourceSnapshot snapshot = MapVegetationDonorSource.Read(new[] { a, b });
            Assert.That(snapshot.Trees.Count, Is.EqualTo(2));
            Assert.That(snapshot.PairedCardCount, Is.Zero);
        }

        [Test]
        public void SlopedCardBases_UseEdgeIntersectionInsteadOfLowestCorner()
        {
            Vector3[] x = CardX();
            Vector3[] z = CardZ();
            for (int i = 0; i < x.Length; i++) x[i].y += x[i].x * 0.1f;
            for (int i = 0; i < z.Length; i++) z[i].y += z[i].z * 0.15f;
            GameObject a = MakeSource("a", "MAP/MESH/FOLIAGE/TREES1", x);
            GameObject b = MakeSource("b", "MAP/MESH/FOLIAGE/TREES2", z);
            MapVegetationSourceSnapshot snapshot = MapVegetationDonorSource.Read(new[] { a, b });
            Assert.That(snapshot.Trees.Count, Is.EqualTo(1));
            Assert.That(snapshot.Trees[0].Position, Is.EqualTo(Vector3.zero));
            Assert.That(snapshot.Trees[0].Height, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void BoundaryMesh_UsesBottomEdgeWithoutAddingTreeTransforms()
        {
            GameObject root = MakeSource("wall", "MAP/MESH/FOLIAGE/TREEWALL_LOW", CardX());
            MapVegetationSourceSnapshot snapshot = MapVegetationDonorSource.Read(new[] { root });
            Assert.That(snapshot.Trees.Count, Is.Zero);
            Assert.That(snapshot.BoundarySegments.Count, Is.EqualTo(1));
            MapVegetationBoundarySegment segment = snapshot.BoundarySegments[0];
            Assert.That(segment.A.y, Is.Zero);
            Assert.That(segment.B.y, Is.Zero);
            Assert.That(Vector3.Distance(segment.A, segment.B), Is.EqualTo(4f));
        }

        [Test]
        public void CanonicalRocks_EmitOneMeasuredAnchorPerDisconnectedBoulder()
        {
            GameObject root = MakeRockSource("rocks", "MAP/MESH/ROCKS",
                TwoRockBoxes());
            MapVegetationSourceSnapshot snapshot =
                MapVegetationDonorSource.Read(new[] { root });

            Assert.That(snapshot.Rocks.Count, Is.EqualTo(2));
            Assert.That(snapshot.Rocks[0].Position.x,
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(snapshot.Rocks[1].Position.x,
                Is.EqualTo(8f).Within(0.0001f));
            Assert.That(snapshot.Rocks.All(rock => rock.ReplacementKey ==
                "legacy-world:rocks"), Is.True);
            Assert.That(snapshot.Rocks.All(rock => rock.Method ==
                "CanonicalRockGeometryComponent"), Is.True);
            Assert.That(snapshot.Rocks[0].SourceSize,
                Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(snapshot.Rocks[1].SourceSize,
                Is.EqualTo(new Vector3(4f, 3f, 2f)));
        }

        [TestCase("MAP/MESH/RockPale")]
        [TestCase("MAP/MESH/Rockwall")]
        public void ContinuousRockSurfaces_RemainLegacyAndProduceNoAlpAnchors(
            string path)
        {
            GameObject root = MakeRockSource("geology", path,
                TwoRockBoxes());
            MapVegetationSourceSnapshot snapshot =
                MapVegetationDonorSource.Read(new[] { root });

            Assert.That(snapshot.Rocks, Is.Empty);
            Assert.That(snapshot.Diagnostics.Any(diagnostic =>
                diagnostic.Code == "ContinuousRockSurfacePreserved"), Is.True);
        }

        [Test]
        public void SourceFingerprint_IsOrderIndependentAndDetectsSourceAndGroundChanges()
        {
            var snapshot = new MapVegetationSourceSnapshot();
            var tree = new MapVegetationSourcePoint { Id = "a", Position = new Vector3(1f, 2f, 3f), Height = 5f, SourceHash = "mesh-a" };
            snapshot.Trees.Add(tree);
            snapshot.Trees.Add(new MapVegetationSourcePoint { Id = "b", Position = new Vector3(4f, 5f, 6f), Height = 8f, SourceHash = "mesh-b" });
            var segment = new MapVegetationBoundarySegment { Id = "wall", A = Vector3.zero, B = Vector3.right * 10f };
            snapshot.BoundarySegments.Add(segment);
            var rock = new MapVegetationRockAnchor
            {
                Id = "rock", Position = new Vector3(7f, 1f, 2f),
                SourceSize = new Vector3(2f, 1f, 3f), Yaw = 12f,
                ReplacementKey = "legacy-world:rocks"
            };
            snapshot.Rocks.Add(rock);
            string original = MapVegetationContext.ComputeSourceFingerprint(snapshot, "ground-a");
            snapshot.Trees.Reverse();
            Assert.That(MapVegetationContext.ComputeSourceFingerprint(snapshot, "ground-a"), Is.EqualTo(original));
            Assert.That(MapVegetationContext.ComputeSourceFingerprint(snapshot, "ground-b"), Is.Not.EqualTo(original));
            tree.Height += 1f;
            Assert.That(MapVegetationContext.ComputeSourceFingerprint(snapshot, "ground-a"), Is.Not.EqualTo(original));
            tree.Height -= 1f;
            tree.SourceHash = "mesh-edited";
            Assert.That(MapVegetationContext.ComputeSourceFingerprint(snapshot, "ground-a"), Is.Not.EqualTo(original));
            tree.SourceHash = "mesh-a";
            segment.B += Vector3.forward;
            Assert.That(MapVegetationContext.ComputeSourceFingerprint(snapshot, "ground-a"), Is.Not.EqualTo(original));
            segment.B -= Vector3.forward;
            rock.Yaw += 1f;
            Assert.That(MapVegetationContext.ComputeSourceFingerprint(snapshot,
                "ground-a"), Is.Not.EqualTo(original));
        }

        [Test]
        public void UnmarkedGeneratedForest_IsNotASecondDonorSource()
        {
            GameObject root = new GameObject("PHASE1_VEGETATION_REPLACEMENT_LICENSED_THIRD_PARTY");
            owned.Add(root);
            root.AddComponent<MeshFilter>().sharedMesh = MakeMesh(CardX());
            MapVegetationSourceSnapshot snapshot = MapVegetationDonorSource.Read(new[] { root });
            Assert.That(snapshot.Trees, Is.Empty);
            Assert.That(snapshot.SourceMeshCount, Is.Zero);
        }

        private GameObject MakeSource(string id, string path, Vector3[] vertices)
        {
            var root = new GameObject(id);
            owned.Add(root);
            root.AddComponent<DonorWorldBaselineEntityMetadata>().Configure(
                id, 1, "", path, "test-mesh", "global", "VegetationTree", "4;23;33",
                "TemporaryDirectImport", "test fixture", true, true, true);
            root.AddComponent<MeshFilter>().sharedMesh = MakeMesh(vertices);
            return root;
        }

        private GameObject MakeRockSource(string id, string path,
            RockMeshData geometry)
        {
            var root = new GameObject(id);
            owned.Add(root);
            root.AddComponent<DonorWorldBaselineEntityMetadata>().Configure(
                id, 1, "", path, "test-rock-mesh", "global", "Rock",
                "4;23;33;64", "TemporaryDirectImport", "test fixture",
                true, true, true);
            var mesh = new Mesh
            {
                name = "rock-source-fixture",
                vertices = geometry.Vertices,
                triangles = geometry.Triangles
            };
            mesh.RecalculateBounds();
            owned.Add(mesh);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            return root;
        }

        private Mesh MakeMesh(Vector3[] vertices)
        {
            var mesh = new Mesh { name = "source-fixture", vertices = vertices };
            owned.Add(mesh);
            var triangles = new List<int>();
            for (int i = 0; i < vertices.Length; i += 4)
                triangles.AddRange(new[] { i, i + 1, i + 2, i + 1, i + 3, i + 2 });
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3[] CardX() => new[]
        {
            new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
            new Vector3(-2f, 5f, 0f), new Vector3(2f, 5f, 0f)
        };

        private static Vector3[] CardZ() => new[]
        {
            new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, 2f),
            new Vector3(0f, 5f, -2f), new Vector3(0f, 5f, 2f)
        };

        private static RockMeshData TwoRockBoxes()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddBox(vertices, triangles, new Vector3(0f, 1f, 0f),
                new Vector3(2f, 2f, 2f));
            AddBox(vertices, triangles, new Vector3(8f, 1.5f, 0f),
                new Vector3(4f, 3f, 2f));
            return new RockMeshData(vertices.ToArray(), triangles.ToArray());
        }

        private static void AddBox(List<Vector3> vertices,
            List<int> triangles, Vector3 center, Vector3 size)
        {
            int first = vertices.Count;
            Vector3 half = size * 0.5f;
            vertices.AddRange(new[]
            {
                center + new Vector3(-half.x, -half.y, -half.z),
                center + new Vector3( half.x, -half.y, -half.z),
                center + new Vector3( half.x, -half.y,  half.z),
                center + new Vector3(-half.x, -half.y,  half.z),
                center + new Vector3(-half.x,  half.y, -half.z),
                center + new Vector3( half.x,  half.y, -half.z),
                center + new Vector3( half.x,  half.y,  half.z),
                center + new Vector3(-half.x,  half.y,  half.z)
            });
            int[] local =
            {
                0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7
            };
            foreach (int index in local) triangles.Add(first + index);
        }

        private readonly struct RockMeshData
        {
            public readonly Vector3[] Vertices;
            public readonly int[] Triangles;
            public RockMeshData(Vector3[] vertices, int[] triangles)
            {
                Vertices = vertices;
                Triangles = triangles;
            }
        }
    }
}
