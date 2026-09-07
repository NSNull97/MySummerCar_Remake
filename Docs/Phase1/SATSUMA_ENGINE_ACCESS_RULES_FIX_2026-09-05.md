# Satsuma engine E1 — installation and removal access

Status: `ImplementedAutomatedPassedManualPending`.
Classification: `BehavioralReference` → project-owned `Reimplemented` predicates
and `ConfigurationTransferred` mount authoring. Private Phase 1 only.

## Scope and donor evidence

This is a bounded follow-up to the accepted suspension A/B and automated C
packages. It changes access rules on eight existing engine mount definitions;
it does not retune physics or claim complete engine assembly parity.

Source: the locked staged `GAME.unity`, revision
`msc-world-baseline-04a1.1-c3f2f337`, SHA256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Original/staging data remain read-only. The detailed engine audit is maintained
separately in `SATSUMA_ENGINE_ASSEMBLY_AUDIT_2026-09-05.md`.

| Existing mount | Installation access | Manual removal access |
| --- | --- | --- |
| `engine-block.crankshaft` | Oilpan, all three main-bearing caps and timing cover must be absent. | All three caps and timing cover must be absent. Existing piston and owned-child blockers remain. |
| `engine-block.main-bearing1/2/3` | Oilpan must be absent. Do not invent a crankshaft-installed prerequisite. | Oilpan must be absent; the existing own-fastener gate remains. |
| `engine-block.piston1/2/3/4` | Cylinder head must be absent; the existing crankshaft-installed dependency remains. | Cylinder head must be absent; the existing own-fastener gate remains. |

The crankshaft Assembly FSM `111483` (byte offset `181309113`) reads its own
Installed state and Oilpan / CrankBearing1 / CrankBearing2 / CrankBearing3 /
Timingcover Installed values in the `Already installed` state. Assembly is
allowed only when all are false. Removal FSM `113480` (`218236388`) additionally
reads the already-known piston and flywheel relationships, but does **not** read
Oilpan.Installed. Consequently the installation oilpan gate is not copied into
crankshaft removal.

Main-bearing Assembly FSMs `113599`, `109146`, `104327` read self/oilpan presence,
not crankshaft presence. Piston Assembly FSMs `106238`, `107760`, `105370`,
`113228` require crankshaft presence and exclude cylinder-head presence. Their
additional crankshaft-B and delayed Drop behaviour are outside E1.

## Why the old rules were incomplete

The existing engine dependency extraction handles `db_PartRequired`,
`db_PartRequired1` and explicit DetachPart relationships. It does not reconstruct
`db_NotInstalled` access predicates from the donor action graph. The eight
generated definitions therefore had empty occupied-mount access exclusions,
allowing insertion and removal through enclosing engine parts.

E1 reuses `ConfigureSequence`, `ConfigureRemovalBlockers` and the existing
assembly graph. No runtime type replacement, new scene lookup, donor FSM
execution, schema migration or stable-ID change is required. Loose-block
assembly remains supported.

## Implementation and safety contract

- Separate Editor helper `Phase1SatsumaEngineAssemblyRules`; the full builder
  delegates to it, and an independent scoped batch refresh updates only the
  eight reviewed ScriptableObjects.
- Before mutation, validate the locked manifest identity, canonical 117-mount
  prefab bindings, target asset paths and expected prior/current rule shapes.
- Preserve installation supports, bolt thresholds, fasteners, constraints,
  physical bodies, poses, dependencies and all other definitions.
- Do not save the prefab, regenerate presentation or replace the canonical
  full-build manifest. A repeated scoped refresh must change zero assets.
- Existing save DTOs remain unchanged; test an assembled engine restore through
  the new access rules without re-running assembly operations.

## Deliberately not changed

- Main-bearing donor B hysteresis (`10` / `0`) versus current generated values.
- Piston donor B hysteresis (`2` / `0`) versus current generated values.
- Shared crankshaft B written by multiple bearing BoltCheck FSMs; do not replace
  this with an invented all-bearings-tight rule.
