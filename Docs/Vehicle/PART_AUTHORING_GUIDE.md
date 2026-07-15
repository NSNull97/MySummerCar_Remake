# Руководство по авторингу деталей

## Создание определения

Используйте `Create > MSC > Vehicle Assembly > Part Definition` либо воспроизводимый builder:

`Tools > MSC Remake > Vehicle Assembly > Build Representative Test Vehicle`.

Заполните:

1. `Definition Id` — стабильный project-owned ID вида `vehicle.engine_block`.
2. `Display Name` — текст интерфейса, не идентификатор.
3. `Category` — структурная, подвеска, тормоз, колесо, двигатель и т. п.
4. `Mass Kilograms` — SI unit; неизвестную donor mass не выдавать за измеренную.
5. `Visual Prefab` — новый проектный prefab. `ReferenceOnly` и `DonorGenerated` запрещены.
6. `Compatibility Rules` — socket type и при необходимости ID владельца mount.

Definition ID не заменяет stable entity ID. Каждая persistent scene instance получает отдельный `StableEntityIdAuthoring`.

## Runtime instance

Корневой GameObject детали должен содержать:

- `Rigidbody`;
- `StableEntityIdAuthoring`;
- `PhysicsPickupTarget` из M4;
- `PartInstance`;
- `AssemblyInstalledPartInteractionTarget`;
- `InteractionTargetHost`, регистрирующий pickup и removal capabilities.

Collider может находиться в clean visual prefab ниже Rigidbody. Loose body должен начинаться dynamic, с gravity и непрерывным collision detection. Assembly root — единственное исключение: он изначально kinematic и не снимается.

## Pivot и ориентация

- Локальный `(0,0,0)` runtime wrapper считается install pivot.
- После установки wrapper получает world position/rotation `MountPose`, но сохраняет исходный world scale.
- Логический `MountPose` должен иметь unit scale; масштабируемую gizmo/debug-геометрию размещайте отдельным дочерним объектом, а не в его parent chain.
- Donor pivot разрешён только как `PivotSource`/`MountPointSource` с ledger record.
- При замене prototype visual запрещено менять wrapper pivot без миграции mount pose и regression tests.

## Prototype-набор M05

Сцена содержит 15 частей: chassis root, rear-left trailing arm, brake drum, wheel, battery, driver seat, hood, left door, radiator, engine block, cylinder head, starter, alternator, exhaust и intake manifolds. Это clean graybox/proof content, а не production art.

## Проверка

После изменения:

1. запустите `Build Representative Test Vehicle` для воспроизводимой генерации;
2. запустите `Validate Definitions and Prototype`;
3. проверьте gizmo pivot/mount и зелёный/красный preview;
4. запустите M05 EditMode и PlayMode suites;
5. убедитесь, что `AssetDatabase.GetDependencies` сцены не содержит `LegacyImport/ReferenceOnly` или `Imported/DonorGenerated`.

Validator сообщает пустые definitions, duplicate IDs, invalid stable IDs, missing visual prefabs и неизвестные dependencies без автоматической подстановки.
