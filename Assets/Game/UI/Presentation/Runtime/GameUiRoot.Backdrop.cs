using System.Collections;
using System.Collections.Generic;
using MSC.UI.Runtime.Routing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    public sealed partial class GameUiRoot
    {
        // Static glass is filtered at the reference plate resolution. Pause
        // capture starts at a denser resolution before its one-time prefilter,
        // so camera-edge aliasing is not baked into the final glass texture.
        private const int MenuBlurWidth = 1672;
        private const int MenuBlurHeight = 941;
        private const int DynamicCaptureWidth = 1024;
        private const int DynamicCaptureHeight = 576;
        private const int DynamicBlurWidth = 512;
        private const int DynamicBlurHeight = 288;
        private const int GaussianIterations = 4;
        private const float GaussianRadiusStart = 2f;
        private const float GaussianRadiusStep = 2f;
        private static readonly int BlurDirectionId =
            Shader.PropertyToID("_BlurDirection");
        private static readonly int BlurRadiusId =
            Shader.PropertyToID("_BlurRadius");
        private readonly List<UiGlassSurface> glassSurfaces =
            new List<UiGlassSurface>();

        private Image menuBackdropFallback;
        private RawImage menuBackdropImage;
        private RawImage pauseBackdropImage;
        private Image pauseBackdropDim;
        private RenderTexture menuBlurredTexture;
        private RenderTexture dynamicCaptureTexture;
        private RenderTexture dynamicBlurredTexture;
        private Material gaussianBlurMaterial;
        private Coroutine dynamicCaptureCoroutine;
        private UiBackdropMode activeBackdropMode;
        private bool dynamicCaptureWarningIssued;
        private bool dynamicCapturePerfLogged;

        public string ActiveBackdropModeName => activeBackdropMode.ToString();

        public int RegisteredGlassSurfaceCount => glassSurfaces.Count;

        private RectTransform CreateBackdropLayer(Transform parent)
        {
            GameObject layer = factory.CreateObject("BackdropLayer", parent);
            factory.Stretch(layer);

            GameObject fallback = factory.CreateObject("MenuBackdropFallback", layer.transform);
            factory.Stretch(fallback);
            menuBackdropFallback = factory.AddSurface(
                fallback,
                UiThemeTokens.MenuBackdropFallback,
                rounded: false);
            menuBackdropFallback.raycastTarget = false;

            GameObject aspectFrame = factory.CreateObject(
                "BackdropAspectFrame_1672x941",
                layer.transform);
            RectTransform aspectFrameRect = factory.Stretch(aspectFrame);
            var aspectFitter = aspectFrame.AddComponent<AspectRatioFitter>();
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspectFitter.aspectRatio = UiThemeTokens.ReferenceAspect;

            GameObject menu = factory.CreateObject("StaticMenuBackdrop", aspectFrame.transform);
            factory.Stretch(menu);
            menuBackdropImage = menu.AddComponent<RawImage>();
            menuBackdropImage.texture = dependencies.MenuBackdropTexture;
            menuBackdropImage.color = Color.white;
            menuBackdropImage.raycastTarget = false;

            GameObject pause = factory.CreateObject("FrozenPauseBackdrop", aspectFrame.transform);
            factory.Stretch(pause);
            pauseBackdropImage = pause.AddComponent<RawImage>();
            pauseBackdropImage.color = Color.white;
            pauseBackdropImage.raycastTarget = false;

            GameObject dim = factory.CreateObject("PauseBackdropDim", layer.transform);
            factory.Stretch(dim);
            pauseBackdropDim = factory.AddSurface(
                dim,
                UiThemeTokens.PauseBackdropDim,
                rounded: false);
            pauseBackdropDim.raycastTarget = false;

            menuBackdropFallback.enabled = false;
            menuBackdropImage.enabled = false;
            pauseBackdropImage.enabled = false;
            pauseBackdropDim.enabled = false;
            PrepareMenuBlurredTexture();
            return aspectFrameRect;
        }

        private void RegisterGlassSurface(UiGlassSurface surface)
        {
            if (surface == null)
            {
                return;
            }

            glassSurfaces.Add(surface);
            ApplyGlassSurface(surface);
        }

        private void RefreshGlassSurfaces()
        {
            for (int index = glassSurfaces.Count - 1; index >= 0; index--)
            {
                UiGlassSurface surface = glassSurfaces[index];
                if (surface == null)
                {
                    glassSurfaces.RemoveAt(index);
                    continue;
                }

                ApplyGlassSurface(surface);
            }
        }

        private void ApplyGlassSurface(UiGlassSurface surface)
        {
            if (surface.Kind == UiGlassKind.HudDark)
            {
                // Live camera capture was visibly stuttering during look input.
                // HUD readability uses a stable dark translucent surface; the
                // camera is captured only once for the frozen pause backdrop.
                surface.Apply(null, UiThemeTokens.HudGlassTint);
                return;
            }

            Texture texture = activeBackdropMode == UiBackdropMode.PauseFrozen
                ? dynamicBlurredTexture
                : menuBlurredTexture;
            Color tint = activeBackdropMode == UiBackdropMode.PauseFrozen
                ? UiThemeTokens.MenuGlassNeutral
                : CurrentMenuGlassTint;
            surface.Apply(texture, tint);
        }

        private void UpdateBlurredBackdrop(UiRouteId route)
        {
            ApplyBackdropMode(ResolveBackdropMode(route));
        }

        private UiBackdropMode ResolveBackdropMode(UiRouteId route)
        {
            if (route == UiRouteId.InGameHud)
            {
                return UiBackdropMode.HudLiveGlass;
            }

            if (route == UiRouteId.Pause ||
                (IsSettingsRoute(route) && settingsReturnRoute == UiRouteId.Pause) ||
                (route == UiRouteId.ConfirmationDialog && confirmationReturnRoute == UiRouteId.Pause) ||
                (route == UiRouteId.SaveStatus && saveStatusReturnRoute == UiRouteId.Pause))
            {
                return UiBackdropMode.PauseFrozen;
            }

            return UiBackdropMode.MenuStatic;
        }

        private void ApplyBackdropMode(UiBackdropMode mode)
        {
            activeBackdropMode = mode;
            bool showMenu = mode == UiBackdropMode.MenuStatic;
            bool showPause = mode == UiBackdropMode.PauseFrozen;

            if (menuBackdropFallback != null)
            {
                menuBackdropFallback.enabled = showMenu;
            }

            if (menuBackdropImage != null)
            {
                menuBackdropImage.enabled = showMenu && dependencies.MenuBackdropTexture != null;
            }

            if (pauseBackdropDim != null)
            {
                pauseBackdropDim.enabled = showPause;
            }

            if (showMenu)
            {
                StopDynamicCapture(releaseTextures: true);
                if (pauseBackdropImage != null)
                {
                    pauseBackdropImage.enabled = false;
                    pauseBackdropImage.texture = null;
                }
            }
            else if (mode == UiBackdropMode.HudLiveGlass)
            {
                StopDynamicCapture(releaseTextures: true);
                if (pauseBackdropImage != null)
                {
                    pauseBackdropImage.enabled = false;
                    pauseBackdropImage.texture = null;
                }
            }
            else if (showPause)
            {
                StopDynamicCapture(releaseTextures: false);
                ApplyDynamicTextureToPauseBackdrop();
                if (dynamicBlurredTexture == null)
                {
                    StartPauseCapture();
                }
            }

            RefreshGlassSurfaces();
        }

        private void PrepareMenuBlurredTexture()
        {
            Texture source = dependencies.MenuBackdropTexture;
            if (source == null)
            {
                return;
            }

            menuBlurredTexture = CreateBackdropTexture(
                "M08A_StaticMenuGlass",
                MenuBlurWidth,
                MenuBlurHeight,
                RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear);
            ApplyGaussianBlur(source, menuBlurredTexture);
        }

        private void StartPauseCapture()
        {
            if (dynamicCaptureCoroutine != null ||
                dependencies?.BackdropCamera == null ||
                !dependencies.IsWorldReady())
            {
                return;
            }

            EnsureDynamicTextures();
            dynamicCaptureCoroutine = StartCoroutine(
                CapturePauseBackdrop(dependencies.BackdropCamera));
        }

        private IEnumerator CapturePauseBackdrop(Camera captureCamera)
        {
            var endOfFrame = new WaitForEndOfFrame();
            try
            {
                yield return endOfFrame;
                if (activeBackdropMode != UiBackdropMode.PauseFrozen ||
                    captureCamera == null)
                {
                    yield break;
                }

                CaptureDynamicFrame(captureCamera);
                RefreshGlassSurfaces();
                ApplyDynamicTextureToPauseBackdrop();
            }
            finally
            {
                RenderTexture.active = null;
                dynamicCaptureCoroutine = null;
            }
        }

        private void CaptureDynamicFrame(Camera captureCamera)
        {
            try
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                long allocationStart = System.GC.GetAllocatedBytesForCurrentThread();
                var captureTimer = System.Diagnostics.Stopwatch.StartNew();
#endif
                var request = new RenderPipeline.StandardRequest
                {
                    destination = dynamicCaptureTexture,
                };
                if (!RenderPipeline.SupportsRenderRequest(captureCamera, request))
                {
                    throw new System.InvalidOperationException(
                        "The active render pipeline does not support StandardRequest.");
                }

                RenderPipeline.SubmitRenderRequest(captureCamera, request);
                ApplyGaussianBlur(dynamicCaptureTexture, dynamicBlurredTexture);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!dynamicCapturePerfLogged)
                {
                    captureTimer.Stop();
                    dynamicCapturePerfLogged = true;
                    Debug.Log(
                        $"M08A UI PERF pause glass capture: " +
                        $"{captureTimer.Elapsed.TotalMilliseconds:F3} ms CPU submission; " +
                        $"thread allocations={System.GC.GetAllocatedBytesForCurrentThread() - allocationStart} bytes; " +
                        $"capture={DynamicCaptureWidth}x{DynamicCaptureHeight}; " +
                        $"glass={DynamicBlurWidth}x{DynamicBlurHeight}; mode=one-shot-gaussian.",
                        this);
                }
#endif
            }
            catch (System.Exception exception)
            {
                if (!dynamicCaptureWarningIssued)
                {
                    Debug.LogWarning(
                        "M08A pause glass capture is unavailable; the readable dimmed fallback remains active. " +
                        exception.Message,
                        this);
                    dynamicCaptureWarningIssued = true;
                }
            }
            finally
            {
                RenderTexture.active = null;
            }
        }

        private void ApplyDynamicTextureToPauseBackdrop()
        {
            if (pauseBackdropImage == null)
            {
                return;
            }

            pauseBackdropImage.texture = dynamicBlurredTexture;
            pauseBackdropImage.enabled = dynamicBlurredTexture != null;
        }

        private void EnsureDynamicTextures()
        {
            if (dynamicBlurredTexture != null)
            {
                return;
            }

            dynamicCaptureTexture = CreateBackdropTexture(
                "M08A_DynamicCapture",
                DynamicCaptureWidth,
                DynamicCaptureHeight,
                RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear);
            dynamicBlurredTexture = CreateBackdropTexture(
                "M08A_DynamicGlass",
                DynamicBlurWidth,
                DynamicBlurHeight,
                RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear);
        }

        private void StopDynamicCapture(bool releaseTextures)
        {
            if (dynamicCaptureCoroutine != null)
            {
                StopCoroutine(dynamicCaptureCoroutine);
                dynamicCaptureCoroutine = null;
            }

            if (!releaseTextures)
            {
                return;
            }

            ReleaseTexture(ref dynamicCaptureTexture);
            ReleaseTexture(ref dynamicBlurredTexture);
            RefreshGlassSurfaces();
        }

        private void DisposeBlurredBackdropTexture()
        {
            StopDynamicCapture(releaseTextures: true);
            ReleaseTexture(ref menuBlurredTexture);
            if (gaussianBlurMaterial != null)
            {
                Object.Destroy(gaussianBlurMaterial);
                gaussianBlurMaterial = null;
            }
            for (int index = glassSurfaces.Count - 1; index >= 0; index--)
            {
                if (glassSurfaces[index] != null)
                {
                    glassSurfaces[index].Apply(null, glassSurfaces[index].Tint);
                }
            }

            glassSurfaces.Clear();
        }

        private void ApplyGaussianBlur(Texture source, RenderTexture destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            Material blurMaterial = GetGaussianBlurMaterial();
            RenderTextureFormat scratchFormat =
                SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                    ? RenderTextureFormat.ARGBHalf
                    : RenderTextureFormat.ARGB32;
            RenderTextureReadWrite scratchReadWrite =
                scratchFormat == RenderTextureFormat.ARGBHalf
                    ? RenderTextureReadWrite.Linear
                    : RenderTextureReadWrite.Default;
            RenderTexture ping = RenderTexture.GetTemporary(
                destination.width,
                destination.height,
                0,
                scratchFormat,
                scratchReadWrite);
            RenderTexture pong = RenderTexture.GetTemporary(
                destination.width,
                destination.height,
                0,
                scratchFormat,
                scratchReadWrite);
            ConfigureScratchTexture(ping);
            ConfigureScratchTexture(pong);

            RenderTexture previousActive = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            try
            {
                BlitWithDestinationColorSpace(source, ping);
                for (int iteration = 0; iteration < GaussianIterations; iteration++)
                {
                    float radius = GaussianRadiusStart +
                        GaussianRadiusStep * iteration;
                    blurMaterial.SetVector(
                        BlurDirectionId,
                        new Vector4(1f, 0f, 0f, 0f));
                    blurMaterial.SetFloat(BlurRadiusId, radius);
                    BlitWithDestinationColorSpace(ping, pong, blurMaterial);

                    blurMaterial.SetVector(
                        BlurDirectionId,
                        new Vector4(0f, 1f, 0f, 0f));
                    BlitWithDestinationColorSpace(pong, ping, blurMaterial);
                }

                BlitWithDestinationColorSpace(ping, destination);
            }
            finally
            {
                GL.sRGBWrite = previousSrgbWrite;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(pong);
                RenderTexture.ReleaseTemporary(ping);
            }
        }

        private Material GetGaussianBlurMaterial()
        {
            if (gaussianBlurMaterial != null)
            {
                return gaussianBlurMaterial;
            }

            Shader shader = dependencies?.UiBlurShader;
            if (shader == null || !shader.isSupported)
            {
                throw new System.InvalidOperationException(
                    "The project-owned M08A Gaussian blur shader is missing or unsupported.");
            }

            gaussianBlurMaterial = new Material(shader)
            {
                name = "M08A UI Gaussian Blur",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return gaussianBlurMaterial;
        }

        private static void ConfigureScratchTexture(RenderTexture texture)
        {
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.useMipMap = false;
            texture.autoGenerateMips = false;
        }

        private static void BlitWithDestinationColorSpace(
            Texture source,
            RenderTexture destination,
            Material material = null)
        {
            GL.sRGBWrite = destination != null && destination.sRGB;
            if (material == null)
            {
                Graphics.Blit(source, destination);
            }
            else
            {
                Graphics.Blit(source, destination, material, 0);
            }
        }

        private static RenderTexture CreateBackdropTexture(
            string textureName,
            int width,
            int height,
            RenderTextureFormat format = RenderTextureFormat.ARGB32,
            RenderTextureReadWrite readWrite = RenderTextureReadWrite.Default)
        {
            var texture = new RenderTexture(
                width,
                height,
                0,
                format,
                readWrite)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
            };
            texture.Create();
            return texture;
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            if (RenderTexture.active == texture)
            {
                RenderTexture.active = null;
            }

            texture.Release();
            Object.Destroy(texture);
            texture = null;
        }

        private enum UiBackdropMode
        {
            MenuStatic = 0,
            HudLiveGlass = 1,
            PauseFrozen = 2,
        }
    }
}
