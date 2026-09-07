# Engine piston-cap presentation and rocker-cover duplicate retirement

Coordinated final validation: **799/799 EditMode, 74/74 PlayMode passed**;
native slot-01 was restored on a copy without rewriting user saves. Authoring
repeat changed0. Manual acceptance remains pending. The authorship-stage notes
below are supplemented by [the final packet report](SATSUMA_ENGINE_REPAIR_PACKET_2026-09-05.md).

Status: `BehavioralReference / Reimplemented / ConfigurationTransferred`.
Source and isolated tests prepared; Unity execution and generated integration
belong to the coordinating task and are not claimed by this report.
Temporary cap/bolt meshes remain `TemporaryDirectImport`, not production art.

## Scope and dependency/compatibility audit

This packet fixes only the four missing conrod-cap visuals and the rocker
cover's six pairs of stock/GT duplicate mounting fasteners. It does not turn a
cap into a separate part, add a physics collider, modify the rocker shaft's 13
current mounting entries, change engine wear, or implement valve adjustment.

Reviewed existing foundations: `PartInstance` installed state, generated loose
presentation import, `MountPointDefinition`, `FastenerGroupDefinition`,
`AssemblyFastenerInteractionTarget`, `VehicleAssemblySaveData`, and the existing
controller's retired steering-fastener migration/additive restoration pipeline.
No foundation API/DTO replacement is needed: one new explicitly bound presenter,
one exact save-shape migration helper, and one scoped Editor authoring extension.
The root task owns the small full-builder and save-restore integration hooks.

All six retained stable IDs and all part/mount IDs remain unchanged. Six retired
definition assets remain reserved on disk; only their duplicate marker trees
are removed from the authored prefab. This is not a global count-based cleanup.
The helper validates the exact old12/new6 definition and target sets, 7 mm,
eight stages, removal ownership, owner/alternatives, matching source positions,
paired rotations and old/new latch configuration before mutation.

## Frozen original evidence

Read-only source:
`raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`
in external donor staging. SHA-256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Byte offsets below are zero-based. Source names/IDs are provenance and are never
used for runtime object lookup.

### Conrod caps already exist, but the imported initial visibility is frozen

| Piston | Loose root Transform | Inactive `Bolts` Transform | Cap Transform | Imported renderer suffix | Assembly FSM / byte |
|---|---:|---:|---:|---:|---|
| 1 | 53913 | 63015 | 63249 | 79638 | 106238 / 82412856 |
| 2 | 54867 | 69049 | 36297 | 72422 | 107760 / 113843367 |
| 3 | 56076 | 70132 | 43857 | 74433 | 105370 / 66778562 |
| 4 | 44101 | 67227 | 60523 | 78884 | 113228 / 213676495 |

All four caps use mesh source GUID
`f48a6a1b2ea4b164d9d920ab78966d86`. Their cap GameObjects are locally active in
the donor, but the owning `Bolts` GameObject starts inactive. The existing
`BuildLoosePartPresentation` flattens each renderer and copies its effective
initial active state (`IsTransformActiveBelow`), producing a permanently hidden
`conrod_bearing_<renderer>` in each generated loose-part presentation.

All four `Assembly/Assemble` states were read directly. The identical action
sequence gets the current Part's child `Bolts`, activates it, then marks the
part Installed. `ActivateGameObject.activate` parameter 16 points to byte14,
size2, literal `0001` in
`0001536574757000000001000100010001000000010000`.
This is an **installation** visibility switch, not a fastening threshold.
Piston1 `Removal` FSM109160 at byte139179124 explicitly disables GO26962
(`Bolts`) in `Remove part`, activate parameter21, byte46, literal `0000`.
The other three removal switches were not separately decoded in this packet;
the common removal presentation behavior is applied symmetrically, consistent
with the identical four assembly contracts and hidden loose donor roots.

`SatsumaPistonCapPresenter` now binds explicit part/cap references and displays
the cap iff that piston is installed, including on an engine still on the
floor. It hides it when removed. It does not alter part identity, mesh pose,
material, mass, body state or fasteners; refresh also covers restored states.

### Rocker cover: six positions, two alternative copies per position

Full identity/mesh evidence is in
`SATSUMA_ENGINE_FASTENER_OWNERSHIP_AUDIT_2026-09-05.md`.
Stock root66479 and GT root40984 each own six 7 mm short bolts, source mesh
`aec6c756751308a4d830708366ad5cdb`. The old shared mount concatenated both
alternatives and serialized 12 fasteners instead of six.

Prefix: `fastener.satsuma.cylinder-head-rocker-cover.boltpm-`.

