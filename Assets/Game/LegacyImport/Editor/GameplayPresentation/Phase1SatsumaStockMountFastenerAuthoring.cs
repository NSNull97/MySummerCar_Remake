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
    /// <summary>Frozen stock-only exhaust, tank and seat bolts; no runtime donor lookup.</summary>
    public static class Phase1SatsumaStockMountFastenerAuthoring
    {
        public const string ShortBoltMeshGuid = "aec6c756751308a4d830708366ad5cdb";
        public const string FuelTankPipeMountId = "mount.satsuma.fuel-tank-pipe";
        private const float PoseTolerance = .0001f;

        // Pose is relative to the PART, not donor world or Bolts root. Passenger
        // Bolts Transform48237 has +.01m X relative to part53013, included below.
        public static Binding[] GetBindings() => new[]
        {
            new Binding("exhaust-pipe", 1, 39814, 7, 6, 112050,
                new Vector3(-0.07544849f, 1.1761678f, 0.2514213f),
                new Quaternion(0.9909611f, 0.0004932284f, -0.0033623422f, 0.13410689f),
                new Vector3(0.70000005f, 0.69999987f, 0.5899998f)),
            new Binding("exhaust-pipe", 2, 40656, 7, 6, 112050,
                new Vector3(-0.12525661f, 1.1761644f, 0.25176153f),
                new Quaternion(0.9909611f, 0.0004924833f, -0.003362372f, 0.13410716f),
                new Vector3(0.7000001f, 0.6999997f, 0.5899998f)),
            new Binding("exhaust-pipe", 3, 41558, 7, 6, 112050,
                new Vector3(0.16671117f, -0.82419634f, 0.049632326f),
                new Quaternion(0.85694826f, 0f, -8.940697e-8f, 0.51540256f),
                new Vector3(0.6999999f, 0.70000005f, 0.70000005f)),
            new Binding("exhaust-muffler", 1, 37241, 7, 2, 106964,
                new Vector3(0.04816f, -0.12776f, 0.029f),
                new Quaternion(1f, 4.734752e-9f, 1.0960481e-8f, 0.000035047527f),
                new Vector3(0.6999999f, 0.70000005f, 0.70000005f)),
            new Binding("fuel-tank", 1, 42281, 11, 12, 107694,
                new Vector3(0.10182512f, -0.32332683f, -0.07161225f),
                new Quaternion(-2.0861586e-7f, 1f, 1.7881433e-7f, 0.0000022649765f),
                new Vector3(1.1f, 1.0999999f, 1.0999999f)),
            new Binding("fuel-tank", 2, 42980, 11, 12, 107694,
                new Vector3(-0.082500815f, -0.32332706f, -0.071612366f),
                new Quaternion(-2.0861586e-7f, 1f, 1.7881433e-7f, 0.0000022649765f),
                new Vector3(1.1f, 1.0999999f, 1.0999999f)),
            new Binding("fuel-tank", 3, 50849, 11, 12, 107694,
                new Vector3(0.2821262f, -0.32332683f, -0.07161225f),
                new Quaternion(-2.0861586e-7f, 1f, 1.7881433e-7f, 0.0000022649765f),
                new Vector3(1.1f, 1.0999999f, 1.0999999f)),
            new Binding("fuel-tank", 4, 63263, 11, 12, 107694,
                new Vector3(-0.26146215f, -0.32332706f, -0.071612366f),
                new Quaternion(-2.0861586e-7f, 1f, 1.7881433e-7f, 0.0000022649765f),
                new Vector3(1.1f, 1.0999999f, 1.0999999f)),
            new Binding("fuel-tank", 5, 63543, 11, 12, 107694,
                new Vector3(-0.082500815f, 0.3077851f, -0.071612015f),
                new Quaternion(-2.0861586e-7f, 1f, 1.7881433e-7f, 0.0000022649765f),
                new Vector3(1.1f, 1.0999999f, 1.0999999f)),
            new Binding("fuel-tank", 6, 64798, 11, 12, 107694,
                new Vector3(0.10182524f, 0.30778533f, -0.071611896f),
                new Quaternion(-2.0861586e-7f, 1f, 1.7881433e-7f, 0.0000022649765f),
                new Vector3(1.1f, 1.0999999f, 1.0999999f)),
            new Binding("fuel-tank", 7, 66640, 11, 12, 107694,
                new Vector3(0.28212547f, 0.30778533f, -0.07161178f),
                new Quaternion(0f, 1f, 0f, 0.0000028173115f),
                new Vector3(1.1f, 1.1f, 1.1f)),
            new Binding("seat-driver", 1, 38061, 9, 7, 114222,
                new Vector3(-0.223f, -0.19f, -0.094f),
                new Quaternion(0f, 0f, 0f, 1f),
                new Vector3(0.9f, 0.90000045f, 0.90000045f)),
            new Binding("seat-driver", 2, 40558, 9, 7, 114222,
                new Vector3(0.2347f, 0.1465f, -0.0792f),
                new Quaternion(0f, 0f, 0f, 1f),
                new Vector3(0.9f, 0.9f, 0.9f)),
            new Binding("seat-driver", 3, 41290, 9, 7, 114222,
                new Vector3(0.235f, -0.1899f, -0.0936f),
                new Quaternion(0f, 0f, 0f, 1f),
                new Vector3(0.9f, 0.9000002f, 0.9000002f)),
            new Binding("seat-driver", 4, 70665, 9, 7, 114222,
                new Vector3(-0.223f, 0.147f, -0.079f),
                new Quaternion(0f, 0f, 0f, 1f),
                new Vector3(0.9f, 0.9000002f, 0.9000002f)),
            new Binding("seat-passenger", 1, 37166, 9, 7, 108905,
                new Vector3(0.2338f, 0.201f, -0.0782f),
                new Quaternion(0f, 0f, 0f, 1f),
                new Vector3(0.9f, 0.9f, 0.9f)),
            new Binding("seat-passenger", 2, 52283, 9, 7, 108905,
                new Vector3(0.2341f, -0.1354f, -0.0926f),
                new Quaternion(0f, 0f, 0f, 1f),
                new Vector3(0.9f, 0.9000002f, 0.9000002f)),
            new Binding("seat-passenger", 3, 56578, 9, 7, 108905,
                new Vector3(-0.2239f, 0.2015f, -0.078f),
                new Quaternion(0f, 0f, 0f, 1f),
                new Vector3(0.9f, 0.9000002f, 0.9000002f)),
            new Binding("seat-passenger", 4, 56767, 9, 7, 108905,
                new Vector3(-0.2239f, -0.1355f, -0.093f),
                new Quaternion(0f, 0f, 0f, 1f),
                new Vector3(0.9f, 0.90000045f, 0.90000045f)),
            new Binding("seat-rear", 1, 42421, 9, 6, 114113,
                new Vector3(-0.576f, 0.1022f, -0.0653f),
                new Quaternion(-0.0000018030405f, -3.258408e-7f, -3.2584137e-7f, 1f),
                new Vector3(0.9f, 0.9f, 0.9f)),
            new Binding("seat-rear", 2, 46247, 9, 6, 114113,
                new Vector3(0.576f, 0.102f, -0.065f),
                new Quaternion(-0.000002324581f, -2.8041964e-7f, -4.7265697e-8f, 1f),
                new Vector3(0.9f, 0.9f, 0.9f)),
        };

        public static FastenerDefinition CreateDefinition(Binding binding)
        {
            var value = ScriptableObject.CreateInstance<FastenerDefinition>();
            value.name = binding.FastenerId;
            value.Configure(binding.FastenerId, "Болт №" + binding.Number,
                (FastenerSize)binding.Size, 8, FastenerDirection.ClockwiseToTighten,
                true, true, ToolCompatibilityRule.Create("Wrench", (FastenerSize)binding.Size));
            return value;
        }

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Stock Mount Fasteners Only")]
        public static void RefreshStockMountFastenersBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before authoring stock mounting bolts.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(root.GetComponent<VehicleAssemblyController>());
                if (changed > 0 && PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidDataException("Could not save stock mounting bolt bindings.");
                Debug.Log("SATSUMA_STOCK_MOUNT_FASTENERS_REFRESH_OK changed=" + changed +
                    " reviewed=21 mounts=6 fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly, string generatedRoot = null)
        {
            string root = generatedRoot ?? Phase1SatsumaCamshaftTimingAuthoring.GeneratedRoot;
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(root + "/Meshes/" + ShortBoltMeshGuid + ".asset");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/" +
                Phase1SatsumaEngineFastenerPresentation.FastenerMaterialSourceGuid + ".mat");
            int layer = LayerMask.NameToLayer(FastenerToolRaycastLayer.Name);
            var definitions = new Dictionary<string, FastenerDefinition>(StringComparer.Ordinal);
            var transient = new List<FastenerDefinition>();
            try
            {
                foreach (Binding b in GetBindings())
                {
                    string path = root + "/FastenerDefinitions/" + b.FastenerId + ".asset";
                    FastenerDefinition definition = AssetDatabase.LoadAssetAtPath<FastenerDefinition>(path);
                    if (definition == null)
                    {
                        if (File.Exists(path)) throw new InvalidDataException("Unexpected stock bolt asset: " + path);
                        definition = CreateDefinition(b);
                        transient.Add(definition);
                    }
                    definitions.Add(b.FastenerId, definition);
                }
                // Validate the complete packet before creating any asset. A
                // persistent mount must never serialize a transient definition:
                // Unity stores that reference as fileID:0 on reimport.
                PrepareWork(assembly, mesh, material, layer, definitions);
                while (transient.Count > 0)
                {
                    FastenerDefinition definition = transient[0];
                    AssetDatabase.CreateAsset(definition, root + "/FastenerDefinitions/" + definition.DefinitionId + ".asset");
                    transient.RemoveAt(0); // A successfully persisted asset no longer belongs to transient cleanup.
                }
                foreach (Binding binding in GetBindings())
                    definitions[binding.FastenerId] = AssetDatabase.LoadAssetAtPath<FastenerDefinition>(
                        root + "/FastenerDefinitions/" + binding.FastenerId + ".asset");
                int changed = Configure(assembly, mesh, material, layer, definitions);
                foreach (FastenerDefinition definition in definitions.Values) AssetDatabase.SaveAssetIfDirty(definition);
                foreach (string id in GetBindings().Select(b => b.MountId).Distinct())
                    AssetDatabase.SaveAssetIfDirty(assembly.MountPoints.Single(m => m.MountId == id).Definition);
                return changed;
            }
            finally
            {
                foreach (FastenerDefinition definition in transient) UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        // Typed, asset-independent entry point. The complete six-mount preflight
        // runs before any change, including inactive imported duplicate visuals.
        public static int Configure(VehicleAssemblyController assembly, Mesh mesh, Material material,
            int layer, IReadOnlyDictionary<string, FastenerDefinition> definitions)
        {
            List<MountWork> work = PrepareWork(assembly, mesh, material, layer, definitions);
            foreach (MountWork row in work)
                if (EditorUtility.IsPersistent(row.Mount.Definition) &&
                    row.Bolts.Any(bolt => !EditorUtility.IsPersistent(bolt.definition)))
                    throw new InvalidDataException("Persist stock fastener definitions before binding asset mount: " + row.Mount.MountId);
            return ApplyWork(assembly, mesh, material, layer, work);
        }

        private static List<MountWork> PrepareWork(VehicleAssemblyController assembly, Mesh mesh, Material material,
            int layer, IReadOnlyDictionary<string, FastenerDefinition> definitions)
        {
            if (assembly == null || mesh == null || material == null || layer < 0 || layer > 31 || definitions == null)
                throw new InvalidDataException("Missing reviewed stock bolt authoring input.");
            Binding[] bindings = GetBindings();
            var allTargets = assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            var work = new List<MountWork>();
            foreach (IGrouping<string, Binding> group in bindings.GroupBy(b => b.MountId))
            {
                Binding[] members = group.ToArray();
                MountPointAuthoring[] mounts = assembly.MountPoints.Where(m => m != null && m.MountId == group.Key).ToArray();
                if (mounts.Length != 1 || mounts[0].Definition == null)
                    throw new InvalidDataException("Missing/duplicate stock bolt mount: " + group.Key);
                MountPointAuthoring mount = mounts[0];
                MountPointDefinition d = mount.Definition;
                if (d.DefinitionId != group.Key || !d.AcceptedPartDefinitionIds.SequenceEqual(new[] { members[0].PartDefinitionId }))
                    throw new InvalidDataException("Unexpected stock part identity: " + group.Key);
                PartInstance[] parts = assembly.Parts.Where(p => p != null && p.Definition != null &&
                    p.Definition.DefinitionId == members[0].PartDefinitionId).ToArray();
                if (parts.Length != 1) throw new InvalidDataException("Missing/duplicate stock bolt part: " + group.Key);
                bool legacy = d.Fasteners.Length == 0;
                string[] expected = members.Select(b => b.FastenerId).ToArray();
                if (!legacy && !d.Fasteners.Select(v => v != null ? v.DefinitionId : "").SequenceEqual(expected))
                    throw new InvalidDataException("Partial/unknown stock bolt shape: " + group.Key);
                FastenerGroupDefinition fg = d.FastenerGroup;
                bool oldGroup = fg != null && fg.FastenerDefinitionIds.Length == 0 &&
                    fg.AggregateMaximumTightness == 0 && fg.BoltedOffThreshold == 0 &&
                    (fg.BoltedOnThreshold == 0 || fg.BoltedOnThreshold == 1);
                bool newGroup = fg != null && fg.FastenerDefinitionIds.SequenceEqual(expected) &&
                    fg.AggregateMaximumTightness == members.Length * 8 &&
                    fg.BoltedOnThreshold == members[0].OnThreshold && fg.BoltedOffThreshold == 0;
                if ((legacy ? !oldGroup : !newGroup) || fg.SpeedRetentionPolicy != FastenerSpeedRetentionPolicy.None ||
                    fg.BreakAction != FastenerBreakAction.None)
                    throw new InvalidDataException("Unexpected stock bolt latch/retention: " + group.Key);
                var targets = allTargets.Where(t => t.MountId == group.Key).ToArray();
                if (targets.Length != (legacy ? 0 : members.Length))
                    throw new InvalidDataException("Partial/unknown stock bolt targets: " + group.Key);
                if (members[0].Slug == "fuel-tank" &&
                    !assembly.MountPoints.Any(m => m != null && m.MountId == FuelTankPipeMountId))
                    throw new InvalidDataException("Fuel tank filler-pipe removal prerequisite missing.");
                var row = new MountWork(mount, members, legacy);
                foreach (Binding b in members)
                {
                    if (!definitions.TryGetValue(b.FastenerId, out FastenerDefinition f) || !IsReviewedDefinition(f, b))
                        throw new InvalidDataException("Unexpected stock bolt definition: " + b.FastenerId);
                    ToolDefinition[] tools = assembly.Tools.Where(t => t != null && t.ToolType == "Wrench" &&
                        (int)t.Size == b.Size).ToArray();
                    if (tools.Length != 1) throw new InvalidDataException("Missing/duplicate stock wrench: " + b.Size);
                    MeshFilter[] originals = parts[0].GetComponentsInChildren<MeshFilter>(true).Where(filter =>
                        filter.sharedMesh == mesh && filter.GetComponentInParent<PartInstance>(true) == parts[0] &&
                        filter.GetComponentInParent<AssemblyFastenerInteractionTarget>(true) == null &&
                        MatrixMatches(RelativeMatrix(filter.transform, parts[0].transform), b.Pose, b.Scale)).ToArray();
                    if (originals.Length != 1 || originals[0].GetComponent<MeshRenderer>() == null ||
                        originals[0].GetComponentsInChildren<Collider>(true).Length != 0)
                        throw new InvalidDataException("Missing/ambiguous donor stock bolt mesh/pose: " + b.FastenerId);
                    if (!legacy)
                    {
                        AssemblyFastenerInteractionTarget[] same = targets.Where(t => t.FastenerDefinitionId == b.FastenerId).ToArray();
                        if (same.Length != 1 || !IsReviewedTarget(same[0], assembly, mount, b, mesh, material, tools[0], layer) ||
                            originals[0].gameObject.activeSelf)
                            throw new InvalidDataException("Changed authored stock bolt target/duplicate: " + b.FastenerId);
                        if (!ReferenceEquals(f, d.Fasteners[Array.IndexOf(members, b)]))
                            throw new InvalidDataException("Stock bolt definition asset was replaced: " + b.FastenerId);
                    }
                    row.Bolts.Add((b, f, tools[0], originals[0]));
                }
                work.Add(row);
            }
            return work;
        }

        private static int ApplyWork(VehicleAssemblyController assembly, Mesh mesh, Material material,
            int layer, List<MountWork> work)
        {
            int changed = 0;
            foreach (MountWork row in work)
            {
                MountPointDefinition d = row.Mount.Definition;
                if (row.Legacy)
                {
                    var serialized = new SerializedObject(d);
                    SerializedProperty fasteners = serialized.FindProperty("fasteners");
                    fasteners.arraySize = row.Bolts.Count;
                    for (int i = 0; i < row.Bolts.Count; i++)
                        fasteners.GetArrayElementAtIndex(i).objectReferenceValue = row.Bolts[i].definition;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    var group = new FastenerGroupDefinition();
                    group.Configure(row.Bindings.Select(b => b.FastenerId).ToArray(),
                        row.Bolts.Count * 8, row.Bindings[0].OnThreshold, 0);
                    d.ConfigureFastenerGroup(group);
                    EditorUtility.SetDirty(d);
                    changed++;
                    foreach (var item in row.Bolts)
                    {
                        // Existing sanitized source visuals are retained but disabled:
                        // only the project-owned target below the mount is authoritative.
                        item.original.gameObject.SetActive(false);
                        CreateTarget(assembly, row.Mount, item.binding, item.tool, mesh, material, layer);
                        changed++;
                    }
                }
                if (row.Bindings[0].Slug == "fuel-tank" &&
                    !d.RemovalBlockedWhileOccupiedMountIds.Contains(FuelTankPipeMountId))
                {
                    d.ConfigureRemovalBlockers(d.RemovalBlockedWhileOccupiedMountIds.Concat(new[] { FuelTankPipeMountId }).ToArray());
                    EditorUtility.SetDirty(d); changed++;
                }
            }
            return changed;
        }

        private static void CreateTarget(VehicleAssemblyController assembly, MountPointAuthoring mount,
            Binding binding, ToolDefinition tool, Mesh mesh, Material material, int layer)
        {
            var marker = new GameObject(binding.FastenerId) { layer = layer };
            marker.transform.SetParent(mount.Pose, false);
            marker.transform.SetLocalPositionAndRotation(binding.Pose.position, binding.Pose.rotation);
            var visible = new GameObject("Visible inserted bolt or nut") { layer = layer };
            visible.transform.SetParent(marker.transform, false);
            visible.transform.localScale = binding.Scale;
            visible.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visible.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.enabled = false;
            var target = marker.AddComponent<AssemblyFastenerInteractionTarget>();
            // Configure before adding the collider: authoring cannot initialize a
            // live graph while the six immutable definitions are being extended.
            target.Configure(assembly, binding.MountId, binding.FastenerId, tool, true,
                visible.transform, binding.Scale.z);
            SphereCollider collider = marker.AddComponent<SphereCollider>();
            collider.isTrigger = true; collider.radius = .028f; collider.enabled = false;
            InteractionTargetHost host = marker.AddComponent<InteractionTargetHost>();
            host.Configure(target); host.ConfigureOutlineRenderers(renderer); host.ConfigureSelectionPriority(40);
        }

        private static bool IsReviewedDefinition(FastenerDefinition f, Binding b) =>
            f != null && f.DefinitionId == b.FastenerId && (int)f.Size == b.Size && f.MaximumStage == 8 &&
            f.InsertedOnInstall && f.RequiredForRemoval && f.TighteningDirection == FastenerDirection.ClockwiseToTighten &&
            f.ToolRule.ToolType == "Wrench" && (int)f.ToolRule.FastenerSize == b.Size;

        private static bool IsReviewedTarget(AssemblyFastenerInteractionTarget target, VehicleAssemblyController assembly,
            MountPointAuthoring mount, Binding b, Mesh mesh, Material material, ToolDefinition tool, int layer)
        {
            if (target.Controller != assembly || target.transform.parent != mount.Pose || target.gameObject.layer != layer ||
                !MatrixMatches(Matrix4x4.TRS(target.transform.localPosition, target.transform.localRotation,
                    target.transform.localScale), b.Pose, Vector3.one) ||
                Mathf.Abs(target.FastenerPresentationStageTravelScale - b.Scale.z) > .00001f) return false;
            var data = new SerializedObject(target);
            Transform visible = data.FindProperty("fastenerPresentation").objectReferenceValue as Transform;
            if (visible == null || visible.parent != target.transform ||
                !MatrixMatches(Matrix4x4.TRS(visible.localPosition, visible.localRotation, visible.localScale),
                    new Pose(Vector3.zero, Quaternion.identity), b.Scale)) return false;
            MeshFilter filter = visible.GetComponent<MeshFilter>();
            MeshRenderer renderer = visible.GetComponent<MeshRenderer>();
            SphereCollider collider = target.GetComponent<SphereCollider>();
            InteractionTargetHost host = target.GetComponent<InteractionTargetHost>();
            return filter != null && filter.sharedMesh == mesh && renderer != null && renderer.sharedMaterial == material &&
                collider != null && collider.isTrigger && Mathf.Abs(collider.radius - .028f) < .00001f &&
                host != null && host.OutlineRenderers.Count == 1 && host.OutlineRenderers[0] == renderer &&
                data.FindProperty("tool").objectReferenceValue == tool;
        }

        private static Matrix4x4 RelativeMatrix(Transform child, Transform root)
        {
            Matrix4x4 value = Matrix4x4.identity;
            while (child != root)
            {
                if (child == null) throw new InvalidDataException("Stock bolt presentation is outside its explicit part.");
                value = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale) * value;
                child = child.parent;
            }
            return value;
        }

        private static bool MatrixMatches(Matrix4x4 actual, Pose pose, Vector3 scale)
        {
            Matrix4x4 expected = Matrix4x4.TRS(pose.position, pose.rotation, scale);
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(actual[i] - expected[i]) > PoseTolerance) return false;
            return true;
        }

        public readonly struct Binding
        {
            public Binding(string slug, int number, long markerId, int size, int onThreshold, long boltCheckId,
                Vector3 position, Quaternion rotation, Vector3 scale)
            { Slug = slug; Number = number; MarkerId = markerId; Size = size; OnThreshold = onThreshold;
                BoltCheckId = boltCheckId; Pose = new Pose(position, rotation); Scale = scale; }
            public string Slug { get; }
            public int Number { get; }
            public long MarkerId { get; }
            public int Size { get; }
            public int OnThreshold { get; }
            public long BoltCheckId { get; }
            public Pose Pose { get; }
            public Vector3 Scale { get; }
            public string MountId => "mount.satsuma." + Slug;
            public string PartDefinitionId => "vehicle.satsuma.part." + Slug;
            public string FastenerId => "fastener.satsuma." + Slug + ".boltpm-" + Number;
        }

        private sealed class MountWork
        {
            public MountWork(MountPointAuthoring mount, Binding[] bindings, bool legacy)
            { Mount = mount; Bindings = bindings; Legacy = legacy; }
            public MountPointAuthoring Mount { get; }
            public Binding[] Bindings { get; }
            public bool Legacy { get; }
            public List<(Binding binding, FastenerDefinition definition, ToolDefinition tool, MeshFilter original)> Bolts { get; } =
                new List<(Binding, FastenerDefinition, ToolDefinition, MeshFilter)>();
        }
    }
}
