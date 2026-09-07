# Installed engine physical mount — compatibility proposal

Status: **proposal only; not implemented or approved**. The coordinating task
has asked the user whether to start a separate physical-engine stage; no answer
was available when this note was prepared. This audit changed no Assets, native
save, generated prefab or donor file and launched no Unity process.

Scope: one explicitly opted-in engine body and its existing compound contents,
not a suspension rewrite, generic damage simulation, or a direct chassis torque
effect. Reuse the source measurements in
[the vibration/audio audit](SATSUMA_ENGINE_VIBRATION_AUDIO_AUDIT_2026-09-06.md)
and the accepted
[docking contract](SATSUMA_ENGINE_DOCKING_DESIGN_2026-09-05.md).
MotorShake was not re-extracted in this compatibility audit.

## 1. Decision and actual blockers

The existing physical-link boundary can host the joint, but **adding a hinge to
the block alone is not safe or sufficient**. Two existing ownership consumers
and installation momentum must participate in the same opt-in transition.

The complete fixed stock fixture has 38 parts, 177.6kg total, three original
block contacts and 53 child proxy contacts. The bare block is 95kg; its children
are therefore 82.6kg. These are the existing fixed-fixture totals, not a promise
that a current engine with purchased consumables always weighs 177.6kg.

| State / naive change | Block body | Chassis contribution of engine branch | Child solid proxies |
| --- | --- | --- | --- |
| Accepted loose compound | 177.6kg dynamic | 0kg | 53 on block |
| Accepted installed engine | 95kg kinematic | 177.6kg | disabled |
| Add physical link only | 95kg dynamic | 82.6kg | disabled |
| Also aggregate engine, without fixing chassis owner | 177.6kg dynamic | 82.6kg — double-counted | would be on block |
| Proposed installed opt-in | 177.6kg dynamic | 0kg | 53 on block |

This is a real incompatibility in applying the new mode to the accepted
foundation, not a defect in its current loose-only contract. The smallest
compatible extension is an explicit installed-compound owner shared by the
compound and chassis consumers. It does not require a new graph or save format.

The second integration blocker is momentum: the current physical-link install
copies the chassis velocity onto the child, while the chassis action consumer
adds an installation impulse directly to the chassis. That policy cannot be
blindly reused for an independently simulated, fully aggregated engine.

## 2. Exact existing contracts to preserve

Paths and line numbers below describe the source inspected on 2026-09-06.

- `Assets/Game/Vehicle/Assembly/Runtime/AssemblyInstalledPhysicsLink.cs:5`:
  serialized enum modes0–4 already describe fixed/suspension/wheel/panel links.
  `TryAttach:92` creates a dynamic body and joint; `CreateHinge:284` uses a zero
  local anchor, connected-body anchor and caller-supplied world axis. Its generic
  limited-hinge contact distance is 1degree, larger than the proposed engine's
  entire 0.5degree range. Do not inherit that setting without an engine-specific
  solver check. `ConfigureCommonJoint:411` disables collisions only with the
  directly connected body and uses infinite break limits. `DetachJoint:422`
  clears the managed reference immediately but destroys the native joint at
  end of frame in PlayMode.
- `Assets/Game/Vehicle/Assembly/Runtime/PartInstance.cs:131`:
  install snaps Transform and Rigidbody to the canonical pose before attaching
  a link. `UsesDynamicInstalledPhysics:81` makes `SynchronizeInstalledPose:209`
  skip that body's pose copy; its kinematic children still synchronize to their
  existing owner mounts. `ApplyInstalledColliderState:370` retains the physical
  root's own solids while ordinary installed child solids remain disabled.
  The explicitly scoped engine snapshot rule at350 excludes nested parts and
  generated proxy contacts. Do not broaden that filter again.
- `Assets/Game/Vehicle/Assembly/Runtime/AssemblyLooseCompoundPhysics.cs:201`:
  aggregation uses immutable definition masses and authored own CoMs, never an
  already aggregated body value. `ResolveLooseOwner:331` currently rejects any
  dynamic installed part, including descendants whose chain reaches it.
  `GetRelativePose:301` uses the stable local hierarchy and exact installed
  child-to-socket identity, avoiding large-world subtraction. `OnDisable:486`
  disables proxies and restores each body's own mass/CoM. That disable policy
  must not silently leave a new installed physical engine with missing mass.
- `Assets/Game/Vehicle/Runtime/AssemblyChassisMassController.cs:63`:
  mass skips a part when that *part* uses dynamic installed physics, but does
  not stop at such an ancestor. `HasInstalledPathToChassis:160` intentionally
  accepts an installed owner chain to the shell and excludes loose assemblies.
  `HandleAssemblyAction:138` applies source linear momentum to the chassis.
