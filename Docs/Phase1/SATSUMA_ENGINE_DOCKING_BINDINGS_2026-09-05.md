# Engine physical docking — scoped authoring and workflow tests

Coordinated final validation: **799/799 EditMode, 74/74 PlayMode passed**;
native slot-01 was restored on a copy without rewriting user saves. Authoring
repeat changed0. Manual acceptance remains pending. The authorship-stage notes
below are supplemented by [the final packet report](SATSUMA_ENGINE_REPAIR_PACKET_2026-09-05.md).

Date: 2026-09-05. `BehavioralReference`, `ConfigurationTransferred`, `Reimplemented`.

## Frozen evidence and frame mapping

Canonical source is the locked read-only `GAME.unity` staging export, SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.

The existing reviewed
`Assets/Game/LegacyImport/Manifests/Phase1SatsumaV1dEngineMountTriangulationAudit.csv`
contains three paired points. The existing builder writes `BlockLocalPoint`
relative to the **engine block source root**, and `ChassisLocalPoint` relative
to the **Satsuma source root**, not the subframe or engine mounting marker.
The scoped authoring therefore binds `block.transform` and `assembly.transform`.
It preserves the already solved installation pose and never solves a replacement
pose during gameplay.

| Fastener suffix | Assembly FSM | Chassis trigger transform | Block trigger transform | Block-local point | Satsuma-local point |
| --- | --- | --- | --- | --- | --- |
| boltpm-1 | 104917 | 38857 | 51236 | (0.222330689,0.150512531,-0.161185935) | (-0.225233659,-0.2019812,1.45231676) |
| boltpm-2 | 111211 | 61244 | 54063 | (-0.227155089,0.145025179,-0.165830344) | (0.229094878,-0.2019812,1.45231688) |
| boltpm-3 | 107768 | 48877 | 44595 | (0.000956096163,-0.237702727,-0.205782264) | (0.0000128941128,-0.2342809,1.07033014) |

The CSV's existing rigid-fit maximum residual is0.00391427241m. The new authoring
reads its three rows, validates exact row/component/trigger identity, finite
vectors and residual<=0.01m, and stores ordinary project-owned vector references.
Donor object IDs and CSV paths are Editor-only provenance, not runtime lookup.

FSM104917 was independently decoded in this packet:

- `Check for Part collision`: hide its bolt; read engine Data.Bolted; if already
  bolted, `Show bolts`. Otherwise `GetDistance` compares the paired triggers
  against `DistanceTolerance=0.1m`; strictly less enters `Ready`.
- `Ready`: show the bolt and keep measuring. Strictly greater than0.1m exits;
  equality has no transition, so it retains the prior visibility state.
- `Show bolts`: while engine Data.Bolted is true, show the bolt regardless of
  proximity; false returns to the distance test.

The coordinated runtime uses a pending single turn while the engine is still a
loose rigidbody. Aggregate2 enters the installed graph; installed latch is
ON2/OFF0. Three existing Wrench11 bolts retain eight stages each and MAX24.
Ordinary click handoff is explicitly excluded through `AssemblyPhysicalDockingOnly`.
No automatic installation is caused merely by touching a trigger.

## Three engine-mount bolt meshes and travel

These are **short bolts**, not the generic nut fallback. All three use mesh
source `aec6c756751308a4d830708366ad5cdb` (`bolt`). Exact reviewed relationships:

| Suffix | Bolt GameObject | Marker transform | Actual child transform / MeshFilter | Screw FSM |
| --- | --- | --- | --- | --- |
| 1 | 23317 | 59373 | 47130 / 84414 | 110705 |
| 2 | 34612 | 70667 | 63305 / 87798 | 114005 |
| 3 | 16814 | 52875 | 42978 / 83528 | 108873 |

The raw donor markers are uniformly scaled1.1; each actual child has unit scale,
zero position and identity rotation. `Setup2 -> Save data2` resolves `ThisBolt`
with `GetChild` before the stage states: the stale serialized template reference
does not select a different runtime bolt. Stages0..8 use localZ=-0.0025m*stage,
and their marker rotation phase advances45 degrees per stage.

The accepted importer puts that physical scale on the visible child and keeps
the project marker at unit scale. The current prefab already has child scale
approximately1.1, which is validated and preserved. The scoped helper changes
only the legacy nut mesh to this short bolt and the separately serialized travel
multiplier from1 to1.1. Full stage8 travel is therefore0.022m. It adds no base
offset, does not double-scale the marker to1.21, and preserves rest poses,
colliders, material, target identities and the existing stage rotation.

All three identities/frames are preflighted before any of these three mesh/travel
updates. An unexpected mesh, scale, rest pose or travel value is rejected, not
silently overwritten. Generated tests assert short mesh, unit marker, physical
child scale1.1, travel1.1 and full22mm stroke for every bolt.

## This bounded packet

- New Editor helper
  `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaEngineDockingAuthoring.cs`.
  `ApplyToInstance(assembly)` is called after ordinary targets and other mounting
  rules. It binds one `AssemblyEngineDockingState` on the existing block, the
  physical-only marker on the existing `mount.satsuma.engine-assembly`, and
  `ConfigurePendingDocking(state)` on the three existing targets. No part,
  mount or fastener stable IDs change. Existing repeat bindings are validated
  without initializing the graph or silently retargeting them.
- Scoped refresh entry point `RefreshEngineDockingBatch`; menu
  `Tools/MSC Remake/Phase 1/Satsuma/Refresh Engine Docking Only`.
  It rejects Play, validates frozen manifest identity, backs up the prefab to
  ignored Logs before saving, and does not run the full donor import.
- New `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaEngineDockingTests.cs`:
  proximity alone, first-stage dynamic body, aggregate second turn through
  either same or different bolts, save capture inside PartInstalled callback,
  wrong tool/away rejection with unchanged pending state and pose, exact
  distance boundary history, pending save/old defaults/invalid data, unfastening
  at the current world pose with retained internal oilpan, click-handoff
  exclusion, and generated pair/target/group bindings.

Root owns the docking runtime and fastener extension; engine-mount agent reviews
validation and the atomic `CommitPendingToInstalled` callback. Engine-fastener
agent owns shared controller/save hooks. The commit callback must run after
`PartInstance.InstallAt` but **before** `PartInstalled` is published: an event
subscriber saving the game must never capture installed engine +zero group
stages +pending turns. The workflow test specifically guards this ordering.

## Validation status

This agent did not launch Unity or execute the tests. Root's single-process
compilation/refresh/test pass and an in-game physical assembly check are still
required. No source changes were made to donor files, shared Player code,
VehicleAssemblyController, save DTOs, or Builder by this packet.

Manual acceptance: put the free engine near its three supports, confirm only
nearby bolts become usable, first turn does not freeze it, aggregate second turn
attaches it, later tightening remains ordinary bolt operation, and fully
loosening all three releases it in place while engine internals remain assembled.
External connections retain their existing removal blocking rules.
