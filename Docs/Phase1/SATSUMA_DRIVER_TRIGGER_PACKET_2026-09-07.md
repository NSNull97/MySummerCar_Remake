# Satsuma — interior driver-trigger / first drive packet

Status: **Interior-trigger/manual-drive implementation complete; scoped automated
coverage executed; human acceptance pending. Whole-project regression gate is
not green; AutoClutch remains an explicit missing parity feature.**

## Authorized scope and plan

The user approved the proposed bounded 11A-V1 first-drive packet on 2026-09-07,
explicitly requiring a driver-place trigger **inside** the car: the player
enters the cabin physically, then the trigger fixes the player at the driver
position and transfers control. No outside-door boarding or automatic exterior
teleport is authorized. Exiting driving mode releases the player in the cabin.

1. Inspect the locked donor trigger, anchoring, activation/exit controls and
   existing player/input/save/vehicle composition. Record measured evidence.
2. Add an explicit, project-owned interior driving session, retaining existing
   player look/cockpit interactions, physical ignition and simulation boundaries.
   Walking and driving must never consume the same movement intent concurrently.
3. Bind the canonical trigger/eye pose and driving input. Reuse the existing
   clutch, gearbox, hydraulic brakes, wheel backend and stock instruments;
   repair only integration defects required for the bounded first-drive flow.
4. Verify activation bounds, release, pause/menu, reload, missing hardware,
   transient-input reset, actual input and the native simulation/physics route.
   Preserve user saves and donor installation. No hidden fuel/repair bypass.
5. Refresh only scoped generated content; record exact tests, limitations,
   controls and the remaining human acceptance checklist.

## Compatibility boundaries

- Preserve accepted 00–08A foundations, current Satsuma state/IDs, suspension,
  physical ignition, handheld interaction, existing UI and the accepted visual/
  audio packet. Add adapters rather than replacing them.
- No full-body player, physical hands, animated entry/exit, broad physics tuning,
  aftermarket instruments, unrelated lighting, new dependencies or Phase 2.
- Petrol refuelling was explicitly excluded from the preceding engine packet.
  If a confirmed fuel-flow gap blocks this route, report it separately; do not
  hide it behind development-only fuel availability.
- The existing driver-seat item is an assembly part, not driving-session
  authority. Donor object identities/names are provenance only, never runtime
  lookup keys. All raw donor inspection stays external or tool output only.
- Save/load behavior and pause/input ownership require executed coverage;
  automated evidence below does not promote broad donor parity rows.

## Frozen evidence and bounded integration design

