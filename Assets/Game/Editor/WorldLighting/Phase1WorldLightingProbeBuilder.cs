using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Bootstrap;
using MSC.LegacyImport;
using MSC.World.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldLighting
{
    public static class Phase1WorldLightingProbeBuilder
    {
        private const string CellSceneFolder =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells";
        private const string CatalogFolder =
            "Assets/Game/World/Content/Lighting";
        private const string CatalogPath =
            CatalogFolder + "/Phase1WorldLightingProbeCatalog.asset";
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string YardCellScenePath =
            CellSceneFolder + "/World_Cell_0_-3_Legacy.unity";
        private const string PlayerBedPath =
            "YARD/Building/BEDROOM1/boybed";
        private const string PlayerBedroomDoorPath =
            "YARD/Building/BEDROOM1/DoorBedroom1/Pivot/house_door1";

        [MenuItem("MSC/World/Build Phase 1 Lighting And Probes")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "World lighting generation is unavailable in Play Mode.");
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Build();
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void Build()
        {
            EnsureFolder(CatalogFolder);
            List<FixtureCandidate> fixtures = CollectFixtures();
            if (fixtures.Count == 0)
            {
                throw new InvalidOperationException(
                    "No supported donor fixture references were found.");
            }

            List<WorldLightDefinition> lights = BuildLights(fixtures);
            AddTeimoRefrigeratorLights(lights);
            List<WorldReflectionProbeDefinition> probes =
                BuildProbes(fixtures);
            PlayerSpawnDefinition playerSpawn = ResolvePlayerSpawn();

            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorldLightingProbeCatalog>(
                    CatalogPath);
            if (catalog == null)
            {
                catalog =
                    ScriptableObject.CreateInstance<WorldLightingProbeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.ConfigureForAuthoring(
                "world-lighting.phase1-fixtures.v1",
                lights,
                probes);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Scene bootstrap = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            ProductionWorldStreamingInstaller installer =
                bootstrap.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<
                            ProductionWorldStreamingInstaller>(true))
                    .Single();
            installer.ConfigureLightingForAuthoring(catalog);
            installer.ConfigurePlayerSpawnForAuthoring(
                playerSpawn.Position,
                playerSpawn.Rotation);
            EditorUtility.SetDirty(installer);
            EditorSceneManager.MarkSceneDirty(bootstrap);
            EditorSceneManager.SaveScene(bootstrap);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"Phase 1 world lighting generated: {lights.Count} lights, " +
                $"{probes.Count} reflection probes. Catalog: {CatalogPath}");
        }

        private static List<FixtureCandidate> CollectFixtures()
        {
            string[] sceneGuids =
                AssetDatabase.FindAssets("t:Scene", new[] { CellSceneFolder });
            var fixtures = new List<FixtureCandidate>(64);
            Array.Sort(sceneGuids, StringComparer.Ordinal);

            for (int sceneIndex = 0;
                 sceneIndex < sceneGuids.Length;
                 sceneIndex++)
            {
                string scenePath =
                    AssetDatabase.GUIDToAssetPath(sceneGuids[sceneIndex]);
                Scene scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Single);
                DonorWorldBaselineEntityMetadata[] metadata =
                    scene.GetRootGameObjects()
                        .SelectMany(root =>
                            root.GetComponentsInChildren<
                                DonorWorldBaselineEntityMetadata>(true))
                        .ToArray();
                for (int index = 0; index < metadata.Length; index++)
                {
                    DonorWorldBaselineEntityMetadata entity = metadata[index];
                    if (!TryClassify(
                            entity.SourceHierarchyPath,
                            out FixtureProfile profile))
                    {
                        continue;
                    }

                    Renderer renderer = entity.GetComponent<Renderer>();
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

                    Bounds bounds = renderer.bounds;
                    Vector3 position = profile.IsStreetLight
                        ? ResolveStreetLightPosition(entity, renderer, bounds)
                        : new Vector3(
                            bounds.center.x,
                            bounds.min.y - 0.08f,
                            bounds.center.z);
                    Vector3 rotationEulerAngles = profile.IsStreetLight
                        ? ResolveStreetLightRotation(entity, position)
                        : Vector3.zero;
                    fixtures.Add(
                        new FixtureCandidate(
                            entity.StableId,
                            entity.SourceCellId,
                            entity.SourceHierarchyPath,
                            position,
                            rotationEulerAngles,
                            profile));
                }
            }

            return Deduplicate(fixtures);
        }

        private static List<FixtureCandidate> Deduplicate(
            List<FixtureCandidate> source)
        {
            source.Sort((left, right) =>
                string.CompareOrdinal(left.StableId, right.StableId));
            var result = new List<FixtureCandidate>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                FixtureCandidate candidate = source[index];
                bool duplicate = result.Any(existing =>
                    string.Equals(
                        existing.CellId,
                        candidate.CellId,
                        StringComparison.Ordinal) &&
                    existing.Profile.IsStreetLight ==
                        candidate.Profile.IsStreetLight &&
                    Vector3.SqrMagnitude(
                        existing.Position - candidate.Position) < 0.36f);
                if (!duplicate)
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

        private static List<WorldLightDefinition> BuildLights(
            List<FixtureCandidate> fixtures)
        {
            var definitions =
                new List<WorldLightDefinition>(fixtures.Count);
            for (int index = 0; index < fixtures.Count; index++)
            {
                FixtureCandidate fixture = fixtures[index];
                bool isStoreInterior =
                    !fixture.Profile.IsStreetLight &&
                    string.Equals(
                        RootLocation(fixture.SourcePath),
                        "STORE",
                        StringComparison.OrdinalIgnoreCase);
                bool isInspectionInterior =
                    !fixture.Profile.IsStreetLight &&
                    string.Equals(
                        RootLocation(fixture.SourcePath),
                        "INSPECTION",
                        StringComparison.OrdinalIgnoreCase);
                StreetLightOverride streetOverride =
                    ResolveStreetLightOverride(fixture);
                InteriorLightOverride interiorOverride =
                    ResolveInteriorLightOverride(fixture);
                bool isStreetLight = fixture.Profile.IsStreetLight;
                bool isNightOnlyExterior =
                    !isStreetLight &&
                    string.Equals(
                        Path.GetFileName(fixture.SourcePath),
                        "outdoor_lamp",
                        StringComparison.OrdinalIgnoreCase);
                Vector3 position = interiorOverride.IsDefined
                    ? interiorOverride.Position
                    : streetOverride.IsDefined
                        ? streetOverride.Position
                        : fixture.Position;
                Vector3 rotation = interiorOverride.IsDefined
                    ? interiorOverride.RotationEulerAngles
                    : streetOverride.IsDefined
                        ? streetOverride.RotationEulerAngles
                        : fixture.RotationEulerAngles;
                float intensity = interiorOverride.IsDefined
                    ? interiorOverride.IntensityLumens
                    : isStreetLight
                        ? 400f
                        : isStoreInterior
                            ? 32_500f
                            : isInspectionInterior
                                ? 1_200f
                                : fixture.Profile.Intensity;
                float range = interiorOverride.IsDefined
                    ? interiorOverride.Range
                    : streetOverride.IsDefined
                        ? streetOverride.Range
                        : isStreetLight
                        ? 35f
                        : isStoreInterior
                            ? fixture.Profile.Range * 0.68f
                            : isInspectionInterior
                                ? 6.5f
                                : fixture.Profile.Range;
                float innerSpotAngle = interiorOverride.IsDefined
                    ? interiorOverride.InnerSpotAngle
                    : isStreetLight ? 107f : 72f;
                float outerSpotAngle = interiorOverride.IsDefined
                    ? interiorOverride.OuterSpotAngle
                    : isStreetLight ? 138f : 105f;
                float indirectMultiplier = interiorOverride.IsDefined
                    ? interiorOverride.IndirectMultiplier
                    : isStreetLight
                        ? 0f
                        : isInspectionInterior
                            ? 0.1f
                            : 1f;
                WorldLightSourceKind sourceKind =
                    isStreetLight || interiorOverride.IsDefined
                        ? WorldLightSourceKind.Spot
                        : WorldLightSourceKind.Point;
                WorldLightBakeMode bakeMode =
                    WorldLightBakeMode.Realtime;
                var definition = new WorldLightDefinition();
                definition.ConfigureForAuthoring(
                    "world.light." + fixture.StableId,
                    fixture.CellId,
                    position,
                    rotation,
                    isStreetLight
                        ? Color.white
                        : isStoreInterior
                            ? Color.white
                        : fixture.Profile.Color,
                    intensity,
                    range,
                    innerSpotAngle,
                    outerSpotAngle,
                    isStreetLight,
                    isStreetLight ? 15.6f : 1f,
                    isStreetLight
                        ? 6_570f
                        : isStoreInterior
                            ? 6_000f
                            : 0f,
                    indirectMultiplier,
                    0.025f,
                    isStreetLight,
                    isStreetLight ? 1f : 0f,
                    sourceKind,
                    bakeMode,
                    isStreetLight || isNightOnlyExterior
                        ? WorldLightActivationPolicy.NightOnly
                        : WorldLightActivationPolicy.AlwaysOn,
                    fixture.Profile.CastsShadows);
                definition.SetDonorReferencePathForAuthoring(
                    fixture.SourcePath);
                definitions.Add(definition);
            }

            return definitions;
        }

        private static List<WorldReflectionProbeDefinition> BuildProbes(
            List<FixtureCandidate> fixtures)
        {
            IEnumerable<IGrouping<string, FixtureCandidate>> groups =
                fixtures
                    .Where(fixture => !fixture.Profile.IsStreetLight)
                    .GroupBy(
                        fixture =>
                            fixture.CellId + "|" +
                            RootLocation(fixture.SourcePath),
                        StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal);
            var definitions =
                new List<WorldReflectionProbeDefinition>();

            foreach (IGrouping<string, FixtureCandidate> group in groups)
            {
                FixtureCandidate[] candidates = group.ToArray();
                if (candidates.Length == 0)
                {
                    continue;
                }

                Vector3 minimum = candidates[0].Position;
                Vector3 maximum = candidates[0].Position;
                for (int index = 1; index < candidates.Length; index++)
                {
                    minimum = Vector3.Min(minimum, candidates[index].Position);
                    maximum = Vector3.Max(maximum, candidates[index].Position);
                }

                Vector3 centre = (minimum + maximum) * 0.5f;
                centre.y = minimum.y - 1.35f;
                Vector3 size = maximum - minimum;
                size.x = Mathf.Clamp(size.x + 10f, 8f, 48f);
                size.y = Mathf.Clamp(size.y + 5f, 4f, 12f);
                size.z = Mathf.Clamp(size.z + 10f, 8f, 48f);

                string location = RootLocation(candidates[0].SourcePath)
                    .ToLowerInvariant();
                if (string.Equals(
                        location,
                        "yard",
                        StringComparison.Ordinal))
                {
                    AddHomeReflectionProbes(
                        definitions,
                        candidates[0].CellId);
                    continue;
                }

                bool isStore =
                    string.Equals(
                        location,
                        "store",
                        StringComparison.Ordinal);
                if (isStore)
                {
                    size.x *= 0.78f;
                    size.y = Mathf.Min(size.y, 4.5f);
                    size.z *= 0.70f;
                }

                var definition = new WorldReflectionProbeDefinition();
                definition.ConfigureForAuthoring(
                    "world.probe." +
                    SanitizeId(candidates[0].CellId) +
                    "." +
                    SanitizeId(location),
                    candidates[0].CellId,
                    centre,
                    size,
                    isStore ? 0.08f : 0.18f,
                    128);
                definitions.Add(definition);
            }

            return definitions;
        }

        private static void AddHomeReflectionProbes(
            List<WorldReflectionProbeDefinition> definitions,
            string cellId)
        {
            // The previous YARD-wide probe used the lowest fixture to derive
            // its capture height. The sauna light consequently put the probe
            // below the house floor, while its 23 x 7 x 25 m influence box
            // crossed the house, garage and exterior. Keep capture points and
            // influence volumes inside the actual enclosed spaces instead.
            AddHomeReflectionProbe(
                definitions,
                "world.probe.cell-0--3.home.living",
                cellId,
                new Vector3(162.8f, 2.35f, -1031.4f),
                new Vector3(10.8f, 3.0f, 9.0f));
            AddHomeReflectionProbe(
                definitions,
                "world.probe.cell-0--3.home.utility",
                cellId,
                new Vector3(159.2f, 2.25f, -1039.5f),
                new Vector3(6.8f, 2.9f, 8.0f));
            AddHomeReflectionProbe(
                definitions,
                "world.probe.cell-0--3.home.garage",
                cellId,
                new Vector3(153.3864f, 2.25f, -1038.4102f),
                new Vector3(4.3f, 2.8f, 9.0f));
        }

        private static void AddHomeReflectionProbe(
            List<WorldReflectionProbeDefinition> definitions,
            string probeId,
            string cellId,
            Vector3 position,
            Vector3 size)
        {
            var definition = new WorldReflectionProbeDefinition();
            definition.ConfigureForAuthoring(
                probeId,
                cellId,
                position,
                size,
                0.12f,
                128);
            definitions.Add(definition);
        }

        private static void AddTeimoRefrigeratorLights(
            List<WorldLightDefinition> definitions)
        {
            AddTeimoRefrigeratorLight(
                definitions,
                "world.light.teimo.shop.refrigerator.01",
                "STORE/LOD/GFX_Store/StoreShelfs/fridge_door1",
                new Vector3(-1374.99f, 7.073f, 141.494f),
                new Vector3(90f, 147.397f, 90.001f),
                new Vector2(0.54187f, 0.4683933f));
            AddTeimoRefrigeratorLight(
                definitions,
                "world.light.teimo.shop.refrigerator.02",
                "STORE/LOD/GFX_Store/StoreShelfs/fridge_door2",
                new Vector3(-1375.44f, 7.078f, 142.2f),
                new Vector3(-270f, 0f, -57.444f),
                new Vector2(0.8432465f, 0.5421705f));
            AddTeimoRefrigeratorLight(
                definitions,
                "world.light.teimo.shop.refrigerator.03",
                "STORE/LOD/GFX_Store/StoreShelfs/fridge_small_door1",
                new Vector3(-1379.74f, 6.527f, 142.99f),
                new Vector3(-270f, -122.602f, 0f),
                new Vector2(0.1790103f, 1.450276f));
        }

        private static void AddTeimoRefrigeratorLight(
            List<WorldLightDefinition> definitions,
            string lightId,
            string donorReferencePath,
            Vector3 position,
            Vector3 rotationEulerAngles,
            Vector2 areaSize)
        {
            var definition = new WorldLightDefinition();
            definition.ConfigureForAuthoring(
                lightId,
                "cell_-3_0",
                position,
                rotationEulerAngles,
                Color.white,
                15_000f,
                3.5f,
                45f,
                65f,
                false,
                1f,
                15_000f,
                0.2f,
                0.01f,
                false,
                0f,
                WorldLightSourceKind.Point,
                WorldLightBakeMode.Realtime,
                WorldLightActivationPolicy.AlwaysOn,
                true);
            definition.SetDonorReferencePathForAuthoring(donorReferencePath);
            definition.SetAreaLightOverridesForAuthoring(
                areaSize,
                suppressEmission: true);
            definitions.Add(definition);
        }

        private static bool TryClassify(
            string sourcePath,
            out FixtureProfile profile)
        {
            profile = default;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            string normalized = sourcePath.Replace('\\', '/');
            string lower = normalized.ToLowerInvariant();
            string leaf = Path.GetFileName(normalized).ToLowerInvariant();
            if (leaf.StartsWith("lightpole", StringComparison.Ordinal))
            {
                profile = FixtureProfile.StreetLight;
                return true;
            }

            if (leaf.StartsWith("office_lamp", StringComparison.Ordinal))
            {
                profile = FixtureProfile.Office;
                return true;
            }

            if (leaf.Contains("toilet_lamp") ||
                leaf.Contains("toiletlamp") ||
                leaf == "livingroom_lamp" ||
                leaf == "kitchen_lamp" ||
                leaf == "lamp hallway" ||
                leaf == "lamp_hallway" ||
                leaf.Contains("paper lamp") ||
                leaf.Contains("lamp_paper"))
            {
                profile = FixtureProfile.Domestic;
                return true;
            }

            if (lower.StartsWith("jail/", StringComparison.Ordinal) &&
                lower.EndsWith("/light_bulb/glass", StringComparison.Ordinal))
            {
                profile = FixtureProfile.Domestic;
                return true;
            }

            if (leaf == "outdoor_lamp")
            {
                profile = FixtureProfile.Exterior;
                return true;
            }

            if (lower == "dancehall/lights/bulbs")
            {
                profile = FixtureProfile.Hall;
                return true;
            }

            return false;
        }

        private static string RootLocation(string sourcePath)
        {
            int separator = sourcePath.IndexOf('/');
            return separator > 0
                ? sourcePath.Substring(0, separator)
                : sourcePath;
        }

        private static Vector3 ResolveStreetLightPosition(
            DonorWorldBaselineEntityMetadata entity,
            Renderer renderer,
            Bounds worldBounds)
        {
            MeshFilter meshFilter = entity.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh == null)
            {
                return new Vector3(
                    worldBounds.center.x,
                    worldBounds.max.y - 0.45f,
                    worldBounds.center.z);
            }

            Bounds localBounds = mesh.bounds;
            Vector3 localPosition = localBounds.center;
            localPosition.y = localBounds.max.y - 0.45f;

            if (localBounds.size.x >= localBounds.size.z)
            {
                localPosition.x = Mathf.Abs(localBounds.max.x) >=
                                  Mathf.Abs(localBounds.min.x)
                    ? localBounds.max.x - 0.20f
                    : localBounds.min.x + 0.20f;
            }
            else
            {
                localPosition.z = Mathf.Abs(localBounds.max.z) >=
                                  Mathf.Abs(localBounds.min.z)
                    ? localBounds.max.z - 0.20f
                    : localBounds.min.z + 0.20f;
            }

            return meshFilter.transform.TransformPoint(localPosition);
        }

        private static Vector3 ResolveStreetLightRotation(
            DonorWorldBaselineEntityMetadata entity,
            Vector3 lightPosition)
        {
            Vector3 outward = lightPosition - entity.transform.position;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f)
            {
                return new Vector3(90f, 0f, 0f);
            }

            outward.Normalize();
            Vector3 direction =
                (outward * 0.56f + Vector3.down * 0.83f).normalized;
            return Quaternion.LookRotation(direction, Vector3.up)
                .eulerAngles;
        }

        private static PlayerSpawnDefinition ResolvePlayerSpawn()
        {
            Scene scene = EditorSceneManager.OpenScene(
                YardCellScenePath,
                OpenSceneMode.Single);
            DonorWorldBaselineEntityMetadata[] metadata =
                scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<
                            DonorWorldBaselineEntityMetadata>(true))
                    .ToArray();
            DonorWorldBaselineEntityMetadata bed = metadata.Single(entity =>
                string.Equals(
                    entity.SourceHierarchyPath,
                    PlayerBedPath,
                    StringComparison.Ordinal));
            DonorWorldBaselineEntityMetadata door = metadata.Single(entity =>
                string.Equals(
                    entity.SourceHierarchyPath,
                    PlayerBedroomDoorPath,
                    StringComparison.Ordinal));
            Renderer bedRenderer = bed.GetComponent<Renderer>();
            Renderer doorRenderer = door.GetComponent<Renderer>();
            if (bedRenderer == null || doorRenderer == null)
            {
                throw new InvalidOperationException(
                    "Player bedroom spawn anchors have no render bounds.");
            }

            Bounds bedBounds = bedRenderer.bounds;
            Vector3 direction =
                doorRenderer.bounds.center - bedBounds.center;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                throw new InvalidOperationException(
                    "Player bedroom spawn anchors overlap.");
            }

            direction.Normalize();
            float bedRadius = Mathf.Max(
                bedBounds.extents.x,
                bedBounds.extents.z);
            Vector3 position =
                bedBounds.center + direction * (bedRadius + 0.65f);
            position.y = bedBounds.min.y + 1.05f;
            Quaternion rotation = Quaternion.LookRotation(
                direction,
                Vector3.up);
            return new PlayerSpawnDefinition(position, rotation);
        }

        private static string SanitizeId(string value)
        {
            return value
                .Trim()
                .ToLowerInvariant()
                .Replace('_', '-')
                .Replace(' ', '-');
        }

        private static StreetLightOverride ResolveStreetLightOverride(
            FixtureCandidate fixture)
        {
            if (string.Equals(
                    fixture.StableId,
                    "8946ab55e770546f2c76e72945c54f24",
                    StringComparison.Ordinal))
            {
                return new StreetLightOverride(
                    new Vector3(-1355.972f, 15.098f, 136.196f),
                    new Vector3(75f, 0f, 37.948f),
                    35f);
            }

            if (string.Equals(
                    fixture.StableId,
                    "5321706220112780baeed0bbe56912b3",
                    StringComparison.Ordinal))
            {
                return new StreetLightOverride(
                    new Vector3(-1379.331f, 13.527f, 119.488f),
                    new Vector3(75f, 0f, 24.229f),
                    35f);
            }

            return default;
        }

        private static InteriorLightOverride ResolveInteriorLightOverride(
            FixtureCandidate fixture)
        {
            switch (fixture.StableId)
            {
                case "770b22bf8eb3d0bfeacfede083978dca":
                    return new InteriorLightOverride(
                        new Vector3(157.86172f, 3.574f, -1030.6177f),
                        new Vector3(90f, 0f, 0f),
                        1_500f,
                        5.5f,
                        78f,
                        118f,
                        0.35f);
                case "6ec5b3ebe08e5c321e68a7d649fa2d83":
                    return new InteriorLightOverride(
                        new Vector3(159.37042f, 3.1599321f, -1027.905f),
                        new Vector3(90f, 0f, 0f),
                        900f,
                        4.2f,
                        82f,
                        122f,
                        0f);
                case "44779c4c4e4cb4dabce3dbc5e862e68c":
                    return new InteriorLightOverride(
                        new Vector3(160.02295f, 3.4368477f, -1030.6078f),
                        new Vector3(90f, 0f, 0f),
                        1_500f,
                        5.5f,
                        78f,
                        118f,
                        0.35f);
                case "4b838825763b9266233ff52963a2d792":
                    return new InteriorLightOverride(
                        new Vector3(162.10258f, 3.2381797f, -1028.9606f),
                        new Vector3(90f, 0f, 0f),
                        700f,
                        3f,
                        75f,
                        115f,
                        0.2f);
                case "450510e720a076d53aeb44a05dd6480d":
                    return new InteriorLightOverride(
                        new Vector3(166.31563f, 3.4368472f, -1027.8579f),
                        new Vector3(90f, 0f, 0f),
                        900f,
                        4.2f,
                        82f,
                        122f,
                        0.2f);
                case "78aeb3c71258a73257d315d8e8eeb0ed":
                    return new InteriorLightOverride(
                        new Vector3(159.94843f, 2.8189328f, -1034.593f),
                        new Vector3(90f, 0f, 0f),
                        1_300f,
                        5f,
                        80f,
                        120f,
                        0f);
                case "babe31b77f1e3a06de0caa90083cec81":
                    return new InteriorLightOverride(
                        new Vector3(157.35641f, 3.3429327f, -1036.827f),
                        new Vector3(90f, 0f, 0f),
                        900f,
                        4f,
                        80f,
                        120f,
                        0.2f);
                case "d47572b57298d90c2aa5f707fc5519ff":
                    return new InteriorLightOverride(
                        new Vector3(157.35641f, 3.2989328f, -1039.4491f),
                        new Vector3(90f, 0f, 0f),
                        800f,
                        3.6f,
                        78f,
                        118f,
                        0.15f);
                case "daa0926d1766b35f989095845c1ccbab":
                    return new InteriorLightOverride(
                        new Vector3(165.40744f, 2.6349335f, -1032.9551f),
                        Vector3.zero,
                        1_500f,
                        5.5f,
                        78f,
                        118f,
                        0.35f);
                case "6a07d52868491a36248ac5e51ecd81d2":
                    return new InteriorLightOverride(
                        new Vector3(156.92699f, 1.61f, -1043.254f),
                        Vector3.zero,
                        650f,
                        3f,
                        72f,
                        112f,
                        0.1f);
                case "cbf1b6cb6074e84c3c2cc8042b8d3e44":
                    return new InteriorLightOverride(
                        new Vector3(153.37552f, 3.354f, -1040.9165f),
                        new Vector3(90f, 0f, 0f),
                        1_800f,
                        4.2f,
                        78f,
                        112f,
                        0f);
                case "cda8c8cbd2913a9f16e802515f9b230d":
                    return new InteriorLightOverride(
                        new Vector3(153.37552f, 3.354f, -1038.4955f),
                        new Vector3(90f, 0f, 0f),
                        1_800f,
                        4.2f,
                        78f,
                        112f,
                        0f);
                case "4f6ac2527ef70e970cb89ef70baaefa9":
                    return new InteriorLightOverride(
                        new Vector3(153.37553f, 3.354f, -1036.3374f),
                        new Vector3(90f, 0f, 0f),
                        1_800f,
                        4.2f,
                        78f,
                        112f,
                        0f);
                case "d76c0bb63327c058f2b82bc57a699d02":
                    return new InteriorLightOverride(
                        new Vector3(153.49457f, 3.234f, -1032.9905f),
                        new Vector3(45f, 0f, 0f),
                        1_100f,
                        8f,
                        65f,
                        95f,
                        0f);
                default:
                    return default;
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private readonly struct FixtureCandidate
        {
            public FixtureCandidate(
                string stableId,
                string cellId,
                string sourcePath,
                Vector3 position,
                Vector3 rotationEulerAngles,
                FixtureProfile profile)
            {
                StableId = stableId;
                CellId = cellId;
                SourcePath = sourcePath;
                Position = position;
                RotationEulerAngles = rotationEulerAngles;
                Profile = profile;
            }

            public string StableId { get; }
            public string CellId { get; }
            public string SourcePath { get; }
            public Vector3 Position { get; }
            public Vector3 RotationEulerAngles { get; }
            public FixtureProfile Profile { get; }
        }

        private readonly struct PlayerSpawnDefinition
        {
            public PlayerSpawnDefinition(
                Vector3 position,
                Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
        }

        private readonly struct StreetLightOverride
        {
            public StreetLightOverride(
                Vector3 position,
                Vector3 rotationEulerAngles,
                float range)
            {
                Position = position;
                RotationEulerAngles = rotationEulerAngles;
                Range = range;
                IsDefined = true;
            }

            public Vector3 Position { get; }
            public Vector3 RotationEulerAngles { get; }
            public float Range { get; }
            public bool IsDefined { get; }
        }

        private readonly struct InteriorLightOverride
        {
            public InteriorLightOverride(
                Vector3 position,
                Vector3 rotationEulerAngles,
                float intensityLumens,
                float range,
                float innerSpotAngle,
                float outerSpotAngle,
                float indirectMultiplier)
            {
                Position = position;
                RotationEulerAngles = rotationEulerAngles;
                IntensityLumens = intensityLumens;
                Range = range;
                InnerSpotAngle = innerSpotAngle;
                OuterSpotAngle = outerSpotAngle;
                IndirectMultiplier = indirectMultiplier;
                IsDefined = true;
            }

            public Vector3 Position { get; }
            public Vector3 RotationEulerAngles { get; }
            public float IntensityLumens { get; }
            public float Range { get; }
            public float InnerSpotAngle { get; }
            public float OuterSpotAngle { get; }
            public float IndirectMultiplier { get; }
            public bool IsDefined { get; }
        }

        private readonly struct FixtureProfile
        {
            private FixtureProfile(
                Color color,
                float intensity,
                float range,
                bool isStreetLight,
                bool castsShadows)
            {
                Color = color;
                Intensity = intensity;
                Range = range;
                IsStreetLight = isStreetLight;
                CastsShadows = castsShadows;
            }

            public static FixtureProfile StreetLight =>
                new FixtureProfile(
                    Color.white,
                    400f,
                    35f,
                    true,
                    true);

            public static FixtureProfile Office =>
                new FixtureProfile(
                    new Color(1f, 0.96f, 0.90f),
                    3_200f,
                    14f,
                    false,
                    true);

            public static FixtureProfile Domestic =>
                new FixtureProfile(
                    new Color(1f, 0.90f, 0.80f),
                    2_600f,
                    12f,
                    false,
                    true);

            public static FixtureProfile Exterior =>
                new FixtureProfile(
                    new Color(1f, 0.88f, 0.74f),
                    3_500f,
                    16f,
                    false,
                    true);

            public static FixtureProfile Hall =>
                new FixtureProfile(
                    new Color(1f, 0.82f, 0.68f),
                    4_000f,
                    20f,
                    false,
                    true);

            public Color Color { get; }
            public float Intensity { get; }
            public float Range { get; }
            public bool IsStreetLight { get; }
            public bool CastsShadows { get; }
        }
    }
}
