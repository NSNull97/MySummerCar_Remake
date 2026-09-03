using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Editor.Vegetation
{
    /// <summary>Repairs only the owned 84-vertex Engelmann shared billboard; no prefab or scene rebuild.</summary>
    public static class MapVegetationBillboardWindingRepair
    {
        public const string MeshPath = "Assets/Game/Presentation/Vegetation/Generated/Phase1/Meshes/EngelmannSpruce/EngelmannSpruce_TieredCrossBillboard.asset";
        public const string MeshGuid = "2d2e3267396235c4183d9a22bb630b50";
        public const string ReportPath = "Artifacts/VegetationRebuild/TreePresentationAudit/billboard-winding-repair.json";

        [MenuItem("Tools/MSC Remake/Vegetation/Repair Shared Engelmann Billboard Winding")]
        public static void RepairBatch()
        {
            if (AssetDatabase.AssetPathToGUID(MeshPath) != MeshGuid)
                throw new InvalidDataException("The owned Engelmann billboard GUID changed; repair refused.");
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            ValidateShape(mesh);
            string beforeHash = HashFile(MeshPath);
            string metaHash = HashFile(MeshPath + ".meta");
            string attributesBefore = AttributeHash(mesh);
            Bounds boundsBefore = mesh.bounds;
            string backup = "Artifacts/VegetationRebuild/TreePresentationAudit/BillboardBackup/" + beforeHash + ".asset";
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(MeshPath, backup);
            else if (HashFile(backup) != beforeHash)
                throw new InvalidDataException("Existing exact billboard backup differs; repair refused.");

            bool changed = CorrectReversedWinding(mesh);
            if (AttributeHash(mesh) != attributesBefore || !mesh.bounds.Equals(boundsBefore))
                throw new InvalidDataException("Billboard repair changed vertex attributes or bounds.");
            if (changed)
            {
                EditorUtility.SetDirty(mesh);
                AssetDatabase.SaveAssetIfDirty(mesh);
            }
            if (AssetDatabase.AssetPathToGUID(MeshPath) != MeshGuid || HashFile(MeshPath + ".meta") != metaHash)
                throw new InvalidDataException("Billboard repair changed its GUID/meta.");
            var report = new RepairReport
            {
                utc = DateTime.UtcNow.ToString("O"), path = MeshPath, guid = MeshGuid,
                beforeSha256 = beforeHash, afterSha256 = HashFile(MeshPath), metaSha256 = metaHash,
                vertexAttributesSha256 = attributesBefore, backup = backup,
                changed = changed, vertices = mesh.vertexCount, triangles = (int)mesh.GetIndexCount(0) / 3,
                bounds = mesh.bounds,
                method = "Reverse each of the 42 triangle windings only when all serialized vertex normals oppose geometric face normals. Preserve all 84 vertices, normal/tangent/UV streams, bounds, GUID and every prefab/scene reference. No material, transform or population change."
            };
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            Debug.Log("MAP_BILLBOARD_WINDING_REPAIR_OK changed=" + changed + " triangles=42 report=" + ReportPath);
        }

        internal static bool CorrectReversedWinding(Mesh mesh)
        {
            ValidateShape(mesh);
            int[] indices = mesh.GetIndices(0);
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int reversed = 0, aligned = 0;
            for (int index = 0; index < indices.Length; index += 3)
            {
                Vector3 face = Vector3.Cross(vertices[indices[index + 1]] - vertices[indices[index]],
                    vertices[indices[index + 2]] - vertices[indices[index]]);
                Vector3 normal = normals[indices[index]] + normals[indices[index + 1]] + normals[indices[index + 2]];
                if (face.sqrMagnitude < 0.00000001f || normal.sqrMagnitude < 0.00000001f)
                    throw new InvalidDataException("Degenerate billboard face/normal; repair refused.");
                float dot = Vector3.Dot(face.normalized, normal.normalized);
                if (dot > 0.999f) aligned++;
                else if (dot < -0.999f) reversed++;
                else throw new InvalidDataException("Non-planar billboard normal basis; repair refused.");
            }
            if (aligned == 42) return false;
            if (reversed != 42)
                throw new InvalidDataException("Mixed billboard winding; repair refused before mutation.");
            for (int index = 0; index < indices.Length; index += 3)
                (indices[index + 1], indices[index + 2]) = (indices[index + 2], indices[index + 1]);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            return true;
        }

        private static void ValidateShape(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable || mesh.vertexCount != 84 || mesh.subMeshCount != 1 ||
                mesh.GetTopology(0) != MeshTopology.Triangles || mesh.GetIndexCount(0) != 126 ||
                mesh.normals.Length != 84 || mesh.GetSubMesh(0).baseVertex != 0)
                throw new InvalidDataException("Expected the exact readable 84-vertex/42-triangle owned billboard.");
        }

        internal static string AttributeHash(Mesh mesh)
        {
            using var data = Mesh.AcquireReadOnlyMeshData(mesh);
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
            {
                writer.Write(mesh.vertexCount); writer.Write(mesh.vertexBufferCount);
                for (int stream = 0; stream < mesh.vertexBufferCount; stream++)
                {
                    writer.Write(mesh.GetVertexBufferStride(stream));
                    var buffer = data[0].GetVertexData<byte>(stream);
                    writer.Write(buffer.Length);
                    for (int index = 0; index < buffer.Length; index++) writer.Write(buffer[index]);
                }
            }
            using SHA256 hash = SHA256.Create();
            return Hex(hash.ComputeHash(bytes.ToArray()));
        }

        private static string HashFile(string path)
        {
            using SHA256 hash = SHA256.Create();
            using FileStream file = File.OpenRead(path);
            return Hex(hash.ComputeHash(file));
        }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

        [Serializable] private sealed class RepairReport
        {
            public string utc, path, guid, beforeSha256, afterSha256, metaSha256, vertexAttributesSha256, backup, method;
            public bool changed;
            public int vertices, triangles;
            public Bounds bounds;
        }
    }
}
