using UnityEngine;

namespace MSC.Weather.Presentation
{
    /// <summary>
    /// Presentation-only lightning request. Gameplay strikes and damage remain
    /// project-owned and are deliberately absent from this contract.
    /// </summary>
    public readonly struct EnvironmentLightningVisualRequest
    {
        public EnvironmentLightningVisualRequest(
            bool isRequested,
            uint sequence,
            Vector3 worldPosition,
            float intensity01)
        {
            IsRequested = isRequested;
            Sequence = sequence;
            WorldPosition = worldPosition;
            Intensity01 = intensity01;
        }

        public bool IsRequested { get; }

        public uint Sequence { get; }

        public Vector3 WorldPosition { get; }

        public float Intensity01 { get; }

        public static EnvironmentLightningVisualRequest None => default;
    }

    /// <summary>
    /// Explicit, sequence-keyed refresh request. The adapter must not perform
    /// reflection or ambient refresh work every frame.
    /// </summary>
    public readonly struct EnvironmentRefreshRequest
    {
        public EnvironmentRefreshRequest(EnvironmentRefreshTarget targets, uint sequence)
        {
            Targets = targets;
            Sequence = sequence;
        }

        public EnvironmentRefreshTarget Targets { get; }

        public uint Sequence { get; }

        public static EnvironmentRefreshRequest None => default;
    }
}
