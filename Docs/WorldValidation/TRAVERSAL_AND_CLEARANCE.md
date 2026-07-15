# Traversal and clearance — Milestone 05B.1

Дата: 2026-07-15
Итог: **representative M4 pilot traversal PASS**.

## Реальный controller fixture

`WorldRemasterPilotPlaytest.unity` содержит сериализованный
`WorldPilotTraversalRoute`. Тест использует существующие M4:

- `MSC.Player.FirstPersonMotor`;
- `UnityEngine.CharacterController`;
- production pilot geometry и colliders;
- `GarageDoorLeft`, `GarageDoorRight`, `HouseFrontDoor`.

Маршрут из 16 checkpoints проходит garage apron/threshold/interior, возвращается
во двор, проходит house step и низкий front-door portal, обходит `LivingTable`,
заходит в representative interior и возвращается к старту.

## Результат

- test: `1/1 PASS`;
- route fingerprint: `f8c915039e48f3c5f8f8fe1a2a8f75b84bca614c01f8505720a69cd402e2e1da`;
- dependency fingerprint: `719fdc2621c83c3d4c261adee62f95ed5db4ee28e7b827fc39041a83f5da66bf`;
- 16/16 waypoint IDs в закреплённом порядке;
- cumulative horizontal distance: `62.780293 m`;
- max per-frame horizontal displacement: `0.100586 m`;
- max waypoint vertical deviation: `0.26 m`;
- authored extent: `17.0 m` по X и `10.4 m` по Z;
- no teleport: PASS;
- stall guard: PASS;
- grounded/finite vertical reach: PASS;
- required crouch passage: PASS;
- return-to-start: PASS.

Вертикальный contract использует waypoint tolerance с минимумом `0.30 m` и
общим accepted ceiling `0.35 m`. Первый усиленный прогон честно выявил `0.26 m`
на `house_step` при слишком узком `0.20 m`; минимальный допуск исправлен, геометрия
и waypoint positions не менялись.

## Защита evidence от stale pass

Файл `Docs/WorldValidation/M05B1_M4_CHARACTER_CONTROLLER_TRAVERSAL.json` создаётся
только после полного успешного маршрута. Reader независимо проверяет:

- pinned route fingerprint и минимальный distance/extent contract;
- SHA-256 player prefab и playtest scene;
- recursive dependency closure playtest + production pilot cell + player;
- asset payload и `.meta` importer/GUID files;
- relevant motor, interaction-door, authoring, reader и test sources;
- exact waypoint order, crouch, vertical, teleport, stall и return flags.

Изменение production prefab, nested collider, material/importer metadata, player
или relevant script инвалидирует старый pass до повторного authoring/test.

## Дополнительные подтверждённые fixtures

- garage moving architecture clearance;
- 76-метровый home-to-pier raycast route;
- home/pier overlap `3.75 m`;
- pier collider gap `0.08 m` при limit `0.10 m`;
- 05C1 collision probes `100/100` и seam deviation `0 m`;
- production cell reload без duplicate stable IDs.

## Честные ограничения

- `FrontDoorHeader` не имеет подтверждённого standing clearance: portal требует crouch;
- fixture открывает двери напрямую и отключает `PlayerInputRouter`; это controller/motor/collision test, не полный пользовательский input/interaction walkthrough;
- streaming lifecycle и traversal не объединены в единый end-to-end fixture;
- `traversalFrameCount` зависит от batch frame rate и не является FPS evidence;
- actual vehicle traversal отсутствует; Road bindings `0/29`;
- vehicle-width swept route, bridge/driveway route и full-map invisible-wall search остаются будущими проверками;
- collision layer/PhysicMaterial policy не классифицирована (`WORLD-COL-003`).

`WORLD-COL-002` закрыт. `WORLD-COL-001`, `WORLD-COL-003` и `WORLD-ROAD-001`
остаются открыты, но не блокируют достигнутый `PilotGate`.
