# 08A.1 — Candidate статических коллизий донорского мира

## Статус

`CANDIDATE / REGENERATED SEPARATELY / AUTOMATED VALIDATION PASSED / NOT MANUALLY ACCEPTED`

Этот документ фиксирует только проектный генератор и правила переноса
статических коллизий мира. Record принятого baseline `DonorWorldBaseline-v001`
не изменён. `DonorWorldBaseline-v002` сначала сгенерирован в отдельной
candidate-копии, затем после automated PASS активирован в основном workspace
только для ручной проверки. Предыдущий generated v001 payload сохранён во
внешней резервной копии.

- версия генератора: `1.2.0-08A1`;
- версия collision policy: `08A1.6`;
- замороженный источник: `M04A1_WorldColliders.csv`;
- исходных строк: `5 001`;
- строк, рассмотренных политикой solid-collision: `1 488`;
- collider-ов, включаемых в candidate runtime: `586`;
- source collider rows под собственным или ancestor `Rigidbody`: `415`, из
  них `108` ранее подходивших static rows явно исключены до project-owned
  presenters.

## Детерминированные disposition-ы

Каждая из 1 488 рассмотренных строк получает явный disposition. Ничего не
включается по неявному render-mesh fallback.

| Disposition | Количество |
|---|---:|
| `IncludedSafetyCriticalGlobal` | 32 |
| `IncludedStaticWorldSolid` | 554 |
| `ExcludedActorOrPlayer` | 23 |
| `ExcludedBuiltinMeshRequiresMapping` | 24 |
| `ExcludedCategory` | 2 |
| `ExcludedDisabled` | 32 |
| `ExcludedDoorRequiresBinding` | 79 |
| `ExcludedDynamicRequiresPresenter` | 108 |
| `ExcludedInactive` | 212 |
| `ExcludedTrigger` | 414 |
| `ExcludedVehicle` | 7 |
| `ExcludedWeatherShelterVolume` | 1 |
| **Всего** | **1 488** |

Типы включаемых collider-ов:

| Тип | Количество |
|---|---:|
| `MeshCollider` | 276 |
| `BoxCollider` | 279 |
| `CapsuleCollider` | 31 |
| `SphereCollider` | 0 |
| **Всего** | **586** |

Дополнительный аудит активных non-trigger строк расширил reviewed static set:

| Новая статическая категория | Добавлено вне старого safety allowlist |
|---|---:|
| `StaticProp` | 67 |
| `UtilityPole` | 12 |
| `Rock` | 3 |
| `Landmark` | 2 |

`Field` и `VegetationGrass` также включены в reviewed policy; их текущие пять
строк уже входят в неизменяемый safety allowlist. Четыре frozen-ID строки
получили точечный reviewed semantic override в `StaticProp`: два физических
корпуса колодцев, ошибочно объединённых донорской категорией `Water`, и две
неподвижные кабельные катушки стройплощадки из широкой категории `Wire`.
Оставшиеся две строки `ExcludedCategory` имеют категорию
`InteractivePropCandidate`: футбольный мяч и гирлянда относятся к будущим
project-owned interactive items.

Поддерживается несколько collider-ов на один project-owned entity. Полный
`ParentObjectId` ancestry проверяется по hash-locked
`WorldObjectPlacements.csv`: collider под donor `Rigidbody` не становится
неподвижным baseline blocker, даже если `Rigidbody` находится выше владельца
collider-а. Исходный
`MeshCollider.convex` сохраняется в disposition-аудите, но runtime-коллайдеры статического
мира всегда создаются non-convex. Это исключает немой PhysX cooking failure для сеток
выше convex-лимита и корректно, поскольку эти объекты не имеют `Rigidbody`. Встроенный Unity mesh GUID
`0000000000000000e000000000000000` без явного mapping не подменяется
render mesh и получает `ExcludedBuiltinMeshRequiresMapping`.

После генерации v002 builder создал:

- `Docs/WorldBaseline/LEGACY_SOLID_COLLIDER_DISPOSITIONS.csv`;
- `Docs/WorldBaseline/LEGACY_SOLID_COLLIDER_DISPOSITIONS.sha256`.

Фактический SHA-256 CSV после финальной генерации v002:
`0b3986725d1d88740639e5d660ea68208777c184ca6b0ceecf11e665896ad665`.
Фактический ownership fingerprint:
`82797c6818ed54ded649bb40fb04cbdcd3575f5d5194350e428854614f1b9b8d`.
Каждая строка содержит source-ancestry flag, disposition, runtime layer,
PhysicsMaterial и policy version.

## Ownership и физические свойства

Существующий неизменённый allowlist из 32 safety-critical collider-ов
сохраняется как `SafetyCriticalGlobal`. Обычные статические collider-ы принадлежат
ячейкам. Глобальными остаются только старые 32 и действительно глобальные
объекты источника/агрегаты карты; наличие collider-а само по себе больше не
принуждает entity к global ownership.

