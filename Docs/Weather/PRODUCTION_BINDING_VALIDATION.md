# Production binding validation

Дата среза: 2026-07-18.

Статус: `STRUCTURAL_AND_SOLAR_NIGHT_FOLLOWUP_AUTOMATED_PASS /
THIRD_VISUAL_ROUTE_PARTIAL_PASS / CURRENT_WORLD_MATERIAL_CONTRACT_FAIL /
TARGETED_DAWN_NIGHT_USER_PASS`.

## Boundary

Authoritative path:

```text
GameTimeService + WeatherDirector + GlobalWetnessController + LightningStrikeDirector
  -> WeatherEnvironmentOutputs
  -> WeatherEnvironmentFrameMapper
  -> IEnvironmentPresentationAdapter
  -> Enviro3EnvironmentAdapter
  -> cloned Enviro runtime modules/assets
```

`MSC.Weather.Production.Runtime`, Bootstrap и legacy wetness assembly не имеют
прямой ссылки на `Enviro3.Runtime`. Vendor types существуют только в dedicated
integration assembly и Editor/tests, где это явно требуется.

Committed Bootstrap хранит canonical Enviro source prefab linked и inactive.
Project-owned activator включает backend только после ownership; installer ждёт
readiness. Это сохраняет vendor graph вне scene YAML и не меняет vendor source.

Production HDRP Volume на startup получает runtime-isolated
`VolumeProfile` и явный rebinding. Адаптер меняет только эту runtime-копию;
authoring profile и vendor assets не мутируются.

## Weather mapping

| Project state / stable binding | Enviro source | Runtime policy | Automated status | Production visual status |
|---|---|---|---|---|
| clear / `weather.clear` | `Clear Sky.asset` | direct typed binding | PASS | PENDING |
| partly cloudy / `weather.partly_cloudy` | `Cloudy 1.asset` | direct typed binding | PASS | PENDING |
| overcast / `weather.overcast` | `Cloudy 3.asset` | direct typed binding | PASS | PENDING |
| drizzle / `weather.drizzle` | `Rain.asset` | isolated Rain clone, project intensity | PASS | PENDING |
| steady rain / `weather.rain` | `Rain.asset` | isolated Rain clone, project intensity | PASS | USER PASS on current reported route |
| heavy rain / `weather.heavy_rain` | `Rain.asset` | separate isolated Rain clone, project intensity | PASS | PENDING |
| thunderstorm / `weather.storm_visual` | `Storm.asset` | isolated Storm clone; gameplay strike separate | PASS | PENDING |
| morning mist / `weather.fog` | `Foggy.asset` | direct typed binding | PASS | PENDING |
| night / `weather.night` | `Clear Sky.asset` | project time state over clear visual preset | PASS | brightness USER PASS 2026-07-18; no capture artifact |

Локальный base pack не предоставляет независимые typed drizzle/heavy profiles
или публичный continuous precipitation setter. Раздельные runtime clones и exact
`Rain` effect emission являются документированным bounded remap, не ported
donor behavior и не final art tuning.

Пре-remediation rain эффект создавал particles, но vendor runtime renderer
получал `maxParticleSize = 0.001`, и капли были нечитаемы в Game View.
Адаптер теперь применяет runtime-only floor `0.01` к rain renderers.
Исходный prefab/material остаётся неизменным; текущая rain presentation
подтверждена пользователем.

## Time, transition и refresh

- Enviro `simulate=false`; clock всегда project-owned.
- Runtime-only presentation calibration зафиксирована как `60 N`, `27.3 E`,
  `UTC+3`; для `1995-08-01` Enviro horizon crossing приходится примерно на
  `04:59` и `21:35`. Это сдвигает рассвет к согласованным `05:00`, не передавая
  Enviro authority над календарём или временем.
- После установки project date/time adapter обновляет sun/moon position в том
  же presentation pass, поэтому DEV `SetDateTime` не ждёт следующего кадра.
- Scheduled transition использует оставшееся project-owned время текущего front,
  переводит game seconds в simulation seconds и применяет duration к cloned
  module. Initial/new-game/restore sync всегда instant; explicit DEV duration
  сохраняется.
- Stale revision и invalid frame отклоняются fail-closed.
- Sky/ambient/reflection requests имеют sequence. Controller запрашивает refresh
  при смене binding, через `600` game seconds или при смещении listener на
  `25 m`; adapter coalesces ambient/reflection refresh с cooldown `1 s`.
- Installed API не имеет bounded explicit sky refresh. Это даёт diagnostic, а
  обычные Enviro sky updates продолжаются.

