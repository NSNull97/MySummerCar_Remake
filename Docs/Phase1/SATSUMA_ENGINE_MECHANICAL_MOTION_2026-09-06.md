# Satsuma mechanical motion — bounded Phase 1 presentation

Status: **Implemented, canonically authored and focused automated/graphics checks passed. Broader integration remains in progress.**
This does not close 11A-V1 or the Phase 1 gate. Classification: `Reimplemented`
presentation using reviewed `TemporaryDirectImport` geometry.

## Authority and compatibility

`SatsumaEngineMechanicalMotion` consumes the existing host RPM, operating point,
part installation, cam setting and purchased belt condition. It never advances
simulation, writes wear, inserts parts, moves physics roots/colliders, or stores
animation phase in saves. It runs after adjustment presenters and before the
accepted render-leaf vibration. The generic M06 host is not opted in.

Nine explicit shaft leaves cover normal/damaged crank, crank pulley, flywheel,
cam, cam gear, pump pulley, alternator pulley and electric radiator fan. Cam
speed is half crank speed only with the chain/cam/gear connected. Belt-driven
accessories use the actual usable belt and their own mechanical health; slipping
uses the operating model's belt-drive fraction. Their ratios use measured outer
mesh diameters (.120217/.119247 and .120217/.088049), not a new contact solver.
Radiator fan phase is independent of engine RPM, consuming the actual fan state.

Thirty-two explicit mesh bindings animate the four normal piston/rod assemblies,
their cap meshes and separate top variants, the separable damaged-piston-1 mesh,
eight rocker arms/tips, eight valve islands and eight existing setting screws.
The fixed shaft and five pedestals remain stationary. An Editor-only seam-welded
connected-island check authors vertex-to-motion metadata. Runtime uses an owned
transient Mesh clone and preserves the original MeshFilter, MeshRenderer,
materials, outline identity and shader attributes. Shared source meshes, imported
payload, collision, fastener hitboxes and part/mount transforms are untouched.

Cam and alternator phase composes with their existing adjustment poses; it does
not accumulate into a new base rotation. Pause freezes phase; restore/reset,
disable and detachment release appropriate transient mesh/rotation ownership.
Native save DTOs and stable IDs are unchanged by this motion packet.

The existing purchased-belt wrapper applies per-renderer texture travel using a
MaterialPropertyBlock and the reviewed two-bone rig. It does not alter a shared
material or replace the purchased item's wrapper/identity. The scale bone's Y
axis remains fixed; X/Z use a bounded deterministic tension-dependent envelope.

## Evidence and deliberate calibration limits

Frozen scene revision/hash and primary Nissan manual references are recorded in
`SATSUMA_LIVE_ENGINE_REFERENCE_AUDIT_2026-09-06.md`. The read-only geometry tool
adds `Logs/satsuma-engine-motion-geometry-20260906.json`, with per-leaf mesh IDs,
part-space transforms, topology islands, crank journal sections and mount poses.

- Frozen belt Rotation110291 integrates texture Y by `-RPM/60`; Jumping110290
  establishes the scale envelope. The new envelope replaces random timing with
  presentation phase. It is not a copied FSM and does not duplicate belt wear.
- `radiator_flect.anim`, SHA256
  `270c86ff0dbc7d4409c999ace556c2a9164abfa5bae726b1e6342a8f09f2d2b6`,
  contains a full +Y turn over .13333334 seconds. Continuous 450 RPM is a
  curve-based presentation normalization, not a claim of exact legacy clip
  playback semantics (the exported clip stop time differs).
- Installed piston 1/4 Z is .087012 m and piston 2/3 Z is .036995 m in block
  space. Their .050017 m separation and crank journal centres near +.025344 /
  -.024286 m establish a geometry-fitted .0250085 m radius. This is deliberately
  **not** substituted with real A10/A12 bore/stroke values.
- Rod length .13097 m and piston pin Z .005665 m are geometry-fit presentation
  values. Small source offsets/asymmetry remain; this is not a new precision
  engineering model. Piston pairs 1/4 and 2/3 use opposing crank phases.
- Four-stroke firing order 1-3-4-2 follows the primary family mechanical
  reference. Smooth 240-crank-degree valve lobes, 7 mm peak valve lift and
  10.5-degree rocker travel are explicit visual calibration, **not transferred
  donor cam profiles or physical valve-clearance measurements**.
- Damaged piston 2 has one connected mesh rather than the normal seven islands.
  Damaged variants 2–4 are not deformed by this packet; arbitrary cuts are not
  used to force them into a rig. Their existing presentation remains intact.
- Timing chain has no separable links/rig. Distributor, starter and water-pump
  housings contain fused geometry with no safely separate rotor/armature/impeller.
  These housings and the entire chain are not spun as a substitute. No new
  production geometry is authored in this Phase 1 pass.
- Rendering at high RPM can strobe/alias at frame rate. No motion blur or false
  capped shaft speed is added to conceal this limitation.

## Authored result and verification record

`Phase1SatsumaEngineMotionAuthoring.RefreshBatch` updated only the canonical
prefab, reporting `changed=1 rotating=9 reciprocating=32 fullRebuild=false` in
`Logs/live-engine-motion-author-topology-20260906.log`. Existing full-baseline
authoring receives the same idempotent extension; no full rebuild was executed.

Earlier author attempts and their failures are retained: a mismatched damaged
piston mesh identity, then the connected topology above. Neither failed attempt
saved the canonical prefab. Runtime compilation succeeded before authoring.

The initial focused EditMode run passed 4/5. The remaining assertion used an
EditMode preview object's `enabled=false` as if it invoked the PlayMode lifecycle.
The fixture now explicitly releases manually applied presentation at teardown;
a separate real player-loop test covers OnDisable. A following compile attempt
caught a test-only SetLoose signature mismatch; this is corrected, not counted
as a passed test run. Final executed results are recorded below when available.

- `Logs/live-engine-motion-edit-final-20260906.xml`: **21/21 passed**, no skips
  (mechanical 7, symptoms 6, actual operating-source integration 8).
- Warm mechanical loop: 200 frames in **144.521 ms**, **0 managed bytes** allocated
  on the test thread after warm-up. This is about .723 ms/frame in this Editor
  fixture, not a populated-world FPS claim.
- `Logs/live-engine-motion-play-20260906.xml`: **10/10 passed**, no skips;
  actual player-loop feedback plus mechanical pause/restore/disable/detach.
- `Logs/live-engine-motion-graphics-fixed-20260906.xml`: **1/1 passed** with
  D3D11. Eight HDRP renders in `Logs/engine-motion-graphics-20260906` cover crank
  0/90/180/270 and valve cycle 0/180/360/540. All eight were visually inspected:
  paired strokes, rod/journal alignment and independent rocker travel are visible.
  Neutral audit-only materials improve shape readability; this is not production
  material approval. The first capture already produced all eight frames but its
  teardown released an assigned camera target; that test error was fixed by
  clearing `Camera.targetTexture` before releasing the test RenderTexture.

Manual auditory/visual acceptance remains the user's decision. None of the
automated fixtures changes the user's save or original installation.
