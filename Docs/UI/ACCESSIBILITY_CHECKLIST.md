# Milestone 08A accessibility checklist

Status: `FoundationImplemented / FocusedPlayModePassed / ManualDeviceReviewPending`
Date: `2026-07-19`

`Implemented` below means present in current code. It does not mean manually
approved at every viewport or input device.

| Requirement | Status | Current evidence / remaining work |
|---|---|---|
| Keyboard UI navigation | `Implemented / FocusSmokePass` | uGUI `Selectable` + `InputSystemUIInputModule`; physical-device traversal remains manual |
| Gamepad UI navigation | `Implemented / BindingAndFocusSmokePass` | gamepad Pause binding and route focus automated; physical controller traversal remains manual |
| Mouse interaction | `Implemented / TestPending` | `GraphicRaycaster` and uGUI controls; complete-page smoke test pending |
| Visible focus state | `Implemented / VisualReviewPending` | centralized amber highlighted/selected theme states; final Main Menu capture shows distinct New Game focus |
| Initial focus | `Implemented` | first interactable `Selectable` selected on route open |
| Focus restoration | `Implemented / PlayModePass` | route-local selection is restored after confirmation/save-status return; invalid or disabled targets fall back to the first interactable control |
| No focus traps | `PartialPass` | controller-style directional focus, main-menu neighbor and all route initial-focus checks pass; exhaustive physical-device traversal remains manual |
| Cancel/back path | `Implemented / PlayModePass` | settings Back and confirmation Cancel return to the expected caller |
| UI scale | `FoundationImplemented / FocusedPlayModePass / ViewportTestPending` | schema allows 0.75-1.5; current safe presentation applies 0.85-1.25 to authored UI only, while the static menu backdrop remains full-canvas outside scale |
| 16:9 target | `CapturePass / VisuallyApproved` | canonical 1672x941 set accepted by user on 2026-07-20 |
| 16:10, ultrawide, 4:3 | `AutomatedViewportPass / ManualVisualReviewPending` | all-route graphic containment passes at `1156x722`, `1024x768` and `2560x1080` with the fixed canonical safe frame |
| High contrast | `SchemaOnly / AdapterPending` | visible row is disabled and labelled `Adapter Pending`; token-variant application pending |
| Reduced motion | `SchemaOnly / AdapterPending` | visible row is disabled and labelled `Adapter Pending`; bounded UI has no mandatory large motion |
| Toggle alternatives for hold actions | `SchemaOnly / AdapterPending` | visible row is disabled; gameplay/input adapter pending |
| Colour-independent status | `PartiallyImplemented` | HUD uses icon + label + bar + numeric value; broader warning-state audit pending |
| Subtitles | `FoundationImplemented` | setting persists and is sent through `IAudioBackend`; subtitle renderer/content pending |
| Captions | `FoundationImplemented` | setting persists and is sent through `IAudioBackend`; caption renderer/content pending |
| Reduced loud sounds | `FoundationImplemented` | setting reaches audio backend; subjective/manual validation pending |
| Text localization | `FoundationImplemented` | English/Russian catalog, runtime locale application and culture-aware format/plural foundation; formal string-table workflow pending |
| Font fallback | `FoundationImplemented / ProductionFontPending` | Windows chain is accepted only after the full bounded Latin/Cyrillic glyph set validates, with explicit `LegacyRuntime.ttf` fallback; final approved bundled font remains art work |
| Text resizing/reflow | `AspectPass / ScaleReviewPending` | aspect clipping is covered by the fixed safe frame; accessibility scaling above 100% still requires manual visual review |
| Non-colour disabled reason | `Implemented` | capability rows use explicit unavailable/adapter/reference text |
| Input rebind cancel | `Implemented / RollbackTestPass` | cancellation restores the previous override without partial mutation; physical Escape-device check remains manual |
| Input rebind conflict feedback | `Implemented / ConflictTestPass` | duplicate effective path is rejected by focused PlayMode coverage |
| UI navigation audio | `Implemented / RuntimeTestPending` | Navigate/Confirm project event IDs are posted only through `IAudioBackend` |

## Manual review route

For every locked page and the minimal Accessibility page:

1. Navigate all interactive controls with keyboard only.
2. Repeat with gamepad only.
3. Verify a visible focus state at every step.
4. Confirm disabled controls are skipped and explain why they are disabled.
5. Open and close the page repeatedly and confirm there is always an exit.
6. Test UI scale at the minimum, default and maximum exposed values.
7. Repeat at representative 16:9, 16:10, ultrawide and 4:3 viewports.
8. Confirm high contrast, reduced motion, toggle/hold and colour-cue controls
   remain disabled with `Adapter Pending`; test them only after consuming
   adapters are implemented.
9. Verify status rows remain understandable in greyscale.
10. Confirm pause/resume restores player/vehicle input and cursor state.

## Approval rule

Accessibility foundations do not waive the reference lock. A locked page may
adapt focus, contrast and scale without rearranging its 100% canonical
composition. No accessibility item or locked screen is user-approved until the
manual route is completed and recorded.
