# Satsuma running mix, quieter ambience and starter continuity

Status: **Implemented; live device output, targeted automated regression and
idempotent import passed. Manual in-game listening remains pending.**
Requested on 2026-09-06 UTC (continued after local midnight on 2026-09-07).
This is a bounded Phase 1 / 11A-V1 follow-up, not engine-physics or full donor
parity approval. It supersedes the two running-front-bed settings in
`SATSUMA_EVENT_MIX_REPAIR_2026-09-06.md`; that report remains historical evidence.

## User request and inspected cause

The user requested noticeably louder idle/rev/running audio, somewhat quieter
ambience, investigation/fix of short gaps during starting/catching, then a
normal computer shutdown after completion. Current saved Master and Engine
are already 100%; their values are not being overwritten.

Inspected the actual two libraries, both scoped importers, selected PCM/WAVs,
`SatsumaEngineFeedbackPresenter`, fallback event timing/scheduling, the world
ambient presenter, production routing, current tests and previous device output.

`ffmpeg -af silencedetect=noise=-40dB:d=0.015 -f null NUL` on the original
user-selected float WAVs found:

| Selected file | Full duration | Quiet tail begins | Quiet tail duration |
| --- | ---: | ---: | ---: |
| `motor_start_1.snd.wav` | 0.533651 s | 0.324467 s | 0.209184 s |
| `motor_start_2.snd.wav` | 3.399048 s | 3.002925 s | 0.396122 s |

These are threshold-relative quiet intervals, not a claim that every sample
is digital zero. The presenter waited for the full selected lead-in duration,
including its tail. The loop then repeated its own quiet tail each turn.
This explains two concrete audible-gap mechanisms. Catch is posted on the
actual Cranking-to-Running transition in the same presentation tick as the
running beds; no extra catch-delay timer was found. The catch recording has
its own late decay, but it does not gate or postpone the running beds.

## Changes and ownership

- Coast/idle event drive: +12 to +18 dB, added-boost ceiling .6 to .95.
- Throttle/rev event drive: +10 to +12 dB, ceiling .5 to .6.
- Those two front running beds use a 2 m near field instead of 1 m. They remain
  spatial and logarithmic, on the existing engine-block emitter, max 40 m.
  Donor min1 remains recorded evidence; this is explicit user-requested mix
  tuning, not a transferred donor setting. Exhaust attenuation/gain is unchanged.
- The existing capped-gain calculation still cannot add a floor, unmute a zero
  RTPC, or override user volume. Its authoring range now permits +18 dB drive.
  Combined drive above the former 18 dB ceiling is accepted **only with a
  positive added-boost ceiling**: the resulting gain is bounded by max(original
  calibrated gain, user-scaled ceiling), at most 4x with existing clip limits.
  Uncapped combined gain remains limited to 18 dB. The 8x DSP overflow filter
  is unchanged; no new compressor, bus, plugin or audio dependency was added.
- World natural layers and the existing rare forest chainsaw use a common
  -4 dB presenter trim (0.63095734 linear), through either current backend.
  Phase/weather/distance scheduling and original layer evidence remain intact.
  This does not change rain/thunder, footsteps, UI, music or user settings.
- Exactly two generated selected starter WAV copies opt into deterministic
  preparation. Lead keeps 14377 frames (0.326009 s). Loop keeps 132300 source
  frames with a 221-frame overlap at the seam, producing 132079 output frames
  (2.994989 s). The original downloads and source hashes are unchanged.
- `UserAudioWavePreparation` accepts only the reviewed 44.1 kHz float PCM WAV
  format, fails closed on malformed/range-invalid input, returns original bytes
  for unprepared files, and does not mutate its input. Integer-weighted double
  crossfade avoids Mono/.NET float-rounding differences in generated hashes.
- Manifest records original **and prepared** SHA-256, explicit frame edits and
  evidence. Importer preflights source/mapping/prepared hashes before output
  writes; build validation checks the prepared hash only for those two entries.
  Existing clip GUIDs, replacement keys, event identity and importer settings
  are retained. The other 15 selected WAV files are still byte-for-byte copies.

