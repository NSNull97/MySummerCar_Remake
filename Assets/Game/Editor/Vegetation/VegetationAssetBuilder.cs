using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.World.Partition;
using MSC.World.Streaming;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Editor.Vegetation
{
    public static class VegetationAssetBuilder
    {
        public const string ManifestPath =
            "Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset";
        public const string RootPath =
            "Assets/Game/World/Content/Vegetation";
        public const string CellPath = RootPath + "/Cells";
        public const string MaskPath = RootPath + "/Masks";
        public const string ProfilePath = RootPath + "/Profiles";
        public const string MeshPath = RootPath + "/Meshes";
        public const string MaterialPath = RootPath + "/Materials";
        public const string TexturePath = RootPath + "/Textures";
        public const string CatalogPath =
            RootPath + "/VegetationCellCatalog.asset";
        public const string RuntimePrefabPath =
            RootPath + "/VegetationWorldRuntime.prefab";
        public const string ShaderPath =
            "Assets/Game/Presentation/Shaders/Vegetation/MSC_VegetationIndirectHDRP.shader";

        private const int MaskResolution = 512;
        private const float TileSizeMeters = 32f;
        private const float MinimumWorldY = -2048f;
        private const float MaximumWorldY = 2048f;

        [MenuItem(
            "Tools/MSC Remake/Vegetation/Create or Update Cell Assets",
            priority = 1900)]
        public static void Build()
        {
            EnsureFolders();
            ProductionWorldStreamingManifest manifest =
                AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(
                    ManifestPath);
            if (manifest == null)
            {
                throw new InvalidOperationException(
                    "Missing production streaming manifest at " + ManifestPath + ".");
            }

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Missing vegetation HDRP shader at " + ShaderPath + ".");
            }

            Texture2D bladeTexture = CreateOrUpdateBladeTexture();
            Material material = CreateOrUpdateMaterial(shader, bladeTexture);
            Mesh nearMesh = CreateOrUpdateCrossMesh(
                MeshPath + "/VegetationCluster_Near.asset",
                3,
                0.42f,
                1f);
            Mesh middleMesh = CreateOrUpdateCrossMesh(
                MeshPath + "/VegetationCluster_Middle.asset",
                2,
                0.46f,
                0.95f);
            Mesh farMesh = CreateOrUpdateCrossMesh(
                MeshPath + "/VegetationCluster_Far.asset",
                2,
                0.5f,
                0.9f);
            VegetationProfile[] profiles = CreateOrUpdateProfiles(
                nearMesh,
                middleMesh,
                farMesh,
                material);
            VegetationCellAsset[] cells = CreateOrUpdateCells(manifest);
            VegetationCellCatalog catalog = LoadOrCreateAsset<VegetationCellCatalog>(
                CatalogPath);
            catalog.ConfigureForAuthoring(
                cells,
                profiles,
                ~0,
                MaximumWorldY,
                MaximumWorldY - MinimumWorldY);
            EditorUtility.SetDirty(catalog);
            CreateOrUpdateRuntimePrefab(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            IReadOnlyList<string> errors = Validate(manifest, catalog);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Vegetation asset build failed:\n" +
                    string.Join("\n", errors));
            }

            Debug.Log(
                $"MESH_VEGETATION_ASSET_BUILD_OK cells={cells.Length} " +
                $"profiles={profiles.Length} mask={MaskResolution} " +
                $"tile={TileSizeMeters:0}m");
        }

        public static void BuildAndValidate()
        {
            Build();
        }

        [MenuItem(
            "Tools/MSC Remake/Vegetation/Validate Production Configuration",
            priority = 1901)]
        public static void ValidateMenu()
        {
            ProductionWorldStreamingManifest manifest =
                AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(
                    ManifestPath);
            VegetationCellCatalog catalog =
                AssetDatabase.LoadAssetAtPath<VegetationCellCatalog>(CatalogPath);
            IReadOnlyList<string> errors = Validate(manifest, catalog);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Vegetation configuration is invalid:\n" +
                    string.Join("\n", errors));
            }

            Debug.Log(
                $"MESH_VEGETATION_VALIDATION_OK cells={catalog.Cells.Count} " +
                $"profiles={catalog.Profiles.Count}");
        }

        private static VegetationCellAsset[] CreateOrUpdateCells(
            ProductionWorldStreamingManifest manifest)
        {
            IReadOnlyList<ProductionWorldCellScene> manifestCells =
                manifest.Cells;
            var cells = new VegetationCellAsset[manifestCells.Count];
            for (int index = 0; index < manifestCells.Count; index++)
            {
                ProductionWorldCellScene manifestCell = manifestCells[index];
                WorldCellIndex cellIndex = manifestCell.Index;
                string safeId = manifestCell.CellId.Replace("-", "_");
                string maskAssetPath =
                    $"{MaskPath}/{safeId}_VegetationDensity.png";
                Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    maskAssetPath);
                if (mask == null)
                {
                    string legacyMaskPath =
                        $"{MaskPath}/{safeId}_VegetationDensity.asset";
                    Texture2D legacyMask =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(legacyMaskPath);
                    var generatedMask = new Texture2D(
                        MaskResolution,
                        MaskResolution,
                        TextureFormat.RGBA32,
                        false,
                        true)
                    {
                        name = safeId + "_VegetationDensity",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        anisoLevel = 0
                    };
                    if (legacyMask != null &&
                        legacyMask.width == MaskResolution &&
                        legacyMask.height == MaskResolution)
                    {
                        generatedMask.SetPixels32(legacyMask.GetPixels32());
                    }
                    else
                    {
                        var raw = generatedMask.GetRawTextureData<Color32>();
                        for (int pixel = 0; pixel < raw.Length; pixel++)
                        {
                            raw[pixel] = new Color32(0, 0, 0, 0);
                        }
                    }

                    generatedMask.Apply(false, false);
                    byte[] encoded = generatedMask.EncodeToPNG();
                    UnityEngine.Object.DestroyImmediate(generatedMask);
                    File.WriteAllBytes(ToAbsolutePath(maskAssetPath), encoded);
                    AssetDatabase.ImportAsset(
                        maskAssetPath,
                        ImportAssetOptions.ForceUpdate);
                    ConfigureMaskImporter(maskAssetPath);
                    mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskAssetPath);

                    if (legacyMask != null)
                    {
                        AssetDatabase.DeleteAsset(legacyMaskPath);
                    }
                }
                else if (mask.width != MaskResolution ||
                         mask.height != MaskResolution)
                {
                    throw new InvalidOperationException(
                        maskAssetPath + " has an incompatible authored resolution.");
                }
                else
                {
                    ConfigureMaskImporter(maskAssetPath);
                    mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskAssetPath);
                }

                byte[] currentMaskBytes =
                    File.ReadAllBytes(ToAbsolutePath(maskAssetPath));
                VegetationMaskStorage.CreateOrUpdateState(mask, currentMaskBytes);

                string cellAssetPath =
                    $"{CellPath}/{safeId}_VegetationCell.asset";
                VegetationCellAsset cell =
                    LoadOrCreateAsset<VegetationCellAsset>(cellAssetPath);
                Bounds bounds = WorldCellMembershipUtility.GetBounds(
                    cellIndex,
                    manifest.CellSizeMeters,
                    MinimumWorldY,
                    MaximumWorldY).Bounds;
                cell.ConfigureForAuthoring(
                    manifestCell.CellId,
                    cellIndex,
                    bounds,
                    MaskResolution,
                    TileSizeMeters,
                    mask);
                EditorUtility.SetDirty(mask);
                EditorUtility.SetDirty(cell);
                cells[index] = cell;
            }

            return cells
                .OrderBy(cell => cell.CellIndex.Z)
                .ThenBy(cell => cell.CellIndex.X)
                .ToArray();
        }

        private static VegetationProfile[] CreateOrUpdateProfiles(
            Mesh nearMesh,
            Mesh middleMesh,
            Mesh farMesh,
            Material material)
        {
            var profiles = new[]
            {
                ConfigureProfile(
                    "ShortGrass",
                    "vegetation.short-grass",
                    VegetationDensityChannel.ShortGrass,
                    0.55f,
                    1f,
                    35f,
                    new Vector2(0.32f, 0.55f),
                    0.15f,
                    new Vector3(28f, 72f, 150f),
                    113),
                ConfigureProfile(
                    "MeadowGrass",
                    "vegetation.meadow-grass",
                    VegetationDensityChannel.MeadowGrass,
                    0.72f,
                    1f,
                    32f,
                    new Vector2(0.55f, 0.95f),
                    0.2f,
                    new Vector3(32f, 82f, 170f),
                    227),
                ConfigureProfile(
                    "TallGrass",
                    "vegetation.tall-grass",
                    VegetationDensityChannel.TallGrass,
                    1.05f,
                    1f,
                    26f,
                    new Vector2(0.9f, 1.45f),
                    0.25f,
                    new Vector3(38f, 92f, 180f),
                    349),
                ConfigureProfile(
                    "Decorative",
                    "vegetation.decorative",
                    VegetationDensityChannel.Decorative,
                    1.8f,
                    0.85f,
                    30f,
                    new Vector2(0.45f, 0.95f),
                    0.25f,
                    new Vector3(34f, 78f, 145f),
                    463)
            };

            for (int index = 0; index < profiles.Length; index++)
            {
                profiles[index].ConfigureForAuthoring(
                    profiles[index].ProfileId,
                    profiles[index].DensityChannel,
                    profiles[index].CandidateSpacingMeters,
                    profiles[index].DensityMultiplier,
                    profiles[index].MaximumSlopeDegrees,
                    new Vector2(MinimumWorldY, MaximumWorldY),
                    profiles[index].UniformScaleRange,
                    profiles[index].SurfaceNormalAlignment,
                    nearMesh,
                    middleMesh,
                    farMesh,
                    material,
                    new Vector3(
                        profiles[index].MiddleLodDistance,
                        profiles[index].FarLodDistance,
                        profiles[index].CullingDistance),
                    7f,
                    index == 0
                        ? ShadowCastingMode.Off
                        : ShadowCastingMode.On,
                    true,
                    profiles[index].StableSeed);
                EditorUtility.SetDirty(profiles[index]);
            }

            return profiles;
        }

        private static VegetationProfile ConfigureProfile(
            string assetName,
            string profileId,
            VegetationDensityChannel channel,
            float spacing,
            float densityMultiplier,
            float maximumSlope,
            Vector2 scaleRange,
            float normalAlignment,
            Vector3 lodDistances,
            int seed)
        {
            VegetationProfile profile =
                LoadOrCreateAsset<VegetationProfile>(
                    $"{ProfilePath}/{assetName}.asset");
            profile.ConfigureForAuthoring(
                profileId,
                channel,
                spacing,
                densityMultiplier,
                maximumSlope,
                new Vector2(MinimumWorldY, MaximumWorldY),
                scaleRange,
                normalAlignment,
                null,
                null,
                null,
                null,
                lodDistances,
                7f,
                ShadowCastingMode.On,
                true,
                seed);
            return profile;
        }

        private static Texture2D CreateOrUpdateBladeTexture()
        {
            string path = TexturePath + "/VegetationBladeMask.asset";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(
                    64,
                    64,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name = "VegetationBladeMask",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            var pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                float vertical = y / 63f;
                float halfWidth = Mathf.Lerp(0.46f, 0.025f, vertical);
                for (int x = 0; x < 64; x++)
                {
                    float horizontal = Mathf.Abs(x / 63f - 0.5f);
                    byte alpha = horizontal <= halfWidth ? (byte)255 : (byte)0;
                    byte green = (byte)Mathf.RoundToInt(
                        Mathf.Lerp(170f, 230f, vertical));
                    pixels[y * 64 + x] = new Color32(90, green, 62, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Material CreateOrUpdateMaterial(
            Shader shader,
            Texture2D bladeTexture)
        {
            string path = MaterialPath + "/VegetationIndirect_HDRP.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "VegetationIndirect_HDRP",
                    enableInstancing = true
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", bladeTexture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Cutoff", 0.42f);
            material.SetFloat("_WindStrength", 0.12f);
            material.SetFloat("_WindSpeed", 1.35f);
            material.SetFloat("_WindFrequency", 0.17f);
            material.SetFloat("_ColorVariation", 0.06f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh CreateOrUpdateCrossMesh(
            string assetPath,
            int planeCount,
            float width,
            float height)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(assetPath)
                };
                AssetDatabase.CreateAsset(mesh, assetPath);
            }

            var vertices = new List<Vector3>(planeCount * 4);
            var normals = new List<Vector3>(planeCount * 4);
            var uvs = new List<Vector2>(planeCount * 4);
            var triangles = new List<int>(planeCount * 6);
            for (int plane = 0; plane < planeCount; plane++)
            {
                float angle = plane / (float)planeCount * Mathf.PI;
                Vector3 right = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 normal = Vector3.Cross(Vector3.up, right).normalized;
                int baseVertex = vertices.Count;
                vertices.Add(-right * width * 0.5f);
                vertices.Add(right * width * 0.5f);
                vertices.Add(-right * width * 0.5f + Vector3.up * height);
                vertices.Add(right * width * 0.5f + Vector3.up * height);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                triangles.Add(baseVertex);
                triangles.Add(baseVertex + 2);
                triangles.Add(baseVertex + 1);
                triangles.Add(baseVertex + 2);
                triangles.Add(baseVertex + 3);
                triangles.Add(baseVertex + 1);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void CreateOrUpdateRuntimePrefab(
            VegetationCellCatalog catalog)
        {
            var root = new GameObject("MSC Mesh Vegetation Runtime");
            try
            {
                VegetationWorldRenderer renderer =
                    root.AddComponent<VegetationWorldRenderer>();
                VegetationDebugRenderer debug =
                    root.AddComponent<VegetationDebugRenderer>();
                renderer.ConfigureForAuthoring(catalog);
                debug.ConfigureForAuthoring(catalog);
                PrefabUtility.SaveAsPrefabAsset(root, RuntimePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static IReadOnlyList<string> Validate(
            ProductionWorldStreamingManifest manifest,
            VegetationCellCatalog catalog)
        {
            var errors = new List<string>();
            if (manifest == null)
            {
                errors.Add("Production streaming manifest is missing.");
                return errors;
            }

            if (catalog == null)
            {
                errors.Add("Vegetation cell catalog is missing.");
                return errors;
            }

            if (catalog.Cells.Count != manifest.Cells.Count)
            {
                errors.Add(
                    $"Catalog has {catalog.Cells.Count} cells but streaming " +
                    $"manifest has {manifest.Cells.Count}.");
            }

            var catalogCellIds = new HashSet<string>(
                catalog.Cells
                    .Where(cell => cell != null)
                    .Select(cell => cell.CellId),
                StringComparer.Ordinal);
            for (int index = 0; index < manifest.Cells.Count; index++)
            {
                if (!catalogCellIds.Contains(manifest.Cells[index].CellId))
                {
                    errors.Add(
                        "Missing vegetation cell for " +
                        manifest.Cells[index].CellId + ".");
                }
            }

            IReadOnlyList<string> catalogErrors = catalog.ValidateConfiguration();
            for (int index = 0; index < catalogErrors.Count; index++)
            {
                errors.Add(catalogErrors[index]);
            }

            return errors;
        }

        private static T LoadOrCreateAsset<T>(string assetPath)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Game/World/Content", "Vegetation");
            EnsureFolder(RootPath, "Cells");
            EnsureFolder(RootPath, "Masks");
            EnsureFolder(RootPath, "Profiles");
            EnsureFolder(RootPath, "Meshes");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Textures");
        }

        private static void ConfigureMaskImporter(string assetPath)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Vegetation mask is not imported as a texture: " + assetPath);
            }

            bool changed =
                !importer.isReadable ||
                importer.mipmapEnabled ||
                importer.sRGBTexture ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.textureCompression != TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException(
                    "Unable to resolve Unity project root.");
            return Path.GetFullPath(
                Path.Combine(projectRoot, projectRelativePath));
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
