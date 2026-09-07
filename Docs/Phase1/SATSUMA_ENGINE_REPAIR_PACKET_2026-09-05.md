# Engine assembly repair packet — 2026-09-05

Status: `FollowupAutomatedValidatedPendingManual`. The automated results below describe the
previous packet, not acceptance of the current gameplay. User testing reported
engine-bay motion, engine mount bolts above the chassis, and a highlighted but
unresponsive carburetor throttle. Follow-up evidence and validation are tracked
in `SATSUMA_ENGINE_MANUAL_REGRESSION_2026-09-05.md`: follow-up 807/807 EditMode
and 81/81 PlayMode passed; renewed in-game acceptance remains pending.
No standalone build produced.

## Scope and evidence

Eight reported engine defects plus physical engine docking, assembled mass and
a debug carry override. Consumable-item conversion remains outside this packet.
Frozen donor GAME SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.

- Piston caps and cover deduplication: `SATSUMA_ENGINE_PISTON_CAP_AND_COVER_FIX_2026-09-05.md`.
- Screw travel: `SATSUMA_ENGINE_FASTENER_TRAVEL_REPAIR_2026-09-05.md`.
- Alternator, distributor, oil filter and carburetor: `SATSUMA_ENGINE_ADJUSTMENTS_2026-09-05.md`.
- Loose compound collision/mass: `SATSUMA_ENGINE_LOOSE_COMPOUND_PHYSICS_2026-09-05.md`.
- Exact save migrations: `SATSUMA_ENGINE_SAVE_EXTENSIONS_2026-09-05.md`.
- Engine docking design/compatibility: `SATSUMA_ENGINE_DOCKING_DESIGN_2026-09-05.md`.

These are `BehavioralReference`, `ConfigurationTransferred` and project-owned
`Reimplemented` logic; existing visual payload remains `TemporaryDirectImport`.
No original scripts/FSM controllers or old runtime assemblies are introduced.

## Changes

1. Four imported connecting-rod caps become visible with their installed pistons.
2. Rocker cover stock/GT duplicate markers collapse to the exact six stock IDs;
   ON2/OFF0 replaces the unsupported fallback ON1. Paired old save stages merge
   using max, never a sum/reset.
3. Explicit collider proxies put installed engine components' solid shapes on
   the outer loose Rigidbody. Original query colliders remain distinct. Mass
   and center are recalculated from own-part definitions, not prior totals.
   Disabled source colliders are validated through explicit hierarchy ownership,
   including when compound physics first initializes after native restore.
   Carry placement bounds include only live solid colliders of the carried body;
   disabled source bounds, query volumes and separately owned nested bodies no
   longer enlarge the placement box. The general collision-ignore pipeline is
   unchanged.
   The own-shape snapshot filter is opt-in through the existing explicit engine
   subassembly pickup capability. Non-engine families keep their accepted
   collider-snapshot semantics; applying this filter globally regressed fresh
   suspension restore and was rejected during the PlayMode comparison.
4. Bare-hand alternator/distributor adjustments use the inspected axes, steps
   and clamp gates. Oil-filter hand tightening uses the donor's axial movement,
   not invented spinning of the whole can. No HandRotate was found on inspected
   flywheel/clutch objects; their mouse-wheel rotation is not invented here.
5. Carburetor throttle is an explicit held-LMB linkage and feeds the existing
   simulation throttle input; this is not a fabricated RPM animation.
6. Mixture screw becomes a screwdriver adjuster, removed from the mounting
   fastener group. Four real 8 mm fasteners remain, ON8/OFF0/MAX32.
7. An explicit outline visibility capability prevents a secured/enclosed child
   from being advertised as removable while whole-engine pickup remains reachable.
8. Reviewed per-marker Z scales correct fastener travel. The hose clamp whose
   donor SetPosition action is disabled rotates without axial movement.
9. Three proximity pairs expose mount bolts before engine attachment. ON2
   transfers their pending stages into the existing assembly graph, OFF0
   releases at the current pose, subject to existing external connections.

The user explicitly approved a **120 kg** carry limit. The inspected donor
pickup FSM gates the engine on InHoist, not on this weight. This is an approved
deviation, not donor configuration. F10 → СБОРКА → «Разрешить поднимать тяжёлые
агрегаты двигателя» bypasses only this opt-in limit, for one player/session;
physical mass, gravity and normal installed/disabled pickup restrictions stay.
It is unavailable in non-development players and is not saved.

## Compatibility and boundaries

The ordinary graph still uses Loose/Installed; pending docking stages use an
optional block-owned versioned DTO. Other optional engine settings likewise
preserve old stable identities. Exactly seven obsolete fastener IDs are retired;
the canonical target roster changes 280 → 273, without deleting old definition
assets. Existing native files are not rewritten by Editor refreshes. Source DTOs
are not mutated by migrations.

No broad vehicle/world rebuild, scene-list repair, player redesign, donor write,
Git reset, commit or push belongs to this packet. Parallel workspace changes
must remain untouched. Generated edits are limited to the Satsuma wrapper and
the three explicitly reviewed mount definitions, with recoverable backups.

