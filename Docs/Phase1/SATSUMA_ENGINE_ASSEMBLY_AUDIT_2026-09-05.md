# Satsuma: фундаментальная сборка двигателя, крепёж и отказ

Дата: 2026-09-05. Статус: **bounded donor audit**. В этом документе код и
Unity-ассеты не изменялись. Классификация: `BehavioralReference` для
проектной `Reimplemented` механики сборки.

## Границы аудита

Подробно проверены:

- блок двигателя как хозяин внутренних mount point;
- коленвал;
- три крышки коренных подшипников (`main bearing 1/2/3`);
- четыре поршня;
- пороги `Bolted`, RPM-ветки отрыва и попытка установки поршня на
  незакреплённый коленвал;
- ближайшая граница цепочки: штатный/racing маховик, крышка сцепления,
  нажимной диск и диск сцепления.

Это **не** полный аудит всего двигателя. Распредвал, ГРМ, головка, прокладка,
масляная/охлаждающая системы, коробка, привод, зажигание, запуск и приборка
в этом проходе не объявляются проверенными. Приборка и ignition остаются
следующим отдельным этапом по просьбе пользователя.

Принятые подвеска, NWH, колёса, кузов, ручник, сохранения и UI не затрагивались.
Unity в рамках этого аудита не запускалась.

## Источник и метод

Authority — зафиксированная версия
`msc-world-baseline-04a1.1-c3f2f337`, Steam build `20171487`:

```text
E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/
  milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity
```

SHA-256 source scene:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Оригинал и DonorStaging читались только read-only.

По известным Component ID выполнялся `rg -b` по заголовкам YAML-документов,
затем bounded `FileStream.Seek` до следующего `--- !u!`. Сопоставлялись
состояния, `actionNames`, индексы действий, параметры, object references и
`byteData`. Весь `GAME.unity` в память не загружался.

Существующие CSV использовались только как индекс геометрии/ID:

- `Phase1SatsumaV1dLoosePartAssemblyFsmAudit.csv`;
- `Phase1SatsumaV1dLoosePartFastenerAudit.csv`;
- `Phase1SatsumaV1dBoltCheckAudit.csv`;
- `Phase1SatsumaV1cFastenerAudit.csv`.

Семантика условий ниже подтверждена raw FSM, а не выведена из имени поля в
CSV. Все ID в таблицах — сериализованные donor GameObject/Transform/Component
ID, не Unity runtime InstanceID.

## 1. Карта объектов и точные FSM

| Деталь | Loose GO / Transform | Database GO | Assembly Component @ byte offset | Removal Component @ byte offset | BoltCheck Component @ byte offset |
|---|---:|---:|---:|---:|---:|
| Коленвал | `32707 / 68769` | `32629` | `111483 @ 181309113` | `113480 @ 218236388` | отдельного собственного BoltCheck нет |
| Main bearing 1 | `18105 / 54164` | `30236` | `113599 @ 220283243` | `109230 @ 140185507` | `109231 @ 140218351` |
| Main bearing 2 | `24564 / 60626` | `29019` | `109146 @ 138842761` | `111033 @ 173445790` | `111034 @ 173478635` |
| Main bearing 3 | `8174 / 44228` | `31512` | `104327 @ 47860296` | `106387 @ 85260644` | `106388 @ 85293480` |
| Piston 1 | `17851 / 53913` | `2170` | `106238 @ 82412856` | `109160 @ 139179124` | `109161 @ 139205581` |
| Piston 2 | `18808 / 54867` | `16624` | `107760 @ 113843367` | `109403 @ 143246907` | `109404 @ 143273371` |
| Piston 3 | `20011 / 56076` | `20184` | `105370 @ 66778562` | `109724 @ 149172032` | `109725 @ 149198493` |
| Piston 4 | `8049 / 44101` | `10907` | `113228 @ 213676495` | `106355 @ 84559565` | `106356 @ 84586028` |

Loose block: GO `30853`, Transform `66906`, runtime definition
`vehicle.satsuma.part.engine-block`. Его внутренние trigger/pivot принадлежат
самому loose block, поэтому оригинал разрешает собирать двигатель на полу, а
не только после установки блока в кузов.

### Запрошенные недостающие Removal ID

Короткий authoritative ответ для E1/E2:

- main bearing 2 — `Removal 111033`, byte offset `173445790`;
- main bearing 3 — `Removal 106387`, byte offset `85260644`;
- piston 1 — `Removal 109160`, byte offset `139179124`;
- piston 2 — `Removal 109403`, byte offset `143246907`;
- piston 3 — `Removal 109724`, byte offset `149172032`;
- piston 4 — `Removal 106355`, byte offset `84559565`.

