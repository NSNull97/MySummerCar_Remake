# WeatherLab: проектирование и setup

Дата среза: 2026-07-17.
Статус: runtime scaffolding, compile, scene/content build, automated preflight и precipitation-aware headless PlayMode smoke `PASS`; пользователь принял ручной visual smoke капель Rain/Storm. Captures, Low/High comparison и performance остаются `PENDING`.

## Назначение

WeatherLab — отдельная non-shipping сцена для Enviro preflight. Она не загружает donor runtime baseline, remaster cells, Bootstrap или Enviro sample scene.

Целевой путь:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab\Scenes\WeatherLab.unity`

Целевые development roots:

```text
E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab\Runtime
E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab\Content\Materials
E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab\Content\Profiles
```

Editor builder/preflight находятся в `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Weather\Enviro3Integration\Editor`. Target scene и generated profiles/materials созданы. Актуальный `M07A_WeatherLab_RainFix.log` содержит `M07A_WEATHERLAB_BUILD_OK` и `M07A_ENVIRO_PREFLIGHT_OK`, а итоговый `M07A_Final_Preflight_RainFix_2.log` подтверждает fresh-process reload после Rain/Storm runtime smoke; оба завершились return code 0. `M07A_WeatherLab_Final_4.log` и `M07A_Final_Preflight_5.log` сохранены только как pre-remediation baseline evidence. Headless runtime test прошёл все явные states/tiers и lifecycle, но не заменяет визуальные captures.

## Build exclusion

WeatherLab должна полностью отсутствовать в `ProjectSettings\EditorBuildSettings.asset`, даже как disabled entry. Builder проверяет исключение до и после сохранения. Enviro `Sample_BuiltIn`, `Sample_URP` и `Sample_HDRP` также не добавляются.

Рекомендуемый guard повторяет fail-fast pattern `WorldBaselineVehicleTraversalHarnessBuilder.AssertHarnessSceneIsExcludedFromBuildSettings()`.

## Состав и hierarchy

```text
WeatherLab_Root [WeatherLabSceneMarker]
  EnvironmentBackend
    Enviro3_Instance [EnviroManager, Enviro3EnvironmentAdapter]
      Enviro3_HDRPVolume [Volume]
  Geometry
    TerrainPatch_40x40
    AsphaltRoad_6x36
    GravelPatch_8x10
    DirtPatch_8x10
    PuddleDepression
    WaterProxy_8x12
    Building
      ExteriorShell
      InteriorRoom
      Doorway
      TransparentWindow
      Roof
  Vegetation
  Props
    OpaqueMaterialSamples
    TransparentMaterialSample
    StationaryVehicleProxy
  Cameras
    ExteriorCamera
    InteriorLookingOutCamera
    VehicleInteriorLookingOutCamera
  CaptureAnchors
    W07A-CAP-EXT-01
    W07A-CAP-INT-01
    W07A-CAP-VEH-01
    W07A-CAP-WATERFOG-01
    W07A-CAP-HEADLIGHTS-01
    W07A-CAP-PERF-01
  LightingFixtures
    Headlight_Left
    Headlight_Right
    InteriorPractical
  Diagnostics [WeatherLabStateController, WeatherLabPerformanceProbe]
    PerformanceMarker
