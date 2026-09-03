using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace MSC.World.Vegetation
{
    /// <summary>
    /// GPU-driven vegetation renderer. Instance records are grouped by
    /// 16/32-metre tiles; each draw therefore uses tight, independently culled
    /// bounds rather than one world-sized bounds group.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VegetationWorldRenderer : MonoBehaviour
    {
        public const int MaximumAutomaticUploadOperationsPerFrame = 4;
        public const int MaximumAutomaticUploadInstancesPerFrame = 8192;
        public const double AutomaticUploadBudgetMilliseconds = 1d;
        public const MotionVectorGenerationMode PackedIndirectMotionVectorMode =
            MotionVectorGenerationMode.Camera;
        public const int MaximumAutomaticMetadataOperationsPerFrame = 4;
        public const int MaximumAutomaticMetadataRecordsPerFrame = 256;
        public const double AutomaticMetadataBudgetMilliseconds = 1d;
        private const double AutomaticMetadataStepBudgetMilliseconds = 0.5d;

        private static readonly VegetationGpuUploadBudget automaticUploadBudget =
            new VegetationGpuUploadBudget();
        private static readonly VegetationMetadataPreparationBudget automaticMetadataBudget =
            new VegetationMetadataPreparationBudget();
        private static readonly ProfilerMarker rebuildMarker = new ProfilerMarker("MSC.Vegetation.ManualRebuild");
        private static readonly ProfilerMarker metadataMarker = new ProfilerMarker("MSC.Vegetation.BuildMetadata");
        private static readonly ProfilerMarker uploadMarker = new ProfilerMarker("MSC.Vegetation.UploadTile");
        private static readonly ProfilerMarker renderMarker = new ProfilerMarker("MSC.Vegetation.CullAndDraw");
        private static readonly ProfilerMarker releaseMarker = new ProfilerMarker("MSC.Vegetation.ReleaseResources");
        private static readonly int InstanceBufferId =
            Shader.PropertyToID("_VegetationInstanceBuffer");
        private static readonly int LodFadeId =
            Shader.PropertyToID("_VegetationLodFade");
        private static readonly int LodFadeDirectionId =
            Shader.PropertyToID("_VegetationLodFadeDirection");

        [SerializeField] private VegetationCellCatalog catalog;
        [SerializeField] private bool renderInSceneView = true;
        [SerializeField] private bool renderInGameView = true;
        [SerializeField] private int renderingLayerMask = 1;

        private readonly List<TileBatch> batches = new List<TileBatch>();
        private readonly Plane[] frustumPlanes = new Plane[6];
        private bool resourcesDirty = true;
        private int readyBatchCount;
        private int residentGpuBufferCount;
        private long residentInstanceCount;
        private bool metadataBuildInProgress;
        private int metadataCellIndex;
        private int metadataTileIndex;
        private int metadataProfileIndex;
        private int metadataLastProcessedFrame = -1;

        public VegetationCellCatalog Catalog => catalog;
        public int GpuBatchCount => readyBatchCount;
        public int SourceBatchCount => batches.Count;
        /// <summary>Allocated instance capacity, including any partially uploaded tile.</summary>
        public long ResidentInstanceCount => residentInstanceCount;
        public int ResidentGpuBufferCount => residentGpuBufferCount;
        public long UploadedInstanceCount { get; private set; }
        public int UploadOperationCount { get; private set; }
        public int CameraRenderCallbackCount { get; private set; }
        public double UploadCpuMilliseconds { get; private set; }
        public double MaxUploadCpuMilliseconds { get; private set; }
        public static VegetationUploadFrameStatistics AutomaticUploadFrameStatistics => automaticUploadBudget.Statistics;
        public bool MetadataBuildInProgress => metadataBuildInProgress;
        public int MetadataPreparationStepCount { get; private set; }
        public int ExaminedMetadataProfileRecordCount { get; private set; }
        public int MaximumMetadataRecordsPerStep { get; private set; }
        public double MetadataPreparationCpuMilliseconds { get; private set; }
        public double MaximumMetadataPreparationStepCpuMilliseconds { get; private set; }
        public static VegetationMetadataFrameStatistics AutomaticMetadataFrameStatistics =>
            automaticMetadataBudget.Statistics;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAutomaticUploadBudget()
        {
            automaticUploadBudget.Reset();
            automaticMetadataBudget.Reset();
        }

        public void ResetUploadStatistics()
        {
            UploadedInstanceCount = 0;
            UploadOperationCount = 0;
            CameraRenderCallbackCount = 0;
            UploadCpuMilliseconds = 0d;
            MaxUploadCpuMilliseconds = 0d;
        }

        private void OnEnable()
        {
            resourcesDirty = true;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            ReleaseResources();
        }

        private void OnDestroy()
        {
            ReleaseResources();
        }

        private void OnValidate()
        {
            resourcesDirty = true;
        }

        public void RebuildGpuResources()
        {
            // Explicit authoring/capture callers retain the synchronous full rebuild.
            // Playing cameras use metadata + visible, budgeted uploads below instead.
            using (rebuildMarker.Auto())
            {
                RebuildBatchMetadata();
                for (int index = 0; index < batches.Count; index++)
                    UploadBatch(batches[index], int.MaxValue, false);
            }
        }

        private void RebuildBatchMetadata()
        {
            using (metadataMarker.Auto())
            {
                BeginBatchMetadataRebuild();
                while (metadataBuildInProgress)
                {
                    ProcessBatchMetadataStep(int.MaxValue, double.MaxValue);
                }
            }
        }

        private void BeginBatchMetadataRebuild()
        {
            ReleaseResources();
            MetadataPreparationStepCount = 0;
            ExaminedMetadataProfileRecordCount = 0;
            MaximumMetadataRecordsPerStep = 0;
            MetadataPreparationCpuMilliseconds = 0d;
            MaximumMetadataPreparationStepCpuMilliseconds = 0d;
            metadataCellIndex = 0;
            metadataTileIndex = 0;
            metadataProfileIndex = 0;
            metadataLastProcessedFrame = -1;
            resourcesDirty = false;
            metadataBuildInProgress = catalog != null;
        }

        private void ProcessAutomaticBatchMetadata()
        {
            if (!metadataBuildInProgress ||
                metadataLastProcessedFrame == Time.frameCount ||
                !automaticMetadataBudget.TryGetRecordAllowance(
                    Time.frameCount,
                    out int allowance))
            {
                return;
            }

            int previousRecords = ExaminedMetadataProfileRecordCount;
            double previousMilliseconds = MetadataPreparationCpuMilliseconds;
            using (metadataMarker.Auto())
            {
                ProcessBatchMetadataStep(
                    allowance,
                    AutomaticMetadataStepBudgetMilliseconds);
            }
            automaticMetadataBudget.Record(
                ExaminedMetadataProfileRecordCount - previousRecords,
                MetadataPreparationCpuMilliseconds - previousMilliseconds);
            metadataLastProcessedFrame = Time.frameCount;
        }

        private void ProcessBatchMetadataStep(
            int maximumRecords,
            double maximumMilliseconds)
        {
            if (!metadataBuildInProgress || maximumRecords <= 0)
            {
                return;
            }

            long started = Stopwatch.GetTimestamp();
            int examined = 0;
            int traversed = 0;
            IReadOnlyList<VegetationCellAsset> cells = catalog.Cells;
            IReadOnlyList<VegetationProfile> profiles = catalog.Profiles;
            while (metadataCellIndex < cells.Count && examined < maximumRecords)
            {
                if (traversed > 0 &&
                    (Stopwatch.GetTimestamp() - started) * 1000d /
                    Stopwatch.Frequency >= maximumMilliseconds)
                {
                    break;
                }

                traversed++;

                VegetationCellAsset cell = cells[metadataCellIndex];
                if (cell == null)
                {
                    AdvanceMetadataCell();
                    continue;
                }

                IReadOnlyList<VegetationTileRecord> tiles = cell.Tiles;
                if (metadataTileIndex >= tiles.Count)
                {
                    AdvanceMetadataCell();
                    continue;
                }

                VegetationTileRecord tile = tiles[metadataTileIndex];
                if (tile == null || tile.TotalInstanceCount == 0)
                {
                    AdvanceMetadataTile();
                    continue;
                }

                IReadOnlyList<VegetationProfileTileInstances> tileProfiles =
                    tile.ProfileInstances;
                if (metadataProfileIndex >= tileProfiles.Count)
                {
                    AdvanceMetadataTile();
                    continue;
                }

                VegetationProfileTileInstances tileProfile =
                    tileProfiles[metadataProfileIndex++];
                examined++;
                if (tileProfile == null ||
                    tileProfile.Count == 0 ||
                    tileProfile.ProfileIndex < 0 ||
                    tileProfile.ProfileIndex >= profiles.Count)
                {
                    continue;
                }

                VegetationProfile profile = profiles[tileProfile.ProfileIndex];
                if (profile == null ||
                    profile.Material == null ||
                    profile.GetLodMesh(0) == null)
                {
                    continue;
                }

                batches.Add(new TileBatch(
                    cell.CellId,
                    tile,
                    profile,
                    tileProfile.InstanceData));
            }

            if (metadataCellIndex >= cells.Count)
            {
                metadataBuildInProgress = false;
            }

            double elapsed = (Stopwatch.GetTimestamp() - started) * 1000d /
                             Stopwatch.Frequency;
            MetadataPreparationStepCount++;
            ExaminedMetadataProfileRecordCount += examined;
            MaximumMetadataRecordsPerStep = Math.Max(
                MaximumMetadataRecordsPerStep,
                examined);
            MetadataPreparationCpuMilliseconds += elapsed;
            MaximumMetadataPreparationStepCpuMilliseconds = Math.Max(
                MaximumMetadataPreparationStepCpuMilliseconds,
                elapsed);
        }

        private void AdvanceMetadataTile()
        {
            metadataTileIndex++;
            metadataProfileIndex = 0;
        }

        private void AdvanceMetadataCell()
        {
            metadataCellIndex++;
            metadataTileIndex = 0;
            metadataProfileIndex = 0;
        }

        private void OnBeginCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            if (!enabled ||
                camera == null ||
                catalog == null ||
                (camera.cullingMask & (1 << gameObject.layer)) == 0 ||
                (camera.cameraType == CameraType.SceneView &&
                    (!renderInSceneView || Application.isPlaying)) ||
                (!renderInGameView && camera.cameraType == CameraType.Game))
            {
                return;
            }

            using (renderMarker.Auto())
            {
                CameraRenderCallbackCount++;
                Vector3 cameraPosition = camera.transform.position;
                if (Application.isPlaying && !IsCatalogWithinDrawDistance(cameraPosition))
                    return;

                if (resourcesDirty)
                {
                    if (Application.isPlaying) BeginBatchMetadataRebuild();
                    else RebuildGpuResources();
                }

                if (Application.isPlaying)
                {
                    ProcessAutomaticBatchMetadata();
                }

                GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
                for (int index = 0; index < batches.Count; index++)
                {
                    TileBatch batch = batches[index];
                    float squaredDistance = batch.Bounds.SqrDistance(
                        cameraPosition);
                    float cullingDistance = batch.Profile.CullingDistance;
                    if (squaredDistance > cullingDistance * cullingDistance ||
                        !GeometryUtility.TestPlanesAABB(frustumPlanes,
                            batch.Bounds))
                    {
                        continue;
                    }

                    if (!batch.IsReady)
                    {
                        if (!automaticUploadBudget.TryGetInstanceAllowance(Time.frameCount, out int allowance))
                            continue;
                        UploadBatch(batch, allowance, true);
                        if (!batch.IsReady) continue;
                    }

                    // LOD selection needs linear metres, but distance rejection
                    // does not. Pay for sqrt only for a visible surviving tile.
                    DrawLod(batch, Mathf.Sqrt(squaredDistance), camera);
                }

                // Spend any remaining shared allowance on nearby tiles behind the
                // camera, after visible work. Turning should not require a cold upload.
                if (Application.isPlaying)
                {
                    for (int index = 0; index < batches.Count; index++)
                    {
                        TileBatch batch = batches[index];
                        if (batch.IsReady || batch.Bounds.SqrDistance(cameraPosition) >
                            batch.Profile.CullingDistance * batch.Profile.CullingDistance) continue;
                        if (!automaticUploadBudget.TryGetInstanceAllowance(Time.frameCount, out int allowance)) break;
                        UploadBatch(batch, allowance, true);
                    }
                }
            }
        }

        private bool IsCatalogWithinDrawDistance(Vector3 cameraPosition)
        {
            float maximumDistance = 0f;
            IReadOnlyList<VegetationProfile> profiles = catalog.Profiles;
            for (int index = 0; index < profiles.Count; index++)
                if (profiles[index] != null)
                    maximumDistance = Mathf.Max(maximumDistance, profiles[index].CullingDistance);

            float squaredDistance = maximumDistance * maximumDistance;
            IReadOnlyList<VegetationCellAsset> cells = catalog.Cells;
            for (int index = 0; index < cells.Count; index++)
            {
                VegetationCellAsset cell = cells[index];
                if (cell == null) continue;
                Bounds bounds = cell.WorldBounds;
                // Authored tile bounds can extend beyond the cell's root-position
                // bounds. A tile-width guard keeps this coarse rejection conservative.
                bounds.Expand(cell.TileSizeMeters * 2f);
                if (bounds.SqrDistance(cameraPosition) <= squaredDistance) return true;
            }
            return false;
        }

        private void UploadBatch(TileBatch batch, int allowance, bool automatic)
        {
            bool hadInstanceBuffer = batch.InstanceBuffer != null;
            bool wasReady = batch.IsReady;
            int previousBufferCount = batch.BufferCount;
            long started = Stopwatch.GetTimestamp();
            int uploaded;
            using (uploadMarker.Auto()) uploaded = batch.UploadNext(allowance);
            double elapsed = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            int createdBuffers = batch.BufferCount - previousBufferCount;
            residentGpuBufferCount += createdBuffers;
            if (!hadInstanceBuffer && batch.InstanceBuffer != null) residentInstanceCount += batch.InstanceCount;
            if (!wasReady && batch.IsReady) readyBatchCount++;
            UploadedInstanceCount += uploaded;
            UploadOperationCount++;
            UploadCpuMilliseconds += elapsed;
            MaxUploadCpuMilliseconds = Math.Max(MaxUploadCpuMilliseconds, elapsed);
            if (automatic) automaticUploadBudget.Record(uploaded, createdBuffers, elapsed);
        }

        private void DrawLod(
            TileBatch batch,
            float distance,
            Camera camera)
        {
            VegetationProfile profile = batch.Profile;
            float ditherWidth = profile.LodDitherWidth;
            if (distance < profile.MiddleLodDistance - ditherWidth)
            {
                DrawBatch(batch, 0, 1f, 1f, camera);
                return;
            }

            if (distance < profile.MiddleLodDistance + ditherWidth)
            {
                float transition = Mathf.InverseLerp(
                    profile.MiddleLodDistance - ditherWidth,
                    profile.MiddleLodDistance + ditherWidth,
                    distance);
                DrawBatch(batch, 0, 1f - transition, -1f, camera);
                DrawBatch(batch, 1, transition, 1f, camera);
                return;
            }

            if (distance < profile.FarLodDistance - ditherWidth)
            {
                DrawBatch(batch, 1, 1f, 1f, camera);
                return;
            }

            if (distance < profile.FarLodDistance + ditherWidth)
            {
                float transition = Mathf.InverseLerp(
                    profile.FarLodDistance - ditherWidth,
                    profile.FarLodDistance + ditherWidth,
                    distance);
                DrawBatch(batch, 1, 1f - transition, -1f, camera);
                DrawBatch(batch, 2, transition, 1f, camera);
                return;
            }

            DrawBatch(batch, 2, 1f, 1f, camera);
        }

        private void DrawBatch(
            TileBatch batch,
            int lodIndex,
            float lodFade,
            float lodFadeDirection,
            Camera camera)
        {
            Mesh mesh = batch.Profile.GetLodMesh(lodIndex);
            if (mesh == null)
            {
                return;
            }

            GraphicsBuffer arguments = batch.Arguments;
            if (arguments == null)
            {
                return;
            }

            MaterialPropertyBlock properties = batch.PropertyBlock;
            properties.SetBuffer(InstanceBufferId, batch.InstanceBuffer);
            properties.SetFloat(LodFadeId, Mathf.Clamp01(lodFade));
            properties.SetFloat(LodFadeDirectionId, lodFadeDirection);

            var renderParams = new RenderParams(batch.Profile.Material)
            {
                worldBounds = batch.Bounds,
                matProps = properties,
                shadowCastingMode = batch.Profile.ShadowCasting,
                receiveShadows = batch.Profile.ReceiveShadows,
                camera = camera,
                motionVectorMode = PackedIndirectMotionVectorMode,
                renderingLayerMask = (uint)Mathf.Max(1, renderingLayerMask),
                layer = gameObject.layer
            };
            Graphics.RenderMeshIndirect(
                renderParams,
                mesh,
                arguments,
                1,
                lodIndex);
        }

        private void ReleaseResources()
        {
            using (releaseMarker.Auto())
            {
                for (int index = 0; index < batches.Count; index++)
                {
                    batches[index].Dispose();
                }

                batches.Clear();
                metadataBuildInProgress = false;
                metadataCellIndex = 0;
                metadataTileIndex = 0;
                metadataProfileIndex = 0;
                metadataLastProcessedFrame = -1;
                readyBatchCount = 0;
                residentGpuBufferCount = 0;
                residentInstanceCount = 0;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(VegetationCellCatalog configuredCatalog)
        {
            catalog = configuredCatalog;
            resourcesDirty = true;
        }
#endif

        private sealed class TileBatch : IDisposable
        {
            private readonly VegetationInstanceRecord[] instanceData;
            private int uploadedCount;

            public TileBatch(
                string cellId,
                VegetationTileRecord tile,
                VegetationProfile profile,
                VegetationInstanceRecord[] instances)
            {
                CellId = cellId;
                TileX = tile.TileX;
                TileZ = tile.TileZ;
                Bounds = tile.WorldBounds;
                Profile = profile;
                instanceData = instances;
            }

            public string CellId { get; }
            public int TileX { get; }
            public int TileZ { get; }
            public Bounds Bounds { get; }
            public VegetationProfile Profile { get; }
            public int InstanceCount => instanceData.Length;
            public GraphicsBuffer InstanceBuffer { get; private set; }
            public GraphicsBuffer Arguments { get; private set; }
            public MaterialPropertyBlock PropertyBlock { get; private set; }
            public bool IsReady => Arguments != null && uploadedCount == InstanceCount;
            public int BufferCount => (InstanceBuffer != null ? 1 : 0) + (Arguments != null ? 1 : 0);

            public int UploadNext(int maximumInstances)
            {
                if (IsReady || maximumInstances <= 0) return 0;
                if (InstanceBuffer == null)
                {
                    InstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                        InstanceCount, VegetationInstanceRecord.Stride);
                    InstanceBuffer.name = $"Vegetation Instances {CellId} {TileX},{TileZ} {Profile.ProfileId}";
                    PropertyBlock = new MaterialPropertyBlock();
                }

                int count = Math.Min(maximumInstances, InstanceCount - uploadedCount);
                // A large authored tile is uploaded in bounded slices; it becomes
                // drawable only when its entire instance range is initialized.
                InstanceBuffer.SetData(instanceData, uploadedCount, uploadedCount, count);
                uploadedCount += count;
                if (uploadedCount == InstanceCount)
                {
                    var drawArguments = new GraphicsBuffer.IndirectDrawIndexedArgs[3];
                    for (int lodIndex = 0; lodIndex < drawArguments.Length; lodIndex++)
                    {
                        Mesh mesh = Profile.GetLodMesh(lodIndex);
                        if (mesh == null) continue;
                        drawArguments[lodIndex] = new GraphicsBuffer.IndirectDrawIndexedArgs
                        {
                            indexCountPerInstance = mesh.GetIndexCount(0),
                            instanceCount = (uint)InstanceCount,
                            startIndex = mesh.GetIndexStart(0),
                            baseVertexIndex = mesh.GetBaseVertex(0),
                            startInstance = 0
                        };
                    }

                    Arguments = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,
                        drawArguments.Length, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                    Arguments.name = $"Vegetation Args {CellId} {TileX},{TileZ} {Profile.ProfileId}";
                    Arguments.SetData(drawArguments);
                }
                return count;
            }

            public void Dispose()
            {
                InstanceBuffer?.Dispose();
                Arguments?.Dispose();
                InstanceBuffer = null;
                Arguments = null;
                PropertyBlock = null;
                uploadedCount = 0;
            }
        }
    }
}