Для полноты: main bearing 1 — `Removal 109230 @ 140185507`, crankshaft —
`Removal 113480 @ 218236388`.

## 2. Коленвал: установка и снятие

### Установка — `Assembly 111483`

В состоянии `Already installed` FSM читает:

- собственный `Crankshaft 32629 / Data.Installed`;
- `Oilpan 23052 / Data.Installed`;
- `CrankBearing1 30236 / Data.Installed`;
- `CrankBearing2 29019 / Data.Installed`;
- `CrankBearing3 31512 / Data.Installed`;
- `Timingcover 22044 / Data.Installed`.

`BoolNoneTrue` ведёт в `ASSEMBLE`, `BoolAnyTrue` — обратно в `LOOP`.
Следовательно, для установки коленвала должны отсутствовать **все пять**
мешающих деталей: поддон, три крышки коренных подшипников и крышка ГРМ.
Собственный крепёж у mount коленвала отсутствует.

Важная, но не очень интуитивная деталь: оригинал позволяет поставить крышку
коренного подшипника без коленвала. Но после этого коленвал уже не вставить,
пока крышку снова не снять. Это не основание изобретать prerequisite
`bearing requires crankshaft`.

### Ручное снятие — `Removal 113480`

`Requirements` читает одиннадцать boolean:

1. собственный `Crankshaft.Data.Bolted`;
2. `CrankBearing1.Data.Installed`;
3. `CrankBearing2.Data.Installed`;
4. `CrankBearing3.Data.Installed`;
5. штатный `Flywheel 29466.Data.Installed`;
6. `Timingcover 22044.Data.Installed`;
7. racing flywheel `18977.Data.Installed`;
8. `Piston1 2170.Data.Installed`;
9. `Piston2 16624.Data.Installed`;
10. `Piston3 20184.Data.Installed`;
11. `Piston4 10907.Data.Installed`.

Только `BoolNoneTrue` разрешает `REMOVE`; любое true возвращает в `LOOP`.
То есть коленвал снимается лишь когда его donor-флаг `Bolted=false` и
отсутствуют три крышки, оба варианта маховика, крышка ГРМ и все четыре поршня.

`Oilpan.Installed` в этом Removal predicate **не читается**. Нельзя
симметрично скопировать установочный запрет поддона в снятие «потому что так
красивее»: это было бы уже наше правило, а не правило зафиксированного донора.

## 3. Крышки коренных подшипников

### Установка

Все три `Assembly` имеют одну и ту же форму:

- собственная крышка должна быть не установлена;
- `Oilpan 23052.Data.Installed` должен быть false;
- состояния `Crankshaft.Installed` или `Crankshaft.Bolted` не читаются.

| Крышка | Assembly | ThisPart | Parent pivot GO |
|---|---:|---:|---:|
| 1 | `113599` | `30236` | `33345` |
| 2 | `109146` | `29019` | `35279` |
| 3 | `104327` | `31512` | `4462` |

Следовательно, поддон блокирует установку каждой крышки, но коленвал не
является prerequisite.

### Снятие

Все три `Removal.Requirements` читают ровно два meaningful условия:

- `db_RemoveReq1 = Oilpan 23052.Data.Installed`;
- `db_ThisPart = собственная крышка.Data.Bolted`.

`BoolNoneTrue -> REMOVE`, `BoolAnyTrue -> LOOP`.

| Крышка | Removal | Условие ручного снятия |
|---|---:|---|
| 1 | `109230` | oilpan отсутствует, `CrankBearing1.Bolted=false` |
| 2 | `111033` | oilpan отсутствует, `CrankBearing2.Bolted=false` |
| 3 | `106387` | oilpan отсутствует, `CrankBearing3.Bolted=false` |

Сам коленвал снятию крышки не мешает — иначе штатная разборка была бы
логически невозможна.

## 4. Поршни

### Установка: две разные проверки, которые нельзя склеивать

У каждого из четырёх `Assembly` сначала выполняется `Requirements`:

- `Crankshaft 32629.Data.Installed=true` разрешает продолжить;
- false возвращает в ожидание.

Затем `Already installed` требует одновременно:

- собственный поршень ещё не установлен;
- `Cylinderhead 10083.Data.Installed=false`.

После команды установки FSM отдельно входит в `Check bolts` и читает
`Crankshaft.Data.Bolted`:

- true — выполняется `Assemble` поршня;
- false — выполняется `Drop part`, который пишет
  `Removal.Detach=true` установленному `crankshaft(Clone)` GO `32707`.

