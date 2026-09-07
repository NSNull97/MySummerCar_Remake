# Unity 6.6 migration — 2026-09-06

Status: Migrated; final scoped Editor/test gates passed and 6000.3.11f1 was
removed with its official uninstaller after verification. User explicitly
requested both actions. This is a deliberate
move from the 6.3 LTS baseline to the 6.6 update stream, not a claim that 6.6 is LTS.

## Baseline and recovery

- Previous editor: 6000.3.11f1 (3000ef702840).
- Target installed editor: 6000.6.0f1 (f7f8ed4d1e24).
- Prior Assets (31,022 files, 10.168 GiB), Packages, ProjectSettings and Config
  were copied without deletion/mirroring to the external local folder
  `E:/GAYmDev_Studio/UpgradeBackups/MySummerCar_Remake_before_6000.6.0f1_20260906`.
  Robocopy reported zero failures/mismatches. Cache/Library is reconstructible
  and is not the backup. No donor installation files or user saves are changed.
- slot-01 pre-migration SHA256:
  `750AC0AC13B707BEDC526EB3C940A19F636A5DB217A9225CFEB8F32A1A86A724`.
- Audio task finished its 6000.3 tests before this migration and agreed to stop
  Unity/package/bank operations. Its accepted changes are part of this snapshot.

## Compatibility and dependency audit

The editor resolves its built-in HDRP/Core/ShaderGraph/VFX/URP cohort to 17.6.0,
uGUI to 2.6.0 and Timeline to 6.6.0. Do not mix old/new SRP packages or relabel
URP as the active rendering backend; it remains an Enviro compatibility dependency.

Unity 6.6 replaces integer scene handles and obsolete object instance-ID APIs
with native typed identities. These are **session-local**, not project-owned
stable entity IDs. Private scene/renderer caches must keep the full native value,
not cast a 64-bit handle to int or use its hash as a supposedly unique identity.

One bounded source incompatibility is unavoidable in `IAudioEmitter`:
`OwningSceneHandle` becomes `UnityEngine.SceneManagement.SceneHandle` instead of
`int`. Repository-wide usage audit found one production implementation
(`AudioEmitterAuthoring`), four test implementations, scene-unload consumers in
the router and both backends, and an Editor diagnostic formatter. All are
compiled together and use transient scene identity. No serialized field, stable
ID, DTO, save schema, bank/event ID or external package implements this contract.
Implementations and scene-unload comparisons are updated together; existing
audio lifecycle regression tests must execute on the new editor. No save
migration or runtime reconstruction of the vehicle is required.

Unity's automatic light API updater rewrote `HDAdditionalLightData` radius
assignments to an undefined `legacyLight` identifier. They must target the
existing matching Unity `Light`, preserving the old source-radius value.
Vendor compatibility edits are recorded separately; no vendor version or bank
content should be changed merely to clear diagnostics.

The map inspection report advances from schema 1.0 to 1.1: its old `int
instanceId` remains readable (new scans leave it zero), and a new `ulong
entityId` records the full diagnostic native identity. The original 31 CSV
columns remain in order; `EntityId` is appended as column 32. Neither field is
a gameplay key; record IDs, source fingerprints and save documents are unchanged.
The inactive terrain pilot builder now uses `ConstantOnly` for its zero-valued
smoothness, matching the authored constant intent. No terrain is regenerated.

Independent backup-diff review found no gameplay logic/DTO changes in vehicle
restore guards, the NWH adapter's invalidation hash or transient identity caches.
Wwise's native-plugin `.meta` and both HDRP settings assets matched the backup
exactly at that review point: their pre-existing Git changes are not upgrade
regressions and must not be reverted.

After complete import, the 29,006 non-C# Assets retained their file set. Seventy
texture `.meta` files (34 Satsuma, 36 NWH) received serializer/default metadata;
all GUIDs, 2,526 prior root settings and 253 prior platform blocks were retained
(the iPhone label becomes iOS). HDRP/URP global settings were migrated by SRP:
all 410/157 prior asset references remain, with new package references added.
The cleared runtime settings list is the SRP container's intentional Editor
cache behavior, not lost authoring. Native Wwise plugin metadata and the default
Volume profile remained byte-identical. Build Settings keep all 233 scenes and
their order; only obsolete `m_UseUCBPForAssetBundles` was removed on serialization.
New PhysicsCore2D/ProjectAuditor settings and VFX Editor resource registration
are editor-generated, not changes to 3D gameplay physics or the world layout.

