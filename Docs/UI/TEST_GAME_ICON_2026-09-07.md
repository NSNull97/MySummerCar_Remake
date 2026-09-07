# Temporary game application icon — 2026-09-07

The user requested a temporary game icon derived from the logo currently used
in the main menu. The resulting icon is assigned to the default application
slot and every Standalone application-icon slot in Unity 6000.6.0f1.

## Source and provenance

- Existing menu artwork: `Assets/Game/UI/Presentation/Content/MainMenu/M08A_MenuLogo.png`.
- Its SHA-256 remains `BB1A29C4381C64A0E50CEF9CC344B3B47FE496AB5E7B8CD842C6009BB9114B8E`.
- Method: built-in `image_gen` editing, followed by deterministic PNG/ICO
  resampling and container encoding with Windows System.Drawing.
- Classification: `ReauthoredTexture`, temporary internal team-test artwork.
- Replacement key: `ui.branding.application-icon`.
- This is an AI-derived adaptation, not a pixel-identical extraction. It retains
  the orange badge, lime/yellow lettering and the existing three-line phrase.
- The menu artwork, approved UI layouts, runtime code, scenes, stable IDs and
  save schema are unchanged. No donor source or runtime payload was imported.

## Files

| File | Purpose |
|---|---|
| `References/Branding/TestGameIcon_Source_2026-09-07.png` | Selected 1254 x 1254 generated RGBA master |
| `Assets/Game/UI/Presentation/Content/ApplicationIcon/TestGameIcon.png` | 1024 x 1024 RGBA Unity application icon |
| `References/Branding/MySummerCar_Remake_Test.ico` | Windows ICO with 16, 24, 32, 48, 64, 128 and 256 px frames |
| `Tools/Branding/Export-TestGameIcon.ps1` | Reproducible PNG/ICO packaging from the selected master |
| `Assets/Game/UI/Editor/TestGameIconAuthoring.cs` | Explicit menu/batch command for Unity import and assignment |
| `ProjectSettings/ProjectSettings.asset` | Default and Standalone icon references |

Unity-generated `.meta` files accompany the new asset folder, texture and Editor
script. The icon texture GUID is `ddbad30731446704894bd6fc10b04788`.

### SHA-256

- Generated master: `BE7A4518831A61E9E914C17059B812B044D7C419BAFA0080DD57352C3BB51DF2`.
- Unity PNG: `5F458392A30372BEB2EE85559A95453ACCC8A71E2395E1886CEA5D0876EEA0A0`.
- ICO: `67A2A8318EC28B5D6BBB71280E1B3D735E9C2EA4AD25C74195DF07B6D16A7FDA`.

## Reapply

Run `Tools/Branding/Export-TestGameIcon.ps1 -SourcePng
References/Branding/TestGameIcon_Source_2026-09-07.png` from the project root.
In Unity use **Tools > MSC Remake > Build > Apply Test Game Icon**.

Batch entry point: `MSC.UI.EditorTools.TestGameIconAuthoring.Apply`.
The command assigns only application icons; it does not build a Player.

## Executed verification

- Unity 6000.6.0f1: `-batchmode -nographics -quit -projectPath <project-root>
  -executeMethod MSC.UI.EditorTools.TestGameIconAuthoring.Apply
  -logFile <project-root>/Logs/test-game-icon-setup.log`.
- Unity imported the PNG at 1024 x 1024 with input alpha, no mipmaps and
  uncompressed default texture import; the command finished and Unity exited 0.
- API readback passed: default slot 128; Standalone slots 1024, 512, 256, 128,
  64, 48, 32 and 16. Report: `Logs/test-game-icon-setup.json`.
- All seven ICO frames decoded with their expected dimensions and transparent
  corners. Windows System.Drawing.Icon also decoded the 32 px icon.
- Visual review on light/dark backgrounds: `Logs/test-game-icon-preview.png`.
  The full lettering is too small to read at 16–24 px; the badge and colour
  silhouette remain identifiable. This is temporary test artwork.
- The pre-assignment snapshot already contains unrelated Unity 6.6 settings
  serialization changes relative to Git HEAD. Those changes are preserved.
  Only the API-authored application-icon block differs from that snapshot;
  all nine icon references resolve to the imported PNG. Whitespace validation
  against the snapshot passed. Commit preparation subsequently trimmed trailing
  whitespace from the empty default-icon target and `switchCaStoreFilePath`
  values; their values and behavior are unchanged.
- No new gameplay tests were added for this presentation-only setting.
- An actual Player executable was not built or inspected in this task.

Next check: build the planned private Windows x64 team-test Player and confirm
the executable/window icon in Windows. An already-built executable is unchanged.

## Generation prompts

Initial edit target: the existing main-menu PNG. Built-in prompt:

> Use case: background-extraction / precise-object-edit. Asset type: temporary Windows desktop game application icon, PNG RGBA with real transparent background, square 1024x1024. Edit target: the attached existing game's main-menu logo. Create the icon directly from this exact logo: retain the orange circular distressed metal badge, the bold yellow-to-lime green Cyrillic lettering, deep near-black purple outlines, slight rising tilt, and recognisable original composition. Preserve the exact existing three-line text: first line «ЕХАЙ», second line «БЛ*ДИНА» with the original yellow/green asterisk-shaped symbol between Л and Д, third line «ЕХАЙ». Do NOT translate, censor further, rewrite, add a subtitle, add TEST text, add a car, or invent a new logo. Extract the badge and letters from the photographic blurry gray/brown backdrop. Remove the surrounding smoke, rubble and sparks outside the badge so the outer contour is clean and reads on both dark and light desktops. Keep all letters fully visible and not clipped. Preserve the artwork character and text shapes as faithfully as possible. Fit the full cutout tightly in the square canvas while preserving proportions, centered optically, with roughly 4 percent transparent margin at the widest edges. Opaque badge and lettering, transparent exterior. Do not display a checkerboard or draw a square panel. This is a flat final icon asset, not a mockup or an icon shown on a desktop.

The first output had a baked checkerboard and was rejected. The selected output
was produced by this second built-in edit prompt, with that first output as input:

> Precise background extraction edit for an app icon. Keep this exact orange circular badge and all Cyrillic lettering unchanged. The supplied image has a mistakenly BAKED-IN white-and-gray checkerboard around the badge. REMOVE every pixel of that checkerboard background and output a true transparent PNG with an alpha channel (RGBA). Background pixels must have alpha=0. Preserve crisp antialiased edges and every letter. Preserve the square framing. Do not draw, simulate, paint, or show checkerboard squares; the background must actually be absent. No other changes. Transparent background, transparent-background sticker cutout, real alpha transparency.
