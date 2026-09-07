using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Frozen donor presentation-frame repair; no graph, stages or save IDs change.</summary>
    public static class Phase1SatsumaMountFrameRepairAuthoring
    {
        public const string LeftShaft = "mount.satsuma.halfshaft-fl";
        public const string RightShaft = "mount.satsuma.halfshaft-fr";
        public const string LeftDoor = "mount.satsuma.door-left";
        public const string RightDoor = "mount.satsuma.door-right";
        private const float PositionTolerance = 0.00001f;

        // GAME: container48337/60434 -> xxxxx48021/57559 -> axle40854/43830.
        // Align the common axle mesh with loose axle51045/66668, not the container.
        public static Pose LegacyShaftPose(bool left) => new(
            left ? new Vector3(-0.06927502f, -0.224f, 0.03274586f)
                : new Vector3(0.06538724f, -0.224f, 1.1588293f), Quaternion.identity);
        public static Pose CorrectedShaftPose(bool left) => new(
            left ? new Vector3(-0.06927559f, -0.2229250004f, 1.162524315f)
                : new Vector3(0.06538736f, -0.2229254177f, 1.1625237096f),
            left ? new Quaternion(0f, 1f, 0f, 0f) : Quaternion.identity);

        public static Pose DoorPivot(bool left) => new(
            new Vector3(left ? -0.67700034f : 0.67699975f, 0.1808086f,
                left ? 0.6389993f : 0.6389997f),
            new Quaternion(1.152023e-7f, 0.707107f, 0.7071066f, -1.15202354e-7f));

        // Trigger37060/36990 is 54.6cm away from pivot45349/61987.
        public static Vector3 DoorAimChassisPosition(bool left) =>
            new(left ? -0.82699966f : 0.8270006f, 0.1808086f,
                left ? 0.11389786f : 0.11389798f);

        // Existing V1c fastener audit poses, in the original container frame.
        public static Pose[] LegacyShaftBoltPoses(bool left)
        {
            Quaternion rotation = left
                ? new Quaternion(0.500001132f, -0.5000011f, -0.499998957f, 0.499998957f)
                : new Quaternion(-0.499999285f, 0.4999991f, -0.5000008f, 0.5000009f);
            return left ? new[]
            {
                new Pose(new Vector3(-0.021795772f, -0.0220652223f, 1.16686773f), rotation),
                new Pose(new Vector3(-0.02179576f, 0.0443708f, 1.12961435f), rotation),
                new Pose(new Vector3(-0.0217957478f, -0.0233245641f, 1.09200048f), rotation),
            } : new[]
            {
                new Pose(new Vector3(0.0217127688f, 0.0442868173f, 0.00391061325f), rotation),
                new Pose(new Vector3(0.0217127819f, -0.0228859484f, -0.03374808f), rotation),
                new Pose(new Vector3(0.0217127576f, -0.02189362f, 0.0413594954f), rotation),
            };
        }

        public static Pose Reframe(Pose local, Pose from, Pose to)
        {
            Quaternion inverse = Quaternion.Inverse(to.rotation);
            return new Pose(inverse * (from.position + from.rotation * local.position - to.position),
                inverse * from.rotation * local.rotation);
        }

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Halfshaft and Door Frames")]
        public static void RefreshMountFramesBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before refreshing installation frames.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(contents.GetComponent<VehicleAssemblyController>());
                if (changed > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/codex-mount-frames-before-" +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                        throw new InvalidDataException("Could not save reviewed mounting frames.");
                }
                Debug.Log("SATSUMA_MOUNT_FRAME_REFRESH_OK changed=" + changed + " fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            var writes = new List<Action>();
            // Validate the complete packet before changing any authored object.
            foreach (bool left in new[] { true, false })
            {
                PlanShaft(assembly, left, writes);
                PlanDoor(assembly, left, writes);
            }
            foreach (Action write in writes) write();
            return writes.Count;
        }

        private static void PlanShaft(VehicleAssemblyController assembly, bool left, List<Action> writes)
        {
            string id = left ? LeftShaft : RightShaft;
            MountPointAuthoring mount = RequireMount(assembly, id);
            Pose oldPose = LegacyShaftPose(left), newPose = CorrectedShaftPose(left);
            bool legacy = Matches(mount.transform, oldPose), current = Matches(mount.transform, newPose);
            if (!legacy && !current || mount.Pose.parent != mount.transform ||
                !Matches(mount.Pose, new Pose(Vector3.zero, Quaternion.identity)))
                throw new InvalidDataException("Unexpected halfshaft frame: " + id);
            AssemblyFastenerInteractionTarget[] targets = mount
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .OrderBy(value => value.FastenerDefinitionId, StringComparer.Ordinal).ToArray();
            Pose[] oldBolts = LegacyShaftBoltPoses(left);
            if (targets.Length != 3 || mount.Definition.Fasteners.Length != 3)
                throw new InvalidDataException("Expected three halfshaft fasteners: " + id);
            for (int index = 0; index < targets.Length; index++)
            {
                AssemblyFastenerInteractionTarget target = targets[index];
                string boltId = "fastener.satsuma.halfshaft-" + (left ? "fl" : "fr") + ".boltpm-" + (index + 1);
                Pose expected = current ? Reframe(oldBolts[index], oldPose, newPose) : oldBolts[index];
                if (target.Controller != assembly || target.MountId != id ||
                    target.FastenerDefinitionId != boltId || target.transform.parent != mount.transform ||
                    !Matches(target.transform, expected))
                    throw new InvalidDataException("Halfshaft bolt frame drifted: " + boltId);
            }
            if (legacy)
            {
                writes.Add(() =>
                {
                    mount.transform.SetLocalPositionAndRotation(newPose.position, newPose.rotation);
                    for (int index = 0; index < targets.Length; index++)
                    {
                        Pose pose = Reframe(oldBolts[index], oldPose, newPose);
                        targets[index].transform.SetLocalPositionAndRotation(pose.position, pose.rotation);
                        EditorUtility.SetDirty(targets[index].transform);
                    }
                    EditorUtility.SetDirty(mount.transform);
                });
            }
        }

        private static void PlanDoor(VehicleAssemblyController assembly, bool left, List<Action> writes)
        {
            MountPointAuthoring mount = RequireMount(assembly, left ? LeftDoor : RightDoor);
            Pose pivot = DoorPivot(left);
            if (!Matches(mount.transform, pivot))
                throw new InvalidDataException("Door hinge frame drifted: " + mount.MountId);
            Vector3 localAim = Quaternion.Inverse(pivot.rotation) * (DoorAimChassisPosition(left) - pivot.position);
            SphereCollider sphere = mount.GetComponent<SphereCollider>();
            var anchor = mount.GetComponent<AssemblyMountInteractionAnchor>();
            bool current = anchor != null;
            if (sphere == null || !sphere.isTrigger || Mathf.Abs(sphere.radius - 0.03f) > PositionTolerance ||
                Vector3.Distance(sphere.center, current ? localAim : Vector3.zero) > PositionTolerance ||
                current && Vector3.Distance(anchor.LocalPosition, localAim) > PositionTolerance)
                throw new InvalidDataException("Door interaction frame drifted: " + mount.MountId);
            if (!current)
            {
                writes.Add(() =>
                {
                    mount.gameObject.AddComponent<AssemblyMountInteractionAnchor>().Configure(localAim);
                    sphere.center = localAim;
                    EditorUtility.SetDirty(sphere);
                });
            }
        }

        private static MountPointAuthoring RequireMount(VehicleAssemblyController assembly, string id)
        {
            MountPointAuthoring mount = assembly.MountPoints.SingleOrDefault(value => value != null && value.MountId == id);
            if (mount == null || mount.Definition == null || mount.Definition.DefinitionId != id ||
                mount.Definition.OwnerPartDefinitionId != "vehicle.satsuma.part.body-shell" ||
                Vector3.Distance(mount.transform.localScale, Vector3.one) > PositionTolerance)
                throw new InvalidDataException("Missing reviewed body-owned mount: " + id);
            return mount;
        }

        private static bool Matches(Transform transform, Pose pose) =>
            Vector3.Distance(transform.localPosition, pose.position) <= PositionTolerance &&
            Mathf.Abs(Quaternion.Dot(transform.localRotation.normalized, pose.rotation.normalized)) > 0.999999f;
    }
}
