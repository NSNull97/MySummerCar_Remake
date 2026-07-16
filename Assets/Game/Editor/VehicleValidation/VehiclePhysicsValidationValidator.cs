using System;
using System.Collections.Generic;
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
    public static class VehiclePhysicsValidationValidator
    {
        [MenuItem(VehiclePhysicsValidationPaths.MenuRoot + "Validate Setup")]
        public static void ValidateMenu()
        {
            ValidateOrThrow();
        }

        public static void ValidateOrThrow()
        {
            IReadOnlyList<string> errors = Validate();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "M06A validation setup failed:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log("M06A_PHYSICS_VALIDATION_SETUP_OK errors=0");
        }

        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            VehicleCalibrationProfile profile =
                AssetDatabase.LoadAssetAtPath<VehicleCalibrationProfile>(
                    VehiclePhysicsValidationPaths.Profile);
            if (profile == null)
            {
                errors.Add("Missing calibration profile: " + VehiclePhysicsValidationPaths.Profile);
            }
            else if (!profile.Validate(out string profileFailure))
            {
                errors.Add("Calibration profile is invalid: " + profileFailure);
            }
            else
            {
                ValidateClassificationGuards(profile, errors);
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(VehiclePhysicsValidationPaths.Scene) == null)
            {
                errors.Add("Missing validation scene: " + VehiclePhysicsValidationPaths.Scene);
            }
            else
            {
                Scene scene = EditorSceneManager.OpenScene(
                    VehiclePhysicsValidationPaths.Scene,
                    OpenSceneMode.Additive);
                try
                {
                    VehiclePhysicsValidationRig[] rigs = FindInScene<VehiclePhysicsValidationRig>(scene);
                    VehicleValidationRouteAuthoring[] routes =
                        FindInScene<VehicleValidationRouteAuthoring>(scene);
                    VehicleSimulationHost[] hosts = FindInScene<VehicleSimulationHost>(scene);
                    if (rigs.Length != 1 || routes.Length != 1 || hosts.Length != 1)
                    {
                        errors.Add(
                            $"Validation scene requires one rig/route/host; found " +
                            $"{rigs.Length}/{routes.Length}/{hosts.Length}.");
                    }
                    else
                    {
                        if (!rigs[0].Validate(out string rigFailure))
                        {
                            errors.Add("Validation rig is invalid: " + rigFailure);
                        }

                        ValidateProxyComposition(hosts[0], errors);
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }

            EditorBuildSettingsScene validationBuildScene = EditorBuildSettings.scenes.FirstOrDefault(
                candidate => string.Equals(
                    candidate.path,
                    VehiclePhysicsValidationPaths.Scene,
                    StringComparison.Ordinal));
            if (validationBuildScene != null && validationBuildScene.enabled)
            {
                errors.Add("M06A validation scene must not modify the frozen normal Build Settings list.");
            }

            VehicleSimulationEditorUtility.ValidateBuildSettings(errors);
            VehicleSimulationEditorUtility.ValidateNoDonorDependencies(
                VehiclePhysicsValidationPaths.Scene,
                errors);
            return errors;
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("M06A validator batch entry requires batch mode.");
            }

            ValidateOrThrow();
        }

        private static void ValidateProxyComposition(
            VehicleSimulationHost host,
            ICollection<string> errors)
        {
            Rigidbody body = host.GetComponent<Rigidbody>();
            BoxCollider collider = host.GetComponent<BoxCollider>();
            if (body == null || collider == null)
            {
                errors.Add("Validation host lacks Rigidbody or BoxCollider.");
                return;
            }

            VehicleDynamicsConfig dynamics = host.Config.Dynamics;
            if (Mathf.Abs(body.mass - dynamics.ProvisionalMassKilograms) > 0.001f ||
                Vector3.Distance(body.centerOfMass, dynamics.CenterOfMassMeters) > 0.001f ||
                Vector3.Distance(collider.center, dynamics.ChassisColliderCenterMeters) > 0.001f ||
                Vector3.Distance(collider.size, dynamics.ChassisColliderSizeMeters) > 0.001f ||
                Mathf.Abs(body.linearDamping - dynamics.ChassisLinearDamping) > 0.0001f ||
                Mathf.Abs(body.angularDamping - dynamics.ChassisAngularDamping) > 0.0001f)
            {
                errors.Add("Validation proxy composition does not match central simulation config.");
            }
        }

        private static void ValidateClassificationGuards(
            VehicleCalibrationProfile profile,
            ICollection<string> errors)
        {
            if (profile.VehicleConfiguration.DynamicTuning.Classification !=
                    VehicleReferenceClassification.RemakeDesignTarget ||
                !profile.VehicleConfiguration.DynamicTuning.IsProvisional)
            {
                errors.Add("Dynamic configuration must remain explicit ProvisionalProjectTuning.");
            }

            VehicleReferenceTarget radius = profile.ReferenceTargets.FirstOrDefault(target =>
                target != null && target.MetricId == "geometry.wheel_radius_m");
            if (radius == null || radius.Classification != VehicleReferenceClassification.PlausibilityTarget)
            {
                errors.Add("Candidate wheel radius must remain PlausibilityTarget.");
            }

            bool invalidMassClaim = profile.ReferenceTargets.Any(target =>
                target != null && target.MetricId == "mass.proxy_total_kg" &&
                (target.Classification == VehicleReferenceClassification.MeasuredDonorReference ||
                 Mathf.Abs(target.TargetValue - 389f) < 0.001f));
            if (invalidMassClaim)
            {
                errors.Add("Donor root Rigidbody 389 kg must not become proxy/assembled mass authority.");
            }
        }

        private static T[] FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
