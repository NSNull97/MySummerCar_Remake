# Cockpit C2 — physical ignition and logical key access

Status: **ImplementedUnverified / automated checks passed, manual acceptance pending**.
Night authorization ends 2026-09-05 03:00 UTC. Manual acceptance is pending.

## Evidence and scope

Use the locked donor `UseNew` component107294 at byte104412228 and
`PlayerKeys` component106395 at byte85492614, as recorded in
`SATSUMA_COCKPIT_CONTROLS_AUDIT_2026-09-05.md`, especially sections2 and10.
The original/staging remain read-only. No FSM or donor runtime is imported.
Fresh Satsuma logical key possession is true; possession can later change and
is persistent. Column Installed, not Bolted, exposes the physical lock.
LMB drives Off/Accessory/Starting with a realtime0.4s hold; exact gesture and
starter-circuit conditions receive a final bounded donor preflight before code.

This packet does not enable seated driving, rebuild the car/world, change
accepted suspension/assembly geometry, or claim complete engine prerequisites.

## Compatible implementation design (recorded before implementation)

1. Plain `ISatsumaKeyAccess` / `SatsumaKeyAccessState` in Vehicle.Runtime:
   logical content ID `vehicle.satsuma.key`, fresh=true, explicit SetAccess(bool).
   This is not a physical item or a StableEntityId GUID. NativeSaveSession owns
   one instance per initialization, shared by the save participant and loaded
   vehicle bindings. No key ownership is derived from the vehicle hierarchy.
2. Required `vehicle.satsuma.key-access`, schema1, GlobalState save participant.
   Prepare validates without mutation, capture/apply preserve false, rollback
   restores the checkpoint. A standard deterministic v16->v17 migration adds
   fresh=true to old documents, sorts domains, and never mutates its source.
   Existing native registry/restore architecture remains unchanged. Optional
   domain is rejected: missing optional domains are skipped, so in-place legacy
   load over current=false would incorrectly retain false.
3. NativeSaveSession passes the same key capability to VehicleSaveParticipant.
   RegisterBinding injects it through explicit `BindKeyAccess` before deferred
   vehicle restore. All existing public initialization/constructor call shapes
   remain valid. No Player/UI/new-game paint callback becomes a key owner.
4. Separate `SatsumaIgnitionController`, LMB continuous interaction target and
   `SatsumaIgnitionInputAdapter : IVehicleInputSource`. Adapter delegates other
   input and reset to the existing router, replacing ignition/start intent only.
   The disabled Satsuma Vehicle action map stays disabled. Existing host, engine
   and electrical prerequisites remain authoritative; pending audit resolves
   the currently missing explicit starter-cable gate before START is connected.
5. Existing vehicle record `bool ignitionOn` stays unchanged. With controller
   present, capture non-Off; restore false->Off, true->Accessory. Existing
   router-only fixtures retain their fallback. Starting and held duration are
   transient and cancel on release/disable/restore/invalidation. Missing access
   gates operation; do not invent automatic loss of a saved Accessory state.
6. A bounded Editor refresh authors only the reviewed physical lock binding,
   controller/adapter and persistence reference in the canonical car prefab.
   Existing colliders, geometry, stable IDs and all117 mount definitions must
   remain unchanged. Full builder receives the same helper for reproducibility.
   Reject drift/partial bindings rather than silently authoring duplicate locks.

## Compatibility risk

Native v17 reads/migrates v16. The already delivered v16 executable024640 cannot
load a newly written v17 save. Do not reuse a v17 slot in that older build;
an old build saving the same slot could overwrite the newer format. No live
user save is opened for writing in this work. Existing storage backup rules
remain intact. This is an additive save-domain migration, not a rewrite of
vehicle DTOs or save foundation. A fresh build is needed to test C2 outside Editor.

## Planned checks

- Fresh/false key state, strict domain validation, source-immutable migration,
  in-place legacy load false->true, savedfalse restore, global-before-vehicle
  ordering and rollback after a later participant failure.
- LMB-only short/long gestures, realtime threshold, cancellation/restore,
  column Installed/notBolted, key denial, powerless lock operation, actual
  starter electrical prerequisites; non-ignition input/reset delegation.
- Existing ignitionOn true/false compatibility including capture while Starting.
- Authoring identity/idempotence, one physical target, serialized references,
  unchanged117 definitions/manifest/build settings and existing prefab payload.
- Focused EditMode/PlayMode regressions; report actual counts and failures.

## Implemented behaviour and evidence

The design above was recorded before code. The additive implementation now uses
the existing continuous mouse interaction, vehicle input/host and transactional
save boundaries. No Player, seat owner, input-map activation or UI redesign was
introduced by C2.

- Off + LMB down enters Accessory immediately. Holding until the absolute
  realtime deadline `start + 0.4` enters Starting; short release remains ACC.
- From ACC without an attempted start, a short LMB gesture turns Off on release;
  a long gesture enters Starting. START release returns to ACC. The donor's
  local `MotorOn` means attempted start, not confirmed engine running: even an
  unpowered attempt makes the next LMB down turn Off immediately.
