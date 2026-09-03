using System;
using MSC.Presentation.Fluid;
using UnityEngine;

namespace MSC.Home
{
    /// <summary>
    /// Rebuildable shower/faucet presentation driven exclusively by the
    /// authoritative home snapshot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeWaterPresentation : MonoBehaviour
    {
        private HomeSystemRuntime runtime;
        private ProceduralFluidStreamPresenter showerHead;
        private ProceduralFluidStreamPresenter faucet;
        private HomeLiquidStreamFillVolume showerFillVolume;
        private HomeLiquidStreamFillVolume faucetFillVolume;
        private bool initialized;

        public void Initialize(
            HomeSystemRuntime configuredRuntime,
            Transform showerHeadAnchor,
            Transform faucetAnchor,
            LayerMask collisionMask)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Home water presentation is already initialized.");
            }

            runtime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));
            showerHead = CreateStream(
                "Shower Head Water",
                showerHeadAnchor,
                FluidStreamProfile.Shower,
                collisionMask,
                out showerFillVolume);
            faucet = CreateStream(
                "Shower Faucet Water",
                faucetAnchor,
                FluidStreamProfile.Faucet,
                collisionMask,
                out faucetFillVolume);
            runtime.StateChanged += HandleStateChanged;
            initialized = true;
            Apply(runtime.Snapshot, clearExisting: true);
        }

        private static ProceduralFluidStreamPresenter CreateStream(
            string displayName,
            Transform anchor,
            FluidStreamProfile profile,
            LayerMask collisionMask,
            out HomeLiquidStreamFillVolume fillVolume)
        {
            if (anchor == null)
            {
                throw new ArgumentNullException(nameof(anchor));
            }

            var streamObject = new GameObject(displayName)
            {
                hideFlags = HideFlags.DontSave,
            };
            streamObject.transform.SetParent(anchor, false);
            ProceduralFluidStreamPresenter result =
                streamObject.AddComponent<ProceduralFluidStreamPresenter>();
            result.Configure(
                profile,
                Vector3.zero,
                Vector3.forward,
                collisionMask);
            fillVolume =
                streamObject.AddComponent<HomeLiquidStreamFillVolume>();
            bool shower = profile == FluidStreamProfile.Shower;
            fillVolume.Configure(
                configuredFillRateLitresPerSecond: shower ? 1.25f : 1.75f,
                radiusMeters: shower ? 0.28f : 0.11f,
                lengthMeters: shower ? 3.45f : 1.4f);
            return result;
        }

        private void HandleStateChanged(HomeStateSnapshot state)
        {
            Apply(state, clearExisting: false);
        }

        private void Apply(
            HomeStateSnapshot state,
            bool clearExisting)
        {
            showerHead?.SetFlowing(
                state.ShowerHeadFlowing,
                clearExisting);
            showerFillVolume?.SetFlowing(state.ShowerHeadFlowing);
            faucet?.SetFlowing(
                state.ShowerTapFlowing,
                clearExisting);
            faucetFillVolume?.SetFlowing(state.ShowerTapFlowing);
        }

        private void OnDestroy()
        {
            if (runtime != null)
            {
                runtime.StateChanged -= HandleStateChanged;
            }
        }
    }
}
