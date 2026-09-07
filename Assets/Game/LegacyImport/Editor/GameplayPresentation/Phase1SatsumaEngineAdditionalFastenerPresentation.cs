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
    /// Reviewed mesh identity for existing engine targets beyond E2a. This is
    /// deliberately not an ownership repair: the cover's duplicate IDs and the
    /// shaft's leaked adjusters require a separate compatible state migration.
    /// </summary>
    internal static class Phase1SatsumaEngineAdditionalFastenerPresentation
    {
        internal const string Nut = Phase1SatsumaEngineFastenerPresentation.DefaultNutMeshSourceGuid;
        internal const string Short = Phase1SatsumaEngineFastenerPresentation.ShortBoltMeshSourceGuid;
        internal const string Long = Phase1SatsumaEngineFastenerPresentation.LongBoltMeshSourceGuid;
        internal const string Slotted = "d42cbf361095bad4f9091a43a07fe91a";
        internal const string MaterialGuid = Phase1SatsumaEngineFastenerPresentation.FastenerMaterialSourceGuid;
        private const string GeneratedRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        private static readonly IReadOnlyList<Binding> Bindings = CreateBindings();

        internal static IReadOnlyList<Binding> ReviewedBindings => Bindings;

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Additional Engine Fastener Meshes")]
        public static void RefreshAdditionalEngineFastenerMeshesBatch()
        {
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyReviewedMeshes(root, GeneratedRoot);
                if (changed > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/codex-engine-additional-fasteners-before-" +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    {
                        throw new InvalidDataException("Engine fastener mesh refresh could not save the prefab.");
                    }
                }

                Debug.Log("PHASE1_SATSUMA_ENGINE_ADDITIONAL_FASTENER_MESH_REFRESH_OK changedMeshFilters=" +
                    changed + " reviewedTargets=100 definitionsUnchanged=true fullRebuild=false");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static int ApplyReviewedMeshes(GameObject root, string generatedRoot)
        {
            if (root == null || string.IsNullOrWhiteSpace(generatedRoot))
            {
                throw new InvalidDataException("Engine fastener mesh refresh requires a canonical root and asset path.");
            }

            StableEntityIdAuthoring[] identities = root.GetComponents<StableEntityIdAuthoring>();
            VehicleAssemblyController[] controllers = root.GetComponents<VehicleAssemblyController>();
            if (identities.Length != 1 || identities[0].SerializedId != Phase1SatsumaBaselineBuilder.StableVehicleId ||
                controllers.Length != 1)
            {
                throw new InvalidDataException("Engine fastener mesh refresh refuses a foreign vehicle root.");
            }

            VehicleAssemblyController controller = controllers[0];
            var targets = new Dictionary<string, AssemblyFastenerInteractionTarget>(StringComparer.Ordinal);
            foreach (AssemblyFastenerInteractionTarget target in root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true))
            {
                if (string.IsNullOrWhiteSpace(target.FastenerDefinitionId) ||
                    !targets.TryAdd(target.FastenerDefinitionId, target))
                {
                    throw new InvalidDataException("Engine fastener mesh refresh refuses ambiguous target identity.");
                }
            }

            var mounts = new Dictionary<string, MountPointAuthoring>(StringComparer.Ordinal);
            foreach (MountPointAuthoring mount in controller.MountPoints)
            {
                if (mount == null || string.IsNullOrWhiteSpace(mount.MountId) || !mounts.TryAdd(mount.MountId, mount))
                {
                    throw new InvalidDataException("Engine fastener mesh refresh refuses ambiguous mount identity.");
                }
            }

            generatedRoot = generatedRoot.TrimEnd('/', '\\');
            var meshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            foreach ((string guid, string name) in new[] { (Nut, "bolt2"), (Short, "bolt"), (Long, "bolt3"), (Slotted, "bolt5") })
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(generatedRoot + "/Meshes/" + guid + ".asset");
                if (mesh == null || mesh.name != name)
                {
                    throw new InvalidDataException("Missing or changed reviewed engine mesh: " + guid);
                }
                meshes.Add(guid, mesh);
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(generatedRoot + "/Materials/" + MaterialGuid + ".mat");
            if (material == null)
            {
                throw new InvalidDataException("Missing reviewed engine fastener material.");
            }

            var changes = new List<(MeshFilter filter, Mesh mesh)>();
            var presentationIds = new HashSet<EntityId>();
            foreach (Binding binding in ResolveActiveBindings(root))
            {
                if (!targets.TryGetValue(binding.FastenerId, out AssemblyFastenerInteractionTarget target) ||
                    !mounts.TryGetValue(binding.MountId, out MountPointAuthoring mount) || mount.Definition == null ||
                    target.Controller != controller || target.MountId != binding.MountId ||
                    target.transform.parent != mount.transform || target.transform.name != binding.FastenerId)
                {
                    throw new InvalidDataException("Missing or changed reviewed engine binding: " + binding.FastenerId);
                }

                FastenerDefinition[] definitions = mount.Definition.Fasteners.Where(value =>
                    value != null && value.DefinitionId == binding.FastenerId).ToArray();
                if (definitions.Length != 1 || (int)definitions[0].Size != binding.SizeMillimeters)
                {
                    throw new InvalidDataException("Changed reviewed engine fastener definition: " + binding.FastenerId);
                }

                var serialized = new SerializedObject(target);
                Transform presentation = serialized.FindProperty("fastenerPresentation")?.objectReferenceValue as Transform;
                if (presentation == null || presentation.parent != target.transform ||
                !presentationIds.Add(presentation.GetEntityId()) || presentation.childCount != 0 ||
                    Vector3.Distance(presentation.localScale, binding.PresentationScale) > 0.00001f ||
                    !Finite(presentation.localPosition) || !Finite(target.transform.localPosition) ||
                    !Finite(serialized.FindProperty("fastenerPresentationBaseLocalPosition").vector3Value) ||
                    !float.IsFinite(serialized.FindProperty("fastenerPresentationStageTravelScale").floatValue))
                {
                    throw new InvalidDataException("Changed reviewed engine presentation frame: " + binding.FastenerId);
                }

                MeshFilter[] filters = presentation.GetComponents<MeshFilter>();
                MeshRenderer[] renderers = presentation.GetComponents<MeshRenderer>();
                InteractionTargetHost host = target.GetComponent<InteractionTargetHost>();
                if (filters.Length != 1 || renderers.Length != 1 ||
                    renderers[0].sharedMaterials.Length != 1 || renderers[0].sharedMaterial != material ||
                    host == null || host.OutlineRenderers.Count != 1 || host.OutlineRenderers[0] != renderers[0] ||
                    !host.TryGetCapability<AssemblyFastenerInteractionTarget>(out AssemblyFastenerInteractionTarget capability) ||
                    capability != target)
                {
                    throw new InvalidDataException("Changed reviewed engine renderer/outline: " + binding.FastenerId);
                }

                Mesh expected = meshes[binding.MeshSourceGuid];
                if (filters[0].sharedMesh != meshes[Nut] && filters[0].sharedMesh != expected)
                {
                    throw new InvalidDataException("Unexpected engine fastener mesh: " + binding.FastenerId);
                }
                if (filters[0].sharedMesh != expected)
                {
                    changes.Add((filters[0], expected));
                }
            }

            // Fail-before-write for the complete reviewed set. Existing poses,
            // travel, colliders, definitions, counts and IDs stay untouched.
            foreach ((MeshFilter filter, Mesh mesh) in changes)
            {
                filter.sharedMesh = mesh;
                EditorUtility.SetDirty(filter);
            }
            return changes.Count;
        }

        internal static void ValidateDonorBinding(DonorUnitySceneModel scene, Binding binding)
        {
            IReadOnlyList<DonorStaticRendererRecord> renderers = scene.GetStaticRenderersBelowIncludingInactive(binding.MarkerTransformId);
            if (renderers.Count != 1)
            {
                throw new InvalidDataException("Expected one reviewed donor engine renderer: " + binding.FastenerId);
            }
            DonorStaticRendererRecord source = renderers[0];
            DonorTransformRecord child = scene.GetTransform(scene.GetTransformIdForGameObject(source.GameObjectId));
            DonorTransformRecord marker = scene.GetTransform(binding.MarkerTransformId);
            if (source.MeshGuid != binding.MeshSourceGuid || source.MaterialGuids.Count != 1 ||
                source.MaterialGuids[0] != MaterialGuid || child.FatherTransformId != binding.MarkerTransformId ||
                Vector3.Distance(child.LocalPosition, binding.DonorChildPosition) > 0.00001f ||
                Quaternion.Angle(child.LocalRotation, Quaternion.identity) > 0.001f ||
                Vector3.Distance(child.LocalScale, binding.DonorChildScale) > 0.00001f ||
                Vector3.Distance(marker.LocalScale, binding.PresentationScale) > 0.00001f)
            {
                throw new InvalidDataException("Reviewed donor engine mesh/material/frame drifted: " + binding.FastenerId);
            }
        }

        internal static IReadOnlyList<Binding> ResolveActiveBindings(GameObject root)
        {
            VehicleAssemblyController controller = root.GetComponent<VehicleAssemblyController>();
            if (controller == null) throw new InvalidDataException("Missing canonical assembly.");
            var retired = new HashSet<string>(StringComparer.Ordinal);
            foreach (string mountId in new[] { SatsumaRockerCoverFastenerMigration.MountId,
                         SatsumaCarburetorFastenerMigration.MountId })
            {
                MountPointAuthoring[] matches = controller.MountPoints.Where(value => value != null && value.MountId == mountId).ToArray();
                if (matches.Length != 1 || matches[0].Definition == null)
                    throw new InvalidDataException("Missing reviewed engine mount.");
                string[] definitions = matches[0].Definition.Fasteners.Select(value => value?.DefinitionId).ToArray();
                string[] targets = root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Where(value => value.MountId == mountId).Select(value => value.FastenerDefinitionId).ToArray();
                bool cover = mountId == SatsumaRockerCoverFastenerMigration.MountId;
                bool legacy = cover ? SatsumaRockerCoverFastenerMigration.IsLegacyShape(definitions)
                    : SatsumaCarburetorFastenerMigration.IsLegacyShape(definitions);
                bool canonical = cover ? SatsumaRockerCoverFastenerMigration.IsCanonicalShape(definitions)
                    : SatsumaCarburetorFastenerMigration.IsCanonicalShape(definitions);
                if ((!legacy && !canonical) || definitions.Length != targets.Length ||
                    !new HashSet<string>(definitions).SetEquals(targets))
                    throw new InvalidDataException("Unexpected engine fastener retirement shape: " + mountId);
                if (canonical)
                {
                    if (cover) retired.UnionWith(SatsumaRockerCoverFastenerMigration.RetiredIds);
                    else retired.Add(SatsumaCarburetorFastenerMigration.RetiredId);
                }
            }
            return Bindings.Where(value => !retired.Contains(value.FastenerId)).ToArray();
        }

        private static IReadOnlyList<Binding> CreateBindings()
        {
            var result = new List<Binding>();
            Add(result, "camshaft", "camshaft-gear", 10, Short, 37245);
            Add(result, "carburetor", "airfilter", 6, Short, 42310, 66624);
            Add(result, "crankshaft", "crankshaft-pulley", 11, Long, 50893);
            Add(result, "crankshaft", "flywheel", 7, Short, 37879, 42749, 44627, 57156, 60650, 64986);
            Add(result, "cylinder-head", "carburetor", 8, Nut, 48741, 50419, 52523, 60922, 64079);
            Replace(result, "cylinder-head", "carburetor", 3, 6, Slotted, 0.65f);
            Add(result, "cylinder-head", "headers", 8, Nut, 36705, 51075, 55793, 61870, 69218);
            // Both alternatives currently have independent IDs. Do not silently
            // discard saved stage state while correcting their mesh identity.
            Add(result, "cylinder-head", "rocker-cover", 7, Short,
                39308, 41931, 43047, 43107, 51693, 53156, 63635, 65810, 67202, 70160, 70985, 71935);
            foreach ((int index, long marker) in new[] { (2, 40906L), (5, 44469L), (6, 45373L), (8, 56020L), (9, 56364L) })
            {
                result.Add(new Binding("cylinder-head", "rocker-shaft", index, marker, 8, Long, Vector3.one * 0.8f, Vector3.zero));
            }
            Add(result, "engine-block", "alternator", 7, Short, 36539, 62684, 65277);
            Replace(result, "engine-block", "alternator", 2, 10, Nut, 1f);
            Replace(result, "engine-block", "alternator", 3, 6, Slotted, 0.65f);
            Add(result, "engine-block", "camshaft", 5, Short, 56110, 63989);
            Add(result, "engine-block", "cylinder-head", 7, Long,
                46010, 48643, 53741, 57645, 58467, 64525, 64613, 64941, 65656, 69091);
            Add(result, "engine-block", "distributor", 6, Slotted, 45067);
            Replace(result, "engine-block", "distributor", 1, 6, Slotted, 0.65f);
            Add(result, "engine-block", "fuel-pump", 7, Nut, 36969, 65212);
            Add(result, "engine-block", "main-bearing1", 9, Long, 37572, 52423);
            Add(result, "engine-block", "main-bearing2", 9, Long, 42728, 55653);
            Add(result, "engine-block", "main-bearing3", 9, Long, 48781, 50879);
            AddPiston(result, "piston1", 53477, 62925);
            AddPiston(result, "piston2", 42160, 50548);
            AddPiston(result, "piston3", 50929, 64131);
            AddPiston(result, "piston4", 54612, 64515);
            // The hose screw is the one reviewed non-unit donor renderer child.
            // Mesh-only repair preserves the existing project scale; do not
            // confuse that with a claim of fully corrected pose/size/travel.
            result.Add(new Binding("engine-block", "radiator-hose2", 1, 55496, 6, Slotted,
                Vector3.one * 0.65f, Vector3.zero, Vector3.one * 0.8f));
            Add(result, "engine-block", "timing-cover", 6, Long, 42707, 53009, 59658, 60350, 60458, 65489);
            foreach (int index in new[] { 1, 3, 4, 6 })
            {
                Replace(result, "engine-block", "timing-cover", index, 6, Long, 0.6f, -0.02f);
            }
            Replace(result, "engine-block", "timing-cover", 2, 6, Short, 0.6f);
            Replace(result, "engine-block", "timing-cover", 5, 6, Short, 0.6f);
            Add(result, "engine-plate", "starter", 7, Short, 57844, 64601);
            Replace(result, "engine-plate", "starter", 1, 7, Short, 0.7f, -0.02f);
            Replace(result, "engine-plate", "starter", 2, 7, Short, 0.7f, -0.02f);
            long[] clutchMarkers = { 38244, 44385, 62237, 66988, 68060, 71498 };
            for (int index = 0; index < clutchMarkers.Length; index++)
            {
                result.Add(new Binding("flywheel", "clutch-cover-plate", index + 1,
                    clutchMarkers[index], 6, Short, new Vector3(0.6f, 0.6f, 0.5f), Vector3.zero));
            }
            Add(result, "gearbox", "drive-gear", 6, Short, 44873, 46343, 49676, 52342, 55118, 63334, 64000);
            Replace(result, "gearbox", "drive-gear", 2, 6, Nut, 0.6f);
            Add(result, "timing-cover", "water-pump", 7, Nut, 46855, 48447, 57782, 65052, 70154);
            Replace(result, "timing-cover", "water-pump", 5, 7, Long, 0.7f);
            Add(result, "water-pump", "water-pump-pulley", 7, Short, 41383, 42659, 59744, 70151);
            if (result.Count != 100 || result.Select(value => value.FastenerId).Distinct().Count() != 100 ||
                result.Select(value => value.MarkerTransformId).Distinct().Count() != 100)
            {
                throw new InvalidDataException("Additional engine fastener mesh table is incomplete or ambiguous.");
            }
            return result.AsReadOnly();
        }

        private static void Add(List<Binding> result, string owner, string part, int size, string mesh, params long[] markers)
        {
            for (int index = 0; index < markers.Length; index++)
            {
                result.Add(new Binding(owner, part, index + 1, markers[index], size, mesh, Vector3.one * (size / 10f), Vector3.zero));
            }
        }

        private static void AddPiston(List<Binding> result, string part, params long[] markers)
        {
            for (int index = 0; index < markers.Length; index++)
            {
                result.Add(new Binding("engine-block", part, index + 1, markers[index], 7, Nut,
                    new Vector3(0.7f, 0.7f, 0.6f), new Vector3(0f, 0f, -0.02f)));
            }
        }

        private static void Replace(List<Binding> result, string owner, string part, int index, int size, string mesh, float scale, float childZ = 0f)
        {
            string id = "fastener.satsuma." + owner + "-" + part + ".boltpm-" + index;
            int offset = result.FindIndex(value => value.FastenerId == id);
            Binding old = result[offset];
            result[offset] = new Binding(owner, part, index, old.MarkerTransformId, size, mesh,
                Vector3.one * scale, new Vector3(0f, 0f, childZ));
        }

        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        internal readonly struct Binding
        {
            public Binding(string owner, string part, int index, long marker, int size, string mesh, Vector3 scale, Vector3 childPosition,
                Vector3? donorChildScale = null)
            {
                MountId = "mount.satsuma." + owner + "." + part;
                FastenerId = "fastener.satsuma." + owner + "-" + part + ".boltpm-" + index;
                MarkerTransformId = marker;
                SizeMillimeters = size;
                MeshSourceGuid = mesh;
                PresentationScale = scale;
                DonorChildPosition = childPosition;
                DonorChildScale = donorChildScale ?? Vector3.one;
            }

            public string MountId { get; }
            public string FastenerId { get; }
            public long MarkerTransformId { get; }
            public int SizeMillimeters { get; }
            public string MeshSourceGuid { get; }
            public Vector3 PresentationScale { get; }
            public Vector3 DonorChildPosition { get; }
            public Vector3 DonorChildScale { get; }
        }
    }
}
