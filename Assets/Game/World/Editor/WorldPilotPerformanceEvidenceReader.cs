using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace MSC.World.Remaster.Editor
{
    public sealed class WorldPilotPerformanceEvidenceReadResult
    {
        internal WorldPilotPerformanceEvidenceReadResult(
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

    public static class WorldPilotPerformanceEvidenceReader
    {
        public const string ValidatorId = "m05b1-bounded-world-performance";
        public const string EvidencePath =
            "Docs/WorldValidation/M05B1_WORLD_PERFORMANCE_EVIDENCE.json";

        private const string ExpectedCaptureId = "m05b1-bounded-world-pilot-1080p";
        private const string ExpectedSourceRevision =
            "53861b52ac6f8d9308f3e6f315c973a390210cd2";
        private const string ExpectedRawCaptureSha256 =
            "da9f0dc40d23d2c65ae2b7e7d1d90243b4b00a1da07a51ea26df9e0a9b285ad6";
        private const string ExpectedPlayerLogSha256 =
            "5a76f1b47aa94daec6440f80fbded0153fe1e709b6394a2260155cd7189cd972";
        private const string ExpectedBuildLogSha256 =
            "3ad4ee0ec9e163006c74c2ff434f90c9b6f1b3cfe239901745a62ba5de46f186";

        private static readonly string[] ExpectedLocationIds =
        {
            "pilot-home",
            "dense-vegetation",
            "interior-transition",
            "water-shoreline"
        };

        private static readonly Dictionary<string, string> ExpectedScreenshotHashes =
            new(StringComparer.Ordinal)
            {
                ["pilot-home"] = "6cce9e9433b6e768ede3ef439528bc8801a6bc884772f0ad4dd20280b4e0aedf",
                ["dense-vegetation"] = "791adedc16d05204e9e8d8c67fd152e91a523a0235b6bfa34653367be148400a",
                ["interior-transition"] = "bc92b7b4c48598a63b1ffb9f7cfc488524fe1633509035fbc480db6dc241be8b",
                ["water-shoreline"] = "52403bc1e073059fc7670e4ba94e208421ee8b889742e89d3f49d030edd8dbbe"
            };

        private static readonly Dictionary<string, string> ExpectedScreenshotSignatures =
            new(StringComparer.Ordinal)
            {
                ["pilot-home"] = "7a2e86a2a0060dcc",
                ["dense-vegetation"] = "83b1a2ea83fa1733",
                ["interior-transition"] = "52412486e8dcd226",
                ["water-shoreline"] = "52bfaa562fd5a58f"
            };

        public static WorldPilotPerformanceEvidenceReadResult ReadAndValidate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            string absoluteEvidencePath = ResolveProjectPath(EvidencePath);
            if (!File.Exists(absoluteEvidencePath))
            {
                return new WorldPilotPerformanceEvidenceReadResult(
                    executed: false,
                    passed: false,
                    evidence: "Bounded world performance evidence has not been captured.",
                    errors: new[] { $"Missing evidence file: {EvidencePath}" },
                    warnings: Array.Empty<string>());
            }

            WorldPilotPerformanceEvidence evidence;
            try
            {
                evidence = JsonUtility.FromJson<WorldPilotPerformanceEvidence>(
                    File.ReadAllText(absoluteEvidencePath));
            }
            catch (Exception exception)
            {
                errors.Add("Performance evidence JSON could not be parsed: " + exception.Message);
                return Result(errors, warnings, null);
            }

            if (evidence == null)
            {
                errors.Add("Performance evidence JSON produced a null object.");
                return Result(errors, warnings, null);
            }

            Require(evidence.schemaVersion == 1, "Performance evidence schemaVersion must be 1.", errors);
            Require(string.Equals(evidence.validatorId, ValidatorId, StringComparison.Ordinal),
                $"Performance evidence validatorId must be '{ValidatorId}'.", errors);
            Require(evidence.passed, "Performance evidence is not marked passed.", errors);
            Require(string.Equals(evidence.scope, "bounded-pilot-steady-state", StringComparison.Ordinal),
                "Performance evidence scope is not the accepted bounded pilot scope.", errors);
            Require(string.Equals(evidence.captureId, ExpectedCaptureId, StringComparison.Ordinal),
                "Performance capture ID differs from the accepted contract.", errors);
            Require(string.Equals(evidence.sourceRevision, ExpectedSourceRevision, StringComparison.Ordinal),
                "Performance capture source revision differs from the accepted snapshot.", errors);
            Require(evidence.sourceDirtyEntryCount == 53,
                "Performance capture must record exactly 53 dirty source entries at acceptance.", errors);
            Require(evidence.sourceProvenanceMismatchCountAtAcceptance == 0,
                "Performance capture provenance did not match its accepted source snapshot.", errors);
            Require(string.Equals(evidence.rawCaptureSha256, ExpectedRawCaptureSha256, StringComparison.Ordinal),
                "Raw performance capture SHA-256 differs from the accepted contract.", errors);
            Require(string.Equals(evidence.playerLogSha256, ExpectedPlayerLogSha256, StringComparison.Ordinal),
                "Performance player log SHA-256 differs from the accepted contract.", errors);
            Require(string.Equals(evidence.buildLogSha256, ExpectedBuildLogSha256, StringComparison.Ordinal),
                "Performance build log SHA-256 differs from the accepted contract.", errors);
            Require(evidence.buildSizeBytes == 220524597L,
                "Performance build size differs from the accepted build.", errors);
            Require(string.Equals(evidence.unityVersion, Application.unityVersion, StringComparison.Ordinal),
                $"Capture Unity version '{evidence.unityVersion}' differs from current '{Application.unityVersion}'.", errors);
            Require(evidence.requestedWidth == 1920 && evidence.requestedHeight == 1080,
                "Performance evidence must be 1920x1080.", errors);
            Require(evidence.warmupFramesPerLocation == 120 && evidence.measuredFramesPerLocation == 300,
                "Performance evidence must use 120 warmup and 300 measured frames per location.", errors);
            Require(Math.Abs(evidence.bootstrapFixedExposureEv100 - 14f) <= 0.0001f,
                "Performance evidence must use Bootstrap fixed exposure EV100 14.", errors);
            Require(string.Equals(evidence.renderMethod, "ordinary-player-backbuffer", StringComparison.Ordinal),
                "Measured frames must use the ordinary player backbuffer.", errors);
            Require(string.Equals(
                    evidence.verificationScreenshotMethod,
                    "hdrp-standard-request-rendertexture-argb32-srgb-after-measured-frames",
                    StringComparison.Ordinal),
                "Screenshot verification method differs from the accepted method.", errors);

            WorldPilotPerformanceLocationEvidence[] locations = evidence.locations ?? Array.Empty<WorldPilotPerformanceLocationEvidence>();
            Require(locations.Length == ExpectedLocationIds.Length,
                $"Performance evidence must contain {ExpectedLocationIds.Length} locations.", errors);
            string[] actualIds = locations.Select(location => location?.locationId ?? string.Empty).ToArray();
            Require(actualIds.SequenceEqual(ExpectedLocationIds, StringComparer.Ordinal),
                "Performance location order or IDs differ from the accepted contract.", errors);

            var signatures = new HashSet<string>(StringComparer.Ordinal);
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldPilotPerformanceLocationEvidence location in locations)
            {
                if (location == null)
                {
                    errors.Add("Performance evidence contains a null location record.");
                    continue;
                }

                Require(IsFinitePositive(location.meanFrameTimeMilliseconds),
                    $"{location.locationId}: mean frame time must be finite and positive.", errors);
                Require(IsFinitePositive(location.percentile95FrameTimeMilliseconds),
                    $"{location.locationId}: p95 frame time must be finite and positive.", errors);
                Require(IsFinitePositive(location.worstFrameTimeMilliseconds),
                    $"{location.locationId}: worst frame time must be finite and positive.", errors);
                Require(location.gpuFrameTimingSamples > 0,
                    $"{location.locationId}: GPU FrameTiming samples are unavailable.", errors);
                Require(IsFinitePositive(location.percentile95CpuFrameTimeMilliseconds) &&
                        IsFinitePositive(location.percentile95GpuFrameTimeMilliseconds),
                    $"{location.locationId}: CPU/GPU p95 must be finite and positive.", errors);
                Require(IsFinitePositive(location.meanDrawCalls) &&
                        IsFinitePositive(location.meanBatches) &&
                        IsFinitePositive(location.meanSetPassCalls),
                    $"{location.locationId}: draw/batch/SetPass counters must be finite and positive.", errors);
                Require(IsFinitePositive(location.meanUsedMemoryBytes),
                    $"{location.locationId}: used-memory counter must be finite and positive.", errors);
                Require(location.visualValidationPassed,
                    $"{location.locationId}: verification screenshot is not accepted.", errors);

                if (ExpectedScreenshotHashes.TryGetValue(location.locationId, out string expectedHash))
                {
                    Require(string.Equals(location.screenshotSha256, expectedHash, StringComparison.Ordinal),
                        $"{location.locationId}: screenshot SHA-256 differs from the accepted capture.", errors);
                }

                if (ExpectedScreenshotSignatures.TryGetValue(location.locationId, out string expectedSignature))
                {
                    Require(string.Equals(location.screenshotSampleSignature, expectedSignature, StringComparison.Ordinal),
                        $"{location.locationId}: screenshot signature differs from the accepted capture.", errors);
                }

                hashes.Add(location.screenshotSha256 ?? string.Empty);
                signatures.Add(location.screenshotSampleSignature ?? string.Empty);
                ValidateOptionalArtifact(location.screenshotPath, location.screenshotSha256, warnings, errors);
            }

            Require(hashes.Count == ExpectedLocationIds.Length,
                "Performance screenshots must have four distinct SHA-256 values.", errors);
            Require(signatures.Count == ExpectedLocationIds.Length,
                "Performance screenshots must have four distinct sampled signatures.", errors);

            ValidateOptionalArtifact(evidence.rawCapturePath, evidence.rawCaptureSha256, warnings, errors);
            ValidateOptionalArtifact(evidence.playerLogPath, evidence.playerLogSha256, warnings, errors);
            ValidateOptionalArtifact(evidence.buildLogPath, evidence.buildLogSha256, warnings, errors);

            string summary = string.Join(
                ";",
                locations.Select(location =>
                    $"{location.locationId}:p95={location.percentile95FrameTimeMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)}ms,gpuSamples={location.gpuFrameTimingSamples}"));
            return Result(errors, warnings, evidence, summary);
        }

        private static WorldPilotPerformanceEvidenceReadResult Result(
            IReadOnlyCollection<string> errors,
            IReadOnlyCollection<string> warnings,
            WorldPilotPerformanceEvidence evidence,
            string summary = "")
        {
            string text = evidence == null
                ? "Bounded world performance evidence could not be read."
                : $"capture={evidence.captureId};locations={evidence.locations?.Length ?? 0};" +
                  $"resolution={evidence.requestedWidth}x{evidence.requestedHeight};" +
                  $"rawSha256={evidence.rawCaptureSha256};{summary}";
            return new WorldPilotPerformanceEvidenceReadResult(
                executed: true,
                passed: errors.Count == 0,
                evidence: text,
                errors: errors,
                warnings: warnings);
        }

        private static void ValidateOptionalArtifact(
            string projectRelativePath,
            string expectedSha256,
            ICollection<string> warnings,
            ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath))
            {
                errors.Add("Performance evidence contains an empty artifact path.");
                return;
            }

            string absolutePath = ResolveProjectPath(projectRelativePath);
            if (!File.Exists(absolutePath))
            {
                warnings.Add($"Ignored local artifact is unavailable: {projectRelativePath}");
                return;
            }

            string actualSha256 = ComputeSha256(absolutePath);
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
            {
                errors.Add($"Artifact SHA-256 mismatch: {projectRelativePath}");
            }
        }

        private static string ResolveProjectPath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath));
        }

        private static string ComputeSha256(string path)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(sha256.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;

        private static void Require(bool condition, string error, ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(error);
            }
        }

        // JsonUtility assigns these private DTO fields reflectively.
