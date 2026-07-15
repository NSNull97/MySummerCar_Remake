# Milestone 05C1 — Ограниченный baseline непрерывной земли и коллизии у Теймо

Дата: 2026-07-15

Статус: **завершён и вручную принят пользователем 2026-07-15**

## Выполненная граница

05C1 ограничен одной подтверждённой пустотой donor-карты за визуальной лесной
границей около магазина Теймо:

- region: `M05C1-VOID-TEIMO-001`;
- classification: `IntentionalDonorVoid`;
- cells: `cell_-4_0`, `cell_-3_0`;
- AABB: `x=[-1742, -1447.7]`, `z=[100, 225]`;
- cell seam: `x=-1536`.

Создаваемая поверхность является project-authored safety/topology baseline.
Она не открывает новую поддерживаемую gameplay-зону и не объявляется полным
terrain remaster.

## Реализация

- Добавлен machine-readable реестр двух частей с project-owned stable IDs,
  station-профилями, cell ownership и SHA-256 authoring fingerprints.
- Западная часть `piece_cell_-4_0` имеет stable ID
  `9c32cc25615de19b4d8ce23a2ade1b5b`; восточная
  `piece_cell_-3_0` — `a708a95ca385ee7e5dbdf56bf2d26eca`.
- Builder `05C1.1` создаёт отдельный low-detail mesh и generated-сцену для
  каждой ячейки.
- World coordinates преобразуются в cell-local vertices; один mesh не
  пересекает две streaming cells.
- Используется существующий project-owned `WR_Terrain.mat` без donor texture.
- На каждой части создаётся статический non-convex `MeshCollider` без
  `Rigidbody`.
- `WorldVoidFillMarker` хранит region/piece/cell IDs, classification,
  fingerprint и boundary evidence.
- Marker явно фиксирует `SafetyTopologyBaseline=true` и
  `OpensGameplayArea=false`.
- Добавлены Editor commands для rebuild, validation, совместного review и
  reference evidence captures.
- Reference-only 05C geometry остаётся отдельной, removable и не становится
  runtime dependency.
- Generated void-fill scenes остаются вне обычных Build Settings до ручной
  приёмки и 05B integration gate.

## Что намеренно не выполнялось

- другие пустоты и внешняя область полной карты;
- удаление tree-card boundary или создание нового леса;
- terrain art, terrain layers, vegetation, LOD/HLOD;
- дороги, здания, магазин, вода и shoreline;
- Player/Interaction, NPC, traffic, quests и другие gameplay systems;
- vehicle assembly/simulation;
- lighting, weather, audio, UI и save;
- 05A continuation, 05B и Milestone 06.

## Артефакты 05C1

- `Prompts/05C1_CONTINUOUS_GROUND_AND_COLLISION_BASELINE.md`;
- `Docs/WorldRemaster/M05C1_DONOR_VOID_REGIONS.csv`;
- `Docs/WorldRemaster/M05C1_CONTINUOUS_GROUND_COVERAGE.md`;
- `Docs/WorldRemaster/M05C1_INSPECTION_CHECKLIST.md`;
- этот отчёт;
- `Assets/Game/Editor/WorldTransfer/WorldContinuousGroundBaselineBuilder.cs`;
- `Assets/Game/Editor/WorldTransfer/WorldContinuousGroundBaselineValidator.cs`;
- `Assets/Game/Editor/WorldTransfer/WorldContinuousGroundBaselineCapture.cs`;
- `Assets/Game/World/Production/Runtime/WorldVoidFillMarker.cs`;
- `Assets/Game/Tests/EditMode/WorldRemaster/WorldContinuousGroundBaselineTests.cs`;
- `Assets/Game/Tests/PlayMode/WorldRemaster/WorldContinuousGroundBaselinePlayModeTests.cs`;
- минимальная assembly reference в `Assets/Game/Editor/MSC.Editor.asmdef`;
- generated meshes:
  `Assets/Game/World/Production/Terrain/VoidFill/M05C1_TeimoVoidFill_cell_-4_0.asset`
  и `M05C1_TeimoVoidFill_cell_-3_0.asset`;
- generated scenes:
  `Assets/Game/World/Generated/VoidFillCells/VoidFill_cell_-4_0.unity`
  и `VoidFill_cell_-3_0.unity`;
- локальные captures, создаваемые командами Unity.

README, roadmap, 05C sign-off и provenance ledgers обновляются только в части,
необходимой для фиксации 05C1 и следующего gate; документы игровых систем не
затрагиваются.

## Проверки

