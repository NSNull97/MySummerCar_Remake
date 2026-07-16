# Полная валидация runtime baseline 06B3

Дата: 2026-07-17
Baseline revision: `DonorWorldBaseline-v001`
Active profile: `donor-feature-parity-06b2`
Classification: `TemporaryDirectImport`
Текущий gate: **PASS / FROZEN / HUMAN ACCEPTED**

## Назначение

Этот отчёт фиксирует donor-карту как техническую основу feature-parity
разработки. Он не утверждает, что импортированная геометрия, материалы,
текстуры, vegetation или collision являются финальным production art.

Авторитетные машинные данные:

- `Docs/WorldBaseline/BASELINE_REVISION.json`;
- `Docs/WorldBaseline/FULL_MAP_VALIDATION_RESULT.json`;
- `Docs/WorldBaseline/TRAVERSAL_VALIDATION.csv`;
- `Docs/WorldBaseline/LEGACY_VISUAL_DEBT.csv`.

## Entry gate

| Условие | Результат |
|---|---|
| 06B1 закрыт | PASS |
| 06B2 закрыт и закоммичен | PASS — `79f02b0` |
| Active profile использует sanitized donor baseline | PASS |
| Rejected custom visuals `cell_0_-3` / `cell_0_-2` не входят в active profile | PASS |
| Forbidden donor runtime logic отсутствует | PASS |
| Broad unrelated diff на старте | PASS — отсутствовал |

## Frozen structural result

| Метрика | Значение |
|---|---:|
| Global scenes | 1 |
| Cell scenes | 49 |
| Всего generated scenes | 50 |
| Eligible entities | 3 842 |
| Global entities | 88 |
| Cell-owned entities | 3 754 |
| Renderers | 2 605 |
| Safe static colliders | 32 |
| Gameplay anchors | 15 |

Ownership fingerprint:

`1abf88e8047c2446e4ecdc1bdc894738171fac0f4ac9651be75470e985bbf28b`

Presentation fingerprint:

`e337f9d1b5a0344473bd0f096cdb9e0dbf8da321ecb428ebc17da4f7dd8831fd`

Строгий validator проверяет:

- source revision и source hashes;
- active manifest, Build Settings и Bootstrap wiring;
- наличие global/cell scenes;
- scene stamps, ownership, stable object IDs и replacement keys;
- duplicate gameplay IDs;
- meshes, ordered shared materials и allowlisted collider data;
- отсутствие missing scripts и forbidden donor component families;
- отсутствие runtime-зависимости от `ReferenceOnly`, `DonorGenerated`,
  PlayMaker, donor assemblies и старого Unity runtime;
- отсутствие rejected prototype visual roots в donor profile;
- project-owned gameplay catalog;
- ignored/generated Git boundary;
- повторяемость ownership и presentation planning.

## Runtime lifecycle

Существующие PlayMode проверки подтверждают:

- Bootstrap загружает global scene и home cells;
- global root сохраняет lifetime при смене focus;
- radius-two preload включается при скорости focus выше `12 m/s`;
- owned cell scenes корректно выгружаются;
- внешний unload/reload не ломает ownership;
- replacement state и gameplay catalog переживают cell reload;
- presentation использует shared assets без роста runtime material instances;
- player OOB recovery возвращает игрока в project-owned safe transform.

06B3 добавляет полный двухпроходный load/unload sweep всех 49 cell scenes при
неизменной global scene. Свежий запуск прошёл: `1/1 PASS`, 49 ячеек × 2 цикла,
`2.7379124 s`. Машинный freeze result записан в
`FULL_MAP_VALIDATION_RESULT.json`.

После добавления freeze gate и manual vehicle fixture полный focused regression
прошёл:

- WorldBaseline EditMode: `15/15 PASS`;
- `DonorWorldCellizationPlayModeTests`: `7/7 PASS`;
- strict 50-scene validator: `PASS`.

Устаревший pre-06B2 тест build isolation обновлён до текущего контракта:
canonical source scene исключена из Build Settings, а 50 generated streaming
scenes намеренно остаются активными.

## Traversal

Ручная проверка пользователя уже подтверждает:

- карта визуально является узнаваемой оригинальной MSC-картой;
- обе бывшие custom-области показывают donor baseline;
- home/garage и lake/shore доступны;
- мосты проходимы пешком;
- перенос персонажа между ячейками не выявил failed load, duplicate world или
  заметный seam;
- Teimo-area unload/reload работает;
- падение ниже карты возвращает игрока домой.

Автоматический streaming-focus sweep прошёл четыре удалённые позиции. Худший
измеренный refresh — `404.3974 ms`; vehicle-speed preload refresh —
`95.4574 ms`. Это нагрузочная проверка lifecycle, а не доказательство
физического проезда.

Изолированный development-only harness:

`Tools > MSC Remake > World Baseline > 06B3 > Open Vehicle Traversal Harness`

