using System.Collections.Generic;
using MSC.Editor.Vegetation;
using MSC.LegacyImport;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldBaseline
{
    public sealed class MapVegetationSurfaceQueryTests
    {
        private readonly List<Object> owned = new List<Object>();
        private MapVegetationPlacementSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<MapVegetationPlacementSettings>();
            owned.Add(settings);
            // These geometry/exclusion fixtures intentionally omit materials and
            // UVs. Texture coverage has its own tests with actual sampled data.
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("grassTextureMaskEnabled").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void MeshRayPreservesXZAndResolvesSlopeAfterSceneObjectsAreDestroyed()
        {
            GameObject ground = Quad("ground", 60f, 0f, true);
            var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i].y = vertices[i].x * 0.25f + 7f;
            mesh.vertices = vertices; mesh.RecalculateBounds();
            var query = MapVegetationSurfaceQuery.Build(new[] { ground }, settings);
            Object.DestroyImmediate(ground);

            Assert.That(query.TryResolve(new Vector3(8.25f, 800f, -3.75f), MapVegetationKind.Tree, out var hit, out string reason), Is.True, reason);
            Assert.That(hit.Position.x, Is.EqualTo(8.25f));
            Assert.That(hit.Position.z, Is.EqualTo(-3.75f));
            Assert.That(hit.Position.y, Is.EqualTo(9.0625f).Within(0.0001f));
            Assert.That(Vector3.Angle(hit.Normal, new Vector3(-0.25f, 1f, 0f).normalized), Is.LessThan(0.02f));
        }

        [Test]
        public void UnknownElevatedGeometryNeverBecomesGround()
        {
            GameObject ground = Quad("ground", 50f, 0f, true);
            GameObject unknownRoof = Quad("green looking roof", 5f, 15f, false);
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, unknownRoof }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Grass, out var hit, out var reason), Is.True, reason);
            Assert.That(hit.Position.y, Is.Zero);
            var unknownOnly = MapVegetationSurfaceQuery.Build(new[] { unknownRoof }, settings);
            Assert.That(unknownOnly.TryResolve(Vector3.zero, MapVegetationKind.Grass, out _, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("NoAllowedGround"));
        }

        [Test]
        public void RoadClearanceUsesMeshEdgesAndSeparateTreeAndGrassMargins()
        {
            GameObject ground = Quad("ground", 50f, 0f, true);
            GameObject road = Quad("road", 2f, 0.05f, false);
            road.AddComponent<VegetationBlocker>().ConfigureForAuthoring(VegetationBlockerKind.GroundRoad);
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, road }, settings);

            Assert.That(query.TryResolve(new Vector3(3f, 0f, 0f), MapVegetationKind.Tree, out _, out var reason), Is.False);
            Assert.That(reason, Does.StartWith("DirtRoad:"));
            Assert.That(query.TryResolve(new Vector3(3f, 0f, 0f), MapVegetationKind.Grass, out _, out reason), Is.True, reason);
            Assert.That(query.TryResolve(new Vector3(0.5f, 0f, 0f), MapVegetationKind.Grass, out _, out _), Is.False);
        }

        [Test]
        public void ExpandedFootprintBoundsKeepExactClearanceBoundary()
        {
            GameObject ground = Quad("ground", 50f, 0f, true);
            GameObject road = Quad("road", 2f, 0.05f, false);
            road.AddComponent<VegetationBlocker>().ConfigureForAuthoring(VegetationBlockerKind.GroundRoad);
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, road }, settings);
            Assert.That(query.TryResolve(new Vector3(6.749f, 0f, 0f), MapVegetationKind.Tree, out _, out _), Is.False);
            Assert.That(query.TryResolve(new Vector3(6.75f, 0f, 0f), MapVegetationKind.Tree, out _, out _), Is.False);
            Assert.That(query.TryResolve(new Vector3(6.751f, 0f, 0f), MapVegetationKind.Tree, out _, out var reason), Is.True, reason);
            float grassEdge = 2f + settings.GetMargin(MapVegetationKind.Grass, MapVegetationExclusionKind.DirtRoad);
            Assert.That(query.TryResolve(new Vector3(0f, 0f, -grassEdge + 0.001f), MapVegetationKind.Grass, out _, out _), Is.False);
            Assert.That(query.TryResolve(new Vector3(0f, 0f, -grassEdge - 0.001f), MapVegetationKind.Grass, out _, out reason), Is.True, reason);
        }

        [Test]
        public void ClearanceEditsRequireNewSnapshotAndFingerprintUsesCapturedMargins()
        {
            GameObject ground = Quad("ground", 80f, 0f, true);
            GameObject road = Quad("road", 2f, 0.05f, false);
            road.AddComponent<VegetationBlocker>().ConfigureForAuthoring(VegetationBlockerKind.GroundRoad);
            var roots = new[] { ground, road };
            var oldQuery = MapVegetationSurfaceQuery.Build(roots, settings);
            string before = oldQuery.ComputeGeometryFingerprint();
            var serialized = new SerializedObject(settings);
            SerializedProperty rules = serialized.FindProperty("clearances");
            for (int i = 0; i < rules.arraySize; i++)
            {
                SerializedProperty rule = rules.GetArrayElementAtIndex(i);
                if (rule.FindPropertyRelative("kind").enumValueIndex == (int)MapVegetationExclusionKind.DirtRoad)
                    rule.FindPropertyRelative("treeMeters").floatValue = 40f;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var newQuery = MapVegetationSurfaceQuery.Build(roots, settings);
            // This crosses the 32m bucket boundary, testing both the cached margin
            // and the expanded spatial index constructed from the same snapshot.
            Assert.That(oldQuery.TryResolve(new Vector3(35f, 0f, 0f), MapVegetationKind.Tree, out _, out var reason), Is.True, reason);
            Assert.That(newQuery.TryResolve(new Vector3(35f, 0f, 0f), MapVegetationKind.Tree, out _, out reason), Is.False);
            Assert.That(reason, Does.StartWith("DirtRoad:"));
            Assert.That(oldQuery.ComputeGeometryFingerprint(), Is.EqualTo(before));
            Assert.That(newQuery.ComputeGeometryFingerprint(), Is.Not.EqualTo(before));
        }

        [Test]
        public void CanonicalDiagonalRailCollidersDoNotExcludeTheirGiantWorldAabb()
        {
            GameObject ground = Quad("ground", 5000f, 0f, true);
            GameObject rail = NewObject("actual RailCol dimensions");
            rail.transform.position = new Vector3(1285.2723f, 0.21104085f, -1705.8378f);
            rail.transform.rotation = new Quaternion(-0.67540944f, -0.20933713f, -0.2093374f, 0.6754095f);
            Metadata(rail, "ColliderOnly", "MAP/MESH/RAILROAD/RailCol");
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject child = NewObject("rail collision strip"); child.transform.SetParent(rail.transform, false);
                var box = child.AddComponent<BoxCollider>();
                box.size = new Vector3(3300f, 0.08f, 0.2f); box.center = new Vector3(0f, side * 0.79f, 0f);
            }
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, rail }, settings);
            Vector3 railPoint = rail.transform.TransformPoint(new Vector3(0f, 0.79f, 0f));
            Vector3 lateral = rail.transform.TransformDirection(Vector3.up).normalized;
            Assert.That(query.TryResolve(railPoint, MapVegetationKind.Grass, out _, out _), Is.False);
            Assert.That(query.TryResolve(railPoint + lateral * 4.9f, MapVegetationKind.Tree, out _, out var reason), Is.False);
            Assert.That(reason, Does.StartWith("Railway:"));
            Assert.That(query.TryResolve(railPoint + lateral * 5.2f, MapVegetationKind.Tree, out _, out reason), Is.True, reason);
            Vector3 oldAabbFalsePositive = railPoint + Vector3.right * 300f;
            Assert.That(rail.GetComponentInChildren<BoxCollider>().bounds.Contains(oldAabbFalsePositive), Is.True);
            Assert.That(query.TryResolve(oldAabbFalsePositive, MapVegetationKind.Tree, out _, out reason), Is.True, reason);
        }

        [Test]
        public void BoxExclusionProjectsParentRotationScaleAndNonzeroLocalCenter()
        {
            GameObject ground = Quad("ground", 500f, 0f, true);
            GameObject parent = NewObject("scaled rotated rail parent");
            parent.transform.SetPositionAndRotation(new Vector3(41f, 3f, -72f), Quaternion.Euler(0f, 45f, 0f));
            parent.transform.localScale = new Vector3(2f, 1f, 0.5f);
            Metadata(parent, "ColliderOnly", "MAP/MESH/RAILROAD/RailCol");
            GameObject child = NewObject("offset child rail"); child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = new Vector3(17f, 0.1f, 8f);
            child.transform.localScale = new Vector3(1.5f, 2f, 1.2f);
            var box = child.AddComponent<BoxCollider>();
            box.size = new Vector3(80f, 2f, 0.2f); box.center = new Vector3(11f, 0f, 4f);
            Vector3 center = child.transform.TransformPoint(box.center);
            Vector3 lateral = child.transform.TransformVector(Vector3.forward).normalized;
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, parent }, settings);
            Assert.That(query.TryResolve(center, MapVegetationKind.Tree, out _, out _), Is.False);
            Assert.That(query.TryResolve(center + lateral * 4.9f, MapVegetationKind.Tree, out _, out _), Is.False);
            Assert.That(box.bounds.Contains(center + lateral * 5.2f), Is.True);
            Assert.That(query.TryResolve(center + lateral * 5.2f, MapVegetationKind.Tree, out _, out var reason), Is.True, reason);
        }

        [Test]
        public void BridgeDeckExcludesWholeColumnEvenWhenOldPainterMarkerAllowsBelow()
        {
            GameObject ground = Quad("ground", 50f, 0f, true);
            GameObject bridge = Quad("bridge", 2f, 20f, false);
            bridge.AddComponent<VegetationBlocker>().ConfigureForAuthoring(VegetationBlockerKind.BridgeDeck, allowBelowBridgeDeck: true);
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, bridge }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Tree, out _, out var reason), Is.False);
            Assert.That(reason, Does.StartWith("Bridge:"));
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Grass, out _, out _), Is.False);
        }

        [TestCase("Field", "MAP/MESH/TERRAIN_OBJ/Fields", MapVegetationExclusionKind.AgriculturalField)]
        [TestCase("Field", "MAP/MESH/TRACKFIELD", MapVegetationExclusionKind.AgriculturalField)]
        [TestCase("StaticProp", "MAP/LakeSimple/Tile (5)", MapVegetationExclusionKind.Water)]
        [TestCase("StaticProp", "MAP/LakeNice/Lake/Tile", MapVegetationExclusionKind.Water)]
        [TestCase("", "MAP/MESH/TERRAIN_OBJ/DIRTROAD", MapVegetationExclusionKind.DirtRoad)]
        [TestCase("", "MAP/RAILROAD/Track", MapVegetationExclusionKind.Railway)]
        [TestCase("Roof", "MAP/HOME/Roof", MapVegetationExclusionKind.Building)]
        public void CanonicalForbiddenGeometryBlocksEveryVegetationCategory(string category, string path, MapVegetationExclusionKind expected)
        {
            GameObject ground = Quad("ground", 60f, 0f, true);
            GameObject obstacle = Quad("canonical evidence", 2f, 1f, false);
            Metadata(obstacle, category, path);
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, obstacle }, settings);
            foreach (MapVegetationKind kind in new[] { MapVegetationKind.Tree, MapVegetationKind.Shrub, MapVegetationKind.Grass })
            {
                Assert.That(query.TryResolve(Vector3.zero, kind, out _, out var reason), Is.False);
                Assert.That(reason, Does.StartWith(expected + ":"));
            }
        }

        [TestCase("MAP/MESH/TERRAIN_OBJ/Grass1")]
        [TestCase("MAP/MESH/TERRAIN_OBJ/Grass2")]
        [TestCase("MAP/SkijumpHill/grass")]
        public void AuditedCanonicalNaturalSurfacesOptInWithoutNameOnlySearch(string path)
        {
            GameObject ground = Quad("arbitrary current object name", 10f, 3f, false);
            Metadata(ground, "VegetationGrass", path);
            var query = MapVegetationSurfaceQuery.Build(new[] { ground }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Grass, out var hit, out var reason), Is.True, reason);
            Assert.That(hit.Position.y, Is.EqualTo(3f));
            Assert.That(hit.Source, Does.Contain(path));
        }

        [Test]
        public void UnderMapWaterProxyDoesNotExcludeDryGround()
        {
            GameObject ground = Quad("ground", 40f, 0f, true);
            GameObject proxy = Quad("water colour helper", 50f, -10f, false);
            Metadata(proxy, "Water", "MAP/LakeWaterUnder/LakeBed");
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, proxy }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Grass, out _, out var reason), Is.True, reason);
        }

        [Test]
        public void ExtendedRealWaterTileDoesNotExcludeDryGroundAboveItsPlane()
        {
            GameObject ground = Quad("raised natural ground", 60f, 10f, true);
            GameObject water = Quad("real lake tile extending under land", 100f, 0f, false);
            Metadata(water, "StaticProp", "MAP/LakeNice/Lake/Tile");
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, water }, settings);
            foreach (MapVegetationKind kind in new[] { MapVegetationKind.Tree, MapVegetationKind.Shrub, MapVegetationKind.Grass })
            {
                Assert.That(query.TryResolve(Vector3.zero, kind, out var hit, out var reason), Is.True, reason);
                Assert.That(hit.Position.y, Is.EqualTo(10f));
            }
        }

        [Test]
        public void ExtendedWaterTileStillExcludesSubmergedGround()
        {
            GameObject ground = Quad("underwater bed", 60f, -1f, true);
            GameObject water = Quad("real water", 100f, 0f, false);
            Metadata(water, "StaticProp", "MAP/LakeNice/Lake/Tile");
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, water }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Grass, out _, out var reason), Is.False);
            Assert.That(reason, Does.StartWith("Water:"));
        }

        [Test]
        public void ShorelineMarginComesFromGroundWaterIntersectionInsideExtendedQuad()
        {
            GameObject ground = Quad("continuous natural bank", 60f, 0f, true);
            SetSlope(ground, 0.2f, 0f);
            GameObject water = Quad("water spans both banks", 100f, 0f, false);
            Metadata(water, "StaticProp", "MAP/LakeNice/Lake/Tile");
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, water }, settings);
            Assert.That(query.TryResolve(new Vector3(-5f, 0f, 0f), MapVegetationKind.Tree, out _, out _), Is.False);
            Assert.That(query.TryResolve(new Vector3(2f, 0f, 0f), MapVegetationKind.Tree, out _, out var reason), Is.False);
            Assert.That(reason, Does.StartWith("Water:"));
            Assert.That(query.TryResolve(new Vector3(2f, 0f, 0f), MapVegetationKind.Grass, out _, out reason), Is.True, reason);
            Assert.That(query.TryResolve(new Vector3(6f, 0f, 0f), MapVegetationKind.Tree, out _, out reason), Is.True, reason);
        }

        [Test]
        public void ShorelineUsesInterpolatedTiltedWaterHeightRatherThanWholeMeshMaximum()
        {
            GameObject ground = Quad("flat elevated shore", 60f, 3f, true);
            GameObject water = Quad("tilted water test surface", 100f, 0f, false);
            SetSlope(water, 0f, 0.2f);
            Metadata(water, "StaticProp", "MAP/LakeSimple/Tile");
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, water }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Tree, out _, out var reason), Is.True, reason);
            Assert.That(query.TryResolve(new Vector3(0f, 0f, 20f), MapVegetationKind.Grass, out _, out _), Is.False);
            Assert.That(query.TryResolve(new Vector3(0f, 0f, 13f), MapVegetationKind.Tree, out _, out _), Is.False);
            Assert.That(query.TryResolve(new Vector3(0f, 0f, 13f), MapVegetationKind.Grass, out _, out reason), Is.True, reason);
        }

        [Test]
        public void WaterEdgeClearanceRemainsWhenPermittedGroundContinuesOutsideTile()
        {
            GameObject ground = Quad("ground beside finite lake", 60f, -1f, true);
            GameObject water = Quad("finite real water", 10f, 0f, false);
            Metadata(water, "StaticProp", "MAP/LakeSimple/Tile");
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, water }, settings);
            Assert.That(query.TryResolve(new Vector3(11f, 0f, 0f), MapVegetationKind.Tree, out _, out var reason), Is.False);
            Assert.That(reason, Does.StartWith("Water:"));
            Assert.That(query.TryResolve(new Vector3(11f, 0f, 0f), MapVegetationKind.Grass, out _, out reason), Is.True, reason);
            Assert.That(query.TryResolve(new Vector3(16f, 0f, 0f), MapVegetationKind.Tree, out _, out reason), Is.True, reason);
        }

        [Test]
        public void SubmergedLowerGroundDoesNotInventWaterOnHigherDryGround()
        {
            GameObject lower = Quad("lower submerged mesh", 60f, -1f, true);
            GameObject upper = Quad("upper dry natural mesh", 60f, 10f, true);
            GameObject water = Quad("water below upper mesh", 100f, 0f, false);
            Metadata(water, "StaticProp", "MAP/LakeNice/Lake/Tile");
            var query = MapVegetationSurfaceQuery.Build(new[] { lower, upper, water }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Tree, out _, out var reason), Is.True, reason);
        }

        [Test]
        public void ElevatedBankKeepsBufferWhereGroundEndsAboveExposedWater()
        {
            GameObject ground = Quad("steep dry island bank", 10f, 8f, true);
            GameObject water = Quad("water without whitelisted lake bed", 100f, 0f, false);
            Metadata(water, "StaticProp", "MAP/LakeNice/Lake/Tile");
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, water }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Tree, out _, out var reason), Is.True, reason);
            Assert.That(query.TryResolve(new Vector3(8f, 0f, 0f), MapVegetationKind.Tree, out _, out reason), Is.False);
            Assert.That(reason, Does.StartWith("Water:"));
            Assert.That(query.TryResolve(new Vector3(8f, 0f, 0f), MapVegetationKind.Grass, out _, out reason), Is.True, reason);
        }

        [Test]
        public void DrySurfaceOverSubmergedMeshRetainsBankBufferAtItsExposedEdge()
        {
            GameObject lower = Quad("submerged underlying mesh", 60f, -1f, true);
            GameObject upper = Quad("dry bank overlay", 10f, 8f, true);
            GameObject water = Quad("real water around bank", 100f, 0f, false);
            Metadata(water, "StaticProp", "MAP/LakeNice/Lake/Tile");
            var query = MapVegetationSurfaceQuery.Build(new[] { lower, upper, water }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Tree, out _, out var reason), Is.True, reason);
            Assert.That(query.TryResolve(new Vector3(8f, 0f, 0f), MapVegetationKind.Tree, out _, out reason), Is.False);
            Assert.That(reason, Does.StartWith("Water:"));
        }

        [Test]
        public void TerrainShorelinePreservesDryLandAndPerCategoryBankMargin()
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 10f, 32f) };
            owned.Add(data);
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = 0.1f + x * 0.025f;
            data.SetHeights(0, 0, heights);
            GameObject terrain = Terrain.CreateTerrainGameObject(data); owned.Add(terrain);
            terrain.transform.position = new Vector3(-16f, -5f, -16f);
            terrain.AddComponent<VegetationSurface>();
            GameObject water = Quad("water under terrain shore", 100f, 0f, false);
            Metadata(water, "StaticProp", "MAP/LakeNice/Lake/Tile");
            var query = MapVegetationSurfaceQuery.Build(new[] { terrain, water }, settings);
            Assert.That(query.TryResolve(new Vector3(2f, 0f, 0f), MapVegetationKind.Tree, out _, out _), Is.False);
            Assert.That(query.TryResolve(new Vector3(2f, 0f, 0f), MapVegetationKind.Grass, out _, out var reason), Is.True, reason);
            Assert.That(query.TryResolve(new Vector3(6f, 0f, 0f), MapVegetationKind.Tree, out _, out reason), Is.True, reason);
            Assert.That(query.TryResolve(new Vector3(-2f, 0f, 0f), MapVegetationKind.Grass, out _, out _), Is.False);
        }

        [Test]
        public void ExclusionUsesTriangleFootprintInsteadOfWholeAabb()
        {
            GameObject ground = Quad("ground", 100f, 0f, true);
            GameObject road = NewObject("triangular road");
            var mesh = new Mesh { vertices = new[] { new Vector3(0f, 1f, 0f), new Vector3(20f, 1f, 0f), new Vector3(0f, 1f, 20f) }, triangles = new[] { 0, 2, 1 } };
            owned.Add(mesh); road.AddComponent<MeshFilter>().sharedMesh = mesh;
            road.AddComponent<VegetationBlocker>().ConfigureForAuthoring(VegetationBlockerKind.GroundRoad);
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, road }, settings);
            Assert.That(query.TryResolve(new Vector3(18f, 0f, 18f), MapVegetationKind.Tree, out _, out var reason), Is.True, reason);
            Assert.That(query.TryResolve(new Vector3(4f, 0f, 4f), MapVegetationKind.Grass, out _, out _), Is.False);
        }

        [Test]
        public void ManualVolumeRetainsRotationScaleAndChannelMaskAfterRootUnload()
        {
            GameObject ground = Quad("ground", 80f, 0f, true);
            GameObject volumeObject = NewObject("authored gameplay protection");
            var volume = volumeObject.AddComponent<VegetationExclusionVolume>();
            volumeObject.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            volumeObject.transform.localScale = new Vector3(2f, 1f, 1f);
            var serialized = new SerializedObject(volume);
            serialized.FindProperty("size").vector3Value = new Vector3(10f, 10f, 1f);
            serialized.FindProperty("channelMask").intValue = 1 << (int)VegetationDensityChannel.Decorative;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Vector3 candidate = volumeObject.transform.TransformPoint(new Vector3(4f, 0f, 0f));
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, volumeObject }, settings);
            Object.DestroyImmediate(volumeObject);
            Assert.That(query.TryResolve(candidate, MapVegetationKind.Tree, out _, out var reason), Is.False);
            Assert.That(reason, Does.StartWith("ExclusionVolume:"));
            Assert.That(query.TryResolve(candidate, MapVegetationKind.Grass, out _, out reason), Is.True, reason);
        }

        [Test]
        public void DeniedMaterialWinsOverGroundMarkerForItsSubmeshOnly()
        {
            GameObject ground = NewObject("multi material ground");
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-10f, 0f, -10f), new Vector3(10f, 0f, -10f), new Vector3(-10f, 0f, 10f), new Vector3(10f, 0f, 10f) },
                subMeshCount = 2
            };
            mesh.SetTriangles(new[] { 0, 2, 1 }, 0); mesh.SetTriangles(new[] { 1, 2, 3 }, 1);
            owned.Add(mesh); ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var allowed = new Material(Shader.Find("Hidden/InternalErrorShader")); var denied = new Material(allowed);
            owned.Add(allowed); owned.Add(denied);
            ground.AddComponent<MeshRenderer>().sharedMaterials = new[] { allowed, denied };
            ground.AddComponent<VegetationSurface>();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("disallowedMaterials").arraySize = 1;
            serialized.FindProperty("disallowedMaterials").GetArrayElementAtIndex(0).objectReferenceValue = denied;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var query = MapVegetationSurfaceQuery.Build(new[] { ground }, settings);
            Assert.That(query.TryResolve(new Vector3(-8f, 0f, -8f), MapVegetationKind.Grass, out _, out var reason), Is.True, reason);
            Assert.That(query.TryResolve(new Vector3(8f, 0f, 8f), MapVegetationKind.Grass, out _, out _), Is.False);
        }

        [Test]
        public void SlopeLimitDoesNotFallThroughToLowerFlatterSurface()
        {
            GameObject lower = Quad("low flat ground", 20f, 0f, true);
            GameObject steep = Quad("steep upper ground", 20f, 50f, true);
            var mesh = steep.GetComponent<MeshFilter>().sharedMesh; Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i].y += vertices[i].x * 2f;
            mesh.vertices = vertices;
            var query = MapVegetationSurfaceQuery.Build(new[] { lower, steep }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Tree, out _, out var reason), Is.False);
            Assert.That(reason, Does.StartWith("ExcessiveSlope:"));
        }

        [Test]
        public void MeadowOnlyGroundRetainsItsAuthoredProfileChannel()
        {
            GameObject ground = Quad("meadow only", 20f, 0f, true);
            ground.GetComponent<VegetationSurface>().ConfigureForAuthoring(false, true, false, false, 70f);
            var query = MapVegetationSurfaceQuery.Build(new[] { ground }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Grass, out var hit, out var reason), Is.True, reason);
            Assert.That(hit.AllowsChannel(VegetationDensityChannel.MeadowGrass), Is.True);
            Assert.That(hit.AllowsChannel(VegetationDensityChannel.ShortGrass), Is.False);
            Assert.That(query.TryResolveGrass(Vector3.zero, VegetationDensityChannel.MeadowGrass, out _, out reason), Is.True, reason);
            Assert.That(query.TryResolveGrass(Vector3.zero, VegetationDensityChannel.ShortGrass, out _, out _), Is.False);
            long resolveCalls = query.ResolveCallCount;
            Assert.That(query.AllowsGrassChannel(hit, VegetationDensityChannel.MeadowGrass, out reason), Is.True, reason);
            Assert.That(query.AllowsGrassChannel(hit, VegetationDensityChannel.ShortGrass, out _), Is.False);
            Assert.That(query.ResolveCallCount, Is.EqualTo(resolveCalls), "Profile selection must reuse the resolved surface.");
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Tree, out _, out _), Is.False);
        }

        [Test]
        public void MeadowExclusionAllowsFallbackToShortGrassProfile()
        {
            GameObject ground = Quad("all grass ground", 20f, 0f, true);
            GameObject volumeObject = NewObject("meadow exclusion");
            var volume = volumeObject.AddComponent<VegetationExclusionVolume>();
            var serialized = new SerializedObject(volume);
            serialized.FindProperty("channelMask").intValue = 1 << (int)VegetationDensityChannel.MeadowGrass;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var query = MapVegetationSurfaceQuery.Build(new[] { ground, volumeObject }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Grass, out var hit, out var reason), Is.True, reason);
            Assert.That(query.TryResolveGrass(Vector3.zero, VegetationDensityChannel.MeadowGrass, out _, out reason), Is.False);
            Assert.That(reason, Does.StartWith("ExclusionVolume:"));
            Assert.That(query.TryResolveGrass(Vector3.zero, VegetationDensityChannel.ShortGrass, out _, out reason), Is.True, reason);
            long resolveCalls = query.ResolveCallCount;
            Assert.That(query.AllowsGrassChannel(hit, VegetationDensityChannel.MeadowGrass, out reason), Is.False);
            Assert.That(reason, Does.StartWith("ExclusionVolume:"));
            Assert.That(query.AllowsGrassChannel(hit, VegetationDensityChannel.ShortGrass, out reason), Is.True, reason);
            Assert.That(query.ResolveCallCount, Is.EqualTo(resolveCalls));
        }

        [Test]
        public void TerrainRespectsExplicitOptInHeightAndHoles()
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 10f, 32f) };
            owned.Add(data);
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = 0.3f;
            data.SetHeights(0, 0, heights);
            GameObject terrain = Terrain.CreateTerrainGameObject(data); owned.Add(terrain);
            terrain.transform.position = new Vector3(-16f, 4f, -16f);
            var unmarked = MapVegetationSurfaceQuery.Build(new[] { terrain }, settings);
            Assert.That(unmarked.TryResolve(Vector3.zero, MapVegetationKind.Grass, out _, out _), Is.False);
            terrain.AddComponent<VegetationSurface>();
            var query = MapVegetationSurfaceQuery.Build(new[] { terrain }, settings);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Grass, out var hit, out var reason), Is.True, reason);
            Assert.That(hit.Position.y, Is.EqualTo(7f).Within(0.001f));
            string originalFingerprint = query.ComputeGeometryFingerprint();
            var holes = new bool[data.holesResolution, data.holesResolution];
            data.SetHoles(0, 0, holes);
            Assert.That(query.TryResolve(Vector3.zero, MapVegetationKind.Grass, out _, out _), Is.True, "An existing query owns an immutable terrain snapshot.");
            Assert.That(query.ComputeGeometryFingerprint(), Is.EqualTo(originalFingerprint));
            var updated = MapVegetationSurfaceQuery.Build(new[] { terrain }, settings);
            Assert.That(updated.TryResolve(Vector3.zero, MapVegetationKind.Grass, out _, out _), Is.False);
            Assert.That(updated.ComputeGeometryFingerprint(), Is.Not.EqualTo(originalFingerprint));
        }

        [Test]
        public void TerrainSnapshotSurvivesDestroyedNativeAssetWithHeightNormalAndWaterBuffer()
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 10f, 32f) };
            owned.Add(data);
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = 0.1f + x * 0.025f;
            data.SetHeights(0, 0, heights);
            GameObject terrain = Terrain.CreateTerrainGameObject(data); owned.Add(terrain);
            terrain.transform.position = new Vector3(-16f, -5f, -16f);
            terrain.AddComponent<VegetationSurface>();
            GameObject water = Quad("water beneath copied terrain", 100f, 0f, false);
            Metadata(water, "StaticProp", "MAP/LakeNice/Lake/Tile");
            var query = MapVegetationSurfaceQuery.Build(new[] { terrain, water }, settings);
            string fingerprint = query.ComputeGeometryFingerprint();
            Object.DestroyImmediate(terrain); Object.DestroyImmediate(data); Object.DestroyImmediate(water);

            Assert.That(query.TryResolve(new Vector3(6f, 500f, 0f), MapVegetationKind.Tree, out var hit, out var reason), Is.True, reason);
            Assert.That(hit.Position.y, Is.EqualTo(1.5f).Within(0.002f));
            Assert.That(Vector3.Angle(hit.Normal, new Vector3(-0.25f, 1f, 0f).normalized), Is.LessThan(0.1f));
            Assert.That(query.TryResolve(new Vector3(2f, 0f, 0f), MapVegetationKind.Tree, out _, out reason), Is.False);
            Assert.That(reason, Does.StartWith("Water:"));
            Assert.That(query.TryResolve(new Vector3(-2f, 0f, 0f), MapVegetationKind.Grass, out _, out _), Is.False);
            Assert.That(query.ComputeGeometryFingerprint(), Is.EqualTo(fingerprint));
        }

        [Test]
        public void GeometryFingerprintIgnoresRootOrderAndShorelineCachePopulation()
        {
            GameObject ground = Quad("natural bank", 60f, 0f, true); SetSlope(ground, 0.2f, 0f);
            GameObject water = Quad("extended water tile", 100f, 0f, false);
            Metadata(water, "StaticProp", "MAP/LakeNice/Lake/Tile");
            GameObject excluded = NewObject("channel volume"); excluded.AddComponent<VegetationExclusionVolume>();
            var first = MapVegetationSurfaceQuery.Build(new[] { ground, water, excluded }, settings);
            var second = MapVegetationSurfaceQuery.Build(new[] { excluded }, settings);
            second.AddRoots(new[] { water }); second.AddRoots(new[] { ground });
            string expected = first.ComputeGeometryFingerprint();
            Assert.That(second.ComputeGeometryFingerprint(), Is.EqualTo(expected));
            first.TryResolve(new Vector3(2f, 0f, 0f), MapVegetationKind.Tree, out _, out _);
            first.TryResolve(new Vector3(6f, 0f, 0f), MapVegetationKind.Grass, out _, out _);
            Assert.That(first.ComputeGeometryFingerprint(), Is.EqualTo(expected));
        }

        [Test]
        public void GeometryFingerprintDetectsGroundWaterAndChannelEditsWithoutMutatingOldSnapshot()
        {
            GameObject ground = Quad("ground", 60f, 10f, true);
            GameObject water = Quad("water", 100f, 0f, false);
            Metadata(water, "StaticProp", "MAP/LakeNice/Lake/Tile");
            GameObject excluded = NewObject("channel volume");
            var volume = excluded.AddComponent<VegetationExclusionVolume>();
            var roots = new[] { ground, water, excluded };
            var first = MapVegetationSurfaceQuery.Build(roots, settings);
            string before = first.ComputeGeometryFingerprint();
            SetSlope(ground, 0.1f, 0f);
            string groundChanged = MapVegetationSurfaceQuery.Build(roots, settings).ComputeGeometryFingerprint();
            Assert.That(groundChanged, Is.Not.EqualTo(before));
            water.transform.position = Vector3.up;
            string waterChanged = MapVegetationSurfaceQuery.Build(roots, settings).ComputeGeometryFingerprint();
            Assert.That(waterChanged, Is.Not.EqualTo(groundChanged));
            var serialized = new SerializedObject(volume);
            serialized.FindProperty("channelMask").intValue = 1 << (int)VegetationDensityChannel.MeadowGrass;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string channelChanged = MapVegetationSurfaceQuery.Build(roots, settings).ComputeGeometryFingerprint();
            Assert.That(channelChanged, Is.Not.EqualTo(waterChanged));
            Assert.That(first.ComputeGeometryFingerprint(), Is.EqualTo(before));
        }

        private GameObject Quad(string name, float halfSize, float y, bool mark)
        {
            GameObject result = NewObject(name);
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-halfSize, y, -halfSize), new Vector3(halfSize, y, -halfSize), new Vector3(-halfSize, y, halfSize), new Vector3(halfSize, y, halfSize) },
                triangles = new[] { 0, 2, 1, 1, 2, 3 }
            };
            owned.Add(mesh); mesh.RecalculateBounds(); result.AddComponent<MeshFilter>().sharedMesh = mesh;
            if (mark) result.AddComponent<VegetationSurface>();
            return result;
        }

        private GameObject NewObject(string name) { var result = new GameObject(name); owned.Add(result); return result; }

        private static void SetSlope(GameObject target, float xSlope, float zSlope)
        {
            Mesh mesh = target.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i].y += vertices[i].x * xSlope + vertices[i].z * zSlope;
            mesh.vertices = vertices; mesh.RecalculateBounds();
        }

        private static void Metadata(GameObject target, string category, string path)
        {
            target.AddComponent<DonorWorldBaselineEntityMetadata>().Configure(
                "query-test-id-" + path, 1, string.Empty, path, "query-test-mesh", "cell_0_0", category,
                "33,23", "SanitizedStatic", "Test fixture", true, true, true);
        }
    }
}
