using System;
using System.Collections.Generic;
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

        private AssemblyGraph graph;
        private VehicleAssemblyQuery query;
        private bool initialized;
        private int operationCount;
        private int candidateQueryCount;
        private int graphMutationCount;

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

        public AssemblyOperationResult LastOperationResult { get; private set; }

        public void Configure(
            PartInstance[] registeredParts,
            MountPointAuthoring[] registeredMounts,
            AssemblyDependency[] registeredDependencies,
            ToolDefinition[] registeredTools,
            Transform detachedPartsRoot)
        {
            parts = registeredParts ?? Array.Empty<PartInstance>();
            mountPoints = registeredMounts ?? Array.Empty<MountPointAuthoring>();
            dependencies = registeredDependencies ?? Array.Empty<AssemblyDependency>();
            tools = registeredTools ?? Array.Empty<ToolDefinition>();
            loosePartsRoot = detachedPartsRoot;
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
                part.InstallAt(mount.Authoring.Pose, mount.MountId);
                graphMutationCount++;
                LastOperationResult = AssemblyOperationResult.Success(
                    AssemblyOperation.Install,
                    "Деталь установлена без затяжки: " + part.Definition.DisplayName);
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

            graphMutationCount++;
            LastOperationResult = AssemblyOperationResult.Success(
                operation,
                $"{fastener.Definition.DisplayName}: {fastener.Stage}/{fastener.Definition.MaximumStage}");
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

            graphMutationCount++;
            LastOperationResult = AssemblyOperationResult.Success(
                AssemblyOperation.InsertFastener,
                "Крепёж вставлен и ещё не посажен.");
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

            graphMutationCount++;
            LastOperationResult = AssemblyOperationResult.Success(
                AssemblyOperation.RemoveFastener,
                "Крепёж вынут.");
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

            for (int i = 0; i < mount.Fasteners.Length; i++)
            {
                FastenerInstance fastener = mount.Fasteners[i];
                if (fastener.IsRemovalSecured)
                {
                    return AssemblyOperationResult.Failure(
                        AssemblyOperation.Remove,
                        AssemblyFailureReason.FastenerSecured,
                        "Сначала полностью ослабьте крепёж: " + fastener.Definition.DisplayName);
                }
            }

            if (graph.HasInstalledRemovalBlocker(part.Definition, out string blockerPartId))
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
            graphMutationCount++;
            LastOperationResult = AssemblyOperationResult.Success(
                AssemblyOperation.Remove,
                "Деталь снята: " + part.Definition.DisplayName);
            return LastOperationResult;
        }

        public VehicleAssemblySaveData CaptureSaveData()
        {
            EnsureInitialized();
            var data = new VehicleAssemblySaveData
            {
                parts = new PartSaveDto[parts.Length],
                mounts = new MountSaveDto[graph.Mounts.Length]
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
                data.parts[i] = new PartSaveDto
                {
                    stableEntityId = state.StableEntityId,
                    partDefinitionId = state.PartDefinitionId,
                    lifecycleState = state.LifecycleState,
                    installedMountId = state.InstalledMountId,
                    worldPosition = part.transform.position,
                    worldRotation = part.transform.rotation
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
            AssemblyOperationResult validation = ValidateSaveData(data);
            if (!validation.Succeeded)
            {
                LastOperationResult = validation;
                return validation;
            }

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
                part.InstallAt(mount.Authoring.Pose, mount.MountId);
            }

            for (int i = 0; i < data.fasteners.Length; i++)
            {
                FastenerSaveDto dto = data.fasteners[i];
                graph.TryGetMount(dto.mountId, out MountPointRuntime mount);
                mount.TryGetFastener(dto.fastenerDefinitionId, out FastenerInstance fastener);
                fastener.TryRestore(dto.inserted, dto.seated, dto.stage);
            }

            graphMutationCount++;
            LastOperationResult = AssemblyOperationResult.Success(
                AssemblyOperation.Restore,
                "Состояние сборки восстановлено.");
            return LastOperationResult;
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
                    part.InstallAt(mount.Authoring.Pose, mount.MountId);
                }
            }
        }

        private AssemblyOperationResult ValidateSaveData(VehicleAssemblySaveData data)
        {
            if (data == null || !data.HasSupportedSchema || data.parts == null ||
                data.mounts == null || data.fasteners == null || data.parts.Length != parts.Length ||
                data.mounts.Length != graph.Mounts.Length ||
                data.fasteners.Length != CountRegisteredFasteners())
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Restore,
                    AssemblyFailureReason.InvalidSaveData,
                    "Версия или размер save DTO не соответствует сборке.");
            }

            var seenParts = new HashSet<string>(StringComparer.Ordinal);
            var occupiedMountIds = new HashSet<string>(StringComparer.Ordinal);
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

                if (dto.lifecycleState == PartLifecycleState.Installed)
                {
                    if (!occupiedMountIds.Add(dto.installedMountId) ||
                        !graph.TryGetMount(dto.installedMountId, out MountPointRuntime mount) ||
                        !part.Definition.IsCompatibleWith(mount.Definition))
                    {
                        return InvalidSave("Некорректная или повторно занятая точка установки в DTO.");
                    }
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
            for (int i = 0; i < data.fasteners.Length; i++)
            {
                FastenerSaveDto dto = data.fasteners[i];
                string key = dto == null ? string.Empty : dto.mountId + "/" + dto.fastenerDefinitionId;
                if (dto == null || !seenFasteners.Add(key) ||
                    !graph.TryGetMount(dto.mountId, out MountPointRuntime mount) ||
                    !mount.TryGetFastener(dto.fastenerDefinitionId, out FastenerInstance fastener) ||
                    dto.stage < 0 || dto.stage > fastener.Definition.MaximumStage ||
                    (!dto.inserted && (dto.seated || dto.stage != 0)) ||
                    (dto.inserted && !dto.seated && dto.stage != 0))
                {
                    return InvalidSave("Некорректное состояние крепежа в DTO.");
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