объединил Bootstrap, active donor profile и M06 prototype, выбрал
allowlisted дорожную поверхность, переносит vehicle на project-owned safe pose,
привязал streaming focus и потребовал четыре последовательные загруженные
ячейки. Пользователь физически проехал Fleetari, Teimo/store,
town/inspection, major road loop, railway crossing и representative bridge.
`F8` экспортировал cell history,
peak frame/streaming-frame time, speed и recovery count в
`PerformanceCaptures/Milestone06B3/M06B3_VehicleTraversalEvidence.json`.
Evidence SHA-256:
`22bc948a87ba0fc99f2c15221103a8b60b4d4026554d8d95c54d286408ca12c4`.

Зафиксировано 24 unique cells, 35 transitions, longest sequence из семи ячеек
и шести границ, 75 streaming refreshes, peak speed `142.777069 km/h`,
streaming peak `300.226898 ms` и один успешный vehicle OOB recovery.
Пользователь подтвердил работающий `Backspace`, отсутствие критических
collision/duplicate/missing-section failures и отсутствие заметного
42.8-секундного зависания. Единожды наблюдался краткий микрофриз; пользователь
связал его с Unity Editor, но причина не считается доказанной.

Подробности и различие между manual walk, relocation, automated focus и
physical vehicle находятся в `TRAVERSAL_VALIDATION.csv`.

## Collision и OOB

Текущий baseline сохраняет узкий allowlist:

- 20 `MeshCollider`;
- 12 `BoxCollider`;
- 0 triggers;
- 0 Rigidbody;
- 0 joints;
- 0 donor behavior components.

Player OOB recovery автоматизирован и подтверждён пользователем. Некоторые
building shells, включая Teimo, пока не имеют полного collision enclosure:
через них можно пройти, но это не создаёт infinite fall или текущий world
softlock. Development harness vehicle reset и below-world recovery фактически
проверены: `Backspace` работает, evidence содержит `recoveryCount = 1`.

Полная граница ответственности описана в
`COLLISION_AND_OOB_SAFETY.md`.

## Visual debt

`LEGACY_VISUAL_DEBT.csv` содержит стабильные IDs `LVD-0001`–`LVD-0016`.
Каталог включает donor voids, tree walls, backdrops, under-map water hacks,
flat proxies, старые материалы/текстуры, неполную collision, missing LODs,
vegetation cost, seams и unsplit global aggregates.

В каталоге нет `GameplayBlocker`. Визуальные дефекты не маскируются как
исправленные и не блокируют 06B3. Physical vehicle traversal принят.

## Private build/content policy

Политика остаётся:

- raw donor extraction и generated payload находятся вне Git;
- baseline имеет только классификацию `TemporaryDirectImport`;
- private local baseline разрешён только в Development build с
  `MSC_PRIVATE_DONOR_BASELINE_BUILD=1`;
- public/distributable build блокируется;
- donor executable, donor runtime assembly и donor installation не нужны для
  запуска remake runtime;
- donor installation не изменяется.

Полный 51-scene private Development Player smoke должен быть записан отдельно;
его отсутствие не превращается в ложный PASS.

## Reproducibility

Freeze опирается на:

- source revision `msc-world-baseline-04a1.1-c3f2f337`;
- source scene SHA-256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- source-files fingerprint
  `72567486f72bd9106eadc0ba87aadb290accefdcee9e6517ffe30c2218d5ceec`;
- cellization `1.1.0-06B2-v5.1`;
- presentation generator `06B2-v5.1.5`;
- baseline-defining commit
  `79f02b04f4c63471d9503850e7f55acf7972c101`.

Любая будущая регенерация обязана следовать
`BASELINE_REGENERATION_POLICY.md` и выпускать semantic diff.

Строгий freeze validator выполняется повторно после любого изменения входных
отчётов. Для неизменных inputs повторный export должен быть байт-в-байт
детерминирован; актуальный hash фиксируется логом финального batch run, а не
включается обратно в один из собственных hashed inputs.

Source manifest сохраняет исторический source-stage marker
`PreparedNotActiveUntil06B2`. Это не active runtime state: generated scene
stamps и профиль `donor-feature-parity-06b2` валидируются как
`ActiveFeatureParityProfile`.

## Repository-wide validator notes

Строгий validator активного donor-профиля проходит. Два более широких
repository-wide validator-а выявляют старый долг вне active baseline:

- Foundation validator: 35 duplicate stable IDs между неактивными comparison,
  prototype, prefab и playtest assets;
- donor pipeline validator: 518 `MissingProvenance` для ignored local
  `ReferenceOnly/World/MeshLibrary` payload.

Эти результаты не скрываются и не помечаются PASS. Они не являются
runtime-зависимостями активного `RuntimeBaseline`, но требуют отдельной
ремедиации вне границ 06B3.

## Gate conclusion

- Structural baseline: **PASS** после свежих и повторных 06B3 batch runs.
- Actual donor map active: **PASS**.
- Rejected custom regions inactive: **PASS**.
- Player/manual world review: **PASS в реально выполненном объёме**.
- Physical vehicle multi-cell traversal: **PASS / HumanAccepted**.
- Vehicle reset и below-world recovery smoke: **PASS / HumanAccepted**.
- Final 06B3 acceptance: **PASS / Frozen**.
- Переход к `07A_ENVIRO3_PREFLIGHT_AND_WEATHERLAB.md`: **GO после отдельного
  запуска milestone; Enviro в 06B3 не устанавливался и не добавлялся**.
