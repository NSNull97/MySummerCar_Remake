using System;
using System.IO;
using System.Linq;
using MSC.World.Debugging;
using MSC.World.Remaster;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.WorldTransfer
{
    /// <summary>
    /// Produces deterministic reference views used to author the bounded 05C1
    /// Teimo void-fill pilot. The captures are evidence only and never become
    /// a runtime dependency.
    /// </summary>
    public static class WorldContinuousGroundBaselineCapture
    {
        private const int Width = 1600;
        private const int Height = 1200;
        private const float OrthographicSize = 180f;

        private static readonly Vector3 TeimoTarget = new(-1382f, 6f, 145f);

        [MenuItem("Tools/MSC Remake/World Transfer/05C1/Capture Teimo Void Evidence")]
        public static void CaptureTeimoEvidence()
        {
            WorldPartitionBuilder.OpenReferenceOverview();
            Camera camera = Resources.FindObjectsOfTypeAll<Camera>()
                .FirstOrDefault(value => value.gameObject.scene.IsValid());
            if (camera == null)
            {
                throw new InvalidOperationException("05C reference overview camera is unavailable.");
            }

            string output = WorldTransferPaths.ToAbsoluteProjectPath(
                "PerformanceCaptures/Milestone05C1");
            Directory.CreateDirectory(output);

            ConfigureTopDown(camera);
            SetVisibility(static _ => true);
            Capture(camera, Path.Combine(output, "Teimo_AllReference.png"));

            SetVisibility(entity =>
                entity.VisualizationKind == WorldReferenceVisualizationKind.ActualMesh &&
                !entity.SemanticCategory.StartsWith("Vegetation", StringComparison.Ordinal));
            Capture(camera, Path.Combine(output, "Teimo_SurfacesWithoutVegetation.png"));

            SetVisibility(entity =>
                entity.VisualizationKind == WorldReferenceVisualizationKind.ActualMesh &&
                entity.SemanticCategory.StartsWith("Vegetation", StringComparison.Ordinal));
            Capture(camera, Path.Combine(output, "Teimo_BoundaryVegetation.png"));

            SetVisibility(static _ => true);
            Debug.Log("WORLD_MAP_05C1_CAPTURE_OK output=" + output);
        }

        public static void RunBatch() => CaptureTeimoEvidence();

        [MenuItem("Tools/MSC Remake/World Transfer/05C1/Capture Teimo Reference + Fill Review")]
        public static void CaptureTeimoFillReview()
        {
            WorldContinuousGroundBaselineBuilder.OpenReview();
            Camera camera = Resources.FindObjectsOfTypeAll<Camera>()
                .FirstOrDefault(value => value.gameObject.scene.IsValid());
            if (camera == null)
            {
                throw new InvalidOperationException("05C1 review camera is unavailable.");
            }

            string output = WorldTransferPaths.ToAbsoluteProjectPath(
                "PerformanceCaptures/Milestone05C1");
            Directory.CreateDirectory(output);
            ConfigureTopDown(camera);
            camera.transform.position = new Vector3(-1585f, 456f, 160f);

            SetVisibility(entity =>
                entity.VisualizationKind == WorldReferenceVisualizationKind.ActualMesh &&
                !entity.SemanticCategory.StartsWith("Vegetation", StringComparison.Ordinal));

            Renderer[] fillRenderers = Resources.FindObjectsOfTypeAll<WorldVoidFillMarker>()
                .Where(value => value.gameObject.scene.IsValid())
                .Select(value => value.GetComponent<Renderer>())
                .Where(value => value != null)
                .ToArray();
            if (fillRenderers.Length != 2)
            {
                throw new InvalidOperationException($"05C1 review expected two fill renderers; found {fillRenderers.Length}.");
            }

            Shader debugShader = Shader.Find("HDRP/Unlit");
            if (debugShader == null)
            {
                throw new InvalidOperationException("HDRP/Unlit shader is unavailable for the 05C1 review capture.");
            }

            var debugMaterial = new Material(debugShader)
            {
                name = "M05C1_CaptureOnly_VoidFill",
                hideFlags = HideFlags.HideAndDontSave
            };
            var debugColor = new Color(0.95f, 0.12f, 0.015f, 1f);
            if (debugMaterial.HasProperty("_UnlitColor"))
            {
                debugMaterial.SetColor("_UnlitColor", debugColor);
            }

            if (debugMaterial.HasProperty("_BaseColor"))
            {
                debugMaterial.SetColor("_BaseColor", debugColor);
            }

            Material[] originalMaterials = fillRenderers
                .Select(renderer => renderer.sharedMaterial)
                .ToArray();
            try
            {
                foreach (Renderer renderer in fillRenderers)
                {
                    renderer.enabled = true;
                    renderer.sharedMaterial = debugMaterial;
                }

                Capture(camera, Path.Combine(output, "Teimo_ReferencePlusFill.png"));

                SetVisibility(static _ => false);
                Capture(camera, Path.Combine(output, "Teimo_FillOnly.png"));
            }
            finally
            {
                for (int index = 0; index < fillRenderers.Length; index++)
                {
                    fillRenderers[index].sharedMaterial = originalMaterials[index];
                }

                UnityEngine.Object.DestroyImmediate(debugMaterial);
                SetVisibility(static _ => true);
            }

            Debug.Log("WORLD_MAP_05C1_REVIEW_CAPTURE_OK output=" + output);
        }

        public static void RunReviewBatch() => CaptureTeimoFillReview();

        private static void ConfigureTopDown(Camera camera)
        {
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.aspect = Width / (float)Height;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.035f);
            camera.transform.position = TeimoTarget + Vector3.up * 450f;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void SetVisibility(Func<WorldReferenceEntity, bool> predicate)
        {
            foreach (WorldReferenceEntity entity in Resources.FindObjectsOfTypeAll<WorldReferenceEntity>())
            {
                if (!entity.gameObject.scene.IsValid())
                {
                    continue;
                }

                Renderer renderer = entity.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = predicate(entity);
                }
            }
        }

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
    }
}
