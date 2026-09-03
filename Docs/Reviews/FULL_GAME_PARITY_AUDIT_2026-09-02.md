# Full Game Parity Audit — 2026-09-02

Status: **Current repository audit snapshot / Phase 1 gate not met**

This document is the current high-level source of truth for repository maturity.
It supplements, but does not replace, the row-level authority of
`Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv` and the persistence authority of
`Docs/Save/FULL_GAME_SAVE_COVERAGE.csv`.

The repository was actively changing during the audit. The snapshot includes
the last green Satsuma builder evidence for `11A-V1d.48`, vegetation
presentation `v12.2`, native save document version `16`, and the test/build
evidence available on 2026-09-02. Final documentation validation also exposed
a later current-tree Satsuma Editor compile blocker described below. Later
implementation must update this snapshot or supersede it explicitly.

## Audit scope

- locked donor revision, hashes and donor/runtime separation;
- Git/worktree state, Unity/package versions and Build Settings;
- runtime module and content inventory;
- all 534 parity rows, including 517 `Required=Yes` rows;
- all 557 full-game save-coverage rows: 534 parity features and 23 explicit
  domain contracts;
- current Satsuma, world, vegetation, traffic, NPC, commerce, audio and UI
  evidence;
- recent EditMode/PlayMode/performance reports and generated build logs;
- current Windows player-build availability and remaining manual gates.

## Status vocabulary

| Status | Meaning |
|---|---|
| `Verified` | Implemented and accepted with the executed evidence required by the parity matrix. |
| `KnownDifferenceApproved` | Implemented difference explicitly accepted by the user. |
| `ImplementedUnverified` | Implementation exists, but the required manual or donor comparison gate is incomplete. |
| `PartiallyImplemented` | A bounded runtime slice exists; required behavior, coverage or integration remains incomplete. |
| `EvidenceCaptured` | Donor evidence exists, but the required gameplay feature is not implemented. |
| `Specified` | Required behavior is described, but no sufficient implementation exists. |
| `Blocked` | An evidenced external decision or dependency prevents progress. |

Type, prefab, scene or test existence alone never promotes a row to `Verified`.

## Overall Phase 1 result

After the 2026-09-02 documentation synchronization, the 517 required rows are:

| Status | Rows |
|---|---:|
| `Verified` | 4 |
| `KnownDifferenceApproved` | 1 |
| `ImplementedUnverified` | 6 |
| `PartiallyImplemented` | 202 |
| `EvidenceCaptured` | 303 |
| `Specified` | 1 |

Only `4 / 517` required rows are formally `Verified`. This is a verification
ratio, not a percentage-of-code estimate. The repository contains substantial
architecture and bounded gameplay implementation, but the Legacy Feature
Complete gate is not close to closure because 303 required features still have
donor evidence without a corresponding complete runtime feature.

The four verified rows are:

1. canonical donor map layout and landmarks;
2. 49-cell additive streaming and the active world profile;
3. project-owned 08A main menu/settings/HUD presentation;
4. temporary donor world presentation baseline.

## Domain status

| Domain | Required | Current formal state |
|---|---:|---|
| Player | 15 | 9 Partial, 5 Evidence, 1 Specified |
| Needs | 15 | 7 Partial, 8 Evidence |
| Home | 19 | 10 Partial, 9 Evidence |
| Items | 99 | 99 Partial |
| Satsuma | 20 | 9 Partial, 11 Evidence |
| Vehicles | 40 | 11 Partial, 29 Evidence |
| Traffic | 10 | 10 Partial |
| NPC | 72 | 21 Partial, 51 Evidence |
| Economy | 5 | 5 Partial |
| Services | 43 | 5 Partial, 38 Evidence |
| Communications | 24 | 1 Partial, 23 Evidence |
| Jobs | 22 | 22 Evidence |
| Story | 22 | 22 Evidence |
| Authority | 21 | 21 Evidence |
| Rally | 8 | 8 Evidence |
| Media | 54 | 54 Evidence |
| Save | 6 | 4 ImplementedUnverified, 2 Partial |
| World | 12 | 2 Verified, 8 Partial, 2 Evidence |
| Presentation | 10 | 2 Verified, 5 Partial, 2 ImplementedUnverified, 1 KnownDifferenceApproved |

