# Milestone 06B3 — runtime baseline validation, debt catalogue, and freeze

Дата: 2026-07-17
Unity: `6000.3.11f1`
Baseline revision: `DonorWorldBaseline-v001`
Active profile: `donor-feature-parity-06b2`
Classification: `TemporaryDirectImport`
Статус: **COMPLETED / FROZEN / HUMAN ACCEPTED**

## Итог

Технический donor-world baseline собран в воспроизводимую revision-запись,
структурная проверка расширена до полного runtime load/unload sweep, visual
legacy debt получил стабильные IDs, а private-build и weather handoff policies
зафиксированы отдельно.

Milestone не выполняет remaster art, не переносит donor gameplay logic и не
начинает Enviro 3.

## Frozen baseline

`DonorWorldBaseline-v001` фиксирует:

- source revision `msc-world-baseline-04a1.1-c3f2f337`;
- source scene SHA-256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- source-files fingerprint
  `72567486f72bd9106eadc0ba87aadb290accefdcee9e6517ffe30c2218d5ceec`;
- sanitation `06B1.4`;
- cellization `1.1.0-06B2-v5.1`;
- presentation generator `06B2-v5.1.5`;
- 1 global + 49 cell scenes;
- 3 842 entities, 2 605 renderers, 32 colliders и 15 gameplay anchors;
- active profile `donor-feature-parity-06b2`;
- ownership/presentation/manifests hashes;
- debt catalogue `legacy-visual-debt-v001`;
- baseline-defining commit `79f02b0`.

## Structural validation

Strict full-map validation охватывает:

- source hashes/revision;
- все registered scenes и scene addresses;
- ownership, stable IDs и replacement keys;
- meshes, materials и allowlisted colliders;
- gameplay catalog;
- forbidden donor logic/dependencies;
- rejected prototype inactivity;
- Bootstrap/Build Settings wiring;
- generated payload Git boundary;
- deterministic planning.

Добавлен двухпроходный runtime load/unload тест всех 49 cells с сохранением
того же global root. Машинный итог экспортируется в
`Docs/WorldBaseline/FULL_MAP_VALIDATION_RESULT.json`.

Старый pre-06B2 EditMode assertion, запрещавший любые `RuntimeBaseline` scenes
в Build Settings, приведён к действующему контракту: canonical source scene
остаётся исключённой, а 1 global + 49 cell streaming scenes должны быть
активны.

## Traversal

Подтверждены фактически выполненные проверки:

- Bootstrap и recognizable donor map;
- home/garage;
- lake/shore;
- Teimo-area streaming relocation;
- bridge walking;
- cross-cell character relocation;
- player OOB recovery;
- automated distant focus sweep;
- automated vehicle-speed radius-two preload.

Development-only manual fixture:

`Tools > MSC Remake > World Baseline > 06B3 > Open Vehicle Traversal Harness`

не изменил Bootstrap, M06 prototype или Build Settings. При ручном запуске
fixture нашёл allowlisted road surface, перенёс vehicle, привязал streaming
focus и настроил reset/below-world recovery. Пользователь проехал Fleetari,
Teimo/store, town/inspection, major road loop, railway crossing и
representative bridge. Клавиша `F8` экспортировала cell history,
peak frame/streaming-frame time, peak speed и recovery count в
`PerformanceCaptures/Milestone06B3/M06B3_VehicleTraversalEvidence.json`.

Evidence SHA-256:
`22bc948a87ba0fc99f2c15221103a8b60b4d4026554d8d95c54d286408ca12c4`.

Фактический результат:

- 24 unique cells и 35 transitions;
- longest sequence: 7 cells / 6 consecutive boundaries;
- 75 streaming refreshes;
- peak streaming frame: `300.226898 ms`;
- peak speed: `142.777069 km/h`;
- vehicle OOB recovery: `recoveryCount = 1`;
- `Backspace` reset: подтверждён;
- critical collision failures: 0 observed;
- visible duplicate/missing sections: 0 observed;
- один краткий микрофриз; пользователь предполагает влияние Unity Editor,
  причина не доказана;
