# Mount points, крепёж и инструменты

## Mount definition и authoring

`MountPointDefinition` описывает семантику, а `MountPointAuthoring` — конкретный runtime mount ID и Transform pose.

Обязательные поля:

- уникальный `Definition Id` и уникальный runtime `Mount Id`;
- `Socket Type`;
- `Owner Part Definition Id`;
- allow-list `Accepted Part Definition Ids`;
- position/orientation/preview/obstruction constraints;
- принадлежащие mount `FastenerDefinition`.

`Reference Candidate Radius` — provenance field. Он не заменяет remake interaction tolerance.

## Tolerances

`MountConstraint` использует метры и градусы:

- position tolerance проверяет расстояние part pivot до pose;
- angular tolerance проверяет quaternion angle;
- preview distance ограничивает candidate scan;
- obstruction radius проверяет только colliders, явно отмеченные `AssemblyMountObstruction`.

Выбранная mount точка детерминирована. При одинаковом расстоянии используется ordinal runtime mount ID.

## Fastener definition

Для каждого крепежа задаются:

- project-owned ID;
- размер `FastenerSize`;
- `Maximum Stage`;
- направление затяжки;
- появляется ли он при install;
- блокирует ли stage `> 0` снятие;
- `ToolCompatibilityRule` с типом и размером.

Один fastener ID уникален внутри mount. Runtime явно разделяет insert/remove и поворот: крепёж проходит состояния `Absent`, `Inserted`, `Loose`, `PartiallyTightened`, `Tightened`, а authored направление затяжки проверяется при каждом turn. Крепёж нельзя затянуть до установки детали, чужим инструментом, в неверном направлении, за пределами диапазона или через obstruction marker.

## Rear-left drum fixture

`mount.brake_drum_rl` хранит:

- accepted part `vehicle.brake_drum_rl`;
- owner/prerequisite `vehicle.trailing_arm_rl`;
- `fastener.drum_rl.boltpm`;
- `FastenerSize.Millimeter14`;
- stages `0..8`;
- reference marker `0.01 m`;
- removal blocker `vehicle.wheel_rl`.

Три donor runtime repetition и user attestation подтверждают поведенческую спецификацию. Torque, число физических оборотов и stripping не измерены и не реализованы.

## Интерактивный prototype

- `E`: pickup или handoff в mount; для установленной loose/unblocked детали — remove.
- `R`: tool activation на жёлтом fastener target.
- Prototype fastener target автоматически переключается на loosen после максимальной стадии и обратно на tighten после zero. Это временная UI-схема без inventory/equipped-tool subsystem.

Mount gizmos отображают position sphere, forward axis и obstruction sphere. Dependency graph доступен через `Tools > MSC Remake > Vehicle Assembly > Dependency Graph`.
