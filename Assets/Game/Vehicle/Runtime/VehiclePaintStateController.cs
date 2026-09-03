using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Donor-aligned Satsuma Paint/PaintType values. Zero deliberately means
    /// keeping the authored starting material (CAR_PAINT_RUSTY), not matte.
    /// </summary>
    public enum VehiclePaintType
    {
        NoChange = 0,
        Regular = 1,
        Metallic = 2,
        Matte = 3,
        Art = 4,
        Gt = 5,
        Gt2 = 6,
    }

    /// <summary>
    /// Stable vehicle-local identities matching the seven independent donor
    /// Paint FSM owners. These are save keys, never hierarchy-derived names.
    /// </summary>
    public static class SatsumaPaintSurfaceIds
    {
        public const string Body = "body";
        public const string DoorLeft = "door-left";
        public const string DoorRight = "door-right";
        public const string FenderLeft = "fender-left";
        public const string FenderRight = "fender-right";
        public const string Hood = "hood";
        public const string Bootlid = "bootlid";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Body,
            DoorLeft,
            DoorRight,
            FenderLeft,
            FenderRight,
            Hood,
            Bootlid,
        };
    }

    [Serializable]
    public sealed class VehiclePaintMaterialProfile
    {
        [SerializeField] private VehiclePaintType paintType;
        [SerializeField] private Material material;
        [SerializeField] private bool appliesBodyColor = true;

        public VehiclePaintMaterialProfile(
            VehiclePaintType configuredPaintType,
            Material configuredMaterial,
            bool shouldApplyBodyColor)
        {
            paintType = configuredPaintType;
            material = configuredMaterial;
            appliesBodyColor = shouldApplyBodyColor;
        }

        public VehiclePaintType PaintType => paintType;
        public Material Material => material;
        public bool AppliesBodyColor => appliesBodyColor;
    }

    [Serializable]
    public sealed class VehiclePaintSurfaceBinding
    {
        [SerializeField] private string surfaceId = string.Empty;
        [SerializeField] private Renderer renderer;
        [SerializeField] private int[] materialIndices = Array.Empty<int>();

        public VehiclePaintSurfaceBinding(
            Renderer targetRenderer,
            int[] targetMaterialIndices)
            : this(string.Empty, targetRenderer, targetMaterialIndices)
        {
        }

        public VehiclePaintSurfaceBinding(
            string configuredSurfaceId,
            Renderer targetRenderer,
            int[] targetMaterialIndices)
        {
            surfaceId = configuredSurfaceId ?? string.Empty;
            renderer = targetRenderer;
            materialIndices = targetMaterialIndices ?? Array.Empty<int>();
        }

        public string SurfaceId => surfaceId;
        public Renderer Renderer => renderer;
        public IReadOnlyList<int> MaterialIndices => materialIndices;
    }

    [Serializable]
    public sealed class VehiclePaintSurfaceSaveDto
    {
        public string surfaceId = string.Empty;
        public int paletteIndex = -1;
        public Color bodyColor = Color.white;
        public int paintType = (int)VehiclePaintType.NoChange;

        public bool TryValidate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(surfaceId) || surfaceId.Length > 96)
            {
                failure = "Vehicle paint surface identity is invalid.";
                return false;
            }

            return VehiclePaintSaveDto.TryValidatePaintValue(
                paletteIndex,
                bodyColor,
                paintType,
                out failure);
        }
    }

    [Serializable]
    public sealed class VehiclePaintSaveDto
    {
        public int paletteIndex = -1;
        public Color bodyColor = Color.white;
        // Matches the donor Paint/PaintType integer contract. Old JSON omits
        // this field and therefore deserializes to the correct NoChange value.
        public int paintType = (int)VehiclePaintType.NoChange;
        // Additive migration: old saves omit this and retain their global state.
        public VehiclePaintSurfaceSaveDto[] surfaces =
            Array.Empty<VehiclePaintSurfaceSaveDto>();

        public bool TryValidate(out string failure)
        {
            if (!TryValidatePaintValue(
                    paletteIndex,
                    bodyColor,
                    paintType,
                    out failure))
            {
                return false;
            }

            if (surfaces == null || surfaces.Length > 32)
            {
                failure = "Vehicle paint surface collection is invalid.";
                return false;
            }

            var observed = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < surfaces.Length; index++)
            {
                VehiclePaintSurfaceSaveDto surface = surfaces[index];
                if (surface == null || !surface.TryValidate(out failure))
                {
                    return false;
                }

                if (!observed.Add(surface.surfaceId))
                {
                    failure = "Vehicle paint surface identities are duplicated.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        internal static bool TryValidatePaintValue(
            int selectedPaletteIndex,
            Color selectedColor,
            int selectedPaintType,
            out string failure)
        {
            // Palette -1 is the backward-compatible authored-rust sentinel.
            if (selectedPaletteIndex == -1 &&
                selectedPaintType == (int)VehiclePaintType.NoChange)
            {
                failure = string.Empty;
                return true;
            }

            if (selectedPaletteIndex < 0 || selectedPaletteIndex > 255 ||
                !Enum.IsDefined(typeof(VehiclePaintType), selectedPaintType) ||
                !float.IsFinite(selectedColor.r) ||
                !float.IsFinite(selectedColor.g) ||
                !float.IsFinite(selectedColor.b) ||
                !float.IsFinite(selectedColor.a) ||
                selectedColor.r < 0f || selectedColor.r > 1f ||
                selectedColor.g < 0f || selectedColor.g > 1f ||
                selectedColor.b < 0f || selectedColor.b > 1f ||
                selectedColor.a < 0f || selectedColor.a > 1f)
            {
                failure = "Vehicle paint state is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Project-owned per-surface body-paint state. The seven mutable surfaces
    /// mirror the donor's separate Paint FSMs. Donor-derived materials remain
    /// shared and immutable; per-vehicle colours use material property blocks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehiclePaintStateController : MonoBehaviour
    {
        private const int LegacyIndexedGlobalSurfaceCount = 5;
        private const string LegacyIndexedSurfacePrefix = "legacy-surface-";

        private sealed class SurfaceState
        {
            public int PaletteIndex;
            public Color BodyColor;
            public VehiclePaintType PaintType;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Renderer[] paintableRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private VehiclePaintSurfaceBinding[] paintSurfaceBindings =
            Array.Empty<VehiclePaintSurfaceBinding>();
        [SerializeField] private VehiclePaintMaterialProfile[] paintMaterialProfiles =
            Array.Empty<VehiclePaintMaterialProfile>();
        [SerializeField] private int paletteIndex = -1;
        [SerializeField] private Color bodyColor = Color.white;
        [SerializeField] private VehiclePaintType paintType =
            VehiclePaintType.NoChange;
        [SerializeField] private bool hasPlayerSelectedPaint;

        private readonly Dictionary<string, SurfaceState> surfaceStates =
            new(StringComparer.Ordinal);
        private bool surfaceStatesInitialized;

        public int PaletteIndex => paletteIndex;
        public Color BodyColor => bodyColor;
        public VehiclePaintType PaintType => paintType;
        public bool HasPlayerSelectedPaint => hasPlayerSelectedPaint;
        public int PaintableRendererCount =>
            paintSurfaceBindings != null && paintSurfaceBindings.Length > 0
                ? paintSurfaceBindings.Length
                : paintableRenderers?.Length ?? 0;
        public IReadOnlyList<VehiclePaintSurfaceBinding> PaintSurfaceBindings =>
            paintSurfaceBindings ?? Array.Empty<VehiclePaintSurfaceBinding>();
        public IReadOnlyList<VehiclePaintMaterialProfile> PaintMaterialProfiles =>
            paintMaterialProfiles ?? Array.Empty<VehiclePaintMaterialProfile>();

        public void Configure(Renderer[] renderers)
        {
            paintableRenderers = renderers ?? Array.Empty<Renderer>();
            paintSurfaceBindings = Array.Empty<VehiclePaintSurfaceBinding>();
            ResetSurfaceStateCache();
        }

        public void Configure(VehiclePaintSurfaceBinding[] bindings)
        {
            paintSurfaceBindings = bindings ??
                Array.Empty<VehiclePaintSurfaceBinding>();
            paintableRenderers = Array.Empty<Renderer>();
            ResetSurfaceStateCache();
        }

        public void Configure(
            VehiclePaintSurfaceBinding[] bindings,
            VehiclePaintMaterialProfile[] profiles)
        {
            Configure(bindings);
            paintMaterialProfiles = profiles ??
                Array.Empty<VehiclePaintMaterialProfile>();
        }

        public bool TryApplyPlayerSelectedPaint(
            int selectedPaletteIndex,
            Color selectedColor,
            out string failure) =>
            TryApplyPaint(
                selectedPaletteIndex,
                selectedColor,
                VehiclePaintType.NoChange,
                out failure);

        /// <summary>
        /// Applies one coating to every configured surface. This is the donor
        /// whole-car paint path and the migration path for old global saves.
        /// </summary>
        public bool TryApplyPaint(
            int selectedPaletteIndex,
            Color selectedColor,
            VehiclePaintType selectedPaintType,
            out string failure)
        {
            if (!VehiclePaintSaveDto.TryValidatePaintValue(
                    selectedPaletteIndex,
                    selectedColor,
                    (int)selectedPaintType,
                    out failure) ||
                !TryResolveProfile(selectedPaintType, out var profile, out failure))
            {
                return false;
            }

            paletteIndex = selectedPaletteIndex;
            bodyColor = selectedColor;
            paintType = selectedPaintType;
            hasPlayerSelectedPaint = true;

            EnsureSurfaceStateCache();
            if (paintSurfaceBindings != null && paintSurfaceBindings.Length > 0)
            {
                for (int index = 0; index < paintSurfaceBindings.Length; index++)
                {
                    VehiclePaintSurfaceBinding binding = paintSurfaceBindings[index];
                    string surfaceId = ResolveSurfaceId(binding, index);
                    SurfaceState state = surfaceStates[surfaceId];
                    state.PaletteIndex = selectedPaletteIndex;
                    state.BodyColor = selectedColor;
                    state.PaintType = selectedPaintType;
                    ApplyBinding(binding, profile, selectedColor);
                }

                failure = string.Empty;
                return true;
            }

            ApplyLegacyRenderers(profile, selectedColor);
            failure = string.Empty;
            return true;
        }

        /// <summary>
        /// Applies Fleetari or future spray paint to exactly one donor surface.
        /// The spray input workflow intentionally remains outside this class so
        /// Cheap Car Repair-style painting can be integrated later.
        /// </summary>
        public bool TryApplySurfacePaint(
            string surfaceId,
            int selectedPaletteIndex,
            Color selectedColor,
            VehiclePaintType selectedPaintType,
            out string failure)
        {
            var candidate = new VehiclePaintSurfaceSaveDto
            {
                surfaceId = surfaceId ?? string.Empty,
                paletteIndex = selectedPaletteIndex,
                bodyColor = selectedColor,
                paintType = (int)selectedPaintType,
            };
            if (!candidate.TryValidate(out failure) ||
                !TryResolveProfile(selectedPaintType, out var profile, out failure))
            {
                return false;
            }

            EnsureSurfaceStateCache();
            if (!TryFindBinding(surfaceId, out VehiclePaintSurfaceBinding binding))
            {
                failure = $"Vehicle paint surface '{surfaceId}' is not configured.";
                return false;
            }

            SurfaceState state = surfaceStates[surfaceId];
            state.PaletteIndex = selectedPaletteIndex;
            state.BodyColor = selectedColor;
            state.PaintType = selectedPaintType;
            ApplyBinding(binding, profile, selectedColor);
            hasPlayerSelectedPaint = true;
            SyncAggregateStateFromBody();
            failure = string.Empty;
            return true;
        }

        public bool TryGetSurfaceState(
            string surfaceId,
            out int selectedPaletteIndex,
            out Color selectedColor,
            out VehiclePaintType selectedPaintType)
        {
            EnsureSurfaceStateCache();
            if (!string.IsNullOrWhiteSpace(surfaceId) &&
                surfaceStates.TryGetValue(surfaceId, out SurfaceState state))
            {
                selectedPaletteIndex = state.PaletteIndex;
                selectedColor = state.BodyColor;
                selectedPaintType = state.PaintType;
                return true;
            }

            selectedPaletteIndex = -1;
            selectedColor = Color.white;
            selectedPaintType = VehiclePaintType.NoChange;
            return false;
        }

        public VehiclePaintSaveDto CaptureSaveData()
        {
            if (!hasPlayerSelectedPaint)
            {
                return null;
            }

            EnsureSurfaceStateCache();
            VehiclePaintSurfaceSaveDto[] savedSurfaces =
                paintSurfaceBindings != null && paintSurfaceBindings.Length > 0
                    ? new VehiclePaintSurfaceSaveDto[paintSurfaceBindings.Length]
                    : Array.Empty<VehiclePaintSurfaceSaveDto>();
            for (int index = 0; index < savedSurfaces.Length; index++)
            {
                string surfaceId = ResolveSurfaceId(
                    paintSurfaceBindings[index],
                    index);
                SurfaceState state = surfaceStates[surfaceId];
                savedSurfaces[index] = new VehiclePaintSurfaceSaveDto
                {
                    surfaceId = surfaceId,
                    paletteIndex = state.PaletteIndex,
                    bodyColor = state.BodyColor,
                    paintType = (int)state.PaintType,
                };
            }

            return new VehiclePaintSaveDto
            {
                paletteIndex = paletteIndex,
                bodyColor = bodyColor,
                paintType = (int)paintType,
                surfaces = savedSurfaces,
            };
        }

        public bool TryRestore(VehiclePaintSaveDto dto, out string failure)
        {
            // A pre-paint save has no payload. Preserve the authored donor
            // appearance instead of repainting an existing playthrough.
            if (dto == null ||
                dto.paletteIndex == -1 &&
                (dto.surfaces == null || dto.surfaces.Length == 0))
            {
                failure = string.Empty;
                return true;
            }

            if (!dto.TryValidate(out failure))
            {
                return false;
            }

            VehiclePaintSurfaceSaveDto[] restoredSurfaces =
                dto.surfaces ?? Array.Empty<VehiclePaintSurfaceSaveDto>();
            if (restoredSurfaces.Length == 0)
            {
                return TryApplyPaint(
                    dto.paletteIndex,
                    dto.bodyColor,
                    (VehiclePaintType)dto.paintType,
                    out failure);
            }

            if (!AreAllSavedSurfacesConfigured(restoredSurfaces) &&
                ContainsLegacyIndexedSurface(restoredSurfaces))
            {
                return TryRestoreLegacyIndexedGlobalPaint(
                    dto,
                    restoredSurfaces,
                    out failure);
            }

            EnsureSurfaceStateCache();
            var resolvedBindings = new VehiclePaintSurfaceBinding[
                restoredSurfaces.Length];
            var resolvedProfiles = new VehiclePaintMaterialProfile[
                restoredSurfaces.Length];
            for (int index = 0; index < restoredSurfaces.Length; index++)
            {
                VehiclePaintSurfaceSaveDto surface = restoredSurfaces[index];
                if (!TryFindBinding(surface.surfaceId, out resolvedBindings[index]))
                {
                    failure = $"Vehicle paint surface '{surface.surfaceId}' is not configured.";
                    return false;
                }

                if (!TryResolveProfile(
                        (VehiclePaintType)surface.paintType,
                        out resolvedProfiles[index],
                        out failure))
                {
                    return false;
                }
            }

            for (int index = 0; index < restoredSurfaces.Length; index++)
            {
                VehiclePaintSurfaceSaveDto surface = restoredSurfaces[index];
                SurfaceState state = surfaceStates[surface.surfaceId];
                state.PaletteIndex = surface.paletteIndex;
                state.BodyColor = surface.bodyColor;
                state.PaintType = (VehiclePaintType)surface.paintType;
                ApplyBinding(
                    resolvedBindings[index],
                    resolvedProfiles[index],
                    surface.bodyColor);
            }

            paletteIndex = dto.paletteIndex;
            bodyColor = dto.bodyColor;
            paintType = (VehiclePaintType)dto.paintType;
            hasPlayerSelectedPaint = true;
            failure = string.Empty;
            return true;
        }

        private bool TryRestoreLegacyIndexedGlobalPaint(
            VehiclePaintSaveDto dto,
            IReadOnlyList<VehiclePaintSurfaceSaveDto> restoredSurfaces,
            out string failure)
        {
            // One predecessor prefab exposed five renderer-index identities even
            // though paint selection was still whole-car state. Only migrate
            // that exact, lossless shape. Independently painted or malformed
            // indexed surfaces remain rejected because their old renderer order
            // cannot be mapped safely to the seven stable donor surface IDs.
            if (restoredSurfaces.Count != LegacyIndexedGlobalSurfaceCount)
            {
                failure =
                    "Legacy indexed vehicle paint does not match the known five-surface layout.";
                return false;
            }

            for (int expectedIndex = 0;
                 expectedIndex < LegacyIndexedGlobalSurfaceCount;
                 expectedIndex++)
            {
                string expectedId = LegacyIndexedSurfacePrefix + expectedIndex;
                VehiclePaintSurfaceSaveDto matchedSurface = null;
                for (int index = 0; index < restoredSurfaces.Count; index++)
                {
                    VehiclePaintSurfaceSaveDto candidate = restoredSurfaces[index];
                    if (string.Equals(
                            candidate.surfaceId,
                            expectedId,
                            StringComparison.Ordinal))
                    {
                        matchedSurface = candidate;
                        break;
                    }
                }

                if (matchedSurface == null)
                {
                    failure =
                        "Legacy indexed vehicle paint surface identities are incomplete.";
                    return false;
                }

                if (matchedSurface.paletteIndex != dto.paletteIndex ||
                    matchedSurface.paintType != dto.paintType ||
                    !ColorsApproximatelyEqual(
                        matchedSurface.bodyColor,
                        dto.bodyColor))
                {
                    failure =
                        "Independent legacy vehicle paint surfaces cannot be migrated safely.";
                    return false;
                }
            }

            return TryApplyPaint(
                dto.paletteIndex,
                dto.bodyColor,
                (VehiclePaintType)dto.paintType,
                out failure);
        }

        private bool AreAllSavedSurfacesConfigured(
            IReadOnlyList<VehiclePaintSurfaceSaveDto> restoredSurfaces)
        {
            for (int index = 0; index < restoredSurfaces.Count; index++)
            {
                if (!TryFindBinding(restoredSurfaces[index].surfaceId, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsLegacyIndexedSurface(
            IReadOnlyList<VehiclePaintSurfaceSaveDto> restoredSurfaces)
        {
            for (int index = 0; index < restoredSurfaces.Count; index++)
            {
                if (restoredSurfaces[index].surfaceId.StartsWith(
                        LegacyIndexedSurfacePrefix,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ColorsApproximatelyEqual(Color left, Color right) =>
            Mathf.Approximately(left.r, right.r) &&
            Mathf.Approximately(left.g, right.g) &&
            Mathf.Approximately(left.b, right.b) &&
            Mathf.Approximately(left.a, right.a);

        private bool TryResolveProfile(
            VehiclePaintType selectedPaintType,
            out VehiclePaintMaterialProfile profile,
            out string failure)
        {
            profile = FindProfile(selectedPaintType);
            if (paintMaterialProfiles != null &&
                paintMaterialProfiles.Length > 0 &&
                (profile == null || profile.Material == null))
            {
                failure =
                    $"Vehicle paint material '{selectedPaintType}' is not configured.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private VehiclePaintMaterialProfile FindProfile(
            VehiclePaintType selectedPaintType)
        {
            if (paintMaterialProfiles == null)
            {
                return null;
            }

            for (int index = 0; index < paintMaterialProfiles.Length; index++)
            {
                VehiclePaintMaterialProfile profile = paintMaterialProfiles[index];
                if (profile != null && profile.PaintType == selectedPaintType)
                {
                    return profile;
                }
            }

            return null;
        }

        private void EnsureSurfaceStateCache()
        {
            if (surfaceStatesInitialized)
            {
                return;
            }

            surfaceStates.Clear();
            Color initialSurfaceColor = bodyColor;
            if (paletteIndex == -1 && paintType == VehiclePaintType.NoChange)
            {
                VehiclePaintMaterialProfile authoredProfile =
                    FindProfile(VehiclePaintType.NoChange);
                if (authoredProfile?.Material != null &&
                    authoredProfile.Material.HasProperty(BaseColorId))
                {
                    initialSurfaceColor = authoredProfile.Material.GetColor(
                        BaseColorId);
                }
            }
            if (paintSurfaceBindings != null)
            {
                for (int index = 0; index < paintSurfaceBindings.Length; index++)
                {
                    string surfaceId = ResolveSurfaceId(
                        paintSurfaceBindings[index],
                        index);
                    if (!surfaceStates.TryAdd(
                            surfaceId,
                            new SurfaceState
                            {
                                PaletteIndex = paletteIndex,
                                BodyColor = initialSurfaceColor,
                                PaintType = paintType,
                            }))
                    {
                        throw new InvalidOperationException(
                            $"Vehicle paint surface '{surfaceId}' is configured more than once.");
                    }
                }
            }

            surfaceStatesInitialized = true;
        }

        private void ResetSurfaceStateCache()
        {
            surfaceStates.Clear();
            surfaceStatesInitialized = false;
        }

        private bool TryFindBinding(
            string surfaceId,
            out VehiclePaintSurfaceBinding binding)
        {
            binding = null;
            if (paintSurfaceBindings == null ||
                string.IsNullOrWhiteSpace(surfaceId))
            {
                return false;
            }

            for (int index = 0; index < paintSurfaceBindings.Length; index++)
            {
                VehiclePaintSurfaceBinding candidate = paintSurfaceBindings[index];
                if (string.Equals(
                        ResolveSurfaceId(candidate, index),
                        surfaceId,
                        StringComparison.Ordinal))
                {
                    binding = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string ResolveSurfaceId(
            VehiclePaintSurfaceBinding binding,
            int index) =>
            binding != null && !string.IsNullOrWhiteSpace(binding.SurfaceId)
                ? binding.SurfaceId
                : $"legacy-surface-{index}";

        private void SyncAggregateStateFromBody()
        {
            if (!surfaceStates.TryGetValue(
                    SatsumaPaintSurfaceIds.Body,
                    out SurfaceState aggregate) &&
                paintSurfaceBindings != null &&
                paintSurfaceBindings.Length > 0)
            {
                surfaceStates.TryGetValue(
                    ResolveSurfaceId(paintSurfaceBindings[0], 0),
                    out aggregate);
            }

            if (aggregate == null)
            {
                return;
            }

            paletteIndex = aggregate.PaletteIndex;
            bodyColor = aggregate.BodyColor;
            paintType = aggregate.PaintType;
        }

        private static void ApplyBinding(
            VehiclePaintSurfaceBinding binding,
            VehiclePaintMaterialProfile profile,
            Color selectedColor)
        {
            if (binding == null || binding.Renderer == null)
            {
                return;
            }

            Material[] materials = binding.Renderer.sharedMaterials;
            IReadOnlyList<int> indices = binding.MaterialIndices;
            bool changed = false;
            for (int index = 0; index < indices.Count; index++)
            {
                int materialIndex = indices[index];
                if (materialIndex < 0 || materialIndex >= materials.Length)
                {
                    continue;
                }

                if (profile?.Material != null)
                {
                    materials[materialIndex] = profile.Material;
                    changed = true;
                }
            }

            if (changed)
            {
                binding.Renderer.sharedMaterials = materials;
            }

            var block = new MaterialPropertyBlock();
            for (int index = 0; index < indices.Count; index++)
            {
                ApplyPropertyBlock(
                    binding.Renderer,
                    indices[index],
                    block,
                    selectedColor,
                    profile == null || profile.AppliesBodyColor);
            }
        }

        private void ApplyLegacyRenderers(
            VehiclePaintMaterialProfile profile,
            Color selectedColor)
        {
            var block = new MaterialPropertyBlock();
            for (int rendererIndex = 0;
                 rendererIndex < (paintableRenderers?.Length ?? 0);
                 rendererIndex++)
            {
                Renderer target = paintableRenderers[rendererIndex];
                if (target == null)
                {
                    continue;
                }

                Material[] materials = target.sharedMaterials;
                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    if (profile?.Material != null)
                    {
                        materials[materialIndex] = profile.Material;
                    }
                }

                if (profile?.Material != null)
                {
                    target.sharedMaterials = materials;
                }

                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    ApplyPropertyBlock(
                        target,
                        materialIndex,
                        block,
                        selectedColor,
                        profile == null || profile.AppliesBodyColor);
                }
            }
        }

        private static void ApplyPropertyBlock(
            Renderer target,
            int materialIndex,
            MaterialPropertyBlock block,
            Color selectedColor,
            bool applyBodyColor)
        {
            Material[] materials = target.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length)
            {
                return;
            }

            Material material = materials[materialIndex];
            if (material == null)
            {
                return;
            }

            if (!applyBodyColor)
            {
                target.SetPropertyBlock(null, materialIndex);
                return;
            }

            target.GetPropertyBlock(block, materialIndex);
            if (material.HasProperty(BaseColorId))
            {
                block.SetColor(BaseColorId, selectedColor);
            }
            if (material.HasProperty(LegacyColorId))
            {
                block.SetColor(LegacyColorId, selectedColor);
            }
            target.SetPropertyBlock(block, materialIndex);
            block.Clear();
        }
    }
}
