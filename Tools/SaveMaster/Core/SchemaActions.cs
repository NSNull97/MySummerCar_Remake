using System.Text.Json.Nodes;

namespace MySummerRemake.SaveMaster.Core;

/// <summary>Each action returns a validated clone; the session owns undo, review and disk writes.</summary>
public static class SchemaActions
{
    public static JsonNode SetFastenerStage(JsonNode vehicleDomain, int vehicleIndex, string mountId, string fastenerId, int stage)
    {
        EnsureValid("vehicle.satsuma", vehicleDomain);
        JsonNode clone = vehicleDomain.DeepClone();
        JsonObject vehicle = Vehicle(clone, vehicleIndex);
        if (vehicle["assembly"] is not JsonObject assembly || SaveSchemaValidator.Integer(assembly, "schemaVersion") is not (2 or 3) ||
            assembly["fasteners"] is not JsonArray fasteners || assembly["fastenerGroups"] is not JsonArray groups || assembly["mounts"] is not JsonArray mounts)
            throw new InvalidDataException("Нужна сборка схемы 2 или 3.");
        if (!SchemaVehicleDefinitions.Fasteners.TryGetValue(fastenerId, out FastenerCatalogEntry? definition) ||
            !SchemaVehicleDefinitions.Mounts.TryGetValue(mountId, out MountCatalogEntry? mountDefinition) || !mountDefinition.Fasteners.Contains(fastenerId, StringComparer.Ordinal))
            throw new InvalidDataException("Точный крепёж или пороги места установки отсутствуют в текущем каталоге.");
        if (stage < 0 || stage > definition.MaximumStage) throw new ArgumentOutOfRangeException(nameof(stage), $"Допустимы ступени от 0 до {definition.MaximumStage}.");
        JsonObject? bolt = fasteners.OfType<JsonObject>().SingleOrDefault(b => SaveSchemaValidator.Text(b, "mountId") == mountId && SaveSchemaValidator.Text(b, "fastenerDefinitionId") == fastenerId);
        JsonObject? mount = mounts.OfType<JsonObject>().SingleOrDefault(m => SaveSchemaValidator.Text(m, "mountId") == mountId);
        if (bolt == null || mount == null || !SaveSchemaValidator.Bool(bolt, "inserted") || SaveSchemaValidator.Text(mount, "installedPartStableEntityId").Length == 0)
            throw new InvalidDataException("Можно затягивать только существующий вставленный крепёж установленной детали.");
        bolt["stage"] = stage;
        if (stage > 0) bolt["seated"] = true;
        JsonObject group = groups.OfType<JsonObject>().SingleOrDefault(g => SaveSchemaValidator.Text(g, "mountId") == mountId)
            ?? throw new InvalidDataException("Группа крепежа отсутствует.");
        int total = 0;
        foreach (string id in mountDefinition.GroupIds)
        {
            JsonObject groupBolt = fasteners.OfType<JsonObject>().SingleOrDefault(b => SaveSchemaValidator.Text(b, "mountId") == mountId && SaveSchemaValidator.Text(b, "fastenerDefinitionId") == id)
                ?? throw new InvalidDataException("Состав группы отличается от текущего каталога. Нужна миграция в игре.");
            total += SaveSchemaValidator.Integer(groupBolt, "stage");
        }
        total = Math.Clamp(total, 0, mountDefinition.MaximumTightness);
        bool bolted = SaveSchemaValidator.Bool(group, "isBolted");
        if (mountDefinition.GroupIds.Length == 0 || total <= mountDefinition.OffThreshold) bolted = false;
        else if (total >= mountDefinition.OnThreshold) bolted = true;
        group["isBolted"] = bolted;
        EnsureValid("vehicle.satsuma", clone);
        return clone;
    }

