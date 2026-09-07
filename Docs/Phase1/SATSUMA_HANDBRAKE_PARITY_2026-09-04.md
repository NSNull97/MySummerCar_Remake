# Satsuma handbrake: donor parity and bounded implementation

Revision: `11A-V1d.64`, 2026-09-04.

Current handbrake status: **ImplementedAutomatedFocusedPassedLimitedManualAccepted**.
Final V64 generation succeeded, final EditMode passed **134/134**, and focused
handbrake PlayMode passed **2/2**. The subsequent bounded
[rotation/fixture regression repair](SATSUMA_FRONT_QUATERNION_REGRESSION_FOLLOWUP_2026-09-04.md)
passed **85/85 EditMode** and **33/33 combined PlayMode**, including all 20
installed-part cases and both handbrake cases; its status is
**ResolvedAutomatedRegressionPassed**. Earlier 13/22 results remain historical
evidence below. The user reported that the handbrake works within the checks
they could perform: limited manual acceptance, not completion of the full
incline/save/audio checklist or a new full-car slope comparison. `P1.CAR.009` remains
`PartiallyImplemented`: the hydraulic service-brake system is not completed by
this handbrake pass.

## Scope and original evidence

The installed licensed original and frozen extraction were inspected read-only.
Authority is `msc-world-baseline-04a1.1-c3f2f337`, frozen
`raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`,
SHA256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
No original FSM, controller, runtime assembly or donor hierarchy lookup is used
as runtime authority in the project.

Reviewed source records:

- `Use` FSM `106799`, `Brake` FSM `106798`, interaction object `9481`;
- rear `Force` / `GetTightness` FSMs `104529` / `104530`;
- handbrake `BoltCheck` FSM `112500`, assembled part `29367`, Transform `65418`;
- handbrake database object `35709`, `Data.Bolted`;
- installed moving lever mesh Transform `50119`; loose handbrake Transform
  `48714` and fixed base child `59668`;
- direct fastener mesh children listed below;
- donor rear-axle handbrake torque plus managed `Wheel.FixedUpdate` torque
  multiplier, transferred through the existing NWH boundary rather than donor
  wheel code.

### Mechanical independence from hydraulic brakes

The handbrake is a cable-operated rear brake in the reviewed logic. Its reviewed
`Use`, `Brake`, `Force` and `GetTightness` paths do **not** require brake hydraulic
lines, the master cylinder or brake fluid. Those are service-brake dependencies
and must not be invented as prerequisites for this mechanism.

`Use` checks `Data.Bolted` before offering control. `Brake` also checks that
state before issuing demand. The rear force path reads **fastener array index
4**, the fifth, 5 mm fastener: stage zero removes the rear handbrake contribution;
any positive stage establishes the connection. It is not an analog tension
coefficient: stage one does not mean one eighth of the braking force.

The remake additionally checks that the explicit handbrake part really occupies
its explicit assembly mount. A stale `PartInstance.IsInstalled` value by itself
cannot create braking force.

### Lever movement and input

| Donor contract | Value |
|---|---|
| `Use.KnobPos` initial value | `0.1` degrees |
| Held LMB / RMB | increase / decrease |
| `FloatAdd`, `everyFrame=true`, `perSecond=true` | `+100` / `-100` degrees per second |
| `FloatClamp` | `0..20` degrees |
| Moving presentation | local X = position; local Y/Z = zero in the authored rest frame |
| Brake-off threshold | `0.1` degrees |
| Applied input above the off threshold | `position * 0.05` |

Released input leaves the ratcheted lever at its current position. This is not
a body-panel hinge: it does not receive the free physical inertia used by the
accepted doors/hood/bootlid. There is no `F` action and no click-to-toggle input.
The project treats `position <= 0.1` as released, so the authored initial pose
does not produce residual wheel drag at the exact threshold.

The held interaction shape is a dedicated trigger, separate from the part's
physical collider. It is enabled only when the installed, bolted mechanism can
be operated, preserving loose pickup and unbolted assembly removal interaction.

### Five bolts, not five nuts

