using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Items.Presentation
{
    /// <summary>
    /// Binds flattened temporary content surfaces to authoritative item data.
    /// Empty containers no longer render donor water, ingredient, or fuel
    /// helper meshes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemContentsPresentationController : MonoBehaviour,
        IWorldItemPresentationBinding
    {
        [SerializeField] private string expectedDefinitionId = string.Empty;
        [SerializeField] private Renderer[] liquidSurfaceRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private ItemScalarSurfaceBinding[] scalarSurfaces =
            Array.Empty<ItemScalarSurfaceBinding>();

        private WorldItemInstance owner;

        public IReadOnlyList<Renderer> LiquidSurfaceRenderers =>
            liquidSurfaceRenderers ?? Array.Empty<Renderer>();
        public string ExpectedDefinitionId => expectedDefinitionId;
        public IReadOnlyList<ItemScalarSurfaceBinding> ScalarSurfaces =>
            scalarSurfaces ?? Array.Empty<ItemScalarSurfaceBinding>();

        public void Bind(WorldItemInstance configuredOwner)
        {
            if (configuredOwner == null ||
                !string.Equals(
                    configuredOwner.DefinitionId,
                    expectedDefinitionId,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (owner != null)
            {
                owner.StatusChanged -= HandleStatusChanged;
            }

            owner = configuredOwner;
            owner.StatusChanged += HandleStatusChanged;
            Refresh();
        }

        public void Refresh()
        {
            bool liquidVisible = owner != null &&
                                 owner.ContentAmount > 0.0001f;
            SetRenderersEnabled(liquidSurfaceRenderers, liquidVisible);
            if (scalarSurfaces == null)
            {
                return;
            }

            for (int index = 0; index < scalarSurfaces.Length; index++)
            {
                scalarSurfaces[index]?.Refresh(owner);
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredDefinitionId,
            Renderer[] configuredLiquidSurfaces,
            ItemScalarSurfaceBinding[] configuredScalarSurfaces)
        {
            expectedDefinitionId = configuredDefinitionId ?? string.Empty;
            liquidSurfaceRenderers = configuredLiquidSurfaces ??
                Array.Empty<Renderer>();
            scalarSurfaces = configuredScalarSurfaces ??
                Array.Empty<ItemScalarSurfaceBinding>();
            SetRenderersEnabled(liquidSurfaceRenderers, false);
        }
#endif

        private void HandleStatusChanged(IItemStatusSource _)
        {
            Refresh();
        }

        private void OnDestroy()
        {
            if (owner != null)
            {
                owner.StatusChanged -= HandleStatusChanged;
            }
        }

        private static void SetRenderersEnabled(
            Renderer[] targets,
            bool enabled)
        {
            if (targets == null)
            {
                return;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null)
                {
                    targets[index].enabled = enabled;
                }
            }
        }
    }
}
