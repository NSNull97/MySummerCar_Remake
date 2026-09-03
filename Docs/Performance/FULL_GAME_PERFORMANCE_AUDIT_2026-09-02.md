# Full-game performance audit — 2026-09-02

## Outcome

The current full Bootstrap world has a real rendering regression near the
player home; it is not explained by PlayMode overhead alone. The strongest
measured cause is the packed woody presentation. Before this audit, the fixed
1920x1080 home view measured `22.641 ms` p95 and disabling packed woody reduced
the same view to `10.604 ms` p95.

The first bounded fixes reduce packed woody submissions from `915` to `255`
for the home-facing camera and reduce the final home-facing result to
`16.217 ms` p95 (`14.760 ms` mean). The project now clears the 60 FPS frame
budget in this Editor capture, but the margin is small and the result is not a
final standalone or GPU acceptance claim.

## Scope and capture contract

- Unity `6000.3.11f1`, HDRP `17.3.0`.
- Full `Bootstrap` composition with the active donor feature-parity streaming
  manifest, gameplay activated, HUD route active, and the player at the home.
- AMD Ryzen 9 5950X, NVIDIA RTX 4070 SUPER, Direct3D11.
- Fixed `1920x1080` RenderTexture.
- Camera position `(146.3196, 2.72054, -1046.6018)`, facing the home at
  `(159, 3.2, -1030)`.
- 90 warm-up frames and 180 measured frames per case.
- The capture explicitly sets VSync to `0` and `Application.targetFrameRate`
  to `-1` after the real UI settings are applied.
- Streaming is allowed to settle and then frozen for paired renderer A/B
  comparisons.
- Raw evidence is written to the ignored local path
  `PerformanceCaptures/FullGameAudit/HOME_EDITOR_PLAYMODE_BASELINE.json`.
- Test results and logs are written to ignored `Reports/PerformanceAudit_*`
  and `Logs/PerformanceAudit_*` paths.

The first pre-fix run revealed that `GameUiRoot` reapplied persisted VSync after
the test's initial override. The final harness corrects that ordering and logs
both configured and capture values. Consequently, the initial-to-final timing
delta is useful regression evidence but is not presented as a strict
standalone before/after acceptance benchmark. The renderer ablations and
submission counts are paired evidence.

## Final Editor PlayMode capture

| Case | Mean frame | p95 frame | Mean FPS | p95 floor | Mean draw calls | Packed submits |
|---|---:|---:|---:|---:|---:|---:|
| Home-facing, High, full | `14.760 ms` | `16.217 ms` | `67.8` | `61.7` | `4,667` | `255` |
| Opposite view, High, full | `13.190 ms` | `15.486 ms` | `75.8` | `64.6` | `1,009` | `238` |
| Home-facing, High, far 500 m | `14.300 ms` | `15.483 ms` | `69.9` | `64.6` | `4,537` | `255` |
| Home-facing, no packed woody | `9.570 ms` | `11.097 ms` | `104.5` | `90.1` | `3,986` | `0` |
| Home-facing, no ground vegetation | `14.310 ms` | `16.319 ms` | `69.9` | `61.3` | `4,565` | `255` |
| Home-facing, no vegetation | `8.880 ms` | `9.899 ms` | `112.6` | `101.0` | `3,890` | `0` |
| Home-facing, Balanced, full | `14.200 ms` | `15.355 ms` | `70.4` | `65.1` | `4,637` | `255` |

`MSC.Vegetation.PackedWoodyCullAndDraw` itself records `1.002 ms` mean and
`1.048 ms` p95 CPU time in the final full view. Disabling packed woody still
saves about `5.12 ms` at p95, so most of its remaining cost is downstream
render-thread/GPU/back-pressure work rather than the C# culling loop. The grass
marker is only `0.130 ms` mean / `0.140 ms` p95 CPU.

The host returned no usable GPU frame time (`0` for all samples), and the
Render Thread recorder was unavailable. Those values are classified as
unavailable, not as zero cost.

