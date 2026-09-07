# Satsuma: loose-door bolt visibility correction

## Scope and source

User-reported visible fasteners on an uninstalled door are a confirmed parity
defect, not a requested change to correct donor behaviour. This supersedes only
the door branch of the earlier body-material/fastener report. Other body panels
are not inferred to have the same rule without their own source audit.

Frozen read-only `Assets/_Scenes/GAME.unity`, SHA256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`:

| Door | Loose root Transform | Bolts GameObject / Transform | Removal FSM | Assembly FSM |
| --- | ---: | ---: | ---: | ---: |
| Left | 59626 | 31026 / 67084 | 110760 | 104384 |
| Right | 66117 | 21881 / 57936 | 112704 | 104362 |

Both `Bolts` objects are initially inactive (`m_IsActive: 0`, source lines501694
and353550). Both Removal FSMs are initially disabled and start at `State 1`
(left source byte167940786; right204523864). Assembly completion enables Removal;
its initial state explicitly activates the corresponding Bolts object and its
children: `activate=true`, `recursive=true`, both FsmBool byte payloads `01 00`.

Both `Remove part` states explicitly execute `ActivateGameObject(Bolts)` with
`activate=false`, `recursive=true`: action4 / parameter21 / byte offset46 is
`00 00`; parameter22 / byte offset48 is `01 00`. This is not merely disabling
the screw interaction. Visible bolt meshes are descendants of that same
inactive group: e.g. left bolt2 GameObject34876 / Transform70928 is a child of
BoltPM9270 / Transform45325, itself under Bolts31026 / Transform67084. The loose
door's direct active children do not provide another external copy of that bolt.

Thus the reviewed lifecycle is: **loose hidden → installed visible → detached
hidden**. The Screw FSM can still manipulate its inserted stage normally after
the Bolts group is activated.

## Root cause and compatible correction

The earlier builder deliberately placed each body bolt renderer under its
`PartInstance`, set it enabled, and asserted this was the donor loose behaviour.
`AssemblyFastenerInteractionTarget` only cached renderers below its separate
mount marker, so its existing inserted-fastener availability loop did not reach
these external part-owned renderers. The test
`StockBodyPanelsUseExactDonorShortBoltsAndNoDeadCopies` then codified that false
claim with `All.True` for both doors as well as the other seven panels.

The correction adds one nullable serialized
`installedOnlyExternalPresentationRenderer` binding to the existing fastener
target. Its existing availability refresh additionally drives this explicitly
bound renderer. **Default null retains the previous behaviour** for every other
fastener, including engine docking and all other body mounts. It does not search
for arbitrary sibling renderers or change outline selection.

`Phase1SatsumaDoorFastenerVisibilityAuthoring` preflights exactly both door
mounts, their four reviewed10mm / eight-stage IDs each, group32 / ON28 / OFF0,
the unique part-owned outline renderers and their existing presentation
references. Only these eight are bound and initially hidden. An old unbound,
new bound or exact partially bound cohort is supported; foreign bindings,
missing/duplicate targets or wrong ownership reject before the first mutation.
The renderer remains parented to the door so all accepted hinge motion and
fastener poses remain unchanged.

No stable ID, mesh, local/world pose, stage, group threshold, pickup/hinge physics,
part topology, save DTO or schema changes. Old saves need no migration: visibility
is reconstructed from their existing installed/inserted states at normal
availability refresh. Generated prefab refresh is required, not a new game.

## Integration and verification

The full builder already calls the scoped night packet. That packet now invokes
`Phase1SatsumaDoorFastenerVisibilityAuthoring.ApplyToInstance(assembly)`; no second
full-builder call is required. Existing generated content can also be repaired
without any full rebuild using executeMethod:

`MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaDoorFastenerVisibilityAuthoring.RefreshDoorFastenerVisibilityBatch`

Expected old-cohort refresh:8 reviewed bindings, repeated pass0; no native save
writes. The standalone command copies the original canonical prefab to a unique
`Logs/satsuma-door-bolt-visibility-before-*.prefab` before loading/saving it; the
night packet also retains its existing whole-packet backup. The old canonical
test changes only the door visibility branch.

New source-ready fixtures:

- `MSC.Tests.EditMode.VehicleAssembly.AssemblyFastenerExternalPresentationTests`
  (5cases): real graph installation/removal and installed/loose JSON restoration;
  default opt-out; cache invalidation; unbinding; stages, save state, poses,
  ownership and meshes unchanged by visibility refresh.
- `MSC.Tests.EditMode.LegacyImport.SatsumaDoorFastenerVisibilityAuthoringTests`
  (5cases): exact8 old→new / repeat0; partial-cohort repair; foreign renderer,
  wrong parent and duplicate-target preflight rejection; other renderers,
  targets and definitions preserved.

Compilation and executed Unity refresh/test results are recorded by the
integrating session; source readiness alone is not a passing Unity result.

### Root's executed integration, 6 September

Night refresh at04:42, PID1176, passed changed11/repeat0, with eight door
bindings and three separate fuel-fitting bindings. The generic126/124/302
topology is unchanged. Log: `Logs/codex-night-refresh-fuel-door-20260906.log`.
All10 new cases passed in
`Logs/codex-night-editmode-fuel-door-final-20260906.xml`. That broader run
failed only because the old canonical test still expected a visible outline
renderer on the now-correctly-hidden loose door bolt. Its expectation is now
null for doors only; all other mesh/pose/tool/stage/ownership checks remain.
The corrected canonical test and five independent electrical-save guard tests
then passed6/6,0skips in
`Logs/codex-night-final-protective-check-20260906.xml`, PID34064 at05:05.
This does not replace manual visual/interaction acceptance or claim a complete
vehicle test suite passed at that checkpoint.
