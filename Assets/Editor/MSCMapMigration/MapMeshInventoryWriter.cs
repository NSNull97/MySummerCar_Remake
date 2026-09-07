using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MSCMapMigration
{
    internal static class MapMeshInventoryWriter
    {
        public static void Write(MapMeshInventory inventory)
        {
            MapMigrationPaths.EnsureAssetFolder(MapMigrationPaths.ReportsRoot);
            File.WriteAllText(
                MapMigrationPaths.ToAbsoluteProjectPath(MapMigrationPaths.InventoryJson),
                JsonUtility.ToJson(inventory, true),
                new UTF8Encoding(false));
            File.WriteAllText(
                MapMigrationPaths.ToAbsoluteProjectPath(MapMigrationPaths.InventoryCsv),
                BuildCsv(inventory),
                new UTF8Encoding(false));
        }

        public static MapMeshInventory Read()
        {
            string path = MapMigrationPaths.ToAbsoluteProjectPath(
                MapMigrationPaths.InventoryJson);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Map inventory has not been generated.", path);
            }

            MapMeshInventory inventory = JsonUtility.FromJson<MapMeshInventory>(
                File.ReadAllText(path, Encoding.UTF8));
            if (inventory == null)
            {
                throw new InvalidDataException("Map inventory JSON is invalid.");
            }

            return inventory;
        }

        private static string BuildCsv(MapMeshInventory inventory)
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine(
                "RecordId,ScenePath,HierarchyPath,Category,GameObjectName," +
                "RendererType,MeshAssetName,AssetGuid,LocalFileId,InstanceId," +
                "VertexCount,TriangleCount,SubMeshCount,MaterialNames," +
                "LocalPosition,LocalRotation,LocalScale,WorldMatrix,WorldBounds," +
                "HasMeshCollider,MeshColliderEnabled,ActiveSelf,ActiveInHierarchy," +
                "NegativeWorldDeterminant,Layer,Tag,ProvenanceStableId," +
                "ProvenanceHierarchyPath,SemanticCategory,SharedMeshInstanceCount," +
                "ClassificationReason,EntityId");
            foreach (MapMeshRecord record in inventory.records)
            {
                string[] values =
                {
                    record.recordId,
                    record.scenePath,
                    record.hierarchyPath,
                    record.category.ToString(),
                    record.gameObjectName,
                    record.rendererType,
                    record.meshAssetName,
                    record.assetGuid,
                    record.localFileId.ToString(CultureInfo.InvariantCulture),
                    record.instanceId.ToString(CultureInfo.InvariantCulture),
                    record.vertexCount.ToString(CultureInfo.InvariantCulture),
                    record.triangleCount.ToString(CultureInfo.InvariantCulture),
                    record.subMeshCount.ToString(CultureInfo.InvariantCulture),
                    string.Join("|", record.materialNames),
                    Vector(record.localTransform.position),
                    Quaternion(record.localTransform.rotation),
                    Vector(record.localTransform.scale),
                    string.Join(";", record.worldMatrix.values.Select(value =>
                        value.ToString("R", CultureInfo.InvariantCulture))),
                    Vector(record.worldBounds.center) + ";" + Vector(record.worldBounds.size),
                    record.hasMeshCollider.ToString(),
                    record.meshColliderEnabled.ToString(),
                    record.activeSelf.ToString(),
                    record.activeInHierarchy.ToString(),
                    record.hasNegativeWorldDeterminant.ToString(),
                    record.layerName,
                    record.tag,
                    record.provenanceStableId,
                    record.provenanceHierarchyPath,
                    record.provenanceSemanticCategory,
                    record.sharedMeshInstanceCount.ToString(CultureInfo.InvariantCulture),
                    record.classificationReason,
                    record.entityId.ToString(CultureInfo.InvariantCulture)
                };
                builder.AppendLine(string.Join(",", values.Select(Escape)));
            }

            return builder.ToString();
        }

        private static string Vector(Vector3 value) => string.Join(";", new[]
        {
            value.x.ToString("R", CultureInfo.InvariantCulture),
            value.y.ToString("R", CultureInfo.InvariantCulture),
            value.z.ToString("R", CultureInfo.InvariantCulture)
        });

        private static string Quaternion(UnityEngine.Quaternion value) => string.Join(";", new[]
        {
            value.x.ToString("R", CultureInfo.InvariantCulture),
            value.y.ToString("R", CultureInfo.InvariantCulture),
            value.z.ToString("R", CultureInfo.InvariantCulture),
            value.w.ToString("R", CultureInfo.InvariantCulture)
        });

        private static string Escape(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}
