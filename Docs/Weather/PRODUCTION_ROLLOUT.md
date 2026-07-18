# Production weather rollout — Milestone 07C

Дата среза: 2026-07-18

Unity: `6000.3.11f1`, HDRP: `17.3.0`

Статус: `PRODUCTION_INTEGRATION_AND_SOLAR_NIGHT_FOLLOWUP_AUTOMATED_PASS /
THIRD_VISUAL_ROUTE_PARTIAL_PASS / CURRENT_WORLD_MATERIAL_CONTRACT_FAIL /
TARGETED_DAWN_NIGHT_USER_PASS`.

Этот документ описывает фактически построенный rollout в активный private
feature-parity мир. Он не является утверждением о завершённом remaster art,
публичной сборке или визуальном принятии всех режимов погоды.

## Зафиксированная база

- Bootstrap: `Assets/Game/Bootstrap/Bootstrap.unity`.
- Active world profile: `donor-feature-parity-06b2`.
- Frozen revision: `DonorWorldBaseline-v001`.
- Baseline classification: `TemporaryDirectImport`.
- Distribution policy: `PrivateLocalFeatureParityOnly`.
- World composition: 1 global + 49 additive cell scenes.
- Production environment builder:
  `Assets/Game/Weather/Enviro3Integration/Editor/ProductionEnvironmentBuilder.cs`.
- Validator:
  `Assets/Game/Weather/Enviro3Integration/Editor/ProductionEnvironmentValidator.cs`.

Пользовательские изменения 08A/UI существуют в общем worktree, но изолированы
от 07C: они не входят в этот rollout, не редактировались и не учитываются как
weather evidence.

## Production owner migration

Initial rollout Builder `1.0.0` выполнил детерминированную и повторяемую
миграцию Bootstrap:

1. Деактивировал прежние foundation placeholders `Directional Sun` и
   `Global Volume`, не удаляя их assets.
2. Создал один дочерний `ProductionEnvironmentBackend` под process-lifetime
   `GameCompositionRoot` и оставил его authored inactive.
3. Сохранил Enviro как linked source-prefab instance, один adapter и один global
   HDRP Volume с project-owned profile. Project-owned
   `ProductionEnvironmentBackendActivator` включает backend только после победы
   composition root в ownership gate; так vendor `OnEnable` не распаковывает
   Enviro graph в committed scene YAML.
4. Подключил `ProductionEnvironmentController`, bounded legacy-wetness bridge и
   shelter-removal presentation bridge.
5. Назначил прямые Low/Medium/High quality assets; default — `Medium`.
6. Передал environment в существующий `ProductionWorldStreamingInstaller`.

Installer имеет явный serialized startup contract: production Bootstrap —
`ProductionEnvironmentRequired`, а legacy debug fixture —
`WorldOnlyDevelopment`. Runtime не выбирает режим по `null`-ссылке. Повторная
cellization сохраняет уже настроенный production owner; 05B.1 fixture builder
использует отдельную explicit world-only conversion, которая удаляет
environment controller, activator, wetness bridge и owned backend из копии.
Оба старых validator-а теперь отклоняют incoherent startup composition.

Исторический initial-rollout batch artifact:
`Logs/M07C_ProductionBuild_17.log` — `PASS`:

```text
M07C_PRODUCTION_ENVIRONMENT_VALIDATION_PASS owners=1 bindings=valid activeWorld=donor-feature-parity-06b2 privateBuildGuard=validated
M07C_PRODUCTION_ENVIRONMENT_BUILD_OK version=1.0.0 quality=Medium owner=Bootstrap vendorFiles=538 vendorFingerprint=8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44
```

## Current visual follow-up evidence

После первого visual `FAIL` была выполнена remediation exposure/fog/shelter/rain.
Повторный маршрут выявил metallic-looking temporary surfaces, rain leakage в
гостиной, interior overexposure, black night с aurora и слишком раннюю темноту
в `18:00–20:00`. Текущая bounded second remediation использует Builder `1.0.2`;
compile, build и validator прошли в
`Logs/M07C_VisualRemediation2_Build.log`.

Evidence этой второй remediation: Enviro integration `17/17`, combined WeatherProduction/
ProductionIntegration/legacy-wetness `32/32`, WeatherPresentation `53/53`,
Production PlayMode `6/6`, world-only `1/1`; full EditMode `330/334` с теми же
четырьмя unrelated historical failures. Artifacts:
`Logs/M07C_VisualRemediation2_*.xml`.
Freeze revalidation — `PASS` с неизменным result SHA-256
`10544fc3ed5bd6c8cd44b451155e766c0552710f4c9d8cc91dda263de001ba5f` в
`Logs/M07C_VisualRemediation2_WorldFreeze.log`.

