using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MSC.Editor.WorldTransfer;
using MSC.World.Data;
using MSC.World.Streaming;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MSC.Editor.WorldBaseline
{
    [Serializable]
    public sealed class WorldBaseline06B3FileHashRecord
    {
        public string path = string.Empty;
        public long lengthBytes;
        public string sha256 = string.Empty;
    }

    public sealed class DonorWorldBaselineFreezeValidationResult
    {
        private readonly List<WorldBaseline06B3FileHashRecord>
            requiredOutputFileHashes =
                new List<WorldBaseline06B3FileHashRecord>();

        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool Passed => Errors.Count == 0;
        public string Status => !Passed
            ? "Failed"
            : string.Equals(
                ManualVehicleTraversalStatus,
                WorldBaseline06B3Paths.PassedHumanAccepted,
                StringComparison.Ordinal)
                ? WorldBaseline06B3Paths.Frozen
                : WorldBaseline06B3Paths.PendingManualVehicleTraversal;
        public string ManualVehicleTraversalStatus
        {
            get;
            internal set;
        } =
            WorldBaseline06B3Paths.PendingManualVehicleTraversal;
        public IReadOnlyList<WorldBaseline06B3FileHashRecord>
            RequiredOutputFileHashes => requiredOutputFileHashes;
        public bool StructuralValidationExecuted { get; internal set; }
        public bool StructuralValidationPassed { get; internal set; }
        public int SceneCount { get; internal set; }
        public int GlobalSceneCount { get; internal set; }
        public int CellSceneCount { get; internal set; }
        public int EntityCount { get; internal set; }
        public int RendererCount { get; internal set; }
        public int ColliderCount { get; internal set; }
        public int GameplayAnchorCount { get; internal set; }
        public int TraversalRowCount { get; internal set; }
        public int DebtRowCount { get; internal set; }
        public string BaselineRevisionId { get; internal set; } =
            string.Empty;
        public string SourceRevisionId { get; internal set; } =
            string.Empty;
        public string ActiveWorldProfileId { get; internal set; } =
            string.Empty;
        public string OwnershipFingerprintSha256 { get; internal set; } =
            string.Empty;
        public string PresentationFingerprintSha256
        {
            get;
            internal set;
        } = string.Empty;
        public string DebtCatalogueRevision { get; internal set; } =
            string.Empty;
        public string RevisionValidationResultSha256
        {
            get;
            internal set;
        } = string.Empty;
        public string MachineReadableResultSha256 { get; internal set; } =
            string.Empty;

        internal void AddRequiredOutputHash(
            WorldBaseline06B3FileHashRecord record)
        {
            requiredOutputFileHashes.Add(record);
        }
    }

    public static class DonorWorldBaselineFreezeValidator
    {
        private const string MenuRoot =
            "Tools/MSC Remake/World Baseline 06B3/";

        private static readonly Regex DebtIdPattern = new Regex(
            "^LVD-[0-9]{4}$",
            RegexOptions.CultureInvariant);

        private static readonly Regex RouteIdPattern = new Regex(
            "^06B3-TRV-[0-9]{3}$",
            RegexOptions.CultureInvariant);

        private static readonly Regex CanonicalSha256Pattern = new Regex(
            "^[0-9a-f]{64}$",
            RegexOptions.CultureInvariant);

        private static readonly Regex CanonicalCommitPattern = new Regex(
            "^[0-9a-f]{40}$",
            RegexOptions.CultureInvariant);

        [MenuItem(MenuRoot + "Validate And Export Freeze Result")]
        public static void ValidateAndExportFromMenu()
        {
            DonorWorldBaselineFreezeValidationResult result =
                ValidateAndExport();
            Log(result);
        }

        public static void RunBatch()
        {
            DonorWorldBaselineFreezeValidationResult result =
                ValidateAndExport();
            Log(result);
            if (!result.Passed)
            {
                throw new InvalidOperationException(
                    "06B3 freeze validation failed:\n- " +
                    string.Join("\n- ", result.Errors));
            }
        }

        public static DonorWorldBaselineFreezeValidationResult
            ValidateAndExport()
        {
            var result =
                new DonorWorldBaselineFreezeValidationResult();
            RunStrictStructuralValidation(result);
            ValidateOutputContractsInto(result);
            EnsureManualVehicleTraversalWarning(result);
            ExportAndVerifyMachineReadableResult(result);
            return result;
        }

        public static DonorWorldBaselineFreezeValidationResult
            ValidateOutputContracts()
        {
            var result =
                new DonorWorldBaselineFreezeValidationResult();
            ValidateOutputContractsInto(result);
            EnsureManualVehicleTraversalWarning(result);
            return result;
        }

        public static string BuildMachineReadableJson(
            DonorWorldBaselineFreezeValidationResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var document = new FreezeValidationDocument
            {
                schemaVersion = WorldBaseline06B3Paths.SchemaVersion,
                validatorVersion =
                    WorldBaseline06B3Paths.ValidatorVersion,
                baselineRevisionId = result.BaselineRevisionId,
                status = result.Status,
                automatedValidationPassed = result.Passed,
                manualVehicleTraversalStatus =
                    result.ManualVehicleTraversalStatus,
                structuralValidationExecuted =
                    result.StructuralValidationExecuted,
                structuralValidationPassed =
                    result.StructuralValidationPassed,
                sourceRevisionId = result.SourceRevisionId,
                activeWorldProfileId = result.ActiveWorldProfileId,
                sceneCount = result.SceneCount,
                globalSceneCount = result.GlobalSceneCount,
                cellSceneCount = result.CellSceneCount,
                entityCount = result.EntityCount,
                rendererCount = result.RendererCount,
                colliderCount = result.ColliderCount,
                gameplayAnchorCount = result.GameplayAnchorCount,
                traversalRowCount = result.TraversalRowCount,
                debtRowCount = result.DebtRowCount,
                ownershipFingerprintSha256 =
                    result.OwnershipFingerprintSha256,
                presentationFingerprintSha256 =
                    result.PresentationFingerprintSha256,
                debtCatalogueRevision =
                    result.DebtCatalogueRevision,
                revisionValidationResultSha256 =
                    result.RevisionValidationResultSha256,
                requiredOutputFiles = result.RequiredOutputFileHashes
                    .Select(CloneHashRecord)
                    .ToArray(),
                errors = result.Errors.ToArray(),
                warnings = result.Warnings.ToArray()
            };
            return JsonUtility.ToJson(document, prettyPrint: true) +
                   Environment.NewLine;
        }

        public static bool TryValidateMachineReadableJson(
            string json,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Machine-readable result JSON is empty.";
                return false;
            }

            FreezeValidationDocument document;
            try
            {
                document =
                    JsonUtility.FromJson<FreezeValidationDocument>(json);
            }
            catch (Exception exception)
            {
                error = "Machine-readable result JSON could not be parsed: " +
                        exception.Message;
                return false;
            }

            if (document == null)
            {
                error = "Machine-readable result JSON parsed to null.";
                return false;
            }

            if (document.schemaVersion !=
                WorldBaseline06B3Paths.SchemaVersion)
            {
                error = "Machine-readable result schemaVersion is invalid.";
                return false;
            }

            if (!string.Equals(
                    document.validatorVersion,
                    WorldBaseline06B3Paths.ValidatorVersion,
                    StringComparison.Ordinal))
            {
                error = "Machine-readable result validatorVersion is invalid.";
                return false;
            }

            bool pending = string.Equals(
                document.manualVehicleTraversalStatus,
                WorldBaseline06B3Paths.PendingManualVehicleTraversal,
                StringComparison.Ordinal);
            bool accepted = string.Equals(
                document.manualVehicleTraversalStatus,
                WorldBaseline06B3Paths.PassedHumanAccepted,
                StringComparison.Ordinal);
            if (!pending && !accepted)
            {
                error =
                    "Machine-readable result has an invalid manual vehicle " +
                    "traversal status.";
                return false;
            }

            string expectedStatus = accepted
                ? WorldBaseline06B3Paths.Frozen
                : WorldBaseline06B3Paths.PendingManualVehicleTraversal;
            if (document.automatedValidationPassed &&
                !string.Equals(
                    document.status,
                    expectedStatus,
                    StringComparison.Ordinal))
            {
                error =
                    "Machine-readable result mixes inconsistent freeze and " +
                    "manual traversal states.";
                return false;
            }

            WorldBaseline06B3FileHashRecord[] hashes =
                document.requiredOutputFiles ??
                Array.Empty<WorldBaseline06B3FileHashRecord>();
            if (hashes.Any(record =>
                    record == null ||
                    string.IsNullOrWhiteSpace(record.path) ||
                    record.lengthBytes <= 0 ||
                    !IsCanonicalSha256(record.sha256)))
            {
                error =
                    "Machine-readable result contains an invalid output hash " +
                    "record.";
                return false;
            }

            return true;
        }

        public static string ComputeFileSha256(
            string projectRelativePath)
        {
            string absolutePath =
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    projectRelativePath);
            using FileStream stream = File.OpenRead(absolutePath);
            using SHA256 sha256 = SHA256.Create();
            return ToCanonicalHex(sha256.ComputeHash(stream));
        }

        public static string ComputeUtf8Sha256(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            return ToCanonicalHex(
                sha256.ComputeHash(
                    new UTF8Encoding(false).GetBytes(
                        value ?? string.Empty)));
        }

        private static void RunStrictStructuralValidation(
            DonorWorldBaselineFreezeValidationResult result)
        {
            result.StructuralValidationExecuted = true;
            try
            {
                DonorWorldCellizationValidationResult structural =
                    DonorWorldCellizationValidator.Validate(
                        verifySourceHashes: true,
                        inspectAllGeneratedScenes: true);
                result.StructuralValidationPassed = structural.Passed;
                result.SceneCount = structural.SceneCount;
                result.EntityCount = structural.EntityCount;
                result.RendererCount = structural.RendererCount;
                result.ColliderCount = structural.ColliderCount;
                result.GameplayAnchorCount =
                    structural.GameplayAnchorCount;
                result.OwnershipFingerprintSha256 =
                    structural.OwnershipFingerprintSha256;
                foreach (string error in structural.Errors)
                {
                    result.Errors.Add(
                        "Strict donor cellization validation: " + error);
                }

                foreach (string warning in structural.Warnings)
                {
                    result.Warnings.Add(
                        "Strict donor cellization validation: " + warning);
                }
            }
            catch (Exception exception)
            {
                result.StructuralValidationPassed = false;
                result.Errors.Add(
                    "Strict donor cellization validation threw: " +
                    exception.Message);
            }
        }

        private static void ValidateOutputContractsInto(
            DonorWorldBaselineFreezeValidationResult result)
        {
            ValidateRequiredOutputPresenceAndHashes(result);
            ValidateTraversalCsv(result);
            ValidateDebtCsv(result);
            ValidateActiveProfileAndRevision(result);
        }

        private static void ValidateRequiredOutputPresenceAndHashes(
            DonorWorldBaselineFreezeValidationResult result)
        {
            foreach (string path in
                     WorldBaseline06B3Paths.RequiredOutputFiles)
            {
                string absolutePath =
                    WorldBaselinePaths.ToAbsoluteProjectPath(path);
                if (!File.Exists(absolutePath))
                {
                    result.Errors.Add(
                        "Required 06B3 output is missing: " + path);
                    continue;
                }

                var info = new FileInfo(absolutePath);
                if (info.Length <= 0)
                {
                    result.Errors.Add(
                        "Required 06B3 output is empty: " + path);
                    continue;
                }

                string sha256;
                try
                {
                    sha256 = ComputeFileSha256(path);
                }
                catch (Exception exception)
                {
                    result.Errors.Add(
                        "Could not hash required 06B3 output " + path +
                        ": " + exception.Message);
                    continue;
                }

                if (!IsCanonicalSha256(sha256))
                {
                    result.Errors.Add(
                        "Required 06B3 output has a non-canonical SHA-256: " +
                        path);
                    continue;
                }

                result.AddRequiredOutputHash(
                    new WorldBaseline06B3FileHashRecord
                    {
                        path = path,
                        lengthBytes = info.Length,
                        sha256 = sha256
                    });

                if (path.EndsWith(
                        ".md",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ValidateMarkdown(path, result);
                }
            }
        }

        private static void ValidateMarkdown(
            string path,
            DonorWorldBaselineFreezeValidationResult result)
        {
            try
            {
                string text = File.ReadAllText(
                    WorldBaselinePaths.ToAbsoluteProjectPath(path),
                    Encoding.UTF8);
                string trimmed = text.TrimStart();
                if (!trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "06B3 Markdown output has no document heading: " +
                        path);
                }

                if (text.IndexOf('\0') >= 0)
                {
                    result.Errors.Add(
                        "06B3 Markdown output contains a NUL byte: " +
                        path);
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Could not parse 06B3 Markdown output " + path +
                    ": " + exception.Message);
            }
        }

        private static void ValidateTraversalCsv(
            DonorWorldBaselineFreezeValidationResult result)
        {
            CsvTable table = ReadCsv(
                WorldBaseline06B3Paths.TraversalValidation,
                WorldBaseline06B3Paths.TraversalColumns,
                result);
            if (table == null)
            {
                return;
            }

            result.TraversalRowCount = table.Rows.Count;
            if (table.Rows.Count == 0)
            {
                result.Errors.Add(
                    "TRAVERSAL_VALIDATION.csv contains no routes.");
                return;
            }

            var routeIds = new HashSet<string>(StringComparer.Ordinal);
            var routeKinds = new HashSet<string>(StringComparer.Ordinal);
            var physicalVehicleRows = new List<CsvRow>();
            foreach (CsvRow row in table.Rows)
            {
                RequireNonEmptyCells(
                    table,
                    row,
                    WorldBaseline06B3Paths.TraversalColumns,
                    result);
                string routeId = table.Value(row, "RouteId");
                if (!RouteIdPattern.IsMatch(routeId))
                {
                    result.Errors.Add(
                        $"Traversal row {row.LineNumber} has an unstable " +
                        $"RouteId: {routeId}");
                }
                else if (!routeIds.Add(routeId))
                {
                    result.Errors.Add(
                        "Traversal RouteId is duplicated: " + routeId);
                }

                string routeKind = table.Value(row, "RouteKind");
                routeKinds.Add(routeKind);
                if (string.Equals(
                        routeKind,
                        "Vehicle",
                        StringComparison.Ordinal))
                {
                    physicalVehicleRows.Add(row);
                }
            }

            string[] requiredKinds =
            {
                "Player",
                "StreamingFocus",
                "VehicleStreamingSurrogate",
                "Vehicle"
            };
            foreach (string requiredKind in requiredKinds)
            {
                if (!routeKinds.Contains(requiredKind))
                {
                    result.Errors.Add(
                        "Traversal catalogue is missing RouteKind " +
                        requiredKind + ".");
                }
            }

            if (physicalVehicleRows.Count == 0)
            {
                result.Errors.Add(
                    "Traversal catalogue has no dedicated physical vehicle " +
                    "route.");
                return;
            }

            string physicalVehicleStatus = null;
            foreach (CsvRow row in physicalVehicleRows)
            {
                string status = table.Value(row, "Status");
                bool pending = string.Equals(
                    status,
                    WorldBaseline06B3Paths.PendingManualVehicleTraversal,
                    StringComparison.Ordinal);
                bool accepted = string.Equals(
                    status,
                    WorldBaseline06B3Paths.PassedHumanAccepted,
                    StringComparison.Ordinal);
                if (!pending && !accepted)
                {
                    result.Errors.Add(
                        "Physical vehicle traversal has an unsupported " +
                        "manual status. Route=" +
                        table.Value(row, "RouteId") +
                        " Status=" + status);
                    continue;
                }

                if (physicalVehicleStatus == null)
                {
                    physicalVehicleStatus = status;
                }
                else if (!string.Equals(
                             physicalVehicleStatus,
                             status,
                             StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Physical vehicle traversal rows mix pending and " +
                        "accepted manual states.");
                }
            }

            if (physicalVehicleStatus != null)
            {
                result.ManualVehicleTraversalStatus =
                    physicalVehicleStatus;
            }
        }

        private static void ValidateDebtCsv(
            DonorWorldBaselineFreezeValidationResult result)
        {
            CsvTable table = ReadCsv(
                WorldBaseline06B3Paths.LegacyVisualDebt,
                WorldBaseline06B3Paths.DebtColumns,
                result);
            if (table == null)
            {
                return;
            }

            result.DebtRowCount = table.Rows.Count;
            if (table.Rows.Count == 0)
            {
                result.Errors.Add(
                    "LEGACY_VISUAL_DEBT.csv contains no debt records.");
                return;
            }

            var debtIds = new HashSet<string>(StringComparer.Ordinal);
            var classifications = new HashSet<string>(
                WorldBaseline06B3Paths.AllowedDebtClassifications,
                StringComparer.Ordinal);
            var revisions = new HashSet<string>(StringComparer.Ordinal);
            foreach (CsvRow row in table.Rows)
            {
                RequireNonEmptyCells(
                    table,
                    row,
                    WorldBaseline06B3Paths.DebtColumns,
                    result);
                string debtId = table.Value(row, "DebtId");
                if (!DebtIdPattern.IsMatch(debtId))
                {
                    result.Errors.Add(
                        $"Debt row {row.LineNumber} has an unstable DebtId: " +
                        debtId);
                }
                else if (!debtIds.Add(debtId))
                {
                    result.Errors.Add(
                        "Legacy visual debt ID is duplicated: " + debtId);
                }

                string revision = table.Value(row, "Revision");
                revisions.Add(revision);
                string classification =
                    table.Value(row, "Classification");
                if (!classifications.Contains(classification))
                {
                    result.Errors.Add(
                        $"Debt {debtId} has unsupported classification " +
                        classification + ".");
                }

                string blockingText =
                    table.Value(row, "Blocking06B3");
                if (!bool.TryParse(
                        blockingText,
                        out bool blocking))
                {
                    result.Errors.Add(
                        $"Debt {debtId} has invalid Blocking06B3 value: " +
                        blockingText);
                    continue;
                }

                if (blocking &&
                    !string.Equals(
                        classification,
                        "GameplayBlocker",
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        classification,
                        "TraversalRisk",
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        $"Debt {debtId} is marked blocking but its " +
                        $"classification is {classification}.");
                }

                if (string.Equals(
                        classification,
                        "GameplayBlocker",
                        StringComparison.Ordinal) &&
                    !blocking)
                {
                    result.Errors.Add(
                        $"Debt {debtId} is a GameplayBlocker but is not " +
                        "marked as blocking 06B3.");
                }
            }

            if (revisions.Count != 1 ||
                !revisions.Contains(
                    WorldBaseline06B3Paths.DebtCatalogueRevision))
            {
                result.Errors.Add(
                    "Legacy visual debt rows must use the single frozen " +
                    "revision " +
                    WorldBaseline06B3Paths.DebtCatalogueRevision + ".");
            }
            else
            {
                result.DebtCatalogueRevision =
                    WorldBaseline06B3Paths.DebtCatalogueRevision;
            }
        }

        private static void ValidateActiveProfileAndRevision(
            DonorWorldBaselineFreezeValidationResult result)
        {
            ProductionWorldStreamingManifest active =
                AssetDatabase.LoadAssetAtPath<
                    ProductionWorldStreamingManifest>(
                    WorldBaseline06B2Paths.ActiveManifest);
            if (active == null)
            {
                result.Errors.Add(
                    "Active donor world streaming manifest is missing.");
                return;
            }

            result.ActiveWorldProfileId = active.ProfileId;
            result.GlobalSceneCount = active.GlobalScenes.Count;
            result.CellSceneCount = active.Cells.Count;
            RequireEqual(
                result,
                "active world profile ID",
                active.ProfileId,
                WorldBaseline06B2Paths.ProfileId);
            RequireEqual(
                result,
                "active world profile kind",
                active.ProfileKind,
                ProductionWorldProfileKind.DonorFeatureParity);
            RequireEqual(
                result,
                "active profile private-local flag",
                active.PrivateLocalRuntimeBaseline,
                true);
            RequireEqual(
                result,
                "active global scene count",
                active.GlobalScenes.Count,
                1);
            RequireEqual(
                result,
                "active cell scene count",
                active.Cells.Count,
                DonorWorldCellizationPlan.ExpectedCellCount);

            string revisionPath =
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldBaseline06B3Paths.BaselineRevision);
            if (!File.Exists(revisionPath))
            {
                return;
            }

            BaselineRevisionDocument revision;
            try
            {
                revision = JsonUtility.FromJson<BaselineRevisionDocument>(
                    File.ReadAllText(revisionPath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "BASELINE_REVISION.json could not be parsed: " +
                    exception.Message);
                return;
            }

            if (revision == null)
            {
                result.Errors.Add(
                    "BASELINE_REVISION.json parsed to null.");
                return;
            }

            result.BaselineRevisionId = revision.baselineRevisionId;
            result.SourceRevisionId = revision.sourceRevisionId;
            result.RevisionValidationResultSha256 =
                revision.validationResultSha256;

            DonorWorldBaselineSourceManifestData source;
            DonorWorldCellizationPlan cellizationPlan;
            DonorWorldMaterialTexturePlan presentationPlan;
            PresentationManifestDocument presentationManifest;
            try
            {
                source = DonorWorldBaselineManifest.Read();
                cellizationPlan = DonorWorldCellizationPlan.Load();
                presentationPlan =
                    DonorWorldMaterialTexturePlan.Load();
                presentationManifest =
                    JsonUtility.FromJson<PresentationManifestDocument>(
                        File.ReadAllText(
                            WorldBaselinePaths.ToAbsoluteProjectPath(
                                WorldBaseline06B2Paths
                                    .PresentationSourceManifest),
                            Encoding.UTF8));
                if (presentationManifest == null)
                {
                    throw new FormatException(
                        "Presentation manifest parsed to null.");
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Could not load frozen source/cellization/presentation " +
                    "contracts: " + exception.Message);
                return;
            }

            result.OwnershipFingerprintSha256 =
                cellizationPlan.OwnershipFingerprintSha256;
            result.PresentationFingerprintSha256 =
                presentationPlan.PresentationFingerprintSha256;
            ValidateRevisionIdentity(
                revision,
                source,
                presentationManifest,
                cellizationPlan,
                presentationPlan,
                active,
                result);
            ValidateRevisionManifestHashes(revision, result);
            ValidateRevisionResultReference(revision, result);
        }

        private static void ValidateRevisionIdentity(
            BaselineRevisionDocument revision,
            DonorWorldBaselineSourceManifestData source,
            PresentationManifestDocument presentationManifest,
            DonorWorldCellizationPlan cellizationPlan,
            DonorWorldMaterialTexturePlan presentationPlan,
            ProductionWorldStreamingManifest active,
            DonorWorldBaselineFreezeValidationResult result)
        {
            RequireEqual(
                result,
                "baseline revision schema",
                revision.schemaVersion,
                WorldBaseline06B3Paths.SchemaVersion);
            RequireEqual(
                result,
                "baseline revision ID",
                revision.baselineRevisionId,
                WorldBaseline06B3Paths.BaselineRevisionId);
            RequireEqual(
                result,
                "baseline revision classification",
                revision.classification,
                "TemporaryDirectImport");
            RequireEqual(
                result,
                "baseline freeze state",
                revision.freezeState,
                string.Equals(
                    result.ManualVehicleTraversalStatus,
                    WorldBaseline06B3Paths.PassedHumanAccepted,
                    StringComparison.Ordinal)
                    ? WorldBaseline06B3Paths.Frozen
                    : WorldBaseline06B3Paths
                        .PendingManualVehicleTraversal);
            RequireEqual(
                result,
                "source revision ID",
                revision.sourceRevisionId,
                WorldBaselinePaths.SourceRevisionId);
            RequireEqual(
                result,
                "source scene SHA-256",
                revision.sourceSceneSha256,
                WorldBaselinePaths.SourceSceneSha256);
            RequireEqual(
                result,
                "source-files fingerprint",
                revision.sourceFilesFingerprintSha256,
                source.sourceFilesFingerprintSha256);
            RequireEqual(
                result,
                "cellization version",
                revision.cellizationVersion,
                WorldBaseline06B2Paths.GeneratorVersion);
            RequireEqual(
                result,
                "presentation generator version",
                revision.presentationGeneratorVersion,
                presentationManifest.generatorVersion);
            RequireEqual(
                result,
                "active world profile ID in revision",
                revision.activeWorldProfileId,
                active.ProfileId);
            RequireEqual(
                result,
                "ownership fingerprint",
                revision.ownershipFingerprintSha256,
                cellizationPlan.OwnershipFingerprintSha256);
            RequireEqual(
                result,
                "presentation fingerprint",
                revision.presentationFingerprintSha256,
                presentationPlan.PresentationFingerprintSha256);
            RequireEqual(
                result,
                "debt catalogue revision",
                revision.debtCatalogueRevision,
                WorldBaseline06B3Paths.DebtCatalogueRevision);
            RequireEqual(
                result,
                "baseline validation status",
                revision.validationStatus,
                string.Equals(
                    result.ManualVehicleTraversalStatus,
                    WorldBaseline06B3Paths.PassedHumanAccepted,
                    StringComparison.Ordinal)
                    ? WorldBaseline06B3Paths
                        .RevisionAcceptedValidationStatus
                    : WorldBaseline06B3Paths
                        .RevisionPendingValidationStatus);
            ValidateManualVehicleTraversalEvidence(revision, result);

            if (string.IsNullOrWhiteSpace(
                    revision.importerSanitizerVersion))
            {
                result.Errors.Add(
                    "Baseline revision has no importer/sanitizer version.");
            }
            else
            {
                DonorWorldBaselineToolRecord builder =
                    (source.tools ??
                     Array.Empty<DonorWorldBaselineToolRecord>())
                    .FirstOrDefault(tool => string.Equals(
                        tool.tool,
                        "DonorWorldBaselineBuilder",
                        StringComparison.Ordinal));
                if (builder != null &&
                    !revision.importerSanitizerVersion.Contains(
                        builder.version,
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Baseline revision importer/sanitizer version does " +
                        "not contain the frozen builder version.");
                }

                if (!revision.importerSanitizerVersion.Contains(
                        source.sanitationPolicyVersion,
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Baseline revision importer/sanitizer version does " +
                        "not contain the frozen sanitation policy version.");
                }
            }

            RequireApproximately(
                result,
                "world units per metre",
                revision.worldUnitsPerMeter,
                1f,
                0.0001f);
            RequireEqual(
                result,
                "world origin policy",
                revision.worldOriginPolicy,
                source.coordinateSystem.originPolicy);
            if (revision.worldBounds == null)
            {
                result.Errors.Add(
                    "Baseline revision has no worldBounds object.");
            }
            else
            {
                RequireVectorApproximately(
                    result,
                    "world bounds minimum",
                    revision.worldBounds.minimum,
                    source.mapBounds.convertedMin,
                    0.01f);
                RequireVectorApproximately(
                    result,
                    "world bounds maximum",
                    revision.worldBounds.maximum,
                    source.mapBounds.convertedMax,
                    0.01f);
            }

            ValidateRevisionStreamingAndCounts(
                revision,
                active,
                cellizationPlan,
                presentationPlan,
                result);
            ValidateRevisionExceptionsAndHashes(revision, result);

            if (!CanonicalCommitPattern.IsMatch(
                    revision.buildCommitIdentifier ?? string.Empty))
            {
                result.Errors.Add(
                    "Baseline revision buildCommitIdentifier is not a " +
                    "canonical 40-character lowercase Git commit.");
            }
        }

        private static void ValidateRevisionStreamingAndCounts(
            BaselineRevisionDocument revision,
            ProductionWorldStreamingManifest active,
            DonorWorldCellizationPlan cellizationPlan,
            DonorWorldMaterialTexturePlan presentationPlan,
            DonorWorldBaselineFreezeValidationResult result)
        {
            if (revision.streaming == null)
            {
                result.Errors.Add(
                    "Baseline revision has no streaming contract.");
            }
            else
            {
                RequireEqual(
                    result,
                    "revision global scene count",
                    revision.streaming.globalSceneCount,
                    active.GlobalScenes.Count);
                RequireEqual(
                    result,
                    "revision cell scene count",
                    revision.streaming.cellSceneCount,
                    active.Cells.Count);
                RequireApproximately(
                    result,
                    "revision cell size",
                    revision.streaming.cellSizeMeters,
                    active.CellSizeMeters,
                    0.0001f);
                RequireEqual(
                    result,
                    "revision loading radius",
                    revision.streaming.loadingRadiusCells,
                    active.LoadingRadiusCells);
                RequireEqual(
                    result,
                    "revision unloading radius",
                    revision.streaming.unloadingRadiusCells,
                    active.UnloadingRadiusCells);
                RequireApproximately(
                    result,
                    "revision vehicle preload speed",
                    revision.streaming
                        .vehiclePreloadSpeedMetersPerSecond,
                    active.VehiclePreloadSpeedMetersPerSecond,
                    0.0001f);
                RequireEqual(
                    result,
                    "revision vehicle preload radius",
                    revision.streaming.vehiclePreloadRadiusCells,
                    active.VehiclePreloadRadiusCells);
            }

            if (revision.contentCounts == null)
            {
                result.Errors.Add(
                    "Baseline revision has no contentCounts contract.");
                return;
            }

            RequireEqual(
                result,
                "revision eligible entity count",
                revision.contentCounts.eligibleEntities,
                DonorWorldCellizationPlan.ExpectedEntityCount);
            RequireEqual(
                result,
                "revision global entity count",
                revision.contentCounts.globalEntities,
                DonorWorldCellizationPlan.ExpectedGlobalEntityCount);
            RequireEqual(
                result,
                "revision cell-owned entity count",
                revision.contentCounts.cellOwnedEntities,
                DonorWorldCellizationPlan.ExpectedCellEntityCount);
            RequireEqual(
                result,
                "revision renderer count",
                revision.contentCounts.renderers,
                DonorWorldMaterialTexturePlan.ExpectedRendererCount);
            RequireEqual(
                result,
                "revision safe collider count",
                revision.contentCounts.safeColliders,
                DonorWorldCellizationPlan.ExpectedColliderCount);
            RequireEqual(
                result,
                "revision gameplay anchor count",
                revision.contentCounts.gameplayAnchors,
                active.GameplayCatalog != null
                    ? active.GameplayCatalog.Anchors.Count
                    : 0);

            if (cellizationPlan.Assignments.Count !=
                    revision.contentCounts.eligibleEntities ||
                presentationPlan.RendererEntries.Count !=
                    revision.contentCounts.renderers)
            {
                result.Errors.Add(
                    "Revision content counts do not match the loaded " +
                    "deterministic plans.");
            }
        }

        private static void ValidateRevisionExceptionsAndHashes(
            BaselineRevisionDocument revision,
            DonorWorldBaselineFreezeValidationResult result)
        {
            string[] exceptions = revision.knownExceptions ??
                                  Array.Empty<string>();
            if (exceptions.Length == 0)
            {
                result.Errors.Add(
                    "Baseline revision has no knownExceptions.");
            }
            else if (!exceptions.Any(value =>
                         value != null &&
                         value.Contains(
                             "vehicle",
                             StringComparison.OrdinalIgnoreCase) &&
                         (string.Equals(
                              result.ManualVehicleTraversalStatus,
                              WorldBaseline06B3Paths.PassedHumanAccepted,
                              StringComparison.Ordinal)
                             ? value.Contains(
                                 "not a production",
                                 StringComparison.OrdinalIgnoreCase)
                             : value.Contains(
                                 "pending",
                                 StringComparison.OrdinalIgnoreCase))))
            {
                result.Errors.Add(
                    "Baseline revision does not disclose the current vehicle " +
                    "traversal/recovery limitation.");
            }

            ValidateRecordedFileHash(
                result,
                "revision debt catalogue",
                revision.debtCatalogueSha256,
                WorldBaseline06B3Paths.LegacyVisualDebt);
            ValidateRecordedFileHash(
                result,
                "revision traversal catalogue",
                revision.traversalCatalogueSha256,
                WorldBaseline06B3Paths.TraversalValidation);
        }

        private static void ValidateRevisionManifestHashes(
            BaselineRevisionDocument revision,
            DonorWorldBaselineFreezeValidationResult result)
        {
            if (revision.manifestHashes == null)
            {
                result.Errors.Add(
                    "Baseline revision has no manifestHashes object.");
                return;
            }

            ValidateRecordedFileHash(
                result,
                "source manifest",
                revision.manifestHashes.sourceManifest,
                WorldBaselinePaths.SourceManifest);
            ValidateRecordedFileHash(
                result,
                "presentation manifest",
                revision.manifestHashes.presentationManifest,
                WorldBaseline06B2Paths.PresentationSourceManifest);
            ValidateRecordedFileHash(
                result,
                "active streaming manifest",
                revision.manifestHashes.activeStreamingManifest,
                WorldBaseline06B2Paths.ActiveManifest);
            ValidateRecordedFileHash(
                result,
                "gameplay catalogue",
                revision.manifestHashes.gameplayCatalog,
                WorldBaseline06B2Paths.GameplayCatalog);
            ValidateRecordedFileHash(
                result,
                "gameplay anchor manifest",
                revision.manifestHashes.gameplayAnchorManifest,
                WorldBaseline06B2Paths.GameplayAnchorManifest);
            ValidateRecordedFileHash(
                result,
                "safe collider allowlist",
                revision.manifestHashes.safeColliderAllowlist,
                WorldBaseline06B2Paths.SafeColliderAllowlist);
            ValidateRecordedFileHash(
                result,
                "ownership matrix",
                revision.manifestHashes.ownershipMatrix,
                WorldBaseline06B2Paths.OwnershipMatrix);
            ValidateRecordedFileHash(
                result,
                "legacy object/cell manifest",
                revision.manifestHashes.legacyObjectCellManifest,
                WorldBaseline06B2Paths.OwnershipManifest);
            ValidateRecordedFileHash(
                result,
                "legacy material/texture manifest",
                revision.manifestHashes
                    .legacyMaterialTextureManifest,
                WorldBaseline06B2Paths.MaterialTextureManifest);

            RequireEqual(
                result,
                "top-level source manifest SHA-256",
                revision.sourceManifestSha256,
                revision.manifestHashes.sourceManifest);
        }

        private static void ValidateRevisionResultReference(
            BaselineRevisionDocument revision,
            DonorWorldBaselineFreezeValidationResult result)
        {
            string recorded = revision.validationResultSha256 ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recorded) ||
                string.Equals(
                    recorded,
                    WorldBaseline06B3Paths.FirstExportHashPlaceholder,
                    StringComparison.Ordinal))
            {
                result.Warnings.Add(
                    "BASELINE_REVISION.json keeps validationResultSha256 at " +
                    "pending-first-export; the freeze validator exports and " +
                    "hashes the result without mutating the revision record.");
                return;
            }

            if (!IsCanonicalSha256(recorded))
            {
                result.Errors.Add(
                    "Baseline revision validationResultSha256 is neither " +
                    "pending-first-export nor a canonical SHA-256.");
                return;
            }

            string resultPath =
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldBaseline06B3Paths.FullMapValidationResult);
            if (!File.Exists(resultPath))
            {
                result.Errors.Add(
                    "Baseline revision references a validation result hash " +
                    "but FULL_MAP_VALIDATION_RESULT.json is missing.");
                return;
            }

            ValidateRecordedFileHash(
                result,
                "previous machine-readable validation result",
                recorded,
                WorldBaseline06B3Paths.FullMapValidationResult);
        }

        private static void ValidateManualVehicleTraversalEvidence(
            BaselineRevisionDocument revision,
            DonorWorldBaselineFreezeValidationResult result)
        {
            if (!string.Equals(
                    result.ManualVehicleTraversalStatus,
                    WorldBaseline06B3Paths.PassedHumanAccepted,
                    StringComparison.Ordinal))
            {
                return;
            }

            ManualVehicleTraversalEvidenceRecord evidence =
                revision.manualVehicleTraversalEvidence;
            if (evidence == null)
            {
                result.Errors.Add(
                    "Frozen baseline revision has no manual vehicle " +
                    "traversal evidence record.");
                return;
            }

            RequireEqual(
                result,
                "manual vehicle evidence path",
                evidence.path,
                WorldBaseline06B3Paths.ManualVehicleTraversalEvidence);
            if (!IsCanonicalSha256(evidence.sha256))
            {
                result.Errors.Add(
                    "Manual vehicle evidence SHA-256 is invalid.");
            }

            if (evidence.lengthBytes <= 0 ||
                string.IsNullOrWhiteSpace(evidence.capturedUtc) ||
                string.IsNullOrWhiteSpace(evidence.humanAcceptedDate))
            {
                result.Errors.Add(
                    "Manual vehicle evidence metadata is incomplete.");
            }

            if (!evidence.harnessReady ||
                !evidence.boundaryGoalReached ||
                !evidence.userConfirmedRequiredLandmarks ||
                !evidence.userConfirmedReset ||
                evidence.observedLoadedCellCount < 4 ||
                evidence.longestConsecutiveBoundaryCount < 3 ||
                evidence.recoveryCount < 1 ||
                evidence.criticalCollisionFailures != 0 ||
                evidence.visibleDuplicateOrMissingSections != 0)
            {
                result.Errors.Add(
                    "Manual vehicle evidence does not satisfy the accepted " +
                    "06B3 traversal and recovery gate.");
            }

            string absolutePath =
                WorldBaselinePaths.ToAbsoluteProjectPath(evidence.path);
            if (!File.Exists(absolutePath))
            {
                result.Warnings.Add(
                    "Local ignored manual vehicle evidence is unavailable; " +
                    "the frozen revision retains its reviewed hash and " +
                    "acceptance metadata.");
                return;
            }

            var info = new FileInfo(absolutePath);
            if (info.Length != evidence.lengthBytes)
            {
                result.Errors.Add(
                    "Local manual vehicle evidence length differs from the " +
                    "frozen revision.");
            }

            ValidateRecordedFileHash(
                result,
                "manual vehicle traversal evidence",
                evidence.sha256,
                evidence.path);
        }

        private static CsvTable ReadCsv(
            string path,
            IReadOnlyList<string> expectedColumns,
            DonorWorldBaselineFreezeValidationResult result)
        {
            string absolutePath =
                WorldBaselinePaths.ToAbsoluteProjectPath(path);
            if (!File.Exists(absolutePath))
            {
                return null;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(absolutePath, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Could not read CSV " + path + ": " +
                    exception.Message);
                return null;
            }

            if (lines.Length == 0)
            {
                result.Errors.Add("CSV has no header: " + path);
                return null;
            }

            List<string> headers;
            try
            {
                headers = WorldEntityTable.ParseRow(lines[0]);
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Could not parse CSV header " + path + ": " +
                    exception.Message);
                return null;
            }

            if (!headers.SequenceEqual(
                    expectedColumns,
                    StringComparer.Ordinal))
            {
                result.Errors.Add(
                    "CSV schema mismatch for " + path +
                    ". Expected=[" +
                    string.Join(",", expectedColumns) +
                    "] Actual=[" + string.Join(",", headers) + "]");
                return null;
            }

            var rows = new List<CsvRow>();
            for (int index = 1; index < lines.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(lines[index]))
                {
                    continue;
                }

                List<string> values;
                try
                {
                    values = WorldEntityTable.ParseRow(lines[index]);
                }
                catch (Exception exception)
                {
                    result.Errors.Add(
                        $"Could not parse {path} line {index + 1}: " +
                        exception.Message);
                    continue;
                }

                if (values.Count != headers.Count)
                {
                    result.Errors.Add(
                        $"{path} line {index + 1} has {values.Count} " +
                        $"columns; expected {headers.Count}.");
                    continue;
                }

                rows.Add(new CsvRow(index + 1, values));
            }

            return new CsvTable(headers, rows);
        }

        private static void RequireNonEmptyCells(
            CsvTable table,
            CsvRow row,
            IReadOnlyList<string> columns,
            DonorWorldBaselineFreezeValidationResult result)
        {
            foreach (string column in columns)
            {
                if (string.IsNullOrWhiteSpace(table.Value(row, column)))
                {
                    result.Errors.Add(
                        $"CSV row {row.LineNumber} has an empty {column} " +
                        "field.");
                }
            }
        }

        private static void ValidateRecordedFileHash(
            DonorWorldBaselineFreezeValidationResult result,
            string label,
            string recorded,
            string path)
        {
            if (!IsCanonicalSha256(recorded))
            {
                result.Errors.Add(
                    label + " is not a canonical lowercase SHA-256.");
                return;
            }

            string absolutePath =
                WorldBaselinePaths.ToAbsoluteProjectPath(path);
            if (!File.Exists(absolutePath))
            {
                result.Errors.Add(
                    label + " target file is missing: " + path);
                return;
            }

            string actual;
            try
            {
                actual = ComputeFileSha256(path);
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Could not hash " + label + ": " +
                    exception.Message);
                return;
            }

            if (!string.Equals(
                    recorded,
                    actual,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    label + " hash drifted. Recorded=" + recorded +
                    " Actual=" + actual + " Path=" + path);
            }
        }

        private static void ExportAndVerifyMachineReadableResult(
            DonorWorldBaselineFreezeValidationResult result)
        {
            string json = BuildMachineReadableJson(result);
            string path =
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldBaseline06B3Paths.FullMapValidationResult);
            try
            {
                AtomicWriteUtf8(path, json);
                string written = File.ReadAllText(path, Encoding.UTF8);
                if (!TryValidateMachineReadableJson(
                        written,
                        out string parseError))
                {
                    result.Errors.Add(parseError);
                    json = BuildMachineReadableJson(result);
                    AtomicWriteUtf8(path, json);
                }

                result.MachineReadableResultSha256 =
                    ComputeFileSha256(
                        WorldBaseline06B3Paths
                            .FullMapValidationResult);
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Could not atomically export/parse " +
                    "FULL_MAP_VALIDATION_RESULT.json: " +
                    exception.Message);
                return;
            }

            if (!IsCanonicalSha256(
                    result.MachineReadableResultSha256))
            {
                result.Errors.Add(
                    "Exported machine-readable result has an invalid " +
                    "SHA-256.");
            }
        }

        private static void AtomicWriteUtf8(
            string absolutePath,
            string content)
        {
            string directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    "Output path has no parent directory.");
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(
                directory,
                Path.GetFileName(absolutePath) + "." +
                Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(
                    temporaryPath,
                    content,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(absolutePath))
                {
                    File.Replace(
                        temporaryPath,
                        absolutePath,
                        destinationBackupFileName: null);
                }
                else
                {
                    File.Move(temporaryPath, absolutePath);
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

        private static void EnsureManualVehicleTraversalWarning(
            DonorWorldBaselineFreezeValidationResult result)
        {
            if (string.Equals(
                    result.ManualVehicleTraversalStatus,
                    WorldBaseline06B3Paths.PassedHumanAccepted,
                    StringComparison.Ordinal))
            {
                return;
            }

            const string warning =
                "A streaming-focus speed surrogate is not a physical vehicle " +
                "drive. Manual vehicle traversal remains " +
                "PendingManualVehicleTraversal.";
            if (!result.Warnings.Contains(warning))
            {
                result.Warnings.Add(warning);
            }
        }

        private static void Log(
            DonorWorldBaselineFreezeValidationResult result)
        {
            string summary =
                $"06B3 freeze validation: status={result.Status} " +
                $"automatedPass={result.Passed} " +
                $"structuralPass={result.StructuralValidationPassed} " +
                $"scenes={result.SceneCount} cells={result.CellSceneCount} " +
                $"entities={result.EntityCount} " +
                $"traversalRows={result.TraversalRowCount} " +
                $"debtRows={result.DebtRowCount} " +
                $"resultSha256={result.MachineReadableResultSha256}";
            if (result.Passed)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }

            foreach (string warning in result.Warnings)
            {
                Debug.LogWarning("06B3 freeze validation: " + warning);
            }

            foreach (string error in result.Errors)
            {
                Debug.LogError("06B3 freeze validation: " + error);
            }
        }

        private static void RequireEqual<T>(
            DonorWorldBaselineFreezeValidationResult result,
            string label,
            T actual,
            T expected)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
            {
                result.Errors.Add(
                    label + " mismatch. Expected=" + expected +
                    " Actual=" + actual);
            }
        }

        private static void RequireApproximately(
            DonorWorldBaselineFreezeValidationResult result,
            string label,
            float actual,
            float expected,
            float tolerance)
        {
            if (float.IsNaN(actual) ||
                float.IsInfinity(actual) ||
                Mathf.Abs(actual - expected) > tolerance)
            {
                result.Errors.Add(
                    label + " mismatch. Expected=" + expected +
                    " Actual=" + actual);
            }
        }

        private static void RequireVectorApproximately(
            DonorWorldBaselineFreezeValidationResult result,
            string label,
            Vector3 actual,
            Vector3 expected,
            float tolerance)
        {
            if (!IsFinite(actual) ||
                Vector3.Distance(actual, expected) > tolerance)
            {
                result.Errors.Add(
                    label + " mismatch. Expected=" + expected +
                    " Actual=" + actual);
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsCanonicalSha256(string value) =>
            CanonicalSha256Pattern.IsMatch(value ?? string.Empty);

        private static string ToCanonicalHex(byte[] bytes) =>
            BitConverter.ToString(bytes)
                .Replace("-", string.Empty)
                .ToLowerInvariant();

        private static WorldBaseline06B3FileHashRecord CloneHashRecord(
            WorldBaseline06B3FileHashRecord source) =>
            new WorldBaseline06B3FileHashRecord
            {
                path = source.path,
                lengthBytes = source.lengthBytes,
                sha256 = source.sha256
            };

        [Serializable]
        private sealed class FreezeValidationDocument
        {
            public int schemaVersion;
            public string validatorVersion = string.Empty;
            public string baselineRevisionId = string.Empty;
            public string status = string.Empty;
            public bool automatedValidationPassed;
            public string manualVehicleTraversalStatus = string.Empty;
            public bool structuralValidationExecuted;
            public bool structuralValidationPassed;
            public string sourceRevisionId = string.Empty;
            public string activeWorldProfileId = string.Empty;
            public int sceneCount;
            public int globalSceneCount;
            public int cellSceneCount;
            public int entityCount;
            public int rendererCount;
            public int colliderCount;
            public int gameplayAnchorCount;
            public int traversalRowCount;
            public int debtRowCount;
            public string ownershipFingerprintSha256 = string.Empty;
            public string presentationFingerprintSha256 = string.Empty;
            public string debtCatalogueRevision = string.Empty;
            public string revisionValidationResultSha256 = string.Empty;
            public WorldBaseline06B3FileHashRecord[] requiredOutputFiles =
                Array.Empty<WorldBaseline06B3FileHashRecord>();
            public string[] errors = Array.Empty<string>();
            public string[] warnings = Array.Empty<string>();
        }

        [Serializable]
        private sealed class BaselineRevisionDocument
        {
            public int schemaVersion = default;
            public string baselineRevisionId = string.Empty;
            public string classification = string.Empty;
            public string freezeState = string.Empty;
            public string sourceRevisionId = string.Empty;
            public string sourceSceneSha256 = string.Empty;
            public string sourceFilesFingerprintSha256 = string.Empty;
            public string sourceManifestSha256 = string.Empty;
            public string importerSanitizerVersion = string.Empty;
            public string cellizationVersion = string.Empty;
            public string presentationGeneratorVersion = string.Empty;
            public float worldUnitsPerMeter = default;
            public string worldOriginPolicy = string.Empty;
            public RevisionBounds worldBounds = new RevisionBounds();
            public RevisionStreaming streaming = new RevisionStreaming();
            public RevisionContentCounts contentCounts =
                new RevisionContentCounts();
            public string ownershipFingerprintSha256 = string.Empty;
            public string presentationFingerprintSha256 = string.Empty;
            public RevisionManifestHashes manifestHashes =
                new RevisionManifestHashes();
            public string activeWorldProfileId = string.Empty;
            public string[] knownExceptions = Array.Empty<string>();
            public string debtCatalogueRevision = string.Empty;
            public string debtCatalogueSha256 = string.Empty;
            public string traversalCatalogueSha256 = string.Empty;
            public ManualVehicleTraversalEvidenceRecord
                manualVehicleTraversalEvidence =
                    new ManualVehicleTraversalEvidenceRecord();
            public string buildCommitIdentifier = string.Empty;
            public string validationStatus = string.Empty;
            public string validationResultSha256 = string.Empty;
        }

        [Serializable]
        private sealed class ManualVehicleTraversalEvidenceRecord
        {
            public string path = string.Empty;
            public string sha256 = string.Empty;
            public long lengthBytes = default;
            public string capturedUtc = string.Empty;
            public string humanAcceptedDate = string.Empty;
            public bool harnessReady = default;
            public bool boundaryGoalReached = default;
            public int observedLoadedCellCount = default;
            public int transitionCount = default;
            public int longestConsecutiveBoundaryCount = default;
            public float peakStreamingFrameMilliseconds = default;
            public float rawPeakFrameMilliseconds = default;
            public float peakSpeedKph = default;
            public int recoveryCount = default;
            public int criticalCollisionFailures = default;
            public int visibleDuplicateOrMissingSections = default;
            public int userObservedMicrofreezeCount = default;
            public bool userConfirmedRequiredLandmarks = default;
            public bool userConfirmedReset = default;
            public string performanceInterpretation = string.Empty;
        }

        [Serializable]
        private sealed class RevisionBounds
        {
            public Vector3 minimum = default;
            public Vector3 maximum = default;
        }

        [Serializable]
        private sealed class RevisionStreaming
        {
            public int globalSceneCount = default;
            public int cellSceneCount = default;
            public float cellSizeMeters = default;
            public int loadingRadiusCells = default;
            public int unloadingRadiusCells = default;
            public float vehiclePreloadSpeedMetersPerSecond = default;
            public int vehiclePreloadRadiusCells = default;
        }

        [Serializable]
        private sealed class RevisionContentCounts
        {
            public int eligibleEntities = default;
            public int globalEntities = default;
            public int cellOwnedEntities = default;
            public int renderers = default;
            public int safeColliders = default;
            public int gameplayAnchors = default;
        }

        [Serializable]
        private sealed class RevisionManifestHashes
        {
            public string sourceManifest = string.Empty;
            public string presentationManifest = string.Empty;
            public string activeStreamingManifest = string.Empty;
            public string gameplayCatalog = string.Empty;
            public string gameplayAnchorManifest = string.Empty;
            public string safeColliderAllowlist = string.Empty;
            public string ownershipMatrix = string.Empty;
            public string legacyObjectCellManifest = string.Empty;
            public string legacyMaterialTextureManifest = string.Empty;
        }

        [Serializable]
        private sealed class PresentationManifestDocument
        {
            public int schemaVersion = default;
            public string generatorVersion = string.Empty;
            public string presentationFingerprintSha256 = string.Empty;
        }

        private sealed class CsvTable
        {
            private readonly Dictionary<string, int> columnIndexes;

            public CsvTable(
                IReadOnlyList<string> headers,
                List<CsvRow> rows)
            {
                Headers = headers;
                Rows = rows;
                columnIndexes = headers
                    .Select((header, index) => new
                    {
                        header,
                        index
                    })
                    .ToDictionary(
                        item => item.header,
                        item => item.index,
                        StringComparer.Ordinal);
            }

            public IReadOnlyList<string> Headers { get; }
            public List<CsvRow> Rows { get; }

            public string Value(CsvRow row, string column) =>
                row.Values[columnIndexes[column]];
        }

        private sealed class CsvRow
        {
            public CsvRow(int lineNumber, List<string> values)
            {
                LineNumber = lineNumber;
                Values = values;
            }

            public int LineNumber { get; }
            public List<string> Values { get; }
        }
    }
}
