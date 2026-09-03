using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Editor.Vegetation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationTreePresentationTests
    {
        [Test]
        public void AuthoredTrunkExtraction_KeepsOnlyActualMainComponentAndVertexAttributes()
        {
            Mesh source = MakeBark(false);
            Mesh result = null;
            try
            {
                Vector3[] sourceVertices = source.vertices;
                Vector2[] sourceUvs = source.uv;
                source.UploadMeshData(true); // The production FBX is deliberately non-readable.
                result = MapVegetationTreePresentation.ExtractAuthoredTrunk(source, Matrix4x4.identity, out var evidence);
                Assert.That(evidence.connectedComponents, Is.EqualTo(2));
                Assert.That(evidence.sourceTriangles, Is.EqualTo(24));
                Assert.That(evidence.outputTriangles, Is.EqualTo(12));
                Assert.That(result.bounds.size.y, Is.EqualTo(10f).Within(0.00001f));
                foreach (Vector3 vertex in result.vertices) Assert.That(sourceVertices, Does.Contain(vertex));
                Assert.That(result.uv, Is.EquivalentTo(sourceUvs.Take(8)));
                Assert.That(result.normals.All(n => n == Vector3.up), Is.True);
                Assert.That(result.tangents.All(t => t == new Vector4(1, 0, 0, 1)), Is.True);
            }
            finally
            {
                if (result != null) UnityEngine.Object.DestroyImmediate(result);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void AuthoredTrunkExtraction_RejectsTwoEquallyPlausibleStems()
        {
            Mesh source = MakeBark(true);
            try
            {
                Assert.Throws<System.IO.InvalidDataException>(() =>
                    MapVegetationTreePresentation.ExtractAuthoredTrunk(source, Matrix4x4.identity, out _));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void AuthoredTrunkExtraction_DetachedBranchesOutsideStemDoNotReplaceTheHeightReference()
        {
            Mesh source = MakeBark(false);
            Mesh result = null;
            try
            {
                Vector3[] vertices = source.vertices;
                for (int i = 8; i < vertices.Length; i++) vertices[i] += Vector3.up * 8f;
                source.vertices = vertices; source.RecalculateBounds();
                result = MapVegetationTreePresentation.ExtractAuthoredTrunk(source, Matrix4x4.identity, out var evidence);
                Assert.That(evidence.selectedHeightFractionOfBark, Is.LessThan(0.8f));
                Assert.That(evidence.maximumComponentHeight, Is.EqualTo(10f));
                Assert.That(evidence.outputTriangles, Is.EqualTo(12));
                Assert.That(result.vertices, Is.EquivalentTo(vertices.Take(8)));
            }
            finally
            {
                if (result != null) UnityEngine.Object.DestroyImmediate(result);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void AuthoredTrunkExtraction_IncludesAuthoredRootAxisForSelectionButKeepsMeshSpaceVertices()
        {
            var root = new GameObject("Authored axis fixture");
            Mesh source = MakeBark(false);
            Mesh result = null;
            try
            {
                root.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
                var child = new GameObject("Bark"); child.transform.SetParent(root.transform, false);
                Renderer bark = child.AddComponent<MeshRenderer>();
                Quaternion intoMeshSpace = Quaternion.Euler(90f, 0, 0);
                Vector3[] vertices = source.vertices.Select(p => intoMeshSpace * p).ToArray();
                source.vertices = vertices; source.RecalculateBounds();
                Matrix4x4 selection = MapVegetationTreePresentation.TrunkSelectionMatrix(root, bark);
                result = MapVegetationTreePresentation.ExtractAuthoredTrunk(source, selection, out var evidence);
                Assert.That(evidence.selectedBoundsInTree.size.y, Is.EqualTo(10f).Within(0.00001f));
                Assert.That(result.bounds.size.z, Is.EqualTo(10f).Within(0.00001f));
                Assert.That(result.vertices, Is.EquivalentTo(vertices.Take(8)));
            }
            finally
            {
                if (result != null) UnityEngine.Object.DestroyImmediate(result);
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ChernobylLods_PreserveRendererSetsRootAndCollision(int count)
        {
            var root = new GameObject("TreePresentationFixture");
            try
            {
                root.transform.position = new Vector3(31, 7, -13);
                root.transform.rotation = Quaternion.Euler(1, 63, 2);
                root.transform.localScale = Vector3.one * 0.75f;
                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.height = 8; collider.radius = 0.2f; collider.center = Vector3.up * 4;
                LODGroup group = root.AddComponent<LODGroup>();
                var lods = new LOD[count];
                for (int i = 0; i < count; i++)
                {
                    var child = new GameObject("AuthoredMeshLOD" + i);
                    child.transform.SetParent(root.transform, false);
                    lods[i] = new LOD(0.8f / (i + 1), new[] { child.AddComponent<MeshRenderer>() });
                }
                group.SetLODs(lods);
                Matrix4x4 before = root.transform.localToWorldMatrix;
                MapVegetationTreePresentation.ConfigureLods(group, false);
                LOD[] actual = group.GetLODs();
                for (int i = 0; i < count; i++) Assert.That(actual[i].renderers, Is.EqualTo(lods[i].renderers));
                Assert.That(actual[0].screenRelativeTransitionHeight, Is.EqualTo(0.20f));
                Assert.That(root.transform.localToWorldMatrix, Is.EqualTo(before));
                Assert.That(collider.height, Is.EqualTo(8));
                Assert.That(collider.radius, Is.EqualTo(0.2f));
                Assert.That(collider.center, Is.EqualTo(Vector3.up * 4));
                Assert.That(collider.enabled, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(4, 0.22f, 0.004f)]
        [TestCase(5, 0.22f, 0.0025f)]
        public void AlpSpruceLods_KeepRealNearGeometryAndReachReviewedFarThreshold(
            int count, float expectedNear, float expectedFar)
        {
            float[] thresholds = MapVegetationAlpSpruceBindings.Thresholds(count);
            Assert.That(thresholds, Has.Length.EqualTo(count));
            Assert.That(thresholds[0], Is.EqualTo(expectedNear));
            Assert.That(thresholds[^1], Is.EqualTo(expectedFar));
            for (int index = 1; index < thresholds.Length; index++)
                Assert.That(thresholds[index], Is.LessThan(thresholds[index - 1]));
        }

        [Test]
        public void MaturePineSelection_DoesNotEnlargeSaplingVariants()
        {
            for (int i = 0; i < 40; i++)
            {
                GameObject prefab = MapVegetationTreePresentation.SelectPrefab("Pine", "fixture:" + i, 20260831, 20f);
                Assert.That(prefab.name, Is.Not.EqualTo("Pine_03"));
                Assert.That(prefab.name, Is.Not.EqualTo("Pine_05"));
                Assert.That(MapVegetationTreePresentation.GetSpecies(prefab), Is.EqualTo("Pine"));
            }
        }

        private static Mesh MakeBark(bool ambiguous)
        {
            var vertices = new List<Vector3>(); var indices = new List<int>();
            AddBox(vertices, indices, Vector3.zero, new Vector3(1, 10, 1));
            AddBox(vertices, indices, new Vector3(5, 0, 0), ambiguous ? new Vector3(1, 10, 1) : new Vector3(6, 1, 1));
            var mesh = new Mesh { name = "Synthetic separate authored stem and branch components" };
            mesh.vertices = vertices.ToArray();
            mesh.triangles = indices.ToArray();
            mesh.normals = Enumerable.Repeat(Vector3.up, vertices.Count).ToArray();
            mesh.tangents = Enumerable.Repeat(new Vector4(1, 0, 0, 1), vertices.Count).ToArray();
            mesh.uv = Enumerable.Range(0, vertices.Count).Select(i => new Vector2(i * 0.03f, i * 0.07f)).ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBox(List<Vector3> vertices, List<int> indices, Vector3 center, Vector3 size)
        {
            int offset = vertices.Count;
            for (int z = 0; z < 2; z++) for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
                vertices.Add(center + Vector3.Scale(new Vector3(x - 0.5f, y - 0.5f, z - 0.5f), size));
            int[] triangles = { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4,
                2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5 };
            indices.AddRange(triangles.Select(i => i + offset));
        }
    }
}
