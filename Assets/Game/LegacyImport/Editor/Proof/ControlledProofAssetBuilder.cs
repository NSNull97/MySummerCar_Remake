using System;
using System.Collections.Generic;
using System.IO;
using MSC.LegacyImport.Editor.Ledger;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.Proof
{
    public static class ControlledProofAssetBuilder
    {
        private const string BuilderVersion = "1.0.0";
        private const string RegistryPath =
            "Assets/Game/LegacyImport/Manifests/milestone-02-controlled-proof.asset";
        private const string EnvironmentRecordId = "m2-environment-garage-shed-roof";
        private const string VehicleRecordId = "m2-vehicle-drum-brake-rear";
        private const string EnvironmentPrefabPath =
            "Assets/Game/World/Content/Proof/ReauthoredGarageShedRoof.prefab";
        private const string VehiclePrefabPath =
            "Assets/Game/Vehicle/Content/Proof/ReauthoredRearBrakeDrum.prefab";
        private const string EnvironmentMeshLod0Path =
            "Assets/Game/World/Content/Proof/Meshes/ReauthoredGarageShedRoof_LOD0.asset";
        private const string EnvironmentMeshLod1Path =
            "Assets/Game/World/Content/Proof/Meshes/ReauthoredGarageShedRoof_LOD1.asset";
        private const string VehicleMeshLod0Path =
            "Assets/Game/Vehicle/Content/Proof/Meshes/ReauthoredRearBrakeDrum_LOD0.asset";
        private const string VehicleMeshLod1Path =
            "Assets/Game/Vehicle/Content/Proof/Meshes/ReauthoredRearBrakeDrum_LOD1.asset";
        private const string MaterialPath =
            "Assets/Game/Presentation/Materials/Proof/ControlledProofPaint.mat";
        private const string BaseColorPath =
            "Assets/Game/Presentation/Materials/Proof/Textures/ControlledProofPaint_BaseColor.png";
        private const string NormalPath =
            "Assets/Game/Presentation/Materials/Proof/Textures/ControlledProofPaint_Normal.png";
        private const string MaskPath =
            "Assets/Game/Presentation/Materials/Proof/Textures/ControlledProofPaint_Mask.png";
        private const string EnvironmentProofPath =
            "Assets/Game/LegacyImport/Manifests/M2_EnvironmentProof.asset";
        private const string VehicleProofPath =
            "Assets/Game/LegacyImport/Manifests/M2_VehiclePartProof.asset";
        private const string LedgerPath = "Docs/Porting/PORTING_LEDGER.csv";

        private static readonly Bounds EnvironmentBounds = new Bounds(
            new Vector3(0.10478342f, 0.1064105f, 0.3524515f),
            new Vector3(4.770005f, 3.510401f, 0.345011f));

        private static readonly Bounds VehicleBounds = new Bounds(
            new Vector3(-0.03564f, 0.00002f, 0f),
            new Vector3(0.10178f, 0.174198f, 0.174516f));

        [MenuItem("Tools/My Summer Car/Legacy Import/Build Milestone 2 Controlled Proof Assets")]
        public static void Build()
        {
            DonorAssetRegistry registry =
                AssetDatabase.LoadAssetAtPath<DonorAssetRegistry>(RegistryPath);
            if (registry == null ||
                !registry.TryGetRecord(EnvironmentRecordId, out DonorAssetRecord environmentRecord) ||
                !registry.TryGetRecord(VehicleRecordId, out DonorAssetRecord vehicleRecord))
            {
                throw new InvalidOperationException(
                    "Execute the reviewed Milestone 2 donor manifest before building proof assets.");
            }

            ConfigureReferenceModel(environmentRecord.ReferenceAssetPath);
            ConfigureReferenceModel(vehicleRecord.ReferenceAssetPath);

            Texture2D[] textures = CreateAuthoredTextureSet();
            Material material = CreateHdrpMaterial(textures);
            Mesh roofLod0 = GetOrCreateMesh(EnvironmentMeshLod0Path, "ReauthoredGarageShedRoof_LOD0");
            Mesh roofLod1 = GetOrCreateMesh(EnvironmentMeshLod1Path, "ReauthoredGarageShedRoof_LOD1");
            BuildRoofMesh(roofLod0, simplified: false);
            BuildBoxMesh(roofLod1, EnvironmentBounds);

            Mesh brakeLod0 = GetOrCreateMesh(VehicleMeshLod0Path, "ReauthoredRearBrakeDrum_LOD0");
            Mesh brakeLod1 = GetOrCreateMesh(VehicleMeshLod1Path, "ReauthoredRearBrakeDrum_LOD1");
            BuildBrakeDrumMesh(brakeLod0, 32);
            BuildBrakeDrumMesh(brakeLod1, 12);

            GameObject environmentPrefab = CreateProofPrefab(
                "ReauthoredGarageShedRoof",
                EnvironmentPrefabPath,
                roofLod0,
                roofLod1,
                material,
                EnvironmentBounds,
                "RoofPivot");
            GameObject vehiclePrefab = CreateProofPrefab(
                "ReauthoredRearBrakeDrum",
                VehiclePrefabPath,
                brakeLod0,
                brakeLod1,
                material,
                VehicleBounds,
                "HubAxis");

            ApplyReplacementReadyState(registry);
            CreateOrUpdateProof(
                EnvironmentProofPath,
                registry,
                EnvironmentRecordId,
                DonorProofRole.Environment,
                environmentPrefab,
                textures,
                includeMountPoint: false);
            CreateOrUpdateProof(
                VehicleProofPath,
                registry,
                VehicleRecordId,
                DonorProofRole.VehiclePart,
                vehiclePrefab,
                textures,
                includeMountPoint: true);

            AssetDatabase.SaveAssets();
            DonorComparisonSceneBuilder.Build();
            UpdateProofLedger(environmentRecord, vehicleRecord);
            AssetDatabase.SaveAssets();
            Debug.Log("CONTROLLED_PROOF_BUILD_OK version=" + BuilderVersion);
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Controlled proof batch build requires batch mode.");
            }

            Build();
        }

        private static void ConfigureReferenceModel(string assetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Reference model importer is unavailable: " + assetPath);
            }

            importer.globalScale = 1f;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
        }

        private static Texture2D[] CreateAuthoredTextureSet()
        {
            EnsureAssetDirectory(BaseColorPath);
            WriteTexturePng(BaseColorPath, CreateBaseColorPixels(), linear: false, normalMap: false);
            WriteTexturePng(NormalPath, CreateNormalPixels(), linear: true, normalMap: true);
            WriteTexturePng(MaskPath, CreateMaskPixels(), linear: true, normalMap: false);
            return new[]
            {
                AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath),
                AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath),
                AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath)
            };
        }

        private static Color[] CreateBaseColorPixels()
        {
            const int size = 64;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float grain = ((x * 17 + y * 31) % 19) / 190f;
                    float seam = y % 16 == 0 ? -0.08f : 0f;
                    pixels[y * size + x] = new Color(
                        0.34f + grain + seam,
                        0.095f + grain * 0.35f,
                        0.055f + grain * 0.2f,
                        1f);
                }
            }

            return pixels;
        }

        private static Color[] CreateNormalPixels()
        {
            const int size = 64;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (((x * 13 + y * 7) % 11) - 5) * 0.007f;
                    float ny = (((x * 5 + y * 19) % 13) - 6) * 0.006f;
                    Vector3 normal = new Vector3(nx, ny, 1f).normalized;
                    pixels[y * size + x] = new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f,
                        1f);
                }
            }

            return pixels;
        }

        private static Color[] CreateMaskPixels()
        {
            const int size = 64;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wear = ((x * 23 + y * 29) % 17) / 170f;
                    pixels[y * size + x] = new Color(
                        0.82f,
                        0.9f - wear,
                        1f,
                        0.34f + wear);
                }
            }

            return pixels;
        }

        private static void WriteTexturePng(
            string assetPath,
            Color[] pixels,
            bool linear,
            bool normalMap)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false, linear);
            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Texture importer is unavailable: " + assetPath);
            }

            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !linear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        private static Material CreateHdrpMaterial(Texture2D[] textures)
        {
            EnsureAssetDirectory(MaterialPath);
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("HDRP/Lit shader is unavailable.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "ControlledProofPaint" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseColorMap", textures[0]);
            material.SetTexture("_NormalMap", textures[1]);
            material.SetTexture("_MaskMap", textures[2]);
            material.SetFloat("_NormalScale", 0.65f);
            material.SetFloat("_Metallic", 0.82f);
            material.SetFloat("_Smoothness", 0.36f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh GetOrCreateMesh(string assetPath, string meshName)
        {
            EnsureAssetDirectory(assetPath);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = meshName };
                AssetDatabase.CreateAsset(mesh, assetPath);
            }

            mesh.Clear();
            mesh.name = meshName;
            return mesh;
        }

        private static void BuildRoofMesh(Mesh mesh, bool simplified)
        {
            if (simplified)
            {
                BuildBoxMesh(mesh, EnvironmentBounds);
                return;
            }

            Vector3 min = EnvironmentBounds.min;
            Vector3 max = EnvironmentBounds.max;
            float middleY = (min.y + max.y) * 0.5f;
            var vertices = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, middleY, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, middleY, max.z)
            };
            int[] triangles =
            {
                0, 2, 1,
                3, 4, 5,
                0, 1, 4, 0, 4, 3,
                1, 2, 5, 1, 5, 4,
                2, 0, 3, 2, 3, 5
            };
            ApplyMesh(mesh, vertices, triangles, EnvironmentBounds);
        }

        private static void BuildBoxMesh(Mesh mesh, Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            var vertices = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z)
            };
            int[] triangles =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6,
                3, 0, 4, 3, 4, 7
            };
            ApplyMesh(mesh, vertices, triangles, bounds);
        }

        private static void BuildBrakeDrumMesh(Mesh mesh, int segmentCount)
        {
            Vector3 min = VehicleBounds.min;
            Vector3 max = VehicleBounds.max;
            float centerY = VehicleBounds.center.y;
            float centerZ = VehicleBounds.center.z;
            float radiusY = VehicleBounds.extents.y;
            float radiusZ = VehicleBounds.extents.z;
            var vertices = new Vector3[segmentCount * 2 + 2];
            var triangles = new int[segmentCount * 12];
            for (int index = 0; index < segmentCount; index++)
            {
                float angle = index * Mathf.PI * 2f / segmentCount;
                float y = centerY + Mathf.Cos(angle) * radiusY;
                float z = centerZ + Mathf.Sin(angle) * radiusZ;
                vertices[index * 2] = new Vector3(min.x, y, z);
                vertices[index * 2 + 1] = new Vector3(max.x, y, z);
            }

            int leftCenter = segmentCount * 2;
            int rightCenter = leftCenter + 1;
            vertices[leftCenter] = new Vector3(min.x, centerY, centerZ);
            vertices[rightCenter] = new Vector3(max.x, centerY, centerZ);
            int triangleIndex = 0;
            for (int index = 0; index < segmentCount; index++)
            {
                int next = (index + 1) % segmentCount;
                int left = index * 2;
                int right = left + 1;
                int nextLeft = next * 2;
                int nextRight = nextLeft + 1;
                triangles[triangleIndex++] = left;
                triangles[triangleIndex++] = right;
                triangles[triangleIndex++] = nextRight;
                triangles[triangleIndex++] = left;
                triangles[triangleIndex++] = nextRight;
                triangles[triangleIndex++] = nextLeft;
                triangles[triangleIndex++] = leftCenter;
                triangles[triangleIndex++] = nextLeft;
                triangles[triangleIndex++] = left;
                triangles[triangleIndex++] = rightCenter;
                triangles[triangleIndex++] = right;
                triangles[triangleIndex++] = nextRight;
            }

            ApplyMesh(mesh, vertices, triangles, VehicleBounds);
        }

        private static void ApplyMesh(Mesh mesh, Vector3[] vertices, int[] triangles, Bounds bounds)
        {
            var uv = new Vector2[vertices.Length];
            float safeX = Mathf.Max(bounds.size.x, 0.0001f);
            float safeY = Mathf.Max(bounds.size.y, 0.0001f);
            for (int index = 0; index < vertices.Length; index++)
            {
                uv[index] = new Vector2(
                    (vertices[index].x - bounds.min.x) / safeX,
                    (vertices[index].y - bounds.min.y) / safeY);
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
        }

        private static GameObject CreateProofPrefab(
            string name,
            string prefabPath,
            Mesh lod0Mesh,
            Mesh lod1Mesh,
            Material material,
            Bounds colliderBounds,
            string mountPointName)
        {
            EnsureAssetDirectory(prefabPath);
            var root = new GameObject(name);
            try
            {
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = colliderBounds.center;
                collider.size = colliderBounds.size;

                MeshRenderer lod0Renderer = AddLodChild(root.transform, "LOD0", lod0Mesh, material);
                MeshRenderer lod1Renderer = AddLodChild(root.transform, "LOD1", lod1Mesh, material);
                var mountPoint = new GameObject(mountPointName);
                mountPoint.transform.SetParent(root.transform, worldPositionStays: false);
                mountPoint.transform.localPosition = Vector3.zero;

                LODGroup lodGroup = root.AddComponent<LODGroup>();
                lodGroup.fadeMode = LODFadeMode.None;
                lodGroup.SetLODs(new[]
                {
                    new LOD(0.5f, new Renderer[] { lod0Renderer }),
                    new LOD(0.08f, new Renderer[] { lod1Renderer })
                });
                lodGroup.RecalculateBounds();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException("Could not save production proof prefab: " + prefabPath);
                }

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static MeshRenderer AddLodChild(
            Transform parent,
            string name,
            Mesh mesh,
            Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, worldPositionStays: false);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static void ApplyReplacementReadyState(DonorAssetRegistry registry)
        {
            var records = new List<DonorAssetRecord>(registry.Records.Count);
            foreach (DonorAssetRecord record in registry.Records)
            {
                records.Add(
                    record.RecordId == EnvironmentRecordId || record.RecordId == VehicleRecordId
                        ? record.WithStatus(DonorAssetStatus.ReplacementReady)
                        : record);
            }

            registry.ApplyManifest(new DonorAssetManifest(
                registry.ManifestId,
                DateTime.UtcNow.ToString("O"),
                registry.PipelineVersion,
                records));
            EditorUtility.SetDirty(registry);
        }

        private static void CreateOrUpdateProof(
            string proofPath,
            DonorAssetRegistry registry,
            string donorRecordId,
            DonorProofRole role,
            GameObject productionPrefab,
            Texture2D[] textures,
            bool includeMountPoint)
        {
            ReauthoredAssetProvenance proof =
                AssetDatabase.LoadAssetAtPath<ReauthoredAssetProvenance>(proofPath);
            if (proof == null)
            {
                proof = ScriptableObject.CreateInstance<ReauthoredAssetProvenance>();
                proof.name = Path.GetFileNameWithoutExtension(proofPath);
                AssetDatabase.CreateAsset(proof, proofPath);
            }

            var serializedProof = new SerializedObject(proof);
            serializedProof.FindProperty("registry").objectReferenceValue = registry;
            serializedProof.FindProperty("donorRecordId").stringValue = donorRecordId;
            serializedProof.FindProperty("role").enumValueIndex = (int)role;
            serializedProof.FindProperty("productionPrefab").objectReferenceValue = productionPrefab;
            serializedProof.FindProperty("referencePivotMeters").vector3Value = Vector3.zero;
            serializedProof.FindProperty("productionPivotMeters").vector3Value = Vector3.zero;
            serializedProof.FindProperty("dimensionalToleranceMeters").floatValue = 0.005f;
            serializedProof.FindProperty("authoringNotes").stringValue =
                "Independent procedural controlled-proof replacement. Geometry, textures, material, " +
                "collision and LODs are project-authored; donor mesh is reference-only and removable.";

            SerializedProperty mountPoints = serializedProof.FindProperty("mountPoints");
            mountPoints.arraySize = includeMountPoint ? 1 : 0;
            if (includeMountPoint)
            {
                SerializedProperty mount = mountPoints.GetArrayElementAtIndex(0);
                mount.FindPropertyRelative("name").stringValue = "HubAxis";
                mount.FindPropertyRelative("referenceLocalPositionMeters").vector3Value = Vector3.zero;
                mount.FindPropertyRelative("productionLocalPositionMeters").vector3Value = Vector3.zero;
            }

            SerializedProperty authoredTextures = serializedProof.FindProperty("authoredTextures");
            authoredTextures.arraySize = textures.Length;
            for (int index = 0; index < textures.Length; index++)
            {
                authoredTextures.GetArrayElementAtIndex(index).objectReferenceValue = textures[index];
            }

            serializedProof.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(proof);
        }

        private static void UpdateProofLedger(
            DonorAssetRecord environmentRecord,
            DonorAssetRecord vehicleRecord)
        {
            var entries = new List<PortingLedgerEntry>
            {
                CreateGeometryLedgerEntry(environmentRecord, EnvironmentPrefabPath, "Pitched roof approximation; donor material omitted."),
                CreateGeometryLedgerEntry(vehicleRecord, VehiclePrefabPath, "Simplified cylindrical proof mesh; internal brake detail omitted."),
                CreateAuthoredLedgerEntry("ControlledProofPaint", "Material", "ReauthoredMaterial", MaterialPath),
                CreateAuthoredLedgerEntry("ControlledProofPaint_BaseColor", "Texture2D", "ReauthoredTexture", BaseColorPath),
                CreateAuthoredLedgerEntry("ControlledProofPaint_Normal", "Texture2D", "ReauthoredTexture", NormalPath),
                CreateAuthoredLedgerEntry("ControlledProofPaint_Mask", "Texture2D", "ReauthoredTexture", MaskPath)
            };
            string existingCsv = File.Exists(LedgerPath) ? File.ReadAllText(LedgerPath) : string.Empty;
            PortingLedgerUpdatePlan plan = PortingLedgerUpdater.CreatePlan(existingCsv, entries);
            PortingLedgerUpdater.ExecuteFile(LedgerPath, plan);
            Debug.Log(
                $"Controlled proof ledger update: {plan.AddedCount} added, {plan.UpdatedCount} updated.");
        }

        private static PortingLedgerEntry CreateGeometryLedgerEntry(
            DonorAssetRecord record,
            string destinationPath,
            string knownDifferences)
        {
            return new PortingLedgerEntry(
                record.SourceRelativePath,
                record.SourceObjectName + " replacement",
                record.SourceSha256,
                "Prefab/Mesh",
                DonorTransferClassification.ReauthoredGeometry.ToString(),
                destinationPath,
                DonorAssetStatus.ReplacementReady.ToString(),
                record.ReferenceAssetPath,
                "MSC ControlledProofAssetBuilder " + BuilderVersion,
                knownDifferences,
                "Project-authored controlled proof with preserved local pivot; donor geometry is not referenced.");
        }

        private static PortingLedgerEntry CreateAuthoredLedgerEntry(
            string objectName,
            string type,
            string classification,
            string destinationPath)
        {
            return new PortingLedgerEntry(
                "ProjectAuthored/Milestone02",
                objectName,
                string.Empty,
                type,
                classification,
                destinationPath,
                "ControlledProofReady",
                string.Empty,
                "MSC ControlledProofAssetBuilder " + BuilderVersion,
                "Procedural proof asset; art-direction replacement is expected in a production art milestone.",
                "Contains no donor texture pixels or donor material data.");
        }

        private static void EnsureAssetDirectory(string assetPath)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(assetPath));
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Asset path has no directory: " + assetPath);
            }

            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
