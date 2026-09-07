using System;
using System.Collections.Generic;
using System.Text;

namespace MSC.Player
{
    /// <summary>
    /// Presentation-only bilingual catalog for the compact interaction HUD.
    /// Gameplay definitions remain language-neutral authorities; this adapter
    /// prevents donor/debug labels and mixed-language prompts from leaking to
    /// the player while retaining the raw text as a compatibility fallback.
    /// </summary>
    public static class InteractionUiTextCatalog
    {
        public const string EnglishLocaleId = "en-US";
        public const string RussianLocaleId = "ru-RU";

        private static readonly LocalizedText[] DisplayNames =
        {
            // Phase 1 items and consumables.
            N("Sausages package", "Упаковка сосисок"),
            N("Macaroni box", "Коробка макарон"),
            N("Pizza", "Пицца"),
            N("Potato chips", "Картофельные чипсы"),
            N("Milk", "Молоко"),
            N("Yeast", "Дрожжи"),
            N("Sugar", "Сахар"),
            N("Juice concentrate", "Концентрат сока"),
            N("Coffee package", "Пачка кофе"),
            N("Coffee", "Кофе"),
            N("Charcoal", "Древесный уголь"),
            N("Cigarettes", "Сигареты"),
            N("Mosquito spray", "Спрей от комаров"),
            N("Beer case", "Ящик пива"),
            N("Beer bottle", "Бутылка пива"),
            N("Beer", "Пиво"),
            N("Booze bottle", "Бутылка спиртного"),
            N("Vodka shot", "Рюмка водки"),
            N("Loose sausage", "Сосиска"),
            N("Sausage", "Сосиска"),
            N("Sausages", "Сосиски"),
            N("Sausage and potatoes meal", "Сосиски с картофелем"),
            N("Sausage and fries", "Сосиски с картофелем фри"),
            N("Pike", "Щука"),
            N("Moose meat", "Мясо лося"),
            N("Brake fluid", "Тормозная жидкость"),
            N("Coolant", "Охлаждающая жидкость"),
            N("Motor oil", "Моторное масло"),
            N("Two-stroke oil", "Двухтактное масло"),
            N("Alternator belt", "Ремень генератора"),
            N("Oil filter", "Масляный фильтр"),
            N("Sparkplug box", "Коробка свечей зажигания"),
            N("Spark plugs", "Свечи зажигания"),
            N("Lightbulb box", "Коробка лампочек"),
            N("Fuse package", "Упаковка предохранителей"),
            N("R20 battery", "Батарейка R20"),
            N("R20 battery box", "Коробка батареек R20"),
            N("Car battery", "Автомобильный аккумулятор"),
            N("Fire extinguisher", "Огнетушитель"),
            N("Spray paint variants", "Аэрозольная краска"),
            N("Matte spray paint", "Матовая аэрозольная краска"),
            N("Spray paint 01", "Аэрозольная краска 01"),
            N("Spray paint 02", "Аэрозольная краска 02"),
            N("Spray paint 03", "Аэрозольная краска 03"),
            N("Spray paint 04", "Аэрозольная краска 04"),
            N("Spray paint 05", "Аэрозольная краска 05"),
            N("Spray paint 06", "Аэрозольная краска 06"),
            N("Spray paint 07", "Аэрозольная краска 07"),
            N("Spray paint 08", "Аэрозольная краска 08"),
            N("Spray paint 09", "Аэрозольная краска 09"),
            N("Spray paint 10", "Аэрозольная краска 10"),
            N("Spray paint 11", "Аэрозольная краска 11"),
            N("Spray paint 12", "Аэрозольная краска 12"),
            N("Gasoline can", "Канистра бензина"),
            N("Diesel can", "Канистра дизельного топлива"),
            N("Axe", "Топор"),
            N("Digging bar", "Лом"),
            N("Sledgehammer", "Кувалда"),
            N("Spanner set", "Набор гаечных ключей"),
            N("Wiring mess", "Моток проводки"),
            N("Car jack", "Домкрат"),
            N("Floor jack", "Подкатной домкрат"),
            N("Motor hoist", "Подъёмник двигателя"),
            N("Kilju bucket", "Ведро для килю"),
            N("Kilju lid", "Крышка ведра для килю"),
            N("Sauna bucket", "Ведро для сауны"),
            N("Sauna dipper", "Ковш для сауны"),
            N("Wood carrier", "Корзина для дров"),
            N("Garbage barrel", "Мусорная бочка"),
            N("Portable grill", "Переносной гриль"),
            N("Coffee pan", "Кофейник"),
            N("Coffee cup", "Кофейная чашка"),
            N("Fish trap", "Рыболовная ловушка"),
            N("Flashlight", "Фонарик"),
            N("Lantern", "Керосиновая лампа"),
            N("Helmet", "Шлем"),
            N("Radar detector", "Радар-детектор"),
            N("Portable radio", "Переносное радио"),
            N("Camera", "Фотоаппарат"),
            N("Floppy disk", "Дискета"),
            N("CD set", "Набор компакт-дисков"),
            N("Notepad", "Блокнот"),
            N("Parts magazine", "Каталог запчастей"),
            N("TV remote", "Пульт телевизора"),
            N("Audio CD 01", "Аудиодиск 01"),
            N("Audio CD 02", "Аудиодиск 02"),
            N("Audio CD 03", "Аудиодиск 03"),
            N("Audio CD case 01", "Коробка аудиодиска 01"),
            N("Audio CD case 02", "Коробка аудиодиска 02"),
            N("Audio CD case 03", "Коробка аудиодиска 03"),
            N("Sofa", "Диван"),
            N("Basketball", "Баскетбольный мяч"),
            N("Football", "Футбольный мяч"),
            N("Firewood", "Полено"),
            N("Firewood definition", "Полено"),
            N("Shopping bag", "Пакет с покупками"),
            N("Berry box", "Коробка ягод"),
            N("Fireworks bag", "Пакет фейерверков"),
            N("Suitcase", "Чемодан"),
            N("Rocket/firework object", "Ракета"),
            N("Christmas lights", "Новогодняя гирлянда"),
            N("Xmas lights", "Новогодняя гирлянда"),
            N("Trophy", "Кубок"),
            N("Trophy candidates", "Кубок"),
            N("Eyewear", "Очки"),
            N("Eyewear candidates", "Очки"),
            N("Hat", "Шляпа"),
            N("Hat candidates", "Шляпа"),
            N("Spark plug", "Свеча зажигания"),
            N("Light bulb", "Лампочка"),
            N("Fuse", "Предохранитель"),
            N("R20 battery unit", "Батарейка R20"),
            N("Mail-order envelope", "Конверт с заказом"),
            N("Marker lights", "Габаритные огни"),
            N("Twin carburetors", "Двойные карбюраторы"),
            N("Twin carburators", "Двойные карбюраторы"),
            N("Steel headers", "Стальной выпускной коллектор"),
            N("Plush wheel cover", "Плюшевая оплётка руля"),
            N("Slot rims", "Диски Slot"),
            N("Window grille", "Решётка заднего стекла"),
            N("Racing flywheel", "Гоночный маховик"),
            N("Leopard seat covers", "Леопардовые чехлы сидений"),
            N("Steel wide rims", "Широкие стальные диски"),
            N("Subwoofers", "Сабвуферы"),
            N("Ratchet set", "Набор трещоток"),
            N("Tachometer", "Тахометр"),
            N("Racing rims", "Гоночные диски"),
            N("Racing muffler", "Гоночный глушитель"),
            N("Leopard dashboard cover", "Леопардовая накидка панели"),
            N("Racing exhaust", "Гоночная выхлопная система"),
            N("Rear spoiler", "Задний спойлер"),
            N("Rear spoiler 2", "Задний спойлер 2"),
            N("Plush dashboard cover", "Плюшевая накидка панели"),
            N("CD player", "CD-проигрыватель"),
            N("N2O kit", "Комплект закиси азота"),
            N("Zebra seat covers", "Зебровые чехлы сидений"),
            N("Black window wrap", "Чёрная плёнка на стёкла"),
            N("Fender flares", "Расширители арок"),
            N("Racing harness", "Гоночные ремни"),
            N("Sport steering wheel", "Спортивный руль"),
            N("Zebra wheel cover", "Зебровая оплётка руля"),
            N("Spoke rims", "Спицованные диски"),
            N("Leopard wheel cover", "Леопардовая оплётка руля"),
            N("Fuel mixture gauge", "Указатель состава смеси"),
            N("Extra gauges", "Дополнительные приборы"),
            N("Turbine rims", "Диски Turbine"),
            N("Plush seat covers", "Плюшевые чехлы сидений"),
            N("Bucket seats", "Ковшеобразные сиденья"),
            N("Antenna", "Антенна"),
            N("Rally steering wheel", "Раллийный руль"),
            N("Racing carburetors", "Гоночные карбюраторы"),
            N("Racing carburators", "Гоночные карбюраторы"),
            N("Front spoiler", "Передний спойлер"),
            N("Dual exhaust tip", "Двойная насадка глушителя"),
            N("Rally rims", "Раллийные диски"),
            N("Hayosiko rims", "Диски Hayosiko"),
            N("Octo rims", "Диски Octo"),
            N("Racing radiator", "Гоночный радиатор"),
            N("Rally suspension", "Раллийная подвеска"),
            N("Zebra dashboard cover", "Зебровая накидка панели"),
            N("Fiberglass hood", "Стеклопластиковый капот"),
            N("Buttermilk", "Пахта"),
            N("Orange juice", "Апельсиновый сок"),
            N("Bug spray", "Спрей от насекомых"),
            N("Laundry detergent", "Стиральный порошок"),
            N("Sponge", "Губка"),
            N("Shampoo", "Шампунь"),
            N("Hand soap", "Мыло для рук"),
            N("Dish soap", "Средство для посуды"),
            N("Soap", "Мыло"),
            N("Wheat flour", "Пшеничная мука"),
            N("Rye flour", "Ржаная мука"),
            N("Mustard", "Горчица"),
            N("Ketchup", "Кетчуп"),
            N("Meat soup", "Мясной суп"),
            N("Pea soup", "Гороховый суп"),
            N("Canned meatballs", "Фрикадельки в банке"),
            N("Fishstick box", "Коробка рыбных палочек"),
            N("Can opener", "Консервный нож"),
            N("Fishstick", "Рыбная палочка"),

            // Satsuma assembly parts. English values intentionally clean up
            // donor/debug spellings such as battery0, oilpan and wheel_gt1.
            N("Air filter", "Воздушный фильтр", "airfilter"),
            N("Alternator", "Генератор", "alternator"),
            N("Back panel", "Задняя панель", "back panel"),
            N("Battery", "Аккумулятор", "battery0"),
            N("Engine block", "Блок двигателя", "block"),
            N("Boot lid", "Крышка багажника", "bootlid"),
            N("Brake lining", "Тормозная магистраль", "brake lining"),
            N("Brake master cylinder", "Главный тормозной цилиндр", "brake master cylinder"),
            N("Front bumper", "Передний бампер", "bumper front"),
            N("Rear bumper", "Задний бампер", "bumper rear"),
            N("Camshaft", "Распределительный вал", "camshaft"),
            N("Camshaft gear", "Шестерня распредвала", "camshaft gear"),
            N("Carburetor", "Карбюратор", "carburator"),
            N("Clock gauge", "Часы", "clock gauge"),
            N("Clutch cover plate", "Корзина сцепления", "clutch cover plate"),
            N("Clutch disc", "Диск сцепления", "clutch disc"),
            N("Clutch lining", "Магистраль сцепления", "clutch lining"),
            N("Clutch master cylinder", "Главный цилиндр сцепления", "clutch master cylinder"),
            N("Clutch pressure plate", "Нажимной диск сцепления", "clutch pressure plate"),
            N("Coil spring", "Пружина подвески", "coil spring"),
            N("Crankshaft", "Коленчатый вал", "crankshaft"),
            N("Crankshaft pulley", "Шкив коленвала", "crankshaft pulley"),
            N("Cylinder head", "Головка блока цилиндров", "cylinder head"),
            N("SUOMI dashboard cover", "Накидка панели SUOMI", "dash cover suomi"),
            N("SUOMI steering wheel cover", "Оплётка руля SUOMI"),
            N("Dashboard", "Приборная панель", "dashboard"),
            N("Dashboard meters", "Комбинация приборов", "dashboard meters"),
            N("Disc brake", "Тормозной диск", "disc brake"),
            N("Distributor", "Распределитель зажигания", "distributor"),
            N("Left door", "Левая дверь", "door left"),
            N("Right door", "Правая дверь", "door right"),
            N("Drive gear", "Ведущая шестерня", "drive gear"),
            N("Brake drum", "Тормозной барабан", "drum brake"),
            N("Electrical wiring", "Электропроводка", "electrics"),
            N("Engine plate", "Плита двигателя", "engine plate"),
            N("Exhaust muffler", "Глушитель", "exhaust muffler"),
            N("Exhaust pipe", "Выхлопная труба", "exhaust pipe"),
            N("Left fender", "Левое крыло", "fender left"),
            N("Right fender", "Правое крыло", "fender right"),
            N("Flywheel", "Маховик", "flywheel"),
            N("Fuel pump", "Топливный насос", "fuel pump"),
            N("Fuel strainer", "Топливный фильтр", "fuel strainer"),
            N("Fuel tank", "Топливный бак", "fuel tank"),
            N("Fuel tank pipe", "Трубка топливного бака", "fuel tank pipe"),
            N("Fuzzy dice", "Пушистые кубики", "fur dices"),
            N("Gear linkage", "Тяга переключения передач", "gear linkage"),
            N("Gear stick", "Рычаг переключения передач", "gear stick"),
            N("Gearbox", "Коробка передач", "gearbox"),
            N("Grille", "Решётка радиатора", "grille"),
            N("GT steering wheel", "Руль GT", "gt steering wheel"),
            N("Halfshaft", "Приводной вал", "halfshaft"),
            N("Handbrake", "Ручной тормоз", "handbrake"),
            N("Head gasket", "Прокладка ГБЦ", "head gasket"),
            N("Exhaust headers", "Выпускной коллектор", "headers"),
            N("Left headlight", "Левая фара", "headlight left"),
            N("Right headlight", "Правая фара", "headlight right"),
            N("Hood", "Капот", "hood"),
            N("Hubcap", "Колпак колеса", "hubcap"),
            N("Inspection cover", "Смотровая крышка", "inspection cover"),
            N("Long coil spring", "Удлинённая пружина", "long coil spring"),
            N("Main bearing 1", "Коренной подшипник 1", "main bearing1"),
            N("Main bearing 2", "Коренной подшипник 2", "main bearing2"),
            N("Main bearing 3", "Коренной подшипник 3", "main bearing3"),
            N("Front-left mudflap", "Передний левый брызговик", "mudflap fl"),
            N("Front-right mudflap", "Передний правый брызговик", "mudflap fr"),
            N("Rear-left mudflap", "Задний левый брызговик", "mudflap rl"),
            N("Rear-right mudflap", "Задний правый брызговик", "mudflap rr"),
            N("Oil filter", "Масляный фильтр", "oilfilter0"),
            N("Oil pan", "Масляный поддон", "oilpan"),
            N("Piston 1", "Поршень 1", "piston1"),
            N("Piston 2", "Поршень 2", "piston2"),
            N("Piston 3", "Поршень 3", "piston3"),
            N("Piston 4", "Поршень 4", "piston4"),
            N("Radiator", "Радиатор", "radiator"),
            N("Radiator hose 1", "Патрубок радиатора 1", "radiator hose1"),
            N("Radiator hose 2", "Патрубок радиатора 2", "radiator hose2"),
            N("Radiator hose 3", "Патрубок радиатора 3", "radiator hose3"),
            N("Radio", "Магнитола", "radio"),
            N("Left tail light", "Левый задний фонарь", "rear light left"),
            N("Right tail light", "Правый задний фонарь", "rear light right"),
            N("Rocker cover", "Клапанная крышка", "rocker cover"),
            N("GT rocker cover", "Клапанная крышка GT", "rocker cover gt"),
            N("Rocker shaft", "Ось коромысел", "rocker shaft"),
            N("Satsuma body shell", "Кузов Satsuma"),
            N("SUOMI seat cover", "Чехол сиденья SUOMI", "seat cover suomi"),
            N("Driver seat", "Сиденье водителя", "seat driver"),
            N("Passenger seat", "Пассажирское сиденье", "seat passenger"),
            N("Rear seat", "Заднее сиденье", "seat rear"),
            N("Shock absorber", "Амортизатор", "shock absorber"),
            N("Front-left spindle", "Передняя левая цапфа", "spindle fl"),
            N("Front-right spindle", "Передняя правая цапфа", "spindle fr"),
            N("Starter", "Стартер", "starter"),
            N("Steering column", "Рулевая колонка", "steering column"),
            N("Steering rack", "Рулевая рейка", "steering rack"),
            N("Front-left steering rod", "Левая рулевая тяга", "steering rod fl"),
            N("Front-right steering rod", "Правая рулевая тяга", "steering rod fr"),
            N("Stock steering wheel", "Штатный руль", "stock steering wheel"),
            N("Front-left strut", "Передняя левая стойка", "strut fl"),
            N("Front-right strut", "Передняя правая стойка", "strut fr"),
            N("Subframe", "Подрамник", "sub frame"),
            N("Subwoofer panel", "Панель сабвуферов", "subwoofer panel"),
            N("Timing chain", "Цепь ГРМ", "timing chain"),
            N("Timing cover", "Крышка ГРМ", "timing cover"),
            N("Rear-left trailing arm", "Задний левый продольный рычаг", "trail arm rl"),
            N("Rear-right trailing arm", "Задний правый продольный рычаг", "trail arm rr"),
            N("Water pump", "Водяной насос", "water pump"),
            N("Water-pump pulley", "Шкив водяного насоса", "water pump pulley"),
            N("SUOMI wheel cover", "Оплётка руля SUOMI", "wheel cover suomi"),
            N("GT wheel, front left", "Диск GT, передний левый", "wheel_gt1"),
            N("GT wheel, front right", "Диск GT, передний правый", "wheel_gt2"),
            N("GT wheel, rear left", "Диск GT, задний левый", "wheel_gt3"),
            N("GT wheel, rear right", "Диск GT, задний правый", "wheel_gt4"),
            N("Steel wheel, front left", "Стальной диск, передний левый", "wheel_steel1"),
            N("Steel wheel, front right", "Стальной диск, передний правый", "wheel_steel2"),
            N("Steel wheel, rear left", "Стальной диск, задний левый", "wheel_steel3"),
            N("Steel wheel, rear right", "Стальной диск, задний правый", "wheel_steel4"),
            N("Front-left wishbone", "Передний левый рычаг подвески", "wishbone fl"),
            N("Front-right wishbone", "Передний правый рычаг подвески", "wishbone fr"),

            // World/service titles that share the same compact target block.
            N("Lid", "Крышка", "Крышка"),
            N("Light switch", "Выключатель", "LIGHT SWITCH"),
            N("Teimo's shop", "Магазин Теймо"),
            N("Teimo's pub", "Паб Теймо"),
            N("Teimo gasoline 98 pump", "Колонка бензина 98 у Теймо"),
            N("Teimo diesel pump", "Дизельная колонка у Теймо"),
            N("Teimo fuel oil pump", "Колонка мазута у Теймо"),
            N("Fleetari workshop", "Мастерская Флитари"),
            N("Vehicle inspection station", "Станция техосмотра"),
            N("Gasoline 98", "Бензин 98"),
            N("Diesel", "Дизельное топливо"),
            N("Fuel oil", "Мазут"),
            N("Teimo checkout", "Касса Теймо", "Касса Теймо"),
            N("Fleetari catalog", "Каталог Флитари", "Каталог Флитари"),
            N("Parts catalog", "Каталог запчастей", "Каталог запчастей"),
            N("Mail-order box", "Почтовый ящик заказов", "Почтовый ящик заказов"),
            N("Mail-order payment", "Оплата почтового заказа", "Оплата почтового заказа"),
            N("Body repair", "Ремонт кузова"),
            N("Left door repair", "Ремонт левой двери"),
            N("Right door repair", "Ремонт правой двери"),
            N("Left fender repair", "Ремонт левого крыла"),
            N("Right fender repair", "Ремонт правого крыла"),
            N("Hood repair", "Ремонт капота"),
            N("Bootlid repair", "Ремонт крышки багажника"),
            N("Front bumper repair", "Ремонт переднего бампера"),
            N("Rear bumper repair", "Ремонт заднего бампера"),
            N("Grille repair", "Ремонт решётки"),
            N("Toe alignment", "Регулировка схождения"),
            N("Brake service", "Обслуживание тормозов"),
            N("Engine repair", "Ремонт двигателя"),
            N("Engine adjustment", "Регулировка двигателя"),
            N("Engine tuning", "Настройка двигателя"),
            N("Windshield replacement", "Замена лобового стекла"),
            N("Suspension service", "Обслуживание подвески"),
            N("Install roll cage", "Установка каркаса безопасности"),
            N("Remove roll cage", "Снятие каркаса безопасности"),
            N("N2O bottle fill", "Заправка баллона N2O"),
            N("Final gear ratio", "Передаточное число главной пары"),
            N("Regular paint", "Обычная окраска"),
            N("Metallic paint", "Окраска металликом"),
            N("Artist paint", "Художественная окраска"),
            N("GT paint", "Окраска GT"),
            N("Regular rim paint", "Обычная окраска дисков"),
            N("Metallic rim paint", "Окраска дисков металликом"),
            N("Rim polish", "Полировка дисков"),
            N("Standard tires", "Стандартные шины"),
            N("Gommer Gobra tires", "Шины Gommer Gobra"),
            N("Europeiska tires", "Шины Europeiska"),
            N("Sutasiko tires", "Шины Sutasiko"),
            N("Vehicle inspection", "Техосмотр автомобиля"),
        };