Следующий ручной маршрут подтвердил rain presentation и текущий
вечерний закат. Остаточные отражения пользователь разрешил оставить как
temporary donor material/shader debt до плановой замены. При этом ночь стала
немного слишком яркой, а рассвет начинался после `03:00`, поэтому выполнен
runtime-only solar/night follow-up: `60 N / 27.3 E / UTC+3`, ожидаемый Enviro
horizon crossing на `1995-08-01` около `04:59 / 21:35`, а night exposure плавно
ограничивается минимумом `7.5 EV` между `solarTime 0.43 -> 0.50` без изменения
daylight branch.

Fresh targeted suites: Enviro integration `17/17`, combined production `32/32`
и Production PlayMode `6/6` — `PASS` в
`Logs/M07C_VisualRemediation3_*.xml`. Full EditMode — `328/334`: четыре прежние
historical failures и две текущие WorldBaseline material-contract failures.
Focused `Logs/M07C_VisualRemediation3_WorldBaseline.xml` воспроизводит последние
две как `8/10`, поэтому это persistent generated-payload drift, не test-order
pollution. Материалы с GUID `06cd...`, `2b837...`, `5cc443...`, `69ad...` имеют
timestamp `09:19:37`, предшествующий follow-up; frozen payload не менялся.
Предыдущий freeze PASS сохраняется только как prior evidence. Пользователь
принял скорректированные рассвет около `05:00` и ночную яркость 2026-07-18;
capture artifact не создавался, поэтому matched fidelity gate остаётся `PENDING`.

Точная матрица authority/owner/disposition находится в
`Docs/Weather/PRODUCTION_OWNER_MATRIX.csv`.

## Runtime lifetime и порядок старта

`ProductionEnvironmentController` живёт на том же persistent Bootstrap object,
что composition root и world streaming. Additive world cells не владеют
временем, погодой, Enviro manager, sky, fog или wind.

Startup barrier:

1. Composition root отклоняет duplicate persistent root до запуска дочерних
   managers.
2. Project-owned activator включает linked inactive backend, а installer ждёт
   его readiness до продолжения старта.
3. Environment создаёт project-owned GameTime, weather, wetness и lightning
   domains.
4. Installer создаёт player в неактивном состоянии и явно передаёт его Camera
   adapter-у.
5. Если есть staged session DTO, он полностью валидируется и применяется до
   reveal.
6. Wetness outputs синхронизируются, adapter подключается и получает один
   coherent presentation frame.
7. World streaming загружает global/focus cells.
8. Только после этого player активируется и начинается simulation.

При additive load/unload authoritative objects не пересоздаются. Scene events
используются только для перестройки project-owned shelter registry и bounded
legacy-material cache. Vendor-neutral topology guard вызывается и при additive
scene changes; проверка coalesced на два кадра, чтобы lifecycle старого и нового
owner успел завершиться. Enviro adapter дополнительно выполняет typed scan
manager-ов и `WindZone` и fail-closed отключает все найденные concrete
presentation owners при нарушении single-owner topology. При same-scene reload
новый root ждёт уничтожения старого persistent Enviro static owner. Duplicate
environment owner отключается fail-closed.

Lifecycle artifact `Logs/M07C_VisualRemediation3_ProductionPlayMode.xml` прошёл
`6/6`: additive duplicate, restore-before-reveal/additive survival, выход в
rootless single scene, same-scene runtime duplicate, single Bootstrap reload и
different-Single handoff в explicit `WorldOnlyDevelopment` root. Старый
WorldRemaster compatibility probe дополнительно прошёл `1/1`. Manual long-soak
остаётся `PENDING`.

## Authoritative state и Enviro binding

Project-owned системы остаются authority для:

- календаря и времени;
- deterministic weather schedule/front/transition;
- accumulated wetness и drying;
- ambient/gameplay lightning requests и fairness;
- session DTO;
- audio/UI/road/vegetation/water future outputs.

Enviro получает immutable presentation frames через
`IEnvironmentPresentationAdapter`. Его autonomous time/weather schedule и audio
принудительно отключены. Core, Bootstrap и production domain assemblies не
ссылаются напрямую на `Enviro3.Runtime`; vendor dependency ограничена dedicated
integration assembly.

