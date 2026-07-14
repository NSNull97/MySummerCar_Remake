using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MSC.LegacyImport.Editor.Ledger
{
    public static class PortingLedgerUpdater
    {
        public static readonly string[] Header =
        {
            "SourceRelativePath",
            "SourceObjectOrSymbol",
            "SourceHash",
            "Type",
            "Classification",
            "DestinationPath",
            "Status",
            "Dependencies",
            "ToolVersion",
            "KnownDifferences",
            "Notes"
        };

        public static PortingLedgerUpdatePlan CreatePlan(
            string existingCsv,
            IEnumerable<PortingLedgerEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            List<string[]> rows = ParseRows(existingCsv ?? string.Empty);
            if (rows.Count == 0)
            {
                rows.Add((string[])Header.Clone());
            }

            ValidateHeader(rows[0]);

            var rowIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                string[] row = rows[rowIndex];
                if (row.Length != Header.Length)
                {
                    throw new FormatException($"Ledger row {rowIndex + 1} has {row.Length} columns.");
                }

                string key = CreateKey(row[0], row[1], row[5]);
                if (!rowIndexByKey.TryAdd(key, rowIndex))
                {
                    throw new FormatException($"Ledger contains a duplicate provenance key at row {rowIndex + 1}.");
                }
            }

            int addedCount = 0;
            int updatedCount = 0;
            foreach (PortingLedgerEntry entry in entries)
            {
                if (entry == null)
                {
                    throw new ArgumentException("Ledger update contains a null entry.", nameof(entries));
                }

                string[] columns = entry.ToColumns();
                if (rowIndexByKey.TryGetValue(entry.Key, out int rowIndex))
                {
                    if (!RowsEqual(rows[rowIndex], columns))
                    {
                        rows[rowIndex] = columns;
                        updatedCount++;
                    }
                }
                else
                {
                    rowIndexByKey.Add(entry.Key, rows.Count);
                    rows.Add(columns);
                    addedCount++;
                }
            }

            return new PortingLedgerUpdatePlan(WriteRows(rows), addedCount, updatedCount);
        }

        public static void ExecuteFile(string path, PortingLedgerUpdatePlan plan)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Ledger path must not be empty.", nameof(path));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (!plan.HasChanges)
            {
                return;
            }

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Ledger path has no parent directory.");
            }

            string temporaryPath = Path.Combine(
                directory,
                Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(
                temporaryPath,
                plan.UpdatedCsv,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            try
            {
                if (File.Exists(fullPath))
                {
                    File.Replace(temporaryPath, fullPath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static List<string[]> ParseRows(string csv)
        {
            var rows = new List<string[]>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool insideQuotes = false;

            for (int index = 0; index < csv.Length; index++)
            {
                char character = csv[index];
                if (insideQuotes)
                {
                    if (character == '"')
                    {
                        bool escapedQuote = index + 1 < csv.Length && csv[index + 1] == '"';
                        if (escapedQuote)
                        {
                            field.Append('"');
                            index++;
                        }
                        else
                        {
                            insideQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(character);
                    }

                    continue;
                }

                if (character == '"' && field.Length == 0)
                {
                    insideQuotes = true;
                }
                else if (character == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else if (character == '\n')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    AddNonEmptyRow(rows, fields);
                    fields.Clear();
                }
                else if (character != '\r')
                {
                    field.Append(character);
                }
            }

            if (insideQuotes)
            {
                throw new FormatException("Ledger CSV ends inside a quoted field.");
            }

            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                AddNonEmptyRow(rows, fields);
            }

            return rows;
        }

        private static void AddNonEmptyRow(List<string[]> rows, List<string> fields)
        {
            if (fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0]))
            {
                return;
            }

            rows.Add(fields.ToArray());
        }

        private static string WriteRows(List<string[]> rows)
        {
            var builder = new StringBuilder();
            foreach (string[] row in rows)
            {
                for (int index = 0; index < row.Length; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append('"');
                    builder.Append((row[index] ?? string.Empty).Replace("\"", "\"\""));
                    builder.Append('"');
                }

                builder.Append("\r\n");
            }

            return builder.ToString();
        }

        private static void ValidateHeader(string[] header)
        {
            if (!RowsEqual(header, Header))
            {
                throw new FormatException("Porting ledger header does not match the required schema.");
            }
        }

        private static bool RowsEqual(string[] left, string[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string CreateKey(string sourcePath, string sourceObject, string destinationPath)
        {
            return sourcePath + "\u001f" + sourceObject + "\u001f" + destinationPath;
        }
    }
}
