# Milestone 08A UI test matrix

Status: `Historical08APassed / Hud20260809FocusedTestsPassed /
CameraSettingsTargetedTestsPassed / AntiAliasingSettingsTargetedTestsPassed /
ContextActionFocusedTestsPassed /
HudAndGraphicsVisualReviewPending / ManualDeviceReviewPending`
Date: `2026-09-02`

`PASS` is used only for executed evidence. Code inspection and unexecuted
manual checks remain explicitly separate.

The final 2026-07-20 Gaussian correction has focused authoring, EditMode,
PlayMode, private build and native-capture evidence. The user accepted the
canonical 16:9 presentation and pause readability on 2026-07-20;
physical-device/non-16:9 review remains separate.

The bounded 2026-08-05 HUD direction reopens only HUD validation and visual
approval. Historical results below remain evidence for the prior composition;
they are not presented as results for the new layout.

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

## 2026-08-15 camera settings extension

| Contract | Executed evidence | Current status |
|---|---|---|
| Schema-v5 defaults, validation, JSON round trip and v4 migration | `MSC.UI.Runtime.Tests.EditMode` | `23/23 PASS`; `TestResults/UserCameraSettings_UIRuntime_EditMode.xml` |
| First-person sink applies aspect-correct horizontal FOV and camera far clip | focused `CameraSettingsSink_AppliesHorizontalFovAndFarClip` EditMode test | `1/1 PASS`; `TestResults/UserCameraSettings_Player_EditMode.xml` |
| Persisted values reach the gameplay camera at UI boot and both Graphics controls are enabled | two focused UI PlayMode tests | `2/2 PASS`; `TestResults/UserCameraSettings_UI_Targeted_PlayMode.xml` |
| Moving both sliders then pressing Apply updates the live sink and saved JSON | `CameraGraphicsSliders_ApplyLiveAndPersist` | `1/1 PASS`; `TestResults/UserCameraSettings_UI_Apply_PlayMode.xml` |
| Full UI PlayMode regression context | complete `MSC.UI.Presentation.Tests.PlayMode` assembly | `21/22`; the sole failure is the unrelated viewport assertion counting masked `SettingsControls/BindingRow22` at `-1.722 px`; `TestResults/UserCameraSettings_UI_PlayMode_Final.xml` |
| Camera card matches the accepted Graphics composition | fresh canonical capture plus user review | `PENDING`; tracked as `UI08A-DEV-030` |

## 2026-08-17 anti-aliasing settings extension

| Contract | Executed evidence | Current status |
|---|---|---|
| Schema-v6 AA defaults, validation, JSON round trip and v1-v5 migration | `MSC.Tests.EditMode.UIRuntime` | `25/25 PASS`; `Logs/Codex_AA_UI_EditMode_Final.xml` |
| Graphics mode/preset/sharpening edits reach the central controller and persisted JSON | `AntiAliasingGraphicsSettings_ApplyRuntimeAndPersist` | `1/1 PASS`; `Logs/Codex_AA_UI_FocusedPlayMode.xml` |
| Native AA controls remain interactive while unsupported upscaler quality stays truthful | `UnsupportedSettings_AreTruthfullyDisabledAndRequiredRowsExist` | `PASS` inside the broader PlayMode run |
| Full UI PlayMode regression context | `GameUiRootPlayModeTests` | `22/23`; sole failure remains the unrelated Controls viewport assertion for `BindingRow22` at `-1.722 px`; `Logs/Codex_AA_UI_PlayMode.xml` |

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

## 2026-08-05 minimal HUD correction verification

