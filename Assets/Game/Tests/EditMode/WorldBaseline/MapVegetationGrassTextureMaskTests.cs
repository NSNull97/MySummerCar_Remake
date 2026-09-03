using System.Collections.Generic;
using MSC.Editor.Vegetation;
using MSC.LegacyImport;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldBaseline
{
    public sealed class MapVegetationGrassTextureMaskTests
    {
        private readonly List<Object> owned = new List<Object>();
        private MapVegetationPlacementSettings settings;
        private static readonly Color32 Green = new Color32(20, 160, 20, 255);
        private static readonly Color32 Brown = new Color32(160, 60, 20, 255);

        [SetUp]
        public void SetUp()
        {
            settings = Own(ScriptableObject.CreateInstance<MapVegetationPlacementSettings>());
            var serialized = new SerializedObject(settings);
            // Existing unit cases exercise the exact sampled texel. Dilation
            // and continuous-carpet evidence have focused coverage cases below.
            serialized.FindProperty("grassTextureGreenDilationPixels").intValue = 0;
            serialized.FindProperty("grassTextureCarpetRadiusPixels").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        [TearDown] public void TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void GreenTextureOnlyFiltersGrassAndFastMigrationMatchesPlacement()
        {
            GameObject ground = Ground(Texture(Green, Brown));
            var query = MapVegetationSurfaceQuery.Build(new[] { ground }, settings);
            var green = new Vector3(2.5f, 100f, 2.5f);
            var brown = new Vector3(7.5f, 100f, 2.5f);
            Assert.That(query.TryResolve(green, MapVegetationKind.Grass, out _, out string reason), Is.True, reason);
            Assert.That(query.AllowsGrassGroundTexture(green, out reason), Is.True, reason);
            Assert.That(query.TryResolve(brown, MapVegetationKind.Grass, out _, out reason), Is.False);
            Assert.That(reason, Does.StartWith("GrassTextureNotGreen:"));
            Assert.That(query.AllowsGrassGroundTexture(brown, out reason), Is.False);
            Assert.That(query.TryResolve(brown, MapVegetationKind.Tree, out _, out reason), Is.True, reason);
            Assert.That(MapVegetationPlanning.TryResolveTreePlacementSurface(
                query, brown, out _, out reason), Is.True, reason,
                "Natural/distant tree infill must accept valid brown forest soil; the colour mask belongs only to grass.");
            Assert.That(query.TryResolve(brown, MapVegetationKind.Shrub, out _, out reason), Is.True, reason);
            Assert.That(query.GrassTextureMaskCoverage, Has.Some.Contains("Supported UV0"));
        }

        [Test]
        public void WorldTransformUsesBarycentricUvInsteadOfWorldPositionAsTextureCoordinate()
        {
            GameObject ground = Ground(Texture(Green, Brown));
            ground.transform.SetPositionAndRotation(new Vector3(410f, 27f, -620f), Quaternion.Euler(0f, 37f, 0f));
            ground.transform.localScale = new Vector3(1.7f, 1f, 0.8f);
            var query = MapVegetationSurfaceQuery.Build(new[] { ground }, settings);
            Assert.That(query.AllowsGrassGroundTexture(ground.transform.TransformPoint(new Vector3(2.5f, 0f, 2.5f)), out var reason), Is.True, reason);
            Assert.That(query.AllowsGrassGroundTexture(ground.transform.TransformPoint(new Vector3(7.5f, 0f, 2.5f)), out reason), Is.False);
        }

        [TestCase(TextureWrapMode.Repeat, true)]
        [TestCase(TextureWrapMode.Clamp, false)]
        [TestCase(TextureWrapMode.Mirror, true)]
        [TestCase(TextureWrapMode.MirrorOnce, false)]
        public void MaterialScaleOffsetAndTextureWrapAreCaptured(TextureWrapMode wrap, bool expected)
        {
            Texture2D texture = Texture(Green, Brown); texture.wrapModeU = wrap;
            GameObject ground = Ground(texture);
            Material material = ground.GetComponent<Renderer>().sharedMaterial;
            material.SetTextureScale("_BaseColorMap", new Vector2(2f, 1f));
            material.SetTextureOffset("_BaseColorMap", new Vector2(0.5f, 0f));
            var query = MapVegetationSurfaceQuery.Build(new[] { ground }, settings);
            // UV .75 * 2 + .5 = 2; Repeat/Mirror reach the left green edge.
            Assert.That(query.AllowsGrassGroundTexture(new Vector3(7.5f, 0f, 2.5f), out _), Is.EqualTo(expected));
        }

        [Test]
        public void BilinearSamplerRejectsNeutralBlendAndAcceptsItsGreenSide()
        {
            Texture2D texture = Texture(Green, new Color32(160, 20, 20, 255));
            texture.filterMode = FilterMode.Bilinear;
            var query = MapVegetationSurfaceQuery.Build(new[] { Ground(texture) }, settings);
            Assert.That(query.AllowsGrassGroundTexture(new Vector3(4.5f, 0f, 2.5f), out var reason), Is.True, reason);
            Assert.That(query.AllowsGrassGroundTexture(new Vector3(5f, 0f, 2.5f), out reason), Is.False);
        }

        [Test]
        public void GreenDilationAcceptsOnlyConfiguredNeighbouringTexels()
        {
            var colours = new Color32[16];
            for (int index = 0; index < colours.Length; index++) colours[index] = Brown;
            colours[0] = Green;
            colours[8] = Green;
            var texture = Own(new Texture2D(8, 2, TextureFormat.RGBA32, false, false));
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(colours);
            texture.Apply();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("grassTextureGreenDilationPixels").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var query = MapVegetationSurfaceQuery.Build(new[] { Ground(texture) }, settings);
            Assert.That(query.AllowsGrassGroundTexture(new Vector3(1.875f, 0f, 2.5f), out var reason), Is.True, reason);
            Assert.That(query.AllowsGrassGroundTexture(new Vector3(3.125f, 0f, 2.5f), out reason), Is.False);
        }

        [Test]
        public void CarpetEvidence_ClosesNoisyGreenHolesWithoutBleedingIntoBroadBrownGround()
        {
            const int width = 48, height = 32, naturalWidth = 32;
            var colours = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                colours[y * width + x] = x < naturalWidth && (x + y) % 2 == 0
                    ? Green
                    : Brown;
            // A lone green texel in the brown half is texture noise, not a
            // licence to spawn an isolated grass flowerbed.
            colours[16 * width + 40] = Green;
            var texture = Own(new Texture2D(width, height,
                TextureFormat.RGBA32, false, false));
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(colours);
            texture.Apply();
            GameObject ground = Ground(texture);

            var baseline = MapVegetationSurfaceQuery.Build(new[] { ground },
                settings);
            var carpetSettings = Own(ScriptableObject.CreateInstance<
                MapVegetationPlacementSettings>());
            var serialized = new SerializedObject(carpetSettings);
            serialized.FindProperty("grassTextureGreenDilationPixels").intValue = 0;
            serialized.FindProperty("grassTextureCarpetRadiusPixels").intValue = 4;
            serialized.FindProperty("grassTextureCarpetMinimumGreenFraction").floatValue = 0.30f;
            serialized.FindProperty("grassTextureCarpetMinimumSectors").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var carpet = MapVegetationSurfaceQuery.Build(new[] { ground },
                carpetSettings);

            int baselineNatural = 0, carpetNatural = 0, naturalSamples = 0;
            for (int y = 4; y < height - 4; y++)
            for (int x = 4; x < naturalWidth - 4; x++)
            {
                Vector3 point = PixelCenter(x, y, width, height);
                if (baseline.AllowsGrassGroundTexture(point, out _))
                    baselineNatural++;
                if (carpet.AllowsGrassGroundTexture(point, out _))
                    carpetNatural++;
                naturalSamples++;
            }
            Assert.That(baselineNatural / (float)naturalSamples,
                Is.EqualTo(0.50f).Within(0.01f),
                "The fixture must reproduce the disconnected pixel-island mask.");
            Assert.That(carpetNatural / (float)naturalSamples,
                Is.GreaterThanOrEqualTo(0.99f),
                "Locally mixed green terrain must become one continuous eligible carpet.");

            int brownAccepted = 0, brownSamples = 0;
            for (int y = 4; y < height - 4; y++)
            for (int x = naturalWidth + 4; x < width - 4; x++)
            {
                if (carpet.AllowsGrassGroundTexture(
                        PixelCenter(x, y, width, height), out _))
                    brownAccepted++;
                brownSamples++;
            }
            Assert.That(brownSamples, Is.GreaterThan(0));
            Assert.That(brownAccepted, Is.Zero,
                "Surrounded-green evidence may close holes but must not invade a broad brown region or preserve a lone green speck.");
            Assert.That(carpet.DiagnosticSummary,
                Does.Contain("grassMaskCarpetGreen="));
        }

        [Test]
        public void TextureDoesNotPromoteUnknownGeometryAndReportsUncoveredExplicitGround()
        {
            GameObject unknown = Ground(Texture(Green, Green), canonical: false);
            var query = MapVegetationSurfaceQuery.Build(new[] { unknown }, settings);
            Assert.That(query.TryResolve(new Vector3(2f, 0f, 2f), MapVegetationKind.Grass, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("NoAllowedGround"));
            unknown.AddComponent<VegetationSurface>();
            query = MapVegetationSurfaceQuery.Build(new[] { unknown }, settings);
            Assert.That(query.TryResolve(new Vector3(2f, 0f, 2f), MapVegetationKind.Grass, out _, out reason), Is.True, reason);
            Assert.That(query.GrassTextureMaskCoverage, Has.Some.Contains("Outside configured mask coverage"));
        }

        [Test]
        public void ExclusionsKeepPriorityAndFastApiDoesNotPretendToValidateNewPlacements()
        {
            GameObject ground = Ground(Texture(Green, Brown));
            GameObject road = Ground(Texture(Green, Green), canonical: false);
            road.AddComponent<VegetationBlocker>().ConfigureForAuthoring(VegetationBlockerKind.GroundRoad);
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, road }, settings);
            Assert.That(query.TryResolve(new Vector3(7f, 0f, 2f), MapVegetationKind.Grass, out _, out var reason), Is.False);
            Assert.That(reason, Does.StartWith("DirtRoad:"));
            Assert.That(MapVegetationPlanning.TryResolveTreePlacementSurface(
                query, new Vector3(7f, 0f, 2f), out _, out reason), Is.False);
            Assert.That(reason, Does.StartWith("DirtRoad:"));
            // Migration API intentionally omits road checks: caller must supply
            // records that already passed the complete placement query.
            Assert.That(query.AllowsGrassGroundTexture(new Vector3(2f, 0f, 2f), out reason), Is.True, reason);
        }

        [Test]
        public void UpperBrownGroundDoesNotFallThroughToGreenSurfaceBelow()
        {
            GameObject lower = Ground(Texture(Green, Green));
            GameObject upper = Ground(Texture(Brown, Brown)); upper.transform.position = Vector3.up * 4f;
            var query = MapVegetationSurfaceQuery.Build(new[] { lower, upper }, settings);
            Assert.That(query.TryResolve(new Vector3(2f, 0f, 2f), MapVegetationKind.Grass, out var hit, out var reason), Is.False);
            Assert.That(reason, Does.StartWith("GrassTextureNotGreen:"));
            Assert.That(hit.Position.y, Is.EqualTo(4f));
            Assert.That(query.AllowsGrassGroundTexture(new Vector3(2f, 0f, 2f), out reason), Is.False);
        }

        [Test]
        public void UnsupportedUvIsExplicitAndFallbackRequiresAuthoredSetting()
        {
            GameObject ground = Ground(Texture(Green, Green));
            ground.GetComponent<MeshFilter>().sharedMesh.uv = System.Array.Empty<Vector2>();
            var strict = MapVegetationSurfaceQuery.Build(new[] { ground }, settings);
            Assert.That(strict.TryResolve(new Vector3(2f, 0f, 2f), MapVegetationKind.Grass, out _, out var reason), Is.False);
            Assert.That(reason, Does.StartWith("GrassTextureUnsupported:"));
            Assert.That(strict.Warnings, Has.Some.Contains("NoUv0"));
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("grassTextureRejectUnsupported").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var fallback = MapVegetationSurfaceQuery.Build(new[] { ground }, settings);
            Assert.That(fallback.AllowsGrassGroundTexture(new Vector3(2f, 0f, 2f), out reason), Is.True, reason);
            Assert.That(strict.AllowsGrassGroundTexture(new Vector3(2f, 0f, 2f), out reason), Is.False);
            Assert.That(fallback.Warnings, Has.Some.Contains("NoUv0"));
        }

        [Test]
        public void SnapshotSurvivesDestructionOfEveryNativeSourceAsset()
        {
            Texture2D texture = Texture(Green, Brown);
            GameObject ground = Ground(texture);
            Material material = ground.GetComponent<Renderer>().sharedMaterial;
            Mesh mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            var query = MapVegetationSurfaceQuery.Build(new[] { ground }, settings);
            string before = query.ComputeGeometryFingerprint();
            Object.DestroyImmediate(ground); Object.DestroyImmediate(mesh); Object.DestroyImmediate(material); Object.DestroyImmediate(texture);
            Assert.That(query.AllowsGrassGroundTexture(new Vector3(2f, 0f, 2f), out var reason), Is.True, reason);
            Assert.That(query.AllowsGrassGroundTexture(new Vector3(7f, 0f, 2f), out reason), Is.False);
            Assert.That(query.ComputeGeometryFingerprint(), Is.EqualTo(before));
        }

        [Test]
        public void FingerprintTracksUvPixelsStAndThresholdWhileOldSnapshotIsImmutable()
        {
            Texture2D texture = Texture(Green, Brown);
            GameObject ground = Ground(texture);
            var roots = new[] { ground };
            var original = MapVegetationSurfaceQuery.Build(roots, settings);
            string baseline = original.ComputeGeometryFingerprint();
            Mesh mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            Vector2[] uv = mesh.uv; for (int i = 0; i < uv.Length; i++) uv[i] += Vector2.right * 0.5f; mesh.uv = uv;
            string uvHash = MapVegetationSurfaceQuery.Build(roots, settings).ComputeGeometryFingerprint();
            Assert.That(uvHash, Is.Not.EqualTo(baseline));
            texture.SetPixels32(new[] { Brown, Green, Brown, Green }); texture.Apply();
            string pixelHash = MapVegetationSurfaceQuery.Build(roots, settings).ComputeGeometryFingerprint();
            Assert.That(pixelHash, Is.Not.EqualTo(uvHash));
            ground.GetComponent<Renderer>().sharedMaterial.SetTextureOffset("_BaseColorMap", new Vector2(0.2f, 0f));
            string stHash = MapVegetationSurfaceQuery.Build(roots, settings).ComputeGeometryFingerprint();
            Assert.That(stHash, Is.Not.EqualTo(pixelHash));
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("grassTextureMinimumGreenExcess").floatValue = 0.9f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(MapVegetationSurfaceQuery.Build(roots, settings).ComputeGeometryFingerprint(), Is.Not.EqualTo(stHash));
            Assert.That(original.ComputeGeometryFingerprint(), Is.EqualTo(baseline));
            Assert.That(original.AllowsGrassGroundTexture(new Vector3(2f, 0f, 2f), out var reason), Is.True, reason);
        }

        private Texture2D Texture(Color32 left, Color32 right)
        {
            var texture = Own(new Texture2D(2, 2, TextureFormat.RGBA32, false, false));
            texture.name = "mask-test"; texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(new[] { left, right, left, right }); texture.Apply();
            return texture;
        }

        private static Vector3 PixelCenter(int x, int y, int width,
            int height)
        {
            return new Vector3((x + 0.5f) / width * 10f, 0f,
                (y + 0.5f) / height * 10f);
        }

        [Test]
        public void MeasuredFootprintContainsRotatedTiltedAndWindDisplacedGrassAcrossLods()
        {
            var near = Own(new Mesh { vertices = new[] { new Vector3(0.8f, 0.9f, 0.15f), new Vector3(-0.55f, -0.08f, 0.6f) } });
            var far = Own(new Mesh { vertices = new[] { new Vector3(-0.9f, 0.75f, -0.2f) } });
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(VegetationAssetBuilder.ShaderPath);
            Assert.That(shader, Is.Not.Null);
            var material = Own(new Material(shader)); material.SetFloat("_WindStrength", 0.065f);
            var profile = Own(ScriptableObject.CreateInstance<VegetationProfile>());
            profile.ConfigureForAuthoring("footprint-test", VegetationDensityChannel.TallGrass, 0.8f, 1f, 50f,
                new Vector2(-100f, 100f), new Vector2(0.55f, 1.05f), 0.25f, near, far, far, material,
                new Vector3(30f, 80f, 150f), 7f, UnityEngine.Rendering.ShadowCastingMode.On, true, 1);
            MapVegetationCategorySettings category = settings.Category(MapVegetationKind.Grass);
            float radius = MapVegetationGrassBindings.MeasureMaximumFootprintRadius(profile, category);
            Assert.That(radius, Is.GreaterThan(0.55f), "The old road margin cannot contain this authored clump.");
            foreach (Mesh mesh in new[] { near, far })
            foreach (Vector3 vertex in mesh.vertices)
            for (int slope = 0; slope <= 52; slope += 4)
            for (int yaw = 0; yaw < 360; yaw += 15)
            {
                Vector3 groundNormal = Quaternion.Euler(slope, 0f, 0f) * Vector3.up;
                Vector3 up = Vector3.Slerp(Vector3.up, groundNormal, profile.SurfaceNormalAlignment * category.NormalAlignment).normalized;
                Vector3 transformed = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, yaw, 0f) * vertex
                    * (profile.UniformScaleRange.y * category.UniformScaleRange.y);
                float wind = 0.065f * Mathf.Pow(Mathf.Clamp01(vertex.y), 2f);
                for (int xSign = -1; xSign <= 1; xSign += 2)
                for (int zSign = -1; zSign <= 1; zSign += 2)
                    Assert.That(new Vector2(transformed.x + xSign * wind, transformed.z + zSign * wind).magnitude,
                        Is.LessThanOrEqualTo(radius + 0.000001f));
            }
        }

        private GameObject Ground(Texture2D texture, bool canonical = true)
        {
            var result = Own(new GameObject("mask test ground"));
            var mesh = Own(new Mesh
            {
                vertices = new[] { Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 10f) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
                triangles = new[] { 0, 2, 1, 1, 2, 3 }
            });
            mesh.RecalculateBounds(); result.AddComponent<MeshFilter>().sharedMesh = mesh;
            Shader shader = Shader.Find("HDRP/Lit"); Assert.That(shader, Is.Not.Null);
            var material = Own(new Material(shader));
            material.SetTexture("_BaseColorMap", texture); material.SetColor("_BaseColor", Color.white);
            result.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (canonical) result.AddComponent<DonorWorldBaselineEntityMetadata>().Configure(
                "texture-ground-fixture", 1, string.Empty, "MAP/MESH/TERRAIN_OBJ/Grass1", "test-mesh", "cell_0_0",
                "VegetationGrass", "33,23", "SanitizedStatic", "Texture test fixture", true, true, true);
            return result;
        }
        private T Own<T>(T instance) where T : Object { owned.Add(instance); return instance; }
    }
}
