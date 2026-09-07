# Satsuma live-engine pass: reference findings before implementation

Status: **Read-only inspection and planning, not an implemented or verified
gameplay feature.** Scope is the user's 2026-09-06 continuation of 11A-V1,
including the later spatial-audio observation. Runtime authoring is paused while
the user performs manual Unity tests. No `Assets`, Unity session or user save was
changed by this inspection.

## Evidence boundary

- Frozen donor scene: staging-relative
  `raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`.
- Revision: `msc-world-baseline-04a1.1-c3f2f337`.
- SHA256, checked again during this audit:
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
- Donor object/component numbers below are **provenance**, not runtime identity.
  Observations are `BehavioralReference` / `ConfigurationTransferred` candidates;
  no donor controller or FSM is to be compiled or recreated as a runtime import.
- Enabled actions and referenced properties must be checked, not just state
  names or serialized variable snapshots. The scratch binary action decoder
  used here decodes scalar values, but does not fully decode arrays and can
  collapse repeated unnamed fields. Unresolved array guards remain unresolved;
  they cannot establish an implementation contract by themselves.

## 1. Engine apparently sounding from the exhaust

### Verified source/configuration facts

The canonical `Satsuma_Phase1_V1a.prefab` has one
`SatsumaEngineFeedbackPresenter`. Its engine emitter component
`392036730538171441` references the block's Transform
`5479578618912755577`, on the same object as block PartInstance
`1462838725858339239`. The emitter GameObject's own origin is **not** its audio
origin. Moving that GameObject alone would not fix this report.

The separate exhaust emitter `4184138698270658058` owns audio-only Transform
`6842208237608413645`. `SatsumaEngineExhaustBinding` selects the last connected
stock exhaust outlet using existing installed part state. It does not move the
engine source. Its four chassis-local points remain the reviewed engine,
headers, pipe and muffler locations from the earlier audio audit.

There is a real attenuation mismatch:

| Layer | Frozen source | Distance configuration | Current explicit Unity path |
| --- | --- | --- | --- |
| Muffler exhaust | AudioSource 94487 on GO7750 | gain .2, min 1 m, max 500 m, rolloffMode 0 | same distances, but linear rolloff |
| Open engine exhaust | AudioSource 95540 on GO29225 | gain 1, min 1 m, max 500 m, rolloffMode 0 | same distances, but linear rolloff |
| Belt whine, not imported yet | AudioSource 95190 on GO21930 | min 1 m, max 100 m, rolloffMode 1 | must not inherit exhaust's curve blindly |

