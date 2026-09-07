# User-selected audio — relative loudness repair

Status: the historical implementation and automated routed-voice regression passed;
the user's subsequent Satsuma listening test rejected the resulting engine/start/pop
balance. That bounded follow-up is tracked in `SATSUMA_EVENT_MIX_REPAIR_2026-09-06.md`;
the measured clip corrections below remain separate and are not discarded. This is temporary private Phase 1
content calibration, not an engine/speaker loudness standard or donor parity claim.

## Measured cause

The user's report was tested against **actual Unity-imported PCM**, not only the
original stereo WAV files. A read-only audit made 37 temporary importer copies,
retained each source's codec/quality/mono-normalization settings and changed only
its load policy to enable PCM readback. It measured all 26 replacement mappings,
exported 37 mono float-WAV readbacks into ignored Logs, removed its own GUID-named
temporary asset folder, and checked 88 protected source/library/manifest files
were byte-identical. It did not reimport the original assets in place.

Executed audit: `Logs/user-audio-imported-pcm-audit-20260906.xml`, **1/1 passed**,
24.439 s. Reports: `Logs/user-audio-loudness-20260906-154442/imported-pcm-report.json`
and `imported-ebur128-report.json`. Installed local FFmpeg's `loudnorm` analysis
was run on the readbacks without writing processed audio. Very short original
clips may return undefined integrated LUFS; undefined is not silence.

The replacement starter bed is 5.5 dB lower in integrated LUFS (3.35 dB lower in
plain RMS), and the coast bed is about 5.3 dB lower by both measurements. Some
other replacements are louder, including the throttle bed and handbrake clicks.
Thirty/forty-second voice tails make whole-file RMS misleading: swear06's active
sound is actually louder. Imported sample peaks are already near 0 dBFS, so a
blanket boost is not appropriate.

## Bounded implementation

`UnityAudioEventDefinition.calibrationGainDb` is a new optional -12..+12 dB
content field. Zero preserves the exact previous path. The user-selected manifest
sets it explicitly for the 15 quiet mappings below; the remaining 11 keep zero.
The importer validates and compares calibration, preserving all existing event
IDs, category/volume/RTPC/rolloff settings, GUIDs and original clip bytes.

| Existing event | Correction | Basis |
| --- | ---: | --- |
| Satsuma starter loop | +5.5 dB | Integrated imported LUFS |
| Satsuma engine catch | +2.2 dB | Conservative RMS correction; transient shape differs |
| Satsuma coast loop | +5.3 dB | Imported RMS and LUFS agree |
| Satsuma exhaust bed | +1.9 dB | Imported LUFS, RMS approximately +1.7 dB |
| Body low impacts 01 / 02 | +6.0 / +6.1 dB | Active 100 ms windows; excludes long quiet tails |
| Body high impacts 01 / 02 | +8.7 / +2.8 dB | Active 100 ms windows, not whole-file duration |
| Swear07 / swear11 | +1.5 / +4.5 dB | Integrated LUFS; swear06 is not raised |
| Jani / Petteri engine | +8.1 / +5.3 dB | Active imported RMS |
| Jani / Petteri crash | +8.7 dB each | Same selected transient as body high 01 |
| Generic tire roll | +2.1 dB | Imported RMS; louder synthetic-template replacements unchanged |

The backend first retains its existing mixed 0..1 level and then applies the
explicit correction. At ordinary RPM/request levels the AudioSource still has
headroom and receives linear gain. Only the amount exceeding its volume ceiling
goes through `UnityAudioCalibrationFilter`: linear below a .8 sample knee,
continuous symmetric soft ceiling at .95. This avoids simply discarding gain at
`AudioSource.volume == 1`. The filter is allocation-free, is bypassed for all
uncalibrated voices, and resets when a pooled voice stops or changes event.
Master/category/focus mute and scoped RTPC silence remain authoritative.

This is **per-voice sample-peak protection**, not a true-peak limiter on the final
sum of all voices. The existing dialogue stage and project +4 dB mix are not
retuned. Very loud transient peaks are intentionally softened; unchanged quiet
tails remain in the user's source clips. Manual listening must judge timbre and
relative mix in the full scene, not only numeric equality.