Иными словами, присутствия коленвала достаточно, чтобы точка установки стала
доступна, но для успешной фиксации поршня коленвал ещё должен считаться
`Bolted`. При неудаче оригинал не ставит поршень и пытается отцепить коленвал.
Это отдельный attempt-time outcome, а не обычная обратная dependency.

### Снятие

Все четыре `Removal.Requirements` проверяют:

- собственный `PistonN.Data.Bolted=false`;
- `Cylinderhead 10083.Data.Installed=false`.

| Поршень | Removal | ThisPart DB | Remove blocker DB |
|---|---:|---:|---:|
| 1 | `109160` | `2170` | Cylinderhead `10083` |
| 2 | `109403` | `16624` | Cylinderhead `10083` |
| 3 | `109724` | `20184` | Cylinderhead `10083` |
| 4 | `106355` | `10907` | Cylinderhead `10083` |

Головка блока, таким образом, блокирует и установку, и ручное снятие каждого
поршня. Одного раскручивания шатунных болтов при установленной головке
недостаточно.

## 5. Крепёж, hysteresis и RPM-break

Каждая крышка и каждый поршень имеет два реальных крепежа со стадиями `0…8`.
`T` ниже — сумма стадий группы.

| Узел | Крепёж | Max T | Donor Bolted ON / OFF | Текущий generated ON / OFF |
|---|---|---:|---:|---:|
| Main bearing 1/2/3 | по 2 болта, ключ 9 мм | 16 | `10 / 0` | `1 / 0` |
| Piston 1/2/3/4 | по 2 болта, ключ 7 мм | 16 | `2 / 0` | `1 / 0` |

Это hysteresis, а не «Bolted равен Tightness больше нуля»:

- выключенная защёлка включается при достижении ON;
- включённая остаётся true при частичном ослаблении;
- OFF наступает при `T<=0`.

### Необычный общий флаг коленвала

Каждый из трёх `BoltCheck` крышек пишет одновременно:

- `Data.Bolted` своей крышки;
- общий `Crankshaft 32629.Data.Bolted`.

Это **три независимых писателя одного donor bool**. Из raw FSM доказан сам
факт записи, но не доказано безопасное высокоуровневое правило вроде
«Crankshaft.Bolted=true только когда затянуты все шесть болтов». Результат
может зависеть от порядка обновления FSM. Поэтому E1 правильно не подменяет
эту странность выдуманной агрегацией. Shared crank B требует отдельного
наблюдения оригинала и детерминированной спецификации.

### Отрыв на оборотах

У всех семи проверенных `BoltCheck` есть donor-ветка:

1. при `RPM > 300` перейти к `Chance`;
2. вычислить вес `w=(15-T)/100`;
3. при неудавшемся отрыве ждать `1.0 s`, `realTime=true`, затем проверять снова;
4. при `BREAK` сбросить стадии, `Tightness` и `Data.Bolted`, включить Removal и
   продолжить узловой сценарий повреждения/отрыва.

Для крышек `Break off` сбрасывает собственную защёлку и использует их связь с
коленвалом. Для поршней после `Break off` присутствуют состояния
`More damage`, `Block`, `Oilpan`, `Rockers`, `Throw out` и ссылки на:

- block database GO `35519`;
- oilpan `23052`;
- rocker shaft `19072`;
- собственный поршень.

Из этого доказано, что donor-сценарий умеет не только выбросить поршень, но и
маршрутизирует дополнительный ущерб к соседним узлам. Точные веса выбора,
damage delta и полный порядок side effects в этом bounded-проходе не
декодированы, поэтому их нельзя переносить «примерно на глаз».

`SendRandomEvent` получает веса, а не готовый процент. Поведение helper при
нулевом/отрицательном `15-T` отдельно не доказано; нельзя молча clamp-нуть или
переосмыслить формулу.

В текущих engine mount definitions `speedRetentionPolicy: 0`. Существующая
wheel/vehicle-speed retention не является заменой RPM authority. Переносить
сюда wheel cadence с км/ч нельзя — получится механический Франкенштейн, który
sam nie wie, po co żyje.

## 6. Доказанный порядок фундаментальной цепочки

Ниже не «единственный удобный walkthrough», а следствие проверенных ворот:

1. Блок может лежать отдельно от машины; внутренние детали всё равно ставятся.
2. Для вставки коленвала должны быть сняты поддон, все три крышки коренных
   подшипников и крышка ГРМ.
3. Крышки коренных подшипников можно поставить даже без коленвала, если нет
   поддона. Такая сборка бессмысленна, но donor FSM её допускает.
4. После установки коленвала крышки ставятся и затягиваются. Они формируют
   собственные B и одновременно пишут shared `Crankshaft.Bolted`.
