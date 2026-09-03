# Satsuma issue 4 — rear droop and separated shock halves

Revision `11A-V1d.38`. Scope: the excessive rear trailing-arm droop and visible
shock separation on a raised chassis. The front suspension is accepted and
must remain unchanged. Rear wheel installation/seating (issues 5–6) and general
save-system repair are outside this change. The red reproduction is executed
and the scoped code correction is implemented. Baseline rebuild, focused
PlayMode and physics regressions pass. The user accepted the reported droop/
shock-separation correction on 2026-08-31: «разжатие без разрыва, принимается».
Remaining EditMode/Bootstrap validation was blocked at the last attempt by
temporary compile errors in parallel vegetation work. Current compilation was
not rechecked for this documentation-only acceptance update; that unrelated
work is not modified here.

Follow-up 4A: the user subsequently reported insufficient loaded compression.
The accepted no-separation fix remains intact, but wheel-fitting work is now
preceded by the force/preload diagnostic and specification in
`Docs/Phase1/SATSUMA_REAR_LOADED_COMPRESSION_AUDIT_2026-08-31.md`.
Its audit changes no runtime code and does not claim a 4A fix.

## Confirmed failure

The shock was not breaking its mesh or losing its mounting point. The existing
rear hinge allowed more droop than the donor rear suspension; two correctly
positioned rigid shock halves consequently stopped overlapping.

Executed evidence: `Logs/codex-rear-droop-v1d38-red.log` and
`Logs/codex-rear-droop-v1d38-red.xml`, fixture
`SatsumaRearDroopPlayModeTests`: **0/2 passed, 2 failed, 0 skipped**.
Both the stock-spring-plus-shock case and shock-without-spring case reproduce
the failure. Telemetry covers both RL and RR.

Before physics, shock attachment distance is approximately 0.386980 m and mesh
overlap is approximately 0.102447 m. After 30 physics steps, and still after 120:

- Arm hinge angle is approximately −32°, at the configured −32…+8° limits.
- Shock attachment distance is approximately 0.516973 m RL / 0.516974 m RR.
- Projection of the actual mesh vertices along the common shock axis gives a
  positive gap of approximately 0.027544 m on each side: **2.75 cm**.
- Hinge anchor error is approximately 0.000092 m, not a multi-centimetre joint
  separation. Correcting welded-joint projection or moving a visual endpoint
  would therefore address the wrong cause.

The same geometric result follows without simulation. At the authored −32°
hinge stop, the reviewed pivots give attachment distance 0.516884 m. The unit-scale
source meshes can reach at most 0.489428 m along their shared axis, leaving at
least 0.027456 m uncovered. This agrees with the real-vertex runtime measurement
within the observed physics/anchor displacement.

**0.489428 m is a mesh-envelope upper bound, not a donor functional shock stroke
or a value to turn into a new physical limiter.** The correct travel source is
the donor wheel profile and trailing-arm IK below. The fix must not hide
overtravel by stretching the shock or sliding its mounting point.

## Locked donor travel and arm direction

Authority: read-only frozen `msc-world-baseline-04a1.1-c3f2f337`.
`GAME.unity` SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
This is serialized FSM/hierarchy and managed calculation evidence, not a new
original-game runtime capture. No donor or vendor file is modified.

Suspension FSM 108170 configures the rear wheel carrier height and travel.
Wheel components 113814 (RL) / 112218 (RR), through the original `Wheel.cs`
`CalcWheelMovement`, place the non-spinning hub at `carrierY + compression - travel`.
Rear arm IK 104501 (RL) / 106870 (RR) aims at a target hanging from that hub:
target 63031 is +0.18 m local X for RL; target 36429 is −0.18 m for RR. Those offsets
place each target on its own arm-pivot X; they must not be discarded when
deriving travel from the wheel centre.

The reviewed body-side pivots 36568 / 67486 have Y −0.2153 m, Z −0.857 m. The hub/IK
target Z is −1.167 m, giving a 0.310 m horizontal run. The angle is the direction
to the IK target, **atan2**, not an asin calculation using a rigid arm length:

```text
angleDegrees = atan2(targetY - (-0.2153), 0.310) * Rad2Deg
targetY at droop = carrierY - travel
targetY at compression = carrierY
```

| Installed spring profile | CarrierY (m) | Travel (m) | Droop targetY (m) | Hinge direction range (degrees) |
|---|---:|---:|---:|---:|
| No spring | −0.150 | 0.140 | −0.290 | −13.548166…+11.895193 |
| Stock spring | −0.165 | 0.140 | −0.305 | −16.138075…+9.216392 |
| Extra-long spring | −0.180 | 0.170 | −0.350 | −23.485751…+6.496352 |