Поддержаны stable bindings для clear, partly cloudy, overcast, drizzle, steady
rain, heavy rain, storm, morning fog/mist и night. Drizzle/steady/heavy rain
используют изолированные runtime-клоны доступного base-pack Rain profile с
project-owned intensity; source assets не изменяются.

Scheduled transition получает оставшееся project-owned время текущего front в
game seconds и переводит его в simulation seconds. Initial state, new game и
restore синхронизируются принудительно без перехода; explicit transition из DEV
API сохраняет переданную duration. Это presentation mapping, а не перенос
authority в Enviro.

Runtime-only Enviro calibration зафиксирована как `60 N`, `27.3 E`, `UTC+3`.
Для `1995-08-01` Enviro horizon crossing приходится примерно на `04:59` и
`21:35`. После project `SetDateTime` adapter обновляет sun/moon position в том
же presentation pass. Aurora принудительно выключается после применения каждого
quality profile, включая vendor Low/Medium presets; vendor assets остаются
read-only.

## Wetness, shelter, wind и lightning

### Wetness

- Process-wide globals `_MSC_GroundWetness`, `_MSC_RoadWetness`,
  `_MSC_PuddleAmount`, `_MSC_VegetationWetness` обновляются dirty-only.
- Temporary donor baseline имеет bounded compatibility bridge с per-slot
  `MaterialPropertyBlock`, quantization 64 и без `renderer.material`.
- Bridge допускает только явно allowlisted source material GUIDs и исключает
  transparent/water, unlit, emissive, unsupported и category-mismatch slots.
- Builder создал и назначил
  `ProductionLegacyWetnessCoverage.asset`: 20 reviewed source GUID entries
  (4 ground, 5 road, 6 exterior roofs, 5 vegetation). Shared wall/interior
  materials остаются default-deny.
- Hardening suite `5/5 PASS` проверяет category-specific writes, сохранение
  foreign MPB data, exact reset, quantized skips и отчёт reviewed/excluded
  coverage. Реальный процент видимых donor-world slots и визуальный результат
  route capture всё ещё `PENDING`, а не full coverage PASS.
- После второго visual `FAIL` wet target smoothness bridge ограничена `0.45`
  для opaque и `0.25` для alpha-clip; это temporary-baseline mitigation, не
  production material reauthoring.
- Остаточный metallic/reflection look принят пользователем как documented
  temporary donor material/shader debt и больше не расширяет bounded 07C.
- Wet-road physics не включена: `WetFrictionEnabled=false`, multiplier `1.0`.

### Shelter

Project-owned axis-aligned volumes со stable IDs формируют
`Exterior/Sheltered/Interior` exposure и runtime-only Enviro removal zones.
Builder детерминированно готовит два bounded Interior AABB под
`ProductionEnvironmentBackend`:

- `weather.shelter.home.house.interior.v1`;
- `weather.shelter.home.garage.interior.v1`.

Они выводятся из renderer bounds frozen cell
`World_Cell_0_-3_Legacy.unity` по восьми stable source IDs, policy
`frozen-renderer-aabb-inset-v1`: XZ inset `0.10 m`, floor `+0.05 m`, ceiling
`-0.10 m`. Builder/validator `_13` подтвердил оба компонента в saved Bootstrap,
а lifecycle path сканирует explicit persistent root после
`DontDestroyOnLoad`. Authoritative AABB и runtime removal-zone registry тем
самым имеют automated PASS. Teimo и другие интерьеры не покрыты.

Enviro removal zone не является точным box: bridge использует deterministic
horizontal tiling вписанными ellipsoids. Их vertical stretch теперь имеет floor
`1`, чтобы не оставлять без покрытия evidence-derived living-room eye/ceiling
probes. Текущая ранее сообщённая rain-проблема подтверждена как исправленная; visual
perimeter/fog gate и остальные интерьеры остаются `PENDING`.

### Wind

Project frame передаёт нормализованное XZ-направление, скорость и gust. Adapter
записывает их в cloned Enviro Environment settings и единственный Enviro-owned
`WindZone`. Дополнительные per-tree writers не создаются. Визуальная амплитуда
растительности на Low/Medium/High требует route capture.

### Lightning

