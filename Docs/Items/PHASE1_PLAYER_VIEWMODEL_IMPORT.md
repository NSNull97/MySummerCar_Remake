# Phase 1 player viewmodel import and licensed hand override

## Purpose

The private Phase 1 build keeps the locked donor hand presentation as read-only
behavioural evidence. The user-licensed AXIS `Neutral Arms Real-Time` mesh,
textures and project-authored clips provide all six active first-person
visuals: drink, smoke, wave, middle finger, push and thumbs-up.

Gameplay, input, item state and action timing remain owned by project code.
Phase 2 can replace either presentation source through replacement key
`presentation.player.viewmodel.phase2`.

## Source and output boundary

The importer resolves the external AssetRipper export from
`Config/DonorPaths.local.json`. The machine-specific staging path is never
serialized into runtime code.

The generated payload is written only under the ignored boundary:

```text
Assets/Game/LegacyImport/RuntimeBaseline/GameplayPresentation/PlayerViewmodel/
```

The purchased `.blend` is authoring input only. The project-owned Blender
exporter opens it with `--disable-autoexec`, bakes the reviewed actions and
writes only a sanitized FBX plus diffuse, normal and roughness maps into the
ignored private source boundary. Embedded vendor UI/updater scripts are never
executed or imported.

The runtime resource is:

```text
Phase1PlayerViewmodel/Phase1PlayerViewmodelBinding
```

The authoritative source list, GUIDs, hashes, donor transform IDs and legacy
ready-pose evidence are in
`Assets/Game/LegacyImport/Manifests/Phase1PlayerViewmodelManifest.json`.
The licensed-hand screen poses are deliberately project-authored in the
licensed importer and do not consume the rejected donor position/rotation
tracks.

## Build

In the Unity Editor:

```text
Tools
  > My Summer Car
    > Legacy Import
      > Build Phase 1 Player Viewmodel
```

Batch entry point:

```text
MSC.LegacyImport.Editor.GameplayPresentation.Phase1PlayerViewmodelImporter.BuildFromBatch
```

The builder:

1. parses and validates the authoritative JSON manifest;
2. verifies the locked donor scene SHA-256;
3. verifies every selected source asset SHA-256 and Unity GUID;
4. resets only the dedicated ignored output directory;
5. reads selected donor hand transforms and clip timing only as locked
   presentation evidence;
6. rejects donor runtime behaviours, FSMs and controllers;
7. keeps the donor import inventory for auditability without binding its hand
   roots into the active prefab;
8. strips animation events and validates authored clip duration;
9. creates a project-owned HDRP hand material;
10. verifies the exact AXIS source/export hashes, converts the supplied maps to
    an HDRP material and instantiates the licensed Generic rig for all six
    active actions;
11. imports interruptible 60 Hz project-authored legacy clips in camera-local
    space and creates a right-palm bottle grip without importing any vendor
    controller or script;
12. binds the result through
   `MSC.Needs.FirstPersonLifeActionViewmodelBinding`;
13. validates every required visual binding and verifies that the prefab has no
    donor behaviour or Animator controller;
14. writes the manifest SHA-256, licensed AXIS source SHA-256 and a SHA-256
    inventory of every copied or
    generated payload asset into the ignored build report.

Every player build runs a fail-closed Editor build guard. It verifies that the
ignored prefab, copied sources and build report match the current manifest.
It also recalculates the generated mesh, texture, clip, material and prefab
hashes. If the payload is missing, edited or stale, the build stops with the
exact menu command above instead of silently shipping the primitive development
fallback. Import is deliberately not started from the build callback because
rebuilding resets the generated AssetDatabase boundary and performs synchronous
reimports.

`drink_rotate.anim` is explicitly checked against its actual approximately
5.87-second curve duration because the extracted YAML contains a misleading
one-second `m_StopTime`.

## Camera-space, skinning and mesh contract

The licensed actions are authored against the accepted gameplay camera at
120 degrees horizontal FOV (88.507155 degrees vertical at 16:9). The Blender
exporter compensates the earlier review-camera depth without scaling bones and
resets the Generic rig before every baked sample. Unity therefore receives
bounded camera-local animation rather than accumulated Blender world-space root
motion.

The AXIS source uses up to eight bone influences per vertex. The model importer
retains all eight; reducing it to Unity's four-weight default tears the fingers
and forearm. Runtime side meshes keep only triangles whose three vertices belong
to the requested distal arm and exclude the shared shoulder cap and opposite
arm. This avoids the previous cross-body spikes and crescent-shaped shoulder
geometry. AXIS viewmodel renderers do not cast or receive world shadows, so the
first-person hand cannot produce detached overhead shadows or black joint seams.

## Included presentation

- drink — licensed right hand and project-authored full/short/spray/throw clips;
- smoke — licensed right hand and project-authored in/light/out/put-off/reset clips;
- wave/hello — licensed right hand and project-authored clip;
- middle finger — licensed left hand and project-authored clip;
- push — licensed right hand and project-authored on/off clips;
- thumbs-up — licensed left hand and project-authored clip.

