using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MSC.Core.Identity;
using MSC.Interaction.Query;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Authoring = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaHeadlightFastenerAuthoring;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaHeadlightFastenerAuthoringTests
    {
        [Test]
        public void FourExactSevenMillimeterBoltsPreserveOtherRulesAndRepeatChangesNothing()
        {
            using var f = new Fixture();
            foreach (MountPointAuthoring mount in f.Mounts)
            {
                mount.Definition.ConfigureRetainedRemovalChildren(new[] { mount.MountId + ".light-bulb" });
                mount.Definition.ConfigureRemovalBlockers(new[] { "test.existing.blocker" });
            }
            PartInstance[] parts = (PartInstance[])f.Assembly.Parts.Clone();
            Assert.That(f.Apply(), Is.EqualTo(6));
            Assert.That(Authoring.GetBindings().Select(b => b.MarkerId), Is.EqualTo(new long[] { 47993, 64196, 39354, 58481 }));
            Assert.That(Authoring.GetBindings().Select(b => b.ScrewFsmId), Is.EqualTo(new long[] { 107507, 112081, 105056, 110431 }));
            Assert.That(f.Assembly.Parts, Is.EqualTo(parts));
            Assert.That(f.Assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Length, Is.EqualTo(4));
            foreach (var cohort in Authoring.GetBindings().GroupBy(b => b.MountId))
            {
                MountPointDefinition definition = f.Mounts.Single(m => m.MountId == cohort.Key).Definition;
                Assert.That(definition.Fasteners.Select(b => b.DefinitionId), Is.EqualTo(cohort.Select(b => b.FastenerId)));
                Assert.That(definition.FastenerGroup.AggregateMaximumTightness, Is.EqualTo(16));
                Assert.That(definition.FastenerGroup.BoltedOnThreshold, Is.EqualTo(2));
                Assert.That(definition.FastenerGroup.BoltedOffThreshold, Is.Zero);
                Assert.That(definition.RemovalRetainedChildMountIds, Is.EqualTo(new[] { cohort.Key + ".light-bulb" }));
                Assert.That(definition.RemovalBlockedWhileOccupiedMountIds, Is.EqualTo(new[] { "test.existing.blocker" }));
                foreach (FastenerDefinition bolt in definition.Fasteners)
                {
                    Assert.That((int)bolt.Size, Is.EqualTo(7));
                    Assert.That(bolt.ToolRule.ToolType, Is.EqualTo("Wrench"));
                    Assert.That((int)bolt.ToolRule.FastenerSize, Is.EqualTo(7));
                    Assert.That(bolt.MaximumStage, Is.EqualTo(8));
                    Assert.That(bolt.InsertedOnInstall && bolt.RequiredForRemoval, Is.True);
                }
            }
            Assert.That(f.Apply(), Is.Zero);
        }

        [Test]
        public void AllNineStagePosesUseInstalledFrameOnTranslatedRotatedVehicle()
        {
            using var f = new Fixture();
            f.Apply();
            MethodInfo apply = typeof(AssemblyFastenerInteractionTarget).GetMethod(
                "ApplyFastenerPresentation", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(apply, Is.Not.Null);
            foreach (Authoring.Binding binding in Authoring.GetBindings())
            {
                MountPointAuthoring mount = f.Mounts.Single(m => m.MountId == binding.MountId);
                var target = f.Target(binding);
                Transform visible = target.transform.GetChild(0);
                Assert.That(Vector3.Distance(target.transform.position, mount.Pose.TransformPoint(binding.Pose.position)), Is.LessThan(.00015f));
                Assert.That(Quaternion.Angle(target.transform.rotation, mount.Pose.rotation * binding.Pose.rotation), Is.LessThan(.05f));
                Assert.That(target.gameObject.layer, Is.EqualTo(8));
                Assert.That(visible.gameObject.layer, Is.EqualTo(8));
                Assert.That(target.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(visible.localScale, Is.EqualTo(binding.Scale));
                Assert.That(f.Originals[binding.FastenerId].gameObject.activeSelf, Is.False);
                Assert.That(target.GetComponent<InteractionTargetHost>().OutlineRenderers.Single(), Is.SameAs(visible.GetComponent<Renderer>()));
                for (int stage = 0; stage <= 8; stage++)
                {
                    apply.Invoke(target, new object[] { (float)stage });
                    Vector3 localTravel = Vector3.back * (.0025f * stage * binding.Scale.z);
                    Assert.That(Vector3.Distance(visible.localPosition, localTravel), Is.LessThan(.000001f));
                    Assert.That(Quaternion.Angle(visible.localRotation, Quaternion.AngleAxis(45f * stage, Vector3.forward)), Is.LessThan(.05f));
                    Vector3 originalPartPosition = binding.Pose.position + binding.Pose.rotation * localTravel;
                    Assert.That(Vector3.Distance(visible.position, mount.Pose.TransformPoint(originalPartPosition)), Is.LessThan(.00015f));
                }
            }
        }

        [Test]
        public void InstalledVisibilityAndTwoZeroHysteresisUseExistingRuntime()
        {
            using var f = new Fixture();
            f.Apply(); f.InitializeRuntime();
            Authoring.Binding binding = Authoring.GetBindings()[0];
            var target = f.Target(binding);
            MountPointAuthoring mount = f.Mounts.Single(m => m.MountId == binding.MountId);
            PartInstance part = f.Parts.Single(p => p.Definition.DefinitionId == binding.PartDefinitionId);
            target.RefreshAvailability();
            Assert.That(target.GetComponent<Collider>().enabled, Is.False);
            Assert.That(target.transform.GetChild(0).GetComponent<Renderer>().enabled, Is.False);
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            Assert.That(f.Assembly.TryInstall(part, mount).Succeeded, Is.True);
            Assert.That(f.Assembly.Graph.TryGetMount(mount.MountId, out MountPointRuntime runtime), Is.True);
            Assert.That(runtime.FastenerGroup.IsBolted, Is.False);
            target.RefreshAvailability();
            Assert.That(target.GetComponent<Collider>().enabled, Is.True);
            Assert.That(target.transform.GetChild(0).GetComponent<Renderer>().enabled, Is.True);
            Assert.That(f.Assembly.TryOperateFastener(mount.MountId, binding.FastenerId, f.Tool, true).Succeeded, Is.True);
            Assert.That(runtime.FastenerGroup.IsBolted, Is.False);
            Assert.That(f.Assembly.TryOperateFastener(mount.MountId, binding.FastenerId, f.Tool, true).Succeeded, Is.True);
            Assert.That(runtime.FastenerGroup.IsBolted, Is.True);
            Assert.That(f.Assembly.TryRemove(part).Succeeded, Is.False);
            Assert.That(f.Assembly.TryOperateFastener(mount.MountId, binding.FastenerId, f.Tool, false).Succeeded, Is.True);
            Assert.That(runtime.FastenerGroup.IsBolted, Is.True); // Historical ON remains at aggregate1.
            Assert.That(f.Assembly.TryOperateFastener(mount.MountId, binding.FastenerId, f.Tool, false).Succeeded, Is.True);
            Assert.That(runtime.FastenerGroup.IsBolted, Is.False);
            Assert.That(f.Assembly.TryRemove(part).Succeeded, Is.True);
            target.RefreshAvailability();
            Assert.That(target.GetComponent<Collider>().enabled, Is.False);
            Assert.That(target.transform.GetChild(0).GetComponent<Renderer>().enabled, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PersistentDefinitionsSurviveReimportAndLastSourceFailurePrecedesAnyWrite(bool invalidSource)
        {
            string folder = "Assets/__SatsumaHeadlightTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                using var f = new Fixture();
                f.PersistSources(folder);
                if (invalidSource)
                {
                    f.Originals[Authoring.GetBindings().Last().FastenerId].transform.localPosition += Vector3.up;
                    Assert.Throws<InvalidDataException>(() => Authoring.ApplyToInstance(f.Assembly, folder));
                    Assert.That(Directory.GetFiles(folder + "/FastenerDefinitions", "*.asset"), Is.Empty);
                    Assert.That(f.Mounts.All(m => m.Definition.Fasteners.Length == 0), Is.True);
                    return;
                }
                // Direct typed binding must also refuse persistent->transient links.
                Assert.Throws<InvalidDataException>(() => f.Apply());
                Assert.That(Authoring.ApplyToInstance(f.Assembly, folder), Is.EqualTo(6));
                foreach (var cohort in Authoring.GetBindings().GroupBy(b => b.MountId))
                {
                    string path = folder + "/MountDefinitions/" + cohort.Key + ".asset";
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    var reloaded = AssetDatabase.LoadAssetAtPath<MountPointDefinition>(path);
                    Assert.That(reloaded.Fasteners.All(d => d != null && EditorUtility.IsPersistent(d)), Is.True);
                    Assert.That(reloaded.Fasteners.Select(d => d.DefinitionId), Is.EqualTo(cohort.Select(b => b.FastenerId)));
                    Assert.That(File.ReadAllText(path), Does.Not.Contain("  - {fileID: 0}"));
                }
                Assert.That(Authoring.ApplyToInstance(f.Assembly, folder), Is.Zero);
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [TestCase("last-pose")]
        [TestCase("missing-source")]
        [TestCase("foreign-owner")]
        [TestCase("partial-definition")]
        [TestCase("wrong-tool")]
        public void UnknownBindingRejectsWholePacketBeforeFirstMutation(string corruption)
        {
            using var f = new Fixture();
            Authoring.Binding last = Authoring.GetBindings().Last();
            if (corruption == "last-pose") f.Originals[last.FastenerId].transform.localPosition += Vector3.up * .01f;
            if (corruption == "missing-source") Object.DestroyImmediate(f.Originals[last.FastenerId]);
            if (corruption == "foreign-owner")
                f.Originals[last.FastenerId].transform.SetParent(f.Parts[0].transform, false);
            if (corruption == "partial-definition")
            {
                var data = new SerializedObject(f.Mounts.Last().Definition);
                data.FindProperty("fasteners").arraySize = 1;
                data.FindProperty("fasteners").GetArrayElementAtIndex(0).objectReferenceValue = f.Definitions[last.FastenerId];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            if (corruption == "wrong-tool")
                f.Definitions[last.FastenerId].Configure(last.FastenerId, "bad", FastenerSize.Millimeter8, 8,
                    FastenerDirection.ClockwiseToTighten, true, true, ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter8));
            Assert.Throws<InvalidDataException>(() => f.Apply());
            Assert.That(f.Mounts[0].Definition.Fasteners, Is.Empty);
            Assert.That(f.Root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true), Is.Empty);
            Assert.That(f.Originals[Authoring.GetBindings()[0].FastenerId].gameObject.activeSelf, Is.True);
        }

        [Test]
        public void OnlyWholeOldOrNewCohortIsAcceptedAndDriftDoesNotDuplicateTargets()
        {
            using var f = new Fixture(); f.Apply();
            var binding = Authoring.GetBindings().Last();
            f.Target(binding).transform.localPosition += Vector3.up * .01f;
            Assert.Throws<InvalidDataException>(() => f.Apply());
            Assert.That(f.Root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Length, Is.EqualTo(4));
            foreach (var target in f.Mounts.Last().GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true))
                Object.DestroyImmediate(target.gameObject);
            var data = new SerializedObject(f.Mounts.Last().Definition);
            data.FindProperty("fasteners").arraySize = 0; data.ApplyModifiedPropertiesWithoutUndo();
            f.Mounts.Last().Definition.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(Array.Empty<FastenerDefinition>()));
            Assert.Throws<InvalidDataException>(() => f.Apply());
            Assert.That(f.Mounts.Last().Definition.Fasteners, Is.Empty);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("headlight four-bolt fixture");
            public readonly VehicleAssemblyController Assembly;
            public readonly List<MountPointAuthoring> Mounts = new List<MountPointAuthoring>();
            public readonly List<PartInstance> Parts = new List<PartInstance>();
            public readonly ToolDefinition Tool;
            public readonly Dictionary<string, FastenerDefinition> Definitions = new Dictionary<string, FastenerDefinition>();
            public readonly Dictionary<string, MeshFilter> Originals = new Dictionary<string, MeshFilter>();
            private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
            private readonly Mesh mesh = new Mesh { name = "reviewed short bolt" };
            private readonly Material material = new Material(Shader.Find("Hidden/InternalErrorShader"));

            public Fixture()
            {
                Root.SetActive(false);
                Root.transform.SetPositionAndRotation(new Vector3(153.5f, 3.2f, -1026.1f), Quaternion.Euler(11, 73, -9));
                Tool = Asset<ToolDefinition>(); Tool.Configure("test.wrench7", "Wrench", "Wrench", FastenerSize.Millimeter7);
                Part("test.body", true);
                foreach (var cohort in Authoring.GetBindings().GroupBy(b => b.MountId))
                {
                    PartInstance part = Part(cohort.First().PartDefinitionId, false);
                    part.transform.SetLocalPositionAndRotation(new Vector3(Mounts.Count * 2, 2, 1), Quaternion.Euler(31, 56, 73));
                    var definition = Asset<MountPointDefinition>();
                    definition.Configure(cohort.Key, cohort.Key, "test.socket", "test.body", new[] { cohort.First().PartDefinitionId },
                        new MountConstraint(.2f, 180f, 1f, 0f), .01f, Array.Empty<FastenerDefinition>());
                    definition.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(Array.Empty<FastenerDefinition>()));
                    var go = new GameObject(cohort.Key); go.transform.SetParent(Root.transform, false);
                    go.transform.SetLocalPositionAndRotation(new Vector3(Mounts.Count * .15f, .2f, -.5f), Quaternion.Euler(88, Mounts.Count * 13, 17));
                    var mount = go.AddComponent<MountPointAuthoring>(); mount.Configure(definition, cohort.Key, go.transform, 0); Mounts.Add(mount);
                    foreach (Authoring.Binding binding in cohort)
                    {
                        FastenerDefinition fastener = Authoring.CreateDefinition(binding); assets.Add(fastener); Definitions.Add(binding.FastenerId, fastener);
                        var original = new GameObject("explicit source " + binding.MarkerId);
                        original.transform.SetParent(part.transform, false);
                        original.transform.SetLocalPositionAndRotation(binding.Pose.position, binding.Pose.rotation);
                        original.transform.localScale = binding.Scale;
                        var filter = original.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
                        original.AddComponent<MeshRenderer>().sharedMaterial = material;
                        Originals.Add(binding.FastenerId, filter);
                    }
                }
                Assembly = Root.AddComponent<VehicleAssemblyController>();
                var data = new SerializedObject(Assembly);
                Assign(data, "parts", Parts.Cast<Object>().ToArray());
                Assign(data, "mountPoints", Mounts.Cast<Object>().ToArray());
                Assign(data, "tools", new Object[] { Tool });
                data.FindProperty("loosePartsRoot").objectReferenceValue = Root.transform;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            public int Apply() => Authoring.Configure(Assembly, mesh, material, 8, Definitions);
            public void InitializeRuntime() => Assembly.Configure(Parts.ToArray(), Mounts.ToArray(), Array.Empty<AssemblyDependency>(), new[] { Tool }, Root.transform);
            public AssemblyFastenerInteractionTarget Target(Authoring.Binding binding) => Root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Single(t => t.FastenerDefinitionId == binding.FastenerId);
            public void PersistSources(string folder)
            {
                foreach (string child in new[] { "Meshes", "Materials", "MountDefinitions", "FastenerDefinitions" }) AssetDatabase.CreateFolder(folder, child);
                AssetDatabase.CreateAsset(mesh, folder + "/Meshes/" + Authoring.ShortBoltMeshGuid + ".asset");
                AssetDatabase.CreateAsset(material, folder + "/Materials/" + Authoring.MaterialGuid + ".mat");
                foreach (MountPointAuthoring mount in Mounts) AssetDatabase.CreateAsset(mount.Definition, folder + "/MountDefinitions/" + mount.MountId + ".asset");
            }
            private PartInstance Part(string id, bool root)
            {
                var go = new GameObject(id); go.transform.SetParent(Root.transform, false);
                var definition = Asset<PartDefinition>();
                definition.Configure(id, id, PartCategory.Body, 1, null, new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                var identity = go.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var part = go.AddComponent<PartInstance>(); part.Configure(definition, identity, go.AddComponent<Rigidbody>(), null, root, "");
                Parts.Add(part); return part;
            }
            private T Asset<T>() where T : ScriptableObject
            { T value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
            private static void Assign(SerializedObject data, string name, Object[] values)
            { var array = data.FindProperty(name); array.arraySize = values.Length;
                for (int index = 0; index < values.Length; index++) array.GetArrayElementAtIndex(index).objectReferenceValue = values[index]; }
            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                foreach (var asset in assets) if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
                if (mesh != null && !EditorUtility.IsPersistent(mesh)) Object.DestroyImmediate(mesh);
                if (material != null && !EditorUtility.IsPersistent(material)) Object.DestroyImmediate(material);
            }
        }
    }
}
