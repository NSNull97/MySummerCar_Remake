using System;
using System.IO;
using MSC.World.LayoutPilot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldLayoutPilot
{
    public static class WorldLayoutPilotComparisonSceneBuilder
    {
        [MenuItem("Tools/My Summer Car/Milestone 04A/Build ReferenceOnly Comparison Scene")]
        public static void Build()
        {
            WorldLayoutPilotData data = WorldLayoutPilotValidator.LoadDataOrThrow();
            EnsureAssetDirectory(WorldLayoutPilotPaths.ComparisonScene);
            EnsureAssetDirectory(WorldLayoutPilotPaths.ComparisonMaterialRoot + "/placeholder.asset");

            Material anchorMaterial = CreateOrUpdateMaterial(
                "M04A_GarageAnchor",
                new Color(0.96f, 0.22f, 0.12f, 1f));
            Material routeMaterial = CreateOrUpdateMaterial(
                "M04A_ReviewedRoute",
                new Color(0.12f, 0.82f, 0.28f, 1f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "M04A_WorldLayoutPilotComparison";

            var reviewedRoot = new GameObject("M04A_REVIEWED_LAYOUT_REFERENCE_ONLY");
            CreateMarker(
                reviewedRoot.transform,
                "GarageAnchor_ProjectLocal_Origin",
                Vector3.zero,
                2.2f,
                anchorMaterial);
            CreateReviewedRoad(data, reviewedRoot.transform, routeMaterial);

            var currentRoot = new GameObject("M03_CURRENT_PROJECT_OVERLAY_UNCHANGED");
            InstantiatePrefab(
                WorldLayoutPilotPaths.GarageShellPrefab,
                currentRoot.transform,
                "M3_GarageShell_AtReviewedAnchor");
            InstantiatePrefab(
                WorldLayoutPilotPaths.RoadPrefab,
                currentRoot.transform,
                "M3_RoadPrefab_CurrentPlacement_DoesNotMatchM04A");

            var metadata = new GameObject(
                $"Measurements_Route_{data.MeasuredPolylineLengthMeters:F3}m_GarageGap_{data.GarageToNearestRoadSampleMeters:F3}m");
            metadata.transform.SetParent(reviewedRoot.transform, false);

            CreateLighting();
            CreateCamera();
            EditorSceneManager.SaveScene(scene, WorldLayoutPilotPaths.ComparisonScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"M04A_WORLD_LAYOUT_COMPARISON_OK samples={data.RoadSamples.Count} " +
                $"polyline={data.MeasuredPolylineLengthMeters:F3}m nearest={data.GarageToNearestRoadSampleMeters:F3}m");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Milestone 04A comparison build requires batch mode.");
            }

            Build();
        }

        private static void CreateReviewedRoad(
            WorldLayoutPilotData data,
            Transform parent,
            Material material)
        {
            var roadRoot = new GameObject("Reviewed_DirtRoad_Waypoints_1655_1685");
            roadRoot.transform.SetParent(parent, false);

            for (int index = 0; index < data.RoadSamples.Count; index++)
            {
                WorldLayoutRoadSample sample = data.RoadSamples[index];
                CreateMarker(
                    roadRoot.transform,
                    $"Waypoint_{sample.WaypointIndex}_GO_{sample.GameObjectPathId}_T_{sample.TransformPathId}",
                    sample.ProjectLocalMeters,
                    1.6f,
                    material);

                if (index == 0)
                {
                    continue;
                }

                Vector3 start = data.RoadSamples[index - 1].ProjectLocalMeters;
                Vector3 end = sample.ProjectLocalMeters;
                CreateSegment(
                    roadRoot.transform,
                    $"Segment_{data.RoadSamples[index - 1].WaypointIndex}_{sample.WaypointIndex}",
                    start,
                    end,
                    material);
            }
        }

        private static void CreateMarker(
            Transform parent,
            string name,
            Vector3 position,
            float diameter,
            Material material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
            marker.transform.localScale = Vector3.one * diameter;
            marker.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        private static void CreateSegment(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            Material material)
        {
            Vector3 delta = end - start;
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = name;
            segment.transform.SetParent(parent, false);
            segment.transform.localPosition = (start + end) * 0.5f;
            segment.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            segment.transform.localScale = new Vector3(0.85f, 0.18f, delta.magnitude);
            segment.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(segment.GetComponent<Collider>());
        }

        private static GameObject InstantiatePrefab(
            string path,
            Transform parent,
            string instanceName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException("Missing comparison dependency: " + path);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate comparison dependency: " + path);
            }

            instance.name = instanceName;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static void CreateLighting()
        {
            var lightObject = new GameObject("Comparison_Sun");
            lightObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 90000f;
            light.shadows = LightShadows.Soft;
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Comparison_Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0f, 185f, -60f);
            camera.transform.LookAt(new Vector3(0f, 0f, 120f));
            camera.fieldOfView = 54f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
        }

        private static Material CreateOrUpdateMaterial(string name, Color color)
        {
            string path = WorldLayoutPilotPaths.ComparisonMaterialRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException("HDRP/Unlit shader is unavailable.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            if (material.HasProperty("_UnlitColor"))
            {
                material.SetColor("_UnlitColor", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureAssetDirectory(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            string[] parts = directory.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }
    }
}
