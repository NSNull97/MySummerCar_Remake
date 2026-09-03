using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Characters;

namespace MSC.NPC
{
    [Serializable]
    public sealed class NpcStateDto
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public CharacterInstanceSnapshot[] characters =
            Array.Empty<CharacterInstanceSnapshot>();

        public static NpcStateDto FromInstances(
            IEnumerable<CharacterInstance> instances)
        {
            var dto = new NpcStateDto
            {
                characters = (instances ?? Enumerable.Empty<CharacterInstance>())
                    .OrderBy(
                        instance => instance.Definition.DefinitionId,
                        StringComparer.Ordinal)
                    .Select(instance => instance.CaptureSnapshot())
                    .ToArray(),
            };
            return dto;
        }

        public NpcStateDto DeepClone() => new NpcStateDto
        {
            schemaVersion = schemaVersion,
            characters = (characters ?? Array.Empty<CharacterInstanceSnapshot>())
                .Select(snapshot => snapshot?.DeepClone())
                .ToArray(),
        };

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure = $"Unsupported NPC-state schema {schemaVersion}.";
                return false;
            }

            CharacterInstanceSnapshot[] configured =
                characters ?? Array.Empty<CharacterInstanceSnapshot>();
            if (configured.Length > 512)
            {
                failure = "NPC-state payload exceeds the supported roster size.";
                return false;
            }

            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterInstanceSnapshot snapshot in configured)
            {
                if (snapshot == null)
                {
                    failure = "NPC-state payload contains a null character snapshot.";
                    return false;
                }

                if (!CharacterStableId.TryValidate(
                        snapshot.definitionId,
                        "character.",
                        out failure) ||
                    !MSC.Core.Identity.StableEntityId.TryParse(
                        snapshot.stableInstanceId,
                        out _) ||
                    !definitionIds.Add(snapshot.definitionId) ||
                    !instanceIds.Add(snapshot.stableInstanceId))
                {
                    failure = string.IsNullOrEmpty(failure)
                        ? "NPC-state payload contains null, duplicate or invalid identity data."
                        : failure;
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }
    }
}
