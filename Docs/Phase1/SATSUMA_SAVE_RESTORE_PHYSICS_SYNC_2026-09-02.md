# Satsuma save restore physics synchronization

Revision `11A-V1d.44`, 2026-09-02. Wheel rotation remains the separately
bounded V1d.43 change. This revision corrects only the physical handoff after
loading a Satsuma save. Automated validation passed; manual native-save
acceptance is `USER PASS` as of 2026-09-02.

## Frozen original evidence

Read-only authority is the extracted donor `GAME.unity`, SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Relevant frozen objects are the Satsuma root GameObject `28145`, Rigidbody
`91271` and setup FSM `112086`.

The donor scene authors the root Rigidbody with `useGravity = true` and
`isKinematic = true`. Loading does not toggle gravity. Its physical release is
ordered instead:

1. restore the saved root transform while the body is already kinematic;
2. explicitly keep it kinematic, add a temporary `FixedJoint`, then wait about
   `0.1` real seconds;
3. disable all four wheel solvers;
4. restore per-part `Installed` state; the front wishbones and rear trailing
   arms then re-enable their matching wheel solvers through their own Data
   FSM side effects;
5. keep the root guarded for the donor's coarse eight-second setup barrier;
6. complete collider/state setup, release `isKinematic` and destroy the
   temporary joint.

The donor does not explicitly call `WakeUp`, zero velocity or switch
`useGravity`. Its long delay is an old asynchronous FSM barrier, not evidence
that gravity should be disabled during a modern load.

## Root cause in the remake

`VehicleAssemblyController.RestoreSaveData` deliberately restores the graph
without publishing an ordinary `ActionCompleted`. That is correct for a load,
but the old persistence sequence released the chassis immediately afterwards.
Controllers normally refreshed by a live assembly action therefore still held
pre-load state for at least one update:

- compound chassis mass and centre of mass;
- NWH corner enablement, contact radius and spring/damper stage;
- front steering and suspension authority;
- rear NWH/presentation authority and installed-part poses.

Changing any part after the load published the missing normal assembly action,
ran those refresh paths and appeared to "cure" the car. This explains the
reported load-only pitch/launch behavior without blaming `useGravity`, which
was already true after loading.

## Implemented correction

`VehiclePersistenceBindingCore` now treats backend synchronization as part of
the restore transaction. While the chassis is still temporarily kinematic it:

1. applies the saved chassis pose, assembly graph, simulation, input and paint;
2. invalidates assembly-dependent simulation prerequisites;
3. invokes the project-owned `IVehiclePhysicsRestoreSynchronizer` boundary;
4. for Satsuma, force-refreshes NWH support, front steering, front suspension,
   rear suspension authority and installed-part propagation;
5. force-refreshes compound mass and centre of mass;
6. calls `Physics.SyncTransforms`;
7. only then restores the body's dynamic state, saved velocities and
   sleep/wake state.

The Satsuma adapter wakes enabled NWH wheels but does not initialize NWH
manually. On a fresh prefab, NWH `Start()` still owns creation of its visual
containers, collider and transient spring/contact sample. It preserves the
already restored radius, travel, spring force, damping and enabled stages, and
the normal fixed/late passes establish the first contact pose. Therefore no
new arbitrary delay or asynchronous save contract is required.

This is the modern synchronous equivalent of the donor's causal ordering. It
does not execute donor FSMs and does not reproduce their eight-second loading
stall or temporary joint.

## Unchanged contracts

- save DTO schema and stable IDs are unchanged;
- `useGravity` is neither serialized nor toggled by this correction;
- accepted front/rear suspension tuning, wheel seating and fasteners are
  unchanged;
- NWH remains the sole contact authority once its existing prerequisites are
  satisfied;
- restore still publishes no fake player assembly action;
- there is no post-`Start` coroutine and no second wheel initialization.

## Files

- `Assets/Game/Vehicle/Simulation/IVehiclePhysicsRestoreSynchronizer.cs`;
- `Assets/Game/Vehicle/Runtime/VehiclePersistence.cs`;
- `Assets/Game/Vehicle/NWH/Runtime/SatsumaNwhPhysicsRestoreSynchronizer.cs`;
- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaBaselineBuilder.cs`;
- `Assets/Game/Tests/EditMode/LegacyImport/Phase1SatsumaGeneratedContentTests.cs`;
- `Assets/Game/Tests/PlayMode/VehiclePhysics/SatsumaInstalledPartPhysicsPlayModeTests.cs`;
- regenerated private Satsuma baseline and manifest, builder `11A-V1d.44`.

## Automated validation

Executed with Unity `6000.3.11f1`:

| Gate | Result | Coverage |
|---|---:|---|
| immediate restore synchronization | 1/1 | no fake assembly action; support, authority, mass and centre of mass are rebuilt before release |
| fresh pre-`Start` restore | 1/1 | destroy/reinstantiate, restore before first yield, then 120 physical ticks on four installed wheels without any part mutation |
| full installed-part physics | 20/20 | front/rear suspension, wheel rotation and both restore regressions |
| generated Satsuma | 29/29 | V1d.44 adapter and persistence wiring in the promoted prefab |
| current-domain save integration | 13/13 | aggregate capture/restore compatibility |
| vehicle simulation | 22/22 | simulation DTO/reset regression |
| production Satsuma bootstrap | 1/1 | production composition still spawns with gravity and required boundaries |

Artifacts:

- `Logs/codex-v1d44-save-restore-build.log`;
- `Logs/codex-v1d44-save-restore-focused-results.xml`;
- `Logs/codex-v1d44-save-restore-fresh-results-2.xml`;
- `Logs/codex-v1d44-satsuma-physics-full-results.xml`;
- `Logs/codex-v1d44-generated-satsuma-results.xml`;
- `Logs/codex-v1d44-current-domain-save-results.xml`;
- `Logs/codex-v1d44-vehicle-simulation-results.xml`;
- `Logs/codex-v1d44-production-satsuma-bootstrap-results-2.xml`.

## Manual acceptance result

`USER PASS`, 2026-09-02. After native save/load the assembled car stood
normally, did not separate, launch, pitch or require a part mutation to recover
its physics. Gravity-driven rolling also behaved normally. The handbrake case
was not exercised because that mechanic is not implemented yet; it is outside
this restore correction.

Retained regression recipe:

The native save check should be performed without touching any part after
loading:

1. fully assemble and tighten the four suspension corners and wheels;
2. park on level ground with no braking constraint, save and reload;
3. confirm the body neither launches nor raises one end and settles on the
   restored suspension by itself;
4. let the car roll and confirm all four visible wheels rotate;
5. repeat with one deliberately incomplete suspension corner and confirm its
   accepted incomplete-assembly behavior is present immediately after load;
6. only after those observations mutate a part, to prove that mutation is no
   longer required as a repair trigger.

The narrow dynamic test covers the production timing up to direct vehicle
restore, including restore before NWH `Start()`. A full
`RequestLoad -> single-scene reload -> NativeSaveLoadHandoff` smoke remains a
useful later integration test, but no red evidence currently justifies adding
an artificial one-fixed-step release delay.
