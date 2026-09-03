using MSC.Weather.Presentation;
using UnityEngine;

namespace MSC.Weather.System
{
    /// <summary>
    /// Allocation-free shader output for the new wetness domain. It reuses the
    /// accepted project shader contract and never instantiates renderer materials.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurfaceWetnessShaderOutput : MonoBehaviour,
        ISurfaceWetnessOutput
    {
        private static readonly int GroundWetnessId = Shader.PropertyToID(
            GlobalWetnessShaderBridge.GroundWetnessProperty);
        private static readonly int RoadWetnessId = Shader.PropertyToID(
            GlobalWetnessShaderBridge.RoadWetnessProperty);
        private static readonly int PuddleAmountId = Shader.PropertyToID(
            GlobalWetnessShaderBridge.PuddleAmountProperty);
        private static readonly int VegetationWetnessId = Shader.PropertyToID(
            GlobalWetnessShaderBridge.VegetationWetnessProperty);

        [SerializeField] private bool writeGlobalContract = true;
        [SerializeField] private Renderer[] localRenderers =
            global::System.Array.Empty<Renderer>();
        [SerializeField, Range(0f, 2f)] private float localWetnessMultiplier = 1f;
        [SerializeField, Range(0f, 2f)] private float localPuddleMultiplier = 1f;

        private MaterialPropertyBlock propertyBlock;

        public float LastWetness01 { get; private set; }
        public float LastPuddleAmount01 { get; private set; }

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        public void Apply(float wetness01, float puddleAmount01)
        {
            float wetness = FiniteClamp01(wetness01);
            float puddles = FiniteClamp01(puddleAmount01);
            LastWetness01 = wetness;
            LastPuddleAmount01 = puddles;

            if (writeGlobalContract)
            {
                Shader.SetGlobalFloat(GroundWetnessId, wetness);
                Shader.SetGlobalFloat(RoadWetnessId, wetness);
                Shader.SetGlobalFloat(PuddleAmountId, puddles);
                Shader.SetGlobalFloat(VegetationWetnessId, wetness);
            }

            if (localRenderers == null || localRenderers.Length == 0)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            float localWetness = Mathf.Clamp01(wetness * localWetnessMultiplier);
            float localPuddles = Mathf.Clamp01(puddles * localPuddleMultiplier);
            for (int index = 0; index < localRenderers.Length; index++)
            {
                Renderer target = localRenderers[index];
                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(GroundWetnessId, localWetness);
                propertyBlock.SetFloat(RoadWetnessId, localWetness);
                propertyBlock.SetFloat(PuddleAmountId, localPuddles);
                propertyBlock.SetFloat(VegetationWetnessId, localWetness);
                target.SetPropertyBlock(propertyBlock);
                propertyBlock.Clear();
            }
        }

        private static float FiniteClamp01(float value) =>
            float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Clamp01(value);
    }
}