- поверхности используют слой `WorldSurface`;
- статические препятствия используют слой `WorldSolid`;
- `WorldSurfaceZeroBounce.physicMaterial` и
  `WorldSolidZeroBounce.physicMaterial` — project-owned, non-null, с
  `bounciness = 0`;
- trigger-ы, disabled-объекты, player/actor/NPC и динамические vehicle-объекты
  исключены; 13 отдельно проверенных неподвижных vehicle wreck obstacles
  включены;
- включаемые collider-ы создаются только из проверенных frozen-source данных.

Текущий bounded mapping намеренно не переносит донорские audience/layer
различия (`PlayerOnlyColl`, `CollCar`, `TireCol`) и сводит физические препятствия
к `WorldSolid`. Для player traversal это допустимый candidate baseline;
vehicle-specific layer semantics остаются отдельным известным долгом.

## Двери: намеренно не включены как неподвижные стены

Политика обнаружила 79 door-like строк. Из них 73 фактически активны:

- 29 активных строк имеют явный ancestor `Pivot*` в frozen placement
  hierarchy;
- 44 активные строки не имеют явного `Pivot*`.

Даже для pivot-backed строк одного закрытого pivot недостаточно для корректной привязки:
источник не фиксирует безопасно направление и угол открытия, lock state,
парность створок и случаи с отдельными open/closed representations. Поэтому все
79 строк получают `ExcludedDoorRequiresBinding`; генератор не превращает их в
неподвижные невидимые блокеры и не угадывает параметры шарнира.

20 июля 2026 пользователь явно принял это ограничение: двери остаются без
collision до будущей механики открытия, которая не должна копировать текущий
донорский способ. Временный generic hinge не создаётся.

Приоритетный отдельный door-binding pass должен разобрать как минимум:

- дом: `Bathroom`, `Bedroom1`, `Bedroom2`, `Pantry`, `Livingroom Front`,
  `WC`, `Middleroom`, `Rear`, `Sauna`, а также
  `YARD/Building/BEDROOM2/house_door1`;
- гараж дома:
  `YARD/Building/Garage/GarageDoors/DoorLeft/Coll` и
  `YARD/Building/Garage/GarageDoors/DoorRight/Coll`;
- Teimo:
  `STORE/LOD/Door2/Pivot/mesh`, `STORE/LOD/DoorBar/Pivot/mesh` и
  `STORE/LOD/DoorStore/Pivot/mesh`;
- Fleetari:
  `REPAIRSHOP/Building/Door1Closed/garage_door 1` и
  `REPAIRSHOP/LOD/Door/mesh`.

Позднее нужна отдельная project-owned авторизация через существующую архитектуру
`WorldHingedArchitecture` либо явно документированный совместимый binding.
До этого двери намеренно остаются проходимыми и не считаются частью статического
collision gate.

## Проверки кода

- `dotnet build MSC.Editor.csproj -v:minimal
  -p:UseSharedCompilation=false` — PASS, 1 existing warning, 0 errors;
- `dotnet build MSC.Tests.PlayMode.csproj -v:minimal
  -p:UseSharedCompilation=false` — PASS, 0 warnings, 0 errors;
- `dotnet build MSC.Tests.EditMode.csproj -v:minimal
  -p:UseSharedCompilation=false` — PASS, 4 existing warnings, 0 errors;
- Unity candidate cellization build r6 — PASS: 50 scenes, 3 842 entities,
  586 collider-ов, 15 anchors;
- Unity full cellization validator r6 — PASS: 2 605 renderers;
- Unity focused EditMode r6 — 41/41 PASS;
- Unity focused PlayMode r6 — 14/14 PASS;
- production environment build/validation r6 — PASS, builder `1.0.3`.

Сцены v002 пока остаются candidate artifact и не становятся новым frozen
accepted baseline до ручной проверки.

## Обязательная ручная проверка v002

1. Проверить shell дома, интерьер, крышу и окна без невидимых блокеров.
2. Проверить Teimo и Fleetari снаружи; двери должны остаться проходимыми либо
   управляться отдельным подтверждённым binding-ом.
3. Проверить стволы деревьев, заборы, полы, дороги и мосты.
4. Пройти границы ячеек пешком и на автомобиле, затем проверить unload/reload.
5. Убедиться, что collider-ы ячейки выгружаются вместе с ней, а 32
   safety-critical global collider-а остаются стабильными.
6. Проверить, что `WorldSurface` и `WorldSolid` видимы player/vehicle raycast-ам
   и участвуют в Physics collision matrix.
7. Проверить отсутствие trigger-ов, actor/vehicle collider-ов и неподвижных
   door blockers в сгенерированном наборе.

До выполнения этих шагов 08A.1 collision candidate не является принятым
baseline; локальная тестовая активация не заменяет frozen record v001.
