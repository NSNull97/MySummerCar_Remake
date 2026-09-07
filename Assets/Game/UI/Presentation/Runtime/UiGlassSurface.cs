using UnityEngine;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    internal enum UiGlassKind
    {
        MenuTinted = 0,
        HudDark = 1,
        MainMenuNeutral = 2,
    }

    /// <summary>
    /// Displays a correctly aligned slice of a shared softened backdrop inside
    /// a rounded uGUI mask. The slice is derived from actual world corners so
    /// it remains aligned under Canvas scaling, letterboxing, and UI scale.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class UiGlassSurface : MonoBehaviour
    {
        private readonly Vector3[] surfaceCorners = new Vector3[4];
        private readonly Vector3[] referenceCorners = new Vector3[4];

        private RectTransform surfaceRect;
        private RectTransform referenceFrame;
        [SerializeField] private RawImage backdropSlice;
        [SerializeField] private Image tintImage;
        [SerializeField] private UiGlassKind kind;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;
        private Matrix4x4 lastSurfaceMatrix;
        private Matrix4x4 lastReferenceMatrix;

        public UiGlassKind Kind => kind;

        public Color Tint => tintImage != null ? tintImage.color : Color.clear;

        public bool HasBackdropTexture =>
            backdropSlice != null && backdropSlice.texture != null;

        public void Initialize(
            UiGlassKind kind,
            RectTransform configuredReferenceFrame,
            RawImage configuredBackdropSlice,
            Image configuredTintImage)
        {
            this.kind = kind;
            surfaceRect = transform as RectTransform;
            referenceFrame = configuredReferenceFrame;
            backdropSlice = configuredBackdropSlice;
            tintImage = configuredTintImage;
            RefreshUv();
        }

        public void RebindSource(RectTransform configuredReferenceFrame)
        {
            surfaceRect = transform as RectTransform;
            referenceFrame = configuredReferenceFrame;
            RefreshUv();
        }

        public void Apply(Texture texture, Color tint)
        {
            if (backdropSlice != null)
            {
                backdropSlice.texture = texture;
                backdropSlice.enabled = texture != null;
            }

            if (tintImage != null)
            {
                tintImage.color = tint;
            }

            RefreshUv();
        }

        private void OnEnable()
        {
            RefreshUv();
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshUv();
        }

        private void LateUpdate()
        {
            bool moved = Kind == UiGlassKind.MainMenuNeutral &&
                surfaceRect != null && referenceFrame != null &&
                (lastSurfaceMatrix != surfaceRect.localToWorldMatrix ||
                 lastReferenceMatrix != referenceFrame.localToWorldMatrix);
            if (!moved && lastScreenWidth == Screen.width && lastScreenHeight == Screen.height)
            {
                return;
            }

            RefreshUv();
        }

        private void RefreshUv()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            if (surfaceRect == null || referenceFrame == null || backdropSlice == null)
            {
                return;
            }

            lastSurfaceMatrix = surfaceRect.localToWorldMatrix;
            lastReferenceMatrix = referenceFrame.localToWorldMatrix;

            surfaceRect.GetWorldCorners(surfaceCorners);
            referenceFrame.GetWorldCorners(referenceCorners);
            float referenceWidth = referenceCorners[2].x - referenceCorners[0].x;
            float referenceHeight = referenceCorners[2].y - referenceCorners[0].y;
            if (referenceWidth <= Mathf.Epsilon || referenceHeight <= Mathf.Epsilon)
            {
                return;
            }

            float x = (surfaceCorners[0].x - referenceCorners[0].x) / referenceWidth;
            float y = (surfaceCorners[0].y - referenceCorners[0].y) / referenceHeight;
            float width = (surfaceCorners[2].x - surfaceCorners[0].x) / referenceWidth;
            float height = (surfaceCorners[2].y - surfaceCorners[0].y) / referenceHeight;
            backdropSlice.uvRect = new Rect(x, y, width, height);
        }
    }
}