- An installed steering column is enough to expose the lock; its Bolted latch
  is not required. Logical key access gates operation. Missing battery does not
  prevent key rotation. Key local-X positions are Off0 / ACC-30 / START-60 degrees.
- Effective starter intent additionally requires existing ElectricsOk, installed
  starter, installed starter lead and cable stage8. ElectricsOk already requires
  battery, both terminal shoes at8, ignition wiring and voltage above9.7V.
  Existing engine/block and generic prerequisite checks remain downstream.
- Other router channels and consumptive reset edges are delegated unchanged.
  Release, disable, restore or key/column invalidation cancel held START. The
  target also drops a stale ownership latch before a new press if the controller
  was cancelled externally before receiving the target's final callback.
- Native17 adds required `vehicle.satsuma.key-access` schema1; the session's
  single capability is injected before deferred vehicle restore. Migration16->17
  adds true without modifying its input; false, rollback and ordering are tested.
  Vehicle `ignitionOn` remains its existing bool/schema: true restores ACC,
  false Off; START/hold/attempted-start are transient. Router-only bindings keep
  their existing fallback. Missing column/access does not rewrite saved ACC.

Evidence is tied to GAME.unity SHA256
`C3F2F3373CCAD4FCBE104840FCB83E364F55438070E808D11EBE4996BC0476C4`:
UseNew107294 @104412228, Waitbutton104415384, Checkkey104444235,
ACC104436604, ACC2 104440552, START104418561, Shutoff104433827,
MotorOFF104421825. PlayerKeys106395 @85492614, Reset85505382,
Load85499914, Save85494998; Pig-bet Use107250/Winhouse103634813 confirms
access may change but that progression is not implemented here. Starter main
106807 @93007437: ElectricityOK93101093, Motorinstalled93078584 and
Wiring93139007; MotorParts106806 @92996816 maps block Data.Bolted.

## Presentation and preservation audit

Scoped menu: `Tools > MSC Remake > Phase1 > Satsuma > Refresh Physical Ignition`.
Batch entry:
`MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaIgnitionAuthoring.RefreshPhysicalIgnitionBatch`.
Full generation also calls the helper, but was not run. The helper preflights
the locked manifest, all117 typed unique mounts, one column, explicit vehicle
bindings, stable identity and missing scripts. Existing/partial drift is rejected.
References are authored through serialized properties without initializing the
assembly graph and reparenting existing content.

Two reviewed Mesh-only payloads were imported from external staging into ignored
RuntimeBaseline wrappers as `TemporaryDirectImport`, not production art:

| Source | Source GUID | SHA256 |
| --- | --- | --- |
| key_satsuma.asset,15905 bytes | 106152c393ea3d944bcccd352b6ee602 | 8F5225BFA68B54D8BEF99D30981091CA69CFBA69606464F50A319ED3DDE8FEBF |
| ignition_hole.asset,7769 bytes | d99cd1a03153439488c620e94c76032d | 2DE11AD71D85749C566D1D2E9FDC7AB6E8FFB60BA7D998D85D93191930132403 |

Existing sanitized ATLAS_MOTOR material is reused; no FSM, MonoBehaviour, donor
runtime or ReferenceOnly asset becomes a dependency. The lock, Keys pivot,
key and socket local poses follow donor transforms49936/47944/51537/51832.
The dedicated sphere follows47206/99431: local radius0.7 at scale0.026748622
(effective18.724035mm), centre(0,0.5,0), trigger. Mapping the secondary installed
column presentation to the existing primary column owner is an evidence-backed
pose inference, not yet manually accepted.

Refresh02 changed exactly one prefab; Refresh03 changed0. The pre-write backup is
`Logs/codex-ignition-prefab-before-20260905-011014-9779762.txt`.
Existing YAML blocks7363 ->7382:19 added,0 removed. Only4 old blocks changed:
root component list, host input-source reference, persistence ignition reference,
and column child list. Every old column transform value and all other old blocks
are unchanged. Of120 protected hashes, only the prefab differs:

- before `7F4C06B701116F43428C54411C954085E4FA39DAD4736E4BBE1848E7B2F58CF6`;
- after `EFC2DEBCDB4346EF3356D082F5990A4C2FE780855ACEB6D5F10ACFDA656C39AD`.

All117 mount definitions, full-generation manifest and EditorBuildSettings are
byte-identical. No accepted suspension geometry/physics, IDs or mount rules
changed. Original/staging remain read-only; generated payload is Git-ignored.

## Executed checks and retained failure history

1. Refresh01 exited1 on a new test's ambiguous NUnit comparer overload; made
   `Using<string>(StringComparer.Ordinal)` explicit. No refresh occurred there.
2. Refresh02 exited0, changedPrefabs1; Refresh03 exited0, changedPrefabs0.
3. Focused Edit35:34 passed,1 failed due NUnit `Has.Count` reflection on an array;
   replaced with the strongly typed `.Count` assertion. Runtime15 and save13
   already passed. No gameplay assertion was weakened.
