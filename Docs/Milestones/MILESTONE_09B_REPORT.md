# Milestone 09B — World Items, Consumables and Containers

Статус: **implementation baseline complete / ручной production playtest и
row-specific donor verification требуются**  
Дата: 2026-07-23

## Итог

Создан project-owned предметный фундамент Phase 1: immutable definitions,
mutable instances, stable IDs, физическое взаимодействие, контейнеры, жидкости,
упаковки, инструменты, streaming archival, out-of-bounds recovery и отдельный
save domain. Реализация расширяет принятые Player, Interaction, World Streaming,
Vehicle Assembly и 09A Save boundaries без их замены.

Это не заявление о полной donor-паритетности всех 09B строк. В authoritative
matrix `100` required-строк честно остаются `PartiallyImplemented`, три optional
строки — `Blocked`, `Verified=0`. Точные специализированные действия, UI/audio и
row-by-row donor comparison перечислены как открытые evidence gaps.

## Что было проверено перед изменениями

- полностью прочитаны `AGENTS.md`, prompt 09B, current state, project design
  guardrails, Phase 1 scope/execution/definition-of-done и отчёт 09A;
- проаудированы текущие Interaction, carry, stable identity, streaming, vehicle
  assembly и native save implementations;
- donor hierarchy и FSM использовались только как read-only evidence;
- сохранены принятые контракты 00–08A и 09A;
- pre-existing reflection-probe файлы под
  `Assets/Game/Bootstrap/Bootstrap/` не изменялись.

## Реализовано

### Предметный runtime

- `MSC.Items.Runtime` с immutable `ItemDefinition` и mutable
  `ItemInstanceState`;
- `86` уникальных runtime definitions: `82` authoritative roster definitions и
  четыре внутренних spawned-child definitions;
- `43` canonical placements с project-owned stable instance IDs;
- явные состояния content/open/broken/enabled/variant/liquid/container;
- donor-style pickup/carry contract: ЛКМ берёт или физически отпускает,
  ПКМ бросает, `F` использует, колесо вращает удерживаемый предмет; отдельный
  surface-snapped placement не назначен и остаётся Phase 2 задачей;
- колесо вращает на `30°` за нормализованный шаг; СКМ циклически переключает
  оси `Y → X → Z` с краткой экранной подсказкой. Целевая ориентация хранится
  относительно точки хвата, поэтому движение камеры само предмет не вращает;
- постоянная центральная точка превращается в крест на доступной цели; под ней
  отображается имя/текущее состояние предмета;
- общий lively-physics профиль предметов: низкое демпфирование, умеренное
  трение/упругость, более поздний сон Rigidbody, повышенные solver limits,
  округлые capsule colliders для бутылок и части цилиндрических предметов;
  CharacterController физически толкает переносимые по массе Rigidbody;
  остаточное скольжение в реальной точке контакта преобразуется в угловую
  скорость, поэтому предмет начинает катиться/опрокидываться от поверхности,
  а не продолжает движение как по льду;
- use/consume result с vendor-neutral `ItemUseEffectDelta`; Needs применит эти
  эффекты в 09C, не создавая обратной зависимости Items → Needs;
- жидкостный transfer только для открытых совместимых ёмкостей, с защитой от
  mixed/empty/broken/consumed state;
- bounded packages и deterministic child IDs: beer `24`, spark plugs `4`,
  lightbulb `1`, fuses `5`, R20 batteries `4`;
- `F` на beer case извлекает одну полную persistent бутылку и уменьшает
  содержимое ящика; употребление и выброс пустой тары выполняются только через
  `F` уже на самой бутылке;
- floppy как одна definition с тремя data-driven variants и тремя canonical
  initial indices;
- tool identity/size compatibility; fastener больше нельзя крутить пустыми
  руками или неподходящим ключом;
- централизованный recovery критических предметов после выхода за world bounds;
- `ItemActionCompleted` и `IItemStatusSource` для последующего UI/audio binding
  без RPG inventory.

### Streaming и сохранения

- новый required domain `items.instances`, configuration
  `items.instances.native.v1`, restore phase `150`;
- `SaveDocument.CurrentDocumentVersion=2` и явная миграция `1 -> 2`, добавляющая
  пустой item domain без изменения исходного слота до успешного load;
- logical item state восстанавливается до `world.entities` physical pose;
- dynamic items регистрируются/удаляются в world save registry;
- unloaded-cell item state хранится deferred и материализуется при регистрации
  точной source cell;
- унесённые за границу исходной ячейки предметы остаются persistent;
- load старого save удаляет более поздние dynamic children, поэтому повторная
  выдача упаковки не создаёт duplicate stable ID;
- rollback не материализует предмет, который до операции был deferred;
- global uniqueness включает top-level и contained IDs; malformed container,
  package-count и liquid state отклоняются до применения.

### TemporaryDirectImport presentation

- deterministic allowlist и read-only evidence normalization;
- `46` project-owned presenter bindings, `90` synchronized mesh assets и `17`
  material slots;
