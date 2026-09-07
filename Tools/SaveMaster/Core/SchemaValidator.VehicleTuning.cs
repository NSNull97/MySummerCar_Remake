using System.Text.Json;
using System.Text.Json.Nodes;

namespace MySummerRemake.SaveMaster.Core;

public static partial class SaveSchemaValidator
{
    // Snapshot of the current optional DTOs and explicit Phase1SatsumaOperatingAuthoring
    // bindings. Absent/false presence bits retain the game's pre-extension behavior.
    private static void ValidatePartTuningExtensions(CheckContext c, JsonObject part, string path, string definition)
    {
        if (ActiveTuningDto(c, part, "hasMechanicalCondition", "mechanicalCondition", path) is JsonObject condition)
        {
            string p = path + "/mechanicalCondition";
            CheckVersion(c, condition, p, 1);
            if (definition is not ("vehicle.satsuma.part.piston1" or "vehicle.satsuma.part.piston2" or
                "vehicle.satsuma.part.piston3" or "vehicle.satsuma.part.piston4" or "vehicle.satsuma.part.crankshaft" or
                "vehicle.satsuma.part.head-gasket" or "vehicle.satsuma.part.water-pump" or
                "vehicle.satsuma.part.alternator" or "vehicle.satsuma.part.rocker-shaft"))
                c.Error(p, "Износ сборки допустим только у проверенной штатной механической детали.");
            RequiredTuningRange(c, condition, "conditionPercent", p, 0, 100);
            RequiredTuningBool(c, condition, "broken", p);
            if (Number(condition, "conditionPercent") == 0 && !Bool(condition, "broken"))
                c.Error(p + "/broken", "При нулевом состоянии деталь должна быть отмечена сломанной.");
        }

        if (ActiveTuningDto(c, part, "hasValveAdjustment", "valveAdjustment", path) is JsonObject valves)
        {
            string p = path + "/valveAdjustment";
            CheckVersion(c, valves, p, 1);
            if (definition != "vehicle.satsuma.part.rocker-shaft")
                c.Error(p, "Клапаны регулируются только на штатной оси коромысел.");
            ValidateValveVector(c, valves, "intake", p, 4, 10);
            ValidateValveVector(c, valves, "exhaust", p, 3, 9);
        }

        if (ActiveTuningDto(c, part, "hasServiceCaps", "serviceCaps", path) is JsonObject caps)
        {
            string p = path + "/serviceCaps";
            CheckVersion(c, caps, p, 1);
            int[] expectedKinds = definition switch
            {
                "vehicle.satsuma.part.rocker-cover" or "vehicle.satsuma.part.gt-rocker-cover-gt" => [0],
                "vehicle.satsuma.part.radiator" => [1],
                "vehicle.satsuma.part.brake-master-cylinder" => [2, 3],
                "vehicle.satsuma.part.clutch-master-cylinder" => [4],
                _ => [],
            };
            if (expectedKinds.Length == 0) c.Error(p, "Крышки не принадлежат этому типу детали.");
            if (caps["kinds"] is not JsonArray kinds || caps["angles"] is not JsonArray angles ||
                kinds.Count is < 1 or > 2 || kinds.Count != angles.Count)
            { c.Error(p, "Для крышек нужны одинаковые массивы kinds и angles длиной 1 или 2."); return; }
            if (kinds.Count != expectedKinds.Length)
                c.Error(p + "/kinds", "Количество крышек не соответствует владельцу.");
            for (int i = 0; i < kinds.Count; i++)
            {
                double kind = kinds[i] is JsonNode kindNode ? ReadDouble(kindNode) : double.NaN;
                if (i >= expectedKinds.Length || kind != expectedKinds[i])
                    c.Error(p + "/kinds/" + i, "Тип и порядок крышек должны совпадать с проверенной привязкой детали.");
                double angle = angles[i] is JsonNode angleNode ? ReadDouble(angleNode) : double.NaN;
                if (!double.IsFinite(angle) || angle < 1 || angle > 359)
                    c.Error(p + "/angles/" + i, "Угол крышки должен быть от 1 до 359 градусов.");
            }
        }
    }

    private static void ValidateVehicleOperatingExtension(CheckContext c, JsonObject simulation, string path)
    {
        // The rundown bit is only authoritative when its presence bit is true.
        OptionalTuningBool(c, simulation, "hasCombustionHistory", path);
        if (Bool(simulation, "hasCombustionHistory")) RequiredTuningBool(c, simulation, "combustionRundownActive", path);
        if (ActiveTuningDto(c, simulation, "hasSatsumaOperatingState", "satsumaOperatingState", path) is not JsonObject state) return;
        string p = path + "/satsumaOperatingState";
        CheckVersion(c, state, p, 1);
        RequiredTuningRange(c, state, "brakeFrontLiters", p, 0, 1);
        RequiredTuningRange(c, state, "brakeRearLiters", p, 0, 1);
        RequiredTuningRange(c, state, "clutchLiters", p, 0, 0.5);
        RequiredTuningRange(c, state, "oilContaminationPercent", p, 0, 100);
        RequiredTuningRange(c, state, "oilPressureBar", p, 0, 10);
        RequiredTuningRange(c, state, "coolantPressurePsi", p, 0, 50);
        RequiredTuningRange(c, state, "crankingSeconds", p, 0, 60);
        RequiredTuningBool(c, state, "radiatorFanRunning", p);
    }

    private static JsonObject? ActiveTuningDto(CheckContext c, JsonObject owner, string flag, string field, string path)
    {
        OptionalTuningBool(c, owner, flag, path);
        if (!Bool(owner, flag)) return null;
        if (owner[field] is JsonObject dto) return dto;
        c.Error(path + "/" + field, "Флаг наличия включён, но состояние отсутствует или имеет неверный тип.");
        return null;
    }

    private static void ValidateValveVector(CheckContext c, JsonObject owner, string field, string path, double min, double max)
    {
        if (owner[field] is not JsonObject vector)
        { c.Error(path + "/" + field, "Отсутствуют четыре настройки клапанов."); return; }
        foreach (string component in new[] { "x", "y", "z", "w" })
            RequiredTuningRange(c, vector, component, path + "/" + field, min, max);
    }

    private static void RequiredTuningRange(CheckContext c, JsonObject owner, string field, string path, double min, double max)
    {
        if (owner[field]?.GetValueKind() != JsonValueKind.Number)
        { c.Error(path + "/" + field, "Отсутствует числовое состояние."); return; }
        Range(c, owner, field, path, min, max);
    }

    private static void OptionalTuningBool(CheckContext c, JsonObject owner, string field, string path)
    {
        if (owner.ContainsKey(field)) RequiredTuningBool(c, owner, field, path);
    }

    private static void RequiredTuningBool(CheckContext c, JsonObject owner, string field, string path)
    {
        if (owner[field]?.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
            c.Error(path + "/" + field, "Отсутствует логическое состояние.");
    }
}
