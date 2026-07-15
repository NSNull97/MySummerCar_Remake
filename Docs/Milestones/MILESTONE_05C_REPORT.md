# Milestone 05C — Full Map Geometry Evaluation Baseline

Дата: 2026-07-15
Статус: **baseline завершён и вручную принят пользователем 2026-07-15**

## Выполненная граница

Выполнена только полная reference-only карта для оценки геометрии и расположения.
Игровые скрипты, NPC, экономика, survival, vehicle simulation, weather, audio,
save, финальные модели, текстуры и освещение не реализовывались.

Player и Interaction не изменялись. Production-префабы не зависят от donor
reference content.

## Что было исследовано

- `AGENTS.md` и `Prompts/05C_FULL_MAP_GEOMETRY_EVALUATION.md`;
- frozen database `04A1.1` и 13 509 geometry records;
- 3 842 `ReferenceWorldEligible` records в 49 concrete cells и global;
- normalized `WorldMeshManifest.csv`;
- 503 используемых mesh GUID;
- exact-hash AssetRipper `GAME.unity` и старый Unity static-batch контракт;
- существующий proxy-only generator и reference/build isolation.

## Что изменено

- В `WorldEntityTable` добавлено чтение source position/rotation/scale; quaternion
  нормализуется, неверный нулевой quaternion отклоняется.
- Синхронизатор копирует только 503 используемых `.asset` + `.meta` из frozen
  staging, проверяет SHA-256 пары и сохранение GUID. Donor installation не меняется.
- Генератор создаёт реальные MeshFilter/MeshRenderer для 2 784 записей и явные
  bounds proxies для остальных 1 058.
- Сцены разделены на `DONOR_REFERENCE_GEOMETRY` и
  `BOUNDS_AND_MISSING_PROXIES`; всё имеет tag `EditorOnly`.
- `Open Reference Overview` теперь открывает все 49 ячеек, global, persistent и
  bootstrap одной командой.
- Для 1 683 static-batched renderer извлечены `m_SubsetIndices`. Из combined mesh
  генерируется только принадлежащая объекту геометрия с восстановленным local TRS.
- Добавлены actual/fallback/structural режимы Scene View.
- Добавлены строгий 05C validator, automated captures и spatial-outlier audit.
- Генерация `1 683` производных mesh выполняется в пакетном режиме AssetDatabase;
  это устраняет refresh storm в batchmode и оставляет один финальный import.

## Ключевой найденный и исправленный дефект

Первая генерация назначала каждому static renderer весь AssetRipper combined
mesh. Автокадр показал повторяющиеся огромные треугольные формы по всей карте.
Анализ frozen `GAME.unity` подтвердил packed little-endian
`MeshRenderer.m_SubsetIndices`. После subset extraction общий контур карты,
дороги, озёра и контрольные зоны стали пространственно связными.

Это исправление относится только к reference-визуализации и не переносит donor
меши в production.

## Результаты

| Проверка | Результат |
|---|---|
| Reference mesh sync | PASS: requested/resolved/verified `503/503/503`, missing `0` |
| Static-batch metadata | PASS: `1 683` eligible subset records |
| Full generation | PASS: `3 842` entities, `49` cells |
| 05C strict validator | PASS: `2 784` actual, `1 058` fallback, `503` unique meshes |
| Parser transform EditMode test | PASS: `1/1` |
| 05C focused EditMode tests | PASS: `2/2` (`GeometryEvaluation05C` filter; `TestResults/M05C_EditMode_final.xml`) |
| Automated captures | PASS: overhead, fallback map и пять structural landmark views |
| Spatial audit | PASS: non-finite/out-of-envelope critical findings `0`; manual candidates `40` |
| Manual Unity visual review | PASS: пользователь сообщил, что ручная проверка дала хороший результат |

## Артефакты

Tracked:

- `Prompts/05C_FULL_MAP_GEOMETRY_EVALUATION.md`;
- `Assets/Game/World/Content/WorldTransfer/M05C_StaticBatchSubsets.csv`;
- world-transfer runtime/editor code и EditMode tests;
- `Docs/WorldTransfer/M05C_GEOMETRY_EVALUATION_COVERAGE.md`;
- `Docs/WorldTransfer/M05C_GEOMETRY_DEFECTS.csv`;
- `Docs/WorldTransfer/M05C_INSPECTION_CHECKLIST.md`;
- этот отчёт.

Ignored/removable:

- `Assets/Game/LegacyImport/ReferenceOnly/World/MeshLibrary/`;
- `Assets/Game/LegacyImport/ReferenceOnly/World/Generated/`;
- `PerformanceCaptures/Milestone05C/`;
- `Logs/M05C_*`.

## Ограничения и риски

- 2 835 degenerate source bounds не позволяют доказать точность каждой детали
  только числами.
- 1 058 объектов не имеют пригодного mesh reference.
- 40 крупных aggregate/pivot candidates были включены в ручной sign-off; их
  числовая интерпретация всё ещё ограничена качеством donor bounds.
- Материалы категорий показывают форму, но не финальный внешний вид.
- All-cells overview предназначен для Editor inspection; FPS/GPU budget и build
  runtime не заявлены.
- Намеренные donor-пустоты за ограничивающими tree-wall плоскостями, в том числе
  около магазина Теймо, подтверждены как исходная топология, а не дефект 05C.

Входной документ `Docs/WorldTransfer/WORLD_TRANSFER_VALIDATION.md`, указанный
в промпте как исторический supporting doc, в текущем репозитории отсутствует.
Вместо него использованы существующие `WorldTransferValidator`,
`WORLD_FIDELITY_REPORT.md`, `WORLD_COMPLETENESS_REPORT.md` и frozen manifests;
это документационный blocker, не причина пропуска геометрической валидации.

## Ручные действия

Ручной sign-off завершён. Отдельно зарегистрирована намеренная donor-пустота у
Теймо; её project-owned safety topology вынесена в bounded 05C1 и не считается
исправлением reference-only карты.

## Рекомендуемый следующий milestone

Ровно один следующий шаг: **05C1 — Continuous Ground and Collision Baseline** —
bounded project-owned safety fill только для подтверждённой пустоты у Теймо.
Игровые системы, финальный art и более широкое заполнение карты не открываются.
