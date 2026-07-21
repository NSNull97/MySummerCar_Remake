# Milestone 08A.1 report — donor world baseline hardening candidate

Status: **Candidate / NotPromoted / HumanAcceptancePending / AutomatedValidationPassed**  
Date: **2026-07-20**

Этот bounded pass подготовил отдельный `DonorWorldBaseline-v002` candidate для
исправления временных материалов, теней, lighting balance и статических
коллизий donor world baseline. Record принятого `DonorWorldBaseline-v001`
не изменён и остаётся `Frozen / Accepted`. Milestone 08B не начат.

Отчёт не утверждает ручной PASS: финальная регенерация и автоматизированные
проверки завершены, но пользовательский playtest ещё нужен.

## 1. Что было проверено

Перед изменениями проверены:

- `AGENTS.md`, включая обязательную Phase 1 модель и защиту принятого baseline
  00–08A;
- frozen donor source identity и regeneration policy;
- текущие cellization/material generators, active streaming profile и generated
  boundary;
- временные HDRP materials, renderer shadow flags и Enviro/HDRP ownership;
- все 1 488 non-trigger/trigger collider rows, их categories, active state,
  parent hierarchy и `Rigidbody` ancestry;
- дом, крыша, магазины, мосты, деревья, ограждения, utility poles, rocks,
  vehicle wreck obstacles, wells и cable reels как representative traversal
  cases;
- door-like rows и существующую архитектуру `WorldHingedArchitecture`;
- существующие EditMode/PlayMode tests и production environment validation.

## 2. Материалы и тени

Material pipeline повышен до `06B2-v5.2-08A1`, compatibility policy — до
`08A1-temporary-hdrp-compatibility-v2`.

Выполнено:

- HDRP materials проверяются как фактически совместимые, а не только по имени
  shader-а;
- metallic/smoothness и emission ограничены безопасными временными диапазонами;
- legacy diffuse-detail luminance пакуется в detail albedo;
- double-sided normals настроены для корректной temporary compatibility;
- renderer shadow cast/receive policy теперь учитывает material identity,
  category и geometry bounds;
- крыши получают two-sided shadow compatibility;
- tunnel, floor jack, window frame и sign face сохраняют cast shadows;
- два колодца и две кабельные катушки точечно переопределены из шумных
  `Water/Wire` категорий в `StaticProp` и теперь cast/receive shadows;
- только подтверждённые широкие тонкие ground-like surfaces отключают casting;
- освещаемые renderers используют light probes.

Сформированный material candidate содержит 292 resolved materials, 265 source
textures и 273 imported texture variants. Presentation fingerprint:
`890ee927163a4fe310e97951580895e4abde6ea44011f358e75fa3a869725f31`.
Material/texture manifest SHA-256:
`88da91f69976197c036435743de5a76c53523ab99b4753049152469f5dcfbb8b`.

Это всё ещё `TemporaryDirectImport`, не финальные Phase 2 материалы.

## 3. Lighting balance

Project-owned Enviro integration и production environment builder `1.0.3`
расширены HDRP `IndirectLightingController`:

- neutral/interior indirect diffuse: `1.0`;
- exterior daylight indirect diffuse: `1.15`;
- sheltered uplift: половина exterior delta;
- indirect reflections и reflection probe multiplier: `1.0`;
- temporary global reflection presentation: `0.6`.

Это осветляет проваленные внешние тени и общую тусклую картинку, но не
возвращает чрезмерный metallic/gloss. Enviro 3 остаётся vendor dependency и не
редактировался; новый production profile/adapter принадлежат проекту.

Shelter evidence переведён на schema 2 и пинит выбранную геометрию fingerprint:
`508b155f7df7c04622952b2b1e63dadcce1a043030c022c271930a54e5dea400`.
Полный scene SHA остаётся audit-only, чтобы не ломать lighting binding при
нерелевантной сериализации cell scene.

## 4. Статические коллизии

