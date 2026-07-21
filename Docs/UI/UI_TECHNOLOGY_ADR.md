# ADR — Milestone 08A UI technology

Status: `Accepted for 08A`  
Date: 2026-07-18

## Decision

Use first-party Unity uGUI (`com.unity.ugui` 2.0.0) with the Unity Input System
for the bounded 08A runtime UI. Build the presentation from project-owned
components and procedural sprites under dedicated `MSC.UI.*` assemblies. Use
Editor-only tooling to load approved PNG references and generate comparisons.

## Context

The repository has no coherent runtime UI framework: no UXML/USS, Canvas,
EventSystem, UI scene, UI prefab or TMP presentation exists. Existing `OnGUI`
code is limited to a crossdot and development diagnostics. The approved 08A
screens require precise 1672×941 composition, context-owned backdrops,
keyboard/mouse/gamepad navigation and deterministic image capture.

uGUI is already pinned and is therefore not a new dependency. Its RectTransform
layout maps directly to the locked pixel geometry, while Screen Space Camera
mode supports off-screen review capture. UI Toolkit remains a valid future
option for tool-heavy screens, but adopting it simultaneously would create two
runtime navigation/focus systems without a concrete need.

## Boundaries

- `MSC.UI.Runtime`: routes, capabilities, localization IDs, settings DTOs,
  persistence, transaction state and vendor-neutral view models.
- `MSC.UI.Presentation.Runtime`: uGUI views, theme, navigation, Input System
  adapters and live data binding.
- `MSC.UI.Editor`: reference validation, overlay, canonical capture and review
  artifact generation. It is the only assembly allowed to read
  `References/UI/Approved/08A/`.
- `MSC.Bootstrap.Runtime`: explicit composition only. UI never references the
  Bootstrap assembly.

No screen depends directly on Wwise, Enviro, donor hierarchies, scene names or
reference PNGs. Settings persistence is versioned and separate from game saves.

### 2026-07-19 backdrop amendment

Backdrop ownership is intentionally route-specific: a static project-owned
menu plate, stable dark HUD blocks and a one-shot frozen pause frame. This
replaces the earlier generic live-camera-background assumption without changing
the uGUI/Input System technology decision.

### 2026-07-20 startup and scale amendment

The static menu plate is owned by a full-canvas aspect-preserved backdrop
outside the accessible `UiScale` root. Production world composition may reach
ready state, but `IGameplaySessionGate` keeps gameplay camera/environment
activation dormant until New Game. This is an additive Bootstrap/UI boundary;
it does not introduce a front-end scene, replace the streamer or change the
uGUI/Input System decision.

### 2026-07-20 Gaussian filtering amendment

The earlier `1/2 -> 1/4 -> 1/8` downsample/upscale pyramid and recurring HUD
capture are removed. Static menu glass and one-shot pause use the project-owned
`Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader`
with four horizontal/vertical iterations at radii `2 / 4 / 6 / 8`. The shader
is a serialized `ProductionUiInstaller` dependency passed through
`GameUiDependencies`; `Shader.Find` is prohibited. HUD uses a stable dark
translucent surface so camera look cannot trigger capture-driven frame loss.

## Consequences

- Exact locked-screen geometry is practical and testable.
- Runtime UI compiles without Editor/reference content.
- The existing Bootstrap remains build index 0; no front-end scene shifts the
  frozen world-streaming build indices.
- Initial boot prepares the production world before revealing the menu, but
  gameplay remains dormant until explicit session activation. Loading progress
  is phase-based because the current streamer exposes readiness, not granular
  progress.
- The unidentified concept font is not silently imported; the bounded built-in
  font fallback is documented in the deviations register.
- The Gaussian shader is included by explicit serialized reference rather than
  runtime string lookup, and pause pays capture/filter cost only once on entry.

## Rejected alternatives

- Shipping the approved PNGs as backgrounds: violates the reference lock and
  makes controls/data non-live.
- IMGUI runtime screens: inadequate focus/navigation/layout and styling model.
- Adding a third-party UI framework/font/icon pack: not approved and unnecessary
  for the milestone.
- Creating a new menu scene before Bootstrap: shifts build indices relied on by
  world-streaming manifests and validators.
