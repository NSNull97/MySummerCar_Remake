using UnityEngine;
using MSC.Core.Lifecycle;

namespace MSC.Player
{
    [DisallowMultipleComponent]
    public sealed class CrossdotPresenter : MonoBehaviour, IUiVisibilityGate
    {
        [SerializeField]
        private bool visible = true;

        [SerializeField]
        private PlayerInteractionController interactionController;

        [SerializeField]
        private bool showOnlyWithCandidate = true;

        [SerializeField]
        [Min(1f)]
        private float dotDiameterPixels = 4f;

        [SerializeField]
        [Min(0f)]
        private float outlineWidthPixels = 1f;

        [SerializeField]
        private Color dotColor = Color.white;

        [SerializeField]
        private Color outlineColor = new Color(0f, 0f, 0f, 0.85f);

        private Texture2D crossdotTexture;

        private bool uiSuppressed;

        public bool Visible => visible;

        public bool IsUiSuppressed => uiSuppressed;

        public float DotDiameterPixels => dotDiameterPixels;

        public float OutlineWidthPixels => outlineWidthPixels;

        public void Configure(
            float diameterPixels,
            float outlinePixels,
            Color fillColor,
            Color borderColor)
        {
            dotDiameterPixels = Mathf.Max(1f, diameterPixels);
            outlineWidthPixels = Mathf.Max(0f, outlinePixels);
            dotColor = fillColor;
            outlineColor = borderColor;
            RebuildTexture();
        }

        public void SetVisible(bool value)
        {
            visible = value;
        }

        public void SetUiSuppressed(bool suppressed)
        {
            uiSuppressed = suppressed;
        }

        public static Rect CalculateCenteredRect(
            float screenWidth,
            float screenHeight,
            float sizePixels)
        {
            float clampedSize = Mathf.Max(1f, sizePixels);
            return new Rect(
                (screenWidth - clampedSize) * 0.5f,
                (screenHeight - clampedSize) * 0.5f,
                clampedSize,
                clampedSize);
        }

        private void OnEnable()
        {
            if (interactionController == null)
            {
                interactionController = GetComponent<PlayerInteractionController>();
            }

            RebuildTexture();
        }

        private void OnDisable()
        {
            ReleaseTexture();
        }

        private void OnValidate()
        {
            dotDiameterPixels = Mathf.Max(1f, dotDiameterPixels);
            outlineWidthPixels = Mathf.Max(0f, outlineWidthPixels);
            if (isActiveAndEnabled)
            {
                RebuildTexture();
            }
        }

        private void OnGUI()
        {
            if (!visible || uiSuppressed || crossdotTexture == null ||
                (showOnlyWithCandidate &&
                 (interactionController == null || !interactionController.HasCandidate)) ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            Rect rect = CalculateCenteredRect(
                Screen.width,
                Screen.height,
                crossdotTexture.width);
            GUI.DrawTexture(rect, crossdotTexture, ScaleMode.StretchToFill, alphaBlend: true);
        }

        private void RebuildTexture()
        {
            ReleaseTexture();

            int dotPixels = Mathf.Max(1, Mathf.RoundToInt(dotDiameterPixels));
            int outlinePixels = Mathf.Max(0, Mathf.RoundToInt(outlineWidthPixels));
            int textureSize = dotPixels + (outlinePixels * 2);
            float center = (textureSize - 1) * 0.5f;
            float innerRadiusSquared = dotPixels * dotPixels * 0.25f;
            float outerRadiusSquared = textureSize * textureSize * 0.25f;
            var pixels = new Color32[textureSize * textureSize];
            Color32 fill = dotColor;
            Color32 border = outlineColor;
            Color32 clear = new Color32(0, 0, 0, 0);

            for (int y = 0; y < textureSize; y++)
            {
                float offsetY = y - center;
                for (int x = 0; x < textureSize; x++)
                {
                    float offsetX = x - center;
                    float distanceSquared = (offsetX * offsetX) + (offsetY * offsetY);
                    pixels[(y * textureSize) + x] = distanceSquared <= innerRadiusSquared
                        ? fill
                        : distanceSquared <= outerRadiusSquared
                            ? border
                            : clear;
                }
            }

            crossdotTexture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                mipChain: false)
            {
                name = "RuntimeCrossdot",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            crossdotTexture.SetPixels32(pixels);
            crossdotTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        }

        private void ReleaseTexture()
        {
            if (crossdotTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(crossdotTexture);
            }
            else
            {
                DestroyImmediate(crossdotTexture);
            }

            crossdotTexture = null;
        }
    }
}