Добавлена детерминированная project-owned collision policy. Она:

- поддерживает несколько `Mesh/Box/Capsule/SphereCollider` на одну entity;
- сохраняет исходную collider geometry и transform evidence;
- создаёт static mesh collision как non-convex без `Rigidbody`;
- проверяет полную `ParentObjectId` ancestry по hash-locked placements;
- исключает actors, triggers, disabled/inactive rows, динамическую ancestry,
  live vehicles и weather shelter volumes;
- сохраняет прежние 32 safety-critical global colliders;
- размещает обычные static solids в cell ownership;
- назначает `WorldSurface` / `WorldSolid` и project-owned zero-bounce physics
  materials.

Из 415 source collider rows под собственным либо ancestor `Rigidbody` 108
ранее подходивших static rows исключены как
`ExcludedDynamicRequiresPresenter`. `NoRain` отдельно исключён как
`ExcludedWeatherShelterVolume`. 13 проверенных неподвижных vehicle wreck
obstacles разрешены как world geometry; live vehicles остаются исключёнными.

Финальный сгенерированный v002 contract:

| Поле | Generated |
|---|---:|
| Всего collider-ов | **586** |
| `MeshCollider` | 276 |
| `BoxCollider` | 279 |
| `CapsuleCollider` | 31 |
| `SphereCollider` | 0 |
| `IncludedSafetyCriticalGlobal` | 32 |
| `IncludedStaticWorldSolid` | 554 |
| `ExcludedCategory` | 2 |

Pre-final snapshot содержал 582 collider-а. Финальная policy `08A1.6` добавила
два колодца как `MeshCollider` и две кабельные катушки как `BoxCollider`.
Ownership fingerprint:
`82797c6818ed54ded649bb40fb04cbdcd3575f5d5194350e428854614f1b9b8d`.
Disposition CSV SHA-256:
`0b3986725d1d88740639e5d660ea68208777c184ca6b0ceecf11e665896ad665`.

## 5. Двери

Все 79 door-like collider rows получают `ExcludedDoorRequiresBinding` и не
становятся статическими стенами. 20 июля 2026 пользователь явно решил оставить
двери без коллизий: будущая project-owned механика открытия будет отличаться от
донорской. Временный generic hinge не создаётся.

До отдельной совместимой door implementation двери намеренно проходимы. Это
принятое ограничение, а не failure текущего static collision gate.

## 6. Candidate isolation и provenance

- Source revision и source hashes не менялись.
- World coordinates, origin, scale, bounds, 3 842 entity, 49 cells, stable IDs,
  anchors и replacement keys не мигрировали.
- Сгенерированный donor payload остаётся ignored/private под
  `Assets/Game/LegacyImport/RuntimeBaseline/`.
- v002 сначала создан и проверен в отдельной локальной candidate-копии;
- после automated PASS основной ignored generated payload переключён на v002
  только для ручного теста;
- прежний v001 payload сохранён во внешней резервной копии с меткой
  `DonorWorldBaseline-v001_before_08A1_20260720`;
- v001 record не изменён и остаётся единственным принятым frozen baseline.
- Новые policy, manifests, reports и provenance records являются project-owned.

Candidate records:

- `Docs/WorldBaseline/Candidates/DonorWorldBaseline-v002/BASELINE_CANDIDATE.json`;
- `Docs/WorldBaseline/Candidates/DonorWorldBaseline-v002/VALIDATION_RESULT.json`;
- `Docs/WorldBaseline/RegenerationDiffs/DonorWorldBaseline-v001__DonorWorldBaseline-v002.json`;
- `Docs/WorldBaseline/RegenerationDiffs/DonorWorldBaseline-v001__DonorWorldBaseline-v002.md`.

## 7. Автоматизированные проверки

Финальный candidate r6 проверен целостно:

