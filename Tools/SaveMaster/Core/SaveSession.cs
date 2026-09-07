using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MySummerRemake.SaveMaster.Core;

public sealed partial class SaveSession
{
    public const string SupportedDocumentVersions = "16, 17, 18";
    private NativeDocument document;
    private string expectedFileHash;
    private readonly List<EditCommand> undo = [];
    private readonly Stack<EditCommand> redo = new();
    private readonly Dictionary<string, bool> supportedPayloadVersions = new(StringComparer.Ordinal);
    private const int MaximumUndoBytes = 32 * 1024 * 1024;

    private SaveSession(string path, byte[] bytes)
    {
        FilePath = Path.GetFullPath(path);
        document = NativeSaveCodec.Read(bytes);
        expectedFileHash = NativeSaveCodec.Hash(bytes);
    }

    public static SaveSession Open(string path) => new(path, SafeSaveStorage.Read(Path.GetFullPath(path)));
    public string FilePath { get; private set; }
    public int DocumentVersion => document.Version;
    public string DisplayName => JsonNode.Parse(document.Metadata["DisplayName"])!.GetValue<string>();
    public string SlotId => JsonNode.Parse(document.Header["SlotId"])!.GetValue<string>();
    public bool CanEdit => DocumentVersion is 16 or 17 or 18;
    public bool IsDirty => document.Domains.Any(domain => !JsonNode.DeepEquals(domain.Payload, domain.OriginalPayload));
    public bool CanUndo => undo.Count != 0;
    public bool CanRedo => redo.Count != 0;
    public IReadOnlyList<SaveDomain> Domains => document.Domains.Select(domain => domain.Snapshot()).ToArray();
    public Func<IReadOnlyList<SaveDomain>, IReadOnlyList<ValidationIssue>>? Validator { get; set; }
    public bool CanEditDomain(string domainId)
    {
        DomainState domain = GetDomain(domainId);
        if (!CanEdit || !SchemaCatalog.IsSupported(domain.Id, domain.SchemaVersion)) return false;
        if (!supportedPayloadVersions.TryGetValue(domainId, out bool supported))
        {
            // Version fields are immutable, so their compatibility can be cached
            // for the session. Other invalid values remain repairable in memory.
            supported = !SaveSchemaValidator.ValidatePayload(domain.Id, domain.OriginalPayload)
                .Any(issue => issue.Severity == ValidationSeverity.Error && issue.JsonPointer.EndsWith("/schemaVersion", StringComparison.Ordinal));
            supportedPayloadVersions.Add(domainId, supported);
        }
        return supported;
    }
    public IReadOnlyList<SaveChange> Changes
    {
        get
        {
            var changes = new List<SaveChange>();
            foreach (DomainState domain in document.Domains) DescribeChanges(domain.Id, "", domain.OriginalPayload, domain.Payload, changes);
            return changes;
        }
    }

    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();
        if (!CanEdit) issues.Add(new(ValidationSeverity.Error, "", "", $"Document version {DocumentVersion} is read-only; supported editing versions are {SupportedDocumentVersions}."));
        foreach (DomainState domain in document.Domains)
        {
            if (NativeSaveCodec.Utf8.GetByteCount(domain.Payload.ToJsonString(NativeSaveCodec.PayloadOptions)) > SaveMasterLimits.MaximumDomainPayloadBytes)
                issues.Add(new(ValidationSeverity.Error, domain.Id, "", "Payload exceeds the 2 MiB limit."));
            if (domain.Id == "core.time" && !JsonNode.DeepEquals(domain.Payload, domain.OriginalPayload))
                issues.AddRange(SaveSchemaValidator.ValidateTimeTransition(domain.OriginalPayload, domain.Payload));
        }
        IReadOnlyList<SaveDomain> snapshots = Domains;
        issues.AddRange(SaveSchemaValidator.Validate(snapshots));
        if (Validator is not null) issues.AddRange(Validator(snapshots));
        return issues.Distinct().ToArray();
    }

    public void SetValue(string domainId, string jsonPointer, string text) => ApplyEdits([new(domainId, jsonPointer, text)]);

    /// <summary>Apply typed scalar edits as one atomic undo step.</summary>
    public void ApplyEdits(IEnumerable<SaveEdit> edits)
    {
        EnsureEditable();
        var replacements = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (SaveEdit edit in edits)
        {
            DomainState domain = GetEditableDomain(edit.DomainId);
            if (!replacements.TryGetValue(domain.Id, out JsonNode? clone)) replacements[domain.Id] = clone = domain.Payload.DeepClone();
            JsonNode? original = Resolve(clone, edit.JsonPointer);
            JsonNode replacement = ParseScalar(original, edit.Text);
            SetAt(clone, edit.JsonPointer, replacement);
        }
        CommitEdit(replacements);
    }

    /// <summary>Replace an existing subtree; domain structure and persistent IDs remain protected.</summary>
    public void SetJson(string domainId, string jsonPointer, string json)
    {
        EnsureEditable();
        DomainState domain = GetEditableDomain(domainId);
        using var strict = NativeSaveCodec.ParseStrict(json);
        JsonNode replacement = JsonNode.Parse(json) ?? throw new InvalidDataException("Null replacements are not supported.");
        JsonNode clone = domain.Payload.DeepClone();
        if (jsonPointer.Length == 0) clone = replacement;
        else SetAt(clone, jsonPointer, replacement);
        CommitEdit(new Dictionary<string, JsonNode>(StringComparer.Ordinal) { [domainId] = clone });
    }

    /// <summary>Change balance by appending a project-format adjustment transaction.</summary>
    public void SetBalance(long balanceMinorUnits)
    {
        EnsureEditable();
        DomainState domain = GetEditableDomain("economy.player");
        JsonNode updated = SchemaActions.SetBalance(domain.Payload.DeepClone(), balanceMinorUnits);
        ValidationIssue[] errors = SaveSchemaValidator.ValidatePayload(domain.Id, updated)
            .Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidDataException("Balance adjustment failed validation: " + string.Join("; ", errors.Select(error => error.Message)));
        // This private path accepts ONLY the clone produced by the audited action.
        // The public JSON editor cannot append arbitrary entities or transactions.
        CommitEdit(new Dictionary<string, JsonNode>(StringComparer.Ordinal) { [domain.Id] = updated }, true);
    }

    public void SetTime(DateTime localGameDateTime)
    {
        EnsureEditable();
        DomainState domain = GetEditableDomain("core.time");
        JsonNode updated = SchemaActions.SetTime(domain.Payload.DeepClone(), localGameDateTime);
        CommitEdit(new Dictionary<string, JsonNode>(StringComparer.Ordinal) { [domain.Id] = updated });
    }

    public void Undo()
    {
        EnsureEditable();
        if (!CanUndo) return;
        EditCommand command = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        foreach (var entry in command.Before) GetDomain(entry.Key).Payload = entry.Value.DeepClone();
        redo.Push(command);
    }

    public void Redo()
    {
        EnsureEditable();
        if (!CanRedo) return;
        EditCommand command = redo.Pop();
        foreach (var entry in command.After) GetDomain(entry.Key).Payload = entry.Value.DeepClone();
        undo.Add(command);
    }

    public SaveWriteResult Save()
    {
        EnsureCanWrite();
        if (!IsDirty) throw new InvalidOperationException("There are no changes to save.");
        return Write(FilePath, expectedFileHash);
    }

    public SaveWriteResult SaveAs(string path)
    {
        EnsureCanWrite();
        string fullPath = Path.GetFullPath(path);
        if (string.Equals(fullPath, FilePath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new SaveConflictException("Save As requires a new path. Use Save for the opened file.");
        return Write(fullPath, null);
    }

    private SaveWriteResult Write(string path, string? expected)
    {
        byte[] bytes = NativeSaveCodec.Write(document);
        SaveWriteResult result = SafeSaveStorage.Write(path, bytes, expected);
        document = NativeSaveCodec.Read(bytes);
        FilePath = result.Path;
        expectedFileHash = result.Sha256;
        undo.Clear(); redo.Clear();
        return result;
    }

    private void EnsureCanWrite()
    {
        EnsureEditable();
        ValidationIssue[] errors = Validate().Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidDataException("Save validation failed: " + string.Join("; ", errors.Take(12).Select(error => error.DomainId + error.JsonPointer + ": " + error.Message)));
    }

    private void CommitEdit(Dictionary<string, JsonNode> replacements, bool trustedShapeChange = false)
    {
        var before = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        var after = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var entry in replacements)
        {
            DomainState domain = GetEditableDomain(entry.Key);
            if (!trustedShapeChange) EnsureCompatibleStructure(domain.Id, "", domain.Payload, entry.Value, false);
            if (NativeSaveCodec.Utf8.GetByteCount(entry.Value.ToJsonString(NativeSaveCodec.PayloadOptions)) > SaveMasterLimits.MaximumDomainPayloadBytes)
                throw new InvalidDataException("Edited payload exceeds the 2 MiB domain limit.");
            if (JsonNode.DeepEquals(domain.Payload, entry.Value)) continue;
            before.Add(entry.Key, domain.Payload.DeepClone());
            after.Add(entry.Key, entry.Value.DeepClone());
        }
        if (before.Count == 0) return;
        foreach (var entry in after) GetDomain(entry.Key).Payload = entry.Value.DeepClone();
        var command = new EditCommand(before, after);
        undo.Add(command);
        redo.Clear();
        while (undo.Count > 1 && (undo.Count > 100 || undo.Sum(item => item.ByteCount) > MaximumUndoBytes)) undo.RemoveAt(0);
    }

    private void EnsureEditable()
    {
        if (!CanEdit) throw new NotSupportedException($"Version {DocumentVersion} is read-only; supported versions: {SupportedDocumentVersions}. No automatic migration is performed.");
    }

    private DomainState GetDomain(string id) => document.Domains.FirstOrDefault(domain => domain.Id == id) ?? throw new KeyNotFoundException("Unknown domain: " + id);
    private DomainState GetEditableDomain(string id)
    {
        DomainState domain = GetDomain(id);
        if (!CanEditDomain(id)) throw new NotSupportedException($"Unknown domain or payload schema in {id} v{domain.SchemaVersion} is read-only.");
        return domain;
    }

    private static JsonNode ParseScalar(JsonNode? original, string text)
    {
        if (original is not JsonValue value) throw new InvalidDataException("Select an existing scalar field; structured values require the JSON editor.");
        JsonValueKind kind = value.GetValueKind();
        if (kind == JsonValueKind.String) return JsonValue.Create(text)!;
        if (kind is JsonValueKind.True or JsonValueKind.False)
        {
            if (!bool.TryParse(text, out bool boolean)) throw new InvalidDataException("Boolean value must be true or false.");
            return JsonValue.Create(boolean)!;
        }
        if (kind != JsonValueKind.Number) throw new InvalidDataException("This scalar type cannot be edited.");
        using var strict = NativeSaveCodec.ParseStrict(text);
        if (strict.RootElement.ValueKind != JsonValueKind.Number) throw new InvalidDataException("Enter a finite JSON number, using a decimal point.");
        // A JSON token such as 0 does not identify its C# numeric type. Typed
        // schema validation, rather than token spelling, enforces integer fields.
        return JsonNode.Parse(text)!;
    }

    internal static JsonNode? Resolve(JsonNode root, string pointer)
    {
        if (pointer.Length == 0) return root;
        JsonNode? current = root;
        foreach (string token in PointerTokens(pointer))
        {
            if (current is JsonObject obj && obj.TryGetPropertyValue(token, out JsonNode? child)) current = child;
            else if (current is JsonArray array && TryIndex(token, array.Count, out int index)) current = array[index];
            else throw new InvalidDataException("JSON pointer does not identify an existing field: " + pointer);
        }
        return current;
    }

    private static void SetAt(JsonNode root, string pointer, JsonNode replacement)
    {
        string[] tokens = PointerTokens(pointer);
        if (tokens.Length == 0) throw new InvalidDataException("Use SetJson to replace a domain root.");
        string parentPointer = pointer[..pointer.LastIndexOf('/')];
        JsonNode? parent = Resolve(root, parentPointer);
        string leaf = tokens[^1];
        if (parent is JsonObject obj && obj.ContainsKey(leaf)) obj[leaf] = replacement;
        else if (parent is JsonArray array && TryIndex(leaf, array.Count, out int index)) array[index] = replacement;
        else throw new InvalidDataException("Cannot add or remove fields through a JSON pointer.");
    }

    private static string[] PointerTokens(string pointer)
    {
        if (pointer.Length == 0) return [];
        if (!pointer.StartsWith('/')) throw new InvalidDataException("A JSON pointer must begin with '/'.");
        string[] tokens = pointer[1..].Split('/');
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];
            for (int offset = 0; offset < token.Length; offset++)
                if (token[offset] == '~' && (++offset == token.Length || token[offset] is not ('0' or '1')))
                    throw new InvalidDataException("Invalid JSON pointer escape.");
            tokens[index] = token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
        }
        return tokens;
    }

    private static bool TryIndex(string text, int count, out int index) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0 && index < count && index.ToString(CultureInfo.InvariantCulture) == text;
    private static string EscapePointer(string text) => text.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static void EnsureCompatibleStructure(string domainId, string pointer, JsonNode? original, JsonNode? edited, bool locked)
    {
        if (JsonNode.DeepEquals(original, edited)) return;
        bool wiring = domainId == "vehicle.satsuma" && pointer.EndsWith("/electrical/installedConnectionIds", StringComparison.Ordinal);
        if (locked && !wiring) throw new InvalidDataException("Persistent identities and schema fields are read-only: " + pointer);
        if (original is JsonObject oldObject && edited is JsonObject newObject)
        {
            if (oldObject.Count != newObject.Count || oldObject.Any(field => !newObject.ContainsKey(field.Key))) throw new InvalidDataException("Object fields cannot be added or deleted: " + pointer);
            foreach (var field in oldObject)
            {
                string key = field.Key;
                bool immutable = key is "id" or "ids" or "guid" || key.EndsWith("Id", StringComparison.Ordinal) || key.EndsWith("Ids", StringComparison.Ordinal) || key.EndsWith("ID", StringComparison.Ordinal) || key.EndsWith("Guid", StringComparison.Ordinal) || key.EndsWith("Version", StringComparison.Ordinal)
                    || key is "hasSteeringAlignment" or "hasCamshaftTiming" or "hasEngineAdjustment" or "hasEngineDocking"
                        or "hasMechanicalCondition" or "hasValveAdjustment" or "hasServiceCaps" or "hasSatsumaOperatingState"
                        or "hasCombustionHistory" or "hasDashboardControls" or "hasFuelLineConnection"
                    || key == "kind" && pointer.EndsWith("/engineAdjustment", StringComparison.Ordinal)
                    || key == "kinds" && pointer.EndsWith("/serviceCaps", StringComparison.Ordinal);
                if (domainId == "vehicle.satsuma")
                {
                    string? presence = key switch
                    {
                        "mechanicalCondition" => "hasMechanicalCondition", "valveAdjustment" => "hasValveAdjustment",
                        "serviceCaps" => "hasServiceCaps", "satsumaOperatingState" => "hasSatsumaOperatingState",
                        "combustionRundownActive" => "hasCombustionHistory", _ => null,
                    };
                    // Unity can serialize default inline DTOs even while their
                    // extension is absent. Editing them would be silently ignored.
                    if (presence is not null && oldObject[presence]?.GetValueKind() != JsonValueKind.True) immutable = true;
                }
                EnsureCompatibleStructure(domainId, pointer + "/" + EscapePointer(key), field.Value, newObject[key], immutable);
            }
        }
        else if (original is JsonArray oldArray && edited is JsonArray newArray)
        {
            if (wiring)
            {
                if (newArray.Any(item => item?.GetValueKind() != JsonValueKind.String)) throw new InvalidDataException("Wiring connection IDs must be strings.");
                string[] connections = newArray.Select(item => item!.GetValue<string>()).ToArray();
                if (connections.Distinct(StringComparer.Ordinal).Count() != connections.Length || connections.Any(connection => !SchemaCatalog.WireConnections.ContainsKey(connection)))
                    throw new InvalidDataException("Wiring connections must be known and unique.");
                return;
            }
            if (oldArray.Count != newArray.Count) throw new InvalidDataException("Array membership is protected: " + pointer);
            for (int index = 0; index < oldArray.Count; index++) EnsureCompatibleStructure(domainId, pointer + "/" + index, oldArray[index], newArray[index], false);
        }
        else if (original is JsonValue oldValue && edited is JsonValue newValue)
        {
            JsonValueKind oldKind = oldValue.GetValueKind();
            JsonValueKind newKind = newValue.GetValueKind();
            bool bothBoolean = oldKind is JsonValueKind.True or JsonValueKind.False && newKind is JsonValueKind.True or JsonValueKind.False;
            if (oldKind != newKind && !bothBoolean) throw new InvalidDataException("Field type cannot change: " + pointer);
            if (oldKind == JsonValueKind.Number)
            {
                string newText = newValue.ToJsonString();
                if (!double.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number)) throw new InvalidDataException("Number must be finite: " + pointer);
                // Generic JSON does not retain the native C# float/double type.
                // Bound newly edited magnitudes to the range safe for either
                // type; unchanged opaque future data remains byte-preserved.
                if (Math.Abs(number) > float.MaxValue)
                    throw new InvalidDataException("Edited number exceeds the finite range supported by native float fields: " + pointer);
            }
        }
        else throw new InvalidDataException("Null values and field types cannot change: " + pointer);
    }

    private static void DescribeChanges(string domainId, string pointer, JsonNode? before, JsonNode? after, List<SaveChange> changes)
    {
        if (JsonNode.DeepEquals(before, after)) return;
        if (before is JsonObject beforeObject && after is JsonObject afterObject)
        {
            foreach (var field in beforeObject) DescribeChanges(domainId, pointer + "/" + EscapePointer(field.Key), field.Value, afterObject[field.Key], changes);
        }
        else if (before is JsonArray beforeArray && after is JsonArray afterArray)
        {
            for (int index = 0; index < Math.Max(beforeArray.Count, afterArray.Count); index++)
            {
                if (index >= beforeArray.Count) changes.Add(new(domainId, pointer + "/" + index, "(absent)", afterArray[index]?.ToJsonString() ?? "null"));
                else if (index >= afterArray.Count) changes.Add(new(domainId, pointer + "/" + index, beforeArray[index]?.ToJsonString() ?? "null", "(absent)"));
                else DescribeChanges(domainId, pointer + "/" + index, beforeArray[index], afterArray[index], changes);
            }
        }
        else changes.Add(new(domainId, pointer, before?.ToJsonString() ?? "null", after?.ToJsonString() ?? "null"));
    }

    private sealed record EditCommand(Dictionary<string, JsonNode> Before, Dictionary<string, JsonNode> After)
    {
        internal int ByteCount { get; } = Before.Values.Concat(After.Values).Sum(value => NativeSaveCodec.Utf8.GetByteCount(value.ToJsonString(NativeSaveCodec.PayloadOptions)));
    }
}
