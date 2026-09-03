# Milestone 09A — Full-Game Native Save Foundation

Статус: **Implementation complete / manual production playtest required**  
Дата: 2026-07-21

## Итог

Вертикальный save stub заменён расширяемой native-save основой Phase 1. Формат,
storage/recovery, domain registry, migration boundary, dependency phases,
deferred stable entities, реальный UI и Editor tooling работают независимо от
donor runtime. Milestone 09B не начат.

## Что было проверено перед изменениями

- полностью прочитаны `AGENTS.md` и prompt 09A;
- прочитаны current state/guardrails, authoritative Phase 1 matrix/scope/plan,
  отчёт 08B и существующие документы Save, stable ID, streaming, Player,
  Interaction, Vehicle, Weather и UI;
- подтверждено пользовательское принятие 08B и разрешение 09A;
- сохранён принятый baseline 00–08A; UI не редизайнился;
- pre-existing untracked reflection-probe artifact под
  `Assets/Game/Bootstrap/Bootstrap/` не изменялся и не относится к 09A.

## Реализовано

### Save core

- versioned `SaveDocument`, header, metadata и domain envelopes;
- validated slot IDs и bounded deterministic JSON;
- SHA-256 integrity;
- `.tmp`/flush/re-read/atomic replace, `.bak`, quarantine и recovery;
- read-only slot enumeration показывает валидный `.tmp`/`.bak` без изменения
  файлов, а фактический Load выполняет проверенный automatic recovery;
- participant registry, restore phases/dependencies, full preflight,
  checkpoint/apply/rollback;
- migration pipeline и retained unknown optional domains;
- busy/completed/failed/recovery events;
- unresolved-content report.

### Текущие игровые домены

- `core.time`: authoritative time/calendar/time-scale/pause state;
- `weather.environment`: weather/wetness/lightning/quality state с явной
  зависимостью от `core.time`;
- `player.state`: pose, motor/crouch/vertical state и look pitch;
- `world.entities`: mutable project-owned pickup bodies по stable ID, включая
  deferred состояние и стабильный `sourceCellId`;
- `interaction.carry`: held stable ID и anchor pose;
- `vehicle.satsuma`: текущая bounded assembly/simulation/physics/ignition
  aggregate boundary.

Missing entity state не теряется при unloaded cell: streaming выдаёт явные
load/will-unload события, а Save integration применяет deferred payload только к
совпавшему stable ID. Static donor world geometry/material hierarchy намеренно не
сериализуется как gameplay authority.

Унесённый или удерживаемый pickup перед выгрузкой исходной ячейки переносится в
persistent Bootstrap scene. При clean-load исходная cell временно удерживается
по `sourceCellId`, объект восстанавливается по stable ID, после чего retention
снимается. Небезопасный перенос блокирует конкретную выгрузку вместо уничтожения
живого предмета вместе со сценой.

Выгрузка pickup-состояния выполняется двухпроходно и атомарно: preflight одного
непереносимого live hierarchy отменяет всю операцию без частично изменённого
registry/deferred state. Повторно загруженный canonical clone удаляется только
для явно отмеченного authoritative-предмета, ранее перенесённого из этой же
source cell; остальные duplicate stable ID остаются жёсткой ошибкой.

### Bootstrap и UI

- production Bootstrap создаёт один native save composition root;
- load из Continue/Load проходит через чистый reload Bootstrap и применяется до
  world reveal;
- Continue использует последний valid slot;
- Load показывает реальные slots/metadata и блокирует corrupt slot;
- Save доступен из pause Save Status;
- UI показывает busy/failure/recovery, не вызывает runtime load поверх мира;
- recovery, завершившийся до создания UI, передаётся через
  `LastLoadReadStatus` и воспроизводится как локализованные status/toast;
- startup-load failure, завершившийся до создания UI, показывается тостом в
  главном меню и сохраняется подробным статусом в Save Status;
- UI settings остаются отдельными от игровых slots.

### Coverage и инструменты

- `FULL_GAME_SAVE_COVERAGE.csv`: 533/533 authoritative FeatureId плюс 22 domain
  contracts, без пропусков/дубликатов;
- будущие домены честно отмечены Planned/Uncovered, importer — PendingDecision;
- native save inspector, slot validator, recovery confirmation и
  stable/deferred/unresolved view;
- документирован обязательный registration checklist будущего домена.

## Основные файлы

- `Assets/Game/Save/Runtime/` — core document/storage/registry/recovery;
- `Assets/Game/Save/Migration/` — migration pipeline;
- `Assets/Game/Save/Integration/` — current-domain participants и session load;
- `Assets/Game/Save/Editor/` — inspector/validator;
- `Assets/Game/Player/Runtime/PlayerPersistenceState.cs`;
- `Assets/Game/Vehicle/Runtime/VehiclePersistence.cs`;
- расширения Interaction, Weather, World Streaming и Bootstrap;
- real-save hooks в `Assets/Game/UI/Presentation/Runtime/`;
- тесты SaveCore/SaveIntegration/SaveCoverage/SaveEditor и существующих доменов;
- `Docs/Save/FULL_GAME_SAVE_COVERAGE.csv`;
- `Docs/Save/NATIVE_SAVE_ARCHITECTURE.md`.

