# M05C1 — Покрытие непрерывной земли

Дата: 2026-07-15

Статус: **ограниченный baseline у Теймо автоматически и вручную принят**

## Что покрывает документ

05C1 рассматривает только одну подтверждённую внутреннюю пустоту donor-карты
за визуальной границей леса около магазина Теймо. Полное заполнение карты,
внешняя область мира и художественный terrain remaster не заявляются.

| Показатель | Значение |
|---|---:|
| Region ID | `M05C1-VOID-TEIMO-001` |
| Классификация | `IntentionalDonorVoid` |
| Approved regions | `1` |
| Generated pieces | `2` |
| Ячейки | `cell_-4_0`, `cell_-3_0` |
| Полный AABB по X | `[-1742, -1447.7] m` |
| Полный AABB по Z | `[100, 225] m` |
| Межъячеечный seam | `x=-1536 m` |
| Generated vertices | `104` |
| Generated triangles | `100` |
| Seam vertex pairs | `26` |
| Collider samples | `100`, требуется `100/100` hits |
| Maximum slope | `15°` |
| Gameplay area opened | `false` |
| Manual approval | `PASS`, пользователь, 2026-07-15 |

## Approved pieces

| Piece | Stable ID | Cell | Mesh | Scene |
|---|---|---|---|---|
| `piece_cell_-4_0` | `9c32cc25615de19b4d8ce23a2ade1b5b` | `cell_-4_0` | `Assets/Game/World/Production/Terrain/VoidFill/M05C1_TeimoVoidFill_cell_-4_0.asset` | `Assets/Game/World/Generated/VoidFillCells/VoidFill_cell_-4_0.unity` |
| `piece_cell_-3_0` | `a708a95ca385ee7e5dbdf56bf2d26eca` | `cell_-3_0` | `Assets/Game/World/Production/Terrain/VoidFill/M05C1_TeimoVoidFill_cell_-3_0.asset` | `Assets/Game/World/Generated/VoidFillCells/VoidFill_cell_-3_0.unity` |

Обе части используют один глобальный station-профиль с шагом не более `5 m`,
surface offset `-0.08 m` и water-mask status `LakebedAabbNonOverlap`.

## Frozen reference basis

- world database: `M04A1_WorldGeometryDatabase.json`, version `04A1.1`;
- database SHA-256:
  `706A4303A715539AC2D45A5AB0DFF487B685BFA3F336C57A10A477A45A454FA8`;
- 05C reference scope: `3 842` represented geometry records in `49` concrete
  cells plus service/global layers;
- Teimo evidence target: approximately `(-1382, 6, 145)`;
- boundary evidence is stored as stable IDs in
  `M05C1_DONOR_VOID_REGIONS.csv`.

Reference geometry remains removable and is not used by generated meshes or
colliders at runtime.

## Classification coverage

| Класс | Зарегистрировано | Заполнено | Решение 05C1 |
|---|---:|---:|---|
| `IntentionalDonorVoid` | 1 region / 2 pieces | 1 region / 2 pieces | Только bounded Teimo pilot |
| `WaterOrShoreline` | 0 | 0 | Не заполнять землёй |
| `ExternalWorldBoundary` | 0 | 0 | Вне scope |
| `UnknownGap` | 0 | 0 | При обнаружении — blocker |

Значение `0` для остальных классов означает отсутствие таких строк в
ограниченном реестре 05C1, а не отсутствие подобных мест на всей карте.

## Cell ownership

Регион разделён на две project-owned части на границе `x=-1536`:

- западная часть принадлежит `cell_-4_0`;
- восточная часть принадлежит `cell_-3_0`;
- station-профиль хранится в world coordinates;
- generated vertices хранятся относительно origin своей ячейки;
- seam должен иметь совпадающие position и height с обеих сторон.

Один общий mesh, пересекающий обе ячейки, не создаётся. Cross-cell hierarchy
не используется.

## Реализуемая поверхность

Источник истины для точной формы, IDs, destinations и fingerprints:

`Docs/WorldRemaster/M05C1_DONOR_VOID_REGIONS.csv`.

Builder `05C1.1` интерполирует только между явно записанными station-профилями,
ограничивает каждую часть границами её ячейки и использует существующий
project-owned `WR_Terrain.mat`. На каждой части создаются:

- `MeshFilter` и `MeshRenderer`;
- статический non-convex `MeshCollider` на том же mesh;
- `WorldVoidFillMarker`;
- project-owned `StableEntityIdAuthoring`.

Baseline не изменяет donor tree-card boundary, дорогу, магазин, воду или
окружающие объекты.

Обе generated void-fill scenes намеренно исключены из обычных Build Settings
до ручной приёмки и отдельного integration-решения в 05B.

## Проверяемое покрытие

Автоматическая приёмка должна подтвердить:

- один region, две pieces и две ожидаемые cells;
- уникальность stable IDs и соответствие authoring fingerprints;
- конечные vertices и ненулевую геометрию;
- cell-local ownership;
- совпадение обеих частей на `x=-1536`;
- `26` seam pairs совпадают с допуском `0.0001 m`;
- наличие визуальной поверхности и статической коллизии;
- отсутствие `Rigidbody` и donor/reference dependencies;
- collision-probe покрытие всех `100` triangle centroids с допуском точки
  `0.02 m`, без уклона выше `15°`;
- повторяемость generated geometry при неизменном реестре.

Фактические результаты запусков записываются в
`Docs/Milestones/MILESTONE_05C1_REPORT.md`.

Фактический automated baseline 2026-07-15: `PASS` — `1` region, `2` pieces,
`2` cells, `104` vertices, `100` triangles, `26` совпадающих seam-пар,
`100/100` collision probes и максимальный уклон `2.582°`. Два последовательных
in-place rebuild дали одинаковые SHA-256 для обеих сцен и обоих mesh assets.
Focused tests: EditMode `3/3`, PlayMode `1/1`.

## Не покрыто

- остальные пустоты donor-карты;
- внешний мир за конечной границей;
- финальный terrain heightfield и terrain layers;
- удаление старых визуальных стен и создание нового лесного пояса;
- roads, buildings, water/shoreline, vegetation и HLOD;
- runtime streaming всей production-карты;
- Player traversal, NPC, traffic и другие gameplay systems;
- lighting, weather, audio, UI и save.

## Manual gate

`M05C1_INSPECTION_CHECKLIST.md` подтверждён пользователем 2026-07-15: заметный
seam, провал и выход за bounded scope не обнаружены. Одобрение относится только
к safety/topology baseline и не является приёмкой final terrain art.
