using System;
using System.Linq;
using System.Reflection;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Authoring = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaElectricalTerminalPoseAuthoring;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaElectricalTerminalPoseTests
    {
        [Test]
        public void DonorRestRotationSurvivesCloneAndReenableAtEveryStage()
        {
            using var f = new Fixture();
            Assert.That(Authoring.ApplyToInstance(f.System), Is.EqualTo(3));
            Assert.That(Authoring.ApplyToInstance(f.System), Is.Zero);
            f.Root.SetActive(true);
            foreach (int stage in Enumerable.Range(0, 9))
            {
                Assert.That(f.System.TryRestore(new SatsumaElectricalSaveDto
                {
                    installedConnectionIds = new[] { "BatteryHarness", "GroundBattery", "Starter" },
                    batteryPlusStage = stage, batteryMinusStage = stage, starterCableStage = stage
                }, out string failure), Is.True, failure);
                foreach (var target in f.Targets) InvokeLifecycle(target, "Update");
                GameObject clone = Object.Instantiate(f.Root);
                try
                {
                    foreach (var target in clone.GetComponentsInChildren<SatsumaElectricalTerminalFastenerInteractionTarget>(true))
                    {
                        var data = new SerializedObject(target);
                        var kind = (SatsumaElectricalFastener)data.FindProperty("fastener").enumValueIndex;
                        var b = Authoring.GetBindings().Single(v => v.Fastener == kind);
                        Quaternion expected = b.Pose.rotation * Quaternion.AngleAxis(-13f * stage, Vector3.forward);
                        Assert.That(Quaternion.Angle(target.transform.localRotation, expected), Is.LessThan(.05f));
                        Assert.That(Vector3.Distance(target.transform.position,
                            target.transform.parent.TransformPoint(b.Pose.position)), Is.LessThan(.00015f));
                        target.gameObject.SetActive(false); target.gameObject.SetActive(true);
                        InvokeLifecycle(target, "OnEnable");
                        Assert.That(Quaternion.Angle(target.transform.localRotation, expected), Is.LessThan(.05f),
                            "Re-enable must not capture an already turned pose as rest.");
                    }
                }
                finally { Object.DestroyImmediate(clone); }
            }
        }

        [Test]
        public void OldUnboundPrefabCapturesDonorRestOnceWithoutChangingElectricalState()
        {
            using var f = new Fixture();
            Assert.That(f.System.TryRestore(new SatsumaElectricalSaveDto
            { installedConnectionIds = new[] { "BatteryHarness", "GroundBattery", "Starter" } },
                out string failure), Is.True, failure);
            string before = JsonUtility.ToJson(f.System.CaptureSaveData());
            f.Root.SetActive(true);
            foreach (var target in f.Targets)
            {
                InvokeLifecycle(target, "OnEnable");
                var data = new SerializedObject(target);
                var kind = (SatsumaElectricalFastener)data.FindProperty("fastener").enumValueIndex;
                var b = Authoring.GetBindings().Single(v => v.Fastener == kind);
                Assert.That(data.FindProperty("hasAuthoredBaseRotation").boolValue, Is.True);
                Assert.That(Quaternion.Angle(target.transform.localRotation, b.Pose.rotation), Is.LessThan(.05f));
            }
            Assert.That(JsonUtility.ToJson(f.System.CaptureSaveData()), Is.EqualTo(before));
        }

        [Test]
        public void UnknownThirdTerminalPoseRejectsBeforeFirstBindingMutation()
        {
            using var f = new Fixture();
            f.Targets[2].transform.localPosition += Vector3.up * .01f;
            Assert.Throws<System.IO.InvalidDataException>(() => Authoring.ApplyToInstance(f.System));
            Assert.That(new SerializedObject(f.Targets[0]).FindProperty("hasAuthoredBaseRotation").boolValue, Is.False);
        }

        private static void InvokeLifecycle(SatsumaElectricalTerminalFastenerInteractionTarget target, string name)
        {
            // EditMode does not schedule ordinary MonoBehaviour callbacks.
            // Invoke managed presentation explicitly; SendMessage asserts on inactive owners.
            MethodInfo method = typeof(SatsumaElectricalTerminalFastenerInteractionTarget)
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("terminal source fixture");
            public readonly SatsumaElectricalSystem System;
            public readonly SatsumaElectricalTerminalFastenerInteractionTarget[] Targets =
                new SatsumaElectricalTerminalFastenerInteractionTarget[3];
            public Fixture()
            {
                Root.SetActive(false);
                Root.transform.SetPositionAndRotation(new Vector3(153f, 3f, -1026f), Quaternion.Euler(19, 81, -13));
                System = Root.AddComponent<SatsumaElectricalSystem>();
                var bindings = Authoring.GetBindings();
                var systemData = new SerializedObject(System);
                var rows = systemData.FindProperty("bindings"); rows.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    var b = bindings[i];
                    var parent = new GameObject("explicit terminal owner " + b.ParentId);
                    parent.transform.SetParent(Root.transform, false);
                    parent.transform.SetLocalPositionAndRotation(new Vector3(0, .2608087f, 0),
                        new Quaternion(1.15202326e-7f, .7071068f, .7071068f, -1.15202326e-7f));
                    var marker = new GameObject("marker " + b.MarkerId);
                    marker.transform.SetParent(parent.transform, false);
                    marker.transform.SetLocalPositionAndRotation(b.Pose.position, b.Pose.rotation);
                    marker.transform.localScale = Vector3.one * b.Scale;
                    var child = new GameObject("mesh " + b.RendererId);
                    child.transform.SetParent(marker.transform, false);
                    var renderer = child.AddComponent<MeshRenderer>();
                    marker.AddComponent<SphereCollider>().isTrigger = true;
                    Targets[i] = marker.AddComponent<SatsumaElectricalTerminalFastenerInteractionTarget>();
                    // Exact old prefab shape: authored marker pose but no cached
                    // serialized baseRotation, unlike a Configure-created object.
                    var data = new SerializedObject(Targets[i]);
                    data.FindProperty("electricalSystem").objectReferenceValue = System;
                    data.FindProperty("fastener").enumValueIndex = (int)b.Fastener;
                    data.FindProperty("fastenerPresentation").objectReferenceValue = marker.transform;
                    data.FindProperty("fastenerRenderer").objectReferenceValue = renderer;
                    data.FindProperty("hasAuthoredBaseRotation").boolValue = false;
                    data.FindProperty("baseRotation").quaternionValue = Quaternion.identity;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    var row = rows.GetArrayElementAtIndex(i);
                    row.FindPropertyRelative("rule").enumValueIndex = i == 0
                        ? (int)SatsumaElectricalPresentationRule.BatteryPositiveShoe
                        : i == 1 ? (int)SatsumaElectricalPresentationRule.BatteryNegativeShoe
                        : (int)SatsumaElectricalPresentationRule.Connection;
                    row.FindPropertyRelative("connection").enumValueIndex = (int)SatsumaElectricalConnection.Starter;
                    row.FindPropertyRelative("installedPresentation").objectReferenceValue = parent;
                }
                systemData.ApplyModifiedPropertiesWithoutUndo();
            }
            public void Dispose() => Object.DestroyImmediate(Root);
        }
    }
}
