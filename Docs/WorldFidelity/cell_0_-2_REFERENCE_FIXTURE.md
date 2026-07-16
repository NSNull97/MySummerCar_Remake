# Reference fixture — `cell_0_-2`

Fixture revision: `M06B.1`  
Existing reference schema: `1 / dataset 04B.4`  
Status: **BlockedMissingCanonicalCaptures**

## Назначение

Канонический fixture для домашнего пирса, берегового профиля, воды/дна,
перехода к дому и трёх участков изгороди.

## Production identity

- scene:
  `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity`;
- prefab:
  `Assets/Game/World/Production/Prefabs/WR_HomeShorelinePier.prefab`;
- root stable ID:
  `7201412942822b5b4724b5e72c74065f`;
- pier anchor:
  `(177.67, -1.779, -894.185)`;
- lake tile anchor:
  `(279.98, -2.929, -606.625)`;
- lake bottom anchor:
  `(362.28, -35.029, -828.725)`.

Stable ID и anchors не изменялись в 06B.

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

Authoritative donor paths:

- `MAP/PierHome/pier`;
- `MAP/PierHome/pier_pontons`;
- `MAP/PierHome/coll`;
- `MAP/LakeNice/Lake/Tile`;
- `MAP/Bottom`;
- три `YARD/Building/LOD/HedgeFence/hedge`.

## Доступное runtime evidence

Прямого donor runtime view домашнего пирса и береговой линии нет.

`M06B-EVD-002` показывает дождливый/туманный aerial-контекст региона, но:

- точная world location не доказана;
- пирс и три hedge anchors не идентифицированы однозначно;
- погода не является нейтральной;
- transform и FOV неизвестны.

Поэтому этот PNG не назначен ни одной canonical camera и не используется для
пространственного ремонта.

## Канонические камеры

Определены десять стабильных camera IDs в
`Docs/WorldFidelity/cell_0_-2_CAMERAS.csv`.

Для всех десяти отсутствуют:

- доказанный donor screenshot/video frame;
- camera position/rotation и eye height;
- FOV;
- neutral clear-day condition;
- matched rejected-before capture.

## Измерения и uncertainty

| Параметр | Состояние |
|---|---|
| Pier/lake/bottom/hedge anchors | подтверждены frozen serialized data |
| Pier silhouette, width, supports and pontoons | runtime visual fixture отсутствует |
| Shoreline profile and terrain boundary | runtime visual fixture отсутствует |
| Home-to-shore sightline | не заморожен |
| Vehicle approach viewpoint | не заморожен |
| Camera transforms/FOV | `PendingCapture` |

## Gate

Нельзя угадывать форму берега, силуэт пирса, видимые опоры, vegetation boundary
или отношение к дому по одному неидентифицированному aerial-кадру. Production
repair запрещён до получения всех десяти donor views.
