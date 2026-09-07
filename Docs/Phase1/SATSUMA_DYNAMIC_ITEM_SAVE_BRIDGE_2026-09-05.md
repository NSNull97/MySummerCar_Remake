# Satsuma dynamic item / save bridge — 2026-09-05

## Scope and evidence

This implements the approved follow-up to the read-only slot-01 compatibility
audit, in the existing shared project. It does not replace the 126 authored
parts, rebuild the assembly graph on purchases, reset wiring, or authorize a
Phase 2 change. No Unity process, authoring builder, donor write, user-save
write, or Git commit was performed by this implementation task.

Inspected: AssemblyGraph/Controller/query and physical consumers, Items
materialization and packaging, the Save participant preparation/apply/rollback
order, vehicle/world streaming registration, existing filter adjustment and
installed-part interaction components, and the supplied reviewed fastener
descriptors. The earlier audit report remains in the delegated checkout:
`C:/Users/NSNull/.codex/worktrees/fd45/MySummerCar_Remake/Docs/Phase1/SATSUMA_SLOT01_SAVE_COMPATIBILITY_AUDIT_2026-09-05.md`.

The audited live file remains unchanged:

```text
C:/Users/NSNull/AppData/LocalLow/DefaultCompany/MySummerCar_Remake/Saves/Native/slot-01/current.save.json
SHA256 2990953C2A6882395D658353C2829416772ECC7CF7F8B3D506AED80EC64E16AC
```

The audit established document 17, vehicle envelope 1, inner assembly 2,
126 base parts, 117 mounts and 273 fasteners. It did not establish a cause for
the saved alternator connection. That connection and other existing wiring
state are preserved by this work.

## Runtime contract

`VehicleAssemblyController.Parts` and `AssemblyGraph.Parts` remain the authored
roster. `AllRuntimeParts` adds registered purchases without reconstructing mount
runtime objects or modifying existing fastener stages/latches. Registration
rejects duplicate identities, roots and definitions outside the explicit
aggregate allowlist. Ordinary removal from the registry requires a loose part.
`TrySetDynamicPartRegistrationsForRestore` is a transaction-only membership seam;
it preserves the wrappers needed by rollback.

The project-owned catalog maps exactly these approved item categories:

| Item | Part definition |
| --- | --- |
| `item.spark-plug` | `vehicle.satsuma.part.spark-plug` |
| `item.alternator-belt` | `vehicle.satsuma.part.alternator-belt` |
| `item.oil-filter` | existing `vehicle.satsuma.part.oilfilter0` |
| `item.light-bulb` | `vehicle.satsuma.part.light-bulb` |

The bridge adds assembly capabilities to the existing item wrapper. Stable ID,
Rigidbody and pickup identity are retained. Items owns item/package/condition
state; the vehicle owns pose, velocity, mount and compound physics for these
materialized parts. The generic world domain excludes those identities.
Contained package children remain contained until explicitly dispensed.

Existing installed removal, subassembly pickup, surface handoff and installed
trigger components are used. Runtime compound shape registration occurs after
the item visual/collider has been fitted. Aggregate-side
`IAssemblyItemPartPresentationBinding` callbacks receive the existing visual
child before proxy/compound reconciliation. The oil filter uses the existing
hand-twist implementation and retains its adjustment when its visual changes.

The query install path (including handoff and final installation) and controller
removal path call the engine author's `SatsumaConsumableAssemblyRules`. Belt
slack/prerequisite authoring and spark-plug presentation are owned by that
parallel authoring task, not reimplemented here.

Mass, loose-engine pickup, occlusion depth, installed-part synchronization and
vehicle save/recovery enumeration include dynamic parts. The existing authored
checklist denominator remains fixed; an installed replacement fulfills the
corresponding baseline row, and spare purchases do not lower completeness.
New mandatory socket readiness belongs to the startup prerequisite extension.

## Save compatibility