Ambient visual request и project-owned gameplay strike остаются раздельными.
Enviro отображает только sequence-keyed visual request. Automatic storm ambient
bolts создаются только через `LightningStrikeDirector.CreateAmbientLightning`:
controller применяет deterministic spawn/load grace `45` game seconds, bounded
cooldown `90–240` game seconds и listener-relative target `180–450 m`. Этот путь
не создаёт gameplay candidate и не наносит damage. Gameplay director сохраняет
собственные cooldown/protection/thunder delay/restore grace и работает в
non-lethal режиме, пока нет согласованного damage contract. Vendor automatic
lightning остаётся принудительно выключенным.

## Fog, exposure, water и reflection cadence

Enviro является единственным fog presentation authority; HDRP Fog в
`ProductionEnvironmentHDRPVolume.asset` — управляемый output substrate, не
второй погодный контроллер. Старый Bootstrap global volume выключен.

Project-owned refresh запрашивается при смене binding, после `600` game seconds
или перемещения listener на `25 m`. Adapter coalesces ambient/reflection refresh
с cooldown `1 s`; full refresh каждый кадр отсутствует. Для temporary donor
baseline reflection intensity ограничена `0.6`. Fixed exposure policy использует
daylight bias `+0.25 EV` и offsets `Exterior 0 / Sheltered -0.15 /
Interior -0.25 EV`. Night branch дополнительно использует smooth minimum
`7.5 EV` на `solarTime 0.43 -> 0.50`, что затемняет ночь и не меняет daylight
branch. Production water остаётся temporary transparent proxy:
shoreline, water fog, windows, underwater и rain impacts не получили отдельного
07C visual sign-off.

## Cross-system outputs

`WeatherEnvironmentOutputs` предоставляет vendor-neutral clock, weather,
wetness, exposure, audio intensity/risk, UI summary и presentation health.
`ProductionRoadWetnessOutput` публикует road wetness, но сохраняет dry 06A
friction. Wwise, final UI, windshield/wipers, final water, vehicle cooling и NPC
consumers в 07C не реализуются.

## Rebuild и rollback

Меню:

- `Tools > MSC Remake > Production Weather > Build or Rebuild Bootstrap Environment`;
- `Tools > MSC Remake > Production Weather > Validate Bootstrap Environment`.

Builder удаляет только собственный `ProductionEnvironmentBackend`, пересоздаёт
его и повторно деактивирует placeholders. Vendor assets и generated donor scenes
не редактируются. Для возврата к pre-rollout presentation нужно откатить
07C-owned Bootstrap/code/assets обычным Git change, а не удалять donor payload
или править Enviro.

## Production DEV tooling

Editor-only окно:

`Tools > MSC Remake > Production Weather > Runtime Validation`

В production Bootstrap Play Mode оно показывает authoritative и presented
state и предоставляет bounded project-owned controls:

- set date/time и controlled time advance;
- transient logical weather override с transition и его снятие;
- freeze/unfreeze schedule и deterministic seed reset;
- ground/road/puddle/vegetation wetness override;
- Low/Medium/High switch;
- ambient visual lightning у явно bound listener;
- validate/present now;
- owner/lifecycle/shelter diagnostics и Scene View shelter overlay;
- JSON export в `Logs/M07C_ProductionWeatherDiagnostics.json`;
- Game View capture в
  `References/Weather/Milestone07C/ProductionCaptures/`.

Runtime DEV API компилируется только в Editor и не раскрывает Enviro types.
Targeted combined suite
`Logs/M07C_VisualRemediation3_WeatherProduction.xml` прошёл `32/32`, включая
DEV API tests, integration, legacy-wetness и authoring repeatability. Отдельный read-only shelter measurement tool доступен
через `Measure Frozen Home Shelter Candidates` и пишет
`Logs/M07C_ProductionShelterMeasurements.json`; он не редактирует donor scene.

Gameplay-strike authoring, canonical anchor/route automation, performance
capture, geometry diff и public shipping audit не подменяются этим окном и
остаются отдельными gates.

## Незакрытые gates

- matched pre/post clear-day captures: `PENDING`;
- strict side-by-side world-fidelity package: `PENDING`;
- full manual weather route и cell-boundary transitions: `PENDING`;
- Low/Medium/High production comparison: `PENDING`;
- Windows x64 1920×1080 development-build performance: `PENDING`;
- production wetness route/coverage evidence и visual shelter perimeter: `PENDING`;
- dawn около `05:00` и скорректированная ночная яркость: `USER PASS 2026-07-18`,
  capture artifact отсутствует и matched fidelity evidence остаётся `PENDING`;
- public/distributable shipping profile: `NO-GO` из-за donor baseline.

Пока эти пункты не закрыты, 07C имеет automated engineering PASS, но не полный
Definition of Done.
