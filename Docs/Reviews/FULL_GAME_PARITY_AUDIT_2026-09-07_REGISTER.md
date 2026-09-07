# Полный построчный реестр аудита — 7 сентября 2026

**Все 534 FeatureId: 517 Required=Yes и 17 Required=No.** Основные выводы, метод и ограничения: [полный отчёт](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Reviews/FULL_GAME_PARITY_AUDIT_2026-09-07.md>).

«Оценка аудита» описывает установленную реализацию сейчас. «CSV Status» и «CSV save» сохранены буквально из исходного учёта, который частично отстаёт. Никакого нового Verified этим реестром не присваивается. В строке также указан milestone-владелец из матрицы; это ownership, не утверждение о его закрытии. Общий результат и недостающая работа описаны в справочнике групп ниже.

## Save — 6 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.SAVE.001 | Версионированный native save document и schema | Реализовано; полный паритет не принят; `save-core` | ImplementedUnverified | Covered | 09A |
| P1.SAVE.002 | Слоты New Game / Continue / Load | Реализовано; полный паритет не принят; `save-core` | ImplementedUnverified | Covered | 09A |
| P1.SAVE.003 | Атомарная запись, backup, quarantine и recovery | Реализовано; полный паритет не принят; `save-core` | ImplementedUnverified | Covered | 09A |
| P1.SAVE.004 | Миграции schema и transaction-safe apply | Реализовано; полный паритет не принят; `save-core` | ImplementedUnverified | Covered | 09A |
| P1.SAVE.005 | Stable entity snapshots включая unloaded cells | Работает частично; `save-world` | PartiallyImplemented | Covered | 09A |
| P1.SAVE.006 | Fresh / mid-game / late-game cross-domain round trips | Работает частично; `save-world` | PartiallyImplemented | Partial | 09A |

## Player — 15 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.PLAYER.001 | Ходьба и analog movement | Работает частично; `player` | PartiallyImplemented | Partial | 09C |
| P1.PLAYER.002 | Бег с donor-compatible input, скоростью и needs cost | Работает частично; `player` | EvidenceCaptured | Planned | 09C |
| P1.PLAYER.003 | Первый уровень приседа | Работает частично; `player` | PartiallyImplemented | Partial | 09C |
| P1.PLAYER.004 | Второй / глубокий уровень приседа | Работает частично; `player` | EvidenceCaptured | Planned | 09C |
| P1.PLAYER.005 | Наклон вперёд пешком | Работает частично; `player` | PartiallyImplemented | NotRequired | 09C |
| P1.PLAYER.006 | Поворот камеры и ограничения взгляда | Работает частично; `player` | PartiallyImplemented | Partial | 09C |
| P1.PLAYER.007 | Надёжное прохождение порогов и ступеней | Работает частично; `player` | PartiallyImplemented | Partial | 09C |
| P1.PLAYER.008 | Гравитация, slopes, grounding, jump и fall/land behavior | Реализовано; полный паритет не принят; `player-mass` | ImplementedUnverified | Planned | 09C |
| P1.PLAYER.009 | Project-owned locomotion state для audio/needs/UI/save | Работает частично; `player` | EvidenceCaptured | Planned | 09C |
| P1.PLAYER.010 | Контекстное взаимодействие лучом | Работает частично; `interaction` | PartiallyImplemented | Partial | 09C |
| P1.PLAYER.011 | Поднять, нести, вращать, поставить и бросить предмет | Работает частично; `interaction` | PartiallyImplemented | Partial | 09C |
| P1.PLAYER.012 | Использование инструментов и capability targets | Работает частично; `interaction` | PartiallyImplemented | Partial | 09C |
| P1.PLAYER.013 | Вход/выход и управление seated vehicle state | Начато; есть основа или предмет; `seating` | EvidenceCaptured | Planned | 11A |
| P1.PLAYER.014 | Наклон влево/вправо в машине через открытое окно или дверь | Только спецификация; `car-lean` | Specified | NotRequired | 11A |
| P1.PLAYER.016 | Ручной мат, фак с голосом и event-driven ругань | Работает частично; `swearing` | PartiallyImplemented | NotRequired | 09C |

## Needs — 15 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.NEEDS.001 | Жажда | Работает частично; `needs` | EvidenceCaptured | Planned | 09C |
| P1.NEEDS.002 | Голод | Работает частично; `needs` | EvidenceCaptured | Planned | 09C |
| P1.NEEDS.003 | Стресс | Работает частично; `needs` | PartiallyImplemented | Partial | 09C |
| P1.NEEDS.004 | Усталость | Работает частично; `needs` | PartiallyImplemented | Partial | 09C |
| P1.NEEDS.005 | Нужда в мочеиспускании | Работает частично; `needs` | EvidenceCaptured | Planned | 09C |
| P1.NEEDS.006 | Нечистоплотность | Работает частично; `needs` | EvidenceCaptured | Planned | 09C |
| P1.NEEDS.007 | Алкогольное опьянение и похмелье | Работает частично; `alcohol` | PartiallyImplemented | Partial | 09C |
| P1.NEEDS.008 | Еда и насыщение | Работает частично; `needs` | EvidenceCaptured | Planned | 09C |
| P1.NEEDS.009 | Питьё и hydration | Работает частично; `needs` | PartiallyImplemented | Partial | 09C |
| P1.NEEDS.010 | Курение и stress effects | Работает частично; `alcohol` | PartiallyImplemented | Partial | 09C |
| P1.NEEDS.011 | Кофе и fatigue effects | Начато; есть основа или предмет; `coffee` | EvidenceCaptured | Planned | 09C |
| P1.NEEDS.012 | Мочеиспускание в мире | Работает частично; `urination-sleep` | PartiallyImplemented | Partial | 09C |
| P1.NEEDS.013 | Сон, пробуждение и time advance | Работает частично; `urination-sleep` | PartiallyImplemented | Partial | 09C |
| P1.NEEDS.014 | Здоровье, болезнь, injury и death triggers | Игровая реализация не найдена; `health` | EvidenceCaptured | Planned | 09C |
| P1.NEEDS.015 | Weight and locomotion coupling | Реализовано; полный паритет не принят; `player-mass` | ImplementedUnverified | Planned | 09C |

## Home — 19 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.HOME.001 | Домашнее электричество, выключатели и приборы | Работает частично; `home` | PartiallyImplemented | Partial | 09C |
| P1.HOME.002 | Вода, краны и душ | Работает частично; `home` | PartiallyImplemented | Partial | 09C |
| P1.HOME.003 | Сауна: печь, камни, температура и пар | Работает частично; `sauna` | PartiallyImplemented | Partial | 09C |
| P1.HOME.004 | Дрова и огонь печи/камина | Работает частично; `fire` | PartiallyImplemented | Partial | 09C |
| P1.HOME.005 | Сон в доступных местах | Работает частично; `beds` | PartiallyImplemented | Partial | 09C |
| P1.HOME.006 | Холодильник/хранение еды и бытовые контейнеры | Работает частично; `food` | PartiallyImplemented | Partial | 09C |
| P1.HOME.007 | Туалет и бытовая гигиена | Работает частично; `home` | PartiallyImplemented | Partial | 09C |
| P1.HOME.008 | Телевизор/радио как домашние устройства | Начато; есть основа или предмет; `home-media` | PartiallyImplemented | Partial | 09C |
| P1.HOME.009 | Коттедж и его бытовые взаимодействия | Начато; есть основа или предмет; `cottage` | EvidenceCaptured | Planned | 09C |
| P1.HOME.010 | Домашние двери, окна и доступ | Работает частично; `doors` | PartiallyImplemented | Partial | 09C |
| P1.HOME.101 | Food spoilage and refrigerator modifier | Работает частично; `food` | EvidenceCaptured | Planned | 09C |
| P1.HOME.102 | Stove cooking | Работает частично; `food` | EvidenceCaptured | Planned | 09C |
| P1.HOME.103 | Portable grill cooking | Работает частично; `grill` | EvidenceCaptured | Planned | 09C |
| P1.HOME.104 | Electric sauna heating | Работает частично; `sauna` | PartiallyImplemented | Partial | 09C |
| P1.HOME.105 | Wood sauna heating/fire | Игровая реализация не найдена; `wood-sauna` | EvidenceCaptured | Planned | 09C |
| P1.HOME.106 | Kilju brewing vessel and fermentation | Начато; есть основа или предмет; `kilju` | EvidenceCaptured | Planned | 09C |
| P1.HOME.107 | Fish trap placement/catch loop | Начато; есть основа или предмет; `fish-trap` | EvidenceCaptured | Planned | 09C |
| P1.HOME.108 | Coffee brewing and serving | Начато; есть основа или предмет; `coffee` | EvidenceCaptured | Planned | 09C |
| P1.HOME.109 | Mosquito repellent use/effect | Начато; есть основа или предмет; `repellent` | EvidenceCaptured | Planned | 09C |

## Items — 99 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.ITEM.001 | Ручные инструменты и spanner/tool sets | Работает частично; `tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.002 | Домкраты, подъёмник и опоры | Начато; есть основа или предмет; `lifting-tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.003 | Топор, кувалда, лом и рабочие инструменты | Начато; есть основа или предмет; `work-tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.004 | Канистры топлива и двухтактной смеси | Работает частично; `liquids` | PartiallyImplemented | Partial | 12A |
| P1.ITEM.005 | Масло, coolant и brake fluid containers | Работает частично; `liquids` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.006 | Вода, ведро, ковш и sauna containers | Работает частично; `liquids` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.007 | Пиво, booze, cigarettes и coffee | Работает частично; `consumption-items` | PartiallyImplemented | Partial | 12A |
| P1.ITEM.008 | Еда и готовые упаковки | Работает частично; `food` | PartiallyImplemented | Partial | 12A |
| P1.ITEM.009 | Рыба, мясо и perishables | Работает частично; `food` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.010 | Kilju ingredients и juice containers | Начато; есть основа или предмет; `kilju` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.011 | Батареи, свечи, лампы, предохранители | Работает частично; `vehicle-consumables` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.012 | Огнетушитель и safety supplies | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.013 | Дрова, wood carrier и физические грузы | Начато; есть основа или предмет; `work-tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.014 | Radio, flashlight, lantern и portable devices | Работает частично; `devices` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.015 | CD, diskette, magazine и media objects | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.016 | Furniture и movable household objects | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.017 | Vehicle service packages and ordered parts | Работает частично; `mail` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.018 | Garbage, empty packaging and disposable states | Работает частично; `waste` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.019 | Generic physical item identity, placement and recovery | Работает частично; `item-identity` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.020 | Liquid quantity, pour/fill/spill and container state | Работает частично; `liquids` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.101 | Sausages package — concrete definition/state | Работает частично; `food` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.102 | Macaroni box — concrete definition/state | Работает частично; `food` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.103 | Pizza — concrete definition/state | Работает частично; `food` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.104 | Potato chips — concrete definition/state | Работает частично; `food` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.105 | Milk — concrete definition/state | Работает частично; `food` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.106 | Yeast — concrete definition/state | Начато; есть основа или предмет; `kilju` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.107 | Sugar — concrete definition/state | Начато; есть основа или предмет; `kilju` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.108 | Juice concentrate — concrete definition/state | Начато; есть основа или предмет; `kilju` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.109 | Coffee package — concrete definition/state | Начато; есть основа или предмет; `coffee` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.110 | Charcoal — concrete definition/state | Работает частично; `grill` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.111 | Cigarettes — concrete definition/state | Работает частично; `consumption-items` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.112 | Mosquito spray — concrete definition/state | Начато; есть основа или предмет; `repellent` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.113 | Beer case — concrete definition/state | Работает частично; `consumption-items` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.114 | Beer bottle — concrete definition/state | Работает частично; `consumption-items` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.115 | Booze bottle — concrete definition/state | Работает частично; `consumption-items` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.116 | Loose sausage — concrete definition/state | Работает частично; `food` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.117 | Sausage and potatoes meal — concrete definition/state | Работает частично; `food` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.118 | Pike — concrete definition/state | Работает частично; `food` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.119 | Moose meat — concrete definition/state | Работает частично; `food` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.120 | Brake fluid — concrete definition/state | Работает частично; `liquids` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.121 | Coolant — concrete definition/state | Работает частично; `liquids` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.122 | Motor oil — concrete definition/state | Работает частично; `liquids` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.123 | Two-stroke oil — concrete definition/state | Работает частично; `liquids` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.124 | Alternator belt — concrete definition/state | Работает частично; `vehicle-consumables` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.125 | Oil filter — concrete definition/state | Работает частично; `vehicle-consumables` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.126 | Sparkplug box — concrete definition/state | Работает частично; `vehicle-consumables` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.127 | Lightbulb box — concrete definition/state | Работает частично; `vehicle-consumables` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.128 | Fuse package — concrete definition/state | Начато; есть основа или предмет; `battery` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.129 | R20 battery — concrete definition/state | Начато; есть основа или предмет; `battery` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.130 | Car battery — concrete definition/state | Начато; есть основа или предмет; `battery` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.131 | Fire extinguisher — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.132 | Spray paint variants — concrete definition/state | Работает частично; `paint` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.133 | Gasoline can — concrete definition/state | Работает частично; `liquids` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.134 | Diesel can — concrete definition/state | Работает частично; `liquids` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.135 | Axe — concrete definition/state | Начато; есть основа или предмет; `work-tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.136 | Digging bar — concrete definition/state | Начато; есть основа или предмет; `work-tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.137 | Sledgehammer — concrete definition/state | Начато; есть основа или предмет; `work-tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.138 | Spanner set — concrete definition/state | Работает частично; `tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.139 | Wiring mess — concrete definition/state | Работает частично; `tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.140 | Car jack — concrete definition/state | Работает частично; `jacks` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.141 | Floor jack — concrete definition/state | Работает частично; `jacks` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.142 | Motor hoist — concrete definition/state | Начато; есть основа или предмет; `lifting-tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.143 | Kilju bucket — concrete definition/state | Начато; есть основа или предмет; `kilju` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.144 | Kilju lid — concrete definition/state | Начато; есть основа или предмет; `kilju` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.145 | Sauna bucket — concrete definition/state | Работает частично; `liquids` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.146 | Sauna dipper — concrete definition/state | Работает частично; `liquids` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.147 | Wood carrier — concrete definition/state | Начато; есть основа или предмет; `work-tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.148 | Garbage barrel — concrete definition/state | Работает частично; `waste` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.149 | Portable grill — concrete definition/state | Работает частично; `grill` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.150 | Coffee pan — concrete definition/state | Начато; есть основа или предмет; `coffee` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.151 | Coffee cup — concrete definition/state | Начато; есть основа или предмет; `coffee` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.152 | Fish trap — concrete definition/state | Начато; есть основа или предмет; `fish-trap` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.153 | Flashlight — concrete definition/state | Работает частично; `devices` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.154 | Lantern — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.155 | Helmet — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.156 | Radar detector — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.157 | Portable radio — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.158 | Camera — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.159 | Floppy disk — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.160 | CD set — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.161 | Notepad — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.162 | Parts magazine — concrete definition/state | Работает частично; `mail` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.163 | TV remote — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.164 | Audio CD 01 — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.165 | Audio CD 02 — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.166 | Audio CD 03 — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.167 | Audio CD case 01 — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.168 | Audio CD case 02 — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.169 | Audio CD case 03 — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.170 | Sofa — concrete definition/state | Работает частично; `beds` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.171 | Basketball — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.172 | Football — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.173 | Firewood definition — concrete definition/state | Начато; есть основа или предмет; `work-tools` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.174 | Shopping bag — concrete definition/state | Работает частично; `shopping-bag` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.175 | Berry box — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.176 | Fireworks bag — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.177 | Suitcase — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.178 | Rocket/firework object — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |
| P1.ITEM.179 | Xmas lights — concrete definition/state | Начато; есть основа или предмет; `item-base` | PartiallyImplemented | Partial | 09B |