- raw `42787.503906 ms` frame sample признан Editor-contaminated: пользователь
  соответствующего 42.8-секундного зависания не наблюдал.

## Collision и safety

Текущий allowlist содержит 20 static `MeshCollider` и 12 static
`BoxCollider`. Donor triggers, Rigidbody, joints и behavior не импортированы.
Player recovery project-owned и работает. Development harness vehicle safe
pose, reset и below-world recovery прошли bounded manual smoke. Item/NPC
recovery hooks пока `N/A`, потому что соответствующие
production systems не реализованы. Неполная building collision и отсутствие
production vehicle recovery policy записаны как ограничения, а не как скрытый
PASS.

## Legacy debt

`LEGACY_VISUAL_DEBT.csv` содержит 16 стабильных debt records. Они покрывают:

- terrain voids и tree walls;
- map-edge/under-map hacks;
- flat proxy objects;
- low-detail geometry;
- legacy materials/textures и temporary water;
- incomplete/weak collision;
- missing LODs и vegetation cost;
- inherited seams;
- unsplit global aggregates;
- large legacy physics triangles;
- ограниченную будущую wetness coverage.

Ни один visual-debt record не классифицирован как текущий
`GameplayBlocker`.

## Private content policy

Raw extraction и generated RuntimeBaseline остаются вне Git. Baseline может
попасть только в явно подтверждённый private local Development build.
Public/distributable build guard остаётся обязательным. Полный 51-scene private
Player smoke не заявляется выполненным до фактического запуска.

## Weather handoff

Зафиксированы:

- единственный active world profile;
- neutral clear/dry baseline;
- process-lifetime Bootstrap как место будущего environment adapter;
- существующие временные HDRP sky/fog/light owners, которые 07A должен
  отключить или передать Enviro;
- additive scene lifecycle boundary;
- legacy material wetness limitations;
- независимый от WeatherLab маршрут для будущего 07C capture.

Repository preflight выполнен: Enviro package в `Packages/manifest.json`,
Enviro assets, project-owned API references и dedicated integration assembly не
найдены. Компоненты Enviro и weather logic в 06B3 не добавлялись.

## Созданные outputs

- `Docs/WorldBaseline/FULL_MAP_VALIDATION_REPORT.md`;
- `Docs/WorldBaseline/FULL_MAP_VALIDATION_RESULT.json`;
- `Docs/WorldBaseline/TRAVERSAL_VALIDATION.csv`;
- `Docs/WorldBaseline/COLLISION_AND_OOB_SAFETY.md`;
- `Docs/WorldBaseline/LEGACY_VISUAL_DEBT.csv`;
- `Docs/WorldBaseline/BASELINE_REVISION.json`;
- `Docs/WorldBaseline/BASELINE_REGENERATION_POLICY.md`;
- `Docs/WorldBaseline/PRIVATE_BUILD_CONTENT_AUDIT.md`;
- `Docs/WorldBaseline/WEATHER_HANDOFF.md`;
- `Docs/Milestones/MILESTONE_06B3_REPORT.md`.

## Реализация и tests

- `Assets/Game/Editor/WorldBaseline/WorldBaseline06B3Paths.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldBaselineFreezeValidator.cs`;
- `Assets/Game/Development/WorldBaseline/Runtime/`;
- `Assets/Game/Development/WorldBaseline/Editor/`;
- `Assets/Game/Development/WorldBaseline/Scenes/WorldBaselineVehicleTraversalHarness.unity`;
- `Assets/Game/Tests/EditMode/WorldBaseline/DonorWorldCellizationEditModeTests.cs`;
- `Assets/Game/Tests/EditMode/WorldBaseline/DonorWorldBaselineEditModeTests.cs`;
- `Assets/Game/Tests/EditMode/WorldBaseline/WorldBaselineVehicleTraversalHarnessEditModeTests.cs`;
- `Assets/Game/Tests/PlayMode/WorldBaseline/DonorWorldCellizationPlayModeTests.cs`.

