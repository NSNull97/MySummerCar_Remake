using System;

namespace MSC.LegacyImport
{
    public static class DonorWorldBaselineDisplayName
    {
        public static string Create(
            string sourceHierarchyPath,
            long sourceObjectId,
            string semanticCategory)
        {
            string[] segments = (sourceHierarchyPath ?? string.Empty)
                .Replace('\\', '/')
                .Split(
                    new[] { '/' },
                    StringSplitOptions.RemoveEmptyEntries);
            string location = segments.Length > 0
                ? Humanize(segments[0])
                : "Unknown Location";
            string leaf = segments.Length > 0
                ? Humanize(segments[segments.Length - 1])
                : "Unnamed Object";
            string parent = segments.Length > 1
                ? Humanize(segments[segments.Length - 2])
                : string.Empty;
            string category = Humanize(semanticCategory);

            if (string.Equals(
                    parent,
                    location,
                    StringComparison.OrdinalIgnoreCase))
            {
                parent = string.Empty;
            }

            string context = string.IsNullOrWhiteSpace(parent)
                ? location
                : location + " / " + parent;
            string prefix = string.IsNullOrWhiteSpace(category)
                ? string.Empty
                : category + " · ";
            return prefix + leaf + " · " + context +
                   " [" + sourceObjectId + "]";
        }

        private static string Humanize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Trim()
                .Replace('_', ' ')
                .Replace('-', ' ');
        }
    }
}
