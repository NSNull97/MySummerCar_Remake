# Existing game audio repair — 2026-09-06

Status: ImplementedAutomatedRepairVerified / ManualListeningPending.
This closes the confirmed existing routing/lifecycle defects, not all missing
Phase 1 audio content. No Phase 2 or full-game audio-completeness approval.

## Confirmed failure and bounded compatible plan

The accepted router chooses a ready backend for the entire session. Private
Phase 1 supplemental and override clips are loaded into Unity Audio, while
several callers post their stable IDs through the router to Wwise. Wwise
readiness does not imply that these events exist in its authoring or banks.
The Satsuma engine's eight events, its six scoped parameters and handbrake
events are examples. Four body-impact mappings additionally name events absent
from the current Wwise project and generated vehicle bank. Regenerating the
old bank authoring script alone cannot repair absent authoring.

Extend, do not replace, the existing boundary with an optional explicit-content
routing capability. A backend may claim only events in its explicitly loaded
supplemental/override libraries and their parameter bindings. The router chooses
that route before posting, preserving the primary Wwise route for other events.
There is no speculative double post, generic substitute, global Wwise disable,
or retry of every rejected event through an unrelated sound.

Both participating backends must receive listener context, settings, emitter
registration, stop and scene/session cleanup. Event handles remain owned by the
backend that actually started them. Scoped gain/pitch parameters follow their
explicit content route. Diagnostics must preserve rejected-event information
even when the selected audio engine itself remains ready.

No existing stable IDs, serialized fields, scene identity, vehicle assembly or
save DTOs are replaced. Existing backends without the optional capability retain
the previous single-route behavior. No user save or donor installation changes
are required. Generated media/banks remain ignored TemporaryDirectImport.

## Verification required before completion

- Ready preferred backend plus explicit Unity engine/assembly media: real voices,
  gain/pitch and one playback only; missing content remains a visible failure.
- Settings/focus/stop and scene/session lifetime reach both routes.
- Listener initialization works with both active and initially inactive players.
- Map/authoring/generated-bank mismatch is detected before treating a name or
  valid-looking handle as proof of playable media.
- Production Bootstrap, new session and load/return lifecycle checks; manual
  listening remains explicitly separate from headless automated verification.

Content with no existing clip, producer or Wwise Play action is recorded as a
separate gap, not claimed repaired by routing alone.

## Implemented repair

- Optional `IAudioExplicitContentBackend` claims only loaded supplemental and
  override events and their parameter bindings. The main generic Unity library
  does not hijack working Wwise events. Unknown events are not substituted.
- Router emitter registration, listener/environment context, settings, focus,
  stop, scene unload and session teardown reach both participating engines.
  Handles keep their real backend ownership. Secondary registration failures
  are returned, not silently accepted. A late secondary does not double-register
  a new emitter. Once-set controls are replayed after backend readiness changes.
- Satsuma's eight engine events, six scoped gain/pitch parameters, ten assembly/
  impact/handbrake definitions use the real existing private clip libraries even
  while Wwise is ready. The four missing Wwise impact names are not posted there.
- Active-player listener binding no longer fails in `Awake` before composition
  can configure it. Validation still rejects genuinely missing configuration.
- Engine presentation stops its own audio on pause and resumes continuous beds
  without replaying ignition/catch gestures. Generic non-UI Unity voices retain
  their handle and sample position through pause; UI-category voices continue.
  Story-traffic presentation also stops advancing its timers during pause.
- The existing lighting-switch event now loads one hash-pinned donor-derived
  Effects one-shot through a project-owned supplemental library. Clip and scene
  hashes are preflighted; no original AudioSource, controller or FSM is imported.
- Bank tooling compares mapped names with actual binary HIRC event IDs and
  generated metadata. It explicitly reports missing/empty content instead of
  treating a map entry, ready engine or copied bank hash as playback proof.

Two test-fixture compatibility defects were also repaired without changing game
save validation or vehicle mechanics. The old player-voice Bootstrap test now
releases its session and restores time scale. The M06 audio playtest initializer
now populates the current fastener-group latch DTO consistently with its fully
installed synthetic fixture; contradictory user-save DTOs remain rejected.

## Executed verification

Unity **6000.3.11f1**, installed official Wwise integration. Every Unity process
was launched only after the prior process had ended; the user's Editor was
closed first. Final results have no ignored tests:

