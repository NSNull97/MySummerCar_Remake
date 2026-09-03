using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MSCMapMigration
{
    internal static class ObjExporter
    {
        public static void Write(
            string absoluteObjPath,
            string materialLibraryReference,
            IReadOnlyList<ScannedMeshInstance> instances,
            bool worldSpace,
            IReadOnlyDictionary<Material, string> materialNames)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteObjPath)!);
            using var writer = new StreamWriter(
                absoluteObjPath,
                append: false,
                new UTF8Encoding(false),
                bufferSize: 1024 * 1024);
            writer.NewLine = "\n";
            writer.WriteLine("# MSC Remake map migration OBJ");
            writer.WriteLine("# 1 Unity unit = 1 metre; coordinates use Unity Y-up.");
            if (!string.IsNullOrWhiteSpace(materialLibraryReference))
            {
                writer.WriteLine("mtllib " + materialLibraryReference.Replace('\\', '/'));
            }

            int vertexOffset = 1;
            int uvOffset = 1;
            int normalOffset = 1;
            foreach (ScannedMeshInstance instance in instances)
            {
                ReadableMeshData data = MapMeshDataReader.Read(instance.Mesh);
                Matrix4x4 matrix = worldSpace
                    ? instance.LocalToWorld
                    : Matrix4x4.identity;
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                string objectName = SafeName(
                    instance.Record.category + "__" +
                    instance.Record.gameObjectName + "__" +
                    instance.Record.recordId);
                writer.WriteLine("o " + objectName);

                for (int index = 0; index < data.Vertices.Length; index++)
                {
                    Vector3 vertex = TransformVertex(matrix, data.Vertices[index]);
                    if (data.Colors.Length == data.Vertices.Length)
                    {
                        Color32 color = data.Colors[index];
                        writer.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "v {0:R} {1:R} {2:R} {3:R} {4:R} {5:R}",
                            vertex.x, vertex.y, vertex.z,
                            color.r / 255f, color.g / 255f, color.b / 255f));
                    }
                    else
                    {
                        writer.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "v {0:R} {1:R} {2:R}",
                            vertex.x, vertex.y, vertex.z));
                    }
                }

                foreach (Vector2 uv in data.Uv0)
                {
                    writer.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "vt {0:R} {1:R}", uv.x, uv.y));
                }

                foreach (Vector3 sourceNormal in data.Normals)
                {
                    Vector3 normal = normalMatrix.MultiplyVector(sourceNormal).normalized;
                    writer.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "vn {0:R} {1:R} {2:R}",
                        normal.x, normal.y, normal.z));
                }

                bool hasUv = data.Uv0.Length == data.Vertices.Length;
                bool hasNormals = data.Normals.Length == data.Vertices.Length;
                bool reverse = ShouldReverseWinding(matrix);
                Material[] materials = instance.Renderer.sharedMaterials;
                for (int subMesh = 0;
                     subMesh < data.TriangleSubMeshes.Count;
                     subMesh++)
                {
                    Material material = subMesh < materials.Length
                        ? materials[subMesh]
                        : null;
                    if (material != null && materialNames.TryGetValue(material, out string materialName))
                    {
                        writer.WriteLine("usemtl " + materialName);
                    }

                    int[] indices = data.TriangleSubMeshes[subMesh];
                    for (int index = 0; index + 2 < indices.Length; index += 3)
                    {
                        int a = indices[index];
                        int b = indices[index + (reverse ? 2 : 1)];
                        int c = indices[index + (reverse ? 1 : 2)];
                        writer.Write("f ");
                        WriteFaceVertex(writer, a, vertexOffset, uvOffset, normalOffset, hasUv, hasNormals);
                        writer.Write(' ');
                        WriteFaceVertex(writer, b, vertexOffset, uvOffset, normalOffset, hasUv, hasNormals);
                        writer.Write(' ');
                        WriteFaceVertex(writer, c, vertexOffset, uvOffset, normalOffset, hasUv, hasNormals);
                        writer.WriteLine();
                    }
                }

                vertexOffset += data.Vertices.Length;
                if (hasUv)
                {
                    uvOffset += data.Uv0.Length;
                }

                if (hasNormals)
                {
                    normalOffset += data.Normals.Length;
                }
            }
        }

        public static void WriteMtl(
            string absoluteMtlPath,
            IEnumerable<Material> materials,
            IReadOnlyDictionary<Material, string> materialNames,
            IReadOnlyDictionary<Texture, string> textureFiles,
            string textureRelativePrefix)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteMtlPath)!);
            using var writer = new StreamWriter(
                absoluteMtlPath,
                append: false,
                new UTF8Encoding(false));
            writer.NewLine = "\n";
            foreach (Material material in materials
                         .Where(value => value != null)
                         .Distinct())
            {
                writer.WriteLine("newmtl " + materialNames[material]);
                Color color = GetColor(material);
                writer.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "Kd {0:R} {1:R} {2:R}", color.r, color.g, color.b));
                writer.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "d {0:R}", color.a));
                writer.WriteLine("illum 2");
                WriteTexture(writer, material, "_BaseColorMap", "map_Kd", textureFiles, textureRelativePrefix);
                WriteTexture(writer, material, "_BaseMap", "map_Kd", textureFiles, textureRelativePrefix);
                WriteTexture(writer, material, "_NormalMap", "map_Bump", textureFiles, textureRelativePrefix);
                WriteTexture(writer, material, "_BumpMap", "map_Bump", textureFiles, textureRelativePrefix);
                writer.WriteLine();
            }
        }

        internal static bool ShouldReverseWinding(Matrix4x4 matrix) =>
            matrix.determinant < 0f;

        internal static Vector3 TransformVertex(Matrix4x4 matrix, Vector3 vertex) =>
            matrix.MultiplyPoint3x4(vertex);

        internal static string SafeName(string value)
        {
            var builder = new StringBuilder(value?.Length ?? 0);
            foreach (char character in value ?? string.Empty)
            {
                builder.Append(char.IsLetterOrDigit(character) ||
                               character == '_' || character == '-'
                    ? character
                    : '_');
            }

            string result = builder.ToString().Trim('_');
            return string.IsNullOrWhiteSpace(result) ? "Unnamed" : result;
        }

        private static void WriteFaceVertex(
            TextWriter writer,
            int index,
            int vertexOffset,
            int uvOffset,
            int normalOffset,
            bool hasUv,
            bool hasNormals)
        {
            writer.Write(index + vertexOffset);
            if (!hasUv && !hasNormals)
            {
                return;
            }

            writer.Write('/');
            if (hasUv)
            {
                writer.Write(index + uvOffset);
            }

            if (hasNormals)
            {
                writer.Write('/');
                writer.Write(index + normalOffset);
            }
        }

        private static void WriteTexture(
            TextWriter writer,
            Material material,
            string property,
            string mtlProperty,
            IReadOnlyDictionary<Texture, string> textureFiles,
            string relativePrefix)
        {
            if (!material.HasProperty(property))
            {
                return;
            }

            Texture texture = material.GetTexture(property);
            if (texture == null || !textureFiles.TryGetValue(texture, out string fileName))
            {
                return;
            }

            writer.WriteLine(
                mtlProperty + " " + relativePrefix.Replace('\\', '/') + fileName);
        }

        private static Color GetColor(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return Color.white;
        }
    }
}
