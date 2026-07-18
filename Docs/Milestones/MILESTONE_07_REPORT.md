# Milestone 07 — environment and weather summary

Дата среза: 2026-07-18.

Общий статус: **`07A IMPLEMENTED / 07B ENGINEERING PASS / 07C DAWN-NIGHT
FOLLOW-UP TARGETED AND USER PASS, WORLD MATERIAL CONTRACT FAIL, BROADER
FIDELITY/PERFORMANCE PENDING`**.

## Итог архитектуры

Milestone 07 построил независимую environment architecture:

```text
Project GameTime + deterministic Weather + Wetness + Lightning
  -> vendor-neutral outputs and presentation frames
  -> dedicated Enviro 3 adapter
  -> one production Bootstrap presentation owner
```

Enviro отвечает только за presentation. Project systems сохраняют authority для
time/calendar, schedule/transitions, accumulated wetness, gameplay lightning,
session save state и cross-system outputs. Vendor source read-only, Enviro audio
не является production audio authority.

## 07A — preflight and WeatherLab

Реализованы package/API audit, dedicated adapter boundary, single-owner
WeatherLab, fog/water strategy, silent audio policy, runtime precipitation and
visual lightning smoke. Пользователь принял bounded visual smoke Rain/Storm и
fog/lightning, но formal captures/Low-High/performance evidence оставались
отдельным debt.

Commit: `61250e2`.

## 07B — project-owned domain

Реализованы transactional GameTime, deterministic weather fronts/RNG,
accumulated wetness/drying, ambient/gameplay lightning separation, versioned DTO,
vendor-neutral outputs, Enviro mappings и WeatherLab DEV/performance harness.

Accepted automated evidence:

- core vendor-neutral: `101/101 PASS`;
- Enviro integration: `13/13 PASS`;
- edge cases: `3/3 PASS`;
- Editor performance harness: `1/1 PASS`.

Commit: `ee553f3`.

## 07C — production rollout

Production Bootstrap теперь содержит один persistent environment owner. Restore
и first presentation sync выполняются до world reveal; additive cells не
перезапускают authority. Default quality — Medium; Low/Medium/High используют
project stable IDs. Scheduled front time конвертируется из game seconds в
simulation seconds, а initial/new/restore sync остаётся instant. Wind идёт в
один Enviro-owned WindZone. Wetness globals,
bounded legacy MPB bridge, shelter contract и disabled wet-road hook существуют.

Canonical Enviro source prefab сохранён linked и authored inactive; project-owned
activator включает его после ownership, installer ждёт readiness, а same-scene
reload ждёт уничтожения старого persistent static owner.

Bounded shelter builder готовит два static Interior AABB для home house/garage
из frozen renderer evidence. Первый ручной route выявил овальный overhang
прежнего single-ellipsoid bridge. Теперь он строит детерминированные
вписанные tiled zones. Post-fix perimeter acceptance остаётся `PENDING`,
другие интерьеры не покрыты.

Тот же manual route дал `FAIL` для constant `EV 10` (пересвеченный
день/интерьер и crushed night), double/misbound opaque/red fog и дождя,
который спавнился, но был нечитаем в Game View из-за runtime
`maxParticleSize = 0.001`. Project-owned remediation ввела isolated runtime
Volume rebinding, deterministic fixed exposure curve с context offsets, один
visibility-to-HDRP-MFP fog pass и runtime-only rain floor `0.01`.

Последующий user review принял rain и текущий sunset. Metallic/reflection look
оставлен как явно принятый временный debt donor materials/shaders. Для двух
оставшихся observations runtime-only solar calibration изменена на
`60 N / 27.3 E / UTC+3` (Enviro horizon около `04:59/21:35`), а night exposure
получил плавный minimum `7.5 EV`; дневная экспозиция не изменилась.

Editor-only Runtime Validation window даёт bounded controls time/weather/seed,
wetness, quality и ambient lightning, owner/lifecycle/shelter diagnostics,
JSON export и captures. Это DEV evidence surface, не shipping UI.

Second-remediation automated 07C evidence:

- compile + Builder `1.0.2` + production validator: `PASS`,
  `Logs/M07C_VisualRemediation2_Build.log`;
- combined WeatherProduction / ProductionIntegration / legacy wetness:
  `32/32 PASS`, `Logs/M07C_VisualRemediation2_WeatherProduction.xml`;
- EnviroIntegration: `17/17 PASS`,
  `Logs/M07C_VisualRemediation2_EnviroIntegration.xml`;
