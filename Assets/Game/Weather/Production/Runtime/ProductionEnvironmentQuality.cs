using System;
using MSC.Weather.Presentation;

namespace MSC.Weather.Production
{
    public static class ProductionEnvironmentQuality
    {
        public static string GetStableId(
            EnvironmentQualityTier qualityTier)
        {
            switch (qualityTier)
            {
                case EnvironmentQualityTier.Low:
                    return "quality.low";
                case EnvironmentQualityTier.High:
                    return "quality.high";
                case EnvironmentQualityTier.Medium:
                    return "quality.medium";
                default:
                    throw new ArgumentOutOfRangeException(nameof(qualityTier));
            }
        }
    }
}
