# Передача donor world baseline в weather milestones

Дата: 2026-07-17

Цель: зафиксировать current environment ownership и требования для
`07A_ENVIRO3_PREFLIGHT_AND_WEATHERLAB.md`, не добавляя Enviro или weather logic
в Milestone 06B3.

Статус: handoff подготовлен документально. Enviro 3 не установлен и не
реализован. Итоговый 06B3 gate закрыт; 07A разрешён как следующий отдельный
milestone.

## 1. Активный world runtime

Единственный active feature-parity profile:

`donor-feature-parity-06b2`

Composition root и streaming owner находятся в:

`Assets/Game/Bootstrap/Bootstrap.unity`

Bootstrap:

- создаёт project-owned player;
- привязывает его как streaming focus;
- загружает global/focus scenes до активации player;
- держит `ProductionWorldStreamingService` на process-lifetime object;
- держит `DonorWorldLegacyPresentationController` на том же persistent object.

Generated donor scenes являются additive presentation/content scenes. Они не
должны становиться владельцами game time, weather schedule, sky, fog, sun,
wetness или lightning.

## 2. Текущие HDRP environment owners

Активная Bootstrap scene содержит:

| Owner | Текущее содержимое | Статус для 07A |
|---|---|---|
| `Directional Sun` | Project-owned directional light с HDRP companion data | Audit, затем передать presentation ownership Enviro adapter-у либо явно оставить единственным согласованным sun owner |
| `Global Volume` | `Assets/Game/Presentation/Lighting/BootstrapGlobalVolume.asset` | Audit и заменить/отключить конфликтующие sky/fog/exposure owners |
| `VisualEnvironment` | Physically Based Sky, no HDRP cloud type | Temporary static baseline |
| `Fog` | Включён, включая volumetric fog | Temporary owner; 07A обязан обеспечить ровно одного fog owner |
| `Exposure` | Fixed exposure `14` | Audit для indoor/outdoor и weather readability |
| `Tonemapping` | ACES-family mode | Может остаться project presentation setting после проверки |

`BootstrapGlobalVolume` не является Enviro integration. Это временная статическая
HDRP-конфигурация.

Generated 06B2 global/cell scenes создаются с:

- `RenderSettings.fog = false`;
- `RenderSettings.skybox = null`;
- flat black ambient;
- reflection intensity `0`;
- без donor lights, probes, post-processing и weather.

Следовательно, additive donor scenes не должны конкурировать с Bootstrap за
environment ownership.

## 3. Neutral clear/dry contract

Для входа в weather work нейтральное состояние определяется так:

- precipitation intensity: `0`;
- accumulated wetness: `0`;
- puddle output: `0`;
- gameplay lightning: disabled;
- dynamic weather transition: отсутствует;
- donor weather, sky и fog logic: отсутствуют;
- sun/sky/fog presentation: только текущие project-owned Bootstrap HDRP owners.

Ограничение: project-owned runtime weather state ещё не реализован. В
`MSC.Weather` существует только boundary:

`IWeatherService.RainIntensity`

Текущий partial `GameServiceBindings` связывает только world streaming и не
предоставляет concrete `IGameTimeService` или `IWeatherService`.

Кроме того, Bootstrap Volume содержит включённый статический fog. Поэтому
текущее состояние корректнее называть `static dry baseline`, а не
подтверждённым финальным clear-weather preset. В 07A требуется явно решить,
должен ли neutral clear preset отключить или перенастроить этот fog.

## 4. Enviro 3 readiness

На момент аудита:

- Enviro package/version отсутствует в `Packages/manifest.json`;
- Enviro assets не найдены в типовых project paths;
- Enviro API references в project-owned runtime code не обнаружены;
- dedicated Enviro integration assembly отсутствует;
- vendor files не изменялись;
- WeatherLab не создан.

Точный approved Enviro 3 package/version должен быть подтверждён в 07A до
implementation. Автоматически добавлять external package или подменять Enviro
заглушками запрещено.