## Выполненные проверки

- Unity compile: без C# errors/warnings в финальных целевых прогонах;
- Save core: **25/25 Passed**;
- Editor save tools: **5/5 Passed**;
- current-domain Save Integration: **9/9 Passed**;
- Save coverage validator: **5/5 Passed**;
- UI Presentation PlayMode: **16/16 Passed**;
- UI Runtime/EditMode: **13/13 Passed**;
- Player/Interaction EditMode: **11/11 Passed**;
- Vehicle Simulation EditMode: **29/29 Passed**;
- Weather Production EditMode: **38/38 Passed**;
- Foundation EditMode: **8/8 Passed**;
- Production World Streaming EditMode: **4/4 Passed**;
- coverage generator: **533 parity rows + 22 domain-contract rows**, повторный
  запуск сохранил тот же SHA-256;
- `git diff --check`: без whitespace errors.

Полный существующий EditMode suite: **201/213 Passed, 12 Failed**. Все 12 падений
относятся к уже существующему дрейфу принятых world/material данных относительно
старых frozen fixtures: GaragePrototype lighting (2), donor baseline/cellization/
material policy (8), WorldValidation PilotGate (1), WorldTransfer source hashes
(1). Эти не связанные с 09A проверки не исправлялись внутри bounded milestone;
соответствующие изменённые границы отдельно прошли зелёные целевые наборы выше.
Ручной production playtest не подменяется автотестами.

## Ручная проверка в Unity

1. Открыть `Assets/Game/Bootstrap/Bootstrap.unity`, запустить Play Mode.
2. Начать новую игру, переместиться, изменить время/погоду, взять доступный
   project-owned pickup (если он присутствует в активной ячейке).
3. `Esc` → Save Status → сохранить `slot-01`; убедиться в сообщении об успехе.
4. Изменить позицию/состояние, вернуться в меню или вызвать Load из Save Status.
5. Убедиться, что показан loading frame, Bootstrap перезапустился, main menu не
   появился поверх загруженной сессии, player/time/weather восстановились.
6. Проверить Continue из нового запуска.
7. Открыть `Tools/MSC Remake/Save/Native Save Inspector`, проверить valid current,
   domains и отсутствие неожиданных unresolved IDs.
8. Унести pickup за пределы его исходной ячейки, выгрузить её, сохранить и
   загрузить игру; отдельно повторить для предмета в руках. Предмет не должен
   исчезнуть или продублироваться.
9. После restore вернуться от исходной ячейки и убедиться, что снятый retention
   снова позволяет ей выгрузиться.
10. На тестовой копии слота отдельно проверить recovery из валидных `.tmp` и
    `.bak`: повреждённый current должен оказаться в quarantine, а после запуска
    UI должен показать уведомление о восстановлении.
11. Сохраниться, провести в новой сессии некоторое время и сохраниться снова:
    `PlayTimeSeconds` должен продолжить расти, а не начаться с нуля.

Локальный слот находится в
`%USERPROFILE%/AppData/LocalLow/DefaultCompany/MySummerCar_Remake/Saves/Native/`.

## Ограничения и риски

- Production Bootstrap пока не содержит production Satsuma; пустой
  `vehicle.satsuma` domain является честным состоянием, а не заявлением
  готовности полного авто; `vehicles.instances` остаётся Planned для 11A;
- full items/consumables/containers появятся в 09B и обязаны зарегистрировать
  свои DTO/participants/coverage в том же commit;
- будущие persistent domains перечислены явно и не придуманы заранее:
  `items.instances`, `needs.player`, `home.state`, `npc.state`,
  `vehicles.instances`, `traffic.state`, `economy.player`, `services.state`,
  `jobs.state`, `communications.state`, `progression.story`,
  `authority.state`, `rally.state`, `media.state` — Planned/Uncovered до своих
  owning milestones; `presentation.runtime` остаётся осознанно NotRequired;
- donor save importer не реализован и остаётся PendingDecision;
- автотест покрывает synthetic carried-object transfer, но production PlayMode
  сценарий moved-not-held + retention/load/release остаётся ручной проверкой;
- отдельного автоматического теста накопления playtime пока нет; в 09A счётчик
  также включает меню/паузу, а `LocationStableId` ещё пуст;
- manual PlayMode round-trip production world ещё должен подтвердить пользователь.

## Совместимость

Публичные/serialized контракты 00–08A не переименовывались и не удалялись.
Player, Interaction, Vehicle, Weather и Streaming получили bounded capture,
validate/restore или lifecycle extensions. Принятый 08A UI сохранил визуальную
структуру и получил только реальные данные/действия Save. Миграция существующих
игровых slots не требуется: до 09A native format отсутствовал.

## Следующий milestone

Ровно один рекомендуемый следующий milestone после ручного принятия 09A:
**09B — World Items, Consumables and Containers**.
