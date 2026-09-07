# Satsuma event balance — second manual loudness finding

Later manual feedback requested substantially more running-engine presence,
quieter ambience and a starter-gap fix. Current follow-up settings and evidence
are in `SATSUMA_RUNNING_MIX_AND_STARTER_CONTINUITY_2026-09-06.md`; this report
retains the previous pass and its measurements, not the final listening approval.

Status: **Scoped implementation, actual device-output capture and final automated
regression passed; manual in-game listening remains pending.**
This is a bounded Phase 1 / 11A-V1 audio follow-up, not an engine simulation
change, a new sound pack, a Wwise-bank rebuild or a donor-parity approval.

## Report and scope

The user adjusted the car with the existing Editor, then reported that the
engine, starter and pops were very quiet while previously existing audio was
normal. The user closed Unity and authorized this repair on 2026-09-06.
The earlier relative clip calibration is therefore not an accepted solution
to the complete Satsuma mix. Master and engine were already 100% in saved
settings, with reduced loud sounds and focus muting disabled.

Read-only inspection covered the actual selected and stock event libraries,
both importers, imported-PCM measurements, the backend, emitter authoring,
feedback/symptom producers, listener composition and the latest native slot.
The saved block/carb transforms are beside the saved player position, not at
a displaced garage/world origin. This alone is not a live spatial-output test.

## Separating two different adjustments

- `calibrationGainDb`: existing per-clip correction for the replacement file.
- `mixGainDb`: optional event balance, inherited from the existing event
  template even when a user replaces its clip. Default 0 preserves the exact
  old path for every unrelated event and every old serialized library.

The existing request/category/master/focus/scoped-RTPC gain and 0..1 ceiling
remain first. The two explicit corrections then multiply the result. The
existing `UnityAudioCalibrationFilter` handles only overflow above the source
volume ceiling; it is not replaced or widened. Both fields remain within
-12..+12 dB and their sum cannot exceed 18 dB (less than the filter's existing
8x limit). Invalid authored or serialized combinations fail validation.

The stock manifest retains the transferred event base volumes, pitch,
distances, rolloff and parameter bindings. It adds these project-authored trims:

| Existing events | Event balance |
| --- | ---: |
| Starter engagement / cranking loop | +6 dB |
| Engine catches | +2 dB |
| Front throttle bed | +10 dB |
| Front coast/idle bed | +12 dB |
| Separate exhaust bed | +3 dB |
| Belt / valve ticking | +4 dB |
| Pinging / bearing / intake spit / exhaust backfire | +6 dB |
| Radiator fan start / loop / stop | +4 dB |
| Key insertion/removal and all unrelated events | 0 dB, unchanged |

The first constant-boost trial produced a clipped 6000-RPM coincident-layer
sum in the real listener capture. That trial is not accepted. A separate
optional `mixBoostCeiling` now limits only the **added** positive boost of the
three running beds (throttle .5, coast .6, exhaust .5). Already louder original
levels are not reduced. The ceiling scales with request/master/category/focus
and accessibility settings; it is not a volume floor and cannot unmute a zero
RTPC. Clip correction stays independent. This targets the quiet low end while
avoiding a constant high-RPM multiplier. Full-scene/fault-overlap true peaks
remain a separate limitation, not something a per-voice filter proves.

Exactly 15 of the 17 engine definitions opt in. Six existing user-selected
engine replacements inherit the corresponding trim; their previous measured
clip corrections are unchanged. Other replacement mappings are unchanged.
There is no forced 2D audio, relocated emitter, enlarged distance radius,
modified RPM formula, altered failure cadence or authored input/save value.

## Output measurement method

`SatsumaAudioOutputProbe.Run` launches only when explicitly called in a fresh
non-batch Editor. It creates an unsaved empty scene, a known sine, the real
Unity fallback, the actual two libraries, and an explicit listener/emitter.
It never reads/writes a native save or saves a scene. The command-line process
exits after the capture; a deadline prevents an indefinite probe session.
`RunAfterRefreshingLibraries` first runs only the two scoped audio importers.

The diagnostic compares the current definitions to in-memory copies with only
event balance removed. It captures source and **listener** PCM independently,
with a silent flush between events and 20 ms capture spacing. Listener output
is the decisive measurement for the downstream overflow filter. It checks a
known +6 dB correction and master mute, then samples starter/catch, stock
800-RPM idle layers and pops at one and two metres. Combined 800/3000/6000-RPM
stock layers use a conservative coincident-emitter setup to inspect summed
peaks; that setup is not claimed to be the real driver's head/engine/tailpipe
geometry or a populated-world speaker measurement.

The first exploratory capture succeeded where the earlier batch/Test Runner
probe returned no audio. Log: `Logs/satsuma-audio-output-before-20260906.log`;
report: `Logs/satsuma-audio-output-20260906-180629.json`. The steady idle coast
bed measured about -34 dBFS RMS at 1 m / -40 dBFS at 2 m, versus approximately
-22.5 / -28.7 dBFS for the isolated exhaust layer. The original exploratory
transient/next-event peaks are not used for acceptance because that first
probe did not yet flush the previous source's output-history buffer.

