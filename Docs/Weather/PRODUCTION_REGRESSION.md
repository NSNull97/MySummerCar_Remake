# Production weather regression matrix

Дата среза: 2026-07-18.

Статус: `THIRD_VISUAL_ROUTE_PARTIAL_PASS /
SOLAR_NIGHT_FOLLOWUP_AUTOMATED_PASS / CURRENT_WORLD_MATERIAL_CONTRACT_FAIL /
TARGETED_DAWN_NIGHT_USER_PASS`.

## Visual regression found by the user

Ручной production-проход и переданные скриншоты зафиксировали реальный
`MANUAL FAIL` после предыдущего engineering gate:

- постоянная HDRP exposure `EV 10` пересвечивала день и интерьер, но давила
  детали в ночи;
- fog применялся дважды/через неверную binding-цепочку, что давало непрозрачную
  и красную пелену;
- один ellipsoid на shelter AABB создавал заметную овальную зону защиты за
  пределами дома/гаража;
- rain particles создавались и были видны в Scene View, но были практически
  нечитаемы в Game View: vendor runtime renderer получал
  `maxParticleSize = 0.001`.

Этот evidence не переименовывается в PASS. Он является before-state для
текущей bounded remediation.

Повторный ручной маршрут после первой remediation также дал `MANUAL FAIL`:

- terrain и interior surfaces выглядели чрезмерно отражающими/металлическими;
- дождь проходил через гостиную у телевизора;
- дневной интерьер оставался пересвеченным;
- ночь была почти чёрной, при этом появлялась нежелательная aurora;
- в `18:00–20:00` темнело существенно раньше ожидаемого финского летнего цикла.

Это второй before-state. Его исправления требуют нового ручного маршрута и не
считаются принятыми только по automated evidence.

Третий ручной маршрут дал частичное принятие:

- текущая rain presentation подтверждена пользователем;
- текущий вечерний закат подтверждён;
- чрезмерные отражения признаны временным дефектом donor-материалов/шейдеров,
  которые будут заменены, и не исправляются дальше в bounded 07C;
- ночь после предыдущего исправления стала немного слишком яркой;
- рассвет начинался после `03:00`, тогда как целевой ориентир — около `05:00`.

Последние два пункта являются текущим before-state для solar/night follow-up;
после follow-up пользователь принял скорректированные рассвет около `05:00` и
ночную яркость (`USER PASS`, 2026-07-18). Capture artifact для этой проверки не
создавался; matched fidelity route этим не закрывается.

## Implemented project-owned remediation

- Production HDRP `VolumeProfile` теперь изолируется runtime-копией и
  явно rebinding-ится к активному production owner; shared/source profile не мутируется.
- Fixed exposure осталась детерминированной, но вместо одного `EV 10`
  использует time-of-day curve и project exposure-context offsets для
  exterior/sheltered/interior.
- Project visibility конвертируется в физический HDRP mean free path; fog
  применяется один раз одним owner-ом.
- Широкий shelter AABB представляется детерминированным набором
  вписанных removal ellipsoids, что убирает прежний большой овальный overhang.
- Только для runtime-копий rain renderers введён нижний предел
  `maxParticleSize = 0.01`; vendor prefab/source не менялся.
- Runtime-only solar calibration установлена в `60 N`, `27.3 E`, `UTC+3`, а
  sun/moon position обновляется в том же presentation pass после project time.
  Для `1995-08-01` Enviro horizon crossing приходится примерно на `04:59`
  (рассвет) и `21:35` (закат).
- Ночная fixed exposure плавно ограничивается минимумом `7.5 EV` между
  `solarTime 0.43 -> 0.50`; более высокий Fixed Exposure EV затемняет ночь,
  при этом дневная ветвь curve не меняется.
- Aurora принудительно выключается после каждого quality application.
- Exposure policy использует daylight bias `+0.25 EV` и context offsets
  `Exterior 0 / Sheltered -0.15 / Interior -0.25 EV`.
- Temporary donor baseline reflection intensity ограничена `0.6`, а wet target
  smoothness — `0.45` для opaque и `0.25` для alpha-clip.
