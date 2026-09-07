# Unity 6000.6 inactive Rigidbody pose compatibility

Date: 2026-09-06

Status: bounded compatibility source change; targeted Unity regression run
passed 45/45 with no failures or skips, including all 11 new `PhysicsPose_`
cases. Broader EditMode validation is in progress; its result is not claimed
here. No new feature, save schema or scene change.

## Executed evidence and cause

The coordinating agent executed the isolated EditMode probe recorded in
`Logs/unity660-inactive-pose-probe-20260906.xml` (one selected case, passed).
Its diagnostic output on Unity 6000.6.0f1 shows:

- Before disabling the parent, native Rigidbody and Transform positions agree
  at `(2.25, 3.5, -4.75)` and rotations agree within floating-point precision.
- Once the parent is inactive, Rigidbody position reads `(0, 0, 0)` and rotation
  reads identity, while the Transform retains its non-origin, non-identity pose.
- Writing Transform and Rigidbody pose, then changing the kinematic flag, does
  not make the inactive native getters reflect the Transform.
- Reactivating the isolated probe recovers the written pose immediately. No
  additional reset is required.

This establishes a pose-read compatibility problem, not proof that inactive
pose writes or Transform state are lost. The probe contains strict active and
reactivated position/rotation assertions; the runner's XML assertion counter
does not enumerate those UnityTest assertions. It is not itself coverage of
the save participants. The new participant tests below assert that boundary.

The affected save/guard paths previously accepted any non-null Rigidbody as
the pose authority. They include inactive wrappers deliberately. A default
native pose could therefore be saved, or cached during a guard and written
back to an otherwise correct Transform. Zero and identity are valid DTO values,
so existing finite-value validation cannot detect this corruption.

## Dependency audit and bounded implementation

| Existing path | Inactive reachability and correction |
| --- | --- |
| `WorldEntitySaveParticipant.RegisterScene` | `GetComponentsInChildren<PhysicsPickupTarget>(true)` includes disabled objects. `RigidbodyRestorePlan.Capture` runs before registration completes and later reapplies the cached pose. Both that capture and normal `CaptureTarget` now choose Transform only for inactive hierarchies. |
| World source-cell capture on unload | Cell classification now uses the same activity-aware pose; otherwise an inactive retained object could be classified at the origin before suspension. |
| `VehicleSaveParticipant` startup/streaming guards | Registration includes inactive bindings, and `CaptureBindingBodies` includes loose assembly parts below inactive parents. `SuspendedVehicleBody` now caches the activity-aware pose before freeze/restore. Both body-cell lookups use that same pose policy. Chassis recovery reads its desired orientation using the policy too. |
| `VehiclePersistenceBindingCore.TryCapture` | Validation guarantees the chassis reference, not activity. Only chassis position/rotation select inactive Transform fallback. Active-body getters remain authoritative. |
| `ItemSaveParticipant.CaptureInstance` | Loaded instances can include consumed or parent-disabled wrappers. Materialization position/rotation use Transform when the body is absent or its hierarchy is inactive. |

Runtime files changed in the original bounded implementation assignment:

- `Assets/Game/Save/Integration/WorldEntitySaveParticipant.cs`
- `Assets/Game/Save/Integration/VehicleSaveParticipant.cs`
- `Assets/Game/Save/Integration/ItemSaveParticipant.cs`
- `Assets/Game/Vehicle/Runtime/VehiclePersistence.cs`

The activity check is `activeInHierarchy`, not `activeSelf`: an enabled child
under a disabled parent has the same observed native-getter problem.

For active bodies, position/rotation still come from Rigidbody, preserving the
existing physical rather than interpolated presentation authority. The patch
does not activate objects, change save-domain ordering, change DTO fields or
versions, migrate IDs, replace public APIs, alter velocities, or change guard
freeze/restore ordering. Existing unrelated worktree changes are preserved.
No assembly dependency or new runtime abstraction is introduced. No save-data
migration is needed; already-corrupted saved coordinates cannot be reconstructed
from this read fix alone.

## New regression coverage

New partial fixtures reuse the existing setup/cleanup and synthetic vehicle/item
factories. Their common targeted filter is `PhysicsPose_` (11 test cases).

In `MSC.Save.Integration.Tests.EditMode.CurrentDomainSaveIntegrationTests`:

- `PhysicsPose_WorldRegistrationAndCapturePreserveActiveAndInactiveBodies`
  (3 cases: active, self-disabled, parent-disabled). Verifies registration does
  not corrupt Transform, payload retains pose/activeSelf, and no activation occurs.
- `PhysicsPose_VehicleBindingCapturesActiveAndInactiveChassis` (same 3 cases).
  Verifies the existing complete vehicle binding can capture its chassis pose
  without altering hierarchy activity or Transform.
