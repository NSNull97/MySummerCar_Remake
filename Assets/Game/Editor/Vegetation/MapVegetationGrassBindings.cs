using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MSC.World.Vegetation;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MSC.Editor.Vegetation
{
    /// <summary>Private generated bindings for installed licensed grass; never edits the painter or vendor assets.</summary>
    public static class MapVegetationGrassBindings
    {
        public const string BindingVersion = "msc.map-vegetation-grass-bindings.v12";
        public const string Root = MapVegetationRebuildOptions.GeneratedRoot + "/Art/Grass";
        public const string ReportPath = "Artifacts/VegetationRebuild/GrassArt/provenance.json";
        public const int CoverageRasterResolution = 40;
        public const int MaximumCarpetClusterCopies = 16;
        public enum ClusterLayout { StaggeredSpiral, BlueNoiseDisk }
        private const string MeadowSourceRoot =
            "Assets/NatureManufacture Assets/Meadow Environment Dynamic Nature/Grass/Prefabs Grass/";
        private const string ForestSourceRoot =
            "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Foliage and Grass/Prefabs/";

        public static VegetationProfile[] BuildProfiles()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(VegetationAssetBuilder.ShaderPath);
            if (shader == null || shader.name != "MSC/HDRP/Vegetation Indirect")
                throw new InvalidOperationException("The existing MSC indirect vegetation shader is required.");
            Binding[] specs = CreateBindings();
            MixtureAudit mixture = AuditMixture(specs);
            if (!mixture.Passed)
                throw new InvalidDataException("The approved short-grass mixture is invalid: " +
                    mixture.Diagnostic);
            string runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var report = new BindingReport
            {
                generator = BindingVersion,
                runId = runId,
                selectionPolicy = "Weighted low northern carpet: Forest grass 02 narrow green blades cover 46% of the selector interval, Forest grass 01 broad arching blades cover 32%, and Forest grass 03 fine dry blades cover 22%. All three are reviewed short forms. Meadow 01/03 seed-head forms and Meadow 02 upright tussocks are excluded.",
                geometryPolicy = "Every active profile keeps the 0.80m saved root grid but expands one record into a progressive blue-noise carpet: sixteen authored clumps near, eight in the middle ring, and six in the far ring. The cheapest reviewed short source of each family is used in every LOD. All visible LODs are checked at median scale for projected span, occupied cells and maximum holes; geometry still becomes cheaper with distance.",
                visualAcceptance = "Inspect VisualAudit/02B_Grass_Carpet_Close.png after regeneration; automated geometry and image-integrity checks cannot decide whether a meadow looks natural.",
                candidateFamilies = CreateCandidateAudit(),
                approvedShortFamilyCount = mixture.DistinctShortFamilyCount,
                approvedDistinctNearSourceCount = mixture.DistinctNearSourceCount,
                approvedCerealOrSeedHeadSourceCount = mixture.CerealOrSeedHeadSourceCount,
                selectionIntervalWeightSum = mixture.SelectionWeightSum,
                mixtureGuardPassed = mixture.Passed,
                mixtureDiagnostic = mixture.Diagnostic
            };
            var profiles = new VegetationProfile[specs.Length];
            for (int index = 0; index < specs.Length; index++)
                profiles[index] = BuildProfile(specs[index], shader, runId, report);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            Debug.Log($"MAP_VEGETATION_GRASS_ART_OK profiles={profiles.Length} " +
                $"shortFamilies={mixture.DistinctShortFamilyCount} cerealSources={mixture.CerealOrSeedHeadSourceCount} " +
                "report=" + ReportPath);
            return profiles;
        }

        public static IReadOnlyList<BindingAudit> ApprovedBindings()
        {
            return Array.ConvertAll(CreateBindings(), binding => new BindingAudit(
                binding.Name, binding.NearSource, binding.MiddleSource, binding.FarSource,
                binding.Channel, binding.HeightRange, binding.Distances,
                binding.NearClusterCopies, binding.MiddleClusterCopies, binding.FarClusterCopies,
                binding.NearClusterSpreadNormalized, binding.MiddleClusterSpreadNormalized,
                binding.FarClusterSpreadNormalized,
                binding.NearClusterLayout, binding.Tint, binding.MinimumCutoff,
                binding.MaximumNearTriangles, binding.MinimumNearProjectedSpanMeters,
                binding.FootprintRadiusRangeMeters, binding.CoverageDiameterMeters,
                binding.CoverageProxyRadiusMeters, binding.CoverageAuditScaleMeters,
                binding.MinimumLodProjectedSpanMeters,
                binding.MinimumLodOccupiedCoverage,
                binding.MaximumLodEmptyRadiusMeters, binding.VisualFamily,
                binding.ReviewedForm, binding.IsShortCarpetSource,
                binding.ContainsCerealOrSeedHead, binding.SelectionRange));
        }

        public static MixtureAudit ApprovedMixtureAudit() => AuditMixture(CreateBindings());

        private static Binding[] CreateBindings()
        {
            return new[]
            {
                new Binding("NatureManufactureShort",
                    ForestSourceRoot + "prefab_grass_02_3.prefab",
                    ForestSourceRoot + "prefab_grass_02_3.prefab",
                    ForestSourceRoot + "prefab_grass_02_3.prefab",
                    VegetationDensityChannel.ShortGrass, new Vector2(0.16f, 0.24f),
                    1f, 35f, 0.12f, new Vector3(18f, 42f, 60f), 16, 8, 6,
                    1.95f, 2.15f, 2.55f,
                    ClusterLayout.BlueNoiseDisk, new Color(0.78f, 0.82f, 0.70f, 1f),
                    0.30f, 1024, 1.18f, new Vector2(0.72f, 1.00f),
                    0.80f, 0.035f, new Vector3(0.98f, 0.90f, 0.75f),
                    new Vector3(0.74f, 0.54f, 0.34f),
                    new Vector3(0.11f, 0.18f, 0.23f), 113,
                    "Forest grass 02", "Narrow green blades", true, false,
                    new Vector2(0f, 0.46f)),
                new Binding("NatureManufactureBroad",
                    ForestSourceRoot + "prefab_grass_01_3.prefab",
                    ForestSourceRoot + "prefab_grass_01_3.prefab",
                    ForestSourceRoot + "prefab_grass_01_3.prefab",
                    VegetationDensityChannel.MeadowGrass, new Vector2(0.15f, 0.22f),
                    1f, 38f, 0.14f, new Vector3(18f, 42f, 60f), 16, 8, 6,
                    2.35f, 2.45f, 2.75f,
                    ClusterLayout.BlueNoiseDisk, new Color(0.75f, 0.80f, 0.67f, 1f),
                    0.29f, 512, 1.10f, new Vector2(0.66f, 0.92f),
                    0.80f, 0.035f, new Vector3(0.94f, 0.86f, 0.72f),
                    new Vector3(0.66f, 0.48f, 0.31f),
                    new Vector3(0.14f, 0.20f, 0.24f), 227,
                    "Forest grass 01", "Broad arching blades", true, false,
                    new Vector2(0.46f, 0.78f)),
                new Binding("NatureManufactureDryFine",
                    ForestSourceRoot + "prefab_grass_03_3.prefab",
                    ForestSourceRoot + "prefab_grass_03_3.prefab",
                    ForestSourceRoot + "prefab_grass_03_3.prefab",
                    VegetationDensityChannel.TallGrass, new Vector2(0.14f, 0.20f),
                    1f, 42f, 0.15f, new Vector3(18f, 42f, 60f), 16, 8, 6,
                    2.55f, 2.65f, 2.95f,
                    ClusterLayout.BlueNoiseDisk, new Color(0.72f, 0.78f, 0.65f, 1f),
                    0.28f, 640, 1.05f, new Vector2(0.64f, 0.92f),
                    0.80f, 0.035f, new Vector3(0.90f, 0.82f, 0.70f),
                    new Vector3(0.56f, 0.43f, 0.28f),
                    new Vector3(0.17f, 0.22f, 0.25f), 349,
                    "Forest grass 03", "Fine dry bent blades", true, false,
                    new Vector2(0.78f, 1f))
            };
        }

        private static List<CandidateFamilyReport> CreateCandidateAudit()
        {
            return new List<CandidateFamilyReport>
            {
                new CandidateFamilyReport("Forest grass 01", ForestSourceRoot + "prefab_grass_01_*.prefab",
                    "SelectedBroadShort", "Broad arching blades add a low second silhouette without cereal heads; the v12 progressive carpet keeps 16/8/6 cheap clumps through near/middle/far LODs."),
                new CandidateFamilyReport("Forest grass 02", ForestSourceRoot + "prefab_grass_02_*.prefab",
                    "SelectedShort", "The reviewed 59-triangle narrow-blade clump supports 16/8/6 progressive copies while staying below the per-root LOD budgets."),
                new CandidateFamilyReport("Forest grass 03", ForestSourceRoot + "prefab_grass_03_*.prefab",
                    "SelectedDryFineShort", "Fine bent blades provide restrained dry colour variation; 16/8/6 progressive copies prevent the middle and far rings from collapsing into isolated speckles."),
                new CandidateFamilyReport("Meadow grass 01", MeadowSourceRoot + "prefab_grass_meadow_01_*.prefab",
                    "RejectedCerealSeedHeads", "Prominent upright seed heads read as grain/cereal planting and are excluded from every active LOD."),
                new CandidateFamilyReport("Meadow grass 02", MeadowSourceRoot + "prefab_grass_meadow_02_*.prefab",
                    "RejectedForActiveGrass", "Large upright tussocks produced the repeated weed-like clumps seen in the user playtest."),
                new CandidateFamilyReport("Meadow grass 03", MeadowSourceRoot + "prefab_grass_meadow_03_*.prefab",
                    "RejectedCerealSeedHeads", "The atlas contains visible panicles and long seed stems; it is not part of the short carpet mixture.")
            };
        }

        private static MixtureAudit AuditMixture(IReadOnlyList<Binding> bindings)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            Binding[] ordered = bindings.OrderBy(binding => binding.SelectionRange.x).ToArray();
            int shortFamilies = bindings.Where(binding => binding.IsShortCarpetSource)
                .Select(binding => binding.VisualFamily).Distinct(StringComparer.Ordinal).Count();
            int distinctNearSources = bindings.Select(binding => binding.NearSource)
                .Distinct(StringComparer.Ordinal).Count();
            int cerealSources = bindings.Count(binding => binding.ContainsCerealOrSeedHead);
            float weightSum = bindings.Sum(binding =>
                binding.SelectionRange.y - binding.SelectionRange.x);
            bool rangesContinuous = ordered.Length > 0 &&
                Mathf.Abs(ordered[0].SelectionRange.x) <= 0.0001f &&
                Mathf.Abs(ordered[ordered.Length - 1].SelectionRange.y - 1f) <= 0.0001f;
            for (int index = 0; index < ordered.Length; index++)
            {
                Vector2 range = ordered[index].SelectionRange;
                rangesContinuous &= float.IsFinite(range.x) && float.IsFinite(range.y) &&
                    range.y > range.x && range.x >= 0f && range.y <= 1f;
                if (index > 0)
                    rangesContinuous &= Mathf.Abs(ordered[index - 1].SelectionRange.y - range.x) <= 0.0001f;
            }
            bool allCarpetCoverage = bindings.All(binding =>
                binding.IsShortCarpetSource && binding.HeightRange.y <= 0.24f &&
                binding.NearClusterCopies == MaximumCarpetClusterCopies &&
                binding.MiddleClusterCopies >= 8 && binding.FarClusterCopies >= 6 &&
                binding.NearClusterLayout == ClusterLayout.BlueNoiseDisk &&
                binding.CoverageDiameterMeters >= 0.80f &&
                binding.CoverageProxyRadiusMeters > 0f);
            bool uniqueChannels = bindings.Select(binding => binding.Channel).Distinct().Count() == bindings.Count;
            bool passed = bindings.Count >= 3 && shortFamilies >= 3 &&
                distinctNearSources >= 3 && cerealSources == 0 &&
                Mathf.Abs(weightSum - 1f) <= 0.0001f && rangesContinuous &&
                allCarpetCoverage && uniqueChannels;
            string diagnostic = $"profiles={bindings.Count} shortFamilies={shortFamilies} " +
                $"distinctNearSources={distinctNearSources} cerealOrSeedHeadSources={cerealSources} " +
                $"selectionWeightSum={weightSum:R} rangesContinuous={rangesContinuous} " +
                $"allCarpetCoverage={allCarpetCoverage} uniqueChannels={uniqueChannels}";
            return new MixtureAudit(passed, shortFamilies, distinctNearSources,
                cerealSources, weightSum, diagnostic);
        }

        private static VegetationProfile BuildProfile(Binding binding, Shader shader, string runId, BindingReport report)
        {
            string[] sourcePaths = { binding.NearSource, binding.MiddleSource, binding.FarSource };
            GameObject[] prefabs = new GameObject[sourcePaths.Length];
            MeshRenderer[] renderers = new MeshRenderer[sourcePaths.Length];
            Material[] sourceMaterials = new Material[sourcePaths.Length];
            string[] textureProperties = new string[sourcePaths.Length];
            Texture2D[] sourceTextures = new Texture2D[sourcePaths.Length];
            for (int index = 0; index < sourcePaths.Length; index++)
            {
                prefabs[index] = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePaths[index]);
                if (prefabs[index] == null)
                    throw new FileNotFoundException("Reviewed NatureManufacture grass prefab is missing. Run the selective importer first.", sourcePaths[index]);
                renderers[index] = RequireSingleMaterialRenderer(prefabs[index], sourcePaths[index]);
                sourceMaterials[index] = renderers[index].sharedMaterial;
                textureProperties[index] = FindAlbedoProperty(sourceMaterials[index]);
                sourceTextures[index] = sourceMaterials[index].GetTexture(textureProperties[index]) as Texture2D;
                if (sourceTextures[index] == null)
                    throw new InvalidDataException("Grass alpha atlas is unavailable: " + sourcePaths[index]);
            }
            string texturePath = AssetDatabase.GetAssetPath(sourceTextures[0]);
            if (!File.Exists(texturePath)) throw new InvalidDataException("Grass atlas payload is unavailable: " + texturePath);
            for (int index = 1; index < sourceTextures.Length; index++)
                if (AssetDatabase.GetAssetPath(sourceTextures[index]) != texturePath)
                    throw new InvalidDataException("The selected grass LOD prefabs do not share one alpha atlas: " + binding.Name);
            Material sourceMaterial = sourceMaterials[0];
            string textureProperty = textureProperties[0];
            string folder = Root + "/" + binding.Name;
            EnsureFolder(folder);
            var entry = new ProfileReport
            {
                name = binding.Name, classification = "LicensedThirdPartyPhase1Presentation",
                visualFamily = binding.VisualFamily,
                reviewedForm = binding.ReviewedForm,
                isShortCarpetSource = binding.IsShortCarpetSource,
                containsCerealOrSeedHead = binding.ContainsCerealOrSeedHead,
                selectionRange = binding.SelectionRange,
                selectionIntervalWeight = binding.SelectionRange.y - binding.SelectionRange.x,
                sourcePrefab = string.Join(" | ", sourcePaths),
                prefabSha256 = string.Join(" | ", Array.ConvertAll(sourcePaths, HashFile)),
                dependencyHash = string.Join(" | ", Array.ConvertAll(sourcePaths,
                    path => AssetDatabase.GetAssetDependencyHash(path).ToString())),
                sourceMaterial = AssetDatabase.GetAssetPath(sourceMaterial),
                materialSha256 = HashFile(AssetDatabase.GetAssetPath(sourceMaterial)),
                sourceTexture = texturePath, textureSha256 = HashFile(texturePath),
                sourceTextureProperty = textureProperty, sourceLodCount = 3,
                farLodPolicy = "Uses reviewed short non-cereal blade prefabs as explicit near/middle/far geometry; the far ring retains six progressive carpet clumps so it cannot collapse into one visible speck.",
                clusterPolicy = binding.NearClusterCopies + "/" + binding.MiddleClusterCopies + "/" + binding.FarClusterCopies + " authored clumps in near/middle/far LOD at " +
                    binding.NearClusterSpreadNormalized.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "/" + binding.MiddleClusterSpreadNormalized.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "/" + binding.FarClusterSpreadNormalized.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " normalized-mesh near/middle radial spread; near layout=" + binding.NearClusterLayout,
                scaleRangeMeters = binding.HeightRange
            };
            Mesh near = null, middle = null, far = null;
            try
            {
                near = BakeMesh(prefabs[0].transform, renderers[0], out LodReport nearReport);
                middle = BakeMesh(prefabs[1].transform, renderers[1], out LodReport middleReport);
                far = BakeMesh(prefabs[2].transform, renderers[2], out LodReport farReport);
                float[] sourceHeights = { near.bounds.size.y, middle.bounds.size.y, far.bounds.size.y };
                for (int index = 0; index < sourceHeights.Length; index++)
                    if (!float.IsFinite(sourceHeights[index]) || sourceHeights[index] < 0.02f || sourceHeights[index] > 5f)
                        throw new InvalidDataException("Unexpected authored grass height: " + sourceHeights[index] + " in " + sourcePaths[index]);
                entry.authoredHeightMeters = sourceHeights[0];
                // Every reviewed source is independently normalised so profile
                // scale consistently means final blade/clump height in metres.
                NormalizeHeight(near, sourceHeights[0]);
                NormalizeHeight(middle, sourceHeights[1]);
                NormalizeHeight(far, sourceHeights[2]);
                near = BuildDenseCluster(near, binding.NearClusterCopies,
                    binding.NearClusterSpreadNormalized, binding.NearClusterLayout);
                middle = BuildDenseCluster(middle, binding.MiddleClusterCopies,
                    binding.MiddleClusterSpreadNormalized, ClusterLayout.BlueNoiseDisk);
                far = BuildDenseCluster(far, binding.FarClusterCopies,
                    binding.FarClusterSpreadNormalized, ClusterLayout.BlueNoiseDisk);
                nearReport.vertices = near.vertexCount;
                nearReport.triangles = TriangleCount(near);
                nearReport.clusterCopies = binding.NearClusterCopies;
                middleReport.vertices = middle.vertexCount;
                middleReport.triangles = TriangleCount(middle);
                middleReport.clusterCopies = binding.MiddleClusterCopies;
                farReport.vertices = far.vertexCount;
                farReport.triangles = TriangleCount(far);
                farReport.clusterCopies = binding.FarClusterCopies;
                nearReport.outputBounds = near.bounds;
                middleReport.outputBounds = middle.bounds;
                farReport.outputBounds = far.bounds;
                ValidateCarpetGeometry(binding, near, middle, far, nearReport, middleReport, farReport);
                entry.geometryGuardPassed = true;
                entry.nearProjectedMinimumSpanMeters = Mathf.Min(near.bounds.size.x, near.bounds.size.z) *
                    binding.HeightRange.y;
                entry.nearHorizontalAspectRatio = Mathf.Min(near.bounds.size.x, near.bounds.size.z) /
                    Mathf.Max(near.bounds.size.x, near.bounds.size.z);
                entry.maximumNearTriangleBudget = binding.MaximumNearTriangles;
                entry.minimumNearProjectedSpanMeters = binding.MinimumNearProjectedSpanMeters;
                entry.coverageRequired = binding.CoverageDiameterMeters > 0f;
                if (entry.coverageRequired)
                {
                    entry.coverageDiameterMeters = binding.CoverageDiameterMeters;
                    entry.coverageProxyRadiusMeters = binding.CoverageProxyRadiusMeters;
                    entry.coverageScaleMeters = binding.CoverageAuditScaleMeters;
                    Mesh[] lodMeshes = { near, middle, far };
                    string[] lodNames = { "near", "middle", "far" };
                    for (int lod = 0; lod < lodMeshes.Length; lod++)
                    {
                        ProjectedCoverageAudit coverage = MeasureProjectedCoverage(lodMeshes[lod],
                            binding.CoverageAuditScaleMeters, binding.CoverageDiameterMeters,
                            binding.CoverageProxyRadiusMeters, CoverageRasterResolution);
                        float minimumOccupied = Component(binding.MinimumLodOccupiedCoverage, lod);
                        float maximumEmpty = Component(binding.MaximumLodEmptyRadiusMeters, lod);
                        float span = Mathf.Min(lodMeshes[lod].bounds.size.x,
                            lodMeshes[lod].bounds.size.z) * binding.CoverageAuditScaleMeters;
                        float minimumSpan = Component(binding.MinimumLodProjectedSpanMeters, lod);
                        entry.lodCoverage.Add(new LodCoverageReport
                        {
                            lod = lodNames[lod], projectedSpanMeters = span,
                            minimumProjectedSpanMeters = minimumSpan,
                            occupiedCellFraction = coverage.OccupiedFraction,
                            minimumOccupiedCellFraction = minimumOccupied,
                            maximumEmptyRadiusMeters = coverage.MaximumEmptyRadiusMeters,
                            maximumEmptyRadiusBudgetMeters = maximumEmpty,
                            occupiedCells = coverage.OccupiedCells,
                            evaluatedCells = coverage.EvaluatedCells
                        });
                        if (span < minimumSpan || coverage.OccupiedFraction < minimumOccupied ||
                            coverage.MaximumEmptyRadiusMeters > maximumEmpty)
                            throw new InvalidDataException($"{binding.Name} {lodNames[lod]} projected carpet coverage failed at median scale {binding.CoverageAuditScaleMeters:F3}m: " +
                                $"span={span:F3}m (minimum {minimumSpan:F3}m), occupied={coverage.OccupiedFraction:P1} (minimum {minimumOccupied:P1}), " +
                                $"largest hole={coverage.MaximumEmptyRadiusMeters:F3}m (maximum {maximumEmpty:F3}m).");
                        if (lod != 0) continue;
                        entry.projectedOccupiedCellFraction = coverage.OccupiedFraction;
                        entry.projectedMaximumEmptyRadiusMeters = coverage.MaximumEmptyRadiusMeters;
                        entry.projectedOccupiedCells = coverage.OccupiedCells;
                        entry.projectedEvaluatedCells = coverage.EvaluatedCells;
                        entry.minimumProjectedOccupiedCellFraction = minimumOccupied;
                        entry.maximumProjectedEmptyRadiusMeters = maximumEmpty;
                    }
                }
                near = SaveMesh(near, folder + "/LOD0.asset", runId);
                middle = SaveMesh(middle, folder + "/LOD1.asset", runId);
                far = SaveMesh(far, folder + "/LOD2.asset", runId);
                entry.lods.Add(nearReport);
                entry.lods.Add(middleReport);
                entry.lods.Add(farReport);
                string copiedTexturePath = folder + "/AlbedoAlpha" + Path.GetExtension(texturePath);
                Texture2D texture = CopyAtlas(texturePath, copiedTexturePath, entry.textureSha256, runId);
                entry.outputTexture = copiedTexturePath;
                entry.outputTextureSha256 = HashFile(copiedTexturePath);
                string materialPath = folder + "/Indirect.mat";
                Backup(materialPath, runId);
                Material material = LoadOrCreate(materialPath, () => new Material(shader));
                material.shader = shader;
                material.name = binding.Name + "_Indirect";
                material.enableInstancing = true;
                material.doubleSidedGI = true;
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", sourceMaterial.GetTextureScale(textureProperty));
                material.SetTextureOffset("_BaseMap", sourceMaterial.GetTextureOffset(textureProperty));
                Color sourceTint = sourceMaterial.HasProperty("_BaseColor") ? sourceMaterial.GetColor("_BaseColor")
                    : sourceMaterial.HasProperty("_Color") ? sourceMaterial.GetColor("_Color") : Color.white;
                // Keep the source RGB identity, but remove the package's bright,
                // saturated demo-scene look with a project-owned boreal tint.
                // Blade transparency still comes exclusively from the source atlas.
                Color tint = new Color(sourceTint.r * binding.Tint.r,
                    sourceTint.g * binding.Tint.g, sourceTint.b * binding.Tint.b, 1f);
                material.SetColor("_BaseColor", tint);
                float sourceCutoff = sourceMaterial.HasProperty("_AlphaCutoff") ? sourceMaterial.GetFloat("_AlphaCutoff")
                    : sourceMaterial.HasProperty("_Cutoff") ? sourceMaterial.GetFloat("_Cutoff") : 0.3f;
                material.SetFloat("_Cutoff", Mathf.Clamp(Mathf.Max(sourceCutoff, binding.MinimumCutoff), 0.05f, 0.95f));
                material.SetFloat("_ColorVariation", 0.065f);
                material.SetFloat("_WindStrength", binding.Channel == VegetationDensityChannel.ShortGrass ? 0.025f : 0.045f);
                material.SetFloat("_WindSpeed", 1.5f);
                material.SetFloat("_WindFrequency", 0.17f);
                material.SetFloat("_Smoothness", 0.04f);
                material.SetColor("_SpecularColor", new Color(0.018f, 0.018f, 0.018f, 1f));
                material.SetColor("_EmissiveColor", Color.black);
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                entry.tint = tint;
                entry.alphaCutoff = material.GetFloat("_Cutoff");
                string profilePath = folder + "/Profile.asset";
                Backup(profilePath, runId);
                VegetationProfile profile = LoadOrCreate(profilePath, ScriptableObject.CreateInstance<VegetationProfile>);
                profile.name = "Map_" + binding.Name;
                profile.ConfigureForAuthoring("msc.map-grass." + binding.Channel.ToString().ToLowerInvariant(), binding.Channel,
                    0.8f, binding.Density, binding.MaximumSlope, new Vector2(-1000f, 2000f), binding.HeightRange,
                    binding.NormalAlignment, near, middle, far, material, binding.Distances, 6f,
                    ShadowCastingMode.Off,
                    true, binding.Seed);
                if (profile.ValidateConfiguration().Count > 0)
                    throw new InvalidDataException("Invalid generated grass profile: " + string.Join("; ", profile.ValidateConfiguration()));
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
                entry.footprintCategoryScaleAssumption = 1.08f;
                entry.footprintCategoryAlignmentAssumption = 1f;
                entry.maximumFootprintRadiusMeters = MeasureMaximumFootprintRadius(profile, 1.08f, 1f, 89f);
                if (entry.maximumFootprintRadiusMeters < binding.FootprintRadiusRangeMeters.x ||
                    entry.maximumFootprintRadiusMeters > binding.FootprintRadiusRangeMeters.y)
                    throw new InvalidDataException($"{binding.Name} footprint radius {entry.maximumFootprintRadiusMeters:F3}m is outside " +
                        $"the approved {binding.FootprintRadiusRangeMeters.x:F2}-{binding.FootprintRadiusRangeMeters.y:F2}m carpet range.");
                entry.minimumFootprintRadiusMeters = binding.FootprintRadiusRangeMeters.x;
                entry.maximumFootprintRadiusBudgetMeters = binding.FootprintRadiusRangeMeters.y;
                entry.outputProfile = profilePath;
                report.profiles.Add(entry);
                Debug.Log($"MAP_VEGETATION_GRASS_BINDING {binding.Name} sourceHeight={sourceHeights[0]:F3} " +
                    $"triangles={nearReport.triangles}/{middleReport.triangles}/{farReport.triangles} texture={texturePath}");
                return profile;
            }
            finally
            {
                if (near != null && !EditorUtility.IsPersistent(near)) Object.DestroyImmediate(near);
                if (middle != null && !EditorUtility.IsPersistent(middle)) Object.DestroyImmediate(middle);
                if (far != null && !EditorUtility.IsPersistent(far)) Object.DestroyImmediate(far);
            }
        }

        /// <summary>
        /// Conservative radius about a saved grass root across authored LODs,
        /// maximum packed scale, permitted surface tilt and the current shader's
        /// two horizontal wind waves. Does not shrink the grass to fit a margin.
        /// </summary>
        public static float MeasureMaximumFootprintRadius(VegetationProfile profile, MapVegetationCategorySettings category)
        {
            if (profile == null || category == null) throw new ArgumentNullException(profile == null ? nameof(profile) : nameof(category));
            return MeasureMaximumFootprintRadius(profile, category.UniformScaleRange.y, category.NormalAlignment, category.MaximumSlopeDegrees);
        }

        private static float MeasureMaximumFootprintRadius(VegetationProfile profile, float categoryScale, float categoryAlignment, float categorySlope)
        {
            // Migration may preserve a record that was selected by an older
            // profile. Only the category slope is guaranteed by TryResolveGrass.
            float slope = Mathf.Clamp(categorySlope, 0f, 89f) * Mathf.Deg2Rad;
            float alignment = Mathf.Clamp01(profile.SurfaceNormalAlignment * categoryAlignment);
            // Writer packs Slerp(up, groundNormal, alignment). Account
            // for packed normal quantisation with an extra 0.05 degrees.
            float tilt = slope * alignment + 0.05f * Mathf.Deg2Rad;
            float scale = profile.UniformScaleRange.y * categoryScale + 8f / 65535f;
            float wind = profile.Material != null && profile.Material.HasProperty("_WindStrength")
                ? Mathf.Abs(profile.Material.GetFloat("_WindStrength")) * Mathf.Sqrt(2f) : 0f;
            float maximum = 0f;
            var visited = new HashSet<Mesh>();
            for (int lod = 0; lod < 3; lod++)
            {
                Mesh mesh = profile.GetLodMesh(lod);
                if (mesh == null || !visited.Add(mesh)) continue;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    float horizontal = new Vector2(vertex.x, vertex.z).magnitude;
                    float height = Mathf.Abs(vertex.y);
                    float angle = Mathf.Min(tilt, Mathf.Atan2(height, horizontal));
                    float tiltedRadius = horizontal * Mathf.Cos(angle) + height * Mathf.Sin(angle);
                    float windWeight = Mathf.Clamp01(vertex.y);
                    maximum = Mathf.Max(maximum, tiltedRadius * scale + wind * windWeight * windWeight);
                }
            }
            return maximum;
        }

        private static MeshRenderer RequireSingleMaterialRenderer(GameObject prefab, string source)
        {
            LODGroup[] groups = prefab.GetComponentsInChildren<LODGroup>(true);
            MeshRenderer renderer;
            if (groups.Length == 1)
            {
                LOD[] lods = groups[0].GetLODs();
                if (lods.Length == 0 || lods[0].renderers.Length != 1 ||
                    !(lods[0].renderers[0] is MeshRenderer authored))
                    throw new InvalidDataException("Expected one renderer in authored grass LOD0: " + source);
                renderer = authored;
            }
            else
            {
                MeshRenderer[] candidates = prefab.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(candidate => candidate.enabled).ToArray();
                if (groups.Length != 0 || candidates.Length != 1)
                    throw new InvalidDataException("Expected one LODGroup or one renderer in authored grass prefab: " + source);
                renderer = candidates[0];
            }
            if (renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial == null)
                throw new InvalidDataException("Expected one material in authored grass renderer: " + source);
            Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || mesh.subMeshCount != 1 || mesh.GetTopology(0) != MeshTopology.Triangles)
                throw new InvalidDataException("Expected a single triangle submesh in authored grass prefab: " + source);
            return renderer;
        }

        private static string FindAlbedoProperty(Material material)
        {
            // AE/Leaves actively samples _Base_Color; its stale serialized
            // _MainTex slot is not the shader's albedo and must not win.
            foreach (string property in new[] { "_Base_Color", "_BaseColorMap", "_BaseMap", "_MainTex" })
                if (material.HasProperty(property) && material.GetTexture(property) is Texture2D) return property;
            throw new InvalidDataException("Grass material has no supported albedo/alpha atlas: " + material.name);
        }

        private static Mesh BakeMesh(Transform prefab, MeshRenderer renderer, out LodReport report)
        {
            Mesh source = renderer.GetComponent<MeshFilter>().sharedMesh;
            using Mesh.MeshDataArray snapshots = MeshUtility.AcquireReadOnlyMeshData(source);
            Mesh.MeshData data = snapshots[0];
            if (!data.HasVertexAttribute(VertexAttribute.TexCoord0))
                throw new InvalidDataException("Grass mesh has no UV0: " + source.name);
            using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            using var normals = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            using var uv = new NativeArray<Vector2>(data.vertexCount, Allocator.Temp);
            using var indices = new NativeArray<int>(data.GetSubMesh(0).indexCount, Allocator.Temp);
            data.GetVertices(vertices);
            bool hasNormals = data.HasVertexAttribute(VertexAttribute.Normal);
            if (hasNormals) data.GetNormals(normals);
            data.GetUVs(0, uv);
            data.GetIndices(indices, 0, true);
            Vector3[] outputVertices = vertices.ToArray(), outputNormals = normals.ToArray();
            int[] outputIndices = indices.ToArray();
            Matrix4x4 local = Matrix4x4.TRS(Vector3.zero, prefab.localRotation, prefab.localScale)
                * prefab.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            Matrix4x4 normalMatrix = local.inverse.transpose;
            for (int index = 0; index < outputVertices.Length; index++)
            {
                outputVertices[index] = local.MultiplyPoint3x4(outputVertices[index]);
                if (hasNormals) outputNormals[index] = normalMatrix.MultiplyVector(outputNormals[index]).normalized;
            }
            if (local.determinant < 0f)
                for (int index = 0; index < outputIndices.Length; index += 3)
                    (outputIndices[index + 1], outputIndices[index + 2]) = (outputIndices[index + 2], outputIndices[index + 1]);
            var mesh = new Mesh { name = source.name + "_MapIndirect", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = outputVertices;
            mesh.uv = uv.ToArray();
            mesh.triangles = outputIndices;
            if (hasNormals) mesh.normals = outputNormals; else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId);
            report = new LodReport { sourceMesh = AssetDatabase.GetAssetPath(source), sourceMeshName = source.name,
                sourceMeshGuid = guid, sourceMeshLocalId = localId, meshFileSha256 = HashFile(AssetDatabase.GetAssetPath(source)),
                vertices = mesh.vertexCount, triangles = outputIndices.Length / 3, bakedLocalTransform = local, authoredBounds = mesh.bounds };
            return mesh;
        }

        private static void NormalizeHeight(Mesh mesh, float sourceHeight)
        {
            Vector3[] vertices = mesh.vertices;
            for (int index = 0; index < vertices.Length; index++) vertices[index] /= sourceHeight;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static Mesh BuildDenseCluster(Mesh source, int copies, float radialSpread,
            ClusterLayout layout)
        {
            if (copies <= 1) return source;
            if (copies > MaximumCarpetClusterCopies || !float.IsFinite(radialSpread) || radialSpread <= 0f)
                throw new ArgumentOutOfRangeException(nameof(copies), "Grass cluster configuration is invalid.");
            if (layout == ClusterLayout.BlueNoiseDisk && copies < 4)
                throw new ArgumentException("A visible carpet LOD requires one centre and at least three progressive surrounding clumps.", nameof(copies));
            // Keep one authored clump on the saved grid point, then distribute
            // overlapping smaller copies. The golden-angle disk is progressive:
            // its four-copy prefix already covers all directions, while adding
            // copies fills the interior instead of creating a regular wreath.
            var combines = new CombineInstance[copies];
            combines[0] = new CombineInstance
            {
                mesh = source,
                subMeshIndex = 0,
                transform = Matrix4x4.identity
            };
            int ringCount = copies - 1;
            for (int index = 1; index < copies; index++)
            {
                float orbit = index * 137.50776f + 19f;
                Vector3 offset;
                if (layout == ClusterLayout.BlueNoiseDisk)
                {
                    float normalized = index / (float)(copies - 1);
                    float radians = (index * 137.50776f + 19f) * Mathf.Deg2Rad;
                    float radius = Mathf.Sqrt(normalized);
                    Vector2 point = new Vector2(Mathf.Cos(radians),
                        Mathf.Sin(radians)) * (radius * radialSpread);
                    offset = new Vector3(point.x, 0f, point.y);
                }
                else
                {
                    float normalized = ringCount <= 1 ? 1f : (index - 1f) / (ringCount - 1f);
                    float radians = orbit * Mathf.Deg2Rad;
                    float radius = radialSpread * Mathf.Lerp(0.42f, 1f, Mathf.Sqrt(normalized));
                    offset = new Vector3(Mathf.Cos(radians) * radius, 0f,
                        Mathf.Sin(radians) * radius);
                }
                float rotation = orbit + 31f + index * 17f;
                float scale = Mathf.Lerp(0.82f, 0.97f,
                    Mathf.Repeat(index * 0.61803398875f, 1f));
                combines[index] = new CombineInstance
                {
                    mesh = source,
                    subMeshIndex = 0,
                    transform = Matrix4x4.TRS(offset, Quaternion.Euler(0f, rotation, 0f), Vector3.one * scale)
                };
            }
            var cluster = new Mesh
            {
                name = source.name + "_DenseCluster" + copies,
                indexFormat = IndexFormat.UInt32
            };
            cluster.CombineMeshes(combines, true, true, false);
            cluster.RecalculateBounds();
            Object.DestroyImmediate(source);
            return cluster;
        }

        private static float Component(Vector3 value, int index) =>
            index == 0 ? value.x : index == 1 ? value.y : value.z;

        /// <summary>
        /// Rasterises projected card triangles into an isotropic target disk.
        /// The fixed proxy radius represents the visible alpha-cut blade width;
        /// it does not inflate the mesh bounds or count empty space between clumps.
        /// </summary>
        public static ProjectedCoverageAudit MeasureProjectedCoverage(Mesh mesh,
            float uniformScale, float diameterMeters, float proxyRadiusMeters,
            int resolution = CoverageRasterResolution)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (!mesh.isReadable) throw new InvalidDataException("Projected grass coverage requires a readable project-owned mesh.");
            if (!float.IsFinite(uniformScale) || uniformScale <= 0f ||
                !float.IsFinite(diameterMeters) || diameterMeters <= 0f ||
                !float.IsFinite(proxyRadiusMeters) || proxyRadiusMeters <= 0f ||
                resolution < 16 || resolution > 128)
                throw new ArgumentOutOfRangeException(nameof(diameterMeters), "Projected grass coverage configuration is invalid.");

            Vector3[] vertices = mesh.vertices;
            var triangleIndices = new List<int>();
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                if (mesh.GetTopology(subMesh) != MeshTopology.Triangles) continue;
                triangleIndices.AddRange(mesh.GetIndices(subMesh));
            }
            if (triangleIndices.Count < 3)
                return new ProjectedCoverageAudit(0f, diameterMeters * 0.5f, 0, 0);

            float targetRadius = diameterMeters * 0.5f;
            float cellSize = diameterMeters / resolution;
            float proxySquared = proxyRadiusMeters * proxyRadiusMeters;
            var samples = new List<Vector2>(resolution * resolution);
            var occupied = new List<bool>(resolution * resolution);
            int occupiedCount = 0;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                Vector2 sample = new Vector2(
                    (x + 0.5f) * cellSize - targetRadius,
                    (z + 0.5f) * cellSize - targetRadius);
                if (sample.sqrMagnitude > targetRadius * targetRadius) continue;
                bool filled = false;
                for (int triangle = 0; triangle + 2 < triangleIndices.Count; triangle += 3)
                {
                    Vector3 av = vertices[triangleIndices[triangle]] * uniformScale;
                    Vector3 bv = vertices[triangleIndices[triangle + 1]] * uniformScale;
                    Vector3 cv = vertices[triangleIndices[triangle + 2]] * uniformScale;
                    if (PointTriangleProjectionDistanceSquared(sample,
                        new Vector2(av.x, av.z), new Vector2(bv.x, bv.z),
                        new Vector2(cv.x, cv.z)) > proxySquared) continue;
                    filled = true;
                    break;
                }
                samples.Add(sample);
                occupied.Add(filled);
                if (filled) occupiedCount++;
            }

            if (occupiedCount == 0)
                return new ProjectedCoverageAudit(0f, targetRadius, 0, samples.Count);
            float maximumEmpty = 0f;
            for (int index = 0; index < samples.Count; index++)
            {
                if (occupied[index]) continue;
                float nearestSquared = float.PositiveInfinity;
                for (int candidate = 0; candidate < samples.Count; candidate++)
                {
                    if (!occupied[candidate]) continue;
                    nearestSquared = Mathf.Min(nearestSquared,
                        (samples[index] - samples[candidate]).sqrMagnitude);
                }
                maximumEmpty = Mathf.Max(maximumEmpty, Mathf.Sqrt(nearestSquared));
            }
            // Include the far corner of the raster cell instead of pretending
            // its centre represents the entire occupied or empty square.
            maximumEmpty += cellSize * 0.70710678f;
            return new ProjectedCoverageAudit(occupiedCount / (float)samples.Count,
                maximumEmpty, occupiedCount, samples.Count);
        }

        private static float PointTriangleProjectionDistanceSquared(Vector2 point,
            Vector2 a, Vector2 b, Vector2 c)
        {
            if (Mathf.Abs(Cross(b - a, c - a)) <= 1e-7f)
                return Mathf.Min(PointSegmentDistanceSquared(point, a, b),
                    Mathf.Min(PointSegmentDistanceSquared(point, b, c),
                        PointSegmentDistanceSquared(point, c, a)));
            float ab = Cross(b - a, point - a);
            float bc = Cross(c - b, point - b);
            float ca = Cross(a - c, point - c);
            bool hasNegative = ab < -1e-7f || bc < -1e-7f || ca < -1e-7f;
            bool hasPositive = ab > 1e-7f || bc > 1e-7f || ca > 1e-7f;
            if (!(hasNegative && hasPositive)) return 0f;
            return Mathf.Min(PointSegmentDistanceSquared(point, a, b),
                Mathf.Min(PointSegmentDistanceSquared(point, b, c),
                    PointSegmentDistanceSquared(point, c, a)));
        }

        private static float PointSegmentDistanceSquared(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float denominator = segment.sqrMagnitude;
            if (denominator <= 1e-10f) return (point - a).sqrMagnitude;
            float position = Mathf.Clamp01(Vector2.Dot(point - a, segment) / denominator);
            return (point - (a + segment * position)).sqrMagnitude;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public readonly struct ProjectedCoverageAudit
        {
            public float OccupiedFraction { get; }
            public float MaximumEmptyRadiusMeters { get; }
            public int OccupiedCells { get; }
            public int EvaluatedCells { get; }

            public ProjectedCoverageAudit(float occupiedFraction,
                float maximumEmptyRadiusMeters, int occupiedCells, int evaluatedCells)
            {
                OccupiedFraction = occupiedFraction;
                MaximumEmptyRadiusMeters = maximumEmptyRadiusMeters;
                OccupiedCells = occupiedCells;
                EvaluatedCells = evaluatedCells;
            }
        }

        public readonly struct MixtureAudit
        {
            public bool Passed { get; }
            public int DistinctShortFamilyCount { get; }
            public int DistinctNearSourceCount { get; }
            public int CerealOrSeedHeadSourceCount { get; }
            public float SelectionWeightSum { get; }
            public string Diagnostic { get; }

            public MixtureAudit(bool passed, int distinctShortFamilyCount,
                int distinctNearSourceCount, int cerealOrSeedHeadSourceCount,
                float selectionWeightSum, string diagnostic)
            {
                Passed = passed;
                DistinctShortFamilyCount = distinctShortFamilyCount;
                DistinctNearSourceCount = distinctNearSourceCount;
                CerealOrSeedHeadSourceCount = cerealOrSeedHeadSourceCount;
                SelectionWeightSum = selectionWeightSum;
                Diagnostic = diagnostic;
            }
        }

        private static int TriangleCount(Mesh mesh)
        {
            int triangles = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                triangles += (int)mesh.GetIndexCount(subMesh) / 3;
            return triangles;
        }

        private static void ValidateCarpetGeometry(Binding binding, Mesh near, Mesh middle, Mesh far,
            LodReport nearReport, LodReport middleReport, LodReport farReport)
        {
            if (binding.HeightRange.y > 0.5f)
                throw new InvalidDataException(binding.Name + " exceeds the reviewed 0.5m grass-carpet envelope.");
            foreach (Mesh mesh in new[] { near, middle, far })
                if (Mathf.Abs(mesh.bounds.size.y - 1f) > 0.001f)
                    throw new InvalidDataException(binding.Name + " has a non-normalized grass LOD height.");
            if (nearReport.triangles < 24 || nearReport.triangles > binding.MaximumNearTriangles)
                throw new InvalidDataException(binding.Name + " near LOD triangle budget is " +
                    nearReport.triangles + "; expected 24.." + binding.MaximumNearTriangles + ".");
            if (middleReport.triangles > nearReport.triangles || farReport.triangles > nearReport.triangles)
                throw new InvalidDataException(binding.Name + " does not reduce geometry outside its near ring.");
            float minimumHorizontal = Mathf.Min(near.bounds.size.x, near.bounds.size.z);
            float maximumHorizontal = Mathf.Max(near.bounds.size.x, near.bounds.size.z);
            float projectedSpan = minimumHorizontal * binding.HeightRange.y;
            if (projectedSpan < binding.MinimumNearProjectedSpanMeters)
                throw new InvalidDataException(binding.Name + " near grass patch spans only " + projectedSpan +
                    "m; it would read as isolated tufts on the 0.80m placement grid.");
            if (maximumHorizontal <= 0f || minimumHorizontal / maximumHorizontal < 0.35f)
                throw new InvalidDataException(binding.Name + " near grass patch is effectively a flat card.");
        }

        private static Mesh SaveMesh(Mesh mesh, string path, string runId)
        {
            Backup(path, runId);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                if (File.Exists(path)) throw new InvalidDataException("Unexpected asset at " + path);
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        private static Texture2D CopyAtlas(string source, string destination, string expectedHash, string runId)
        {
            Backup(destination, runId);
            if (!File.Exists(destination))
            {
                if (!AssetDatabase.CopyAsset(source, destination)) throw new IOException("Could not copy grass atlas " + source);
            }
            else if (HashFile(destination) != expectedHash)
            {
                // Preserve the generated asset's GUID while replacing only its
                // owned binary payload; the installed vendor file remains read-only.
                File.Copy(source, destination, true);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
            }
            var importer = AssetImporter.GetAtPath(destination) as TextureImporter;
            if (importer == null) throw new InvalidDataException("Grass atlas is not an imported texture: " + destination);
            if (importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.alphaIsTransparency || !importer.sRGBTexture)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
            Texture2D result = AssetDatabase.LoadAssetAtPath<Texture2D>(destination);
            if (result == null || HashFile(destination) != expectedHash)
                throw new InvalidDataException("Copied grass atlas failed its source hash check: " + destination);
            return result;
        }

        private static T LoadOrCreate<T>(string path, Func<T> create) where T : Object
        {
            T result = AssetDatabase.LoadAssetAtPath<T>(path);
            if (result != null) return result;
            if (File.Exists(path)) throw new InvalidDataException("Unexpected asset type at " + path);
            result = create();
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static void Backup(string path, string runId)
        {
            if (!path.StartsWith(Root + "/", StringComparison.Ordinal)) throw new InvalidOperationException("Grass output escaped its owned directory.");
            if (!File.Exists(path)) return;
            string destination = "Artifacts/VegetationRebuild/Backups/GrassArt/" + runId + "/" + path.Substring(Root.Length + 1);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(path, destination, false);
            if (File.Exists(path + ".meta")) File.Copy(path + ".meta", destination + ".meta", false);
        }

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private readonly struct Binding
        {
            public readonly string Name, NearSource, MiddleSource, FarSource,
                VisualFamily, ReviewedForm;
            public readonly VegetationDensityChannel Channel;
            public readonly Vector2 HeightRange;
            public readonly float Density, MaximumSlope, NormalAlignment;
            public readonly Vector3 Distances;
            public readonly int NearClusterCopies, MiddleClusterCopies, FarClusterCopies;
            public readonly float NearClusterSpreadNormalized, MiddleClusterSpreadNormalized,
                FarClusterSpreadNormalized;
            public readonly ClusterLayout NearClusterLayout;
            public readonly Color Tint;
            public readonly float MinimumCutoff;
            public readonly int MaximumNearTriangles;
            public readonly float MinimumNearProjectedSpanMeters;
            public readonly Vector2 FootprintRadiusRangeMeters;
            public readonly float CoverageDiameterMeters, CoverageProxyRadiusMeters,
                CoverageAuditScaleMeters;
            public readonly Vector3 MinimumLodProjectedSpanMeters,
                MinimumLodOccupiedCoverage, MaximumLodEmptyRadiusMeters;
            // Compatibility aliases for the current visual-audit DTO. They
            // intentionally mean near-ring thresholds; v12 generation and
            // regression tests validate all three vectors above.
            public float MinimumOccupiedCoverage => MinimumLodOccupiedCoverage.x;
            public float MaximumEmptyRadiusMeters => MaximumLodEmptyRadiusMeters.x;
            public readonly bool IsShortCarpetSource, ContainsCerealOrSeedHead;
            public readonly Vector2 SelectionRange;
            public readonly int Seed;
            public Binding(string name, string nearSource, string middleSource, string farSource,
                VegetationDensityChannel channel, Vector2 heightRange,
                float density, float maximumSlope, float normalAlignment, Vector3 distances,
                int nearClusterCopies, int middleClusterCopies, int farClusterCopies,
                float nearClusterSpreadNormalized, float middleClusterSpreadNormalized,
                float farClusterSpreadNormalized,
                ClusterLayout nearClusterLayout, Color tint, float minimumCutoff,
                int maximumNearTriangles, float minimumNearProjectedSpanMeters,
                Vector2 footprintRadiusRangeMeters, float coverageDiameterMeters,
                float coverageProxyRadiusMeters, Vector3 minimumLodProjectedSpanMeters,
                Vector3 minimumLodOccupiedCoverage,
                Vector3 maximumLodEmptyRadiusMeters, int seed, string visualFamily,
                string reviewedForm, bool isShortCarpetSource,
                bool containsCerealOrSeedHead, Vector2 selectionRange)
            { Name = name; NearSource = nearSource; MiddleSource = middleSource; FarSource = farSource;
                Channel = channel; HeightRange = heightRange; Density = density;
                MaximumSlope = maximumSlope; NormalAlignment = normalAlignment; Distances = distances;
                NearClusterCopies = nearClusterCopies; MiddleClusterCopies = middleClusterCopies;
                FarClusterCopies = farClusterCopies; NearClusterSpreadNormalized = nearClusterSpreadNormalized;
                MiddleClusterSpreadNormalized = middleClusterSpreadNormalized;
                FarClusterSpreadNormalized = farClusterSpreadNormalized;
                NearClusterLayout = nearClusterLayout;
                Tint = tint; MinimumCutoff = minimumCutoff; MaximumNearTriangles = maximumNearTriangles;
                MinimumNearProjectedSpanMeters = minimumNearProjectedSpanMeters;
                FootprintRadiusRangeMeters = footprintRadiusRangeMeters;
                CoverageDiameterMeters = coverageDiameterMeters;
                CoverageProxyRadiusMeters = coverageProxyRadiusMeters;
                CoverageAuditScaleMeters = (heightRange.x + heightRange.y) * 0.5f;
                MinimumLodProjectedSpanMeters = minimumLodProjectedSpanMeters;
                MinimumLodOccupiedCoverage = minimumLodOccupiedCoverage;
                MaximumLodEmptyRadiusMeters = maximumLodEmptyRadiusMeters;
                Seed = seed;
                VisualFamily = visualFamily; ReviewedForm = reviewedForm;
                IsShortCarpetSource = isShortCarpetSource;
                ContainsCerealOrSeedHead = containsCerealOrSeedHead;
                SelectionRange = selectionRange; }
        }

        public readonly struct BindingAudit
        {
            public readonly string Name, NearSource, MiddleSource, FarSource,
                VisualFamily, ReviewedForm;
            public readonly VegetationDensityChannel Channel;
            public readonly Vector2 HeightRange;
            public readonly Vector3 Distances;
            public readonly int NearClusterCopies, MiddleClusterCopies, FarClusterCopies;
            public readonly float NearClusterSpreadNormalized, MiddleClusterSpreadNormalized,
                FarClusterSpreadNormalized, MinimumCutoff;
            public readonly ClusterLayout NearClusterLayout;
            public readonly int MaximumNearTriangles;
            public readonly float MinimumNearProjectedSpanMeters;
            public readonly Vector2 FootprintRadiusRangeMeters;
            public readonly float CoverageDiameterMeters, CoverageProxyRadiusMeters,
                CoverageAuditScaleMeters;
            public readonly Vector3 MinimumLodProjectedSpanMeters,
                MinimumLodOccupiedCoverage, MaximumLodEmptyRadiusMeters;
            public float MinimumOccupiedCoverage => MinimumLodOccupiedCoverage.x;
            public float MaximumEmptyRadiusMeters => MaximumLodEmptyRadiusMeters.x;
            public readonly bool IsShortCarpetSource, ContainsCerealOrSeedHead;
            public readonly Vector2 SelectionRange;
            public readonly Color Tint;

            public BindingAudit(string name, string nearSource, string middleSource, string farSource,
                VegetationDensityChannel channel, Vector2 heightRange, Vector3 distances,
                int nearClusterCopies, int middleClusterCopies, int farClusterCopies,
                float nearClusterSpreadNormalized, float middleClusterSpreadNormalized,
                float farClusterSpreadNormalized,
                ClusterLayout nearClusterLayout, Color tint, float minimumCutoff,
                int maximumNearTriangles, float minimumNearProjectedSpanMeters,
                Vector2 footprintRadiusRangeMeters, float coverageDiameterMeters,
                float coverageProxyRadiusMeters, float coverageAuditScaleMeters,
                Vector3 minimumLodProjectedSpanMeters,
                Vector3 minimumLodOccupiedCoverage,
                Vector3 maximumLodEmptyRadiusMeters, string visualFamily,
                string reviewedForm, bool isShortCarpetSource,
                bool containsCerealOrSeedHead, Vector2 selectionRange)
            {
                Name = name; NearSource = nearSource; MiddleSource = middleSource; FarSource = farSource;
                Channel = channel; HeightRange = heightRange; Distances = distances;
                NearClusterCopies = nearClusterCopies; MiddleClusterCopies = middleClusterCopies;
                FarClusterCopies = farClusterCopies; NearClusterSpreadNormalized = nearClusterSpreadNormalized;
                MiddleClusterSpreadNormalized = middleClusterSpreadNormalized;
                FarClusterSpreadNormalized = farClusterSpreadNormalized;
                NearClusterLayout = nearClusterLayout;
                Tint = tint; MinimumCutoff = minimumCutoff; MaximumNearTriangles = maximumNearTriangles;
                MinimumNearProjectedSpanMeters = minimumNearProjectedSpanMeters;
                FootprintRadiusRangeMeters = footprintRadiusRangeMeters;
                CoverageDiameterMeters = coverageDiameterMeters;
                CoverageProxyRadiusMeters = coverageProxyRadiusMeters;
                CoverageAuditScaleMeters = coverageAuditScaleMeters;
                MinimumLodProjectedSpanMeters = minimumLodProjectedSpanMeters;
                MinimumLodOccupiedCoverage = minimumLodOccupiedCoverage;
                MaximumLodEmptyRadiusMeters = maximumLodEmptyRadiusMeters;
                VisualFamily = visualFamily; ReviewedForm = reviewedForm;
                IsShortCarpetSource = isShortCarpetSource;
                ContainsCerealOrSeedHead = containsCerealOrSeedHead;
                SelectionRange = selectionRange;
            }
        }
        [Serializable] private sealed class BindingReport
        {
            public string generator, runId, selectionPolicy, geometryPolicy,
                visualAcceptance, mixtureDiagnostic;
            public int approvedShortFamilyCount, approvedDistinctNearSourceCount,
                approvedCerealOrSeedHeadSourceCount;
            public float selectionIntervalWeightSum;
            public bool mixtureGuardPassed;
            public List<CandidateFamilyReport> candidateFamilies = new List<CandidateFamilyReport>();
            public List<ProfileReport> profiles = new List<ProfileReport>();
        }
        [Serializable] private sealed class LodCoverageReport
        {
            public string lod;
            public float projectedSpanMeters, minimumProjectedSpanMeters,
                occupiedCellFraction, minimumOccupiedCellFraction,
                maximumEmptyRadiusMeters, maximumEmptyRadiusBudgetMeters;
            public int occupiedCells, evaluatedCells;
        }
        [Serializable] private sealed class CandidateFamilyReport
        {
            public string family, pathPattern, status, rationale;
            public CandidateFamilyReport(string family, string pathPattern, string status, string rationale)
            { this.family = family; this.pathPattern = pathPattern; this.status = status; this.rationale = rationale; }
        }
        [Serializable] private sealed class ProfileReport
        {
            public string name, classification, sourcePrefab, prefabSha256, dependencyHash, sourceMaterial, materialSha256,
                sourceTexture, textureSha256, sourceTextureProperty, outputTexture, outputTextureSha256, outputProfile,
                farLodPolicy, clusterPolicy, visualFamily, reviewedForm;
            public int sourceLodCount;
            public float authoredHeightMeters, alphaCutoff, maximumFootprintRadiusMeters,
                footprintCategoryScaleAssumption, footprintCategoryAlignmentAssumption,
                nearProjectedMinimumSpanMeters, nearHorizontalAspectRatio, minimumNearProjectedSpanMeters,
                minimumFootprintRadiusMeters, maximumFootprintRadiusBudgetMeters,
                coverageDiameterMeters, coverageProxyRadiusMeters, coverageScaleMeters,
                projectedOccupiedCellFraction,
                projectedMaximumEmptyRadiusMeters, minimumProjectedOccupiedCellFraction,
                maximumProjectedEmptyRadiusMeters;
            public float selectionIntervalWeight;
            public int maximumNearTriangleBudget;
            public int projectedOccupiedCells, projectedEvaluatedCells;
            public bool geometryGuardPassed, coverageRequired, isShortCarpetSource,
                containsCerealOrSeedHead;
            public Vector2 scaleRangeMeters;
            public Vector2 selectionRange;
            public Color tint;
            public List<LodReport> lods = new List<LodReport>();
            public List<LodCoverageReport> lodCoverage = new List<LodCoverageReport>();
        }
        [Serializable] private sealed class LodReport
        {
            public string sourceMesh, sourceMeshName, sourceMeshGuid, meshFileSha256;
            public long sourceMeshLocalId;
            public int vertices, triangles, clusterCopies;
            public Matrix4x4 bakedLocalTransform;
            public Bounds authoredBounds, outputBounds;
        }
    }
}
