using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.ItemsIntegration;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Seven additive purchased-item sockets. No duplicate starting items or static plug meshes.</summary>
    public static class Phase1SatsumaConsumableMountAuthoring
    {
        public const string CanonicalRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        public const string HeadId = "vehicle.satsuma.part.cylinder-head";
        public const string BlockId = "vehicle.satsuma.part.engine-block";
        public const string LeftHeadlightId = "vehicle.satsuma.part.headlight-left";
        public const string RightHeadlightId = "vehicle.satsuma.part.headlight-right";
        public static readonly string[] MountIds =
        {
            "mount.satsuma.cylinder-head.spark-plug-1", "mount.satsuma.cylinder-head.spark-plug-2",
            "mount.satsuma.cylinder-head.spark-plug-3", "mount.satsuma.cylinder-head.spark-plug-4",
            SatsumaConsumableAssemblyRules.BeltMountId,
            "mount.satsuma.headlight-left.light-bulb", "mount.satsuma.headlight-right.light-bulb",
        };
        public static readonly string[] BeltPrerequisites =
        {
            "mount.satsuma.crankshaft.crankshaft-pulley", "mount.satsuma.water-pump.water-pump-pulley",
            SatsumaConsumableAssemblyRules.AlternatorMountId,
        };

        public sealed class DefinitionSet
        {
            public PartDefinition[] Parts;
            public MountPointDefinition[] Mounts;
            public FastenerDefinition[] Fasteners;
            public ToolDefinition SparkPlugWrench;
            public VehicleItemPartCatalog Catalog;
            public GameObject InstalledBeltPresentation;
        }

        public static Pose LocalPose(int index)
        {
            if (index >= 0 && index < 4)
                return new Pose(new Vector3(new[] { .122430325f, .035472274f, -.05150169f, -.13845968f }[index],
                    .07151509f, .007053325f), new Quaternion(-.38268396f, 0f, 0f, .9238794f));
            if (index == 4) return new Pose(new Vector3(.24000037f, -.0199996f, .010000048f), Quaternion.identity);
            if (index == 5 || index == 6)
                return new Pose(new Vector3(-.0021003543f, -.0084f, .0023996313f), Quaternion.identity);
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        public static Vector3 LocalTriggerPosition(int index)
        {
            if (index == 5 || index == 6) return new Vector3(0f, -.0273f, 0f);
            if (index == 4) return new Vector3(.24f, -.0199996f, .009999989f);
            return LocalPose(index).position;
        }

        public static string OwnerId(int index) => index < 4 ? HeadId : index == 4 ? BlockId :
            index == 5 ? LeftHeadlightId : index == 6 ? RightHeadlightId : throw new ArgumentOutOfRangeException(nameof(index));

        public static DefinitionSet CreateDefinitions(PartDefinition oilFilter)
        {
            if (oilFilter == null || oilFilter.DefinitionId != "vehicle.satsuma.part.oilfilter0")
                throw new InvalidDataException("The purchased-filter mapping must reuse the existing stock definition.");
            var set = new DefinitionSet
            {
                Parts = new PartDefinition[3], Mounts = new MountPointDefinition[7],
                Fasteners = new FastenerDefinition[4], SparkPlugWrench = SatsumaAuxiliaryAssemblyTools.CreateSparkPlugWrench(),
                Catalog = ScriptableObject.CreateInstance<VehicleItemPartCatalog>(),
            };
            string[] partIds = { SatsumaConsumableAssemblyRules.SparkPlugPartId,
                SatsumaConsumableAssemblyRules.BeltPartId, SatsumaConsumableAssemblyRules.BulbPartId };
            string[] names = { "Свеча зажигания", "Ремень генератора", "Лампочка фары" };
            for (int index = 0; index < 3; index++)
            {
                var rules = new List<PartCompatibilityRule>();
                for (int socket = 0; socket < 7; socket++)
                    if ((socket < 4 ? 0 : socket == 4 ? 1 : 2) == index)
                        rules.Add(PartCompatibilityRule.Create(SocketType(socket), OwnerId(socket)));
                PartDefinition part = ScriptableObject.CreateInstance<PartDefinition>();
                part.Configure(partIds[index], names[index], index == 2 ? PartCategory.Electrical : PartCategory.Engine,
                    index == 1 ? .5f : .2f, null, rules.ToArray());
                set.Parts[index] = part;
            }
            for (int index = 0; index < 7; index++)
            {
                FastenerDefinition[] fasteners = Array.Empty<FastenerDefinition>();
                if (index < 4)
                {
                    var thread = ScriptableObject.CreateInstance<FastenerDefinition>();
                    thread.Configure(SatsumaConsumableAssemblyRules.SparkPlugFastenerId(index + 1), "Свеча №" + (index + 1),
                        FastenerSize.None, 8, FastenerDirection.ClockwiseToTighten, true, true,
                        SatsumaAuxiliaryAssemblyTools.SparkPlugWrenchRule());
                    set.Fasteners[index] = thread;
                    fasteners = new[] { thread };
                }
                var mount = ScriptableObject.CreateInstance<MountPointDefinition>();
                mount.Configure(MountIds[index], index < 4 ? "Свеча №" + (index + 1) : index == 4 ? names[1] : names[2],
                    SocketType(index), OwnerId(index), new[] { partIds[index < 4 ? 0 : index == 4 ? 1 : 2] },
                    new MountConstraint(.22f, 50f, .75f, 0f), .03f, fasteners);
                // The donor plug has no separate Bolted FSM. The existing compatibility
                // latch mirrors Tightness>0 for removal; full safety still requires 8.
                mount.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(fasteners));
                if (index == 4) mount.ConfigureInstallationOccupancy(BeltPrerequisites);
                set.Mounts[index] = mount;
            }
            set.Catalog.Configure(new[]
            {
                new VehicleItemPartRecord("item.spark-plug", set.Parts[0]),
                new VehicleItemPartRecord("item.alternator-belt", set.Parts[1]),
                new VehicleItemPartRecord("item.light-bulb", set.Parts[2]),
                new VehicleItemPartRecord("item.oil-filter", oilFilter),
            });
            return set;
        }

        public static DefinitionSet GetOrCreateDefinitions(VehicleAssemblyController assembly, string generatedRoot)
        {
            PartDefinition filter = assembly.Parts.Single(part => part != null &&
                part.Definition.DefinitionId == "vehicle.satsuma.part.oilfilter0").Definition;
            DefinitionSet set = CreateDefinitions(filter);
            for (int index = 0; index < set.Parts.Length; index++)
                set.Parts[index] = Persist(set.Parts[index], generatedRoot + "/LoosePartDefinitions/" + set.Parts[index].DefinitionId + ".asset");
            for (int index = 0; index < set.Fasteners.Length; index++)
                set.Fasteners[index] = Persist(set.Fasteners[index], generatedRoot + "/FastenerDefinitions/" + set.Fasteners[index].DefinitionId + ".asset");
            for (int index = 0; index < set.Mounts.Length; index++)
            {
                // Rewrite temporary references before validation/persistence.
                MountPointDefinition value = set.Mounts[index];
                value.Configure(value.DefinitionId, value.DisplayName, value.SocketType, value.OwnerPartDefinitionId,
                    value.AcceptedPartDefinitionIds, value.Constraint, value.ReferenceCandidateRadiusMeters,
                    index < 4 ? new[] { set.Fasteners[index] } : Array.Empty<FastenerDefinition>());
                set.Mounts[index] = Persist(value, generatedRoot + "/MountDefinitions/" + value.DefinitionId + ".asset");
            }
            set.SparkPlugWrench = Persist(set.SparkPlugWrench, generatedRoot + "/ToolDefinitions/spark-plug-wrench.asset");
            set.Catalog.Configure(new[]
            {
                new VehicleItemPartRecord("item.spark-plug", set.Parts[0]),
                new VehicleItemPartRecord("item.alternator-belt", set.Parts[1]),
                new VehicleItemPartRecord("item.light-bulb", set.Parts[2]),
                new VehicleItemPartRecord("item.oil-filter", filter),
            });
            set.Catalog = Persist(set.Catalog, generatedRoot + "/VehicleItemPartCatalog.asset");
            set.InstalledBeltPresentation = Phase1SatsumaConsumableBeltPresentationAuthoring.GetOrCreatePrefab(generatedRoot);
            return set;
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly, DefinitionSet set)
        {
            if (assembly == null || set == null || set.Parts.Length != 3 || set.Mounts.Length != 7 ||
                set.Fasteners.Length != 4 || set.SparkPlugWrench == null || set.Catalog == null)
                throw new InvalidDataException("The complete reviewed consumable definition packet is required.");
            int fastenerLayer = RequireFastenerLayer();
            var owners = new PartInstance[7];
            for (int index = 0; index < 7; index++)
            {
                owners[index] = assembly.Parts.Single(part => part != null && part.Definition.DefinitionId == OwnerId(index));
                if (set.Mounts[index].DefinitionId != MountIds[index]) throw new InvalidDataException("Consumable mount order drift.");
            }
            int existing = assembly.MountPoints.Count(mount => MountIds.Contains(mount.MountId));
            if (existing != 0 && existing != 7) throw new InvalidDataException("Reject partial consumable socket migration.");
            foreach (string prerequisite in BeltPrerequisites)
                if (!assembly.MountPoints.Any(mount => mount.MountId == prerequisite))
                    throw new InvalidDataException("Missing reviewed belt prerequisite: " + prerequisite);
            if (existing == 7)
                for (int index = 0; index < 7; index++) ValidateMount(assembly, set, owners[index], index);

            int changes = 0;
            if (existing == 7)
                for (int index = 0; index < 4; index++)
                {
                    var thread = assembly.MountPoints.Single(value => value.MountId == MountIds[index])
                        .GetComponentInChildren<AssemblyFastenerInteractionTarget>(true);
                    if (thread.gameObject.layer == fastenerLayer) continue;
                    thread.gameObject.layer = fastenerLayer;
                    changes++;
                }
            if (existing == 0)
            {
                var mounts = new List<MountPointAuthoring>(assembly.MountPoints);
                for (int index = 0; index < 7; index++)
                {
                    var point = new GameObject("Purchased item socket " + (index + 1));
                    point.layer = owners[index].gameObject.layer;
                    point.transform.SetParent(owners[index].transform, false);
                    Pose pose = LocalPose(index);
                    point.transform.SetLocalPositionAndRotation(pose.position, pose.rotation);
                    var mount = point.AddComponent<MountPointAuthoring>();
                    mount.Configure(set.Mounts[index], MountIds[index], point.transform, 0);
                    point.AddComponent<AssemblyOwnedMountAuthoring>().Configure(owners[index], false);
                    Vector3 trigger = Quaternion.Inverse(pose.rotation) * (LocalTriggerPosition(index) - pose.position);
                    point.AddComponent<AssemblyMountInteractionAnchor>().Configure(trigger);
                    var shape = point.AddComponent<SphereCollider>();
                    shape.isTrigger = true; shape.center = trigger; shape.radius = .03f;
                    var target = point.AddComponent<AssemblyMountHandoffTarget>();
                    target.Configure(assembly, mount);
                    var host = point.AddComponent<InteractionTargetHost>();
                    host.Configure(target); host.ConfigureSelectionPriority(40);
                    if (index < 4) CreatePlugThreadTarget(point.transform, assembly, set, index);
                    mounts.Add(mount);
                    changes++;
                }
                SetObjectArray(assembly, "mountPoints", mounts.Cast<UnityEngine.Object>().ToArray());
            }
            if (!assembly.Tools.Contains(set.SparkPlugWrench))
            {
                if (assembly.Tools.Any(tool => tool != null && tool.DefinitionId == set.SparkPlugWrench.DefinitionId))
                    throw new InvalidDataException("A conflicting spark-plug wrench is already registered.");
                SetObjectArray(assembly, "tools", assembly.Tools.Cast<UnityEngine.Object>().Append(set.SparkPlugWrench).ToArray());
                changes++;
            }
            var bridge = assembly.GetComponent<VehicleItemAssemblyBridge>();
            if (bridge == null) { bridge = assembly.gameObject.AddComponent<VehicleItemAssemblyBridge>(); changes++; }
            if (bridge.Assembly != assembly || bridge.Catalog != set.Catalog)
            { bridge.Configure(assembly, set.Catalog); changes++; }
            var binding = assembly.GetComponent<AssemblyConsumablePresentationBinding>();
            if (binding == null) { binding = assembly.gameObject.AddComponent<AssemblyConsumablePresentationBinding>(); changes++; }
            if (binding.Assembly != assembly) { binding.Configure(assembly); changes++; }
            if (set.InstalledBeltPresentation != null && binding.InstalledBeltPresentation != set.InstalledBeltPresentation)
            { binding.ConfigureBeltPresentation(set.InstalledBeltPresentation); changes++; }
            // SetParent in the donor retains these nested wrappers when their owner
            // is removed. Do not add inferred inverse-install blockers to those sockets.
            for (int index = 0; index < 7; index++)
            {
                MountPointAuthoring ownerMount = assembly.MountPoints.Single(mount =>
                    mount.Definition.AcceptedPartDefinitionIds.Contains(OwnerId(index)));
                if (ownerMount.Definition.RemovalRetainedChildMountIds.Contains(MountIds[index])) continue;
                ownerMount.Definition.ConfigureRetainedRemovalChildren(ownerMount.Definition.RemovalRetainedChildMountIds.Append(MountIds[index]).ToArray());
                EditorUtility.SetDirty(ownerMount.Definition);
                changes++;
            }
            return changes;
        }

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Purchased Consumable Mounts")]
        public static void RefreshConsumableMountsBatch()
        {
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            GameObject contents = PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                var assembly = contents.GetComponent<VehicleAssemblyController>();
                DefinitionSet definitions = GetOrCreateDefinitions(assembly, CanonicalRoot);
                int changes = ApplyToInstance(assembly, definitions);
                int repeat = ApplyToInstance(assembly, definitions);
                if (repeat != 0) throw new InvalidDataException("Consumable authoring is not idempotent.");
                if (changes > 0) PrefabUtility.SaveAsPrefabAsset(contents, Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
                foreach (MountPointAuthoring mount in assembly.MountPoints) AssetDatabase.SaveAssetIfDirty(mount.Definition);
                Debug.Log($"SATSUMA_CONSUMABLE_MOUNTS_OK changes={changes} repeat={repeat} newSockets=7 newThreads=4 authoredParts={assembly.Parts.Length}");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static string SocketType(int index) => "satsuma.socket." + MountIds[index].Substring("mount.satsuma.".Length);

        private static void CreatePlugThreadTarget(Transform parent, VehicleAssemblyController assembly, DefinitionSet set, int index)
        {
            var go = new GameObject("Spark-plug thread interaction");
            go.layer = RequireFastenerLayer(); go.transform.SetParent(parent, false);
            var shape = go.AddComponent<BoxCollider>();
            shape.isTrigger = true; shape.center = new Vector3(0f, 0f, -.01f);
            shape.size = new Vector3(.025f, .025f, .10639f);
            var target = go.AddComponent<AssemblyFastenerInteractionTarget>();
            // Avoid graph initialization while authoring still owns the serialized registry.
            target.Configure(null, MountIds[index], set.Fasteners[index].DefinitionId, set.SparkPlugWrench, false);
            var serialized = new SerializedObject(target);
            serialized.FindProperty("controller").objectReferenceValue = assembly;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            shape.enabled = false;
            var host = go.AddComponent<InteractionTargetHost>();
            host.Configure(target); host.ConfigureSelectionPriority(60);
        }

        private static void ValidateMount(VehicleAssemblyController assembly, DefinitionSet set, PartInstance owner, int index)
        {
            MountPointAuthoring mount = assembly.MountPoints.Single(value => value.MountId == MountIds[index]);
            Pose expected = LocalPose(index);
            if (mount.Definition != set.Mounts[index] || mount.transform.parent != owner.transform ||
                mount.Pose != mount.transform || Vector3.Distance(mount.transform.localPosition, expected.position) > .000001f ||
                Mathf.Abs(Quaternion.Dot(mount.transform.localRotation, expected.rotation)) < .999999f ||
                mount.GetComponent<AssemblyOwnedMountAuthoring>()?.OwnerPart != owner ||
                mount.GetComponent<AssemblyMountHandoffTarget>() == null)
                throw new InvalidDataException("Reviewed consumable socket drift: " + MountIds[index]);
            Vector3 trigger = Quaternion.Inverse(expected.rotation) * (LocalTriggerPosition(index) - expected.position);
            SphereCollider shape = mount.GetComponent<SphereCollider>();
            if (shape == null || !shape.isTrigger || shape.radius != .03f ||
                Vector3.Distance(shape.center, trigger) > .000001f ||
                mount.GetComponent<AssemblyMountInteractionAnchor>() == null)
                throw new InvalidDataException("Reviewed consumable trigger drift: " + MountIds[index]);
            var threads = mount.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            if (threads.Length != (index < 4 ? 1 : 0) || index < 4 &&
                (threads[0].Controller != assembly || threads[0].MountId != MountIds[index] ||
                 threads[0].FastenerDefinitionId != set.Fasteners[index].DefinitionId || threads[0].GetComponentInChildren<Renderer>() != null))
                throw new InvalidDataException("Reviewed spark-plug interaction drift: " + MountIds[index]);
            if (index < 4 && threads[0].gameObject.layer != RequireFastenerLayer() &&
                threads[0].gameObject.layer != mount.gameObject.layer)
                throw new InvalidDataException("Unreviewed spark-plug ray layer: " + MountIds[index]);
        }

        private static int RequireFastenerLayer()
        {
            int layer = LayerMask.NameToLayer(FastenerToolRaycastLayer.Name);
            if (layer < 0) throw new InvalidDataException("Missing dedicated fastener ray layer: " + FastenerToolRaycastLayer.Name);
            return layer;
        }

        private static T Persist<T>(T candidate, string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                candidate.name = existing.name;
                if (JsonUtility.ToJson(existing) != JsonUtility.ToJson(candidate))
                    throw new InvalidDataException("Existing consumable definition differs: " + path);
                UnityEngine.Object.DestroyImmediate(candidate);
                return existing;
            }
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidDataException("Consumable asset path contains the wrong type: " + path);
            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                throw new InvalidDataException("Expected existing generated definition directory: " + folder);
            AssetDatabase.CreateAsset(candidate, path);
            return candidate;
        }

        private static void SetObjectArray(UnityEngine.Object target, string property, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(property);
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++) array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