| Canonical stock suffix | Retired GT suffix | Shared mount-local position (metres) |
|---:|---:|---|
| 2 | 5 | (-0.13673234, -0.0513382, -0.034412872) |
| 4 | 11 | (-0.048354626, -0.0513382, -0.034416568) |
| 8 | 3 | (0.12149751, 0.062173855, -0.03440928) |
| 9 | 7 | (-0.052220702, 0.062173855, -0.034412738) |
| 10 | 6 | (-0.13894153, 0.0621729, -0.034412798) |
| 12 | 1 | (0.122877955, -0.0513382, -0.034416866) |

All twelve frozen source Transform records were checked directly: each stock/GT
pair has exactly the same local position. The old generated GT markers differ
from their stock counterparts by 0.037087, 0.037817, 0.060688, 0.054429,
0.055267 and 0.058145 mm respectively. This is import precision loss, not a
different donor mounting offset: `CreateLoosePartFastenerEvidence` calls
`DonorUnitySceneModel.GetTransformRelativeTo`, which uses float world matrices
(`referenceWorld.inverse * targetWorld`). The GT root40984 starts at
(1552.808, 4.547, 741.829), whereas stock root66479 starts at
(1.0498486, 0.7941661, -13.848479).

The compatibility guard accepts either the tightly coincident source form
(10 micrometres) or the six exact reviewed legacy-import GT positions
(1 micrometre); it does not generally widen positional or angular tolerance.
Canonical stock positions remain tightly source-validated and are not moved.
Regression cases cover all six imported aliases, idempotence, and rejection of
unreviewed GT offsets of 30 and 200 micrometres before any mutation.

The new mount/group arrays use canonical suffixes `[2,4,8,9,10,12]` in that
order. The accepted stock/GT part alternatives remain unchanged. Each retained
definition stays 7 mm, maximum stage8; total maximum changes96→48. Retained
marker poses, presentation base poses, stage travel, tools and materials are
preserved; only their mesh becomes the donor-proved short bolt.

Stock `BoltCheck`112846 at byte206590512 and GT105477 at byte69177589 were
checked at action level. Their `Bolts OFF` compares Tightness with BoltedYES2:
equal and greaterThan are FINISHED→`Bolts ON`. `Bolts ON` compares Tightness
with BoltedNO0: equal and lessThan are FINISHED→`Bolts OFF`. Thus **ON>=2,
OFF<=0**, not the previous generated compatibility ON1/OFF0.

## Exact old-save migration

`SatsumaRockerCoverFastenerMigration.TryMigrate(source, targetDefinition, ...)`
is called before the existing global shape-count/additive migration guards.
Only the reviewed cover mount is touched:

1. Old target still owns the exact12: no conversion is attempted into an
   incompatible prefab. New target must own the exact6 at max48/ON2/OFF0.
2. Canonical6 source or source with no cover records: no-op; the normal
   validator remains responsible for the rest of the save.
3. Legacy conversion requires every one of the exact12 IDs once, a single mount
   record, valid individual insertion/seating/stage states, and a group record
   for schema2. Partial, unknown, duplicated, inconsistent or out-of-range
   state is rejected without modifying source DTOs.
4. Old latch is validated against the old ON1/OFF0 state. Each physical pair
   becomes `stage=max(stock,GT)`, `inserted=stock OR GT`, `seated=stock OR GT`.
   This preserves the strongest saved fastening per physical bolt without
   double-counting, resetting or summing incompatible duplicate turns.
5. Valid saved latch history is retained when consistent with the new group;
   T1/Bolted=true remains legitimate hysteresis history after raising ON to2.
   A canonical new T1/Bolted=false save is also valid and is not rewritten.
   Empty mount entries remain entirely absent/zero. Schema1 may lack a latch
   section and leaves normal legacy reconstruction authoritative.
6. A new wrapper/fastener array is returned; source arrays/DTOs are not modified.
   Unrelated sections and optional part extensions are preserved. The save
   storage layer's original payload/backups remain recoverable. Reapplying the
   migration returns the canonical payload unchanged.

Integration count consequence: global280→274. Historical252/260 payloads become
246/254 if this alias conversion runs first. The parent controller's existing
known additive transitions must recognize the exact documented states, not a
blanket count tolerance. Existing presentation passes with a strict280 target
contract must run before this dedupe or explicitly admit the canonical shape.
Repair outline/selection renderer arrays after marker retirement if an outer
host had previously cached those renderer references.

## Validation prepared

`SatsumaEngineCapAndCoverTests` covers exact canonical IDs/group/mesh, retained
definition and pose invariance, caps before/install/remove/restored state,
idempotence and binding repair, packet fail-before-mutation, all six migration
pairs, insertion/seating union, source immutability, schema1/empty cases,
old-prefab no-op, T1 latch history, unrelated state, and corrupt/partial aliases.
No Unity process was launched by this subtask. Coordinating task records the
executed test totals and final generated integration results.

Manual check: install all four pistons on the loose block (caps appear before
turning either nut), remove one (cap hides), and exercise stock/GT covers with
six unique 7 mm bolts. Load an old12 cover save and verify retained pair stages.
