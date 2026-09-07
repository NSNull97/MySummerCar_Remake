using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MSC.Lighting.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Lighting.Editor
{
    public static class LightingMapBuilder
    {
        public const string ReportRoot = "Docs/Lighting";
        private static readonly int EmissiveColorId =
            Shader.PropertyToID("_EmissiveColor");
        private static readonly int EmissiveIntensityId =
            Shader.PropertyToID("_EmissiveIntensity");
        private static readonly string[] LightHints =
        {
            "light", "lamp", "bulb", "headlight", "beam", "fluorescent",
            "ceiling", "streetlamp", "street_lamp", "spotlight", "lantern",
            "taillight", "brakelight", "indicator", "blinker", "reverse",
            "licenseplate", "dashboard", "interiorlight", "valaisin", "lamppu",
        };

        private static readonly string[] SwitchHints =
        {
            "switch", "breaker", "kytkin", "katkaisin",
        };

        private sealed class Candidate
        {
            public Transform Transform = null;
            public Light Light;
            public Renderer[] EmissiveRenderers = Array.Empty<Renderer>();
            public bool HasSwitch;
            public LightFixtureCategory Category;
            public string Circuit = string.Empty;
            public float Confidence;
            public string Classification = string.Empty;
            public string ReviewReason = string.Empty;
        }

        [MenuItem("Tools/Lighting/Build Lighting Map")]
        public static void BuildLightingMapMenu()
        {
            BuildFromBatch();
        }

        public static void BuildFromBatch()
        {
            LightingProfileCatalog catalog =
                LightingContentBuilder.BuildProfiles();
            WorldLightingBindingCatalogBuilder.BuildAndInstall(catalog);
            LightingAuditDocument before = ScanProject(false);
            WriteReports(before, "before");
            catalog = AssetDatabase.LoadAssetAtPath<LightingProfileCatalog>(
                LightingContentBuilder.CatalogPath);
            int configured = ApplySafePlacements(catalog);
            LightingAuditDocument after = ScanProject(false);
            WriteReports(after, string.Empty);
            Debug.Log(
                $"[LightingMapBuilder] Scanned {after.scenesScanned} scenes " +
                $"and {after.prefabsScanned} prefabs; candidates=" +
                $"{after.candidates}, configured={configured}, manual=" +
                $"{after.manualReview}.");
        }

        public static void RebuildAuditReportsFromBatch()
        {
            LightingAuditDocument audit = ScanProject(false);
            WriteReports(audit, string.Empty);
            Debug.Log(
                $"[LightingMapBuilder] Rebuilt audit reports for " +
                $"{audit.scenesScanned} scenes and {audit.prefabsScanned} " +
                $"prefabs; candidates={audit.candidates}, manual=" +
                $"{audit.manualReview}.");
        }

        public static LightingAuditDocument ScanProject(
            bool includeThirdParty,
            LightingProfileCatalog catalog = null)
        {
            var document = new LightingAuditDocument
            {
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
            };

            string[] scenes = FindGameAssets("t:Scene", includeThirdParty);
            string[] prefabs = FindGameAssets("t:Prefab", includeThirdParty);
            Array.Sort(scenes, StringComparer.Ordinal);
            Array.Sort(prefabs, StringComparer.Ordinal);

            for (int index = 0; index < scenes.Length; index++)
            {
                string scenePath = scenes[index];
                try
                {
                    Scene scene = EditorSceneManager.OpenScene(
                        scenePath,
                        OpenSceneMode.Single);
                    document.scenesScanned++;
                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    {
                        CollectAuditRecords(
                            roots[rootIndex],
                            scenePath,
                            string.Empty,
                            document.records);
                    }
                }
                catch (Exception exception)
                {
                    document.records.Add(CreateFailureRecord(
                        scenePath,
                        exception.Message));
                }
            }

            for (int index = 0; index < prefabs.Length; index++)
            {
                string prefabPath = prefabs[index];
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(prefabPath);
                    document.prefabsScanned++;
                    CollectAuditRecords(
                        root,
                        prefabPath,
                        AssetDatabase.AssetPathToGUID(prefabPath),
                        document.records);
                }
                catch (Exception exception)
                {
                    document.records.Add(CreateFailureRecord(
                        prefabPath,
                        exception.Message));
                }
                finally
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            document.candidates = document.records.Count;
            for (int index = 0; index < document.records.Count; index++)
            {
                LightingAuditRecord record = document.records[index];
                if (record.lightPlacementApplied)
                {
                    document.configuredFixtures++;
                }

                if (record.manualReviewRequired)
                {
                    document.manualReview++;
                }
            }

            return document;
        }

        public static int ApplySafePlacements(LightingProfileCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            int configured = 0;
            string[] scenes = FindGameAssets("t:Scene", false);
            string[] prefabs = FindGameAssets("t:Prefab", false);
            Array.Sort(scenes, StringComparer.Ordinal);
            Array.Sort(prefabs, StringComparer.Ordinal);

            for (int index = 0; index < scenes.Length; index++)
            {
                string scenePath = scenes[index];
                if (!IsSafeProjectOwnedAsset(scenePath))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Single);
                catalog = ReloadCatalog();
                bool changed = false;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    changed |= ConfigureSafeCandidates(
                        roots[rootIndex],
                        scenePath,
                        catalog,
                        ref configured);
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            for (int index = 0; index < prefabs.Length; index++)
            {
                string prefabPath = prefabs[index];
                if (!IsSafeProjectOwnedAsset(prefabPath))
                {
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    catalog = ReloadCatalog();
                    bool changed = ConfigureSafeCandidates(
                            root,
                            prefabPath,
                            catalog,
                            ref configured);
                    changed |= ConfigureVehicleAdapter(root);
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            return configured;
        }

        private static LightingProfileCatalog ReloadCatalog()
        {
            LightingProfileCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LightingProfileCatalog>(
                    LightingContentBuilder.CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    "Lighting profile catalog could not be reloaded after an asset open operation.");
            }

            return catalog;
        }

        private static bool ConfigureSafeCandidates(
            GameObject root,
            string assetPath,
            LightingProfileCatalog catalog,
            ref int configured)
        {
            bool changed = false;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                changed |= RemoveUnsafeAutoDirectionalFixture(
                    transforms[index],
                    assetPath);
                Candidate candidate = Analyze(transforms[index]);
                if (candidate == null || candidate.Light == null ||
                    candidate.Confidence < 0.7f)
                {
                    continue;
                }

                GameLightFixture fixture =
                    candidate.Light.GetComponent<GameLightFixture>();
                if (fixture == null)
                {
                    fixture = candidate.Light.gameObject.AddComponent<
                        GameLightFixture>();
                }

                LightFixtureProfile profile = catalog.GetRequiredProfile(
                    candidate.Category);
                string hierarchy = GetHierarchyPath(candidate.Transform);
                string fixtureId = "lighting.fixture.auto." +
                    StableHash(assetPath + "|" + hierarchy);
                string switchId = candidate.HasSwitch
                    ? candidate.Circuit + ".switch.auto"
                    : string.Empty;
                fixture.ConfigureForAuthoring(
                    fixtureId,
                    profile,
                    new[] { candidate.Light },
                    candidate.EmissiveRenderers,
                    InferPowerSource(candidate.Category),
                    candidate.Circuit,
                    switchId,
                    InferZone(assetPath, hierarchy),
                    InferBusiness(candidate.Category),
                    InferVehicleChannel(candidate.Category),
                    true);
                EditorUtility.SetDirty(fixture);
                EditorUtility.SetDirty(candidate.Light);
                configured++;
                changed = true;
            }

            return changed;
        }

        private static bool RemoveUnsafeAutoDirectionalFixture(
            Transform transform,
            string assetPath)
        {
            Light light = transform.GetComponent<Light>();
            GameLightFixture fixture = transform.GetComponent<GameLightFixture>();
            if (light == null || !IsGlobalDirectionalLight(transform, light))
            {
                return false;
            }

            bool changed = false;
            if (fixture != null && fixture.FixtureId.StartsWith(
                    "lighting.fixture.auto.",
                    StringComparison.Ordinal))
            {
                RestoreDirectionalPhotometry(assetPath, transform, light);
                UnityEngine.Object.DestroyImmediate(fixture, true);
                EditorUtility.SetDirty(light);
                changed = true;
            }

            if (IsKnownM06DirectionalBaseline(assetPath, transform))
            {
                if (light.lightUnit != LightUnit.Candela)
                {
                    light.lightUnit = LightUnit.Candela;
                    EditorUtility.SetDirty(light);
                    changed = true;
                }

                HDAdditionalLightData orphanedHd =
                    light.GetComponent<HDAdditionalLightData>();
                if (orphanedHd != null)
                {
                    UnityEngine.Object.DestroyImmediate(orphanedHd, true);
                    changed = true;
                }
            }

            return changed;
        }

        private static void RestoreDirectionalPhotometry(
            string assetPath,
            Transform transform,
            Light light)
        {
            Light source = PrefabUtility.GetCorrespondingObjectFromSource(light);
            if (source != null &&
                source.GetComponent<GameLightFixture>() == null)
            {
                CopyLightPhotometry(source, light);
                HDAdditionalLightData sourceHd =
                    source.GetComponent<HDAdditionalLightData>();
                HDAdditionalLightData targetHd =
                    light.GetComponent<HDAdditionalLightData>();
                if (sourceHd != null && targetHd != null)
                {
                    CopyHdrpPhotometry(sourceHd, targetHd);
                }

                return;
            }

            string context = (assetPath + "/" + GetHierarchyPath(transform))
                .ToLowerInvariant();
            light.type = LightType.Directional;
            light.lightUnit = LightUnit.Lux;
            light.range = 10f;
            light.spotAngle = 30f;
            light.innerSpotAngle = 21.80208f;
            light.areaSize = Vector2.one;
            light.colorTemperature = 6570f;
            light.useColorTemperature = false;
            light.enableSpotReflector = true;

            if (context.Contains("late_day", StringComparison.Ordinal) ||
                context.Contains("lateday", StringComparison.Ordinal))
            {
                light.color = new Color(1f, 0.78f, 0.56f, 1f);
                light.intensity = 48000f;
            }
            else if (context.Contains("neutral", StringComparison.Ordinal))
            {
                light.color = new Color(1f, 0.97f, 0.9f, 1f);
                light.intensity = 95000f;
            }
            else if (context.Contains("m06_", StringComparison.Ordinal))
            {
                light.lightUnit = LightUnit.Candela;
                light.color = Color.white;
                light.intensity = 100000f;
            }
            else
            {
                light.color = new Color(1f, 0.956f, 0.839f, 1f);
                light.intensity = 100000f;
            }

            HDAdditionalLightData hd =
                light.GetComponent<HDAdditionalLightData>();
            if (hd != null)
            {
                light.shapeRadius = 0.025f;
                hd.affectsVolumetric = true;
                hd.volumetricDimmer = 1f;
                hd.volumetricShadowDimmer = 1f;
                EditorUtility.SetDirty(hd);
            }
        }

        private static bool IsKnownM06DirectionalBaseline(
            string assetPath,
            Transform transform)
        {
            if (!transform.name.Equals(
                    "M06_DirectionalLight",
                    StringComparison.Ordinal))
            {
                return false;
            }

            return assetPath.Equals(
                    "Assets/Game/Vehicle/Content/Simulation/Scenes/VehicleSimulationPrototype.unity",
                    StringComparison.Ordinal) ||
                assetPath.Equals(
                    "Assets/Game/Vehicle/Content/Validation/Scenes/VehiclePhysicsValidation.unity",
                    StringComparison.Ordinal);
        }

        private static void CopyLightPhotometry(Light source, Light target)
        {
            target.type = source.type;
            target.lightUnit = source.lightUnit;
            target.intensity = source.intensity;
            target.range = source.range;
            target.color = source.color;
            target.useColorTemperature = source.useColorTemperature;
            target.colorTemperature = source.colorTemperature;
            target.enableSpotReflector = source.enableSpotReflector;
            target.innerSpotAngle = source.innerSpotAngle;
            target.spotAngle = source.spotAngle;
            target.areaSize = source.areaSize;
        }

        private static void CopyHdrpPhotometry(
            HDAdditionalLightData source,
            HDAdditionalLightData target)
        {
            target.GetComponent<Light>().shapeRadius = source.GetComponent<Light>().shapeRadius;
            target.affectsVolumetric = source.affectsVolumetric;
            target.volumetricDimmer = source.volumetricDimmer;
            target.volumetricShadowDimmer = source.volumetricShadowDimmer;
            EditorUtility.SetDirty(target);
        }

        private static bool ConfigureVehicleAdapter(GameObject root)
        {
            GameLightFixture[] fixtures =
                root.GetComponentsInChildren<GameLightFixture>(true);
            var vehicleFixtures = new List<GameLightFixture>();
            for (int index = 0; index < fixtures.Length; index++)
            {
                LightFixtureCategory category = fixtures[index].Profile.Category;
                if (category >= LightFixtureCategory.VehicleLowBeam &&
                    category <= LightFixtureCategory.VehicleInterior)
                {
                    vehicleFixtures.Add(fixtures[index]);
                }
            }

            if (vehicleFixtures.Count == 0)
            {
                return false;
            }

            MonoBehaviour vendorVehicle = null;
            MonoBehaviour simulationHost = null;
            MonoBehaviour[] behaviours =
                root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName ?? string.Empty;
                if (typeName == "NWH.VehiclePhysics2.VehicleController")
                {
                    vendorVehicle = behaviour;
                }
                else if (typeName == "MSC.Vehicle.VehicleSimulationHost")
                {
                    simulationHost = behaviour;
                }
            }

            if (vendorVehicle == null && simulationHost == null)
            {
                return false;
            }

            VehicleLightingElectricalAdapter adapter =
                root.GetComponent<VehicleLightingElectricalAdapter>() ??
                root.AddComponent<VehicleLightingElectricalAdapter>();
            adapter.ConfigureForAuthoring(
                vendorVehicle,
                simulationHost,
                vehicleFixtures.ToArray());
            EditorUtility.SetDirty(adapter);
            return true;
        }

        private static void CollectAuditRecords(
            GameObject root,
            string assetPath,
            string prefabGuid,
            List<LightingAuditRecord> records)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Candidate candidate = Analyze(transforms[index]);
                if (candidate == null)
                {
                    continue;
                }

                GameLightFixture fixture = candidate.Light != null
                    ? candidate.Light.GetComponent<GameLightFixture>()
                    : candidate.Transform.GetComponent<GameLightFixture>();
                string hierarchy = GetHierarchyPath(candidate.Transform);
                bool configured = fixture != null;
                bool manual = !configured;
                string reason = candidate.ReviewReason;
                if (manual && string.IsNullOrEmpty(reason))
                {
                    reason = candidate.Light == null
                        ? "Candidate has no authoritatively placed Light; transform/orientation requires visual review."
                        : "Existing Light was not auto-bound because classification confidence is below the safe threshold.";
                }

                records.Add(new LightingAuditRecord
                {
                    assetPath = assetPath,
                    scene = assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                        ? Path.GetFileNameWithoutExtension(assetPath)
                        : string.Empty,
                    prefabGuid = prefabGuid,
                    hierarchyPath = hierarchy,
                    worldPosition = candidate.Transform.position,
                    localPosition = candidate.Transform.localPosition,
                    inferredType = candidate.Category.ToString(),
                    existingLight = candidate.Light != null,
                    existingEmissiveRenderer = candidate.EmissiveRenderers.Length > 0,
                    existingSwitch = candidate.HasSwitch,
                    inferredCircuit = candidate.Circuit,
                    classification = candidate.Classification,
                    confidence = candidate.Confidence,
                    lightPlacementApplied = configured,
                    manualReviewRequired = manual,
                    manualReviewReason = reason,
                });
            }
        }

        private static Candidate Analyze(Transform transform)
        {
            Light directLight = transform.GetComponent<Light>();
            if (directLight != null &&
                IsGlobalDirectionalLight(transform, directLight))
            {
                return null;
            }

            Renderer directRenderer = transform.GetComponent<Renderer>();
            MeshFilter mesh = transform.GetComponent<MeshFilter>();
            int hintScore = CountHints(transform.name);
            if (mesh != null && mesh.sharedMesh != null)
            {
                hintScore += CountHints(mesh.sharedMesh.name);
            }

            string directSemantic = (transform.name + " " +
                (mesh != null && mesh.sharedMesh != null
                    ? mesh.sharedMesh.name
                    : string.Empty)).ToLowerInvariant();
            if (directLight == null && IsKnownNonFixtureSemantic(directSemantic))
            {
                return null;
            }

            var emissive = new List<Renderer>(2);
            if (directRenderer != null && IsEmissive(directRenderer))
            {
                emissive.Add(directRenderer);
                hintScore++;
            }

            if (directLight == null && hintScore == 0)
            {
                return null;
            }

            if (emissive.Count == 0)
            {
                Renderer[] children = transform.GetComponentsInChildren<Renderer>(true);
                int limit = Mathf.Min(children.Length, 8);
                for (int index = 0; index < limit; index++)
                {
                    if (IsEmissive(children[index]))
                    {
                        emissive.Add(children[index]);
                    }
                }
            }

            bool hasSwitch = HasHintInHierarchy(transform, SwitchHints);
            LightFixtureCategory category = InferCategory(transform, directLight);
            float confidence = directLight != null ? 0.78f : 0.25f;
            confidence += Mathf.Min(0.17f, hintScore * 0.06f);
            if (emissive.Count > 0)
            {
                confidence += 0.05f;
            }

            confidence = Mathf.Clamp01(confidence);
            return new Candidate
            {
                Transform = transform,
                Light = directLight,
                EmissiveRenderers = emissive.ToArray(),
                HasSwitch = hasSwitch,
                Category = category,
                Circuit = InferCircuit(category, transform),
                Confidence = confidence,
                Classification = directLight != null
                    ? "ExistingLight+SemanticAndMaterialEvidence"
                    : "SemanticAndMaterialCandidate",
                ReviewReason = directLight == null
                    ? "No existing Light component; automatic placement would guess physical origin and aiming."
                    : string.Empty,
            };
        }

        private static bool IsGlobalDirectionalLight(
            Transform transform,
            Light light)
        {
            if (light.type == LightType.Directional)
            {
                return true;
            }

            string context = GetHierarchyPath(transform).ToLowerInvariant();
            return ContainsAny(
                context,
                "directional sun",
                "directional light",
                "directionallight",
                "sun/moon directional light");
        }

        private static bool IsEmissive(Renderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null)
                {
                    continue;
                }

                if (CountHints(material.name) > 0 ||
                    material.IsKeywordEnabled("_EMISSION") ||
                    material.IsKeywordEnabled("_EMISSIVE_COLOR_MAP"))
                {
                    return true;
                }

                Shader shader = material.shader;
                if (shader != null &&
                    shader.FindPropertyIndex("_EmissiveColor") >= 0)
                {
                    Color color = material.GetColor(EmissiveColorId);
                    float intensity = shader.FindPropertyIndex(
                            "_EmissiveIntensity") >= 0
                        ? material.GetFloat(EmissiveIntensityId)
                        : color.maxColorComponent;
                    if (color.maxColorComponent > 0.001f &&
                        intensity > 0.001f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static LightFixtureCategory InferCategory(
            Transform transform,
            Light light)
        {
            string context = GetHierarchyPath(transform).ToLowerInvariant();
            if (ContainsAny(context, "highbeam", "high_beam", "longbeam")) return LightFixtureCategory.VehicleHighBeam;
            if (ContainsAny(context, "headlight", "lowbeam", "low_beam")) return LightFixtureCategory.VehicleLowBeam;
            if (ContainsAny(context, "brake")) return LightFixtureCategory.VehicleBrake;
            if (ContainsAny(context, "indicator", "blinker", "turnsignal")) return LightFixtureCategory.VehicleIndicator;
            if (ContainsAny(context, "reverse")) return LightFixtureCategory.VehicleReverse;
            if (ContainsAny(context, "licenseplate", "license_plate", "numberplate", "number_plate")) return LightFixtureCategory.VehicleLicensePlate;
            if (ContainsAny(context, "dashboard", "gauge")) return LightFixtureCategory.VehicleDashboard;
            if (ContainsAny(context, "interiorlight", "dome_light")) return LightFixtureCategory.VehicleInterior;
            if (ContainsAny(context, "tail", "rear_light")) return LightFixtureCategory.VehicleTail;
            if (ContainsAny(context, "fleetari", "repairshop", "workshop")) return LightFixtureCategory.FleetariWorkshop;
            if (ContainsAny(context, "teimo_pub", "teimo pub", "/pub/") ||
                context.EndsWith("/pub", StringComparison.Ordinal)) return LightFixtureCategory.TeimoPub;
            if (ContainsAny(context, "teimo", "/store/")) return LightFixtureCategory.TeimoShop;
            if (ContainsAny(context, "lightbar", "light_bar")) return LightFixtureCategory.Technical;
            if (ContainsAny(context, "street", "streetlamp", "roadlamp", "lamp_post")) return LightFixtureCategory.StreetLamp;
            if (ContainsAny(context, "fluorescent", "tube")) return LightFixtureCategory.Fluorescent;
            if (ContainsAny(context, "flashlight", "torch")) return LightFixtureCategory.PlayerFlashlight;
            if (ContainsAny(context, "exterior", "porch", "outside")) return LightFixtureCategory.ExteriorBuilding;
            if (ContainsAny(context, "ceiling", "plafond")) return LightFixtureCategory.EnclosedCeiling;
            if (light != null && light.type == LightType.Spot) return LightFixtureCategory.Spotlight;
            return LightFixtureCategory.DomesticIncandescent;
        }

        private static string InferCircuit(
            LightFixtureCategory category,
            Transform transform)
        {
            return category switch
            {
                LightFixtureCategory.StreetLamp => "grid.street.public",
                LightFixtureCategory.TeimoShop => "grid.teimo.shop",
                LightFixtureCategory.TeimoPub => "grid.teimo.pub",
                LightFixtureCategory.FleetariWorkshop => "grid.fleetari.workshop",
                LightFixtureCategory.VehicleLowBeam or
                LightFixtureCategory.VehicleHighBeam or
                LightFixtureCategory.VehicleTail or
                LightFixtureCategory.VehicleBrake or
                LightFixtureCategory.VehicleIndicator or
                LightFixtureCategory.VehicleReverse or
                LightFixtureCategory.VehicleLicensePlate or
                LightFixtureCategory.VehicleDashboard or
                LightFixtureCategory.VehicleInterior => "vehicle.electrical.lighting",
                _ => "grid.building." + StableHash(
                    transform.root.name.ToLowerInvariant()),
            };
        }

        private static string InferPowerSource(LightFixtureCategory category)
        {
            return category.ToString().StartsWith(
                    "Vehicle",
                    StringComparison.Ordinal)
                ? "source.vehicle.battery"
                : "source.grid.main";
        }

        private static string InferBusiness(LightFixtureCategory category)
        {
            return category switch
            {
                LightFixtureCategory.TeimoShop => "service.teimo.shop",
                LightFixtureCategory.TeimoPub => "service.teimo.pub",
                LightFixtureCategory.FleetariWorkshop => "service.fleetari.workshop",
                _ => string.Empty,
            };
        }

        private static string InferVehicleChannel(LightFixtureCategory category)
        {
            return category switch
            {
                LightFixtureCategory.VehicleLowBeam => "low-beam",
                LightFixtureCategory.VehicleHighBeam => "high-beam",
                LightFixtureCategory.VehicleTail => "tail",
                LightFixtureCategory.VehicleBrake => "brake",
                LightFixtureCategory.VehicleIndicator => "indicator",
                LightFixtureCategory.VehicleReverse => "reverse",
                LightFixtureCategory.VehicleLicensePlate => "license-plate",
                LightFixtureCategory.VehicleDashboard => "dashboard",
                LightFixtureCategory.VehicleInterior => "interior",
                _ => string.Empty,
            };
        }

        private static string InferZone(string assetPath, string hierarchy)
        {
            return "zone.auto." + StableHash(assetPath + "|" + hierarchy);
        }

        private static int CountHints(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            string lower = value.ToLowerInvariant();
            int count = 0;
            for (int index = 0; index < LightHints.Length; index++)
            {
                if (lower.Contains(LightHints[index]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsKnownNonFixtureSemantic(string value)
        {
            return ContainsAny(
                value,
                "lighttrunk",
                "light_trunk",
                "light trunk",
                "lightmap",
                "light_map",
                "lightprobe",
                "light_probe",
                "lightingdata",
                "lighting_data",
                "highlight",
                "billboard_birch",
                "lightningorigin",
                "lightningtarget",
                "lightingzone",
                "lighting_zone",
                "lightingswitch",
                "light_switch",
                "lightinggameplaybindings",
                "neutrallighting");
        }

        private static bool HasHintInHierarchy(
            Transform transform,
            string[] hints)
        {
            Transform current = transform;
            int depth = 0;
            while (current != null && depth < 4)
            {
                string lower = current.name.ToLowerInvariant();
                for (int index = 0; index < hints.Length; index++)
                {
                    if (lower.Contains(hints[index]))
                    {
                        return true;
                    }
                }

                current = current.parent;
                depth++;
            }

            return false;
        }

        private static string[] FindGameAssets(
            string filter,
            bool includeThirdParty)
        {
            string[] guids = AssetDatabase.FindAssets(filter, new[] { "Assets" });
            var paths = new List<string>(guids.Length);
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (!path.StartsWith("Assets/Game/", StringComparison.Ordinal) &&
                    !IsBuildScene(path))
                {
                    continue;
                }

                if (!includeThirdParty && IsExcludedAuditPath(path))
                {
                    continue;
                }

                paths.Add(path);
            }

            return paths.ToArray();
        }

        private static bool IsBuildScene(string path)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int index = 0; index < scenes.Length; index++)
            {
                if (string.Equals(
                        scenes[index].path,
                        path,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExcludedAuditPath(string path)
        {
            return path.Contains("/Tests/", StringComparison.Ordinal) ||
                path.Contains("/Test/", StringComparison.Ordinal) ||
                path.Contains("/Samples/", StringComparison.Ordinal) ||
                path.Contains("/ReferenceOnly/", StringComparison.Ordinal);
        }

        private static bool IsSafeProjectOwnedAsset(string path)
        {
            return path.StartsWith("Assets/Game/", StringComparison.Ordinal) &&
                !path.Contains("/LegacyImport/RuntimeBaseline/", StringComparison.Ordinal) &&
                !path.Contains("/Imported/DonorGenerated/", StringComparison.Ordinal) &&
                !path.Contains("/ThirdParty/", StringComparison.Ordinal) &&
                !path.Contains("/Tests/", StringComparison.Ordinal) &&
                !path.Contains("/PrototypeOnly/", StringComparison.Ordinal);
        }

        private static void WriteReports(
            LightingAuditDocument document,
            string suffix)
        {
            Directory.CreateDirectory(ReportRoot);
            string postfix = string.IsNullOrEmpty(suffix)
                ? string.Empty
                : "." + suffix;
            File.WriteAllText(
                ReportRoot + "/LightingAudit" + postfix + ".json",
                JsonUtility.ToJson(document, true),
                new UTF8Encoding(false));
            File.WriteAllText(
                ReportRoot + "/LightingAudit" + postfix + ".csv",
                ToCsv(document.records),
                new UTF8Encoding(false));
            if (string.IsNullOrEmpty(suffix))
            {
                File.WriteAllText(
                    ReportRoot + "/LightingManualReview.md",
                    ToManualReview(document),
                    new UTF8Encoding(false));
            }
        }

        private static string ToCsv(List<LightingAuditRecord> records)
        {
            var output = new StringBuilder(records.Count * 256);
            output.AppendLine("AssetPath,Scene,PrefabGuid,HierarchyPath,WorldPosition,LocalPosition,InferredType,ExistingLight,ExistingEmissiveRenderer,ExistingSwitch,InferredCircuit,Classification,Confidence,LightPlacementApplied,ManualReviewRequired,ManualReviewReason");
            for (int index = 0; index < records.Count; index++)
            {
                LightingAuditRecord record = records[index];
                AppendCsv(output, record.assetPath);
                AppendCsv(output, record.scene);
                AppendCsv(output, record.prefabGuid);
                AppendCsv(output, record.hierarchyPath);
                AppendCsv(output, FormatVector(record.worldPosition));
                AppendCsv(output, FormatVector(record.localPosition));
                AppendCsv(output, record.inferredType);
                AppendCsv(output, record.existingLight.ToString());
                AppendCsv(output, record.existingEmissiveRenderer.ToString());
                AppendCsv(output, record.existingSwitch.ToString());
                AppendCsv(output, record.inferredCircuit);
                AppendCsv(output, record.classification);
                AppendCsv(output, record.confidence.ToString("0.00", CultureInfo.InvariantCulture));
                AppendCsv(output, record.lightPlacementApplied.ToString());
                AppendCsv(output, record.manualReviewRequired.ToString());
                AppendCsv(output, record.manualReviewReason, true);
            }

            return output.ToString();
        }

        private static string ToManualReview(LightingAuditDocument document)
        {
            var output = new StringBuilder();
            output.AppendLine("# Lighting Manual Review");
            output.AppendLine();
            output.AppendLine($"Generated UTC: `{document.generatedUtc}`  ");
            output.AppendLine($"Candidates: **{document.candidates}**; configured fixtures: **{document.configuredFixtures}**; manual review: **{document.manualReview}**.");
            output.AppendLine();
            output.AppendLine("These rows were deliberately not auto-mutated because physical light origin, orientation, donor provenance, or semantic classification is ambiguous. This is a safety queue, not a claim that the fixture is complete.");
            output.AppendLine();
            output.AppendLine("| Asset | Hierarchy | Type | Confidence | Reason |");
            output.AppendLine("|---|---|---:|---:|---|");
            for (int index = 0; index < document.records.Count; index++)
            {
                LightingAuditRecord record = document.records[index];
                if (!record.manualReviewRequired)
                {
                    continue;
                }

                output.Append('|').Append(EscapeMarkdown(record.assetPath))
                    .Append('|').Append(EscapeMarkdown(record.hierarchyPath))
                    .Append('|').Append(record.inferredType)
                    .Append('|').Append(record.confidence.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append('|').Append(EscapeMarkdown(record.manualReviewReason))
                    .AppendLine("|");
            }

            return output.ToString();
        }

        private static void AppendCsv(
            StringBuilder output,
            string value,
            bool final = false)
        {
            string safe = (value ?? string.Empty).Replace("\"", "\"\"");
            output.Append('"').Append(safe).Append('"');
            output.Append(final ? '\n' : ',');
        }

        private static string FormatVector(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.###};{1:0.###};{2:0.###}",
                value.x,
                value.y,
                value.z);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var segments = new List<string>(12);
            Transform current = transform;
            while (current != null)
            {
                segments.Add(current.name);
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619;
                }

                return hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            for (int index = 0; index < tokens.Length; index++)
            {
                if (value.Contains(tokens[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string EscapeMarkdown(string value)
        {
            return (value ?? string.Empty)
                .Replace("|", "\\|")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static LightingAuditRecord CreateFailureRecord(
            string assetPath,
            string reason)
        {
            return new LightingAuditRecord
            {
                assetPath = assetPath,
                classification = "AuditFailure",
                manualReviewRequired = true,
                manualReviewReason = reason,
            };
        }
    }
}