- Delayed crankshaft/flywheel Drop, cascades and engine speed damage.
- Flywheel's compound InspectionCover AND Gearbox presence gate. The existing
  flat any-blocker array cannot express that conjunction faithfully.
- Cylinder-head gasket assumptions, combustion, fluids and electrical wiring.
- Previously accepted suspension geometry, mass, compression, droop, wheel
  alignment/offsets and handbrake.

## Validation record

Unity `6000.3.11f1` scoped refresh PID `19020` exited `0`: exactly eight changed
definitions (`Logs/codex-engine-e1-refresh-01.log`). The second refresh, PID
`12880`, exited `0` with zero changes (`Logs/codex-engine-e1-refresh-02.log`).
Across 120 captured file hashes, only the eight intended mount assets changed;
the other 109 definitions and the three protected files below were unchanged.

EditMode PID `9572` exited `0`: **125/125 passed**, failed `0`, skipped `0`,
duration `56.6747097 s` (`Logs/codex-engine-e1-edit.xml` / `.log`). This comprises
E1 access/save/idempotence `19`, generated content `39`, assembly `27`, C removal
policy `23`, C generated `5` and front latch/save compatibility `12` cases.
The restore test checks unchanged input DTO, all part identities/lifecycle/mount
occupancy, all bolt stages and all group latches; it does not require byte-exact
world-space floating-point pose reconstruction.

The first Unity refresh compiled without C# errors. It reported five unique
obsolete ShaderUtil API warnings in the parallel TextureInventoryExporter
work, each printed twice; no warning originated in the E1 helper or tests. The
earlier helper-only offline compile also exited `0`, but it preceded the final
strict-shape/reference preflight refinement and is not a substitute for Unity
validation.

PlayMode PID `26512` exited `0`: **11/11 passed**, failed `0`, skipped `0`,
duration `5.0750606 s`, fixture `VehicleAssemblyPlayModeTests`
(`Logs/codex-engine-e1-play.xml` / `.log`). This is a targeted aggregate/assembly
regression, not a claim that every engine gameplay scenario was exercised in
PlayMode. The prior C-wide 278 EditMode / 61 PlayMode pass remains separate.

Final comparison after both test runs: all 120 captured hashes were unchanged
from the first E1 refresh; relative to the original snapshot, exactly the eight
intended definitions changed. XML SHA256:

- EditMode: `05D14A9F1507293762499AEF8F61F7D2D8AFAF7F0F2781C87E22AD28EC0E63B6`.
- PlayMode: `06184BB0A7FF4739CFB665BBBFEF30AADC6AA1DBE7498B75555634AA88D340C1`.

Scoped source `git diff --check` passed. Manual acceptance remains pending.

Pre-refresh protected hashes:

- Satsuma prefab: `7F4C06B701116F43428C54411C954085E4FA39DAD4736E4BBE1848E7B2F58CF6`.
- Canonical manifest: `3BF4C2535BFF9CE40520FE117220FD0AD1434765A8025AEA0847A14DD524B4DF`.
- EditorBuildSettings: `1849B8BE614E204FB0CF463CBCCC784380598D1BB4302E82AC3840F46CF2E454`.

The independent Windows build task
`01a06e01-469a-7e93-b6ba-d50695c79625` received `BUILD_WINDOW_RELEASED` after
the final tests/hash check and a fresh empty Unity process check. Root and its
agents must not launch Unity or edit Assets, ProjectSettings or Packages until
that task returns `BUILD_WINDOW_RETURNED`. Documentation and read-only audits
may continue. No build success is claimed here; the build task owns that result.

## Manual checks

On a loose engine block, check each access gate in both directions: blocking part
present → operation denied; blocking part removed → operation allowed. Check
all three bearing caps and all four pistons, including placing caps before the
crankshaft. Save and load an assembled crankshaft/piston/head combination and
confirm the assembly and bolt stages survive. E1 does not promise donor-accurate
loose-bearing collapse; that requires the separate shared-B/Drop investigation.

Next bounded milestone after E1 and the build window: cockpit controls audit and
evidence-backed fixes, as added to the user's night plan.
