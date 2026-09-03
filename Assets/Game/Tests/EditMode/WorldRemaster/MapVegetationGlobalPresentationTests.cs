using System;
using System.Linq;
using MSC.Editor.Vegetation;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationGlobalPresentationTests
    {
        [TestCase("Spruce")]
        [TestCase("Pine")]
        [TestCase("Birch")]
        [TestCase("Aspen")]
        public void DistantTemplate_PreservesEveryFarRendererSubmeshMaterial(
            string species)
        {
            MapVegetationGlobalPresentation.DistantTemplate template =
                MapVegetationGlobalPresentation.DistantTemplate.Load(species);

            Assert.That(template.Height, Is.GreaterThan(0f));
            Assert.That(template.Parts, Is.Not.Empty);
            foreach (MapVegetationGlobalPresentation.DistantPart part in
                     template.Parts)
            {
                Assert.That(part.Material, Is.Not.Null);
                Assert.That(part.Vertices, Is.Not.Empty);
                Assert.That(part.Normals.Length,
                    Is.EqualTo(part.Vertices.Length));
                Assert.That(part.Tangents.Length,
                    Is.EqualTo(part.Vertices.Length));
                Assert.That(part.Uvs.Length,
                    Is.EqualTo(part.Vertices.Length));
                Assert.That(part.Uv1.Length,
                    Is.EqualTo(part.Vertices.Length));
                Assert.That(part.Uv2.Length,
                    Is.EqualTo(part.Vertices.Length));
                Assert.That(part.Colors.Length,
                    Is.EqualTo(part.Vertices.Length));
                Assert.That(part.Tangents.All(tangent =>
                    float.IsFinite(tangent.x) &&
                    float.IsFinite(tangent.y) &&
                    float.IsFinite(tangent.z) &&
                    new Vector3(tangent.x, tangent.y, tangent.z)
                        .sqrMagnitude > 0.99f &&
                    Mathf.Abs(Mathf.Abs(tangent.w) - 1f) < 0.0001f),
                    Is.True, species + " far LOD has invalid tangent data.");
                Assert.That(part.Indices.Length, Is.GreaterThanOrEqualTo(3));
                Assert.That(part.Indices.Length % 3, Is.Zero);
                Assert.That(part.Indices.All(index => index >= 0 &&
                    index < part.Vertices.Length), Is.True);
                AssertFarMaterialPolicy(species, part.Material);
            }
        }

        [TestCase("Spruce")]
        [TestCase("Pine")]
        [TestCase("Birch")]
        [TestCase("Aspen")]
        public void DistantTemplate_SelectsDeterministicBroadestCheapFarCrown(
            string species)
        {
            MapVegetationGlobalPresentation.DistantTemplate selected =
                MapVegetationGlobalPresentation.DistantTemplate.Load(species);
            MapVegetationGlobalPresentation.DistantTemplate expected =
                MapVegetationTreePresentation.LoadSpeciesPrefabs(species)
                    .OrderBy(prefab => AssetDatabase.GetAssetPath(prefab),
                        StringComparer.Ordinal)
                    .Select(MapVegetationGlobalPresentation.DistantTemplate
                        .LoadSingleForTests)
                    .Where(template => template.FitsBackdropCost)
                    .OrderByDescending(template =>
                        template.NormalizedCrownRadius)
                    .ThenBy(template => template.TotalVertexCount)
                    .ThenBy(template => template.SourcePrefabPath,
                        StringComparer.Ordinal)
                    .First();

            Assert.That(selected.SourcePrefabPath,
                Is.EqualTo(expected.SourcePrefabPath));
            Assert.That(selected.NormalizedCrownRadius,
                Is.EqualTo(expected.NormalizedCrownRadius).Within(0.00001f));
            Assert.That(selected.NormalizedCrownRadius,
                Is.GreaterThan(0.05f),
                "Selected distant foliage must have a useful crown silhouette.");
            Assert.That(selected.Parts, Has.Count.EqualTo(
                MapVegetationGlobalPresentation.DistantTemplateMaximumParts));
            Assert.That(selected.TotalVertexCount,
                Is.InRange(1, MapVegetationGlobalPresentation
                    .DistantTemplateMaximumVertices));
        }

        [TestCase("Spruce")]
        [TestCase("Pine")]
        [TestCase("Birch")]
        [TestCase("Aspen")]
        public void DistantTemplate_SelectedProxyCostFitsFullPopulationBudget(
            string species)
        {
            MapVegetationGlobalPresentation.DistantTemplate selected =
                MapVegetationGlobalPresentation.DistantTemplate.Load(species);

            const int hardTreeCap = 16000;
            const int hardVertexBudget = 2000000;
            Assert.That((long)selected.TotalVertexCount * hardTreeCap,
                Is.LessThanOrEqualTo(hardVertexBudget),
                species + " selected proxy cannot fit the full tree cap.");
            Assert.That(selected.Parts, Has.Count.EqualTo(1),
                species + " selected proxy must remain one renderer per batch.");
        }

        [Test]
        public void DistantBatch_UsesMetadataCountInsteadOfAssumedIndexCount()
        {
            var part = new MapVegetationGlobalPresentation.DistantPart
            {
                Name = "single-triangle",
                Vertices = new[]
                {
                    Vector3.zero, Vector3.right, Vector3.up * 2f
                },
                Normals = new[] { Vector3.forward, Vector3.forward,
                    Vector3.forward },
                Tangents = new[]
                {
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f)
                },
                Uvs = new[] { Vector2.zero, Vector2.right, Vector2.up },
                Uv1 = new[] { Vector4.zero, Vector4.zero, Vector4.zero },
                Uv2 = new[] { Vector4.zero, Vector4.zero, Vector4.zero },
                Colors = new[] { Color.white, Color.white, Color.white },
                Indices = new[] { 0, 1, 2 }
            };
            var placements = new[]
            {
                Placement("a", new Vector3(512f, 3f, 1024f), 4f),
                Placement("b", new Vector3(516f, 3f, 1028f), 4f)
            };
            Mesh mesh = null;
            GameObject owner = null;
            try
            {
                Vector3 origin = new Vector3(512f, 0f, 1024f);
                mesh = MapVegetationGlobalPresentation.BuildBatchMesh(part,
                    2f, placements, "test-distant-batch", origin);
                Assert.That(mesh.vertexCount, Is.EqualTo(6));
                Assert.That(mesh.tangents, Has.Length.EqualTo(
                    mesh.vertexCount));
                Assert.That(mesh.tangents.All(tangent =>
                    float.IsFinite(tangent.x) &&
                    float.IsFinite(tangent.y) &&
                    float.IsFinite(tangent.z) &&
                    new Vector3(tangent.x, tangent.y, tangent.z)
                        .sqrMagnitude > 0.99f), Is.True);
                Assert.That(mesh.GetIndexCount(0), Is.EqualTo(6));
                Assert.That(mesh.bounds.size.x, Is.LessThan(8f));
                Assert.That(mesh.bounds.size.z, Is.LessThan(8f));

                owner = new GameObject("batch-owner");
                var metadata = owner.AddComponent<
                    GeneratedDistantForestBatch>();
                metadata.ConfigureForAuthoring("cell_1_2", "Spruce",
                    placements.Length, 1);
                Assert.That(metadata.InstanceCount, Is.EqualTo(2));
                Assert.That(metadata.InstanceCount,
                    Is.Not.EqualTo((int)mesh.GetIndexCount(0) / 6));
                Assert.That(metadata.TemplatePartCount, Is.EqualTo(1));
            }
            finally
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static MapVegetationPlacement Placement(string id,
            Vector3 position, float height) => new MapVegetationPlacement
        {
            id = id,
            position = position,
            height = height,
            yaw = 0f
        };

        private static void AssertFarMaterialPolicy(string species,
            Material material)
        {
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            Assert.That(material.enableInstancing, Is.True,
                species + " backdrop material must support instanced/batched policy.");
            Assert.That(material.HasProperty("_Metallic"), Is.True);
            Assert.That(material.GetFloat("_Metallic"),
                Is.Zero.Within(0.0001f));

            bool doubleSided = material.HasProperty("_DoubleSidedEnable") &&
                material.GetFloat("_DoubleSidedEnable") > 0.5f;
            if (material.HasProperty("_CullMode"))
                Assert.That(material.GetFloat("_CullMode"), Is.EqualTo(
                    (float)(doubleSided ? CullMode.Off : CullMode.Back)));
            if (doubleSided && material.HasProperty("_AlphaCutoffEnable"))
                Assert.That(material.GetFloat("_AlphaCutoffEnable"),
                    Is.EqualTo(1f),
                    species + " far foliage cards must retain alpha clipping.");
        }
    }
}
