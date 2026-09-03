using UnityEditor;
using UnityEngine;

namespace MSC.Editor.TextureUpscale
{
    public static class TextureImportSettingsBackup
    {
        public const string SnapshotRelativePath =
            "Reports/TextureUpscale/importer_settings_snapshot.json";

        [MenuItem("MSC/Texture Upscale/Capture Importer Snapshot")]
        public static void CaptureMenu() => CaptureBatch();

        public static void CaptureBatch()
        {
            TextureInventoryData inventory =
                TextureInventoryExporter.BuildInventory();
            WriteSnapshot(inventory);
            Debug.Log(
                $"Texture importer snapshot captured: " +
                $"{inventory.textures.Count} textures -> " +
                SnapshotRelativePath);
        }

        public static void WriteSnapshot(TextureInventoryData inventory) =>
            TextureInventoryExporter.WriteJson(
                SnapshotRelativePath,
                inventory);
    }
}
