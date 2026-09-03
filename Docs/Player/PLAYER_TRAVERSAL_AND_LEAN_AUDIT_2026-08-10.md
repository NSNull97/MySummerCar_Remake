# Player traversal and forward-lean audit — 2026-08-10

## Scope and evidence

The donor installation and BetterMSC were inspected read-only. No donor or mod
assembly is a runtime dependency of the remake.

| Evidence | SHA-256 | Use |
|---|---|---|
| `raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity` | `C3F2F3373CCAD4FCBE104840FCB83E364F55438070E808D11EBE4996BC0476C4` | Exact `CharacterController`, camera/posture and Reach FSM serialization |
| `Assets/Plugins/Assembly-UnityScript-firstpass/CharacterMotor.cs` in external staging | `FBE413DD246120ECD65B88D440E057E49AE3123D72524F4B36AD1AA6FC63644A` | Ground-normal projection and grounded movement behavior |
| Installed `BetterMSC.dll` | `20410ADBB59CF7A3252103C36301A91CA8F9C83AE3CC9D1E4F8DE0BF0D17E6F8` | Third-party behavioral reference for collision-limited lean and wall impact conditions |

## Audit result

The previous remake controller was the traversal fault. Its capsule was
`1.8 m` high and `0.32 m` in radius (`0.64 m` diameter), while the donor
serialization is `0.5 m` high and `0.12 m` in radius (`0.24 m` diameter).
The remake was therefore about `2.67x` wider. It also reduced step offset from
`0.4 m` to zero in deep crouch because posture resized the physical capsule.

The donor keeps the small traversal capsule and changes the first-person camera
height instead: standing `1.4 m`, crouch `0.85 m`, deep crouch `0.3 m`. Exact
serialized hierarchy inspection also resolves the posture of the original
Reach action: `PLAYER/Pivot` is at local `Y = -0.3 m`; its camera is `1.7 m`
above that hinge, yielding the `1.4 m` standing eye height. The earlier remake
placed the hinge at `Y = 1.1 m` with only a `0.3 m` camera arm, so it visibly
tilted the head instead of bending the body.

BetterMSC rotates that donor pivot up to `40 degrees` at `120 degrees/s` and
uses `Quaternion.RotateTowards` on release, so returning is gradual rather than
an immediate reset. A `0.11 m` sphere cast limits the head path. Its wall-hit
gate requires forward controller speed greater than `3 m/s` and a wall normal
within `30 degrees` of the direction facing the player. The inspected impact
routine also drives a short `SleepEyes` response and impact/vocal audio; its
intoxication mutation and nearby-NPC reaction remain outside this bounded fix.

## Project-owned reimplementation

- `FirstPersonMotor` now enforces the exact donor traversal capsule:
  `height 0.5`, `radius 0.12`, `step 0.4`, `skin 0.03`, `slope 90`, and
  `minMoveDistance 0`.
- Posture changes camera/head-clearance height, not physical capsule width or
  step capability.
- Ground motion projects onto the contacted walkable normal and adds a bounded
  `0.4 m` ground snap for small descending thresholds.
- The view hierarchy is split into `LeanPivot -> CameraPivot -> ImpactPivot ->
  LookPitchPivot -> FirstPersonCamera`. Lean, impact and mouse pitch can no
  longer overwrite the same transform.
- `LeanPivot` now stays at the exact donor `Y = -0.3 m`; posture moves the
  separate `CameraPivot` instead. At standing height the camera arm is `1.7 m`,
  so a full lean advances the viewpoint by about `1.09 m` and lowers it by
  about `0.40 m`: this is a body bend, not a neck tilt.
- Forward lean reaches `40 degrees` at a slightly faster project-tuned
  `150 degrees/s`; release returns smoothly at the BetterMSC-evidenced
  `120 degrees/s`. Collision may still clamp inward immediately so the head
  cannot tunnel through a wall.
- A `0.11 m` sphere cast with `0.02 m` clearance limits the body-driven head
  arc.
- A forward impact is raised only above `3 m/s` and against a near-frontal wall
  (`<30 degrees`). It emits `PlayerLeanImpact`, applies a short camera kick,
  keeps `35%` of forward velocity and uses a `0.75 s` repeat cooldown.
- `PlayerLeanImpactFeedbackPresenter` adds a short unscaled-time eyelid close /
  open response. `PlayerLeanImpactAudioPresenter` posts the existing typed
  `audio.event.interaction.impact` through `IAudioBackend` and the shared player
  emitter, so Wwise and the Unity fallback both follow the same event path.

This is `ConfigurationTransferred` plus clean-room `Reimplemented` behavior.
BetterMSC is only a hash-locked `BehavioralReference`; its DLL, settings,
audio files, intoxication mutation, `SleepEyes` asset and NPC reaction code were
not copied. The eyelid presenter, camera kick and speed loss are project-owned.
The sound binding reuses the already-authored project event; it does not add a
new donor or mod audio payload.

## Executed validation

- Player prefab import and separated pivots: EditMode `1/1` passed.
- Player/Interaction focused EditMode: `29/29` passed.
- Player/Interaction focused PlayMode: `12/12` passed.
- New locomotion parity fixture: `6/6` passed, covering exact capsule values,
  a `0.36 m` opening, a `0.28 m` threshold, head-sphere wall restriction,
  full-body hinge travel, smooth return and running lean impact/eyelid feedback.
- Audio player-integration PlayMode: `2/2` passed, including a real running
  lean collision posting exactly one typed impact event and intensity value.
- Existing real M4 garage/house traversal route was regenerated against the
  corrected prefab and passed `1/1` in `27.447 s`; evidence now records player
  prefab SHA-256 `619a875fd111bdd2d1f63e3571f9cbc81e3b9b45206957bbc9717bbc75a8fab3`.

The synthetic opening and threshold prove controller behavior, not exact donor
garage geometry. Manual acceptance is still required in the active donor-world
garage pit, doorways, irregular stairs, steep banks and several wall approach
angles.

## Compatibility

Input actions, save DTO schema, stable IDs, interaction ray origin, carry anchor,
audio listener and accepted UI remain compatible. Builder `1.2.1` reproduces
the corrected prefab hierarchy and feedback component. Production audio
composition adds one presenter while reusing the existing registered player
emitter. No donor hierarchy lookup, BetterMSC type or external path is present
in runtime code.
