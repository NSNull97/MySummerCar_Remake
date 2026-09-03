# Known production weather limitations

## Current celestial ownership (2026-08-13)

- The production Bootstrap selects `EnviroLegacy`. Enviro owns the HDRP sky,
  moving sun and moon, stars, clouds, and the bounded rain/splash presentation.
- The project clock and calendar remain authoritative. The
  `Enviro3EnvironmentAdapter` pushes every accepted date/time revision into
  Enviro and updates its celestial transforms; a two-minute clock step is
  smoothed for `0.3 s` instead of being turned into a binary day/night cut.
- `NativeHdrpWeatherBridge` is the single fog, exposure, and indirect-lighting
  writer in the hybrid route. The Enviro sky Volume intentionally contains no
  competing Fog, Exposure, or Indirect Lighting overrides.
- Full Native HDRP remains a reversible fallback. It has no accepted moon or
  stars, no longer advertises `SunMoonLighting`, and must not become the
  production default until that visual parity is implemented and accepted.
- Headless lifecycle checks do not replace rendered moon-phase, night
  readability, rain-fidelity, or Windows-player CPU/GPU acceptance.
- Runtime-only Finnish sky calibration now suppresses the all-night warm horizon,
  raises the clear-night star floor, enlarges the moon to a readable minimum,
  and provides an art-directed maximum `8 lux` full-moon light for the accepted
  `7.25 EV` game camera. The light is cold, phase/cloud/altitude-aware, and is
  exactly zero at new moon or below the astronomical horizon. The moon remains
  on Enviro's realistic orbit, so changing weather at one fixed date/time does
  not guarantee that it is above the horizon.

Дата среза: 2026-07-18.

Дополнение Native HDRP visual correction: 2026-08-13.

Эти ограничения являются частью честного статуса 07C и не должны молча
переименовываться в PASS.

## Vendor identity and compatibility

- Точный semantic patch Enviro неизвестен. Authoritative identity:
  `538 files / 305967931 bytes /
  8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44`.
- Локальные version markers противоречат друг другу (`3.0.0`, `3.0.7`, changelog
  through `3.0.8`).
- Vendor `CS0618` warnings не исправляются project patch-ами.
- Additional Weather Pack не требуется и не является частью принятой базы.

## Manual evidence

- Первый production visual route закончился `FAIL`: были зафиксированы
  пересвеченные день/интерьер, crushed night, opaque/red fog,
  овальный shelter overhang и нечитаемый в Game View дождь.
- Повторный production visual route после первой remediation также закончился
  `FAIL`: temporary donor surfaces выглядели металлическими, дождь проходил
  через гостиную у телевизора, интерьер оставался пересвеченным, ночь была
  почти чёрной с нежелательным aurora, а в `18:00–20:00` темнело слишком рано.
- Третий ручной маршрут дал `PARTIAL PASS`: rain presentation и
  текущий вечерний закат подтверждены пользователем; остаточный metallic/
  reflection look принят как временный долг donor-материалов/шейдеров до их
  плановой замены.
- На том же маршруте ночь оказалась немного слишком яркой, а рассвет начинался
  после `03:00`. Runtime-only solar/night follow-up реализован, но ручная
  перепроверка скорректированных рассвета около `05:00` и ночной яркости получила
  `USER PASS` 2026-07-18. Capture artifact не создавался; matched fidelity и
  headlight-specific route этим не закрыты.
- Нет matched 07C before/after clear-day captures.
- Нет strict side-by-side package с camera/FOV/time/exposure metadata.
- Нет полного manual production weather route.
- Нет production load-during-transition/wet-ground/recent-lightning video.
- Нет Low/Medium/High production comparison.
- Принятый 07A WeatherLab smoke не считается 07C world evidence.

## Performance

- 07B Editor PlayMode 640×480 artifact не является 1080p production sign-off.
- Нет 07C Windows x64 Development Player capture.
- GPU/Render Thread/cloud/fog/reflection decomposition на donor world отсутствует.
- Нельзя заявлять stable 60 FPS или правильный cost order quality tiers.

## Wetness/materials

- Global MSC wetness shader values существуют, но обычный HDRP/Lit не читает их
  автоматически.
