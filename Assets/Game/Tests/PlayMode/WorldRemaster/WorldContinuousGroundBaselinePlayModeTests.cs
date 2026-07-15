using System.Collections;
using System.Linq;
using MSC.World.Remaster;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace MSC.Tests.PlayMode.WorldRemaster
{
    public sealed class WorldContinuousGroundBaselinePlayModeTests
    {
        private static readonly VoidFillSceneCase[] SceneCases =
        {
            new VoidFillSceneCase(
                "Assets/Game/World/Generated/VoidFillCells/VoidFill_cell_-4_0.unity",
                "piece_cell_-4_0",
                "cell_-4_0",
                new Vector3(-1600f, 20f, 160f)),
            new VoidFillSceneCase(
                "Assets/Game/World/Generated/VoidFillCells/VoidFill_cell_-3_0.unity",
                "piece_cell_-3_0",
                "cell_-3_0",
                new Vector3(-1495f, 20f, 160f))
        };

        [UnityTest]
        public IEnumerator GeneratedVoidFillScenes_LoadMarkerAndStaticMeshCollider()
        {
            foreach (VoidFillSceneCase sceneCase in SceneCases)
            {
                yield return LoadSingle(sceneCase.ScenePath);
                WorldVoidFillMarker marker = Object.FindObjectsByType<WorldVoidFillMarker>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .SingleOrDefault(candidate => candidate.PieceId == sceneCase.PieceId);

                Assert.That(marker, Is.Not.Null, "Void-fill marker is missing in " + sceneCase.ScenePath);
                Assert.That(marker.RegionId, Is.EqualTo("M05C1-VOID-TEIMO-001"));
                Assert.That(marker.CellId, Is.EqualTo(sceneCase.CellId));
                Assert.That(marker.Classification, Is.EqualTo(WorldVoidRegionClassification.IntentionalDonorVoid));
                Assert.That(marker.SafetyTopologyBaseline, Is.True);
                Assert.That(marker.OpensGameplayArea, Is.False);

                MeshCollider meshCollider = marker.GetComponent<MeshCollider>();
                Assert.That(meshCollider, Is.Not.Null);
                Assert.That(meshCollider.sharedMesh, Is.Not.Null);
                Assert.That(meshCollider.convex, Is.False);

                Physics.SyncTransforms();
                Assert.That(
                    Physics.Raycast(
                        sceneCase.RayOrigin,
                        Vector3.down,
                        out RaycastHit hit,
                        40f,
                        Physics.AllLayers,
                        QueryTriggerInteraction.Ignore),
                    Is.True,
                    "Void-fill MeshCollider is not raycastable in " + sceneCase.ScenePath);
                Assert.That(hit.collider, Is.EqualTo(meshCollider));
                Assert.That(float.IsFinite(hit.point.y), Is.True);

                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                try
                {
                    sphere.name = "05C1_GroundBaselineProbe";
                    sphere.transform.position = hit.point + Vector3.up * 6f;
                    Rigidbody body = sphere.AddComponent<Rigidbody>();
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    Physics.SyncTransforms();

                    for (int step = 0; step < 120; step++)
                    {
                        yield return new WaitForFixedUpdate();
                    }

                    Assert.That(body.position.y, Is.GreaterThan(hit.point.y + 0.2f),
                        "Dynamic probe fell through the void-fill surface in " + sceneCase.ScenePath);
                    Assert.That(body.position.y, Is.LessThan(hit.point.y + 1.5f),
                        "Dynamic probe did not settle on the void-fill surface in " + sceneCase.ScenePath);
                    Assert.That(Mathf.Abs(body.linearVelocity.y), Is.LessThan(0.25f));
                }
                finally
                {
                    Object.Destroy(sphere);
                }
            }
        }

        private static IEnumerator LoadSingle(string scenePath)
        {
#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                scenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(load, Is.Not.Null, "Generated scene cannot be loaded by asset path: " + scenePath);
            yield return load;
            yield return new WaitForFixedUpdate();
#else
            Assert.Ignore("05C1 generated scenes are intentionally excluded from Build Settings before manual sign-off.");
            yield break;
#endif
        }

        private readonly struct VoidFillSceneCase
        {
            public VoidFillSceneCase(string scenePath, string pieceId, string cellId, Vector3 rayOrigin)
            {
                ScenePath = scenePath;
                PieceId = pieceId;
                CellId = cellId;
                RayOrigin = rayOrigin;
            }

            public string ScenePath { get; }
            public string PieceId { get; }
            public string CellId { get; }
            public Vector3 RayOrigin { get; }
        }
    }
}
