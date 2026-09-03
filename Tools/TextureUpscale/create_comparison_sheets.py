from __future__ import annotations

import argparse
import html
from pathlib import Path
from typing import Any, Sequence

from PIL import Image, ImageDraw, ImageFont

from pipeline_common import (
    COMPARISON_ROOT,
    INVENTORY_JSON_PATH,
    PROCESS_MANIFEST_PATH,
    PROJECT_ROOT,
    latest_backup_manifest,
    read_json,
)


PANEL_WIDTH = 360
PANEL_HEIGHT = 300
HEADER_HEIGHT = 34
SHEET_WIDTH = PANEL_WIDTH * 4
SHEET_HEIGHT = (PANEL_HEIGHT + HEADER_HEIGHT) * 3


def _font() -> ImageFont.ImageFont:
    try:
        return ImageFont.truetype("segoeui.ttf", 18)
    except OSError:
        return ImageFont.load_default()


def _contain(image: Image.Image, size: tuple[int, int], nearest: bool = False) -> Image.Image:
    copy = image.copy()
    resample = Image.Resampling.NEAREST if nearest else Image.Resampling.LANCZOS
    copy.thumbnail(size, resample)
    canvas = Image.new("RGBA", size, (38, 40, 44, 255))
    x = (size[0] - copy.width) // 2
    y = (size[1] - copy.height) // 2
    canvas.paste(copy.convert("RGBA"), (x, y), copy.convert("RGBA"))
    return canvas


def _center_crop(image: Image.Image, width: int, height: int) -> Image.Image:
    width = min(width, image.width)
    height = min(height, image.height)
    left = (image.width - width) // 2
    top = (image.height - height) // 2
    return image.crop((left, top, left + width, top + height))


def _alpha_preview(image: Image.Image) -> Image.Image:
    if "A" not in image.getbands():
        return Image.new("RGBA", image.size, (255, 255, 255, 255))
    alpha = image.getchannel("A")
    return Image.merge("RGBA", (alpha, alpha, alpha, Image.new("L", image.size, 255)))


def _channel_preview(image: Image.Image, channel: int) -> Image.Image:
    rgba = image.convert("RGBA")
    value = rgba.getchannel(channel)
    zero = Image.new("L", rgba.size, 0)
    opaque = Image.new("L", rgba.size, 255)
    channels = [zero, zero, zero, opaque]
    channels[channel if channel < 3 else 0] = value
    if channel == 3:
        channels = [value, value, value, opaque]
    return Image.merge("RGBA", tuple(channels))


def _tiled_preview(image: Image.Image) -> Image.Image:
    tile = image.convert("RGBA")
    canvas = Image.new("RGBA", (tile.width * 3, tile.height * 3))
    for y in range(3):
        for x in range(3):
            canvas.paste(tile, (x * tile.width, y * tile.height))
    return canvas


def _panel(sheet: Image.Image, index: int, title: str, image: Image.Image, nearest: bool = False) -> None:
    column = index % 4
    row = index // 4
    x = column * PANEL_WIDTH
    y = row * (PANEL_HEIGHT + HEADER_HEIGHT)
    draw = ImageDraw.Draw(sheet)
    draw.rectangle((x, y, x + PANEL_WIDTH, y + HEADER_HEIGHT), fill=(23, 25, 28, 255))
    draw.text((x + 10, y + 7), title, fill=(235, 237, 240, 255), font=_font())
    contained = _contain(image, (PANEL_WIDTH, PANEL_HEIGHT), nearest=nearest)
    sheet.paste(contained, (x, y + HEADER_HEIGHT))


def _comparison_paths(operation: dict[str, Any]) -> tuple[Path, Path]:
    if operation.get("backupPath"):
        return Path(operation["backupPath"]), PROJECT_ROOT / operation["assetPath"]
    return PROJECT_ROOT / operation["assetPath"], PROJECT_ROOT / operation["processedPath"]