No simulation, tuning, physical starter catch conditions, DTO, prefab, scene,
08A UI, official Wwise vendor code or native save is changed. Prepared copies
remain ignored private `TemporaryDirectImport`; preparation and scalar balance
are project-owned `Reimplemented` presentation work.

## Executed validation

- Read-only source silence detection and 50 ms source RMS sampling confirmed
  the loop remains active through 2.95 s but falls to approximately -96 dBFS
  by 3.1 s. No source media was altered by these inspections.
- Initial authoring was stopped by prepared-hash validation before any selected
  media write: single-precision seam arithmetic differed between .NET and Mono.
  This was fixed using integer-weighted double arithmetic, then the canonical
  prepared hash was recomputed. Initial failed log is retained as
  `Logs/audio-mix-continuity-author-20260906.log`, exit 1, not counted as passed.
- `Logs/audio-mix-continuity-author-verified-20260906.log`: exit 0, stock
  17 events changed=0 on this repeat; selected 17 files / 26 events changed=3
  (two prepared WAVs and the selected library). The prior attempt had already
  refreshed the stock library (changed=1), before selected-media preflight.
- `Logs/audio-mix-continuity-edit-20260906.xml`: **70/70 passed**, no failures
  or skips. Explicit new-case presence was checked: capped stronger drive,
  nine deterministic WAV preparation cases, authored selected timing/gain,
  world ambient trim and the previous feedback/symptom/runtime controls.
- `Logs/audio-mix-continuity-device-20260906.log` and
  `Logs/satsuma-audio-output-20260906-191140.json`: exit 0, **60 actual device
  measurement stages** (not 60 NUnit tests). Gain/mute controls passed. The
  seven-second starter stage captured 6.976 DSP seconds / 341 blocks across
  two loop seams, with no observed sampled quiet run (threshold peak .005).
  All 24 combined stages remained below the sample clipping rail; maximum
  observed peak .982188. Headroom is small in the conservative 8000-RPM
  coincident setup; no claim is made about fault overlap or full-scene true peak.
- Same-run previous/current stock-chain sum at 2 m, measured RMS dBFS:

  | RPM / pedal | Previous mix | Current mix | Difference |
  | --- | ---: | ---: | ---: |
  | 800 / 0 | -23.75 | -16.84 | +6.92 dB |
  | 1500 / .2 | -22.50 | -15.50 | +7.00 dB |
  | 3000 / .45 | -19.25 | -11.81 | +7.44 dB |
  | 3000 / 1 | -19.87 | -13.07 | +6.80 dB |
  | 6000 / 1 | -17.02 | -11.58 | +5.44 dB |
  | 6000 / 0 | -18.75 | -12.49 | +6.26 dB |
  | 8000 / 0 | -16.50 | -10.13 | +6.38 dB |
  | 8000 / 1 | -14.83 | -9.06 | +5.77 dB |

  Previous definitions use the immediately preceding pass's gain/ceiling/min1
  in memory; current definitions use the authored mix. Same real clips,
  controls and parameter formulas are used. This conservative coincident
  engine/exhaust layout is not the actual driver's head/outlet geometry.
- `Logs/audio-mix-continuity-play-20260906.xml`: **42 passed, zero failed,
  one explicit batch-device skip**, exit 0. The canonical selected-pack test
  now requires the real cranking loop by .38 s, before the old padded .534 s
  lead could finish. It also checks new idle/headroom/min2, user 50% and mute,
  high RPM, emitter following, catch and stop. Two production sessions retained
  the official Wwise backend, six banks and the native thunder event.
  The skipped interactive PCM test is not counted as passed; device PCM was
  independently executed by the successful non-batch probe above.
- `Logs/audio-mix-continuity-idempotent-20260906.log`: exit 0, **changed=0**
  for both scoped importers. No prefab refresh or new save is needed.
