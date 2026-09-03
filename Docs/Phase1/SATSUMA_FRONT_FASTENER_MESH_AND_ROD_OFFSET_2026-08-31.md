# Satsuma issue 3 — front bolt meshes and steering-rod endpoint

Revision `11A-V1d.35`, 2026-08-31. Continues issue 3; manual acceptance is
pending. Issues 4–6 and the accepted front ground-contact behavior are not
changed. This is a geometry correction, not full steering-state parity.

## Read-only original evidence

Authority: `msc-world-baseline-04a1.1-c3f2f337`, frozen
`raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets`.
`_Scenes/GAME.unity` SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
No donor installation changes or new live donor capture.

The original MeshFilter, not the object name or real-car expectation, defines
the visible fastener. All 30 reviewed front markers have one direct visual
child at position zero, identity rotation and unit scale, using material
`98697bae08a8c114ba9774c487f2658d`.

| Connection | Count, both sides | Original mesh |
|---|---:|---|
| Strut upper, 10 mm | 6 | `bolt2`, nut with a through-hole |
| Strut lower, 9 mm | 8 | `bolt`, short bolt |
| Rod outer joint, 12 mm | 2 | `bolt`, short bolt |
| Spindle, 12 mm | 2 | `bolt`, short bolt |
| Wishbone, 10 mm | 4 | `bolt3`, long bolt |
| Subframe, 10 mm | 4 | `bolt`, short bolt |
| Steering rack, 9 mm | 4 | `bolt`, short bolt |

Mesh identities and SHA256:

- `bolt`: `aec6c756751308a4d830708366ad5cdb`, 33.888 mm source length;
  `E4E11C64E0D183F9F1E8A7B1208223736A58267729970768969C9C692851B862`.
- `bolt2`: `e711c8a15b1135c4089caad19b8f56e8`, 7.414 mm source thickness;
  `91A16B72A229908A9490F4BB3B93E62B6B80FAEE38509A6AB55178AB8001616A`.
- `bolt3`: `bd64aade39680ac43a380f1c62373e0b`, 61.534 mm source length;
  `7D042D3033F800DA45C97B806D1CF9C78176EABE92CA5303B1122828B65E0A59`.

V1d.34 used the nut mesh for every marker: 24 of these 30 front visuals were
wrong. V1d.35 validates the actual donor child mesh and preserves the source
origin/scale. Other assemblies keep their previous visuals pending their own
sequential review; this is not a whole-car fastener audit.

## Missing 50 mm in the steering-rod hierarchy

FL outer bone T39613 (GAME line 628103) belongs to OFFSET T58309 (873650),
which is translated `(+0.05,0,0)` under hub/Spindle T70301 (1028430).
FR outer bone T66023 (973320) belongs to OFFSET T57398 (858267), translated
`(-0.05,0,0)` under hub T54171 (817268). Both OFFSET rotations are identity
and scales are one. The builder incorrectly treated bone-local coordinates
as hub-local, dropping that parent translation on each side.

Correct hub-local endpoints in metres:

- FL `(0.11290412, 0.035795197, -0.11750424)`.
- FR `(-0.11423402, 0.033416495, -0.1176604)`.

The existing two-bone skins are retained: donor renderers 101852 / 101683,
outer bones 39613 / 66023. The endpoint follows the hub hierarchy while the
rod is installed, regardless of fastening stage. A bone origin and a bolt-head
origin are different reference points: do not force their positions to match.

## Connection timing is a separate mechanism

Rod Assembly FSMs 111295 / 110868 activate the rod, the 12 mm joint fastener
and the separate 14 mm toe adjuster; they do not snap the hub themselves.
Screw 113544 / 113958 changes Stage 0–8. Rod BoltCheck 111004 has hysteresis:
Bolted OFF -> ON at 8; ON -> OFF at 0, so loosening from 8 to 7 does not
immediately disconnect it.

SteeringFL/FR FSMs 107556 / 107558 (GAME lines 4055614 / 4057273) connect when
the rod is Bolted AND the stock or rally strut is Installed. Before connection
the donor wheel has a chassis-connected Y-axis hinge with limits +/-33 degrees
and steering disabled. On entering connected it removes the hinge/Rigidbody,
sets wheel yaw to saved `Data.Alignment`, and restores local `WheelPos`.
The rod Data load branch also restores alignment; that is not evidence of a
snap on every normal installation.

The current front NWH adapter does not recreate this free-yaw hinge and
Stage-8 connection transition. This patch removes the evidenced visual gap;
it does not claim to implement that broader physical behavior or toe adjustment.

Follow-up: V1d.36 implements the bounded steering/adjuster work. Its later
evidence supersedes this report's open-item wording and the generalized reload
assumption below: see `SATSUMA_FRONT_ALIGNMENT_PARITY_2026-08-31.md`.
No arbitrary yaw reset, extra Rigidbody, spring force or centre-of-mass change.

