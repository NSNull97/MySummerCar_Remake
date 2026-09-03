# Satsuma rear suspension fastener mesh parity

Revision `11A-V1d.42`, 2026-09-01. This is the bounded rear-fastener visual
correction requested after road-wheel seating. Suspension forces, travel,
mount poses, fastening stages, latch rules, wheels and saves are unchanged.
Manual in-game acceptance is `USER PASS` as of 2026-09-02.

## Read-only original evidence

Authority: `msc-world-baseline-04a1.1-c3f2f337`, frozen
`raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets`.
`_Scenes/GAME.unity` SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
The live runtime dump was used only to cross-check active stock hierarchy and
stage behavior. No donor file was modified and no donor runtime code is used.

The original marker's direct `MeshFilter` child defines whether the visible
piece is a bolt or a nut. Object name `BoltPM`, wrench size and real-car
expectation are not sufficient. All 12 reviewed visual children have local
position zero, identity local rotation and unit local scale, using the shared
fastener material `98697bae08a8c114ba9774c487f2658d`.
The millimetre wrench values below are the established donor scale convention
(`0.6 -> 6`, `1.2 -> 12`, `1.4 -> 14`) corroborated by its tool interaction;
they are inferred from scale rather than stored as a literal `WrenchSize` field.

| Generated fastener | Frozen YAML Transform | Wrench | Original mesh |
|---|---:|---:|---|
| `trail-arm-rl.boltpm-1` | 63892 | 12 mm | `bolt3`, long bolt |
| `trail-arm-rl.boltpm-2` | 71971 | 12 mm | `bolt3`, long bolt |
| `trail-arm-rr.boltpm-1` | 39901 | 12 mm | `bolt3`, long bolt |
| `trail-arm-rr.boltpm-2` | 70647 | 12 mm | `bolt3`, long bolt |
| `shock-rl.boltpm-1` | 51042 | 6 mm | `bolt`, short bolt |
| `shock-rl.boltpm-2` | 55634 | 6 mm | `bolt`, short bolt |
| `shock-rl.boltpm-3` | 58968 | 12 mm | `bolt2`, nut |
| `shock-rr.boltpm-1` | 51409 | 12 mm | `bolt2`, nut |
| `shock-rr.boltpm-2` | 53482 | 6 mm | `bolt`, short bolt |
| `shock-rr.boltpm-3` | 56872 | 6 mm | `bolt`, short bolt |
| `drum-brake-rl.boltpm` | 48747 | 14 mm | `bolt`, short bolt |
| `drum-brake-rr.boltpm` | 63595 | 14 mm | `bolt`, short bolt |

Exact rear suspension/drum total: **4 long bolts, 6 short bolts and 2 nuts**.
Stock and long rear springs have no fasteners. Rear wheel lugs remain four
`bolt2` nuts per corner with a 13 mm wrench.

With both rear wheels fitted, the visible rear-axle total is therefore **20**:
12 suspension/drum pieces plus 8 wheel nuts. The original has no separately
installed rear hub/spindle/stub-axle part and no extra hub fastener; its carrier
and wheel pivot are inside the trailing-arm hierarchy.

The rally rear-shock alternative has the same physical connection pattern:
top nuts at T37862/T69688 and lower short bolts at T52108/T66577 (RL) and
T38026/T55771 (RR). Its four additional objects also named `BoltPM` are damper
adjusters, not assembly fasteners: they have no visual child, no `Stage` or
`PartAssembled` state, and their FSMs write `SettingBump`/`SettingRebound`.
They must not acquire either a bolt or a nut presentation.

Mesh identities are the same frozen assets already audited for the front:

- `bolt`: source GUID `aec6c756751308a4d830708366ad5cdb`, short bolt;
- `bolt2`: source GUID `e711c8a15b1135c4089caad19b8f56e8`, nut;
- `bolt3`: source GUID `bd64aade39680ac43a380f1c62373e0b`, long bolt.

