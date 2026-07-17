using MSC.Weather.Wetness;
using UnityEngine;

namespace MSC.Weather.Presentation
{
    /// <summary>
    /// Shared shader-global output bridge. Shaders opt in to the contract; ordinary
    /// HDRP/Lit materials are not claimed as covered merely because globals exist.
    /// </summary>
    public sealed class GlobalWetnessShaderBridge : IWetnessShaderBridge
    {
        public const string GroundWetnessProperty = "_MSC_GroundWetness";
        public const string RoadWetnessProperty = "_MSC_RoadWetness";
        public const string PuddleAmountProperty = "_MSC_PuddleAmount";
        public const string VegetationWetnessProperty = "_MSC_VegetationWetness";

        private static readonly int GroundWetnessId = Shader.PropertyToID(GroundWetnessProperty);
        private static readonly int RoadWetnessId = Shader.PropertyToID(RoadWetnessProperty);
        private static readonly int PuddleAmountId = Shader.PropertyToID(PuddleAmountProperty);
        private static readonly int VegetationWetnessId = Shader.PropertyToID(VegetationWetnessProperty);

        private uint lastAppliedRevision;

        public uint LastAppliedRevision => lastAppliedRevision;

        public void Apply(in WetnessEnvironmentOutputs outputs)
        {
            if (outputs.Revision == 0U || outputs.Revision == lastAppliedRevision)
            {
                return;
            }

            Shader.SetGlobalFloat(GroundWetnessId, outputs.GroundWetness01);
            Shader.SetGlobalFloat(RoadWetnessId, outputs.RoadWetness01);
            Shader.SetGlobalFloat(PuddleAmountId, outputs.PuddleAmount01);
            Shader.SetGlobalFloat(VegetationWetnessId, outputs.VegetationWetness01);
            lastAppliedRevision = outputs.Revision;
        }

        public void Reset()
        {
            Shader.SetGlobalFloat(GroundWetnessId, 0f);
            Shader.SetGlobalFloat(RoadWetnessId, 0f);
            Shader.SetGlobalFloat(PuddleAmountId, 0f);
            Shader.SetGlobalFloat(VegetationWetnessId, 0f);
            lastAppliedRevision = 0U;
        }
    }
}