The Editor capture reports roughly `153 KiB` managed allocation per frame.
This includes Editor/Test Runner overhead, so it cannot be assigned entirely
to runtime code. Static inspection nevertheless found and removed several
guaranteed runtime arrays; a standalone allocation capture remains required.

## Runtime content at the capture point

- `21` loaded scenes, `20` owned by production streaming.
- `1,573` active renderers.
- `1,110` active colliders.
- `17` active Unity lights.
- `30` packed woody renderers containing `8,628` source batches and `15,828`
  instances.
- `10` ground vegetation renderers.

The home cell itself contains `775` GameObjects and `396` MeshRenderers,
including `316` active renderers and `305` active shadow casters. Active home
geometry is only about `92,598` vertices / `70,825` triangles. There are no
LODGroups, no non-zero static flags, and no baked occlusion data. Of the 305
shadow casters, 171 contain at most 100 triangles. The problem is submission,
shadow and visibility fragmentation, not raw triangle count.

The home cell also contains 120 active interior renderers. From outdoors they
receive no room/portal or baked-occlusion rejection. Fourteen authored home
fixtures are shadow-capable; the High lighting profile allows 12 shadowed
lights, four every-frame shadow updates and three high-definition volumetric
beams.

## Root causes and priority

### P0 — packed woody fragmentation

Around the home, 11,192 original trees were serialized into 6,158 source
batches, only `1.82` trees per batch on average, with shrubs adding another
1,135 batches. The old camera callback submitted each visible source batch and
draw part separately. One camera direction reached 3,176 packed submissions in
the pre-fix capture.

The renderer now preserves the tight 32 m source bounds for distance, frustum
and LOD selection, then coalesces only the already-visible matrices by
prototype and LOD into allocation-free submissions of up to 1,023 instances.
Persistent placement metadata, collisions, stable IDs and authored source
batches are untouched.

### P0 — unsafe HDRP working-tree regression

Uncommitted settings had changed all four High shadow atlases from 4096 to
16384, enabled ray tracing/VFX ray tracing and enabled native volumetric clouds.
A single 16384 D16 atlas is nominally about 512 MiB; four such atlases can
represent roughly 2 GiB of raw residency before auxiliary resources.

The Performant profile had also become heavier than Balanced: SSR, transparent
SSR, SSGI, volumetrics, clouds and water were enabled, punctual shadows were
16384, cached punctual shadows were 8192 and the local fog count was 697.

Only these unsafe values were restored to the repository baseline. The
intentional dynamic-resolution, DLSS and DLAA edits in the same assets were
preserved.

### P1 — home renderer/shadow fragmentation

The final view still produces about 3,983 draw calls with packed woody disabled.
The temporary legacy home has hundreds of tiny renderers, including deep
interior objects, but no static batching or occlusion. This is the next largest
home-specific rendering opportunity. Generated classification metadata must be
used; runtime gameplay must not depend on donor hierarchy names.

### P1 — repeated background work

Static inspection found these whole-game costs:

- lighting performed `Resources.FindObjectsOfTypeAll<Camera>()` once per
  fixture every 0.2 s in Editor; it now scans once per budget pass;
- item combustion, cooking, freshness and recovery created repeated dictionary
  and heat-source arrays; they now use reusable mutation-safe buffers;
- `LoadedInstances` created a filtered array on every access, including the
  refrigerator's FixedUpdate; it now exposes the allocation-free registered
  value view;
- authoritative weather publishes a continuously changing state and rewrites
  the HDRP volume every rendered frame;
- NPC schedule evaluation and full presentation reconciliation run on every
  game-time advance for all 22 NPCs and include LINQ/component lookup;
- the UI formats clock/date values every game-time revision and updates a
  hidden performance graph on a fixed cadence;
- stationary world streaming validates the full 220-entry scene manifest with
  repeated loaded-scene scans every frame;
- interaction performs both ray and overlap queries with repeated parent
  component resolution every frame.

The latter five remain measured/profile-required work; cadence or caching must
not be changed blindly because they cross simulation/presentation boundaries.

