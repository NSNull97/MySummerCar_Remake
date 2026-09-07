using System.Globalization;

namespace MySummerRemake.SaveMaster.Core;

public sealed record FieldMetadata(string Label, string Help, bool IsReadOnly = false,
    double? Min = null, double? Max = null, IReadOnlyDictionary<string, string>? Choices = null);

/// <summary>Audited against the project-owned DTOs, September 2026. No Unity dependency.</summary>
public static class SchemaCatalog
{
    private static readonly Dictionary<string, (string Title, int Version)> Domains = new(StringComparer.Ordinal)
    {
        ["core.time"] = ("Время и календарь", 1), ["weather.environment"] = ("Погода и влажность", 1),
        ["player.state"] = ("Игрок · положение и камера", 1), ["player.needs"] = ("Игрок · потребности", 3),
        ["world.entities"] = ("Мир · предметы и физика", 2), ["vehicle.satsuma"] = ("Машина · сборка и механика", 1),
        ["vehicle.satsuma.key-access"] = ("Машина · ключи", 1), ["interaction.carry"] = ("Предмет в руках", 1),
        ["items.instances"] = ("Предметы · содержимое и состояние", 1), ["home.state"] = ("Дом и сауна", 2),
        ["economy.player"] = ("Деньги и операции", 1), ["npc.state"] = ("Персонажи и отношения", 2),
        ["traffic.state"] = ("Транспорт и дорожные события", 1), ["services.state"] = ("Магазин, сервис и заказы", 1),
        ["lighting.electrical-grid"] = ("Электросеть и освещение", 1),
    };

    public static string DomainTitle(string domainId) => Domains.TryGetValue(domainId, out var domain) ? domain.Title : domainId + " · неизвестный раздел";
    public static bool IsSupported(string domainId, int schemaVersion) => Domains.TryGetValue(domainId, out var domain) && schemaVersion == domain.Version;

    public static bool IsInteger(string domainId, string pointer)
    {
        string name = pointer.Split('/').LastOrDefault() ?? "";
        if (pointer.Contains("/pendingStages/", StringComparison.Ordinal)) return true;
        if (domainId == "npc.state" && pointer.Contains("/relationships/", StringComparison.Ordinal) && name == "value") return true;
        if (name is "year" or "month" or "day" or "stage" or "kind" or "mode" or "phase" or "posture" or "lifecycleState" or "engineStatus" or "shiftStatus" or "activityState" or "Surface" or "selectedGear" or "direction" or "quantity" or "remaining" or "revision" or "Revision" or "qualityTier" or "batteryPlusStage" or "batteryMinusStage" or "starterCableStage") return true;
        return new[] { "Count", "Index", "Ordinal", "Sequence", "Ticks", "MinorUnits", "BasisPoints", "Version", "Circuits", "Trips", "Runs", "Attempts" }.Any(s => name.EndsWith(s, StringComparison.Ordinal));
    }

