using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Night = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaStartableCarNightBatch;

namespace MSC.Tests.EditMode.LegacyImport
{
    /// <summary>Private generated-content integration gate; never edits the canonical prefab, definitions or saves.</summary>
    public sealed class SatsumaStartableCarNightBatchTests
    {
        [Test]
        public void RefreshedCanonicalPacketHas126Parts124Mounts294FastenersAndZeroRepeat()
        {
            using var f = new Fixture();
            AssertComplete(f.Assembly);
            Assert.That(Night.ApplyToInstance(f.Assembly), Is.Zero);
            Assert.That(Night.ApplyToInstance(f.Assembly), Is.Zero);
            AssertComplete(f.Assembly);
            f.AssertCanonicalFilesUnchanged();
        }

        [Test]
        public void Previous298TargetPacketAddsOnlyFourHeadlightBoltsAndRepeatsZero()
        {
            using var f = new Fixture();
            f.RestorePreValveTopologyInTestCopy();
            f.RestorePreHeadlightTopologyInTestCopy();
            var previous = Night.ValidateTopology(f.Assembly, false);
            Assert.That(previous.FastenerCount, Is.EqualTo(298));
            Assert.That(previous.MountCount, Is.EqualTo(124));
            Assert.Throws<InvalidDataException>(() => Night.ValidateTopology(f.Assembly, true));
            Assert.That(Night.ApplyToInstance(f.Assembly), Is.EqualTo(6));
            Assert.That(Night.ApplyToInstance(f.Assembly), Is.Zero);
            AssertComplete(f.Assembly, 302);
            f.AssertCanonicalFilesUnchanged();
        }

        [Test]
        public void Previous302GraphWithoutFixedFittingAddsOnlyOptionalConnectionAndRepeatsZero()
        {
            using var f = new Fixture();
            f.RestorePreValveTopologyInTestCopy();
            f.RemoveFuelLineFromTestCopy();
            string[] ids = f.Assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Select(target => target.FastenerDefinitionId).ToArray();
            AssertComplete(f.Assembly, 302);
            Assert.That(Night.ApplyToInstance(f.Assembly), Is.EqualTo(3));
            Assert.That(Night.ApplyToInstance(f.Assembly), Is.Zero);
            Assert.That(f.Assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Select(target => target.FastenerDefinitionId), Is.EqualTo(ids));
            Assert.That(f.Assembly.GetComponent<SatsumaFuelLineConnection>(), Is.Not.Null);
            AssertComplete(f.Assembly, 302);
            f.AssertCanonicalFilesUnchanged();
        }

        [Test]
        public void Original117Mount273TargetShapeUpgradesAdditivelyAndSecondPassChangesNothing()
        {
            using var f = new Fixture();
            PartInstance[] parts = (PartInstance[])f.Assembly.Parts.Clone();
            string[] identities = parts.Select(part => part.StableId.Value).ToArray();
            f.RestorePreValveTopologyInTestCopy();
            f.RestorePreNightTopologyInTestCopy();
            var old = Night.ValidateTopology(f.Assembly, false);
            Assert.That(old.PartCount, Is.EqualTo(126));
            Assert.That(old.MountCount, Is.EqualTo(117));
            Assert.That(old.FastenerCount, Is.EqualTo(273));
            Assert.That(old.ConsumableMountCount, Is.Zero);
            Assert.That(Night.ApplyToInstance(f.Assembly), Is.GreaterThan(0));
            Assert.That(Night.ApplyToInstance(f.Assembly), Is.Zero);
            AssertComplete(f.Assembly, 302);
            Assert.That(f.Assembly.Parts, Is.EqualTo(parts));
            Assert.That(f.Assembly.Parts.Select(part => part.StableId.Value), Is.EqualTo(identities));
            f.AssertCanonicalFilesUnchanged();
        }

