using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Lighting
{
    /// <summary>
    /// Keeps the project-local VLB configuration aligned with the HDRP project.
    /// VLB creates its override asset with Built-in defaults on some imports,
    /// which also leaves Built-in generated shaders bound to the package.
    /// </summary>
    [InitializeOnLoad]
    public static class VlbHdrpCompatibilityConfigurator
    {
        private const string ConfigPath =
            "Assets/Resources/VLBConfigOverride.asset";
        private const int HdrpPipeline = 2;
        private const int SrpBatcherRenderingMode = 3;

        static VlbHdrpCompatibilityConfigurator()
        {
            ScheduleConfiguration();
        }

        [MenuItem("Tools/MSC/Lighting/Configure VLB for HDRP")]
        private static void ConfigureFromMenu()
        {
            Configure(logWhenAlreadyValid: true);
        }

        private static void ScheduleConfiguration()
        {
            EditorApplication.delayCall -= ConfigureAfterEditorLoad;
            EditorApplication.delayCall += ConfigureAfterEditorLoad;
        }

        private static void ConfigureAfterEditorLoad()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleConfiguration();
                return;
            }

            Configure(logWhenAlreadyValid: false);
        }

        private static void Configure(bool logWhenAlreadyValid)
        {
            UnityEngine.Object config =
                AssetDatabase.LoadMainAssetAtPath(ConfigPath);
            if (config == null || config.GetType().FullName != "VLB.Config")
            {
                return;
            }

            var serialized = new SerializedObject(config);
            SerializedProperty pipeline = serialized.FindProperty(
                "m_RenderPipeline");
            SerializedProperty renderingMode = serialized.FindProperty(
                "m_RenderingMode");
            SerializedProperty hdShaderProperty = serialized.FindProperty(
                "_BeamShaderHD");
            if (pipeline == null || renderingMode == null)
            {
                Debug.LogError(
                    "[MSC] VLB config schema is not compatible with the " +
                    "HDRP configurator.",
                    config);
                return;
            }

            Shader hdShader = hdShaderProperty?.objectReferenceValue as Shader;
            bool shaderIsHdrp = hdShader != null &&
                hdShader.name.IndexOf("_HDRP_", StringComparison.Ordinal) >= 0;
            bool needsRefresh = pipeline.intValue != HdrpPipeline ||
                renderingMode.intValue != SrpBatcherRenderingMode ||
                !shaderIsHdrp;
            if (!needsRefresh)
            {
                if (logWhenAlreadyValid)
                {
                    Debug.Log("[MSC] VLB is already configured for HDRP SRP Batcher.");
                }

                return;
            }

            pipeline.intValue = HdrpPipeline;
            renderingMode.intValue = SrpBatcherRenderingMode;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            MethodInfo refresh = config.GetType().GetMethod(
                "_EditorSetRenderingModeAndRefreshShader",
                BindingFlags.Instance | BindingFlags.Public);
            ParameterInfo[] parameters = refresh?.GetParameters();
            if (refresh == null || parameters == null || parameters.Length != 1)
            {
                Debug.LogError(
                    "[MSC] VLB shader refresh API was not found; generated " +
                    "shaders were not changed.",
                    config);
                return;
            }

            object srpBatcher = Enum.ToObject(
                parameters[0].ParameterType,
                SrpBatcherRenderingMode);
            refresh.Invoke(config, new[] { srpBatcher });
            config.GetType().GetMethod(
                "SetScriptingDefineSymbolsForCurrentRenderPipeline",
                BindingFlags.Instance | BindingFlags.Public)?.Invoke(
                config,
                Array.Empty<object>());
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[MSC] VLB configured for HDRP with SRP Batcher shaders. " +
                "Legacy VLB 2.2.3 depth-camera occlusion remains guarded " +
                "until the vendor package is upgraded.",
                config);
        }
    }
}
