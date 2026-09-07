# Wwise bank and authoring audit — 2026-09-06

Status: **ConfigurationInspected / EditorValidationExecuted / BoundedRuntimePlaybackTested**.
This is not a claim of human listening or complete audio-content verification.
The initial inspection did not launch Unity or generate banks. The later
root-coordinated Unity runs are recorded below; this worker did not change user
saves, vendor files or Wwise authoring.

## Confirmed cause of the reported impact errors

`AudioEventMap.asset` contains 69 distinct Wwise event names. The actual authored
`Events/Default Work Unit.wwu` and generated Windows banks contain the same 65
events. Exactly these four mapped names are absent from both authoring and banks:

| Backend event | Reported Wwise ID |
|---|---:|
| `Play_MSC_Vehicle_Body_Impact_Low_01` | 381304523 |
| `Play_MSC_Vehicle_Body_Impact_Low_02` | 381304520 |
| `Play_MSC_Vehicle_Body_Impact_High_01` | 2553075639 |
| `Play_MSC_Vehicle_Body_Impact_High_02` | 2553075636 |

The map is authored in
`Assets/Game/Audio/Wwise/Editor/ProductionAudioAuthoring.cs`, lines 188–191;
serialized entries are in `Assets/Game/Audio/Content/AudioEventMap.asset`,
lines 289–308. The map does not create Wwise Event objects or bank content.

The captured Editor.log contains 50 failed impact posts and 50 corresponding
Event-ID-not-found messages: 21 Low01, 23 Low02, 3 High01, 3 High02. This sample
does not contain a failed-bank-load or missing-bank-file message. It also has one
`AkInitializer is null` warning and three successful engine initialization
messages; the warning alone does not establish a persistent initialization fault.
The log is mutable, so counts describe the inspected snapshot, not future runs.

Regenerating the unchanged Wwise project cannot create these missing events.
They require explicit presentation routing to their existing Unity library, or
reviewed Wwise authoring plus bank inclusion and regeneration. Do not substitute
an unrelated impact clip merely to suppress the error.

## Versions and effective paths

| Component | Inspected value |
|---|---|
| Unity | 6000.3.11f1, revision 3000ef702840 |
| Official Wwise SDK | 2025.1.9 build 9197 |
| Official Unity bundle | 2025.1.9.4241, integration version 21 |
| Project | `MySummerCar_Remake_WwiseProject/MySummerCar_Remake_WwiseProject.wproj` |
| Source banks | `MySummerCar_Remake_WwiseProject/GeneratedSoundBanks/Windows/` |
| Player/build mirror | `Assets/StreamingAssets/Audio/GeneratedSoundBanks/Windows/` |

`Assets/WwiseSettings.xml` resolves project paths relative to Unity `Assets`,
with copy-on-build enabled and generate-on-build disabled. Source and runtime
paths are project-local and already ignored.

The source directory contains all six banks. The runtime mirror directory is
currently empty. This does **not** explain the Editor impact failures:
official `AkBasePathGetter.GetPlatformBasePath()` first uses the Wwise project
bank output in the Editor, not the build mirror. See the installed official
`Assets/Wwise/API/Runtime/Handwritten/Common/AkBasePathGetter.cs`, lines 117–139
and 216–280. The player uses its packaged StreamingAssets path.

The documented official post-build cleanup can empty the mirror; inspect actual
packaged banks for build validation rather than treating the empty Editor copy
as proof of a broken sound engine. The existing project-owned
`WwiseBankSynchronizer.SynchronizeWindowsBanks()` delegates the copy to the
official integration and verifies six source/destination SHA-256 pairs.

## Current source bank inventory

All six SHA-256 values still match the recorded 2026-08-02 baseline in
`WWISE_SETUP.md`; no source-bank corruption or source/metadata authoring drift
was detected in this read-only comparison.

| Bank | Bytes | Events | Embedded media |
|---|---:|---:|---:|
| Init | 1,877 | 0 | 0 |
| MSC_Interaction | 1,179,302 | 19 | See generated metadata |
| MSC_UI | 145 | 5 | 0 |
| MSC_Vehicle | 1,992,969 | 14 | 8 |
| MSC_Weather | 12,554,129 | 7 | See generated metadata |
| MSC_World | 93,600,920 | 20 | See generated metadata |

