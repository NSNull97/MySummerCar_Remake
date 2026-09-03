# Native Save Architecture

## Current audited state — 2026-09-02

Current native document version: **16**.

The production composition currently registers 14 participants:
`core.time`, `weather.environment`, `player.state`, `world.entities`,
`interaction.carry`, `items.instances`, `vehicle.satsuma`, `player.needs`,
`home.state`, `lighting.electrical-grid`, `economy.player`, `services.state`,
`npc.state` and `traffic.state`.

The storage, slot, integrity, `.tmp`/`.bak`, quarantine/recovery,
dependency-ordered transaction and migration foundations are implemented.
They remain `ImplementedUnverified` at the full-game gate because jobs,
communications, story, authority, rally, media and generic non-Satsuma vehicle
state are not registered complete domains and no fresh/mid/late complete-game
round trip has been accepted. See
`Docs/Reviews/FULL_GAME_PARITY_AUDIT_2026-09-02.md` and
`Docs/Save/FULL_GAME_SAVE_COVERAGE.csv`.

## 2026-09-02 streamed persistent-physics handoff

Native restore now has an explicit static-before-dynamic barrier. Bootstrap
quarantines the persistent Satsuma, performs the initial world
`RefreshNow`, retains the configured home recovery cell, applies the native
transaction, then performs a second refresh for the restored player focus and
deferred source cells before gameplay activation. The temporary recovery-cell
retention is released after materialization. This is a collision-readiness
contract; no arbitrary fixed-step delay was added.

When a base collision cell unloads, `world.entities` spatially suspends
persistent/DDOL pickups located inside that cell, while `vehicle.satsuma`
suspends its chassis, loose parts and any installed jointed/dynamic actors.
Scene-owned bodies are collisionless and kinematic before asynchronous scene
teardown. Reload order is pose under guard, `Physics.SyncTransforms`, collision
restore while kinematic, another sync, dynamics release, then a final sync.
Presentation-only vegetation/backdrop layers own no persistent physics batch.

Saved important objects below `Y=-64` are recovered to the project-owned
`anchor.home.important-object-recovery`: vehicle chassis, parts still authored
as `Loose`, and item definitions explicitly marked `CriticalRecovery`.
Installed part lifecycle/mount/fastener authority is not changed. Stable-ID
ordering, static support raycasts, overlap checks and a bounded 20 x 20
formation make placement deterministic and fail-closed. Existing save files are
not rewritten; the next normal save captures the repaired coordinates. Full
evidence and acceptance limits are in
`Docs/Phase1/ITEM_STREAMING_PHYSICS_AND_SATSUMA_SAVE_AUDIT_2026-09-02.md`.

The `world.entities` rollback checkpoint includes scene root/parent/sibling
ownership, collision/interpolation flags, rehomed-source mappings and suspension
topology. A later participant failure therefore cannot leave a pickup half
rehomed or guarded in the failed save's destination cell.

## Milestone 10B-R1 NPC-state addendum — historical v11 stage

At this historical milestone stage the native document version was **11**.
`npc.state` schema 2 is a required
production domain restored in `SaveRestorePhase.GlobalState` after `core.time`.
It contains only project-owned character definition IDs, 32-character stable
instance IDs, current schedule/anchor/route/activity state, normalized route
progress, bounded flags, relationships and dialogue-line cooldown deadlines. It
never serializes a presentation prefab, donor object name, scene path, Animator
state or Unity instance ID.

Logical NPC state exists in the persistent Bootstrap runtime and therefore
restores immediately even when the relevant world cell is unloaded. The
temporary character wrapper is reconciled only after its project-owned cell ID
is available; unload destroys the transient wrapper without changing the DTO,
and reload recreates it from the same state. Presentation replacement keeps the
stable instance ID, binding ID and production replacement key.

