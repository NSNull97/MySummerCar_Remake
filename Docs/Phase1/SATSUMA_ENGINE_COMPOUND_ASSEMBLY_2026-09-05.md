# Engine compound assembly: bounded floor-assembly correction

Status: implementation in progress; Unity validation not yet executed.

## Evidence and corrected interpretation

Source: locked read-only `GAME.unity`, SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Classification: `BehavioralReference` / project-owned `Reimplemented` rules.

- Head gasket Assembly `107324 @ 105037986`, `Already installed`: reads
  gasket and cylinder-head `10083` Installed; either true rejects assembly.
  Removal `105192 @ 63572118`, `Requirements`: cylinder-head Installed or
  gasket Bolted prevents removal.
- Engine plate Assembly `113357 @ 215596529`: rejects an installed gearbox
  `31625`, stock flywheel `29466`, or racing flywheel `18977`. Removal
  `113919 @ 225731779`: stock-flywheel Installed / plate Bolted prevent removal;
  racing-flywheel Installed also has a rejection branch. The source's successful
  BoolNoneTrue array omits racing flywheel while its rejection array includes it;
  racing-alternative ordering is not inferred from this conflict in this patch.
  The current canonical project represents one stock-flywheel mount only.
- Clutch pressure plate Assembly `106353 @ 84533332`: cover `11583` Installed
  true rejects assembly. Pressure plate is inserted while the cover is loose.
  Removal `111105 @ 174607585`: disc `5151` Installed blocks removal. A later
  GetFsmBool reads pressure-plate Bolted, but the subsequent BoolTest tests
  `Required1` (disc Installed), not `Required2`; do not invent an extra condition
  from the variable names.
- Clutch disc Assembly `113571 @ 219640417`: pressure plate `14555` Installed
  false rejects; cover `11583` Installed true rejects. The generic importer
  incorrectly treated the latter `db_PartRequired1` as a positive install
  dependency and omitted `db_PartRequired2`, making the third floor-assembly
  component impossible to insert. Disc Removal `108162 @ 120582928` tests its
  own Bolted, which the cover BoltCheck shares with the inner clutch components.
- Clutch cover Removal `109148 @ 138871176` Requirements tests only cover Bolted.
  Gearbox and drive-gear references exist in its variables but are not read in
  this state. This corrects the earlier engine audit's overly broad inference
  from variable presence. Assembling pressure plate / disc destroys their
  separate Rigidbody and parents them to cover pivots; the cover remains the
  physical assembly owner.
- Water pump Assembly `109181 @ 139506375`: no additional installed/bolted
  prerequisite beyond its physical timing-cover owner. Original trigger Sphere
  `99695` radius is 0.03 m. Current prefab has the trigger, owner, and radius;
  missing selection is a separate interaction/occlusion investigation.

## Small compatible extension required for complete clutch removal

Existing `AssemblyGraph.HasInstalledRemovalBlocker` unconditionally treats an
occupied owned mount as a parent-removal blocker. Its existing
`RemovalIgnoredDependentMountIds` deliberately excludes owned children, and
changing that meaning would silently weaken accepted suspension rules.

Add a separate `MountPointDefinition.RemovalRetainedChildMountIds` whitelist.
Only an explicitly listed occupied child mount whose owner equals the part being
removed bypasses this inferred ownership blocker. Explicit removal blockers,
fastener checks, structural dependencies and force-break behavior remain intact.
Author the whitelist only on clutch-cover's flywheel mount, for its pressure
plate and disc sockets. Normal `TryRemove` already releases only the requested
mount and detaches its root transform, preserving nested child occupancy.

Compatibility: additive authoring field with empty default; no existing public
API replacement, no stable-ID changes, no save DTO/schema migration, no altered
thresholds or bolt counts, no suspension/body whitelist entries. Existing saves
can retain their same occupancy and fastening state. The faulty clutch-disc
positive dependency and its inverse removal dependency are removed only by their
exact stable part-ID pair; unrelated graph edges remain unchanged.

## Implementation and verification plan

1. Add scoped Editor helper for four access definitions, one cover whitelist,
   and the exact two erroneous dependency edges.
2. Preserve generated prefab IDs, poses, fastener definitions and unrelated
   content; apply through a scoped refresh, not a full donor rebuild.
3. Test floor assembly pressure -> disc -> complete cover onto flywheel ->
   complete cover removal, plus disassembly order, fastening, save round trip,
   idempotence, and default ownership-block regression.
4. Root session handles pump-target selection and picking up complete loose
   engine/pump/clutch assemblies separately. Whole engine-to-chassis removal
   needs its own original-gate review; this whitelist does not enable it.

Results: pending. No original runtime gameplay comparison has been executed for
this packet; source-state evidence is distinguished from manual acceptance.

## Follow-up: complete engine into/out of chassis

Root session executed the initial small suite: 20/20 passed, including all 14
compound-assembly cases. Following that validation, the user-requested complete
engine installation receives a second scoped authoring step.

Additional original evidence:

- Engine block GO `30853`, Transform `66906`, `CheckCarAssembly 112949 @
  208412046`: State 1 disables its `_Triggers 13497`, reads
  Gearbox `31625.Installed` and Oilpan `23052.Installed`, and enters State 2 only
  when both are true. The three receiving `Trigger_motor` objects are children
  of the installed subframe presentation, so no installed subframe means no
  physical receiving sockets. The reviewed trigger Assembly `104917 @ 58715894`
  does not require a Bolted subframe latch.
- Block `BoltCheck 112951 @ 208422453`, `State 1`: checks
  Halfshaft_FR `34206.Bolted`, Halfshaft_FL `32554.Bolted`,
  ClutchLining `32817.Installed`, GearLinkage `6190.Bolted`,
  ExhaustPipe `33059.Bolted`. All false takes normal `Remove engine`; any true
  goes to the distinct force/break policy. `Remove engine` destroys only the
  block owner's HingeJoint, sets its tag to PART and reparents that owner to
  null. It does not detach or check engine-internal children.

Authoring plan: apply installation-only gearbox, oilpan and subframe occupancy;
retain the 20 explicitly enumerated current block-owned sockets; block normal
manual removal by the five external connection predicates above. Never derive
the retained list from all children at runtime. No change to engine threshold,
RPM/speed break policy, body/joint physics or force-detach semantics.

Known pre-existing limitation: canonical `mount.satsuma.exhaust-pipe` has no
fastener definitions/group members, so its Bolted latch cannot become true.
The correct Bolted dependency can be authored, but exhaust fastening remains a
separate necessary fix; installed presence is not substituted for this latch.

Timing-chain polarity correction is included following the sibling audit:
Assembly `108011 @ 118473478` reads Timingcover `22044.Installed`, with true ->
LOOP and false -> ASSEMBLE. Directly rechecked Removal `104132 @ 44094281`,
Requirements: Timingcover Installed OR chain `23.Bolted` blocks removal.
Remove only the exact two generic timing-chain/timing-cover dependency edges;
author cover occupancy as both chain installation and removal blocker.
