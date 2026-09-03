using System;
using UnityEngine;

namespace MSC.Items
{
    /// <summary>
    /// Physics-facing adapter for authored granular fuel. A matching package
    /// pours continuously only while its body overlaps the receiver and is
    /// tilted far enough; authoritative quantities stay in item state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemFuelPourReceiver : MonoBehaviour
    {
        private const int OverlapCapacity = 24;
        private const float PourStepSeconds = 0.1f;

        private readonly Collider[] overlaps = new Collider[OverlapCapacity];
        private WorldItemInstance receiver;
        private ItemCombustionDefinition combustion;
        private float pendingPourSeconds;

        public bool IsConfigured => receiver != null &&
                                    combustion?.IsConfigured == true;
        public bool IsPouring { get; private set; }
        public WorldItemInstance ActiveSource { get; private set; }

        public void Configure(WorldItemInstance configuredReceiver)
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException(
                    "Fuel pour receiver is already configured.");
            }

            if (configuredReceiver?.Definition?.Combustion.IsConfigured != true)
            {
                throw new ArgumentException(
                    "Fuel receiver requires an authored combustion definition.",
                    nameof(configuredReceiver));
            }

            receiver = configuredReceiver;
            combustion = receiver.Definition.Combustion;
        }

        public bool TryPourFrom(
            WorldItemInstance source,
            float elapsedSeconds,
            out float transferredAmount)
        {
            transferredAmount = 0f;
            if (!IsConfigured || source == null || receiver.IsEnabled ||
                !float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f ||
                !string.Equals(
                    source.DefinitionId,
                    combustion.AcceptedFuelDefinitionId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            float uprightDot = Vector3.Dot(source.transform.up, Vector3.up);
            float maximumUprightDot = Mathf.Cos(
                combustion.MinimumFuelPourTiltDegrees * Mathf.Deg2Rad);
            if (uprightDot > maximumUprightDot)
            {
                return false;
            }

            return source.TryTransferFuelTo(
                receiver,
                combustion.FuelPourRatePerSecond * elapsedSeconds,
                out transferredAmount);
        }

        public void Tick(float elapsedSeconds)
        {
            IsPouring = false;
            ActiveSource = null;
            if (!IsConfigured || receiver.IsEnabled ||
                !float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
            {
                pendingPourSeconds = 0f;
                return;
            }

            ItemHeatSourceDefinition volume = receiver.Definition.HeatSource;
            Vector3 lossyScale = transform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(
                volume.LocalSize * 0.5f,
                new Vector3(
                    Mathf.Abs(lossyScale.x),
                    Mathf.Abs(lossyScale.y),
                    Mathf.Abs(lossyScale.z)));
            int count = Physics.OverlapBoxNonAlloc(
                transform.TransformPoint(volume.LocalCenter),
                halfExtents,
                overlaps,
                transform.rotation,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            WorldItemInstance source = null;
            for (int index = 0; index < count; index++)
            {
                Collider overlap = overlaps[index];
                WorldItemInstance candidate = overlap != null
                    ? overlap.GetComponentInParent<WorldItemInstance>()
                    : null;
                if (candidate != null && candidate != receiver &&
                    string.Equals(
                        candidate.DefinitionId,
                        combustion.AcceptedFuelDefinitionId,
                        StringComparison.Ordinal))
                {
                    source = candidate;
                    break;
                }
            }

            if (source == null)
            {
                pendingPourSeconds = 0f;
                return;
            }

            float uprightDot = Vector3.Dot(source.transform.up, Vector3.up);
            float maximumUprightDot = Mathf.Cos(
                combustion.MinimumFuelPourTiltDegrees * Mathf.Deg2Rad);
            if (uprightDot > maximumUprightDot)
            {
                pendingPourSeconds = 0f;
                return;
            }

            IsPouring = true;
            ActiveSource = source;
            pendingPourSeconds += elapsedSeconds;
            if (pendingPourSeconds < PourStepSeconds)
            {
                return;
            }

            float pourSeconds = pendingPourSeconds;
            pendingPourSeconds = 0f;
            if (!TryPourFrom(source, pourSeconds, out _))
            {
                IsPouring = false;
                ActiveSource = null;
            }
        }

        private void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }
    }
}