Migration `7 -> 8` still adds the accepted 10A domain. Migration `8 -> 9`
preserves every compatible 10A character snapshot, initializes missing 10B-R1
rows from the configured catalog, upgrades schema 1 to schema 2 and initializes
missing dialogue cooldown arrays without modifying the source document. Unknown,
duplicate or stable-ID-incompatible legacy identities fail preflight. The
runtime then performs semantic preflight against the exact configured project
roster before mutation and participates in the existing transactional rollback.

Migration `9 -> 10` repairs the item-gravity compatibility regression. The
original `world.entities` schema gained `useGravity` without a migration, so an
absent JSON member from existing slots deserialized as `false`. Only stable IDs
owned by authoritative `items.instances` are normalized to a dynamic,
gravity-enabled body and `world.entities` advances to schema 2; unrelated
pickup targets keep their saved flags. Migration `10 -> 11` adds the
project-owned helmet color and matte state corresponding to the donor-observed
Paint FSM. Both migrations clone the document and leave the source file intact
until the normal transactional load succeeds.

Historical section status: **Milestone 09B implementation baseline**
Historical format at this point: `msc.native-save`, document version `11`.
The current audited format is version `16` as recorded at the top of this file.

## Граница ответственности

`MSC.Save.Runtime` владеет форматом документа, слотами, безопасной записью,
реестром участников, миграциями, отчётами и событиями операций. Игровые модули
не знают о файлах и не ссылаются на Save assembly: они предоставляют собственные
DTO и методы `Capture / Validate / Restore`. Связка выполняется только в
`MSC.Save.Integration`, которую создаёт production Bootstrap.

Никогда не сериализуются:

- donor hierarchy/name/path или donor instance ID;
- Unity instance ID, сцена целиком или raw `GameObject` graph;
- Enviro/Wwise/vendor runtime objects;
- визуальная иерархия временного donor world baseline.

## Документ и хранилище

Один слот расположен под:

```text
Application.persistentDataPath/
  Saves/Native/<slot-id>/
    current.save.json
    current.save.json.tmp
    current.save.json.bak
    corrupt/
```

`SaveDocument` содержит header, UI metadata и отсортированные domain envelopes.
Каждый envelope имеет project-owned `DomainId`, schema version, required flag и
канонический JSON payload. SHA-256 integrity вычисляется для документа без
записанного hash и проверяется при чтении.

Запись выполняется так:

1. полная capture/validation всех зарегистрированных доменов;
2. запись `.tmp` с write-through и flush;
3. повторное чтение и проверка `.tmp`;
4. atomic replace текущего файла с `.bak`, либо безопасный fallback;
5. повреждённый кандидат переносится в `corrupt/`, а не перезаписывается.

При чтении порядок recovery: valid current → valid interrupted `.tmp` → valid
`.bak`. Исходный повреждённый файл сохраняется в quarantine.

`EnumerateSlots()` выполняет только read-only inspection и не перемещает файлы:
валидный `.tmp` или `.bak` делает слот доступным для загрузки и сообщает, что
recovery произойдёт при Load. Фактический runtime Load автоматически помещает
повреждённый current в quarantine и продвигает проверенный `.tmp` либо `.bak` в
current. Поэтому просмотр меню не изменяет хранилище, а начатая пользователем
загрузка завершает восстановление атомарно.

## Текущие домены через 09B

| DomainId | Schema owner | Restore phase | Фактическое покрытие |
|---|---|---:|---|
| `core.time` | Core Time + Save Integration | 0 | authoritative ticks, calendar projection и time scale; UI pause is session-only and is normalized to active on save/load |
| `weather.environment` | Weather Production + Save Integration | 100 | weather schedule/state, wetness, lightning и quality tier; зависит от `core.time` |
| `items.instances` | Items Runtime + Save Integration | 150 | mutable item state, deterministic child identities, liquid/container membership и deferred state по stable ID |
| `world.entities` | Save Integration + Interaction | 200 | загруженные и deferred mutable `PhysicsPickupTarget` по stable ID; static donor world не является gameplay authority |
| `vehicle.satsuma` | Vehicle Runtime | 300 | текущая bounded Satsuma aggregate boundary: assembly → backend reset/simulation → physics pose/velocity → ignition; production Satsuma ещё не существует |
| `player.state` | Player Runtime | 500 | world pose, motor/crouch/vertical state, camera pitch |
| `interaction.carry` | Interaction Runtime | 600 | carried stable entity и local anchor pose |

