# Satsuma live-engine integration and regression

Status: **Scoped implementation and final automated regression passed; manual
auditory/gameplay acceptance pending.** This is the integration packet for the
user-approved 11A-V1 live-engine pass, not a Phase 1 completion claim. The final
combined results below supersede the earlier in-progress checkpoints without
rewriting their original failures or limitations.

## Final combined regression — 2026-09-06

All three final runs used the installed Unity **6000.6.0f1**, HDRP **17.6.0**,
`-batchmode -force-d3d11 -job-worker-count 2 -runTests`. Each exited **0** with
no test failures or C# compilation errors. These are scoped regression suites,
not every test in the repository or a complete donor comparison.

| Run / local XML in Logs | Total | Passed | Failed | Explicitly skipped | Seconds |
| --- | ---: | ---: | ---: | ---: | ---: |
| `live-engine-complete-edit-20260906.xml` | 1360 | 1359 | 0 | 1 | 380.758 |
| `live-engine-complete-play-20260906.xml` | 130 | 127 | 0 | 3 | 100.107 |
| `live-engine-complete-bootstrap-20260906.xml` | 3 | 3 | 0 | 0 | 46.410 |

The EditMode filter covers VehicleAssembly, VehicleSimulation, Satsuma
LegacyImport/generated content, Items, SaveIntegration, pure audio calibration
and the actual imported-PCM audit. The explicitly supplied `-liveEngineSavePath`
selects the read-only current nine-purchase document. All new operating/source,
cap/pour, part-wear/valve migration, unavailable-owner restore, mechanical/fluid
graphics, native headlamp graphics and related historical authoring checks pass.

The broad PlayMode filter is the same verified 27-fixture roster as the earlier
128-case run, with the added calibration cases discovered in its existing
hybrid-routing fixture. The symptom and mechanical motion files are partials
of `SatsumaEngineFeedbackPlayModeTests`, so they are included, not separate
undiscovered fixture names. The real purchased-plug/belt key-start, actual
cabin mouse/tool controls, bulb handoff, engine pause/restore, separate moving
audio emitters and assembly/contact/steering regression paths all pass.

The separate Bootstrap filter contains
`MSC.Tests.PlayMode.VehicleAssembly.ProductionSatsumaBootstrapPlayModeTests` and
`MSC.Save.Integration.Tests.PlayMode.NativeSaveBootstrapLoadPlayModeTests`.
Actual world activation binds the operating/environment/health/caps/levels/
motion/audio extensions; both complete Bootstrap save-reload lifecycles pass.
Their temporary GUID-named test slots are cleaned up by their own fixture.

### Why four cases were explicitly skipped

- One old EditMode native-start smoke requires the historical `-engineSavePath`
  contract. It is not repurposed for a source containing nine dynamic purchases.
- Two old real-spool PlayMode wiring tests likewise require that historical
  pre-purchase snapshot. The actual current save is not stripped or rewritten
  to satisfy them; its new vehicle/items/world restore checks pass separately.
- One new `RequiresRealtimeAudioDevice` PlayMode probe cannot receive device
  PCM from this batch Editor. Its previous failed realtime/offline/non-batch
  attempts are documented in the loudness report. Imported PCM, filter math,
  real voice gain/mute/pool configuration pass; complete DSP output and manual
  listening do not acquire a false pass from those facts.

Full exact command lines, class filters and absolute local result paths are in
the corresponding `.log` headers. Existing donor mesh-version warnings and
third-party/test-fixture warnings are not silently described as zero warnings.

### Final idempotence and repository checks

Two fresh Unity 6000.6.0f1 processes used `-batchmode -nographics -quit
-job-worker-count 2 -executeMethod` after all three final test runs:

- `MSC.LegacyImport.Editor.GameplayPresentation.Phase1UserSelectedAudioImporter.Build`:
  exit 0, `files=17 events=26 unmatched=18 changed=0 sourceUntouched=true`.
  Log: `Logs/live-engine-audio-idempotent-final-20260906.log`.
- `MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaDashboardControlsAuthoring.RefreshDashboardLightingBatch`:
  exit 0, `changed=0 fullRebuild=false`.
  Log: `Logs/live-engine-headlight-idempotent-final-20260906.log`.

All 41 protected generated files (canonical prefab/meta and user-selected audio
payload/library/meta) are byte-identical across those refreshes. Both methods
use their existing reviewed preflight; no whole baseline regeneration ran.
The user save SHA256 remains unchanged afterwards and no Unity process was
left running by this pass.

