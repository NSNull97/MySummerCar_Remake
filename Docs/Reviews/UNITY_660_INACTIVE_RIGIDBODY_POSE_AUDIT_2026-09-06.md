# Unity 6000.6.0f1: inactive Rigidbody pose and consumable save coverage

Classification: engine-lifecycle compatibility and regression-test maintenance.
Status: lifecycle cause reproduced; the revised consumable fixture, targeted
pose/save regressions and strengthened active purchased plug/bulb Play Mode
checks passed.

## Reproduced cause

`Logs/unity660-editmode1-20260906.xml` reports two failures in
`CanonicalConsumableNativeSaveTests.PurchasedGeneratedUnitRoundTripsThroughNativeDocumentIntoFreshCanonicalCar`.
Both fail during the original purchase's ownership check **before save capture**.
The Transform matches its mount; reading `Rigidbody.position` does not. The
spark-plug delta is `13.8938942`, and the light-bulb delta is `12.2631569`.
Independent evaluation of the canonical prefab's Transform chains gives those
same distances from world zero. These were zero-valued native reads, not an
arbitrary loose-part offset or evidence of a save-deserialization failure.

The canonical test fixture deliberately keeps the car beneath an inactive
holder. An A/B run using normal Play Mode scenes retained the same full flow,
inactive holder and assertions. All four cases produced the same deltas:
`Logs/unity660-pose-ab3-20260906.xml`. Therefore this is not preview-scene-only.
Earlier normal-scene setup attempts were rejected by Editor scene restrictions
and were not treated as physics evidence; no user's unsaved scene was discarded.

The independent test
`InactiveRigidbodyPoseWritesRecoverExactPoseWhenProbeIsReactivated` passed
**1/1** in `Logs/unity660-inactive-pose-probe-20260906.xml`, recorded from
2026-09-06 07:49:39 to 07:50:19 UTC. It uses only a standalone GameObject,
Rigidbody, BoxCollider and parent in its own scene, not the canonical car.

| Stage | Transform position | Native body position | Native rotation |
| --- | --- | --- | --- |
| Active before parenting | `(2.25, 3.5, -4.75)` | Same as Transform | Matches Transform |
| Reparented below inactive parent | Original position | `(0, 0, 0)` | Identity |
| Transform and sequential body writes | `(7.25, -3.5, 2)` | `(0, 0, 0)` | Identity |
| Kinematic transition, then repeated writes | Updated position | `(0, 0, 0)` | Identity |
| Reactivate only the standalone probe | Updated position | Same as Transform immediately | Matches Transform |

Strict position (`<0.0001` m) and rotation (`<0.001` degrees) assertions pass
both before disabling and after reactivation. No canonical vehicle was
activated merely to make the original inactive test pass.

This observed lifecycle behavior is consistent with Unity's fixes for disabled
Rigidbody actor lifetime (UUM-143658) and prefab-context API safety
(UUM-137716/UUM-135381). The issue-to-observation connection is an inference;
the local executed probe establishes the behavior used by this project's tests.
See the [official 6000.6.0f1 release notes](https://unity.com/releases/editor/whats-new/6000.6.0f1).

## Bounded correction

`CanonicalConsumableNativeSaveTests.cs` retains both original inactive Edit Mode
cases, both additional inactive full-roundtrip Play Mode cases and the lifecycle
regression. Logical state, stable identity, single ownership, generated visual,
fastener, source-cell, fresh materialization and source-document immutability
checks remain. Transform position and rotation remain strictly checked while
inactive. Native body pose is checked only when `activeInHierarchy` permits a
native actor. New assertions require the serialized dynamic part's world pose
to preserve the pre-capture installed pose, including after fresh restore and
recapture; zero-valued save poisoning is not accepted as fixture behavior.

Native pose coverage is also strengthened in the existing **active** canonical
fixtures, without conditional skips:

- `CanonicalPurchasedBulbHandoffPlayModeTests` already activates the real car
  and completes the real carry/handoff transition. It now requires the purchased
  body's active state and exact position/rotation against its authored socket
  after completion and the existing rendered-frame boundary.
- `CanonicalPurchasedPlugIgnitionPlayModeTests` already activates the real car.
  Each real purchased-plug handoff now checks active native pose against its
  mount; the check repeats after the real ignition/engine host-frame flow.

These tests do not introduce extra physics simulation into their stationary
fixtures. The bulb fixture intentionally pauses scaled physics while its real
handoff and presentation advance through rendered frames. The plug fixture
later executes its existing host FixedUpdate flow. This is not a road-physics
calibration or an assertion of deterministic PhysX behavior.

## Compatibility boundary and verification

No `PartInstance`, dynamic-joint behavior, vehicle activation policy, serialized
runtime field, stable ID, DTO schema, donor asset or user save file is changed by
this test correction. `SynchronizeInstalledPose` already writes the body pose
after installed kinematic configuration; reordering it would not restore an
absent actor. Reflection and the installed PhysicsModule XML also confirm that
this editor exposes `Rigidbody.Move`, but not an atomic
`Rigidbody.SetPositionAndRotation`; `Move` was not substituted for teleporting.

Production save code that reads an inactive body's zero/identity values remains
a real compatibility risk, not a reason to waive persistence assertions. Its
separate migration fix and tests are owned by the production-save audit. The
central targeted rerun, `Logs/unity660-pose-fixed-20260906.xml`, reports
**45/45 passed, 0 failed, 0 skipped**, from **2026-09-06 08:03:18 to 08:03:25 UTC**.
Within it, the complete `CanonicalConsumableNativeSaveTests` fixture reports
**6/6 passed**:

- the two original inactive preview-scene spark-plug/light-bulb roundtrips;
- the two full-roundtrip Play Mode cases retaining the inactive canonical holder;
- the standalone inactive-body/reactivation lifecycle regression;
- the existing native-v18 two-bulb fastener migration case.

This verifies the revised fixture's strict logical, Transform, serialized-pose
and fresh-restore assertions. The complete Edit Mode suite is outside this
targeted result.

The later central Play Mode result,
`Logs/unity660-playmode-final-r3-20260906.xml`, independently records both
strengthened active fixtures **Passed** on **2026-09-06**:

- `CanonicalPurchasedBulbHandoffPlayModeTests.CarriedRealBulbUsesCanonicalRayHandoffAndLiveHeadlightCondition`
  at **08:31:29 UTC**;
- `CanonicalPurchasedPlugIgnitionPlayModeTests.RealPurchasedPlugsAndIgnitionHoldSustainIdleRevAndKeyOffThroughHostFrames`
  from **08:31:29 to 08:31:32 UTC**.

These executed passes include the mandatory active-body checks and strict
native position/rotation assertions added above. The complete r3 run reports
169 passed, 2 failed and 2 skipped out of 173; the individual passes do not
represent a claim that the whole Play Mode run was green.

No new Unity process was launched by the test-audit worker; the migration owner
ran the cited diagnostics. Scoped source/backup diffs and whitespace checks were
also inspected. Active purchased **native fresh reload** remains distinct from
the active handoff coverage; the generic Bootstrap reload test alone does not
prove purchased-body pose restoration.
