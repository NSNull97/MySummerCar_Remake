# Regeneration diff: DonorWorldBaseline-v001 → DonorWorldBaseline-v002

Дата: **2026-07-20**  
Статус diff: **DraftCandidateDiff**  
Статус v002: **Candidate / NotPromoted / HumanAcceptancePending / AutomatedValidationPassed**

`DonorWorldBaseline-v001` остаётся неизменённым замороженным и принятым
baseline. Этот документ не повышает v002 до accepted/frozen revision и не
утверждает ручной PASS.

После отдельной автоматической проверки локальный ignored generated payload
основного workspace переключён на v002 только для ручного Play Mode теста.
Предыдущий v001 payload сохранён во внешней резервной копии с меткой
`DonorWorldBaseline-v001_before_08A1_20260720`.

## Причина новой revision

После принятого Milestone 08A пользователь запросил ограниченное укрепление
временного donor world baseline перед 08B:

- привести temporary donor-материалы к предсказуемому HDRP-поведению;
- исправить неверное cast/receive shadows, включая просветы крыши;
- убрать чрезмерную металлическость, свечение и грязно-чёрное восприятие мира;
- добавить коллизии для проверенных статических препятствий;
- не менять карту, координаты, stable IDs, streaming-архитектуру и принятые
  системы 00–08A.

Milestone 08B этим изменением **не начат**.

## Неизменяемый источник и topology contract

