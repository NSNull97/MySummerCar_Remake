using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMapMigration
{
    internal readonly struct MapClassificationEvidence
    {
        public string HierarchyPath { get; init; }
        public string ProvenancePath { get; init; }
        public string SemanticCategory { get; init; }
        public string MaterialText { get; init; }
        public string Layer { get; init; }
        public string Tag { get; init; }
        public string ObjectName { get; init; }
        public string MeshName { get; init; }
        public float UpwardTriangleRatio { get; init; }
        public float VerticalRange { get; init; }
        public bool HasMultipleHeightsAtSameXZ { get; init; }
    }

    internal readonly struct MapClassificationResult
    {
        public MapClassificationResult(MapMeshCategory category, string reason)
        {
            Category = category;
            Reason = reason;
        }

        public MapMeshCategory Category { get; }
        public string Reason { get; }
    }

    internal static class MapMeshClassifier
    {
        private static readonly string[] GroundPaths =
        {
            "map/mesh/terrain_obj/grass1",
            "map/mesh/terrain_obj/grass2",
            "map/mesh/terrain_obj/fields",
            "map/mesh/terrain_obj/4tie",
            "map/mesh/terrainout",
            "map/mesh/lakebed",
            "bettermsc/missingterrain"
        };

        private static readonly string[] RoadAsphaltPaths =
        {
            "map/mesh/terrain_obj/asphalt",
            "map/mesh/terrain_obj/pavement",
            "map/mesh/terrain_obj/road",
            "map/mesh/terrain_obj/roadside"
        };

        private static readonly string[] RoadDirtPaths =
        {
            "map/mesh/terrain_obj/dirtroad",
            "map/mesh/terrain_obj/gravel"
        };

        public static MapClassificationResult Classify(
            MapClassificationEvidence evidence)
        {
            string hierarchy = Normalize(
                evidence.ProvenancePath + "/" + evidence.HierarchyPath);
            string semantic = Normalize(evidence.SemanticCategory);

            MapClassificationResult? hierarchyResult =
                ClassifyHierarchy(hierarchy, semantic);
            if (hierarchyResult.HasValue)
            {
                return hierarchyResult.Value;
            }

            string materials = Normalize(evidence.MaterialText);
            MapClassificationResult? materialResult = ClassifyMaterial(materials);
            if (materialResult.HasValue)
            {
                return materialResult.Value;
            }

            string layerAndTag = Normalize(evidence.Layer + "/" + evidence.Tag);
            MapClassificationResult? layerResult =
                ClassifyTokens(layerAndTag, "layer/tag");
            if (layerResult.HasValue)
            {
                return layerResult.Value;
            }

            MapClassificationResult? objectResult =
                ClassifyTokens(Normalize(evidence.ObjectName), "object name");
            if (objectResult.HasValue)
            {
                return objectResult.Value;
            }

            MapClassificationResult? meshResult =
                ClassifyTokens(Normalize(evidence.MeshName), "mesh asset name");
            if (meshResult.HasValue)
            {
                return meshResult.Value;
            }

            if (evidence.HasMultipleHeightsAtSameXZ ||
                evidence.UpwardTriangleRatio < 0.45f)
            {
                return new MapClassificationResult(
                    MapMeshCategory.ResidualUnsupported,
                    "geometry is not safely representable by a single X/Z heightfield");
            }

            if (evidence.UpwardTriangleRatio >= 0.92f &&
                evidence.VerticalRange > 1f)
            {
                return new MapClassificationResult(
                    MapMeshCategory.Ambiguous,
                    "geometry appears heightfield-like but lacks authoritative hierarchy/material evidence");
            }

            return new MapClassificationResult(
                MapMeshCategory.Ambiguous,
                "no high-confidence evidence matched; retained as mesh");
        }

        private static MapClassificationResult? ClassifyHierarchy(
            string hierarchy,
            string semantic)
        {
            foreach (string path in GroundPaths)
            {
                if (ContainsPath(hierarchy, path))
                {
                    return Result(MapMeshCategory.GroundCandidate, "authoritative hierarchy path");
                }
            }

            foreach (string path in RoadDirtPaths)
            {
                if (ContainsPath(hierarchy, path))
                {
                    return Result(MapMeshCategory.RoadDirtOrGravel, "authoritative hierarchy path");
                }
            }

            foreach (string path in RoadAsphaltPaths)
            {
                if (ContainsPath(hierarchy, path))
                {
                    return Result(MapMeshCategory.RoadAsphalt, "authoritative hierarchy path");
                }
            }

            if (ContainsAny(
                    hierarchy,
                    "/trafficsigns/",
                    "/sign_road",
                    "/sign_railroad"))
            {
                return Result(
                    MapMeshCategory.Prop,
                    "traffic-sign hierarchy overrides coarse donor Road semantic");
            }

            if (ContainsAny(hierarchy, "map/mesh/railroad", "map/mesh/road_lines"))
            {
                return Result(
                    MapMeshCategory.RoadStructure,
                    "rail/road-marking overlay is retained outside the road-bed constraint set");
            }

            if (ContainsAny(hierarchy, "rockpale", "rockwall"))
            {
                return Result(
                    MapMeshCategory.ResidualUnsupported,
                    "rock overlay is retained because it is not base heightfield terrain");
            }

            if (ContainsAny(hierarchy, "trackfield", "skijumphill"))
            {
                return Result(
                    MapMeshCategory.ResidualUnsupported,
                    "surface overlay is retained independently from base terrain");
            }

            if (ContainsToken(semantic, "landmark") || ContainsToken(semantic, "rock"))
            {
                return Result(
                    MapMeshCategory.Prop,
                    "landmark/rock semantic is retained independently from base terrain");
            }

            if (ContainsToken(semantic, "utilitypole") ||
                ContainsToken(semantic, "utility"))
            {
                return Result(MapMeshCategory.Utility, "semantic utility evidence");
            }

            if (ContainsToken(semantic, "interactivepropcandidate") ||
                ContainsToken(semantic, "staticprop") ||
                ContainsToken(semantic, "roadsign") ||
                ContainsToken(semantic, "prop"))
            {
                return Result(MapMeshCategory.Prop, "semantic prop evidence");
            }

            if (ContainsAny(hierarchy, "bridge", "overpass", "culvert", "tunnel") ||
                ContainsToken(semantic, "bridge") ||
                ContainsToken(semantic, "roadstructure"))
            {
                return Result(MapMeshCategory.RoadStructure, "hierarchy/semantic road structure evidence");
            }

            if (ContainsAny(hierarchy, "water", "lake", "river", "pond", "sea") ||
                ContainsToken(semantic, "water"))
            {
                return Result(MapMeshCategory.Water, "hierarchy/semantic water evidence");
            }

            if (ContainsToken(semantic, "road") ||
                ContainsAny(hierarchy, "/roads/", "/road/"))
            {
                return Result(MapMeshCategory.RoadAsphalt, "hierarchy/semantic road evidence");
            }

            if (ContainsToken(semantic, "terrain") ||
                ContainsToken(semantic, "field") ||
                ContainsAny(hierarchy, "/terrain/", "/ground/"))
            {
                return Result(MapMeshCategory.GroundCandidate, "hierarchy/semantic ground evidence");
            }

            if (ContainsToken(semantic, "building") ||
                ContainsAnyToken(hierarchy, "building", "house", "garage", "cabin", "store"))
            {
                return Result(MapMeshCategory.Building, "hierarchy/semantic building evidence");
            }

            if (ContainsToken(semantic, "vegetation") ||
                ContainsToken(semantic, "vegetationtree") ||
                ContainsAnyToken(hierarchy, "tree", "forest", "bush", "vegetation", "spruce"))
            {
                return Result(MapMeshCategory.Vegetation, "hierarchy/semantic vegetation evidence");
            }

            if (ContainsAnyToken(hierarchy, "utility", "powerline", "wire", "pole", "lightpole"))
            {
                return Result(MapMeshCategory.Utility, "hierarchy/semantic utility evidence");
            }

            return null;
        }

        private static MapClassificationResult? ClassifyMaterial(string text)
        {
            if (ContainsAny(text, "asphalt", "tarmac", "pavement"))
            {
                return Result(MapMeshCategory.RoadAsphalt, "material evidence");
            }

            if (ContainsAny(text, "gravel", "dirtroad", "dirt_road"))
            {
                return Result(MapMeshCategory.RoadDirtOrGravel, "material evidence");
            }

            if (ContainsAny(text, "water", "lake", "river"))
            {
                return Result(MapMeshCategory.Water, "material evidence");
            }

            if (ContainsAny(text, "terrain", "ground", "grass", "field", "soil"))
            {
                return Result(MapMeshCategory.GroundCandidate, "material evidence");
            }

            return null;
        }

        private static MapClassificationResult? ClassifyTokens(
            string text,
            string evidenceSource)
        {
            if (ContainsAny(text, "bridge", "overpass", "culvert", "tunnel"))
            {
                return Result(MapMeshCategory.RoadStructure, evidenceSource);
            }

            if (ContainsAny(text, "asphalt", "road", "pavement"))
            {
                return Result(MapMeshCategory.RoadAsphalt, evidenceSource);
            }

            if (ContainsAny(text, "gravel", "dirtroad", "dirt_road"))
            {
                return Result(MapMeshCategory.RoadDirtOrGravel, evidenceSource);
            }

            if (ContainsAny(text, "water", "lake", "river", "pond"))
            {
                return Result(MapMeshCategory.Water, evidenceSource);
            }

            if (ContainsAny(text, "building", "house", "garage", "cabin"))
            {
                return Result(MapMeshCategory.Building, evidenceSource);
            }

            if (ContainsAny(text, "tree", "bush", "vegetation", "forest"))
            {
                return Result(MapMeshCategory.Vegetation, evidenceSource);
            }

            if (ContainsAny(text, "wire", "pole", "utility"))
            {
                return Result(MapMeshCategory.Utility, evidenceSource);
            }

            if (ContainsAny(text, "trigger", "technical", "helper", "lodproxy"))
            {
                return Result(MapMeshCategory.Technical, evidenceSource);
            }

            if (ContainsAny(text, "terrain", "ground", "field", "mesh_map2_", "mesh_terrain_"))
            {
                return Result(MapMeshCategory.GroundCandidate, evidenceSource);
            }

            return null;
        }

        private static MapClassificationResult Result(
            MapMeshCategory category,
            string reason) => new MapClassificationResult(category, reason);

        private static string Normalize(string value) =>
            (value ?? string.Empty).Replace('\\', '/').Trim().ToLowerInvariant();

        private static bool ContainsPath(string value, string path) =>
            value.Contains(path, StringComparison.Ordinal);

        private static bool ContainsAny(string value, params string[] tokens)
        {
            foreach (string token in tokens)
            {
                if (value.Contains(token, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAnyToken(string value, params string[] tokens)
        {
            foreach (string token in tokens)
            {
                if (ContainsToken(value, token))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsToken(string value, string token)
        {
            string[] segments = value.Split(
                new[] { '/', '\\', ' ', '_', '-', '.', ':', '·', '[', ']', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string segment in segments)
            {
                if (string.Equals(segment, token, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