## Fog, exposure и water

- Active production profile:
  `Assets/Game/Weather/Production/Content/Profiles/ProductionEnvironmentHDRPVolume.asset`.
- Ровно один active global Volume и один active directional environment light
  подтверждены validator-ом.
- Project weather/exposure context — authority; dedicated Enviro adapter владеет
  единственным HDRP Fog/Exposure presentation pass.
- Прежний constant `EV 10`, а затем неверный знак больших interior offsets дали
  ручные `FAIL`: пересвеченные день/интерьер и crushed night. Fixed exposure
  вычисляется по детерминированной time-of-day curve с daylight bias `+0.25 EV`
  и offsets `Exterior 0 / Sheltered -0.15 / Interior -0.25 EV`. После сообщения,
  что исправленная ночь стала немного слишком яркой, night branch плавно
  ограничивается минимумом `7.5 EV` между `solarTime 0.43 -> 0.50`; более
  высокий HDRP Fixed Exposure EV затемняет изображение, а дневная ветвь curve
  не меняется.
- Прежний double/misbound fog дал opaque/red mist. Теперь project
  physical visibility конвертируется в HDRP mean free path и применяется
  один раз единственным owner-ом.
- Pre-07C `Directional Sun` и `Global Volume` сохранены, но inactive.
- Aurora принудительно выключается в runtime после применения любого quality
  profile; это перекрывает включённую aurora в vendor Low/Medium assets без их
  изменения. Temporary donor baseline reflection intensity ограничена `0.6`.
  Оставшийся metallic/reflection look принят пользователем как временный долг
  donor-материалов/шейдеров до их плановой замены, а не как дальнейшая задача 07C.
- Transparent water/window ordering, shoreline, interior fog leakage,
  headlights, underwater и matched post-remediation readability остаются
  manual `PENDING`. Отдельно скорректированные рассвет/ночная яркость получили
  targeted `USER PASS` 2026-07-18 без capture artifact.

## Wetness/material binding

Global contract всегда публикуется. Обычный HDRP/Lit не читает произвольные MSC
globals автоматически; поэтому full-world response требует project shader или
bounded MPB compatibility bridge.

Legacy bridge:

- регистрирует только explicit `DonorWorldLegacyMaterialBinding`;
- классифицирует HDRP/Lit opaque/alpha-clip отдельно;
- требует project-owned source-GUID allowlist;
- исключает water/transparent, unlit, emissive и unsupported;
- использует per-slot MPB и сохраняет shared materials;
- обновляет только при смене quantized wetness bucket.

Builder создаёт и назначает
`Assets/Game/Weather/Production/Content/Profiles/ProductionLegacyWetnessCoverage.asset`
с 20 reviewed source GUID entries: 4 ground, 5 road, 6 exterior roofs и 5
vegetation. Shared wall/interior materials остаются default-deny. Из 293 manifest
materials потенциальные семейства составляют 251 OpaqueLit и 14 AlphaClipLit,
но shader compatibility и число allowlist GUIDs не равны реальному числу
видимых slots. До route/coverage export процент production coverage не
заявляется.

После второго visual `FAIL` wet target smoothness bridge ограничена значениями
`0.45` для opaque и `0.25` для alpha-clip. Это ослабляет металлический вид
temporary donor surfaces вместе с reflection intensity `0.6`, но не считается
production material reauthoring. Оставшийся вид принят как temporary visual debt.

## Shelter binding

`ProductionShelterVolumeAuthoring` создаёт project-owned axis-aligned volumes со
stable IDs. Domain resolver выбирает strongest containing exposure, а
`Enviro3ShelterRemovalBridge` создаёт runtime-only removal zones. Donor hierarchy
names не используются.

Builder теперь выводит два Interior AABB для home house/garage из renderer
bounds frozen `World_Cell_0_-3_Legacy.unity` по восьми stable source IDs. Stable
IDs: `weather.shelter.home.house.interior.v1` и
`weather.shelter.home.garage.interior.v1`; policy
`frozen-renderer-aabb-inset-v1`, XZ inset `0.10 m`, floor `+0.05 m`, ceiling
`-0.10 m`. Evidence target:
`Assets/Game/Weather/Enviro3Integration/Editor/Evidence/ProductionShelterAuthoringEvidence.json`.

Builder `_13` и validator подтвердили ровно два saved authored volume, exact IDs,
bounds и frozen evidence. Runtime domain и Enviro bridge явно сканируют
persistent root после `DontDestroyOnLoad`.