- gameplay не использует donor filenames/hierarchy для поиска или логики;
- определения без binding используют project-owned proxy fallback;
- generated payload остаётся под ignored
  `Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/` и не входит в Git;
- world sanitation validator исключает этот отдельно валидируемый 09B subtree из
  canonical world fingerprint, не ослабляя проверку world payload.

### Матрицы и provenance

- category roster: `20/20 PartiallyImplemented`;
- concrete roster: `79 PartiallyImplemented + 3 Blocked optional`;
- parity matrix 09B: `100 PartiallyImplemented + 3 Blocked`, `Verified=0`;
- save coverage 09B: `99 Partial + 1 NotRequired + 3 Uncovered`;
- CSV структурно ровные, ID уникальны, ссылки roster → parity → save coverage
  однозначны;
- SHA-256 `ITEM_DEFINITION_SUBROSTER.csv` совпадает с ledger:
  `6D9A828F6CA3990FC2CED9C490B1C99C6AB589454DD47569ED0D3FF7A9E55DE9`.

## Основные файлы

- `Assets/Game/Items/Runtime/` — definitions, instances, actions, presentation
  boundary, placement/runtime/save DTO;
- `Assets/Game/Items/Content/` — definition и placement catalogs;
- `Assets/Game/Items/Editor/` — catalog builder, donor evidence allowlist,
  presentation pipeline и validators;
- `Assets/Game/Save/Integration/ItemSaveParticipant.cs` и
  `Milestone09BSaveMigration.cs`;
- bounded extensions в Bootstrap, Interaction, Player, World Streaming и
  Vehicle Assembly;
- `Assets/Game/Tests/EditMode/Items/` и расширенные regression tests;
- `Docs/Items/M09B_DONOR_ITEM_EVIDENCE.md`;
- item rosters, parity/save matrices, presentation report, porting ledger,
  system map и native save architecture.

## Выполненные проверки

- Unity compile: exit code `0`, C# errors/warnings не обнаружены в финальном
  compile log;
- после control/crossdot/lively-physics корректировки:
  `dotnet build MSC.Items.Runtime.csproj` и
  `dotnet build MSC.Player.Runtime.csproj` — по `0` ошибок и предупреждений;
- после collider/beer-case корректировки: Items Runtime и Items EditMode test
  assembly — `0` ошибок/предупреждений; Items Editor — `0` ошибок и только `14`
  существующих serialization DTO warnings из UI/WorldTransfer dependencies;
- Items EditMode: **20/20 Passed**;
- Player Interaction EditMode: **15/15 Passed**;
- Save regression EditMode: **39/39 Passed**;
- Vehicle Assembly EditMode: **22/22 Passed**;
- Save Coverage validator после исправления contract row: **5/5 Passed**;
- catalog build: `M09B_ITEM_CATALOG_BUILD_PASS`;
- presentation plan: `46` bindings, deterministic revision accepted;
- generated presentation validation: `46` bindings accepted;
- targeted world sanitation после отделения Items subtree: ожидаемо остаётся
  красным только на pre-existing world payload fingerprint drift;
  `Generated/Items` в сообщении больше отсутствует;
- `git diff --check`: без whitespace errors;
- CSV/ID/hash audit: без структурных ошибок, дубликатов и битых cross-links.

Полный существующий EditMode suite зафиксировал **475/490 Passed, 15 Failed** до
финального исправления save-contract CSV. Один новый SaveCoverage failure после
этого исправления отдельно подтверждён зелёным прогоном `5/5`.

Двенадцать остальных падений совпадают с baseline 09A: GaragePrototype lighting
`2`, donor world baseline/cellization/material policy `8`, WorldValidation
PilotGate `1`, WorldTransfer source hashes `1`. Два дополнительных WeatherLab
теста стабильно падают из-за существующего несовпадения dev-профиля: после
усиления Enviro adapter профиль `WeatherLabHDRPVolume.asset` не содержит
`IndirectLightingController`; production Bootstrap использует другой профиль и
этой причиной не затронут. WeatherLab regression не исправлялся внутри bounded
09B.

## Ручная проверка в Unity

1. Открыть `Assets/Game/Bootstrap/Bootstrap.unity`, запустить Play Mode и начать
   новую игру.
2. Убедиться, что в загруженных ячейках предметы появляются по одному; у
   TemporaryDirectImport предметов видна donor presentation, у остальных —
   читаемый proxy, без duplicate stable-ID ошибок в Console.
3. Убедиться, что без цели постоянно видна точка, а при наведении на предмет —
   крест и его имя/остаток.
4. Взять несколько предметов: ЛКМ должен отпустить их без привязки к
   поверхности, ПКМ — бросить, колесо — быстро вращать по выбранной оси,
   СКМ — переключать `Y/X/Z`. При движении камеры ориентация относительно
   экрана должна сохраняться. Бутылка после броска должна естественно
   опрокидываться/катиться, лёгкий предмет — сдвигаться при ходьбе игрока в него.
5. Использовать порционный consumable: content/status должен уменьшаться, empty
   state — сохраняться. Изменение потребностей появится в 09C; в 09B проверяется
   только authored use-result/status.
