using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MySummerRemake.SaveMaster.Core;

public sealed partial class SaveSession
{
    /// <summary>Apply existing, active vehicle adjustments atomically, including their dependent state.</summary>
    public void ApplyVehicleTuning(IReadOnlyList<SaveEdit> edits)
    {
        EnsureEditable();
        DomainState vehicle = GetEditableDomain("vehicle.satsuma");
        JsonNode? items = document.Domains.FirstOrDefault(domain => domain.Id == "items.instances")?.Payload;
        var allowed = new Dictionary<(string Domain, string Pointer), VehicleTuningField>();
        if (vehicle.Payload["vehicles"] is JsonArray vehicles)
            for (int i = 0; i < vehicles.Count; i++)
                foreach (VehicleTuningField field in VehicleTuningCatalog.Build(vehicle.Payload, i, items))
                    if (field.IsEditable) allowed[(field.DomainId, field.JsonPointer)] = field;

        var staged = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        var touched = new HashSet<(string Domain, string Pointer)>();
        foreach (SaveEdit edit in edits)
        {
            if (!allowed.TryGetValue((edit.DomainId, edit.JsonPointer), out VehicleTuningField? field))
                throw new InvalidDataException("Настройка недоступна или вычисляется игрой: " + edit.DomainId + edit.JsonPointer);
            if (!touched.Add((edit.DomainId, edit.JsonPointer)))
                throw new InvalidDataException("Настройка указана дважды: " + field.Label);
            if (!staged.TryGetValue(edit.DomainId, out JsonNode? clone))
                staged[edit.DomainId] = clone = GetEditableDomain(edit.DomainId).Payload.DeepClone();
            string text = edit.Text.Trim();
            if (!field.IsBoolean) text = text.Replace(',', '.');
            JsonNode value = ParseScalar(Resolve(clone, edit.JsonPointer), text);
            if (!field.IsBoolean)
            {
                if (!double.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    || !double.IsFinite(number) || Math.Abs(number) > float.MaxValue
                    || field.Min is double min && number < min || field.Max is double max && number > max)
                    throw new InvalidDataException("Значение вне допустимого диапазона: " + field.Label);
            }
            SetAt(clone, edit.JsonPointer, value);
        }

        // Resolve coupling after all requested edits, independent of their order.
        foreach (var key in touched)
        {
            JsonNode clone = staged[key.Domain];
            if (key.Domain == "vehicle.satsuma" && key.Pointer.EndsWith("/simulation/batteryVoltage", StringComparison.Ordinal))
            {
                string charge = key.Pointer[..^"batteryVoltage".Length] + "batteryCharge01";
                if (Resolve(clone, key.Pointer[..key.Pointer.LastIndexOf('/')]) is JsonObject simulation
                    && simulation["batteryCharge01"]?.GetValueKind() == JsonValueKind.Number)
                    SetAt(clone, charge, JsonValue.Create(Math.Clamp(Resolve(clone, key.Pointer)!.GetValue<double>() / 12.6, 0, 1))!);
            }
            if (key.Domain == "vehicle.satsuma" && key.Pointer.EndsWith("/mechanicalCondition/conditionPercent", StringComparison.Ordinal)
                && Resolve(clone, key.Pointer)!.GetValue<double>() == 0)
                SetAt(clone, key.Pointer[..^"conditionPercent".Length] + "broken", JsonValue.Create(true)!);
        }

        if (staged.Count == 0) return;
        SaveDomain[] snapshots = document.Domains.Select(domain =>
        {
            SaveDomain snapshot = domain.Snapshot();
            return staged.TryGetValue(domain.Id, out JsonNode? payload) ? snapshot with { Payload = payload.DeepClone() } : snapshot;
        }).ToArray();
        var issues = SaveSchemaValidator.Validate(snapshots).ToList();
        if (Validator is not null) issues.AddRange(Validator(snapshots));
        ValidationIssue[] errors = issues.Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidDataException("Настройки не прошли проверку: " + string.Join("; ", errors.Take(6).Select(error => error.DomainId + error.JsonPointer + ": " + error.Message)));
        CommitEdit(staged);
    }
}
