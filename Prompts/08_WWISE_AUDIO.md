/plan

# MILESTONE 08 — WWISE AUDIO ARCHITECTURE AND PROTOTYPE

Read `AGENTS.md` completely.

Read:

- all reports through Milestone 07;
- `Docs/AUDIO_GUIDE.md`;
- vehicle telemetry and simulation reports;
- world/interior/streaming reports;
- weather outputs;
- reference-capture audio inventory;
- package manifest and assembly definitions;
- current Git status and diff.

## Objective

Implement or complete a robust audio-backend architecture and a Wwise-powered
prototype when the official integration is available.

The Unity project must continue to compile with a Unity-audio fallback when
Wwise is absent, unless the project has explicitly standardized on mandatory
Wwise installation.

## Pre-flight

Inspect:

- whether the official Audiokinetic Wwise Unity integration is installed;
- exact integration version;
- compatible Unity version evidence;
- Wwise project location;
- generated SoundBank location;
- platform settings;
- current audio backend code;
- existing donor/reference clips;
- licensing/setup documentation already present.

If Wwise is absent:

1. Do not install it silently.
2. Implement all backend-independent contracts, data, validation, and Unity
   fallback work.
3. Create exact official installation/setup instructions.
4. Mark Wwise-specific execution blocked.
5. Do not fake Wwise types.

## Architecture

Create or align:

- `IAudioBackend`;
- `IAudioEventHandle`;
- `IAudioEmitter`;
- `AudioEventId`;
- `AudioParameterId`;
- `AudioSwitchId`;
- `AudioStateId`;
- `AudioListenerContext`;
- `AudioEnvironmentContext`;
- `AudioSurfaceContext`;
- `AudioBackendRouter`;
- `UnityAudioBackend`;
- `WwiseAudioBackend` only behind official integration availability;
- `AudioEventMap`;
- `AudioParameterMap`;
- `AudioValidationService`;
- development debug tools.

Keep gameplay systems dependent on project-owned contracts.

Do not spread direct Wwise API calls through vehicle, player, weather, or world
code.

## Vehicle audio

Map stable parameters from telemetry:

- RPM;
- engine load;
- throttle;
- starter state;
- ignition;
- engine running/stalled;
- gear;
- clutch slip;
- vehicle speed;
- wheel speed;
- wheel slip;
- brake input;
- suspension impact;
- surface type;
- damage/wear hooks;
- interior/exterior context;
- doors/windows openness hooks.

Prototype layered engine audio:

- intake;
- exhaust;
- mechanical;
- starter;
- transmission/driveline;
- body vibration/rattle hooks.

Do not solve engine sound with one clip and pitch alone.

Unknown audio design values must remain tunable.

## World and ambience

Prototype:

- forest ambience;
- lake/shore ambience;
- wind;
- rain;
- interior room tone;
- garage/workshop ambience;
- distant traffic/future hook;
- insects/birds as design allows;
- day/night state;
- weather state;
- streaming-safe emitters.

Create zone/portal hooks for:

- interior/exterior transition;
- obstruction/occlusion;
- reverb/aux sends;
- weather sheltering.

Do not create per-frame raycasts for every emitter without a budgeted system.

## Interaction and mechanical audio

Create event mappings for:

- pickup/drop;
- metal/wood/plastic impacts;
- tools;
- bolt insert/tighten/loosen;
- part install/remove;
- doors/gates/windows;
- footsteps by surface;
- UI navigation;
- save/load feedback hooks.

Use surface/material metadata rather than object-name checks.

## Donor audio policy

Donor clips may be:

- inventoried;
- auditioned privately;
- used as behavioral/timing reference;
- locally imported when permitted by project policy and recorded in the
  porting ledger.

Do not commit donor audio contrary to repository policy.

Do not make the architecture depend on a specific donor clip.

## Banks and generated content

Document:

- Wwise project path;
- work-unit policy;
- event naming;
- RTPC naming;
- switch/state naming;
- SoundBank naming;
- generated-bank path;
- source-control policy;
- build integration;
- missing-bank behavior.

Do not commit caches.

Do not commit generated banks unless project policy explicitly says so.

## Debug and validation

Create tools under:

`Tools → MSC Remake → Audio`

Required:

- show active backend;
- validate event IDs;
- validate parameter IDs;
- validate missing banks;
- inspect current vehicle parameters;
- audition mapped events where available;
- show active emitters;
- show listener context;
- show zone/portal state;
- report fallback usage;
- export audio validation report.

## Settings integration

Expose configuration for future UI:

- master;
- vehicle;
- effects;
- ambience;
- music;
- UI;
- dynamic range;
- output device hook if supported;
- subtitles/captions hooks;
- mute-on-focus-loss;
- accessibility options.

Do not build final settings UI in this milestone.

## Tests

Add tests for:

- event/parameter ID validation;
- backend selection;
- missing-Wwise fallback;
- parameter clamping;
- vehicle telemetry mapping;
- surface mapping;
- interior/exterior context;
- weather parameter mapping;
- emitter lifecycle;
- no duplicate registration;
- missing-bank error reporting.

Add PlayMode smoke tests for:

- Unity fallback;
- Wwise backend when installed;
- vehicle event lifecycle;
- scene/cell unload cleanup;
- weather transition;
- interior/exterior transition.

## Performance

Measure:

- active voices;
- emitter count;
- update cost;
- occlusion budget;
- memory/bank footprint;
- vehicle parameter update cost;
- streaming/unload cleanup.

## Documentation

Create or update:

- `Docs/Audio/AUDIO_ARCHITECTURE.md`;
- `Docs/Audio/WWISE_SETUP.md`;
- `Docs/Audio/NAMING_AND_BANK_POLICY.md`;
- `Docs/Audio/VEHICLE_AUDIO_MODEL.md`;
- `Docs/Audio/WORLD_AUDIO_ZONES.md`;
- `Docs/Audio/AUDIO_EVENT_MATRIX.csv`;
- `Docs/Audio/AUDIO_PARAMETER_MATRIX.csv`;
- `Docs/Audio/PERFORMANCE_REPORT.md`;
- `Docs/Milestones/MILESTONE_08_REPORT.md`.

## Definition of done

1. Gameplay depends on project-owned audio contracts.
2. Unity fallback works.
3. Official Wwise adapter exists only when official types exist.
4. Vehicle/weather/world parameters are mapped.
5. Prototype audio events work or exact external setup blockers are documented.
6. Streaming cleanup works.
7. Validation and tests exist.
8. Generated-content policy is documented.
9. No fake Wwise implementation exists.

## Final response

Report:

1. Wwise availability/version.
2. Backend architecture.
3. Vehicle audio.
4. World/interaction audio.
5. Banks/setup.
6. Tests.
7. Performance.
8. Files changed.
9. Manual Wwise actions.
10. Blockers.
11. Readiness for UI milestone.

Stop after Milestone 08.
