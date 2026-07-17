# Enviro 3: граница project-owned adapter

Дата среза: 2026-07-17.

## Authoritative dependency direction

```text
Project game time / weather domain (07B+)
                |
                v
MSC.Weather.Presentation.Runtime
                |
                v
Dedicated project-owned Enviro integration assembly
                |
                v
Enviro3.Runtime public API
```

Обратные ссылки запрещены. `MSC.Weather.Presentation.Runtime`, core, save, world, vehicle и gameplay assemblies не должны ссылаться на `Enviro3.Runtime` или хранить `EnviroWeatherType`/`EnviroQuality` references.

## Существующий vendor-neutral contract

Assembly:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Presentation\Runtime\MSC.Weather.Presentation.Runtime.asmdef`

Namespace: `MSC.Weather.Presentation`.

Фактически присутствуют:

- `EnvironmentPresentationFrame`;
- `EnvironmentPresentationCapabilities`;
- `EnvironmentQualityTier` (`Low`, `High`);
- `IEnvironmentPresentationAdapter`;
- `EnvironmentPresentationStatus`;
- `EnvironmentPresentationDiagnostic` и стабильные diagnostic codes;
- `EnvironmentBindingId`;
- `EnvironmentPresentationBindingDefinition`;
- `EnvironmentLightningVisualRequest`;
- `EnvironmentRefreshRequest`;
- pure `EnvironmentPresentationValidator`.

Frame выражает project-owned date/time, cloud type/coverage/intensity, precipitation type/intensity, fog target, wind direction/base/gust speed, lightning visual request, refresh request, quality tier, transition duration и enabled state.

`EnvironmentBindingId` допускает 3-64 lower-case ASCII letters/digits с одиночными `.`, `-`, `_` между сегментами. Это project stable ID, не Unity instance ID и не vendor display name.

## Dedicated integration assembly

Реализованная runtime assembly: `MSC.Weather.Enviro3Integration`:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Runtime\MSC.Weather.Enviro3Integration.asmdef`

Это единственная player runtime assembly, которая может ссылаться одновременно на:

- `MSC.Weather.Presentation.Runtime`;
- `Enviro3.Runtime`.

Она ограничена define constraints `ENVIRO_3` и `ENVIRO_HDRP`. Editor assembly `MSC.Weather.Enviro3Integration.Editor` содержит builder/preflight и может ссылаться на HDRP/Core Editor APIs. Единственное дополнительное исключение — Editor-only `MSC.Weather.Enviro3Integration.Tests.EditMode`, которое тестирует точный vendor boundary и не попадает в player. Vendor-neutral presentation и WeatherLab runtime assemblies не ссылаются на `Enviro3.Runtime`.

## Стабильная binding map для bounded 07A

| Project ID | Vendor typed asset | GUID | Статус |
|---|---|---|---|
| `weather.clear` | `Profiles\Weather Types\Clear Sky.asset` | `3e61e22e1ac8ba045a3b0e53c22b3629` | automated runtime mapping PASS; manual general PASS; capture missing |
| `weather.overcast` | `Profiles\Weather Types\Cloudy 3.asset` | `11fdd63974de7ae44b1c689d499953f6` | automated runtime mapping PASS; manual general PASS; capture missing |
| `weather.rain` | `Profiles\Weather Types\Rain.asset` | `f736e404e0b052942bc41c35c50dccad` | automated particle runtime PASS; High visual remediation accepted; capture/Low comparison pending |
| `weather.storm_visual` | `Profiles\Weather Types\Storm.asset` | `ebf8ae7a51a5cd342a90c81f5182d8a8` | isolated runtime/particles PASS; lightning and rain visible; capture pending |
| `weather.night` | `Clear Sky.asset` + explicit night time | `3e61e22e1ac8ba045a3b0e53c22b3629` | compound runtime mapping PASS; manual partial; headlights capture missing |
| `weather.fog` | `Profiles\Weather Types\Foggy.asset` | `9ba6458aa7df92d4494a7a3d40830a15` | automated runtime mapping PASS; manual fog composition PASS; capture missing |
| `quality.low` | `Profiles\Quality\Low.asset` | `a002704085c17f1439758fcee25df529` | automated runtime mapping PASS; performance pending |
| `quality.high` | `Profiles\Quality\High.asset` | `60e887b1524da0a4a8f1318ef102e22a` | automated runtime mapping PASS; performance pending |

Runtime code хранит serialized typed references в integration-owned binding asset. Он не вызывает `ChangeWeather(string)` и не сохраняет vendor array index.

Effects имеют отдельную явную typed reference: `Default Effects Preset.asset`, GUID `59a8076f06e540343b875f850ac3b6a4`. Это базовый preset с существующим prefab и exact key `Rain`. Exact key является внутренним подтверждённым контрактом vendor Weather/Effects API; core/presentation domain его не видит. Adapter клонирует preset на runtime и fail-fast проверяет единственную usable запись `Rain`, ненулевой prefab с `ParticleSystem` и положительный `maxEmission`.

## Attach lifecycle

`Attach()` должен:

1. получить serialized `EnviroManager`, camera и binding asset;
2. подтвердить ровно один manager;
3. проверить `ENVIRO_HDRP`, `volumeHDRP`, non-null project-owned lab `VolumeProfile` и регистрацию `EnviroHDRPRenderer`;
4. проверить наличие required modules;
5. отфильтровать/диагностировать null vendor weather entries;
6. выключить autonomous time: `Time.Settings.simulate = false`;
7. выключить zone auto-weather, если zone присутствует;
8. обеспечить silent policy для Audio module;
9. открыть capability flags только для реально доступных modules;
10. не менять vendor assets.

