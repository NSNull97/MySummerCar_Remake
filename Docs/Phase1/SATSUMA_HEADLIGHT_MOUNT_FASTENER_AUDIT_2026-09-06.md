# Headlight mounting bolts: missing real lighting prerequisite

Initially read-only `BehavioralReference` / `MountPointSource` follow-up;
the subsequently approved bounded source implementation is recorded below.
No Unity was launched by this audit/implementation agent. The lighting implementation session identified that BeamsShort
FSM106670 requires each headlight database's Bolted state; the current two
empty mount groups can never meet that prerequisite. Do not weaken the gate.

Source: frozen external staged GAME.unity, SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
The blocks below were read directly and action parameter bytes decoded, not
inferred only from part names or a remembered assembly guide.

## Exact contract

| Side | Part GO / Transform | Bolts parent | Database | BoltCheck / byte offset | Removal |
| --- | --- | --- | --- | --- | --- |
| Left | 9838 / 45890 | 60504 | HeadlightLeft1 GO557 | 106921 / 94918533 | 106922 |
| Right | 27388 / 63443 | 52988 | HeadlightRight1 GO5205 | 111828 / 187810112 | 111829 |

Each side has exactly **two Wrench7 bolts, max stage8 each**. Both active
`Bolts OFF` action payloads are identical apart from the database owner:
FloatClamp Tightness0..16, SetFsmBool(Data.Bolted=false), FloatCompare
Tightness against variable BoltedYES=2. Equality/greater enters ON.
`Bolts ON` sets true and enters `Check break`, which compares Tightness to
BoltedNO=0; equality/less returns OFF. The source group contract is therefore
**max16 / ON2 / OFF0**, not a full16-tightness light-enable condition.
Intermediate latch history is retained during ordinary live loosening.

Both Removal `Requirements` states read their own database Bolted and a null
db_RemoveReq1; BoolNoneTrue permits removal. No grille-removal dependency was
established by these states. Preserve the separately accepted purchased-bulb
retained-child behavior; these mounting bolts do not represent bulb threads.

There is also a donor speed-based Chance/break branch (Check break compares
SpeedKMH against50). Its complete stochastic/crash behavior is not ported or
claimed verified by the bounded missing-bolt proposal.

## Binding table

New IDs follow the accepted existing convention and are allocated only by
the approved four-bolt authoring extension below. Mount IDs already exist:

```text
mount.satsuma.headlight-left
mount.satsuma.headlight-right
fastener.satsuma.headlight-left.boltpm-1 / -2
fastener.satsuma.headlight-right.boltpm-1 / -2
```

Numbering is ascending marker Transform ID, as in the stock21 packet; right
source sibling order is reversed and must not silently change that numbering.
Both Bolts parents are identity relative to their respective part, so the
coordinates below are directly **part-local**, suitable for the existing
installed mount pose; they are not loose-world coordinates.

| Proposed suffix | Marker / Screw FSM | Visible / MeshFilter | Position (metres) | Quaternion (x,y,z,w) | Physical scale |
| --- | --- | --- | --- | --- | --- |
| headlight-left.1 | 47993 / 107507 | 64802 / 88117 | (-0.09532297, 0.018868791, 0.0036582127) | (-0.707106, -1.9441359e-8, 0, 0.70710766) | (0.7, 0.6999998, 0.69999987) |
| headlight-left.2 | 64196 / 112081 | 59038 / 86850 | (0.09062467, 0.018868607, 0.0036573187) | (-0.70710605, -1.9441355e-8, -2.9802315e-8, 0.70710754) | (0.6999999, 0.6999998, 0.69999975) |
| headlight-right.1 | 39354 / 105056 | 59827 / 87027 | (-0.095091924, 0.018869085, 0.0036579706) | (-0.70710605, 1.8894204e-7, -1.490116e-7, 0.70710754) | (0.7, 0.6999999, 0.69999987) |
| headlight-right.2 | 58481 / 110431 | 63228 / 87775 | (0.09085509, 0.018869027, 0.0036576726) | (-0.70710605, 1.8894204e-7, -1.490116e-7, 0.70710754) | (0.7, 0.6999999, 0.69999987) |

All four meshes are the short bolt
`aec6c756751308a4d830708366ad5cdb`, **not a nut**. Each visible child has
identity local pose and unit scale in the donor. All36 individual stage states
were decoded: SetPosition and SetRotation both enabled (`0101`), local space1,
z=-0.0025*stage, zAngle=45*stage. With unit project target root and physical
scale on its visible child, per-target travel multiplier is source scale.z,
approximately0.7:14mm at stage8. Use inserted-on-install and required-for-removal
on these four real mounting definitions. Do not retune accepted other bolts.

