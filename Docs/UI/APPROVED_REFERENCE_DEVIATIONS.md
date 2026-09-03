# Milestone 08A — approved reference deviations

Status: `HistoricalBaselineVisuallyApproved /
Post08ACorrectionsVisualApprovalPending`
Date: `2026-09-02`

Only the user can close visual deviations. This document records deliberate,
truthful differences before implementation and is updated after captures.

| ID | Locked screen/slot | Required implementation difference | Reason/status |
|---|---|---|---|
| `UI08A-DEV-001` | All text | Use a Windows system-font preference chain with explicit Unity `LegacyRuntime.ttf` fallback | The concept font is unidentified; adding an external font is outside approved dependencies. Final approved typography asset replacement remains visual debt. |
| `UI08A-DEV-002` | All icons/logo | Use project-owned procedural shape icons and rebuilt text logo | Reference pixels may not be cropped, traced or shipped. |
| `UI08A-DEV-003` | Main-menu background/vehicle | Use a temporary project-owned static garage plate with an original generic 1970s compact and raised hood, owned by a full-canvas backdrop outside `UiScale` | The runtime no longer exposes or scales the gameplay scene behind the main menu. The plate preserves its aspect by cropping at viewport edges instead of shrinking with the accessible UI root. It contains no donor or approved-reference pixels and is explicitly `PrototypeOnly / ProjectAuthored`. |
| `UI08A-DEV-004` | Continue/Load | Visible but disabled with `Save storage unavailable` | `ISaveService` exposes activity only; slots, metadata and storage do not exist inside the current bounded implementation. |
| `UI08A-DEV-005` | Main-menu performance card | Show measured frame sample or `Awaiting sample`; no reference hp/Nm graph | Concept numbers are not authoritative telemetry. |
| `UI08A-DEV-006` | Interior/trunk and music import | Retain cards with explicit bounded unavailable status | No production preview renderer or music importer exists. |
| `UI08A-DEV-007` | Graphics advanced rows | DLSS, frame generation, ray tracing and unsupported granular quality rows disabled | Current HDRP project does not expose approved adapters/capabilities for them. |
| `UI08A-DEV-008` | Graphics performance preview | Remove the complete right-side performance-preview panel | Direct user correction supersedes this non-essential reference block. Runtime settings no longer present average FPS, 1% low or VRAM as part of the Graphics page. |
| `UI08A-DEV-009` | Audio buses | Voice, radio, weather, thunder and reverb remain disabled | M08 has no independent backend settings for these slots. UI never calls Wwise directly. |
| `UI08A-DEV-010` | Audio output/test | Retain truthful `System Default` output state, but remove the complete audio-test block | Direct user correction removes the diagnostic test surface; device enumeration and a guaranteed test event are still not part of `IAudioBackend`. The compact backend/profile status remains informational. |
| `UI08A-DEV-011` | Controls table | Inventory, Map, Journal and handbrake rows show `Unavailable` | These Input System actions do not exist; visual fidelity cannot manufacture gameplay. |
| `UI08A-DEV-012` | Gameplay profile/immersion/summary | Remove the three right-side cards instead of retaining disabled diagnostic summaries | Direct user correction supersedes these reference blocks. Backing profile/immersion/summary services do not exist, and the page now presents only actionable or truthfully disabled setting rows. |
| `UI08A-DEV-013` | Settings chrome across all categories | Keep logo, six-category navigation, Back and greeting in one invariant geometry; reduce and align category/main-menu buttons, align the greeting to the main action right edge, and change only selected state plus central/right content | Direct user clarification supersedes the per-screen shell offsets, omitted Gameplay greeting and oversized initial button geometry. |
| `UI08A-DEV-014` | HUD money/needs | Production view shows unavailable values until providers exist | Runtime has authoritative clock only; invented survival state would be false. Review captures use an Editor-only fixture. |
| `UI08A-DEV-015` | HUD date/day | Always compute both from authoritative clock | The concept's Saturday/28.06.1992 combination is internally inconsistent. |
| `UI08A-DEV-016` | HUD interaction affordance | Persistent debug overlay removed; crossdot becomes contextual only | Locked HUD forbids persistent prompts/clutter while interaction still needs a bounded look target. |
| `UI08A-DEV-017` | Accessibility/Mods and pause/dialogs | Minimal shared-style `ReferencePending` layouts | No approved screen-specific visual reference exists. |
| `UI08A-DEV-018` | Wider/narrower aspect ratios | Canonical UI frame remains centred; extra area shows the project backdrop | Exact pixel lock is defined only at 1672×941. |
| `UI08A-DEV-019` | Menu/HUD/pause backdrop treatment | Split the effect by context: sharp static menu plate with full-resolution Gaussian glass source, stable dark HUD blocks without capture/blur, and a one-shot Gaussian frozen pause backdrop with dark dim | The recurring HUD capture produced severe look-input stutter and was removed. Menu glass uses a one-time `1672x941` ARGBHalf Linear result; pause captures `1024x576` once and filters to `512x288` ARGBHalf. Gameplay remains sharp and HUD has no camera-capture cost. |
| `UI08A-DEV-020` | Main-menu car colour | Swatches are live, focusable, preserve a session-only pre-confirm selection and tint menu/settings glass by an 8% mix | Direct user correction reduces saturation so the selected colour remains a subtle glass cast. No production new-game/paint/save adapter exists in 08A, so selection cannot mutate a save or claim vehicle paint application. |
| `UI08A-DEV-021` | Gameplay/accessibility adapter rows | Keep required rows visible but disable settings whose consuming adapters do not exist | Prevents persisted schema fields from being presented as working gameplay/high-contrast/reduced-motion/toggle-hold behaviour. |
| `UI08A-DEV-022` | uGUI edge quality | Keep current uGUI/Text foundation but rasterize project-owned panels/icons at 4x coverage and enable pixel-perfect Canvas placement | Removes the dominant binary 24-32 px edge stair-stepping without adding a package or changing locked layout. Legacy `Text` remains a documented typography limitation pending a future approved TMP/font migration. |
| `UI08A-DEV-023` | Radius, border and panel/action separation | Use a clearly visible 12 px rounded surface family, a supersampled procedural 1.75 px rounded-ring border, 7 px compact-track radius, 10 px card/action gaps and a 14 px main-card-to-music gap | Direct user refinement requested stronger rounding, a clear continuous outline without clipped corners and visible separation where the initial cards, settings panels and action buttons touched. |
| `UI08A-DEV-024` | Startup session ownership | Keep additive gameplay scenes unloaded in the main menu; `New Game` prepares world streaming once through `IGameplaySessionPreparationGate`, then activates `IGameplaySessionGate` | Direct user correction requires the menu to be a true lightweight front end rather than a paused or preloaded gameplay session. The gates are explicit and idempotent; non-menu/headless startup keeps its automatic compatibility path. |
| `UI08A-DEV-025` | Settings diagnostics and actions | Remove Graphics performance preview, Audio test, Controls input preview and Gameplay profile/immersion/summary; use one equal-size `Apply / Reset / Cancel` row on settings pages | Direct user correction supersedes the diagnostic/reference-only blocks. Every settings edit, including control bindings, remains pending: Apply validates and persists, Reset restores defaults to Pending, Cancel restores Applied. |
| `UI08A-DEV-026` | Slider handles and transient toast | Render slider handles as true `16 x 16` circles and disable the whole toast object after its message duration | Direct user correction replaces vertically stretched white handles and prevents an empty toast panel from remaining on screen. |
| `UI08A-DEV-027` | Menu/pause blur algorithm and dependency | Replace the old `1/2 -> 1/4 -> 1/8` pyramid with four separable Gaussian H/V iterations at radii `2 / 4 / 6 / 8` | Filtering uses the project-owned `M08A_SeparableGaussianBlur.shader`, serialized explicitly through `ProductionUiInstaller` and `GameUiDependencies`; runtime code does not call `Shader.Find`. Focused authoring/EditMode/PlayMode, private build and native capture gates pass. |
| `UI08A-DEV-028` | Default gameplay HUD | Replace the upper-left glass cards with the user-supplied 2026-08-05 minimal HUD direction, then apply the direct screenshot correction: six vertically arranged needs at upper-left, clock at upper-right and money directly beneath it in `value MK` order; slightly enlarge typography and widen need tracks; retain shadows on text, icons, tracks, fills and amber indicators; exclude the reference speedometer/gear cluster. The optional FPS counter has no panel or line and reads as a pure-white number followed by amber `FPS`. | Direct user correction. The reference image omits Urine and Dirtiness, so the project retains both to preserve the established six-needs gameplay contract; the Russian Dirtiness label remains `НЕОПРЯТНОСТЬ`. No vehicle telemetry becomes persistent HUD content. The revised HUD requires a new canonical capture and user visual approval. |
| `UI08A-DEV-029` | Clock subline and FPS suffix | Place localized day/date fields on one baseline with a 4 px layout gap. Keep the FPS numeric field digits-only and render exactly one separate amber `FPS` suffix at the same 17 px size. | Direct screenshot correction: the previous right alignment created a large visual gap between day and date, while the shared performance formatter added a white `FPS` inside the numeric field and duplicated the separate amber suffix. The main-menu performance string retains its complete `value FPS` format. |
| `UI08A-DEV-030` | Graphics right-side area | Add a compact functional Camera card with horizontal FOV and gameplay-camera draw-distance sliders; retain the authored `120 degrees / 500 m` defaults and the existing shared settings transaction. | Direct user request on 2026-08-15. The primary locked Graphics rows do not move, the removed performance diagnostics stay absent, and far clip does not masquerade as a streaming/LOD control. A fresh Graphics capture and user visual reapproval are required. |
| `UI08A-DEV-031` | Context-action placement | Replace the 2026-08-09 projected target title/action blocks with the 2026-09-02 centre dot/open-palm/check/cross reticle and a lower-left stack of at most three compact action plaques. Remove world-projected titles and hide the action stack when no actions exist. | Direct user reference supersedes only the earlier interaction-prompt treatment. Explicit target names move to the bottom-centre dynamic text stack rather than disappearing. Runtime uses no reference pixels and requires a fresh canonical capture/user approval. |
| `UI08A-DEV-032` | Context-action behavior beyond the image | Add `ЛКМ — ОТПУСТИТЬ`, endpoint-aware signed wheel actions and an Alt-held replacement stack for H/M/N gestures; deliberately render no `ALT — ДОП. ДЕЙСТВИЯ` plaque. | Direct user corrections on 2026-09-02. Alt is intentionally undiscoverable in the HUD and does not gate the existing gesture inputs; it only reveals their plaques. |
| `UI08A-DEV-033` | Context-action legibility and combined text | Give binding labels lightly rounded outlined keycaps; use semantic mouse glyphs with active-button fills and adjacent scroll arrows; replace the broad outlined hand with a compact filled open palm; strengthen central icon halos; add a cross for explicit removal actions; combine the current target title and active subtitle in a collapsible bottom-centre two-row stack with differentiated typography. | Direct user corrections after in-Editor gameplay review on 2026-09-02. The title/subtitle source remains gameplay-owned, rows reserve no space when empty, and removal uses `IRemovalInteractionTarget` rather than localized-string matching. Lucide ISC and Material Symbols Apache-2.0 sources are preserved with licenses. |
| `UI08A-DEV-034` | Context text typography and localization | Use the same loaded font and `18 px` size for target names and subtitles, differentiating them with bold versus regular weight. Route target names, compact action labels, keycaps and common feedback through one `ru-RU`/`en-US` presentation catalog fed by stable localization keys. | Direct user correction after gameplay review on 2026-09-02. This removes undersized item titles, mixed-language prompts and donor/debug identifiers without changing gameplay definitions or interaction dispatch. Unknown authored names remain a compatibility fallback. |
| `UI08A-DEV-035` | Pickup reticle source | Replace the Material Symbols runtime hand with the original game's hash-locked `gui_uset` open palm as a private Phase 1 `TemporaryDirectImport`, retaining its white mass and black finger separators under the stronger project-owned halo. | Direct user approval on 2026-09-02. The tracked manifest records SHA-256 `7939FE87B8F6F3154595AFF54AE17B0B4A19EA7062DCE3D3281B254266BB62A2`, deterministic GUID and replacement key `ui.interaction.pickup-hand`; donor runtime logic is not imported and Phase 2 still requires a reauthored icon. |
| `UI08A-DEV-036` | Installed assembly target text | Hide the bottom-centre name row for every installed assembly part and its fasteners. Preserve only actions whose capability reports that they are currently valid; restoring the part to a loose state restores its localized title. | Direct user correction on 2026-09-02. A secured fixed lever now shows neither a title nor a false pickup/removal plaque, while an installed hinged door can still show its valid open/close actions. |