- `Assets/Game/Vehicle/Assembly/Runtime/VehicleAssemblyController.cs:190`:
  install captures source velocities, calls `PartInstance.InstallAt`, commits
  pending docking stages at241, synchronizes, then publishes `PartInstalled`.
  Published transferred mass at250 currently means the installed part's own
  definition mass, not necessarily its full retained subassembly mass.
  `NotifyInstalledPhysicsStateChanged:664` already invalidates mass consumers
  through the mutation counter. `TryRemoveAtCurrentPose:1064` preserves the
  engine's actual position/rotation, not the generic removal offset.
- `Assets/Game/Vehicle/Assembly/Runtime/AssemblyEngineDockingState.cs:91`:
  loose, uncarried engines may expose three actual paired points inside0.1m.
  It deliberately does not consult the120kg hand-carry permission. The first
  two aggregate turns attach the engine; stages are committed before public
  callbacks. ON2/OFF0, three11mm bolts, maximum8 each, and the exact identities
  remain unchanged. `ReleaseIfUnfastened:212` retains external connection
  blockers and internal assembly when releasing at OFF0.
- `Assets/Game/Vehicle/Assembly/Runtime/AssemblySubassemblyPickupTarget.cs:65`:
  pickup resolves the outer *loose* owner through actual graph ownership, never
  an arbitrary dynamic body. Its captured delegate keeps Body/StableId stable
  through handoff until release. `PartInstance` disables pickup on installation.
  `PhysicsPickupTarget.cs:54` in `Assets/Game/Interaction/Runtime/Carrying`
  keeps disabled/held/kinematic/identity guards independent of the opt-in120kg
  mass override. No installed engine or chassis may become pickable merely
  because the block becomes dynamic.
- `Assets/Game/Vehicle/Assembly/Runtime/AssemblyCompoundColliderProxy.cs:7`:
  generated contacts have SourcePart/SourceShape and a `LooseOwner`, not a saved
  entity or capability host. Installed physical ownership must not change the
  public meaning of `LooseOwner`. Add a separate derived physical-owner property
  if diagnostics need it; leave LooseOwner null for an installed aggregate.
  `AssemblyLooseOwnerMountOcclusionTarget` remains loose-only; do not turn the
  new physical compound into a general through-car query exception.

Existing dynamic consumables participate through `AllRuntimeParts` and the
explicit `TryRegisterRuntimeBinding`/refresh/unregister APIs. The proposed
ownership rule must handle the actual occupied plug/filter/belt instances,
exclude loose spares and alternative covers, and preserve those APIs. See
[dynamic consumer contracts](SATSUMA_DYNAMIC_PART_CONSUMERS_2026-09-05.md).

## 3. Minimum additive implementation shape, if approved

1. Append `EngineMountHinge = 5` to the existing link enum; keep values0–4 and
   all default paths unchanged. Author this mode only on the one existing
   engine-block PartInstance, with explicit chassis Rigidbody connection and
   existing canonical engine mount. Reuse serialized target/connected body and
   min/max fields; use the reviewed[-0.25,+0.25]degree limits and zero anchor.
   The donor left its newly created hinge axis at default: encode the intended
   local-X basis explicitly and verify it in the mode's axis test rather than
   accidentally inheriting a suspension's chassis-forward/right convention.
   Do not add a motor, suspension spring, weld projection, or wheel rest clamp.
2. Add a small shared ownership calculation in the Assembly module, using
   existing explicit owned mounts and the reviewed compound binding registry.
   It may return an outer loose aggregate or this explicitly configured,
   attached engine aggregate. Any other dynamic installed link remains a
   boundary with its accepted behavior. Missing/cyclic/foreign ownership fails
   validation, not a fallback to a Transform ancestor. No runtime donor IDs or
   object-name search; the Editor helper validates the exact stable engine ID.
3. Extend `AssemblyLooseCompoundPhysics` compatibly, without renaming its type,
   serialized bindings, public APIs or existing loose-owner semantics. In the
   new installed mode, the physical engine root retains its own solids and all
   installed kinematic descendants contribute their exact proxy shapes/mass/CoM
   to that body. Do not enable their source solids, add nested dynamic bodies,
   duplicate root shapes or reparent the logical parts out of their sockets.
   Chassis mass skips precisely this aggregate's full branch. Unopted parts
   retain current chassis/suspension rules.
4. Preflight the complete engine binding/shape/owner mapping before graph
   mutation. Add only a narrow transaction hook for this new mode, completing
   mass, CoM, proxy ownership and joint readiness before an install/remove
   observer or first physics step can inspect it. Do not rely on component
   subscription order to repair a half-transition. Reuse the existing mutation
   invalidation and direct-restore completion points. Keep pending ON2 stage
   commitment before any public installation notification.
