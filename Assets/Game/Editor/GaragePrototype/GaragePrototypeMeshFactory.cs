using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Editor.GaragePrototype
{
    internal static class GaragePrototypeMeshFactory
    {
        internal static void BuildUnitBox(Mesh mesh)
        {
            var vertices = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var triangles = new List<int>(36);
            AddQuad(vertices, uv, triangles,
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f));
            AddQuad(vertices, uv, triangles,
                new Vector3(0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f));
            AddQuad(vertices, uv, triangles,
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, 0.5f));
            AddQuad(vertices, uv, triangles,
                new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, -0.5f));
            AddQuad(vertices, uv, triangles,
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f));
            AddQuad(vertices, uv, triangles,
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f));
            Apply(mesh, vertices, triangles, uv);
        }

        internal static void BuildMappedGarageRoof(Mesh mesh)
        {
            Vector3 min = GaragePrototypePaths.ProductionRoofBoundsMin;
            Vector3 max = GaragePrototypePaths.ProductionRoofBoundsMax;
            float middleZ = (min.z + max.z) * 0.5f;
            var vertices = new List<Vector3>
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, middleZ),
                new Vector3(max.x, max.y, middleZ),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z)
            };
            var triangles = new List<int>
            {
                0, 2, 3, 0, 3, 1,
                2, 4, 5, 2, 5, 3,
                0, 4, 2,
                1, 3, 5,
                0, 1, 5, 0, 5, 4
            };
            var uv = new List<Vector2>(vertices.Count);
            foreach (Vector3 vertex in vertices)
            {
                uv.Add(new Vector2(
                    Mathf.InverseLerp(min.x, max.x, vertex.x),
                    Mathf.InverseLerp(min.z, max.z, vertex.z)));
            }

            Apply(mesh, vertices, triangles, uv);
            mesh.bounds = new Bounds(
                (min + max) * 0.5f,
                max - min);
        }

        internal static void BuildBoundsBox(Mesh mesh, Bounds bounds)
        {
            BuildUnitBox(mesh);
            Vector3[] vertices = mesh.vertices;
            for (int index = 0; index < vertices.Length; index++)
            {
                vertices[index] = Vector3.Scale(vertices[index], bounds.size) + bounds.center;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
        }

        internal static void BuildCylinder(Mesh mesh, int segments, float radius, float height)
        {
            var vertices = new List<Vector3>(segments * 2 + 2);
            var uv = new List<Vector2>(segments * 2 + 2);
            var triangles = new List<int>(segments * 12);
            float halfHeight = height * 0.5f;
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                vertices.Add(new Vector3(x, -halfHeight, z));
                vertices.Add(new Vector3(x, halfHeight, z));
                uv.Add(new Vector2((float)index / segments, 0f));
                uv.Add(new Vector2((float)index / segments, 1f));
            }

            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -halfHeight, 0f));
            uv.Add(new Vector2(0.5f, 0.5f));
            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, halfHeight, 0f));
            uv.Add(new Vector2(0.5f, 0.5f));

            for (int index = 0; index < segments; index++)
            {
                int next = (index + 1) % segments;
                int bottom = index * 2;
                int top = bottom + 1;
                int nextBottom = next * 2;
                int nextTop = nextBottom + 1;
                triangles.Add(bottom);
                triangles.Add(top);
                triangles.Add(nextTop);
                triangles.Add(bottom);
                triangles.Add(nextTop);
                triangles.Add(nextBottom);
                triangles.Add(bottomCenter);
                triangles.Add(nextBottom);
                triangles.Add(bottom);
                triangles.Add(topCenter);
                triangles.Add(top);
                triangles.Add(nextTop);
            }

            Apply(mesh, vertices, triangles, uv);
        }

        internal static void BuildCone(Mesh mesh, int segments, float radius, float height)
        {
            var vertices = new List<Vector3>(segments + 2);
            var uv = new List<Vector2>(segments + 2);
            var triangles = new List<int>(segments * 6);
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius));
                uv.Add(new Vector2((float)index / segments, 0f));
            }

            int apex = vertices.Count;
            vertices.Add(new Vector3(0f, height, 0f));
            uv.Add(new Vector2(0.5f, 1f));
            int center = vertices.Count;
            vertices.Add(Vector3.zero);
            uv.Add(new Vector2(0.5f, 0.5f));
            for (int index = 0; index < segments; index++)
            {
                int next = (index + 1) % segments;
                triangles.Add(index);
                triangles.Add(apex);
                triangles.Add(next);
                triangles.Add(center);
                triangles.Add(next);
                triangles.Add(index);
            }

            Apply(mesh, vertices, triangles, uv);
        }

        internal static void BuildRoadStrip(Mesh mesh, int segments, float width, float y)
        {
            var vertices = new List<Vector3>((segments + 1) * 2);
            var uv = new List<Vector2>((segments + 1) * 2);
            var triangles = new List<int>(segments * 6);
            float halfLength = GaragePrototypePaths.RoadLengthMeters * 0.5f;
            for (int index = 0; index <= segments; index++)
            {
                float t = (float)index / segments;
                float z = Mathf.Lerp(-halfLength, halfLength, t);
                float centerX = RoadCenterX(t);
                float previousT = Mathf.Max(0f, t - 0.01f);
                float nextT = Mathf.Min(1f, t + 0.01f);
                Vector2 tangent = new Vector2(
                    RoadCenterX(nextT) - RoadCenterX(previousT),
                    (nextT - previousT) * GaragePrototypePaths.RoadLengthMeters).normalized;
                Vector2 side = new Vector2(tangent.y, -tangent.x) * (width * 0.5f);
                vertices.Add(new Vector3(centerX - side.x, y, z - side.y));
                vertices.Add(new Vector3(centerX + side.x, y, z + side.y));
                uv.Add(new Vector2(0f, t * 18f));
                uv.Add(new Vector2(1f, t * 18f));
            }

            for (int index = 0; index < segments; index++)
            {
                int start = index * 2;
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            Apply(mesh, vertices, triangles, uv);
        }

        internal static void BuildTerrainPatch(Mesh mesh, int columns, int rows)
        {
            const float minX = -50f;
            const float maxX = 60f;
            const float minZ = -105f;
            const float maxZ = 105f;
            var vertices = new List<Vector3>((columns + 1) * (rows + 1));
            var uv = new List<Vector2>((columns + 1) * (rows + 1));
            var triangles = new List<int>(columns * rows * 6);
            for (int zIndex = 0; zIndex <= rows; zIndex++)
            {
                float zT = (float)zIndex / rows;
                float z = Mathf.Lerp(minZ, maxZ, zT);
                for (int xIndex = 0; xIndex <= columns; xIndex++)
                {
                    float xT = (float)xIndex / columns;
                    float x = Mathf.Lerp(minX, maxX, xT);
                    vertices.Add(new Vector3(x, TerrainHeight(x, z), z));
                    uv.Add(new Vector2(xT * 11f, zT * 21f));
                }
            }

            int stride = columns + 1;
            for (int zIndex = 0; zIndex < rows; zIndex++)
            {
                for (int xIndex = 0; xIndex < columns; xIndex++)
                {
                    int start = zIndex * stride + xIndex;
                    triangles.Add(start);
                    triangles.Add(start + stride);
                    triangles.Add(start + 1);
                    triangles.Add(start + 1);
                    triangles.Add(start + stride);
                    triangles.Add(start + stride + 1);
                }
            }

            Apply(mesh, vertices, triangles, uv);
        }

        private static float RoadCenterX(float t)
        {
            return 15f + Mathf.Sin((t - 0.1f) * Mathf.PI * 1.35f) * 5.5f;
        }

        private static float TerrainHeight(float x, float z)
        {
            float macro = Mathf.Sin(x * 0.055f) * 0.18f + Mathf.Cos(z * 0.035f) * 0.14f;
            float garageFlatten = Mathf.Clamp01((new Vector2(x, z).magnitude - 10f) / 10f);
            return macro * garageFlatten - 0.12f;
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<Vector2> uv,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(1f, 0f));
            uv.Add(new Vector2(1f, 1f));
            uv.Add(new Vector2(0f, 1f));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void Apply(
            Mesh mesh,
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector2> uv)
        {
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
        }
    }
}