## Bottle grip, gestures and input contract

The manifest retains the reviewed donor grip (transform file ID `61293`) as
provenance and behavioural evidence. Its local transform was:

```text
position: (0.3400203, 0.15354463, -0.04791023)
rotation quaternion: (-0.9828533, -0.0077871177, -0.0037561825, -0.18418667)
scale: (1, 1, 1)
```

The current licensed drink visual does not reuse that donor rig-space
transform. The importer derives an empty `Beer Bottle Grip Anchor` from the
licensed right palm and authored curled fingers. The hand sits on the upper
bottle body, the thumb opposes the four curled fingers, and the anchor remains
palm-local through every clip. Its axial offset places the bottle body inside
the fist instead of suspending the bottle below the fingertips. The real
project-owned `WorldItemInstance` follows its world pose for presentation
without being reparented.

The donor beer mesh is longitudinally aligned to local `Z`, not Unity's
primitive-cylinder `Y`, and its neck points along local `-Z`. The authored grip
therefore maps local `-Z` toward the mouth instead of presenting the recessed
base to the camera. The approved ready pose starts at `0.0 s` with a
straight forearm and vertical bottle. The bottle reaches the camera-local mouth
at `0.73 s`, remains there while drinking, then makes four small cumulative
bottom raises around `2.20`, `4.20`, `6.20` and `8.20 s`. It returns to the
exact ready pose by `10.60 s` and holds through the `11.0 s` endpoint. This
prevents the previous reversed «cap into the lens» motion. It does not change
the item root, clone the bottle or use animation as gameplay authority.

1. first `F` on a usable world bottle picks it up into the ready hand pose and
   consumes nothing;
2. the next held `F` drinks continuously;
3. releasing `F` stops drinking but retains the bottle in the ready pose;
4. LMB releases and RMB throws the real bottle;
5. wheel rotation is disabled while the bottle is ready or drinking.

On first pickup, the binding enters from a small lower-screen offset and reaches
the authored licensed ready pose over `0.22 s`. Starting a sip moves the same
rig and real bottle toward the camera-local mouth pose. Releasing `F` reverses
the current drink presentation and returns to ready without changing item or
needs authority.

Presentation-only gestures use the canonical Player input map:

- `H` / gamepad D-pad Up — wave hello;
- `M` / gamepad D-pad Right — middle finger.

Camera-facing composition is deliberate rather than copied from the donor:
the right-hand wave points the palm outward into the scene, keeps the forearm in
the approved 25-30 degree entry and combines restrained forearm motion with a
more active wrist. The left-hand middle-finger gesture uses the reviewed
palm-facing first-person composition; its neighbouring fingers use separate
joint curls and the thumb crosses the fist. Both gestures remain immediately
readable at the gameplay FOV of 120 horizontal degrees.

A gesture is rejected while drinking, holding a drink-ready pose or performing
a life action. Starting a gameplay action interrupts any active gesture. Both
gesture clips stop themselves and never write simulation or save state.

While the ready or drinking presentation is active, the same authoritative
Rigidbody is snapped directly to the animated grip in `LateUpdate`. It is not
cloned or reparented. Its collision and simulation flags are held only
transiently and are restored before drop, throw, placement, handoff, disable or
save restore. The `world.entities` save domain records the pre-carry physics
flags rather than these presentation-owned flags.

The ready state is transient presentation state and is deliberately not added
to save DTOs.

Unity's legacy `Animation.Play` path evaluates this purchased Generic hierarchy
incorrectly at runtime in Unity 6 even though static clip sampling is valid. It
is retained only as the serialized clip container; the binding owns a small
unscaled-time clock and calls `AnimationClip.SampleAnimation` directly.
Gameplay completion, needs and item state remain independent of sampled frames.

The final Unity camera audit renders the real project beer mesh and samples the
ready, mouth, drinking and all gesture poses at the gameplay 120-degree
horizontal FOV. A realtime HDRP PlayMode audit exercises the same direct-
sampling path that runs in game, verifies finite bounded skinning and renders
the bottle and cigarette grip proxies. The focused EditMode fixture validates
six contracts:
clip timing, anatomical side selection, camera-local pose bounds, untorn mesh
topology, eight-weight skinning/shadow policy and event-free 60 Hz clips.

## Explicit exclusions

The generated prefab does not include donor scripts, FSMs, controllers,
animation events, bottle/cigarette geometry, audio, gameplay state or a
physical first-person body. A project item remains the visible/authoritative
bottle. The legacy hand animation is presentation only and may be interrupted
or reset without changing simulation state.

The generated payload is private and ignored by Git. Donor presentation is not
`ProductionReady`; the licensed source remains governed by its original Asset
Store licence and still requires final in-game visual acceptance before being
classified as production-ready for this project.
