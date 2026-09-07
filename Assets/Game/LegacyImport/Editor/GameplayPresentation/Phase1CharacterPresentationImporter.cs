using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Characters;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Builds reviewed private Phase 1 character presentation wrappers. Only
    /// hash-locked meshes, textures and clips cross the donor boundary; the
    /// generated prefabs contain project-owned HDRP materials, wrappers,
    /// Transform, Animation and renderer data.
    /// </summary>
    public static class Phase1CharacterPresentationImporter
    {
        private const string ConfigurationPath =
            "Config/DonorPaths.local.json";
        private const string ManifestPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1CharacterPresentationManifest.json";
        private const string OutputRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Characters";
        private const string SourceRoot = OutputRoot + "/Source";
        private const string GeneratedRoot = OutputRoot + "/Generated";
        private const string ResourcesRoot = OutputRoot +
            "/Resources/Phase1Characters";
        private const string CatalogPath = ResourcesRoot +
            "/CharacterPresentationCatalog.asset";
        private const string MaterialRoot = GeneratedRoot + "/Materials";
        private const string BuildReportPath = OutputRoot +
            "/Phase1CharacterPresentationBuildReport.json";

        [MenuItem(
            "Tools/My Summer Car/Legacy Import/" +
            "Build Phase 1 Character Presentation Fixtures")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Phase 1 character presentation",
                "The sanitized private Phase 1 character wrappers were rebuilt.",
                "OK");
        }

        public static void BuildFromBatch() => Build();

        public static void Build()
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

            ResetOutput();
            var imported = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (AssetSpec spec in manifest.assets)
            {
                string source = Path.Combine(
                    donorAssetsRoot,
                    spec.relativePath.Replace('/', Path.DirectorySeparatorChar));
                RequireHash(source, spec.sha256, spec.role);
                string destination = SourceRoot + "/" + spec.relativePath;
                CopyAssetAndMeta(
                    source,
                    destination,
                    spec.guid,
                    spec.generatedGuid);
                imported.Add(spec.role, destination);
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            EnsureAssetFolder(GeneratedRoot);
            EnsureAssetFolder(MaterialRoot);
            EnsureAssetFolder(ResourcesRoot);
            foreach (AssetSpec spec in manifest.assets)
            {
                string actualGuid = AssetDatabase.AssetPathToGUID(imported[spec.role]);
                if (!string.Equals(
                        actualGuid,
                        spec.generatedGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Imported GUID mismatch for '{spec.role}'. Expected project GUID {spec.generatedGuid}, got {actualGuid}.");
                }
            }

            var clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (AssetSpec spec in manifest.assets.Where(candidate =>
                         candidate.relativePath.EndsWith(
                             ".anim",
                             StringComparison.OrdinalIgnoreCase)))
            {
                AnimationClip clip = RequireAsset<AnimationClip>(imported[spec.role]);
                clip.legacy = true;
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                SetLoop(clip, true);
                EditorUtility.SetDirty(clip);
                clips.Add(spec.role, clip);
            }

            foreach (AssetSpec spec in manifest.assets.Where(candidate =>
                         candidate.relativePath.EndsWith(
                             ".png",
                             StringComparison.OrdinalIgnoreCase)))
            {
                ConfigureLegacyTexture(imported[spec.role]);
            }

            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (MaterialSpec spec in manifest.materials)
            {
                Texture2D texture = string.IsNullOrWhiteSpace(spec.textureRole)
                    ? null
                    : RequireAsset<Texture2D>(imported[spec.textureRole]);
                materials.Add(spec.role, CreateLegacyMaterial(spec, texture));
            }

            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(scenePath);
            var entries = new List<CharacterPresentationCatalogEntry>();
            foreach (FixtureSpec fixture in manifest.fixtures)
            {
                AssetSpec meshSpec = manifest.assets.Single(candidate =>
                    string.Equals(candidate.role, fixture.meshRole, StringComparison.Ordinal));
                Mesh mesh = RequireAsset<Mesh>(imported[fixture.meshRole]);
                Material[] fixtureMaterials = fixture.materialRoles
                    .Select(role => materials[role])
                    .ToArray();
                string[] sourceMaterialGuids = fixture.materialRoles
                    .Select(role => manifest.materials.Single(material =>
                        string.Equals(
                            material.role,
                            role,
                            StringComparison.Ordinal)).sourceMaterialGuid)
                    .ToArray();
                CharacterAnimationBinding[] stateBindings = fixture.stateBindings
                    .Select(binding => new CharacterAnimationBinding(
                        (CharacterActivityState)Enum.Parse(
                            typeof(CharacterActivityState),
                            binding.activityState,
                            ignoreCase: false),
                        clips[binding.clipRole],
                        binding.loop))
                    .ToArray();
                CharacterActionAnimationBinding[] actionBindings =
                    (fixture.actionBindings ??
                     Array.Empty<ActionAnimationBindingSpec>())
                    .Select(binding => new CharacterActionAnimationBinding(
                        binding.actionId,
                        clips[binding.clipRole],
                        binding.loop))
                    .ToArray();
                LayeredAnimationBuildData[] layeredAnimations =
                    (fixture.layeredAnimations ??
                     Array.Empty<LayeredAnimationSpec>())
                    .Select(layered => new LayeredAnimationBuildData(
                        layered.targetTransformFileId,
                        clips[layered.clipRole],
                        layered.startTimeSeconds))
                    .ToArray();
                AccessoryBuildData[] accessories =
                    (fixture.accessories ?? Array.Empty<AccessorySpec>())
                    .Select(accessory =>
                    {
                        AssetSpec accessoryMeshSpec = manifest.assets.Single(
                            candidate => string.Equals(
                                candidate.role,
                                accessory.meshRole,
                                StringComparison.Ordinal));
                        return new AccessoryBuildData(
                            accessory.rendererComponentFileId,
                            string.IsNullOrWhiteSpace(
                                accessory.sourceMeshGuidOverride)
                                    ? accessoryMeshSpec.guid
                                    : accessory.sourceMeshGuidOverride,
                            RequireAsset<Mesh>(imported[accessory.meshRole]),
                            accessory.materialRoles
                                .Select(role => materials[role])
                                .ToArray(),
                            accessory.materialRoles
                                .Select(role => manifest.materials.Single(material =>
                                    string.Equals(
                                        material.role,
                                        role,
                                        StringComparison.Ordinal))
                                    .sourceMaterialGuid)
                                .ToArray(),
                            accessory.visibilityFlagId);
                    })
                    .ToArray();
                AccessoryBuildData[] contextProps =
                    (fixture.contextProps ?? Array.Empty<AccessorySpec>())
                    .Select(accessory =>
                    {
                        AssetSpec accessoryMeshSpec = manifest.assets.Single(
                            candidate => string.Equals(
                                candidate.role,
                                accessory.meshRole,
                                StringComparison.Ordinal));
                        return new AccessoryBuildData(
                            accessory.rendererComponentFileId,
                            string.IsNullOrWhiteSpace(
                                accessory.sourceMeshGuidOverride)
                                    ? accessoryMeshSpec.guid
                                    : accessory.sourceMeshGuidOverride,
                            RequireAsset<Mesh>(imported[accessory.meshRole]),
                            accessory.materialRoles
                                .Select(role => materials[role])
                                .ToArray(),
                            accessory.materialRoles
                                .Select(role => manifest.materials.Single(material =>
                                    string.Equals(
                                        material.role,
                                        role,
                                        StringComparison.Ordinal))
                                    .sourceMaterialGuid)
                                .ToArray(),
                            accessory.visibilityFlagId);
                    })
                    .ToArray();
                string prefabPath = ResourcesRoot + "/" + fixture.id + ".prefab";
                GameObject prefab = BuildFixture(
                    scene,
                    fixture,
                    meshSpec.guid,
                    mesh,
                    fixtureMaterials,
                    sourceMaterialGuids,
                    accessories,
                    contextProps,
                    stateBindings,
                    actionBindings,
                    layeredAnimations,
                    prefabPath);
                entries.Add(new CharacterPresentationCatalogEntry(
                    fixture.bindingId,
                    fixture.productionReplacementKey,
                    prefab));
            }

            var catalog = ScriptableObject.CreateInstance<
                CharacterPresentationCatalog>();
            catalog.ConfigureForAuthoring(entries);
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            WriteReport(manifest, scenePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureGeneratedForBuild();
            Debug.Log(
                $"Phase 1 character presentation complete: {manifest.fixtures.Length} " +
                "textured TemporaryDirectImport wrappers, no donor scripts/FSMs/controllers/shaders.");
        }

        public static void EnsureGeneratedForBuild()
        {
            Manifest manifest = LoadManifest();
            string reportFile = ToFileSystemPath(BuildReportPath);
            if (!File.Exists(reportFile))
            {
                throw new InvalidOperationException(
                    "Generated Phase 1 character presentation report is missing.");
            }

            BuildReportData report = JsonUtility.FromJson<BuildReportData>(
                File.ReadAllText(reportFile));
            string currentManifestHash = ComputeHash(
                ToFileSystemPath(ManifestPath));
            if (report == null || report.schemaVersion != manifest.schemaVersion ||
                !string.Equals(
                    report.sourceManifestSha256,
                    currentManifestHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Generated Phase 1 character presentation was built from a stale manifest.");
            }

            CharacterPresentationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    "Generated Phase 1 character presentation catalog is missing.");
            }

            IReadOnlyList<string> failures = catalog.ValidateConfiguration();
            if (failures.Count > 0 || catalog.Entries.Count < manifest.fixtures.Length)
            {
                throw new InvalidOperationException(
                    "Generated Phase 1 character presentation is stale or invalid: " +
                    string.Join(" | ", failures));
            }

            foreach (FixtureSpec fixture in manifest.fixtures)
            {
                bool found = catalog.TryGet(fixture.bindingId, out var entry);
                LegacyCharacterPresentationBinding binding = found &&
                    entry.WrapperPrefab != null
                        ? entry.WrapperPrefab.GetComponent<
                            LegacyCharacterPresentationBinding>()
                        : null;
                bool expectsConditionalPresentation =
                    (fixture.accessories ?? Array.Empty<AccessorySpec>())
                        .Concat(fixture.contextProps ??
                            Array.Empty<AccessorySpec>())
                        .Any(accessory => !string.IsNullOrWhiteSpace(
                            accessory.visibilityFlagId));
                CharacterFlagPresentationBinding conditional = found &&
                    entry.WrapperPrefab != null
                        ? entry.WrapperPrefab.GetComponent<
                            CharacterFlagPresentationBinding>()
                        : null;
                bool conditionalValid = conditional != null &&
                    conditional.TryValidate(out _);
                // The catalog deliberately replaces only default Suski with the
                // approved StoryTraffic wrapper. Its materials have a separate
                // source contract; all baseline structural checks still apply.
                bool usesValidatedSuskiOverride = found &&
                    Phase1StoryTrafficPresentationImporter
                        .TryValidateDefaultSuskiOverrideForBuild(
                            fixture.bindingId,
                            entry.WrapperPrefab);
                if (!found || entry.WrapperPrefab == null ||
                    binding == null ||
                    !string.Equals(binding.GeometryReplacementKey,
                        fixture.geometryReplacementKey,
                        StringComparison.Ordinal) ||
                    !string.Equals(binding.MaterialReplacementKey,
                        fixture.materialReplacementKey,
                        StringComparison.Ordinal) ||
                    !string.Equals(binding.AnimationReplacementKey,
                        fixture.animationReplacementKey,
                        StringComparison.Ordinal) ||
                    entry.WrapperPrefab.GetComponentsInChildren<Animator>(true).Length != 0 ||
                     entry.WrapperPrefab
                         .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Any(renderer =>
                            renderer.sharedMaterials.Length != renderer.sharedMesh.subMeshCount ||
                            renderer.sharedMaterials.Any(material =>
                                 !usesValidatedSuskiOverride &&
                                 !IsGeneratedMaterialValid(material, manifest))) ||
                    entry.WrapperPrefab
                        .GetComponentsInChildren<MeshRenderer>(true)
                        .Any(renderer =>
                        {
                            MeshFilter filter = renderer.GetComponent<MeshFilter>();
                            return filter == null ||
                                   filter.sharedMesh == null ||
                                   renderer.sharedMaterials.Length !=
                                   filter.sharedMesh.subMeshCount ||
                                   renderer.sharedMaterials.Any(material =>
                                       !IsGeneratedMaterialValid(material, manifest));
                        }) ||
                    entry.WrapperPrefab
                        .GetComponentsInChildren<MeshRenderer>(true).Length !=
                    (fixture.accessories?.Length ?? 0) +
                    (fixture.contextProps?.Length ?? 0) ||
                    (conditional != null) !=
                        expectsConditionalPresentation ||
                    (conditional != null && !conditionalValid) ||
                    entry.WrapperPrefab
                        .GetComponentsInChildren<Animation>(true).Length !=
                    1 + (fixture.layeredAnimations?.Length ?? 0) ||
                    entry.WrapperPrefab
                        .GetComponentsInChildren<LegacyLoopingAnimationLayer>(
                            true).Length !=
                    (fixture.layeredAnimations?.Length ?? 0) ||
                    entry.WrapperPrefab
                        .GetComponentsInChildren<LegacyLoopingAnimationLayer>(
                            true)
                        .Any(candidate => !candidate.TryValidate(out _)) ||
                    !HasValidBicycleMotion(entry.WrapperPrefab, fixture))
                {
                    throw new InvalidOperationException(
                        $"Generated character fixture '{fixture.id}' is missing or retained an Animator.");
                }
            }
        }

        private static GameObject BuildFixture(
            DonorUnitySceneModel scene,
            FixtureSpec fixture,
            string meshGuid,
            Mesh mesh,
            Material[] materials,
            IReadOnlyList<string> expectedMaterialGuids,
            AccessoryBuildData[] accessories,
            AccessoryBuildData[] contextProps,
            CharacterAnimationBinding[] stateBindings,
            CharacterActionAnimationBinding[] actionBindings,
            LayeredAnimationBuildData[] layeredAnimations,
            string prefabPath)
        {
            if (materials == null || materials.Length != mesh.subMeshCount ||
                materials.Any(material => material == null))
            {
                throw new InvalidOperationException(
                    $"Character fixture '{fixture.id}' requires exactly {mesh.subMeshCount} non-null material slots; got {materials?.Length ?? 0}.");
            }

            if (stateBindings == null || stateBindings.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Character fixture '{fixture.id}' has no project-owned animation state bindings.");
            }

            DonorActionSlice slice = scene.CreateHandActionSlice(
                fixture.rootTransformFileId,
                fixture.animationTargetTransformFileId,
                meshGuid,
                fixture.rendererComponentFileIds,
                accessories.Select(accessory =>
                    accessory.RendererComponentFileId).ToArray());
            foreach (DonorSkinnedRendererRecord renderer in slice.Renderers)
            {
                if (!renderer.MaterialGuids.SequenceEqual(
                        expectedMaterialGuids,
                        StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Character fixture '{fixture.id}' material slots do not match locked renderer {renderer.ComponentId}. Expected [{string.Join(", ", expectedMaterialGuids)}], donor has [{string.Join(", ", renderer.MaterialGuids)}].");
                }
            }

            var root = new GameObject("Phase 1 Character " + fixture.id);
            try
            {
                var created = new Dictionary<long, Transform>();
                foreach (DonorTransformRecord source in slice.Transforms)
                {
                    Transform parent = created.TryGetValue(
                        source.FatherTransformId,
                        out Transform createdParent)
                        ? createdParent
                        : root.transform;
                    var node = new GameObject(scene.GetGameObjectName(source.GameObjectId));
                    node.transform.SetParent(parent, false);
                    bool isFixtureRoot =
                        source.TransformId == slice.RootTransformId;
                    node.transform.localPosition = isFixtureRoot &&
                        fixture.rootLocalPositionOverride?.Length == 3
                            ? new Vector3(
                                fixture.rootLocalPositionOverride[0],
                                fixture.rootLocalPositionOverride[1],
                                fixture.rootLocalPositionOverride[2])
                            : source.LocalPosition;
                    node.transform.localRotation = isFixtureRoot &&
                        fixture.rootLocalRotationOverride?.Length == 4
                            ? new Quaternion(
                                fixture.rootLocalRotationOverride[0],
                                fixture.rootLocalRotationOverride[1],
                                fixture.rootLocalRotationOverride[2],
                                fixture.rootLocalRotationOverride[3])
                            : source.LocalRotation;
                    node.transform.localScale = source.LocalScale;
                    created.Add(source.TransformId, node.transform);
                }

                foreach (DonorSkinnedRendererRecord source in slice.Renderers)
                {
                    DonorTransformRecord rendererTransform =
                        slice.Transforms.First(candidate =>
                            candidate.GameObjectId == source.GameObjectId);
                    SkinnedMeshRenderer renderer =
                        created[rendererTransform.TransformId].gameObject
                            .AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = mesh;
                    renderer.sharedMaterials = materials;
                    renderer.rootBone = RequireTransform(created, source.RootBoneTransformId);
                    renderer.bones = source.BoneTransformIds
                        .Select(id => RequireTransform(created, id))
                        .ToArray();
                    renderer.localBounds = source.LocalBounds;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                var staticRendererTransforms =
                    new Dictionary<long, Transform>();
                var conditionalProps = new List<
                    CharacterFlagVisibilityBinding>();
                foreach (DonorStaticRendererRecord source in
                         slice.StaticRenderers)
                {
                    AccessoryBuildData accessory = accessories.Single(candidate =>
                        candidate.RendererComponentFileId == source.ComponentId);
                    if (!string.Equals(
                            source.MeshGuid,
                            accessory.SourceMeshGuid,
                            StringComparison.OrdinalIgnoreCase) ||
                        !source.MaterialGuids.SequenceEqual(
                            accessory.SourceMaterialGuids,
                            StringComparer.OrdinalIgnoreCase) ||
                        accessory.Materials.Length != accessory.Mesh.subMeshCount)
                    {
                        throw new InvalidOperationException(
                            $"Character fixture '{fixture.id}' accessory renderer {source.ComponentId} does not match its locked mesh/material slots.");
                    }

                    DonorTransformRecord rendererTransform =
                        slice.Transforms.First(candidate =>
                            candidate.GameObjectId == source.GameObjectId);
                    GameObject owner =
                        created[rendererTransform.TransformId].gameObject;
                    MeshFilter filter = owner.AddComponent<MeshFilter>();
                    filter.sharedMesh = accessory.Mesh;
                    MeshRenderer renderer = owner.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = accessory.Materials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    staticRendererTransforms.Add(
                        source.ComponentId,
                        owner.transform);
                    AddConditionalProp(
                        conditionalProps,
                        accessory,
                        owner);
                }

                Transform fixtureRoot = created[fixture.rootTransformFileId];
                foreach (AccessoryBuildData contextProp in contextProps)
                {
                    DonorStaticRendererRecord source = scene.GetStaticRenderer(
                        contextProp.RendererComponentFileId);
                    if (!string.Equals(
                            source.MeshGuid,
                            contextProp.SourceMeshGuid,
                            StringComparison.OrdinalIgnoreCase) ||
                        !source.MaterialGuids.SequenceEqual(
                            contextProp.SourceMaterialGuids,
                            StringComparer.OrdinalIgnoreCase) ||
                        contextProp.Materials.Length !=
                        contextProp.Mesh.subMeshCount)
                    {
                        throw new InvalidOperationException(
                            $"Character fixture '{fixture.id}' context renderer {source.ComponentId} does not match its locked donor mesh/material slots.");
                    }

                    var owner = new GameObject(
                        scene.GetGameObjectName(source.GameObjectId) +
                        " Context");
                    owner.transform.SetParent(fixtureRoot, false);
                    scene.GetStaticRendererTransformRelativeTo(
                        source.ComponentId,
                        fixture.rootTransformFileId,
                        out Vector3 localPosition,
                        out Quaternion localRotation,
                        out Vector3 localScale);
                    owner.transform.localPosition = localPosition;
                    owner.transform.localRotation = localRotation;
                    owner.transform.localScale = localScale;
                    MeshFilter filter = owner.AddComponent<MeshFilter>();
                    filter.sharedMesh = contextProp.Mesh;
                    MeshRenderer renderer = owner.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = contextProp.Materials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    staticRendererTransforms.Add(
                        source.ComponentId,
                        owner.transform);
                    AddConditionalProp(
                        conditionalProps,
                        contextProp,
                        owner);
                }

                Transform animationTarget =
                    created[fixture.animationTargetTransformFileId];
                var animation = animationTarget.gameObject.AddComponent<Animation>();
                animation.playAutomatically = false;
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                foreach (CharacterAnimationBinding stateBinding in stateBindings)
                {
                    ValidateClipPaths(animationTarget, stateBinding.Clip);
                }

                foreach (CharacterActionAnimationBinding actionBinding in
                         actionBindings ??
                         Array.Empty<CharacterActionAnimationBinding>())
                {
                    ValidateClipPaths(animationTarget, actionBinding.Clip);
                }

                foreach (LayeredAnimationBuildData layered in layeredAnimations)
                {
                    Transform target = RequireTransform(
                        created,
                        layered.TargetTransformFileId);
                    ValidateClipPaths(target, layered.Clip);
                    var layeredAnimation = target.gameObject.AddComponent<
                        Animation>();
                    var loopingLayer = target.gameObject.AddComponent<
                        LegacyLoopingAnimationLayer>();
                    loopingLayer.ConfigureForAuthoring(
                        layeredAnimation,
                        layered.Clip,
                        layered.StartTimeSeconds);
                }

                var binding = root.AddComponent<LegacyCharacterPresentationBinding>();
                binding.ConfigureForAuthoring(
                    fixture.bindingId,
                    fixture.productionReplacementKey,
                    fixture.geometryReplacementKey,
                    fixture.materialReplacementKey,
                    fixture.animationReplacementKey,
                    animation,
                    stateBindings,
                    actionBindings);

                if (conditionalProps.Count > 0)
                {
                    var conditionalBinding = root.AddComponent<
                        CharacterFlagPresentationBinding>();
                    conditionalBinding.ConfigureForAuthoring(
                        conditionalProps);
                }

                if (HasConfiguredBicycleMotion(fixture.bicycleMotion))
                {
                    BicycleMotionSpec motion = fixture.bicycleMotion;
                    Transform frontWheel = staticRendererTransforms[
                        motion.wheelRendererComponentFileIds[0]];
                    Transform rearWheel = staticRendererTransforms[
                        motion.wheelRendererComponentFileIds[1]];
                    float groundContactCalibration =
                        CalibrateWheelContact(
                            root.transform,
                            fixtureRoot,
                            frontWheel,
                            rearWheel,
                            motion.maximumGroundCorrectionMeters);
                    var bicyclePresentation = root.AddComponent<
                        TeimoBicyclePresentationBinding>();
                    bicyclePresentation.ConfigureForAuthoring(
                        binding,
                        staticRendererTransforms[
                            motion.pedalsRendererComponentFileId],
                        frontWheel,
                        rearWheel,
                        created[motion.leftLegJointTransformFileIds[0]],
                        created[motion.leftLegJointTransformFileIds[1]],
                        created[motion.leftLegJointTransformFileIds[2]],
                        created[motion.rightLegJointTransformFileIds[0]],
                        created[motion.rightLegJointTransformFileIds[1]],
                        created[motion.rightLegJointTransformFileIds[2]],
                        motion.pedalHalfWidthMeters,
                        motion.pedalRadiusMeters,
                        motion.wheelDegreesPerMeter,
                        motion.pedalSpeedDivisor,
                        motion.greetingDistanceMeters,
                        groundContactCalibration);
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not save character fixture '{prefabPath}'.");
                }

                LegacyCharacterPresentationBinding saved =
                    prefab.GetComponent<LegacyCharacterPresentationBinding>();
                string failure = saved == null
                    ? "project-owned binding component is missing"
                    : string.Empty;
                if (saved == null || !saved.TryValidate(out failure))
                {
                    throw new InvalidOperationException(
                        $"Saved character fixture '{fixture.id}' is invalid: {failure}");
                }

                CharacterFlagPresentationBinding savedConditional =
                    prefab.GetComponent<CharacterFlagPresentationBinding>();
                bool expectsConditionalPresentation = conditionalProps.Count > 0;
                if ((expectsConditionalPresentation &&
                     (savedConditional == null ||
                      !savedConditional.TryValidate(out failure))) ||
                    (!expectsConditionalPresentation &&
                     savedConditional != null))
                {
                    throw new InvalidOperationException(
                        $"Saved character fixture '{fixture.id}' has invalid conditional presentation: {failure}");
                }

                TeimoBicyclePresentationBinding savedBicycle =
                    prefab.GetComponent<TeimoBicyclePresentationBinding>();
                bool expectsBicycleMotion =
                    HasConfiguredBicycleMotion(fixture.bicycleMotion);
                if ((expectsBicycleMotion &&
                     (savedBicycle == null ||
                      !savedBicycle.TryValidate(out failure))) ||
                    (!expectsBicycleMotion && savedBicycle != null))
                {
                    throw new InvalidOperationException(
                        $"Saved character fixture '{fixture.id}' has invalid bicycle motion: {failure}");
                }

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AddConditionalProp(
            ICollection<CharacterFlagVisibilityBinding> bindings,
            AccessoryBuildData accessory,
            GameObject presentationRoot)
        {
            if (!string.IsNullOrWhiteSpace(accessory.VisibilityFlagId))
            {
                presentationRoot.SetActive(false);
                bindings.Add(new CharacterFlagVisibilityBinding(
                    accessory.VisibilityFlagId,
                    presentationRoot,
                    visibleWhenSet: true));
            }
        }

        private static float CalibrateWheelContact(
            Transform wrapperRoot,
            Transform fixtureRoot,
            Transform frontWheel,
            Transform rearWheel,
            float maximumCorrectionMeters)
        {
            float minimumY = float.PositiveInfinity;
            foreach (Transform wheel in new[] { frontWheel, rearWheel })
            {
                MeshFilter filter = wheel != null
                    ? wheel.GetComponent<MeshFilter>()
                    : null;
                if (filter == null || filter.sharedMesh == null)
                {
                    throw new InvalidOperationException(
                        "Teimo bicycle ground calibration requires both locked tire meshes.");
                }

                Bounds bounds = filter.sharedMesh.bounds;
                Vector3 center = bounds.center;
                Vector3 extents = bounds.extents;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 localCorner = center + Vector3.Scale(
                                extents,
                                new Vector3(x, y, z));
                            Vector3 wrapperLocal = wrapperRoot.InverseTransformPoint(
                                wheel.TransformPoint(localCorner));
                            minimumY = Mathf.Min(minimumY, wrapperLocal.y);
                        }
                    }
                }
            }

            if (!float.IsFinite(minimumY) || minimumY < 0f ||
                minimumY > maximumCorrectionMeters)
            {
                throw new InvalidOperationException(
                    $"Teimo bicycle ground-contact correction {minimumY:F6} m exceeds the reviewed 0..{maximumCorrectionMeters:F3} m range.");
            }

            fixtureRoot.localPosition += Vector3.down * minimumY;
            return minimumY;
        }

        private static bool HasValidBicycleMotion(
            GameObject prefab,
            FixtureSpec fixture)
        {
            TeimoBicyclePresentationBinding motion = prefab != null
                ? prefab.GetComponent<TeimoBicyclePresentationBinding>()
                : null;
            return !HasConfiguredBicycleMotion(fixture.bicycleMotion)
                ? motion == null
                : motion != null && motion.TryValidate(out _);
        }

        private static void ValidateClipPaths(Transform target, AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!string.IsNullOrEmpty(binding.path) &&
                    target.Find(binding.path) == null)
                {
                    throw new InvalidOperationException(
                        $"Clip '{clip.name}' cannot resolve donor bone path '{binding.path}' in its sanitized wrapper.");
                }
            }
        }

        private static Transform RequireTransform(
            IReadOnlyDictionary<long, Transform> created,
            long id)
        {
            if (id == 0 || !created.TryGetValue(id, out Transform transform))
            {
                throw new InvalidOperationException(
                    $"Sanitized character slice is missing required bone {id}.");
            }

            return transform;
        }

        private static void ConfigureLegacyTexture(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as
                TextureImporter ?? throw new InvalidOperationException(
                    $"Character texture importer is unavailable for '{assetPath}'.");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static Material CreateLegacyMaterial(
            MaterialSpec spec,
            Texture2D texture)
        {
            Shader shader = Shader.Find("HDRP/Lit") ??
                throw new InvalidOperationException("HDRP/Lit shader is unavailable.");
            var material = new Material(shader)
            {
                name = "Phase 1 Temporary " + spec.role,
                enableInstancing = true,
            };
            if (texture != null)
            {
                material.SetTexture("_BaseColorMap", texture);
            }

            Color baseColor = spec.baseColorOverride?.Length == 4
                ? new Color(
                    spec.baseColorOverride[0],
                    spec.baseColorOverride[1],
                    spec.baseColorOverride[2],
                    spec.baseColorOverride[3])
                : new Color(0.8014706f, 0.8014706f, 0.8014706f, 1f);
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat(
                "_Metallic",
                spec.hasMetallicOverride ? spec.metallicOverride : 0f);
            material.SetFloat(
                "_Smoothness",
                spec.hasSmoothnessOverride ? spec.smoothnessOverride : 0f);
            bool transparent = string.Equals(
                spec.surfaceType,
                "Transparent",
                StringComparison.Ordinal);
            material.SetFloat("_SurfaceType", transparent ? 1f : 0f);
            material.SetFloat("_BlendMode", 0f);
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            SetFloatIfPresent(material, "_SrcBlend", 1f);
            SetFloatIfPresent(material, "_DstBlend", transparent ? 10f : 0f);
            SetFloatIfPresent(material, "_AlphaSrcBlend", 1f);
            SetFloatIfPresent(
                material,
                "_AlphaDstBlend",
                transparent ? 10f : 0f);
            if (material.HasProperty("_TransparentZWrite"))
            {
                material.SetFloat("_TransparentZWrite", 0f);
            }

            if (material.HasProperty("_EnableBlendModePreserveSpecularLighting"))
            {
                material.SetFloat(
                    "_EnableBlendModePreserveSpecularLighting",
                    transparent ? 1f : 0f);
            }

            if (transparent)
            {
                material.renderQueue = (int)RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                material.renderQueue = (int)RenderQueue.Geometry;
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            string path = GetGeneratedMaterialPath(spec);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static string GetGeneratedMaterialPath(MaterialSpec spec) =>
            MaterialRoot + "/" + spec.role.Replace('.', '_') + ".mat";

        private static void SetFloatIfPresent(
            Material material,
            string propertyName,
            float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static bool IsGeneratedMaterialValid(
            Material material,
            Manifest manifest)
        {
            if (material == null)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(material);
            MaterialSpec spec = manifest.materials.SingleOrDefault(candidate =>
                string.Equals(
                    GetGeneratedMaterialPath(candidate),
                    assetPath,
                    StringComparison.Ordinal));
            if (spec == null ||
                (!string.IsNullOrWhiteSpace(spec.textureRole) &&
                 material.GetTexture("_BaseColorMap") == null))
            {
                return false;
            }

            bool expectedTransparent = string.Equals(
                spec.surfaceType,
                "Transparent",
                StringComparison.Ordinal);
            return !material.HasProperty("_SurfaceType") ||
                   Mathf.Approximately(
                       material.GetFloat("_SurfaceType"),
                       expectedTransparent ? 1f : 0f);
        }

        private static void SetLoop(AnimationClip clip, bool loopValue)
        {
            var serialized = new SerializedObject(clip);
            SerializedProperty settings = serialized.FindProperty("m_AnimationClipSettings") ??
                throw new InvalidOperationException(
                    $"Animation settings are unavailable for '{clip.name}'.");
            settings.FindPropertyRelative("m_LoopTime").boolValue = loopValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Manifest LoadManifest()
        {
            string path = ToFileSystemPath(ManifestPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Phase 1 character manifest is missing.", path);
            }

            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            if (manifest == null || manifest.schemaVersion != 8 ||
                manifest.source == null || manifest.fixtures == null ||
                manifest.fixtures.Length == 0 || manifest.assets == null ||
                manifest.materials == null || manifest.materials.Length == 0)
            {
                throw new InvalidDataException("Phase 1 character manifest is malformed.");
            }

            var assetRoles = new HashSet<string>(
                manifest.assets.Select(asset => asset.role),
                StringComparer.Ordinal);
            var materialRoles = new HashSet<string>(
                manifest.materials.Select(material => material.role),
                StringComparer.Ordinal);
            if (assetRoles.Count != manifest.assets.Length ||
                materialRoles.Count != manifest.materials.Length ||
                manifest.materials.Any(material =>
                    (string.IsNullOrWhiteSpace(material.textureRole) &&
                     material.baseColorOverride == null) ||
                    (!string.IsNullOrWhiteSpace(material.textureRole) &&
                     !assetRoles.Contains(material.textureRole)) ||
                    (material.baseColorOverride != null &&
                     material.baseColorOverride.Length != 4) ||
                    (!string.IsNullOrWhiteSpace(material.surfaceType) &&
                     !string.Equals(material.surfaceType, "Opaque",
                         StringComparison.Ordinal) &&
                     !string.Equals(material.surfaceType, "Transparent",
                         StringComparison.Ordinal))) ||
                 manifest.fixtures.Any(fixture =>
                     fixture.materialRoles == null ||
                     fixture.stateBindings == null ||
                     (fixture.rootLocalPositionOverride != null &&
                      fixture.rootLocalPositionOverride.Length != 3) ||
                     (fixture.rootLocalRotationOverride != null &&
                      fixture.rootLocalRotationOverride.Length != 4) ||
                     !assetRoles.Contains(fixture.meshRole) ||
                     fixture.materialRoles.Any(role => !materialRoles.Contains(role)) ||
                     (fixture.accessories ?? Array.Empty<AccessorySpec>())
                         .Concat(fixture.contextProps ?? Array.Empty<AccessorySpec>())
                         .Any(accessory =>
                         accessory.rendererComponentFileId <= 0 ||
                         !assetRoles.Contains(accessory.meshRole) ||
                         accessory.materialRoles == null ||
                         accessory.materialRoles.Any(role =>
                             !materialRoles.Contains(role))) ||
                     fixture.stateBindings.Any(binding =>
                        !assetRoles.Contains(binding.clipRole)) ||
                     (fixture.actionBindings ??
                      Array.Empty<ActionAnimationBindingSpec>()).Any(binding =>
                         string.IsNullOrWhiteSpace(binding.actionId) ||
                         !assetRoles.Contains(binding.clipRole)) ||
                     (fixture.actionBindings ??
                      Array.Empty<ActionAnimationBindingSpec>())
                         .Select(binding => binding.actionId)
                         .Distinct(StringComparer.Ordinal)
                         .Count() !=
                     (fixture.actionBindings?.Length ?? 0) ||
                     (fixture.layeredAnimations ??
                      Array.Empty<LayeredAnimationSpec>()).Any(layered =>
                          layered.targetTransformFileId <= 0 ||
                          !assetRoles.Contains(layered.clipRole) ||
                          !float.IsFinite(layered.startTimeSeconds) ||
                          layered.startTimeSeconds < 0f) ||
                     (fixture.layeredAnimations ??
                      Array.Empty<LayeredAnimationSpec>())
                         .Select(layered => layered.targetTransformFileId)
                         .Distinct()
                         .Count() !=
                     (fixture.layeredAnimations?.Length ?? 0) ||
                     (fixture.accessories ?? Array.Empty<AccessorySpec>())
                         .Concat(fixture.contextProps ?? Array.Empty<AccessorySpec>())
                         .Select(accessory => accessory.rendererComponentFileId)
                         .Distinct()
                         .Count() !=
                     (fixture.accessories?.Length ?? 0) +
                     (fixture.contextProps?.Length ?? 0)))
            {
                throw new InvalidDataException(
                    "Phase 1 character manifest contains duplicate or unresolved asset roles.");
            }

            foreach (FixtureSpec fixture in manifest.fixtures)
            {
                if (!IsValidBicycleMotionSpec(
                        fixture.bicycleMotion,
                        fixture.accessories))
                {
                    throw new InvalidDataException(
                        $"Phase 1 character fixture '{fixture.id}' has invalid bicycle-motion evidence or renderer references.");
                }
            }

            return manifest;
        }

        private static bool IsValidBicycleMotionSpec(
            BicycleMotionSpec motion,
            AccessorySpec[] accessories)
        {
            if (!HasConfiguredBicycleMotion(motion))
            {
                return motion == null ||
                       motion.pedalsRendererComponentFileId == 0 &&
                       (motion.wheelRendererComponentFileIds == null ||
                        motion.wheelRendererComponentFileIds.Length == 0) &&
                       motion.wheelDegreesPerMeter == 0f &&
                       motion.pedalSpeedDivisor == 0f &&
                       motion.greetingDistanceMeters == 0f &&
                       motion.maximumGroundCorrectionMeters == 0f &&
                       (motion.leftLegJointTransformFileIds == null ||
                        motion.leftLegJointTransformFileIds.Length == 0) &&
                       (motion.rightLegJointTransformFileIds == null ||
                        motion.rightLegJointTransformFileIds.Length == 0) &&
                       motion.pedalHalfWidthMeters == 0f &&
                       motion.pedalRadiusMeters == 0f;
            }

            long[] accessoryIds = (accessories ?? Array.Empty<AccessorySpec>())
                .Select(accessory => accessory.rendererComponentFileId)
                .ToArray();
            return motion.pedalsRendererComponentFileId > 0 &&
                   accessoryIds.Contains(
                       motion.pedalsRendererComponentFileId) &&
                   motion.wheelRendererComponentFileIds != null &&
                   motion.wheelRendererComponentFileIds.Length == 2 &&
                   motion.wheelRendererComponentFileIds.Distinct().Count() == 2 &&
                   motion.wheelRendererComponentFileIds.All(accessoryIds.Contains) &&
                   IsValidJointChain(motion.leftLegJointTransformFileIds) &&
                   IsValidJointChain(motion.rightLegJointTransformFileIds) &&
                   !motion.leftLegJointTransformFileIds.Intersect(
                       motion.rightLegJointTransformFileIds).Any() &&
                   float.IsFinite(motion.pedalHalfWidthMeters) &&
                   motion.pedalHalfWidthMeters > 0f &&
                   float.IsFinite(motion.pedalRadiusMeters) &&
                   motion.pedalRadiusMeters > 0f &&
                   float.IsFinite(motion.wheelDegreesPerMeter) &&
                   motion.wheelDegreesPerMeter > 0f &&
                   float.IsFinite(motion.pedalSpeedDivisor) &&
                   motion.pedalSpeedDivisor > 0f &&
                   float.IsFinite(motion.greetingDistanceMeters) &&
                   motion.greetingDistanceMeters > 0f &&
                   float.IsFinite(motion.maximumGroundCorrectionMeters) &&
                   motion.maximumGroundCorrectionMeters > 0f;
        }

        private static bool IsValidJointChain(long[] transformIds) =>
            transformIds != null &&
            transformIds.Length == 3 &&
            transformIds.All(id => id > 0) &&
            transformIds.Distinct().Count() == 3;

        private static bool HasConfiguredBicycleMotion(
            BicycleMotionSpec motion) =>
            motion != null && motion.pedalsRendererComponentFileId > 0;

        private static void ResetOutput()
        {
            string output = ToFileSystemPath(OutputRoot);
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }

            Directory.CreateDirectory(ToFileSystemPath(SourceRoot));
            Directory.CreateDirectory(ToFileSystemPath(GeneratedRoot));
            Directory.CreateDirectory(ToFileSystemPath(ResourcesRoot));
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
                    "Locked donor asset or its metadata is missing.",
                    source);
            }

            string destination = ToFileSystemPath(destinationAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, overwrite: true);
            string meta = File.ReadAllText(source + ".meta");
            string sourceLine = "guid: " + sourceGuid;
            string generatedLine = "guid: " + generatedGuid;
            if (!meta.Contains(sourceLine))
            {
                throw new InvalidDataException(
                    $"Donor metadata for '{source}' does not contain locked GUID {sourceGuid}.");
            }

            File.WriteAllText(
                destination + ".meta",
                meta.Replace(sourceLine, generatedLine),
                new UTF8Encoding(false));
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidOperationException(
                $"Unity could not import required asset '{path}' as {typeof(T).Name}.");

        private static void RequireHash(string path, string expected, string label)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Locked {label} is missing.", path);
            }

            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            string actual = string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Locked {label} hash mismatch. Expected {expected}, got {actual}.");
            }
        }

        private static void WriteReport(Manifest manifest, string scenePath)
        {
            string report = JsonUtility.ToJson(new BuildReportData
            {
                schemaVersion = manifest.schemaVersion,
                manifestId = manifest.manifestId,
                classification = manifest.classification,
                sourceSceneSha256 = ComputeHash(scenePath),
                sourceManifestSha256 = ComputeHash(
                    ToFileSystemPath(ManifestPath)),
                fixtureCount = manifest.fixtures.Length,
                materialCount = manifest.materials.Length,
                textureCount = manifest.assets.Count(asset =>
                    asset.relativePath.EndsWith(
                        ".png",
                        StringComparison.OrdinalIgnoreCase)),
                accessoryCount = manifest.fixtures.Sum(fixture =>
                    (fixture.accessories?.Length ?? 0) +
                    (fixture.contextProps?.Length ?? 0)),
                layeredAnimationCount = manifest.fixtures.Sum(fixture =>
                    fixture.layeredAnimations?.Length ?? 0),
                excludesDonorScriptsFsmControllers = true,
                excludesDonorShaders = true,
            }, prettyPrint: true) + Environment.NewLine;
            File.WriteAllText(
                ToFileSystemPath(BuildReportPath),
                report,
                new UTF8Encoding(false));
        }

        private static string ComputeHash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
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
            public FixtureSpec[] fixtures;
            public MaterialSpec[] materials;
            public AssetSpec[] assets;
        }

        [Serializable]
        private sealed class SourceSpec
        {
            public string stagingRootRelativePath;
            public string sceneRelativePath;
            public string sceneSha256;
        }

        [Serializable]
        private sealed class FixtureSpec
        {
            public string id;
            public string bindingId;
            public string productionReplacementKey;
            public string geometryReplacementKey;
            public string materialReplacementKey;
            public string animationReplacementKey;
            public long rootTransformFileId;
            public long animationTargetTransformFileId;
            public long[] rendererComponentFileIds;
            public string meshRole;
            public string[] materialRoles;
            public float[] rootLocalPositionOverride;
            public float[] rootLocalRotationOverride;
            public AccessorySpec[] accessories;
            public AccessorySpec[] contextProps;
            public AnimationBindingSpec[] stateBindings;
            public ActionAnimationBindingSpec[] actionBindings;
            public LayeredAnimationSpec[] layeredAnimations;
            public BicycleMotionSpec bicycleMotion;
        }

        [Serializable]
        private sealed class AccessorySpec
        {
            public long rendererComponentFileId;
            public string meshRole;
            public string[] materialRoles;
            public string sourceMeshGuidOverride;
            public string visibilityFlagId;
        }

        [Serializable]
        private sealed class AnimationBindingSpec
        {
            public string activityState;
            public string clipRole;
            public bool loop;
        }

        [Serializable]
        private sealed class ActionAnimationBindingSpec
        {
            public string actionId;
            public string clipRole;
            public bool loop;
        }

        [Serializable]
        private sealed class LayeredAnimationSpec
        {
            public long targetTransformFileId;
            public string clipRole;
            public float startTimeSeconds;
        }

        [Serializable]
        private sealed class BicycleMotionSpec
        {
            public long pedalsRendererComponentFileId;
            public long[] wheelRendererComponentFileIds;
            public long[] leftLegJointTransformFileIds;
            public long[] rightLegJointTransformFileIds;
            public float pedalHalfWidthMeters;
            public float pedalRadiusMeters;
            public float wheelDegreesPerMeter;
            public float pedalSpeedDivisor;
            public float greetingDistanceMeters;
            public float maximumGroundCorrectionMeters;
        }

        [Serializable]
        private sealed class MaterialSpec
        {
            public string role;
            public string sourceMaterialGuid;
            public string textureRole;
            public float[] baseColorOverride;
            public bool hasMetallicOverride;
            public float metallicOverride;
            public bool hasSmoothnessOverride;
            public float smoothnessOverride;
            public string surfaceType;
            public string productionReplacementKey;
        }

        [Serializable]
        private sealed class AssetSpec
        {
            public string role;
            public string relativePath;
            public string guid;
            public string generatedGuid;
            public string sha256;
        }

        [Serializable]
        private sealed class BuildReportData
        {
            public int schemaVersion;
            public string manifestId;
            public string classification;
            public string sourceSceneSha256;
            public string sourceManifestSha256;
            public int fixtureCount;
            public int materialCount;
            public int textureCount;
            public int accessoryCount;
            public int layeredAnimationCount;
            public bool excludesDonorScriptsFsmControllers;
            public bool excludesDonorShaders;
        }

        private sealed class AccessoryBuildData
        {
            public AccessoryBuildData(
                long rendererComponentFileId,
                string meshGuid,
                Mesh mesh,
                Material[] materials,
                string[] sourceMaterialGuids,
                string visibilityFlagId)
            {
                RendererComponentFileId = rendererComponentFileId;
                SourceMeshGuid = meshGuid ?? string.Empty;
                Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
                Materials = materials ??
                    throw new ArgumentNullException(nameof(materials));
                SourceMaterialGuids = sourceMaterialGuids ??
                    throw new ArgumentNullException(nameof(sourceMaterialGuids));
                VisibilityFlagId = visibilityFlagId ?? string.Empty;
            }

            public long RendererComponentFileId { get; }

            public string SourceMeshGuid { get; }

            public Mesh Mesh { get; }

            public Material[] Materials { get; }

            public string[] SourceMaterialGuids { get; }

            public string VisibilityFlagId { get; }
        }

        private sealed class LayeredAnimationBuildData
        {
            public LayeredAnimationBuildData(
                long targetTransformFileId,
                AnimationClip clip,
                float startTimeSeconds)
            {
                TargetTransformFileId = targetTransformFileId;
                Clip = clip ?? throw new ArgumentNullException(nameof(clip));
                StartTimeSeconds = startTimeSeconds;
            }

            public long TargetTransformFileId { get; }

            public AnimationClip Clip { get; }

            public float StartTimeSeconds { get; }
        }
    }

    public sealed class Phase1CharacterPresentationBuildGuard :
        IPreprocessBuildWithReport
    {
        public int callbackOrder => -890;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                Phase1CharacterPresentationImporter.EnsureGeneratedForBuild();
                Phase1StoryTrafficPresentationImporter.EnsureGeneratedForBuild();
            }
            catch (Exception exception)
            {
                throw new BuildFailedException(
                    "Mandatory Phase 1 character presentation validation failed. " +
                    exception.Message);
            }
        }
    }
}