Final read-only vendor verification found all 216 Wwise plugin files identical
to the backup, including 69 native binary/symbol files. The six source bank
hashes match the recorded pre-migration bank audit (the source WwiseProject was
not part of the Assets snapshot). Both NWH/Foliage compatibility patches pass
strict forward checks against the backup and reverse checks against the current
files; all before/after raw and normalized hash checks pass. Package lock and
the nine relevant installed SRP/Timeline/uGUI manifests agree. See
`Docs/Architecture/UNITY_660_VENDOR_COMPATIBILITY_2026-09-06.md` and
`Tools/Compatibility/` for exact vendor files and reproducible patches.

The editor newly warns about old serialized Mesh/TextureImporter versions when
loading existing donor-generated content. The 6.6 release notes explicitly add
this warning. No mass reserialization, mesh regeneration or donor payload
replacement was performed just to silence it. Geometry, physics and rendered
pixel gates must be reported separately; a warning-free project is not claimed.

### Pre-existing validation drift found during the expanded gate

- Lighting: the 2026-08-31 `revision-editmode-final-r7.xml` and the vegetation
  revision report already record the same three failures. The unchanged catalog
  has 49 lights, 10 probes, 14 night-only lights and the established refrigerator
  pose `(90, 147.397, 90.001)`. Exact test expectations were reconciled; runtime,
  assets and rotation tolerance were not changed. See
  `Docs/Reviews/UNITY_660_LIGHTING_EXPECTATION_AUDIT_2026-09-06.md`.
- Weather's old one-global-scene gate rejected the existing second, explicitly
  registered global rock presentation layer. The unchanged manifest still has
  the locked profile ID, private flag and 49 legacy cells. The Editor gate now
  accepts only the exact original global scene plus that exact approved optional
  layer; a regression rejects unknown, redirected, duplicate and missing-legacy
  registrations. No world content or ownership changed.
- The optional `SatsumaNativeStartSmokeTests` file test requires a **pre-bridge**
  snapshot without dynamic purchased wrappers. The current user save has such
  wrappers and is correctly rejected by that deliberately bounded harness. It
  is not rewritten or stripped to manufacture a pass. Modern native round-trip
  and Bootstrap reload tests are the relevant migration checks.
- All 540 Enviro vendor files match the pre-upgrade backup by SHA-256. However,
  that snapshot already differs from the strict 08A1 frozen 538-file baseline:
  a new unreferenced weather type plus metadata, a Foggy inspector foldout, and
  HDRP sample profile/terrain-layer presentation edits. No approved 540-file
  fingerprint migration was found. The historical strict vendor preflight drift
  remains open; its fingerprint is **not** rewritten to hide prior user changes.
  Runtime integration checks and unchanged-before/after evidence are separate
  from that full provenance preflight.
- The weather PlayMode fixture still expected thirteen removal ellipsoids.
  The same failure (13 expected / 20 actual) is recorded on 2026-09-01 before
  this migration. Unchanged authoring yields 7 house + 3 garage + 7 garage-door
  transition + 3 yard-hall ellipsoids, while retaining three logical zones and
  two original shelter volumes. The test now checks those exact counts and
  stable zone IDs. No shelter, profile, collider or weather runtime was changed;
  see `Docs/Reviews/UNITY_660_WEATHER_REMOVAL_EXPECTATION_AUDIT_2026-09-06.md`.
- The unchanged M4 assembly handoff coroutine executes its first eased step
  synchronously. Its old immediate-displacement assertion implicitly required
  a frame faster than approximately 26.79 ms. The test now compares that first
  position to the actual unscaled-delta-time easing result, keeping its 1 cm
  tolerance and all final installed/body/scale/visibility assertions. Gameplay
  timing is unchanged; see
  `Docs/Reviews/UNITY_660_HANDOFF_FIRST_FRAME_AUDIT_2026-09-06.md`.

