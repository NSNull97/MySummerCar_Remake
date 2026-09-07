# Satsuma cockpit C1b — steering wheel installation and latch

Date: 2026-09-05. Status: **ImplementedAutomatedPassedManualPending**.
Classification: `BehavioralReference / ConfigurationTransferred / Reimplemented`.

## Scope and evidence

Source: `SATSUMA_COCKPIT_STEERING_DEPENDENCY_AUDIT_2026-09-05.md`,
reviewed completely by root. Locked donor scene SHA256
`C3F2F3373CCAD4FCBE104840FCB83E364F55438070E808D11EBE4996BC0476C4`.
Column Assembly109461 activates the wheel trigger through steering_column2;
Wheel Assembly113042 accepts an installed but unbolted column. Stock/GT wheel
BoltChecks105776/109891 use ON2/OFF0, as do the two currently unimplemented
Sport/Rally alternatives. Column Removal108777 does not read wheel DB state.
Root also independently reopened the two bounded raw BoltCheck documents at
byte74620201 and151948156 and verified their BoltedYES2/BoltedNO0 variables.

## Path review

The independent read-only review found one required integration beyond the
central query: physical surface routing. Root verified and patched that path.
Explicit-marker preview, surface offers, handoff pre-release and final animated
commit already share `AreMountInstallPrerequisitesMet`. The carried-object
selection filter must remain capable of selecting an invalid target to display
the prerequisite failure; it is not a second installation authority. Restore
and initial authored occupancy remain direct, intentionally grandfathered.
No reverse/removal/retention/collapse consumer reads the new field.

## Implemented compatible correction

- Add an empty-default installation-only occupied prerequisite to mount
  definitions; validate its authored references, check it only for new install
  queries/preview/commit, never inverse removal, structural collapse or restore.
- Route the wheel offer through the installed column's physical surface as
  well as its explicit marker. The existing surface resolver originally read
  only structural required-occupied edges; it must also read install-only edges
  for this interaction path, without adding any structural relationship.
- Set only the existing steering-wheel definition's installation prerequisite
  to the steering-column mount and change its group ON1 to ON2, retaining OFF0.
- Preserve body-shell ownership, stable IDs, geometry, central10mm nut, column
  fastening and accepted physical behaviour. No inferred hide, detach or drop.
- Preserve old installed-wheel/absent-column saves and true latches atT1;
  apply the stricter requirement only when attempting a fresh installation.
- Integrate the bounded rule into the full authoring path without rebuilding
  donor presentation now. Refresh only the existing definition, with preflight
  before any mutation; then repeat to prove idempotence.

## Validation scope

Generated definition and helper drift guards; stock/GT new install with and
without an installed/unbolted column; reverse removal/collapse non-regression;
real capture/validate/restore of legacy topology and B=true/T1; threshold
hysteresis. Run C1a/C/E1/assembly/physics fixture regressions, inspect actual XML,
compare all117 mount definitions plus prefab/manifest/build-settings hashes.
Do not label fixture tests manual gameplay acceptance. No live user saves.

## Executed authoring checks

- Fresh preflight: Unity6000.3.11f1, no other Unity process, 24.89GiB free of
  31.92GiB RAM. Captured120 protected hashes; all matched the completed C1a.
- Root independently reviewed the three runtime changes, the surface routing,
  Editor helper/full-builder hook and all new test code. Peer helper review
  led to internal column-ID and inactive-scalar drift guards before mutation.
- `RefreshSteeringWheelRulesBatch`, PID19140, exit0, changed1 definition.
  Repeat PID9716, exit0, changed0. Logs `codex-cockpit-c1b-refresh-01/02.log`.
- Post-refresh120 hashes: exactly the steering-wheel SO changed; all116 other
  definitions, full prefab, manifest and build settings remained byte-identical.
  Wheel SO SHA256:
  `D39629C78AF6A8B61451290EEB14C539652EA719C501C8DEC68A6346FDD85515`.
  Unity also serialized already-existing empty/default additive assembly fields
  on that SO; these are not new semantic rules. No other asset was refreshed.
- Compile had no C1b errors/warnings. Existing five rear CS0414, Bootstrap
  CS0108 and five TextureInventoryExporter CS0618 warnings remain outside scope.

## Executed test results

Unity batchmode/nographics fixture runs, one process at a time:

