# Bounded engine docking extension

Coordinated final validation: **799/799 EditMode, 74/74 PlayMode passed**;
native slot-01 was restored on a copy without rewriting user saves. Authoring
repeat changed0. Manual acceptance remains pending. The authorship-stage notes
below are supplemented by [the final packet report](SATSUMA_ENGINE_REPAIR_PACKET_2026-09-05.md).

Scope: user's 2026-09-05 engine installation correction. Frozen donor GAME hash
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.

Evidence: Trigger_motor Assembly104917 (58715894) checks the paired block trigger
distance against 0.1 m and activates its bolt, without installing the block.
The other two trigger pairs and measured poses are recorded in the existing
Phase1SatsumaV1dEngineMountTriangulationAudit.csv. Block BoltCheck112951
(208422453) uses ON2/OFF0, snaps onto Pivot at Bolts ON, then establishes its joint.
CheckCarAssembly112949 requires installed gearbox and oil pan. Receiving triggers
belong to the installed subframe. Removal retains engine internal assembly;
halfshafts, clutch lining, gear linkage and exhaust are separate external checks.

Compatibility plan, recorded before implementation:

- Preserve all existing part/mount/fastener IDs and binary assembly graph states.
- Add an opt-in engine docking adapter. Before ON2 the block stays loose and
  physical; three pending bolt states are owned by an optional, independently
  versioned block DTO, NOT an illegally occupied empty mount.
- Reuse existing wrench capabilities/presentation and normal assembly operations.
  A separate explicit marker excludes only this mount from click handoff routing.
- At ON2, use the existing install transaction and transfer pending stages. At
  OFF0, use a new pose-preserving removal entry point; existing removal entry
  points keep their accepted offset behavior. Existing external connection
  blockers remain in force.
- No changes to chassis foundation, old save identity or accepted suspension.
  Installed engine currently uses the accepted chassis-bound physics, not a
  claim of reproducing the donor's HingeJoint break-force behavior.
- Add focused docking, no-click, partial-stage persistence and in-place release
  tests before reporting success. Full hoist operation is not introduced here.

User-approved deviation: aggregate engine mass and a 120 kg hand-carry threshold.
Inspected donor PickUp113435 has no mass rejection; it gates engine pickup on
Block/Data.InHoist. The debug toggle bypasses only the carry limit, not physics.