5. Поршень можно начать ставить лишь при установленном коленвале и снятой
   головке. Успех дополнительно зависит от shared crank B; иначе коленвал
   получает `Removal.Detach`.
6. После установки поршни крепятся двумя 7-мм болтами каждый. Головка должна
   быть снята для последующего ручного удаления поршня.
7. Коленвал нельзя вручную снять, пока стоят хотя бы одна крышка, поршень,
   крышка ГРМ или любой из двух маховиков, и пока donor crank B true.
8. Поддон блокирует манипуляции крышками и установку коленвала, но не входит в
   raw Removal predicate коленвала.

На этом точная фундаментальная последовательность заканчивается. Любые
дальнейшие утверждения про запуск, масло, компрессию или работу стартера без
отдельных FSM/managed evidence были бы гаданием на кофейной гуще.

## 7. Маховик и сцепление: подтверждённая граница, не E1

### Маховик

`Assembly 114349 @ 233373502` на коленвале выбирает один из двух вариантов:

- штатный flywheel: database `29466`, installed GO `30646`, Transform `66698`;
- racing flywheel: database `18977`, installed GO `15556`, Transform `51614`.

Оба используют pivot GO `12530` и взаимоисключающие ветки `Assemble` /
`Assemble2`. Если установлен любой вариант, новый маховик не ставится.

Дополнительный gate читает `InspectionCover 11194.Installed` и
`Gearbox 31625.Installed`. `BoolAllTrue -> LOOP`: установка блокируется при
одновременном наличии **обоих** узлов, не каждого по отдельности. Обычный
плоский `blockedWhileOccupied[]` выражает OR и потому не может честно
представить этот AND-predicate.

Removal:

- штатный: `112894 @ 207635224`;
- racing: `108531 @ 128209328`.

Оба требуют `own Bolted=false` и отсутствия `Gearbox 31625`,
`ClutchCoverplate 11583`, `InspectionCover 11194`.

BoltCheck:

- штатный: `112895 @ 207663764`;
- racing: `108533 @ 128238884`;
- шесть болтов 7 мм, Max T=48, donor `Bolted ON/OFF = 32/0`;
- FSM также ссылается на `Engineplate 12040` как связанный bolted target.

Текущий mount `mount.satsuma.crankshaft.flywheel` принимает только
`vehicle.satsuma.part.flywheel`, имеет шесть правильных 7-мм fastener, но
порог `1/0`, пустые access arrays и не представляет racing-вариант/compound
AND-gate. Это отдельный пакет, не повод раздувать E1.

### Ближайшие узлы сцепления

Подтверждены следующие FSM/ID:

- clutch cover Assembly `112523 @ 201314868`, Removal
  `109148 @ 138871176`, BoltCheck `109149 @ 138903119`;
- clutch pressure plate Assembly `106353 @ 84533332`, Removal
  `111105 @ 174607585`;
- clutch disc Assembly `113571 @ 219640417`, Removal
  `108162 @ 120582928`.

Cover Removal читает собственный B и присутствие gearbox/drive gear;
BoltCheck cover имеет donor ON/OFF `36/0` и связывает cover с pressure plate и
clutch disc. Inner-part Assembly/Removal содержит собственный порядок и
attempt-time проверки. Их полный action graph в этом проходе не доведён до
уровня, достаточного для безопасного authoring, поэтому текущие пустые access
arrays не исправляются предположением.

## 8. Сравнение с нашим runtime

### Что было неверно до E1

Автоматический importer в
`Phase1SatsumaBaselineBuilder.cs:7438-7484` переносил только:

- `RequiredGameObjectId` / `Required1GameObjectId` как
  `InstallRequiresInstalled` и обратный removal dependency;
- `DetachPartGameObjectId` как статический
  `RemovalBlockedWhileInstalled`.

Он не реконструировал donor `db_NotInstalled` и actual
`Removal.Requirements`. Поэтому generated crank/bearing/piston definitions
изначально пропускали установку через поддон/крышку ГРМ/головку и часть
неправильного снятия.

В prefab уже были:

- `piston1..4 -> crankshaft` install requirements
  (`Satsuma_Phase1_V1a.prefab:108744-108755`);
- `crankshaft -> piston1..4` manual removal blockers
  (`:108726-108737`).

Это покрывало только часть raw FSM.

### Что E1 уже исправил и проверил

Параллельный E1-пакет адресно обновил восемь существующих definitions:

- crank install blockers: oilpan, bearing1/2/3, timing cover;
- crank manual removal blockers: bearing1/2/3, timing cover;
- bearing1/2/3 install + removal blocker: oilpan;
- piston1/2/3/4 install + removal blocker: cylinder head;
- прежний piston requires crankshaft сохранён.

