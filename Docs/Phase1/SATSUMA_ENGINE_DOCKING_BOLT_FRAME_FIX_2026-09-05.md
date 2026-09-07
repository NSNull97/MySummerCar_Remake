# Engine mounting bolts: chassis frame applied twice

Date: 2026-09-05. Scope: the three existing `fastener.satsuma.engine-assembly.boltpm-1/2/3` markers only.
Classification: `MountPointSource`, `ConfigurationTransferred`, `Reimplemented`.

## Confirmed defect

The three bolt meshes/travel were correct, but the marker **parent coordinate
frame was not**. Earlier mesh/travel checks proved a zero child rest pose and
unit marker scale, not the absolute location of the marker. Their previous
"poses preserved" claim was insufficient and preserved this existing defect.

`Phase1SatsumaBaselineBuilder.AddEngineMount` obtains marker position/rotation
with `CreateLoosePartFastenerEvidence(... satsumaRoot.TransformId, ...)`.
`CreateLoosePartFastenerEvidence` calls `GetTransformRelativeTo` with that
reference. Those are SATSUMA-local poses. The later generic marker construction
parents the markers below the solved engine assembly mount and writes those
same values as mount-local poses. Therefore the solved block pose is applied a
second time, rotating forward distance into height.

The current generated engine mount has chassis-local position
`(-0.0015974252,-0.0347443372,1.31011021)` and quaternion
`(0.00795983151,-0.6982286,-0.7158303,-0.0007621968)`.

| Bolt | Correct chassis-local position, m | Incorrect resulting chassis-local position, m | Distance error |
| --- | --- | --- | --- |
| 1 | (-0.224999,-0.212374,1.502543) | (0.210442,1.474810,1.137925) | 1.780 m |
| 2 | (0.225000,-0.212374,1.502543) | (-0.239500,1.470299,1.132318) | 1.784 m |
| 3 | (0.000000,-0.237436,1.048367) | (-0.009531,1.019168,1.098794) | 1.258 m |

These are marker/head stage-zero locations, not the proximity trigger centers.
The independent triangulation trigger CSV is unchanged.

## Direct original evidence

Read-only frozen `GAME.unity`, SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Exact parent chain: marker -> `_Motor` Transform43037 -> installed subframe
Transform47402 -> identity `Chassis` Transform38351 -> SATSUMA Transform64200.
Donor IDs/hierarchy names remain provenance only, not runtime lookup.

| Stable suffix | Assembly FSM | Marker Transform / GameObject | Marker local position under43037 | Marker local quaternion |
| --- | --- | --- | --- | --- |
| 1 | 104917 | 59373 /23317 | (0.22501259,-0.10993704,-0.18478249) | (-0.9659261,1.1313301e-8,-4.2221988e-8,-0.258818) |
| 2 | 111211 | 70667 /34612 | (-0.2249874,-0.1099374,-0.18478276) | (-0.9659263,1.6868266e-7,-8.438868e-8,-0.2588176) |
| 3 | 107768 | 52875 /16814 | (1.26986315e-5,-0.1349985,0.26939392) | (-1.0442067e-6,0.13052534,-0.991445,-6.5869597e-7) |

Parent43037: position `(-1.3051683e-5,0.117760345,0.1392753)`, quaternion
`(0.7071064,-2.843851e-8,-7.3660664e-9,0.70710725)`.
Parent47402: position `(0,-0.24171245,1.2000003)`, quaternion
`(3.8274667e-8,0.70710725,0.7071064,-5.934715e-8)`.
Both have scale1. Markers have scale1.1; the generated presentation retains
that on the visible child, with marker scale1 and22mm stage0..8 travel.

The checked-in reviewed chassis poses retain the original import's float32
rounding. Independent composition from these raw local transforms agrees within
2 micrometers. Bolt3 quaternion signs may differ; they describe the same rotation.

## Bounded correction

`Phase1SatsumaEngineDockingAuthoring.ApplyMountBoltPresentation` now converts:

`localPosition = mount.InverseTransformPoint(chassis.TransformPoint(donorChassisPosition))`

`localRotation = inverse(mount.rotation) * chassis.rotation * donorChassisRotation`

Only each marker's local position/rotation changes. The helper accepts exactly
the known old chassis-as-local pose or the corrected pose, and preflights all
three before writing any marker. Unknown offsets are rejected. Repeating the
existing docking refresh makes zero changes. The same hook already runs after
generic marker construction in a full build, so the large Builder need not be
rewritten.

Preserved: component references, IDs/counts, fastener stages/latches, short-bolt
mesh, physical scale1.1, travel1.1, visible child zero rest pose, colliders, engine
installation pose, proximity points, save DTOs, compound physics, and all other
fastener markers. No save migration is necessary.

## Validation and handoff

- Added `SatsumaEngineDockingPoseTests`: original transform-chain comparison;
  known-old and corrected frames at both identity and translated/rotated vehicle
  transforms; actual visible world position plus shaft axis; repeat-zero;
  preservation of engine mount, graph mutation count and unrelated fasteners;
  rejection of an unknown third marker before changing either known marker.
- Isolated helper+fixture compilation passed:0 errors/0 warnings (the temporary
  project suppresses only the expected same-type local-vs-compiled-reference
  CS0436 shadow warning).
- Executed the pure raw-donor transform-chain assertion outside Unity: all3
  marker poses passed. This is not a Unity scene/prefab test run.
- `git diff --check` clean. This agent did not launch Unity or edit generated
  assets. Root owns scoped refresh and actual Unity tests.

Next validation: existing `Refresh Engine Docking Only`, then the new fixture
and manual check that all three bolts appear at the subframe supports as the
free engine reaches the original proximity points.
