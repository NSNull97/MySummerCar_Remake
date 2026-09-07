# Satsuma — visual/audio correction packet, 2026-09-07

Scope: the user's eight reported defects, including the clarification that
“radiator stands still” means its electric fan. This is a bounded 11A / Phase 1
presentation repair, not Phase 2 art work or full Satsuma parity approval.
Unity is `6000.6.0f1`, HDRP `17.6.0`; the local path override selects this checkout.

## User acceptance — 2026-09-07

The user explicitly accepted the delivered correction packet with “принимаю”.
This records acceptance of the bounded changes described here, with their stated
limitations; it does not assert that every suggested manual check was performed.
The packet is no longer awaiting user acceptance. Broader 11A-V1 / CAR parity,
unsupported cockpit controls and full-scene lighting remain open. This is not
full Satsuma parity approval or approval of the Phase 1 completion gate; Phase 2
remains closed. Transfer classifications and executed test evidence are unchanged.

## Evidence inspected

- Three user screenshots; the first shows the fuel **pump** and the hose that
  belongs to the fuel strainer/filter. The third shows the hose2/hose3 junction.
- Existing canonical Satsuma assembly, dynamic purchased consumables, engine
  mechanics/vibration/audio, electrical/dashboard controls, time and native-save
  integration; the accepted 00–08A foundations are preserved.
- Locked extracted `GAME.unity`, SHA256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
  Read-only evidence is in external staging `runtime-inspection/visual-packet-20260907`.
- Cockpit sources: Speed104548, Temp107749, Fuel109205, Clock105216,
  Odometer105464; original transform/mesh records and emission material.
  `SATSUMA_COCKPIT_CONTROLS_AUDIT_2026-09-05.md` remains the broader cockpit audit.
- The explicitly selected current native document, with all nine purchased
  descriptors, is restored into isolated services only. Tests verify its bytes
  remain unchanged. No user slot was rewritten or “repaired” in place.

## Changes by reported item

| Item | Implemented correction / verified distinction |
| --- | --- |
| 1. Connections | Three mesh-only flexible ends: filter→fuel pump, hose1→head, hose3→hose2. Hash-locked ring and face indices author explicit runtime anchors. A 3 mm insertion and bounded, smooth end deformation close the measured offsets while the far end remains fixed. No assembly pose, collider, mount or donor mesh is moved. |
| 2. Engine shake | Refresh the driven set on assembly graph mutation, including purchased plugs and other installed engine descendants. The actual two-bone belt presentation tree participates, not merely its skinned renderer transform. Frequency follows the reviewed MotorShake RPM period, with bounded translation/roll and frame integration to reduce aliasing. |
| 3. Pump fasteners | Render-only fastener followers rotate around the actual shaft pivot, including rotation of the centered bolt. Shaft reset removes the vibration overlay first, so restoring a save between updates cannot leave residual offsets. |
| 4. White belt | The GUID-only legacy PNG importer had become a Cube texture under Unity 6.6; loading it as Texture2D returned null. Explicit TextureImporter v13 + Texture2D repair, non-null validation and the actual reviewed rubber map fix the missing texture. GUID/source PNG bytes remain unchanged. |
| 5. Exhaust shake | Installed pipe and muffler receive attenuated visual vibration (.32 / .18). Loose parts remain still; there is no new force on the chassis. |
| 6. Radiator fan | Cold fan staying still is correct. The actual saved coolant/engine temperature was about 90.43°C, below the 97°C switch-on threshold. Existing fan logic/mesh rotation was exercised hot, cold, with ignition off, and with its wire disconnected. No always-spinning fan hack was added. |
| 7. Stock instruments | Speed, coolant, fuel, both clock hands, six odometer wheels, stock face/needle illumination, oil/charge warning and existing hazard indicators now use explicit project-owned data and bindings. |
| 8. Audio / apparent idle blip | Native zero-pedal trace stays approximately 906.29–906.68 RPM. The selected coast recording itself contains a pronounced recorded rev near 4–5 s. Satsuma-only steady coast/exhaust derivatives omit that section; the original files and NPC mappings are retained. Six selected engine/start events gain 1 dB of calibration, with a revised high-RPM coast boost ceiling after an actual summed-output clipping failure. |

### Connection geometry and lifecycle

