using System;
using MSC.Weather.System.NativeHDRP;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Weather.System.Editor
{
    public static class FinnishSummerClimateAssetBuilder
    {
        public const string ContentRoot =
            "Assets/Game/Weather/System/Content/FinnishSummer";
        public const string PresetRoot = ContentRoot + "/Presets";
        public const string ClimateProfilePath =
            ContentRoot + "/FinnishSummerClimateProfile.asset";
        public const string GeographyPath =
            ContentRoot + "/FinnishSummerGeography.asset";
        public const string NativeVolumeProfilePath =
            ContentRoot + "/NativeHDRPWeatherVolume.asset";

        [MenuItem("Tools/MSC/Weather System/Create or Update Finnish Summer Content")]
        public static void BuildFromMenu()
        {
            BuildAll();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<
                FinnishSummerClimateProfile>(ClimateProfilePath);
            Debug.Log(
                "Weather System: Finnish summer content is up to date (16 presets)." +
                " Existing assets were updated in place.");
        }

        public static void BuildAll()
        {
            EnsureFolder(ContentRoot);
            EnsureFolder(PresetRoot);
            WeatherPresetDefinition[] definitions =
                FinnishSummerClimateDefaults.CreateDefinitions();
            var presets = new WeatherPreset[definitions.Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                WeatherPresetDefinition definition = definitions[index];
                string suffix = definition.StableId.Substring(
                    definition.StableId.LastIndexOf('.') + 1);
                string path = PresetRoot + "/WeatherPreset_" + suffix + ".asset";
                WeatherPreset preset = LoadOrCreate<WeatherPreset>(path);
                preset.ConfigureForAuthoring(definition);
                EditorUtility.SetDirty(preset);
                presets[index] = preset;
            }

            FinnishSummerClimateProfile profile =
                LoadOrCreate<FinnishSummerClimateProfile>(ClimateProfilePath);
            profile.ConfigureForAuthoring(
                FinnishSummerClimateProfile.DefaultConfigId,
                presets,
                FinnishSummerWeatherIds.PartlyCloudy,
                6,
                0.12f,
                0.5f);
            EditorUtility.SetDirty(profile);

            WeatherGeographySettings geography =
                LoadOrCreate<WeatherGeographySettings>(GeographyPath);
            geography.ConfigureForAuthoring(
                62.4f,
                25.7f,
                220,
                3f,
                0f);
            EditorUtility.SetDirty(geography);

            LoadOrCreate<VolumeProfile>(NativeVolumeProfilePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = global::System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
