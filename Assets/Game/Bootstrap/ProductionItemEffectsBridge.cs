using System;
using MSC.Items;
using MSC.Presentation.Fire;
using MSC.Presentation.Fluid;
using UnityEngine;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Composition-only attachment point for replaceable item effects. Item
    /// simulation remains independent from HDRP presentation assemblies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionItemEffectsBridge : MonoBehaviour
    {
        private ItemWorldRuntime items;
        private bool initialized;

        public void Initialize(ItemWorldRuntime configuredItems)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Item effects bridge is already initialized.");
            }

            items = configuredItems ??
                throw new ArgumentNullException(nameof(configuredItems));
            items.InstanceMaterialized += HandleInstanceMaterialized;
            items.PresentationAttached += HandlePresentationAttached;
            foreach (WorldItemInstance instance in items.LoadedInstances)
            {
                EnsureEffects(instance, instance.PresentationRoot);
            }

            initialized = true;
        }

        private static void HandleInstanceMaterialized(
            WorldItemInstance instance)
        {
            EnsureEffects(instance, instance?.PresentationRoot);
        }

        private static void HandlePresentationAttached(
            WorldItemInstance instance,
            GameObject presentationRoot)
        {
            EnsureEffects(instance, presentationRoot);
        }

        private static void EnsureEffects(
            WorldItemInstance instance,
            GameObject presentationRoot)
        {
            if (instance == null || instance.Definition == null)
            {
                return;
            }

            ServiceFluidPourController pour = instance.GetComponent<ServiceFluidPourController>();
            if (pour != null)
            {
                ServiceFluidPourPresenter presentation = instance.GetComponent<ServiceFluidPourPresenter>() ??
                    instance.gameObject.AddComponent<ServiceFluidPourPresenter>();
                if (!presentation.IsConfigured) presentation.Configure(pour, instance.DefinitionId);
            }

            if (LiquidContainerSurfacePresenter.SupportsDefinition(
                    instance.DefinitionId))
            {
                LiquidContainerSurfacePresenter water =
                    instance.GetComponent<LiquidContainerSurfacePresenter>() ??
                    instance.gameObject.AddComponent<
                        LiquidContainerSurfacePresenter>();
                if (!water.IsConfigured)
                {
                    water.Configure(instance, presentationRoot);
                }
                else
                {
                    water.BindPresentation(presentationRoot);
                }
            }

            if (instance.Definition.PrimaryAction == ItemPrimaryAction.Ignite ||
                instance.Definition.Combustion.IsConfigured)
            {
                GarbageBarrelFirePresenter fire =
                    instance.GetComponent<GarbageBarrelFirePresenter>() ??
                    instance.gameObject.AddComponent<
                        GarbageBarrelFirePresenter>();
                if (!fire.IsConfigured)
                {
                    fire.Configure(instance, presentationRoot);
                }
                else
                {
                    fire.BindPresentation(presentationRoot);
                }
            }
        }

        private void OnDestroy()
        {
            if (items != null)
            {
                items.InstanceMaterialized -= HandleInstanceMaterialized;
                items.PresentationAttached -= HandlePresentationAttached;
            }
        }
    }
}