По отчёту owning-сессии после scoped refresh:

- refresh изменил 8 assets, повторный refresh — 0;
- focused EditMode: `125/125`, включая 19 новых E1 tests;
- assembly PlayMode: `11/11`;
- 120 protected hashes после тестов совпали с post-refresh baseline.

Это результаты E1-сессии, а не тесты, выполненные данным read-only аудитом.
Подробности находятся в
`SATSUMA_ENGINE_ACCESS_RULES_FIX_2026-09-05.md`.

### Что всё ещё отличается

1. Main bearing B остаётся `1/0` вместо donor `10/0`.
2. Piston B остаётся `1/0` вместо donor `2/0`.
3. Shared `Crankshaft.Bolted` с тремя writers не воспроизведён.
4. Piston `Check bolts -> Drop crankshaft` не воспроизведён как
   attempt-time failure. Generic dependency только блокирует снятие и не
   эквивалентен `Removal.Detach` при неудачной установке.
5. RPM>300 cadence, шанс, reset крепежа, выброс поршня и collateral damage
   отсутствуют.
6. Racing flywheel, compound flywheel AND-gate и точные flywheel/clutch
   access predicates не представлены.
7. Flywheel и clutch cover generated thresholds остаются `1/0` вместо
   donor `32/0` и `36/0`.

`AssemblyGraph.HasInstalledRemovalBlocker` также автоматически блокирует
снятие part при любом занятом mount, owner которого равен этому part
(`AssemblyGraph.cs:452-468`). Для loose engine-block это означает широкую
блокировку по внутренним детям. Соответствует ли это donor-снятию блока с
кузова, данным аудитом не доказано; это риск для отдельного block/chassis
прохода, а не готовое основание ослабить generic graph.

## 9. Что сознательно отложено

E1 ограничен access predicates. В него намеренно не входят:

- смена threshold `10/0` и `2/0`;
- shared crank B и порядок трёх writers;
- piston failed-install Drop;
- RPM-break и damage routing;
- маховик/racing-вариант/сцепление;
- engine-block-to-chassis removal parity;
- save-миграция latch при смене порогов;
- физика работающего двигателя, жидкости, электрика и cockpit controls.

Это не «забыли», а защита от ложного parity: если одновременно поменять
доступность, B, отрыв и damage, потом хрен поймёшь, кто именно устроил
масленицу с поршнем в стене.

## 10. Bounded next steps

1. **E2 — latch thresholds, save-safe.** Перенести bearing `10/0` и piston
   `2/0`; сохранить уже валидные saved B через явно протестированную migration/
   hysteresis policy. Не менять shared crank B в этом же шаге.
2. **E3 — shared crank B + piston attempt.** В оригинале наблюдать все
   перестановки затяжки/ослабления трёх крышек и save/load. Затем определить
   project-owned deterministic rule и реализовать `Check bolts` + failed
   install без фиксации входящего поршня.
3. **E4 — RPM break.** Добавить отдельную engine-RPM authority/cadence, точную
   формулу и узловые break actions. Не переиспользовать vehicle-speed wheel
   policy. До реализации декодировать damage delta/weights и нулевые/
   отрицательные веса.
4. **E5 — flywheel/clutch.** Поддержать stock+racing alternative, AND-gate
   InspectionCover+Gearbox, removal predicates, пороги `32/0` и `36/0`, затем
   полностью доказать inner clutch ordering.
5. **E6 — остальной двигатель.** Продолжить наружу от уже проверенной цепочки:
   camshaft/gear/timing cover, oilpan, head/gasket/rockers, gearbox/drive,
   fluids и только потом runtime start/ignition.

Для каждого шага: отдельные focused EditMode rules, минимум один generated
PlayMode flow, native save roundtrip и защищённый full assembly regression.
Не менять принятые кузов/подвеску/NWH и не исполнять donor FSM в runtime.

## Итоговый вердикт

Главная прежняя ошибка была не в позах или mesh, а в том, что importer
считал несколько named references полной спецификацией Assembly. В оригинале
же доступ к внутренностям двигателя задаётся разными state-local boolean
воротами, а крепёж после установки управляет ещё и B/отрывом.

E1 уже закрывает восемь доказанных access gaps. Механика всё ещё не является
полной копией оригинала, пока не перенесены hysteresis, shared crank B,
failed-install Drop, RPM-break и соседняя flywheel/clutch chain. Эти части
нельзя смешивать в один «ну вроде работает» фикс: там слишком много
раздельных источников истины и очень легко получить работающую, но неверную
хуйню.
