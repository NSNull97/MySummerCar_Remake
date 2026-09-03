# Item streaming physics and Satsuma save audit

Date: 2026-09-02. Classification: project-owned `Reimplemented` lifecycle and
save repair. This pass changes no donor payload, donor installation, native-save
schema, stable ID, assembly lifecycle, mount, fastener, NWH authority or accepted
V1d.43/V1d.44 suspension/save synchronization behavior.

## Proven failure

The disappearing engine parts were present in the native document. Read-only
inspection of the three local v16 slots found one vehicle aggregate and 126
unique part stable IDs in every slot, with no duplicate part IDs and no overlap
between vehicle-part IDs and `world.entities` ownership.

| Slot | Loose / installed / root | Loose below Y=-64 | Engine loose below Y=-64 | Installed below Y=-64 |
|---|---:|---:|---:|---:|
| slot-01 | 97 / 28 / 1 | 54 | 32 / 39 | 0 |
| slot-02 | 97 / 28 / 1 | 62 | 37 / 39 | 0 |
| slot-03 | 114 / 11 / 1 | 14 | 3 / 39 | 0 |

The affected loose parts have finite but enormous negative Y values. Across
slot-01 to slot-02, 65 of the same 97 loose IDs fell further, with median
`delta Y=-5,226,592`; 73 retained X/Z within one centimetre. Chassis and
installed parts did not show that signature. This is free fall, not lost DTO
rows, duplicate identities or an assembly-graph migration.

Two independent lifecycle holes produced it:

1. a base-cell scene could unload its floors, shelves and walls while a
   persistent/DDOL pickup, Satsuma chassis or loose `PartInstance` in that cell
   remained dynamic;
2. a clean native load restored and released the persistent Satsuma before the
   first additive world refresh had materialized the home cell's static
   collision.

Loose and installed Satsuma parts are intentionally excluded from
`world.entities`; `vehicle.satsuma` is their sole physics/save owner. That
ownership rule was correct. The missing piece was a matching vehicle streaming
quarantine.

## Ownership and handoff contract

| Runtime category | Authoritative owner | Base-cell unload | Base-cell load |
|---|---|---|---|
| Scene-owned ordinary pickup | `world.entities` physics plus its logical domain when applicable | capture authoritative state, disable interpolation/collision, zero dynamic velocity, make kinematic, then allow scene teardown | materialize static scene first; apply pose while guarded; `Sync`, restore collision while kinematic, `Sync`, release dynamics, `Sync` |
| Rehomed/persistent pickup | `world.entities` | spatial ownership from current body position; suspend in DDOL instead of destroying | thaw only when the matching base collision cell is loaded |
| Loose Satsuma part | `vehicle.satsuma` | spatially suspend the part body without changing `Loose` lifecycle | restore the preserved body state after static support exists |
| Installed kinematic part | assembly graph/mount | follows chassis/mount; no independent recovery or lifecycle mutation | assembly synchronization remains authoritative |
| Installed jointed/dynamic part | assembly graph plus vehicle physics | quarantine the body, preserving `Installed`, mount and fasteners | release with the aggregate after support returns |
| Nonpersistent vegetation/backdrop/presentation | presentation layer | ordinary unload; no stable save identity and no physics restore batch | ordinary recreate |

Scene registration is now a batch barrier. Every involved Rigidbody is frozen
before restore, and a freshly streamed canonical clone discarded in favour of
an already rehomed authoritative instance remains inactive, kinematic and
collisionless until its deferred Unity destruction completes.

A transactional checkpoint also records scene root, parent/sibling ownership,
collision/interpolation flags, rehomed-source mapping and suspension topology.
If any later save participant fails, rollback returns the pickup to the exact
pre-load scene topology and physics state instead of leaving a half-rehomed
guarded object behind.

## Static-before-dynamic native load

The one-shot slot handoff is still consumed during fresh Bootstrap
initialization, but applying it is deferred:

1. the persistent Satsuma aggregate is quarantined during front-end startup;
2. the initial player focus performs the first `RefreshNow`, materializing home
   base collision;
3. the home recovery cell is temporarily retained;
4. the transactional native load restores logical state and guarded physics;
5. restored player focus and retained source cells drive a second
   `RefreshNow`;
6. deferred objects materialize while both source and home recovery support are
   available;
7. the temporary home retention is released and gameplay is activated.

There is no artificial fixed-step delay. Readiness is the project-owned base
cell collision boundary, not presentation-layer callback order.

## Below-world recovery and save healing

The configured invalid threshold remains `Y=-64`. During load, the following
saved states below it are repaired in memory:

- the vehicle chassis aggregate;
- only parts whose saved lifecycle is exactly `Loose`;
- ordinary `WorldItemInstance` objects whose authored definition explicitly
  has `CriticalRecovery`.

Installed parts are never scattered into the recovery formation, even if an
informational installed-part world position is negative: the mount graph owns
their pose. Other noncritical objects are not silently promoted to important.