`Detach()` должен:

- отписаться от vendor events;
- остановить/удалить временные Enviro audio/effects, созданные adapter-ом;
- освободить adapter-owned references;
- не оставлять второй manager/volume после reload;
- не удалять project-owned logical state.

Manager prefab имеет `dontDestroyOnLoad: false`; WeatherLab lifecycle остаётся scene-bound. Для будущего production Bootstrap lifetime решение принимается отдельно в 07B/07C.

## Present semantics

`Present(frame)`:

1. запускает `EnvironmentPresentationValidator.Validate`;
2. отклоняет invalid frame с bounded diagnostics;
3. игнорирует revision, который уже применён;
4. разрешает stable binding ID в typed vendor asset;
5. синхронизирует date/time через `SetDateTime`, не включая simulation;
6. применяет typed `ChangeWeather`/`ChangeWeatherInstant` согласно transition policy;
7. назначает `Quality.Settings.defaultQuality` только при изменении tier;
8. выполняет lightning visual только при новом lightning sequence;
9. публикует status/capabilities/diagnostics без gameplay side effects.

Текущая bounded реализация применяет weather мгновенно через `ChangeWeatherInstant`. Transition duration больше нуля фиксируется warning. `EnvironmentRefresh` capability намеренно не объявлена: WeatherLab использует `EnvironmentRefreshRequest.None`, поэтому финальный preflight не создаёт warning. Только внешний non-None запрос фиксируется warning и не выполняет vendor reflection API или reflection-based fallback.

Unchanged-value spam каждый rendered frame запрещён. Adapter применяется по revision/state changes.

## Vendor configuration safety

`EnviroManager.CreateHDRPVolume()` в Editor ищет vendor profile по имени и назначает его как `sharedProfile`. В player build helper возвращает `null`, если volume не был serialized заранее. Sky/Lighting/Fog modules могут вызывать `sharedProfile.Add<T>()`.

Реализованный WeatherLab builder:

- создаёт/сохраняет project-owned HDRP VolumeProfile под development root;
- назначает её в serialized `EnviroManager.volumeHDRP`;
- не изменяет vendor profile;
- не добавляет HDRP-native volumetric cloud owner;
- валидирует managers, profiles и duplicate environment owners.

`Enviro3EnvironmentAdapter` клонирует configuration и module ScriptableObjects на runtime, ждёт один frame после `EnviroManager.Start`, захватывает live clones, отключает autonomous time/lightning, обнуляет Enviro audio и уничтожает owned clones при teardown/`OnDestroy`. При unload он явно освобождает native cloud resources, но не вызывает небезопасный `DestroyImmediate` для уже уничтожаемых scene objects. Actual WeatherLab PlayMode test подтвердил lifecycle, повторные attach/detach, одну manager/volume/light authority, один cloud-module cleanup и отсутствие double-destroy.

Default configuration также содержит autonomous time и Effects block с отсутствующими Additional Weather Pack prefab/preset GUID, а его имена `Rain - ...` не совпадают с exact key `Rain`, который используют базовые `Rain.asset` и `Storm.asset`. Их нельзя исправлять на месте. Integration binding asset явно назначает базовый `Default Effects Preset.asset`, а adapter клонирует его вместо `sourceConfiguration.Effects`. Это устраняет null/mismatched precipitation chain без vendor patch или импорта Additional Weather Pack.

## Audio и lightning

Audio capability намеренно не входит в project-neutral presentation flags 07A. Enviro audio отключён; будущий project weather domain публикует typed weather audio events в `IAudioBackend`/Wwise boundary.

Lightning contract содержит только visual request (`sequence`, position, intensity). Adapter временно отсоединяет Enviro Audio и подставляет один переиспользуемый runtime-клон flash-материала перед публичным `CastLightningBolt`, поэтому серия запросов не создаёт линейно растущее число native materials, vendor thunder не стартует и shared vendor material не меняется. Damage, fire, NPC/player реакция и strike selection не входят в adapter.

## Required diagnostics

Минимальные failure cases:

- manager отсутствует/дублирован;
- HDRP renderer отсутствует в effective Global Settings registration;
- volume/profile отсутствует;
- required module отсутствует;
- explicit Effects source отсутствует или не содержит usable exact key `Rain`;
- stable binding ID неизвестен/дублирован;
- vendor binding asset null;
- time simulation осталась включённой;
- Enviro audio активен;
- competing sky/cloud/fog/directional-light owner;
- repeated lightning/refresh sequence;
- lifecycle cleanup incomplete.

Adapter/bindings/builder compile, scene generation и automated preflight: `PASS_BY_LOG`; post-remediation builder/final fresh preflight находятся в `M07A_WeatherLab_RainFix.log` и `M07A_Final_Preflight_RainFix_2.log`, return code 0. Targeted Weather/Enviro suite: `57/57 PASS`; final actual WeatherLab smoke `M07A_EnviroIntegration_RainFix_Final.xml` подтверждает созданный и играющий Rain ParticleSystem, `particleCount > 0` и emission `0.5`/`1.0` для Rain/Storm. Пользователь принял fog, lightning и капли после исправления без capture artifacts. Low/High comparison, captures и performance остаются `PENDING`.