`git diff --check` passed on the scoped sources and updated tracked reports.
All 114 inventoried source/test/manifest files have their Unity metadata;
all JSON/asmdef files parse, the 45 new-file GUIDs are unique within the new
roster, and no source covered by the pre-pass backup was removed. The index
distinguishes this pass from unrelated changes already in the dirty worktree.

### Compatibility and acceptance boundary

The generic simulation path, 00–08A module boundaries, wheel/contact backend,
Enviro ownership, stable part IDs and locked UI layout remain in place.
The exact eight-pseudo-fastener retirement preserves the five genuine rocker
bolts. Optional presence-bit, independently versioned DTO extensions preserve
old tuning, litres, temperature, voltage and purchased identities; new
hydraulics default empty, caps closed, valves 7/6 and previously unmodelled
part health 100. No purchased-battery ownership or physical engine-mount
migration was performed.

The source native file is byte-identical after final Bootstrap verification:
SHA256 `D03E6FB217C41B1F4B5054EE89403926352C96CCBE059F546F3A6E32A593A666`.
No user slot was filled, repaired, deleted or replaced. Temporary test copies
were the only cleaned-up content; recoverable pre-authoring backups remain.

Manual steps: open this checkout only in Unity 6000.6.0f1, open
`Assets/Game/Bootstrap/Bootstrap.unity`, enter Play and load the existing slot.
Use the Russian guide for servicing and controls; the stock headlamp housings
in that slot require their four 7 mm bolts tightened, then the existing light
switch needs two clicks from Off. Check sound at unchanged volume settings and
check beams on a real road at night under Enviro. No manual import/build menu
step is required for this already-authored local checkout. Physical-output
audio Test Runner instructions are in the loudness report.

Known limits remain: petrol refuelling is excluded; the fuel-only debug test
switch is explicit and session-only; GT-cover cap presentation, purchased-battery
charge ownership, fused/damaged unrigged mechanical variants and physical engine
mount movement are not declared complete. Isolated graphics and stationary
start tests are not road-physics, 60-FPS, standalone-build or whole-game parity
approval. Manual timbre/loudness acceptance and final-bus true peaks remain open.

Full scoped file inventory: `SATSUMA_LIVE_ENGINE_FILE_INDEX_2026-09-06.md`.
Player-facing controls: `SATSUMA_ENGINE_PLAYER_GUIDE_RU_2026-09-06.md`.
Exactly one recommended next milestone: continue **11A-V1 with the reported
petrol-refuelling repair and an ordinary fuelled start without the debug bypass**,
after this packet's manual feedback. This does not authorize beginning it now
or entering Phase 2.

## Native save / purchased ownership

`SatsumaLiveEngineNativeSaveTests.cs` extends the existing real canonical
consumable fixture. It uses the actual item catalog, generated presentation
provider, item/vehicle bridge, native codec and the vehicle/items/world restore
transaction. Other native domains are integrity-checked by the codec but are
not restored by this isolated test; there is no full-bootstrap claim here.

The explicitly selected old native document contains 126 fixed parts, 124
mounts, 302 historical fasteners and nine purchased descriptors: four plugs,
two bulbs, two belts (one loose), and one loose oil filter. All nine identities,
item conditions, source-cell metadata and installed/loose ownership restore
without proxies or duplicate world ownership. The exact retired eight valve
pseudo-bolts migrate to the current 294-fastener shape. Existing fuel, oil,
coolant and battery voltage remain unchanged; the new hydraulic reservoirs
default empty, caps closed, old unmodelled health 100 and valves 7/6.

A second in-memory round trip changes the new nine part-health values, cap
angles, valve settings and all operating-state fields. It checks capture while
the vehicle's gameplay layer is unavailable, fresh real presentation in another
fixture, and later re-registration of the original unavailable aggregate. The
purchase cell stays unloaded. Nine items plus one vehicle are retained in the
deferred store and consumed once when their owner becomes available. This uses
the actual registration/deferred path, not an end-to-end streaming scene unload.

Executed Unity 6000.6.0f1 EditMode command: `-batchmode -nographics -runTests
-testPlatform EditMode`, filter
`ReadOnlyNativeNinePurchasesRestoreIntoCurrentEngineWithoutServicingOldSave;CurrentLiveEngineExtensionsAndPurchasesSurviveUnavailableOwnerThenFreshPresentation`,
with the explicit read-only `-liveEngineSavePath` argument. Result:
**2/2 passed, zero failures/skips, exit 0** in
`Logs/live-engine-native-initial-20260906.xml`.

