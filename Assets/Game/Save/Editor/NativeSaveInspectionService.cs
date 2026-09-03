using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MSC.Save.EditorTools
{
    public enum SaveCandidateKind
    {
        Current = 0,
        InterruptedWrite = 1,
        Backup = 2,
        Quarantined = 3,
    }

    public enum NativeSaveSlotState
    {
        Empty = 0,
        Valid = 1,
        Recoverable = 2,
        Corrupt = 3,
        InvalidSlotId = 4,
    }

    public enum StableEntityReferenceKind
    {
        Referenced = 0,
        Deferred = 1,
        Unresolved = 2,
    }

    public sealed class SaveCandidateInspection
    {
        public SaveCandidateInspection(
            SaveCandidateKind kind,
            string path,
            bool isValid,
            SaveDocument document,
            string failure)
        {
            Kind = kind;
            Path = path ?? string.Empty;
            IsValid = isValid;
            Document = document?.DeepClone();
            Failure = failure ?? string.Empty;
        }

        public SaveCandidateKind Kind { get; }
        public string Path { get; }
        public bool IsValid { get; }
        public SaveDocument Document { get; }
        public string Failure { get; }
    }

    public sealed class StableEntityReferenceInspection
    {
        public StableEntityReferenceInspection(
            string domainId,
            string fieldPath,
            string stableEntityId,
            StableEntityReferenceKind kind)
        {
            DomainId = domainId ?? string.Empty;
            FieldPath = fieldPath ?? string.Empty;
            StableEntityId = stableEntityId ?? string.Empty;
            Kind = kind;
        }

        public string DomainId { get; }
        public string FieldPath { get; }
        public string StableEntityId { get; }
        public StableEntityReferenceKind Kind { get; }
    }

    public sealed class DomainPayloadInspectionIssue
    {
        public DomainPayloadInspectionIssue(string domainId, string message)
        {
            DomainId = domainId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string DomainId { get; }
        public string Message { get; }
    }

    public sealed class NativeSaveSlotInspection
    {
        public NativeSaveSlotInspection(
            string slotId,
            string slotDirectory,
            NativeSaveSlotState state,
            IReadOnlyList<SaveCandidateInspection> candidates,
            SaveDocument preferredDocument,
            SaveCandidateKind? preferredCandidate,
            IReadOnlyList<StableEntityReferenceInspection> stableEntityReferences,
            IReadOnlyList<DomainPayloadInspectionIssue> payloadIssues,
            string message)
        {
            SlotId = slotId ?? string.Empty;
            SlotDirectory = slotDirectory ?? string.Empty;
            State = state;
            Candidates = candidates ?? Array.Empty<SaveCandidateInspection>();
            PreferredDocument = preferredDocument?.DeepClone();
            PreferredCandidate = preferredCandidate;
            StableEntityReferences = stableEntityReferences ?? Array.Empty<StableEntityReferenceInspection>();
            PayloadIssues = payloadIssues ?? Array.Empty<DomainPayloadInspectionIssue>();
            Message = message ?? string.Empty;
        }

        public string SlotId { get; }
        public string SlotDirectory { get; }
        public NativeSaveSlotState State { get; }
        public IReadOnlyList<SaveCandidateInspection> Candidates { get; }
        public SaveDocument PreferredDocument { get; }
        public SaveCandidateKind? PreferredCandidate { get; }
        public IReadOnlyList<StableEntityReferenceInspection> StableEntityReferences { get; }
        public IReadOnlyList<DomainPayloadInspectionIssue> PayloadIssues { get; }
        public string Message { get; }
        public bool CanRecover => State == NativeSaveSlotState.Recoverable;
    }

    public sealed class NativeSaveRootValidation
    {
        public NativeSaveRootValidation(IReadOnlyList<NativeSaveSlotInspection> slots, IReadOnlyList<string> issues)
        {
            Slots = slots ?? Array.Empty<NativeSaveSlotInspection>();
            Issues = issues ?? Array.Empty<string>();
        }

        public IReadOnlyList<NativeSaveSlotInspection> Slots { get; }
        public IReadOnlyList<string> Issues { get; }
        public bool IsValid => Issues.Count == 0;
    }

    /// <summary>
    /// Read-only by default. Recovery is exposed as a separate explicit operation because it can
    /// quarantine invalid files and promote a validated temporary or backup candidate.
    /// </summary>
    public sealed class NativeSaveInspectionService
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly SaveDocumentCodec codec;
        private readonly FileSystemSaveStorage storage;

        public NativeSaveInspectionService(string rootDirectory, SaveDocumentCodec codec = null)
        {
            this.codec = codec ?? new SaveDocumentCodec();
            storage = new FileSystemSaveStorage(rootDirectory, this.codec);
        }

        public string RootDirectory => storage.RootDirectory;

        public IReadOnlyList<SaveSlotSummary> EnumerateSlots()
        {
            return storage.EnumerateSlots();
        }

        public NativeSaveSlotInspection InspectSlot(string slotId)
        {
            try
            {
                SaveSlotId.Validate(slotId);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidDataException)
            {
                return new NativeSaveSlotInspection(
                    slotId,
                    string.Empty,
                    NativeSaveSlotState.InvalidSlotId,
                    Array.Empty<SaveCandidateInspection>(),
                    null,
                    null,
                    Array.Empty<StableEntityReferenceInspection>(),
                    Array.Empty<DomainPayloadInspectionIssue>(),
                    exception.Message);
            }

            string slotDirectory = SaveSlotId.CombineUnderRoot(RootDirectory, slotId);
            List<SaveCandidateInspection> candidates = new List<SaveCandidateInspection>();
            AddCandidate(candidates, SaveCandidateKind.Current, Path.Combine(slotDirectory, FileSystemSaveStorage.CurrentFileName));
            AddCandidate(candidates, SaveCandidateKind.InterruptedWrite, Path.Combine(slotDirectory, FileSystemSaveStorage.TemporaryFileName));
            AddCandidate(candidates, SaveCandidateKind.Backup, Path.Combine(slotDirectory, FileSystemSaveStorage.BackupFileName));
            AddQuarantinedCandidates(candidates, slotDirectory);

            SaveCandidateInspection current = candidates.FirstOrDefault(candidate => candidate.Kind == SaveCandidateKind.Current);
            SaveCandidateInspection temporary = candidates.FirstOrDefault(candidate => candidate.Kind == SaveCandidateKind.InterruptedWrite);
            SaveCandidateInspection backup = candidates.FirstOrDefault(candidate => candidate.Kind == SaveCandidateKind.Backup);
            SaveCandidateInspection preferred = current?.IsValid == true
                ? current
                : temporary?.IsValid == true
                    ? temporary
                    : backup?.IsValid == true
                        ? backup
                        : null;

            NativeSaveSlotState state;
            string message;
            if (current?.IsValid == true)
            {
                state = NativeSaveSlotState.Valid;
                message = "Current save document is valid.";
            }
            else if (temporary?.IsValid == true || backup?.IsValid == true)
            {
                state = NativeSaveSlotState.Recoverable;
                message = temporary?.IsValid == true
                    ? "Current document is unavailable; a validated interrupted write can be recovered."
                    : "Current document is unavailable; a validated backup can be recovered.";
            }
            else if (candidates.Any(candidate => candidate.Kind != SaveCandidateKind.Quarantined))
            {
                state = NativeSaveSlotState.Corrupt;
                message = "No valid current, interrupted-write, or backup candidate exists.";
            }
            else
            {
                state = NativeSaveSlotState.Empty;
                message = "Save slot contains no active candidates.";
            }

            IReadOnlyList<StableEntityReferenceInspection> stableReferences = Array.Empty<StableEntityReferenceInspection>();
            IReadOnlyList<DomainPayloadInspectionIssue> payloadIssues = Array.Empty<DomainPayloadInspectionIssue>();
            if (preferred?.Document != null)
            {
                ExtractStableEntityReferences(preferred.Document, out stableReferences, out payloadIssues);
            }

            return new NativeSaveSlotInspection(
                slotId,
                slotDirectory,
                state,
                candidates,
                preferred?.Document,
                preferred?.Kind,
                stableReferences,
                payloadIssues,
                message);
        }

        public NativeSaveRootValidation ValidateRoot()
        {
            if (!Directory.Exists(RootDirectory))
            {
                return new NativeSaveRootValidation(Array.Empty<NativeSaveSlotInspection>(), Array.Empty<string>());
            }

            string[] directories = Directory.EnumerateDirectories(RootDirectory)
                .Take(SaveLimits.MaximumSlotCount + 1)
                .ToArray();
            List<string> issues = new List<string>();
            if (directories.Length > SaveLimits.MaximumSlotCount)
            {
                issues.Add($"Save root exceeds {SaveLimits.MaximumSlotCount} slot directories.");
                directories = directories.Take(SaveLimits.MaximumSlotCount).ToArray();
            }

            List<NativeSaveSlotInspection> slots = new List<NativeSaveSlotInspection>();
            foreach (string directory in directories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                NativeSaveSlotInspection inspection = InspectSlot(Path.GetFileName(directory));
                slots.Add(inspection);
                if (inspection.State == NativeSaveSlotState.Corrupt ||
                    inspection.State == NativeSaveSlotState.InvalidSlotId)
                {
                    issues.Add($"{inspection.SlotId}: {inspection.Message}");
                }

                foreach (DomainPayloadInspectionIssue issue in inspection.PayloadIssues)
                {
                    issues.Add($"{inspection.SlotId}/{issue.DomainId}: {issue.Message}");
                }
            }

            return new NativeSaveRootValidation(slots, issues);
        }

        public SaveReadResult RecoverSlot(string slotId)
        {
            SaveSlotId.Validate(slotId);
            return storage.Read(slotId, true);
        }

        public void ExtractStableEntityReferences(
            SaveDocument document,
            out IReadOnlyList<StableEntityReferenceInspection> references,
            out IReadOnlyList<DomainPayloadInspectionIssue> issues)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            List<StableEntityReferenceInspection> result = new List<StableEntityReferenceInspection>();
            List<DomainPayloadInspectionIssue> failures = new List<DomainPayloadInspectionIssue>();
            foreach (SaveDomainEnvelope domain in document.Domains ?? Array.Empty<SaveDomainEnvelope>())
            {
                if (domain == null)
                {
                    continue;
                }

                try
                {
                    StableIdJsonScanner.Scan(domain.DomainId, domain.PayloadJson ?? string.Empty, result);
                }
                catch (FormatException exception)
                {
                    failures.Add(new DomainPayloadInspectionIssue(domain.DomainId, exception.Message));
                }
            }

            references = result
                .OrderBy(item => item.Kind)
                .ThenBy(item => item.DomainId, StringComparer.Ordinal)
                .ThenBy(item => item.StableEntityId, StringComparer.Ordinal)
                .ThenBy(item => item.FieldPath, StringComparer.Ordinal)
                .ToArray();
            issues = failures.ToArray();
        }

        private void AddCandidate(ICollection<SaveCandidateInspection> candidates, SaveCandidateKind kind, string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            candidates.Add(InspectCandidate(kind, path));
        }

        private void AddQuarantinedCandidates(ICollection<SaveCandidateInspection> candidates, string slotDirectory)
        {
            string corruptDirectory = Path.Combine(slotDirectory, "corrupt");
            if (!Directory.Exists(corruptDirectory))
            {
                return;
            }

            foreach (string path in Directory.EnumerateFiles(corruptDirectory)
                         .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                         .Take(SaveLimits.MaximumSlotCount))
            {
                candidates.Add(InspectCandidate(SaveCandidateKind.Quarantined, path));
            }
        }

        private SaveCandidateInspection InspectCandidate(SaveCandidateKind kind, string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length > SaveLimits.MaximumDocumentBytes)
                {
                    throw new InvalidDataException($"File exceeds {SaveLimits.MaximumDocumentBytes} bytes.");
                }

                string json = File.ReadAllText(path, StrictUtf8);
                SaveDocument document = codec.Deserialize(json);
                return new SaveCandidateInspection(kind, path, true, document, string.Empty);
            }
            catch (Exception exception) when (IsInspectionFailure(exception))
            {
                return new SaveCandidateInspection(kind, path, false, null, exception.Message);
            }
        }

        private static bool IsInspectionFailure(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is FormatException ||
                   exception is InvalidDataException ||
                   exception is NotSupportedException ||
                   exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is OverflowException;
        }

        private sealed class StableIdJsonScanner
        {
            private readonly string domainId;
            private readonly string json;
            private readonly ICollection<StableEntityReferenceInspection> output;
            private int position;

            private StableIdJsonScanner(
                string domainId,
                string json,
                ICollection<StableEntityReferenceInspection> output)
            {
                this.domainId = domainId;
                this.json = json;
                this.output = output;
            }

            public static void Scan(
                string domainId,
                string json,
                ICollection<StableEntityReferenceInspection> output)
            {
                StableIdJsonScanner scanner = new StableIdJsonScanner(domainId, json, output);
                scanner.SkipWhitespace();
                scanner.ParseValue("$", string.Empty, 0);
                scanner.SkipWhitespace();
                if (scanner.position != json.Length)
                {
                    throw scanner.Failure("Unexpected content after the JSON value.");
                }
            }

            private void ParseValue(string path, string propertyName, int depth)
            {
                if (depth > 128)
                {
                    throw Failure("JSON nesting exceeds 128 levels.");
                }

                SkipWhitespace();
                if (position >= json.Length)
                {
                    throw Failure("Unexpected end of JSON.");
                }

                switch (json[position])
                {
                    case '{':
                        ParseObject(path, depth + 1);
                        return;
                    case '[':
                        ParseArray(path, propertyName, depth + 1);
                        return;
                    case '"':
                        string value = ParseString();
                        if (IsStableIdProperty(propertyName) && !string.IsNullOrWhiteSpace(value))
                        {
                            if (output.Count >= SaveLimits.MaximumUnresolvedEntries)
                            {
                                throw Failure($"Stable-ID reference count exceeds {SaveLimits.MaximumUnresolvedEntries} entries.");
                            }

                            output.Add(new StableEntityReferenceInspection(
                                domainId,
                                path,
                                value,
                                Classify(path)));
                        }

                        return;
                    default:
                        ParseLiteral();
                        return;
                }
            }

            private void ParseObject(string path, int depth)
            {
                Expect('{');
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (position >= json.Length || json[position] != '"')
                    {
                        throw Failure("Expected a JSON object property name.");
                    }

                    string property = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    string childPath = path == "$" ? property : path + "." + property;
                    ParseValue(childPath, property, depth);
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return;
                    }

                    Expect(',');
                }
            }

            private void ParseArray(string path, string propertyName, int depth)
            {
                Expect('[');
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return;
                }

                int index = 0;
                while (true)
                {
                    ParseValue($"{path}[{index}]", propertyName, depth);
                    index++;
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return;
                    }

                    Expect(',');
                }
            }

            private string ParseString()
            {
                Expect('"');
                StringBuilder builder = new StringBuilder();
                while (position < json.Length)
                {
                    char character = json[position++];
                    if (character == '"')
                    {
                        return builder.ToString();
                    }

                    if (character < 0x20)
                    {
                        throw Failure("JSON string contains a control character.");
                    }

                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    if (position >= json.Length)
                    {
                        throw Failure("JSON string ends after an escape character.");
                    }

                    char escape = json[position++];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u': builder.Append(ParseUnicodeEscape()); break;
                        default: throw Failure($"Unsupported JSON escape '\\{escape}'.");
                    }
                }

                throw Failure("Unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (position + 4 > json.Length)
                {
                    throw Failure("Incomplete JSON unicode escape.");
                }

                int value = 0;
                for (int index = 0; index < 4; index++)
                {
                    char character = json[position++];
                    int digit = character >= '0' && character <= '9' ? character - '0' :
                        character >= 'a' && character <= 'f' ? character - 'a' + 10 :
                        character >= 'A' && character <= 'F' ? character - 'A' + 10 : -1;
                    if (digit < 0)
                    {
                        throw Failure("Invalid JSON unicode escape.");
                    }

                    value = value * 16 + digit;
                }

                return (char)value;
            }

            private void ParseLiteral()
            {
                int start = position;
                while (position < json.Length)
                {
                    char character = json[position];
                    if (character == ',' || character == '}' || character == ']' || char.IsWhiteSpace(character))
                    {
                        break;
                    }

                    position++;
                }

                string token = json.Substring(start, position - start);
                if (token == "true" || token == "false" || token == "null")
                {
                    return;
                }

                if (!double.TryParse(
                        token,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out _))
                {
                    throw Failure($"Invalid JSON literal '{token}'.");
                }
            }

            private void SkipWhitespace()
            {
                while (position < json.Length && char.IsWhiteSpace(json[position]))
                {
                    position++;
                }
            }

            private bool TryConsume(char expected)
            {
                if (position < json.Length && json[position] == expected)
                {
                    position++;
                    return true;
                }

                return false;
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (!TryConsume(expected))
                {
                    throw Failure($"Expected '{expected}'.");
                }
            }

            private FormatException Failure(string message)
            {
                return new FormatException($"{message} Offset: {position}.");
            }

            private static bool IsStableIdProperty(string propertyName)
            {
                return propertyName.Equals("StableEntityId", StringComparison.OrdinalIgnoreCase) ||
                       propertyName.Equals("StableId", StringComparison.OrdinalIgnoreCase) ||
                       propertyName.EndsWith("StableEntityId", StringComparison.OrdinalIgnoreCase);
            }

            private static StableEntityReferenceKind Classify(string path)
            {
                if (path.IndexOf("unresolved", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return StableEntityReferenceKind.Unresolved;
                }

                return path.IndexOf("deferred", StringComparison.OrdinalIgnoreCase) >= 0
                    ? StableEntityReferenceKind.Deferred
                    : StableEntityReferenceKind.Referenced;
            }
        }
    }
}