5. Make momentum ownership explicit for this opt-in only. A loose aggregate
   that remains the same dynamic engine body must not lose its velocity and
   then receive the old extra chassis impulse. Preserve its source momentum
   once when attaching; let the joint communicate the actual reaction.
   For a loose child/subassembly becoming kinematic inside the installed
   engine, capture its complete pre-transfer aggregate mass and momentum and
   route the transfer once to the engine owner, not the chassis. Avoid changing
   the meaning of the existing public event for unrelated parts: use a narrow
   internal pre-transfer record/hook for the opt-in branch. Keep legacy
   suspension/chassis transfer behavior covered unchanged.
6. Handle lifecycle atomically: on engine detachment remove/disconnect its
   joint constraint before allowing loose contacts, retain its children and
   recompute the same compound before returning. The current generic
   `RigidbodyDefaults.Restore` zeroes velocity; if continuity is needed for this
   physical engine release, preserve/restore velocity only in the new mode.
   Do not change generic removal behavior. Component disable/re-enable must
   either retain coherent ownership or explicitly hand back to the accepted
   kinematic/chassis mode before removing proxies. Never leave a95kg body
   temporarily representing a177.6kg engine.
7. Put healthy excitation in one new project-owned, explicitly bound component
   on this physical engine. Reuse the audited input amplitude/period and
   start/stop subsystem semantics; drive the engine body only. Stop adding
   impulse on disable, invalid binding, restore or detached state; stopping
   excitation does not forcibly zero physically acquired velocity. No damaged
   crankshaft±22000 impulse, no always-on shake, no direct chassis AddTorque.
   The renderer-only `SatsumaEngineVisualVibration` must remove its offsets and
   suppress its approximation while this physical mode is active; its default
   behavior remains available for unopted legacy/prototype content.

No additional generic serialized physics switch is necessary beyond the new
link mode. The excitation component needs explicit assembly/block/link and
simulation references plus a reviewed healthy configuration; no name lookup.
One scoped Editor helper must preflight the exact old/new shape, back up the
canonical prefab, add only those bindings, save, and prove repeat changes0.
Do not silently retrofit every part using the existing link component.

The joint's `enableCollision=false` excludes only engine versus connected
chassis contacts. Separate subframe/panel bodies still exist: test their actual
contacts. This is a measured-integration risk, not evidence authorizing blanket
collision ignores. Any required pair exception needs its own exact contact
evidence, scoped opt-in, and restoration on detach; otherwise stop and report.

## 4. Save and restore compatibility

The minimum proposal needs **no stable-ID, socket, fastener, lifecycle enum or
save-schema change**. Existing saved assembly/fastener/docking data determines
the new authored mode; old saves without a physical-engine DTO remain valid.
Generated prefab binding is the migration, performed only after approval.
Keep the original payload recoverable and test both old and refreshed content.

`VehicleAssemblyController.RestoreSaveData:1373` restores installed parts before
fastener stages and group latches, then synchronizes owned poses, restores
adjustments/docking, and calls compound.Refresh(true) at1481. Therefore a new
physical engine cannot infer its final fastening state from the first
`InstallAt` call during restore. Finalize it after restored group state is
available, without emitting ordinary assembly actions or replaying source
momentum/excitation. Refresh a coherent mass partition before returning.

`PartSaveDto.worldRotation` already exists. Currently its restored argument is
consumed only by hinged-panel interaction, not a generic installed physics
link. For the new mode, establish the hinge's zero/rest frame at the canonical
mount first, then derive and restore only its bounded axis rotation from the
saved quaternion. Do not create a hinge at the displaced rotation and thereby
rebase its zero. Old exact-mount saves yield zero angle; unrelated parts keep
their old restore path. Oscillator phase and applied impulse are transient and
restart safely; this proposal does not introduce engine-body velocity fields.

`Assets/Game/Vehicle/Runtime/VehiclePersistence.cs:488` temporarily holds the
chassis kinematic during assembly restore. The existing NWH synchronizer then
replays suspension/wheel handoffs; chassis mass refresh occurs at579 and saved
chassis velocity is restored only at591. A new engine attached during the
kinematic interval initially inherits zero velocity from the current link.
Before the first live step, it must instead inherit the *restored* chassis point
velocity and angular velocity, plus no invented saved relative oscillation.
Use a narrow finalization in this existing restore boundary, after final mass
and chassis motion are known. Do not modify NWH's accepted ownership modes or
allow a physics step between the old/new mass owners. Invalid restore must
leave the checkpoint behavior coherent too.

If exact relative engine angular velocity/oscillator phase persistence later
becomes a required feature, that is a separate optional versioned DTO proposal;
do not smuggle those fields or a save-version bump into this minimum stage.