## Implemented foundations

The following are real integration baselines and must be extended rather than
replaced:

- Unity `6000.3.11f1`, HDRP `17.3.0`, Input System `1.19.0`;
- project-owned stable IDs, composition roots, registries and module boundaries;
- exact temporary donor map identity with 49-cell additive streaming;
- project-owned player/interaction/carry/tool capability foundation;
- vehicle assembly and simulation boundaries, including an `IWheelPhysicsBackend`;
- project-owned time/weather/wetness/lightning with Enviro 3 presentation;
- `IAudioBackend`, Wwise integration and Unity fallback;
- accepted 08A project-owned UI structure;
- native save format v16 with atomic storage, recovery, migrations and 14 current
  domain participants;
- bounded item, needs, home, NPC, traffic, economy and physical-service
  foundations;
- removable `TemporaryDirectImport` world/character/vehicle/item/audio
  presentation wrappers.

These foundations are not equivalent to full donor feature parity.

## Native save correction

The pre-audit parity rows incorrectly stated that native world save, slots,
atomic recovery and migrations did not exist. Current code has:

- `SaveDocument.CurrentDocumentVersion = 16`;
- deterministic JSON, SHA-256 integrity and bounded slot IDs;
- `.tmp`, `.bak`, quarantine and automatic recovery;
- dependency-ordered capture/preflight/apply/rollback;
- migrations through v16;
- real New Game, Continue, Load and Save Status bindings;
- deferred stable-entity restore across unloaded cells;
- 14 participants: `core.time`, `weather.environment`, `player.state`,
  `world.entities`, `interaction.carry`, `items.instances`, `vehicle.satsuma`,
  `player.needs`, `home.state`, `lighting.electrical-grid`, `economy.player`,
  `services.state`, `npc.state` and `traffic.state`.

Rows `P1.SAVE.001` through `P1.SAVE.004` are therefore
`ImplementedUnverified`, not `Specified`. `P1.SAVE.005` and `P1.SAVE.006`
remain Partial because complete unloaded-cell coverage and fresh/mid/late
round trips do not yet include every Phase 1 domain.

The current save-coverage matrix contains 557 rows:

| Coverage | Rows |
|---|---:|
| Covered | 11 |
| Partial | 203 |
| Planned | 310 |
| Uncovered | 13 |
| NotRequired | 19 |
| PendingDecision | 1 |

Missing persistent domains include jobs, communications, story/progression,
authority, rally, media and generic non-Satsuma vehicles.

## Satsuma status

The last green builder evidence for `11A-V1d.48` completed successfully with:

- 8 renderers and 29 donor-evidenced solid colliders;
- 125 loose parts, 120 active at the generated start state;
- 117 mounts and 260 runtime fasteners;
- 3 hinged mounts and 52 part-owned mounts;
- 46 mail-order mappings.

The implemented slice includes stable part identities, assembly routing,
fastener/tool rules, physical chassis/parts, bounded drivetrain and suspension,
native persistence, replacement-safe temporary presentation and current New
Game paint state.

It remains Partial because wiring, fluids, thermal behavior, wear, failures,
body damage, tuning, aftermarket configurations, gauges/electrical consumers,
inspection legality, Fleetari outcomes and donor-calibrated driving are not
complete. A full new-game build/drive/break/save/stream comparison has not been
accepted.

The first V1d.48 generated-content run was `28/29` because the test used an
invalid NUnit collection assertion. The assertion was corrected without
changing runtime behavior; `codex_body_paint_type_tests_r3.xml` is the current
last green rerun at `29/29 PASS`.

