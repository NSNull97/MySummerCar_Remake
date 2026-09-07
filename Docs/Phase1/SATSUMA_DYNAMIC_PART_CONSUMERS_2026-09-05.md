# Dynamic assembly part consumers

Status: implementation and C# compilation complete; Unity test execution pending.
This is a bounded extension of the accepted Phase 1 assembly runtime, with
project-owned `Reimplemented` logic and no donor payload, save edits or generated
asset changes. The item bridge, DTO changes and additive mount/fastener migration
are owned by the coordinating implementation, not this consumer packet.

## Runtime changes

- `AssemblyChassisMassController`: aggregate `Graph.AllRuntimeParts`; bound the
  explicit installed-owner traversal by `Controller.AllRuntimeParts.Length`.
  Purchased installed children contribute to the chassis only when their owner
  chain reaches it. Loose engine mass remains separate.
- `AssemblyVehiclePrerequisiteAdapter`: existing installed-definition lookup
  scans `Graph.AllRuntimeParts`. Requirement authoring and startup policy remain
  owned by the startup implementation.
- `AssemblySubassemblyPickupTarget` and `AssemblyLooseOwnerMountOcclusionTarget`:
  runtime membership/traversal includes registered dynamic parts.
- `VehicleAssemblyQuery.GetMissingParts`: retain the fixed authored checklist;
  an installed matching definition fulfills the original row. Loose spare
  purchases are not missing-assembly rows. Graph owns the corresponding
  completeness calculation.
- `AssemblyLooseCompoundPhysics`: preserve serialized `Bindings`; add a cached
  combined binding array and explicit dynamic registration. Existing proxies,
  own centers, mount runtimes and fixed part roster are not rebuilt.

The block-only docking membership and front/rear suspension-specific scans were
audited and retained. The approved consumable definitions do not satisfy their
wheel/suspension predicates. Editor authoring and baseline validation retain
their fixed `Parts` semantics.

## Item bridge hooks

`AssemblyCompoundShapeBinding(PartInstance owner, Collider[] ownedShapes)` captures
the wrapper's own center of mass. Construct it after initial collider fitting,
before installation, never from an accumulated engine body.

- `TryRegisterRuntimeBinding(binding, out string error)`: after controller graph
  registration, validate the part and every explicit solid collider before
  extending physics. Reject duplicate parts/shapes and foreign bodies.
- `HasRuntimeBinding(part)`: explicit idempotent reconciliation check.
- `TryRefreshRuntimeBindingShapes(part, out string error)`: after presentation
  resize, copy the same source colliders' geometry, material and contact offset
  into existing proxies. Preserve graph state and the captured own center.
- `TryUnregisterRuntimeBinding(part, out string error)`: after assembly detach,
  before graph unregistration. Reject installed parts or owners with retained
  installed children. Disable/destroy only this binding's proxies and restore
  its own mass/center. A restore transaction keeps wrappers alive independently.

Source colliders must be explicitly owned by the same `PartInstance` and
`Rigidbody`. Trigger, nested foreign-body and unsupported mesh shapes reject.
No item-definition names or donor hierarchy lookups are introduced here.

## Verification

New `Assets/Game/Tests/EditMode/LegacyImport/AssemblyDynamicPartConsumerTests.cs`
and its `.meta` contain six focused regression tests:

1. An installed replacement fulfills the baseline checklist; another loose
   purchase does not reduce completeness or appear as missing.
2. A runtime child retains whole-engine pickup and loose compound contacts;
   engine installation/removal transfers total mass without recreating proxies.
3. Dynamic installed definitions satisfy the existing simulation lookup and
   removal invalidates its mutation-based cache.
4. Resizing an installed source updates its existing proxy without detaching.
5. Duplicate registration and installed unregistration reject without contact
   or mass mutation.
6. Repeated detach/unregister/register cycles leave no orphan contacts or drift.

Executed: targeted `rg` dependency audit; `git diff --check`; direct C# compiler
validation with the installed .NET SDK and Unity's existing response references.
Outputs were directed outside the project to a task-specific temporary folder.
`MSC.Audio.Runtime`, `MSC.Vehicle.Assembly`, `MSC.Vehicle.Runtime`, and the six
focused tests compile. Five existing `CS0414` warnings remain in untouched
`SatsumaRearSuspensionController` fields; no new consumer warnings were emitted.

No Unity process was started, and no NUnit test was executed in this packet.
The coordinating task must run the new fixture and the existing compound,
pickup, chassis ownership and end-to-end item/save transaction regressions with
its single Unity process. There are no manual authoring steps for these code
changes. Compatibility is additive; this packet adds no save migration and
preserves public serialized fields and base part IDs.

Next milestone: coordinated consumable bridge save/restore regression gate.
