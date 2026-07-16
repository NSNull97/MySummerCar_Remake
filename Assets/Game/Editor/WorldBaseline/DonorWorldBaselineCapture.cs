using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.LegacyImport;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.WorldBaseline
{
    public static class DonorWorldBaselineCapture
    {
        private const int Width = 1600;
        private const int Height = 1000;

        private static readonly Vector3 HomeShorelineTarget =
            new Vector3(160f, 0f, -980f);
        private static readonly Vector3 TeimoTownTarget =
            new Vector3(-1382f, 0f, 145f);

        [MenuItem(
            "Tools/My Summer Car/Milestone 06B1/" +
            "Capture Canonical Baseline Review Views")]
        public static void CaptureReviewViews()
        {
            DonorWorldBaselineBuilder.OpenCanonicalScene();
            DonorWorldBaselineSceneMetadata sceneMetadata =
                Resources.FindObjectsOfTypeAll<DonorWorldBaselineSceneMetadata>()
                    .SingleOrDefault(value => value.gameObject.scene.IsValid());
            if (sceneMetadata == null)
            {
                throw new InvalidOperationException(
                    "Canonical donor baseline scene metadata is unavailable.");
            }

            DonorWorldBaselineEntityMetadata[] entities =
                Resources.FindObjectsOfTypeAll<DonorWorldBaselineEntityMetadata>()
                    .Where(value => value.gameObject.scene.IsValid())
                    .ToArray();
            Renderer[] activeRenderers = entities
                .Where(value =>
                    value.HasSanitizedRenderer &&
                    value.gameObject.activeInHierarchy)
                .Select(value => value.GetComponent<Renderer>())
                .Where(value => value != null && value.enabled)
                .ToArray();
            if (activeRenderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "Canonical donor baseline has no active renderers to capture.");
            }

            string output = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaselinePaths.CaptureRoot);
            Directory.CreateDirectory(output);
            var manifest = new StringBuilder(
                "view,file,target_x,target_y,target_z," +
                "semantic_fingerprint,payload_fingerprint,unity_version\n");

            var cameraObject = new GameObject(
                "M06B1_CaptureOnly_Camera",
                typeof(Camera))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f, 1f);
            camera.allowHDR = true;
            camera.allowMSAA = false;

            Dictionary<Renderer, bool> originalVisibility =
                entities
                    .Select(value => value.GetComponent<Renderer>())
                    .Where(value => value != null)
                    .Distinct()
                    .ToDictionary(value => value, value => value.enabled);
            try
            {
                Bounds fullBounds = activeRenderers[0].bounds;
                for (int index = 1; index < activeRenderers.Length; index++)
                {
                    fullBounds.Encapsulate(activeRenderers[index].bounds);
                }

                SetAllOriginalVisibility(originalVisibility);
                ConfigureOverhead(camera, fullBounds);
                CaptureAndRecord(
                    camera,
                    output,
                    "FullMap_Overhead.png",
                    "FullMapOverhead",
                    fullBounds.center,
                    sceneMetadata.SemanticFingerprintSha256,
                    manifest);

                SetStructuralVisibility(entities, originalVisibility);
                ConfigureAreaOverhead(
                    camera, HomeShorelineTarget, orthographicSize: 340f);
                CaptureAndRecord(
                    camera,
                    output,
                    "HomeShoreline_Structural.png",
                    "HomeShorelineStructural",
                    HomeShorelineTarget,
                    sceneMetadata.SemanticFingerprintSha256,
                    manifest);

                ConfigureAreaOblique(
                    camera,
                    HomeShorelineTarget,
                    new Vector3(290f, 220f, -290f));
                CaptureAndRecord(
                    camera,
                    output,
                    "HomeShoreline_Oblique.png",
                    "HomeShorelineOblique",
                    HomeShorelineTarget,
                    sceneMetadata.SemanticFingerprintSha256,
                    manifest);

                ConfigureAreaOverhead(
                    camera, TeimoTownTarget, orthographicSize: 260f);
                CaptureAndRecord(
                    camera,
                    output,
                    "TeimoTown_Structural.png",
                    "TeimoTownStructural",
                    TeimoTownTarget,
                    sceneMetadata.SemanticFingerprintSha256,
                    manifest);
            }
            finally
            {
                foreach (KeyValuePair<Renderer, bool> item in originalVisibility)
                {
                    if (item.Key != null)
                    {
                        item.Key.enabled = item.Value;
                    }
                }
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            File.WriteAllText(
                Path.Combine(output, "capture_manifest.csv"),
                manifest.ToString(),
                new UTF8Encoding(false));
            Debug.Log(
                "DONOR_WORLD_BASELINE_CAPTURE_OK output=" + output);
        }

        public static void RunBatch() => CaptureReviewViews();

        private static void SetAllOriginalVisibility(
            IReadOnlyDictionary<Renderer, bool> originalVisibility)
        {
            foreach (KeyValuePair<Renderer, bool> item in originalVisibility)
            {
                item.Key.enabled = item.Value;
            }
        }

        private static void SetStructuralVisibility(
            IEnumerable<DonorWorldBaselineEntityMetadata> entities,
            IReadOnlyDictionary<Renderer, bool> originalVisibility)
        {
            foreach (DonorWorldBaselineEntityMetadata entity in entities)
            {
                Renderer renderer = entity.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled =
                    originalVisibility[renderer] &&
                    !entity.SemanticCategory.StartsWith(
                        "Vegetation",
                        StringComparison.Ordinal);
            }
        }

        private static void ConfigureOverhead(Camera camera, Bounds bounds)
        {
            camera.orthographic = true;
            camera.aspect = Width / (float)Height;
            camera.orthographicSize = Mathf.Max(
                bounds.extents.z * 1.08f,
                bounds.extents.x / camera.aspect * 1.08f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20000f;
            camera.transform.position =
                bounds.center + Vector3.up * (bounds.extents.y + 2500f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void ConfigureAreaOverhead(
            Camera camera,
            Vector3 target,
            float orthographicSize)
        {
            camera.orthographic = true;
            camera.aspect = Width / (float)Height;
            camera.orthographicSize = orthographicSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 5000f;
            camera.transform.position = target + Vector3.up * 1200f;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void ConfigureAreaOblique(
            Camera camera,
            Vector3 target,
            Vector3 offset)
        {
            camera.orthographic = false;
            camera.aspect = Width / (float)Height;
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 8000f;
            camera.transform.position = target + offset;
            camera.transform.LookAt(target + Vector3.up * 10f);
        }

        private static void CaptureAndRecord(
            Camera camera,
            string output,
            string fileName,
            string view,
            Vector3 target,
            string semanticFingerprint,
            StringBuilder manifest)
        {
            Capture(camera, Path.Combine(output, fileName));
            manifest.Append(view).Append(',')
                .Append(fileName).Append(',')
                .Append(target.x.ToString(
                    "0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(target.y.ToString(
                    "0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(target.z.ToString(
                    "0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(semanticFingerprint).Append(',')
                .Append(
                    DonorWorldBaselineManifest.Read()
                        .runtimePayloadFingerprintSha256).Append(',')
                .AppendLine(Application.unityVersion);
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(
                Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };
            var image = new Texture2D(
                Width, Height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(
                    new Rect(0f, 0f, Width, Height), 0, 0, false);
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
    }
}