        private static readonly LocalizedText[] ActionLabels =
        {
            N("TAKE", "ВЗЯТЬ", "Поднять", "ПОДНЯТЬ"),
            N("INSTALL", "УСТАНОВИТЬ", "Установить деталь"),
            N("REMOVE", "СНЯТЬ"),
            N("RELEASE", "ОТПУСТИТЬ"),
            N("THROW", "БРОСИТЬ"),
            N("ROTATE", "ВРАЩАТЬ"),
            N("STOW TOOL", "УБРАТЬ ИНСТРУМЕНТ"),
            N("TIGHTEN", "ЗАТЯНУТЬ"),
            N("LOOSEN", "ОСЛАБИТЬ"),
            N("OPEN", "ОТКРЫТЬ"),
            N("CLOSE", "ЗАКРЫТЬ"),
            N("TURN ON", "ВКЛЮЧИТЬ"),
            N("TURN OFF", "ВЫКЛЮЧИТЬ"),
            N("USE", "ИСПОЛЬЗОВАТЬ"),
            N("DRINK", "ВЫПИТЬ"),
            N("EAT", "СЪЕСТЬ"),
            N("LIGHT", "ЗАЖЕЧЬ", "Поджечь"),
            N("ADD CHARCOAL", "НАСЫПАТЬ УГОЛЬ"),
            N("TAKE OUT", "ДОСТАТЬ"),
            N("CHANGE MODE", "СМЕНИТЬ РЕЖИМ"),
            N("TALK", "ПОГОВОРИТЬ"),
            N("SLEEP", "СПАТЬ"),
            N("BUY", "КУПИТЬ"),
            N("ADD TO BASKET", "ДОБАВИТЬ В КОРЗИНУ"),
            N("PAY", "ОПЛАТИТЬ"),
            N("REFUEL", "ЗАПРАВИТЬ"),
            N("WASH", "УМЫТЬСЯ"),
            N("RAISE", "ПОДНЯТЬ"),
            N("LOWER", "ОПУСТИТЬ"),
            N("ROLL", "КАТИТЬ"),
            N("CARRY", "НЕСТИ"),
            N("SUBMIT", "ОТПРАВИТЬ"),
            N("SWITCH", "ПЕРЕКЛЮЧИТЬ"),
            N("ADD STEAM", "ПОДДАТЬ ПАРУ"),
            N("EXTINGUISH", "ПОГАСИТЬ"),
            N("INCREASE", "УВЕЛИЧИТЬ"),
            N("DECREASE", "УМЕНЬШИТЬ"),
            N("INCREASE TOE", "УВЕЛИЧИТЬ СХОЖДЕНИЕ"),
            N("DECREASE TOE", "УМЕНЬШИТЬ СХОЖДЕНИЕ"),
            N("ENTER BUS", "СЕСТЬ В АВТОБУС"),
            N("EXIT BUS", "ВЫЙТИ ИЗ АВТОБУСА"),
            N("ORDER SERVICE", "ЗАКАЗАТЬ УСЛУГУ"),
            N("INSPECT VEHICLE", "ПРОЙТИ ТЕХОСМОТР"),
            N("RESCUE SUSKI", "ВЫТАЩИТЬ СУСКИ"),
            N("WAVE", "ПОМАХАТЬ"),
            N("GIVE THE FINGER", "ПОКАЗАТЬ ФАК"),
            N("SWEAR", "ВЫРУГАТЬСЯ"),
            N("EMPTY", "ПУСТО"),
        };