    public static JsonNode SetFasteners(JsonNode vehicleDomain, int vehicleIndex, bool tighten)
    {
        JsonNode clone = vehicleDomain.DeepClone();
        JsonObject vehicle = Vehicle(clone, vehicleIndex);
        if (vehicle["assembly"] is not JsonObject assembly || SaveSchemaValidator.Integer(assembly, "schemaVersion") is not (2 or 3) ||
            assembly["fasteners"] is not JsonArray fasteners || assembly["fastenerGroups"] is not JsonArray groups || assembly["mounts"] is not JsonArray mounts)
            throw new InvalidDataException("Нужна схема сборки 2 или 3. Сначала загрузите и пересохраните старый слот в игре.");
        EnsureValid("vehicle.satsuma", clone);
        var occupied = mounts.OfType<JsonObject>().Where(m => SaveSchemaValidator.Text(m, "installedPartStableEntityId").Length != 0)
            .Select(m => SaveSchemaValidator.Text(m, "mountId")).ToHashSet(StringComparer.Ordinal);
        var saved = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (JsonObject bolt in fasteners.OfType<JsonObject>())
        {
            string id = SaveSchemaValidator.Text(bolt, "fastenerDefinitionId"), mountId = SaveSchemaValidator.Text(bolt, "mountId");
            saved.Add(mountId + "/" + id, bolt);
            if (!SaveSchemaValidator.Bool(bolt, "inserted") || !occupied.Contains(mountId)) continue;
            if (!SchemaVehicleDefinitions.Fasteners.TryGetValue(id, out FastenerCatalogEntry? definition))
                throw new InvalidDataException("Неизвестен точный предел крепежа: " + id + ". Никаких предположений о затяжке не сделано.");
            if (!SchemaVehicleDefinitions.Mounts.TryGetValue(mountId, out MountCatalogEntry? mount) || !mount.Fasteners.Contains(id, StringComparer.Ordinal))
                throw new InvalidDataException("Крепёж отличается от текущего каталога места установки: " + mountId + ". Сначала пересохраните слот в игре.");
            bolt["stage"] = tighten ? definition.MaximumStage : 0;
            if (tighten) bolt["seated"] = true;
        }
        foreach (JsonObject group in groups.OfType<JsonObject>())
        {
            string mountId = SaveSchemaValidator.Text(group, "mountId");
            if (!SchemaVehicleDefinitions.Mounts.TryGetValue(mountId, out MountCatalogEntry? definition))
                throw new InvalidDataException("Неизвестны точные пороги группы " + mountId + ". Массовое действие отменено.");
            int total = 0;
            foreach (string id in definition.GroupIds)
            {
                if (!saved.TryGetValue(mountId + "/" + id, out JsonObject? bolt)) throw new InvalidDataException("В сохранении отсутствует крепёж группы " + mountId + ". Сначала нужна миграция в игре.");
                total += SaveSchemaValidator.Integer(bolt, "stage");
            }
            total = Math.Clamp(total, 0, definition.MaximumTightness);
            bool bolted = SaveSchemaValidator.Bool(group, "isBolted");
            if (!occupied.Contains(mountId) || definition.GroupIds.Length == 0 || total <= definition.OffThreshold) bolted = false;
            else if (total >= definition.OnThreshold) bolted = true;
            group["isBolted"] = bolted;
        }
        EnsureValid("vehicle.satsuma", clone);
        return clone;
    }