The vehicle envelope remains version 1. Inner assembly version 3 adds a separate
`dynamicParts` array with item-definition mapping, existing part DTO state,
linear/angular velocity and sleep state. Base part DTOs and stable IDs remain
unchanged. Descriptor preflight works before wrappers are materialized; apply
requires the exact live registration, including the actual PartDefinition.

Document 17 -> 18 is a forward-format gate. It does not invent ownership or
mount state. Inner versions 1/2 remain intact until the assembly controller
performs its existing latch-aware content migrations.

Cross-domain preflight uses private DTO copies. A legacy materialized purchase
is transferred from item + world records into a loose dynamic vehicle record
using the same ID and the authoritative saved world pose/velocity. Only that
world record is removed. Current-format duplicate physical owners, missing
descriptors, incompatible definitions, canonical-placement collisions,
consumed descriptors and ambiguous aggregate ownership fail before apply.
There is no guessed pose or inferred installation.

All participants prepare before mutation. The optional checkpoint boundary then
cancels transient install handoffs before any rollback snapshot is captured.
This prevents a kinematic, collision-disabled animation state from being
restored without its coroutine. Invalid preparation does not cancel a handoff.

The item transaction retains old wrapper references until commit. Before
ordinary reverse participant rollback it restores wrappers, old membership,
compound bindings and assembly state; the item journal finally restores object
topology/body state. Destructive retirement occurs only after every apply
succeeds. Fresh ownership is established before public materialization events;
world registration and deferred transfer are guarded as well.

## Reviewed content migrations

The additive path validates stable-ID sets and target contracts, not just counts.
It composes with existing rocker-cover/carburetor retirement and lower-strut /
exterior-panel migrations.

The reviewed 21 additions are exhaust pipe 3, muffler 1, fuel tank 7, driver seat
4, passenger seat 4 and rear seat 2. Their IDs are
`fastener.satsuma.<slug>.boltpm-N`; wrench sizes are 7/7/11/9/9/9 mm, maximum stage
8, clockwise, inserted on installation and required for removal. Group ON
thresholds are 6/2/12/7/7/6; OFF is 0. New stages and these six latches start at
zero/false. Inserted/seated follows saved occupancy. Existing stages, latches,
poses, loose seats, filter adjustment and other extensions are preserved.

Seven new mounts start empty: four cylinder-head spark-plug sockets, the engine
block belt socket and left/right headlight bulb sockets. The four thread IDs
are `fastener.satsuma.cylinder-head-spark-plug-N.thread`, maximum 8, typed
`SparkPlugWrench` / `None`; belt and bulb mounts have no fasteners. Their new
thread stages are zero and inserted/seated false. Final reviewed shape is
126 base parts, 124 mounts/groups and 298 fasteners; purchases are separate.

Partial additions, wrong missing IDs, invalid old empty-group latches and
unreviewed target fastener tools/IDs are rejected. Source DTOs are not modified.

## Changed source and tests

All paths below are relative to this shared project. New C# files have `.meta`.
Pre-existing dirty changes in these files were preserved.

- Assembly: `AssemblyGraph.cs`, `VehicleAssemblyController.cs`, new
  `VehicleAssemblyController.DynamicParts.cs` and
  `VehicleAssemblyController.ContentMigration.cs`, `VehicleAssemblySaveData.cs`,
  `SatsumaRockerCoverFastenerMigration.cs`, `SatsumaCarburetorFastenerMigration.cs`.
- Consumers: `VehicleAssemblyQuery.cs`, `AssemblySubassemblyPickupTarget.cs`,
  `AssemblyLooseOwnerMountOcclusionTarget.cs`, `AssemblyLooseCompoundPhysics.cs`,
  `AssemblyChassisMassController.cs`; only the installed-part enumeration seam
  in `AssemblyVehiclePrerequisiteAdapter.cs`.
- Items: `ItemWorldRuntime.cs`, new `ItemWorldRuntime.Ownership.cs`.
- ItemsIntegration: new `VehicleItemPartCatalog.cs`,
  `VehicleItemAssemblyBridge.cs`, `ItemPartPresentationBinding.cs`; module refs.
