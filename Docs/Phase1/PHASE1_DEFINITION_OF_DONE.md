# Phase 1 Definition of Done — Legacy Feature Complete

Статус: **Authoritative / 08B user-approved**.

Текущий audit result (2026-09-02): **NOT MET**. Из 517 обязательных parity
rows только 4 имеют `Verified`, одна имеет `KnownDifferenceApproved`, а 512
остаются `ImplementedUnverified`, `PartiallyImplemented`, `EvidenceCaptured`
или `Specified`. Актуальный разбор находится в
`Docs/Reviews/FULL_GAME_PARITY_AUDIT_2026-09-02.md`.

Поле `Critical` не входит в обязательную схему parity matrix и не используется
как отдельный gate. Критичность выражается `Required=Yes` и зависимостями полного
gameplay flow.

Переход к Phase 2 запрещён, пока не выполнены все обязательные условия.

## Scope

- точная donor-версия зафиксирована хешами и evidence;
- `LEGACY_FEATURE_PARITY_MATRIX.csv` не содержит необработанных `Unknown` rows;
- все `Required=Yes` rows имеют статус `Verified` или
  `KnownDifferenceApproved`;
- каждый `Blocked` row имеет доказанный внешний blocker и отдельное решение
  пользователя; critical blocker не допускает Phase 2 gate.

## Full game content

- полный подтверждённый список NPC существует в runtime;
- полный подтверждённый список транспорта существует и выполняет свою роль;
- все подтверждённые stores/services/jobs/events/story chains доступны;
- все оригинальные core mechanics работают;
- ни один gameplay-critical экран не питается fake/demo data;
- нет обязательного greybox вместо доступного donor presentation asset.

## Runtime ownership

- новый проект не зависит от donor executable или runtime assemblies;
- donor scripts/FSMs/old Unity components отсутствуют;
- gameplay использует project-owned stable IDs, DTOs, services and state machines;
- temporary donor presentation можно удалить/заменить через replacement keys,
  не ломая gameplay или saves.

## Save/load

- fresh game, mid-game и late-game states сохраняются и восстанавливаются;
- NPC, транспорт, предметы, jobs, events, relationships, economy, world cells,
  weather и player state покрыты;
- сохранение не требует загрузки всей карты;
- есть backup, corruption reporting и recovery policy;
- нет критической потери прогресса в regression matrix.

## Playability

- игра стартует из Main Menu;
- New Game и Load работают;
- все основные donor progression paths можно пройти;
- открытый игровой цикл после progression продолжает работать;
- нет crashes, data loss, progression blockers и permanent softlocks;
- все ключевые места доступны;
- полный private Windows x64 build запускается без donor installation/runtime.

## Presentation baseline

- нет missing meshes, magenta materials, invisible required NPC/vehicles/items;
- основные действия имеют donor-compatible temporary visual/audio feedback;
- temporary donor content классифицировано `TemporaryDirectImport`;
- build и reports честно называют его Legacy baseline, а не production art.

## Validation

- full-map traversal completed;
- representative complete-game playthrough matrix completed;
- feature-domain tests and smoke tests passed or documented honestly;
- performance measured on representative late-game save;
- no regression in the accepted 00–08A baseline contracts;
- all roster references resolve to authoritative matrix rows;
- donor installation, executable and runtime assemblies are absent from the
  running private build dependency graph;
- user explicitly approves Phase 1 gate.

## Evidence gate

- every required row names executed acceptance evidence, not only planned tests;
- persistent rows have fresh, mid-game and late-game save/load round trips or an
  evidence-backed non-persistent justification;
- unloaded-cell and presentation-replacement persistence is covered;
- no unresolved required `Unknown`, `Blocked`, `PartiallyImplemented` or
  `ImplementedUnverified` remains;
- every approved difference records the user decision and date;
- `TemporaryDirectImport` presentation is replaceable without changing stable
  IDs, gameplay state or requiring a new playthrough.