Non-UI banks were last generated on 2026-08-02 at 14:52 local; UI is the empty
reservation from 2026-07-18. Authoring contains 65 Event objects, all present in
generated metadata. Parameter mapping/authored/generated sets are **32/32/32**
with no missing mapped parameter. This excludes the new Satsuma engine parameters
that are not in the serialized parameter map in the first place.

### Silent authored placeholders

Eighteen authored events have **no Action children**, not merely absent direct
MediaRefs. They are not audible just because posting returns a valid handle:

- Vehicle: transmission loop, body rattle, suspension impact, tire skid.
- World: garage room tone, interior room tone, distant traffic.
- Interaction: tool use; fastener insert/tighten/loosen; part install/remove.
- UI: navigate, confirm, cancel, save feedback, load feedback.

These must be distinguished from legitimate Stop events, whose lack of media is
expected, and the footstep Switch Container, whose media is conditional rather
than a top-level `MediaRefs` list. Current Unity presentation libraries and
producer bindings need to be checked before deciding which placeholders are
intentional deferred hooks and which currently swallow implemented feedback.

## Authoring tool hazard

`Tools/Audio/ConfigureWwisePrototype.ps1` is a full legacy M08 authoring recipe,
not a harmless generic bank rebuild command:

1. Its hard-coded event list still has 65 old names and none of the four impacts.
2. Lines 760–766 delete all existing Action children for entries with `Action=0`.
   Blindly rerunning this script after adding content to such events erases the
   event actions again.
3. Lines 771–785 use `soundbank.setInclusions` with `operation='replace'` and only
   that old event list. Independently added events can disappear from bank
   inclusions, even if their Event objects survive.
4. The recipe may reconvert donor staging audio and clears the audio-file cache.
   Do not invoke it while staging must remain read-only or without an explicit
   review of those writes and current content scope.

## Smallest safe implementation/validation sequence

1. Preserve all stable IDs and accepted sound libraries. Make backend ownership
   explicit for temporary Unity-only content; do not send mapped-but-unauthored
   impact events to Wwise. Root task owns runtime route changes.
2. Add coverage validation joining declared route, event map, authored action
   presence and generated bank metadata. A ready bank is not proof that every
   name exists or produces audio. For intentionally deferred hooks, record that
   status explicitly instead of treating a zero-action Event as verified audio.
3. If Wwise authoring changes are chosen, first make the recipe preserve new
   reviewed actions/inclusions or extend its authoritative specifications.
   Otherwise no generation is needed to repair the four existing Unity routes.
4. After the user releases Unity, synchronize the ignored bank mirror through
   the existing official-copy wrapper. Check packaged copies separately.
5. Run production Bootstrap with actual Wwise, generated source banks and actual
   fallback voices; test both known missing event and incomplete bank cases,
   scene return/restart, listener ownership and user category volumes. Verify
   voice/media or output evidence, not only PostEvent handles or clip presence.

The installed console help was read successfully, without generating anything:

```powershell
& 'C:/Audiokinetic/Wwise_2025.1.9.9197/Authoring/x64/Release/bin/WwiseConsole.exe' generate-soundbank --help
```

When generation is actually required after reviewed authoring, the verified CLI
syntax supports this bounded Windows generation (not executed in this audit):

```powershell
& 'C:/Audiokinetic/Wwise_2025.1.9.9197/Authoring/x64/Release/bin/WwiseConsole.exe' generate-soundbank 'E:/GAYmDev_Studio/MySummerCar_Remake/MySummerCar_Remake_WwiseProject/MySummerCar_Remake_WwiseProject.wproj' --platform Windows --bank MSC_Vehicle MSC_Weather MSC_World MSC_Interaction MSC_UI --skip-languages --no-source-control --abort-on-load-issues
```

Do not add `--save`, cache deletion, project migration, or a new bank layout
without a demonstrated need. The CLI path here is local tooling evidence only,
never runtime source. Then use Unity menu
`Tools/MSC Remake/Audio/Synchronize Windows SoundBanks`, or the existing static
entry point `MSC.Audio.Wwise.Editor.WwiseBankSynchronizer.SynchronizeWindowsBanks`
in the single coordinated Unity validation run.

## Limitations / compatibility

