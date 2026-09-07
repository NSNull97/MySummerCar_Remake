# Recovery: missing global world scene at build index 11

Date: 2026-09-04. Scope: project scene-list recovery and UI validation-tool isolation.

## Evidence and cause

`ProductionWorldStreamingService.ValidateBuildEntry` reported that
`global-legacy` expected index 11 to resolve to
`Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/World_Global_Legacy.unity`,
but Unity returned an empty path.

On disk, `ProjectSettings/EditorBuildSettings.asset` contained just one enabled
scene: `Assets/Game/UI/Validation/MainMenuStandaloneBuild_f3360fd9bed04160a0a3d67e2e169114.unity`.
That temporary scene and its `.meta` had already been removed. The independent
production streaming manifest still contained its original 220 index/path
bindings; all 220 therefore failed against the truncated list.

The UI standalone helper assigned its fixture-only list to the global
`EditorBuildSettings.scenes` and attempted restoration in `finally`. This
unnecessary persistent mutation is the identified contamination path. The
specific reason restoration did not leave the correct list is not established
by the source alone: `finally` cannot protect against process termination or
restore a valid list when its saved input was already contaminated. This report
does not attribute the incident to the user or to native save data.

## Bounded changes

- Restored only `m_Scenes` from the reviewed HEAD baseline: 233 enabled scenes,
  all existing on disk, with matching metadata GUIDs. Bootstrap remains index 0,
  the legacy world remains 11, and global vegetation remains 232. All 220
  current manifest addresses match that baseline. Unreferenced entries were
  retained to preserve subsequent indices. No new required scene was missing
  from the candidate; other Build Settings fields already matched HEAD and
  were preserved.
- Removed both override and restoration assignments, and the unused snapshot,
  from `Assets/Game/UI/Validation/StandaloneEditor/MainMenuStandaloneBuild.cs`.
  The helper already passes its scene list explicitly through
  `BuildPlayerOptions.scenes` and `DonorRuntimeBaselineBuildGuard.BeginExplicitSceneBuild`.
  It now leaves the production list untouched throughout the build, including
  failure paths. Donor build guards remain enabled.
- Added `MainMenuStandaloneBuildSourceTests` in EditMode/UIEditor to reject
  global scene-list assignment and retain explicit build-scene routing.
- Added `ProductionStreamingBuildSceneTests` in EditMode/WorldBaseline to
  verify all enabled scene files/GUIDs, Bootstrap position, and global/cell/layer
  addresses through Unity's actual `SceneUtility.GetScenePathByBuildIndex` API.
- Updated `Docs/UI/MAIN_MENU_VALIDATION_TOOLING.md` with the no-mutation contract.

No runtime streaming validation was weakened. No world scene, generated
vehicle, stable ID, manifest address or save DTO was changed. User saves were
not opened or rewritten. Recovery was applied after the user closed Unity and
after coordinating editor ownership with the parallel UI task.

## Verification

Static inspection: 233 scene files and GUIDs valid; 220/220 manifest addresses
match; recovered Build Settings have no retained diff from HEAD;
`git diff --check` reports no whitespace errors.

Focused Unity execution is recorded in:

- `Logs/codex-streaming-scenes-recovery-edit.log`
- `Logs/codex-streaming-scenes-recovery-edit.xml`

Command: Unity `6000.3.11f1`, `-batchmode -nographics -runTests -testPlatform EditMode`,
filter `MSC.Tests.EditMode.UIEditor.MainMenuStandaloneBuildSourceTests;MSC.Tests.EditMode.WorldBaseline.ProductionStreamingBuildSceneTests`.

Result: **5/5 passed, zero failed/skipped**, Unity process exited with code 0.
The 233-scene settings file still matches HEAD after the editor process exits.
This bounded check is not a new full private-player build or an end-to-end
new-game/load test.

Exactly one next check: launch the normal Bootstrap/game again and confirm the
reported missing-scene exception is gone. Broader handbrake/physics work remains
separate from this scene-configuration repair.
