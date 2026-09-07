using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MySummerRemake.SaveMaster.Core;

public sealed record VehicleTuningField(string Category, string Label, string DomainId,
    string JsonPointer, string Unit, double? Min, double? Max, double? RecommendedValue,
    bool IsEditable, string Help, bool IsBoolean = false, double Increment = 0.1,
    string Owner = "");

/// <summary>
/// Read-only descriptions of existing save fields, audited against the project-owned
/// assembly DTOs, SatsumaOperatingSaveDto and the 2026-09-06 engine player guide.
/// Presence bits and independently versioned DTOs remain authoritative.
/// </summary>
public static class VehicleTuningCatalog
{
    private const string VehicleDomain = "vehicle.satsuma";
    private const string ItemsDomain = "items.instances";
    private const string Adjustments = "Регулировки";
    private const string Fluids = "Жидкости";
    private const string Condition = "Состояние";
    private const string Simulation = "Симуляция";
    private const string SnapshotHelp = "Сохранённый снимок: игра пересчитывает значение при симуляции. Это не регулировка и не подтверждение исправности двигателя.";

    public static IReadOnlyList<VehicleTuningField> Build(JsonNode vehicleDomain,
        int vehicleIndex, JsonNode? itemsDomain = null)
    {
        var fields = new List<VehicleTuningField>();
        if (vehicleDomain is not JsonObject root || !Version(root, 1) ||
            root["vehicles"] is not JsonArray vehicles || vehicleIndex < 0 ||
            vehicleIndex >= vehicles.Count || vehicles[vehicleIndex] is not JsonObject vehicle ||
            !Version(vehicle, 1)) return fields.AsReadOnly();

        string path = "/vehicles/" + vehicleIndex.ToString(CultureInfo.InvariantCulture);
        string owner = WithIdentity("Сатсума", Text(vehicle["stableVehicleId"]));
        if (vehicle["simulation"] is JsonObject simulation && Version(simulation, 1))
            AddSimulation(new FieldGroup(fields, simulation, path + "/simulation", owner));
        if (Active(vehicle, "hasDashboardControls", "dashboardControls") is JsonObject dashboard)
        {
            var controls = new FieldGroup(fields, dashboard, path + "/dashboardControls", owner);
            controls.Number("choke01", Adjustments, "Подсос", "0–1", 0, 1, null, true,
                "0 — утоплен, 1 — вытянут. Для проверки смеси прогретого двигателя подсос убирают; холодному запуску он может понадобиться.");
        }
        if (vehicle["electrical"] is JsonObject electrical && Version(electrical, 2))
            AddElectrical(new FieldGroup(fields, electrical, path + "/electrical", owner));

        if (vehicle["assembly"] is not JsonObject assembly ||
            ReadNumber(assembly["schemaVersion"]) is not (1 or 2 or 3)) return fields.AsReadOnly();
        string assemblyPath = path + "/assembly";
        if (assembly["parts"] is JsonArray parts)
        {
            for (int i = 0; i < parts.Count; i++)
                if (parts[i] is JsonObject part)
                    AddPart(fields, part, assemblyPath + "/parts/" + i);
        }
        if (Version(assembly, 3) && assembly["dynamicParts"] is JsonArray dynamicParts)
        {
            var linkedItems = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < dynamicParts.Count; i++)
            {
                if (dynamicParts[i] is not JsonObject dynamic || dynamic["part"] is not JsonObject part) continue;
                string itemDefinition = Text(dynamic["itemDefinitionId"]);
                if (ExpectedDynamicPart(itemDefinition) is not string expected ||
                    expected != Text(part["partDefinitionId"])) continue;
                AddPart(fields, part, assemblyPath + "/dynamicParts/" + i + "/part");
                string stableId = Text(part["stableEntityId"]);
                if (!string.IsNullOrWhiteSpace(stableId) && linkedItems.Add(stableId))
                    AddItemCondition(fields, itemsDomain, part, itemDefinition, stableId);
            }
        }
        return fields.AsReadOnly();
    }

    private static void AddSimulation(FieldGroup group)
    {
        group.Number("oilLiters", Fluids, "Моторное масло", "л", 0, null, 3, true,
            "Ориентир при обслуживании — 3 л. Старые сохранения с запасом выше 3 л допустимы и не обрезаются. Поддон, фильтр и пробка должны удерживать масло.");
        group.Number("coolantLiters", Fluids, "Охлаждающая жидкость", "л", 0, null, 5.4, true,
            "Ориентир при обслуживании — 5,4 л. Сохранение допускает и больший запас; исправность радиатора, шлангов и помпы проверяется отдельно.");
        group.Number("fuelLiters", Fluids, "Топливо", "л", 0, null, null, true,
            "Фактический запас топлива. Проверенной ёмкости бака для рекомендации здесь нет; стартовые 15 л не означают полный бак.");
        group.Number("batteryVoltage", Condition, "Напряжение аккумулятора", "В", 0, null, 12.6, true,
            "Номинальное напряжение — 12,6 В. Заряд пересчитывается вместе с напряжением. В текущем ремейке это общее состояние машины; отдельное состояние купленной батареи здесь не задаётся.");
        group.Number("batteryCharge01", Simulation, "Расчётный заряд аккумулятора", "0–1", 0, 1, null, false,
            "Рассчитывается как доля напряжения от 12,6 В в пределах 0–1. Изменяется согласованно с напряжением, отдельно не редактируется.");
        group.Number("engineRpm", Simulation, "Обороты двигателя", "об/мин", 0, null, null, false, SnapshotHelp);
        group.Number("engineTemperatureCelsius", Simulation, "Температура двигателя", "°C", null, null, null, false, SnapshotHelp);
        group.Number("coolantTemperatureCelsius", Simulation, "Температура охлаждения", "°C", null, null, null, false, SnapshotHelp);
        group.Number("filteredThrottle01", Simulation, "Положение газа после фильтрации", "0–1", 0, 1, null, false, SnapshotHelp);
        group.Number("engineLoad01", Simulation, "Нагрузка двигателя", "0–1", 0, 1, null, false, SnapshotHelp);
        group.Number("engineTorqueNewtonMeters", Simulation, "Крутящий момент двигателя", "Н·м", null, null, null, false, SnapshotHelp);
        group.Number("engineStatus", Simulation, "Состояние двигателя", "код", 0, 3, null, false,
            SnapshotHelp + " 0 — остановлен, 1 — прокрутка, 2 — работает, 3 — заглох.", 1);
        if (IsTrue(group.Data["hasCombustionHistory"]))
            group.Boolean("combustionRundownActive", Simulation, "Выбег после горения", false, SnapshotHelp);

        if (Active(group.Data, "hasSatsumaOperatingState", "satsumaOperatingState") is not JsonObject operating) return;
        var state = group.Child(operating, "satsumaOperatingState");
        state.Number("brakeFrontLiters", Fluids, "Передний тормозной бачок", "л", 0, 1, 1, true,
            "Ёмкость переднего контура — 1 л. Это отдельный запас; задний бачок и соединения обслуживаются отдельно.");
        state.Number("brakeRearLiters", Fluids, "Задний тормозной бачок", "л", 0, 1, 1, true,
            "Ёмкость заднего контура — 1 л. Уровень не заменяет установку магистралей и затяжку соединений.");
        state.Number("clutchLiters", Fluids, "Бачок сцепления", "л", 0, 0.5, 0.5, true,
            "Ёмкость гидравлики сцепления — 0,5 л. Используется тормозная жидкость.");
        state.Number("oilContaminationPercent", Fluids, "Загрязнение масла", "%", 0, 100, 0, true,
            "0 — чистое масло, 100 — максимальное загрязнение. Уровень масла и состояние двигателя сохраняются отдельно.", 1);
        state.Number("oilPressureBar", Simulation, "Давление масла", "бар", 0, 10, null, false, SnapshotHelp);
        state.Number("coolantPressurePsi", Simulation, "Давление охлаждения", "psi", 0, 50, null, false, SnapshotHelp);
        state.Number("crankingSeconds", Simulation, "Накопленная прокрутка стартера", "с", 0, 60, null, false, SnapshotHelp);
        state.Boolean("radiatorFanRunning", Simulation, "Вентилятор радиатора работает", false,
            "Расчётный снимок: питание, проводка, радиатор и температура определяют работу вентилятора. Сам флаг не ремонтирует цепь.");
    }

    private static void AddElectrical(FieldGroup group)
    {
        if (group.Data["installedConnectionIds"] is not JsonArray connections) return;
        bool Has(string id) => connections.Any(node => Text(node) == id);
        Add("batteryPlusStage", "Плюсовая клемма", Has("BatteryHarness") || Has("Starter"));
        Add("batteryMinusStage", "Минусовая клемма", Has("GroundBattery"));
        Add("starterCableStage", "Силовой кабель стартера", Has("Starter"));
        void Add(string key, string label, bool cablePresent) => group.Number(key, Adjustments,
            label, "ступень", 0, 8, null, cablePresent,
            cablePresent ? "Ступень затяжки 0–8; 8 — до упора. Провод должен существовать в установленном жгуте. Это не отдельное состояние исправности стартера."
                : "Соответствующий кабель отсутствует; без него допустима только нулевая затяжка. Подключение выполняется отдельно в разделе проводки.", 1);
    }

    private static void AddPart(List<VehicleTuningField> fields, JsonObject part, string path)
    {
        string definition = Text(part["partDefinitionId"]);
        string owner = WithIdentity(PartName(definition), Text(part["stableEntityId"]));
        bool installed = ReadNumber(part["lifecycleState"]) == 1;
        var group = new FieldGroup(fields, part, path, owner);
        if (Active(part, "hasEngineAdjustment", "engineAdjustment") is JsonObject adjustment)
        {
            var setting = group.Child(adjustment, "engineAdjustment");
            switch (ReadNumber(adjustment["kind"]))
            {
                case 1 when definition == "vehicle.satsuma.part.alternator":
                    setting.Number("value", Adjustments, "Положение генератора", "°", 0, 8, 7, true,
                        "Проверенная исходная натяжка — 7. Для надевания ремня положение сначала ниже 4; после настройки затягивают фиксатор. Штатный шаг — 0,5°.", 0.5);
                    break;
                case 2 when definition == "vehicle.satsuma.part.distributor":
                    setting.Number("value", Adjustments, "Угол трамблёра", "°", 0, 20, 15, true,
                        "Проверенная исходная настройка — 15°. В игре регулировка требует установленного трамблёра и ослабленного винта; после настройки винт затягивают.", 0.2);
                    break;
                case 3 when definition == "vehicle.satsuma.part.carburetor":
                    setting.Number("value", Adjustments, "Смесь карбюратора", "игр. ед.", 10, 22, 15, true,
                        "15 — стартовая настройка винта, НЕ AFR. Меньше — беднее, больше — богаче. Итог проверяют на прогретом моторе без подсоса; AFR вычисляется из условий работы и отдельно не сохраняется.", 0.2);
                    break;
                case 4 when definition == "vehicle.satsuma.part.oilfilter0":
                    setting.Number("value", Adjustments, "Затяжка масляного фильтра", "ступень", 0, 8, null, installed,
                        installed ? "Целые ступени 0–8; установленный фильтр затягивают до 8. Перед снятием отпускают до 0."
                            : "Фильтр снят: сохранённая затяжка обязана быть 0. Для затяжки сначала установите фильтр в игре.", 1);
                    break;
            }
        }
        if (definition == "vehicle.satsuma.part.rocker-shaft" &&
            Active(part, "hasValveAdjustment", "valveAdjustment") is JsonObject valves)
        {
            string[] axes = ["x", "y", "z", "w"];
            var valveGroup = group.Child(valves, "valveAdjustment");
            foreach (bool intake in new[] { true, false })
            {
                string key = intake ? "intake" : "exhaust";
                if (valves[key] is not JsonObject vector) continue;
                var fourValves = valveGroup.Child(vector, key);
                for (int i = 0; i < axes.Length; i++)
                    fourValves.Number(axes[i], Adjustments,
                        (intake ? "Впускной клапан" : "Выпускной клапан") + " · цилиндр " + (i + 1),
                        "игр. ед.", intake ? 4 : 3, intake ? 10 : 9, intake ? 7 : 6, true,
                        "Игровая настройка, НЕ миллиметры. Рабочее окно: " + (intake ? "впуск 6–8" : "выпуск 5–7") +
                        ". Это отдельный регулировочный винт; пять болтов оси коромысел сохраняются в крепеже.", 0.3);
            }
        }
        if (definition == "vehicle.satsuma.part.camshaft-gear" &&
            Active(part, "hasCamshaftTiming", "camshaftTiming") is JsonObject timing)
            group.Child(timing, "camshaftTiming").Number("angleDegrees", Adjustments,
                "Метка распредвала", "°", 0, 360, installed ? 0 : null, installed,
                installed ? "0° — совмещённая метка; 360° эквивалентно 0°. Штатный поворот — 5°. Положение влияет на горение и износ оси."
                    : "Шестерня снята: при загрузке игра заново случайно выбирает угол даже при сохранённом значении. Редактирование отключено до установки.", 5);
        if (definition is "vehicle.satsuma.part.steering-rod-fl" or "vehicle.satsuma.part.steering-rod-fr" &&
            Active(part, "hasSteeringAlignment", "steeringAlignment") is JsonObject alignment)
            group.Child(alignment, "steeringAlignment").Number("alignmentDegrees", Adjustments,
                "Схождение", "°", -6, 6, installed ? 0 : null, installed,
                installed ? "0° — нейтральное положение совместимости старых установленных тяг. Это не подтверждённый дорожный оптимум; левая и правая тяги настраиваются отдельно."
                    : "Тяга снята: игра случайно выбирает схождение при загрузке. Сохранённая цифра не удержится; редактирование доступно после установки.");
        if (HasMechanicalCondition(definition) &&
            Active(part, "hasMechanicalCondition", "mechanicalCondition") is JsonObject condition)
        {
            var state = group.Child(condition, "mechanicalCondition");
            state.Number("conditionPercent", Condition, "Состояние детали", "%", 0, 100, null, true,
                "100% — исправное состояние, 0% включает поломку. Повышение процента при оставшейся галочке «Деталь сломана» само по себе не чинит деталь.", 1);
            state.Boolean("broken", Condition, "Деталь сломана", true,
                "Флаг поломки механической детали. При состоянии 0% он обязан быть включён; согласованное исправление меняет и процент, и флаг.");
        }
        AddCaps(group, definition);
    }

    private static void AddCaps(FieldGroup group, string definition)
    {
        if (Active(group.Data, "hasServiceCaps", "serviceCaps") is not JsonObject caps ||
            caps["kinds"] is not JsonArray kinds || caps["angles"] is not JsonArray angles) return;
        int[] expected = definition switch
        {
            "vehicle.satsuma.part.rocker-cover" or "vehicle.satsuma.part.gt-rocker-cover-gt" => [0],
            "vehicle.satsuma.part.radiator" => [1],
            "vehicle.satsuma.part.brake-master-cylinder" => [2, 3],
            "vehicle.satsuma.part.clutch-master-cylinder" => [4],
            _ => [],
        };
        if (expected.Length == 0 || kinds.Count != expected.Length || angles.Count != expected.Length) return;
        for (int i = 0; i < expected.Length; i++)
            if (ReadNumber(kinds[i]) != expected[i]) return;
        string[] names = ["Крышка маслозаливной горловины", "Крышка радиатора", "Крышка переднего тормозного бачка",
            "Крышка заднего тормозного бачка", "Крышка бачка сцепления"];
        var capGroup = group.Child(caps, "serviceCaps");
        for (int i = 0; i < expected.Length; i++)
            capGroup.NumberAt(angles[i], "angles/" + i, Fluids, names[expected[i]], "°", 1, 359, null, true,
                "1° — полностью открыта, 359° — закрыта. Штатный шаг — 33°; жидкость льётся только через полностью открытую подходящую горловину. Тип и порядок крышек закреплены за деталью.", 33);
    }

    private static void AddItemCondition(List<VehicleTuningField> fields, JsonNode? itemsDomain,
        JsonObject part, string itemDefinition, string stableId)
    {
        if (itemsDomain is not JsonObject root || !Version(root, 1) || root["instances"] is not JsonArray items) return;
        JsonObject? matchedState = null;
        int matchedIndex = -1;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is not JsonObject record || record["state"] is not JsonObject state ||
                Text(state["stableEntityId"]) != stableId) continue;
            // Ambiguous ownership is never resolved by taking the first matching item.
            if (matchedIndex >= 0) return;
            matchedIndex = i;
            matchedState = state;
        }
        if (matchedState is null || !Version(matchedState, 1) ||
            Text(matchedState["definitionId"]) != itemDefinition) return;
        var group = new FieldGroup(fields, matchedState, "/instances/" + matchedIndex + "/state",
            WithIdentity(PartName(Text(part["partDefinitionId"])), stableId), ItemsDomain);
        group.Number("condition", Condition, "Состояние покупной детали", "%", 0, 100, null, true,
            "Состояние конкретного купленного экземпляра. Изменяется та же запись предмета, которую использует установленная деталь.", 1);
        group.Boolean("isBroken", Condition, "Покупная деталь сломана", true,
            "Флаг принадлежит тому же экземпляру предмета. Процент состояния и флаг поломки — отдельные сохранённые значения.");
    }

    private static bool HasMechanicalCondition(string id) => id is
        "vehicle.satsuma.part.piston1" or "vehicle.satsuma.part.piston2" or
        "vehicle.satsuma.part.piston3" or "vehicle.satsuma.part.piston4" or
        "vehicle.satsuma.part.crankshaft" or "vehicle.satsuma.part.head-gasket" or
        "vehicle.satsuma.part.water-pump" or "vehicle.satsuma.part.alternator" or
        "vehicle.satsuma.part.rocker-shaft";

    private static string? ExpectedDynamicPart(string item) => item switch
    {
        "item.spark-plug" => "vehicle.satsuma.part.spark-plug",
        "item.alternator-belt" => "vehicle.satsuma.part.alternator-belt",
        "item.oil-filter" => "vehicle.satsuma.part.oilfilter0",
        "item.light-bulb" => "vehicle.satsuma.part.light-bulb",
        _ => null,
    };

    private static string PartName(string id) => id switch
    {
        "vehicle.satsuma.part.piston1" => "Поршень 1",
        "vehicle.satsuma.part.piston2" => "Поршень 2",
        "vehicle.satsuma.part.piston3" => "Поршень 3",
        "vehicle.satsuma.part.piston4" => "Поршень 4",
        "vehicle.satsuma.part.crankshaft" => "Коленвал",
        "vehicle.satsuma.part.head-gasket" => "Прокладка ГБЦ",
        "vehicle.satsuma.part.water-pump" => "Водяная помпа",
        "vehicle.satsuma.part.alternator" => "Генератор",
        "vehicle.satsuma.part.rocker-shaft" => "Ось коромысел",
        "vehicle.satsuma.part.distributor" => "Трамблёр",
        "vehicle.satsuma.part.carburetor" => "Карбюратор",
        "vehicle.satsuma.part.oilfilter0" => "Масляный фильтр",
        "vehicle.satsuma.part.camshaft-gear" => "Шестерня распредвала",
        "vehicle.satsuma.part.steering-rod-fl" => "Левая рулевая тяга",
        "vehicle.satsuma.part.steering-rod-fr" => "Правая рулевая тяга",
        "vehicle.satsuma.part.rocker-cover" => "Клапанная крышка",
        "vehicle.satsuma.part.gt-rocker-cover-gt" => "Клапанная крышка GT",
        "vehicle.satsuma.part.radiator" => "Радиатор",
        "vehicle.satsuma.part.brake-master-cylinder" => "Главный тормозной цилиндр",
        "vehicle.satsuma.part.clutch-master-cylinder" => "Главный цилиндр сцепления",
        "vehicle.satsuma.part.spark-plug" => "Свеча зажигания",
        "vehicle.satsuma.part.alternator-belt" => "Ремень генератора",
        "vehicle.satsuma.part.light-bulb" => "Лампочка фары",
        _ => string.IsNullOrWhiteSpace(id) ? "Деталь без типа" : id,
    };

    private static string WithIdentity(string name, string id) =>
        name + " · " + (string.IsNullOrWhiteSpace(id) ? "без ID" : id.Length <= 12 ? id : id[..6] + "…" + id[^6..]);
    private static string Text(JsonNode? node) => node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : "";
    private static bool IsTrue(JsonNode? node) => node?.GetValueKind() == JsonValueKind.True;
    private static bool Version(JsonObject node, int version) => ReadNumber(node["schemaVersion"]) == version;
    private static JsonObject? Active(JsonObject owner, string flag, string field) =>
        IsTrue(owner[flag]) && owner[field] is JsonObject dto && Version(dto, 1) ? dto : null;
    private static double? ReadNumber(JsonNode? node) => node?.GetValueKind() == JsonValueKind.Number &&
        double.TryParse(node.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
        double.IsFinite(value) ? value : null;

    private sealed class FieldGroup(List<VehicleTuningField> fields, JsonObject data,
        string path, string owner, string domain = VehicleDomain)
    {
        public JsonObject Data { get; } = data;
        public FieldGroup Child(JsonObject state, string key) => new(fields, state, path + "/" + key, owner, domain);
        public void Number(string key, string category, string label, string unit, double? min,
            double? max, double? recommended, bool editable, string help, double increment = 0.1) =>
            NumberAt(Data[key], key, category, label, unit, min, max, recommended, editable, help, increment);
        public void NumberAt(JsonNode? value, string relativePath, string category, string label,
            string unit, double? min, double? max, double? recommended, bool editable, string help,
            double increment = 0.1)
        {
            if (ReadNumber(value) is null) return;
            fields.Add(new VehicleTuningField(category, label, domain, path + "/" + relativePath,
                unit, min, max, recommended, editable, help, false, increment, owner));
        }
        public void Boolean(string key, string category, string label, bool editable, string help)
        {
            if (Data[key]?.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False)) return;
            fields.Add(new VehicleTuningField(category, label, domain, path + "/" + key,
                "", null, null, null, editable, help, true, 1, owner));
        }
    }
}
