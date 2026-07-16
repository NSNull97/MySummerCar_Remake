/plan

# MILESTONE 07A — ENVIRO 3 PREFLIGHT, VENDOR BOUNDARY, AND WEATHERLAB

Read `AGENTS.md` completely before doing anything.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Milestones/MILESTONE_06A_REPORT.md`;
- `Docs/Milestones/MILESTONE_06B3_REPORT.md`;
- `Docs/WorldBaseline/BASELINE_REVISION.json`;
- `Docs/WorldBaseline/WEATHER_HANDOFF.md`;
- all current HDRP, sky, cloud, fog, exposure, reflection, water, vegetation,
  graphics, quality, bootstrap, scene-lifecycle, and assembly documentation;
- package manifests, scripting defines, Graphics/Quality settings, Build
  Settings, Custom Post Process lists, Volumes, scenes, prefabs, and Git diff;
- all documentation and examples included with the exact locally installed
  Enviro 3 package.

## Entry gate

Do not continue if:

- Milestone 06A is not closed;
- Milestone 06B3 has not frozen a validated active donor runtime baseline;
- the inaccurate custom visual roots are still active in the feature-parity world profile;
- the project does not compile before Enviro work;
- there is an unreviewed broad Git diff unrelated to this milestone.

WeatherLab must remain independent from the full donor baseline and must not trigger world remastering.

## Objective

Safely establish the exact local Enviro 3 integration boundary and build a
bounded, non-shipping WeatherLab that proves the package works with this Unity
and HDRP project before any project-owned weather simulation or production-world
rollout is implemented.

This milestone is:

- dependency audit;
- exact-version/API discovery;
- environment-owner conflict resolution;
- adapter boundary preparation;
- laboratory scene and smoke validation.

It is **not** the full weather/time/wetness/lightning implementation.

## Architecture decision — authoritative

```text
Project-owned environment contracts
                ↓
Project-owned Enviro integration assembly
                ↓
Exact installed Enviro 3 public API
                ↓