def create_sheet(operation: dict[str, Any], inventory: dict[str, Any]) -> Path:
    original_path, processed_path = _comparison_paths(operation)
    with Image.open(original_path) as source_image:
        original = source_image.copy()
    with Image.open(processed_path) as processed_image:
        processed = processed_image.copy()

    source_crop = _center_crop(original, min(256, original.width), min(256, original.height))
    processed_crop = _center_crop(
        processed,
        min(256 * int(operation.get("scaleFactor", 1)), processed.width),
        min(256 * int(operation.get("scaleFactor", 1)), processed.height),
    )
    source_200 = _center_crop(original, min(128, original.width), min(128, original.height)).resize((256, 256), Image.Resampling.NEAREST)
    processed_200 = _center_crop(
        processed,
        min(128 * int(operation.get("scaleFactor", 1)), processed.width),
        min(128 * int(operation.get("scaleFactor", 1)), processed.height),
    ).resize((256, 256), Image.Resampling.NEAREST)

    sheet = Image.new("RGBA", (SHEET_WIDTH, SHEET_HEIGHT), (46, 48, 52, 255))
    panels: list[tuple[str, Image.Image, bool]] = [
        ("Original", original, False),
        ("Processed", processed, False),
        ("Original 100% crop", source_crop, True),
        ("Processed 100% crop", processed_crop, True),
        ("Original 200% crop", source_200, True),
        ("Processed 200% crop", processed_200, True),
        ("Original alpha", _alpha_preview(original), False),
        ("Processed alpha", _alpha_preview(processed), False),
    ]
    category = str(operation.get("category", ""))
    if category == "HDRP Mask Map":
        labels = ("R Metallic", "G AO", "B Detail", "A Smoothness")
        panels.extend((label, _channel_preview(processed, index), False) for index, label in enumerate(labels))
    elif category == "Normal Map":
        panels.extend(
            [
                ("Normal original", original.convert("RGB"), False),
                ("Normal processed", processed.convert("RGB"), False),
            ]
        )
    elif inventory.get("isTileable"):
        panels.extend(
            [
                ("Original tile 3x3", _tiled_preview(original), False),
                ("Processed tile 3x3", _tiled_preview(processed), False),
            ]
        )
    while len(panels) < 12:
        panels.append(("", Image.new("RGBA", (1, 1), (46, 48, 52, 255)), False))
    for index, (title, image, nearest) in enumerate(panels[:12]):
        _panel(sheet, index, title, image, nearest)

    safe_name = operation["assetPath"].replace("/", "__").replace("\\", "__")
    destination = COMPARISON_ROOT / f"{safe_name}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(destination, format="PNG", optimize=True)
    return destination


def create_all_comparisons(manifest_path: Path) -> list[Path]:
    COMPARISON_ROOT.mkdir(parents=True, exist_ok=True)
    manifest = read_json(manifest_path)
    inventory_payload = read_json(INVENTORY_JSON_PATH)
    inventory_by_path = {entry["assetPath"]: entry for entry in inventory_payload.get("textures", [])}
    if "operations" in manifest:
        operations = [
            value
            for value in manifest.get("operations", [])
            if value.get("status") in {"UnityImported", "Applied"}
            and Path(value.get("backupPath", "")).exists()
            and (PROJECT_ROOT / value["assetPath"]).exists()
        ]
        source_note = "Originals are read from the reversible backup; processed images are the current sample assets."
    else:
        applied_by_path: dict[str, dict[str, Any]] = {}
        try:
            backup_manifest = read_json(latest_backup_manifest())
            applied_by_path = {
                value["assetPath"]: value
                for value in backup_manifest.get("operations", [])
                if value.get("status") in {"UnityImported", "Applied"}
            }
        except FileNotFoundError:
            pass
        operations = []
        for value in manifest.get("textures", []):
            if (
                value.get("status") not in {"Accepted", "ManualReview"}
                or not value.get("processedPath")
                or not (PROJECT_ROOT / value["assetPath"]).exists()
                or not (PROJECT_ROOT / value["processedPath"]).exists()
            ):
                continue
            operation = dict(value)
            applied = applied_by_path.get(value["assetPath"])
            if applied and applied.get("sourceSha256") == value.get("sourceSha256"):
                operation["backupPath"] = applied.get("backupPath", "")
            operations.append(operation)
        source_note = (
            "Applied originals are read from the reversible backup; manual-review originals remain in Assets. "
            "All processed images come from the bounded sample."
        )
    outputs: list[Path] = []
    rows: list[str] = []
    for operation in operations:
        destination = create_sheet(operation, inventory_by_path.get(operation["assetPath"], {}))
        outputs.append(destination)
        relative = destination.relative_to(COMPARISON_ROOT).as_posix()
        rows.append(
            "<tr>"
            f"<td><code>{html.escape(operation['assetPath'])}</code></td>"
            f"<td>{html.escape(str(operation.get('category', '')))}</td>"
            f"<td>{html.escape(str(operation.get('backend', '')))}</td>"
            f"<td>{operation.get('scaleFactor', 1)}x</td>"
            f"<td><a href=\"{html.escape(relative)}\"><img src=\"{html.escape(relative)}\" loading=\"lazy\"></a></td>"
            "</tr>"
        )
    document = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Texture Upscale Sample</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;background:#17191c;color:#eef0f2;margin:24px}
table{border-collapse:collapse;width:100%}th,td{border:1px solid #3b3e44;padding:8px;vertical-align:top}
th{background:#25282d;position:sticky;top:0}img{width:480px;max-width:45vw;height:auto}code{white-space:normal}
</style></head><body>
<h1>Texture Upscale — bounded sample comparison</h1>
<p>""" + html.escape(source_note) + """</p>
<table><thead><tr><th>Asset</th><th>Category</th><th>Backend</th><th>Scale</th><th>Comparison</th></tr></thead>
<tbody>
""" + "\n".join(rows) + """
</tbody></table></body></html>
"""
    (COMPARISON_ROOT / "index.html").write_text(document, encoding="utf-8")
    return outputs


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Create sample comparison sheets and HTML report.")
    parser.add_argument("--manifest", type=Path)
    parser.add_argument(
        "--processed-sample",
        action="store_true",
        help="Use the staged process manifest, including manual-review outputs.",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    manifest = args.manifest or (PROCESS_MANIFEST_PATH if args.processed_sample else latest_backup_manifest())
    outputs = create_all_comparisons(manifest)
    print(f"Comparison sheets: {len(outputs)} -> {COMPARISON_ROOT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
