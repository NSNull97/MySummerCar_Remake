using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Audio;
using MSC.Characters;
using MSC.Core.Identity;
using MSC.Core.Lifecycle;
using MSC.Core.Time;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Notifications;
using MSC.Interaction.Query;
using MSC.World.Streaming;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.NPC
{
    public interface INpcNavigationBackend
    {
        void ApplyPose(Transform presentationRoot, in NpcPose pose);
    }

    public sealed class DirectNpcNavigationBackend : INpcNavigationBackend
    {
        private const float GroundProbeUpMeters = 3f;
        private const float GroundProbeDownMeters = 5f;
        private const int GroundHitCapacity = 16;

        private readonly RaycastHit[] groundHits =
            new RaycastHit[GroundHitCapacity];
        private readonly int groundLayerMask;

        public DirectNpcNavigationBackend()
        {
            int worldSurface = LayerMask.NameToLayer("WorldSurface");
            int worldSolid = LayerMask.NameToLayer("WorldSolid");
            groundLayerMask = LayerBit(worldSurface) | LayerBit(worldSolid);
        }

        public void ApplyPose(Transform presentationRoot, in NpcPose pose)
        {
            if (presentationRoot == null)
            {
                throw new ArgumentNullException(nameof(presentationRoot));
            }

            Vector3 position = pose.Position;
            if (pose.ShouldConformToGround &&
                TryResolveGroundHeight(position, out float groundY))
            {
                position.y = groundY;
            }

            presentationRoot.SetPositionAndRotation(position, pose.Rotation);
        }

        private bool TryResolveGroundHeight(
            Vector3 authoredPosition,
            out float groundY)
        {
            groundY = authoredPosition.y;
            if (groundLayerMask == 0)
            {
                return false;
            }

            Vector3 origin = authoredPosition +
                             Vector3.up * GroundProbeUpMeters;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                groundHits,
                GroundProbeUpMeters + GroundProbeDownMeters,
                groundLayerMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
            {
                return false;
            }

            float bestVerticalCorrection = float.PositiveInfinity;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = groundHits[index];
                if (hit.collider == null)
                {
                    continue;
                }

                float correction = Mathf.Abs(
                    hit.point.y - authoredPosition.y);
                if (correction >= bestVerticalCorrection)
                {
                    continue;
                }

                bestVerticalCorrection = correction;
                groundY = hit.point.y;
                found = true;
            }

            return found;
        }

        private static int LayerBit(int layer) =>
            layer >= 0 ? 1 << layer : 0;
    }

    public static class NpcPresentationPolicy
    {
        public static bool ShouldMaterialize(
            CharacterActivityState activity,
            bool cellLoaded) =>
            cellLoaded &&
            activity != CharacterActivityState.Hidden &&
            activity != CharacterActivityState.Disabled;
    }

    internal interface INpcCellAvailability : IDisposable
    {
        event Action<string, bool> AvailabilityChanged;
        bool IsLoaded(string cellId);
        bool TryResolveCellId(Vector3 worldPosition, out string cellId);
    }

    internal sealed class ProductionNpcCellAvailability :
        INpcCellAvailability
    {
        private readonly ProductionWorldStreamingService streaming;

        public ProductionNpcCellAvailability(
            ProductionWorldStreamingService configuredStreaming)
        {
            streaming = configuredStreaming ??
                throw new ArgumentNullException(nameof(configuredStreaming));
            streaming.OwnedSceneLoaded += HandleSceneLoaded;
            streaming.OwnedSceneWillUnload += HandleSceneWillUnload;
        }

        public event Action<string, bool> AvailabilityChanged;

        public bool IsLoaded(string cellId) =>
            streaming.IsCellLoaded(cellId) ||
            streaming.IsGlobalSceneLoaded(cellId);

        public bool TryResolveCellId(
            Vector3 worldPosition,
            out string cellId) =>
            streaming.TryGetCellIdForPosition(worldPosition, out cellId);

        public void Dispose()
        {
            streaming.OwnedSceneLoaded -= HandleSceneLoaded;
            streaming.OwnedSceneWillUnload -= HandleSceneWillUnload;
        }

        private void HandleSceneLoaded(Scene scene)
        {
            if (TryGetAvailabilityId(scene, out string availabilityId))
            {
                AvailabilityChanged?.Invoke(availabilityId, true);
            }
        }

        private void HandleSceneWillUnload(Scene scene)
        {
            if (TryGetAvailabilityId(scene, out string availabilityId))
            {
                AvailabilityChanged?.Invoke(availabilityId, false);
            }
        }

        private bool TryGetAvailabilityId(
            Scene scene,
            out string availabilityId)
        {
            if (streaming.TryGetCellIdForScene(scene, out availabilityId))
            {
                return true;
            }

            ProductionWorldStreamingManifest manifest = streaming.Manifest;
            if (manifest != null && scene.IsValid())
            {
                IReadOnlyList<ProductionWorldGlobalScene> globalScenes =
                    manifest.GlobalScenes;
                for (int index = 0; index < globalScenes.Count; index++)
                {
                    ProductionWorldGlobalScene globalScene =
                        globalScenes[index];
                    if (string.Equals(
                            globalScene.ScenePath,
                            scene.path,
                            StringComparison.Ordinal))
                    {
                        availabilityId = globalScene.SceneId;
                        return true;
                    }
                }
            }

            availabilityId = string.Empty;
            return false;
        }
    }

    [DisallowMultipleComponent]
    public sealed class NpcWorldRuntime : MonoBehaviour, IGameSessionLifetime
    {
        public const string SuskiDefinitionId = "character.suski";
        public const string SuskiRescuedFromJaniCrashFlagId =
            "flag.suski.rescued-from-jani-crash";
        internal const string StoryTrafficRaceStartedFlagId =
            "flag.story-traffic.race-started";
        internal const string StoryTrafficDancehallSessionFlagId =
            "flag.story-traffic.dancehall-session";
        internal const string JaniRaceRouteId =
            "route.story-traffic.jani-race";
        internal const string PetteriRaceRouteId =
            "route.story-traffic.petteri-race";
        internal const string JaniDancehallRouteId =
            "route.story-traffic.jani-dancehall-cycle";
        internal const string PetteriDancehallRouteId =
            "route.story-traffic.petteri-dancehall-cycle";
        internal const string SuskiRescueBedAnchorId =
            "anchor.story.suski-rescue-bed";
        internal const string SuskiRescuePresentationBindingId =
            "presentation.character.suski-better-rescue";

        private const double TemporaryAutomaticRaceStartSecondsOfDay =
            57840d; // 16:04; replaced by the Satsuma rev challenge later.
        private const float TemporaryRaceTriggerDistanceMeters = 35f;
        private const float TemporaryRaceTriggerDwellSeconds = 2f;
        private const float StoryTrafficResidencyPollSeconds = 5f;
        private const double SaturdayDancehallStartSecondsOfDay = 72000d;
        private const float SuskiBedAcceptanceRadiusMeters = 2.2f;
        private const float CorruptIncidentRouteSeparationMeters = 250f;
        private const int StoryRacePerajarviLastWaypointIndex = 358;
        private const int StoryRaceRoadRaceLastWaypointIndex = 982;
        private const int StoryRaceTrackfieldApproachWaypointIndex = 1028;
        private const int StoryRaceTrackfieldOvalStartWaypointIndex = 1056;
        private const int StoryRaceTrackfieldApproachEndWaypointIndex = 1063;
        private const int StoryRaceFirstLapEndWaypointIndex = 1184;
        private const int StoryRaceFieldRepeatWaypointCount = 129;
        private const int StoryRaceLastLoopClosureWaypointIndex = 1958;
        private const int StoryRaceTerminalApproachWaypointIndex = 2078;
        private const int StoryRaceJaniTerminalStopWaypointIndex = 2087;
        private const int StoryRacePetteriTerminalStopWaypointIndex = 2085;
        private const int StoryRaceTeimoApproachWaypointIndex = 88;
        private const int StoryRaceJaniTeimoStopWaypointIndex = 96;
        private const int StoryRacePetteriTeimoStopWaypointIndex = 94;
        private const float StoryRaceJaniTeimoStopSeconds = 6f;
        private const float StoryRacePetteriTeimoStopSeconds = 8f;
        private const float StoryRaceSocialStopArrivalMeters = 1.8f;
        private const float StoryRaceTerminalStopArrivalMeters = 2.2f;
        private const int StoryRaceWaypointCount = 2088;
        private const int StoryRaceTeimoManeuverStartWaypointIndex = 72;
        private const int StoryRaceTeimoManeuverEndWaypointIndex = 107;
        private const int StoryRaceInspectionH2BrakeStartWaypointIndex = 314;
        private const int StoryRaceInspectionH2PreviewStartWaypointIndex = 317;
        private const int StoryRaceInspectionH2CoreEndWaypointIndex = 326;
        private const int StoryRaceInspectionH2ReleaseEndWaypointIndex = 329;
        private const int StoryRaceInspectionH3BrakeStartWaypointIndex = 329;
        private const int StoryRaceInspectionH3PreviewStartWaypointIndex = 333;
        private const int StoryRaceInspectionH3CoreEndWaypointIndex = 344;
        private const int StoryRaceInspectionH3ReleaseEndWaypointIndex = 348;
        private const int StoryRacePerajarviOutboundBrakeStartWaypointIndex =
            369;
        private const int StoryRacePerajarviOutboundPreviewStartWaypointIndex =
            372;
        private const int StoryRacePerajarviOutboundCoreStartWaypointIndex =
            381;
        private const int StoryRacePerajarviOutboundCoreEndWaypointIndex = 390;
        private const int StoryRacePerajarviOutboundPreviewEndWaypointIndex =
            393;
        private const int StoryRacePerajarviOutboundReleaseEndWaypointIndex =
            398;
        private const int StoryRacePerajarviInboundBrakeStartWaypointIndex = 939;
        private const int StoryRacePerajarviInboundPreviewStartWaypointIndex =
            946;
        private const int StoryRacePerajarviInboundCoreStartWaypointIndex = 953;
        private const int StoryRacePerajarviInboundCoreEndWaypointIndex = 963;
        private const int StoryRacePerajarviInboundPreviewEndWaypointIndex = 965;
        private const int StoryRacePerajarviInboundReleaseEndWaypointIndex = 975;
        private const float StoryTrafficRouteRejoinThresholdMeters = 5f;
        // Waypoint progress is distance-normalized. On the locked 20.5 km
        // route this is roughly 245 metres of braking preview before the
        // RoadRace -> Trackfield/Perajarvi return bend.
        private const float StoryRaceRoadExitBrakePreviewProgress = 0.012f;
        private const float DonorHandbrakeZoneRadiusMeters = 12f;
        private static readonly Vector3[] DonorPerajarviHandbrakeZones =
        {
            // Locked donor HandbrakeZone0..3 plus the established
            // source-to-project translation (169.98, 1.611, -1040.625).
            new Vector3(-1408.82f, 4.511f, 129.075f),
            new Vector3(-1349.95f, 4.961f, 143.335f),
            new Vector3(-1397.72f, 5.611f, 241.875f),
            new Vector3(-1359.84f, 4.511f, 192.545f),
        };

        internal enum StoryTrafficDirectedManeuverCorridor
        {
            None = 0,
            Teimo = 1,
            InspectionH2 = 2,
            InspectionH3 = 3,
            PerajarviOutbound = 4,
            PerajarviInbound = 5,
            TrackfieldApproach = 6,
            TrackfieldOval = 7,
            TrackfieldLoopClosure = 8,
            TerminalApproach = 9,
        }

        private readonly Dictionary<string, LegacyCharacterPresentationBinding>
            presentations =
                new Dictionary<string, LegacyCharacterPresentationBinding>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, StoryTrafficMotionRuntimeState>
            storyTrafficMotionStates =
                new Dictionary<string, StoryTrafficMotionRuntimeState>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, string> physicalStoryTrafficRoutes =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> forcePhysicalPlacement =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> knownStoryTrafficResidency =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> residentStoryTrafficCharacters =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, StoryTrafficVehicleAudioPresenter>
            storyTrafficAudioOwners =
                new Dictionary<string, StoryTrafficVehicleAudioPresenter>(
                    StringComparer.Ordinal);
        private readonly HashSet<string> storyTrafficCellRetentionOwners =
            new HashSet<string>(StringComparer.Ordinal);

        private CharacterDefinitionCatalog characters;
        private NpcFoundationCatalog foundation;
        private CharacterPresentationCatalog presentationCatalog;
        private IGameTimeService gameTime;
        private INpcCellAvailability cellAvailability;
        private INpcNavigationBackend navigation;
        private ProductionWorldStreamingService productionStreaming;
        private NpcDialogueCatalog dialogueCatalog;
        private NpcDialogueRuntime dialogueRuntime;
        private NpcDialogueFeedbackHandler dialogueFeedback;
        private IDisposable gameTimeSubscription;
        private TeimoShopWorldPresentationController
            teimoShopWorldPresentation;
        private Transform player;
        private MonoBehaviour vehicleAudioBackendComponent;
        private IInteractionActionSource carryActionSource;
        private StoryTrafficStateDto storyTrafficState;
        private float storyTrafficPlayerTriggerSeconds;
        private float storyTrafficResidencyPollRemainingSeconds;
        private bool initialized;

        private void FixedUpdate()
        {
            if (!initialized || Simulation == null)
            {
                return;
            }

            UpdateTemporaryStoryTrafficRaceTrigger(Time.fixedDeltaTime);

            storyTrafficResidencyPollRemainingSeconds -= Time.fixedDeltaTime;
            if (storyTrafficResidencyPollRemainingSeconds <= 0f)
            {
                storyTrafficResidencyPollRemainingSeconds =
                    StoryTrafficResidencyPollSeconds;
                ReconcilePhysicalStoryTrafficResidency();
            }

            foreach (KeyValuePair<string,
                         LegacyCharacterPresentationBinding> pair in
                     presentations)
            {
                LegacyCharacterPresentationBinding presentation = pair.Value;
                StoryTrafficVehiclePresentationBinding traffic =
                    presentation != null
                        ? presentation.GetComponent<
                            StoryTrafficVehiclePresentationBinding>()
                        : null;
                if (traffic == null ||
                    !Simulation.TryGetInstance(
                        pair.Key,
                        out CharacterInstance instance) ||
                    string.IsNullOrEmpty(instance.CurrentRouteId))
                {
                    continue;
                }

                if (ShouldKeepStoryTrafficPhysical(instance))
                {
                    RetainStoryTrafficCell(
                        pair.Key,
                        "current",
                        traffic.PhysicalWorldPosition);
                }

                if (!traffic.HasPhysicalMotionBackend)
                {
                    continue;
                }

                TryGetStoryTrafficDriverState(
                    pair.Key,
                    out StoryTrafficDriverStateDto driverState);
                if (driverState != null)
                {
                    if (driverState.teimoSocialStopConsumed)
                    {
                        driverState.teimoSocialStopSecondsRemaining =
                            traffic.SocialStopSecondsRemaining;
                    }

                    if (driverState.terminalCrash)
                    {
                        continue;
                    }

                    if (driverState.routeFullStopReached)
                    {
                        traffic.SetRouteFullStopHold(true);
                        traffic.SetPassingSuppressed(true);
                        continue;
                    }
                }

                // NWH needs a genuine pure-pursuit target, not the next dense
                // donor sample. The preview remains speed-scaled, but town and
                // gravel profiles deliberately stay shorter than RoadRace so
                // the chassis does not cut junctions or roadside furniture.
                StoryTrafficRoadBehaviorProfile projectedProfile =
                    ResolveStoryTrafficRoadBehaviorProfile(
                        foundation,
                        instance.CurrentRouteId,
                        (float)instance.RouteProgress01);
                bool projectedInsideHandbrakeZone =
                    projectedProfile ==
                        StoryTrafficRoadBehaviorProfile.Perajarvi &&
                    IsInsideDonorPerajarviHandbrakeZone(
                        traffic.PhysicalWorldPosition);
                float lookAheadMeters =
                    ResolveStoryTrafficRouteLookAheadMeters(
                        foundation,
                        instance.CurrentRouteId,
                        (float)instance.RouteProgress01,
                        projectedProfile,
                        traffic.CurrentSpeedMetersPerSecond,
                        projectedInsideHandbrakeZone);
                StoryTrafficDirectedManeuverCorridor projectedCorridor =
                    ResolveStoryTrafficDirectedManeuverCorridor(
                        foundation,
                        instance.CurrentRouteId,
                        (float)instance.RouteProgress01);
                traffic.SetPassingSuppressed(
                    projectedCorridor !=
                        StoryTrafficDirectedManeuverCorridor.None ||
                    traffic.HasIntentionalStationaryHold);
                if (Simulation.TryUpdatePhysicalRoute(
                        instance,
                        traffic.PhysicalWorldPosition,
                        traffic.CurrentSpeedMetersPerSecond >= 1.5f
                            ? traffic.PhysicalWorldRotation * Vector3.forward
                            : Vector3.zero,
                        lookAheadMeters,
                        out NpcPose guidancePose,
                        out float physicalProgress01,
                        out float routeDeviationMeters))
                {
                    StoryTrafficRoadBehaviorProfile behaviorProfile =
                        ResolveStoryTrafficRoadBehaviorProfile(
                            foundation,
                            instance.CurrentRouteId,
                            physicalProgress01);
                    traffic.SetRoadBehaviorProfile(
                        behaviorProfile,
                        behaviorProfile ==
                            StoryTrafficRoadBehaviorProfile.Perajarvi &&
                        IsInsideDonorPerajarviHandbrakeZone(
                            traffic.PhysicalWorldPosition));
                    bool routeRejoinActive = routeDeviationMeters >
                        StoryTrafficRouteRejoinThresholdMeters;
                    traffic.SetRouteRejoinActive(routeRejoinActive);
                    StoryTrafficDirectedManeuverCorridor physicalCorridor =
                        ResolveStoryTrafficDirectedManeuverCorridor(
                            foundation,
                            instance.CurrentRouteId,
                            physicalProgress01);
                    traffic.SetPassingSuppressed(
                        routeRejoinActive ||
                        physicalCorridor !=
                            StoryTrafficDirectedManeuverCorridor.None ||
                        traffic.HasIntentionalStationaryHold);
                    float routeSpeedCap =
                        ResolveStoryTrafficRouteSpeedCap(
                            foundation,
                            instance.CurrentRouteId,
                            physicalProgress01);
                    if (routeRejoinActive)
                    {
                        routeSpeedCap = Mathf.Min(
                            routeSpeedCap,
                            ResolveStoryTrafficRouteRejoinSpeedCap(
                                routeDeviationMeters));
                    }

                    if (driverState != null &&
                        ApplyStoryTrafficAuthoredRouteEvents(
                            instance,
                            traffic,
                            driverState,
                            physicalProgress01,
                            ref guidancePose,
                            ref routeSpeedCap))
                    {
                        traffic.SetPassingSuppressed(true);
                        continue;
                    }

                    traffic.SetRouteSpeedCap(routeSpeedCap);
                    traffic.SetPhysicalRouteGuidanceTarget(
                        guidancePose.Position,
                        guidancePose.Rotation,
                        physicalProgress01);
                    float preloadDistanceMeters = Mathf.Max(
                        96f,
                        traffic.CurrentSpeedMetersPerSecond * 3.5f);
                    Vector3 preloadPosition = guidancePose.Position;
                    if (Simulation.TryResolvePhysicalRoutePreloadPose(
                            instance,
                            preloadDistanceMeters,
                            out NpcPose preloadPose))
                    {
                        preloadPosition = preloadPose.Position;
                    }

                    RetainStoryTrafficCell(
                        pair.Key,
                        "ahead",
                        preloadPosition);
                }
            }

            SynchronizeLiveSuskiRagdollPose();
        }

        internal static float ResolveStoryTrafficLookAheadMeters(
            StoryTrafficRoadBehaviorProfile profile,
            float speedMetersPerSecond) =>
            ResolveStoryTrafficLookAheadMeters(
                profile,
                speedMetersPerSecond,
                insideDonorHandbrakeZone: false,
                insideTightManeuverCorridor: false);

        internal static float ResolveStoryTrafficLookAheadMeters(
            StoryTrafficRoadBehaviorProfile profile,
            float speedMetersPerSecond,
            bool insideDonorHandbrakeZone) =>
            ResolveStoryTrafficLookAheadMeters(
                profile,
                speedMetersPerSecond,
                insideDonorHandbrakeZone,
                insideTightManeuverCorridor: false);

        internal static float ResolveStoryTrafficLookAheadMeters(
            StoryTrafficRoadBehaviorProfile profile,
            float speedMetersPerSecond,
            bool insideDonorHandbrakeZone,
            bool insideTightManeuverCorridor)
        {
            float speed = Mathf.Max(0f, speedMetersPerSecond);
            if (profile == StoryTrafficRoadBehaviorProfile.Perajarvi &&
                insideDonorHandbrakeZone)
            {
                // The shop manoeuvre is a genuine pass-the-pumps then
                // handbrake U-turn. Once the physical chassis reaches the
                // donor trigger, keep the pursuit target almost local so the
                // handbrake request happens beside the pumps instead of
                // steering across the return leg before the slide begins.
                return Mathf.Clamp(3.25f + speed * 0.05f, 4f, 5f);
            }

            if (profile == StoryTrafficRoadBehaviorProfile.Perajarvi &&
                insideTightManeuverCorridor)
            {
                // Dense donor samples already describe both Teimo's loop and
                // the inspection manoeuvres. A normal town pursuit chord cuts
                // their inside edge; this still looks ahead far enough for NWH
                // steering while making the Rigidbody visit the measured arc.
                return Mathf.Clamp(3.5f + speed * 0.06f, 4.5f, 6f);
            }

            switch (profile)
            {
                case StoryTrafficRoadBehaviorProfile.Perajarvi:
                    // The donor Village route has tight junctions and roadside
                    // furniture. Keep it shorter than the race preview, while
                    // still giving a 90 km/h car enough time to read the turns
                    // at inspection and Teimo before it reaches their poles.
                    return Mathf.Clamp(8f + speed * 0.42f, 12f, 22f);
                case StoryTrafficRoadBehaviorProfile.RoadRace:
                    // At donor race speed the old 34 m ceiling exposed less
                    // than 0.7 s of road. That made the chassis discover the
                    // Trackfield return only after entering the bend.
                    return Mathf.Clamp(18f + speed * 0.85f, 28f, 62f);
                default:
                    // Trackfield is a compact paved/gravel circuit. A long
                    // highway-style pure-pursuit chord pulls the cars inside
                    // every bend instead of following the authored lane.
                    return Mathf.Clamp(4f + speed * 0.12f, 5.5f, 9f);
            }
        }

        internal static float ResolveStoryTrafficRouteLookAheadMeters(
            NpcFoundationCatalog configuredFoundation,
            string routeId,
            float progress01,
            StoryTrafficRoadBehaviorProfile profile,
            float speedMetersPerSecond,
            bool insideDonorHandbrakeZone)
        {
            if (insideDonorHandbrakeZone &&
                profile == StoryTrafficRoadBehaviorProfile.Perajarvi)
            {
                return ResolveStoryTrafficLookAheadMeters(
                    profile,
                    speedMetersPerSecond,
                    insideDonorHandbrakeZone: true,
                    insideTightManeuverCorridor: false);
            }

            StoryTrafficDirectedManeuverCorridor corridor =
                ResolveStoryTrafficDirectedManeuverCorridor(
                    configuredFoundation,
                    routeId,
                    progress01);
            float speed = Mathf.Max(0f, speedMetersPerSecond);
            switch (corridor)
            {
                case StoryTrafficDirectedManeuverCorridor.Teimo:
                    return ResolveStoryTrafficLookAheadMeters(
                        profile,
                        speed,
                        insideDonorHandbrakeZone: false,
                        insideTightManeuverCorridor: true);
                case StoryTrafficDirectedManeuverCorridor.InspectionH2:
                    return Mathf.Clamp(3.75f + speed * 0.06f, 4f, 5.5f);
                case StoryTrafficDirectedManeuverCorridor.InspectionH3:
                    return Mathf.Clamp(3.5f + speed * 0.06f, 4.5f, 6f);
                case StoryTrafficDirectedManeuverCorridor.PerajarviOutbound:
                case StoryTrafficDirectedManeuverCorridor.PerajarviInbound:
                    // The two opposing Perajarvi branches overlap to within a
                    // metre. Progress-directed preview keeps each car on its
                    // authored branch; a world-space trigger would randomly
                    // apply the wrong direction at the shared junction.
                    return Mathf.Clamp(6f + speed * 0.18f, 8f, 12f);
                case StoryTrafficDirectedManeuverCorridor.TrackfieldApproach:
                    // Follow the measured road-to-field exit instead of taking
                    // a direct chord from the public road to the oval.
                    return Mathf.Clamp(3.75f + speed * 0.06f, 4f, 5.5f);
                case StoryTrafficDirectedManeuverCorridor.TrackfieldOval:
                    return Mathf.Clamp(3.4f + speed * 0.05f, 4f, 5f);
                case StoryTrafficDirectedManeuverCorridor.TrackfieldLoopClosure:
                    // TF201 -> TF73 is a 24 m authored lap closure. A long
                    // preview cuts across the infield; keep the pursuit target
                    // local on both sides of every repeated boundary.
                    return Mathf.Clamp(3f + speed * 0.045f, 3.75f, 4.5f);
                case StoryTrafficDirectedManeuverCorridor.TerminalApproach:
                    return Mathf.Clamp(2.75f + speed * 0.04f, 3.25f, 4f);
                default:
                    return ResolveStoryTrafficLookAheadMeters(
                        profile,
                        speed,
                        insideDonorHandbrakeZone: false,
                        insideTightManeuverCorridor: false);
            }
        }

        internal static float ResolveStoryTrafficRouteRejoinSpeedCap(
            float routeDeviationMeters)
        {
            float normalized = Mathf.InverseLerp(
                StoryTrafficRouteRejoinThresholdMeters,
                20f,
                Mathf.Max(0f, routeDeviationMeters));
            return Mathf.Lerp(12f, 6.5f, normalized);
        }

        internal static float ResolveStoryTrafficRouteSpeedCap(
            NpcFoundationCatalog configuredFoundation,
            string routeId,
            float progress01)
        {
            if (configuredFoundation == null)
            {
                throw new ArgumentNullException(
                    nameof(configuredFoundation));
            }

            if (!string.Equals(routeId, JaniRaceRouteId,
                    StringComparison.Ordinal) &&
                !string.Equals(routeId, PetteriRaceRouteId,
                    StringComparison.Ordinal) ||
                !configuredFoundation.TryGetRoute(
                    routeId,
                    out NpcRouteDefinition route) ||
                route.WaypointProgress01.Count != StoryRaceWaypointCount)
            {
                return float.PositiveInfinity;
            }

            IReadOnlyList<double> waypointProgress =
                route.WaypointProgress01;
            float progress = Mathf.Clamp01(progress01);
            if (IsProgressInsideWaypointRange(
                    waypointProgress,
                    progress,
                    StoryRaceTeimoManeuverStartWaypointIndex,
                    StoryRaceTeimoManeuverEndWaypointIndex))
            {
                // The donor Navigation speed range remains authoritative, but
                // the compact pass-the-pumps arc needs a bounded entry speed
                // so the physical handbrake request produces a recoverable
                // slide rather than an early pure-pursuit cut.
                return 60f / 3.6f;
            }

            if (TryResolvePiecewiseWaypointSpeedCap(
                    waypointProgress,
                    progress,
                    StoryRaceInspectionH2BrakeStartWaypointIndex,
                    StoryRaceInspectionH2PreviewStartWaypointIndex,
                    StoryRaceInspectionH2CoreEndWaypointIndex,
                    StoryRaceInspectionH2ReleaseEndWaypointIndex,
                    60f,
                    38f,
                    60f,
                    out float inspectionH2SpeedCap))
            {
                return inspectionH2SpeedCap;
            }

            if (TryResolvePiecewiseWaypointSpeedCap(
                    waypointProgress,
                    progress,
                    StoryRaceInspectionH3BrakeStartWaypointIndex,
                    StoryRaceInspectionH3PreviewStartWaypointIndex,
                    StoryRaceInspectionH3CoreEndWaypointIndex,
                    StoryRaceInspectionH3ReleaseEndWaypointIndex,
                    60f,
                    38f,
                    60f,
                    out float inspectionH3SpeedCap))
            {
                return inspectionH3SpeedCap;
            }

            if (TryResolvePiecewiseWaypointSpeedCap(
                    waypointProgress,
                    progress,
                    StoryRacePerajarviOutboundBrakeStartWaypointIndex,
                    StoryRacePerajarviOutboundCoreStartWaypointIndex,
                    StoryRacePerajarviOutboundCoreEndWaypointIndex,
                    StoryRacePerajarviOutboundReleaseEndWaypointIndex,
                    90f,
                    44f,
                    90f,
                    out float perajarviOutboundSpeedCap))
            {
                return perajarviOutboundSpeedCap;
            }

            if (TryResolvePiecewiseWaypointSpeedCap(
                    waypointProgress,
                    progress,
                    StoryRacePerajarviInboundBrakeStartWaypointIndex,
                    StoryRacePerajarviInboundCoreStartWaypointIndex,
                    StoryRacePerajarviInboundCoreEndWaypointIndex,
                    StoryRacePerajarviInboundReleaseEndWaypointIndex,
                    185f,
                    43f,
                    68f,
                    out float perajarviInboundSpeedCap))
            {
                return perajarviInboundSpeedCap;
            }

            float trackfieldApproach = (float)waypointProgress[
                StoryRaceTrackfieldApproachWaypointIndex];
            float trackfieldOvalStart = (float)waypointProgress[
                StoryRaceTrackfieldOvalStartWaypointIndex];
            if (progress >= trackfieldOvalStart)
            {
                return 50f / 3.6f;
            }

            if (progress >= trackfieldApproach)
            {
                float trackfieldBrake01 = Mathf.InverseLerp(
                    trackfieldApproach,
                    trackfieldOvalStart,
                    progress);
                return Mathf.Lerp(
                    72f / 3.6f,
                    50f / 3.6f,
                    trackfieldBrake01);
            }

            float roadRaceExit = (float)Midpoint(
                waypointProgress[StoryRaceRoadRaceLastWaypointIndex],
                waypointProgress[StoryRaceRoadRaceLastWaypointIndex + 1]);
            float previewStart = Mathf.Max(
                0f,
                roadRaceExit - StoryRaceRoadExitBrakePreviewProgress);
            if (progress < previewStart || progress >= roadRaceExit)
            {
                return float.PositiveInfinity;
            }

            float normalized = Mathf.InverseLerp(
                previewStart,
                roadRaceExit,
                progress);
            float eased = normalized * normalized * (3f - 2f * normalized);
            return Mathf.Lerp(185f / 3.6f, 68f / 3.6f, eased);
        }

        internal static bool ShouldKeepStoryTrafficPhysical(
            CharacterActivityState activityState,
            string routeId,
            bool terminalCrash)
        {
            // The named road actors are story authority, not disposable LOD.
            // Keeping their Rigidbody/NWH state alive lets an out-running Jani
            // still collide and enter the real terminal incident instead of
            // freezing as soon as the player falls 500 metres behind.
            return terminalCrash ||
                   activityState == CharacterActivityState.VehicleSeated &&
                   !string.IsNullOrWhiteSpace(routeId);
        }

        internal static StoryTrafficRoadBehaviorProfile
            ResolveStoryTrafficRoadBehaviorProfile(
                NpcFoundationCatalog configuredFoundation,
                string routeId,
                float progress01)
        {
            if (configuredFoundation == null)
            {
                throw new ArgumentNullException(
                    nameof(configuredFoundation));
            }

            if (!string.Equals(routeId, JaniRaceRouteId, StringComparison.Ordinal) &&
                !string.Equals(routeId, PetteriRaceRouteId, StringComparison.Ordinal))
            {
                return StoryTrafficRoadBehaviorProfile.Gravel;
            }

            if (!configuredFoundation.TryGetRoute(
                    routeId,
                    out NpcRouteDefinition route) ||
                route.WaypointProgress01.Count != StoryRaceWaypointCount)
            {
                return StoryTrafficRoadBehaviorProfile.RoadRace;
            }

            IReadOnlyList<double> waypointProgress = route.WaypointProgress01;
            double progress = Mathf.Clamp01(progress01);
            double perajarviExit = Midpoint(
                waypointProgress[StoryRacePerajarviLastWaypointIndex],
                waypointProgress[StoryRacePerajarviLastWaypointIndex + 1]);
            if (progress < perajarviExit)
            {
                return StoryTrafficRoadBehaviorProfile.Perajarvi;
            }

            double roadRaceExit = Midpoint(
                waypointProgress[StoryRaceRoadRaceLastWaypointIndex],
                waypointProgress[StoryRaceRoadRaceLastWaypointIndex + 1]);
            return progress < roadRaceExit
                ? StoryTrafficRoadBehaviorProfile.RoadRace
                : StoryTrafficRoadBehaviorProfile.Gravel;
        }

        private static double Midpoint(double left, double right) =>
            left + (right - left) * 0.5d;

        internal static bool IsInsideStoryTrafficTightManeuverCorridor(
            NpcFoundationCatalog configuredFoundation,
            string routeId,
            float progress01) =>
            ResolveStoryTrafficDirectedManeuverCorridor(
                configuredFoundation,
                routeId,
                progress01) != StoryTrafficDirectedManeuverCorridor.None;

        internal static StoryTrafficDirectedManeuverCorridor
            ResolveStoryTrafficDirectedManeuverCorridor(
                NpcFoundationCatalog configuredFoundation,
                string routeId,
                float progress01)
        {
            if (configuredFoundation == null ||
                !string.Equals(routeId, JaniRaceRouteId,
                    StringComparison.Ordinal) &&
                !string.Equals(routeId, PetteriRaceRouteId,
                    StringComparison.Ordinal) ||
                !configuredFoundation.TryGetRoute(
                    routeId,
                    out NpcRouteDefinition route) ||
                route.WaypointProgress01.Count != StoryRaceWaypointCount)
            {
                return StoryTrafficDirectedManeuverCorridor.None;
            }

            IReadOnlyList<double> waypointProgress =
                route.WaypointProgress01;
            float progress = Mathf.Clamp01(progress01);
            if (IsProgressInsideWaypointRange(
                    waypointProgress,
                    progress,
                    StoryRaceTeimoManeuverStartWaypointIndex,
                    StoryRaceTeimoManeuverEndWaypointIndex))
            {
                return StoryTrafficDirectedManeuverCorridor.Teimo;
            }

            if (IsProgressInsideWaypointRange(
                    waypointProgress,
                    progress,
                    StoryRaceInspectionH2BrakeStartWaypointIndex,
                    StoryRaceInspectionH2ReleaseEndWaypointIndex))
            {
                return StoryTrafficDirectedManeuverCorridor.InspectionH2;
            }

            if (IsProgressInsideWaypointRange(
                    waypointProgress,
                    progress,
                    StoryRaceInspectionH3PreviewStartWaypointIndex,
                    StoryRaceInspectionH3CoreEndWaypointIndex))
            {
                return StoryTrafficDirectedManeuverCorridor.InspectionH3;
            }

            if (IsProgressInsideWaypointRange(
                    waypointProgress,
                    progress,
                    StoryRacePerajarviOutboundPreviewStartWaypointIndex,
                    StoryRacePerajarviOutboundPreviewEndWaypointIndex))
            {
                return StoryTrafficDirectedManeuverCorridor.PerajarviOutbound;
            }

            if (IsProgressInsideWaypointRange(
                    waypointProgress,
                    progress,
                    StoryRacePerajarviInboundPreviewStartWaypointIndex,
                    StoryRacePerajarviInboundPreviewEndWaypointIndex))
            {
                return StoryTrafficDirectedManeuverCorridor.PerajarviInbound;
            }

            if (IsProgressInsideWaypointRange(
                    waypointProgress,
                    progress,
                    StoryRaceTerminalApproachWaypointIndex,
                    StoryRaceWaypointCount - 1))
            {
                return StoryTrafficDirectedManeuverCorridor.TerminalApproach;
            }

            if (IsInsideStoryTrafficTrackfieldLoopClosure(
                    waypointProgress,
                    progress))
            {
                return StoryTrafficDirectedManeuverCorridor
                    .TrackfieldLoopClosure;
            }

            if (IsProgressInsideWaypointRange(
                    waypointProgress,
                    progress,
                    StoryRaceTrackfieldApproachWaypointIndex,
                    StoryRaceTrackfieldApproachEndWaypointIndex))
            {
                return StoryTrafficDirectedManeuverCorridor
                    .TrackfieldApproach;
            }

            if (IsProgressInsideWaypointRange(
                    waypointProgress,
                    progress,
                    StoryRaceTrackfieldOvalStartWaypointIndex,
                    StoryRaceTerminalApproachWaypointIndex - 1))
            {
                return StoryTrafficDirectedManeuverCorridor.TrackfieldOval;
            }

            return StoryTrafficDirectedManeuverCorridor.None;
        }

        internal static bool IsInsideStoryTrafficTrackfieldLoopClosure(
            IReadOnlyList<double> waypointProgress,
            float progress01)
        {
            for (int lapEnd = StoryRaceFirstLapEndWaypointIndex;
                 lapEnd <= StoryRaceLastLoopClosureWaypointIndex;
                 lapEnd += StoryRaceFieldRepeatWaypointCount)
            {
                if (IsProgressInsideWaypointRange(
                        waypointProgress,
                        progress01,
                        lapEnd - 6,
                        lapEnd + 8))
                {
                    return true;
                }
            }

            return false;
        }

        internal static int ResolveStoryTrafficTerminalStopWaypointIndex(
            string characterId) => string.Equals(
                characterId,
                "character.petteri",
                StringComparison.Ordinal)
                    ? StoryRacePetteriTerminalStopWaypointIndex
                    : StoryRaceJaniTerminalStopWaypointIndex;

        internal static int ResolveStoryTrafficTeimoStopWaypointIndex(
            string characterId) => string.Equals(
                characterId,
                "character.petteri",
                StringComparison.Ordinal)
                    ? StoryRacePetteriTeimoStopWaypointIndex
                    : StoryRaceJaniTeimoStopWaypointIndex;

        private bool ApplyStoryTrafficAuthoredRouteEvents(
            CharacterInstance instance,
            StoryTrafficVehiclePresentationBinding traffic,
            StoryTrafficDriverStateDto driverState,
            float physicalProgress01,
            ref NpcPose guidancePose,
            ref float routeSpeedCapMetersPerSecond)
        {
            if (instance == null || traffic == null || driverState == null ||
                !foundation.TryGetRoute(
                    instance.CurrentRouteId,
                    out NpcRouteDefinition route) ||
                route.WaypointProgress01.Count != StoryRaceWaypointCount)
            {
                return false;
            }

            IReadOnlyList<double> waypointProgress =
                route.WaypointProgress01;
            float progress = Mathf.Clamp01(physicalProgress01);
            int socialStopWaypointIndex =
                ResolveStoryTrafficTeimoStopWaypointIndex(
                    instance.Definition.DefinitionId);
            int socialDepartureWaypointIndex = Mathf.Min(
                StoryRaceTeimoManeuverEndWaypointIndex,
                socialStopWaypointIndex + 4);
            if (!driverState.teimoSocialStopConsumed)
            {
                if (progress >
                    (float)waypointProgress[socialDepartureWaypointIndex])
                {
                    // An older save may already be beyond Teimo. Never make it
                    // reverse down the route just to satisfy the new one-shot
                    // presentation event.
                    driverState.teimoSocialStopConsumed = true;
                    driverState.teimoSocialStopSecondsRemaining = 0f;
                }
                else if (progress >= (float)waypointProgress[
                             StoryRaceTeimoApproachWaypointIndex] &&
                         TryResolveStoryTrafficWaypointPose(
                             route,
                             socialStopWaypointIndex,
                             out NpcPose socialStopPose))
                {
                    guidancePose = socialStopPose;
                    float distance = HorizontalDistance(
                        traffic.PhysicalWorldPosition,
                        socialStopPose.Position);
                    routeSpeedCapMetersPerSecond = Mathf.Min(
                        routeSpeedCapMetersPerSecond,
                        ResolveStoryTrafficStopApproachSpeedCap(
                            distance,
                            StoryRaceSocialStopArrivalMeters,
                            20f / 3.6f));
                    if (distance <= StoryRaceSocialStopArrivalMeters)
                    {
                        float dwellSeconds = string.Equals(
                            instance.Definition.DefinitionId,
                            "character.petteri",
                            StringComparison.Ordinal)
                                ? StoryRacePetteriTeimoStopSeconds
                                : StoryRaceJaniTeimoStopSeconds;
                        driverState.teimoSocialStopConsumed = true;
                        driverState.teimoSocialStopSecondsRemaining =
                            dwellSeconds;
                        traffic.BeginTeimoSocialStop(dwellSeconds);
                    }
                }
            }

            if (progress < (float)waypointProgress[
                    StoryRaceTerminalApproachWaypointIndex])
            {
                return false;
            }

            int terminalStopWaypointIndex =
                ResolveStoryTrafficTerminalStopWaypointIndex(
                    instance.Definition.DefinitionId);
            if (!TryResolveStoryTrafficWaypointPose(
                    route,
                    terminalStopWaypointIndex,
                    out NpcPose terminalStopPose))
            {
                return false;
            }

            guidancePose = terminalStopPose;
            float terminalDistance = HorizontalDistance(
                traffic.PhysicalWorldPosition,
                terminalStopPose.Position);
            routeSpeedCapMetersPerSecond = Mathf.Min(
                routeSpeedCapMetersPerSecond,
                ResolveStoryTrafficStopApproachSpeedCap(
                    terminalDistance,
                    StoryRaceTerminalStopArrivalMeters,
                    30f / 3.6f));
            if (terminalDistance > StoryRaceTerminalStopArrivalMeters)
            {
                return false;
            }

            driverState.routeFullStopReached = true;
            driverState.teimoSocialStopSecondsRemaining = 0f;
            driverState.worldPosition = traffic.PhysicalWorldPosition;
            driverState.worldRotation = traffic.PhysicalWorldRotation;
            traffic.SetRouteFullStopHold(true);
            if (storyTrafficAudioOwners.TryGetValue(
                    instance.Definition.DefinitionId,
                    out StoryTrafficVehicleAudioPresenter audio) &&
                audio != null)
            {
                audio.SetTerminallyDisabled(true);
            }

            return true;
        }

        internal static float ResolveStoryTrafficStopApproachSpeedCap(
            float distanceMeters,
            float arrivalRadiusMeters,
            float maximumMetersPerSecond)
        {
            if (!float.IsFinite(distanceMeters) || distanceMeters < 0f ||
                !float.IsFinite(arrivalRadiusMeters) ||
                arrivalRadiusMeters <= 0f ||
                !float.IsFinite(maximumMetersPerSecond) ||
                maximumMetersPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distanceMeters));
            }

            float brakingDistance = Mathf.Max(
                0f,
                distanceMeters - arrivalRadiusMeters);
            float brakingLimitedSpeed = Mathf.Sqrt(
                2f * 5f * brakingDistance);
            return Mathf.Clamp(
                brakingLimitedSpeed,
                1.2f,
                maximumMetersPerSecond);
        }

        private bool TryResolveStoryTrafficWaypointPose(
            NpcRouteDefinition route,
            int waypointIndex,
            out NpcPose pose)
        {
            pose = default;
            if (route == null || waypointIndex < 0 ||
                waypointIndex >= route.WaypointAnchorIds.Count ||
                !foundation.TryGetAnchor(
                    route.WaypointAnchorIds[waypointIndex],
                    out NpcAnchorDefinition anchor))
            {
                return false;
            }

            Vector3 direction = Vector3.zero;
            int adjacentIndex = waypointIndex + 1 <
                                route.WaypointAnchorIds.Count
                ? waypointIndex + 1
                : waypointIndex - 1;
            if (adjacentIndex >= 0 && foundation.TryGetAnchor(
                    route.WaypointAnchorIds[adjacentIndex],
                    out NpcAnchorDefinition adjacent))
            {
                direction = adjacentIndex > waypointIndex
                    ? adjacent.Position - anchor.Position
                    : anchor.Position - adjacent.Position;
                direction.y = 0f;
            }

            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : anchor.Rotation;
            pose = new NpcPose(
                anchor.Position,
                rotation,
                anchor.CellId,
                shouldConformToGround: true);
            return true;
        }

        private static float HorizontalDistance(
            Vector3 left,
            Vector3 right)
        {
            Vector3 delta = left - right;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static bool TryResolvePiecewiseWaypointSpeedCap(
            IReadOnlyList<double> waypointProgress,
            float progress01,
            int brakeStartWaypointIndex,
            int coreStartWaypointIndex,
            int coreEndWaypointIndex,
            int releaseEndWaypointIndex,
            float approachKilometersPerHour,
            float coreKilometersPerHour,
            float releaseKilometersPerHour,
            out float speedCapMetersPerSecond)
        {
            speedCapMetersPerSecond = float.PositiveInfinity;
            if (!IsProgressInsideWaypointRange(
                    waypointProgress,
                    progress01,
                    brakeStartWaypointIndex,
                    releaseEndWaypointIndex))
            {
                return false;
            }

            float progress = Mathf.Clamp01(progress01);
            float speedKilometersPerHour;
            if (progress < waypointProgress[coreStartWaypointIndex])
            {
                float brake01 = SmoothStep01(Mathf.InverseLerp(
                    (float)waypointProgress[brakeStartWaypointIndex],
                    (float)waypointProgress[coreStartWaypointIndex],
                    progress));
                speedKilometersPerHour = Mathf.Lerp(
                    approachKilometersPerHour,
                    coreKilometersPerHour,
                    brake01);
            }
            else if (progress <= waypointProgress[coreEndWaypointIndex])
            {
                speedKilometersPerHour = coreKilometersPerHour;
            }
            else
            {
                float release01 = SmoothStep01(Mathf.InverseLerp(
                    (float)waypointProgress[coreEndWaypointIndex],
                    (float)waypointProgress[releaseEndWaypointIndex],
                    progress));
                speedKilometersPerHour = Mathf.Lerp(
                    coreKilometersPerHour,
                    releaseKilometersPerHour,
                    release01);
            }

            speedCapMetersPerSecond = speedKilometersPerHour / 3.6f;
            return true;
        }

        private static float SmoothStep01(float value)
        {
            float clamped = Mathf.Clamp01(value);
            return clamped * clamped * (3f - 2f * clamped);
        }

        private static bool IsProgressInsideWaypointRange(
            IReadOnlyList<double> waypointProgress,
            float progress01,
            int firstWaypointIndex,
            int lastWaypointIndex)
        {
            if (waypointProgress == null ||
                firstWaypointIndex < 0 ||
                lastWaypointIndex < firstWaypointIndex ||
                lastWaypointIndex >= waypointProgress.Count)
            {
                return false;
            }

            // Runtime route progress is carried as a float. Compare against
            // the same representation so an exact authored waypoint does not
            // fall just outside the corridor after double-to-float rounding.
            float progress = Mathf.Clamp01(progress01);
            return progress >= (float)waypointProgress[firstWaypointIndex] &&
                   progress <= (float)waypointProgress[lastWaypointIndex];
        }

        internal static bool IsInsideDonorPerajarviHandbrakeZone(
            Vector3 worldPosition)
        {
            float radiusSquared = DonorHandbrakeZoneRadiusMeters *
                                  DonorHandbrakeZoneRadiusMeters;
            for (int index = 0;
                 index < DonorPerajarviHandbrakeZones.Length;
                 index++)
            {
                Vector3 delta = worldPosition -
                                DonorPerajarviHandbrakeZones[index];
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        public NpcSimulation Simulation { get; private set; }
        public bool IsInitialized => initialized;
        public int MaterializedPresentationCount => presentations.Count;
        public IReadOnlyList<NpcTraceEntry> Trace =>
            Simulation?.Trace ?? Array.Empty<NpcTraceEntry>();

        public bool TryGetPresentation(
            string characterDefinitionId,
            out LegacyCharacterPresentationBinding presentation)
        {
            presentation = null;
            return initialized &&
                   !string.IsNullOrWhiteSpace(characterDefinitionId) &&
                   presentations.TryGetValue(
                       characterDefinitionId,
                       out presentation) &&
                   presentation != null;
        }

        public void ConfigureVehicleAudio(MonoBehaviour backendComponent)
        {
            if (backendComponent != null &&
                !(backendComponent is IAudioBackend))
            {
                throw new ArgumentException(
                    "NPC vehicle-audio component must implement IAudioBackend.",
                    nameof(backendComponent));
            }

            if (!ReferenceEquals(
                    vehicleAudioBackendComponent,
                    backendComponent))
            {
                DestroyStoryTrafficAudioOwners();
            }

            vehicleAudioBackendComponent = backendComponent;
            if (!initialized)
            {
                return;
            }

            foreach (KeyValuePair<string,
                         LegacyCharacterPresentationBinding> pair in
                     presentations)
            {
                if (Simulation.TryGetInstance(
                        pair.Key,
                        out CharacterInstance instance))
                {
                    ConfigureSpecializedPresentation(
                        pair.Value,
                        instance);
                }
            }
        }

        public void ConfigureStoryTrafficRescue(
            IInteractionActionSource configuredCarryActions)
        {
            if (ReferenceEquals(carryActionSource, configuredCarryActions))
            {
                return;
            }

            if (carryActionSource != null)
            {
                carryActionSource.ActionCompleted -=
                    HandleCarryActionCompleted;
            }

            carryActionSource = configuredCarryActions;
            if (carryActionSource != null)
            {
                carryActionSource.ActionCompleted +=
                    HandleCarryActionCompleted;
            }
        }

        public void Initialize(
            CharacterDefinitionCatalog characterCatalog,
            NpcFoundationCatalog foundationCatalog,
            CharacterPresentationCatalog configuredPresentationCatalog,
            IGameTimeService configuredGameTime,
            ProductionWorldStreamingService streaming)
        {
            Initialize(
                characterCatalog,
                foundationCatalog,
                configuredPresentationCatalog,
                configuredGameTime,
                streaming,
                null);
        }

        public void Initialize(
            CharacterDefinitionCatalog characterCatalog,
            NpcFoundationCatalog foundationCatalog,
            CharacterPresentationCatalog configuredPresentationCatalog,
            IGameTimeService configuredGameTime,
            ProductionWorldStreamingService streaming,
            Transform configuredPlayer)
        {
            productionStreaming = streaming ??
                throw new ArgumentNullException(nameof(streaming));
            InitializeInternal(
                characterCatalog,
                foundationCatalog,
                configuredPresentationCatalog,
                configuredGameTime,
                new ProductionNpcCellAvailability(streaming),
                new DirectNpcNavigationBackend(),
                configuredPlayer);
        }

        public void ConfigureDialogue(
            NpcDialogueCatalog configuredCatalog,
            NpcDialogueFeedbackHandler configuredFeedback,
            INpcExternalConditionSource externalConditions = null,
            INpcDomainEventSink eventSink = null)
        {
            dialogueCatalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
            dialogueRuntime = new NpcDialogueRuntime(
                externalConditions,
                eventSink);
            dialogueFeedback = configuredFeedback;

            if (initialized)
            {
                foreach (CharacterInstance instance in Simulation.Instances)
                {
                    if (presentations.TryGetValue(
                            instance.Definition.DefinitionId,
                            out LegacyCharacterPresentationBinding presentation) &&
                        presentation != null)
                    {
                        ConfigureDialogueInteraction(presentation, instance);
                    }
                }
            }
        }

        public NpcStateDto CaptureDto()
        {
            RequireInitialized();
            return Simulation.CaptureDto();
        }

        public StoryTrafficStateDto CaptureTrafficDto()
        {
            RequireInitialized();
            SynchronizeLiveStoryTrafficTransforms();
            return storyTrafficState.DeepClone();
        }

        public bool TryValidateTrafficDto(
            StoryTrafficStateDto dto,
            out string failure)
        {
            RequireInitialized();
            if (dto == null)
            {
                failure = "Story-traffic state is missing.";
                return false;
            }

            if (!dto.TryValidate(out failure))
            {
                return false;
            }

            foreach (StoryTrafficDriverStateDto driver in dto.drivers)
            {
                if (!Simulation.TryGetInstance(
                        driver.characterDefinitionId,
                        out CharacterInstance instance) ||
                    !string.Equals(
                        driver.stableInstanceId,
                        instance.StableInstanceId,
                        StringComparison.Ordinal))
                {
                    failure =
                        $"Story-traffic driver '{driver.characterDefinitionId}' has an incompatible stable identity.";
                    return false;
                }
            }

            if (!Simulation.TryGetInstance(
                    SuskiDefinitionId,
                    out CharacterInstance suski) ||
                !string.Equals(
                    dto.suski.stableInstanceId,
                    suski.StableInstanceId,
                    StringComparison.Ordinal))
            {
                failure =
                    "Suski rescue state has an incompatible stable identity.";
                return false;
            }

            if (dto.suski.stage == SuskiRescueStage.CrashedInCar)
            {
                StoryTrafficDriverStateDto jani = dto.drivers.FirstOrDefault(
                    driver => driver != null && string.Equals(
                        driver.characterDefinitionId,
                        "character.jani",
                        StringComparison.Ordinal));
                if (jani == null || !jani.terminalCrash)
                {
                    failure =
                        "Suski cannot be crashed in-car without Jani's terminal wreck.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        public bool TryRestoreTrafficDto(
            StoryTrafficStateDto dto,
            out string failure)
        {
            if (!TryValidateTrafficDto(dto, out failure))
            {
                return false;
            }

            storyTrafficState = dto.DeepClone();
            knownStoryTrafficResidency.Clear();
            residentStoryTrafficCharacters.Clear();
            storyTrafficResidencyPollRemainingSeconds = 0f;
            RepairCorruptBootstrapIncidentState();
            NormalizeSuskiTerminalCrashStage();
            if (storyTrafficState.suski.stage ==
                SuskiRescueStage.Transporting)
            {
                // Carry ownership is intentionally not serialized across a
                // session boundary. Resume as a loose roadside/body pickup at
                // the captured pose instead of fabricating a held object.
                storyTrafficState.suski.stage =
                    SuskiRescueStage.AwaitingPickup;
            }
            ApplyStoryTrafficIncidentState();
            ReconcileAllPresentations();
            return true;
        }

        private void NormalizeSuskiTerminalCrashStage()
        {
            if (storyTrafficState?.suski == null ||
                storyTrafficState.suski.stage != SuskiRescueStage.Passenger ||
                !TryGetStoryTrafficDriverState(
                    "character.jani",
                    out StoryTrafficDriverStateDto jani) ||
                !jani.terminalCrash)
            {
                return;
            }

            // Compatibility for a terminal wreck captured before the explicit
            // in-car stage existed. Do not reinterpret AwaitingPickup: that can
            // be a legitimate older save after the player already extracted her.
            storyTrafficState.suski.stage = SuskiRescueStage.CrashedInCar;
        }

        private void RepairCorruptBootstrapIncidentState()
        {
            if (storyTrafficState?.drivers == null)
            {
                return;
            }

            bool repairedJani = false;
            foreach (StoryTrafficDriverStateDto driver in
                     storyTrafficState.drivers)
            {
                if (driver == null || !driver.terminalCrash ||
                    !Simulation.TryGetInstance(
                        driver.characterDefinitionId,
                        out CharacterInstance instance) ||
                    !Simulation.TryResolvePose(instance, out NpcPose routePose) ||
                    Vector3.Distance(
                        driver.worldPosition,
                        routePose.Position) <=
                    CorruptIncidentRouteSeparationMeters)
                {
                    continue;
                }

                driver.terminalCrash = false;
                driver.worldPosition = routePose.Position;
                driver.worldRotation = routePose.Rotation;
                driver.lastCrashSpeedMetersPerSecond = 0f;
                driver.lastCrashDayIndex = -1L;
                driver.lastCrashSecondsOfDay = 0d;
                repairedJani |= string.Equals(
                    driver.characterDefinitionId,
                    "character.jani",
                    StringComparison.Ordinal);
            }

            if (repairedJani && storyTrafficState.suski != null &&
                storyTrafficState.suski.stage != SuskiRescueStage.Rescued)
            {
                storyTrafficState.suski.stage = SuskiRescueStage.Passenger;
            }
        }

        public void NotifyPlayerSlept()
        {
            RequireInitialized();
            if (storyTrafficState?.suski == null ||
                storyTrafficState.suski.stage !=
                SuskiRescueStage.RestingAtParentsBed)
            {
                return;
            }

            storyTrafficState.suski.stage = SuskiRescueStage.Rescued;
            SetSuskiRescuedFromJaniCrash(true);
        }

        public bool TryRestoreDto(NpcStateDto dto, out string failure)
        {
            RequireInitialized();
            if (!Simulation.TryRestoreDto(dto, out failure))
            {
                return false;
            }

            Simulation.SynchronizePhysicalRouteDriverClocks(
                gameTime.Snapshot.ElapsedGameSeconds);
            // Force an explicit snap to the restored route pose. Ordinary
            // clock reevaluation must not keep writing the logical pose into a
            // live Rigidbody, but a save restore is an intentional discontinuity.
            physicalStoryTrafficRoutes.Clear();
            forcePhysicalPlacement.Add("character.jani");
            forcePhysicalPlacement.Add("character.petteri");
            ApplyStoryTrafficScenarioState(
                gameTime.Snapshot,
                allowAutomaticStart: false);
            if (storyTrafficState?.suski != null &&
                storyTrafficState.suski.stage ==
                    SuskiRescueStage.Passenger &&
                Simulation.TryGetInstance(
                    SuskiDefinitionId,
                    out CharacterInstance restoredSuski) &&
                restoredSuski.GetFlag(
                    SuskiRescuedFromJaniCrashFlagId))
            {
                // Compatibility for saves created before traffic.state existed.
                storyTrafficState.suski.stage = SuskiRescueStage.Rescued;
            }
            ApplySuskiStoryState();
            ReconcileAllPresentations();
            return true;
        }

        public bool TryValidateDto(NpcStateDto dto, out string failure)
        {
            RequireInitialized();
            return Simulation.TryValidateDto(dto, out failure);
        }

        public void ReevaluateNow()
        {
            RequireInitialized();
            Evaluate(gameTime.Snapshot);
        }

        public bool TryGetInstance(
            string characterDefinitionId,
            out CharacterInstance instance)
        {
            RequireInitialized();
            return Simulation.TryGetInstance(characterDefinitionId, out instance);
        }

        public void SetSuskiRescuedFromJaniCrash(bool rescued)
        {
            RequireInitialized();
            if (!Simulation.TryGetInstance(
                    SuskiDefinitionId,
                    out CharacterInstance suski))
            {
                throw new InvalidOperationException(
                    "Suski character state is unavailable.");
            }

            suski.SetFlag(SuskiRescuedFromJaniCrashFlagId, rescued);
            if (storyTrafficState?.suski != null)
            {
                storyTrafficState.suski.stage = rescued
                    ? SuskiRescueStage.Rescued
                    : SuskiRescueStage.Passenger;
            }
            RemovePresentation(SuskiDefinitionId);
            ApplySuskiStoryState();
            ReconcileAllPresentations();
        }

        public bool CanExtractSuskiFromCrashedCar()
        {
            if (!initialized || storyTrafficState?.suski == null ||
                storyTrafficState.suski.stage !=
                    SuskiRescueStage.CrashedInCar)
            {
                return false;
            }

            return TryGetStoryTrafficDriverState(
                       "character.jani",
                       out StoryTrafficDriverStateDto jani) &&
                   jani.terminalCrash;
        }

        public bool TryExtractSuskiFromCrashedCar()
        {
            if (!CanExtractSuskiFromCrashedCar())
            {
                return false;
            }

            SuskiRescueStateDto rescue = storyTrafficState.suski;
            if (presentations.TryGetValue(
                    "character.jani",
                    out LegacyCharacterPresentationBinding presentation) &&
                presentation != null)
            {
                StoryTrafficVehiclePresentationBinding traffic =
                    presentation.GetComponent<
                        StoryTrafficVehiclePresentationBinding>();
                if (traffic != null && Simulation.TryGetInstance(
                        SuskiDefinitionId,
                        out CharacterInstance suski) &&
                    traffic.TryGetPassengerWorldPose(
                        suski.Definition.FeatureId,
                        out Vector3 passengerPosition,
                        out Quaternion passengerRotation))
                {
                    // Extraction begins from the current wreck-space passenger
                    // pose; no side-of-car teleport or synthetic eject impulse.
                    rescue.worldPosition = passengerPosition;
                    rescue.worldRotation = passengerRotation;
                }
            }

            rescue.stage = SuskiRescueStage.AwaitingPickup;
            ApplySuskiStoryState();
            ReconcileAllPresentations();
            return true;
        }

        public void EndGameSession()
        {
            if (!initialized)
            {
                return;
            }

            gameTimeSubscription?.Dispose();
            gameTimeSubscription = null;
            ConfigureStoryTrafficRescue(null);
            if (cellAvailability != null)
            {
                cellAvailability.AvailabilityChanged -=
                    HandleCellAvailabilityChanged;
                cellAvailability.Dispose();
            }

            foreach (LegacyCharacterPresentationBinding presentation in
                     presentations.Values.ToArray())
            {
                DestroyPresentation(presentation);
            }

            presentations.Clear();
            storyTrafficMotionStates.Clear();
            physicalStoryTrafficRoutes.Clear();
            forcePhysicalPlacement.Clear();
            knownStoryTrafficResidency.Clear();
            residentStoryTrafficCharacters.Clear();
            ReleaseAllStoryTrafficCellRetentions();
            productionStreaming = null;
            DestroyStoryTrafficAudioOwners();
            teimoShopWorldPresentation?.Dispose();
            teimoShopWorldPresentation = null;
            player = null;
            storyTrafficState = null;
            storyTrafficResidencyPollRemainingSeconds = 0f;
            initialized = false;
        }

        private void OnDestroy()
        {
            EndGameSession();
        }

        internal void InitializeInternal(
            CharacterDefinitionCatalog characterCatalog,
            NpcFoundationCatalog foundationCatalog,
            CharacterPresentationCatalog configuredPresentationCatalog,
            IGameTimeService configuredGameTime,
            INpcCellAvailability configuredCellAvailability,
            INpcNavigationBackend configuredNavigation,
            Transform configuredPlayer = null)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "NPC world runtime is already initialized.");
            }

            characters = characterCatalog ??
                throw new ArgumentNullException(nameof(characterCatalog));
            foundation = foundationCatalog ??
                throw new ArgumentNullException(nameof(foundationCatalog));
            presentationCatalog = configuredPresentationCatalog ??
                throw new ArgumentNullException(nameof(configuredPresentationCatalog));
            gameTime = configuredGameTime ??
                throw new ArgumentNullException(nameof(configuredGameTime));
            cellAvailability = configuredCellAvailability ??
                throw new ArgumentNullException(nameof(configuredCellAvailability));
            navigation = configuredNavigation ??
                throw new ArgumentNullException(nameof(configuredNavigation));
            player = configuredPlayer;
            storyTrafficResidencyPollRemainingSeconds = 0f;

            IReadOnlyList<string> presentationFailures =
                presentationCatalog.ValidateConfiguration();
            if (presentationFailures.Count > 0)
            {
                throw new ArgumentException(
                    "NPC presentation catalog is invalid: " +
                    string.Join(" | ", presentationFailures));
            }

            foreach (CharacterDefinition definition in characters.Definitions)
            {
                if (!definition.StateOnly &&
                    !presentationCatalog.TryGet(
                        definition.PresentationBindingId,
                        out _))
                {
                    throw new ArgumentException(
                        $"NPC '{definition.DefinitionId}' has no project-owned presentation binding '{definition.PresentationBindingId}'.");
                }
            }

            foreach (NpcScheduleBlock block in foundationCatalog.ScheduleBlocks)
            {
                if (!string.IsNullOrEmpty(
                        block.PresentationBindingIdOverride) &&
                    !presentationCatalog.TryGet(
                        block.PresentationBindingIdOverride,
                        out _))
                {
                    throw new ArgumentException(
                        $"NPC schedule '{block.ScheduleBlockId}' has no project-owned presentation binding override '{block.PresentationBindingIdOverride}'.");
                }
            }

            Simulation = new NpcSimulation(characters, foundation);
            storyTrafficState = CreateInitialStoryTrafficState();
            GameTimeSnapshot initialSnapshot = gameTime.Snapshot;
            RegisterPhysicalRouteDrivers(initialSnapshot);
            if (Simulation.TryGetInstance(
                    "character.fixture.stationary-service",
                    out CharacterInstance teimo))
            {
                teimoShopWorldPresentation =
                    new TeimoShopWorldPresentationController(teimo);
            }

            cellAvailability.AvailabilityChanged +=
                HandleCellAvailabilityChanged;
            gameTimeSubscription = gameTime.Subscribe(HandleGameTimeEvent);
            initialized = true;
            Evaluate(initialSnapshot);
        }

        private void HandleGameTimeEvent(in GameTimeEvent gameTimeEvent)
        {
            Evaluate(gameTimeEvent.Current);
        }

        private void Evaluate(GameTimeSnapshot snapshot)
        {
            Simulation.Evaluate(
                snapshot.DayIndex,
                snapshot.SecondsOfDay,
                snapshot.ElapsedGameSeconds);
            ApplyStoryTrafficScenarioState(
                snapshot,
                allowAutomaticStart: true);
            ApplySuskiStoryState();
            ReconcileAllPresentations();
        }

        private void UpdateTemporaryStoryTrafficRaceTrigger(float deltaTime)
        {
            if (player == null ||
                !TryGetStoryTrafficPair(
                    out CharacterInstance jani,
                    out CharacterInstance petteri) ||
                !IsStoryTrafficAvailable(jani) ||
                !IsStoryTrafficAvailable(petteri) ||
                jani.GetFlag(StoryTrafficRaceStartedFlagId))
            {
                storyTrafficPlayerTriggerSeconds = 0f;
                return;
            }

            float nearestSqrDistance = float.PositiveInfinity;
            if (TryGetStoryTrafficPosition(jani, out Vector3 janiPosition))
            {
                nearestSqrDistance = Mathf.Min(
                    nearestSqrDistance,
                    (player.position - janiPosition).sqrMagnitude);
            }

            if (TryGetStoryTrafficPosition(
                    petteri,
                    out Vector3 petteriPosition))
            {
                nearestSqrDistance = Mathf.Min(
                    nearestSqrDistance,
                    (player.position - petteriPosition).sqrMagnitude);
            }

            float triggerDistanceSqr =
                TemporaryRaceTriggerDistanceMeters *
                TemporaryRaceTriggerDistanceMeters;
            if (nearestSqrDistance > triggerDistanceSqr)
            {
                storyTrafficPlayerTriggerSeconds = 0f;
                return;
            }

            storyTrafficPlayerTriggerSeconds += Mathf.Max(0f, deltaTime);
            if (storyTrafficPlayerTriggerSeconds <
                TemporaryRaceTriggerDwellSeconds)
            {
                return;
            }

            ApplyStoryTrafficScenarioState(
                gameTime.Snapshot,
                allowAutomaticStart: true,
                forcePlayerTrigger: true);
            ReconcileAllPresentations();
        }

        private void ApplyStoryTrafficScenarioState(
            in GameTimeSnapshot snapshot,
            bool allowAutomaticStart,
            bool forcePlayerTrigger = false)
        {
            if (!TryGetStoryTrafficPair(
                    out CharacterInstance jani,
                    out CharacterInstance petteri))
            {
                return;
            }

            bool available = IsStoryTrafficAvailable(jani) &&
                             IsStoryTrafficAvailable(petteri);
            if (!available)
            {
                jani.SetFlag(StoryTrafficRaceStartedFlagId, false);
                petteri.SetFlag(StoryTrafficRaceStartedFlagId, false);
                jani.SetFlag(StoryTrafficDancehallSessionFlagId, false);
                petteri.SetFlag(StoryTrafficDancehallSessionFlagId, false);
                storyTrafficPlayerTriggerSeconds = 0f;
                return;
            }

            bool migratedActiveRoute =
                !string.IsNullOrEmpty(jani.CurrentRouteId) ||
                !string.IsNullOrEmpty(petteri.CurrentRouteId);
            bool raceStarted =
                jani.GetFlag(StoryTrafficRaceStartedFlagId) ||
                petteri.GetFlag(StoryTrafficRaceStartedFlagId) ||
                migratedActiveRoute;
            bool wrappedNightWindow =
                snapshot.SecondsOfDay < 7200d;
            bool automaticStart = allowAutomaticStart &&
                (wrappedNightWindow ||
                 snapshot.SecondsOfDay >=
                 TemporaryAutomaticRaceStartSecondsOfDay);
            if (!raceStarted && (forcePlayerTrigger || automaticStart))
            {
                raceStarted = true;
                bool dancehallSession =
                    IsSaturday(snapshot.DayIndex) &&
                    snapshot.SecondsOfDay >=
                    SaturdayDancehallStartSecondsOfDay;
                jani.SetFlag(
                    StoryTrafficDancehallSessionFlagId,
                    dancehallSession);
                petteri.SetFlag(
                    StoryTrafficDancehallSessionFlagId,
                    dancehallSession);
            }

            jani.SetFlag(StoryTrafficRaceStartedFlagId, raceStarted);
            petteri.SetFlag(StoryTrafficRaceStartedFlagId, raceStarted);
            if (!raceStarted)
            {
                ApplyStoryTrafficWaitingState(jani);
                ApplyStoryTrafficWaitingState(petteri);
                return;
            }

            bool useDancehall =
                jani.GetFlag(StoryTrafficDancehallSessionFlagId) ||
                petteri.GetFlag(StoryTrafficDancehallSessionFlagId);
            ApplyStoryTrafficDrivingState(
                jani,
                useDancehall ? JaniDancehallRouteId : JaniRaceRouteId);
            ApplyStoryTrafficDrivingState(
                petteri,
                useDancehall
                    ? PetteriDancehallRouteId
                    : PetteriRaceRouteId);
        }

        private static bool IsSaturday(long dayIndex)
        {
            long weekday = dayIndex % 7L;
            if (weekday < 0L)
            {
                weekday += 7L;
            }

            // The authoritative calendar starts on Tuesday 01.08.1995.
            return weekday == 4L;
        }

        private bool TryGetStoryTrafficPair(
            out CharacterInstance jani,
            out CharacterInstance petteri)
        {
            bool hasJani = Simulation.TryGetInstance(
                "character.jani",
                out jani);
            bool hasPetteri = Simulation.TryGetInstance(
                "character.petteri",
                out petteri);
            return hasJani && hasPetteri;
        }

        private static bool IsStoryTrafficAvailable(
            CharacterInstance instance) =>
            instance != null &&
            instance.ActivityState != CharacterActivityState.Hidden &&
            instance.ActivityState != CharacterActivityState.Disabled;

        private bool TryGetStoryTrafficPosition(
            CharacterInstance instance,
            out Vector3 position)
        {
            if (presentations.TryGetValue(
                    instance.Definition.DefinitionId,
                    out LegacyCharacterPresentationBinding presentation) &&
                presentation != null)
            {
                StoryTrafficVehiclePresentationBinding traffic =
                    presentation.GetComponent<
                        StoryTrafficVehiclePresentationBinding>();
                position = traffic != null
                    ? traffic.PhysicalWorldPosition
                    : presentation.transform.position;
                return true;
            }

            if (Simulation.TryResolvePose(instance, out NpcPose pose))
            {
                position = pose.Position;
                return true;
            }

            position = default;
            return false;
        }

        private static void ApplyStoryTrafficWaitingState(
            CharacterInstance instance)
        {
            if (string.IsNullOrEmpty(instance.CurrentRouteId))
            {
                return;
            }

            instance.ApplyScheduleState(
                instance.ActiveScheduleBlockId,
                instance.CurrentAnchorId,
                string.Empty,
                0d,
                CharacterActivityState.VehicleSeated);
        }

        private static void ApplyStoryTrafficDrivingState(
            CharacterInstance instance,
            string routeId)
        {
            double progress = string.Equals(
                    instance.CurrentRouteId,
                    routeId,
                    StringComparison.Ordinal)
                ? instance.RouteProgress01
                : 0d;
            instance.ApplyScheduleState(
                instance.ActiveScheduleBlockId,
                instance.CurrentAnchorId,
                routeId,
                progress,
                CharacterActivityState.VehicleSeated);
        }

        private void RegisterPhysicalRouteDrivers(
            in GameTimeSnapshot snapshot)
        {
            foreach (CharacterDefinition definition in characters.Definitions)
            {
                bool usesPhysicalDriver =
                    IsPhysicalStoryTrafficBinding(
                        definition.PresentationBindingId) ||
                    foundation.GetSchedule(definition.DefinitionId)
                        .Any(block => IsPhysicalStoryTrafficBinding(
                            block.PresentationBindingIdOverride));
                if (usesPhysicalDriver)
                {
                    Simulation.RegisterPhysicalRouteDriver(
                        definition.DefinitionId,
                        snapshot.ElapsedGameSeconds);
                }
            }
        }

        private bool IsPhysicalStoryTrafficBinding(string bindingId)
        {
            if (string.IsNullOrEmpty(bindingId) ||
                !presentationCatalog.TryGet(
                    bindingId,
                    out CharacterPresentationCatalogEntry entry) ||
                entry.WrapperPrefab == null)
            {
                return false;
            }

            return entry.WrapperPrefab
                .GetComponents<MonoBehaviour>()
                .Any(component =>
                    component is IStoryTrafficVehicleMotionBackend);
        }

        private void ApplySuskiStoryState()
        {
            if (!Simulation.TryGetInstance(
                    SuskiDefinitionId,
                    out CharacterInstance suski))
            {
                return;
            }

            bool rescued = suski.GetFlag(
                SuskiRescuedFromJaniCrashFlagId);
            bool rescueBodyActive = storyTrafficState?.suski != null &&
                IsSuskiLooseRescueBody(storyTrafficState.suski.stage);
            suski.ApplyScheduleState(
                string.Empty,
                suski.Definition.HomeAnchorId,
                string.Empty,
                0d,
                rescued || rescueBodyActive
                    ? CharacterActivityState.Idle
                    : CharacterActivityState.Hidden);
        }

        internal static bool IsSuskiInsideJaniVehicle(
            SuskiRescueStage stage) =>
            stage == SuskiRescueStage.Passenger ||
            stage == SuskiRescueStage.CrashedInCar;

        internal static bool IsSuskiLooseRescueBody(
            SuskiRescueStage stage) =>
            stage == SuskiRescueStage.AwaitingPickup ||
            stage == SuskiRescueStage.Transporting ||
            stage == SuskiRescueStage.RestingAtParentsBed;

        private StoryTrafficStateDto CreateInitialStoryTrafficState()
        {
            var dto = new StoryTrafficStateDto
            {
                drivers = new[]
                {
                    CreateInitialStoryTrafficDriverState("character.jani"),
                    CreateInitialStoryTrafficDriverState("character.petteri"),
                },
            };
            if (Simulation.TryGetInstance(
                    SuskiDefinitionId,
                    out CharacterInstance suski))
            {
                dto.suski.stableInstanceId = suski.StableInstanceId;
                if (Simulation.TryResolvePose(suski, out NpcPose pose))
                {
                    dto.suski.worldPosition = pose.Position;
                    dto.suski.worldRotation = pose.Rotation;
                }
            }

            return dto;
        }

        private StoryTrafficDriverStateDto
            CreateInitialStoryTrafficDriverState(string characterId)
        {
            if (!Simulation.TryGetInstance(
                    characterId,
                    out CharacterInstance instance))
            {
                throw new InvalidOperationException(
                    $"Story-traffic character '{characterId}' is missing.");
            }

            var state = new StoryTrafficDriverStateDto
            {
                characterDefinitionId = characterId,
                stableInstanceId = instance.StableInstanceId,
            };
            if (Simulation.TryResolvePose(instance, out NpcPose pose))
            {
                state.worldPosition = pose.Position;
                state.worldRotation = pose.Rotation;
            }

            return state;
        }

        private void HandleStoryTrafficCollision(
            StoryTrafficCollisionEvent incident)
        {
            if (!incident.IsCrash || storyTrafficState == null)
            {
                return;
            }

            string characterId = ResolveStoryTrafficCharacterId(
                incident.DriverFeatureId);
            if (string.IsNullOrEmpty(characterId) ||
                !TryGetStoryTrafficDriverState(
                    characterId,
                    out StoryTrafficDriverStateDto driverState))
            {
                return;
            }

            driverState.crashCount++;
            driverState.lastCrashDayIndex = gameTime.Snapshot.DayIndex;
            driverState.lastCrashSecondsOfDay =
                gameTime.Snapshot.SecondsOfDay;
            driverState.lastCrashSpeedMetersPerSecond =
                incident.SpeedMetersPerSecond;

            if (presentations.TryGetValue(
                    characterId,
                    out LegacyCharacterPresentationBinding presentation) &&
                presentation != null)
            {
                ResolveAuthoritativePresentationPose(
                    presentation,
                    out driverState.worldPosition,
                    out driverState.worldRotation);
            }
        }

        private void HandleStoryTrafficTerminalCrash(
            StoryTrafficTerminalCrashEvent incident)
        {
            if (storyTrafficState == null)
            {
                return;
            }

            string characterId = ResolveStoryTrafficCharacterId(
                incident.DriverFeatureId);
            if (string.IsNullOrEmpty(characterId) ||
                !TryGetStoryTrafficDriverState(
                    characterId,
                    out StoryTrafficDriverStateDto driverState) ||
                driverState.terminalCrash)
            {
                return;
            }

            GameTimeSnapshot snapshot = gameTime.Snapshot;
            bool collisionAlreadyRecorded =
                driverState.lastCrashDayIndex == snapshot.DayIndex &&
                Math.Abs(
                    driverState.lastCrashSecondsOfDay -
                    snapshot.SecondsOfDay) <= 1d;
            if (!collisionAlreadyRecorded)
            {
                driverState.crashCount++;
            }

            driverState.terminalCrash = true;
            driverState.lastCrashDayIndex = snapshot.DayIndex;
            driverState.lastCrashSecondsOfDay = snapshot.SecondsOfDay;
            driverState.lastCrashSpeedMetersPerSecond =
                incident.SpeedMetersPerSecond;

            if (presentations.TryGetValue(
                    characterId,
                    out LegacyCharacterPresentationBinding presentation) &&
                presentation != null)
            {
                StoryTrafficVehiclePresentationBinding traffic =
                    presentation.GetComponent<
                        StoryTrafficVehiclePresentationBinding>();
                driverState.worldPosition = traffic != null
                    ? traffic.PhysicalWorldPosition
                    : presentation.transform.position;
                driverState.worldRotation = traffic != null
                    ? traffic.PhysicalWorldRotation
                    : presentation.transform.rotation;
                traffic?.SetStoryIncidentHold(true);

                if (traffic != null && string.Equals(
                        characterId,
                        "character.jani",
                        StringComparison.Ordinal) &&
                    storyTrafficState.suski.stage ==
                        SuskiRescueStage.Passenger)
                {
                    storyTrafficState.suski.stage =
                        SuskiRescueStage.CrashedInCar;
                    if (Simulation.TryGetInstance(
                            SuskiDefinitionId,
                            out CharacterInstance suski) &&
                        traffic.TryGetPassengerWorldPose(
                            suski.Definition.FeatureId,
                            out Vector3 passengerPosition,
                            out Quaternion passengerRotation))
                    {
                        // Persist the in-car pose for streaming/save. The body
                        // remains owned by the wreck until explicit extraction.
                        storyTrafficState.suski.worldPosition =
                            passengerPosition;
                        storyTrafficState.suski.worldRotation =
                            passengerRotation;
                    }
                }
            }

            ApplySuskiStoryState();
            ReconcileAllPresentations();
        }

        private string ResolveStoryTrafficCharacterId(string featureId)
        {
            foreach (string characterId in new[]
                     {
                         "character.jani",
                         "character.petteri",
                     })
            {
                if (Simulation.TryGetInstance(
                        characterId,
                        out CharacterInstance instance) &&
                    string.Equals(
                        instance.Definition.FeatureId,
                        featureId,
                        StringComparison.Ordinal))
                {
                    return characterId;
                }
            }

            return string.Empty;
        }

        private bool TryGetStoryTrafficDriverState(
            string characterId,
            out StoryTrafficDriverStateDto state)
        {
            StoryTrafficDriverStateDto[] drivers =
                storyTrafficState?.drivers ??
                Array.Empty<StoryTrafficDriverStateDto>();
            for (int index = 0; index < drivers.Length; index++)
            {
                if (drivers[index] != null &&
                    string.Equals(
                        drivers[index].characterDefinitionId,
                        characterId,
                        StringComparison.Ordinal))
                {
                    state = drivers[index];
                    return true;
                }
            }

            state = null;
            return false;
        }

        private void HandleCarryActionCompleted(
            InteractionActionCompleted action)
        {
            if (storyTrafficState?.suski == null ||
                !Simulation.TryGetInstance(
                    SuskiDefinitionId,
                    out CharacterInstance suski) ||
                !string.Equals(
                    action.TargetStableId.Value,
                    suski.StableInstanceId,
                    StringComparison.Ordinal))
            {
                return;
            }

            SuskiRescueStateDto rescue = storyTrafficState.suski;
            rescue.worldPosition = action.WorldPosition;
            if (presentations.TryGetValue(
                    SuskiDefinitionId,
                    out LegacyCharacterPresentationBinding presentation) &&
                presentation != null)
            {
                SuskiRescueRagdoll ragdoll = presentation.GetComponent<
                    SuskiRescueRagdoll>();
                rescue.worldRotation = ragdoll != null &&
                    ragdoll.TryGetPrimaryPose(
                        out _,
                        out Quaternion ragdollRotation)
                            ? ragdollRotation
                            : presentation.transform.rotation;
            }

            switch (action.Action)
            {
                case InteractionActionKind.Pickup
                    when rescue.stage == SuskiRescueStage.AwaitingPickup:
                    rescue.stage = SuskiRescueStage.Transporting;
                    break;

                case InteractionActionKind.Drop:
                case InteractionActionKind.Place:
                    if (rescue.stage != SuskiRescueStage.Transporting)
                    {
                        return;
                    }

                    if (foundation.TryGetAnchor(
                            SuskiRescueBedAnchorId,
                            out NpcAnchorDefinition bed) &&
                        Vector3.Distance(
                            action.WorldPosition,
                            bed.Position) <=
                        SuskiBedAcceptanceRadiusMeters)
                    {
                        rescue.stage =
                            SuskiRescueStage.RestingAtParentsBed;
                        rescue.worldPosition = bed.Position;
                        rescue.worldRotation = bed.Rotation;
                    }
                    else
                    {
                        rescue.stage = SuskiRescueStage.AwaitingPickup;
                    }

                    break;

                case InteractionActionKind.Throw:
                    if (rescue.stage == SuskiRescueStage.Transporting)
                    {
                        rescue.stage = SuskiRescueStage.AwaitingPickup;
                    }

                    break;

                default:
                    return;
            }

            ApplySuskiStoryState();
            ReconcileAllPresentations();
        }

        private void SynchronizeLiveStoryTrafficTransforms()
        {
            foreach (StoryTrafficDriverStateDto driver in
                     storyTrafficState.drivers)
            {
                if (driver == null || !presentations.TryGetValue(
                        driver.characterDefinitionId,
                        out LegacyCharacterPresentationBinding presentation) ||
                    presentation == null)
                {
                    continue;
                }

                StoryTrafficVehiclePresentationBinding traffic =
                    presentation.GetComponent<
                        StoryTrafficVehiclePresentationBinding>();
                if (driver.teimoSocialStopConsumed && traffic != null)
                {
                    driver.teimoSocialStopSecondsRemaining =
                        traffic.SocialStopSecondsRemaining;
                }

                if (driver.terminalCrash || driver.routeFullStopReached)
                {
                    ResolveAuthoritativePresentationPose(
                        presentation,
                        out driver.worldPosition,
                        out driver.worldRotation);
                }
            }

            if (IsSuskiLooseRescueBody(storyTrafficState.suski.stage) &&
                presentations.TryGetValue(
                    SuskiDefinitionId,
                    out LegacyCharacterPresentationBinding suski) &&
                suski != null)
            {
                SuskiRescueRagdoll ragdoll = suski.GetComponent<
                    SuskiRescueRagdoll>();
                if (ragdoll != null && ragdoll.TryGetPrimaryPose(
                        out Vector3 position,
                        out Quaternion rotation))
                {
                    storyTrafficState.suski.worldPosition = position;
                    storyTrafficState.suski.worldRotation = rotation;
                }
                else
                {
                    storyTrafficState.suski.worldPosition =
                        suski.transform.position;
                    storyTrafficState.suski.worldRotation =
                        suski.transform.rotation;
                }
            }
        }

        private void SynchronizeLiveSuskiRagdollPose()
        {
            if (storyTrafficState?.suski == null ||
                !IsSuskiLooseRescueBody(storyTrafficState.suski.stage) ||
                !presentations.TryGetValue(
                    SuskiDefinitionId,
                    out LegacyCharacterPresentationBinding presentation) ||
                presentation == null)
            {
                return;
            }

            SuskiRescueRagdoll ragdoll = presentation.GetComponent<
                SuskiRescueRagdoll>();
            if (ragdoll != null && ragdoll.TryGetPrimaryPose(
                    out Vector3 position,
                    out Quaternion rotation))
            {
                storyTrafficState.suski.worldPosition = position;
                storyTrafficState.suski.worldRotation = rotation;
            }
        }

        private void ApplyStoryTrafficIncidentState()
        {
            foreach (StoryTrafficDriverStateDto driver in
                     storyTrafficState.drivers)
            {
                if (driver == null ||
                    !driver.terminalCrash && !driver.routeFullStopReached)
                {
                    continue;
                }

                forcePhysicalPlacement.Add(driver.characterDefinitionId);
                if (presentations.TryGetValue(
                        driver.characterDefinitionId,
                        out LegacyCharacterPresentationBinding presentation) &&
                    presentation != null)
                {
                    presentation.transform.SetPositionAndRotation(
                        driver.worldPosition,
                        driver.worldRotation);
                    StoryTrafficVehiclePresentationBinding traffic =
                        presentation.GetComponent<
                            StoryTrafficVehiclePresentationBinding>();
                    traffic?.SetStoryIncidentHold(driver.terminalCrash);
                    traffic?.SetRouteFullStopHold(
                        driver.routeFullStopReached);
                }

                if (storyTrafficAudioOwners.TryGetValue(
                        driver.characterDefinitionId,
                        out StoryTrafficVehicleAudioPresenter audio) &&
                    audio != null)
                {
                    audio.SetTerminallyDisabled(true);
                }
            }

            if (Simulation.TryGetInstance(
                    SuskiDefinitionId,
                    out CharacterInstance suski))
            {
                suski.SetFlag(
                    SuskiRescuedFromJaniCrashFlagId,
                    storyTrafficState.suski.stage ==
                    SuskiRescueStage.Rescued);
            }

            ApplySuskiStoryState();
        }

        private NpcPose ResolveStoryIncidentPose(
            CharacterInstance instance,
            in NpcPose fallback)
        {
            if (instance == null || storyTrafficState == null)
            {
                return fallback;
            }

            Vector3 position;
            Quaternion rotation;
            if (TryGetStoryTrafficDriverState(
                    instance.Definition.DefinitionId,
                    out StoryTrafficDriverStateDto driver) &&
                (driver.terminalCrash || driver.routeFullStopReached))
            {
                position = driver.worldPosition;
                rotation = driver.worldRotation;
            }
            else if (string.Equals(
                         instance.Definition.DefinitionId,
                         SuskiDefinitionId,
                         StringComparison.Ordinal) &&
                     IsSuskiLooseRescueBody(
                         storyTrafficState.suski.stage))
            {
                position = storyTrafficState.suski.worldPosition;
                rotation = storyTrafficState.suski.worldRotation;
            }
            else
            {
                return fallback;
            }

            string cellId = fallback.CellId;
            if (cellAvailability.TryResolveCellId(
                    position,
                    out string resolvedCellId))
            {
                cellId = resolvedCellId;
            }
            return new NpcPose(
                position,
                rotation,
                cellId,
                shouldConformToGround: false);
        }

        private void ConfigureSuskiRescuePresentation(
            LegacyCharacterPresentationBinding presentation,
            CharacterInstance instance)
        {
            SuskiRescueStateDto rescue = storyTrafficState?.suski;
            if (presentation == null || instance == null || rescue == null ||
                !IsSuskiLooseRescueBody(rescue.stage))
            {
                return;
            }

            if (!StableEntityId.TryParse(
                    instance.StableInstanceId,
                    out StableEntityId stableId))
            {
                throw new InvalidOperationException(
                    "Suski has no valid project-owned stable identity.");
            }

            bool resting = rescue.stage ==
                SuskiRescueStage.RestingAtParentsBed;
            NpcDialogueInteractionTarget dialogue =
                presentation.GetComponent<NpcDialogueInteractionTarget>();
            SuskiRescueRagdoll ragdoll = GetOrAddComponent<
                SuskiRescueRagdoll>(presentation.gameObject);
            ragdoll.Initialize(
                presentation,
                stableId,
                rescue.worldPosition,
                rescue.worldRotation,
                resting,
                dialogue);
        }

        private void HandleCellAvailabilityChanged(
            string cellId,
            bool _)
        {
            teimoShopWorldPresentation?.MarkBindingsDirty();
            foreach (CharacterInstance instance in Simulation.Instances)
            {
                if (!Simulation.TryResolvePose(instance, out NpcPose pose))
                {
                    continue;
                }

                NpcPose effectivePose = ResolveStoryIncidentPose(
                    instance,
                    pose);
                bool logicalCellChanged = string.Equals(
                    effectivePose.CellId,
                    cellId,
                    StringComparison.Ordinal);
                bool physicalCellChanged = TryGetPhysicalStoryTrafficCell(
                    instance.Definition.DefinitionId,
                    out string physicalCellId) &&
                    string.Equals(
                        physicalCellId,
                        cellId,
                        StringComparison.Ordinal);
                if (logicalCellChanged || physicalCellChanged)
                {
                    ReconcilePresentation(
                        instance,
                        pose,
                        cellAvailability.IsLoaded(pose.CellId));
                }
            }

            teimoShopWorldPresentation?.Reconcile();
        }

        private void ReconcileAllPresentations()
        {
            foreach (CharacterInstance instance in Simulation.Instances)
            {
                if (!Simulation.TryResolvePose(instance, out NpcPose pose))
                {
                    RemovePresentation(instance.Definition.DefinitionId);
                    continue;
                }

                ReconcilePresentation(
                    instance,
                    pose,
                    cellAvailability.IsLoaded(pose.CellId));
            }

            teimoShopWorldPresentation?.Reconcile();
        }

        private void ReconcilePhysicalStoryTrafficResidency()
        {
            ReconcilePhysicalStoryTrafficResidency("character.jani");
            ReconcilePhysicalStoryTrafficResidency("character.petteri");
        }

        private void ReconcilePhysicalStoryTrafficResidency(
            string characterId)
        {
            if (!Simulation.TryGetInstance(
                    characterId,
                    out CharacterInstance instance) ||
                !Simulation.TryResolvePose(instance, out NpcPose pose))
            {
                RemovePresentation(characterId);
                return;
            }

            knownStoryTrafficResidency.Add(characterId);
            if (ShouldKeepStoryTrafficPhysical(instance))
            {
                residentStoryTrafficCharacters.Add(characterId);
                RetainStoryTrafficCell(
                    characterId,
                    "current",
                    pose.Position);
            }
            else
            {
                residentStoryTrafficCharacters.Remove(characterId);
                ReleaseStoryTrafficCellRetentions(characterId);
            }

            ReconcilePresentation(
                instance,
                pose,
                cellAvailability.IsLoaded(pose.CellId));
        }

        private bool ShouldKeepStoryTrafficPhysical(
            CharacterInstance instance)
        {
            if (instance == null ||
                !IsStoryTrafficCharacter(
                    instance.Definition.DefinitionId))
            {
                return false;
            }

            bool terminalState = TryGetStoryTrafficDriverState(
                    instance.Definition.DefinitionId,
                    out StoryTrafficDriverStateDto state) &&
                (state.terminalCrash || state.routeFullStopReached);
            return ShouldKeepStoryTrafficPhysical(
                instance.ActivityState,
                instance.CurrentRouteId,
                terminalState);
        }

        private void ReconcilePresentation(
            CharacterInstance instance,
            in NpcPose pose,
            bool cellLoaded)
        {
            string characterId = instance.Definition.DefinitionId;
            NpcPose effectivePose = ResolveStoryIncidentPose(
                instance,
                pose);
            bool effectiveCellLoaded = string.Equals(
                    effectivePose.CellId,
                    pose.CellId,
                    StringComparison.Ordinal)
                ? cellLoaded
                : cellAvailability.IsLoaded(effectivePose.CellId);
            ReconcileStoryTrafficAudioOwner(instance, effectivePose);
            if (instance.Definition.StateOnly)
            {
                RemovePresentation(characterId);
                return;
            }

            if (string.Equals(
                    characterId,
                    SuskiDefinitionId,
                    StringComparison.Ordinal) &&
                !instance.GetFlag(SuskiRescuedFromJaniCrashFlagId) &&
                (storyTrafficState?.suski == null ||
                 IsSuskiInsideJaniVehicle(
                     storyTrafficState.suski.stage)))
            {
                RemovePresentation(characterId);
                return;
            }

            bool suskiBodyMustRemainMaterialized = string.Equals(
                    characterId,
                    SuskiDefinitionId,
                    StringComparison.Ordinal) &&
                storyTrafficState?.suski != null &&
                IsSuskiLooseRescueBody(storyTrafficState.suski.stage);
            bool storyTrafficUsesPersistentPhysics =
                ShouldKeepStoryTrafficPhysical(instance);
            if (storyTrafficUsesPersistentPhysics)
            {
                knownStoryTrafficResidency.Add(characterId);
                residentStoryTrafficCharacters.Add(characterId);
            }

            bool presentationCellLoaded =
                effectiveCellLoaded ||
                IsPhysicalStoryTrafficCellLoaded(characterId) ||
                suskiBodyMustRemainMaterialized ||
                storyTrafficUsesPersistentPhysics &&
                productionStreaming == null;
            if (!NpcPresentationPolicy.ShouldMaterialize(
                    instance.ActivityState,
                    presentationCellLoaded))
            {
                RemovePresentation(characterId);
                return;
            }

            string presentationBindingId = ResolvePresentationBindingId(
                instance);

            bool newlyMaterialized = false;
            if (!presentations.TryGetValue(
                    characterId,
                    out LegacyCharacterPresentationBinding presentation) ||
                presentation == null ||
                !string.Equals(
                    presentation.BindingId,
                    presentationBindingId,
                    StringComparison.Ordinal))
            {
                RemovePresentation(characterId);
                CharacterPresentationCatalogEntry entry;
                if (!presentationCatalog.TryGet(
                        presentationBindingId,
                        out entry) ||
                    entry.WrapperPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"NPC presentation binding '{presentationBindingId}' is unavailable.");
                }

                GameObject wrapper = Instantiate(
                    entry.WrapperPrefab,
                    effectivePose.Position,
                    effectivePose.Rotation,
                    transform);
                wrapper.name = "NPC_Presentation_" + characterId;
                presentation = wrapper.GetComponent<
                    LegacyCharacterPresentationBinding>();
                if (presentation == null)
                {
                    DestroyPresentation(wrapper);
                    throw new InvalidOperationException(
                        $"NPC wrapper '{entry.BindingId}' lost its project-owned presenter.");
                }

                presentations[characterId] = presentation;
                newlyMaterialized = true;
                ConfigureDialogueInteraction(presentation, instance);
                ConfigureSpecializedPresentation(presentation, instance);
            }

            StoryTrafficVehiclePresentationBinding movingStoryTraffic =
                presentation.GetComponent<
                    StoryTrafficVehiclePresentationBinding>();
            if (movingStoryTraffic != null &&
                !string.IsNullOrEmpty(instance.CurrentRouteId))
            {
                bool physicalMotion =
                    movingStoryTraffic.HasPhysicalMotionBackend;
                if (physicalMotion)
                {
                    Simulation.SetPhysicalRouteAuthority(
                        characterId,
                        true,
                        gameTime.Snapshot.ElapsedGameSeconds);
                    physicalStoryTrafficRoutes[characterId] =
                        instance.CurrentRouteId;

                    // Initialize handling before the wrapper's first physics
                    // tick. Otherwise a Perajarvi spawn can briefly inherit
                    // the prefab's RoadRace defaults before FixedUpdate has
                    // supplied its first look-ahead sample.
                    StoryTrafficRoadBehaviorProfile initialProfile =
                        ResolveStoryTrafficRoadBehaviorProfile(
                            foundation,
                            instance.CurrentRouteId,
                            (float)instance.RouteProgress01);
                    movingStoryTraffic.SetRoadBehaviorProfile(
                        initialProfile,
                        initialProfile ==
                            StoryTrafficRoadBehaviorProfile.Perajarvi &&
                        IsInsideDonorPerajarviHandbrakeZone(
                            effectivePose.Position));
                }

                // A physical wrapper receives one placement pose when it is
                // created (or when its route/save state intentionally changes).
                // From then on NpcWorldRuntime.FixedUpdate supplies look-ahead
                // guidance and the Rigidbody is authoritative. Reapplying the
                // logical pose on every game-clock publication races the
                // presentation FixedUpdate and can reduce desired speed to
                // zero while the driven wheels continue spinning.
                bool forcedPlacement =
                    forcePhysicalPlacement.Remove(characterId);
                if (!physicalMotion || newlyMaterialized || forcedPlacement)
                {
                    movingStoryTraffic.SnapToRoutePoseTarget(
                        effectivePose.Position,
                        effectivePose.Rotation);
                }
                if (newlyMaterialized &&
                    storyTrafficMotionStates.TryGetValue(
                        characterId,
                        out StoryTrafficMotionRuntimeState retainedState))
                {
                    movingStoryTraffic.RestoreRuntimeState(in retainedState);
                    storyTrafficMotionStates.Remove(characterId);
                }
            }
            else
            {
                if (movingStoryTraffic != null)
                {
                    physicalStoryTrafficRoutes.Remove(characterId);
                    Simulation.SetPhysicalRouteAuthority(
                        characterId,
                        false,
                        gameTime.Snapshot.ElapsedGameSeconds);
                }
                bool suskiRagdollOwnsPose = string.Equals(
                        characterId,
                        SuskiDefinitionId,
                    StringComparison.Ordinal) &&
                    storyTrafficState?.suski != null &&
                    IsSuskiLooseRescueBody(storyTrafficState.suski.stage);
                if (newlyMaterialized || !suskiRagdollOwnsPose)
                {
                    navigation.ApplyPose(
                        presentation.transform,
                        effectivePose);
                }
            }
            presentation.ApplyState(instance.ActivityState);
            if (string.Equals(
                    characterId,
                    SuskiDefinitionId,
                    StringComparison.Ordinal))
            {
                ConfigureSuskiRescuePresentation(presentation, instance);
            }
            presentation.GetComponent<CharacterFlagPresentationBinding>()?
                .Apply(instance);
            ReconcileStoryTrafficPassengers(presentation);
        }

        private bool IsPhysicalStoryTrafficCellLoaded(string characterId) =>
            TryGetPhysicalStoryTrafficCell(
                characterId,
                out string physicalCellId) &&
            cellAvailability.IsLoaded(physicalCellId);

        private void RetainStoryTrafficCell(
            string characterId,
            string role,
            Vector3 worldPosition)
        {
            if (productionStreaming == null ||
                !productionStreaming.TryGetCellIdForPosition(
                    worldPosition,
                    out string cellId))
            {
                return;
            }

            string ownerId = "npc.story-traffic:" + characterId + ":" + role;
            productionStreaming.RetainCell(ownerId, cellId);
            storyTrafficCellRetentionOwners.Add(ownerId);
        }

        private void ReleaseStoryTrafficCellRetentions(string characterId)
        {
            ReleaseStoryTrafficCellRetention(
                "npc.story-traffic:" + characterId + ":current");
            ReleaseStoryTrafficCellRetention(
                "npc.story-traffic:" + characterId + ":ahead");
        }

        private void ReleaseStoryTrafficCellRetention(string ownerId)
        {
            if (productionStreaming == null)
            {
                return;
            }

            productionStreaming.ReleaseCellRetention(ownerId);
            storyTrafficCellRetentionOwners.Remove(ownerId);
        }

        private void ReleaseAllStoryTrafficCellRetentions()
        {
            if (productionStreaming != null)
            {
                foreach (string ownerId in storyTrafficCellRetentionOwners)
                {
                    productionStreaming.ReleaseCellRetention(ownerId);
                }
            }

            storyTrafficCellRetentionOwners.Clear();
        }

        private bool TryGetPhysicalStoryTrafficCell(
            string characterId,
            out string cellId)
        {
            cellId = string.Empty;
            if (!presentations.TryGetValue(
                    characterId,
                    out LegacyCharacterPresentationBinding presentation) ||
                presentation == null ||
                presentation.GetComponent<
                    StoryTrafficVehiclePresentationBinding>() is not
                    StoryTrafficVehiclePresentationBinding traffic)
            {
                return false;
            }

            return cellAvailability.TryResolveCellId(
                traffic.PhysicalWorldPosition,
                out cellId);
        }

        private static void ResolveAuthoritativePresentationPose(
            LegacyCharacterPresentationBinding presentation,
            out Vector3 position,
            out Quaternion rotation)
        {
            StoryTrafficVehiclePresentationBinding traffic =
                presentation.GetComponent<
                    StoryTrafficVehiclePresentationBinding>();
            position = traffic != null
                ? traffic.PhysicalWorldPosition
                : presentation.transform.position;
            rotation = traffic != null
                ? traffic.PhysicalWorldRotation
                : presentation.transform.rotation;
        }

        private void ReconcileStoryTrafficPassengers(
            LegacyCharacterPresentationBinding presentation)
        {
            StoryTrafficVehiclePresentationBinding storyTraffic =
                presentation != null
                    ? presentation.GetComponent<
                        StoryTrafficVehiclePresentationBinding>()
                    : null;
            if (storyTraffic == null ||
                !Simulation.TryGetInstance(
                    SuskiDefinitionId,
                    out CharacterInstance suski))
            {
                return;
            }

            storyTraffic.SetPassengerVisible(
                "P1.NPC.006",
                storyTrafficState?.suski == null
                    ? !suski.GetFlag(
                        SuskiRescuedFromJaniCrashFlagId)
                    : IsSuskiInsideJaniVehicle(
                        storyTrafficState.suski.stage));
            ConfigureStoryTrafficInteractionHost(presentation);
        }

        private void ReconcileStoryTrafficAudioOwner(
            CharacterInstance instance,
            in NpcPose pose)
        {
            if (instance == null ||
                !IsStoryTrafficCharacter(
                    instance.Definition.DefinitionId) ||
                vehicleAudioBackendComponent == null)
            {
                return;
            }

            bool activeDriving =
                instance.ActivityState != CharacterActivityState.Hidden &&
                instance.ActivityState != CharacterActivityState.Disabled &&
                !string.IsNullOrEmpty(instance.CurrentRouteId);
            if (!activeDriving &&
                !storyTrafficAudioOwners.ContainsKey(
                    instance.Definition.DefinitionId))
            {
                return;
            }

            StoryTrafficVehicleAudioPresenter audio =
                GetOrCreateStoryTrafficAudioOwner(
                    instance.Definition.DefinitionId);
            audio.ConfigurePersistent(
                vehicleAudioBackendComponent,
                instance.Definition.DefinitionId,
                player);
            bool terminal = TryGetStoryTrafficDriverState(
                    instance.Definition.DefinitionId,
                    out StoryTrafficDriverStateDto driverState) &&
                (driverState.terminalCrash ||
                 driverState.routeFullStopReached);
            // Apply the terminal gate before the active logical pose. A fresh
            // audio owner must never post engine/music loops for one frame while
            // a saved terminal wreck is being restored.
            audio.SetTerminallyDisabled(terminal);
            audio.SetLogicalPose(
                pose.Position,
                pose.Rotation,
                activeDriving);
        }

        private StoryTrafficVehicleAudioPresenter
            GetOrCreateStoryTrafficAudioOwner(string characterId)
        {
            if (storyTrafficAudioOwners.TryGetValue(
                    characterId,
                    out StoryTrafficVehicleAudioPresenter existing) &&
                existing != null)
            {
                return existing;
            }

            storyTrafficAudioOwners.Remove(characterId);
            var owner = new GameObject(
                "NPC_StoryTrafficAudio_" + characterId);
            owner.transform.SetParent(transform, false);
            StoryTrafficVehicleAudioPresenter presenter =
                owner.AddComponent<StoryTrafficVehicleAudioPresenter>();
            storyTrafficAudioOwners.Add(characterId, presenter);
            return presenter;
        }

        private void DestroyStoryTrafficAudioOwners()
        {
            foreach (StoryTrafficVehicleAudioPresenter audio in
                     storyTrafficAudioOwners.Values.ToArray())
            {
                DestroyPresentation(audio);
            }

            storyTrafficAudioOwners.Clear();
        }

        private static bool IsStoryTrafficCharacter(string characterId) =>
            string.Equals(
                characterId,
                "character.jani",
                StringComparison.Ordinal) ||
            string.Equals(
                characterId,
                "character.petteri",
                StringComparison.Ordinal);

        private void ConfigureSpecializedPresentation(
            LegacyCharacterPresentationBinding presentation,
            CharacterInstance instance)
        {
            TeimoBicyclePresentationBinding bicycle = presentation != null
                ? presentation.GetComponent<TeimoBicyclePresentationBinding>()
                : null;
            if (bicycle != null)
            {
                bicycle.ConfigureRuntime(instance, player, gameTime);
            }

            StoryTrafficVehiclePresentationBinding storyTraffic =
                presentation != null
                    ? presentation.GetComponent<
                        StoryTrafficVehiclePresentationBinding>()
                    : null;
            if (storyTraffic != null)
            {
                storyTraffic.CollisionIncident -=
                    HandleStoryTrafficCollision;
                storyTraffic.CollisionIncident +=
                    HandleStoryTrafficCollision;
                storyTraffic.TerminalCrash -=
                    HandleStoryTrafficTerminalCrash;
                storyTraffic.TerminalCrash +=
                    HandleStoryTrafficTerminalCrash;
                if (TryGetStoryTrafficDriverState(
                        instance.Definition.DefinitionId,
                        out StoryTrafficDriverStateDto currentDriverState))
                {
                    storyTraffic.SetStoryIncidentHold(
                        currentDriverState.terminalCrash);
                    storyTraffic.SetRouteFullStopHold(
                        currentDriverState.routeFullStopReached);
                    if (currentDriverState.teimoSocialStopConsumed &&
                        currentDriverState
                            .teimoSocialStopSecondsRemaining > 0f)
                    {
                        storyTraffic.BeginTeimoSocialStop(
                            currentDriverState
                                .teimoSocialStopSecondsRemaining);
                    }
                }

                if (!storyTraffic.IsStoryIncidentHeld &&
                    player != null && string.Equals(
                        instance.Definition.DefinitionId,
                        "character.jani",
                        StringComparison.Ordinal))
                {
                    storyTraffic.ConfigureStructuralFailureSensor(player);
                }

                if (string.Equals(
                        instance.Definition.DefinitionId,
                        "character.jani",
                        StringComparison.Ordinal))
                {
                    SuskiCrashExtractionInteractionTarget extraction =
                        GetOrAddComponent<
                            SuskiCrashExtractionInteractionTarget>(
                            presentation.gameObject);
                    extraction.Configure(
                        CanExtractSuskiFromCrashedCar,
                        TryExtractSuskiFromCrashedCar);
                    ConfigureStoryTrafficInteractionHost(presentation);
                }
            }
            storyTraffic?.ConfigureDrivingProfile(
                configuredMinimumCruiseSpeedMetersPerSecond: 115f / 3.6f,
                configuredMaximumSpeedMetersPerSecond: 185f / 3.6f,
                configuredAccelerationMetersPerSecond2: 7.5f,
                configuredBrakingMetersPerSecond2: 20f,
                configuredTurnRateDegreesPerSecond: 150f,
                configuredPassingLaneOffsetMeters: -3.2f);
            // Locked donor Navigation uses LanePosition=+2 and writes that
            // value directly to Target.localPosition.x for both Jani and
            // Petteri. The generated wrappers defaulted to zero, leaving the
            // physical controller to chase the route centre/shoulder.
            storyTraffic?.ConfigureRoadLanePolicy(
                configuredBaseLaneOffsetMeters: 2f,
                configuredPassingLaneOffsetMeters: -2f,
                migrateLegacyCenterLane: true);
            if (storyTraffic != null &&
                vehicleAudioBackendComponent != null)
            {
                StoryTrafficVehicleAudioPresenter audio =
                    GetOrCreateStoryTrafficAudioOwner(
                        instance.Definition.DefinitionId);
                audio.ConfigurePersistent(
                    vehicleAudioBackendComponent,
                    instance.Definition.DefinitionId,
                    player);
                audio.BindMotion(storyTraffic);
            }

            if (storyTraffic != null &&
                TryGetStoryTrafficDriverState(
                    instance.Definition.DefinitionId,
                    out StoryTrafficDriverStateDto driverState))
            {
                if (storyTrafficAudioOwners.TryGetValue(
                        instance.Definition.DefinitionId,
                        out StoryTrafficVehicleAudioPresenter audio) &&
                    audio != null)
                {
                    audio.SetTerminallyDisabled(
                        driverState.terminalCrash ||
                        driverState.routeFullStopReached);
                }
            }

        }

        private string ResolvePresentationBindingId(
            CharacterInstance instance)
        {
            if (instance != null && string.Equals(
                    instance.Definition.DefinitionId,
                    SuskiDefinitionId,
                    StringComparison.Ordinal) &&
                storyTrafficState?.suski != null &&
                IsSuskiLooseRescueBody(storyTrafficState.suski.stage))
            {
                return SuskiRescuePresentationBindingId;
            }

            if (instance != null &&
                !string.IsNullOrEmpty(instance.ActiveScheduleBlockId) &&
                foundation.TryGetScheduleBlock(
                    instance.ActiveScheduleBlockId,
                    out NpcScheduleBlock block) &&
                !string.IsNullOrEmpty(
                    block.PresentationBindingIdOverride))
            {
                return block.PresentationBindingIdOverride;
            }

            return instance?.Definition.PresentationBindingId ?? string.Empty;
        }

        private void RemovePresentation(string characterId)
        {
            if (!presentations.TryGetValue(characterId, out var presentation))
            {
                return;
            }

            StoryTrafficVehiclePresentationBinding storyTraffic =
                presentation != null
                    ? presentation.GetComponent<
                        StoryTrafficVehiclePresentationBinding>()
                    : null;
            if (storyTraffic != null)
            {
                storyTraffic.CollisionIncident -=
                    HandleStoryTrafficCollision;
                storyTraffic.TerminalCrash -=
                    HandleStoryTrafficTerminalCrash;
                physicalStoryTrafficRoutes.Remove(characterId);
                if (Simulation != null)
                {
                    Simulation.SetPhysicalRouteAuthority(
                        characterId,
                        false,
                        gameTime != null
                            ? gameTime.Snapshot.ElapsedGameSeconds
                            : 0d);
                }
                storyTrafficMotionStates[characterId] =
                    storyTraffic.CaptureRuntimeState();
                if (storyTrafficAudioOwners.TryGetValue(
                        characterId,
                        out StoryTrafficVehicleAudioPresenter audio) &&
                    audio != null)
                {
                    audio.UnbindMotion(storyTraffic);
                }
            }

            if (string.Equals(
                    characterId,
                    SuskiDefinitionId,
                    StringComparison.Ordinal) &&
                storyTrafficState?.suski != null &&
                presentation != null)
            {
                SuskiRescueRagdoll ragdoll = presentation.GetComponent<
                    SuskiRescueRagdoll>();
                if (ragdoll != null && ragdoll.TryGetPrimaryPose(
                        out Vector3 position,
                        out Quaternion rotation))
                {
                    storyTrafficState.suski.worldPosition = position;
                    storyTrafficState.suski.worldRotation = rotation;
                }
            }

            presentations.Remove(characterId);
            DestroyPresentation(presentation);
        }

        private void ConfigureDialogueInteraction(
            LegacyCharacterPresentationBinding presentation,
            CharacterInstance instance)
        {
            if (presentation == null || instance == null ||
                dialogueCatalog == null || dialogueRuntime == null ||
                !dialogueCatalog.TryGetForCharacter(
                    instance.Definition.DefinitionId,
                    out NpcDialogueDefinition definition))
            {
                return;
            }

            StoryTrafficVehiclePresentationBinding storyTraffic =
                presentation.GetComponent<
                    StoryTrafficVehiclePresentationBinding>();
            if (storyTraffic == null)
            {
                CapsuleCollider collider = GetOrAddComponent<CapsuleCollider>(
                    presentation.gameObject);
                collider.isTrigger = false;
                collider.center = new Vector3(0f, 0.9f, 0f);
                collider.height = 1.8f;
                collider.radius = 0.34f;
            }
            else if (presentation.GetComponent<Collider>() == null)
            {
                throw new InvalidOperationException(
                    $"Story-traffic presentation for " +
                    $"'{instance.Definition.DefinitionId}' requires its " +
                    "authored chassis collider for dialogue interaction.");
            }

            NpcDialogueInteractionTarget target =
                GetOrAddComponent<NpcDialogueInteractionTarget>(
                    presentation.gameObject);
            target.Configure(
                instance,
                definition,
                dialogueRuntime,
                () => gameTime.Snapshot.ElapsedGameSeconds,
                dialogueFeedback);
            ConfigureStoryTrafficInteractionHost(presentation);
        }

        private void ConfigureStoryTrafficInteractionHost(
            LegacyCharacterPresentationBinding presentation)
        {
            if (presentation == null)
            {
                return;
            }

            InteractionTargetHost host =
                presentation.GetComponent<InteractionTargetHost>();
            SuskiCrashExtractionInteractionTarget extraction =
                presentation.GetComponent<
                    SuskiCrashExtractionInteractionTarget>();
            NpcDialogueInteractionTarget dialogue =
                presentation.GetComponent<NpcDialogueInteractionTarget>();
            if (host == null && extraction == null && dialogue == null)
            {
                return;
            }

            host ??= GetOrAddComponent<InteractionTargetHost>(
                presentation.gameObject);
            if (extraction != null && extraction.IsAvailable)
            {
                // Both dialogue and extraction implement the same capability.
                // Expose exactly one authoritative action: a terminal wreck is
                // for rescue, while an ordinary live car keeps its dialogue.
                host.Configure(extraction);
            }
            else if (dialogue != null)
            {
                host.Configure(dialogue);
            }
            else
            {
                host.Configure();
            }
        }

        private static TComponent GetOrAddComponent<TComponent>(
            GameObject owner)
            where TComponent : Component
        {
            TComponent component = owner.GetComponent<TComponent>();
            if (component == null)
            {
                component = owner.AddComponent<TComponent>();
            }

            return component;
        }

        private static void DestroyPresentation(Component presentation)
        {
            if (presentation != null)
            {
                DestroyPresentation(presentation.gameObject);
            }
        }

        private static void DestroyPresentation(GameObject presentation)
        {
            if (presentation == null)
            {
                return;
            }

            presentation.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(presentation);
            }
            else
            {
                DestroyImmediate(presentation);
            }
        }

        private void RequireInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException(
                    "NPC world runtime has not been initialized.");
            }
        }
    }

    /// <summary>
    /// Explicit player action which releases the incapacitated passenger from
    /// Jani's terminal wreck. The story runtime owns the state transition; this
    /// component is only the interaction adapter on the streamed car wrapper.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuskiCrashExtractionInteractionTarget : MonoBehaviour,
        IContextInteractionTarget
    {
        private Func<bool> canExtract;
        private Func<bool> tryExtract;

        public string InteractionPrompt =>
            "\u0412\u042b\u0422\u0410\u0429\u0418\u0422\u042c \u0421\u0423\u0421\u041a\u0418";

        public bool IsAvailable => canExtract?.Invoke() == true;

        public void Configure(
            Func<bool> configuredCanExtract,
            Func<bool> configuredTryExtract)
        {
            canExtract = configuredCanExtract ??
                throw new ArgumentNullException(nameof(configuredCanExtract));
            tryExtract = configuredTryExtract ??
                throw new ArgumentNullException(nameof(configuredTryExtract));
        }

        public bool CanInteract(in InteractionContext context) => IsAvailable;

        public void Interact(in InteractionContext context)
        {
            if (IsAvailable)
            {
                tryExtract();
            }
        }
    }
}
