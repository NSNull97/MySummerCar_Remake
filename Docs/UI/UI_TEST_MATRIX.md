# Milestone 08A UI test matrix

Status: `GaussianFocusedSuitesBuildAndCapturePassed / UserVisualReviewPassed / ManualDeviceReviewPending`  
Date: `2026-07-20`

`PASS` is used only for executed evidence. Code inspection and unexecuted
manual checks remain explicitly separate.

The final 2026-07-20 Gaussian correction has focused authoring, EditMode,
PlayMode, private build and native-capture evidence. The user accepted the
canonical 16:9 presentation and pause readability on 2026-07-20;
physical-device/non-16:9 review remains separate.

## Executed suites

| Suite/gate | Coverage | Actual result |
|---|---|---:|
| Focused UI EditMode | runtime contracts/settings/localization, Editor source/reference/tooling, authored menu plate/logo and project-owned Gaussian shader | `33/33 PASS`; `TestResults/M08A_UI_GaussianBlur_EditMode.xml` |
| UI PlayMode smoke | production presentation routes, directional focus, HUD, bounded New Game/session gating, composition-wide pause gating, backdrop/Gaussian contracts, corrected settings/actions, circular handles, toast dismissal, rebind rollback/conflict | `11/11 PASS`; `TestResults/M08A_UI_GaussianBlur_PlayMode.xml` |
| Full project EditMode | pre-remediation regression context beyond the bounded UI suites | `386/392 PASS`; 6 known failures outside 08A; `TestResults/M08A_FullEditMode.xml`; `Logs/M08A_FinalEditModeFull.log` |
| Bootstrap authoring and validation | explicit production wiring, including serialized Gaussian shader dependency | `PASS`; `Logs/M08A_UI_GaussianBlur_Wire.log` |
| Private Windows development build | Windows x64 player with private donor-baseline gate | `PASS`; `851,423,934` bytes; `Logs/M08A_UI_GaussianBlur_Build.log` |
| Native capture player | world/UI boot, 12 routes, full-resolution static Gaussian glass, stable HUD, one-shot Gaussian pause, PNG writes | `PASS`; completion marker present; `Logs/M08A_UI_GaussianBlur_PlayerCapture.log` |
| Review-set generator | corrected implementation/reference/blend and ReferencePending captures updated 2026-07-20 | `PASS` |

## 2026-07-20 correction verification

| Contract requiring fresh evidence | Required automated/manual evidence | Current status |
|---|---|---|
| Static menu backdrop is outside `UiScale` and covers the viewport | focused PlayMode hierarchy/geometry plus native 16:9 capture | `PASS`; non-16:9 manual smoke remains pending |
| Prepared gameplay remains dormant until New Game | focused PlayMode `IGameplaySessionGate` coverage | `PASS` |
| Removed diagnostics stay absent | focused PlayMode absence checks | `PASS` |
| Shared settings actions are transactional | EditMode transaction plus focused PlayMode action/rebind coverage | `PASS` |
| Main/category buttons and greeting use corrected bounds | focused PlayMode geometry checks and corrected recapture | `PASS` |
| Glass tint is restrained to 8% | focused PlayMode colour assertion | `PASS` |
| Borders are continuous 1.75 px rounded rings | focused PlayMode sprite/child-structure assertion plus user review | `PASS`; user accepted on 2026-07-20 |
| Slider handles are circular `16 x 16` | focused PlayMode anchor/size/aspect assertion | `PASS` |
| Toast root dismisses completely | `NoticePanel_DisappearsTogetherWithItsMessage` | `PASS` |
| Project-owned Gaussian shader is imported/supported and explicitly wired | EditMode source contract plus authoring validator | `PASS` |
| No recurring HUD camera capture/blur | source contract, focused PlayMode, native HUD capture and user review | `PASS` |
| Pause one-shot Gaussian capture | native player log, capture and user review | `PASS`; appearance/readability accepted on 2026-07-20 |

## Requirement matrix

