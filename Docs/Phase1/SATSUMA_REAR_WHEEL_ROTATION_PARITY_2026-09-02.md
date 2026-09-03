# Satsuma rear wheel rotation parity

Revision `11A-V1d.43`, 2026-09-02. This is the bounded rear-wheel visual
rotation correction requested after rear suspension and road-wheel seating
were accepted. Save/load is deliberately outside this revision and is handled
as the next separate task. Manual gravity-driven rolling acceptance is
`USER PASS` as of 2026-09-02.

## Read-only original evidence

Authorities:

- frozen donor `GAME.unity`, SHA-256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- donor `Scripts/Assembly-CSharp/Wheel.cs`, SHA-256
  `85FFBF994222E04AC61A32BEBC9CDEAE9C7EFE53DF7D7BF75910EA80BC051945`;
- the accepted rear runtime hierarchy and V1d.41 regular-wheel seating audit.

The original registers all four wheels in the same axle/wheel update path.
Rear wheels are not engine-powered, but that does not make their visible models
static: road contact updates wheel angular velocity and `Wheel.cs` applies the
resulting axle angle to every configured `modelTransform`. The rear rotating
branch contains the tyre, drum, drum fastener and wheel pivot. A stationary
rear model while the chassis rolls is therefore not donor behavior. A donor
handbrake can intentionally stop the rear pair, so rolling comparison must be
performed with the handbrake released.

No donor file was modified and no donor runtime component is used.

## Root cause

NWH already calculated rear hub travel and roll. The current rear authority
correctly projected suspension travel into the trailing-arm presentation, but
never copied the relative rotation between NWH `NonRotating` and `Rotating`
containers into the installed rear carrier hierarchy. The installed arm, drum
and road wheel are kinematic presentation while NWH owns contact, so their
disabled solid colliders and absent second axle could not rotate them through
PhysX. Front presentation already consumed the NWH rotating hierarchy; rear
presentation stopped at suspension angle.

This is why the car could roll correctly while only its front visible wheels
turned.

## Correction

`SatsumaRearNwhCornerBinding` now serializes the arm-owned drum and road-wheel
mounts plus their neutral local transforms. Each application performs:

1. restore neutral carrier poses and project suspension travel;
2. synchronize installed parts once;
3. calculate the absolute world-space roll delta as
   `Rotating * inverse(NonRotating)`;
4. left-apply that delta to both carrier mount owners while preserving their
   world positions;
5. synchronize again so the drum, road wheel and both fastener presentations
   receive the same roll.

Neutral restoration before every application makes the mapping idempotent and
prevents per-frame accumulation. The second synchronization is presentation
propagation, not another suspension/contact solver.

Unchanged contracts:

- NWH remains the only rear contact and wheel-rotation authority;
- no rear `HingeJoint`, second tyre collider or duplicate axle is created;
- the accepted standard rear road-wheel seat remains `-0.040 m` on local X;
- the regular/offset pivot-family decision from V1d.41 is unchanged;
- mount IDs, part stable IDs, fastener stages, suspension coefficients and save
  DTOs are unchanged.

## Files

- `Assets/Game/Vehicle/NWH/Runtime/SatsumaRearNwhSuspensionController.cs`;
- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaBaselineBuilder.cs`;
- `Assets/Game/Tests/PlayMode/VehiclePhysics/SatsumaInstalledPartPhysicsPlayModeTests.cs`;
- regenerated private Satsuma baseline and manifest, builder `11A-V1d.43`.

## Automated validation

Executed with Unity `6000.3.11f1`:

- baseline builder completed and promoted manifest `11A-V1d.43`;
- focused rear carrier roll/seating test: **1/1 passed**;
- post-review idempotence extension: **1/1 passed**;
- full Satsuma installed-part physics rerun: **18/18 passed**;
- generated Satsuma EditMode fixture: **29/29 passed**.

One earlier full-fixture run was `17/18` because the existing rear spring
equilibrium test settled about 5 mm outside its tolerance. The same test passed
focused `1/1`, and the unchanged full fixture then passed `18/18`. It is an
order-sensitive PhysX observation, not a suppressed result.

Artifacts:

- `Logs/codex-v1d43-rear-wheel-rotation-results.xml`;
- `Logs/codex-v1d43-rear-wheel-idempotence-results.xml`;
- `Logs/codex-v1d43-satsuma-physics-rerun-results.xml`;
- `Logs/codex-v1d43-satsuma-generated-results.xml`.

## Manual acceptance result

`USER PASS`, 2026-09-02. The assembled car was made to roll under gravity and
the visible wheel behavior remained correct. A handbrake-release comparison
could not be performed because the remake does not yet implement the
handbrake; that missing feature is not classified as a wheel-rotation defect.

Retained regression recipe:

With both rear wheels and drums installed:

1. ensure no braking constraint is active; after the handbrake is implemented,
   release it explicitly;
2. let the car roll slowly forward and backward on level ground or a shallow
   slope;
3. confirm both rear tyres, drums and visible fasteners roll with the chassis;
4. confirm neither wheel orbits its hub and the accepted rear seating does not
   shift;
5. jack one rear side and spin/roll again to check droop does not break the
   carrier hierarchy.

Save/reload behavior is not part of this acceptance gate.
