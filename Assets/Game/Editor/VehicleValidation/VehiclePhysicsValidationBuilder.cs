using System;
using System.Linq;
using MSC.Editor.VehicleSimulation;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.VehicleValidation
{
    public static class VehiclePhysicsValidationBuilder
    {
        [MenuItem(VehiclePhysicsValidationPaths.MenuRoot + "Build / Rebuild Validation Setup")]
        public static void Build()
        {
            VehicleSimulationPrototypeBuilder.Build();
            VehicleSimulationConfig config = AssetDatabase.LoadAssetAtPath<VehicleSimulationConfig>(
                VehicleSimulationPrototypePaths.PrototypeConfig);
            if (config == null)
            {
                throw new InvalidOperationException(
                    "M06A requires the M06 simulation config asset.");
            }

            if (!config.Validate(out string configFailure))
            {
                throw new InvalidOperationException(
                    "M06A requires a valid M06 simulation config: " + configFailure);
            }

            VehicleCalibrationProfile profile = VehiclePhysicsValidationProfileBuilder.Build(config);
            Scene scene = EditorSceneManager.OpenScene(
                VehicleSimulationPrototypePaths.PrototypeScene,
                OpenSceneMode.Single);
            VehicleSimulationHost host = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<VehicleSimulationHost>(true))
                .Single();
            GameObject validationRoot = new GameObject("M06A_VehiclePhysicsValidation");
            VehicleValidationRouteAuthoring route =
                VehiclePhysicsValidationCourseBuilder.Build(validationRoot.transform, config);
            VehiclePhysicsValidationRig rig =
                validationRoot.AddComponent<VehiclePhysicsValidationRig>();
            rig.Configure(profile, host, route);
            if (!rig.Validate(out string rigFailure))
            {
                throw new InvalidOperationException("M06A validation rig is invalid: " + rigFailure);
            }

            VehicleSimulationEditorUtility.EnsureFolder(VehiclePhysicsValidationPaths.Scenes);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(
                    scene,
                    VehiclePhysicsValidationPaths.Scene,
                    saveAsCopy: true))
            {
                throw new InvalidOperationException("Could not save M06A validation scene.");
            }

            EditorSceneManager.OpenScene(
                VehicleSimulationPrototypePaths.PrototypeScene,
                OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            VehiclePhysicsValidationValidator.ValidateOrThrow();
            Debug.Log(
                "M06A_PHYSICS_VALIDATION_BUILD_OK " +
                $"version={VehiclePhysicsValidationPaths.BuilderVersion} " +
                $"profile={profile.ProfileId} fixtures={profile.Fixtures.Length} " +
                $"metrics={profile.MetricDefinitions.Length} scene={VehiclePhysicsValidationPaths.Scene}");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("M06A builder batch entry requires batch mode.");
            }

            Build();
        }
    }
}
