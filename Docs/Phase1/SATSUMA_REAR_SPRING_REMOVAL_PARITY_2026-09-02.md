# Satsuma rear spring removal parity

Revision `11A-V1d.46`, 2026-09-02.

## Donor evidence

The locked donor `GAME.unity` SHA-256 remains
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
The installed spring `Removal` FSMs gate both mouse-over and removal through
same-corner shock `Data/Installed` values:

- stock RL component `113824` and long/rally RL component `112850` check
  `Shock_RL` (`22955`) and `Shock Rally RL` (`11018`);
- stock RR component `107537` and long/rally RR component `107450` check
  `Shock_RR` (`12404`) and `Shock Rally RR` (`33764`).

The predicate is `Installed`, not `Bolted`. A completely loosened shock still
blocks spring removal until the shock itself is detached. The donor assembly
trigger does not require a spring before installing a shock.

## Remake correction

`MountPointDefinition.RemovalBlockedWhileOccupiedMountIds` is a new additive,
mount-specific removal predicate. Both stock and long spring mounts point to
the shock mount on their own corner. This avoids the incorrect part-ID model
where a right shock could block a left interchangeable spring.

`AssemblyGraph` evaluates that predicate from the actual mount occupied by the
part. `VehicleAssemblyController.EvaluateRemoval` therefore returns the normal
`RemovalBlocked` result while the same-corner shock remains installed.
`AssemblyInstalledPartInteractionTarget` suppresses the removal prompt and
prompt-driven outline for both `FastenerSecured` and `RemovalBlocked`; the
proxy stays enabled for fastener targeting and nested mount handoff.

The existing live spring-change physics fixture now follows the donor order:
loosen and remove shock, change the spring, then reinstall and tighten shock.
No suspension force, travel, pose, wheel, stable-ID or save DTO changed.

## Automated validation

Unity `6000.3.11f1`:

- Satsuma baseline build: success, builder `11A-V1d.46`;
- `P0SatsumaBoltCheckParityTests`: `5/5` passed, including the exact
  loose-but-installed shock gate and affordance return after shock removal;
- `Phase1SatsumaGeneratedContentTests`: `29/29` passed, including all four
  `RL/RR x stock/long` mount mappings;
- `VehicleAssemblyEditModeTests`: `23/23` passed;
- `SatsumaRearDroopPlayModeTests`: `4/4` passed;
- `ProductionSatsumaBootstrapPlayModeTests`: `1/1` passed.

Local results are under `Logs/codex-spring-removal-v1d46-*.xml`.

## Manual check

Install a rear spring and its same-corner shock. The spring must have no
ordinary removal outline or prompt. Loosen the shock fully: the spring must
remain hidden. Remove the shock: the spring outline and `Снять` prompt must
return immediately. Repeat on the other corner; the opposite shock must have
no effect.