| Generated ID suffix | Frozen marker Transform | Wrench | Original visible mesh |
|---|---:|---:|---|
| `handbrake.boltpm-1` | 55735 | 8 mm | short bolt |
| `handbrake.boltpm-2` | 63139 | 8 mm | short bolt |
| `handbrake.boltpm-3` | 65746 | 8 mm | short bolt |
| `handbrake.boltpm-4` | 68029 | 8 mm | short bolt |
| `handbrake.boltpm-5` | 68693 | 5 mm | short bolt |

All five direct `MeshFilter` children use source mesh GUID
`aec6c756751308a4d830708366ad5cdb` (`bolt`, short bolt), with zero local position,
identity rotation, unit child scale and shared material
`98697bae08a8c114ba9774c487f2658d`. Their source GameObject labels include
`bolt0` through `bolt4`; labels are not authoritative mesh classifications.

The previous generated prefab used source mesh
`e711c8a15b1135c4089caad19b8f56e8` (`bolt2`, nut) on all five. The root cause was
the generic `FastenerBuild` nut fallback: earlier reviewed front/rear/body
override tables did not include the handbrake. V64 adds an exact, source-guarded
five-marker override without changing their IDs, sizes, positions or stages.

Each fastener has eight stages, giving aggregate maximum `40`. The donor
`BoltCheck` latch is **on at 6, off at 0**. The old generated handbrake still had
the generic on-at-1 fallback. V64 transfers 6/0 rather than requiring all five
pieces to be fully tightened before any interaction becomes available.

### Partially tightened break-off

The donor checks break-off on `Brake OFF -> Chance`, when position first rises
above `0.1`, not on every held frame. `Chance` reads aggregate tightness `T` and
selects between two weighted outcomes:

- `FINISHED`: weight `1`;
- `BREAK`: weight `(39 - T) / 40`.

The project treats non-positive break weights as zero. For positive weight `w`,
break probability is `w / (1 + w)`: about `45.21%` at `T=6`, about `2.44%` at
`T=38`, and zero at `T=39..40`. Releasing the mouse without lowering the lever
does not reroll the chance. Lowering to the released threshold rearms the next
application check. Reactivating a raised, installed lever also enters this
check; applying the save DTO itself never mutates the graph or plays audio.

Donor `Break off` sends `IS_DAMAGED` to the handbrake `BoltCheck` and waits one
second. The remake uses the project-owned
`VehicleAssemblyController.TryBreakInstalledPart` path, which rejects root,
foreign and loose parts and reuses established dependency collapse, physical
detach and action notifications. It does not bypass normal removal by zeroing
screw stages. The donor's one-second dormant-FSM wait is not reproduced as a
global control delay after a detached part becomes unavailable.

## Implementation boundaries

- `SatsumaHandbrakeController` owns position, held intent, effective demand,
  donor break chance and the optional save DTO. Its graph checks use project
  stable part/mount/fastener IDs.
- `SatsumaHandbrakeInteractionTarget` implements
  `IContinuousContextInteractionTarget`, with held LMB/RMB and no
  `IContextInteractionTarget`/F action. Input continuation does not simulate the
  lever a second time.
- The builder creates an independent moving-only pivot below the handbrake
  `PartInstance`. It reparents only the lever renderer, source mesh
  `ab90c359dad610a4a8325d7a8a3c3162`. The fixed base, assembly pose, fastener
  targets and pickup body must not rotate with the lever.
- `SatsumaHandbrakeNwhAdapter` supplies independent parking-brake torque to the
  two explicitly bound rear NWH wheels. Maximum torque is **1000 N m per rear
  wheel**, from donor rear-axle 500 N m, rear balance multiplier 1, and the donor
  wheel's factor 2. Front wheel brake torque is not changed by this adapter.
- `NwhWheelPhysicsBackend` retains distinct service and parking torque channels
  and combines them additively. A service command, an unavailable drivetrain or
  a reset must not overwrite the mechanical parking contribution. No chassis
  rotation freeze, velocity lock, added contact or suspension-force rewrite is
  used to simulate parking.
- `SatsumaNwhPhysicsRestoreSynchronizer` reapplies the resolved parking torque
  along with the existing physics restore synchronization.

### Satsuma-only slope release policy

