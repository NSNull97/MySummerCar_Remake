using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    public readonly struct DynamicPartRegistration
    {
        public DynamicPartRegistration(PartInstance part, string itemDefinitionId)
        {
            Part = part;
            ItemDefinitionId = itemDefinitionId;
        }

        public PartInstance Part { get; }
        public string ItemDefinitionId { get; }
    }

    public sealed partial class VehicleAssemblyController
    {
        [SerializeField] private PartDefinition[] dynamicPartDefinitions = Array.Empty<PartDefinition>();
        private readonly Dictionary<string, DynamicPartRegistration> dynamicRegistrations =
            new Dictionary<string, DynamicPartRegistration>(StringComparer.Ordinal);

        public PartInstance[] AllRuntimeParts => Graph.AllRuntimeParts;

        public void ConfigureDynamicPartDefinitions(PartDefinition[] definitions)
        {
            var candidate = definitions ?? Array.Empty<PartDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PartDefinition definition in candidate)
                if (definition == null || string.IsNullOrWhiteSpace(definition.DefinitionId) ||
                    !ids.Add(definition.DefinitionId))
                    throw new ArgumentException("Dynamic definitions must have unique nonempty identities.", nameof(definitions));
            foreach (DynamicPartRegistration registration in dynamicRegistrations.Values)
                if (!candidate.Contains(registration.Part.Definition))
                    throw new InvalidOperationException("Cannot discard a registered dynamic part definition.");
            dynamicPartDefinitions = (PartDefinition[])candidate.Clone();
        }

        public bool TryGetDynamicPartDefinition(string definitionId, out PartDefinition definition)
        {
            foreach (PartDefinition candidate in dynamicPartDefinitions ?? Array.Empty<PartDefinition>())
                if (candidate != null && string.Equals(candidate.DefinitionId, definitionId, StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            definition = null;
            return false;
        }

        public bool TryRegisterDynamicPart(PartInstance part, string itemDefinitionId, out string failure)
        {
            EnsureInitialized();
            if (!ValidateDynamicRegistration(new DynamicPartRegistration(part, itemDefinitionId), out failure))
                return false;
            if (part.IsInstalled || !graph.TryRegisterDynamicPart(part, out failure))
            {
                failure = string.IsNullOrEmpty(failure) ? "A new registration must be a loose part." : failure;
                return false;
            }
            dynamicRegistrations.Add(part.StableId.Value, new DynamicPartRegistration(part, itemDefinitionId));
            graphMutationCount++;
            return true;
        }

        public bool TryUnregisterDynamicPart(PartInstance part, out string failure)
        {
            EnsureInitialized();
            if (part == null || !dynamicRegistrations.TryGetValue(part.StableId.Value, out DynamicPartRegistration entry) ||
                entry.Part != part || !graph.TryUnregisterDynamicPart(part, out failure))
            {
                failure = "Only this aggregate's registered loose dynamic part can be removed.";
                return false;
            }
            dynamicRegistrations.Remove(part.StableId.Value);
            graphMutationCount++;
            failure = string.Empty;
            return true;
        }

        public DynamicPartRegistration[] CaptureDynamicPartRegistrations() =>
            dynamicRegistrations.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToArray();

        /// <summary>
        /// Restore-transaction seam, not a gameplay detach operation. The caller
        /// keeps all wrappers alive and restores this checkpoint before vehicle
        /// rollback. Every candidate is validated before any occupancy changes.
        /// Existing mount runtime objects and the base roster are never rebuilt.
        /// </summary>
        public bool TrySetDynamicPartRegistrationsForRestore(DynamicPartRegistration[] registrations, out string failure)
        {
            EnsureInitialized();
            if (registrations == null)
            {
                failure = "Dynamic registration checkpoint is missing.";
                return false;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (DynamicPartRegistration entry in registrations)
            {
                if (!ValidateDynamicRegistration(entry, out failure)) return false;
                if (!seen.Add(entry.Part.StableId.Value) || parts.Any(part => part != null && part.StableId == entry.Part.StableId))
                {
                    failure = "Dynamic identity collides with the base roster or another registration.";
                    return false;
                }
            }
            CancelPendingInstallTransitionsForRestore();
            AssemblyLooseCompoundPhysics compound = GetComponent<AssemblyLooseCompoundPhysics>();
            foreach (DynamicPartRegistration existing in dynamicRegistrations.Values)
            {
                if (registrations.Any(entry => entry.Part == existing.Part)) continue;
                PartInstance part = existing.Part;
                if (part == null) continue;
                MountPointRuntime mount = graph.FindMountForPart(part);
                mount?.Release();
                part.Detach(loosePartsRoot, part.transform.position, part.transform.rotation);
                // Transaction membership and its runtime-only shape registry
                // must leave together. Retained item wrappers may return during
                // rollback; the item bridge then recreates only their bindings.
                // Keeping the old entries makes the first reconciled purchase
                // validate against the other now-unregistered old parts.
                if (compound != null && compound.HasRuntimeBinding(part) &&
                    !compound.TryUnregisterRuntimeBinding(part, out failure)) return false;
            }
            dynamicRegistrations.Clear();
            foreach (DynamicPartRegistration entry in registrations)
                dynamicRegistrations.Add(entry.Part.StableId.Value, entry);
            graph.SetDynamicPartsForRestore(registrations.Select(entry => entry.Part).ToArray());
            graphMutationCount++;
            failure = string.Empty;
            return true;
        }

        private bool ValidateDynamicRegistration(DynamicPartRegistration entry, out string failure)
        {
            PartInstance part = entry.Part;
            if (part == null || !part.StableId.IsValid || part.Definition == null || part.IsAssemblyRoot ||
                string.IsNullOrWhiteSpace(entry.ItemDefinitionId) ||
                !TryGetDynamicPartDefinition(part.Definition.DefinitionId, out PartDefinition allowed) || allowed != part.Definition)
            {
                failure = "Dynamic part identity or allowed definition is invalid.";
                return false;
            }
            failure = string.Empty;
            return true;
        }

        private static PartSaveDto CapturePart(PartInstance part)
        {
            part.RefreshLoosePose();
            PartRuntimeState state = part.RuntimeState;
            var alignment = part.GetComponent<AssemblySteeringAlignmentState>();
            var timing = part.GetComponent<AssemblyCamshaftTimingState>();
            var adjustment = part.GetComponent<AssemblyEngineAdjustmentState>();
            var docking = part.GetComponent<AssemblyEngineDockingState>();
            adjustment?.RefreshPresentation();
            return new PartSaveDto
            {
                stableEntityId = state.StableEntityId, partDefinitionId = state.PartDefinitionId,
                lifecycleState = state.LifecycleState, installedMountId = state.InstalledMountId,
                worldPosition = part.transform.position, worldRotation = part.transform.rotation,
                hasSteeringAlignment = alignment != null, steeringAlignment = alignment?.CaptureSaveData(),
                hasCamshaftTiming = timing != null, camshaftTiming = timing?.CaptureSaveData(),
                hasEngineAdjustment = adjustment != null, engineAdjustment = adjustment?.CaptureSaveData(),
                hasEngineDocking = docking != null, engineDocking = docking?.CaptureSaveData(),
            };
        }

        private DynamicAssemblyPartSaveDto[] CaptureDynamicParts()
        {
            return CaptureDynamicPartRegistrations().Select(entry => new DynamicAssemblyPartSaveDto
            {
                itemDefinitionId = entry.ItemDefinitionId,
                part = CapturePart(entry.Part),
                linearVelocity = entry.Part.Body != null && !entry.Part.Body.isKinematic ? entry.Part.Body.linearVelocity : Vector3.zero,
                angularVelocity = entry.Part.Body != null && !entry.Part.Body.isKinematic ? entry.Part.Body.angularVelocity : Vector3.zero,
                sleeping = entry.Part.Body != null && entry.Part.Body.IsSleeping(),
            }).ToArray();
        }

        private static PartSaveDto[] AllSavedParts(VehicleAssemblySaveData data) =>
            data.parts.Concat((data.dynamicParts ?? Array.Empty<DynamicAssemblyPartSaveDto>()).Select(entry => entry?.part)).ToArray();

        private AssemblyOperationResult ValidateDynamicDescriptors(VehicleAssemblySaveData data)
        {
            var seen = new HashSet<string>(data.parts.Where(part => part != null).Select(part => part.stableEntityId), StringComparer.Ordinal);
            foreach (DynamicAssemblyPartSaveDto entry in data.dynamicParts ?? Array.Empty<DynamicAssemblyPartSaveDto>())
            {
                PartSaveDto dto = entry?.part;
                if (dto == null || !StableEntityId.TryParse(dto.stableEntityId, out _) || !seen.Add(dto.stableEntityId) ||
                    string.IsNullOrWhiteSpace(entry.itemDefinitionId) || dto.lifecycleState == PartLifecycleState.AssemblyRoot ||
                    !TryGetDynamicPartDefinition(dto.partDefinitionId, out _) || !IsFinite(entry.linearVelocity) || !IsFinite(entry.angularVelocity))
                    return InvalidSave("Invalid dynamic part descriptor or duplicate identity.");
                if (dto.hasSteeringAlignment || dto.hasCamshaftTiming || dto.hasEngineDocking ||
                    dto.hasMechanicalCondition || dto.hasValveAdjustment || dto.hasServiceCaps)
                    return InvalidSave("Unsupported optional state on a dynamic consumable.");
                if (dto.hasEngineAdjustment && (dto.partDefinitionId != SatsumaEngineAdjustmentRules.PartId(SatsumaEngineAdjustmentKind.OilFilter) ||
                    dto.engineAdjustment == null || !dto.engineAdjustment.IsValidFor(SatsumaEngineAdjustmentKind.OilFilter) ||
                    dto.lifecycleState != PartLifecycleState.Installed && dto.engineAdjustment.value != 0f))
                    return InvalidSave("Invalid dynamic oil filter adjustment.");
            }
            return AssemblyOperationResult.Success(AssemblyOperation.Restore, "Dynamic descriptors are valid.");
        }
    }
}