        private static readonly LocalizedText[] StatusTexts =
        {
            N("You are drinking.", "Вы пьёте."),
            N("You are eating.", "Вы едите."),
            N("You are smoking.", "Вы курите."),
            N("Food eaten.", "Еда съедена."),
            N("Item used.", "Предмет использован."),
            N("You slept.", "Вы поспали."),
            N("You washed.", "Вы умылись."),
            N("You drank water.", "Вы выпили воды."),
            N("You are urinating.", "Вы мочитесь."),
            N("Bladder emptied.", "Мочевой пузырь опустошён."),
            N("Tap opened.", "Кран открыт."),
            N("Tap closed.", "Кран закрыт."),
            N("Refrigerator turned on.", "Холодильник включён."),
            N("Refrigerator turned off.", "Холодильник выключен."),
            N("Stove turned on.", "Плита включена."),
            N("Stove turned off.", "Плита выключена."),
            N("Television turned on.", "Телевизор включён."),
            N("Television turned off.", "Телевизор выключен."),
            N("Fireplace lit.", "Камин разожжён."),
            N("Fireplace extinguished.", "Камин погашен."),
            N("Grill lit.", "Мангал разожжён."),
            N("Grill extinguished.", "Мангал погашен."),
            N("Part installed.", "Деталь установлена."),
            N("Part removed.", "Деталь снята."),
            N("Added to basket.", "Добавлено в корзину"),
            N("Purchases paid.", "Покупки оплачены"),
            N("Order paid.", "Заказ оплачен"),
            N("Closed right now.", "Сейчас закрыто"),
            N("Out of stock.", "Товар закончился"),
            N("Basket is empty.", "Корзина пуста"),
            N("Not enough money.", "Не хватает денег"),
            N("Paid; collection pending.", "Оплачено, выдача ожидается"),
            N("The item cannot be collected yet.", "Товар пока нельзя выдать"),
            N("Service unavailable: no service vehicle.",
                "Услуга недоступна: нет сервисного автомобиля"),
            N("Inspection unavailable: no vehicle.",
                "Техосмотр недоступен: нет автомобиля"),
            N("Action unavailable.", "Действие недоступно"),
            N("The shop is closed.", "Магазин закрыт"),
            N("Not in stock.", "Нет в наличии"),
            N("The checkout is closed.", "Касса закрыта"),
            N("The pub is closed.", "Паб закрыт"),
            N("There is nothing to refuel.", "Нечего заправлять"),
            N("Fuel receiver unavailable.", "Топливный приёмник недоступен"),
            N("Invalid refuelling amount.", "Неверный объём заправки"),
            N("The receiver rejected the fuel.", "Приёмник не принял топливо"),
            N("The receiver returned an invalid fuel amount.",
                "Приёмник вернул неверный объём топлива"),
            N("No fuel has been dispensed yet.", "Топливо ещё не подавалось"),
            N("Fuel nozzle returned.", "Топливный пистолет возвращён"),
            N("Service unavailable: no service car.",
                "Недоступно: нет сервисного авто"),
            N("Fleetari order accepted.", "Заказ Флитари принят"),
            N("Inspection unavailable.", "Техосмотр недоступен"),
            N("Inspection completed.", "Техосмотр завершён"),
            N("Only the order envelope fits in this box.",
                "В ящик нужен именно конверт заказа"),
            N("This envelope does not belong to the current order.",
                "Этот конверт не относится к текущему заказу"),
            N("Envelope sent. Teimo will let you know when the parts arrive.",
                "Конверт отправлен. Теймо сообщит, когда детали приедут"),
            N("No mail order is ready right now.",
                "Готового почтового заказа сейчас нет"),
            N("Order paid; parts collected from the mail counter.",
                "Заказ оплачен — детали выданы у почтовой стойки"),
            N("Not enough money for the mail order.",
                "Не хватает денег на почтовый заказ"),
            N("Teimo is not handling mail right now.",
                "Теймо сейчас не обслуживает почту"),
            N("Payment succeeded, but collecting the parts needs another try.",
                "Оплата прошла, но выдача деталей ждёт повторной попытки"),
            N("Water routed to the shower.", "Вода направлена в душ."),
            N("Water routed to the tap.", "Вода направлена в кран."),
            N("Shower valve opened.", "Вентиль душа открыт."),
            N("Shower valve closed.", "Вентиль душа закрыт."),
            N("Electric sauna turned on.", "Электрическая сауна включена."),
            N("Electric sauna turned off.", "Электрическая сауна выключена."),
            N("Sauna timer set.", "Таймер сауны установлен."),
            N("Sauna timer turned off.", "Таймер сауны выключен."),
            N("Water thrown on the sauna stones.", "На камни подана вода."),
            N("Using the toilet.", "Использование туалета начато."),
            N("Home adjustment unavailable.",
                "Домашняя регулировка недоступна."),
            N("Invalid adjustment amount.",
                "Величина регулировки недействительна."),
            N("Home adjustment is not supported.",
                "Домашняя регулировка не поддерживается."),
            N("The tap is already open.", "Кран уже открыт."),
            N("The tap is already closed.", "Кран уже закрыт."),
            N("Open the tap first.", "Сначала откройте кран."),
            N("Player needs are not ready.",
                "Потребности игрока не готовы."),
            N("The sauna stove is not heating.",
                "Печь сауны не нагревается."),
            N("The sauna stones are not hot enough yet.",
                "Камни сауны ещё недостаточно нагреты."),
        };