| Проверка | Результат |
|---|---|
| Candidate cellization build r6 | PASS; 50 scenes / 3 842 entities / 586 colliders / 15 anchors |
| Candidate cellization validator r6 | PASS; 2 605 renderers, fingerprint `82797c68...b9b8d` |
| Production environment build/validation r6 | PASS; builder `1.0.3`, shelter schema 2 |
| Focused EditMode r6 | 41/41 PASS |
| Focused PlayMode r6 | 14/14 PASS |
| Main workspace local activation | PASS; build/validator 586, EditMode 41/41, PlayMode 14/14 |
| Accepted 08A UI regression after Bootstrap save | EditMode 33/33, PlayMode 11/11 PASS |
| `MSC.Editor.csproj` | PASS, 0 errors; 1 existing warning |
| `MSC.Tests.EditMode.csproj` | PASS, 0 errors; 4 existing warnings |
| `MSC.Tests.PlayMode.csproj` | PASS, 0 errors |
| `MSC.Weather.Enviro3Integration.Editor.csproj` | PASS, 0 errors; vendor warnings не скрыты |
| `MSC.Weather.Production.Tests.PlayMode.csproj` | PASS, 0 errors |

SHA-256 объединённого final evidence bundle:
`4a9f506e50da6acbabda95abaaebe82b8abc1fec7772822220da6c1d3439ecb0`.
SHA-256 evidence bundle локальной активации в основном workspace:
`bca4221dce001ea9842c41bf91af53fa17d38f0f4ee7185282f1c8640a4792dc`.
Автоматические gate закрыты; ручная visual/traversal проверка остаётся
обязательной.

## 8. Что должен проверить пользователь

После финальной локальной установки v002 открыть
`Assets/Game/Bootstrap/Bootstrap.unity`, запустить Play → New Game и проверить:

1. В доме и гараже крыша не пропускает внешние тени/небо; при смене времени
   суток shadow behaviour остаётся стабильным.
2. Грунт, дороги, крыши и стены не выглядят самосветящимися либо металлическими;
   дневные тени читаются, но не проваливаются в грязно-чёрный цвет.
3. Игрок упирается в shells дома, Teimo/Fleetari, окна, стволы деревьев,
   ограждения, utility poles, rocks, мосты, колодцы и кабельные катушки.
4. Двери намеренно остаются проходимыми.
5. `NoRain` и другие shelter/trigger volumes не создают невидимых blockers.
6. Динамические props, actors и live vehicles не оказываются замороженными
   статическими препятствиями.
7. После переходов через границы ячеек и unload/reload коллизии не дублируются
   и не пропадают.

До явного сообщения пользователя об успешной проверке статус остаётся
`HumanAcceptancePending`.

## 9. Известные ограничения и риски

- **P2:** donor audiences/layers `PlayerOnlyColl`, `CollCar`, `TireCol` пока
  сведены к `WorldSolid`. До полноценной проверки production vehicle traversal
  потребуется отдельный project-owned mapping.
- Двери временно не имеют collision по прямому решению пользователя.
- Temporary donor материалы и geometry не становятся production art.
- Standalone private player build для этой candidate revision пока не заявлен
  как выполненный.

## 10. Совместимость

Архитектура Player, Interaction, streaming, Enviro ownership, Wwise boundary,
save foundation и 08A UI не заменялись. Public APIs, serialized stable IDs,
world transforms и save DTOs не мигрировали. Runtime collision расширяется
через generated baseline metadata/layers; presentation lighting — через
существующий Enviro integration adapter.

Compatibility impact: **bounded additive candidate; no migration required**.

## 11. Promotion gate и следующий milestone

Promotion decision: **NotPromoted**.  
Manual result: **HumanAcceptancePending**.  
Принятый baseline: **DonorWorldBaseline-v001 (Frozen / Accepted)**.  
Локальный workspace: **DonorWorldBaseline-v002 / ActiveForManualValidation**.

Ровно один следующий milestone после финальной регенерации и явного ручного
принятия v002: **Milestone 08B — Phase 1 scope lock and donor feature parity audit**.
