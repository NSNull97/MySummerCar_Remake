# Weather shipping/content audit — Milestone 07C

Дата среза: 2026-07-17.

Статус: `PRIVATE_BASELINE_GUARD_PASS / PUBLIC_DISTRIBUTABLE_PROFILE_NO_GO`.

## Важно: два разных понятия build

Текущий active profile содержит лицензированный temporary donor baseline и
разрешён только для private local feature-parity development. Его нельзя
называть public/shipping build. Аудит разделяет:

1. private local Development build с явным подтверждением;
2. будущий public/distributable build без donor baseline.

## Current enabled Build Settings inventory

Всего включено 61 scene:

| Category | Count | Disposition |
|---|---:|---|
| Bootstrap | 1 | Required production entry |
| Prototype/development scenes | 6 | Не годятся для final shipping profile |
| Old production/prototype comparison scenes | 4 | Должны быть исключены/пересмотрены для final shipping profile |
| Donor RuntimeBaseline scenes | 50 | Допустимы только в explicitly acknowledged private Development build |

WeatherLab отсутствует. Enviro demo/sample scenes и Additional Weather Pack
scenes отсутствуют. Purchased Enviro runtime assets не удаляются: исключаются
его sample scenes, а required runtime dependency остаётся локальной vendor
зависимостью.

## Automated weather audit

`ProductionEnvironmentValidator` подтверждает:

- Bootstrap включён;
- ровно один active Enviro manager/adapter/global profile owner;
- WeatherLab не включён;
- paths под base Enviro и Additional Pack не включены как build scenes;
- old Bootstrap global environment placeholders inactive;
- binding asset полон;
- content помечен как private-guarded.

`Enviro3BuildSettingsTests.WeatherLab_IsAbsentFromEditorBuildSettings` и
production content tests проходят.

## Donor baseline guard

`DonorRuntimeBaselineBuildGuard` разрешает RuntimeBaseline только если
одновременно:

- build является Development;
- active manifest разрешает private local runtime baseline;
- текущий build process получил явное acknowledgement environment flag;
- build не заявляется public/distributable.

Иначе build блокируется. Этот hard guard — PASS. Он не является лицензией и не
превращает donor content в `ProductionReady`.

## Audit table

| Requirement | Private current profile | Future public profile | Evidence/status |
|---|---|---|---|
| WeatherLab excluded | PASS | required | Automated PASS |
| Enviro demo/sample scenes excluded | PASS | required | Automated PASS |
| Additional Weather Pack sample scenes excluded | PASS | required | Automated PASS |
| Azure Sky production owner | no competing active owner observed | must be absent | Bootstrap text/owner audit finds none; dedicated package-wide type scan not recorded |
| Old Bootstrap sky/fog placeholders active | no; inactive | must remain absent/inactive | Automated PASS |
| Duplicate environment manager | none | none allowed | Automated PASS |
| Editor diagnostics as scene dependency | no WeatherLab scene | full dependency audit required | Partial PASS |
| Temporary test materials/prefabs | WeatherLab excluded | complete dependency audit required | PENDING |
| Reference-only world scenes | not listed | complete dependency audit required | PENDING dependency sweep |
| Donor RuntimeBaseline | 50 scenes, private-only | forbidden | PRIVATE ONLY / PUBLIC NO-GO |
| Prototype/comparison scenes | present | forbidden unless explicitly retained | PUBLIC CLEANUP PENDING |
| Standalone build smoke | not run for full 51-scene donor profile | not possible until clean profile | PENDING |

## Why public shipping audit does not pass

Build Settings intentionally carry all 50 donor scenes plus development and
prototype scenes. Отдельного pruned public scene/content profile нет. Поэтому
пункт prompt «exclude donor assets» может быть истинным только для будущего
distributable profile, не для текущей private feature-parity конфигурации.

Нельзя исправлять это удалением purchased Enviro content или raw donor files.
Нужно создать отдельный project-owned public profile и dependency audit в
будущем разрешённом scope.

## Final result

- Private local content guard: `PASS`.
- WeatherLab/vendor sample scene exclusion: `PASS`.
- Full private Development Player smoke/performance: `PENDING`.
- Public/distributable shipping configuration: `NO-GO`.
- Public artifact produced: `NO`.
