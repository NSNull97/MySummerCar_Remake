# Enviro 3: доказательства локального public API

Дата среза: 2026-07-17. Все signatures взяты из локального vendor payload:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Enviro 3 - Sky and Weather\Scripts`

Этот документ фиксирует наличие API, но не утверждает, что каждый путь прошёл PlayMode smoke test.

## Namespace и composition root

Основной namespace: `Enviro`.

| API | Signature/field | Локальный источник | Решение интеграции |
|---|---|---|---|
| Manager | `EnviroManager : EnviroManagerBase` | `Scripts\Runtime\Base\EnviroManager.cs:11` | Единственный manager на WeatherLab/process lifetime |
| Singleton | `public static EnviroManager instance` | `EnviroManager.cs:15` | Допустим только внутри integration assembly; project contracts singleton не видят |
| Camera | `public void ChangeCamera(Camera cam)` | `EnviroManager.cs:237` | Вызывается при переключении lab-camera |
| Modules | public `Time`, `Lighting`, `Reflections`, `Sky`, `Fog`, `VolumetricClouds`, `FlatClouds`, `Weather`, `Aurora`, `Audio`, `Effects`, `Lightning`, `Quality`, `Environment` | `EnviroManagerBase.cs:37-50` | Capability discovery через null/module-state checks |
| Manager events | `OnHourPassed`, `OnDayPassed`, `OnYearPassed`, `OnWeatherChanged`, `OnZoneWeatherChanged`, `OnSeasonChanged`, `OnNightTime`, `OnDayTime` | `EnviroManager.cs:83-99` | Только presentation diagnostics; не authority gameplay state |

Vendor singleton использует obsolete `FindObjectOfType<EnviroManager>()`. Project-owned код не должен повторять этот способ binding; WeatherLab передаёт manager через serialized reference.

## Time/date

Источник: `Scripts\Runtime\Modules\Time\EnviroTimeModule.cs`.

- `EnviroTime.Settings.simulate` управляет автоматическим ходом времени;
- `SetDateTime(int sec, int min, int hours, int day, int month, int year)`;
- `SetTimeOfDay(float tod)` принимает часы, например `12.5`;
- `GetTimeOfDay()` и `GetUniversalTimeOfDay()`;
- `UpdateSunAndMoonPosition()`.

`UpdateModule()` продвигает время только при `Settings.simulate && Application.isPlaying`. Default configuration содержит `simulate: 1` и цикл 10 минут, поэтому adapter обязан установить `simulate = false` до применения фиксированных WeatherLab frames. Project-owned logical time остаётся authority.

## Weather и presets

Источник: `Scripts\Runtime\Modules\Weather\EnviroWeatherModule.cs`.

- `ChangeWeather(EnviroWeatherType type)`;
- `ChangeWeather(string typeName)`;
- `ChangeWeather(int index)`;
- `ChangeWeatherInstant(EnviroWeatherType type)`;
- `ChangeWeatherInstant(int index)`.

Для runtime adapter разрешён typed overload с заранее проверенным `EnviroWeatherType`. String/index overloads не соответствуют stable binding policy: display names и порядок массива могут меняться.

`EnviroWeatherType` — `ScriptableObject` с overrides для:

- volumetric/flat clouds;
- lighting;
- fog;
- effects;
- audio;
- aurora;
- environment (temperature, wetness, snow, wind);
- lightning.

Отдельного универсального `SetRainIntensity(float)` в локальном API не обнаружено. Осадки представлены weather preset/effect overrides. Нормализованный project precipitation target должен переводиться в bounded preset mapping, а не напрямую протекать в core API.

## Zones

Источник: `Scripts\Runtime\Modules\Weather\EnviroZone.cs`.

- `autoWeatherChanges` по умолчанию `true`;
- `ChangeZoneWeather(EnviroWeatherType)`;
- `ChangeZoneWeatherInstant(EnviroWeatherType)`;
- `AddWeatherType`/`RemoveWeatherZoneType`.

WeatherLab не использует autonomous zone schedule. Если zone нужен для корректной работы manager, `autoWeatherChanges` должен быть выключен, а weather задаётся adapter-командой.

## Quality

Источники:

- `Scripts\Runtime\Modules\Quality\EnviroQualityModule.cs`;
- `Scripts\Runtime\Modules\Quality\EnviroQuality.cs`.

`EnviroQualityModule.Settings.defaultQuality` и `Settings.Qualities` публичны. `UpdateModule()` переносит параметры выбранного `EnviroQuality` в clouds, fog, flat clouds и aurora. Публичного `SetQuality(...)` метода нет. Integration assembly должен назначать заранее serialized `EnviroQuality` reference и не искать профиль по имени.

Базовые локальные assets: `Low`, `Medium`, `High`, `Ultra`, `Insane`.

## Sky, clouds и HDRP renderer

Источники:

- `Scripts\Runtime\Modules\Sky\EnviroSkyModule.cs`;
- `Scripts\Runtime\Modules\Sky\EnviroHDRPSky.cs`;
- `Scripts\Runtime\Base\Renderer\EnviroHDRPRenderer.cs`;
- `Scripts\Runtime\Modules\VolumetricClouds\EnviroVolumetricCloudsModule.cs`.

При `ENVIRO_HDRP`:

- `UpdateHDRPSky()` получает или добавляет `VisualEnvironment` и `EnviroHDRPSky` в `volumeHDRP.sharedProfile`;
- Enviro sky type устанавливается в vendor custom ID `990`;
- `EnviroHDRPRenderer : CustomPostProcessVolumeComponent`;
- injection point задан как `(CustomPostProcessInjectionPoint)0`, что в HDRP 17.3 соответствует `AfterOpaqueAndSky`;
- renderer объединяет Enviro volumetric clouds и Enviro height fog.

Наличие class/Volume component не регистрирует renderer автоматически в HDRP Global Settings. В рамках 07A project-owned настройка явно зарегистрировала `Enviro.EnviroHDRPRenderer, Enviro3.Runtime`; structural preflight подтверждает запись и отсутствие duplicate owner. Фактический визуальный output всё ещё требует manual captures.

## Fog

Источник: `Scripts\Runtime\Modules\Fog\EnviroFogModule.cs`.

Доступны:

- custom height fog (`Settings.fog`);
- custom volumetrics (`Settings.volumetrics`);
- HDRP control flags `controlHDRPFog` и `controlHDRPVolumetrics`;
- `RenderHeightFogHDRP(...)`;
- point/spot volumetric light registration через `AddLight`/`RemoveLight`.

Vendor default configuration содержит `fog: true`, `volumetrics: true`, `controlHDRPFog: false`, `controlHDRPVolumetrics: false`. Это доказательство исходного vendor baseline, а не фактической настройки WeatherLab.

В bounded 07A integration runtime clone принудительно держит `controlHDRPFog=true` и `controlHDRPVolumetrics=true`: Enviro остаётся presentation authority, а единственный HDRP `Fog` в project-owned profile служит управляемым output substrate. Два независимо управляемых fog owner не запускаются. Пользователь подтвердил WeatherLab fog/water composition без неприемлемых перекрытий; статус `MANUAL_PASS_BY_USER_CAPTURE_MISSING`, production-world и underwater compatibility остаются `PENDING`.

## Lighting и exposure

Источник: `Scripts\Runtime\Modules\Lighting\EnviroLightingModule.cs`.

Enviro может:

- создать один или два directional lights в зависимости от `LightingMode`;
- управлять HDRP light intensity/color temperature;
- получить или добавить `IndirectLightingController`;
- получить или добавить HDRP `Exposure`;
- при `controlExposure` задавать fixed exposure по `sceneExposure` curve;
- обновлять ambient lighting.

WeatherLab должен использовать single lighting mode и не загружать Bootstrap `Directional Sun`/`BootstrapGlobalVolume` одновременно.

## Reflections

Источники:

- `Scripts\Runtime\Modules\Reflections\EnviroReflectionsModule.cs`;
- `Scripts\Runtime\Modules\Reflections\EnviroReflectionProbe.cs`.

Публичные операции:

- `RenderGlobalReflectionProbe(bool forced = false)`;
- `RefreshReflection(bool timeSlice = false)`;
- `RefreshUnity()`, `RefreshInstant(...)`, `RefreshOvertime(...)`.

Adapter 07A не объявляет `EnvironmentRefresh` capability и не вызывает vendor reflection refresh API. WeatherLab подаёт `EnvironmentRefreshRequest.None`; внешний non-None запрос только отклоняется bounded warning. Per-frame forced refresh запрещён.

## Environment, wind, wetness и snow

Источник: `Scripts\Runtime\Modules\Environment\EnviroEnvironmentModule.cs`.

Public settings включают:

- `wetness`, `wetnessTarget`, accumulation/dry speeds;
- `snow`, `snowTarget`, accumulation/melt speeds;
- `windDirectionX/Y`, `windSpeed`, `windTurbulence`;
- temperature/season fields.

Эти поля доказывают presentation capability vendor-пакета, но не передают authority. Project-owned scheduler/wetness/gameplay systems не должны читать Enviro как источник истины.

## Effects и precipitation

Источник: `Scripts\Runtime\Modules\Effects\EnviroEffectsModule.cs`.

- `CreateEffects()` инстанцирует configured particle prefabs;
- `GetEmissionRate(ParticleSystem)`;
- `SetEmissionRate(ParticleSystem, float)`;
- weather type effects overrides задают emission targets.

Project adapter не должен обращаться к effects по display name. Binding выполняется к typed weather assets и capability state.

## Lightning

Источник: `Scripts\Runtime\Modules\Lightning\EnviroLightningModule.cs`.

- `CastLightningBolt(Vector3 from, Vector3 to)`;
- `CastLightningBoltRandom()`;
- settings содержат `lightningStorm`, delay и spawn/target ranges.

Разрешён только presentation request. Vendor lightning callbacks не наносят damage и не определяют gameplay strike. Повтор одного sequence ID должен игнорироваться adapter-ом.

## Audio

Источник: `Scripts\Runtime\Modules\Audio\EnviroAudioModule.cs`.

- `CreateAudio()` создаёт `AudioSource` для ambient/weather/thunder clips;
- `PlayRandomThunderSFX()`;
- `ambientMasterVolume`, `weatherMasterVolume`, `thunderMasterVolume`;
- additive modifiers `ambientVolumeModifier`, `weatherVolumeModifier`, `thunderVolumeModifier`.

Default master volumes равны `1`. Modifiers равны `0`, что **не является mute**, потому что код использует `masterVolume + modifier`. WeatherLab и production integration должны отключать Audio module либо гарантировать master volumes `0` на runtime clone и проверять отсутствие играющих Enviro AudioSources.

## Необнаруженные/неподтверждённые API

- отдельный public deterministic weather scheduler contract;
- стабильные IDs у vendor presets;
- универсальный normalized precipitation setter;
- Wwise integration;
- gameplay lightning damage API, который допустимо использовать;
- production save/versioning API проекта;
- подтверждённый runtime water/underwater integration с текущим проектом.

Все перечисленное остаётся project-owned или `PENDING`.
