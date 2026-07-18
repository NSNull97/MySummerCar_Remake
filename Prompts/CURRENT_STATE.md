# CURRENT PROJECT STATE — USER CONFIRMED

Updated through the bounded Milestone 07C dawn/night follow-up automated gate
and user visual acceptance on 2026-07-18. Current generated-material contract
remediation, matched-fidelity and Development Player performance acceptance
remain pending.

## Active milestone

- The next and only active milestone is
  `07C_PRODUCTION_WEATHER_ROLLOUT_AND_VALIDATION.md`.
- Milestone 06 is treated as implemented.
- Milestone 06A is closed and human-accepted.
- Milestone 06B1 is closed and committed.
- **Milestone 06B2 — donor map streaming cellization and active world profile
  has automated status `PASS` and manual status `HumanAccepted`.**
- 06B2 is fixed in commit `79f02b0` (`world: complete milestone 06B2 v5.1`).
- **Milestone 06B3 — runtime baseline validation, debt catalogue, and freeze is
  closed with status `PASS / Frozen / HumanAccepted`.**
- The user completed the physical vehicle route through Fleetari, Teimo,
  inspection/town, the major road loop, railway crossing and a representative
  bridge. The run covered 24 cells, six consecutive boundaries, reset and OOB
  recovery without critical collision, duplicate or missing-section failures.
- The user confirmed Bootstrap startup, donor-map fidelity, a walking route to
  the lake, Teimo-area streaming unload/reload and out-of-bounds recovery.
- The corrected temporary water presentation and original-game terrain voids
  remain explicit late-remaster debt, not production art.
- **Milestone 07A — Enviro 3 preflight and WeatherLab is implemented and fixed
  in commit `61250e2` (`weather: complete milestone 07A Enviro WeatherLab`).**
- **Milestone 07B — project-owned time/weather domain and Enviro 3 adapter has
  automated status `PASS`.** The 07B deliverable itself was confined to
  WeatherLab/tests; its production rollout is the bounded 07C integration
  recorded below.
- The final vendor-neutral core result is **101/101 PASS**: GameTime `20`,
  WeatherDomain `29`, WeatherPresentation `52`. Enviro integration is **13/13
  PASS**, WeatherLab time-domain integration is **3/3 PASS**, and the automated
  Editor PlayMode performance harness is **1/1 PASS**.
- GameTime callbacks/scheduler mutations are transactionally hardened: callback
  or subscriber failure rolls back clock/queue state, reentrant mutation fails
  closed, and WeatherLab DEV advance rolls back every authoritative domain.
- Final builder/fresh preflight evidence is
  `Logs/M07B_WeatherLabBuilder_Final_03.log` with `PASS`.
- The accepted canonical Enviro baseline is `538` files / `305967931` bytes /
  `8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44`.
- **Milestone 07C production integration is built; two manual visual routes
  found bounded presentation defects, and the later user review accepted rain,
  sunset and the temporary reflection debt while requesting a dawn/night
  follow-up. Targeted automated gates pass; 07C is not yet human-accepted.**
  Bootstrap owns one persistent
  project time/weather/wetness/lightning domain and one Enviro presentation
  adapter. The linked Enviro source prefab is authored below an inactive
  project-owned backend marker and is activated only after composition-root
  ownership; same-scene/additive duplicates, same/different Single-scene
  handoff and teardown are covered by runtime lifecycle tests.
- The user's first production screenshots are recorded as a manual `FAIL`:
  constant HDRP `EV 10` overexposed day/interior and crushed night; double or
  misbound fog produced opaque/red mist; one max-X/Z shelter ellipsoid created
  an oval exterior overhang; rain spawned in Scene View but was unreadable in
  Game View because the vendor runtime renderer received
  `maxParticleSize = 0.001`.
- The bounded project-owned remediation uses a runtime-isolated production
  `VolumeProfile` with explicit rebinding, deterministic fixed exposure curve
  plus exterior/sheltered/interior offsets, one physical-visibility-to-HDRP-MFP
  fog pass, deterministic in-AABB tiled shelter zones and a runtime-only rain
  renderer floor `maxParticleSize = 0.01`. Enviro vendor files and frozen donor
  world content/transforms remain unchanged.
