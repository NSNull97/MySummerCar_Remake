using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MSC.Characters;
using MSC.LegacyImport.Editor.Configuration;
using MSC.Traffic;
using MSC.Vehicle.NWH;
using MSC.Vehicle.Simulation;
using NWH.WheelController3D;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using InCarRagdollShape =
    MSC.Characters.StoryTrafficInCarRagdollBinding.ColliderShape;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Builds removable, script-free Phase 1 wrappers for the two authored
    /// story-traffic cars. Donor vehicle controllers, PlayMaker FSMs, shaders,
    /// rigidbodies and save authority never cross this boundary.
    /// </summary>
    public static partial class Phase1StoryTrafficPresentationImporter
    {
        private const string ConfigurationPath =
            "Config/DonorPaths.local.json";
        private const string ManifestPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1StoryTrafficPresentationManifest.json";
        private const string CharacterRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Characters";
        private const string OutputRoot = CharacterRoot + "/StoryTraffic";
        private const string SourceRoot = OutputRoot + "/Source";
        private const string GeneratedRoot = OutputRoot + "/Generated";
        private const string MaterialRoot = GeneratedRoot + "/Materials";
        private const string ClipRoot = GeneratedRoot + "/Clips";
        private const string PrefabRoot = GeneratedRoot + "/Prefabs";
        private const string SimulationRoot = GeneratedRoot + "/Simulation";
        private const string SuskiRescueBindingId =
            "presentation.character.suski-better-rescue";
        private const string SuskiDefaultBindingId =
            "presentation.character.suski";
        private const string CatalogPath = CharacterRoot +
            "/Resources/Phase1Characters/CharacterPresentationCatalog.asset";
        private const string BuildReportPath = OutputRoot +
            "/Phase1StoryTrafficPresentationBuildReport.json";

        [MenuItem(
            "Tools/My Summer Car/Legacy Import/" +
            "Build Phase 1 Story Traffic Presentation")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Phase 1 story traffic",
                "Jani/Suski and Petteri story-traffic wrappers were rebuilt.",
                "OK");
        }

        public static void BuildFromBatch() => Build();

        public static void Build()
        {
            // The bus wrapper embeds the explicitly catalogued Latanen walker.
            // Validate that baseline before moving any story-traffic output so
            // a standalone menu/batch invocation remains transactional.
            Phase1CharacterPresentationImporter.EnsureGeneratedForBuild();
            string outputPath = ToFileSystemPath(OutputRoot);
            string catalogPath = ToFileSystemPath(CatalogPath);
            string rollbackRoot = Path.Combine(
                Path.GetFullPath(Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Library")),
                "LegacyImportRollback",
                "Phase1StoryTraffic-" + Guid.NewGuid().ToString("N"));
            string rollbackOutputPath = Path.Combine(
                rollbackRoot,
                "StoryTraffic");
            string rollbackCatalogPath = Path.Combine(
                rollbackRoot,
                "CharacterPresentationCatalog.asset");
            bool hadOutput = Directory.Exists(outputPath);
            bool hadCatalog = File.Exists(catalogPath);
            Directory.CreateDirectory(rollbackRoot);
            if (hadOutput)
            {
                Directory.Move(outputPath, rollbackOutputPath);
            }

            if (hadCatalog)
            {
                File.Copy(catalogPath, rollbackCatalogPath, overwrite: true);
            }

            try
            {
                BuildTransactionalContent();
                Directory.Delete(rollbackRoot, recursive: true);
            }
            catch
            {
                if (Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, recursive: true);
                }

                if (hadOutput && Directory.Exists(rollbackOutputPath))
                {
                    Directory.Move(rollbackOutputPath, outputPath);
                }

                if (hadCatalog && File.Exists(rollbackCatalogPath))
                {
                    File.Copy(
                        rollbackCatalogPath,
                        catalogPath,
                        overwrite: true);
                }

                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                if (Directory.Exists(rollbackRoot))
                {
                    Directory.Delete(rollbackRoot, recursive: true);
                }

                Debug.LogError(
                    "Phase 1 story-traffic import failed; the previous generated output and presentation catalog were restored.");
                throw;
            }
        }

        private static void BuildTransactionalContent()
        {
            Manifest manifest = LoadManifest();
            DonorPathConfiguration paths =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string donorAssetsRoot = Path.Combine(
                paths.DonorStagingDirectory,
                manifest.source.stagingRootRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string scenePath = Path.Combine(
                donorAssetsRoot,
                manifest.source.sceneRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            RequireHash(scenePath, manifest.source.sceneSha256, "donor scene");

            BetterMscSuskiSpec betterSpec = manifest.betterMscSuski;
            string betterAssetsRoot = Path.Combine(
                paths.DonorStagingDirectory,
                betterSpec.stagingRootRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string betterPrefabPath = Path.Combine(
                betterAssetsRoot,
                betterSpec.prefabRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string betterAvatarPath = Path.Combine(
                betterAssetsRoot,
                betterSpec.avatarRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string betterSitClipPath = Path.Combine(
                betterAssetsRoot,
                betterSpec.vehicleSitClipRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string betterStandClipPath = Path.Combine(
                betterAssetsRoot,
                betterSpec.standClipRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            RequireHash(
                betterPrefabPath,
                betterSpec.prefabSha256,
                "BetterMSC Suski prefab");
            RequireHash(
                betterAvatarPath,
                betterSpec.avatarSha256,
                "BetterMSC Suski avatar");
            RequireHash(
                betterSitClipPath,
                betterSpec.vehicleSitClipSha256,
                "BetterMSC Suski vehicle-sit clip");
            RequireHash(
                betterStandClipPath,
                betterSpec.standClipSha256,
                "BetterMSC Suski standing clip");

            ResetOutput();
            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(scenePath);
            DonorUnitySceneModel betterSuski =
                DonorUnitySceneModel.Parse(betterPrefabPath);
            DonorActionSlice betterSuskiSlice =
                betterSuski.CreatePresentationSlice(
                    betterSpec.rootTransformFileId,
                    betterSpec.rootTransformFileId,
                    new[] { betterSpec.skinnedRendererComponentFileId },
                    Array.Empty<long>());
            var fixtureSources = manifest.vehicles.Select(spec =>
            {
                DonorStaticRendererRecord[] selectedStaticRenderers = scene
                    .GetActiveStaticRenderersBelow(
                        spec.staticRendererRootTransformFileIds)
                    .ToArray();
                long[] excludedStaticRendererIds =
                    spec.excludedStaticRendererComponentFileIds ??
                    Array.Empty<long>();
                long[] missingExclusions = excludedStaticRendererIds
                    .Where(id => selectedStaticRenderers.All(renderer =>
                        renderer.ComponentId != id))
                    .ToArray();
                if (missingExclusions.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Story-traffic fixture '{spec.id}' could not find excluded static renderer IDs: " +
                        string.Join(", ", missingExclusions));
                }

                DonorStaticRendererRecord[] staticRenderers =
                    selectedStaticRenderers
                        .Where(renderer => !excludedStaticRendererIds.Contains(
                            renderer.ComponentId))
                        .ToArray();
                DonorStaticRendererRecord[] passengerReferenceRenderers =
                    selectedStaticRenderers
                        .Where(renderer => excludedStaticRendererIds.Contains(
                            renderer.ComponentId))
                        .ToArray();
                DonorSkinnedRendererRecord[] skinnedRenderers =
                    spec.skinnedRendererComponentFileIds
                        .Select(scene.GetSkinnedRenderer)
                        .ToArray();
                if (staticRenderers.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Story-traffic fixture '{spec.id}' selected no static vehicle renderers.");
                }

                return new FixtureSource(
                    spec,
                    skinnedRenderers,
                    staticRenderers,
                    passengerReferenceRenderers);
            }).ToArray();
            AmbientFixtureSource[] ambientFixtureSources =
                BuildAmbientFixtureSources(scene, manifest.ambientVehicles);
            TransportFixtureSource[] transportFixtureSources =
                BuildTransportFixtureSources(scene, manifest.transports);

            Dictionary<string, string> sourceByGuid = BuildGuidIndex(
                donorAssetsRoot);
            MergeGuidIndex(sourceByGuid, BuildGuidIndex(betterAssetsRoot));
            string[] meshGuids = fixtureSources
                .SelectMany(source => source.SkinnedRenderers
                    .Select(renderer => renderer.MeshGuid)
                    .Concat(source.StaticRenderers.Select(renderer =>
                        renderer.MeshGuid))
                    .Concat(source.PassengerReferenceRenderers.Select(
                        renderer => renderer.MeshGuid)))
                .Concat(betterSuskiSlice.Renderers.Select(renderer =>
                    renderer.MeshGuid))
                .Concat(ambientFixtureSources.SelectMany(source =>
                    source.SkinnedRenderers
                        .Select(renderer => renderer.MeshGuid)
                        .Concat(source.StaticRenderers.Select(renderer =>
                            renderer.MeshGuid))))
                .Concat(transportFixtureSources.SelectMany(source =>
                    source.SkinnedRenderers
                        .Select(renderer => renderer.MeshGuid)
                        .Concat(source.StaticRenderers.Select(renderer =>
                            renderer.MeshGuid))))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] materialGuids = fixtureSources
                .SelectMany(source => source.SkinnedRenderers
                    .SelectMany(renderer => renderer.MaterialGuids)
                    .Concat(source.StaticRenderers.SelectMany(renderer =>
                        renderer.MaterialGuids)))
                .Concat(betterSuskiSlice.Renderers.SelectMany(renderer =>
                    renderer.MaterialGuids))
                .Concat(ambientFixtureSources.SelectMany(source =>
                    source.SkinnedRenderers
                        .SelectMany(renderer => renderer.MaterialGuids)
                        .Concat(source.StaticRenderers.SelectMany(renderer =>
                            renderer.MaterialGuids))))
                .Concat(transportFixtureSources.SelectMany(source =>
                    source.SkinnedRenderers
                        .SelectMany(renderer => renderer.MaterialGuids)
                        .Concat(source.StaticRenderers.SelectMany(renderer =>
                            renderer.MaterialGuids))))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            var materialSpecs = materialGuids.ToDictionary(
                guid => guid,
                guid => ReadMaterialSpec(
                    RequireGuidPath(sourceByGuid, guid, "material")),
                StringComparer.OrdinalIgnoreCase);
            string[] textureGuids = materialSpecs.Values
                .Select(spec => spec.MainTextureGuid)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            var importedMeshes = ImportAssets(
                meshGuids,
                sourceByGuid,
                SourceRoot + "/Mesh");
            var importedTextures = ImportAssets(
                textureGuids,
                sourceByGuid,
                SourceRoot + "/Texture");
            string importedBetterSitClip = ImportLockedAsset(
                betterSitClipPath,
                SourceRoot + "/BetterMSC/Suski_car_sit.anim",
                "better-msc-suski-car-sit");
            string importedBetterStandClip = ImportLockedAsset(
                betterStandClipPath,
                SourceRoot + "/BetterMSC/Suski_stand.anim",
                "better-msc-suski-stand");
            string importedBetterAvatar = ImportLockedAsset(
                betterAvatarPath,
                SourceRoot + "/BetterMSC/SuskiAvatar.asset",
                "better-msc-suski-avatar");
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            foreach (string path in importedTextures.Values)
            {
                ConfigureTexture(path);
            }

            EnsureAssetFolder(MaterialRoot);
            EnsureAssetFolder(ClipRoot);
            EnsureAssetFolder(PrefabRoot);
            EnsureAssetFolder(SimulationRoot);
            EnsureAmbientOutputFolders();
            EnsureTransportOutputFolders();
            var materials = materialSpecs.ToDictionary(
                pair => pair.Key,
                pair => CreateMaterial(
                    pair.Key,
                    pair.Value,
                    importedTextures),
                StringComparer.OrdinalIgnoreCase);
            AnimationClip betterSitClip = RequireAsset<AnimationClip>(
                importedBetterSitClip);
            AnimationClip betterStandClip = RequireAsset<AnimationClip>(
                importedBetterStandClip);
            Avatar betterAvatar = RequireAsset<Avatar>(importedBetterAvatar);
            if (!betterAvatar.isValid || !betterAvatar.isHuman)
            {
                throw new InvalidOperationException(
                    "Locked BetterMSC Suski avatar is not a valid humanoid Avatar.");
            }

            var entries = new List<CharacterPresentationCatalogEntry>();
            foreach (FixtureSource source in fixtureSources)
            {
                entries.Add(new CharacterPresentationCatalogEntry(
                    source.Spec.bindingId,
                    source.Spec.productionReplacementKey,
                    BuildFixture(
                        scene,
                        source,
                        importedMeshes,
                        materials,
                        betterSuski,
                        betterSuskiSlice,
                        betterSitClip,
                        betterAvatar)));
            }

            CharacterPresentationCatalogEntry janiEntry = entries.Single(
                entry => string.Equals(
                    entry.BindingId,
                    "presentation.character.jani-car",
                    StringComparison.Ordinal));
            entries.Add(new CharacterPresentationCatalogEntry(
                SuskiRescueBindingId,
                SuskiDefaultBindingId,
                BuildBetterMscSuskiRescueFixture(janiEntry.WrapperPrefab)));
            entries.Add(new CharacterPresentationCatalogEntry(
                SuskiDefaultBindingId,
                SuskiDefaultBindingId,
                BuildBetterMscSuskiStandingFixture(
                    betterSuski,
                    betterSuskiSlice,
                    importedMeshes,
                    materials,
                    betterStandClip,
                    betterAvatar)));

            MergeCatalog(entries);
            BuildAmbientPresentationCatalog(
                scene,
                ambientFixtureSources,
                transportFixtureSources,
                importedMeshes,
                materials);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            WriteReport(
                manifest,
                scenePath,
                fixtureSources,
                ambientFixtureSources,
                transportFixtureSources,
                meshGuids,
                materialGuids,
                textureGuids,
                betterPrefabPath,
                betterAvatarPath,
                betterSitClipPath,
                betterStandClipPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureGeneratedForBuild();
            Debug.Log(
                $"Phase 1 story traffic presentation complete: " +
                $"{fixtureSources.Length} cars, " +
                $"{ambientFixtureSources.Length} ambient traffic archetypes, " +
                $"{transportFixtureSources.Length} public transport archetypes, " +
                $"{fixtureSources.Sum(value => value.SkinnedRenderers.Length) + 1} crew bodies (Suski from BetterMSC) and " +
                $"{fixtureSources.Sum(value => value.StaticRenderers.Length)} static vehicle renderers.");
        }

        public static void EnsureGeneratedForBuild()
        {
            Manifest manifest = LoadManifest();
            string reportPath = ToFileSystemPath(BuildReportPath);
            if (!File.Exists(reportPath))
            {
                throw new InvalidOperationException(
                    "Generated story-traffic presentation report is missing.");
            }

            BuildReportData report = JsonUtility.FromJson<BuildReportData>(
                File.ReadAllText(reportPath));
            if (report == null ||
                report.schemaVersion != manifest.schemaVersion ||
                !string.Equals(
                    report.sourceManifestSha256,
                    ComputeHash(ToFileSystemPath(ManifestPath)),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Generated story-traffic presentation is stale.");
            }

            CharacterPresentationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                    CatalogPath) ?? throw new InvalidOperationException(
                    "Character presentation catalog is missing.");
            foreach (VehicleSpec spec in manifest.vehicles)
            {
                if (!catalog.TryGet(spec.bindingId, out var entry) ||
                    entry.WrapperPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Story-traffic binding '{spec.bindingId}' is missing.");
                }

                LegacyCharacterPresentationBinding character =
                    entry.WrapperPrefab.GetComponent<
                        LegacyCharacterPresentationBinding>();
                StoryTrafficVehiclePresentationBinding vehicle =
                    entry.WrapperPrefab.GetComponent<
                        StoryTrafficVehiclePresentationBinding>();
                StoryTrafficInCarRagdollBinding crashRagdoll =
                    entry.WrapperPrefab.GetComponent<
                        StoryTrafficInCarRagdollBinding>();
                string failure = string.Empty;
                if (character == null ||
                    !character.TryValidate(out failure) ||
                    vehicle == null ||
                    !vehicle.TryValidate(out failure) ||
                    crashRagdoll == null ||
                    !crashRagdoll.TryValidate(out failure) ||
                    crashRagdoll.OccupantCount !=
                        1 + (spec.passengerFeatureIds?.Length ?? 0) ||
                    entry.WrapperPrefab.GetComponent<Rigidbody>() == null ||
                    entry.WrapperPrefab.GetComponent<BoxCollider>() == null ||
                    entry.WrapperPrefab.GetComponent<
                        NwhWheelPhysicsBackend>() == null ||
                    entry.WrapperPrefab.GetComponent<
                        NwhStoryTrafficVehicleMotionBackend>() == null ||
                    entry.WrapperPrefab.GetComponentsInChildren<
                        WheelController>(true).Length != 4 ||
                    entry.WrapperPrefab
                        .GetComponentsInChildren<Animator>(true).Length != 0 ||
                    entry.WrapperPrefab
                        .GetComponentsInChildren<MonoBehaviour>(true)
                        .Any(component =>
                            component != character &&
                            component != vehicle &&
                            component is not
                                StoryTrafficInCarRagdollBinding &&
                            component is not NwhWheelPhysicsBackend &&
                            component is not
                                NwhStoryTrafficVehicleMotionBackend &&
                            component is not WheelController))
                {
                    throw new InvalidOperationException(
                        $"Story-traffic fixture '{spec.id}' is invalid: {failure}");
                }

                if (spec.betterMscPassengerAnchorTransformFileId > 0L)
                {
                    Transform[] passengerAnchors = entry.WrapperPrefab
                        .GetComponentsInChildren<Transform>(true)
                        .Where(transform => string.Equals(
                            transform.name,
                            "Passenger",
                            StringComparison.Ordinal))
                        .ToArray();
                    if (passengerAnchors.Length != 1 ||
                        passengerAnchors[0].childCount != 1 ||
                        passengerAnchors[0]
                            .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .Length != 1 ||
                        passengerAnchors[0]
                            .GetComponentsInChildren<MeshRenderer>(true)
                            .Length != 0)
                    {
                        throw new InvalidOperationException(
                            $"Story-traffic fixture '{spec.id}' must contain exactly one BetterMSC passenger and no surviving original Suski renderers.");
                    }
                }
            }

            string suskiFailure = string.Empty;
            if (!catalog.TryGet(
                    SuskiRescueBindingId,
                    out CharacterPresentationCatalogEntry suskiRescueEntry) ||
                suskiRescueEntry.WrapperPrefab == null ||
                suskiRescueEntry.WrapperPrefab.GetComponent<
                    LegacyCharacterPresentationBinding>() is not
                    LegacyCharacterPresentationBinding suskiRescueBinding ||
                !suskiRescueBinding.TryValidate(out suskiFailure) ||
                suskiRescueEntry.WrapperPrefab.GetComponentsInChildren<
                    SkinnedMeshRenderer>(true).Length != 1 ||
                suskiRescueEntry.WrapperPrefab.GetComponentsInChildren<
                    MeshRenderer>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    "Standalone BetterMSC Suski rescue presentation is " +
                    "missing or invalid: " + suskiFailure);
            }

            string defaultSuskiFailure = string.Empty;
            if (!catalog.TryGet(
                    SuskiDefaultBindingId,
                    out CharacterPresentationCatalogEntry defaultSuskiEntry) ||
                defaultSuskiEntry.WrapperPrefab == null ||
                defaultSuskiEntry.WrapperPrefab.GetComponent<
                    LegacyCharacterPresentationBinding>() is not
                    LegacyCharacterPresentationBinding defaultSuskiBinding ||
                !defaultSuskiBinding.TryValidate(out defaultSuskiFailure) ||
                defaultSuskiEntry.WrapperPrefab.GetComponentsInChildren<
                    SkinnedMeshRenderer>(true).Length != 1 ||
                defaultSuskiEntry.WrapperPrefab.GetComponentsInChildren<
                    MeshRenderer>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    "Default BetterMSC Suski presentation is missing or " +
                    "invalid: " + defaultSuskiFailure);
            }

            EnsureAmbientGeneratedForBuild(manifest);
        }

        private static GameObject BuildBetterMscSuskiRescueFixture(
            GameObject janiPrefab)
        {
            if (janiPrefab == null)
            {
                throw new ArgumentNullException(nameof(janiPrefab));
            }

            Transform passengerAnchor = janiPrefab
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => string.Equals(
                    transform.name,
                    "Passenger",
                    StringComparison.Ordinal));
            if (passengerAnchor.childCount != 1)
            {
                throw new InvalidOperationException(
                    "Jani fixture does not expose exactly one accepted " +
                    "BetterMSC Suski passenger root.");
            }

            var root = new GameObject("Phase 1 BetterMSC Suski Rescue");
            try
            {
                GameObject acceptedModel = UnityEngine.Object.Instantiate(
                    passengerAnchor.GetChild(0).gameObject,
                    root.transform,
                    worldPositionStays: false);
                acceptedModel.name = "BetterMSC Suski Rescue Model";
                Animation animation = acceptedModel.GetComponent<Animation>();
                AnimationClip poseClip = animation != null
                    ? animation.Cast<AnimationState>()
                        .Select(state => state.clip)
                        .SingleOrDefault()
                    : null;
                if (animation == null || poseClip == null ||
                    !poseClip.legacy ||
                    acceptedModel.GetComponentsInChildren<
                        SkinnedMeshRenderer>(true).Length != 1)
                {
                    throw new InvalidOperationException(
                        "Accepted BetterMSC Suski passenger has no reusable " +
                        "sanitized seated pose for the rescue ragdoll.");
                }

                var character = root.AddComponent<
                    LegacyCharacterPresentationBinding>();
                character.ConfigureForAuthoring(
                    SuskiRescueBindingId,
                    "presentation.character.suski",
                    "presentation.character.suski.geometry",
                    "presentation.character.suski.materials",
                    "presentation.character.suski.animation",
                    animation,
                    new[]
                    {
                        new CharacterAnimationBinding(
                            CharacterActivityState.Idle,
                            poseClip,
                            shouldLoop: false),
                    });

                string prefabPath = PrefabRoot +
                    "/suski-better-rescue.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath) ?? throw new InvalidOperationException(
                    "Could not save BetterMSC Suski rescue prefab.");
                LegacyCharacterPresentationBinding saved = prefab.GetComponent<
                    LegacyCharacterPresentationBinding>();
                string failure = string.Empty;
                if (saved == null || !saved.TryValidate(out failure))
                {
                    throw new InvalidOperationException(
                        "Saved BetterMSC Suski rescue prefab is invalid: " +
                        failure);
                }

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildBetterMscSuskiStandingFixture(
            DonorUnitySceneModel betterSuski,
            DonorActionSlice betterSuskiSlice,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials,
            AnimationClip betterSuskiStandClip,
            Avatar betterSuskiAvatar)
        {
            var root = new GameObject("Phase 1 BetterMSC Suski");
            try
            {
                Transform model = BuildBetterMscPassenger(
                    betterSuski,
                    betterSuskiSlice,
                    root.transform,
                    importedMeshes,
                    materials);
                model.name = "BetterMSC Suski Model";
                AnimationClip poseClip = BakeBetterMscStandalonePoseClip(
                    "suski-better-standing",
                    model,
                    betterSuskiStandClip,
                    betterSuskiAvatar);
                Animation animation = model.gameObject.AddComponent<Animation>();
                animation.playAutomatically = false;
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                var character = root.AddComponent<
                    LegacyCharacterPresentationBinding>();
                character.ConfigureForAuthoring(
                    SuskiDefaultBindingId,
                    SuskiDefaultBindingId,
                    SuskiDefaultBindingId + ".geometry",
                    SuskiDefaultBindingId + ".materials",
                    SuskiDefaultBindingId + ".animation",
                    animation,
                    new[]
                    {
                        new CharacterAnimationBinding(
                            CharacterActivityState.Idle,
                            poseClip,
                            shouldLoop: true),
                    });

                string prefabPath = PrefabRoot + "/suski-better.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath) ?? throw new InvalidOperationException(
                    "Could not save default BetterMSC Suski prefab.");
                LegacyCharacterPresentationBinding saved = prefab.GetComponent<
                    LegacyCharacterPresentationBinding>();
                string failure = string.Empty;
                if (saved == null || !saved.TryValidate(out failure))
                {
                    throw new InvalidOperationException(
                        "Saved default BetterMSC Suski prefab is invalid: " +
                        failure);
                }

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildFixture(
            DonorUnitySceneModel scene,
            FixtureSource source,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials,
            DonorUnitySceneModel betterSuski,
            DonorActionSlice betterSuskiSlice,
            AnimationClip betterSuskiVehicleSitClip,
            Avatar betterSuskiAvatar)
        {
            VehicleSpec spec = source.Spec;
            DonorActionSlice slice = scene.CreatePresentationSlice(
                spec.rootTransformFileId,
                spec.animationTargetTransformFileId,
                spec.skinnedRendererComponentFileIds,
                source.StaticRenderers
                    .Concat(source.PassengerReferenceRenderers)
                    .Select(renderer => renderer.ComponentId)
                    .ToArray(),
                RequiredFixtureTransformIds(spec));
            var root = new GameObject("Phase 1 Story Traffic " + spec.id);
            try
            {
                var created = new Dictionary<long, Transform>();
                foreach (DonorTransformRecord donor in slice.Transforms)
                {
                    Transform parent = created.TryGetValue(
                        donor.FatherTransformId,
                        out Transform createdParent)
                            ? createdParent
                            : root.transform;
                    var node = new GameObject(
                        scene.GetGameObjectName(donor.GameObjectId));
                    node.transform.SetParent(parent, false);
                    bool isFixtureRoot =
                        donor.TransformId == spec.rootTransformFileId;
                    node.transform.localPosition = isFixtureRoot
                        ? Vector3.zero
                        : donor.LocalPosition;
                    node.transform.localRotation = isFixtureRoot
                        ? Quaternion.identity
                        : donor.LocalRotation;
                    node.transform.localScale = donor.LocalScale;
                    created.Add(donor.TransformId, node.transform);
                }

                foreach (DonorSkinnedRendererRecord donor in slice.Renderers)
                {
                    DonorTransformRecord ownerRecord = slice.Transforms.Single(
                        candidate =>
                            candidate.GameObjectId == donor.GameObjectId);
                    GameObject owner =
                        created[ownerRecord.TransformId].gameObject;
                    Mesh mesh = RequireAsset<Mesh>(
                        importedMeshes[donor.MeshGuid]);
                    var renderer = owner.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = mesh;
                    renderer.sharedMaterials = donor.MaterialGuids
                        .Select(guid => materials[guid])
                        .ToArray();
                    if (renderer.sharedMaterials.Length != mesh.subMeshCount)
                    {
                        throw new InvalidOperationException(
                            $"Story-traffic skinned renderer {donor.ComponentId} has a material slot mismatch.");
                    }

                    renderer.rootBone = donor.RootBoneTransformId > 0L
                        ? RequireTransform(created, donor.RootBoneTransformId)
                        : null;
                    renderer.bones = donor.BoneTransformIds
                        .Where(id => id > 0L)
                        .Select(id => RequireTransform(created, id))
                        .ToArray();
                    renderer.localBounds = donor.LocalBounds;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                foreach (DonorStaticRendererRecord donor in
                         slice.StaticRenderers)
                {
                    DonorTransformRecord ownerRecord = slice.Transforms.Single(
                        candidate =>
                            candidate.GameObjectId == donor.GameObjectId);
                    GameObject owner =
                        created[ownerRecord.TransformId].gameObject;
                    Mesh mesh = RequireAsset<Mesh>(
                        importedMeshes[donor.MeshGuid]);
                    var filter = owner.AddComponent<MeshFilter>();
                    filter.sharedMesh = mesh;
                    if (source.PassengerReferenceRenderers.Any(reference =>
                            reference.ComponentId == donor.ComponentId))
                    {
                        continue;
                    }

                    var renderer = owner.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = donor.MaterialGuids
                        .Select(guid => materials[guid])
                        .ToArray();
                    if (renderer.sharedMaterials.Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"Story-traffic static renderer {donor.ComponentId} has no material slots.");
                    }

                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                Animation animation;
                AnimationClip authoredPose;
                Transform betterMscPassengerRoot = null;
                if (spec.betterMscPassengerAnchorTransformFileId > 0L)
                {
                    Transform passengerAnchor = RequireTransform(
                        created,
                        spec.betterMscPassengerAnchorTransformFileId);
                    DonorPassengerPoseReference donorPassengerPose =
                        CaptureDonorPassengerPoseReference(
                            created,
                            passengerAnchor,
                            spec);
                    RemoveLegacyPassengerChildren(passengerAnchor);
                    betterMscPassengerRoot = BuildBetterMscPassenger(
                        betterSuski,
                        betterSuskiSlice,
                        passengerAnchor,
                        importedMeshes,
                        materials);
                    authoredPose = BakeBetterMscPoseClip(
                        spec.id,
                        betterMscPassengerRoot,
                        betterSuskiVehicleSitClip,
                        betterSuskiAvatar,
                        passengerAnchor,
                        donorPassengerPose);
                    animation = betterMscPassengerRoot.gameObject.AddComponent<
                        Animation>();
                }
                else
                {
                    authoredPose = CreateAuthoredPoseClip(spec.id);
                    animation = root.AddComponent<Animation>();
                }

                animation.playAutomatically = false;
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                var character = root.AddComponent<
                    LegacyCharacterPresentationBinding>();
                character.ConfigureForAuthoring(
                    spec.bindingId,
                    spec.productionReplacementKey,
                    spec.productionReplacementKey + ".geometry",
                    spec.productionReplacementKey + ".materials",
                    spec.productionReplacementKey + ".animation",
                    animation,
                    new[]
                    {
                        new CharacterAnimationBinding(
                            CharacterActivityState.VehicleSeated,
                            authoredPose,
                            shouldLoop: true),
                    });

                var vehicle = root.AddComponent<
                    StoryTrafficVehiclePresentationBinding>();
                Transform fixtureRoot = RequireTransform(
                    created,
                    spec.rootTransformFileId);
                Transform[] donorWheelRoots = spec.wheelTransformFileIds
                    .Select(id => RequireTransform(created, id))
                    .ToArray();
                Transform[] wheelTransforms = CreateWheelSpinPivots(
                    donorWheelRoots);
                float groundContactCalibration = CalibrateWheelContact(
                    root.transform,
                    fixtureRoot,
                    wheelTransforms,
                    spec.maximumGroundCorrectionMeters);
                vehicle.ConfigureForAuthoring(
                    spec.driverFeatureId,
                    spec.passengerFeatureIds,
                    betterMscPassengerRoot != null
                        ? new[] { betterMscPassengerRoot.gameObject }
                        : Array.Empty<GameObject>(),
                    wheelTransforms,
                    spec.wheelDegreesPerMeter,
                    groundContactCalibration);
                vehicle.ConfigureDrivingProfile(
                    configuredMinimumCruiseSpeedMetersPerSecond: 115f / 3.6f,
                    configuredMaximumSpeedMetersPerSecond: 185f / 3.6f,
                    configuredAccelerationMetersPerSecond2: 7.5f,
                    configuredBrakingMetersPerSecond2: 20f,
                    configuredTurnRateDegreesPerSecond: 150f,
                    configuredPassingLaneOffsetMeters: -3.2f);
                vehicle.ConfigureHooliganBehavior(
                    configuredLaneWanderAmplitudeMeters: 1.05f,
                    configuredMinimumDriftSpeedMetersPerSecond: 16.5f,
                    configuredDriftEntryAngleDegrees: 22f,
                    configuredDriftExitAngleDegrees: 8f,
                    configuredMaximumDriftSlipDegrees: 24f);
                ConfigureDynamicVehicle(
                    root,
                    spec,
                    wheelTransforms,
                    vehicle);
                ConfigureInCarCrashRagdolls(
                    root,
                    spec,
                    betterMscPassengerRoot,
                    animation,
                    vehicle);

                string prefabPath = PrefabRoot + "/" + spec.id + ".prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath) ?? throw new InvalidOperationException(
                    $"Could not save story-traffic prefab '{prefabPath}'.");
                LegacyCharacterPresentationBinding savedCharacter =
                    prefab.GetComponent<LegacyCharacterPresentationBinding>();
                StoryTrafficVehiclePresentationBinding savedVehicle =
                    prefab.GetComponent<
                        StoryTrafficVehiclePresentationBinding>();
                string failure = string.Empty;
                if (savedCharacter == null ||
                    !savedCharacter.TryValidate(out failure) ||
                    savedVehicle == null ||
                    !savedVehicle.TryValidate(out failure))
                {
                    throw new InvalidOperationException(
                        $"Saved story-traffic fixture '{spec.id}' is invalid: {failure}");
                }

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureDynamicVehicle(
            GameObject root,
            VehicleSpec spec,
            Transform[] visualWheelTransforms,
            StoryTrafficVehiclePresentationBinding presentation)
        {
            float wheelRadius = 360f /
                (2f * Mathf.PI * spec.wheelDegreesPerMeter);
            Bounds chassisBounds = CalculateChassisBounds(
                root.transform,
                visualWheelTransforms,
                wheelRadius);
            VehicleSimulationConfig config =
                CreateStoryTrafficSimulationConfig(
                    spec,
                    chassisBounds,
                    wheelRadius);

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = config.Dynamics.ProvisionalMassKilograms;
            body.linearDamping = config.Dynamics.ChassisLinearDamping;
            body.angularDamping = config.Dynamics.ChassisAngularDamping;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.centerOfMass = config.Dynamics.CenterOfMassMeters;
            body.maxDepenetrationVelocity = 3f;
            body.maxLinearVelocity = spec.isAmbientTraffic ? 42f : 70f;
            body.maxAngularVelocity = 12f;

            BoxCollider chassis = root.AddComponent<BoxCollider>();
            chassis.center = config.Dynamics.ChassisColliderCenterMeters;
            chassis.size = config.Dynamics.ChassisColliderSizeMeters;

            var physicsWheels = new WheelController[visualWheelTransforms.Length];
            for (int index = 0; index < visualWheelTransforms.Length; index++)
            {
                Vector3 visualCenter = root.transform.InverseTransformPoint(
                    visualWheelTransforms[index].position);
                var wheelObject = new GameObject(
                    $"NWH_PhysicsWheel_{index + 1:00}");
                wheelObject.transform.SetParent(root.transform, false);
                wheelObject.transform.localPosition = visualCenter +
                    Vector3.up * config.Dynamics.SuspensionRestLengthMeters;
                wheelObject.transform.localRotation = Quaternion.identity;

                WheelController wheel =
                    wheelObject.AddComponent<WheelController>();
                wheel.Radius = config.Dynamics.WheelRadiusMeters;
                wheel.Width = Mathf.Clamp(
                    config.Dynamics.WheelRadiusMeters * 0.62f,
                    0.14f,
                    0.24f);
                wheel.Mass = config.Dynamics.WheelInertiaKilogramSquareMeters /
                             Mathf.Max(
                                 0.0001f,
                                 config.Dynamics.WheelRadiusMeters *
                                 config.Dynamics.WheelRadiusMeters);
                wheel.SpringMaxLength =
                    config.Dynamics.SuspensionTravelMeters;
                float configuredSpringForce =
                    config.Dynamics.SpringRateNewtonPerMeter *
                    config.Dynamics.SuspensionTravelMeters;
                // WheelController starts at roughly 30% compression. The
                // transferred soft spring values alone provide less than the
                // chassis' static weight at that pose, which lets heavy actors
                // bottom through a thin donor road before the spring catches.
                // Keep the transferred rates in project simulation data, but
                // satisfy NWH's documented 3x static-load force reserve here.
                float nwhStaticLoadReserve = body.mass *
                    Mathf.Abs(Physics.gravity.y) * 0.75f;
                wheel.SpringMaxForce = Mathf.Max(
                    configuredSpringForce,
                    nwhStaticLoadReserve);
                wheel.DamperBumpRate =
                    config.Dynamics.DamperRateNewtonSecondsPerMeter;
                wheel.DamperReboundRate =
                    config.Dynamics.DamperRateNewtonSecondsPerMeter;
                wheel.MaxLoad = body.mass * Mathf.Abs(Physics.gravity.y) * 0.5f;
                wheel.forwardFriction.grip = index >= 2 ? 0.84f : 0.88f;
                wheel.sideFriction.grip = string.Equals(
                        spec.id,
                        "petteri-car",
                        StringComparison.Ordinal)
                    ? index >= 2 ? 0.84f : 0.9f
                    : index >= 2 ? 0.88f : 0.92f;
                wheel.forwardFriction.stiffness = 0.82f;
                wheel.sideFriction.stiffness = 0.9f;
                wheel.rollingResistanceTorque =
                    config.Dynamics.RollingResistanceCoefficient *
                    body.mass * Mathf.Abs(Physics.gravity.y) * 0.25f *
                    config.Dynamics.WheelRadiusMeters;
                wheel.forceApplicationPointDistance = 0.72f;
                wheel.frictionSubsteps = 20;
                wheel.useContactModification = true;
                int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
                int worldSolidLayer = LayerMask.NameToLayer("WorldSolid");
                int groundLayers =
                    (worldSurfaceLayer >= 0 ? 1 << worldSurfaceLayer : 0) |
                    (worldSolidLayer >= 0 ? 1 << worldSolidLayer : 0);
                wheel.layerMask = groundLayers != 0
                    ? groundLayers
                    : Physics.DefaultRaycastLayers;
                int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
                wheel.meshColliderLayer = ignoreRaycastLayer >= 0
                    ? ignoreRaycastLayer
                    : 2;
                physicsWheels[index] = wheel;
            }

            NwhWheelPhysicsBackend wheelBackend =
                root.AddComponent<NwhWheelPhysicsBackend>();
            wheelBackend.Configure(body, physicsWheels);
            bool heavyVehicle = spec.provisionalMassKilograms >= 2500f;
            bool isBus = string.Equals(
                spec.id,
                "traffic-bus",
                StringComparison.Ordinal);
            wheelBackend.ConfigureAxleStability(
                configuredFrontAntiRollForceNewtons:
                    heavyVehicle ? 12000f : 2600f,
                configuredRearAntiRollForceNewtons:
                    heavyVehicle ? 9500f : 2100f);
            NwhStoryTrafficVehicleMotionBackend motionBackend =
                root.AddComponent<NwhStoryTrafficVehicleMotionBackend>();
            motionBackend.Configure(body, wheelBackend, config);
            motionBackend.ConfigureChassisSafetyLimits(
                configuredMaximumDepenetrationVelocityMetersPerSecond: 3f,
                configuredMaximumChassisVelocityMetersPerSecond:
                    spec.isAmbientTraffic ? 42f : 70f);
            motionBackend.ConfigureTrafficControlTuning(
                configuredSpeedResponseGain: 0.48f,
                configuredRollingThrottle01: 0.08f,
                configuredLaunchClutchPedal01:
                    ResolveLaunchClutchPedal01(
                        spec.isAmbientTraffic,
                        isBus),
                configuredLaunchClutchReleaseSpeedMetersPerSecond:
                    ResolveLaunchClutchReleaseSpeedMetersPerSecond(
                        spec.isAmbientTraffic,
                        isBus),
                configuredUpshiftRpm: isBus ? 2200f : 6200f,
                // The old 900 RPM bus threshold let a free-rev upshift leave
                // the 9.8-tonne chassis in fifth at roughly 10 m/s. A modest
                // 1100 RPM floor keeps fourth until the road speed can support
                // fifth, while remaining below the post-upshift RPM and thus
                // avoiding the former 4<->5 oscillation.
                configuredDownshiftRpm: isBus ? 1100f : 2200f,
                configuredSteeringLookAheadMeters: 5f);
            presentation.ConfigureMotionBackendForAuthoring(motionBackend);
        }

        private static void ConfigureInCarCrashRagdolls(
            GameObject root,
            VehicleSpec spec,
            Transform betterMscPassengerRoot,
            Animation authoredPoseAnimation,
            StoryTrafficVehiclePresentationBinding presentation)
        {
            if (root == null || spec == null || presentation == null)
            {
                throw new ArgumentNullException(
                    "Story-traffic in-car ragdoll authoring requires a wrapper, spec and presentation.");
            }

            Rigidbody chassisBody = root.GetComponent<Rigidbody>();
            if (chassisBody == null)
            {
                throw new InvalidOperationException(
                    $"Story-traffic fixture '{spec.id}' has no authored chassis Rigidbody for crash occupants.");
            }

            Transform driverPelvis = RequireUniqueDescendant(
                root.transform,
                "pelvis",
                $"{spec.id} driver pelvis");
            var occupants = new List<
                StoryTrafficInCarRagdollBinding.OccupantDefinition>
            {
                BuildInCarRagdollOccupant(
                    spec.driverFeatureId,
                    driverPelvis.gameObject,
                    betterMscPassengerRoot == null &&
                    authoredPoseAnimation != null
                        ? new Behaviour[] { authoredPoseAnimation }
                        : Array.Empty<Behaviour>(),
                    BuildLegacyDriverRagdollBodies(root.transform, spec.id)),
            };

            if (betterMscPassengerRoot != null)
            {
                if (spec.passengerFeatureIds == null ||
                    spec.passengerFeatureIds.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Story-traffic fixture '{spec.id}' has a BetterMSC passenger rig without exactly one passenger FeatureId.");
                }

                occupants.Add(BuildInCarRagdollOccupant(
                    spec.passengerFeatureIds[0],
                    betterMscPassengerRoot.gameObject,
                    authoredPoseAnimation != null
                        ? new Behaviour[] { authoredPoseAnimation }
                        : Array.Empty<Behaviour>(),
                    BuildBetterMscSuskiRagdollBodies(
                        betterMscPassengerRoot,
                        spec.id)));
            }

            StoryTrafficInCarRagdollBinding ragdoll = root.AddComponent<
                StoryTrafficInCarRagdollBinding>();
            ragdoll.ConfigureForAuthoring(chassisBody, occupants);
            if (!ragdoll.TryValidate(out string failure))
            {
                throw new InvalidOperationException(
                    $"Story-traffic fixture '{spec.id}' has invalid in-car crash occupants: {failure}");
            }

            presentation.ConfigureInCarRagdollForAuthoring(ragdoll);
        }

        private static StoryTrafficInCarRagdollBinding.OccupantDefinition
            BuildInCarRagdollOccupant(
                string featureId,
                GameObject presentationRoot,
                IEnumerable<Behaviour> poseDrivers,
                IEnumerable<StoryTrafficInCarRagdollBinding.BodyDefinition>
                    bodies)
        {
            return new StoryTrafficInCarRagdollBinding.OccupantDefinition(
                featureId,
                presentationRoot,
                poseDrivers,
                bodies);
        }

        private static StoryTrafficInCarRagdollBinding.BodyDefinition[]
            BuildLegacyDriverRagdollBodies(
                Transform vehicleRoot,
                string fixtureId)
        {
            Transform pelvis = RequireUniqueDescendant(
                vehicleRoot,
                "pelvis",
                $"{fixtureId} driver pelvis");
            Transform spine = RequireUniqueDescendant(
                vehicleRoot,
                "spine_upper",
                $"{fixtureId} driver upper spine");
            Transform head = RequireUniqueDescendant(
                vehicleRoot,
                "head",
                $"{fixtureId} driver head");
            Transform leftUpperArm = RequireUniqueDescendant(
                vehicleRoot,
                "shoulder_left",
                $"{fixtureId} driver left upper arm");
            Transform leftForearm = RequireUniqueDescendant(
                vehicleRoot,
                "arm_left",
                $"{fixtureId} driver left forearm");
            Transform leftHand = RequireUniqueDescendant(
                vehicleRoot,
                "hand_left",
                $"{fixtureId} driver left hand");
            Transform rightUpperArm = RequireUniqueDescendant(
                vehicleRoot,
                "shoulder_right",
                $"{fixtureId} driver right upper arm");
            Transform rightForearm = RequireUniqueDescendant(
                vehicleRoot,
                "arm_right",
                $"{fixtureId} driver right forearm");
            Transform rightHand = RequireUniqueDescendant(
                vehicleRoot,
                "hand_right",
                $"{fixtureId} driver right hand");
            Transform leftThigh = RequireUniqueDescendant(
                vehicleRoot,
                "thig_left",
                $"{fixtureId} driver left thigh");
            Transform leftCalf = RequireUniqueDescendant(
                vehicleRoot,
                "knee_left",
                $"{fixtureId} driver left calf");
            Transform leftFoot = RequireUniqueDescendant(
                vehicleRoot,
                "ankle_left",
                $"{fixtureId} driver left foot");
            Transform rightThigh = RequireUniqueDescendant(
                vehicleRoot,
                "thig_right",
                $"{fixtureId} driver right thigh");
            Transform rightCalf = RequireUniqueDescendant(
                vehicleRoot,
                "knee_right",
                $"{fixtureId} driver right calf");
            Transform rightFoot = RequireUniqueDescendant(
                vehicleRoot,
                "ankle_right",
                $"{fixtureId} driver right foot");

            return BuildHumanoidRagdollBodies(
                pelvis,
                spine,
                head,
                leftUpperArm,
                leftForearm,
                leftHand,
                rightUpperArm,
                rightForearm,
                rightHand,
                leftThigh,
                leftCalf,
                leftFoot,
                rightThigh,
                rightCalf,
                rightFoot);
        }

        private static StoryTrafficInCarRagdollBinding.BodyDefinition[]
            BuildBetterMscSuskiRagdollBodies(
                Transform passengerRoot,
                string fixtureId)
        {
            Transform pelvis = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 Pelvis",
                $"{fixtureId} BetterMSC Suski pelvis");
            Transform spine = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 Spine2",
                $"{fixtureId} BetterMSC Suski upper spine");
            Transform neck = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 Neck",
                $"{fixtureId} BetterMSC Suski neck");
            Transform head = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 Head",
                $"{fixtureId} BetterMSC Suski head");
            Transform leftUpperArm = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 L UpperArm",
                $"{fixtureId} BetterMSC Suski left upper arm");
            Transform leftForearm = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 L Forearm",
                $"{fixtureId} BetterMSC Suski left forearm");
            Transform leftHand = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 L Hand",
                $"{fixtureId} BetterMSC Suski left hand");
            Transform rightUpperArm = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 R UpperArm",
                $"{fixtureId} BetterMSC Suski right upper arm");
            Transform rightForearm = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 R Forearm",
                $"{fixtureId} BetterMSC Suski right forearm");
            Transform rightHand = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 R Hand",
                $"{fixtureId} BetterMSC Suski right hand");
            Transform leftThigh = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 L Thigh",
                $"{fixtureId} BetterMSC Suski left thigh");
            Transform leftCalf = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 L Calf",
                $"{fixtureId} BetterMSC Suski left calf");
            Transform leftFoot = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 L Foot",
                $"{fixtureId} BetterMSC Suski left foot");
            Transform rightThigh = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 R Thigh",
                $"{fixtureId} BetterMSC Suski right thigh");
            Transform rightCalf = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 R Calf",
                $"{fixtureId} BetterMSC Suski right calf");
            Transform rightFoot = RequireUniqueDescendant(
                passengerRoot,
                "Bip01 R Foot",
                $"{fixtureId} BetterMSC Suski right foot");

            return BuildHumanoidRagdollBodies(
                pelvis,
                spine,
                head,
                leftUpperArm,
                leftForearm,
                leftHand,
                rightUpperArm,
                rightForearm,
                rightHand,
                leftThigh,
                leftCalf,
                leftFoot,
                rightThigh,
                rightCalf,
                rightFoot,
                spineCapsuleEndpoint: neck);
        }

        private static StoryTrafficInCarRagdollBinding.BodyDefinition[]
            BuildHumanoidRagdollBodies(
                Transform pelvis,
                Transform spine,
                Transform head,
                Transform leftUpperArm,
                Transform leftForearm,
                Transform leftHand,
                Transform rightUpperArm,
                Transform rightForearm,
                Transform rightHand,
                Transform leftThigh,
                Transform leftCalf,
                Transform leftFoot,
                Transform rightThigh,
                Transform rightCalf,
                Transform rightFoot,
                Transform spineCapsuleEndpoint = null)
        {
            return new[]
            {
                CreateInCarBody(pelvis, null, -1, InCarRagdollShape.Box,
                    new Vector3(0.34f, 0.24f, 0.28f), 0.12f, 14f, 25f, 35f),
                CreateInCarBody(spine, spineCapsuleEndpoint ?? head, 0,
                    InCarRagdollShape.Capsule, Vector3.zero, 0.16f, 12f, 25f, 35f),
                CreateInCarBody(head, null, 1, InCarRagdollShape.Sphere,
                    Vector3.zero, 0.135f, 5f, 25f, 30f),
                CreateInCarBody(leftUpperArm, leftForearm, 1, InCarRagdollShape.Capsule,
                    Vector3.zero, 0.075f, 3f, 55f, 70f),
                CreateInCarBody(leftForearm, leftHand, 3, InCarRagdollShape.Capsule,
                    Vector3.zero, 0.065f, 2f, 12f, 80f),
                CreateInCarBody(rightUpperArm, rightForearm, 1, InCarRagdollShape.Capsule,
                    Vector3.zero, 0.075f, 3f, 55f, 70f),
                CreateInCarBody(rightForearm, rightHand, 5,
                    InCarRagdollShape.Capsule,
                    Vector3.zero, 0.065f, 2f, 12f, 80f),
                CreateInCarBody(leftThigh, leftCalf, 0, InCarRagdollShape.Capsule,
                    Vector3.zero, 0.11f, 7.5f, 35f, 55f),
                CreateInCarBody(leftCalf, leftFoot, 7, InCarRagdollShape.Capsule,
                    Vector3.zero, 0.09f, 4.5f, 8f, 75f),
                CreateInCarBody(rightThigh, rightCalf, 0, InCarRagdollShape.Capsule,
                    Vector3.zero, 0.11f, 7.5f, 35f, 55f),
                CreateInCarBody(rightCalf, rightFoot, 9, InCarRagdollShape.Capsule,
                    Vector3.zero, 0.09f, 4.5f, 8f, 75f),
            };
        }

        private static StoryTrafficInCarRagdollBinding.BodyDefinition
            CreateInCarBody(
                Transform bone,
                Transform endpoint,
                int connectedBodyIndex,
                StoryTrafficInCarRagdollBinding.ColliderShape shape,
                Vector3 boxSize,
                float radius,
                float massKilograms,
                float twistDegrees,
                float swingDegrees)
        {
            return new StoryTrafficInCarRagdollBinding.BodyDefinition(
                bone,
                endpoint,
                connectedBodyIndex,
                shape,
                boxSize,
                radius,
                massKilograms,
                twistDegrees,
                swingDegrees);
        }

        internal static float ResolveLaunchClutchPedal01(
            bool isAmbientTraffic,
            bool isBus)
        {
            // Clutch pedal 1 is fully open. The bus and provisional ambient
            // engines have very low imported inertia, so the old universal
            // 0.45 value coupled more torque at idle than an ordinary 145 Nm
            // ambient engine could produce. That caused the observed loop of
            // start, select first, stall, restart. Keep the accepted story-car
            // tuning intact and give generated ambient traffic enough slip to
            // build wheel speed before full engagement.
            return isBus ? 0.72f : isAmbientTraffic ? 0.68f : 0.45f;
        }

        internal static float ResolveLaunchClutchReleaseSpeedMetersPerSecond(
            bool isAmbientTraffic,
            bool isBus)
        {
            return isBus ? 6f : isAmbientTraffic ? 4.5f : 3f;
        }

        private static Transform[] CreateWheelSpinPivots(
            IReadOnlyList<Transform> donorWheelRoots)
        {
            if (donorWheelRoots == null || donorWheelRoots.Count == 0)
            {
                throw new InvalidOperationException(
                    "Story-traffic fixture has no donor wheel roots.");
            }

            var pivots = new Transform[donorWheelRoots.Count];
            for (int index = 0; index < donorWheelRoots.Count; index++)
            {
                pivots[index] = CreateWheelSpinPivot(
                    donorWheelRoots[index],
                    index);
            }

            return pivots;
        }

        /// <summary>
        /// Donor wheel roots also own suspension and drivetrain geometry. A
        /// project-owned pivot isolates only tire/rim/hub renderer branches so
        /// steering and axle rotation cannot move a spindle, axle or tandem
        /// arm with the wheel.
        /// </summary>
        internal static Transform CreateWheelSpinPivot(
            Transform donorWheelRoot,
            int wheelIndex)
        {
            if (donorWheelRoot == null)
            {
                throw new ArgumentNullException(nameof(donorWheelRoot));
            }

            Renderer[] renderers = donorWheelRoot.GetComponentsInChildren<
                Renderer>(true);
            var rotatingBranches = new List<Transform>();
            foreach (Renderer renderer in renderers)
            {
                Transform branch = ResolveRotatingWheelBranch(
                    renderer.transform,
                    donorWheelRoot);
                if (branch != null && !rotatingBranches.Contains(branch))
                {
                    rotatingBranches.Add(branch);
                }
            }

            if (rotatingBranches.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Donor wheel root '{donorWheelRoot.name}' has no " +
                    "isolatable tire/rim/hub renderer geometry.");
            }

            Transform[] mechanicalBranches = donorWheelRoot
                .GetComponentsInChildren<Transform>(true)
                .Where(candidate =>
                    candidate != donorWheelRoot &&
                    IsMechanicalWheelBranchName(candidate.name))
                .ToArray();
            foreach (Transform branch in rotatingBranches)
            {
                if (branch.GetComponentsInChildren<Transform>(true).Any(
                        candidate =>
                            candidate != branch &&
                            IsMechanicalWheelBranchName(candidate.name)))
                {
                    throw new InvalidOperationException(
                        $"Wheel visual branch '{branch.name}' below " +
                        $"'{donorWheelRoot.name}' also owns mechanical " +
                        "geometry and cannot be used as a spin target.");
                }
            }

            var pivotObject = new GameObject(
                $"WheelSpinPivot_{wheelIndex + 1:00}");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(donorWheelRoot, false);
            foreach (Transform branch in rotatingBranches)
            {
                branch.SetParent(pivot, true);
            }

            if (pivot.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                UnityEngine.Object.DestroyImmediate(pivotObject);
                throw new InvalidOperationException(
                    $"Wheel spin pivot below '{donorWheelRoot.name}' " +
                    "contains no renderer geometry.");
            }

            if (mechanicalBranches.Any(branch => branch.IsChildOf(pivot)))
            {
                UnityEngine.Object.DestroyImmediate(pivotObject);
                throw new InvalidOperationException(
                    $"Wheel spin pivot below '{donorWheelRoot.name}' " +
                    "captured a mechanical suspension/drivetrain branch.");
            }

            return pivot;
        }

        private static Transform ResolveRotatingWheelBranch(
            Transform rendererTransform,
            Transform donorWheelRoot)
        {
            Transform current = rendererTransform;
            Transform highestVisualBranch = null;
            while (current != null && current != donorWheelRoot)
            {
                if (IsMechanicalWheelBranchName(current.name))
                {
                    return null;
                }

                // Some donor cars parent the driveshaft below the generic
                // "Wheel" container beside Tire and Rim. Moving that whole
                // container would reproduce the visible axle/bridge artifact.
                // Keep walking so the narrower Tire/Rim renderer branches can
                // still be isolated, but never select an ancestor that owns a
                // mechanical descendant.
                if (IsRotatingWheelVisualName(current.name) &&
                    !ContainsMechanicalWheelDescendant(current))
                {
                    highestVisualBranch = current;
                }

                current = current.parent;
            }

            return current == donorWheelRoot
                ? highestVisualBranch
                : null;
        }

        private static bool ContainsMechanicalWheelDescendant(Transform root)
        {
            return root.GetComponentsInChildren<Transform>(true).Any(
                candidate =>
                    candidate != root &&
                    IsMechanicalWheelBranchName(candidate.name));
        }

        private static bool IsRotatingWheelVisualName(string objectName)
        {
            string normalized = (objectName ?? string.Empty).ToLowerInvariant();
            return normalized.Contains("wheel") ||
                   normalized.Contains("tire") ||
                   normalized.Contains("tyre") ||
                   normalized.Contains("rim") ||
                   normalized.Contains("hub");
        }

        private static bool IsMechanicalWheelBranchName(string objectName)
        {
            string normalized = (objectName ?? string.Empty).ToLowerInvariant();
            // A donor "*_axle_hub" is rotating hub geometry, not the axle
            // branch itself.
            if (normalized.Contains("hub"))
            {
                return false;
            }

            return normalized.Contains("spindle") ||
                   normalized.Contains("axle") ||
                   normalized.Contains("shaft") ||
                   normalized.Contains("brake") ||
                   normalized.Contains("ikpivot") ||
                   normalized.Contains("tandem_arm") ||
                   normalized == "arm" ||
                   normalized.StartsWith("arm_", StringComparison.Ordinal);
        }

        private static Bounds CalculateChassisBounds(
            Transform root,
            IReadOnlyList<Transform> wheelTransforms,
            float wheelRadius)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<
                MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "Story-traffic fixture has no vehicle renderers for chassis bounds.");
            }

            bool initialized = false;
            Bounds localBounds = default;
            foreach (MeshRenderer renderer in renderers)
            {
                Bounds bounds = renderer.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 worldCorner = new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z);
                    Vector3 localCorner = root.InverseTransformPoint(
                        worldCorner);
                    if (!initialized)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }

            float wheelCenterY = wheelTransforms.Average(wheel =>
                root.InverseTransformPoint(wheel.position).y);
            float colliderBottom = wheelCenterY - wheelRadius * 0.42f;
            float colliderTop = Mathf.Max(
                colliderBottom + 0.35f,
                localBounds.max.y - wheelRadius * 0.08f);
            Vector3 size = new Vector3(
                Mathf.Max(0.8f, localBounds.size.x * 0.9f),
                colliderTop - colliderBottom,
                Mathf.Max(1.6f, localBounds.size.z * 0.94f));
            Vector3 center = new Vector3(
                localBounds.center.x,
                (colliderBottom + colliderTop) * 0.5f,
                localBounds.center.z);
            return new Bounds(center, size);
        }

        private static VehicleSimulationConfig
            CreateStoryTrafficSimulationConfig(
                VehicleSpec spec,
                Bounds chassisBounds,
                float wheelRadius)
        {
            bool isJani = string.Equals(
                spec.id,
                "jani-car",
                StringComparison.Ordinal);
            bool isAmbient = spec.isAmbientTraffic;
            bool isTruck = isAmbient && string.Equals(
                spec.id,
                "traffic-truck",
                StringComparison.Ordinal);
            bool isBus = isAmbient && string.Equals(
                spec.id,
                "traffic-bus",
                StringComparison.Ordinal);
            var config = ScriptableObject.CreateInstance<
                VehicleSimulationConfig>();
            config.name = spec.id + "_simulation";
            string evidence =
                "Locked donor GAME.unity SHA-256 c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4; " +
                (isAmbient
                    ? "sanitized donor highway vehicle geometry and wheel transforms"
                    : isJani
                    ? "Rigidbody 90042, drivetrain 106062, axles 106064"
                    : "Rigidbody 90346, drivetrain 107589, axles 107600");
            config.ConfigureStoryTraffic(
                isAmbient
                    ? "phase1.ambient-traffic." + spec.id
                    : "phase1.story-traffic." + spec.id,
                configuredSubstepCount: 5,
                configuredLeftDrivenWheelIndex: 2,
                configuredRightDrivenWheelIndex: 3,
                classification:
                    isAmbient
                        ? VehicleReferenceClassification.ObservedDonorReference
                        : VehicleReferenceClassification.MeasuredDonorReference,
                source: evidence,
                notes:
                    isAmbient
                        ? "Temporary donor presentation and wheel layout are transferred. Mass and powertrain tuning are project-derived Phase 1 values pending per-model donor capture."
                        : "Mass, drivetrain ratios, engine landmarks, suspension, braking, steering and RWD topology are transferred. The continuous torque curve between donor landmarks is project-derived.");

            float maximumTorque = isAmbient
                ? isBus ? 886f : isTruck ? 330f : 145f
                : isJani ? 200f : 122f;
            float torqueAtPowerPeak = isAmbient
                ? maximumTorque * 0.91f
                : isJani ? 195f : 125f;
            config.Engine.Configure(
                configuredInertiaKilogramSquareMeters: 0.04f,
                configuredIdleTargetRpm: isBus ? 500f : 680f,
                configuredStartThresholdRpm: isBus ? 280f : 450f,
                configuredStallRpm: isBus ? 250f : 420f,
                configuredRedlineRpm: isBus ? 2600f : 7800f,
                configuredMaximumRpm: isBus ? 2700f : 8000f,
                configuredBaseFrictionNewtonMeters: 7f,
                configuredViscousFrictionNewtonMetersPerRadian: 0.01f,
                configuredEngineBrakingNewtonMeters: 18f,
                configuredThrottleResponsePerSecond: 10f,
                configuredStarterTorqueNewtonMeters: 72f,
                configuredStarterMaximumRpm: 650f,
                configuredStallDelaySeconds: 0.28f,
                configuredTorqueCurve: new[]
                {
                    new VehicleTorqueSample(0f, 0f),
                    new VehicleTorqueSample(isBus ? 300f : 680f,
                        maximumTorque * 0.46f),
                    new VehicleTorqueSample(isBus ? 900f : 1500f,
                        maximumTorque * 0.72f),
                    new VehicleTorqueSample(isBus ? 1400f : 4000f,
                        maximumTorque),
                    new VehicleTorqueSample(isBus ? 2200f : 6000f,
                        torqueAtPowerPeak),
                    new VehicleTorqueSample(isBus ? 2600f : 7800f, 0f),
                });
            config.Clutch.Configure(
                // The old universal 179.2 Nm clutch was below the 330 Nm
                // truck and 886 Nm bus engines. They could reach high RPM but
                // only transmit passenger-car torque, so the bus crawled and
                // every heavy engine sounded permanently unloaded.
                configuredMaximumTorqueNewtonMeters: Mathf.Max(
                    112f * 1.6f,
                    maximumTorque * 1.35f),
                configuredSlipStiffness: 7.5f,
                configuredEngagementExponent: 1.6f);
            config.Gearbox.Configure(
                configuredReverseRatio: isAmbient
                    ? isBus ? -6.96f : -3.9f
                    : isJani ? -4.093f : -4.27f,
                configuredForwardRatios: isAmbient
                    ? isBus
                        ? new[] { 7.41f, 4.27f, 2.75f, 1.84f, 1.24f, 1f }
                        : isTruck
                        ? new[] { 5.8f, 3.4f, 2.1f, 1.35f, 1f }
                        : new[] { 3.8f, 2.2f, 1.45f, 1f }
                    : isJani
                    ? new[] { 3.673f, 2.217f, 1.448f, 1f }
                    : new[] { 4.56f, 2.54f, 1.69f, 1.296f, 1f },
                configuredFinalDriveRatio: isAmbient
                    ? isBus ? 4.7f : isTruck ? 5.1f : 4.15f
                    : isJani ? 4.286f : 4.1f,
                configuredEfficiency: 0.9f);
            float mass = isAmbient
                ? Mathf.Max(500f, spec.provisionalMassKilograms)
                : isJani ? 1010f : 875f;
            float springRate = isAmbient
                ? isBus ? 210000f : isTruck ? 62000f : 27000f
                : isJani ? 28000f : 24000f;
            float damperRate = isAmbient
                ? isBus ? 8000f : isTruck ? 4800f : 1950f
                : isJani ? 2000f : 1700f;
            // Locked donor CoG transforms: Jani 48948 under KYLAJANI 43020,
            // Petteri 43274 under AMIS2 48255. The earlier generic 0.2 m
            // value raised both cars and amplified fore/aft rocking.
            Vector3 donorCenterOfMass = isAmbient
                ? new Vector3(0f, isBus ? 0.65f : isTruck ? 0.42f : 0.14f, 0f)
                : isJani
                ? new Vector3(0f, 0.065f, 0f)
                : new Vector3(0f, 0.1f, -0.05f);
            config.Dynamics.Configure(
                configuredMassKilograms: mass,
                configuredCenterOfMassMeters: donorCenterOfMass,
                configuredChassisColliderCenterMeters: chassisBounds.center,
                configuredChassisColliderSizeMeters: chassisBounds.size,
                configuredChassisLinearDamping: 0.02f,
                configuredChassisAngularDamping:
                    isAmbient ? isBus || isTruck ? 0.28f : 0.2f : 0.16f,
                configuredWheelRadiusMeters: wheelRadius,
                configuredWheelInertiaKilogramSquareMeters:
                    12f * wheelRadius * wheelRadius,
                configuredMaximumBrakeTorqueNewtonMeters:
                    isAmbient
                        ? isBus ? 14000f : isTruck ? 5200f : 2200f
                        : isJani ? 2400f : 2000f,
                configuredMaximumRearBrakeTorqueNewtonMeters: 100f,
                configuredMaximumHandbrakeTorqueNewtonMeters: 5000f,
                configuredMaximumSteeringAngleDegrees: isBus ? 40f : 35f,
                configuredHighSpeedSteeringAngleDegrees: 9f,
                configuredSteeringFadeSpeedMetersPerSecond: 51.4f,
                configuredSuspensionRestLengthMeters: 0.112f,
                configuredSuspensionTravelMeters: 0.16f,
                configuredSpringRateNewtonPerMeter: springRate,
                configuredDamperRateNewtonSecondsPerMeter: damperRate,
                configuredLongitudinalStiffness: 7000f,
                configuredLateralStiffness: isAmbient
                    ? isTruck ? 10500f : 6000f
                    : isJani ? 6200f : 5850f,
                configuredRollingResistanceCoefficient: 0.018f);

            if (!config.Validate(out string failure))
            {
                UnityEngine.Object.DestroyImmediate(config);
                throw new InvalidOperationException(
                    $"Story-traffic simulation config '{spec.id}' is invalid: {failure}");
            }

            string path = SimulationRoot + "/" + spec.id + ".asset";
            AssetDatabase.CreateAsset(config, path);
            return config;
        }

        private static AnimationClip CreateAuthoredPoseClip(string id)
        {
            var clip = new AnimationClip
            {
                name = id + "_authored_vehicle_pose",
                legacy = true,
                wrapMode = WrapMode.Loop,
            };
            AnimationUtility.SetAnimationEvents(
                clip,
                Array.Empty<AnimationEvent>());
            string path = ClipRoot + "/" + id + "_authored_pose.anim";
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static Transform BuildBetterMscPassenger(
            DonorUnitySceneModel sourceModel,
            DonorActionSlice slice,
            Transform passengerAnchor,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            var created = new Dictionary<long, Transform>();
            foreach (DonorTransformRecord donor in slice.Transforms)
            {
                bool modelRoot = donor.TransformId == slice.RootTransformId;
                Transform parent = modelRoot
                    ? passengerAnchor
                    : RequireTransform(created, donor.FatherTransformId);
                var node = new GameObject(
                    sourceModel.GetGameObjectName(donor.GameObjectId));
                node.transform.SetParent(parent, false);
                node.transform.localPosition = modelRoot
                    ? Vector3.zero
                    : donor.LocalPosition;
                node.transform.localRotation = modelRoot
                    ? Quaternion.identity
                    : donor.LocalRotation;
                node.transform.localScale = donor.LocalScale;
                created.Add(donor.TransformId, node.transform);
            }

            foreach (DonorSkinnedRendererRecord donor in slice.Renderers)
            {
                DonorTransformRecord ownerRecord = slice.Transforms.Single(
                    candidate => candidate.GameObjectId == donor.GameObjectId);
                Mesh mesh = RequireAsset<Mesh>(importedMeshes[donor.MeshGuid]);
                var renderer = created[ownerRecord.TransformId]
                    .gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = donor.MaterialGuids
                    .Select(guid => materials[guid])
                    .ToArray();
                if (renderer.sharedMaterials.Length != mesh.subMeshCount)
                {
                    throw new InvalidOperationException(
                        $"BetterMSC Suski renderer {donor.ComponentId} has a material slot mismatch.");
                }

                renderer.rootBone = RequireTransform(
                    created,
                    donor.RootBoneTransformId);
                renderer.bones = donor.BoneTransformIds
                    .Select(id => RequireTransform(created, id))
                    .ToArray();
                renderer.localBounds = donor.LocalBounds;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                ConfigureBetterMscSuskiCulling(renderer);
            }

            return RequireTransform(created, slice.RootTransformId);
        }

        private static void ConfigureBetterMscSuskiCulling(
            SkinnedMeshRenderer renderer)
        {
            // The accepted rig can leave its authored seated bounds both when
            // the car banks and when the rescue ragdoll articulates. Keeping
            // skinning active and giving the one Phase-1 Suski renderer a
            // conservative local envelope prevents angle-dependent vanishing.
            renderer.updateWhenOffscreen = true;
            Bounds bounds = renderer.localBounds;
            bounds.size = new Vector3(
                Mathf.Max(4f, bounds.size.x),
                Mathf.Max(4f, bounds.size.y),
                Mathf.Max(4f, bounds.size.z));
            renderer.localBounds = bounds;
        }

        private static void RemoveLegacyPassengerChildren(
            Transform passengerAnchor)
        {
            if (passengerAnchor == null)
            {
                throw new ArgumentNullException(nameof(passengerAnchor));
            }

            for (int index = passengerAnchor.childCount - 1;
                 index >= 0;
                 index--)
            {
                UnityEngine.Object.DestroyImmediate(
                    passengerAnchor.GetChild(index).gameObject);
            }
        }

        private static AnimationClip BakeBetterMscPoseClip(
            string fixtureId,
            Transform modelRoot,
            AnimationClip sourceClip,
            Avatar sourceAvatar,
            Transform passengerAnchor,
            DonorPassengerPoseReference donorPassengerPose)
        {
            if (sourceClip == null || sourceAvatar == null ||
                !sourceAvatar.isValid || !sourceAvatar.isHuman)
            {
                throw new InvalidOperationException(
                    "BetterMSC Suski pose baking requires the locked humanoid Avatar and source clip.");
            }

            RestoreBetterMscAvatarTPose(modelRoot, sourceAvatar);
            Avatar compatibilityAvatar =
                BuildBetterMscCompatibilityAvatar(modelRoot, sourceAvatar);
            try
            {
                ApplyHumanoidMusclePose(
                    modelRoot,
                    compatibilityAvatar,
                    sourceClip,
                    0f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(compatibilityAvatar);
            }

            AlignBetterMscPassengerToReference(
                modelRoot,
                passengerAnchor,
                donorPassengerPose);
            ValidateBetterMscSeatedPose(
                modelRoot,
                passengerAnchor,
                donorPassengerPose);

            return CreateBetterMscConstantPoseClip(
                modelRoot,
                fixtureId + "_bettermsc_suski_vehicle_sit",
                ClipRoot + "/" + fixtureId +
                "_bettermsc_suski_vehicle_sit.anim");
        }

        private static AnimationClip BakeBetterMscStandalonePoseClip(
            string fixtureId,
            Transform modelRoot,
            AnimationClip sourceClip,
            Avatar sourceAvatar)
        {
            if (sourceClip == null || sourceAvatar == null ||
                !sourceAvatar.isValid || !sourceAvatar.isHuman)
            {
                throw new InvalidOperationException(
                    "BetterMSC Suski standing-pose baking requires the " +
                    "locked humanoid Avatar and source clip.");
            }

            RestoreBetterMscAvatarTPose(modelRoot, sourceAvatar);
            Avatar compatibilityAvatar =
                BuildBetterMscCompatibilityAvatar(modelRoot, sourceAvatar);
            try
            {
                ApplyHumanoidMusclePose(
                    modelRoot,
                    compatibilityAvatar,
                    sourceClip,
                    0f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(compatibilityAvatar);
            }

            Transform leftFoot = RequireUniqueDescendant(
                modelRoot,
                "Bip01 L Foot",
                "BetterMSC Suski left foot");
            Transform rightFoot = RequireUniqueDescendant(
                modelRoot,
                "Bip01 R Foot",
                "BetterMSC Suski right foot");
            Transform parent = modelRoot.parent;
            float lowestFoot = Mathf.Min(
                parent.InverseTransformPoint(leftFoot.position).y,
                parent.InverseTransformPoint(rightFoot.position).y);
            if (!float.IsFinite(lowestFoot) || Mathf.Abs(lowestFoot) > 2f)
            {
                throw new InvalidOperationException(
                    $"BetterMSC Suski standing foot height {lowestFoot} is invalid.");
            }

            modelRoot.localPosition -= Vector3.up * lowestFoot;
            return CreateBetterMscConstantPoseClip(
                modelRoot,
                fixtureId,
                ClipRoot + "/" + fixtureId + ".anim");
        }

        private static AnimationClip CreateBetterMscConstantPoseClip(
            Transform modelRoot,
            string clipName,
            string assetPath)
        {
            if (modelRoot == null)
            {
                throw new ArgumentNullException(nameof(modelRoot));
            }

            PoseSample[] samples = modelRoot
                .GetComponentsInChildren<Transform>(true)
                .Select(transform => new PoseSample(
                    AnimationUtility.CalculateTransformPath(
                        transform,
                        modelRoot),
                    transform.localPosition,
                    transform.localRotation,
                    transform.localScale))
                .ToArray();

            var baked = new AnimationClip
            {
                name = clipName,
                legacy = true,
                wrapMode = WrapMode.Loop,
            };
            foreach (PoseSample sample in samples)
            {
                SetConstantTransformCurve(
                    baked,
                    sample.Path,
                    "m_LocalPosition.x",
                    sample.Position.x);
                SetConstantTransformCurve(
                    baked,
                    sample.Path,
                    "m_LocalPosition.y",
                    sample.Position.y);
                SetConstantTransformCurve(
                    baked,
                    sample.Path,
                    "m_LocalPosition.z",
                    sample.Position.z);
                SetConstantTransformCurve(
                    baked,
                    sample.Path,
                    "m_LocalRotation.x",
                    sample.Rotation.x);
                SetConstantTransformCurve(
                    baked,
                    sample.Path,
                    "m_LocalRotation.y",
                    sample.Rotation.y);
                SetConstantTransformCurve(
                    baked,
                    sample.Path,
                    "m_LocalRotation.z",
                    sample.Rotation.z);
                SetConstantTransformCurve(
                    baked,
                    sample.Path,
                    "m_LocalRotation.w",
                    sample.Rotation.w);
                SetConstantTransformCurve(
                    baked,
                    sample.Path,
                    "m_LocalScale.x",
                    sample.Scale.x);
                SetConstantTransformCurve(
                    baked,
                    sample.Path,
                    "m_LocalScale.y",
                    sample.Scale.y);
                SetConstantTransformCurve(
                    baked,
                    sample.Path,
                    "m_LocalScale.z",
                    sample.Scale.z);
            }

            AnimationUtility.SetAnimationEvents(
                baked,
                Array.Empty<AnimationEvent>());
            AssetDatabase.CreateAsset(baked, assetPath);
            SetLoop(baked, true);
            return baked;
        }

        private static void AlignBetterMscPassengerToReference(
            Transform modelRoot,
            Transform passengerAnchor,
            DonorPassengerPoseReference donorPose)
        {
            Transform pelvis = RequireUniqueDescendant(
                modelRoot,
                "Bip01 Pelvis",
                "BetterMSC Suski pelvis");
            Transform leftFoot = RequireUniqueDescendant(
                modelRoot,
                "Bip01 L Foot",
                "BetterMSC Suski left foot");
            Transform rightFoot = RequireUniqueDescendant(
                modelRoot,
                "Bip01 R Foot",
                "BetterMSC Suski right foot");

            Vector3 replacementForward = HorizontalDirection(
                passengerAnchor.InverseTransformPoint(
                    (leftFoot.position + rightFoot.position) * 0.5f) -
                passengerAnchor.InverseTransformPoint(pelvis.position),
                "BetterMSC Suski seated forward direction");
            float yawCorrection = Vector3.SignedAngle(
                replacementForward,
                donorPose.Forward,
                Vector3.up);
            if (!float.IsFinite(yawCorrection) ||
                Mathf.Abs(yawCorrection) > 135f)
            {
                throw new InvalidOperationException(
                    $"BetterMSC Suski passenger yaw correction {yawCorrection:F2} degrees is invalid.");
            }

            modelRoot.localRotation = Quaternion.AngleAxis(
                yawCorrection,
                Vector3.up) * modelRoot.localRotation;
            Vector3 pelvisCorrection = donorPose.Pelvis -
                passengerAnchor.InverseTransformPoint(pelvis.position);
            if (!IsFinite(pelvisCorrection) ||
                pelvisCorrection.magnitude > 1.25f)
            {
                throw new InvalidOperationException(
                    $"BetterMSC Suski passenger pelvis correction {pelvisCorrection} metres is invalid.");
            }

            modelRoot.localPosition += pelvisCorrection;
            Vector3 calibratedPelvis = passengerAnchor.InverseTransformPoint(
                pelvis.position);
            Vector3 calibratedForward = HorizontalDirection(
                passengerAnchor.InverseTransformPoint(
                    (leftFoot.position + rightFoot.position) * 0.5f) -
                calibratedPelvis,
                "calibrated BetterMSC Suski seated forward direction");
            if (Vector3.Distance(calibratedPelvis, donorPose.Pelvis) > 0.01f ||
                Vector3.Dot(calibratedForward, donorPose.Forward) < 0.995f)
            {
                throw new InvalidOperationException(
                    "BetterMSC Suski passenger calibration did not preserve the donor pelvis and travel direction.");
            }

            Debug.Log(
                $"BetterMSC Suski passenger calibrated by yaw={yawCorrection:F2} degrees, " +
                $"translation={pelvisCorrection} m; donor pelvis={donorPose.Pelvis}, " +
                $"forward={donorPose.Forward}.");
        }

        private static void ValidateBetterMscSeatedPose(
            Transform modelRoot,
            Transform passengerAnchor,
            DonorPassengerPoseReference donorPose)
        {
            Vector3 pelvis = PassengerLocal("Bip01 Pelvis");
            Vector3 head = PassengerLocal("Bip01 Head");
            Vector3 leftUpperArm = PassengerLocal("Bip01 L UpperArm");
            Vector3 rightUpperArm = PassengerLocal("Bip01 R UpperArm");
            Vector3 leftForearm = PassengerLocal("Bip01 L Forearm");
            Vector3 rightForearm = PassengerLocal("Bip01 R Forearm");
            Vector3 leftHand = PassengerLocal("Bip01 L Hand");
            Vector3 rightHand = PassengerLocal("Bip01 R Hand");
            Vector3 leftThigh = PassengerLocal("Bip01 L Thigh");
            Vector3 rightThigh = PassengerLocal("Bip01 R Thigh");
            Vector3 leftCalf = PassengerLocal("Bip01 L Calf");
            Vector3 rightCalf = PassengerLocal("Bip01 R Calf");
            Vector3 leftFoot = PassengerLocal("Bip01 L Foot");
            Vector3 rightFoot = PassengerLocal("Bip01 R Foot");
            Transform neckTransform = RequireUniqueDescendant(
                modelRoot,
                "Bip01 Neck",
                "BetterMSC Suski neck");
            Transform headPivotTransform = RequireUniqueDescendant(
                modelRoot,
                "HeadPivot",
                "BetterMSC Suski head pivot");
            Transform headTransform = RequireUniqueDescendant(
                modelRoot,
                "Bip01 Head",
                "BetterMSC Suski head");

            Vector3 headOffset = head - pelvis;
            headOffset.y = 0f;
            bool uprightTorso = head.y > pelvis.y + 0.45f &&
                                headOffset.magnitude < 0.35f;
            Vector3 shoulderSpan = rightUpperArm - leftUpperArm;
            shoulderSpan.y = 0f;
            Vector3 shoulderMidpoint =
                (leftUpperArm + rightUpperArm) * 0.5f;
            Vector3 torsoMidpoint = (pelvis + head) * 0.5f;
            shoulderMidpoint.y = 0f;
            torsoMidpoint.y = 0f;
            bool symmetricShoulders = shoulderSpan.magnitude > 0.20f &&
                Mathf.Abs(leftUpperArm.y - rightUpperArm.y) < 0.08f &&
                Vector3.Distance(shoulderMidpoint, torsoMidpoint) < 0.12f;
            bool armsFoldIntoCabin =
                leftForearm.y < leftUpperArm.y - 0.10f &&
                rightForearm.y < rightUpperArm.y - 0.10f &&
                Vector3.Dot(
                    leftHand - leftForearm,
                    donorPose.Forward) > 0.10f &&
                Vector3.Dot(
                    rightHand - rightForearm,
                    donorPose.Forward) > 0.10f &&
                Mathf.Abs(leftHand.y - rightHand.y) < 0.08f;
            bool legsExtendForward =
                Vector3.Dot(
                    leftCalf - leftThigh,
                    donorPose.Forward) > 0.20f &&
                Vector3.Dot(
                    rightCalf - rightThigh,
                    donorPose.Forward) > 0.20f &&
                Vector3.Dot(
                    leftFoot - leftCalf,
                    donorPose.Forward) > 0.25f &&
                Vector3.Dot(
                    rightFoot - rightCalf,
                    donorPose.Forward) > 0.25f &&
                leftFoot.y < leftCalf.y - 0.10f &&
                rightFoot.y < rightCalf.y - 0.10f;
            bool headChainIsSourceCompatible =
                headPivotTransform.parent == neckTransform &&
                headTransform.parent == headPivotTransform &&
                headPivotTransform.localPosition.magnitude > 0.09f &&
                headPivotTransform.localPosition.magnitude < 0.11f &&
                headTransform.localPosition.magnitude < 0.001f;
            bool calibratedToDonorSeat =
                Vector3.Distance(pelvis, donorPose.Pelvis) < 0.01f &&
                Vector3.Dot(
                    HorizontalDirection(
                        (leftFoot + rightFoot) * 0.5f - pelvis,
                        "validated BetterMSC Suski forward direction"),
                    donorPose.Forward) > 0.995f;
            if (!uprightTorso || !symmetricShoulders ||
                !armsFoldIntoCabin || !legsExtendForward ||
                !headChainIsSourceCompatible || !calibratedToDonorSeat)
            {
                throw new InvalidOperationException(
                    "BetterMSC Suski seated-pose validation failed: " +
                    $"pelvis={pelvis}, head={head}, " +
                    $"upperArms={leftUpperArm}/{rightUpperArm}, " +
                    $"forearms={leftForearm}/{rightForearm}, " +
                    $"hands={leftHand}/{rightHand}, " +
                    $"thighs={leftThigh}/{rightThigh}, " +
                    $"calves={leftCalf}/{rightCalf}, " +
                    $"feet={leftFoot}/{rightFoot}, " +
                    $"headPivotLocal={headPivotTransform.localPosition}, " +
                    $"headLocal={headTransform.localPosition}, " +
                    $"donorPelvis={donorPose.Pelvis}, " +
                    $"donorForward={donorPose.Forward}.");
            }

            Vector3 PassengerLocal(string boneName)
            {
                Transform bone = modelRoot
                    .GetComponentsInChildren<Transform>(true)
                    .SingleOrDefault(candidate =>
                        string.Equals(
                            candidate.name,
                            boneName,
                            StringComparison.Ordinal));
                if (bone == null)
                {
                    throw new InvalidOperationException(
                        $"BetterMSC Suski seated pose is missing bone '{boneName}'.");
                }

                return passengerAnchor.InverseTransformPoint(bone.position);
            }
        }

        private static DonorPassengerPoseReference
            CaptureDonorPassengerPoseReference(
                IReadOnlyDictionary<long, Transform> created,
                Transform passengerAnchor,
                VehicleSpec spec)
        {
            Transform pelvis = RequireNamedTransform(
                created,
                spec.donorPassengerPelvisTransformFileId,
                "pelvis");
            Transform leftAnkle = RequireNamedTransform(
                created,
                spec.donorPassengerLeftAnkleTransformFileId,
                "ankle_left");
            Transform rightAnkle = RequireNamedTransform(
                created,
                spec.donorPassengerRightAnkleTransformFileId,
                "ankle_right");
            Vector3 pelvisLocal = passengerAnchor.InverseTransformPoint(
                pelvis.position);
            Vector3 feetLocal = passengerAnchor.InverseTransformPoint(
                (leftAnkle.position + rightAnkle.position) * 0.5f);
            return new DonorPassengerPoseReference(
                pelvisLocal,
                HorizontalDirection(
                    feetLocal - pelvisLocal,
                    "donor Suski seated forward direction"));
        }

        private static long[] RequiredFixtureTransformIds(VehicleSpec spec)
        {
            IEnumerable<long> required = spec.auxiliaryTransformFileIds;
            if (spec.betterMscPassengerAnchorTransformFileId > 0L)
            {
                required = required.Concat(new[]
                {
                    spec.donorPassengerPelvisTransformFileId,
                    spec.donorPassengerLeftAnkleTransformFileId,
                    spec.donorPassengerRightAnkleTransformFileId,
                });
            }

            return required.Distinct().ToArray();
        }

        private static Transform RequireNamedTransform(
            IReadOnlyDictionary<long, Transform> created,
            long transformId,
            string expectedName)
        {
            Transform transform = RequireTransform(created, transformId);
            if (!string.Equals(
                    transform.name,
                    expectedName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Locked donor transform {transformId} was expected to be '{expectedName}', got '{transform.name}'.");
            }

            return transform;
        }

        private static Transform RequireUniqueDescendant(
            Transform root,
            string name,
            string description)
        {
            Transform[] matches = root
                .GetComponentsInChildren<Transform>(true)
                .Where(candidate => string.Equals(
                    candidate.name,
                    name,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one {description} named '{name}', found {matches.Length}.");
            }

            return matches[0];
        }

        private static Vector3 HorizontalDirection(
            Vector3 value,
            string description)
        {
            value.y = 0f;
            if (!IsFinite(value) || value.sqrMagnitude < 0.01f)
            {
                throw new InvalidOperationException(
                    $"Could not calculate a finite {description}.");
            }

            return value.normalized;
        }

        private static Avatar BuildBetterMscCompatibilityAvatar(
            Transform modelRoot,
            Avatar sourceAvatar)
        {
            if (modelRoot == null)
            {
                throw new ArgumentNullException(nameof(modelRoot));
            }

            if (sourceAvatar == null || !sourceAvatar.isValid ||
                !sourceAvatar.isHuman)
            {
                throw new ArgumentException(
                    "A valid humanoid source Avatar is required.",
                    nameof(sourceAvatar));
            }

            // The AssetRipper-authored Avatar keeps its human description in
            // Unity's internal serialized Avatar payload, so the public
            // Avatar.humanDescription arrays are empty. Rebuild the same Biped
            // mapping explicitly after RestoreBetterMscAvatarTPose has applied
            // the locked internal T-pose. BetterMSC maps HeadPivot as the
            // humanoid head and leaves Bip01 Head as its fixed mesh bone; using
            // Bip01 Head here applies the pivot transform twice and stretches
            // the neck. Include every mapped finger segment; the previous
            // body-only map left hand muscles unresolved.
            HumanBone[] humanBones = CreateBetterMscHumanBones();
            SkeletonBone[] skeleton = modelRoot
                .GetComponentsInChildren<Transform>(true)
                .Select(transform => new SkeletonBone
                {
                    name = transform.name,
                    position = transform.localPosition,
                    rotation = transform.localRotation,
                    scale = transform.localScale,
                })
                .ToArray();
            var description = new HumanDescription
            {
                human = humanBones,
                skeleton = skeleton,
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false,
            };
            Avatar avatar = AvatarBuilder.BuildHumanAvatar(
                modelRoot.gameObject,
                description);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                if (avatar != null)
                {
                    UnityEngine.Object.DestroyImmediate(avatar);
                }

                throw new InvalidOperationException(
                    "Could not build the sanitized BetterMSC Suski compatibility Avatar.");
            }

            avatar.name = "BetterMSC_Suski_CompatibilityAvatar";
            return avatar;
        }

        private static void ApplyHumanoidMusclePose(
            Transform modelRoot,
            Avatar compatibilityAvatar,
            AnimationClip sourceClip,
            float sampleTime)
        {
            string[] muscleNames = HumanTrait.MuscleName;
            var muscleIndexByName = new Dictionary<string, int>(
                muscleNames.Length,
                StringComparer.Ordinal);
            for (int index = 0; index < muscleNames.Length; index++)
            {
                muscleIndexByName[muscleNames[index]] = index;
            }

            var pose = new HumanPose
            {
                bodyPosition = Vector3.zero,
                bodyRotation = Quaternion.identity,
                muscles = new float[HumanTrait.MuscleCount],
            };
            Vector3 bodyPosition = Vector3.zero;
            Quaternion bodyRotation = Quaternion.identity;
            int sampledMuscleCount = 0;
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(sourceClip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(
                    sourceClip,
                    binding);
                if (curve == null)
                {
                    continue;
                }

                float value = curve.Evaluate(sampleTime);
                if (muscleIndexByName.TryGetValue(
                        binding.propertyName,
                        out int muscleIndex))
                {
                    pose.muscles[muscleIndex] = value;
                    sampledMuscleCount++;
                    continue;
                }

                switch (binding.propertyName)
                {
                    case "RootT.x":
                        bodyPosition.x = value;
                        break;
                    case "RootT.y":
                        bodyPosition.y = value;
                        break;
                    case "RootT.z":
                        bodyPosition.z = value;
                        break;
                    case "RootQ.x":
                        bodyRotation.x = value;
                        break;
                    case "RootQ.y":
                        bodyRotation.y = value;
                        break;
                    case "RootQ.z":
                        bodyRotation.z = value;
                        break;
                    case "RootQ.w":
                        bodyRotation.w = value;
                        break;
                }
            }

            if (sampledMuscleCount < 20)
            {
                throw new InvalidOperationException(
                    $"BetterMSC Suski pose clip exposed only {sampledMuscleCount} humanoid muscle curves.");
            }

            float humanScale = GetHumanScale(modelRoot, compatibilityAvatar);
            if (!float.IsFinite(humanScale) || humanScale <= 0.01f)
            {
                throw new InvalidOperationException(
                    $"BetterMSC Suski compatibility Avatar has invalid human scale {humanScale}.");
            }

            // RootT curves in the donor humanoid clip are authored in metres.
            // HumanPose expects the body position normalized by Avatar.humanScale.
            // Assigning the raw values lifted Suski's pelvis through Jani's roof.
            pose.bodyPosition = bodyPosition / humanScale;
            pose.bodyRotation = NormalizeQuaternion(bodyRotation);
            var handler = new HumanPoseHandler(
                compatibilityAvatar,
                modelRoot);
            try
            {
                handler.SetHumanPose(ref pose);
            }
            finally
            {
                handler.Dispose();
            }
        }

        private static void RestoreBetterMscAvatarTPose(
            Transform modelRoot,
            Avatar sourceAvatar)
        {
            if (modelRoot == null)
            {
                throw new ArgumentNullException(nameof(modelRoot));
            }

            if (sourceAvatar == null || !sourceAvatar.isValid ||
                !sourceAvatar.isHuman)
            {
                throw new ArgumentException(
                    "A valid humanoid source Avatar is required.",
                    nameof(sourceAvatar));
            }

            var serializedAvatar = new SerializedObject(sourceAvatar);
            SerializedProperty humanPose = RequireSerializedProperty(
                serializedAvatar,
                "m_Avatar.m_Human.data.m_SkeletonPose.data.m_X");
            string[] poseBoneNames = CreateBetterMscHumanPoseBoneNames();
            if (humanPose.arraySize != poseBoneNames.Length)
            {
                throw new InvalidOperationException(
                    "BetterMSC Suski Avatar exposes inconsistent serialized T-pose arrays: " +
                    $"poseBones={poseBoneNames.Length}, humanPose={humanPose.arraySize}.");
            }

            Dictionary<string, Transform> transformsByName = modelRoot
                .GetComponentsInChildren<Transform>(true)
                .GroupBy(transform => transform.name, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Single(),
                    StringComparer.Ordinal);
            int restoredHumanCount = 0;
            var missingHumanBones = new List<string>();
            for (int poseIndex = 0;
                 poseIndex < poseBoneNames.Length;
                 poseIndex++)
            {
                string donorBoneName = poseBoneNames[poseIndex];
                if (!transformsByName.TryGetValue(
                        donorBoneName,
                        out Transform target))
                {
                    missingHumanBones.Add(donorBoneName);
                    continue;
                }

                ApplySerializedAvatarPose(
                    target,
                    humanPose.GetArrayElementAtIndex(poseIndex));
                restoredHumanCount++;
            }

            if (missingHumanBones.Count > 0)
            {
                throw new InvalidOperationException(
                    "BetterMSC Suski hierarchy is missing Avatar T-pose bones: " +
                    string.Join(", ", missingHumanBones));
            }

            if (restoredHumanCount != poseBoneNames.Length)
            {
                throw new InvalidOperationException(
                    $"BetterMSC Suski restored only {restoredHumanCount} of " +
                    $"{poseBoneNames.Length} humanoid T-pose transforms.");
            }
        }

        private static SerializedProperty RequireSerializedProperty(
            SerializedObject owner,
            string propertyPath) =>
            owner.FindProperty(propertyPath) ??
            throw new InvalidOperationException(
                $"BetterMSC Suski Avatar is missing serialized property '{propertyPath}'.");

        private static void ApplySerializedAvatarPose(
            Transform target,
            SerializedProperty pose)
        {
            SerializedProperty translation = pose.FindPropertyRelative("t");
            SerializedProperty rotation = pose.FindPropertyRelative("q");
            SerializedProperty scale = pose.FindPropertyRelative("s");
            if (translation == null || rotation == null || scale == null)
            {
                throw new InvalidOperationException(
                    "BetterMSC Suski Avatar contains an incomplete serialized transform pose.");
            }

            target.localPosition = ReadSerializedVector3(translation);
            target.localRotation = NormalizeQuaternion(
                ReadSerializedQuaternion(rotation));
            target.localScale = ReadSerializedVector3(scale);
        }

        private static Vector3 ReadSerializedVector3(
            SerializedProperty value) =>
            new Vector3(
                value.FindPropertyRelative("x").floatValue,
                value.FindPropertyRelative("y").floatValue,
                value.FindPropertyRelative("z").floatValue);

        private static Quaternion ReadSerializedQuaternion(
            SerializedProperty value) =>
            new Quaternion(
                value.FindPropertyRelative("x").floatValue,
                value.FindPropertyRelative("y").floatValue,
                value.FindPropertyRelative("z").floatValue,
                value.FindPropertyRelative("w").floatValue);

        private static float GetHumanScale(
            Transform modelRoot,
            Avatar compatibilityAvatar)
        {
            var probe = modelRoot.gameObject.AddComponent<Animator>();
            try
            {
                probe.avatar = compatibilityAvatar;
                return probe.humanScale;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        private static Quaternion NormalizeQuaternion(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x +
                value.y * value.y +
                value.z * value.z +
                value.w * value.w);
            if (!float.IsFinite(magnitude) || magnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            float inverse = 1f / magnitude;
            return new Quaternion(
                value.x * inverse,
                value.y * inverse,
                value.z * inverse,
                value.w * inverse);
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static string[] CreateBetterMscHumanPoseBoneNames() =>
            new[]
            {
                "Bip01",
                "Bip01 Pelvis",
                "Bip01 Spine",
                "Bip01 Spine1",
                "Bip01 Spine2",
                "Bip01 Neck",
                "HeadPivot",
                "Bip01 L Clavicle",
                "Bip01 L UpperArm",
                "Bip01 L Forearm",
                "Bip01 L Hand",
                "Bip01 L Finger0",
                "Bip01 L Finger01",
                "Bip01 L Finger1",
                "Bip01 L Finger11",
                "Bip01 L Finger2",
                "Bip01 L Finger21",
                "Bip01 L Finger3",
                "Bip01 L Finger31",
                "Bip01 L Finger4",
                "Bip01 L Finger41",
                "Bip01 R Clavicle",
                "Bip01 R UpperArm",
                "Bip01 R Forearm",
                "Bip01 R Hand",
                "Bip01 R Finger0",
                "Bip01 R Finger01",
                "Bip01 R Finger1",
                "Bip01 R Finger11",
                "Bip01 R Finger2",
                "Bip01 R Finger21",
                "Bip01 R Finger3",
                "Bip01 R Finger31",
                "Bip01 R Finger4",
                "Bip01 R Finger41",
                "Bip01 L Thigh",
                "Bip01 L Calf",
                "Bip01 L Foot",
                "Bip01 L Toe0",
                "Bip01 R Thigh",
                "Bip01 R Calf",
                "Bip01 R Foot",
                "Bip01 R Toe0",
            };

        private static HumanBone[] CreateBetterMscHumanBones() =>
            new[]
            {
                HumanBone("Hips", "Bip01 Pelvis"),
                HumanBone("Spine", "Bip01 Spine1"),
                HumanBone("Chest", "Bip01 Spine2"),
                HumanBone("Head", "HeadPivot"),
                HumanBone("LeftUpperLeg", "Bip01 L Thigh"),
                HumanBone("LeftLowerLeg", "Bip01 L Calf"),
                HumanBone("LeftFoot", "Bip01 L Foot"),
                HumanBone("LeftToes", "Bip01 L Toe0"),
                HumanBone("RightUpperLeg", "Bip01 R Thigh"),
                HumanBone("RightLowerLeg", "Bip01 R Calf"),
                HumanBone("RightFoot", "Bip01 R Foot"),
                HumanBone("RightToes", "Bip01 R Toe0"),
                HumanBone("LeftShoulder", "Bip01 L Clavicle"),
                HumanBone("LeftUpperArm", "Bip01 L UpperArm"),
                HumanBone("LeftLowerArm", "Bip01 L Forearm"),
                HumanBone("LeftHand", "Bip01 L Hand"),
                HumanBone("RightShoulder", "Bip01 R Clavicle"),
                HumanBone("RightUpperArm", "Bip01 R UpperArm"),
                HumanBone("RightLowerArm", "Bip01 R Forearm"),
                HumanBone("RightHand", "Bip01 R Hand"),
                HumanBone("LeftThumbProximal", "Bip01 L Finger0"),
                HumanBone("LeftThumbIntermediate", "Bip01 L Finger01"),
                HumanBone("LeftIndexProximal", "Bip01 L Finger1"),
                HumanBone("LeftIndexIntermediate", "Bip01 L Finger11"),
                HumanBone("LeftMiddleProximal", "Bip01 L Finger2"),
                HumanBone("LeftMiddleIntermediate", "Bip01 L Finger21"),
                HumanBone("LeftRingProximal", "Bip01 L Finger3"),
                HumanBone("LeftRingIntermediate", "Bip01 L Finger31"),
                HumanBone("LeftLittleProximal", "Bip01 L Finger4"),
                HumanBone("LeftLittleIntermediate", "Bip01 L Finger41"),
                HumanBone("RightThumbProximal", "Bip01 R Finger0"),
                HumanBone("RightThumbIntermediate", "Bip01 R Finger01"),
                HumanBone("RightIndexProximal", "Bip01 R Finger1"),
                HumanBone("RightIndexIntermediate", "Bip01 R Finger11"),
                HumanBone("RightMiddleProximal", "Bip01 R Finger2"),
                HumanBone("RightMiddleIntermediate", "Bip01 R Finger21"),
                HumanBone("RightRingProximal", "Bip01 R Finger3"),
                HumanBone("RightRingIntermediate", "Bip01 R Finger31"),
                HumanBone("RightLittleProximal", "Bip01 R Finger4"),
                HumanBone("RightLittleIntermediate", "Bip01 R Finger41"),
            };

        private static HumanBone HumanBone(
            string humanName,
            string donorBoneName) =>
            new HumanBone
            {
                humanName = humanName,
                boneName = donorBoneName,
                limit = new HumanLimit
                {
                    useDefaultValues = true,
                },
            };

        private static float CalibrateWheelContact(
            Transform wrapperRoot,
            Transform fixtureRoot,
            IReadOnlyList<Transform> wheelTransforms,
            float maximumCorrectionMeters)
        {
            if (wrapperRoot == null || fixtureRoot == null ||
                wheelTransforms == null || wheelTransforms.Count != 4 ||
                !float.IsFinite(maximumCorrectionMeters) ||
                maximumCorrectionMeters <= 0f ||
                maximumCorrectionMeters > 1f)
            {
                throw new InvalidOperationException(
                    "Story-traffic wheel calibration requires four wheels and a correction limit in (0, 1] meters.");
            }

            float minimumY = float.PositiveInfinity;
            foreach (Transform wheel in wheelTransforms)
            {
                Renderer[] renderers = wheel != null
                    ? wheel.GetComponentsInChildren<Renderer>(true)
                    : Array.Empty<Renderer>();
                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Story-traffic wheel calibration could not find a renderer below a locked wheel transform.");
                }

                foreach (Renderer renderer in renderers)
                {
                    Bounds bounds = renderer.bounds;
                    minimumY = Mathf.Min(
                        minimumY,
                        wrapperRoot.InverseTransformPoint(
                            new Vector3(
                                bounds.center.x,
                                bounds.min.y,
                                bounds.center.z)).y);
                }
            }

            float correction = -minimumY;
            if (!float.IsFinite(correction) ||
                Mathf.Abs(correction) > maximumCorrectionMeters)
            {
                throw new InvalidOperationException(
                    $"Story-traffic wheel correction {correction:F3} m exceeds the locked {maximumCorrectionMeters:F3} m limit.");
            }

            fixtureRoot.localPosition += Vector3.up * correction;
            return correction;
        }

        private static void SetConstantTransformCurve(
            AnimationClip clip,
            string path,
            string property,
            float value)
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    path,
                    typeof(Transform),
                    property),
                AnimationCurve.Constant(0f, 1f, value));
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            var serialized = new SerializedObject(clip);
            SerializedProperty settings = serialized.FindProperty(
                "m_AnimationClipSettings") ??
                throw new InvalidOperationException(
                    $"Animation settings are unavailable for '{clip.name}'.");
            settings.FindPropertyRelative("m_LoopTime").boolValue = loop;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void MergeCatalog(
            IReadOnlyList<CharacterPresentationCatalogEntry> storyEntries)
        {
            CharacterPresentationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                    CatalogPath) ?? throw new InvalidOperationException(
                    "Build the base Phase 1 character presentation before story traffic.");
            var replacementIds = new HashSet<string>(
                storyEntries.Select(entry => entry.BindingId),
                StringComparer.Ordinal);
            catalog.ConfigureForAuthoring(
                catalog.Entries
                    .Where(entry => !replacementIds.Contains(entry.BindingId))
                    .Concat(storyEntries));
            EditorUtility.SetDirty(catalog);
        }

        private static Dictionary<string, string> ImportAssets(
            IEnumerable<string> guids,
            IReadOnlyDictionary<string, string> sourceByGuid,
            string destinationRoot)
        {
            var result = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string guid in guids)
            {
                string source = RequireGuidPath(sourceByGuid, guid, "asset");
                string extension = Path.GetExtension(source).ToLowerInvariant();
                string destination = destinationRoot + "/" + guid + extension;
                CopyAssetAndMeta(
                    source,
                    destination,
                    guid,
                    DeriveGeneratedGuid("story-traffic:" + guid));
                result.Add(guid, destination);
            }

            return result;
        }

        private static Dictionary<string, string> BuildGuidIndex(
            string donorAssetsRoot)
        {
            var result = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string metaPath in Directory.EnumerateFiles(
                         donorAssetsRoot,
                         "*.meta",
                         SearchOption.AllDirectories))
            {
                using var reader = new StreamReader(metaPath);
                for (int index = 0; index < 8 && !reader.EndOfStream; index++)
                {
                    string line = reader.ReadLine();
                    if (line == null || !line.StartsWith(
                            "guid: ",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string guid = line.Substring(6).Trim();
                    string assetPath = metaPath.Substring(
                        0,
                        metaPath.Length - ".meta".Length);
                    result.TryAdd(guid, assetPath);
                    break;
                }
            }

            return result;
        }

        private static void MergeGuidIndex(
            IDictionary<string, string> destination,
            IReadOnlyDictionary<string, string> source)
        {
            foreach (KeyValuePair<string, string> pair in source)
            {
                if (destination.TryGetValue(pair.Key, out string existing) &&
                    !string.Equals(
                        existing,
                        pair.Value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Source GUID '{pair.Key}' resolves to both '{existing}' and '{pair.Value}'.");
                }

                destination[pair.Key] = pair.Value;
            }
        }

        private static string ImportLockedAsset(
            string sourcePath,
            string destinationAssetPath,
            string generatedIdentity)
        {
            string sourceGuid = ReadMetaGuid(sourcePath + ".meta");
            CopyAssetAndMeta(
                sourcePath,
                destinationAssetPath,
                sourceGuid,
                DeriveGeneratedGuid("story-traffic:" + generatedIdentity));
            return destinationAssetPath;
        }

        private static string ReadMetaGuid(string metaPath)
        {
            if (!File.Exists(metaPath))
            {
                throw new FileNotFoundException(
                    "Source metadata is missing.",
                    metaPath);
            }

            Match match = Regex.Match(
                File.ReadAllText(metaPath),
                @"^guid:\s*(?<value>[0-9a-fA-F]+)$",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            return match.Success
                ? match.Groups["value"].Value.ToLowerInvariant()
                : throw new InvalidDataException(
                    $"Source metadata '{metaPath}' contains no GUID.");
        }

        private static SourceMaterialSpec ReadMaterialSpec(string path)
        {
            string yaml = File.ReadAllText(path);
            string textureGuid = MatchValue(
                yaml,
                @"name:\s*_MainTex[\s\S]*?m_Texture:\s*\{fileID:\s*-?\d+(?:,\s*guid:\s*(?<value>[0-9a-fA-F]+))?");
            Color color = MatchColor(yaml, "_Color", Color.white);
            float metallic = MatchFloat(yaml, "_Metallic", 0f);
            float smoothness = MatchFloat(yaml, "_Glossiness", 0.25f);
            int renderQueue = (int)MatchFloat(
                yaml,
                "m_CustomRenderQueue",
                -1f,
                directField: true);
            float destinationBlend = MatchFloat(yaml, "_DstBlend", 0f);
            float zWrite = MatchFloat(yaml, "_ZWrite", 1f);
            bool transparent = renderQueue >= 3000 ||
                               Mathf.Approximately(destinationBlend, 10f) ||
                               Mathf.Approximately(zWrite, 0f);
            bool alphaClipping = Path.GetFileNameWithoutExtension(path)
                .IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0;
            return new SourceMaterialSpec(
                textureGuid,
                color,
                Mathf.Clamp01(metallic),
                Mathf.Clamp01(smoothness),
                transparent,
                alphaClipping,
                Mathf.Clamp01(MatchFloat(yaml, "_Cutoff", 0.3f)));
        }

        private static Material CreateMaterial(
            string sourceGuid,
            SourceMaterialSpec spec,
            IReadOnlyDictionary<string, string> importedTextures)
        {
            Shader shader = Shader.Find("HDRP/Lit") ??
                throw new InvalidOperationException(
                    "HDRP/Lit shader is unavailable.");
            var material = new Material(shader)
            {
                name = "Phase 1 Story Traffic " + sourceGuid,
                enableInstancing = true,
            };
            if (!string.IsNullOrEmpty(spec.MainTextureGuid))
            {
                material.SetTexture(
                    "_BaseColorMap",
                    RequireAsset<Texture2D>(
                        importedTextures[spec.MainTextureGuid]));
            }

            material.SetColor("_BaseColor", spec.BaseColor);
            material.SetFloat("_Metallic", spec.Metallic);
            material.SetFloat("_Smoothness", spec.Smoothness);
            material.SetFloat("_SurfaceType", spec.Transparent ? 1f : 0f);
            material.SetFloat("_BlendMode", 0f);
            material.SetFloat("_ZWrite", spec.Transparent ? 0f : 1f);
            SetFloatIfPresent(material, "_SrcBlend", 1f);
            SetFloatIfPresent(
                material,
                "_DstBlend",
                spec.Transparent ? 10f : 0f);
            if (spec.Transparent)
            {
                material.renderQueue = (int)RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                material.renderQueue = (int)RenderQueue.Geometry;
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            material.SetFloat("_AlphaCutoffEnable", spec.AlphaClipping ? 1f : 0f);
            material.SetFloat("_AlphaCutoff", spec.AlphaCutoff);
            if (spec.AlphaClipping)
            {
                material.EnableKeyword("_ALPHATEST_ON");
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
            }

            string path = MaterialRoot + "/" + sourceGuid + ".mat";
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static string MatchValue(string text, string pattern)
        {
            Match match = Regex.Match(
                text,
                pattern,
                RegexOptions.CultureInvariant);
            return match.Success
                ? match.Groups["value"].Value.ToLowerInvariant()
                : string.Empty;
        }

        private static Color MatchColor(
            string text,
            string property,
            Color fallback)
        {
            Match match = Regex.Match(
                text,
                @"name:\s*" + Regex.Escape(property) +
                @"\s*\r?\n\s*second:\s*\{r:\s*(?<r>[^,]+),\s*g:\s*(?<g>[^,]+),\s*b:\s*(?<b>[^,]+),\s*a:\s*(?<a>[^}]+)\}",
                RegexOptions.CultureInvariant);
            return match.Success
                ? new Color(
                    ParseFloat(match.Groups["r"].Value),
                    ParseFloat(match.Groups["g"].Value),
                    ParseFloat(match.Groups["b"].Value),
                    ParseFloat(match.Groups["a"].Value))
                : fallback;
        }

        private static float MatchFloat(
            string text,
            string property,
            float fallback,
            bool directField = false)
        {
            string pattern = directField
                ? @"^\s*" + Regex.Escape(property) +
                  @":\s*(?<value>[^\r\n]+)"
                : @"name:\s*" + Regex.Escape(property) +
                  @"\s*\r?\n\s*second:\s*(?<value>[^\r\n]+)";
            Match match = Regex.Match(
                text,
                pattern,
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            return match.Success && float.TryParse(
                match.Groups["value"].Value.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value)
                    ? value
                    : fallback;
        }

        private static float ParseFloat(string value) => float.Parse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);

        private static void ConfigureTexture(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as
                TextureImporter ?? throw new InvalidOperationException(
                    $"Texture importer is unavailable for '{assetPath}'.");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void SetFloatIfPresent(
            Material material,
            string property,
            float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static Transform RequireTransform(
            IReadOnlyDictionary<long, Transform> transforms,
            long transformId) =>
            transformId != 0 && transforms.TryGetValue(
                transformId,
                out Transform transform)
                    ? transform
                    : throw new InvalidOperationException(
                        $"Story-traffic slice is missing transform {transformId}.");

        private static string RequireGuidPath(
            IReadOnlyDictionary<string, string> sourceByGuid,
            string guid,
            string kind) =>
            !string.IsNullOrWhiteSpace(guid) &&
            sourceByGuid.TryGetValue(guid, out string path) &&
            File.Exists(path)
                ? path
                : throw new FileNotFoundException(
                    $"Locked donor {kind} GUID '{guid}' was not found below staging.");

        private static void ResetOutput()
        {
            string path = ToFileSystemPath(OutputRoot);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            Directory.CreateDirectory(ToFileSystemPath(SourceRoot));
            Directory.CreateDirectory(ToFileSystemPath(GeneratedRoot));
        }

        private static void EnsureAssetFolder(string path)
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

        private static void CopyAssetAndMeta(
            string source,
            string destinationAssetPath,
            string sourceGuid,
            string generatedGuid)
        {
            if (!File.Exists(source) || !File.Exists(source + ".meta"))
            {
                throw new FileNotFoundException(
                    "Locked donor asset or metadata is missing.",
                    source);
            }

            string destination = ToFileSystemPath(destinationAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, overwrite: true);
            string meta = File.ReadAllText(source + ".meta");
            string sourceLine = "guid: " + sourceGuid;
            if (!meta.Contains(sourceLine))
            {
                throw new InvalidDataException(
                    $"Donor metadata for '{source}' does not contain GUID {sourceGuid}.");
            }

            File.WriteAllText(
                destination + ".meta",
                meta.Replace(
                    sourceLine,
                    "guid: " + generatedGuid),
                new UTF8Encoding(false));
        }

        private static string DeriveGeneratedGuid(string value)
        {
            using var sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(
                Encoding.UTF8.GetBytes(value ?? string.Empty));
            return string.Concat(digest.Take(16).Select(item =>
                item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidOperationException(
                $"Unity could not import '{path}' as {typeof(T).Name}.");

        private static void RequireHash(
            string path,
            string expected,
            string label)
        {
            string actual = ComputeHash(path);
            if (!string.Equals(
                    actual,
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Locked {label} hash mismatch. Expected {expected}, got {actual}.");
            }
        }

        private static string ComputeHash(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Required file is missing.",
                    path);
            }

            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(stream).Select(value =>
                value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void WriteReport(
            Manifest manifest,
            string scenePath,
            IReadOnlyList<FixtureSource> fixtures,
            IReadOnlyList<AmbientFixtureSource> ambientFixtures,
            IReadOnlyList<TransportFixtureSource> transportFixtures,
            IReadOnlyList<string> meshGuids,
            IReadOnlyList<string> materialGuids,
            IReadOnlyList<string> textureGuids,
            string betterPrefabPath,
            string betterAvatarPath,
            string betterSitClipPath,
            string betterStandClipPath)
        {
            string json = JsonUtility.ToJson(new BuildReportData
            {
                schemaVersion = manifest.schemaVersion,
                manifestId = manifest.manifestId,
                classification = manifest.classification,
                sourceSceneSha256 = ComputeHash(scenePath),
                betterMscSuskiPrefabSha256 = ComputeHash(betterPrefabPath),
                betterMscSuskiAvatarSha256 = ComputeHash(betterAvatarPath),
                betterMscSuskiVehicleSitClipSha256 = ComputeHash(
                    betterSitClipPath),
                betterMscSuskiStandClipSha256 = ComputeHash(
                    betterStandClipPath),
                sourceManifestSha256 = ComputeHash(
                    ToFileSystemPath(ManifestPath)),
                vehicleCount = fixtures.Count,
                ambientVehicleArchetypeCount = ambientFixtures.Count,
                transportVehicleArchetypeCount = transportFixtures.Count,
                skinnedRendererCount = fixtures.Sum(value =>
                    value.SkinnedRenderers.Length) + 1 +
                    ambientFixtures.Sum(value =>
                        value.SkinnedRenderers.Length) +
                    transportFixtures.Sum(value =>
                        value.SkinnedRenderers.Length),
                staticRendererCount = fixtures.Sum(value =>
                    value.StaticRenderers.Length) +
                    ambientFixtures.Sum(value =>
                        value.StaticRenderers.Length) +
                    transportFixtures.Sum(value =>
                        value.StaticRenderers.Length),
                meshCount = meshGuids.Count,
                materialCount = materialGuids.Count,
                textureCount = textureGuids.Count,
                excludesDonorScriptsFsmControllers = true,
                excludesDonorShaders = true,
                excludesDonorVehiclePhysics = true,
            }, prettyPrint: true) + Environment.NewLine;
            File.WriteAllText(
                ToFileSystemPath(BuildReportPath),
                json,
                new UTF8Encoding(false));
        }

        private static Manifest LoadManifest()
        {
            string path = ToFileSystemPath(ManifestPath);
            Manifest manifest = File.Exists(path)
                ? JsonUtility.FromJson<Manifest>(File.ReadAllText(path))
                : null;
            if (manifest == null || manifest.schemaVersion != 13 ||
                manifest.source == null ||
                manifest.betterMscSuski == null ||
                manifest.vehicles == null ||
                manifest.vehicles.Length != 2 ||
                !TryValidateAmbientManifest(manifest.ambientVehicles) ||
                !TryValidateTransportManifest(manifest.transports) ||
                string.IsNullOrWhiteSpace(
                    manifest.betterMscSuski.stagingRootRelativePath) ||
                string.IsNullOrWhiteSpace(
                    manifest.betterMscSuski.prefabRelativePath) ||
                string.IsNullOrWhiteSpace(
                    manifest.betterMscSuski.prefabSha256) ||
                manifest.betterMscSuski.rootTransformFileId <= 0L ||
                manifest.betterMscSuski
                    .skinnedRendererComponentFileId <= 0L ||
                string.IsNullOrWhiteSpace(
                    manifest.betterMscSuski.avatarRelativePath) ||
                string.IsNullOrWhiteSpace(
                    manifest.betterMscSuski.avatarSha256) ||
                string.IsNullOrWhiteSpace(
                    manifest.betterMscSuski.vehicleSitClipRelativePath) ||
                string.IsNullOrWhiteSpace(
                    manifest.betterMscSuski.vehicleSitClipSha256) ||
                string.IsNullOrWhiteSpace(
                    manifest.betterMscSuski.standClipRelativePath) ||
                string.IsNullOrWhiteSpace(
                    manifest.betterMscSuski.standClipSha256) ||
                manifest.vehicles.Any(spec =>
                    string.IsNullOrWhiteSpace(spec.id) ||
                    string.IsNullOrWhiteSpace(spec.bindingId) ||
                    spec.rootTransformFileId <= 0 ||
                    spec.animationTargetTransformFileId <= 0 ||
                     spec.skinnedRendererComponentFileIds == null ||
                     spec.skinnedRendererComponentFileIds.Length == 0 ||
                     spec.excludedStaticRendererComponentFileIds == null ||
                     spec.excludedStaticRendererComponentFileIds.Any(id =>
                         id <= 0L) ||
                     spec.excludedStaticRendererComponentFileIds.Distinct()
                         .Count() !=
                     spec.excludedStaticRendererComponentFileIds.Length ||
                     spec.auxiliaryTransformFileIds == null ||
                     (spec.betterMscPassengerAnchorTransformFileId > 0L &&
                      (spec.donorPassengerPelvisTransformFileId <= 0L ||
                       spec.donorPassengerLeftAnkleTransformFileId <= 0L ||
                       spec.donorPassengerRightAnkleTransformFileId <= 0L)) ||
                     spec.staticRendererRootTransformFileIds == null ||
                    spec.staticRendererRootTransformFileIds.Length == 0 ||
                    spec.wheelTransformFileIds == null ||
                    spec.wheelTransformFileIds.Length != 4 ||
                    !float.IsFinite(spec.wheelDegreesPerMeter) ||
                    spec.wheelDegreesPerMeter <= 0f ||
                    !float.IsFinite(spec.maximumGroundCorrectionMeters) ||
                    spec.maximumGroundCorrectionMeters <= 0f ||
                    spec.maximumGroundCorrectionMeters > 1f))
            {
                throw new InvalidDataException(
                    "Phase 1 story-traffic presentation manifest is malformed.");
            }

            return manifest;
        }

        private static string ToFileSystemPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                assetPath.Replace('/', Path.DirectorySeparatorChar)));

        [Serializable]
        private sealed class Manifest
        {
            public int schemaVersion;
            public string manifestId;
            public string classification;
            public SourceSpec source;
            public BetterMscSuskiSpec betterMscSuski;
            public VehicleSpec[] vehicles;
            public AmbientVehicleSpec[] ambientVehicles;
            public TransportVehicleSpec[] transports;
        }

        [Serializable]
        private sealed class SourceSpec
        {
            public string stagingRootRelativePath;
            public string sceneRelativePath;
            public string sceneSha256;
        }

        [Serializable]
        private sealed class BetterMscSuskiSpec
        {
            public string stagingRootRelativePath;
            public string prefabRelativePath;
            public string prefabSha256;
            public long rootTransformFileId;
            public long skinnedRendererComponentFileId;
            public string avatarRelativePath;
            public string avatarSha256;
            public string vehicleSitClipRelativePath;
            public string vehicleSitClipSha256;
            public string standClipRelativePath;
            public string standClipSha256;
        }

        [Serializable]
        private sealed class VehicleSpec
        {
            public string id;
            public string bindingId;
            public string productionReplacementKey;
            public string driverFeatureId;
            public string[] passengerFeatureIds;
            public long rootTransformFileId;
            public long animationTargetTransformFileId;
            public long[] skinnedRendererComponentFileIds;
            public long[] staticRendererRootTransformFileIds;
            public long[] excludedStaticRendererComponentFileIds;
            public long[] auxiliaryTransformFileIds;
            public long betterMscPassengerAnchorTransformFileId;
            public long donorPassengerPelvisTransformFileId;
            public long donorPassengerLeftAnkleTransformFileId;
            public long donorPassengerRightAnkleTransformFileId;
            public long[] wheelTransformFileIds;
            public float wheelDegreesPerMeter;
            public float maximumGroundCorrectionMeters;
            public bool isAmbientTraffic;
            public float provisionalMassKilograms;
        }

        private sealed class FixtureSource
        {
            public FixtureSource(
                VehicleSpec spec,
                DonorSkinnedRendererRecord[] skinnedRenderers,
                DonorStaticRendererRecord[] staticRenderers,
                DonorStaticRendererRecord[] passengerReferenceRenderers)
            {
                Spec = spec;
                SkinnedRenderers = skinnedRenderers;
                StaticRenderers = staticRenderers;
                PassengerReferenceRenderers = passengerReferenceRenderers;
            }

            public VehicleSpec Spec { get; }
            public DonorSkinnedRendererRecord[] SkinnedRenderers { get; }
            public DonorStaticRendererRecord[] StaticRenderers { get; }
            public DonorStaticRendererRecord[] PassengerReferenceRenderers
            {
                get;
            }
        }

        private readonly struct SourceMaterialSpec
        {
            public SourceMaterialSpec(
                string mainTextureGuid,
                Color baseColor,
                float metallic,
                float smoothness,
                bool transparent,
                bool alphaClipping,
                float alphaCutoff)
            {
                MainTextureGuid = mainTextureGuid ?? string.Empty;
                BaseColor = baseColor;
                Metallic = metallic;
                Smoothness = smoothness;
                Transparent = transparent;
                AlphaClipping = alphaClipping;
                AlphaCutoff = alphaCutoff;
            }

            public string MainTextureGuid { get; }
            public Color BaseColor { get; }
            public float Metallic { get; }
            public float Smoothness { get; }
            public bool Transparent { get; }
            public bool AlphaClipping { get; }
            public float AlphaCutoff { get; }
        }

        private readonly struct PoseSample
        {
            public PoseSample(
                string path,
                Vector3 position,
                Quaternion rotation,
                Vector3 scale)
            {
                Path = path ?? string.Empty;
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            public string Path { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }
        }

        private readonly struct DonorPassengerPoseReference
        {
            public DonorPassengerPoseReference(
                Vector3 pelvis,
                Vector3 forward)
            {
                Pelvis = pelvis;
                Forward = forward;
            }

            public Vector3 Pelvis { get; }
            public Vector3 Forward { get; }
        }

        [Serializable]
        private sealed class BuildReportData
        {
            public int schemaVersion;
            public string manifestId;
            public string classification;
            public string sourceSceneSha256;
            public string betterMscSuskiPrefabSha256;
            public string betterMscSuskiAvatarSha256;
            public string betterMscSuskiVehicleSitClipSha256;
            public string betterMscSuskiStandClipSha256;
            public string sourceManifestSha256;
            public int vehicleCount;
            public int ambientVehicleArchetypeCount;
            public int transportVehicleArchetypeCount;
            public int skinnedRendererCount;
            public int staticRendererCount;
            public int meshCount;
            public int materialCount;
            public int textureCount;
            public bool excludesDonorScriptsFsmControllers;
            public bool excludesDonorShaders;
            public bool excludesDonorVehiclePhysics;
        }
    }
}
