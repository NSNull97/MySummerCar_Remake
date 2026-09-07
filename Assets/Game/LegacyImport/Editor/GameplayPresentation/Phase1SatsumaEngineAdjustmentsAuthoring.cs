using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Interaction.Query;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Scoped frozen-GAME bindings; no original FSM runtime is imported.</summary>
    public static class Phase1SatsumaEngineAdjustmentsAuthoring
    {
        public const string ObsoleteMixtureFastenerId = "fastener.satsuma.cylinder-head-carburetor.boltpm-3";
        public const string GeneratedRoot = Phase1SatsumaCamshaftTimingAuthoring.GeneratedRoot;
        public const string AlternatorMesh = "5b55f637bcd4e5d4db3d8326a5c733e4";
        public const string AlternatorPulleyMesh = "f8771cd5897dcc94abbc279a97615b05";
        public const string DistributorMesh = "ed8c93264302de144b73f50955ac0872";
        public const string FilterMesh = "5833635d309ec3248b5c7ba65bddad85";
        public const string ThrottleLinkageMesh = "47923c19a9934cf4096251afb7c03d86";
        public const string ClosedButterflyMesh = "75b83bb00e0a6664b9d079523a9a5392";
        public const string OpenButterflyMesh = "2de4325ee6ab3ef429a10e8554147537";

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Engine Adjustments Only")]
        public static void RefreshEngineAdjustmentsBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before authoring engine adjustments.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var assembly = contents.GetComponent<VehicleAssemblyController>();
                MountPointDefinition carbMount = Mount(assembly, SatsumaEngineAdjustmentKind.CarburetorMixture).Definition;
                int changed = ApplyToInstance(assembly);
                if (changed > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/codex-engine-adjustments-before-" +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                        throw new InvalidDataException("Could not save scoped engine adjustment bindings.");
                    AssetDatabase.SaveAssetIfDirty(carbMount);
                }
                Debug.Log("SATSUMA_ENGINE_ADJUSTMENTS_REFRESH_OK changed=" + changed + " fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        // Run after normal fastener and ignition authoring. Never initialize the
        // assembly graph in an Editor authoring pass.
        public static int ApplyToInstance(VehicleAssemblyController assembly, string generatedRoot = GeneratedRoot)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            int changed = 0;
            changed += ConfigureHandPart(assembly, SatsumaEngineAdjustmentKind.Alternator,
                new Vector3(-0.0030999184f, 0.029600155f, -0.062200014f), Vector3.right, 7f,
                new Vector3(0.05f, -0.019f, 0.063f), 0.055f,
                new[] { AlternatorMesh, AlternatorPulleyMesh }, generatedRoot);
            changed += ConfigureHandPart(assembly, SatsumaEngineAdjustmentKind.Distributor,
                Vector3.zero, Vector3.forward, 0f, new Vector3(0f, 0f, 0.05f), 0.04f,
                new[] { DistributorMesh }, generatedRoot);
            changed += ConfigureHandPart(assembly, SatsumaEngineAdjustmentKind.OilFilter,
                Vector3.zero, Vector3.forward, 0f, Vector3.zero, 0.06f,
                new[] { FilterMesh }, generatedRoot);
            changed += ConfigureMixture(assembly);
            changed += ConfigureThrottle(assembly, generatedRoot);
            changed += RetireMixtureFromMount(Mount(assembly, SatsumaEngineAdjustmentKind.CarburetorMixture).Definition);
            return changed;
        }

        private static int ConfigureHandPart(VehicleAssemblyController assembly, SatsumaEngineAdjustmentKind kind,
            Vector3 pivot, Vector3 axis, float referenceAngle, Vector3 colliderCenter, float radius,
            string[] meshGuids, string generatedRoot)
        {
            PartInstance part = Part(assembly, kind);
            var existing = part.GetComponent<AssemblyEngineAdjustmentState>();
            if (existing != null)
            {
                ValidateState(existing, assembly, kind);
                if (part.GetComponentsInChildren<AssemblyEngineAdjustmentTarget>(true).Count(value => value.State == existing) != 1)
                    throw new InvalidDataException("Engine hand-adjustment target binding drifted: " + kind);
                return 0;
            }
            MeshFilter[] meshes = meshGuids.Select(guid => Mesh(part, guid, generatedRoot)).ToArray();
            var control = new GameObject("Engine adjustment " + kind);
            control.transform.SetParent(part.transform, false);
            if (kind == SatsumaEngineAdjustmentKind.Alternator)
            {
                // The donor input sphere belongs to the same local-X pivot.
                control.transform.localPosition = pivot;
                control.transform.localRotation = Quaternion.Euler(referenceAngle, 0f, 0f);
            }
            if (kind == SatsumaEngineAdjustmentKind.OilFilter)
            {
                var box = control.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(0.06f, 0.06f, 0.122568026f);
            }
            else
            {
                var sphere = control.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.center = colliderCenter;
                sphere.radius = radius;
            }
            var bindings = new List<AssemblyEngineAdjustmentPresentation>();
            foreach (MeshFilter mesh in meshes) bindings.Add(Capture(part.transform, mesh.transform));
            bindings.Add(Capture(part.transform, control.transform));
            if (kind == SatsumaEngineAdjustmentKind.Alternator)
            {
                AssemblyFastenerInteractionTarget clamp = Fastener(assembly,
                    SatsumaEngineAdjustmentRules.ClampFastenerId(kind));
                bindings.Add(Capture(Mount(assembly, kind).Pose, clamp.transform, true));
            }
            var state = part.gameObject.AddComponent<AssemblyEngineAdjustmentState>();
            state.Configure(kind, part, assembly, pivot, axis, referenceAngle, bindings.ToArray());
            var target = control.AddComponent<AssemblyEngineAdjustmentTarget>();
            target.Configure(state, meshes[0].GetComponent<Renderer>());
            ConfigureHost(control, target, target.OutlineRenderer, 35);
            return 1;
        }

        private static int ConfigureMixture(VehicleAssemblyController assembly)
        {
            var kind = SatsumaEngineAdjustmentKind.CarburetorMixture;
            PartInstance part = Part(assembly, kind);
            var state = part.GetComponent<AssemblyEngineAdjustmentState>();
            AssemblyFastenerInteractionTarget obsolete = assembly
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .SingleOrDefault(value => value.FastenerDefinitionId == ObsoleteMixtureFastenerId);
            if (state != null)
            {
                ValidateState(state, assembly, kind);
                var existing = assembly.GetComponentsInChildren<AssemblyEngineAdjustmentTarget>(true)
                    .Single(value => value.State == state);
                if (obsolete != null && obsolete.gameObject != existing.gameObject)
                    throw new InvalidDataException("Duplicate obsolete carburetor mixture marker.");
                bool retired = obsolete != null;
                if (retired) RemoveGenericTarget(obsolete);
                return retired ? 1 : 0;
            }
            if (obsolete == null || obsolete.MountId != SatsumaEngineAdjustmentRules.MountId(kind))
                throw new InvalidDataException("The exact source52523 mixture marker is missing.");
            var serialized = new SerializedObject(obsolete);
            var presentation = serialized.FindProperty("fastenerPresentation").objectReferenceValue as Transform;
            Renderer renderer = presentation != null ? presentation.GetComponentInChildren<Renderer>(true) : null;
            if (renderer == null) throw new InvalidDataException("Mixture screw presentation is missing.");
            Transform frame = Mount(assembly, kind).Pose;
            Vector3 pivot = frame.InverseTransformPoint(obsolete.transform.position);
            Vector3 axis = frame.InverseTransformDirection(obsolete.transform.forward).normalized;
            state = part.gameObject.AddComponent<AssemblyEngineAdjustmentState>();
            state.Configure(kind, part, assembly, pivot, axis, 0f,
                new[] { Capture(frame, presentation) });
            GameObject marker = obsolete.gameObject;
            RemoveGenericTarget(obsolete);
            var target = marker.AddComponent<AssemblyEngineAdjustmentTarget>();
            target.Configure(state, renderer);
            // The marker lives on the mount owner. Its collider is install-gated,
            // but its explicitly bound screw follows even the loose carb, not
            // an empty cylinder-head mount in midair.
            ConfigureHost(marker, target, renderer, 40);
            renderer.gameObject.SetActive(true);
            renderer.enabled = true;
            return 1;
        }

        private static void RemoveGenericTarget(AssemblyFastenerInteractionTarget obsolete)
        {
            // Its OnDisable may query the graph. Clear only this retiring
            // component's controller before disposal to keep authoring read-only.
            var serialized = new SerializedObject(obsolete);
            serialized.FindProperty("controller").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(obsolete);
        }

        private static int ConfigureThrottle(VehicleAssemblyController assembly, string generatedRoot)
        {
            PartInstance part = Part(assembly, SatsumaEngineAdjustmentKind.CarburetorMixture);
            AssemblyCarburetorThrottleTarget target = part.GetComponentInChildren<AssemblyCarburetorThrottleTarget>(true);
            int changed = 0;
            if (target == null)
            {
                MeshFilter linkage = Mesh(part, ThrottleLinkageMesh, generatedRoot);
                Renderer closed = Mesh(part, ClosedButterflyMesh, generatedRoot).GetComponent<Renderer>();
                Renderer open = Mesh(part, OpenButterflyMesh, generatedRoot).GetComponent<Renderer>();
                var control = new GameObject("Carburetor throttle linkage interaction");
                control.transform.SetParent(part.transform, false);
                var sphere = control.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 0.03f;
                sphere.center = new Vector3(-0.06f, -0.02f, 0.012f);
                target = control.AddComponent<AssemblyCarburetorThrottleTarget>();
                target.Configure(part, linkage.transform, closed, open,
                    new Vector3(-0.05525875f, -0.021597866f, 0.012377917f),
                    part.transform.InverseTransformPoint(linkage.transform.position),
                    Quaternion.Inverse(part.transform.rotation) * linkage.transform.rotation);
                closed.gameObject.SetActive(true);
                open.gameObject.SetActive(true);
                closed.enabled = true;
                open.enabled = false;
                ConfigureHost(control, target, linkage.GetComponent<Renderer>(), 36);
                changed++;
            }
            if (target.Part != part || target.Linkage == null)
                throw new InvalidDataException("Stock carburetor throttle binding drifted.");
            var input = assembly.GetComponent<SatsumaIgnitionInputAdapter>();
            if (input == null) throw new InvalidDataException("Existing ignition input adapter is missing.");
            if (input.CarburetorThrottle != target)
            { input.ConfigureCarburetorThrottle(target); changed++; }
            return changed;
        }

        public static int RetireMixtureFromMount(MountPointDefinition definition)
        {
            string mountId = SatsumaEngineAdjustmentRules.MountId(SatsumaEngineAdjustmentKind.CarburetorMixture);
            if (definition == null || definition.DefinitionId != mountId ||
                definition.OwnerPartDefinitionId != "vehicle.satsuma.part.cylinder-head")
                throw new InvalidDataException("Carburetor mount identity drifted.");
            FastenerDefinition[] keep = definition.Fasteners.Where(value =>
                value != null && value.DefinitionId != ObsoleteMixtureFastenerId).ToArray();
            string[] ids = new[] { 1, 2, 4, 5 }.Select(index =>
                "fastener.satsuma.cylinder-head-carburetor.boltpm-" + index).ToArray();
            if (!keep.Select(value => value.DefinitionId).SequenceEqual(ids) ||
                keep.Any(value => value.MaximumStage != 8 || value.Size != FastenerSize.Millimeter8))
                throw new InvalidDataException("Expected four original 8 mm carburetor mounting fasteners.");
            FastenerGroupDefinition old = definition.FastenerGroup;
            if (old == null) throw new InvalidDataException("Carburetor fastener group is missing.");
            if (definition.Fasteners.Length == 4 && old.AggregateMaximumTightness == 32 &&
                old.BoltedOnThreshold == 8 && old.BoltedOffThreshold == 0 &&
                old.FastenerDefinitionIds.SequenceEqual(ids)) return 0;
            if (definition.Fasteners.Length != 4 && (definition.Fasteners.Length != 5 ||
                definition.Fasteners.Count(value => value.DefinitionId == ObsoleteMixtureFastenerId) != 1))
                throw new InvalidDataException("Unexpected carburetor fastener contract; refusing broad migration.");
            definition.Configure(definition.DefinitionId, definition.DisplayName, definition.SocketType,
                definition.OwnerPartDefinitionId, definition.AcceptedPartDefinitionIds, definition.Constraint,
                definition.ReferenceCandidateRadiusMeters, keep);
            var group = new FastenerGroupDefinition();
            group.Configure(ids, 32, 8, 0,
                old.SpeedRetentionPolicy, old.LooseBreakSpeedKph, old.PartialCheckSpeedKph,
                old.ChanceDivisor, old.BreakAction);
            definition.ConfigureFastenerGroup(group);
            EditorUtility.SetDirty(definition);
            return 1;
        }

        private static void ValidateState(AssemblyEngineAdjustmentState state, VehicleAssemblyController assembly,
            SatsumaEngineAdjustmentKind kind)
        {
            if (state.Kind != kind || state.Assembly != assembly || state.Part != Part(assembly, kind) ||
                state.Presentations.Length == 0 || state.Presentations.Any(value => value?.Target == null))
                throw new InvalidDataException("Engine adjustment authoring drifted: " + kind);
        }

        private static AssemblyEngineAdjustmentPresentation Capture(Transform frame, Transform target, bool installedOnly = false) =>
            new(target, frame.InverseTransformPoint(target.position), Quaternion.Inverse(frame.rotation) * target.rotation, installedOnly);

        private static void ConfigureHost(GameObject owner, MonoBehaviour target, Renderer renderer, int priority)
        {
            var host = owner.GetComponent<InteractionTargetHost>() ?? owner.AddComponent<InteractionTargetHost>();
            host.Configure(target);
            host.ConfigureOutlineRenderers(renderer);
            host.ConfigureSelectionPriority(priority);
        }

        private static PartInstance Part(VehicleAssemblyController assembly, SatsumaEngineAdjustmentKind kind) =>
            assembly.Parts.Single(value => value != null && value.Definition != null &&
                value.Definition.DefinitionId == SatsumaEngineAdjustmentRules.PartId(kind));
        private static MountPointAuthoring Mount(VehicleAssemblyController assembly, SatsumaEngineAdjustmentKind kind) =>
            assembly.MountPoints.Single(value => value != null && value.MountId == SatsumaEngineAdjustmentRules.MountId(kind));
        private static AssemblyFastenerInteractionTarget Fastener(VehicleAssemblyController assembly, string id) =>
            assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Single(value => value.Controller == assembly && value.FastenerDefinitionId == id);
        private static MeshFilter Mesh(PartInstance part, string sourceGuid, string generatedRoot)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(generatedRoot.TrimEnd('/', '\\') + "/Meshes/" + sourceGuid + ".asset");
            if (mesh == null) throw new InvalidDataException("Missing frozen mesh " + sourceGuid);
            MeshFilter[] matches = part.GetComponentsInChildren<MeshFilter>(true).Where(value =>
                value.sharedMesh == mesh && value.GetComponentInParent<PartInstance>() == part).ToArray();
            if (matches.Length != 1 || matches[0].transform == part.transform)
                throw new InvalidDataException("Expected one separate part-owned presentation for " + sourceGuid);
            return matches[0];
        }
    }
}
