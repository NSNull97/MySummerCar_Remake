# Satsuma live-engine pass — first audio batch

Status: implemented, targeted automated checks passed; full live-engine pass and
manual listening/visual acceptance remain in progress.

## User-selected content

The 2026-09-06 user selection contains 35 audio files. The checked-in
`Phase1UserSelectedAudioManifest.json` records all names, relative paths, SHA-256
hashes, explicit event mappings and 18 unmatched files. Seventeen unique files
replace 26 existing events: Satsuma cranking/catch/engine/exhaust, body impacts,
handbrake, three existing player swear slots, Jani/Petteri engine/crash, and
generic vehicle engine/transmission/tire-roll mappings. A generic event mapping
does not assert that its production gameplay producer is complete. No unrelated
door, drink or NPC event was substituted to consume an unmatched file.

The importer reads ignored `Config/UserAudioPack.local.json`, preflights every
source hash and template mapping, copies only selected media into ignored
`RuntimeBaseline/Audio/UserSelected`, and preserves event settings/parameter
bindings. Source bytes are unchanged. Unity imports point-source mono Vorbis at
full quality from the retained WAV bytes. No supplied executable is run.
Rights were not independently verified: this selection is private local
`TemporaryDirectImport`, not production or public-distribution content.

One optional explicit replacement library precedes the existing libraries;
ordinary duplicate-ID checks remain intact. Routing chooses exactly one backend
before posting even when Wwise is ready. No banks or original media were changed.
The optional event-duration boundary schedules the starter bed after the selected
lead-in (about 0.534 s), not the former donor constant (0.316 s).

## Corrected defects and compatibility

- Per-event logarithmic attenuation is applied to reviewed Satsuma engine and
  exhaust beds. Existing libraries default to linear. Every pooled voice resets
  rolloff before use; emitters retain their actual block/outlet references.
- Starter RPM limiting no longer creates a false `Stalled` transition while the
  key and circuit remain engaged. Failed-start rundown cannot arm engine/exhaust
  beds. Genuine running-engine rundown is retained.
- Simulation schema 1 gains optional `hasCombustionHistory` and
  `combustionRundownActive`. New saves distinguish failed versus genuine rundown.
  Legacy Off/Stalled rotating records lack that evidence, so their previous
  rundown presentation is preserved once. No existing identity/schema is renamed.
- The exact audited missing belt texture is repaired in place, retaining the
  material GUID and two-bone mesh bindings. Unexpected material drift still fails.

## Executed checks

Unity `6000.6.0f1` scoped `SatsumaLiveEnginePassAuthoring.BuildAudioAndBelt` exited
0: 8 original engine mappings refreshed, 17 user clips/26 replacement mappings
generated, canonical belt texture repaired. Log:
`Logs/live-engine-audio-belt-author-20260906.log`.

Focused EditMode: **61/61 passed**, no failures/skips, exit 0:
`Logs/live-engine-initial-edit-20260906.xml`.

Initial PlayMode: 17/19 passed; two test-fixture errors were retained in the log
and corrected (invalid-library helper rejected the fixture before the tested
call; a rundown sequence reintroduced RPM after zero without a fresh run).
Final D3D11 PlayMode: **20/20 passed**, no failures/skips, exit 0:
`Logs/live-engine-audio-play-fixed-20260906.xml`. Checks include real Unity voice
selection, pooled rolloff reset, invalid/single-selection boundaries, lifecycle,
pause/restore, and failed versus genuine rundown. These are not human-audibility
or complete donor-parity claims. Broad regressions, content idempotence and
auditory/visual review follow the remaining operating/fluid/motion batches.

The later causal-symptom batch adds nine stock-source events and two explicit
front emitters without changing the 26 pack replacements. It passed 67/67
EditMode and 22/22 PlayMode checks; details and calibration limits are in
`SATSUMA_ENGINE_SYMPTOMS_2026-09-06.md`. The user's subsequent low-volume report
was not corrected by those routing/lifecycle checks alone. The subsequent
measured correction and its explicit realtime-output limitation are now
recorded in `USER_PACK_LOUDNESS_CALIBRATION_2026-09-06.md`; final combined
regression is recorded in the live-engine integration report.
