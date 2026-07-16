using System;
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
    public static class VehicleSimulationPrototypeBuilder
    {
        private static readonly string[] WheelIds =
        {
            "m06.wheel.fl",
            "m06.wheel.fr",
            "m06.wheel.rl",
            "m06.wheel.rr"
        };

        private static readonly Vector3[] WheelAnchors04B4 =
        {
            new Vector3(-0.6299995f, 0f, 1.1669996f),
            new Vector3(0.6300007f, 0f, 1.1669996f),
            new Vector3(-0.6029993f, 0f, -1.167f),
            new Vector3(0.603001f, 0f, -1.1669996f)
        };

        [MenuItem(VehicleSimulationPrototypePaths.MenuRoot + "Build / Rebuild Prototype")]
        public static void Build()
        {
            EnsureFolders();
            VehicleSimulationConfig config = BuildConfig();
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                VehicleSimulationPrototypePaths.PrototypeInputActions);
            if (input == null)
            {
                throw new InvalidOperationException(
                    "M06 requires the dedicated Input System asset: " +
                    VehicleSimulationPrototypePaths.PrototypeInputActions);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("M06_VehicleSimulationPrototype");
            VehicleSimulationTrackBuildResult track =
                VehicleSimulationTrackBuilder.Build(root.transform, config);
            VehicleAssemblyController assembly =
                VehicleSimulationAssemblyFixtureBuilder.Build(root.transform);
            AssemblyVehiclePrerequisiteAdapter prerequisites =
                assembly.gameObject.AddComponent<AssemblyVehiclePrerequisiteAdapter>();
            prerequisites.Configure(assembly);

            BuildDynamicProxy(
                root.transform,
                track.ResetPose,
                config,
                input,
                prerequisites,
                out VehicleSimulationHost host,
                out Transform[] wheelVisuals);
            BuildPresentation(host, wheelVisuals);
            BuildDiagnosticAudio(host);
            BuildLightingAndCamera(root.transform, host.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, VehicleSimulationPrototypePaths.PrototypeScene))
            {
                throw new InvalidOperationException("Failed to save the M06 prototype scene.");
            }

            VehicleSimulationEditorUtility.AppendPrototypeSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"M06_VEHICLE_SIMULATION_BUILD_OK version={VehicleSimulationPrototypePaths.BuilderVersion} " +
                $"scene={VehicleSimulationPrototypePaths.PrototypeScene} buildIndex=9 wheels=4");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("M06 builder batch entry requires batch mode.");
            }

            Build();
        }

        [MenuItem(VehicleSimulationPrototypePaths.MenuRoot + "Spawn/Reset Prototype")]
        public static void SpawnOrResetPrototype()
        {
            if (!string.Equals(
                    SceneManager.GetActiveScene().path,
                    VehicleSimulationPrototypePaths.PrototypeScene,
                    StringComparison.Ordinal))
            {
                EditorSceneManager.OpenScene(
                    VehicleSimulationPrototypePaths.PrototypeScene,
                    OpenSceneMode.Single);
            }

            VehicleSimulationHost[] hosts =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleSimulationHost>(
                    SceneManager.GetActiveScene());
            if (hosts.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one M06 VehicleSimulationHost, found {hosts.Length}.");
            }

            if (EditorApplication.isPlaying)
            {
                hosts[0].ResetToSpawn();
            }
            else
            {
                Selection.activeGameObject = hosts[0].gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
                Debug.Log("M06 prototype opened. Enter Play Mode, then use Spawn/Reset Prototype to reset physics.");
            }
        }

        private static VehicleSimulationConfig BuildConfig()
        {
            VehicleSimulationConfig config = AssetDatabase.LoadAssetAtPath<VehicleSimulationConfig>(
                VehicleSimulationPrototypePaths.PrototypeConfig);
            if (config == null)
            {
                config = VehicleSimulationEditorUtility.LoadOrCreate<VehicleSimulationConfig>(
                    VehicleSimulationPrototypePaths.PrototypeConfig);
                config.ApplyProvisionalPrototypeDefaults();
            }
            else
            {
                config.MigrateLegacyCompositionTuning();
            }

            if (!config.Validate(out string failure))
            {
                throw new InvalidOperationException(
                    "M06 config is invalid. Use an explicit tuning migration/reset instead of " +
                    "silently replacing calibration values: " + failure);
            }

            EditorUtility.SetDirty(config);
            return config;
        }

        private static void BuildDynamicProxy(
            Transform parent,
            Transform resetPose,
            VehicleSimulationConfig config,
            InputActionAsset input,
            AssemblyVehiclePrerequisiteAdapter prerequisites,
            out VehicleSimulationHost host,
            out Transform[] wheelVisuals)
        {
            GameObject proxy = new GameObject("M06_DynamicProxyVehicle");
            proxy.transform.SetParent(parent, false);
            proxy.transform.SetPositionAndRotation(resetPose.position, resetPose.rotation);

            Rigidbody chassis = proxy.AddComponent<Rigidbody>();
            chassis.mass = config.Dynamics.ProvisionalMassKilograms;
            chassis.centerOfMass = config.Dynamics.CenterOfMassMeters;
            chassis.interpolation = RigidbodyInterpolation.Interpolate;
            chassis.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            chassis.linearDamping = config.Dynamics.ChassisLinearDamping;
            chassis.angularDamping = config.Dynamics.ChassisAngularDamping;
            BoxCollider chassisCollider = proxy.AddComponent<BoxCollider>();
            chassisCollider.center = config.Dynamics.ChassisColliderCenterMeters;
            chassisCollider.size = config.Dynamics.ChassisColliderSizeMeters;

            Material bodyMaterial = BuildMaterial("M06_ProxyBody", new Color(0.5f, 0.12f, 0.08f));
            Material tireMaterial = BuildMaterial("M06_ProxyTire", new Color(0.035f, 0.04f, 0.045f));
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ProxyBodyVisual";
            body.transform.SetParent(proxy.transform, false);
            body.transform.localPosition = config.Dynamics.ChassisColliderCenterMeters;
            body.transform.localScale = config.Dynamics.ChassisColliderSizeMeters -
                                        new Vector3(0.03f, 0.04f, 0.05f);
            body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

            var bindings = new RaycastWheelBinding[WheelIds.Length];
            wheelVisuals = new Transform[WheelIds.Length];
            for (int index = 0; index < WheelIds.Length; index++)
            {
                Transform anchor = new GameObject("Anchor_" + WheelIds[index]).transform;
                anchor.SetParent(proxy.transform, false);
                anchor.localPosition = WheelAnchors04B4[index];
                bindings[index] = new RaycastWheelBinding(WheelIds[index], anchor);

                GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "Visual_" + WheelIds[index];
                wheel.transform.SetParent(proxy.transform, false);
                wheel.transform.localPosition = WheelAnchors04B4[index] + Vector3.down * 0.3f;
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                float diameter = config.Dynamics.WheelRadiusMeters * 2f;
                wheel.transform.localScale = new Vector3(diameter, 0.18f, diameter);
                wheel.GetComponent<Renderer>().sharedMaterial = tireMaterial;
                UnityEngine.Object.DestroyImmediate(wheel.GetComponent<Collider>());
                wheelVisuals[index] = wheel.transform;
            }

            PrototypeRaycastWheelPhysicsBackend backend =
                proxy.AddComponent<PrototypeRaycastWheelPhysicsBackend>();
            backend.Configure(chassis, config, bindings);
            VehicleInputRouter inputRouter = proxy.AddComponent<VehicleInputRouter>();
            inputRouter.Configure(
                input,
                "Vehicle",
                config.Gearbox.ForwardGearCount,
                initialIgnition: false);
            host = proxy.AddComponent<VehicleSimulationHost>();
            host.Configure(config, backend, prerequisites, inputRouter);
            VehicleResetController reset = proxy.AddComponent<VehicleResetController>();
            reset.Configure(host, chassis, resetPose);
            host.ConfigureResetController(reset);
        }

        private static void BuildPresentation(VehicleSimulationHost host, Transform[] wheelVisuals)
        {
            PrototypeRaycastWheelPhysicsBackend backend =
                host.BackendComponent as PrototypeRaycastWheelPhysicsBackend;
            if (backend == null)
            {
                throw new InvalidOperationException("M06 host backend is not the prototype raycast backend.");
            }

            VehicleSimulationPresenter presenter =
                host.gameObject.AddComponent<VehicleSimulationPresenter>();
            presenter.Configure(host, backend, wheelVisuals);
            VehicleTelemetryOverlay overlay = host.gameObject.AddComponent<VehicleTelemetryOverlay>();
            overlay.Configure(host, initiallyVisible: true);
            VehicleTelemetryRecorder recorder = host.gameObject.AddComponent<VehicleTelemetryRecorder>();
            recorder.Configure(
                host,
                frameCapacity: 18000,
                targetExportPath:
                    VehicleSimulationPrototypePaths.TelemetryDirectory + "/VehicleTelemetry.csv");
        }

        private static void BuildDiagnosticAudio(VehicleSimulationHost host)
        {
            GameObject audioObject = new GameObject("M06_LocalDiagnosticVehicleAudio");
            audioObject.transform.SetParent(host.transform, false);
            UnityAudioBackend backend = audioObject.AddComponent<UnityAudioBackend>();
            VehicleAudioPresenter presenter = host.gameObject.AddComponent<VehicleAudioPresenter>();
            presenter.Configure(host, backend);
        }

        private static void BuildLightingAndCamera(Transform sceneRoot, Transform proxy)
        {
            GameObject sun = new GameObject("M06_DirectionalLight");
            sun.transform.SetParent(sceneRoot, false);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 100000f;
            light.shadows = LightShadows.Soft;

            GameObject cameraObject = new GameObject("M06_PrototypeCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(sceneRoot, false);
            VehiclePrototypeChaseCamera chaseCamera =
                cameraObject.AddComponent<VehiclePrototypeChaseCamera>();
            chaseCamera.Configure(
                proxy,
                new Vector3(0f, 2.7f, -5.5f),
                new Vector3(0f, 0.5f, 2.5f));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();
        }

        private static Material BuildMaterial(string name, Color color)
        {
            string path = VehicleSimulationPrototypePaths.Materials + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException("No compatible shader exists for M06 graybox materials.");
                }

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolders()
        {
            VehicleSimulationEditorUtility.EnsureFolder(VehicleSimulationPrototypePaths.Configurations);
            VehicleSimulationEditorUtility.EnsureFolder(VehicleSimulationPrototypePaths.Input);
            VehicleSimulationEditorUtility.EnsureFolder(VehicleSimulationPrototypePaths.Materials);
            VehicleSimulationEditorUtility.EnsureFolder(VehicleSimulationPrototypePaths.Scenes);
        }
    }
}
