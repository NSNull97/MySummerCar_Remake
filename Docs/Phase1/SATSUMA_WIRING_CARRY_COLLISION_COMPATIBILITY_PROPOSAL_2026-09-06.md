# Wiring carry collision — compatibility proposal

Status: **proposal only; not implemented and not approved**. Documentation
only: no Assets, native save, generated content or donor file changed; no Unity
process launched. This is a compatibility boundary requiring user agreement.

## Preserved working baseline

Executed coordinating-task results: **1247/1247 EditMode** and **108/108
PlayMode**, zero skipped, recorded in:

- `Logs/codex-night-editmode-wiring-selection-final-20260906.xml`;
- `Logs/codex-night-playmode-service-cabin-final-20260906.xml`.

The actual catalog/provider spool completed six circuits through normal
carry/query/F: FuelTank, both rear lights, Alternator and both headlights.
The foreign-wall negative passed. Individual lamp distances were approximately
.08956/.08963 m; shared points .09302/.09335 m. Normal side approach and rotation,
not expanded reach, achieved these results. **Today's passing core stays intact.**

## Evidence and exact scope

See [original held-collision evidence](SATSUMA_WIRING_HELD_COLLISION_REFERENCE_2026-09-06.md)
for source hashes, active PickUp113435/Use112423 and the original `mainData`
128-byte matrix confirmation. This proposal did not run an original physical
comparison. The 1 kg spool box (.09,.09,.055) m, center (0,0,.013) m, is correct.
Original PART pickup sets root layer16, drop restores19; held16 ignores
Collider2/layer22 while retaining ordinary world/terrain contact.
This is a general donor PART rule, **not a wiring-only source exception**.
The proposed modern spool-only adaptation does not claim full layer16 parity.

Exact source-layer22 chassis MeshColliders (diagnostic names, not runtime IDs):

- 91784 floor; 91847 roofright; 91867 fender_in_left; 91869 wheelwell_right;
- 91871 rear; 91938 front; 92003 firewall_right; 92187 floor2;
- 92200 firewall; 92201 rear_left; 92207 pillar_left; 92284 roof;
- 92311 left; 92334 rear_right; 92417 floor3; 92590 firewall_left;
- 92604 pillar_right; 92606 roofleft; 92698 fender_in_right;
- 92700 right; 92736 wheelwell_left.

The evidence report maps all 21 to current serialized Collider references.
Do not reuse the whole 29-entry carry scope: rearwindow92252 is source layer2;
remaining17/23/9 shapes are also outside22. Other parts/vehicles need a separate
scope decision. No modern layer-number16 assignment or global matrix edit.

## Existing contracts and minimum compatible packet

All source paths below are under `Assets/Game/`; line numbers are audit checkpoints.

1. Reuse `Interaction/Runtime/Carrying/CarryCollisionBypassScope.cs:12`, an
   explicit Collider array. A new vehicle-side binding in the existing
   `MSC.Vehicle.ItemsIntegration` assembly stores only the reviewed21 refs.
   Editor authoring validates exact cohort, missing/duplicate/foreign targets
   before writing; repeat scoped refresh must report zero changes.
2. Use existing `Items/Runtime/ItemWorldRuntime.cs:59–69` events
   InstanceMaterialized/Removed, PresentationAttached and LoadedInstances.
   `Save/Integration/VehicleSaveParticipant.cs:507` is an existing vehicle
   composition seam for BindRuntime, not a request to alter DTO interpretation.
   Match only `DefinitionId == item.wiring-mess`, retain its ordinary identity,
   parent and source cell. Do not expand AssemblyBridge.OwnsDefinition, add
   PartInstance, fake mount, external physics ownership or a replacement registry.
3. Put the opt-in scope on the actual item Rigidbody root: carry searches
   ancestry, not visual children (`PhysicalCarryController.cs:1395`).
   Keep that root binding across provider replacement and initially absent car.
   Initial item materialization and later vehicle arrival both need reconciliation;
   a child presenter alone or only vehicle-time binding misses early-held spools.
4. Current pickup caches held shapes and scope once (:195/:1347); Configure
   merely replaces its array. Therefore pure authoring is insufficient for
   streaming while held. A narrow, default-compatible opt-in scope revision/
   notification plus carry reconciliation must handle changed refs/shapes,
   empty-to-loaded scopes and vehicle disable/re-enable. No whole-scene scans
   or per-physics-tick allocations. Second-owner conflicts must fail explicitly.
5. Maintain an immutable actual-pair ledger with previous GetIgnoreCollision
   values. Restore removed pairs and all pairs on release, throw, disable,
   target loss/handoff; don't mutate an active release snapshot. Current code
   always restores false (:1381), which can revoke another policy's prior ignore.
6. Physical filtering does not authorize ray-occlusion bypass. Preserve actual
   spool position, .1 m tolerance, reachable-candidate filter and cluster/F input.
   Leave camera collision, foreign walls and unreviewed physical shapes intact.

No mass, PhysicsPickupTarget, stable-ID, save-schema or migration change is
needed to represent this exception. Fields would be additive/default-off.
The carry lifecycle extension still modifies an accepted foundation and needs
approval and regression evidence; no source implementation is authorized here.

## Release/save safety boundary and mandatory tests

Current `PhysicalCarryController.Release:945–953` immediately restores pairs;
Drop/Throw have no overlap guard. TryPlace:265 does, but is a different path.
An inside-hull release may cause depenetration and chassis impulse.
Carry persistence:112 returns original gravity/kinematic flags, not a safe
position: inside-hull saved position may load as a loose colliding spool.
These are inferred risks, not observed failures in today's passing route.
Measure first; if unsafe, agree a separate bounded release/recovery policy.
No indefinite ignore, hidden teleport, chassis freeze or fake parent.

Required executed coverage before approval of the implementation:

- Exact21 binding/idempotence and old-prefab compatibility; other items,
  players, suspension and engine pickup unchanged; old29 scope untouched.
- Real spool carry and all six F circuits; ground/world/unreviewed shapes solid,
  foreign-wall query blocked; no direct Connect calls establish success.
- Prior-true/prior-false pair restoration for Drop/Throw/disable/loss/handoff.
- Pickup before car arrival; car unload/reload and collider re-enable while held;
  provider arrival/replacement; newly created shapes updated, dead refs cleared.
- Controlled outside/inside-hull Drop/Throw and native roundtrip at real home
  coordinates: measure item and chassis response, preserve original save hashes,
  use copies. Do not disable test colliders to obtain safe results.
- Full accepted EditMode/PlayMode and native-bootstrap regression after focused
  coverage. The current1247/108 passes do not test this unimplemented proposal.

Next step: user review of this wiring-only fidelity packet and safety boundary.
Keep today's passing core; do not bundle general PART16/19 rules, installed-engine
physics, tuning or fluid work into this proposal.