The original arm is transform IK driven from wheel travel, not our free
physical −32° hinge. `SimpleIKSolver` runs in LateUpdate and rotates its joints
toward the target. Its rear arm/shock `AngleRestrictionRange` axis flags are
all false; these IK restrictions do not supply an independent −32° stop.
The profile-derived directions bound the existing physical remake arm without
replacing the accepted PhysX rear assembly/contact system.

### Exact source locations and script identity

The source locations below use the frozen export selected by
`Config/DonorPaths.local.json`; they are provenance references, not runtime
dependencies. `GAME:<line>` elsewhere in this report denotes the same scene.

- [Suspension FSM 108170](<E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity:4465829>):
  `Not installed 5` at 4467126, `Not Installed 6` at 4467421;
  `Stock 5` at 4468270, `Stock 6` at 4468565;
  `Long1` at 4468860, `Long2` at 4469155. These paired states supply the three
  rear profiles in the table, rather than the raw serialized wheel defaults.
- Wheel components: RL 113814 at `GAME:8111964`, RR 112218 at `GAME:7086279`.
  [Wheel.cs — CalcWheelMovement](<E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/Scripts/Assembly-CSharp/Wheel.cs:750>)
  applies `compression - suspensionTravel` at line 759 and copies that position
  to `caliperModelTransform` at line 763; this non-spinning child owns the rear
  arm's IK target chain.
- [Rear arm IK 104501, RL](<E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity:1993869>)
  and [106870, RR](<E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity:3528939>)
  use target transforms 63031 (`GAME:935104`) / 36429 (`GAME:587753`).
  Rear wheel parents 59720 (`GAME:892020`) / 41159 (`GAME:647779`) establish
  target Z; body-side pivots 36568 (`GAME:589488`) / 67486 (`GAME:992075`)
  establish the origin used by the angle calculation.
- [SimpleIKSolver.cs — Solve](<E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/Scripts/Assembly-CSharp/SimpleIKSolver.cs:73>):
  LateUpdate dispatch is at line 65, joint world rotation toward the target at
  line 105, optional axis restrictions at line 113. The scene flags determine
  whether those restrictions are active.

Read-only SHA256 verification of the frozen `Assets/Scripts/Assembly-CSharp/`
files used for this review:

| Script | SHA256 |
|---|---|
| `Wheel.cs` | `85FFBF994222E04AC61A32BEBC9CDEAE9C7EFE53DF7D7BF75910EA80BC051945` |
| `SimpleIKSolver.cs` | `7A597C23F8A7E2ACEEDF73EEB564820F704977913F9D0127DBC9962514F97C90` |

## Shock geometry and hierarchy audit

All IDs/line numbers below refer to the frozen `GAME.unity`. The source shock
is two rigid MeshFilter/MeshRenderer objects, **not a SkinnedMeshRenderer**.

| Corner | Upper mesh transform / MeshFilter | Lower mesh transform / MeshFilter |
|---|---|---|
| RL | 69228 (`GAME:1014796`) / 89047 (`GAME:1343237`) | 54385 (`GAME:819964`) / 85919 (`GAME:1321341`) |
| RR | 45970 (`GAME:712606`) / 84160 (`GAME:1309028`) | 63605 (`GAME:942431`) / 87870 (`GAME:1334998`) |

Upper source attachment coordinates in body space are approximately
RL (−0.47000018, 0.14, −1.1499996), RR (+0.4699994, 0.1400002, −1.1499996).
The lower source attachment is under the moving IK arm: RL relative to 64371
is (−0.052000046, −0.027999997, −0.119999714); RR relative to 62886 is
(+0.052000046, −0.027999997, −0.11999983). Folding in the visible arm mesh frame
produces the builder's approximately (±0.052, −0.24, −0.028) arm-local anchors.
The existing reviewed positions are therefore not misplaced loose-part pivots.

Original orientation chains are separate at each end:

- RL upper IK 104129 (`GAME:1763560`): joints 47288 / 37727, target 58701 on the arm.
- RL lower IK 107837 (`GAME:4278996`): joints 64747 / 44461, fixed upper target 52143.
- RR upper IK 107102 (`GAME:3753360`): joints 49460 / 56157, target 57079 on the arm.
- RR lower IK 111709 (`GAME:6754155`): joints 44463 / 38408, fixed upper target 61124.

All four have `IsDamping=false`; the IK's damping option is not the physical
shock damping. The remake's `UpdateShockTargetRotations` keeps the two mesh +Z
axes coaxial while preserving opposite roll. `PresentInstalledShock` sets each
rigid mesh at its corresponding endpoint. The observed gap remains even with
these orientations correct because the endpoints are too far apart.

