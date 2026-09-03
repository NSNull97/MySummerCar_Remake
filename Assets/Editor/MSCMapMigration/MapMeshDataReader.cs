using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSCMapMigration
{
    internal sealed class ReadableMeshData
    {
        public Vector3[] Vertices { get; init; } = Array.Empty<Vector3>();
        public Vector3[] Normals { get; init; } = Array.Empty<Vector3>();
        public Vector2[] Uv0 { get; init; } = Array.Empty<Vector2>();
        public Color32[] Colors { get; init; } = Array.Empty<Color32>();
        public List<int[]> TriangleSubMeshes { get; init; } = new List<int[]>();
    }

    internal static class MapMeshDataReader
    {
        public static ReadableMeshData Read(Mesh mesh)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            using Mesh.MeshDataArray dataArray =
                Mesh.AcquireReadOnlyMeshData(mesh);
            Mesh.MeshData data = dataArray[0];

            Vector3[] vertices = ReadVertices(data);
            Vector3[] normals = data.HasVertexAttribute(VertexAttribute.Normal)
                ? ReadNormals(data)
                : Array.Empty<Vector3>();
            Vector2[] uv0 = data.HasVertexAttribute(VertexAttribute.TexCoord0)
                ? ReadUv0(data)
                : Array.Empty<Vector2>();
            Color32[] colors = data.HasVertexAttribute(VertexAttribute.Color)
                ? ReadColors(data)
                : Array.Empty<Color32>();

            var subMeshes = new List<int[]>(data.subMeshCount);
            for (int subMesh = 0; subMesh < data.subMeshCount; subMesh++)
            {
                SubMeshDescriptor descriptor = data.GetSubMesh(subMesh);
                if (descriptor.topology != MeshTopology.Triangles)
                {
                    subMeshes.Add(Array.Empty<int>());
                    continue;
                }

                using var indices = new NativeArray<int>(
                    descriptor.indexCount,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);
                data.GetIndices(indices, subMesh, true);
                subMeshes.Add(indices.ToArray());
            }

            return new ReadableMeshData
            {
                Vertices = vertices,
                Normals = normals,
                Uv0 = uv0,
                Colors = colors,
                TriangleSubMeshes = subMeshes
            };
        }

        private static Vector3[] ReadVertices(Mesh.MeshData data)
        {
            using var values = new NativeArray<Vector3>(
                data.vertexCount,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            data.GetVertices(values);
            return values.ToArray();
        }

        private static Vector3[] ReadNormals(Mesh.MeshData data)
        {
            using var values = new NativeArray<Vector3>(
                data.vertexCount,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            data.GetNormals(values);
            return values.ToArray();
        }

        private static Vector2[] ReadUv0(Mesh.MeshData data)
        {
            using var values = new NativeArray<Vector2>(
                data.vertexCount,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            data.GetUVs(0, values);
            return values.ToArray();
        }

        private static Color32[] ReadColors(Mesh.MeshData data)
        {
            using var values = new NativeArray<Color32>(
                data.vertexCount,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            data.GetColors(values);
            return values.ToArray();
        }
    }
}
