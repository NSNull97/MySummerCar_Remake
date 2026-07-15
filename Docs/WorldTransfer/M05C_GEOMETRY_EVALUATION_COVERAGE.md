# M05C — покрытие геометрии полной карты

## Назначение

Этот baseline предназначен только для оценки внешнего вида геометрии, масштаба,
поворотов, взаимного расположения и крупных пространственных дефектов карты. Это
не production-art и не перенос игровых систем.

Источник зафиксирован на базе `04A1.1`:

- donor `level2` SHA-256: `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31`;
- frozen AssetRipper `GAME.unity` SHA-256: `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- генератор reference-сцен: `2.1.0-05C`;
- Unity: `6000.3.11f1`.

Текущая переустановленная donor-игра не используется как источник 05C и не
перезаписывает историческую provenance.

## Итоговое покрытие

| Метрика | Результат |
|---|---:|
| `ReferenceWorldEligible` записей | 3 842 |
| Реальные mesh-представления | 2 784 |
| Bounds-fallback | 1 058 |
| Уникальные donor mesh GUID | 503 |
| Объекты старого static batching | 1 683 |
| Сгенерированные изолированные static-batch subset meshes | 1 683 |
| Concrete partition cells | 49 |
| Global layer | 1 |
| Уникальные stable IDs в сценах | 3 842 |
| Production/build dependencies на ReferenceOnly | 0 |

Каждая запись представлена ровно один раз: реальным мешем либо явно отмеченным
fallback-кубом. Ничего не отбрасывается молча.

## Покрытие по категориям

| Категория | Всего | Mesh | Fallback |
|---|---:|---:|---:|
| BuildingExterior | 1 568 | 1 426 | 142 |
| ColliderOnly | 772 | 0 | 772 |
| InteractivePropCandidate | 395 | 302 | 93 |
| StaticProp | 265 | 260 | 5 |
| BuildingInterior | 211 | 198 | 13 |
| RoadSign | 163 | 163 | 0 |
| Roof | 124 | 124 | 0 |
| Door | 120 | 118 | 2 |
| Window | 67 | 63 | 4 |
| Road | 29 | 29 | 0 |
| Floor | 23 | 20 | 3 |
| Fence | 18 | 18 | 0 |
| VegetationTree | 16 | 11 | 5 |
| SpawnMarker | 14 | 0 | 14 |
| UtilityPole | 14 | 14 | 0 |
| Water | 12 | 10 | 2 |
| Landmark | 9 | 6 | 3 |
| VegetationBush | 7 | 7 | 0 |
| Wire | 4 | 4 | 0 |
| VegetationGrass | 3 | 3 | 0 |
| Rock | 3 | 3 | 0 |
| Field | 2 | 2 | 0 |
| Bridge | 2 | 2 | 0 |
| Terrain | 1 | 1 | 0 |

## Static batching

AssetRipper-сцена содержит combined meshes старого Unity static batching. Простое
назначение такого меша каждому renderer повторяет целый пакет карты и создаёт
огромные «шипы». Для 1 683 eligible renderer прочитаны исходные
`m_SubsetIndices`; результат хранится как project-owned metadata в
`Assets/Game/World/Content/WorldTransfer/M05C_StaticBatchSubsets.csv`.

Для каждого такого объекта генератор:

1. выбирает только принадлежащие объекту submeshes;
2. уплотняет используемый vertex buffer;
3. обратным source TRS восстанавливает локальные вершины;
4. применяет converted position и исходные rotation/scale в reference-сцене;
5. сохраняет производный меш только в игнорируемом `ReferenceOnly/World/Generated`.

Garage fixture `fb0f962be1b325cc19296c66751818c0` использует submesh `80` и
проверяется отдельным EditMode-тестом: итоговые world-bounds совпадают с
исходными вершинами static batch после coordinate offset с допуском `0.01 m`.

## Ограничения достоверности

- У 2 835 записей исходные renderer-bounds точечные или вырожденные. Поэтому
  индивидуальная автоматическая fit-проверка по bounds для них невозможна.
- 1 058 fallback-записей позволяют оценивать положение, но не внешний вид.
- Материалы категорий нейтральные; donor textures/materials не импортированы.
- 40 крупных renderer-bounds сохранены в
  `PerformanceCaptures/Milestone05C/spatial_outlier_candidates.csv` для ручного
  просмотра. Это в основном агрегаты `MAP/MESH`; автоматическое одобрение не дано.
- Ручное визуальное подтверждение пользователь выполнил 2026-07-15 и сообщил о
  хорошем результате. Пустоты за donor tree-wall границами, включая участок у
  Теймо, приняты как намеренная исходная топология; bounded safety-fill ведётся
  отдельно в 05C1 и не меняет достоверность reference-only карты.

## Автоматические кадры

Игнорируемые артефакты находятся в `PerformanceCaptures/Milestone05C/`:

- `FullMap_ActualMeshes.png`;
- `FullMap_BoundsFallbacks.png`;
- `HomeGarage_StructuralReview.png`;
- `TownChurch_StructuralReview.png`;
- `RepairWorkshop_StructuralReview.png`;
- `IslandCottage_StructuralReview.png`;
- `HighwayBridge_StructuralReview.png`;
- `capture_manifest.csv`;
- `spatial_outlier_candidates.csv`.
