using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSCMapMigration.Tests
{
    public sealed class MapMigrationEditModeTests
    {
        [Test]
        public void Classifier_PrioritizesAuthoritativeRoadHierarchy()
        {
            MapClassificationResult result = MapMeshClassifier.Classify(
                new MapClassificationEvidence
                {
                    ProvenancePath = "MAP/MESH/TERRAIN_OBJ/DirtRoad",
                    MaterialText = "ground_grass",
                    UpwardTriangleRatio = 1f,
                    VerticalRange = 2f
                });

            Assert.That(result.Category, Is.EqualTo(MapMeshCategory.RoadDirtOrGravel));
            StringAssert.Contains("hierarchy", result.Reason);
        }

        [Test]
        public void Classifier_RetainsUnsupportedGeometryAsResidual()
        {
            MapClassificationResult result = MapMeshClassifier.Classify(
                new MapClassificationEvidence
                {
                    ObjectName = "UnknownRock",
                    MeshName = "mesh_unnamed",
                    UpwardTriangleRatio = 0.2f,
                    VerticalRange = 8f,
                    HasMultipleHeightsAtSameXZ = true
                });

            Assert.That(result.Category, Is.EqualTo(MapMeshCategory.ResidualUnsupported));
        }

        [Test]
        public void Classifier_TrafficSignOverridesCoarseRoadSemantic()
        {
            MapClassificationResult result = MapMeshClassifier.Classify(
                new MapClassificationEvidence
                {
                    ProvenancePath = "MAP/TrafficSigns/3/sign_pole 10/sign_road1",
                    SemanticCategory = "Road",
                    MaterialText = "trafficsigns",
                    UpwardTriangleRatio = 0.2f
                });

            Assert.That(result.Category, Is.EqualTo(MapMeshCategory.Prop));
            StringAssert.Contains("traffic-sign", result.Reason);
        }

        [Test]
        public void Classifier_RetainsRockOverlayOutsideBaseTerrain()
        {
            MapClassificationResult result = MapMeshClassifier.Classify(
                new MapClassificationEvidence
                {
                    ProvenancePath = "MAP/TERRAIN OBJ/Rockwall",
                    SemanticCategory = "Terrain",
                    UpwardTriangleRatio = 0.3f,
                    HasMultipleHeightsAtSameXZ = true
                });

            Assert.That(result.Category, Is.EqualTo(MapMeshCategory.ResidualUnsupported));
        }

        [TestCase("MAP/MESH/RAILROAD")]
        [TestCase("MAP/MESH/road_lines")]
        public void Classifier_RetainsRoadOverlaysOutsideRoadBedConstraints(string path)
        {
            MapClassificationResult result = MapMeshClassifier.Classify(
                new MapClassificationEvidence
                {
                    ProvenancePath = path,
                    SemanticCategory = "Road",
                    UpwardTriangleRatio = 1f
                });

            Assert.That(result.Category, Is.EqualTo(MapMeshCategory.RoadStructure));
        }

        [Test]
        public void CoordinateTransform_AppliesWorldMatrixExactlyOnce()
        {
            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(10f, 2f, -3f),
                Quaternion.Euler(0f, 90f, 0f),
                new Vector3(2f, 1f, 1f));

            Vector3 transformed = ObjExporter.TransformVertex(matrix, Vector3.right);

            Assert.That(transformed.x, Is.EqualTo(10f).Within(1e-5f));
            Assert.That(transformed.y, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(transformed.z, Is.EqualTo(-5f).Within(1e-5f));
        }

        [Test]
        public void BarycentricSampling_InterpolatesTriangleHeight()
        {
            var triangle = new SurfaceTriangle(
                new Vector3(0f, 0f, 0f),
                new Vector3(2f, 2f, 0f),
                new Vector3(0f, 4f, 2f),
                "ground",
                MapMeshCategory.GroundCandidate);

            bool sampled = triangle.TrySample(0.5f, 0.5f, out float height);

            Assert.That(sampled, Is.True);
            Assert.That(height, Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void TerrainBorders_CompareWithoutSeam()
        {
            var left = new float[3, 3]
            {
                { 0f, 0.1f, 0.2f },
                { 0f, 0.2f, 0.3f },
                { 0f, 0.3f, 0.4f }
            };
            var right = new float[3, 3]
            {
                { 0.2f, 0.4f, 0.5f },
                { 0.3f, 0.5f, 0.6f },
                { 0.4f, 0.6f, 0.7f }
            };

            Assert.That(TerrainGridBuilder.CompareVerticalBorder(left, right), Is.Zero);
        }

        [Test]
        public void ObjExport_NegativeScaleReversesWinding()
        {
            Matrix4x4 matrix = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

            Assert.That(ObjExporter.ShouldReverseWinding(matrix), Is.True);
            Assert.That(ObjExporter.ShouldReverseWinding(Matrix4x4.identity), Is.False);
        }

        [Test]
        public void RoadTransform_PreservationUsesToleranceWithoutMutation()
        {
            Matrix4x4 original = Matrix4x4.TRS(
                new Vector3(100f, 5f, -200f),
                Quaternion.Euler(2f, 35f, 0f),
                Vector3.one);
            Matrix4x4 copy = original;

            Assert.That(MapMigrationValidator.MatricesEqual(original, copy, 1e-6f), Is.True);
            copy.m03 += 0.1f;
            Assert.That(MapMigrationValidator.MatricesEqual(original, copy, 1e-6f), Is.False);
        }

        [Test]
        public void RoadClearance_LowersTerrainBelowRoad()
        {
            float terrainHeight = RoadConstraintBuilder.ApplyClearance(12f, 0.075f);

            Assert.That(terrainHeight, Is.EqualTo(11.925f).Within(1e-6f));
            Assert.That(terrainHeight, Is.LessThan(12f));
        }

        [Test]
        public void RoadConstraint_UsesFullWeightAcrossSampleInterpolationBand()
        {
            Assert.That(
                RoadConstraintBuilder.ConstraintWeight(2.5f, 4f, 2f),
                Is.EqualTo(1f));
            Assert.That(RoadConstraintBuilder.ConstraintWeight(4f, 4f, 2f), Is.Zero);
        }

        [Test]
        public void RoadConstraint_ChoosesLowerOverlappingRoadBedSurface()
        {
            Assert.That(
                RoadConstraintBuilder.ShouldReplaceConstraint(0f, 5f, 0f, 5.5f),
                Is.True);
            Assert.That(
                RoadConstraintBuilder.ShouldReplaceConstraint(0f, 6f, 0f, 5.5f),
                Is.False);
        }

        [Test]
        public void RoadConstraint_DoesNotRejectAuthoritativeRoadBedByHeightDelta()
        {
            MapMigrationSettings settings = ScriptableObject.CreateInstance<MapMigrationSettings>();
            try
            {
                var ground = new List<SurfaceTriangle>
                {
                    new SurfaceTriangle(
                        new Vector3(0f, 10f, 0f),
                        new Vector3(10f, 10f, 0f),
                        new Vector3(0f, 11f, 10f),
                        "ground",
                        MapMeshCategory.GroundCandidate)
                };
                TerrainGridDomain domain = TerrainGridDomain.Create(ground, settings);
                int count = checked(domain.SampleCountX * domain.SampleCountZ);
                var heights = new float[count];
                var valid = new bool[count];
                for (int index = 0; index < count; index++)
                {
                    heights[index] = 10f;
                    valid[index] = true;
                }

                var result = new TerrainSamplingResult(
                    domain,
                    heights,
                    valid,
                    new bool[count]);
                var road = new List<SurfaceTriangle>
                {
                    new SurfaceTriangle(
                        new Vector3(0f, 0f, 0f),
                        new Vector3(4f, 0f, 0f),
                        new Vector3(0f, 0f, 4f),
                        "road",
                        MapMeshCategory.RoadAsphalt)
                };

                RoadConstraintBuilder.Apply(result, road, settings);

                Assert.That(
                    result.Heights[result.Index(0, 0)],
                    Is.LessThanOrEqualTo(-settings.RoadClearance));
                Assert.That(result.RejectedRoadConstraintCount, Is.Zero);
                Vector3 centroid = (road[0].A + road[0].B + road[0].C) / 3f;
                Assert.That(
                    RoadConstraintBuilder.TrySampleHeightfield(
                        result,
                        centroid.x,
                        centroid.z,
                        out float continuousHeight),
                    Is.True);
                Assert.That(
                    continuousHeight,
                    Is.LessThanOrEqualTo(centroid.y - settings.RoadClearance));
                int cellX = Mathf.FloorToInt(
                    (centroid.x - domain.Minimum.x) / domain.SampleSpacing);
                int cellZ = Mathf.FloorToInt(
                    (centroid.z - domain.Minimum.z) / domain.SampleSpacing);
                foreach (int corner in new[]
                         {
                             result.Index(cellX, cellZ),
                             result.Index(cellX + 1, cellZ),
                             result.Index(cellX, cellZ + 1),
                             result.Index(cellX + 1, cellZ + 1)
                         })
                {
                    Assert.That(
                        result.Heights[corner],
                        Is.LessThanOrEqualTo(centroid.y - settings.RoadClearance));
                }
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void RoadConstraint_SeparatesVerticallyOverlappingRoadStructure()
        {
            var triangles = new List<SurfaceTriangle>
            {
                new SurfaceTriangle(
                    new Vector3(0f, 0f, 0f),
                    new Vector3(4f, 0f, 0f),
                    new Vector3(0f, 0f, 4f),
                    "lower",
                    MapMeshCategory.RoadAsphalt),
                new SurfaceTriangle(
                    new Vector3(0f, 3f, 0f),
                    new Vector3(4f, 3f, 0f),
                    new Vector3(0f, 3f, 4f),
                    "upper",
                    MapMeshCategory.RoadAsphalt)
            };

            List<SurfaceTriangle> roadBed =
                RoadConstraintBuilder.FilterRoadBedTriangles(triangles, out int elevated);

            Assert.That(elevated, Is.EqualTo(1));
            Assert.That(roadBed.Count, Is.EqualTo(1));
            Assert.That(roadBed[0].RecordId, Is.EqualTo("lower"));
        }

        [Test]
        public void Rerun_UsesStableUniqueTileNames()
        {
            var first = new HashSet<string>();
            var second = new HashSet<string>();
            for (int z = 0; z < 9; z++)
            {
                for (int x = 0; x < 11; x++)
                {
                    first.Add(TerrainGridBuilder.TileName(x, z));
                    second.Add(TerrainGridBuilder.TileName(x, z));
                }
            }

            Assert.That(first.Count, Is.EqualTo(99));
            Assert.That(second, Is.EquivalentTo(first));
        }

        [Test]
        public void ResolutionSelection_RespectsPowerOfTwoPlusOneAndSampleBudget()
        {
            int resolution = TerrainGridDomain.SelectResolution(
                1024f,
                2f,
                11,
                9,
                30000000);

            Assert.That(resolution, Is.EqualTo(513));
            Assert.That((resolution - 1) & (resolution - 2), Is.Zero);
        }

        [Test]
        public void Smoothing_ReusesFixedNeighborBufferAcrossFullTile()
        {
            MapMigrationSettings settings = ScriptableObject.CreateInstance<MapMigrationSettings>();
            try
            {
                var triangles = new List<SurfaceTriangle>
                {
                    new SurfaceTriangle(
                        new Vector3(0f, 0f, 0f),
                        new Vector3(10f, 1f, 0f),
                        new Vector3(0f, 2f, 10f),
                        "ground",
                        MapMeshCategory.GroundCandidate)
                };
                TerrainGridDomain domain = TerrainGridDomain.Create(triangles, settings);
                int count = checked(domain.SampleCountX * domain.SampleCountZ);
                var heights = new float[count];
                var valid = new bool[count];
                for (int index = 0; index < count; index++)
                {
                    heights[index] = (index % 17) * 0.01f;
                    valid[index] = true;
                }

                var result = new TerrainSamplingResult(
                    domain,
                    heights,
                    valid,
                    new bool[count]);

                TerrainHeightSmoother.Smooth(result, settings);

                Assert.That(result.MaximumSmoothingDisplacement, Is.LessThanOrEqualTo(
                    settings.MaximumAllowedSmoothingDisplacement + 1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void NeighborConnector_RestoresReciprocalTerrainLinks()
        {
            var root = new GameObject("terrain-grid-test");
            var data = new List<TerrainData>();
            try
            {
                var terrains = new Terrain[4];
                for (int index = 0; index < terrains.Length; index++)
                {
                    var terrainData = new TerrainData();
                    data.Add(terrainData);
                    GameObject tile = Terrain.CreateTerrainGameObject(terrainData);
                    tile.transform.SetParent(root.transform);
                    terrains[index] = tile.GetComponent<Terrain>();
                }

                MapMigrationTerrainNeighborConnector connector =
                    root.AddComponent<MapMigrationTerrainNeighborConnector>();
                Assert.That(connector.Apply(), Is.False);
                connector.Configure(2, 2, terrains);

                Assert.That(terrains[0].rightNeighbor, Is.SameAs(terrains[1]));
                Assert.That(terrains[1].leftNeighbor, Is.SameAs(terrains[0]));
                Assert.That(terrains[0].topNeighbor, Is.SameAs(terrains[2]));
                Assert.That(terrains[2].bottomNeighbor, Is.SameAs(terrains[0]));
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (TerrainData terrainData in data)
                {
                    Object.DestroyImmediate(terrainData);
                }
            }
        }

        [Test]
        public void RuntimeMarkers_SurviveSceneSerializationRoundTrip()
        {
            const string scenePath =
                "Assets/Scenes/Generated/__MapMigrationMarkerSerializationTest.unity";
            try
            {
                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                var root = new GameObject("markers");
                root.AddComponent<MapMigrationSourceMarker>()
                    .Configure("record", 1f, true, true, true);
                root.AddComponent<MapMigrationGeneratedMarker>()
                    .Configure("fingerprint", 1, 1, 33, 1024f, false);
                root.AddComponent<MapMigrationTerrainNeighborConnector>();
                root.AddComponent<MapMigrationTerrainResidualMarker>()
                    .Configure("record", 3);

                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Scene reloaded = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                GameObject loaded = reloaded.GetRootGameObjects()[0];

                Assert.That(loaded.GetComponent<MapMigrationSourceMarker>(), Is.Not.Null);
                Assert.That(loaded.GetComponent<MapMigrationGeneratedMarker>(), Is.Not.Null);
                Assert.That(
                    loaded.GetComponent<MapMigrationTerrainNeighborConnector>(),
                    Is.Not.Null);
                Assert.That(loaded.GetComponent<MapMigrationTerrainResidualMarker>(), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }
    }
}
