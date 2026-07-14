using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Editor.Validation;
using MSC.Interaction.Carrying;
using MSC.Interaction.Prototype;
using MSC.Interaction.Query;
using MSC.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MSC.Editor.PlayerInteraction
{
    public static class PlayerInteractionPrototypeValidator
    {
        private static readonly string[] RequiredActions =
        {
            "Move",
            "Look",
            "Crouch",
            "Interact",
            "Drop",
            "Place",
            "Throw",
            "RotateModifier",
            "ToolActivate"
        };

        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            ValidateRequiredAssets(errors);
            ValidateInputActions(errors);
            ValidatePlayerPrefab(errors);
            ValidatePrototypeScene(errors);
            ValidateBuildSettings(errors);

            IReadOnlyList<StableEntityIdIssue> identityIssues =
                StableEntityIdProjectValidator.ValidateProjectContent();
            foreach (StableEntityIdIssue issue in identityIssues)
            {
                string conflict = string.IsNullOrEmpty(issue.ConflictingContext)
                    ? string.Empty
                    : " conflicts with " + issue.ConflictingContext;
                errors.Add(
                    $"Stable ID {issue.Kind}: {issue.Context} value='{issue.SerializedId}'{conflict}");
            }

            return errors;
        }

        [MenuItem("Tools/My Summer Car/Milestone 4/Validate Player Interaction Prototype")]
        public static void ValidateMenu()
        {
            ThrowIfInvalid();
            Debug.Log("M4_PLAYER_INTERACTION_VALIDATION_OK");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Milestone 4 batch validation requires batch mode.");
            }

            ThrowIfInvalid();
            Debug.Log("M4_PLAYER_INTERACTION_VALIDATION_OK");
        }

        private static void ThrowIfInvalid()
        {
            IReadOnlyList<string> errors = Validate();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Milestone 4 validation failed:\n- " + string.Join("\n- ", errors));
            }
        }

        private static void ValidateRequiredAssets(List<string> errors)
        {
            string[] required =
            {
                PlayerInteractionPrototypePaths.InputActions,
                PlayerInteractionPrototypePaths.PlayerPrefab,
                PlayerInteractionPrototypePaths.DebugMaterial,
                PlayerInteractionPrototypePaths.PrototypeScene
            };

            foreach (string path in required)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    errors.Add("Missing required M4 asset: " + path);
                }
            }
        }

        private static void ValidateInputActions(List<string> errors)
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                PlayerInteractionPrototypePaths.InputActions);
            InputActionMap map = asset?.FindActionMap("Player", throwIfNotFound: false);
            if (map == null)
            {
                errors.Add("M4 InputActionAsset has no Player map.");
                return;
            }

            foreach (string actionName in RequiredActions)
            {
                InputAction action = map.FindAction(actionName, throwIfNotFound: false);
                if (action == null)
                {
                    errors.Add("M4 input action is missing: Player/" + actionName);
                }
                else if (action.bindings.Count == 0)
                {
                    errors.Add("M4 input action has no bindings: Player/" + actionName);
                }
            }
        }

        private static void ValidatePlayerPrefab(List<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerInteractionPrototypePaths.PlayerPrefab);
            if (prefab == null)
            {
                return;
            }

            RequireComponent<CharacterController>(prefab, errors);
            RequireComponent<FirstPersonMotor>(prefab, errors);
            RequireComponent<FirstPersonLook>(prefab, errors);
            RequireComponent<PhysicalCarryController>(prefab, errors);
            RequireComponent<RaycastInteractionCandidateSource>(prefab, errors);
            RequireComponent<PlayerInteractionController>(prefab, errors);
            RequireComponent<PlayerInputRouter>(prefab, errors);
            RequireComponent<InteractionDebugOverlay>(prefab, errors);

            PlayerInputRouter input = prefab.GetComponent<PlayerInputRouter>();
            if (input != null && input.InputActions == null)
            {
                errors.Add("Player prefab has no InputActionAsset reference.");
            }

            if (prefab.GetComponentInChildren<Camera>(true) == null)
            {
                errors.Add("Player prefab has no first-person camera.");
            }
        }

        private static void ValidatePrototypeScene(List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    PlayerInteractionPrototypePaths.PrototypeScene) == null)
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(
                PlayerInteractionPrototypePaths.PrototypeScene,
                OpenSceneMode.Single);
            if (FindInScene<PlayerInteractionPrototypeMarker>(scene) == null)
            {
                errors.Add("M4 scene has no PlayerInteractionPrototypeMarker.");
            }

            if (FindInScene<PlayerInputRouter>(scene) == null || FindInScene<Camera>(scene) == null)
            {
                errors.Add("M4 scene does not boot a configured first-person player and camera.");
            }

            PhysicsPickupTarget[] pickupTargets = FindAllInScene<PhysicsPickupTarget>(scene);
            if (pickupTargets.Length < 2)
            {
                errors.Add("M4 scene must contain light and heavy pickup targets.");
            }

            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PhysicsPickupTarget target in pickupTargets)
            {
                if (!target.StableId.IsValid)
                {
                    errors.Add("Pickup target has no valid stable ID: " + target.name);
                }
                else if (!stableIds.Add(target.StableId.Value))
                {
                    errors.Add("Pickup targets have duplicate stable ID: " + target.StableId.Value);
                }
            }

            if (FindInScene<ContextToggleTarget>(scene) == null)
            {
                errors.Add("M4 scene has no contextual interaction target.");
            }

            if (FindInScene<ToolActivationCounterTarget>(scene) == null)
            {
                errors.Add("M4 scene has no tool activation target.");
            }

            if (FindInScene<PrototypeMountHandoffTarget>(scene) == null)
            {
                errors.Add("M4 scene has no mount handoff boundary target.");
            }

            InteractionTargetHost[] hosts = FindAllInScene<InteractionTargetHost>(scene);
            if (hosts.Any(host => !host.HasCapabilities))
            {
                errors.Add("M4 scene contains an InteractionTargetHost with no explicit capabilities.");
            }
        }

        private static void ValidateBuildSettings(List<string> errors)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int bootstrapIndex = Array.FindIndex(
                scenes,
                scene => scene.enabled && scene.path == "Assets/Game/Bootstrap/Bootstrap.unity");
            int prototypeIndex = Array.FindIndex(
                scenes,
                scene => scene.enabled && scene.path == PlayerInteractionPrototypePaths.PrototypeScene);
            if (bootstrapIndex != 0)
            {
                errors.Add("Bootstrap must remain the first enabled build scene.");
            }

            if (prototypeIndex != 1)
            {
                errors.Add("M4 player interaction scene must be the second enabled build scene.");
            }
        }

        private static void RequireComponent<T>(GameObject prefab, List<string> errors)
            where T : Component
        {
            if (prefab.GetComponent<T>() == null)
            {
                errors.Add("Player prefab is missing component: " + typeof(T).Name);
            }
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
