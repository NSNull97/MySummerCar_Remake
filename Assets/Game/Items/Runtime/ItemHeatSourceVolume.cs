using System;
using UnityEngine;

namespace MSC.Items
{
    /// <summary>
    /// Project-owned cooking volume shared by portable item heat sources and
    /// fixed home appliances. It exposes heat only; food owns its state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemHeatSourceVolume : MonoBehaviour, IItemHeatSource
    {
        private ItemWorldRuntime runtime;
        private WorldItemInstance itemAuthority;
        private Func<bool> enabledProvider;
        private Vector3 localCenter;
        private Vector3 localSize;
        private float cookingRate = 1f;
        private bool requiresItemEnabled;
        private bool registered;

        public bool IsHeating
        {
            get
            {
                if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                {
                    return false;
                }

                if (enabledProvider != null)
                {
                    return enabledProvider();
                }

                return !requiresItemEnabled ||
                       itemAuthority?.IsEnabled == true;
            }
        }

        public float CookingRate => cookingRate;

        public Bounds WorldBounds
        {
            get
            {
                Vector3 half = localSize * 0.5f;
                bool initialized = false;
                Bounds bounds = default;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = localCenter + new Vector3(
                        (corner & 1) == 0 ? -half.x : half.x,
                        (corner & 2) == 0 ? -half.y : half.y,
                        (corner & 4) == 0 ? -half.z : half.z);
                    Vector3 worldPoint = transform.TransformPoint(point);
                    if (!initialized)
                    {
                        bounds = new Bounds(worldPoint, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(worldPoint);
                    }
                }

                return bounds;
            }
        }

        public void ConfigureForItem(
            ItemWorldRuntime configuredRuntime,
            WorldItemInstance authority,
            ItemHeatSourceDefinition definition)
        {
            if (authority == null || definition == null ||
                !definition.ProvidesCookingHeat)
            {
                throw new ArgumentException(
                    "Item heat-source configuration is incomplete.");
            }

            ConfigureCore(
                configuredRuntime,
                authority,
                null,
                definition.RequiresItemEnabled,
                definition.CookingRate,
                definition.LocalCenter,
                definition.LocalSize);
        }

        public void ConfigureFixed(
            ItemWorldRuntime configuredRuntime,
            Func<bool> configuredEnabledProvider,
            float configuredCookingRate,
            Vector3 configuredLocalCenter,
            Vector3 configuredLocalSize)
        {
            ConfigureCore(
                configuredRuntime,
                null,
                configuredEnabledProvider ??
                    throw new ArgumentNullException(
                        nameof(configuredEnabledProvider)),
                false,
                configuredCookingRate,
                configuredLocalCenter,
                configuredLocalSize);
        }

        private void ConfigureCore(
            ItemWorldRuntime configuredRuntime,
            WorldItemInstance authority,
            Func<bool> configuredEnabledProvider,
            bool configuredRequiresItemEnabled,
            float configuredCookingRate,
            Vector3 configuredLocalCenter,
            Vector3 configuredLocalSize)
        {
            if (configuredRuntime == null ||
                !float.IsFinite(configuredCookingRate) ||
                configuredCookingRate <= 0f ||
                !IsFinite(configuredLocalCenter) ||
                !IsFinitePositive(configuredLocalSize))
            {
                throw new ArgumentException(
                    "Heat-source runtime or volume is invalid.");
            }

            Unregister();
            runtime = configuredRuntime;
            itemAuthority = authority;
            enabledProvider = configuredEnabledProvider;
            requiresItemEnabled = configuredRequiresItemEnabled;
            cookingRate = configuredCookingRate;
            localCenter = configuredLocalCenter;
            localSize = configuredLocalSize;
            runtime.RegisterHeatSource(this);
            registered = true;
        }

        private void OnDestroy()
        {
            Unregister();
        }

        private void Unregister()
        {
            if (registered && runtime != null)
            {
                runtime.UnregisterHeatSource(this);
            }

            registered = false;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinitePositive(Vector3 value) =>
            IsFinite(value) && value.x > 0f && value.y > 0f && value.z > 0f;
    }
}
