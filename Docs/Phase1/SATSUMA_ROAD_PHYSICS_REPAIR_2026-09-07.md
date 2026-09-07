# Satsuma road-physics repair — 2026-09-07

Status: **Paused at the user's request after a manual micro-drive on 2026-09-07.
The user reports the repaired car is generally usable, but rejects its overly
grippy/toy-like handling. Preserve the current repairs; do not claim donor
handling parity or a fully green performance/regression gate.**

Current handoff and next-session entry point:
`Docs/Phase1/SATSUMA_HANDLING_HANDOFF_2026-09-07.md`.

## Final checkpoint before pause

The latest `Logs/road-physics-bounded-edge-dense2.xml` reports **2/2 Passed**.
Its rail crossing trace includes 60.0012 -> 55.06898 km/h, max tilt 5.442945
degrees and hub error .0003860224 m. The solid-wall guard reports hit=True,
furthest travel 8.127584 m and final speed 1.483861 km/h. These are bounded
coast/contact checks, not evidence of realistic cornering, wheelspin or traction.

The earlier `Logs/road-physics-final-play.xml` reports **40/41 Passed**, with
`NativeRoadPhysicsAuditUsesActualRoadColliders` failing before the bounded-edge
revision. The later focused pass addresses the road case, but does not count
as a rerun of all 41 tests against the final code. Populated render smoke and
post-optimization performance verification remain open as described below.

User feedback, including the later clarification: the car DOES leave the
ground. Both the car and the player seem not to distinguish dirt/rough terrain
from asphalt. The car grips these surfaces excessively and does not exhibit
convincing skids or wheelspin. Parts twist out of their installed positions
instead of detaching as in the original; the cause is not yet diagnosed.
A further user report adds doors opening spontaneously while driving. Latch
state, hinge physics and any relationship to part distortion remain unverified;
recorded for the next session only, without further runtime changes.
Abrupt snagging, stopping and rollover at some terrain features
remain possible; these must be distinguished from legitimate collision risk.
The user explicitly requested documentation only and no further fixes in this
session. Earlier in-progress narrative below is retained as diagnostic history,
not as a newer instruction to continue running tests or editing runtime code.

## Authorization and preserved baseline

On 2026-09-07 the user explicitly requested fixing the reported physics bugs
before another drive. The preceding interior-driver/manual-drive packet remains
the integration baseline, not a completed road-handling acceptance gate.
Preserve its interior-only entry/release, player/input/save compatibility,
physical ignition, native purchased parts and accepted assembly/fastener rules.
Preserve accepted rear spring travel/force ownership and wheel seating. Do not
rerun historical suspension milestones or replace NWH with donor runtime code.

The six reported symptoms, after the user released the handbrake:

1. A chassis-collider group's selection pivot appears away from/below the car.
2. Wheel/hub/spring presentation moves into the cabin while driving.
3. Dirt-road contact produces repeated body-impact sounds and rollovers above
   approximately 50 km/h.
4. Small obstacles, specifically railway rails, nearly stop the car.
5. Highway driving around 100–110 km/h reduces observed FPS from 60 to 10.
6. Highway driving can also roll the car over.

The screenshot does not by itself distinguish a pivot offset from a collider
or Rigidbody pose error. FPS symptoms are not yet attributed to CPU physics,
rendering, Scene View overhead or streaming without a capture. No guarantee of
an unflippable car or arbitrary kerb-climbing behavior is authorized.

## Plan and verification gates

1. Freeze/reuse the read-only native snapshot and capture the current car on
   representative actual road geometry. Measure authoritative body, collider,
   mount and wheel poses before modifying them; release the handbrake explicitly.
2. Add a bounded development external-camera/telemetry view using existing
   player/camera/input ownership. It must not change physical state, redesign
   the accepted HUD or persist a new gameplay/save dependency.
3. Reproduce and fix confirmed coordinate/interpolation/physics-owner defects.
   Record a dependency audit before any incompatible foundation change; prefer
   compatible extensions and retain existing public/serialized identities.
4. Compare wheel/road/chassis contacts at rail crossings and dirt/highway
   sections. Separate actual body impact from ordinary wheel contact and scrape.
   Reimplement the evidenced donor collision-event constraints through current
   project-owned categories and IAudioBackend, not donor layer-number lookups.
5. Capture speed-banded performance and identify hot work before optimizing.
   Recheck low-speed stability, motion at highway speed and camera-frame wheel
   alignment. Do not fix FPS by silently reducing required world content.
