# Enviro 3: аудит локальной установки для Milestone 07A

Дата среза: 2026-07-17
Репозиторий: `E:\GAYmDev_Studio\MySummerCar_Remake`
Статус: базовый пакет присутствует; integration compile, WeatherLab build, automated preflight и headless runtime smoke завершились успешно. Пользователь подтвердил общее отображение, композицию тумана, молнии и капли Rain/Storm после remediation. Capture artifacts, отдельный Low/High comparison и performance measurements ещё не выполнены.

## Итог

Enviro 3 установлен как локальный Asset Store payload, а не как UPM dependency:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Enviro 3 - Sky and Weather`

Корень существует, содержит prefab, исходники, asmdef, профили, shaders, SFX, документацию и sample-сцены. Он намеренно исключён из Git правилами:

- `/Assets/Enviro 3 - Sky and Weather/`;
- `/Assets/Enviro 3 - Sky and Weather.meta`;
- `/Assets/Enviro 3 - Additional Weather Pack/`;
- `/Assets/Enviro 3 - Additional Weather Pack.meta`.

`git ls-files` не возвращает файлов vendor-корня. Поэтому Git сам по себе не доказывает отсутствие локальных изменений vendor payload; нужны сохранённые hashes и повторный file manifest.

## Платформа проекта

| Параметр | Подтверждённое значение | Evidence |
|---|---|---|
| Unity | `6000.3.11f1 (3000ef702840)` | `E:\GAYmDev_Studio\MySummerCar_Remake\ProjectSettings\ProjectVersion.txt` |
| HDRP | `17.3.0` | `E:\GAYmDev_Studio\MySummerCar_Remake\Packages\manifest.json` |
| Core RP | transitive dependency HDRP/URP `17.3.0` | `Packages\packages-lock.json`, локальный PackageCache |
| URP compatibility dependency | `17.3.0` | `E:\GAYmDev_Studio\MySummerCar_Remake\Packages\manifest.json` |
| Active render pipeline | HDRP asset GUID `b9f3086da92434da0bc1518f19f0ce86` | `E:\GAYmDev_Studio\MySummerCar_Remake\ProjectSettings\GraphicsSettings.asset` |
| Standalone defines | `ENVIRO_3;ENVIRO_HDRP` | `E:\GAYmDev_Studio\MySummerCar_Remake\ProjectSettings\ProjectSettings.asset` |
| `ENVIRO_URP` | отсутствует | тот же файл |

URP установлен только потому, что vendor runtime asmdef безусловно ссылается на URP Runtime, а vendor URP shaders импортируются даже в HDRP-проекте. Ни Graphics Settings, ни Quality tiers не переключались на URP.

## Состав установки

- главный prefab: `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Enviro 3 - Sky and Weather\Enviro 3.prefab`;
- default configuration: `...\Profiles\Configurations\Default Enviro Configuration.asset`;
- HDRP volume profile: `...\Profiles\HDRP\Enviro HDRP Sky and Fog Volume.asset`;
- runtime asmdef: `...\Scripts\Runtime\Enviro3.Runtime.asmdef`;
- editor asmdef: `...\Scripts\Editor\Enviro3.Editor.asmdef`;
- PDF: `...\Documentation.pdf`;
- changelog/version evidence: `...\version.txt`;
- sample scenes: `Sample_BuiltIn.unity`, `Sample_URP.unity`, `Sample_HDRP.unity`.

Sample-сцены не найдены в `ProjectSettings\EditorBuildSettings.asset`. WeatherLab также отсутствует в Build Settings; это обязательная non-shipping граница, а не признак отсутствия реализации.

## Additional Weather Pack

Внешний read-only payload доступен по пути:

`E:\GAYmDev_Studio\Additional\Enviro 3 - Additional Weather Pack`

Он содержит 135 файлов, 226 809 050 байт. `HowToUse.txt` требует Enviro `3.0.7a+`. Pack в проект не импортирован.

При этом `Default Enviro Configuration.asset` уже содержит 30 GUID-ссылок на этот внешний pack: 13 weather types, 15 effect prefabs, четыре wind clips и связанные configuration assets пересекаются с payload. Часть категорий перекрывается в одном объекте, поэтому число ссылок и число уникальных внешних файлов не следует смешивать. Внутри текущего `Assets` эти GUID не разрешаются.

Следствия:

- нельзя считать default target weather рабочим: он указывает на отсутствующий `Fog - Mist Light`;
- адаптер обязан использовать явные project binding IDs и проверенные базовые `EnviroWeatherType` assets;
- null entries требуется диагностировать, но нельзя исправлять vendor configuration;
- Additional Weather Pack не нужен для bounded WeatherLab и не должен импортироваться автоматически.

## Defines и HDRP setup

`ENVIRO_3` добавляется vendor editor-кодом `EnviroDefineSymbol.cs` для доступных build target groups. Для Standalone дополнительно присутствует `ENVIRO_HDRP`; на других перечисленных платформах зафиксирован только `ENVIRO_3`.

`ENVIRO_HDRP` включает:

- `EnviroManager.volumeHDRP` и `CreateHDRPVolume()`;
- `EnviroHDRPRenderer`;
- `EnviroHDRPSky`/`EnviroHDRPSkyRenderer`;
- HDRP branches в Sky, Lighting, Fog и VolumetricClouds modules.

Текущий HDRP Global Settings asset:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Settings\HDRPDefaultResources\HDRenderPipelineGlobalSettings.asset`