| Contract requiring fresh evidence | Required automated/manual evidence | Current status |
|---|---|---|
| Six-needs column upper-left; clock upper-right; `value MK` directly beneath | `MainMenuActionsAndHud_MatchBoundedApprovedPersistentContract` | `PASS`; `TestResults/M08A_HudMinimal_Vertical_Contract_PlayMode.xml` |
| Six vertical needs include Urine and Dirtiness; text is 1 px larger and tracks are 144 px | same focused PlayMode test | `PASS` |
| Text, icons, tracks, fills and indicators own dark shadows | same focused PlayMode test plus bright-scene capture | `PASS` automated; visual capture pending |
| Optional FPS uses pure-white numeric value followed by amber `FPS`, shared shadows and no line/panel | same focused PlayMode test | `PASS` |
| Day/date field gap is 2 px; FPS value is digits-only and both FPS elements are 17 px | updated focused PlayMode contract | `PASS` |
| Speedometer/tachometer/gear/fuel remain absent | same focused PlayMode forbidden-name assertion | `PASS` |

## 2026-08-09 context, needs and subtitle correction

| Contract | Coverage | Current status |
|---|---|---|
| Item title and pickup action are distinct; title has no duplicated action/name payload | `ItemHudMetadata_SeparatesTitleFromPickupAction` | test source added; Unity Test Runner pending Editor release |
| Prompt rows use 1 px gap, track a bounded ray-hit-local target anchor and flip at viewport edges | `InteractionPromptBlocks_StaySeparatedAndFlipBesideScreenEdges` and `InteractionPromptAnchor_IsBoundedAroundTheAimedSurface` | `PASS` in the focused `22/22` EditMode run |
| Need percentages are absent; icons are 28 px white; tracks are 112 x 3 px; fill is horizontal gradient and endpoint is 7 px white | updated `MainMenuActionsAndHud_MatchBoundedApprovedPersistentContract` | `PASS` in the focused `20/20` PlayMode run |
| Bottom subtitle frame remains centred and bounded | `SubtitleFrame_IsBottomCenteredAndScreenBounded` | test source added; `MSC.Needs.Runtime` compiles |

## 2026-09-02 context-action revision

| Contract | Coverage | Current status |
|---|---|---|
| Fixed maximum of three rows, lower-left canonical safe-frame geometry, compact binding labels and Alt replacement semantics | `ContextActionStack_RemainsBottomLeftAndCapsAtThreeRows`, `ContextActionSnapshot_CapsRowsAndAltReplacesTheStack`, `ContextActionBindingLabels_UseCompactMouseAndKeyboardNames` | `PASS`; included in `88/88` focused EditMode run, `TestResults/ContextActionUi.Relevant.EditMode.xml` |
| Selected wrench exposes endpoint-aware positive/negative wheel actions and keeps tool deselection within the three-row cap | `FirstPersonToolMode_RaycastSelectsOnlyScrollTargetsAndWheelOperatesThem` plus directional fastener capability tests | `PASS`; included in the focused `88/88` EditMode run |
| Carried object exposes release, throw and rotation; pickup/install reticle states follow real capability checks | `HeldObjectActionSnapshot_ExposesReleaseThrowAndRotation`, `PickupKeepsSelectedSurfacePointAtPhysicalCarryAnchor`, `HeldBodyDoesNotOccludeMountHandoffThroughPlayerController` | Snapshot/handoff contracts `PASS`; full player-flow class is `8/9`, with only the pre-existing carry-spring tolerance test at `0.091 m` versus `<0.08 m` failing, `TestResults/ContextActionUi.PlayMode.xml` |
| Left or right Alt press/release reaches the router; no Alt instruction action exists | `HoldingAlt_ExposesAlternativeActionLayerState`, `CanonicalPlayerInput_ContainsLicensedHandGestures` | `PASS`; isolated Input System fixture `1/1`, canonical bindings use explicit `<Keyboard>/leftAlt` and `<Keyboard>/rightAlt`, `TestResults/ContextActionUi.Alt.PlayMode.xml` |
| Hand/check/cross and semantic mouse assets are 64 px, transparent, uncompressed, no-mipmap and clamp/bilinear | `ContextActionIcons_UseLicensedCrispRuntimeImports` | `PASS`; included in the focused `88/88` EditMode run |
| Explicit removal uses the cross reticle; target and subtitle rows share a collapsible bottom-centre stack | `RemovalCapabilityUsesTheDedicatedCrossReticle`, `ContextTextStack_RemainsBottomCentredAtWideAspectRatios`, `ContextHudClaimsAndReleasesExplicitSubtitleSource` | `PASS`; isolated removal `1/1`, `TestResults/ContextActionUi.Removal.PlayMode.xml`; context contracts included in `88/88` EditMode |
| Canonical 1672x941 appearance, bright/dark-world readability and Alt transition | fresh private-player capture and user review | `PENDING`; only the user may mark visually approved |

