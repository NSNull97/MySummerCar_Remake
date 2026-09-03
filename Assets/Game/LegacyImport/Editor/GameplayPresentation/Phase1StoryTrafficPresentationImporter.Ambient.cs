using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Characters;
using MSC.Traffic;
using MSC.Vehicle.NWH;
using NWH.WheelController3D;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static partial class Phase1StoryTrafficPresentationImporter
    {
        private const string AmbientPrefabRoot = GeneratedRoot +
            "/AmbientPrefabs";
        private const string AmbientCatalogPath = GeneratedRoot +
            "/Resources/Phase1Traffic/TrafficPresentationCatalog.asset";

        private static AmbientFixtureSource[] BuildAmbientFixtureSources(
            DonorUnitySceneModel scene,
            IReadOnlyList<AmbientVehicleSpec> specs)
        {
            return (specs ?? Array.Empty<AmbientVehicleSpec>())
                .Select(spec =>
                {
                    DonorTransformRecord root = scene.GetTransform(
                        spec.rootTransformFileId);
                    string rootName = scene.GetGameObjectName(
                        root.GameObjectId);
                    if (!string.Equals(
                            rootName,
                            spec.donorRootName,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Ambient traffic root {spec.rootTransformFileId} " +
                            $"was expected to be '{spec.donorRootName}', got " +
                            $"'{rootName}'.");
                    }

                    DonorStaticRendererRecord[] selectedStaticRenderers = scene
                        .GetActiveStaticRenderersBelow(
                            new[] { spec.rootTransformFileId },
                            treatSelectedRootsAsActive: true)
                        .ToArray();
                    DonorSkinnedRendererRecord[] selectedSkinnedRenderers = scene
                        .GetActiveSkinnedRenderersBelow(
                            new[] { spec.rootTransformFileId },
                            treatSelectedRootsAsActive: true)
                        .ToArray();
                    long[] excludedStaticIds =
                        spec.excludedStaticRendererComponentFileIds ??
                        Array.Empty<long>();
                    long[] excludedSkinnedIds =
                        spec.excludedSkinnedRendererComponentFileIds ??
                        Array.Empty<long>();
                    RequireAllRendererExclusionsExist(
                        spec.id,
                        selectedStaticRenderers.Select(value =>
                            value.ComponentId),
                        excludedStaticIds,
                        "static");
                    RequireAllRendererExclusionsExist(
                        spec.id,
                        selectedSkinnedRenderers.Select(value =>
                            value.ComponentId),
                        excludedSkinnedIds,
                        "skinned");
                    DonorStaticRendererRecord[] staticRenderers =
                        selectedStaticRenderers.Where(value =>
                            !excludedStaticIds.Contains(value.ComponentId))
                            .ToArray();
                    DonorSkinnedRendererRecord[] skinnedRenderers =
                        selectedSkinnedRenderers.Where(value =>
                            !excludedSkinnedIds.Contains(value.ComponentId))
                            .ToArray();
                    if (staticRenderers.Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"Ambient traffic fixture '{spec.id}' selected no " +
                            "static vehicle renderers.");
                    }

                    long[] wheelTransformIds = spec.wheelNames
                        .Select(name => scene.GetUniqueDirectChildByName(
                            spec.rootTransformFileId,
                            name).TransformId)
                        .ToArray();
                    return new AmbientFixtureSource(
                        spec,
                        skinnedRenderers,
                        staticRenderers,
                        wheelTransformIds);
                })
                .ToArray();
        }

        private static void RequireAllRendererExclusionsExist(
            string fixtureId,
            IEnumerable<long> selectedIds,
            IReadOnlyCollection<long> excludedIds,
            string rendererKind)
        {
            var selected = new HashSet<long>(selectedIds ??
                Enumerable.Empty<long>());
            long[] missing = (excludedIds ?? Array.Empty<long>())
                .Where(id => !selected.Contains(id))
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Ambient traffic fixture '{fixtureId}' could not find " +
                    $"excluded {rendererKind} renderer IDs: " +
                    string.Join(", ", missing));
            }
        }

        private static void EnsureAmbientOutputFolders()
        {
            EnsureAssetFolder(AmbientPrefabRoot);
            EnsureAssetFolder(GeneratedRoot + "/Resources");
            EnsureAssetFolder(GeneratedRoot + "/Resources/Phase1Traffic");
        }

        private static void BuildAmbientPresentationCatalog(
            DonorUnitySceneModel scene,
            IReadOnlyList<AmbientFixtureSource> sources,
            IReadOnlyList<TransportFixtureSource> transportSources,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            var entries = new List<TrafficPresentationCatalogEntry>();
            foreach (AmbientFixtureSource source in sources)
            {
                GameObject prefab = BuildAmbientFixture(
                    scene,
                    source,
                    importedMeshes,
                    materials);
                entries.Add(new TrafficPresentationCatalogEntry(
                    source.Spec.presentationId,
                    source.Spec.productionReplacementKey,
                    prefab));
            }

            entries.AddRange(BuildTransportFixtures(
                scene,
                transportSources,
                importedMeshes,
                materials));

            var catalog = ScriptableObject.CreateInstance<
                TrafficPresentationCatalog>();
            catalog.name = "Phase1TrafficPresentationCatalog";
            catalog.ConfigureForAuthoring(entries);
            if (!catalog.TryValidate(out string failure))
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                throw new InvalidOperationException(
                    $"Ambient traffic presentation catalog is invalid: " +
                    failure);
            }

            AssetDatabase.CreateAsset(catalog, AmbientCatalogPath);
            EditorUtility.SetDirty(catalog);
        }

        private static GameObject BuildAmbientFixture(
            DonorUnitySceneModel scene,
            AmbientFixtureSource source,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            AmbientVehicleSpec ambientSpec = source.Spec;
            DonorActionSlice slice = scene.CreatePresentationSlice(
                ambientSpec.rootTransformFileId,
                ambientSpec.rootTransformFileId,
                source.SkinnedRenderers
                    .Select(renderer => renderer.ComponentId)
                    .ToArray(),
                source.StaticRenderers
                    .Select(renderer => renderer.ComponentId)
                    .ToArray(),
                source.WheelTransformIds);
            var root = new GameObject(
                "Phase 1 Ambient Traffic " + ambientSpec.id);
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
                    bool isFixtureRoot = donor.TransformId ==
                                         ambientSpec.rootTransformFileId;
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
                        candidate => candidate.GameObjectId ==
                                     donor.GameObjectId);
                    Mesh mesh = RequireAsset<Mesh>(
                        importedMeshes[donor.MeshGuid]);
                    var renderer = created[ownerRecord.TransformId]
                        .gameObject.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = mesh;
                    renderer.sharedMaterials = donor.MaterialGuids
                        .Select(guid => materials[guid])
                        .ToArray();
                    if (renderer.sharedMaterials.Length != mesh.subMeshCount)
                    {
                        throw new InvalidOperationException(
                            $"Ambient traffic skinned renderer " +
                            $"{donor.ComponentId} has a material slot mismatch.");
                    }

                    renderer.rootBone = donor.RootBoneTransformId > 0L
                        ? RequireTransform(created, donor.RootBoneTransformId)
                        : null;
                    renderer.bones = donor.BoneTransformIds
                        .Where(id => id > 0L)
                        .Select(id => RequireTransform(created, id))
                        .ToArray();
                    Bounds bounds = donor.LocalBounds;
                    bounds.Encapsulate(new Vector3(-4f, -4f, -4f));
                    bounds.Encapsulate(new Vector3(4f, 4f, 4f));
                    renderer.localBounds = bounds;
                    renderer.updateWhenOffscreen = true;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                foreach (DonorStaticRendererRecord donor in
                         slice.StaticRenderers)
                {
                    DonorTransformRecord ownerRecord = slice.Transforms.Single(
                        candidate => candidate.GameObjectId ==
                                     donor.GameObjectId);
                    GameObject owner = created[ownerRecord.TransformId]
                        .gameObject;
                    Mesh mesh = RequireAsset<Mesh>(
                        importedMeshes[donor.MeshGuid]);
                    owner.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = owner.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = donor.MaterialGuids
                        .Select(guid => materials[guid])
                        .ToArray();
                    if (renderer.sharedMaterials.Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"Ambient traffic static renderer " +
                            $"{donor.ComponentId} has no material slots.");
                    }

                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                Transform fixtureRoot = RequireTransform(
                    created,
                    ambientSpec.rootTransformFileId);
                Transform[] donorWheelRoots = source.WheelTransformIds
                    .Select(id => RequireTransform(created, id))
                    .ToArray();
                Transform[] wheels = CreateWheelSpinPivots(donorWheelRoots);
                float groundContact = CalibrateWheelContact(
                    root.transform,
                    fixtureRoot,
                    wheels,
                    ambientSpec.maximumGroundCorrectionMeters);

                var vehicle = root.AddComponent<
                    StoryTrafficVehiclePresentationBinding>();
                vehicle.ConfigureForAuthoring(
                    ambientSpec.driverFeatureId,
                    Array.Empty<string>(),
                    Array.Empty<GameObject>(),
                    wheels,
                    ambientSpec.wheelDegreesPerMeter,
                    groundContact);
                bool isCousinVehicle = string.Equals(
                        ambientSpec.presentationId,
                        TrafficCousinBehaviorRules.OrdinaryPresentationId,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        ambientSpec.presentationId,
                        TrafficCousinBehaviorRules.SaturdayPresentationId,
                        StringComparison.Ordinal);
                vehicle.ConfigureDrivingProfile(
                    ambientSpec.minimumSpeedMetersPerSecond,
                    ambientSpec.maximumSpeedMetersPerSecond,
                    configuredAccelerationMetersPerSecond2:
                        isCousinVehicle ? 3.5f : 4.8f,
                    configuredBrakingMetersPerSecond2:
                        isCousinVehicle ? 12f : 17f,
                    configuredTurnRateDegreesPerSecond:
                        isCousinVehicle ? 105f : 125f,
                    configuredPassingLaneOffsetMeters:
                        isCousinVehicle ? -1.1f : -2f);
                vehicle.ConfigureRoadLanePolicy(
                    configuredBaseLaneOffsetMeters:
                        isCousinVehicle ? 1.1f : 2f,
                    configuredPassingLaneOffsetMeters:
                        isCousinVehicle ? -1.1f : -2f,
                    migrateLegacyCenterLane: true);
                vehicle.ConfigureHooliganBehavior(
                    configuredLaneWanderAmplitudeMeters:
                        isCousinVehicle ? 1.05f : 0.18f,
                    configuredMinimumDriftSpeedMetersPerSecond:
                        isCousinVehicle ? 1000f : 32f,
                    configuredDriftEntryAngleDegrees: 35f,
                    configuredDriftExitAngleDegrees: 10f,
                    configuredMaximumDriftSlipDegrees: 12f);

                var dynamicSpec = new VehicleSpec
                {
                    id = ambientSpec.id,
                    wheelDegreesPerMeter =
                        ambientSpec.wheelDegreesPerMeter,
                    maximumGroundCorrectionMeters =
                        ambientSpec.maximumGroundCorrectionMeters,
                    isAmbientTraffic = true,
                    provisionalMassKilograms =
                        ambientSpec.provisionalMassKilograms,
                };
                ConfigureDynamicVehicle(
                    root,
                    dynamicSpec,
                    wheels,
                    vehicle);

                string prefabPath = AmbientPrefabRoot + "/" +
                                    ambientSpec.id + ".prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath) ?? throw new InvalidOperationException(
                    $"Could not save ambient traffic prefab " +
                    $"'{prefabPath}'.");
                ValidateAmbientWrapper(ambientSpec, prefab);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void EnsureAmbientGeneratedForBuild(Manifest manifest)
        {
            TrafficPresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficPresentationCatalog>(AmbientCatalogPath) ??
                throw new InvalidOperationException(
                    "Ambient traffic presentation catalog is missing.");
            if (!catalog.TryValidate(out string failure))
            {
                throw new InvalidOperationException(
                    "Ambient traffic presentation catalog is invalid: " +
                    failure);
            }

            foreach (AmbientVehicleSpec spec in manifest.ambientVehicles)
            {
                if (!catalog.TryGet(spec.presentationId, out var entry) ||
                    entry.WrapperPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Ambient traffic presentation " +
                        $"'{spec.presentationId}' is missing.");
                }

                ValidateAmbientWrapper(spec, entry.WrapperPrefab);
            }


            foreach (TransportVehicleSpec spec in manifest.transports)
            {
                if (!catalog.TryGet(spec.presentationId, out var entry) ||
                    entry.WrapperPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Transport presentation '{spec.presentationId}' is missing.");
                }

                ValidateTransportWrapper(spec, entry.WrapperPrefab);
            }
        }

        private static void ValidateAmbientWrapper(
            AmbientVehicleSpec spec,
            GameObject wrapper)
        {
            StoryTrafficVehiclePresentationBinding vehicle = wrapper
                .GetComponent<StoryTrafficVehiclePresentationBinding>();
            string failure = string.Empty;
            if (vehicle == null || !vehicle.TryValidate(out failure) ||
                wrapper.GetComponent<Rigidbody>() == null ||
                wrapper.GetComponent<BoxCollider>() == null ||
                wrapper.GetComponent<NwhWheelPhysicsBackend>() == null ||
                wrapper.GetComponent<
                    NwhStoryTrafficVehicleMotionBackend>() == null ||
                wrapper.GetComponentsInChildren<WheelController>(true)
                    .Length != 4 ||
                wrapper.GetComponentsInChildren<Animator>(true).Length != 0 ||
                wrapper.GetComponentsInChildren<MonoBehaviour>(true)
                    .Any(component =>
                        component != vehicle &&
                        component is not NwhWheelPhysicsBackend &&
                        component is not
                            NwhStoryTrafficVehicleMotionBackend &&
                        component is not WheelController))
            {
                throw new InvalidOperationException(
                    $"Ambient traffic fixture '{spec.id}' is invalid: " +
                    failure);
            }
        }

        private static bool TryValidateAmbientManifest(
            IReadOnlyList<AmbientVehicleSpec> specs)
        {
            if (specs == null || specs.Count != 16 ||
                specs.Select(value => value.id)
                    .Distinct(StringComparer.Ordinal).Count() != specs.Count ||
                specs.Select(value => value.presentationId)
                    .Distinct(StringComparer.Ordinal).Count() != specs.Count)
            {
                return false;
            }

            return specs.All(spec =>
                spec != null &&
                !string.IsNullOrWhiteSpace(spec.id) &&
                !string.IsNullOrWhiteSpace(spec.donorRootName) &&
                !string.IsNullOrWhiteSpace(spec.presentationId) &&
                spec.presentationId.StartsWith(
                    "presentation.traffic.",
                    StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(spec.productionReplacementKey) &&
                !string.IsNullOrWhiteSpace(spec.vehicleFeatureId) &&
                spec.vehicleFeatureId.StartsWith(
                    "P1.VEHICLE.",
                    StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(spec.driverFeatureId) &&
                spec.driverFeatureId.StartsWith(
                    "P1.NPC.",
                    StringComparison.Ordinal) &&
                spec.rootTransformFileId > 0L &&
                IsValidOptionalRendererIdList(
                    spec.excludedStaticRendererComponentFileIds) &&
                IsValidOptionalRendererIdList(
                    spec.excludedSkinnedRendererComponentFileIds) &&
                spec.wheelNames != null &&
                spec.wheelNames.Length == 4 &&
                spec.wheelNames.All(value =>
                    !string.IsNullOrWhiteSpace(value)) &&
                spec.wheelNames.Distinct(StringComparer.Ordinal).Count() == 4 &&
                float.IsFinite(spec.wheelDegreesPerMeter) &&
                spec.wheelDegreesPerMeter > 0f &&
                float.IsFinite(spec.maximumGroundCorrectionMeters) &&
                spec.maximumGroundCorrectionMeters > 0f &&
                spec.maximumGroundCorrectionMeters <= 1f &&
                float.IsFinite(spec.minimumSpeedMetersPerSecond) &&
                spec.minimumSpeedMetersPerSecond > 0f &&
                float.IsFinite(spec.maximumSpeedMetersPerSecond) &&
                spec.maximumSpeedMetersPerSecond >=
                    spec.minimumSpeedMetersPerSecond &&
                float.IsFinite(spec.provisionalMassKilograms) &&
                spec.provisionalMassKilograms >= 500f);
        }

        private static bool IsValidOptionalRendererIdList(
            IReadOnlyCollection<long> componentIds)
        {
            return componentIds == null ||
                   componentIds.All(value => value > 0L) &&
                   componentIds.Distinct().Count() == componentIds.Count;
        }

        [Serializable]
        private sealed class AmbientVehicleSpec
        {
            public string id;
            public string donorRootName;
            public string presentationId;
            public string productionReplacementKey;
            public string vehicleFeatureId;
            public string driverFeatureId;
            public long rootTransformFileId;
            public long[] excludedStaticRendererComponentFileIds;
            public long[] excludedSkinnedRendererComponentFileIds;
            public string[] wheelNames;
            public float wheelDegreesPerMeter;
            public float maximumGroundCorrectionMeters;
            public float minimumSpeedMetersPerSecond;
            public float maximumSpeedMetersPerSecond;
            public float provisionalMassKilograms;
        }

        private sealed class AmbientFixtureSource
        {
            public AmbientFixtureSource(
                AmbientVehicleSpec spec,
                DonorSkinnedRendererRecord[] skinnedRenderers,
                DonorStaticRendererRecord[] staticRenderers,
                long[] wheelTransformIds)
            {
                Spec = spec;
                SkinnedRenderers = skinnedRenderers;
                StaticRenderers = staticRenderers;
                WheelTransformIds = wheelTransformIds;
            }

            public AmbientVehicleSpec Spec { get; }
            public DonorSkinnedRendererRecord[] SkinnedRenderers { get; }
            public DonorStaticRendererRecord[] StaticRenderers { get; }
            public long[] WheelTransformIds { get; }
        }
    }
}