Limits: complete hoist operation is not added here; installed-engine HingeJoint
break-force parity is not claimed. Mixture/timing/belt/filter settings have
interaction/persistence, but all related running-engine effects are not yet
implemented. Existing external engine connections remain removal blockers;
destructive donor force-break behavior is not silently substituted.

## Validation

The scoped refresh executed successfully in Unity 6000.3.11f1:
`Logs/codex-engine-repair-refresh-03.log`, `changed=174 repeat=0 targets=273
compoundParts=39 fullRebuild=false`. Recoverable backup:
`Logs/engine-repair-before-20260905-104518-1579438/`.
Comparison with its 117 mount-definition backups found changes only to the
rocker-cover, carburetor and engine-assembly definitions. No broad rebuild ran.
Final repeat in a fresh Editor process also exited0 with `changed=0 repeat=0`
(`Logs/codex-engine-repair-refresh-04.log`), confirming persisted idempotence.
Final prefab SHA256:
`E683DD2558046EB10CBFF6CD04A80AEE5EAC5BA60EE33D758479928E0F88F785`.

Initial test runs exposed a late-initialization collider ownership bug, an
EditMode lifecycle fixture problem, and old generated-roster/click-install
expectations. Extended PlayMode checks also caught the over-broad snapshot
filter: disabling/removing compound physics did not remove the fresh-restore
flip; restricting the filter to explicit engine assemblies restored the original
unmodified settling assertion. Failed diagnostic runs are retained as evidence,
not counted as passes. Isolated agent checks are not Unity test passes.

Final executed results, using one hidden Unity process at a time with
`-batchmode -nographics -job-worker-count 2`:

- **799/799 EditMode passed**, zero failures/skips:
  `Logs/codex-engine-repair-final-edit.xml` and matching `.log`.
  SHA256 `A230D001277A60595CA764618422035970AA0665B4920E501D4CE7FCD71A8C37`.
- **74/74 PlayMode passed**, zero failures/skips:
  `Logs/codex-engine-repair-final-play.xml` and matching `.log`.
  SHA256 `97C5AEB177DD7F60A8D110FFC20643F85545BF20E5A30F705AE3C405EEA6BD13`.
- `Phase1SatsumaEngineRepairBatch.ValidateNativeEngineSaveBatch` executed with
  an explicit `-engineSavePath` for current slot-01. Log:
  `Logs/codex-engine-repair-native-save-audit.log`, exit0,
  `oldFasteners=280 currentFasteners=273 parts=126 sourceDtoUnchanged=true
  nativeFileWritten=false`. The vehicle assembly payload was restored onto an
  instantiated copy and its migrated capture validated again. This is not a
  claim that a complete native game session was manually played.
- SHA256 comparisons: all six pre-recorded native files (slot-01 current,
  backup and old corrupt snapshot; slot-02 current/backup; slot-03 current)
  remained byte-identical. Slot-01 current remains
  `6DDC6BEF3062F70BF499723D205A91EA49FD213E2D26330A63FC5735257704D9`.
- `git diff --check` completed without whitespace errors. Existing unrelated
  line-ending notices and rear-suspension unused-field warnings are not reported
  as fixed by this packet. No new engine compile errors remain.

The test invocations used `-runTests -testPlatform EditMode|PlayMode`, explicit
`-testFilter`, `-testResults` and `-logFile`; their complete argument lists are
recorded at the top of the corresponding logs. Authoring/audit invocations used
`-executeMethod MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaEngineRepairBatch.RefreshEngineRepairBatch`
or `.ValidateNativeEngineSaveBatch`, followed by `-quit`. No full builder or
standalone player build was invoked.

The additional player pickup fixture sampled the accepted inertial follower at
its first overshoot. Existing coefficients (110, 0.82, linear damping 1, dt0.02)
predict 0.090994081m error at step20, matching observed 0.090994120m. At step50
the predicted error is 0.000195069m. Only the fixture's settling time changed
20 to50 fixed steps; the strict 0.08m tolerance and actual physical carry state
checks remain. No player follow tuning or inertia was changed.

The new full-engine fixture assembles the real generated stock path (38 parts,
GT cover excluded), checks aggregate mass/contact ownership and the carry gate,
then repeatedly restores its captured native assembly DTO. It also docks the
complete heavy motor with debug disabled, loosens its three mount bolts and
checks in-place removal with all internal fasteners retained and all 53 compound
contacts restored. No consumable items are synthesized to fill unimplemented
bridges.

## Scoped file index

All new C# assets include project-owned `.meta` files.