Unity `6000.3.11f1` batch compilation completed with zero context-action
compiler errors. The focused related EditMode filter passed `88/88`; the
isolated Alt PlayMode fixture passed `1/1`; the broader player-flow class passed
`8/9`, retaining one unrelated pre-existing carry-physics tolerance failure.
The M4 and M05 batch validators also ran: M4 is blocked by existing duplicate
stable IDs in vehicle/world validation assets, while M05 is blocked by existing
fastener-group threshold data on 14 mounts. Neither validator reported a new
context-action icon, binding or prefab-reference error.
| No HUD panel capture or blur | `RemediatedBackdropGlassAndRasterizationContracts_AreActive` | `PASS`; `TestResults/M08A_HudMinimal_Vertical_Backdrop_PlayMode.xml` |
| Revised spacing does not regress shared rounded surfaces | `RoundedSurfacesAndSpacing_FollowRemediatedVisualContract` | `PASS`; `TestResults/M08A_HudMinimal_Vertical_Spacing_PlayMode.xml` |
| Full UI PlayMode class | 17 tests | `16/17`; unrelated existing Graphics expectation for missing `UpscalerQualityState` |
| Canonical 1672x941 appearance | fresh private-player implementation capture and user review | `BLOCKED`; player compile `CS1061` in untracked `WorldLightingProbeRuntime.cs` |

## 2026-07-31 lightweight-menu startup regression

| Contract | Evidence | Current status |
|---|---|---|
| Main-menu Bootstrap has no streaming focus and owns zero additive gameplay scenes | `ProductionEnvironmentLifecyclePlayModeTests` pre-preparation assertions | `COMPILES`; Unity PlayMode/manual execution pending |
| New Game remains on Loading until asynchronous world preparation completes, then activates gameplay once | `GameUiRootPlayModeTests.NewGame_WaitsForGameplayWorldPreparationBeforeActivation` | `COMPILES`; Unity PlayMode/manual execution pending |

## 2026-08-06 New Game input restoration regression

| Contract | Evidence | Current status |
|---|---|---|
| An enabled player input gate that is inactive behind Main Menu is enabled after New Game activation | `GameUiRootPlayModeTests.NewGame_RestoresInputGateActivatedAfterMainMenuSuspension` | `COMPILES`; batch PlayMode rerun blocked while the project is open in the user Editor |
| An intentionally disabled inactive gate stays disabled; a subsequent pause/resume restores the active gate | same focused PlayMode test | `COMPILES`; batch PlayMode rerun blocked while the project is open in the user Editor |

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
- HUD: shadowed scene-integrated graphics, no panels, camera capture, blur or
  recurring render cadence; authoritative data refresh remains revision-driven;
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

## 2026-08-05 developer-menu coverage

| Contract | Coverage | Result |
|---|---|---|
| Main-menu Developer Tools invokes the composed action | `MainMenuDeveloperTools_InvokesComposedDevelopmentMenu` | PlayMode focused run pending project lock release |
| Reset clears all needs, weight and pending metabolism | `DeveloperResetAll_ClearsNeedsPendingEffectsAndWeight` | PASS |
| Disabled needs ignore time and gameplay effects | `DeveloperDisable_FreezesProgressionAndDoesNotCatchUp` | PASS |
| Re-enable does not apply the disabled interval as catch-up | same EditMode test | PASS |
| Button-menu layout and cursor/input restoration | manual visual smoke in `Docs/UI/DEVELOPER_MENU.md` | PENDING |