        private static readonly Dictionary<string, LocalizedText>
            DisplayByAlias = BuildAliasIndex(DisplayNames);
        private static readonly Dictionary<string, LocalizedText>
            ActionByAlias = BuildAliasIndex(ActionLabels);
        private static readonly Dictionary<string, LocalizedText>
            StatusByAlias = BuildAliasIndex(StatusTexts);

        public static bool IsRussianLocale(string localeId) =>
            !string.IsNullOrWhiteSpace(localeId) &&
            localeId.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

        public static string NormalizeLocaleId(string localeId) =>
            IsRussianLocale(localeId) ? RussianLocaleId : EnglishLocaleId;

        public static string LocalizeBindingLabel(
            string label,
            string localeId)
        {
            string normalized = label?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            bool russian = IsRussianLocale(localeId);
            return normalized.ToUpperInvariant() switch
            {
                "ЛКМ" or "LMB" => russian ? "ЛКМ" : "LMB",
                "ПКМ" or "RMB" => russian ? "ПКМ" : "RMB",
                "СКМ" or "MMB" => russian ? "СКМ" : "MMB",
                "КОЛЕСО" or "WHEEL" => russian ? "КОЛЕСО" : "WHEEL",
                "ПРОБЕЛ" or "SPACE" => russian ? "ПРОБЕЛ" : "SPACE",
                _ => normalized.ToUpperInvariant(),
            };
        }