These are sampled device-PCM RMS values, not LUFS, dBA/SPL, calibrated speaker
levels, human listening approval or a final-bus true-peak measurement. The
mono/stereo output path and captured window affect their absolute values.
Unity API references: [source output](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.GetOutputData.html),
[listener output](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioListener.GetOutputData.html).

## Verification record

`Logs/satsuma-audio-output-balanced-20260906.log` and
`Logs/satsuma-audio-output-20260906-181509.json`: the real +6 dB control and
master mute passed; the first constant-boost trial was rejected because the
summed 6000-RPM signal reached 1.0. The report's original exit=0 only tested
the device controls, not summed clipping. The refined probe additionally
returns a nonzero code if its combined-layer peak reaches the clipping rail.

Final headroom-limited capture: `Logs/satsuma-audio-output-headroom-20260906.log`
and `Logs/satsuma-audio-output-20260906-182346.json`, exit 0. **43 measurement
stages**, not 43 NUnit tests: three device controls, 32 isolated before/after
stages and eight summed stock operating points. The known sine's listener peak
was .0280172 before / .0559017 at +6 dB; master mute returned exactly zero.

| Actual listener PCM, at 2 metres | Before RMS dBFS | After RMS dBFS |
| --- | ---: | ---: |
| Starter engagement | -24.6 | -18.6 |
| Starter loop | -21.0 | -15.0 |
| Engine catch | -14.7 | -12.7 |
| Minor throttle layer at 800 RPM / zero pedal | -50.8 | -40.9 |
| Main coast/idle layer at 800 RPM / zero pedal | -40.1 | -27.9 |
| Separate stock exhaust | -28.8 | -26.7 |
| Intake spit | -26.1 | -19.4 |
| Exhaust backfire | -25.4 | -19.4 |

Short transient RMS is a sampled-window estimate, not whole-clip integrated
loudness. The stock summed operating points were 800/0, 1500/.2, 3000/.45,
3000/1, 6000/1, 6000/0, 8000/0 and 8000/1 (RPM/pedal). The largest observed
listener sample peak was **.916458**, below the clipping rail. This is not a
claim about every fault overlap, unmuffled outlet, intersample true peak or
the full Wwise-plus-Unity scene sum.

Final executed regression on Unity **6000.6.0f1**:

- `Logs/satsuma-event-mix-edit-final-20260906.xml`: **60/60 passed**, zero
  failures/skips, 3.057 s. Includes both new content-validation tests, the
  separate-gain and boost-headroom tests, existing donor feedback/symptoms and
  audio runtime controls. Explicit test-case presence was checked in the XML.
- `Logs/satsuma-event-mix-play-verified-20260906.xml`: **42 passed / zero
  failed / one explicit batch-device skip**, 25.775 s. Includes the new real
  canonical selected-pack crank/idle/headroom/mute test, existing pause/restore/
  emitter ownership tests, hybrid and pooled gain tests, Bootstrap, and two
  complete production sessions with official Wwise ready, six banks loaded,
  the actual selected clips and the existing native thunder event.
- The skipped interactive Test Runner PCM test is not counted as passed.
  Its downstream-gain assertion now correctly reads listener output, not the
  earlier source tap. The non-batch device probe above separately executed
  the actual output/gain/mute checks.
- `Logs/satsuma-event-mix-idempotent-20260906.log`: both scoped importers
  exited 0; stock **17 events, changed=0**, selected **17 files / 26 events,
  changed=0**. No full vehicle/prefab refresh is required.
- Hash comparison of 74 protected files found only the two intentionally
  edited runtime C# files changed. **72 were byte-identical**, including all
  68 stock/selected clip and importer-meta files, the canonical Satsuma prefab,
  project AudioManager, user audio settings and the current native save. The
  selected manifest is also byte-identical to its pre-change backup.
- Text comparison of both generated libraries, excluding only the new mix
  fields and explicitly serialized zero clip-calibration defaults, confirmed
  **all pre-existing serialized values unchanged**. Both generated libraries
  remain ignored by Git.

Intermediate failures remain recorded, not silently relabeled as passes:

- Two new fixture meta files initially contained overlong GUIDs and Unity did
  not discover those fixtures. Their GUIDs were corrected before the final
  runs; explicit new-case discovery/passing results are verified above.
- `satsuma-event-mix-production-20260906.xml` failed before playback because
  the official integration intentionally disables Wwise in batch mode. The
  later runs use its existing `-wwiseEnableWithNoGraphics` test flag; no Wwise
  runtime/vendor settings were modified.
