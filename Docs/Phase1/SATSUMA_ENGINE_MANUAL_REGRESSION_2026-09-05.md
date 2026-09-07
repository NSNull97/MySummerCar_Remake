# Engine manual-regression follow-up — 2026-09-05

Status: `AutomatedValidatedPendingManual`, not manually accepted. This follow-up supersedes the
acceptance status (not historical test results) of the previous engine packet.

User clarification: the engine moves immediately after being placed in the bay,
before fastening; the misplaced bolts prevented attachment entirely. Do not
reinterpret this as movement of an already secured engine.

## Scope

Three user reports: engine moving in the bay, mount bolts above the car, and
carburetor throttle highlighting without responding to input. Preserve the
approved 120 kg carry gate, assembly identities, accepted suspension and player
systems. No engine simulation rewrite, hoist implementation or save-file edits.

## Confirmed causes

### Loose engine compound-contact stability

The new generated-38-part diagnostic failed before the motion repair. Moving
the whole engine from the origin through home-sized world coordinates without
changing assembly or settings moved its own local contact shapes by up to
0.000222648465 m and its center of mass by 0.0000572649151 m. The old per-step
world-to-local calculation exceeded its 1 micrometer update threshold, causing
shape updates, inertia resets and wake calls for unchanged rigid geometry.
The companion installed-engine test passed: ON2 kept the body kinematic at its
mount, with zero independent compound contacts.

Repair only `AssemblyLooseCompoundPhysics`: compose the explicit source-to-owner
local transform chain. For a bound installed part at its authoritative socket,
use its exact identity position/rotation contract instead of propagating the
roundoff from the kinematic world-pose synchronization. Keep actual mount and
presentation transforms, shape scale, real mass/gravity and live adjustments.
No changes to `PartInstance`, suspension or chassis attachment are needed.

This establishes a real source of artificial contact motion, not a claim that
every unbolted engine movement is a bug. A loose engine still responds to gravity
and support contacts. New actual generated-engine contact tests cover settling
at origin and home coordinates; a manual bay comparison remains required.

### Engine mount bolt coordinates

The builder measures the three engine fastener positions/rotations relative to
the donor Satsuma root, but generic mount-object creation uses them as local
coordinates beneath the solved, rotated engine installation pose. This applies
the motor frame a second time. The docking proximity points are independent and
were already converted/bound correctly; correct proximity therefore exposed
incorrectly positioned visible bolts.

The repair converts these three measured chassis-local poses into the actual
mount-local frame. Preserve mesh identity, physical scale 1.1, 22 mm full-stage
travel, the three 11 mm stable fastener IDs and ON2/OFF0/MAX24. Validate both
origin and translated/rotated vehicle frames. Do not move the engine install
pose or compensate with arbitrary offsets.

### Carburetor throttle input routing

`AssemblyCarburetorThrottleTarget` implemented the continuous capability but
returned `UsesDirectionalHold=false`. The existing player press/hold/release
route and HUD require that flag. Highlight eligibility did not require it, so
the target could highlight without ever receiving a mouse-held begin event.

Enable the existing route locally on the target, retaining its Primary-only
gate. The donor throttle is held LMB, not wheel-operated mixture adjustment or
a fabricated RPM setting. Validate through the actual player routing in addition
to direct target calls. No player-controller redesign is needed.

## Original reference

Frozen GAME SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Three engine trigger pairs and bolt identities: existing
`SATSUMA_ENGINE_DOCKING_BINDINGS_2026-09-05.md` and
`Phase1SatsumaV1dEngineMountTriangulationAudit.csv`.
Throttle `Screw108301`, linkage pivot60542 local X0/40 degrees and its separate
mixture control: `SATSUMA_ENGINE_ADJUSTMENTS_2026-09-05.md`.
Classification: `BehavioralReference`, `ConfigurationTransferred`,
project-owned `Reimplemented`; imported visual payload stays
`TemporaryDirectImport`. Original installation remains read-only.

## Validation

Pending this follow-up's source integration, scoped prefab refresh and tests.
Previous 799/799 and 74/74 passes did not cover these observed integration
failures and are not evidence of their resolution.

Diagnostic Unity EditMode run:
`Logs/codex-engine-followup-motion-before.xml` / matching `.log`: 8 executed,
7 passed and the loose-engine local-geometry invariant failed as described
above. The six bolt frame checks and installed-engine check passed. Preserve
this failing baseline as evidence; do not count it as a successful final run.
An initial carb invocation used the wrong test namespace and selected no tests;
it is likewise not validation evidence.

Post-repair EditMode: `Logs/codex-engine-followup-edit-01.xml`, 807/807 passed,
zero failures/skips. All four measured geometry/center/scale/rotation drift
values are zero with the repaired compound calculation.

First PlayMode integration run: `Logs/codex-engine-followup-play-01.xml`, 78/80
passed. The four actual mouse-routing carburetor tests and all 74 prior
regressions passed. Both new contact tests exposed a fixture ownership error:
the prefab detaches its loose-parts root in Awake, so moving only the vehicle
root to a local physics scene left the engine unsimulated in the default scene.
Their unchanged position/velocity was not successful settling. Correct the
scene transfer and cleanup of both explicitly owned roots, assert scene/body
flags and actual initial motion, then rerun before reporting physical results.

