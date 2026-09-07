using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Query;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Applies the reviewed donor bolt meshes to the already-correct oilpan and
    /// gearbox fastener contracts. This rule is presentation-only: it never
    /// initializes the assembly graph or rewrites definitions and save state.
    /// </summary>
    internal static class Phase1SatsumaEngineFastenerPresentation
    {
        internal const string DefaultNutMeshSourceGuid =
            "e711c8a15b1135c4089caad19b8f56e8";
        internal const string ShortBoltMeshSourceGuid =
            "aec6c756751308a4d830708366ad5cdb";
        internal const string LongBoltMeshSourceGuid =
            "bd64aade39680ac43a380f1c62373e0b";
        internal const string FastenerMaterialSourceGuid =
            "98697bae08a8c114ba9774c487f2658d";

        internal const string OilpanMountId =
            "mount.satsuma.engine-block.oilpan";
        internal const string GearboxMountId =
            "mount.satsuma.engine-block.gearbox";

        private const string EngineBlockPartId =
            "vehicle.satsuma.part.engine-block";
        private const string OilpanPartId = "vehicle.satsuma.part.oilpan";
        private const string GearboxPartId = "vehicle.satsuma.part.gearbox";
        private const string PresentationName = "Visible inserted bolt or nut";
        private const int ExpectedFastenerTargetCount = 280;
        private const float PoseTolerance = 0.00001f;
        private const float RotationToleranceDegrees = 0.001f;

        private static readonly IReadOnlyList<ReviewedFastenerBinding>
            Bindings = Array.AsReadOnly(new[]
            {
                Oilpan(1, 40758L, FastenerSize.Millimeter7),
                Oilpan(2, 44950L, FastenerSize.Millimeter7),
                Oilpan(
                    3,
                    53276L,
                    FastenerSize.Millimeter13,
                    new Vector3(1.3f, 1.3f, 0.8f),
                    0.03375f),
                Oilpan(4, 54874L, FastenerSize.Millimeter7),
                Oilpan(5, 56771L, FastenerSize.Millimeter7),
                Oilpan(6, 56810L, FastenerSize.Millimeter7),
                Oilpan(7, 58044L, FastenerSize.Millimeter7),
                Oilpan(8, 58670L, FastenerSize.Millimeter7),
                Oilpan(9, 61811L, FastenerSize.Millimeter7),
                Gearbox(1, 36907L, FastenerSize.Millimeter7,
                    LongBoltMeshSourceGuid),
                Gearbox(2, 38331L, FastenerSize.Millimeter7,
                    LongBoltMeshSourceGuid),
                Gearbox(3, 42660L, FastenerSize.Millimeter7,
                    LongBoltMeshSourceGuid),
                Gearbox(4, 44837L, FastenerSize.Millimeter7,
                    LongBoltMeshSourceGuid),
                Gearbox(
                    5,
                    51824L,
                    FastenerSize.Millimeter10,
                    ShortBoltMeshSourceGuid,
                    new Vector3(1f, 1f, 0.8f)),
                Gearbox(6, 67087L, FastenerSize.Millimeter7,
                    LongBoltMeshSourceGuid),
                Gearbox(7, 68100L, FastenerSize.Millimeter7,
                    LongBoltMeshSourceGuid),
            });

        internal static IReadOnlyList<ReviewedFastenerBinding>
            ReviewedBindings => Bindings;

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Engine Fastener Presentation")]
        public static void RefreshEngineFastenerPresentationBatch()
        {
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string prefabPath = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                int changed = ApplyReviewedMeshes(
                    contents,
                    "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma");
                if (changed > 0)
                {
                    Directory.CreateDirectory("Logs");
                    string backup = "Logs/codex-engine-fasteners-before-" +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") +
                        ".prefab";
                    File.Copy(prefabPath, backup, overwrite: false);
                    if (PrefabUtility.SaveAsPrefabAsset(contents, prefabPath) == null)
                    {
                        throw new InvalidDataException(
                            "E2a could not save the scoped Satsuma prefab.");
                    }
                }

                Debug.Log(
                    "PHASE1_SATSUMA_ENGINE_FASTENER_PRESENTATION_REFRESH_OK " +
                    "changedMeshFilters=" + changed +
                    " fullRebuild=false definitionsUnchanged=true " +
                    "manifestUnchanged=true");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        internal static int ApplyReviewedMeshes(
            GameObject root,
            string generatedRoot)
        {
            ValidateBindingTable();
            if (root == null || string.IsNullOrWhiteSpace(generatedRoot))
            {
                throw new InvalidDataException(
                    "E2a requires a generated Satsuma root and asset path.");
            }

            generatedRoot = generatedRoot.TrimEnd('/', '\\');
            ValidateStableVehicleRoot(root);
            VehicleAssemblyController assembly = RequireAssembly(root);
            IReadOnlyDictionary<string, MountPointAuthoring> mounts =
                RequireCanonicalMounts(assembly);
            IReadOnlyDictionary<string, AssemblyFastenerInteractionTarget> targets =
                RequireCanonicalFastenerTargets(root, assembly);
            Mesh oldNut = RequireMesh(
                generatedRoot,
                DefaultNutMeshSourceGuid,
                "bolt2");
            Mesh shortBolt = RequireMesh(
                generatedRoot,
                ShortBoltMeshSourceGuid,
                "bolt");
            Mesh longBolt = RequireMesh(
                generatedRoot,
                LongBoltMeshSourceGuid,
                "bolt3");
            Material material = RequireAsset<Material>(
                generatedRoot + "/Materials/" +
                FastenerMaterialSourceGuid + ".mat");

            ValidateMountContract(
                mounts[OilpanMountId],
                Bindings.Where(value => value.MountId == OilpanMountId).ToArray(),
                OilpanPartId,
                generatedRoot,
                expectedAggregateTightness: 72);
            ValidateMountContract(
                mounts[GearboxMountId],
                Bindings.Where(value => value.MountId == GearboxMountId).ToArray(),
                GearboxPartId,
                generatedRoot,
                expectedAggregateTightness: 56);

            var changes = new List<(MeshFilter filter, Mesh mesh)>(Bindings.Count);
            foreach (ReviewedFastenerBinding binding in Bindings)
            {
                AssemblyFastenerInteractionTarget target = targets[binding.FastenerId];
                Mesh expectedMesh = binding.ExpectedMeshSourceGuid ==
                    ShortBoltMeshSourceGuid
                        ? shortBolt
                        : longBolt;
                MeshFilter filter = ValidatePresentation(
                    target,
                    mounts[binding.MountId],
                    binding,
                    assembly,
                    material);
                if (filter.sharedMesh != oldNut && filter.sharedMesh != expectedMesh)
                {
                    throw new InvalidDataException(
                        "E2a refuses a null, foreign, or wrong bolt mesh on " +
                        binding.FastenerId + ".");
                }

                if (filter.sharedMesh == oldNut)
                {
                    changes.Add((filter, expectedMesh));
                }
            }

            // Every target is validated before the first serialized mutation.
            foreach ((MeshFilter filter, Mesh mesh) in changes)
            {
                filter.sharedMesh = mesh;
                EditorUtility.SetDirty(filter);
            }

            return changes.Count;
        }

        private static MeshFilter ValidatePresentation(
            AssemblyFastenerInteractionTarget target,
            MountPointAuthoring mount,
            ReviewedFastenerBinding binding,
            VehicleAssemblyController assembly,
            Material expectedMaterial)
        {
            if (target == null || target.Controller != assembly ||
                target.MountId != binding.MountId ||
                target.FastenerDefinitionId != binding.FastenerId ||
                target.transform.name != binding.FastenerId ||
                target.transform.parent != mount.transform ||
                !target.gameObject.activeSelf ||
                target.transform.childCount != 1 ||
                !Approximately(target.transform.localScale, Vector3.one) ||
                !IsFinite(target.transform.localPosition) ||
                !IsFinite(target.transform.localRotation))
            {
                throw new InvalidDataException(
                    "E2a refuses drifted target ownership or marker frame: " +
                    binding.FastenerId);
            }

            Component[] markerComponents = target.GetComponents<Component>();
            SphereCollider[] colliders = target.GetComponents<SphereCollider>();
            InteractionTargetHost[] hosts = target.GetComponents<InteractionTargetHost>();
            if (markerComponents.Length != 4 || colliders.Length != 1 ||
                hosts.Length != 1 || markerComponents.Any(value => value == null) ||
                markerComponents.Count(value => value is Transform) != 1 ||
                markerComponents.Count(value => value is SphereCollider) != 1 ||
                markerComponents.Count(value =>
                    value is AssemblyFastenerInteractionTarget) != 1 ||
                markerComponents.Count(value => value is InteractionTargetHost) != 1)
            {
                throw new InvalidDataException(
                    "E2a refuses drifted target component shape: " +
                    binding.FastenerId);
            }

            SphereCollider collider = colliders[0];
            if (!collider.isTrigger || collider.enabled ||
                !Approximately(collider.center, Vector3.zero) ||
                !float.IsFinite(collider.radius) ||
                Mathf.Abs(collider.radius - binding.ExpectedColliderRadius) >
                PoseTolerance)
            {
                throw new InvalidDataException(
                    "E2a refuses drifted fastener collider: " +
                    binding.FastenerId);
            }

            var serializedTarget = new SerializedObject(target);
            SerializedProperty controllerProperty =
                serializedTarget.FindProperty("controller");
            SerializedProperty presentationProperty =
                serializedTarget.FindProperty("fastenerPresentation");
            SerializedProperty basePositionProperty =
                serializedTarget.FindProperty(
                    "fastenerPresentationBaseLocalPosition");
            SerializedProperty baseRotationProperty =
                serializedTarget.FindProperty(
                    "fastenerPresentationBaseLocalRotation");
            SerializedProperty travelProperty =
                serializedTarget.FindProperty(
                    "fastenerPresentationStageTravelScale");
            SerializedProperty reverseProperty =
                serializedTarget.FindProperty("automaticReverseAtLimits");
            SerializedProperty toolProperty = serializedTarget.FindProperty("tool");
            Transform presentation = presentationProperty?.objectReferenceValue
                as Transform;
            ToolDefinition tool = toolProperty?.objectReferenceValue as ToolDefinition;
            if (controllerProperty?.objectReferenceValue != assembly ||
                presentation == null || presentation.parent != target.transform ||
                presentation.name != PresentationName ||
                presentation.childCount != 0 || !presentation.gameObject.activeSelf ||
                !Approximately(presentation.localPosition, Vector3.zero) ||
                !IsFinite(presentation.localRotation) ||
                Quaternion.Angle(presentation.localRotation, Quaternion.identity) >
                RotationToleranceDegrees ||
                !Approximately(
                    presentation.localScale,
                    binding.ExpectedPresentationScale) ||
                basePositionProperty == null ||
                !Approximately(basePositionProperty.vector3Value, Vector3.zero) ||
                baseRotationProperty == null ||
                !IsFinite(baseRotationProperty.quaternionValue) ||
                Quaternion.Angle(
                    baseRotationProperty.quaternionValue,
                    Quaternion.identity) > RotationToleranceDegrees ||
                travelProperty == null ||
                !float.IsFinite(travelProperty.floatValue) ||
                !Phase1SatsumaEngineFastenerTravel.IsLegacyOrReviewedScale(
                    binding.FastenerId, travelProperty.floatValue) ||
                reverseProperty == null || !reverseProperty.boolValue ||
                tool == null || tool.ToolType != "Wrench" ||
                tool.Size != binding.ExpectedSize)
            {
                throw new InvalidDataException(
                    "E2a refuses drifted presentation binding or base pose: " +
                    binding.FastenerId);
            }

            Component[] visualComponents = presentation.GetComponents<Component>();
            MeshFilter[] filters = presentation.GetComponents<MeshFilter>();
            MeshRenderer[] renderers = presentation.GetComponents<MeshRenderer>();
            if (visualComponents.Length != 3 || filters.Length != 1 ||
                renderers.Length != 1 ||
                visualComponents.Any(value => value == null) ||
                visualComponents.Count(value => value is Transform) != 1 ||
                visualComponents.Count(value => value is MeshFilter) != 1 ||
                visualComponents.Count(value => value is MeshRenderer) != 1)
            {
                throw new InvalidDataException(
                    "E2a refuses drifted fastener presenter shape: " +
                    binding.FastenerId);
            }

            MeshRenderer renderer = renderers[0];
            InteractionTargetHost host = hosts[0];
            if (renderer.sharedMaterials.Length != 1 ||
                renderer.sharedMaterial != expectedMaterial || renderer.enabled ||
                host.SelectionPriority != 40 || host.OutlineRenderers.Count != 1 ||
                host.OutlineRenderers[0] != renderer ||
                !host.TryGetCapability<AssemblyFastenerInteractionTarget>(
                    out AssemblyFastenerInteractionTarget capability) ||
                capability != target)
            {
                throw new InvalidDataException(
                    "E2a refuses drifted material, outline, or capability binding: " +
                    binding.FastenerId);
            }

            return filters[0];
        }

        private static void ValidateMountContract(
            MountPointAuthoring mount,
            IReadOnlyList<ReviewedFastenerBinding> bindings,
            string acceptedPartId,
            string generatedRoot,
            int expectedAggregateTightness)
        {
            MountPointDefinition definition = mount != null
                ? mount.Definition
                : null;
            string mountId = bindings.Count > 0 ? bindings[0].MountId : string.Empty;
            string expectedPath = generatedRoot + "/MountDefinitions/" +
                mountId + ".asset";
            if (definition == null || mount.MountId != mountId ||
                definition.DefinitionId != mountId ||
                definition.OwnerPartDefinitionId != EngineBlockPartId ||
                definition.AcceptedPartDefinitionIds.Length != 1 ||
                definition.AcceptedPartDefinitionIds[0] != acceptedPartId ||
                AssetDatabase.GetAssetPath(definition) != expectedPath ||
                definition.Fasteners.Length != bindings.Count)
            {
                throw new InvalidDataException(
                    "E2a refuses drifted mount binding: " + mountId);
            }

            for (int index = 0; index < bindings.Count; index++)
            {
                ReviewedFastenerBinding binding = bindings[index];
                FastenerDefinition fastener = definition.Fasteners[index];
                string fastenerPath = generatedRoot + "/FastenerDefinitions/" +
                    binding.FastenerId + ".asset";
                if (fastener == null || fastener.DefinitionId != binding.FastenerId ||
                    fastener.Size != binding.ExpectedSize ||
                    fastener.MaximumStage != 8 || !fastener.InsertedOnInstall ||
                    !fastener.RequiredForRemoval ||
                    fastener.TighteningDirection !=
                    FastenerDirection.ClockwiseToTighten ||
                    fastener.ToolRule == null ||
                    fastener.ToolRule.ToolType != "Wrench" ||
                    fastener.ToolRule.FastenerSize != binding.ExpectedSize ||
                    AssetDatabase.GetAssetPath(fastener) != fastenerPath)
                {
                    throw new InvalidDataException(
                        "E2a refuses drifted fastener definition: " +
                        binding.FastenerId);
                }
            }

            FastenerGroupDefinition group = definition.FastenerGroup;
            if (group == null ||
                !group.FastenerDefinitionIds.SequenceEqual(
                    bindings.Select(value => value.FastenerId)) ||
                group.AggregateMaximumTightness != expectedAggregateTightness ||
                group.BoltedOnThreshold != 1 || group.BoltedOffThreshold != 0 ||
                group.SpeedRetentionPolicy != FastenerSpeedRetentionPolicy.None ||
                group.LooseBreakSpeedKph != 0f ||
                group.PartialCheckSpeedKph != 0f ||
                group.ChanceDivisor != 100f ||
                group.BreakAction != FastenerBreakAction.None)
            {
                throw new InvalidDataException(
                    "E2a refuses drifted fastener group: " + mountId);
            }
        }

        private static IReadOnlyDictionary<string, MountPointAuthoring>
            RequireCanonicalMounts(VehicleAssemblyController assembly)
        {
            MountPointAuthoring[] mounts = assembly.MountPoints;
            if (mounts == null ||
                mounts.Any(value => value == null ||
                    string.IsNullOrWhiteSpace(value.MountId)) ||
                !Phase1SatsumaReviewedGraphShape.HasReviewedMountRoster(mounts.Select(value => value.MountId)))
            {
                throw new InvalidDataException(
                    "E2a requires the reviewed base or additive Satsuma mount roster.");
            }

            var byId = new Dictionary<string, MountPointAuthoring>(
                StringComparer.Ordinal);
            foreach (MountPointAuthoring mount in mounts)
            {
                if (!byId.TryAdd(mount.MountId, mount))
                {
                    throw new InvalidDataException(
                        "E2a refuses duplicate mount ID: " + mount.MountId);
                }
            }

            foreach (string mountId in new[] { OilpanMountId, GearboxMountId })
            {
                if (!byId.ContainsKey(mountId))
                {
                    throw new InvalidDataException(
                        "E2a is missing reviewed mount: " + mountId);
                }
            }

            return byId;
        }

        private static IReadOnlyDictionary<string, AssemblyFastenerInteractionTarget>
            RequireCanonicalFastenerTargets(
                GameObject root,
                VehicleAssemblyController assembly)
        {
            AssemblyFastenerInteractionTarget[] targets = root
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            int retired = Phase1SatsumaEngineAdditionalFastenerPresentation.ReviewedBindings.Count -
                Phase1SatsumaEngineAdditionalFastenerPresentation.ResolveActiveBindings(root).Count;
            if (SatsumaRockerShaftFastenerMigration.IsCanonicalShape(targets.Where(value =>
                    value.MountId == SatsumaRockerShaftFastenerMigration.MountId)
                .Select(value => value.FastenerDefinitionId).ToArray())) retired += 8;
            if (targets.Any(value => value == null ||
                    string.IsNullOrWhiteSpace(value.FastenerDefinitionId) ||
                    value.Controller != assembly) ||
                !Phase1SatsumaReviewedGraphShape.HasReviewedFastenerRoster(
                    targets.Select(value => value.FastenerDefinitionId), ExpectedFastenerTargetCount - retired))
            {
                throw new InvalidDataException(
                    "E2a requires the exact canonical fastener roster, including reviewed retirements.");
            }

            var byId = new Dictionary<string, AssemblyFastenerInteractionTarget>(
                StringComparer.Ordinal);
            foreach (AssemblyFastenerInteractionTarget target in targets)
            {
                if (!byId.TryAdd(target.FastenerDefinitionId, target))
                {
                    throw new InvalidDataException(
                        "E2a refuses duplicate fastener target ID: " +
                        target.FastenerDefinitionId);
                }
            }

            foreach (ReviewedFastenerBinding binding in Bindings)
            {
                if (!byId.ContainsKey(binding.FastenerId))
                {
                    throw new InvalidDataException(
                        "E2a is missing reviewed fastener target: " +
                        binding.FastenerId);
                }
            }

            return byId;
        }

        private static VehicleAssemblyController RequireAssembly(GameObject root)
        {
            VehicleAssemblyController[] assemblies =
                root.GetComponents<VehicleAssemblyController>();
            if (assemblies.Length != 1)
            {
                throw new InvalidDataException(
                    "E2a requires one root VehicleAssemblyController.");
            }

            return assemblies[0];
        }

        private static void ValidateStableVehicleRoot(GameObject root)
        {
            StableEntityIdAuthoring[] identities =
                root.GetComponents<StableEntityIdAuthoring>();
            if (identities.Length != 1 ||
                identities[0].SerializedId !=
                Phase1SatsumaBaselineBuilder.StableVehicleId)
            {
                throw new InvalidDataException(
                    "E2a requires the canonical Satsuma stable vehicle root.");
            }
        }

        private static Mesh RequireMesh(
            string generatedRoot,
            string sourceGuid,
            string expectedName)
        {
            Mesh mesh = RequireAsset<Mesh>(
                generatedRoot + "/Meshes/" + sourceGuid + ".asset");
            if (mesh.name != expectedName)
            {
                throw new InvalidDataException(
                    "E2a refuses drifted imported mesh identity: " + sourceGuid);
            }

            return mesh;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidDataException(
                    "E2a is missing reviewed asset: " + path);
            }

            return asset;
        }

        private static void ValidateBindingTable()
        {
            if (Bindings.Count != 16 ||
                Bindings.Select(value => value.FastenerId).Distinct(
                    StringComparer.Ordinal).Count() != Bindings.Count ||
                Bindings.Select(value => value.DonorMarkerTransformId).Distinct()
                    .Count() != Bindings.Count ||
                Bindings.Count(value => value.MountId == OilpanMountId) != 9 ||
                Bindings.Count(value => value.MountId == GearboxMountId) != 7 ||
                Bindings.Any(value =>
                    value.ExpectedMeshSourceGuid != ShortBoltMeshSourceGuid &&
                    value.ExpectedMeshSourceGuid != LongBoltMeshSourceGuid))
            {
                throw new InvalidDataException(
                    "E2a reviewed binding table is incomplete or ambiguous.");
            }
        }

        private static ReviewedFastenerBinding Oilpan(
            int index,
            long markerId,
            FastenerSize size,
            Vector3? scale = null,
            float colliderRadius = 0.028f) =>
            new ReviewedFastenerBinding(
                OilpanMountId,
                "fastener.satsuma.engine-block-oilpan.boltpm-" + index,
                markerId,
                ShortBoltMeshSourceGuid,
                size,
                scale ?? new Vector3(0.7f, 0.7f, 0.7f),
                colliderRadius,
                new Vector3(0f, 0f, -0.02f));

        private static ReviewedFastenerBinding Gearbox(
            int index,
            long markerId,
            FastenerSize size,
            string meshGuid,
            Vector3? scale = null) =>
            new ReviewedFastenerBinding(
                GearboxMountId,
                "fastener.satsuma.engine-block-gearbox.boltpm-" + index,
                markerId,
                meshGuid,
                size,
                scale ?? new Vector3(0.7f, 0.7f, 0.7f),
                0.028f,
                Vector3.zero);

        private static bool Approximately(Vector3 left, Vector3 right) =>
            Vector3.Distance(left, right) <= PoseTolerance;

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w) &&
            Mathf.Abs(Quaternion.Dot(value, value) - 1f) <= PoseTolerance;

        internal readonly struct ReviewedFastenerBinding
        {
            public ReviewedFastenerBinding(
                string mountId,
                string fastenerId,
                long donorMarkerTransformId,
                string expectedMeshSourceGuid,
                FastenerSize expectedSize,
                Vector3 expectedPresentationScale,
                float expectedColliderRadius,
                Vector3 expectedDonorRendererChildLocalPosition)
            {
                MountId = mountId;
                FastenerId = fastenerId;
                DonorMarkerTransformId = donorMarkerTransformId;
                ExpectedMeshSourceGuid = expectedMeshSourceGuid;
                ExpectedSize = expectedSize;
                ExpectedPresentationScale = expectedPresentationScale;
                ExpectedColliderRadius = expectedColliderRadius;
                ExpectedDonorRendererChildLocalPosition =
                    expectedDonorRendererChildLocalPosition;
            }

            public string MountId { get; }
            public string FastenerId { get; }
            public long DonorMarkerTransformId { get; }
            public string ExpectedMeshSourceGuid { get; }
            public FastenerSize ExpectedSize { get; }
            public Vector3 ExpectedPresentationScale { get; }
            public float ExpectedColliderRadius { get; }
            public Vector3 ExpectedDonorRendererChildLocalPosition { get; }
        }
    }
}