        public static string LocalizeActionLabel(
            string label,
            InteractionActionBinding binding,
            string localeId)
        {
            string normalized = label?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            string folded = normalized.ToLowerInvariant();
            if (folded.Contains("домкрат") || folded.Contains("jack"))
            {
                return Select(binding switch
                {
                    InteractionActionBinding.Interact =>
                        new LocalizedText("ROLL", "КАТИТЬ"),
                    InteractionActionBinding.Throw =>
                        new LocalizedText("LOWER", "ОПУСТИТЬ"),
                    _ => new LocalizedText("RAISE", "ПОДНЯТЬ"),
                }, localeId);
            }

            bool combinedDoor =
                (folded.Contains("откры") || folded.Contains("open")) &&
                (folded.Contains("закры") || folded.Contains("close"));
            if (combinedDoor)
            {
                return Select(
                    binding == InteractionActionBinding.Throw
                        ? new LocalizedText("CLOSE", "ЗАКРЫТЬ")
                        : new LocalizedText("OPEN", "ОТКРЫТЬ"),
                    localeId);
            }

            bool combinedRaiseLower =
                (folded.Contains("поднять") || folded.Contains("raise")) &&
                (folded.Contains("опустить") || folded.Contains("lower"));
            if (combinedRaiseLower)
            {
                return Select(
                    binding == InteractionActionBinding.Throw
                        ? new LocalizedText("LOWER", "ОПУСТИТЬ")
                        : new LocalizedText("RAISE", "ПОДНЯТЬ"),
                    localeId);
            }

            if (TryResolve(ActionByAlias, normalized, out LocalizedText exact))
            {
                return Select(exact, localeId);
            }

            LocalizedText inferred = InferAction(folded);
            if (inferred.IsValid)
            {
                return Select(inferred, localeId);
            }

            bool russian = IsRussianLocale(localeId);
            if (russian != ContainsCyrillic(normalized))
            {
                return russian ? "ДЕЙСТВИЕ" : "INTERACT";
            }

            return normalized;
        }

