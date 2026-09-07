# Private Development build FPS audit — 2026-09-05

## Conclusion and scope

The user's observation (roughly 60 FPS with occasional higher values) is
consistent with insufficient rendering headroom. A hard project-owned 60 FPS
limit was not found, including in the delivered managed binaries. The strongest
existing causal evidence is expensive packed tree/shrub rendering and a heavily
fragmented home scene. Precise current standalone CPU-versus-GPU attribution
remains unmeasured.

This is an audit only. No gameplay, graphics setting, asset, save, vendor file,
or executable was changed. No Unity/player process was launched in this audit.
The only new deliverable is this report. Earlier tests and captures are explicitly
identified as historical evidence, not reruns or fresh standalone measurements.

Inspected build: `Builds/PrivatePhase1_20260905_024640/`, previously repaired for
serialized script references and explicitly marked as post-build patched for
its installed third-party Wwise native plugin. The user's latest `Player.log`
identifies this exact build, Unity 6000.3.11f1, D3D12, and RTX 4070 SUPER.

Read-only Mono.Cecil inspection confirms:

- `MSC.UI.Presentation.Runtime.dll` SHA-256:
  `D4AD1F28F539589D2675C5213121B7B22995D107B35E331C78B870DF82E32B5A`.
- `MSC.World.Runtime.dll` SHA-256:
  `E1335797BE5555DCC247B9F2C4D7FD816B4268CBD8A46F2D1BEF1C4B1875C457`.

The working tree contains later changes, so current source alone is insufficient
to establish the shipped implementation. Binary checks below address that gap.

## 1. No hard 60 FPS cap found

All 210 managed DLLs were inspected for frame-limiter setters. Project-owned
`Application.targetFrameRate` assignments only set `-1` in opt-in diagnostics;
no positive 60 assignment or project-owned `renderFrameInterval` limiter was
found. Bundled Unity Adaptive Performance support is not evidence of an active
provider or limiter.

The shipped `GameUiRoot.ApplyDocument` calls `SetQualityLevel` at IL_00c9 before
setting `vSyncCount` from the user's choice at IL_00dc. Therefore, the previous
quality-change/VSync ordering defect is already fixed in this build. Source:
`Assets/Game/UI/Presentation/Runtime/GameUiRoot.cs:828–833`.

Current saved settings request 2560x1440, borderless, High Fidelity, TAA Ultra,
DLSS off, far clip 1336 m, and VSync on. However, the settings file was last
written at 10:15:02 while the user's player log ended at 09:31:49; this snapshot
does not prove the exact settings used throughout that session. The saved
refresh value is 165 Hz while the user reports a 144 Hz display. Neither that
saved value nor a later desktop query should replace the user's measurement.
VSync can still affect pacing, but it does not explain a rigid 60 cap here.

## 2. Strongest measured rendering causes

The existing capture from 2026-09-02 used the full Bootstrap/gameplay/HUD home
view on this machine (Ryzen 9 5950X, RTX 4070 SUPER), D3D11 Editor PlayMode,
1920x1080 RenderTexture, VSync 0, targetFrameRate -1, settled/frozen streaming,
90 warm-up frames and 180 measured frames per case.

| Same home view | Mean frame | p95 frame | Mean FPS | Mean draw calls |
|---|---:|---:|---:|---:|
| High, full content | 14.760 ms | 16.217 ms | 67.8 | 4,667 |
| Packed trees/shrubs disabled | 9.570 ms | 11.097 ms | 104.5 | 3,986 |
| Ground vegetation disabled | 14.310 ms | 16.319 ms | 69.9 | 4,565 |
| All vegetation disabled | 8.880 ms | 9.899 ms | 112.6 | 3,890 |
| Balanced, full content | 14.200 ms | 15.355 ms | 70.4 | 4,637 |

Source report: `Docs/Performance/FULL_GAME_PERFORMANCE_AUDIT_2026-09-02.md:47`.
Raw evidence: `PerformanceCaptures/FullGameAudit/HOME_EDITOR_PLAYMODE_BASELINE.json`.

Packed trees/shrubs account for a paired difference of 5.19 ms mean / 5.12 ms
p95 in that view. This is the leading measured subsystem cost. Its own C# marker
was only 1.002 ms mean / 1.048 ms p95: the full frame difference cannot be
attributed solely to C# culling. Render-thread submission, GPU work and pipeline
waiting require separate timings.

The previous tree optimization IS in the shipped `MSC.World.Runtime.dll`:
`PackedWoodyCellRenderer.OnBeginCameraRendering` calls `EnsureSubmissionBuffers`,
`ResetSubmissionGroups`, `AppendBatch` and `FlushSubmissionGroups`; `AppendBatch`
uses pooled matrix submissions. This is not a missing-old-fix regression. Source:
`Assets/Game/World/Runtime/Vegetation/PackedWoodyCellRenderer.cs:94–262`.

The shipped renderer also retains expensive distance rules: mature tree LOD0
through 80 m, no final billboard closer than 220 m, and a maximum tree draw
distance of 900 m. Constants and use sites were verified in IL and current
source (`PackedWoodyCellRenderer.cs:17–19,310,346`). These are concrete GPU/pass
profiling candidates, not a reason to remove required vegetation from gameplay.