## Execution log

### Inactive Rigidbody compatibility boundary

The original two purchased-consumable assertions and their normal Play-mode
scene A/B variants all exposed the same Unity 6.6 actor-lifetime behavior, not
a preview-scene-only issue. A separately executed isolated probe passed 1/1:
after parenting a Rigidbody below an inactive parent, its native pose getters
returned zero/identity while its Transform retained the exact authored pose.
Transform plus sequential Rigidbody writes, a kinematic transition and repeated
post-configuration writes did not create an inactive actor. Reactivating only
the isolated probe immediately restored the exact native pose from Transform.
See `Logs/unity660-inactive-pose-probe-20260906.xml` and
`Docs/Reviews/UNITY_660_INACTIVE_RIGIDBODY_POSE_AUDIT_2026-09-06.md`.

This exposed real save/rollback risks, not just invalid inactive native-getter
assertions. Scoped fixes in `WorldEntitySaveParticipant`, `ItemSaveParticipant`,
`VehicleSaveParticipant`, `VehiclePersistenceBinding` and item restore
`ItemTopology` preserve **active** native Rigidbody position/rotation (including
physics/interpolation precision) and use Transform only for inactive objects.
They neither activate gameplay content nor alter stable IDs, DTOs, restore
ordering, authored placement or gameplay mechanics. The vehicle support-cell
lookup follows the same pose policy, preventing a false origin-cell lookup.
New registration/capture/guard/rollback regressions and strict purchased DTO
pose assertions must pass before this boundary is considered verified. Active
plug/bulb handoff tests additionally require native body-to-mount alignment.

### Runs

- Backup: 11:31–11:32 local, completed.
- First import: PID8116, `Logs/unity660-first-import-20260906.log`, batch mode,
  no graphics, API updater accepted, job-worker-count4. Stopped with compiler
  errors; this is not a successful migration check.
- Compile passes 2–5 exposed dependent assemblies after their upstream errors
  were repaired. Compile 6 (PID3320) cleared C# compilation and started the full
  asset import. `ProjectVersion.txt` was then updated by the actual editor to
  6000.6.0f1 / f7f8ed4d1e24. Full import completed in 527.568 seconds, process exit 0.
- The first import encountered a truncated Burst IL hash cache. With every
  owned Unity process closed, only `Library/BurstCache` was moved to the backup's
  `BurstCache-after-first660-import` subfolder. It is recoverable; source assets
  and the remainder of Library were not removed. The error did not recur in
  the next two editor runs.
- First expanded EditMode: 1,359 passed, 6 failed, 1 opt-in skipped. This is
  diagnostic history, not the final passing gate. The lighting/weather baseline
  mismatches above were identified; inactive purchased-wrapper physics pose is
  being investigated with an A/B fixture before changing gameplay code.
- The next 37-case diagnostic run passed the repaired lighting/weather audits
  and the full-width identity tests. Its normal-scene A/B cases were blocked
  by Editor additive-scene creation with an unsaved untitled scene; they are
  being rerun with isolated `SceneManager.CreateScene`, without discarding or
  saving the current scene. The pre-bridge opt-in failure is documented above.
- The three vendor foliage shader import errors were repaired through scoped
  HDRP 17.6 shadow keyword updates; their errors did not recur in the graphics
  diagnostic import. See the separate vendor compatibility report.
- Post-fix targeted EditMode: `Logs/unity660-pose-fixed-20260906.xml`, **45/45
  passed**, zero failed/skipped. This includes all eleven new save/guard pose
  cases, two inactive item rollback cases and the complete six-case canonical
  consumable native-save fixture, plus lighting/weather validation.
- Final expanded EditMode: `Logs/unity660-editmode-final-20260906.xml`, **1,382
  passed, zero failed, one explicitly opt-in skipped** (1,383 discovered).
  The skip is only `SatsumaNativeStartSmokeTests.ExplicitNativeSnapshotSmokePreservesFileAndExercisesStartFailureBoundaries`;
  its pre-bridge snapshot limitation is documented above. Unity exited 0.
  The test framework's short aggregate duration excludes substantial editor
  reload/import work; it must not be presented as the wall-clock run duration.
  The selected gate covers the existing vehicle/assembly/simulation/import,
  items/save, audio, streaming, weather/Enviro, AA and identity-report regressions.
