using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MSC.World.Remaster.Editor
{
    public static class WorldRemasterVisualCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("Tools/MSC Remake/World Remaster/Capture Pilot Comparison Views")]
        public static void CapturePilotComparisonViews()
        {
            string outputDirectory = Path.GetFullPath(WorldRemasterPaths.VisualCaptureRoot);
            Directory.CreateDirectory(outputDirectory);

            var scene = EditorSceneManager.OpenScene(WorldRemasterPaths.ComparisonScene, OpenSceneMode.Single);
            WorldRemasterModeController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldRemasterModeController>(true))
                .FirstOrDefault();
            Camera camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault();

            if (controller == null || camera == null)
            {
                throw new InvalidOperationException("Comparison scene requires a mode controller and camera.");
            }

            var manifest = new StringBuilder();
            manifest.AppendLine("mode,file,width,height,camera_position,camera_rotation,fov,unity_version,builder_version");
            foreach (WorldComparisonMode mode in new[]
                     {
                         WorldComparisonMode.ReferenceOnly,
                         WorldComparisonMode.ProductionOnly,
                         WorldComparisonMode.OverlayComparison
                     })
            {
                controller.ApplyMode(mode);
                string fileName = mode + ".png";
                string path = Path.Combine(outputDirectory, fileName);
                Capture(camera, path);
                manifest.Append(mode).Append(',')
                    .Append(fileName).Append(',')
                    .Append(Width).Append(',')
                    .Append(Height).Append(',')
                    .Append(Quote(Vector(camera.transform.position))).Append(',')
                    .Append(Quote(Vector(camera.transform.eulerAngles))).Append(',')
                    .Append(camera.fieldOfView.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Application.unityVersion).Append(',')
                    .AppendLine(WorldRemasterPaths.BuilderVersion);
            }

            File.WriteAllText(Path.Combine(outputDirectory, "capture_manifest.csv"), manifest.ToString(), new UTF8Encoding(false));
            Debug.Log("WORLD_REMASTER_05A_CAPTURE_OK output=" + outputDirectory);
        }

        [MenuItem("Tools/MSC Remake/World Remaster/Capture Batch 01 Comparison Views")]
        public static void CaptureNextZoneComparisonViews()
        {
            string outputDirectory = Path.GetFullPath(WorldRemasterPaths.NextZoneVisualCaptureRoot);
            Directory.CreateDirectory(outputDirectory);
            var scene = EditorSceneManager.OpenScene(WorldRemasterPaths.NextZoneComparisonScene, OpenSceneMode.Single);
            WorldRemasterModeController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldRemasterModeController>(true))
                .FirstOrDefault();
            Camera[] cameras = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .OrderBy(camera => camera.name, StringComparer.Ordinal)
                .ToArray();
            if (controller == null || cameras.Length != 3)
            {
                throw new InvalidOperationException("Batch 01 comparison scene requires a mode controller and exactly three review cameras.");
            }

            var manifest = new StringBuilder();
            manifest.AppendLine("view,mode,file,width,height,camera_position,camera_rotation,fov,unity_version,builder_version");
            foreach (Camera camera in cameras)
            {
                string view = camera.name.StartsWith("Hedge", StringComparison.Ordinal)
                    ? "Hedge"
                    : camera.name.StartsWith("Seam", StringComparison.Ordinal)
                        ? "Seam"
                        : "Pier";
                foreach (WorldComparisonMode mode in new[]
                         {
                             WorldComparisonMode.ReferenceOnly,
                             WorldComparisonMode.ProductionOnly,
                             WorldComparisonMode.OverlayComparison
                         })
                {
                    controller.ApplyMode(mode);
                    string fileName = view + "_" + mode + ".png";
                    Capture(camera, Path.Combine(outputDirectory, fileName));
                    manifest.Append(view).Append(',')
                        .Append(mode).Append(',')
                        .Append(fileName).Append(',')
                        .Append(Width).Append(',')
                        .Append(Height).Append(',')
                        .Append(Quote(Vector(camera.transform.position))).Append(',')
                        .Append(Quote(Vector(camera.transform.eulerAngles))).Append(',')
                        .Append(camera.fieldOfView.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                        .Append(Application.unityVersion).Append(',')
                        .AppendLine(WorldRemasterPaths.BuilderVersion);
                }
            }

            File.WriteAllText(Path.Combine(outputDirectory, "capture_manifest.csv"), manifest.ToString(), new UTF8Encoding(false));
            Debug.Log("WORLD_REMASTER_05A_BATCH01_CAPTURE_OK output=" + outputDirectory);
        }

        public static void RunBatch() => CaptureNextZoneComparisonViews();

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };
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

        private static string Vector(Vector3 value) =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.###} {1:0.###} {2:0.###}",
                value.x,
                value.y,
                value.z);

        private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