Enviro visual modules
```

Enviro is a presentation backend.

Project-owned code will later own:

- logical time/calendar;
- deterministic weather schedule/transitions;
- logical cloud/rain/fog/wind outputs;
- wetness accumulation/drying;
- gameplay lightning;
- audio events;
- UI state;
- saves;
- vehicle/road/water/NPC integration.

## Vendor rules

Mandatory:

1. Do not install, download, update, move, rename, reformat, or patch Enviro
   vendor files.
2. Do not modify Enviro shaders/source to make the preflight pass.
3. Do not copy Enviro source into project-owned assemblies.
4. Do not create fake/stub Enviro types when the package is absent.
5. Use only public API evidenced by the exact local package and its included
   docs/source signatures.
6. Do not rely on old forum snippets when local evidence differs.
7. Do not import or activate Azure Sky in production scenes.
8. Do not put Enviro demo/sample scenes into shipping Build Settings.
9. Do not make the project require the optional Enviro Terrain Shader add-on.
10. Record vendor file change count before and after; expected result is zero.

## Package absence stop condition

If Enviro 3 is not installed:

- do not replace it with a custom HDRP weather renderer;
- do not install it automatically;
- create `Docs/Weather/ENVIRO3_INSTALLATION_BLOCKER.md` with exact evidence;
- point the user to `ENVIRO3_MANUAL_SETUP_RU.md`;
- stop.

## Exact installation audit

Discover and record:

- vendor root path;
- exact version evidence and confidence;
- Unity compatibility metadata;
- runtime/editor asmdefs;
- namespaces and manager/instance types;
- HDRP-specific integration files;
- public time/date API;
- public weather/preset/transition API;
- public sky, clouds, fog, effects, precipitation, wind, lightning, reflection,
  wetness/snow, zone and event APIs that actually exist;
- included docs and code examples;
- demo/sample scenes;
- scripting define symbols;
- Custom Post Process registrations;
- Graphics/Quality/Volume/project-setting modifications;
- compile warnings/errors;
- dependencies and optional modules;
- any existing project code that already references Enviro.

Create:

- `Docs/Weather/ENVIRO3_LOCAL_INSTALLATION_AUDIT.md`;
- `Docs/Weather/ENVIRO3_PUBLIC_API_EVIDENCE.md`;
- `Docs/Weather/ENVIRO3_FILE_AND_ASSEMBLY_MAP.md`;
- `Docs/Weather/ENVIRO3_VERSION_EVIDENCE.md`.

Do not state an exact version without local evidence.

## Environment-owner conflict audit

Inspect all project scenes and bootstrap paths for competing owners of:

- sky;
- sun/moon lights;
- volumetric clouds;
- fog;
- exposure;
- ambient/environment lighting;
- reflections;
- rain/effects;
- wind;
- lightning visuals;
- weather audio;
- time progression.

Classify each as:

- `ProjectTemporaryOwner`;
- `HDRPNativeOwner`;
- `EnviroOwner`;
- `DuplicateConflict`;
- `Unknown`.

Choose and document **one owner per presentation domain** for the WeatherLab.

Special care:

- fog ownership must be explicit, especially if HDRP water or custom transparent
  shaders exist;
- do not run Enviro volumetric clouds and HDRP volumetric clouds together;
- do not keep duplicate directional sun/moon lights;
- do not allow Enviro autonomous time/weather scheduling to free-run;
- Enviro weather/ambient audio must be disabled for production architecture.

Do not delete old temporary owners blindly. Disable them in the bounded lab or
create a reversible migration plan.

Create:

`Docs/Weather/ENVIRONMENT_OWNER_MATRIX.csv`

## Project-owned contract assembly

Create or align a vendor-neutral presentation contract assembly that does not
reference Enviro.

Minimum contracts:

- `EnvironmentPresentationFrame`;
- `EnvironmentPresentationCapabilities`;
- `EnvironmentQualityTier`;
- `IEnvironmentPresentationAdapter`;
- `EnvironmentPresentationStatus`;
- `EnvironmentPresentationDiagnostic`;
- stable binding IDs/config DTOs.

The contract must express at least:

- logical time/date inputs;
- sun/moon direction or normalized time input;
- cloud coverage/type/intensity targets;
- precipitation intensity/type;
- fog/mist target;
- wind direction/intensity/gust target;
- lightning visual request;
- reflection/environment refresh request;
- quality tier;
- transition duration;
- enabled/disabled state.

It must not expose Enviro types, preset objects, scene instance IDs, array
indices, or vendor asset references.

## Dedicated Enviro integration assembly

Create a narrow project-owned integration assembly, for example:

```text
MSC.Remake.Enviro3Integration
```

Only this assembly may directly reference Enviro types.

Implement a bounded adapter using the exact local public API:

- `Enviro3EnvironmentAdapter` or project-equivalent;
- capability discovery;
- binding validation;
- lifecycle attach/detach;
- safe missing-module reporting;
- no silent fallback to duplicate HDRP systems;
- no per-frame reflection refresh;
- no unchanged-value spam every frame;
- no direct gameplay logic.

Do not create the full weather scheduler here.

## WeatherLab scene

Create a separate non-shipping scene, for example:

```text
Assets/MSCRemake/Scenes/Dev/WeatherLab.unity
```

It must include a small controlled test environment:

- terrain patch;
- asphalt road;
- gravel/dirt patch;
- shallow depression suitable for puddle visualization later;
- simple building with exterior and interior room;
- doorway/window transition;
- representative opaque and transparent materials;
- a small water/shore proxy or existing approved water test object;
- nearby vegetation using the project's approved vegetation path;
- stationary vehicle/camera proxy;
- exterior camera;
- interior-looking-out camera;
- vehicle-interior-looking-out camera;
- deterministic capture anchors;
- performance markers.

Do not copy the full donor runtime baseline or inactive prototype cells into WeatherLab.

Do not include WeatherLab in shipping Build Settings.

## Preflight presentation smoke states

Without implementing scheduling, prove the adapter can explicitly present:

- fixed neutral clear day;
- fixed overcast;
- fixed rain;
- fixed storm visual state;
- fixed night;
- chosen fog/mist strategy;
- two quality tiers minimum.

These are manual/development commands, not autonomous gameplay weather.

Use stable project binding IDs. Do not search Enviro presets by display name at
runtime.

## Fog and water compatibility decision

Test and document:

- Enviro fog with current HDRP/transparency/water setup;
- HDRP-native fog alternative when required;
- underwater/through-water behavior if a water system already exists;
- interior fog leakage;
- transparent windows;
- headlights/lights through fog;
- additive scene lifecycle.

Choose one bounded strategy for Milestone 07B/07C:

- Enviro fog owner;
- HDRP-native fog owner driven by the project/adapter;
- another already-approved single owner.

Do not patch vendor water/fog shaders in this milestone.

## Audio policy

- Disable Enviro weather/ambient audio in production configuration.
- WeatherLab may use temporary silent mode only.
- Do not author final Wwise events here.
- Record how future project-owned weather events will reach Wwise.

## Lightning policy

- Enviro may render ambient lightning or a requested visual effect where its
  public API supports it.
- No Enviro visual callback may directly damage player/NPC/world objects.
- Gameplay strikes will be project-owned in 07B.
- WeatherLab only proves visual request capability and cleanup.

## Editor tooling

Create or extend:

`Tools → MSC Remake → Enviro 3 Preflight`

Required actions:

- show local version evidence;
- validate vendor boundary;
- list project assemblies referencing Enviro;
- scan duplicate environment owners;
- open WeatherLab;
- validate bindings;
- select clear/overcast/rain/storm/night smoke state;
- toggle quality tier;
- trigger ambient lightning visual;
- show current capability matrix;
- export diagnostics;
- verify WeatherLab is excluded from shipping scenes;
- show vendor file changes.

## Tests

### Vendor-neutral EditMode tests

- presentation-frame validation;
- capability flags;
- stable binding ID validation;
- no Enviro references in core contract assembly.

### Integration tests when Enviro is present

- adapter resolves exact local manager/modules;
- missing module reports a bounded error;
- clear/overcast/rain/storm mapping smoke test;
- time sync command does not free-run;
- no duplicate manager after scene reload;
- no duplicate sky/cloud/fog owner in WeatherLab;
- Enviro audio disabled in production config;
- lifecycle cleanup;
- WeatherLab excluded from shipping build;
- vendor source change count zero.

### Manual validation

Capture:

- clear exterior;
- overcast exterior;
- rain exterior;
- rain from interior;
- rain from vehicle interior;
- night/headlights;
- fog near water proxy;
- storm/lightning visual;
- low/high quality comparison.

Do not claim a capture that was not produced.

## Performance

Measure WeatherLab in a development build where possible:

- Enviro disabled diagnostic baseline;
- clear;
- overcast;
- rain;
- storm;
- fog;
- night;
- low and high quality.

Record CPU/GPU frame time, render/main thread, memory, allocations, clouds, fog,
precipitation, reflections, shadows, vegetation wind, and adapter overhead.

Do not tune production world or claim final targets here.

## Documentation

Create/update:

- `Docs/Weather/ENVIRO3_LOCAL_INSTALLATION_AUDIT.md`;
- `Docs/Weather/ENVIRO3_PUBLIC_API_EVIDENCE.md`;
- `Docs/Weather/ENVIRONMENT_OWNER_MATRIX.csv`;
- `Docs/Weather/ENVIRO3_ADAPTER_BOUNDARY.md`;
- `Docs/Weather/FOG_AND_WATER_COMPATIBILITY.md`;
- `Docs/Weather/WEATHERLAB_SETUP.md`;
- `Docs/Weather/WEATHERLAB_CAPTURE_INDEX.csv`;
- `Docs/Weather/WEATHERLAB_PERFORMANCE.md`;
- `Docs/Weather/KNOWN_ENVIRO_LIMITATIONS.md`;
- `Docs/Milestones/MILESTONE_07A_REPORT.md`.

## Non-goals

Do not implement:

- autonomous game-time progression;
- deterministic weather fronts;
- global wetness accumulation;
- gameplay lightning damage;
- final puddles;
- production-scene rollout;
- Wwise content;
- final UI;
- seasons/winter/snow;
- full water/swimming/buoyancy;
- final windshield effects;
- vendor patches;
- unrelated world remastering.

## Definition of done

1. Exact local Enviro installation and API evidence are documented.
2. Vendor source change count is zero.
3. Core contracts contain no Enviro types.
4. One narrow integration assembly exists.
5. Duplicate environment owners are identified/resolved in WeatherLab.
6. WeatherLab presents clear/overcast/rain/storm/night/fog smoke states.
7. Fog/water strategy is chosen without vendor patching.
8. Enviro audio production policy is explicit.
9. WeatherLab is excluded from shipping.
10. Tests/captures/performance actually run are reported honestly.
11. The project compiles and remains ready for 07B.

## Final response

Report:

1. Enviro presence/version evidence.
2. Local API/modules discovered.
3. Vendor files changed.
4. Environment-owner conflicts.
5. Adapter/assembly boundary.
6. WeatherLab contents.
7. Smoke states validated.
8. Fog/water strategy.
9. Audio/lightning policy.
10. Tests and captures.
11. Performance.
12. Manual steps/blockers.
13. Readiness for `07B_TIME_WEATHER_DOMAIN_AND_ENVIRO3_ADAPTER.md`.

Stop after Milestone 07A.