- Shelter ellipsoids сохраняют deterministic horizontal tiling, но их vertical
  stretch теперь не меньше `1`, что покрывает living-room eye/ceiling probes.
- Enviro vendor payload и frozen donor world остались неизменными.

## Current follow-up evidence

| Check | Result | Interpretation |
|---|---:|---|
| `Logs/M07C_VisualRemediation2_Build.log` | PASS | Unity compile, Builder `1.0.2`, validator, one owner, Medium and accepted vendor fingerprint |
| `Logs/M07C_VisualRemediation3_EnviroIntegration.xml` | 17/17 PASS | Enviro integration, solar calibration and night-exposure policy |
| `Logs/M07C_VisualRemediation3_WeatherProduction.xml` | 32/32 PASS | Combined WeatherProduction/ProductionIntegration/legacy-wetness contracts |
| `Logs/M07C_VisualRemediation2_WeatherPresentation.xml` | 53/53 PASS | Vendor-neutral exposure/fog/frame contracts |
| `Logs/M07C_VisualRemediation3_ProductionPlayMode.xml` | 6/6 PASS | Production lifecycle, restore/reveal and owner teardown |
| `Logs/M07C_VisualRemediation2_WorldOnlyCompatibility.xml` | 1/1 PASS | Explicit world-only compatibility remains intact |
| `Logs/M07C_VisualRemediation3_WorldBaseline.xml` | 8/10 | Focused rerun reproduces the same two generated-material contract failures; not test-order pollution |
| `Logs/M07C_VisualRemediation3_FullEditMode.xml` | 328/334 | Four historical failures plus two current WorldBaseline material-contract failures; all 07C follow-up cases pass |
| Prior `Logs/M07C_VisualRemediation2_WorldFreeze.log` | PASS (prior evidence) | Frozen result SHA-256 was `10544fc3ed5bd6c8cd44b451155e766c0552710f4c9d8cc91dda263de001ba5f`; this is not relabelled as a current pass |
| Manual production visual route | PARTIAL PASS | rain, sunset and corrected dawn/night accepted by user on 2026-07-18; reflections accepted as temporary debt; no dawn/night capture artifact |

## Pre-remediation automated artifacts

Эти artifacts объясняют предыдущий engineering baseline, но не считаются
post-remediation revalidation:

| Artifact | Result | Interpretation |
|---|---:|---|
| `Logs/M07C_ProductionBuild_17.log` | PASS | Builder `1.0.0`, one owner, valid bindings, two shelters, exact 20-entry wetness profile, private guard, default Medium, accepted vendor fingerprint |
| `Logs/M07C_ProductionValidation_Final.log` | PASS | Fresh validation of saved Bootstrap |
| `TestResults/M07C_Production_EditMode_Final_10.xml` | 31/31 PASS | Content/contracts/persistence/quality, 8 DEV API tests, legacy wetness and authoring repeatability |
| `TestResults/M07C_Enviro3Integration_EditMode_Final.xml` | 16/16 PASS | Low/Medium/High bindings, numeric compatibility, adapter isolation/audio/autonomy, wind/WeatherLab lifecycle |
| `TestResults/M07C_WeatherPresentation_EditMode_Final.xml` | 53/53 PASS | Stable IDs, assembly boundary, frame/Medium/wind validation, mapper, global wetness contract |
| `Logs/M07C_LegacyWetness_EditMode.xml` | 5/5 PASS | Category-specific per-slot MPB, foreign block preservation, exact reset, quantized skips, reviewed/excluded coverage |
| `TestResults/M07C_Production_PlayMode_Final_10.xml` | 6/6 PASS | Restore/reveal, additive duplicate, same-scene duplicate/reload, rootless teardown and different-Single world-only handoff |
| `TestResults/M07C_WorldOnlyCompatibility_PlayMode_02.xml` | 1/1 PASS | Existing WorldRemaster fixture remains operational in explicit world-only mode |
| `Logs/M07C_WorldOnlyAuthoringValidation.log` | PASS | 05B.1 validator reports errors=0/warnings=0 with explicit world-only startup |
| `Logs/M07C_ActiveBootstrapCellizationValidation.log` | PASS | 06B2 validator keeps active Bootstrap startup composition coherent |
| `TestResults/M07C_EditMode_Full_04.xml` | 328/332 PASS | Full EditMode snapshot; all 07C cases PASS, four known unrelated failures |
| `Logs/M07C_WorldFreezeValidation_Final.log` | PASS | Frozen/structural pass, 50 scenes, 49 cells, 3842 entities, unchanged result hash |