        [TestCase("stock-target")]
        [TestCase("headlight-target")]
        [TestCase("headlights-without-stock")]
        [TestCase("consumable-socket")]
        [TestCase("foreign-base-id")]
        public void PartialOrSameCountForeignRostersRejectBeforeApplyingOtherPackets(string corruption)
        {
            using var f = new Fixture();
            if (corruption == "stock-target" || corruption == "headlight-target")
            {
                string id = corruption == "stock-target"
                    ? Phase1SatsumaStockMountFastenerAuthoring.GetBindings()[0].FastenerId
                    : Phase1SatsumaHeadlightFastenerAuthoring.GetBindings()[0].FastenerId;
                Object.DestroyImmediate(f.Assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Single(target => target.FastenerDefinitionId == id).gameObject);
            }
            else if (corruption == "headlights-without-stock")
            {
                string[] stockIds = Phase1SatsumaStockMountFastenerAuthoring.GetBindings().Select(binding => binding.FastenerId).ToArray();
                foreach (var target in f.Assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Where(value => stockIds.Contains(value.FastenerDefinitionId))) Object.DestroyImmediate(target.gameObject);
            }
            else if (corruption == "consumable-socket")
            {
                MountPointAuthoring mount = f.Assembly.MountPoints.Single(value =>
                    value.MountId == Phase1SatsumaConsumableMountAuthoring.MountIds[0]);
                f.SetMountRegistry(f.Assembly.MountPoints.Where(value => value != mount).ToArray());
                Object.DestroyImmediate(mount.gameObject);
            }
            else
            {
                var target = f.Assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .First(value => value.FastenerDefinitionId == "fastener.satsuma.engine-assembly.boltpm-1");
                var data = new SerializedObject(target);
                data.FindProperty("fastenerDefinitionId").stringValue = "fastener.satsuma.unreviewed.boltpm-1";
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            int mounts = f.Assembly.MountPoints.Length;
            int targets = f.Assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Length;
            Assert.Throws<InvalidDataException>(() => Night.ApplyToInstance(f.Assembly));
            Assert.That(f.Assembly.MountPoints.Length, Is.EqualTo(mounts));
            Assert.That(f.Assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Length, Is.EqualTo(targets));
            f.AssertCanonicalFilesUnchanged();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrDuplicateDefinitionReportsExactMountAndBinding(bool duplicate)
        {
            using var f = new Fixture();
            MountPointAuthoring mount = f.Assembly.MountPoints.Single(value => value.MountId == "mount.satsuma.engine-assembly");
            FastenerDefinition other = f.Assembly.MountPoints.First(value => value != mount && value.Definition.Fasteners.Length > 0)
                .Definition.Fasteners[0];
            var data = new SerializedObject(mount.Definition);
            data.FindProperty("fasteners").GetArrayElementAtIndex(0).objectReferenceValue = duplicate ? other : null;
            data.ApplyModifiedPropertiesWithoutUndo();
            InvalidDataException error = Assert.Throws<InvalidDataException>(() => Night.ValidateTopology(f.Assembly, true));
            Assert.That(error.Message, Does.Contain(mount.MountId));
            Assert.That(error.Message, Does.Contain(duplicate ? other.DefinitionId : "index=0"));
            f.AssertCanonicalFilesUnchanged();
        }

        private static void AssertComplete(VehicleAssemblyController assembly, int expectedFasteners = 294)
        {
            var topology = Night.ValidateTopology(assembly, true);
            Assert.That(topology.PartCount, Is.EqualTo(126));
            Assert.That(topology.MountCount, Is.EqualTo(124));
            Assert.That(topology.FastenerCount, Is.EqualTo(expectedFasteners));
            Assert.That(topology.ConsumableMountCount, Is.EqualTo(7));
            foreach (string id in Phase1SatsumaConsumableMountAuthoring.MountIds)
                Assert.That(assembly.MountPoints.Count(mount => mount.MountId == id), Is.EqualTo(1));
        }

        private sealed class Fixture : IDisposable
        {
            private readonly string folder;
            private readonly GameObject root;
            private readonly string[] protectedPaths;
            private readonly string protectedHash;
            public VehicleAssemblyController Assembly { get; }

            public Fixture()
            {
                string prefab = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
                if (!File.Exists(prefab)) Assert.Ignore("Private Satsuma prefab is not generated.");
                var authored = AssetDatabase.LoadAssetAtPath<GameObject>(prefab)?.GetComponent<VehicleAssemblyController>();
                Assert.That(authored, Is.Not.Null);
                // Run the explicit night refresh first. Tests must never install
                // missing canonical assets merely as a fixture side effect.
                AssertComplete(authored);
                protectedPaths = new[] { prefab }.Concat(Directory.GetFiles(Night.GeneratedRoot + "/MountDefinitions", "*.asset"))
                    .Concat(Directory.GetFiles(Night.GeneratedRoot + "/FastenerDefinitions", "*.asset"))
                    .Concat(Directory.GetFiles(Night.GeneratedRoot + "/LoosePartDefinitions", "*.asset"))
                    .Concat(Directory.GetFiles(Night.GeneratedRoot + "/ToolDefinitions", "*.asset"))
                    .Append(Night.GeneratedRoot + "/VehicleItemPartCatalog.asset")
                    .Append(Night.GeneratedRoot + "/InstalledAlternatorBeltPresentation.prefab")
                    .OrderBy(path => path, StringComparer.Ordinal).ToArray();
                protectedHash = FilesHash(protectedPaths);
                folder = "Assets/__SatsumaNightTests_" + Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                try
                {
                    root = PrefabUtility.LoadPrefabContents(prefab);
                    Assembly = root.GetComponent<VehicleAssemblyController>();
                    // Test helpers persist changed mount definitions. Redirect all
                    // base definitions to disposable project-owned test assets;
                    // the seven canonical consumable definitions remain read-only.
                    foreach (MountPointAuthoring mount in Assembly.MountPoints)
                    {
                        if (Phase1SatsumaConsumableMountAuthoring.MountIds.Contains(mount.MountId)) continue;
                        MountPointDefinition copy = Object.Instantiate(mount.Definition);
                        AssetDatabase.CreateAsset(copy, folder + "/" + mount.MountId + ".asset");
                        var serialized = new SerializedObject(mount);
                        serialized.FindProperty("definition").objectReferenceValue = copy;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                catch
                {
                    if (root != null) PrefabUtility.UnloadPrefabContents(root);
                    AssetDatabase.DeleteAsset(folder);
                    throw;
                }
            }

            public void RestorePreNightTopologyInTestCopy()
            {
                RemoveFuelLineFromTestCopy();
                RestorePreHeadlightTopologyInTestCopy();
                string[] sockets = Phase1SatsumaConsumableMountAuthoring.MountIds;
                MountPointAuthoring[] removed = Assembly.MountPoints.Where(mount => sockets.Contains(mount.MountId)).ToArray();
                SetMountRegistry(Assembly.MountPoints.Except(removed).ToArray());
                foreach (MountPointAuthoring mount in removed) Object.DestroyImmediate(mount.gameObject);
                foreach (var cohort in Phase1SatsumaStockMountFastenerAuthoring.GetBindings().GroupBy(binding => binding.MountId))
                {
                    MountPointAuthoring mount = Assembly.MountPoints.Single(value => value.MountId == cohort.Key);
                    foreach (AssemblyFastenerInteractionTarget target in mount.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true))
                        Object.DestroyImmediate(target.gameObject);
                    var definition = new SerializedObject(mount.Definition);
                    definition.FindProperty("fasteners").arraySize = 0;
                    definition.ApplyModifiedPropertiesWithoutUndo();
                    mount.Definition.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(Array.Empty<FastenerDefinition>()));
                }
                foreach (MountPointAuthoring mount in Assembly.MountPoints)
                    mount.Definition.ConfigureRetainedRemovalChildren(mount.Definition.RemovalRetainedChildMountIds
                        .Where(id => !sockets.Contains(id)).ToArray());
            }

            public void RestorePreValveTopologyInTestCopy()
            {
                MountPointAuthoring mount = Assembly.MountPoints.Single(value => value.MountId == SatsumaRockerShaftFastenerMigration.MountId);
                var old = mount.Definition.Fasteners.ToList();
                foreach (string id in SatsumaRockerShaftFastenerMigration.RetiredIds)
                {
                    FastenerDefinition definition = AssetDatabase.LoadAssetAtPath<FastenerDefinition>(Night.GeneratedRoot + "/FastenerDefinitions/" + id + ".asset");
                    Assert.That(definition, Is.Not.Null, "Retired definition assets remain available for historical comparison.");
                    old.Add(definition);
                    var marker = new GameObject("Historical valve pseudo-fastener " + id);
                    marker.transform.SetParent(mount.transform, false);
                    var target = marker.AddComponent<AssemblyFastenerInteractionTarget>();
                    var state = new SerializedObject(target);
                    state.FindProperty("controller").objectReferenceValue = Assembly;
                    state.FindProperty("mountId").stringValue = mount.MountId;
                    state.FindProperty("fastenerDefinitionId").stringValue = id;
                    state.ApplyModifiedPropertiesWithoutUndo();
                }
                var serialized = new SerializedObject(mount.Definition);
                SerializedProperty fasteners = serialized.FindProperty("fasteners");
                fasteners.arraySize = old.Count;
                for (int index = 0; index < old.Count; index++) fasteners.GetArrayElementAtIndex(index).objectReferenceValue = old[index];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var group = new FastenerGroupDefinition();
                group.Configure(old.Select(value => value.DefinitionId).ToArray(), 104, 1, 0);
                mount.Definition.ConfigureFastenerGroup(group);
            }

            public void RemoveFuelLineFromTestCopy()
            {
                Assembly.GetComponent<VehiclePersistenceBinding>().ConfigureFuelLineConnection(null);
                foreach (var target in Assembly.GetComponentsInChildren<SatsumaFuelLineFastenerInteractionTarget>(true))
                    Object.DestroyImmediate(target.gameObject);
                var state = Assembly.GetComponent<SatsumaFuelLineConnection>();
                if (state != null) Object.DestroyImmediate(state);
            }

            public void RestorePreHeadlightTopologyInTestCopy()
            {
                Mesh shortBolt = AssetDatabase.LoadAssetAtPath<Mesh>(Night.GeneratedRoot + "/Meshes/" +
                    Phase1SatsumaHeadlightFastenerAuthoring.ShortBoltMeshGuid + ".asset");
                Assert.That(shortBolt, Is.Not.Null);
                foreach (var cohort in Phase1SatsumaHeadlightFastenerAuthoring.GetBindings().GroupBy(binding => binding.MountId))
                {
                    MountPointAuthoring mount = Assembly.MountPoints.Single(value => value.MountId == cohort.Key);
                    PartInstance part = Assembly.Parts.Single(value => value.Definition.DefinitionId == cohort.First().PartDefinitionId);
                    foreach (var binding in cohort)
                    {
                        // Restore only the reviewed imported leaf, not the whole
                        // legacy Bolts subtree or another part's coincident mesh.
                        Matrix4x4 expected = Matrix4x4.TRS(binding.Pose.position, binding.Pose.rotation, binding.Scale);
                        MeshFilter original = part.GetComponentsInChildren<MeshFilter>(true).Single(filter =>
                            filter.sharedMesh == shortBolt && filter.GetComponentInParent<PartInstance>(true) == part &&
                            filter.GetComponentInParent<AssemblyFastenerInteractionTarget>(true) == null &&
                            MatricesMatch(RelativeMatrix(filter.transform, part.transform), expected));
                        original.gameObject.SetActive(true);
                        AssemblyFastenerInteractionTarget target = mount.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                            .Single(value => value.FastenerDefinitionId == binding.FastenerId);
                        Object.DestroyImmediate(target.gameObject);
                    }
                    var definition = new SerializedObject(mount.Definition);
                    definition.FindProperty("fasteners").arraySize = 0;
                    definition.ApplyModifiedPropertiesWithoutUndo();
                    mount.Definition.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(Array.Empty<FastenerDefinition>()));
                }
            }

            private static Matrix4x4 RelativeMatrix(Transform child, Transform owner)
            {
                Matrix4x4 result = Matrix4x4.identity;
                while (child != owner)
                {
                    Assert.That(child, Is.Not.Null);
                    result = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale) * result;
                    child = child.parent;
                }
                return result;
            }

            private static bool MatricesMatch(Matrix4x4 actual, Matrix4x4 expected)
            {
                for (int index = 0; index < 16; index++)
                    if (Mathf.Abs(actual[index] - expected[index]) > .0001f) return false;
                return true;
            }

            public void SetMountRegistry(MountPointAuthoring[] mounts)
            {
                var data = new SerializedObject(Assembly);
                SerializedProperty array = data.FindProperty("mountPoints");
                array.arraySize = mounts.Length;
                for (int index = 0; index < mounts.Length; index++) array.GetArrayElementAtIndex(index).objectReferenceValue = mounts[index];
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            public void AssertCanonicalFilesUnchanged() => Assert.That(FilesHash(protectedPaths), Is.EqualTo(protectedHash));

            public void Dispose()
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
                if (!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets/__SatsumaNightTests_", StringComparison.Ordinal))
                    AssetDatabase.DeleteAsset(folder);
            }

            private static string FilesHash(IEnumerable<string> paths)
            {
                using var sha = SHA256.Create();
                var identities = new StringBuilder();
                foreach (string path in paths)
                {
                    using var stream = File.OpenRead(path);
                    identities.Append(path).Append(':').Append(BitConverter.ToString(sha.ComputeHash(stream))).Append('\n');
                }
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identities.ToString())));
            }
        }
    }
}