- `satsuma-event-mix-play-final-20260906.xml` had 41 passes, one failure, one
  skip: the old production fixture counted every traffic voice sharing the
  same selected coast sample (14), rather than the probe's own source. The
  test now resolves the effective selected clip and the explicit probe origin.
  It retains the exactly-one-source assertion, and both sessions passed in
  the final run. Compressed-in-memory clips are checked as loaded, not read
  through an unsupported `GetData` call; real output evidence is separate.

The initial 58 EditMode / 39 PlayMode passes and the historical 1359/127-test
run do not replace the final results above. Final runs have no new compile
errors or diagnostics on the new files; unrelated pre-existing Unity API
deprecation warnings remain outside this packet.

## Reproduction and files

Use the configured Unity 6000.6.0f1 executable from the repository root. The
actual executed argument sets (output filenames as above) are:

```text
-projectPath <project> -force-d3d11
  -executeMethod MSC.Editor.VehicleSimulation.SatsumaAudioOutputProbe.RunAfterRefreshingLibraries
  -logFile <output log>

-batchmode -nographics -quit -projectPath <project>
  -executeMethod MSC.Editor.VehicleSimulation.SatsumaAudioOutputProbe.RefreshLibrariesBatch
  -logFile <idempotence log>

-batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode
  -testFilter "UnityAudioCalibrationTests;AudioRuntimeEditModeTests;SatsumaEngineFeedbackTests;SatsumaEngineSymptomAudioTests;SatsumaAudioEventMixTests"
  -testResults <EditMode XML> -logFile <EditMode log>

-batchmode -force-d3d11 -wwiseEnableWithNoGraphics -projectPath <project>
  -runTests -testPlatform PlayMode
  -testFilter "ProductionAudioPlaybackRegressionTests;ProductionSatsumaBootstrapPlayModeTests;SatsumaEngineFeedbackPlayModeTests;AudioHybridRoutingPlayModeTests;UnityAudioBackendPlayModeTests;UnityAudioScopedParameterTests;AudioListenerLifecyclePlayModeTests"
  -testResults <PlayMode XML> -logFile <PlayMode log>
```

The device probe deliberately has no `-batchmode`, `-nographics` or `-quit`.
It accepts only its explicit command-line method invocation and is inactive
in ordinary Editor sessions. Its guard/deadline/exit ownership are Editor-only.

Changed project-owned implementation and tests:

- `Assets/Game/Audio/UnityFallback/UnityAudioEventLibrary.cs`
- `Assets/Game/Audio/UnityFallback/UnityAudioBackend.cs`
- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaEngineAudioImporter.cs`
- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1UserSelectedAudioImporter.cs`
- `Assets/Game/LegacyImport/Manifests/Phase1SatsumaEngineAudioManifest.json`
- New `Assets/Game/Editor/VehicleSimulation/SatsumaAudioOutputProbe.cs` and meta.
- `Assets/Game/Tests/EditMode/AudioUnityFallback/UnityAudioCalibrationTests.cs`
- New `Assets/Game/Tests/EditMode/LegacyImport/SatsumaAudioEventMixTests.cs` and meta.
- `Assets/Game/Tests/PlayMode/AudioUnityFallback/AudioHybridRoutingPlayModeTests.cs`
- New `Assets/Game/Tests/PlayMode/VehicleAssembly/SatsumaEngineMixPlayModeTests.cs` and meta.
- `Assets/Game/Tests/PlayMode/VehicleAssembly/ProductionAudioPlaybackRegressionTests.cs`
- This report, the prior `USER_PACK_LOUDNESS_CALIBRATION_2026-09-06.md` status,
  and `DONOR_AUDIT.md`, `PORTING_MATRIX.md`, `SYSTEM_MAP.md`, `PORTING_LEDGER.csv`.

Only the two ignored generated audio libraries were refreshed. No new clip,
prefab, bank, production asset or save was generated. The current scene can
be tested without rerunning either importer.

## Compatibility and ownership

No existing public method, serialized field, stable ID, event, prefab,
controller, save DTO or 08A UI element is removed or renamed. The new field is
an optional presentation-only extension; no save migration is needed.
The scalar balance is `Reimplemented`, the retained donor scalar evidence is
`ConfigurationTransferred`, and all existing private clip payload remains
`TemporaryDirectImport`. Source hashes, replacement keys and ignored content
boundaries remain authoritative. No donor executable, controller or assembly
is introduced. The original game and user pack are read-only.

Next bounded milestone: manual in-game acceptance of this Satsuma event mix at
unchanged master/engine settings, followed by recording the result here.

Manual steps: open `Assets/Game/Bootstrap/Bootstrap.unity` in Unity 6000.6.0f1,
press Play and load the existing slot. Compare a normal start, idle and a short
rev/release from the driving position and beside the engine/exhaust. Listen
to a naturally occurring symptom; do not deliberately alter the working
engine tuning just to manufacture a fault. Check Engine volume at 50% and 0%,
then restore the desired setting. Steps/doors/UI should retain their previous
balance. These are the remaining user listening checks, not an extra import
or setup task. Failure cadence and engine-behavior gaps are unchanged.