Предыдущий accepted 07B evidence остаётся базой:

- core GameTime + WeatherDomain + WeatherPresentation: `101/101 PASS`;
- Enviro integration: `13/13 PASS`;
- WeatherLab time/front/callback edge: `3/3 PASS`;
- automated Editor performance harness: `1/1 PASS`.

## Current full-suite failures

1. `GaragePrototypeContentTests.LightingPreset_UsesPhysicalSkyFogFixedExposureAndAces` — старое ожидание M3 lighting profile.
2. `GaragePrototypeContentTests.Validator_ReportsNoMilestoneThreeErrors` — следствие того же Garage M3 contract drift.
3. `WorldValidationEditModeTests.CurrentWorld_ReportsExactCoverageAndAchievesOnlyPilotGate` — старое PilotGate expectation после более позднего world progression.
4. `WorldTransferDataTests.Validation_DryRunMatchesDatabasePlan` — external
   donor source hash drift в `sharedassets3.assets/resource`.

К ним в текущем `328/334` добавились две WorldBaseline material-contract
ошибки:

5. `DonorWorldCellizationEditModeTests.FullValidatorContract_PassesWithoutInspectingEveryGeneratedScene`.
6. `DonorWorldCellizationEditModeTests.GeneratedPresentation_UsesSharedHdrpAssetsAndNoDonorShaderPayload`.

Обе указывают на ignored generated materials с source GUID
`06cd824234ef21f40b2c797b15acbd26`,
`2b8378937c6afb64390d5474f4bcd14a`,
`5cc44389f1f10bf4cabee6d33feb1551` и
`69ad9b54c687ad847a9df25112c2d430`. Их `LastWriteTime 09:19:37`
предшествует этому follow-up; frozen generated payload не исправлялся и не
перегенерировался в weather scope.

Первые четыре совпадают с pre-07C/pre-07B known baseline и не исправлялись в
weather scope. Последние две воспроизводятся focused WorldBaseline suite
`8/10`, то есть являются текущим persistent material-contract failure, а не
test-order pollution или weather regression, и не могут быть скрыты прежним
freeze PASS. Current full run не является зелёным `334/334`; корректный статус
— `328/334`.

## Pre-remediation automated coverage disposition (historical)