The first slope run exposed an additional interaction with NWH's low-speed
anti-creep lock and the backend's stationary-wheel clamp: after releasing the
handbrake, the rear axle remained stationary even with zero parking torque.
The correction is a project-owned, default-off opt-in enabled only by the
Satsuma builder. Other vehicles retain their existing behavior.

Before clamping each wheel's stationary angular state, backend execution order
150 evaluates the physical contact plane for **all four wheels**. NWH itself
steps at order 100. With measured normal load `L`, wheel radius `R`, contact
normal `n` and steered forward direction projected onto that plane:

```text
normalGravity = abs(dot(gravity, normalize(n)))
longitudinalGravity = abs(dot(gravity projected onto contact plane,
                              normalized wheel-forward tangent))
gradeTorque = L * longitudinalGravity / normalGravity * R
release lock if gradeTorque > BrakeTorque + RollingResistanceTorque + 0.05 N m
```

Only a grounded, valid, loaded contact can qualify. Qualifying wheels receive
the public `WakeFromSleep()` signal, and that frame's project rest clamp is
skipped. The signal reaches NWH's next step and disables its anti-creep lock;
no propulsion force, velocity, chassis constraint or vendor-code change is
introduced. Considering every corner prevents released front wheels becoming
invisible anchors. Comparing actual resisting torque also prevents a weak
handbrake becoming an infinitely strong slope lock.

Flat-ground behavior is retained: chassis pitch alone is not an incline, and a
pure sideways grade does not release the longitudinal lock. Tests cover these
cases, insufficient/excessive resistance, invalid input and explicit opt-in.

## Save compatibility

The existing `VehicleSaveRecordDto` schema remains 1 and receives the optional
`handbrake` field. `SatsumaHandbrakeSaveDto` schema 1 stores only finite
`positionDegrees` in `0..20`. Missing payloads in old saves retain the donor
released/default `0.1` pose. Unsupported schema, NaN, infinity and out-of-range
values fail validation before applying the position.

Held mouse direction, a pending deterministic chance sample and derived wheel
torque are transient and are not serialized. Successful restore/reset/disable
clears held input. Existing stable IDs and fastener stage arrays remain intact;
the handbrake geometry fix does not migrate or rewrite user saves. The normal
aggregate save validation and rollback remain authoritative.

## Presentation implemented; automated contracts passed, full manual checklist pending

- The permanent fixed rod, source Transform `60945`, is authored as a fixed
  vehicle-root child using mesh `1114fb760fb4d7a4cac87438f8bdc8a9` and material
  `244b34167b8e8de4992ebb466651484d`. It remains active even while the detachable
  handbrake part is loose, does not follow the moving lever pivot, casts no
  shadows and receives shadows. This corrects the earlier omission without
  turning the rod into a new removable assembly part.
- Moving-only lever/base/rod ownership is implemented and final V64 generation
  succeeded. Post-rebuild generated-rig assertions passed within the final 134/134
  EditMode run; exhaustive presentation acceptance is not established by the
  user's limited manual check.
- Original `CarFoley/handbrake_on` and `handbrake_off` are non-looping, volume 1,
  pitch 1, source min/max distance 1/8 m. Hash-locked temporary audio bindings and
  `HoldStarted(bool raising)` presentation are implemented through the existing
  `IAudioBackend` path. Handbrake requests pass a null emitter and an explicit
  current lever world position so the shared chassis emitter cannot override
  their source position. Existing assembly audio routing is unchanged. The
  audio import succeeded with 9 clips and 10 event overrides; audible/spatial
  acceptance remains pending.

All donor presentation stays `TemporaryDirectImport`, private, replaceable and
outside Git. The new runtime does not use the donor executable or assemblies.

## Validation and acceptance

### Executed handbrake results, regression follow-up and earlier diagnostics

Unity version: `6000.3.11f1`. Focused automated completion is distinct from
full manual acceptance. The final handbrake-stage failure and its later
regression repair are listed separately so the old failure is not erased:

| Run | Result | Evidence |
|---|---|---|
| Temporary assembly audio import | exit 0; 9 clips, 10 event overrides | `Logs/codex-handbrake-v64-audio.log` |
| Initial focused EditMode | 35/39, four failures; not accepted as a pass | `Logs/codex-handbrake-v64-edit-focused.xml` |
| Expanded EditMode before the final rebuild | 83/84, one generated moving-lever failure | `Logs/codex-handbrake-v64-edit-final.xml` |
| Earlier presentation-corrected rebuild | exit 0; V64 `BUILD_OK` | `Logs/codex-handbrake-v64-build-final.log` |
| Earlier post-rebuild EditMode | 84/84 passed; zero failed/skipped | `Logs/codex-handbrake-v64-edit-verified.xml` and `.log` |
| Earlier combined PlayMode | 11/22 passed; 11 failed; zero skipped | `Logs/codex-handbrake-v64-play.xml` and `.log` |
| Final slope-policy baseline rebuild | exit 0; `PHASE1_SATSUMA_BUILD_OK version=11A-V1d.64` | `Logs/codex-handbrake-v64-slope-build-final.log` |
| Final EditMode | 134/134 passed; zero failed/skipped | `Logs/codex-handbrake-v64-slope-edit.xml` and `.log` |
| Final focused handbrake PlayMode | 2/2 passed, within the combined run below | `Logs/codex-handbrake-v64-slope-play.xml` and `.log` |
| Handbrake-stage combined physics PlayMode | 13/22 passed; nine failed; zero skipped | Same V64 PlayMode artifacts; historical **not an overall pass** |
| Subsequent rotation/fixture EditMode | 85/85 passed; zero failed/skipped | `Logs/codex-physics-regression-rotation-edit.xml` and `.log` |
| Subsequent combined physics PlayMode | 33/33 passed; zero failed/skipped; installed-part 20/20 and handbrake 2/2 included | `Logs/codex-physics-regression-combined-play.xml` and `.log` |
| Manual acceptance | LimitedManualAccepted | User: works within the checks they could perform; full incline/save/audio checklist is not claimed |

The final builder retained 125 loose parts, 120 active loose parts, 117 mounts,
280 assembly fasteners, four hinged mounts and 52 owned mounts. The current
successful rebuild is later than the XML named `edit-final`: that filename
must not be mistaken for validation of the newest generated output.

The initial four failures exposed an audio event-count/lifecycle fixture
assumption, generated rig lookup, an uninitialized persistence fixture and an
EditMode test expecting ordinary `OnDisable` delivery. The latter now explicitly
checks unavailable-target continuation and controller simulation; it does not
pretend to be a PlayMode lifecycle test. Production `ExecuteAlways` was not
added to satisfy a fixture.

The expanded `83/84` run isolated a real builder defect: Unity rejected moving
a renderer out of a nested prefab instance, leaving the generated moving pivot
without its intended mesh. The correction unpacks **only the generated
handbrake presentation instance** before reparenting and fails generation if
the moving renderer did not reach its intended pivot. It is not a broad prefab
unpack or a runtime hierarchy search. The corrected builder succeeded and its
generated-rig regression passed in the post-rebuild 84/84 run.

The earlier slope failure measured downhill speed about -0.00000537 m/s after
release, against a greater-than-0.3 m/s gate. Following the opt-in policy above,
both handbrake PlayMode tests passed: controlled 5-degree free-roll/stop/hold/
release, and additive service/parking torque with reset and real `OnDisable`
cleanup. No assertion or physical fixture tolerance was relaxed to achieve it.

The handbrake-stage **broader run was not a pass**.
`RearNwhSpringsSupportTheChassisThroughGroundContact` passed, but nine
`SatsumaInstalledPartPhysicsPlayModeTests` failed at that point:

- Six cases report non-unit rotation quaternions: fresh restore; rear arm
  interaction proxy; rear arm/spring/shock ownership; springless arm contact;
  rear drum socket; and installed rear arm hinge. Diagnostic samples record
  raw chassis Rigidbody quaternion norm-squared `1.00010252`, inherited by the
  cached base frame; the multiplied anchor quaternion reaches norm-squared
  `1.00020504`. This identifies the failing value chain, not its final root
  cause; no steering fix had been applied at that stage.
- `FrontAssemblyEnablesNwhSupportAndDrivesDonorRig`: y `0.292074203 m`, expected
  above `0.302074641 m`.
- `FrontNoStrutDynamicChassisCompressesWithoutPhantomSpringLaunch`: y
  `0.410550267 m`, expected `0.23..0.32 m`.
