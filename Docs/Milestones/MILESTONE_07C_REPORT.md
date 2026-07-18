# Milestone 07C — production weather rollout and validation

Дата: 2026-07-18

Unity: `6000.3.11f1`, HDRP: `17.3.0`

Статус: **`DAWN_NIGHT_FOLLOWUP_TARGETED_AUTOMATED_AND_USER_PASS /
CURRENT_WORLD_MATERIAL_CONTRACT_FAIL /
MANUAL_FIDELITY_PERFORMANCE_AND_PUBLIC_SHIPPING_PENDING`**.

Milestone 07C внедрил 07A/07B environment stack в production Bootstrap, но его
полный Definition of Done пока не закрыт. После двух неудачных visual route
пользователь принял дождь, текущий sunset и временный reflection debt, но
попросил сдвинуть dawn к 05:00 и немного затемнить ночь. Follow-up проходит
targeted automated gate и принят пользователем; current generated-material
contract, matched world captures, Development Player performance и
public shipping audit остаются открыты.

## 1. Production owner migration

`ProductionEnvironmentBuilder 1.0.2` построил один environment backend под
persistent `GameCompositionRoot`. Старые `Directional Sun` и Bootstrap `Global
Volume` сохранены обратимо, но inactive. Canonical Enviro source prefab остаётся
linked и authored inactive; project-owned activator включает его только после
ownership, а installer ждёт backend readiness. В runtime активны ровно один
Enviro manager, adapter, global HDRP Volume и directional environment-light
strategy.

Final builder evidence: `Logs/M07C_VisualRemediation2_Build.log` (`PASS`).

`ProductionEnvironmentValidator` прошёл:

```text
M07C_PRODUCTION_ENVIRONMENT_VALIDATION_PASS owners=1 bindings=valid activeWorld=donor-feature-parity-06b2 privateBuildGuard=validated
```

Artifact: `Logs/M07C_VisualRemediation2_Build.log`.

## 2. Bootstrap/streaming lifecycle

Project-owned domains живут в Bootstrap, а не в additive cell. Player Camera
передаётся adapter-у явно. Restore и первый coherent presentation frame
выполняются до streaming reveal/player activation. Static duplicate guards
существуют для composition root и environment owner; cell load/unload только
перестраивает shelter/material registrations. Same-scene reload ждёт уничтожения
старого persistent Enviro static owner; topology revalidation coalesced на два
кадра.

Startup mode сериализован явно: production Bootstrap требует
`ProductionEnvironmentRequired`, legacy streaming fixture использует
`WorldOnlyDevelopment`; implicit `null` fallback отсутствует. Repeat
cellization сохраняет существующий production mode, а fixture rebuild выполняет
отдельную world-only conversion с удалением weather ownership. Старые world
validators проверяют coherent startup composition.

Final second-remediation
`Logs/M07C_VisualRemediation2_ProductionPlayMode.xml` прошёл `6/6`: additive
duplicate, restore-before-reveal/additive survival, выход в rootless single
scene, same-scene runtime duplicate, single Bootstrap reload и переход в другую
Single-сцену с собственным world-only composition root. Отдельный прежний
WorldRemaster probe прошёл `1/1` в
`Logs/M07C_VisualRemediation2_WorldOnlyCompatibility.xml`. Manual long-soak всё ещё
остаётся `PENDING`.

## 3. Enviro integration and local version

Exact patch неизвестен. Authoritative local payload:

```text
Enviro 3.x
538 files
305967931 bytes
SHA-256 8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44
```

Прямые Enviro references из core/production assemblies отсутствуют; dependency
сосредоточена в dedicated integration assembly. Vendor files changed в 07C: `0`.

## 4. Time/weather/wetness/lightning

- GameTime и WeatherDirector authoritative.
- Enviro autonomous time/weather и audio выключены.
- Vendor source location `0/0/UTC0` больше не используется в production
  runtime clone. Integration применяет solar-calibration
  `60 N / 27.3 E / UTC+3` как `RemakeDesignTarget`, а не literal GPS location.
  Установленный Enviro algorithm даёт horizon crossings около `04:59` и
  `21:35`; PlayMode фиксирует sun below/above в `04:30/05:30` и
  above/below в `20:30/22:00`.