```

Capture anchors должны иметь serialized project IDs. Object names используются только для Editor readability, не для runtime lookup.

## Layout

| Объект | Позиция/размер |
|---|---|
| Terrain | центр `(0,0,0)`, 40 x 40 м |
| Asphalt road | центр по X, вдоль Z, 6 x 36 м, `y=0.03` |
| Gravel | около `(-9,0,-5)`, 8 x 10 м |
| Dirt | около `(-9,0,7)`, 8 x 10 м |
| Water/depression | около `(10,0,8)`, 8 x 12 м; bottom `y=-0.25`, water `y=-0.12` |
| Building | около `(-10,0,10)`, footprint примерно 8 x 6 м |
| Vehicle proxy | около `(3,0,-8)`, вдоль дороги |
| Exterior camera | `(12,4.5,-14)`, смотрит на `(0,1,4)` |
| Interior camera | `(-10,1.65,8)`, смотрит через окно к `(2,1,8)` |
| Vehicle camera | `(3,1.25,-8)`, смотрит к `(3,1.1,6)` |

Размеры являются lab specification, не donor measurements.

## Материалы

Только project-owned HDRP/Lit:

- `WL_Ground.mat`;
- `WL_Asphalt.mat`;
- `WL_Gravel.mat`;
- `WL_Dirt.mat`;
- `WL_Wall.mat`;
- `WL_Trim.mat`;
- `WL_Metal.mat`;
- `WL_Glass.mat`;
- `WL_Water.mat`;
- `WL_Marker.mat`.

Donor textures/meshes и Enviro sample terrain не копируются. Допустим project-owned approved vegetation prefab, например `Assets\Game\World\Production\Prefabs\WR_SpruceTree.prefab`, после reference validation.

## Environment lifecycle

- scene открывается в `NewSceneMode.Single`;
- Enviro `dontDestroyOnLoad=false`;
- один manager, один serialized lab Volume, один project-owned lab VolumeProfile;
- manager получает активную camera через explicit binding/`ChangeCamera`;
- autonomous time выключен; WeatherLab не запускает zone scheduler и подаёт только явные state-команды;
- Enviro Audio disabled/silent;
- Effects module берётся из явно назначенного базового `Default Effects Preset.asset`; adapter клонирует его на runtime, поэтому `Rain`/`Storm` управляют существующим exact key `Rain`, не затрагивая отсутствующий Additional Weather Pack;
- WeatherLab 07A не отправляет refresh request; bounded adapter не объявляет `EnvironmentRefresh` и не вызывает vendor refresh API;
- reload/unload удаляет manager, effects, transient lightning, AudioSources и buffers;
- повторная загрузка не создаёт duplicate owners.

Vendor profile напрямую не используется как mutable `sharedProfile`. Builder создаёт project-owned copy, отключает HDRP-native cloud/fog conflicts и сериализует ссылку в manager, чтобы player build не зависел от Editor-only `AssetDatabase.FindAssets` внутри `GetDefaultSkyAndFogProfile`.

## Editor actions

Фактически реализованное меню:

- `Tools > MSC Remake > Enviro 3 Preflight > Build or Rebuild WeatherLab`;
- `Tools > MSC Remake > Enviro 3 Preflight > Open WeatherLab`;
- `Tools > MSC Remake > Enviro 3 Preflight > Open Dashboard`;
- `Tools > MSC Remake > Enviro 3 Preflight > Show Local Version Evidence`;
- `Tools > MSC Remake > Enviro 3 Preflight > List Project Assemblies Referencing Enviro`;
- `Tools > MSC Remake > Enviro 3 Preflight > Scan Duplicate Environment Owners`;
- `Tools > MSC Remake > Enviro 3 Preflight > Verify WeatherLab Excluded From Build`;
- `Tools > MSC Remake > Enviro 3 Preflight > Show Vendor File Changes`;
- `Tools > MSC Remake > Enviro 3 Preflight > Validate Full Preflight`;
- `Tools > MSC Remake > Enviro 3 Preflight > Export Diagnostics`.

State, quality, lightning и camera controls реализованы в одном Preflight window; capture anchors и их статусы показываются там же, а не размазаны по отдельным menu items.

## Smoke states

| Project binding | Time | Quality | Expected presentation | Статус |
|---|---:|---|---|---|
| `weather.clear` | fixed day | Low/High | neutral clear exterior | AUTOMATED_RUNTIME_PASS / MANUAL_PASS_CAPTURE_MISSING |
| `weather.overcast` | fixed day | Low/High | clouded exterior, no rain | AUTOMATED_RUNTIME_PASS / MANUAL_PASS_CAPTURE_MISSING |
| `weather.rain` | fixed day | Low/High | rain effects and wet-looking atmosphere only; no accumulated wetness claim | PARTICLE_RUNTIME_PASS / HIGH_MANUAL_PASS_CAPTURE_MISSING / LOW_COMPARISON_PENDING |
| `weather.storm_visual` | fixed day | Low/High | storm clouds/rain, explicit visual lightning request | PARTICLE_RUNTIME_PASS / HIGH_LIGHTNING_RAIN_MANUAL_PASS_CAPTURE_MISSING / LOW_COMPARISON_PENDING |
| `weather.night` | fixed night | Low/High | night sky and headlights | AUTOMATED_RUNTIME_PASS / MANUAL_PARTIAL_CAPTURE_MISSING |
| `weather.fog` | fixed day | Low/High | Enviro fog around water/building | AUTOMATED_RUNTIME_PASS / MANUAL_PASS_CAPTURE_MISSING |

## Preflight перед ручным тестом

1. Подтвердить успешную Unity compilation без C# errors.
2. Проверить effective registration `Enviro.EnviroHDRPRenderer` в HDRP Global Settings; serialized configuration уже присутствует.
3. Убедиться, что `ENVIRO_HDRP` есть для Standalone, а `ENVIRO_URP` отсутствует.
4. Создать scene builder output и выполнить validator.
5. Проверить, что VolumeProfile расположен под WeatherLab development root, а vendor profile/hash не изменён.
6. Проверить stable binding assets: explicit base Effects source, все шесть weather и два quality references non-null; Effects source должен содержать ровно один usable exact key `Rain`.
7. Проверить `Time.Settings.simulate=false`, отсутствие автономной WeatherLab zone scheduling и Enviro Audio disabled.
8. Войти в PlayMode и выполнить captures строго по `WEATHERLAB_CAPTURE_INDEX.csv`.
9. Отдельно выполнить development-build performance matrix.

Исторический 07A срез: пункты 1-7 подтверждены builder `1.0.1`, отдельным fresh-process preflight и targeted Weather/Enviro tests (`57/57`). Runtime smoke ждёт несколько кадров в `Rain` и `Storm`, проверяет normalized emission `0.5`/`1.0`, `isPlaying`, `particleCount > 0`, renderer и material. Пользователь принял общее отображение, fog composition, lightning и капли после remediation. Capture artifacts и отдельный Low/High comparison отсутствовали; пункт 8 был `MANUAL_PASS_BY_USER_CAPTURE_MISSING / LOW_HIGH_COMPARISON_PENDING`, пункт 9 — `PERF_PENDING`.

Актуальный 07B engineering/performance срез находится в `WEATHERLAB_07B_VALIDATION.md` и `WEATHERLAB_07B_PERFORMANCE.md`; исторические числа 07A выше не являются текущим gate.
