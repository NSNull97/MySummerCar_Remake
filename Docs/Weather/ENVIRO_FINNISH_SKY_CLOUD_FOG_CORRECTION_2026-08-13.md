# Enviro Finnish sky, cloud, and fog correction — 2026-08-13

## Trigger and ownership

The correction responds to current-build captures showing a warm/purple horizon
throughout the night, unreadable stars and moon, more cloud in Clear than in
Overcast, sandy/bright daylight, a Dense Fog state that remained too open, and
full-strength sun/hard shadows surviving under closed cloud cover. A later
streaming capture also exposed a destroyed `LegacyWeatherZoneAdapter` retained
by a persistent door portal.

The production Bootstrap remains on the approved `EnviroLegacy` hybrid route.
Enviro owns sky, celestial objects, stars, clouds, and precipitation
presentation. The project clock/calendar, Finnish climate state, exposure/fog
bridge, wetness, lightning gameplay, and saves remain authoritative. No Enviro
vendor source asset or script is modified.

## Runtime-only calibration

- Sky gradients are cloned and calibrated at startup. Deep-night keys are
  rebuilt from a cool luminance-preserving palette, so a moonless midnight no
  longer inherits the pink/orange dawn key while the sky keeps a dark blue
  Finnish summer-night floor.
- The cloned star curve keeps a visible night floor and fades continuously into
  daylight instead of relying on the too-weak vendor curve.
- Realistic Enviro moon orbit and phase remain enabled. Runtime moon scale has a
  `9.5` minimum. The existing single directional light now follows the moon only
  at night and produces at most `8 lux` at full phase, high altitude and clear
  sky. This is an explicit game-readability calibration for the accepted
  `7.25 EV` night camera, not a physical moon-lux claim. Illumination and its
  restrained `0.28` maximum shadow strength fade with altitude, lunar phase and
  cloud occlusion; both become exactly zero below the horizon or at new moon.
- Clear, Partly Cloudy, Bright Overcast, Heavy Overcast, Fog, Drizzle, Rain,
  Heavy Rain, and Storm use isolated runtime weather clones. Partly Cloudy is a
  scattered, eroded volumetric field instead of a few horizon remnants. Bright
  Overcast is a light broken deck with minimal cirrus veil; Heavy Overcast keeps
  the closed, more absorbent target. Clear cirrus is reduced to `0.015` alpha.
- Every discrete weather binding receives a deterministic runtime weather-map
  offset derived from date, presentation revision and stable binding ID. Enviro's
  existing single-layer weather map then moves with the project-owned wind at a
  bounded `0.012` layer modifier and `0.35` travel speed. No per-frame random
  sampling is used, so transitions do not shimmer or teleport every clock tick.
- `CloudIntensity01` now carries optical density (`1 - AmbientReadability01`),
  not readability itself. Enviro direct light and shadow strength follow the
  continuous coverage/density pair: clear sky retains about `0.92` direct-light
  multiplier, the production `0.62` mostly-cloudy state falls to about `0.20`,
  and a closed storm remains near `0.10`. The Native HDRP fallback uses the same
  stronger overcast response rather than a bright hard-shadow sun.
- Midday direct and ambient light receive a stronger cool-neutral Finnish tint;
  daylight colour temperature approaches `6300 K`. Hybrid
  daylight exposure moves from `12.50 EV` to `12.65 EV`, about ten percent less
  image brightness, while the accepted readable-night target is unchanged.
- Active Lake Mist/Dense Fog visibility is `80 m`; HDRP fog mean free path may
  fall to `20 m`, and dense-fog maximum distance is capped at `350 m`.

## Performance bounds

- Sky gradients, star curve, and weather clones allocate only during adapter
  startup and are destroyed with the isolated runtime configuration.
- No cloud layer, cloud ray-march step, shadow pass, fog volume, particle system,
  or reflection refresh cadence was added.
- Cloud randomization changes only the existing weather-map UV offset when a
  binding changes. Motion reuses Enviro's existing wind animation; dual-layer
  rendering remains disabled and map resolution is unchanged.
- Continuous cloud targets update only when a project presentation revision is
  accepted. A bounded adapter `LateUpdate` performs only scalar cloud-occlusion
  math plus one directional-light intensity/shadow reassert; it adds no renderer,
  shadow pass, particle system, ray-march step, or per-weather material instance.
- Persistent door portals treat Unity-destroyed zone components as unavailable;
  the streaming bridge may rebind them to a live nearest zone and never calls
  `GetComponent` through a destroyed managed shell.
- Existing bounded rain/splash budgets and selected Enviro quality tiers are
  unchanged. Target-player GPU profiling is still required before claiming the
  stable-60-FPS gate.

## Automated evidence

- Expanded Enviro/presentation/weather-system EditMode suite: `104/104 PASS`.
- Focused cloud/lifecycle EditMode suite: `21/21 PASS`, including the exact
  destroyed-zone regression.
- Production Bootstrap celestial lifecycle PlayMode test: `1/1 PASS`
  (`43.47 s` on the current run). It verifies the hybrid owner, runtime moon
  scale, visible star intensity, continuous `23:20 -> 23:22` celestial movement,
  distinct Partly/Bright/Heavy cloud targets, deterministic field offsets,
  wind-driven motion, and cloud attenuation of direct light and hard shadows.
- Current Enviro integration run: `24/24 PASS`; focused policy/adapter run:
  `8/8 PASS`. The latter executes full-moon/phase/horizon lighting, moon shadow
  bounds, moon colour temperature, night-horizon neutralization, cloud shape
  separation, deterministic field distribution, source-clone isolation and
  the existing lifecycle checks.
- No new project-owned compiler warning was introduced; the obsolete HDRP light
  API call found during the first compile was replaced before the passing runs.

Results are stored locally in
`Reports/CodexWeatherNightClouds_20260813/editmode-results-2.xml`,
`Reports/CodexWeatherNightClouds_20260813/editmode-enviro-results.xml`, and
`Reports/CodexWeatherNightClouds_20260813/playmode-bootstrap-results.xml`.

## Manual acceptance still required

Rendered acceptance must compare Clear, Partly Cloudy, Bright Overcast, Heavy
Overcast, Drizzle, and Dense Fog at matched camera/FOV and time. Hold each cloud
state for at least one game minute to confirm wind movement without popping.
Inspect direct-shadow contrast on the same ground/tree/building target at
`23:20`, `23:54`, `00:30`, and daylight; then advance the date until a near-full
moon is above the horizon and compare the same location with the moon below it.
Capture CPU, render-thread, and GPU frame time in the Windows Development Player.
