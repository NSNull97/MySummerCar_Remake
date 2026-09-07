using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Migration = MSC.Vehicle.Assembly.SatsumaRockerCoverFastenerMigration;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Bounded presentation binding and exact stock/GT cover dedupe; no graph initialization.</summary>
    public static class Phase1SatsumaEngineCapAndCoverAuthoring
    {
        internal const string CapMeshSourceGuid = "f48a6a1b2ea4b164d9d920ab78966d86";
        internal const string ShortBoltMeshSourceGuid = "aec6c756751308a4d830708366ad5cdb";
        internal static readonly string[] PistonIds =
        {
            "vehicle.satsuma.part.piston1", "vehicle.satsuma.part.piston2",
            "vehicle.satsuma.part.piston3", "vehicle.satsuma.part.piston4",
        };
        // Canonical stock IDs 2,4,8,9,10,12, shared by the GT alternative.
        internal static readonly Vector3[] CoverMarkerPositions =
        {
            new Vector3(-.13673234f, -.0513382f, -.034412872f),
            new Vector3(-.048354626f, -.0513382f, -.034416568f),
            new Vector3(.12149751f, .062173855f, -.03440928f),
            new Vector3(-.052220702f, .062173855f, -.034412738f),
            new Vector3(-.13894153f, .0621729f, -.034412798f),
            new Vector3(.122877955f, -.0513382f, -.034416866f),
        };
        // Exact reviewed legacy-import form, in the same pair order as above.
        // The source stock/GT marker local positions are identical, but the old
        // referenceWorld.inverse * targetWorld float round trip used the GT spawn
        // near x=1552.808 m. It quantized these aliases by 37-61 micrometres.
        // Recognize that existing form without accepting arbitrary pose drift or
        // moving the retained stock markers. Direct source/coincident poses remain valid.
        private static readonly Vector3[] LegacyImportedGtMarkerPositions =
        {
            new Vector3(-.13671875f, -.0513037f, -.034413148f),
            new Vector3(-.048339844f, -.0513037f, -.034416962f),
            new Vector3(.12145996f, .062221676f, -.034409795f),
            new Vector3(-.052246094f, .062221676f, -.034413133f),
            new Vector3(-.13891602f, .062221676f, -.034413133f),
            new Vector3(.122924805f, -.0513037f, -.034416962f),
        };

        public static int Configure(GameObject root, string generatedRoot)
        {
            if (root == null || string.IsNullOrWhiteSpace(generatedRoot))
                throw new InvalidDataException("Engine cap/cover binding needs an explicit generated root.");
            Mesh capMesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                generatedRoot.TrimEnd('/', '\\') + "/Meshes/" + CapMeshSourceGuid + ".asset");
            Mesh shortBolt = AssetDatabase.LoadAssetAtPath<Mesh>(
                generatedRoot.TrimEnd('/', '\\') + "/Meshes/" + ShortBoltMeshSourceGuid + ".asset");
            return Configure(root.GetComponent<VehicleAssemblyController>(), capMesh, shortBolt);
        }

        internal static int Configure(VehicleAssemblyController assembly, Mesh capMesh, Mesh shortBolt)
        {
            if (Application.isPlaying)
                throw new InvalidDataException("Cover retirement is prefab authoring, not a live graph migration.");
            if (assembly == null || capMesh == null || shortBolt == null)
                throw new InvalidDataException("Missing assembly or reviewed piston-cap/short-bolt mesh.");
            // Resolve the whole packet before modifying any shared definition or object.
            (PartInstance part, GameObject cap)[] caps = ValidateCaps(assembly, capMesh);
            CoverPlan cover = ValidateCover(assembly);
            int changes = 0;
            foreach ((PartInstance part, GameObject cap) in caps)
            {
                SatsumaPistonCapPresenter presenter = part.GetComponent<SatsumaPistonCapPresenter>();
                bool changed = presenter == null || presenter.Piston != part || presenter.CapPresentation != cap;
                bool previousActive = cap.activeSelf;
                if (presenter == null) presenter = part.gameObject.AddComponent<SatsumaPistonCapPresenter>();
                presenter.Configure(part, cap);
                if (changed || previousActive != cap.activeSelf) changes++;
            }
            if (cover.IsLegacy)
            {
                MountPointDefinition definition = cover.Definition;
                FastenerDefinition[] canonical = Migration.CanonicalIds.Select(id =>
                    definition.Fasteners.Single(fastener => fastener.DefinitionId == id)).ToArray();
                definition.Configure(definition.DefinitionId, definition.DisplayName, definition.SocketType,
                    definition.OwnerPartDefinitionId, definition.AcceptedPartDefinitionIds,
                    definition.Constraint, definition.ReferenceCandidateRadiusMeters, canonical);
                var group = new FastenerGroupDefinition();
                group.Configure(Migration.CanonicalIds, 48, 2, 0);
                definition.ConfigureFastenerGroup(group);
                EditorUtility.SetDirty(definition);
                changes++;
                foreach (string retiredId in Migration.RetiredIds)
                {
                    // Only the six validated dedicated marker roots are retired.
                    // Their definition assets remain on disk as reserved aliases.
                    Object.DestroyImmediate(cover.Targets[retiredId].gameObject);
                    changes++;
                }
            }
            foreach (string id in Migration.CanonicalIds)
            {
                MeshFilter filter = GetPresentation(cover.Targets[id]).GetComponent<MeshFilter>();
                if (filter.sharedMesh == shortBolt) continue;
                filter.sharedMesh = shortBolt;
                EditorUtility.SetDirty(filter);
                changes++;
            }
            return changes;
        }

        private static (PartInstance part, GameObject cap)[] ValidateCaps(
            VehicleAssemblyController assembly, Mesh mesh)
        {
            var caps = new (PartInstance part, GameObject cap)[PistonIds.Length];
            for (int index = 0; index < PistonIds.Length; index++)
            {
                PartInstance[] parts = assembly.Parts.Where(part => part != null &&
                    part.Definition != null && part.Definition.DefinitionId == PistonIds[index]).ToArray();
                if (parts.Length != 1 || parts[0].IsAssemblyRoot)
                    throw new InvalidDataException("Expected one non-root piston: " + PistonIds[index]);
                PartInstance piston = parts[0];
                MeshFilter[] matches = piston.GetComponentsInChildren<MeshFilter>(true)
                    .Where(filter => filter.sharedMesh == mesh &&
                        filter.GetComponentInParent<PartInstance>() == piston).ToArray();
                if (matches.Length != 1 || matches[0].gameObject == piston.gameObject ||
                    matches[0].GetComponent<Renderer>() == null ||
                    matches[0].GetComponentsInChildren<Collider>(true).Length != 0 ||
                    matches[0].GetComponentsInChildren<PartInstance>(true).Length != 0)
                    throw new InvalidDataException("Expected one visual-only conrod cap: " + PistonIds[index]);
                SatsumaPistonCapPresenter[] presenters = piston.GetComponentsInChildren<SatsumaPistonCapPresenter>(true);
                if (presenters.Length > 1 || presenters.Length == 1 && presenters[0].gameObject != piston.gameObject)
                    throw new InvalidDataException("Piston cap presenter must live on the piston root.");
                caps[index] = (piston, matches[0].gameObject);
            }
            return caps;
        }

        private static CoverPlan ValidateCover(VehicleAssemblyController assembly)
        {
            MountPointAuthoring[] mounts = assembly.MountPoints.Where(mount =>
                mount != null && mount.Definition != null && mount.MountId == Migration.MountId).ToArray();
            if (mounts.Length != 1) throw new InvalidDataException("Expected one rocker-cover mount.");
            MountPointDefinition definition = mounts[0].Definition;
            string[] accepted = definition.AcceptedPartDefinitionIds;
            if (definition.OwnerPartDefinitionId != "vehicle.satsuma.part.cylinder-head" ||
                accepted.Length != 2 || !accepted.Contains("vehicle.satsuma.part.rocker-cover") ||
                !accepted.Contains("vehicle.satsuma.part.gt-rocker-cover-gt") ||
                definition.Fasteners.Any(fastener => fastener == null ||
                    fastener.Size != FastenerSize.Millimeter7 || fastener.MaximumStage != 8 ||
                    !fastener.RequiredForRemoval || !fastener.InsertedOnInstall ||
                    fastener.TighteningDirection != FastenerDirection.ClockwiseToTighten ||
                    fastener.ToolRule.ToolType != "Wrench" ||
                    fastener.ToolRule.FastenerSize != FastenerSize.Millimeter7))
                throw new InvalidDataException("Rocker-cover owner, alternatives or fastener contract drifted.");
            string[] ids = definition.Fasteners.Select(fastener => fastener.DefinitionId).ToArray();
            bool legacy = Migration.IsLegacyShape(ids);
            FastenerGroupDefinition group = definition.FastenerGroup;
            if ((!legacy && !Migration.IsCanonicalShape(ids)) || group == null ||
                !(legacy ? Migration.IsLegacyShape(group.FastenerDefinitionIds) :
                    Migration.IsCanonicalShape(group.FastenerDefinitionIds)) ||
                group.AggregateMaximumTightness != (legacy ? 96 : 48) ||
                group.BoltedOnThreshold != (legacy ? 1 : 2) || group.BoltedOffThreshold != 0 ||
                group.SpeedRetentionPolicy != FastenerSpeedRetentionPolicy.None ||
                group.BreakAction != FastenerBreakAction.None)
                throw new InvalidDataException("Expected the exact old12 or new6 rocker-cover group.");
            AssemblyFastenerInteractionTarget[] allTargets = assembly
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Where(target => target.MountId == Migration.MountId).ToArray();
            string[] targetIds = allTargets.Select(target => target.FastenerDefinitionId).ToArray();
            if (!(legacy ? Migration.IsLegacyShape(targetIds) : Migration.IsCanonicalShape(targetIds)) ||
                allTargets.Any(target => target.Controller != assembly ||
                    target.transform.parent != mounts[0].transform ||
                    target.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Length != 1 ||
                    target.GetComponentsInChildren<PartInstance>(true).Length != 0))
                throw new InvalidDataException("Cover targets must match its exact dedicated marker roots.");
            var targets = allTargets.ToDictionary(target => target.FastenerDefinitionId, StringComparer.Ordinal);
            string[] stockIds = Migration.CanonicalIds;
            string[] retiredIds = Migration.RetiredIds;
            for (int index = 0; index < stockIds.Length; index++)
            {
                AssemblyFastenerInteractionTarget stock = targets[stockIds[index]];
                if (Vector3.Distance(stock.transform.localPosition, CoverMarkerPositions[index]) > .00001f)
                    throw new InvalidDataException("Cover marker pose changed: " + stockIds[index]);
                ValidatePresentation(stock);
                if (!legacy) continue;
                AssemblyFastenerInteractionTarget retired = targets[retiredIds[index]];
                ValidatePresentation(retired);
                bool coincidentSourceForm = Vector3.Distance(stock.transform.localPosition,
                    retired.transform.localPosition) <= .00001f;
                bool reviewedLegacyImportForm = Vector3.Distance(LegacyImportedGtMarkerPositions[index],
                    retired.transform.localPosition) <= .000001f;
                if ((!coincidentSourceForm && !reviewedLegacyImportForm) ||
                    Quaternion.Angle(stock.transform.localRotation, retired.transform.localRotation) > .001f)
                    throw new InvalidDataException("Stock/GT cover aliases no longer coincide: " + retiredIds[index]);
            }
            return new CoverPlan { Definition = definition, Targets = targets, IsLegacy = legacy };
        }

        private static Transform GetPresentation(AssemblyFastenerInteractionTarget target) =>
            new SerializedObject(target).FindProperty("fastenerPresentation").objectReferenceValue as Transform;

        private static void ValidatePresentation(AssemblyFastenerInteractionTarget target)
        {
            Transform presentation = GetPresentation(target);
            if (presentation == null || presentation == target.transform ||
                presentation.parent != target.transform || presentation.GetComponent<MeshFilter>() == null ||
                presentation.GetComponent<Renderer>() == null)
                throw new InvalidDataException("Cover fastener needs its dedicated child presentation.");
        }

        private sealed class CoverPlan
        {
            public MountPointDefinition Definition;
            public Dictionary<string, AssemblyFastenerInteractionTarget> Targets;
            public bool IsLegacy;
        }
    }
}
