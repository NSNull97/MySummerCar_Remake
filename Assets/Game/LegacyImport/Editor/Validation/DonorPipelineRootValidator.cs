using System;
using System.Collections.Generic;
using System.IO;
using MSC.LegacyImport.Editor.Configuration;

namespace MSC.LegacyImport.Editor.Validation
{
    public static class DonorPipelineRootValidator
    {
        public static IReadOnlyList<string> Validate(
            string donorRoot,
            string stagingRoot,
            string projectRoot)
        {
            string donor = DonorPathConfiguration.NormalizeDirectoryPath(donorRoot);
            string staging = DonorPathConfiguration.NormalizeDirectoryPath(stagingRoot);
            string project = DonorPathConfiguration.NormalizeDirectoryPath(projectRoot);
            var errors = new List<string>();

            ValidatePair("donor", donor, "staging", staging, errors);
            ValidatePair("donor", donor, "project", project, errors);
            ValidatePair("staging", staging, "project", project, errors);
            return errors;
        }

        private static void ValidatePair(
            string leftName,
            string left,
            string rightName,
            string right,
            List<string> errors)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Configured {leftName} and {rightName} roots are identical.");
                return;
            }

            if (Contains(left, right) || Contains(right, left))
            {
                errors.Add(
                    $"Configured {leftName} and {rightName} roots must not contain one another.");
            }
        }

        private static bool Contains(string parent, string child)
        {
            string parentWithSeparator = parent.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return child.StartsWith(parentWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
    }
}
