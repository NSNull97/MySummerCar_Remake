using System.Text.Json;
using System.Text.Json.Nodes;
using MySummerRemake.SaveMaster.Core;

namespace MySummerRemake.SaveMaster;

internal sealed record FieldRow(string Domain, string Pointer, string Key, string Owner, string Value, JsonValueKind Kind)
{
    public string SearchText => $"{Domain} {Pointer} {Owner} {Value}";
}

internal static class FieldIndex
{
    public static List<FieldRow> Build(IReadOnlyList<SaveDomain> domains)
    {
        var fields = new List<FieldRow>();
        foreach (var domain in domains) Walk(domain.Payload, domain.Id, "", "", fields);
        return fields;
    }

    private static void Walk(JsonNode? node, string domain, string pointer, string owner, List<FieldRow> fields)
    {
        if (node is JsonObject obj)
        {
            var objectName = new[] { "partDefinitionId", "fastenerDefinitionId", "itemDefinitionId", "definitionId", "characterDefinitionId", "mountId", "vehicleId", "stableEntityId", "id" }
                .Select(key => obj[key]?.ToString()).FirstOrDefault(value => !string.IsNullOrEmpty(value));
            if (objectName is not null) owner = objectName;
            foreach (var property in obj) Walk(property.Value, domain, pointer + "/" + Escape(property.Key), owner, fields);
        }
        else if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++) Walk(array[i], domain, pointer + "/" + i, owner, fields);
        }
        else
        {
            var kind = node?.GetValueKind() ?? JsonValueKind.Null;
            fields.Add(new FieldRow(domain, pointer, Unescape(pointer.Split('/').Last()), owner,
                kind == JsonValueKind.String ? node!.GetValue<string>() : node?.ToJsonString() ?? "null", kind));
        }
    }

    public static string Escape(string value) => value.Replace("~", "~0").Replace("/", "~1");
    public static string Unescape(string value) => value.Replace("~1", "/").Replace("~0", "~");
    public static JsonNode? At(JsonNode? node, string pointer)
    {
        foreach (var segment in pointer.Split('/').Skip(1))
            node = node is JsonArray array ? array[int.Parse(segment)] : node?[Unescape(segment)];
        return node;
    }
}
