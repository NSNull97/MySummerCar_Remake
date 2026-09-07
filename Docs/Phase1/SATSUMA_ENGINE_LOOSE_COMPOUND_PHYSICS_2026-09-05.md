# Loose engine compound contact, mass and carrying — 2026-09-05

Coordinated final validation: **799/799 EditMode, 74/74 PlayMode passed**;
native slot-01 was restored on a copy without rewriting user saves. Authoring
repeat changed0. Manual acceptance remains pending. The authorship-stage notes
below are supplemented by [the final packet report](SATSUMA_ENGINE_REPAIR_PACKET_2026-09-05.md).

Status: project-owned compatibility extension, source/test implementation ready;
coordinating task owns executed Unity results and generated prefab integration.
No Unity process was launched by this subtask. No original files were modified.

## Why this is needed

Installed engine children use their own kinematic Rigidbody and their original
solid colliders are disabled by the established `PartInstance` contract. Their
separate interaction triggers remain queryable. Parenting those child bodies
under the loose engine does not make their shapes participate in the engine's
dynamic Rigidbody. Consequently a complete floor engine could be supported only
by the outer block's original shapes, and its carried mass stayed the bare-part
value. Do not restore the nested kinematic solids: that creates independent
immovable obstacles rather than contacts belonging to the loose engine.

`AssemblyLooseCompoundPhysics` is an explicitly opted-in physics consumer. It
does not replace the graph, source colliders, save DTOs, body installation rules
or chassis mass controller. Runtime-only proxy colliders are placed directly
under the true outer loose body's Transform, using explicit source bindings.
Only boxes, spheres, capsules and convex meshes are accepted; no bounding-box
approximation is substituted. Source layers, materials, shape parameters,
contact offset and transformed pose are preserved. Shape sources remain owned
by the original part; query targets still select the established part/bolt
capabilities. Proxy objects have no persistent ID and are marked DontSave.

The installed owner chain must traverse explicit `AssemblyOwnedMountAuthoring`
links. A missing/cyclic link, unbound owner, assembly root, or dynamically jointed
installed body ends eligibility. No Transform-parent fallback can lift the
chassis or include suspension. When the complete engine enters the car, all
loose-engine proxy contacts switch off and the body's own mass/CoM are restored;
the existing chassis consumer becomes authoritative. Removing the retaining
engine/subassembly reassigns the same proxy pool to its new outer loose owner.

## Mass, center and lifecycle

Each bound part contributes `PartDefinition.MassKilograms`, never the already
aggregated `Rigidbody.mass`. The own center is captured in authoring, before
runtime installation disables shapes. Weighted centers use body-frame rotation
and translation (Rigidbody.centerOfMass does not scale with Transform scale).
The owned mount chain decides aggregation; inactive/uninstalled alternatives
are separate bodies, never both added to one engine.

Refresh follows installed-pose synchronization. It recomputes the current
weighted values, but only writes changed body/proxy poses and resets inertia
when necessary. Repeated refresh cannot accumulate mass or shift CoM. Disable
restores each body's own mass/center and disables the pool; reenable rebuilds
the current ownership without recapturing an aggregate center. All binding
validation occurs before cache population and proxy creation.

The root-owned `PartInstance` collider snapshot extension excludes nested parts
and `AssemblyCompoundColliderProxy` contacts so installation/removal cannot
restore a child's solid or ghost contact as an outer part's own collider.

## Authored scope and measured totals

`Phase1SatsumaEngineCompoundPhysicsAuthoring.Configure(assembly)` runs after
`Phase1SatsumaEnginePickupAuthoring`. It verifies the same explicit engine
definition closure and requires each registered part's existing pickup adapter
to reference that assembly and surface part. It serializes the exact owned,
active-below-part, enabled non-trigger shapes; nested part shapes are excluded.
The pass is idempotent and preserves non-engine bindings.

Read-only current generated prefab traversal found **39 existing engine pickup
bindings and 58 owned enabled solid collider references**, all 39 parts having
at least one. The source manifest's corresponding collider inventory also
sums58. Current definition/manifest masses total **178.6 kg across the complete
39-part inventory**, including both alternative rocker covers. This is not one
legal assembly. Selecting either the 1 kg stock or 1 kg GT cover gives **38
parts / 177.6 kg / 56 source shapes** in the current complete engine graph,
including its gearbox, mounted accessories and currently registered legacy
oilfilter0. A full stock assembly has three original block shapes plus up to53
active child proxies. This is a definition-level total, not a claim that every
currently registered legacy consumable is accessible through finished gameplay.
Fluids, future item-consumable bridges and unregistered parts are not invented.

The block's donor-derived own mass is95 kg. The **120 kg carry limit** is a
separate user-confirmed remake extension, not donor parity: the original pickup
logic does not use that mass threshold. Every reviewed engine PhysicsPickupTarget
gets `ConfigureAssemblyCarryLimit(120f)`, enabling the root-owned per-player
debug override. The normal limit reads the current compound body mass; the debug
override changes permission only, not physical mass, gravity or contact.

## Prepared coverage and integration

- `AssemblyLooseCompoundPhysicsTests`: 11 EditMode cases for floor nested mass,
  no drift, CoM, owner-bound solid contacts, split/retaining removal, into-car
  handoff, unchanged chassis mass/force, disabled/re-enabled controller, invalid
  owner/shape rejection, authored center recovery, dynamic-jointed exclusion,
  explicit authoring/idempotence and scoped120 kg bindings.
- `AssemblyLooseCompoundContactPlayModeTests`: two isolated local PhysicsScene
  tests. A lower attached child must support the whole engine on a table;
  removing/moving that child restores two independent bodies and eliminates
  ghost support. Tests simulate only their own scene, not the active game world.

Root integration: call the authoring pass after engine pickup bindings and
before saving/validating the generated prefab. No change to a public stable ID,
existing save section or donor code dependency. Unity compilation, executed
regressions, actual generated binding counts and manual table/carry checks must
be recorded by the coordinating task before marking this packet verified.
