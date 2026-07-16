# Reference fixture — `cell_0_-3`

Fixture revision: `M06B.1`  
Existing reference schema: `1 / dataset 04B.4`  
Status: **BlockedMissingCanonicalCaptures**

## Назначение

Канонический fixture для дома, гаража, двора, основной дороги и driveway
игрока. Он должен позволить повторить одинаковые donor/remake камеры без
подгонки ракурса после ремонта.

## Production identity

- scene:
  `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity`;
- prefab:
  `Assets/Game/World/Production/Prefabs/WR_HomeYardPilot.prefab`;
- root stable ID:
  `fc6a437b97ea997ca03a5e8bad1ba9b7`;
- anchor:
  `(153.495, 0.95, -1033.23)`;
- authored rotation:
  `(0, 180, 0)`;
- intended donor hierarchy:
  `YARD/Building/Garage`.

Stable ID и anchor не изменялись в 06B.

## Frozen donor/layout sources

- source database:
  `Assets/Game/World/Content/WorldTransfer/M04A1_WorldGeometryDatabase.json`;
- database revision:
  `04A1.1`;
- database SHA-256:
  `706A4303A715539AC2D45A5AB0DFF487B685BFA3F336C57A10A477A45A454FA8`;
- donor `level2` source SHA-256:
  `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31`;
- coordinate convention:
  Unity Y-up, `1 unit = 1 m`.

Исторический `CABIN/Shed` record не считается домом игрока и не используется
как authoritative fixture.

## Доступное runtime evidence

`M06B-EVD-001` — реальный donor screenshot:

`References/DonorRuntime/M06B/MSC-20171487-20260716-home-yard-aerial-overview-day.png`

Он подтверждает на композиционном уровне:

- изогнутую основную дорогу перед/слева от участка;
- отдельное примыкание driveway к гаражной площадке;
- длинный одноэтажный кирпичный объём дома;
- гаражную часть и площадку позади/справа относительно главного фасада;
- подстриженную изгородь вокруг передней части двора;
- столб и плотную, нерегулярную границу растительности.

Из этого кадра нельзя надёжно получить точные размеры, высоты, FOV, camera
transform или road centerline. Он является supplementary evidence, а не
канонической камерой.

Три существующих MP4 относятся преимущественно к сборке автомобиля около
гаража. Они дают только локальный контекст и не закрывают ни одну полную
camera category.

## Канонические камеры

Определены десять стабильных camera IDs в
`Docs/WorldFidelity/cell_0_-3_CAMERAS.csv`.

Для всех камер пока отсутствуют:

- donor camera position/rotation;
- eye height, подтверждённая записью;
- точный FOV;
- frame-addressable neutral donor evidence;
- matched rejected-before remake capture.

## Neutral condition

Будущий matched capture:

- `1920×1080`, `16:9`;
- ясный день без дождя/тумана;
- одинаковый standing FOV;
- без DOF, motion blur, vignette и драматического grading;
- player-height standing views, кроме явно помеченного vehicle-seat view.

## Измерения и uncertainty

| Параметр | Состояние |
|---|---|
| Home/garage anchor | подтверждён serialized world data |
| Root stable ID | подтверждён production scene |
| Road centerline/width/elevation | недостаточно данных для repair tolerance |
| House footprint/height/rotation tolerance | не заморожены каноническими кадрами/измерениями |
| Driveway clearances | только project-authored fixture; donor parity не доказана |
| Camera transforms/FOV | `PendingCapture` |

## Gate

Production repair запрещён до получения всех десяти donor views. Допустимо
только зарегистрировать новые captures, вычислить hashes, заполнить camera
metadata и затем снять matched rejected-before состояние.
