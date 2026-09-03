using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace MSC.Editor.Vegetation
{
    /// <summary>
    /// Builds project-owned Unity 6 HDRP wrappers for reviewed forest-floor art.
    /// Rocks deliberately remain separate from the plant/organic-accent pool:
    /// the existing shrub writer normalises ordinary prefabs to a plant height
    /// and must not receive rocks.
    /// </summary>
    public static class MapVegetationForestFloorBindings
    {
        public enum Pool
        {
            Understory,
            Shrubs,
            RocksAndBoulders
        }

        public readonly struct PlacementPolicy
        {
            public readonly float MinimumSpacingMeters;
            public readonly float Density;
            public readonly float MaximumSlopeDegrees;
            public readonly float NormalAlignment;
            public readonly Vector2 UniformScaleRange;
            public readonly float SurfaceOffsetMeters;
            public readonly bool UsesSourceHeight;

            public PlacementPolicy(float spacing, float density, float slope, float alignment,
                Vector2 scaleRange, float surfaceOffset)
            {
                MinimumSpacingMeters = spacing;
                Density = density;
                MaximumSlopeDegrees = slope;
                NormalAlignment = alignment;
                UniformScaleRange = scaleRange;
                SurfaceOffsetMeters = surfaceOffset;
                // This is the contract which keeps a boulder from being resized
                // to the current shrub target height of 0.35-3 metres.
                UsesSourceHeight = true;
            }
        }

        public const string Root = MapVegetationRebuildOptions.GeneratedRoot + "/Art/ForestFloor";
        public const string ReportPath = "Artifacts/VegetationRebuild/ForestFloor/provenance.json";
        public const string AlpAuditPath = "Artifacts/VegetationImport/ALPSpruceTreesPack.json";
        public const string NatureManufactureAuditPath = "Artifacts/VegetationImport/NatureManufactureFinnishSubset.json";
        private const string BindingVersion = "msc.map-vegetation-forest-floor-bindings.v5";

        private const string AlpRoot =
            "Assets/Game/Presentation/Vegetation/ThirdParty/ALPSpruceTreesPack/Source/Prefabs/";
        private const string ForestRoot =
            "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/";
        private const string MeadowRoot =
            "Assets/NatureManufacture Assets/Meadow Environment Dynamic Nature/";

        private static readonly Source[] Sources =
        {
            // Large rocks retain one project-owned primitive collider. Small
            // stones are presentation-only, avoiding five mesh colliders per LOD.
            new Source(Pool.RocksAndBoulders, "ALP_Rock01", AlpRoot + "Rock01_Pref.prefab", 4,
                Surface.OpaqueRock, true, "ThirdPartyPrivatePhase1"),
            new Source(Pool.RocksAndBoulders, "ALP_Rock02", AlpRoot + "Rock02_pref.prefab", 4,
                Surface.OpaqueRock, true, "ThirdPartyPrivatePhase1"),
            new Source(Pool.RocksAndBoulders, "ALP_Rock03", AlpRoot + "Rock03_pref.prefab", 2,
                Surface.OpaqueRock, true, "ThirdPartyPrivatePhase1"),
            new Source(Pool.RocksAndBoulders, "ALP_Stone01", AlpRoot + "stone01.prefab", 7,
                Surface.OpaqueRock, false, "ThirdPartyPrivatePhase1"),
            new Source(Pool.RocksAndBoulders, "ALP_Stone02", AlpRoot + "stone02.prefab", 7,
                Surface.OpaqueRock, false, "ThirdPartyPrivatePhase1"),

            new Source(Pool.Understory, "Fern01_1", ForestRoot + "Foliage and Grass/Prefabs/prefab_fern_01_1.prefab", 5,
                Surface.FlexibleCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Understory, "Fern01_2", ForestRoot + "Foliage and Grass/Prefabs/prefab_fern_01_2.prefab", 5,
                Surface.FlexibleCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Understory, "Fern01_3", ForestRoot + "Foliage and Grass/Prefabs/prefab_fern_01_3.prefab", 5,
                Surface.FlexibleCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Understory, "Fern01_4", ForestRoot + "Foliage and Grass/Prefabs/prefab_fern_01_4.prefab", 5,
                Surface.FlexibleCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Understory, "Moss01_1", ForestRoot + "Details/Prefabs/prefab_detail_moss_01_1.prefab", 3,
                Surface.StaticCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Understory, "Moss01_2", ForestRoot + "Details/Prefabs/prefab_detail_moss_01_2.prefab", 3,
                Surface.StaticCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Understory, "Armillaria01", ForestRoot + "Mushrooms/Prefabs/prefab_Mushroom_Armillaria_01.prefab", 1,
                Surface.StaticCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Understory, "Armillaria02", ForestRoot + "Mushrooms/Prefabs/prefab_Mushroom_Armillaria_02.prefab", 1,
                Surface.StaticCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Understory, "Russula02", ForestRoot + "Mushrooms/Prefabs/prefab_Mushroom_Russula_02.prefab", 1,
                Surface.StaticCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Understory, "DeadGrass01", MeadowRoot + "Details/Prefabs/prefab_detail_meadow_dead_grass_01.prefab", 3,
                Surface.FlexibleCutout, false, "LicensedThirdPartyPhase1Presentation"),
            // Preserve the two existing weighted output slots and their prefab
            // GUIDs so the already generated cell scenes pick up the new litter
            // art without adding instance records. Source identity and report
            // provenance remain explicit; these are not disguised as grass.
            new Source(Pool.Understory, "BranchLitter01", ForestRoot + "Details/Prefabs/prefab_detail_branches_01.prefab", 3,
                Surface.StaticCutout, false, "LicensedThirdPartyPhase1Presentation", "DeadGrass02"),
            new Source(Pool.Understory, "PoplarLeafLitter01", MeadowRoot + "Details/Prefabs/prefab_detail_poplar_leaves_01_1.prefab", 3,
                Surface.StaticCutout, false, "LicensedThirdPartyPhase1Presentation", "DeadGrass03"),

            // Reviewed Finnish wet-ground shrubs. ALP Bush01/02 stay excluded
            // until their species and authored size are positively identified.
            new Source(Pool.Shrubs, "GreyWillow01", MeadowRoot + "Bushes/Prefabs/prefab_grey_willow_01.prefab", 4,
                Surface.FlexibleCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Shrubs, "GreyWillow02", MeadowRoot + "Bushes/Prefabs/prefab_grey_willow_02.prefab", 4,
                Surface.FlexibleCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Shrubs, "GreyWillow03", MeadowRoot + "Bushes/Prefabs/prefab_grey_willow_03.prefab", 4,
                Surface.FlexibleCutout, false, "LicensedThirdPartyPhase1Presentation"),
            new Source(Pool.Shrubs, "GreyWillow04", MeadowRoot + "Bushes/Prefabs/prefab_grey_willow_04.prefab", 4,
                Surface.FlexibleCutout, false, "LicensedThirdPartyPhase1Presentation")
        };
        private static readonly Dictionary<GameObject, Bounds> PrefabBounds =
            new Dictionary<GameObject, Bounds>();

        public static PlacementPolicy Policy(Pool pool)
        {
            switch (pool)
            {
                case Pool.RocksAndBoulders:
                    return new PlacementPolicy(11f, 0.14f, 48f, 0.35f,
                        new Vector2(0.65f, 1.30f), -0.035f);
                case Pool.Shrubs:
                    return new PlacementPolicy(6f, 0.55f, 38f, 0.35f,
                        new Vector2(0.75f, 1.15f), 0.01f);
                default:
                    // Sparse authored accents only. The dense carpet belongs to
                    // the indirect grass renderer and must not become thousands
                    // of streamed GameObjects per 512 m cell.
                    return new PlacementPolicy(5.5f, 0.28f, 50f, 0.82f,
                        new Vector2(0.82f, 1.22f), 0.008f);
            }
        }

        public static PlacementPolicy Policy(Pool pool, string stableId)
        {
            PlacementPolicy general = Policy(pool);
            if (pool != Pool.Understory) return general;
            switch (SemanticForStableId(stableId))
            {
                case MapVegetationForestFloorSemantic.GroundCover:
                    return new PlacementPolicy(1.8f, 1f, 50f, 0.82f,
                        new Vector2(0.72f, 1.05f),
                        general.SurfaceOffsetMeters);
                case MapVegetationForestFloorSemantic.ConiferLitter:
                case MapVegetationForestFloorSemantic.DeciduousLitter:
                    return new PlacementPolicy(2.1f, 1f, 50f, 0.92f,
                        new Vector2(0.78f, 1.18f),
                        general.SurfaceOffsetMeters);
                case MapVegetationForestFloorSemantic.WoodyDebris:
                    return new PlacementPolicy(9f, 1f, 46f, 0.96f,
                        new Vector2(1.20f, 1.75f),
                        general.SurfaceOffsetMeters);
                default:
                    return general;
            }
        }

        /// <summary>
        /// Hash consumed by the map-plan settings fingerprint. It changes when
        /// pool policy, approved source identity or a built wrapper dependency
        /// changes, so stale pilot/cell plans cannot pass validation by accident.
        /// Missing not-yet-built outputs are represented explicitly and are
        /// therefore also different from a completed binding build.
        /// </summary>
        public static string PlacementFingerprint
        {
            get
            {
                var writer = new StringBuilder(8192);
                writer.Append(BindingVersion).Append('\n');
                writer.Append(MapVegetationForestFloorEcology.PolicyVersion)
                    .Append('\n');
                foreach (Pool pool in new[] { Pool.Understory, Pool.Shrubs, Pool.RocksAndBoulders })
                {
                    PlacementPolicy policy = Policy(pool);
                    writer.Append(pool).Append('|')
                        .Append(Float(policy.MinimumSpacingMeters)).Append('|')
                        .Append(Float(policy.Density)).Append('|')
                        .Append(Float(policy.MaximumSlopeDegrees)).Append('|')
                        .Append(Float(policy.NormalAlignment)).Append('|')
                        .Append(Float(policy.UniformScaleRange.x)).Append('|')
                        .Append(Float(policy.UniformScaleRange.y)).Append('|')
                        .Append(Float(policy.SurfaceOffsetMeters)).Append('|')
                        .Append(policy.UsesSourceHeight ? '1' : '0').Append('\n');
                }
                foreach (MapVegetationForestFloorSemantic semantic in
                         new[]
                         {
                             MapVegetationForestFloorSemantic.GroundCover,
                             MapVegetationForestFloorSemantic.ConiferLitter,
                             MapVegetationForestFloorSemantic.DeciduousLitter,
                             MapVegetationForestFloorSemantic.WoodyDebris
                         })
                {
                    PlacementPolicy policy = Policy(Pool.Understory,
                        "forest-floor:ecology:test:" +
                        SemanticMarker(semantic) + ":0");
                    writer.Append("ecology|").Append(semantic).Append('|')
                        .Append(Float(policy.MinimumSpacingMeters)).Append('|')
                        .Append(Float(policy.NormalAlignment)).Append('|')
                        .Append(Float(policy.UniformScaleRange.x)).Append('|')
                        .Append(Float(policy.UniformScaleRange.y)).Append('|')
                        .Append(string.Join(",", ApprovedSourcePaths(
                            Pool.Understory, semantic)
                            .OrderBy(path => path, StringComparer.Ordinal)))
                        .Append('\n');
                }
                foreach (Source source in Sources.OrderBy(item => item.Pool).ThenBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.Append(source.Pool).Append('|').Append(source.Name).Append('|')
                        .Append(source.LegacyOutputSlot).Append('|')
                        .Append(source.Weight).Append('|').Append(source.Surface).Append('|')
                        .Append(source.SimpleCollider ? '1' : '0').Append('|')
                        .Append(source.Classification).Append('|')
                        .Append(source.Path).Append('|').Append(DependencyIdentity(source.Path)).Append('|')
                        .Append(OutputPath(source)).Append('|').Append(DependencyIdentity(OutputPath(source)))
                        .Append('\n');
                }
                using SHA256 sha = SHA256.Create();
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(writer.ToString()));
                return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static IReadOnlyList<string> ApprovedSourcePaths(Pool pool) => Sources
            .Where(source => source.Pool == pool)
            .Select(source => source.Path)
            .ToArray();

        public static IReadOnlyList<string> ApprovedSourcePaths(Pool pool,
            MapVegetationForestFloorSemantic semantic) => Sources
            .Where(source => source.Pool == pool &&
                             EligibleForSemantic(source, semantic))
            .Select(source => source.Path)
            .ToArray();

        public static MapVegetationForestFloorSemantic SemanticForStableId(
            string stableId)
        {
            stableId = stableId ?? string.Empty;
            if (stableId.IndexOf(":ground-cover:",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return MapVegetationForestFloorSemantic.GroundCover;
            if (stableId.IndexOf(":conifer-litter:",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return MapVegetationForestFloorSemantic.ConiferLitter;
            if (stableId.IndexOf(":deciduous-litter:",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return MapVegetationForestFloorSemantic.DeciduousLitter;
            if (stableId.IndexOf(":woody-debris:",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return MapVegetationForestFloorSemantic.WoodyDebris;
            return MapVegetationForestFloorSemantic.General;
        }

        public static string SelectSourcePath(Pool pool, string stableId, int seed)
        {
            return SelectSource(pool, stableId, seed).Path;
        }

        /// <summary>
        /// Returns the stable generated wrapper slot selected for a placement.
        /// Presentation substitutions may deliberately keep an older slot name
        /// so existing scene prefab references retain their GUID.
        /// </summary>
        public static string SelectOutputPath(Pool pool, string stableId, int seed)
        {
            return OutputPath(SelectSource(pool, stableId, seed));
        }

        public static GameObject SelectPrefab(Pool pool, string stableId, int seed)
        {
            Source source = SelectSource(pool, stableId, seed);
            string path = OutputPath(source);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new FileNotFoundException("Forest-floor wrappers must be built before placement: " + path, path);
            return prefab;
        }

        /// <summary>
        /// Shared instantiation hook for the cell writer. It intentionally does
        /// not apply the shrub height normalisation or disable wrapper colliders.
        /// </summary>
        public static GameObject Instantiate(Pool pool, string stableId, int seed, Transform parent,
            Vector3 position, Vector3 surfaceNormal)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            GameObject prefab = SelectPrefab(pool, stableId, seed);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            instance.transform.SetParent(parent, false);
            PlacementPolicy policy = Policy(pool, stableId);
            uint hash = MapVegetationPlanning.HashId(stableId, seed ^ SelectionSalt(pool, false));
            float variation = VegetationStableHash.ToUnitFloat(VegetationStableHash.Hash(hash ^ 0xA511E9B3u));
            float scale = Mathf.Lerp(policy.UniformScaleRange.x, policy.UniformScaleRange.y, variation);
            float yaw = VegetationStableHash.ToUnitFloat(hash) * 360f;
            Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
            Quaternion tilt = Quaternion.Slerp(Quaternion.identity,
                Quaternion.FromToRotation(Vector3.up, normal), policy.NormalAlignment);
            instance.name = stableId;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.rotation = tilt * Quaternion.Euler(0f, yaw, 0f);
            // The planner owns the surface offset and serializes its final
            // resolved position. Applying it again here would make validation
            // disagree with the saved placement record.
            instance.transform.position = position;
            return instance;
        }

        /// <summary>
        /// Replaces one donor-evidenced rock component at its measured bottom
        /// centre, principal yaw and authored component size. Variant selection
        /// is deterministic, but no position is procedurally scattered.
        /// </summary>
        public static GameObject InstantiateCanonicalRock(
            string stableId,
            int seed,
            Transform parent,
            Vector3 bottomCenter,
            Vector3 sourceSize,
            float sourceYaw)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (!float.IsFinite(sourceSize.x) || !float.IsFinite(sourceSize.y) ||
                !float.IsFinite(sourceSize.z) || sourceSize.x <= 0f ||
                sourceSize.y <= 0f || sourceSize.z <= 0f)
                throw new ArgumentOutOfRangeException(nameof(sourceSize));

            GameObject prefab = SelectPrefab(Pool.RocksAndBoulders,
                stableId, seed);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                prefab, parent.gameObject.scene);
            instance.transform.SetParent(parent, false);
            Bounds prefabBounds = GetPrefabBounds(prefab);
            float sourceFootprint = Mathf.Max(sourceSize.x, sourceSize.z);
            float prefabFootprint = Mathf.Max(prefabBounds.size.x,
                prefabBounds.size.z);
            float horizontalScale = sourceFootprint /
                Mathf.Max(prefabFootprint, 0.01f);
            float verticalScale = sourceSize.y /
                Mathf.Max(prefabBounds.size.y, 0.01f);
            float scale = Mathf.Clamp(Mathf.Sqrt(
                Mathf.Max(0.01f, horizontalScale * verticalScale)),
                0.18f, 8f);

            instance.name = stableId;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.rotation = Quaternion.Euler(
                0f, sourceYaw, 0f);
            instance.transform.position = Vector3.zero;
            Bounds rendered = RendererBounds(instance);
            instance.transform.position = bottomCenter - new Vector3(
                rendered.center.x,
                rendered.min.y,
                rendered.center.z);
            instance.AddComponent<CanonicalRockReplacementAnchor>()
                .ConfigureForAuthoring(stableId, bottomCenter, sourceSize,
                    Mathf.Repeat(sourceYaw, 360f), scale);

            // The canonical donor MAP/MESH/ROCKS MeshCollider stays the exact
            // physics authority. ALP is presentation-only here; retaining its
            // approximate wrapper collider would create a second, mismatched
            // contact surface and the familiar invisible snag nonsense.
            foreach (Collider collider in instance.GetComponentsInChildren<
                         Collider>(true))
                Object.DestroyImmediate(collider);
            return instance;
        }

        private static Bounds GetPrefabBounds(GameObject prefab)
        {
            if (PrefabBounds.TryGetValue(prefab, out Bounds cached))
                return cached;
            Bounds bounds = RendererBounds(prefab);
            PrefabBounds[prefab] = bounds;
            return bounds;
        }

        private static Bounds RendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidDataException("Forest-floor prefab has no renderer: " + root.name);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        public static GameObject[] BuildAll()
        {
            RequireAudit(AlpAuditPath,
                "The ALP package must be selectively imported and audited before building rocks.");
            RequireAudit(NatureManufactureAuditPath,
                "The reviewed NatureManufacture subset must be imported and audited before building understory.");
            Shader lit = Shader.Find("HDRP/Lit");
            Shader wind = Shader.Find("MSC/HDRP/Spruce Wind");
            if (lit == null || wind == null)
                throw new InvalidOperationException("Unity 6 HDRP/Lit and the project-owned vegetation wind shader are required.");

            EnsureFolder(Root);
            string runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" +
                Guid.NewGuid().ToString("N").Substring(0, 8);
            var report = new BuildReport
            {
                runId = runId,
                alpImportAudit = AlpAuditPath,
                natureManufactureImportAudit = NatureManufactureAuditPath
            };
            var materialReports = new Dictionary<string, MaterialReport>(StringComparer.Ordinal);
            var outputs = new List<GameObject>();
            foreach (Source source in Sources)
                outputs.Add(BuildWrapper(source, lit, wind, runId, report, materialReports));
            report.materials.AddRange(materialReports.Values.OrderBy(item => item.outputPath, StringComparer.Ordinal));
            AssetDatabase.SaveAssets();
            report.placementFingerprint = PlacementFingerprint;
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            Debug.Log($"MAP_VEGETATION_FOREST_FLOOR_ART_OK rocks={Sources.Count(s => s.Pool == Pool.RocksAndBoulders)} " +
                $"shrubs={Sources.Count(s => s.Pool == Pool.Shrubs)} " +
                $"understory={Sources.Count(s => s.Pool == Pool.Understory)} " +
                $"organicLitter={Sources.Count(s => s.Name.Contains("Litter"))} report={ReportPath}");
            return outputs.ToArray();
        }

        [MenuItem("MSC/World/Vegetation/Build Reviewed Forest Floor Art")]
        public static void BuildAllForBatch()
        {
            BuildAll();
        }

        private static GameObject BuildWrapper(Source source, Shader lit, Shader wind, string runId,
            BuildReport report, Dictionary<string, MaterialReport> materialReports)
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(source.Path);
            if (sourcePrefab == null)
                throw new FileNotFoundException("Reviewed forest-floor source is missing: " + source.Path, source.Path);
            string outputPath = OutputPath(source);
            EnsureFolder(Path.GetDirectoryName(outputPath).Replace('\\', '/'));
            Backup(outputPath, runId);
            var root = new GameObject("MSC_ForestFloor_" + source.Name);
            try
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, root.transform);
                visual.name = "Visuals";
                visual.transform.localPosition = sourcePrefab.transform.localPosition;
                visual.transform.localRotation = sourcePrefab.transform.localRotation;
                visual.transform.localScale = sourcePrefab.transform.localScale;
                StripRuntimeAndCollision(visual);
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++)
                        materials[index] = BuildMaterial(source, materials[index], lit, wind, runId, materialReports);
                    renderer.sharedMaterials = materials;
                }
                ConfigureLods(visual, source.Surface != Surface.OpaqueRock);
                if (source.SimpleCollider)
                    AddSimpleCollider(root);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, outputPath);
                if (saved == null) throw new IOException("Could not save forest-floor wrapper: " + outputPath);
                var evidence = new PrefabReport
                {
                    pool = source.Pool.ToString(),
                    name = source.Name,
                    classification = source.Classification,
                    productionReady = false,
                    sourcePath = source.Path,
                    legacyOutputSlot = source.LegacyOutputSlot,
                    sourceGuid = AssetDatabase.AssetPathToGUID(source.Path),
                    sourceSha256 = HashFile(source.Path),
                    dependencyHash = AssetDatabase.GetAssetDependencyHash(source.Path).ToString(),
                    outputPath = outputPath,
                    outputGuid = AssetDatabase.AssetPathToGUID(outputPath),
                    outputSha256 = HashFile(outputPath),
                    selectionWeight = source.Weight,
                    primitiveCollider = source.SimpleCollider,
                    sourceHeightPreserved = true
                };
                ValidateBuiltWrapper(saved, source, evidence);
                report.prefabs.Add(evidence);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material BuildMaterial(Source source, Material input, Shader lit, Shader wind, string runId,
            Dictionary<string, MaterialReport> reports)
        {
            if (input == null) throw new InvalidDataException("Forest-floor renderer has a missing material: " + source.Path);
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(input, out string guid, out long localId) || string.IsNullOrEmpty(guid))
                throw new InvalidDataException("Forest-floor source material has no stable asset identity: " + input.name);
            string key = guid + ":" + localId + ":" + source.Surface;
            string safeName = Sanitize(input.name);
            string folder = Root + "/Materials";
            EnsureFolder(folder);
            string localToken = localId.ToString(System.Globalization.CultureInfo.InvariantCulture).Replace('-', 'n');
            string outputPath = folder + "/" + safeName + "_" + guid.Substring(0, 12) + "_" +
                localToken + "_" + source.Surface + ".mat";
            if (reports.ContainsKey(key))
            {
                Material cached = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
                if (cached == null)
                    throw new InvalidDataException("Generated forest-floor material cache is missing: " + outputPath);
                return cached;
            }
            Material output = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
            Shader shader = source.Surface == Surface.FlexibleCutout ? wind : lit;
            if (output == null)
            {
                Backup(outputPath, runId);
                output = new Material(shader);
                AssetDatabase.CreateAsset(output, outputPath);
            }
            else
            {
                Backup(outputPath, runId);
            }
            output.shader = shader;
            output.name = Path.GetFileNameWithoutExtension(outputPath);
            output.shaderKeywords = Array.Empty<string>();
            output.enableInstancing = true;

            Texture baseMap = FindTexture(input, "_Base_Color", "_BaseColorMap", "_BaseMap", "_MainTex");
            if (baseMap == null)
                throw new InvalidDataException("Forest-floor material has no supported base map: " + AssetDatabase.GetAssetPath(input));
            Texture normal = FindTexture(input, "_Normal", "_NormalMap", "_BumpMap");
            string sourceBaseProperty = FindTextureProperty(input, baseMap,
                "_Base_Color", "_BaseColorMap", "_BaseMap", "_MainTex");
            output.SetTexture("_BaseColorMap", baseMap);
            output.SetTextureScale("_BaseColorMap", input.GetTextureScale(sourceBaseProperty));
            output.SetTextureOffset("_BaseColorMap", input.GetTextureOffset(sourceBaseProperty));
            output.SetTexture("_NormalMap", normal);
            Color tint = input.HasProperty("_BaseColor") ? input.GetColor("_BaseColor")
                : input.HasProperty("_Color") ? input.GetColor("_Color") : Color.white;
            tint.a = 1f;
            output.SetColor("_BaseColor", tint);
            output.SetFloat("_NormalScale", ReadFloat(input, 1f, "_NormalScale", "_Normal_Power", "_BumpScale"));
            output.SetFloat("_Metallic", source.Surface == Surface.OpaqueRock ? 0f : 0f);
            output.SetFloat("_Smoothness", source.Surface == Surface.OpaqueRock
                ? Mathf.Clamp(ReadFloat(input, 0.28f, "_Smoothness", "_Glossiness"), 0.08f, 0.48f)
                : 0.18f);
            output.SetColor("_EmissiveColor", Color.black);
            if (output.HasProperty("_EmissionColor")) output.SetColor("_EmissionColor", Color.black);

            bool cutout = source.Surface != Surface.OpaqueRock;
            output.SetFloat("_SurfaceType", 0f);
            output.SetFloat("_AlphaCutoffEnable", cutout ? 1f : 0f);
            float cutoff = Mathf.Clamp(ReadFloat(input, cutout ? 0.3f : 0f,
                "_AlphaCutoff", "_Cutoff"), 0.05f, 0.9f);
            output.SetFloat("_AlphaCutoff", cutoff);
            if (output.HasProperty("_AlphaCutoffShadow")) output.SetFloat("_AlphaCutoffShadow", cutoff);
            output.SetFloat("_DoubleSidedEnable", cutout ? 1f : 0f);
            output.SetFloat("_CullMode", cutout ? 0f : 2f);
            output.SetFloat("_CullModeForward", cutout ? 0f : 2f);
            if (output.HasProperty("_DoubleSidedConstants"))
                output.SetVector("_DoubleSidedConstants", cutout
                    ? new Vector4(-1f, -1f, -1f, 0f)
                    : new Vector4(1f, 1f, 1f, 0f));
            if (cutout && output.HasProperty("_ZTestDepthEqualForOpaque"))
                output.SetFloat("_ZTestDepthEqualForOpaque", (float)CompareFunction.LessEqual);
            output.doubleSidedGI = cutout;
            output.renderQueue = cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            if (source.Surface == Surface.StaticCutout && input.HasProperty("_MaskMap"))
                output.SetTexture("_MaskMap", input.GetTexture("_MaskMap"));
            else if (output.HasProperty("_MaskMap"))
                output.SetTexture("_MaskMap", null);
            if (source.Surface == Surface.FlexibleCutout)
            {
                bool willow = source.Name.StartsWith("GreyWillow", StringComparison.Ordinal);
                bool fern = source.Name.StartsWith("Fern", StringComparison.Ordinal);
                output.SetFloat("_WindStrengthMeters", willow ? 0.18f : fern ? 0.07f : 0.045f);
                output.SetFloat("_WindSpeed", 1.2f);
                output.SetFloat("_WindFrequency", willow ? 0.035f : 0.055f);
                output.SetFloat("_WindBaseHeightMeters", willow ? 0.15f : 0.02f);
                output.SetFloat("_WindHeightMeters", willow ? 3f : fern ? 0.65f : 0.38f);
                output.SetFloat("_MinimumWindIntensity", 0.025f);
                // This project-owned shader is HDRP-compatible but is not
                // HDRP/Lit itself, so HDMaterial validation must not be applied.
                output.EnableKeyword("_ALPHATEST_ON");
                output.EnableKeyword("_DOUBLESIDED_ON");
            }
            else if (!HDMaterial.ValidateMaterial(output))
                throw new InvalidDataException("HDRP rejected generated forest-floor material: " + outputPath);
            EditorUtility.SetDirty(output);
            AssetDatabase.SaveAssetIfDirty(output);
            if (!reports.ContainsKey(key))
            {
                reports[key] = new MaterialReport
                {
                    classification = "ReauthoredMaterial",
                    sourcePath = AssetDatabase.GetAssetPath(input),
                    sourceGuid = guid,
                    sourceLocalId = localId,
                    sourceShader = input.shader != null ? input.shader.name : "missing",
                    baseMapPath = AssetDatabase.GetAssetPath(baseMap),
                    normalMapPath = AssetDatabase.GetAssetPath(normal),
                    outputPath = outputPath,
                    outputGuid = AssetDatabase.AssetPathToGUID(outputPath),
                    outputShader = shader.name,
                    alphaCutout = cutout,
                    note = source.Surface == Surface.OpaqueRock
                        ? "Standard rock albedo/normal transferred to HDRP/Lit; vendor metallic/gloss packing is not treated as an HDRP mask."
                        : "Reviewed albedo/normal transferred to a project-owned Unity 6 HDRP binding; no vendor shader is required at runtime."
                };
            }
            return output;
        }

        private static void StripRuntimeAndCollision(GameObject visual)
        {
            foreach (MonoBehaviour behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null) Object.DestroyImmediate(behaviour);
            foreach (Transform transform in visual.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        private static void ConfigureLods(GameObject visual, bool flexible)
        {
            foreach (LODGroup group in visual.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                if (lods.Length == 0) continue;
                float[] thresholds = Thresholds(lods.Length, flexible);
                for (int index = 0; index < lods.Length; index++)
                {
                    lods[index].screenRelativeTransitionHeight = thresholds[index];
                    lods[index].fadeTransitionWidth = index == lods.Length - 1 ? 0.12f : 0.18f;
                    foreach (Renderer renderer in lods[index].renderers ?? Array.Empty<Renderer>())
                    {
                        if (renderer == null) continue;
                        renderer.shadowCastingMode = index == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        renderer.receiveShadows = true;
                        renderer.motionVectorGenerationMode = flexible
                            ? MotionVectorGenerationMode.Object
                            : MotionVectorGenerationMode.ForceNoMotion;
                    }
                }
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = true;
                group.SetLODs(lods);
                group.RecalculateBounds();
            }
        }

        private static float[] Thresholds(int count, bool flexible)
        {
            if (count == 1) return new[] { flexible ? 0.012f : 0.006f };
            if (count == 2) return new[] { flexible ? 0.16f : 0.12f, flexible ? 0.012f : 0.006f };
            if (count == 3) return new[] { 0.24f, 0.075f, 0.008f };
            if (count == 4) return new[] { 0.28f, 0.115f, 0.04f, 0.006f };
            var values = new float[count];
            for (int index = 0; index < count; index++)
                values[index] = Mathf.Lerp(0.28f, 0.004f, index / (float)(count - 1));
            return values;
        }

        private static void AddSimpleCollider(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidDataException("Boulder wrapper has no renderer bounds: " + root.name);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            Vector3 size = root.transform.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Abs(size.x) * 0.82f,
                Mathf.Abs(size.y) * 0.88f, Mathf.Abs(size.z) * 0.82f);
        }

        private static void ValidateBuiltWrapper(GameObject prefab, Source source, PrefabReport evidence)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if ((prefab.transform.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                throw new InvalidDataException("Forest-floor wrapper root must retain unit scale: " + prefab.name);
            int missingScripts = 0;
            foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            if (missingScripts != 0 || behaviours.Any(behaviour => behaviour != null))
                throw new InvalidDataException("Forest-floor wrapper retained vendor runtime scripts: " + prefab.name);

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidDataException("Forest-floor wrapper has no renderers: " + prefab.name);
            string expectedShader = source.Surface == Surface.FlexibleCutout
                ? "MSC/HDRP/Spruce Wind"
                : "HDRP/Lit";
            foreach (Renderer renderer in renderers)
            foreach (Material material in renderer.sharedMaterials)
                if (material == null || material.shader == null || material.shader.name != expectedShader)
                    throw new InvalidDataException("Forest-floor wrapper retained a non-project material binding: " +
                        prefab.name + "/" + renderer.name);

            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            int expectedColliders = source.SimpleCollider ? 1 : 0;
            if (colliders.Length != expectedColliders ||
                source.SimpleCollider && !(colliders[0] is BoxCollider))
                throw new InvalidDataException("Forest-floor wrapper collider policy drifted: " + prefab.name);

            LODGroup[] groups = prefab.GetComponentsInChildren<LODGroup>(true);
            foreach (LODGroup group in groups)
            {
                float previous = 1f;
                foreach (LOD lod in group.GetLODs())
                {
                    if (!float.IsFinite(lod.screenRelativeTransitionHeight) ||
                        lod.screenRelativeTransitionHeight <= 0f ||
                        lod.screenRelativeTransitionHeight >= previous)
                        throw new InvalidDataException("Forest-floor LOD thresholds are not strictly descending: " + prefab.name);
                    previous = lod.screenRelativeTransitionHeight;
                }
            }
            evidence.rendererCount = renderers.Length;
            evidence.lodGroupCount = groups.Length;
            evidence.colliderCount = colliders.Length;
        }

        private static Source SelectSource(Pool pool, string stableId, int seed)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Stable ID is required.", nameof(stableId));
            MapVegetationForestFloorSemantic semantic =
                SemanticForStableId(stableId);
            Source[] candidates = Sources.Where(source => source.Pool == pool &&
                EligibleForSemantic(source, semantic)).ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException("No reviewed forest-floor source is approved for " + pool + "/" + semantic + ".");
            int totalWeight = candidates.Sum(source => source.Weight);
            uint hash = MapVegetationPlanning.HashId(stableId,
                seed ^ SelectionSalt(pool, true));
            int slot = (int)(hash % (uint)totalWeight);
            foreach (Source source in candidates)
            {
                if (slot < source.Weight) return source;
                slot -= source.Weight;
            }
            throw new InvalidOperationException("Forest-floor weighted selection failed.");
        }

        private static bool EligibleForSemantic(Source source,
            MapVegetationForestFloorSemantic semantic)
        {
            bool branches = string.Equals(source.Name, "BranchLitter01",
                StringComparison.Ordinal);
            bool leaves = string.Equals(source.Name, "PoplarLeafLitter01",
                StringComparison.Ordinal);
            if (source.Pool != Pool.Understory)
                return true;
            // Generic boundary undergrowth must remain living/low cover. Organic
            // litter is emitted only by the canopy ecology planner, otherwise
            // branches and leaf cards reappear as unrelated random spots.
            if (semantic == MapVegetationForestFloorSemantic.General)
                return !branches && !leaves;
            switch (semantic)
            {
                case MapVegetationForestFloorSemantic.GroundCover:
                    return !branches && !leaves;
                case MapVegetationForestFloorSemantic.ConiferLitter:
                case MapVegetationForestFloorSemantic.WoodyDebris:
                    return branches;
                case MapVegetationForestFloorSemantic.DeciduousLitter:
                    return leaves;
                default:
                    return false;
            }
        }

        private static string SemanticMarker(
            MapVegetationForestFloorSemantic semantic)
        {
            switch (semantic)
            {
                case MapVegetationForestFloorSemantic.GroundCover:
                    return "ground-cover";
                case MapVegetationForestFloorSemantic.ConiferLitter:
                    return "conifer-litter";
                case MapVegetationForestFloorSemantic.DeciduousLitter:
                    return "deciduous-litter";
                case MapVegetationForestFloorSemantic.WoodyDebris:
                    return "woody-debris";
                default:
                    return "general";
            }
        }

        private static int SelectionSalt(Pool pool, bool sourceSelection)
        {
            if (sourceSelection)
                return pool == Pool.RocksAndBoulders ? 0x41A7 : pool == Pool.Shrubs ? 0x5D13 : 0x7C31;
            return pool == Pool.RocksAndBoulders ? 0x2F19 : pool == Pool.Shrubs ? 0x53D7 : 0x6A31;
        }

        private static Texture FindTexture(Material material, params string[] properties)
        {
            foreach (string property in properties)
                if (material.HasProperty(property) && material.GetTexture(property) != null)
                    return material.GetTexture(property);
            return null;
        }

        private static string FindTextureProperty(Material material, Texture texture, params string[] properties)
        {
            foreach (string property in properties)
                if (material.HasProperty(property) && material.GetTexture(property) == texture) return property;
            throw new InvalidDataException("Could not identify source texture property on " + material.name);
        }

        private static float ReadFloat(Material material, float fallback, params string[] properties)
        {
            foreach (string property in properties)
                if (material.HasProperty(property)) return material.GetFloat(property);
            return fallback;
        }

        private static string OutputPath(Source source) =>
            Root + "/" + source.Pool + "/" + source.LegacyOutputSlot + ".prefab";

        private static string DependencyIdentity(string path)
        {
            if (!File.Exists(path)) return "missing";
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return "unimported:" + HashFile(path);
            return guid + ":" + AssetDatabase.GetAssetDependencyHash(path);
        }

        private static string Float(float value) =>
            value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        private static string Sanitize(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        }

        private static void RequireAudit(string path, string message)
        {
            if (!File.Exists(path) || new FileInfo(path).Length < 32)
                throw new FileNotFoundException(message + " Missing: " + path, path);
        }

        private static void Backup(string path, string runId)
        {
            if (!path.StartsWith(Root + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Forest-floor output escaped its owned directory.");
            if (!File.Exists(path)) return;
            string destination = "Artifacts/VegetationRebuild/Backups/ForestFloor/" + runId + "/" +
                path.Substring(Root.Length + 1);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(path, destination, false);
            if (File.Exists(path + ".meta")) File.Copy(path + ".meta", destination + ".meta", false);
        }

        private static string HashFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return string.Empty;
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Invalid forest-floor folder: " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private enum Surface { OpaqueRock, FlexibleCutout, StaticCutout }

        private readonly struct Source
        {
            public readonly Pool Pool;
            public readonly string Name, Path, Classification, LegacyOutputSlot;
            public readonly int Weight;
            public readonly Surface Surface;
            public readonly bool SimpleCollider;

            public Source(Pool pool, string name, string path, int weight, Surface surface,
                bool simpleCollider, string classification, string legacyOutputSlot = null)
            {
                Pool = pool;
                Name = name;
                Path = path;
                Weight = weight;
                Surface = surface;
                SimpleCollider = simpleCollider;
                Classification = classification;
                LegacyOutputSlot = string.IsNullOrWhiteSpace(legacyOutputSlot) ? name : legacyOutputSlot;
            }
        }

        [Serializable]
        private sealed class BuildReport
        {
            public string generator = BindingVersion;
            public string runId;
            public string placementFingerprint;
            public bool productionReady;
            public string provenance = "ALP sources remain ThirdPartyPrivatePhase1 with license receipt pending; NatureManufacture sources are the selectively imported reviewed Finnish-biome subset. The branch cluster retains its exact vendor beech-environment source path but presents bare dead wood only; leaf litter uses the Meadow Populus ground-detail family.";
            public string integration = "Separate weighted rock, shrub and understory/accent pools; source scale is preserved; no shrub height normalisation. Branch and leaf litter replace two existing weighted output slots through explicit LegacyOutputSlot aliases, preserving prefab GUIDs and instance-record count. The planner owns surface offset and passes final saved positions to Instantiate; cell ownership and streaming remain with the existing vegetation writer.";
            public string exclusions = "No beech leaf/log/root/stump content, ALP tree-internal branch geometry, particle leaves or vendor runtime scripts. The supplied packages contain no reviewed standalone Finnish conifer twig or cone scatter prefab.";
            public string alpImportAudit, natureManufactureImportAudit;
            public List<PrefabReport> prefabs = new List<PrefabReport>();
            public List<MaterialReport> materials = new List<MaterialReport>();
        }

        [Serializable]
        private sealed class PrefabReport
        {
            public string pool, name, classification, sourcePath, legacyOutputSlot, sourceGuid, sourceSha256,
                dependencyHash, outputPath, outputGuid, outputSha256;
            public bool productionReady, primitiveCollider, sourceHeightPreserved;
            public int selectionWeight, rendererCount, lodGroupCount, colliderCount;
        }

        [Serializable]
        private sealed class MaterialReport
        {
            public string classification, sourcePath, sourceGuid, sourceShader, baseMapPath,
                normalMapPath, outputPath, outputGuid, outputShader, note;
            public long sourceLocalId;
            public bool alphaCutout;
        }
    }
}
