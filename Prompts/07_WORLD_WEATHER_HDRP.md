/plan

# MILESTONE 07 — TIME, WEATHER, AND HDRP WORLD PRESENTATION

Read `AGENTS.md` completely before doing anything.

Read:

- all milestone reports through 06A;
- `Docs/WORLD_WEATHER.md`;
- `Docs/ART_GUIDE.md`;
- world-remaster material and lighting standards;
- performance budget;
- save architecture;
- audio-backend contracts;
- reference-capture time/weather data;
- current Git status and diff.

## Objective

Implement a data-driven time and weather system with HDRP presentation suitable
for the vertical slice.

Required states:

- clear;
- partly cloudy or transitional;
- overcast;
- rain;
- fog or mist where supported;
- day/night progression;
- wetness accumulation and drying;
- wind response;
- weather output parameters for audio, UI, save, vegetation, roads, and vehicle
  simulation.

Keep simulation state separate from HDRP presentation.

## Architecture

Create or align:

- `GameTimeService`;
- `GameTimeState`;
- `GameTimeConfig`;
- `WeatherService`;
- `WeatherState`;
- `WeatherPreset`;
- `WeatherTransition`;
- `WeatherSchedule`;
- `WeatherRandomSource`;
- `WeatherPresentationController`;
- `HdrpSkyPresenter`;
- `HdrpFogPresenter`;
- `HdrpCloudPresenter`;
- `RainPresenter`;
- `WindPresenter`;
- `GlobalWetnessController`;
- `PuddlePresenter`;
- `WeatherEnvironmentOutputs`;
- versioned save DTOs.

Do not make every material query the weather service directly.

Expose stable global/environment parameters through a narrow boundary.

## Time system

Support:

- configurable day length;
- time scale;
- pause handling;
- deterministic configured progression;
- calendar/day index needed by the current slice;
- sunrise/sunset reference;
- time events without event-subscription leaks;
- save/load;
- debug time controls;
- no direct dependence on wall-clock time.

## Weather simulation

Support:

- current state;
- target state;
- transition progress;
- intensity parameters;
- optional seeded scheduling;
- scripted override for tests;
- transition validation;
- save/load;
- clear separation between logical weather and visual availability.

Unknown donor behavior must be labeled as remake tuning.

## HDRP presentation

Configure or drive, according to the installed HDRP version:

- sky;
- sun direction and intensity;
- ambient contribution;
- volumetric clouds when appropriate;
- fog;
- exposure;
- reflection strategy;
- global volume parameters;
- rain particles/VFX;
- wind;
- wetness;
- puddle prototype;
- lightning only if explicitly required by documented design.

Do not require ray tracing.

Do not replace stable project settings blindly.

Use restrained post-processing.

Avoid:

- excessive bloom;
- gameplay depth of field;
- constant chromatic aberration;
- crushed blacks that hide interactions;
- cinematic grading that breaks overcast/rain readability.

## Wetness interface

Create a coherent material contract for:

- terrain;
- roads;
- buildings;
- props;
- vehicles;
- vegetation where relevant.

Parameters may include:

- global wetness;
- rain intensity;
- drying rate;
- puddle amount;
- ripple intensity;
- surface exposure;
- interior exclusion.

Use existing material standards.

Do not create unique material instances for every object.

## Puddles and rain

Implement a bounded prototype that supports:

- accumulation inputs;
- drying;
- road/terrain masks;
- interior exclusion;
- representative reflections;
- rain collision/shelter strategy;
- scalable quality settings.

Do not attempt physically exact hydrology.

## Wind and vegetation

Drive approved vegetation systems through shared wind parameters.

Validate:

- no violent unrealistic motion;
- no mismatched tree/grass wind;
- no per-object update explosion;
- quality scaling;
- rain compatibility.

## World and vehicle integration

Expose outputs for:

- road wetness/friction hooks;
- tire/surface audio;
- windshield/rain future hooks;
- engine temperature/cooling future hooks;
- headlights/visibility;
- interior/exterior audio;
- UI time/weather display;
- save state.

Do not implement final vehicle wet-physics tuning here.

## Debug and authoring tools

Create tools under:

`Tools → MSC Remake → Time and Weather`

Required:

- set time;
- pause/advance time;
- select preset;
- blend to preset;
- set intensity;
- freeze random scheduling;
- show current logical and visual state;
- validate preset ranges;
- capture reference conditions;
- show missing material integration;
- run performance capture.

## Settings and scalability

Support configurable quality tiers for:

- volumetric clouds;
- fog;
- rain particles;
- puddles/reflections;
- vegetation wind;
- shadow distance;
- weather VFX distance.

Integrate with future settings UI through configuration/service APIs, not direct
menu dependencies.

## Tests

Add EditMode tests for:

- time progression;
- pause/time scale;
- day wrap;
- weather interpolation;
- transition cancellation/replacement;
- seeded scheduling;
- preset validation;
- wetness accumulation/drying;
- save DTO round trip;
- environment output mapping.

Add PlayMode tests where practical for:

- bootstrap with clear weather;
- transition to rain;
- save/load during transition;
- interior wetness exclusion;
- no missing HDRP references;
- no invalid numeric values;
- performance smoke test.

## Performance

Measure representative:

- clear day;
- overcast;
- rain;
- fog;
- dusk/night;
- dense vegetation;
- driving route.

Record CPU/GPU impact and memory.

Do not claim performance targets without actual measurements.

## Documentation

Create or update:

- `Docs/Weather/TIME_ARCHITECTURE.md`;
- `Docs/Weather/WEATHER_ARCHITECTURE.md`;
- `Docs/Weather/HDRP_PRESENTATION.md`;
- `Docs/Weather/WETNESS_MATERIAL_CONTRACT.md`;
- `Docs/Weather/QUALITY_TIERS.md`;
- `Docs/Weather/REFERENCE_COMPARISON.md`;
- `Docs/Weather/PERFORMANCE_REPORT.md`;
- `Docs/Milestones/MILESTONE_07_REPORT.md`.

## Non-goals

Do not implement:

- final Wwise content;
- final UI screens;
- full seasonal system;
- winter/snow;
- advanced hydrology;
- final windshield effects;
- unrelated world remodeling.

## Definition of done

1. Logical time/weather works.
2. HDRP presentation follows logical state.
3. clear/overcast/rain transitions work.
4. Wetness and puddle prototype works.
5. World systems receive stable outputs.
6. Save DTOs exist.
7. Quality tiers exist.
8. Tests and measurements exist.
9. The project remains readable and playable.

## Final response

Report:

1. Time architecture.
2. Weather architecture.
3. HDRP systems used.
4. Material/wetness integration.
5. Quality tiers.
6. Tests.
7. Performance.
8. Files changed.
9. Manual validation.
10. Known limitations.
11. Readiness for Milestone 08.

Stop after Milestone 07.
