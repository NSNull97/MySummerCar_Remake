# Production environment save/restore contract

Дата среза: 2026-07-18.

Статус: `SESSION_DTO_AND_PRE_REVEAL_RESTORE_PASS / FILE_STORAGE_DEFERRED_TO_M09`.

## Scope

07C предоставляет versioned session-level DTO и atomic runtime restore. Он не
создаёт файл на диске и не расширяет минимальный `ISaveService` storage backend.
Файловое хранение, slots, crash safety, migrations и original-save import
остаются Milestone 09.

Основные типы:

- `ProductionEnvironmentSaveDto`;
- `ProductionEnvironmentPersistence`;
- `GameTimeSaveDto`;
- `WeatherDomainSaveDto` с weather/front/RNG/overrides, wetness и lightning;
- `ProductionEnvironmentController.StageRestore(...)`.

## Envelope

`ProductionEnvironmentSaveDto` schema `1` / config
`environment.production.save.v1` содержит:

- project GameTime DTO;
- composite WeatherDomain DTO;
- project-owned quality enum value.

Enviro references, vendor asset indices, runtime objects/clones, Unity instance
IDs, scene hierarchy paths, VFX frames и diagnostics не сериализуются.

## Validation before mutation

Restore сначала валидирует полный envelope:

1. schema/config ID и наличие вложенных DTO;
2. quality enum;
3. GameTime state против текущей config;
4. weather/front/profile IDs, RNG version/state, overrides;
5. wetness finite/range/config values;
6. lightning cooldown/fairness/candidates/restore fields.

Invalid candidate отклоняется до изменения live state. Это включает unknown
quality/profile/config, duplicate stable IDs, NaN/Infinity и out-of-range data.

## Atomic commit и rollback

После полной проверки implementation:

1. сохраняет checkpoint composite weather state;
2. атомарно восстанавливает weather, wetness и lightning;
3. восстанавливает GameTime;
4. при исключении GameTime откатывает composite weather checkpoint;
5. возвращает validated project quality tier.

Отдельные domain restore operations также валидируются до commit. Presentation
никогда не является источником restore state.

## Startup ordering без clear-sky flash

DTO можно staged только для primary owner до `IsWorldRevealReady` и до запуска
simulation. Попытка restore после reveal отклоняется.

Production startup sequence:

1. composition root wins ownership and activates the linked inactive backend;
2. installer waits backend readiness;
3. create authoritative domains;
4. validate and apply staged DTO;
5. reapply project lightning spawn/load grace;
6. apply restored wetness globals/material bridge;
7. queue one sky/ambient/reflection refresh request;
8. attach adapter;
9. issue one coherent instant presentation frame;
10. mark world reveal ready;
11. stream global/focus cells;
12. activate player and simulation.

Так adapter не должен показывать default clear state между restore и reveal.
Фактическое отсутствие пиксельного flash требует manual/load capture и пока
имеет статус `PENDING`.

## Lightning safety

Restore сохраняет cooldown/fairness state и заново применяет project-owned
restore grace. Автоматический тест проверяет, что
`IsGameplayStrikeAllowed=false` сразу после restore. Enviro visual lightning не
может обойти этот gate.

## Quality restore

Quality сохраняется как project enum, не как ссылка на Enviro asset. После
restore `Low|Medium|High` разрешается через stable project mapping; unknown
numeric value fail-closed. Pre-07C serialized contract Low=`0`, High=`1`
сохранён; Medium=`2`.

## Evidence

Production persistence tests подтверждают:

- capture/restore всех authoritative domains и Medium;
- rejection invalid weather payload до time mutation;
- rejection unsupported envelope/quality;
- post-restore lightning grace.

`Logs/M07C_VisualRemediation2_WeatherProduction.xml` прошёл `32/32` в
combined WeatherProduction/ProductionIntegration/legacy-wetness suite, включая
три production persistence tests. Lifecycle artifact
`Logs/M07C_VisualRemediation2_ProductionPlayMode.xml` прошёл `6/6`, включая
restore-before-reveal и сохранение owner через additive lifecycle. Full suite
`Logs/M07C_VisualRemediation2_FullEditMode.xml` имеет `330/334`: все 07C cases
PASS, а четыре historical Garage/World failures не изменились.

Matched pixel capture отсутствия clear-sky flash всё ещё `PENDING`; automated
state-ordering PASS не подменяет визуальный capture.

## Ограничения до Milestone 09

- нет save slot/file path;
- нет atomic temp-file replace/fsync policy;
- нет storage encryption/compression;
- нет migration registry для production envelope;
- нет UI ошибок/slot management;
- нет recovery повреждённого файла;
- нет интеграции с текущим минимальным `ISaveService`;
- нет standalone load-game visual capture.

Следовательно, корректный статус — session restore contract `PASS`, file save
system `NOT IMPLEMENTED`.
