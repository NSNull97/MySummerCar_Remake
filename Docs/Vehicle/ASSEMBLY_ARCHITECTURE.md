# Архитектура сборки автомобиля — Milestone 05

## Граница подсистемы

`MSC.Vehicle.Assembly` — самостоятельная runtime-сборка, зависящая только от `MSC.Core.Runtime` и `MSC.Interaction.Runtime`. Она не ссылается на Editor API, donor assemblies, PlayMaker, файловую систему или будущую симуляцию автомобиля.

Ответственность разделена так:

- M4 `PlayerInteractionController` формирует намерение и вызывает существующие `IPickupTarget`, `IMountHandoffTarget`, `IToolActivationTarget` и `IContextInteractionTarget`;
- `PhysicalCarryController` переносит Rigidbody и передаёт удерживаемую деталь через handoff;
- `VehicleAssemblyController` проверяет совместимость, граф, допуски, препятствия, крепёж и применяет атомарную операцию;
- `PartInstance` применяет безопасный физический переход между loose/installed состояниями;
- `AssemblyMountPreviewPresenter` отображает лучший детерминированный кандидат зелёным или красным prototype ghost;
- `VehicleAssemblySaveData` хранит versioned DTO, но не владеет диском, слотами сохранений или глобальным разрешением сущностей.

## Неизменяемые определения

ScriptableObject-данные находятся в `Assets/Game/Vehicle/Content/Assembly/Definitions/`:

- `PartDefinition`: project-owned ID, отображаемое имя, категория, масса, clean prototype prefab и правила совместимости;
- `MountPointDefinition`: тип socket, владелец, допустимые part IDs, `MountConstraint`, reference marker и набор крепежей;
- `FastenerDefinition`: project-owned ID, размер, направление, число дискретных стадий, insert/removal semantics и правило инструмента;
- `ToolDefinition`: тип и размер инструмента.

Определения не содержат mutable installation state. Машинно-зависимые пути и donor object names в них отсутствуют.

## Изменяемое состояние

- `PartRuntimeState` хранит stable entity ID, definition ID, lifecycle, mount ID и loose world pose.
- `MountPointRuntime` хранит занятую деталь и принадлежащие точке `FastenerInstance`.
- `FastenerInstance` хранит inserted/seated flags и целочисленную стадию.
- `AssemblyGraph` владеет зарегистрированными instances, mounts и явными `AssemblyDependency`.

Состояния крепежа: `Absent`, `Inserted`, `Loose`, `PartiallyTightened`, `Tightened`. Физическая резьба, torque и непрерывный угол не моделируются.

## Операции и инварианты

`AssemblyOperation` поддерживает install, insert, tighten, loosen, remove и restore. Каждый запрос возвращает `AssemblyOperationResult` с результатом и `AssemblyFailureReason`; silent fallback отсутствует.

Установка разрешена только если:

1. part и mount имеют валидные project-owned IDs/definitions;
2. деталь loose, а mount свободен;
3. `PartCompatibilityRule` и allow-list mount совпадают;
4. владелец mount и install prerequisites установлены;
5. соблюдены position/orientation tolerances;
6. зона не перекрыта `AssemblyMountObstruction`;
7. mount является детерминированным победителем: сначала валидность, затем distance, затем ordinal mount ID.

После успешной установки деталь становится дочерней `MountPose`, получает его world position/rotation, но сохраняет свой world scale: масштаб authoring/debug-маркера не может уменьшить физическую деталь. Rigidbody переводится в `isKinematic=true`, gravity выключается, скорости обнуляются до перехода. После снятия восстанавливаются dynamic defaults, parent loose-root и pickup-ready состояние.

Снятие запрещено при затянутом обязательном крепеже, установленном removal blocker или препятствии. Дублирующая установка и двойное занятие mount невозможны.

## Граф и запросы

`AssemblyDependencyKind.InstallRequiresInstalled` определяет порядок установки. `RemovalBlockedWhileInstalled` задаёт обратные блокировки без проверки имён объектов. Validator выявляет неизвестные IDs и циклы install-графа.

`VehicleAssemblyQuery` предоставляет:

- лучший mount;
- отсутствующие детали;
- установленные, но не закреплённые детали;
- полноту сборки;
- готовность placeholder connection point для будущих fluids/electrical связей.

Расширение connection points отложено; текущий API только сообщает готовность занятого mount.

## Сохранение

Schema v1 состоит из `PartSaveDto`, `MountSaveDto`, `FastenerSaveDto` и агрегата `VehicleAssemblySaveData`. Ключ части — `StableEntityId`, а не instance ID, hierarchy path или имя.

Restore сначала полностью валидирует schema, количество записей, повторные stable/mount/fastener keys, definition compatibility, occupancy/fastener consistency и диапазоны стадий. Только после успешной preflight-проверки состояние применяется. Storage backend и миграции следующих schema принадлежат Save milestone.

## Производительность

Hot path использует сериализованные массивы, `OverlapSphereNonAlloc` с фиксированным буфером и не вызывает scene-wide lookup. Preview query не изменяет граф. Аудит 10 000 запросов заднего барабана в Unity Editor batch mode зафиксировал 0 managed bytes и 0 graph mutations; подробности — `ASSEMBLY_PERFORMANCE_AUDIT.md`.

## Provenance заднего барабана

Из dataset `04B.4` перенесена только поведенческая спецификация:

- один `BoltPM`;
- ключ `14`;
- дискретные стадии `0..8`;
- полное ослабление перед снятием;
- установленное rear-left wheel блокирует снятие;
- donor overlap marker `0.01 m` сохранён как `ReferenceCandidateRadiusMeters`.

Remake position tolerance `0.42 m` и orientation tolerance `35°` — явно project-authored usability tuning. Donor code, PlayMaker FSM, textures и direct mesh не используются runtime-сценой.