    public static JsonNode SetWiring(JsonNode vehicleDomain, int vehicleIndex, IEnumerable<string> selected, bool tightenTerminals)
    {
        ArgumentNullException.ThrowIfNull(selected);
        JsonNode clone = vehicleDomain.DeepClone();
        JsonObject vehicle = Vehicle(clone, vehicleIndex);
        if (vehicle["electrical"] is not JsonObject electrical || SaveSchemaValidator.Integer(electrical, "schemaVersion") != 2 || electrical["installedConnectionIds"] is not JsonArray)
            throw new InvalidDataException("Для панели проводов нужна существующая проводка схемы 2. Загрузите старый слот в игре и сохраните снова.");
        string[] requested = selected.ToArray();
        var connections = new HashSet<string>(requested, StringComparer.Ordinal);
        if (connections.Count != requested.Length || requested.Any(id => !SchemaCatalog.WireConnections.ContainsKey(id)))
            throw new InvalidDataException("Подключения должны быть известными и неповторяющимися.");
        electrical["installedConnectionIds"] = new JsonArray(SchemaCatalog.WireConnections.Keys.Where(connections.Contains).Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
        SetTerminal("batteryPlusStage", connections.Contains("BatteryHarness") || connections.Contains("Starter"));
        SetTerminal("batteryMinusStage", connections.Contains("GroundBattery"));
        SetTerminal("starterCableStage", connections.Contains("Starter"));
        EnsureValid("vehicle.satsuma", clone);
        return clone;

        void SetTerminal(string field, bool hasCable)
        {
            if (!electrical.ContainsKey(field)) throw new InvalidDataException("В проводке отсутствует обязательное поле " + field + ".");
            if (!hasCable) electrical[field] = 0;
            else if (tightenTerminals) electrical[field] = 8;
        }
    }

    public static JsonNode ResetNeeds(JsonNode needsDomain)
    {
        JsonNode clone = needsDomain.DeepClone();
        if (clone is not JsonObject needs) throw new InvalidDataException("Отсутствует состояние потребностей.");
        foreach (string key in new[] { "thirst", "hunger", "stress", "urine", "fatigue", "dirtiness", "intoxication", "hangover", "pendingHungerEffect", "pendingThirstEffect", "pendingWeightEffect", "pendingIntoxicationEffect", "pendingUrineEffect", "pendingFatigueEffect", "pendingDirtinessEffect" })
            if (needs.ContainsKey(key)) needs[key] = 0f;
        EnsureValid("player.needs", clone);
        return clone;
    }

    public static JsonNode SetBalance(JsonNode economyDomain, long balanceMinorUnits)
    {
        if (balanceMinorUnits < 0) throw new ArgumentOutOfRangeException(nameof(balanceMinorUnits), "Баланс не может быть отрицательным.");
        EnsureValid("economy.player", economyDomain);
        JsonObject clone = economyDomain.DeepClone().AsObject();
        long before = SaveSchemaValidator.Long(clone, "balanceMinorUnits");
        if (before == balanceMinorUnits) return clone;
        if (clone["ledger"] is not JsonArray ledger) throw new InvalidDataException("Отсутствует журнал денежных операций.");
        long sequence = SaveSchemaValidator.Long(clone, "nextSequence");
        long amount = Math.Abs(checked(balanceMinorUnits - before));
        ledger.Add(new JsonObject
        {
            ["sequence"] = sequence,
            ["transactionId"] = "save-master.adjustment." + Guid.NewGuid().ToString("N"),
            ["kind"] = 6, // EconomyTransactionKind.Adjustment
            ["direction"] = balanceMinorUnits < before ? 0 : 1,
            ["amountMinorUnits"] = amount,
            ["balanceBeforeMinorUnits"] = before,
            ["balanceAfterMinorUnits"] = balanceMinorUnits,
            ["sourceStableId"] = "tool.save-master",
            ["priceId"] = "",
            ["quantity"] = 1,
            ["relatedTransactionId"] = "",
            ["gameTimeTicks"] = ledger.Count > 0 && ledger[^1] is JsonObject last ? SaveSchemaValidator.Long(last, "gameTimeTicks") : 0L,
        });
        clone["balanceMinorUnits"] = balanceMinorUnits;
        clone["nextSequence"] = checked(sequence + 1);
        EnsureValid("economy.player", clone);
        return clone;
    }

    public static JsonNode SetTime(JsonNode timeDomain, DateTime localGameDateTime)
    {
        EnsureValid("core.time", timeDomain);
        JsonObject clone = timeDomain.DeepClone().AsObject();
        DateTime previousDate = new(SaveSchemaValidator.Integer(clone, "year"), SaveSchemaValidator.Integer(clone, "month"), SaveSchemaValidator.Integer(clone, "day"));
        long dayDelta = (localGameDateTime.Date - previousDate).Days;
        long targetTime = localGameDateTime.TimeOfDay.Ticks / 10; // 1 game tick = 1 microsecond.
        long delta = checked(dayDelta * 86_400_000_000L + targetTime - SaveSchemaValidator.Long(clone, "timeOfDayTicks"));
        long elapsed = checked(SaveSchemaValidator.Long(clone, "elapsedGameTicks") + delta);
        long dayIndex = checked(SaveSchemaValidator.Long(clone, "dayIndex") + dayDelta);
        if (elapsed < 0 || dayIndex < 0) throw new InvalidDataException("Нельзя установить дату раньше начала текущего игрового мира.");
        clone["elapsedGameTicks"] = elapsed;
        clone["dayIndex"] = dayIndex;
        clone["year"] = localGameDateTime.Year;
        clone["month"] = localGameDateTime.Month;
        clone["day"] = localGameDateTime.Day;
        clone["timeOfDayTicks"] = targetTime;
        clone["fractionalGameTickRemainder"] = 0d;
        EnsureValid("core.time", clone);
        return clone;
    }

    private static JsonObject Vehicle(JsonNode root, int index)
    {
        if (root["vehicles"] is not JsonArray vehicles || index < 0 || index >= vehicles.Count || vehicles[index] is not JsonObject vehicle)
            throw new ArgumentOutOfRangeException(nameof(index), "Машина не найдена в сохранении.");
        return vehicle;
    }

    private static void EnsureValid(string id, JsonNode payload)
    {
        ValidationIssue? first = SaveSchemaValidator.ValidatePayload(id, payload).FirstOrDefault(i => i.Severity == ValidationSeverity.Error);
        if (first != null) throw new InvalidDataException(first.JsonPointer + ": " + first.Message);
    }
}
