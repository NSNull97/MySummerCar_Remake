# MILESTONE 04B — Reference Capture and Measurement Database

Дата: 2026-07-14
Статус: **завершён как reference-data baseline; assembly-specific Milestone 05 gate — GO**
Dataset: schema `1`, dataset `04B.4`
Unity: `6000.3.11f1`

## 1. Проверенные источники

Полностью прочитаны `AGENTS.md`, `Prompts/04B_REFERENCE_CAPTURE_AND_MEASUREMENTS.md`, `Prompts/CURRENT_STATE_AFTER_04.md`, milestone reports 00–04A1, donor audit/system map/porting matrix/ledger, world-transfer coordinate/scale/completeness/fidelity/fixture documents, а также существующие player, interaction, vehicle, save, audio, weather, architecture и testing documents.

Read-only проверены:

- donor `mysummercar_Data/level2`, SHA-256 `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31`;
- `sharedassets3.assets`, SHA-256 `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684`;
- `sharedassets1.assets`, SHA-256 `8f0a0984f4e55229ecaebb57ef931b052f56998b9569a32e013780aa9dc78e02`;
- `sharedassets3.resource`, SHA-256 `19797fa0386c74d091b530b874a03325191e002c921767240c71ba7791a5a20b`;
- external AssetRipper `GAME.unity`, SHA-256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- 04A/04A1 `WorldObjectPlacements.csv`, `WorldMeshManifest.csv`, landmark fixtures и layout pilot;
- controlled-proof metadata для garage roof и rear brake drum.

Baseline: Steam build ID `20171487`, donor Unity `5.0.0f4`. Локальная установка модифицирована и не объявляется clean stock. Donor installation и donor saves не изменялись.

## 2. Доступные методы capture

Schema поддерживает все требуемые методы:

- `SerializedDonorData`;
- `DecompiledConstant`;
- `DecompiledFormula`;
- `SceneTransform`;
- `AssetMetadata`;
- `ManualMeasurement`;
- `VideoTiming`;
- `ScreenshotMeasurement`;
- `RuntimeObservation`;
- `DerivedCalculation`;
- `Approximation`;
- `Unknown`.

Baseline использовал безопасную статическую инспекцию (`SerializedDonorData`, `SceneTransform`, `AssetMetadata`) и прозрачные `DerivedCalculation`. Supplements `04B.3/04B.4` добавили два external runtime video review и одно текстовое user runtime attestation. Raw media осталось вне Git; screenshot/audio measurement не выполнялись и не заявляются выполненными.

## 3. Созданная database/schema

Созданы `ReferenceCaptureDatabase`, `ReferenceRecord`, `MeasurementRecord`, `BehaviorFixture`, source/evidence records, units, categories, confidence, methods, tolerance, dataset version, stable-ID utility, unit conversion, deterministic JSON, schema migration и validator.

Основная база содержит:

- 9 source records;
- 21 evidence records;
- 40 measurement records;
- 11 behavior fixture records;
- 35 coverage requirements;
- итого 51 queryable reference record.

Каждая измеренная запись содержит стабильный 32-hex project ID, category/subcategory, priority, typed value, unit, coordinate space, source/locator, method/date, confidence, tolerance, raw observation, normalized value, notes, evidence, dependencies и validation status.

Measured donor data находится в `ReferenceCaptureDatabase.json`. Четыре осознанных M4 tuning comparison находятся только в отдельном `ReferenceTuningOverrides.json`; поле `tunedValue` отсутствует в measured database.

## 4. Покрытие P0/P1

| Priority | Всего | Covered | Partial | Missing |
|---|---:|---:|---:|---:|
| P0 | 29 | 9 | 2 | 18 |
| P1 | 6 | 0 | 1 | 5 |

Validator выводит capture queue как `20` non-covered P0 (`Partial + Missing`) и `6` non-covered P1. Это не structural validation errors: значения не выдумываются для искусственного закрытия gate.

Подтверждённые P0 включают world scale/garage anchor/roof, standing camera anchor/base walk speed, body envelope/wheelbase/tracks, representative drum geometry/pivot/installed transform, mount rule и fastener semantics. P1 static records для controller/acceleration/gravity и representative landmarks существуют, но часть P1 остаётся `NeedsReview`/`Partial`.

## 5. Критически отсутствующие измерения

Главные blockers:

