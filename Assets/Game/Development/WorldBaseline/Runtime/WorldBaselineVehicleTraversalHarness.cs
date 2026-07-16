#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MSC.Bootstrap;
using MSC.LegacyImport;
using MSC.Vehicle;
using MSC.World.Partition;
using MSC.World.Streaming;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MSC.Development.WorldBaseline
{
    /// <summary>
    /// Development-only 06B3 bridge between the accepted M06 vehicle prototype
    /// and the active donor-world streaming profile. It mutates only the loaded
    /// runtime instances and never writes back to the source scenes.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class WorldBaselineVehicleTraversalHarness : MonoBehaviour
    {
        public const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        public const string VehiclePrototypeScenePath =
            "Assets/Game/Vehicle/Content/Simulation/Scenes/VehicleSimulationPrototype.unity";
        public const string HarnessScenePath =
            "Assets/Game/Development/WorldBaseline/Scenes/WorldBaselineVehicleTraversalHarness.unity";
        public const string PrototypeTrackName = "M06_BoundedSurfaceTrack";
        public const string PrototypeLightName = "M06_DirectionalLight";
        public const int RequiredStreamingBoundaryCount = 3;

        private const float InitializationTimeoutSeconds = 120f;
        private const float SurfaceSearchRadiusMeters = 128f;
        private const float SurfaceSearchStepMeters = 4f;
        private const float MinimumUpwardNormal = 0.55f;
        private const float VehicleSurfaceClearanceMeters = 0.75f;
        private const int RaycastBufferSize = 64;

        [SerializeField] private string[] allowedTraversalColliderIds =
            Array.Empty<string>();

        private readonly HashSet<string> consecutiveLoadedCells =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> allObservedLoadedCells =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> loadedCellTransitionHistory =
            new List<string>();
        private readonly List<string> longestConsecutiveCellSequence =
            new List<string>();
        private readonly RaycastHit[] raycastBuffer =
            new RaycastHit[RaycastBufferSize];

        private ProductionWorldStreamingInstaller installer;
        private ProductionWorldStreamingService streaming;
        private VehicleSimulationHost vehicleHost;
        private VehicleResetController resetController;
        private Rigidbody chassis;
        private WorldOutOfBoundsRecovery vehicleRecovery;
        private string initializationFailure = string.Empty;
        private string selectedColliderStableId = string.Empty;
        private string currentCellId = string.Empty;
        private WorldCellIndex lastAcceptedCellIndex;
        private bool hasAcceptedCellIndex;
        private int observedBoundaryCount;
        private bool boundaryGoalReached;
        private bool wasStreaming;
        private int streamingRefreshCount;
        private float peakFrameMilliseconds;
        private float peakStreamingFrameMilliseconds;
        private float peakSpeedKph;
        private string evidenceExportPath = string.Empty;

        public IReadOnlyList<string> AllowedTraversalColliderIds =>
            allowedTraversalColliderIds ?? Array.Empty<string>();
        public bool IsReady { get; private set; }
        public bool HasFailed => !string.IsNullOrWhiteSpace(initializationFailure);
        public string InitializationFailure => initializationFailure;
        public string SelectedColliderStableId => selectedColliderStableId;
        public string CurrentCellId => currentCellId;
        public int ObservedCellCount => allObservedLoadedCells.Count;
        public int ConsecutiveLoadedCellCount =>
            consecutiveLoadedCells.Count;
        public int ObservedBoundaryCount => observedBoundaryCount;
        public bool BoundaryGoalReached => boundaryGoalReached;
        public IReadOnlyList<string> LongestConsecutiveCellSequence =>
            longestConsecutiveCellSequence;
        public float PeakFrameMilliseconds => peakFrameMilliseconds;
        public float PeakStreamingFrameMilliseconds =>
            peakStreamingFrameMilliseconds;
        public int StreamingRefreshCount => streamingRefreshCount;
        public string EvidenceExportPath => evidenceExportPath;
        public Rigidbody Chassis => chassis;
        public WorldOutOfBoundsRecovery VehicleRecovery => vehicleRecovery;

        public void ConfigureForAuthoring(string[] traversalColliderIds)
        {
            allowedTraversalColliderIds = traversalColliderIds != null
                ? (string[])traversalColliderIds.Clone()
                : Array.Empty<string>();
        }

        private IEnumerator Start()
        {
            yield return Initialize();
        }

        private IEnumerator Initialize()
        {
            if (!ValidateLoadedSceneSet(out string sceneFailure))
            {
                Fail(sceneFailure);
                yield break;
            }

            double timeout =
                Time.realtimeSinceStartupAsDouble +
                InitializationTimeoutSeconds;
            while (Time.realtimeSinceStartupAsDouble < timeout)
            {
                ProductionWorldStreamingInstaller[] installers =
                    FindObjectsByType<ProductionWorldStreamingInstaller>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                if (installers.Length == 1 &&
                    installers[0].IsReady &&
                    !installers[0].WorldStreaming.IsStreaming)
                {
                    installer = installers[0];
                    streaming = installer.WorldStreaming;
                    break;
                }

                yield return null;
            }

            if (installer == null || streaming == null)
            {
                Fail(
                    "06B3 vehicle harness timed out waiting for exactly one " +
                    "ready ProductionWorldStreamingInstaller.");
                yield break;
            }

            if (installer.SpawnedPlayer == null)
            {
                Fail("06B3 vehicle harness found no Bootstrap-spawned player.");
                yield break;
            }

            installer.SpawnedPlayer.SetActive(false);

            string trackFailure = string.Empty;
            string lightFailure = string.Empty;
            if (!TryGetExactlyOnePrototypeObject(
                    PrototypeTrackName,
                    out GameObject prototypeTrack,
                    out trackFailure) ||
                !TryGetExactlyOnePrototypeObject(
                    PrototypeLightName,
                    out GameObject prototypeLight,
                    out lightFailure))
            {
                Fail(
                    !string.IsNullOrWhiteSpace(trackFailure)
                        ? trackFailure
                        : lightFailure);
                yield break;
            }

            prototypeTrack.SetActive(false);
            prototypeLight.SetActive(false);

            if (!TryGetExactlyOnePrototypeComponent(
                    out vehicleHost,
                    out string hostFailure))
            {
                Fail(hostFailure);
                yield break;
            }

            resetController =
                vehicleHost.GetComponent<VehicleResetController>();
            chassis = resetController != null
                ? resetController.Chassis
                : null;
            if (resetController == null || chassis == null)
            {
                Fail(
                    "06B3 vehicle harness requires the prototype host to own " +
                    "one configured VehicleResetController and chassis Rigidbody.");
                yield break;
            }

            bool originalKinematicState = chassis.isKinematic;
            chassis.isKinematic = true;
            chassis.linearVelocity = Vector3.zero;
            chassis.angularVelocity = Vector3.zero;

            Physics.SyncTransforms();
            if (!TryFindNearestTraversalSurface(
                    installer.PlayerSpawnPosition,
                    out RaycastHit surfaceHit,
                    out string surfaceFailure))
            {
                chassis.isKinematic = originalKinematicState;
                Fail(surfaceFailure);
                yield break;
            }

            Vector3 preferredForward =
                installer.PlayerSpawnRotation * Vector3.forward;
            Vector3 forward;
            if (!TryProbeSurfaceTangent(
                    surfaceHit,
                    preferredForward,
                    out forward))
            {
                forward =
                    Vector3.ProjectOnPlane(
                        preferredForward,
                        surfaceHit.normal);
                if (forward.sqrMagnitude < 0.001f)
                {
                    forward =
                        Vector3.Cross(
                            surfaceHit.normal,
                            Vector3.right);
                }

                Debug.LogWarning(
                    "06B3 vehicle harness could not derive a road tangent " +
                    "from same-collider probes; using the project-owned " +
                    "Bootstrap facing projected onto the surface.",
                    this);
            }

            Quaternion spawnRotation =
                Quaternion.LookRotation(
                    forward.normalized,
                    surfaceHit.normal);
            Vector3 spawnPosition =
                surfaceHit.point +
                surfaceHit.normal * VehicleSurfaceClearanceMeters;

            chassis.position = spawnPosition;
            chassis.rotation = spawnRotation;
            resetController.Configure(vehicleHost, chassis, null);
            resetController.SetSpawnPose(spawnPosition, spawnRotation);
            vehicleHost.ConfigureResetController(resetController);
            vehicleHost.ResetSimulation();

            vehicleRecovery =
                vehicleHost.GetComponent<WorldOutOfBoundsRecovery>();
            if (vehicleRecovery == null)
            {
                vehicleRecovery =
                    vehicleHost.gameObject
                        .AddComponent<WorldOutOfBoundsRecovery>();
            }
            vehicleRecovery.Configure(
                spawnPosition,
                spawnRotation,
                installer.OutOfBoundsMinimumY);

            streaming.BindFocus(chassis.transform);
            streaming.ReportFocusSpeedMetersPerSecond(0f);
            yield return streaming.RefreshNow();

            chassis.isKinematic = originalKinematicState;
            chassis.WakeUp();
            Physics.SyncTransforms();

            ObserveCurrentCell();
            IsReady = true;
            Debug.Log(
                "M06B3_VEHICLE_TRAVERSAL_HARNESS_READY " +
                $"collider={selectedColliderStableId} " +
                $"cell={currentCellId} spawn={spawnPosition:F3}",
                this);
        }

        private void Update()
        {
            if (!IsReady || chassis == null || streaming == null)
            {
                return;
            }

            streaming.ReportFocusSpeedMetersPerSecond(
                chassis.linearVelocity.magnitude);
            float frameMilliseconds =
                Time.unscaledDeltaTime * 1000f;
            if (float.IsFinite(frameMilliseconds))
            {
                peakFrameMilliseconds =
                    Mathf.Max(
                        peakFrameMilliseconds,
                        frameMilliseconds);
                if (streaming.IsStreaming)
                {
                    peakStreamingFrameMilliseconds =
                        Mathf.Max(
                            peakStreamingFrameMilliseconds,
                            frameMilliseconds);
                }
            }

            if (streaming.IsStreaming && !wasStreaming)
            {
                streamingRefreshCount++;
            }
            wasStreaming = streaming.IsStreaming;
            peakSpeedKph = Mathf.Max(
                peakSpeedKph,
                chassis.linearVelocity.magnitude * 3.6f);
            ObserveCurrentCell();

            if (Keyboard.current != null &&
                Keyboard.current.f8Key.wasPressedThisFrame)
            {
                ExportEvidence();
            }
        }

        private void ObserveCurrentCell()
        {
            ProductionWorldStreamingManifest manifest =
                streaming != null ? streaming.Manifest : null;
            if (manifest == null || chassis == null)
            {
                return;
            }

            WorldCellIndex nextCellIndex =
                WorldCellMembershipUtility.FromPosition(
                    chassis.position,
                    manifest.CellSizeMeters);
            string nextCellId = nextCellIndex.Id;
            if (!manifest.TryGetCell(nextCellId, out _))
            {
                currentCellId = nextCellId + " (outside manifest)";
                return;
            }

            currentCellId = nextCellId;
            if (!streaming.IsCellLoaded(nextCellId))
            {
                currentCellId += " (loading)";
                return;
            }

            allObservedLoadedCells.Add(nextCellId);
            if (!hasAcceptedCellIndex)
            {
                AcceptSequenceStart(nextCellIndex);
                return;
            }

            if (nextCellIndex.Equals(lastAcceptedCellIndex))
            {
                return;
            }

            int cellDistance = Mathf.Max(
                Mathf.Abs(nextCellIndex.X - lastAcceptedCellIndex.X),
                Mathf.Abs(nextCellIndex.Z - lastAcceptedCellIndex.Z));
            if (cellDistance != 1 ||
                consecutiveLoadedCells.Contains(nextCellId))
            {
                AcceptSequenceStart(nextCellIndex);
                return;
            }

            lastAcceptedCellIndex = nextCellIndex;
            consecutiveLoadedCells.Add(nextCellId);
            loadedCellTransitionHistory.Add(nextCellId);
            observedBoundaryCount++;
            UpdateLongestConsecutiveSequence();
            if (observedBoundaryCount >= RequiredStreamingBoundaryCount &&
                consecutiveLoadedCells.Count >=
                RequiredStreamingBoundaryCount + 1)
            {
                boundaryGoalReached = true;
            }
        }

        private void AcceptSequenceStart(WorldCellIndex cellIndex)
        {
            lastAcceptedCellIndex = cellIndex;
            hasAcceptedCellIndex = true;
            consecutiveLoadedCells.Clear();
            consecutiveLoadedCells.Add(cellIndex.Id);
            loadedCellTransitionHistory.Add(cellIndex.Id);
            observedBoundaryCount = 0;
            UpdateLongestConsecutiveSequence();
        }

        private void UpdateLongestConsecutiveSequence()
        {
            if (consecutiveLoadedCells.Count <=
                longestConsecutiveCellSequence.Count)
            {
                return;
            }

            longestConsecutiveCellSequence.Clear();
            int sequenceStart =
                Mathf.Max(
                    0,
                    loadedCellTransitionHistory.Count -
                    consecutiveLoadedCells.Count);
            for (int index = sequenceStart;
                 index < loadedCellTransitionHistory.Count;
                 index++)
            {
                longestConsecutiveCellSequence.Add(
                    loadedCellTransitionHistory[index]);
            }
        }

        private void ExportEvidence()
        {
            try
            {
                string path = ResolveEvidencePath();
                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    throw new InvalidOperationException(
                        "06B3 evidence path has no parent directory.");
                }

                Directory.CreateDirectory(directory);
                var evidence = new VehicleTraversalEvidence
                {
                    schemaVersion = 1,
                    capturedUtc = DateTime.UtcNow.ToString("O"),
                    baselineRevisionId = "DonorWorldBaseline-v001",
                    activeWorldProfileId = "donor-feature-parity-06b2",
                    harnessReady = IsReady,
                    boundaryGoalReached = BoundaryGoalReached,
                    selectedColliderStableId =
                        selectedColliderStableId,
                    currentCellId = currentCellId,
                    observedLoadedCellIds =
                        SortedCopy(allObservedLoadedCells),
                    loadedCellTransitionHistory =
                        loadedCellTransitionHistory.ToArray(),
                    longestConsecutiveCellSequence =
                        longestConsecutiveCellSequence.ToArray(),
                    longestConsecutiveBoundaryCount =
                        Mathf.Max(
                            0,
                            longestConsecutiveCellSequence.Count - 1),
                    streamingRefreshCount = streamingRefreshCount,
                    peakFrameMilliseconds = peakFrameMilliseconds,
                    peakStreamingFrameMilliseconds =
                        peakStreamingFrameMilliseconds,
                    peakSpeedKph = peakSpeedKph,
                    recoveryCount =
                        vehicleRecovery != null
                            ? vehicleRecovery.RecoveryCount
                            : 0,
                    chassisPosition =
                        chassis != null
                            ? chassis.position
                            : Vector3.zero,
                    manualCollisionAndVisualReviewRequired = true
                };
                string json =
                    JsonUtility.ToJson(evidence, prettyPrint: true) +
                    Environment.NewLine;
                File.WriteAllText(
                    path,
                    json,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false));
                evidenceExportPath = path;
                Debug.Log(
                    "M06B3_VEHICLE_TRAVERSAL_EVIDENCE_EXPORTED " +
                    path,
                    this);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "06B3 vehicle traversal evidence export failed: " +
                    exception.Message,
                    this);
            }
        }

        private static string ResolveEvidencePath()
        {
#if UNITY_EDITOR
            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException(
                    "Could not resolve the Unity project root.");
            }

            return Path.Combine(
                projectRoot,
                "PerformanceCaptures",
                "Milestone06B3",
                "M06B3_VehicleTraversalEvidence.json");