- Legacy MPB bridge механически проверен; production profile содержит 20
  reviewed GUID entries (4 ground, 5 road, 6 exterior roof, 5 vegetation).
  Это bounded allowlist, не обещание полного покрытия всех видимых slots.
- Manifest содержит 293 materials: 251 OpaqueLit, 14 AlphaClipLit, 12
  TransparentLit, 7 Unlit, 5 EmissiveLit, 2 TemporaryWater, 1 TransparentUnlit
  и 1 Unsupported. Совместимость shader family не означает approved coverage.
- Transparent/water/unlit/emissive/unsupported исключены из generic wetness.
- Final puddle masks/ripples и production material reauthoring отсутствуют.
- Для temporary donor baseline wet target smoothness ограничена значениями
  `0.45` для opaque и `0.25` для alpha-clip, а reflection intensity — `0.6`.
  Это bounded mitigation, не финальная reauthoring материалов.
- Оставшийся отражающий/metallic вид пользователь разрешил оставить до замены
  temporary donor материалов и шейдеров; это documented accepted visual debt,
  а не закрытая production-material fidelity.
- Wet-road friction отключена; dry 06A multiplier остаётся `1.0`.

## Shelter/interiors

- Resolver и Enviro removal-zone bridge реализованы для project-owned
  axis-aligned stable-ID volumes.
- Builder/validator создали и проверили два home house/garage Interior AABB из
  frozen renderer bounds; persistent DDOL root scan покрыт lifecycle tests.
- Migration v7 классифицирует все 24 активных donor `NoRain` records: 23
  static/Phase-1-overlay records имеют project-owned runtime coverage, а
  динамический bus record закреплён за vehicle-cabin presenter и намеренно не
  превращён в статическую world zone. Streaming catalog содержит 20 definitions;
  три home-house records покрывает существующий compound volume. Машинохоллы
  farm/strawberry/home-yard и оба моста добавлены как open shelters.
- Teimo shop/pub, Fleetari и inspection hall/office используют точные donor
  horizontal footprints и ClosedInterior profile. Open-shelter profile оставляет
  exterior audio/fog/wind/thunder/lighting, поэтому навес больше не должен
  создавать ложный indoor sound state. Полный visual/audio perimeter route всё
  ещё не выполнен.
- Прежняя схема `one AABB -> one max-X/Z ellipsoid` подтверждённо
  давала ложную защиту снаружи. Теперь AABB разбивается на
  детерминированные вписанные ellipsoids, а их vertical stretch не может быть
  меньше `1`; это сохраняет покрытие реальных eye/ceiling probes и не расширяет
  horizontal footprint. Текущий rain route принят; manual perimeter/fog retest
  ещё нужен.
- Rotated/non-box shelter geometry не поддерживается bounded contract.
- Открытие garage doors динамически не изменяет статический volume; open
  doors/windows требуют manual review и будущего bounded portal policy.

## Weather presentation

- Clear, partly cloudy, bright overcast, heavy overcast, fog, and precipitation
  states resolve to isolated runtime weather clones. Project `CloudCoverage01`
  maps monotonically into Enviro coverage. Partly cloudy uses scattered eroded
  volumes; bright overcast uses a light broken deck rather than a cirrus veil;
  heavy overcast remains closed and more absorbent. Clear cirrus is restrained.
- A deterministic weather-map offset changes when the stable weather binding
  changes, distributing formations across the sky without per-frame noise.
  The same single cloud layer moves through Enviro's existing animation from
  the project-owned wind. Dual-layer rendering, map resolution and ray-march
  budgets are unchanged; final speed and formation fidelity remain manual
  acceptance items.
- Finnish midday tint is slightly cooler/neutral and hybrid daylight exposure is
  `12.65 EV`; the accepted readable-night EV target is unchanged. Final colour
  and brightness acceptance still requires matched rendered captures.
- Presentation cloud intensity is optical density rather than ambient
  readability. Enviro direct sunlight and directional shadow strength now fade
  continuously from clear to closed cover; the production `0.62` mostly-cloudy
  profile is approximately `0.20` of clear direct light. This is automated in
  Bootstrap, but final Finnish colour/contrast and target-GPU acceptance remain
  manual.
