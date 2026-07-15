# MILESTONE 04B.1 — bounded vehicle-assembly static capture

Дата: 2026-07-14
Статус: **static capture завершён; runtime supplement `04B.4` открыл assembly-specific Milestone 05 gate**
Dataset: schema `1`, baseline `04B.2`; corrective supplements `04B.3/04B.4`
Unity: `6000.3.11f1`

## 1. Выполненная граница

После preflight `Prompts/05_VEHICLE_ASSEMBLY.md` выполнена только разрешённая bounded 04B capture-сессия по representative rear-left brake drum. Gameplay/runtime vehicle assembly, сцены, prefabs, production assets, Player и Interaction не менялись. К 05 не переходили.

Read-only изучены существующие внешние данные:

- `AGENTS.md`, `Prompts/04B_REFERENCE_CAPTURE_AND_MEASUREMENTS.md`, `Prompts/05_VEHICLE_ASSEMBLY.md` и текущие reference-capture документы;
- внешний AssetRipper `GAME.unity`, SHA-256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- `WorldObjectPlacements.csv` и соответствующие serialized GameObject/Transform/Collider/FSM records;
- `drum brake.prefab.meta` только для связи spawn GUID с именем prefab.

Donor executable не запускался. Donor installation и saves не открывались для записи и не изменялись. Новая extraction не создавалась; donor payload в Git не добавлялся.

## 2. Подтверждённые данные

### Mount/install/remove

- Installed drum: GO `4017`, T `40078`, local position `(0,0,0)` относительно `TireRL`.
- Candidate trigger: GO `30683`, T `66735`, local position `(-0.1,0,0)`, SphereCollider `100240`, radius `0.01 m`.
- Candidate identity: collision tag `PART`, child `drum brake(Clone)`.
- Install prerequisites: `Trailarm_RL.Data.Installed=true` и `Trailarm_RL.Data.Bolted=true`; `Drumbrake_RL.Data.Installed` должен быть false.
- Install completion: `Drumbrake_RL.Data.Installed=true`, installed GO активируется, loose candidate уничтожается.
- Remove prerequisites: `TriggerWheelRL_New` active и `Drumbrake_RL.Data.Bolted=false`.
- Remove completion: `Installed/Bolted=false`, loose `drum brake` prefab GUID `7ea6e4a1b669a4343807ab188c22efb5` создаётся у trigger, trigger активируется, installed owner деактивируется.

### Fastener

- Под drum GO найден один `BoltPM` control marker GO `12701` и один visual child `bolt0` GO `19555`.
- `Stage` ограничен `0..8`; tighten/untighten изменяют его и `BoltCheck.Tightness` на `+1/-1`.
- `BoltCheck` пишет `Data.Bolted=true` при `Tightness >= 8`, `false` при `Tightness <= 0` и переключает interaction collider.
- Первичный разбор заметил `Screw.BoltSize=0`, но supplement `04B.3` установил, что это unused/default local value, не номер ключа. Реальный tool check сопоставляет `BoltPM.localScale.x=1.4` с ключом `14`.

`8` записано как discrete completion stage, не как torque и не как число физических оборотов.

## 3. Изменения

Dataset повышен `04B.1 -> 04B.2` без изменения schema. Добавлены:

- 1 project evidence record для reviewed static trace;
- 5 measurements: trigger position/radius, install/remove FSM contract, marker count, fastener stage contract;
- обновлённая representative fixture;
- обновлённые coverage и missing-data records;
- regression test, который требует `Partial` для обоих M05 gates.

Corrective supplement `04B.3` добавил external diagnostic video evidence, отдельные wrench-11 rejection и tool/input records, а также исправил прежнюю трактовку `BoltSize=0`. Это не изменило `Partial`/NO-GO решение.

Runtime supplement `04B.4` добавил external video с тремя clean trials, отдельное user attestation для wheel-installed blocked removal и обновил оба assembly requirements до `Covered`.

Итог базы после supplements: 9 sources, 21 evidence record, 40 measurements, 11 behavior fixtures, 35 requirements; всего 51 queryable measurement/fixture record.

Coverage change:

- `P0-ASSEMBLY-MOUNT-RULE`: `Missing -> Partial`;
- `P0-ASSEMBLY-FASTENER-SEMANTICS`: `Partial` после static capture;
- после runtime supplement `04B.4` оба requirement: `Covered`, fixture: `Ready`.

## 4. Созданные и изменённые файлы

Создано:

- `Docs/ReferenceCapture/Sessions/04B_VEHICLE_ASSEMBLY_STATIC_TRACE_20260714.md`;
- `Docs/Milestones/MILESTONE_04B1_VEHICLE_ASSEMBLY_STATIC_CAPTURE_REPORT.md`.

Изменены:

- `Assets/Game/Core/Runtime/ReferenceCapture/ReferenceCaptureDatabase.cs`;
- `Assets/Game/Core/Configuration/ReferenceCapture/ReferenceCaptureDatabase.json`;
- `ReferenceCaptureImportTemplate.json`, `ReferenceTuningOverrides.json` и 11 fixture JSON files — только dataset version; representative drum fixture также получил новые records/ожидания;
- `Assets/Game/Tests/EditMode/ReferenceCapture/ReferenceCaptureDatabaseTests.cs`;
- `Docs/ReferenceCapture/REFERENCE_DATA_FORMAT.md`, `REFERENCE_DATABASE_INDEX.csv`, `MISSING_REFERENCE_DATA.csv`, `CAPTURE_SESSION_LOG.csv`, `VEHICLE_ASSEMBLY_CAPTURE.md`;
- `Docs/ARCHITECTURE.md`, `Docs/Architecture/CURRENT_REPOSITORY_STATE.md`, `Docs/ROADMAP.md`, `Docs/TESTING_AND_VALIDATION.md`;
- `Docs/Porting/DONOR_AUDIT.md`, `PORTING_LEDGER.csv`, `PORTING_MATRIX.md`, `SYSTEM_MAP.md`.

## 5. Проверки

Фактически выполнено:

- повторный SHA-256 внешнего `GAME.unity`: совпал с recorded hash;
- JSON parse, version consistency, stable-record presence и CSV consistency: passed;
- `MSC.Editor.ReferenceCapture.ReferenceCaptureValidationRunner.RunBatch`: passed — `records=47`, `missingP0=22`, `missingP1=6`;
- первый validator обнаружил 5 `UNUSED_DEPENDENCY` warnings у non-derived records; поля удалены, финальный validator прошёл без этих warnings;
- full Unity EditMode: **81/81 passed**, 0 failed, 0 skipped;
- full Unity PlayMode: **9/9 passed**, 0 failed, 0 skipped;
- `git diff --check`: passed.

Ignored outputs:

- `Logs/Milestone04B2_Validator_Final.log`;
- `Logs/Milestone04B2_EditMode.log`;
- `Logs/Milestone04B2_PlayMode.log`;
- `TestResults/Milestone04B2_EditMode.xml`;
- `TestResults/Milestone04B2_PlayMode.xml`.

Corrective supplement `04B.3` validation:

- validator: passed, `records=49`, `missingP0=22`, `missingP1=6`;
- ReferenceCapture EditMode: **14/14 passed**;
- PlayMode: **9/9 passed**;
- full EditMode: **80/81 passed**; единственный failure относится к frozen 04A1 donor hash provenance после переустановки исходной игры, а не к reference-capture changes. Исторические 04A1 hashes не переписывались без новой audited extraction.

Runtime supplement `04B.4` validation:

- validator: passed, `records=51`, `missingP0=20`, `missingP1=6`;
- ReferenceCapture EditMode: **14/14 passed**;
- PlayMode: **9/9 passed**;
- full EditMode: **80/81 passed**; тот же unrelated frozen 04A1 donor hash drift;
- JSON/version/evidence-hash/CSV checks и `git diff --check`: passed (ledger показывает только informational CRLF/LF warning).

## 6. Ограничения и manual steps

В runtime supplement выполнены:

- три clean donor-runtime install/forward/reverse/remove repetition;
- runtime snap внутри collider-overlap candidate;
- явное подтверждение ключа `14` и полного прямого/обратного progression;
- user-confirmed wheel-installed removal blocker, связанный со static `TriggerWheelRL_New` gate.

Не заявляются physical torque, continuous angular turn или strip/failure behavior: donor contract дискретный `0..8`. Blocked case подтверждён текстовым user attestation с `Medium` confidence, без frame-addressable видео.

## 7. Решение и следующий milestone

`Prompts/05_VEHICLE_ASSEMBLY.md`: **GO в заявленных границах milestone**. Оба обязательных assembly requirement имеют `Covered`; representative fixture — `Ready`. M05 implementation в рамках 04B не начат.

Рекомендуемый следующий milestone — **Milestone 05 Vehicle Assembly**, строго по `Prompts/05_VEHICLE_ASSEMBLY.md`.