- First combined graphics PlayMode: `Logs/unity660-playmode-final-20260906.xml`,
  166 passed, five failed, two opt-in skipped (173 selected). The handoff and
  pre-existing weather expectation failures are documented above. The real
  production Wwise/explicit-clip two-session test, Bootstrap native reload and
  strengthened active purchased plug/bulb tests already passed in this run.
- Dedicated graphics UI gate: `Logs/unity660-menu-isolated-20260906.xml`, **3/3
  passed** on unchanged menu runtime, including real paint pixels, three HDRP
  profiles with an unchanged world sun, and hide/resume/disposal. Fresh blue
  and red render-target diagnostics were produced; the blue image was inspected.
- Combined retry r2 stopped at a diagnostic-test compilation typo before tests
  ran (`EntityId.ToULong` is static). The typo was fixed; no test pass is claimed
  for that process. Retry r3 (`Logs/unity660-playmode-final-r3-20260906.xml`)
  passed 169, failed two UI cases and skipped two opt-in cases. The corrected
  handoff measured an actual 37.3128951 ms step with **zero** expected/actual
  positional deviation; the exact weather counts and stable-zone IDs passed.
- r3's UI diagnostics identify the mixed-suite conflict: the earlier M4 tests
  leave `VehicleAssemblyPrototype` loaded with its 95,000-lux directional sun;
  the UI quality fixture adds its intended 48,000-lux world sun, producing two
  active directional shadow owners. The aborted quality iterator also leaves
  that same second light visible to the next fixture. This is not a reason to
  disable the production sun or rewrite accepted menu lighting. Final gameplay
  and UI gates run separately, matching the established isolated UI workflow;
  see `Docs/Reviews/UNITY_660_UI_TEST_ISOLATION_AUDIT_2026-09-06.md`.
- The two PlayMode skips are the optional `CanonicalWiringReachPlayModeTests`
  pre-bridge-file cases. Like the optional EditMode native snapshot test, they
  explicitly reject dynamic purchases without the Items domain. Both current
  user slot files contain nine dynamic purchased parts. Neither file is stripped,
  downgraded or rewritten for these historical fixtures. Modern full-domain
  native restore, active purchased handoff and canonical wiring unit/integration
  checks are separately covered; these two old-file comparisons remain opt-in.
- Final isolated gameplay gate: `Logs/unity660-gameplay-final-20260906.xml`,
  **168 passed, zero failed, two explicit opt-in skipped** (170 discovered),
  Unity exit 0. It includes the real Wwise-preferred/Unity-explicit playback
  regression across two sessions, with six loaded Wwise banks and native thunder
  playback, as well as native Bootstrap reload, active purchased items, vehicle
  physics, streaming lifecycle, weather and AA checks. Build Settings SHA-256
  remains `9021BAE89AD067F64A6412F29DB6E215095DC3853D0173BAEC0D38FC553B0344`
  after fixture cleanup (233 scenes, original order).
- Final current-source isolated UI gate: `Logs/unity660-menu-final-20260906.xml`,
  **3/3 passed**, zero failed/skipped, Unity exit 0. All three retain their
  original timing/pixel/quality assertions; only diagnostic context was added.
  Final selected totals are **1,382 EditMode passes + 171 PlayMode passes**,
  zero failures in these final isolated gates, with three historical opt-in
  skips total. The earlier failed diagnostic runs remain preserved above.
- Optional actual production-menu screenshot: the owned non-batch Editor
  (PID19256, started 08:42:28.3823461 UTC) imported cleanly, but its startup
  created an unsaved untitled scene containing Main Camera, Directional Light
  and WwiseGlobal. `open-owned-menu-review` and the narrower empty-scene cleanup
  correctly refused to discard it. Their blocked reports are under
  `Artifacts/MainMenuRedesign/result-unity660-production-menu-open-20260906.json`
  and `result-unity660-owned-empty-check-20260906.json`. No Bootstrap screenshot
  is claimed and neither guard was weakened. Later the same Editor was observed
  running Bootstrap in Play mode with no dirty scenes. The read-only
  `unity660-production-menu-capture-20260906` request then correctly refused
  because exactly one visible MainMenu was not present; it did not navigate or
  stop the session. The current 6.6 Editor remains open. The inspected fresh menu
  render-target PNGs and passing production Bootstrap gameplay tests are separate
  evidence; the agent saved no scene asset.
