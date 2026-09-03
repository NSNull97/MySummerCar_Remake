using System;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Home
{
    /// <summary>
    /// Physical water column owned by a home fixture. A compatible container
    /// fills while its collider remains under an enabled stream.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeLiquidStreamFillVolume : MonoBehaviour
    {
        private CapsuleCollider fillVolume;
        private float fillRateLitresPerSecond;
        private bool configured;
        private bool flowing;

        public bool IsFlowing => flowing;
        public float FillRateLitresPerSecond => fillRateLitresPerSecond;
        public CapsuleCollider FillVolume => fillVolume;

        public void Configure(
            float configuredFillRateLitresPerSecond,
            float radiusMeters,
            float lengthMeters)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Liquid stream fill volume is already configured.");
            }

            if (!float.IsFinite(configuredFillRateLitresPerSecond) ||
                configuredFillRateLitresPerSecond <= 0f ||
                !float.IsFinite(radiusMeters) ||
                radiusMeters <= 0f ||
                !float.IsFinite(lengthMeters) ||
                lengthMeters <= radiusMeters * 2f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredFillRateLitresPerSecond));
            }

            fillRateLitresPerSecond = configuredFillRateLitresPerSecond;
            fillVolume = gameObject.AddComponent<CapsuleCollider>();
            fillVolume.isTrigger = true;
            fillVolume.direction = 2;
            fillVolume.radius = radiusMeters;
            fillVolume.height = lengthMeters;
            fillVolume.center = Vector3.forward * (lengthMeters * 0.5f);
            fillVolume.enabled = false;
            configured = true;
        }

        public void SetFlowing(bool value)
        {
            flowing = configured && value;
            if (fillVolume != null)
            {
                fillVolume.enabled = flowing;
            }
        }

        public bool TryFill(Collider other, float elapsedSeconds)
        {
            if (!flowing || other == null ||
                !float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
            {
                return false;
            }

            MonoBehaviour[] candidates =
                other.GetComponentsInParent<MonoBehaviour>(
                    includeInactive: false);
            float requestedLitres =
                fillRateLitresPerSecond * elapsedSeconds;
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index] is ILiquidContainerTarget container &&
                    container.CanAcceptLiquid(
                        LiquidTypeIds.Water,
                        requestedLitres) &&
                    container.TryAcceptLiquid(
                        LiquidTypeIds.Water,
                        requestedLitres,
                        out float acceptedLitres) &&
                    acceptedLitres > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnTriggerStay(Collider other)
        {
            TryFill(other, Time.fixedDeltaTime);
        }
    }
}
