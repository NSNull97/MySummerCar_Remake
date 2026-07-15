# Streaming validation — Milestone 05B

Дата: 2026-07-15
Итог: **cell lifecycle PASS; production streaming wiring FAIL**.

## Production cells

На текущем baseline существуют две production-bound cells:

- `cell_0_-3` — home/garage pilot;
- `cell_0_-2` — home shoreline/pier Batch 01.

Детерминированная генерация и структурный validator проходят. В focused PlayMode
suite каждая cell тестируется из `Bootstrap`, а не поверх playtest scene:

1. additive load;
2. проверка единственного cell root, marker count и точного runtime stable-ID set;
3. unload и отсутствие orphan root;
4. повторный load;
5. повторная проверка того же точного stable-ID set;
6. повторный unload.

Оба lifecycle-теста прошли. Весь focused PlayMode набор: **9/9 PASS**.

Точные snapshots:

- `cell_0_-3`: 7 runtime stable IDs, `MappedReferenceRecordCount=24`;
- `cell_0_-2`: 8 runtime stable IDs, `MappedReferenceRecordCount=9`.

## Исправленный дефект fixture

Старые тесты открывали playtest scene, уже содержащую проверяемую зону, а затем
additive загружали ту же production cell. Это создавало вторую копию renderers,
colliders и stable IDs, но тест проверял лишь наличие объектов. В 05B fixture
переведён на чистый `Bootstrap`, точные ID sets и два load/unload цикла.
`WORLD-STREAM-002` закрыт.

## Формальный blocker

В runtime отсутствует подключённый production streaming service:

- `IWorldStreamingService` не установлен composition root’ом;
- focus-driven cell selection/load policy отсутствует;
- `WorldReferenceCellLoader` является runtime-типом; его единственный
  сгенерированный reference-only экземпляр disabled и помечен `EditorOnly`;
- production scenes/prefabs не содержат активного loader.

Поэтому прямой вызов `SceneManager.LoadSceneAsync` в тесте доказывает корректный
lifecycle scene, но не production streaming. `WORLD-STREAM-001` остаётся blocker
для всех трёх gate.

## Непроверенные streaming contracts

- cross-cell production roads и water;
- large-object ownership;
- interiors и persistent landmarks;
- runtime state persistence между unload/reload;
- memory recovery и допустимый load/unload stall;
- full route focus transitions;
- включение двух 05C1 safety cells в production policy.

Соответствующие открытые finding’и: `WORLD-STREAM-003`, `WORLD-STREAM-004` и
`WORLD-STREAM-005`.

## Следующая проверка

После bounded production-streamer wiring повторить те же lifecycle assertions
через публичную service boundary, добавить focus crossing между `cell_0_-3` и
`cell_0_-2`, измерить peak/recovered memory и worst-frame load/unload spike.
