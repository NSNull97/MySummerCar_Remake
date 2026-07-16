# Milestone 06B — Production World Cell Fidelity Gate

> **HISTORICAL / SUPERSEDED.** This report records the retired pre-v5
> production-cell fidelity gate. The active world workflow is now
> `06B1 -> 06B2 -> 06B3`, with the sanitized donor map as the temporary runtime
> baseline. Do not use this document as the current world or Enviro go/no-go.

Дата: 2026-07-16  
Unity project: `6000.3.11f1`  
Результат: **BLOCKED / STOPPED BEFORE PRODUCTION REPAIR**  
Milestone 07: **NOT STARTED**

## 1. Предыдущий milestone

Принятый Milestone 06A зафиксирован отдельным коммитом:

```text
bb30ffac521e0c7a2cce1c1c5e123f78661bb45a
vehicle: complete accepted Milestone 06A validation
```

После этого выполнена только разрешённая часть 06B.

## 2. Как установлены две cells

Ровно две существующие production cells имеют production scenes и runtime
streaming bindings:

| Cell | Intended donor location | Scene | First creation |
|---|---|---|---|
| `cell_0_-3` | дом, гараж и двор игрока; `YARD/Building/Garage` | `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity` | `d743043d4469d2ff0f072c06ea48e898cb6622c7` |
| `cell_0_-2` | домашний пирс, берег, озеро/дно и три hedge segments | `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity` | `53861b52ac6f8d9308f3e6f315c973a390210cd2` |

Идентификация подтверждена `CURRENT_STATE`, zone registry, runtime streaming
manifest, M05/M06A reports, scene roots, stable IDs и Git history. Историческая
ошибочная метка `CABIN/Shed` в `cell_0_0` исключена: это не фактический дом
игрока и у неё нет production-cell binding.

Подробности:
`Docs/WorldFidelity/REJECTED_CELL_IDENTIFICATION.md`.

## 3. Reference evidence

Пользователь передал два новых реальных donor runtime PNG:

1. Home/yard aerial overview:
   - локальный ignored path:
     `References/DonorRuntime/M06B/MSC-20171487-20260716-home-yard-aerial-overview-day.png`;
   - 1920×1080;
   - SHA-256:
     `ED82A65B276BCF7297C12FD7F4AB8BD036E8BC2FCC313F435F974090B045637C`;
   - уверенно связан с `cell_0_-3`;
   - supplementary only: aerial camera, transform/FOV unknown.
2. Rainy/foggy world-region aerial overview:
   - локальный ignored path:
     `References/DonorRuntime/M06B/MSC-20171487-20260716-world-region-aerial-overview-rain.png`;
   - 1920×1080;
   - SHA-256:
     `E26DCAB1F9AAC147985AB91C1251CC2CD7E77C2E44084F1EFD47E17B35533994`;
   - точная cell association не доказана;
   - не является neutral shoreline reference.

Также повторно проверены три пользовательских MP4 от 2026-07-14. Они относятся
преимущественно к wheel/brake-drum assembly около гаража и не закрывают
канонические world views.

Raw media исключено из Git через `References/DonorRuntime/`. В tracked
документации находятся только logical paths, SHA-256 и ограничения evidence.

## 4. Canonical fixture status

Созданы по десять стабильных camera IDs на cell:

- `Docs/WorldFidelity/cell_0_-3_CAMERAS.csv` — `10 / 10` описаний,
  `0 / 10` фактических canonical donor captures;
- `Docs/WorldFidelity/cell_0_-2_CAMERAS.csv` — `10 / 10` описаний,
  `0 / 10` фактических canonical donor captures.

Для неизвестных camera position, rotation, eye height и FOV записано
`PendingCapture`; значения не выдумывались.

Созданы versioned blocked fixtures на существующей reference schema:

- `Docs/WorldFidelity/Fixtures/cell_0_-3_reference_fixture.json`;
- `Docs/WorldFidelity/Fixtures/cell_0_-2_reference_fixture.json`.

## 5. Найденные различия

### `cell_0_-3`

По реальному donor aerial overview и историческому нематченному production
capture предварительно видны:

- другая узнаваемая схема main-road bend, driveway junction и прибытия к дому;
- другая связь длинного одноэтажного дома, гаража и гаражной площадки;
- другая композиция изгороди, utility pole, открытого двора и vegetation
  boundary.

Эти findings имеют `Medium` confidence для характера различия, но не дают
численных размеров. Они зарегистрированы как
`FID-CELL-0-M3-001..003`.

### `cell_0_-2`

Explicit user rejection подтверждён, но прямого donor pier/shore view нет.
Точные spatial/visual differences не классифицировались по памяти и не
заменялись generic shoreline. Blockers:
`FID-CELL-0-M2-001..002`.

## 6. Repairs

Production repair не выполнялся.

Не изменялись в рамках 06B:

- production scenes;
- prefabs;
- meshes;
- materials;
- colliders/LOD/streaming assets;
- Player/Interaction;
- Vehicle;
- donor installation;
- weather/Enviro.

Это обязательная остановка по условию:
`exact donor captures needed for comparison are missing`.

## 7. Stable identity

Сохранены без изменений:

- `cell_0_-3` root stable ID:
  `fc6a437b97ea997ca03a5e8bad1ba9b7`;
- `cell_0_-2` root stable ID:
  `7201412942822b5b4724b5e72c74065f`;
- home/garage anchor:
  `(153.495, 0.95, -1033.23)`;
- pier anchor:
  `(177.67, -1.779, -894.185)`;
- frozen database SHA-256:
  `706A4303A715539AC2D45A5AB0DFF487B685BFA3F336C57A10A477A45A454FA8`.

## 8. Tests and checks actually run

Выполнено:

- inspected current Git status and diff boundaries;
- inspected production manifests, registries, reports and Git history;
- visually inspected two new donor PNG;
- visually inspected historical production captures for the home and shoreline
  pilots;
- SHA-256 verified for both new PNG, all three existing MP4, frozen world
  database and both production scenes;
- all new CSV parsed through PowerShell `Import-Csv`;
- camera fixture counts: `10 + 10`;
- comparison index count: `20`;
- camera IDs unique: `20 / 20`;
- both fixture JSON parsed successfully:
  schema `1`, dataset `04B.4`, status `Blocked`;
- both zone rows verified as `Rejected`;
- `git diff --check` on the 06B tracked scope: PASS;
- raw donor PNG verified ignored and absent from `git ls-files`;
- protected user file
  `M3_NeutralVolume.asset` hash preserved:
  `9F88DF59949B4C59C9358BFB6B947C85251B6267CBE1DBC92032869A5E93268C`;
- `Prompts/Prompts.zip` remains absent.

Unity compile/EditMode/PlayMode tests were not run for this blocked portion:
runtime code, scenes, prefabs and production assets were intentionally not
changed. Existing M06A test evidence remains in its committed report.

## 9. Before/after comparisons

Canonical matched package:

| Artifact | Status |
|---|---|
| donor reference | `0 / 20` canonical; two supplementary PNG exist |
| rejected-before | `0 / 20` matched |
| repaired-after | not started |
| overlay/flicker | not started |

Index:
`Docs/WorldFidelity/COMPARISON_CAPTURE_INDEX.csv`.

Historical `PerformanceCaptures/Milestone05A/` files are explicitly
noncanonical: their `ReferenceOnly` mode contains metadata/proxy geometry, not
donor runtime imagery.

## 10. Current cell status

| Cell | Status |
|---|---|
| `cell_0_-3` | `Rejected / NeedsRework / NotApproved / FidelityGateBlocked` |
| `cell_0_-2` | `Rejected / NeedsRework / NotApproved / FidelityGateBlocked` |

`Docs/WorldRemaster/ZONE_REMASTER_STATUS.csv` и
`Docs/WorldFidelity/WORLD_FIDELITY_STATUS_LEDGER.csv` обновлены cell-level
override.

Ограничение: старый `WorldRemasterRegistryBuilder` по-прежнему способен
сгенерировать `ProductionCandidate`. Широкая регенерация не запускалась:
`WR_WorldProductionAssetRegistry.asset` уже имеет крупный пользовательский
serialization diff. Перед будущим builder run статус-authoring нужно исправить
в рамках продолжения 06B, не перезаписывая пользовательские assets.

## 11. Требуемое действие пользователя

Записать два MP4 по:

`Docs/WorldFidelity/MANUAL_DONOR_CAPTURE_REQUEST_RU.md`

В каждом видео нужны десять последовательных ракурсов с паузой не менее трёх
секунд. После записи сообщить Codex абсолютные пути к MP4 и, при наличии, к
скриншоту FOV/graphics settings.

## 12. Enviro 3 readiness

`NO-GO`.

Milestone 07, Enviro preflight и weather integration нельзя начинать, пока
обе cells не пройдут resumed 06B, bounded repair, matched after-capture и
explicit human approval.

## 13. Созданные/изменённые tracked files

- `.gitignore`;
- `Docs/WorldRemaster/ZONE_REMASTER_STATUS.csv`;
- `Docs/WorldFidelity/REJECTED_CELL_IDENTIFICATION.md`;
- `Docs/WorldFidelity/DONOR_EVIDENCE_INDEX.csv`;
- `Docs/WorldFidelity/WORLD_FIDELITY_STATUS_LEDGER.csv`;
- `Docs/WorldFidelity/MANUAL_DONOR_CAPTURE_REQUEST_RU.md`;
- per-cell reference fixture Markdown/CSV/JSON;
- per-cell blocked/preliminary diff audit и matrices;
- `Docs/WorldFidelity/COMPARISON_CAPTURE_INDEX.csv`;
- `Docs/WorldFidelity/HUMAN_APPROVAL_CHECKLIST.md`;
- `Docs/WorldFidelity/WORLD_CELL_FIDELITY_SUMMARY.md`;
- этот milestone report.

Пользовательские незакоммиченные prompt/world/art изменения сохранены и не
включались в 06B.

## 14. Следующий ровно один milestone

Не переходить к 07. Следующий шаг — **продолжение текущего Milestone 06B:
ingest canonical donor captures and capture matched rejected-before**.

Milestone 06B на этом останавливается по mandatory stop condition.
