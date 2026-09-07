using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MySummerRemake.SaveMaster.Core;

// Independent implementation of the project-owned MSC.Save envelope contract.
// Domains are JSON strings to the Unity codec; their internal JSON is never part
// of Unity's floating point formatting. Preserve outer scalar lexemes from the
// verified source, especially PlayTimeSeconds, instead of rounding via .NET.
internal sealed partial class NativeSaveCodec
{
    internal static readonly UTF8Encoding Utf8 = new(false, true);
    internal static readonly JsonSerializerOptions PayloadOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        MaxDepth = SaveMasterLimits.MaximumJsonDepth
    };
    private static readonly string[] HeaderFields = ["FormatId", "DocumentVersion", "SaveId", "SlotId", "BuildId", "CreatedUtc", "UpdatedUtc", "IntegritySha256"];
    private static readonly string[] MetadataFields = ["DisplayName", "PlayTimeSeconds", "GameTimestamp", "LocationStableId"];
    private static readonly string[] DomainFields = ["DomainId", "SchemaVersion", "Required", "PayloadJson"];

    internal static NativeDocument Read(byte[] bytes)
    {
        if (bytes.Length > SaveMasterLimits.MaximumDocumentBytes)
            throw new InvalidDataException("Save exceeds the 16 MiB document limit.");
        string json = Utf8.GetString(bytes);
        if (json.StartsWith('\uFEFF')) json = json[1..];
        using JsonDocument parsed = ParseStrict(json);
        JsonElement root = parsed.RootElement;
        RequireProperties(root, ["Header", "Metadata", "Domains"], "document");
        JsonElement header = root.GetProperty("Header");
        JsonElement metadata = root.GetProperty("Metadata");
        RequireProperties(header, HeaderFields, "Header");
        RequireProperties(metadata, MetadataFields, "Metadata");
        ValidateHeader(header, metadata);
        var result = new NativeDocument(RawFields(header), RawFields(metadata), header.GetProperty("DocumentVersion").GetInt32());
        JsonElement envelopes = root.GetProperty("Domains");
        if (envelopes.ValueKind != JsonValueKind.Array || envelopes.GetArrayLength() > SaveMasterLimits.MaximumDomainCount)
            throw new InvalidDataException("Invalid domain array or more than 512 domains.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (JsonElement envelope in envelopes.EnumerateArray())
        {
            RequireProperties(envelope, DomainFields, "domain envelope");
            string id = RequireString(envelope, "DomainId", 96, false);
            if (!DomainIdPattern().IsMatch(id) || !ids.Add(id))
                throw new InvalidDataException($"Invalid or duplicate domain ID '{id}'.");
            if (!envelope.GetProperty("SchemaVersion").TryGetInt32(out int version) || version < 1)
                throw new InvalidDataException($"Invalid schema version for '{id}'.");
            if (envelope.GetProperty("Required").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidDataException($"Invalid Required flag for '{id}'.");
            string payloadJson = RequireString(envelope, "PayloadJson", int.MaxValue, true);
            if (Utf8.GetByteCount(payloadJson) > SaveMasterLimits.MaximumDomainPayloadBytes)
                throw new InvalidDataException($"Domain '{id}' exceeds the 2 MiB payload limit.");
            using JsonDocument payloadCheck = ParseStrict(payloadJson);
            JsonNode payload = JsonNode.Parse(payloadJson, documentOptions: new JsonDocumentOptions { MaxDepth = SaveMasterLimits.MaximumJsonDepth })
                ?? throw new InvalidDataException($"Domain '{id}' has a null payload.");
            if (payload is not JsonObject)
                throw new InvalidDataException($"Domain '{id}' must contain a JSON object.");
            result.Domains.Add(new DomainState(id, version, envelope.GetProperty("Required").GetBoolean(), RawFields(envelope), payloadJson, payload));
        }
        result.Domains.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        string actual = header.GetProperty("IntegritySha256").GetString()!;
        string expected = Hash(Utf8.GetBytes(Canonical(result, false)));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expected)))
            throw new InvalidDataException("Native save checksum failed. The file is corrupt, modified externally, or uses an unsupported envelope encoding; editing is refused.");
        return result;
    }

    internal static byte[] Write(NativeDocument document)
    {
        document.Header["IntegritySha256"] = Quote(Hash(Utf8.GetBytes(Canonical(document, false))));
        byte[] bytes = Utf8.GetBytes(Canonical(document, true) + "\n");
        if (bytes.Length > SaveMasterLimits.MaximumDocumentBytes)
            throw new InvalidDataException("Edited save exceeds the 16 MiB document limit.");
        // Validate the exact staged bytes, including their newly calculated hash.
        _ = Read(bytes);
        return bytes;
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string Canonical(NativeDocument document, bool includeIntegrity)
    {
        var json = new StringBuilder();
        json.Append("{\"Header\":{");
        AppendFields(json, HeaderFields, document.Header, includeIntegrity ? null : "IntegritySha256");
        json.Append("},\"Metadata\":{");
        AppendFields(json, MetadataFields, document.Metadata, null);
        json.Append("},\"Domains\":[");
        for (int index = 0; index < document.Domains.Count; index++)
        {
            DomainState domain = document.Domains[index];
            if (index != 0) json.Append(',');
            json.Append('{');
            for (int field = 0; field < DomainFields.Length; field++)
            {
                string key = DomainFields[field];
                if (field != 0) json.Append(',');
                json.Append('"').Append(key).Append("\":");
                json.Append(key == "PayloadJson" && !JsonNode.DeepEquals(domain.Payload, domain.OriginalPayload)
                    ? Quote(domain.Payload.ToJsonString(PayloadOptions))
                    : domain.RawFields[key]);
            }
            json.Append('}');
        }
        return json.Append("]}").ToString();
    }

    private static void AppendFields(StringBuilder json, string[] fields, Dictionary<string, string> raw, string? blank)
    {
        for (int index = 0; index < fields.Length; index++)
        {
            string field = fields[index];
            if (index != 0) json.Append(',');
            json.Append('"').Append(field).Append("\":").Append(field == blank ? "\"\"" : raw[field]);
        }
    }

    // JsonUtility leaves Unicode and '/' literal, and uses standard JSON escapes
    // for controls, backslashes and quotes. Keep the payload as an opaque string.
    internal static string Quote(string text)
    {
        var result = new StringBuilder(text.Length + 2).Append('"');
        foreach (char value in text)
        {
            switch (value)
            {
                case '"': result.Append("\\\""); break;
                case '\\': result.Append("\\\\"); break;
                case '\b': result.Append("\\b"); break;
                case '\f': result.Append("\\f"); break;
                case '\n': result.Append("\\n"); break;
                case '\r': result.Append("\\r"); break;
                case '\t': result.Append("\\t"); break;
                default:
                    if (value < 0x20) result.Append("\\u").Append(((int)value).ToString("X4", CultureInfo.InvariantCulture));
                    else result.Append(value);
                    break;
            }
        }
        return result.Append('"').ToString();
    }

    internal static JsonDocument ParseStrict(string json)
    {
        try
        {
            var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = SaveMasterLimits.MaximumJsonDepth });
            try { RejectDuplicates(document.RootElement); }
            catch { document.Dispose(); throw; }
            return document;
        }
        catch (JsonException exception) { throw new InvalidDataException("Malformed JSON: " + exception.Message, exception); }
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException($"Duplicate JSON field '{property.Name}' is ambiguous.");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in element.EnumerateArray()) RejectDuplicates(item);
        else if (element.ValueKind == JsonValueKind.Number && (!element.TryGetDouble(out double value) || !double.IsFinite(value)))
            throw new InvalidDataException("Non-finite JSON number is not supported by native saves.");
    }

    private static Dictionary<string, string> RawFields(JsonElement element) => element.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetRawText(), StringComparer.Ordinal);

    private static void RequireProperties(JsonElement element, string[] fields, string section)
    {
        if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject().Count() != fields.Length || fields.Any(field => !element.TryGetProperty(field, out _)))
            throw new InvalidDataException($"Unsupported {section} structure. Required fields must be present and unknown envelope fields are never silently discarded.");
    }

    private static string RequireString(JsonElement element, string key, int maximum, bool allowEmpty)
    {
        JsonElement field = element.GetProperty(key);
        if (field.ValueKind != JsonValueKind.String) throw new InvalidDataException($"{key} must be a string.");
        string value = field.GetString()!;
        if (value.Length > maximum || !allowEmpty && string.IsNullOrWhiteSpace(value)) throw new InvalidDataException($"Invalid length or empty value for {key}.");
        return value;
    }

    private static void ValidateHeader(JsonElement header, JsonElement metadata)
    {
        if (RequireString(header, "FormatId", 256, false) != "msc.native-save")
            throw new InvalidDataException("This is not a My Summer Remake native save. Original MSC saves are not supported.");
        if (!header.GetProperty("DocumentVersion").TryGetInt32(out int version) || version < 1)
            throw new InvalidDataException("Invalid document version.");
        RequireString(header, "SaveId", 256, false);
        RequireString(header, "BuildId", 256, true);
        if (!SlotIdPattern().IsMatch(RequireString(header, "SlotId", 48, false))) throw new InvalidDataException("Invalid SlotId.");
        string created = RequireString(header, "CreatedUtc", 64, false);
        string updated = RequireString(header, "UpdatedUtc", 64, false);
        foreach (string timestamp in new[] { created, updated })
            if (!DateTimeOffset.TryParseExact(timestamp, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) || parsed.Offset != TimeSpan.Zero)
                throw new InvalidDataException("Header dates must be ISO-8601 UTC timestamps.");
        if (string.CompareOrdinal(created, updated) > 0) throw new InvalidDataException("CreatedUtc is after UpdatedUtc.");
        if (!HashPattern().IsMatch(RequireString(header, "IntegritySha256", 64, false))) throw new InvalidDataException("Missing or invalid lowercase SHA-256 integrity value.");
        RequireString(metadata, "DisplayName", 128, true);
        RequireString(metadata, "GameTimestamp", 256, true);
        RequireString(metadata, "LocationStableId", 256, true);
        if (!metadata.GetProperty("PlayTimeSeconds").TryGetDouble(out double playTime) || !double.IsFinite(playTime) || playTime < 0)
            throw new InvalidDataException("PlayTimeSeconds must be finite and non-negative.");
    }

    [GeneratedRegex("^[a-z][a-z0-9._-]{0,95}$", RegexOptions.CultureInvariant)] private static partial Regex DomainIdPattern();
    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,47}$", RegexOptions.CultureInvariant)] private static partial Regex SlotIdPattern();
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)] private static partial Regex HashPattern();
}

internal sealed class NativeDocument(Dictionary<string, string> header, Dictionary<string, string> metadata, int version)
{
    internal Dictionary<string, string> Header { get; } = header;
    internal Dictionary<string, string> Metadata { get; } = metadata;
    internal int Version { get; } = version;
    internal List<DomainState> Domains { get; } = [];
}

internal sealed class DomainState(string id, int schemaVersion, bool required, Dictionary<string, string> rawFields, string originalPayloadJson, JsonNode payload)
{
    internal string Id { get; } = id;
    internal int SchemaVersion { get; } = schemaVersion;
    internal bool Required { get; } = required;
    internal Dictionary<string, string> RawFields { get; } = rawFields;
    internal string OriginalPayloadJson { get; } = originalPayloadJson;
    internal JsonNode OriginalPayload { get; } = payload.DeepClone();
    internal JsonNode Payload { get; set; } = payload;
    internal SaveDomain Snapshot() => new(Id, SchemaVersion, Required, Payload.DeepClone());
}