Corrected physical fixture: `Logs/codex-engine-followup-contact-02.xml`, 3/3
passed. The full 38-part engine actually moves about 40.8 mm during initial
landing on the test table. At both origin and home coordinates, its final
two-second window has zero speed, travel and awake frames.

The additional engine-bay fixture keeps the real chassis/subframe solids and
starts the free engine at the reviewed docking pose. Initial contacts include
four overlaps with Collider_92134, 18.6–76.5 mm. Physical contact resolution
moves the free engine about 68.5 mm initially; this is not a fixed or auto-snapped
engine. In the last two seconds, speed is at most 0.0010978 m/s and travel
0.0001223 m. Final docking distances are 0.0901921, 0.0624503 and 0.0464683 m,
all inside the existing 0.1 m threshold. The refined regression asserts final
speed <0.01 m/s, travel <0.002 m and availability of all three bolts for this
specific starting/support configuration. Bay sleep is not promised: its live
support contact remains awake. No alternate placement/rotation is claimed to
stay inside the docking range.

Independent source review found no new scale/ownership defect in the local-chain
repair: current generated chains contain no negative scale or rotated descendants
under materially non-uniform ancestors. The one materially non-uniform solid
scale belongs to a leaf collider. A future sheared shape hierarchy needs explicit
review; this is not a general-purpose shear-preserving collider system.

Final PlayMode integration: `Logs/codex-engine-followup-play-final.xml` and
matching `.log`, **81/81 passed**, zero failures/skips. This includes all 74
previous regressions, four actual mouse-routing tests and the three corrected
physical tests with the strengthened bay limits above. Every Unity invocation
uses one hidden process, `-batchmode -nographics -job-worker-count 2`, pinned
6000.3.11f1. Full filters and arguments are recorded at each log's start;
test invocations use `-runTests -testPlatform EditMode|PlayMode -testFilter ...
-testResults ... -logFile ...` (no `-quit` with tests). Refresh uses
`-executeMethod MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaEngineRepairBatch.RefreshEngineRepairBatch -quit`.

Final EditMode rerun after fixture ownership/cleanup repair:
`Logs/codex-engine-followup-edit-final.xml` / matching `.log`, **807/807 passed**,
zero failures/skips; all common-motion drift measurements remain exactly zero.
SHA256 Edit XML:
`9D61DAA0B975461B23053BB1F93A513AB17E049E60126C36DBB97793C6BCF098`;
Play XML:
`5F3B994791E5B2A36E029053D3A3B196DD2BC41701E61DB7F8466498A9230E39`.
All six native current/backup/corrupt-snapshot files remain byte-identical to
this follow-up's fresh pre-check hashes. `git diff --check` reports no whitespace
errors; existing unrelated CRLF conversion notices remain. No new runtime
compiler errors or extra engine warnings were introduced. Unity exited after
the single-process runs and is free for manual testing.

## Scope and recovery

The scoped `RefreshEngineRepairBatch` ran with `changed=3 repeat=0`, 273 targets,
39 compound parts and `fullRebuild=false`:
`Logs/codex-engine-followup-refresh-01.log`.
Backup: `Logs/engine-repair-before-20260905-133537-3772919/`.
Hash comparison shows all 117 backed-up mount definitions unchanged; only the
three existing bolt marker transforms changed in the generated prefab.
Prefab SHA256:
`2181F8CC901998A65C4C28DC6319F8FB907803D7884EB888BD414DFA4FE522E2`.

Runtime changes: `AssemblyLooseCompoundPhysics.cs` and
`AssemblyCarburetorThrottleTarget.cs`. Authoring:
`Phase1SatsumaEngineDockingAuthoring.cs`. New regression files:
`Tests/EditMode/LegacyImport/SatsumaEngineCompoundMotionTests.cs`,
`SatsumaEngineDockingPoseTests.cs`,
`Tests/PlayMode/VehicleAssembly/SatsumaEngineCompoundMotionPlayModeTests.cs`,
`Tests/PlayMode/PlayerInteraction/SatsumaCarburetorThrottleInteractionPlayModeTests.cs`
(all below `Assets/Game/`, with their own metadata).
Additional detailed evidence: `SATSUMA_ENGINE_DOCKING_BOLT_FRAME_FIX_2026-09-05.md`,
`SATSUMA_ENGINE_COMPOUND_MOTION_REGRESSION_2026-09-05.md` and the throttle
follow-up in `SATSUMA_ENGINE_ADJUSTMENTS_2026-09-05.md`.

No runtime/save schema, stable-ID or accepted player/suspension migration. No
native save overwrite, donor write, full world rebuild, standalone build,
commit or push. Generated payload remains ignored and recoverable from backup.

## Manual next step

Restart Play/load the existing save to instantiate the refreshed wrapper.
With the approved heavy-carry debug override if needed, place the complete
engine on its supports: three 11 mm bolts should be at the subframe mounts,
not above the car. Physical settling before fastening is allowed; continuous
large wandering is not accepted. Bolt through the existing ON2 threshold and
check that it follows the chassis; loosen fully to release in place. With no
tool selected, hold LMB on the carburetor throttle linkage, then release it.
It should move out/back; this does not magically start an incomplete engine.

Exactly one next validation milestone: user repetition of these three reported
in-game cases. Broader hoist/engine-simulation work remains outside this repair.
