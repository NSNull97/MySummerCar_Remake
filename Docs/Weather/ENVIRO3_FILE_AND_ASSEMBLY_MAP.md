# Enviro 3: карта файлов и assemblies

Дата среза: 2026-07-17.

## Vendor assemblies

| Assembly | Полный путь asmdef | Platform | Прямые references |
|---|---|---|---|
| `Enviro3.Runtime` | `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Enviro 3 - Sky and Weather\Scripts\Runtime\Enviro3.Runtime.asmdef` | все | URP Runtime, Core RP Runtime, HDRP Runtime |
| `Enviro3.Editor` | `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Enviro 3 - Sky and Weather\Scripts\Editor\Enviro3.Editor.asmdef` | Editor | `Enviro3.Runtime`, HDRP Editor, Core RP Editor |

GUID mapping подтверждён локальными package metas:

| GUID | Assembly |
|---|---|
| `8990947d903fec847b19d9f51781afb1` | `Enviro3.Runtime` |
| `15fc0a57446b3144c949da3e2b9737a9` | `Unity.RenderPipelines.Universal.Runtime` |
| `df380645f10b7bc4b97d4f5eb6303d95` | `Unity.RenderPipelines.Core.Runtime` |
| `457756d89b35d2941b3e7b37b4ece6f1` | `Unity.RenderPipelines.HighDefinition.Runtime` |
| `78bd2ddd6e276394a9615c203e574844` | `Unity.RenderPipelines.HighDefinition.Editor` |
| `3eae0364be2026648bf74846acb8a731` | `Unity.RenderPipelines.Core.Editor` |

Оба vendor asmdef имеют `autoReferenced: true`. Vendor asmdefs изменять нельзя.

## Runtime source map

Все пути ниже начинаются с:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Enviro 3 - Sky and Weather\Scripts\Runtime`

| Область | Основные файлы |
|---|---|
| Composition/lifecycle | `Base\EnviroManager.cs`, `Base\EnviroManagerBase.cs`, `Base\EnviroConfiguration.cs`, `Base\EnviroHelper.cs` |
| HDRP/Built-in/URP renderer | `Base\Renderer\EnviroHDRPRenderer.cs`, `EnviroRenderer.cs`, `EnviroURPRenderFeature.cs`, `EnviroURPRenderPass.cs` |
| Time | `Modules\Time\EnviroTimeModule.cs` |
| Weather/presets/zones | `Modules\Weather\EnviroWeatherModule.cs`, `EnviroWeatherType.cs`, `EnviroZone.cs` |
| Sky | `Modules\Sky\EnviroSkyModule.cs`, `EnviroHDRPSky.cs`, `EnviroHDRPSkyRenderer.cs` |
| Clouds | `Modules\VolumetricClouds\EnviroVolumetricCloudsModule.cs`, `Modules\FlatClouds\EnviroFlatCloudsModule.cs` |
| Fog | `Modules\Fog\EnviroFogModule.cs`, `EnviroVolumetricFogLight.cs` |
| Lighting | `Modules\Lighting\EnviroLightingModule.cs` |
| Reflections | `Modules\Reflections\EnviroReflectionsModule.cs`, `EnviroReflectionProbe.cs` |
| Environment/wind/wetness | `Modules\Environment\EnviroEnvironmentModule.cs` |
| Effects | `Modules\Effects\EnviroEffectsModule.cs` |
| Lightning | `Modules\Lightning\EnviroLightningModule.cs`, `Lightning.cs` |
| Audio | `Modules\Audio\EnviroAudioModule.cs` |
| Quality | `Modules\Quality\EnviroQualityModule.cs`, `EnviroQuality.cs` |
| Events | `Modules\Events\EnviroEventModule.cs` |
| Optional visuals | `Modules\Aurora\EnviroAuroraModule.cs` |

## Key serialized assets

| Назначение | Полный путь |
|---|---|
| Main prefab | `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Enviro 3 - Sky and Weather\Enviro 3.prefab` |
| Default configuration | `...\Profiles\Configurations\Default Enviro Configuration.asset` |
| HDRP profile | `...\Profiles\HDRP\Enviro HDRP Sky and Fog Volume.asset` |
| Base weather types | `...\Profiles\Weather Types\` |
| Quality profiles | `...\Profiles\Quality\` |
| Particle prefabs | `...\Prefabs\Particle Systems\` |
| Lightning prefab | `...\Prefabs\Lightning\LightningStrike.prefab` |
| Shaders/resources | `...\Resources\Shader\` |
| Audio | `...\SFX\` |

Base weather assets в установленном корне:

- `Clear Sky.asset`;
- `Cloudy 1.asset`, `Cloudy 2.asset`, `Cloudy 3.asset`;
- `Foggy.asset`;
- `Rain.asset`;
- `Snow.asset`;
- `Storm.asset`.

Snow не входит в bounded 07A smoke scope.

## Docs и examples

- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Enviro 3 - Sky and Weather\Documentation.pdf`;
- `...\version.txt`;
- `...\Sample\Scene\Sample_BuiltIn.unity`;
- `...\Sample\Scene\Sample_URP.unity`;
- `...\Sample\Scene\Sample_HDRP.unity`;
- `...\Sample\Prefab\UI Sample.prefab`;
- sample terrain/material/volume assets под `...\Sample\`.

Sample content является vendor reference, не WeatherLab template и не должен попадать в shipping Build Settings.

## Third-party support map

| Integration | Путь | Статус для проекта |
|---|---|---|
| Mirror | `Scripts\ThirdPartySupport\Mirror\` | non-goal; `ENVIRO_MIRROR_SUPPORT` не включён |
| WAPI | `Scripts\ThirdPartySupport\WAPI\` | optional; `WORLDAPI_PRESENT` не подтверждён |
| MicroSplat | `Scripts\ThirdPartySupport\Microsplat\` | не утверждён как production dependency |

MicroSplat helper публикует global wetness/snow values, но это не project-owned wetness contract и не используется в 07A.

## Project-owned boundary

Vendor-neutral assembly уже существует:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Presentation\Runtime\MSC.Weather.Presentation.Runtime.asmdef`

