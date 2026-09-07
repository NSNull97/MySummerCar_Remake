using System;
using MSC.Interaction.Carrying;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [Serializable]
    public sealed class AssemblyEngineDockingSaveDto
    {
        public int schemaVersion = 1;
        public int[] pendingStages = new int[3];
        public bool IsValid => schemaVersion == 1 && pendingStages != null && pendingStages.Length == 3 &&
            pendingStages[0] >= 0 && pendingStages[1] >= 0 && pendingStages[2] >= 0 &&
            pendingStages[0] <= 1 && pendingStages[1] <= 1 && pendingStages[2] <= 1 &&
            pendingStages[0] + pendingStages[1] + pendingStages[2] < 2;
        public bool IsEmpty => IsValid && pendingStages[0] + pendingStages[1] + pendingStages[2] == 0;
    }

    /// <summary>Bolts become reachable before the loose engine is attached. The graph remains binary.</summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyEngineDockingState : MonoBehaviour
    {
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private PartInstance block;
        [SerializeField] private MountPointAuthoring mount;
        [SerializeField] private Transform chassisFrame;
        [SerializeField] private Vector3[] blockPoints = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] chassisPoints = Array.Empty<Vector3>();
        [SerializeField] private string[] fastenerIds = Array.Empty<string>();
        [SerializeField] private float distanceToleranceMeters = 0.1f;
        private FastenerInstance[] pending;
        private bool attaching;
        private readonly bool[] withinDockingRange = new bool[3];

        public VehicleAssemblyController Assembly => assembly;
        public PartInstance Block => block;
        public MountPointAuthoring Mount => mount;
        public Transform ChassisFrame => chassisFrame;
        public Vector3[] BlockPoints => blockPoints;
        public Vector3[] ChassisPoints => chassisPoints;
        public float DistanceToleranceMeters => distanceToleranceMeters;
        public string[] FastenerIds => fastenerIds;
        public bool IsAttaching => attaching;

        public void Configure(VehicleAssemblyController controller, PartInstance engineBlock,
            MountPointAuthoring engineMount, Transform referenceFrame, Vector3[] engineAnchors,
            Vector3[] chassisAnchors, string[] ids)
        {
            if (attaching) throw new InvalidOperationException("Cannot reconfigure engine docking during attachment.");
            assembly = controller; block = engineBlock; mount = engineMount; chassisFrame = referenceFrame;
            blockPoints = engineAnchors == null ? Array.Empty<Vector3>() : (Vector3[])engineAnchors.Clone();
            chassisPoints = chassisAnchors == null ? Array.Empty<Vector3>() : (Vector3[])chassisAnchors.Clone();
            fastenerIds = ids == null ? Array.Empty<string>() : (string[])ids.Clone();
            Array.Clear(withinDockingRange, 0, withinDockingRange.Length);
            pending = null;
        }

        private void EnsureInitialized()
        {
            if (pending != null) return;
            if (assembly == null || block == null || block.Definition == null || block.IsAssemblyRoot ||
                block.gameObject != gameObject || block.Body == null || block.PickupTarget == null ||
                mount?.Definition == null || chassisFrame == null ||
                Array.IndexOf(assembly.Parts, block) < 0 || Array.IndexOf(assembly.MountPoints, mount) < 0 ||
                blockPoints == null || chassisPoints == null || fastenerIds == null ||
                blockPoints.Length != 3 || chassisPoints.Length != 3 || fastenerIds.Length != 3 ||
                mount.Definition.Fasteners == null || mount.Definition.Fasteners.Length != 3 ||
                !float.IsFinite(distanceToleranceMeters) || distanceToleranceMeters <= 0f)
                throw new InvalidOperationException("Invalid engine docking binding.");
            var states = new FastenerInstance[3];
            for (int index = 0; index < 3; index++)
            {
                if (fastenerIds[index] != "fastener.satsuma.engine-assembly.boltpm-" + (index + 1) ||
                    !IsFinite(blockPoints[index]) || !IsFinite(chassisPoints[index]))
                    throw new InvalidOperationException("Engine docking anchors or bolt identity drifted.");
                FastenerDefinition definition = Array.Find(mount.Definition.Fasteners,
                    value => value != null && value.DefinitionId == fastenerIds[index]);
                if (definition == null || definition.MaximumStage != 8 || definition.Size != FastenerSize.Millimeter11 ||
                    !definition.InsertedOnInstall || !definition.RequiredForRemoval ||
                    definition.TighteningDirection != FastenerDirection.ClockwiseToTighten ||
                    definition.ToolRule.ToolType != "Wrench" || definition.ToolRule.FastenerSize != FastenerSize.Millimeter11)
                    throw new InvalidOperationException("Missing or incompatible engine docking bolt.");
                states[index] = new FastenerInstance(definition);
                states[index].ResetForInstalledPart();
            }
            ValidateGroup(mount.Definition.FastenerGroup);
            assembly.Initialize();
            pending = states;
        }

        public bool CanExposePending(int index)
        {
            EnsureInitialized();
            if (index < 0 || index >= 3) return false;
            // A heavy engine supported by a jack/hoist can be bolted. Carry mass
            // permission is intentionally not a prerequisite for docking.
            if (attaching || block.IsInstalled || block.Body == null || block.PickupTarget.IsCarried ||
                !assembly.EvaluateHandoffInstall(block, mount).Succeeded)
            {
                withinDockingRange[index] = false;
                return false;
            }
            float distance = Vector3.Distance(block.transform.TransformPoint(blockPoints[index]),
                chassisFrame.TransformPoint(chassisPoints[index]));
            if (!float.IsFinite(distance) || distance > distanceToleranceMeters) withinDockingRange[index] = false;
            else if (distance < distanceToleranceMeters) withinDockingRange[index] = true;
            // Donor FloatCompare leaves equality unhandled in both states.
            return withinDockingRange[index];
        }

        public bool TryGetPending(string id, out FastenerInstance fastener)
        {
            EnsureInitialized();
            int index = Array.IndexOf(fastenerIds, id);
            fastener = index >= 0 && CanExposePending(index) ? pending[index] : null;
            return fastener != null;
        }

        public AssemblyOperationResult TryTurnPending(string id, ToolDefinition tool, FastenerRotationDirection direction)
        {
            if (!TryGetPending(id, out FastenerInstance fastener) || !fastener.Definition.ToolRule.Matches(tool))
                return Failure("Совместите двигатель с подушками и выберите подходящий ключ.");
            bool tighten = (direction == FastenerRotationDirection.Clockwise) ==
                (fastener.Definition.TighteningDirection == FastenerDirection.ClockwiseToTighten);
            var previous = new[] { pending[0].Stage, pending[1].Stage, pending[2].Stage };
            if (!fastener.TryAdvance(tighten)) return Failure("Крепёж достиг предела.");
            int aggregate = pending[0].Stage + pending[1].Stage + pending[2].Stage;
            if (aggregate < 2) return AssemblyOperationResult.Success(
                tighten ? AssemblyOperation.TightenFastener : AssemblyOperation.LoosenFastener,
                "Крепёж наживлён; двигатель ещё не закреплён.");

            // Validate the complete destination mapping before attachment changes
            // the graph. The controller commits these stages BEFORE publishing
            // PartInstalled, so save/physics subscribers never observe a zero-bolt engine.
            try { ResolveTransferTargets(requireOccupied: false); }
            catch { RestorePendingStages(previous); throw; }
            attaching = true;
            try
            {
                AssemblyOperationResult installed = assembly.TryInstallFromHandoff(
                    block.PickupTarget, mount);
                if (!installed.Succeeded || !block.IsInstalled)
                {
                    RestorePendingStages(previous);
                    return installed.Succeeded ? Failure("Опора двигателя не удержалась.") : installed;
                }
                return AssemblyOperationResult.Success(AssemblyOperation.TightenFastener, "Двигатель закреплён на подушках.");
            }
            catch
            {
                if (!block.IsInstalled) RestorePendingStages(previous);
                throw;
            }
            finally { attaching = false; }
        }

        /// <summary>Controller-only pre-publication hook; ordinary install operations are unchanged.</summary>
        public void CommitPendingToInstalled(MountPointRuntime runtime)
        {
            if (!attaching) return;
            EnsureInitialized();
            FastenerInstance[] destinations = ResolveTransferTargets(requireOccupied: true);
            if (runtime == null || runtime != assembly.Graph.FindMountForPart(block))
                throw new InvalidOperationException("Engine docking commit used another runtime mount.");
            // TryRestore cannot fail after the complete definition/stage preflight.
            // No callbacks, tool operations or save reads are issued mid-transfer.
            for (int index = 0; index < 3; index++)
                destinations[index].TryRestore(true, true, pending[index].Stage);
            runtime.ReevaluateFastenerGroup();
            foreach (FastenerInstance state in pending) state.ResetForInstalledPart();
        }

        private FastenerInstance[] ResolveTransferTargets(bool requireOccupied)
        {
            if (!assembly.Graph.TryGetMount(mount.MountId, out MountPointRuntime runtime) ||
                runtime.Authoring != mount || runtime.Fasteners.Length != 3 ||
                (requireOccupied ? runtime.InstalledPart != block : runtime.IsOccupied))
                throw new InvalidOperationException("Engine docking destination is not the validated mount.");
            ValidateGroup(runtime.FastenerGroup.Definition);
            int sum = 0;
            var targets = new FastenerInstance[3];
            for (int index = 0; index < 3; index++)
            {
                if (!runtime.TryGetFastener(fastenerIds[index], out targets[index]) ||
                    targets[index].Definition != pending[index].Definition ||
                    pending[index].Stage < 0 || pending[index].Stage > 2 ||
                    pending[index].Stage > targets[index].Definition.MaximumStage)
                    throw new InvalidOperationException("Engine docking stage transfer is inconsistent.");
                sum += pending[index].Stage;
            }
            if (sum != 2) throw new InvalidOperationException("Engine docking requires the first two aggregate turns.");
            return targets;
        }

        private void ValidateGroup(FastenerGroupDefinition group)
        {
            if (group == null || group.FastenerDefinitionIds.Length != 3 ||
                group.AggregateMaximumTightness != 24 || group.BoltedOnThreshold != 2 || group.BoltedOffThreshold != 0)
                throw new InvalidOperationException("Engine docking needs the reviewed ON2/OFF0 three-bolt group.");
            for (int index = 0; index < 3; index++)
                if (Array.IndexOf(group.FastenerDefinitionIds, fastenerIds[index]) < 0)
                    throw new InvalidOperationException("Engine docking group membership drifted.");
        }

        private void RestorePendingStages(int[] stages)
        {
            for (int index = 0; index < 3; index++) pending[index].TryRestore(true, true, stages[index]);
        }

        private void FixedUpdate() => ReleaseIfUnfastened();

        public bool ReleaseIfUnfastened()
        {
            if (attaching || assembly == null || block == null || !block.IsInstalled) return false;
            EnsureInitialized();
            if (!assembly.Graph.TryGetMount(mount.MountId, out MountPointRuntime runtime) ||
                runtime.InstalledPart != block || runtime.FastenerGroup.IsBolted) return false;
            // Existing external connection rules protect halfshafts/lining/linkage/exhaust.
            return assembly.TryRemoveAtCurrentPose(block).Succeeded;
        }

        public AssemblyEngineDockingSaveDto CaptureSaveData()
        {
            EnsureInitialized();
            return new AssemblyEngineDockingSaveDto { pendingStages = block.IsInstalled
                ? new int[3] : new[] { pending[0].Stage, pending[1].Stage, pending[2].Stage } };
        }

        public void RestoreValidated(AssemblyEngineDockingSaveDto dto)
        {
            EnsureInitialized();
            if (dto != null && (!dto.IsValid || block.IsInstalled && !dto.IsEmpty))
                throw new InvalidOperationException("Invalid pending engine docking state.");
            for (int index = 0; index < 3; index++) pending[index].TryRestore(true, true,
                dto == null ? 0 : dto.pendingStages[index]);
            Array.Clear(withinDockingRange, 0, withinDockingRange.Length);
        }

        private static AssemblyOperationResult Failure(string message) => AssemblyOperationResult.Failure(
            AssemblyOperation.TightenFastener, AssemblyFailureReason.InvalidMount, message);

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
