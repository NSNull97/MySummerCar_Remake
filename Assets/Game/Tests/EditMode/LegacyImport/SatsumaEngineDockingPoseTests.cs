using System;
using System.IO;
using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaEngineDockingPoseTests
    {
        [Test]
        public void ReviewedChassisPosesMatchOriginalMotorSubframeTransformChain()
        {
            Pose[] reviewed = Phase1SatsumaEngineDockingAuthoring.GetReviewedBoltChassisPoses();
            for (int index = 0; index < 3; index++)
            {
                Pose original = OriginalChassisPose(index);
                Assert.That(Vector3.Distance(reviewed[index].position, original.position), Is.LessThan(0.000002f));
                Assert.That(Quaternion.Angle(reviewed[index].rotation, original.rotation), Is.LessThan(0.05f));
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void OldAndCorrectedFramesPlaceActualBoltsAtDonorWorldPointsAndRepeatWithoutChanges(
            bool legacyFrame, bool translatedAndRotated)
        {
            GameObject instance = CreateInstance();
            try
            {
                var assembly = instance.GetComponent<VehicleAssemblyController>();
                Phase1SatsumaEngineDockingAuthoring.ApplyToInstance(assembly);
                AssemblyFastenerInteractionTarget[] targets = Targets(assembly);
                if (legacyFrame) PutInKnownLegacyFrame(targets);
                if (translatedAndRotated)
                    assembly.transform.SetPositionAndRotation(new Vector3(7.25f, -2.5f, 13f),
                        Quaternion.Euler(23f, 147f, -31f));
                MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                    value.MountId == Phase1SatsumaEngineDockingAuthoring.MountId);
                Pose mountPose = new Pose(mount.transform.position, mount.transform.rotation);
                int mutationCount = assembly.GraphMutationCount;
                var unrelated = assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Except(targets).ToDictionary(value => value, value =>
                        new Pose(value.transform.position, value.transform.rotation));

                Assert.That(Phase1SatsumaEngineDockingAuthoring.ApplyToInstance(assembly),
                    Is.EqualTo(legacyFrame ? 3 : 0));

                for (int index = 0; index < 3; index++)
                {
                    Pose original = OriginalChassisPose(index);
                    Vector3 expectedPosition = assembly.transform.TransformPoint(original.position);
                    Quaternion expectedRotation = assembly.transform.rotation * original.rotation;
                    Transform marker = targets[index].transform;
                    var data = new SerializedObject(targets[index]);
                    Transform visible = data.FindProperty("fastenerPresentation").objectReferenceValue as Transform;
                    Assert.That(Vector3.Distance(marker.position, expectedPosition), Is.LessThan(0.00002f));
                    Assert.That(Vector3.Distance(visible.position, expectedPosition), Is.LessThan(0.00002f));
                    Assert.That(Quaternion.Angle(marker.rotation, expectedRotation), Is.LessThan(0.05f));
                    Assert.That(Vector3.Distance(marker.forward, expectedRotation * Vector3.forward), Is.LessThan(0.00002f));
                    Assert.That(visible.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(visible.localRotation, Is.EqualTo(Quaternion.identity));
                    Assert.That(Vector3.Distance(visible.localScale, Vector3.one * 1.1f), Is.LessThan(0.00001f));
                    Assert.That(targets[index].FastenerPresentationStageTravelScale, Is.EqualTo(1.1f).Within(0.00001f));
                    Assert.That(data.FindProperty("fastenerPresentationBaseLocalPosition").vector3Value,
                        Is.EqualTo(Vector3.zero));
                }
                Assert.That(Phase1SatsumaEngineDockingAuthoring.ApplyToInstance(assembly), Is.Zero);
                Assert.That(assembly.GraphMutationCount, Is.EqualTo(mutationCount));
                Assert.That(mount.transform.position, Is.EqualTo(mountPose.position));
                Assert.That(mount.transform.rotation, Is.EqualTo(mountPose.rotation));
                foreach (var entry in unrelated)
                {
                    Assert.That(entry.Key.transform.position, Is.EqualTo(entry.Value.position));
                    Assert.That(entry.Key.transform.rotation, Is.EqualTo(entry.Value.rotation));
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void UnknownThirdMarkerPoseRejectsBeforeChangingEitherKnownLegacyMarker()
        {
            GameObject instance = CreateInstance();
            try
            {
                var assembly = instance.GetComponent<VehicleAssemblyController>();
                Phase1SatsumaEngineDockingAuthoring.ApplyToInstance(assembly);
                AssemblyFastenerInteractionTarget[] targets = Targets(assembly);
                PutInKnownLegacyFrame(targets);
                targets[2].transform.localPosition += new Vector3(0.1f, 0.2f, 0.3f);
                Pose[] before = targets.Select(value =>
                    new Pose(value.transform.localPosition, value.transform.localRotation)).ToArray();

                Assert.Throws<InvalidDataException>(() => Phase1SatsumaEngineDockingAuthoring.ApplyToInstance(assembly));

                for (int index = 0; index < 3; index++)
                {
                    Assert.That(targets[index].transform.localPosition, Is.EqualTo(before[index].position));
                    Assert.That(targets[index].transform.localRotation, Is.EqualTo(before[index].rotation));
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static GameObject CreateInstance()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null) Assert.Ignore("Private Satsuma baseline has not been generated.");
            return Object.Instantiate(prefab);
        }

        private static AssemblyFastenerInteractionTarget[] Targets(VehicleAssemblyController assembly) =>
            Phase1SatsumaEngineDockingAuthoring.FastenerIds.Select(id =>
                assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Single(value => value.Controller == assembly && value.FastenerDefinitionId == id)).ToArray();

        private static void PutInKnownLegacyFrame(AssemblyFastenerInteractionTarget[] targets)
        {
            Pose[] poses = Phase1SatsumaEngineDockingAuthoring.GetReviewedBoltChassisPoses();
            for (int index = 0; index < 3; index++)
                targets[index].transform.SetLocalPositionAndRotation(poses[index].position, poses[index].rotation);
        }

        // Independent raw GAME transform measurements, not the author's output
        // pose table. All parent scales are1; Chassis38351 is identity below64200.
        private static Pose OriginalChassisPose(int index)
        {
            var positions = new[]
            {
                new Vector3(0.22501259f, -0.10993704f, -0.18478249f), //59373
                new Vector3(-0.2249874f, -0.1099374f, -0.18478276f), //70667
                new Vector3(1.26986315e-5f, -0.1349985f, 0.26939392f), //52875
            };
            var rotations = new[]
            {
                new Quaternion(-0.9659261f, 1.1313301e-8f, -4.2221988e-8f, -0.258818f),
                new Quaternion(-0.9659263f, 1.6868266e-7f, -8.438868e-8f, -0.2588176f),
                new Quaternion(-1.0442067e-6f, 0.13052534f, -0.991445f, -6.5869597e-7f),
            };
            Vector3 motorPosition = new Vector3(-1.3051683e-5f, 0.117760345f, 0.1392753f); //43037
            Quaternion motorRotation = new Quaternion(0.7071064f, -2.843851e-8f, -7.3660664e-9f, 0.70710725f);
            Vector3 subframePosition = new Vector3(0f, -0.24171245f, 1.2000003f); //47402
            Quaternion subframeRotation = new Quaternion(3.8274667e-8f, 0.70710725f, 0.7071064f, -5.934715e-8f);
            return new Pose(subframePosition + subframeRotation * (motorPosition + motorRotation * positions[index]),
                subframeRotation * motorRotation * rotations[index]);
        }
    }
}