| Requirement | Evidence | Status |
|---|---|---|
| Route IDs stable and unique | `UiContractsTests.RouteAndLocalizationKeys_AreStablePrefixedAndUnique` | `PASS` |
| Missing capability truthful/unavailable | `UiContractsTests.MissingCapability_IsExplicitlyUnavailable` | `PASS` |
| Bounded defaults do not claim unsupported features | `UiContractsTests.BoundedDefaults_DoNotClaimUnsupportedFeatures` | `PASS` |
| Settings default/clone/validation | `UiSettingsDocumentTests` | `PASS` |
| Pending/apply/revert/default transaction | `UiSettingsTransactionTests` | `PASS` |
| Settings serialization/migration/recovery | `UiSettingsJsonStoreTests` | `PASS` |
| Approved reference manifest/hashes/dimensions | `UIReferenceEditorTests` | `PASS` |
| Missing approved-reference detection | `Validator_ReportsMissingApprovedReferenceFiles` | `PASS` |
| 50% overlay math | `Blend_AtHalfOpacity_ProducesMidpointPixels` | `PASS` |
| No crop/stretch at wrong aspect | `Normalize_RejectsNonCanonicalAspectInsteadOfCroppingOrStretching` | `PASS` |
| Runtime has no approved-PNG dependency | `RuntimeUiSources_DoNotReferenceApprovedReferencePixels` plus native build/capture | `PASS` |
| Project-owned menu backdrop is canonical and independently imported | `ProjectOwnedMenuBackdrop_IsCanonicalAndNotAReferenceScreenshot` | `PASS` |
| Presentation localization literals resolve | `PresentationLiteralLocalizationKeys_AllExistAndAreUnique` | `PASS` |
| Stable localization contract exists in catalog | `StableLocalizationContractKeys_ArePresentInPresentationCatalog` | `PASS` |
| English/Russian decimal and plural rules | `UiLocaleFormatterTests` (6 cases) | `PASS` |
| Pause keyboard/gamepad bindings authored | `CanonicalPlayerInput_ContainsPauseKeyboardAndGamepadBindings` | `PASS` |
| Main-menu exact action order | `MainMenuActionsAndHud_MatchBoundedApprovedPersistentContract` | `PASS` |
| HUD exact persistent categories/percent slots | same PlayMode test | `PASS` |
| HUD excludes GPS/minimap/quest/telemetry/hotbar | same PlayMode test | `PASS` |
| Locked and ReferencePending routes open with bounded focus | `BootMainMenuSettingsBackAndLockedRoutes_AreNavigableWithFocus` | `PASS` |
| Confirmation Cancel returns to caller | same PlayMode test | `PASS` |
| Save status is truthful unavailable state | same PlayMode test | `PASS` |
| Pause suspends/restores player and sibling vehicle gates | `PauseInput_SuspendsAndRestoresGameplayGates` | `PASS` |
| Static menu Gaussian / stable HUD tint / one-shot frozen pause modes and 8% car-colour tint | `RemediatedBackdropGlassAndRasterizationContracts_AreActive` | `PASS` |
| Pixel-perfect Canvas and fractional-alpha procedural coverage | same PlayMode test | `PASS` |
| Settings logo/navigation/Back/greeting geometry is invariant across all six categories | `SettingsChromeGeometry_IsInvariantAcrossCategories` | `PASS` |
| Explicit main/settings gaps and visible rounded-surface family | `RoundedSurfacesAndSpacing_FollowRemediatedVisualContract` | `PASS` |
| Toast panel and message dismiss together | `NoticePanel_DisappearsTogetherWithItsMessage` | `PASS` |
| Project-owned Gaussian shader import/support | `ProjectOwnedGaussianBlurShader_IsImportedAndSupported` | `PASS` |
| Persisted UI scale/look/dead-zone applies at boot | `PersistedAccessibilityScaleAndControls_AreAppliedAtBoot` | `PASS` |
| Effective-path conflict rejection | `BindingConflictDetection_RejectsDuplicateEffectivePathWithinControlGroup` | `PASS` |
| Interactive rebind cancel restores override | `CancelledInteractiveRebind_RestoresPreviousOverride` | `PASS` |
| Physical keyboard/mouse/gamepad traversal | architecture and initial focus automated | `MANUAL DEVICE CHECK PENDING` |
| Developer Tools hidden in non-development release | capability contract/source conditional | `RELEASE-SPECIFIC CHECK PENDING` |
| High contrast/reduced-motion consuming adapters | persistence foundation only | `NOT IMPLEMENTED IN 08A BOUND` |
| Money/needs production state not fabricated | source/data-boundary audit; review fixture is development-only | `PASS STATIC; provider integration deferred` |

## Flow matrix