- The second manual route is also recorded as `FAIL`: temporary world wetness
  and full Enviro reflections read as metallic, living-room rain crossed the
  ceiling edge, indoor/day exposure remained too bright, midnight was crushed
  with an unintended aurora, and vendor `0/0/UTC0` location made 18:00–20:00
  behave like equatorial night.
- Second remediation keeps all changes project-owned and runtime-isolated:
  northern-Finland summer location `64.166 N / 24.3 E / UTC+2`
  (`RemakeDesignTarget`), same-pass sun/moon refresh, forced aurora-off after
  every quality update, restrained fixed exposure (`+0.25 EV` global with
  sheltered/interior compensation), reflection intensity `0.6`, temporary
  donor wet-smoothness caps `0.45/0.25`, and shelter ellipsoids with vertical
  stretch never below `1`.
- Second-remediation evidence is Builder `1.0.2` plus production
  validator `PASS` (`Logs/M07C_VisualRemediation2_Build.log`). Combined
  WeatherProduction/ProductionIntegration/legacy-wetness EditMode is `32/32
  PASS`, EnviroIntegration `17/17 PASS`, WeatherPresentation `53/53 PASS`,
  Production PlayMode `6/6 PASS` and explicit world-only compatibility `1/1
  PASS`; authoritative artifacts are `Logs/M07C_VisualRemediation2_*`.
- The later user review records rain and the current sunset as manual `PASS`.
  Metallic-looking reflections are accepted as temporary donor material/shader
  debt for later replacement and are not an open 07C visual fix. Night is now
  slightly too bright and visible dawn begins after 03:00, so only those two
  observations remain in the bounded visual follow-up.
- The follow-up uses project-owned runtime solar-calibration values
  `60 N / 27.3 E / UTC+3` rather than literal in-world GPS coordinates. On the
  reference date, the installed Enviro algorithm crosses the horizon at about
  `04:59` and `21:35`. A smooth `7.5 EV` minimum night exposure is applied from
  full-night `solarTime <= 0.43` toward daylight at `0.5`; daylight exposure and
  context-offset differences remain unchanged.
- Fresh follow-up evidence is EnviroIntegration `17/17 PASS`, combined
  WeatherProduction `32/32 PASS` and Production PlayMode `6/6 PASS` in
  `Logs/M07C_VisualRemediation3_*`. The user manually accepted the corrected
  dawn timing and night brightness on 2026-07-18; no capture artifact was saved.
- Current full EditMode is `328/334` in
  `Logs/M07C_VisualRemediation3_FullEditMode.xml`: the same four historical
  Garage/World failures plus two current WorldBaseline material-contract
  failures. Four ignored generated compatibility materials (`06cd8242...`,
  `2b837893...`, `5cc44389...`, `69ad9b54...`) were already rewritten at
  `2026-07-18 09:19`, before this follow-up, and no frozen payload was edited by
  the dawn/night change. Focused WorldBaseline recheck reproduces only those
  two failures at `8/10` in
  `Logs/M07C_VisualRemediation3_WorldBaseline.xml`.
- The previous frozen-world revalidation remains the last strict `PASS` in
  `Logs/M07C_VisualRemediation2_WorldFreeze.log`, with unchanged result SHA-256
  `10544fc3ed5bd6c8cd44b451155e766c0552710f4c9d8cc91dda263de001ba5f`.
  Current generated-material validation is not clean and must not be described
  as a fresh freeze PASS.
- The last accepted 06B3 freeze record remains `Frozen / automatedPass=True /
  structuralPass=True`, with 50 scenes, 49 cells, 3,842 entities and unchanged
  machine-readable result SHA-256
  `10544fc3ed5bd6c8cd44b451155e766c0552710f4c9d8cc91dda263de001ba5f`.
