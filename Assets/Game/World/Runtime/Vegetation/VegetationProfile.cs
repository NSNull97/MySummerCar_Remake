using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.World.Vegetation
{
    [CreateAssetMenu(
        fileName = "VegetationProfile",
        menuName = "MSC/World/Vegetation/Profile")]
    public sealed class VegetationProfile : ScriptableObject
    {
        [SerializeField] private string profileId = "vegetation.short-grass";
        [SerializeField] private VegetationDensityChannel densityChannel =
            VegetationDensityChannel.ShortGrass;
        [SerializeField, Min(0.05f)] private float candidateSpacingMeters = 0.65f;
        [SerializeField, Min(0f)] private float densityMultiplier = 1f;
        [SerializeField, Range(0f, 90f)] private float maximumSlopeDegrees = 35f;
        [SerializeField] private Vector2 worldHeightRange =
            new Vector2(-1000f, 1000f);
        [SerializeField] private Vector2 uniformScaleRange =
            new Vector2(0.85f, 1.15f);
        [SerializeField, Range(0f, 1f)] private float surfaceNormalAlignment = 0.2f;
        [SerializeField] private Mesh nearMesh;
        [SerializeField] private Mesh middleMesh;
        [SerializeField] private Mesh farMesh;
        [SerializeField] private Material material;
        [SerializeField, Min(0f)] private float middleLodDistance = 35f;
        [SerializeField, Min(0f)] private float farLodDistance = 90f;
        [SerializeField, Min(0f)] private float cullingDistance = 180f;
        [SerializeField, Min(0.01f)] private float lodDitherWidth = 8f;
        [SerializeField] private ShadowCastingMode shadowCasting =
            ShadowCastingMode.On;
        [SerializeField] private bool receiveShadows = true;
        [SerializeField, Min(0)] private int stableSeed = 173;

        public string ProfileId => profileId;
        public VegetationDensityChannel DensityChannel => densityChannel;
        public float CandidateSpacingMeters => candidateSpacingMeters;
        public float DensityMultiplier => densityMultiplier;
        public float MaximumSlopeDegrees => maximumSlopeDegrees;
        public Vector2 WorldHeightRange => worldHeightRange;
        public Vector2 UniformScaleRange => uniformScaleRange;
        public float SurfaceNormalAlignment => surfaceNormalAlignment;
        public Material Material => material;
        public float MiddleLodDistance => middleLodDistance;
        public float FarLodDistance => farLodDistance;
        public float CullingDistance => cullingDistance;
        public float LodDitherWidth => lodDitherWidth;
        public ShadowCastingMode ShadowCasting => shadowCasting;
        public bool ReceiveShadows => receiveShadows;
        public int StableSeed => stableSeed;

        public Mesh GetLodMesh(int lodIndex)
        {
            return lodIndex switch
            {
                0 => nearMesh,
                1 => middleMesh != null ? middleMesh : nearMesh,
                _ => farMesh != null
                    ? farMesh
                    : middleMesh != null
                        ? middleMesh
                        : nearMesh
            };
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(profileId))
            {
                errors.Add(name + " has no stable profile ID.");
            }

            if (!float.IsFinite(candidateSpacingMeters) ||
                candidateSpacingMeters < 0.05f)
            {
                errors.Add(name + " has invalid candidate spacing.");
            }

            if (!float.IsFinite(densityMultiplier) || densityMultiplier < 0f)
            {
                errors.Add(name + " has invalid density multiplier.");
            }

            if (worldHeightRange.x > worldHeightRange.y)
            {
                errors.Add(name + " has an inverted height range.");
            }

            if (uniformScaleRange.x <= 0f ||
                uniformScaleRange.x > uniformScaleRange.y)
            {
                errors.Add(name + " has an invalid scale range.");
            }

            if (nearMesh == null || middleMesh == null || farMesh == null)
            {
                errors.Add(name + " requires near, middle and far LOD meshes.");
            }

            if (material == null)
            {
                errors.Add(name + " has no HDRP vegetation material.");
            }

            if (middleLodDistance >= farLodDistance ||
                farLodDistance >= cullingDistance)
            {
                errors.Add(name + " has invalid increasing LOD distances.");
            }

            return errors;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredProfileId,
            VegetationDensityChannel configuredChannel,
            float configuredSpacing,
            float configuredDensityMultiplier,
            float configuredMaximumSlope,
            Vector2 configuredHeightRange,
            Vector2 configuredScaleRange,
            float configuredNormalAlignment,
            Mesh configuredNearMesh,
            Mesh configuredMiddleMesh,
            Mesh configuredFarMesh,
            Material configuredMaterial,
            Vector3 configuredLodDistances,
            float configuredLodDitherWidth,
            ShadowCastingMode configuredShadowCasting,
            bool configuredReceiveShadows,
            int configuredSeed)
        {
            profileId = configuredProfileId;
            densityChannel = configuredChannel;
            candidateSpacingMeters = Mathf.Max(0.05f, configuredSpacing);
            densityMultiplier = Mathf.Max(0f, configuredDensityMultiplier);
            maximumSlopeDegrees = Mathf.Clamp(configuredMaximumSlope, 0f, 90f);
            worldHeightRange = configuredHeightRange;
            uniformScaleRange = configuredScaleRange;
            surfaceNormalAlignment = Mathf.Clamp01(configuredNormalAlignment);
            nearMesh = configuredNearMesh;
            middleMesh = configuredMiddleMesh;
            farMesh = configuredFarMesh;
            material = configuredMaterial;
            middleLodDistance = Mathf.Max(0f, configuredLodDistances.x);
            farLodDistance = Mathf.Max(middleLodDistance + 0.01f, configuredLodDistances.y);
            cullingDistance = Mathf.Max(farLodDistance + 0.01f, configuredLodDistances.z);
            lodDitherWidth = Mathf.Max(0.01f, configuredLodDitherWidth);
            shadowCasting = configuredShadowCasting;
            receiveShadows = configuredReceiveShadows;
            stableSeed = configuredSeed;
        }
#endif
    }
}
