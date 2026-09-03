using System;
using UnityEngine;

namespace MSC.Items
{
    public static class ItemPaintStateIds
    {
        public const string ColorRed = "paint-color-r";
        public const string ColorGreen = "paint-color-g";
        public const string ColorBlue = "paint-color-b";
        public const string Applied = "paint-applied";
        public const string Matte = "paint-matte";
    }
}

namespace MSC.Items.Presentation
{
    /// <summary>
    /// Project-owned paint binding for the temporary helmet wrapper. The
    /// mutable color lives on WorldItemInstance, not on the donor material.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelmetPaintPresentation : MonoBehaviour
    {
        private static readonly int BaseColorProperty =
            Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessProperty =
            Shader.PropertyToID("_Smoothness");

        [SerializeField] private Renderer paintSurface;

        private MaterialPropertyBlock propertyBlock;
        private WorldItemInstance owner;

        public Renderer PaintSurface => paintSurface;

        public void Bind(WorldItemInstance configuredOwner)
        {
            if (owner != null)
            {
                owner.StatusChanged -= HandleStatusChanged;
            }

            owner = configuredOwner ?? throw new ArgumentNullException(
                nameof(configuredOwner));
            owner.StatusChanged += HandleStatusChanged;
            Refresh();
        }

        public bool TryApplyPaint(Color color, bool matte) =>
            owner != null && owner.TryApplyPaint(color, matte);

#if UNITY_EDITOR
        public void ConfigureForAuthoring(Renderer configuredSurface)
        {
            paintSurface = configuredSurface;
        }
#endif

        private void HandleStatusChanged(IItemStatusSource _)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (paintSurface == null || owner == null ||
                !owner.TryGetPaint(
                    out Color color,
                    out bool matte,
                    out bool applied))
            {
                return;
            }

            if (!applied)
            {
                paintSurface.SetPropertyBlock(null);
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorProperty, color);
            propertyBlock.SetFloat(SmoothnessProperty, matte ? 0.08f : 0.82f);
            paintSurface.SetPropertyBlock(propertyBlock);
        }

        private void OnDestroy()
        {
            if (owner != null)
            {
                owner.StatusChanged -= HandleStatusChanged;
            }
        }
    }
}
