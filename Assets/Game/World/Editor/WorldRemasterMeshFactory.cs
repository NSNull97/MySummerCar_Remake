using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.World.Remaster.Editor
{
    internal static class WorldRemasterMeshFactory
    {
        internal static void BuildTerrain(Mesh mesh, int columns = 48, int rows = 40)
        {
            int vertexCount = (columns + 1) * (rows + 1);
            var vertices = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var triangles = new int[columns * rows * 6];

            for (int row = 0; row <= rows; row++)
            {
                float v = row / (float)rows;
                float z = Mathf.Lerp(-60f, 70f, v);
                for (int column = 0; column <= columns; column++)
                {
                    float u = column / (float)columns;
                    float x = Mathf.Lerp(-80f, 80f, u);
                    vertices[row * (columns + 1) + column] = new Vector3(x, TerrainHeight(x, z), z);
                    uv[row * (columns + 1) + column] = new Vector2(u * 12f, v * 10f);
                }
            }

            int triangle = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int a = row * (columns + 1) + column;
                    int b = a + 1;
                    int c = a + columns + 1;
                    int d = c + 1;
                    triangles[triangle++] = a;
                    triangles[triangle++] = c;
                    triangles[triangle++] = b;
                    triangles[triangle++] = b;
                    triangles[triangle++] = c;
                    triangles[triangle++] = d;
                }
            }

            Apply(mesh, vertices, triangles, uv);
        }

        internal static void BuildRoad(Mesh mesh, int segments = 48)
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int index = 0; index <= segments; index++)
            {
                float t = index / (float)segments;
                float x = Mathf.Lerp(-80f, 80f, t);
                float centerZ = -30f + Mathf.Sin(t * Mathf.PI * 1.35f) * 1.4f;
                float y = TerrainHeight(x, centerZ) + 0.06f;
                vertices[index * 2] = new Vector3(x, y, centerZ - 3.15f);
                vertices[index * 2 + 1] = new Vector3(x, y, centerZ + 3.15f);
                uv[index * 2] = new Vector2(t * 24f, 0f);
                uv[index * 2 + 1] = new Vector2(t * 24f, 1f);
                if (index >= segments)
                {
                    continue;
                }

                int vertex = index * 2;
                int triangle = index * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            Apply(mesh, vertices, triangles, uv);
        }

        internal static void BuildDriveway(Mesh mesh)
        {
            const int segments = 12;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int index = 0; index <= segments; index++)
            {
                float t = index / (float)segments;
                float z = Mathf.Lerp(-30f, 0f, t);
                float halfWidth = Mathf.Lerp(4.1f, 3.25f, t);
                float centerX = Mathf.Sin(t * Mathf.PI) * 0.65f;
                vertices[index * 2] = new Vector3(centerX - halfWidth, 0.075f, z);
                vertices[index * 2 + 1] = new Vector3(centerX + halfWidth, 0.075f, z);
                uv[index * 2] = new Vector2(0f, t * 6f);
                uv[index * 2 + 1] = new Vector2(1f, t * 6f);
                if (index >= segments)
                {
                    continue;
                }

                int vertex = index * 2;
                int triangle = index * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            Apply(mesh, vertices, triangles, uv);
        }

        internal static void BuildDitchWater(Mesh mesh)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            AddWaterSpan(vertices, uv, triangles, -80f, -5.2f, -22.6f, 1.3f);
            AddWaterSpan(vertices, uv, triangles, 5.2f, 80f, -22.6f, 1.3f);
            Apply(mesh, vertices.ToArray(), triangles.ToArray(), uv.ToArray());
        }

        private static void AddWaterSpan(
            List<Vector3> vertices,
            List<Vector2> uv,
            List<int> triangles,
            float minX,
            float maxX,
            float centerZ,
            float width)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(minX, -0.48f, centerZ - width * 0.5f));
            vertices.Add(new Vector3(maxX, -0.48f, centerZ - width * 0.5f));
            vertices.Add(new Vector3(minX, -0.48f, centerZ + width * 0.5f));
            vertices.Add(new Vector3(maxX, -0.48f, centerZ + width * 0.5f));
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(8f, 0f));
            uv.Add(new Vector2(0f, 1f));
            uv.Add(new Vector2(8f, 1f));
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        internal static float TerrainHeight(float x, float z)
        {
            float undulation = Mathf.Sin(x * 0.055f) * 0.16f + Mathf.Cos(z * 0.043f) * 0.11f;
            float ditchDistance = Mathf.Abs(z + 22.6f);
            float ditch = -0.72f * Mathf.Exp(-ditchDistance * ditchDistance / 5.5f);
            float homeFlatten = Mathf.InverseLerp(6f, 1.5f, DistanceToRectangle(x, z, -10f, 24f, -4f, 18f));
            float drivewayFlatten = Mathf.InverseLerp(4f, 0.5f, DistanceToRectangle(x, z, -5f, 5f, -30f, 1f));
            float flatten = Mathf.Max(homeFlatten, drivewayFlatten);
            return Mathf.Lerp(undulation + ditch, 0f, flatten);
        }

        private static float DistanceToRectangle(
            float x,
            float z,
            float minX,
            float maxX,
            float minZ,
            float maxZ)
        {
            float dx = Mathf.Max(Mathf.Max(minX - x, 0f), x - maxX);
            float dz = Mathf.Max(Mathf.Max(minZ - z, 0f), z - maxZ);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static void Apply(Mesh mesh, Vector3[] vertices, int[] triangles, Vector2[] uv)
        {
            mesh.Clear();
            mesh.indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }
    }
}