## Minimal authoring and compatibility plan

Prefer an isolated four-binding headlight helper after the existing Stock21
helper, rather than changing the already tested Stock21 cohort's identity.
Both cohorts together are the reviewed late-stock25 packet. New generic graph
count is298+4=302; fixed parts126 and mounts124 remain unchanged.

Authoring must preflight both whole headlight groups and all four source
meshes/poses, persist newly created FastenerDefinitions **before** assigning
them to persistent MountPointDefinitions, then bind typed targets. Leave old
source bolt copies disabled; do not activate the original whole Bolts subtree
alongside the new targets. Preserve all existing owner, sequence and retained
bulb arrays. Accept only exactly empty old or exactly authored2+2 target shape;
partial/foreign entries reject before mutation. Verify repeat0 and saved/reloaded
references, including the previously discovered transient-reference failure.

The existing content migration in
`VehicleAssemblyController.ContentMigration.cs:22` currently treats the first
six mounts as one exact21 cohort. Simply changing its `present==21` constant
to25 would reject real saves created after the earlier night revision.
Required source cases against a new25 target:

| Source late-stock keys | Meaning | Safe addition |
| --- | --- | --- |
| none of the exact25 | Before stock21 | All25 defaults |
| all exact old21, none of headlight4 | Accepted previous night revision | Only headlight4 defaults |
| all exact25 | New revision | None; preserve every stage and group latch |
| partial21, partial4, headlight4 without old21, foreign/duplicate keys | Unknown/contradictory source | Reject without mutating source |

For an added group whose mount is occupied, append inserted+seated stage0;
for an empty mount append absent stage0. New group Bolted=false. An old empty
headlight group saved as true is contradictory and must not be silently
accepted. Do not auto-tighten lamps or rewrite native save files. Existing
cover/carb alias migrations and independently reviewed seven-socket consumable
migration run unchanged; preserve their known old target revisions as well.

Night topology checks should use the exact union of old21 and headlight4:
base273 hash unchanged, allowed late-stock roster none/exact21/exact25, with
the existing all-or-none7 consumable sockets and4 plug threads. The complete
new target is126/124/302. A naked count-only allowance is insufficient.

Required checks: four exact sizes/meshes/IDs, both16/2/0 groups; all9 stage poses
per bolt on translated/rotated vehicle; saved asset references survive reimport;
repeat0; before-stock and old21 native-copy migration; rejection of partial
cohorts without source mutation; real headlight Bolted starts false, reaches
true at aggregate2, restores correctly and satisfies the existing light gate
when a usable purchased bulb, wiring and electrical power are present.
Headlight-specific crash randomness and live source comparison remain explicit
unverified work, not a reason to weaken the mounting or lighting gates.

## Approved bounded source implementation

`Phase1SatsumaHeadlightFastenerAuthoring` exposes the exact four-entry
`GetBindings()` descriptor and `ApplyToInstance(assembly, generatedRoot)`.
Both mounting groups and all four source copies/typed targets are preflighted
together; mixed old/new sides, unknown IDs, changed tool/mesh/TRS and foreign
ownership reject before mutation. New definitions are persisted before binding
asset mount references. Existing retained-bulb/removal/installation arrays are
not replaced. Runtime lifecycle uses the existing generic fastener presenter
and group, not a new headlight-only state machine. Source copies are disabled;
the mounted project target owns its short-bolt renderer, interaction layer,
collider and outline. Exact source scale.z supplies the stage-travel multiplier.

The scoped NightBatch calls this helper after unchanged Stock21, validates
the exact whole Headlight4 cohort only in the presence of Stock21, preserves
the base273 identity hash, accepts the previous complete298 roster and requires
302 for the new complete packet. The separate refresh entry is
`MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaHeadlightFastenerAuthoring.RefreshHeadlightFastenersBatch`;
it also requires the prior Stock21 extension and an idempotent second pass.

Source tests are `SatsumaHeadlightFastenerAuthoringTests` (typed contract,
translated/rotated installed frame, all36 poses, live visibility and 2/0
hysteresis, persistent refs after forced reimport, whole-packet rejection)
and updated `SatsumaStartableCarNightBatchTests` (old273 and previous298 upgrade,
new302 repeat0, partial/foreign/head4-without-stock rejection). Integration
fixtures restore only reviewed original headlight bolt leaves while stripping
the new four targets from disposable mount-definition copies; canonical
prefab/asset hashes are protected. These new tests have not been executed by
this agent; the parent owns Unity validation. Runtime save migrations are
owned by the separate save task and must implement the cohorts above before
declaring native compatibility verified. No native save files were edited.
