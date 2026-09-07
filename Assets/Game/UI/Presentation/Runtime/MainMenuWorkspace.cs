using System;
using MSC.Presentation.AntiAliasing;
using UnityEngine;

namespace MSC.UI.Presentation
{
    /// <summary>
    /// Keeps the front end centred, extending its working width on ultrawide
    /// displays up to 24:9. The existing CanvasScaler still owns pixel density. Safe-area
    /// fitting is local to MainMenu and does not move the accepted settings/HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuWorkspace : MonoBehaviour
    {
        private readonly Vector3[] parentCorners = new Vector3[4];
        private RectTransform workspace;
        private RectTransform parentRect;
        private CanvasGroup visibility;
        private Vector3 lastParentScale;
        private Rect lastSafeArea;
        private int lastWidth;
        private int lastHeight;
        private float elapsed;
        private bool reducedMotion;
        private IDisposable debugOverlaySuppression;

        public void Initialize(bool preferReducedMotion)
        {
            workspace = (RectTransform)transform;
            parentRect = (RectTransform)workspace.parent;
            visibility = gameObject.AddComponent<CanvasGroup>();
            reducedMotion = preferReducedMotion;
            RefreshGeometry();
        }

        private void OnEnable()
        {
            if (debugOverlaySuppression == null &&
                AntiAliasingController.TryGetInstance(out AntiAliasingController antiAliasing))
            {
                debugOverlaySuppression = antiAliasing.SuppressDebugOverlay();
            }

            elapsed = 0f;
            if (visibility != null)
            {
                visibility.alpha = reducedMotion ? 1f : 0f;
                RefreshGeometry();
            }
        }

        private void OnDisable()
        {
            debugOverlaySuppression?.Dispose();
            debugOverlaySuppression = null;
        }

        private void Update()
        {
            if (visibility == null || visibility.alpha >= 1f)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / MainMenuStyle.AppearanceDurationSeconds);
            visibility.alpha = progress * progress * (3f - 2f * progress);
        }

        private void LateUpdate()
        {
            if (parentRect == null ||
                lastWidth == Screen.width && lastHeight == Screen.height &&
                lastSafeArea == Screen.safeArea && lastParentScale == parentRect.lossyScale)
            {
                return;
            }

            RefreshGeometry();
        }

        private void RefreshGeometry()
        {
            if (parentRect == null)
            {
                return;
            }

            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
            lastParentScale = parentRect.lossyScale;
            parentRect.GetWorldCorners(parentCorners);
            float width = parentCorners[2].x - parentCorners[0].x;
            float height = parentCorners[2].y - parentCorners[0].y;
            if (width <= 0f || height <= 0f || lastSafeArea.width <= 0f || lastSafeArea.height <= 0f)
            {
                return;
            }

            // Widen only the main-menu working area. Controls retain their
            // existing logical sizes; a 32:9 display keeps broad side margins.
            float widthFactor = Mathf.Clamp(
                (lastSafeArea.width / lastSafeArea.height) / (16f / 9f), 1f, 1.5f);
            float targetWidth = UiThemeTokens.ReferenceWidth * widthFactor;
            workspace.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
            float requiredWidth = width * targetWidth / parentRect.rect.width;
            float requiredHeight = height * UiThemeTokens.ReferenceHeight / parentRect.rect.height;
            float fit = Mathf.Min(1f, Mathf.Min(
                lastSafeArea.width / requiredWidth, lastSafeArea.height / requiredHeight));
            workspace.localScale = new Vector3(fit, fit, 1f);
            Vector3 safeCenter = new Vector3(lastSafeArea.center.x, lastSafeArea.center.y, 0f);
            Vector3 parentCenter = (parentCorners[0] + parentCorners[2]) * 0.5f;
            workspace.anchoredPosition = parentRect.InverseTransformVector(safeCenter - parentCenter);
        }
    }
}
