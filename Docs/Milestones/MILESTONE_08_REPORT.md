# Milestone 08 — Wwise audio architecture and prototype

Status: **Completed / AutomatedValidated / UserAcceptedBoundedBaseline**  
Date: **2026-07-18**

## Scope

Milestone 08 inspected and integrated the existing M06 vehicle telemetry/audio
diagnostic, production weather/lightning outputs, typed interaction completion,
world streaming lifetime, audio settings handoff and the official Wwise
installation. No Milestone 08A UI implementation was performed.

## Wwise availability and authoring

- Wwise Authoring/SDK: **2025.1.9.9197**;
- official Unity Integration bundle: **2025.1.9.4241**;
- Wwise project:
  `MySummerCar_Remake_WwiseProject/MySummerCar_Remake_WwiseProject.wproj`;
- verified authoring: **52 events, 32 RTPCs, 4 switch groups, 3 state groups**;
- banks: **5 user banks plus `Init.bnk`**;
- mixer: **6 buses, 6 Volume RTPC curves, 7 routed Actor-Mixer roots**;
- child routing: **52/52**;
- footsteps: **10 sounds, 5 random pairs, 9/9 switch assignments**;
- generated media: **30 embedded objects, 0 loose WEM**;
- wind shelter attenuation: **present, up to -24 dB**.

The six Windows banks total **26,115,951 B**. Per-file sizes and SHA-256 values
are recorded in `Docs/Audio/WWISE_SETUP.md` and
`Docs/Audio/PERFORMANCE_REPORT.md`. Generated banks/caches remain ignored.

## Backend architecture

Gameplay depends on project-owned `IAudioBackend`, stable IDs, maps, handles,
emitters and listener/environment/surface contexts. `AudioBackendRouter` prefers
the official isolated `WwiseAudioBackend` and selects the operational
`UnityAudioBackend` fallback when required. Scene unload/backend disable removes
owned emitters and voices. Missing maps/banks are explicit diagnostics and do
not affect gameplay state.

No vehicle, interaction, weather, world, player or UI assembly calls Wwise
directly. No fake Audiokinetic types exist.

## Vehicle audio

Fixed-step telemetry maps RPM/load/throttle/gear/clutch/speed/wheel/brake/
suspension/electrical/thermal signals, typed surface and engine state. Intake,
exhaust and mechanical engine handles plus tire roll have explicit lifecycle;
starter/engine transitions are typed. Transmission, body rattle, suspension
one-shot and skid IDs are authoring-complete `DeferredHook` values until a typed
runtime producer/content design is approved.

The development-only
`Assets/Game/Audio/Content/Scenes/M08_VehicleAudioPlaytest.unity` additively
loads the existing vehicle simulation prototype and binds its telemetry,
emitter and listener to the production router. It does not place a vehicle in
`Bootstrap` and is not a production build scene. The real authored-scene and
synthetic rebind PlayMode checks pass **2/2**.

The post-remediation performance fixture measures the vehicle parameter update
at **17.906 us/iteration with 0 B allocated**.

## Weather, world and interaction

`WeatherAudioPresenter` consumes only project-owned weather/lightning/thunder
contracts. Exterior/sheltered/interior rain and wind use retained handles and
separate hysteresis: rain `0.01/0.005`, wind `0.18/0.12`. Explicit audio zones
win; otherwise production weather exposure supplies the listener's bounded
shelter/interior context. Thunder uses project position, delay and intensity.
Enviro audio is disabled and never becomes a duplicate audio owner.

Stable listener/zone/portal/surface hooks and streamed-scene cleanup exist.
Forest/lake/room-tone/traffic/bird/insect placement is `DeferredHook` rather
than falsely claimed production ambience. The interaction bridge currently
posts completed pickup/drop/throw/place/mount-handoff actions; expanded impact,
tool, fastener, part and door/gate/window events remain later typed producers.

Player footsteps are now runtime-mapped. Cadence uses grounded planar
`CharacterController` travel, crouch-aware distance and teleport reset. Surface
selection uses `IAudioSurfaceMetadataProvider` only; it never guesses from a
renderer material, texture or object name. A non-allocating raycast is performed
only when a step is due.

Weather parameter batching measures **2.930 us/iteration with 0 B allocated**.
Occlusion query budget is explicitly **0 per frame** in this milestone.

## Settings/UI handoff

The settings contract exposes master/vehicle/effects/ambience/music/UI levels,
dynamic range, mute-on-focus-loss, output-device, subtitle/caption and reduced-
loud-sounds hooks. UI events are declared but have no Milestone 08 runtime
producer. Per user direction, later menu/UI audio is a different newly authored
set and does not reuse donor gameplay audio.

## Donor and source-control boundary

All thirty selected original-game prototype clips have individual source hashes
and ledger rows. Clips and conversions are private/local/ignored
`TemporaryDirectImport`; generated banks are also ignored and are not approved
for a public/distributable build. They are prototype content, not architecture
dependencies or `ProductionReady` audio. Final gameplay/world/weather/UI audio
is newly authored and mixed. No donor code, scene/FSM audio logic, runtime
assembly or Wwise cache is committed.

## Automated evidence

| Gate | Result |
|---|---|
| Audio Runtime EditMode | **7/7 PASS** |
| Audio Runtime PlayMode | **4/4 PASS** |
| Unity fallback PlayMode | **6/6 PASS** |
| Player-footstep EditMode | **3/3 PASS** |
| Player-footstep PlayMode | **1/1 PASS** |
| Weather audio EditMode | **7/7 PASS** |
| WeatherProduction Bootstrap PlayMode | **7/7 functional PASS**, 1 explicit performance skip |
| Vehicle/Wwise playtest PlayMode | **2/2 PASS** |
| Wwise adapter EditMode | **3/3 PASS** |
| Wwise adapter PlayMode | **3/3 PASS** |
| Live official Bootstrap Wwise PlayMode | **7/7 PASS**, six banks, no missing |
| Audio performance PlayMode | **1/1 PASS** |
| Private Windows x64 Development build | **PASS** |
| Native Development Player boot | **Wwise SoundEngine initialized** |