- `RearSpringAndShockFollowNwhCompressionAndDampingStage`: hit
  `collider_floor3_92417`, not the fixture's `Rear NWH compression patch`.

Front diagnostics also hit `Fresh Satsuma restore ground`, not their own
intended isolated fixture surface. These foreign contacts motivated investigating
fixture isolation/contact ownership, not declaring the expectations obsolete.
Temporary diagnostic logging was removed after evidence collection. At that
stage the old front steering source and installed-part fixture were unchanged.

The later [regression follow-up](SATSUMA_FRONT_QUATERNION_REGRESSION_FOLLOWUP_2026-09-04.md)
reproduced fresh-restore and rear-arm non-unit failures independently (0/1 each),
normalized valid front-yaw physics frames without identity substitution for
invalid data, and made fixture ownership/cleanup explicit across detached parts
and two deferred-destruction frames. It added 13 rotation EditMode cases and
explicit contact-owner assertions without changing suspension settings or
existing numeric tolerances. Final 85/85 EditMode and 33/33 combined PlayMode
passed; all nine historical failures are included in the now-passing 20-case
installed-part group. This closes the documented automated regression, not an
unexecuted full-car donor comparison.

### Test inventory and scope

Test code covers:

- `SatsumaHandbrakeTests`: 18 focused EditMode cases for lever movement,
  release/disable, graph/fastener gates, absence of hydraulic dependencies,
  schema validation, direction events, weighted break and damage-detach safety;
- `SatsumaHandbrakeGeneratedTests`: generated rig/fastener/save integration;
- `SatsumaHandbrakeAudioTests`: reviewed source/binding contracts;
- `SatsumaHandbrakePlayModeTests`: both named tests above passed;
- `NwhWheelPhysicsBackendTests`: 50 cases passed, including grade/resistance,
  flat/sideways contact, validation and opt-in regression cases.

The final 134-case EditMode XML contains 39 generated-content cases, six
front-fastener mesh guards, one handbrake HUD case, 16 electrical/wiper cases,
two handbrake audio cases, two generated/save cases, 18 controller cases and
50 NWH backend cases.

The slope fixture is deliberately bounded: a `200 kg`, rear-axle-only body with
rotation constrained isolates rear contact/brake behavior. Even a passing
result there is not full-vehicle incline acceptance, full suspension/load
distribution validation or a substitute for the manual assembled-Satsuma check.

The 2/2 result validates this bounded handbrake fixture only. The subsequent
33/33 regression run does not broaden it into a full-car slope test. Limited
manual acceptance comes from the user's report, not from an automated pass.

### Executed commands and artifacts

The full recorded command lines are in the corresponding logs. All used
`-batchmode -nographics` against the existing project, with serial Unity
ownership:

- `-executeMethod MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaAssemblyAudioImporter.BuildFromBatch`
  with `-quit` and the audio log above;
- `-executeMethod MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaBaselineBuilder.Build`
  with `-quit` and final `codex-handbrake-v64-slope-build-final.log`;
- `-runTests -testPlatform EditMode -testFilter` includes
  `MSC.Tests.EditMode.VehicleAssembly.SatsumaHandbrake`,
  `MSC.Tests.EditMode.VehicleAssembly.SatsumaElectricalWiperTests`,
  `MSC.Tests.EditMode.PlayerInteraction.PlayerInteractionRuntimeTests.ContextHud_HandbrakeHoldHintsUseEachDirectionInsteadOfPickup`,
  `MSC.Tests.EditMode.LegacyImport.Phase1SatsumaGeneratedContentTests`,
  `MSC.Tests.EditMode.LegacyImport.SatsumaFrontFastenerMeshTests`, and
  `MSC.Tests.EditMode.VehicleNwhIntegration.NwhWheelPhysicsBackendTests`, joined
  by semicolons; `-testResults Logs/codex-handbrake-v64-slope-edit.xml`;
- `-runTests -testPlatform PlayMode -testFilter` includes
  `MSC.Tests.PlayMode.VehiclePhysics.SatsumaHandbrakePlayModeTests` and
  `MSC.Tests.PlayMode.VehiclePhysics.SatsumaInstalledPartPhysicsPlayModeTests`,
  joined by a semicolon; `-testResults Logs/codex-handbrake-v64-slope-play.xml`;
