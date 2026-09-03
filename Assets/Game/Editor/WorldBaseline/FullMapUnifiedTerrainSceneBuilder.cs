using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.LegacyImport;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    public static class FullMapUnifiedTerrainSceneBuilder
    {
        private const string ExternalFbxPath =
            "BlenderWork/MapEditing/" +
            "MSC_UnifiedBaseTerrain_4m_v006_Unity.fbx";
        private const string ImportedRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/" +
            "World/UnifiedTerrain";
        private const string ImportedFbxPath =
            ImportedRoot + "/MSC_UnifiedBaseTerrain_4m_v006_Unity.fbx";
        private const string MaterialPath =
            ImportedRoot + "/MSC_UnifiedBaseTerrain_4m.mat";
        private const string CollisionSourceScene =
            "Assets/Scenes/Generated/3Buildings_TerrainMigration.unity";
        public const string OutputScene =
            "Assets/Scenes/Generated/FullMap_UnifiedBaseTerrain.unity";
        private const string ReportPath =
            "Reports/WorldBaseline/FullMapUnifiedTerrainSceneValidation.json";

        private const string UnifiedRootName =
            "World_Global_UnifiedBaseTerrain";
        private const string UnifiedInstanceName =
            "MSC_UnifiedBaseTerrain_4m_v006";
        private const string CollisionRootSourceName =
            "MSC_MAP_TERRAIN_MIGRATION_GENERATED";
        private const string CollisionRootName =
            "COLLISION_ONLY_TERRAIN_TILES_2M";
        private const string StableReplacementKey =
            "legacy-world:unified-base-terrain-v006";
        private const int ExpectedCellSceneCount = 49;
        private const int ExpectedDonorEntityCount = 3846;
        private const int ExpectedDonorRendererEntityCount = 2607;
        private const int ExpectedTerrainColliderCount = 72;
        private const int ExpectedDisabledLegacyGroundEntityCount = 11;
        private const int ExpectedVertexCount = 3563337;
        private const int ExpectedTriangleCount = 7119112;
        private const int ExpectedSubMeshCount = 1;
        private const float GridStepMeters = 4f;

        private static readonly HashSet<string> LegacyGroundPaths =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "MAP/MESH/TERRAIN_OBJ/Grass1",
                "MAP/MESH/TERRAIN_OBJ/Grass2",
                "MAP/MESH/TERRAIN_OBJ/Fields",
                "MAP/MESH/TERRAIN_OBJ/4tie",
                "MAP/MESH/TERRAINOUT",
                "MAP/MESH/LAKEBED",
                "MAP/MESH/TRACKFIELD",
                "MAP/SkijumpHill/grass",
                "BetterMSC/MissingTerrain",
                "MAP/MESH/TERRAIN_OBJ/Roadside",
                "MAP/MESH/SWAMP"
            };

        private static readonly HashSet<string> RequiredRoadSurfacePaths =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "MAP/MESH/TERRAIN_OBJ/DirtRoad",
                "MAP/MESH/TERRAIN_OBJ/Asphalt",
                "MAP/MESH/TERRAIN_OBJ/Pavement",
                "MAP/MESH/TERRAIN_OBJ/Gravel",
                "MAP/MESH/TERRAIN_OBJ/Road",
                "MAP/MESH/AIRPORT",
                "MAP/MESH/RAILROAD",
                "MAP/MESH/RAILROAD_TUNNEL",
                "MAP/MESH/road_lines"
            };

        private static readonly HashSet<string> IntegratedRoadSurfacePaths =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "MAP/MESH/TERRAIN_OBJ/DirtRoad",
                "MAP/MESH/TERRAIN_OBJ/Asphalt",
                "MAP/MESH/TERRAIN_OBJ/Pavement",
                "MAP/MESH/TERRAIN_OBJ/Gravel",
                "MAP/MESH/TERRAIN_OBJ/Road"
            };

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Build Full Map With Unified Base Terrain")]
        public static void BuildFromMenu()
        {
            Build();
        }

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Validate Full Map With Unified Base Terrain")]
        public static void ValidateFromMenu()
        {
            ValidateExistingScene();
        }

        public static void RunBatch()
        {
            Build();
        }

        public static void RunValidationBatch()
        {
            ValidateExistingScene();
        }

        public static void RunRefreshTerrainBatch()
        {
            RefreshTerrainAssetInExistingScene();
        }

        public static void RunRoadSurfaceDiagnosticsBatch()
        {
            Scene scene = EditorSceneManager.OpenScene(
                OutputScene,
                OpenSceneMode.Single);
            DonorWorldBaselineEntityMetadata[] entities =
                GetSceneComponents<DonorWorldBaselineEntityMetadata>(scene)
                    .Where(entity => RequiredRoadSurfacePaths.Contains(
                        entity.SourceHierarchyPath ?? string.Empty))
                    .OrderBy(entity => entity.SourceHierarchyPath, StringComparer.Ordinal)
                    .ToArray();
            foreach (DonorWorldBaselineEntityMetadata entity in entities)
            {
                Renderer[] renderers =
                    entity.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    Mesh mesh = filter != null ? filter.sharedMesh : null;
                    string materials = string.Join(
                        " | ",
                        renderer.sharedMaterials.Select(material =>
                            material == null
                                ? "<null>"
                                : material.name + ":" +
                                  AssetDatabase.GetAssetPath(material) + ":" +
                                  (material.shader != null
                                      ? material.shader.name
                                      : "<no-shader>") + ":" +
                                  (material.HasProperty("_BaseColor")
                                      ? material.GetColor("_BaseColor").ToString()
                                      : "<no-base-color>") + ":baseMap=" +
                                  (material.HasProperty("_BaseColorMap") &&
                                   material.GetTexture("_BaseColorMap") != null
                                      ? material.GetTexture("_BaseColorMap").name + "@" +
                                        AssetDatabase.GetAssetPath(
                                            material.GetTexture("_BaseColorMap"))
                                      : "<none>") + ":surface=" +
                                  (material.HasProperty("_SurfaceType")
                                      ? material.GetFloat("_SurfaceType").ToString("F0")
                                      : "<none>") + ":queue=" +
                                  material.renderQueue));
                    Debug.Log(
                        "ROAD_SURFACE_DIAGNOSTIC " +
                        $"path={entity.SourceHierarchyPath} " +
                        $"renderer={renderer.name} enabled={renderer.enabled} " +
                        $"active={renderer.gameObject.activeInHierarchy} " +
                        $"type={renderer.GetType().Name} materials=[{materials}] " +
                        $"mesh={mesh?.name} vertices={mesh?.vertexCount} " +
                        $"submeshes={mesh?.subMeshCount} " +
                        $"worldBounds={renderer.bounds}");
                }
            }
        }

        private static void Build()
        {
            string sourceHashBefore = ComputeSha256(
                ToAbsoluteProjectPath(WorldBaseline06B2Paths.GlobalScene));
            string fbxHash = ImportUnifiedTerrainFbx();
            Scene output = CreateOutputSceneCopy();
            int mergedCellScenes = MergeCellScenes(output);
            int disabledGroundEntities = DisableLegacyGround(output);
            EnsureCanonicalRoadSurfacesActive(output);
            CollisionLayerResult collision =
                MoveCollisionOnlyTerrainLayer(output);
            UnifiedTerrainResult unified =
                CreateUnifiedTerrainPresentation(output, fbxHash);

            EditorSceneManager.SetActiveScene(output);
            EditorSceneManager.MarkSceneDirty(output);
            if (!EditorSceneManager.SaveScene(output, OutputScene))
            {
                throw new IOException(
                    "Failed to save full-map unified terrain scene.");
            }
            AssetDatabase.SaveAssets();

            string sourceHashAfter = ComputeSha256(
                ToAbsoluteProjectPath(WorldBaseline06B2Paths.GlobalScene));
            if (!string.Equals(
                    sourceHashBefore,
                    sourceHashAfter,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    "Canonical global legacy scene changed during build.");
            }

            FullMapUnifiedTerrainReport report = ValidateScene(
                output,
                fbxHash,
                sourceHashBefore);
            report.mergedCellSceneCount = mergedCellScenes;
            report.disabledLegacyGroundEntityCount =
                disabledGroundEntities;
            report.collisionTerrainCount = collision.TerrainCount;
            report.collisionTerrainColliderCount = collision.ColliderCount;
            report.unifiedMeshFilterCount = unified.MeshFilterCount;
            report.unifiedRendererCount = unified.RendererCount;
            WriteReport(report);

            Debug.Log(
                "FULL_MAP_UNIFIED_TERRAIN_SCENE_BUILD_PASS " +
                $"scene={OutputScene} cells={mergedCellScenes} " +
                $"donorEntities={report.donorEntityCount} " +
                $"groundDisabled={disabledGroundEntities} " +
                $"meshFilters={unified.MeshFilterCount} " +
                $"vertices={report.unifiedVertexCount} " +
                $"triangles={report.unifiedTriangleCount} " +
                $"collisionTerrains={collision.TerrainCount}");
        }

        private static string ImportUnifiedTerrainFbx()
        {
            string externalAbsolute =
                ToAbsoluteProjectPath(ExternalFbxPath);
            if (!File.Exists(externalAbsolute))
            {
                throw new FileNotFoundException(
                    "Unity-ready unified terrain FBX was not found.",
                    externalAbsolute);
            }

            EnsureAssetFolder(ImportedRoot);
            string destinationAbsolute =
                ToAbsoluteProjectPath(ImportedFbxPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(destinationAbsolute) ??
                throw new InvalidDataException(
                    "Unified terrain import folder is invalid."));
            File.Copy(
                externalAbsolute,
                destinationAbsolute,
                overwrite: true);
            AssetDatabase.ImportAsset(
                ImportedFbxPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            ModelImporter importer =
                AssetImporter.GetAtPath(ImportedFbxPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidDataException(
                    "Imported unified terrain is not a model asset.");
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.materialImportMode =
                ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.generateSecondaryUV = false;
            importer.optimizeMeshPolygons = false;
            importer.optimizeMeshVertices = false;
            importer.weldVertices = false;
            importer.SaveAndReimport();

            return ComputeSha256(externalAbsolute);
        }

        private static Scene CreateOutputSceneCopy()
        {
            if (!File.Exists(ToAbsoluteProjectPath(
                    WorldBaseline06B2Paths.GlobalScene)))
            {
                throw new FileNotFoundException(
                    "Canonical global legacy scene is missing.",
                    WorldBaseline06B2Paths.GlobalScene);
            }

            Scene source = EditorSceneManager.OpenScene(
                WorldBaseline06B2Paths.GlobalScene,
                OpenSceneMode.Single);
            if (source.isDirty)
            {
                throw new InvalidOperationException(
                    "Canonical global legacy scene is dirty.");
            }

            EnsureAssetFolder("Assets/Scenes/Generated");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OutputScene) != null)
            {
                if (!AssetDatabase.DeleteAsset(OutputScene))
                {
                    throw new IOException(
                        "Could not replace the generated output scene.");
                }
            }

            if (!EditorSceneManager.SaveScene(
                    source,
                    OutputScene,
                    saveAsCopy: true))
            {
                throw new IOException(
                    "Failed to create output scene copy.");
            }

            return EditorSceneManager.OpenScene(
                OutputScene,
                OpenSceneMode.Single);
        }

        private static int MergeCellScenes(Scene output)
        {
            string cellRootAbsolute = ToAbsoluteProjectPath(
                WorldBaseline06B2Paths.StreamingCellSceneRoot);
            string[] cellScenes = Directory
                .GetFiles(cellRootAbsolute, "*.unity", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (cellScenes.Length != ExpectedCellSceneCount)
            {
                throw new InvalidDataException(
                    "Expected " + ExpectedCellSceneCount +
                    " canonical cell scenes, found " +
                    cellScenes.Length + ".");
            }

            foreach (string absolutePath in cellScenes)
            {
                string assetPath = ToAssetPath(absolutePath);
                Scene cell = EditorSceneManager.OpenScene(
                    assetPath,
                    OpenSceneMode.Additive);
                GameObject[] roots = cell.GetRootGameObjects();
                foreach (GameObject root in roots)
                {
                    SceneManager.MoveGameObjectToScene(root, output);
                }
                if (!EditorSceneManager.CloseScene(cell, removeScene: true))
                {
                    throw new IOException(
                        "Failed to close merged cell scene " + assetPath);
                }
            }

            return cellScenes.Length;
        }

        private static int DisableLegacyGround(Scene scene)
        {
            var groundEntities = new HashSet<GameObject>();
            foreach (DonorWorldBaselineEntityMetadata metadata in
                     GetSceneComponents<DonorWorldBaselineEntityMetadata>(scene))
            {
                string sourcePath = metadata.SourceHierarchyPath ?? string.Empty;
                if (!LegacyGroundPaths.Contains(sourcePath))
                {
                    continue;
                }
                groundEntities.Add(metadata.gameObject);
            }

            foreach (DonorWorldSupplementalEntityMetadata metadata in
                     GetSceneComponents<DonorWorldSupplementalEntityMetadata>(scene))
            {
                string sourcePath = metadata.SourceHierarchyPath ?? string.Empty;
                if (!LegacyGroundPaths.Contains(sourcePath))
                {
                    continue;
                }
                groundEntities.Add(metadata.gameObject);
            }

            foreach (GameObject groundEntity in groundEntities)
            {
                DisablePresentationAndCollision(groundEntity);
            }

            if (groundEntities.Count != ExpectedDisabledLegacyGroundEntityCount)
            {
                throw new InvalidDataException(
                    "Unexpected legacy ground entity count: expected " +
                    ExpectedDisabledLegacyGroundEntityCount + ", found " +
                    groundEntities.Count + ".");
            }

            return groundEntities.Count;
        }

        private static int EnsureCanonicalRoadSurfacesActive(Scene scene)
        {
            var roadEntities = new HashSet<GameObject>();
            foreach (DonorWorldBaselineEntityMetadata metadata in
                     GetSceneComponents<DonorWorldBaselineEntityMetadata>(scene))
            {
                if (IntegratedRoadSurfacePaths.Contains(
                        metadata.SourceHierarchyPath ?? string.Empty))
                {
                    roadEntities.Add(metadata.gameObject);
                }
            }
            foreach (DonorWorldSupplementalEntityMetadata metadata in
                     GetSceneComponents<DonorWorldSupplementalEntityMetadata>(scene))
            {
                if (IntegratedRoadSurfacePaths.Contains(
                        metadata.SourceHierarchyPath ?? string.Empty))
                {
                    roadEntities.Add(metadata.gameObject);
                }
            }
            if (roadEntities.Count != IntegratedRoadSurfacePaths.Count)
            {
                throw new InvalidDataException(
                    "Canonical road surface set is incomplete: expected " +
                    IntegratedRoadSurfacePaths.Count + ", found " +
                    roadEntities.Count + ".");
            }
            foreach (GameObject roadEntity in roadEntities)
            {
                foreach (Renderer renderer in
                         roadEntity.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = true;
                }
                EnableCollision(roadEntity);
            }
            return roadEntities.Count;
        }

        private static void DisablePresentationAndCollision(GameObject root)
        {
            DisablePresentation(root);
            foreach (Collider collider in
                     root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private static void DisablePresentation(GameObject root)
        {
            foreach (Renderer renderer in
                     root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
        }

        private static void EnableCollision(GameObject root)
        {
            foreach (Collider collider in
                     root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
            }
        }

        private static CollisionLayerResult MoveCollisionOnlyTerrainLayer(
            Scene output)
        {
            if (!File.Exists(ToAbsoluteProjectPath(CollisionSourceScene)))
            {
                throw new FileNotFoundException(
                    "Validated Terrain collision source scene is missing.",
                    CollisionSourceScene);
            }

            Scene collisionSource = EditorSceneManager.OpenScene(
                CollisionSourceScene,
                OpenSceneMode.Additive);
            GameObject generatedRoot = FindGameObject(
                collisionSource,
                CollisionRootSourceName);
            if (generatedRoot == null)
            {
                throw new InvalidDataException(
                    "Terrain migration generated root is missing.");
            }

            generatedRoot.transform.SetParent(null, worldPositionStays: true);
            SceneManager.MoveGameObjectToScene(generatedRoot, output);
            generatedRoot.name = CollisionRootName;

            Terrain[] terrains =
                generatedRoot.GetComponentsInChildren<Terrain>(true);
            TerrainCollider[] colliders =
                generatedRoot.GetComponentsInChildren<TerrainCollider>(true);
            foreach (Terrain terrain in terrains)
            {
                terrain.drawHeightmap = false;
                terrain.drawTreesAndFoliage = false;
                terrain.enabled = false;
            }
            foreach (TerrainCollider collider in colliders)
            {
                collider.enabled = true;
            }

            if (!EditorSceneManager.CloseScene(
                    collisionSource,
                    removeScene: true))
            {
                throw new IOException(
                    "Failed to close collision source scene.");
            }

            if (terrains.Length != ExpectedTerrainColliderCount ||
                colliders.Length != ExpectedTerrainColliderCount)
            {
                throw new InvalidDataException(
                    "Unexpected collision Terrain grid: terrains=" +
                    terrains.Length + " colliders=" + colliders.Length + ".");
            }

            return new CollisionLayerResult(
                terrains.Length,
                colliders.Length);
        }

        private static UnifiedTerrainResult CreateUnifiedTerrainPresentation(
            Scene output,
            string fbxHash)
        {
            GameObject model =
                AssetDatabase.LoadAssetAtPath<GameObject>(ImportedFbxPath);
            if (model == null)
            {
                throw new InvalidDataException(
                    "Unified terrain model prefab could not be loaded.");
            }

            var root = new GameObject(UnifiedRootName);
            SceneManager.MoveGameObjectToScene(root, output);
            GameObject instance = PrefabUtility.InstantiatePrefab(
                model,
                output) as GameObject;
            if (instance == null)
            {
                throw new InvalidDataException(
                    "Unified terrain model could not be instantiated.");
            }
            instance.name = UnifiedInstanceName;
            instance.transform.SetParent(root.transform, worldPositionStays: false);

            MeshRenderer[] renderers =
                instance.GetComponentsInChildren<MeshRenderer>(true);
            ConfigureUnifiedTerrainMaterials(instance);
            foreach (MeshRenderer renderer in renderers)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            foreach (Transform transform in
                     instance.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(
                    transform.gameObject,
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ReflectionProbeStatic);
            }

            MeshFilter[] filters =
                instance.GetComponentsInChildren<MeshFilter>(true);
            int vertexCount = filters.Sum(
                filter => filter.sharedMesh != null
                    ? filter.sharedMesh.vertexCount
                    : 0);
            int triangleCount = filters.Sum(
                filter => filter.sharedMesh != null
                    ? CountTriangles(filter.sharedMesh)
                    : 0);
            Bounds bounds = CalculateBounds(renderers);
            ValidateImportedTerrainGeometry(
                filters,
                renderers,
                vertexCount,
                triangleCount,
                bounds);

            root.AddComponent<UnifiedBaseTerrainMarker>().Configure(
                StableReplacementKey,
                ImportedFbxPath,
                fbxHash,
                GridStepMeters,
                vertexCount,
                triangleCount);

            return new UnifiedTerrainResult(
                filters.Length,
                renderers.Length,
                vertexCount,
                triangleCount,
                bounds);
        }

        private static Material CreateOrLoadTerrainMaterial()
        {
            return CreateOrLoadLitMaterial(
                MaterialPath,
                "MSC_UnifiedBaseTerrain_4m_Matte",
                new Color(0.22f, 0.30f, 0.16f, 1f),
                0f);
        }

        private static Material CreateOrLoadLitMaterial(
            string path,
            string materialName,
            Color baseColor,
            float smoothness)
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new InvalidDataException(
                    "HDRP/Lit shader is unavailable.");
            }

            if (material == null)
            {
                material = new Material(shader)
                {
                    name = materialName
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureUnifiedTerrainMaterials(GameObject root)
        {
            Material terrainMaterial = CreateOrLoadTerrainMaterial();
            foreach (MeshRenderer renderer in
                     root.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    throw new InvalidDataException(
                        "Unified terrain renderer has no MeshFilter mesh.");
                }
                if (mesh.subMeshCount != ExpectedSubMeshCount)
                {
                    throw new InvalidDataException(
                        "Unified terrain must contain exactly one ground " +
                        "submesh: found " + mesh.subMeshCount + ".");
                }
                renderer.sharedMaterials = new[]
                {
                    terrainMaterial
                };
            }
        }

        private static void ValidateImportedTerrainGeometry(
            MeshFilter[] filters,
            MeshRenderer[] renderers,
            int vertexCount,
            int triangleCount,
            Bounds bounds)
        {
            if (filters.Length != 1 || renderers.Length != 1)
            {
                throw new InvalidDataException(
                    "Unified terrain FBX must import as one mesh and one renderer: " +
                    $"filters={filters.Length}, renderers={renderers.Length}.");
            }
            if (filters[0].sharedMesh == null ||
                filters[0].sharedMesh.subMeshCount != ExpectedSubMeshCount)
            {
                throw new InvalidDataException(
                    "Unified terrain must import as one ground submesh.");
            }
            if (renderers[0].sharedMaterials.Length != ExpectedSubMeshCount ||
                renderers[0].sharedMaterials.Any(material => material == null))
            {
                throw new InvalidDataException(
                    "Unified terrain ground material is incomplete.");
            }
            if (vertexCount != ExpectedVertexCount ||
                triangleCount != ExpectedTriangleCount)
            {
                throw new InvalidDataException(
                    "Unified terrain topology changed during FBX import: " +
                    $"vertices={vertexCount}, triangles={triangleCount}.");
            }
            if (bounds.size.x < 7950f || bounds.size.x > 8050f ||
                bounds.size.z < 7050f || bounds.size.z > 7160f ||
                bounds.size.y < 90f || bounds.size.y > 125f)
            {
                throw new InvalidDataException(
                    "Unified terrain bounds indicate a scale or axis error: " +
                    bounds + ".");
            }
            if (bounds.center.x < 260f || bounds.center.x > 290f ||
                bounds.center.y < 20f || bounds.center.y > 40f ||
                bounds.center.z < -660f || bounds.center.z > -630f)
            {
                throw new InvalidDataException(
                    "Unified terrain bounds center indicates a mirrored or " +
                    "misoriented map: " + bounds + ".");
            }
        }

        private static FullMapUnifiedTerrainReport ValidateScene(
            Scene scene,
            string expectedFbxHash,
            string sourceSceneHash)
        {
            UnifiedBaseTerrainMarker[] markers =
                GetSceneComponents<UnifiedBaseTerrainMarker>(scene).ToArray();
            if (markers.Length != 1)
            {
                throw new InvalidDataException(
                    "Expected exactly one unified terrain marker, found " +
                    markers.Length + ".");
            }
            UnifiedBaseTerrainMarker marker = markers[0];
            if (!string.Equals(
                    marker.StableReplacementKey,
                    StableReplacementKey,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    marker.SourceSha256,
                    expectedFbxHash,
                    StringComparison.OrdinalIgnoreCase) ||
                marker.Classification !=
                DonorWorldBaselineClassification.TemporaryDirectImport)
            {
                throw new InvalidDataException(
                    "Unified terrain provenance marker is invalid.");
            }

            MeshFilter[] unifiedFilters =
                marker.GetComponentsInChildren<MeshFilter>(true);
            MeshRenderer[] unifiedRenderers =
                marker.GetComponentsInChildren<MeshRenderer>(true);
            int vertices = unifiedFilters.Sum(
                filter => filter.sharedMesh != null
                    ? filter.sharedMesh.vertexCount
                    : 0);
            int triangles = unifiedFilters.Sum(
                filter => filter.sharedMesh != null
                    ? CountTriangles(filter.sharedMesh)
                    : 0);
            Bounds bounds = CalculateBounds(unifiedRenderers);
            ValidateImportedTerrainGeometry(
                unifiedFilters,
                unifiedRenderers,
                vertices,
                triangles,
                bounds);

            var legacyGroundEntities = new HashSet<GameObject>();
            var integratedRoadEntities = new HashSet<GameObject>();
            foreach (DonorWorldBaselineEntityMetadata metadata in
                     GetSceneComponents<DonorWorldBaselineEntityMetadata>(scene))
            {
                string sourcePath = metadata.SourceHierarchyPath ?? string.Empty;
                if (IntegratedRoadSurfacePaths.Contains(sourcePath))
                {
                    integratedRoadEntities.Add(metadata.gameObject);
                }
                if (!LegacyGroundPaths.Contains(sourcePath))
                {
                    continue;
                }
                legacyGroundEntities.Add(metadata.gameObject);
            }
            foreach (DonorWorldSupplementalEntityMetadata metadata in
                     GetSceneComponents<DonorWorldSupplementalEntityMetadata>(scene))
            {
                string sourcePath = metadata.SourceHierarchyPath ?? string.Empty;
                if (IntegratedRoadSurfacePaths.Contains(sourcePath))
                {
                    integratedRoadEntities.Add(metadata.gameObject);
                }
                if (!LegacyGroundPaths.Contains(sourcePath))
                {
                    continue;
                }
                legacyGroundEntities.Add(metadata.gameObject);
            }
            int disabledLegacyGroundEntities = legacyGroundEntities.Count;
            int visibleLegacyGroundRenderers = legacyGroundEntities.Sum(
                entity => entity.GetComponentsInChildren<Renderer>(true)
                    .Count(renderer => renderer.enabled));
            int enabledLegacyGroundColliders = legacyGroundEntities
                .Where(entity => !integratedRoadEntities.Contains(entity)).Sum(
                entity => entity.GetComponentsInChildren<Collider>(true)
                    .Count(collider => collider.enabled));
            int integratedRoadColliderCount = integratedRoadEntities.Sum(
                entity => entity.GetComponentsInChildren<Collider>(true).Length);
            int enabledIntegratedRoadColliders = integratedRoadEntities.Sum(
                entity => entity.GetComponentsInChildren<Collider>(true)
                    .Count(collider => collider.enabled));
            if (disabledLegacyGroundEntities !=
                ExpectedDisabledLegacyGroundEntityCount)
            {
                throw new InvalidDataException(
                    "Legacy ground replacement coverage changed: expected " +
                    ExpectedDisabledLegacyGroundEntityCount + ", found " +
                    disabledLegacyGroundEntities + ".");
            }
            if (visibleLegacyGroundRenderers != 0 ||
                enabledLegacyGroundColliders != 0)
            {
                throw new InvalidDataException(
                    "Legacy ground remains active: renderers=" +
                    visibleLegacyGroundRenderers + " colliders=" +
                    enabledLegacyGroundColliders + ".");
            }
            if (enabledIntegratedRoadColliders != integratedRoadColliderCount)
            {
                throw new InvalidDataException(
                    "An integrated road source collider was disabled: enabled=" +
                    enabledIntegratedRoadColliders + " total=" +
                    integratedRoadColliderCount + ".");
            }

            GameObject collisionRoot = FindGameObject(scene, CollisionRootName);
            if (collisionRoot == null)
            {
                throw new InvalidDataException(
                    "Collision-only Terrain root is missing.");
            }
            Terrain[] collisionTerrains =
                collisionRoot.GetComponentsInChildren<Terrain>(true);
            TerrainCollider[] collisionColliders =
                collisionRoot.GetComponentsInChildren<TerrainCollider>(true);
            if (collisionTerrains.Length != ExpectedTerrainColliderCount ||
                collisionTerrains.Any(terrain =>
                    terrain.enabled || terrain.drawHeightmap) ||
                collisionColliders.Length != ExpectedTerrainColliderCount ||
                collisionColliders.Any(collider => !collider.enabled))
            {
                throw new InvalidDataException(
                    "Collision-only Terrain layer is not configured correctly.");
            }

            DonorWorldBaselineEntityMetadata[] donorEntities =
                GetSceneComponents<DonorWorldBaselineEntityMetadata>(scene)
                    .ToArray();
            if (donorEntities.Length != ExpectedDonorEntityCount)
            {
                throw new InvalidDataException(
                    "Consolidated donor entity count changed: expected " +
                    ExpectedDonorEntityCount + ", got " +
                    donorEntities.Length + ".");
            }
            int donorRendererEntities = donorEntities.Count(
                entity => entity.HasSanitizedRenderer);
            if (donorRendererEntities != ExpectedDonorRendererEntityCount)
            {
                throw new InvalidDataException(
                    "Consolidated donor renderer entity count changed: " +
                    "expected " + ExpectedDonorRendererEntityCount +
                    ", got " + donorRendererEntities + ".");
            }

            DonorWorldBaselineEntityMetadata[] roadSurfaceEntities =
                donorEntities.Where(entity => RequiredRoadSurfacePaths.Contains(
                    entity.SourceHierarchyPath ?? string.Empty)).ToArray();
            if (roadSurfaceEntities.Length != RequiredRoadSurfacePaths.Count ||
                RequiredRoadSurfacePaths.Any(path => !roadSurfaceEntities.Any(
                    entity => string.Equals(
                        entity.SourceHierarchyPath,
                        path,
                        StringComparison.Ordinal))))
            {
                throw new InvalidDataException(
                    "Required road surface set is incomplete: expected " +
                    RequiredRoadSurfacePaths.Count + ", found " +
                    roadSurfaceEntities.Length + ".");
            }
            int enabledRoadSurfaceRenderers = roadSurfaceEntities.Sum(
                entity => entity.GetComponentsInChildren<Renderer>(true)
                    .Count(renderer => renderer.enabled &&
                                       renderer.gameObject.activeInHierarchy));
            string[] inactiveRoadSurfacePaths = roadSurfaceEntities
                .Where(entity => !entity.GetComponentsInChildren<Renderer>(true)
                    .Any(renderer => renderer.enabled &&
                                     renderer.gameObject.activeInHierarchy))
                .Select(entity => entity.SourceHierarchyPath ?? string.Empty)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (inactiveRoadSurfacePaths.Length != 0)
            {
                throw new InvalidDataException(
                    "One or more canonical road renderers are inactive: " +
                    string.Join(", ", inactiveRoadSurfacePaths) + ".");
            }

            int cellRootCount = scene.GetRootGameObjects().Count(
                root => root.name.StartsWith(
                    "World_cell_",
                    StringComparison.OrdinalIgnoreCase));
            if (cellRootCount != ExpectedCellSceneCount)
            {
                throw new InvalidDataException(
                    "Consolidated cell root count changed: " +
                    cellRootCount + ".");
            }

            int missingScripts = scene.GetRootGameObjects().Sum(
                root => CountMissingScripts(root));
            if (missingScripts != 0)
            {
                throw new InvalidDataException(
                    "Generated scene contains missing scripts: " +
                    missingScripts + ".");
            }

            var report = new FullMapUnifiedTerrainReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                passed = true,
                scenePath = OutputScene,
                sourceGlobalScenePath = WorldBaseline06B2Paths.GlobalScene,
                sourceGlobalSceneSha256 = sourceSceneHash,
                importedFbxPath = ImportedFbxPath,
                importedFbxSha256 = expectedFbxHash,
                donorEntityCount = donorEntities.Length,
                donorRendererEntityCount = donorRendererEntities,
                mergedCellSceneCount = cellRootCount,
                disabledLegacyGroundEntityCount =
                    disabledLegacyGroundEntities,
                unifiedMeshFilterCount = unifiedFilters.Length,
                unifiedRendererCount = unifiedRenderers.Length,
                unifiedSubMeshCount = unifiedFilters.Sum(
                    filter => filter.sharedMesh != null
                        ? filter.sharedMesh.subMeshCount
                        : 0),
                unifiedVertexCount = vertices,
                unifiedTriangleCount = triangles,
                unifiedBoundsCenter = bounds.center,
                unifiedBoundsSize = bounds.size,
                visibleLegacyGroundRendererCount =
                    visibleLegacyGroundRenderers,
                enabledLegacyGroundColliderCount =
                    enabledLegacyGroundColliders,
                integratedRoadColliderCount = integratedRoadColliderCount,
                enabledIntegratedRoadColliderCount =
                    enabledIntegratedRoadColliders,
                collisionTerrainCount = collisionTerrains.Length,
                collisionTerrainColliderCount = collisionColliders.Length,
                roadSurfaceEntityCount = roadSurfaceEntities.Length,
                enabledRoadSurfaceRendererCount =
                    enabledRoadSurfaceRenderers,
                missingScriptCount = missingScripts,
                categoryCounts = donorEntities
                    .GroupBy(entity => entity.SemanticCategory ?? string.Empty)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new CategoryCount
                    {
                        category = group.Key,
                        count = group.Count()
                    })
                    .ToList()
            };
            return report;
        }

        private static void RefreshTerrainAssetInExistingScene()
        {
            if (!File.Exists(ToAbsoluteProjectPath(OutputScene)))
            {
                throw new FileNotFoundException(
                    "Generated full-map unified terrain scene is missing.",
                    OutputScene);
            }

            string fbxHash = ImportUnifiedTerrainFbx();
            string sourceHash = ComputeSha256(
                ToAbsoluteProjectPath(WorldBaseline06B2Paths.GlobalScene));
            Scene scene = EditorSceneManager.OpenScene(
                OutputScene,
                OpenSceneMode.Single);
            DisableLegacyGround(scene);
            EnsureCanonicalRoadSurfacesActive(scene);
            UnifiedBaseTerrainMarker[] markers =
                GetSceneComponents<UnifiedBaseTerrainMarker>(scene).ToArray();
            if (markers.Length != 1)
            {
                throw new InvalidDataException(
                    "Expected exactly one unified terrain marker, found " +
                    markers.Length + ".");
            }

            UnifiedBaseTerrainMarker marker = markers[0];
            foreach (Transform child in marker.transform.Cast<Transform>()
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            GameObject model =
                AssetDatabase.LoadAssetAtPath<GameObject>(ImportedFbxPath);
            if (model == null)
            {
                throw new InvalidDataException(
                    "Unified terrain model prefab could not be loaded.");
            }
            GameObject instance = PrefabUtility.InstantiatePrefab(
                model,
                scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidDataException(
                    "Unified terrain model could not be instantiated.");
            }
            instance.name = UnifiedInstanceName;
            instance.transform.SetParent(
                marker.transform,
                worldPositionStays: false);
            foreach (Transform transform in
                     instance.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(
                    transform.gameObject,
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ReflectionProbeStatic);
            }
            MeshFilter[] filters =
                instance.GetComponentsInChildren<MeshFilter>(true);
            MeshRenderer[] renderers =
                instance.GetComponentsInChildren<MeshRenderer>(true);
            ConfigureUnifiedTerrainMaterials(instance);
            foreach (MeshRenderer renderer in renderers)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            int vertices = filters.Sum(
                filter => filter.sharedMesh != null
                    ? filter.sharedMesh.vertexCount
                    : 0);
            int triangles = filters.Sum(
                filter => filter.sharedMesh != null
                    ? CountTriangles(filter.sharedMesh)
                    : 0);
            ValidateImportedTerrainGeometry(
                filters,
                renderers,
                vertices,
                triangles,
                CalculateBounds(renderers));

            marker.Configure(
                StableReplacementKey,
                ImportedFbxPath,
                fbxHash,
                GridStepMeters,
                vertices,
                triangles);
            EditorUtility.SetDirty(marker);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, OutputScene))
            {
                throw new IOException(
                    "Failed to save refreshed full-map scene.");
            }
            AssetDatabase.SaveAssets();

            FullMapUnifiedTerrainReport report = ValidateScene(
                scene,
                fbxHash,
                sourceHash);
            WriteReport(report);
            Debug.Log(
                "FULL_MAP_UNIFIED_TERRAIN_SCENE_REFRESH_PASS " +
                $"scene={OutputScene} fbxSha256={fbxHash} " +
                $"boundsCenter={report.unifiedBoundsCenter} " +
                $"vertices={report.unifiedVertexCount} " +
                $"triangles={report.unifiedTriangleCount}");
        }

        private static void ValidateExistingScene()
        {
            if (!File.Exists(ToAbsoluteProjectPath(OutputScene)))
            {
                throw new FileNotFoundException(
                    "Generated full-map unified terrain scene is missing.",
                    OutputScene);
            }
            string sourceHash = ComputeSha256(
                ToAbsoluteProjectPath(WorldBaseline06B2Paths.GlobalScene));
            string fbxHash = ComputeSha256(
                ToAbsoluteProjectPath(ExternalFbxPath));
            Scene scene = EditorSceneManager.OpenScene(
                OutputScene,
                OpenSceneMode.Single);
            FullMapUnifiedTerrainReport report = ValidateScene(
                scene,
                fbxHash,
                sourceHash);
            WriteReport(report);
            Debug.Log(
                "FULL_MAP_UNIFIED_TERRAIN_SCENE_VALIDATION_PASS " +
                $"scene={OutputScene} donorEntities={report.donorEntityCount} " +
                $"cells={report.mergedCellSceneCount} " +
                $"vertices={report.unifiedVertexCount} " +
                $"triangles={report.unifiedTriangleCount} " +
                $"collisionTerrains={report.collisionTerrainCount} " +
                $"roadSurfaceRenderers={report.enabledRoadSurfaceRendererCount} " +
                "legacyGroundVisible=0 missingScripts=0");
        }

        private static IEnumerable<T> GetSceneComponents<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<T>(true));
        }

        private static GameObject FindGameObject(Scene scene, string name)
        {
            foreach (Transform transform in scene.GetRootGameObjects()
                         .SelectMany(root =>
                             root.GetComponentsInChildren<Transform>(true)))
            {
                if (string.Equals(
                        transform.name,
                        name,
                        StringComparison.Ordinal))
                {
                    return transform.gameObject;
                }
            }
            return null;
        }

        private static Bounds CalculateBounds(
            IReadOnlyList<MeshRenderer> renderers)
        {
            if (renderers.Count == 0)
            {
                throw new InvalidDataException(
                    "Unified terrain contains no MeshRenderer.");
            }
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Count; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return bounds;
        }

        private static int CountMissingScripts(GameObject root)
        {
            int count = 0;
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                count += GameObjectUtility
                    .GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject);
            }
            return count;
        }

        private static int CountTriangles(Mesh mesh)
        {
            long indexCount = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                indexCount += (long)mesh.GetIndexCount(subMesh);
            }
            return checked((int)(indexCount / 3));
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }
            string parent =
                (Path.GetDirectoryName(normalized) ?? string.Empty)
                .Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parent))
            {
                throw new InvalidDataException(
                    "Invalid asset folder path: " + assetPath);
            }
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(normalized));
        }

        private static string ToAbsoluteProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ??
                throw new InvalidDataException(
                    "Unity project root is unavailable."),
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot =
                Path.GetDirectoryName(Application.dataPath) ??
                throw new InvalidDataException(
                    "Unity project root is unavailable.");
            string relative = Path.GetRelativePath(projectRoot, absolutePath)
                .Replace('\\', '/');
            if (!relative.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Scene is outside the Unity project: " + absolutePath);
            }
            return relative;
        }

        private static string ComputeSha256(string absolutePath)
        {
            using FileStream stream = File.OpenRead(absolutePath);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(stream);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                builder.Append(value.ToString("x2"));
            }
            return builder.ToString();
        }

        private static void WriteReport(
            FullMapUnifiedTerrainReport report)
        {
            string absolute = ToAbsoluteProjectPath(ReportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(absolute) ??
                throw new InvalidDataException(
                    "Validation report folder is invalid."));
            File.WriteAllText(
                absolute,
                JsonUtility.ToJson(report, prettyPrint: true) + "\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        [Serializable]
        private sealed class FullMapUnifiedTerrainReport
        {
            public string generatedUtc = string.Empty;
            public bool passed;
            public string scenePath = string.Empty;
            public string sourceGlobalScenePath = string.Empty;
            public string sourceGlobalSceneSha256 = string.Empty;
            public string importedFbxPath = string.Empty;
            public string importedFbxSha256 = string.Empty;
            public int donorEntityCount;
            public int donorRendererEntityCount;
            public int mergedCellSceneCount;
            public int disabledLegacyGroundEntityCount;
            public int unifiedMeshFilterCount;
            public int unifiedRendererCount;
            public int unifiedSubMeshCount;
            public int unifiedVertexCount;
            public int unifiedTriangleCount;
            public Vector3 unifiedBoundsCenter;
            public Vector3 unifiedBoundsSize;
            public int visibleLegacyGroundRendererCount;
            public int enabledLegacyGroundColliderCount;
            public int integratedRoadColliderCount;
            public int enabledIntegratedRoadColliderCount;
            public int collisionTerrainCount;
            public int collisionTerrainColliderCount;
            public int roadSurfaceEntityCount;
            public int enabledRoadSurfaceRendererCount;
            public int missingScriptCount;
            public List<CategoryCount> categoryCounts =
                new List<CategoryCount>();
        }

        [Serializable]
        private sealed class CategoryCount
        {
            public string category = string.Empty;
            public int count;
        }

        private readonly struct CollisionLayerResult
        {
            public CollisionLayerResult(int terrainCount, int colliderCount)
            {
                TerrainCount = terrainCount;
                ColliderCount = colliderCount;
            }

            public int TerrainCount { get; }
            public int ColliderCount { get; }
        }

        private readonly struct UnifiedTerrainResult
        {
            public UnifiedTerrainResult(
                int meshFilterCount,
                int rendererCount,
                int vertexCount,
                int triangleCount,
                Bounds bounds)
            {
                MeshFilterCount = meshFilterCount;
                RendererCount = rendererCount;
                VertexCount = vertexCount;
                TriangleCount = triangleCount;
                Bounds = bounds;
            }

            public int MeshFilterCount { get; }
            public int RendererCount { get; }
            public int VertexCount { get; }
            public int TriangleCount { get; }
            public Bounds Bounds { get; }
        }
    }
}
