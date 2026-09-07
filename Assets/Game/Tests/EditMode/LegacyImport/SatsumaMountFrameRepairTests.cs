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
    public sealed class SatsumaMountFrameRepairTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void CorrectedShaftPoseMatchesIndependentCommonMeshTransformChain(bool left)
        {
            Vector3 container = left ? new Vector3(-.06927559f, -.224f, .032746315f)
                : new Vector3(.06538736f, -.224f, 1.15883f);
            Quaternion shaft = new(0f, -.7071068f, 0f, .7071068f);
            Vector3 axle = left ? new Vector3(1.129778f, .0010749996f, -.069f)
                : new Vector3(.0036937096f, .0010745823f, .069f);
            Quaternion axleRotation = left ? new Quaternion(-.5f, -.5f, -.5f, .5f)
                : new Quaternion(-.5f, .5f, .5f, .5f);
            Quaternion looseAxleRotation = new(-.7071069f, 0f, 0f, .70710677f);
            Pose corrected = Phase1SatsumaMountFrameRepairAuthoring.CorrectedShaftPose(left);
            Vector3 transferredAxle = corrected.position + corrected.rotation * new Vector3(-.069f, 0f, 0f);
            Assert.That(Vector3.Distance(transferredAxle, container + shaft * axle), Is.LessThan(.000002f));
            Assert.That(Mathf.Abs(Quaternion.Dot((corrected.rotation * looseAxleRotation).normalized,
                (shaft * axleRotation).normalized)), Is.GreaterThan(.999999f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyPacketRepairsBothShaftsAndDoorAimWithoutMovingBoltsOrHinges(bool moveVehicle)
        {
            GameObject contents = LoadContents();
            try
            {
                var assembly = contents.GetComponent<VehicleAssemblyController>();
                PutInLegacyFrames(assembly);
                if (moveVehicle) contents.transform.SetPositionAndRotation(new Vector3(8f, 2f, -12f), Quaternion.Euler(7f, 143f, 4f));
                var bolts = Targets(assembly).ToDictionary(value => value, value => new Pose(value.transform.position, value.transform.rotation));
                var doors = new[] { Mount(assembly, "door-left"), Mount(assembly, "door-right") };
                var doorPoses = doors.Select(value => new Pose(value.Pose.position, value.Pose.rotation)).ToArray();
                int mutation = assembly.GraphMutationCount;
                Assert.That(Phase1SatsumaMountFrameRepairAuthoring.ApplyToInstance(assembly), Is.EqualTo(4));
                Assert.That(Phase1SatsumaMountFrameRepairAuthoring.ApplyToInstance(assembly), Is.Zero);
                Assert.That(assembly.GraphMutationCount, Is.EqualTo(mutation));
                foreach (var entry in bolts)
                {
                    Assert.That(Vector3.Distance(entry.Key.transform.position, entry.Value.position), Is.LessThan(.00002f));
                    Assert.That(Mathf.Abs(Quaternion.Dot(entry.Key.transform.rotation, entry.Value.rotation)), Is.GreaterThan(.999999f));
                }
                for (int index = 0; index < 2; index++)
                {
                    Assert.That(doors[index].Pose.position, Is.EqualTo(doorPoses[index].position));
                    Assert.That(doors[index].Pose.rotation, Is.EqualTo(doorPoses[index].rotation));
                    Vector3 aim = AssemblyMountInteractionAnchor.ResolveWorldPosition(doors[index]);
                    Assert.That(Vector3.Distance(aim, assembly.transform.TransformPoint(
                        Phase1SatsumaMountFrameRepairAuthoring.DoorAimChassisPosition(index == 0))), Is.LessThan(.00002f));
                    SphereCollider shape = doors[index].GetComponent<SphereCollider>();
                    Assert.That(Vector3.Distance(shape.transform.TransformPoint(shape.center), aim), Is.LessThan(.000002f));
                    Assert.That(Vector3.Distance(aim, doors[index].Pose.position), Is.GreaterThan(.54f));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        [Test]
        public void UnknownLastDoorOffsetRejectsWholePacketBeforeAnyWrite()
        {
            GameObject contents = LoadContents();
            try
            {
                var assembly = contents.GetComponent<VehicleAssemblyController>();
                PutInLegacyFrames(assembly);
                Mount(assembly, "door-right").GetComponent<SphereCollider>().center = Vector3.one;
                Vector3 before = Mount(assembly, "halfshaft-fl").transform.localPosition;
                Assert.Throws<InvalidDataException>(() => Phase1SatsumaMountFrameRepairAuthoring.ApplyToInstance(assembly));
                Assert.That(Mount(assembly, "halfshaft-fl").transform.localPosition, Is.EqualTo(before));
                Assert.That(Mount(assembly, "door-left").GetComponent<AssemblyMountInteractionAnchor>(), Is.Null);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        [Test]
        public void UnboundMountRetainsOriginalPoseAimContract()
        {
            var root = new GameObject("unbound");
            try
            {
                var mount = root.AddComponent<MountPointAuthoring>();
                root.transform.position = new Vector3(2f, 3f, 4f);
                Assert.That(AssemblyMountInteractionAnchor.ResolveWorldPosition(mount), Is.EqualTo(mount.Pose.position));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void PutInLegacyFrames(VehicleAssemblyController assembly)
        {
            foreach (bool left in new[] { true, false })
            {
                var mount = Mount(assembly, left ? "halfshaft-fl" : "halfshaft-fr");
                Pose pose = Phase1SatsumaMountFrameRepairAuthoring.LegacyShaftPose(left);
                mount.transform.SetLocalPositionAndRotation(pose.position, pose.rotation);
                var targets = mount.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .OrderBy(value => value.FastenerDefinitionId, StringComparer.Ordinal).ToArray();
                Pose[] bolts = Phase1SatsumaMountFrameRepairAuthoring.LegacyShaftBoltPoses(left);
                for (int index = 0; index < 3; index++) targets[index].transform.SetLocalPositionAndRotation(bolts[index].position, bolts[index].rotation);
                var door = Mount(assembly, left ? "door-left" : "door-right");
                var anchor = door.GetComponent<AssemblyMountInteractionAnchor>();
                if (anchor != null) Object.DestroyImmediate(anchor);
                door.GetComponent<SphereCollider>().center = Vector3.zero;
            }
        }
        private static MountPointAuthoring Mount(VehicleAssemblyController assembly, string slug) =>
            assembly.MountPoints.Single(value => value.MountId == "mount.satsuma." + slug);
        private static AssemblyFastenerInteractionTarget[] Targets(VehicleAssemblyController assembly) =>
            assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Where(value =>
                value.MountId == Phase1SatsumaMountFrameRepairAuthoring.LeftShaft ||
                value.MountId == Phase1SatsumaMountFrameRepairAuthoring.RightShaft).ToArray();
        private static GameObject LoadContents()
        {
            if (!File.Exists(Phase1SatsumaBaselineBuilder.RuntimePrefabPath)) Assert.Ignore("Private generated baseline absent.");
            return PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
        }
    }
}
