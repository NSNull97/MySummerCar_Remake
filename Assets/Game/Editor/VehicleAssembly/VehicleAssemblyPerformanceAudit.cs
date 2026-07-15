using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace MSC.Editor.VehicleAssembly
{
    public static class VehicleAssemblyPerformanceAudit
    {
        public const string ReportPath = "Docs/Vehicle/ASSEMBLY_PERFORMANCE_AUDIT.md";
        private const int QueryIterations = 10000;

        [MenuItem("Tools/MSC Remake/Vehicle Assembly/Run Performance Audit")]
        public static void Run()
        {
            Scene scene = EditorSceneManager.OpenScene(
                VehicleAssemblyPrototypePaths.PrototypeScene,
                OpenSceneMode.Single);
            VehicleAssemblyController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<VehicleAssemblyController>(true))
                .Single();
            controller.Initialize();
            PartInstance drum = controller.Parts.Single(part =>
                part.Definition.DefinitionId == "vehicle.brake_drum_rl");
            MountPointAuthoring mount = controller.MountPoints.Single(candidate =>
                candidate.Definition.AcceptsPart("vehicle.brake_drum_rl"));
            drum.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            Physics.SyncTransforms();

            for (int i = 0; i < 128; i++)
            {
                controller.FindBestMount(drum, includeInvalid: true);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            int mutationsBefore = controller.GraphMutationCount;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < QueryIterations; i++)
            {
                controller.FindBestMount(drum, includeInvalid: true);
            }

            stopwatch.Stop();
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            int mutationsAfter = controller.GraphMutationCount;
            int gameObjects = scene.GetRootGameObjects()
                .Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
            int renderers = scene.GetRootGameObjects()
                .Sum(root => root.GetComponentsInChildren<Renderer>(true).Length);
            int colliders = scene.GetRootGameObjects()
                .Sum(root => root.GetComponentsInChildren<Collider>(true).Length);
            int rigidbodies = scene.GetRootGameObjects()
                .Sum(root => root.GetComponentsInChildren<Rigidbody>(true).Length);
            double microsecondsPerQuery = stopwatch.Elapsed.TotalMilliseconds * 1000d / QueryIterations;
            string report =
                "# M05 Assembly Performance Audit\n\n" +
                "Профиль: Unity Editor batch mode, deterministic rear-drum candidate query.\n\n" +
                $"- Iterations: `{QueryIterations}`\n" +
                $"- Total query time: `{stopwatch.Elapsed.TotalMilliseconds:0.###} ms`\n" +
                $"- Mean: `{microsecondsPerQuery:0.###} µs/query`\n" +
                $"- Managed allocation in measured loop: `{allocatedBytes} bytes`\n" +
                $"- Graph mutations during query loop: `{mutationsAfter - mutationsBefore}`\n" +
                $"- Scene GameObjects: `{gameObjects}`\n" +
                $"- Renderers: `{renderers}`\n" +
                $"- Colliders: `{colliders}`\n" +
                $"- Rigidbodies: `{rigidbodies}`\n" +
                "- Candidate search uses serialized arrays and `OverlapSphereNonAlloc`; no scene-wide lookup is performed per frame.\n" +
                "- Dependency graph changes only after successful operations or restore, not during preview queries.\n";

            string fullPath = Path.GetFullPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException());
            File.WriteAllText(fullPath, report);
            AssetDatabase.Refresh();

            if (allocatedBytes != 0 || mutationsAfter != mutationsBefore)
            {
                throw new InvalidOperationException(
                    $"M05 performance audit failed: allocations={allocatedBytes}, graphMutations={mutationsAfter - mutationsBefore}.");
            }

            Debug.Log(
                $"M05_VEHICLE_ASSEMBLY_PERFORMANCE_OK iterations={QueryIterations} " +
                $"allocatedBytes={allocatedBytes} meanMicroseconds={microsecondsPerQuery:0.###} objects={gameObjects}");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("M05 performance audit requires batch mode.");
            }

            Run();
        }
    }
}