6. Открыть source и target containers и выполнить transfer. Совместимая жидкость
   переносится, закрытая/пустая/сломанная ёмкость и смешивание разных жидкостей
   отклоняются без потери содержимого.
7. Проверить beer case: бутылки стоят ровной сеткой `6×4`; `F` извлекает одну
   полную бутылку рядом с ящиком и уменьшает счётчик. Взять её ЛКМ и нажать
   `F`: только теперь пиво употребляется, а пустая бутылка выбрасывается.
   Повторное действие не создаёт дубликат с тем же stable ID.
8. Открыть spanner set, выбрать размеры `8–17`. Болт автомобиля должен вращаться
   только подходящим ключом; пустые руки и неверный размер ничего не меняют.
9. Перенести dynamic item, сохранить слот, уйти достаточно далеко для выгрузки
   исходной ячейки, вернуться, затем загрузить save. Transform, velocity,
   content/open/variant/container state должны восстановиться без дубликатов.
10. Уронить critical item за нижнюю границу мира: он должен вернуться в recovery
   pose, а не быть потерян навсегда.
11. Открыть `Tools/MSC Remake/Save/Native Save Inspector`: слот должен содержать
    `items.instances` и `world.entities`, без неожиданных missing required
    domains и unresolved stable IDs.

## Ограничения и риски

- exact milk/beer/booze/cigarette effects и подключение Needs отложены до 09C;
- cooking/spoilage, kilju formula/action order, disposable/garbage variants,
  spray color mapping и специальные действия jack/hoist/axe/прочих инструментов
  ещё не прошли полный donor comparison;
- spawned child use/install presentation подтверждена не для всех четырёх типов;
- screwdriver/ratchet concrete roster и optional trophy/eyewear/hat reachability
  требуют дополнительного donor evidence;
- UI/audio feedback и все row-specific manual routes ещё не выполнены;
- только `46` definitions имеют donor presenter; остальные временно используют
  proxy;
- поэтому ни одна 09B строка не повышена до `Verified`.

## Финальная калибровка физики предметов

- Из канонического `GAME.unity` read-only перенесены `46` доступных значений
  donor `Rigidbody.mass`; среди них beer case `9 kg`, gasoline/diesel can
  `21 kg`, garbage barrel `30 kg`, sofa `90 kg`, floor jack и motor hoist
  `9999 kg`.
- Для определений без доступного donor Rigidbody назначены явные временные
  категорийные массы. Они являются проектной Phase 1 калибровкой, а не
  заявлением о точном переносе donor-конфигурации.
- Runtime выбирает ограниченный PhysX-профиль по массе: лёгкий, средний,
  тяжёлый или практически неподвижный. Дополнительное преобразование
  контактного скольжения во вращение применяется только к округлым предметам и
  мячам, а не ко всем объектам.
- Basketball и football используют сферический collider; бутылки и прочие
  перечисленные округлые предметы сохраняют capsule collider. Beer case
  остаётся одним физическим телом, а отдельная бутылка материализуется только
  при извлечении.
- Канонические стартовые предметы после построения presentation/collider
  получают нулевые скорости, остаются динамическими и явно пробуждаются.
  Свободный предмет всегда использует gravity/collision; перенос временно
  владеет телом, а release/throw возвращает обычный PhysX с сохранением
  импульса и вращения. Это соответствует принятому для проекта живому
  VotV-подобному профилю вместо статичных/кинематических декораций.
- Документ v9 содержал регрессию совместимости: отсутствовавшее в старом JSON
  поле `useGravity` читалось как `false`. Миграция `9 -> 10` ремонтирует только
  stable IDs из authoritative `items.instances`, переводит их
  `world.entities` в schema 2 и не меняет прочие pickup targets.
- Basketball использует отдельный упругий PhysicsMaterial (`0.68` bounce,
  Maximum combine), низкий linear/angular damping и reviewed donor
  albedo/normal в project-owned HDRP/Lit material.
- Helmet использует reviewed `racing_accessories` donor atlas как временную
  Phase 1 presentation. Донорский `Paint` FSM не перенесён: project-owned
  `paint-color-r/g/b`, `paint-applied` и `paint-matte` хранятся в item state,
  применяются через `HelmetPaintPresentation` и мигрируют `10 -> 11`.
- После hotfix выполнены Item/Save EditMode `50/50`, целевой throw PlayMode
  `1/1` и lighting EditMode `9/9`. Полный PlayerInteraction fixture остаётся
  `4/5` из-за ранее существующего отсутствующего action `Player/Run`; новый
  throw test в нём проходит.

## Совместимость

Публичные и serialized контракты 00–08A не переименовывались и не удалялись.
Player/Interaction/Streaming/Vehicle получили bounded capability/lifecycle
extensions. Save schema мигрирует с `1` на `2`; старые слоты сохраняются и
получают пустой item domain. Temporary presentation сменяется через stable IDs и
replacement keys без изменения simulation/save state.

## Следующий milestone

Ровно один рекомендуемый следующий milestone после ручного принятия 09B:
**09C — Player Needs, Home State and Daily Survival Loop**.