The measured filter hose end is `(0.075427, 0.032485, 1.485894)` in the installed
audit's car coordinates; the pump nipple tip is
`(0.071055, 0.033523, 1.478658)`. The hose2 and hose3 end centers are approximately
`(-0.246819,-0.070407,1.046698)` and `(-0.244504,-0.073519,1.061621)`.
These coordinates are evidence only, not runtime name/position lookups.

`Phase1SatsumaFlexibleConnectionsAuthoring` validates all six source mesh hashes,
selects the reviewed ring indices and serializes local anchors. The runtime owns
only transient `HideAndDontSave` mesh copies, updates cached vertex/normal arrays
after vibration, preserves the non-deformed far end, and restores the source
mesh on detach/disable. A >75 mm separation is explicitly counted as out of
range and releases the deformation, so a removed engine cannot stretch a hose
across the garage. This is `Reimplemented` temporary presentation, not a physical
hose simulation or newly authored production model.

Read-only tools: `Tools/DonorPipeline/Inspect-MeshConnections.py` and
`Measure-SatsumaConnectorAnchors.py`; their CSV inputs and screenshots are ignored
QA payloads. Runtime/full rebuild does not depend on those QA files.

### Instrument semantics and save compatibility

- Speed uses mean driven-wheel angular speed in **rad/s**, not GPS km/h:
  clamp 0…250, multiply by −1.6875°. Wheelspin/reverse behavior follows that source.
- Coolant: temperature × −.461°, clamped −75…0°, Dash1 + ElectricsOK.
- Fuel: litres × −1.66°, clamped −70…0°, additionally tank wiring + ACC.
- Clock: existing project game time, not wall-clock time. Five original installed
  conditions (battery, clock, dashboard, meters, Dash1); no invented ACC/voltage
  requirement. Missing power freezes the hands. The bootstrap explicitly binds
  `IGameTimeService`; vehicle code has no Enviro dependency.
- Existing flattened source parent rotations and needle zeroes are preserved.
  Stock face/needle material variants use per-renderer emission property blocks;
  shared source materials and the 08A UI are unchanged.
- Odometer is an additive optional schema-1 engine extension:
  `hasOdometerState`, `odometerTenKilometerUnits`, `odometerPartialMeters`.
  Old payloads default to donor counter 10000 = **100000 km**, not 10000 km.
  Distance accumulates once per root tick (not once per physics substep), including
  coasting/key-off and reverse; the last digit is continuous. No top-level schema
  bump, stable-ID migration or new playthrough is required.
- New integration coverage serializes the odometer through an unavailable vehicle
  owner, then restores a fresh canonical car and purchased parts without touching
  the native slot. Presentation is not the owner of the odometer state.

### Audio provenance and mix

`UserAudioWavePreparation.Prepare` adds a backward-compatible optional start frame;
existing starter preparation hashes are unchanged. Reviewed selected derivatives
use source frames 352800…1278900, a 882-frame (20 ms) seam, and 925218 output frames
(20.98 s at 44.1 kHz). The independent NumPy byte oracle in
`Tools/Audio/Inspect-EngineLoop.py` agrees with the prepared hashes.

| Satsuma event only | Source SHA256 | Prepared SHA256 |
| --- | --- | --- |
| Coast / idle_sisa4b | `0ae60cdf740da032fd66ded2e967577d01b8c18636cfd9e59fe00ef8defc43fc` | `5ea7caade3a0c413a13a88c5489818b22b3e3c6131b72816ef85b2be1525e0f2` |
| Exhaust / idle_sisa4 | `07f1450fcc892d3c39297eb106d35848f9f4683f5b88af458f32c1377420b70c` | `0abb06c9706014a9a0a4799f1d63033a9f816352139ddaf7b102b088045084e0` |

The explicit selected library now has 19 clips for the same 26 event IDs.
NPC Jani/Petteri and generic mappings retain the original recordings. Starter,
catch, throttle, coast and exhaust source files are not edited. Derivatives stay
in ignored `RuntimeBaseline/Audio/UserSelected`, `TemporaryDirectImport`, with
the existing Phase 2 audio replacement boundary. No Wwise/vendor/DSP architecture
changes are made. This does not claim the remaining source recording is perfectly
stationary or that an automated meter substitutes for human listening.