#pragma warning disable 0649
        [Serializable]
        private sealed class WorldPilotPerformanceEvidence
        {
            public int schemaVersion;
            public string validatorId;
            public bool passed;
            public string scope;
            public string captureId;
            public string capturedUtc;
            public string sourceRevision;
            public string sourceWorkingTreeState;
            public int sourceDirtyEntryCount;
            public int sourceProvenanceMismatchCountAtAcceptance;
            public string rawCapturePath;
            public string rawCaptureSha256;
            public string playerLogPath;
            public string playerLogSha256;
            public string buildLogPath;
            public string buildLogSha256;
            public long buildSizeBytes;
            public string unityVersion;
            public string operatingSystem;
            public string processor;
            public int processorCount;
            public int systemMemoryMegabytes;
            public string graphicsDevice;
            public int graphicsMemoryMegabytes;
            public string graphicsApi;
            public string qualityLevel;
            public int requestedWidth;
            public int requestedHeight;
            public int warmupFramesPerLocation;
            public int measuredFramesPerLocation;
            public string renderMethod;
            public string verificationScreenshotMethod;
            public float bootstrapFixedExposureEv100;
            public WorldPilotPerformanceLocationEvidence[] locations;
            public string[] unavailableMetrics;
            public string[] unavailableLocations;
            public string[] limitations;
        }

        [Serializable]
        private sealed class WorldPilotPerformanceLocationEvidence
        {
            public string locationId;
            public double meanFrameTimeMilliseconds;
            public double percentile95FrameTimeMilliseconds;
            public double worstFrameTimeMilliseconds;
            public double percentile95CpuFrameTimeMilliseconds;
            public double percentile95GpuFrameTimeMilliseconds;
            public int gpuFrameTimingSamples;
            public double meanDrawCalls;
            public double meanBatches;
            public double meanSetPassCalls;
            public double meanUsedMemoryBytes;
            public bool visualValidationPassed;
            public string screenshotPath;
            public string screenshotSha256;
            public string screenshotSampleSignature;
        }
#pragma warning restore 0649
    }
}
