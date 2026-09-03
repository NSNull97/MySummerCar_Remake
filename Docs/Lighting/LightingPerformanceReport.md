# Lighting performance report

Target: Ryzen 9 5950X, RTX 4070 Super, 2560x1440, stable 60 FPS on High/Ultra.

Implemented performance controls:

- no per-fixture `Update`;
- no continuous scene scan (one initial scan plus scene-load registration);
- one profiled manager update and one profiled budget section;
- cached VLB reflection state;
- cached OnDemand shadow configuration;
- distance hysteresis and native renderer/frustum culling;
- explicit shadow, Every Frame, HD and SD budgets per quality tier;
- distance/zone/stable-ID priority without LINQ or per-refresh temporary lists.

Executed evidence:

- architecture regression verifies `GameLightFixture` declares no `Update`;
- VLB assembly-boundary regression verifies no vendor compile reference;
- focused EditMode lighting suite: 33/33 passed, including cached/static
  containment and directional/environment exclusion regressions;
- Lighting Map Builder batch: exit 0, 70 scenes and 168 prefabs.
- D3D11 Bootstrap lighting lifecycle: 1/1 passed in 42.90 seconds while
  streaming Teimo, Fleetari and home; no cached-shadow, renderer-after-culling
  or null-reference signature was emitted.

Not yet accepted:

- the requested 0 B/frame Profiler capture for the manager;
- matched before/after CPU and GPU timings;
- 1440p High/Ultra FPS on the target GPU;
- per-case GPU cost of home, Teimo, Fleetari, road fog/rain and multiple vehicle
  beams.

The latest functional PlayMode lifecycle is accepted as an automated pass, but
it is not a matched GPU/GC benchmark. No FPS, frame-time or allocation number is
claimed from it; High/Ultra target-hardware profiling remains a separate manual
gate.