- Save runtime: `SaveParticipants.cs`, `SaveDocument.cs`.
- Save integration: new `VehicleItemSaveRestorePlanFactory.cs`,
  `ItemSaveParticipant.cs`, `VehicleSaveParticipant.cs`,
  `WorldEntitySaveParticipant.cs`, `NativeSaveSessionController.cs`; module refs.
- Save integration migration: new `SatsumaDynamicAssemblySaveMigration.cs`.
- Tests: new `SatsumaDynamicAssemblySaveTests.cs`,
  `AssemblyDynamicPartConsumerTests.cs`,
  `ItemRuntimeAndSaveTests.Ownership.cs`, `VehicleItemOwnershipSaveTests.cs`,
  `SaveRestoreTransactionTests.cs`, `AssemblyDynamicRestoreHandoffPlayModeTests.cs`;
  extended `SatsumaEngineSaveExtensionTests.cs`, item partial declaration / refs,
  and current-document expectations in `SatsumaKeyAccessSaveTests.cs` and
  `NativeSaveBootstrapLoadPlayModeTests.cs`.

Engine-author definitions, mount presentation, startup prerequisites and Editor
builder changes are parallel integration dependencies with separate ownership.

## Lamp condition and skinned-bounds follow-up

The later approved lighting integration uses `IAssemblyItemCondition` on the
existing `ItemPartPresentationBinding`, attached to every mapped materialized
item before presentation observers run. It reads the same `WorldItemInstance`
through new readonly `ConditionPercent` / `IsBroken` scalar accessors. The
ordinary `State` API returns a deep copy and is deliberately not called every
lighting frame. State replacement, rollback and item saves remain authoritative;
there is no duplicate condition field or condition migration. Missing wrappers
report broken / zero condition.

`ItemPartInteractionBounds` measures SkinnedMeshRenderer geometry with one
temporary `BakeMesh(false)` during presentation/proxy reconciliation and frees
the temporary mesh. Non-skinned renderer bounds keep their existing calculation.
Invalid/empty skinned geometry requests the explicit item proxy-size fallback;
the imported skinning/culling AABB is never the fallback. The bridge retains
its existing 0.035 m interaction padding.

The current catalog has no authored collider shapes for any of the four mapped
types. Generic item fitting therefore also consumed the inflated imported AABB
before the bridge callback. For skinned visuals only, the bridge reuses the same
baked bounds to correct the non-authored default solid BoxCollider before
compound shape registration, retaining the existing 0.02 m minimum thickness.
Authored physical shapes and ordinary MeshRenderer fitting remain untouched.

Additional files: `WorldItemInstance.cs`, new `ItemPartInteractionBounds.cs`,
`ItemPartInteractionBoundsTests.cs`, `ItemRuntimeAndSaveTests.SkinnedBounds.cs`
and their `.meta` files. Runtime condition
coverage includes live state replacement on non-filter items, retained identity
and absence of filter-adjustment components on bulbs/belts/plugs. The five
bounds-helper cases cover scale, current bone pose, a deliberately inflated
12 m AABB, temporary-mesh cleanup, ordinary/mixed and empty geometry. Actual
Unity BakeMesh scale behavior remains a required runner assertion, not a result
inferred from successful C# compilation.
Two additional bridge cases exercise real materialization/events, correction of
both default solid and query, identity/state retention, and preservation of an
explicitly authored box+sphere compound. All current Items EditMode sources and
the final ItemsIntegration source compiled successfully after these changes.

## Validation and next gate

Performed: focused source review, independent review of migration/rollback and
interaction seams, `git diff --check`, read-only live-save hash verification,
and private Roslyn C# compilation using installed Unity reference assemblies.
Compiler output is outside the shared project's Library:

```text
C:/Users/NSNull/AppData/Local/Temp/msc-dynamic-item-validation/Compile-ItemsAndSave.ps1
C:/Users/NSNull/AppData/Local/Temp/msc-dynamic-consumer-validation/Compile-Consumers.ps1
```

