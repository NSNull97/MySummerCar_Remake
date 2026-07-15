using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Player;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.VehicleAssembly
{
    public static class VehicleAssemblyPrototypeValidator
    {
        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(VehicleAssemblyPrototypePaths.PrototypeScene) == null)
            {
                errors.Add("Missing M05 prototype scene: " + VehicleAssemblyPrototypePaths.PrototypeScene);
                return errors;
            }

            Scene scene = EditorSceneManager.OpenScene(
                VehicleAssemblyPrototypePaths.PrototypeScene,
                OpenSceneMode.Single);
            VehicleAssemblyController controller = FindInScene<VehicleAssemblyController>(scene);
            if (controller == null)
            {
                errors.Add("Prototype scene has no VehicleAssemblyController.");
                return errors;
            }

            if (FindInScene<VehicleAssemblyPrototypeMarker>(scene) == null)
            {
                errors.Add("Prototype scene has no M05 marker.");
            }

            if (FindInScene<PlayerInteractionController>(scene) == null ||
                FindInScene<PhysicalCarryController>(scene) == null ||
                FindInScene<CrossdotPresenter>(scene) == null)
            {
                errors.Add(
                    "Prototype scene does not preserve the M4 Player/Interaction/Crossdot composition.");
            }

            if (controller.Parts.Length < 12 || controller.Parts.Length > 20)
            {
                errors.Add($"Representative vehicle must have 12-20 parts, found {controller.Parts.Length}.");
            }

            if (controller.MountPoints.Length != controller.Parts.Length - 1)
            {
                errors.Add("Every non-root representative part must have one mount point.");
            }

            int rootCount = controller.Parts.Count(part => part != null && part.IsAssemblyRoot);
            if (rootCount != 1)
            {
                errors.Add("Representative vehicle must have exactly one assembly root.");
            }

            IReadOnlyList<VehicleAssemblyValidationIssue> runtimeIssues = VehicleAssemblyValidator.Validate(
                controller.Parts,
                controller.MountPoints,
                controller.Dependencies,
                controller.Tools);
            foreach (VehicleAssemblyValidationIssue issue in runtimeIssues)
            {
                if (issue.Severity == VehicleAssemblyValidationSeverity.Error)
                {
                    errors.Add(issue.Code + ": " + issue.Message);
                }
            }

            ValidateInteractionTargets(scene, controller, errors);
            ValidateRearDrumContract(controller, errors);
            ValidateNoDonorDependencies(errors);
            ValidateBuildSettings(errors);
            return errors;
        }

        [MenuItem("Tools/MSC Remake/Vehicle Assembly/Validate Definitions and Prototype")]
        public static void ValidateMenu()
        {
            ThrowIfInvalid();
            Debug.Log("M05_VEHICLE_ASSEMBLY_VALIDATION_OK");
        }

        [MenuItem("Tools/MSC Remake/Vehicle Assembly/Export Validation Report")]
        public static void ExportValidationReport()
        {
            IReadOnlyList<string> errors = Validate();
            VehicleAssemblyController controller = FindInScene<VehicleAssemblyController>(
                SceneManager.GetActiveScene());
            string report = BuildReport(controller, errors);
            string fullPath = Path.GetFullPath(VehicleAssemblyPrototypePaths.ValidationReport);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException());
            File.WriteAllText(fullPath, report);
            AssetDatabase.Refresh();
            Debug.Log($"M05_VEHICLE_ASSEMBLY_REPORT_WRITTEN path={VehicleAssemblyPrototypePaths.ValidationReport} errors={errors.Count}");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("M05 batch validation requires batch mode.");
            }

            ThrowIfInvalid();
            Debug.Log("M05_VEHICLE_ASSEMBLY_VALIDATION_OK");
        }

        private static void ThrowIfInvalid()
        {
            IReadOnlyList<string> errors = Validate();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Milestone 05 vehicle assembly validation failed:\n- " +
                    string.Join("\n- ", errors));
            }
        }

        private static void ValidateInteractionTargets(
            Scene scene,
            VehicleAssemblyController controller,
            List<string> errors)
        {
            if (FindAllInScene<AssemblyMountHandoffTarget>(scene).Length != controller.MountPoints.Length)
            {
                errors.Add("Each mount must expose the M4 IMountHandoffTarget boundary.");
            }

            if (FindAllInScene<AssemblyFastenerInteractionTarget>(scene).Length != controller.MountPoints.Length)
            {
                errors.Add("Each representative mount must expose a fastener tool target.");
            }

            if (FindAllInScene<AssemblyInstalledPartInteractionTarget>(scene).Length != controller.Parts.Length)
            {
                errors.Add("Each representative part must expose a contextual removal target.");
            }

            if (FindInScene<AssemblyMountPreviewPresenter>(scene) == null)
            {
                errors.Add("Mount preview presenter is missing.");
            }

            InteractionTargetHost[] hosts = FindAllInScene<InteractionTargetHost>(scene);
            if (hosts.Any(host => !host.HasCapabilities))
            {
                errors.Add("An InteractionTargetHost has no explicit capability.");
            }
        }

        private static void ValidateRearDrumContract(
            VehicleAssemblyController controller,
            List<string> errors)
        {
            PartInstance drum = controller.Parts.FirstOrDefault(part =>
                part != null && part.Definition != null &&
                part.Definition.DefinitionId == "vehicle.brake_drum_rl");
            MountPointAuthoring mount = controller.MountPoints.FirstOrDefault(candidate =>
                candidate != null && candidate.Definition != null &&
                candidate.Definition.AcceptsPart("vehicle.brake_drum_rl"));
            if (drum == null || mount == null)
            {
                errors.Add("Rear brake drum representative fixture is missing.");
                return;
            }

            FastenerDefinition[] fasteners = mount.Definition.Fasteners;
            if (fasteners.Length != 1 || fasteners[0] == null ||
                fasteners[0].Size != FastenerSize.Millimeter14 ||
                fasteners[0].MaximumStage != 8 ||
                Mathf.Abs(mount.Definition.ReferenceCandidateRadiusMeters - 0.01f) > 0.0001f)
            {
                errors.Add("Rear drum must preserve one BoltPM, wrench 14, stages 0..8 and 0.01 m donor marker provenance.");
            }

            bool wheelBlocker = controller.Dependencies.Any(dependency =>
                dependency != null &&
                dependency.Kind == AssemblyDependencyKind.RemovalBlockedWhileInstalled &&
                dependency.DependentPartDefinitionId == "vehicle.brake_drum_rl" &&
                dependency.RelatedPartDefinitionId == "vehicle.wheel_rl");
            if (!wheelBlocker)
            {
                errors.Add("Rear wheel installed-state removal blocker is missing.");
            }
        }

        private static void ValidateNoDonorDependencies(List<string> errors)
        {
            string[] dependencies = AssetDatabase.GetDependencies(
                VehicleAssemblyPrototypePaths.PrototypeScene,
                recursive: true);
            for (int i = 0; i < dependencies.Length; i++)
            {
                string path = dependencies[i].Replace('\\', '/');
                if (path.Contains("/LegacyImport/ReferenceOnly/") ||
                    path.Contains("/Imported/DonorGenerated/"))
                {
                    errors.Add("Production prototype depends on donor/reference-only asset: " + path);
                }
            }
        }

        private static void ValidateBuildSettings(List<string> errors)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int bootstrapIndex = Array.FindIndex(scenes, scene =>
                scene.enabled && scene.path == "Assets/Game/Bootstrap/Bootstrap.unity");
            int prototypeIndex = Array.FindIndex(scenes, scene =>
                scene.enabled && scene.path == VehicleAssemblyPrototypePaths.PrototypeScene);
            if (bootstrapIndex != 0)
            {
                errors.Add("Bootstrap must remain the first enabled build scene.");
            }

            if (prototypeIndex < 1)
            {
                errors.Add("M05 prototype scene is not enabled after Bootstrap.");
            }
        }

        private static string BuildReport(
            VehicleAssemblyController controller,
            IReadOnlyList<string> errors)
        {
            int parts = controller != null ? controller.Parts.Length : 0;
            int mounts = controller != null ? controller.MountPoints.Length : 0;
            int fasteners = controller != null
                ? controller.MountPoints.Sum(mount => mount.Definition.Fasteners.Length)
                : 0;
            string result = errors.Count == 0 ? "PASS" : "FAIL";
            string errorLines = errors.Count == 0
                ? "- Ошибок не найдено."
                : string.Join("\n", errors.Select(error => "- " + error));
            return
                "# M05 Assembly Validation Report\n\n" +
                $"- Result: `{result}`\n" +
                $"- Builder: `{VehicleAssemblyPrototypePaths.BuilderVersion}`\n" +
                "- Reference dataset: `04B.4`\n" +
                $"- Representative parts: `{parts}`\n" +
                $"- Mounts: `{mounts}`\n" +
                $"- Fasteners: `{fasteners}`\n\n" +
                "## Findings\n\n" + errorLines + "\n";
        }

        private static T FindInScene<T>(Scene scene)
            where T : Component
        {
            T[] all = FindAllInScene<T>(scene);
            return all.Length > 0 ? all[0] : null;
        }

        private static T[] FindAllInScene<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