The focused needs assembly passed `12/12` at
`TestResults/DeveloperMenu_Needs_EditMode.xml`. The first attempt to run the UI
PlayMode assembly was rejected before test execution because another Unity
process held the project lock; it must be repeated after that independent test
finishes.

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

## 2026-08-09 HUD typography and target-anchor follow-up (historical)

The two target-anchor/action-sentence rows below are superseded by the
2026-09-02 context-action revision. Their PASS state remains evidence for the
former implementation, not the current visual contract.

| Contract | Coverage | Status |
|---|---|---|
| needs track is `112 x 3`, percentages remain absent | `GameUiRootPlayModeTests` HUD review assertions | PASS |
| fixed full-scale white-to-amber fill uses `Image.Type.Filled` | same PlayMode HUD contract | PASS |
| all persistent UI resolves bundled Helvetica Neue Roman | money/font assertion plus `UiFontResolver` glyph validation | PASS |
| needs icons resolve the six licensed Lucide runtime assets | six sprite-name assertions | PASS |
| need icon textures are 64 px, uncompressed and have no mip chain | runtime texture assertions plus importer settings | PASS |
| weekday/date uses one 180 px right-aligned line | HUD review hierarchy and text assertions | PASS |
| large character/vehicle bounds cannot move the anchor far from the ray hit | `InteractionPromptAnchor_IsBoundedAroundTheAimedSurface` | PASS |
| action sentence is `Нажмите <binding>, чтобы <action>` | `InteractionActionInstruction_UsesRequestedSentenceStyle` | PASS |

Runtime source builds for `MSC.Player.Runtime`, `MSC.Needs.Runtime` and
`MSC.UI.Presentation.Runtime` completed with zero errors after these changes.
The focused Unity runs passed `22/22` EditMode and `20/20` PlayMode; reports are
`TestResults/HUD_TargetAnchor_EditMode.xml` and
`TestResults/HUD_TypographyIcons_PlayMode.xml`.

## 2026-08-09 Helvetica Neue and icon-import follow-up

The bundled UI face was replaced with the project owner's Helvetica Neue Roman
asset and applied to every `GameUiRoot` route plus interaction, subtitle and
development-console surfaces. `HUD_Helvetica_PlayMode.xml` passed `20/20`,
including an all-route font assertion. The focused font/glyph and six-icon
import contracts in `HUD_Helvetica_Icons_Focused_EditMode.xml` passed `2/2`.

The enclosing seven-test `UIPresentationSourceContractTests` run passed `6/7`;
its unrelated existing menu-logo contract still expects `600 x 337` while the
current logo asset is `1024` wide. That discrepancy was not changed as part of
the gameplay typography/icon follow-up.

## 2026-08-10 prompt and responsive-safe-frame follow-up (historical)

Interaction prompt rows now have explicit no-wrap styles, normalized whitespace,
two-pixel Helvetica metric reserve and natural action casing. NPC interaction
uses `Поговорить`; the beer case and bottle expose Russian authored titles.
Focused EditMode coverage passed `24/24` in
`UI_Prompts_Responsive_EditMode.xml`.

The prompt formatting portion is superseded by the 2026-09-02 lower-left action
stack. The responsive centred-safe-frame evidence remains applicable.

`GameUiRoot` now uses `CanvasScaler.ScreenMatchMode.Expand` around a fixed,
centred `1672 x 941` route frame. The PlayMode contract checks every active
uGUI `Graphic` across main menu, settings, pause, dialogs and HUD against the
actual viewport bounds. Results:

- `1156 x 722`: `20/20` passed;
- `1024 x 768`: focused `2/2` passed;
- `2560 x 1080`: focused `2/2` passed.
