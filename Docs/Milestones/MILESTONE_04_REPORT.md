# Отчёт Milestone 04 — Player and Physical Interaction

Дата: 2026-07-14

Результат: **пройден**. Реализован самостоятельный first-person physical-interaction prototype на Unity Input System. Полный EditMode suite проходит `45/45`, PlayMode boot — `1/1`, M4 project validation проходит.

## 1. Что было проверено

- Прочитаны `AGENTS.md`, prompt M4, отчёты Milestone 0–3, `Docs/PLAYER_INTERACTION.md` и `Docs/ARCHITECTURE.md`.
- Проверены текущие Core/Bootstrap/Interaction/Player asmdef, stable-ID contracts, Input System `1.19.0`, build scenes и существующий `InputSystem_Actions.inputactions`.
- Существующие пользовательские изменения и временно открытая несохранённая Garage scene не закрывались и не перезаписывались автоматикой.
- Donor installation, staged extraction и decompiled reference для M4 не читались и не изменялись.

## 2. Реализованный scope

- First-person CharacterController movement, gravity, mouse/gamepad look и hold-to-crouch с headroom check.
- Отдельный M4 `InputActionAsset` с девятью actions и keyboard/mouse/gamepad bindings.
- Bounded raycast candidate query и явный `InteractionTargetHost` без object-name dispatch.
- Explicit capabilities: pickup, contextual interaction, tool activation и mount handoff.
- Physical Rigidbody carry без parenting к камере; collision-state preservation, controlled following и separation release.
- Drop, surface placement с overlap rejection, throw impulse и rotation target.
- Stable-ID-compatible carried-object snapshot schema version 1.
- Debug crosshair/prompt/held-ID overlay и ray gizmo.
- Player prefab и отдельная `PlayerInteractionPrototype.unity` scene с light/heavy pickup, context, tool и mount examples.
- M4 Editor builder и validator.

Не реализованы full-body IK, inventory, vehicle assembly graph, NPC, save storage или vehicle simulation.

## 3. Архитектура

Input routing, movement/look, query, carry state и target behavior разделены. `PlayerInputRouter` передаёт intent узким владельцам. `PlayerInteractionController` выбирает capability текущего кандидата. `PhysicalCarryController` является единственным владельцем held-object physics state.

`IMountHandoffTarget` — только граница передачи уже переносимого объекта. Compatibility, mount constraints, fasteners и installed state принадлежат M5.

`CarriedObjectSaveState` содержит schema version, presence flag, canonical stable ID и transform относительно carry anchor. Будущий Save layer должен разрешать ID через `IEntityIdProvider`; scene lookup и прямое сохранение GameObject не допускаются.

## 4. Основные созданные и изменённые файлы

Runtime Interaction:

- `Assets/Game/Interaction/Runtime/Capabilities/*.cs`;
- `Assets/Game/Interaction/Runtime/Query/*.cs`;
- `Assets/Game/Interaction/Runtime/Carrying/*.cs`;
- `Assets/Game/Interaction/Runtime/Prototype/*.cs`;
- `Assets/Game/Interaction/Runtime/InteractionContext.cs`.

Runtime Player:

- `Assets/Game/Player/Runtime/FirstPersonMotor.cs`;
- `Assets/Game/Player/Runtime/FirstPersonLook.cs`;
- `Assets/Game/Player/Runtime/PlayerInputRouter.cs`;
- `Assets/Game/Player/Runtime/PlayerInteractionController.cs`;
- `Assets/Game/Player/Runtime/InteractionDebugOverlay.cs`;
- `Assets/Game/Player/Runtime/PlayerInteractionPrototypeMarker.cs`.

Content/Editor/tests:

- `Assets/Game/Player/Content/Input/M4_Player.inputactions`;
- `Assets/Game/Player/Content/Prefabs/M4_FirstPersonPlayer.prefab`;
- `Assets/Game/Player/Content/Scenes/PlayerInteractionPrototype.unity`;
- `Assets/Game/Player/Content/Materials/M4_InteractionDebug.mat`;
- `Assets/Game/Editor/PlayerInteraction/*.cs`;
- `Assets/Game/Tests/EditMode/PlayerInteraction/PlayerInteractionRuntimeTests.cs`;
- `Assets/Game/Tests/PlayMode/PlayerInteraction/PlayerInteractionBootTests.cs`;
- Player, Editor, EditMode и PlayMode asmdef references;
- `ProjectSettings/EditorBuildSettings.asset`.

Документация:

- `Docs/PLAYER_INTERACTION.md`;
- `Docs/SAVE_SYSTEM.md`;
- `Docs/ARCHITECTURE.md`;
- `Docs/TESTING_AND_VALIDATION.md`;
- `Docs/ROADMAP.md`;
- `Docs/Porting/DONOR_AUDIT.md`;
- `Docs/Porting/PORTING_MATRIX.md`;
- `Docs/Porting/SYSTEM_MAP.md`;
- `Docs/Architecture/CURRENT_REPOSITORY_STATE.md`;
- этот отчёт.

## 5. Команды и фактические результаты

| Проверка | Результат |
|---|---|
| Runtime/Player `dotnet build` diagnostic | успешно, 0 warnings/errors |
| Editor/EditMode/PlayMode `dotnet build` diagnostic | успешно, 0 warnings/errors; временная generated-csproj reference удалена после проверки |
| `PlayerInteractionPrototypeBuilder.RunBatch` | exit `0`; `M4_PLAYER_INTERACTION_BUILD_OK` |
| `PlayerInteractionPrototypeValidator.RunBatch` | exit `0`; `M4_PLAYER_INTERACTION_VALIDATION_OK` |
| Unity EditMode Test Runner | `45 total`, `45 passed`, `0 failed`, `0 skipped` |
| Unity PlayMode Test Runner | `1 total`, `1 passed`, `0 failed`, `0 skipped` |
| Foundation validation | passed; stable IDs, asmdef boundaries, paths и Bootstrap valid |
| Donor pipeline validation | passed, `0` warnings |
| Garage M3 validation | passed; `M3_GARAGE_VALIDATION_OK` |

Обязательные тестовые сценарии покрыты:

- pickup/drop round trip и восстановление physics state;
- уничтожение/потеря target;
- collider без capability host как invalid target;
- carried-object mount handoff;
- stable-ID carried snapshot JSON round trip;
- реальная additive загрузка и выгрузка prototype scene в PlayMode.

## 6. Управление

| Действие | Keyboard/mouse | Gamepad |
|---|---|---|
| Move/look | `WASD`, mouse | left/right stick |
| Crouch | hold `Left Ctrl` | hold right-stick press |
| Interact/pickup/handoff | `E` | west button |
| Drop | `G` | east button |
| Place | `F` | south button |
| Throw | left mouse | right trigger |
| Rotate held | right mouse + mouse | left trigger + right stick |
| Tool activation | `R` | north button |

## 7. Ручная Unity-проверка

Автоматические exit-gate checks пройдены. Для feel/collision review:

1. Открыть `Assets/Game/Player/Content/Scenes/PlayerInteractionPrototype.unity`.
2. Нажать Play и проверить movement/look/crouch и headroom recovery.
3. Поднять объекты 3 кг и 18 кг, затем выполнить drop, place, throw и rotate рядом с полом/стеной.
4. Проверить context target (`E`), tool target (`R`) и передачу held object в mount (`E`).
5. Убедиться, что debug overlay показывает candidate/prompt/stable ID, а Rigidbody не становится child камеры.

## 8. Ограничения и риски

1. Movement/carry tuning проектное и не откалибровано по donor behavior.
2. Нет jump/sprint, step audio, full body, hands или IK.
3. Placement использует bounds approximation; сложные concave формы потребуют shape-aware probe.
4. Tool target — минимальный capability proof без tool definition/inventory.
5. Prototype mount не проверяет compatibility и не поддерживает unmount.
6. Snapshot capture не является полноценным save/load round trip.
7. Debug overlay использует IMGUI и предназначен только для разработки.
8. M4 scene использует project-owned M3 neutral lighting prefab.

## 9. Exit gate

- [x] Movement/look/crouch и Input System actions реализованы.
- [x] Candidate query и contextual capability boundary реализованы без name dispatch.
- [x] Pickup/carry/place/drop/throw/rotate реализованы.
- [x] Basic tool activation и mount handoff boundary реализованы.
- [x] Stable-ID carried snapshot design существует и тестируется.
- [x] Debug visualization присутствует.
- [x] Обязательные EditMode scenarios проходят.
- [x] Basic PlayMode boot проходит.
- [x] Donor runtime/code/data не использовались.

## 10. Рекомендуемый следующий milestone

Выполнить ровно **Milestone 5 — Vehicle Assembly**: `PartDefinition`/`PartInstance`, mount points, fasteners/tools, assembly graph, representative parts и save round trip, используя M4 mount handoff boundary без реализации vehicle simulation.
