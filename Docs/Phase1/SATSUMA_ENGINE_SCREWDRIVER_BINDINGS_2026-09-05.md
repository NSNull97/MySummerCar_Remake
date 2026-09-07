# Satsuma: three reviewed engine clamp screws

## Evidence and scope

Classification: `BehavioralReference` / `Reimplemented`. The action-level donor
evidence was inspected read-only in the locked staged `GAME.unity`; source SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
See `SATSUMA_ENGINE_CONSUMABLE_BRIDGE_PROPOSAL_2026-09-05.md`, three-clamp addendum.

| Mounting screw | Donor marker / Screw FSM | Project fastener ID |
| --- | --- | --- |
| Alternator clamp | 65277 / 112463 | `fastener.satsuma.engine-block-alternator.boltpm-3` |
| Distributor clamp | 45067 / 106655 | `fastener.satsuma.engine-block-distributor.boltpm-1` |
| Radiator hose 2 clamp | 55496 / 109558 | `fastener.satsuma.engine-block-radiator-hose2.boltpm-1` |

All three markers have scale 0.65 and slotted bolt5 presentation. Tool-pickup FSM
110228, `Screwdriver`, SetFloatValue action 14 writes global ToolWrenchSize=0.65.
Player `Check tool` FSM 105041 compares the marker scale with that value using
tolerance 0.02. This, not the screws' local BoltSize=0 field, establishes the tool.

## Bounded authoring correction

`Phase1SatsumaEngineScrewdriverAuthoring` binds exactly these three definitions to
the existing unsized `Screwdriver` rule, binds their existing interaction targets
to `tool.satsuma.screwdriver`, and registers that tool on the assembly controller.
The tool is persisted as `ToolDefinitions/screwdriver.asset`; the full builder
passes its active tool folder, including transactional staging.

Legacy Size=6, stages, directions, inserted/removal flags, stable IDs, fastener
counts, poses, mount gates and save DTOs stay unchanged. No public runtime API is
replaced. Existing runtime tool matching and directional scroll are reused.
Bindings are validated before shared definitions are mutated. A second pass is
idempotent; existing wrong target references are repaired without recapturing
presentation poses. No compatibility migration is needed: fastening state stays
keyed by the same fastener IDs and only the accepted tool changes.

Carburettor marker 52523 is a tuning screw and is deliberately excluded. No spark
plug/tool registration is added before the separately reviewed consumable bridge.
This is source authoring plus synthetic regression coverage, not a completed
live donor/gameplay comparison. The parent task owns Unity execution and final
generated-content validation.
