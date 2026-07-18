using System;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Future tire/road consumer contract. Milestone 07C deliberately preserves
    /// the calibrated 06A dry baseline until wet friction is measured separately.
    /// </summary>
    public readonly struct ProductionRoadWetnessOutput
    {
        public ProductionRoadWetnessOutput(float roadWetness01)
        {
            if (!float.IsFinite(roadWetness01) ||
                roadWetness01 < 0f || roadWetness01 > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(roadWetness01));
            }

            RoadWetness01 = roadWetness01;
        }

        public float RoadWetness01 { get; }

        public bool WetFrictionEnabled => false;

        public float FrictionMultiplier => 1f;
    }
}
