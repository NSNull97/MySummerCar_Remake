using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MSC.World.Data
{
    public readonly struct WorldEntityPlacement
    {
        public WorldEntityPlacement(
            string stableId,
            long sourceObjectId,
            string hierarchyPath,
            string originalName,
            string category,
            Vector3 position,
            Bounds bounds,
            string meshGuid,
            bool active,
            string cellId,
            string interiorExterior,
            string landmarkTag,
            string replacementStatus,
            string transferStatus,
            bool referenceWorldEligible)
        {
            StableId = stableId;
            SourceObjectId = sourceObjectId;
            HierarchyPath = hierarchyPath;
            OriginalName = originalName;
            Category = category;
            Position = position;
            Bounds = bounds;
            MeshGuid = meshGuid;
            Active = active;
            CellId = cellId;
            InteriorExterior = interiorExterior;
            LandmarkTag = landmarkTag;
            ReplacementStatus = replacementStatus;
            TransferStatus = transferStatus;
            ReferenceWorldEligible = referenceWorldEligible;
        }

        public string StableId { get; }
        public long SourceObjectId { get; }
        public string HierarchyPath { get; }
        public string OriginalName { get; }
        public string Category { get; }
        public Vector3 Position { get; }
        public Bounds Bounds { get; }
        public string MeshGuid { get; }
        public bool Active { get; }
        public string CellId { get; }
        public string InteriorExterior { get; }
        public string LandmarkTag { get; }
        public string ReplacementStatus { get; }
        public string TransferStatus { get; }
        public bool ReferenceWorldEligible { get; }
    }

    public static class WorldEntityTable
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static IReadOnlyList<WorldEntityPlacement> Parse(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                throw new ArgumentException("World entity CSV must not be empty.", nameof(csv));
            }

            using var reader = new StringReader(csv);
            string headerLine = reader.ReadLine();
            if (headerLine == null)
            {
                throw new FormatException("World entity CSV has no header.");
            }

            List<string> headers = ParseRow(headerLine);
            var indices = new Dictionary<string, int>(headers.Count, StringComparer.Ordinal);
            for (int index = 0; index < headers.Count; index++)
            {
                if (!indices.TryAdd(headers[index], index))
                {
                    throw new FormatException("Duplicate world entity CSV column: " + headers[index]);
                }
            }

            RequireColumns(indices,
                "StableId", "SourceObjectId", "HierarchyPath", "OriginalName", "SemanticCategory",
                "ConvertedPositionX", "ConvertedPositionY", "ConvertedPositionZ",
                "ConvertedBoundsMinX", "ConvertedBoundsMinY", "ConvertedBoundsMinZ",
                "ConvertedBoundsMaxX", "ConvertedBoundsMaxY", "ConvertedBoundsMaxZ",
                "MeshGuid", "Active", "CellId", "InteriorExterior", "LandmarkTag",
                "ReplacementStatus", "TransferStatus", "ReferenceWorldEligible");

            var records = new List<WorldEntityPlacement>();
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> values = ParseRow(line);
                if (values.Count != headers.Count)
                {
                    throw new FormatException($"World entity CSV line {lineNumber} has {values.Count} values; expected {headers.Count}.");
                }

                string Get(string name) => values[indices[name]];
                float Number(string name) => float.Parse(Get(name), NumberStyles.Float, Invariant);
                Vector3 position = new Vector3(Number("ConvertedPositionX"), Number("ConvertedPositionY"), Number("ConvertedPositionZ"));
                Vector3 min = new Vector3(Number("ConvertedBoundsMinX"), Number("ConvertedBoundsMinY"), Number("ConvertedBoundsMinZ"));
                Vector3 max = new Vector3(Number("ConvertedBoundsMaxX"), Number("ConvertedBoundsMaxY"), Number("ConvertedBoundsMaxZ"));
                records.Add(new WorldEntityPlacement(
                    Get("StableId"),
                    long.Parse(Get("SourceObjectId"), NumberStyles.Integer, Invariant),
                    Get("HierarchyPath"),
                    Get("OriginalName"),
                    Get("SemanticCategory"),
                    position,
                    new Bounds((min + max) * 0.5f, max - min),
                    Get("MeshGuid"),
                    Get("Active") == "1",
                    Get("CellId"),
                    Get("InteriorExterior"),
                    Get("LandmarkTag"),
                    Get("ReplacementStatus"),
                    Get("TransferStatus"),
                    Get("ReferenceWorldEligible") == "1"));
            }

            return records;
        }

        public static List<string> ParseRow(string line)
        {
            var values = new List<string>();
            var current = new System.Text.StringBuilder();
            bool quoted = false;
            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];
                if (character == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (character == ',' && !quoted)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(character);
                }
            }

            if (quoted)
            {
                throw new FormatException("World entity CSV contains an unterminated quoted value.");
            }

            values.Add(current.ToString());
            return values;
        }

        private static void RequireColumns(IReadOnlyDictionary<string, int> indices, params string[] required)
        {
            foreach (string name in required)
            {
                if (!indices.ContainsKey(name))
                {
                    throw new FormatException("World entity CSV is missing required column: " + name);
                }
            }
        }
    }
}