    public static IReadOnlyDictionary<string, string> WireConnections { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Alternator"] = "Генератор", ["AmplifierPower"] = "Усилитель · питание", ["AmplifierAudio"] = "Усилитель · сигнал",
        ["BatteryHarness"] = "Плюсовой жгут аккумулятора", ["CoilHarness"] = "Катушка зажигания", ["Dash1"] = "Приборная панель 1",
        ["Dash2"] = "Приборная панель 2", ["GroundBattery"] = "Масса аккумулятора", ["MarkerLeft"] = "Левый габарит",
        ["MarkerRight"] = "Правый габарит", ["FuelTank"] = "Топливный бак", ["GaugeAfr"] = "Датчик состава смеси",
        ["GaugeExtra"] = "Дополнительные приборы", ["FrontLightsHarness"] = "Жгут переднего света", ["HeadlightLeft"] = "Левая фара",
        ["HeadlightRight"] = "Правая фара", ["Ignition"] = "Замок зажигания", ["RadiatorFan"] = "Вентилятор радиатора",
        ["Radio"] = "Магнитола", ["RearlightLeft"] = "Левый задний фонарь", ["RearlightRight"] = "Правый задний фонарь",
        ["RegulatorHarness"] = "Жгут регулятора", ["Starter"] = "Стартер", ["SubwooferLeft"] = "Левый сабвуфер",
        ["SubwooferRight"] = "Правый сабвуфер", ["SwitchLights"] = "Выключатель света",
    };

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["schemaVersion"]="Версия данных", ["configurationId"]="Конфигурация", ["configId"]="Конфигурация", ["catalogId"]="Каталог",
        ["worldPosition"]="Положение в мире, м", ["worldRotation"]="Поворот (кватернион)", ["linearVelocity"]="Скорость, м/с", ["angularVelocity"]="Угловая скорость, рад/с",
        ["motor"]="Движение", ["look"]="Камера", ["posture"]="Поза", ["crouching"]="Приседание (совместимость)", ["pitchDegrees"]="Наклон камеры, °",
        ["verticalSpeedMetersPerSecond"]="Вертикальная скорость, м/с", ["thirst"]="Жажда", ["hunger"]="Голод", ["stress"]="Стресс", ["urine"]="Мочевой пузырь",
        ["fatigue"]="Усталость", ["dirtiness"]="Грязь", ["weightKilograms"]="Вес, кг", ["intoxication"]="Опьянение", ["hangover"]="Похмелье",
        ["vehicles"]="Машины", ["assembly"]="Сборка", ["parts"]="Детали", ["dynamicParts"]="Купленные детали", ["part"]="Деталь", ["itemDefinitionId"]="Тип предмета", ["mounts"]="Места установки", ["fasteners"]="Болты и крепёж", ["fastenerGroups"]="Группы затяжки",
        ["lifecycleState"]="Состояние установки", ["installedMountId"]="Место установки", ["installedPartStableEntityId"]="Установленная деталь", ["partDefinitionId"]="Тип детали",
        ["stableEntityId"]="Постоянный ID", ["stableVehicleId"]="ID машины", ["fastenerDefinitionId"]="Тип крепежа", ["mountId"]="ID места установки",
        ["inserted"]="Болт вставлен", ["seated"]="Болт наживлён", ["stage"]="Ступень затяжки / этап", ["isBolted"]="Группа зафиксирована",
        ["simulation"]="Двигатель и жидкости", ["physics"]="Положение и движение", ["ignitionOn"]="Зажигание включено", ["electrical"]="Проводка и клеммы",
        ["installedConnectionIds"]="Подключённые провода", ["batteryPlusStage"]="Затяжка плюсовой клеммы", ["batteryMinusStage"]="Затяжка минусовой клеммы", ["starterCableStage"]="Затяжка кабеля стартера",
        ["wipers"]="Дворники", ["handbrake"]="Ручной тормоз", ["paint"]="Покраска", ["engineStatus"]="Состояние двигателя", ["engineRpm"]="Обороты двигателя, об/мин",
        ["selectedGear"]="Передача", ["fuelLiters"]="Топливо, л", ["oilLiters"]="Масло, л", ["coolantLiters"]="Охлаждающая жидкость, л", ["batteryCharge01"]="Заряд аккумулятора, 0–1",
        ["engineTemperatureCelsius"]="Температура двигателя, °C", ["oilTemperatureCelsius"]="Температура масла, °C", ["engineWear01"]="Износ двигателя, 0–1", ["engineDamage01"]="Повреждение двигателя, 0–1",
        ["wheels"]="Колёса", ["alignmentDegrees"]="Схождение, °", ["steeringAlignment"]="Регулировка схождения", ["camshaftTiming"]="Метка распредвала", ["angleDegrees"]="Угол метки, °",
        ["engineAdjustment"]="Регулировка двигателя", ["engineDocking"]="Наживление двигателя", ["value"]="Значение", ["kind"]="Тип",
        ["balanceMinorUnits"]="Баланс, пенни (100 = 1 марка)", ["ledger"]="История операций", ["transactions"]="Операции", ["priceScopes"]="Изменения цен",
        ["amountMinorUnits"]="Сумма, пенни", ["quantity"]="Количество", ["remaining"]="Остаток", ["revision"]="Счётчик изменений", ["nextSequence"]="Следующая операция",
        ["instances"]="Экземпляры", ["entities"]="Объекты", ["state"]="Состояние", ["activeSelf"]="Объект активен", ["active"]="Активен", ["sleeping"]="Физика спит",
        ["isKinematic"]="Кинематический объект", ["useGravity"]="Гравитация", ["sourceCellId"]="Ячейка мира", ["materializationPosition"]="Место появления, м", ["materializationRotation"]="Поворот при появлении",
        ["kitchenTapOpen"]="Кухонный кран открыт", ["showerSwitchOn"]="Душ выбран", ["showerValveOpen"]="Вода в душе открыта", ["electricSaunaPowerOn"]="Сауна включена",
        ["electricSaunaHeatKnobDegrees"]="Регулятор нагрева сауны, °", ["electricSaunaTimerKnobDegrees"]="Регулятор таймера сауны, °", ["electricSaunaTimerSecondsRemaining"]="Таймер сауны, с",
        ["saunaTemperatureCelsius"]="Температура сауны, °C", ["saunaSteamNormalized"]="Пар в сауне, 0–1", ["fridgePowered"]="Холодильник включён", ["fridgeDoorOpen"]="Холодильник открыт",
        ["stovePowered"]="Плита включена", ["televisionPowered"]="Телевизор включён", ["fireplaceLit"]="Камин горит", ["mangalLit"]="Мангал горит",
        ["weatherDomain"]="Состояние погоды", ["weather"]="Погода", ["wetness"]="Влажность", ["lightning"]="Молнии", ["qualityTier"]="Качество окружения",
        ["GroundWetness01"]="Влажность земли", ["RoadWetness01"]="Влажность дороги", ["PuddleAmount01"]="Лужи", ["VegetationWetness01"]="Влажность растений",
        ["elapsedGameTicks"]="Игровое время, тики", ["dayIndex"]="Номер игрового дня", ["year"]="Год", ["month"]="Месяц", ["day"]="День", ["timeOfDayTicks"]="Время суток, тики",
        ["timeScale"]="Скорость времени", ["isPaused"]="Время на паузе", ["fractionalGameTickRemainder"]="Доля игрового тика", ["hasKey"]="Ключ получен", ["hasSatsumaKey"]="Ключ Satsuma получен",
        ["drivers"]="Водители", ["suski"]="Суски · спасение", ["ambientActors"]="Обычный трафик", ["transports"]="Общественный транспорт", ["eventActors"]="Участники событий",
        ["workshopOrder"]="Заказ в мастерской", ["inspectionOrder"]="Техосмотр", ["homeMailOrder"]="Почтовый заказ", ["workshopSelection"]="Выбранные работы", ["stock"]="Товарные остатки",
    };

    private static readonly Dictionary<string, string> VehicleTuningLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mechanicalCondition"]="Состояние механической детали", ["conditionPercent"]="Состояние детали, %", ["broken"]="Деталь сломана",
        ["valveAdjustment"]="Регулировка клапанов", ["intake"]="Впускные клапаны", ["exhaust"]="Выпускные клапаны",
        ["serviceCaps"]="Крышки бачков и горловин", ["kinds"]="Типы крышек", ["angles"]="Углы крышек, °",
        ["satsumaOperatingState"]="Рабочее состояние двигателя и гидравлики", ["brakeFrontLiters"]="Жидкость передних тормозов, л",
        ["brakeRearLiters"]="Жидкость задних тормозов, л", ["clutchLiters"]="Жидкость сцепления, л", ["oilContaminationPercent"]="Загрязнение масла, %",
        ["oilPressureBar"]="Давление масла, бар", ["coolantPressurePsi"]="Давление охлаждающей жидкости, PSI",
        ["crankingSeconds"]="Накопленное время прокрутки, с", ["radiatorFanRunning"]="Вентилятор радиатора включён",
        ["combustionRundownActive"]="Двигатель докручивается после сгорания",
        ["hasMechanicalCondition"]="Есть состояние механической детали", ["hasValveAdjustment"]="Есть настройки клапанов",
        ["hasServiceCaps"]="Есть состояние крышек", ["hasSatsumaOperatingState"]="Есть рабочее состояние двигателя",
        ["hasCombustionHistory"]="Есть история сгорания", ["hasDashboardControls"]="Есть состояние органов управления",
        ["hasFuelLineConnection"]="Есть состояние топливного соединения",
    };

    private static readonly HashSet<string> Needs = new(StringComparer.OrdinalIgnoreCase) { "thirst", "hunger", "stress", "urine", "fatigue", "dirtiness", "intoxication", "hangover" };

    public static FieldMetadata Describe(string domainId, string pointer)
    {
        string name = pointer.Split('/').LastOrDefault() ?? "";
        name = name.Replace("~1", "/").Replace("~0", "~");
        string label = Labels.GetValueOrDefault(name, name.Length == 0 ? DomainTitle(domainId) : name);
        if (domainId == "vehicle.satsuma") label = VehicleTuningLabels.GetValueOrDefault(name, label);
        if (int.TryParse(name, out int index)) label = "Запись " + (index + 1).ToString(CultureInfo.InvariantCulture);
        if (domainId == "vehicle.satsuma" && DescribeVehicleTuning(pointer, name, label) is FieldMetadata tuning) return tuning;
        bool wire = pointer.Contains("/electrical/installedConnectionIds", StringComparison.Ordinal);
        bool locked = !wire && (name is "id" or "ids" or "guid" || name.EndsWith("Id", StringComparison.Ordinal) || name.EndsWith("Ids", StringComparison.Ordinal) || name.EndsWith("Version", StringComparison.Ordinal) || name.EndsWith("Guid", StringComparison.Ordinal) || name is "hasSteeringAlignment" or "hasCamshaftTiming" or "hasEngineAdjustment" or "hasEngineDocking" || name == "kind" && pointer.Contains("/engineAdjustment/"));
        if (locked) return new(label, "Служебная идентичность или версия: изменение нарушает совместимость сохранения.", true);
        if (wire) return new(label, "Постоянные подключения проводки. Изменяйте через панель проводов; клеммы зависят от кабелей.");
        if (domainId == "player.needs")
        {
            if (Needs.Contains(name)) return new(label, "0 — потребность удовлетворена, 100 — максимальное значение.", Min: 0, Max: 100);
            if (name.StartsWith("pending", StringComparison.Ordinal)) return new("Отложенный эффект · " + name, "Эффект будет применён игрой со временем.", Min: -1000, Max: 1000);
            if (name == "weightKilograms") return new(label, "Допустим строго положительный вес до 500 кг.", Min: 0, Max: 500);
        }
        if (name.EndsWith("01", StringComparison.OrdinalIgnoreCase) || name == "saunaSteamNormalized") return new(label, "Нормализованное значение от 0 до 1.", Min: 0, Max: 1);
        if (name is "batteryPlusStage" or "batteryMinusStage" or "starterCableStage") return new(label, "0 — ослаблено, 8 — затянуто. Нужен соответствующий подключённый кабель.", Min: 0, Max: 8);
        if (name == "stage" && pointer.Contains("/fasteners/")) return new(label, "Максимум берётся из каталога конкретного болта. Вставка, наживление и группа проверяются вместе.", Min: 0);
        if (name == "isBolted") return new(label, "Защёлка группы зависит от суммы затяжки и порогов. Используйте действия с крепежом для согласованного изменения.");
        if (name == "lifecycleState") return new(label, "Связано с местом установки и записью mount. Несогласованные изменения блокируются.", Choices: Options("Свободная деталь", "Установлена", "Основа сборки"));
        if (name == "posture") return new(label, "Положение тела игрока.", Choices: Options("Стоит", "Присел", "Низкий присед"));
        if (name == "activityState") return new("Действие персонажа", "Текущее состояние персонажа.", Choices: Options("Скрыт", "Ожидает", "Работает", "Идёт", "Разговаривает", "Сидит в машине", "Отключён"));
        if (name == "condition" && domainId == "items.instances") return new("Состояние предмета, %", "0–100; износ и испорченность зависят от типа предмета.", Min: 0, Max: 100);
        if (name == "content" && domainId == "items.instances") return new("Содержимое", "Единицы и максимум зависят от типа предмета. У контейнера значение должно совпадать с количеством дочерних предметов.", Min: -0.0001);
        if (name == "engineStatus") return new(label, "Состояние двигателя в текущей симуляции.", Choices: Options("Выключен", "Прокрутка стартером", "Работает", "Заглох"));
        if (name == "shiftStatus") return new("Статус переключения", "Сохранённый результат переключения передачи.", Choices: Options("Стабильна", "Принято", "Отклонено"));
        if (domainId == "vehicle.satsuma" && name == "selectedGear") return new(label, "Текущая конфигурация ремейка: задняя, нейтральная и пять передач вперёд.", Min: -1, Max: 5, Choices: new Dictionary<string, string>
        {
            ["-1"] = "Задняя", ["0"] = "Нейтральная", ["1"] = "1-я передача", ["2"] = "2-я передача",
            ["3"] = "3-я передача", ["4"] = "4-я передача", ["5"] = "5-я передача",
        });
        if (name == "mode" && pointer.Contains("/wipers/")) return new("Режим дворников", "Скорость работы дворников.", Choices: Options("Выключены", "Медленно", "Быстро"));
        if (name == "pitchDegrees") return new(label, "Вертикальный угол камеры.", Min: -89, Max: 89);
        if (name == "verticalSpeedMetersPerSecond") return new(label, "Скорость вдоль вертикальной оси.", Min: -200, Max: 200);
        if (name == "alignmentDegrees") return new(label, "Диапазон рулевой тяги из AssemblySteeringAlignmentState.", Min: -6, Max: 6);
        if (name == "angleDegrees" && pointer.Contains("/camshaftTiming/")) return new(label, "Сохранённая метка распредвала; штатный шаг 5°.", Min: 0, Max: 360);
        if (name == "electricSaunaHeatKnobDegrees") return new(label, "Положение ручки нагрева.", Min: 1, Max: 150);
        if (name == "electricSaunaTimerKnobDegrees") return new(label, "Положение ручки таймера.", Min: 1, Max: 120);
        if (name == "electricSaunaTimerSecondsRemaining") return new(label, "Оставшееся время нагрева.", Min: 0, Max: 720);
        if (name == "saunaTemperatureCelsius") return new(label, "Температура воздуха сауны.", Min: -80, Max: 250);
        if (domainId == "core.time") return new(label, "Календарь вычисляется из elapsedGameTicks. Разрозненные изменения даты могут не пройти загрузку; требуется согласованное изменение.");
        if (domainId is "npc.state" or "traffic.state" or "services.state" or "economy.player") return new(label, "Состояние связано с другими записями и каталогами игры. Проверки редактора не заменяют проверку игрового процесса.");
        return new(label, "Исходное поле сохранения ремейка. Тип и структура сохраняются; служебные ID защищены.");
    }

    private static FieldMetadata? DescribeVehicleTuning(string pointer, string name, string label)
    {
        if (name is "hasMechanicalCondition" or "hasValveAdjustment" or "hasServiceCaps" or
            "hasSatsumaOperatingState" or "hasCombustionHistory" or "hasDashboardControls" or "hasFuelLineConnection")
            return new(label, "Служебный флаг наличия состояния. При выключенном флаге игра использует прежнее поведение и игнорирует вложенные значения; флаг защищён от редактирования.", true);
        if (pointer.EndsWith("/serviceCaps/kinds", StringComparison.Ordinal) || pointer.Contains("/serviceCaps/kinds/", StringComparison.Ordinal))
        {
            bool leaf = int.TryParse(name, out int kindIndex);
            return new(leaf ? "Тип крышки " + (kindIndex + 1) : label,
                "Тип и порядок крышек связаны с конкретной деталью: 0 — масло, 1 — охлаждающая жидкость, 2 — передние тормоза, 3 — задние тормоза, 4 — сцепление. Эта привязка защищена.",
                true, Min: leaf ? 0 : null, Max: leaf ? 4 : null);
        }

        if (pointer.Contains("/mechanicalCondition/", StringComparison.Ordinal))
        {
            if (name == "conditionPercent") return new(label, "100 — исправное состояние, 0 — полностью изношена. При нуле обязательно включить признак поломки.", Min: 0, Max: 100);
            if (name == "broken") return new(label, "Поломка сохраняется отдельно от процента состояния. Нельзя снять её при нулевом состоянии детали.");
        }
        if (pointer.Contains("/valveAdjustment/", StringComparison.Ordinal))
        {
            int cylinder = name switch { "x" => 1, "y" => 2, "z" => 3, "w" => 4, _ => 0 };
            bool intake = pointer.Contains("/valveAdjustment/intake/", StringComparison.Ordinal);
            bool exhaust = pointer.Contains("/valveAdjustment/exhaust/", StringComparison.Ordinal);
            if (cylinder != 0 && (intake || exhaust))
                return new((intake ? "Впуск" : "Выпуск") + " · цилиндр " + cylinder,
                    "Условная шкала настройки винта. Штатный шаг управления — 0,3; настройка сохраняется на оси коромысел. Исходное исправное значение — " + (intake ? "7." : "6."),
                    Min: intake ? 4 : 3, Max: intake ? 10 : 9);
        }
        if (pointer.Contains("/serviceCaps/angles/", StringComparison.Ordinal) && int.TryParse(name, out int capIndex))
            return new("Угол крышки " + (capIndex + 1) + ", °", "1° — полностью открыта, 359° — полностью закрыта. Штатный шаг управления — 33°; порядок крышек сохраняется.", Min: 1, Max: 359);
        if (pointer.Contains("/satsumaOperatingState/", StringComparison.Ordinal))
        {
            FieldMetadata? operating = name switch
            {
                "brakeFrontLiters" => new(label, "Объём в бачке переднего контура тормозов. Полная ёмкость — 1 л.", Min: 0, Max: 1),
                "brakeRearLiters" => new(label, "Объём в бачке заднего контура тормозов. Полная ёмкость — 1 л.", Min: 0, Max: 1),
                "clutchLiters" => new(label, "Объём в бачке сцепления. Полная ёмкость — 0,5 л.", Min: 0, Max: 0.5),
                "oilContaminationPercent" => new(label, "0 — чистое масло, 100 — максимальное загрязнение. Объём масла хранится отдельно.", Min: 0, Max: 100),
                "oilPressureBar" => new(label, "Сохранённое давление масла. Работающая симуляция пересчитывает его по состоянию двигателя.", Min: 0, Max: 10),
                "coolantPressurePsi" => new(label, "Сохранённое давление охлаждающей жидкости. Симуляция пересчитывает его по нагреву, объёму жидкости и состоянию крышки.", Min: 0, Max: 50),
                "crankingSeconds" => new(label, "Накопленная прокрутка для запуска двигателя. При прекращении запуска симуляция сбрасывает это значение.", Min: 0, Max: 60),
                "radiatorFanRunning" => new(label, "Сохранённое состояние электрического вентилятора; симуляция пересчитывает его по питанию и температуре."),
                _ => null,
            };
            if (operating is not null) return operating;
        }
        return name switch
        {
            "mechanicalCondition" => new(label, "Износ штатной механической детали. Купленные расходники хранят своё состояние в разделе предметов."),
            "valveAdjustment" => new(label, "Четыре впускных и четыре выпускных настройки на оси коромысел; единицы шкалы условные."),
            "intake" when pointer.Contains("/valveAdjustment/", StringComparison.Ordinal) => new(label, "Четыре впускные настройки, цилиндры 1–4. Диапазон каждой — 4–10."),
            "exhaust" when pointer.Contains("/valveAdjustment/", StringComparison.Ordinal) => new(label, "Четыре выпускные настройки, цилиндры 1–4. Диапазон каждой — 3–9."),
            "serviceCaps" => new(label, "Положение крышек принадлежит бачку или крышке двигателя и сохраняется при снятии детали."),
            "angles" when pointer.Contains("/serviceCaps/", StringComparison.Ordinal) => new(label, "Углы крышек следуют сохранённому порядку типов: 1° — открыто, 359° — закрыто."),
            "satsumaOperatingState" => new(label, "Снимок жидкостей, загрязнения и рабочих параметров. Давления, прокрутка и вентилятор изменяются работающей симуляцией."),
            "combustionRundownActive" => new(label, "Сохранённый признак вращения после сгорания. Используется при включённом флаге истории сгорания; работающий двигатель сохраняет этот признак включённым."),
            _ => null,
        };
    }

    private static IReadOnlyDictionary<string, string> Options(params string[] values) => values.Select((s, i) => new KeyValuePair<string, string>(i.ToString(CultureInfo.InvariantCulture), s)).ToDictionary();
}
