using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MSC.UI.EditorTools
{
    /// <summary>Assigns the temporary project-owned logo icon without rebuilding UI scenes.</summary>
    public static class TestGameIconAuthoring
    {
        private const string IconPath =
            "Assets/Game/UI/Presentation/Content/ApplicationIcon/TestGameIcon.png";

        [MenuItem("Tools/MSC Remake/Build/Apply Test Game Icon")]
        public static void Apply()
        {
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("The test game icon PNG is missing: " + IconPath);

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null || icon.width != 1024 || icon.height != 1024)
                throw new InvalidOperationException("The test game icon must import at 1024 x 1024.");

            int[] defaultSizes = AssignAndValidate(NamedBuildTarget.Unknown, icon);
            int[] standaloneSizes = AssignAndValidate(NamedBuildTarget.Standalone, icon);
            AssetDatabase.SaveAssets();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string reportPath = Path.Combine(projectRoot, "Logs", "test-game-icon-setup.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(new SetupReport
            {
                unityVersion = Application.unityVersion,
                iconPath = IconPath,
                iconGuid = AssetDatabase.AssetPathToGUID(IconPath),
                defaultIconSizes = defaultSizes,
                standaloneIconSizes = standaloneSizes,
                result = "PASS"
            }, true));
            Debug.Log("MSC_TEST_GAME_ICON_APPLIED: " + IconPath + "; report=" + reportPath);
        }

        private static int[] AssignAndValidate(NamedBuildTarget target, Texture2D icon)
        {
            int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Application);
            if (sizes.Length == 0)
                throw new InvalidOperationException("No application icon slots for " + target.TargetName);
            var icons = new Texture2D[sizes.Length];
            for (int index = 0; index < icons.Length; index++) icons[index] = icon;
            PlayerSettings.SetIcons(target, icons, IconKind.Application);
            Texture2D[] assigned = PlayerSettings.GetIcons(target, IconKind.Application);
            if (assigned.Length != icons.Length)
                throw new InvalidOperationException("The assigned icon slot count does not match.");
            for (int index = 0; index < assigned.Length; index++)
                if (assigned[index] != icon)
                    throw new InvalidOperationException("The application icon assignment did not persist.");
            return sizes;
        }

        [Serializable]
        private sealed class SetupReport
        {
            public string unityVersion;
            public string iconPath;
            public string iconGuid;
            public int[] defaultIconSizes;
            public int[] standaloneIconSizes;
            public string result;
        }
    }
}
