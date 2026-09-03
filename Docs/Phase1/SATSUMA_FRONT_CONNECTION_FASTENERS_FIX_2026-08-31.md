# Satsuma — front connection fasteners, issue 3

Follow-up: `SATSUMA_FRONT_FASTENER_MESH_AND_ROD_OFFSET_2026-08-31.md`
records V1d.35's donor bolt/nut correction, the missing 50 mm rod-end parent
offset and the separate, still incomplete physical steering-connection state.

Revision: `11A-V1d.34`, 2026-08-31. Build, EditMode, new front-fastener
PlayMode and isolated Bootstrap passed. Existing rear-lift regression test
still fails; issue-3 manual acceptance pending. Scope is only the missing front
strut/steering-rod fastening.
The user accepted the previous front-contact behavior and reported that the
one-sided front-suspension nose lift no longer reproduces. No suspension forces,
mass/centre of mass, wheel seating, rear physics or assembly prerequisites are
changed here. Do not start issue 4 before the user's acceptance of issue 3.

## Frozen donor evidence

Authority: `msc-world-baseline-04a1.1-c3f2f337`. Source relative path:
`raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`.
SHA-256: `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
The original installation and frozen export are read-only. No new donor-runtime
capture was executed in this pass; evidence is the serialized scene/FSM data.

| Connection, per side | Donor | V1d.33 problem | V1d.34 |
|---|---|---|---|
| Strut top to body | 3 x 10 mm | Present | Existing IDs and body-fixed pose retained |
| Strut bottom to spindle | 4 x 9 mm | Entirely absent | Four additive IDs; moving lower-strut frame |
| Steering rod outer joint | 1 x 12 mm | Wrong 14 mm alignment marker used | Correct source, size and lower-strut frame; existing ID retained |
| Toe adjustment | 14 mm, Alignment state | Misrepresented as the joint bolt | Not treated as a tightening fastener; actual alignment feature is outside this fix |

Each actual fastening Screw has discrete Stage 0..8. Donor strut data owns seven
fasteners; rod data owns one. PLAYER/Raycast Check FSM `105041`, GAME line
2359086, reads the bolt's local X scale and compares with ToolWrenchSize
(tolerance 0.02): scales 0.9/1.0/1.2 correspond to 9/10/12 mm. This is not a
guess from apparent head diameter.

Important source records (GAME line numbers, not runtime IDs):

- Strut Assembly `107329` FL / `108451` FR, lines 3914017 / 4672095:
  Assemble enables both `ActivateThis` and `ActivateSpindleBolts`.
  The second reference is lower-bolt GO `14001` / `34573`, Transform
  `50059` / `70627`, outside the strut's own presentation subtree.
- Lower marker transforms in stable ascending order: FL
  `39160,49488,64879,65179`; FR `38251,51736,65041,70471`.
  Their Screw PartAssembled references the corresponding strut.
- Actual rod joint: FL GO `32885`, Transform `68946`, Screw `113544`
  (line 7945927); FR GO `34430`, Transform `70485`, Screw `113958`
  (line 8213738). PartAssembled references rod GO `24431` / `2810`.
- Rod Assembly `111295` / `110868`, lines 6475043 / 6216438, enables
  `Bolt` (the real 12 mm fastening) and `ActivateBolt` (the separate adjuster).
  Use `111005` / `104922` disables them on removal.
- Adjuster Screw `112113` / `105829`, lines 7004979 / 2875193:
  no Stage/intVariables; reads/writes `Data.Alignment`. This cannot be mapped
  onto a FastenerInstance merely because its object resembles a bolt.
- Moving reference frames: `pivot_shock_fl` Transform `64686` (line 956222),
  `pivot_shock_fr` Transform `44833` (line 694931). Both lower bolt sets and the
  real rod joint are measured relative to these frames. The builder reads their
  transforms directly from the locked scene rather than flattening their pose
  relative to the stationary strut top.

Lower bolts appear with the strut; rod joint appears with the rod. The donor
does not require an installed strut to show the rod joint. Existing remake
installation prerequisites remain unchanged; broader graph differences are
not silently corrected by this presentation/fastener slice.

## Cause and correction

Generic extraction follows ActivateThis/activation-root descendants. It omitted
the separately activated spindle bolt group. Earlier documentation claimed
coverage, but the actual V1d.33 prefab contained only three upper strut bolts.
The existing tests counted total targets and checked the wrong rod marker's
parent; they did not test the missing seven-bolt strut contract.

`Phase1SatsumaBaselineBuilder` now adds the four source-validated lower markers
per strut while retaining `.boltpm-1..3` for the upper fasteners. New IDs are
`fastener.satsuma.strut-{fl,fr}.lower-1..4` in the source order above.
`fastener.satsuma.steering-rod-{fl,fr}.outer-joint` keeps its identity and stages
but now uses the actual 12 mm source. Lower markers and joint attach to the
existing `ShockBottomTarget`; top markers stay at the strut mount. No new
per-frame follower, vendor edit or physics authority is introduced.

The generated graph changes from 252 to 260 fasteners. Parts stay 126 including
the shell; mounts stay 117. Existing fastener-group removal semantics remain:
strut maximum aggregate is now 56 rather than 24; compatibility latch threshold
is still 1/0. This pass does not claim full donor loose-strut driving/failure
calibration.

## Save compatibility

Schema and all pre-existing IDs remain unchanged. The existing additive
migration now recognizes exactly the previous 126-part/117-mount/252-fastener
shape when upgrading to the 260-fastener graph. It verifies that the absent
records are precisely the eight new lower bolts, not arbitrary truncated data.

All 252 previous stages/inserted/seated flags and group latches are preserved.
New lower bolts are inserted and seated at Stage 0 if their strut is installed;
otherwise they remain absent. No automatic tightening. A 251-record payload or
a 252-record payload missing an unrelated old fastener fails without changing
live state. Historical pre-V1d.33 migration policies are unchanged.

## Checks and manual handoff

Executed:

- Baseline build: exit 0, `PHASE1_SATSUMA_BUILD_OK version=11A-V1d.34`,
  260 fasteners, 117 mounts, 125 loose parts, 120 active loose parts.
- Initial focused EditMode: 38/38 passed.
- Final EditMode: **104/104 passed**: generated content 28, new front save
  migration 5, existing rear/wheel BoltCheck parity 5, assembly 23, NWH 30,
  native-save integration 13. Includes the explicit Stage/PartAssembled source
  guard: numeric events on the alignment adjuster must not count as stages.
- Isolated physics PlayMode: **19/20 passed**: new front-fastener 2/2 and
  existing installed-part physics 17/18. The pre-existing
  `RearSpringsLiftTheChassisThroughGroundedDrums` failure reproduces:
  before 0.292074 m, after 0.303044 m, lift 10.97 mm versus required >20 mm.
  Neither its assertion nor rear tuning was changed. All front tests passed.
- Isolated production Bootstrap: **1/1 passed**, including 260 live targets.
- The first combined Bootstrap+physics run did not complete after loading
  Bootstrap; only the owned batch process was stopped. No result from that
  run is counted as a pass. Separate runs above completed. A test-lifecycle
  interaction is suspected, not established as a production defect.
- Scoped source whitespace checks passed. The overall dirty worktree has
  unrelated pre-existing Bootstrap YAML whitespace; it was not reformatted.

Artifacts under `Logs/`: `codex-front-fasteners-v1d34-build.log`,
`codex-front-fasteners-v1d34-editmode.xml`,
`codex-front-fasteners-v1d34-integration.xml`,
`codex-front-fasteners-v1d34-physics.xml`,
`codex-front-fasteners-v1d34-bootstrap.xml`. The incomplete combined run is
`codex-front-fasteners-v1d34-playmode.log`.

Commands executed through hidden Unity batch processes: `-executeMethod
MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaBaselineBuilder.Build`,
then `-runTests -testPlatform EditMode` / `PlayMode` with the named fixture
filters. The editor version is 6000.3.11f1. No interactive user Editor was
closed and no tests are claimed to have rendered a visual comparison.

Changed project-owned sources/tests (plus .meta files for new tests):

- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaBaselineBuilder.cs`
- `Assets/Game/Vehicle/Assembly/Runtime/VehicleAssemblyController.cs`
- `Assets/Game/Tests/EditMode/LegacyImport/Phase1SatsumaGeneratedContentTests.cs`
- `Assets/Game/Tests/EditMode/LegacyImport/SatsumaFrontFastenerEditModeTests.cs` (new)
- `Assets/Game/Tests/PlayMode/VehiclePhysics/SatsumaFrontFastenerPlayModeTests.cs` (new)
- `Assets/Game/Tests/EditMode/SaveIntegration/CurrentDomainSaveIntegrationTests.cs` (count)
- `Assets/Game/Tests/PlayMode/VehicleAssembly/ProductionSatsumaBootstrapPlayModeTests.cs` (counts)

