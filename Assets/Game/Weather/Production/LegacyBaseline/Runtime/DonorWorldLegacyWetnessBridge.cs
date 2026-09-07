using System;
using System.Collections.Generic;
using MSC.LegacyImport;
using MSC.Weather.Wetness;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Weather.Production.LegacyBaseline
{
    /// <summary>
    /// Bounded compatibility bridge for the temporary donor-world baseline.
    /// It discovers only explicit project-owned material bindings and applies
    /// per-slot property blocks only to source GUIDs approved by a project-owned
    /// coverage profile. Process-wide shader globals remain owned by the
    /// production environment controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DonorWorldLegacyWetnessBridge :
        MonoBehaviour,
        IWetnessShaderBridge
    {
        public const float ProductionOpaqueWetSmoothness = 0.45f;
        public const float ProductionAlphaClipWetSmoothness = 0.25f;

        private const string HdrpLitShaderName = "HDRP/Lit";
        private const string UnlitShaderToken = "Unlit";
        private const string EmissiveMapKeyword = "_EMISSIVE_COLOR_MAP";
        private const float ClassificationEpsilon = 0.001f;

        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId =
            Shader.PropertyToID("_Smoothness");
        private static readonly int SurfaceTypeId =
            Shader.PropertyToID("_SurfaceType");
        private static readonly int AlphaCutoffEnableId =
            Shader.PropertyToID("_AlphaCutoffEnable");
        private static readonly int EmissiveColorId =
            Shader.PropertyToID("_EmissiveColor");

        [SerializeField]
        private LegacyWetnessCoverageProfile coverageProfile;

        [SerializeField, Min(1)]
        private int quantizationSteps = 64;

        [SerializeField, Range(0.5f, 1f)]
        private float opaqueWetColorMultiplier = 0.84f;

        [SerializeField, Range(0f, 1f)]
        private float opaqueWetSmoothness =
            ProductionOpaqueWetSmoothness;

        [SerializeField, Range(0.5f, 1f)]
        private float alphaClipWetColorMultiplier = 0.9f;

        [SerializeField, Range(0f, 1f)]
        private float alphaClipWetSmoothness =
            ProductionAlphaClipWetSmoothness;

        private readonly Dictionary<SceneHandle, SceneCache> sceneCaches =
            new Dictionary<SceneHandle, SceneCache>();

        private MaterialPropertyBlock propertyBlock;
        private LegacyWetnessCoverageCounters coverage;
        private QuantizedWetness lastQuantizedWetness;
        private bool hasAppliedWetness;
        private uint lastAppliedRevision;
        private uint lastObservedRevision;
        private int lastAppliedSlotCount;
        private int lastSkippedInactivePresentationSlotCount;
        private ulong propertyBlockWriteCount;

        public LegacyWetnessCoverageProfile CoverageProfile =>
            coverageProfile;
        public LegacyWetnessCoverageCounters Coverage => coverage;
        public bool CoverageProfileIsValid =>
            coverageProfile != null && coverageProfile.IsValid;
        public float OpaqueWetSmoothness => opaqueWetSmoothness;
        public float AlphaClipWetSmoothness =>
            alphaClipWetSmoothness;
        public uint LastAppliedRevision => lastAppliedRevision;
        public uint LastObservedRevision => lastObservedRevision;
        public int LastAppliedSlotCount => lastAppliedSlotCount;
        public int LastSkippedInactivePresentationSlotCount =>
            lastSkippedInactivePresentationSlotCount;
        public ulong PropertyBlockWriteCount => propertyBlockWriteCount;

        public void SetCoverageProfile(
            LegacyWetnessCoverageProfile profile)
        {
            coverageProfile = profile;
            if (isActiveAndEnabled)
            {
                RefreshLoadedScenes();
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            LegacyWetnessCoverageProfile profile)
        {
            coverageProfile = profile;
            opaqueWetSmoothness = ProductionOpaqueWetSmoothness;
            alphaClipWetSmoothness =
                ProductionAlphaClipWetSmoothness;
        }
#endif

        public void Apply(in WetnessEnvironmentOutputs outputs)
        {
            if (!isActiveAndEnabled || outputs.Revision == 0U)
            {
                return;
            }

            lastObservedRevision = outputs.Revision;
            QuantizedWetness quantized = QuantizedWetness.Create(
                outputs,
                quantizationSteps);
            if (hasAppliedWetness &&
                quantized.Equals(lastQuantizedWetness))
            {
                return;
            }

            EnsurePropertyBlock();
            lastSkippedInactivePresentationSlotCount = 0;
            lastAppliedSlotCount = ApplyToAllScenes(quantized);
            lastQuantizedWetness = quantized;
            hasAppliedWetness = true;
            lastAppliedRevision = outputs.Revision;
        }

        /// <summary>
        /// Rebuilds the event-time cache from all loaded scenes. This is intended
        /// for explicit validation and legacy presentation-mode changes, not
        /// per-frame use.
        /// </summary>
        public void RefreshLoadedScenes()
        {
            RestoreAllCachedSlots();
            sceneCaches.Clear();

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                RegisterScene(SceneManager.GetSceneAt(index));
            }

            RecalculateCoverage();
            if (hasAppliedWetness)
            {
                EnsurePropertyBlock();
                lastSkippedInactivePresentationSlotCount = 0;
                lastAppliedSlotCount =
                    ApplyToAllScenes(lastQuantizedWetness);
            }
        }

        /// <summary>
        /// Restores the exact per-slot property blocks captured at registration
        /// and clears the bridge's local wetness state. Scene registration remains
        /// available for a later authoritative presentation sync.
        /// </summary>
        public void ResetPresentation()
        {
            RestoreAllCachedSlots();
            hasAppliedWetness = false;
            lastQuantizedWetness = default;
            lastAppliedRevision = 0U;
            lastObservedRevision = 0U;
            lastAppliedSlotCount = 0;
            lastSkippedInactivePresentationSlotCount = 0;
            propertyBlockWriteCount = 0UL;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            RefreshLoadedScenes();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            ResetPresentation();
            sceneCaches.Clear();
            coverage = default;
        }

        private void OnValidate()
        {
            quantizationSteps = Mathf.Max(1, quantizationSteps);
            opaqueWetColorMultiplier =
                Mathf.Clamp(opaqueWetColorMultiplier, 0.5f, 1f);
            opaqueWetSmoothness = Mathf.Clamp01(opaqueWetSmoothness);
            alphaClipWetColorMultiplier =
                Mathf.Clamp(alphaClipWetColorMultiplier, 0.5f, 1f);
            alphaClipWetSmoothness =
                Mathf.Clamp01(alphaClipWetSmoothness);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (sceneCaches.TryGetValue(
                    scene.handle,
                    out SceneCache previous))
            {
                RestoreScene(previous);
                sceneCaches.Remove(scene.handle);
            }

            SceneCache registered = RegisterScene(scene);
            RecalculateCoverage();
            if (hasAppliedWetness && registered != null)
            {
                EnsurePropertyBlock();
                lastSkippedInactivePresentationSlotCount = 0;
                lastAppliedSlotCount = ApplyToScene(
                    registered,
                    lastQuantizedWetness);
            }
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            sceneCaches.Remove(scene.handle);
            RecalculateCoverage();
        }

        private SceneCache RegisterScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            var cache = new SceneCache();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0;
                 rootIndex < roots.Length;
                 rootIndex++)
            {
                DonorWorldLegacyMaterialBinding[] bindings =
                    roots[rootIndex].GetComponentsInChildren<
                        DonorWorldLegacyMaterialBinding>(
                        includeInactive: true);
                for (int bindingIndex = 0;
                     bindingIndex < bindings.Length;
                     bindingIndex++)
                {
                    RegisterBinding(bindings[bindingIndex], cache);
                }
            }

            if (cache.RegisteredBindingCount == 0 &&
                cache.RejectedBindingCount == 0)
            {
                return null;
            }

            sceneCaches[scene.handle] = cache;
            return cache;
        }

        private void RegisterBinding(
            DonorWorldLegacyMaterialBinding binding,
            SceneCache sceneCache)
        {
            if (binding == null ||
                !binding.TryValidateConfiguration(out _))
            {
                sceneCache.RejectedBindingCount++;
                return;
            }

            sceneCache.RegisteredBindingCount++;
            Renderer renderer = binding.TargetRenderer;
            IReadOnlyList<string> sourceGuids =
                binding.SourceMaterialGuids;
            IReadOnlyList<Material> texturedMaterials =
                binding.TexturedMaterials;
            var rendererCache = new RendererCache(
                renderer,
                binding.MaterialSlotCount);
            rendererCache.RefreshSharedMaterials();

            for (int slotIndex = 0;
                 slotIndex < binding.MaterialSlotCount;
                 slotIndex++)
            {
                Material material = texturedMaterials[slotIndex];
                LegacyWetnessSlotCoverage slotCoverage =
                    Classify(material);
                sceneCache.RecordShaderFamily(slotCoverage);
                if (slotCoverage != LegacyWetnessSlotCoverage.OpaqueLit &&
                    slotCoverage !=
                        LegacyWetnessSlotCoverage.AlphaClipLit)
                {
                    continue;
                }

                if (coverageProfile == null ||
                    !coverageProfile.TryGetCategory(
                        sourceGuids[slotIndex],
                        out LegacyWetnessSurfaceCategory category))
                {
                    sceneCache.ExcludedNotAllowlistedSlotCount++;
                    continue;
                }

                if (!IsCategoryCompatible(slotCoverage, category))
                {
                    sceneCache.ExcludedCategoryMismatchSlotCount++;
                    continue;
                }

                if (!rendererCache.IsExpectedMaterial(
                        slotIndex,
                        material))
                {
                    sceneCache.ExcludedInactivePresentationSlotCount++;
                    continue;
                }

                var originalPropertyBlock =
                    new MaterialPropertyBlock();
                renderer.GetPropertyBlock(
                    originalPropertyBlock,
                    slotIndex);
                Color baseColor = originalPropertyBlock.HasColor(
                    BaseColorId)
                        ? originalPropertyBlock.GetColor(BaseColorId)
                        : material.GetColor(BaseColorId);
                float baseSmoothness = originalPropertyBlock.HasFloat(
                    SmoothnessId)
                        ? originalPropertyBlock.GetFloat(SmoothnessId)
                        : material.GetFloat(SmoothnessId);
                rendererCache.Slots.Add(new SlotCache(
                    slotIndex,
                    material,
                    category,
                    baseColor,
                    baseSmoothness,
                    originalPropertyBlock));
                sceneCache.RecordApprovedCategory(category);
            }

            if (rendererCache.Slots.Count > 0)
            {
                sceneCache.Renderers.Add(rendererCache);
            }
        }

        private static LegacyWetnessSlotCoverage Classify(
            Material material)
        {
            if (material == null || material.shader == null)
            {
                return LegacyWetnessSlotCoverage.ExcludedUnsupported;
            }

            string shaderName = material.shader.name;
            if (shaderName.IndexOf(
                    UnlitShaderToken,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return LegacyWetnessSlotCoverage.ExcludedUnlit;
            }

            if (!string.Equals(
                    shaderName,
                    HdrpLitShaderName,
                    StringComparison.Ordinal) ||
                !material.HasProperty(BaseColorId) ||
                !material.HasProperty(SmoothnessId) ||
                !material.HasProperty(SurfaceTypeId) ||
                !material.HasProperty(AlphaCutoffEnableId))
            {
                return LegacyWetnessSlotCoverage.ExcludedUnsupported;
            }

            if (material.GetFloat(SurfaceTypeId) > 0.5f ||
                material.renderQueue >= 3000)
            {
                return LegacyWetnessSlotCoverage
                    .ExcludedTransparentOrWater;
            }

            if (material.IsKeywordEnabled(EmissiveMapKeyword) ||
                material.HasProperty(EmissiveColorId) &&
                material.GetColor(EmissiveColorId)
                    .maxColorComponent > ClassificationEpsilon)
            {
                return LegacyWetnessSlotCoverage.ExcludedEmissive;
            }

            return material.GetFloat(AlphaCutoffEnableId) > 0.5f
                ? LegacyWetnessSlotCoverage.AlphaClipLit
                : LegacyWetnessSlotCoverage.OpaqueLit;
        }

        private static bool IsCategoryCompatible(
            LegacyWetnessSlotCoverage slotCoverage,
            LegacyWetnessSurfaceCategory category) =>
            slotCoverage == LegacyWetnessSlotCoverage.AlphaClipLit
                ? category == LegacyWetnessSurfaceCategory.Vegetation
                : category != LegacyWetnessSurfaceCategory.Vegetation;

        private int ApplyToAllScenes(QuantizedWetness wetness)
        {
            int appliedCount = 0;
            foreach (SceneCache cache in sceneCaches.Values)
            {
                appliedCount += ApplyToScene(cache, wetness);
            }

            return appliedCount;
        }

        private int ApplyToScene(
            SceneCache cache,
            QuantizedWetness wetness)
        {
            int appliedCount = 0;
            for (int rendererIndex = 0;
                 rendererIndex < cache.Renderers.Count;
                 rendererIndex++)
            {
                RendererCache rendererCache =
                    cache.Renderers[rendererIndex];
                if (rendererCache.Renderer == null)
                {
                    continue;
                }

                rendererCache.RefreshSharedMaterials();
                for (int slotIndex = 0;
                     slotIndex < rendererCache.Slots.Count;
                     slotIndex++)
                {
                    SlotCache slot = rendererCache.Slots[slotIndex];
                    if (!rendererCache.IsExpectedMaterial(
                            slot.MaterialSlotIndex,
                            slot.ExpectedMaterial))
                    {
                        RestoreSlot(rendererCache.Renderer, slot);
                        lastSkippedInactivePresentationSlotCount++;
                        continue;
                    }

                    float amount = GetWetness(wetness, slot.Category);
                    bool vegetation = slot.Category ==
                        LegacyWetnessSurfaceCategory.Vegetation;
                    if (ApplySlot(
                            rendererCache.Renderer,
                            slot,
                            amount,
                            vegetation
                                ? alphaClipWetColorMultiplier
                                : opaqueWetColorMultiplier,
                            vegetation
                                ? alphaClipWetSmoothness
                                : opaqueWetSmoothness))
                    {
                        appliedCount++;
                    }
                }
            }

            return appliedCount;
        }

        private static float GetWetness(
            QuantizedWetness wetness,
            LegacyWetnessSurfaceCategory category)
        {
            switch (category)
            {
                case LegacyWetnessSurfaceCategory.Road:
                    return wetness.RoadWetness01;
                case LegacyWetnessSurfaceCategory.Vegetation:
                    return wetness.VegetationWetness01;
                default:
                    return wetness.GroundWetness01;
            }
        }

        private bool ApplySlot(
            Renderer renderer,
            SlotCache slot,
            float wetness01,
            float wetColorMultiplier,
            float wetSmoothness)
        {
            if (renderer == null)
            {
                return false;
            }

            wetness01 = Mathf.Clamp01(wetness01);
            Color wetColor = slot.BaseColor * wetColorMultiplier;
            wetColor.a = slot.BaseColor.a;
            Color outputColor = Color.Lerp(
                slot.BaseColor,
                wetColor,
                wetness01);
            float outputSmoothness = Mathf.Lerp(
                slot.BaseSmoothness,
                Mathf.Max(slot.BaseSmoothness, wetSmoothness),
                wetness01);

            propertyBlock.Clear();
            renderer.GetPropertyBlock(
                propertyBlock,
                slot.MaterialSlotIndex);
            propertyBlock.SetColor(BaseColorId, outputColor);
            propertyBlock.SetFloat(SmoothnessId, outputSmoothness);
            renderer.SetPropertyBlock(
                propertyBlock,
                slot.MaterialSlotIndex);
            propertyBlockWriteCount++;
            return true;
        }

        private void RestoreAllCachedSlots()
        {
            foreach (SceneCache cache in sceneCaches.Values)
            {
                RestoreScene(cache);
            }
        }

        private static void RestoreScene(SceneCache cache)
        {
            for (int rendererIndex = 0;
                 rendererIndex < cache.Renderers.Count;
                 rendererIndex++)
            {
                RendererCache rendererCache =
                    cache.Renderers[rendererIndex];
                if (rendererCache.Renderer == null)
                {
                    continue;
                }

                for (int slotIndex = 0;
                     slotIndex < rendererCache.Slots.Count;
                     slotIndex++)
                {
                    RestoreSlot(
                        rendererCache.Renderer,
                        rendererCache.Slots[slotIndex]);
                }
            }
        }

        private static void RestoreSlot(
            Renderer renderer,
            SlotCache slot)
        {
            renderer.SetPropertyBlock(
                slot.OriginalPropertyBlock.isEmpty
                    ? null
                    : slot.OriginalPropertyBlock,
                slot.MaterialSlotIndex);
        }

        private void EnsurePropertyBlock()
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        private void RecalculateCoverage()
        {
            int bindings = 0;
            int rejectedBindings = 0;
            int totalSlots = 0;
            int opaque = 0;
            int alphaClip = 0;
            int ground = 0;
            int road = 0;
            int exterior = 0;
            int vegetation = 0;
            int transparentOrWater = 0;
            int unlit = 0;
            int emissive = 0;
            int unsupported = 0;
            int notAllowlisted = 0;
            int categoryMismatch = 0;
            int inactivePresentation = 0;

            foreach (SceneCache cache in sceneCaches.Values)
            {
                bindings += cache.RegisteredBindingCount;
                rejectedBindings += cache.RejectedBindingCount;
                totalSlots += cache.TotalSlotCount;
                opaque += cache.OpaqueLitSlotCount;
                alphaClip += cache.AlphaClipLitSlotCount;
                ground += cache.ApprovedGroundSlotCount;
                road += cache.ApprovedRoadSlotCount;
                exterior += cache.ApprovedExteriorSlotCount;
                vegetation += cache.ApprovedVegetationSlotCount;
                transparentOrWater +=
                    cache.ExcludedTransparentOrWaterSlotCount;
                unlit += cache.ExcludedUnlitSlotCount;
                emissive += cache.ExcludedEmissiveSlotCount;
                unsupported += cache.ExcludedUnsupportedSlotCount;
                notAllowlisted += cache.ExcludedNotAllowlistedSlotCount;
                categoryMismatch +=
                    cache.ExcludedCategoryMismatchSlotCount;
                inactivePresentation +=
                    cache.ExcludedInactivePresentationSlotCount;
            }

            coverage = new LegacyWetnessCoverageCounters(
                sceneCaches.Count,
                bindings,
                rejectedBindings,
                totalSlots,
                opaque,
                alphaClip,
                ground,
                road,
                exterior,
                vegetation,
                transparentOrWater,
                unlit,
                emissive,
                unsupported,
                notAllowlisted,
                categoryMismatch,
                inactivePresentation);
        }

        private sealed class SceneCache
        {
            public List<RendererCache> Renderers { get; } =
                new List<RendererCache>();
            public int RegisteredBindingCount { get; set; }
            public int RejectedBindingCount { get; set; }
            public int TotalSlotCount { get; private set; }
            public int OpaqueLitSlotCount { get; private set; }
            public int AlphaClipLitSlotCount { get; private set; }
            public int ApprovedGroundSlotCount { get; private set; }
            public int ApprovedRoadSlotCount { get; private set; }
            public int ApprovedExteriorSlotCount { get; private set; }
            public int ApprovedVegetationSlotCount { get; private set; }
            public int ExcludedTransparentOrWaterSlotCount
            {
                get;
                private set;
            }
            public int ExcludedUnlitSlotCount { get; private set; }
            public int ExcludedEmissiveSlotCount { get; private set; }
            public int ExcludedUnsupportedSlotCount { get; private set; }
            public int ExcludedNotAllowlistedSlotCount { get; set; }
            public int ExcludedCategoryMismatchSlotCount { get; set; }
            public int ExcludedInactivePresentationSlotCount { get; set; }

            public void RecordShaderFamily(
                LegacyWetnessSlotCoverage slotCoverage)
            {
                TotalSlotCount++;
                switch (slotCoverage)
                {
                    case LegacyWetnessSlotCoverage.OpaqueLit:
                        OpaqueLitSlotCount++;
                        break;
                    case LegacyWetnessSlotCoverage.AlphaClipLit:
                        AlphaClipLitSlotCount++;
                        break;
                    case LegacyWetnessSlotCoverage
                        .ExcludedTransparentOrWater:
                        ExcludedTransparentOrWaterSlotCount++;
                        break;
                    case LegacyWetnessSlotCoverage.ExcludedUnlit:
                        ExcludedUnlitSlotCount++;
                        break;
                    case LegacyWetnessSlotCoverage.ExcludedEmissive:
                        ExcludedEmissiveSlotCount++;
                        break;
                    default:
                        ExcludedUnsupportedSlotCount++;
                        break;
                }
            }

            public void RecordApprovedCategory(
                LegacyWetnessSurfaceCategory category)
            {
                switch (category)
                {
                    case LegacyWetnessSurfaceCategory.Ground:
                        ApprovedGroundSlotCount++;
                        break;
                    case LegacyWetnessSurfaceCategory.Road:
                        ApprovedRoadSlotCount++;
                        break;
                    case LegacyWetnessSurfaceCategory.Exterior:
                        ApprovedExteriorSlotCount++;
                        break;
                    case LegacyWetnessSurfaceCategory.Vegetation:
                        ApprovedVegetationSlotCount++;
                        break;
                }
            }
        }

        private sealed class RendererCache
        {
            private readonly List<Material> sharedMaterials;

            public RendererCache(Renderer renderer, int materialCapacity)
            {
                Renderer = renderer;
                sharedMaterials = new List<Material>(materialCapacity);
                Slots = new List<SlotCache>(materialCapacity);
            }

            public Renderer Renderer { get; }
            public List<SlotCache> Slots { get; }

            public void RefreshSharedMaterials()
            {
                sharedMaterials.Clear();
                if (Renderer != null)
                {
                    Renderer.GetSharedMaterials(sharedMaterials);
                }
            }

            public bool IsExpectedMaterial(
                int slotIndex,
                Material expected) =>
                slotIndex >= 0 &&
                slotIndex < sharedMaterials.Count &&
                sharedMaterials[slotIndex] == expected;
        }

        private readonly struct SlotCache
        {
            public SlotCache(
                int materialSlotIndex,
                Material expectedMaterial,
                LegacyWetnessSurfaceCategory category,
                Color baseColor,
                float baseSmoothness,
                MaterialPropertyBlock originalPropertyBlock)
            {
                MaterialSlotIndex = materialSlotIndex;
                ExpectedMaterial = expectedMaterial;
                Category = category;
                BaseColor = baseColor;
                BaseSmoothness = baseSmoothness;
                OriginalPropertyBlock = originalPropertyBlock;
            }

            public int MaterialSlotIndex { get; }
            public Material ExpectedMaterial { get; }
            public LegacyWetnessSurfaceCategory Category { get; }
            public Color BaseColor { get; }
            public float BaseSmoothness { get; }
            public MaterialPropertyBlock OriginalPropertyBlock { get; }
        }

        private readonly struct QuantizedWetness :
            IEquatable<QuantizedWetness>
        {
            private QuantizedWetness(
                int steps,
                int ground,
                int road,
                int vegetation)
            {
                Steps = steps;
                Ground = ground;
                Road = road;
                Vegetation = vegetation;
            }

            public int Steps { get; }
            public int Ground { get; }
            public int Road { get; }
            public int Vegetation { get; }
            public float GroundWetness01 => Dequantize(Ground, Steps);
            public float RoadWetness01 => Dequantize(Road, Steps);
            public float VegetationWetness01 =>
                Dequantize(Vegetation, Steps);

            public static QuantizedWetness Create(
                in WetnessEnvironmentOutputs outputs,
                int requestedSteps)
            {
                int steps = Mathf.Max(1, requestedSteps);
                return new QuantizedWetness(
                    steps,
                    Quantize(outputs.GroundWetness01, steps),
                    Quantize(outputs.RoadWetness01, steps),
                    Quantize(outputs.VegetationWetness01, steps));
            }

            public bool Equals(QuantizedWetness other) =>
                Steps == other.Steps &&
                Ground == other.Ground &&
                Road == other.Road &&
                Vegetation == other.Vegetation;

            public override bool Equals(object obj) =>
                obj is QuantizedWetness other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Steps;
                    hash = (hash * 397) ^ Ground;
                    hash = (hash * 397) ^ Road;
                    hash = (hash * 397) ^ Vegetation;
                    return hash;
                }
            }

            private static int Quantize(float value, int steps) =>
                Mathf.Clamp(
                    Mathf.RoundToInt(Mathf.Clamp01(value) * steps),
                    0,
                    steps);

            private static float Dequantize(int value, int steps) =>
                value / (float)steps;
        }
    }
}