- representative compatible mount, install/detach prerequisites и state transitions;
- fastener count, tool size, direction, stages/turns и completion rule;
- assembled/curb mass state и доказанная fitted wheel/tire identity/radius/width;
- garage door clearance и interior usable spans;
- sprint, crouch, interaction/pickup reach, carry/place/drop/throw behavior;
- idle/start/stall, gear ratios/final drive, steering/suspension, acceleration/braking;
- time progression, natural weather transitions и wetness lag;
- engine/environment audio state map;
- UI/needs thresholds and transitions.

`389 kg` — только root Rigidbody mass. `datsun_body` AABB — только body mesh. `tire_stock` — кандидат по имени. `BoltPM` — marker. Ни одно из этих наблюдений не повышено до более сильного утверждения.

## 6. Созданные fixtures

Созданы 11 machine-readable calibration fixtures:

1. `player_movement_interaction.json`;
2. `garage_dimensions_clearance.json`;
3. `vehicle_body_wheel_geometry.json`;
4. `representative_part_mount_pivot.json`;
5. `engine_idle_start_stall.json`;
6. `gearbox_final_drive.json`;
7. `steering_suspension.json`;
8. `braking_acceleration.json`;
9. `time_progression.json`;
10. `weather_transition.json`;
11. `audio_state_mapping.json`.

Один fixture имеет статус `Ready`, три — `Partial`, семь — `Missing`. Пустые expected measurements не заменены fabricated numbers; вместо них заданы inputs, expected observations, trial counts/tolerance rules и missing requirement IDs.

## 7. Созданные Editor tools

Меню: `Tools > MSC Remake > Reference Capture`.

Dashboard умеет:

- показывать dataset/source/evidence/coverage state;
- фильтровать records по category, priority и validation status;
- просматривать raw/normalized values и provenance;
- импортировать structured measurements;
- добавлять manual numeric observation с external evidence path;
- конвертировать units;
- сравнивать measured и separately tuned values;
- валидировать units, IDs, evidence, confidence и dependencies;
- показывать Missing P0/P1;
- экспортировать calibration fixture;
- генерировать capture checklist;
- открывать доступный project/staging/reference evidence через ignored local path configuration.

Batch entry point: `MSC.Editor.ReferenceCapture.ReferenceCaptureValidationRunner.RunBatch`. Runtime assemblies не получают Editor dependency и не открывают donor files.

## 8. Tests и результаты

Добавлено 13 EditMode tests по всем требуемым категориям: validation/serialization, schema migration, stable IDs, duplicate detection, unit conversion, coordinate space, source/evidence provenance, confidence, derived dependencies, measured/tuned separation, Missing P0/P1 reporting и fixture loading.

Реально выполнено:

- JSON parse и CSV width/coverage consistency: passed;
- повторная SHA-256 проверка 5 source artifacts: passed;
- `git diff --check`: passed;
- первый Unity compile/validator run: failed из-за недоступного в установленном NUnit API `Assert.Multiple`; тесты исправлены на совместимые последовательные assertions;
- повторный и финальный M04B batch validator: passed, `records=42`, `missingP0=22`, `missingP1=6`;
- полный EditMode regression: **80 total, 80 passed, 0 failed, 0 skipped**;
- полный PlayMode regression: **9 total, 9 passed, 0 failed, 0 skipped**.

Финальная проверка supplement `04B.4`:

- JSON/version/evidence-hash/CSV checks: passed;
- M04B batch validator: passed, `records=51`, `missingP0=20`, `missingP1=6`;
- ReferenceCapture EditMode: **14/14 passed**;
- PlayMode: **9/9 passed**;
- full EditMode: **80/81 passed**; единственный failure — существующий frozen 04A1 donor hash drift после переустановки (`sharedassets3.assets` и `.resource`), не 04B.4 regression;
- `git diff --check`: passed с informational CRLF/LF warning для ledger.

Ignored outputs:

- `Logs/Milestone04B_Validator_Final.log`;
- `Logs/Milestone04B_EditMode.log`;
- `Logs/Milestone04B_PlayMode.log`;
- `TestResults/Milestone04B_EditMode.xml`;
- `TestResults/Milestone04B_PlayMode.xml`.
- `Logs/Milestone04B4_Validator.log`;
- `Logs/Milestone04B4_ReferenceCapture_EditMode.log`;
- `Logs/Milestone04B4_EditMode.log`;
- `Logs/Milestone04B4_PlayMode.log`;
- `TestResults/Milestone04B4_ReferenceCapture_EditMode.xml`;
- `TestResults/Milestone04B4_EditMode.xml`;
- `TestResults/Milestone04B4_PlayMode.xml`.