## Satsuma — 20 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.CAR.001 | Полный project-owned part catalog и stable identities | Работает частично; `assembly` | PartiallyImplemented | Partial | 11A |
| P1.CAR.002 | Полный assembly graph, mount points и compatibility | Работает частично; `assembly` | PartiallyImplemented | Partial | 11A |
| P1.CAR.003 | Болты, гайки, затяжка и tool-size rules | Работает частично; `assembly` | PartiallyImplemented | Partial | 11A |
| P1.CAR.004 | Электрическая проводка и connected state | Работает частично; `wiring` | EvidenceCaptured | Planned | 11A |
| P1.CAR.005 | Топливо, масло, coolant, brake/clutch fluids | Работает частично; `car-fluids` | EvidenceCaptured | Planned | 11A |
| P1.CAR.006 | Двигатель: запуск, combustion, stall и RPM/load | Работает частично; `engine` | PartiallyImplemented | Partial | 11A |
| P1.CAR.007 | Сцепление, gearbox, differential и drivetrain | Работает частично; `drivetrain` | PartiallyImplemented | Partial | 11A |
| P1.CAR.008 | Подвеска, steering, wheels and tires | Работает частично; `drivetrain` | PartiallyImplemented | Partial | 11A |
| P1.CAR.009 | Тормоза и handbrake | Работает частично; `drivetrain` | PartiallyImplemented | Partial | 11A |
| P1.CAR.010 | Температура, охлаждение и перегрев | Работает частично; `thermal` | EvidenceCaptured | Planned | 11A |
| P1.CAR.011 | Wear, breakage и failure modes | Работает частично; `wear` | EvidenceCaptured | Planned | 11A |
| P1.CAR.012 | Body damage, detachable parts и windshield | Начато; есть основа или предмет; `body-damage` | EvidenceCaptured | Planned | 11A |
| P1.CAR.013 | Tuning: carburetor, distributor, valves и gearing | Работает частично; `tuning` | EvidenceCaptured | Planned | 11A |
| P1.CAR.014 | Aftermarket/performance parts и configuration | Работает частично; `tuning` | EvidenceCaptured | Planned | 11A |
| P1.CAR.015 | Interior controls, gauges, lights и electrical consumers | Работает частично; `cockpit` | ImplementedUnverified | Planned | 11A |
| P1.CAR.016 | Inspection legality и roadworthiness state | Начато; есть основа или предмет; `car-legality` | EvidenceCaptured | Planned | 13B |
| P1.CAR.017 | Fleetari service integration и repair outcomes | Работает частично; `workshop` | EvidenceCaptured | Planned | 12A |
| P1.CAR.018 | Complete vehicle save aggregate and world pose | Работает частично; `car-save` | PartiallyImplemented | Partial | 11A |
| P1.CAR.019 | Temporary full Legacy vehicle presentation | Работает частично; `car-view` | PartiallyImplemented | Partial | 11A |
| P1.CAR.020 | Donor-calibrated driving behavior and comparison | Начато; есть основа или предмет; `car-calibration` | EvidenceCaptured | Planned | 11A |

## Vehicles — 40 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.VEHICLE.001 | Satsuma project vehicle — complete role and state | Работает частично; `satsuma` | PartiallyImplemented | Partial | 11A |
| P1.VEHICLE.002 | Hayosiko van — complete role and state | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.003 | Gifu sewage truck — complete role and state | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.004 | Kekmet tractor — complete role and state | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.005 | Ruscko car — complete role and state | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.006 | Ferndale muscle car — complete role and state | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.007 | Jonnez ES moped — complete role and state | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.008 | Motor boat — complete role and state | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.009 | Combine harvester — complete role and state | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.010 | Flatbed trailer — complete role and state | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.011 | Public bus — complete role and state | Работает частично; `bus` | EvidenceCaptured | Planned | 11B |
| P1.VEHICLE.012 | Train — complete role and state | Работает частично; `train` | EvidenceCaptured | Planned | 11B |
| P1.VEHICLE.013 | Police patrol car — complete role and state | Работает частично; `police-cars` | EvidenceCaptured | Planned | 13B |
| P1.VEHICLE.014 | Victro traffic car — complete role and state | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |
| P1.VEHICLE.015 | Lamore traffic car — complete role and state | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |
| P1.VEHICLE.016 | Traffic truck — complete role and state | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |
| P1.VEHICLE.017 | Polsa traffic car — complete role and state | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |
| P1.VEHICLE.018 | Fittan traffic car — complete role and state | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |
| P1.VEHICLE.019 | Svoboda traffic car — complete role and state | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |
| P1.VEHICLE.020 | Menace traffic car — complete role and state | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |
| P1.VEHICLE.021 | KUSKI scripted car — complete role and state | Работает частично; `pena` | EvidenceCaptured | Planned | 11B |
| P1.VEHICLE.022 | Amis/Jani car — complete role and state | Работает частично; `jani-petteri` | PartiallyImplemented | Partial | 11B |
| P1.VEHICLE.023 | Amis/Petteri car — complete role and state | Работает частично; `jani-petteri` | PartiallyImplemented | Partial | 11B |
| P1.VEHICLE.024 | Rally competitor cars — complete role and state | Работает частично; `rally-cars` | EvidenceCaptured | Planned | 13B |
| P1.VEHICLE.025 | Drag-race cars — complete role and state | Работает частично; `drag-cars` | EvidenceCaptured | Planned | 13C |
| P1.VEHICLE.026 | AI boats — complete role and state | Работает частично; `boats` | EvidenceCaptured | Planned | 11B |
| P1.VEHICLE.027 | Teimo bicycle — complete role and state | Работает частично; `bicycle` | PartiallyImplemented | Partial | 10B |
| P1.VEHICLE.028 | Police/checkpoint vehicle variants — complete role and state | Работает частично; `police-cars` | EvidenceCaptured | Planned | 13B |
| P1.VEHICLE.101 | Wreck 01 — persistent identity and role | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.102 | Wreck 02 — persistent identity and role | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.103 | Wreck 03 — persistent identity and role | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.104 | Wreck 04 — persistent identity and role | Игровая реализация не найдена; `other-vehicles` | EvidenceCaptured | Planned | 11A |
| P1.VEHICLE.105 | AI boat 01 — persistent identity and role | Работает частично; `boats` | EvidenceCaptured | Planned | 11B |
| P1.VEHICLE.106 | AI boat 02 — persistent identity and role | Работает частично; `boats` | EvidenceCaptured | Planned | 11B |
| P1.VEHICLE.107 | Rally car 01 — persistent identity and role | Работает частично; `rally-cars` | EvidenceCaptured | Planned | 13B |
| P1.VEHICLE.108 | Rally car 02 — persistent identity and role | Работает частично; `rally-cars` | EvidenceCaptured | Planned | 13B |
| P1.VEHICLE.109 | Rally car 03 — persistent identity and role | Работает частично; `rally-cars` | EvidenceCaptured | Planned | 13B |
| P1.VEHICLE.110 | Drag car 01 — persistent identity and role | Работает частично; `drag-cars` | EvidenceCaptured | Planned | 13C |
| P1.VEHICLE.111 | Drag car 02 — persistent identity and role | Работает частично; `drag-cars` | EvidenceCaptured | Planned | 13C |
| P1.VEHICLE.112 | Dirt-rally Fittan — persistent identity and role | Начато; есть основа или предмет; `rally-fittan` | EvidenceCaptured | Planned | 13B |

## NPC — 72 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.NPC.001 | Teimo shopkeeper/pub owner/bicyclist — presence, schedule, dialogue and state | Работает частично; `teimo` | PartiallyImplemented | Partial | 12A |
| P1.NPC.002 | Fleetari mechanic — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 12A |
| P1.NPC.003 | Uncle Kesseli — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.004 | Grandmother — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.005 | Jokke / Kilju buyer and hiker — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.006 | Suski — presence, schedule, dialogue and state | Работает частично; `suski` | PartiallyImplemented | Partial | 10B |
| P1.NPC.007 | Farmer — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.008 | Strawberry-field owner/berryman — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.009 | Sewage customer 1 — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.010 | Sewage customer 2 — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.011 | Sewage customer 3 — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.012 | Sewage customer 4 — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.013 | Sewage customer 5 — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.014 | Firewood customer — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.015 | Inspection officer — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.016 | Wastewater-facility attendant — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.017 | Ventti opponent/Pigman — presence, schedule, dialogue and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.018 | Bus driver Latanen — presence, route, schedule and state | Работает частично; `bus` | PartiallyImplemented | Partial | 10B |
| P1.NPC.019 | Bus passenger Markku — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.020 | Bus passenger Signe — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.022 | Ambient walker Alpo — presence, schedule, dialogue and state | Начато; есть основа или предмет; `alpo` | EvidenceCaptured | Planned | 10B |
| P1.NPC.023 | Ambient walker Julli — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.024 | Ambient walker Kale — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.025 | Ambient walker Kristian — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.026 | Ambient walker Rauno — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.027 | Ambient walker Unto — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.028 | Pub fighter role — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.029 | Dance-hall fighter role — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.030 | Dance-hall guard — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.031 | Band singer — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.032 | Band synthist — presence, schedule and performance state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.033 | Band bassist — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.034 | Band drummer — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.035 | Theatre actors — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.036 | Theatre patrons — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.037 | Janitor Reijo — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.038 | Football children — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.039 | Rally parts salesman — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.040 | Rally officials — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.041 | Rally drivers — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.042 | Rally spectators/helpers — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.043 | Police/checkpoint officers — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.044 | Traffic drivers — presence, schedule, dialogue and state | Работает частично; `traffic` | EvidenceCaptured | Planned | 10B |
| P1.NPC.045 | Traffic passengers — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.047 | Murderer/hitchhiker role — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.048 | Crazy old-man watcher — presence, schedule, dialogue and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.049 | Jani / KYLAJANI driver — identity, schedule and state | Работает частично; `jani-petteri` | PartiallyImplemented | Partial | 10B |
| P1.NPC.050 | Petteri / AMIS2 driver — identity, schedule and state | Работает частично; `jani-petteri` | PartiallyImplemented | Partial | 10B |
| P1.NPC.101 | Jokke wife — identity, schedule and state | Начато; есть основа или предмет; `npc-foundation` | PartiallyImplemented | Partial | 10B |
| P1.NPC.102 | Cousin/KUSKI driver — identity, schedule and state | Работает частично; `pena` | EvidenceCaptured | Planned | 10B |
| P1.NPC.104 | Store drunk 01 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.105 | Store drunk 02 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.106 | House-party drunk 01 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.107 | House-party drunk 02 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.108 | Dancehall dancer couple 01 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.109 | Dancehall dancer couple 02 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.110 | Dancehall solo dancer — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.111 | Dancehall additional fighter — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.112 | Police CopHome2 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.113 | Police CopHome3 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.114 | Police CopHome4 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.115 | Police CopTeimo1 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.116 | Police CopTeimo2 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.117 | Police CopTeimo3 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.118 | Police CopRadar1 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.119 | Police CopRadar2 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.120 | Police CopAlc1 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.121 | Police CopAlc2 — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.122 | Rally Saturday official — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.123 | Rally Sunday official — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.124 | Rally finish official — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |
| P1.NPC.125 | Drag starter — identity, schedule and state | Игровая реализация не найдена; `npc-missing` | EvidenceCaptured | Planned | 10B |

