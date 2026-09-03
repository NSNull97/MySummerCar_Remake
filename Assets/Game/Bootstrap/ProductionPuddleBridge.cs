using System;
using MSC.Interaction.Capabilities;
using MSC.Items;
using MSC.Presentation.Fluid;
using MSC.Weather.Domain;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using UnityEngine;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Composition-only bridge between authoritative rain/item state and the
    /// replaceable puddle presenter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionPuddleBridge : MonoBehaviour
    {
        private ProductionEnvironmentController environment;
        private ItemWorldRuntime items;
        private ProceduralPuddlePresenter presenter;
        private bool initialized;

        public void Initialize(
            ProductionEnvironmentController configuredEnvironment,
            ItemWorldRuntime configuredItems,
            Transform player,
            Transform presentationParent)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Puddle bridge is already initialized.");
            }

            environment = configuredEnvironment ??
                throw new ArgumentNullException(nameof(configuredEnvironment));
            items = configuredItems ??
                throw new ArgumentNullException(nameof(configuredItems));
            if (player == null || presentationParent == null)
            {
                throw new ArgumentNullException(
                    player == null ? nameof(player) : nameof(presentationParent));
            }

            var presenterObject = new GameObject("Production Puddle Presentation");
            presenterObject.transform.SetParent(presentationParent, false);
            presenter = presenterObject.AddComponent<ProceduralPuddlePresenter>();
            presenter.Configure(player, ~0);

            environment.EnvironmentOutputsChanged += HandleEnvironmentOutputs;
            items.ActionCompleted += HandleItemAction;
            ApplyRain(environment.CurrentOutputs);
            initialized = true;
        }

        private void HandleEnvironmentOutputs(WeatherEnvironmentOutputs outputs)
        {
            ApplyRain(outputs);
        }

        private void ApplyRain(in WeatherEnvironmentOutputs outputs)
        {
            if (presenter == null)
            {
                return;
            }

            float exposureMultiplier =
                string.Equals(
                    outputs.Wetness.ExposureProfileId,
                    SurfaceExposureProfile.Exterior.StableId,
                    StringComparison.Ordinal)
                    ? 1f
                    : 0f;
            presenter.SetRainAmount(
                outputs.Wetness.PuddleAmount01 * exposureMultiplier);
        }

        private void HandleItemAction(ItemActionCompleted action)
        {
            if (presenter == null ||
                action.Action != ItemActionKind.LiquidSpilled ||
                action.AffectedAmount <= 0f ||
                !string.Equals(
                    action.LiquidId,
                    LiquidTypeIds.Water,
                    StringComparison.Ordinal) ||
                !items.TryGetInstance(action.StableId.Value, out WorldItemInstance item))
            {
                return;
            }

            presenter.AddLocalPuddle(
                item.transform.position,
                action.AffectedAmount,
                item.transform);
        }

        private void OnDestroy()
        {
            if (environment != null)
            {
                environment.EnvironmentOutputsChanged -= HandleEnvironmentOutputs;
            }

            if (items != null)
            {
                items.ActionCompleted -= HandleItemAction;
            }
        }
    }
}