The first +1 dB device trial (`visual-packet-audio-device-1.log`, output JSON
`satsuma-audio-output-20260907-061414.json`) **failed** coincident high-RPM clipping
checks, exit 5. It is not a passed mix. Reducing the added-boost ceiling alone
(.95→.82) also failed (`satsuma-audio-output-20260907-063446.json`, exit 5),
because that compatibility contract deliberately does not reduce the calibrated
base signal. An explicit .85 output ceiling then passed the 6000-RPM case but
still clipped the 8000-RPM coincident closed-throttle case
(`satsuma-audio-output-20260907-064353.json`, exit 5); that trial is also rejected.
The selected coast event therefore adds an explicit .75 output-gain
ceiling through a new optional zero-default definition field. It bounds this
event's base **and** boost and follows request/master/category scaling; zero
RTPC and mute remain silent. Existing events retain the exact old path, the
backend/pool/DSP filter are unchanged, and the importer validates/copies the field.
The prior running mix report is historical, not validation of the new derivatives.

Final device run: `Logs/satsuma-audio-output-20260907-064813.json`, **exit 0**,
60 actual device measurement stages (not NUnit test cases), including 24 summed
coincident stages. Maximum observed combined sample peak is .990121; headroom
is small and neither true-peak nor arbitrary fault/full-scene overlap is claimed.
The 7-second starter stage spans two seams without an observed sampled quiet run.
Relative to the previously accepted 2026-09-06 device baseline at 2 m, running
RMS changes are +.29 dB at 800/zero pedal, +.06…+.20 dB at 1500–3000, +1.12 dB
at 6000/full pedal and +.99 dB at 8000/full pedal. Closed-throttle 8000 is reduced
1.55 dB to keep summed headroom. These are diagnostic signal measurements, not
claims about human perceived loudness. Starter/catch selected calibration is +1 dB.

## Executed verification

- Scoped motion/belt refresh and actual Texture2D repair executed; source evidence
  hash check passed. Instrument authoring executed; repeat returned changed=0.
- Scoped audio/connection build: stock library changed=1, selected library changed=1,
  connections changed=1, instruments changed=0; no full Satsuma rebuild.
- Final repeated scoped build: `Logs/visual-packet-idempotence-final.log`, all
  four authors returned **changed=0** (stock audio, selected audio, connectors,
  instruments). The generated local content is current and repeatable. All
  assistant-owned Unity sessions exited; the checkout is released for the user.
- Focused current packet: `Logs/visual-packet-focused-2.xml`, **16/16 passed**,
  zero failures/skips. Includes three native read-only tests, three connections,
  physical/save preservation, allocation checks, source mesh restoration, stock
  instruments and HDRP captures. The preceding run was 13/16: explicit Editor
  lifecycle setup was corrected (belt refresh, scene fixture, manual reset).
- Broad final EditMode: `Logs/visual-packet-final-edit-2.xml`, **105/105 passed**,
  zero failures/skips, covering existing operating/mechanical/feedback calculations,
  new visuals/instruments, optional audio defaults/validation, preparation and native
  deferred restoration. Preceding run was 104/105: the existing extension roundtrip
  fixture now explicitly populates canonical odometer fields, while separate old-save
  tests retain absent-field migration coverage.
- Play lifecycle/audio backend: `Logs/visual-packet-final-play-2.xml`, **38 passed,
  zero failures, one skip**. Actual LateUpdate, pause, disable/re-enable, detached
  hose source-mesh restoration, engine audio lifecycle, user volume/mute and
  selected high-RPM output ceiling ran. The skip is batch realtime-PCM readback;
  the non-batch 60-stage device probe above is the separate output check.
  Preceding scoped Play run was 11/12 because its numerical assertions still
  expected the old 11.5 dB starter / .95 coast profile; updated to the explicitly
  authored 12.5 dB / .75 profile and strengthened with high-RPM/user-scaling checks.
- All 35 original selected source hashes were checked again: zero mismatches.
  The native save remains SHA256
  `AC539E263100AFE2001E930704E57653D871C4C4255761CC41A68392B88B8091`.
  Canonical prefab and both derivative audio payloads are confirmed Git-ignored.
- Actual Bootstrap/audio/listener composition: `Logs/visual-packet-bootstrap-play.xml`,
  **5/5 passed**, including explicit stock-instrument game-time binding in the
  real composition, a single canonical persistent car, existing audio routes and
  listener lifecycle. Combined Play runs: **43 passed, zero failures, one batch
  PCM skip**; the device run independently passed.