## Проверки

| Проверка | Результат |
|---|---|
| C# compilation | PASS — Unity compilation и sequential `dotnet build MSC.Tests.EditMode.csproj`, 0 errors |
| Strict 50-scene validator | PASS — 50 scenes, 3 842 entities, 2 605 renderers, 32 colliders, 15 anchors; `Logs/M06B3_FullMapValidator_Final.log` |
| Full WorldBaseline EditMode regression | 15/15 PASS — `TestResults/M06B3_Accepted_EditMode_Final.xml`, `90.535 s` |
| Full donor cellization PlayMode regression | 7/7 PASS — `TestResults/M06B3_Accepted_PlayMode.xml`, `8.872 s` |
| 06B3 freeze schema/determinism EditMode | 2/2 PASS — `TestResults/M06B3_FreezeSchemas_EditMode.xml`, `TestResults/M06B3_FreezeDeterminism_EditMode.xml` |
| All-cell two-pass load/unload | 1/1 PASS — 49 cells × 2 cycles, `TestResults/M06B3_AllCellsLifecycle_PlayMode.xml` |
| Vehicle traversal harness builder | PASS — six approved road-surface IDs, scene excluded from Build Settings, `Logs/M06B3_VehicleHarness_Build.log` |
| Vehicle traversal harness EditMode contract | 1/1 PASS — `TestResults/M06B3_VehicleHarness_EditMode.xml` |
| Vehicle traversal evidence exporter | PASS — runtime `F8` export, SHA-256 `22bc948a87ba0fc99f2c15221103a8b60b4d4026554d8d95c54d286408ca12c4` |
| Vehicle traversal harness runtime | PASS / HumanAccepted — ready marker, road placement, route, reset и recovery подтверждены |
| Machine-readable freeze export | PASS в accepted-state; финальная повторяемость проверена логами `Logs/M06B3_FreezeValidator_Accepted_Final*.log` |
| Active-profile content/provenance/Git audit | PASS в strict donor-profile validator; JSON/CSV parse и `git diff --check` PASS |
| Repository-wide Foundation validator | FAIL с 35 ранее существовавшими duplicate stable IDs в неактивных comparison/prototype assets; active donor gameplay IDs проходят strict validator |
| Repository-wide donor pipeline validator | FAIL с 518 `MissingProvenance` для ignored local `ReferenceOnly/World/MeshLibrary`; active `RuntimeBaseline` dependency/provenance gate проходит |
| Private 51-scene Development Player | Not run |
| Manual physical vehicle traversal | PASS / HumanAccepted — all required landmarks, 24 cells, 6 consecutive boundaries |

## Ограничения и риски

- Baseline не является production art и не разрешён для публичного
  распространения.
- Полная collision donor-world не перенесена.
- Некоторые building shells пока проходятся насквозь.
- Unsplit global content повышает resident memory.
- Реальный standalone GPU/render-thread baseline ещё не снят.
- Source manifest всё ещё хранит исторический source-stage marker
  `PreparedNotActiveUntil06B2`; runtime scene stamps и active profile уже имеют
  `ActiveFeatureParityProfile`.
- Development harness является development-only fixture, а не production
  vehicle recovery policy.
- Один краткий микрофриз наблюдался в Editor-контексте; standalone GPU/render
  capture остаётся отдельной будущей проверкой.

## Go/no-go

- 06B3 automated structural/freeze implementation: **PASS**.
- Final 06B3 acceptance: **PASS / Frozen / HumanAccepted**.
- `07A_ENVIRO3_PREFLIGHT_AND_WEATHERLAB.md`: **GO как следующий отдельный
  milestone**.

После 06B3 не начинать Enviro автоматически.