## AI/semantic artifacts corrected

- Main-menu duplicate `NOT IMPORTED` becomes one status plus one honest helper.
- Controls `Mop` is interpreted as `Map`, but remains unavailable until a Map
  action exists.
- Machine-specific `Realtek(R) Audio` is replaced by the actual capability or
  `System Default`.
- Audio/gameplay percentages, performance values, profile scores and save age
  are never copied as production facts.
- Need day/date, money and percentages come from live providers or explicitly
  marked review data.

## Visual acceptance state

- Reference integrity: `PASS`.
- Implementation captures: `PASS` (six locked files).
- 50% blended comparisons: `PASS` (six locked files).
- Separate unreferenced captures: `PASS` (six `ReferencePending` files).
- User visual approval: `PASS` for the historical 2026-07-20 composition;
  `PENDING` for the bounded `UI08A-DEV-028` HUD revision and the
  `UI08A-DEV-030` Graphics camera card, plus the `UI08A-DEV-031/032/033`
  context-action revision.
- Latest correction state: the static menu backdrop is full-canvas and outside
  `UiScale`; gameplay is prepared but dormant until New Game; settings diagnostics
  listed in `UI08A-DEV-025` are absent; every settings page uses the shared
  `Apply / Reset / Cancel` transaction row; glass uses an 8% colour mix; rounded
  surfaces use a procedural 1.75 px ring; slider handles are circular and the
  complete toast dismisses. The corrected implementation/reference review set
  was regenerated on 2026-07-20. Gaussian authoring, `33/33` EditMode,
  `11/11` PlayMode, private build and native capture gates pass. The user then
  accepted the corrected presentation, including blur/readability, so the
  canonical 16:9 Milestone 08A baseline is `VisuallyApproved`.