## Traffic — 10 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.TRAFFIC.001 | Road graph, lanes, spawn/despawn and density | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |
| P1.TRAFFIC.002 | Ambient vehicle route AI and collision response | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |
| P1.TRAFFIC.003 | Traffic drivers/passengers binding and schedules | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |
| P1.TRAFFIC.004 | Public bus route, stops, timetable and riding | Работает частично; `bus` | PartiallyImplemented | Partial | 11B |
| P1.TRAFFIC.005 | Train route, crossings and collision safety | Работает частично; `train` | PartiallyImplemented | Partial | 11B |
| P1.TRAFFIC.006 | AI boat routes and water traversal | Работает частично; `boats` | PartiallyImplemented | Partial | 11B |
| P1.TRAFFIC.007 | Scripted Amis/KUSKI encounters and races | Работает частично; `jani-petteri` | PartiallyImplemented | Partial | 11B |
| P1.TRAFFIC.008 | Streaming handoff, persistence and recovery for moving actors | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |
| P1.TRAFFIC.009 | Surface/road rules and traffic audio | Работает частично; `audio` | PartiallyImplemented | Partial | 11B |
| P1.TRAFFIC.010 | Long-route performance and duplicate prevention | Работает частично; `traffic` | PartiallyImplemented | Partial | 11B |

## Economy — 5 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.ECONOMY.001 | Money balance and transaction ledger | Работает частично; `economy` | PartiallyImplemented | Partial | 12A |
| P1.ECONOMY.002 | Donor prices and price-change rules | Работает частично; `economy` | PartiallyImplemented | Partial | 12A |
| P1.ECONOMY.003 | Purchases, refunds and insufficient-funds behavior | Работает частично; `economy` | PartiallyImplemented | Partial | 12A |
| P1.ECONOMY.004 | Payments/rewards/fines across services and jobs | Работает частично; `economy` | PartiallyImplemented | Partial | 12A |
| P1.ECONOMY.005 | Economy persistence and UI binding | Работает частично; `economy` | PartiallyImplemented | Partial | 12A |

## Communications — 24 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.COMMS.001 | Phone incoming calls, answer/hang-up and conditional scheduling | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.002 | Phone service calls and job/story offers | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.003 | Mail delivery and readable letters | Работает частично; `mail` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.004 | Bills, due dates, payment and consequences | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.005 | Catalog orders and forms | Работает частично; `mail` | PartiallyImplemented | Partial | 12A |
| P1.COMMS.006 | Package/order delivery and pickup | Работает частично; `mail` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.007 | Communication state persistence and missed-call handling | Работает частично; `mail` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.101 | Phone caller/event: Fleetari | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.102 | Phone caller/event: Drunk lift | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.103 | Phone caller/event: Moving job | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.104 | Phone caller/event: Grandmother | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.105 | Phone caller/event: Suski | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.106 | Phone caller/event: Uncle | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.107 | Phone caller/event: Firewood customer | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.108 | Phone caller/event: Salesman | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.109 | Phone caller/event: Septic caller 01 | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.110 | Phone caller/event: Septic caller 02 | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.111 | Phone caller/event: Septic caller 03 | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.112 | Phone caller/event: Septic caller 04 | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.113 | Phone caller/event: Septic caller 05 | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.114 | Phone caller/event: Septic caller 06 | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.115 | Phone caller/event: Tohvakka/farmer | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.116 | Phone caller/event: Wrong number | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |
| P1.COMMS.117 | Phone caller/event: No connection | Игровая реализация не найдена; `comms` | EvidenceCaptured | Planned | 12A |

## Services — 43 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.SERVICE.001 | Home and yard — gameplay/service flow | Работает частично; `home` | PartiallyImplemented | Partial | 12A |
| P1.SERVICE.002 | Teimo shop — gameplay/service flow | Работает частично; `shop` | PartiallyImplemented | Partial | 12A |
| P1.SERVICE.003 | Pub — gameplay/service flow | Работает частично; `pub` | PartiallyImplemented | Partial | 12A |
| P1.SERVICE.004 | Fuel pumps — gameplay/service flow | Работает частично; `fuel` | PartiallyImplemented | Partial | 12A |
| P1.SERVICE.005 | Fleetari repair shop — gameplay/service flow | Работает частично; `workshop` | PartiallyImplemented | Partial | 12A |
| P1.SERVICE.006 | Vehicle inspection station — gameplay/service flow | Начато; есть основа или предмет; `inspection` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.007 | Wastewater treatment facility — gameplay/service flow | Игровая реализация не найдена; `services-missing` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.008 | Post/mail and catalog delivery point — gameplay/service flow | Работает частично; `mail` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.009 | Church/cemetery — gameplay/service flow | Игровая реализация не найдена; `services-missing` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.010 | Dance pavilion — gameplay/service flow | Начато; есть основа или предмет; `dancehall` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.011 | Theatre — gameplay/service flow | Игровая реализация не найдена; `services-missing` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.012 | Strawberry field — gameplay/service flow | Игровая реализация не найдена; `services-missing` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.013 | Farm and hay fields — gameplay/service flow | Игровая реализация не найдена; `services-missing` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.014 | Firewood delivery house — gameplay/service flow | Игровая реализация не найдена; `services-missing` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.015 | Five sewage customer houses — gameplay/service flow | Игровая реализация не найдена; `services-missing` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.016 | Jokke house and moving destination — gameplay/service flow | Игровая реализация не найдена; `services-missing` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.017 | Cottage/island — gameplay/service flow | Начато; есть основа или предмет; `cottage` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.018 | Landfill/dump — gameplay/service flow | Игровая реализация не найдена; `services-missing` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.019 | Rally registration/service points — gameplay/service flow | Начато; есть основа или предмет; `rally-foundation` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.020 | Drag strip — gameplay/service flow | Работает частично; `drag-cars` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.021 | Police checkpoints — gameplay/service flow | Работает частично; `police-cars` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.023 | Jail — gameplay/service flow | Игровая реализация не найдена; `services-missing` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.024 | Bus stops — gameplay/service flow | Работает частично; `bus` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.025 | Repair-shop dyno/lifter and service interactions — gameplay/service flow | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.101 | Fleetari SKU: Body repair | Работает частично; `workshop` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.102 | Fleetari SKU: Brake service | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.103 | Fleetari SKU: Engine adjustment | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.104 | Fleetari SKU: Engine repair | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.105 | Fleetari SKU: Engine tune | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.106 | Fleetari SKU: Gear linkage service | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.107 | Fleetari SKU: Gear ratio change | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.108 | Fleetari SKU: N2O fill | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.109 | Fleetari SKU: Paint job | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.110 | Fleetari SKU: Rim polish | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.111 | Fleetari SKU: Rollcage install | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.112 | Fleetari SKU: Rollcage removal | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.113 | Fleetari SKU: Suspension repair | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.114 | Fleetari SKU: Tires Europeiska | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.115 | Fleetari SKU: Tires Gommer | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.116 | Fleetari SKU: Tires Standard | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.117 | Fleetari SKU: Tires Sutasiko | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.118 | Fleetari SKU: Toe adjustment | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |
| P1.SERVICE.119 | Fleetari SKU: Windshield replacement | Начато; есть основа или предмет; `workshop-other` | EvidenceCaptured | Planned | 12A |

## Jobs — 22 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.JOB.001 | Sewage pumping and delivery/payment across five clients — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.002 | Firewood chopping, loading and delivery — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.003 | Kilju brewing, bottling and sale — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.004 | Hay-bale collection and delivery — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.005 | Combine delivery/return — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.006 | Strawberry picking and payment — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.007 | Junk-car towing/recovery variants — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.008 | Fleetari advertisement delivery — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.009 | Fleetari vandalism request and outcome — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.010 | Jokke ride home — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.011 | Jokke house moving/furniture transport — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.012 | Grandmother food/fish delivery — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.013 | Grandmother transport/church schedule — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.014 | Teimo advertisement distribution — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.015 | Wastewater disposal interaction — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.016 | Wood cutting and carrier handling — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.017 | Vehicle service drop-off/pickup flow — prerequisites, states and rewards | Начато; есть основа или предмет; `job-fragments` | EvidenceCaptured | Planned | 12B |
| P1.JOB.018 | Drag-race participation — prerequisites, states and rewards | Начато; есть основа или предмет; `job-fragments` | EvidenceCaptured | Planned | 12B |
| P1.JOB.019 | Football/soccer local activity — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.020 | Berry-field repeatable work loop — prerequisites, states and rewards | Игровая реализация не найдена; `jobs` | EvidenceCaptured | Planned | 12B |
| P1.JOB.021 | Rally participation flow — prerequisites, states and rewards | Начато; есть основа или предмет; `job-fragments` | EvidenceCaptured | Planned | 13B |
| P1.JOB.022 | Vehicle inspection attempt/retry — prerequisites, states and rewards | Начато; есть основа или предмет; `job-fragments` | EvidenceCaptured | Planned | 13B |

## Story — 22 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.EVENT.001 | Uncle van access and key progression — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.002 | Uncle/Gifu availability and consequences — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.003 | Jokke phone-call and ride chain — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.004 | Jokke move-house chain — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.005 | Jokke suitcase/money branch — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.006 | Jokke suicide/outcome branch — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.007 | Suski roadside rescue — conditions and outcomes | Работает частично; `suski` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.008 | Suski date/relationship progression — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.009 | Suski late outcome/ending branch — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.010 | Jani/Petteri road encounter and crash consequences — conditions and outcomes | Работает частично; `jani-petteri` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.011 | Fleetari vandalism request and consequence — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.012 | Grandmother visit/food dialogue progression — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.013 | Vehicle readiness and inspection progression — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.014 | Police warrant/arrest escalation — conditions and outcomes | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.015 | Rally eligibility/result progression — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.016 | House/drunk moving event chain — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.017 | Suicidal hiker encounter — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.018 | Murderer/hitchhiker encounter — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.019 | Open-world calendar/weekday conditional events — conditions and outcomes | Работает частично; `calendar-events` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.020 | Late-game continuation after story outcomes — conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.101 | Prologue/intro sequence — reachability, conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |
| P1.EVENT.102 | Suski ending sequence — reachability, conditions and outcomes | Игровая реализация не найдена; `story` | EvidenceCaptured | Planned | 13A |

## Authority — 21 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.AUTHORITY.001 | Inspection test inputs, pass/fail and certificate state | Начато; есть основа или предмет; `inspection` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.002 | Vehicle roadworthiness enforcement | Начато; есть основа или предмет; `inspection` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.003 | Police checkpoints and stop behavior | Работает частично; `police-cars` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.004 | Fines, tickets, payment and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.005 | Arrest, jail duration and release | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.007 | Death, permadeath option and save consequences | Игровая реализация не найдена; `health` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.008 | Illegal actions and authority response | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.101 | Offense: Speeding — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.102 | Offense: Speeding over 80 — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.103 | Offense: DUI — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.104 | Offense: Environmental offense — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.105 | Offense: Wrong fuel offense — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.106 | Offense: Helmet violation — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.107 | Offense: Ignoring stop order — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.108 | Offense: Motorcycle class violation — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.109 | Offense: No inspection — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.110 | Offense: No plates — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.111 | Offense: Officer attack — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.112 | Offense: Radar detector offense — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.113 | Offense: Seatbelt violation — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |
| P1.AUTHORITY.114 | Offense: Taxation offense — detection, fine and escalation | Игровая реализация не найдена; `authority` | EvidenceCaptured | Planned | 13B |

## Rally — 8 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.RALLY.001 | Registration and entry prerequisites | Игровая реализация не найдена; `rally` | EvidenceCaptured | Planned | 13B |
| P1.RALLY.002 | Vehicle eligibility and inspection interaction | Игровая реализация не найдена; `rally` | EvidenceCaptured | Planned | 13B |
| P1.RALLY.003 | Route/stage geometry and checkpoints | Начато; есть основа или предмет; `rally-foundation` | EvidenceCaptured | Planned | 13B |
| P1.RALLY.004 | Timing, penalties and result calculation | Игровая реализация не найдена; `rally` | EvidenceCaptured | Planned | 13B |
| P1.RALLY.005 | Competitor vehicles and officials | Начато; есть основа или предмет; `rally-foundation` | EvidenceCaptured | Planned | 13B |
| P1.RALLY.006 | Spectators/helpers and event schedule | Начато; есть основа или предмет; `rally-foundation` | EvidenceCaptured | Planned | 13B |
| P1.RALLY.007 | Rewards/consequences and persistence | Начато; есть основа или предмет; `rally-foundation` | EvidenceCaptured | Planned | 13B |
| P1.RALLY.008 | Rally audio/UI presentation | Игровая реализация не найдена; `rally` | EvidenceCaptured | Planned | 13B |

## Media — 54 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.MEDIA.001 | Radio stations, tuning and schedule | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.002 | Television channels/program schedule | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.003 | Computer power/use flow | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.004 | Music import playback through project audio boundary | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.005 | Computer game/minigame content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.006 | Ventti card gambling | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.007 | Video Poker machine and gambling loop | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.008 | Dance pavilion music/event | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.009 | Theatre event/program | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.010 | Football/soccer activity | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.011 | Drag-race event | Работает частично; `drag-cars` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.012 | CD/diskette physical media use | Начато; есть основа или предмет; `media-objects` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.013 | Local band performance | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.014 | Arcade/minigame remaining evidence | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.101 | Computer title: Kaappis Fishgame | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.102 | Computer title: Grilli | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.103 | Computer title: Rally95 | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.104 | Computer title: Wildvest | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.105 | Computer title: Procyon Propilkki | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.106 | Computer title: Rami Filosofiapeli | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.107 | Computer title: Rami JFK | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.108 | Computer title: Rami Joulupukki | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.109 | Computer title: Rami Massacre | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.110 | Computer title: Rami Ojasta | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.111 | Computer title: Rami PasiInvaders | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.112 | Computer title: Rami Pieno | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.113 | Computer title: Rami Rapula | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.114 | Computer title: Rami RunTheGauntlet | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.115 | Computer title: Rami SimppaJokke | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.116 | Computer title: Rami Sormileikki | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.117 | Computer title: Rami WorldsMan | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.118 | Lottery system | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.119 | Basketball activity | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.120 | Teletext pages 100–502 | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.121 | Rally TV broadcast | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.122 | Film: Topless Gun | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.123 | Film: Marjatta | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.124 | Radio channel 01 | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.125 | Radio channel 02 | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.126 | Radio channel 03 | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.127 | Radio channel 04 | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.128 | Named radio program 01 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.129 | Named radio program 02 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.130 | Named radio program 03 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.131 | Named radio program 04 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.132 | Named radio program 05 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.133 | Named radio program 06 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.134 | Named radio program 07 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.135 | Named radio program 08 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.136 | Named radio program 09 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.137 | Named radio program 10 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.138 | Named radio program 11 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.139 | Named radio program 12 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |
| P1.MEDIA.140 | Named radio program 13 — schedule/content | Игровая реализация не найдена; `media` | EvidenceCaptured | Planned | 13C |