#else
            return Path.Combine(
                Application.persistentDataPath,
                "Milestone06B3",
                "M06B3_VehicleTraversalEvidence.json");
#endif
        }

        private static string[] SortedCopy(
            HashSet<string> source)
        {
            string[] result = new string[source.Count];
            source.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private bool TryFindNearestTraversalSurface(
            Vector3 referencePosition,
            out RaycastHit nearestHit,
            out string failure)
        {
            nearestHit = default;
            failure = string.Empty;

            var allowedIds = new HashSet<string>(
                allowedTraversalColliderIds ??
                Array.Empty<string>(),
                StringComparer.Ordinal);
            if (allowedIds.Count == 0)
            {
                failure =
                    "06B3 vehicle harness has no serialized traversal " +
                    "collider IDs from the approved allowlist.";
                return false;
            }

            DonorWorldBaselineColliderMetadata[] metadataComponents =
                FindObjectsByType<DonorWorldBaselineColliderMetadata>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            var allowedColliders = new HashSet<Collider>();
            foreach (DonorWorldBaselineColliderMetadata metadata in
                     metadataComponents)
            {
                if (!allowedIds.Contains(metadata.ColliderStableId))
                {
                    continue;
                }

                Collider collider = metadata.GetComponent<Collider>();
                if (collider != null &&
                    collider.enabled &&
                    collider.gameObject.activeInHierarchy)
                {
                    allowedColliders.Add(collider);
                }
            }

            if (allowedColliders.Count == 0)
            {
                failure =
                    "06B3 vehicle harness found no active donor collider " +
                    "whose DonorWorldBaselineColliderMetadata ID belongs to " +
                    "the approved road/asphalt/dirt/gravel/pavement allowlist.";
                return false;
            }

            float bestPlanarDistanceSquared = float.PositiveInfinity;
            float rayOriginY = Mathf.Max(
                referencePosition.y + 1024f,
                2048f);
            for (float x = -SurfaceSearchRadiusMeters;
                 x <= SurfaceSearchRadiusMeters;
                 x += SurfaceSearchStepMeters)
            {
                for (float z = -SurfaceSearchRadiusMeters;
                     z <= SurfaceSearchRadiusMeters;
                     z += SurfaceSearchStepMeters)
                {
                    Vector3 rayOrigin = new Vector3(
                        referencePosition.x + x,
                        rayOriginY,
                        referencePosition.z + z);
                    int hitCount = Physics.RaycastNonAlloc(
                        rayOrigin,
                        Vector3.down,
                        raycastBuffer,
                        4096f,
                        Physics.AllLayers,
                        QueryTriggerInteraction.Ignore);
                    for (int hitIndex = 0;
                         hitIndex < hitCount;
                         hitIndex++)
                    {
                        RaycastHit hit = raycastBuffer[hitIndex];
                        if (hit.collider == null ||
                            !allowedColliders.Contains(hit.collider) ||
                            Vector3.Dot(hit.normal, Vector3.up) <
                            MinimumUpwardNormal)
                        {
                            continue;
                        }

                        Vector2 planarDelta = new Vector2(
                            hit.point.x - referencePosition.x,
                            hit.point.z - referencePosition.z);
                        float planarDistanceSquared =
                            planarDelta.sqrMagnitude;
                        if (planarDistanceSquared >=
                            bestPlanarDistanceSquared)
                        {
                            continue;
                        }

                        bestPlanarDistanceSquared =
                            planarDistanceSquared;
                        nearestHit = hit;
                    }
                }
            }

            if (!float.IsFinite(bestPlanarDistanceSquared))
            {
                failure =
                    "06B3 vehicle harness could not raycast an upward-facing " +
                    "approved donor traversal surface within 128 metres of " +
                    "the project-owned Bootstrap spawn.";
                return false;
            }

            DonorWorldBaselineColliderMetadata selectedMetadata =
                nearestHit.collider.GetComponent<
                    DonorWorldBaselineColliderMetadata>();
            selectedColliderStableId =
                selectedMetadata != null
                    ? selectedMetadata.ColliderStableId
                    : string.Empty;
            return !string.IsNullOrWhiteSpace(selectedColliderStableId);
        }

        private static bool TryProbeSurfaceTangent(
            RaycastHit centerHit,
            Vector3 preferredForward,
            out Vector3 tangent)
        {
            tangent = Vector3.zero;
            Collider selectedCollider = centerHit.collider;
            if (selectedCollider == null)
            {
                return false;
            }

            const int directionCount = 16;
            const float probeDistanceMeters = 8f;
            const float probeHeightMeters = 32f;
            const float probeLengthMeters = 64f;
            float bestScore = float.NegativeInfinity;
            Vector3 planarPreferred =
                Vector3.ProjectOnPlane(
                    preferredForward,
                    centerHit.normal);
            if (planarPreferred.sqrMagnitude > 0.001f)
            {
                planarPreferred.Normalize();
            }

            for (int index = 0;
                 index < directionCount;
                 index++)
            {
                float angle =
                    index * (180f / directionCount);
                Vector3 horizontalDirection =
                    Quaternion.Euler(0f, angle, 0f) *
                    Vector3.forward;
                Vector3 positiveOrigin =
                    centerHit.point +
                    horizontalDirection *
                    probeDistanceMeters +
                    Vector3.up * probeHeightMeters;
                Vector3 negativeOrigin =
                    centerHit.point -
                    horizontalDirection *
                    probeDistanceMeters +
                    Vector3.up * probeHeightMeters;
                if (!selectedCollider.Raycast(
                        new Ray(
                            positiveOrigin,
                            Vector3.down),
                        out RaycastHit positiveHit,
                        probeLengthMeters) ||
                    !selectedCollider.Raycast(
                        new Ray(
                            negativeOrigin,
                            Vector3.down),
                        out RaycastHit negativeHit,
                        probeLengthMeters) ||
                    Vector3.Dot(
                        positiveHit.normal,
                        Vector3.up) <
                    MinimumUpwardNormal ||
                    Vector3.Dot(
                        negativeHit.normal,
                        Vector3.up) <
                    MinimumUpwardNormal)
                {
                    continue;
                }

                Vector3 candidate =
                    Vector3.ProjectOnPlane(
                        positiveHit.point -
                        negativeHit.point,
                        centerHit.normal);
                float span = candidate.magnitude;
                if (span < probeDistanceMeters)
                {
                    continue;
                }

                candidate /= span;
                if (planarPreferred.sqrMagnitude > 0.001f &&
                    Vector3.Dot(candidate, planarPreferred) < 0f)
                {
                    candidate = -candidate;
                }

                float verticalPenalty =
                    Mathf.Abs(
                        positiveHit.point.y -
                        centerHit.point.y) +
                    Mathf.Abs(
                        negativeHit.point.y -
                        centerHit.point.y);
                float preferredAlignment =
                    planarPreferred.sqrMagnitude > 0.001f
                        ? Mathf.Abs(
                            Vector3.Dot(
                                candidate,
                                planarPreferred))
                        : 0f;
                float score =
                    span -
                    verticalPenalty * 0.25f +
                    preferredAlignment * 0.01f;
                if (score > bestScore)
                {
                    bestScore = score;
                    tangent = candidate;
                }
            }

            return tangent.sqrMagnitude > 0.001f;
        }

        private static bool ValidateLoadedSceneSet(out string failure)
        {
            string[] requiredPaths =
            {
                BootstrapScenePath,
                VehiclePrototypeScenePath,
                HarnessScenePath
            };
            for (int requiredIndex = 0;
                 requiredIndex < requiredPaths.Length;
                 requiredIndex++)
            {
                int matches = 0;
                for (int sceneIndex = 0;
                     sceneIndex < SceneManager.sceneCount;
                     sceneIndex++)
                {
                    Scene scene = SceneManager.GetSceneAt(sceneIndex);
                    if (scene.isLoaded &&
                        string.Equals(
                            scene.path,
                            requiredPaths[requiredIndex],
                            StringComparison.Ordinal))
                    {
                        matches++;
                    }
                }

                if (matches != 1)
                {
                    failure =
                        "06B3 vehicle harness requires exactly one loaded " +
                        $"scene at {requiredPaths[requiredIndex]}; found " +
                        matches + ". Use its dedicated Editor menu.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetExactlyOnePrototypeObject(
            string objectName,
            out GameObject result,
            out string failure)
        {
            result = null;
            Scene scene =
                SceneManager.GetSceneByPath(VehiclePrototypeScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                failure =
                    "M06 vehicle prototype scene is not loaded.";
                return false;
            }

            var matches = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms =
                    root.GetComponentsInChildren<Transform>(true);
                foreach (Transform candidate in transforms)
                {
                    if (string.Equals(
                            candidate.name,
                            objectName,
                            StringComparison.Ordinal))
                    {
                        matches.Add(candidate.gameObject);
                    }
                }
            }

            if (matches.Count != 1)
            {
                failure =
                    $"06B3 vehicle harness expected exactly one project-owned " +
                    $"{objectName} in the M06 prototype scene; found " +
                    matches.Count + ".";
                return false;
            }

            result = matches[0];
            failure = string.Empty;
            return true;
        }

        private static bool TryGetExactlyOnePrototypeComponent(
            out VehicleSimulationHost result,
            out string failure)
        {
            result = null;
            Scene scene =
                SceneManager.GetSceneByPath(VehiclePrototypeScenePath);
            var matches = new List<VehicleSimulationHost>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                matches.AddRange(
                    root.GetComponentsInChildren<
                        VehicleSimulationHost>(true));
            }

            if (matches.Count != 1)
            {
                failure =
                    "06B3 vehicle harness expected exactly one " +
                    "VehicleSimulationHost in the M06 prototype scene; " +
                    "found " + matches.Count + ".";
                return false;
            }

            result = matches[0];
            failure = string.Empty;
            return true;
        }

        private void Fail(string message)
        {
            initializationFailure =
                string.IsNullOrWhiteSpace(message)
                    ? "Unknown 06B3 vehicle harness failure."
                    : message;
            IsReady = false;
            Debug.LogError(initializationFailure, this);
        }

        private void OnGUI()
        {
            const float width = 470f;
            const float height = 280f;
            Rect area = new Rect(
                Mathf.Max(10f, Screen.width - width - 16f),
                16f,
                width,
                height);
            GUI.Box(area, "06B3 donor-map vehicle traversal");

            GUILayout.BeginArea(
                new Rect(
                    area.x + 12f,
                    area.y + 26f,
                    area.width - 24f,
                    area.height - 34f));
            if (HasFailed)
            {
                GUILayout.Label("FAILED: " + initializationFailure);
                GUILayout.EndArea();
                return;
            }

            if (!IsReady)
            {
                GUILayout.Label("Preparing Bootstrap + donor world + M06 vehicle...");
                GUILayout.EndArea();
                return;
            }

            float speedKph =
                chassis != null
                    ? chassis.linearVelocity.magnitude * 3.6f
                    : 0f;
            GUILayout.Label(
                "W/S throttle-brake | A/D steer | Shift clutch | " +
                "I ignition | Enter starter | E/Q gears | Backspace reset | " +
                "F8 export");
            GUILayout.Label(
                $"Cell: {currentCellId} | route unique: {ObservedCellCount} | " +
                $"sequence: {ConsecutiveLoadedCellCount} | " +
                $"boundaries: {observedBoundaryCount}/" +
                RequiredStreamingBoundaryCount);
            GUILayout.Label(
                $"Speed: {speedKph:F1} km/h | recovery: " +
                (vehicleRecovery != null
                    ? vehicleRecovery.RecoveryCount
                    : 0));
            GUILayout.Label(
                "Surface collider ID: " + selectedColliderStableId);
            GUILayout.Label(
                BoundaryGoalReached
                    ? "GOAL REACHED: four consecutive loaded cells visited."
                    : "Goal: visit four consecutive loaded cells without reset.");
            if (!string.IsNullOrWhiteSpace(evidenceExportPath))
            {
                GUILayout.Label(
                    "Evidence exported: " + evidenceExportPath);
            }
            GUILayout.EndArea();
        }

        [Serializable]
        private sealed class VehicleTraversalEvidence
        {
            public int schemaVersion;
            public string capturedUtc = string.Empty;
            public string baselineRevisionId = string.Empty;
            public string activeWorldProfileId = string.Empty;
            public bool harnessReady;
            public bool boundaryGoalReached;
            public string selectedColliderStableId = string.Empty;
            public string currentCellId = string.Empty;
            public string[] observedLoadedCellIds = Array.Empty<string>();
            public string[] loadedCellTransitionHistory =
                Array.Empty<string>();
            public string[] longestConsecutiveCellSequence =
                Array.Empty<string>();
            public int longestConsecutiveBoundaryCount;
            public int streamingRefreshCount;
            public float peakFrameMilliseconds;
            public float peakStreamingFrameMilliseconds;
            public float peakSpeedKph;
            public int recoveryCount;
            public Vector3 chassisPosition;
            public bool manualCollisionAndVisualReviewRequired;
        }
    }
}
#endif
