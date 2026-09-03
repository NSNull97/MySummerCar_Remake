using System;
using System.Collections.Generic;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    internal sealed class VegetationMaskBrush
    {
        private readonly List<VegetationCellAsset> intersectingCells =
            new List<VegetationCellAsset>();
        private readonly HashSet<Texture2D> touchedTextures =
            new HashSet<Texture2D>();
        private readonly Dictionary<Texture2D, VegetationMaskEditState> maskStates =
            new Dictionary<Texture2D, VegetationMaskEditState>();
        private readonly HashSet<VegetationCellAsset> undoRegisteredCells =
            new HashSet<VegetationCellAsset>();
        private readonly VegetationDirtyTileRegistry dirtyTiles =
            new VegetationDirtyTileRegistry();

        private VegetationCellCatalog catalog;
        private VegetationDensityChannel channel;
        private VegetationBrushOperation operation;
        private int undoGroup = -1;
        private int strokeSeed;
        private bool strokeActive;

        public bool StrokeActive => strokeActive;

        public void BeginStroke(
            VegetationCellCatalog configuredCatalog,
            VegetationDensityChannel configuredChannel,
            VegetationBrushOperation configuredOperation)
        {
            if (strokeActive)
            {
                throw new InvalidOperationException(
                    "A vegetation brush stroke is already active.");
            }

            catalog = configuredCatalog != null
                ? configuredCatalog
                : throw new ArgumentNullException(nameof(configuredCatalog));
            channel = configuredChannel;
            operation = configuredOperation;
            touchedTextures.Clear();
            maskStates.Clear();
            undoRegisteredCells.Clear();
            dirtyTiles.Clear();
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Paint mesh vegetation");
            strokeSeed = unchecked(
                (int)VegetationStableHash.Hash(
                    (uint)(DateTime.UtcNow.Ticks & uint.MaxValue)));
            strokeActive = true;
        }

        public bool ApplyDab(
            Vector3 worldCenter,
            float radius,
            float strength,
            float hardness)
        {
            if (!strokeActive || catalog == null)
            {
                return false;
            }

            catalog.GetCellsIntersectingCircle(
                worldCenter,
                radius,
                intersectingCells);
            bool changed = false;
            for (int index = 0; index < intersectingCells.Count; index++)
            {
                VegetationCellAsset cell = intersectingCells[index];
                if (ApplyDabToCell(
                        cell,
                        worldCenter,
                        radius,
                        strength,
                        hardness))
                {
                    dirtyTiles.MarkCircle(cell, worldCenter, radius);
                    changed = true;
                }
            }

            return changed;
        }

        public void EndStroke()
        {
            if (!strokeActive)
            {
                return;
            }

            foreach (Texture2D texture in touchedTextures)
            {
                texture.Apply(false, false);
                if (!maskStates.TryGetValue(texture, out VegetationMaskEditState state) ||
                    state == null)
                {
                    throw new InvalidOperationException(
                        "Vegetation mask has no persistent Undo state: " +
                        AssetDatabase.GetAssetPath(texture));
                }

                VegetationMaskStorage.Commit(texture, state);
            }

            VegetationDirtyTile[] tiles = dirtyTiles.Consume();
            for (int index = 0; index < tiles.Length; index++)
            {
                VegetationCellAsset cell = tiles[index].Cell;
                if (cell != null && undoRegisteredCells.Add(cell))
                {
                    Undo.RegisterCompleteObjectUndo(
                        cell,
                        "Rebuild vegetation tiles");
                }
            }

            VegetationTileBuilder.RebuildDirtyTiles(catalog, tiles);
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            ResetStrokeState();
        }

        public void CancelStroke()
        {
            if (!strokeActive)
            {
                return;
            }

            Undo.RevertAllDownToGroup(undoGroup);
            ResetStrokeState();
        }

        private bool ApplyDabToCell(
            VegetationCellAsset cell,
            Vector3 worldCenter,
            float radius,
            float strength,
            float hardness)
        {
            Texture2D texture = cell.DensityMask;
            if (texture == null || !texture.isReadable)
            {
                return false;
            }

            if (touchedTextures.Add(texture))
            {
                VegetationMaskEditState state =
                    VegetationMaskStorage.GetState(texture);
                if (state == null)
                {
                    throw new InvalidOperationException(
                        "Vegetation mask has no edit-state asset: " +
                        AssetDatabase.GetAssetPath(texture));
                }

                maskStates.Add(texture, state);
                Undo.RegisterCompleteObjectUndo(
                    state,
                    "Paint vegetation density mask");
            }

            RectInt rectangle = cell.WorldCircleToPixelRect(worldCenter, radius);
            var rawPixels = texture.GetRawTextureData<Color32>();
            var source = new Color32[rectangle.width * rectangle.height];
            for (int localY = 0; localY < rectangle.height; localY++)
            {
                int sourceOffset =
                    (rectangle.y + localY) * texture.width + rectangle.x;
                int destinationOffset = localY * rectangle.width;
                for (int localX = 0; localX < rectangle.width; localX++)
                {
                    source[destinationOffset + localX] =
                        rawPixels[sourceOffset + localX];
                }
            }
            var modified = (Color32[])source.Clone();
            bool changed = false;
            float radiusSquared = radius * radius;
            float clampedStrength = Mathf.Clamp01(strength);
            float clampedHardness = Mathf.Clamp01(hardness);

            for (int localY = 0; localY < rectangle.height; localY++)
            {
                for (int localX = 0; localX < rectangle.width; localX++)
                {
                    int pixelX = rectangle.x + localX;
                    int pixelY = rectangle.y + localY;
                    Vector3 worldPosition = cell.MaskPixelToWorldXZ(
                        pixelX,
                        pixelY,
                        worldCenter.y);
                    float deltaX = worldPosition.x - worldCenter.x;
                    float deltaZ = worldPosition.z - worldCenter.z;
                    float distanceSquared = deltaX * deltaX + deltaZ * deltaZ;
                    if (distanceSquared > radiusSquared)
                    {
                        continue;
                    }

                    float normalizedDistance =
                        Mathf.Sqrt(distanceSquared) / Mathf.Max(radius, 0.0001f);
                    float falloff = CalculateFalloff(
                        normalizedDistance,
                        clampedHardness);
                    if (falloff <= 0f)
                    {
                        continue;
                    }

                    if (operation != VegetationBrushOperation.Erase &&
                        !CanPaintWorldPosition(worldPosition))
                    {
                        continue;
                    }

                    int pixelIndex = localY * rectangle.width + localX;
                    byte current = GetChannel(source[pixelIndex], channel);
                    byte next = CalculateNextValue(
                        current,
                        source,
                        rectangle.width,
                        rectangle.height,
                        localX,
                        localY,
                        pixelX,
                        pixelY,
                        clampedStrength * falloff);
                    if (next == current)
                    {
                        continue;
                    }

                    modified[pixelIndex] = SetChannel(
                        modified[pixelIndex],
                        channel,
                        next);
                    changed = true;
                }
            }

            if (changed)
            {
                texture.SetPixels32(
                    rectangle.x,
                    rectangle.y,
                    rectangle.width,
                    rectangle.height,
                    modified,
                    0);
                EditorUtility.SetDirty(texture);
            }

            return changed;
        }

        private bool CanPaintWorldPosition(Vector3 worldPosition)
        {
            var rayOrigin = new Vector3(
                worldPosition.x,
                catalog.CandidateRaycastHeight,
                worldPosition.z);
            return VegetationSceneRaycaster.TryResolve(
                new Ray(rayOrigin, Vector3.down),
                catalog.CandidateRaycastDistance,
                catalog.RelevantRaycastLayers,
                channel,
                out _,
                out _);
        }

        private byte CalculateNextValue(
            byte current,
            IReadOnlyList<Color32> source,
            int width,
            int height,
            int localX,
            int localY,
            int pixelX,
            int pixelY,
            float amount)
        {
            float current01 = current / 255f;
            float next01;
            switch (operation)
            {
                case VegetationBrushOperation.Erase:
                    next01 = Mathf.Clamp01(current01 - amount);
                    break;
                case VegetationBrushOperation.Smooth:
                    next01 = Mathf.Lerp(
                        current01,
                        CalculateNeighbourAverage(
                            source,
                            width,
                            height,
                            localX,
                            localY),
                        amount);
                    break;
                case VegetationBrushOperation.Noise:
                    uint hash = VegetationStableHash.Hash(
                        pixelX,
                        pixelY,
                        strokeSeed);
                    float noise = VegetationStableHash.ToUnitFloat(hash);
                    next01 = Mathf.Lerp(
                        current01,
                        noise,
                        amount);
                    break;
                case VegetationBrushOperation.Paint:
                default:
                    next01 = Mathf.Clamp01(current01 + amount);
                    break;
            }

            return (byte)Mathf.RoundToInt(next01 * 255f);
        }

        private float CalculateNeighbourAverage(
            IReadOnlyList<Color32> pixels,
            int width,
            int height,
            int centerX,
            int centerY)
        {
            int sum = 0;
            int count = 0;
            for (int y = Mathf.Max(0, centerY - 1);
                 y <= Mathf.Min(height - 1, centerY + 1);
                 y++)
            {
                for (int x = Mathf.Max(0, centerX - 1);
                     x <= Mathf.Min(width - 1, centerX + 1);
                     x++)
                {
                    sum += GetChannel(pixels[y * width + x], channel);
                    count++;
                }
            }

            return count > 0 ? sum / (255f * count) : 0f;
        }

        private static float CalculateFalloff(float distance01, float hardness)
        {
            if (distance01 >= 1f)
            {
                return 0f;
            }

            if (hardness >= 0.999f || distance01 <= hardness)
            {
                return 1f;
            }

            float normalized = Mathf.InverseLerp(hardness, 1f, distance01);
            return 1f - normalized * normalized * (3f - 2f * normalized);
        }

        private static byte GetChannel(
            Color32 color,
            VegetationDensityChannel densityChannel)
        {
            return densityChannel switch
            {
                VegetationDensityChannel.ShortGrass => color.r,
                VegetationDensityChannel.MeadowGrass => color.g,
                VegetationDensityChannel.TallGrass => color.b,
                VegetationDensityChannel.Decorative => color.a,
                _ => 0
            };
        }

        private static Color32 SetChannel(
            Color32 color,
            VegetationDensityChannel densityChannel,
            byte value)
        {
            switch (densityChannel)
            {
                case VegetationDensityChannel.ShortGrass:
                    color.r = value;
                    break;
                case VegetationDensityChannel.MeadowGrass:
                    color.g = value;
                    break;
                case VegetationDensityChannel.TallGrass:
                    color.b = value;
                    break;
                case VegetationDensityChannel.Decorative:
                    color.a = value;
                    break;
            }

            return color;
        }

        private void ResetStrokeState()
        {
            strokeActive = false;
            catalog = null;
            undoGroup = -1;
            touchedTextures.Clear();
            maskStates.Clear();
            undoRegisteredCells.Clear();
            dirtyTiles.Clear();
        }
    }
}