- `PhysicsPose_VehicleStartupGuardPreservesChassisAndLooseParts` (2 cases:
  active/inactive). Verifies guard, payload capture and guard release preserve
  both chassis and loose-part position/orientation and hierarchy activity.

In `MSC.Items.Tests.EditMode.ItemRuntimeAndSaveTests`:

- `PhysicsPose_ItemCapturePreservesActiveAndInactiveMaterializationPose`
  (3 cases: active, self-disabled, parent-disabled). Verifies materialization
  pose, unchanged logical state and retention of the registered wrapper.

Tests compare position plus two independent rotated basis vectors with a
`0.0001` tolerance. They do not depend on asserting the engine's default inactive
native-getter values, and therefore test the intended contract rather than lock
in the engine defect. Active controls do not simulate interpolation over frames;
they check the native-authority capture path at the assigned pose.

## Executed targeted validation

The coordinating agent executed the targeted Unity EditMode run recorded in
`Logs/unity660-pose-fixed-20260906.xml`, from `2026-09-06 08:03:18Z` to
`2026-09-06 08:03:25Z`. The XML reports **Passed: 45 total, 45 passed, 0 failed,
0 skipped, 0 inconclusive**. Every selected test-case node is Passed.

| Selected coverage | Passed cases |
| --- | ---: |
| New `PhysicsPose_` world, vehicle binding/startup guard and item capture cases | 11 |
| ItemTopology inactive-wrapper rollback cases described below | 2 |
| `CanonicalConsumableNativeSaveTests`, including the isolated pose probe and native purchased-part save flows | 6 |
| `WorldLightingProbeCatalogTests` | 13 |
| `ProductionEnvironmentContentTests` | 13 |
| Total | 45 |

Implementation-agent checks also passed: source inspection, diagnostic/result
XML review, helper/name conflict scan, and scoped `git diff --check`. This agent
did not launch Unity. C# remains frozen while the coordinating agent runs the
broader EditMode gate.

## Coordinated ItemTopology rollback correction

The separate read-only finding in
`Assets/Game/Items/Runtime/ItemWorldRuntime.Ownership.cs` was subsequently fixed
by the coordinating agent. `ItemDynamicRestoreTransaction.ItemTopology` now
captures native Rigidbody position/rotation only when its hierarchy is active;
otherwise it captures the item Transform. This applies the same policy before
the rollback journal can cache and later reapply a default native pose. The
existing parent, scene, activeSelf, scale, velocity, interpolation and physics
restore ordering are unchanged by that pose-selection correction.

Both cases of
`MSC.Items.Tests.EditMode.ItemRuntimeAndSaveTests.ItemRestoreTransactionRollbackPreservesInactiveWrapperTransformPose`
passed in the same 45-case run: `(False)` disables the wrapper itself and
`(True)` disables its parent. Each starts an item restore transaction at a
non-origin/non-identity pose, changes the Transform, rolls back, and verifies
the same registered wrapper, original parent, unchanged inactivity/activeSelf,
position within `0.0001`, and rotation angle within `0.001` degrees. This is
explicitly a fifth coordinated runtime-file correction, not an unreported
expansion of the original four-file assignment.

## Retained limitations and separate risks

- The 11 pose cases and 2 rollback cases passed, but the broader EditMode gate
  remains in progress at this report update. No full-game, standalone-build or
  all-platform compatibility claim follows from the targeted run.
- Active controls do not create a frame-interpolated `Rigidbody != Transform`
  pose. Active native getter branches are preserved by source inspection;
  those tests cover their assigned-pose behavior, not interpolation timing.
- This patch does not change or establish inactive velocity, sleep/wake, or
  all collision semantics. The measured incompatibility and new assertions
  concern pose capture/preservation.
- Source-cell routing and recovery orientation use the corrected pose policy,
  but these 11 new cases are not dedicated unloaded-cell routing or recovery
  formation tests.
- Previously saved origin/identity corruption cannot be reconstructed from
  the read fix alone; no recovery migration is claimed.
- Recovery bounds formation and story traffic/ragdoll presentation snapshots
  still contain separately identified native pose reads outside these fixes.
  Ordinary NPC/traffic teardown captures before disabling and unregistering
  the presentation, unlike the directly reproduced inactive save paths. The
  targeted run does not prove those separate paths universally safe.

Existing assembly part save capture already uses part Transforms. Carry capture
rejects inactive held wrappers. Native save scene-unload ordering captures item,
world and vehicle domains before normal streaming deactivation; it does not,
however, exclude objects that were already inactive.

Next gate: finish the coordinating agent's broader EditMode regression run,
including the established save/vehicle/items coverage, and review its result.
