# Milestone 06A — интеграция автомобиля с миром

## Итог и границы

Проверка разделена на два независимых контура:

- изолированный M06A course для повторяемой динамики;
- bounded production fixture для garage, driveway, surface lookup и streaming boundary.

Полная карта 05C остаётся `WorldLayoutReference`/blockout baseline из 49 cells и 3842 entities. Она не объявляется полностью production-ready.

Production runtime сейчас содержит две pilot cells:

- `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity`;
- `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity`.

Пользовательская проверка 2026-07-16 отклонила их сходство с оригиналом: обе ячейки визуально и пространственно «абсолютно не похожи» на соответствующие места donor-игры. Поэтому в 06A они служат только техническими collision/streaming fixtures и не являются доказательством donor world parity.

## Доступные fixtures

| Проверка | Значение | Классификация | Граница результата |
|---|---:|---|---|
| garage opening | `3.12 × 2.22 m` | `RemakeDesignTarget` | технический проём |
| M06 collider envelope | `1.35 × 0.42 × 3.15 m` | `RemakeDesignTarget` | явный config |
| driveway width | примерно `8.2 → 6.5 m` | project-authored | bounded geometry |
| garage threshold | `0.105 m` | `RemakeDesignTarget` | driveway-to-floor |
| road/driveway height delta | примерно `0.015 m` | project-measured | bounded fixture |
| streaming boundary | `z = -1024` | runtime contract | cells `0_-3/0_-2` |
| home-to-pier seam | max step `0.25 m` | project validation | не production road |

## Semantic surface metadata

M06A добавляет metadata в authoring source production pilot:

- garage floor — `Paved`;
- driveway — `Gravel`;
- bounded road — `Gravel`;
- home terrain — `Grass`.

Классификация — `RemakeDesignTarget`. Donor friction coefficients отсутствуют. Metadata не выводится из имён объектов или материалов и покрывает только bounded home fixture.

## Collision и layers

В исходном аудите 164 world colliders находились на `Default` layer и без `PhysicMaterial`. `WORLD-COL-003` остаётся открытым. M06A не вводит широкую layer/material taxonomy: backend использует explicit surface metadata и текущую contact mask.

Проверяются только:

- отсутствие намеренного разрыва на synthetic collider transition;
- гаражный порог;
- driveway/road переход;
- сохранение wheel contacts на bounded маршруте;
- отсутствие invalid numeric state.

## Streaming

PlayMode fixture загружает `Bootstrap.unity`, отключает конфликтующий M4 vehicle, добавляет M06 simulation scene, назначает chassis как streaming focus, открывает гаражные створки и driveway gates и едет от `(153.495, 1.665734, -1036.23)` к границе `z = -1024`.

Фактический bounded run:

- `302` route frames;
- reached `z = -1023.974915`;
- horizontal progress `12.255066 m`;
- peak speed `2.489053 m/s`;
- maximum lateral deviation `0.213638 m`;
- minimum contact count `4`;
- zero-contact streak `0`;
- `Production_cell_0_-2` загружена автоматически;
- после пересечения границы сохранены `4` контакта;
- post-streaming vertical delta `0.000231 m`.

После этого выполняется отдельный next-cell-only contact probe на `z = -970`: colliders `cell_0_-3` отключаются, chassis ставится на support geometry `cell_0_-2`, и в measured window сохраняются `4` wheel contacts с максимальным vertical delta `0.000231 m`. Probe также подтверждает typed `Grass` metadata следующей ячейки.

Успех bounded fixture означает, что локальная граница пересекается без потери контактов, соседняя cell автоматически загружается, а next-cell geometry отдельно поддерживает автомобиль. Он не доказывает непрерывный drive от `z = -1024` до `z = -970` и не закрывает полную world-route continuity.

Durable evidence:

- `Docs/VehicleValidation/M06A_PHYSX_RUN_EVIDENCE.json`;
- `Docs/VehicleValidation/Telemetry/production-world-transition.csv`;
- строки `world.*` в `Docs/VehicleValidation/METRIC_RESULTS.csv`;
- run `m06a-physx-world-transition` в `Docs/VehicleValidation/CALIBRATION_RUNS.csv`.

## Открытые world gaps

- road bindings: `0/29`;
- bridge bindings: `0/2`;
- нет непрерывного production road/bridge vehicle route;
- curb/ditch interaction не имеет production fixture;
- полная terrain-transition матрица отсутствует;
- reset/recovery в production world не валидирован;
- нет полной collision layer/PhysicMaterial policy;
- spatial/visual donor parity двух pilot cells отклонена пользователем.

Связанные открытые issues: `WORLD-COL-001`, `WORLD-COL-003`, `WORLD-ROAD-001`, `WORLD-STREAM-003`, `WORLD-STREAM-004`.

## Вердикт для 06A

- Автоматический gate изолированной vehicle physics validation и bounded home collision/streaming fixture: `PASS`.
- Текущий статус: `Accepted / HumanAccepted` 2026-07-16.
- `GO` для перехода к bounded Milestone 06B; Milestone 07 остаётся за границей текущего этапа.
- `NO-GO` для заявлений о полной production road, bridge, terrain или donor-world parity.
- Мир нельзя переписывать в рамках 06A ради сокрытия physics defects.
- Следующий milestone: `06B_PRODUCTION_WORLD_CELL_FIDELITY_GATE`.