Source mesh files are below the frozen export's `Assets/Mesh/`:

| Mesh | Source GUID | SHA256 |
|---|---|---|
| `susp_rear_coil_top.asset` | `8d9d50ecb1914b540b5169d030e5261f` | `B4FD96EB52D268E657973AF239819D13AE84CB71DB45229E59DF126B7C4A3B1B` |
| `susp_rear_coil_bottom.asset` | `0e83db6312a310d4eaaf3eac92a5e0bd` | `D079AD89E4D3D4C911BB3E5742BE63296F47834D918B9B145058448C08A0D2FA` |
| `rear_spring.asset` | `127a720fb7356a04d854fbb20f2ca5e5` | `14368E67D36E1B1F3D345806BE8AC3A0D9E4394A6F444E6B5E5FB63A47076346` |

Both shock files have empty bind poses at line 25 and empty skin at line 33.
Their bounds at lines 124–126 give top local Z [−0.162688, +0.022855] and bottom
local Z [−0.015225, +0.326740]. Maximum axial reach toward each other is thus
0.162688 + 0.326740 = 0.489428 m at unit scale.

The spring is different: donor skins 101927 (RL, `GAME:1658069`) and 101757 (RR,
`GAME:1649303`) use bones 62179 / 38476 and 56620 / 46808 respectively.
`rear_spring.asset:25` contains two bind poses; the second has translation
e03 = 0.12885824 m. Weights from line 65 blend bones 0 and 1. Our
`SatsumaRearSuspensionPartPresentation` instead uses a MeshRenderer stretched
along the longest bounds axis between the two seats. This is an explicit
existing presentation approximation, not a new exact-skinning claim. It does
not explain separation of the independent rigid shock meshes and is not
rewritten in this issue 4 fix.

## Scoped correction contract

The implemented V1d.38 code change is limited to:

1. A project-owned `SatsumaRearSuspensionTravel.ResolveArmLimits` calculation for the three
   donor-evidenced carrier/travel profiles and their IK-derived direction limits.
2. `SatsumaRearSuspensionController` updating the existing installed arm hinge
   limits for the currently installed spring profile. `UpdateTravelLimits` runs before the
   early `!hasSpring` return, so a shock-only or empty corner cannot retain the
   previous spring's travel range.
3. `Phase1SatsumaBaselineBuilder` generating the initial no-spring profile limits
   instead of the arbitrary −32…+8° pair, with generated revision V1d.38.

The relevant project-owned files are
`Assets/Game/Vehicle/Assembly/Runtime/SatsumaRearSuspensionTravel.cs`,
`Assets/Game/Vehicle/Assembly/Runtime/SatsumaRearSuspensionController.cs`, and
`Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaBaselineBuilder.cs`.
The limit update precedes the no-spring return and also runs synchronously on
completed installation/removal events. It updates the existing joint without
changing the arm transform or recreating the hinge. The pivot is read from
the joint's fixed chassis-local connected anchor, not its moving arm endpoint.

No new physical shock joint, visual length clamp, mesh scaling workaround,
collision layer, spring force, damper coefficient or rear force owner is added.
The accepted chassis → physical arm → drum construction remains; rear NWH stays
disabled. Existing spring expansion/presentation, forces and damping are
unchanged. In particular, the current force model's no-spring early return and
compression-only force clamp are known implementation characteristics, not
silently changed into a claimed exact donor force solver.

This is donor-derived travel configuration adapted to the existing remake
physics, **not identical original suspension trajectories**. No part/mount/
fastener IDs, public assembly identity or save schemas are replaced. Front
suspension, wheel fitting and saves remain outside the change.

## Validation status and manual acceptance

Executed on Unity 6000.3.11f1:

| Check | Result | Evidence in `Logs/` |
|---|---|---|
| Baseline V1d.38 generation | PASS, exit 0 | `codex-rear-droop-v1d38-build.log` |
| Original faulty limits, focused red | 0/2, both reproduced the gap | `codex-rear-droop-v1d38-red.xml` / `.log` |
| Corrected focused PlayMode | 4/4, no skips, exit 0 | `codex-rear-droop-v1d38-green.xml` / `.log` |
| Expanded front/rear physics regressions | 38/38, no skips, exit 0 | `codex-rear-droop-v1d38-physics.xml` / `.log` |
| EditMode content/calculation regressions | Not executed: project was open on first attempt; retry was blocked by parallel vegetation compilation | `codex-rear-droop-v1d38-edit.log`, `codex-rear-droop-v1d38-edit-complete.log` |
| Isolated production Bootstrap check | Not launched after the compile blocker | No new result |

