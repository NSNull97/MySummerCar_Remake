using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MSC.World.Remaster.Editor
{
    public sealed class ProductionWorldStreamingLifecycleEvidenceReadResult
    {
        internal ProductionWorldStreamingLifecycleEvidenceReadResult(
            bool executed,
            bool passed,
            string evidence,
            IEnumerable<string> errors,
            IEnumerable<string> warnings)
        {
            Executed = executed;
            Passed = passed;
            Evidence = evidence ?? string.Empty;
            Errors = (errors ?? Array.Empty<string>()).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).ToArray();
        }

        public bool Executed { get; }
        public bool Passed { get; }
        public string Evidence { get; }
        public string[] Errors { get; }
        public string[] Warnings { get; }
    }

    public static class ProductionWorldStreamingLifecycleEvidenceReader
    {
        public const int CurrentSchemaVersion = 1;
        public const string ValidatorId = "production-streaming-lifecycle";
        public const string EvidencePath =
            "Docs/WorldValidation/M05B1_PRODUCTION_STREAMING_LIFECYCLE.json";

        private const string ManifestPath =
            "Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset";
        private const string BootstrapScenePath = "Assets/Game/Bootstrap/Bootstrap.unity";

        private static readonly string[] ExpectedSequence =
        {
            "cycle-1:pilot",
            "cycle-1:pilot+next",
            "cycle-1:next",
            "cycle-1:none",
            "cycle-2:pilot",
            "cycle-2:pilot+next",
            "cycle-2:next",
            "cycle-2:none"
        };

        private static readonly int[] ExpectedOwnedSceneCounts = { 1, 2, 1, 0, 1, 2, 1, 0 };
        private static readonly bool[] ExpectedCleanup = { true, true };

        private static readonly string[] ExpectedPilotStableIds =
        {
            "3be598c0aa9798dd8ab43e20f4a35e8f",
            "504a5620f62904b2d93d7803efc4eecf",
            "6f4c37ebb3d0b019392e9fe6a16da65d",
            "bb26b42e9fe463fd254bc0478a56946d",
            "c4bbad1ab8714ff807bee9195a77399a",
            "fc6a437b97ea997ca03a5e8bad1ba9b7",
            "ff8e5e6cb145461b84108d4f24eaee07"
        };

        private static readonly string[] ExpectedNextZoneStableIds =
        {
            "0b03b3508f6be68192698ac5f7ae7550",
            "2d9650c25f6324b6367493f48c56c908",
            "615645f7ce4d6ee6836547a00c3dedb9",
            "68783d0b852bcffb1eced37581f4aff8",
            "7201412942822b5b4724b5e72c74065f",
            "d7b7b8ce9d3e448a346b5344e18c2023",
            "dcd8f98c103fb4eb4640d2abb757d089",
            "dd8587032e6c12708151d60bd881aec6"
        };

        private static readonly string[] ImplementationPaths =
        {
            "Assets/Game/World/Runtime/Partition/WorldPartition.cs",
            "Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingManifest.cs",
            "Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingService.cs",
            "Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs",
            "Assets/Game/Bootstrap/GameServiceBindings.cs",
            "Assets/Game/Editor/WorldStreaming/ProductionWorldStreamingBuilder.cs",
            "Assets/Game/Editor/WorldStreaming/WorldPilotGateRemediationValidator.cs",
            "Assets/Game/World/Editor/ProductionWorldStreamingLifecycleEvidenceReader.cs",
            "Assets/Game/World/Editor/WorldValidationRunner.cs",
            "Assets/Game/Tests/PlayMode/WorldRemaster/WorldRemasterPlayModeTests.cs"
        };

        public static ProductionWorldStreamingLifecycleEvidenceReadResult ReadAndValidate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            string projectRoot = GetProjectRoot();
            string absoluteEvidencePath = ResolveProjectPath(projectRoot, EvidencePath);
            if (!File.Exists(absoluteEvidencePath))
            {
                errors.Add("Production streaming lifecycle evidence has not been produced: " + EvidencePath);
                return Result(executed: false, errors, warnings, "Lifecycle evidence file is missing.");
            }

            ProductionWorldStreamingLifecycleEvidenceDto evidence;
            try
            {
                evidence = JsonUtility.FromJson<ProductionWorldStreamingLifecycleEvidenceDto>(
                    File.ReadAllText(absoluteEvidencePath));
            }
            catch (Exception exception)
            {
                errors.Add("Production streaming lifecycle evidence JSON cannot be read: " + exception.Message);
                return Result(executed: true, errors, warnings, "Lifecycle evidence JSON is invalid.");
            }

            if (evidence == null)
            {
                errors.Add("Production streaming lifecycle evidence JSON is empty.");
                return Result(executed: true, errors, warnings, "Lifecycle evidence JSON is empty.");
            }

            ValidateContract(evidence, errors);
            ValidateCurrentFingerprints(projectRoot, evidence, errors);
            string summary = errors.Count == 0
                ? $"Production streamer completed {evidence.completedCycles} clean two-cell lifecycle cycles; " +
                  $"sequenceSteps={evidence.completedSequence?.Length ?? 0}; implementation={evidence.implementationFingerprint}."
                : "Production streaming lifecycle evidence is stale or inconsistent with the current project.";
            return Result(executed: true, errors, warnings, summary);
        }

        private static void ValidateContract(
            ProductionWorldStreamingLifecycleEvidenceDto evidence,
            ICollection<string> errors)
        {
            if (evidence.schemaVersion != CurrentSchemaVersion)
            {
                errors.Add($"Production streaming lifecycle evidence schema must be {CurrentSchemaVersion}.");
            }

            if (!string.Equals(evidence.validatorId, ValidatorId, StringComparison.Ordinal) || !evidence.passed)
            {
                errors.Add("Production streaming lifecycle evidence is not a passing contract result.");
            }

            if (!string.Equals(evidence.unityVersion, Application.unityVersion, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Production streaming lifecycle evidence Unity version {evidence.unityVersion} " +
                    $"does not match current {Application.unityVersion}.");
            }

            if (evidence.completedCycles != 2)
            {
                errors.Add("Production streaming lifecycle evidence must contain exactly two completed cycles.");
            }

            if (!(evidence.completedSequence ?? Array.Empty<string>())
                .SequenceEqual(ExpectedSequence, StringComparer.Ordinal))
            {
                errors.Add("Production streaming lifecycle sequence must be pilot -> both -> next -> none twice.");
            }

            if (!(evidence.ownedSceneCounts ?? Array.Empty<int>()).SequenceEqual(ExpectedOwnedSceneCounts))
            {
                errors.Add("Production streaming lifecycle owned-scene counts must be 1,2,1,0 for both cycles.");
            }

            if (!(evidence.pilotRootsDestroyed ?? Array.Empty<bool>()).SequenceEqual(ExpectedCleanup) ||
                !(evidence.nextRootsDestroyed ?? Array.Empty<bool>()).SequenceEqual(ExpectedCleanup))
            {
                errors.Add("Production streaming lifecycle evidence does not prove root cleanup for both cells and cycles.");
            }

            if (!evidence.stableIdsUnique)
            {
                errors.Add("Production streaming lifecycle evidence does not prove unique stable IDs after each reload.");
            }

            string[] pilotIds = evidence.pilotStableIds ?? Array.Empty<string>();
            string[] nextIds = evidence.nextZoneStableIds ?? Array.Empty<string>();
            if (pilotIds.Distinct(StringComparer.Ordinal).Count() != pilotIds.Length ||
                nextIds.Distinct(StringComparer.Ordinal).Count() != nextIds.Length ||
                !pilotIds.SequenceEqual(ExpectedPilotStableIds, StringComparer.Ordinal) ||
                !nextIds.SequenceEqual(ExpectedNextZoneStableIds, StringComparer.Ordinal))
            {
                errors.Add("Production streaming lifecycle evidence stable-ID snapshots are missing, duplicated, or stale.");
            }
        }

        private static void ValidateCurrentFingerprints(
            string projectRoot,
            ProductionWorldStreamingLifecycleEvidenceDto evidence,
            ICollection<string> errors)
        {
            ValidateFileFingerprint(
                projectRoot,
                ManifestPath,
                evidence.manifestFingerprint,
                "production streaming manifest",
                errors);
            ValidateFileFingerprint(
                projectRoot,
                BootstrapScenePath,
                evidence.bootstrapFingerprint,
                "Bootstrap scene",
                errors);

            string currentImplementationFingerprint;
            try
            {
                currentImplementationFingerprint = CalculateSourceSetFingerprint(projectRoot, ImplementationPaths);
            }
            catch (Exception exception)
            {
                errors.Add("Production streaming implementation fingerprint cannot be calculated: " + exception.Message);
                return;
            }

            if (!string.Equals(
                    currentImplementationFingerprint,
                    evidence.implementationFingerprint,
                    StringComparison.Ordinal))
            {
                errors.Add("Production streaming lifecycle evidence is stale for the current implementation sources.");
            }
        }

        private static void ValidateFileFingerprint(
            string projectRoot,
            string relativePath,
            string recordedFingerprint,
            string label,
            ICollection<string> errors)
        {
            try
            {
                string currentFingerprint = CalculateFileSha256(ResolveProjectPath(projectRoot, relativePath));
                if (!string.Equals(currentFingerprint, recordedFingerprint, StringComparison.Ordinal))
                {
                    errors.Add("Production streaming lifecycle evidence is stale for the current " + label + ".");
                }
            }
            catch (Exception exception)
            {
                errors.Add("Current " + label + " fingerprint cannot be calculated: " + exception.Message);
            }
        }

        private static ProductionWorldStreamingLifecycleEvidenceReadResult Result(
            bool executed,
            ICollection<string> errors,
            ICollection<string> warnings,
            string evidence) =>
            new ProductionWorldStreamingLifecycleEvidenceReadResult(
                executed,
                executed && errors.Count == 0,
                evidence,
                errors,
                warnings);

        private static string GetProjectRoot() =>
            Directory.GetParent(Application.dataPath)?.FullName ??
            throw new InvalidOperationException("Unity project root cannot be resolved.");

        private static string ResolveProjectPath(string projectRoot, string projectRelativePath) =>
            Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));

        private static string CalculateSourceSetFingerprint(string projectRoot, string[] relativePaths)
        {
            var canonical = new StringBuilder();
            foreach (string relativePath in relativePaths
                         .Select(path => path.Replace('\\', '/'))
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                string absolutePath = ResolveProjectPath(projectRoot, relativePath);
                canonical.Append(relativePath).Append('\n')
                    .Append(CalculateFileSha256(absolutePath)).Append('\n');
            }

            using SHA256 sha256 = SHA256.Create();
            return ToLowerHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())));
        }

        private static string CalculateFileSha256(string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("Fingerprint input is missing.", absolutePath);
            }

            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(absolutePath);
            return ToLowerHex(sha256.ComputeHash(stream));
        }

        private static string ToLowerHex(byte[] hash)
        {
            var output = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                output.Append(value.ToString("x2"));
            }

            return output.ToString();
        }

        [Serializable]
        private sealed class ProductionWorldStreamingLifecycleEvidenceDto
        {
            public int schemaVersion = 0;
            public string validatorId = string.Empty;
            public bool passed = false;
            public string unityVersion = string.Empty;
            public int completedCycles = 0;
            public string[] completedSequence = Array.Empty<string>();
            public int[] ownedSceneCounts = Array.Empty<int>();
            public bool[] pilotRootsDestroyed = Array.Empty<bool>();
            public bool[] nextRootsDestroyed = Array.Empty<bool>();
            public bool stableIdsUnique = false;
            public string[] pilotStableIds = Array.Empty<string>();
            public string[] nextZoneStableIds = Array.Empty<string>();
            public string manifestFingerprint = string.Empty;
            public string bootstrapFingerprint = string.Empty;
            public string implementationFingerprint = string.Empty;
        }
    }
}