- Date/time применяется до exposure и немедленно обновляет положение sun/moon,
  поэтому DEV jump не оставляет один stale solar-time frame.
- Aurora принудительно выключается после Low/Medium/High quality ownership pass;
  vendor quality assets не изменялись.
- Clear/partly/overcast/drizzle/rain/heavy/storm/mist/night bindings доступны.
- Wetness/drying остаётся project-owned и публикует globals.
- Scheduled front duration переводится из game seconds в simulation seconds;
  initial/new/restore sync instant, explicit DEV duration сохраняется.
- Ambient и gameplay lightning разделены; automatic ambient storm path имеет
  project-owned `45 s` grace, `90–240 s` cooldown и `180–450 m` listener-relative
  target, не создавая gameplay candidate; gameplay restore grace проверен.
- Gameplay lightning non-lethal до будущего damage contract.

## 5. Fog/water/shelter

Project visibility/exposure context — authoritative input; dedicated Enviro adapter
владеет единственным HDRP Fog/Exposure presentation pass. Production Volume
получает runtime-isolated `VolumeProfile` и explicit rebinding. Fixed exposure
вычисляется по deterministic time-of-day curve с `+0.25 EV` global bias и
exterior/sheltered/interior offsets `0/-0.15/-0.25 EV`. Follow-up добавляет
плавный project-owned minimum night exposure `7.5 EV` между full-night
`solarTime <= 0.43` и daylight `0.5`; дневная экспозиция и разница контекстов не
меняются. Physical visibility конвертируется в HDRP mean free path.
Old Bootstrap fog owner inactive. Water остаётся temporary transparent proxy.

Shelter resolver и runtime Enviro removal-zone bridge реализованы. Builder и
validator подтвердили два frozen-derived Interior AABB для home house и garage
с project stable IDs; runtime явно сканирует persistent DDOL root.

Первый ручной route подтвердил oval overhang от одного max-X/Z ellipsoid.
Bridge детерминированно разбивает AABB на вписанные removal ellipsoids. Второй
route обнаружил вертикальное сжатие этих зон у потолка гостиной: теперь stretch
никогда не меньше `1`, а evidence-derived probes у дальнего кресла и над
телевизором покрыты автоматическим containment-тестом. Perimeter/interior visual
acceptance после второго фикса остаётся `PENDING`; Teimo/остальные интерьеры не
покрыты.

Rain particles до remediation спавнились, но были нечитаемы в Game View
из-за vendor runtime renderer `maxParticleSize = 0.001`. Adapter теперь
применяет только к runtime-копиям floor `0.01`; vendor prefab/source не
менялся. Game View retest остаётся `PENDING`.

## 6. Materials and wind

Global wetness contract и bounded per-slot MPB bridge реализованы без unique
materials. Production asset содержит 20 reviewed GUID entries. Второй route
показал зеркальный gloss на low-detail donor terrain/road, поэтому временные
smoothness targets снижены с `0.72/0.50` до `0.45/0.25`, а runtime Enviro
reflection intensity ограничена `0.6`; shared materials и metallic не мутируют.
Фактическая donor-world visual coverage и route capture всё ещё не заявляются
PASS.

Project wind direction/speed/gust передаются в cloned Enviro settings и один
Enviro-owned WindZone. Current Enviro integration suite: `17/17 PASS`
(`Logs/M07C_VisualRemediation2_EnviroIntegration.xml`); production vegetation
captures ещё нужны.

## 7. Save/restore ordering

Schema-1 production envelope сохраняет GameTime, weather/front/RNG/overrides,
wetness, lightning и project quality. Candidate полностью валидируется до
mutation; composite weather восстанавливается атомарно, затем GameTime, с
rollback при ошибке. Wetness sync и один presentation frame выполняются до
world reveal. File persistence остаётся Milestone 09.

## 8. Cross-system outputs