- `git diff --check` for scoped source/document edits; no whitespace errors.

SHA256 of the final local evidence artifacts (not donor payload; logs stay
ignored and are not committed):

| Artifact under `Logs/` | SHA256 |
|---|---|
| `codex-handbrake-v64-slope-build-final.log` | `57B88C25C53549964B29E88B421034B3B74B260F44AC4604948204A6FADB6FA1` |
| `codex-handbrake-v64-slope-edit.xml` | `5A92BFF0E0457FD9A21E8A1E3FDA42E18F355CF4F0616148244066D7CB13A1DC` |
| `codex-handbrake-v64-slope-edit.log` | `509E6478FD55D25D6D0D4863F469A5038D76447CADA8998803DA047B8D935066` |
| `codex-handbrake-v64-slope-play.xml` | `00940B01689F893952D5D98AB39F2740E33AAC3FE840C56CDE0A18F14A8F8ABD` |
| `codex-handbrake-v64-slope-play.log` | `20EF91F12A1E528010D53B48F3E51D02A5EE57C46C3791331A63C44587E631DB` |

## Change list and compatibility impact

New runtime files are `SatsumaHandbrakeController.cs`,
`SatsumaHandbrakeInteractionTarget.cs` and `SatsumaHandbrakeNwhAdapter.cs`, with
their project-owned Unity metadata. The four focused test files are listed
above. Existing integration edits are confined to the Satsuma baseline builder,
`VehicleAssemblyController`'s validated damage-detach entry point,
`VehiclePersistence`, NWH brake channels/restore synchronization and Satsuma-only
slope lock-release policy, existing audio
IDs/presenter/manifest, directional context text and the reviewed-fastener
regression exclusions. The new report and Porting/parity records document the
same bounded scope.

The compatibility intent is to preserve accepted suspension/contact behavior,
wheel seating, road-wheel identity, hinged panels, wiring and the 08A UI layout.
The later 33/33 run demonstrates preservation for the selected installed-part,
steering/spawn and handbrake fixtures, not all possible runtime flows. This pass adds no
input binding, changes no existing stable part/mount/fastener ID and renames no
existing serialized field. The optional handbrake save field is the compatible
extension described above, not a wholesale save migration. The root damage
API reuses existing collapse behavior; front/service braking remains outside
the new rear parking channel.

The original installation remains read-only. Generated meshes, prefabs,
materials, temporary audio and extraction payload are ignored/private and must
stay outside Git; only authored code, tests, manifests, mappings and reports
belong in the commit. The previously failed physical regression is now covered
by the passing follow-up; full-vehicle incline and exhaustive manual acceptance
remain outside the demonstrated scope, not permission to reopen accepted
unrelated systems.

Full manual acceptance recipe, **not established in full** by the limited check:

1. Install the lever; verify four 8 mm short bolts at the mounting base and the
   separate 5 mm short bolt under the vehicle. Verify pickup/removal still works
   before the group latches and that the held target does not steal bolt focus.
2. Tighten all five fully for a safe test. With rear braking/wheel contact
   available, hold LMB to raise and RMB to lower. Confirm retained intermediate
   position, no F action, moving lever only and correct sound position.
3. Check rolling, holding on a suitable incline, then release; no brake lines,
   fluid, running engine or electrical supply should be required for the cable
   brake. Confirm front wheels are not parking-braked.
4. Leave four mounting bolts tightened, loosen the 5 mm connection to zero:
   lever movement remains available but parking force disappears. One positive
   stage reconnects the cable; use full tightening for normal testing.
5. Save with the lever raised and with it released; load each and check retained
   position and matching brake demand without stuck held input or chassis jump.

This work does not complete hydraulic brakes, engine/driver-seat integration,
vehicle wear, the remaining cabin controls, or the full Satsuma parity row.

The bounded [front-rotation and physics-fixture regression repair](SATSUMA_FRONT_QUATERNION_REGRESSION_FOLLOWUP_2026-09-04.md)
has now passed its automated gate. Exactly one recommended next step is a
separate donor comparison of the current front/rear assembly; this report does
not imply that audit or hydraulic-brake completion has already occurred.
