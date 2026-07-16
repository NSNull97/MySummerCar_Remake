using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Core.Identity;
using MSC.World.Data;
using MSC.World.Partition;
using MSC.World.Streaming;
using UnityEngine;

namespace MSC.Editor.WorldBaseline
{
    public sealed class WorldGameplayAnchorManifestData
    {
        public WorldGameplayAnchorManifestData(
            WorldGameplayAnchorRecord[] records,
            string fingerprintSha256)
        {
            Records = records;
            FingerprintSha256 = fingerprintSha256;
        }

        public IReadOnlyList<WorldGameplayAnchorRecord> Records { get; }
        public string FingerprintSha256 { get; }
    }

    public static class WorldGameplayAnchorManifest
    {
        public const int ExpectedAnchorCount = 15;

        public static WorldGameplayAnchorManifestData Load()
        {
            string path = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaseline06B2Paths.GameplayAnchorManifest);
            using var reader = new StreamReader(path);
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            var indices = new Dictionary<string, int>(
                StringComparer.Ordinal);
            for (int index = 0; index < headers.Count; index++)
            {
                indices.Add(headers[index], index);
            }

            string[] required =
            {
                "AnchorId", "StableEntityId", "CellX", "CellZ",
                "PositionX", "PositionY", "PositionZ",
                "EulerX", "EulerY", "EulerZ"
            };
            foreach (string column in required)
            {
                if (!indices.ContainsKey(column))
                {
                    throw new FormatException(
                        "06B2 gameplay-anchor manifest lacks column " +
                        column + ".");
                }
            }

            var records = new List<WorldGameplayAnchorRecord>();
            var anchorIds = new HashSet<string>(StringComparer.Ordinal);
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> values = WorldEntityTable.ParseRow(line);
                if (values.Count != headers.Count)
                {
                    throw new FormatException(
                        $"06B2 gameplay-anchor line {lineNumber} has " +
                        $"{values.Count} values; expected {headers.Count}.");
                }

                string Get(string column) => values[indices[column]];
                float Number(string column) => float.Parse(
                    Get(column),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture);
                int Integer(string column) => int.Parse(
                    Get(column),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture);

                string anchorId = Get("AnchorId");
                string stableId = Get("StableEntityId");
                if (!anchorIds.Add(anchorId) ||
                    !stableIds.Add(stableId) ||
                    !StableEntityId.TryParse(stableId, out _) ||
                    !string.Equals(
                        anchorId,
                        "world-gameplay:" + stableId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "06B2 gameplay-anchor identity is invalid or " +
                        "duplicated at line " + lineNumber + ".");
                }

                var position = new Vector3(
                    Number("PositionX"),
                    Number("PositionY"),
                    Number("PositionZ"));
                var euler = new Vector3(
                    Number("EulerX"),
                    Number("EulerY"),
                    Number("EulerZ"));
                if (!IsFinite(position) || !IsFinite(euler))
                {
                    throw new InvalidDataException(
                        "06B2 gameplay-anchor transform is non-finite at " +
                        "line " + lineNumber + ".");
                }

                var cell = new WorldCellIndex(
                    Integer("CellX"),
                    Integer("CellZ"));
                if (!cell.Equals(new WorldCellIndex(0, -3)) &&
                    !cell.Equals(new WorldCellIndex(0, -2)))
                {
                    throw new InvalidDataException(
                        "06B2 gameplay-anchor source cell is outside the " +
                        "accepted bounded 05B.1 migration set at line " +
                        lineNumber + ": " + cell.Id + ".");
                }
                records.Add(new WorldGameplayAnchorRecord(
                    anchorId,
                    stableId,
                    cell,
                    position,
                    Quaternion.Euler(euler)));
            }

            WorldGameplayAnchorRecord[] ordered = records
                .OrderBy(
                    record => record.StableEntityId,
                    StringComparer.Ordinal)
                .ToArray();
            if (ordered.Length != ExpectedAnchorCount)
            {
                throw new InvalidDataException(
                    "06B2 gameplay-anchor count drifted; expected " +
                    ExpectedAnchorCount + ", got " + ordered.Length + ".");
            }

            return new WorldGameplayAnchorManifestData(
                ordered,
                ComputeFingerprint(ordered));
        }

        private static string ComputeFingerprint(
            IEnumerable<WorldGameplayAnchorRecord> records)
        {
            var builder = new StringBuilder();
            foreach (WorldGameplayAnchorRecord record in records)
            {
                builder.Append(record.AnchorId).Append('|')
                    .Append(record.StableEntityId).Append('|')
                    .Append(record.Cell.X).Append('|')
                    .Append(record.Cell.Z).Append('|')
                    .Append(record.Position.x.ToString(
                        "R",
                        CultureInfo.InvariantCulture)).Append('|')
                    .Append(record.Position.y.ToString(
                        "R",
                        CultureInfo.InvariantCulture)).Append('|')
                    .Append(record.Position.z.ToString(
                        "R",
                        CultureInfo.InvariantCulture)).Append('|')
                    .Append(record.Rotation.x.ToString(
                        "R",
                        CultureInfo.InvariantCulture)).Append('|')
                    .Append(record.Rotation.y.ToString(
                        "R",
                        CultureInfo.InvariantCulture)).Append('|')
                    .Append(record.Rotation.z.ToString(
                        "R",
                        CultureInfo.InvariantCulture)).Append('|')
                    .Append(record.Rotation.w.ToString(
                        "R",
                        CultureInfo.InvariantCulture)).Append('\n');
            }

            return DonorWorldBaselineManifest.Sha256Text(
                builder.ToString());
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