        public static string LocalizeDisplayName(
            string localizationKey,
            string displayName,
            string localeId)
        {
            string normalized = displayName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (TryLocalizeWrenchName(normalized, localeId, out string wrench))
            {
                return wrench;
            }

            if (string.Equals(
                    normalized,
                    "Каталог запчастей",
                    StringComparison.OrdinalIgnoreCase))
            {
                return IsRussianLocale(localeId)
                    ? "Каталог запчастей"
                    : "Parts catalog";
            }

            if (TryResolveDecoratedDisplayName(
                    normalized,
                    localeId,
                    out string decorated))
            {
                return decorated;
            }

            if (!string.IsNullOrEmpty(localizationKey) &&
                localizationKey.StartsWith(
                    "fastener.",
                    StringComparison.OrdinalIgnoreCase))
            {
                return LocalizeFastenerName(normalized, localeId);
            }

            if (TryResolveLocalizationKey(
                    localizationKey,
                    out LocalizedText keyed))
            {
                return Select(keyed, localeId);
            }

            if (TryResolve(
                    DisplayByAlias,
                    normalized,
                    out LocalizedText exact))
            {
                return Select(exact, localeId);
            }

            // Never expose known mojibake or prototype labels in the player
            // HUD. Unknown authored prose is preserved for compatibility.
            if (normalized.Contains("РќР", StringComparison.Ordinal) ||
                normalized.Contains("M06 logical", StringComparison.OrdinalIgnoreCase))
            {
                return IsRussianLocale(localeId) ? "Деталь" : "Part";
            }

            return normalized;
        }

