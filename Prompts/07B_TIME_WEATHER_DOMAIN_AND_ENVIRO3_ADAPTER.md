/plan

# MILESTONE 07B — PROJECT-OWNED TIME/WEATHER DOMAIN AND ENVIRO 3 ADAPTER

Read `AGENTS.md` completely before doing anything.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Milestones/MILESTONE_07A_REPORT.md`;
- all 07A Enviro installation/API, owner, fog/water, WeatherLab, capture and
  performance reports;
- reference-capture time/weather data;
- current save, audio, road/surface, vehicle, vegetation, water, UI, bootstrap,
  test and performance contracts;
- current Git status and diff.

## Entry gate

Stop if:

- Enviro 3 is absent or the exact local API is unresolved;
- vendor source was modified;
- the WeatherLab does not compile/run;
- duplicate environment ownership remains unresolved in the lab;
- Milestone 07A is not closed;
- the project has unrelated unreviewed changes.

Do not replace Enviro with a custom weather renderer.

## Objective

Implement the deterministic, project-owned time and weather domain, wetness
state, gameplay-lightning selection, stable cross-system outputs, and the
validated Enviro 3 presentation adapter **inside WeatherLab and tests only**.

This milestone does not roll weather into production world cells. That is 07C.

## Ownership model

Mandatory:

```text
GameTimeService
        ↓
WeatherDirector
        ↓
WeatherEnvironmentOutputs
        ↓
Enviro3EnvironmentAdapter
        ↓
