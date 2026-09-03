using System.Collections;
using System.IO;
using EPOOutline;
using MSC.Interaction.Prototype;
using MSC.Interaction.Query;
using MSC.Player;
using MSC.Presentation.InteractionOutline.EPO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.PlayerInteraction
{
    public sealed class InteractionOutlineVisualPlayModeTests
    {
        private const int CaptureWidth = 640;
        private const int CaptureHeight = 360;

        [UnityTest]
        public IEnumerator EpoHdrpOutlineRendersWhiteWithoutErrorMagenta()
        {
            var cameraObject = new GameObject("Outline Test Camera");
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var passObject = new GameObject("Outline Test Custom Pass");
            Material targetMaterial = null;
            RenderTexture targetTexture = null;
            Texture2D capture = null;

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.transform.position = new Vector3(0f, 0f, -5f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 50f;
                camera.allowHDR = true;
                cameraObject.AddComponent<HDAdditionalCameraData>();

                targetTexture = new RenderTexture(
                    CaptureWidth,
                    CaptureHeight,
                    24,
                    RenderTextureFormat.ARGB32)
                {
                    name = "Interaction Outline Visual Test"
                };
                targetTexture.Create();
                camera.targetTexture = targetTexture;

                HdrpOutliner outliner =
                    cameraObject.AddComponent<HdrpOutliner>();
                outliner.PrimaryBufferSizeMode = BufferSizeMode.Native;
                outliner.PrimaryRendererScale = 1f;
                outliner.DilateIterations = 1;
                outliner.DilateQuality = DilateQuality.Base;
                outliner.DilateShift = 0.75f;
                outliner.BlurIterations = 0;
                outliner.BlurShift = 0f;
                outliner.RenderStage = RenderStage.AfterTransparents;

                passObject.transform.SetParent(
                    cameraObject.transform,
                    worldPositionStays: false);
                CustomPassVolume passVolume =
                    passObject.AddComponent<CustomPassVolume>();
                passVolume.isGlobal = true;
                passVolume.targetCamera = camera;
                passVolume.injectionPoint =
                    CustomPassInjectionPoint.BeforePostProcess;
                passVolume.AddPassOfType<OutlineCustomPass>().name =
                    "EPO Interaction Outline Test";

                target.transform.position = Vector3.zero;
                target.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
                Shader unlitShader = Shader.Find("HDRP/Unlit");
                Assert.That(unlitShader, Is.Not.Null);
                targetMaterial = new Material(unlitShader);
                SetUnlitColor(targetMaterial, new Color(0.12f, 0.12f, 0.12f));
                target.GetComponent<Renderer>().sharedMaterial = targetMaterial;

                ContextToggleTarget capability =
                    target.AddComponent<ContextToggleTarget>();
                InteractionTargetHost host =
                    target.AddComponent<InteractionTargetHost>();
                host.Configure(capability);

                InteractionOutlinePresenter presenter =
                    cameraObject.AddComponent<InteractionOutlinePresenter>();
                presenter.Configure(null, Color.white, 3f);
                // This fixture feeds an explicit candidate. Keep the normal
                // controller-driven Update from clearing it on the next frame.
                presenter.enabled = false;
                Outlinable outlinable =
                    cameraObject.AddComponent<Outlinable>();
                EpoInteractionOutlineAdapter adapter =
                    cameraObject.AddComponent<EpoInteractionOutlineAdapter>();
                adapter.Configure(presenter, outlinable);
                presenter.Present(
                    new InteractionCandidate(
                        host,
                        Vector3.zero,
                        Vector3.back,
                        5f,
                        target.GetComponent<Collider>()),
                    actionable: true);
                adapter.Synchronize();
                adapter.Advance(EpoInteractionOutlineAdapter.ReferenceGrowSeconds);

                // WaitForEndOfFrame is not invoked by Unity batchmode. A
                // target-texture camera is rendered during ordinary updates.
                yield return null;
                yield return null;
                yield return null;

                capture = new Texture2D(
                    CaptureWidth,
                    CaptureHeight,
                    TextureFormat.RGBA32,
                    mipChain: false,
                    linear: false);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = targetTexture;
                capture.ReadPixels(
                    new Rect(0, 0, CaptureWidth, CaptureHeight),
                    0,
                    0,
                    recalculateMipMaps: false);
                capture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                RenderTexture.active = previous;

                Color32[] pixels = capture.GetPixels32();
                int whitePixels = 0;
                int errorMagentaPixels = 0;
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color32 pixel = pixels[index];
                    int maximum = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                    int minimum = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                    if (minimum >= 150 && maximum - minimum <= 24)
                    {
                        whitePixels++;
                    }

                    if (pixel.r >= 200 && pixel.b >= 200 && pixel.g <= 80)
                    {
                        errorMagentaPixels++;
                    }
                }

                string artifactDirectory = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "../TestResults"));
                Directory.CreateDirectory(artifactDirectory);
                File.WriteAllBytes(
                    Path.Combine(
                        artifactDirectory,
                        "EPO_InteractionOutline_Visual.png"),
                    capture.EncodeToPNG());

                Assert.That(
                    errorMagentaPixels,
                    Is.Zero,
                    "The rendered frame contains shader-error magenta.");
                Assert.That(
                    whitePixels,
                    Is.GreaterThan(40),
                    "The rendered frame contains no visible white outline.");
                Assert.That(
                    whitePixels,
                    Is.LessThan(pixels.Length / 10),
                    "The outline filled too much of the target/frame.");
            }
            finally
            {
                if (targetTexture != null)
                {
                    targetTexture.Release();
                }

                Object.DestroyImmediate(capture);
                Object.DestroyImmediate(targetMaterial);
                Object.DestroyImmediate(passObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(targetTexture);
            }
        }

        private static void SetUnlitColor(Material material, Color color)
        {
            if (material.HasProperty("_UnlitColor"))
            {
                material.SetColor("_UnlitColor", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
        }
    }
}
