# Interaction Hover Outline — 2026-08-12

## Scope

Presentation-only outline for the currently actionable interaction candidate.
The visual and transition behavior was checked against the user's licensed
local installation of Cheap Car Repair (`Auto Fuszerka`) and six user-supplied
current-build screenshots.

The rejected first implementation used an HDRP shell shader. It produced
shader-error magenta and was removed completely. The runtime now uses the
user-purchased Easy Performant Outline 3.4.2 package, the same EPO family used
by the reference game.

## Direct reference evidence

Read-only Mono.Cecil inspection of
`Fuszerka_Data/Managed/Assembly-CSharp.dll` found
`Fuszerka.Interactables.Outlines.OutlineableEffect`:

- `Awake` resolves `EPOOutline.Outlinable`, disables it, and initializes
  `OutlineParameters.DilateShift` to `0`;
- enable sets the configured color, enables `Outlinable`, and tweens
  `DilateShift` from `0` to `1` over `0.2 s`;
- disable kills the tween, resets `DilateShift` to `0`, and disables
  `Outlinable`;
- no reference assembly or donor/reference code is loaded by the remake.

Evidence hashes:

- Cheap Car Repair `Assembly-CSharp.dll` SHA-256:
  `80B305144550642701B6C4747038C9A150BC1B6299A33CCB703E4BD09EC1E11F`;
- reference `EPO.dll` SHA-256:
  `9BF0D07B8D793C97C53C8AD4A30BDE78C18B76A09D3C9C7DDB5FA0EE46BCC98A`;
- reference `EPOURP.dll` SHA-256:
  `A0C86D566A3A3E38FAC18992BE46791A4A3A2909F33BB58E3EC3372A7A7D132E`.

## Licensed package boundary

The user supplied
`Easy Performant Outline 2D 3D URP HDRP and Built-in Renderer v3.4.2.unitypackage`
with SHA-256
`2CD3B6132A30A40EED5E46BCFCAB544A3D457D2145C230E4AD4308F5706DEFD5`.

Only the HDRP-capable runtime/resources and bounded inspector support were
locally imported under `Assets/Plugins/Easy performant outline`. Demo,
documentation, DOTween support, URP support, and setup wizard were excluded.
The licensed vendor payload and its root `.meta` are ignored by Git and must be
installed from the user's package on another workstation. Project-owned code
depends on the isolated `EPO.Runtime` assembly and `HDRP_OUTLINE` define.

## Runtime contract

- `PlayerInteractionController.CurrentCandidate` remains the only focus
  authority. There is no second raycast and no hierarchy-name dispatch.
- `InteractionCandidate.SourceCollider` records the exact registered collider
  hit by the interaction ray.
- `InteractionOutlinePresenter` resolves the source renderer set but never
  creates shell geometry or changes source materials/colliders.
- `EpoInteractionOutlineAdapter` binds that renderer set to one disabled
  `Outlinable`, uses white, zero blur, and the reference `0.2 s` grow-in.
- The first-person camera owns an HDRP custom pass and a native-resolution
  outliner with one dilation pass and no blur.
- `HdrpOutliner` is a project-owned compatibility subclass. It suppresses
  EPO 3.4.2's Editor-only per-frame built-in command-buffer maintenance;
  HDRP rendering remains owned by the package's `OutlineCustomPass`. The
  package may still emit its bounded teardown warning when the camera is
  destroyed in Editor tests.
- Source renderer count remains bounded at 32 per candidate.

## Partial-object outline

`InteractionTargetHost.ConfigureOutlineRenderers(...)` explicitly limits the
visual scope without changing gameplay capabilities or the clickable collider.
This supports handle-only door/vehicle interaction without brittle object-name
heuristics.

`ProductionWorldDoorInstaller` resolves the authored handle object by its donor
provenance stable ID and binds only its renderers. The enlarged handle trigger
stays easy to hit while only the physical handle is outlined. Doors without a
separate authored handle renderer retain the conservative existing fallback.
Vehicle interaction targets can use the same explicit renderer scope when their
handle targets are authored; the current vehicle baseline has no general
vehicle-door interaction host to bind yet.

## Presentation values

- color: white RGBA `(1, 1, 1, 1)`;
- intended border: approximately `3 px` at native resolution;
- grow-in: `0.2 s`, unscaled time;
- blur: disabled;
- occlusion: EPO/HDRP depth-aware outline, not an always-on-top shell.

## Validation

Automated EditMode coverage checks exact collider propagation, source
preservation, handle-only authored scope, EPO target binding and timing, and
player-prefab HDRP configuration. A PlayMode visual regression writes
`TestResults/EPO_InteractionOutline_Visual.png`, rejects shader-error magenta,
and requires a bounded white result.

Executed on Unity `6000.3.11f1`, D3D11 before the user's live Editor session
locked the project:

- focused EPO EditMode tests: `5/5` passed;
- focused HDRP visual PlayMode test: `1/1` passed; the generated frame contains
  a thin bounded white border and no shader-error magenta;
- player prefab build: passed, builder `1.5.0`;
- full project validator reached unrelated pre-existing duplicate stable-ID
  failures in vehicle/world-remaster content;
- current response-file compilation passes for `MSC.Interaction.Runtime`,
  `MSC.Player.Runtime`, `MSC.Presentation.InteractionOutline.EPO`, and
  `MSC.Tests.PlayMode`.

## Compatibility

This is an additive presentation adapter. Existing input, capabilities, stable
IDs, door motion/collision, carried-item interaction, and save DTOs are not
renamed or replaced. The vendor asset is removable only together with this
explicit presentation integration; gameplay state remains independent from it.