Namespace: `MSC.Weather.Presentation`. В asmdef нет ссылок на Enviro assemblies.

Связанные contract files:

- `EnvironmentBindingId.cs`;
- `EnvironmentPresentationBindingDefinition.cs`;
- `EnvironmentPresentationFrame.cs`;
- `EnvironmentPresentationTypes.cs`;
- `EnvironmentPresentationRequests.cs`;
- `EnvironmentPresentationStatus.cs`;
- `EnvironmentPresentationDiagnostic.cs`;
- `EnvironmentPresentationValidator.cs`;
- `IEnvironmentPresentationAdapter.cs`;
- `IEnvironmentPresentationCameraTarget.cs`.

Тестовая assembly:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Tests\EditMode\WeatherPresentation\MSC.Weather.Presentation.Tests.EditMode.asmdef`

напрямую ссылается только на `MSC.Weather.Presentation.Runtime`.

В текущем рабочем дереве реализованы следующие project-owned границы:

| Assembly | Полный путь | References / constraints | Назначение |
|---|---|---|---|
| `MSC.Weather.Enviro3Integration` | `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Runtime\MSC.Weather.Enviro3Integration.asmdef` | `MSC.Weather.Presentation.Runtime`, `Enviro3.Runtime`; `ENVIRO_3`, `ENVIRO_HDRP` | Единственная runtime assembly, которой разрешены Enviro types |
| `MSC.Weather.Enviro3Integration.Editor` | `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Editor\MSC.Weather.Enviro3Integration.Editor.asmdef` | lab/runtime/presentation/vendor/HDRP/Core; Editor only | builder, preflight, diagnostics |
| `MSC.Weather.Enviro3Integration.Tests.EditMode` | `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Tests\EditMode\Enviro3Integration\MSC.Weather.Enviro3Integration.Tests.EditMode.asmdef` | integration/presentation/lab/vendor/Core; Editor only | exact vendor mappings, lifecycle, actual WeatherLab smoke и shipping gate |
| `MSC.Development.WeatherLab` | `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab\Runtime\MSC.Development.WeatherLab.asmdef` | только `MSC.Weather.Presentation.Runtime`; `ENVIRO_3`, `ENVIRO_HDRP`; `autoReferenced: false` | vendor-neutral lab controller, anchors и CPU probe |

Ключевые integration files:

- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Runtime\Enviro3EnvironmentAdapter.cs`;
- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Runtime\Enviro3EnvironmentBindings.cs`;
- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Editor\WeatherLabBuilder.cs`;
- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Editor\Enviro3PreflightValidator.cs`;
- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Editor\Enviro3PreflightWindow.cs`;
- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Editor\WeatherLabBuildGuard.cs`.

WeatherLab runtime scaffolding и generated output присутствуют:

- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab\Scenes\WeatherLab.unity`;
- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab\Content\Profiles\WeatherLabEnviro3Bindings.asset`;
- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab\Content\Profiles\WeatherLabHDRPVolume.asset`;
- 10 project-owned `WL_*.mat` materials под `Content\Materials`.

Builder подтвердил 3 cameras, 6 capture anchors, exclusion from Build Settings и отсутствие donor baseline. Финальный fresh-process preflight подтвердил canonical vendor fingerprint после всех runtime tests, три разрешённые Enviro-referencing assemblies (runtime, Editor tooling, Editor-only integration tests) и single owners; markers находятся в `E:\GAYmDev_Studio\MySummerCar_Remake\Logs\M07A_WeatherLab_Final_4.log` и `M07A_Final_Preflight_5.log`.

## Shipping boundary

Vendor dependency локальна и исключена из Git. WeatherLab должен размещаться только под project-owned development root, например:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab`

Сцена и Enviro sample scenes должны полностью отсутствовать в `EditorBuildSettings.scenes`, включая disabled entries. Отдельный validator должен проверять это до и после builder operation.