- Enviro route всё ещё использует один base Rain source через разные runtime
  clones и intensity remap. Его rain runtime clone использует density multiplier,
  bounded drop scale `0.25–0.48`; screen-size cap уменьшен до `0.0035` после
  пользовательского замечания о слишком крупных редких каплях. Collision
  включает уже существующий vendor-authored `Rain_Splash` sub-emitter и
  настраивается только на runtime clone; vendor prefab/material не изменены.
- Full-Native HDRP fallback независимо нормализует существующие
  camera-local emitters: `4200/s`, максимум `5200` rain particles, footprint
  `18 x 14 m`, Low/static collision и один impact pool на `600` particles.
  GPU PlayMode подтвердил реальные roof collisions и impact emission; финальная
  плотность, размер и читаемость всё ещё требуют ручного visual route.
- Scheduled transition duration корректно переводится из оставшихся game
  seconds front-а в simulation seconds, но visual easing и lightning bolt
  intensity остаются bounded approximations локального public API.
- Native HDRP выполняет sequenced `RequestSkyEnvironmentUpdate`; Enviro route
  сохраняет regular vendor update.
- Reflection/ambient refresh запрашивается по binding/`600` game seconds/`25 m`
  listener movement и coalesces до `1 s`, но production cost не измерен.
- Runtime presentation использует калибровку `60 N`, `27.3 E`, `UTC+3`;
  для `1995-08-01` Enviro horizon crossing приходится примерно на `04:59` и
  `21:35`. Sun/moon обновляются в том же presentation pass после установки
  времени; project clock остаётся authority.
- Aurora принудительно отключается после каждого quality application, включая
  vendor Low/Medium presets, где она включена в source asset.
- Wind mapping реализован на один WindZone, но vegetation response на полном
  donor baseline не принят визуально.

## Fog and water

- Active Lake Mist/Dense Fog now targets `80 m` visibility, permits a `20 m`
  HDRP mean-free-path floor, and caps dense-fog maximum distance at `350 m`.
  This makes the state structurally denser; final visibility and GPU cost remain
  manual acceptance items.
- Прежние fixed `EV 10` и double/misbound fog привели к ручному `FAIL`:
  пересвеченный день/интерьер, crushed night и opaque/red mist.
- Full-Native HDRP fallback использует progressive center-weighted Automatic
  exposure с bounded EV limits и локальной interior compensation. Он больше не
  подавляет indirect diffuse до прежних `0.18–0.38`; powered fixtures должны
  оставаться читаемыми, но переход улица/дом и ночь требуют visual acceptance.
- Production water — temporary transparent proxy, не final HDRP Water system.
- Shoreline, water fog, transparent windows и headlights не имеют актуальных
  matched captures. Rain impacts имеют automated GPU collision evidence, но не
  ручной fidelity capture.
- Underwater/swimming/buoyancy отсутствуют.
- Native rain теперь уничтожается static world collision и мгновенно очищается
  при нулевой local precipitation exposure. Interior fog/precipitation leakage
  всё равно не проверена полным визуальным маршрутом.
  Все donor-evidenced static зоны теперь authoring/lifecycle-covered, но это не
  заменяет проход по периметру каждого помещения и проверку дверей/окон.

## Lightning and audio

- Gameplay lightning остаётся non-lethal до появления damage/health contract.
- Native presentation имеет deterministic segmented bolt и multi-pulse flash;
  автоматическая гроза и финальная читаемость/аудиомикс требуют ручной проверки.
- Forest fire, electrical-grid effects и final attractor content отсутствуют.
- Enviro audio выключено; Wwise content и final thunder mix не реализованы.
- Typed audio outputs готовы только как consumer boundary.

## Save/load

- Реализован session DTO и atomic in-memory restore.
- File storage, slots, migration registry, corruption recovery и UI остаются M09.
- Current follow-up Production lifecycle PlayMode прошёл `6/6 PASS`:
  `Logs/M07C_VisualRemediation3_ProductionPlayMode.xml`. Pixel evidence
  отсутствия clear-sky flash в ручном production route всё ещё не зафиксирован.

## World fidelity and content policy