4. Broad Edit `Logs/codex-cockpit-c2-edit.xml`:516/516 passed,0 failed/skipped.
5. Initial Play `Logs/codex-cockpit-c2-play.xml`:62/63 passed. All61 baseline
   regressions passed. The second new fixture exposed stale target hold ownership
   after external key cancellation/restore; corrected narrowly and added two
   Edit regressions for re-press without an intervening target End callback.
6. Final Edit518/518 passed,0 failed/skipped, exit0,66.8854229s:
   `Logs/codex-cockpit-c2-edit-final.xml`, SHA256
   `0C17D0EE5139FD71229DA6D41FCE68C826B9A958F31B4B0A723CD7FB9CEE8C0C`.
   Final fixture Play63/63 passed,0 failed/skipped, exit0,80.2439275s:
   `Logs/codex-cockpit-c2-play-final.xml`, SHA256
   `4D4268F39F8CEBDCD9B631B95C8A57FF8CCF1FA53850DB487065D7D99D5674FA`.
7. Initial D3D11 native Bootstrap Play0/1 failed in16.3707179s:
   `Logs/codex-cockpit-c2-native-play.xml`. The new fixture called raw
   `SaveService.Load` after world reveal, violating the existing environment's
   pre-reveal restore contract. Both apply and rollback correctly reject this
   unsupported usage. The fixture now uses the real RequestLoad/Bootstrap
   handoff; the runtime environment guard is unchanged.
   All six pre-existing user save/backup hashes remain byte-identical after the
   failed run; its isolated test slot was cleaned up by the fixture.
8. Final real-D3D11 Bootstrap Play1/1 passed,0 failed/skipped, exit0,27.5199175s:
   `Logs/codex-cockpit-c2-native-play-final.xml`, SHA256
   `70FF7EC1A916D96F7576D8B91D7AF1F790FE96F06E360DEE111D30ADB53CD8FB`.
   Two actual RequestLoad/Bootstrap reloads verify savedfalse in a new v17
   session and legacyv16 migration totrue, both before environment/world reveal.
   Only an isolated GUID test slot was written and cleaned; all six pre-existing
   user save/backup files retain their original hashes. All120 protected hashes
   match post-refresh after final tests; only the intended prefab differs from
   pre-C2. No Unity process remains. No interactive manual or full-engine-driving
   acceptance is inferred from these automated checks.

Commands used the pinned Unity6000.3.11f1 with `-batchmode -projectPath`,
`-runTests -testPlatform EditMode/PlayMode -testFilter -testResults -logFile`.
Edit/fixture Play used `-nographics`; the native Bootstrap fixture used
`-force-d3d11` without `-nographics`. Exact filters/arguments are retained at the
start of each named log. All processes were serial and hidden; full donor/car
generation, player rebuild, commits and push were not run. `git diff --check`
passed for the bounded C2 files; the two meshes remain Git-ignored.

## Manual acceptance and known limits

Manual check still required: install/loosen/remove the column; aim at the lock
with empty hands; test short/long LMB, key travel, release and next-click Off.
Repeat without battery, then with the audited starter circuit and engine
prerequisites. Confirm lock/socket alignment, selection readability and no
unintended wheel/column movement. Save in ACC and during START, reload: ACC must
survive, held/start intent must not. Use a new current build for outside-Editor
testing; the delivered024640 player predates C1a/C1b/C2 and cannot read v17.

Known differences/deferred work:

- Keyless unavailable target is hidden by the accepted contextual UI convention;
  donor retains its collider and branches to NOKEY after a click. Installed socket
  remains visible. This is a documented adaptation, not exact hover parity.
- A release received after0.4s without any intervening simulation tick preserves
  the attempted-start latch but does not invent a crank pulse after release.
- Existing generic engine prerequisites can be stricter than donor Installed
  checks (notably fully-secured starter). Full engine readiness is not claimed.
- Donor persistence of ACC itself is not proven; retaining existing native bool
  semantics is a compatibility decision. Key-loss/disable cancellation is safe
  project-owned integration, not a port of uninspected donor cleanup.
- Key/ignition audio, seated input ownership, steering/gear presentation, full
  gauges/warning lamps and progression changing key access remain separate work.
  The Pig-bet ownership event and complete cockpit/engine parity are not done.

Primary files: Vehicle/Runtime/SatsumaIgnitionController.cs,
SatsumaIgnitionInteractionTarget.cs, SatsumaIgnitionInputAdapter.cs,
SatsumaKeyAccessState.cs, SatsumaElectricalSystem.cs, VehiclePersistence.cs;
Save/Integration/SatsumaKeyAccessSaveParticipant.cs, NativeSaveSessionController.cs,
VehicleSaveParticipant.cs and Save/Runtime/SaveDocument.cs;
LegacyImport/Editor/GameplayPresentation/Phase1SatsumaIgnitionAuthoring.cs and
Phase1SatsumaBaselineBuilder.cs; new ignition authoring/runtime/key Edit suites,
SatsumaIgnitionPlayModeTests.cs and the existing native Bootstrap save fixture.
New scripts have stable .meta GUIDs. No public API was removed or renamed.

Next bounded milestone after C2 validation: confirmed engine fastener presentation
ownership; retirement/alias of duplicate/adjuster IDs first needs save-safe design.
