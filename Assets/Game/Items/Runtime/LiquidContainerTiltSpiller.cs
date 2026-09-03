using System;
using UnityEngine;

namespace MSC.Items
{
    /// <summary>
    /// Authoritative gravity spill for the two reviewed open sauna containers.
    /// Presentation may observe this state, but litres are removed here and the
    /// regular LiquidSpilled event remains the puddle/audio boundary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LiquidContainerTiltSpiller : MonoBehaviour
    {
        public const string SaunaBucketDefinitionId = "item.sauna-bucket";
        public const string SaunaDipperDefinitionId = "item.sauna-dipper";

        private const float SpillEventIntervalSeconds = 0.12f;
        private const float MinimumLiquidLitres = 0.0001f;

        private WorldItemInstance item;
        private Vector3 localOpenAxis;
        private float pendingSpillSeconds;
        private bool configured;

        public bool IsConfigured => configured;
        public bool IsSpilling { get; private set; }
        public float SpillIntensity01 { get; private set; }
        public Vector3 LocalOpenAxis => localOpenAxis;

        public static bool TryGetLocalOpenAxis(
            string definitionId,
            out Vector3 axis)
        {
            if (string.Equals(
                    definitionId,
                    SaunaBucketDefinitionId,
                    StringComparison.Ordinal) ||
                string.Equals(
                    definitionId,
                    SaunaDipperDefinitionId,
                    StringComparison.Ordinal))
            {
                // Read-only donor mesh evidence: the open water insert faces
                // local -Z in both sanitized wrappers.
                axis = Vector3.back;
                return true;
            }

            axis = Vector3.zero;
            return false;
        }

        public void Configure(
            WorldItemInstance configuredItem,
            Vector3 configuredLocalOpenAxis)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Liquid tilt spiller is already configured.");
            }

            item = configuredItem ??
                throw new ArgumentNullException(nameof(configuredItem));
            if (!float.IsFinite(configuredLocalOpenAxis.x) ||
                !float.IsFinite(configuredLocalOpenAxis.y) ||
                !float.IsFinite(configuredLocalOpenAxis.z) ||
                configuredLocalOpenAxis.sqrMagnitude < 0.5f)
            {
                throw new ArgumentException(
                    "Liquid container open axis is invalid.",
                    nameof(configuredLocalOpenAxis));
            }

            localOpenAxis = configuredLocalOpenAxis.normalized;
            configured = true;
        }

        /// <summary>
        /// Deterministic tick exposed for focused simulation tests. A fuller
        /// container starts spilling at a shallower tilt than an almost-empty
        /// one; a fully inverted container always drains.
        /// </summary>
        public void Tick(float elapsedSeconds)
        {
            if (!configured || item == null ||
                !float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f ||
                item.LiquidAmountLitres <= MinimumLiquidLitres ||
                string.IsNullOrEmpty(item.LiquidId))
            {
                ResetSpillState();
                return;
            }

            float capacity = Mathf.Max(
                MinimumLiquidLitres,
                item.Definition.MaximumContent);
            float fillRatio = Mathf.Clamp01(
                item.LiquidAmountLitres / capacity);
            float openUpDot = Vector3.Dot(
                transform.TransformDirection(localOpenAxis),
                Vector3.up);

            // At full volume the rim is reached at roughly 40 degrees. Near
            // empty, the container must pass horizontal before water escapes.
            float spillStartDot = Mathf.Lerp(-0.2f, 0.75f, fillRatio);
            if (openUpDot >= spillStartDot)
            {
                ResetSpillState();
                return;
            }

            IsSpilling = true;
            SpillIntensity01 = Mathf.Clamp01(
                (spillStartDot - openUpDot) /
                Mathf.Max(0.0001f, spillStartDot + 1f));
            pendingSpillSeconds += elapsedSeconds;
            if (pendingSpillSeconds < SpillEventIntervalSeconds &&
                item.LiquidAmountLitres > capacity * 0.01f)
            {
                return;
            }

            float spillSeconds = pendingSpillSeconds;
            pendingSpillSeconds = 0f;
            float litresPerSecond = capacity * Mathf.Lerp(
                0.35f,
                1.8f,
                SpillIntensity01);
            item.TrySpillLiquid(
                litresPerSecond * spillSeconds,
                out _);
            if (item.LiquidAmountLitres <= MinimumLiquidLitres)
            {
                ResetSpillState();
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void ResetSpillState()
        {
            IsSpilling = false;
            SpillIntensity01 = 0f;
            pendingSpillSeconds = 0f;
        }
    }
}
