using System;
using System.Collections;
using System.Linq;
using MSC.Core.Identity;
using MSC.Player;
using MSC.Vehicle.Assembly;
using MSC.World.Remaster;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.WorldRemaster
{
    public sealed class WorldRemasterPlayModeTests
    {
        private static readonly string[] PilotRuntimeStableIds =
        {
            "3be598c0aa9798dd8ab43e20f4a35e8f",
            "504a5620f62904b2d93d7803efc4eecf",
            "6f4c37ebb3d0b019392e9fe6a16da65d",
            "bb26b42e9fe463fd254bc0478a56946d",
            "c4bbad1ab8714ff807bee9195a77399a",
            "fc6a437b97ea997ca03a5e8bad1ba9b7",
            "ff8e5e6cb145461b84108d4f24eaee07"
        };

        private static readonly string[] NextZoneRuntimeStableIds =
        {
            "0b03b3508f6be68192698ac5f7ae7550",
            "2d9650c25f6324b6367493f48c56c908",
            "615645f7ce4d6ee6836547a00c3dedb9",
            "68783d0b852bcffb1eced37581f4aff8",
            "7201412942822b5b4724b5e72c74065f",
            "d7b7b8ce9d3e448a346b5344e18c2023",
            "dcd8f98c103fb4eb4640d2abb757d089",
            "dd8587032e6c12708151d60bd881aec6"
        };

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
            yield return LoadSingle("Bootstrap");
            yield return ValidateProductionCellLifecycle(
                "Production_cell_0_-3",
                "cell_0_-3",
                expectedMappedReferenceRecordCount: 24,
                expectedStableIds: PilotRuntimeStableIds);
        }

        [UnityTest]
        public IEnumerator Batch01Playtest_LoadsShorelinePlayerAndWalkablePier()
        {
            yield return LoadSingle("WorldRemasterHomeShorelinePlaytest");
            WorldRemasterPilotMarker marker = Object.FindObjectsByType<WorldRemasterPilotMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(candidate => candidate.ZoneId == "cell_0_-2");
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.ZoneId, Is.EqualTo("cell_0_-2"));
            Assert.That(marker.MappedReferenceRecordCount, Is.EqualTo(9));
            Assert.That(marker.TotalReferenceRecordCount, Is.EqualTo(15));
            Assert.That(marker.DonorBinaryIndependent, Is.True);
            Assert.That(Find<CrossdotPresenter>(), Is.Not.Null);

            Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(colliders.Count(collider => collider.name.StartsWith("DeckPlank_")), Is.EqualTo(14));
            Transform water = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(transform => transform.name == "BoundedLakeSurface");
            Assert.That(water.GetComponent<Collider>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator Batch01ProductionCell_LoadsAndUnloadsAdditively()
        {
            yield return LoadSingle("Bootstrap");
            yield return ValidateProductionCellLifecycle(
                "Production_cell_0_-2",
                "cell_0_-2",
                expectedMappedReferenceRecordCount: 9,
                expectedStableIds: NextZoneRuntimeStableIds);
        }

        [UnityTest]
        public IEnumerator Batch01HomeToPierSeam_HasContinuousWalkableCollision()
        {
            yield return LoadSingle("WorldRemasterHomeShorelinePlaytest");
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();

            float? previousHeight = null;
            for (float z = -976f; z <= -901f; z += 1f)
            {
                RaycastHit[] walkableHits = Physics.RaycastAll(
                        new Vector3(177.5f, 4f, z),
                        Vector3.down,
                        10f,
                        Physics.AllLayers,
                        QueryTriggerInteraction.Ignore)
                    .Where(hit => IsHomeToPierWalkable(hit.collider))
                    .OrderByDescending(hit => hit.point.y)
                    .ToArray();

                Assert.That(
                    walkableHits,
                    Is.Not.Empty,
                    $"Walkable collision is missing at the home-to-pier seam near world z={z:0.###}m.");

                float height = walkableHits[0].point.y;
                if (previousHeight.HasValue)
                {
                    Assert.That(
                        Mathf.Abs(height - previousHeight.Value),
                        Is.LessThanOrEqualTo(0.25f),
                        $"Walkable surface has an abrupt height step near world z={z:0.###}m.");
                }

                previousHeight = height;
            }
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

        private static IEnumerator ValidateProductionCellLifecycle(
            string sceneName,
            string expectedZoneId,
            int expectedMappedReferenceRecordCount,
            string[] expectedStableIds)
        {
            for (int cycle = 0; cycle < 2; cycle++)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                Assert.That(load, Is.Not.Null);
                yield return load;

                Scene cell = SceneManager.GetSceneByName(sceneName);
                Assert.That(cell.IsValid() && cell.isLoaded, Is.True);
                GameObject[] roots = cell.GetRootGameObjects();
                WorldRemasterPilotMarker[] markers = roots
                    .SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true))
                    .ToArray();
                Assert.That(markers.Count(marker => marker.ZoneId == expectedZoneId), Is.EqualTo(1));
                WorldRemasterPilotMarker marker = markers.Single(candidate => candidate.ZoneId == expectedZoneId);
                Assert.That(marker.MappedReferenceRecordCount, Is.EqualTo(expectedMappedReferenceRecordCount));

                string[] stableIds = roots
                    .SelectMany(root => root.GetComponentsInChildren<StableEntityIdAuthoring>(true))
                    .Select(identity => identity.SerializedId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                Assert.That(stableIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(stableIds.Length));
                Assert.That(
                    stableIds,
                    Is.EqualTo(expectedStableIds),
                    $"Stable-ID snapshot drifted for {sceneName} during lifecycle cycle {cycle + 1}.");

                AsyncOperation unload = SceneManager.UnloadSceneAsync(cell);
                Assert.That(unload, Is.Not.Null);
                yield return unload;
                yield return null;

                Assert.That(SceneManager.GetSceneByName(sceneName).isLoaded, Is.False);
                Assert.That(roots.All(root => root == null), Is.True, "Production-cell roots survived unload.");
            }
        }

        private static bool IsHomeToPierWalkable(Collider collider) =>
            collider.name == "Terrain" ||
            collider.name == "ShoreApproach" ||
            collider.name == "FootpathToPier" ||
            collider.name == "ShoreThreshold" ||
            collider.name.StartsWith("DeckPlank_");

        private static T Find<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }
}