## World — 12 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.WORLD.001 | Canonical donor map layout and landmarks | Доделана и принята база; `world-map` | Verified | NotRequired | 13C |
| P1.WORLD.002 | 49-cell additive streaming and active profile | Доделана и принята база; `streaming` | Verified | NotRequired | 13C |
| P1.WORLD.003 | Global terrain/roads/water continuity | Работает частично; `world-physical` | PartiallyImplemented | NotRequired | 13C |
| P1.WORLD.004 | Solid collision for buildings, props and trunks | Работает частично; `world-physical` | PartiallyImplemented | Partial | 13C |
| P1.WORLD.005 | Doors/windows/gates and project-owned interaction state | Работает частично; `doors` | PartiallyImplemented | Partial | 13C |
| P1.WORLD.006 | Stair/threshold traversal landmarks | Работает частично; `world-physical` | PartiallyImplemented | Partial | 09C |
| P1.WORLD.007 | World utilities/triggers and scene transitions | Начато; есть основа или предмет; `world-utilities` | EvidenceCaptured | Planned | 13C |
| P1.WORLD.008 | Out-of-bounds detection and safe recovery | Работает частично; `world-physical` | PartiallyImplemented | Partial | 13C |
| P1.WORLD.009 | Game time, calendar and weekday behavior | Работает частично; `weather` | PartiallyImplemented | Covered | 13C |
| P1.WORLD.010 | Weather, wetness and lightning gameplay outputs | Работает частично; `weather` | PartiallyImplemented | Covered | 13C |
| P1.WORLD.011 | World object replacement keys and Legacy/production layers | Работает частично; `replacement` | PartiallyImplemented | Partial | 13C |
| P1.WORLD.012 | Clocks, signs and diegetic world feedback | Начато; есть основа или предмет; `world-utilities` | EvidenceCaptured | Planned | 13C |

## Presentation — 10 обязательных строк

| FeatureId | Функция оригинала | Оценка аудита / группа | CSV Status | CSV save | Milestone |
|---|---|---|---|---|---|
| P1.PRESENTATION.001 | Project-owned 08A main menu/settings/HUD reference lock | Доделана и принята база; `ui` | Verified | NotRequired | 09C |
| P1.PRESENTATION.002 | Wwise-ready IAudioBackend and temporary donor gameplay audio routing | Работает частично; `audio` | PartiallyImplemented | NotRequired | 13C |
| P1.PRESENTATION.003 | Enviro 3 weather/sky presentation adapter | Работает частично; `weather` | PartiallyImplemented | NotRequired | 13C |
| P1.PRESENTATION.004 | Temporary donor world presentation baseline | Доделана и принята база; `world-map` | Verified | NotRequired | 14B |
| P1.PRESENTATION.005 | Temporary donor character presentation wrappers | Реализовано; полный паритет не принят; `characters-view` | ImplementedUnverified | NotRequired | 10B |
| P1.PRESENTATION.006 | Temporary donor vehicle presentation wrappers | Работает частично; `vehicles-view` | PartiallyImplemented | NotRequired | 11A |
| P1.PRESENTATION.007 | Temporary donor item presentation wrappers | Работает частично; `items-view` | PartiallyImplemented | Partial | 09B |
| P1.PRESENTATION.008 | Legacy animation presentation with project-owned state authority | Реализовано; полный паритет не принят; `characters-view` | ImplementedUnverified | NotRequired | 10B |
| P1.PRESENTATION.009 | Legacy gameplay audio inventory and event bindings | Работает частично; `audio` | PartiallyImplemented | NotRequired | 13C |
| P1.PRESENTATION.010 | Accepted temporary donor materials with replacement debt | Временное решение принято с долгом; `temporary-art` | KnownDifferenceApproved | NotRequired | 14B |

## Необязательные, спорные и исключённые — 17 строк

| FeatureId | Название | Оценка аудита | CSV Status | Milestone |
|---|---|---|---|---|
| P1.SAVE.007 | Read-only donor save schema evidence и optional importer decision | Необязательный заблокированный пункт | Blocked | 09A |
| P1.PLAYER.015 | Отдельный боковой наклон пешком | Исключено из выбранной версии | RejectedNotInLockedDonorVersion | 09C |
| P1.NPC.021 | Unresolved third bus passenger candidate | Необязательный спорный кандидат | Unknown | 10B |
| P1.NPC.046 | Duplicate suicidal-hiker identity candidate | Исключено из выбранной версии | RejectedNotInLockedDonorVersion | 10B |
| P1.SERVICE.022 | Hospital/recovery endpoint candidate | Необязательный спорный кандидат | Unknown | 13B |
| P1.AUTHORITY.006 | Hospital/recovery state and costs candidate | Необязательный спорный кандидат | Unknown | 13B |
| P1.NEEDS.016 | Mosquito allergy state | Необязательный спорный кандидат | Unknown | 09C |
| P1.NEEDS.017 | Burns state and consequences | Необязательный спорный кандидат | Unknown | 09C |
| P1.NEEDS.018 | Berry-picking skill progression | Необязательный спорный кандидат | Unknown | 09C |
| P1.ITEM.180 | Trophy candidates — concrete definition/state | Необязательный заблокированный пункт | Blocked | 09B |
| P1.ITEM.181 | Eyewear candidates — concrete definition/state | Необязательный заблокированный пункт | Blocked | 09B |
| P1.ITEM.182 | Hat candidates — concrete definition/state | Необязательный заблокированный пункт | Blocked | 09B |
| P1.NPC.103 | KYLAJANI passenger role — merged identity | Исключено из выбранной версии | Rejected | 10B |
| P1.COMMS.118 | Phone caller/event: Cheat/debug candidate | Необязательный спорный кандидат | Unknown | 12A |
| P1.EVENT.103 | Jokke memorial grave state — reachability, conditions and outcomes | Необязательный спорный кандидат | Unknown | 13A |
| P1.EVENT.104 | Sirkka memorial grave state — reachability, conditions and outcomes | Необязательный спорный кандидат | Unknown | 13A |
| P1.EVENT.105 | Suski memorial grave state — reachability, conditions and outcomes | Необязательный спорный кандидат | Unknown | 13A |

## Справочник групп: выполненное, остаток и источники

### Слоты, запись, резервные копии и миграции — `save-core`

**Реализовано; полный паритет не принят.**

Есть: Native save v18, реальные New/Continue/Load, атомарная запись, backup/quarantine/recovery; 15 production-участников сохранения.

Осталось: Приёмка всей игры, реальные поздние сохранения и покрытие ещё не реализованных доменов не закрыты. CSV отстаёт: там v17.

Источники: [save_code](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Save/Runtime/SaveDocument.cs>); [save_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Save/Integration/NativeSaveSessionController.cs>); [09a](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09A_REPORT.md>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.SAVE.001, P1.SAVE.002, P1.SAVE.003, P1.SAVE.004.

### Сохранение предметов, машин и выгруженных ячеек — `save-world`

**Работает частично.**

Есть: Есть стабильные ID, снимки реализованных доменов, интеграция со streaming и восстановление состояния.

Осталось: Нет доказанного полного fresh/mid/late-game round trip всех обязательных систем и замен временной презентации.

Источники: [save_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Save/Integration/NativeSaveSessionController.cs>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>); [dod](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/PHASE1_DEFINITION_OF_DONE.md>).

FeatureId: P1.SAVE.005, P1.SAVE.006.

### Ходьба, бег, два приседа, прыжок, взгляд и наклон — `player`

**Работает частично.**

Есть: Реальное движение, бег, два уровня приседа, наклон вперёд, grounding/ступени, прыжок и coupling массы. Основа 09C отдельно принята пользователем.

Осталось: Это принятие ограниченной основы, а не полного donor-паритета движения; калибровка и все граничные маршруты остаются открыты.

Источники: [09c](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09C_REPORT.md>); [player](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Player/PLAYER_TRAVERSAL_AND_LEAN_AUDIT_2026-08-10.md>); [mass](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Player/PLAYER_MASS_JUMP_AND_POSTURE_TUNING_2026-09-03.md>).

FeatureId: P1.PLAYER.001, P1.PLAYER.002, P1.PLAYER.003, P1.PLAYER.004, P1.PLAYER.005, P1.PLAYER.006, P1.PLAYER.007, P1.PLAYER.009.

### Прыжок и влияние массы игрока — `player-mass`

**Реализовано; полный паритет не принят.**

Есть: Реализованы модель массы, влияние на движение/прыжок, настройки позы и профильные проверки.

Осталось: Формальный статус ImplementedUnverified; полное сравнение всей физики игрока с донором не принято.

Источники: [mass](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Player/PLAYER_MASS_JUMP_AND_POSTURE_TUNING_2026-09-03.md>); [09c](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09C_REPORT.md>).

FeatureId: P1.PLAYER.008, P1.NEEDS.015.

### Взаимодействие, перенос и инструменты — `interaction`

**Работает частично.**

Есть: Луч, capability targets, подсказки, поднять/нести/повернуть/поставить/бросить; подключены реальные инструменты сборки.

Осталось: Нужна проверка всех предметных сценариев, узких мест мира и поведения при загрузке/выгрузке.

Источники: [09b](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09B_REPORT.md>); [09c](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09C_REPORT.md>); [mounts](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_STOCK_MOUNT_FASTENERS_AND_HOSE_CLAMPS_2026-09-05.md>).

FeatureId: P1.PLAYER.010, P1.PLAYER.011, P1.PLAYER.012.

### Сесть за руль и выйти из транспорта — `seating`

**Начато; есть основа или предмет.**

Есть: Есть маршрутизация vehicle input и отдельная рабочая посадка пассажиром в автобус.

Осталось: Полный единый водительский цикл для обязательного автопарка не подтверждён; автобус не закрывает все сиденья.

Источники: [bus](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Traffic/Runtime/TrafficBusPassengerInteractionTarget.cs>); [cockpit](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_COCKPIT_CONTROLS_AUDIT_2026-09-05.md>); [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>).

FeatureId: P1.PLAYER.013.

### Боковой наклон из автомобиля — `car-lean`

**Только спецификация.**

Есть: Есть спецификация donor-совместимого поведения через окно/дверь.

Осталось: Реализация этой функции не установлена.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [player](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Player/PLAYER_TRAVERSAL_AND_LEAN_AUDIT_2026-08-10.md>).

FeatureId: P1.PLAYER.014.

### Мат, фак и голосовые реакции — `swearing`

**Работает частично.**

Есть: Ручная ругань, жест, голосовые события и реакции подключены через проектное аудио.

Осталось: Все donor-условия, частоты и реакции по будущим игровым системам ещё не сверены.

Источники: [voice](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Player/PLAYER_VOICE_REACTIONS_2026-08-13.md>); [09c](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09C_REPORT.md>).

FeatureId: P1.PLAYER.016.

### Голод, жажда, стресс, усталость, грязь, мочевой пузырь — `needs`

**Работает частично.**

Есть: Состояния потребностей, HUD/save, еда/питьё и отложенное насыщение/гидратация реально работают.

Осталось: Часть коэффициентов предварительная; здоровье, смерть и весь набор последствий ещё не образуют полный donor-цикл.

Источники: [09c](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09C_REPORT.md>); [food](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Items/FOOD_FRIDGE_INITIAL_PLACEMENT_FIX_2026-08-14.md>); [save_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Save/Integration/NativeSaveSessionController.cs>).

FeatureId: P1.NEEDS.001, P1.NEEDS.002, P1.NEEDS.003, P1.NEEDS.004, P1.NEEDS.005, P1.NEEDS.006, P1.NEEDS.008, P1.NEEDS.009.

### Алкоголь, похмелье и курение — `alcohol`

**Работает частично.**

Есть: Потребление, состояние опьянения/похмелья, метаболизм и влияние курения на стресс реализованы.

Осталось: Нужна полная калибровка скоростей, порогов и последствий по донору.

Источники: [09c](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09C_REPORT.md>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>).

FeatureId: P1.NEEDS.007, P1.NEEDS.010.

### Мочеиспускание и сон — `urination-sleep`

**Работает частично.**

Есть: Мочеиспускание в мире и сон с продвижением игрового времени связаны с потребностями и сохранением.

Осталось: Не приняты все места сна, крайние состояния и взаимодействия с будущими событиями.

Источники: [09c](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09C_REPORT.md>); [home](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Home/Runtime/HomeSystemRuntime.cs>); [home_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionHomeInstaller.cs>).

FeatureId: P1.NEEDS.012, P1.NEEDS.013.

### Здоровье, болезни, травмы, смерть и permadeath — `health`

**Игровая реализация не найдена.**

Есть: Есть donor evidence и требования; локальные crash/ragdoll реакции не являются глобальной системой здоровья.

Осталось: Полный набор повреждений/смертей, последствия для прохождения и сохранения не найден.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>); [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>).

FeatureId: P1.NEEDS.014, P1.AUTHORITY.007.