| Check | Executed result | Local evidence |
|---|---|---|
| Focused EditMode: audio, bank validator, engine feedback, handbrake, lighting import, prototype latch compatibility | **87/87 passed**, 0 failed/skipped | `Logs/AudioRepair-EditMode-Final-20260906.xml` |
| Combined PlayMode: audio suites, ten hybrid real-Unity tests, engine pause, traffic, real production playback, native reload, authored M08 playtest | **49/49 passed**, 0 failed/skipped | `Logs/AudioRepair-PlayMode-Final-20260906.xml` |
| Real production composition, separately executed | **1/1 passed**, two sessions, 19 explicit events per session | `Logs/AudioRepair-Production2-20260906.xml` |
| Native Bootstrap save/reload, separately executed | **1/1 passed**, generated GUID test slot subsequently removed | `Logs/AudioRepair-NativeReload-20260906.xml` |
| Lighting importer | First successful generation: 1 clip / 1 event; repeat **changed=0**, exit 0; sources hash-verified | `Logs/AudioRepair-LightingImport2-20260906.log`, `Logs/AudioRepair-LightingImport-Idempotent-20260906.log` |
| Source-bank audit | 69 raw routes / 22 raw gaps; 71 explicit Unity IDs (8 also mapped in Wwise); 61 remaining Wwise routes / 14 known unresolved content gaps / **0 unexpected failures** | `Logs/AudioRepair-BankAudit-20260906.log` |

Production playback used no substituted backend: the preferred engine was real
Wwise with six loaded banks and exactly one enabled Unity listener. Each of the
38 explicit posts resolved to exactly one playing Unity source with the correct
clip, positive volume/pitch and nonzero decoded PCM. Native Wwise thunder also
returned a live playing handle in each session. No `Event ID not found` occurred
in these production checks. This is stronger than a mocked-handle test, but is
not a physical-output recording or human acoustic/mix approval.

The final PlayMode invocation used a graphics device, `-wwiseEnableWithNoGraphics`
and process-local `MSC_REQUIRE_LIVE_WWISE=1`. The official integration suppresses
its engine in **any** batch run without that flag, even without `-nographics`.
An initial production attempt without it failed honestly and was rerun with the
documented integration opt-in; it was not counted as a pass. Earlier runs also
caught a validator exception-filter mistake and the two fixture issues above.
The paused-time-stalled first PlayMode batch was terminated by its exact owned
PID; no user Editor process was terminated. All failures were followed by the
passing combined final suites, not hidden through test exclusions.

Commands executed from the repository (each test run also specified the evidence
`-testResults`/`-logFile` paths above):

```text
Unity.exe -batchmode -nographics -projectPath <repo> -executeMethod MSC.LegacyImport.Editor.GameplayPresentation.Phase1LightingSwitchAudioImporter.Build -quit
Unity.exe -batchmode -nographics -projectPath <repo> -executeMethod MSC.Audio.Wwise.Editor.WwiseProductionBankAudit.RunSourceBankAudit -quit
Unity.exe -batchmode -nographics -projectPath <repo> -runTests -testPlatform EditMode -testFilter "MSC.Tests.EditMode.Audio;MSC.Tests.EditMode.LegacyImport.LightingSwitchAudioContentTests;MSC.Tests.EditMode.VehicleAssembly.SatsumaEngineFeedbackTests;MSC.Tests.EditMode.VehicleAssembly.SatsumaHandbrakeAudioTests;MSC.Tests.EditMode.VehicleSimulation.PrototypeAssemblyStateInitializerTests"
Unity.exe -batchmode -wwiseEnableWithNoGraphics -projectPath <repo> -runTests -testPlatform PlayMode -testFilter "MSC.Tests.PlayMode.Audio;MSC.Tests.PlayMode.VehicleAssembly.SatsumaEngineFeedbackPlayModeTests;MSC.NPC.Tests.PlayMode.StoryTrafficVehicleAudioPresenterPlayModeTests;MSC.Tests.PlayMode.VehicleAssembly.ProductionAudioPlaybackRegressionTests;MSC.Save.Integration.Tests.PlayMode.NativeSaveBootstrapLoadPlayModeTests.PendingNativeSave_ReloadsBootstrapAndRestoresWithoutNewGameClick"
git diff --check
git check-ignore <generated lighting clip and Resources library>
Get-FileHash <native slot-01 current.save.json>
```

The post-close user slot-01 SHA-256 remained
`750AC0AC13B707BEDC526EB3C940A19F636A5DB217A9225CFEB8F32A1A86A724` after
the production and native-reload checks. No user save, donor installation,
canonical extraction, existing bank payload or Wwise vendor source was edited.
No bank regeneration, commits, broad asset rebuild or save migration was needed.