The initial read-only inspection did not attach to the live SoundEngine or test
builds. It proves the on-disk map/authoring/bank discrepancy and authoring-tool
hazards. The subsequent bounded production playback test below adds real
SoundEngine/Unity-voice evidence, but neither stage verifies the physical output
device mix or every gameplay consumer. No save schema, stable IDs or generated
bank payload was changed by this worker.
The follow-up adds an Editor-only `WwiseBankContentValidator`, an optional
stronger synchronizer overload, a read-only `WwiseProductionBankAudit` batch
entry point, and 14 isolated parsing/route fixtures. The original synchronizer
menu retains its previous copy/hash semantics. No runtime backend or authoring
Work Unit is changed by this subtask.

`WwiseBankContentValidator.Inspect(bankRoot, expectedWwiseRoutes)` accepts only
explicit caller-selected Wwise routes. It checks bank presence, JSON presence,
matching binary/JSON bank identity, actual version-172 HIRC Event IDs, event
metadata and direct/nested-switch media references. It does not interpret the
complete binary action/media graph or prove that metadata media references are
fresh and playable. A stale JSON can therefore still misrepresent media for an
existing Event ID; real SoundEngine voice/output validation remains mandatory.
An initial guessed action-count shortcut was removed after direct binary
inspection showed extra Event fields in version 172.

The root validation run, not this worker, owns Unity execution. Historically,
the first focused run reported 49/50 total tests with the truncated-bank fixture
failing because `InvalidDataException` needed an explicit catch filter; root
corrected that filter. That first result is not the final validation gate;
the parent repair report owns later aggregate fixture totals.

Read-only batch entry point:
`MSC.Audio.Wwise.Editor.WwiseProductionBankAudit.RunSourceBankAudit`.
It first prints every raw-map gap, then inspects the actual Bootstrap Unity
supplemental/override arrays in a closed-again preview scene and the four
documented runtime Resources libraries (assembly, engine, player voice,
lighting switches). It
checks real clip references/sample lengths and reports their declared stable-ID
ownership. It does not start a new game or mutate Bootstrap. The 18 named old
empty Events are explicitly labelled known unresolved content; they are **not**
removed from the result. Unknown failures throw. A completed audit with known
gaps is explicitly not a full-content or audible-verification pass.

## Executed final audit and bounded runtime follow-up

The root executed `WwiseProductionBankAudit.RunSourceBankAudit` successfully
against the actual source banks. Evidence:
`Logs/AudioRepair-BankAudit-20260906.log`, completion marker at line 1843,
process exit **0**.

| Result | Count |
|---|---:|
| Raw mapped routes | 69 |
| Raw content issues | 22 |
| Mapped events explicitly owned by Unity libraries | 8 |
| Total declared explicit Unity events, including unmapped supplemental content | 71 |
| Remaining declared Wwise routes | 61 |
| Known unresolved Wwise content gaps | 14 |
| Unexpected resolved-route failures | 0 |

The eight mapped Unity routes account for all four missing body-impact Events
and four formerly silent assembly/fastener Events. The original 22 raw gaps
remain visible in the audit; they were not erased or mislabelled as newly
authored Wwise content. Fourteen old empty Wwise hooks still need content or
an explicit feature-owner decision: transmission, body rattle, suspension
impact, tire skid; garage/interior room tone and distant traffic; generic
tool use and fastener insert; five UI feedback hooks. An exit-zero audit with
these named gaps means **no unexpected route discrepancy**, not complete game
audio or all 69 Wwise routes passing.

The root also executed the graphics-enabled production test with native Wwise:
`Logs/AudioRepair-Production2-20260906.xml`, **1/1 passed**, zero failed/skipped,
duration **20.336394 s** (2026-09-06 06:09:54–06:10:15 UTC).
It exercised two sessions, each with **19 explicit Unity events** while Wwise
remained preferred, plus a native `audio.event.weather.thunder` post with
**six loaded banks**. The output records playing Unity sources, nonzero clip
PCM, gain/pitch values and native Wwise playback; no Event-ID-not-found errors
were reported in this bounded run.

This is stronger evidence than mock handles or merely finding AudioClips, but
it is **not human listening**, microphone/loopback output capture, a complete
manual engine-start sequence, every gameplay sound, or a packaged-player gate.
Final physical-device audibility, mix judgement and the 14 known content gaps
remain explicitly outside what these two successful runs prove.