| Поле | v001 | v002 candidate |
|---|---:|---:|
| Source revision | `msc-world-baseline-04a1.1-c3f2f337` | без изменений |
| `GAME.unity` SHA-256 | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` | без изменений |
| Eligible entities | 3 842 | 3 842 |
| Global entities | 88 | 88 |
| Cell-owned entities | 3 754 | 3 754 |
| Renderers | 2 605 | 2 605 |
| Legacy cell scenes | 49 | 49 |
| Gameplay anchors | 15 | 15 |

Не менялись world origin, bounds, scale, transforms, stable entity IDs,
replacement keys и ownership intent. Donor hierarchy не стал runtime-
архитектурой.

## Изменение material/shadow presentation

| Контракт | v001 | v002 candidate |
|---|---|---|
| Presentation generator | `06B2-v5.1.5` | `06B2-v5.2-08A1` |
| Compatibility policy | legacy v5.1.5 mapping | `08A1-temporary-hdrp-compatibility-v2` |
| Presentation fingerprint | `e337f9d1...8831fd` | `890ee927...725f31` |
| Material/texture manifest SHA-256 | `cfbce4fa...c4192` | `88da91f6...cfbb8b` |

В v002:

- metallic, smoothness и emission ограничиваются explicit compatibility policy;
- legacy diffuse-detail luminance пакуется как HDRP detail albedo, а не
  ошибочно интерпретируется как normal data;
- cast/receive shadows выбираются по материалу, категории и геометрии;
- крыши получают необходимую two-sided compatibility, а floor jack, tunnel,
  window frame и sign face не теряют корректные тени;
- два колодца и две кабельные катушки точечно переопределяются из шумных
  `Water/Wire` категорий в `StaticProp` и отбрасывают/принимают тени;
- настоящие широкие тонкие ground-like поверхности могут оставаться
  non-casting, но продолжают принимать тени;
- освещаемые временные renderers используют light probes.

Это hardening `TemporaryDirectImport`, а не production reauthoring.

## Изменение lighting ownership

Enviro 3 остаётся единственным владельцем sky/weather/fog presentation, а
project-owned integration — владельцем игрового времени и выходных параметров.
Production environment builder `1.0.3` добавляет HDRP
`IndirectLightingController` в project-owned профиль. Внешний дневной indirect
diffuse получает ограниченный multiplier `1.15`; interior/neutral baseline и
indirect reflections остаются `1.0`. Global reflection presentation остаётся
ограниченной `0.6`, поэтому исправление тусклых теней не усиливает прежний
«металлический» дефект.

## Изменение collision contract

Финальный candidate artifact сгенерирован и прошёл structural validation.

| Поле | v001 | v002 generated | Delta |
|---|---:|---:|---:|
| Всего collider-ов | 32 | **586** | +554 |
| `MeshCollider` | не нормировано отдельным v002 policy | 276 | — |
| `BoxCollider` | не нормировано отдельным v002 policy | 279 | — |
| `CapsuleCollider` | не нормировано отдельным v002 policy | 31 | — |
| `SphereCollider` | не нормировано отдельным v002 policy | 0 | — |
| Safety-critical global | 32 | 32 | 0 |
| Ordinary cell-owned static | 0 | 554 | +554 |

Disposition всех 1 488 рассмотренных source rows:

| Disposition | Generated |
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

Pre-final snapshot содержал 582 collider-а (`274 Mesh / 277 Box / 31
Capsule`). Финальная policy `08A1.6` добавила два проверенных колодца как
`MeshCollider` и две кабельные катушки как `BoxCollider`; четыре строки перешли
из `ExcludedCategory` в `IncludedStaticWorldSolid`. Итог: 586 collider-ов.

Полная `ParentObjectId` ancestry проверяется по hash-locked placements. 415
source collider rows находятся под собственным либо ancestor `Rigidbody`; 108
ранее подходивших static rows исключены как
`ExcludedDynamicRequiresPresenter`. Триггеры, actors/NPC, живые vehicle,
disabled/inactive объекты и `NoRain` shelter volume не превращаются в
неподвижные препятствия.

## Решение по дверям

Все 79 door-like rows получают `ExcludedDoorRequiresBinding`; runtime static
collider для них не создаётся. 20 июля 2026 пользователь явно решил оставить
двери без коллизий, потому что будущая project-owned механика открытия не будет
копировать донорскую реализацию. Generic временный hinge и невидимые статические
door blockers не создаются.

Проходимость дверей — намеренное временное ограничение и не является failure
текущего статического collision gate.

## Автоматизированное evidence и незакрытые поля

Финальный r6 artifact прошёл:

- cellization build: PASS, 50 scenes / 3 842 entities / 586 colliders / 15
  anchors; ownership fingerprint
  `82797c6818ed54ded649bb40fb04cbdcd3575f5d5194350e428854614f1b9b8d`;
- cellization validator: PASS для всех 50 generated scenes;
- production environment build/validation: PASS, builder `1.0.3`, shelter
  evidence schema 2;
- focused EditMode: 41/41 PASS;
- focused PlayMode: 14/14 PASS;
- disposition CSV SHA-256:
  `0b3986725d1d88740639e5d660ea68208777c184ca6b0ceecf11e665896ad665`;
- automated evidence bundle SHA-256:
  `4a9f506e50da6acbabda95abaaebe82b8abc1fec7772822220da6c1d3439ecb0`;
- профильные `.csproj` компилируются без ошибок; существующие vendor/non-blocking
  warnings не объявляются устранёнными.

Незакрыты только ручная проверка материалов, теней, traversal/streaming и
явное пользовательское принятие. До этого v002 остаётся
`Candidate / NotPromoted / HumanAcceptancePending`.

## Известные различия и долг

- **P2 — donor collision audience/layers.** `PlayerOnlyColl`, `CollCar` и
  `TireCol` пока сведены к `WorldSolid`. Для текущей player-traversal проверки
  это допустимо; vehicle-specific semantics требуют отдельного mapping.
- **Accepted temporary limitation — двери.** Они намеренно проходимы до
  project-owned door mechanic.
- **TemporaryDirectImport debt.** Donor geometry/materials/textures/collision
  не становятся `ProductionReady` и подлежат Phase 2 replacement.

## Promotion gate

Promotion: **NotPromoted**.  
Manual result: **HumanAcceptancePending**.  
Принятым замороженным baseline остаётся `DonorWorldBaseline-v001`; локальный
workspace временно использует v002 как `ActiveForManualValidation`.
