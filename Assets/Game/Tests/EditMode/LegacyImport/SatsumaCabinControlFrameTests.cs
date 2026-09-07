using System.IO;
using System.Linq;
using System.Reflection;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaCabinControlFrameTests
    {
        [Test]
        public void GeneratedLegacyAndReviewedCabinBindingsAreIdempotentAndPreserveTargetIdentity()
        {
            GameObject contents = LoadContents();
            try
            {
                var assembly = contents.GetComponent<VehicleAssemblyController>();
                // Parts includes the body/root as well as nested part roots;
                // flattening every subtree counts the same target repeatedly.
                var dashboard = assembly.Parts.Single(value => value.Definition != null &&
                    value.Definition.DefinitionId == "vehicle.satsuma.part.dashboard");
                var release = dashboard.GetComponentsInChildren<AssemblyHoodReleaseInteractionTarget>(true).Single();
                var oldTarget = release;
                var housing = release.GetComponent<MeshFilter>();
                Pose housingPose = new(release.transform.localPosition, release.transform.localRotation);
                int first = Phase1SatsumaCabinControlFrameAuthoring.ApplyToInstance(assembly);
                Assert.That(first, Is.InRange(0, 2));
                Assert.That(Phase1SatsumaCabinControlFrameAuthoring.ApplyToInstance(assembly), Is.Zero);
                Assert.That(release, Is.SameAs(oldTarget));
                Assert.That(release.LeverVisual, Is.Not.SameAs(release.transform));
                Assert.That(AssetDatabase.GetAssetPath(release.LeverVisual.GetComponent<MeshFilter>().sharedMesh),
                    Does.EndWith(Phase1SatsumaCabinControlFrameAuthoring.HandleMeshGuid + ".asset"));
                Assert.That(housing.transform.localPosition, Is.EqualTo(housingPose.position));
                Assert.That(housing.transform.localRotation, Is.EqualTo(housingPose.rotation));
                SphereCollider shape = release.GetComponent<SphereCollider>();
                Vector3 expected = release.LeverVisual.parent.TransformPoint(
                    Phase1SatsumaCabinControlFrameAuthoring.TriggerPosition +
                    Phase1SatsumaCabinControlFrameAuthoring.TriggerRotation * Phase1SatsumaCabinControlFrameAuthoring.TriggerCenter);
                Assert.That(Vector3.Distance(shape.transform.TransformPoint(shape.center), expected), Is.LessThan(.00001f));

                // Explicitly recreate the two supported legacy properties; no
                // object/component replacement or arbitrary current pose reset.
                var data = new SerializedObject(release);
                data.FindProperty("useReviewedHandlePull").boolValue = false;
                data.FindProperty("leverVisual").objectReferenceValue = release.transform;
                data.ApplyModifiedPropertiesWithoutUndo();
                shape.center = housing.sharedMesh.bounds.center;
                var wiper = assembly.GetComponent<SatsumaWiperController>();
                data = new SerializedObject(wiper);
                data.FindProperty("hasSwitchKnobBaseLocalRotation").boolValue = false;
                data.ApplyModifiedPropertiesWithoutUndo();
                wiper.SwitchKnob.localRotation = Quaternion.identity;
                Assert.That(Phase1SatsumaCabinControlFrameAuthoring.ApplyToInstance(assembly), Is.EqualTo(2));
                Assert.That(Phase1SatsumaCabinControlFrameAuthoring.ApplyToInstance(assembly), Is.Zero);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void KnobRestBasisSurvivesModesDtoRestoreAndComponentSerialization(int mode)
        {
            var root = new GameObject("Wiper basis serialization");
            try
            {
                var knob = new GameObject("knob");
                knob.transform.SetParent(root.transform, false);
                var controller = root.AddComponent<SatsumaWiperController>();
                var data = new SerializedObject(controller);
                data.FindProperty("switchKnob").objectReferenceValue = knob.transform;
                data.ApplyModifiedPropertiesWithoutUndo();
                Quaternion rest = Phase1SatsumaCabinControlFrameAuthoring.WiperRestRotation;
                controller.ConfigureSwitchKnobRestRotation(rest);
                var dto = new SatsumaWiperSaveDto { mode = mode };
                Assert.That(controller.TryRestore(dto, out string failure), Is.True, failure);
                Quaternion expected = rest * Quaternion.AngleAxis(-45f * mode, Vector3.up);
                Assert.That(Mathf.Abs(Quaternion.Dot(knob.transform.localRotation, expected)), Is.GreaterThan(.999999f));
                // This is the controller-owned numeric serialization contract,
                // not a clone of Unity's native m_GameObject/reference envelope.
                // Scene/prefab reference preservation is exercised above.
                var persisted = JsonUtility.FromJson<KnobSerializedFields>(EditorJsonUtility.ToJson(controller));
                EditorJsonUtility.FromJsonOverwrite(JsonUtility.ToJson(persisted), controller);
                Assert.That(controller.SwitchKnob, Is.SameAs(knob.transform));
                typeof(SatsumaWiperController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
                controller.enabled = false;
                controller.enabled = true;
                Assert.That(controller.TryRestore(dto, out failure), Is.True, failure);
                Assert.That(Mathf.Abs(Quaternion.Dot(controller.SwitchKnobBaseLocalRotation, rest)), Is.GreaterThan(.999999f));
                Assert.That(Mathf.Abs(Quaternion.Dot(knob.transform.localRotation, expected)), Is.GreaterThan(.999999f));
                controller.ResetState();
                Assert.That(controller.Mode, Is.EqualTo(SatsumaWiperMode.Off));
                Assert.That(controller.SwitchKnob, Is.SameAs(knob.transform));
                Assert.That(Mathf.Abs(Quaternion.Dot(knob.transform.localRotation, rest)), Is.GreaterThan(.999999f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void HoodPullFollowsOriginalThreePositionKeysWithoutInventedRotation()
        {
            Assert.That(AssemblyHoodReleaseInteractionTarget.EvaluateReviewedPullOffset(0f), Is.Zero);
            Assert.That(AssemblyHoodReleaseInteractionTarget.EvaluateReviewedPullOffset(1f / 6f), Is.EqualTo(-.0086f).Within(.000001f));
            Assert.That(AssemblyHoodReleaseInteractionTarget.EvaluateReviewedPullOffset(.25f), Is.Zero);
            float duration = 1f / 6f;
            // Independent Hermite midpoint: slopes -.0516 and +.025800005.
            float midpoint = .5f * -.0086f + .125f * duration * (-.0516f - .025800005f);
            Assert.That(AssemblyHoodReleaseInteractionTarget.EvaluateReviewedPullOffset(duration * .5f), Is.EqualTo(midpoint).Within(.000001f));
        }

        [System.Serializable]
        private sealed class KnobSerializedFields
        {
            public int mode = 0;
            public Quaternion switchKnobBaseLocalRotation = Quaternion.identity;
            public bool hasSwitchKnobBaseLocalRotation = false;
        }

        private static GameObject LoadContents()
        {
            if (!File.Exists(Phase1SatsumaBaselineBuilder.RuntimePrefabPath)) Assert.Ignore("Private baseline absent.");
            return PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
        }
    }
}