### Дом: электричество, краны, душ и гигиена — `home`

**Работает частично.**

Есть: Проектные выключатели, электропитание, вода/душ и очистка грязи подключены к бытовым точкам.

Осталось: Счета, отключения по задолженности, все аварии и все бытовые точки донорской игры не закрыты.

Источники: [home](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Home/Runtime/HomeSystemRuntime.cs>); [home_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionHomeInstaller.cs>); [09c](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09C_REPORT.md>).

FeatureId: P1.HOME.001, P1.HOME.002, P1.HOME.007, P1.SERVICE.001.

### Электрическая сауна — `sauna`

**Работает частично.**

Есть: Нагрев, таймер, температура, камни/пар, ведро и ковш связаны с project-owned состоянием.

Осталось: Полный donor-баланс и все последствия перегрева/здоровья ещё не приняты.

Источники: [home](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Home/Runtime/HomeSystemRuntime.cs>); [home_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionHomeInstaller.cs>); [09c](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09C_REPORT.md>).

FeatureId: P1.HOME.003, P1.HOME.104.

### Камин, огонь и дрова — `fire`

**Работает частично.**

Есть: Есть домашние fire/heat состояния и базовые взаимодействия; переносимые дрова представлены как предметы.

Осталось: Полная рубка/загрузка/горение и пожарные последствия не доказаны; дровяная сауна не закрыта.