Unity показывает существующие warnings о пустых future asmdef modules; новых compile warnings/errors от 04B после исправления нет.

## 9. Необходимый manual capture

Созданы общий `MANUAL_CAPTURE_GUIDE.md` и checklists для player/interaction, vehicle assembly, vehicle behavior, world landmarks, time/weather, audio и UI state.

Следующие сессии должны:

- зафиксировать build/settings/save-state prerequisites и не редактировать donor save;
- использовать заранее определённые start/end gates, scale markers и coordinate spaces;
- хранить raw video/screenshots/audio вне Git;
- сохранять SHA-256/logical evidence path и session ID;
- выполнять 5 trials для коротких событий, 7 для acceleration/braking и 3 long observations для time/weather;
- публиковать median/range и uncertainty;
- оставлять ненадёжное значение `Missing`/`Blocked`, а не guessed numeric value.

Первая обязательная сессия representative rear drum mount/install/detach и fastener semantics выполнена в dataset `04B.4`: три video trials плюс user-confirmed wheel-installed blocked case. Остальные manual sessions остаются очередью для соответствующих будущих milestones.

## 10. Созданные и изменённые файлы

Создано:

- `Assets/Game/Core/Runtime/ReferenceCapture/` — 5 runtime source files;
- `Assets/Game/Core/Configuration/ReferenceCapture/` — database, separate tuning, import template и 11 fixtures;
- `Assets/Game/Editor/ReferenceCapture/` — 4 Editor source files;
- `Assets/Game/Tests/EditMode/ReferenceCapture/ReferenceCaptureDatabaseTests.cs`;
- `Docs/ReferenceCapture/` — format, index, source map, guide, 7 checklists, missing queue и session log;
- Unity `.meta` files для новых `Assets`;
- этот отчёт.

Обновлено:

- `Docs/ARCHITECTURE.md`;
- `Docs/Architecture/CURRENT_REPOSITORY_STATE.md`;
- `Docs/ROADMAP.md`;
- `Docs/TESTING_AND_VALIDATION.md`;
- `Docs/Porting/DONOR_AUDIT.md`;
- `Docs/Porting/PORTING_MATRIX.md`;
- `Docs/Porting/SYSTEM_MAP.md`;
- `Docs/Porting/PORTING_LEDGER.csv`.

Gameplay code, scenes, prefabs, Player/Interaction architecture, vehicle simulation, production world, weather, audio, UI и save runtime не изменялись.

## 11. Риски и assumptions

- Донорная установка модифицирована; static values привязаны к exact hashes и не универсализируются как clean-stock truth.
- AssetRipper hierarchy — вспомогательное static representation; runtime FSM behavior по нему не утверждается.
- Static component values могут быть изменены runtime state machines; поэтому sprint/crouch/engine/assembly behavior требуют наблюдения.
- Source hash фиксирует container, но часть external normalized evidence пока не имеет отдельного artifact hash в каждой evidence row.
- Manual capture зависит от воспроизводимого legal save state и уже установленного recorder; новые external tools не устанавливались.
- Database schema `1` и dataset `04B.4` используют migration `0 → 1`; будущие несовместимые изменения потребуют отдельной пошаговой migration fixture.
- Blocked removal подтверждён user attestation без frame-addressable media и поэтому имеет `Medium` confidence.
- Fitted wheel identity и assembled/curb mass остаются `Partial`; их нельзя выдавать за production calibration.

## 12. Точная готовность к `05_VEHICLE_ASSEMBLY.md`

Решение: **GO для `Prompts/05_VEHICLE_ASSEMBLY.md` в заявленных границах milestone**.

Архитектурный и data foundation готов: stable IDs, units, provenance, pivots/transforms, fixtures, validation и отдельные tuning values доступны без donor runtime dependency. Dataset `04B.4` переводит оба assembly exit prerequisite в `Covered`: static graph и три runtime repetitions подтверждают install/remove и snap, user attestation подтверждает wheel-installed blocker, а fastener contract фиксирует один marker, ключ `14`, scroll direction и discrete stages `0..8`. Отдельного donor angular compare нет; физический torque не заявляется.

Fitted wheel identity и assembled/curb mass остаются `Partial`, поэтому они должны оставаться explicit unknown/project tuning там, где не нужны для assembly graph. Это не блокирует bounded architecture/prototype scope Milestone 05, но блокирует заявления о production vehicle calibration.

Рекомендуемый следующий milestone — **Milestone 05 Vehicle Assembly**, строго по `Prompts/05_VEHICLE_ASSEMBLY.md`.

Milestone 05 не начат.