Another recorded difference, not changed here: donor stage animation translates
the child by `Z=-0.0025*Stage`, scaled by its parent marker (9 mm -> 18 mm full
travel, 12 mm -> 24 mm), and replaces the marker's local Euler Z with
`45*Stage`. The existing remake animates the visual child with unscaled 20 mm
full travel and relative rotation. Review this independently before claiming
exact fastener seating parity; changing all 260 targets here would exceed scope.

## Validation and compatibility

Executed with Unity 6000.3.11f1 after the user closed the interactive Editor:

- Build exit 0, `PHASE1_SATSUMA_BUILD_OK version=11A-V1d.35`: 260 fasteners,
  117 mounts, 125 loose parts, 120 active loose parts.
- EditMode **111/111 passed**, no skips: generated content 29, fastener mesh 6,
  front save migration 5, BoltCheck parity 5, generic assembly 23, NWH 30,
  native-save integration 13.
- Isolated physics PlayMode **20/20 passed**, no skips: front fastener 2,
  installed-part physics 18, including actual baked rod-end vertices during
  ground compression and steering-frame movement.
- Isolated production Bootstrap **1/1 passed**, including 260 live targets.
- Scoped whitespace checks and CSV parsing passed (parity 19 columns, ledger 11).

The previously failing rear-lift test passed this run: chassis 0.292074 to
0.323919 m, a 31.845 mm rise. Previous V1d.34 run measured 10.97 mm. No rear
forces or assertion changed, so one pass does not establish a rear fix or
manual acceptance. The difference between runs remains uninvestigated in this
front-only slice.

Artifacts: `Logs/codex-front-mesh-v1d35-build.log`,
`codex-front-mesh-v1d35-edit.xml`, `codex-front-mesh-v1d35-physics.xml`,
`codex-front-mesh-v1d35-bootstrap.xml` and corresponding logs. Build command:
`-batchmode -nographics -quit -executeMethod
MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaBaselineBuilder.Build`.
Test commands: `-batchmode -nographics -runTests -testPlatform EditMode` or
`PlayMode`, with the fixture filters above; Bootstrap is run separately to
avoid the previously observed combined-run lifecycle problem. No visual donor
comparison or user's in-game acceptance is claimed.

Tests cover donor MeshFilter identity for all 30 front targets; the complete
source endpoint-parent-hub chain; rendered outer skin vertices after live
ground compression and steering-frame rotation. Existing fastener, save and
front-contact tests ran against the rebuilt private prefab.

No stable IDs, fastener counts (260), mounts (117), save DTOs, latch policies,
installation prerequisites or accepted 00–08A APIs change. V1d.34's eight-ID
save migration remains intact. New meshes are ignored TemporaryDirectImport
presentation, not production art or donor runtime code.

Changed code/test files:

- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaBaselineBuilder.cs`
- `Assets/Game/Tests/EditMode/LegacyImport/Phase1SatsumaGeneratedContentTests.cs`
- `Assets/Game/Tests/EditMode/LegacyImport/SatsumaFrontFastenerMeshTests.cs` and `.meta` (new)
- `Assets/Game/Tests/PlayMode/VehiclePhysics/SatsumaFrontFastenerPlayModeTests.cs`

The ignored Satsuma assets were rebuilt. This report, the V1d.34 follow-up link,
porting audit/matrix/system map/ledger and parity rows P1.CAR.003/P1.CAR.008
record the correction and remaining boundaries. No runtime C# was changed.

## Manual check and small handoff specification

1. Rebuild using `Tools/MSC Remake/Phase 1/Satsuma/Build V1d Baseline`, then start
   a fresh Play session so the regenerated prefab is used.
2. Check both sides: upper nuts, lower bolts, long wishbone bolts. Install strut
   and rod, tighten the 12 mm fastener fully; inspect the rod-end/bracket fit
   while raising/lowering the car and turning the steering.
3. Remove/reinstall the rod and reload a save: no duplicated visuals or lost
   tightening. Report remaining mismatch with side and fastener stage.
4. Stay on issue 3 until user confirmation. If physical alignment still differs,
   implement the donor connection condition and 8/0 latch via the existing NWH
   boundary; first document compatibility, save handling and free-yaw ownership.
   Keep the accepted ground-contact solver as sole contact authority. Test
   0->7->8->7->0, missing/removed/reinstalled strut and save/load without a new
   random alignment on reload. Do not confuse the 14 mm adjuster with the joint.

Only after issue 3 is accepted may the next item be issue 4 (rear droop/shock
separation). Wheel placement and rear physics are untouched by this patch.