The live Bootstrap suite runs with `-wwiseEnableWithNoGraphics`. The current
post-remediation Windows build report is **844,040,197 B**, complete folder
**844,257,747 B**, and all six copied bank hashes match the source generated
banks. Native boot initializes Wwise SDK `2025.1.9.9197`; the smoke process is
then stopped deliberately after the initialization marker.

The user separately confirmed the visible runtime diagnostic on 2026-07-18:
backend **Wwise**, banks **6**, missing banks **0**. This closes backend/bank
availability, but it is not treated as approval of the new audible mix.

The official Wwise build postprocessor intentionally removes the ignored
`StreamingAssets` bank mirror after packaging. The packaged player retained all
six matching banks, and the mirror was restored and hash-verified afterward for
continued Editor testing (`Logs/M08_Remediation_BankSync_Final.log`).

The latest full EditMode baseline is **350/356** with exactly six unrelated
Garage/World failures. It is not claimed as a full-suite pass and does not
invalidate the focused Milestone 08 gates.

Full PlayMode is **68/70**: one unrelated
`VehiclePhysicsValidation.ProductionWorld_GarageExit...` failure and one
performance test skipped by explicit environment design. The explicitly
enabled performance run passes **1/1**. The exact failed production vehicle-
route case passes **1/1 in 8.696 s** when rerun in isolation, showing an
order-dependent/flaky non-audio baseline
(`Logs/M08_Final_UnrelatedVehicleRoute_Rerun.xml`). Full PlayMode is still not
claimed as a pass.

## Performance result

The bounded capture records:

- emitters/voices: baseline **2/0**, peak **3/5**, cleanup **2/0**;
- global RTPC update: **0.782 us/iteration, 0 B**;
- vehicle parameter update: **17.906 us/iteration, 0 B**;
- weather parameter batch: **2.930 us/iteration, 0 B**;
- Unity allocated memory: **340,423,612 → 866,855,152 B**, including
  Bootstrap/world rather than audio alone;
- batch process WorkingSet unavailable; the current native smoke did not
  remeasure it (the pre-remediation reference was **454,602,752 B**);
- occlusion budget: **0**.

See `PerformanceCaptures/Milestone08/M08_Audio_Performance.json`. Headless Wwise
suspends the audio output thread, so audible output and Wwise audio-thread CPU
cannot be inferred from this capture.

## Manual acceptance and reproducible listening route

On 2026-07-18 the user closed Milestone 08 and accepted the current bounded
audio baseline. This acceptance does not claim a final mix or production
acoustics. More detailed per-zone attenuation, transition and mix tuning is
explicitly deferred to the later polishing phase.

Production weather has no keyboard shortcuts. Open
`Assets/Game/Bootstrap/Bootstrap.unity`, enter Play Mode, then use:

- `Tools > MSC Remake > Audio > Diagnostics`;
- `Tools > MSC Remake > Production Weather > Runtime Validation`;
- set `Transition seconds = 0`, choose `weather.clear`, then use
  `Force transient logical profile`: rain must be absent and the low clear-day
  wind output must remain silent;
- choose `weather.heavy_rain` or `weather.thunderstorm`: rain and wind must be
  audible outside, then distinctly quieter in the living room and garage;
- walk at least 3–5 m with WASD: grounded footsteps must sound; standing still
  or being airborne must not emit cadence steps. `Left Ctrl` verifies the
  quieter crouched cadence.

For the engine, open
`Assets/Game/Audio/Content/Scenes/M08_VehicleAudioPlaytest.unity`, enter Play
Mode and wait for `M08 vehicle audio playtest ready: production router bound to
the M06 drivable fixture.`. Controls are `I` ignition, hold `Enter` starter,
`W/S` throttle/brake, `Left Shift` clutch, `A/D` steering, `Q/E` gear down/up
and `Backspace` reset. Starter/engine transitions and RPM/load response must be
audible; the diagnostics should show emitter
`audio.emitter.vehicle.m08.playtest`.

## Principal files

- `Assets/Game/Audio/Runtime/`;
- `Assets/Game/Audio/UnityFallback/`;
- `Assets/Game/Audio/Wwise/`;
- `Assets/Game/Audio/WeatherIntegration/`;
- `Assets/Game/Audio/InteractionIntegration/`;
- `Assets/Game/Audio/PlayerIntegration/`;
- `Assets/Game/Audio/VehicleIntegration/`;
- `Assets/Game/Audio/Editor/`;
- focused EditMode/PlayMode tests under `Assets/Game/Tests/`;
- `MySummerCar_Remake_WwiseProject/` authoring data;
- `Docs/Audio/`, this report and audio provenance records.

## Remaining limitations

- fine-grained per-zone attenuation, transitions and subjective mix tuning are
  accepted as later polishing work rather than Milestone 08 blockers;
- Wwise Profiler audio-thread CPU, virtualization, starvation and final output-
  device behaviour remain later profiling work because batch mode suspends
  audio output;
- production world ambience placement, physical acoustics/occlusion and several
  declared gameplay/UI producers are deferred;
- temporary donor content is not final/distributable content;
- the report does not mark UI visuals or sounds approved.

## Exactly one next milestone

Proceed to **Milestone 08A — UI menu, settings and HUD**. Detailed audio-zone
and mix calibration remains reserved for the polishing phase.
