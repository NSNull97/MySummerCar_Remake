# World cell fidelity summary — Milestone 06B

> **HISTORICAL / SUPERSEDED.** This summary belongs to the retired pre-v5
> two-cell fidelity gate. The two custom cells remain useful technical fixtures,
> but the active feature-parity profile uses the sanitized donor baseline under
> the `06B1 -> 06B2 -> 06B3` workflow.

Дата: 2026-07-16  
Результат: **BLOCKED BEFORE PRODUCTION REPAIR**

## Краткий итог

| Cell | Donor location | Canonical views | Supplementary runtime evidence | Status |
|---|---|---:|---|---|
| `cell_0_-3` | дом, гараж, двор и дорожный подъезд игрока | `0 / 10` | один ясный aerial overview + локальные assembly-видео | `Rejected / NeedsRework / NotApproved` |
| `cell_0_-2` | домашний пирс, берег, вода/дно и три hedge segments | `0 / 10` | один rainy/foggy world overview с неподтверждённой cell-привязкой | `Rejected / NeedsRework / NotApproved` |

## Выполнено

- полностью идентифицированы ровно две отклонённые production cells;
- доказаны intended donor locations, scenes, first-creation commits, anchors и
  root stable IDs;
- старый `ProductionCandidate` переопределён на `Rejected` в cell-level status
  ledger;
- приняты, сохранены вне Git и хэшированы два новых donor PNG;
- заведены по десять стабильных camera IDs;
- созданы versioned blocked fixtures на существующей reference schema;
- создан точный русскоязычный capture request;
- выполнен ограниченный preliminary audit `cell_0_-3`;
- для `cell_0_-2` отказались придумывать diff без прямого donor evidence;
- production scenes, prefabs, meshes, materials, player, interaction и vehicle
  content не изменялись.

## Почему gate остановлен

06B требует по десять реальных donor runtime views на cell, затем matched
rejected-before, и только после этого — bounded repair. Два aerial PNG полезны,
но:

- не имеют camera transform/FOV metadata;
- сняты не с канонической высоты игрока;
- второй кадр не нейтрален и не имеет доказанной shoreline-привязки;
- не покрывают road-level, facade, reverse, vehicle-seat и pier-specific views.

Следовательно mandatory stop condition
`exact donor captures needed for comparison are missing` остаётся активным.

## Repairs и before/after

- production repairs: **не выполнялись**;
- matched donor captures: **не созданы**;
- matched rejected-before: **не создан**;
- repaired-after: **не применимо**;
- overlays: **не применимо**.

Исторические captures в `PerformanceCaptures/Milestone05A/` просмотрены только
как нематченный project baseline. Они не являются donor fidelity evidence.

## Риски

- `WorldRemasterRegistryBuilder` исторически генерирует
  `ProductionCandidate`; широкая регенерация сейчас не запускалась, поскольку
  registry asset имеет крупный пользовательский serialization diff и могла бы
  перезаписать текущую работу.
- `WORLD_REPLACEMENT_LEDGER.csv` хранит maturity отдельных replacement assets;
  он не используется для отмены authoritative cell-level rejection.
- существующие static transforms не доказывают silhouette, sightline или
  узнаваемость.

## Следующий единственный шаг

Продолжить этот же Milestone 06B после получения двух MP4 по
`MANUAL_DONOR_CAPTURE_REQUEST_RU.md`. Milestone 07 и Enviro 3 пока имеют
`NO-GO`.