That result is not a current-tree compile pass. A later targeted save-coverage
test attempt on 2026-09-02 failed during script compilation before Test Runner
and produced no result XML. `Phase1SatsumaBaselineBuilder.cs` currently calls
missing `BuildRustHdrpTextures` once and missing `CreatePaintSurfaceBinding`
twice (`CS0103`). These later incomplete edits are outside this documentation
sync and must be resolved by the active 11A-V1 owner.

## Entire required domains without runtime feature completion

There are no dedicated runtime module roots for `Jobs`, `Authority`, `Rally`,
`Media`, `Progression` or `Communications`. The corresponding donor evidence is
catalogued, but the following required gameplay is not implemented:

- 22 jobs and repeatable activities;
- 22 story/progression chains;
- 21 inspection/police/fine/jail/death/legal rules;
- 8 rally systems;
- 54 radio/TV/computer/minigame/media rows;
- phone, mail, bills, packages and caller/event scheduling beyond the partial
  catalog-order slice.

## World and presentation status

The donor map identity and streaming profile are verified. Terrain/road/water
continuity, solid collision, interactive doors/windows, traversal landmarks,
out-of-bounds behavior, utilities, clocks/signs and replacement-layer coverage
remain Partial or Evidence-only.

The active world is a private `TemporaryDirectImport`, not `ProductionReady`.
Current vegetation uses reviewed removable third-party presentation and
project-owned streaming/rendering. The v12.2 zero-radiance regression gate
passes, including a `36/36` focused lighting suite and zero grass luminance in
the black-environment probe. Whole-map subjective gameplay-night acceptance,
representative Player performance and production-licence closure remain open.

## Build and performance status

- latest existing Windows player: Milestone 08A UI correction, 2026-07-20;
- no current Windows x64 player represents the active working tree;
- the active working tree currently fails Editor script compilation on three
  missing Satsuma builder helpers, so no current build gate is green;
- Build Settings currently contain 233 enabled scenes: 1 Bootstrap, 50 legacy
  world, 172 vegetation, 5 prototype and 5 other scenes;
- current home-view Editor capture at 1920x1080 records p95 `16.217 ms`, mean
  `14.760 ms`, 4,667 draw calls, 1,573 active renderers, 1,110 colliders and
  approximately 153 KiB managed allocation per frame;
- no representative 2560x1440 standalone GPU/Render Thread acceptance exists;
- home still has 305 active shadow casters and no accepted LOD/static-flag/
  baked-occlusion solution.

The 60 FPS target is therefore not verified.

## Test status at capture

- vegetation grass-lighting hotfix EditMode: `36/36` passed;
- Satsuma V1d.48 generated content: `29/29` passed after correcting the
  test-only collection assertion; this is the last green bounded evidence;
- targeted save-coverage EditMode rerun: not executed because current script
  compilation failed first with three Satsuma builder `CS0103` errors; no XML
  was produced;
- performance targeted EditMode: `84/85`, missing dusk photometry profile;
- recent streaming handoff PlayMode: `6/6` passed;
- recent vehicle recovery EditMode: `1/1` passed;
- full EditMode D3D12 attempt crashed in `D3D12Core.dll` before XML output;
- D3D11 retry was invalidated by concurrent source changes/recompile and
  produced no final XML.

No full-suite pass is claimed.

## Repository-state risk

At capture, HEAD remained `41b114cbf967aea740810cdce1b37c90bfef3a1f`
from 2026-07-21 with 231 modified tracked files and 698 untracked paths. The
working tree was being changed by concurrent Unity batch tasks. Results in this
document are a dated snapshot, and generated logs alone must not be treated as
a clean release baseline.

## Phase 1 gate conclusion

Phase 1 is **not ready** and Phase 2 must not begin. The architecture through
08A and the later bounded foundations are preserved, but full donor feature,
presentation, save, test, build and playthrough coverage is incomplete.

The next active closure boundary is **Milestone 11A-V1 — full Satsuma state**.
Its acceptance requires a green V1d.48-or-later gate plus complete wiring,
fluids, thermal, wear/damage, tuning/electrical, inspection/Fleetari integration
and a donor-compared build-drive-save-stream gameplay route.