| Flow | Status |
|---|---|
| Boot -> Loading -> prepared/dormant Main Menu | `PASS focused PlayMode + native capture` |
| Main Menu -> New Game -> activate session gate -> fresh bounded HUD session | `PASS focused PlayMode` |
| Main Menu -> Graphics -> Back | `PASS PlayMode` |
| Graphics -> Audio -> Controls -> Gameplay | `PASS PlayMode review-route traversal` |
| Accessibility and Mods bounded pages | `PASS PlayMode + native captures` |
| Quit -> Confirmation -> Cancel | `PASS PlayMode` |
| Save/load unavailable status | `PASS PlayMode + native capture` |
| Pause -> Resume | `PASS PlayMode` |
| Settings Pending -> Apply/Reset/Cancel -> new UI root | `PASS focused EditMode/PlayMode` |
| HUD gameplay/review state | `PASS PlayMode + native capture` |
| 16:9 1672x941 at 100% | `PASS 24-file capture set` |
| 16:10 | `MANUAL PENDING` |
| 21:9/ultrawide | `MANUAL PENDING` |
| 4:3 | `MANUAL PENDING` |

## Build and visual evidence

| Evidence | Status |
|---|---|
| six `*_Implementation.png` | `PASS` |
| six `*_ReferenceOnly.png` | `PASS` |
| six `*_Blended50.png` | `PASS` |
| six `*_ReferencePending.png` | `PASS` |
| corrected captures dated 2026-07-20 | `PASS` |
| user visual review, including pause blur/readability | `PASS`; accepted on 2026-07-20 |

All 24 PNG files under `Docs/UI/Review/08A/` were updated on 2026-07-20 for the
Gaussian correction. The locked set remains canonical at 1672x941 and the six
unreferenced required screens stay separate. The user supplied the final visual
verdict on 2026-07-20 and accepted the corrected canonical presentation.

## Performance evidence

Native development-player log/capture evidence:

- route construction: `93.193 ms` for 12 routes in the final D3D12 capture;
- sampled rebuilds in that capture: approximately `0.193-10.311 ms` per route;
- static menu glass: one full-resolution `1672x941` ARGBHalf Linear Gaussian
  result created from the project-owned plate; no recurring menu-camera render;
- pause: one-shot `1024x576` capture filtered to `512x288` ARGBHalf; D3D12 CPU
  submission `969.897 ms`, `0` measured thread bytes;
- supplemental forced-DX11 capture: shader orientation and capture set complete
  under `Temp/M08A_UI_GaussianBlur_DX11`; one-shot pause submission
  `596.251 ms`, `0` measured thread bytes;
- HUD: stable dark translucent blocks, no camera capture, blur or recurring
  render cadence; authoritative data refresh remains revision-driven;
- render-texture vehicle preview: unavailable, no recurring cost;
- thread-allocation counter: `0` bytes in sampled construction/rebuild sections,
  treated only as the counter floor;
- controller focus assignment: synchronous with route activation; physical
  controller-to-display latency remains unprofiled;
- after the successful capture completion marker, the development player emits
  a generic shutdown leak-detector warning for 8 persistent allocations; it is
  retained as non-08A cleanup evidence, not hidden by the passing capture gate.

The D3D12 evidence is authoritative at `Docs/UI/Review/08A/` and
`Logs/M08A_UI_GaussianBlur_PlayerCapture.log`. DX11 is supplemental evidence in
ignored output and does not replace the canonical capture set. The later user
verdict closes the manual pause appearance/readability gate.

## Reproduction

Use Unity `6000.3.11f1`:

```text
Unity.exe -batchmode -nographics -projectPath <PROJECT_ROOT> \
  -runTests -testPlatform EditMode \
  -testFilter "MSC.Tests.EditMode.UIEditor;MSC.Tests.EditMode.UIRuntime" \
  -testResults TestResults/M08A_UI_GaussianBlur_EditMode.xml
```

```text
Unity.exe -batchmode -nographics -projectPath <PROJECT_ROOT> \
  -runTests -testPlatform PlayMode \
  -testFilter MSC.Tests.PlayMode.UIPresentation.GameUiRootPlayModeTests \
  -testResults TestResults/M08A_UI_GaussianBlur_PlayMode.xml
```

The native development player accepts:

```text
-msc-ui08a-capture <Docs/UI/Review/08A>
```

Then run `MSC.UI.EditorTools.UIReferenceBatchCommands.GenerateAllReviewSetsBatch`
or use `Tools > My Summer Car > UI > Milestone 08A Reference Review`.

## Full-suite failures retained for visibility

The full EditMode run predates the bounded remediation and is retained only as
repository-level context. Its six failures were outside the original bounded
08A implementation:

- two GaragePrototype checks expect the older neutral HDRP lighting profile;
- two donor-world cellization checks report compatibility-material drift;
- WorldRemaster still expects the earlier `PilotGate` state;
- WorldTransfer reports canonical donor source-hash drift.

Focused green results do not override or conceal these repository-level debts.