- Two project-owned home/garage `Interior` shelter AABBs are derived from the
  frozen donor-world reference and keyed by project stable IDs. Their former
  oversized single-ellipsoid representation is replaced by deterministic tiled
  zones whose vertical support is never pinched below a sphere. Other
  interiors, post-fix visual perimeter behavior, matched
  before/after captures and real-GPU performance remain explicitly `PENDING`.
- Automated Editor PlayMode performance evidence is recorded in
  `PerformanceCaptures/Milestone07B/M07B_WeatherLab_Performance.json`. Manual
  comparison captures and standalone/real-GPU performance sign-off remain
  `PENDING`; neither is inferred from the automated harness.

## User-confirmed world state

The user confirms:

- the original donor map has already been extracted;
- the extracted map has already been visually inspected;
- world streaming has already been connected/prototyped;
- two custom production cells exist but do not resemble their original
  locations.

The two inaccurate cells are now treated as:

```text
PrototypeOnly
RejectedForFidelity
InactiveInFeatureParityProfile
NotProductionReady
```

Do not hand-remodel them during the immediate feature-parity phase.

Preserve useful streaming architecture, cell IDs, registries, tests and
project-owned metadata. Disable the inaccurate custom visual roots and use the
exact donor map as the temporary runtime baseline.

## World strategy — fixed

### Feature-parity phase

Use the already extracted original map as a sanitized, deterministic
`TemporaryDirectImport` runtime baseline.

Allowed baseline content may include donor-derived terrain, world geometry,
roads, buildings, props, vegetation, temporary materials/textures and collision.

Forbidden baseline content includes donor scripts, PlayMaker FSMs, runtime
assemblies, old UnityEngine references, player/vehicle/NPC logic, cameras,
lighting, weather, audio, UI, saves, Steam/platform code and gameplay managers.

The baseline may be used in private local feature-parity development builds. It
is not final art and is not allowed in a distributable/public build without
explicit rights.

### Remaster phase

Replace the baseline cell by cell through production override layers while
preserving coordinates, stable IDs, gameplay anchors, saves and streaming.

## Next allowed sequence

Current allowed sequence:

1. keep frozen `DonorWorldBaseline-v001` unchanged except through the recorded
   regeneration policy;
2. finish only the bounded 07C manual/fidelity/performance evidence next;
3. continue to Milestone 08 only after the user accepts Milestone 07 production
   rollout and its validation.

Do not run the deprecated
`06B_PRODUCTION_WORLD_CELL_FIDELITY_GATE.md`.
Do not repeat 07A or 07B and do not start Milestone 08 before 07C closes.

## Fixed design decisions

Read `PROJECT_DESIGN_GUARDRAILS.md` before every future milestone.

Key decisions:

- donor feature parity first;
- exact donor map baseline before production-art replacement;
- real donor references outrank concept art;
- no physical full-body first-person player in current scope;
- lightweight viewmodel hands only for approved actions;
- Enviro 3 is the visual environment backend;
- project-owned systems own game time, weather, wetness, lightning, saves and
  cross-system outputs;
- Enviro vendor source is read-only;
- Enviro audio is not the production audio authority;
- no GPS, RPG inventory, permanent quest tracker or exact hidden simulation
  percentages in the default experience.

## Repository paths

The currently opened repository and local configuration are authoritative.

Historical possible paths include:

- `E:\GAYmDev_Studio\MySummerCar_Remake`
- `E:\GAYmDev_Studio\MySummerCar_Remake_Game`

Expected donor installation may be:

- `D:\SteamLibrary\steamapps\common\My Summer Car`

Read actual paths from project-local configuration such as
`Config/DonorPaths.local.json`. Never move or rename the repository or donor
installation automatically.

## Enviro package status

Enviro 3 is imported and its local public API is validated. Exact semantic patch
metadata is not authoritative, so the installation is identified by the
canonical 07B fingerprint `538 / 305967931 / 8e376fa2…`. The vendor source is a
read-only third-party presentation dependency, not donor content and not game
state authority. Project-owned GameTime, weather fronts, wetness, lightning and
save DTOs must remain independent of Enviro runtime objects.