## 5. Целевой ownership contract

Будущая интеграция должна соблюдать:

1. Project-owned game time и calendar остаются authoritative.
2. Project-owned deterministic weather scheduler остаётся authoritative.
3. Project-owned wetness/drying, gameplay lightning, saves и cross-system
   outputs остаются authoritative.
4. Enviro отвечает только за presentation через dedicated integration assembly.
5. Core/gameplay assemblies не ссылаются на Enviro types.
6. Azure Sky и другие конкурирующие sky/weather owners не запускаются.
7. Одновременно существует ровно один sky owner, один sun/moon presentation
   owner и один fog owner.
8. Additive cells не получают отдельные Enviro managers.
9. Temporary donor material limitations документируются, а не маскируются
   массовой reauthoring работой внутри weather milestone.

## 6. Lifetime location

Рекомендуемое стабильное место lifetime:

- process-lifetime Bootstrap composition object, где уже находятся
  `ProductionWorldStreamingInstaller`,
  `ProductionWorldStreamingService` и
  `DonorWorldLegacyPresentationController`;
- concrete Enviro adapter в отдельной integration assembly;
- project-owned weather service передаётся composition root через существующую
  `IWeatherService` boundary после появления реальной реализации.

Это архитектурный handoff, а не утверждение, что environment service уже
реализован.

## 7. Additive scene lifecycle

Существующие проверенные patterns:

- `ProductionWorldStreamingService` использует additive
  `LoadSceneAsync`/`UnloadSceneAsync`;
- `DonorWorldLegacyPresentationController` подписывается на
  `SceneManager.sceneLoaded` и применяет shared material mode к новым cells;
- `DonorWorldLegacyReplacementRegistry` отслеживает `sceneLoaded` и
  `sceneUnloaded`;
- global scene остаётся загруженной, cells меняются вокруг focus.

Отдельного public environment lifecycle interface пока нет.

07A/07B должны выбрать один project-owned hook:

- либо persistent adapter подписывается на `SceneManager` lifecycle;
- либо streaming module публикует узкие typed events без Enviro dependency.

Hook должен:

- регистрировать загруженные renderers/volumes только при необходимости;
- не создавать per-cell weather owners;
- безопасно удалять per-scene presentation registrations при unload;
- повторно применять neutral/current weather output после reload;
- не использовать donor object names или hierarchy paths.

## 8. Temporary owners вне active profile

Standalone prototype, validation и comparison scenes могут содержать собственные
lights/volumes для локальной разработки. Перед 07A нужно проверить как минимум:

- `Assets/Game/Player/Content/Scenes/PlayerInteractionPrototype.unity`;
- `Assets/Game/Vehicle/Content/Assembly/Scenes/VehicleAssemblyPrototype.unity`;
- `Assets/Game/Vehicle/Content/Simulation/Scenes/VehicleSimulationPrototype.unity`;
- `Assets/Game/Vehicle/Content/Validation/Scenes/VehiclePhysicsValidation.unity`;
- `Assets/Game/World/Content/GaragePrototype/Scenes/GarageArtPrototype.unity`;
- `Assets/Game/World/Production/Scenes/WorldRemasterPilotPlaytest.unity`;
- `Assets/Game/World/Production/Scenes/WorldRemasterHomeShorelinePlaytest.unity`;
- world comparison/debug scenes.

Эти scenes не входят в active donor profile как environment owners. Их local
lighting допустим только внутри purpose-specific fixture и не должен
одновременно запускаться с production Bootstrap environment.

## 9. Legacy material families и wetness

Временный compatibility mapping содержит 292 resolved donor materials:

| Family | Количество | Ожидаемый wetness contract | Текущий статус |
|---|---:|---|---|
| `OpaqueLit` | 251 | Кандидат на global darkening/smoothness response после material audit | Не реализовано |
| `AlphaClipLit` | 14 | Vegetation/tree-wall surfaces требуют отдельного restrained response; generic puddle response недопустим | Не реализовано / coverage unknown |
| `TransparentLit` | 12 | Стекло и прозрачные surfaces обычно не получают generic ground wetness | Не реализовано / исключение ожидается |
| `TransparentUnlit` | 1 | Generic wetness не ожидается | Не реализовано |
| `Unlit` | 7 | Global PBR wetness неприменим без специального shader contract | Не реализовано |
| `EmissiveLit` | 5 | Wet base response возможен только без нарушения emission | Не реализовано / требует review |
| `TemporaryWater` | 2 | Управляется отдельным water presentation contract, не как rain-wet material | Temporary water only |

Текущая runtime baseline presentation:

- переключает только shared `LegacyTextured`/`LegacyDiagnostic` materials;
- не создаёт material instances;
- не публикует global wetness shader parameter;
- не связывает `IWeatherService.RainIntensity` с materials;
- не реализует accumulated wetness, drying или puddles.

Следовательно, wetness coverage temporary donor baseline на входе в 07A:
`Unknown / Partial by future implementation`, а не `PASS`.

Особые ограничения:

- severe low-quality/stretched terrain texture останется видимым debt;
- tree-wall/proxy vegetation не должна ошибочно получать тяжёлую material
  систему;
- glass, unlit signage и emissive surfaces требуют исключений;
- temporary water не должна использовать surface-wetness path;
- final production materials будут переавторены позднее.

## 10. WeatherLab-independent route для 07C

Предлагаемый production-route ID: `W07C-PROD-ROUTE-01`.

Контекст:

- старт только из `Assets/Game/Bootstrap/Bootstrap.unity`;
- active profile `donor-feature-parity-06b2`;
- WeatherLab scene не используется;
- player start/recovery position:
  `(153.495, 1.1, -1028.03)`.

Маршрут:

1. Home/garage spawn и двор.
2. Выезд на основной road loop.
3. Пересечение нескольких consecutive streaming boundaries.
4. Representative bridge segment.
5. Teimo/store и town context.
6. Возврат к home/garage тем же или альтернативным road segment.

Дополнительная weather visual station:

- lake/shore approach для fog, reflection, rain visibility и water response.

Текущий evidence:

- player walking у озера, по мостам и cross-cell relocation принят;
- automated vehicle-speed preload прошёл в 06B2;
- 06B3 development-harness vehicle route прошёл Fleetari, Teimo/store,
  inspection/town, major road loop, railway crossing и bridge через 24 cells.

Ограничение:

- dedicated production weather capture по точному
  `W07C-PROD-ROUTE-01` не выполнялся; 06B3 development-harness route не
  заменяет будущий 07C capture;
- exact route waypoints/cell sequence и standalone 1080p capture остаются
  `PENDING` для 07C.

## 11. Preflight checklist для 07A

До добавления Enviro:

1. Подтвердить точную package/version и способ лицензированной установки.
2. Проверить compatibility с Unity `6000.3.11f1` и HDRP `17.3.0`.
3. Создать dedicated integration assembly без ссылок из gameplay/core.
4. Зафиксировать единственного sky/sun/fog owner.
5. Решить судьбу `Directional Sun` и Bootstrap Global Volume components.
6. Определить explicit neutral clear/dry preset.
7. Определить project-owned game-time/weather state contracts.
8. Определить additive lifecycle hook.
9. Зафиксировать global wetness/material property contract и exclusions.
10. Не изменять Enviro vendor files.
11. Не менять donor baseline geometry, cell ownership или stable IDs.

## 12. Handoff decision

- Active donor world/profile и environment owner location: `READY`.
- Current static dry Bootstrap presentation: `READY WITH AUDIT`.
- Enviro package/API integration: `NOT IMPLEMENTED`.
- Concrete game time/weather service: `NOT IMPLEMENTED`.
- Wetness coverage: `PENDING`.
- Production weather-route capture: `PENDING` для 07C.
- Переход к 07A: `GO`, только отдельным milestone после подтверждения Enviro
  package/version.
