using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Editor.WorldTransfer;
using MSC.LegacyImport;
using MSC.World.Data;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    /// <summary>
    /// Bounded Phase 1 remediation for lighting data lost while donor static
    /// batches were split. It only edits the generated RuntimeBaseline copies
    /// and generated streaming scenes; frozen donor/reference inputs remain
    /// read-only and byte-identical.
    /// </summary>
    public static class DonorWorldMaterialLightingRemediator
    {
        internal const string PolicyVersion =
            "08A1-local-material-lighting-remediation-v1";
        private const float MinimumVectorLengthSquared = 0.00000001f;
        private const float PositionToleranceSquared = 0.000001f;

        [MenuItem(
            "Tools/MSC Remake/World Baseline 08A1/" +
            "Rebuild Material + Mesh Lighting Compatibility")]
        public static void RunBatch()
        {
            DonorWorldBaselineManifest
                .AssertCanonicalFrozenInputsMatchRevision();
            DonorWorldBaselineManifest.AssertExistingSourceLockUnchanged();

            DonorWorldCellizationPlan plan =
                DonorWorldCellizationPlan.Load();
            DonorWorldMaterialTexturePlan materialPlan =
                DonorWorldMaterialTexturePlan.Load();
            DonorWorldMaterialTextureAssets presentation =
                DonorWorldMaterialTexturePipeline.Build(materialPlan);

            RepairSummary repair = RepairGeneratedMeshes(plan);
            int rendererCount = ApplyRendererPolicyToGeneratedScenes(
                plan,
                materialPlan);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            Debug.Log(
                "DONOR_WORLD_08A1_MATERIAL_LIGHTING_REMEDIATION_OK " +
                $"policy={PolicyVersion} " +
                $"materials={presentation.TexturedMaterials.Count} " +
                $"meshes={repair.MeshCount} " +
                $"normalsRepaired={repair.NormalsRepaired} " +
                $"tangentsRepaired={repair.TangentsRepaired} " +
                $"rendererPolicies={rendererCount}");
        }

        private static RepairSummary RepairGeneratedMeshes(
            DonorWorldCellizationPlan plan)
        {
            IReadOnlyDictionary<long, int[]> subsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();
            var processedPaths = new HashSet<string>(StringComparer.Ordinal);
            int meshCount = 0;
            int normalsRepaired = 0;
            int tangentsRepaired = 0;

            foreach (DonorWorldCellizationAssignment assignment in
                     plan.Assignments.OrderBy(
                         value => value.SanitationEntry.Placement.StableId,
                         StringComparer.Ordinal))
            {
                WorldBaselineSanitationEntry entry =
                    assignment.SanitationEntry;
                if (!entry.IncludeRenderer)
                {
                    continue;
                }

                bool derived = subsets.TryGetValue(
                    entry.Placement.SourceObjectId,
                    out int[] subMeshIndices);
                string path = derived
                    ? WorldBaselinePaths.DerivedMeshRoot + "/" +
                      entry.Placement.StableId + ".asset"
                    : WorldBaselinePaths.SourceMeshRoot + "/" +
                      entry.Placement.MeshGuid + ".asset";
                if (!processedPaths.Add(path))
                {
                    continue;
                }

                AssertGeneratedMeshPath(path);
                Mesh target = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (target == null || target.vertexCount == 0)
                {
                    throw new InvalidDataException(
                        "Generated donor-world mesh is missing or empty: " +
                        path);
                }
                if (!target.isReadable)
                {
                    throw new InvalidDataException(
                        "Generated donor-world mesh is not readable: " + path);
                }

                bool repairedNormals;
                bool repairedTangents;
                if (derived)
                {
                    RepairDerivedMesh(
                        target,
                        entry.Placement,
                        subMeshIndices,
                        out repairedNormals,
                        out repairedTangents);
                }
                else
                {
                    RepairStandaloneMesh(
                        target,
                        out repairedNormals,
                        out repairedTangents);
                }

                if (repairedNormals || repairedTangents)
                {
                    EditorUtility.SetDirty(target);
                }
                meshCount++;
                normalsRepaired += repairedNormals ? 1 : 0;
                tangentsRepaired += repairedTangents ? 1 : 0;
            }

            AssetDatabase.SaveAssets();
            return new RepairSummary(
                meshCount,
                normalsRepaired,
                tangentsRepaired);
        }

        private static void RepairDerivedMesh(
            Mesh target,
            WorldEntityPlacement placement,
            IReadOnlyList<int> subMeshIndices,
            out bool repairedNormals,
            out bool repairedTangents)
        {
            bool normalsValid = HasValidNormals(target);
            bool tangentsValid = HasValidTangents(target);
            repairedNormals = !normalsValid;
            repairedTangents = !tangentsValid;
            if (normalsValid && tangentsValid)
            {
                return;
            }

            string sourcePath =
                AssetDatabase.GUIDToAssetPath(placement.MeshGuid);
            if (!WorldReferenceMeshLibrarySync.IsBelowReferenceMeshRoot(
                    sourcePath))
            {
                throw new InvalidDataException(
                    "Derived lighting repair resolved outside the audited " +
                    "reference mesh root: " + sourcePath);
            }
            Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath);
            if (source == null)
            {
                throw new InvalidDataException(
                    "Audited source mesh is missing: " +
                    sourcePath);
            }

            SourceMeshSnapshot sourceData = ReadSourceMeshSnapshot(
                source,
                subMeshIndices,
                placement.StableId);
            Vector3[] sourceVertices = sourceData.Vertices;
            Vector3[] sourceNormals = sourceData.Normals;
            Vector4[] sourceTangents = sourceData.Tangents;
            Matrix4x4 positionTransform = Matrix4x4.TRS(
                placement.SourcePosition,
                placement.Rotation,
                placement.Scale).inverse;
            Matrix4x4 normalTransform =
                positionTransform.inverse.transpose;
            float handedness = positionTransform.determinant < 0f
                ? -1f
                : 1f;

            bool canCopyNormals =
                sourceNormals.Length == sourceVertices.Length;
            bool canCopyTangents =
                sourceTangents.Length == sourceVertices.Length;
            var vertexMap = new Dictionary<int, int>();
            var expectedVertices = new List<Vector3>();
            var expectedNormals = new List<Vector3>();
            var expectedTangents = new List<Vector4>();

            foreach (int subMeshIndex in subMeshIndices)
            {
                if (subMeshIndex < 0 || subMeshIndex >= source.subMeshCount)
                {
                    throw new InvalidDataException(
                        "Derived lighting repair contains an invalid source " +
                        $"submesh {subMeshIndex} for {placement.StableId}.");
                }

                int[] sourceIndices = sourceData.IndicesBySubMesh[
                    subMeshIndex];
                foreach (int sourceVertex in sourceIndices)
                {
                    if (sourceVertex < 0 ||
                        sourceVertex >= sourceVertices.Length)
                    {
                        throw new InvalidDataException(
                            "Derived lighting repair contains an invalid " +
                            $"vertex {sourceVertex} for {placement.StableId}.");
                    }
                    if (vertexMap.ContainsKey(sourceVertex))
                    {
                        continue;
                    }

                    vertexMap.Add(sourceVertex, expectedVertices.Count);
                    expectedVertices.Add(positionTransform.MultiplyPoint3x4(
                        sourceVertices[sourceVertex]));

                    Vector3 normal = Vector3.zero;
                    if (canCopyNormals)
                    {
                        normal = normalTransform.MultiplyVector(
                            sourceNormals[sourceVertex]);
                        if (!TryNormalize(ref normal))
                        {
                            canCopyNormals = false;
                        }
                    }
                    expectedNormals.Add(normal);

                    Vector4 sourceTangent = canCopyTangents
                        ? sourceTangents[sourceVertex]
                        : Vector4.zero;
                    Vector3 tangent = positionTransform.MultiplyVector(
                        new Vector3(
                            sourceTangent.x,
                            sourceTangent.y,
                            sourceTangent.z));
                    if (canCopyTangents && canCopyNormals)
                    {
                        tangent -= normal * Vector3.Dot(normal, tangent);
                    }
                    if (canCopyTangents &&
                        (!TryNormalize(ref tangent) ||
                         !float.IsFinite(sourceTangent.w)))
                    {
                        canCopyTangents = false;
                    }
                    expectedTangents.Add(new Vector4(
                        tangent.x,
                        tangent.y,
                        tangent.z,
                        sourceTangent.w * handedness));
                }
            }

            AssertGeometryStillMatches(
                target,
                expectedVertices,
                placement.StableId);

            if (!normalsValid)
            {
                if (canCopyNormals)
                {
                    target.SetNormals(expectedNormals);
                }
                else
                {
                    RequireTriangleTopology(target, placement.StableId);
                    target.RecalculateNormals();
                }
            }

            if (!tangentsValid)
            {
                if (canCopyTangents && canCopyNormals)
                {
                    target.SetTangents(expectedTangents);
                }
                else if (HasCompleteUv0(target) &&
                         HasTriangleTopology(target))
                {
                    target.RecalculateTangents();
                }
                else
                {
                    repairedTangents = false;
                }
            }
        }

        private static void RepairStandaloneMesh(
            Mesh target,
            out bool repairedNormals,
            out bool repairedTangents)
        {
            repairedNormals = !HasValidNormals(target);
            if (repairedNormals)
            {
                RequireTriangleTopology(target, target.name);
                target.RecalculateNormals();
            }

            repairedTangents = !HasValidTangents(target) &&
                               HasCompleteUv0(target) &&
                               HasTriangleTopology(target);
            if (repairedTangents)
            {
                target.RecalculateTangents();
            }
        }

        private static SourceMeshSnapshot ReadSourceMeshSnapshot(
            Mesh source,
            IReadOnlyList<int> subMeshIndices,
            string stableId)
        {
            using Mesh.MeshDataArray meshDataArray =
                MeshUtility.AcquireReadOnlyMeshData(source);
            if (meshDataArray.Length != 1)
            {
                throw new InvalidDataException(
                    "Could not acquire one read-only mesh snapshot for " +
                    stableId + ".");
            }

            Mesh.MeshData meshData = meshDataArray[0];
            var vertices = new NativeArray<Vector3>(
                meshData.vertexCount,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            try
            {
                meshData.GetVertices(vertices);
                Vector3[] managedVertices = vertices.ToArray();

                Vector3[] managedNormals = Array.Empty<Vector3>();
                if (meshData.HasVertexAttribute(VertexAttribute.Normal) &&
                    meshData.GetVertexAttributeDimension(
                        VertexAttribute.Normal) == 3)
                {
                    var normals = new NativeArray<Vector3>(
                        meshData.vertexCount,
                        Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);
                    try
                    {
                        meshData.GetNormals(normals);
                        managedNormals = normals.ToArray();
                    }
                    finally
                    {
                        normals.Dispose();
                    }
                }

                Vector4[] managedTangents = Array.Empty<Vector4>();
                if (meshData.HasVertexAttribute(VertexAttribute.Tangent) &&
                    meshData.GetVertexAttributeDimension(
                        VertexAttribute.Tangent) == 4)
                {
                    var tangents = new NativeArray<Vector4>(
                        meshData.vertexCount,
                        Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);
                    try
                    {
                        meshData.GetTangents(tangents);
                        managedTangents = tangents.ToArray();
                    }
                    finally
                    {
                        tangents.Dispose();
                    }
                }

                var indicesBySubMesh =
                    new Dictionary<int, int[]>();
                foreach (int subMeshIndex in subMeshIndices.Distinct())
                {
                    if (subMeshIndex < 0 ||
                        subMeshIndex >= meshData.subMeshCount)
                    {
                        throw new InvalidDataException(
                            "Read-only source snapshot contains an invalid " +
                            $"submesh {subMeshIndex} for {stableId}.");
                    }

                    SubMeshDescriptor descriptor =
                        meshData.GetSubMesh(subMeshIndex);
                    var indices = new NativeArray<int>(
                        descriptor.indexCount,
                        Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);
                    try
                    {
                        meshData.GetIndices(
                            indices,
                            subMeshIndex,
                            applyBaseVertex: true);
                        indicesBySubMesh.Add(
                            subMeshIndex,
                            indices.ToArray());
                    }
                    finally
                    {
                        indices.Dispose();
                    }
                }

                return new SourceMeshSnapshot(
                    managedVertices,
                    managedNormals,
                    managedTangents,
                    indicesBySubMesh);
            }
            finally
            {
                vertices.Dispose();
            }
        }

        private static int ApplyRendererPolicyToGeneratedScenes(
            DonorWorldCellizationPlan plan,
            DonorWorldMaterialTexturePlan materialPlan)
        {
            IReadOnlyDictionary<string, DonorWorldCellizationAssignment>
                assignmentById = plan.Assignments.ToDictionary(
                    value => value.SanitationEntry.Placement.StableId,
                    StringComparer.Ordinal);
            string[] scenePaths = new[] { WorldBaseline06B2Paths.GlobalScene }
                .Concat(plan.CellIds.Select(WorldBaseline06B2Paths.CellScene))
                .ToArray();
            SceneSetup[] previousSetup =
                EditorSceneManager.GetSceneManagerSetup();
            bool restoreSetup =
                !Application.isBatchMode && previousSetup.Length > 0;
            int rendererCount = 0;
            try
            {
                foreach (string scenePath in scenePaths)
                {
                    if (!File.Exists(
                            WorldBaselinePaths.ToAbsoluteProjectPath(
                                scenePath)))
                    {
                        throw new FileNotFoundException(
                            "Generated streaming scene is missing.",
                            WorldBaselinePaths.ToAbsoluteProjectPath(
                                scenePath));
                    }

                    Scene scene = EditorSceneManager.OpenScene(
                        scenePath,
                        OpenSceneMode.Single);
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        DonorWorldBaselineEntityMetadata[] entities =
                            root.GetComponentsInChildren<
                                DonorWorldBaselineEntityMetadata>(true);
                        foreach (DonorWorldBaselineEntityMetadata entity in
                                 entities)
                        {
                            if (!assignmentById.TryGetValue(
                                    entity.StableId,
                                    out DonorWorldCellizationAssignment
                                        assignment))
                            {
                                throw new InvalidDataException(
                                    "Generated scene contains an unknown " +
                                    "legacy entity: " + entity.StableId);
                            }

                            DonorWorldLegacyMaterialBinding binding =
                                entity.GetComponent<
                                    DonorWorldLegacyMaterialBinding>();
                            if (binding?.TargetRenderer is not MeshRenderer
                                renderer)
                            {
                                continue;
                            }

                            DonorWorldRendererCompatibilityPolicy.Apply(
                                renderer,
                                assignment.SanitationEntry,
                                binding.SourceMaterialGuids,
                                materialPlan);
                            rendererCount++;
                        }
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, scenePath))
                    {
                        throw new IOException(
                            "Could not save remediated streaming scene: " +
                            scenePath);
                    }
                }
            }
            finally
            {
                if (restoreSetup)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }

            return rendererCount;
        }

        private static void AssertGeneratedMeshPath(string path)
        {
            bool underSource = path.StartsWith(
                WorldBaselinePaths.SourceMeshRoot + "/",
                StringComparison.Ordinal);
            bool underDerived = path.StartsWith(
                WorldBaselinePaths.DerivedMeshRoot + "/",
                StringComparison.Ordinal);
            if (!underSource && !underDerived)
            {
                throw new InvalidDataException(
                    "Lighting remediation may only edit generated " +
                    "RuntimeBaseline mesh copies: " + path);
            }
        }

        private static void AssertGeometryStillMatches(
            Mesh target,
            IReadOnlyList<Vector3> expectedVertices,
            string stableId)
        {
            Vector3[] actual = target.vertices;
            if (actual.Length != expectedVertices.Count)
            {
                throw new InvalidDataException(
                    "Derived mesh vertex count drifted before lighting " +
                    $"remediation: {stableId}.");
            }

            for (int index = 0; index < actual.Length; index++)
            {
                if ((actual[index] - expectedVertices[index]).sqrMagnitude >
                    PositionToleranceSquared)
                {
                    throw new InvalidDataException(
                        "Derived mesh vertex order drifted before lighting " +
                        $"remediation: {stableId} at vertex {index}.");
                }
            }
        }

        private static bool HasValidNormals(Mesh mesh)
        {
            Vector3[] values = mesh.normals;
            return values.Length == mesh.vertexCount &&
                   values.All(IsFiniteNonZero);
        }

        private static bool HasValidTangents(Mesh mesh)
        {
            Vector4[] values = mesh.tangents;
            return values.Length == mesh.vertexCount &&
                   values.All(value =>
                       float.IsFinite(value.x) &&
                       float.IsFinite(value.y) &&
                       float.IsFinite(value.z) &&
                       float.IsFinite(value.w) &&
                       new Vector3(value.x, value.y, value.z).sqrMagnitude >
                       MinimumVectorLengthSquared);
        }

        private static bool IsFiniteNonZero(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            value.sqrMagnitude > MinimumVectorLengthSquared;

        private static bool TryNormalize(ref Vector3 value)
        {
            if (!IsFiniteNonZero(value))
            {
                return false;
            }
            value.Normalize();
            return true;
        }

        private static bool HasCompleteUv0(Mesh mesh) =>
            mesh.uv.Length == mesh.vertexCount;

        private static bool HasTriangleTopology(Mesh mesh)
        {
            for (int index = 0; index < mesh.subMeshCount; index++)
            {
                if (mesh.GetTopology(index) != MeshTopology.Triangles)
                {
                    return false;
                }
            }
            return mesh.subMeshCount > 0;
        }

        private static void RequireTriangleTopology(
            Mesh mesh,
            string identity)
        {
            if (!HasTriangleTopology(mesh))
            {
                throw new InvalidDataException(
                    "Cannot reconstruct missing normals for non-triangle " +
                    "generated mesh: " + identity);
            }
        }

        private readonly struct RepairSummary
        {
            public RepairSummary(
                int meshCount,
                int normalsRepaired,
                int tangentsRepaired)
            {
                MeshCount = meshCount;
                NormalsRepaired = normalsRepaired;
                TangentsRepaired = tangentsRepaired;
            }

            public int MeshCount { get; }
            public int NormalsRepaired { get; }
            public int TangentsRepaired { get; }
        }

        private sealed class SourceMeshSnapshot
        {
            public SourceMeshSnapshot(
                Vector3[] vertices,
                Vector3[] normals,
                Vector4[] tangents,
                IReadOnlyDictionary<int, int[]> indicesBySubMesh)
            {
                Vertices = vertices;
                Normals = normals;
                Tangents = tangents;
                IndicesBySubMesh = indicesBySubMesh;
            }

            public Vector3[] Vertices { get; }
            public Vector3[] Normals { get; }
            public Vector4[] Tangents { get; }
            public IReadOnlyDictionary<int, int[]> IndicesBySubMesh { get; }
        }
    }
}
