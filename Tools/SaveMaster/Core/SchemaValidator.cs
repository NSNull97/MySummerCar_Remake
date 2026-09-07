using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;

namespace MySummerRemake.SaveMaster.Core;

/// <summary>Offline checks for project-owned DTO invariants. Runtime catalog and scene checks remain in Unity.</summary>
public static partial class SaveSchemaValidator
{
    public static IReadOnlyList<ValidationIssue> ValidateTimeTransition(JsonNode original, JsonNode edited)
    {
        if (JsonNode.DeepEquals(original, edited)) return Array.Empty<ValidationIssue>();
        var c = new CheckContext("core.time");
        try
        {
            static (long EpochMicroseconds, long EpochDay) Epoch(JsonNode node)
            {
                JsonObject o = node.AsObject();
                DateTime date = new(Integer(o, "year"), Integer(o, "month"), Integer(o, "day"));
                return (checked(date.Ticks / 10 + Long(o, "timeOfDayTicks") - Long(o, "elapsedGameTicks")), checked(date.Ticks / TimeSpan.TicksPerDay - Long(o, "dayIndex")));
            }
            if (Epoch(original) != Epoch(edited)) c.Error("", "Календарь и elapsedGameTicks не согласованы. Используйте действие изменения даты и времени.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        { c.Error("", "Нельзя проверить календарь: " + exception.Message); }
        return c.Issues;
    }

    public static IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<SaveDomain> domains)
    {
        var issues = new List<ValidationIssue>();
        foreach (SaveDomain domain in domains)
        {
            if (!SchemaCatalog.IsSupported(domain.Id, domain.SchemaVersion))
            {
                issues.Add(new(ValidationSeverity.Warning, domain.Id, "", "Неизвестный раздел или версия: доступен просмотр, исходные данные сохраняются без изменений."));
                continue;
            }
            issues.AddRange(ValidatePayload(domain.Id, domain.Payload));
        }
        issues.AddRange(ValidateDynamicOwnership(domains));
        return issues;
    }

    public static IReadOnlyList<ValidationIssue> ValidatePayload(string domainId, JsonNode payload)
    {
        var context = new CheckContext(domainId);
        try
        {
            if (payload is not JsonObject root) { context.Error("", "Раздел должен быть JSON-объектом."); return context.Issues; }
            if (!SchemaCatalog.IsSupported(domainId, Integer(root, "schemaVersion"))) context.Error("/schemaVersion", "Версия содержимого раздела не поддерживается редактором.");
            Walk(context, root, "");
            switch (domainId)
            {
                case "vehicle.satsuma": ValidateVehicles(context, root); break;
                case "economy.player": ValidateEconomy(context, root); break;
                case "items.instances": ValidateItems(context, root); break;
                case "core.time": ValidateTime(context, root); break;
                case "world.entities": ValidateUniqueArray(context, root["entities"], "/entities", "stableEntityId", 100000); break;
                case "lighting.electrical-grid":
                    foreach (string field in new[] { "sources", "circuits", "switches" }) ValidateUniqueArray(context, root[field], "/" + field, "id", 100000);
                    break;
                case "player.needs":
                    if (Number(root, "weightKilograms") <= 0) context.Error("/weightKilograms", "Вес должен быть строго больше нуля.");
                    break;
                case "player.state":
                    if (root["motor"] is JsonObject motor) CheckVersion(context, motor, "/motor", 1);
                    else context.Error("/motor", "Отсутствует состояние движения игрока.");
                    if (root["look"] is JsonObject look) CheckVersion(context, look, "/look", 1);
                    else context.Error("/look", "Отсутствует состояние камеры игрока.");
                    break;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or OverflowException or InvalidCastException or ArgumentException)
        {
            context.Error("", "Тип или структура поля не соответствует схеме: " + exception.Message);
        }
        return context.Issues;
    }

    private static void Walk(CheckContext c, JsonNode? node, string path)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("x") && obj.ContainsKey("y") && obj.ContainsKey("z"))
            {
                foreach (string component in obj.ContainsKey("w") ? new[] { "x", "y", "z", "w" } : new[] { "x", "y", "z" })
                    if (Math.Abs(Number(obj, component)) > float.MaxValue) c.Error(path + "/" + component, "Компонента вектора или поворота превышает диапазон float.");
                double magnitude = Number(obj, "x") * Number(obj, "x") + Number(obj, "y") * Number(obj, "y") + Number(obj, "z") * Number(obj, "z");
                if (obj.ContainsKey("w"))
                {
                    magnitude += Number(obj, "w") * Number(obj, "w");
                    if (!(magnitude > 0.000001 && magnitude < 1000000)) c.Error(path, "Некорректный кватернион: нулевой или чрезмерный поворот.");
                }
                else if (path.EndsWith("/linearVelocity", StringComparison.Ordinal) && magnitude > 250000) c.Error(path, "Скорость превышает допустимые 500 м/с.");
                else if (path.EndsWith("/angularVelocity", StringComparison.Ordinal) && magnitude > 40000) c.Error(path, "Угловая скорость превышает допустимые 200 рад/с.");
            }
            foreach (var pair in obj)
            {
                // JsonUtility may emit a default inline DTO despite a false presence bit.
                if (pair.Key is "steeringAlignment" or "camshaftTiming" or "engineAdjustment" or "engineDocking" or
                    "mechanicalCondition" or "valveAdjustment" or "serviceCaps" or "satsumaOperatingState")
                {
                    string bit = "has" + char.ToUpperInvariant(pair.Key[0]) + pair.Key[1..];
                    if (!Bool(obj, bit)) continue;
                }
                Walk(c, pair.Value, path + "/" + Escape(pair.Key));
            }
        }
        else if (node is JsonArray array)
        {
            for (int i = 0; i < array.Count; i++) Walk(c, array[i], path + "/" + i);
        }
        else if (node is JsonValue value && value.GetValueKind() == JsonValueKind.Number)
        {
            double number = ReadDouble(value);
            if (!double.IsFinite(number)) { c.Error(path, "Число должно быть конечным."); return; }
            if (SchemaCatalog.IsInteger(c.DomainId, path) && number != Math.Truncate(number)) c.Error(path, "Поле должно содержать целое число.");
            FieldMetadata metadata = SchemaCatalog.Describe(c.DomainId, path);
            if (metadata.Min.HasValue && number < metadata.Min.Value || metadata.Max.HasValue && number > metadata.Max.Value)
                c.Error(path, "Значение вне допустимого диапазона " + metadata.Min + "…" + metadata.Max + ".");
            if (metadata.Choices != null && !metadata.Choices.ContainsKey(value.ToJsonString())) c.Error(path, "Неизвестное значение перечисления.");
        }
    }

    // Audited against VehicleItemPartCatalog and VehicleItemSaveRestorePlanFactory.
    // This checks ownership only; offline editing never migrates or creates a descriptor.
    private static IReadOnlyList<ValidationIssue> ValidateDynamicOwnership(IReadOnlyList<SaveDomain> domains)
    {
        var c = new CheckContext("vehicle.satsuma");
        try
        {
            SaveDomain? vehicleDomain = domains.FirstOrDefault(domain => domain.Id == c.DomainId);
            if (vehicleDomain is null || !SchemaCatalog.IsSupported(vehicleDomain.Id, vehicleDomain.SchemaVersion) ||
                vehicleDomain.Payload is not JsonObject vehicleRoot || Integer(vehicleRoot, "schemaVersion") != 1 ||
                vehicleRoot["vehicles"] is not JsonArray vehicles) return c.Issues;
            var assemblies = new List<(JsonObject Value, string Path)>();
            for (int i = 0; i < vehicles.Count; i++)
                if (vehicles[i] is JsonObject vehicle && vehicle["assembly"] is JsonObject assembly && Integer(assembly, "schemaVersion") is 1 or 2 or 3)
                    assemblies.Add((assembly, "/vehicles/" + i + "/assembly"));
            bool hasDescriptors = assemblies.Any(entry => entry.Value["dynamicParts"] is JsonArray { Count: > 0 });
            bool allCurrent = assemblies.Count > 0 && assemblies.Count == vehicles.Count && assemblies.All(entry => Integer(entry.Value, "schemaVersion") == 3);
            if (!hasDescriptors && !allCurrent) return c.Issues;

            SaveDomain? itemDomain = domains.FirstOrDefault(domain => domain.Id == "items.instances");
            SaveDomain? worldDomain = domains.FirstOrDefault(domain => domain.Id == "world.entities");
            bool itemsSupported = itemDomain is not null && SchemaCatalog.IsSupported(itemDomain.Id, itemDomain.SchemaVersion) &&
                itemDomain.Payload is JsonObject itemRoot && Integer(itemRoot, "schemaVersion") == 1;
            bool worldSupported = worldDomain is not null && SchemaCatalog.IsSupported(worldDomain.Id, worldDomain.SchemaVersion) &&
                worldDomain.Payload is JsonObject worldRoot && SchemaCatalog.IsSupported(worldDomain.Id, Integer(worldRoot, "schemaVersion"));
            if (hasDescriptors && (!itemsSupported || !worldSupported))
            { c.Error("/vehicles", "Для проверки владельцев динамических деталей нужны поддержанные разделы предметов и объектов мира."); return c.Issues; }

            var itemById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            if (itemsSupported && itemDomain!.Payload["instances"] is JsonArray instances)
                foreach (JsonNode? node in instances)
                    if (node is JsonObject record && record["state"] is JsonObject state) itemById.TryAdd(Text(state, "stableEntityId"), record);
            var worldIds = new HashSet<string>(StringComparer.Ordinal);
            if (worldSupported && worldDomain!.Payload["entities"] is JsonArray entities)
                foreach (JsonNode? node in entities)
                    if (node is JsonObject entity) worldIds.Add(Text(entity, "stableEntityId"));
            var baseIds = new HashSet<string>(StringComparer.Ordinal);
            foreach ((JsonObject assembly, string path) in assemblies)
                if (assembly["parts"] is JsonArray parts)
                    for (int i = 0; i < parts.Count; i++)
                        if (parts[i] is JsonObject part && !baseIds.Add(Text(part, "stableEntityId")))
                            c.Error(path + "/parts/" + i, "Базовая деталь записана в нескольких сборках.");

            var owners = new HashSet<string>(StringComparer.Ordinal);
            foreach ((JsonObject assembly, string path) in assemblies)
            {
                if (assembly["dynamicParts"] is not JsonArray descriptors) continue;
                for (int i = 0; i < descriptors.Count; i++)
                {
                    if (descriptors[i] is not JsonObject descriptor || descriptor["part"] is not JsonObject part) continue;
                    string id = Text(part, "stableEntityId"), p = path + "/dynamicParts/" + i;
                    if (baseIds.Contains(id) || !owners.Add(id)) c.Error(p + "/part/stableEntityId", "Деталь имеет несколько владельцев в сборках.");
                    if (!itemById.TryGetValue(id, out JsonObject? item) || item["state"] is not JsonObject state)
                        c.Error(p, "Динамическая деталь отсутствует в разделе предметов.");
                    else if (Bool(state, "isConsumed") || Bool(item, "isCanonicalPlacement") || Text(state, "definitionId") != Text(descriptor, "itemDefinitionId"))
                        c.Error(p, "Предмет динамической детали израсходован, имеет каноническое размещение или другой тип.");
                    if (worldIds.Contains(id)) c.Error(p + "/part/stableEntityId", "Динамическая деталь одновременно принадлежит физике сборки и разделу объектов мира.");
                }
            }
            // Older assemblies are allowed to retain the game's definition-aware migration path.
            if (allCurrent)
                foreach (var pair in itemById)
                {
                    JsonObject state = pair.Value["state"]!.AsObject();
                    if (!Bool(state, "isConsumed") && DynamicPartDefinition(Text(state, "definitionId")).Length != 0 && !owners.Contains(pair.Key))
                        c.Error("/vehicles", "У материализованного сборочного предмета отсутствует динамическая запись: " + pair.Key + ".");
                }
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or OverflowException or InvalidCastException or ArgumentException)
        { c.Error("/vehicles", "Не удалось проверить владельцев динамических деталей: " + exception.Message); }
        return c.Issues;
    }

    private static string DynamicPartDefinition(string itemId) => itemId switch
    {
        "item.spark-plug" => "vehicle.satsuma.part.spark-plug",
        "item.alternator-belt" => "vehicle.satsuma.part.alternator-belt",
        "item.oil-filter" => "vehicle.satsuma.part.oilfilter0",
        "item.light-bulb" => "vehicle.satsuma.part.light-bulb",
        _ => "",
    };

    private static void RequireVector(CheckContext c, JsonObject owner, string field, string path, bool rotation)
    {
        string[] components = rotation ? ["x", "y", "z", "w"] : ["x", "y", "z"];
        if (owner[field] is not JsonObject vector || components.Any(component => vector[component]?.GetValueKind() != JsonValueKind.Number))
            c.Error(path + "/" + field, "Отсутствует полный числовой вектор или поворот.");
    }

    private static void ValidateVehicles(CheckContext c, JsonObject root)
    {
        CheckVersion(c, root, "", 1);
        ValidateUniqueArray(c, root["vehicles"], "/vehicles", "stableVehicleId", 64);
        if (root["vehicles"] is not JsonArray vehicles) return;
        for (int index = 0; index < vehicles.Count; index++)
        {
            if (vehicles[index] is not JsonObject vehicle) continue;
            string p = "/vehicles/" + index;
            CheckVersion(c, vehicle, p, 1);
            if (vehicle["assembly"] is JsonObject assembly) ValidateAssembly(c, assembly, p + "/assembly");
            else c.Error(p + "/assembly", "Отсутствует сборка машины.");
            if (vehicle["simulation"] is JsonObject simulation)
            {
                CheckVersion(c, simulation, p + "/simulation", 1);
                ValidateVehicleOperatingExtension(c, simulation, p + "/simulation");
                Range(c, simulation, "engineStatus", p + "/simulation", 0, 3, true);
                Range(c, simulation, "shiftStatus", p + "/simulation", 0, 2, true);
                // Audited M06_VehicleSimulationConfig.asset has five forward ratios.
                // GearboxSimulationConfig.TryGetRatio also accepts reverse -1 and neutral 0.
                Range(c, simulation, "selectedGear", p + "/simulation", -1, 5, true);
                foreach (string field in new[] { "elapsedSeconds", "engineRpm", "stallTimerSeconds", "invalidShiftCount", "batteryVoltage", "fuelLiters", "oilLiters", "coolantLiters" }) Range(c, simulation, field, p + "/simulation", 0, double.MaxValue);
                if (simulation["wheels"] is JsonArray wheels && wheels.Count > 32) c.Error(p + "/simulation/wheels", "Слишком много колёс: максимум 32.");
                if (simulation["wheels"] is JsonArray wheelStates)
                    for (int wi = 0; wi < wheelStates.Count; wi++) if (wheelStates[wi] is JsonObject wheel) Range(c, wheel, "Surface", p + "/simulation/wheels/" + wi, 0, 5, true);
            }
            else c.Error(p + "/simulation", "Отсутствует симуляция машины.");
            if (vehicle["physics"] is not JsonObject) c.Error(p + "/physics", "Отсутствует физическое состояние машины.");
            if (vehicle["electrical"] is JsonObject electrical) ValidateElectrical(c, electrical, p + "/electrical");
            if (vehicle["wipers"] is JsonObject wipers)
            {
                CheckVersion(c, wipers, p + "/wipers", 1);
                Range(c, wipers, "mode", p + "/wipers", 0, 2, true);
                Range(c, wipers, "cycleTimeSeconds", p + "/wipers", 0, 1);
            }
            if (vehicle["handbrake"] is JsonObject handbrake)
            {
                CheckVersion(c, handbrake, p + "/handbrake", 1);
                Range(c, handbrake, "positionDegrees", p + "/handbrake", 0, 20);
            }
        }
    }

    private static void ValidateAssembly(CheckContext c, JsonObject a, string path)
    {
        int schema = Integer(a, "schemaVersion");
        if (schema is not (1 or 2 or 3)) c.Error(path + "/schemaVersion", "Неизвестная версия сборки.");
        ValidateUniqueArray(c, a["parts"], path + "/parts", "stableEntityId", 2048);
        ValidateUniqueArray(c, a["mounts"], path + "/mounts", "mountId", 2048);
        if (a["parts"] is not JsonArray parts || a["mounts"] is not JsonArray mounts || a["fasteners"] is not JsonArray bolts)
        { c.Error(path, "Сборка должна содержать parts, mounts и fasteners."); return; }
        if (bolts.Count > 8192) c.Error(path + "/fasteners", "Превышен лимит 8192 крепежей.");
        var occupancy = new Dictionary<string, string>(StringComparer.Ordinal);
        var mountById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (JsonNode? node in mounts) if (node is JsonObject m) mountById.TryAdd(Text(m, "mountId"), m);
        var allParts = new List<(JsonObject Part, string Path)>();
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] is JsonObject part) allParts.Add((part, path + "/parts/" + i));
        }
        var identities = new HashSet<string>(allParts.Select(entry => Text(entry.Part, "stableEntityId")), StringComparer.Ordinal);
        if (a["dynamicParts"] is JsonArray dynamicParts)
        {
            if (dynamicParts.Count > 4096) c.Error(path + "/dynamicParts", "Превышен лимит 4096 динамических деталей.");
            for (int i = 0; i < dynamicParts.Count; i++)
            {
                string p = path + "/dynamicParts/" + i;
                if (dynamicParts[i] is not JsonObject descriptor || descriptor["part"] is not JsonObject part)
                { c.Error(p, "Отсутствует описание динамической детали."); continue; }
                string id = Text(part, "stableEntityId"), itemId = Text(descriptor, "itemDefinitionId");
                if (!Guid.TryParseExact(id, "N", out Guid parsed) || parsed.ToString("N") != id || !identities.Add(id))
                    c.Error(p + "/part/stableEntityId", "Неверная или повторная идентичность динамической детали.");
                string expectedDefinition = DynamicPartDefinition(itemId);
                if (expectedDefinition.Length == 0 || Text(part, "partDefinitionId") != expectedDefinition)
                    c.Error(p + "/itemDefinitionId", "Нет проверенного соответствия предмета и детали сборки.");
                if (Integer(part, "lifecycleState") is not (0 or 1))
                    c.Error(p + "/part/lifecycleState", "Динамическая деталь может быть только свободной или установленной.");
                if (Bool(part, "hasSteeringAlignment") || Bool(part, "hasCamshaftTiming") || Bool(part, "hasEngineDocking") ||
                    Bool(part, "hasMechanicalCondition") || Bool(part, "hasValveAdjustment") || Bool(part, "hasServiceCaps"))
                    c.Error(p + "/part", "Динамический расходник не поддерживает отдельный износ сборки, клапаны, крышки, схождение, метку распредвала или наживление двигателя.");
                if (Bool(part, "hasEngineAdjustment") && itemId != "item.oil-filter")
                    c.Error(p + "/part/engineAdjustment", "Регулировка динамической детали разрешена только масляному фильтру.");
                RequireVector(c, part, "worldPosition", p + "/part", false);
                RequireVector(c, part, "worldRotation", p + "/part", true);
                RequireVector(c, descriptor, "linearVelocity", p, false);
                RequireVector(c, descriptor, "angularVelocity", p, false);
                if (descriptor["sleeping"]?.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
                    c.Error(p + "/sleeping", "Отсутствует логическое состояние сна физики.");
                allParts.Add((part, p + "/part"));
            }
        }
        else if (schema == 3 || a["dynamicParts"] is not null)
            c.Error(path + "/dynamicParts", "Схема 3 требует массив динамических деталей.");
        foreach ((JsonObject part, string p) in allParts)
        {
            int state = Integer(part, "lifecycleState");
            string mount = Text(part, "installedMountId"), id = Text(part, "stableEntityId"), definition = Text(part, "partDefinitionId");
            if (state < 0 || state > 2) c.Error(p + "/lifecycleState", "Допустимы состояния 0, 1, 2.");
            if (state != 1 && mount.Length != 0) c.Error(p + "/installedMountId", "Свободная деталь и основа сборки не должны занимать mount.");
            if (state == 1)
            {
                if (mount.Length == 0 || !mountById.ContainsKey(mount)) c.Error(p + "/installedMountId", "Место установки отсутствует в сборке.");
                if (!occupancy.TryAdd(mount, id)) c.Error(p + "/installedMountId", "Одно место установки занято несколькими деталями.");
                if (SchemaVehicleDefinitions.Mounts.TryGetValue(mount, out MountCatalogEntry? catalog) && !catalog.AcceptedParts.Contains(definition, StringComparer.Ordinal)) c.Error(p, "Тип детали несовместим с этим местом установки.");
            }
            ValidatePartExtensions(c, part, p, state, definition);
        }
        for (int i = 0; i < mounts.Count; i++)
        {
            if (mounts[i] is not JsonObject mount) continue;
            string id = Text(mount, "mountId");
            if (Text(mount, "installedPartStableEntityId") != occupancy.GetValueOrDefault(id, "")) c.Error(path + "/mounts/" + i, "Записи детали и места установки противоречат друг другу.");
        }
        var savedBolts = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        int unknownBolts = 0;
        for (int i = 0; i < bolts.Count; i++)
        {
            string p = path + "/fasteners/" + i;
            if (bolts[i] is not JsonObject bolt) { c.Error(p, "Пустая запись крепежа."); continue; }
            string mount = Text(bolt, "mountId"), definition = Text(bolt, "fastenerDefinitionId");
            int stage = Integer(bolt, "stage"); bool inserted = Bool(bolt, "inserted"), seated = Bool(bolt, "seated");
            if (!savedBolts.TryAdd(mount + "/" + definition, bolt)) c.Error(p, "Повтор крепежа в одном mount.");
            if (!mountById.ContainsKey(mount)) c.Error(p, "Крепёж ссылается на отсутствующее место установки.");
            if (stage < 0 || !inserted && (seated || stage != 0) || inserted && !seated && stage != 0 || inserted && !occupancy.ContainsKey(mount)) c.Error(p, "Затяжка, вставка, наживление и наличие детали не согласованы.");
            if (SchemaVehicleDefinitions.Fasteners.TryGetValue(definition, out FastenerCatalogEntry? known))
            { if (stage > known.MaximumStage) c.Error(p + "/stage", "Максимум этого крепежа — " + known.MaximumStage + "."); }
            else unknownBolts++;
            if (SchemaVehicleDefinitions.Mounts.TryGetValue(mount, out MountCatalogEntry? knownMount) && !knownMount.Fasteners.Contains(definition, StringComparer.Ordinal))
                c.Warning(p, "Крепёж отсутствует в текущем каталоге этого mount; старое сохранение может требовать миграцию в игре.");
        }
        if (unknownBolts != 0) c.Warning(path + "/fasteners", $"Для {unknownBolts} крепежей неизвестен точный максимум. Массовые действия с ними недоступны.");
        if (a["fastenerGroups"] is not JsonArray groups)
        {
            if (schema is 2 or 3) c.Error(path + "/fastenerGroups", "Отсутствуют группы крепежа схемы 2/3.");
            return;
        }
        ValidateUniqueArray(c, groups, path + "/fastenerGroups", "mountId", 2048);
        if (schema is 2 or 3 && groups.Count != mounts.Count) c.Error(path + "/fastenerGroups", "Для каждого mount требуется одна группа крепежа.");
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] is not JsonObject group) continue;
            string mount = Text(group, "mountId"), p = path + "/fastenerGroups/" + i;
            bool bolted = Bool(group, "isBolted"), occupied = occupancy.ContainsKey(mount);
            if (!mountById.ContainsKey(mount)) { c.Error(p, "Группа ссылается на отсутствующий mount."); continue; }
            if (!occupied && bolted) c.Error(p + "/isBolted", "Пустое место установки не может быть затянуто.");
            if (!SchemaVehicleDefinitions.Mounts.TryGetValue(mount, out MountCatalogEntry? definition)) { c.Warning(p, "Пороги группы отсутствуют в каталоге; необходима проверка в игре."); continue; }
            int total = 0; bool complete = true;
            foreach (string boltId in definition.GroupIds)
            {
                if (!savedBolts.TryGetValue(mount + "/" + boltId, out JsonObject? bolt)) { complete = false; break; }
                total += Integer(bolt, "stage");
            }
            if (!complete) { c.Warning(p, "Группа отличается от текущего каталога; требуется миграция в игре до массовой затяжки."); continue; }
            total = Math.Clamp(total, 0, definition.MaximumTightness);
            bool consistent = !occupied || definition.GroupIds.Length == 0 ? !bolted : bolted ? total > definition.OffThreshold : total < definition.OnThreshold;
            if (!consistent) c.Error(p + "/isBolted", $"Защёлка группы не согласована с затяжкой {total}; пороги вкл/выкл {definition.OnThreshold}/{definition.OffThreshold}.");
        }
    }

    private static void ValidatePartExtensions(CheckContext c, JsonObject part, string p, int state, string definition)
    {
        ValidatePartTuningExtensions(c, part, p, definition);
        if (Bool(part, "hasCamshaftTiming"))
        {
            if (part["camshaftTiming"] is not JsonObject timing || definition != "vehicle.satsuma.part.camshaft-gear") c.Error(p + "/camshaftTiming", "Метка допустима только у шестерни распредвала.");
            else { CheckVersion(c, timing, p + "/camshaftTiming", 1); Range(c, timing, "angleDegrees", p + "/camshaftTiming", 0, 360); }
        }
        if (Bool(part, "hasSteeringAlignment"))
        {
            if (part["steeringAlignment"] is not JsonObject alignment) c.Error(p + "/steeringAlignment", "Отсутствует регулировка схождения.");
            else { CheckVersion(c, alignment, p + "/steeringAlignment", 1); Range(c, alignment, "alignmentDegrees", p + "/steeringAlignment", -6, 6); }
        }
        if (Bool(part, "hasEngineAdjustment"))
        {
            if (part["engineAdjustment"] is not JsonObject adjustment) c.Error(p + "/engineAdjustment", "Отсутствует регулировка двигателя.");
            else
            {
                CheckVersion(c, adjustment, p + "/engineAdjustment", 1);
                int kind = Integer(adjustment, "kind");
                string[] expected = ["", "vehicle.satsuma.part.alternator", "vehicle.satsuma.part.distributor", "vehicle.satsuma.part.carburetor", "vehicle.satsuma.part.oilfilter0"];
                if (kind < 1 || kind > 4 || expected[kind] != definition) c.Error(p + "/engineAdjustment/kind", "Регулировка не принадлежит этой детали.");
                else Range(c, adjustment, "value", p + "/engineAdjustment", kind == 3 ? 10 : 0, kind switch { 1 or 4 => 8, 2 => 20, _ => 22 }, kind == 4);
                if (kind == 4 && state != 1 && Number(adjustment, "value") != 0) c.Error(p + "/engineAdjustment/value", "Снятый масляный фильтр должен иметь нулевую затяжку.");
            }
        }
        if (Bool(part, "hasEngineDocking"))
        {
            if (part["engineDocking"] is not JsonObject docking || docking["pendingStages"] is not JsonArray stages || stages.Count != 3 || definition != "vehicle.satsuma.part.engine-block") c.Error(p + "/engineDocking", "Наживление допустимо только для блока двигателя с тремя креплениями.");
            else
            {
                CheckVersion(c, docking, p + "/engineDocking", 1);
                int total = 0;
                foreach (JsonNode? stage in stages) { int value = stage!.GetValue<int>(); if (value is < 0 or > 1) c.Error(p + "/engineDocking/pendingStages", "Допустимы только 0 и 1."); total += value; }
                if (total > 1 || state != 0 && total != 0) c.Error(p + "/engineDocking", "У свободного двигателя допустим один наживлённый болт; у установленного — ни одного pending.");
            }
        }
    }

    private static void ValidateElectrical(CheckContext c, JsonObject e, string path)
    {
        int schema = Integer(e, "schemaVersion");
        if (schema is not (1 or 2)) { c.Error(path + "/schemaVersion", "Неизвестная версия проводки."); return; }
        foreach (string key in new[] { "batteryPlusStage", "batteryMinusStage", "starterCableStage" }) Range(c, e, key, path, 0, 8, true);
        if (schema == 1)
        {
            if (!Bool(e, "batteryPlusInstalled") && Integer(e, "batteryPlusStage") != 0 || !Bool(e, "batteryMinusInstalled") && Integer(e, "batteryMinusStage") != 0 || Integer(e, "starterCableStage") != 0)
                c.Error(path, "Старая схема проводки имеет затяжку без установленной клеммы.");
            return;
        }
        var selected = new HashSet<string>(StringComparer.Ordinal);
        if (e["installedConnectionIds"] is JsonArray connections)
        {
            for (int i = 0; i < connections.Count; i++)
            {
                string? value = connections[i]?.GetValue<string>();
                if (value == null || !SchemaCatalog.WireConnections.ContainsKey(value) || !selected.Add(value)) c.Error(path + "/installedConnectionIds/" + i, "Неизвестное или повторное подключение проводки.");
            }
        }
        if (!selected.Contains("BatteryHarness") && !selected.Contains("Starter") && Integer(e, "batteryPlusStage") != 0) c.Error(path + "/batteryPlusStage", "Для плюсовой клеммы нужен BatteryHarness или Starter.");
        if (!selected.Contains("GroundBattery") && Integer(e, "batteryMinusStage") != 0) c.Error(path + "/batteryMinusStage", "Для минусовой клеммы нужен GroundBattery.");
        if (!selected.Contains("Starter") && Integer(e, "starterCableStage") != 0) c.Error(path + "/starterCableStage", "Для крепежа стартера нужен кабель Starter.");
    }

    private static void ValidateEconomy(CheckContext c, JsonObject root)
    {
        _ = Long(root, "balanceMinorUnits"); // Exact signed 64-bit conversion; double bounds round near Int64.MaxValue.
        _ = Long(root, "nextSequence");
        Range(c, root, "balanceMinorUnits", "", 0, long.MaxValue, true);
        Range(c, root, "nextSequence", "", 1, long.MaxValue, true);
        if (root["ledger"] is not JsonArray ledger) return;
        long previousSequence = 0, previousBalance = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < ledger.Count; i++)
        {
            if (ledger[i] is not JsonObject record) { c.Error("/ledger/" + i, "Пустая операция."); continue; }
            long sequence = Long(record, "sequence"), before = Long(record, "balanceBeforeMinorUnits"), after = Long(record, "balanceAfterMinorUnits"), amount = Long(record, "amountMinorUnits");
            int direction = Integer(record, "direction");
            Range(c, record, "kind", "/ledger/" + i, 0, 6, true);
            if (sequence <= previousSequence || amount <= 0 || before < 0 || after < 0 || !ids.Add(Text(record, "transactionId"))) c.Error("/ledger/" + i, "Сумма, баланс, порядок или идентичность операции некорректны.");
            // EconomyTransactionDirection: Debit=0, Credit=1.
            if (direction is < 0 or > 1 || checked(direction == 0 ? before - amount : before + amount) != after || i > 0 && before != previousBalance) c.Error("/ledger/" + i, "Нарушена цепочка денежных операций.");
            previousSequence = sequence; previousBalance = after;
        }
        if (ledger.Count > 0 && Long(root, "balanceMinorUnits") != previousBalance) c.Error("/balanceMinorUnits", "Баланс должен совпадать с последней операцией. Нельзя изменять только это число.");
        if (Long(root, "nextSequence") <= previousSequence) c.Error("/nextSequence", "Следующий номер операции должен быть больше записанных.");
    }

    private static void ValidateItems(CheckContext c, JsonObject root)
    {
        if (root["instances"] is not JsonArray records) { c.Error("/instances", "Отсутствуют экземпляры предметов."); return; }
        if (records.Count > 4096) c.Error("/instances", "Превышен лимит 4096 предметов.");
        var allIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < records.Count; i++)
        {
            string p = "/instances/" + i + "/state";
            if (records[i] is not JsonObject record || record["state"] is not JsonObject state) { c.Error(p, "Отсутствует состояние предмета."); continue; }
            CheckVersion(c, state, p, 1);
            if (!allIds.Add(Text(state, "stableEntityId"))) c.Error(p, "Повторная идентичность предмета.");
            Range(c, state, "condition", p, 0, 100);
            Range(c, state, "content", p, -0.0001, double.MaxValue);
            Range(c, state, "cookingSeconds", p, 0, double.MaxValue);
            Range(c, state, "lastFoodSimulationGameSeconds", p, 0, double.MaxValue);
            Range(c, state, "variantIndex", p, 0, int.MaxValue, true);
            if (!SchemaItemDefinitions.Items.TryGetValue(Text(state, "definitionId"), out ItemCatalogEntry? definition)) { c.Warning(p, "Тип предмета отсутствует в каталоге редактора; нужны проверки игры."); continue; }
            Range(c, state, "content", p, -0.0001, definition.MaximumContent + 0.0001);
            Range(c, state, "variantIndex", p, 0, definition.VariantCount - 1, true);
            double content = Number(state, "content");
            if (state["containedStableIds"] is JsonArray children)
            {
                if (children.Count > 256) c.Error(p + "/containedStableIds", "Превышен лимит 256 дочерних предметов.");
                if (definition.HasChildren && Math.Abs(content - children.Count) > 0.0001) c.Error(p + "/content", "Количество в контейнере не совпадает с дочерними предметами.");
            }
            if (definition.SupportsLiquidTransfer && (content > 0.0001 ? string.IsNullOrWhiteSpace(Text(state, "liquidId")) : !string.IsNullOrEmpty(Text(state, "liquidId")))) c.Error(p + "/content", "Количество жидкости и её ID не согласованы.");
            if (state["scalarStates"] is JsonArray scalars)
            {
                var scalarIds = new HashSet<string>(StringComparer.Ordinal);
                for (int j = 0; j < scalars.Count; j++)
                {
                    if (scalars[j] is not JsonObject scalar) { c.Error(p + "/scalarStates/" + j, "Пустое скалярное состояние."); continue; }
                    string id = Text(scalar, "stateId");
                    if (!scalarIds.Add(id)) c.Error(p + "/scalarStates/" + j, "Повтор состояния предмета.");
                    if (definition.Scalars.TryGetValue(id, out ItemScalarRange? range)) Range(c, scalar, "value", p + "/scalarStates/" + j, range.Minimum, range.Maximum);
                }
            }
        }
        for (int i = 0; i < records.Count; i++)
            if (records[i]?["state"]?["containedStableIds"] is JsonArray children)
                foreach (JsonNode? child in children)
                    if (child is null || !allIds.Add(child.GetValue<string>())) c.Error("/instances/" + i + "/state/containedStableIds", "Дочерний предмет записан дважды или одновременно находится в мире.");
    }

    private static void ValidateTime(CheckContext c, JsonObject root)
    {
        Range(c, root, "elapsedGameTicks", "", 0, long.MaxValue, true);
        Range(c, root, "dayIndex", "", 0, long.MaxValue, true);
        Range(c, root, "timeOfDayTicks", "", 0, 86_400_000_000L - 1, true);
        Range(c, root, "fractionalGameTickRemainder", "", 0, 1);
        if (Number(root, "fractionalGameTickRemainder") >= 1) c.Error("/fractionalGameTickRemainder", "Дробный остаток должен быть меньше 1.");
        Range(c, root, "timeScale", "", 0, 1000);
        if (Number(root, "timeScale") <= 0) c.Error("/timeScale", "Скорость времени должна быть строго положительной.");
        try { _ = new DateTime(Integer(root, "year"), Integer(root, "month"), Integer(root, "day")); }
        catch (ArgumentOutOfRangeException) { c.Error("/day", "Недопустимая календарная дата."); }
    }

    private static void ValidateUniqueArray(CheckContext c, JsonNode? node, string path, string key, int maximum)
    {
        if (node is not JsonArray array) { c.Error(path, "Отсутствует массив записей."); return; }
        if (array.Count > maximum) c.Error(path, "Превышен лимит количества записей: " + maximum + ".");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < array.Count; i++)
            if (array[i] is not JsonObject item || string.IsNullOrWhiteSpace(Text(item, key)) || !ids.Add(Text(item, key))) c.Error(path + "/" + i, "Отсутствующая или повторная идентичность записи.");
    }

    private static void CheckVersion(CheckContext c, JsonObject o, string path, int version)
    { if (Integer(o, "schemaVersion") != version) c.Error(path + "/schemaVersion", "Неизвестная версия данных."); }
    private static void Range(CheckContext c, JsonObject o, string field, string path, double minimum, double maximum, bool integer = false)
    {
        if (!o.ContainsKey(field)) return;
        double value = Number(o, field);
        if (!double.IsFinite(value) || value < minimum || value > maximum || integer && value != Math.Truncate(value)) c.Error(path + "/" + field, $"Допустимый диапазон: {minimum}…{maximum}" + (integer ? ", целое число." : "."));
    }
    internal static string Text(JsonObject o, string key) => o[key]?.GetValue<string>() ?? "";
    internal static int Integer(JsonObject o, string key) => o[key] is JsonNode n ? checked((int)ReadLong(n)) : 0;
    internal static long Long(JsonObject o, string key) => o[key] is JsonNode n ? ReadLong(n) : 0;
    internal static double Number(JsonObject o, string key) => o[key] is JsonNode n ? ReadDouble(n) : 0;
    private static long ReadLong(JsonNode node)
    {
        if (node.GetValueKind() != JsonValueKind.Number) throw new FormatException("Ожидалось целое число.");
        decimal value = decimal.Parse(node.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
        if (value != decimal.Truncate(value)) throw new FormatException("Ожидалось целое число.");
        return checked((long)value);
    }
    private static double ReadDouble(JsonNode node)
    {
        if (node.GetValueKind() != JsonValueKind.Number) throw new FormatException("Ожидалось число.");
        return double.Parse(node.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
    internal static bool Bool(JsonObject o, string key) => o[key]?.GetValue<bool>() ?? false;
    private static string Escape(string name) => name.Replace("~", "~0").Replace("/", "~1");
    private sealed class CheckContext(string domainId)
    {
        public string DomainId { get; } = domainId;
        public List<ValidationIssue> Issues { get; } = [];
        public void Error(string path, string message) => Issues.Add(new(ValidationSeverity.Error, DomainId, path, message));
        public void Warning(string path, string message) => Issues.Add(new(ValidationSeverity.Warning, DomainId, path, message));
    }
}
