using System;
using System.Linq;
using MSC.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.World.Remaster.Editor
{
    public static class WorldPilotTraversalAuthoring
    {
        private const string RouteObjectName = "M05B1_M4CharacterControllerTraversal";

        [MenuItem("Tools/MSC Remake/World Validation/Author 05B.1 M4 Traversal Route")]
        public static void AuthorExistingPilotScene()
        {
            Scene scene = EditorSceneManager.OpenScene(
                WorldRemasterPaths.PilotPlaytestScene,
                OpenSceneMode.Single);
            WorldRemasterPilotMarker marker = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true))
                .Single(candidate => candidate.ZoneId == WorldRemasterPaths.PilotZoneId);
            FirstPersonMotor motor = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FirstPersonMotor>(true))
                .Single();
            WorldPilotTraversalRoute[] previousRoutes = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldPilotTraversalRoute>(true))
                .ToArray();
            foreach (WorldPilotTraversalRoute previousRoute in previousRoutes)
            {
                UnityEngine.Object.DestroyImmediate(previousRoute.gameObject);
            }

            GameObject sceneRoot = marker.transform.root.gameObject;
            WorldPilotTraversalRoute route = Attach(sceneRoot, marker.gameObject, motor.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, WorldRemasterPaths.PilotPlaytestScene);
            RefreshDependencyFingerprint(route);
            EditorSceneManager.SaveScene(scene, WorldRemasterPaths.PilotPlaytestScene);
            AssetDatabase.SaveAssets();
            Debug.Log("WORLD_05B1_TRAVERSAL_AUTHORING_OK");
        }

        public static void RunBatch()
        {
            AuthorExistingPilotScene();
        }

        public static WorldPilotTraversalRoute Attach(
            GameObject sceneRoot,
            GameObject productionRoot,
            GameObject playerRoot)
        {
            if (sceneRoot == null || productionRoot == null || playerRoot == null)
            {
                throw new ArgumentNullException("Traversal authoring requires scene, production and player roots.");
            }

            WorldRemasterPilotMarker marker = productionRoot.GetComponent<WorldRemasterPilotMarker>();
            FirstPersonMotor motor = playerRoot.GetComponent<FirstPersonMotor>();
            CharacterController controller = playerRoot.GetComponent<CharacterController>();
            if (marker == null || motor == null || controller == null)
            {
                throw new InvalidOperationException(
                    "05B.1 traversal requires the production pilot marker and the unchanged M4 motor/CharacterController.");
            }

            string[] requiredDoorNames = { "GarageDoorLeft", "GarageDoorRight", "HouseFrontDoor" };
            WorldHingedArchitecture[] availableDoors = productionRoot
                .GetComponentsInChildren<WorldHingedArchitecture>(true);
            WorldHingedArchitecture[] requiredDoors = requiredDoorNames
                .Select(name => availableDoors.SingleOrDefault(door => door.name == name) ??
                    throw new InvalidOperationException("05B.1 traversal door is missing: " + name))
                .ToArray();

            GameObject routeObject = new GameObject(RouteObjectName);
            routeObject.transform.SetParent(sceneRoot.transform, false);
            WorldPilotTraversalRoute route = routeObject.AddComponent<WorldPilotTraversalRoute>();
            route.Configure(
                WorldRemasterPaths.PilotZoneId,
                AssetDatabase.AssetPathToGUID(WorldRemasterPaths.PlayerPrefab),
                marker.transform,
                playerRoot.transform,
                controller,
                requiredDoors,
                new[]
                {
                    Waypoint("garage_apron", 0f, 0.15f, -2.2f, 0.28f),
                    Waypoint("garage_threshold", 0f, 0.15f, 0.9f, 0.24f),
                    Waypoint("garage_interior", 0f, 0.15f, 5.2f, 0.3f),
                    Waypoint("garage_apron_return", 0f, 0.15f, -2.2f, 0.3f),
                    Waypoint("house_approach", 14.95f, 0.18f, -2.2f, 0.35f),
                    Waypoint("house_step", 14.95f, 0.18f, -0.75f, 0.2f),
                    Waypoint("house_threshold", 14.95f, 0.18f, 0.65f, 0.24f, crouching: true),
                    Waypoint("house_entry_clear", 14.95f, 0.18f, 1.6f, 0.25f, crouching: true),
                    Waypoint("living_detour_in", 17f, 0.18f, 1.6f, 0.3f),
                    Waypoint("house_interior", 17f, 0.18f, 4.8f, 0.3f),
                    Waypoint("living_detour_return", 17f, 0.18f, 1.6f, 0.3f),
                    Waypoint("house_entry_return", 14.95f, 0.18f, 1.6f, 0.25f),
                    Waypoint("house_threshold_return", 14.95f, 0.18f, 0.65f, 0.24f, crouching: true),
                    Waypoint("house_step_return", 14.95f, 0.18f, -0.75f, 0.2f, crouching: true),
                    Waypoint("house_exit", 14.95f, 0.18f, -2.2f, 0.35f, crouching: true),
                    Waypoint("garage_return_start", 0f, 0.15f, -5.2f, 0.3f)
                });

            return route;
        }

        public static void RefreshDependencyFingerprint(WorldPilotTraversalRoute route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            WorldPilotDependencyFingerprint fingerprint = WorldPilotDependencyFingerprintUtility.CalculateCurrent();
            route.ConfigureDependencyFingerprintForAuthoring(fingerprint.Value);
            EditorUtility.SetDirty(route);
            EditorSceneManager.MarkSceneDirty(route.gameObject.scene);
        }

        private static WorldPilotTraversalWaypoint Waypoint(
            string id,
            float x,
            float y,
            float z,
            float tolerance,
            bool crouching = false) =>
            new WorldPilotTraversalWaypoint(
                id,
                new Vector3(x, y, z),
                tolerance,
                grounded: true,
                crouching: crouching);
    }
}