The home still had 316 active renderers, 305 shadow casters and 120 interior
renderers in the prior content audit, with no LODGroups, nonzero static flags or
baked occlusion data. Of those shadow casters, 171 had at most 100 triangles.
Even with vegetation removed, the view retained about 3,890 draws. The problem
is small-object/material/shadow pass fragmentation and ineffective rejection of
hidden content, not evidence that the home has too many triangles. These are
historical content counts; later Satsuma additions were not recounted as a live
scene in this audit.

The historical capture loaded 21 scenes, of which 20 belonged to streaming.
The 233 scenes included in the executable must not be described as 233 scenes
simultaneously rendered or simulated.

## 3. Why the current graphics profile leaves little headroom

60 FPS allows 16.67 ms per frame. The previous full home view already needed
14.76 ms on average and 16.22 ms p95 at 1080p. A 144 Hz display needs 6.94 ms for
144 FPS; the baseline is far from that target even before moving to 1440p.

2560x1440 contains 1.78 times the pixels of 1920x1080. With DLSS off, this adds
potential raster/fullscreen-effect cost. This ratio is not an FPS prediction:
the exact change depends on the CPU/GPU split. TAA Ultra and HDRP lighting are
additional profiling candidates, not separately measured causes in this build.

Changing High to Balanced only improved the historical mean from 67.8 to 70.4
FPS. Disabling grass alone gave no reliable p95 improvement. Shortening the
historical 5000 m far clip to 500 m removed about 130 draws but did not yield a
stable timing improvement across repeated runs. These are weaker first targets
than woody rendering and home visibility/shadow fragmentation. The current
saved far clip is 1336 m; the old 5000 m value is not current.

The old HDRP regression with 16384 shadow atlases and ray tracing was already
repaired. Current High assets use 4096 atlases and ray tracing disabled. Do not
attribute the user's present slowdown to those already-corrected settings.

## 4. Additional costs requiring timing

- Shipped streaming checks required global/cell/layer scenes in its steady
  Update path, with repeated manifest and loaded-scene traversal even when
  stationary. Source: `ProductionWorldStreamingService.cs:644,1128,1221`.
- Current NPC runtime evaluates schedules and reconciles presentations on each
  game-time event. Source: `NpcWorldRuntime.cs:1930`.
- The executable is `Development | AllowDebugging`, as shown by the build
  entry point and player debugger startup. All 56 shipped `MSC.*` assemblies
  contain `DebuggableAttribute=263`, including `DisableOptimizations`. This
  confirms debug-oriented managed compilation. The cost relative to a matched
  optimized build is unknown; no fixed percentage is justified. No project
  IL call enabling Deep Profile or `Profiler.enabled` was found; Development
  must not be conflated with an active Deep Profile session.
- Actual player logs contain repeated route rebuilding when applying settings:
  Controls alone records roughly 45–52 ms. These explain possible settings/menu
  hitches, not persistent gameplay FPS by themselves.

No measured millisecond budget is assigned to these candidates. In particular,
main-thread elapsed time includes possible waits and cannot by itself prove
that gameplay simulation saturates the CPU. The old GPU recorder returned only
zero samples and the render-thread recorder was unavailable. Those are missing
measurements, not zero GPU/render-thread cost. The old Editor allocation series
also includes Test Runner/Editor overhead and is not a standalone GC budget.

The current user log has no recurring `AkUnitySoundEngine` DLL-load exception
storm. The previous missing-DLL smoke failure is not a supported explanation
for this user's current FPS.

The same actual log contains 104 `Story traffic physical stall` warnings for
`P1.NPC.102`: `pathBlocked=True`, `sleeping=False`, `kinematic=False`. This proves
a persistent blocked physical actor, but not its distance from the player or
its CPU cost. The diagnostic is throttled to once every five seconds after a
two-second stall (`StoryTrafficVehiclePresentationBinding.cs:3424–3453`), so
this is not a per-frame logging storm. Its route/obstacle/backend work continues
in FixedUpdate. Story-actor physical residency is intentional; any future
scheduling optimization must preserve off-screen story/traffic outcomes.

## 5. FPS counter limits

The shipped HUD counter averages the last 240 frame deltas, rounds to an integer
and updates every 0.25 s. At 60 FPS its averaging window is about four seconds.
It has no upper clamp, but it smooths brief speed changes. Frames longer than
one second are discarded, so long freezes are underrepresented. Source:
`Assets/Game/UI/Presentation/Runtime/GameUiRoot.cs:1018–1076`.

## Audit validation and next milestone

Executed: read-only repository/configuration/log searches; inspection of raw
historical JSON and capture implementation; SHA-256 checks; Mono.Cecil IL
inspection of delivered assemblies; source/binary verification of frame
settings, FPS counter, tree batching and streaming traversal.

No new gameplay tests, runtime frame capture or optimization was executed. No
manual Unity step is needed to apply this report. Compatibility impact on
accepted 00–08A systems and save schemas: none; no migration.

Recommended next milestone: **Performance Hardening 01 — standalone GPU capture
and home visibility/shadow reduction**, already identified by the September 2
audit. Use the same home view and fixed weather/time in a private player with a
disposable copy of save data. Capture 30–60 seconds of CPU/render-thread/GPU
timings and p95/p99 with VSync off, then compare 1440p versus 1080p, packed woody,
and local lights/shadows one variable at a time. Follow with targeted batching,
visibility and shadow-policy changes based on those measurements. Retain the
required Phase 1 content and gameplay behavior throughout.