- **71 of 73 protected files are byte-identical** after the whole run; the
  only differences are the two deliberately prepared generated WAVs. All
  34 selected/stock clip meta files, all 17 stock clips, 15 other selected
  clips, the canonical prefab, feedback presenter, project AudioManager,
  saved user settings and current native save are unchanged. All 35 original
  user-pack files were rehashed against the manifest: zero mismatches.
- No new compile errors. Existing unrelated Unity 6.6 deprecation warnings
  remain outside scope. No full-suite or full-game parity pass is claimed.

The expanded explicit non-batch device probe measures the actual listener PCM,
gain/mute controls, previous-vs-current combined mix at 2 m for eight operating
points, close coincident-layer headroom, and a seven-second prepared starter
loop spanning two seams. Its previous-balance copies exist only in memory.
The loop check rejects a repeated sampled quiet run of 80 ms or longer after
startup. Device output is not LUFS, speaker SPL, full-scene true peak, human
listening or the physical cranking/combustion comparison gate.

## Files and reproduction

Project-owned modified files:

- `Assets/Game/Audio/UnityFallback/UnityAudioEventLibrary.cs`
- `Assets/Game/Audio/Composition/WorldAmbientAudioPresenter.cs`
- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaEngineAudioImporter.cs`
- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1UserSelectedAudioImporter.cs`
- Both corresponding Satsuma-engine and user-selected audio manifests.
- New `UserAudioWavePreparation.cs` and meta beside the scoped importers.
- `Assets/Game/Editor/VehicleSimulation/SatsumaAudioOutputProbe.cs`
- `Assets/Game/Tests/EditMode/AudioUnityFallback/UnityAudioCalibrationTests.cs`
- `Assets/Game/Tests/EditMode/AudioRuntime/AudioRuntimeEditModeTests.cs`
- `Assets/Game/Tests/EditMode/LegacyImport/SatsumaAudioEventMixTests.cs`
- New `UserAudioWavePreparationTests.cs` and meta in that test directory.
- `Assets/Game/Tests/PlayMode/VehicleAssembly/SatsumaEngineMixPlayModeTests.cs`
- This report, prior report's follow-up notice, and four porting records.

The only generated runtime payload changes are the two starter WAV copies
and two scoped event libraries. Use configured Unity **6000.6.0f1**, not 6000.3:

```text
-batchmode -nographics -quit -projectPath <project>
  -executeMethod MSC.Editor.VehicleSimulation.SatsumaAudioOutputProbe.RefreshLibrariesBatch
  -logFile <author log>

-batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode
  -testFilter "UnityAudioCalibrationTests;AudioRuntimeEditModeTests;SatsumaEngineFeedbackTests;SatsumaEngineSymptomAudioTests;SatsumaAudioEventMixTests;UserAudioWavePreparationTests"
  -testResults <Edit XML> -logFile <Edit log>

-projectPath <project> -force-d3d11
  -executeMethod MSC.Editor.VehicleSimulation.SatsumaAudioOutputProbe.Run
  -logFile <device log>

-batchmode -force-d3d11 -wwiseEnableWithNoGraphics -projectPath <project>
  -runTests -testPlatform PlayMode
  -testFilter "ProductionAudioPlaybackRegressionTests;ProductionSatsumaBootstrapPlayModeTests;SatsumaEngineFeedbackPlayModeTests;AudioHybridRoutingPlayModeTests;UnityAudioBackendPlayModeTests;UnityAudioScopedParameterTests;AudioListenerLifecyclePlayModeTests"
  -testResults <Play XML> -logFile <Play log>
```

No importer rerun is needed for the already refreshed checkout. Safe recovery
copies are under ignored `Logs/audio-mix-continuity-before-20260906/`; original
selected media remains the canonical source. No public-distribution permission
or new sound-pack rights are asserted.

Next bounded milestone: manual listening acceptance in Bootstrap on the same
saved volume settings, from the driver seat and next to the engine: start,
idle, brief rev/release and ambient balance. No mechanical tuning changes or
new save are required for that comparison. Old engine-parity gaps remain open.
