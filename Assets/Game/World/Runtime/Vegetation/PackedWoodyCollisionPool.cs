using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.World.Vegetation
{
    /// <summary>
    /// Lazily maintains a bounded set of upright trunk capsules around the game
    /// camera. Serialized cell scenes contain only compact records; pooled child
    /// objects are created at runtime and are never a second physics authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PackedWoodyCollisionPool : MonoBehaviour
    {
        public const float DefaultActivationRadiusMeters = 80f;
        public const float DefaultReleaseRadiusMeters = 112f;
        public const int DefaultMaximumColliderCount = 512;
        public const int DefaultMaximumMutationsPerFixedUpdate = 48;
        public const float DefaultMovementRefreshThresholdMeters = 4f;
        public const int GlobalMaximumMutationsPerFixedUpdate = 48;
        public const int GlobalMaximumPoolRefreshesPerFixedUpdate = 3;

        private static readonly ProfilerMarker refreshMarker =
            new ProfilerMarker("MSC.Vegetation.RefreshWoodyCollisionPool");
        private static readonly List<PackedWoodyCollisionPool> enabledPools =
            new List<PackedWoodyCollisionPool>(16);
        private static long lastProcessedFixedStep = long.MinValue;
        private static int nextPoolIndex;
        private static long testFixedStep;
        private static WeakReference<Camera> authoritativeGameCamera;
        private static bool warnedAboutAmbiguousGameCamera;

        public static int LastGlobalMutationCount { get; private set; }
        public static int LastGlobalEligiblePoolCount { get; private set; }

        [SerializeField] private PackedWoodyCellAsset cellAsset;
        [SerializeField, Min(10f)] private float activationRadiusMeters =
            DefaultActivationRadiusMeters;
        [SerializeField, Min(11f)] private float releaseRadiusMeters =
            DefaultReleaseRadiusMeters;
        [SerializeField, Range(1, DefaultMaximumColliderCount)]
        private int maximumColliderCount = DefaultMaximumColliderCount;
        [SerializeField, Range(1, 128)]
        private int maximumMutationsPerFixedUpdate =
            DefaultMaximumMutationsPerFixedUpdate;

        private ColliderSlot[] slots = Array.Empty<ColliderSlot>();
        private int[] desiredRecordIndices = Array.Empty<int>();
        private float[] desiredSquaredDistances = Array.Empty<float>();
        private readonly Dictionary<Hash128, int> activeByStableId =
            new Dictionary<Hash128, int>(DefaultMaximumColliderCount);
        private readonly HashSet<int> desiredRecordSet =
            new HashSet<int>(DefaultMaximumColliderCount);
        private Vector3 trackedPosition;
        private bool hasTrackedPosition;
        private Camera fallbackGameCamera;
        private Vector3 lastRefreshPosition;
        private bool hasRefreshPosition;
        private bool hasPendingMutations;
        private bool warnedAboutOverflow;

        public PackedWoodyCellAsset CellAsset => cellAsset;
        public int ActiveColliderCount => activeByStableId.Count;
        public int CreatedColliderCount { get; private set; }
        public int LastCandidateCount { get; private set; }
        public int LastOverflowCandidateCount { get; private set; }
        public int LastCandidateRecordExaminationCount { get; private set; }
        public int RefreshExecutionCount { get; private set; }
        public int TotalMutationCount { get; private set; }
        public float ActivationRadiusMeters => activationRadiusMeters;
        public float ReleaseRadiusMeters => releaseRadiusMeters;
        public int MaximumColliderCount => maximumColliderCount;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            string error = cellAsset == null
                ? "cell asset is missing" : string.Empty;
            if (cellAsset == null ||
                !cellAsset.TryValidateRuntimeCollisionHeader(out error))
            {
                Debug.LogError(
                    "PACKED_WOODY_COLLISION_INVALID object=" + name + ": " +
                    error,
                    this);
                enabled = false;
                return;
            }
            maximumColliderCount = Mathf.Clamp(
                maximumColliderCount, 1, DefaultMaximumColliderCount);
            releaseRadiusMeters = Mathf.Max(
                releaseRadiusMeters, activationRadiusMeters + 1f);
            slots = new ColliderSlot[maximumColliderCount];
            desiredRecordIndices = new int[maximumColliderCount];
            desiredSquaredDistances = new float[maximumColliderCount];
            activeByStableId.Clear();
            desiredRecordSet.Clear();
            hasRefreshPosition = false;
            hasPendingMutations = false;
            fallbackGameCamera = null;
            if (!enabledPools.Contains(this)) enabledPools.Add(this);
            RenderPipelineManager.beginCameraRendering +=
                OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            for (int index = 0; index < slots.Length; index++)
                if (slots[index] != null && slots[index].Owner != null)
                    slots[index].Owner.SetActive(false);
            activeByStableId.Clear();
            int registeredIndex = enabledPools.IndexOf(this);
            if (registeredIndex >= 0)
            {
                enabledPools.RemoveAt(registeredIndex);
                if (registeredIndex < nextPoolIndex) nextPoolIndex--;
                if (nextPoolIndex >= enabledPools.Count) nextPoolIndex = 0;
            }
            hasTrackedPosition = false;
            hasRefreshPosition = false;
            hasPendingMutations = false;
            fallbackGameCamera = null;
        }

        private void OnBeginCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            ConsiderGameCamera(camera);
        }

        /// <summary>
        /// Binds the camera which represents the playable character for every
        /// resident packed-woody pool. The composition root may call this when
        /// more than one Game camera exists. No name, tag, hierarchy path or
        /// render order becomes physics authority.
        /// </summary>
        public static void BindAuthoritativeGameCamera(Camera camera)
        {
            if (camera != null && camera.cameraType != CameraType.Game)
                throw new ArgumentException(
                    "Packed woody collision authority must be a Game camera.",
                    nameof(camera));
            authoritativeGameCamera = camera != null
                ? new WeakReference<Camera>(camera)
                : null;
        }

        public static void ReleaseAuthoritativeGameCamera(Camera camera)
        {
            // A destroyed UnityEngine.Object compares equal to null. Use the
            // managed reference here so an old session's deferred OnDestroy
            // cannot clear a newer session's already-bound camera.
            if (ReferenceEquals(camera, null)) return;
            Camera current = GetAuthoritativeGameCamera();
            if (current == camera)
                authoritativeGameCamera = null;
        }

        private void ConsiderGameCamera(Camera camera)
        {
            if (!IsUsableGameCamera(camera, false)) return;

            Camera explicitCamera = GetAuthoritativeGameCamera();
            if (explicitCamera != null)
            {
                if (!IsUsableGameCamera(explicitCamera, true))
                    authoritativeGameCamera = null;
                else
                {
                    if (camera != explicitCamera) return;
                    TrackCamera(camera);
                    return;
                }
            }

            if (!IsUsableGameCamera(fallbackGameCamera, false))
            {
                fallbackGameCamera = camera;
                TrackCamera(camera);
                return;
            }
            if (camera == fallbackGameCamera)
            {
                TrackCamera(camera);
                return;
            }

            // Capture/minimap cameras normally render into a target texture.
            // Prefer one backbuffer Game camera, then keep it sticky. This
            // avoids the old last-rendered-camera-wins behaviour while still
            // allowing an explicitly bound target-texture gameplay camera.
            if (fallbackGameCamera.targetTexture != null &&
                camera.targetTexture == null)
            {
                fallbackGameCamera = camera;
                TrackCamera(camera);
                return;
            }
            if (fallbackGameCamera.targetTexture == null &&
                camera.targetTexture != null)
                return;

            if (!warnedAboutAmbiguousGameCamera)
            {
                warnedAboutAmbiguousGameCamera = true;
                Debug.LogWarning(
                    "PACKED_WOODY_COLLISION_CAMERA_AMBIGUOUS cell=" +
                    (cellAsset != null ? cellAsset.CellId : "unbound") +
                    ". Keeping the first stable Game camera. Bind the " +
                    "authoritative gameplay camera explicitly when multiple " +
                    "equivalent Game cameras are active.",
                    this);
            }
        }

        private bool IsUsableGameCamera(Camera camera, bool explicitlyBound)
        {
            return camera != null && camera.cameraType == CameraType.Game &&
                camera.enabled && camera.gameObject.activeInHierarchy &&
                (explicitlyBound ||
                 (camera.cullingMask & (1 << gameObject.layer)) != 0);
        }

        private static Camera GetAuthoritativeGameCamera()
        {
            if (authoritativeGameCamera != null &&
                authoritativeGameCamera.TryGetTarget(out Camera camera) &&
                camera != null)
                return camera;
            authoritativeGameCamera = null;
            return null;
        }

        private void TrackCamera(Camera camera)
        {
            trackedPosition = camera.transform.position;
            hasTrackedPosition = true;
        }

        private void FixedUpdate()
        {
            ProcessGlobalFixedStep(
                BitConverter.DoubleToInt64Bits(Time.fixedTimeAsDouble));
        }

        public void SetTrackedPositionForTests(Vector3 position)
        {
            trackedPosition = position;
            hasTrackedPosition = true;
        }

        public void RefreshImmediatelyForTests()
        {
            if (hasTrackedPosition) Refresh(trackedPosition, int.MaxValue);
        }

        public void TickImmediatelyForTests()
        {
            if (hasTrackedPosition && ShouldRefresh(trackedPosition))
                Refresh(trackedPosition, maximumMutationsPerFixedUpdate);
        }

        public static void TickAllPoolsImmediatelyForTests()
        {
            // Negative synthetic tokens can never collide with the bit pattern
            // used by ordinary positive FixedUpdate time in these tests.
            ProcessGlobalFixedStep(long.MinValue + ++testFixedStep);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGlobalCoordinator()
        {
            enabledPools.Clear();
            lastProcessedFixedStep = long.MinValue;
            nextPoolIndex = 0;
            testFixedStep = 0;
            LastGlobalMutationCount = 0;
            LastGlobalEligiblePoolCount = 0;
            authoritativeGameCamera = null;
            warnedAboutAmbiguousGameCamera = false;
        }

        private static void ProcessGlobalFixedStep(long fixedStep)
        {
            if (lastProcessedFixedStep == fixedStep) return;
            lastProcessedFixedStep = fixedStep;
            LastGlobalMutationCount = 0;
            LastGlobalEligiblePoolCount = 0;
            int count = enabledPools.Count;
            if (count == 0) return;
            if (nextPoolIndex >= count) nextPoolIndex = 0;

            int index = nextPoolIndex;
            int visited = 0;
            int refreshed = 0;
            int remainingBudget = GlobalMaximumMutationsPerFixedUpdate;
            while (visited < count && remainingBudget > 0 &&
                refreshed < GlobalMaximumPoolRefreshesPerFixedUpdate)
            {
                PackedWoodyCollisionPool pool = enabledPools[index];
                index = (index + 1) % count;
                visited++;
                if (pool == null || !pool.isActiveAndEnabled ||
                    !pool.hasTrackedPosition ||
                    !pool.ShouldRefresh(pool.trackedPosition))
                    continue;

                LastGlobalEligiblePoolCount++;
                refreshed++;
                int before = pool.TotalMutationCount;
                pool.Refresh(
                    pool.trackedPosition,
                    Mathf.Min(pool.maximumMutationsPerFixedUpdate,
                        remainingBudget));
                int consumed = Mathf.Clamp(
                    pool.TotalMutationCount - before,
                    0,
                    remainingBudget);
                remainingBudget -= consumed;
                LastGlobalMutationCount += consumed;
            }
            // If one dense pool consumed the budget, the next step starts at
            // its successor. Sparse/settled pools cost only the O(1) predicate.
            nextPoolIndex = index;
        }

        private bool ShouldRefresh(Vector3 position)
        {
            if (!hasRefreshPosition || hasPendingMutations) return true;
            return HorizontalSqrDistance(position, lastRefreshPosition) >=
                DefaultMovementRefreshThresholdMeters *
                DefaultMovementRefreshThresholdMeters;
        }

        private void Refresh(Vector3 position, int mutationBudget)
        {
            if (cellAsset == null ||
                cellAsset.Category != PackedWoodyCategory.OriginalTree ||
                mutationBudget <= 0)
                return;

            using (refreshMarker.Auto())
            {
                RefreshExecutionCount++;
                lastRefreshPosition = position;
                hasRefreshPosition = true;
                int mutations = 0;
                float releaseSquared = releaseRadiusMeters * releaseRadiusMeters;
                if (HorizontalSqrDistance(cellAsset.WorldBounds, position) >
                    releaseSquared)
                {
                    for (int slotIndex = 0;
                         slotIndex < slots.Length && mutations < mutationBudget;
                         slotIndex++)
                    {
                        if (slots[slotIndex] == null || !slots[slotIndex].Active)
                            continue;
                        DisableSlot(slotIndex);
                        mutations++;
                    }
                    LastCandidateCount = 0;
                    LastOverflowCandidateCount = 0;
                    LastCandidateRecordExaminationCount = 0;
                    hasPendingMutations = activeByStableId.Count != 0;
                    TotalMutationCount += mutations;
                    return;
                }

                int desiredCount = CollectNearestCandidates(position);
                PackedWoodyCollisionRecord[] records =
                    cellAsset.CollisionRecordData;

                // Release capsules beyond hysteresis first. If capacity is full,
                // stale in-range records which are no longer among the nearest
                // reviewed set are also retired gradually.
                for (int slotIndex = 0;
                     slotIndex < slots.Length && mutations < mutationBudget;
                     slotIndex++)
                {
                    ColliderSlot slot = slots[slotIndex];
                    if (slot == null || !slot.Active) continue;
                    PackedWoodyCollisionRecord record = records[slot.RecordIndex];
                    float squaredDistance = HorizontalSqrDistance(
                        record.BottomCenter, position);
                    bool desired = desiredRecordSet.Contains(slot.RecordIndex);
                    if (squaredDistance <= releaseSquared &&
                        (desired || activeByStableId.Count < maximumColliderCount))
                        continue;
                    DisableSlot(slotIndex);
                    mutations++;
                }

                for (int desiredIndex = 0;
                     desiredIndex < desiredCount && mutations < mutationBudget;
                     desiredIndex++)
                {
                    int recordIndex = desiredRecordIndices[desiredIndex];
                    PackedWoodyCollisionRecord record = records[recordIndex];
                    if (activeByStableId.ContainsKey(record.StableIdHash))
                        continue;
                    int slotIndex = FindFreeSlot();
                    if (slotIndex < 0) break;
                    EnableSlot(slotIndex, recordIndex, record);
                    mutations++;
                }
                hasPendingMutations = false;
                for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                {
                    ColliderSlot slot = slots[slotIndex];
                    if (slot == null || !slot.Active) continue;
                    PackedWoodyCollisionRecord record = records[slot.RecordIndex];
                    if (HorizontalSqrDistance(record.BottomCenter, position) <=
                        releaseSquared)
                        continue;
                    hasPendingMutations = true;
                    break;
                }
                for (int desiredIndex = 0;
                     !hasPendingMutations && desiredIndex < desiredCount;
                     desiredIndex++)
                {
                    if (activeByStableId.ContainsKey(
                        records[desiredRecordIndices[desiredIndex]].StableIdHash))
                        continue;
                    hasPendingMutations = true;
                    break;
                }
                TotalMutationCount += mutations;
            }
        }

        private int CollectNearestCandidates(Vector3 position)
        {
            PackedWoodyCollisionTile[] tiles = cellAsset.CollisionTileData;
            PackedWoodyCollisionRecord[] records =
                cellAsset.CollisionRecordData;
            float activationSquared =
                activationRadiusMeters * activationRadiusMeters;
            int desiredCount = 0;
            int candidateCount = 0;
            int examinedCount = 0;

            for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
            {
                PackedWoodyCollisionTile tile = tiles[tileIndex];
                if (HorizontalSqrDistance(tile.WorldBounds, position) >
                    activationSquared)
                    continue;
                int end = tile.StartIndex + tile.Count;
                for (int recordIndex = tile.StartIndex;
                     recordIndex < end; recordIndex++)
                {
                    examinedCount++;
                    float squaredDistance = HorizontalSqrDistance(
                        records[recordIndex].BottomCenter, position);
                    if (squaredDistance > activationSquared) continue;
                    candidateCount++;
                    if (desiredCount < maximumColliderCount)
                    {
                        PushCandidateMaxHeap(
                            recordIndex, squaredDistance, ref desiredCount);
                        continue;
                    }
                    if (squaredDistance >= desiredSquaredDistances[0])
                        continue;
                    desiredRecordIndices[0] = recordIndex;
                    desiredSquaredDistances[0] = squaredDistance;
                    RestoreCandidateMaxHeap(0, desiredCount);
                }
            }

            LastCandidateCount = candidateCount;
            LastCandidateRecordExaminationCount = examinedCount;
            LastOverflowCandidateCount = Mathf.Max(
                0, candidateCount - maximumColliderCount);
            desiredRecordSet.Clear();
            for (int index = 0; index < desiredCount; index++)
                desiredRecordSet.Add(desiredRecordIndices[index]);
            if (LastOverflowCandidateCount > 0 && !warnedAboutOverflow)
            {
                warnedAboutOverflow = true;
                Debug.LogWarning(
                    "PACKED_WOODY_COLLISION_POOL_CAP cell=" +
                    cellAsset.CellId + " candidates=" + candidateCount +
                    " cap=" + maximumColliderCount +
                    " overflow=" + LastOverflowCandidateCount +
                    ". The nearest records remain authoritative; report this " +
                    "density instead of silently creating unbounded colliders.",
                    this);
            }
            return desiredCount;
        }

        private void PushCandidateMaxHeap(
            int recordIndex,
            float squaredDistance,
            ref int count)
        {
            int index = count++;
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (desiredSquaredDistances[parent] >= squaredDistance) break;
                desiredRecordIndices[index] = desiredRecordIndices[parent];
                desiredSquaredDistances[index] =
                    desiredSquaredDistances[parent];
                index = parent;
            }
            desiredRecordIndices[index] = recordIndex;
            desiredSquaredDistances[index] = squaredDistance;
        }

        private void RestoreCandidateMaxHeap(int index, int count)
        {
            int recordIndex = desiredRecordIndices[index];
            float squaredDistance = desiredSquaredDistances[index];
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= count) break;
                int right = left + 1;
                int child = right < count &&
                    desiredSquaredDistances[right] >
                    desiredSquaredDistances[left]
                        ? right : left;
                if (desiredSquaredDistances[child] <= squaredDistance) break;
                desiredRecordIndices[index] = desiredRecordIndices[child];
                desiredSquaredDistances[index] =
                    desiredSquaredDistances[child];
                index = child;
            }
            desiredRecordIndices[index] = recordIndex;
            desiredSquaredDistances[index] = squaredDistance;
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < slots.Length; index++)
                if (slots[index] == null || !slots[index].Active) return index;
            return -1;
        }

        private void EnableSlot(
            int slotIndex,
            int recordIndex,
            PackedWoodyCollisionRecord record)
        {
            ColliderSlot slot = slots[slotIndex];
            if (slot == null)
            {
                var owner = new GameObject("Pooled Trunk Collision");
                owner.transform.SetParent(transform, false);
                owner.layer = gameObject.layer;
                var capsule = owner.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                slot = new ColliderSlot(owner, capsule);
                slots[slotIndex] = slot;
                CreatedColliderCount++;
            }
            slot.RecordIndex = recordIndex;
            slot.StableId = record.StableIdHash;
            slot.Owner.transform.SetPositionAndRotation(
                record.BottomCenter, Quaternion.identity);
            slot.Owner.transform.localScale = Vector3.one;
            slot.Capsule.center = new Vector3(
                0f, record.CapsuleHeight * 0.5f, 0f);
            slot.Capsule.height = record.CapsuleHeight;
            slot.Capsule.radius = record.CapsuleRadius;
            slot.Owner.SetActive(true);
            slot.Active = true;
            activeByStableId.Add(slot.StableId, slotIndex);
        }

        private void DisableSlot(int slotIndex)
        {
            ColliderSlot slot = slots[slotIndex];
            activeByStableId.Remove(slot.StableId);
            slot.Active = false;
            slot.RecordIndex = -1;
            slot.Owner.SetActive(false);
        }

        private static float HorizontalSqrDistance(
            Vector3 point,
            Vector3 position)
        {
            float x = point.x - position.x;
            float z = point.z - position.z;
            return x * x + z * z;
        }

        private static float HorizontalSqrDistance(
            Bounds bounds,
            Vector3 position)
        {
            float x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x) -
                position.x;
            float z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z) -
                position.z;
            return x * x + z * z;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(PackedWoodyCellAsset configuredAsset)
        {
            cellAsset = configuredAsset;
        }

        public void ConfigurePoolForTests(
            float activationRadius,
            float releaseRadius,
            int maximumColliders,
            int maximumMutations)
        {
            activationRadiusMeters = activationRadius;
            releaseRadiusMeters = releaseRadius;
            maximumColliderCount = maximumColliders;
            maximumMutationsPerFixedUpdate = maximumMutations;
        }

        public void ConsiderGameCameraForTests(Camera camera) =>
            ConsiderGameCamera(camera);

        public Vector3 TrackedPositionForTests => trackedPosition;
#endif

        private sealed class ColliderSlot
        {
            public readonly GameObject Owner;
            public readonly CapsuleCollider Capsule;
            public Hash128 StableId;
            public int RecordIndex = -1;
            public bool Active;

            public ColliderSlot(GameObject owner, CapsuleCollider capsule)
            {
                Owner = owner;
                Capsule = capsule;
            }
        }
    }
}
