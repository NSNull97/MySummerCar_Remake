# Original viewmodel hand transfer audit

Date: 2026-08-10  
Superseding corrections: 2026-08-11 and 2026-08-12  
Donor authority: locked `GAME.unity` and animation hashes from
`Phase1PlayerViewmodelManifest.json`.

## Donor evidence retained

- Each donor action owns a separately pre-posed 21-bone `hand_rigged`
  forearm; it has no visible upper arm or shoulder.
- The donor four drink clips last `5.866667`, `1.95`, `4.0` and
  `0.733333` seconds. The user-approved full-drink presentation now lasts
  `11.0` seconds; this is a deliberate project-authored presentation timing,
  not a donor timing transfer.
- Wave lasts `1.333333` seconds; middle finger lasts `1.4` seconds.
- The original grip and sampled bone tracks remain recorded in
  `Logs/OriginalViewmodelMotionAudit.txt` as read-only evidence.

## Rejected transfer

The first licensed-rig pass copied donor world-space elbow, palm, digit and
grip trajectories onto an anatomically different full-arm skeleton. Executed
in-game screenshots showed a detached bottle, finger intersections, an
undersized wave and a deformed middle-finger pose. The user explicitly rejected
those positions on 2026-08-11.

The donor positions, rotations, digit directions, grip transform and the
`0.87382` limb-normalization workaround are therefore superseded and must not
become generated-animation authority again. They remain evidence only.

## Current AXIS licensed-hand contract

The rejected Realistic FPS Hands mesh/rig has been superseded by the
user-purchased `Neutral_Arms_Real-Time.blend` AXIS source supplied locally on
2026-08-11. The deterministic export opens that file with Blender script
auto-execution disabled, samples only project-authored controls and writes a
sanitized Generic FBX plus the three packed surface maps into the ignored
private RuntimeBaseline. The source `.blend`, embedded UI/updater scripts and
control rig never become runtime dependencies.

The generated clips now:

1. preserve the short/spray/throw evidence durations while the user-approved
   full drink uses an explicit `11.0 s` project-authored duration;
2. use project-authored camera-local shoulder, palm and forearm-direction
   profiles;
3. derive the elbow from the exact licensed forearm/twist lengths and preserve
   the exact licensed upper-arm length;
4. orient the palm from explicit forward and normal axes;
5. distribute wrist roll across forearm and twist bones instead of concentrating
   it at the palm seam;
6. apply separate anatomical open-hand, cylindrical bottle grip and
   palm-facing middle-finger poses, including per-joint finger curls and
   independent thumb opposition;
7. keep the bottle anchor local to the licensed right palm;
8. generate a hand-and-forearm-only right viewmodel mesh instead of hiding the
   left skeleton by a zero scale or rendering the upper-arm/shoulder lobe;
9. bake interruptible quaternion-continuous keys at 60 Hz;
10. force the camera-local viewmodel renderer to emit no object motion vectors,
    avoiding HDRP gesture smearing;
11. render automated review frames at the gameplay horizontal FOV of 120
    degrees (88.507 degrees vertical at 16:9);
12. drive the clips through direct `AnimationClip.SampleAnimation` in runtime,
    because Unity 6 legacy `Animation.Play` mis-evaluates this Generic FBX and
    stretches the otherwise valid arm hierarchy through the camera.

Ready drinking is the user-approved straight forearm and vertical bottle pose.
The full cycle waits `0.08 s`, reaches the mouth at `0.73 s`, drinks in place,
then adds four small cumulative bottle-bottom raises starting at `2.20`, `4.20`,
`6.20` and `8.20 s`. Return begins at `9.70 s`, reaches the exact ready pose at
`10.60 s` and holds through `11.0 s`. The bottle anchor remains local to the
right palm, maps the donor bottle neck's local `-Z` toward the mouth, and the
real carried Rigidbody follows it; animation never clones, reparents or changes
the authoritative item. Wave uses the right arm. Middle finger and thumbs-up
use the left arm; smoking, wave and drinking use the right arm as explicitly
locked by the user's photo/video references.

Donor gameplay code, FSMs, controllers, animation events and runtime assemblies
are not used.

## Verification contract

`LicensedRealisticFpsHandsAuthoringTests` retains its historical fixture name
for CI filter compatibility and now locks:

- the approved `11.0 s` full drink and 60 Hz bake;
- the exact ready endpoint, fast mouth lift, four cumulative finishing raises
  and exact return to ready;
- a fixed palm-local bottle anchor and upright ready bottle axis;
- a sip axis that lifts the bottle base toward the viewer instead of bringing
  the cap into the camera;
- material separation from the rejected donor palm positions;
- bounded camera-local palm placement, a bent lower-right elbow and no
  vertical-pole arm trajectory;
- absence of adjacent-frame wrist flips;
- readable open-hand and middle-finger silhouettes, including a thumb kept
  inside the fist silhouette;
- whole-arm wave travel with a bounded secondary wrist angle;
- a palm-facing middle-finger recipient side;
- opposed thumb/fingertip radial placement around the bottle axis;
- use of the generated hand-and-forearm-only mesh with viewmodel motion vectors
  disabled.

Classification: donor motion remains `BehavioralReference`; the explicit full
drink timing, generated animation, IK, finger poses, screen placement and grip
are project-owned `Reimplemented` presentation over a user-licensed
third-party AXIS mesh and textures.