- Actual HDRP images: `Logs/visual-packet-20260907/graphics` (7 synthetic setup
  views) and `native-graphics` (5 views of the native restored car: pump/filter,
  belt/hoses, lower junction, stock instruments day/night). Visually inspected;
  material/lighting limitations above are retained rather than hidden.

Executed CLI families (configured Unity path, project path and native input are
machine-local; full argument lists are retained in the corresponding logs):

```text
-batchmode -quit -force-d3d11 -executeMethod MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaVisualPacketAuthoring.RefreshAudioAndConnectionsBatch
-batchmode -force-d3d11 -runTests -testPlatform EditMode -testFilter <packet/operating/mechanical/audio/native classes> -liveEngineSavePath <read-only current.save.json>
-batchmode -force-d3d11 -runTests -testPlatform PlayMode -testFilter SatsumaEngineFeedbackPlayModeTests;UnityAudioBackendPlayModeTests;UnityAudioScopedParameterTests;AudioHybridRoutingPlayModeTests
-force-d3d11 -executeMethod MSC.Editor.VehicleSimulation.SatsumaAudioOutputProbe.RunAfterRefreshingLibraries
```

No new compiler error. Existing unrelated Unity 6.6 deprecation/import warnings
remain; this is not a zero-warning or whole-project test-suite claim.

## Limitations and retained manual regression checklist

- No rigidbody mount oscillation or whole-body/chassis reaction is introduced.
  Body-mounted battery/radiator do not shake as if bolted directly to the engine.
- The existing cockpit controller has no high-beam intent; that lens is kept off,
  not falsely lit by dipped beams. Separate turn-indicator input and unsupported
  purchased extra/AFR/tachometer presenters are not invented in this packet.
- HDRP captures use actual private materials but controlled diagnostic lighting,
  not the full Enviro scene. Some pre-existing unlit legacy trim blows out at the
  diagnostic night exposure. Those images verify bindings, not full-night lighting
  acceptance or production art quality.
- “All possible part connections” is not claimed. Three reported/measured junctions
  are addressed; the whole vehicle needs the continuing Phase 1 assembly audit.
- The packet is user-accepted above. For future manual regression: load the same slot, inspect
  the three connections at idle and raised RPM, observe pump bolts/plugs/belt and
  exhaust, drive for speed/fuel/temp/odometer, compare clock to game time, switch
  parking/headlamps and hazards, pause/resume, and restart the engine several times.
  Do not wait for a cold fan to spin; test a correctly wired engine above 97°C.

## Files and reproduction

New runtime: `SatsumaInstrumentPresenter.cs`, `SatsumaFlexibleConnectionPresenter.cs`.
Extended runtime: `SatsumaEngineVisualVibration.cs`, `SatsumaEngineMechanicalMotion.cs`,
`SatsumaEngineFeedbackRules.cs`, `SatsumaOperatingContracts.cs`,
`SatsumaOperatingState.cs`, `VehicleSimulationRoot.cs`,
`Bootstrap/ProductionWorldStreamingInstaller.cs` (time binding), and the optional
`UnityAudioEventLibrary.cs` event-output headroom field.
Scoped Editor authors, two audio manifests, preparation helper, regression/graphics/
native/Play tests and read-only measurement scripts accompany them. Generated
prefab/material/texture/audio outputs are ignored; raw donor data stays external.

Menu actions, with Play stopped:

1. `Tools > MSC Remake > Phase 1 > Satsuma > Refresh Visual Motion And Belt Only`
2. `Tools > MSC Remake > Phase 1 > Satsuma > Refresh Stock Instruments Only`
3. `Tools > MSC Remake > Phase 1 > Satsuma > Refresh Flexible Connections Only`
4. `Tools > MSC Remake > Phase 1 > Satsuma > Build Engine Audio Only`, then
   `Tools > MSC Remake > Phase 1 > Audio > Build User Selected Audio Only`.

These have already been applied locally; they are reproduction instructions,
not hidden manual work needed to make the current generated prefab functional.
No commits, public build, donor-installation mutation or power-off was performed.

Next milestone: continue **11A-V1 full Satsuma state**, starting with an evidence
audit of the remaining parity gaps. This acceptance does not authorize starting
another implementation packet or entering Phase 2.