The builder reports 125 parts / 120 active loose parts, 117 mounts and 260
staged fasteners, unchanged from the accepted generated content. Separate toe
adjustment targets remain separate. No donor runtime dependency was introduced.

After 120 fixed steps, the focused physical fixtures measure the following on
both rear corners (stock rigid shock, no ground or wheel masking the result):

| Spring profile | Settled hinge angle | Attachment distance | Actual axial mesh overlap |
|---|---:|---:|---:|
| None | approximately −13.5476° | 0.443834 m | 45.59 mm |
| Stock | approximately −16.1376° | 0.454486 m | 34.94 mm |
| Extra-long | approximately −23.4853° | 0.484095 m | 5.33 mm |

All three profiles retain overlap. The live none → stock → none → long → none
case verifies immediate limit updates and the same HingeJoint instance across
spring changes. The broad suite also exercises grounded drums lifting the
chassis, spring deflection/rebound, no-spring contact, and accepted front
free-yaw, installation and contact behavior. These are executed remake tests,
not a new live donor capture or full suspension parity certification.

The later EditMode retry reports CS0103 in
`Assets/Game/Editor/Vegetation/MapVegetationContext.cs`: the concurrently authored
`VegetationStableHash` symbol is not yet available. The user explicitly noted
parallel work; none of those files was changed or rolled back. The successful
build/physics logs predate this external compile blocker and contain no C#
compile errors. Do not present them as proof that the concurrent project state
currently compiles. The 24 new EditMode cases are authored, not counted as
passed. Existing save regression coverage was selected for compatibility only;
no save repair or migration was attempted.

Commands use Unity `-batchmode -nographics -projectPath ...` with either
`-quit -executeMethod MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaBaselineBuilder.Build`
or `-runTests -testPlatform PlayMode/EditMode -testFilter ... -testResults ...`.
Exact commands/filters are retained in the logs. User acceptance on 2026-08-31
closes the reported issue 4: suspension extends without separating the shock
halves. The user did not enumerate every spring/corner/contact permutation;
do not turn that acceptance into a claim that all were manually verified.
It does not certify the deferred tests, general saves, wheel fitting or full
donor suspension parity. No new tests or runtime changes accompany this
documentation-only acceptance update.

Retained regression coverage:

- Both rear corners, stock spring + shock and shock without spring, airborne
  through settled droop: profile angle limits and overlapping rigid shock halves.
- Switching none/stock/extra-long profiles in one session, including removing
  the spring: limits update even when there is no spring force.
- Extra-long spring reaches its own donor droop envelope; it must not be
  silently restricted to the stock profile.
- Grounded compression/support and lift/release regressions retain contact and
  return to the profile's airborne range. No front physics regression or mesh
  stretch is accepted as the price of closing the gap.

Manual regression guidance for a fresh Bootstrap Play session: assemble the rear corner,
raise the body on the jack, and inspect the arm and the silver rod/upper shock
housing at full droop on both sides. Repeat with stock, without the spring,
and with the extra-long spring. Lower the jack and check ground response, then
raise it again. Watch for a real air gap, not merely the normal exposed rod.
Do not treat a test pass as the user's visual acceptance.

## Small next-session specification

Preserve the accepted V1d.38 rear droop fix and the accepted front suspension.
After parallel work compiles, rerun the EditMode filter from
`codex-rear-droop-v1d38-edit-complete.log` and run
`MSC.Tests.PlayMode.VehicleAssembly.ProductionSatsumaBootstrapPlayModeTests`
separately; the missing automated results remain outstanding despite manual
acceptance. The subsequent user report adds issue 4A (loaded compression)
before rear wheel installation (issue 5); follow its scoped audit/specification
linked above. Neither is implemented in this acceptance record.
If droop/gap regresses, capture active profile, hinge limits/angle,
attachment distance, actual axial mesh intervals and hinge anchor error before
changing pivots or geometry. Keep the existing front acceptance and rear force
ownership intact. Work on one reported issue at a time and obtain the user's
acceptance before moving on. General save repair remains deferred until the
whole suspension is accepted; issue 4 acceptance does not lift that boundary.

Changed project-owned files: the three runtime/builder files above; new
`Assets/Game/Tests/EditMode/LegacyImport/SatsumaRearSuspensionTravelTests.cs`
and `Assets/Game/Tests/PlayMode/VehiclePhysics/SatsumaRearDroopPlayModeTests.cs`
with their `.meta` files; this report and the four `Docs/Porting/` audit,
matrix, system-map and ledger records. The helper also has a new `.meta`.
Private ignored baseline content was regenerated. No migration is required;
general save reliability remains deliberately deferred by the user.
