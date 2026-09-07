using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Authoring = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaHoseClampAuthoring;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaHoseClampAuthoringTests
    {
        [Test]
        public void FourClampsUseScrewdriverWithoutChangingLegacyIdsSizesOrGroups()
        {
            using var f = new Fixture();
            string[] groups = f.Mounts.Select(m => JsonUtility.ToJson(m.Definition.FastenerGroup)).ToArray();
            Pose[] poses = f.Targets.Select(t => new Pose(t.transform.position, t.transform.rotation)).ToArray();
            Assert.That(f.Apply(), Is.GreaterThan(0));
            for (int i = 0; i < 4; i++)
            {
                var b = Authoring.GetBindings()[i];
                Assert.That(f.Fasteners[i].DefinitionId, Is.EqualTo(b.FastenerId));
                Assert.That((int)f.Fasteners[i].Size, Is.EqualTo(b.LegacySize));
                Assert.That(f.Fasteners[i].MaximumStage, Is.EqualTo(8));
                Assert.That(f.Fasteners[i].ToolRule.Matches(f.Screwdriver), Is.True);
                Assert.That(f.Fasteners[i].ToolRule.Matches(f.Wrench6), Is.False);
                Assert.That(f.Targets[i].FastenerPresentationStageTravelScale, Is.Zero);
                Assert.That(f.Visuals[i].GetComponent<MeshFilter>().sharedMesh, Is.SameAs(f.Screw));
                Assert.That(f.Visuals[i].localScale, Is.EqualTo(Vector3.one * .52f));
                Assert.That(Vector3.Distance(f.Targets[i].transform.position, poses[i].position), Is.LessThan(.000001f));
                Assert.That(Quaternion.Angle(f.Targets[i].transform.rotation, poses[i].rotation), Is.LessThan(.05f));
            }
            Assert.That(f.Mounts.Select(m => JsonUtility.ToJson(m.Definition.FastenerGroup)), Is.EqualTo(groups));
            Assert.That(f.Apply(), Is.Zero);
        }

        [Test]
        public void AllNineStagesRotateWithoutAxialTravel()
        {
            using var f = new Fixture();
            f.Apply();
            MethodInfo apply = typeof(AssemblyFastenerInteractionTarget).GetMethod("ApplyFastenerPresentation",
                BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (AssemblyFastenerInteractionTarget target in f.Targets)
                for (int stage = 0; stage <= 8; stage++)
                {
                    apply.Invoke(target, new object[] { (float)stage });
                    Transform visual = target.transform.GetChild(0);
                    Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(Quaternion.Angle(visual.localRotation,
                        Quaternion.AngleAxis(stage * 45f, Vector3.forward)), Is.LessThan(.05f));
                }
        }

        [TestCase("missing")]
        [TestCase("pose")]
        [TestCase("mesh")]
        public void UnknownFourthBindingRejectsBeforeEarlierWrites(string drift)
        {
            using var f = new Fixture();
            if (drift == "missing") Object.DestroyImmediate(f.Targets[3]);
            if (drift == "pose") f.Targets[3].transform.localPosition += Vector3.right * .01f;
            if (drift == "mesh") f.Visuals[3].GetComponent<MeshFilter>().sharedMesh = null;
            Assert.Throws<InvalidDataException>(() => f.Apply());
            Assert.That(f.Fasteners[0].ToolRule.ToolType, Is.EqualTo("Wrench"));
            Assert.That(f.Visuals[0].GetComponent<MeshFilter>().sharedMesh, Is.SameAs(f.Nut));
            Assert.That(f.Targets[0].FastenerPresentationStageTravelScale, Is.EqualTo(1f));
        }

        [Test]
        public void RegistryAndUnrelatedChildrenRemainUntouched()
        {
            using var f = new Fixture();
            var unrelated = new GameObject("unrelated accepted suspension");
            unrelated.transform.SetParent(f.Root.transform, false);
            unrelated.transform.localPosition = new Vector3(1f, 2f, 3f);
            var before = f.Assembly.Tools.ToArray();
            f.Apply();
            Assert.That(f.Assembly.Tools, Is.EqualTo(before));
            Assert.That(unrelated.transform.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(f.Root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Length, Is.EqualTo(4));
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("hose authoring fixture");
            public readonly VehicleAssemblyController Assembly;
            public readonly MountPointAuthoring[] Mounts = new MountPointAuthoring[2];
            public readonly FastenerDefinition[] Fasteners = new FastenerDefinition[4];
            public readonly AssemblyFastenerInteractionTarget[] Targets = new AssemblyFastenerInteractionTarget[4];
            public readonly Transform[] Visuals = new Transform[4];
            public readonly ToolDefinition Screwdriver = SatsumaAuxiliaryAssemblyTools.CreateScrewdriver();
            public readonly ToolDefinition Wrench6 = ScriptableObject.CreateInstance<ToolDefinition>();
            private readonly ToolDefinition wrench7 = ScriptableObject.CreateInstance<ToolDefinition>();
            public readonly Mesh Screw = new Mesh { name = "bolt5" };
            public readonly Mesh Nut = new Mesh { name = "bolt2" };
            private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
            public Fixture()
            {
                Root.SetActive(false);
                Root.transform.SetPositionAndRotation(new Vector3(153f, 3f, -1026f), Quaternion.Euler(11f, 72f, -9f));
                Wrench6.Configure("test.wrench6", "6", "Wrench", FastenerSize.Millimeter6);
                wrench7.Configure("test.wrench7", "7", "Wrench", FastenerSize.Millimeter7);
                var bindings = Authoring.GetBindings();
                for (int i = 0; i < 4; i++)
                {
                    var b = bindings[i];
                    Fasteners[i] = ScriptableObject.CreateInstance<FastenerDefinition>();
                    Fasteners[i].Configure(b.FastenerId, b.FastenerId, (FastenerSize)b.LegacySize, 8,
                        FastenerDirection.ClockwiseToTighten, true, true,
                        ToolCompatibilityRule.Create("Wrench", (FastenerSize)b.LegacySize));
                    definitions.Add(Fasteners[i]);
                }
                for (int i = 0; i < 2; i++)
                {
                    var d = ScriptableObject.CreateInstance<MountPointDefinition>();
                    string id = bindings[i * 2].MountId;
                    d.Configure(id, id, "test.socket", "", Array.Empty<string>(),
                        new MountConstraint(1f, 180f, 1f, 0f), 0f, Fasteners.Skip(i * 2).Take(2).ToArray());
                    definitions.Add(d);
                    var socket = new GameObject(id);
                    socket.transform.SetParent(Root.transform, false);
                    socket.transform.SetLocalPositionAndRotation(new Vector3(i * .2f, .1f, 1.3f), Quaternion.Euler(90f, 0f, 0f));
                    Mounts[i] = socket.AddComponent<MountPointAuthoring>();
                    Mounts[i].Configure(d, id, socket.transform, 0);
                }
                Assembly = Root.AddComponent<VehicleAssemblyController>();
                // Authoring must not initialize a live assembly graph.
                var serialized = new SerializedObject(Assembly);
                SerializedProperty mounts = serialized.FindProperty("mountPoints"); mounts.arraySize = 2;
                for (int i = 0; i < 2; i++) mounts.GetArrayElementAtIndex(i).objectReferenceValue = Mounts[i];
                SerializedProperty tools = serialized.FindProperty("tools"); tools.arraySize = 3;
                tools.GetArrayElementAtIndex(0).objectReferenceValue = Wrench6;
                tools.GetArrayElementAtIndex(1).objectReferenceValue = wrench7;
                tools.GetArrayElementAtIndex(2).objectReferenceValue = Screwdriver;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                for (int i = 0; i < 4; i++)
                {
                    var b = bindings[i];
                    var marker = new GameObject(b.FastenerId);
                    marker.transform.SetParent(Mounts[i / 2].transform, false);
                    marker.transform.SetLocalPositionAndRotation(b.Pose.position, b.Pose.rotation);
                    Visuals[i] = new GameObject("Visible inserted bolt or nut").transform;
                    Visuals[i].SetParent(marker.transform, false); Visuals[i].localScale = Vector3.one * .65f;
                    Visuals[i].gameObject.AddComponent<MeshFilter>().sharedMesh = Nut;
                    Targets[i] = marker.AddComponent<AssemblyFastenerInteractionTarget>();
                    Targets[i].Configure(Assembly, b.MountId, b.FastenerId,
                        b.LegacySize == 6 ? Wrench6 : wrench7, true, Visuals[i], 1f);
                }
            }
            public int Apply() => Authoring.Configure(Assembly, Screw, Nut, Screwdriver);
            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                foreach (var d in definitions) Object.DestroyImmediate(d);
                Object.DestroyImmediate(Screwdriver); Object.DestroyImmediate(Wrench6); Object.DestroyImmediate(wrench7);
                Object.DestroyImmediate(Screw); Object.DestroyImmediate(Nut);
            }
        }
    }
}