| Contract | Status | Notes |
|---|---|---|
| One active Bootstrap environment owner | PASS | Validator + content tests |
| Linked inactive Enviro source + project activator | PASS | Activates after ownership; installer waits readiness; committed scene keeps prefab link |
| Old Bootstrap sky/sun/fog placeholders inactive | PASS | Validator checks active directional/global owners |
| Direct Enviro dependency isolated | PASS | asmdef/content tests |
| Enviro autonomous time/weather/audio disabled | PASS | integration lifecycle tests |
| Stable clear/cloud/rain/storm/fog/night mappings | PASS | binding and mapper tests |
| Low/Medium/High stable IDs/assets | PASS | direct assets; serialized compatibility preserved |
| Project wind writes one Enviro-owned WindZone | PASS in contract/integration suite | Production visual response pending |
| Session DTO validation/atomic restore | PASS | invalid payload rejected before partial mutation |
| Immediate gameplay strike blocked after restore | PASS | project restore grace |
| Global wetness shader contract | PASS | dirty-only revision semantics |
| Legacy material MPB mechanism | PASS in combined 32/32 | Exact 20-entry reviewed allowlist and wet targets `0.45/0.25` present; production route/visible slot coverage pending |
| Shelter resolver/DDOL discovery | PASS | Persistent root is scanned explicitly; PlayMode lifecycle 6/6 |
| Home house/garage shelter authoring | PASS | Two frozen-derived Interior AABBs; evidence asset/log and saved Bootstrap validated |
| Enviro shelter visual shape | SECOND REMEDIATION / MANUAL RETEST PENDING | Deterministic in-AABB tiling retained; vertical stretch floor `1` covers evidence-derived living-room probes |
| Road wetness future hook | PASS | Physics deliberately remains dry |
| WeatherLab/vendor weather scenes excluded from Build Settings | PASS | Public donor-content exclusion is a separate NO-GO |
| Frozen world structural/hash audit | PRIOR PASS / CURRENT MATERIAL VALIDATION FAIL | Prior strict run had `automatedPass=True`, `structuralPass=True` and unchanged result hash; current full suite reports two generated-material contract failures, so no fresh frozen PASS is claimed |
| Production lifecycle PlayMode | CURRENT PASS 6/6 | `Logs/M07C_VisualRemediation2_ProductionPlayMode.xml` |
| Production DEV API | PASS | Included in the combined 32/32 production suite |
| Vendor fingerprint | PASS | 538 files / 305967931 bytes / accepted SHA unchanged; manual runtime route still pending |

## Manual production matrix

| Scenario | Required observation | Status |
|---|---|---|
| Neutral clear-day canonical route | matched camera/FOV/time/exposure | PARTIAL USER ACCEPTANCE: overall acceptable; residual reflections explicitly retained as temporary donor material/shader debt |
| Dawn and dusk | sky/sun/exposure/readability | SUNSET AND CORRECTED DAWN USER PASS 2026-07-18; no capture artifact, matched route still PENDING |
| Night with headlights | road/interactions/fog readability | NIGHT BRIGHTNESS USER PASS 2026-07-18 after smooth minimum `7.5 EV`; headlight-specific capture remains PENDING |
| Clear → partly cloudy → overcast → rain | transition continuity and no reset | PENDING |
| Rain → drying | global state and supported surfaces | PENDING |
| Heavy rain | visibility/particles/cell continuity | USER PASS for current rain presentation; cell-continuity stress route remains PENDING |
| Thunderstorm | ambient/gameplay separation and fair cooldown | PENDING |
| Morning mist/fog | shoreline/road/interior behavior | PRE-REMEDIATION FAIL / RETEST PENDING: opaque/red mist |
| Exterior → interior → exterior | rain/fog/audio/wetness exclusion | Reported rain issue USER PASS; full fog/audio/perimeter and non-home interiors remain PENDING |
| Driving across cell boundaries | no owner duplicate/state reset/pop | PENDING |
| Load during transition | no clear flash and schedule continuity | PENDING |
| Load wet ground | restored wetness before reveal | PENDING |
| Load after lightning | no immediate gameplay strike | AUTOMATED PASS / MANUAL PENDING |
| Low/Medium/High | visual and performance comparison | PENDING |

## Scope isolation

07C regression scope не включает user-owned 08A/UI diff. Документы и tests не
используют его как evidence и не меняют `AGENTS.md`, UI references/prompts/assets,
UI roadmap или porting ledger. `CURRENT_STATE` обновляется только фактами
этой 07C remediation.

## Gate result

Solar/night follow-up targeted regression: EnviroIntegration `17/17`, combined
WeatherProduction/ProductionIntegration/legacy-wetness `32/32` and Production
PlayMode `6/6` — `PASS`. Full EditMode is `328/334`: four historical failures
plus two current ignored generated-material contract failures. Prior strict
freeze PASS remains historical evidence, but a fresh frozen PASS is not claimed.
Manual rain, sunset, corrected dawn and night brightness are accepted on
2026-07-18 without capture artifacts. Reflections remain user-approved temporary
material/shader debt; matched fidelity comparison, full route and performance
sign-off remain `PENDING`, so Milestone 07C must not be marked fully `COMPLETED`.