## Files in this repair packet

Runtime/composition modifications, relative to `Assets/Game`:

```text
Audio/Runtime/IAudioBackend.cs
Audio/Runtime/AudioBackendRouter.cs
Audio/Runtime/AudioListenerContextPresenter.cs
Audio/UnityFallback/UnityAudioBackend.cs
Bootstrap/ProductionWorldStreamingInstaller.cs
NPC/Runtime/StoryTrafficVehicleAudioPresenter.cs
Vehicle/Runtime/SatsumaEngineFeedbackPresenter.cs
Vehicle/Runtime/PrototypeAssemblyStateInitializer.cs
```

New Editor/data files (and new Unity `.meta` files):

```text
Audio/Wwise/Editor/WwiseBankContentValidator.cs
Audio/Wwise/Editor/WwiseProductionBankAudit.cs
LegacyImport/Editor/GameplayPresentation/Phase1LightingSwitchAudioImporter.cs
LegacyImport/Manifests/Phase1LightingSwitchAudioManifest.json
```

`Audio/Wwise/Editor/WwiseBankSynchronizer.cs` gains an optional strict overload;
the established copy menu remains compatible. New tests:

```text
Tests/EditMode/AudioWwiseEditor/ (validator fixture and assembly definition)
Tests/EditMode/LegacyImport/LightingSwitchAudioContentTests.cs
Tests/EditMode/VehicleSimulation/PrototypeAssemblyStateInitializerTests.cs
Tests/PlayMode/AudioRuntime/AudioListenerLifecyclePlayModeTests.cs
Tests/PlayMode/AudioUnityFallback/AudioHybridRoutingPlayModeTests.cs
Tests/PlayMode/VehicleAssembly/SatsumaEngineFeedbackPausePlayModeTests.cs
Tests/PlayMode/VehicleAssembly/ProductionAudioPlaybackRegressionTests.cs
```

Existing `PlayerVoiceReactionControllerTests.cs` gains fixture cleanup;
`SatsumaEngineFeedbackPlayModeTests.cs` becomes partial for the added pause tests.
Documentation: this report, `AUDIO_COVERAGE_AUDIT_2026-09-06.md`,
`WWISE_BANK_AUDIT_2026-09-06.md`, plus the four required Porting audit/ledger/map
documents. Generated `RuntimeBaseline/Audio/LightingSwitch` clip/library payloads
are Git-ignored `TemporaryDirectImport`; they are not production-authored media.
Unrelated pre-existing dirty changes remain untouched.

## Remaining content and manual boundary

The 14 unresolved Wwise entries are explicitly listed in the bank audit. They
include five reserved UI events and nine generic vehicle/world/tool hooks with
no generated playback media. Some hooks also have no production producer. The
full inventory additionally records absent car-door/panel/wiper/radio and other
not-yet-implemented feedback; this repair does not invent them or call them
ported. The resulting seven Unity libraries contain **127 unique IDs and 74
unique referenced clips**, not proof of full donor-feature audio parity.

No additional Editor wiring is required on this machine. To regenerate the new
private clip on a prepared checkout, use
`Tools/MSC Remake/Phase 1/Audio/Build Lighting Switch Audio Only`.
The read-only audit is
`Tools/MSC Remake/Audio/Audit Production Source Banks (Read Only)`.
Do not blindly run the old `ConfigureWwisePrototype.ps1`: it can overwrite later
authoring/inclusion choices. Manual listening remains required for spatial mix,
actual device output, and the user's mechanically valid car state.

Compatibility: accepted 00–08A systems, stable IDs, serialized fields, scenes,
prefabs, save schemas and UI presentation remain in place. Only compatible
audio routing/lifecycle extensions and diagnostic fixture DTO construction were
changed. No migration and no Phase 2 work.

## Unity 6.6 migration handoff

After these checks, the parent task reported the user's new request to migrate
the shared project to installed Unity 6.6. All audio-owned Unity processes had
already ended; the last importer process (PID 6456) exited 0 with changed=0.
No further 6000.3 run, bank generation or Packages/ProjectSettings change will
be made by this audio task. The results above belong to **6000.3.11f1**, not to
the forthcoming 6.6 configuration. Exact new patch/package validation belongs
to the migration task; this report does not certify that untested combination.

Exactly one next milestone: **post-migration audio revalidation and acceptance**.
Repeat the focused suites after the migration owner confirms the new patch;
then manually hear ignition/starter/running engine, handbrake/assembly/impacts,
light switch, pause/resume, volume/focus and return/load in the normal session.
