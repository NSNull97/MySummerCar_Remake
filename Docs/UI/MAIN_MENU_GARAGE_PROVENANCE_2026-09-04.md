# Main-menu garage plate provenance — 2026-09-04

**Superseded / user-rejected presentation.** The user subsequently rejected the
real Satsuma rendered against this photographic garage and requested a fully 3D
location from the existing game. The replacement is the home-yard mesh preview
documented in `MAIN_MENU_HOME_ENVIRONMENT.md`. Its production menu path must
have no photo dependency. The replacement's generation and Bootstrap wiring have
passed; graphical and runtime validation remain pending. This file preserves historical image provenance rather
than declaring the photographic version accepted or current.

The user explicitly requested matching the garage/car composition after reviewing
our first UI revision, then requested the existing real Satsuma geometry in place
of the painted car. That intermediate version used an **empty garage** image
with the car rendered separately from the project's existing sanitized model.
It was menu presentation only, with no change to the gameplay world.

Generation mode: built-in `image_gen.imagegen`, reference edit; no CLI/API fallback.
Reference and intermediate files are review/staging inputs outside Assets.
No logo, button, palette or other UI pixels are in that historical plate.

| Stage | File | SHA-256 |
|---|---|---|
| User reference | `References/UI/Requested/2026-09-04/MainMenu_Reference.png` | `2422ad4677091eb248133b836265d19fbc062cd50057c7f3d890f6bd89cc7ce1` |
| Intermediate garage/car, UI removed | `Artifacts/MainMenuRedesign/ReferenceBackground/MainMenu_Garage_ReferenceClean.png` | `1a1caeae345be7a0408e5791cb1d99b0e4f8bab313b032f9f8964b028c76fa0c` |
| Superseded empty garage | `Assets/Game/UI/Presentation/Content/MainMenu/M08A_TemporaryGarageBackdrop.png` | `5f6116b754eee2a302c00b61d63cb371a6ad714d76128f36fc14177b83c2bb9e` |

The historical runtime filename and asset GUID
`97fb9a0a46d3b264e90d602527e24ec1` are retained to preserve serialized references.
The previous runtime image was backed up under
`Artifacts/MainMenuRedesign/ReferenceBackground/Previous_M08A_Garage.png`.
The original generated outputs remain in Codex's generated-images directory.
The garage is a static backplate, not a reconstructed playable 3D location.
Visual approval belongs to the user; generated-image inspection alone is not
Unity render validation.

## Prompt 1: remove UI, preserve garage/car composition

```text
Use case: precise-object-edit.
Asset type: clean high-resolution background plate for a Unity main menu, wide 16:9, ideally 3840x2160.
Input image 1 is the user's target visual reference. Create a faithful clean cinematic garage background from this reference. The requested output is ONLY the garage and automobile environment, with all user interface removed and the covered scenery convincingly reconstructed. Keep the same camera angle, composition and automobile proportions as the reference: worn orange late-1970s Japanese two-door compact hatchback at centre-left, open hood with visible engine, front three-quarter view, rectangular headlights, black grille, chrome bumper and black steel wheels. Keep the large warm circular ceiling lamp, the thin illuminated circular line in the polished wet concrete floor around the car, dark workshop tool cabinets at left, warm-lit trophy shelves and sofa/plant behind the car, industrial black walls, and the open glass garage door at right with blue dusk and orange sunset outside. Preserve the reference's realistic rich reflections, warm interior light against cool exterior dusk, worn metal and automotive detail.
Remove completely: the large colourful game logo at top left; the greeting/smiley panel at top right; all five right-side menu buttons and every UI icon/text; the paint palette panel at lower left; all bottom utility buttons. Do not leave any UI remnants, blurred-out rectangles, colour swatches, panel outlines, new logos or watermarks. Reconstruct the real wall, garage opening and floor behind those UI regions. Keep the small in-world neon wall sign reading exactly 'Drive / Fix / Repeat' as part of the garage.
Invariants: one automobile, open hood, reference camera/framing, ceiling light ring, illuminated floor ring, garage furnishings and doorway. Do not substitute a different garage or car, do not make the car larger or more central than in the reference. The original scene composition needs to leave the same top-left, lower-left and right-side space for separately rendered interactive UI. Render a clean finished background plate with no interface, not a screenshot mockup. Preserve a 16:9 landscape canvas.
```

## Prompt 2: remove the painted car for real geometry

```text
Remove the orange car completely from this garage image, including open hood, wheels, undercarriage, its cast shadow, and its reflection. Reconstruct the garage floor and any obscured garage furniture seamlessly. Keep the camera perspective, exact architecture, ceiling ring light, floor light ring, neon Drive Fix Repeat sign, tools, cabinets, shelves, warm indoor lighting and cool blue sunset windows unchanged. This is an EMPTY GARAGE photographic clean plate for a real 3D car to be placed onto the lit floor circle later. Floor inside the circle must be cleanly empty concrete with only existing natural lighting reflections; no car-shaped shadow, no car-shaped reflection. Preserve full image 16:9 composition. No UI, logo, controls, additional text, people, or new objects. Photorealistic, matching input precisely except removing the car.
```