Ручная проверка подтвердила, что один max-X/Z ellipsoid создавал
большой oval overhang за пределами narrow AABB. Bridge теперь строит
детерминированный набор вписанных ellipsoids вдоль длинной оси, а vertical
stretch ограничен снизу значением `1`. Это сохраняет покрытие evidence-derived
living-room eye/ceiling probes. Пользователь подтвердил, что ранее сообщённая
rain-проблема теперь устранена; perimeter/fog и Teimo/другие интерьеры остаются
`PENDING`.

## Wind binding

Frame требует finite, normalized XZ direction и ordered base/gust speeds.
Adapter переводит speed в нормализованный диапазон от 0 до 20 m/s, пишет cloned
Environment settings и единственный Enviro-owned WindZone. Attach отклоняет
duplicate active WindZone. Per-object Update writers не создаются.

## Lightning and audio

- Ambient и gameplay request имеют разные project-owned paths.
- Automatic storm ambient request создаётся только через
  `LightningStrikeDirector.CreateAmbientLightning`: после `45` game seconds
  spawn/load grace, с cooldown `90–240` game seconds и target `180–450 m` от
  listener. Он не создаёт gameplay candidate и не наносит damage.
- Enviro не выбирает gameplay target и не владеет damage/fairness.
- Vendor automatic lightning принудительно выключен.
- Runtime lightning prefab/material клонируются; vendor source не мутируется.
- Enviro weather audio и vendor thunder подавлены.
- `WeatherAudioOutput` публикует precipitation/wind/thunder risk будущему
  project audio consumer.

## Current follow-up automated evidence

| Check | Result | Покрытие |
|---|---:|---|
| `Logs/M07C_VisualRemediation2_Build.log` | PASS | compile, Builder `1.0.2`, validator, one owner and active donor profile |
| `Logs/M07C_VisualRemediation3_EnviroIntegration.xml` | 17/17 PASS | Enviro integration, solar calibration and night-exposure policy |
| `Logs/M07C_VisualRemediation3_WeatherProduction.xml` | 32/32 PASS | combined WeatherProduction/ProductionIntegration/legacy-wetness contracts |
| `Logs/M07C_VisualRemediation2_WeatherPresentation.xml` | 53/53 PASS | vendor-neutral exposure/fog/frame contracts |
| `Logs/M07C_VisualRemediation3_ProductionPlayMode.xml` | 6/6 PASS | production lifecycle and teardown |
| `Logs/M07C_VisualRemediation2_WorldOnlyCompatibility.xml` | 1/1 PASS | explicit world-only compatibility |
| `Logs/M07C_VisualRemediation3_WorldBaseline.xml` | 8/10 | focused rerun reproduces the same two generated-material contract failures; not order pollution |
| `Logs/M07C_VisualRemediation3_FullEditMode.xml` | 328/334 | four historical failures plus the same two current WorldBaseline material-contract failures |
| Prior `Logs/M07C_VisualRemediation2_WorldFreeze.log` | PASS (prior evidence) | previous frozen result hash was unchanged; no fresh frozen PASS is claimed |

Последний successful strict frozen result SHA-256:
`10544fc3ed5bd6c8cd44b451155e766c0552710f4c9d8cc91dda263de001ba5f`.
Текущие две WorldBaseline ошибки относятся к ignored generated materials с
GUID `06cd824234ef21f40b2c797b15acbd26`,
`2b8378937c6afb64390d5474f4bcd14a`,
`5cc44389f1f10bf4cabee6d33feb1551` и
`69ad9b54c687ad847a9df25112c2d430`; их timestamp `09:19:37` предшествует
этому follow-up. Они воспроизводятся focused suite `8/10`, поэтому это текущий
generated-payload contract drift, а не order pollution. Frozen payload не
модифицировался. Прежние `16/16`, `328/332` и другие pre-remediation artifacts
остаются только historical evidence. Production screenshots, manual route и
development-build profiling автоматикой не заменяются.

## Final disposition

- Binding architecture: `PASS`.
- Single production owner: `PASS`.
- Vendor source changes: `0`.
- Frozen donor world changes: `0`.
- Post-remediation Production PlayMode: `PASS 6/6`.
- First and second production visual routes: `FAIL` (historical evidence kept).
- Third visual route: `PARTIAL PASS`; rain and sunset accepted, reflections
  accepted as temporary donor material/shader debt.
- Corrected dawn around `05:00` and night brightness: `USER PASS 2026-07-18`;
  no capture artifact, matched fidelity/night-headlight evidence remains `PENDING`.
- Full material/shelter coverage: `PENDING`.
- World-fidelity side-by-side: `PENDING`.
