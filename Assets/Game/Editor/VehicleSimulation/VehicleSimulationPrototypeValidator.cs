using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Audio.UnityFallback;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MSC.Editor.VehicleSimulation
{
    public static class VehicleSimulationPrototypeValidator
    {
        private static readonly Vector3[] ExpectedWheelAnchors04B4 =
        {
            new Vector3(-0.6299995f, 0f, 1.1669996f),
            new Vector3(0.6300007f, 0f, 1.1669996f),
            new Vector3(-0.6029993f, 0f, -1.167f),
            new Vector3(0.603001f, 0f, -1.1669996f)
        };

        private static readonly string[] RequiredAssemblyPartIds =
        {
            "m06.chassis",
            "m06.engine",
            "m06.starter",
            "m06.battery",
            "m06.fuel_tank",
            "m06.clutch",
            "m06.gearbox",
            "m06.differential",
            "m06.wheel.fl",
            "m06.wheel.fr",
            "m06.wheel.rl",
            "m06.wheel.rr"
        };

        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            ValidateRequiredAssets(errors);
            VehicleSimulationEditorUtility.ValidateBuildSettings(errors);

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                VehicleSimulationPrototypePaths.PrototypeScene);
            if (sceneAsset == null)
            {
                return errors;
            }

            Scene scene = EditorSceneManager.OpenScene(
                VehicleSimulationPrototypePaths.PrototypeScene,
                OpenSceneMode.Single);
            ValidateAssemblyFixture(scene, errors);
            ValidateSimulationComposition(scene, errors);
            VehicleSimulationEditorUtility.ValidateNoDonorDependencies(
                VehicleSimulationPrototypePaths.PrototypeScene,
                errors);
            VehicleSimulationEditorUtility.ValidateNoDonorDependencies(
                VehicleSimulationPrototypePaths.PrototypeConfig,
                errors);
            return errors;
        }

        [MenuItem(VehicleSimulationPrototypePaths.MenuRoot + "Validate Configs")]
        public static void ValidateMenu()
        {
            ThrowIfInvalid();
            Debug.Log("M06_VEHICLE_SIMULATION_VALIDATION_OK");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("M06 validation batch entry requires batch mode.");
            }

            ThrowIfInvalid();
            Debug.Log("M06_VEHICLE_SIMULATION_VALIDATION_OK");
        }

        private static void ThrowIfInvalid()
        {
            IReadOnlyList<string> errors = Validate();
            if (errors.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Milestone 06 vehicle simulation validation failed:\n- " +
                string.Join("\n- ", errors));
        }

        private static void ValidateRequiredAssets(ICollection<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    VehicleSimulationPrototypePaths.PrototypeScene) == null)
            {
                errors.Add("Missing M06 prototype scene: " + VehicleSimulationPrototypePaths.PrototypeScene);
            }

            if (AssetDatabase.LoadMainAssetAtPath(VehicleSimulationPrototypePaths.PrototypeConfig) == null)
            {
                errors.Add("Missing M06 simulation config: " + VehicleSimulationPrototypePaths.PrototypeConfig);
            }
            else
            {
                ValidateConfig(errors);
            }

            if (AssetDatabase.LoadMainAssetAtPath(
                    VehicleSimulationPrototypePaths.PrototypeInputActions) == null)
            {
                errors.Add("Missing M06 input actions: " + VehicleSimulationPrototypePaths.PrototypeInputActions);
            }
            else
            {
                ValidateInputActions(errors);
            }
        }

        private static void ValidateConfig(ICollection<string> errors)
        {
            VehicleSimulationConfig config = AssetDatabase.LoadAssetAtPath<VehicleSimulationConfig>(
                VehicleSimulationPrototypePaths.PrototypeConfig);
            if (config == null)
            {
                errors.Add("M06 simulation config asset could not be loaded.");
                return;
            }

            if (!config.Validate(out string failure))
            {
                errors.Add("M06 simulation config is invalid: " + failure);
                return;
            }

            if (config.LeftDrivenWheelIndex != 0 || config.RightDrivenWheelIndex != 1)
            {
                errors.Add(
                    "The authored M06 target-vehicle config must map its configurable driven pair to FL/FR (0/1).");
            }

            if (!string.Equals(
                    config.DynamicTuning.Label,
                    VehicleSimulationConfig.PrototypeTuningLabel,
                    StringComparison.Ordinal) ||
                config.DynamicTuning.Classification !=
                VehicleReferenceClassification.RemakeDesignTarget ||
                !config.DynamicTuning.IsProvisional)
            {
                errors.Add(
                    "M06 dynamic values must remain classified as RemakeDesignTarget / ProvisionalProjectTuning.");
            }

            if (config.DynamicTuning.Classification ==
                    VehicleReferenceClassification.MeasuredDonorReference ||
                config.DynamicTuning.Classification ==
                    VehicleReferenceClassification.ObservedDonorReference)
            {
                errors.Add("M06 provisional dynamic tuning must not claim donor-measured provenance.");
            }

            if (config.GeometryReference.Classification !=
                    VehicleReferenceClassification.DerivedReference ||
                !string.Equals(
                    config.GeometryReference.Source,
                    "ReferenceCaptureDatabase/vehicle-body-wheel-geometry-v1",
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "M06 wheel-anchor provenance must point to the reviewed 04B.4 derived geometry fixture.");
            }

            VehicleSurfaceType[] requiredResponses =
            {
                VehicleSurfaceType.Unknown,
                VehicleSurfaceType.Paved,
                VehicleSurfaceType.Gravel,
                VehicleSurfaceType.Dirt,
                VehicleSurfaceType.Grass,
                VehicleSurfaceType.MudWet
            };
            VehicleSurfaceType[] authoredResponses = (config.SurfaceResponses ??
                    Array.Empty<VehicleSurfaceResponse>())
                .Select(response => response.SurfaceType)
                .OrderBy(value => value)
                .ToArray();
            VehicleSurfaceType[] expectedResponses = requiredResponses
                .OrderBy(value => value)
                .ToArray();
            if (!authoredResponses.SequenceEqual(expectedResponses))
            {
                errors.Add(
                    "M06 config must author exactly one Unknown/Paved/Gravel/Dirt/Grass/MudWet response.");
            }

            for (int index = 0; index < requiredResponses.Length; index++)
            {
                VehicleSurfaceResponse response = config.GetSurfaceResponse(requiredResponses[index]);
                if (response.SurfaceType != requiredResponses[index])
                {
                    errors.Add("M06 config is missing surface response: " + requiredResponses[index]);
                }
            }
        }

        private static void ValidateInputActions(ICollection<string> errors)
        {
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                VehicleSimulationPrototypePaths.PrototypeInputActions);
            InputActionMap map = input != null
                ? input.FindActionMap("Vehicle", throwIfNotFound: false)
                : null;
            if (map == null)
            {
                errors.Add("M06 input asset must expose the exact 'Vehicle' action map.");
                return;
            }

            string[] requiredActions =
            {
                "Throttle",
                "Brake",
                "Clutch",
                "Steering",
                "Ignition",
                "Starter",
                "GearUp",
                "GearDown",
                "Reset"
            };
            for (int index = 0; index < requiredActions.Length; index++)
            {
                if (map.FindAction(requiredActions[index], throwIfNotFound: false) == null)
                {
                    errors.Add("M06 input action is missing: Vehicle/" + requiredActions[index]);
                }
            }
        }

        private static void ValidateAssemblyFixture(Scene scene, ICollection<string> errors)
        {
            VehicleAssemblyController[] controllers =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleAssemblyController>(scene);
            if (controllers.Length != 1)
            {
                errors.Add(
                    $"M06 must own one separate VehicleAssemblyController fixture, found {controllers.Length}.");
                return;
            }

            VehicleAssemblyController controller = controllers[0];
            PrototypeAssemblyStateInitializer[] initializers =
                VehicleSimulationEditorUtility.FindAllInScene<PrototypeAssemblyStateInitializer>(scene);
            if (initializers.Length != 1)
            {
                errors.Add(
                    $"M06 assembly fixture requires one PrototypeAssemblyStateInitializer, found {initializers.Length}.");
            }
            else if (initializers[0].AssemblyController != controller)
            {
                errors.Add("M06 installed+tight initializer is not bound to its logical assembly controller.");
            }

            string[] actualIds = controller.Parts
                .Where(part => part != null && part.Definition != null)
                .Select(part => part.Definition.DefinitionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] expectedIds = RequiredAssemblyPartIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal))
            {
                errors.Add(
                    "M06 assembly fixture part IDs differ from the bounded prerequisite set. " +
                    "Expected: " + string.Join(", ", expectedIds) + "; actual: " +
                    string.Join(", ", actualIds));
            }

            int assemblyRoots = controller.Parts.Count(part => part != null && part.IsAssemblyRoot);
            if (assemblyRoots != 1)
            {
                errors.Add($"M06 assembly fixture requires one assembly root, found {assemblyRoots}.");
            }

            int wheels = controller.Parts.Count(part =>
                part != null && part.Definition != null &&
                part.Definition.DefinitionId.StartsWith("m06.wheel.", StringComparison.Ordinal));
            if (wheels != 4)
            {
                errors.Add($"M06 assembly fixture requires four logical wheels, found {wheels}.");
            }

            if (controller.Parts.Any(part =>
                    part != null && !part.IsAssemblyRoot &&
                    string.IsNullOrWhiteSpace(part.InitialMountId)))
            {
                errors.Add("Every non-root M06 prerequisite part must author an initial installed mount.");
            }

            if (controller.MountPoints.Length != RequiredAssemblyPartIds.Length - 1)
            {
                errors.Add(
                    $"M06 fixture requires {RequiredAssemblyPartIds.Length - 1} mounts, found {controller.MountPoints.Length}.");
            }

            for (int index = 0; index < controller.MountPoints.Length; index++)
            {
                MountPointAuthoring mount = controller.MountPoints[index];
                if (mount == null || mount.Definition == null)
                {
                    errors.Add("M06 assembly fixture contains a null mount/definition.");
                    continue;
                }

                FastenerDefinition[] fasteners = mount.Definition.Fasteners;
                if (fasteners.Length == 0 || fasteners.Any(fastener =>
                        fastener == null || !fastener.InsertedOnInstall || !fastener.RequiredForRemoval))
                {
                    errors.Add(
                        "M06 logical mount must own required insert-on-install fasteners: " + mount.MountId);
                }
            }

            IReadOnlyList<VehicleAssemblyValidationIssue> assemblyIssues =
                VehicleAssemblyValidator.Validate(
                    controller.Parts,
                    controller.MountPoints,
                    controller.Dependencies,
                    controller.Tools);
            foreach (VehicleAssemblyValidationIssue issue in assemblyIssues)
            {
                if (issue.Severity == VehicleAssemblyValidationSeverity.Error)
                {
                    errors.Add("M06 assembly " + issue.Code + ": " + issue.Message);
                }
            }
        }

        private static void ValidateSimulationComposition(Scene scene, ICollection<string> errors)
        {
            AssemblyVehiclePrerequisiteAdapter[] adapters =
                VehicleSimulationEditorUtility.FindAllInScene<AssemblyVehiclePrerequisiteAdapter>(scene);
            VehicleAssemblyController[] controllers =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleAssemblyController>(scene);
            if (adapters.Length != 1 || controllers.Length != 1 ||
                adapters.Length == 1 && adapters[0].AssemblyController != controllers[0])
            {
                errors.Add("M06 requires one assembly prerequisite adapter wired to its own fixture.");
            }

            VehicleInputRouter[] inputs =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleInputRouter>(scene);
            InputActionAsset expectedInput = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                VehicleSimulationPrototypePaths.PrototypeInputActions);
            if (inputs.Length != 1 || inputs[0].InputActions != expectedInput ||
                !string.Equals(inputs[0].ActionMapName, "Vehicle", StringComparison.Ordinal))
            {
                errors.Add("M06 requires one input router with the exact Vehicle action map asset.");
            }

            VehicleSimulationConfig expectedConfig =
                AssetDatabase.LoadAssetAtPath<VehicleSimulationConfig>(
                    VehicleSimulationPrototypePaths.PrototypeConfig);
            VehicleSimulationHost[] hosts =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleSimulationHost>(scene);
            PrototypeRaycastWheelPhysicsBackend[] backends =
                VehicleSimulationEditorUtility.FindAllInScene<PrototypeRaycastWheelPhysicsBackend>(scene);
            VehicleResetController[] resets =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleResetController>(scene);
            VehicleSimulationPresenter[] presenters =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleSimulationPresenter>(scene);
            VehicleTelemetryOverlay[] overlays =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleTelemetryOverlay>(scene);
            VehicleTelemetryRecorder[] recorders =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleTelemetryRecorder>(scene);
            VehicleAudioPresenter[] audioPresenters =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleAudioPresenter>(scene);
            UnityAudioBackend[] audioBackends =
                VehicleSimulationEditorUtility.FindAllInScene<UnityAudioBackend>(scene);
            if (hosts.Length != 1 || backends.Length != 1 || resets.Length != 1)
            {
                errors.Add(
                    $"M06 requires one host/backend/reset composition, found {hosts.Length}/{backends.Length}/{resets.Length}.");
            }
            else
            {
                VehicleSimulationHost host = hosts[0];
                PrototypeRaycastWheelPhysicsBackend backend = backends[0];
                VehicleResetController reset = resets[0];
                if (host.Config != expectedConfig || host.BackendComponent != backend ||
                    host.PrerequisiteSource != (adapters.Length == 1 ? adapters[0] : null) ||
                    host.InputSourceComponent != (inputs.Length == 1 ? inputs[0] : null))
                {
                    errors.Add("M06 host has an incorrect config/backend/prerequisite/input binding.");
                }

                if (backend.Config != expectedConfig || backend.Chassis == null ||
                    backend.WheelCount != 4)
                {
                    errors.Add("M06 raycast backend is not configured for one dynamic chassis and four wheels.");
                }

                if (backend.Chassis != null && backend.Chassis.isKinematic)
                {
                    errors.Add("M06 proxy chassis must remain a dynamic Rigidbody.");
                }

                VehicleAssemblyController logicalController =
                    controllers.Length == 1 ? controllers[0] : null;
                if (logicalController != null && backend.Chassis != null &&
                    logicalController.Parts.Any(part => part != null && part.Body == backend.Chassis))
                {
                    errors.Add("M06 dynamic proxy Rigidbody must be separate from the logical AssemblyGraph fixture.");
                }

                if (reset.Host != host || reset.Chassis != backend.Chassis || reset.ResetPose == null)
                {
                    errors.Add("M06 reset controller is not wired to the host, dynamic chassis and reset pose.");
                }

                Camera[] cameras = VehicleSimulationEditorUtility.FindAllInScene<Camera>(scene);
                VehiclePrototypeChaseCamera[] chaseCameras =
                    VehicleSimulationEditorUtility.FindAllInScene<VehiclePrototypeChaseCamera>(scene);
                if (cameras.Length != 1 || chaseCameras.Length != 1 ||
                    backend.Chassis == null ||
                    chaseCameras[0].Target != backend.Chassis.transform ||
                    chaseCameras[0].transform != cameras[0].transform ||
                    cameras[0].transform.IsChildOf(backend.Chassis.transform))
                {
                    errors.Add(
                        "M06 bounded route camera must be one detached world-space chase camera targeting the proxy chassis.");
                }

                if (presenters.Length != 1 || presenters[0].Host != host ||
                    presenters[0].Backend != backend || presenters[0].WheelVisuals.Length != 4 ||
                    presenters[0].WheelVisuals.Any(visual => visual == null))
                {
                    errors.Add("M06 presenter must bind the host/backend and four proxy wheel visuals.");
                }

                if (overlays.Length != 1 || overlays[0].Host != host ||
                    recorders.Length != 1 || recorders[0].Host != host ||
                    recorders[0].Capacity < 18000)
                {
                    errors.Add("M06 telemetry overlay/recorder wiring or fixed buffer capacity is invalid.");
                }

                if (audioPresenters.Length != 1 || audioBackends.Length != 1 ||
                    audioPresenters[0].SimulationHost != host ||
                    audioPresenters[0].BackendComponent != audioBackends[0] ||
                    backend.Chassis == null ||
                    !audioBackends[0].transform.IsChildOf(backend.Chassis.transform) ||
                    audioBackends[0].GetComponents<AudioSource>().Length != 0)
                {
                    errors.Add(
                        "M06 local diagnostic audio must use one typed presenter/backend, follow the proxy, and serialize no AudioSource or donor clip.");
                }

                RaycastWheelBinding[] wheels = backend.Wheels;
                if (wheels == null || wheels.Length != ExpectedWheelAnchors04B4.Length)
                {
                    errors.Add("M06 backend must expose four wheel bindings.");
                }
                else
                {
                    for (int index = 0; index < wheels.Length; index++)
                    {
                        if (wheels[index].SuspensionAnchor == null ||
                            Vector3.Distance(
                                wheels[index].SuspensionAnchor.localPosition,
                                ExpectedWheelAnchors04B4[index]) > 0.001f)
                        {
                            errors.Add($"M06 wheel anchor {index} differs from reviewed 04B.4 geometry.");
                        }
                    }
                }
            }

            VehicleSurfaceMetadataAuthoring[] surfaces =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleSurfaceMetadataAuthoring>(scene);
            VehicleSurfaceType[] actualSurfaceTypes = surfaces
                .Select(surface => surface.SurfaceType)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            VehicleSurfaceType[] expectedSurfaceTypes =
            {
                VehicleSurfaceType.Paved,
                VehicleSurfaceType.Gravel,
                VehicleSurfaceType.Dirt,
                VehicleSurfaceType.Grass
            };
            if (!actualSurfaceTypes.SequenceEqual(expectedSurfaceTypes))
            {
                errors.Add(
                    "M06 bounded route must author exact paved/gravel/dirt/grass surface metadata.");
            }

            if (VehicleSimulationEditorUtility.FindAllInScene<Camera>(scene).Length != 1)
            {
                errors.Add("M06 prototype requires exactly one dedicated camera.");
            }


            string[] sceneDependencies = AssetDatabase.GetDependencies(
                VehicleSimulationPrototypePaths.PrototypeScene,
                recursive: true);
            if (sceneDependencies.Any(path =>
                    AssetDatabase.LoadAssetAtPath<AudioClip>(path) != null))
            {
                errors.Add(
                    "M06 prototype scene must not serialize donor or project AudioClip dependencies; diagnostic clips load transiently from ignored staging.");
            }
        }
    }
}
