# Initial Performance Budget

These are starting targets, not sacred numbers. Measure and revise through ADRs.

## Baseline

- Windows x64.
- 1920x1080.
- Stable 60 FPS on a mid-range gaming PC.
- No ray-tracing requirement.

## CPU

- Avoid per-frame managed allocations in hot gameplay loops.
- Vehicle simulation substeps must be configurable.
- Distant loose rigidbodies sleep or use simplified state.
- Slow systems use scheduled ticks.
- Avoid global object searches during gameplay.

## GPU

- Use LODs for production environment and vehicle assets.
- Control shadow distances and caster counts.
- Use sensible vegetation density and impostors/billboards where appropriate.
- Share materials and use instancing where practical.
- Avoid excessive transparent layers and full-screen effects.

## Memory and content

- Reference-only donor assets excluded from builds.
- Streaming cells or additive scenes for the larger world.
- Texture resolution chosen by texel density and screen use, not ego.
- Audio banks organized by domain/streaming needs later.

## Required captures

For each major vertical slice record:

- CPU main thread;
- render thread;
- GPU frame time;
- managed allocations;
- physics time;
- visible triangles/draw calls;
- memory snapshot;
- build size;
- tested hardware and settings.

## Post-Milestone 4 review-fix status

The `DEP-001` correction moved the M3 opt-in capture component from
`MSC.World.Runtime` to `MSC.Development.Performance`. The complete capture and
file-output branch is compiled only for the Editor or a development player; a
release player retains only an inert serialized-reference placeholder.

Validation on 2026-07-14 rebuilt the Windows x64 development player and reran
the M3 1920×1080 High Fidelity capture on an AMD Ryzen 9 5950X and NVIDIA RTX
4070 SUPER (Direct3D11):

- build size: 217,320,758 bytes;
- 600 measured frames after 180 warmup frames;
- mean frame time: 2.876 ms;
- p95 frame time: 3.250 ms;
- mean CPU frame time: 2.748 ms;
- GPU frame timing: unavailable (`0.0 ms`) despite 600 frame-timing samples.

Ignored artifacts are stored under `PerformanceCaptures/Review99A/` and
`Logs/99A_Performance*.log`.

This rerun validates the development tooling move but does **not** close the
required-capture gate. It is still an M3 garage-only scene and lacks a reliable
GPU time, managed-allocation series, physics time, draw-call/visible-triangle
runtime series and memory snapshot. A representative integrated Player + World
slice does not yet exist; creating one during the review-fix task would cross
the approved milestone boundary. `PERF-001` therefore remains explicitly
blocked rather than being reported as fixed.

## Milestone 05A pilot static budget

The integrated production pilot now provides the first representative Player + World + Assembly content slice. Static Editor audit records 332 renderers, 134 colliders, 64 LOD groups, 158,548 instance-counted mesh triangles and approximately 0.18 MiB of unique mesh data for `WR_HomeYardPilot.prefab`.

These are content counts, not an FPS claim. CPU/GPU frame time, draw calls, VRAM, peak memory, streaming load/unload time and standalone 1920 × 1080 frame pacing remain unmeasured. The 60 FPS target is therefore still a manual performance gate after visual acceptance.

## Milestone 05B.1 bounded world baseline

The accepted Windows x64 Development Player capture on the Ryzen 9 5950X / RTX 4070 SUPER records four 1920 × 1080 HDRP High Fidelity steady-state locations with frame-time p95 `3.620 / 3.863 / 4.003 / 3.803 ms`. This is the last completed pre-M06 frame baseline.

It does not include the M06 vehicle simulation. Resident VRAM, isolated Present cost, `Physics.Processing`, a road-driving-speed location and a continuous production vertical-slice route are unavailable. It must not be subtracted from a later capture to infer vehicle physics cost.

## Milestone 06 simulation budget and audit result

M06 maintains the existing 60 FPS target and adds these gates:

- no managed allocation in the normal warmed simulation tick;
- one backend sample and one apply per `FixedUpdate`, independent of pure substep count;
- explicit cost comparison at `1/2/4` substeps;
- separate direct backend and telemetry snapshot measurements;
- assembly prerequisite scanning only when `GraphMutationCount` changes;
- non-allocating wheel raycasts and cached surface-provider lookup.

