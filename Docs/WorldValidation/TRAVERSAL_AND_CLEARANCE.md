# Traversal and clearance — Milestone 05B

Дата: 2026-07-15
Итог: **bounded fixtures PASS; formal PilotGate traversal incomplete**.

## Автоматически подтверждено

- `cell_0_-3` загружается вместе с M4 player prefab.
- Garage moving architecture открывается, сохраняя clearance проёма.
- Home-to-pier collision route проверяется raycast sample через каждый 1 m на
  отрезке приблизительно 76 m.
- Home/pier overlap равен 3.75 m при required minimum 1.0 m.
- Максимальный gap между соседними pier colliders равен 0.08 m при limit 0.10 m.
- 05C1 void-fill даёт 100/100 collision hits; seam между двумя cells имеет 26
  совпадающих пар вершин и 0 m deviation.
- Production cell load/reload не создаёт duplicate stable IDs.

Focused WorldRemaster PlayMode: **9/9 PASS**. Полная PlayMode-регрессия:
**26/26 PASS**.

## Ручное evidence

Пользователь в предыдущих проверках подтвердил:

- работающие garage door/gate interactions;
- наличие воды и изгороди в Batch 01;
- отсутствие провала в принятом 05C1 Teimo safety baseline;
- приемлемый bounded world result.

Pilot metadata теперь честно записывает:
`DoorGatePass;LightingReadabilityLow;FullTraversalPending;PerformancePending`.
Это не превращает частичную ручную проверку в полный traversal pass.

## Не выполнено

- Полный deterministic маршрут настоящим M4 `CharacterController` через exterior,
  garage, representative interior и обратно.
- Проверка floors, stairs/ramps и всех portal transitions реальным controller.
- Vehicle-width swept clearance по непрерывному route.
- Actual vehicle traversal: production `Road` bindings равны 0/29, а vehicle
  simulation относится к будущему milestone.
- Bridge/driveway transitions полной карты.
- Collision layer/PhysicMaterial classification: текущие production objects в
  основном используют `Default` и null PhysicMaterial.
- Автоматический поиск всех invisible walls и terrain holes за пределами bounded
  fixtures.

## Issues

- `WORLD-COL-002` — реальный player traversal; блокирует PilotGate.
- `WORLD-COL-001` — нет continuous driveable production route.
- `WORLD-COL-003` — не определена surface/layer/material policy.
- `WORLD-ROAD-001` — road/junction production coverage отсутствует.

Следующий bounded fixture должен использовать существующую M4 architecture без
её переписывания и проходить по сериализованному списку checkpoints. Для vehicle
clearance до Milestone 06 допустим только геометрический swept volume, без ложного
заявления об actual driving.
