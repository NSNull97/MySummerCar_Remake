# Texture Upscale bounded sample — 2026-08-01

## Outcome

- Unity-authoritative audit: 600 textures, 536 materials, 63 duplicate-payload entries.
- Bounded sample: 25 unique source textures staged and compared.
- Automatically accepted and applied: 14.
- Withheld for manual review: 11.
- Python validation: 14 passed, 0 warnings, 0 failures.
- Unity validation: 14 passed, 0 warnings, 0 failures.
- Restore proof: the first applied backup restored 14/14 image and `.meta` hashes exactly; the accepted sample was then reapplied and validated again.
- Current reversible backup: `Backups/TextureUpscale/20260801_185213/manifest.json`.

No mass apply was executed.

## Coverage limitation

The current `Assets` tree contains no `Multi-Sprite Atlas`, no texture assigned to a vehicle material or stored in a vehicle texture path, and only two game UI textures. One of those UI files is a concurrently modified 08A reference-lock logo. The sample therefore records these shortages instead of falsely relabelling unrelated assets:

- Multi-Sprite Atlas: 1 missing.
- UI Sprite: 3 missing.
- Vehicle: 2 missing.

The sample was filled to 25 with six explicitly labelled `Coverage Fallback` textures. See `sample_selection.json` for the authoritative selection and shortage record.

## Local processing methods

- `channel-safe-mask-resample`: four HDRP mask maps; RGBA channels processed independently.
- `vector-normal-resample`: four normal maps; decoded vectors resampled and renormalized.
- `alpha-aware-conservative-pillow`: sixteen alpha/cutout/albedo assets; premultiplied RGB and alpha handled separately.
- `conservative-pillow`: one opaque albedo fallback.
- Repeat assets used wrapped padding and seam-regression metrics.

No external dependency was installed. The run used the Codex bundled Python runtime with Pillow 12.2 and NumPy 2.3.5. No approved Real-ESRGAN executable/model was present, so the sample used the explicitly reported deterministic fallback and does not claim AI upscaling. Mass RGB upscale is withheld when an approved local AI backend is absent.

## Applied textures

1. `Assets/Game/Presentation/Materials/Proof/Textures/ControlledProofPaint_Mask.png`
2. `Assets/Game/Presentation/Materials/GaragePrototype/Textures/M3_Bark_Mask.png`
3. `Assets/Game/Presentation/Materials/GaragePrototype/Textures/M3_Concrete_Mask.png`
4. `Assets/Game/Presentation/Materials/Proof/Textures/ControlledProofPaint_Normal.png`
5. `Assets/Game/Presentation/Materials/GaragePrototype/Textures/M3_Foliage_BaseColor.png`
6. `Assets/Game/Presentation/Materials/GaragePrototype/Textures/M3_Foliage_Mask.png`
7. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_1bb21905ad6633843889f4dc298b2c6c_color.png`
8. `Assets/Game/LegacyImport/RuntimeBaseline/Characters/Source/Texture2D/char_face01.png`
9. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Phase1JobLocations/Textures/3da72f11c3dccaf4094498d3342d2a01.png`
10. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_8dc4a3e69f40c0243ae94a3fa2d85461_color.png`
11. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_ac6f5a6ba24f61c4486229c86c2fe0c3_color.png`
12. `Assets/Game/Presentation/Materials/Proof/Textures/ControlledProofPaint_BaseColor.png`
13. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_148710e4d9642944fb00fe40cc63df59_color.png`
14. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_26887fe73cb2f5446a4188d0810021e5_color.png`

## Manual-review sample outputs

1. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_154257df59a5e35448854c0f183a91d1_normal.png`
2. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_187790253f9b5e04094fba1ab6db7acf_normal.png`
3. `Assets/Game/Presentation/Materials/GaragePrototype/Textures/M3_Foliage_Normal.png`
4. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_0dda65a63ac2f2743a5a008ce48347fe_color.png`
5. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_44a59ad488d3ed6419b183a3a652cb20_color.png`
6. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_6971c87b918f8d1409f952e8fed51530_color.png`
7. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_a4a9da16af26a654786a322537de8765_color.png`
8. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_3b07495536861a14581e90244c3cb895_color.png`
9. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_6702bb7dccbacf848aa38e162c47fb4c_color.png`
10. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_a0a60236cd1f5714387993bd01e845b3_color.png`
11. `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Textures/M06B2_1241424523776b54e9980002948a3f41_color.png`

The files above remain unchanged in `Assets`; their staged outputs and metrics are available for review in `process_manifest.json` and `Comparisons/index.html`.

## VRAM estimate for the applied sample

- Before: 1.03 MiB.
- After: 6.52 MiB.
- Delta: +5.49 MiB.
- Growth: 6.32x.

This is an importer-policy estimate, not measured GPU residency.

## Full command after separate approval

Place an approved local Real-ESRGAN-compatible executable/model under `Tools/TextureUpscale/models`, then run:

```powershell
$TexturePython = 'C:\Users\NSNull\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $TexturePython Tools\TextureUpscale\upscale_textures.py --scan --process --apply --validate --allow-mass-apply
```

## Exact rollback command for the current sample

```powershell
$TexturePython = 'C:\Users\NSNull\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $TexturePython Tools\TextureUpscale\restore_backup.py --manifest 'E:\GAYmDev_Studio\MySummerCar_Remake\Backups\TextureUpscale\20260801_185213\manifest.json'
```
