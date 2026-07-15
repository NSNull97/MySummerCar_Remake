using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    public readonly struct AssemblyMountCandidate
    {
        public AssemblyMountCandidate(
            MountPointRuntime mount,
            AssemblyOperationResult result,
            float distanceMeters,
            float angleDegrees)
        {
            Mount = mount;
            Result = result;
            DistanceMeters = distanceMeters;
            AngleDegrees = angleDegrees;
        }

        public MountPointRuntime Mount { get; }

        public AssemblyOperationResult Result { get; }

        public float DistanceMeters { get; }

        public float AngleDegrees { get; }

        public bool IsValid => Mount != null && Result.Succeeded;
    }

    public sealed class VehicleAssemblyQuery
    {
        private readonly AssemblyGraph graph;

        public VehicleAssemblyQuery(AssemblyGraph assemblyGraph)
        {
            graph = assemblyGraph ?? throw new ArgumentNullException(nameof(assemblyGraph));
        }

        public AssemblyOperationResult EvaluateInstall(PartInstance part, MountPointRuntime mount)
        {
            if (part == null || part.Definition == null || !part.StableId.IsValid)
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.InvalidPart,
                    "Деталь не имеет определения или стабильного ID.");
            }

            if (mount == null || mount.Definition == null || mount.Authoring.Pose == null ||
                string.IsNullOrEmpty(mount.MountId))
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.InvalidMount,
                    "Точка установки не настроена.");
            }

            if (part.IsInstalled)
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.AlreadyInstalled,
                    "Деталь уже установлена.");
            }

            if (mount.IsOccupied)
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.MountOccupied,
                    "Точка установки уже занята.");
            }

            if (!part.Definition.IsCompatibleWith(mount.Definition))
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.Incompatible,
                    "Деталь несовместима с этой точкой установки.");
            }

            if (!string.IsNullOrEmpty(mount.Definition.OwnerPartDefinitionId) &&
                !graph.IsPartDefinitionInstalled(mount.Definition.OwnerPartDefinitionId))
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.MissingPrerequisite,
                    "Сначала установите владельца точки.");
            }

            if (!graph.AreInstallPrerequisitesMet(part.Definition, out string missingPartId))
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.MissingPrerequisite,
                    "Не установлена обязательная деталь.");
            }

            float distance = Vector3.Distance(part.transform.position, mount.Authoring.Pose.position);
            if (distance > mount.Definition.Constraint.PositionToleranceMeters)
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.OutsidePositionTolerance,
                    "Поднесите деталь ближе.");
            }

            float angle = Quaternion.Angle(part.transform.rotation, mount.Authoring.Pose.rotation);
            if (angle > mount.Definition.Constraint.AngularToleranceDegrees)
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.OutsideAngularTolerance,
                    "Совместите ориентацию.");
            }

            if (mount.Authoring.IsObstructed(part))
            {
                return AssemblyOperationResult.Failure(
                    AssemblyOperation.Install,
                    AssemblyFailureReason.Obstructed,
                    "Доступ к точке установки перекрыт.");
            }

            return AssemblyOperationResult.Success(
                AssemblyOperation.Install,
                "Установить деталь.");
        }

        public AssemblyMountCandidate FindBestMount(PartInstance part, bool includeInvalid)
        {
            MountPointRuntime bestMount = null;
            AssemblyOperationResult bestResult = default;
            float bestDistance = float.PositiveInfinity;
            float bestAngle = float.PositiveInfinity;

            MountPointRuntime[] mounts = graph.Mounts;
            for (int i = 0; i < mounts.Length; i++)
            {
                MountPointRuntime mount = mounts[i];
                if (mount == null || mount.Definition == null || mount.Authoring.Pose == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(part.transform.position, mount.Authoring.Pose.position);
                if (distance > mount.Definition.Constraint.PreviewDistanceMeters)
                {
                    continue;
                }

                AssemblyOperationResult result = EvaluateInstall(part, mount);
                if (!includeInvalid && !result.Succeeded)
                {
                    continue;
                }

                float angle = Quaternion.Angle(part.transform.rotation, mount.Authoring.Pose.rotation);
                if (bestMount == null || result.Succeeded && !bestResult.Succeeded ||
                    result.Succeeded == bestResult.Succeeded &&
                    (distance < bestDistance - 0.0001f ||
                     Mathf.Abs(distance - bestDistance) <= 0.0001f && string.CompareOrdinal(
                         mount.MountId,
                         bestMount.MountId) < 0))
                {
                    bestMount = mount;
                    bestResult = result;
                    bestDistance = distance;
                    bestAngle = angle;
                }
            }

            return new AssemblyMountCandidate(bestMount, bestResult, bestDistance, bestAngle);
        }

        public IReadOnlyList<PartInstance> GetMissingParts()
        {
            var missing = new List<PartInstance>();
            PartInstance[] parts = graph.Parts;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null && !parts[i].IsAssemblyRoot && !parts[i].IsInstalled)
                {
                    missing.Add(parts[i]);
                }
            }

            return missing;
        }

        public IReadOnlyList<PartInstance> GetUnsecuredParts()
        {
            var unsecured = new List<PartInstance>();
            MountPointRuntime[] mounts = graph.Mounts;
            for (int i = 0; i < mounts.Length; i++)
            {
                MountPointRuntime mount = mounts[i];
                if (mount == null || !mount.IsOccupied)
                {
                    continue;
                }

                for (int fastenerIndex = 0; fastenerIndex < mount.Fasteners.Length; fastenerIndex++)
                {
                    FastenerInstance fastener = mount.Fasteners[fastenerIndex];
                    if (fastener.Definition != null && fastener.Definition.RequiredForRemoval &&
                        fastener.State != FastenerState.Tightened)
                    {
                        unsecured.Add(mount.InstalledPart);
                        break;
                    }
                }
            }

            return unsecured;
        }

        public float GetCompleteness01()
        {
            return graph.CalculateCompleteness01();
        }

        public bool IsConnectionPointReady(string mountId)
        {
            return graph.TryGetMount(mountId, out MountPointRuntime mount) &&
                mount.IsOccupied &&
                mount.InstalledPart != null;
        }
    }
}