- `Assets/Game/Vehicle/Assembly/Runtime/`: new `SatsumaPistonCapPresenter.cs`,
  `SatsumaRockerCoverFastenerMigration.cs`, `SatsumaCarburetorFastenerMigration.cs`,
  `SatsumaEngineAdjustmentRules.cs`, `AssemblyEngineAdjustmentState.cs`,
  `AssemblyEngineAdjustmentTarget.cs`, `AssemblyCarburetorThrottleTarget.cs`,
  `AssemblyEngineDockingState.cs`, `AssemblyPhysicalDockingOnly.cs`,
  `AssemblyLooseCompoundPhysics.cs`, `AssemblyCompoundColliderProxy.cs`.
  Extended `VehicleAssemblyController.cs`, `VehicleAssemblySaveData.cs`,
  `PartInstance.cs`, `AssemblySubassemblyPickupTarget.cs`,
  `AssemblyFastenerInteractionTarget.cs`, `AssemblyMountHandoffTarget.cs`,
  `AssemblySurfaceMountHandoffTarget.cs`.
- `Assets/Game/Interaction/Runtime/`: new
  `Capabilities/IInteractionOutlineVisibility.cs`,
  `Carrying/AssemblyCarryDebugOverride.cs`; extended
  `Carrying/PhysicsPickupTarget.cs`, `Carrying/PhysicalCarryController.cs`.
  Other integration files: `Assets/Game/Player/Runtime/InteractionOutlinePresenter.cs`,
  `Assets/Game/Bootstrap/Development/ProductionDeveloperConsole.cs`,
  `Assets/Game/Vehicle/Runtime/SatsumaIgnitionInputAdapter.cs`.
- `Assets/Game/LegacyImport/Editor/GameplayPresentation/`: new
  `Phase1SatsumaEngineCapAndCoverAuthoring.cs`,
  `Phase1SatsumaEngineAdjustmentsAuthoring.cs`,
  `Phase1SatsumaEngineFastenerTravel.cs`,
  `Phase1SatsumaEngineCompoundPhysicsAuthoring.cs`,
  `Phase1SatsumaEngineDockingAuthoring.cs`, `Phase1SatsumaEngineRepairBatch.cs`.
  Extended `Phase1SatsumaBaselineBuilder.cs`,
  `Phase1SatsumaEngineFastenerPresentation.cs`,
  `Phase1SatsumaEngineAdditionalFastenerPresentation.cs`;
  bounded compiler-warning cleanup in `Phase1SatsumaCamshaftTimingAuthoring.cs`
  and `Phase1SatsumaEnginePickupAuthoring.cs`.
- New EditMode fixtures: `SatsumaEngineCapAndCoverTests`,
  `SatsumaEngineFastenerTravelTests`, `AssemblyLooseCompoundPhysicsTests`,
  `AssemblyCarryDebugOverrideTests`, `SatsumaGeneratedFullEngineCompoundTests`
  under `Assets/Game/Tests/EditMode/LegacyImport/`;
  `SatsumaEngineSaveExtensionTests`, `SatsumaEngineAdjustmentTests`,
  `SatsumaEngineDockingTests` under `VehicleAssembly/`;
  `PhysicalCarryCompoundBoundsTests` under `PlayerInteraction/`.
  Existing pickup/outline, generated content, front/rear meshes, engine meshes,
  legacy alignment and native integration fixtures were updated for the exact
  canonical shape. New PlayMode fixture:
  `Assets/Game/Tests/PlayMode/VehicleAssembly/AssemblyLooseCompoundContactPlayModeTests.cs`.
  Existing `PlayerInteractionFlowTests.cs` settles before measuring; existing
  `SatsumaInstalledPartPhysicsPlayModeTests.cs` retains failure-only restore
  diagnostics. Temporary A/B wrappers were removed.
- Generated private assets: the existing Satsuma prefab and only
  `mount.satsuma.cylinder-head.rocker-cover.asset`,
  `mount.satsuma.cylinder-head.carburetor.asset`,
  `mount.satsuma.engine-assembly.asset`. These generated payloads remain outside
  Git. Retired fastener definition assets were not deleted.
- Evidence reports are listed above, plus
  `SATSUMA_ENGINE_DOCKING_BINDINGS_2026-09-05.md`. Provenance updated in
  `Docs/Porting/DONOR_AUDIT.md`, `SYSTEM_MAP.md`, `PORTING_MATRIX.md`,
  `PORTING_LEDGER.csv`.

## Manual check after automated validation

1. Install all four pistons: cap appears; remove one: its cap disappears.
2. Stock and GT cover each show six working bolts with plausible final depth.
3. Stand/roll the growing engine on different faces: fitted parts support it on
   the table; no block-only penetration. Detaching a subassembly restores its
   independent collision/mass.
4. Check loose/tight clamps and wheel direction on alternator/distributor;
   filter must fully unscrew before removal. Try screwdriver mixture and held
   carb linkage with the appropriate assembly/access state.
5. Carry limit blocks the complete motor. Enable the debug override to position
   it in the bay, release it, and use the three mount bolts. Merely approaching
   the bay does not install/snap it. Undo all mount stages with external lines
   disconnected: engine is released without a sideways jump.
6. Save/reload partial engine assembly, pending first mount stage, fully mounted
   engine and adjustment settings. Check that unrelated body/suspension remain
   as accepted. Do not treat automated results as this manual acceptance.

Next milestone after acceptance: engine hoist integration with this physical
docking/carry contract.