## 5. Tests required before enabling the canonical prefab

These are proposed tests, **not executed coverage of the new mode**.

| Fixture / case | Required evidence |
| --- | --- |
| Opt-in link and authoring EditMode | Old modes0–4 and serialized defaults unchanged; exactly one reviewed engine link; zero anchor, correct local axis,±0.25degree limits; no motor/spring; idempotent scoped authoring; malformed owner/binding rejected before mutation. |
| Complete mass partition EditMode | Bare95kg then complete38/177.6kg; docked engine remains177.6kg, chassis excludes the full branch, no double-count82.6kg. Sum only active physical owners, not nested kinematic body.mass fields. Weighted world CoM agrees before/after handoff. Include purchased plugs/filter/belt and loose alternatives. |
| Atomic docking and momentum | Pending stage0/1 remains loose; ON2 observer sees installed graph, stages2, one joint and complete mass/contact owner. OFF0 respects external connections and retains internals. No spurious chassis impulse for floor assembly, no duplicated impulse at dock, child install transfers to actual engine owner. Rejected preflight leaves graph, body and pending stages unchanged. |
| Compound lifecycle | Installed own shapes plus exactly53 fixed-fixture child proxies on block; nested source solids disabled. Detach, disable/re-enable, repeated installation and dynamic-wrapper registration preserve mass/CoM, remove stale contacts and never expose proxy identities in saves. No-frame drop to95kg or doubled aggregate. |
| Large-world and physical contacts PlayMode | Existing origin/home local-frame drift tests, supported floor/table engine sleep, and actual bay settling remain valid. New installed engine at rest has bounded relative angle/translation and no sustained drift or unintended force with excitation off. Validate actual scene membership, dynamic flags and gravity; do not pass a body that never simulates. |
| Real physical excitation PlayMode | Level car on real four-wheel support, complete engine, healthy RPM sweeps/start/stop: bounded relative hinge response and chassis reaction through joint, not direct body transform changes. No accumulated mean angular bias, no contact explosions or perpetual movement with excitation disabled. Numerical bounds require measured Unity6 output, not invented equality with old PhysX. |
| Save transaction and first fixed step | Old exact-mount save, new small hinge-angle save, moving chassis, sleeping parked car, installed/loose/pending1turn cases and invalid-record rollback. Three restore cycles preserve IDs/stages/contents; final mass partition exists before return and engine velocity matches restored chassis before simulation; no load impulse, fall, flip or half-installed body. |
| Carry and interaction | 120kg accepted, heavier rejected unless same-player opt-in debug; full177.6kg still docks physically without carry permission. Installed dynamic engine/child never lifts the car, even with debug. Captured owner Body/StableId remains stable during release/handoff; detached retained assembly regains correct mass gate and child pickup/removal. |

Keep the existing legacy tests on an explicitly unopted fixture, rather than
weakening them to accept either physics ownership. In particular:

- `SatsumaEngineCompoundMotionTests.InstalledEngineFollowsChassisWithoutIndependentContactsOrMotion`
  remains the old kinematic contract; the opt-in mode gets a separate test.
- `SatsumaGeneratedFullEngineCompoundTests` currently asserts zero installed
  proxies and block95kg. Split the authored new-mode expectation from that
  historical test explicitly, preserving the full floor/dock/retain/carry cycle.
- Rerun `AssemblyChassisMassOwnershipTests`, `AssemblyLooseCompoundPhysicsTests`,
  `AssemblyDynamicPartConsumerTests`, `AssemblySubassemblyPickupTests`,
  `AssemblyCarryDebugOverrideTests`, docking and direct-restore fixtures.
- Rerun all accepted front/rear/road-wheel/door physical tests and
  `SatsumaInstalledPartPhysicsPlayModeTests.FreshVehicleRestoreSettlesWithoutAssemblyMutation`.
  The previous restore-flip regression was caused by broadening PartInstance
  collider snapshots outside opted-in engine families; do not repeat that fix.
- Finish with the native snapshot copy, production bootstrap and full suites;
  keep user files byte-identical. A manual start/stop/dock/unbolt comparison is
  still required before claiming original mechanical feel.

## 6. Approval boundary and next step

No new framework, external dependency or graph replacement is required by this
audit. The genuine foundation extension is the opt-in physical aggregate owner
and its atomic mass/momentum/restore handoff; simply skipping that extension
would be unsafe. Actual subframe contact behavior, hinge limit contact distance
and Unity6 response amplitude remain test questions, not claimed solved facts.

Next step, **only if the user approves**: implement and test this one installed
engine physical-mount packet, preserving the current default behavior until its
ownership, restore and accepted suspension regressions pass. Do not begin the
physical stage merely because this proposal exists.