Unity's published source defines rolloff 0 as logarithmic and 1 as linear.
`UnityAudioBackend.cs` currently assigns `AudioRolloffMode.Linear` to pooled
sources; `UnityAudioEventDefinition` has no per-event rolloff field. Thus a donor
500 m maximum was transferred without its associated curve. The linear
distance gain at 5 m is approximately `(500 - 5) / (500 - 1) = .992`, before
other mix factors. This is source-equivalent arithmetic, **not an audio capture**.
See [Unity 6.6 rolloff API](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/AudioRolloffMode.html)
and [Unity's AudioRolloffMode definition](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/Audio/Public/ScriptBindings/Audio.bindings.cs).

There is also a balance consideration: at 800 RPM, zero throttle and the stock
muffler, existing formulas produce throttle/coast gains .01/.05 while the
exhaust gain is .2. These are source coefficients, not perceived loudness:
the recordings' level/spectrum and listener distances also matter.

### Smallest compatible correction and acceptance

Add an optional per-event attenuation selection preserving the existing linear
default for old libraries and unrelated events. Author only reviewed Satsuma
events with their appropriate curves. Do not change `IAudioBackend`, global
audio settings, all Wwise banks or unrelated library defaults to fix one car.
Check pooled-source reuse so a prior event's curve cannot leak to the next one.

Then verify actual front/side/rear positions and gains with one correct player
listener, installed engine, moving car, stop/start, native restore, complete
stock exhaust and broken chains. Compare the engine and outlet recordings at
idle and under load before changing their coefficients. The attenuation mismatch
is a likely contributor; the user's exact auditory observation remains a manual
acceptance item, not a completed diagnosis from code alone.

The separately identified failed-crank `Cranking`/`Stalled` chatter and false
exhaust rundown still require correction; spatial tuning cannot fix those.

## 2. Alternator belt material and movement

The installed belt renderer currently references generated material GUID
`55c3a1cb13000a74e8a9b59fa206138f`, asset
`Vehicles/Satsuma/Materials/1dc990c84250ce844b24b6439ec55445.mat` under the runtime
baseline. It is HDRP/Lit, but `_BaseColorMap` and `_MainTex` both have null
texture references and `_BaseColor` is white. The frozen `Material/fanbelt.mat`
instead references `Texture2D/fanbelt.png`, source texture GUID
`92213420fc2d0b34f938d80a92971def`.

`Phase1SatsumaInstalledBeltAssets` already knows and hash-checks this exact
texture (SHA256
`9fd61b8cad32b3774742218e047cc5263a8a2104970c70c976a52a36f581f7db`).
Its creation path binds it, but an existing mismatched material is rejected,
not repaired. `Phase1SatsumaConsumableBeltPresentationAuthoring.ValidateAssets`
only checks material existence and mesh skinning, so that separate path does not
detect a null base map. A narrow, explicit repair plus a texture-binding
regression is required; a full vehicle/world reimport is not.

Frozen belt Rotation FSM110291 on GO21930 animates the source material's
`mainTextureOffset.y`: it integrates `-RPM / 60` per second and wraps its phase.
It is texture travel, **not rotating the whole triangular belt mesh**.
Jumping FSM110290 separately reads alternator `BoltCheck.Rotation`, alters the
scale bone, drives whine pitch `1 + RPM / 30000`, and increases belt wear from
the departure from rotation 7. Broken pump/alternator guards and stop/damage
gates require the full referenced guard review before reproducing wear rates.
AudioSource95190 references `AudioClip/belt_whine.ogg`, source GUID
`02a4a14fd48d3214dab1a30043f87bdd`; it is not part of the existing eight-clip
healthy audio packet.

Reuse the two-bone installed-belt presenter and actual alternator adjustment.
Apply motion to explicit presentation bindings with pause/detach/disable/restore
guards and no shared-material mutation across cars. Preserve collider and mount
poses. The other generic `Rotation` candidate, FSM110034, reads a Color/Timescale
controller and is not established as an engine animation; do not include it
because of its name.

## 3. Operating-model findings that constrain the implementation

These supplement the prior starter, electrical, adjustment and fastener audits;
they are not a complete new simulation specification.

- Choke consumer FSM109201 writes Mixture109336.Choke from
  `ChokeLevel + 1` every frame. Its earlier multiplication by the serialized 1.8
  multiplier is overwritten in the same enabled action sequence. Do not use
  the stale 1.8 field as the final choke contract.
- Mixture109336 uses engine temperature in an air-density factor and consumes
  choke and the existing carb adjustment. Enabled rich/lean branches differ
  from two disabled low-AFR stop/sputter actions. Do not turn disabled actions
  into new donor requirements. Fuel/priming and tuning must affect the existing
  independent crank-versus-combustion boundary.
- Cooling106153 has thermostat, pressure, fan, leakage and wear paths. The
  reviewed thermostat comparison is 80 C and fan threshold is 97 C with
  electrical conditions. Oil112415 models pressure, filtering, leaks, friction
  and wear. These are not evidence for a universal immediate oil/coolant
  no-start switch. Extend the Satsuma operating profile, preserving generic
  M06 and other vehicle behavior.
- Oilfilter leakage uses `(8 - Tightness) / 800`; oilpan leakage derives from
  its aggregate fastening value. These are donor intermediate leak values,
  **not yet proven litres/second**. Identify downstream consumption and timing
  before adapting them to the project's SI state.
- Rocker adjuster104479 increments/decrements its alignment by .3, reads the
  rocker database's limits and rotates the screw by alignment times 43 degrees.
  Valves108555 checks intake against 6–8 and exhaust against 5–7 for the
  reviewed cylinder branches and produces power/noise consequences. These are
  game adjustment units, not physical millimetres of valve clearance. Preserve
  the five true mounting bolts and explicitly retire the eight mistaken bolt
  entries through a bounded old-save migration; never convert old bolt stages
  into invented valve calibration.
- Braking and clutch hydraulics need separate reservoirs. Existing service
  bottles already carry finite oil, coolant and brake-fluid amounts. Receiver
  capacities, caps, fill/drain transforms and full downstream failure behavior
  still need final donor evidence. Do not derive the game's capacities from a
  different real-car variant. Petrol refuelling is excluded from this pass.

## 4. Real-engine mechanical cross-check

Primary source: Nissan Motor Co. A10/A12 service manual scans, not a fan wiki.
The available manual covers F10/B210/B120 configurations; it is a **family-level
mechanical reference, not proof of every E10/Satsuma detail**. Donor game
evidence remains the Phase 1 behavior authority.

The [Engine General section, EG-2/EG-3](https://retrojdm.com/Scans/Datsun/Misc/Service%20Manuals/A10%20and%20A12/PDF/01%20-%20Engine%20General%20%28EG%29.pdf)
shows the accessory layout and lists an inline-four OHV engine with firing
order 1-3-4-2. It also shows different fan arrangements across applications;
one photograph is not a universal belt-driven-fan specification.

The [Engine Mechanical section, EM-2 through EM-4, figures EM-2/3/4](https://retrojdm.com/Scans/Datsun/Misc/Service%20Manuals/A10%20and%20A12/PDF/03%20-%20Engine%20Mechanical%20%28EM%29.pdf)
identifies crankshaft, pistons, connecting rods, flywheel, chain-driven camshaft,
tappets, pushrods, rockers and sprung valves. The resulting presentation audit
must distinguish rotating shafts, reciprocating pistons/rods and rocking valve
gear, not rotate every engine part uniformly. A four-stroke cam phase at half
crank speed is a mechanical-model inference; exact donor mesh axes and motion
bindings must still be measured.

The [Cooling section, CO-2 through CO-5](https://retrojdm.com/Scans/Datsun/Misc/Service%20Manuals/A10%20and%20A12/PDF/05%20-%20Cooling%20System%20%28CO%29.pdf)
explicitly distinguishes the F10 electric radiator fan from engine-driven
versions and shows the pump and alternator belt adjustment. It supports keeping
the reviewed donor radiator fan electrically/thermally controlled rather than
locking it to crank RPM. It also supports tension-related pump/alternator
problems; its exact capacities and pressures are not silently substituted for
the donor's game values.

Motion binding checklist for the implementation: crank/pulley/flywheel,
cam/chain, pistons/rods, valve gear, belt/pump/alternator, electric radiator fan,
and any separately evidenced carb/distributor/starter mechanical leaves. Only
visible, safely bindable geometry is animated; absent internal geometry is an
explicit limitation, not permission for an unrelated production-art rebuild.

Scans were rendered and the complete relevant pages visually inspected with
the PDF workflow. Raw PDFs and renderings were moved outside Git to donor
staging `references/engine-audit-20260906`. No PDF was edited or re-exported.
PDF SHA256 values:

| Section | SHA256 |
| --- | --- |
| General | `d246933011364bed42ca6730bc7590c804c671792df851df4d24f889c3d32da9` |
| Mechanical | `911f55d904aed407d68997091f72b3cf437c8b4d1193dd2800aab9d18e2f659c` |
| Cooling | `54c23b80d7423492ccf07d6c813947eb5abcf587bee328f2fcb623bb70b21b61` |

## 5. Executed checks and current limits

- Read selected scene/prefab/material and managed reference source; verified
  frozen donor scene SHA256. No original executable or injected probe used.
- New project-owned `Tools/DonorPipeline/Read-UnityYamlObject.ps1`: selected
  five exact objects (two AudioSources and three FSM records), confirmed types
  and byte-offset headers; duplicate IDs, missing ID and size-limit excess
  were all rejected. Subsequent belt queries also succeeded.
- Downloaded three primary PDFs to temporary files, rendered thirteen pages
  with bundled Poppler, visually inspected those pages and moved all sixteen
  acquired/generated files outside Git. No new dependency installed.
- No Unity compilation, EditMode/PlayMode run, live input injection, audible
  capture, vehicle save edit or runtime content authoring in this inspection.

Compatibility impact so far: **none on 00–08A runtime APIs, serialized assets,
stable IDs or save schema**. Only this audit, the scoped plan and read-only
inspection tooling changed. The exact engine-mount physics and purchased-battery
ownership proposals remain separate; this report does not approve them.

Next milestone: resume this same **11A-V1 Satsuma live-engine integration pass**
after the user reports closing Unity, starting with compatible operating-state
and failed-start/audio regressions. This does not close Phase 1 or open Phase 2.

This section records the read-only audit's original checkpoint. The user later
closed Unity and authorized implementation; subsequent changes, exact
compatibility impact and executed regressions are recorded in
`SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md`. The two broader ownership and
physical-mount proposals above remain outside the implemented packet.