Vendor-neutral outputs готовы для audio precipitation/wind/thunder risk, UI
summary, exposure, wetness и presentation status. Road wetness hook существует,
но wet friction выключена. Wwise, final UI, wipers, water, cooling и NPC
consumers не входят в 07C.

## 9. Quality tiers

Low=`0`/`quality.low`, High=`1`/`quality.high`, Medium=`2`/`quality.medium`.
High сохраняет pre-07C serialized value; default production tier — Medium.
Три direct vendor assets подтверждены. Project QualitySettings не связаны с
weather tier автоматически. Production cost tiers не измерен.

## 10. Tests and manual validation

| Automated evidence | Result |
|---|---:|
| `Logs/M07C_VisualRemediation2_Build.log` | compile + Builder `1.0.2` + production validator PASS |
| `Logs/M07C_VisualRemediation2_WeatherProduction.xml` | combined WeatherProduction / ProductionIntegration / legacy wetness 32/32 PASS |
| `Logs/M07C_VisualRemediation2_EnviroIntegration.xml` | EnviroIntegration 17/17 PASS |
| `Logs/M07C_VisualRemediation2_WeatherPresentation.xml` | WeatherPresentation 53/53 PASS |
| `Logs/M07C_VisualRemediation2_ProductionPlayMode.xml` | Production PlayMode 6/6 PASS |
| `Logs/M07C_VisualRemediation2_WorldOnlyCompatibility.xml` | world-only 1/1 PASS |
| `Logs/M07C_VisualRemediation2_FullEditMode.xml` | 330/334; same four unrelated historical failures |
| `Logs/M07C_VisualRemediation2_WorldFreeze.log` | PASS; frozen result hash unchanged |
| `Logs/M07C_VisualRemediation3_EnviroIntegration.xml` | dawn/night policy and integration 17/17 PASS |
| `Logs/M07C_VisualRemediation3_WeatherProduction.xml` | combined production EditMode 32/32 PASS |
| `Logs/M07C_VisualRemediation3_ProductionPlayMode.xml` | dawn/dusk lifecycle and production PlayMode 6/6 PASS |
| `Logs/M07C_VisualRemediation3_FullEditMode.xml` | 328/334; four historical plus two current WorldBaseline material-contract failures |
| `Logs/M07C_VisualRemediation3_WorldBaseline.xml` | focused WorldBaseline 8/10; same two current material-contract failures |
| Manual rain and current sunset | USER PASS |
| Temporary reflection/material appearance | USER ACCEPTED DEBT |
| Manual dawn/night retest after follow-up | USER PASS 2026-07-18; capture artifact missing |

Fresh full EditMode не является `334/334`: четыре failures — те же historical
Garage M3 lighting expectations, obsolete PilotGate expectation и external
donor-source hash drift. Дополнительно два WorldBaseline tests фиксируют
current contract drift четырёх ignored generated compatibility materials
(`06cd8242...`, `2b837893...`, `5cc44389...`, `69ad9b54...`). Их on-disk
timestamp `2026-07-18 09:19` предшествует dawn/night follow-up; frozen payload
не редактировался. Focused WorldBaseline `8/10` воспроизводит те же две ошибки,
то есть это persistent current payload drift, не test-order pollution. Все
targeted dawn/night 07C cases проходят.

Первый manual production weather route: `FAIL`. По screenshots зафиксированы:

- day/interior overexposure и crushed night из-за constant `EV 10`;
- opaque/red mist из-за double/misbound fog;
- shelter oval overhang;
- rain spawned in Scene View, but unreadable in Game View из-за
  `maxParticleSize = 0.001`.

Второй manual production weather route также завершился `FAIL`. Зафиксированы:

- зеркальный/«металлический» вид temporary terrain и части интерьера;
- rain ingress в гостиной у телевизора;
- всё ещё слишком яркий daytime interior;
- crushed-black midnight и unintended aurora;
- слишком ранний sunset: к 18–19 часам почти темно, в 20 — ночь.

После второй реализованной remediation manual production weather matrix:
`RETEST PENDING`; автоматический PASS не подменяет визуальную приёмку.

Третий user review частично закрыл matrix:

- rain и текущий sunset: `PASS`;
- metallic/reflection look: принят как temporary donor material/shader debt,
  без дальнейшего исправления в 07C;
- night: слишком яркая, требуется небольшое затемнение;
- dawn: визуально начинается после 03:00, target сдвинут к 05:00.

Dawn/night follow-up реализован, targeted-validated и принят пользователем
2026-07-18. Отдельный capture artifact не сохранён.

Production Runtime Validation window и vendor-neutral Editor-only DEV API
реализованы для time/weather/seed/wetness/quality/ambient lightning,
owner/lifecycle/shelter diagnostics, JSON export и captures. Current
Combined production suite прошёл `32/32`, включая repeatable authoring
между production и explicit world-only режимами:
`Logs/M07C_VisualRemediation2_WeatherProduction.xml`.

## 11. Performance

07B Editor 640×480 regression artifact существует, но 07C Windows x64 1080p
Development Player route отсутствует. CPU/GPU/render/memory/quality-tier sign-off
не заявляется.

## 12. World-fidelity regression

Frozen `DonorWorldBaseline-v001`, transforms и fingerprints записаны. Последний
strict audit прошёл с `automatedPass=True`, `structuralPass=True`, 50 scenes, 49 cells,
3842 entities и неизменным result SHA-256
`10544fc3ed5bd6c8cd44b451155e766c0552710f4c9d8cc91dda263de001ba5f`.
Последний strict-PASS artifact: `Logs/M07C_VisualRemediation2_WorldFreeze.log`.
Required frozen paths не имеют Git content diff; visual remediation не меняла
frozen world и vendor payload. Однако current local ignored generated-material
payload больше не совпадает с recorded compatibility-material contract, поэтому
fresh freeze PASS не заявляется. Matched before/after side-by-side captures не
созданы; visual fidelity result `PENDING`.

## 13. Shipping/content audit

WeatherLab и Enviro sample scenes исключены, private donor build guard работает.
Текущие Build Settings содержат Bootstrap, 50 RuntimeBaseline scenes и 10
prototype/development/old production scenes (61 scene total). Private
feature-parity use защищён; public/distributable profile — `NO-GO`.

## 14. Изменённые области реализации

- `Assets/Game/Bootstrap/` — owner lifetime, service binding, reveal ordering;
- `Assets/Game/World/Debug/Streaming/PrototypeWorldStreamingFixture.unity` —
  explicit environment-free development startup mode;
- `Assets/Game/Editor/WorldStreaming/` и world-baseline validator — repeatable
  production/world-only authoring guard;
- `Assets/Game/Weather/Production/` — controller, DTO, quality, shelter,
  bounded wetness and future road output;
- `Assets/Game/Weather/Enviro3Integration/` — production builder/validator,
  Medium, wind, refresh, isolated production Volume rebinding, exposure/fog,
  rain renderer adaptation and tiled shelter bridge;
- `Assets/Game/Weather/Presentation/` — compatible Medium/wind/exposure/fog contracts;
- targeted EditMode/PlayMode tests;
- production Runtime Validation window и read-only shelter measurement tool;
- 07C weather documentation package.

Пользовательский 08A/UI diff не входит в этот список и не затрагивался.

## 15. Vendor files changed

`0`. Accepted canonical fingerprint остаётся `8e376fa2…d30bd44`.

## 16. Known limitations

Полный список: `Docs/Weather/KNOWN_LIMITATIONS.md`. Критические open gates:

- accepted temporary donor material/shader reflection debt и current generated
  compatibility-material contract drift;
- dawn/night matched captures (manual route is `USER PASS`);
- 1080p development-build performance;
- strict geometry/transform side-by-side;
- public clean shipping profile;
- file save storage.

## 17. Readiness for `08_WWISE_AUDIO.md`

Audio output boundary готов технически, но полный 07C acceptance не закрыт.
Поэтому переход к 08 как к следующему milestone имеет статус **`NO-GO UNTIL
07C MANUAL/FIDELITY/PERFORMANCE EVIDENCE IS ACCEPTED`**.

Рекомендуемый следующий этап: только bounded завершение 07C production evidence
gate; не начинать новый milestone автоматически.
