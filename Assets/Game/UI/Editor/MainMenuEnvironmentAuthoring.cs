using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.LegacyImport;
using MSC.UI.Presentation;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.UI.EditorTools
{
    /// <summary>
    /// Reads accepted local world resources into an isolated static presentation.
    /// Source scenes, mesh assets, materials, coordinates and build settings remain untouched.
    /// </summary>
    public static class MainMenuEnvironmentAuthoring
    {
        public const string OutputDirectory = "Assets/Game/LegacyImport/RuntimeBaseline/GameplayPresentation/MainMenu";
        public const string PrefabPath = OutputDirectory + "/HomeYardMenuEnvironment.prefab";
        public const string ReportPath = OutputDirectory + "/HomeYardMenuEnvironmentReport.json";
        public const string LightingProfilePath = "Assets/Game/Presentation/Lighting/GaragePrototype/M3_LateDayVolume.asset";
        private const string StreamingRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/";
        private const string ManifestPath = "Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset";
        private const string SourceManifestPath = "Assets/Game/LegacyImport/Manifests/DonorWorldBaselineSourceManifest.json";
        private const string VegetationRoot = "Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/Data/";
        private static readonly string[] SourceScenes =
        {
            StreamingRoot + "World_Global_Legacy.unity",
            StreamingRoot + "Cells/World_Cell_0_-3_Legacy.unity",
            StreamingRoot + "Cells/World_Cell_0_-2_Legacy.unity",
        };
        private static readonly string[] HomeCells = { "cell_0_-3", "cell_0_-2" };
        private static readonly string[] WoodyFiles =
            { "PackedOriginalTrees.asset", "PackedShrubsAndUndergrowth.asset", "PackedBoundaryForest.asset" };
        // This fixed origin preserves every imported world transform. The vehicle
        // anchor separately follows canonical car spawn X/Z, with measured ground Y.
        private static readonly Vector3 SourceOrigin = new Vector3(153.495f, 0f, -1026.8f);
        private const string ConcreteBaseId = "59be8f2e37d6d0e62fbdfec0aec537b9";
        private const string GarageLampId = "d76c0bb63327c058f2b82bc57a699d02";
        // The lower lantern housing spans source-local Z .083 to .360 m;
        // local +Z points down in the accepted mesh's installed orientation.
        private static readonly Vector3 LampEmitterLocalPoint = new Vector3(0f, 0f, 0.22f);
        private static readonly Bounds SelectionBounds = new Bounds(
            new Vector3(165f, 20f, -1030f), new Vector3(160f, 80f, 180f));
        private static readonly HashSet<string> RequiredHouseIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "59be8f2e37d6d0e62fbdfec0aec537b9", // concrete base
            "458dc5a50e4ab4a9dca3f5445fc1dc80", // brick wall
            "1704d967080fa78707fe486f215b2811", // roof
            "b5e7b987d5aac9da197a6ce662beb64c", // garage roof
            "419f49d30da6bff0fcfa679c84d652ce", // garage wall
            "250d1cb74558e7c7e27e5e860981c938", // left garage door
            "e8660bda40e1d2946e004456e245c803", // right garage door
        };
        private static readonly HashSet<string> GroundIds = new HashSet<string>(StringComparer.Ordinal)
        {
            ConcreteBaseId, // existing garage apron at the canonical vehicle spawn
            "6c67caadad48505de0fb7c7a6f590426", // Grass1, intact cross-cell geometry
            "2dee048c54a6fce0a349dcae54f43e8b", // Grass2
            "b3326cd1a123a496fe44b26d64dda2e6", // DirtRoad
            "c578f914304beb0ed461b440d81961de", // Gravel
            "4bb072bb095570d5315c475342279879", // Roadside
        };

        [MenuItem("Tools/My Summer Car/UI/Main Menu/Rebuild Home Yard Mesh Environment")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before authoring the menu environment.");
            if (!File.ReadAllText(ManifestPath).Contains("donor-feature-parity-06b2"))
                throw new InvalidDataException("The active world profile no longer matches this bounded home recipe.");
            GameObject sourceVehicle = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuVehicleAuthoring.SourcePrefabPath);
            LegacySatsumaBaselineMetadata spawnMetadata = sourceVehicle != null
                ? sourceVehicle.GetComponent<LegacySatsumaBaselineMetadata>() : null;
            if (spawnMetadata == null)
                throw new InvalidDataException("The canonical Satsuma spawn metadata is missing.");
            if (!spawnMetadata.TryValidate(out string spawnFailure))
                throw new InvalidDataException("The canonical Satsuma spawn metadata is invalid: " + spawnFailure);
            Vector3 requestedParkingPoint = spawnMetadata.DefaultWorldPosition;
            var report = new Report
            {
                sourceOrigin = SourceOrigin,
                spawnSourcePrefab = MainMenuVehicleAuthoring.SourcePrefabPath,
                canonicalSpawnPosition = requestedParkingPoint,
                selectionCenter = SelectionBounds.center,
                selectionSize = SelectionBounds.size,
                sourceManifestSha256 = Sha256(SourceManifestPath),
            };
            VolumeProfile lightingProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LightingProfilePath);
            if (lightingProfile == null) throw new InvalidDataException("The accepted menu exposure/tone profile is missing.");
            report.sources.Add(SourceRecord.For(LightingProfilePath));
            report.sources.Add(SourceRecord.For(ManifestPath));
            report.sources.Add(SourceRecord.For(SourceManifestPath));
            report.sources.Add(SourceRecord.For(MainMenuVehicleAuthoring.SourcePrefabPath));
            var scenes = new List<Scene>();
            Scene outputScene = EditorSceneManager.NewPreviewScene();
            GameObject output = null;
            try
            {
                output = new GameObject("Home Yard Menu Environment");
                SceneManager.MoveGameObjectToScene(output, outputScene);
                var transformMap = new Dictionary<Transform, Transform>();
                var selectedIds = new HashSet<string>(StringComparer.Ordinal);
                var ground = new List<GroundSource>();
                MeshRenderer garageLamp = null;
                Bounds houseBounds = default;
                bool hasHouseBounds = false;
                foreach (string path in SourceScenes)
                {
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                        throw new FileNotFoundException("Accepted home source scene is missing.", path);
                    report.sources.Add(SourceRecord.For(path));
                    Scene scene = EditorSceneManager.OpenPreviewScene(path);
                    scenes.Add(scene);
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            DonorWorldBaselineEntityMetadata metadata =
                                renderer.GetComponent<DonorWorldBaselineEntityMetadata>();
                            if (metadata == null || !metadata.HasSanitizedRenderer) continue;
                            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                            if (!renderer.bounds.Intersects(SelectionBounds)) continue;
                            // Accepted packed vegetation supplies trees, avoiding the old billboard forest.
                            if (metadata.SemanticCategory == "VegetationTree" ||
                                metadata.SemanticCategory == "VegetationForest")
                            {
                                report.excludedLegacyTreeCount++;
                                continue;
                            }
                            if (!selectedIds.Add(metadata.StableId))
                                throw new InvalidDataException("Duplicate active source renderer: " + metadata.StableId);
                            MeshFilter sourceFilter = renderer.GetComponent<MeshFilter>();
                            if (sourceFilter == null || sourceFilter.sharedMesh == null)
                                throw new InvalidDataException("Selected source has no mesh: " + metadata.StableId);
                            Material[] materials = SourceMaterials(renderer);
                            Transform target = CopyTransform(renderer.transform, output.transform, transformMap);
                            MeshRenderer copied = AddRenderer(target.gameObject, sourceFilter.sharedMesh, materials,
                                renderer.shadowCastingMode, renderer.receiveShadows);
                            copied.allowOcclusionWhenDynamic = renderer.allowOcclusionWhenDynamic;
                            if (RequiredHouseIds.Contains(metadata.StableId))
                            {
                                if (hasHouseBounds) houseBounds.Encapsulate(copied.bounds);
                                else { houseBounds = copied.bounds; hasHouseBounds = true; }
                            }
                            if (metadata.StableId == GarageLampId)
                            {
                                if (materials.Length != 1 || sourceFilter.sharedMesh.subMeshCount != 1)
                                    throw new InvalidDataException("The accepted garage lamp's single material binding changed.");
                                garageLamp = copied;
                                string meshPath = AssetDatabase.GetAssetPath(sourceFilter.sharedMesh);
                                string materialPath = AssetDatabase.GetAssetPath(materials[0]);
                                report.sources.Add(SourceRecord.For(meshPath));
                                report.sources.Add(SourceRecord.For(materialPath));
                                report.garageLamp = new LampRecord
                                {
                                    sourceId = GarageLampId, meshPath = meshPath,
                                    materialPath = materialPath, materialIndex = 0,
                                    sourcePivotWorld = renderer.transform.position,
                                    emitterSourceLocalPoint = LampEmitterLocalPoint,
                                    sourceBoundsCenter = renderer.bounds.center,
                                    sourceBoundsSize = renderer.bounds.size,
                                };
                            }
                            report.renderers.Add(new RendererRecord
                            {
                                sourceId = metadata.StableId,
                                replacementKey = metadata.ReplacementKey,
                                sourceScene = path,
                                sourceHierarchy = metadata.SourceHierarchyPath,
                                semanticCategory = metadata.SemanticCategory,
                                meshPath = AssetDatabase.GetAssetPath(sourceFilter.sharedMesh),
                                materialPaths = materials.Select(AssetDatabase.GetAssetPath).ToArray(),
                                sourcePosition = renderer.transform.position,
                                intactCrossCellMesh = GroundIds.Contains(metadata.StableId) && metadata.StableId != ConcreteBaseId,
                            });
                            if (GroundIds.Contains(metadata.StableId))
                                ground.Add(new GroundSource(metadata.StableId, renderer, sourceFilter.sharedMesh));
                        }
                    }
                }
                foreach (string id in RequiredHouseIds)
                    if (!selectedIds.Contains(id))
                        throw new InvalidDataException("Required accepted home exterior renderer was not selected: " + id);
                if (garageLamp == null || !hasHouseBounds)
                    throw new InvalidDataException("The selected home is missing its explicit garage lamp or exterior bounds.");
                if (ground.Count == 0) throw new InvalidDataException("No accepted ground mesh intersects the home preview.");
                float groundHeight = MeasureParkingGround(ground, requestedParkingPoint, report, out Vector3 parkingCentre);
                AddPackedVegetation(output.transform, report);
                var anchor = new GameObject("Vehicle Anchor").transform;
                anchor.SetParent(output.transform, false);
                anchor.localPosition = parkingCentre - SourceOrigin;
                anchor.localRotation = Quaternion.identity;
                Renderer[] renderers = output.GetComponentsInChildren<Renderer>(true);
                MainMenuEnvironmentModel model = output.AddComponent<MainMenuEnvironmentModel>();
                // Begin inside the audited front arc, leaving room to drag
                // both ways. This changes the camera reference, not car yaw.
                Vector3 cameraDirection = Quaternion.AngleAxis(20f, Vector3.up) * new Vector3(-0.8f, 0.3f, 1f).normalized;
                cameraDirection = Quaternion.AngleAxis(3f, Vector3.Cross(Vector3.up, -cameraDirection).normalized) * cameraDirection;
                model.ConfigureForAuthoring(renderers, anchor, cameraDirection, SourceOrigin, groundHeight, lightingProfile);
                var lampAnchor = new GameObject("Garage Lamp Anchor").transform;
                lampAnchor.SetParent(output.transform, false);
                lampAnchor.position = garageLamp.transform.TransformPoint(LampEmitterLocalPoint);
                lampAnchor.rotation = Quaternion.identity;
                model.ConfigureAtmosphereForAuthoring(garageLamp, 0, lampAnchor, houseBounds);
                report.garageLamp.emitterPositionWorld = lampAnchor.position + SourceOrigin;
                report.homeExteriorBoundsCenterWorld = houseBounds.center + SourceOrigin;
                report.homeExteriorBoundsSize = houseBounds.size;
                report.cameraFromVehicleDirection = model.CameraFromVehicleDirection;
                ValidateOutput(model);
                report.rendererCount = renderers.Length;
                report.meshCount = renderers.Select(value => value.GetComponent<MeshFilter>().sharedMesh).Distinct().Count();
                report.groundHeightWorld = groundHeight;
                report.localBoundsCenter = model.LocalBounds.center;
                report.localBoundsSize = model.LocalBounds.size;
                EnsureFolder(OutputDirectory);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(output, PrefabPath);
                if (saved == null) throw new InvalidOperationException("Could not save the menu environment prefab.");
                ValidateOutput(saved.GetComponent<MainMenuEnvironmentModel>());
                VerifySourceFiles(report);
                report.status = "Generated; ground measured; structural checks passed; requires graphical verification.";
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(ReportPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"Home menu: {report.rendererCount} renderers, {report.meshCount} referenced meshes, " +
                    $"{report.vegetation.Count} woody placements, ground Y={groundHeight:R}. {PrefabPath}");
            }
            catch (Exception exception)
            {
                report.status = "Failed";
                report.errors.Add(exception.ToString());
                Directory.CreateDirectory("Artifacts/MainMenuRedesign");
                File.WriteAllText("Artifacts/MainMenuRedesign/HomeYardMenuEnvironmentFailure.json",
                    JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                throw;
            }
            finally
            {
                if (output != null) Object.DestroyImmediate(output);
                foreach (Scene scene in scenes) EditorSceneManager.ClosePreviewScene(scene);
                EditorSceneManager.ClosePreviewScene(outputScene);
            }
        }

        private static Transform CopyTransform(Transform source, Transform output,
            Dictionary<Transform, Transform> map)
        {
            if (map.TryGetValue(source, out Transform existing)) return existing;
            Transform target = new GameObject(source.name).transform;
            if (source.parent == null)
            {
                target.SetParent(output, false);
                target.localPosition = source.position - SourceOrigin;
                target.localRotation = source.rotation;
                target.localScale = source.lossyScale;
            }
            else
            {
                target.SetParent(CopyTransform(source.parent, output, map), false);
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
            }
            map.Add(source, target);
            return target;
        }

        private static Material[] SourceMaterials(Renderer renderer)
        {
            DonorWorldLegacyMaterialBinding binding = renderer.GetComponent<DonorWorldLegacyMaterialBinding>();
            Material[] result = binding != null && binding.TargetRenderer == renderer
                ? binding.TexturedMaterials.ToArray() : renderer.sharedMaterials;
            if (result.Length == 0 || result.Any(value => value == null || value.shader == null))
                throw new InvalidDataException("Selected source has missing textured materials: " + renderer.name);
            return result;
        }

        private static MeshRenderer AddRenderer(GameObject target, Mesh mesh, Material[] materials,
            ShadowCastingMode shadows, bool receiveShadows)
        {
            if (!AssetDatabase.Contains(mesh) || materials.Any(value => !AssetDatabase.Contains(value)))
                throw new InvalidDataException("Menu environment may reference only existing persistent meshes/materials.");
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = receiveShadows;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private static void AddPackedVegetation(Transform output, Report report)
        {
            Transform vegetation = new GameObject("Accepted static woody presentation").transform;
            vegetation.SetParent(output, false);
            var placementIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string cell in HomeCells)
            foreach (string file in WoodyFiles)
            {
                string path = VegetationRoot + cell + "/" + file;
                PackedWoodyCellAsset asset = AssetDatabase.LoadAssetAtPath<PackedWoodyCellAsset>(path);
                if (asset == null) throw new InvalidDataException("Accepted woody cell data is missing: " + path);
                report.sources.Add(SourceRecord.For(path));
                foreach (PackedWoodyPlacementRecord placement in asset.Placements)
                {
                    if (!SelectionBounds.Contains(placement.WorldPosition)) continue;
                    if (!placementIds.Add(placement.StableIdHash.ToString()))
                        throw new InvalidDataException("Duplicate accepted woody placement: " + placement.StableIdHash);
                    PackedWoodyPrototypeAsset prototype = asset.Prototypes[placement.PrototypeIndex];
                    PackedWoodyBatch batch = asset.Batches[placement.BatchIndex];
                    if (batch.PrototypeIndex != placement.PrototypeIndex)
                        throw new InvalidDataException("Woody placement and batch disagree: " + placement.StableIdHash);
                    Matrix4x4 matrix = batch.Matrices[placement.MatrixIndex];
                    int lod = Vector3.Distance(placement.WorldPosition, SourceOrigin) < 25f ? 0 :
                        Math.Min(1, prototype.Lods.Count - 1);
                    if (lod < 0) throw new InvalidDataException("Accepted woody prototype has no LOD: " + prototype.name);
                    // Each accepted LOD mesh may have several explicit submesh draw parts.
                    // Group them before making a MeshRenderer, so no submesh is drawn twice.
                    foreach (IGrouping<Mesh, PackedWoodyDrawPart> group in prototype.Lods[lod].DrawParts.GroupBy(value => value.Mesh))
                    {
                        if (group.Key == null) throw new InvalidDataException("Packed woody mesh is missing.");
                        var materials = new Material[group.Key.subMeshCount];
                        foreach (PackedWoodyDrawPart part in group)
                        {
                            if (part.SubMeshIndex < 0 || part.SubMeshIndex >= materials.Length ||
                                part.Material == null || materials[part.SubMeshIndex] != null)
                                throw new InvalidDataException("Packed woody submesh mapping is incomplete/duplicated: " + prototype.name);
                            materials[part.SubMeshIndex] = part.Material;
                        }
                        if (materials.Any(value => value == null))
                            throw new InvalidDataException("A packed woody mesh uses unbound submeshes: " + prototype.name);
                        GameObject item = new GameObject("Woody " + placement.StableIdHash);
                        item.transform.SetParent(vegetation, false);
                        item.transform.localPosition = (Vector3)matrix.GetColumn(3) - SourceOrigin;
                        item.transform.localRotation = matrix.rotation;
                        item.transform.localScale = matrix.lossyScale;
                        Matrix4x4 expected = Matrix4x4.Translate(-SourceOrigin) * matrix;
                        if (!SameMatrix(expected, item.transform.localToWorldMatrix))
                            throw new InvalidDataException("Packed woody transform cannot be preserved as TRS.");
                        PackedWoodyDrawPart first = group.First();
                        if (group.Any(value => value.ShadowCasting != first.ShadowCasting || value.ReceiveShadows != first.ReceiveShadows))
                            throw new InvalidDataException("Packed woody submesh shadow policies disagree.");
                        AddRenderer(item, group.Key, materials, first.ShadowCasting, first.ReceiveShadows);
                    }
                    report.vegetation.Add(new VegetationRecord
                    {
                        sourceAsset = path, stableId = placement.StableIdHash.ToString(),
                        prototype = AssetDatabase.GetAssetPath(prototype), lod = lod,
                        sourcePosition = placement.WorldPosition, planFingerprint = asset.PlanFingerprint,
                    });
                }
            }
        }

        private static bool SameMatrix(Matrix4x4 left, Matrix4x4 right)
        {
            for (int i = 0; i < 16; i++) if (Mathf.Abs(left[i] - right[i]) > 0.002f) return false;
            return true;
        }

        private static float MeasureParkingGround(List<GroundSource> sources, Vector3 requestedParkingPoint,
            Report report, out Vector3 parkingCentre)
        {
            // The user selected the real car spawn. X/Z must match its metadata
            // exactly; fail on unsupported ground rather than choosing another point.
            Vector2[] offsets =
            {
                Vector2.zero, new Vector2(-0.65f, -1.25f), new Vector2(0.65f, -1.25f),
                new Vector2(-0.65f, 1.25f), new Vector2(0.65f, 1.25f),
            };
            var triangles = new List<GroundTriangle>();
            foreach (GroundSource source in sources)
            {
                Vector3[] vertices = source.mesh.vertices;
                Matrix4x4 matrix = source.renderer.transform.localToWorldMatrix;
                string meshPath = AssetDatabase.GetAssetPath(source.mesh);
                for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                for (int submesh = 0; submesh < source.mesh.subMeshCount; submesh++)
                {
                    if (source.mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                    int[] indices = source.mesh.GetIndices(submesh);
                    for (int index = 0; index < indices.Length; index += 3)
                    {
                        Vector3 a = vertices[indices[index]], b = vertices[indices[index + 1]], c = vertices[indices[index + 2]];
                        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                        // Match the accepted world ground query: mirrored canonical
                        // transforms can reverse winding. These approved ground meshes
                        // are sampled from above as two-sided surfaces.
                        if (normal.y < 0f) normal = -normal;
                        if (normal.y < 0.98f) continue;
                        const float sampleReach = 1.25f;
                        if (Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < requestedParkingPoint.x - sampleReach ||
                            Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > requestedParkingPoint.x + sampleReach ||
                            Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < requestedParkingPoint.z - sampleReach ||
                            Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > requestedParkingPoint.z + sampleReach) continue;
                        triangles.Add(new GroundTriangle
                        {
                            a = a, b = b, c = c, normal = normal, sourceId = source.id,
                            meshPath = meshPath, submesh = submesh, triangle = index / 3,
                        });
                    }
                }
            }
            report.requestedParkingCentre = requestedParkingPoint;
            report.parkingSearchRadius = 0f;
            report.parkingSearchStep = 0f;
            report.parkingCandidatesEvaluated = 1;
            var hits = new GroundHit[offsets.Length];
            for (int sample = 0; sample < offsets.Length; sample++)
            {
                Vector2 point = new Vector2(requestedParkingPoint.x, requestedParkingPoint.z) + offsets[sample];
                foreach (GroundTriangle triangle in triangles)
                {
                    if (!TriangleHeight(triangle.a, triangle.b, triangle.c, point, out float height) ||
                        height < -5f || height > requestedParkingPoint.y ||
                        (hits[sample] != null && hits[sample].position.y >= height)) continue;
                    hits[sample] = new GroundHit
                    {
                        sourceId = triangle.sourceId, meshPath = triangle.meshPath,
                        position = new Vector3(point.x, height, point.y), normal = triangle.normal,
                        submesh = triangle.submesh, triangle = triangle.triangle,
                    };
                }
            }
            report.requestedGroundSamples.AddRange(hits.Where(value => value != null));
            if (hits.Any(value => value == null))
                throw new InvalidDataException("The exact canonical Satsuma spawn lacks a supported five-sample parking footprint.");
            float min = hits.Min(value => value.position.y), max = hits.Max(value => value.position.y);
            if (max - min > 0.18f)
                throw new InvalidDataException("The exact canonical Satsuma spawn exceeds the allowed parking height variation.");
            parkingCentre = new Vector3(requestedParkingPoint.x, max, requestedParkingPoint.z);
            report.actualParkingCentre = parkingCentre;
            report.parkingHeightVariation = max - min;
            report.groundSamples.AddRange(hits);
            return max;
        }

        private static bool TriangleHeight(Vector3 a, Vector3 b, Vector3 c, Vector2 point, out float height)
        {
            float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(denominator) < 0.000001f) { height = 0f; return false; }
            float u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.y - c.z)) / denominator;
            float v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.y - c.z)) / denominator;
            float w = 1f - u - v;
            height = u * a.y + v * b.y + w * c.y;
            return u >= -0.00001f && v >= -0.00001f && w >= -0.00001f;
        }

        public static void ValidateOutput(MainMenuEnvironmentModel model)
        {
            if (model == null || model.RendererCount == 0 || model.VehicleAnchor == null || model.MenuLightingProfile == null || !AssetDatabase.Contains(model.MenuLightingProfile))
                throw new InvalidDataException("Menu environment authoring bindings are missing.");
            if (model.GarageLampRenderer == null || model.GarageLampAnchor == null ||
                !model.Renderers.Contains(model.GarageLampRenderer) ||
                !model.GarageLampAnchor.IsChildOf(model.transform) ||
                model.GarageLampMaterialIndex < 0 ||
                model.GarageLampMaterialIndex >= model.GarageLampRenderer.sharedMaterials.Length ||
                model.HomeExteriorBoundsLocal.size.sqrMagnitude <= 0f)
                throw new InvalidDataException("The explicit garage lamp or measured house bounds are missing.");
            foreach (Component component in model.GetComponentsInChildren<Component>(true))
                if (component == null || !(component is Transform || component is MeshFilter ||
                    component is MeshRenderer || component == model))
                    throw new InvalidDataException("Forbidden component in menu environment: " + component);
            foreach (Renderer renderer in model.Renderers)
            {
                MeshFilter filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null || !AssetDatabase.Contains(filter.sharedMesh) ||
                    renderer.sharedMaterials.Any(value => value == null || !AssetDatabase.Contains(value)))
                    throw new InvalidDataException("Menu environment contains missing or copied geometry/material resources.");
            }
        }

        private static void VerifySourceFiles(Report report)
        {
            foreach (SourceRecord source in report.sources)
                if (source.sha256 != Sha256(source.path))
                    throw new InvalidDataException("An accepted source file changed while authoring: " + source.path);
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Split('/').Skip(1))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }

        private static string Sha256(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private sealed class GroundSource
        {
            public readonly string id;
            public readonly MeshRenderer renderer;
            public readonly Mesh mesh;
            public GroundSource(string sourceId, MeshRenderer sourceRenderer, Mesh sourceMesh)
            { id = sourceId; renderer = sourceRenderer; mesh = sourceMesh; }
        }
        private sealed class GroundTriangle
        {
            public Vector3 a, b, c, normal;
            public string sourceId, meshPath;
            public int submesh, triangle;
        }
        [Serializable] private sealed class Report
        {
            public string version = "MainMenuHomeYard.4";
            public string classification = "TemporaryDirectImport";
            public string replacementKey = "menu.home-yard-preview";
            public string status;
            public string sourceManifestSha256;
            public string spawnSourcePrefab;
            public Vector3 canonicalSpawnPosition;
            public LampRecord garageLamp;
            public Vector3 homeExteriorBoundsCenterWorld, homeExteriorBoundsSize;
            public Vector3 cameraFromVehicleDirection;
            public Vector3 sourceOrigin, selectionCenter, selectionSize, localBoundsCenter, localBoundsSize;
            public Vector3 requestedParkingCentre, actualParkingCentre;
            public float groundHeightWorld, parkingHeightVariation, parkingSearchRadius, parkingSearchStep;
            public int rendererCount, meshCount, excludedLegacyTreeCount, parkingCandidatesEvaluated;
            public string limitations = "Static home display, no world simulation/collision. Whole global ground meshes are referenced intact. " +
                "Woody placements use accepted static LOD0 within 25m and LOD1 farther away. Indirect grass draw services are intentionally not copied. " +
                "Selection bounds limit local objects, not the geometry of indivisible cross-cell ground meshes.";
            public List<SourceRecord> sources = new List<SourceRecord>();
            public List<RendererRecord> renderers = new List<RendererRecord>();
            public List<VegetationRecord> vegetation = new List<VegetationRecord>();
            public List<GroundHit> groundSamples = new List<GroundHit>();
            public List<GroundHit> requestedGroundSamples = new List<GroundHit>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class SourceRecord
        {
            public string path, sha256, dependencyHash;
            public static SourceRecord For(string path) => new SourceRecord
            {
                path = path, sha256 = Sha256(path),
                dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
            };
        }
        [Serializable] private sealed class LampRecord
        {
            public string sourceId, meshPath, materialPath;
            public int materialIndex;
            public Vector3 sourcePivotWorld, emitterSourceLocalPoint, emitterPositionWorld;
            public Vector3 sourceBoundsCenter, sourceBoundsSize;
        }
        [Serializable] private sealed class RendererRecord
        {
            public string sourceId, replacementKey, sourceScene, sourceHierarchy, semanticCategory, meshPath;
            public string[] materialPaths;
            public Vector3 sourcePosition;
            public bool intactCrossCellMesh;
        }
        [Serializable] private sealed class VegetationRecord
        {
            public string sourceAsset, stableId, prototype, planFingerprint;
            public int lod;
            public Vector3 sourcePosition;
        }
        [Serializable] private sealed class GroundHit
        {
            public string sourceId, meshPath;
            public Vector3 position, normal;
            public int submesh, triangle;
        }
    }
}
