# Main-menu Satsuma presentation provenance

Date: 2026-09-04. Replacement key: `menu.satsuma-preview`.

Latest additive follow-up: `MAIN_MENU_VEHICLE_LIGHTS_2026-09-04.md` records the
four serialized night-lamp bindings and the generated rear-lens material-slot
partition. The source/render evidence below predates that follow-up. The
mesh-only component allowlist, seven paint surfaces and static display pose
remain unchanged.

## Scope and classification

The user authorized a real Satsuma model in the main menu. This preview reuses
the existing sanitized private Phase 1 vehicle presentation. No new donor
extraction or original-installation modification is part of this work.

The generated geometry and materials remain **TemporaryDirectImport**. The
exporter and menu paint component are project-owned **Reimplemented** code.
The preview is a static installed display pose, not a drivable vehicle,
suspension simulation, new parity verification, or ProductionReady art.

The source roster is the existing body shell plus 75 `PartsCar` and 39
`PartsMotor` entries: a 115-part source stock roster. The display selects 114:
`back-panel` occupies the single `mount.satsuma.panel-back` socket, while
`subwoofer-panel` is explicitly recorded as its excluded alternative. Both are
present in the source `PartsCar` inventory, so inventory membership alone does
not mean they can be installed simultaneously. The six `PartsGT` and five
`PartsExtra` alternatives are also excluded and listed in the generated report.
Interchangeable stock parts receive explicit compatible mounts, so an
alternative cannot occupy the same socket.
All required part placements must resolve; the exporter throws on missing
parts, ambiguous mounts, duplicate occupancy, or cyclic ownership.

## Source authority

| Record | Value |
| --- | --- |
| Canonical source prefab | `Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab` |
| Source prefab SHA-256 | `df1907cd72abf68db7bb6d871970959873f0752e58db0ef7dbe37ad9879a3e5c` |
| Existing manifest | `Assets/Game/LegacyImport/Manifests/Phase1SatsumaV1aManifest.json` |
| Manifest SHA-256 | `3bf4c2535bff9ce40520fe117220fd0ad1434765a8025aea0847a14dd524b4df` |
| Existing baseline builder | `11A-V1d.64` |
| Unity source dependency hash | `1189b60a6403ec31dea74bb2908adf02` |
| Existing locked GAME scene SHA-256 | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` |

The GAME hash is inherited provenance from the accepted manifest, not evidence
of a new donor read or extraction. Source-prefab and manifest hashes above were
read from the current local files. Successful export records them again together
with Unity's dependency hash, so changes to referenced source assets are visible.

## Presentation derivation

`MainMenuVehicleAuthoring` reads the canonical prefab asset and its explicit
`PartInstance`, `MountPointAuthoring`, owner-part, hinge, suspension, and paint
authoring references. It does not instantiate the gameplay prefab or call its
installer, assembly operations, save restoration, or physics controllers.

The exporter creates fresh transforms and renderers. It recursively composes
installed mount ownership while preserving the existing mesh/material sources.
Front struts and steering rods use their authored installed two-bone presentation
at the existing full-droop pose, baked once into static meshes. Rear springs fit
the existing corner targets; the two shock halves follow their authored upper
and lower targets in the static rear arm pose. The hood uses its recorded local
X hinge and -87-degree open angle. These are display choices, not a loaded
vehicle-equilibrium result.

Seven explicit paint bindings are preserved: `body`, `door-left`, `door-right`,
`fender-left`, `fender-right`, `hood`, and `bootlid`. Only those material slots
use the existing Regular paint profile. `MainMenuVehicleModel.ApplyPaint`
changes per-renderer material property blocks; it does not mutate canonical
shared materials, vehicle paint saves, or the gameplay vehicle. The component
has no Update/FixedUpdate loop.

## Output boundary and validation

Generated output is confined to:

```text
Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/MenuPreview/
  SatsumaMenuPreview.prefab
  SatsumaMenuPreviewReport.json
  Meshes/InstalledSuspension*.asset
```

This payload is ignored by Git, as confirmed with `git check-ignore` for the
preview prefab. Keep it outside version control. Commit project-owned authoring,
runtime code, this document, and ledger/audit metadata only.

The prefab component allowlist is `Transform`, `MeshFilter`, `MeshRenderer`, and
one root `MainMenuVehicleModel`. No Rigidbody, Collider, Joint, Animator, camera,
light, audio component, gameplay controller, save identity, or donor script may
be retained. The generation report records selected part IDs, mounts, owners,
paint material slots, source hashes, bounds, unique mesh count, renderer count,
and missing-data failures. Canonical vehicle assets are not overwritten.

Unity 6000.3.11f1 authoring succeeded with exit code 0. The executed entry point
was `MSC.UI.EditorTools.MainMenuPreviewInstallation.BuildAndWire`; its log confirms
the generated model was wired and build scene configuration was untouched.
The exporter validated both its transient output and the saved prefab against
the component allowlist and seven-surface contract. Every selected part had to
contribute a copied renderer. Rear installed-presentation references are explicit;
the loose inventory's inactive flag cannot silently omit a selected part.

| Successful report field | Recorded value |
| --- | --- |
| Source stock roster including shell | 115 |
| Installed display parts including shell | 114 |
| Explicit exclusions | 12: subwoofer panel, six GT, five Extra |
| Static renderers | 249 |
| Unique meshes | 166 |
| Paint surfaces | 7, each using material slot 0 |
| Missing-data entries | 0 |
| Local bounds centre, metres | `(-0.0315075, 0.2456365, -0.0145178)` |
| Local bounds size, metres | `(1.5466359, 1.5643450, 3.7056034)` |
| Generated report SHA-256 | `059785540913ab4eabab0b82c8a08de70b00bd25e9fff5ccb2ea21fc5920bc4f` |
| Generated prefab SHA-256 | `90df1a17dfd84232a64322acaa1ea49c1c4a757621c266671b48587662735f72` |
| Authoring log SHA-256 | `575b1a3455aba97b4826b65dfd71825eca9537498b4505a029296fb5e4b055ef` |

Evidence: `Artifacts/MainMenuRedesign/VehiclePreview.Authoring.04.log` and the
generated `SatsumaMenuPreviewReport.json`. Source prefab and manifest hashes
were checked again after export and remained unchanged. The isolated source
compile also passed. This is generation and structural validation; visual
framing, paint visibility, and menu lifecycle acceptance remain pending.

Authoring command: `Tools/My Summer Car/UI/Main Menu/Rebuild Satsuma Mesh Preview`
(`MSC.UI.EditorTools.MainMenuVehicleAuthoring.Build`). This is an explicit local
generation step; there is no runtime donor dependency or automatic extraction.

## Replacement and compatibility

A future authored production model replaces `menu.satsuma-preview` through the
menu's explicit model reference while preserving the seven paint surface names
and framing/bounds contract. The canonical vehicle, assembly IDs, gameplay
prefabs, physics, and save schema are unchanged by this preview export.
