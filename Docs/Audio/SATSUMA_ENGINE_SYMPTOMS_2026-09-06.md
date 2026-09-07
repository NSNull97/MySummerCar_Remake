# Satsuma causal engine symptoms

Status: **Canonical authored; focused EditMode and D3D11 PlayMode passed.**
This is batch 4 of the active 11A-V1 live-engine pass, not full parity or human
auditory approval. The user-reported replacement-pack loudness and headlights
remain queued after the current integration plan.

## Authority and spatial ownership

`SatsumaEngineSymptomAudio` consumes the opt-in operating model's current point
and the existing saved radiator-fan state. It writes no simulation, part or save
state. `SatsumaEngineFeedbackPresenter` remains the single emitter-registration
owner and forwards pause, disable, reset and restore to the new helper.

- Belt squeal: actual belt availability, alternator adjustment and rotating RPM.
- Pinging: current mixture/ignition symptom, only while combustion is running.
- Valve ticking: current adjustment/rocker health symptom, one audible impulse
  per four-stroke cycle at full severity.
- Bearing knock: installed crankshaft condition below 10, a new read-only point
  output. Existing part-owned wear is still authoritative.
- Intake spit and exhaust backfire: current valve/plug/cam symptoms, with separate
  carburetor and effective exhaust-outlet emitters.
- Radiator start/run/stop: actual electric-fan state, independent of crank RPM
  and permitted with a hot stopped engine. Restoring an already-running fan
  resumes only its loop, not a historical startup.

Crank/valve/bearing sounds follow the existing block emitter. Two additive
stable emitter IDs follow the explicitly bound stock carburetor and radiator
part transforms. The separate stock-chain exhaust outlet binding is preserved.
Five emitters register once; only this vehicle's handles are stopped.

The helper retains at most nine handles. Repaired faults clear their own phases
and sounds. Unavailable media retries are bounded. Pause/reset/load discard
accumulated impulses, rather than playing a burst when the simulation resumes.

## Frozen media and adaptation boundary

Source is frozen `GAME.unity`, revision
`msc-world-baseline-04a1.1-c3f2f337`, SHA256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Every individual media hash is in `Phase1SatsumaEngineAudioManifest.json`.

| Event | Reviewed evidence | Media / configuration |
| --- | --- | --- |
| Belt loop | Jumping110290, AudioSource95190 | `belt_whine.ogg`, linear 1–100 m, pitch `1 + RPM/30000` |
| Pinging loop | Cylinders104983 Advanced, SoundPinging114117 / AudioSource95864 | `motor_pinging_loop.ogg`, gain .1, logarithmic 1–5 m, pitch clamped .8–1.2 |
| Valve impulse | Valves108555, Damage111117, AudioSource95337 | `valve_knock.wav`, logarithmic 1–20 m |
| Bearing impulse | Damage111117, AudioSource94113 | `damage_bearing.ogg`, base gain .5, logarithmic 1–20 m |
| Intake / outlet pops | Spitting111114 and Backfires111113, AudioSource95741 | Both use `spitting.ogg` with distinct event IDs, origins and fixed pitch |
| Fan | Cooling106153 and AudioSource94926 | `radiator_fan_start/run/stop.ogg`, gains .15/.1/.15, logarithmic 1–7 m |

The source's Satsuma Backfires111113 explicitly selects the `spitting` variation;
unrelated clips called `backfire` are not substituted by filename inference.
The selected user pack has no corresponding replacement for these nine new
events; the previous 26 mapped replacements are untouched in this batch.

Classification: scalar/media settings **ConfigurationTransferred**, controller
and cadence **Reimplemented**, ignored private media **TemporaryDirectImport**.
No donor FSM, controller, assembly or animation-event authority is imported.

Known calibration differences: deterministic revolution-integrated impulses
replace donor randomized waits; bearing sample density is one per eight turns
and pops one per 24 turns at full severity. Severity modulates density and gain.
One-shot pitch is fixed (valve 2; intake 1.3; outlet .65) because the existing
Unity fallback requires fixed-pitch expiry for one-shots. Its validation and
pitch ceiling were preserved. Continuous belt/pinging loops retain live pitch.
This is causal playable feedback, not a phase-perfect donor waveform claim.

## Executed checks

- First author compile stopped on a missing **test-only** Unity fallback assembly
  reference. Added the explicit test reference; no runtime dependency changed.
- Next author run rejected proposed one-shot pitch RTPCs using the existing
  library validator. Fixed-pitch definitions preserve that boundary. Failure
  logs remain in `Logs/live-engine-symptoms-author[-fixed]-20260906.log`.
- `Phase1SatsumaEngineFeedbackAuthoring.RefreshEngineFeedbackBatch`: exit 0,
  17 hash-locked events, canonical refresh changed 4; no full vehicle/world
  rebuild. `Logs/live-engine-symptoms-author-pitch-20260906.log`.
- **67/67 EditMode passed**, zero failures/skips, exit 0:
  `Logs/live-engine-symptoms-edit-20260906.xml`. Includes source and operating
  regressions, old feedback, fault causality/cadence, bounded voice ownership,
  fan state, canonical idempotence and media validation.
- First new PlayMode compile caught an incorrect DTO member in the test fixture;
  corrected to the existing `satsumaOperatingState`, without changing the DTO.
- **22/22 D3D11 PlayMode passed**, zero failures/skips, exit 0:
  `Logs/live-engine-symptoms-play-fixed-20260906.xml`. Includes real imported
  Unity voices, moving emitter positions, belt pitch, separate radiator origin,
  pooled attenuation, real LateUpdate, pause and restore. Controlled telemetry
  tests are not claimed as a fully assembled-car gameplay comparison.

Compatibility: existing event IDs and original eight media mappings remain;
new events/emitters are additive. No save migration, physical ownership change,
UI change or user-save write. Full regression, mechanical motion, graphics
review and manual listening remain part of the active pass.