Runtime InstanceIDs such as `418592` are not valid builder keys. The builder
parses serialized GAME YAML and therefore uses Transform IDs such as `63892`.
The runtime/YAML correspondence was checked by hierarchy and marker order.

## Root cause and correction

`FastenerBuild` intentionally defaults to `bolt2`, the nut mesh. V1d.35 added
an exact override table for the reviewed front suspension, but the rear had no
equivalent table. Consequently all rear markers rendered as nuts even though
their wrench sizes, maximum stages, group tightness and retention behavior were
already correct.

V1d.42 adds a frozen `marker Transform ID -> mesh GUID` table for exactly the
six rear mounts above. During generation every target is required to match:

- a reviewed marker ID;
- exactly one direct donor renderer;
- the expected mesh and material;
- zero/identity/unit child frame.

Only `FastenerBuild.MeshSourceGuid` changes. The marker pose and scale continue
to drive the generated target. Donor runtime `Z=-0.0025*Stage` movement is a
tightening result and is deliberately not baked into the base pose.

Unchanged mechanical contract:

- trail arm: two 12 mm fasteners, aggregate 16, bolted latch 12/0;
- shock: 12+6+6 mm, aggregate 24, latch 2/0;
- drum: one 14 mm fastener, aggregate 8, latch 8/0;
- rear wheel: four 13 mm nuts, aggregate 32, latch 1/0;
- arms/shocks/springs remain body-owned; drum and wheel remain arm-owned.

No `FastenerDefinition`, stable ID, mount, ownership, interaction collider,
assembly dependency, NWH setting, suspension parameter or save DTO changed.

## Automated validation

Executed with Unity `6000.3.11f1`:

- baseline build succeeded:
  `PHASE1_SATSUMA_BUILD_OK version=11A-V1d.42`, 117 mounts and 260 fasteners;
- focused/expanded EditMode **46/46 passed**, no skips:
  rear mesh 6, front mesh 6, BoltCheck parity 5, generated content 29;
- installed-part physics PlayMode **18/18 passed**, no skips;
- production Bootstrap PlayMode **1/1 passed**, no skips.

One combined multi-class PlayMode invocation was terminated after the Unity
Test Runner failed to close a test scene and continuously emitted the existing
no-`AudioListener` warning without producing results XML. The same two suites
were immediately rerun as isolated invocations and both passed as listed above;
this runner issue did not reproduce in either suite independently.

New rear tests lock all 12 source and generated mesh assignments, marker scale,
wrench sizes, zero-fastener springs and unchanged rear-wheel nuts. The existing
front bounded-scope test now excludes both reviewed tables and requires all
remaining 218 fasteners to retain their legacy nut mesh.

Artifacts:

- `Logs/codex-satsuma-v1d42-rear-fastener-mesh-build.log`;
- `Logs/codex-v1d42-rear-fastener-editmode.log` and results XML;
- `Logs/codex-v1d42-rear-fastener-physics.log` and results XML;
- `Logs/codex-v1d42-rear-fastener-bootstrap.log` and results XML.

## Manual acceptance result

`USER PASS`, 2026-09-02. The user inspected the installed rear fastener
presentation in game and reported no remaining bolt/nut mismatch.

Retained regression recipe:

In a fresh assembly flow, check both sides independently:

1. Install each trailing arm: both visible 12 mm pieces must be long bolts.
2. Install each stock rear shock: two lower 6 mm pieces must be short bolts;
   the upper 12 mm piece must be a nut.
3. Install each rear drum: its 14 mm piece must be a short bolt.
4. Confirm springs show no invented fastener and each rear wheel still shows
   four 13 mm nuts.
5. Tighten and loosen them once to confirm the existing stage animation and
   removal gates still behave normally.

Any future mesh change must repeat this check without changing fastening state,
tool sizes or suspension ownership.
