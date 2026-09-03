using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MSCMapMigration
{
    internal sealed class TerrainMaterialPlan
    {
        public bool ExactPlanarTransfer { get; init; }
        public string Description { get; init; } = string.Empty;
        public Texture2D BaseTexture { get; init; }
        public Texture2D NormalTexture { get; init; }
        public float Ux { get; init; }
        public float UOffset { get; init; }
        public float Vz { get; init; }
        public float VOffset { get; init; }
        public Color PlaceholderColor { get; init; } = new Color(0.28f, 0.34f, 0.22f, 1f);
    }

    internal static class TerrainMaterialTransfer
    {
        public static TerrainMaterialPlan Analyze(
            IReadOnlyList<ScannedMeshInstance> ground,
            TerrainMaterialTransferMode mode)
        {
            if (mode == TerrainMaterialTransferMode.SafePlaceholder)
            {
                return Placeholder("Safe placeholder material was explicitly selected.");
            }

            Material[] materials = ground
                .SelectMany(instance => instance.Renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .ToArray();
            Texture2D[] baseTextures = materials
                .Select(GetBaseTexture)
                .Where(texture => texture != null)
                .Distinct()
                .ToArray();
            if (baseTextures.Length != 1)
            {
                return Placeholder(
                    $"Exact transfer requires one shared base-map texture; found {baseTextures.Length}.");
            }

            var samples = new List<(Vector3 position, Vector2 uv)>(8192);
            foreach (ScannedMeshInstance instance in ground)
            {
                ReadableMeshData data = MapMeshDataReader.Read(instance.Mesh);
                if (data.Uv0.Length != data.Vertices.Length)
                {
                    return Placeholder(
                        $"Mesh '{instance.Record.meshAssetName}' has no complete UV0 stream.");
                }

                int stride = Math.Max(1, data.Vertices.Length / 1024);
                for (int index = 0; index < data.Vertices.Length; index += stride)
                {
                    samples.Add((
                        instance.LocalToWorld.MultiplyPoint3x4(data.Vertices[index]),
                        data.Uv0[index]));
                }
            }

            if (samples.Count < 16 ||
                !TryFit(samples, out double[] u, out double[] v, out float rms))
            {
                return Placeholder("UV planarity fit was underdetermined.");
            }

            double uCross = Math.Abs(u[1]);
            double vCross = Math.Abs(v[0]);
            double uMain = Math.Abs(u[0]);
            double vMain = Math.Abs(v[1]);
            if (rms > 0.01f || uMain < 1e-8 || vMain < 1e-8 ||
                uCross > uMain * 0.01 || vCross > vMain * 0.01)
            {
                return Placeholder(
                    $"Ground UVs are not an axis-aligned global X/Z projection (RMS={rms:R}).");
            }

            Material representative = materials.First(material =>
                GetBaseTexture(material) == baseTextures[0]);
            Vector2 scale = GetBaseTextureScale(representative);
            Vector2 offset = GetBaseTextureOffset(representative);
            return new TerrainMaterialPlan
            {
                ExactPlanarTransfer = true,
                Description =
                    $"Global planar texture transfer accepted (UV RMS={rms:R}); per-tile offsets preserve continuity.",
                BaseTexture = baseTextures[0],
                NormalTexture = GetNormalTexture(representative),
                Ux = (float)(u[0] * scale.x),
                UOffset = (float)(u[2] * scale.x + offset.x),
                Vz = (float)(v[1] * scale.y),
                VOffset = (float)(v[2] * scale.y + offset.y)
            };
        }

        public static TerrainLayer CreateLayer(
            TerrainMaterialPlan plan,
            TerrainGridDomain domain,
            int tileX,
            int tileZ)
        {
            MapMigrationPaths.EnsureAssetFolder(MapMigrationPaths.TerrainLayerRoot);
            string name = $"MapMigration_Terrain_{tileX:000}_{tileZ:000}";
            var layer = new TerrainLayer { name = name };
            if (plan.ExactPlanarTransfer)
            {
                layer.diffuseTexture = plan.BaseTexture;
                layer.normalMapTexture = plan.NormalTexture;
                float tileSizeX = 1f / plan.Ux;
                float tileSizeZ = 1f / plan.Vz;
                float worldTileX = domain.Minimum.x + tileX * domain.TileSize;
                float worldTileZ = domain.Minimum.z + tileZ * domain.TileSize;
                layer.tileSize = new Vector2(tileSizeX, tileSizeZ);
                layer.tileOffset = new Vector2(
                    worldTileX + plan.UOffset / plan.Ux,
                    worldTileZ + plan.VOffset / plan.Vz);
            }
            else
            {
                layer.diffuseTexture = LoadOrCreatePlaceholderTexture(plan.PlaceholderColor);
                layer.tileSize = new Vector2(32f, 32f);
                layer.tileOffset = Vector2.zero;
                layer.metallic = 0f;
                layer.smoothness = 0f;
            }

            string path = $"{MapMigrationPaths.TerrainLayerRoot}/{name}.terrainlayer";
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        private static TerrainMaterialPlan Placeholder(string reason) =>
            new TerrainMaterialPlan
            {
                ExactPlanarTransfer = false,
                Description = reason +
                              " A valid matte HDRP-compatible TerrainLayer is used; source meshes remain available as reference."
            };

        private static Texture2D LoadOrCreatePlaceholderTexture(Color color)
        {
            const string path =
                MapMigrationPaths.TerrainLayerRoot + "/MapMigration_PlaceholderGround.asset";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: true)
            {
                name = "MapMigration_PlaceholderGround",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = Enumerable.Repeat(color, 16).ToArray();
            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private static bool TryFit(
            IReadOnlyList<(Vector3 position, Vector2 uv)> samples,
            out double[] u,
            out double[] v,
            out float rms)
        {
            u = Array.Empty<double>();
            v = Array.Empty<double>();
            var matrix = new double[3, 3];
            var rhsU = new double[3];
            var rhsV = new double[3];
            foreach ((Vector3 position, Vector2 uv) sample in samples)
            {
                double[] row = { sample.position.x, sample.position.z, 1d };
                for (int i = 0; i < 3; i++)
                {
                    rhsU[i] += row[i] * sample.uv.x;
                    rhsV[i] += row[i] * sample.uv.y;
                    for (int j = 0; j < 3; j++)
                    {
                        matrix[i, j] += row[i] * row[j];
                    }
                }
            }

            if (!Solve3x3(matrix, rhsU, out u))
            {
                rms = float.PositiveInfinity;
                return false;
            }

            if (!Solve3x3(matrix, rhsV, out v))
            {
                rms = float.PositiveInfinity;
                return false;
            }

            double sum = 0d;
            foreach ((Vector3 position, Vector2 uv) sample in samples)
            {
                double predictedU = u[0] * sample.position.x + u[1] * sample.position.z + u[2];
                double predictedV = v[0] * sample.position.x + v[1] * sample.position.z + v[2];
                double du = predictedU - sample.uv.x;
                double dv = predictedV - sample.uv.y;
                sum += du * du + dv * dv;
            }

            rms = (float)Math.Sqrt(sum / samples.Count);
            return true;
        }

        private static bool Solve3x3(double[,] input, double[] rhs, out double[] result)
        {
            var augmented = new double[3, 4];
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    augmented[row, column] = input[row, column];
                }

                augmented[row, 3] = rhs[row];
            }

            for (int pivot = 0; pivot < 3; pivot++)
            {
                int best = pivot;
                for (int row = pivot + 1; row < 3; row++)
                {
                    if (Math.Abs(augmented[row, pivot]) >
                        Math.Abs(augmented[best, pivot]))
                    {
                        best = row;
                    }
                }

                if (Math.Abs(augmented[best, pivot]) < 1e-12)
                {
                    result = Array.Empty<double>();
                    return false;
                }

                if (best != pivot)
                {
                    for (int column = pivot; column < 4; column++)
                    {
                        (augmented[pivot, column], augmented[best, column]) =
                            (augmented[best, column], augmented[pivot, column]);
                    }
                }

                double divisor = augmented[pivot, pivot];
                for (int column = pivot; column < 4; column++)
                {
                    augmented[pivot, column] /= divisor;
                }

                for (int row = 0; row < 3; row++)
                {
                    if (row == pivot)
                    {
                        continue;
                    }

                    double factor = augmented[row, pivot];
                    for (int column = pivot; column < 4; column++)
                    {
                        augmented[row, column] -= factor * augmented[pivot, column];
                    }
                }
            }

            result = new[] { augmented[0, 3], augmented[1, 3], augmented[2, 3] };
            return true;
        }

        private static Texture2D GetBaseTexture(Material material) =>
            GetTexture(material, "_BaseColorMap", "_BaseMap", "_MainTex");

        private static Texture2D GetNormalTexture(Material material) =>
            GetTexture(material, "_NormalMap", "_BumpMap");

        private static Texture2D GetTexture(Material material, params string[] properties)
        {
            foreach (string property in properties)
            {
                if (material.HasProperty(property) &&
                    material.GetTexture(property) is Texture2D texture)
                {
                    return texture;
                }
            }

            return null;
        }

        private static Vector2 GetBaseTextureScale(Material material)
        {
            foreach (string property in new[] { "_BaseColorMap", "_BaseMap", "_MainTex" })
            {
                if (material.HasProperty(property))
                {
                    return material.GetTextureScale(property);
                }
            }

            return Vector2.one;
        }

        private static Vector2 GetBaseTextureOffset(Material material)
        {
            foreach (string property in new[] { "_BaseColorMap", "_BaseMap", "_MainTex" })
            {
                if (material.HasProperty(property))
                {
                    return material.GetTextureOffset(property);
                }
            }

            return Vector2.zero;
        }
    }
}
