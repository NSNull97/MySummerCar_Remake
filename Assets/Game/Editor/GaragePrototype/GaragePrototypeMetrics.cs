using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.GaragePrototype
{
    [Serializable]
    public sealed class GaragePrototypeMetricsCapture
    {
        public string capturedUtc = string.Empty;
        public string unityVersion = string.Empty;
        public string scenePath = string.Empty;
        public int rootObjects;
        public int renderers;
        public int uniqueMeshes;
        public int vertices;
        public int triangles;
        public int uniqueMaterials;
        public int uniqueTextures;
        public long estimatedTextureBytes;
        public int colliders;
        public int lodGroups;
        public int lights;
        public int reflectionProbes;
    }

    public static class GaragePrototypeMetrics
    {
        public const int TriangleBudget = 100000;
        public const int RendererBudget = 300;
        public const int LightBudget = 8;
        public const long EstimatedTextureBudgetBytes = 32L * 1024L * 1024L;

        public static GaragePrototypeMetricsCapture CollectProductionScene(bool writeCapture)
        {
            Scene scene = EditorSceneManager.OpenScene(
                GaragePrototypePaths.ProductionScene,
                OpenSceneMode.Single);
            GaragePrototypeMetricsCapture capture = Collect(scene);
            if (writeCapture)
            {
                string fullPath = Path.GetFullPath(GaragePrototypePaths.StaticCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
                File.WriteAllText(fullPath, JsonUtility.ToJson(capture, true));
            }

            return capture;
        }

        public static GaragePrototypeMetricsCapture Collect(Scene scene)
        {
            var meshes = new HashSet<Mesh>();
            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            int rendererCount = 0;
            int colliderCount = 0;
            int lodGroupCount = 0;
            int lightCount = 0;
            int reflectionProbeCount = 0;

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                Renderer[] rootRenderers = root.GetComponentsInChildren<Renderer>(true);
                rendererCount += rootRenderers.Length;
                foreach (Renderer renderer in rootRenderers)
                {
                    if (renderer is MeshRenderer)
                    {
                        MeshFilter filter = renderer.GetComponent<MeshFilter>();
                        if (filter != null && filter.sharedMesh != null)
                        {
                            meshes.Add(filter.sharedMesh);
                        }
                    }

                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null)
                        {
                            materials.Add(material);
                        }
                    }
                }

                colliderCount += root.GetComponentsInChildren<Collider>(true).Length;
                lodGroupCount += root.GetComponentsInChildren<LODGroup>(true).Length;
                lightCount += root.GetComponentsInChildren<Light>(true).Length;
                reflectionProbeCount += root.GetComponentsInChildren<ReflectionProbe>(true).Length;
            }

            int vertexCount = 0;
            int triangleCount = 0;
            foreach (Mesh mesh in meshes)
            {
                vertexCount += mesh.vertexCount;
                triangleCount += mesh.triangles.Length / 3;
            }

            foreach (Material material in materials)
            {
                string[] textureProperties = material.GetTexturePropertyNames();
                foreach (string property in textureProperties)
                {
                    Texture texture = material.GetTexture(property);
                    if (texture != null)
                    {
                        textures.Add(texture);
                    }
                }
            }

            long estimatedTextureBytes = 0;
            foreach (Texture texture in textures)
            {
                if (texture is Texture2D texture2D)
                {
                    estimatedTextureBytes += (long)texture2D.width * texture2D.height * 4L * 4L / 3L;
                }
            }

            return new GaragePrototypeMetricsCapture
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                scenePath = scene.path,
                rootObjects = roots.Length,
                renderers = rendererCount,
                uniqueMeshes = meshes.Count,
                vertices = vertexCount,
                triangles = triangleCount,
                uniqueMaterials = materials.Count,
                uniqueTextures = textures.Count,
                estimatedTextureBytes = estimatedTextureBytes,
                colliders = colliderCount,
                lodGroups = lodGroupCount,
                lights = lightCount,
                reflectionProbes = reflectionProbeCount
            };
        }
    }
}