The final compilation passed for nine runtime modules: Audio, Assembly,
Vehicle Simulation, Vehicle runtime, Items, ItemsIntegration, Save
runtime/migration/integration. It also
passed for the three focused Items/Save test assemblies, the three assembly /
consumer EditMode sources and the new handoff PlayMode source. Items ownership
contains 18 compiled NUnit cases, plus five bounds-helper and two skinned bridge
cases. The final affected ItemsIntegration and all Items tests were recompiled
after the bounds/solid correction. Five existing RearSuspension CS0414 warnings
remain; no new compiler warnings/errors were reported. This is compilation
evidence, not an executed NUnit or gameplay result. Source scope is frozen and
the main task received the final result.

Required runner suites: `SatsumaDynamicAssemblySaveTests`,
`SatsumaEngineSaveExtensionTests`, `AssemblyDynamicPartConsumerTests`,
`ItemRuntimeAndSaveTests` (including Ownership partial),
`VehicleItemOwnershipSaveTests` (18 cases), `SaveRestoreTransactionTests`
(5 cases), `AssemblyDynamicRestoreHandoffPlayModeTests` (2 coroutine cases),
and existing key/native bootstrap compatibility suites.

The main task is the sole Unity/authoring runner. It must regenerate the
approved catalog/sockets/presentation through its authoring entry point, compile
in Unity, execute these EditMode/PlayMode suites and validate a **copy** of the
audited slot through native preflight/restore/recapture. The real slot must remain
untouched. Verify loose/installed purchases, same-wrapper rollback after a later
participant failure, package child identity, source-cell unload/reload and
subassembly pickup. No manual user Editor action is claimed complete here.

A previously unseen unloaded vehicle with no known catalog fails explicitly;
known owner catalogs are retained across unload. Initial vehicle registration
binds the item bridge and transfers existing world ownership. Arbitrary late
external bridge attachment must perform the same transfer; it is not a supported
replacement for session composition. Gameplay validation and authoring fidelity
remain unverified until the main task executes the gate.

Compatibility is additive to completed milestones 00–08A. No scene/stable-ID,
UI, weather, donor-runtime or production-art foundation is replaced. These are
project-owned reimplementations and reviewed configuration migrations; no donor
code or payload was copied. Exactly one next milestone: the integrated private
Phase 1 Unity save/consumable compatibility gate described above.

## Integrated runner results — 6 September 2026

This section supersedes the compilation-only status above; the earlier entries
remain the implementation history. Root executed broad EditMode **1186/1186**
with zero failures/skips (`Logs/codex-night-editmode-final-20260906.xml`). XML
discovery explicitly includes the canonical native consumable tests,26 exact
headlight migration cases and dynamic graph validation. Headless PlayMode
**100/100** passed (`codex-night-playmode-retry-20260906.xml`), including real
purchased bulb pickup/raycast/handoff coroutine, condition/light gating and
removal. Real native Bootstrap load/migration tests passed **2/2**
(`codex-night-native-bootstrap-20260906.xml`), using only GUID-named temporary
test slots. Actual four-purchased-plug installation and normal host-driven
ignition/idle/rev/key-off passed in `codex-night-real-plugs-wiring-20260906.xml`;
the separately listed wiring fixture failures in that file do not negate or
replace its engine result.

Two additional **actual old-cube** tests passed in
`Logs/codex-night-wiring-contact-bulb-proxy-20260906.xml`: absent-provider cube ->
serialized ItemRuntimeSaveRecord -> destroyed old runtime -> fresh canonical
bridge/provider with exactly one real bulb and preserved identity/condition;
late-provider callback -> replacement of the same cube wrapper without state or
model duplication. Both accept the resulting item into the canonical headlight
socket. These cover the Items materialization/presentation boundary, not a second
full native-document coordinator or a manual shop-to-car walkthrough. The
separate headless bulb test covers physical handoff rather than a direct
installation call.

The source slot-01 remains byte-identical:
`2990953C2A6882395D658353C2829416772ECC7CF7F8B3D506AED80EC64E16AC`.
The night integration report owns subsequent final combined results and manual
acceptance status; no raw donor payload or user native file was committed.