содержит запись `Enviro.EnviroHDRPRenderer, Enviro3.Runtime` в custom post-process configuration. Automated preflight и реальный PlayMode lifecycle WeatherLab подтвердили загрузку одного manager/volume/directional owner. Пользовательский smoke подтвердил общее отображение и fog composition без capture artifacts; отдельные clouds/exposure/night criteria остаются частично неподтверждёнными.

В текущем рабочем дереве также присутствуют project-owned assemblies:

- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Runtime\MSC.Weather.Enviro3Integration.asmdef`;
- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Editor\MSC.Weather.Enviro3Integration.Editor.asmdef`;
- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Tests\EditMode\Enviro3Integration\MSC.Weather.Enviro3Integration.Tests.EditMode.asmdef` — Editor-only integration tests;
- `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab\Runtime\MSC.Development.WeatherLab.asmdef`.

Сцена `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab\Scenes\WeatherLab.unity` создана builder-ом вместе с 10 material assets, project-owned HDRP VolumeProfile и Enviro bindings. Актуальный post-remediation marker: `M07A_WEATHERLAB_BUILD_OK version=1.0.1 cameras=3 anchors=6 buildSettings=excluded donorBaseline=absent`.

## Compile evidence

Фактически выполненный batchmode compile:

`E:\GAYmDev_Studio\MySummerCar_Remake\Logs\M07A_EnviroHDRP_Compile.log`

Завершился с return code `0`, script compilation errors не зафиксированы. В логе присутствуют vendor warnings об obsolete API:

- `Object.FindObjectOfType`/`FindObjectsOfType`;
- `HDAdditionalLightData.SetIntensity(float)`;
- устаревший `VolumeComponentEditorAttribute`;
- устаревшие BuildTargetGroup define APIs.

Также есть licensing handshake noise, после которого batchmode завершился успешно. Этот конкретный compile-запуск не проверял runtime PlayMode, visual smoke или development build; runtime проверен отдельным Test Runner ниже.

После появления integration выполнены:

- `E:\GAYmDev_Studio\MySummerCar_Remake\Logs\M07A_Integration_Compile.log` — return code `0`;
- `E:\GAYmDev_Studio\MySummerCar_Remake\Logs\M07A_WeatherLab_Build.log` — первый scene build дошёл до `M07A_WEATHERLAB_BUILD_OK`, затем preflight выявил несовпадение алгоритма сортировки fingerprint и завершился return code `1`;
- Pre-remediation baseline `E:\GAYmDev_Studio\MySummerCar_Remake\Logs\M07A_WeatherLab_Final_4.log` — повторяемый build/preflight с persisted VolumeProfile и diagnostics export завершился return code `0`; повторный builder сохранил SHA-256 profile `d69c1bbd3aa5eea733e7ee89f6bdf2a478b9ea02f8507d7dd96a113b134c4cd6`;
- Pre-remediation baseline `E:\GAYmDev_Studio\MySummerCar_Remake\Logs\M07A_Final_Preflight_5.log` — fresh-process preflight после прежних runtime/full-suite проверок завершился return code `0`;
- Initial pre-remediation `E:\GAYmDev_Studio\MySummerCar_Remake\Logs\M07A_Weather_EditMode_Final.log` и `TestResults\M07A_Weather_EditMode_Final.xml` — vendor-neutral, integration lifecycle и прежний WeatherLab runtime smoke: `56/56 PASS`; появление rain particles тогда ещё не проверялось.
- `Logs\M07A_WeatherLab_RainFix.log` и финальный `Logs\M07A_Final_Preflight_RainFix_2.log` — builder `1.0.1` и fresh-process preflight после Rain/Storm runtime smoke: return code `0`, canonical vendor fingerprint сохранён;
- `TestResults\M07A_EnviroIntegration_RainFix_Final.xml` и `TestResults\M07A_WeatherPresentation_RainFix.xml` — post-remediation targeted suite: `9/9 + 48/48 = 57/57 PASS`; runtime smoke проверяет фактический Rain ParticleSystem, `particleCount > 0` и emission `0.5`/`1.0` для Rain/Storm;
- `TestResults\M07A_EditMode_RainFix.xml` — полный post-remediation run: `235/239 PASS`, только четыре ранее известные несвязанные Garage/World failure.

Первый validator использовал ordinal sorting и получил `ffc848d3...`; исходный exporter с ru-RU `Sort-Object` воспроизводил `9a4e8bab...` на тех же 538 files / 305 967 970 bytes. После фикса canonical sorting финальный marker: `M07A_ENVIRO_PREFLIGHT_OK vendorFiles=538 vendorBytes=305967970 vendorFingerprint=9a4e8bab... integrationAssemblies=3 weatherLab=excluded owners=single`. Третья разрешённая assembly — Editor-only integration tests; vendor-neutral contracts и WeatherLab runtime Enviro не видят.

## Vendor boundary snapshot

До документационных изменений был записан baseline:

- 538 файлов;
- 305 967 970 байт;
- 223 non-meta и 315 meta файлов;
- `version.txt` SHA-256: `F4AEE13D2017B552DA61B5109B46BBBAA2A2592A99F4C03C9BF854AF3C92FCC7`;
- `EnviroManager.cs` SHA-256: `15A35BD1306599D57F1F9CE69FA007F9A4C47766F280D3545519F93088D51D7C`;
- runtime asmdef SHA-256: `5794678280E876518E20F038024992EF36CEAE834C0A0DDF45766736C893CC82`;
- editor asmdef SHA-256: `701251F3A2184CA1CA01F06486C49C124CEA29F7AFF0FF814994C515AA10F686`;
- `Documentation.pdf` SHA-256: `451311DF5302E0E31BFD2CAA17589AA15F2B092AF02D8851756FC24398DF1574`.

Сохранённый ранее full-manifest fingerprint в `ENVIRO3_COMPATIBILITY_BLOCKER.md`: `9a4e8bab6bdf231c415fc8e60f3988f12f221cc20dfbfc0b7090feb14f431af3`. Финальный preflight повторил тот же count/bytes/fingerprint после compile, runtime smoke и lightning request. Diagnostics: `Logs\M07A_Enviro3PreflightDiagnostics.json` (`passed: true`, errors/warnings empty).

Первый lightning runtime smoke выявил, что vendor `Lightning` пишет `_Intensity` прямо в shared `planeMat`. Материал был восстановлен из побайтно совпадающей локальной установки того же package payload, а adapter переведён на runtime clone flash-материала. Повторный lightning smoke больше не меняет asset. Итоговое число vendor-файлов, отличающихся от baseline: `0`.

## Локальная документация

`Documentation.pdf` физически присутствует, размер 1 131 598 байт. Все 28 страниц были извлечены и прочитаны локальным bundled PDF runtime без изменения vendor payload. Документация подтверждает `ENVIRO_HDRP` и регистрацию `Enviro.EnviroHDRPRenderer` в HDRP Custom Post Process до transparent rendering; точные вызываемые signatures дополнительно сверены с локальным C# source.

## Текущий gate

| Gate | Статус |
|---|---|
| Пакет присутствует | PASS |
| Unity импортирует C# assemblies | PASS |
| Standalone `ENVIRO_HDRP` | PASS |
| Shader missing-includes после URP dependency | в последнем compile log не обнаружены |
| Enviro HDRP custom post process записан в Global Settings | PASS_BY_CONFIG_AND_HEADLESS_RUNTIME; GENERAL_VISUAL_PASS_CAPTURE_MISSING |
| Additional Pack импортирован | NO, намеренно |
| Project-owned adapter и WeatherLab builder/scaffolding созданы | PASS_BY_COMPILE_AND_RUNTIME_TEST |
| WeatherLab scene создана | PASS_BY_BUILDER |
| Полный Enviro preflight | PASS (`M07A_WeatherLab_RainFix.log`; final fresh-process `M07A_Final_Preflight_RainFix_2.log`) |
| Vendor-neutral + integration + WeatherLab runtime tests | PASS (`57/57`) |
| Smoke states визуально подтверждены | MANUAL_PASS_BY_USER_CAPTURE_MISSING; FOG/LIGHTNING/RAIN_PASS; LOW_HIGH_COMPARISON_PENDING |
| Captures/performance | PENDING |