- Old-editor removal: verified the exact old installation path and absence of
  any process running that old editor, then invoked its official `Editor/Uninstall.exe`
  with `/S`, hidden, from a working directory outside the installation. Launcher
  PID30596 exited 0 and the copied uninstaller completed. The old Unity.exe is
  absent and the new 6000.6.0f1 Unity.exe remains. The measured old installation
  occupied 12,799,313,672 bytes (approximately 11.92 GiB). Hub, the new editor,
  project snapshot and user saves were not removed. Reinstall 6000.3 through Hub
  if needed for the external pre-upgrade snapshot; do not downgrade this checkout.
  The uninstaller left only `metadata.hub.json` and `modules.json` (40,845 bytes)
  under the old version folder; no editor executable or runtime payload remains.

## Handoff, compatibility and remaining limits

- Exact changed files: `Docs/Architecture/UNITY_660_CHANGED_FILES_2026-09-06.md`
  (75 C# files: 73 modified and two added, plus dependency/settings/importer
  metadata and reproducible vendor patches). Existing dirty pre-snapshot work
  is preserved and is not attributed to this migration. No commit was made.
- Commands are retained verbatim in each linked log's command-line header.
  Final tests use the installed 6000.6.0f1 editor, `-batchmode -force-d3d11`,
  `-wwiseEnableWithNoGraphics`, four workers, `-runTests`, the recorded scoped
  filter and XML path; PlayMode also has process-local
  `MSC_REQUIRE_LIVE_WWISE=1`. No `-quit` or `-nographics` was added to these gates.
- Final `Tools/Validate-Environment.ps1` run with the explicit ignored local
  config resolves the new editor, donor/project separation, Git/LFS and existing
  extraction tool successfully. Its only failure-labelled line is the already
  unconfigured **optional** ILSpyCmd path; no tool was installed to hide that
  pre-existing configuration gap. Scoped whitespace checks passed. Final user
  slot SHA-256 still exactly matches the pre-migration value recorded above;
  the existing `.bak` also remains unchanged at
  `836A8C8A111BDC6E15AB9EFBD2A1AD3412272269E369BCDE8261C4A57CAD4ABD`.
- Completed 00–08A runtime/UI/weather/world/vehicle foundations remain in place.
  The only public source boundary migration is the transient audio scene handle;
  the map diagnostic report gains its documented schema-1.1 full-width entity ID.
  Native save schemas, stable gameplay IDs and accepted presentation are unchanged.
  The activity-aware save/rollback fix requires no save-data migration.
- Manual Unity acceptance: continue the normal interactive review in
  `Assets/Game/Bootstrap/Bootstrap.unity` (already observed in Play mode at
  handoff). Automated rendered-pixel evidence is not user
  visual approval or physical-input/whole-game playthrough acceptance.
- Existing serialization/deprecation warnings and the strict historical Enviro
  fingerprint drift remain documented. No standalone Windows player was built
  and no performance/FPS, full-game parity or Phase 2 approval is claimed.
- Next milestone: resume the existing Phase 1 Satsuma feature-parity work on this
  pinned editor, preserving the completed integration baseline; do not enter Phase 2.

## Primary references

- [Unity 6000.6.0f1 release notes](https://unity.com/releases/editor/whats-new/6000.6.0f1)
- [Unity 6.6 upgrade guide](https://docs.unity3d.com/6000.6/Documentation/Manual/UpgradeGuideUnity66.html)
- [Unity 6.4 upgrade guide](https://docs.unity3d.com/6000.6/Documentation/Manual/UpgradeGuideUnity64.html)
- [Editor command-line arguments](https://docs.unity3d.com/6000.6/Documentation/Manual/EditorCommandLineArguments.html)
