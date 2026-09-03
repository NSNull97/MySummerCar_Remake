# Texture Upscale Pipeline

Local, category-aware and reversible texture processing for the Unity 6 HDRP
project. The pipeline scans Unity `TextureImporter` data and material texture
properties, creates a 25-texture review sample, stages processed images, backs
up source images plus `.meta` files, applies replacements atomically, reimports
through Unity and validates GUID/import compatibility.

The configuration file is JSON syntax stored in a `.yaml` file. JSON is valid
YAML 1.2, so the core workflow does not require a YAML package.

## Safety defaults

- No mass apply is implicit. `--process` only writes under
  `Reports/TextureUpscale/Processed`.
- `--apply --sample` is restricted to the generated sample selection.
- A non-sample apply additionally requires `--allow-mass-apply`; this guard is
  implemented for a later approved run and was not used while building the
  bounded sample.
- Every applied file and `.meta` is copied to
  `Backups/TextureUpscale/<timestamp>` first.
- Restore refuses to overwrite a file whose current hash differs from the
  recorded processed hash unless `--force` is supplied.
- Vendor assets, lightmaps, cubemaps, font atlases and reference-only donor
  files are excluded from automatic application by default.
- Normal maps are decoded/resampled/renormalized. HDRP mask channels are
  handled independently. Alpha is resized separately from RGB.
- Repeat textures use wrapped padding and seam regression checks.

## Runtime

The current Codex desktop runtime already provides compatible NumPy and Pillow:

```powershell
$TexturePython = 'C:\Users\NSNull\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $TexturePython Tools\TextureUpscale\upscale_textures.py --scan
```

For a standalone environment, create a virtual environment and install the
small requirements file after approval:

```powershell
python -m venv Tools\TextureUpscale\.venv
Tools\TextureUpscale\.venv\Scripts\python -m pip install -r Tools\TextureUpscale\requirements.txt
```

No model or third-party executable is bundled. If an approved local
`realesrgan-ncnn-vulkan.exe` plus its model files are placed in `models/`, the
pipeline detects and uses it for eligible RGB albedo/emission images. Otherwise
it uses the explicitly reported `conservative-pillow` fallback; it never labels
that fallback as AI.

## Commands

```powershell
# Unity importer/material export followed by the enriched inventory.
& $TexturePython Tools\TextureUpscale\upscale_textures.py --scan

# Select the bounded 25-texture sample and show intended operations.
& $TexturePython Tools\TextureUpscale\upscale_textures.py --sample --dry-run

# Stage the sample without touching Assets.
& $TexturePython Tools\TextureUpscale\upscale_textures.py --sample --process

# Back up, atomically apply only accepted sample items, then Unity reimport.
& $TexturePython Tools\TextureUpscale\upscale_textures.py --sample --apply

# Python and Unity validation plus comparison sheets.
& $TexturePython Tools\TextureUpscale\upscale_textures.py --sample --validate

# Restore the most recent applied manifest.
& $TexturePython Tools\TextureUpscale\upscale_textures.py --restore

# Full run after separate approval and after installing an approved local
# Real-ESRGAN-compatible executable/model. The explicit guard prevents an
# accidental mass apply.
& $TexturePython Tools\TextureUpscale\upscale_textures.py --scan --process --apply --validate --allow-mass-apply
```

All commands accept the bounded filters `--category`, `--include`, `--exclude`,
`--max-size`, `--scale`, `--device`, `--tile-size` and `--force`. The direct
scripts (`scan_textures.py`, `validate_textures.py`,
`create_comparison_sheets.py`, `restore_backup.py`) expose the corresponding
focused operations.

## Reports

Generated files live under `Reports/TextureUpscale`:

- `texture_inventory.csv` / `.json`;
- `manual_review.csv`;
- `sample_selection.json`;
- `process_manifest.json`;
- `validation_python.json` / `.csv`;
- `validation_unity.json`;
- `vram_report.json` / `.md`;
- `Comparisons/index.html` and per-texture sheets.

The inventory and reports are review evidence. Processed payloads and backups
remain local and are ignored by Git.

## Failure modes

- If Unity is open on the same project, batch export/reimport stops and logs the
  failure rather than using stale importer data silently.
- Unsupported/16-bit/suspicious assets are written to `manual_review.csv`.
- A failed Unity reimport after apply triggers restoration from the just-created
  backup before the command exits.
- A GUID change, missing material/texture, wrong normal-map type, lost alpha,
  incompatible sprite metadata or excessive metric/seam drift fails validation.