6. Run targeted assembly, suspension, steering, driver/input, save/rollback and
   audio regressions plus the reproduced road cases. Record results and remaining
   manual route acceptance honestly; do not relabel the earlier flat-pad run as
   coverage of these failures. No broad unrelated content/test repair.

## Read-only evidence available before implementation

Frozen `GAME.unity` SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Satsuma root GO28145, Transform64200, body91271, CarDynamics112090.

- Donor `Wheel.cs` SHA256
  `85ffbf994222e04ac61a32bebc9cdeae9c7efe53df7d7bf75910ea80bc051945`:
  model motion is local to its carrier (lines 748–767), compression is bounded
  to travel (843–846), forces act on the chassis (913), with spring/damper and
  travel-stop processing. Installed geometry is not a competing free suspension.
- Satsuma `EventSounds112085` is enabled, collision event `Crashes`, volume .4,
  time-based retrigger limit 1.5 seconds and other-object layers 0/8/10/26
  (Default/Road/Terrain/Forest in the frozen donor layer table). EventSounds
  source SHA256 `e8c4f25ba1a2e5d7dd6343c0e72c4c820744c5d1752c8b49e89591fdbac08f8d`;
  collision filter at line 491, time gate at 827–860.
- Generic donor `SoundController` contains directional impact attenuation and
  a Wheel-layer exclusion, but Satsuma component112089 has empty crash-clip
  references in the frozen scene. Do not mistake those generic crash branches
  for the configured Satsuma Crashes event. Its scrape binding is populated.
- The current body-impact presenter has no corresponding collision filter and
  a .08-second cooldown. This is a confirmed behavior mismatch, not yet a repair.
- Front presentation reads NWH's cached world wheel position in both fixed/late
  passes; moving-body interpolation alignment requires a runtime reproduction.
- Assembly/front/rear controllers perform at least four full installed-part
  synchronization passes per fixed step and four per late frame. These write
  Rigidbody poses as well as Transforms. Their measured cost, not their mere
  existence, must determine optimization.

## Safety and input state

## Executed diagnosis and bounded collision integration (in progress)

`road-physics-before2.xml` reproduces the cached-world hub error (up to .339 m)
and highway coast rollover (59.76 degrees, 110 -> 48.76 km/h in 1.5 seconds).
The front adapter now consumes NWH's chassis-parented nonrotating container;
`road-physics-posefix.xml` measures under .0004 m after LateUpdate while the
highway rollover still occurs independently. No mount coordinates changed.

An explicit test-only ContinuousDynamic A/B for the same native car and actual
road changes highway results to 5.74 degrees and 95.77 km/h. Speculative CCD
previously reported many zero-impulse compound/road contacts and a 9221 N s
floor impulse. The evidence supports a speculative-contact failure, not an
engine/spring coefficient diagnosis. The bounded prefab refresh changes only
chassis CCD; attached physical links inherit an owner's sweep-CCD choice, while
other owners retain their old policy. Detached Rigidbody defaults remain intact.

The rail fixture uses the actual world rail boxes and the intentional road cut.
Early `road-physics-rails3.xml` had a fixture defect: relocation cleared chassis
velocity but not the preceding speed band's wheel spin. Those results are not
clean A/B evidence. The corrected fixture resets rotor energy before settling.
`road-physics-corrected-before.xml` still reproduces old-CCD highway tilt 51.32
degrees and rail-60 tilt 59.25 degrees. `road-physics-corrected-after.xml` with
sweep CCD keeps highway tilt to 6.05 degrees and final speed 95.58 km/h, but
rail-60 still fails the speed-loss assertion (22.98 km/h). The rail issue is not
closed by the highway repair.

The four player-only shapes have a separate kinematic Rigidbody, deliberately
isolated from world collision. Parenting does not make that actor follow the
dynamic chassis: measured local drift grows beyond 100 m across the diagnostic.
Disabling its interpolation alone does not fix it (`road-physics-proxy-none`).
`VehiclePlayerCollisionFollower` preserves that ownership/mask architecture and
explicitly follows the chassis physical pose in FixedUpdate and rendered pose in
LateUpdate. `road-physics-contact-proxy-all.xml` measures zero proxy-local error
through all ten road/speed cases. No collider geometry, local anchor or save ID
is migrated.

