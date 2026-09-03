using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Editor.WorldBaseline
{
    internal static class EngelmannGroundingAuditRenderer
    {
        private const string OutputPath =
            "Artifacts/VegetationImport/" +
            "EngelmannGroundingAudit.png";

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Render Engelmann Grounding Audit")]
        public static void RenderFromMenu()
        {
            Render();
        }

        public static void RenderBatch()
        {
            Render();
        }

        private static void Render()
        {
            var temporaryObjects = new List<UnityEngine.Object>();
            RenderTexture target = null;
            Texture2D capture = null;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                const float targetTreeHeight = 18f;
                const float spacing = 19f;
                string[] variantIds =
                    Phase1VegetationAssetBuilder
                        .EngelmannSpruceVariantIds;
                for (int index = 0;
                     index < variantIds.Length;
                     index++)
                {
                    string variantId = variantIds[index];
                    string path =
                        Phase1VegetationAssetBuilder
                            .EngelmannSprucePrefabRoot +
                        "/EngelmannSpruce_" + variantId + ".prefab";
                    GameObject prefab =
                        AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        throw new FileNotFoundException(
                            "Engelmann audit prefab is missing.",
                            path);
                    }
                    GameObject instance = UnityEngine.Object.Instantiate(
                        prefab);
                    temporaryObjects.Add(instance);
                    Bounds sourceBounds = EnableLod(instance, 0);
                    float scale = targetTreeHeight /
                                  Mathf.Max(sourceBounds.size.y, 0.01f);
                    instance.transform.localScale = Vector3.one * scale;
                    instance.transform.position = new Vector3(
                        index * spacing,
                        0f,
                        0f);
                }

                GameObject ground = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                temporaryObjects.Add(ground);
                ground.name = "Engelmann Audit Ground";
                ground.transform.position = new Vector3(
                    spacing * 2f,
                    -0.15f,
                    0f);
                ground.transform.localScale = new Vector3(
                    spacing * 6f,
                    0.3f,
                    24f);
                Material groundMaterial = new Material(
                    Shader.Find("HDRP/Unlit"))
                {
                    name = "Engelmann Audit Ground Material"
                };
                temporaryObjects.Add(groundMaterial);
                groundMaterial.SetColor(
                    "_UnlitColor",
                    new Color(0.2f, 0.22f, 0.18f, 1f));
                ground.GetComponent<MeshRenderer>().sharedMaterial =
                    groundMaterial;

                GameObject volumeObject = new GameObject(
                    "Engelmann Audit Volume");
                temporaryObjects.Add(volumeObject);
                Volume volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10000f;
                VolumeProfile volumeProfile =
                    ScriptableObject.CreateInstance<VolumeProfile>();
                temporaryObjects.Add(volumeProfile);
                Exposure exposure = volumeProfile.Add<Exposure>();
                exposure.mode.Override(ExposureMode.Fixed);
                exposure.fixedExposure.Override(14f);
                volume.sharedProfile = volumeProfile;

                GameObject lightObject = new GameObject(
                    "Engelmann Audit Sun");
                temporaryObjects.Add(lightObject);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                // HDRP directional intensity is expressed in lux. Keep the
                // audit near a clear daylight reference after the foliage
                // migrated from Unlit to the project Lit wind shader.
                light.intensity = 100000f;
                lightObject.transform.rotation = Quaternion.Euler(
                    42f,
                    -28f,
                    0f);

                GameObject cameraObject = new GameObject(
                    "Engelmann Audit Camera");
                temporaryObjects.Add(cameraObject);
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<HDAdditionalCameraData>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(
                    0.56f,
                    0.62f,
                    0.68f,
                    1f);
                camera.orthographic = true;
                camera.orthographicSize = 24f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 200f;
                Vector3 center = new Vector3(spacing * 2f, 7f, 0f);
                camera.transform.position = center +
                                            new Vector3(0f, 2f, -70f);
                camera.transform.LookAt(center);

                target = new RenderTexture(
                    1600,
                    800,
                    24,
                    RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(
                    1600,
                    800,
                    TextureFormat.RGBA32,
                    false);
                capture.ReadPixels(new Rect(0f, 0f, 1600f, 800f), 0, 0);
                capture.Apply();
                camera.targetTexture = null;

                string absolutePath = Path.GetFullPath(OutputPath);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(absolutePath) ??
                    Directory.GetCurrentDirectory());
                File.WriteAllBytes(absolutePath, capture.EncodeToPNG());
                Debug.Log(
                    "ENGELMANN_GROUNDING_AUDIT_RENDER_OK path='" +
                    absolutePath + "' variants=[" +
                    string.Join(",", variantIds) + "]");
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                if (capture != null)
                {
                    UnityEngine.Object.DestroyImmediate(capture);
                }
                foreach (UnityEngine.Object temporary in
                         temporaryObjects
                             .Where(item => item != null)
                             .Reverse<UnityEngine.Object>())
                {
                    UnityEngine.Object.DestroyImmediate(temporary);
                }
            }
        }

        private static Bounds EnableLod(
            GameObject instance,
            int lodIndex)
        {
            LODGroup group = instance.GetComponent<LODGroup>();
            if (group == null)
            {
                throw new InvalidDataException(
                    "Engelmann audit prefab has no LODGroup: " +
                    instance.name);
            }
            LOD[] lods = group.GetLODs();
            foreach (Renderer renderer in
                     instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
            group.enabled = false;
            Renderer[] selected = lods[lodIndex].renderers;
            Bounds bounds = selected[0].bounds;
            foreach (Renderer renderer in selected)
            {
                renderer.enabled = true;
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }
    }
}
