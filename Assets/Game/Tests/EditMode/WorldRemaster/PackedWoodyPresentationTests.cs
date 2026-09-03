using System;
using System.IO;
using System.Linq;
using MSC.Editor.Vegetation;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class PackedWoodyPresentationTests
    {
        private Mesh mesh;
        private Material material;
        private PackedWoodyPrototypeAsset prototype;

        [SetUp]
        public void SetUp()
        {
            mesh = new Mesh { name = "Packed woody test mesh" };
            mesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Shader shader = Shader.Find("HDRP/Lit") ??
                Shader.Find("Standard") ??
                Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            material = new Material(shader)
            {
                name = "Packed woody test material",
                enableInstancing = true
            };
            prototype = ScriptableObject.CreateInstance<
                PackedWoodyPrototypeAsset>();
            ConfigurePrototype(
                MapVegetationPackedWoodyBuilder.ProjectPolicySignature());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(prototype);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void PackedAssetByteBudget_PerCellAcrossThreeAssetsUsesOneBase()
        {
            Assert.That(
                MapVegetationPackedWoodyBuilder.PackedAssetByteBudget(0),
                Is.EqualTo(256L * 1024L));
            Assert.That(
                MapVegetationPackedWoodyBuilder.PackedAssetByteBudget(5521),
                Is.EqualTo(256L * 1024L + 5521L * 1024L));
            Assert.That(
                MapVegetationPackedWoodyBuilder.PackedAssetByteBudget(
                    1000 + 2000 + 2521),
                Is.Not.EqualTo(
                    3L * 256L * 1024L + 5521L * 1024L),
                "The three category-local assets share one per-cell base " +
                "allowance.");
            long budget = MapVegetationPackedWoodyBuilder
                .PackedAssetByteBudget(5521);
            Assert.That(MapVegetationPackedWoodyBuilder
                .PackedAssetBytesFitBudget(budget, 5521), Is.True);
            Assert.That(MapVegetationPackedWoodyBuilder
                .PackedAssetBytesFitBudget(budget + 1, 5521), Is.False);
            Assert.That(
                () => MapVegetationPackedWoodyBuilder.PackedAssetByteBudget(-1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => MapVegetationPackedWoodyBuilder
                    .PackedAssetBytesFitBudget(-1, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void CompactCell_ValidatesExactPlacementMatrixAndCollisionIndex()
        {
            PackedWoodyCellAsset asset = CreateValidCell();
            try
            {
                Assert.That(asset.ValidateConfiguration(), Is.Empty);
                Assert.That(asset.InstanceCount, Is.EqualTo(1));
                Assert.That(asset.BatchCount, Is.EqualTo(1));
                Assert.That(asset.Batches[0].Count, Is.EqualTo(1));
                Assert.That(asset.CollisionRecordCount, Is.EqualTo(1));
                Assert.That(asset.CountSpecies(PackedWoodySpecies.Spruce),
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void BatchAboveRenderMeshInstancedLimit_IsRejected()
        {
            var asset = ScriptableObject.CreateInstance<PackedWoodyCellAsset>();
            try
            {
                asset.ConfigureForAuthoring(
                    "test-generator",
                    MapVegetationPackedWoodyBuilder.PresentationVersion,
                    "cell_0_0",
                    "fingerprint",
                    PackedWoodyCategory.OriginalTree,
                    new Bounds(Vector3.zero, Vector3.one * 10f),
                    new[] { prototype },
                    new[]
                    {
                        new PackedWoodyBatch(
                            0,
                            new Bounds(Vector3.zero, Vector3.one * 10f),
                            20f,
                            Enumerable.Repeat(
                                Matrix4x4.identity,
                                PackedWoodyBatch.MaximumInstanceCount + 1)
                                .ToArray())
                    },
                    Array.Empty<PackedWoodyPlacementRecord>(),
                    Array.Empty<PackedWoodyCollisionTile>(),
                    Array.Empty<PackedWoodyCollisionRecord>());
                Assert.That(asset.ValidateConfiguration().Any(error =>
                    error.Contains("1..1023")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void PlacementMetadata_MustClaimEveryMatrixExactlyOnce()
        {
            var asset = ScriptableObject.CreateInstance<PackedWoodyCellAsset>();
            try
            {
                Vector3 position = new Vector3(2f, 0f, 3f);
                asset.ConfigureForAuthoring(
                    "test-generator",
                    MapVegetationPackedWoodyBuilder.PresentationVersion,
                    "cell_0_0",
                    "fingerprint",
                    PackedWoodyCategory.OriginalTree,
                    new Bounds(position, Vector3.one * 20f),
                    new[] { prototype },
                    new[]
                    {
                        new PackedWoodyBatch(
                            0,
                            new Bounds(position, Vector3.one * 10f),
                            20f,
                            new[]
                            {
                                Matrix4x4.TRS(position,
                                    Quaternion.identity, Vector3.one),
                                Matrix4x4.TRS(position + Vector3.right,
                                    Quaternion.identity, Vector3.one)
                            })
                    },
                    new[]
                    {
                        new PackedWoodyPlacementRecord(
                            Hash128.Compute("tree:a"), 0, 0, 0,
                            PackedWoodyCategory.OriginalTree,
                            PackedWoodyPlacementMethod.DonorOriginal,
                            PackedWoodySpecies.Spruce, position, 20f),
                        new PackedWoodyPlacementRecord(
                            Hash128.Compute("tree:b"), 0, 0, 0,
                            PackedWoodyCategory.OriginalTree,
                            PackedWoodyPlacementMethod.DonorOriginal,
                            PackedWoodySpecies.Spruce, position, 20f)
                    },
                    Array.Empty<PackedWoodyCollisionTile>(),
                    Array.Empty<PackedWoodyCollisionRecord>());

                Assert.That(asset.ValidateConfiguration().Any(error =>
                    error.Contains("matrix pointer is duplicated")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ProjectPolicySignature_RejectsStalePrototypeProvenance()
        {
            ConfigurePrototype("stale-policy-signature");
            Assert.That(
                MapVegetationPackedWoodyBuilder.HasCurrentProjectPolicy(
                    prototype),
                Is.False);

            ConfigurePrototype(
                MapVegetationPackedWoodyBuilder.ProjectPolicySignature());
            Assert.That(
                MapVegetationPackedWoodyBuilder.HasCurrentProjectPolicy(
                    prototype),
                Is.True);
        }

        [Test]
        public void PrototypeAsset_SavedCellReferenceSurvivesReloadAndFailedRebuildPreservesAsset()
        {
            string folder = "Assets/Game/Tests/EditMode/WorldRemaster/" +
                "TransactionalPrototype_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(
                "Assets/Game/Tests/EditMode/WorldRemaster",
                Path.GetFileName(folder));
            string meshPath = folder + "/SourceMesh.asset";
            string materialPath = folder + "/SourceMaterial.mat";
            string prefabPath = folder + "/Source.prefab";
            string prototypePath = folder + "/Prototype.asset";
            string cellPath = folder + "/Cell.asset";
            try
            {
                var sourceMesh = new Mesh { name = "Transactional source" };
                sourceMesh.vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up
                };
                sourceMesh.triangles = new[] { 0, 1, 2 };
                sourceMesh.RecalculateNormals();
                sourceMesh.RecalculateBounds();
                AssetDatabase.CreateAsset(sourceMesh, meshPath);

                Shader shader = Shader.Find("HDRP/Lit") ??
                    Shader.Find("Standard") ??
                    Shader.Find("Hidden/InternalErrorShader");
                Assert.That(shader, Is.Not.Null);
                var sourceMaterial = new Material(shader)
                {
                    name = "Transactional material",
                    enableInstancing = true
                };
                AssetDatabase.CreateAsset(sourceMaterial, materialPath);

                var source = new GameObject("Transactional source");
                source.AddComponent<MeshFilter>().sharedMesh = sourceMesh;
                source.AddComponent<MeshRenderer>().sharedMaterial =
                    sourceMaterial;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    source, prefabPath);
                Object.DestroyImmediate(source);
                Assert.That(prefab, Is.Not.Null);

                PackedWoodyPrototypeAsset first =
                    MapVegetationPackedWoodyBuilder
                        .RebuildPrototypeAtPathForTests(
                            prefab, prototypePath);
                Assert.That(first.ValidateConfiguration(), Is.Empty);
                AssetDatabase.SaveAssets();

                MonoScript prototypeScript =
                    MonoScript.FromScriptableObject(first);
                Assert.That(prototypeScript, Is.Not.Null,
                    "A persisted ScriptableObject needs a matching MonoScript " +
                    "or it reloads as a missing-script asset.");
                Assert.That(prototypeScript.GetClass(),
                    Is.EqualTo(typeof(PackedWoodyPrototypeAsset)));
                StringAssert.EndsWith(
                    "/PackedWoodyPrototypeAsset.cs",
                    AssetDatabase.GetAssetPath(prototypeScript)
                        .Replace('\\', '/'));

                var cell =
                    ScriptableObject.CreateInstance<PackedWoodyCellAsset>();
                cell.ConfigureForAuthoring(
                    "saved-reload-regression",
                    MapVegetationPackedWoodyBuilder.PresentationVersion,
                    "cell_test",
                    "saved-reload-fingerprint",
                    PackedWoodyCategory.ShrubOrUndergrowth,
                    new Bounds(Vector3.zero, Vector3.one),
                    new[] { first },
                    Array.Empty<PackedWoodyBatch>(),
                    Array.Empty<PackedWoodyPlacementRecord>(),
                    Array.Empty<PackedWoodyCollisionTile>(),
                    Array.Empty<PackedWoodyCollisionRecord>());
                AssetDatabase.CreateAsset(cell, cellPath);
                AssetDatabase.SaveAssets();

                // Exercise the cold-import path which exposed m_Script: 0.
                Resources.UnloadAsset(cell);
                Resources.UnloadAsset(first);
                AssetDatabase.ImportAsset(
                    prototypePath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(
                    cellPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                PackedWoodyCellAsset reloadedCell = AssetDatabase
                    .LoadAssetAtPath<PackedWoodyCellAsset>(cellPath);
                Assert.That(reloadedCell, Is.Not.Null);
                Assert.That(reloadedCell.Prototypes.Count, Is.EqualTo(1));
                PackedWoodyPrototypeAsset reloadedPrototype =
                    reloadedCell.Prototypes[0];
                Assert.That(reloadedPrototype, Is.Not.Null,
                    "Saved packed cell prototype reference was lost after a " +
                    "forced asset reload.");
                Assert.That(
                    AssetDatabase.GetAssetPath(reloadedPrototype),
                    Is.EqualTo(prototypePath));
                Assert.That(reloadedCell.ValidateConfiguration(), Is.Empty);

                byte[] before = File.ReadAllBytes(
                    Path.GetFullPath(Path.Combine(
                        Application.dataPath, "..", prototypePath)));
                int meshCountBefore = AssetDatabase
                    .LoadAllAssetsAtPath(prototypePath).OfType<Mesh>().Count();
                Assert.That(meshCountBefore, Is.GreaterThan(0));

                GameObject contents = PrefabUtility.LoadPrefabContents(
                    prefabPath);
                Object.DestroyImmediate(
                    contents.GetComponent<MeshRenderer>());
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
                AssetDatabase.ImportAsset(
                    prefabPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                Assert.That(
                    () => MapVegetationPackedWoodyBuilder
                        .RebuildPrototypeAtPathForTests(
                            prefab, prototypePath),
                    Throws.TypeOf<InvalidDataException>());
                byte[] after = File.ReadAllBytes(
                    Path.GetFullPath(Path.Combine(
                        Application.dataPath, "..", prototypePath)));
                Assert.That(after, Is.EqualTo(before),
                    "Failed candidate generation must not rewrite the " +
                    "previous prototype asset.");
                PackedWoodyPrototypeAsset preserved = AssetDatabase
                    .LoadAssetAtPath<PackedWoodyPrototypeAsset>(prototypePath);
                Assert.That(preserved, Is.Not.Null);
                Assert.That(preserved.ValidateConfiguration(), Is.Empty);
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(prototypePath)
                    .OfType<Mesh>().Count(), Is.EqualTo(meshCountBefore));
            }
            finally
            {
                MapVegetationPackedWoodyBuilder.ClearCaches();
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ProxyBake_ReadsNonReadableMeshAndPreservesVertexChannels()
        {
            mesh.tangents = new[]
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f)
            };
            mesh.colors = new[] { Color.red, Color.green, Color.blue };
            mesh.UploadMeshData(true);
            Assert.That(mesh.isReadable, Is.False,
                "The fixture must exercise the imported non-readable path.");

            Matrix4x4 transform = Matrix4x4.TRS(
                new Vector3(2f, 3f, 4f),
                Quaternion.Euler(0f, 35f, 0f),
                new Vector3(-2f, 1.5f, 0.75f));
            Mesh proxy = MapVegetationPackedWoodyBuilder
                .BuildSingleProxyForTests(mesh, 0, transform);
            try
            {
                Assert.That(proxy.vertexCount, Is.EqualTo(3));
                Assert.That(proxy.GetIndexCount(0), Is.EqualTo(3));
                Assert.That(proxy.HasVertexAttribute(VertexAttribute.Normal),
                    Is.True);
                Assert.That(proxy.HasVertexAttribute(VertexAttribute.Tangent),
                    Is.True);
                Assert.That(proxy.HasVertexAttribute(VertexAttribute.Color),
                    Is.True);
                Assert.That(proxy.HasVertexAttribute(VertexAttribute.TexCoord0),
                    Is.True);
                Assert.That(Vector3.Distance(
                    proxy.vertices[0],
                    transform.MultiplyPoint3x4(Vector3.zero)),
                    Is.LessThan(0.00001f));
                Assert.That(proxy.triangles,
                    Is.EqualTo(new[] { 0, 2, 1 }),
                    "Mirrored proxy geometry must repair triangle winding.");
                Assert.That(proxy.tangents.All(value => value.w < 0f),
                    Is.True,
                    "Mirrored proxy geometry must repair tangent handedness.");
            }
            finally
            {
                Object.DestroyImmediate(proxy);
            }
        }

        [Test]
        public void TreeLodGuard_KeepsRealNearStageAndDefersLastStage()
        {
            var cameraOwner = new GameObject("Packed woody LOD camera");
            try
            {
                Camera camera = cameraOwner.AddComponent<Camera>();
                camera.fieldOfView = 60f;
                PackedWoodyDrawPart part = Part();
                prototype.ConfigureForAuthoring(
                    MapVegetationPackedWoodyBuilder.PresentationVersion,
                    "tree-prototype",
                    "source-guid",
                    "source-dependency",
                    MapVegetationPackedWoodyBuilder.ProjectPolicySignature(),
                    PackedWoodySpecies.Spruce,
                    20f,
                    new Bounds(Vector3.up * 10f,
                        new Vector3(8f, 20f, 8f)),
                    new[]
                    {
                        new PackedWoodyLod(.9f, new[] { part }),
                        new PackedWoodyLod(.8f, new[] { part }),
                        new PackedWoodyLod(.7f, new[] { part }),
                        new PackedWoodyLod(.0001f, new[] { part })
                    });

                Assert.That(PackedWoodyCellRenderer.SelectLod(
                    prototype, 20f, 79f, camera), Is.Zero);
                Assert.That(PackedWoodyCellRenderer.SelectLod(
                    prototype, 20f, 150f, camera), Is.EqualTo(2),
                    "The last/billboard stage must not appear in the near field.");
                Assert.That(PackedWoodyCellRenderer.SelectLod(
                    prototype, 20f, 221f, camera), Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(cameraOwner);
            }
        }

        [Test]
        public void PackedTreeAcceptance_RejectsFlatSingleMaterialNearProxy()
        {
            material.name = "Synthetic Foliage";
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(.66f, .76f, .60f, 1f));
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", .02f);
            if (material.HasProperty("_SpecularColor"))
                material.SetColor(
                    "_SpecularColor",
                    Color.white * MapVegetationTreeMaterialPolicy.BillboardSpecularF0);
            if (material.HasProperty("_ReceivesSSR"))
                material.SetFloat("_ReceivesSSR", 0f);
            if (material.HasProperty("_CoatMask"))
                material.SetFloat("_CoatMask", 0f);
            if (material.HasProperty("_SubsurfaceMask"))
                material.SetFloat("_SubsurfaceMask", 0f);
            if (material.HasProperty("_EmissiveColor"))
                material.SetColor("_EmissiveColor", Color.black);

            var error = Assert.Throws<System.IO.InvalidDataException>(() =>
                MapVegetationTreeAcceptance.AuditPackedPrototype(prototype));
            StringAssert.Contains("LOD0", error.Message);
            StringAssert.Contains("near", error.Message.ToLowerInvariant());
        }

        [Test]
        public void BoundaryOrShrubCollisionRecords_AreRejected()
        {
            PackedWoodyCellAsset asset = CreateValidCell();
            try
            {
                PackedWoodyPlacementRecord placement = asset.Placements[0];
                asset.ConfigureForAuthoring(
                    "test-generator",
                    MapVegetationPackedWoodyBuilder.PresentationVersion,
                    "cell_0_0",
                    "fingerprint",
                    PackedWoodyCategory.BoundaryForest,
                    asset.WorldBounds,
                    new[] { prototype },
                    new[] { asset.Batches[0] },
                    new[]
                    {
                        new PackedWoodyPlacementRecord(
                            placement.StableIdHash,
                            0,
                            0,
                            0,
                            PackedWoodyCategory.BoundaryForest,
                            PackedWoodyPlacementMethod.BoundaryForest,
                            PackedWoodySpecies.Spruce,
                            placement.WorldPosition,
                            placement.HeightMeters)
                    },
                    asset.CollisionTiles.ToArray(),
                    asset.CollisionRecords.ToArray());
                Assert.That(asset.ValidateConfiguration().Any(error =>
                    error.Contains("Only playable original/infill")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CompactCell_RejectsNonFiniteGeometryAndMetadataDrift()
        {
            PackedWoodyCellAsset asset = CreateValidCell();
            try
            {
                PackedWoodyPlacementRecord placement = asset.Placements[0];
                Matrix4x4 nonFiniteMatrix = Matrix4x4.identity;
                nonFiniteMatrix.m22 = float.NaN;
                asset.ConfigureForAuthoring(
                    "test-generator",
                    MapVegetationPackedWoodyBuilder.PresentationVersion,
                    "cell_0_0",
                    "fingerprint",
                    PackedWoodyCategory.OriginalTree,
                    new Bounds(Vector3.zero, Vector3.zero),
                    new[] { prototype },
                    new[]
                    {
                        new PackedWoodyBatch(
                            0,
                            new Bounds(Vector3.zero,
                                new Vector3(float.PositiveInfinity, 1f, 1f)),
                            20f,
                            new[] { nonFiniteMatrix })
                    },
                    new[]
                    {
                        new PackedWoodyPlacementRecord(
                            placement.StableIdHash,
                            0,
                            0,
                            0,
                            PackedWoodyCategory.OriginalTree,
                            PackedWoodyPlacementMethod.DonorOriginal,
                            PackedWoodySpecies.Pine,
                            new Vector3(15f, 2f, -9f),
                            20f)
                    },
                    Array.Empty<PackedWoodyCollisionTile>(),
                    Array.Empty<PackedWoodyCollisionRecord>());

                string[] errors = asset.ValidateConfiguration().ToArray();
                Assert.That(errors.Any(error =>
                    error.Contains("cell world bounds")), Is.True);
                Assert.That(errors.Any(error =>
                    error.Contains("batch world bounds")), Is.True);
                Assert.That(errors.Any(error =>
                    error.Contains("non-finite matrix")), Is.True);
                Assert.That(errors.Any(error =>
                    error.Contains("species differs")), Is.True);
                Assert.That(errors.Any(error =>
                    error.Contains("matrix translation")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private PackedWoodyCellAsset CreateValidCell()
        {
            var asset = ScriptableObject.CreateInstance<PackedWoodyCellAsset>();
            Hash128 stableId = Hash128.Compute("tree:test");
            Vector3 position = new Vector3(2f, 3f, 4f);
            Matrix4x4 matrix = Matrix4x4.TRS(
                position, Quaternion.Euler(0f, 37f, 0f), Vector3.one);
            asset.ConfigureForAuthoring(
                "test-generator",
                MapVegetationPackedWoodyBuilder.PresentationVersion,
                "cell_0_0",
                "fingerprint",
                PackedWoodyCategory.OriginalTree,
                new Bounds(position + Vector3.up * 10f,
                    new Vector3(10f, 20f, 10f)),
                new[] { prototype },
                new[]
                {
                    new PackedWoodyBatch(
                        0,
                        new Bounds(position + Vector3.up * 10f,
                            new Vector3(10f, 20f, 10f)),
                        20f,
                        new[] { matrix })
                },
                new[]
                {
                    new PackedWoodyPlacementRecord(
                        stableId,
                        0,
                        0,
                        0,
                        PackedWoodyCategory.OriginalTree,
                        PackedWoodyPlacementMethod.DonorOriginal,
                        PackedWoodySpecies.Spruce,
                        position,
                        20f)
                },
                new[]
                {
                    new PackedWoodyCollisionTile(
                        0, 0, 0, 1,
                        new Bounds(position + Vector3.up * 7.2f,
                            new Vector3(1f, 14.4f, 1f)))
                },
                new[]
                {
                    new PackedWoodyCollisionRecord(
                        stableId, position, 14.4f, .24f)
                });
            return asset;
        }

        private void ConfigurePrototype(string policySignature)
        {
            prototype.ConfigureForAuthoring(
                MapVegetationPackedWoodyBuilder.PresentationVersion,
                "tree-prototype",
                "source-guid",
                "source-dependency",
                policySignature,
                PackedWoodySpecies.Spruce,
                20f,
                new Bounds(Vector3.up * 10f,
                    new Vector3(8f, 20f, 8f)),
                new[]
                {
                    new PackedWoodyLod(.2f, new[] { Part() }),
                    new PackedWoodyLod(.0035f, new[] { Part() })
                });
        }

        private PackedWoodyDrawPart Part() => new PackedWoodyDrawPart(
            mesh,
            material,
            0,
            ShadowCastingMode.On,
            true);
    }
}
