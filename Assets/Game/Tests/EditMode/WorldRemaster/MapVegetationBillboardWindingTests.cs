using System.IO;
using MSC.Editor.Vegetation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationBillboardWindingTests
    {
        [Test]
        public void SavedSharedBillboard_All42FacesAgreeWithAuthoredNormals()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MapVegetationBillboardWindingRepair.MeshPath);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.EqualTo(84));
            Assert.That(AssetDatabase.AssetPathToGUID(MapVegetationBillboardWindingRepair.MeshPath),
                Is.EqualTo(MapVegetationBillboardWindingRepair.MeshGuid));
            AssertFacesAligned(mesh);
        }

        [Test]
        public void Repair_IsIdempotentAndPreservesEveryVertexAttributeAndBounds()
        {
            Mesh mesh = CloneReversedBillboard();
            try
            {
                string attributes = MapVegetationBillboardWindingRepair.AttributeHash(mesh);
                Bounds bounds = mesh.bounds;
                Assert.That(MapVegetationBillboardWindingRepair.CorrectReversedWinding(mesh), Is.True);
                AssertFacesAligned(mesh);
                Assert.That(MapVegetationBillboardWindingRepair.AttributeHash(mesh), Is.EqualTo(attributes));
                Assert.That(mesh.bounds, Is.EqualTo(bounds));
                int[] indices = mesh.triangles;
                Assert.That(MapVegetationBillboardWindingRepair.CorrectReversedWinding(mesh), Is.False);
                Assert.That(mesh.triangles, Is.EqualTo(indices));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Repair_RejectsMixedWindingBeforeChangingMesh()
        {
            Mesh mesh = CloneReversedBillboard();
            try
            {
                int[] indices = mesh.triangles;
                (indices[1], indices[2]) = (indices[2], indices[1]);
                mesh.SetIndices(indices, UnityEngine.MeshTopology.Triangles, 0, false);
                string attributes = MapVegetationBillboardWindingRepair.AttributeHash(mesh);
                Assert.Throws<InvalidDataException>(() => MapVegetationBillboardWindingRepair.CorrectReversedWinding(mesh));
                Assert.That(mesh.triangles, Is.EqualTo(indices));
                Assert.That(MapVegetationBillboardWindingRepair.AttributeHash(mesh), Is.EqualTo(attributes));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        private static Mesh CloneReversedBillboard()
        {
            Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(MapVegetationBillboardWindingRepair.MeshPath);
            Assert.That(source, Is.Not.Null);
            Mesh mesh = Object.Instantiate(source);
            int[] indices = mesh.triangles;
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            for (int index = 0; index < indices.Length; index += 3)
            {
                Vector3 face = Vector3.Cross(vertices[indices[index + 1]] - vertices[indices[index]],
                    vertices[indices[index + 2]] - vertices[indices[index]]);
                if (Vector3.Dot(face, normals[indices[index]]) > 0)
                    (indices[index + 1], indices[index + 2]) = (indices[index + 2], indices[index + 1]);
            }
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            return mesh;
        }

        private static void AssertFacesAligned(Mesh mesh)
        {
            int[] indices = mesh.triangles;
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            Assert.That(indices.Length, Is.EqualTo(126));
            for (int index = 0; index < indices.Length; index += 3)
            {
                Vector3 face = Vector3.Cross(vertices[indices[index + 1]] - vertices[indices[index]],
                    vertices[indices[index + 2]] - vertices[indices[index]]);
                Vector3 normal = normals[indices[index]] + normals[indices[index + 1]] + normals[indices[index + 2]];
                Assert.That(Vector3.Dot(face.normalized, normal.normalized), Is.GreaterThan(0.999f), "triangle " + index / 3);
            }
        }
    }
}