Two contact experiments were rejected: a modifier that changed zero contacts,
and replacing rail-edge normals with upward normals (some phase offsets then
launched the chassis). Their passing individual cases are not repair evidence.
The integration gives compliant lower-tread contacts to the existing NWH
suspension authority: the auxiliary solid contact is ignored only while
grounded, with more than 1 mm travel remaining, below 35% of wheel radius from
its centre, with a predominantly fore/aft rather than sidewall normal. A dense
canonical rerun exposed the missing boundary: at zero travel a lower rail-edge
contact still delivered 8913 N s (`road-physics-canonical-dense.xml`), slowing
60 -> 32.67 km/h. For this same lower fore/aft region at bottom-out, retain the
contact but bound its maximum impulse to the authored spring maximum force
multiplied by the fixed step. That prevents an effectively infinite fore/aft
stiffness from appearing at the travel boundary. Vertical bottom-out normals,
upper rim, sidewalls, airborne wheels and chassis shapes retain unmodified
solid contacts. These adapter thresholds/impulse budgets are project-owned
integration parameters, NOT donor-transferred calibration coefficients.
`SatsumaWheelContactPolicy` binds four explicit wheel references,
matches Unity 6.6 collider EntityIds (not whole chassis actors), transforms
contact data through solver collider poses, and handles ordinary/CCD callbacks.
It reads a main-thread snapshot under a lock; callbacks do not query Unity
objects. The old Satsuma NWH contact modifier is disabled in the scoped prefab.
NWH vendor source, road/rail shapes, spring rates and rear force ownership are
intact. This is project-owned integration, not a donor Wheel code port.

The final coast stimulus sets the SAME translational velocity on the chassis,
jointed subframe and panels, plus matching wheel spin. Earlier chassis-only
stimuli caused an artificial initial joint impulse and understated coasting
speed. The final diagnostic also observes existing ProfilerMarkers instead of
adding an extra assembly synchronization pass. Before/after comparisons must
use matching versions of these conditions, not combine old speed numbers.

`road-physics-final-baseline.xml` uses the old speculative CCD/stock contact
policy in test memory, all-actor coast velocity, fresh native restores, and
eight rail cases offset by 7 cm. It reproduces highway-110 tilt 57.55 degrees,
and rail-60 tilt up to 160.88 degrees with final speed down to 13.23 km/h.
This old-policy diagnostic intentionally does not enforce the repair's
stability gates: its NUnit Passed status does NOT mean road acceptance passed.

`road-physics-common-velocity-after.xml` exercises the new force-ownership
policy: all 16 road/speed cases pass (dirt/highway 0/30/60/110, eight rail
30/60 cases). Highway-110 ends at 103.53 km/h, max tilt 5.58 degrees; rail-60
ends at 54.49–54.76 km/h, max tilt 6.29 degrees. Seven contacts were handled by
the new policy. A 3-metre solid barrier blocks the chassis at 8.13 m on a route
with the barrier centred 10 m ahead. Rendered hub error remains under .001 m.
The final canonical/regression rerun (without the extra diagnostic pose pass)
is recorded below when complete; this intermediate run is not the entire gate.

Scoped refresh `road-physics-final-refresh.log` returned changed=6: four stock
contact switches plus the new wheel policy and player-collision follower. Root
CCD was already refreshed earlier. No full prefab rebuild was performed.

## Performance investigation

The populated audit uses the real pending-native-load Bootstrap order with a
strictly read-only memory storage adapter. It loads native time/weather, items,
NPCs and vehicle state before reveal, then uses the existing HDRP player camera
at 1920x1080. This is an Editor GPU-rendered coast workload, not a Windows build
or an engine-acceleration benchmark. No native slot is rewritten.

Initial `road-render-populated3.xml` captured 29 streamed scenes on an RTX 4070
SUPER, Performant profile. Median frame times were 15.95 / 17.59 / 18.83 / 23.32
ms at initial 0 / 30 / 60 / 110 km/h. At 110, mean Physics.Simulate was 4.08 ms
and installed-pose synchronization 2.57 ms. GPU timing returned zero and render
thread timing was unavailable; those counters are NOT a zero-cost GPU claim.
The severe reported 10 FPS was not reproduced, but stable 60 FPS is not proven.
The image `Logs/road-render-audit/highway.png` was visually inspected: actual
native cabin, road and vegetation rendered, not an empty physics scene.

That populated test is **Failed**, not green: world materialization throws
`Duplicate light fixture ID 'traffic.light.p1.npc.043.low-beam'`. No exception
was suppressed. The numeric capture is usable for investigation but not a
passing whole-game smoke gate. Two preceding fixture-only failures (starting
before Bootstrap Start, then restoring weather after reveal) were corrected by
using the existing boot lifecycle rather than changing production contracts.

Measured repeated pose writes led to a bounded `PartInstance` optimization:
retain every ordered assembly pass, but skip Transform/Rigidbody setters only
when their actual coordinates are exactly equal to the requested coordinates.
No tolerance, cached target, mount dependency order, physical ownership or save
semantics changed. Performance and regression results for this change follow.