UI settings остаются отдельным пользовательским файлом
`Settings/ui-settings.json`. Они не входят в игровой слот и не откатываются при
загрузке игры.

`SaveMetadata.PlayTimeSeconds` складывается из загруженной накопленной базы и
unscaled realtime текущей сессии. После успешной загрузки база и начало отсчёта
устанавливаются заново, поэтому счётчик не обнуляется между сессиями. В 09A он
считается с инициализации Bootstrap, включая главное меню и паузу;
`LocationStableId` пока остаётся пустым до появления отдельного authoritative
location boundary.

## Транзакционная загрузка

Загрузка никогда не применяется поверх уже показанного production мира.
Принятый UI передаёт slot ID через одноразовый process-local handoff и запускает
чистую загрузку Bootstrap scene. Новый composition root потребляет ID ровно один
раз в `Awake`.

Порядок:

1. новый Bootstrap держит persistent vehicle bodies под startup guard;
2. первый streaming refresh материализует static collision начальной ячейки;
3. storage recovery, document migration и pure preflight всех payload;
4. checkpoint каждого участника;
5. `core.time` и `weather.environment` проходят раздельный preflight, затем
   единый environment restore stage до world reveal;
6. логическое состояние item instances и их container membership;
7. mutable world entities применяют позы под collisionless/kinematic guard;
8. vehicle aggregates восстанавливают assembly/simulation, сохраняя streaming
   quarantine для тел без загруженной опоры;
9. player pose/state и carried object восстанавливаются после своих зависимостей;
10. второй refresh материализует ячейку восстановленного player focus,
    retained source cells и static support;
11. guard снимается только после scene registration batch и PhysX sync barrier;
12. при ошибке уже применённые участники откатываются в обратном порядке,
    включая scene/suspension topology world entities;
13. environment завершает staged restore и только после этого включается gameplay.

Одноразовый handoff хранит только slot ID и не является сохранённым gameplay
state или service locator.

## Выгруженные ячейки и missing content

Перед выгрузкой owned cell streaming сообщает Save integration, пока объекты ещё
живы. Mutable item/entity/vehicle state захватывается и сохраняется по паре
`OwnerDomainId + StableEntityId`. Это позволяет нескольким доменам хранить
независимое состояние одного объекта, например physics и carry attachment.

Если stable entity отсутствует при restore:

- состояние не теряется и не привязывается по имени;
- payload помещается в `DeferredStableEntityStore`;
- `UnresolvedContentReport` получает `DeferredUntilCellLoad`;
- `WorldEntityStateDto.sourceCellId` хранит project-owned стабильный ID исходной
  ячейки, а не scene path и не вычисленную текущую позицию; допустимо пустое
  значение для объектов вне cell-сцен, длина ограничена 128 символами, `/` и
  `\` запрещены;
- Save integration удерживает сохранённую исходную ячейку владельцем
  `world.entities:<StableEntityId>`, поэтому она может загрузиться вне обычного
  streaming radius;
- после регистрации объекта состояние применяется к точному stable ID,
  retention снимается, и ячейка снова может штатно выгружаться;
- неизвестный текущему manifest `sourceCellId` не подменяется: payload остаётся
  deferred, а отчёт получает `ParticipantReported`;
- неизвестный required domain блокирует загрузку, неизвестный optional domain
  сохраняется для следующей записи и отражается в отчёте.

Pickup, который был унесён из исходной ячейки или удерживается игроком, перед её
выгрузкой переносится в persistent Bootstrap scene и остаётся зарегистрированным
в `world.entities`. Если конкретная нестандартная иерархия не позволяет безопасно
перенести entity root, исходная ячейка явно удерживается, а текущая выгрузка
отменяется; Unity не уничтожает живой persistent объект вместе со сценой.

Подготовка выгрузки выполняется атомарным двухпроходным планом: сначала все
pickup проверяются без перемещения, удаления из registry или записи deferred
payload; один небезопасный перенос блокирует всю выгрузку. Только после успешного
preflight переносимые объекты rehome-ятся, а обычные объекты переходят в deferred
store. При последующей загрузке исходной ячейки canonical clone с тем же stable ID
удаляется только для явно отмеченного перенесённого authoritative-экземпляра;
любой другой duplicate stable ID по-прежнему считается ошибкой.

Для item instances логическая запись архивируется отдельно от Rigidbody pose.
Динамический дочерний предмет из упаковки не материализуется в незагруженной
исходной ячейке: `items.instances` и `world.entities` удерживают payload до
регистрации ячейки, после чего логическое состояние применяется перед физическим.
При загрузке старого слота динамические экземпляры, отсутствующие в документе,
удаляются, поэтому повторная выдача из упаковки не создаёт duplicate stable ID.

## Миграции

Document migration pipeline построен как явная цепочка
`ISaveDocumentMigration FromVersion -> ToVersion`. Миграция `1 -> 2` добавляет
пустой required-домен `items.instances.native.v1`; старые домены и исходный файл
не переписываются до успешного transactional load.
Новая версия не может молча читать несовместимый payload: required schema без
миграции завершает load понятной ошибкой, исходный файл остаётся неизменным.
Текущая цепочка после NPC v9 дополнительно включает `9 -> 10` для безопасного
возврата gravity свободным item bodies и `10 -> 11` для helmet paint state.

## Обязательный контракт нового Phase 1 домена

Любой последующий milestone обязан в том же commit:

1. назначить стабильный project-owned `DomainId` и владельца schema;
2. определить DTO schema и ограничения размеров/значений;
3. определить stable identity каждой persistent записи;
4. реализовать deterministic capture и pure preflight;
5. указать restore phase и зависимости;
6. определить unloaded-cell policy;
7. определить missing/removed/replacement-content policy;
8. описать migration impact;
9. добавить round-trip и malformed/corruption fixture;
10. обновить `Docs/Save/FULL_GAME_SAVE_COVERAGE.csv`;
11. не помечать feature `Verified` без выполненного gameplay/donor comparison.

## Инструменты

Unity menu:

- `Tools/MSC Remake/Save/Native Save Inspector` — документы, домены, candidates,
  stable/deferred/unresolved IDs;
- `Tools/MSC Remake/Save/Validate All Native Save Slots` — read-only slot audit;
- ручной recovery в Editor Inspector выполняется только явной кнопкой с
  подтверждением; runtime Load использует автоматический проверенный recovery,
  описанный выше.

Coverage валидируется Editor-командой и тестами против authoritative Phase 1
matrix. Генератор: `Tools/Save/GenerateFullGameSaveCoverage.ps1`.

## Текущие ограничения

- 09B не создаёт будущие NPC, needs, economy, jobs, services, progression,
  media behavior или full vehicle roster state;
- точные donor-specific item effects, cooking/spoilage, kilju sequence,
  disposable presentation variants и row-by-row manual comparison остаются
  `Partial`/`Blocked`, а не `Verified`;
- активный production Bootstrap пока не содержит production Satsuma aggregate;
  `vehicle.satsuma` честно сохраняет пустой список, пока 11A не зарегистрирует
  production aggregate; отдельный `vehicles.instances` остаётся Planned для
  полного roster;
- donor save importer остаётся `PendingDecision` до отдельного evidence и решения
  пользователя;
- playtime 09A включает время в меню/паузе, а metadata location пока не
  заполняется;
- `UnresolvedContentReport` принадлежит конкретному load result и не записывается
  как authoritative gameplay state.
