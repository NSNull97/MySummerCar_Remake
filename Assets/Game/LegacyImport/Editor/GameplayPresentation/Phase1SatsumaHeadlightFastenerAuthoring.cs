using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Four stock headlamp mounting bolts, independent of purchased bulb sockets and Stock21.</summary>
    public static class Phase1SatsumaHeadlightFastenerAuthoring
    {
        public const string ShortBoltMeshGuid = "aec6c756751308a4d830708366ad5cdb";
        public const string MaterialGuid = "98697bae08a8c114ba9774c487f2658d";

        // Frozen GAME SHA c3f2f337...0476c4; see the headlight audit dated 2026-09-06.
        // Both source Bolts parents are identity relative to the PART. Numbering
        // follows ascending marker ID (right source sibling order is reversed).
        public static Binding[] GetBindings() => new[]
        {
            new Binding("headlight-left", 1, 47993, 107507, 106921,
                new Vector3(-.09532297f, .018868791f, .0036582127f),
                new Quaternion(-.707106f, -1.9441359e-8f, 0, .70710766f),
                new Vector3(.7f, .6999998f, .69999987f)),
            new Binding("headlight-left", 2, 64196, 112081, 106921,
                new Vector3(.09062467f, .018868607f, .0036573187f),
                new Quaternion(-.70710605f, -1.9441355e-8f, -2.9802315e-8f, .70710754f),
                new Vector3(.6999999f, .6999998f, .69999975f)),
            new Binding("headlight-right", 1, 39354, 105056, 111828,
                new Vector3(-.095091924f, .018869085f, .0036579706f),
                new Quaternion(-.70710605f, 1.8894204e-7f, -1.490116e-7f, .70710754f),
                new Vector3(.7f, .6999999f, .69999987f)),
            new Binding("headlight-right", 2, 58481, 110431, 111828,
                new Vector3(.09085509f, .018869027f, .0036576726f),
                new Quaternion(-.70710605f, 1.8894204e-7f, -1.490116e-7f, .70710754f),
                new Vector3(.7f, .6999999f, .69999987f))
        };

        public static FastenerDefinition CreateDefinition(Binding binding)
        {
            var definition = ScriptableObject.CreateInstance<FastenerDefinition>();
            definition.name = binding.FastenerId;
            definition.Configure(binding.FastenerId, "Болт №" + binding.Number, FastenerSize.Millimeter7,
                8, FastenerDirection.ClockwiseToTighten, true, true,
                ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter7));
            return definition;
        }

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Headlight Mounting Bolts Only")]
        public static void RefreshHeadlightFastenersBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before headlight bolt authoring.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            if (!File.Exists(path)) throw new InvalidDataException("Existing canonical Satsuma prefab is required.");
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                VehicleAssemblyController assembly = contents.GetComponent<VehicleAssemblyController>();
                Phase1SatsumaStartableCarNightBatch.ValidateTopology(assembly, false);
                string[] existingIds = assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Select(target => target.FastenerDefinitionId).ToArray();
                if (!Phase1SatsumaStockMountFastenerAuthoring.GetBindings().All(binding => existingIds.Contains(binding.FastenerId)))
                    throw new InvalidDataException("Apply the reviewed Stock21 packet before the isolated Headlight4 extension.");
                int changed = ApplyToInstance(assembly);
                if (ApplyToInstance(assembly) != 0) throw new InvalidDataException("Headlight bolt repeat must change zero bindings.");
                if (changed > 0 && PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                    throw new IOException("Could not save headlight bolt bindings.");
                Debug.Log("SATSUMA_HEADLIGHT_FASTENERS_OK changed=" + changed + " repeat=0 bolts=4 fullRebuild=false nativeSaveWrites=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly, string generatedRoot = null)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Headlight authoring cannot run in Play.");
            string root = generatedRoot ?? Phase1SatsumaCamshaftTimingAuthoring.GeneratedRoot;
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(root + "/Meshes/" + ShortBoltMeshGuid + ".asset");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/" + MaterialGuid + ".mat");
            int layer = LayerMask.NameToLayer(FastenerToolRaycastLayer.Name);
            var definitions = new Dictionary<string, FastenerDefinition>(StringComparer.Ordinal);
            var transient = new List<FastenerDefinition>();
            try
            {
                foreach (Binding binding in GetBindings())
                {
                    string path = root + "/FastenerDefinitions/" + binding.FastenerId + ".asset";
                    FastenerDefinition definition = AssetDatabase.LoadAssetAtPath<FastenerDefinition>(path);
                    if (definition == null)
                    {
                        if (File.Exists(path)) throw new InvalidDataException("Unexpected headlight fastener asset: " + path);
                        definition = CreateDefinition(binding);
                        transient.Add(definition);
                    }
                    definitions.Add(binding.FastenerId, definition);
                }
                // Shared dry-run rejects every source/definition/target discrepancy
                // before the first asset write. Persistent mounts must not serialize
                // transient definitions (Unity otherwise reloads fileID:0).
                PrepareWork(assembly, mesh, material, layer, definitions);
                if (!AssetDatabase.IsValidFolder(root + "/FastenerDefinitions"))
                    throw new InvalidDataException("Existing generated FastenerDefinitions folder is required.");
                while (transient.Count > 0)
                {
                    FastenerDefinition definition = transient[0];
                    AssetDatabase.CreateAsset(definition, root + "/FastenerDefinitions/" + definition.DefinitionId + ".asset");
                    transient.RemoveAt(0);
                }
                foreach (Binding binding in GetBindings())
                    definitions[binding.FastenerId] = AssetDatabase.LoadAssetAtPath<FastenerDefinition>(
                        root + "/FastenerDefinitions/" + binding.FastenerId + ".asset");
                int changed = Configure(assembly, mesh, material, layer, definitions);
                foreach (FastenerDefinition definition in definitions.Values) AssetDatabase.SaveAssetIfDirty(definition);
                foreach (string id in GetBindings().Select(binding => binding.MountId).Distinct())
                    AssetDatabase.SaveAssetIfDirty(assembly.MountPoints.Single(mount => mount.MountId == id).Definition);
                return changed;
            }
            finally { foreach (FastenerDefinition definition in transient) UnityEngine.Object.DestroyImmediate(definition); }
        }

        /// <summary>Typed fixture entry. Only the whole old 0+0 or exact authored 2+2 cohort is accepted.</summary>
        public static int Configure(VehicleAssemblyController assembly, Mesh mesh, Material material,
            int layer, IReadOnlyDictionary<string, FastenerDefinition> definitions)
        {
            List<MountWork> work = PrepareWork(assembly, mesh, material, layer, definitions);
            foreach (MountWork row in work)
                if (EditorUtility.IsPersistent(row.Mount.Definition) && row.Bolts.Any(b => !EditorUtility.IsPersistent(b.Definition)))
                    throw new InvalidDataException("Persist headlight fasteners before binding asset mount: " + row.Mount.MountId);
            if (!work[0].Legacy) return 0;
            foreach (MountWork row in work)
            {
                var data = new SerializedObject(row.Mount.Definition);
                SerializedProperty array = data.FindProperty("fasteners");
                array.arraySize = row.Bolts.Count;
                for (int index = 0; index < row.Bolts.Count; index++)
                    array.GetArrayElementAtIndex(index).objectReferenceValue = row.Bolts[index].Definition;
                data.ApplyModifiedPropertiesWithoutUndo();
                var group = new FastenerGroupDefinition();
                group.Configure(row.Bolts.Select(b => b.Binding.FastenerId).ToArray(), 16, 2, 0);
                row.Mount.Definition.ConfigureFastenerGroup(group);
                EditorUtility.SetDirty(row.Mount.Definition);
                foreach (BoltWork bolt in row.Bolts)
                {
                    bolt.Original.gameObject.SetActive(false);
                    CreateTarget(assembly, row.Mount, bolt.Binding, bolt.Tool, mesh, material, layer);
                }
            }
            return 6; // Two immutable groups and four explicitly owned targets.
        }

        private static List<MountWork> PrepareWork(VehicleAssemblyController assembly, Mesh mesh, Material material,
            int layer, IReadOnlyDictionary<string, FastenerDefinition> definitions)
        {
            if (assembly == null || mesh == null || material == null || definitions == null || layer < 0 || layer > 31)
                throw new InvalidDataException("Missing reviewed headlight authoring input.");
            var targets = assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            var work = new List<MountWork>();
            foreach (var cohort in GetBindings().GroupBy(binding => binding.MountId))
            {
                Binding[] bindings = cohort.ToArray();
                MountPointAuthoring[] mounts = assembly.MountPoints.Where(m => m != null && m.MountId == cohort.Key).ToArray();
                if (mounts.Length != 1 || mounts[0].Definition == null || mounts[0].Pose == null)
                    throw new InvalidDataException("Missing/duplicate headlight mount: " + cohort.Key);
                MountPointAuthoring mount = mounts[0];
                MountPointDefinition definition = mount.Definition;
                if (definition.DefinitionId != cohort.Key ||
                    !definition.AcceptedPartDefinitionIds.SequenceEqual(new[] { bindings[0].PartDefinitionId }))
                    throw new InvalidDataException("Unexpected headlight part identity: " + cohort.Key);
                PartInstance[] parts = assembly.Parts.Where(p => p != null && p.Definition != null &&
                    p.Definition.DefinitionId == bindings[0].PartDefinitionId).ToArray();
                if (parts.Length != 1) throw new InvalidDataException("Missing/duplicate headlight part: " + cohort.Key);
                bool legacy = definition.Fasteners.Length == 0;
                string[] ids = bindings.Select(b => b.FastenerId).ToArray();
                if (!legacy && !definition.Fasteners.Select(f => f != null ? f.DefinitionId : "").SequenceEqual(ids))
                    throw new InvalidDataException("Partial/unknown headlight fastener shape: " + cohort.Key);
                FastenerGroupDefinition group = definition.FastenerGroup;
                bool oldGroup = group != null && group.FastenerDefinitionIds.Length == 0 &&
                    group.AggregateMaximumTightness == 0 && group.BoltedOffThreshold == 0 &&
                    (group.BoltedOnThreshold == 0 || group.BoltedOnThreshold == 1);
                bool newGroup = group != null && group.FastenerDefinitionIds.SequenceEqual(ids) &&
                    group.AggregateMaximumTightness == 16 && group.BoltedOnThreshold == 2 && group.BoltedOffThreshold == 0;
                if ((legacy ? !oldGroup : !newGroup) || group.SpeedRetentionPolicy != FastenerSpeedRetentionPolicy.None ||
                    group.BreakAction != FastenerBreakAction.None)
                    throw new InvalidDataException("Unexpected headlight latch/retention: " + cohort.Key);
                var ownedTargets = targets.Where(t => t.MountId == cohort.Key).ToArray();
                if (ownedTargets.Length != (legacy ? 0 : 2) || (work.Count > 0 && work[0].Legacy != legacy))
                    throw new InvalidDataException("Headlight mounting bolts require the whole old 0+0 or new 2+2 cohort: " + cohort.Key);
                var row = new MountWork(mount, legacy);
                foreach (Binding binding in bindings)
                {
                    if (!definitions.TryGetValue(binding.FastenerId, out FastenerDefinition f) || !IsReviewedDefinition(f, binding))
                        throw new InvalidDataException("Unexpected headlight fastener definition: " + binding.FastenerId);
                    ToolDefinition[] tools = assembly.Tools.Where(t => t != null && t.ToolType == "Wrench" && (int)t.Size == 7).ToArray();
                    if (tools.Length != 1) throw new InvalidDataException("Exactly one 7mm wrench is required.");
                    MeshFilter[] originals = parts[0].GetComponentsInChildren<MeshFilter>(true).Where(filter =>
                        filter.sharedMesh == mesh && filter.GetComponentInParent<PartInstance>(true) == parts[0] &&
                        filter.GetComponentInParent<AssemblyFastenerInteractionTarget>(true) == null &&
                        MatrixMatches(RelativeMatrix(filter.transform, parts[0].transform), binding.Pose, binding.Scale)).ToArray();
                    if (originals.Length != 1 || originals[0].GetComponent<MeshRenderer>() == null ||
                        originals[0].GetComponentsInChildren<Collider>(true).Length != 0)
                        throw new InvalidDataException("Missing/ambiguous headlight source short bolt pose: " + binding.FastenerId);
                    if (targets.Count(t => t.FastenerDefinitionId == binding.FastenerId) != (legacy ? 0 : 1))
                        throw new InvalidDataException("Foreign/duplicate headlight fastener identity: " + binding.FastenerId);
                    if (!legacy)
                    {
                        var target = ownedTargets.SingleOrDefault(t => t.FastenerDefinitionId == binding.FastenerId);
                        if (target == null || !IsReviewedTarget(target, assembly, mount, binding, mesh, material, tools[0], layer) ||
                            originals[0].gameObject.activeSelf || f != definition.Fasteners[Array.IndexOf(bindings, binding)])
                            throw new InvalidDataException("Changed authored headlight bolt binding: " + binding.FastenerId);
                    }
                    row.Bolts.Add(new BoltWork(binding, f, tools[0], originals[0]));
                }
                work.Add(row);
            }
            return work;
        }

        private static void CreateTarget(VehicleAssemblyController assembly, MountPointAuthoring mount,
            Binding binding, ToolDefinition tool, Mesh mesh, Material material, int layer)
        {
            var marker = new GameObject(binding.FastenerId) { layer = layer };
            marker.transform.SetParent(mount.Pose, false);
            marker.transform.SetLocalPositionAndRotation(binding.Pose.position, binding.Pose.rotation);
            var visible = new GameObject("Visible inserted headlight bolt") { layer = layer };
            visible.transform.SetParent(marker.transform, false);
            visible.transform.localScale = binding.Scale;
            visible.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visible.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            var target = marker.AddComponent<AssemblyFastenerInteractionTarget>();
            // Configure before adding the collider: no live graph is initialized
            // while the immutable two-group packet is extended.
            target.Configure(assembly, binding.MountId, binding.FastenerId, tool, true, visible.transform, binding.Scale.z);
            SphereCollider collider = marker.AddComponent<SphereCollider>();
            collider.isTrigger = true; collider.radius = .028f; collider.enabled = false;
            InteractionTargetHost host = marker.AddComponent<InteractionTargetHost>();
            host.Configure(target); host.ConfigureOutlineRenderers(renderer); host.ConfigureSelectionPriority(40);
        }

        private static bool IsReviewedDefinition(FastenerDefinition f, Binding b) =>
            f != null && f.DefinitionId == b.FastenerId && (int)f.Size == 7 && f.MaximumStage == 8 &&
            f.InsertedOnInstall && f.RequiredForRemoval && f.TighteningDirection == FastenerDirection.ClockwiseToTighten &&
            f.ToolRule.ToolType == "Wrench" && (int)f.ToolRule.FastenerSize == 7;

        private static bool IsReviewedTarget(AssemblyFastenerInteractionTarget target, VehicleAssemblyController assembly,
            MountPointAuthoring mount, Binding binding, Mesh mesh, Material material, ToolDefinition tool, int layer)
        {
            if (target.Controller != assembly || target.transform.parent != mount.Pose || target.gameObject.layer != layer ||
                !MatrixMatches(Matrix4x4.TRS(target.transform.localPosition, target.transform.localRotation, target.transform.localScale),
                    binding.Pose, Vector3.one) || Mathf.Abs(target.FastenerPresentationStageTravelScale - binding.Scale.z) > .00001f)
                return false;
            var data = new SerializedObject(target);
            Transform visible = data.FindProperty("fastenerPresentation").objectReferenceValue as Transform;
            if (visible == null || visible.parent != target.transform || visible.gameObject.layer != layer ||
                !MatrixMatches(Matrix4x4.TRS(visible.localPosition, visible.localRotation, visible.localScale),
                    new Pose(Vector3.zero, Quaternion.identity), binding.Scale)) return false;
            var filter = visible.GetComponent<MeshFilter>();
            var renderer = visible.GetComponent<MeshRenderer>();
            var collider = target.GetComponent<SphereCollider>();
            var host = target.GetComponent<InteractionTargetHost>();
            return filter != null && filter.sharedMesh == mesh && renderer != null && renderer.sharedMaterial == material &&
                collider != null && collider.isTrigger && Mathf.Abs(collider.radius - .028f) < .00001f &&
                host != null && host.OutlineRenderers.Count == 1 && host.OutlineRenderers[0] == renderer &&
                data.FindProperty("tool").objectReferenceValue == tool;
        }

        private static Matrix4x4 RelativeMatrix(Transform child, Transform root)
        {
            Matrix4x4 result = Matrix4x4.identity;
            while (child != root)
            {
                if (child == null) throw new InvalidDataException("Headlight source is outside its explicit part.");
                result = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale) * result;
                child = child.parent;
            }
            return result;
        }

        private static bool MatrixMatches(Matrix4x4 actual, Pose pose, Vector3 scale)
        {
            Matrix4x4 expected = Matrix4x4.TRS(pose.position, pose.rotation, scale);
            for (int index = 0; index < 16; index++) if (Mathf.Abs(actual[index] - expected[index]) > .0001f) return false;
            return true;
        }

        public readonly struct Binding
        {
            public Binding(string slug, int number, long markerId, long screwFsmId, long boltCheckId,
                Vector3 position, Quaternion rotation, Vector3 scale)
            { Slug = slug; Number = number; MarkerId = markerId; ScrewFsmId = screwFsmId; BoltCheckId = boltCheckId;
                Pose = new Pose(position, rotation); Scale = scale; }
            public string Slug { get; }
            public int Number { get; }
            public long MarkerId { get; }
            public long ScrewFsmId { get; }
            public long BoltCheckId { get; }
            public int Size => 7;
            public int OnThreshold => 2;
            public Pose Pose { get; }
            public Vector3 Scale { get; }
            public string MountId => "mount.satsuma." + Slug;
            public string PartDefinitionId => "vehicle.satsuma.part." + Slug;
            public string FastenerId => "fastener.satsuma." + Slug + ".boltpm-" + Number;
        }

        private sealed class MountWork
        {
            public MountWork(MountPointAuthoring mount, bool legacy) { Mount = mount; Legacy = legacy; }
            public MountPointAuthoring Mount { get; }
            public bool Legacy { get; }
            public List<BoltWork> Bolts { get; } = new List<BoltWork>();
        }

        private readonly struct BoltWork
        {
            public BoltWork(Binding binding, FastenerDefinition definition, ToolDefinition tool, MeshFilter original)
            { Binding = binding; Definition = definition; Tool = tool; Original = original; }
            public Binding Binding { get; }
            public FastenerDefinition Definition { get; }
            public ToolDefinition Tool { get; }
            public MeshFilter Original { get; }
        }
    }
}