Recovery uses project-owned anchor
`anchor.home.important-object-recovery`. Candidates are allocated in
deterministic domain/cell order and stable-ID order inside each batch, within a
bounded 20 x 20 candidate formation with 0.75 m pitch: candidate centres span
14.25 m across and 14.25 m forward from the configured centre. Dense candidates
do not permit dense overlaps; actual collider bounds reserve the required
space. Each slot must have a static
`WorldSurface` or `WorldSolid` raycast hit, must not overlap another reservation,
and must pass a collider overlap check. Collider bounds are conservatively
rotated to the restored orientation. If no safe supported slot exists, load
fails clearly instead of spawning an important object in a wall or under the
map.

The configured centre `(157.08923, 8, -1022)` shares X with the frozen front
door `(157.08923, 2.1022449, -1030.2072)` and extends in +Z, away from the house
and its rear door. It resolves to adjacent `cell_0_-2`; the player starts in
`cell_0_-3`, whose radius-one initial load includes that neighbour. Three
global `WorldSurface` mesh-collider AABBs cover the point, but offline evidence
cannot prove a triangle under every candidate. Nearby milk/hedge geometry is
why runtime support and overlap rejection remain mandatory rather than treating
the coordinates as an unquestioned donor anchor.

Recovered poses keep the same vehicle, part and item stable IDs. Loose parts
remain `Loose`; installed parts remain `Installed`; velocities are zeroed. The
existing `current.save.json` is never edited by this recovery pass. After the
player loads the slot and saves normally, ordinary capture writes the recovered
above-world coordinates, which heals the next load without a special migration.

## Validation

Synthetic PlayMode coverage uses real additive scene unload/recreate events for
shelf and wall contacts, two cycles each, plus a persistent spatial pickup,
the rehomed/canonical-clone destruction path and a production-like direct
streaming-focus teleport. The teleport case uses the real automatic
`ProductionWorldStreamingService` update and owned-scene events with the
destination manifest entry deliberately ordered before the source. Synthetic
vehicle coverage uses a project-owned aggregate containing a corrupt chassis,
two corrupt loose engine parts and an installed engine with a deliberately
negative informational pose.
It asserts supported non-overlapping formation slots, zero chassis velocity,
unchanged installed lifecycle/mount/identity, unique IDs and a second healed
JSON round trip.

Unity `6000.3.11f1` executed:

- `WorldEntityStreamingPhysicsPlayModeTests`: **6/6 passed**, including two
  real unload/recreate cycles for shelf and wall contacts, persistent spatial
  suspension, canonical-clone quarantine, forced transactional rollback and a
  direct focus jump through the real automatic streaming event order;
- `SyntheticVehicleRecovery_RoundTripsCorruptedChassisAndLooseEngineBlockWithoutMutatingInstalledEngine`:
  **1/1 passed** in EditMode.

Artifacts are `Reports/streaming-physics-playmode.xml`,
`Reports/streaming-physics-playmode.log`,
`Reports/vehicle-recovery-editmode.xml` and
`Reports/vehicle-recovery-editmode.log`. The worktree lacks its private Enviro
3 and Easy Performant Outline payload, so the focused run used temporary
junctions to the approved installed main-checkout copies. They and their
Unity-generated sibling meta files were removed after the run; vendor sources
were not modified. Logs retain unrelated missing-Wwise-bank and headless shader
noise, while both TestRunner results completed with code 0.

The local native slot files were inspected read-only and were not rewritten.

### Live feedback triage

The first user playtest after this implementation did not execute the code under
test. The human-controlled Unity `Editor.log` records project path
`E:\GAYmDev_Studio\MySummerCar_Remake`, while this task's implementation and
automated results belong to
`C:\Users\NSNull\.codex\worktrees\366a\MySummerCar_Remake`. The launched
checkout had no `ImportantObjectRecoveryFormation.cs`; its vehicle participant
restored the saved aggregate directly and its world participant had none of the
new persistent-body quarantine.

That old runtime saved `slot-02` twice while the test was observed. At the
13:14:52 snapshot it still had all 126 unique part IDs and the same
`97 Loose / 28 Installed / 1 AssemblyRoot` lifecycle split, but 63 loose parts
were below `Y=-64` and the engine block had fallen to approximately
`Y=-17,815,878`. Chassis and every installed part remained above the threshold.
The observed result is therefore evidence that the old free-fall path persists;
it is not a failed acceptance result for the recovery implementation. The slot
remains structurally recoverable by stable ID after the worktree changes are
integrated.

## Remaining acceptance

- Validate the configured home-front origin and the whole bounded formation in
  the private generated home cell with gizmos/collision visualization.
- Execute one full `RequestLoad -> Bootstrap Single load -> two RefreshNow`
  smoke with the private generated Satsuma and one deliberately corrupt copy of
  a slot; never use the only copy of a player save as the fixture.
- Confirm the recovered set is visibly in front of the home, save to a new slot,
  reload it, and verify the formation repair does not run again.

Explicit `UnloadOwnedScenes` and `EndGameSession` remain unconditional teardown
APIs. Cell retention vetoes ordinary gameplay `RefreshNow` transitions; it is
not intended to keep scenes alive while the whole game session is being
destroyed.
