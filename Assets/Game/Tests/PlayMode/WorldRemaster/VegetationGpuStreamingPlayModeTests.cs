#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MSC.World.Partition;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace MSC.Tests.PlayMode.WorldRemaster
{
    public sealed class VegetationGpuStreamingPlayModeTests
    {
        private const int TestLayer = 31;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();
        private readonly List<RenderTexture> targets = new List<RenderTexture>();

        [SetUp]
        public void RequireRealGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supportsInstancing)
                Assert.Ignore("Actual GPU resource tests require a graphics device; do not run with -nographics.");
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Not.Null, "The camera callback test requires the project's SRP.");
        }

        [UnityTest]
        public IEnumerator TwoRenderersAndTwoCamerasShareOneBudgetAndUploadExactRecords()
        {
            VegetationWorldRenderer first = CreateFixture(0, 16, 2048);
            VegetationWorldRenderer second = CreateFixture(0, 16, 2048);
            CreateCamera(new Vector3(64f, 2f, -10f));
            CreateCamera(new Vector3(65f, 2f, -10f));
            int distinctUploadFrames = 0;
            int previousFrame = -1;
            long priorUploads = 0;
            for (int wait = 0; wait < 240; wait++)
            {
                yield return null;
                VegetationUploadFrameStatistics frame = VegetationWorldRenderer.AutomaticUploadFrameStatistics;
                AssertWithinFrameBudget(frame);
                if (frame.FrameIndex != previousFrame && frame.UploadOperations > 0)
                {
                    previousFrame = frame.FrameIndex;
                    distinctUploadFrames++;
                }
                long uploads = first.UploadedInstanceCount + second.UploadedInstanceCount;
                Assert.That(uploads - priorUploads, Is.LessThanOrEqualTo(VegetationWorldRenderer.MaximumAutomaticUploadInstancesPerFrame));
                priorUploads = uploads;
                if (first.GpuBatchCount == 16 && second.GpuBatchCount == 16) break;
            }
            Assert.That(first.CameraRenderCallbackCount, Is.GreaterThan(0));
            Assert.That(second.CameraRenderCallbackCount, Is.GreaterThan(0));
            Assert.That(first.GpuBatchCount + second.GpuBatchCount, Is.EqualTo(32));
            Assert.That(distinctUploadFrames, Is.GreaterThanOrEqualTo(4));
            Assert.That(priorUploads, Is.EqualTo(65536));
            Assert.That(first.ResidentGpuBufferCount + second.ResidentGpuBufferCount, Is.EqualTo(64));
            AssertUploadedTile(first, 0);
            AssertUploadedTile(second, 15);
        }

        [UnityTest]
        public IEnumerator FarCellAllocatesNothingUntilCameraApproachesThenReloadsAfterDisable()
        {
            VegetationWorldRenderer renderer = CreateFixture(2, 4, 128);
            Camera camera = CreateCamera(new Vector3(64f, 2f, -10f));
            for (int wait = 0; wait < 4; wait++) yield return null;
            Assert.That(renderer.CameraRenderCallbackCount, Is.GreaterThan(0), "A real camera must exercise the rejection.");
            Assert.That(renderer.SourceBatchCount, Is.Zero);
            Assert.That(renderer.ResidentGpuBufferCount, Is.Zero);
            Assert.That(renderer.UploadedInstanceCount, Is.Zero);

            camera.transform.position += new Vector3(1024f, 0f, 0f);
            yield return WaitForBatches(renderer, 4);
            GraphicsBuffer oldBuffer = GetBatchBuffer(renderer, 0, "InstanceBuffer");
            AssertUploadedTile(renderer, 0);
            renderer.enabled = false;
            Assert.That(renderer.ResidentGpuBufferCount, Is.Zero);
            Assert.That(renderer.ResidentInstanceCount, Is.Zero);
            Assert.That(oldBuffer.IsValid(), Is.False);
            renderer.enabled = true;
            yield return WaitForBatches(renderer, 4);
            AssertUploadedTile(renderer, 3);
            Assert.That(renderer.UploadedInstanceCount, Is.EqualTo(1024));
        }

        [UnityTest]
        public IEnumerator OversizedTileUploadsInSlicesAndNeverDrawsPartialData()
        {
            int count = VegetationWorldRenderer.MaximumAutomaticUploadInstancesPerFrame * 3 + 17;
            VegetationWorldRenderer renderer = CreateFixture(0, 1, count);
            CreateCamera(new Vector3(64f, 2f, -10f));
            bool sawPartial = false;
            long previous = 0;
            for (int wait = 0; wait < 120; wait++)
            {
                yield return null;
                AssertWithinFrameBudget(VegetationWorldRenderer.AutomaticUploadFrameStatistics);
                long uploaded = renderer.UploadedInstanceCount;
                Assert.That(uploaded - previous, Is.LessThanOrEqualTo(VegetationWorldRenderer.MaximumAutomaticUploadInstancesPerFrame));
                previous = uploaded;
                if (uploaded > 0 && uploaded < count)
                {
                    sawPartial = true;
                    Assert.That(renderer.GpuBatchCount, Is.Zero, "A partial instance range must not become drawable.");
                    Assert.That(renderer.ResidentGpuBufferCount, Is.EqualTo(1));
                }
                if (renderer.GpuBatchCount == 1) break;
            }
            Assert.That(sawPartial, Is.True);
            Assert.That(renderer.GpuBatchCount, Is.EqualTo(1));
            Assert.That(renderer.UploadedInstanceCount, Is.EqualTo(count));
            AssertUploadedTile(renderer, 0);
        }

        [UnityTest]
        public IEnumerator MetadataPreparationIsIncrementalAndSharedAcrossRenderers()
        {
            const int tilesPerRenderer = 384;
            VegetationWorldRenderer first = CreateFixture(0, tilesPerRenderer, 1);
            VegetationWorldRenderer second = CreateFixture(0, tilesPerRenderer, 1);
            CreateCamera(new Vector3(64f, 2f, -10f));

            bool observedPartialPreparation = false;
            for (int wait = 0; wait < 240; wait++)
            {
                yield return null;
                VegetationMetadataFrameStatistics frame =
                    VegetationWorldRenderer.AutomaticMetadataFrameStatistics;
                Assert.That(
                    frame.ExaminedProfileRecords,
                    Is.LessThanOrEqualTo(
                        VegetationWorldRenderer
                            .MaximumAutomaticMetadataRecordsPerFrame));
                Assert.That(
                    frame.PreparationOperations,
                    Is.LessThanOrEqualTo(
                        VegetationWorldRenderer
                            .MaximumAutomaticMetadataOperationsPerFrame));

                int prepared = first.SourceBatchCount + second.SourceBatchCount;
                if (prepared > 0 && prepared < tilesPerRenderer * 2)
                {
                    observedPartialPreparation = true;
                    Assert.That(
                        prepared,
                        Is.LessThan(tilesPerRenderer * 2),
                        "Automatic metadata must not materialize both catalogs synchronously.");
                }

                if (first.CameraRenderCallbackCount > 0 &&
                    second.CameraRenderCallbackCount > 0 &&
                    !first.MetadataBuildInProgress &&
                    !second.MetadataBuildInProgress)
                {
                    break;
                }
            }

            Assert.That(observedPartialPreparation, Is.True);
            Assert.That(first.CameraRenderCallbackCount, Is.GreaterThan(0));
            Assert.That(second.CameraRenderCallbackCount, Is.GreaterThan(0));
            Assert.That(first.MetadataBuildInProgress, Is.False);
            Assert.That(second.MetadataBuildInProgress, Is.False);
            Assert.That(first.SourceBatchCount, Is.EqualTo(tilesPerRenderer));
            Assert.That(second.SourceBatchCount, Is.EqualTo(tilesPerRenderer));
            Assert.That(
                first.MaximumMetadataRecordsPerStep,
                Is.LessThanOrEqualTo(
                    VegetationWorldRenderer
                        .MaximumAutomaticMetadataRecordsPerFrame));
            Assert.That(
                second.MaximumMetadataRecordsPerStep,
                Is.LessThanOrEqualTo(
                    VegetationWorldRenderer
                        .MaximumAutomaticMetadataRecordsPerFrame));
            Assert.That(
                first.MetadataPreparationStepCount +
                second.MetadataPreparationStepCount,
                Is.GreaterThan(2));
        }

        [UnityTest]
        public IEnumerator ManualRebuildRemainsEagerAndInitializesAllLodCommands()
        {
            VegetationWorldRenderer renderer = CreateFixture(2, 4, 128, false);
            renderer.RebuildGpuResources();
            Assert.That(renderer.GpuBatchCount, Is.EqualTo(4));
            Assert.That(renderer.SourceBatchCount, Is.EqualTo(4));
            Assert.That(renderer.ResidentInstanceCount, Is.EqualTo(512));
            Assert.That(renderer.ResidentGpuBufferCount, Is.EqualTo(8));
            Assert.That(renderer.UploadedInstanceCount, Is.EqualTo(512));
            AssertUploadedTile(renderer, 0);
            yield return null;
        }

        [UnityTest, Explicit("Isolated GPU-upload benchmark using the current saved 25-cell home population; requires graphics and local generated assets.")]
        public IEnumerator SavedHomePopulation_ManualEagerVersusAutomaticBudgetedBenchmark()
        {
            const string folder = "Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/Data/";
            var catalogs = new List<VegetationCellCatalog>();
            long assetLoadStarted = Stopwatch.GetTimestamp();
            for (int z = -5; z <= -1; z++)
            for (int x = -2; x <= 2; x++)
            {
                string path = folder + $"cell_{x}_{z}/Catalog.asset";
                if (!File.Exists(path)) Assert.Ignore("Saved full-map benchmark population is unavailable: " + path);
                VegetationCellCatalog catalog = AssetDatabase.LoadAssetAtPath<VegetationCellCatalog>(path);
                Assert.That(catalog, Is.Not.Null, path);
                catalogs.Add(catalog);
            }
            var report = new UploadBenchmarkReport
            {
                utc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                catalogCount = catalogs.Count,
                excludedAssetLoadWallMilliseconds = ElapsedMilliseconds(assetLoadStarted),
                cameraPosition = new Vector3(146.3195953f, 2.7205393f, -1046.6018066f),
                baselineDescription = "Current explicit synchronous eager RebuildGpuResources on the same catalogs. Measures former work volume, not an executed pre-patch binary; backing-array and argument-buffer improvements are present in both modes.",
                limitations = "Grass GPU preparation only. CPU asset loading above is excluded from both preparation timings; no source-world/tree scene activation, gameplay, texture cold-start isolation, representative resolution, GPU frame-time or 60 FPS acceptance. The 2ms budget is cooperative: one driver/API call can exceed it."
            };
            var manual = new List<VegetationWorldRenderer>();
            foreach (VegetationCellCatalog catalog in catalogs) manual.Add(CreateRenderer(catalog, false));
            long baselineStarted = Stopwatch.GetTimestamp();
            foreach (VegetationWorldRenderer renderer in manual)
            {
                long cellStarted = Stopwatch.GetTimestamp();
                renderer.RebuildGpuResources();
                report.maximumSynchronousCellRebuildMilliseconds = Math.Max(report.maximumSynchronousCellRebuildMilliseconds, ElapsedMilliseconds(cellStarted));
                report.manualResidentInstances += renderer.ResidentInstanceCount;
                report.manualReadyBatches += renderer.GpuBatchCount;
                report.manualGpuBuffers += renderer.ResidentGpuBufferCount;
                report.manualUploadCpuMilliseconds += renderer.UploadCpuMilliseconds;
            }
            report.manualTotalSynchronousMilliseconds = ElapsedMilliseconds(baselineStarted);
            foreach (VegetationWorldRenderer renderer in manual) Object.Destroy(renderer.gameObject);
            yield return null;
            yield return null;

            Camera camera = CreateCamera(report.cameraPosition, false);
            var automatic = new List<VegetationWorldRenderer>();
            foreach (VegetationCellCatalog catalog in catalogs) automatic.Add(CreateRenderer(catalog, false));
            report.expectedNearInstances = CountNearbyInstances(catalogs, report.cameraPosition, out report.expectedNearBatches);
            int firstFrame = Time.frameCount;
            long automaticStarted = Stopwatch.GetTimestamp();
            foreach (VegetationWorldRenderer renderer in automatic) renderer.gameObject.SetActive(true);
            camera.enabled = true;
            long previousTick = Stopwatch.GetTimestamp();
            int previousUploadFrame = -1;
            int ready = 0;
            for (int wait = 0; wait < 1200; wait++)
            {
                yield return null;
                double interval = ElapsedMilliseconds(previousTick);
                previousTick = Stopwatch.GetTimestamp();
                report.maximumObservedYieldIntervalMilliseconds = Math.Max(report.maximumObservedYieldIntervalMilliseconds, interval);
                VegetationUploadFrameStatistics frame = VegetationWorldRenderer.AutomaticUploadFrameStatistics;
                if (frame.FrameIndex != previousUploadFrame && frame.FrameIndex >= firstFrame)
                {
                    previousUploadFrame = frame.FrameIndex;
                    report.maximumAutomaticInstancesInFrame = Math.Max(report.maximumAutomaticInstancesInFrame, frame.UploadedInstances);
                    report.maximumAutomaticOperationsInFrame = Math.Max(report.maximumAutomaticOperationsInFrame, frame.UploadOperations);
                    report.maximumAutomaticUploadCpuMillisecondsInFrame = Math.Max(report.maximumAutomaticUploadCpuMillisecondsInFrame, frame.CpuMilliseconds);
                    report.frames.Add(new UploadBenchmarkFrame { frame = frame.FrameIndex, operations = frame.UploadOperations,
                        instances = frame.UploadedInstances, createdBuffers = frame.CreatedBuffers, uploadCpuMilliseconds = frame.CpuMilliseconds });
                }
                ready = 0;
                foreach (VegetationWorldRenderer renderer in automatic) ready += renderer.GpuBatchCount;
                if (ready == report.expectedNearBatches) break;
            }
            report.automaticFramesToReady = Time.frameCount - firstFrame;
            report.automaticWallMillisecondsToReady = ElapsedMilliseconds(automaticStarted);
            foreach (VegetationWorldRenderer renderer in automatic)
            {
                report.automaticResidentInstances += renderer.ResidentInstanceCount;
                report.automaticReadyBatches += renderer.GpuBatchCount;
                report.automaticGpuBuffers += renderer.ResidentGpuBufferCount;
                report.automaticUploadCpuMilliseconds += renderer.UploadCpuMilliseconds;
                report.maximumAutomaticUploadStepMilliseconds = Math.Max(report.maximumAutomaticUploadStepMilliseconds, renderer.MaxUploadCpuMilliseconds);
            }
            report.passed = report.automaticReadyBatches == report.expectedNearBatches &&
                report.automaticResidentInstances == report.expectedNearInstances &&
                report.maximumAutomaticInstancesInFrame <= VegetationWorldRenderer.MaximumAutomaticUploadInstancesPerFrame &&
                report.maximumAutomaticOperationsInFrame <= VegetationWorldRenderer.MaximumAutomaticUploadOperationsPerFrame &&
                report.automaticResidentInstances > 0 && report.automaticResidentInstances < report.manualResidentInstances;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "VegetationRebuild", "Performance", "grass-gpu-benchmark.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Assert.That(report.passed, Is.True, output);
        }

        private VegetationWorldRenderer CreateFixture(int cellX, int tileCount, int instanceCount, bool active = true)
        {
            var mask = Own(new Texture2D(16, 16, TextureFormat.RGBA32, false, true));
            var cell = Own(ScriptableObject.CreateInstance<VegetationCellAsset>());
            var profile = Own(ScriptableObject.CreateInstance<VegetationProfile>());
            Shader shader = Shader.Find("MSC/HDRP/Vegetation Indirect");
            Assert.That(shader, Is.Not.Null);
            var material = Own(new Material(shader) { enableInstancing = true });
            Mesh near = CreateMesh(1), middle = CreateMesh(2), far = CreateMesh(3);
            profile.ConfigureForAuthoring("gpu-fixture", VegetationDensityChannel.ShortGrass, 0.5f, 1f, 45f,
                new Vector2(-100f, 100f), Vector2.one, 1f, near, middle, far, material,
                new Vector3(20f, 70f, 180f), 5f, ShadowCastingMode.Off, false, 17);
            var index = new WorldCellIndex(cellX, 0);
            cell.ConfigureForAuthoring(index.Id, index, new Bounds(new Vector3(cellX * 512f + 256f, 0f, 256f),
                new Vector3(512f, 200f, 512f)), 16, 32f, mask);
            for (int tile = 0; tile < tileCount; tile++)
            {
                int tileX = tile % 4, tileZ = tile / 4;
                Vector3 center = new Vector3(cellX * 512f + tileX * 32f + 16f, 0f, tileZ * 32f + 16f);
                var records = new VegetationInstanceRecord[instanceCount];
                for (int i = 0; i < records.Length; i++)
                    records[i] = new VegetationInstanceRecord(center + new Vector3((i % 16) * .02f, 0f, (i / 16 % 16) * .02f),
                        Vector3.up, i % 360, .2f, 0, (byte)i, (ushort)i);
                cell.ReplaceTileForAuthoring(new VegetationTileRecord(tileX, tileZ, new Bounds(center, new Vector3(32f, 4f, 32f)),
                    new[] { new VegetationProfileTileInstances(0, records) }, Array.Empty<Vector3>(), Array.Empty<VegetationRejectedSample>()));
            }
            var catalog = Own(ScriptableObject.CreateInstance<VegetationCellCatalog>());
            catalog.ConfigureForAuthoring(new[] { cell }, new[] { profile }, ~0, 100f, 200f);
            return CreateRenderer(catalog, active);
        }

        private VegetationWorldRenderer CreateRenderer(VegetationCellCatalog catalog, bool active)
        {
            var owner = new GameObject("Vegetation GPU streaming fixture") { layer = TestLayer };
            owner.SetActive(false);
            objects.Add(owner);
            VegetationWorldRenderer renderer = owner.AddComponent<VegetationWorldRenderer>();
            renderer.ConfigureForAuthoring(catalog);
            var serialized = new SerializedObject(renderer);
            serialized.FindProperty("renderInSceneView").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Unity only guarantees OnDestroy for objects that have been active.
            // The inactive manual-benchmark owners still own native GPU buffers.
            owner.SetActive(true);
            if (!active) owner.SetActive(false);
            return renderer;
        }

        private Camera CreateCamera(Vector3 position, bool enabled = true)
        {
            var owner = new GameObject("Vegetation GPU streaming camera");
            objects.Add(owner);
            Camera camera = owner.AddComponent<Camera>();
            camera.enabled = false;
            owner.AddComponent<HDAdditionalCameraData>();
            camera.transform.SetPositionAndRotation(position, Quaternion.identity);
            camera.fieldOfView = 90f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 300f;
            camera.cullingMask = 1 << TestLayer;
            var target = new RenderTexture(64, 64, 24);
            target.Create();
            targets.Add(target);
            camera.targetTexture = target;
            camera.enabled = enabled;
            return camera;
        }

        private Mesh CreateMesh(int triangles)
        {
            var mesh = Own(new Mesh());
            mesh.vertices = new[] { Vector3.zero, new Vector3(.1f, 0f, 0f), new Vector3(0f, .2f, 0f) };
            var indices = new int[triangles * 3];
            for (int i = 0; i < indices.Length; i++) indices[i] = i % 3;
            mesh.triangles = indices;
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private T Own<T>(T asset) where T : Object { assets.Add(asset); return asset; }

        private static IEnumerator WaitForBatches(VegetationWorldRenderer renderer, int count)
        {
            for (int frame = 0; frame < 240 && renderer.GpuBatchCount != count; frame++) yield return null;
            Assert.That(renderer.GpuBatchCount, Is.EqualTo(count));
        }

        private static void AssertWithinFrameBudget(VegetationUploadFrameStatistics frame)
        {
            Assert.That(frame.UploadOperations, Is.LessThanOrEqualTo(VegetationWorldRenderer.MaximumAutomaticUploadOperationsPerFrame));
            Assert.That(frame.UploadedInstances, Is.LessThanOrEqualTo(VegetationWorldRenderer.MaximumAutomaticUploadInstancesPerFrame));
            Assert.That(frame.CreatedBuffers, Is.LessThanOrEqualTo(VegetationWorldRenderer.MaximumAutomaticUploadOperationsPerFrame * 2));
            // Time is a soft bound: a single native allocation/upload may exceed it.
        }

        private static GraphicsBuffer GetBatchBuffer(VegetationWorldRenderer renderer, int tile, string name)
        {
            var batches = (IList)typeof(VegetationWorldRenderer).GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer);
            object batch = batches[tile];
            return (GraphicsBuffer)batch.GetType().GetProperty(name).GetValue(batch);
        }

        private static void AssertUploadedTile(VegetationWorldRenderer renderer, int tile)
        {
            IReadOnlyList<VegetationInstanceRecord> source = renderer.Catalog.Cells[0].Tiles[tile].ProfileInstances[0].Instances;
            var actual = new VegetationInstanceRecord[source.Count];
            GetBatchBuffer(renderer, tile, "InstanceBuffer").GetData(actual);
            foreach (int sample in new[] { 0, source.Count / 3, source.Count / 2, source.Count - 1 })
            {
                Assert.That(actual[sample].WorldPosition, Is.EqualTo(source[sample].WorldPosition));
                Assert.That(actual[sample].YawDegrees, Is.EqualTo(source[sample].YawDegrees));
                Assert.That(actual[sample].WindPhase, Is.EqualTo(source[sample].WindPhase));
                Assert.That(actual[sample].ColorVariation, Is.EqualTo(source[sample].ColorVariation));
            }
            var args = new GraphicsBuffer.IndirectDrawIndexedArgs[3];
            GetBatchBuffer(renderer, tile, "Arguments").GetData(args);
            for (int lod = 0; lod < 3; lod++)
            {
                Assert.That(args[lod].indexCountPerInstance, Is.EqualTo((uint)((lod + 1) * 3)));
                Assert.That(args[lod].instanceCount, Is.EqualTo((uint)source.Count));
                Assert.That(args[lod].startInstance, Is.Zero);
            }
        }

        private static long CountNearbyInstances(List<VegetationCellCatalog> catalogs, Vector3 position, out int batches)
        {
            long count = 0;
            batches = 0;
            foreach (VegetationCellCatalog catalog in catalogs)
            foreach (VegetationCellAsset cell in catalog.Cells)
            foreach (VegetationTileRecord tile in cell.Tiles)
            foreach (VegetationProfileTileInstances group in tile.ProfileInstances)
            {
                VegetationProfile profile = catalog.Profiles[group.ProfileIndex];
                if (tile.WorldBounds.SqrDistance(position) > profile.CullingDistance * profile.CullingDistance) continue;
                count += group.Count;
                batches++;
            }
            return count;
        }

        private static double ElapsedMilliseconds(long started) => (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject owner in objects)
            {
                if (owner == null) continue;
                Camera camera = owner.GetComponent<Camera>();
                if (camera != null) { camera.enabled = false; camera.targetTexture = null; }
                Object.Destroy(owner);
            }
            yield return null;
            foreach (RenderTexture target in targets) { target.Release(); Object.Destroy(target); }
            foreach (Object asset in assets) if (asset != null) Object.Destroy(asset);
            objects.Clear(); assets.Clear(); targets.Clear();
            yield return null;
        }

        [Serializable]
        private sealed class UploadBenchmarkReport
        {
            public string utc, unityVersion, graphicsDevice, baselineDescription, limitations;
            public bool passed;
            public int catalogCount, manualReadyBatches, manualGpuBuffers, automaticReadyBatches, automaticGpuBuffers, expectedNearBatches;
            public long manualResidentInstances, automaticResidentInstances, expectedNearInstances;
            public Vector3 cameraPosition;
            public double excludedAssetLoadWallMilliseconds, manualTotalSynchronousMilliseconds, maximumSynchronousCellRebuildMilliseconds;
            public double manualUploadCpuMilliseconds, automaticUploadCpuMilliseconds, maximumAutomaticUploadStepMilliseconds;
            public int automaticFramesToReady, maximumAutomaticInstancesInFrame, maximumAutomaticOperationsInFrame;
            public double automaticWallMillisecondsToReady, maximumAutomaticUploadCpuMillisecondsInFrame, maximumObservedYieldIntervalMilliseconds;
            public List<UploadBenchmarkFrame> frames = new List<UploadBenchmarkFrame>();
        }

        [Serializable]
        private sealed class UploadBenchmarkFrame
        {
            public int frame, operations, instances, createdBuffers;
            public double uploadCpuMilliseconds;
        }
    }
}
#endif
