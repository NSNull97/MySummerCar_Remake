using System;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Interaction.Prototype;
using MSC.Interaction.Query;
using MSC.Player;
using MSC.Presentation.InteractionOutline.EPO;
using EPOOutline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Editor.PlayerInteraction
{
    public static class PlayerInteractionPrototypeBuilder
    {
        private const string LightObjectStableId = "41c026f11b5a4c55a68db234ad475bc1";
        private const string HeavyObjectStableId = "9ab8e51f9e174e349926efb57f3481fc";

        [MenuItem("Tools/My Summer Car/Milestone 4/Build Player Interaction Prototype")]
        public static void Build()
        {
            EnsureParentDirectory(PlayerInteractionPrototypePaths.PlayerPrefab);
            EnsureParentDirectory(PlayerInteractionPrototypePaths.DebugMaterial);
            EnsureParentDirectory(PlayerInteractionPrototypePaths.PrototypeScene);

            AssetDatabase.ImportAsset(
                PlayerInteractionPrototypePaths.InputActions,
                ImportAssetOptions.ForceUpdate);
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                PlayerInteractionPrototypePaths.InputActions);
            if (inputActions == null)
            {
                throw new InvalidOperationException(
                    "Milestone 4 InputActionAsset could not be imported: " +
                    PlayerInteractionPrototypePaths.InputActions);
            }

            Material debugMaterial = BuildDebugMaterial();
            GameObject playerPrefab = BuildPlayerPrefab(
                inputActions);
            BuildPrototypeScene(playerPrefab, debugMaterial);
            EnsureSceneInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"M4_PLAYER_INTERACTION_BUILD_OK version={PlayerInteractionPrototypePaths.BuilderVersion}");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Milestone 4 batch build requires batch mode.");
            }

            Build();
        }

        private static Material BuildDebugMaterial()
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("HDRP/Lit shader is unavailable.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                PlayerInteractionPrototypePaths.DebugMaterial);
            if (material == null)
            {
                material = new Material(shader) { name = "M4_InteractionDebug" };
                AssetDatabase.CreateAsset(material, PlayerInteractionPrototypePaths.DebugMaterial);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", new Color(0.18f, 0.42f, 0.62f, 1f));
            material.SetFloat("_Metallic", 0.15f);
            material.SetFloat("_Smoothness", 0.32f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildPlayerPrefab(
            InputActionAsset inputActions)
        {
            var root = new GameObject("M4_FirstPersonPlayer");
            try
            {
                CharacterController characterController = root.AddComponent<CharacterController>();
                characterController.radius = 0.12f;
                characterController.height = 0.5f;
                characterController.center = new Vector3(0f, 0.25f, 0f);
                characterController.stepOffset = 0.4f;
                characterController.slopeLimit = 90f;
                characterController.skinWidth = 0.03f;
                characterController.minMoveDistance = 0f;

                GameObject leanPivotObject = new GameObject("LeanPivot");
                leanPivotObject.transform.SetParent(root.transform, false);
                leanPivotObject.transform.localPosition = new Vector3(0f, -0.3f, 0f);

                GameObject pivotObject = new GameObject("CameraPivot");
                pivotObject.transform.SetParent(leanPivotObject.transform, false);
                pivotObject.transform.localPosition = new Vector3(0f, 1.78f, 0f);

                GameObject impactPivotObject = new GameObject("ImpactPivot");
                impactPivotObject.transform.SetParent(pivotObject.transform, false);

                GameObject motionPivotObject = new GameObject("MotionPivot");
                motionPivotObject.transform.SetParent(
                    impactPivotObject.transform,
                    false);

                GameObject lookPitchPivotObject = new GameObject("LookPitchPivot");
                lookPitchPivotObject.transform.SetParent(
                    motionPivotObject.transform,
                    false);

                GameObject cameraObject = new GameObject("FirstPersonCamera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(
                    lookPitchPivotObject.transform,
                    false);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.03f;
                camera.farClipPlane = 500f;
                camera.fieldOfView =
                    FirstPersonCameraFieldOfView.HorizontalToVerticalDegrees(
                        120f,
                        16f / 9f);
                cameraObject.AddComponent<AudioListener>();
                FirstPersonCameraFieldOfView cameraFieldOfView =
                    cameraObject.AddComponent<FirstPersonCameraFieldOfView>();
                cameraFieldOfView.Configure(
                    camera,
                    horizontalFieldOfView: 120f,
                    configuredZoomMultiplier: 0.5f,
                    transitionSeconds: 0.08f);

                GameObject carryAnchorObject = new GameObject("CarryAnchor");
                carryAnchorObject.transform.SetParent(cameraObject.transform, false);
                carryAnchorObject.transform.localPosition = new Vector3(0f, -0.1f, 0.82f);

                FirstPersonMotor motor = root.AddComponent<FirstPersonMotor>();
                motor.Configure(
                    characterController,
                    leanPivotObject.transform,
                    pivotObject.transform,
                    impactPivotObject.transform);

                PlayerLeanImpactFeedbackPresenter impactFeedback =
                    root.AddComponent<PlayerLeanImpactFeedbackPresenter>();
                impactFeedback.Configure(motor);

                FirstPersonLook look = root.AddComponent<FirstPersonLook>();
                look.Configure(
                    root.transform,
                    lookPitchPivotObject.transform,
                    shouldLockCursor: true);

                FirstPersonCameraMotion cameraMotion =
                    motionPivotObject.AddComponent<FirstPersonCameraMotion>();
                cameraMotion.Configure(
                    motor,
                    look,
                    motionPivotObject.transform);

                PhysicalCarryController carry = root.AddComponent<PhysicalCarryController>();
                carry.Configure(carryAnchorObject.transform, characterController);

                RaycastInteractionCandidateSource query =
                    root.AddComponent<RaycastInteractionCandidateSource>();
                query.Configure(cameraObject.transform, 2.25f, ~0);

                PlayerInteractionController interaction = root.AddComponent<PlayerInteractionController>();
                interaction.Configure(query, carry, cameraObject.transform, ~0);

                InteractionOutlinePresenter outline =
                    root.AddComponent<InteractionOutlinePresenter>();
                outline.Configure(
                    interaction,
                    Color.white,
                    3f);

                Outlinable outlinable = root.AddComponent<Outlinable>();
                outlinable.enabled = false;
                EpoInteractionOutlineAdapter outlineAdapter =
                    root.AddComponent<EpoInteractionOutlineAdapter>();
                outlineAdapter.Configure(outline, outlinable);

                HdrpOutliner outliner =
                    cameraObject.AddComponent<HdrpOutliner>();
                outliner.PrimaryBufferSizeMode = BufferSizeMode.Native;
                outliner.PrimaryRendererScale = 1f;
                outliner.DilateIterations = 1;
                outliner.DilateQuality = DilateQuality.Base;
                outliner.DilateShift = 0.75f;
                outliner.BlurIterations = 0;
                outliner.BlurShift = 0f;
                outliner.RenderStage = RenderStage.AfterTransparents;

                var customPassObject = new GameObject(
                    "Interaction Outline Custom Pass");
                customPassObject.transform.SetParent(
                    cameraObject.transform,
                    worldPositionStays: false);
                CustomPassVolume customPassVolume =
                    customPassObject.AddComponent<CustomPassVolume>();
                customPassVolume.isGlobal = true;
                customPassVolume.targetCamera = camera;
                customPassVolume.injectionPoint =
                    CustomPassInjectionPoint.BeforePostProcess;
                customPassVolume.priority = 100f;
                customPassVolume.AddPassOfType<OutlineCustomPass>().name =
                    "EPO Interaction Outline";

                PlayerInputRouter input = root.AddComponent<PlayerInputRouter>();
                input.Configure(
                    inputActions,
                    motor,
                    look,
                    cameraFieldOfView,
                    interaction);

                InteractionDebugOverlay debugOverlay = root.AddComponent<InteractionDebugOverlay>();
                debugOverlay.Configure(interaction);

                CrossdotPresenter crossdot = root.AddComponent<CrossdotPresenter>();
                crossdot.Configure(
                    diameterPixels: 4f,
                    outlinePixels: 1f,
                    fillColor: Color.white,
                    borderColor: new Color(0f, 0f, 0f, 0.85f));

                PrefabUtility.SaveAsPrefabAsset(root, PlayerInteractionPrototypePaths.PlayerPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerInteractionPrototypePaths.PlayerPrefab);
            if (prefab == null)
            {
                throw new InvalidOperationException("Failed to create Milestone 4 player prefab.");
            }

            return prefab;
        }

        private static void BuildPrototypeScene(GameObject playerPrefab, Material debugMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject root = new GameObject("M4_PlayerInteractionPrototype");
            root.AddComponent<PlayerInteractionPrototypeMarker>();

            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetParent(root.transform);
            player.transform.position = new Vector3(0f, 0f, -4.5f);

            GameObject floor = CreatePrimitive(
                PrimitiveType.Cube,
                "Floor",
                root.transform,
                new Vector3(0f, -0.1f, 2f),
                new Vector3(12f, 0.2f, 14f),
                debugMaterial);
            floor.GetComponent<Renderer>().sharedMaterial = debugMaterial;

            CreatePrimitive(
                PrimitiveType.Cube,
                "Backstop",
                root.transform,
                new Vector3(0f, 1.5f, 8.5f),
                new Vector3(12f, 3f, 0.25f),
                debugMaterial);

            CreatePickupObject(
                "LightPickup_3kg",
                new Vector3(-1.15f, 0.35f, 0.7f),
                new Vector3(0.55f, 0.55f, 0.55f),
                3f,
                LightObjectStableId,
                root.transform,
                debugMaterial);
            CreatePickupObject(
                "HeavyPickup_18kg",
                new Vector3(1.2f, 0.45f, 1.25f),
                new Vector3(0.8f, 0.8f, 0.8f),
                18f,
                HeavyObjectStableId,
                root.transform,
                debugMaterial);
            CreateContextTarget(root.transform, debugMaterial);
            CreateToolTarget(root.transform, debugMaterial);
            CreateMountTarget(root.transform, debugMaterial);
            InstantiateLighting(scene, root.transform);

            EditorSceneManager.SaveScene(scene, PlayerInteractionPrototypePaths.PrototypeScene);
        }

        private static void CreatePickupObject(
            string name,
            Vector3 position,
            Vector3 scale,
            float massKilograms,
            string stableId,
            Transform parent,
            Material material)
        {
            GameObject item = CreatePrimitive(
                PrimitiveType.Cube,
                name,
                parent,
                position,
                scale,
                material);
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.mass = massKilograms;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            StableEntityIdAuthoring identity = item.AddComponent<StableEntityIdAuthoring>();
            SetStableId(identity, stableId);
            PhysicsPickupTarget pickupTarget = item.AddComponent<PhysicsPickupTarget>();
            pickupTarget.Configure(body, identity, $"Поднять ({massKilograms:0} кг)", 35f);
            InteractionTargetHost host = item.AddComponent<InteractionTargetHost>();
            host.Configure(pickupTarget);
        }

        private static void CreateContextTarget(Transform parent, Material material)
        {
            GameObject target = CreatePrimitive(
                PrimitiveType.Cube,
                "ContextToggleTarget",
                parent,
                new Vector3(2.5f, 0.75f, 3.2f),
                new Vector3(0.7f, 1.5f, 0.4f),
                material);
            ContextToggleTarget capability = target.AddComponent<ContextToggleTarget>();
            target.AddComponent<InteractionTargetHost>().Configure(capability);
        }

        private static void CreateToolTarget(Transform parent, Material material)
        {
            GameObject target = CreatePrimitive(
                PrimitiveType.Cylinder,
                "ToolActivationTarget",
                parent,
                new Vector3(0f, 0.6f, 4.2f),
                new Vector3(0.65f, 0.6f, 0.65f),
                material);
            ToolActivationCounterTarget capability = target.AddComponent<ToolActivationCounterTarget>();
            target.AddComponent<InteractionTargetHost>().Configure(capability);
        }

        private static void CreateMountTarget(Transform parent, Material material)
        {
            GameObject target = CreatePrimitive(
                PrimitiveType.Cube,
                "MountHandoffBoundary",
                parent,
                new Vector3(-2.5f, 0.5f, 3.5f),
                new Vector3(1.2f, 1f, 0.35f),
                material);

            GameObject pose = new GameObject("MountPose");
            pose.transform.SetParent(target.transform, false);
            pose.transform.localPosition = new Vector3(0f, 0.85f, -0.35f);

            PrototypeMountHandoffTarget capability = target.AddComponent<PrototypeMountHandoffTarget>();
            capability.Configure(pose.transform);
            target.AddComponent<InteractionTargetHost>().Configure(capability);
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            return gameObject;
        }

        private static void InstantiateLighting(Scene scene, Transform parent)
        {
            GameObject lightingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerInteractionPrototypePaths.NeutralLightingPrefab);
            if (lightingPrefab == null)
            {
                throw new InvalidOperationException(
                    "Milestone 3 neutral lighting prefab is required for the M4 prototype scene.");
            }

            GameObject lighting = (GameObject)PrefabUtility.InstantiatePrefab(lightingPrefab, scene);
            lighting.name = "M4_NeutralLighting";
            lighting.transform.SetParent(parent);
        }

        private static void EnsureSceneInBuildSettings()
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            var result = current
                .Where(scene => !string.Equals(
                    scene.path,
                    PlayerInteractionPrototypePaths.PrototypeScene,
                    StringComparison.Ordinal))
                .ToList();

            int insertIndex = result.Count > 0 ? 1 : 0;
            result.Insert(
                insertIndex,
                new EditorBuildSettingsScene(PlayerInteractionPrototypePaths.PrototypeScene, true));
            EditorBuildSettings.scenes = result.ToArray();
        }

        private static void SetStableId(StableEntityIdAuthoring authoring, string stableId)
        {
            if (!StableEntityId.TryParse(stableId, out _))
            {
                throw new InvalidOperationException("Builder stable ID is not canonical: " + stableId);
            }

            var serializedObject = new SerializedObject(authoring);
            SerializedProperty property = serializedObject.FindProperty("stableId");
            property.stringValue = stableId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureParentDirectory(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Asset path has no parent directory: " + assetPath);
            }

            string current = "Assets";
            string[] parts = directory.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