| Проверка | Результат |
|---|---|
| Unity compile/import | `PASS`, ошибок C# и новых предупреждений C# нет |
| Два последовательных 05C1 rebuild | `PASS`: `regions=1 pieces=2 cells=2` |
| `WorldContinuousGroundBaselineValidator.RunBatch` после обоих rebuild | `PASS`, token `WORLD_MAP_05C1_VALID` |
| Region/piece/cell count `1/2/2` | `PASS` |
| Geometry count `104` vertices / `100` triangles | `PASS` |
| Authoring fingerprints | `PASS` |
| Geometry finiteness and cell ownership | `PASS` |
| Seam geometry `26` pairs at `x=-1536`, tolerance `0.0001 m` | `PASS` |
| Static collision probes `100/100`, point tolerance `0.02 m` | `PASS` |
| Maximum slope `<=15°` | `PASS`: observed `2.582°` |
| Donor/reference dependency isolation | `PASS`, нарушений `0` |
| Void-fill scenes excluded from normal Build Settings | `PASS`, намеренно до 05B |
| Focused EditMode `WorldContinuousGroundBaselineTests` | `PASS 3/3` |
| Focused PlayMode `WorldContinuousGroundBaselinePlayModeTests` | `PASS 1/1` |
| Reference/fill captures | `PASS`: создано `5` PNG |
| Manual Scene View/collision review | `PASS`: пользователь подтвердил «пока ок» |

Оба последовательных in-place rebuild дали одинаковые counts, seam и
collision results. Побитовое сравнение четырёх generated-файлов между
rebuild подтвердило полную повторяемость.

## Deterministic SHA-256

| Generated asset | SHA-256 после обоих rebuild |
|---|---|
| `VoidFill_cell_-4_0.unity` | `B88F96709951391CFD0CA723AED1F3B9F7378334964F60DD01B072CEAC42D7E4` |
| `VoidFill_cell_-3_0.unity` | `947C9B00DA08D6DF8A854EB10BE585BC314C1CDF1C9F1CCE6C9849F3C035C771` |
| `M05C1_TeimoVoidFill_cell_-4_0.asset` | `B1CF45E55736DD7463C7E3137D31345CB310BE4F78FF67E9C87C254094041932` |
| `M05C1_TeimoVoidFill_cell_-3_0.asset` | `2247F4A67CEA452534A9A36EE2618FE69D3E42C596064DAA13754E880A00DBCF` |

Test results сохранены локально в:

- `TestResults/M05C1_EditMode.xml` — `3/3 PASS`;
- `TestResults/M05C1_PlayMode.xml` — `1/1 PASS`.

Пять локальных reference/fill captures созданы под
`PerformanceCaptures/Milestone05C1/`. Их наличие и генерация подтверждены;
визуальная приёмка содержимого остаётся ручным gate.

## Выполненные команды

- Unity batch `WorldContinuousGroundBaselineBuilder.RunBatch` — initial build,
  затем два последовательных in-place rebuild;
- Unity batch `WorldContinuousGroundBaselineValidator.RunBatch` — финальная
  отдельная проверка;
- Unity Test Framework EditMode с filter
  `MSC.Tests.EditMode.WorldRemaster.WorldContinuousGroundBaselineTests`;
- Unity Test Framework PlayMode с filter
  `MSC.Tests.PlayMode.WorldRemaster.WorldContinuousGroundBaselinePlayModeTests`;
- Unity batch `WorldContinuousGroundBaselineCapture.RunReviewBatch`;
- SHA-256 сравнение четырёх generated outputs, CSV column-count validation,
  Build Settings exclusion check и `git diff --check`.

Логи и test XML находятся в ignored локальных `Logs/` и `TestResults/`; они не
являются runtime- или source-control dependencies.

## Ручная проверка

Выполнить
`Docs/WorldRemaster/M05C1_INSPECTION_CHECKLIST.md` через команды меню 05C1.
Нужно подтвердить:

- непрерывность поверхности за границей у Теймо;
- отсутствие видимого и collision seam на `x=-1536`;
- отсутствие перекрытия дороги, магазина и воды;
- сохранение исходной tree-card boundary;
- отсутствие провала сквозь обе части.

Пользователь подтвердил визуальную непрерывность, коллизию и bounded scope
формулировкой «подтверждаю, пока ок». Manual gate 05C1 закрыт; это не меняет
статус baseline на final terrain art.

## Ограничения и риски

- Форма за donor boundary является явно документированной project-authored
  интерполяцией; это не измеренный исходный рельеф.
- Пилот доказывает подход только для одного региона и не разрешает массовое
  заполнение остальных дыр.
- Старые визуальные стены сохраняются; их художественная замена относится к
  будущей графике.
- Generated fill не означает FullWorldGate и не меняет 05A production
  replacement coverage.
- Runtime streaming полной production-карты этим этапом не валидируется.

## Следующий milestone

Автоматические проверки и ручное принятие 05C1 завершены. Ровно один следующий
этап: `Prompts/05B_WORLD_VALIDATION.md`.

Не продолжать другие map batches и не переходить к Milestone 06 до завершения
05B validation gate.
