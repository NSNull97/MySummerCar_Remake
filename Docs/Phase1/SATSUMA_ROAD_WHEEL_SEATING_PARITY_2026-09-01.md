# Satsuma road-wheel seating parity

Status: V1d.41 implementation and scoped automated validation passed. Manual
Bootstrap acceptance is `USER PASS` as of 2026-09-02. The separate save
physics repair was subsequently completed and accepted in V1d.44.

## Frozen donor evidence

Read-only authority is the frozen `GAME.unity` with SHA256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`
and the existing runtime inspection captures under donor staging.

Each donor corner has two wheel installation branches:

- `wheel_regula` -> `PART1` -> `Assemble` -> `Pivot1` / standard seat;
- `wheel_offset` -> `PART2` -> `Assemble 2` -> `Pivot2` / offset seat.

The measured separation between those seats is 43 mm at either front corner
and 40 mm at either rear corner, along the mirrored mount's local X axis.
This is a type-dependent seat choice, not one universal world-space offset.

Every road wheel currently accepted into the generated remake baseline uses
the donor `wheel_regula` identity:

| Remake part | Donor object | Donor Use FSM |
|---|---|---:|
| `wheel-stock-fl` | `wheel_steel1` | 113900 |
| `wheel-stock-fr` | `wheel_steel2` | 111136 |
| `wheel-stock-rl` | `wheel_steel3` | 108243 |
| `wheel-stock-rr` | `wheel_steel4` | 111898 |
| `wheel-gt-fl` | `wheel_gt1` | 114016 |
| `wheel-gt-fr` | `wheel_gt2` | 109103 |
| `wheel-gt-rl` | `wheel_gt3` | 112738 |
| `wheel-gt-rr` | `wheel_gt4` | 107110 |

The donor classifies Hayosiko, Octo, Racing, Slot, Spoke, Steelwide and Turbine
families as `wheel_offset`. None of those families is currently an accepted
part in this generated baseline.

## Implemented correction

The existing mount-owner transforms are retained because they also own
fasteners, suspension following and the NWH handoff. Their child `MountPose`
now recovers the donor standard seat for all currently supported wheels:

- front road-wheel mounts: local X `-0.043 m`;
- rear road-wheel mounts: local X `-0.040 m`;
- rotation and scale remain identity/one.

Left/right mount rotations mirror this local correction inward. The previous
user-measured `-0.034 m` trial was mechanically safe but remained 9 mm outward
at the front and 6 mm outward at the rear relative to the donor standard seat.

This change does not alter NWH contact geometry, track width authority,
suspension force/travel, steering/camber/toe, the four wheel fasteners, stable
IDs, assembly prerequisites or save DTOs.

When an actual donor `wheel_offset` family is imported, wheel installation must
gain a part-type seat selector before that family is accepted. Building that
unused selector now would unnecessarily cross `PartInstance`, handoff and save
boundaries, so it is intentionally deferred with the absent wheel families.

## Executed verification

| Artifact | Result | Coverage |
|---|---:|---|
| `codex-satsuma-v1d41-wheel-seating-build.log` | exit 0 | generated prefab, builder `11A-V1d.41` |
| `codex-v1d41-wheel-seating-generated-class.xml` | 29/29 | exact front/rear standard MountPose and generated assembly contracts |
| `codex-v1d41-wheel-seating-installed-class.xml` | 18/18 | installed-part physics, front and rear wheel following, authority handoff |
| `codex-v1d41-wheel-seating-rear-regression.xml` | 9/9 | accepted rear lifecycle, droop and loaded-compression behavior |
| `codex-v1d41-wheel-seating-bootstrap.xml` | 1/1 | production Satsuma Bootstrap composition |

## Manual acceptance result

`USER PASS`, 2026-09-02. The current regular stock/GT wheel family was observed
installed and rolling without a remaining hub/drum seating defect. This does
not claim support for donor offset-family wheels that are not imported yet.

Retained regression recipe:

In a fresh Bootstrap run, install at least one stock front and rear wheel, then
repeat with GT wheels if practical. Confirm that both sides seat against the
  hub/drum without a visible gap or penetration, fasteners remain reachable and
  the accepted suspension response is unchanged.
