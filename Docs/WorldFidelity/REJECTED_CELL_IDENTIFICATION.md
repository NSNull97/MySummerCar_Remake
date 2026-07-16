# Milestone 06B — идентификация отклонённых production cells

Дата: 2026-07-16  
Статус: **ровно две ячейки идентифицированы; неоднозначности, требующей запроса к пользователю, нет**

## Авторитетный статус

Пользователь отклонил визуальное и пространственное сходство двух существующих
production cells. Для обеих действует:

```text
Rejected
NeedsRework
NotApproved
NotComplete
```

Технические проверки коллизий, traversal и streaming не отменяют этот статус.

## Установленная пара

| Cell | Production scene / prefab | Предполагаемое место оригинала | Первый production-коммит | Текущий статус | Уверенность |
|---|---|---|---|---|---:|
| `cell_0_-3` | `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity`; `WR_HomeYardPilot` | дом, гараж и двор игрока; donor hierarchy `YARD/Building/Garage` | `d743043d4469d2ff0f072c06ea48e898cb6622c7` — `world: complete bounded Milestone 05A pilot` | `Rejected / NeedsRework / NotApproved` | 0,99 |
| `cell_0_-2` | `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity`; `WR_HomeShorelinePier` | домашний пирс, берег, озеро/дно и три участка изгороди | `53861b52ac6f8d9308f3e6f315c973a390210cd2` — `world: complete accepted map baseline and Milestone 05B validation` | `Rejected / NeedsRework / NotApproved` | 0,99 |

## Доказательная цепочка

1. `Prompts/CURRENT_STATE.md` фиксирует пользовательское отклонение ровно двух
   уже существующих production cells и отменяет прежний `ProductionCandidate`.
2. `Docs/WorldRemaster/ZONE_REMASTER_STATUS.csv` содержит ровно две строки с
   непустым `ProductionCellPath`: `cell_0_-3` и `cell_0_-2`.
3. `Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset`
   адресует ровно эту пару production cells.
4. `Docs/VehicleValidation/WORLD_INTEGRATION_REPORT.md` и
   `Docs/Milestones/MILESTONE_06A_REPORT.md` прямо называют обе ячейки
   отклонёнными по donor spatial/visual fidelity и разрешают использовать их
   только как технические fixtures.
5. Git-история однозначно связывает создание `cell_0_-3` с 05A pilot, а
   `cell_0_-2` — с HomeShorelinePier batch.

## Идентичность и стабильные точки

### `cell_0_-3`

- production root stable ID:
  `fc6a437b97ea997ca03a5e8bad1ba9b7`;
- home/garage anchor:
  `(153.495, 0.95, -1033.23)`;
- authored rotation:
  `(0, 180, 0)`;
- правильный donor-контекст:
  `YARD/Building/Garage`.

Историческая 04A1-метка `PrimaryHomeGarage` на `CABIN/Shed` в `cell_0_0`
является известной неоднозначностью данных и не была использована для выбора
ячейки. У `cell_0_0` нет production-cell сцены и записи в runtime streaming
manifest.

### `cell_0_-2`

- production root stable ID:
  `7201412942822b5b4724b5e72c74065f`;
- pier anchor:
  `(177.67, -1.779, -894.185)`;
- подтверждённые donor paths:
  - `MAP/PierHome/pier`;
  - `MAP/PierHome/pier_pontons`;
  - `MAP/PierHome/coll`;
  - `MAP/LakeNice/Lake/Tile`;
  - `MAP/Bottom`;
  - три `YARD/Building/LOD/HedgeFence/hedge`.

## Новые пользовательские runtime-снимки

- `M06B-EVD-001` — реальный aerial overview домашнего участка, 1920×1080,
  SHA-256
  `ED82A65B276BCF7297C12FD7F4AB8BD036E8BC2FCC313F435F974090B045637C`.
  Он уверенно относится к `cell_0_-3`, но не является канонической камерой:
  transform и FOV неизвестны, камера находится сильно выше уровня игрока.
- `M06B-EVD-002` — реальный rainy/foggy aerial overview региона, 1920×1080,
  SHA-256
  `E26DCAB1F9AAC147985AB91C1251CC2CD7E77C2E44084F1EFD47E17B35533994`.
  Точная привязка к `cell_0_-2` по одному кадру не доказана, поэтому он
  зарегистрирован как общий контекст, а не как shoreline fixture.

Raw PNG сохранены локально под ignored-путём
`References/DonorRuntime/M06B/`; в Git входят только метаданные и hashes.

## Вывод Phase 1

Ровно две ячейки установлены: `cell_0_-3` и `cell_0_-2`. Production content
после идентификации не изменялся. Следующий разрешённый шаг — получить
канонический donor-набор камер; без него bounded repair запрещён условиями 06B.