- Предыдущий strict frozen audit прошёл для 50 scenes/49 cells/3842 entities с
  неизменным result hash, но текущие full/focused suites обнаруживают две
  generated-material contract failures. Поэтому прежний PASS сохраняется как
  historical evidence, а свежий frozen PASS не заявляется.
- Donor visual debt остаётся видимым: terrain voids/tree walls, stretched
  textures, proxy vegetation/water и incomplete collision.
- Текущий profile private-only и содержит 50 donor scenes.
- Public/distributable shipping profile отсутствует и имеет `NO-GO`.

## Test status

Актуальный solar/night follow-up automated evidence:

- compile/builder/validator `1.0.2`: `PASS`,
  `Logs/M07C_VisualRemediation2_Build.log`;
- EnviroIntegration EditMode: `17/17 PASS`,
  `Logs/M07C_VisualRemediation3_EnviroIntegration.xml`;
- combined WeatherProduction/ProductionIntegration/legacy-wetness EditMode:
  `32/32 PASS`, `Logs/M07C_VisualRemediation3_WeatherProduction.xml`;
- WeatherPresentation EditMode: `53/53 PASS`,
  `Logs/M07C_VisualRemediation2_WeatherPresentation.xml`;
- Production PlayMode: `6/6 PASS`,
  `Logs/M07C_VisualRemediation3_ProductionPlayMode.xml`;
- world-only compatibility: `1/1 PASS`,
  `Logs/M07C_VisualRemediation2_WorldOnlyCompatibility.xml`;
- focused WorldBaseline: `8/10`, те же две generated-material contract failures,
  `Logs/M07C_VisualRemediation3_WorldBaseline.xml`;
- full EditMode: `328/334`: четыре historical failure плюс те же две текущие
  WorldBaseline material-contract failures,
  `Logs/M07C_VisualRemediation3_FullEditMode.xml`;
- prior frozen-world validation: `PASS`, result SHA-256
  `10544fc3ed5bd6c8cd44b451155e766c0552710f4c9d8cc91dda263de001ba5f`,
  `Logs/M07C_VisualRemediation2_WorldFreeze.log`; это historical evidence, не
  свежий PASS;
- production manual follow-up: rain/sunset/corrected dawn/night brightness
  `USER PASS 2026-07-18`, reflections accepted debt; dawn/night capture artifact
  отсутствует, matched fidelity остаётся `PENDING`.

Две текущие WorldBaseline failures относятся к ignored generated materials с
GUID `06cd824234ef21f40b2c797b15acbd26`,
`2b8378937c6afb64390d5474f4bcd14a`,
`5cc44389f1f10bf4cabee6d33feb1551` и
`69ad9b54c687ad847a9df25112c2d430`. Их timestamp `09:19:37` предшествует
follow-up; frozen payload не менялся из-за freeze policy. Focused `8/10`
подтверждает persistent drift, а не test-order pollution.

Ранее записанные `328/332`, `16/16` и другие pre-remediation
artifacts сохраняются только как historical evidence.

## DEV tooling boundary

- Runtime Validation window работает только в Editor Play Mode и не является
  shipping UI.
- Оно управляет ambient visual lightning, но не создаёт production gameplay
  strike candidates/protection content.
- Shelter overlay ничего не author-ит; отдельный measurement/builder path
  создал только два frozen-derived home Interior AABB; визуальная граница
  tiled ellipsoids требует отдельной ручной проверки.
- Canonical route automation, geometry diff, vendor change report, performance
  capture и public shipping profile остаются отдельными tools/evidence tasks.

## Scope isolation

User-owned 08A/UI diff присутствует в worktree, но не является частью 07C и не
затрагивался. `AGENTS.md` не редактировался; `CURRENT_STATE` и porting records
обновлены только фактами 07C.
## Active presentation route — 2026-08-13

- Production Bootstrap currently selects the Enviro/HDRP hybrid: Enviro owns the
  sky, sun, moon, stars, clouds and its bounded runtime precipitation; the
  project-owned HDRP bridge owns fog, exposure and indirect-lighting overrides.
- The complete Native HDRP backend remains an explicit reversible fallback. Its
  corrected solar rotation and twilight handling are covered by tests, but it
  is not the default until moon/star parity and visual acceptance exist.
- Final visual acceptance and matched Windows-player performance capture are
  still required for the hybrid route.