The selected user file was verified byte-for-byte unchanged before/after each
test, SHA256 `D03E6FB217C41B1F4B5054EE89403926352C96CCBE059F546F3A6E32A593A666`.
No storage writer, save-slot replacement, donor modification or automatic
servicing is involved. The older no-dynamic-parts start smoke remains strict;
it is not weakened to bypass these nine actual purchases.

## Broad EditMode regression and exact history

The D3D11 broad filter covers VehicleAssembly, VehicleSimulation, Satsuma
LegacyImport/generated content, Items and SaveIntegration. The read-only native
argument above is included. The older `SatsumaNativeStartSmokeTests` requires a
separate `-engineSavePath` and rejects dynamic purchases by design; it remains
one explicit opt-in skip, superseded for this nine-purchase document by the two
actual item/vehicle transaction tests rather than weakened.

- `Logs/live-engine-broad-initial-20260906.xml`: 1353 total; 1314 passed,
  38 failed, one opt-in skipped. Failures exposed stale 302-vs-294 expectations,
  historical fixtures that no longer reconstructed their eight former valve
  identities, and the scoped night importer's frozen 273-target hash guard.
- The night guard now reconstructs **only** the exact complete eight-valve
  retirement when comparing that original hash. It still rejects arbitrary
  missing/substituted targets and partial rosters; historical 302 and current
  294 revisions both remain supported without changing runtime save authority.
- Current-asset assertions use 294/265. Historical 252/260/273/298/302 fixtures
  explicitly reconstruct the retained old IDs; they are not renamed to smaller
  counts. The synthetic older headlight fixture remains unchanged. Native
  historical retirement preserves real five-bolt state and never infers valves.
- `Logs/live-engine-broad-shape-20260906.xml`: **1351 passed**, one failed,
  one opt-in skipped, 353.946 s. The last failure is a secondary expected
  unreviewed-nut count (76 instead of 68); it is corrected with an additional
  assertion that retired valve IDs are absent.
- `Logs/live-engine-broad-mesh-final-20260906.xml`: **8/8 passed**, zero
  failures/skips, exit 0: all front-fastener mesh checks and both read-only native
  transactions. This closes the one remaining broad failure; a single combined
  all-green broad run has not yet been executed after this final test change.

## Real key / serviced-engine flow

`Logs/live-engine-serviced-key-20260906.xml`: **1/1 PlayMode passed**, zero
failures/skips. This is a stationary isolated physics-scene fixture retaining
the actual canonical NWH backend, configuration, operating source, input adapter
and FixedUpdate lifecycle. It is not a road-dynamics comparison or a user-save
edit. Fixed parts and healthy cam timing are prepared through an in-memory
assembly DTO; the four plugs and belt use real purchased item presentation,
handoff and vehicle bridge. The alternator clamp is loosened with its real tool,
tension adjusted from 2 to 7, then re-tightened; the actual stock filter is
hand-tightened to 8. Real coil/charging/fan circuits and 3 L oil, 5.4 L coolant
are present. The warm engine starts at 80 C. Only petrol absence is bypassed;
general fluid bypass and battery autocharge remain off.

Executed flow: hold the actual key, observe cranking and catch, release starter,
idle for three simulated seconds, hold the actual carburetor throttle for two,
return to idle for five, switch the key off and coast down to zero. Fluids remain
in their prepared narrow tolerance with zero petrol. No model, wheel backend or
input source is replaced to obtain the pass. Separate pure/source tests cover
cold start and battery/alternator loss.

## Rendered service levels and production composition

`Logs/live-engine-service-graphics-20260906.xml`: **1/1 EditMode passed**, zero
failures/skips, exit 0. Sixteen actual HDRP captures (four real reservoirs by
empty/half/full/closed) were inspected; see the service-fluid report. The oil
filler intentionally has no fictitious pool in the rocker cover.

The level composition now uses explicit registered parts because real Bootstrap
detaches loose parts before configuration. Production coverage now asserts the
source/environment binding, nine health owners, eight typed valve targets, five
caps, four level presenters, nine shafts/32 deformable bindings and all 17 engine
event clips. The test-only PlayMode assembly gained the existing fluid
presentation dependency for these assertions; its first compile attempt exposed
that missing direct reference and stopped before tests. The reference is now
explicit, with no new runtime module dependency.

Broad PlayMode/full-bootstrap and the queued loudness/headlight follow-up remain
in progress. No blanket all-green or full-game claim is made.