The ignored canonical generated Satsuma assets were rebuilt. This report, the
historical V1d.28 claim correction, parity rows P1.CAR.003/P1.CAR.008 and porting
ledger record the provenance and limits. No accepted 00–08A public API, stable
ID, scene, save schema or presentation layout is replaced.

Added test coverage:

- direct frozen-scene position/rotation/scale comparison for ten moving markers;
- exact 3x10 + 4x9 and 1x12 coverage on both sides;
- live ground compression, steering-frame movement, fixed upper mounts,
  renderer availability and Collider.Raycast reachability;
- tighten/loosen, wrong wrench rejection, detach/reinstall and stage reset;
- old-save migration, current-save roundtrip and corrupted-payload rejection.

Manual test after a normal scene/game reload with the regenerated prefab:

1. Assemble one front side through strut and steering rod, then the other.
2. Confirm four lower bolts accept 9 mm, three top bolts accept 10 mm, and the
   rod's outer joint accepts 12 mm (not 14 mm).
3. Raise/lower the chassis and steer: lower bolts must stay seated at the
   moving connection, while top bolts stay on the body tower.
4. Tighten and loosen, remove/reinstall the strut and rod. No floating,
   duplicated or permanently missing bolt targets.
5. Reload an existing save: prior tightening survives; newly added lower bolts
   start loose. Tighten them manually if the strut was already installed.

### Small handoff specification for another session

Stay on issue 3 until manual acceptance. If a bolt remains missing, identify
its stable ID, installed mount state and marker world pose before changing
counts or forces. Do not restore the 14 mm toe adjuster as a joint bolt. Run
the front-fastener tests and preserve the eight-ID save migration. After the
user accepts issue 3, the next sequential item is issue 4 (rear droop/shock
separation), with the previous rear-spring lift test result still explicitly
unresolved. Do not mix wheel seating or rear fixes into this patch.
