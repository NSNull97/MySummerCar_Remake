using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.World.Vegetation
{
    /// <summary>
    /// Draws an authored packed woody cell directly from compact matrix batches.
    /// The source HDRP materials remain authoritative; this renderer never routes
    /// trees through the flat grass/indirect shader.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PackedWoodyCellRenderer : MonoBehaviour
    {
        public const float MatureTreeMinimumNearDistanceMeters = 80f;
        public const float TreeMinimumFarDistanceMeters = 220f;
        public const float TreeMaximumDrawDistanceMeters = 900f;
        public const float ShrubMaximumDrawDistanceMeters = 180f;

        // Packed batches currently submit Matrix4x4[] instance data, which has
        // no prevObjectToWorld member. Requesting per-object vectors in that
        // configuration gives HDRP no valid previous instance transform and
        // produces the large radial TAA/motion-blur streaks seen after the v12
        // migration. Placements are static while their cell is resident, so
        // camera-only vectors are the correct contract for every packed LOD.
        public const MotionVectorGenerationMode PackedMatrixMotionVectorMode =
            MotionVectorGenerationMode.Camera;

        private static readonly ProfilerMarker renderMarker =
            new ProfilerMarker("MSC.Vegetation.PackedWoodyCullAndDraw");

        [SerializeField] private PackedWoodyCellAsset cellAsset;
        [SerializeField] private bool renderInSceneView = true;
        [SerializeField] private bool renderInGameView = true;
        [SerializeField] private int renderingLayerMask = 1;

        private readonly Plane[] frustumPlanes = new Plane[6];
        private Matrix4x4[][] submissionMatrices =
            Array.Empty<Matrix4x4[]>();
        private int[] submissionCounts = Array.Empty<int>();
        private Bounds[] submissionBounds = Array.Empty<Bounds>();
        private bool[] submissionHasBounds = Array.Empty<bool>();
        private sbyte runtimeValidationState;

        public PackedWoodyCellAsset CellAsset => cellAsset;
        public int SourceBatchCount => cellAsset != null
            ? cellAsset.BatchCount : 0;
        public int SourceInstanceCount => cellAsset != null
            ? cellAsset.InstanceCount : 0;
        public int CameraRenderCallbackCount { get; private set; }
        public int LastVisibleBatchCount { get; private set; }
        public int LastDrawCallCount { get; private set; }

        private void OnEnable()
        {
            runtimeValidationState = 0;
            if (Application.isPlaying && !EnsureRuntimeConfiguration()) return;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        private void OnBeginCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            if (!enabled || camera == null ||
                !EnsureRuntimeConfiguration() ||
                (camera.cullingMask & (1 << gameObject.layer)) == 0 ||
                (camera.cameraType == CameraType.SceneView &&
                    (!renderInSceneView || Application.isPlaying)) ||
                (!renderInGameView && camera.cameraType == CameraType.Game))
                return;

            using (renderMarker.Auto())
            {
                CameraRenderCallbackCount++;
                LastVisibleBatchCount = 0;
                LastDrawCallCount = 0;
                Vector3 cameraPosition = camera.transform.position;
                float maximumDrawDistance =
                    cellAsset.Category == PackedWoodyCategory.ShrubOrUndergrowth
                        ? ShrubMaximumDrawDistanceMeters
                        : TreeMaximumDrawDistanceMeters;
                if (cellAsset.WorldBounds.SqrDistance(cameraPosition) >
                    maximumDrawDistance * maximumDrawDistance)
                    return;

                GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
                PackedWoodyBatch[] batches = cellAsset.BatchData;
                PackedWoodyPrototypeAsset[] prototypes =
                    cellAsset.PrototypeData;
                EnsureSubmissionBuffers(prototypes);
                ResetSubmissionGroups();
                int activePrototypeIndex = -1;
                for (int batchIndex = 0; batchIndex < batches.Length; batchIndex++)
                {
                    PackedWoodyBatch batch = batches[batchIndex];
                    if (batch == null || batch.Count == 0 ||
                        batch.WorldBounds.SqrDistance(cameraPosition) >
                        maximumDrawDistance * maximumDrawDistance ||
                        !GeometryUtility.TestPlanesAABB(
                            frustumPlanes, batch.WorldBounds))
                        continue;

                    PackedWoodyPrototypeAsset prototype =
                        prototypes[batch.PrototypeIndex];
                    // Use the nearest point of the tight 32 m batch bounds. A
                    // tree on the camera-facing edge therefore cannot be pushed
                    // into a lower LOD merely because the batch centre is farther.
                    float distance = Mathf.Sqrt(
                        batch.WorldBounds.SqrDistance(cameraPosition));
                    int lodIndex = SelectLod(
                        prototype, batch.MaximumHeightMeters, distance, camera);
                    if (lodIndex < 0) continue;

                    LastVisibleBatchCount++;
                    if (activePrototypeIndex != batch.PrototypeIndex)
                    {
                        if (activePrototypeIndex >= 0)
                        {
                            FlushSubmissionGroups(
                                prototypes[activePrototypeIndex],
                                camera);
                        }
                        activePrototypeIndex = batch.PrototypeIndex;
                    }

                    AppendBatch(prototype, lodIndex, batch, camera);
                }
                if (activePrototypeIndex >= 0)
                {
                    FlushSubmissionGroups(
                        prototypes[activePrototypeIndex],
                        camera);
                }
            }
        }

        private void EnsureSubmissionBuffers(
            PackedWoodyPrototypeAsset[] prototypes)
        {
            int maximumLodCount = 0;
            for (int index = 0; index < prototypes.Length; index++)
            {
                maximumLodCount = Mathf.Max(
                    maximumLodCount,
                    prototypes[index].LodData.Length);
            }
            if (submissionMatrices.Length >= maximumLodCount) return;

            submissionMatrices = new Matrix4x4[maximumLodCount][];
            submissionCounts = new int[maximumLodCount];
            submissionBounds = new Bounds[maximumLodCount];
            submissionHasBounds = new bool[maximumLodCount];
            for (int index = 0; index < maximumLodCount; index++)
            {
                submissionMatrices[index] =
                    new Matrix4x4[PackedWoodyBatch.MaximumInstanceCount];
            }
        }

        private void ResetSubmissionGroups()
        {
            Array.Clear(submissionCounts, 0, submissionCounts.Length);
            Array.Clear(submissionHasBounds, 0, submissionHasBounds.Length);
        }

        private void AppendBatch(
            PackedWoodyPrototypeAsset prototype,
            int lodIndex,
            PackedWoodyBatch batch,
            Camera camera)
        {
            Matrix4x4[] source = batch.MatrixData;
            int sourceIndex = 0;
            while (sourceIndex < source.Length)
            {
                int available = PackedWoodyBatch.MaximumInstanceCount -
                    submissionCounts[lodIndex];
                if (available == 0)
                {
                    FlushSubmissionGroup(prototype, lodIndex, camera);
                    available = PackedWoodyBatch.MaximumInstanceCount;
                }

                int copyCount = Mathf.Min(
                    available,
                    source.Length - sourceIndex);
                Array.Copy(
                    source,
                    sourceIndex,
                    submissionMatrices[lodIndex],
                    submissionCounts[lodIndex],
                    copyCount);
                submissionCounts[lodIndex] += copyCount;
                sourceIndex += copyCount;
                if (submissionHasBounds[lodIndex])
                    submissionBounds[lodIndex].Encapsulate(batch.WorldBounds);
                else
                {
                    submissionBounds[lodIndex] = batch.WorldBounds;
                    submissionHasBounds[lodIndex] = true;
                }

                if (submissionCounts[lodIndex] ==
                    PackedWoodyBatch.MaximumInstanceCount)
                {
                    FlushSubmissionGroup(prototype, lodIndex, camera);
                }
            }
        }

        private void FlushSubmissionGroups(
            PackedWoodyPrototypeAsset prototype,
            Camera camera)
        {
            for (int lodIndex = 0;
                 lodIndex < prototype.LodData.Length;
                 lodIndex++)
            {
                FlushSubmissionGroup(prototype, lodIndex, camera);
            }
        }

        private void FlushSubmissionGroup(
            PackedWoodyPrototypeAsset prototype,
            int lodIndex,
            Camera camera)
        {
            int count = submissionCounts[lodIndex];
            if (count == 0) return;

            PackedWoodyLod lod = prototype.LodData[lodIndex];
            PackedWoodyDrawPart[] parts = lod.DrawPartData;
            for (int partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                PackedWoodyDrawPart part = parts[partIndex];
                ShadowCastingMode shadows = lodIndex == 0
                    ? part.ShadowCasting
                    : ShadowCastingMode.Off;
                var renderParams = new RenderParams(part.Material)
                {
                    camera = camera,
                    layer = gameObject.layer,
                    renderingLayerMask =
                        (uint)Mathf.Max(1, renderingLayerMask),
                    worldBounds = submissionBounds[lodIndex],
                    shadowCastingMode = shadows,
                    receiveShadows = part.ReceiveShadows,
                    // Per-instance probe interpolation allocates/does CPU work in
                    // the camera callback. Outdoor trees use sky/ambient lighting.
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                    motionVectorMode = PackedMatrixMotionVectorMode
                };
                Graphics.RenderMeshInstanced(
                    renderParams,
                    part.Mesh,
                    part.SubMeshIndex,
                    submissionMatrices[lodIndex],
                    count);
                LastDrawCallCount++;
            }

            submissionCounts[lodIndex] = 0;
            submissionHasBounds[lodIndex] = false;
        }

        private bool EnsureRuntimeConfiguration()
        {
            if (runtimeValidationState > 0) return true;
            if (runtimeValidationState < 0) return false;
            // AddComponent invokes OnEnable before the Editor generator can
            // assign the asset. Defer this harmless authoring state; Player
            // activation must always reject it.
            if (cellAsset == null && !Application.isPlaying) return false;
            string error = cellAsset == null
                ? "cell asset is missing" : string.Empty;
            if (cellAsset == null ||
                !cellAsset.TryValidateRuntimeRenderingHeader(out error))
            {
                runtimeValidationState = -1;
                Debug.LogError(
                    "PACKED_WOODY_RENDERER_INVALID object=" + name + ": " +
                    error,
                    this);
                if (Application.isPlaying) enabled = false;
                return false;
            }
            runtimeValidationState = 1;
            return true;
        }

        public static int SelectLod(
            PackedWoodyPrototypeAsset prototype,
            float maximumHeightMeters,
            float distanceMeters,
            Camera camera)
        {
            if (prototype == null || camera == null || prototype.Lods.Count == 0)
                return -1;

            bool tree = prototype.Species <= PackedWoodySpecies.Aspen;
            if (tree && distanceMeters <= MatureTreeMinimumNearDistanceMeters)
                return 0;

            float relativeHeight;
            if (camera.orthographic)
            {
                relativeHeight = maximumHeightMeters /
                    Mathf.Max(0.01f, camera.orthographicSize * 2f);
            }
            else
            {
                float denominator = Mathf.Max(0.01f,
                    2f * Mathf.Max(distanceMeters, 0.01f) *
                    Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
                relativeHeight = maximumHeightMeters / denominator;
            }
            relativeHeight *= Mathf.Max(0.01f, QualitySettings.lodBias);

            PackedWoodyLod[] lods = prototype.LodData;
            int selected = -1;
            for (int index = 0; index < lods.Length; index++)
            {
                if (relativeHeight >= lods[index].ScreenRelativeTransitionHeight)
                {
                    selected = index;
                    break;
                }
            }
            if (selected < 0) return -1;

            // The last authored tree stage is commonly a billboard. It remains
            // unavailable in the near/mid field even if an importer threshold is
            // accidentally aggressive. This guard fixes the reviewed near-tree
            // "log trunk" regression at the renderer boundary as well as in art.
            if (tree && lods.Length > 1 &&
                selected == lods.Length - 1 &&
                distanceMeters < TreeMinimumFarDistanceMeters)
                selected = lods.Length - 2;
            return selected;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(PackedWoodyCellAsset configuredAsset)
        {
            cellAsset = configuredAsset;
        }
#endif
    }
}
