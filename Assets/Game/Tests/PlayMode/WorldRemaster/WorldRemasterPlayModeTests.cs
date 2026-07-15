using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Bootstrap;
using MSC.Core.Identity;
using MSC.Player;
using MSC.Vehicle.Assembly;
using MSC.World.Remaster;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.WorldRemaster
{
    public sealed class WorldRemasterPlayModeTests
    {
        private const int StreamingEvidenceSchemaVersion = 1;
        private const string StreamingEvidenceValidatorId = "production-streaming-lifecycle";
        private const string StreamingEvidenceRelativePath =
            "Docs/WorldValidation/M05B1_PRODUCTION_STREAMING_LIFECYCLE.json";
        private const string StreamingManifestPath =
            "Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset";
        private const string BootstrapScenePath = "Assets/Game/Bootstrap/Bootstrap.unity";

        private static readonly string[] StreamingImplementationPaths =
        {
            "Assets/Game/World/Runtime/Partition/WorldPartition.cs",
            "Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingManifest.cs",
            "Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingService.cs",
            "Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs",
            "Assets/Game/Bootstrap/GameServiceBindings.cs",
            "Assets/Game/Editor/WorldStreaming/ProductionWorldStreamingBuilder.cs",
            "Assets/Game/Editor/WorldStreaming/WorldPilotGateRemediationValidator.cs",
            "Assets/Game/World/Editor/ProductionWorldStreamingLifecycleEvidenceReader.cs",
            "Assets/Game/World/Editor/WorldValidationRunner.cs",
            "Assets/Game/Tests/PlayMode/WorldRemaster/WorldRemasterPlayModeTests.cs"
        };

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
        public IEnumerator ProductionStreaming_PilotCellOwnedLifecycleIsRepeatable()
        {
            ProductionWorldStreamingService service = null;
            ProductionWorldStreamingInstaller installer = null;
            yield return LoadProductionStreamingSession(
                resolvedService => service = resolvedService,
                resolvedInstaller => installer = resolvedInstaller);

            service.enabled = false;
            DisablePlayerInputAndMotor(installer.SpawnedPlayer);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                if (!service.IsCellLoaded("cell_0_-3"))
                {
                    yield return service.RefreshNow();
                }

                GameObject[] roots = ValidateLoadedProductionCell(
                    service,
                    "cell_0_-3",
                    expectedMappedReferenceRecordCount: 24,
                    expectedStableIds: PilotRuntimeStableIds,
                    cycle: cycle);
                Assert.That(service.OwnedLoadedSceneCount, Is.EqualTo(1));

                yield return service.UnloadOwnedScenes();
                yield return null;

                Assert.That(service.IsCellLoaded("cell_0_-3"), Is.False);
                Assert.That(service.OwnedLoadedSceneCount, Is.Zero);
                Assert.That(roots.All(root => root == null), Is.True, "Owned pilot-cell roots survived unload.");
            }
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
        public IEnumerator ProductionStreaming_CellTransitionAndOwnedUnloadAreRepeatable()
        {
            DeletePreviousStreamingLifecycleEvidence();
            ProductionWorldStreamingService service = null;
            ProductionWorldStreamingInstaller installer = null;
            yield return LoadProductionStreamingSession(
                resolvedService => service = resolvedService,
                resolvedInstaller => installer = resolvedInstaller);

            service.enabled = false;
            DisablePlayerInputAndMotor(installer.SpawnedPlayer);
            var completedSequence = new string[8];
            var ownedSceneCounts = new int[8];
            var pilotRootCleanup = new bool[2];
            var nextRootCleanup = new bool[2];
            for (int cycle = 0; cycle < 2; cycle++)
            {
                int evidenceOffset = cycle * 4;
                service.Focus.position = new Vector3(153.495f, 10f, -1280f);
                yield return service.RefreshNow();
                GameObject[] pilotRoots = ValidateLoadedProductionCell(
                    service,
                    "cell_0_-3",
                    expectedMappedReferenceRecordCount: 24,
                    expectedStableIds: PilotRuntimeStableIds,
                    cycle: cycle);
                Assert.That(service.IsCellLoaded("cell_0_-2"), Is.False);
                Assert.That(service.OwnedLoadedSceneCount, Is.EqualTo(1));
                completedSequence[evidenceOffset] = $"cycle-{cycle + 1}:pilot";
                ownedSceneCounts[evidenceOffset] = service.OwnedLoadedSceneCount;

                service.Focus.position = new Vector3(153.495f, 10f, -800f);
                yield return service.RefreshNow();
                GameObject[] nextRoots = ValidateLoadedProductionCell(
                    service,
                    "cell_0_-2",
                    expectedMappedReferenceRecordCount: 9,
                    expectedStableIds: NextZoneRuntimeStableIds,
                    cycle: cycle);
                Assert.That(service.IsCellLoaded("cell_0_-3"), Is.True);
                Assert.That(service.OwnedLoadedSceneCount, Is.EqualTo(2));
                completedSequence[evidenceOffset + 1] = $"cycle-{cycle + 1}:pilot+next";
                ownedSceneCounts[evidenceOffset + 1] = service.OwnedLoadedSceneCount;

                service.Focus.position = new Vector3(153.495f, 10f, -200f);
                yield return service.RefreshNow();
                yield return null;
                Assert.That(service.IsCellLoaded("cell_0_-3"), Is.False);
                Assert.That(service.IsCellLoaded("cell_0_-2"), Is.True);
                Assert.That(service.OwnedLoadedSceneCount, Is.EqualTo(1));
                pilotRootCleanup[cycle] = pilotRoots.All(root => root == null);
                Assert.That(pilotRootCleanup[cycle], Is.True, "Pilot-cell roots survived the owned transition unload.");
                completedSequence[evidenceOffset + 2] = $"cycle-{cycle + 1}:next";
                ownedSceneCounts[evidenceOffset + 2] = service.OwnedLoadedSceneCount;

                service.Focus.position = new Vector3(153.495f, 10f, -1800f);
                yield return service.RefreshNow();
                yield return null;
                Assert.That(service.IsCellLoaded("cell_0_-3"), Is.False);
                Assert.That(service.IsCellLoaded("cell_0_-2"), Is.False);
                Assert.That(service.OwnedLoadedSceneCount, Is.Zero);
                nextRootCleanup[cycle] = nextRoots.All(root => root == null);
                Assert.That(nextRootCleanup[cycle], Is.True, "Next-cell roots survived the owned transition unload.");
                completedSequence[evidenceOffset + 3] = $"cycle-{cycle + 1}:none";
                ownedSceneCounts[evidenceOffset + 3] = service.OwnedLoadedSceneCount;
            }

            WritePassingStreamingLifecycleEvidence(
                completedSequence,
                ownedSceneCounts,
                pilotRootCleanup,
                nextRootCleanup);
        }

        [UnityTearDown]
        public IEnumerator CleanupProductionStreamingSession()
        {
            ProductionWorldStreamingService[] services = Object.FindObjectsByType<ProductionWorldStreamingService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (ProductionWorldStreamingService service in services)
            {
                service.enabled = false;
                int timeoutFrames = 120;
                while (service.IsStreaming && timeoutFrames-- > 0)
                {
                    yield return null;
                }

                if (!service.IsStreaming)
                {
                    yield return service.UnloadOwnedScenes();
                }
            }

            foreach (GameCompositionRoot root in Object.FindObjectsByType<GameCompositionRoot>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                Object.Destroy(root.gameObject);
            }

            yield return null;

            for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded || !IsProductionCellPath(scene.path))
                {
                    continue;
                }

                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    yield return unload;
                }
            }
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

        private static IEnumerator LoadProductionStreamingSession(
            Action<ProductionWorldStreamingService> setService,
            Action<ProductionWorldStreamingInstaller> setInstaller)
        {
            yield return LoadSingle("Bootstrap");
            ProductionWorldStreamingInstaller installer = Find<ProductionWorldStreamingInstaller>();
            ProductionWorldStreamingService service = Find<ProductionWorldStreamingService>();
            Assert.That(installer, Is.Not.Null);
            Assert.That(service, Is.Not.Null);

            int timeoutFrames = 300;
            while ((!installer.IsReady || service.IsStreaming || !service.IsCellLoaded("cell_0_-3")) &&
                   timeoutFrames-- > 0)
            {
                yield return null;
            }

            Assert.That(installer.IsReady, Is.True, "Bootstrap installer did not finish its initial production-cell refresh.");
            Assert.That(service.HasFocus, Is.True);
            Assert.That(service.Focus, Is.SameAs(installer.SpawnedPlayer.transform));
            Assert.That(service.IsCellLoaded("cell_0_-3"), Is.True);
            setService(service);
            setInstaller(installer);
        }

        private static GameObject[] ValidateLoadedProductionCell(
            ProductionWorldStreamingService service,
            string expectedZoneId,
            int expectedMappedReferenceRecordCount,
            string[] expectedStableIds,
            int cycle)
        {
            Assert.That(service.Manifest, Is.Not.Null);
            Assert.That(service.Manifest.TryGetCell(expectedZoneId, out ProductionWorldCellScene entry), Is.True);
            Scene cell = GetLoadedSceneByBuildIndex(entry.BuildIndex);
            Assert.That(cell.IsValid() && cell.isLoaded, Is.True);
            Assert.That(cell.path, Is.EqualTo(entry.ScenePath));
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
                $"Stable-ID snapshot drifted for {entry.ScenePath} during lifecycle cycle {cycle + 1}.");
            return roots;
        }

        private static Scene GetLoadedSceneByBuildIndex(int buildIndex)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.buildIndex == buildIndex && scene.isLoaded)
                {
                    return scene;
                }
            }

            return default;
        }

        private static void DisablePlayerInputAndMotor(GameObject player)
        {
            FirstPersonMotor motor = player.GetComponent<FirstPersonMotor>();
            PlayerInputRouter input = player.GetComponent<PlayerInputRouter>();
            if (motor != null)
            {
                motor.enabled = false;
            }

            if (input != null)
            {
                input.enabled = false;
            }
        }

        private static bool IsProductionCellPath(string scenePath) =>
            string.Equals(
                scenePath,
                "Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity",
                StringComparison.Ordinal) ||
            string.Equals(
                scenePath,
                "Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity",
                StringComparison.Ordinal);

        private static void DeletePreviousStreamingLifecycleEvidence()
        {
            string path = GetStreamingEvidenceAbsolutePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void WritePassingStreamingLifecycleEvidence(
            string[] completedSequence,
            int[] ownedSceneCounts,
            bool[] pilotRootCleanup,
            bool[] nextRootCleanup)
        {
            string projectRoot = GetProjectRoot();
            var evidence = new ProductionStreamingLifecycleEvidenceDto
            {
                schemaVersion = StreamingEvidenceSchemaVersion,
                validatorId = StreamingEvidenceValidatorId,
                passed = true,
                unityVersion = Application.unityVersion,
                completedCycles = 2,
                completedSequence = completedSequence.ToArray(),
                ownedSceneCounts = ownedSceneCounts.ToArray(),
                pilotRootsDestroyed = pilotRootCleanup.ToArray(),
                nextRootsDestroyed = nextRootCleanup.ToArray(),
                stableIdsUnique = true,
                pilotStableIds = PilotRuntimeStableIds.ToArray(),
                nextZoneStableIds = NextZoneRuntimeStableIds.ToArray(),
                manifestFingerprint = CalculateFileSha256(ResolveProjectPath(projectRoot, StreamingManifestPath)),
                bootstrapFingerprint = CalculateFileSha256(ResolveProjectPath(projectRoot, BootstrapScenePath)),
                implementationFingerprint = CalculateSourceSetFingerprint(projectRoot, StreamingImplementationPaths)
            };

            string path = GetStreamingEvidenceAbsolutePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
            string temporary = path + ".tmp";
            File.WriteAllText(
                temporary,
                JsonUtility.ToJson(evidence, prettyPrint: true) + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        private static string GetStreamingEvidenceAbsolutePath() =>
            ResolveProjectPath(GetProjectRoot(), StreamingEvidenceRelativePath);

        private static string GetProjectRoot() =>
            Directory.GetParent(Application.dataPath)?.FullName ??
            throw new InvalidOperationException("Unity project root cannot be resolved.");

        private static string ResolveProjectPath(string projectRoot, string projectRelativePath) =>
            Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));

        private static string CalculateSourceSetFingerprint(string projectRoot, string[] relativePaths)
        {
            var canonical = new StringBuilder();
            foreach (string relativePath in relativePaths
                         .Select(path => path.Replace('\\', '/'))
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                string absolutePath = ResolveProjectPath(projectRoot, relativePath);
                canonical.Append(relativePath).Append('\n')
                    .Append(CalculateFileSha256(absolutePath)).Append('\n');
            }

            using SHA256 sha256 = SHA256.Create();
            return ToLowerHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())));
        }

        private static string CalculateFileSha256(string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("Streaming lifecycle fingerprint input is missing.", absolutePath);
            }

            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(absolutePath);
            return ToLowerHex(sha256.ComputeHash(stream));
        }

        private static string ToLowerHex(byte[] hash)
        {
            var output = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                output.Append(value.ToString("x2"));
            }

            return output.ToString();
        }

        private static bool IsHomeToPierWalkable(Collider collider) =>
            collider.name == "Terrain" ||
            collider.name == "ShoreApproach" ||
            collider.name == "FootpathToPier" ||
            collider.name == "ShoreThreshold" ||
            collider.name.StartsWith("DeckPlank_");

        private static T Find<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        [Serializable]
        private sealed class ProductionStreamingLifecycleEvidenceDto
        {
            public int schemaVersion;
            public string validatorId = string.Empty;
            public bool passed;
            public string unityVersion = string.Empty;
            public int completedCycles;
            public string[] completedSequence = Array.Empty<string>();
            public int[] ownedSceneCounts = Array.Empty<int>();
            public bool[] pilotRootsDestroyed = Array.Empty<bool>();
            public bool[] nextRootsDestroyed = Array.Empty<bool>();
            public bool stableIdsUnique;
            public string[] pilotStableIds = Array.Empty<string>();
            public string[] nextZoneStableIds = Array.Empty<string>();
            public string manifestFingerprint = string.Empty;
            public string bootstrapFingerprint = string.Empty;
            public string implementationFingerprint = string.Empty;
        }
    }
}
