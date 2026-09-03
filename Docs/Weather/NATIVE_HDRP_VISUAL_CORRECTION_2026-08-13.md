# Native HDRP weather visual correction — 2026-08-13

> Activation update: this implementation is retained as a reversible full-Native
> fallback. The production Bootstrap uses the Enviro/HDRP hybrid because Enviro
> already owns the accepted moving sun, moon, stars and celestial sky while the
> native backend has not reached celestial feature parity.

## Trigger and scope

The correction responds to the current-build screenshots captured by the user:

- weak sand-coloured exterior light in clear and cloudy daytime states;
- crushed exterior and interior readability;
- sparse, thick and over-bright rain streaks;
- rain visible through roofs and inside closed buildings;
- no readable ground-impact droplets;
- no readable thunderstorm flash or bolt.

Surface wetness accumulation was not replaced. The user confirmed that the
ground already appears to become wet; this pass changes precipitation and
impact presentation only.

## Implementation

- Native HDRP exposure now uses progressive center-weighted automatic exposure
  with bounded EV limits and local interior compensation.
- The native sun is forced to a neutral base colour while attached, and its
  authored colour/temperature settings are restored on detach.
- Cloud shadowing and indirect-light suppression were reduced so overcast
  weather and powered interior fixtures remain readable.
- Existing authored rain emitters are normalized at runtime, so the correction
  does not require a destructive Bootstrap scene rebuild.
- Rain uses thin low-alpha neutral streaks, a smaller camera-local emitter and
  immediate particle clearing when precipitation exposure becomes zero.
- World collision kills rain on roofs and other static colliders. Collision
  messages feed one project-owned surface-impact particle pool.
- Lightning uses a deterministic segmented bolt and a short multi-pulse local
  flash. Gameplay lightning scheduling and thunder authority are unchanged.
- Explicit HDRP sky-environment refresh is requested only for sequenced sky or
  ambient refresh events.

## Performance bounds

- heavy rain: at most `5200` live particles, `4200/s` authored rate;
- drizzle: at most `2800` live particles, `1800/s` authored rate;
- emitter footprint: at most `18 x 14 m` around the presentation camera;
- collision quality: `Low`, static colliders only, at most `128` collision
  shapes per emitter;
- impacts: one shared `600`-particle pool;
- impact emission: at most `28` droplets/frame for rain and `12` for drizzle;
- collision events use a pre-sized reused list and create no managed objects in
  the callback;
- profiler marker: `Weather.RainSurfaceImpacts`.

These are code and lifecycle bounds, not a final GPU performance sign-off.
Matched rendered profiling on the target Windows player remains required after
manual visual acceptance.

## Automated evidence

- direct Roslyn compile: Native HDRP runtime, weather Editor tooling, Weather
  System EditMode/PlayMode and Weather Production PlayMode assemblies — pass;
- `NativeBackend_AttachClonesProfileAndDetachRestoresOwners` — `1/1 PASS`;
- `Bootstrap_AttachesNativeWeatherAndPresentsRainAndLightning` — `1/1 PASS`;
  the RTX 4070 SUPER GPU run also verifies live rain collision and impact
  emission against a temporary roof collider;
- `Bootstrap_RestoresBeforeRevealAndSurvivesAdditiveLifecycle` — `1/1 PASS`.

Results are stored in `TestResults/Codex_WeatherVisualFix_*.xml` locally.

## Manual acceptance still required

Check clear, overcast, heavy rain and thunderstorm at noon, dusk and night;
walk from outdoors into the home and garage; look into both buildings from
outside; verify roof occlusion, impact visibility, powered fixture readability
and frame time. Visual fidelity and target-player performance are not claimed
until that route passes.
