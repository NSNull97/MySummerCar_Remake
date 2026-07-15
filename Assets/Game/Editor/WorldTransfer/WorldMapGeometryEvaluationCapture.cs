using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.World.Data;
using MSC.World.Debugging;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.WorldTransfer
{
    public static class WorldMapGeometryEvaluationCapture
    {
        private const int Width = 1600;
        private const int Height = 900;

        [MenuItem("Tools/MSC Remake/World Transfer/05C/Capture Full Map Geometry Views")]
        public static void CaptureViews()
        {
            WorldPartitionBuilder.OpenReferenceOverview();
            Camera camera = Resources.FindObjectsOfTypeAll<Camera>().FirstOrDefault(value => value.gameObject.scene.IsValid());
            if (camera == null) throw new InvalidOperationException("05C bootstrap camera is unavailable.");

            IReadOnlyList<WorldEntityPlacement> records = WorldEntityTable.Parse(File.ReadAllText(
                WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.EntityTableAssetPath)));
            WorldEntityPlacement[] eligible = records.Where(record => record.ReferenceWorldEligible).ToArray();
            string output = Path.GetFullPath(WorldTransferPaths.GeometryEvaluationCaptureRoot);
            Directory.CreateDirectory(output);
            var manifest = new StringBuilder("view,layer,file,target_x,target_y,target_z,unity_version,generator_version\n");

            SetLayerVisibility(showActual: true, showFallback: false);
            Bounds bounds = eligible[0].Bounds;
            for (int index = 1; index < eligible.Length; index++) bounds.Encapsulate(eligible[index].Bounds);
            ConfigureOverhead(camera, bounds);
            Capture(camera, Path.Combine(output, "FullMap_ActualMeshes.png"));
            AppendManifest(manifest, "FullMap", "ActualMeshes", "FullMap_ActualMeshes.png", bounds.center);

            SetLayerVisibility(showActual: false, showFallback: true);
            Dictionary<Transform, Vector3> fallbackScales = EnlargePointFallbacksForOverview();
            Capture(camera, Path.Combine(output, "FullMap_BoundsFallbacks.png"));
            AppendManifest(manifest, "FullMap", "BoundsFallbacks", "FullMap_BoundsFallbacks.png", bounds.center);
            foreach ((Transform transform, Vector3 scale) in fallbackScales) transform.localScale = scale;

            SetStructuralVisibility();
            foreach ((string name, string stableId) in new[]
                     {
                         ("HomeGarage", "fb0f962be1b325cc19296c66751818c0"),
                         ("TownChurch", "5922254ed391973d33d7babd6be0b89f"),
                         ("RepairWorkshop", "f4a1a1c8ca621a1360978da9675c45f0"),
                         ("IslandCottage", "cc1ebee642bc6f1c46285b30093c5a35"),
                         ("HighwayBridge", "cb01ace10f605a79de39069b7e159fbf")
                     })
            {
                WorldEntityPlacement landmark = eligible.FirstOrDefault(record => string.Equals(record.StableId, stableId, StringComparison.Ordinal));
                if (string.IsNullOrEmpty(landmark.StableId)) continue;
                ConfigureOblique(camera, landmark.Position);
                string fileName = name + "_StructuralReview.png";
                Capture(camera, Path.Combine(output, fileName));
                AppendManifest(manifest, name, "StructuralWithoutVegetation", fileName, landmark.Position);
            }

            File.WriteAllText(Path.Combine(output, "capture_manifest.csv"), manifest.ToString(), new UTF8Encoding(false));
            WriteSpatialOutlierCandidates(output, eligible, bounds);
            SetLayerVisibility(showActual: true, showFallback: true);
            Debug.Log("WORLD_MAP_05C_CAPTURE_OK output=" + output);
        }

        public static void RunBatch() => CaptureViews();

        private static void ConfigureOverhead(Camera camera, Bounds bounds)
        {
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z * (Width / (float)Height)) * 0.55f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20000f;
            camera.transform.position = bounds.center + Vector3.up * (bounds.extents.y + 2000f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.075f);
        }

        private static void ConfigureOblique(Camera camera, Vector3 target)
        {
            camera.orthographic = false;
            camera.fieldOfView = 52f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 12000f;
            camera.transform.position = target + new Vector3(65f, 45f, -65f);
            camera.transform.LookAt(target + Vector3.up * 5f);
        }

        private static void SetLayerVisibility(bool showActual, bool showFallback)
        {
            foreach (WorldReferenceEntity entity in Resources.FindObjectsOfTypeAll<WorldReferenceEntity>())
            {
                if (!entity.gameObject.scene.IsValid()) continue;
                Renderer renderer = entity.GetComponent<Renderer>();
                if (renderer == null) continue;
                renderer.enabled = entity.VisualizationKind == WorldReferenceVisualizationKind.ActualMesh ? showActual : showFallback;
            }
        }

        private static void SetStructuralVisibility()
        {
            foreach (WorldReferenceEntity entity in Resources.FindObjectsOfTypeAll<WorldReferenceEntity>())
            {
                if (!entity.gameObject.scene.IsValid()) continue;
                Renderer renderer = entity.GetComponent<Renderer>();
                if (renderer == null) continue;
                renderer.enabled = entity.VisualizationKind == WorldReferenceVisualizationKind.ActualMesh &&
                                   !entity.SemanticCategory.StartsWith("Vegetation", StringComparison.Ordinal);
            }
        }

        private static Dictionary<Transform, Vector3> EnlargePointFallbacksForOverview()
        {
            var result = new Dictionary<Transform, Vector3>();
            foreach (WorldReferenceEntity entity in Resources.FindObjectsOfTypeAll<WorldReferenceEntity>())
            {
                if (entity.VisualizationKind != WorldReferenceVisualizationKind.BoundsFallback || !entity.gameObject.scene.IsValid()) continue;
                Renderer renderer = entity.GetComponent<Renderer>();
                if (renderer == null || Mathf.Max(renderer.bounds.size.x, Mathf.Max(renderer.bounds.size.y, renderer.bounds.size.z)) >= 5f) continue;
                result.Add(entity.transform, entity.transform.localScale);
                entity.transform.localScale *= 15f;
            }
            return result;
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void AppendManifest(StringBuilder manifest, string view, string layer, string file, Vector3 target) =>
            manifest.Append(view).Append(',').Append(layer).Append(',').Append(file).Append(',')
                .Append(target.x.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(target.y.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(target.z.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(Application.unityVersion).Append(',').AppendLine(WorldPartitionBuilder.GeneratorVersion);

        private static void WriteSpatialOutlierCandidates(string output, IReadOnlyList<WorldEntityPlacement> records, Bounds mapBounds)
        {
            Dictionary<string, WorldEntityPlacement> byStableId = records.ToDictionary(record => record.StableId, StringComparer.Ordinal);
            var csv = new StringBuilder("severity,stable_id,category,hierarchy_path,max_dimension_m,renderer_center_distance_m,reason\n");
            int candidateCount = 0;
            foreach (WorldReferenceEntity entity in Resources.FindObjectsOfTypeAll<WorldReferenceEntity>())
            {
                if (entity.VisualizationKind != WorldReferenceVisualizationKind.ActualMesh ||
                    !entity.gameObject.scene.IsValid() || !byStableId.TryGetValue(entity.StableId, out WorldEntityPlacement record)) continue;
                Renderer renderer = entity.GetComponent<Renderer>();
                if (renderer == null) continue;
                Bounds bounds = renderer.bounds;
                float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                float centerDistance = Vector3.Distance(bounds.center, record.Position);
                string severity = string.Empty;
                string reason = string.Empty;
                if (!IsFinite(bounds.min) || !IsFinite(bounds.max))
                {
                    severity = "Critical";
                    reason = "NonFiniteRendererBounds";
                }
                else if (!mapBounds.ExpandContains(bounds.center, 1000f))
                {
                    severity = "High";
                    reason = "RendererCenterOutsideMapEnvelope";
                }
                else if (IsLocalizedCategory(record.Category) && (maxDimension > 250f || centerDistance > 250f))
                {
                    severity = "Review";
                    reason = maxDimension > 250f ? "LocalizedMeshOver250m" : "LocalizedMeshCenterOver250mFromPivot";
                }
                if (string.IsNullOrEmpty(severity)) continue;
                candidateCount++;
                csv.Append(severity).Append(',').Append(record.StableId).Append(',').Append(record.Category).Append(',')
                    .Append(QuoteCsv(record.HierarchyPath)).Append(',')
                    .Append(maxDimension.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                    .Append(centerDistance.ToString("0.###", CultureInfo.InvariantCulture)).Append(',').AppendLine(reason);
            }
            File.WriteAllText(Path.Combine(output, "spatial_outlier_candidates.csv"), csv.ToString(), new UTF8Encoding(false));
            Debug.Log("WORLD_MAP_05C_SPATIAL_AUDIT_OK candidates=" + candidateCount);
        }

        private static bool IsLocalizedCategory(string category) => category is
            "BuildingExterior" or "BuildingInterior" or "InteractivePropCandidate" or "StaticProp" or
            "RoadSign" or "Roof" or "Door" or "Window" or "Floor" or "Fence" or "VegetationTree" or
            "VegetationBush" or "VegetationGrass" or "UtilityPole" or "Landmark" or "Rock" or "Wire";

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool ExpandContains(this Bounds bounds, Vector3 point, float margin)
        {
            bounds.Expand(margin * 2f);
            return bounds.Contains(point);
        }

        private static string QuoteCsv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