Body audio retains the donor-evidenced 1.5-second time gate and mapped world
categories, but uses a project-owned physical-impact check: nonzero impulse and
the normal component of relative velocity. Tangential highway speed is not a
hard body crash. Existing event IDs/backends/clips are preserved. This is not a
claim that the unbound generic donor SoundController crash branch was ported.

## Changed-file inventory for this packet

Runtime changes (existing APIs/serialized identities preserved):

- `Assets/Game/Vehicle/NWH/Runtime/SatsumaFrontSuspensionController.cs` —
  chassis-frame hub presentation.
- `Assets/Game/Vehicle/NWH/Runtime/SatsumaWheelContactPolicy.cs` — new explicit
  auxiliary-contact integration; `MSC.Vehicle.NWH.asmdef` references the already
  installed Unity.Collections assembly (no package installation/version change).
- `Assets/Game/Vehicle/Runtime/VehiclePlayerCollisionFollower.cs` — new player
  collision actor follower; original four masks/shapes retained.
- `Assets/Game/Vehicle/Assembly/Runtime/AssemblyInstalledPhysicsLink.cs` — owner
  sweep-CCD inheritance; detach defaults preserved.
- `Assets/Game/Vehicle/Assembly/Runtime/PartInstance.cs` — exact-pose redundant
  setter elision; all ordered synchronization passes retained.
- `Assets/Game/Vehicle/Assembly/Runtime/VehicleAssemblyController.cs` — named
  profiler marker only in this packet; unrelated earlier graph/save changes
  in the dirty worktree are not part of this repair.
- `Assets/Game/Vehicle/Runtime/VehicleAssemblyAudioPresenter.cs` — 1.5-second
  minimum body-event interval, world/self filtering, no zero-impulse events.
- `Assets/Game/Bootstrap/Development/SatsumaDrivingDebugView.cs` and
  `Assets/Game/Bootstrap/SatsumaDrivingSessionController.cs` — opt-in development
  F9 camera/telemetry with early restoration before input and explicit late
  application after the driver's eye anchor. No player/body/save movement.

Editor/generated content:

- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaBaselineBuilder.cs`
  wires the scoped policy into future builds.
- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaRoadPhysicsAuthoring.cs`
  validates provenance/bindings, backs up the existing prefab and updates only
  the road policy. Menu: `Tools > MSC Remake > Phase 1 > Satsuma > Refresh Road
  Collision Policy Only` (Play must be stopped).
- Existing ignored `Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab`
  was updated in place. Backup path is logged under `Logs/road-physics-before-*`.

Tests added/extended (with normal Unity metadata):

- `Tests/EditMode/SaveIntegration/SatsumaRoadPhysicsAuditTests.cs` and
  `SatsumaRoadRenderAuditTests.cs` (under `Assets/Game/`).
- `Tests/EditMode/VehicleAssembly/SatsumaBodyImpactPolicyTests.cs` and
  `InstalledPhysicsCollisionPolicyTests.cs`.
- `Tests/EditMode/VehicleNwhIntegration/SatsumaWheelContactPolicyTests.cs`.
- `Tests/EditMode/LegacyImport/SatsumaRoadPhysicsAuthoringTests.cs`.
- `Tests/PlayMode/VehiclePhysics/VehiclePlayerCollisionFollowerPlayModeTests.cs`.
- `Tests/PlayMode/VehicleAssembly/SatsumaNativeDriverPlayModeTests.cs` and
  `SatsumaDrivingSessionPlayModeTests.cs`.

All diagnostics, native snapshots, CSV/XML/logs/screenshots and generated donor
payload remain Git-ignored. No original installation, old Unity assembly,
donor FSM, production art replacement, schema migration or stable-ID change
is introduced by this packet. Human road handling/audio acceptance remains
distinct from automated coast, input and pose tests.

Initial worktree has 967 pre-existing/new dirty entries from ongoing accepted
work; preserve unrelated changes. No commit, donor patch, save-slot write,
external dependency installation or Phase 2 work is authorized by this packet.
Unity version is 6000.6.0f1; do not open with 6000.3.

The existing read-only snapshot `Logs/driver-native-source-20260907-0843.json`
matches the live source at start of this packet, SHA256
`484F554BEF1CE77AC6797D40E8F6C30A5279DC6BBFF2A0EDFCF78069FC62CDA1`.
It contains the prepared running engine and nine purchased parts, and a fully
applied handbrake. Tests must explicitly release that lever before driving;
never modify the native slot to make a test pass. All generated diagnostic
payloads stay ignored. This is not an automatic-clutch feature implementation.
