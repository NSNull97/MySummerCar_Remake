# Engine repair save compatibility — 2026-09-05

Coordinated final validation: **799/799 EditMode, 74/74 PlayMode passed**;
native slot-01 was restored on a copy without rewriting user saves. Authoring
repeat changed0. Manual acceptance remains pending. The authorship-stage notes
below are supplemented by [the final packet report](SATSUMA_ENGINE_REPAIR_PACKET_2026-09-05.md).

Classification: `Reimplemented`, bounded native-save compatibility. This is not a donor-save importer or a save-format replacement. Schema 2 of the assembly graph remains unchanged.

## Exact graph retirements

`VehicleAssemblyController.TryPrepareSaveDataForRestore` applies the existing steering and mount-alias migrations, then `SatsumaRockerCoverFastenerMigration`, then `SatsumaCarburetorFastenerMigration`, before the generic additive migration and final validation.

- Cover: the separately reviewed twelve-to-six exact physical-pair migration; canonical suffixes 2,4,8,9,10,12, retired 5,11,3,7,6,1. Each pair retains maximum stage and OR flags; no source mutation. See the cap/cover report and helper for the frozen source evidence.
- Carburetor: retire only `fastener.satsuma.cylinder-head-carburetor.boltpm-3` on `mount.satsuma.cylinder-head.carburetor`. Frozen marker52523/Screw108773 is mixture adjustment, not an eight-stage mounting screw. Keep the four real mounting IDs1,2,4,5 and their states unchanged. Frozen BoltCheck104785 has MAX32/ON8/OFF0; the earlier generated fallback was MAX40/ON1/OFF0. Validate the old latch before retiring its member; preserve it only if consistent with the remaining real stages, otherwise derive the new latch from occupancy and the new threshold. A fake-stage-only latch therefore becomes loose. Never invent a mixture value from the retired assembly stage.
- Both migrations recognize exact old/new sets, not counts alone. Partial/unknown/duplicate IDs, impossible states and contradictory old latches fail without mutating the caller's payload. A schema-2 old group must exist. Schema-1 absence retains the existing generic latch upgrade.
- Generated target280 stays supported; cover-canonical target274 stays supported; both-canonical target273 stays supported. No backward synthesis of retired aliases into older prefabs. Historical117-mount252/260 additions normalize by the exact target retirements:246/254 on274,245/253 on273. The existing exact missing-lower-strut/exterior-ID audit still runs and refuses same-count corruption.

## Optional part-owned settings

`PartSaveDto` adds explicit `hasEngineAdjustment` / `engineAdjustment` and `hasEngineDocking` / `engineDocking`. This follows the existing steering/camshaft presence-bit pattern because Unity JSON can materialize absent inline objects.

- Adjustment DTO is independently schema1, bound to the existing part identity and the configured adjustment kind. Defaults for old absent data: alternator2°, distributor15°, mixture15, oil filter stage0. Invalid, nonfinite, wrong-kind and wrong-component settings reject before graph mutation. A loose filter may not retain a positive screwed-in stage.
- Docking DTO is independently schema1 with three pending stages, each0..1, aggregate below2. A positive pending stage is valid only on a loose engine block with its configured docking component. At2 the runtime transfers into existing graph fasteners; installed saves have zero pending stages.
- Capture, explicit mount-alias cloning (including a deep clone of the docking array), validation and post-graph restore carry both optional extensions. Old absent settings reset to documented defaults. No native graph IDs or outer schema changes.
- `EvaluateRemoval` blocks ordinary removal of a screwed-in oil filter. Structural collapse remains the existing bypass. Capture refreshes adjustment presentation/state first, so immediate capture after a forced detach cannot preserve a hidden screwed-in loose filter.
- `TryRemoveAtCurrentPose` is a bounded additional controller entry point for engine undocking. It reuses normal removal validation, notifications and physical detach; only the ordinary0.35m hand-removal pose offset is omitted. `TryRemove` behavior remains unchanged.

## Verification status

New isolated fixture: `SatsumaEngineSaveExtensionTests`. It covers old/current targets and additive histories, retirement idempotence/source immutability, wrong-ID/count lookalikes, optional settings/defaults/corruption, docking pending validation and ordinary-versus-forced filter removal. These NUnit cases require the root session's Unity EditMode run; they were **not executed in Unity by this subagent**.

An isolated .NET compile of all current Assembly runtime source plus this fixture succeeded with zero errors. Five existing unused rear-suspension-field warnings are outside this packet. `git diff --check` of the two shared source edits succeeded. No generated assets or saves were written, no Unity instance launched.

Next verification: root runs `SatsumaEngineSaveExtensionTests` with the new helper authoring and existing native-save/suspension regression suites, then checks a real old slot without overwriting its only recoverable copy.

## Read-only original pickup finding

Frozen GAME SHA256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`:

- Engine block GO30853 `block(Clone)` has raw Rigidbody91451 mass95. A full streaming scan found no FSM SetMass with a direct reference to30853; this does not exclude indirect variable targets or managed code.
- Active player Hand GO32546/PickUp113435 has no pickup-time mass comparison; its only GetMass is in `Throw part` for throwing-force calculation. The31kg ItemTrigger comparisons belong to the potato box GO17504 and Jonnez GO34971, not the hand.
- `Set pivot` identifies the engine and enters `Motor kinematic`: GetFsmBool on Block35519/Data.InHoist, true→HOIST→Wait (abort pickup), otherwise SetIsKinematic(true) on PickedObject and carGO28145 before Part picked. Therefore no hand-carry mass limit is established by this source evidence; do not reuse the unrelated31kg threshold.
