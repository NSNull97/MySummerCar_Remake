using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MSC.Core.Identity;
using MSC.Interaction.Query;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Authoring = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaStockMountFastenerAuthoring;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaStockMountFastenerAuthoringTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void PersistentMountsReceiveSavedDefinitionsBeforeBindingAndPreflightStillRejectsFirst(bool invalidSource)
        {
            string folder = "Assets/__SatsumaStockPersistenceTests_" + Guid.NewGuid().ToString("N");
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
                    Assert.That(f.Mounts[0].Definition.Fasteners, Is.Empty);
                    return;
                }
                Assert.That(Authoring.ApplyToInstance(f.Assembly, folder), Is.EqualTo(28));
                foreach (var group in Authoring.GetBindings().GroupBy(binding => binding.MountId))
                {
                    string path = folder + "/MountDefinitions/" + group.Key + ".asset";
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    MountPointDefinition reloaded = AssetDatabase.LoadAssetAtPath<MountPointDefinition>(path);
                    Assert.That(reloaded.Fasteners.All(value => value != null && EditorUtility.IsPersistent(value)), Is.True, group.Key);
                    Assert.That(reloaded.Fasteners.Select(value => value.DefinitionId), Is.EqualTo(group.Select(binding => binding.FastenerId)));
                    Assert.That(File.ReadAllText(path), Does.Not.Contain("  - {fileID: 0}"));
                }
                Assert.That(Authoring.ApplyToInstance(f.Assembly, folder), Is.Zero);
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void ExactTwentyOneStockBoltsHaveDonorSizesThresholdsAndNoFuelLineNut()
        {
            using var f = new Fixture();
            string[] partIds = f.Assembly.Parts.Select(p => p.Definition.DefinitionId).ToArray();
            Assert.That(f.Apply(), Is.EqualTo(28)); // six groups +21 targets +filler removal rule
            var bindings = Authoring.GetBindings();
            Assert.That(bindings.Length, Is.EqualTo(21));
            Assert.That(bindings.Select(b => b.FastenerId).Distinct().Count(), Is.EqualTo(21));
            Assert.That(f.Assembly.Parts.Select(p => p.Definition.DefinitionId), Is.EqualTo(partIds));
            foreach (var group in bindings.GroupBy(b => b.MountId))
            {
                MountPointDefinition d = f.Mounts.Single(m => m.MountId == group.Key).Definition;
                Assert.That(d.Fasteners.Select(b => b.DefinitionId), Is.EqualTo(group.Select(b => b.FastenerId)));
                Assert.That(d.FastenerGroup.AggregateMaximumTightness, Is.EqualTo(group.Count() * 8));
                Assert.That(d.FastenerGroup.BoltedOnThreshold, Is.EqualTo(group.First().OnThreshold));
                Assert.That(d.FastenerGroup.BoltedOffThreshold, Is.Zero);
                foreach (FastenerDefinition b in d.Fasteners)
                {
                    Assert.That(b.MaximumStage, Is.EqualTo(8));
                    Assert.That(b.InsertedOnInstall && b.RequiredForRemoval, Is.True);
                    Assert.That((int)b.Size, Is.EqualTo(group.First().Size));
                    Assert.That(b.ToolRule.ToolType, Is.EqualTo("Wrench"));
                }
            }
            Assert.That(bindings.Any(b => b.Size == 12 || b.MarkerId == 45288), Is.False);
            Assert.That(f.Apply(), Is.Zero);
        }

        [Test]
        public void FinalWorldPoseUsesInstalledPartFrameOnTranslatedRotatedVehicle()
        {
            using var f = new Fixture();
            f.Apply();
            foreach (var b in Authoring.GetBindings())
            {
                MountPointAuthoring mount = f.Mounts.Single(m => m.MountId == b.MountId);
                var target = f.Target(b);
                // Compare world result with the original PART-relative coordinate,
                // not just against identity/local values. Loose source part is elsewhere.
                Vector3 expected = mount.Pose.TransformPoint(b.Pose.position);
                Assert.That(Vector3.Distance(target.transform.position, expected), Is.LessThan(.00015f), b.FastenerId);
                Assert.That(Quaternion.Angle(target.transform.rotation, mount.Pose.rotation * b.Pose.rotation), Is.LessThan(.05f));
                Assert.That(target.transform.parent, Is.SameAs(mount.Pose));
                Assert.That(target.transform.GetChild(0).localScale, Is.EqualTo(b.Scale));
                Assert.That(f.Originals[b.FastenerId].gameObject.activeSelf, Is.False);
                Assert.That(target.GetComponent<InteractionTargetHost>().OutlineRenderers[0],
                    Is.SameAs(target.transform.GetChild(0).GetComponent<MeshRenderer>()));
            }
            var passenger = Authoring.GetBindings().First(b => b.Slug == "seat-passenger");
            Assert.That(passenger.Pose.position.x, Is.EqualTo(.2238f + .01f).Within(.000001f));
        }

        [Test]
        public void AllStagesPreserveOriginalAxisTravelAndRotation()
        {
            using var f = new Fixture();
            f.Apply();
            MethodInfo apply = typeof(AssemblyFastenerInteractionTarget).GetMethod(
                "ApplyFastenerPresentation", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var b in Authoring.GetBindings())
            {
                var target = f.Target(b);
                Transform visual = target.transform.GetChild(0);
                for (int stage = 0; stage <= 8; stage++)
                {
                    apply.Invoke(target, new object[] { (float)stage });
                    Assert.That(Vector3.Distance(visual.localPosition, Vector3.back * (.0025f * stage * b.Scale.z)),
                        Is.LessThan(.000001f));
                    Assert.That(Quaternion.Angle(visual.localRotation, Quaternion.AngleAxis(stage * 45f, Vector3.forward)),
                        Is.LessThan(.05f));
                    Assert.That(Vector3.Distance(visual.position, target.transform.TransformPoint(
                        Vector3.back * (.0025f * stage * b.Scale.z))), Is.LessThan(.00015f));
                }
            }
        }

        [Test]
        public void InstalledBoltsAreVisibleAndLatchBlocksRemovalUntilZero()
        {
            using var f = new Fixture();
            f.Apply(); f.InitializeRuntime();
            var b = Authoring.GetBindings().First(v => v.Slug == "seat-rear");
            var target = f.Target(b);
            var mount = f.Mounts.Single(m => m.MountId == b.MountId);
            var part = f.Parts.Single(p => p.Definition.DefinitionId == b.PartDefinitionId);
            target.RefreshAvailability();
            Assert.That(target.GetComponent<Collider>().enabled, Is.False);
            Assert.That(target.transform.GetChild(0).GetComponent<Renderer>().enabled, Is.False);
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            Assert.That(f.Assembly.TryInstall(part, mount).Succeeded, Is.True);
            target.RefreshAvailability();
            Assert.That(target.GetComponent<Collider>().enabled, Is.True);
            Assert.That(target.transform.GetChild(0).GetComponent<Renderer>().enabled, Is.True);
            ToolDefinition tool = f.Tools.Single(t => (int)t.Size == b.Size);
            for (int i = 0; i < b.OnThreshold; i++)
                Assert.That(f.Assembly.TryOperateFastener(b.MountId, b.FastenerId, tool, true).Succeeded, Is.True);
            Assert.That(f.Assembly.TryRemove(part).Succeeded, Is.False);
            for (int i = 0; i < b.OnThreshold; i++)
                Assert.That(f.Assembly.TryOperateFastener(b.MountId, b.FastenerId, tool, false).Succeeded, Is.True);
            Assert.That(f.Assembly.TryRemove(part).Succeeded, Is.True);
            target.RefreshAvailability();
            Assert.That(target.GetComponent<Collider>().enabled, Is.False);
            Assert.That(target.transform.GetChild(0).GetComponent<Renderer>().enabled, Is.False);
            Assert.That(f.Originals[b.FastenerId].gameObject.activeSelf, Is.False);
        }

        [Test]
        public void FuelTankRemovalRuleIsAdditiveAndOtherSequenceRulesArePreserved()
        {
            using var f = new Fixture();
            MountPointDefinition tank = f.Mounts.Single(m => m.MountId == "mount.satsuma.fuel-tank").Definition;
            tank.ConfigureRemovalBlockers(new[] { "test.existing.blocker" });
            tank.ConfigureInstallationOccupancy(new[] { "test.existing.prerequisite" });
            f.Apply();
            Assert.That(tank.RemovalBlockedWhileOccupiedMountIds,
                Is.EqualTo(new[] { "test.existing.blocker", Authoring.FuelTankPipeMountId }));
            Assert.That(tank.InstallationRequiredOccupiedMountIds, Is.EqualTo(new[] { "test.existing.prerequisite" }));
            Assert.That(f.Apply(), Is.Zero);
        }

        [TestCase("source-pose")]
        [TestCase("source-missing")]
        [TestCase("partial-definitions")]
        [TestCase("definition-rule")]
        public void UnknownLastBindingRejectsBeforeFirstMountMutation(string problem)
        {
            using var f = new Fixture();
            var last = Authoring.GetBindings().Last();
            if (problem == "source-pose") f.Originals[last.FastenerId].transform.localPosition += Vector3.right * .01f;
            if (problem == "source-missing") Object.DestroyImmediate(f.Originals[last.FastenerId]);
            if (problem == "partial-definitions")
            {
                var d = f.Mounts.Single(m => m.MountId == last.MountId).Definition;
                var so = new SerializedObject(d);
                so.FindProperty("fasteners").arraySize = 1;
                so.FindProperty("fasteners").GetArrayElementAtIndex(0).objectReferenceValue = f.Definitions[last.FastenerId];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (problem == "definition-rule")
                f.Definitions[last.FastenerId].Configure(last.FastenerId, "bad", FastenerSize.Millimeter12, 8,
                    FastenerDirection.ClockwiseToTighten, true, true, ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter12));
            Assert.Throws<InvalidDataException>(() => f.Apply());
            Assert.That(f.Mounts[0].Definition.Fasteners, Is.Empty);
            Assert.That(f.Root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true), Is.Empty);
            Assert.That(f.Originals[Authoring.GetBindings()[0].FastenerId].gameObject.activeSelf, Is.True);
        }

        [Test]
        public void DriftedAuthoredTargetRejectsWithoutAddingDuplicateTargets()
        {
            using var f = new Fixture(); f.Apply();
            var first = Authoring.GetBindings()[0];
            f.Target(first).transform.localPosition += Vector3.up * .01f;
            Assert.Throws<InvalidDataException>(() => f.Apply());
            Assert.That(f.Root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Length, Is.EqualTo(21));
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("stock fastener fixture");
            public readonly VehicleAssemblyController Assembly;
            public readonly List<MountPointAuthoring> Mounts = new List<MountPointAuthoring>();
            public readonly List<PartInstance> Parts = new List<PartInstance>();
            public readonly List<ToolDefinition> Tools = new List<ToolDefinition>();
            public readonly Dictionary<string, FastenerDefinition> Definitions = new Dictionary<string, FastenerDefinition>();
            public readonly Dictionary<string, MeshFilter> Originals = new Dictionary<string, MeshFilter>();
            private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
            private readonly Mesh mesh = new Mesh { name = "reviewed short bolt" };
            private readonly Material material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            public Fixture()
            {
                Root.SetActive(false);
                Root.transform.SetPositionAndRotation(new Vector3(153.5f, 3.2f, -1026.1f), Quaternion.Euler(11, 73, -9));
                foreach (int size in new[] { 7, 9, 11 })
                {
                    var t = Asset<ToolDefinition>(); t.Configure("test.wrench" + size, "Wrench", "Wrench", (FastenerSize)size);
                    Tools.Add(t);
                }
                Part("test.body", true);
                foreach (var group in Authoring.GetBindings().GroupBy(b => b.Slug))
                {
                    var first = group.First();
                    PartInstance part = Part(first.PartDefinitionId, false);
                    part.transform.SetLocalPositionAndRotation(new Vector3(Mounts.Count * 2, 2, 1), Quaternion.Euler(31, 56, 73));
                    MountPointAuthoring mount = Mount(first.MountId, first.PartDefinitionId);
                    foreach (var b in group)
                    {
                        FastenerDefinition d = Authoring.CreateDefinition(b); assets.Add(d); Definitions.Add(b.FastenerId, d);
                        var original = new GameObject("explicit reviewed source " + b.MarkerId);
                        original.transform.SetParent(part.transform, false);
                        original.transform.SetLocalPositionAndRotation(b.Pose.position, b.Pose.rotation);
                        original.transform.localScale = b.Scale;
                        var filter = original.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
                        original.AddComponent<MeshRenderer>().sharedMaterial = material;
                        Originals.Add(b.FastenerId, filter);
                    }
                }
                Part("vehicle.satsuma.part.fuel-tank-pipe", false);
                Mount(Authoring.FuelTankPipeMountId, "vehicle.satsuma.part.fuel-tank-pipe");
                Assembly = Root.AddComponent<VehicleAssemblyController>();
                var so = new SerializedObject(Assembly);
                Assign(so, "parts", Parts.Cast<Object>().ToArray());
                Assign(so, "mountPoints", Mounts.Cast<Object>().ToArray());
                Assign(so, "tools", Tools.Cast<Object>().ToArray());
                so.FindProperty("loosePartsRoot").objectReferenceValue = Root.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            public int Apply() => Authoring.Configure(Assembly, mesh, material, 0, Definitions);
            public void PersistSources(string folder)
            {
                foreach (string name in new[] { "Meshes", "Materials", "MountDefinitions", "FastenerDefinitions" })
                    AssetDatabase.CreateFolder(folder, name);
                AssetDatabase.CreateAsset(mesh, folder + "/Meshes/" + Authoring.ShortBoltMeshGuid + ".asset");
                AssetDatabase.CreateAsset(material, folder + "/Materials/98697bae08a8c114ba9774c487f2658d.mat");
                foreach (MountPointAuthoring mount in Mounts)
                    AssetDatabase.CreateAsset(mount.Definition, folder + "/MountDefinitions/" + mount.MountId + ".asset");
            }
            public AssemblyFastenerInteractionTarget Target(Authoring.Binding b) =>
                Root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Single(t => t.FastenerDefinitionId == b.FastenerId);
            public void InitializeRuntime() => Assembly.Configure(Parts.ToArray(), Mounts.ToArray(),
                Array.Empty<AssemblyDependency>(), Tools.ToArray(), Root.transform);
            private PartInstance Part(string id, bool isRoot)
            {
                var go = new GameObject(id); go.transform.SetParent(Root.transform, false);
                var d = Asset<PartDefinition>();
                d.Configure(id, id, PartCategory.Body, 1, null, new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                var identity = go.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var part = go.AddComponent<PartInstance>();
                part.Configure(d, identity, go.AddComponent<Rigidbody>(), null, isRoot, "");
                Parts.Add(part); return part;
            }
            private MountPointAuthoring Mount(string id, string partId)
            {
                var d = Asset<MountPointDefinition>();
                d.Configure(id, id, "test.socket", "test.body", new[] { partId },
                    new MountConstraint(.2f, 180f, 1f, 0f), .01f, Array.Empty<FastenerDefinition>());
                var group = new FastenerGroupDefinition(); group.Configure(Array.Empty<string>(), 0, 0, 0);
                d.ConfigureFastenerGroup(group);
                var go = new GameObject(id); go.transform.SetParent(Root.transform, false);
                go.transform.SetLocalPositionAndRotation(new Vector3(Mounts.Count * .15f, .2f, -.5f),
                    Quaternion.Euler(88, Mounts.Count * 13, 17));
                var mount = go.AddComponent<MountPointAuthoring>(); mount.Configure(d, id, go.transform, 0);
                Mounts.Add(mount); return mount;
            }
            private T Asset<T>() where T : ScriptableObject
            { T value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
            private static void Assign(SerializedObject so, string name, Object[] values)
            { var a = so.FindProperty(name); a.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
            public void Dispose()
            { Object.DestroyImmediate(Root);
                foreach (var a in assets) if (a != null && !EditorUtility.IsPersistent(a)) Object.DestroyImmediate(a);
                if (mesh != null && !EditorUtility.IsPersistent(mesh)) Object.DestroyImmediate(mesh);
                if (material != null && !EditorUtility.IsPersistent(material)) Object.DestroyImmediate(material); }
        }
    }
}
