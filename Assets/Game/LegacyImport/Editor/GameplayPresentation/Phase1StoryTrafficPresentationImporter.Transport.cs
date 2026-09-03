using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Characters;
using MSC.Interaction.Query;
using MSC.Traffic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static partial class Phase1StoryTrafficPresentationImporter
    {
        private const string TransportPrefabRoot = GeneratedRoot +
            "/TransportPrefabs";
        private const string BusDriverWalkerBindingId =
            "presentation.character.fixture.vehicle-linked";

        private static TransportFixtureSource[] BuildTransportFixtureSources(
            DonorUnitySceneModel scene,
            IReadOnlyList<TransportVehicleSpec> specs)
        {
            return (specs ?? Array.Empty<TransportVehicleSpec>())
                .Select(spec =>
                {
                    DonorTransformRecord root = scene.GetTransform(
                        spec.rootTransformFileId);
                    string rootName = scene.GetGameObjectName(root.GameObjectId);
                    if (!string.Equals(
                            rootName,
                            spec.donorRootName,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Transport root {spec.rootTransformFileId} was " +
                            $"expected to be '{spec.donorRootName}', got " +
                            $"'{rootName}'.");
                    }

                    DonorStaticRendererRecord[] staticRenderers = scene
                        .GetActiveStaticRenderersBelow(
                            new[] { spec.rootTransformFileId },
                            treatSelectedRootsAsActive: true)
                        .ToArray();
                    DonorSkinnedRendererRecord[] skinnedRenderers = scene
                        .GetActiveSkinnedRenderersBelow(
                            new[] { spec.rootTransformFileId },
                            treatSelectedRootsAsActive: true)
                        .ToArray();
                    if (staticRenderers.Length + skinnedRenderers.Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"Transport fixture '{spec.id}' selected no renderers.");
                    }

                    long[] wheelIds = spec.wheelTransformFileIds ??
                        Array.Empty<long>();
                    foreach (long wheelId in wheelIds)
                    {
                        scene.GetTransform(wheelId);
                    }

                    long[] requiredTransformIds = wheelIds;
                    if (spec.kind == (int)TrafficTransportKind.Bus)
                    {
                        requiredTransformIds = wheelIds.Concat(new[]
                            {
                                spec.seatedDriverTransformFileId,
                                spec.walkerTransformFileId,
                                spec.doorPivotTransformFileId,
                                spec.drivingLightsTransformFileId,
                                spec.deadLightsTransformFileId,
                            })
                            .Distinct()
                            .ToArray();
                        foreach (long transformId in requiredTransformIds)
                        {
                            scene.GetTransform(transformId);
                        }
                    }

                    return new TransportFixtureSource(
                        spec,
                        skinnedRenderers,
                        staticRenderers,
                        wheelIds,
                        requiredTransformIds);
                })
                .ToArray();
        }

        private static void EnsureTransportOutputFolders()
        {
            EnsureAssetFolder(TransportPrefabRoot);
        }

        private static IEnumerable<TrafficPresentationCatalogEntry>
            BuildTransportFixtures(
                DonorUnitySceneModel scene,
                IReadOnlyList<TransportFixtureSource> sources,
                IReadOnlyDictionary<string, string> importedMeshes,
                IReadOnlyDictionary<string, Material> materials)
        {
            foreach (TransportFixtureSource source in sources)
            {
                GameObject prefab = BuildTransportFixture(
                    scene,
                    source,
                    importedMeshes,
                    materials);
                yield return new TrafficPresentationCatalogEntry(
                    source.Spec.presentationId,
                    source.Spec.productionReplacementKey,
                    prefab);
            }
        }

        private static GameObject BuildTransportFixture(
            DonorUnitySceneModel scene,
            TransportFixtureSource source,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            TransportVehicleSpec spec = source.Spec;
            DonorActionSlice slice = scene.CreatePresentationSlice(
                spec.rootTransformFileId,
                spec.rootTransformFileId,
                source.SkinnedRenderers.Select(value => value.ComponentId)
                    .ToArray(),
                source.StaticRenderers.Select(value => value.ComponentId)
                    .ToArray(),
                source.RequiredTransformIds);
            var root = new GameObject("Phase 1 Transport " + spec.id);
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
                    bool fixtureRoot = donor.TransformId ==
                                       spec.rootTransformFileId;
                    node.transform.localPosition = fixtureRoot
                        ? Vector3.zero
                        : donor.LocalPosition;
                    node.transform.localRotation = fixtureRoot
                        ? Quaternion.identity
                        : donor.LocalRotation;
                    node.transform.localScale = donor.LocalScale;
                    created.Add(donor.TransformId, node.transform);
                }

                foreach (DonorSkinnedRendererRecord donor in slice.Renderers)
                {
                    DonorTransformRecord ownerRecord = slice.Transforms.Single(
                        value => value.GameObjectId == donor.GameObjectId);
                    Mesh mesh = RequireAsset<Mesh>(
                        importedMeshes[donor.MeshGuid]);
                    var renderer = created[ownerRecord.TransformId]
                        .gameObject.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = mesh;
                    renderer.sharedMaterials = donor.MaterialGuids
                        .Select(guid => materials[guid])
                        .ToArray();
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
                        value => value.GameObjectId == donor.GameObjectId);
                    GameObject owner = created[ownerRecord.TransformId]
                        .gameObject;
                    Mesh mesh = RequireAsset<Mesh>(
                        importedMeshes[donor.MeshGuid]);
                    owner.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = owner.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = donor.MaterialGuids
                        .Select(guid => materials[guid])
                        .ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                TrafficTransportKind kind =
                    (TrafficTransportKind)spec.kind;
                StoryTrafficVehiclePresentationBinding roadMotion = null;
                Rigidbody body;
                Transform seat = null;
                Transform exit = null;
                GameObject seatedDriverRoot = null;
                LegacyCharacterPresentationBinding driverWalker = null;
                Transform driverExit = null;
                Transform serviceDoorPivot = null;
                GameObject drivingLightsRoot = null;
                GameObject deadLightsRoot = null;
                if (kind == TrafficTransportKind.Bus)
                {
                    Transform fixtureRoot = RequireTransform(
                        created,
                        spec.rootTransformFileId);
                    Transform[] donorWheelRoots = source.WheelTransformIds
                        .Select(id => RequireTransform(created, id))
                        .ToArray();
                    Transform[] wheels = CreateWheelSpinPivots(
                        donorWheelRoots);
                    float groundContact = CalibrateWheelContact(
                        root.transform,
                        fixtureRoot,
                        wheels,
                        spec.maximumGroundCorrectionMeters);
                    roadMotion = root.AddComponent<
                        StoryTrafficVehiclePresentationBinding>();
                    roadMotion.ConfigureForAuthoring(
                        spec.driverFeatureId,
                        Array.Empty<string>(),
                        Array.Empty<GameObject>(),
                        wheels,
                        spec.wheelDegreesPerMeter,
                        groundContact);
                    roadMotion.ConfigureDrivingProfile(
                        6f,
                        spec.maximumSpeedMetersPerSecond,
                        2.2f,
                        8f,
                        75f,
                        -4f);
                    roadMotion.ConfigureRoadLanePolicy(
                        configuredBaseLaneOffsetMeters: 0f,
                        configuredPassingLaneOffsetMeters: -4f,
                        migrateLegacyCenterLane: true);
                    var dynamicSpec = new VehicleSpec
                    {
                        id = "traffic-bus",
                        wheelDegreesPerMeter = spec.wheelDegreesPerMeter,
                        maximumGroundCorrectionMeters =
                            spec.maximumGroundCorrectionMeters,
                        isAmbientTraffic = true,
                        provisionalMassKilograms = spec.massKilograms,
                    };
                    ConfigureDynamicVehicle(
                        root,
                        dynamicSpec,
                        wheels,
                        roadMotion);
                    body = root.GetComponent<Rigidbody>();

                    Bounds bounds = CalculateLocalRendererBounds(root.transform);
                    seat = CreateAnchor(
                        root.transform,
                        "PassengerSeat",
                        bounds.center + new Vector3(
                            -bounds.extents.x * 0.35f,
                            0.15f,
                            -bounds.extents.z * 0.12f));
                    exit = CreateAnchor(
                        root.transform,
                        "PassengerExit",
                        bounds.center + new Vector3(
                            bounds.extents.x + 1.2f,
                             -bounds.extents.y,
                             bounds.extents.z * 0.35f));

                    seatedDriverRoot = RequireTransform(
                            created,
                            spec.seatedDriverTransformFileId)
                        .gameObject;
                    serviceDoorPivot = RequireTransform(
                        created,
                        spec.doorPivotTransformFileId);
                    drivingLightsRoot = RequireTransform(
                            created,
                            spec.drivingLightsTransformFileId)
                        .gameObject;
                    deadLightsRoot = RequireTransform(
                            created,
                            spec.deadLightsTransformFileId)
                        .gameObject;

                    CharacterPresentationCatalog characterCatalog =
                        RequireAsset<CharacterPresentationCatalog>(CatalogPath);
                    if (!characterCatalog.TryGet(
                            BusDriverWalkerBindingId,
                            out CharacterPresentationCatalogEntry walkerEntry) ||
                        walkerEntry.WrapperPrefab == null)
                    {
                        throw new InvalidOperationException(
                            $"Character presentation catalog has no explicit " +
                            $"bus-driver walker '{BusDriverWalkerBindingId}'.");
                    }

                    GameObject walkerTemplate = walkerEntry.WrapperPrefab;
                    driverExit = RequireTransform(
                        created,
                        spec.walkerTransformFileId);
                    driverExit.name = "DriverExit";
                    GameObject walkerRoot = PrefabUtility.InstantiatePrefab(
                        walkerTemplate,
                        driverExit) as GameObject;
                    if (walkerRoot == null)
                    {
                        throw new InvalidOperationException(
                            "Could not instantiate the explicit Latanen bus-driver walker prefab.");
                    }

                    walkerRoot.name = "BusDriverWalker";
                    walkerRoot.transform.localPosition = Vector3.zero;
                    walkerRoot.transform.localRotation = Quaternion.identity;
                    driverWalker = walkerRoot.GetComponent<
                        LegacyCharacterPresentationBinding>() ??
                        throw new InvalidOperationException(
                            $"Bus-driver walker '{BusDriverWalkerBindingId}' has no character binding.");
                    if (!driverWalker.HasStateBinding(
                            CharacterActivityState.Idle) ||
                        !driverWalker.HasStateBinding(
                            CharacterActivityState.Walking))
                    {
                        throw new InvalidOperationException(
                            $"Bus-driver walker '{BusDriverWalkerBindingId}' " +
                            "must bind explicit Idle and Walking clips.");
                    }

                    driverWalker.ApplyState(CharacterActivityState.Hidden);
                }
                else
                {
                    body = root.AddComponent<Rigidbody>();
                    body.mass = spec.massKilograms;
                    body.useGravity = kind != TrafficTransportKind.Boat;
                    body.isKinematic = kind == TrafficTransportKind.Train;
                    body.linearDamping = kind == TrafficTransportKind.Boat
                        ? 0.1f
                        : 0f;
                    body.angularDamping = kind == TrafficTransportKind.Boat
                        ? 0.1f
                        : 0.05f;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode = kind ==
                        TrafficTransportKind.Train
                            ? CollisionDetectionMode.ContinuousSpeculative
                            : CollisionDetectionMode.ContinuousDynamic;
                    Bounds bounds = CalculateLocalRendererBounds(root.transform);
                    BoxCollider collider = root.AddComponent<BoxCollider>();
                    collider.center = bounds.center;
                    collider.size = bounds.size;
                }

                var transport = root.AddComponent<
                    TrafficTransportPresentationBinding>();
                Vector3 serviceDoorOpenLocalPosition = Vector3.zero;
                Quaternion serviceDoorOpenLocalRotation = Quaternion.identity;
                float serviceDoorOpenDurationRealSeconds = 1f;
                if (kind == TrafficTransportKind.Bus)
                {
                    serviceDoorOpenLocalPosition = ToTransportVector3(
                        spec.serviceDoorOpenLocalPosition,
                        spec.id);
                    serviceDoorOpenLocalRotation = ToTransportQuaternion(
                        spec.serviceDoorOpenLocalRotation,
                        spec.id);
                    serviceDoorOpenDurationRealSeconds =
                        spec.serviceDoorOpenDurationRealSeconds;
                }

                transport.ConfigureForAuthoring(
                    kind,
                    body,
                    roadMotion,
                    seat,
                    exit,
                    seatedDriverRoot,
                    driverWalker,
                    driverExit,
                    serviceDoorPivot,
                    drivingLightsRoot,
                    deadLightsRoot,
                    serviceDoorOpenLocalPosition,
                    serviceDoorOpenLocalRotation,
                    serviceDoorOpenDurationRealSeconds);
                if (kind == TrafficTransportKind.Bus)
                {
                    InteractionTargetHost host = root.AddComponent<
                        InteractionTargetHost>();
                    var target = root.AddComponent<
                        TrafficBusPassengerInteractionTarget>();
                    target.ConfigureForAuthoring(transport);
                    host.Configure(target);
                }

                string prefabPath = TransportPrefabRoot + "/" +
                                    spec.id + ".prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath) ?? throw new InvalidOperationException(
                    $"Could not save transport prefab '{prefabPath}'.");
                ValidateTransportWrapper(spec, prefab);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Bounds CalculateLocalRendererBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "Transport fixture has no renderer bounds.");
            }

            bool initialized = false;
            Bounds result = default;
            foreach (Renderer renderer in renderers)
            {
                Bounds world = renderer.bounds;
                for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 local = root.InverseTransformPoint(new Vector3(
                        x == 0 ? world.min.x : world.max.x,
                        y == 0 ? world.min.y : world.max.y,
                        z == 0 ? world.min.z : world.max.z));
                    if (!initialized)
                    {
                        result = new Bounds(local, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        result.Encapsulate(local);
                    }
                }
            }

            result.Expand(0.08f);
            return result;
        }

        private static Transform CreateAnchor(
            Transform parent,
            string name,
            Vector3 localPosition)
        {
            var anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            return anchor.transform;
        }

        private static void ValidateTransportWrapper(
            TransportVehicleSpec spec,
            GameObject wrapper)
        {
            TrafficTransportPresentationBinding binding = wrapper
                .GetComponent<TrafficTransportPresentationBinding>();
            string failure = string.Empty;
            if (binding == null || !binding.TryValidate(out failure) ||
                wrapper.GetComponent<Rigidbody>() == null ||
                wrapper.GetComponent<BoxCollider>() == null)
            {
                throw new InvalidOperationException(
                    $"Transport fixture '{spec.id}' is invalid: {failure}");
            }
        }

        private static bool TryValidateTransportManifest(
            IReadOnlyList<TransportVehicleSpec> specs)
        {
            if (specs == null || specs.Count != 4 ||
                specs.Select(value => value.id)
                    .Distinct(StringComparer.Ordinal).Count() != specs.Count ||
                specs.Select(value => value.presentationId)
                    .Distinct(StringComparer.Ordinal).Count() != specs.Count)
            {
                return false;
            }

            return specs.All(spec => spec != null &&
                !string.IsNullOrWhiteSpace(spec.id) &&
                !string.IsNullOrWhiteSpace(spec.donorRootName) &&
                !string.IsNullOrWhiteSpace(spec.presentationId) &&
                spec.presentationId.StartsWith(
                    "presentation.traffic.",
                    StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(spec.productionReplacementKey) &&
                spec.rootTransformFileId > 0L &&
                spec.kind >= 0 && spec.kind <= 2 &&
                float.IsFinite(spec.massKilograms) &&
                spec.massKilograms >= 40f &&
                (spec.kind == (int)TrafficTransportKind.Bus ?
                 !string.IsNullOrWhiteSpace(spec.driverFeatureId) &&
                 spec.wheelTransformFileIds != null &&
                 spec.wheelTransformFileIds.Length == 4 &&
                 spec.wheelTransformFileIds.Distinct().Count() == 4 &&
                 float.IsFinite(spec.wheelDegreesPerMeter) &&
                 spec.wheelDegreesPerMeter > 0f &&
                 float.IsFinite(spec.maximumGroundCorrectionMeters) &&
                 spec.maximumGroundCorrectionMeters > 0f &&
                 spec.maximumGroundCorrectionMeters <= 1f &&
                 float.IsFinite(spec.maximumSpeedMetersPerSecond) &&
                 spec.maximumSpeedMetersPerSecond > 0f &&
                 spec.seatedDriverTransformFileId > 0L &&
                 spec.walkerTransformFileId > 0L &&
                 spec.doorPivotTransformFileId > 0L &&
                 spec.drivingLightsTransformFileId > 0L &&
                 spec.deadLightsTransformFileId > 0L &&
                 IsTransportVector3(spec.serviceDoorOpenLocalPosition) &&
                 IsTransportQuaternion(spec.serviceDoorOpenLocalRotation) &&
                 float.IsFinite(spec.serviceDoorOpenDurationRealSeconds) &&
                 spec.serviceDoorOpenDurationRealSeconds > 0f :
                 HasNoBusDoorAuthoring(spec)));
        }

        private static bool HasNoBusDoorAuthoring(TransportVehicleSpec spec) =>
            (spec.serviceDoorOpenLocalPosition == null ||
             spec.serviceDoorOpenLocalPosition.Length == 0) &&
            (spec.serviceDoorOpenLocalRotation == null ||
             spec.serviceDoorOpenLocalRotation.Length == 0) &&
            spec.serviceDoorOpenDurationRealSeconds == 0f;

        private static bool IsTransportVector3(float[] values) =>
            values != null && values.Length == 3 && values.All(float.IsFinite);

        private static Vector3 ToTransportVector3(
            float[] values,
            string transportId)
        {
            if (!IsTransportVector3(values))
            {
                throw new InvalidOperationException(
                    $"Bus transport '{transportId}' has an invalid service-door " +
                    "local position; exactly three finite values are required.");
            }

            return new Vector3(values[0], values[1], values[2]);
        }

        private static bool IsTransportQuaternion(float[] values) =>
            values != null && values.Length == 4 && values.All(float.IsFinite) &&
            values.Sum(value => value * value) > 0.0001f;

        private static Quaternion ToTransportQuaternion(
            float[] values,
            string transportId)
        {
            if (!IsTransportQuaternion(values))
            {
                throw new InvalidOperationException(
                    $"Bus transport '{transportId}' has an invalid service-door " +
                    "local rotation; a finite non-zero quaternion is required.");
            }

            return new Quaternion(values[0], values[1], values[2], values[3]);
        }

        [Serializable]
        private sealed class TransportVehicleSpec
        {
            public string id;
            public string donorRootName;
            public string presentationId;
            public string productionReplacementKey;
            public int kind;
            public string driverFeatureId;
            public long rootTransformFileId;
            public long[] wheelTransformFileIds;
            public long seatedDriverTransformFileId;
            public long walkerTransformFileId;
            public long doorPivotTransformFileId;
            public long drivingLightsTransformFileId;
            public long deadLightsTransformFileId;
            public float[] serviceDoorOpenLocalPosition;
            public float[] serviceDoorOpenLocalRotation;
            public float serviceDoorOpenDurationRealSeconds;
            public float wheelDegreesPerMeter;
            public float maximumGroundCorrectionMeters;
            public float maximumSpeedMetersPerSecond;
            public float massKilograms;
        }

        private sealed class TransportFixtureSource
        {
            public TransportFixtureSource(
                TransportVehicleSpec spec,
                DonorSkinnedRendererRecord[] skinnedRenderers,
                DonorStaticRendererRecord[] staticRenderers,
                long[] wheelTransformIds,
                long[] requiredTransformIds)
            {
                Spec = spec;
                SkinnedRenderers = skinnedRenderers;
                StaticRenderers = staticRenderers;
                WheelTransformIds = wheelTransformIds;
                RequiredTransformIds = requiredTransformIds;
            }

            public TransportVehicleSpec Spec { get; }
            public DonorSkinnedRendererRecord[] SkinnedRenderers { get; }
            public DonorStaticRendererRecord[] StaticRenderers { get; }
            public long[] WheelTransformIds { get; }
            public long[] RequiredTransformIds { get; }
        }
    }
}
