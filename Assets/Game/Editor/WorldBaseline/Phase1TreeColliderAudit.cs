using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.LegacyImport;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    public static class Phase1TreeColliderAudit
    {
        private const string ForestRootName =
            "PHASE1_VEGETATION_REPLACEMENT_LICENSED_THIRD_PARTY";
        private const string ReportPath =
            "Artifacts/VegetationImport/TreeColliderAudit.csv";
        private const string ApprovedMapBoundaryColliderPath =
            "MAP/MESH/FOLIAGE/TREEWALL_LOW/treewallcoll";

        [MenuItem(
            "Tools/MSC Remake/World Baseline/Audit Invisible Tree Colliders")]
        public static void AuditFromMenu()
        {
            Audit();
        }

        public static void RunBatch()
        {
            Audit();
        }

        public static void Audit()
        {
            Scene scene = EditorSceneManager.OpenScene(
                WorldBaseline06B2Paths.GlobalScene,
                OpenSceneMode.Single);
            GameObject[] sceneRoots = scene.GetRootGameObjects();
            GameObject forestRoot = sceneRoots
                .SelectMany(root =>
                    root.GetComponentsInChildren<Transform>(true))
                .Select(item => item.gameObject)
                .FirstOrDefault(item =>
                    string.Equals(
                        item.name,
                        ForestRootName,
                        StringComparison.Ordinal));
            if (forestRoot == null)
            {
                throw new InvalidDataException(
                    "Generated Phase 1 forest root is missing.");
            }

            int generatedTreeCount = 0;
            int generatedColliderCount = 0;
            int generatedInvisibleColliderCount = 0;
            var auditRows = new List<AuditRow>();
            Transform[] generatedTrees = forestRoot
                .GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.StartsWith(
                    "Tree_",
                    StringComparison.Ordinal))
                .ToArray();
            foreach (Transform tree in generatedTrees)
            {
                generatedTreeCount++;
                bool hasRenderableLod = HasRenderableLod(tree.gameObject);
                Collider[] colliders = tree
                    .GetComponentsInChildren<Collider>(true)
                    .Where(collider => collider.enabled)
                    .ToArray();
                generatedColliderCount += colliders.Length;
                if (hasRenderableLod)
                {
                    continue;
                }

                generatedInvisibleColliderCount += colliders.Length;
                foreach (Collider collider in colliders)
                {
                    auditRows.Add(
                        AuditRow.For(
                            "GeneratedTreeWithoutRenderableLOD",
                            tree.name,
                            collider));
                }
            }

            int legacyVegetationColliderCount = 0;
            int legacyInvisibleInteriorColliderCount = 0;
            int approvedMapBoundaryColliderCount = 0;
            DonorWorldBaselineEntityMetadata[] metadata = sceneRoots
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        DonorWorldBaselineEntityMetadata>(true))
                .ToArray();
            foreach (DonorWorldBaselineEntityMetadata item in metadata)
            {
                string path = item.SourceHierarchyPath ?? string.Empty;
                if (!IsLegacyVegetation(item, path))
                {
                    continue;
                }

                bool hasEnabledRenderer = item
                    .GetComponentsInChildren<Renderer>(true)
                    .Any(renderer =>
                        renderer.enabled &&
                        renderer.gameObject.activeInHierarchy);
                foreach (Collider collider in item
                    .GetComponentsInChildren<Collider>(true)
                    .Where(collider =>
                        collider.enabled &&
                        collider.gameObject.activeInHierarchy &&
                        !collider.transform.IsChildOf(
                            forestRoot.transform)))
                {
                    legacyVegetationColliderCount++;
                    if (IsApprovedMapBoundaryCollider(item, collider))
                    {
                        approvedMapBoundaryColliderCount++;
                        auditRows.Add(
                            AuditRow.For(
                                "ApprovedInvisibleMapBoundary",
                                path,
                                collider));
                        continue;
                    }
                    if (hasEnabledRenderer)
                    {
                        continue;
                    }

                    legacyInvisibleInteriorColliderCount++;
                    auditRows.Add(
                        AuditRow.For(
                            "HiddenLegacyInteriorVegetation",
                            path,
                            collider));
                }
            }

            WriteReport(auditRows);
            if (generatedColliderCount != generatedTreeCount ||
                generatedInvisibleColliderCount != 0 ||
                legacyInvisibleInteriorColliderCount != 0 ||
                approvedMapBoundaryColliderCount != 1)
            {
                throw new InvalidDataException(
                    "Tree collider audit failed. See " + ReportPath + ".");
            }
            Debug.Log(
                "PHASE1_TREE_COLLIDER_AUDIT_OK " +
                $"generatedTrees={generatedTreeCount} " +
                $"generatedColliders={generatedColliderCount} " +
                $"generatedInvisibleColliders=" +
                $"{generatedInvisibleColliderCount} " +
                $"legacyVegetationColliders=" +
                $"{legacyVegetationColliderCount} " +
                $"legacyInvisibleInteriorColliders=" +
                $"{legacyInvisibleInteriorColliderCount} " +
                $"approvedMapBoundaryColliders=" +
                $"{approvedMapBoundaryColliderCount} " +
                $"report='{ReportPath}'");
        }

        private static bool IsApprovedMapBoundaryCollider(
            DonorWorldBaselineEntityMetadata metadata,
            Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            DonorWorldBaselineEntityMetadata owner =
                collider.GetComponentInParent<
                    DonorWorldBaselineEntityMetadata>();
            string ownerPath = owner != null
                ? owner.SourceHierarchyPath ?? string.Empty
                : metadata != null
                    ? metadata.SourceHierarchyPath ?? string.Empty
                    : string.Empty;
            ownerPath = ownerPath.Replace('\\', '/').TrimEnd('/');
            if (ownerPath.EndsWith(
                    ApprovedMapBoundaryColliderPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            const string boundaryParentPath =
                "MAP/MESH/FOLIAGE/TREEWALL_LOW";
            return ownerPath.EndsWith(
                       boundaryParentPath,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       collider.name,
                       "treewallcoll",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasRenderableLod(GameObject tree)
        {
            LODGroup[] groups = tree.GetComponentsInChildren<LODGroup>(true);
            if (groups.Length == 0)
            {
                return false;
            }

            foreach (LODGroup group in groups)
            {
                foreach (LOD lod in group.GetLODs())
                {
                    foreach (Renderer renderer in lod.renderers)
                    {
                        if (renderer == null || !renderer.enabled)
                        {
                            continue;
                        }
                        Material[] materials = renderer.sharedMaterials;
                        if (materials.Length > 0 && materials.All(material =>
                                material != null &&
                                material.shader != null &&
                                material.shader.isSupported))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool IsLegacyVegetation(
            DonorWorldBaselineEntityMetadata metadata,
            string path)
        {
            return path.IndexOf(
                       "TREEWALL_",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf(
                       "/FOLIAGE/TREES",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf(
                       "/FOLIAGE/BUSHES",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   string.Equals(
                       metadata.SemanticCategory,
                       "VegetationTree",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       metadata.SemanticCategory,
                       "VegetationBush",
                       StringComparison.Ordinal);
        }

        private static void WriteReport(
            IReadOnlyCollection<AuditRow> suspicious)
        {
            string absolutePath = Path.GetFullPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            var output = new StringBuilder();
            output.AppendLine(
                "Classification,Source,ColliderType,CenterX,CenterY," +
                "CenterZ,SizeX,SizeY,SizeZ");
            foreach (AuditRow row in suspicious
                .OrderBy(item => item.Classification, StringComparer.Ordinal)
                .ThenBy(item => item.Source, StringComparer.Ordinal)
                .ThenBy(item => item.Center.x)
                .ThenBy(item => item.Center.z))
            {
                output.Append(Escape(row.Classification)).Append(',')
                    .Append(Escape(row.Source)).Append(',')
                    .Append(Escape(row.ColliderType)).Append(',')
                    .Append(Format(row.Center.x)).Append(',')
                    .Append(Format(row.Center.y)).Append(',')
                    .Append(Format(row.Center.z)).Append(',')
                    .Append(Format(row.Size.x)).Append(',')
                    .Append(Format(row.Size.y)).Append(',')
                    .Append(Format(row.Size.z)).AppendLine();
            }
            File.WriteAllText(absolutePath, output.ToString());
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            return '"' + (value ?? string.Empty).Replace("\"", "\"\"") + '"';
        }

        private readonly struct AuditRow
        {
            public string Classification { get; }
            public string Source { get; }
            public string ColliderType { get; }
            public Vector3 Center { get; }
            public Vector3 Size { get; }

            private AuditRow(
                string classification,
                string source,
                string colliderType,
                Vector3 center,
                Vector3 size)
            {
                Classification = classification;
                Source = source;
                ColliderType = colliderType;
                Center = center;
                Size = size;
            }

            public static AuditRow For(
                string classification,
                string source,
                Collider collider)
            {
                Bounds bounds = collider.bounds;
                return new AuditRow(
                    classification,
                    source,
                    collider.GetType().Name,
                    bounds.center,
                    bounds.size);
            }
        }
    }
}