`Tools > MSC Remake > Vehicle Simulation > Run Performance Audit` refreshed `Docs/Vehicle/M06_SIMULATION_PERFORMANCE.json` at `2026-07-15T19:37:34.5972700Z`. It used 512 warmup ticks, 10,000 measured pure-simulation ticks and 50,000 backend/telemetry iterations:

| Scope | Cost | Allocated bytes |
|---|---:|---:|
| Pure root, 1 substep | `2.85809 us/tick` | `0` |
| Pure root, 2 substeps | `4.09848 us/tick` | `0` |
| Pure root, 4 substeps | `6.49453 us/tick` | `0` |
| Prototype backend `Sample + Apply` | `7.71657 us/iteration` | `0` |
| Telemetry buffer copy | `0.243964 us/iteration` | `0` |

The isolated audit passes its no-allocation and bounded-method evidence scope. It times direct methods with `Stopwatch` and cannot isolate Unity's later `Physics.Processing` phase, which is recorded as `UnavailableNotMeasured`. Physics CPU therefore remains unavailable until a later player/Profiler capture, and this is not a standalone M06 60 FPS claim.

The bounded M06 graybox track was not a production route. M06A has now added
repeatable config/profile provenance, telemetry off/on comparison and a
bounded production-world driving/streaming fixture. It still does not provide
isolated `Physics.Processing`, GPU timing or a Windows-player 60 FPS
acceptance capture.

## Milestone 06A physics-validation budget result

`Docs/VehicleValidation/M06A_PHYSICS_VALIDATION_PERFORMANCE.json` records a
fresh Unity `6000.3.11f1` pure managed audit with 512 warmup ticks, 20,000
measured ticks and zero measured allocations:

| Scope | Cost | Allocated bytes |
|---|---:|---:|
| Pure root, 1 substep | `2.571855 us/tick` | `0` |
| Pure root, 2 substeps | `3.55201 us/tick` | `0` |
| Pure root, 4 substeps | `5.962805 us/tick` | `0` |
| Root + telemetry snapshot | `6.04886 us/tick` | `0` |
| Scripted validation | `6.95269 us/tick` | `0` |
| Real backend + blocking `Physics.Simulate`, telemetry off | `0.038737 ms/tick` combined; `0.024990 ms/tick` physics | `0` |
| Real backend + blocking `Physics.Simulate`, telemetry on | `0.036251 ms/tick` combined; `0.023025 ms/tick` physics | `0` |

The focused production-world telemetry, after a 50-frame warmup, records:

| Scope | Mean | p95 | Maximum |
|---|---:|---:|---:|
| Root + backend | `0.038197 ms` | `0.0461 ms` (linear) | `0.0629 ms` |

The production fixture traveled `12.255066 m` to `z=-1024` with four wheel
contacts and completed a next-cell-only contact probe at `z=-970`. Both cells
remain `Rejected` / `NeedsRework` for donor fidelity; the capture is valid only
as bounded technical integration evidence.

Automated performance sanity checks pass, and the user accepted the bounded
prototype baseline on 2026-07-16 (`Accepted / HumanAccepted`). The blocking
`Physics.Simulate` wall-clock is measured for a stationary bounded window, but
the `Physics.Processing` marker, GPU frame time, player frame pacing and the
60 FPS production acceptance gate are still unavailable and must be captured
manually. Only Milestone 06B may follow.

## 2026-09-02 full-game home audit

The first repeatable full-`Bootstrap` Editor PlayMode capture now covers the
active donor feature-parity world at the player home. Packed woody rendering was
the dominant measured regression: disabling it reduced the original home view
from `22.641 ms` p95 to `10.604 ms` p95.

The first hardening pass coalesces already-culled woody matrices into bounded
prototype/LOD submissions. The final 1920x1080 High capture records
`14.760 ms` mean / `16.217 ms` p95 with `4,667` mean draw calls, versus `6,051`
before the batching change. Packed submissions fell from `915` to `255` in the
home view. Unsafe working-tree HDRP regressions (16K shadow atlases, RT support
and an inverted Performant feature set) were also removed while preserving the
separate DLSS/dynamic-resolution work.

This is an Editor CPU/frame-pacing audit, not the final 60 FPS acceptance gate:
GPU frame time returned zero and Render Thread timing was unavailable. The
remaining player-build and GPU work, content counts, limitations and exact
capture command are documented in
`Docs/Performance/FULL_GAME_PERFORMANCE_AUDIT_2026-09-02.md`.