`Logs/live-engine-broad-play-fixed-20260906.xml`: **128 total, 126 passed,
zero failures, two explicit opt-in skips**, 100.219 s, exit 0. This includes
all seven real cabin-input checks, the purchased bulb/plug flows, ten engine
feedback/motion tests, 13 hybrid-routing tests, assembly/compound physics,
front/rear suspension, steering, hinges, ignition and handbrake coverage.
The two skipped `CanonicalWiringReachPlayModeTests` require the historical
pre-purchase native snapshot via `-engineSavePath`; the current document has
nine real purchases, so the guard remains unchanged and is not bypassed by
stripping their descriptors. The new read-only native transaction tests cover
that current document separately. This PlayMode run does not include the three
heavy full-Bootstrap tests.

The Russian operating guide is `SATSUMA_ENGINE_PLAYER_GUIDE_RU_2026-09-06.md`.
Its source check found a reversed pre-existing mixture prompt. The wheel-up
decreasing setting raises AFR (leaner), while wheel-down lowers AFR (richer).
Only the Russian prompt is corrected; input mapping, stored tuning values,
simulation formula and the locked UI layout are unchanged. An actual canonical
source/model regression now checks both scroll directions against their text.

`Logs/live-engine-broad-edit-final-20260906.xml`: **1356 total, 1354 passed,
one failed, one opt-in skipped**, 341.183 s. All previously failing broad tests,
the native transactions, new sibling-reservoir lifecycle check and both graphics
checks passed. The one failure was in the new prompt test: its lookup wrongly
assumed the authored target was a child of the carburetor part. It now locates
the target by its explicit `State` binding in the canonical fixture. No runtime
hierarchy is changed for a test. Focused confirmation follows below; this report
does not relabel the original broad run as all-green.

`Logs/live-engine-mixture-source-final-20260906.xml`: **40/40 passed**, zero
failures/skips, exit 0. Both complete adjustment/source fixtures ran, including
the corrected physical-to-model mixture direction and sibling-reservoir
composition. All identified broad failures have focused passing confirmation;
one combined broad rerun is reserved for the final loudness/headlight changes.

## Full Bootstrap lifecycle

`Logs/live-engine-bootstrap-bound-final-20260906.xml`: **3/3 PlayMode passed**,
zero failures/skips, 45.575 s, exit 0, D3D11/HDRP. The updated production startup
assertions pass through actual world preparation/activation. The engine's
environment bridge waits for a valid published environment sample; after world
activation its ambient temperature matches the production weather output.
The initial test incorrectly required that sample while still in the main menu
(`live-engine-bootstrap-final-20260906.xml`, 2/3 passed). The assertion was moved
to the correct lifecycle stage, not replaced with an unconditional fallback.
The test assembly now directly references the existing weather runtime and
production assemblies; an intermediate compile-only run exposed the missing
test dependencies (`live-engine-bootstrap-lifecycle-final-20260906.log`).

Both native Bootstrap tests pass: ordinary save/load automatically reloads
Bootstrap and restores before reveal, and key-access state survives current
save/load plus v16-to-v18 migration. These fixtures create uniquely named
`test-load-<guid>` / `test-key-<guid>` slots, then remove only those own temporary
slots in teardown. The user's native file remains read-only and its SHA256 is
still `D03E6FB217C41B1F4B5054EE89403926352C96CCBE059F546F3A6E32A593A666`.
No user slot is replaced or serviced. Audio loudness and headlight follow-up
now proceed after this integration packet; final combined regression remains.

## Queued audio and headlight follow-up

The measured relative-loudness correction is implemented for 15 of the existing
26 user-selected event mappings, with optional zero-default calibration and a
bounded overflow stage. Existing libraries, source audio bytes and banks are
preserved. Details, measured levels and initial 25/25 routed-voice checks:
`Docs/Audio/USER_PACK_LOUDNESS_CALIBRATION_2026-09-06.md`.

The physical headlight switch was located and rendered at the far left of the
existing dashboard; it was not duplicated. Its existing prompt now identifies
Off/Parking/Headlights and the next click. The native source has four unfastened
7 mm headlamp bolts; the copied test car lights only after real wrench servicing.
Road beams already worked under the proper gates, but the lenses stayed black.
Two explicit existing lens renderers now follow the same powered lamp state.
Final calibrated HDRP graphics plus dashboard checks: **13/13 passed**, exit 0,
with source save unchanged. Initial excessive lens glow was rejected after
image inspection and reduced without altering global bloom or beam intensity.
Evidence and exact source bindings:
`SATSUMA_HEADLIGHT_VISIBILITY_FOLLOWUP_2026-09-06.md`.