        public static string LocalizeSubtitle(
            string subtitle,
            string localeId)
        {
            string normalized = subtitle?.Trim() ?? string.Empty;
            if (TryResolve(StatusByAlias, normalized, out LocalizedText text))
            {
                return Select(text, localeId);
            }

            const string russianFuelPrefix = "Принято топлива:";
            const string englishFuelPrefix = "Fuel accepted:";
            if (normalized.StartsWith(
                    russianFuelPrefix,
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(
                    englishFuelPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                int separator = normalized.IndexOf(':');
                string amount = separator >= 0
                    ? normalized.Substring(separator + 1).Trim()
                    : string.Empty;
                if (!IsRussianLocale(localeId))
                {
                    amount = ReplaceOrdinalIgnoreCase(amount, " л", " l");
                }

                return (IsRussianLocale(localeId)
                    ? russianFuelPrefix
                    : englishFuelPrefix) + " " + amount;
            }

            return normalized;
        }

        public static bool CanLocalizeDisplayName(string displayName) =>
            TryResolve(
                DisplayByAlias,
                displayName?.Trim() ?? string.Empty,
                out _);

        private static bool TryResolveDecoratedDisplayName(
            string source,
            string localeId,
            out string localized)
        {
            string prefix = string.Empty;
            string coreAndSuffix = source;
            int firstDoubleSpace = source.IndexOf("  ", StringComparison.Ordinal);
            if (firstDoubleSpace > 0 &&
                IsCounter(source.AsSpan(0, firstDoubleSpace)))
            {
                prefix = source.Substring(0, firstDoubleSpace + 2);
                coreAndSuffix = source.Substring(firstDoubleSpace + 2);
            }

            int separator = coreAndSuffix.IndexOf(" — ", StringComparison.Ordinal);
            string core = separator >= 0
                ? coreAndSuffix.Substring(0, separator)
                : coreAndSuffix;
            string suffix = separator >= 0
                ? coreAndSuffix.Substring(separator)
                : string.Empty;
            string trailingTag = string.Empty;
            int tagStart = core.LastIndexOf(" [", StringComparison.Ordinal);
            if (tagStart >= 0)
            {
                trailingTag = core.Substring(tagStart);
                core = core.Substring(0, tagStart);
            }

            if (!TryResolve(DisplayByAlias, core.Trim(), out LocalizedText entry))
            {
                localized = string.Empty;
                return false;
            }

            bool russian = IsRussianLocale(localeId);
            trailingTag = LocalizeDecoratedSuffix(trailingTag, russian);
            suffix = LocalizeDecoratedSuffix(suffix, russian);
            localized = prefix + Select(entry, localeId) + trailingTag + suffix;
            return true;
        }

        private static bool TryResolveLocalizationKey(
            string localizationKey,
            out LocalizedText entry)
        {
            string normalized = localizationKey?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                entry = default;
                return false;
            }

            if (TryResolve(DisplayByAlias, normalized, out entry))
            {
                return true;
            }

            int separator = normalized.LastIndexOf('.');
            string leaf = separator >= 0
                ? normalized.Substring(separator + 1)
                : normalized;
            leaf = leaf.Replace('-', ' ').Replace('_', ' ');
            return TryResolve(DisplayByAlias, leaf, out entry);
        }

        private static string LocalizeDecoratedSuffix(
            string source,
            bool russian)
        {
            string selected = russian ? "[выбрано]" : "[selected]";
            string onForm = russian ? "[в бланке]" : "[on form]";
            string result = ReplaceOrdinalIgnoreCase(
                source,
                "[выбрано]",
                selected);
            result = ReplaceOrdinalIgnoreCase(result, "[selected]", selected);
            result = ReplaceOrdinalIgnoreCase(result, "[в бланке]", onForm);
            result = ReplaceOrdinalIgnoreCase(result, "[on form]", onForm);
            return ReplaceOrdinalIgnoreCase(
                result,
                "MK/л",
                russian ? "MK/л" : "MK/l");
        }

        private static string ReplaceOrdinalIgnoreCase(
            string source,
            string oldValue,
            string newValue)
        {
            int match = source.IndexOf(
                oldValue,
                StringComparison.OrdinalIgnoreCase);
            if (match < 0)
            {
                return source;
            }

            var builder = new StringBuilder(source.Length);
            int start = 0;
            while (match >= 0)
            {
                builder.Append(source, start, match - start);
                builder.Append(newValue);
                start = match + oldValue.Length;
                match = source.IndexOf(
                    oldValue,
                    start,
                    StringComparison.OrdinalIgnoreCase);
            }

            builder.Append(source, start, source.Length - start);
            return builder.ToString();
        }

        private static bool TryLocalizeWrenchName(
            string source,
            string localeId,
            out string localized)
        {
            string folded = source.ToLowerInvariant();
            if (!folded.StartsWith("ключ ", StringComparison.Ordinal) &&
                !folded.StartsWith("wrench ", StringComparison.Ordinal))
            {
                localized = string.Empty;
                return false;
            }

            int firstDigit = -1;
            int digitCount = 0;
            for (int index = 0; index < source.Length; index++)
            {
                if (char.IsDigit(source[index]))
                {
                    if (firstDigit < 0)
                    {
                        firstDigit = index;
                    }
                    digitCount++;
                }
                else if (firstDigit >= 0)
                {
                    break;
                }
            }

            if (firstDigit < 0 || digitCount == 0)
            {
                localized = string.Empty;
                return false;
            }

            string size = source.Substring(firstDigit, digitCount);
            localized = IsRussianLocale(localeId)
                ? $"Ключ {size} мм"
                : $"Wrench {size} mm";
            return true;
        }

        private static bool IsCounter(ReadOnlySpan<char> value)
        {
            int slash = value.IndexOf('/');
            if (slash <= 0 || slash >= value.Length - 1)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (index != slash && !char.IsDigit(value[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string LocalizeFastenerName(
            string source,
            string localeId)
        {
            int number = ReadTrailingNumber(source);
            if (IsRussianLocale(localeId))
            {
                return number > 0 ? $"Крепёж №{number}" : "Крепёж";
            }

            return number > 0 ? $"Fastener {number}" : "Fastener";
        }

        private static int ReadTrailingNumber(string value)
        {
            int end = value.Length - 1;
            while (end >= 0 && !char.IsDigit(value[end]))
            {
                end--;
            }

            if (end < 0)
            {
                return 0;
            }

            int start = end;
            while (start > 0 && char.IsDigit(value[start - 1]))
            {
                start--;
            }

            return int.TryParse(
                value.Substring(start, end - start + 1),
                out int number)
                    ? number
                    : 0;
        }

        private static LocalizedText InferAction(string folded)
        {
            if (StartsWithAny(folded, "поднять", "pick up", "take"))
                return new LocalizedText("TAKE", "ВЗЯТЬ");
            if (StartsWithAny(folded, "установить", "install"))
                return new LocalizedText("INSTALL", "УСТАНОВИТЬ");
            if (StartsWithAny(folded, "снять", "remove", "detach"))
                return new LocalizedText("REMOVE", "СНЯТЬ");
            if (StartsWithAny(folded, "открыть", "open"))
                return new LocalizedText("OPEN", "ОТКРЫТЬ");
            if (StartsWithAny(folded, "закрыть", "close"))
                return new LocalizedText("CLOSE", "ЗАКРЫТЬ");
            if (StartsWithAny(folded, "включить", "turn on", "switch on"))
                return new LocalizedText("TURN ON", "ВКЛЮЧИТЬ");
            if (StartsWithAny(folded, "выключить", "turn off", "switch off"))
                return new LocalizedText("TURN OFF", "ВЫКЛЮЧИТЬ");
            if (StartsWithAny(folded, "пить", "выпить", "drink"))
                return new LocalizedText("DRINK", "ВЫПИТЬ");
            if (StartsWithAny(folded, "есть", "съесть", "eat"))
                return new LocalizedText("EAT", "СЪЕСТЬ");
            if (StartsWithAny(folded, "использовать", "use"))
                return new LocalizedText("USE", "ИСПОЛЬЗОВАТЬ");
            if (StartsWithAny(folded, "переключить", "switch"))
                return new LocalizedText("SWITCH", "ПЕРЕКЛЮЧИТЬ");
            if (StartsWithAny(folded, "поддать пару", "add steam"))
                return new LocalizedText("ADD STEAM", "ПОДДАТЬ ПАРУ");
            if (StartsWithAny(folded, "погасить", "extinguish"))
                return new LocalizedText("EXTINGUISH", "ПОГАСИТЬ");
            if (StartsWithAny(folded, "разжечь", "зажечь", "поджечь", "light"))
                return new LocalizedText("LIGHT", "ЗАЖЕЧЬ");
            if (StartsWithAny(folded, "забрать", "достать", "take out", "collect"))
                return new LocalizedText("TAKE OUT", "ДОСТАТЬ");
            if (StartsWithAny(folded, "опустить конверт", "submit"))
                return new LocalizedText("SUBMIT", "ОТПРАВИТЬ");
            if (StartsWithAny(folded, "перенести", "carry"))
                return new LocalizedText("CARRY", "НЕСТИ");
            if (StartsWithAny(folded, "сменить", "change mode"))
                return new LocalizedText("CHANGE MODE", "СМЕНИТЬ РЕЖИМ");
            if (StartsWithAny(folded, "катить", "roll"))
                return new LocalizedText("ROLL", "КАТИТЬ");
            if (StartsWithAny(folded, "опустить", "lower"))
                return new LocalizedText("LOWER", "ОПУСТИТЬ");
            if (StartsWithAny(folded, "добавить в корзину", "add to basket"))
                return new LocalizedText("ADD TO BASKET", "ДОБАВИТЬ В КОРЗИНУ");
            if (StartsWithAny(folded, "оплатить", "pay"))
                return new LocalizedText("PAY", "ОПЛАТИТЬ");
            if (StartsWithAny(folded, "купить", "buy"))
                return new LocalizedText("BUY", "КУПИТЬ");
            if (StartsWithAny(folded, "заправить", "refuel"))
                return new LocalizedText("REFUEL", "ЗАПРАВИТЬ");
            if (StartsWithAny(folded, "поговорить", "talk"))
                return new LocalizedText("TALK", "ПОГОВОРИТЬ");
            if (StartsWithAny(folded, "спать", "sleep"))
                return new LocalizedText("SLEEP", "СПАТЬ");
            if (StartsWithAny(folded, "заказать услугу", "order service"))
                return new LocalizedText("ORDER SERVICE", "ЗАКАЗАТЬ УСЛУГУ");
            if (StartsWithAny(folded, "пройти техосмотр", "inspect vehicle"))
                return new LocalizedText("INSPECT VEHICLE", "ПРОЙТИ ТЕХОСМОТР");
            if (StartsWithAny(folded, "вытащить суски", "rescue suski"))
                return new LocalizedText("RESCUE SUSKI", "ВЫТАЩИТЬ СУСКИ");
            if (StartsWithAny(folded, "увеличить", "increase"))
                return new LocalizedText("INCREASE", "УВЕЛИЧИТЬ");
            if (StartsWithAny(folded, "уменьшить", "decrease"))
                return new LocalizedText("DECREASE", "УМЕНЬШИТЬ");
            return default;
        }

        private static bool ContainsCyrillic(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if ((character >= '\u0400' && character <= '\u04ff') ||
                    character == '\u2116')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StartsWithAny(string value, params string[] prefixes)
        {
            for (int index = 0; index < prefixes.Length; index++)
            {
                if (value.StartsWith(prefixes[index], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolve(
            IReadOnlyDictionary<string, LocalizedText> index,
            string value,
            out LocalizedText entry)
        {
            entry = default;
            return !string.IsNullOrWhiteSpace(value) &&
                   index.TryGetValue(value.Trim(), out entry);
        }

        private static Dictionary<string, LocalizedText> BuildAliasIndex(
            IEnumerable<LocalizedText> entries)
        {
            var result = new Dictionary<string, LocalizedText>(
                StringComparer.OrdinalIgnoreCase);
            foreach (LocalizedText entry in entries)
            {
                AddAlias(result, entry.English, entry);
                AddAlias(result, entry.Russian, entry);
                for (int index = 0; index < entry.Aliases.Length; index++)
                {
                    AddAlias(result, entry.Aliases[index], entry);
                }
            }

            return result;
        }

        private static void AddAlias(
            IDictionary<string, LocalizedText> index,
            string alias,
            LocalizedText entry)
        {
            if (!string.IsNullOrWhiteSpace(alias) &&
                !index.ContainsKey(alias.Trim()))
            {
                index.Add(alias.Trim(), entry);
            }
        }

        private static string Select(LocalizedText entry, string localeId) =>
            IsRussianLocale(localeId) ? entry.Russian : entry.English;

        private static LocalizedText N(
            string english,
            string russian,
            params string[] aliases) =>
            new LocalizedText(english, russian, aliases);

        private readonly struct LocalizedText
        {
            public LocalizedText(
                string english,
                string russian,
                params string[] aliases)
            {
                English = english ?? string.Empty;
                Russian = russian ?? string.Empty;
                Aliases = aliases ?? Array.Empty<string>();
            }

            public string English { get; }
            public string Russian { get; }
            public string[] Aliases { get; }
            public bool IsValid =>
                !string.IsNullOrEmpty(English) &&
                !string.IsNullOrEmpty(Russian);
        }
    }
}
