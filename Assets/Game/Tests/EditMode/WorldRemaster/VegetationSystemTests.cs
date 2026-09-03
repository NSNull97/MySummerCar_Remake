using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MSC.World.Partition;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class VegetationSystemTests
    {
        private const string CatalogPath =
            "Assets/Game/World/Content/Vegetation/VegetationCellCatalog.asset";
        private const string IndirectShaderPath =
            "Assets/Game/Presentation/Shaders/Vegetation/MSC_VegetationIndirectHDRP.shader";

        [Test]
        public void IndirectShader_UsesStableNonJitteredLodDitherSeed()
        {
            string source = File.ReadAllText(IndirectShaderPath);

            Assert.That(source, Does.Contain(
                "float2 lodDitherPosition : TEXCOORD5;"));
            Assert.That(source, Does.Contain("UNITY_MATRIX_UNJITTERED_VP"));
            Assert.That(source, Does.Contain(
                "DitherNoise(input.lodDitherPosition)"));
            Assert.That(source, Does.Not.Contain(
                "DitherNoise(input.positionCS.xy)"));
        }

        [Test]
        public void ProductionCatalog_CoversEveryStreamingCell()
        {
            VegetationCellCatalog catalog =
                AssetDatabase.LoadAssetAtPath<VegetationCellCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Cells.Count, Is.EqualTo(49));
            Assert.That(catalog.Profiles.Count, Is.EqualTo(4));
            Assert.That(catalog.ValidateConfiguration(), Is.Empty);
            Assert.That(
                catalog.Cells.Select(cell => cell.CellId).Distinct().Count(),
                Is.EqualTo(catalog.Cells.Count));
        }

        [Test]
        public void InstanceRecord_RemainsCompactAndRoundTripsPackedValues()
        {
            Assert.That(
                Marshal.SizeOf<VegetationInstanceRecord>(),
                Is.EqualTo(VegetationInstanceRecord.Stride));

            var record = new VegetationInstanceRecord(
                new Vector3(12.5f, 3.25f, -88f),
                new Vector3(0.25f, 0.93f, -0.27f).normalized,
                271.25f,
                1.7f,
                2,
                143,
                50000);

            Assert.That(record.WorldPosition, Is.EqualTo(
                new Vector3(12.5f, 3.25f, -88f)));
            Assert.That(record.SurfaceNormal.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(record.YawDegrees, Is.EqualTo(271.25f).Within(0.01f));
            Assert.That(record.UniformScale, Is.EqualTo(1.7f).Within(0.001f));
            Assert.That(record.ProfileIndex, Is.EqualTo(2));
            Assert.That(record.ColorVariation, Is.EqualTo(143));
            Assert.That(record.WindPhase, Is.EqualTo(50000));
        }

        [Test]
        public void StableJitter_IsDeterministicInGlobalCoordinates()
        {
            Vector2 first =
                VegetationStableHash.JitteredGridOffset(-4512, 2301, 1907);
            Vector2 repeated =
                VegetationStableHash.JitteredGridOffset(-4512, 2301, 1907);
            Vector2 neighbor =
                VegetationStableHash.JitteredGridOffset(-4511, 2301, 1907);

            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(neighbor, Is.Not.EqualTo(first));
            Assert.That(first.x, Is.InRange(0f, 1f));
            Assert.That(first.y, Is.InRange(0f, 1f));
        }

        [Test]
        public void WorldSpaceMaskMapping_DoesNotUseSourceMeshUvs()
        {
            var texture = new Texture2D(
                16,
                16,
                TextureFormat.RGBA32,
                false,
                true);
            var pixels = new Color32[16 * 16];
            Color32 authoredDensity = new Color32(64, 128, 192, 255);
            pixels[8 * 16 + 4] = authoredDensity;
            pixels[8 * 16 + 5] = authoredDensity;
            pixels[9 * 16 + 4] = authoredDensity;
            pixels[9 * 16 + 5] = authoredDensity;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            VegetationCellAsset cell = ScriptableObject.CreateInstance<
                VegetationCellAsset>();

            try
            {
                cell.ConfigureForAuthoring(
                    "cell_0_0",
                    new WorldCellIndex(0, 0),
                    new Bounds(
                        new Vector3(8f, 0f, 8f),
                        new Vector3(16f, 20f, 16f)),
                    16,
                    16f,
                    texture);

                Vector2Int pixel = cell.WorldToMaskPixel(
                    new Vector3(4.5f, 999f, 8.5f));
                Vector3 reconstructed = cell.MaskPixelToWorldXZ(
                    pixel.x,
                    pixel.y,
                    -12f);

                Assert.That(pixel, Is.EqualTo(new Vector2Int(4, 8)));
                Assert.That(reconstructed.x, Is.EqualTo(4.5f).Within(0.001f));
                Assert.That(reconstructed.z, Is.EqualTo(8.5f).Within(0.001f));
                Assert.That(reconstructed.y, Is.EqualTo(-12f));
                Assert.That(
                    cell.SampleDensity(
                        VegetationDensityChannel.Decorative,
                        reconstructed),
                    Is.GreaterThan(0.8f));
            }
            finally
            {
                Object.DestroyImmediate(cell);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void BrushCircle_ReturnsBothCellsAtStreamingBoundary()
        {
            VegetationCellAsset left = CreateCell(
                "cell_0_0",
                new WorldCellIndex(0, 0),
                new Vector3(256f, 0f, 256f));
            VegetationCellAsset right = CreateCell(
                "cell_1_0",
                new WorldCellIndex(1, 0),
                new Vector3(768f, 0f, 256f));
            VegetationCellCatalog catalog =
                ScriptableObject.CreateInstance<VegetationCellCatalog>();

            try
            {
                catalog.ConfigureForAuthoring(
                    new[] { left, right },
                    null,
                    ~0,
                    2048f,
                    4096f);
                var results = new List<VegetationCellAsset>();

                catalog.GetCellsIntersectingCircle(
                    new Vector3(512f, 0f, 128f),
                    5f,
                    results);

                Assert.That(results, Is.EquivalentTo(new[] { left, right }));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                DestroyCell(left);
                DestroyCell(right);
            }
        }

        [Test]
        public void BlockerRules_DistinguishRoadBridgeDeckAndPillar()
        {
            GameObject gameObject = new GameObject("Vegetation blocker test");
            VegetationBlocker blocker =
                gameObject.AddComponent<VegetationBlocker>();

            try
            {
                blocker.ConfigureForAuthoring(
                    VegetationBlockerKind.GroundRoad,
                    roadMaximumGap: 1.25f);
                Assert.That(blocker.BlocksSurface(10f, 9f), Is.True);
                Assert.That(blocker.BlocksSurface(10f, 5f), Is.False);

                blocker.ConfigureForAuthoring(
                    VegetationBlockerKind.BridgeDeck,
                    allowBelowBridgeDeck: true,
                    bridgeMinimumClearance: 4f);
                Assert.That(blocker.BlocksSurface(10f, 8f), Is.True);
                Assert.That(blocker.BlocksSurface(10f, 5f), Is.False);

                blocker.ConfigureForAuthoring(
                    VegetationBlockerKind.BridgePillar);
                Assert.That(blocker.BlocksSurface(10f, -100f), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static VegetationCellAsset CreateCell(
            string id,
            WorldCellIndex index,
            Vector3 center)
        {
            var texture = new Texture2D(
                16,
                16,
                TextureFormat.RGBA32,
                false,
                true);
            var cell = ScriptableObject.CreateInstance<VegetationCellAsset>();
            cell.ConfigureForAuthoring(
                id,
                index,
                new Bounds(center, new Vector3(512f, 200f, 512f)),
                16,
                32f,
                texture);
            return cell;
        }

        private static void DestroyCell(VegetationCellAsset cell)
        {
            if (cell != null)
            {
                Object.DestroyImmediate(cell.DensityMask);
                Object.DestroyImmediate(cell);
            }
        }
    }
}
