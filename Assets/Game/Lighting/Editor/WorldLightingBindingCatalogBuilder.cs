using System;
using System.Collections.Generic;
using System.IO;
using MSC.Bootstrap;
using MSC.LegacyImport;
using MSC.Lighting.Production;
using MSC.Interaction.Query;
using MSC.Weather.Production;
using MSC.World.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Lighting.Editor
{
    public static class WorldLightingBindingCatalogBuilder
    {
        public const string WorldCatalogPath =
            "Assets/Game/World/Content/Lighting/Phase1WorldLightingProbeCatalog.asset";
        public const string BindingRoot =
            "Assets/Game/Lighting/Content/Bindings";
        public const string BindingCatalogPath = BindingRoot +
            "/Phase1WorldLightingBindings.asset";
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string HomeGarageLightingZoneId =
            "zone.home.garage";
        private const string HomeGarageWeatherZoneId =
            "weather.shelter.home.garage.interior.v1";
        private const string HomeGarageEntryTransitionName =
            "GarageEntryExposureTransition";

        // The frozen shell AABB ends behind the actual garage-door threshold.
        // A camera standing just inside the opening consequently inherited the
        // exterior daylight exposure and made the realtime fluorescents look
        // switched off. This is the reviewed world-space door threshold from
        // the home/garage traversal anchor, plus a small exterior blend lip.
        private const float HomeGarageDoorThresholdWorldZ = -1033.23f;
        private const float HomeGarageDoorBlendLipMeters = 0.12f;
        private const float HomeGarageEntrySideInsetMeters = 0.15f;
        private const float HomeGarageEntryOverlapMeters = 0.08f;
        private const float HomeGarageEntryHeadroomMeters = 0.15f;

        // The donor stores both Teimo rooms under STORE and all six ceiling
        // lights under GFX_Store/Lamps. Only the eastern fixture nearest the
        // pub service point belongs to the pub; the other five light the shop.
        // Stable project-owned IDs keep that 5/1 split explicit without making
        // runtime behavior depend on donor hierarchy names.
        private static readonly HashSet<string> SharedTeimoPubLightIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "world.light.ba653c2356f3c338bf05d8fe6a63d4e8",
            };

        private static readonly HashSet<string> TeimoRefrigeratorLightIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "world.light.teimo.shop.refrigerator.01",
                "world.light.teimo.shop.refrigerator.02",
                "world.light.teimo.shop.refrigerator.03",
            };

        // The Phase 1 interaction contract intentionally groups the entrance,
        // corridor and living-hall lamps behind the hallway rocker. This keeps
        // the two physical entrance/corridor buttons synchronized and gives the
        // user-confirmed three-lamp behavior without hierarchy-name lookups.
        private static readonly HashSet<string> SharedHomeHallwayLightIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "world.light.44779c4c4e4cb4dabce3dbc5e862e68c",
                "world.light.babe31b77f1e3a06de0caa90083cec81",
                "world.light.daa0926d1766b35f989095845c1ccbab",
            };

        private const string HomeToiletLightId =
            "world.light.4b838825763b9266233ff52963a2d792";
        private const string HomeOutdoorLightId =
            "world.light.d76c0bb63327c058f2b82bc57a699d02";

        [MenuItem("Tools/Lighting/Build World Lighting Bindings")]
        public static void BuildMenu()
        {
            LightingProfileCatalog profiles =
                LightingContentBuilder.BuildProfiles();
            BuildAndInstall(profiles);
        }

        public static WorldLightingBindingCatalog BuildAndInstall(
            LightingProfileCatalog profiles)
        {
            WorldLightingProbeCatalog world =
                AssetDatabase.LoadAssetAtPath<WorldLightingProbeCatalog>(
                    WorldCatalogPath);
            if (world == null)
            {
                throw new InvalidOperationException(
                    "Accepted Phase 1 world lighting catalog is missing.");
            }

            Dictionary<string, string> donorPaths = ResolveDonorPaths(world);
            Dictionary<string, SwitchAnchor> switches =
                ResolveHomeSwitchAnchors();

            // Opening the generated donor scenes may unload unreferenced native
            // objects. Reload the catalog after all read-only scene scans so the
            // asset reference below cannot become a destroyed Unity object.
            world = AssetDatabase.LoadAssetAtPath<WorldLightingProbeCatalog>(
                WorldCatalogPath);
            if (world == null)
            {
                throw new InvalidOperationException(
                    "Accepted Phase 1 world lighting catalog was unloaded and could not be reloaded.");
            }

            EnsureFolder(BindingRoot);
            var bindings = new List<WorldLightingFixtureBinding>(
                world.Lights.Count);
            for (int index = 0; index < world.Lights.Count; index++)
            {
                WorldLightDefinition light = world.Lights[index];
                string referencePath = light.DonorReferencePath;
                if (string.IsNullOrWhiteSpace(referencePath) &&
                    donorPaths.TryGetValue(light.LightId, out string discovered))
                {
                    referencePath = discovered;
                    light.SetDonorReferencePathForAuthoring(discovered);
                }

                bindings.Add(BuildBinding(light, referencePath));
            }
            EditorUtility.SetDirty(world);

            WorldLightingBindingCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorldLightingBindingCatalog>(
                    BindingCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<
                    WorldLightingBindingCatalog>();
                AssetDatabase.CreateAsset(catalog, BindingCatalogPath);
            }

            catalog.ConfigureForAuthoring(
                "lighting.world-bindings.phase1.v1",
                world,
                bindings);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            InstallInBootstrap(profiles, catalog, switches);
            Debug.Log(
                $"[Lighting] Bound {bindings.Count} accepted streamed " +
                "world lights to the production lighting runtime.");
            return catalog;
        }

        private static WorldLightingFixtureBinding BuildBinding(
            WorldLightDefinition light,
            string donorReferencePath)
        {
            string path = (donorReferencePath ?? string.Empty)
                .Replace('\\', '/')
                .ToLowerInvariant();
            string leaf = Path.GetFileName(path);
            LightFixtureCategory category;
            string circuit;
            string switchId = string.Empty;
            string zone;
            string business = string.Empty;

            if (light.ActivationPolicy ==
                    WorldLightActivationPolicy.NightOnly &&
                (path.Contains("/streetlights/") ||
                 leaf.StartsWith("lightpole", StringComparison.Ordinal)))
            {
                category = LightFixtureCategory.StreetLamp;
                circuit = "grid.street.public";
                zone = "zone.exterior";
            }
            else if (IsTeimoRefrigeratorLight(light.LightId))
            {
                category = LightFixtureCategory.RefrigeratedDisplay;
                circuit = "grid.teimo.refrigeration";
                zone = "zone.teimo.shop";
                business = "service.teimo.shop";
            }
            else if (path.StartsWith("store/", StringComparison.Ordinal) &&
                     (path.Contains("pub") ||
                      IsSharedTeimoPubLight(light.LightId)))
            {
                category = LightFixtureCategory.TeimoPub;
                circuit = "grid.teimo.pub";
                zone = "zone.teimo.pub";
                business = "service.teimo.pub";
            }
            else if (path.StartsWith("store/", StringComparison.Ordinal))
            {
                category = LightFixtureCategory.TeimoShop;
                circuit = "grid.teimo.shop";
                zone = "zone.teimo.shop";
                business = "service.teimo.shop";
            }
            else if (path.Contains("fleetari") ||
                     path.Contains("repairshop"))
            {
                category = LightFixtureCategory.FleetariWorkshop;
                circuit = "grid.fleetari.workshop";
                zone = "zone.fleetari.workshop";
                business = "service.fleetari.workshop";
            }
            else if (string.Equals(
                         light.LightId,
                         HomeOutdoorLightId,
                         StringComparison.Ordinal))
            {
                // Donor evidence has no motion/presence sensor. The outdoor
                // fixture is therefore the visible load of the garage rocker,
                // sharing the existing home circuit, fuse and metering path.
                category = LightFixtureCategory.HomeExterior;
                circuit = "grid.home.garage";
                switchId = "switch.home.garage";
                zone = "zone.exterior";
            }
            else if (light.ActivationPolicy ==
                     WorldLightActivationPolicy.NightOnly)
            {
                category = LightFixtureCategory.ExteriorBuilding;
                circuit = "grid.exterior.buildings";
                zone = "zone.exterior";
            }
            else if (SharedHomeHallwayLightIds.Contains(light.LightId))
            {
                category = LightFixtureCategory.DomesticIncandescent;
                circuit = "grid.home.hallway";
                switchId = "switch.home.hallway";
                zone = "zone.home.hallway";
            }
            else if (string.Equals(
                         light.LightId,
                         HomeToiletLightId,
                         StringComparison.Ordinal))
            {
                category = LightFixtureCategory.DomesticIncandescent;
                circuit = "grid.home.toilet";
                switchId = "switch.home.toilet";
                zone = "zone.home.toilet";
            }
            else if (path.StartsWith("yard/", StringComparison.Ordinal))
            {
                category = leaf.Contains("office_lamp")
                    ? LightFixtureCategory.Fluorescent
                    : LightFixtureCategory.DomesticIncandescent;
                string room = InferHomeRoom(path);
                circuit = "grid.home." + room;
                switchId = "switch.home." + room;
                zone = "zone.home." + room;
            }
            else if (leaf.Contains("office_lamp"))
            {
                category = LightFixtureCategory.Fluorescent;
                circuit = "grid.building.fluorescent";
                zone = "zone.interior.other";
            }
            else
            {
                category = light.SourceKind == WorldLightSourceKind.Spot
                    ? LightFixtureCategory.ExteriorBuilding
                    : LightFixtureCategory.EnclosedCeiling;
                circuit = "grid.building.general";
                zone = light.ActivationPolicy ==
                    WorldLightActivationPolicy.NightOnly
                    ? "zone.exterior"
                    : "zone.interior.other";
            }

            var binding = new WorldLightingFixtureBinding();
            binding.ConfigureForAuthoring(
                light.LightId,
                category,
                "source.grid.main",
                circuit,
                switchId,
                zone,
                business);
            return binding;
        }

        private static bool IsSharedTeimoPubLight(string lightId) =>
            !string.IsNullOrWhiteSpace(lightId) &&
            SharedTeimoPubLightIds.Contains(lightId);

        private static bool IsTeimoRefrigeratorLight(string lightId) =>
            !string.IsNullOrWhiteSpace(lightId) &&
            TeimoRefrigeratorLightIds.Contains(lightId);

        private static Dictionary<string, string> ResolveDonorPaths(
            WorldLightingProbeCatalog world)
        {
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < world.Lights.Count; index++)
            {
                string id = world.Lights[index].LightId;
                int separator = id.LastIndexOf('.');
                wanted.Add(separator >= 0 ? id.Substring(separator + 1) : id);
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] sceneGuids = AssetDatabase.FindAssets(
                "t:Scene",
                new[]
                {
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells",
                });
            for (int sceneIndex = 0;
                 sceneIndex < sceneGuids.Length && result.Count < wanted.Count;
                 sceneIndex++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(
                    sceneGuids[sceneIndex]);
                Scene scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Single);
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    DonorWorldBaselineEntityMetadata[] metadata =
                        roots[rootIndex].GetComponentsInChildren<
                            DonorWorldBaselineEntityMetadata>(true);
                    for (int index = 0; index < metadata.Length; index++)
                    {
                        DonorWorldBaselineEntityMetadata entity = metadata[index];
                        if (!wanted.Contains(entity.StableId))
                        {
                            continue;
                        }

                        result["world.light." + entity.StableId] =
                            entity.SourceHierarchyPath;
                    }
                }
            }

            return result;
        }

        private static string InferHomeRoom(string path)
        {
            if (path.Contains("bedroom1")) return "bedroom-boy";
            if (path.Contains("bedroom2")) return "bedroom-parents";
            if (path.Contains("kitchen")) return "kitchen";
            if (path.Contains("livingroom")) return "living-room";
            if (path.Contains("bathroom")) return "bathroom";
            if (path.Contains("sauna")) return "bathroom";
            if (path.Contains("middleroom")) return "hallway";
            if (path.Contains("hallway")) return "hallway";
            if (path.Contains("garage")) return "garage";
            if (path.Contains("/building/lod/")) return "garage";
            return "hallway";
        }

        private static void InstallInBootstrap(
            LightingProfileCatalog profiles,
            WorldLightingBindingCatalog bindings,
            Dictionary<string, SwitchAnchor> switches)
        {
            Scene scene = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            profiles = AssetDatabase.LoadAssetAtPath<LightingProfileCatalog>(
                LightingContentBuilder.CatalogPath);
            bindings = AssetDatabase.LoadAssetAtPath<
                WorldLightingBindingCatalog>(BindingCatalogPath);
            if (profiles == null || bindings == null)
            {
                throw new InvalidOperationException(
                    "Lighting catalogs could not be reloaded after opening Bootstrap.");
            }

            ProductionWorldStreamingInstaller world = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length && world == null;
                 rootIndex++)
            {
                world = roots[rootIndex].GetComponentInChildren<
                    ProductionWorldStreamingInstaller>(true);
            }

            if (world == null)
            {
                throw new InvalidOperationException(
                    "Bootstrap has no ProductionWorldStreamingInstaller.");
            }

            ProductionLightingInstaller installer =
                world.GetComponent<ProductionLightingInstaller>() ??
                world.gameObject.AddComponent<ProductionLightingInstaller>();
            installer.ConfigureForAuthoring(profiles, bindings);
            BuildGameplayBindings(
                scene,
                world.transform,
                installer.RuntimeManager,
                bindings,
                switches);
            EditorUtility.SetDirty(installer);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void BuildGameplayBindings(
            Scene scene,
            Transform compositionRoot,
            LightingRuntimeManager runtime,
            WorldLightingBindingCatalog bindings,
            Dictionary<string, SwitchAnchor> switches)
        {
            Transform existing = compositionRoot.Find(
                "LightingGameplayBindings");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var root = new GameObject("LightingGameplayBindings");
            root.transform.SetParent(compositionRoot, false);
            var groupedPositions = new Dictionary<string, List<Vector3>>(
                StringComparer.Ordinal);
            for (int index = 0;
                 index < bindings.SourceCatalog.Lights.Count;
                 index++)
            {
                WorldLightDefinition light =
                    bindings.SourceCatalog.Lights[index];
                if (!bindings.TryGet(
                        light.LightId,
                        out WorldLightingFixtureBinding binding) ||
                    string.Equals(
                        binding.ZoneId,
                        "zone.exterior",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!groupedPositions.TryGetValue(
                        binding.ZoneId,
                        out List<Vector3> positions))
                {
                    positions = new List<Vector3>();
                    groupedPositions.Add(binding.ZoneId, positions);
                }

                positions.Add(light.Position);
            }

            var zoneData = new List<ZoneAuthoringData>();
            foreach (KeyValuePair<string, List<Vector3>> pair in
                     groupedPositions)
            {
                Bounds bounds = new Bounds(pair.Value[0], Vector3.zero);
                for (int index = 1; index < pair.Value.Count; index++)
                {
                    bounds.Encapsulate(pair.Value[index]);
                }

                Vector3 size = bounds.size + new Vector3(3.5f, 3f, 3.5f);
                size.x = Mathf.Max(size.x, 3.5f);
                size.y = Mathf.Max(size.y, 3f);
                size.z = Mathf.Max(size.z, 3.5f);
                Vector3 center = bounds.center + Vector3.down * 1f;
                var zoneObject = new GameObject(
                    "LightingZone_" + pair.Key.Replace('.', '_'));
                zoneObject.transform.SetParent(root.transform, false);
                zoneObject.transform.position = center;
                BoxCollider collider = zoneObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = size;
                LightingZone zone = zoneObject.AddComponent<LightingZone>();
                zoneData.Add(new ZoneAuthoringData(
                    pair.Key,
                    zone,
                    collider,
                    center));
            }

            AlignHomeGarageLightingAndExposureZones(scene, zoneData);

            for (int index = 0; index < zoneData.Count; index++)
            {
                ZoneAuthoringData current = zoneData[index];
                var adjacent = new List<string>();
                for (int otherIndex = 0;
                     otherIndex < zoneData.Count;
                     otherIndex++)
                {
                    ZoneAuthoringData other = zoneData[otherIndex];
                    if (index == otherIndex)
                    {
                        continue;
                    }

                    bool sameHome = current.Id.StartsWith(
                            "zone.home.",
                            StringComparison.Ordinal) &&
                        other.Id.StartsWith(
                            "zone.home.",
                            StringComparison.Ordinal);
                    if (sameHome && Vector3.Distance(
                            current.Center,
                            other.Center) <= 12f)
                    {
                        adjacent.Add(other.Id);
                    }
                }

                current.Zone.ConfigureForAuthoring(
                    current.Id,
                    ResolveZoneType(current.Id),
                    current.Collider,
                    Vector3.zero,
                    current.Collider.size,
                    adjacent.ToArray(),
                    0.75f);
            }

            var requiredSwitchIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < bindings.Bindings.Count; index++)
            {
                string id = bindings.Bindings[index].SwitchId;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    requiredSwitchIds.Add(id);
                }
            }

            foreach (KeyValuePair<string, SwitchAnchor> pair in switches)
            {
                string switchId = "switch.home." + pair.Key;
                if (!requiredSwitchIds.Contains(switchId))
                {
                    continue;
                }

                var switchObject = new GameObject();
                switchObject.name = "LightSwitch_" + pair.Key;
                switchObject.transform.SetParent(root.transform, true);
                switchObject.transform.position = pair.Value.Position;
                switchObject.transform.rotation = pair.Value.Rotation;
                switchObject.transform.localScale =
                    new Vector3(0.08f, 0.12f, 0.035f);
                switchObject.AddComponent<BoxCollider>();
                LightSwitchInteractionTarget target =
                    switchObject.AddComponent<LightSwitchInteractionTarget>();
                target.ConfigureForAuthoring(
                    switchId,
                    runtime,
                    switchObject.transform,
                    new Vector3(12f, 0f, 0f),
                    new Vector3(-12f, 0f, 0f),
                    "LIGHT SWITCH — " + pair.Key.ToUpperInvariant());
                InteractionTargetHost host =
                    switchObject.AddComponent<InteractionTargetHost>();
                host.Configure(target);
            }
        }

        private static void AlignHomeGarageLightingAndExposureZones(
            Scene scene,
            List<ZoneAuthoringData> zoneData)
        {
            int lightingZoneIndex = zoneData.FindIndex(value =>
                string.Equals(
                    value.Id,
                    HomeGarageLightingZoneId,
                    StringComparison.Ordinal));
            if (lightingZoneIndex < 0)
            {
                throw new InvalidOperationException(
                    "The generated home-garage lighting zone is missing.");
            }

            WeatherZone weatherZone = FindWeatherZone(
                scene,
                HomeGarageWeatherZoneId);
            BoxCollider interior = weatherZone != null
                ? weatherZone.GetComponent<BoxCollider>()
                : null;
            if (weatherZone == null || interior == null ||
                !interior.enabled || !interior.isTrigger)
            {
                throw new InvalidOperationException(
                    "The audited home-garage indoor weather zone is missing " +
                    "its enabled trigger bounds.");
            }

            Transform previous = weatherZone.transform.Find(
                HomeGarageEntryTransitionName);
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            }

            Bounds interiorBounds = GetAuthoredBoxWorldBounds(interior);
            Bounds transitionBounds = CreateGarageEntryTransitionBounds(
                interiorBounds);
            var transitionObject = new GameObject(
                HomeGarageEntryTransitionName);
            transitionObject.transform.SetParent(weatherZone.transform, true);
            transitionObject.transform.position = transitionBounds.center;
            BoxCollider transition = transitionObject.AddComponent<
                BoxCollider>();
            transition.isTrigger = true;
            transition.size = DivideByLossyScale(
                transitionBounds.size,
                transitionObject.transform.lossyScale);
            weatherZone.ConfigureForAuthoring(
                weatherZone.StableId,
                weatherZone.Profile,
                weatherZone.Priority,
                interior,
                transition);

            Bounds operationalBounds = interiorBounds;
            operationalBounds.Encapsulate(transitionBounds);
            ZoneAuthoringData lighting = zoneData[lightingZoneIndex];
            lighting.Zone.transform.position = operationalBounds.center;
            lighting.Collider.size = DivideByLossyScale(
                operationalBounds.size,
                lighting.Zone.transform.lossyScale);
            zoneData[lightingZoneIndex] = new ZoneAuthoringData(
                lighting.Id,
                lighting.Zone,
                lighting.Collider,
                operationalBounds.center);

            EditorUtility.SetDirty(weatherZone);
            EditorUtility.SetDirty(interior);
            EditorUtility.SetDirty(transition);
            EditorUtility.SetDirty(lighting.Zone);
            EditorUtility.SetDirty(lighting.Collider);
        }

        private static WeatherZone FindWeatherZone(
            Scene scene,
            string stableId)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                WeatherZone[] candidates = roots[rootIndex]
                    .GetComponentsInChildren<WeatherZone>(true);
                for (int index = 0; index < candidates.Length; index++)
                {
                    if (string.Equals(
                            candidates[index].StableId,
                            stableId,
                            StringComparison.Ordinal))
                    {
                        return candidates[index];
                    }
                }
            }

            return null;
        }

        private static Bounds CreateGarageEntryTransitionBounds(
            Bounds interiorBounds)
        {
            Vector3 minimum = interiorBounds.min;
            Vector3 maximum = interiorBounds.max;
            minimum.x += HomeGarageEntrySideInsetMeters;
            maximum.x -= HomeGarageEntrySideInsetMeters;
            minimum.z = interiorBounds.max.z -
                HomeGarageEntryOverlapMeters;
            maximum.z = Mathf.Max(
                interiorBounds.max.z,
                HomeGarageDoorThresholdWorldZ +
                HomeGarageDoorBlendLipMeters);
            maximum.y += HomeGarageEntryHeadroomMeters;
            if (minimum.x >= maximum.x || minimum.z >= maximum.z)
            {
                throw new InvalidOperationException(
                    "Home-garage entry transition bounds are degenerate.");
            }

            var result = new Bounds();
            result.SetMinMax(minimum, maximum);
            return result;
        }

        private static Vector3 DivideByLossyScale(
            Vector3 worldSize,
            Vector3 lossyScale) =>
            new Vector3(
                worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(lossyScale.x)),
                worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(lossyScale.y)),
                worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(lossyScale.z)));

        private static Bounds GetAuthoredBoxWorldBounds(BoxCollider box)
        {
            Vector3 scale = box.transform.lossyScale;
            Vector3 size = Vector3.Scale(
                box.size,
                new Vector3(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.y),
                    Mathf.Abs(scale.z)));
            return new Bounds(
                box.transform.TransformPoint(box.center),
                size);
        }

        private static Dictionary<string, SwitchAnchor>
            ResolveHomeSwitchAnchors()
        {
            const string yardScene =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_0_-3_Legacy.unity";
            var result = new Dictionary<string, SwitchAnchor>(
                StringComparer.Ordinal);
            Scene scene = EditorSceneManager.OpenScene(
                yardScene,
                OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                DonorWorldBaselineEntityMetadata[] metadata =
                    roots[rootIndex].GetComponentsInChildren<
                        DonorWorldBaselineEntityMetadata>(true);
                for (int index = 0; index < metadata.Length; index++)
                {
                    DonorWorldBaselineEntityMetadata entity = metadata[index];
                    string path = entity.SourceHierarchyPath ?? string.Empty;
                    if (!path.EndsWith(
                            "/light_switch_button",
                            StringComparison.OrdinalIgnoreCase) ||
                        !TryMapHomeSwitch(path, out string room))
                    {
                        continue;
                    }

                    result[room] = new SwitchAnchor(
                        entity.transform.position,
                        entity.transform.rotation);
                }
            }

            return result;
        }

        private static bool TryMapHomeSwitch(
            string path,
            out string room)
        {
            string lower = path.ToLowerInvariant();
            if (lower.Contains("switch_kitchen")) room = "kitchen";
            else if (lower.Contains("switch_bedroomboy")) room = "bedroom-boy";
            else if (lower.Contains("switch_bedroomparents")) room = "bedroom-parents";
            else if (lower.Contains("switch_bathroom")) room = "bathroom";
            else if (lower.Contains("switch_garage")) room = "garage";
            else if (lower.Contains("switch_hallway")) room = "hallway";
            else if (lower.Contains("switch_entry")) room = "hallway";
            else if (lower.Contains("switch_wc")) room = "toilet";
            else
            {
                room = string.Empty;
                return false;
            }

            return true;
        }

        private static LightingZoneType ResolveZoneType(string id)
        {
            if (id.StartsWith("zone.home.", StringComparison.Ordinal))
                return LightingZoneType.DomesticRoom;
            if (id == "zone.teimo.shop") return LightingZoneType.TeimoShop;
            if (id == "zone.teimo.pub") return LightingZoneType.TeimoPub;
            if (id == "zone.fleetari.workshop")
                return LightingZoneType.FleetariWorkshop;
            return LightingZoneType.OtherInterior;
        }

        private readonly struct SwitchAnchor
        {
            public SwitchAnchor(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
        }

        private readonly struct ZoneAuthoringData
        {
            public ZoneAuthoringData(
                string id,
                LightingZone zone,
                BoxCollider collider,
                Vector3 center)
            {
                Id = id;
                Zone = zone;
                Collider = collider;
                Center = center;
            }

            public string Id { get; }
            public LightingZone Zone { get; }
            public BoxCollider Collider { get; }
            public Vector3 Center { get; }
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