Read-only `GAME.unity`, SHA256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`:

- DriveTrigger GO1542 / Transform37601 / Capsule100498 / FSM104549 at byte
  51570412. Parent Transform41422 has position `(0,.053279385,-.16712156)`
  and Y scale `.91168696`; child position `(-.282,.254,.1)`; capsule center
  `(0,-.25,0)`, radius `.03`, height `.3`, Z direction. The sanitized equivalent
  capsule center in car coordinates is `(-.282,.05692613284,-.06712156)`.
- Enabled states: trigger enter/stay -> `Press return`; `DrivingMode` button
  -> `Check seat`, which reads **Installed**, not Bolted, from regular/racing
  seat databases. The existing stock seat is the supported first-drive variant.
- Active driving assigns pedal/steering/shift inputs; `Wait for player` clears
  them. The separate PLAYER `Stopping` FSM107677 disables CharacterMotor,
  CharacterController and FPS movement, without destroying the player.
- `Create player` clears PlayerStop and detaches the player without resetting
  local position, then removes roll/pitch. It is not an exterior exit teleport.
- `Check speed` compares the absolute world velocity components with `.15 m/s`.
  Donor seatbelt conditions are also present, but seatbelt gameplay is not
  implemented by this bounded packet and is not claimed complete.
- DriverHeadPivot Transform48821 position `(-.3000002,.5608089,.02000036)` is the
  spatial reference for the new eye anchor. The old physical head joint and
  nested camera offsets are not imported; the new +Z-facing, look-enabled camera
  anchoring is `Reimplemented`, subject to real prefab visual verification.

Integration is additive: Player keeps its existing look/interact actions while
a new locomotion gate suppresses walking/posture/jump intent. The car router
gets opt-in driver-session permission separate from pause permission; old M06
hosts retain their path. Physical ignition overrides still own key/starter;
prototype reset/ignition hotkeys are disabled only for a managed driver session.
Entering/resuming driving requires neutral controls before pedal/shift sampling.

Save compatibility: optional presence-tagged schema-1 driving data within the
existing player schema stores project-owned station ID, relative yaw and the
in-cabin release pose/posture. Absent data means walking. Player restore occurs
after vehicle assembly/simulation; missing station binding or malformed new data
fails preflight, not silent loss. No persistent input, donor identity, renamed
field, top-level schema bump or modification of the existing user slot. The
new Player/Bootstrap interface is the explicit cross-module persistence boundary;
the Player assembly does not reference Vehicle or Bootstrap.

## Implemented behavior

- `SatsumaDriverStation` exposes the explicit stock station and measured capsule;
  `SatsumaDrivingSessionController` binds the existing player, camera, input,
  motor, interaction controller and canonical vehicle. Enter works only from
  inside the capsule with the installed stock seat and no held item/tool action.
- Enter fixes the existing camera at the car-local eye anchor, disables walking
  and the CharacterController, retains mouse look and physical cockpit use,
  and exclusively enables the existing M06 pedal/steering/shift action map.
  Input has a real Input System initial-update/neutral guard: held walking W
  cannot become accidental throttle. Prototype I/Enter/Backspace car commands
  are suppressed in this managed mode; physical key/starter remains authority.
- A stopped Enter releases the player at the original in-cabin local feet pose
  in the car's **current** frame. Seat loss/disabled session forces safe release.
  No exterior teleport, animation, player parenting or full physical body.
- Pause permission is independent from occupancy. Both gate-restore orders,
  timeScale-only pause, saved driving/walking state and transient input clearing
  have explicit tests. Invalid station/owner/seat state fails before mutation.
- `SatsumaCockpitSteeringPresenter` applies raw steering times 450 degrees to
  ten explicitly bound render leaves on the column, stock/GT wheel and shared
  nut. Frozen Transform67214 relative to car Transform64200 yields pivot
  `(-.2194313,-.1529065,.9609475)` and axis
  `(-.0391918,.5996105,-.7993321)`. Physics roots, mount coordinates and colliders
  never rotate for this presentation. Offsets reset before the next assembly
  frame and on disable; detached parts are not driven.
- Scoped authoring adds/validates these bindings on the existing generated
  canonical prefab. Full baseline-builder hooks preserve deterministic rebuilds;
  no full rebuild, map replacement or production reauthoring was performed.

### Compatible repeated-restore correction

The native transaction tests exposed stale purchased-part compound bindings:
`TrySetDynamicPartRegistrationsForRestore` removed old parts from the assembly
graph but retained their runtime compound bindings. Re-registering a restored
purchased part then failed the compound's complete validation. The existing
`TryUnregisterRuntimeBinding` is now called for those old dynamic parts after
detach/release and before the roster replacement. It removes only runtime
proxies and restores their authored body state; existing item restoration
rebuilds the bindings. Native same-car reload and injected late-failure rollback
exercise this path. No public API removal, stable-ID change, schema migration,
base-roster rewrite or replacement of the accepted save/physics architecture.

## Real route and visual evidence

The route is a real Unity PlayMode run: native canonical assembly, nine saved
purchases, original M4/M06 InputAction JSON, virtual keyboard through Input
System, physical ignition adapter, existing hydraulic model and actual NWH wheel
contacts on a flat test pad. No imposed car motion, wheel velocity, drive torque,
fuel flag or auto-repair. This is not a donor-road or human-drive comparison.

The live source slot changed externally during the work. A read-only snapshot
was frozen at `Logs/driver-native-source-20260907-0843.json`, SHA256
`484F554BEF1CE77AC6797D40E8F6C30A5279DC6BBFF2A0EDFCF78069FC62CDA1`.
It already contains a running engine, 30 L petrol and front/rear/clutch fluids
1/1/.5 L; the route uses these values as-is. Tests never write the native slot.
Earlier observations of the older live slot's empty hydraulic circuits do not
describe this frozen input. Cold-start-from-empty/refuelling is not this route.

The executed flow: enter inside -> neutral guard -> clutch/first -> actual
forward movement -> clutch/second -> front-wheel and visible-wheel steering ->
hydraulic stop -> second/first/neutral/reverse -> reverse movement -> stop ->
physical handbrake hold contract -> release clutch against service brake and
stall -> release player inside -> paused four-domain native roundtrip -> still
walking, still parked, all nine purchases retained, stalled engine not revived.
The handbrake uses the real lever API here; mouse targeting has separate tests.

Final route run `Logs/driver-final-route-play-v3.xml`: **6/6 passed**, zero failed
or skipped (one native route plus five session/input flows). Observed forward
travel 16.08531 m, speed 8.653858 m/s; second-gear speed 10.70169 m/s; service
brake stop .001651721 m/s with 1800 Nm aggregate brake torque; reverse travel
-3.018248 m; loaded stall RPM 0, gear -1, clutch pedal 0, brake 1. These are
observations from this run, not cross-machine deterministic tolerances.

HDRP captures from the actual native car and existing player-camera settings are
`Logs/driver-station-20260907/driver-forward.png`, `driver-dashboard.png` and
`driver-steered.png` (1600x1000). Forward and steered captures were visually
inspected: eyes inside the cabin, visible windshield horizon, stock wheel and
instruments, plausible rotation around the measured steering center. Controlled
lighting is test-only, not a replacement for Enviro or a full-world visual gate.

### Intermediate failures retained as evidence

- Input tests caught an actual same-frame neutral-guard race after enabling the
  action map; waiting for a real Input System update fixed that integration bug.
- Creating virtual devices before batch focus routing disabled the test keyboard;
  fixture setup now sets routing first. Restoring before native joint activation
  was also a fixture-order error; the fixture now matches active production load.
- The first combined Bootstrap/route batch left persistent Bootstrap scenes and
  its item provider in the test world. Bootstrap and native route are therefore
  executed in separate Unity processes; no unrelated teardown rewrite.
- A steering assertion sampled before LateUpdate and was corrected to inspect
  the completed visual frame. A first stall scenario relied on the rear-only
  parking brake of a front-wheel-drive car; the verified scenario holds the
  hydraulic service brake on the driven wheels while releasing the clutch.
  These failures are not passed tests or evidence of complete physics parity.

## User acceptance and remaining boundaries

1. Open this checkout only with Unity 6000.6.0f1. Launch the existing Bootstrap
   play flow with the prepared native car; no additional authoring menu is needed.
2. Enter the cabin physically and move to the installed stock driver seat.
   Enter outside must do nothing; the existing context hint inside offers Enter.
3. Press Enter and release movement keys once. Mouse look and physical cockpit
   controls remain. W gas, S service brake, A/D steer, left Shift clutch,
   E gear up, Q gear down. Use the existing physical ignition and handbrake.
4. Drive, stop, set the handbrake and select neutral before releasing the clutch
   for normal parking. Enter releases walking **inside** the cabin. For the
   deliberate stall check, keep a gear selected, hold S and release Shift.
5. Check pause/resume and a native save/load both seated and walking. Human
   camera/feel/actual-road acceptance is still required; it is not inferred from
   automated input or screenshots.

Stock seat only; racing-seat entry and donor seatbelt gating remain outside this
bounded packet. Existing gearbox selection is not a complete donor grinding/
damage model. **AutoClutch is not implemented in the current driver input path**,
not merely disabled: this packet uses manual left-Shift clutch. The user raised
this gap on 2026-09-07 during final verification. Donor AutoClutch is evidenced
by the frozen vehicle-control menu and Starter106807 `Check clutch`/StartHelp
branch already recorded in `SATSUMA_STARTER_FUEL_IGNITION_GATES_AUDIT_2026-09-06.md`.
Complete assistant behavior still needs a bounded donor audit and implementation;
automatic clutch must not be confused with automatic gear selection. This is an
open Phase 1 parity gap, not an approved permanent difference or a completed
feature. No force-feedback, steering calibration claim, performance gate,
new sound mix or UI redesign. Existing engine audio/instruments are reused.
No standalone executable was rebuilt. Private temporary presentation remains
`TemporaryDirectImport`, not `ProductionReady`. CAR parity rows remain open.

Exactly one next milestone: **11A-V1 first-drive human acceptance** of this
interior-trigger packet on the existing native car; not Phase 2.

## Changed-file index

New project-owned runtime files (plus Unity `.meta` files):

- `Assets/Game/Vehicle/Runtime/SatsumaDriverStation.cs`
- `Assets/Game/Vehicle/Runtime/SatsumaCockpitSteeringPresenter.cs`
- `Assets/Game/Player/Runtime/IPlayerContextActionSource.cs`
- `Assets/Game/Player/Runtime/PlayerDrivingSaveDto.cs`
- `Assets/Game/Bootstrap/SatsumaDrivingSessionController.cs`
- `Assets/Game/Bootstrap/SatsumaDrivingSessionController.Persistence.cs`

Existing runtime/input integration extended, not replaced:

- `Assets/Game/Player/Runtime/PlayerInputRouter.cs`
- `Assets/Game/Player/Runtime/PlayerInteractionController.cs`
- `Assets/Game/Player/Runtime/InteractionActionPresentation.cs`
- `Assets/Game/Player/Runtime/PlayerPersistenceState.cs`
- `Assets/Game/Vehicle/Runtime/VehicleInputRouter.cs`
- `Assets/Game/Vehicle/Assembly/Runtime/VehicleAssemblyController.DynamicParts.cs`
- `Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs`
- `Assets/Game/Save/Integration/EnvironmentAndPlayerSaveParticipants.cs`
- Existing `M4_Player.inputactions`: additive DrivingMode/Enter action and binding.

New Editor/test files (plus `.meta` files):

- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaDriverStationAuthoring.cs`
- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaCockpitSteeringAuthoring.cs`
- `Assets/Game/Tests/EditMode/VehicleSimulation/SatsumaDriverStationTests.cs`
- `Assets/Game/Tests/EditMode/VehicleSimulation/SatsumaCockpitSteeringTests.cs`
- `Assets/Game/Tests/EditMode/SaveIntegration/SatsumaDriverNativeSaveTests.cs`
- `Assets/Game/Tests/EditMode/SaveIntegration/SatsumaDriverNativeDriveTests.cs`
- `Assets/Game/Tests/EditMode/SaveIntegration/SatsumaDriverNativeGraphicsTests.cs`
- `Assets/Game/Tests/PlayMode/VehicleAssembly/SatsumaDrivingSessionPlayModeTests.cs`
- `Assets/Game/Tests/PlayMode/VehicleAssembly/SatsumaNativeDriverPlayModeTests.cs`

Existing `Phase1SatsumaBaselineBuilder.cs` calls both scoped authoring hooks.
Existing `CanonicalConsumableNativeSaveTests.cs` exposes its one restore plan to
the extended fixture; `MSC.Save.Integration.Tests.EditMode.asmdef` adds test-only
references. `ProductionSatsumaBootstrapPlayModeTests.cs` checks real binding.
The native route fixture is shared through an Editor-only test entry point;
standalone test-player execution intentionally skips that Editor fixture.

`Tools/DonorPipeline/Inspect-UnitySceneHierarchy.py` is new read-only hierarchy
inspection; `Read-PlayMakerActionParameters.ps1` gains optional object-ID reading
without changing existing defaults. This report, the execution plan, donor
audit, porting matrix, ledger and system map record the work; the preceding
visual/audio report records the user's earlier acceptance. Scoped generated
canonical prefab changes and all native/log/capture payloads remain ignored.
Pre-existing unrelated dirty work was preserved; no commit was created.

## Reproduction commands

Use the installed Unity **6000.6.0f1** executable and this configured checkout.
The runs use `-batchmode -projectPath <checkout> -force-d3d11 -runTests`, with
the following additional arguments (paths are checkout-relative below):

- Route: `-testPlatform PlayMode -testFilter SatsumaNativeDriverPlayModeTests;SatsumaDrivingSessionPlayModeTests`
  and `-liveEngineSavePath <checkout>/Logs/driver-native-source-20260907-0843.json`;
  results/log `Logs/driver-final-route-play-v3.xml` / `.log`.
- Edit regression: `-testPlatform EditMode -assemblyNames MSC.Tests.EditMode;MSC.Save.Integration.Tests.EditMode;MSC.Save.Tests.EditMode;MSC.UI.Runtime.Tests.EditMode;MSC.UI.Editor.Tests.EditMode;MSC.Vehicle.NWH.Tests.EditMode`
  with the same explicit native snapshot; results/log `Logs/driver-final-edit.xml` / `.log`.
- Cockpit/assembly regression: `-testPlatform PlayMode -testFilter SatsumaCabinControlFramePlayModeTests;SatsumaIgnitionPlayModeTests;SatsumaHandbrakePlayModeTests;AssemblyDynamicRestoreHandoffPlayModeTests;VehicleAssemblyPlayModeTests`;
  results/log `Logs/driver-final-legacy-play.xml` / `.log`.
- Production binding in an isolated process: `-testPlatform PlayMode -testFilter ProductionSatsumaBootstrapPlayModeTests`;
  results/log `Logs/driver-final-bootstrap-play.xml` / `.log`.

Set `-testResults` and `-logFile` to the full respective paths. Execute Unity
jobs serially on this checkout, not concurrently. Source and snapshot hashes
are read-only checks. `git diff --check -- <owned tracked paths>` was executed
without whitespace errors; Git's existing CRLF-normalization warning is not a
compile or test result. No distributable build command was run.

## Final regression results and unresolved findings

| Run | Passed | Failed | Skipped | Meaning |
| --- | ---: | ---: | ---: | --- |
| `driver-final-route-play-v3.xml` | 6 | 0 | 0 | Real native route and five input/session flows. |
| `driver-final-edit.xml` | 1990 | 22 | 1 | Broad six-assembly regression; **not a green project gate**. |
| `driver-final-legacy-play.xml` | 24 | 0 | 0 | Cockpit control frame, ignition, handbrake, assembly and dynamic-restore handoff. |
| `driver-final-bootstrap-play.xml` | 1 | 0 | 0 | Actual production Bootstrap binds the canonical station, player session, managed input and steering presenter. |
| `driver-final-isolated-edit.xml` | 15 | 1 | 0 | Fourteen station/steering cases repeated plus two suspicious interaction cases; only existing bulb contextual-name coverage fails. |

Within the broad EditMode run, all **18** driver-station/steering/new native
save/preflight/rollback/graphics cases passed, including both canonical authoring
idempotence checks (`ApplyToInstance == 0`). Save Core passed 31/31, UI Editor
27/27, UI Runtime 27/27 and NWH EditMode 63/63. Save Integration passed 170/172.
The opt-in legacy `-engineSavePath` snapshot test was intentionally skipped:
that different old-save contract was not supplied or weakened.

The 22 broad failures are retained in the XML, grouped here without silently
rewriting unrelated accepted content or weakening old fixtures:

- **2 old native-source contracts:** one expects an unfastened source before a
  servicing test; one expects a 302-part source roster instead of the selected
  snapshot's 294. The prepared nine-purchase snapshot deliberately does not meet
  those older immutable-input assumptions. The new native route/save tests use
  its actual current contents and passed. No source slot was edited to satisfy
  either fixture.
- **3 Foundation/Garage lighting expectations:** ACES/Neutral and old fixed
  exposure values differ from the current content.
- **2 generated Satsuma paint expectations:** NoChange versus current Metallic.
- **2 player-interaction cases:** missing RU/EN contextual title for the existing
  headlight bulb, and nested authored-control ray selection (isolated rerun
  recorded separately). They are not driver-station tests.
- **1 full-game save-coverage record:** key-access contract registration checklist
  remains incomplete.
- **12 world baseline/transfer/remaster checks:** sanitation allowlist, frozen
  cellization/manifests/material mappings, one spruce colour render, pilot gate,
  and two recorded donor-source hashes reported by one transfer validation.

These failures concern fixtures/content outside this packet's new driver path.
There was no pristine pre-change whole-project A/B run; **do not infer that all
22 are proven pre-existing**, or that this packet clears the full 00–08A/Phase 1
regression gate. No broad repair, donor mutation, art rollback, test deletion or
expected-value rewrite was made to turn this batch green. Existing empty-assembly
and old imported Mesh-version warnings remain; there were no C# compile errors.

The isolated EditMode filter was
`PlayerInteractionRuntimeTests.RaycastQuery_PrefersNestedAuthoredControlWithoutSeeingThroughWalls;PlayerInteractionRuntimeTests.ContextTargetNameCatalog_CoversItemsServicesAndSatsumaParts;SatsumaDriverStationTests;SatsumaCockpitSteeringTests`.
The ray-selection case **passed** in the fresh process without any code change;
its broad-run failure is order/environment sensitive and not reproduced in
isolation. That is not a complete root-cause diagnosis of the broad-run failure.
The bulb-name case still fails and was left unchanged. The final snapshot hash
was rechecked byte-identical; all owned Unity test processes completed. Both
scoped authoring idempotence checks passed again in this final isolated run.
