using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class VehicleAssemblyController : MonoBehaviour
    {
        [SerializeField]
        private PartInstance[] parts = Array.Empty<PartInstance>();

        [SerializeField]
        private MountPointAuthoring[] mountPoints = Array.Empty<MountPointAuthoring>();

        [SerializeField]
        private AssemblyDependency[] dependencies = Array.Empty<AssemblyDependency>();

        [SerializeField]
        private ToolDefinition[] tools = Array.Empty<ToolDefinition>();

        [SerializeField]
        private Transform loosePartsRoot;

        [SerializeField]
        private bool allowAdditiveSaveMigration;

        [SerializeField]
        private Rigidbody retentionSpeedSource;

        private AssemblyGraph graph;
        private VehicleAssemblyQuery query;
        private bool initialized;
        private int operationCount;
        private int candidateQueryCount;
        private int graphMutationCount;
        private readonly Dictionary<PartInstance, InstallTransitionState>
            installTransitions = new Dictionary<PartInstance, InstallTransitionState>();
        private readonly HashSet<MountPointAuthoring> reservedInstallMounts =
            new HashSet<MountPointAuthoring>();
        private readonly HashSet<PartInstance> synchronizedParts =
            new HashSet<PartInstance>();
        private readonly HashSet<PartInstance> synchronizingParts =
            new HashSet<PartInstance>();
        private uint retentionPolicyTick;

        public const float InstallTransitionDurationSeconds = 0.17f;

        public AssemblyGraph Graph
        {
            get
            {
                EnsureInitialized();
                return graph;
            }
        }

        public VehicleAssemblyQuery Query
        {
            get
            {
                EnsureInitialized();
                return query;
            }
        }

        public PartInstance[] Parts => parts;

        public MountPointAuthoring[] MountPoints => mountPoints;

        public AssemblyDependency[] Dependencies => dependencies;

        public ToolDefinition[] Tools => tools;

        public int OperationCount => operationCount;

        public int CandidateQueryCount => candidateQueryCount;

        public int GraphMutationCount => graphMutationCount;

        public bool AllowsAdditiveSaveMigration => allowAdditiveSaveMigration;

        public AssemblyOperationResult LastOperationResult { get; private set; }

        public event Action<AssemblyActionCompleted> ActionCompleted;

        public void Configure(
            PartInstance[] registeredParts,
            MountPointAuthoring[] registeredMounts,
            AssemblyDependency[] registeredDependencies,
            ToolDefinition[] registeredTools,
            Transform detachedPartsRoot,
            bool permitAdditiveSaveMigration = false,
            Rigidbody configuredRetentionSpeedSource = null)
        {
            parts = registeredParts ?? Array.Empty<PartInstance>();
            mountPoints = registeredMounts ?? Array.Empty<MountPointAuthoring>();
            dependencies = registeredDependencies ?? Array.Empty<AssemblyDependency>();
            tools = registeredTools ?? Array.Empty<ToolDefinition>();
            loosePartsRoot = detachedPartsRoot;
            allowAdditiveSaveMigration = permitAdditiveSaveMigration;
            retentionSpeedSource = configuredRetentionSpeedSource != null
                ? configuredRetentionSpeedSource
                : GetComponent<Rigidbody>();
            retentionPolicyTick = 0u;
            initialized = false;
            EnsureInitialized();
        }

        public void Initialize()
        {
            EnsureInitialized();
        }

        public PartInstance ResolvePart(IPickupTarget pickupTarget)
        {
            if (pickupTarget == null || pickupTarget.Body == null)
            {
                return null;
            }

            return pickupTarget.Body.GetComponentInParent<PartInstance>();
        }

        public MountPointRuntime ResolveMount(MountPointAuthoring authoring)
        {
            EnsureInitialized();
            if (authoring == null)
            {
                return null;
            }

            MountPointRuntime[] mounts = graph.Mounts;
            for (int i = 0; i < mounts.Length; i++)
            {
                if (mounts[i] != null && mounts[i].Authoring == authoring)
                {
                    return mounts[i];
                }
            }

            return null;
        }

        public AssemblyMountCandidate FindBestMount(PartInstance part, bool includeInvalid = false)
        {
            EnsureInitialized();
            candidateQueryCount++;
            return part == null ? default : query.FindBestMount(part, includeInvalid);
        }

        public AssemblyOperationResult EvaluateInstall(
            PartInstance part,
            MountPointAuthoring authoring)
        {
            EnsureInitialized();
            return query.EvaluateInstall(part, ResolveMount(authoring));
        }

        public AssemblyOperationResult EvaluateHandoffInstall(
            PartInstance part,
            MountPointAuthoring authoring)
        {
            EnsureInitialized();
            if (part != null && installTransitions.ContainsKey(part))
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.InvalidPart,
                    "Деталь уже устанавливается.");
            }

            if (authoring != null && reservedInstallMounts.Contains(authoring))
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.MountOccupied,
                    "Точка установки уже занята перемещаемой деталью.");
            }

            return query.EvaluateHandoffInstall(part, ResolveMount(authoring));
        }

        public AssemblyOperationResult TryInstall(
            PartInstance part,
            MountPointAuthoring authoring)
        {
            EnsureInitialized();
            operationCount++;
            MountPointRuntime mount = ResolveMount(authoring);
            AssemblyOperationResult evaluation = query.EvaluateInstall(part, mount);
            if (!evaluation.Succeeded)
            {
                LastOperationResult = evaluation;
                return evaluation;
            }

            if (!mount.TryOccupy(part))
            {
                LastOperationResult = AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.MountOccupied,
                    "Точка установки занята.");
                return LastOperationResult;
            }

            try
            {
                Rigidbody installedBody = part.Body;
                Vector3 sourceLinearVelocity = installedBody != null
                    ? installedBody.linearVelocity
                    : Vector3.zero;
                Vector3 sourceAngularVelocity = installedBody != null
                    ? installedBody.angularVelocity
                    : Vector3.zero;
                if (installTransitions.TryGetValue(
                        part,
                        out InstallTransitionState activeTransition))
                {
                    sourceLinearVelocity =
                        activeTransition.StartLinearVelocity;
                    sourceAngularVelocity =
                        activeTransition.StartAngularVelocity;
                }

                part.InstallAt(mount.Authoring, mount.MountId);
                RefreshOwnedMountAvailability();
                SynchronizeInstalledParts();
                graphMutationCount++;
                PublishAction(
                    AssemblyActionKind.PartInstalled,
                    part,
                    mount,
                    string.Empty,
                    mount.Authoring.Pose.position,
                    part.Definition != null
                        ? part.Definition.MassKilograms
                        : installedBody != null
                            ? installedBody.mass
                            : 0f,
                    sourceLinearVelocity,
                    sourceAngularVelocity);
                int collapsedPartCount = CollapseIfStructurallyUnsupported(
                    mount);
                LastOperationResult = AssemblyOperationResult.Success(
                    AssemblyOperation.Install,
                    collapsedPartCount > 0
                        ? "Деталь установлена, но незатянутая опора не удержала конструкцию."
                        : "Деталь установлена без затяжки: " +
                          part.Definition.DisplayName);
                return LastOperationResult;
            }
            catch
            {
                mount.Release();
                throw;
            }
        }

        public AssemblyOperationResult TryInstall(
            IPickupTarget pickupTarget,
            MountPointAuthoring authoring)
        {
            return TryInstall(ResolvePart(pickupTarget), authoring);
        }

        public AssemblyOperationResult TryInstallFromHandoff(
            IPickupTarget pickupTarget,
            MountPointAuthoring authoring)
        {
            PartInstance part = ResolvePart(pickupTarget);
            AssemblyOperationResult evaluation = EvaluateHandoffInstall(
                part,
                authoring);
            if (!evaluation.Succeeded || part == null || authoring == null)
            {
                LastOperationResult = evaluation;
                return evaluation;
            }

            Vector3 originalPosition = part.transform.position;
            Quaternion originalRotation = part.transform.rotation;
            part.transform.SetPositionAndRotation(
                authoring.Pose.position,
                authoring.Pose.rotation);
            if (part.Body != null)
            {
                part.Body.position = authoring.Pose.position;
                part.Body.rotation = authoring.Pose.rotation;
            }

            AssemblyOperationResult result = TryInstall(part, authoring);
            if (!result.Succeeded)
            {
                part.transform.SetPositionAndRotation(
                    originalPosition,
                    originalRotation);
                if (part.Body != null)
                {
                    part.Body.position = originalPosition;
                    part.Body.rotation = originalRotation;
                }
            }

            return result;
        }

        /// <summary>
        /// Starts the player-facing mount handoff. The donor mount pose and
        /// assembly rules remain authoritative; only the presentation between
        /// the released carry pose and that exact pose is interpolated.
        /// EditMode callers keep the immediate path so deterministic assembly
        /// tests do not depend on a player loop.
        /// </summary>
        public AssemblyOperationResult BeginInstallFromHandoff(
            IPickupTarget pickupTarget,
            MountPointAuthoring authoring)
        {
            PartInstance part = ResolvePart(pickupTarget);
            AssemblyOperationResult evaluation = EvaluateHandoffInstall(
                part,
                authoring);
            if (!evaluation.Succeeded || part == null || authoring == null)
            {
                LastOperationResult = evaluation;
                return evaluation;
            }

            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return TryInstallFromHandoff(pickupTarget, authoring);
            }

            var transition = InstallTransitionState.Capture(part);
            installTransitions.Add(part, transition);
            reservedInstallMounts.Add(authoring);
            transition.Coroutine = StartCoroutine(
                RunInstallTransition(part, authoring, transition));
            LastOperationResult = AssemblyOperationResult.Success(
                AssemblyOperation.Install,
                "Деталь перемещается в точную точку установки: " +
                part.Definition.DisplayName);
            return LastOperationResult;
        }

        private IEnumerator RunInstallTransition(
            PartInstance part,
            MountPointAuthoring authoring,
            InstallTransitionState transition)
        {
            Rigidbody body = part.Body;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
                body.detectCollisions = false;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.isKinematic = true;
            }

            transition.BeginPresentation(authoring);

            float elapsed = 0f;
            while (elapsed < InstallTransitionDurationSeconds &&
                   part != null && authoring != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(
                    elapsed / InstallTransitionDurationSeconds);
                float eased = normalized * normalized *
                              (3f - 2f * normalized);
                Vector3 position = Vector3.LerpUnclamped(
                    transition.StartPosition,
                    authoring.Pose.position,
                    eased);
                Quaternion rotation = Quaternion.SlerpUnclamped(
                    transition.StartRotation,
                    authoring.Pose.rotation,
                    eased);
                part.transform.SetPositionAndRotation(position, rotation);
                if (body != null)
                {
                    body.position = position;
                    body.rotation = rotation;
                }

                transition.ApplyPresentation(eased);

                yield return null;
            }

            if (part == null || authoring == null)
            {
                FinishInstallTransition(
                    part,
                    authoring,
                    transition,
                    operationSucceeded: false,
                    remainsInstalled: false);
                yield break;
            }

            part.transform.SetPositionAndRotation(
                authoring.Pose.position,
                authoring.Pose.rotation);
            if (body != null)
            {
                body.position = authoring.Pose.position;
                body.rotation = authoring.Pose.rotation;
            }

            transition.ApplyPresentation(1f);

            AssemblyOperationResult result = TryInstall(part, authoring);
            FinishInstallTransition(
                part,
                authoring,
                transition,
                operationSucceeded: result.Succeeded,
                remainsInstalled: result.Succeeded && part.IsInstalled);
        }

        private void FinishInstallTransition(
            PartInstance part,
            MountPointAuthoring authoring,
            InstallTransitionState transition,
            bool operationSucceeded,
            bool remainsInstalled)
        {
            if (part != null)
            {
                installTransitions.Remove(part);
            }

            if (authoring != null)
            {
                reservedInstallMounts.Remove(authoring);
            }

            if (!operationSucceeded && part != null)
            {
                transition.Restore(part);
            }

            transition.CompletePresentation(remainsInstalled);
        }

        private void OnDisable()
        {
            if (installTransitions.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<PartInstance, InstallTransitionState> pair in
                     installTransitions)
            {
                if (pair.Value.Coroutine != null)
                {
                    StopCoroutine(pair.Value.Coroutine);
                }

                if (pair.Key != null)
                {
                    pair.Value.Restore(pair.Key);
                }
            }

            installTransitions.Clear();
            reservedInstallMounts.Clear();
        }

        private void FixedUpdate()
        {
            SynchronizeInstalledParts();
            EvaluateFastenerRetentionPolicies();
        }

        private void EvaluateFastenerRetentionPolicies()
        {
            if (!initialized || retentionSpeedSource == null || graph == null)
            {
                return;
            }

            float speedKph = retentionSpeedSource.linearVelocity.magnitude *
                3.6f;
            uint tick = ++retentionPolicyTick;
            MountPointRuntime[] mounts = graph.Mounts;
            for (int index = 0; index < mounts.Length; index++)
            {
                MountPointRuntime mount = mounts[index];
                if (mount == null || !mount.IsOccupied ||
                    mount.FastenerGroup.Definition.SpeedRetentionPolicy ==
                    FastenerSpeedRetentionPolicy.None)
                {
                    continue;
                }

                uint seed = CombineRetentionSeed(
                    StableHash32(mount.MountId),
                    tick);
                float sample01 = FastenerGroupDefinition.Sample01(seed);
                if (mount.FastenerGroup.ShouldBreak(speedKph, sample01))
                {
                    // Snapshot iteration is safe: BREAK mutates occupancy but
                    // not the authored mount array. Only explicitly authored
                    // donor policies ever reach this detach path.
                    TryApplyFastenerBreakPolicy(mount.MountId, speedKph, seed);
                }
            }
        }

        private static uint StableHash32(string value)
        {
            uint hash = 2166136261u;
            string source = value ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                hash ^= source[index];
                hash *= 16777619u;
            }

            return hash;
        }

        private static uint CombineRetentionSeed(uint mountHash, uint tick)
        {
            uint value = mountHash ^ (tick + 0x9e3779b9u +
                (mountHash << 6) + (mountHash >> 2));
            return value == 0u ? 0x6d2b79f5u : value;
        }

        private void LateUpdate()
        {
            // PhysX writes the chassis Transform after FixedUpdate. A second
            // pass keeps presentation exact in the rendered frame.
            SynchronizeInstalledParts();
        }

        public void SynchronizeInstalledParts()
        {
            if (!initialized || parts == null)
            {
                return;
            }

            synchronizedParts.Clear();
            synchronizingParts.Clear();
            for (int index = 0; index < parts.Length; index++)
            {
                SynchronizeInstalledPart(parts[index]);
            }
        }

        internal void NotifyInstalledPhysicsStateChanged()
        {
            // Installed mass ownership changed between the chassis compound
            // and a jointed child body. Consumers already key their refresh on
            // this counter, so keep the handoff inside the existing contract.
            graphMutationCount++;
        }

        private void SynchronizeInstalledPart(PartInstance part)
        {
            if (part == null || synchronizedParts.Contains(part) ||
                !part.IsInstalled || part.IsAssemblyRoot)
            {
                return;
            }

            if (!synchronizingParts.Add(part))
            {
                // AssemblyGraph validation rejects dependency cycles. Keep
                // this runtime guard fail-closed for malformed authored data.
                return;
            }

            Transform pose = part.InstalledPose;
            PartInstance owner = pose != null
                ? pose.GetComponentInParent<PartInstance>()
                : null;
            if (owner != null && owner != part)
            {
                SynchronizeInstalledPart(owner);
            }

            part.SynchronizeInstalledPose();
            synchronizingParts.Remove(part);
            synchronizedParts.Add(part);
        }

        private sealed class InstallTransitionState
        {
            private InstallTransitionState(
                Vector3 startPosition,
                Quaternion startRotation,
                bool useGravity,
                bool isKinematic,
                bool detectCollisions,
                CollisionDetectionMode collisionDetectionMode,
                RigidbodyInterpolation interpolation)
            {
                StartPosition = startPosition;
                StartRotation = startRotation;
                UseGravity = useGravity;
                IsKinematic = isKinematic;
                DetectCollisions = detectCollisions;
                CollisionDetectionMode = collisionDetectionMode;
                Interpolation = interpolation;
                StartLinearVelocity = Vector3.zero;
                StartAngularVelocity = Vector3.zero;
            }

            public Vector3 StartPosition { get; }
            public Quaternion StartRotation { get; }
            public Vector3 StartLinearVelocity { get; private set; }
            public Vector3 StartAngularVelocity { get; private set; }
            private bool UseGravity { get; }
            private bool IsKinematic { get; }
            private bool DetectCollisions { get; }
            private CollisionDetectionMode CollisionDetectionMode { get; }
            private RigidbodyInterpolation Interpolation { get; }
            private IAssemblyInstallTransitionPresentation[] presentations =
                Array.Empty<IAssemblyInstallTransitionPresentation>();
            private bool presentationCompleted;
            public Coroutine Coroutine { get; set; }

            public static InstallTransitionState Capture(PartInstance part)
            {
                Rigidbody body = part.Body;
                var result = new InstallTransitionState(
                    part.transform.position,
                    part.transform.rotation,
                    body != null && body.useGravity,
                    body != null && body.isKinematic,
                    body == null || body.detectCollisions,
                    body != null
                        ? body.collisionDetectionMode
                        : CollisionDetectionMode.Discrete,
                    body != null
                        ? body.interpolation
                        : RigidbodyInterpolation.None);
                MonoBehaviour[] behaviours = part.GetComponents<MonoBehaviour>();
                var transitionPresentations = new List<
                    IAssemblyInstallTransitionPresentation>();
                for (int index = 0; index < behaviours.Length; index++)
                {
                    if (behaviours[index] is
                        IAssemblyInstallTransitionPresentation presentation)
                    {
                        transitionPresentations.Add(presentation);
                    }
                }

                result.presentations = transitionPresentations.ToArray();
                if (body != null)
                {
                    result.StartLinearVelocity = body.linearVelocity;
                    result.StartAngularVelocity = body.angularVelocity;
                }

                return result;
            }

            public void BeginPresentation(MountPointAuthoring mount)
            {
                for (int index = 0; index < presentations.Length; index++)
                {
                    presentations[index]?.BeginInstallTransition(mount);
                }
            }

            public void ApplyPresentation(float normalizedProgress)
            {
                for (int index = 0; index < presentations.Length; index++)
                {
                    presentations[index]?.ApplyInstallTransition(
                        normalizedProgress);
                }
            }

            public void CompletePresentation(bool installed)
            {
                if (presentationCompleted)
                {
                    return;
                }

                presentationCompleted = true;
                for (int index = 0; index < presentations.Length; index++)
                {
                    presentations[index]?.CompleteInstallTransition(installed);
                }
            }

            public void Restore(PartInstance part)
            {
                part.transform.SetPositionAndRotation(
                    StartPosition,
                    StartRotation);
                Rigidbody body = part.Body;
                if (body == null)
                {
                    CompletePresentation(false);
                    return;
                }

                body.position = StartPosition;
                body.rotation = StartRotation;
                body.isKinematic = IsKinematic;
                body.useGravity = UseGravity;
                body.detectCollisions = DetectCollisions;
                body.collisionDetectionMode = CollisionDetectionMode;
                body.interpolation = Interpolation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.WakeUp();
                }

                CompletePresentation(false);
            }
        }

        public AssemblyOperationResult TryOperateFastener(
            string mountId,
            string fastenerDefinitionId,
            ToolDefinition tool,
            bool tighten)
        {
            EnsureInitialized();
            operationCount++;
            AssemblyOperation operation = tighten
                ? AssemblyOperation.TightenFastener
                : AssemblyOperation.LoosenFastener;

            if (!graph.TryGetMount(mountId, out MountPointRuntime mount) || !mount.IsOccupied)
            {
                return SetFailure(operation, AssemblyFailureReason.InvalidMount, "На точке нет установленной детали.");
            }

            if (!mount.TryGetFastener(fastenerDefinitionId, out FastenerInstance fastener) ||
                fastener.Definition == null)
            {
                return SetFailure(operation, AssemblyFailureReason.InvalidFastener, "Крепёж не принадлежит этой точке.");
            }

            if (!fastener.IsInserted)
            {
                return SetFailure(operation, AssemblyFailureReason.InvalidFastener, "Крепёж не вставлен.");
            }

            if (fastener.Definition.ToolRule == null ||
                !fastener.Definition.ToolRule.Matches(tool))
            {
                return SetFailure(operation, AssemblyFailureReason.InvalidTool, "Нужен совместимый инструмент и размер.");
            }

            if (mount.Authoring.IsObstructed(mount.InstalledPart))
            {
                return SetFailure(operation, AssemblyFailureReason.Obstructed, "Доступ к крепежу перекрыт.");
            }

            if (!fastener.TryAdvance(tighten))
            {
                return SetFailure(operation, AssemblyFailureReason.FastenerAtLimit, "Крепёж уже на предельной стадии.");
            }

            mount.ReevaluateFastenerGroup();

            graphMutationCount++;
            LastOperationResult = AssemblyOperationResult.Success(
                operation,
                $"{fastener.Definition.DisplayName}: {fastener.Stage}/{fastener.Definition.MaximumStage}");
            PublishAction(
                tighten
                    ? AssemblyActionKind.FastenerTightened
                    : AssemblyActionKind.FastenerLoosened,
                mount.InstalledPart,
                mount,
                fastenerDefinitionId,
                mount.Authoring.Pose.position);
            return LastOperationResult;
        }

        public AssemblyOperationResult TryTurnFastener(
            string mountId,
            string fastenerDefinitionId,
            ToolDefinition tool,
            FastenerRotationDirection rotationDirection)
        {
            EnsureInitialized();
            if (!graph.TryGetMount(mountId, out MountPointRuntime mount) ||
                !mount.TryGetFastener(fastenerDefinitionId, out FastenerInstance fastener) ||
                fastener.Definition == null)
            {
                operationCount++;
                return SetFailure(
                    AssemblyOperation.TightenFastener,
                    AssemblyFailureReason.InvalidFastener,
                    "Крепёж не найден.");
            }

            bool clockwiseTightens =
                fastener.Definition.TighteningDirection == FastenerDirection.ClockwiseToTighten;
            bool tighten = rotationDirection == FastenerRotationDirection.Clockwise
                ? clockwiseTightens
                : !clockwiseTightens;
            return TryOperateFastener(mountId, fastenerDefinitionId, tool, tighten);
        }

        public AssemblyOperationResult TryInsertFastener(
            string mountId,
            string fastenerDefinitionId)
        {
            EnsureInitialized();
            operationCount++;
            if (!graph.TryGetMount(mountId, out MountPointRuntime mount) || !mount.IsOccupied)
            {
                return SetFailure(
                    AssemblyOperation.InsertFastener,
                    AssemblyFailureReason.InvalidMount,
                    "Нельзя вставить крепёж без установленной детали.");
            }

            if (!mount.TryGetFastener(fastenerDefinitionId, out FastenerInstance fastener) ||
                !fastener.TryInsert())
            {
                return SetFailure(
                    AssemblyOperation.InsertFastener,
                    AssemblyFailureReason.InvalidFastener,
                    "Крепёж неизвестен или уже вставлен.");
            }

            mount.ReevaluateFastenerGroup();

            graphMutationCount++;
            LastOperationResult = AssemblyOperationResult.Success(
                AssemblyOperation.InsertFastener,
                "Крепёж вставлен и ещё не посажен.");
            PublishAction(
                AssemblyActionKind.FastenerInserted,
                mount.InstalledPart,
                mount,
                fastenerDefinitionId,
                mount.Authoring.Pose.position);
            return LastOperationResult;
        }

        public AssemblyOperationResult TryRemoveFastener(
            string mountId,
            string fastenerDefinitionId)
        {
            EnsureInitialized();
            operationCount++;
            if (!graph.TryGetMount(mountId, out MountPointRuntime mount) || !mount.IsOccupied)
            {
                return SetFailure(
                    AssemblyOperation.RemoveFastener,
                    AssemblyFailureReason.InvalidMount,
                    "На точке нет установленной детали.");
            }

            if (!mount.TryGetFastener(fastenerDefinitionId, out FastenerInstance fastener) ||
                !fastener.TryRemove())
            {
                return SetFailure(
                    AssemblyOperation.RemoveFastener,
                    AssemblyFailureReason.FastenerSecured,
                    "Крепёж можно вынуть только на нулевой стадии.");
            }

            mount.ReevaluateFastenerGroup();

            graphMutationCount++;
            LastOperationResult = AssemblyOperationResult.Success(
                AssemblyOperation.RemoveFastener,
                "Крепёж вынут.");
            PublishAction(
                AssemblyActionKind.FastenerRemoved,
                mount.InstalledPart,
                mount,
                fastenerDefinitionId,
                mount.Authoring.Pose.position);
            return LastOperationResult;
        }

        public AssemblyOperationResult EvaluateRemoval(PartInstance part)
        {
            EnsureInitialized();
            if (part == null || part.IsAssemblyRoot || !part.IsInstalled)
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Remove,
                    AssemblyFailureReason.InvalidPart,
                    "Деталь не установлена или является корнем сборки.");
            }

            MountPointRuntime mount = graph.FindMountForPart(part);
            if (mount == null)
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Remove,
                    AssemblyFailureReason.InvalidMount,
                    "У детали нет занятой точки установки.");
            }

            if (mount.FastenerGroup.IsBolted)
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Remove,
                    AssemblyFailureReason.FastenerSecured,
                    "Сначала ослабьте группу крепежа до donor BoltedOffThreshold.");
            }

            if (graph.HasInstalledRemovalBlocker(part, out string blockerPartId))
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Remove,
                    AssemblyFailureReason.RemovalBlocked,
                    "Снятие блокирует установленная деталь: " + blockerPartId);
            }

            if (mount.Authoring.IsObstructed(part))
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Remove,
                    AssemblyFailureReason.Obstructed,
                    "Зона снятия перекрыта.");
            }

            return AssemblyOperationResult.Success(
                AssemblyOperation.Remove,
                "Снять: " + part.Definition.DisplayName);
        }

        public AssemblyOperationResult TryRemove(PartInstance part)
        {
            EnsureInitialized();
            operationCount++;
            AssemblyOperationResult evaluation = EvaluateRemoval(part);
            if (!evaluation.Succeeded)
            {
                LastOperationResult = evaluation;
                return evaluation;
            }

            MountPointRuntime mount = graph.FindMountForPart(part);
            Vector3 detachPosition = mount.Authoring.Pose.position + mount.Authoring.Pose.right * 0.35f;
            Quaternion detachRotation = mount.Authoring.Pose.rotation;
            mount.Release();
            part.Detach(loosePartsRoot, detachPosition, detachRotation);
            RefreshOwnedMountAvailability();
            graphMutationCount++;
            LastOperationResult = AssemblyOperationResult.Success(
                AssemblyOperation.Remove,
                "Деталь снята: " + part.Definition.DisplayName);
            PublishAction(
                AssemblyActionKind.PartRemoved,
                part,
                mount,
                string.Empty,
                detachPosition);
            return LastOperationResult;
        }

        public AssemblyOperationResult TryApplyFastenerBreakPolicy(
            string mountId,
            float speedKph,
            uint deterministicSeed)
        {
            EnsureInitialized();
            operationCount++;
            if (!graph.TryGetMount(mountId, out MountPointRuntime mount) ||
                !mount.IsOccupied)
            {
                return SetFailure(
                    AssemblyOperation.BreakRetention,
                    AssemblyFailureReason.InvalidMount,
                    "BREAK policy вызван для пустой или неизвестной точки.");
            }

            FastenerGroupState group = mount.FastenerGroup;
            float sample01 = FastenerGroupDefinition.Sample01(
                deterministicSeed);
            if (!group.ShouldBreak(speedKph, sample01))
            {
                LastOperationResult = AssemblyOperationResult.Success(
                    AssemblyOperation.BreakRetention,
                    "Группа крепежа удержала деталь.");
                return LastOperationResult;
            }

            if (group.Definition.BreakAction !=
                FastenerBreakAction.DetachInstalledPart)
            {
                LastOperationResult = AssemblyOperationResult.Success(
                    AssemblyOperation.BreakRetention,
                    "Donor BREAK зафиксирован без detach action.");
                return LastOperationResult;
            }

            PartInstance part = mount.InstalledPart;
            Vector3 detachPosition = mount.Authoring.Pose.position +
                mount.Authoring.Pose.right * 0.35f;
            Quaternion detachRotation = mount.Authoring.Pose.rotation;
            mount.Release();
            part.Detach(loosePartsRoot, detachPosition, detachRotation);
            RefreshOwnedMountAvailability();
            graphMutationCount++;
            LastOperationResult = AssemblyOperationResult.Success(
                AssemblyOperation.BreakRetention,
                "Donor BREAK: деталь сорвана с крепления.");
            PublishAction(
                AssemblyActionKind.PartBrokenLoose,
                part,
                mount,
                string.Empty,
                detachPosition);
            return LastOperationResult;
        }

        private int CollapseIfStructurallyUnsupported(
            MountPointRuntime installedMount)
        {
            if (installedMount == null ||
                graph.AreMountStructuralRetentionPrerequisitesMet(
                    installedMount,
                    out string unboltedSupportMountId))
            {
                return 0;
            }

            MountPointRuntime collapseRoot = installedMount;
            if (!string.IsNullOrEmpty(unboltedSupportMountId) &&
                graph.TryGetMount(
                    unboltedSupportMountId,
                    out MountPointRuntime supportMount) &&
                supportMount.IsOccupied)
            {
                collapseRoot = supportMount;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            int collapsedPartCount = CollapseMountHierarchy(
                collapseRoot,
                visited);
            if (collapsedPartCount > 0)
            {
                RefreshOwnedMountAvailability();
                SynchronizeInstalledParts();
            }

            return collapsedPartCount;
        }

        private int CollapseMountHierarchy(
            MountPointRuntime root,
            HashSet<string> visited)
        {
            if (root == null || !root.IsOccupied ||
                !visited.Add(root.MountId))
            {
                return 0;
            }

            int collapsedPartCount = 0;
            MountPointRuntime[] mounts = graph.Mounts;
            for (int index = 0; index < mounts.Length; index++)
            {
                MountPointRuntime candidate = mounts[index];
                if (candidate != null && candidate.IsOccupied &&
                    graph.IsMountDependentOn(candidate, root))
                {
                    collapsedPartCount += CollapseMountHierarchy(
                        candidate,
                        visited);
                }
            }

            PartInstance part = root.InstalledPart;
            if (part == null)
            {
                return collapsedPartCount;
            }

            Vector3 detachPosition = part.transform.position;
            Quaternion detachRotation = part.transform.rotation;
            root.Release();
            part.Detach(loosePartsRoot, detachPosition, detachRotation);
            graphMutationCount++;
            PublishAction(
                AssemblyActionKind.PartBrokenLoose,
                part,
                root,
                string.Empty,
                detachPosition);
            return collapsedPartCount + 1;
        }

        private void PublishAction(
            AssemblyActionKind action,
            PartInstance part,
            MountPointRuntime mount,
            string fastenerDefinitionId,
            Vector3 worldPosition,
            float transferredMassKilograms = 0f,
            Vector3 sourceLinearVelocity = default,
            Vector3 sourceAngularVelocity = default)
        {
            ActionCompleted?.Invoke(new AssemblyActionCompleted(
                action,
                part,
                mount?.MountId ?? string.Empty,
                fastenerDefinitionId,
                worldPosition,
                transferredMassKilograms,
                sourceLinearVelocity,
                sourceAngularVelocity));
        }

        public VehicleAssemblySaveData CaptureSaveData()
        {
            EnsureInitialized();
            var data = new VehicleAssemblySaveData
            {
                parts = new PartSaveDto[parts.Length],
                mounts = new MountSaveDto[graph.Mounts.Length],
                fastenerGroups = new FastenerGroupSaveDto[
                    graph.Mounts.Length],
            };

            int fastenerCount = 0;
            for (int i = 0; i < graph.Mounts.Length; i++)
            {
                fastenerCount += graph.Mounts[i].Fasteners.Length;
            }

            data.fasteners = new FastenerSaveDto[fastenerCount];
            for (int i = 0; i < parts.Length; i++)
            {
                PartInstance part = parts[i];
                part.RefreshLoosePose();
                PartRuntimeState state = part.RuntimeState;
                AssemblySteeringAlignmentState alignment =
                    part.GetComponent<AssemblySteeringAlignmentState>();
                data.parts[i] = new PartSaveDto
                {
                    stableEntityId = state.StableEntityId,
                    partDefinitionId = state.PartDefinitionId,
                    lifecycleState = state.LifecycleState,
                    installedMountId = state.InstalledMountId,
                    worldPosition = part.transform.position,
                    worldRotation = part.transform.rotation,
                    hasSteeringAlignment = alignment != null,
                    steeringAlignment = alignment?.CaptureSaveData(),
                };
            }

            int fastenerWriteIndex = 0;
            for (int i = 0; i < graph.Mounts.Length; i++)
            {
                MountPointRuntime mount = graph.Mounts[i];
                data.mounts[i] = new MountSaveDto
                {
                    mountId = mount.MountId,
                    installedPartStableEntityId = mount.IsOccupied
                        ? mount.InstalledPart.StableId.Value
                        : string.Empty
                };
                data.fastenerGroups[i] = new FastenerGroupSaveDto
                {
                    mountId = mount.MountId,
                    isBolted = mount.FastenerGroup.IsBolted,
                };

                for (int fastenerIndex = 0; fastenerIndex < mount.Fasteners.Length; fastenerIndex++)
                {
                    FastenerInstance fastener = mount.Fasteners[fastenerIndex];
                    data.fasteners[fastenerWriteIndex++] = new FastenerSaveDto
                    {
                        mountId = mount.MountId,
                        fastenerDefinitionId = fastener.Definition.DefinitionId,
                        inserted = fastener.IsInserted,
                        seated = fastener.IsSeated,
                        stage = fastener.Stage
                    };
                }
            }

            return data;
        }

        public AssemblyOperationResult RestoreSaveData(VehicleAssemblySaveData data)
        {
            EnsureInitialized();
            operationCount++;
            if (!TryPrepareSaveDataForRestore(
                    data,
                    out VehicleAssemblySaveData prepared,
                    out AssemblyOperationResult preparationFailure))
            {
                LastOperationResult = preparationFailure;
                return preparationFailure;
            }

            AssemblyOperationResult validation = ValidateSaveData(prepared);
            if (!validation.Succeeded)
            {
                LastOperationResult = validation;
                return validation;
            }

            data = prepared;

            for (int i = 0; i < graph.Mounts.Length; i++)
            {
                graph.Mounts[i].Reset();
            }

            for (int i = 0; i < data.parts.Length; i++)
            {
                PartSaveDto dto = data.parts[i];
                graph.TryGetPartByStableId(dto.stableEntityId, out PartInstance part);
                if (dto.lifecycleState == PartLifecycleState.AssemblyRoot)
                {
                    part.MarkAssemblyRoot();
                }
                else if (dto.lifecycleState == PartLifecycleState.Loose)
                {
                    part.Detach(loosePartsRoot, dto.worldPosition, dto.worldRotation);
                }
            }

            for (int i = 0; i < data.parts.Length; i++)
            {
                PartSaveDto dto = data.parts[i];
                if (dto.lifecycleState != PartLifecycleState.Installed)
                {
                    continue;
                }

                graph.TryGetPartByStableId(dto.stableEntityId, out PartInstance part);
                graph.TryGetMount(dto.installedMountId, out MountPointRuntime mount);
                mount.TryOccupy(part);
                part.InstallAt(
                    mount.Authoring,
                    mount.MountId,
                    dto.worldRotation);
            }

            for (int i = 0; i < data.fasteners.Length; i++)
            {
                FastenerSaveDto dto = data.fasteners[i];
                graph.TryGetMount(dto.mountId, out MountPointRuntime mount);
                mount.TryGetFastener(dto.fastenerDefinitionId, out FastenerInstance fastener);
                fastener.TryRestore(dto.inserted, dto.seated, dto.stage);
            }

            for (int i = 0; i < data.fastenerGroups.Length; i++)
            {
                FastenerGroupSaveDto dto = data.fastenerGroups[i];
                graph.TryGetMount(dto.mountId, out MountPointRuntime mount);
                mount.TryRestoreFastenerGroupLatch(dto.isBolted);
            }

            RefreshOwnedMountAvailability();
            SynchronizeInstalledParts();
            for (int i = 0; i < data.parts.Length; i++)
            {
                PartSaveDto dto = data.parts[i];
                graph.TryGetPartByStableId(dto.stableEntityId, out PartInstance part);
                part.GetComponent<AssemblySteeringAlignmentState>()
                    ?.RestoreValidated(dto.hasSteeringAlignment ? dto.steeringAlignment : null);
            }
            graphMutationCount++;
            LastOperationResult = AssemblyOperationResult.Success(
                AssemblyOperation.Restore,
                "Состояние сборки восстановлено.");
            return LastOperationResult;
        }

        public AssemblyOperationResult ValidateSaveDataForRestore(
            VehicleAssemblySaveData data)
        {
            EnsureInitialized();
            return TryPrepareSaveDataForRestore(
                data,
                out VehicleAssemblySaveData prepared,
                out AssemblyOperationResult preparationFailure)
                    ? ValidateSaveData(prepared)
                    : preparationFailure;
        }

        private bool TryPrepareSaveDataForRestore(
            VehicleAssemblySaveData data,
            out VehicleAssemblySaveData prepared,
            out AssemblyOperationResult failure)
        {
            prepared = data;
            failure = default;
            if (data != null && !TryMigrateRetiredSteeringFastenerShape(
                    data,
                    out prepared,
                    out failure))
            {
                return false;
            }

            data = prepared;
            if (data != null && !TryMigrateRetiredMountAliases(
                    data,
                    out prepared,
                    out failure))
            {
                return false;
            }

            data = prepared;
            if (data == null || data.parts == null || data.mounts == null ||
                data.fasteners == null || !data.HasSupportedSchema)
            {
                return true;
            }

            int registeredFastenerCount = CountRegisteredFasteners();
            bool addingFrontStrutLowerFasteners = data.parts.Length == 126 &&
                data.mounts.Length == 117 && data.fasteners.Length == 252 &&
                graph.Mounts.Length == 117 &&
                (registeredFastenerCount == 260 ||
                 registeredFastenerCount == 280);
            bool addingExteriorPanelFasteners = data.parts.Length == 126 &&
                data.mounts.Length == 117 &&
                (data.fasteners.Length == 252 ||
                 data.fasteners.Length == 260) &&
                graph.Mounts.Length == 117 && registeredFastenerCount == 280;
            bool graphShapeMatches =
                data.parts.Length == parts.Length &&
                data.mounts.Length == graph.Mounts.Length &&
                data.fasteners.Length == registeredFastenerCount;
            bool currentGroupShape =
                data.fastenerGroups != null &&
                data.fastenerGroups.Length == graph.Mounts.Length;
            if (data.schemaVersion ==
                    VehicleAssemblySaveData.CurrentSchemaVersion &&
                graphShapeMatches && currentGroupShape)
            {
                return true;
            }

            bool requiresAdditiveGraphMigration = !graphShapeMatches;
            if (data.parts.Length != parts.Length ||
                data.mounts.Length > graph.Mounts.Length ||
                data.fasteners.Length > registeredFastenerCount ||
                requiresAdditiveGraphMigration &&
                (!allowAdditiveSaveMigration || !IsKnownAdditiveSaveShape(
                    data.parts.Length,
                    data.mounts.Length,
                    data.fasteners.Length)))
            {
                failure = InvalidSave(
                    "Размер старого графа сборки нельзя безопасно мигрировать.");
                return false;
            }

            var savedMounts = new Dictionary<string, MountSaveDto>(
                StringComparer.Ordinal);
            for (int index = 0; index < data.mounts.Length; index++)
            {
                MountSaveDto dto = data.mounts[index];
                if (dto == null || string.IsNullOrEmpty(dto.mountId) ||
                    !savedMounts.TryAdd(dto.mountId, dto) ||
                    !graph.TryGetMount(dto.mountId, out _))
                {
                    failure = InvalidSave(
                        "Старый save содержит неизвестную или повторную точку установки.");
                    return false;
                }
            }

            var savedFasteners = new Dictionary<string, FastenerSaveDto>(
                StringComparer.Ordinal);
            for (int index = 0; index < data.fasteners.Length; index++)
            {
                FastenerSaveDto dto = data.fasteners[index];
                string key = dto == null
                    ? string.Empty
                    : dto.mountId + "/" + dto.fastenerDefinitionId;
                if (dto == null || string.IsNullOrEmpty(dto.mountId) ||
                    string.IsNullOrEmpty(dto.fastenerDefinitionId) ||
                    !savedFasteners.TryAdd(key, dto) ||
                    !graph.TryGetMount(dto.mountId, out MountPointRuntime mount) ||
                    !mount.TryGetFastener(dto.fastenerDefinitionId, out _))
                {
                    failure = InvalidSave(
                        "Старый save содержит неизвестный или повторный крепёж.");
                    return false;
                }
            }

            if (addingFrontStrutLowerFasteners ||
                addingExteriorPanelFasteners)
            {
                // These known graph revisions add only explicit stable-ID
                // fasteners. A payload with the same aggregate count but a
                // different missing set is corruption, not a migration.
                foreach (MountPointRuntime mount in graph.Mounts)
                {
                    foreach (FastenerInstance fastener in mount.Fasteners)
                    {
                        string id = fastener.Definition.DefinitionId;
                        bool present = savedFasteners.ContainsKey(
                            mount.MountId + "/" + id);
                        bool expectedMissing =
                            addingFrontStrutLowerFasteners &&
                            IsFrontStrutLowerFastener(mount.MountId, id) ||
                            addingExteriorPanelFasteners &&
                            IsPost260ExteriorPanelFastener(mount.MountId, id);
                        if (present == expectedMissing)
                        {
                            failure = InvalidSave(
                                "Старый save потерял крепёж вне известной additive-миграции.");
                            return false;
                        }
                    }
                }
            }

            var savedGroups = new Dictionary<string, FastenerGroupSaveDto>(
                StringComparer.Ordinal);
            FastenerGroupSaveDto[] sourceGroups = data.fastenerGroups ??
                Array.Empty<FastenerGroupSaveDto>();
            for (int index = 0; index < sourceGroups.Length; index++)
            {
                FastenerGroupSaveDto dto = sourceGroups[index];
                if (dto == null || string.IsNullOrEmpty(dto.mountId) ||
                    !savedGroups.TryAdd(dto.mountId, dto) ||
                    !graph.TryGetMount(dto.mountId, out _))
                {
                    failure = InvalidSave(
                        "Старый save содержит неизвестную или повторную группу крепежа.");
                    return false;
                }
            }

            if (data.schemaVersion ==
                    VehicleAssemblySaveData.CurrentSchemaVersion &&
                sourceGroups.Length != graph.Mounts.Length)
            {
                failure = InvalidSave(
                    "Текущий save потерял latch-группы крепежа и не может быть восстановлен двусмысленно.");
                return false;
            }

            var migrated = new VehicleAssemblySaveData
            {
                schemaVersion = VehicleAssemblySaveData.CurrentSchemaVersion,
                parts = data.parts,
                mounts = new MountSaveDto[graph.Mounts.Length],
                fasteners = new FastenerSaveDto[registeredFastenerCount],
                fastenerGroups = new FastenerGroupSaveDto[
                    graph.Mounts.Length],
            };
            int fastenerIndex = 0;
            for (int mountIndex = 0; mountIndex < graph.Mounts.Length; mountIndex++)
            {
                MountPointRuntime mount = graph.Mounts[mountIndex];
                migrated.mounts[mountIndex] = savedMounts.TryGetValue(
                    mount.MountId,
                    out MountSaveDto savedMount)
                        ? savedMount
                        : new MountSaveDto { mountId = mount.MountId };
                bool mountOccupied = !string.IsNullOrEmpty(
                    migrated.mounts[mountIndex]
                        .installedPartStableEntityId);
                for (int index = 0; index < mount.Fasteners.Length; index++)
                {
                    FastenerDefinition definition =
                        mount.Fasteners[index].Definition;
                    string key = mount.MountId + "/" + definition.DefinitionId;
                    migrated.fasteners[fastenerIndex++] = savedFasteners.TryGetValue(
                        key,
                        out FastenerSaveDto savedFastener)
                            ? savedFastener
                            : new FastenerSaveDto
                            {
                                mountId = mount.MountId,
                                fastenerDefinitionId = definition.DefinitionId,
                                inserted = mountOccupied,
                                seated = mountOccupied,
                                // New lower strut bolts start loose. Existing
                                // installed exterior panels inherit secured new
                                // fasteners so the graph expansion cannot detach
                                // or invalidate previously installed parts.
                                stage = mountOccupied &&
                                    !(addingFrontStrutLowerFasteners &&
                                      IsFrontStrutLowerFastener(
                                          mount.MountId,
                                          definition.DefinitionId))
                                    ? definition.MaximumStage
                                    : 0,
                            };
                }

                int tightness = CalculateSavedGroupTightness(
                    mount,
                    migrated.fasteners,
                    fastenerIndex - mount.Fasteners.Length,
                    mount.Fasteners.Length);
                bool rebuildLatchForAddedExteriorFasteners =
                    addingExteriorPanelFasteners &&
                    IsPost260ExteriorPanelMount(mount.MountId);
                bool migratedLatch = !rebuildLatchForAddedExteriorFasteners &&
                    savedGroups.TryGetValue(
                        mount.MountId,
                        out FastenerGroupSaveDto savedGroup)
                            ? savedGroup.isBolted
                            : mountOccupied &&
                              mount.FastenerGroup.Definition.HasFasteners &&
                              tightness >= mount.FastenerGroup.Definition
                                  .BoltedOnThreshold;
                migrated.fastenerGroups[mountIndex] =
                    new FastenerGroupSaveDto
                    {
                        mountId = mount.MountId,
                        isBolted = migratedLatch,
                    };
            }

            prepared = migrated;
            return true;
        }

        private bool TryMigrateRetiredSteeringFastenerShape(
            VehicleAssemblySaveData source,
            out VehicleAssemblySaveData migrated,
            out AssemblyOperationResult failure)
        {
            const string columnMountId = "mount.satsuma.steering-column";
            const string oldTachometerFastenerId =
                "fastener.satsuma.steering-column.boltpm-2";
            const string retiredColumnFastenerId =
                "fastener.satsuma.steering-column.boltpm-3";
            const string steeringWheelMountId =
                "mount.satsuma.steering-wheel";
            const string steeringWheelFastenerId =
                "fastener.satsuma.steering-wheel.boltpm-1";

            migrated = source;
            failure = default;
            if (source.fasteners == null || source.mounts == null ||
                !source.fasteners.Any(value => value != null &&
                    string.Equals(
                        value.mountId,
                        columnMountId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        value.fastenerDefinitionId,
                        retiredColumnFastenerId,
                        StringComparison.Ordinal)))
            {
                return true;
            }

            FastenerSaveDto[] retiredColumn = source.fasteners
                .Where(value => value != null && string.Equals(
                        value.mountId,
                        columnMountId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        value.fastenerDefinitionId,
                        retiredColumnFastenerId,
                        StringComparison.Ordinal))
                .ToArray();
            FastenerSaveDto[] leakedTachometer = source.fasteners
                .Where(value => value != null && string.Equals(
                        value.mountId,
                        columnMountId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        value.fastenerDefinitionId,
                        oldTachometerFastenerId,
                        StringComparison.Ordinal))
                .ToArray();
            if (retiredColumn.Length != 1 || leakedTachometer.Length != 1 ||
                !graph.TryGetMount(
                    steeringWheelMountId,
                    out MountPointRuntime steeringWheelMount) ||
                !steeringWheelMount.TryGetFastener(
                    steeringWheelFastenerId,
                    out FastenerInstance steeringWheelFastener))
            {
                failure = InvalidSave(
                    "Старый save содержит неоднозначную ревизию крепежа руля.");
                return false;
            }

            bool alreadyHasSteeringWheelFastener = source.fasteners.Any(
                value => value != null && string.Equals(
                        value.mountId,
                        steeringWheelMountId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        value.fastenerDefinitionId,
                        steeringWheelFastenerId,
                        StringComparison.Ordinal));
            bool steeringWheelOccupied = source.mounts.Any(value =>
                value != null && string.Equals(
                    value.mountId,
                    steeringWheelMountId,
                    StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(value.installedPartStableEntityId));

            var fasteners = new List<FastenerSaveDto>(
                source.fasteners.Length +
                (alreadyHasSteeringWheelFastener ? -1 : 0));
            for (int index = 0; index < source.fasteners.Length; index++)
            {
                FastenerSaveDto value = source.fasteners[index];
                if (ReferenceEquals(value, retiredColumn[0]))
                {
                    continue;
                }

                if (ReferenceEquals(value, leakedTachometer[0]))
                {
                    FastenerSaveDto donorBolt = retiredColumn[0];
                    fasteners.Add(new FastenerSaveDto
                    {
                        mountId = columnMountId,
                        fastenerDefinitionId = oldTachometerFastenerId,
                        inserted = donorBolt.inserted,
                        seated = donorBolt.seated,
                        stage = donorBolt.stage,
                    });
                    continue;
                }

                fasteners.Add(CloneFastenerSaveDto(value));
            }

            if (!alreadyHasSteeringWheelFastener)
            {
                fasteners.Add(new FastenerSaveDto
                {
                    mountId = steeringWheelMountId,
                    fastenerDefinitionId = steeringWheelFastenerId,
                    inserted = steeringWheelOccupied,
                    seated = steeringWheelOccupied,
                    stage = steeringWheelOccupied
                        ? steeringWheelFastener.Definition.MaximumStage
                        : 0,
                });
            }

            FastenerSaveDto[] migratedFasteners = fasteners.ToArray();
            FastenerGroupSaveDto[] migratedGroups = source.fastenerGroups == null
                ? null
                : source.fastenerGroups.Select(value => value == null
                    ? null
                    : new FastenerGroupSaveDto
                    {
                        mountId = value.mountId,
                        isBolted = value.isBolted,
                    }).ToArray();
            RebuildMigratedFastenerGroupLatch(
                source.mounts,
                migratedFasteners,
                migratedGroups,
                columnMountId);
            RebuildMigratedFastenerGroupLatch(
                source.mounts,
                migratedFasteners,
                migratedGroups,
                steeringWheelMountId);

            migrated = new VehicleAssemblySaveData
            {
                schemaVersion = source.schemaVersion,
                parts = source.parts,
                mounts = source.mounts,
                fasteners = migratedFasteners,
                fastenerGroups = migratedGroups,
            };
            return true;
        }

        private void RebuildMigratedFastenerGroupLatch(
            MountSaveDto[] savedMounts,
            FastenerSaveDto[] savedFasteners,
            FastenerGroupSaveDto[] savedGroups,
            string mountId)
        {
            if (savedGroups == null || !graph.TryGetMount(
                    mountId,
                    out MountPointRuntime mount))
            {
                return;
            }

            bool occupied = savedMounts.Any(value => value != null &&
                string.Equals(value.mountId, mountId, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(value.installedPartStableEntityId));
            int tightness = savedFasteners
                .Where(value => value != null && string.Equals(
                    value.mountId,
                    mountId,
                    StringComparison.Ordinal) &&
                    mount.FastenerGroup.Definition.FastenerDefinitionIds
                        .Contains(
                            value.fastenerDefinitionId,
                            StringComparer.Ordinal))
                .Sum(value => value.stage);
            bool isBolted = occupied &&
                mount.FastenerGroup.Definition.HasFasteners &&
                tightness >= mount.FastenerGroup.Definition.BoltedOnThreshold;
            for (int index = 0; index < savedGroups.Length; index++)
            {
                FastenerGroupSaveDto group = savedGroups[index];
                if (group != null && string.Equals(
                        group.mountId,
                        mountId,
                        StringComparison.Ordinal))
                {
                    group.isBolted = isBolted;
                }
            }
        }

        private static FastenerSaveDto CloneFastenerSaveDto(
            FastenerSaveDto value) => value == null
                ? null
                : new FastenerSaveDto
                {
                    mountId = value.mountId,
                    fastenerDefinitionId = value.fastenerDefinitionId,
                    inserted = value.inserted,
                    seated = value.seated,
                    stage = value.stage,
                };

        private bool TryMigrateRetiredMountAliases(
            VehicleAssemblySaveData source,
            out VehicleAssemblySaveData migrated,
            out AssemblyOperationResult failure)
        {
            const string retiredSubframeMountId = "mount.satsuma.subframe";
            const string canonicalSubframeMountId = "mount.satsuma.sub-frame";

            migrated = source;
            failure = default;
            if (source.parts == null || source.mounts == null ||
                source.fasteners == null ||
                !source.mounts.Any(value => value != null && string.Equals(
                    value.mountId,
                    retiredSubframeMountId,
                    StringComparison.Ordinal)))
            {
                return true;
            }

            if (!graph.TryGetMount(canonicalSubframeMountId, out _))
            {
                failure = InvalidSave(
                    "Не найдена каноническая точка установки подрамника.");
                return false;
            }

            MountSaveDto retired = source.mounts.First(value =>
                value != null && string.Equals(
                    value.mountId,
                    retiredSubframeMountId,
                    StringComparison.Ordinal));
            MountSaveDto canonical = source.mounts.FirstOrDefault(value =>
                value != null && string.Equals(
                    value.mountId,
                    canonicalSubframeMountId,
                    StringComparison.Ordinal));
            string retiredOccupant = retired.installedPartStableEntityId ??
                string.Empty;
            string canonicalOccupant = canonical != null
                ? canonical.installedPartStableEntityId ?? string.Empty
                : string.Empty;
            if (!string.IsNullOrEmpty(retiredOccupant) &&
                !string.IsNullOrEmpty(canonicalOccupant) &&
                !string.Equals(
                    retiredOccupant,
                    canonicalOccupant,
                    StringComparison.Ordinal))
            {
                failure = InvalidSave(
                    "Старая и каноническая точки подрамника заняты разными деталями.");
                return false;
            }

            string occupant = !string.IsNullOrEmpty(canonicalOccupant)
                ? canonicalOccupant
                : retiredOccupant;
            PartSaveDto[] parts = source.parts.Select(value =>
            {
                if (value == null)
                {
                    return null;
                }

                return new PartSaveDto
                {
                    stableEntityId = value.stableEntityId,
                    partDefinitionId = value.partDefinitionId,
                    lifecycleState = value.lifecycleState,
                    installedMountId = string.Equals(
                        value.installedMountId,
                        retiredSubframeMountId,
                        StringComparison.Ordinal)
                            ? canonicalSubframeMountId
                            : value.installedMountId,
                    worldPosition = value.worldPosition,
                    worldRotation = value.worldRotation,
                    hasSteeringAlignment = value.hasSteeringAlignment,
                    steeringAlignment = value.steeringAlignment == null
                        ? null
                        : new AssemblySteeringAlignmentSaveDto
                        {
                            schemaVersion = value.steeringAlignment.schemaVersion,
                            alignmentDegrees = value.steeringAlignment.alignmentDegrees,
                        },
                };
            }).ToArray();
            MountSaveDto[] mounts = source.mounts
                .Where(value => value == null || !string.Equals(
                    value.mountId,
                    retiredSubframeMountId,
                    StringComparison.Ordinal))
                .Select(value => value == null
                    ? null
                    : new MountSaveDto
                    {
                        mountId = value.mountId,
                        installedPartStableEntityId = string.Equals(
                            value.mountId,
                            canonicalSubframeMountId,
                            StringComparison.Ordinal)
                                ? occupant
                                : value.installedPartStableEntityId,
                    })
                .ToArray();
            FastenerSaveDto[] fasteners = source.fasteners.Select(value =>
                value == null
                    ? null
                    : new FastenerSaveDto
                    {
                        mountId = string.Equals(
                            value.mountId,
                            retiredSubframeMountId,
                            StringComparison.Ordinal)
                                ? canonicalSubframeMountId
                                : value.mountId,
                        fastenerDefinitionId = value.fastenerDefinitionId,
                        inserted = value.inserted,
                        seated = value.seated,
                        stage = value.stage,
                    }).ToArray();
            FastenerGroupSaveDto[] sourceGroups = source.fastenerGroups ??
                Array.Empty<FastenerGroupSaveDto>();
            FastenerGroupSaveDto retiredGroup = sourceGroups.FirstOrDefault(
                value => value != null && string.Equals(
                    value.mountId,
                    retiredSubframeMountId,
                    StringComparison.Ordinal));
            FastenerGroupSaveDto canonicalGroup = sourceGroups.FirstOrDefault(
                value => value != null && string.Equals(
                    value.mountId,
                    canonicalSubframeMountId,
                    StringComparison.Ordinal));
            if (retiredGroup != null && canonicalGroup != null &&
                retiredGroup.isBolted != canonicalGroup.isBolted)
            {
                failure = InvalidSave(
                    "Старая и каноническая latch-группы подрамника противоречат друг другу.");
                return false;
            }

            var migratedGroups = sourceGroups
                .Where(value => value == null || !string.Equals(
                    value.mountId,
                    retiredSubframeMountId,
                    StringComparison.Ordinal))
                .Select(value => value == null
                    ? null
                    : new FastenerGroupSaveDto
                    {
                        mountId = value.mountId,
                        isBolted = value.isBolted,
                    })
                .ToList();
            if (retiredGroup != null && canonicalGroup == null)
            {
                migratedGroups.Add(new FastenerGroupSaveDto
                {
                    mountId = canonicalSubframeMountId,
                    isBolted = retiredGroup.isBolted,
                });
            }

            FastenerGroupSaveDto[] fastenerGroups =
                migratedGroups.ToArray();
            migrated = new VehicleAssemblySaveData
            {
                schemaVersion = source.schemaVersion,
                parts = parts,
                mounts = mounts,
                fasteners = fasteners,
                fastenerGroups = fastenerGroups,
            };
            return true;
        }

        private bool IsKnownAdditiveSaveShape(
            int savedPartCount,
            int savedMountCount,
            int savedFastenerCount)
        {
            // These are generated Satsuma graph revisions that were locally
            // playable before fastener coverage additions. A truncated payload
            // must still fail closed instead of being mistaken for migration.
            return savedPartCount == 126 && savedMountCount == 117 &&
                       (savedFastenerCount == 252 ||
                        savedFastenerCount == 260) ||
                   savedMountCount == 115 &&
                       (savedFastenerCount == 205 ||
                        savedFastenerCount == 220) ||
                   savedMountCount == 46 && savedFastenerCount == 57 ||
                   savedMountCount == 83 && savedFastenerCount == 167 ||
                   savedMountCount == 84 && savedFastenerCount == 170;
        }

        private static bool IsFrontStrutLowerFastener(string mountId, string id)
        {
            if (mountId != "mount.satsuma.strut-fl" &&
                mountId != "mount.satsuma.strut-fr")
            {
                return false;
            }

            string prefix = "fastener.satsuma.strut-" +
                mountId.Substring(mountId.Length - 2) + ".lower-";
            for (int index = 1; index <= 4; index++)
            {
                if (id == prefix + index)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPost260ExteriorPanelFastener(
            string mountId,
            string fastenerId)
        {
            int count = GetPost260ExteriorPanelFastenerCount(mountId);
            if (count == 0)
            {
                return false;
            }

            const string mountPrefix = "mount.";
            string fastenerPrefix = "fastener." +
                mountId.Substring(mountPrefix.Length) + ".boltpm-";
            for (int index = 1; index <= count; index++)
            {
                if (string.Equals(
                        fastenerId,
                        fastenerPrefix + index,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPost260ExteriorPanelMount(string mountId) =>
            GetPost260ExteriorPanelFastenerCount(mountId) > 0;

        private static int GetPost260ExteriorPanelFastenerCount(
            string mountId)
        {
            switch (mountId)
            {
                case "mount.satsuma.bumper-front":
                case "mount.satsuma.bumper-rear":
                case "mount.satsuma.grille":
                    return 2;

                case "mount.satsuma.fender-left":
                case "mount.satsuma.fender-right":
                    return 5;

                case "mount.satsuma.hood":
                    return 4;

                default:
                    return 0;
            }
        }

        private static int CalculateSavedGroupTightness(
            MountPointRuntime mount,
            FastenerSaveDto[] savedFasteners,
            int startIndex,
            int count)
        {
            FastenerGroupDefinition definition =
                mount.FastenerGroup.Definition;
            string[] groupIds = definition.FastenerDefinitionIds;
            int result = 0;
            int end = Mathf.Min(savedFasteners.Length, startIndex + count);
            for (int index = Mathf.Max(0, startIndex); index < end; index++)
            {
                FastenerSaveDto dto = savedFasteners[index];
                if (dto == null)
                {
                    continue;
                }

                for (int groupIndex = 0;
                     groupIndex < groupIds.Length;
                     groupIndex++)
                {
                    if (string.Equals(
                            groupIds[groupIndex],
                            dto.fastenerDefinitionId,
                            StringComparison.Ordinal))
                    {
                        result += dto.stage;
                        break;
                    }
                }
            }

            return Mathf.Clamp(
                result,
                0,
                definition.AggregateMaximumTightness);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            BindMountsToLogicalOwners();
            var mountRuntimes = new MountPointRuntime[mountPoints.Length];
            for (int i = 0; i < mountPoints.Length; i++)
            {
                if (mountPoints[i] != null)
                {
                    mountRuntimes[i] = new MountPointRuntime(mountPoints[i]);
                }
            }

            graph = new AssemblyGraph(parts, mountRuntimes, dependencies);
            query = new VehicleAssemblyQuery(graph);
            initialized = true;

            for (int i = 0; i < parts.Length; i++)
            {
                PartInstance part = parts[i];
                if (part != null && part.IsAssemblyRoot)
                {
                    part.MarkAssemblyRoot();
                }
            }

            for (int i = 0; i < parts.Length; i++)
            {
                PartInstance part = parts[i];
                if (part == null || part.IsAssemblyRoot || string.IsNullOrEmpty(part.InitialMountId))
                {
                    continue;
                }

                if (graph.TryGetMount(part.InitialMountId, out MountPointRuntime mount) &&
                    mount != null && !mount.IsOccupied)
                {
                    mount.TryOccupy(part);
                    part.InstallAt(mount.Authoring, mount.MountId);
                }
            }

            RefreshOwnedMountAvailability();
        }

        private void RefreshOwnedMountAvailability()
        {
            for (int index = 0; index < mountPoints.Length; index++)
            {
                mountPoints[index]
                    ?.GetComponent<AssemblyOwnedMountAuthoring>()
                    ?.RefreshAvailability();
            }
        }

        private void BindMountsToLogicalOwners()
        {
            PartInstance assemblyRoot = null;
            for (int partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                if (parts[partIndex] != null && parts[partIndex].IsAssemblyRoot)
                {
                    assemblyRoot = parts[partIndex];
                    break;
                }
            }

            for (int mountIndex = 0; mountIndex < mountPoints.Length; mountIndex++)
            {
                MountPointAuthoring mount = mountPoints[mountIndex];
                if (mount == null || mount.Definition == null)
                {
                    continue;
                }

                PartInstance owner = FindPartByDefinitionId(
                    mount.Definition.OwnerPartDefinitionId) ?? assemblyRoot;
                if (owner == null || mount.transform == owner.transform ||
                    mount.transform.IsChildOf(owner.transform))
                {
                    continue;
                }

                // Donor mount pivots describe a logical assembly hierarchy.
                // Scene/prefab hierarchy is presentation only and can leave
                // sockets behind when a dynamic chassis moves. Reparent the
                // socket while preserving its reviewed world pose so the
                // trigger, installed part and fasteners become one aggregate.
                mount.transform.SetParent(owner.transform, true);
            }
        }

        private PartInstance FindPartByDefinitionId(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId))
            {
                return null;
            }

            for (int index = 0; index < parts.Length; index++)
            {
                PartInstance part = parts[index];
                if (part != null && part.Definition != null &&
                    string.Equals(
                        part.Definition.DefinitionId,
                        definitionId,
                        StringComparison.Ordinal))
                {
                    return part;
                }
            }

            return null;
        }

        private AssemblyOperationResult ValidateSaveData(VehicleAssemblySaveData data)
        {
            if (data == null || data.schemaVersion !=
                    VehicleAssemblySaveData.CurrentSchemaVersion ||
                data.parts == null || data.mounts == null ||
                data.fasteners == null || data.fastenerGroups == null ||
                data.parts.Length != parts.Length ||
                data.mounts.Length != graph.Mounts.Length ||
                data.fasteners.Length != CountRegisteredFasteners() ||
                data.fastenerGroups.Length != graph.Mounts.Length)
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Restore,
                    AssemblyFailureReason.InvalidSaveData,
                    "Версия или размер save DTO не соответствует сборке.");
            }

            var seenParts = new HashSet<string>(StringComparer.Ordinal);
            var occupiedMountIds = new HashSet<string>(StringComparer.Ordinal);
            var installedPartDefinitionIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (int i = 0; i < data.parts.Length; i++)
            {
                PartSaveDto dto = data.parts[i];
                if (dto == null || !seenParts.Add(dto.stableEntityId) ||
                    !graph.TryGetPartByStableId(dto.stableEntityId, out PartInstance part) ||
                    part.Definition == null || !string.Equals(
                        part.Definition.DefinitionId,
                        dto.partDefinitionId,
                        StringComparison.Ordinal))
                {
                    return InvalidSave("Неизвестная, повторная или несовместимая деталь в DTO.");
                }

                if (!Enum.IsDefined(typeof(PartLifecycleState), dto.lifecycleState) ||
                    !IsFinite(dto.worldPosition) ||
                    !IsValidRotation(dto.worldRotation))
                {
                    return InvalidSave("Некорректные lifecycle или transform детали в DTO.");
                }

                if (dto.hasSteeringAlignment &&
                    (dto.steeringAlignment == null || !dto.steeringAlignment.IsValid ||
                     part.GetComponent<AssemblySteeringAlignmentState>() == null))
                {
                    return InvalidSave("Некорректное схождение или настройка у неподходящей детали.");
                }

                if (part.IsAssemblyRoot !=
                        (dto.lifecycleState == PartLifecycleState.AssemblyRoot) ||
                    (dto.lifecycleState != PartLifecycleState.Installed &&
                     !string.IsNullOrEmpty(dto.installedMountId)))
                {
                    return InvalidSave("Assembly root или installed mount детали не согласованы.");
                }

                if (dto.lifecycleState == PartLifecycleState.Installed)
                {
                    installedPartDefinitionIds.Add(dto.partDefinitionId);
                    if (!occupiedMountIds.Add(dto.installedMountId) ||
                        !graph.TryGetMount(dto.installedMountId, out MountPointRuntime mount) ||
                        !part.Definition.IsCompatibleWith(mount.Definition))
                    {
                        return InvalidSave("Некорректная или повторно занятая точка установки в DTO.");
                    }
                }
            }

            for (int i = 0; i < data.parts.Length; i++)
            {
                PartSaveDto dto = data.parts[i];
                if (dto.lifecycleState != PartLifecycleState.Installed ||
                    !graph.TryGetMount(
                        dto.installedMountId,
                        out MountPointRuntime mount))
                {
                    continue;
                }

                AssemblyOwnedMountAuthoring ownedMount = mount.Authoring
                    .GetComponent<AssemblyOwnedMountAuthoring>();
                if (ownedMount != null &&
                    ownedMount.RequireInstalledOwner &&
                    !installedPartDefinitionIds.Contains(
                        mount.Definition.OwnerPartDefinitionId))
                {
                    return InvalidSave(
                        "Дочерняя точка занята без установленного владельца.");
                }
            }

            var seenMounts = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < data.mounts.Length; i++)
            {
                MountSaveDto dto = data.mounts[i];
                if (dto == null || !seenMounts.Add(dto.mountId) ||
                    !graph.TryGetMount(dto.mountId, out _))
                {
                    return InvalidSave("Неизвестная или повторная точка установки в DTO.");
                }

                string expectedPartId = string.Empty;
                for (int partIndex = 0; partIndex < data.parts.Length; partIndex++)
                {
                    PartSaveDto partDto = data.parts[partIndex];
                    if (partDto.lifecycleState == PartLifecycleState.Installed && string.Equals(
                            partDto.installedMountId,
                            dto.mountId,
                            StringComparison.Ordinal))
                    {
                        expectedPartId = partDto.stableEntityId;
                        break;
                    }
                }

                if (!string.Equals(
                        expectedPartId,
                        dto.installedPartStableEntityId ?? string.Empty,
                        StringComparison.Ordinal))
                {
                    return InvalidSave("Occupancy mount DTO не совпадает с part DTO.");
                }
            }

            var seenFasteners = new HashSet<string>(StringComparer.Ordinal);
            var savedFasteners = new Dictionary<string, FastenerSaveDto>(
                StringComparer.Ordinal);
            for (int i = 0; i < data.fasteners.Length; i++)
            {
                FastenerSaveDto dto = data.fasteners[i];
                string key = dto == null ? string.Empty : dto.mountId + "/" + dto.fastenerDefinitionId;
                if (dto == null || !seenFasteners.Add(key) ||
                    !graph.TryGetMount(dto.mountId, out MountPointRuntime mount) ||
                    !mount.TryGetFastener(dto.fastenerDefinitionId, out FastenerInstance fastener) ||
                    dto.stage < 0 || dto.stage > fastener.Definition.MaximumStage ||
                    (!dto.inserted && (dto.seated || dto.stage != 0)) ||
                    (dto.inserted && !dto.seated && dto.stage != 0) ||
                    (dto.inserted && !occupiedMountIds.Contains(dto.mountId)))
                {
                    return InvalidSave("Некорректное состояние крепежа в DTO.");
                }

                savedFasteners.Add(key, dto);
            }

            var seenGroups = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < data.fastenerGroups.Length; index++)
            {
                FastenerGroupSaveDto dto = data.fastenerGroups[index];
                if (dto == null || !seenGroups.Add(dto.mountId) ||
                    !graph.TryGetMount(dto.mountId, out MountPointRuntime mount))
                {
                    return InvalidSave(
                        "Неизвестная или повторная latch-группа крепежа в DTO.");
                }

                FastenerGroupDefinition definition =
                    mount.FastenerGroup.Definition;
                int tightness = 0;
                string[] groupIds = definition.FastenerDefinitionIds;
                for (int fastenerIndex = 0;
                     fastenerIndex < groupIds.Length;
                     fastenerIndex++)
                {
                    string key = dto.mountId + "/" +
                                 groupIds[fastenerIndex];
                    if (!savedFasteners.TryGetValue(
                            key,
                            out FastenerSaveDto fastenerDto))
                    {
                        return InvalidSave(
                            "Latch-группа ссылается на отсутствующий крепёж.");
                    }

                    tightness += fastenerDto.stage;
                }

                tightness = Mathf.Clamp(
                    tightness,
                    0,
                    definition.AggregateMaximumTightness);
                if (!definition.IsLatchConsistent(
                        tightness,
                        dto.isBolted,
                        occupiedMountIds.Contains(dto.mountId)))
                {
                    return InvalidSave(
                        "Latch IsBolted противоречит Tightness или occupancy.");
                }
            }

            return AssemblyOperationResult.Success(AssemblyOperation.Restore, "DTO валиден.");
        }

        private int CountRegisteredFasteners()
        {
            int count = 0;
            for (int i = 0; i < graph.Mounts.Length; i++)
            {
                count += graph.Mounts[i].Fasteners.Length;
            }

            return count;
        }

        private AssemblyOperationResult InvalidSave(string message)
        {
            return AssemblyOperationResult.Failure(
                AssemblyOperation.Restore,
                AssemblyFailureReason.InvalidSaveData,
                message);
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsValidRotation(Quaternion value)
        {
            if (!float.IsFinite(value.x) ||
                !float.IsFinite(value.y) ||
                !float.IsFinite(value.z) ||
                !float.IsFinite(value.w))
            {
                return false;
            }

            float magnitudeSquared = value.x * value.x + value.y * value.y +
                                     value.z * value.z + value.w * value.w;
            return magnitudeSquared > 0.000001f &&
                   magnitudeSquared < 1000000f;
        }

        private AssemblyOperationResult SetFailure(
            AssemblyOperation operation,
            AssemblyFailureReason reason,
            string message)
        {
            LastOperationResult = AssemblyOperationResult.Failure(operation, reason, message);
            return LastOperationResult;
        }
    }
}
