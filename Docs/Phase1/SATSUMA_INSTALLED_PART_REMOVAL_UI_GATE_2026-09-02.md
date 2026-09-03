# Satsuma installed-part removal UI gate

Revision `11A-V1d.45`, 2026-09-02.

Follow-up `11A-V1d.46` extends the presentation gate to the donor-evidenced
`RemovalBlocked` state and adds the corner-specific rear spring/shock removal
predicate. It supersedes the statements below that only `FastenerSecured` is
hidden and that all fastener-free parts are immediately removable. See
`Docs/Phase1/SATSUMA_REAR_SPRING_REMOVAL_PARITY_2026-09-02.md`.

## Scope

An installed part no longer advertises its removal action while its owning
fastener group is latched as bolted. This is a project-owned UI improvement;
it reuses the already transferred donor `BoltCheck` thresholds but does not
claim that the donor renders the same outline or HUD.

The authoritative state remains `FastenerGroupState.IsBolted`:

- the latch engages at `BoltedOnThreshold`;
- partial loosening does not clear it;
- it clears only at or below `BoltedOffThreshold`;
- mounts without removal fasteners never engage the latch.

## Runtime behavior

`AssemblyInstalledPartInteractionTarget.CanInteract` now suppresses only the
`FastenerSecured` removal result. `InteractionCandidate` therefore returns no
removal prompt, the RMB binding disappears, and
`InteractionOutlinePresenter` clears the ordinary part outline because the
current prompt is empty.

The installed-part proxy collider stays enabled. This deliberately preserves:

- wrench access to the separate fastener targets;
- installation handoff for compatible parts mounted onto a subassembly;
- independent actions such as opening a hinged door or lid;
- useful `RemovalBlocked` and `Obstructed` explanations when fasteners are not
  the blocker.

## Automated validation

Unity `6000.3.11f1`, EditMode:

- focused generated rear drum: `1/1` passed (`8 -> 1 -> 0` latch path);
- complete generated Satsuma content: `29/29` passed;
- focused donor rear-shock hysteresis and no-fastener spring: `1/1` passed
  (`1 -> 2 -> 1 -> 0`);
- complete `P0SatsumaBoltCheckParityTests`: `5/5` passed;
- complete `VehicleAssemblyEditModeTests`: `23/23` passed.

Results are in ignored local files under `Logs/codex-installed-part-ui-*.xml`.

## Manual acceptance

Pending in-game confirmation. Expected sequence:

1. Install a bolted part while it is still below its on-threshold: removal
   prompt and outline remain available.
2. Tighten enough to latch `IsBolted`: the part removal prompt, RMB binding and
   outline disappear; its bolts remain selectable with the wrench.
3. Loosen only partway: the removal UI stays hidden.
4. Reach `BoltedOffThreshold`: the removal prompt and outline return
   immediately.

No mount IDs, stable IDs, physics parameters, prefabs, save DTOs or donor data
were changed.