### P1 — persisted graphics configuration

The active local settings request High Fidelity, TAA Ultra, DLSS off, VSync on,
and a 5,000 m camera far plane. The authored default is 500 m. A 500 m paired
home capture reduces about 130 draw calls but did not produce a stable timing
win across repeated Editor runs, so far distance is not the main home culprit.
It can still be expensive in open-world directions and should be set to
500–1,000 m for normal play unless a wider distance is explicitly being tested.

The settings application order also allowed `SetQualityLevel` to restore the
quality tier's authored VSync after the player selected Off. Quality is now
applied before the explicit VSync choice.

## Implemented changes

1. Coalesced visible packed woody matrices into large prototype/LOD submissions.
2. Prevented custom woody and ground vegetation from rendering into Scene View
   while PlayMode is active; Edit Mode authoring preview remains available.
3. Restored bounded High/Performant HDRP feature and shadow-atlas values without
   discarding the separate DLSS/dynamic-resolution work.
4. Disabled the global Motion Blur override while the accepted UI setting is
   Off and no runtime post-process binding exists.
5. Corrected graphics quality/VSync application order.
6. Reduced Editor lighting camera discovery from once per fixture to once per
   budget refresh.
7. Removed repeated hot-path item/heat-source arrays and the allocating live
   item collection property.
8. Added a repeatable full-world home audit with renderer ablations and raw
   evidence output.

These are presentation/settings/runtime-loop changes only. Stable IDs, save
DTOs, donor provenance, gameplay state, cell addresses, scenes and accepted UI
layout are unchanged. The packed renderer consumes the same matrices and LOD
rules; a manual visual pass is still required to confirm no rendering mismatch
on the user's interactive Game View and standalone player.

## Verification performed

- Full project batch compilation: passed before the audit harness was added.
- Full-world home PlayMode audit: passed after woody batching.
- Full-world home PlayMode audit: passed after HDRP/settings corrections.
- Final full-world audit: passed, 7 paired cases, 1 test in `37.06 s`.
- Targeted EditMode suite: 84/85 passed. The single failure,
  `ExteriorProfiles_ProvideUsefulDuskPhotometry`, is an existing missing-profile
  fixture (`Expected not null`) and is unrelated to the edited lighting camera
  scan. Item runtime and packed woody tests in the same run passed.

The command used for the repeatable full-world audit is:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe' `
  -batchmode -runTests -testPlatform PlayMode `
  -projectPath 'E:\GAYmDev_Studio\MySummerCar_Remake' `
  -testFilter 'MSC.Tests.PlayMode.Performance.FullGameHomePerformanceAuditPlayModeTests.Bootstrap_HomeView_RecordsPairedPerformanceEvidence' `
  -testResults 'Reports\PerformanceAudit_Home_Final.xml' `
  -logFile 'Logs\PerformanceAudit_Home_Final.log'
```

## Manual validation still required

1. In the Game View and a Windows x64 Development Player, verify the home view,
   opposite forest view and moving vehicle view at the user's actual 2560x1440
   resolution. Disable VSync for diagnosis, then test the shipping choice.
2. Inspect packed tree LOD transitions, shadows and motion at walking and driving
   speed. Compare Game View with Edit Mode Scene View; PlayMode Scene View must
   no longer double-submit custom vegetation.
3. Capture GPU and Render Thread timings with Unity Profiler/RenderDoc or PIX;
   the automated D3D11 runner returned no usable GPU counter.
4. Use Frame Debugger at the home to classify the remaining ~4,000 non-packed
   draw calls and shadow passes before changing legacy static flags.
5. Compare home lights all-off versus all-on and Lighting High versus Low.

## Recommended next milestone

**Performance Hardening 01 — standalone GPU capture and home visibility/shadow
reduction.** Produce matched 2560x1440 Development Player captures, then add a
generator-owned static/occlusion/room-visibility policy and a reviewed
small-prop shadow-caster allowlist for the temporary legacy home. Do not begin a
global art rewrite or Phase 2 replacement work.
