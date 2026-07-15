using System.Collections;
using System.Linq;
using MSC.Player;
using MSC.Vehicle.Assembly;
using MSC.World.Remaster;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.WorldRemaster
{
    public sealed class WorldRemasterPlayModeTests
    {
        [UnityTest]
        public IEnumerator PilotPlaytest_LoadsProductionWorldAndPlayer()
        {
            yield return LoadSingle("WorldRemasterPilotPlaytest");
            WorldRemasterPilotMarker marker = Find<WorldRemasterPilotMarker>();
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.ZoneId, Is.EqualTo("cell_0_-3"));
            Assert.That(marker.DonorBinaryIndependent, Is.True);
            Assert.That(Find<CrossdotPresenter>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(renderer => renderer.gameObject.name.StartsWith("REF_")), Is.False);
        }

        [UnityTest]
        public IEnumerator ProductionCell_LoadsAndUnloadsAdditively()
        {
            yield return LoadSingle("WorldRemasterPilotPlaytest");
            AsyncOperation load = SceneManager.LoadSceneAsync("Production_cell_0_-3", LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            Scene cell = SceneManager.GetSceneByName("Production_cell_0_-3");
            Assert.That(cell.IsValid() && cell.isLoaded, Is.True);
            Assert.That(cell.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true)).Any(), Is.True);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(cell);
            Assert.That(unload, Is.Not.Null);
            yield return unload;
            Assert.That(SceneManager.GetSceneByName("Production_cell_0_-3").isLoaded, Is.False);
        }

        [UnityTest]
        public IEnumerator ComparisonModes_ToggleOnlyExplicitRoots()
        {
            GameObject host = new GameObject("ModeHost");
            GameObject production = new GameObject("Production");
            GameObject reference = new GameObject("Reference");
            production.transform.SetParent(host.transform);
            reference.transform.SetParent(host.transform);
            WorldRemasterModeController controller = host.AddComponent<WorldRemasterModeController>();
            controller.Configure(production, reference, WorldComparisonMode.ProductionOnly);
            Assert.That(production.activeSelf, Is.True);
            Assert.That(reference.activeSelf, Is.False);
            controller.ApplyMode(WorldComparisonMode.ReferenceOnly);
            Assert.That(production.activeSelf, Is.False);
            Assert.That(reference.activeSelf, Is.True);
            controller.ApplyMode(WorldComparisonMode.OverlayComparison);
            Assert.That(production.activeSelf, Is.True);
            Assert.That(reference.activeSelf, Is.True);
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MovingArchitecture_OpensGarageAndPreservesClearance()
        {
            yield return LoadSingle("WorldRemasterPilotPlaytest");
            WorldRemasterPilotMarker marker = Find<WorldRemasterPilotMarker>();
            WorldHingedArchitecture[] hinges = Object.FindObjectsByType<WorldHingedArchitecture>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            WorldHingedArchitecture left = hinges.Single(hinge => hinge.name == "GarageDoorLeft");
            WorldHingedArchitecture right = hinges.Single(hinge => hinge.name == "GarageDoorRight");
            left.SetOpen(true, immediate: true);
            right.SetOpen(true, immediate: true);
            yield return new WaitForFixedUpdate();
            Assert.That(left.OpenNormalized, Is.EqualTo(1f));
            Assert.That(right.OpenNormalized, Is.EqualTo(1f));

            Vector3 center = marker.transform.TransformPoint(new Vector3(0f, 1.15f, -0.45f));
            Collider[] overlaps = Physics.OverlapBox(
                center,
                new Vector3(1.1f, 0.8f, 0.24f),
                marker.transform.rotation,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            Assert.That(overlaps.Where(collider => collider.transform.IsChildOf(marker.transform)).Select(collider => collider.name), Is.Empty);
        }

        [UnityTest]
        public IEnumerator VehicleAssemblyScene_ContainsAssemblyAndPilotWorld()
        {
            yield return LoadSingle("VehicleAssemblyPrototype");
            VehicleAssemblyController controller = Find<VehicleAssemblyController>();
            WorldRemasterPilotMarker marker = Find<WorldRemasterPilotMarker>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(marker, Is.Not.Null);
            Assert.That(Vector3.Distance(controller.transform.root.position, marker.WorldGarageAnchor), Is.LessThan(0.01f));
            Assert.That(controller.transform.root.Cast<Transform>().Any(child => child.name == "AssemblyWorkshopFloor"), Is.False);
        }

        private static IEnumerator LoadSingle(string sceneName)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "Scene is missing from Build Settings: " + sceneName);
            yield return load;
        }

        private static T Find<T>() where T : Object =>
            Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }
}
