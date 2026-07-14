# Отчёт пространственной точности

## Автоматически доказано

- identity axis/rotation/scale conversion с одной audited translation;
- garage roof anchor: source `(-169.98,-1.6110001,1040.625)` -> remake `(0,~-1.19e-7,0)`;
- five fixture transforms записаны с tolerance 0,005–0,05 м;
- 13 509 stable IDs уникальны;
- cell assignment и parent-child composition тестируются;
- generated scene stamps соответствуют database `04A1.1`.

## Landmark deviation

| Fixture | Расчётное отклонение conversion | Runtime visual comparison | Confidence |
|---|---:|---|---|
| Primary garage roof | < 0,000001 м | Previous audited roof measurement + current proxy origin | High for transform |
| Repair area sample | 0 by formula | Pending | Medium for serialized transform |
| Town pier sample | 0 by formula | Pending | Medium |
| Inspection sample | 0 by formula | Pending | Medium |
| Highway bridge sample | 0 by formula | Pending | Medium |

Нулевое deviation по формуле означает точное применение serialized transform, а не доказанную визуальную точность mesh reconstruction.

## Непроверенные отклонения

- terrain elevation: не измерено;
- road centerline/elevation/width: не измерено;
- building placement: serialized transforms сохранены, screenshots pending;
- interior alignment: pending;
- collider alignment: metadata сохранено, physics walk/drive pending;
- shoreline/water elevation: pending;
- extreme Y bounds: review required.

## Zone confidence

- `cell_0_0` garage anchor: High for origin, Medium for surrounding unreviewed geometry.
- cells with major landmark records: Medium for data, Low for visual parity.
- all other generated cells: Medium for serialized placement, Low for semantic/visual parity.
- global large geometry: Medium-Low because 2 007 combined-mesh bounds require review.

Required screenshots: overhead full map; garage/home; town; repair shop; inspection; major bridge; cottage; landfill; representative junctions; lake/shoreline; cell seam pairs. До их получения visual parity не заявляется.