- WeatherPresentation: `53/53 PASS`,
  `Logs/M07C_VisualRemediation2_WeatherPresentation.xml`;
- Production PlayMode: `6/6 PASS`,
  `Logs/M07C_VisualRemediation2_ProductionPlayMode.xml`;
- world-only compatibility: `1/1 PASS`,
  `Logs/M07C_VisualRemediation2_WorldOnlyCompatibility.xml`;
- full EditMode: `330/334` with the same four unrelated historical failures,
  `Logs/M07C_VisualRemediation2_FullEditMode.xml`;
- frozen-world revalidation: `PASS`, unchanged result hash
  `10544fc3ed5bd6c8cd44b451155e766c0552710f4c9d8cc91dda263de001ba5f`,
  `Logs/M07C_VisualRemediation2_WorldFreeze.log`;
- manual production visual retest after second remediation: superseded by the
  partial user review above.

Fresh dawn/night follow-up evidence:

- EnviroIntegration: `17/17 PASS`,
  `Logs/M07C_VisualRemediation3_EnviroIntegration.xml`;
- combined production EditMode: `32/32 PASS`,
  `Logs/M07C_VisualRemediation3_WeatherProduction.xml`;
- Production PlayMode: `6/6 PASS`, including chronological dusk/dawn brackets,
  `Logs/M07C_VisualRemediation3_ProductionPlayMode.xml`;
- full EditMode: `328/334`, `Logs/M07C_VisualRemediation3_FullEditMode.xml`.
  Four failures are the same historical set; two additional WorldBaseline
  failures report four ignored generated compatibility materials already
  drifted before this follow-up. Frozen payload was not edited and a fresh
  strict freeze PASS is not claimed;
- focused WorldBaseline: `8/10`, reproducing the same two material-contract
  failures, `Logs/M07C_VisualRemediation3_WorldBaseline.xml`;
- manual dawn/night retest: `USER PASS` on 2026-07-18, without a saved capture
  artifact.

Прежние `16/16`, `328/332` и другие artifacts сохраняются
только как historical pre-remediation evidence.

Exact Enviro patch остаётся unknown. Accepted payload identity:
`538 / 305967931 /
8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44`.
Functional vendor modifications в 07C: `0`.
Visual remediation не меняла tracked frozen donor world scenes, transforms,
manifests или vendor payload. The last strict frozen hash remains historical
evidence; current generated-material contract requires separate remediation.

## Production scope truth

Следующие пункты не закрыты и не считаются PASS:

- matched neutral clear-day before/after captures;
- strict geometry/transform regression package;
- production weather route across streaming cells;
- dawn/night matched captures (manual visual acceptance is `USER PASS`);
- accepted temporary reflection/material debt and current generated-material
  contract remediation;
- Low/Medium/High production performance comparison;
- Windows x64 1080p Development Player profiling;
- manual long-soak evidence;
- public/distributable content profile;
- file-backed save system.

Текущий donor world остаётся `TemporaryDirectImport` и private-only. Build guard
защищает local Development use, но 50 donor scenes исключают public shipping.

## Documentation index

- `Docs/Weather/PRODUCTION_ROLLOUT.md`;
- `Docs/Weather/PRODUCTION_OWNER_MATRIX.csv`;
- `Docs/Weather/QUALITY_TIERS.md`;
- `Docs/Weather/PRODUCTION_BINDING_VALIDATION.md`;
- `Docs/Weather/PRODUCTION_SAVE_RESTORE.md`;
- `Docs/Weather/PRODUCTION_PERFORMANCE.md`;
- `Docs/Weather/PRODUCTION_REGRESSION.md`;
- `Docs/Weather/WORLD_FIDELITY_WEATHER_REGRESSION.md`;
- `Docs/Weather/SHIPPING_CONTENT_AUDIT.md`;
- `Docs/Weather/KNOWN_LIMITATIONS.md`;
- `Docs/Milestones/MILESTONE_07C_REPORT.md`.

## Go/no-go

- 07 architecture and 07C visual-remediation automated gate: `PASS`.
- Bounded rain/sunset/dawn/night review: `USER PASS`; temporary reflection debt
  is explicitly user-accepted. Broader fidelity evidence remains `PENDING`.
- Full Milestone 07 acceptance: `PENDING`.
- Automatic transition to Milestone 08: `NO-GO`.

Рекомендуемый следующий этап: bounded 07C manual/fidelity/performance evidence
closure, после чего пользователь отдельно принимает или отклоняет переход к 08.
