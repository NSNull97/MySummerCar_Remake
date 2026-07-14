# Отчёт семантической классификации

Правила: `Config/WorldTransferRules.example.json`, schema 2. Классификация детерминирована при одинаковых source hashes/rules, учитывает имя, hierarchy context, world-root allowlist и exclusions.

## Reference-world категории

| Категория | Количество |
|---|---:|
| Bridge | 2 |
| BuildingExterior | 1 568 |
| BuildingInterior | 211 |
| ColliderOnly | 772 |
| Door | 120 |
| Fence | 18 |
| Field | 2 |
| Floor | 23 |
| InteractivePropCandidate | 395 |
| Landmark | 9 |
| Road | 29 |
| RoadSign | 163 |
| Rock | 3 |
| Roof | 124 |
| SpawnMarker | 14 |
| StaticProp | 265 |
| Terrain | 1 |
| UtilityPole | 14 |
| VegetationBush | 7 |
| VegetationGrass | 3 |
| VegetationTree | 16 |
| Water | 12 |
| Window | 67 |
| Wire | 4 |
| **Всего** | **3 842** |

## Контекстная очистка

Первый dry run выявил ложные совпадения по подстрокам: character bone `shoulder` как `RoadShoulder`, mini-game content как terrain/ditch, store seat как water. Rules schema 2 добавила `hierarchyStartsWith`, required/excluded hierarchy context. Из reference layer исключены:

- `/COMPUTER/SYSTEM/`;
- `/RagDoll/`;
- `RALLY/Spectators/`;
- `RALLY/RallyCars/`;
- `STORE/Boxes/`;
- `MAP/CloudSystem/`.

Исключённые записи не потеряны: 9 667 geometric entities остаются в `M04A1_WorldEntities.csv` с `ReferenceWorldEligible=0`, cell `excluded` и статусом `ClassifiedNonWorld`.

## Очередь review

- 1 683 eligible records имеют `ExtractedBoundsNeedsReview`.
- `BuildingExterior` остаётся широкой fallback-категорией для геометрии location roots; её нужно дробить при production replacement.
- 395 `InteractivePropCandidate` требуют решения static/dynamic/interactive.
- Категорий `Unknown`, `RoadShoulder`, `Ditch`, `Shoreline`, `Culvert` и `Driveway` после контекстной очистки нет; отсутствие означает «не доказано правилами», а не доказанное отсутствие в мире.
- 37 serialized class IDs сохранены metadata-only.

Никакая запись не получила `ProductionReady`.