Enviro 3 presentation
```

Parallel project-owned consumers:

```text
GlobalWetnessController
LightningStrikeDirector
Road/Vehicle hooks
Vegetation/Wind hooks
Water hooks
Audio event contract
UI summary contract
Save DTOs
```

Forbidden:

- gameplay checks against Enviro preset names;
- Enviro autonomous schedule/time competing with project services;
- direct Enviro references outside the integration assembly;
- saving Enviro asset references, indices, object IDs or runtime objects;
- Enviro weather audio as production authority;
- vendor modifications;
- optional Enviro Terrain Shader dependency;
- Unity global random state for schedule generation.

## Time domain

Create or align:

- `IGameTimeService`;
- `GameTimeService`;
- `GameTimeConfig`;
- `GameTimeState`;
- `GameDate`;
- `GameTimeSnapshot`;
- `GameTimeEvent`;
- versioned time DTO.

Support:

- configurable day length;
- time scale;
- pause;
- day index and required calendar data;
- deterministic progression independent of wall clock;
- exact save/restore;
- sunrise/sunset reference inputs;
- safe event scheduling;
- no subscription leaks;
- no per-frame date/string allocations in hot paths;
- DEV set/advance/freeze controls.

The project time service pushes time to Enviro through the adapter. Enviro must
not free-run and feed time back into gameplay.

## Weather domain

Create or align:

- `IWeatherService`;
- `WeatherDirector`;
- `WeatherState`;
- `WeatherStateId`;
- `WeatherProfile`;
- `WeatherFront`;
- `WeatherTransition`;
- `WeatherTimeline`;
- `WeatherSchedule`;
- deterministic `WeatherSeed`/RNG state;
- `WeatherOverride` with owner/reason/priority;
- `WeatherSnapshot`;
- `WeatherEnvironmentOutputs`;
- versioned weather DTO.

Required vertical-slice states:

- clear;
- mostly clear/partly cloudy;
- overcast;
- light drizzle;
- steady rain;
- heavy rain;
- thunderstorm;
- morning mist/fog using the 07A-approved owner strategy.

Do not create snow, winter, full seasons or geographically separate storms.

## Deterministic weather fronts

Do not implement independent random preset switches.

Use seeded front/timeline continuity, for example:

```text
clear
→ increasing clouds
→ overcast
→ drizzle/rain
→ frontal passage
→ cloud breakup
→ drying/morning mist depending on logical state
```

Each profile/front defines project-owned logical outputs:

- cloud coverage/type target;
- precipitation type/intensity;
- fog/mist target;
- wind direction/intensity/gustiness;
- visibility;
- ambient/readability target;
- lightning probability/intensity;
- wetness input;
- drying modifier;
- duration range;
- transition duration range;
- allowed predecessors/successors;
- stable Enviro presentation binding ID.

Given the same versioned state and seed, progression must be reproducible within
documented deterministic limits.

Unknown donor values are labeled `RemakeDesignTarget`, not presented as measured.

## Overrides

Support scoped overrides for:

- DEV tools;
- automated tests;
- reference capture;
- future scripted events.

Each override records:

- owner;
- reason;
- priority;
- start/end/lifetime;
- serialization policy;
- requested logical outputs.

Removing an override must reveal the underlying schedule without destroying it.

## Stable environment outputs

Create one project-owned snapshot/contract consumed by other systems.

At minimum expose:

- normalized time/day information;
- cloud coverage;
- precipitation type/intensity;
- fog/mist intensity and visibility;
- wind direction, speed, gust;
- temperature placeholder/output if already needed;
- ground/road/puddle/vegetation wetness;
- lightning risk/state;
- exterior/interior exposure context hook;
- audio weather event/intensity hooks;
- UI weather/time summary;
- quality/presentation status.

No consumer should query Enviro directly.

## Enviro presentation binding

Use the exact adapter/public APIs validated in 07A.

Requirements:

- stable binding IDs, not runtime display-name searches;
- explicit capability matrix;
- time sync from project state;
- logical profile → Enviro presentation mapping;
- transition mapping;
- approved fog owner strategy;
- quality-tier mapping;
- ambient lightning visual request;
- reflection/environment refresh request with thresholds/cadence;
- dirty flags or bounded update cadence;
- no unchanged-value writes every frame;
- clear diagnostics for missing modules/bindings;
- no vendor patches.

If Enviro cannot express an output exactly, document a bounded approximation in
project-owned binding data.

## Global wetness domain

Create or align:

- `GlobalWetnessController`;
- `WetnessConfig`;
- `WetnessState`;
- `WetnessEnvironmentInputs`;
- `WetnessEnvironmentOutputs`;
- `SurfaceExposureProfile`;
- `ShelterVolume` or existing exposure abstraction;
- `WetnessShaderBridge` contract;
- versioned wetness DTO.

At minimum model:

- `GroundWetness`;
- `RoadWetness`;
- `PuddleAmount`;
- `VegetationWetness` or future-compatible output;
- accumulation from precipitation;
- drying from time/wind/temperature/sun exposure;
- shelter/interior exclusion;
- deterministic save/restore.

This is project-owned gameplay state.

Enviro may receive or reflect visual wetness values, but its runtime objects are
not saved as authority.

Do not implement physically exact hydrology.

## Material wetness contract prototype

In WeatherLab only, prove project-owned global shader/material inputs can drive:

- asphalt;
- gravel/dirt;
- building exterior;
- vehicle paint/material proxy;
- vegetation proxy;
- bounded puddle/ripple prototype.

Prefer shared global parameters/property blocks/material systems. Do not create
unique runtime material instances for every object.

Do not require Enviro Terrain Shader.

## Gameplay lightning domain

Create or align:

- `LightningStrikeDirector`;
- `LightningStrikeConfig`;
- `LightningStrikeCandidate`;
- `LightningAttractor`;
- `LightningProtectionVolume`;
- `LightningStrikeEvent`;
- `LightningPresentationRequest`;
- `ThunderAudioRequest`;
- cooldown/fairness state;
- future-compatible damage/electrical/NPC hooks.

Separate:

### Ambient visual lightning

- atmosphere only;
- no gameplay damage;
- may use Enviro visual capability.

### Gameplay strike

- project-owned point selection;
- weighted by exposure, height, attractors and protection;
- never simply aims at the player;
- uses deterministic RNG/state;
- emits presentation request;
- emits delayed thunder request based on listener distance;
- emits gameplay effect hook;
- applies spawn/load fairness cooldown;
- supports non-lethal DEV mode.

If no existing player health/death contract exists, do not invent a parallel
health system. Implement the event/hook and validate non-lethal behavior.

Do not implement forest fires, full power-grid damage or broad destruction.

## Save DTO contracts

Create versioned project-owned DTOs for:

- game time/date/day index;
- weather state/profile/front IDs;
- current/target transition and progress;
- deterministic seed/RNG state;
- timeline cursor;
- serializable override state;
- wetness/puddle state;
- lightning cooldown/fairness state;
- selected binding/config IDs needed for reconstruction.

Never serialize:

- Enviro objects/assets;
- vendor array indices;
- scene instance IDs;
- current VFX frame;
- display-name lookups.

07B only creates and tests the domain DTOs. Final file persistence belongs to
Milestone 09.

## DEV tooling

Create or extend:

`Tools → MSC Remake → Time and Weather`

Required:

- set time/date/day;
- pause/advance/time scale;
- select logical weather profile;
- transition over duration;
- freeze/unfreeze schedule;
- set/read seed;
- inspect timeline/front cursor;
- add/remove override;
- set wetness/puddles;
- show shelter/exposure;
- validate Enviro bindings;
- compare logical vs presented state;
- trigger ambient lightning;
- trigger gameplay strike at cursor/target/nearest attractor;
- non-lethal lightning mode;
- show candidate weights/protection/cooldowns;
- export diagnostics;
- capture WeatherLab states.

When an existing DEV command registry exists, add bounded hooks:

```text
time.set 04:30
time.scale 20
weather.set clear
weather.set rain
weather.transition overcast 300
weather.freeze on
weather.seed 123
weather.wetness 1
weather.puddles 0.5
lightning.ambient cursor
lightning.strike cursor
lightning.damage off
```

Do not build a duplicate console architecture.

## Tests

### Core EditMode tests — no Enviro dependency

- time progression/pause/scale/day wrap;
- deterministic schedule for known seed;
- valid/invalid transitions;
- front interpolation;
- override priority/lifecycle;
- wetness accumulation;
- drying monotonicity under controlled inputs;
- shelter/exposure mapping;
- lightning candidate weighting;
- protection/cooldown/fairness;
- thunder delay calculation;
- DTO round trips;
- environment output validation;
- invalid numeric/config rejection;
- no Enviro references in core assemblies.

### Enviro integration tests

- binding resolution;
- time sync;
- clear/overcast/rain/storm/mist mapping;
- no autonomous drift;
- no duplicate manager/owner;
- Enviro audio disabled in production config;
- adapter lifecycle;
- quality mapping;
- missing binding diagnostics;
- vendor file changes zero.

### WeatherLab PlayMode/manual

- clear → cloud → rain transition;
- rain → drying;
- mist/fog strategy;
- interior/exterior precipitation behavior;
- ambient vs gameplay lightning separation;
- no immediate strike after state restore;
- wetness material prototype;
- low/high quality;
- no invalid state or visible clear-sky flash during in-memory restore.

## Performance

Measure WeatherLab:

- domain simulation without Enviro presentation;
- Enviro clear;
- overcast;
- rain;
- heavy rain;
- storm/lightning;
- fog/mist;
- night;
- wetness/puddles;
- low/high quality.

Record CPU/GPU frame time, main/render thread, allocations, memory, clouds, fog,
precipitation, reflections, adapter update cost, domain update cost, wetness and
lightning cost.

## Documentation

Create/update:

- `Docs/Weather/TIME_ARCHITECTURE.md`;
- `Docs/Weather/WEATHER_ARCHITECTURE.md`;
- `Docs/Weather/WEATHER_FRONT_AND_SEED_MODEL.md`;
- `Docs/Weather/WEATHER_ENVIRONMENT_OUTPUTS.md`;
- `Docs/Weather/ENVIRO3_PRESENTATION_BINDINGS.md`;
- `Docs/Weather/ENVIRO3_BINDING_MATRIX.csv`;
- `Docs/Weather/WETNESS_ARCHITECTURE.md`;
- `Docs/Weather/WETNESS_MATERIAL_CONTRACT.md`;
- `Docs/Weather/LIGHTNING_ARCHITECTURE.md`;
- `Docs/Weather/WEATHER_SAVE_DTO_SCHEMA.md`;
- `Docs/Weather/WEATHERLAB_07B_VALIDATION.md`;
- `Docs/Weather/WEATHERLAB_07B_PERFORMANCE.md`;
- `Docs/Milestones/MILESTONE_07B_REPORT.md`.

## Non-goals

Do not implement:

- production world rollout;
- final Wwise content;
- final UI;
- seasons/winter/snow;
- geographically independent weather fronts;
- full water/swimming/buoyancy;
- final windshield/wipers;
- final wet-road tire calibration;
- forest fires;
- power-grid simulation;
- broad NPC weather behavior;
- unrelated world remodeling;
- vendor patches.

## Definition of done

1. Project time/calendar is authoritative and deterministic.
2. Weather fronts/timeline are deterministic and testable.
3. Stable cross-system outputs exist.
4. Enviro follows project state through one adapter.
5. Core assemblies do not reference Enviro.
6. Wetness accumulates/dries and round-trips through DTOs.
7. Ambient and gameplay lightning are separated and fair.
8. WeatherLab tests/captures/performance are honest.
9. Vendor source change count is zero.
10. The project compiles and is ready for production rollout in 07C.

## Final response

Report:

1. Time architecture.
2. Weather-front/seed model.
3. Stable environment outputs.
4. Exact Enviro mapping and installed version.
5. Wetness/material prototype.
6. Lightning architecture and damage-hook status.
7. Save DTOs.
8. DEV tools.
9. Tests/captures.
10. Performance.
11. Files changed.
12. Vendor files changed.
13. Known limitations.
14. Readiness for `07C_PRODUCTION_WEATHER_ROLLOUT_AND_VALIDATION.md`.

Stop after Milestone 07B. Do not modify production world cells yet.