## Executed checks so far

- Scoped `Phase1UserSelectedAudioImporter.Build`, Unity 6000.6.0f1: exit 0,
  17 source clips / 26 events; only selected library calibration regenerated.
  `Logs/user-audio-calibration-author-20260906.log`.
- Real Unity fallback/hybrid routing/RTPC PlayMode suite: **25/25 passed**, no
  failures/skips, 1.183 s, exit 0. Includes corrected low-level source gain,
  overflow stage, scoped/master mute and exact pool reset.
  `Logs/user-audio-calibration-play-20260906.xml`.
- Pure calibration EditMode checks: **2/2 passed**. Exact old-path bypass,
  finite range validation, small-signal linear gain, continuous knee and 10,001
  symmetric monotonic bounded samples. The combined run's unrelated new native
  graphics fixture failed because it used a PlayMode scene API in EditMode;
  that fixture is being corrected and is not counted as a passing graphics test.
  `Logs/native-headlights-calibration-edit-20260906.xml`.

No save migration, source-pack write, donor installation change, bank rebuild,
new dependency or 08A UI change is involved.

## Actual-output probe: explicit remaining boundary

A separate PlayMode probe attempts to compare actual Unity `AudioSource`
PCM output for a known sine at zero versus +6 dB calibration, then checks
master mute. It does not mistake a configured volume property for rendered
audio. This machine's automated Editor sessions did not provide usable output:

- `Logs/audio-pcm-cabin-final-20260906.xml`: 33 total, 32 passed, one failed.
  The probe's uncorrected baseline returned zero samples through
  `AudioSource.GetOutputData`; all seven real cabin input checks passed.
- `Logs/audio-offline-pcm-cabin-final-20260906.xml`: 22 total, 21 passed, one
  failed. The alternate offline `AudioRenderer` started but reported zero
  available sample frames. This was not counted as a successful output test.
- `Logs/audio-editor-pcm-final-20260906.log`: a non-batch command-line runner
  exited 3 with `RunErrorNoCallbacks`, without a result XML. No output
  assertion passed, no scene was saved, and this did not modify vendor code.

The final test keeps ordinary realtime PCM readback and is explicitly tagged
`RequiresRealtimeAudioDevice`. It ignores batch mode with the specific device
limitation above; it remains executable in the interactive Editor through
**Window > General > Test Runner > PlayMode >
AudioHybridRoutingPlayModeTests >
UserSelection_CalibrationIsPresentInActualUnityPcmReadback**. The unused
offline-renderer experiment and its temporary dependency were removed.

Consequently, imported clip PCM, gain calculation, bounded sample processing,
real routed voice configuration, mute and pool lifecycle have executed tests.
**The complete audio-device DSP chain, speaker output, perceived loudness and
final mixed true peaks remain unverified.** Manual comparison should cover
starter/catch, idle/throttle/coast, front versus exhaust position, and the
adjusted collision/voice sounds at unchanged master/category settings.

## Final combined evidence

`Logs/live-engine-complete-edit-20260906.xml`: **1359 passed, zero failed,
one unrelated historical opt-in skip**, including both pure calibration tests
and a fresh actual imported-PCM audit. The audit again verified all 88 protected
files were unchanged.

`Logs/live-engine-complete-play-20260906.xml`: **127 passed, zero failed,
three explicit skips**. The real gain/mute/pool/hybrid tests passed; skips are
the realtime audio-device probe described above and two unrelated historical
wiring-snapshot tests. `Logs/live-engine-complete-bootstrap-20260906.xml`:
**3/3 passed**, including actual production startup and both native reloads.
None of these results is a replacement for the manual listening boundary.

Final fresh `Phase1UserSelectedAudioImporter.Build`:
`Logs/live-engine-audio-idempotent-final-20260906.log`, exit 0,
17 files / 26 events / 18 unmatched / **changed=0**. The selected audio
payload/library/meta and canonical prefab/meta (41 protected files altogether)
are byte-identical across the final audio and lighting refreshes. No further
manual import step is required for this authored local checkout.