Источники: [home](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Home/Runtime/HomeSystemRuntime.cs>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [grill](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Items/PORTABLE_GRILL_AND_CONTAINER_LIDS_2026-08-14.md>).

FeatureId: P1.HOME.004.

### Кровати, диван и места сна — `beds`

**Работает частично.**

Есть: Есть sleep targets и связанный с needs/time/save цикл, предметная основа дивана.

Осталось: Полный список доступных мест и donor-условий сна не принят.

Источники: [home](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Home/Runtime/HomeSystemRuntime.cs>); [home_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionHomeInstaller.cs>); [09c](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09C_REPORT.md>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>).

FeatureId: P1.HOME.005, P1.ITEM.170.

### Еда, порча, холодильник и приготовление на плите — `food`

**Работает частично.**

Есть: Свежесть, приготовление/подгорание, per-item save; холодильник охлаждает при закрытой двери и питании; еда реально потребляется.

Осталось: Нужна полная сверка ассортимента, скоростей, температур и влияния испорченной еды. Рыбный предмет не означает готовую рыбалку.

Источники: [food](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Items/FOOD_FRIDGE_INITIAL_PLACEMENT_FIX_2026-08-14.md>); [home](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Home/Runtime/HomeSystemRuntime.cs>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.HOME.006, P1.ITEM.008, P1.ITEM.009, P1.HOME.101, P1.HOME.102, P1.ITEM.101, P1.ITEM.102, P1.ITEM.103, P1.ITEM.104, P1.ITEM.105, P1.ITEM.116, P1.ITEM.117, P1.ITEM.118, P1.ITEM.119.

### Переносной гриль и уголь — `grill`

**Работает частично.**

Есть: Топливо, пламя/угли, крышка, мокрое топливо, наклон, готовка и сохранение реализованы.

Осталось: Полная donor-калибровка всех продуктов, условий и крайних состояний ещё не принята.

Источники: [grill](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Items/PORTABLE_GRILL_AND_CONTAINER_LIDS_2026-08-14.md>); [food](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Items/FOOD_FRIDGE_INITIAL_PLACEMENT_FIX_2026-08-14.md>).

FeatureId: P1.HOME.103, P1.ITEM.110, P1.ITEM.149.

### Домашний телевизор и радио как приборы — `home-media`

**Начато; есть основа или предмет.**

Есть: Есть бытовые переключения питания и представление устройств.

Осталось: Программы, каналы, эфир, настройка и весь медиаконтент не реализованы как законченная система.

Источники: [home](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Home/Runtime/HomeSystemRuntime.cs>); [home_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionHomeInstaller.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.HOME.008.

### Коттедж, остров и бытовые точки — `cottage`

**Начато; есть основа или предмет.**

Есть: Локация присутствует на карте; есть отдельные общие бытовые точки/механизмы.

Осталось: Полный набор островных взаимодействий, дровяная сауна и связанные занятия не закрыты.

Источники: [home_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionHomeInstaller.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.HOME.009, P1.SERVICE.017.

### Двери, окна, ворота и доступ — `doors`

**Работает частично.**

Есть: Проектные интерактивные объекты и сохраняемое состояние доступа существуют отдельно от donor hierarchy.

Осталось: Не подтверждена полнота всех объектов, доступов, столкновений и переходов.

Источники: [home_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionHomeInstaller.cs>); [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.HOME.010, P1.WORLD.005.

### Дровяная сауна — `wood-sauna`

**Игровая реализация не найдена.**

Есть: Есть donor evidence и общая система тепла/огня.

Осталось: Отдельный полный цикл загрузки дров, растопки, нагрева и пара не установлен.

Источники: [home](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Home/Runtime/HomeSystemRuntime.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.HOME.105.

### Варка и продажа килю — `kilju`

**Начато; есть основа или предмет.**

Есть: Существуют сахар/дрожжи/сок, ведро, крышка, жидкости и состояние предметов.

Осталось: Брожение, рецепт, качество, розлив и продажа с последствиями не найдены как законченный игровой цикл.

Источники: [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [grill](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Items/PORTABLE_GRILL_AND_CONTAINER_LIDS_2026-08-14.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.ITEM.010, P1.HOME.106, P1.ITEM.106, P1.ITEM.107, P1.ITEM.108, P1.ITEM.143, P1.ITEM.144.

### Рыболовная ловушка — `fish-trap`

**Начато; есть основа или предмет.**

Есть: Предмет ловушки существует и использует общую инфраструктуру переносимых объектов.

Осталось: Размещение с расчётом улова, ожидание, добыча и сохранение улова не подтверждены.

Источники: [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.HOME.107, P1.ITEM.152.

### Кофе: заваривание, подача и эффект — `coffee`

**Начато; есть основа или предмет.**

Есть: Пачка, кофейник и чашка описаны как предметы.

Осталось: Полный цикл заваривания/подачи и специфический donor-эффект на усталость не установлен.

Источники: [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [home](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Home/Runtime/HomeSystemRuntime.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.NEEDS.011, P1.HOME.108, P1.ITEM.109, P1.ITEM.150, P1.ITEM.151.

### Средство от комаров — `repellent`

**Начато; есть основа или предмет.**

Есть: Есть определение предмета.

Осталось: Специфическое действие, длительность и связь с комарами не подтверждены.

Источники: [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.HOME.109, P1.ITEM.112.

### Остальные предметы: определения и общие действия — `item-base`

**Начато; есть основа или предмет.**

Есть: 152 определения в актуальном каталоге: предметы, дочерние объекты, почтовые детали и одобренный ExpandedShop; общие ID/перенос/quantity/save.

Осталось: Наличие определения или модели не закрывает назначение предмета. Для этой группы специфический полный сценарий отдельно не подтверждён.

Источники: [09b](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09B_REPORT.md>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [items_view](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/ITEM_LEGACY_PRESENTATION_BUILD_REPORT.md>).

FeatureId: P1.ITEM.012, P1.ITEM.015, P1.ITEM.016, P1.ITEM.131, P1.ITEM.154, P1.ITEM.155, P1.ITEM.156, P1.ITEM.157, P1.ITEM.158, P1.ITEM.159, P1.ITEM.160, P1.ITEM.161, P1.ITEM.163, P1.ITEM.164, P1.ITEM.165, P1.ITEM.166, P1.ITEM.167, P1.ITEM.168, P1.ITEM.169, P1.ITEM.171, P1.ITEM.172, P1.ITEM.175, P1.ITEM.176, P1.ITEM.177, P1.ITEM.178, P1.ITEM.179.

### Ключи, трещотка, отвёртка и проводка — `tools`

**Работает частично.**

Есть: Инструменты реально связаны с болтами, размерами, настройками двигателя и электрическими соединениями.

Осталось: Нужна полная проверка доступности, всех размеров/мест и донорских ограничений.

Источники: [mounts](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_STOCK_MOUNT_FASTENERS_AND_HOSE_CLAMPS_2026-09-05.md>); [tuning](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_ADJUSTMENTS_2026-09-05.md>); [wiring](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_WIRING_REACH_AND_TERMINAL_POSES_2026-09-05.md>).

FeatureId: P1.ITEM.001, P1.ITEM.138, P1.ITEM.139.

### Штатный и подкатной домкраты — `jacks`

**Работает частично.**

Есть: Есть специальные контроллеры взаимодействия и физического подъёма, а также профильные Edit/Play проверки.

Осталось: Полная donor-калибровка, все позиции и безопасность при крайних нагрузках не приняты.

Источники: [jack](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Vehicle/ItemsIntegration/Runtime/VehicleJackInteractionController.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.ITEM.140, P1.ITEM.141.

### Подъёмник двигателя и комплект подъёмных устройств — `lifting-tools`

**Начато; есть основа или предмет.**

Есть: Домкраты работают; моторный подъёмник есть в предметном каталоге.

Осталось: Отдельное законченное поведение моторного подъёмника не найдено. Два домкрата не закрывают весь комплект.

Источники: [jack](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Vehicle/ItemsIntegration/Runtime/VehicleJackInteractionController.cs>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>).

FeatureId: P1.ITEM.002, P1.ITEM.142.

### Топор, кувалда, лом и переноска дров — `work-tools`

**Начато; есть основа или предмет.**

Есть: Предметы и общая физика/перенос есть.

Осталось: Рубка, специальные разрушения, полная обработка и сдача дров не установлены как законченные механики.

Источники: [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.ITEM.003, P1.ITEM.013, P1.ITEM.135, P1.ITEM.136, P1.ITEM.137, P1.ITEM.147, P1.ITEM.173.

### Канистры, масло, антифриз и тормозная жидкость — `liquids`

**Работает частично.**

Есть: Количество жидкости, крышки, налив/перелив и состояния ёмкостей; колонки наполняют подходящие открытые канистры; у Satsuma есть пять сервисных приёмников.

Осталось: Полный топливный путь до бензобака Satsuma не закрыт; не все жидкости и все машины имеют конечные получатели.

Источники: [grill](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Items/PORTABLE_GRILL_AND_CONTAINER_LIDS_2026-08-14.md>); [fluids](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_SERVICE_FLUID_PASS_2026-09-06.md>); [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>).

FeatureId: P1.ITEM.004, P1.ITEM.005, P1.ITEM.006, P1.ITEM.020, P1.ITEM.120, P1.ITEM.121, P1.ITEM.122, P1.ITEM.123, P1.ITEM.133, P1.ITEM.134, P1.ITEM.145, P1.ITEM.146.

### Пиво, крепкий алкоголь и сигареты — `consumption-items`

**Работает частично.**

Есть: Потребление, упаковки/бутылки и интеграция с needs; кофе из общей категории остаётся отдельным незакрытым сценарием.

Осталось: Калибровка всех эффектов и количеств, специфические долгосрочные последствия не приняты.

Источники: [09c](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09C_REPORT.md>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [food](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Items/FOOD_FRIDGE_INITIAL_PLACEMENT_FIX_2026-08-14.md>).

FeatureId: P1.ITEM.007, P1.ITEM.111, P1.ITEM.113, P1.ITEM.114, P1.ITEM.115.

### Свечи, лампы, ремень и масляный фильтр — `vehicle-consumables`

**Работает частично.**

Есть: Четыре типа купленных расходников связаны со сборкой и постоянным владением/сохранением.

Осталось: Аккумулятор не является пятой готовой привязкой; предохранители и все электрические последствия не закрыты этой четвёркой.

Источники: [consumables](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_PURCHASED_CONSUMABLE_MOUNTS_2026-09-05.md>); [battery](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_PURCHASED_BATTERY_BOUNDARY_2026-09-06.md>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>).

FeatureId: P1.ITEM.011, P1.ITEM.124, P1.ITEM.125, P1.ITEM.126, P1.ITEM.127.

### Аккумулятор, зарядка, батарейки и предохранители — `battery`

**Начато; есть основа или предмет.**

Есть: Есть предметы и электрическое состояние автомобиля.

Осталось: Полный цикл конкретного купленного аккумулятора, зарядного устройства, ресурса и потребителей не закрыт.

Источники: [battery](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_PURCHASED_BATTERY_BOUNDARY_2026-09-06.md>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>).

FeatureId: P1.ITEM.128, P1.ITEM.129, P1.ITEM.130.

### Баллончики краски — `paint`

**Работает частично.**

Есть: Есть 13 вариантов цвета и проектное состояние окраски панелей.

Осталось: Это не полная система геометрических повреждений кузова или всех услуг покраски Fleetari.

Источники: [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [body](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/SatsumaWorkshopOutcomeBackend.cs>).

FeatureId: P1.ITEM.132.

### Фонарик и переносные устройства — `devices`

**Работает частично.**

Есть: У фонарика есть специальная runtime-привязка; другие устройства представлены в каталоге.

Осталось: Каналы радио, полная логика фонаря/батареек и все portable devices не закрыты как общий набор.

Источники: [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [09b](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09B_REPORT.md>).

FeatureId: P1.ITEM.014, P1.ITEM.153.

### Мусор, упаковки и сжигание — `waste`

**Работает частично.**

Есть: Пустая тара/количества, контейнерная логика и топливное состояние бочки существуют.

Осталось: Все правила сжигания/утилизации и экологические последствия не завершены.

Источники: [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [grill](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Items/PORTABLE_GRILL_AND_CONTAINER_LIDS_2026-08-14.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.ITEM.018, P1.ITEM.148.

### Идентичность и сохранение физических предметов — `item-identity`

**Работает частично.**

Есть: Стабильные ID, реестр определений, размещение, перенос, контейнеры и item-domain сохранение реализованы.

Осталось: Полная проверка всех 99 обязательных предметных строк, восстановления и замены презентации ещё не закрыта.

Источники: [09b](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09B_REPORT.md>); [save_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Save/Integration/NativeSaveSessionController.cs>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.ITEM.019.

### Пакет с покупками — `shopping-bag`

**Работает частично.**

Есть: Есть физическая выдача купленных вещей и предметная/контейнерная основа пакета.

Осталось: Все составы, крайние случаи и donor-сравнение поведения пакета не приняты.

Источники: [09b](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09B_REPORT.md>); [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>).

FeatureId: P1.ITEM.174.

### Satsuma целиком — `satsuma`

**Работает частично.**

Есть: Самая развитая машина: сборка, болты, проводка, cockpit, fluids, operating model и native save связаны в проектном runtime.

Осталось: До полной машины остаются штатная заправка бензином, обычный запуск/поездка без debug, donor-калибровка, повреждения, сервисы и часть оборудования.

Источники: [engine](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md>); [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>); [assembly](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_ASSEMBLY_AUDIT_2026-09-05.md>).

FeatureId: P1.VEHICLE.001.

### Satsuma: детали, крепления и болты — `assembly`

**Работает частично.**

Есть: Канонический набор 126 фиксированных деталей, 124 mount points, 294 fasteners; исправлялись доступ, крышки, хомуты, compound assembly и инструменты.

Осталось: Все варианты сборки/разборки и полное сравнение с донором ещё не приняты. Старые 302 болта включали 8 снятых псевдоболтов.

Источники: [assembly](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_ASSEMBLY_AUDIT_2026-09-05.md>); [mounts](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_STOCK_MOUNT_FASTENERS_AND_HOSE_CLAMPS_2026-09-05.md>); [engine](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md>).

FeatureId: P1.CAR.001, P1.CAR.002, P1.CAR.003.

### Satsuma: электропроводка — `wiring`

**Работает частично.**

Есть: 26 пар соединений, физические клеммы/позы, удержание проводки и сохранение connected state.

Осталось: Весь набор потребителей, диагностика и поведение купленных аккумуляторов ещё не закрыты.

Источники: [wiring](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_WIRING_REACH_AND_TERMINAL_POSES_2026-09-05.md>); [battery](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_PURCHASED_BATTERY_BOUNDARY_2026-09-06.md>); [engine](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md>).

FeatureId: P1.CAR.004.

### Satsuma: бензин и сервисные жидкости — `car-fluids`

**Работает частично.**

Есть: Реальные приёмники масла, coolant, двух тормозных контуров и сцепления; крышки, уровни, течи и расход состояния.

Осталось: Заправка бензобака штатным путём пока не закрыта. Нельзя считать заправку канистры доказательством заправки машины.

Источники: [fluids](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_SERVICE_FLUID_PASS_2026-09-06.md>); [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>).

FeatureId: P1.CAR.005.

### Satsuma: запуск и работа двигателя — `engine`

**Работает частично.**

Есть: Зажигание/стартер, gates собранности, choke, RPM, combustion/stall, live operating state и звуковая обратная связь.

Осталось: Текущая проверка тёплого старта использовала явный fuel-only debug bypass; полный обычный start/idle/load cycle, misfire и backfire-калибровка не закрыты.

Источники: [engine](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md>); [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>); [ignition](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_COCKPIT_IGNITION_C2_2026-09-05.md>); [audio](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Audio/SATSUMA_RUNNING_MIX_AND_STARTER_CONTINUITY_2026-09-06.md>).

FeatureId: P1.CAR.006.

### Satsuma: трансмиссия, подвеска и тормоза — `drivetrain`

**Работает частично.**

Есть: Есть тестируемые слои сцепления/КПП/дифференциала, колёс, подвески, руления, тормозов и ручника.

Осталось: Модель ещё использует предварительный solver M06; эталонные разгон, сцепление, тормозной путь и устойчивость полностью не откалиброваны.

Источники: [operating](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_OPERATING_MODEL_PASS_2026-09-06.md>); [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>); [assembly](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_ASSEMBLY_AUDIT_2026-09-05.md>).

FeatureId: P1.CAR.007, P1.CAR.008, P1.CAR.009.

### Satsuma: охлаждение и перегрев — `thermal`

**Работает частично.**

Есть: Температура, вентилятор, давление, ограниченные объёмы жидкостей и часть течей потребляются рабочей моделью.

Осталось: Полные термокривые, все сценарии потери охлаждения, дым/пар и последствия не приняты.

Источники: [operating](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_OPERATING_MODEL_PASS_2026-09-06.md>); [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>); [fluids](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_SERVICE_FLUID_PASS_2026-09-06.md>).

FeatureId: P1.CAR.010.

### Satsuma: износ и поломки — `wear`

**Работает частично.**

Есть: Есть состояние износа девяти деталей и влияние части дефектов на operating model/save.

Осталось: Не все donor-отказы, топливный насос, грязь без фильтра, дым/пар, повреждения и их обратная связь завершены.

Источники: [operating](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_OPERATING_MODEL_PASS_2026-09-06.md>); [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>).

FeatureId: P1.CAR.011.

### Satsuma: деформации кузова и лобовое стекло — `body-damage`

**Начато; есть основа или предмет.**

Есть: Съёмные детали, крепления и окраска уже имеют рабочую основу.

Осталось: Полноценное повреждение/ремонт геометрии и стекла не подтверждены; смена цвета панели не закрывает кузовной ремонт.

Источники: [assembly](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_ASSEMBLY_AUDIT_2026-09-05.md>); [body](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/SatsumaWorkshopOutcomeBackend.cs>); [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>).

FeatureId: P1.CAR.012.

### Satsuma: клапаны, карбюратор, трамблёр и тюнинг — `tuning`

**Работает частично.**

Есть: Реальные настройки клапанов, карбюратора, трамблёра, фаз распредвала и генератора; 46 заказных деталей имеют отображение на проектные определения.

Осталось: Не все aftermarket-варианты и performance-эффекты закрыты. Впуск/выпуск влияют на звук, но полный эффект на мощность не доказан.

Источники: [tuning](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_ADJUSTMENTS_2026-09-05.md>); [operating](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_OPERATING_MODEL_PASS_2026-09-06.md>); [mail](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/12A_C1_HOME_MAIL_ORDER_REPORT.md>); [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>).

FeatureId: P1.CAR.013, P1.CAR.014.

### Satsuma: ключ, подсос, фары, аварийка и дворники — `cockpit`

**Работает частично.**

Есть: Физический ключ/доступ, подсос, фары/аварийка, отдельные дворники и ручник связаны с состоянием и save.

Осталось: Полный набор приборов, сигнал, радио, подрулевые органы и все электрические потребители ещё не закрыты. Formal ImplementedUnverified охватывает ограниченный pass.

Источники: [cockpit](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_COCKPIT_CONTROLS_AUDIT_2026-09-05.md>); [dashboard](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_DASHBOARD_CONTROLS_IMPLEMENTATION_2026-09-05.md>); [ignition](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_COCKPIT_IGNITION_C2_2026-09-05.md>); [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>).

FeatureId: P1.CAR.015.

### Satsuma: техосмотр и легальность — `car-legality`

**Начато; есть основа или предмет.**

Есть: Имеются детали/состояния, которые будущая проверка может оценивать, и определения станции.

Осталось: Production inspection backend не подключён; фактический pass/fail/сертификат и enforcement не готовы.

Источники: [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>); [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.CAR.016.

### Fleetari: заказ и ограниченный кузовной результат — `workshop`

**Работает частично.**

Есть: Есть заказ/оплата/ожидание и подключённый SatsumaWorkshopOutcomeBackend для семи кузовных offer IDs; он применяет сохранённую окраску поверхностей.

Осталось: Не ремонтирует геометрию/двигатель/шины. Остальные outcomes отклоняются; Ferndale loaner и перемещение машины не реализованы этим backend.

Источники: [body](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/SatsumaWorkshopOutcomeBackend.cs>); [workshop](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/WorkshopServiceEngine.cs>); [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>).

FeatureId: P1.CAR.017, P1.SERVICE.005, P1.SERVICE.101.

### Satsuma: агрегат сохранения — `car-save`

**Работает частично.**

Есть: Сохраняются сборка, соединения, fluids/operating state, динамические предметы, cockpit, доступ и world pose.

Осталось: Полный игровой round trip всех будущих вариантов, износа, сервисов и поломок ещё не закрыт.

Источники: [save_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Save/Integration/NativeSaveSessionController.cs>); [engine](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.CAR.018.

### Satsuma: временная визуальная машина — `car-view`

**Работает частично.**

Есть: Sanitized Legacy presentation находится под проектными wrappers и replacement keys.

Осталось: Есть неохваченные варианты/риг/обратная связь; production-модели и материалы отложены до Phase 2.

Источники: [engine](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md>); [scope](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/PHASE1_SCOPE_LOCK.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.CAR.019.

### Satsuma: эталонное поведение в движении — `car-calibration`

**Начато; есть основа или предмет.**

Есть: Собраны измерения, сравнения и проектная симуляция; ведётся подробный список расхождений.

Осталось: Полный исполненный donor-сравнительный маршрут и откалиброванная готовая машина не подтверждены.

Источники: [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>); [operating](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_OPERATING_MODEL_PASS_2026-09-06.md>).

FeatureId: P1.CAR.020.

### Остальной управляемый автопарк и кузова-утиль — `other-vehicles`

**Игровая реализация не найдена.**

Есть: Есть донорский реестр, evidence и временная карта; отдельные имена/модели в исходниках не подтверждают управляемую машину.

Осталось: Полные Hayosiko, Gifu, Kekmet, Ruscko, Ferndale, Jonnez, лодка игрока, комбайн, прицеп и четыре утильных кузова не найдены как готовые игровые роли.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.VEHICLE.002, P1.VEHICLE.003, P1.VEHICLE.004, P1.VEHICLE.005, P1.VEHICLE.006, P1.VEHICLE.007, P1.VEHICLE.008, P1.VEHICLE.009, P1.VEHICLE.010, P1.VEHICLE.101, P1.VEHICLE.102, P1.VEHICLE.103, P1.VEHICLE.104.

### Дорожный трафик и его машины — `traffic`

**Работает частично.**

Есть: 16 маршрутов, 9320 точек, 48 связей; 11 ambient actors, включая Пену. Есть движение, столкновения, drivers, streaming/save и восстановление.

Осталось: Полное donor-сравнение плотности/столкновений, весь состав водителей/пассажиров, звук и длительная проверка производительности не закрыты.

Источники: [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>); [traffic_audio](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/TRAFFIC_AUDIO_RUNTIME_BOUNDARY.md>); [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>).

FeatureId: P1.VEHICLE.014, P1.VEHICLE.015, P1.VEHICLE.016, P1.VEHICLE.017, P1.VEHICLE.018, P1.VEHICLE.019, P1.VEHICLE.020, P1.NPC.044, P1.TRAFFIC.001, P1.TRAFFIC.002, P1.TRAFFIC.003, P1.TRAFFIC.008, P1.TRAFFIC.010.

### Автобус и водитель Latanen — `bus`

**Работает частично.**

Есть: Едет по маршруту из 1084 точек (~13,7 км), обслуживает 3 остановки; можно сесть пассажиром и выйти, состояние сохраняется. Есть отдельная сцена ухода водителя.

Осталось: Билеты/оплата, кнопка запроса остановки, полный обычный пассажирский цикл двери, Markku/Signe и полная сверка рейса не закрыты. Дверь в сцене ухода водителя не закрывает посадочную механику.

Источники: [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>); [bus](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Traffic/Runtime/TrafficBusPassengerInteractionTarget.cs>); [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.VEHICLE.011, P1.NPC.018, P1.TRAFFIC.004, P1.SERVICE.024.

### Поезд и переезды — `train`

**Работает частично.**

Есть: Маршрут в двух направлениях, движение около 30 м/с, ожидание/повтор, физические столкновения и transport state.

Осталось: Полное сравнение расписания, переездов, звуков и смертельных последствий для игрока не принято.

Источники: [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>); [traffic_audio](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/TRAFFIC_AUDIO_RUNTIME_BOUNDARY.md>); [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>).

FeatureId: P1.VEHICLE.012, P1.TRAFFIC.005.

### Две NPC-лодки — `boats`

**Работает частично.**

Есть: Два транспортных актёра с маршрутами, высотой воды и сохраняемым состоянием.

Осталось: Полная звуковая/физическая калибровка не принята. Это не лодка, которой управляет игрок.

Источники: [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>); [traffic_audio](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/TRAFFIC_AUDIO_RUNTIME_BOUNDARY.md>).

FeatureId: P1.VEHICLE.026, P1.TRAFFIC.006, P1.VEHICLE.105, P1.VEHICLE.106.

### Пена / KUSKI — `pena`

**Работает частично.**

Есть: Cousin rules, маршруты, взаимоисключающие weekday/Saturday контексты и traffic.state уже реализованы.

Осталось: Полная роль кузена, пассажирские взаимодействия и все донорские последствия не приняты.

Источники: [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>); [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>).

FeatureId: P1.VEHICLE.021, P1.NPC.102.

### Jani, Petteri и дорожные гонки — `jani-petteri`

**Работает частично.**

Есть: Физические машины, собственный powertrain, длинные маршруты/гонка, поездка к танцплощадке, структурный crash и ragdoll-состояния.

Осталось: Все диалоги, последствия, донорская калибровка и полный набор сценариев встречи не закрыты.

Источники: [story_traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_10B_R4_PHYSICAL_STORY_TRAFFIC_REPORT.md>); [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>); [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>).

FeatureId: P1.VEHICLE.022, P1.VEHICLE.023, P1.NPC.049, P1.NPC.050, P1.TRAFFIC.007, P1.EVENT.010.

### Машины соперников ралли — `rally-cars`

**Работает частично.**

Есть: Три event cars, маршруты и календарный запуск существуют в TrafficWorldRuntime.Events.

Осталось: Наличие соперников не даёт регистрацию игрока, зачёт, штрафы, результаты и награды.

Источники: [traffic_events](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Traffic/Runtime/TrafficWorldRuntime.Events.cs>); [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>).

FeatureId: P1.VEHICLE.024, P1.VEHICLE.107, P1.VEHICLE.108, P1.VEHICLE.109.

### Драг: машины соперников и заезд — `drag-cars`

**Работает частично.**

Есть: Два event cars и последовательность burnout → обратная подача → стартовая позиция → заезд.

Осталось: Полное участие игрока, условия старта, результат/награда и весь персонажный состав не готовы.

Источники: [traffic_events](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Traffic/Runtime/TrafficWorldRuntime.Events.cs>); [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>).

FeatureId: P1.VEHICLE.025, P1.SERVICE.020, P1.MEDIA.011, P1.VEHICLE.110, P1.VEHICLE.111.

### Полицейские машины, пост и погоня — `police-cars`

**Работает частично.**

Есть: Две event cars, выбор места/дня поста, состояние и механизм запроса погони.

Осталось: RequestPoliceChase не имеет найденного игрового вызывающего кода; штрафы, проверки, арест, розыск и тюрьма не подключены в полный цикл.

Источники: [traffic_events](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Traffic/Runtime/TrafficWorldRuntime.Events.cs>); [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.VEHICLE.013, P1.VEHICLE.028, P1.SERVICE.021, P1.AUTHORITY.003.

### Велосипед Теймо — `bicycle`

**Работает частично.**

Есть: Есть привязка и расписание велосипедного контекста.

Осталось: Полный donor-сценарий поездки Теймо и все переключения ролей ещё не приняты.

Источники: [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>); [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>); [10a](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_10A_REPORT.md>).

FeatureId: P1.VEHICLE.027.

### Dirt-rally Fittan: отдельная роль требует уточнения — `rally-fittan`

**Начато; есть основа или предмет.**

Есть: Есть Пена и его Saturday-контекст, а также донорская строка Dirt-rally Fittan.

Осталось: Не доказано, что это отдельная готовая машина, а не контекст/дублирование KUSKI. Нужна сверка идентичности без удаления required строки.

Источники: [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.VEHICLE.112.

### Остальные NPC и их полные роли — `npc-missing`

**Игровая реализация не найдена.**

Есть: Донорский roster фиксирует требуемых жителей, пассажиров, чиновников, артистов и участников событий.

Осталось: Для этой группы полный production-сценарий присутствия, расписания, диалога, последствий и save не установлен.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [characters](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Content/Phase1Foundation/CharacterDefinitionCatalog.asset>); [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.NPC.019, P1.NPC.020, P1.NPC.023, P1.NPC.024, P1.NPC.025, P1.NPC.026, P1.NPC.027, P1.NPC.028, P1.NPC.029, P1.NPC.030, P1.NPC.031, P1.NPC.032, P1.NPC.033, P1.NPC.034, P1.NPC.035, P1.NPC.036, P1.NPC.037, P1.NPC.038, P1.NPC.039, P1.NPC.040, P1.NPC.041, P1.NPC.042, P1.NPC.043, P1.NPC.045, P1.NPC.047, P1.NPC.048, P1.NPC.104, P1.NPC.105, P1.NPC.106, P1.NPC.107, P1.NPC.108, P1.NPC.109, P1.NPC.110, P1.NPC.111, P1.NPC.112, P1.NPC.113, P1.NPC.114, P1.NPC.115, P1.NPC.116, P1.NPC.117, P1.NPC.118, P1.NPC.119, P1.NPC.120, P1.NPC.121, P1.NPC.122, P1.NPC.123, P1.NPC.124, P1.NPC.125.

### Основные жители и клиенты: основа NPC — `npc-foundation`

**Начато; есть основа или предмет.**

Есть: Есть определения, wrappers, контексты/расписания, bounded dialogue и native state. Каталог содержит 22 определения персонажей.

Осталось: Наличие продавца/клиента не означает готовые задания и сюжет. Полные роли родственников, клиентов и сервисных NPC ещё не закрыты.

Источники: [10a](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_10A_REPORT.md>); [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>); [characters](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Content/Phase1Foundation/CharacterDefinitionCatalog.asset>); [npc_manual](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/NPC/M10B_R1_MANUAL_TEST_RU.md>).

FeatureId: P1.NPC.002, P1.NPC.003, P1.NPC.004, P1.NPC.005, P1.NPC.007, P1.NPC.008, P1.NPC.009, P1.NPC.010, P1.NPC.011, P1.NPC.012, P1.NPC.013, P1.NPC.014, P1.NPC.015, P1.NPC.016, P1.NPC.017, P1.NPC.101.

### Теймо: магазин, паб и повседневные роли — `teimo`

**Работает частично.**

Есть: Реальные shop/pub контексты, взаимодействия, обслуживание и отдельный велосипедный контекст.

Осталось: Полная хореография, календарь, все разговоры/последствия и работа с будущими заданиями не приняты.

Источники: [10a](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_10A_REPORT.md>); [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>); [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>).

FeatureId: P1.NPC.001.

### Суски: спасение после аварии — `suski`

**Работает частично.**

Есть: В коде есть Passenger → CrashedInCar → AwaitingPickup → Transporting → RestingAtParentsBed → Rescued, перенос к кровати/сон и сохранение.

Осталось: Нужна полная донорская приёмка спасения. Свидания, отношения и концовка этим не закрыты.

Источники: [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>); [story_traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_10B_R4_PHYSICAL_STORY_TRAFFIC_REPORT.md>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.NPC.006, P1.EVENT.007.

### Alpo: существующий персонажный образец — `alpo`

**Начато; есть основа или предмет.**

Есть: Сохранён ранее сделанный foundation fixture/presentation персонажа.

Осталось: Полная production-роль гуляющего жителя с маршрутом и расписанием не подтверждена.

Источники: [10a](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_10A_REPORT.md>); [characters](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Content/Phase1Foundation/CharacterDefinitionCatalog.asset>); [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>).

FeatureId: P1.NPC.022.

### Деньги, цены и покупки — `economy`

**Работает частично.**

Есть: Баланс, transaction ledger, проверки денег, идемпотентные покупки/выдача, цены и UI/save реализованы.

Осталось: Награды всех заданий, штрафы, счета, возвраты и все donor-правила экономики не закрыты.

Источники: [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [09b](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09B_REPORT.md>); [save_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Save/Integration/NativeSaveSessionController.cs>).

FeatureId: P1.ECONOMY.001, P1.ECONOMY.002, P1.ECONOMY.003, P1.ECONOMY.004, P1.ECONOMY.005.

### Телефон, звонки, счета и общая переписка — `comms`

**Игровая реализация не найдена.**

Есть: Есть donor evidence, адресаты/условия и требования к сохранению.

Осталось: Полный входящий звонок/ответ/отбой/пропуск, календарь звонков, счета/сроки/последствия не найдены как рабочая система.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>); [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>).

FeatureId: P1.COMMS.001, P1.COMMS.002, P1.COMMS.004, P1.COMMS.101, P1.COMMS.102, P1.COMMS.103, P1.COMMS.104, P1.COMMS.105, P1.COMMS.106, P1.COMMS.107, P1.COMMS.108, P1.COMMS.109, P1.COMMS.110, P1.COMMS.111, P1.COMMS.112, P1.COMMS.113, P1.COMMS.114, P1.COMMS.115, P1.COMMS.116, P1.COMMS.117.

### Заказ деталей по каталогу и доставка — `mail`

**Работает частично.**

Есть: Каталог → конверт → отправка → ожидание → оплата → получение; 46 деталей, физические предметы, идемпотентная выдача и save.

Осталось: Это ограниченный почтовый путь: все письма/счета/звонки не реализованы. Интерпретация donor-задержки доставки требует сверки.

Источники: [mail](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/12A_C1_HOME_MAIL_ORDER_REPORT.md>); [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.ITEM.017, P1.COMMS.003, P1.COMMS.005, P1.COMMS.006, P1.COMMS.007, P1.SERVICE.008, P1.ITEM.162.

### Прочие сервисы и занятия в локациях — `services-missing`

**Игровая реализация не найдена.**

Есть: Большинство мест присутствует на карте; есть списки услуг, anchors и донорское evidence.

Осталось: Наличие здания не подтверждает услугу. Полные циклы станции слива, церкви, театра, полей, свалки, домов клиентов и тюрьмы не найдены.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.SERVICE.007, P1.SERVICE.009, P1.SERVICE.011, P1.SERVICE.012, P1.SERVICE.013, P1.SERVICE.014, P1.SERVICE.015, P1.SERVICE.016, P1.SERVICE.018, P1.SERVICE.023.

### Магазин Теймо — `shop`

**Работает частично.**

Есть: Ассортимент/цены, корзина, оплата, остатки, физическая выдача и сохранение; имеется одобренное расширение товаров.

Осталось: Полный донорский календарь, restock, все состояния продавца и последствия не приняты.

Источники: [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [09b](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09B_REPORT.md>); [extras](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Items/EXPANDED_SHOP_EXTENSION_2026-08-14.md>).

FeatureId: P1.SERVICE.002.

### Паб — `pub`

**Работает частично.**

Есть: Заказ/оплата и выдача еды/напитков, базовый контекст обслуживания.

Осталось: Полная жизнь паба, посетители, драки, программа и все сценарии обслуживания не готовы.

Источники: [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>); [food](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Items/FOOD_FRIDGE_INITIAL_PLACEMENT_FIX_2026-08-14.md>).

FeatureId: P1.SERVICE.003.

### Заправочные колонки — `fuel`

**Работает частично.**

Есть: Физический пистолет/шланг, три сорта топлива, подходящая открытая канистра, начисление долга и оплата.

Осталось: Полная заправка всех автомобилей не закрыта, прежде всего бензобак Satsuma.

Источники: [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [engine_compare](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_ENGINE_BEHAVIOR_COMPARISON_2026-09-06.md>); [fluids](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_SERVICE_FLUID_PASS_2026-09-06.md>).

FeatureId: P1.SERVICE.004.

### Техосмотр — `inspection`

**Начато; есть основа или предмет.**

Есть: Есть станция, NPC/контракт и donor evidence критериев.

Осталось: В production configuredInspection: null; реальная оценка машины, pass/fail, документ и повторная попытка не подключены.

Источники: [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>); [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.SERVICE.006, P1.AUTHORITY.001, P1.AUTHORITY.002.

### Танцплощадка — `dancehall`

**Начато; есть основа или предмет.**

Есть: Локация и поездка Jani/Petteri к площадке представлены.

Осталось: Танцы, охрана/драки, группа, посетители, музыка и полный календарный сценарий не готовы.

Источники: [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>); [story_traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_10B_R4_PHYSICAL_STORY_TRAFFIC_REPORT.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.SERVICE.010.

### Остальные услуги Fleetari — `workshop-other`

**Начато; есть основа или предмет.**

Есть: Есть донорские SKU и общая инфраструктура заказов/очереди/оплаты.

Осталось: Конечные результаты ремонта двигателя/тормозов/подвески, шин, развала/передаточных чисел, N2O, стекла, каркаса, окраски как отдельной услуги и dyno/lifter не закрыты.

Источники: [body](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/SatsumaWorkshopOutcomeBackend.cs>); [workshop](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/WorkshopServiceEngine.cs>); [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>).

FeatureId: P1.SERVICE.025, P1.SERVICE.102, P1.SERVICE.103, P1.SERVICE.104, P1.SERVICE.105, P1.SERVICE.106, P1.SERVICE.107, P1.SERVICE.108, P1.SERVICE.109, P1.SERVICE.110, P1.SERVICE.111, P1.SERVICE.112, P1.SERVICE.113, P1.SERVICE.114, P1.SERVICE.115, P1.SERVICE.116, P1.SERVICE.117, P1.SERVICE.118, P1.SERVICE.119.

### Работы и заработок — `jobs`

**Игровая реализация не найдена.**

Есть: Есть клиенты, локации, часть инструментов и общие деньги/предметы; отдельные event cars и сервисные заказы уже работают.

Осталось: Ни один полный required job loop этой группы не установлен: условия → выполнение → оценка → награда → повтор/последствие → save. Это относится к 22 строкам работ, включая ассенизацию, дрова, килю, сено, комбайн, ягоды, буксировку, рекламу и перевозки.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>); [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>).

FeatureId: P1.JOB.001, P1.JOB.002, P1.JOB.003, P1.JOB.004, P1.JOB.005, P1.JOB.006, P1.JOB.007, P1.JOB.008, P1.JOB.009, P1.JOB.010, P1.JOB.011, P1.JOB.012, P1.JOB.013, P1.JOB.014, P1.JOB.015, P1.JOB.016, P1.JOB.019, P1.JOB.020.

### Работы/участие: уже начатые зависимые части — `job-fragments`

**Начато; есть основа или предмет.**

Есть: Есть ограниченные заказы Fleetari, машины драг/ралли и основа станции техосмотра.

Осталось: Полные player drop-off/pickup, участие/зачёт/награда и попытка/повтор техосмотра не готовы. Эта основа не делает четыре работы законченными.

Источники: [service](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Services/Runtime/ServiceRuntime.cs>); [body](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/SatsumaWorkshopOutcomeBackend.cs>); [traffic_events](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Traffic/Runtime/TrafficWorldRuntime.Events.cs>); [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>).

FeatureId: P1.JOB.017, P1.JOB.018, P1.JOB.021, P1.JOB.022.

### Основные сюжетные цепочки и концовки — `story`

**Игровая реализация не найдена.**

Есть: Собран донорский перечень веток и требований к состоянию.

Осталось: Доступ к фургону/Gifu через дядю, ветки Jokke, свидания/концовка Suski, задания Fleetari, прогресс бабушки, розыск и поздние исходы не найдены как полные цепочки.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>); [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>).

FeatureId: P1.EVENT.001, P1.EVENT.002, P1.EVENT.003, P1.EVENT.004, P1.EVENT.005, P1.EVENT.006, P1.EVENT.008, P1.EVENT.009, P1.EVENT.011, P1.EVENT.012, P1.EVENT.013, P1.EVENT.015, P1.EVENT.016, P1.EVENT.017, P1.EVENT.018, P1.EVENT.020, P1.EVENT.101, P1.EVENT.102.

### События по дню недели — `calendar-events`

**Работает частично.**

Есть: Календарь уже управляет частью трафика, Пены и event cars.

Осталось: Общий полный набор недельных событий, телефонных цепочек и сохраняемых исходов ещё не реализован.

Источники: [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>); [traffic_events](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Traffic/Runtime/TrafficWorldRuntime.Events.cs>); [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>).

FeatureId: P1.EVENT.019.

### Штрафы, полиция, розыск и тюрьма — `authority`

**Игровая реализация не найдена.**

Есть: Есть donor evidence правонарушений и отдельная транспортная основа полицейского поста.

Осталось: Нет полного detection → протокол/штраф → оплата/эскалация → арест/тюрьма/освобождение. Все 14 детальных составов правонарушений остаются без закрытого игрового цикла.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [traffic_events](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Traffic/Runtime/TrafficWorldRuntime.Events.cs>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.EVENT.014, P1.AUTHORITY.004, P1.AUTHORITY.005, P1.AUTHORITY.008, P1.AUTHORITY.101, P1.AUTHORITY.102, P1.AUTHORITY.103, P1.AUTHORITY.104, P1.AUTHORITY.105, P1.AUTHORITY.106, P1.AUTHORITY.107, P1.AUTHORITY.108, P1.AUTHORITY.109, P1.AUTHORITY.110, P1.AUTHORITY.111, P1.AUTHORITY.112, P1.AUTHORITY.113, P1.AUTHORITY.114.

### Ралли для игрока — `rally`

**Игровая реализация не найдена.**

Есть: Существуют event cars и маршруты; это отдельные транспортные заготовки.

Осталось: Регистрация, допуск, игровые контрольные точки, тайминг/штрафы, итог, награда и весь состав события не реализованы как полный режим.

Источники: [traffic_events](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Traffic/Runtime/TrafficWorldRuntime.Events.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.RALLY.001, P1.RALLY.002, P1.RALLY.004, P1.RALLY.008.

### Ралли: трасса, соперники и расписание — `rally-foundation`

**Начато; есть основа или предмет.**

Есть: Маршруты/три машины/день события и их transport state реализованы.

Осталось: Игровые checkpoints, официальные лица/зрители, результат игрока, призы и event persistence полного ралли не закрыты.

Источники: [traffic_events](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Traffic/Runtime/TrafficWorldRuntime.Events.cs>); [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.SERVICE.019, P1.RALLY.003, P1.RALLY.005, P1.RALLY.006, P1.RALLY.007.

### Радио, ТВ, компьютер, игры и развлечения — `media`

**Игровая реализация не найдена.**

Есть: Есть донорский контентный реестр, физические устройства и проектная граница аудио.

Осталось: Полные эфиры, телеканалы/телетекст/фильмы, 17 компьютерных игр, Ventti, видеопокер, лотерея, выступления и спортивные мини-игры не найдены в рабочем runtime.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [home](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Home/Runtime/HomeSystemRuntime.cs>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.MEDIA.001, P1.MEDIA.002, P1.MEDIA.003, P1.MEDIA.004, P1.MEDIA.005, P1.MEDIA.006, P1.MEDIA.007, P1.MEDIA.008, P1.MEDIA.009, P1.MEDIA.010, P1.MEDIA.013, P1.MEDIA.014, P1.MEDIA.101, P1.MEDIA.102, P1.MEDIA.103, P1.MEDIA.104, P1.MEDIA.105, P1.MEDIA.106, P1.MEDIA.107, P1.MEDIA.108, P1.MEDIA.109, P1.MEDIA.110, P1.MEDIA.111, P1.MEDIA.112, P1.MEDIA.113, P1.MEDIA.114, P1.MEDIA.115, P1.MEDIA.116, P1.MEDIA.117, P1.MEDIA.118, P1.MEDIA.119, P1.MEDIA.120, P1.MEDIA.121, P1.MEDIA.122, P1.MEDIA.123, P1.MEDIA.124, P1.MEDIA.125, P1.MEDIA.126, P1.MEDIA.127, P1.MEDIA.128, P1.MEDIA.129, P1.MEDIA.130, P1.MEDIA.131, P1.MEDIA.132, P1.MEDIA.133, P1.MEDIA.134, P1.MEDIA.135, P1.MEDIA.136, P1.MEDIA.137, P1.MEDIA.138, P1.MEDIA.139, P1.MEDIA.140.

### Физические носители: основа — `media-objects`

**Начато; есть основа или предмет.**

Есть: Диски, дискеты и футляры описаны как переносимые предметы.

Осталось: Установка/чтение носителей и воспроизведение контента не установлены.

Источники: [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.MEDIA.012.

### Карта оригинала и её локации — `world-map`

**Доделана и принята база.**

Есть: Каноническая карта frozen 04A1 уже импортирована как sanitized временная runtime-база; география и landmarks приняты.

Осталось: Статус относится к карте/презентационной базе, а не ко всем занятиям в каждом здании. Производственная замена графики относится к Phase 2.

Источники: [lock](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/DONOR_VERSION_LOCK.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [scope](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/PHASE1_SCOPE_LOCK.md>).

FeatureId: P1.WORLD.001, P1.PRESENTATION.004.

### 49 ячеек и additive streaming — `streaming`

**Доделана и принята база.**

Есть: Существующая 49-cell архитектура и активный профиль имеют Verified; принятая база сохранена.

Осталось: Поздние движущиеся актёры и ещё не существующие save-домены требуют собственной проверки; это не автоматическое закрытие их функций.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [migration](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Architecture/UNITY_660_MIGRATION_2026-09-06.md>); [save_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Save/Integration/NativeSaveSessionController.cs>).

FeatureId: P1.WORLD.002.

### Земля, дороги, коллизии и безопасное перемещение — `world-physical`

**Работает частично.**

Есть: Глобальные непрерывные объекты, interim collision, проверки порогов/ступеней и OOB recovery реализованы.

Осталось: Весь мир ещё требует coverage маршрутов/коллизий; donor sprite-лес/плоские proxy/визуальные дефекты остаются документированным временным долгом.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [player](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Player/PLAYER_TRAVERSAL_AND_LEAN_AUDIT_2026-08-10.md>); [migration](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Architecture/UNITY_660_MIGRATION_2026-09-06.md>).

FeatureId: P1.WORLD.003, P1.WORLD.004, P1.WORLD.006, P1.WORLD.008.

### Мировые триггеры, переходы и часы — `world-utilities`

**Начато; есть основа или предмет.**

Есть: Есть карта, anchors, time outputs и отдельные домашние/сервисные взаимодействия.

Осталось: Полное покрытие utilities/переходов, часов, знаков и всей diegetic-обратной связи не подтверждено.

Источники: [composition](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs>); [home_comp](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Bootstrap/ProductionHomeInstaller.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.WORLD.007, P1.WORLD.012.

### Время, погода, дождь, мокрота и молнии — `weather`

**Работает частично.**

Есть: Проектное время/календарь, weather schedule, wetness и gameplay lightning; Enviro 3 подключён отдельным адаптером, не владеет игровой симуляцией.

Осталось: Не все donor-параметры/сценарии и материалы мокроты проверены. Исторический Enviro fingerprint drift остаётся в документации миграции.

Источники: [migration](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Architecture/UNITY_660_MIGRATION_2026-09-06.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [scope](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/PHASE1_SCOPE_LOCK.md>).

FeatureId: P1.WORLD.009, P1.WORLD.010, P1.PRESENTATION.003.

### Слои мира и замена временной графики — `replacement`

**Работает частично.**

Есть: Стабильные ID, legacy/gameplay/override разделение и replacement keys существуют.

Осталось: Полная замена всего контента с сохранением каждого позднего домена ещё не проверена; production reauthoring отложен.

Источники: [scope](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/PHASE1_SCOPE_LOCK.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [coverage](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Save/FULL_GAME_SAVE_COVERAGE.csv>).

FeatureId: P1.WORLD.011.

### Меню, настройки и HUD — `ui`

**Доделана и принята база.**

Есть: 08A project-owned UI reference lock принят; реальные меню/save/settings/HUD и позднее одобренное оформление остаются частью базы.

Осталось: Принимать все будущие экраны заданий/полиции/медиа автоматически нельзя; они зависят от своих реальных данных.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [09a](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09A_REPORT.md>); [migration](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Architecture/UNITY_660_MIGRATION_2026-09-06.md>).

FeatureId: P1.PRESENTATION.001.

### Аудиоархитектура и игровые звуки — `audio`

**Работает частично.**

Есть: IAudioBackend, event IDs, fallback/Wwise-граница, временные clips и реальные звуки предметов, двигателя, погоды/реакций.

Осталось: Весь donor soundscape и dedicated transport/будущие события не покрыты; final authored audio относится к Phase 2.

Источники: [audio](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Audio/SATSUMA_RUNNING_MIX_AND_STARTER_CONTINUITY_2026-09-06.md>); [traffic_audio](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/TRAFFIC_AUDIO_RUNTIME_BOUNDARY.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.TRAFFIC.009, P1.PRESENTATION.002, P1.PRESENTATION.009.

### Персонажи и анимационная основа — `characters-view`

**Реализовано; полный паритет не принят.**

Есть: Проектные wrappers, presentation/animation binding и сохранение авторитета project-owned состояния реализованы.

Осталось: Это не все обязательные NPC, расписания, диалоги и сюжет. Полная Legacy presentation приёмка и все совместимости ещё открыты.

Источники: [10a](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_10A_REPORT.md>); [characters](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Content/Phase1Foundation/CharacterDefinitionCatalog.asset>); [npc](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/NPC/Runtime/NpcWorldRuntime.cs>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.PRESENTATION.005, P1.PRESENTATION.008.

### Временная презентация транспорта — `vehicles-view`

**Работает частично.**

Есть: Satsuma и реализованные traffic/story/transport actors имеют project-owned presentation.

Осталось: Все обязательные управляемые машины и варианты ещё не покрыты.

Источники: [engine](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md>); [story_traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_10B_R4_PHYSICAL_STORY_TRAFFIC_REPORT.md>); [traffic](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.PRESENTATION.006.

### Временная презентация предметов — `items-view`

**Работает частично.**

Есть: Есть импортёр, wrappers и отчёт презентации; в последнем отчёте 147 презентационных записей.

Осталось: 152 определения не равны 152 завершённым моделям и тем более 152 полным механикам; fidelity/все действия не закрыты.

Источники: [items_view](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/ITEM_LEGACY_PRESENTATION_BUILD_REPORT.md>); [items](<E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset>); [09b](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Milestones/MILESTONE_09B_REPORT.md>).

FeatureId: P1.PRESENTATION.007.

### Временные материалы оригинала — `temporary-art`

**Временное решение принято с долгом.**

Есть: TemporaryDirectImport допустим и принят как временная частная Phase 1 презентация.

Осталось: ProductionReady из этого не следует; новые модели, PBR-текстуры, анимация, звук и финальная полировка относятся к Phase 2 после её разрешения.

Источники: [scope](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/PHASE1_SCOPE_LOCK.md>); [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>).

FeatureId: P1.PRESENTATION.010.

### Необязательный заблокированный пункт — `nonrequired-blocked`

**Необязательный заблокированный пункт.**

Есть: Исходный Required=No сохранён; пункт не включён в 517 обязательных требований.

Осталось: Донорская идентичность/обязательность или решение по импорту требуют отдельного evidence/решения; аудит не меняет scope.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [scope](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/PHASE1_SCOPE_LOCK.md>); [lock](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/DONOR_VERSION_LOCK.md>).

FeatureId: P1.SAVE.007, P1.ITEM.180, P1.ITEM.181, P1.ITEM.182.

### Исключено из выбранной версии — `nonrequired-excluded`

**Исключено из выбранной версии.**

Есть: Исходный Required=No сохранён; пункт не включён в 517 обязательных требований.

Осталось: Донорская идентичность/обязательность или решение по импорту требуют отдельного evidence/решения; аудит не меняет scope.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [scope](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/PHASE1_SCOPE_LOCK.md>); [lock](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/DONOR_VERSION_LOCK.md>).

FeatureId: P1.PLAYER.015, P1.NPC.046, P1.NPC.103.

### Необязательный спорный кандидат — `nonrequired-decision`

**Необязательный спорный кандидат.**

Есть: Исходный Required=No сохранён; пункт не включён в 517 обязательных требований.

Осталось: Донорская идентичность/обязательность или решение по импорту требуют отдельного evidence/решения; аудит не меняет scope.

Источники: [matrix](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv>); [scope](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/PHASE1_SCOPE_LOCK.md>); [lock](<E:/GAYmDev_Studio/MySummerCar_Remake/Docs/Phase1/DONOR_VERSION_LOCK.md>).

FeatureId: P1.NPC.021, P1.SERVICE.022, P1.AUTHORITY.006, P1.NEEDS.016, P1.NEEDS.017, P1.NEEDS.018, P1.COMMS.118, P1.EVENT.103, P1.EVENT.104, P1.EVENT.105.