- EditMode PID3508, exit0, **411/411**, failed0/skipped0, 63.3359134s.
  New C1b49; C1a49; E1engine19; generated39; assembly27; removal23+5;
  front install65, front alignment24, steering targets6, old latch saves12;
  wheel cadence17; electrical/wipers16; player interaction52; audio bridge3;
  P0 BoltCheck5. XML `Logs/codex-cockpit-c1b-edit.xml`, SHA256
  `4EB1BA8625C0271E01F7DC292F95F05E5A32F14E587B2CAC9879765E22F83B67`.
- PlayMode PID5816, exit0, **61/61**, failed0/skipped0, 78.7213604s.
  Assembly11, front install4, front steering3/spawn8, handbrake2,
  installed physics20, rear drive2/droop4/compression3, wheel cadence4.
  XML `Logs/codex-cockpit-c1b-play.xml`, SHA256
  `6F2D6C4AFCC39100C2D41898B3A20DAD7A65A562934910B6B9DF3375E0DC546E`.
- Root inspected both result XMLs. Complete `-testFilter` arguments and Unity
  output are retained in the same-named `.log` files. These are fixtures, not
  a new-game/load/menu input-driven journey or a donor gameplay capture.
- Final120 protected hashes exactly match post-refresh. Only the intended
  wheel definition differs from pre-C1b; other116 definitions + prefab,
  manifest and build settings remain unchanged. No Unity process remains.
- `git diff --check` on the bounded code/tests is clean. Existing unrelated
  warnings above and fixture NWH warnings are not represented as new failures.

The new49 cases verify the real query, install, manual remove, forced column
break and save capture/validate/restore paths, stock+GT alternatives, surface
handoff routing, ON2/OFF0 hysteresis, empty/null defaults, bad/self/duplicate
references, helper idempotence and31 authoring drift cases. Invalid scalar test
setup was corrected before execution to write raw serialized values instead of
letting `Configure` clamp them; no test assertion was weakened to obtain a pass.

## Changed files and compatibility

- Runtime: `MountPointDefinition.cs`, `AssemblyGraph.cs`,
  `VehicleAssemblyValidator.cs`, `AssemblySurfaceMountHandoffTarget.cs` under
  `Assets/Game/Vehicle/Assembly/Runtime`.
- Editor: `Phase1SatsumaCockpitRules.cs` and the full-builder hook in
  `Phase1SatsumaBaselineBuilder.cs` under
  `Assets/Game/LegacyImport/Editor/GameplayPresentation`.
- New `Assets/Game/Tests/EditMode/LegacyImport/SatsumaCockpitSteeringRulesTests.cs`
  plus `.meta`, GUID `ae30ce03cc1e454ea719428b8d1e3a04`.
- One ignored generated SO: `MountDefinitions/mount.satsuma.steering-wheel.asset`.
  No raw donor payload enters Git. Reports/checkpoint/provenance updated.

No existing API, serialized field, stable ID, mount ownership, geometry or
physics setting was removed or replaced. The added authoring array defaults
empty in every old definition. Existing assembly schema2/legacy1 and native
vehicle DTOs are unchanged; no migration is needed. Saved wheel-without-column
topology and valid true T1 latch both round-trip without mutating the source DTO.
Fresh installation after removing such a legacy wheel uses the new gate.

## Manual acceptance and remaining limits

No manual Unity repair step is needed; scoped asset refresh already ran.
In the current Editor project, not the older player build:

1. Check stock and GT wheel offers with the column absent: both are blocked.
2. Assemble the column normally onto its prepared support but leave its own
   bolts loose. The wheel must now install, including aiming at the column.
3. Central10mm nut: from looseT0, T1 remains removable, T2 sets Bolted;
   loosening fromT2 toT1 retains Bolted, T0 releases it again.
4. The installed wheel must not newly prohibit loosening/removing the column.
   Exact donor wheel visibility/fate after that removal remains uncaptured:
   C1b deliberately preserves the existing ownership/presentation, not an
   inferred hide/reattach/drop implementation.
5. When testing an existing save, no retrospective column dependency should
   delete its installed wheel or reject the save. No live user save was touched
   by this packet; automated DTO fixtures cover the compatibility path.

The earlier standalone build024640 does not contain C1a or C1b. Sport/Rally
wheel parts, physical ignition/key access, seat ownership and cockpit visuals
remain separate work. Next bounded step: design and implement the compatible
physical ignition access/intent bridge, then return to the agreed engine work.
