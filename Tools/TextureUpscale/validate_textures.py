from __future__ import annotations

import argparse
import math
from pathlib import Path
from typing import Any, Sequence

import numpy as np
from PIL import Image

from pipeline_common import (
    INVENTORY_JSON_PATH,
    PROJECT_ROOT,
    PYTHON_VALIDATION_PATH,
    REPORT_ROOT,
    calculate_comparison_metrics,
    estimate_vram_bytes,
    latest_backup_manifest,
    load_config,
    parse_meta_guid,
    read_json,
    run_unity_method,
    sha256_file,
    utc_now_iso,
    write_csv,
    write_json_atomic,
)


VALIDATION_CSV_PATH = REPORT_ROOT / "validation_python.csv"
VRAM_JSON_PATH = REPORT_ROOT / "vram_report.json"
VRAM_MARKDOWN_PATH = REPORT_ROOT / "vram_report.md"


def validate_normal_map(path: Path) -> dict[str, Any]:
    with Image.open(path) as image:
        rgb = np.asarray(image.convert("RGB"), dtype=np.float32)
    vectors = rgb / 127.5 - 1.0
    lengths = np.linalg.norm(vectors, axis=2)
    return {
        "containsNaN": bool(np.isnan(lengths).any()),
        "meanLength": float(lengths.mean()),
        "meanLengthError": float(np.abs(lengths - 1.0).mean()),
        "minimumLength": float(lengths.min()),
        "maximumLength": float(lengths.max()),
        "meanVector": vectors.mean(axis=(0, 1)).tolist(),
    }


def alpha_stats(path: Path) -> dict[str, Any]:
    with Image.open(path) as image:
        if "A" not in image.getbands():
            return {"hasAlpha": False, "coverage": 1.0, "extrema": [255, 255]}
        alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
    return {
        "hasAlpha": True,
        "coverage": float(np.mean(alpha > 127)),
        "extrema": [int(alpha.min()), int(alpha.max())],
    }


def validate_operation(operation: dict[str, Any], inventory_by_path: dict[str, dict[str, Any]]) -> dict[str, Any]:
    asset_path = str(operation["assetPath"])
    current = PROJECT_ROOT / asset_path
    current_meta = Path(str(current) + ".meta")
    backup = Path(operation["backupPath"])
    result: dict[str, Any] = {
        "assetPath": asset_path,
        "category": operation.get("category", ""),
        "status": "Pass",
        "failures": [],
        "warnings": list(operation.get("warnings", [])),
    }
    if not current.exists():
        result["failures"].append("Current texture is missing.")
        result["status"] = "Fail"
        return result
    if not current_meta.exists():
        result["failures"].append("Unity .meta is missing.")
    current_guid = parse_meta_guid(current_meta)
    if current_guid != str(operation.get("guid", "")).lower():
        result["failures"].append(f"GUID changed: {operation.get('guid')} -> {current_guid}")
    if operation.get("newSha256") and sha256_file(current) != operation["newSha256"]:
        result["failures"].append("Current texture hash differs from the applied manifest.")
    if not backup.exists():
        result["failures"].append("Backup texture is missing.")
        result["status"] = "Fail"
        return result

    try:
        with Image.open(backup) as source_image:
            source = source_image.copy()
        with Image.open(current) as current_image:
            processed = current_image.copy()
    except Exception as exc:
        result["failures"].append(f"Image decode failed: {type(exc).__name__}: {exc}")
        result["status"] = "Fail"
        return result

    expected_size = tuple(int(value) for value in operation.get("newSize", []))
    if expected_size and processed.size != expected_size:
        result["failures"].append(f"Unexpected size: {processed.size} != {expected_size}")
    result["sourceMode"] = source.mode
    result["processedMode"] = processed.mode
    result["sourceSize"] = list(source.size)
    result["processedSize"] = list(processed.size)
    result["comparison"] = calculate_comparison_metrics(source, processed)

    source_alpha = alpha_stats(backup)
    processed_alpha = alpha_stats(current)
    result["sourceAlpha"] = source_alpha
    result["processedAlpha"] = processed_alpha
    if source_alpha["hasAlpha"] and not processed_alpha["hasAlpha"]:
        result["failures"].append("Alpha channel was lost.")
    if source_alpha["hasAlpha"]:
        coverage_delta = abs(float(source_alpha["coverage"]) - float(processed_alpha["coverage"]))
        result["alphaCoverageDelta"] = coverage_delta
        if coverage_delta > 0.04:
            result["warnings"].append(f"Alpha coverage changed by {coverage_delta:.4f}.")

    if operation.get("category") == "Normal Map":
        normal = validate_normal_map(current)
        result["normal"] = normal
        tolerance = float(load_config().get("validation", {}).get("normalMeanLengthTolerance", 0.025))
        if normal["containsNaN"] or normal["meanLengthError"] > tolerance:
            result["failures"].append(
                f"Normal vectors invalid: NaN={normal['containsNaN']}, mean length error={normal['meanLengthError']:.6f}."
            )

    inventory = inventory_by_path.get(asset_path, {})
    compression = str(inventory.get("textureCompression", "Unknown"))
    mipmaps = bool(inventory.get("mipmaps", False))
    result["estimatedVramBytesBefore"] = estimate_vram_bytes(
        source.width,
        source.height,
        source_alpha["hasAlpha"],
        mipmaps,
        compression,
    )
    result["estimatedVramBytesAfter"] = estimate_vram_bytes(
        processed.width,
        processed.height,
        processed_alpha["hasAlpha"],
        mipmaps,
        compression,
    )
    if result["failures"]:
        result["status"] = "Fail"
    elif result["warnings"]:
        result["status"] = "PassWithWarnings"
    return result


def write_vram_report(results: Sequence[dict[str, Any]]) -> None:
    by_category: dict[str, dict[str, int]] = {}
    before_total = 0
    after_total = 0
    for result in results:
        before = int(result.get("estimatedVramBytesBefore", 0))
        after = int(result.get("estimatedVramBytesAfter", 0))
        before_total += before
        after_total += after
        category = str(result.get("category", "Unknown"))
        bucket = by_category.setdefault(category, {"before": 0, "after": 0, "delta": 0})
        bucket["before"] += before
        bucket["after"] += after
        bucket["delta"] = bucket["after"] - bucket["before"]
    top = sorted(
        (
            {
                "assetPath": result["assetPath"],
                "category": result["category"],
                "before": int(result.get("estimatedVramBytesBefore", 0)),
                "after": int(result.get("estimatedVramBytesAfter", 0)),
                "delta": int(result.get("estimatedVramBytesAfter", 0)) - int(result.get("estimatedVramBytesBefore", 0)),
            }
            for result in results
        ),
        key=lambda value: value["after"],
        reverse=True,
    )[:20]
    payload = {
        "schemaVersion": 1,
        "generatedAtUtc": utc_now_iso(),
        "beforeBytes": before_total,
        "afterBytes": after_total,
        "deltaBytes": after_total - before_total,
        "growthRatio": (after_total / before_total) if before_total else 0,
        "byCategory": by_category,
        "top20": top,
        "note": "Editor-side estimate derived from source dimensions, mipmap policy and importer compression class; not measured GPU residency.",
    }
    write_json_atomic(VRAM_JSON_PATH, payload)
    lines = [
        "# Texture upscale VRAM estimate",
        "",
        f"- Before: {before_total / 1048576:.2f} MiB",
        f"- After: {after_total / 1048576:.2f} MiB",
        f"- Delta: {(after_total - before_total) / 1048576:.2f} MiB",
        f"- Growth: {payload['growthRatio']:.2f}x",
        "",
        "| Category | Before MiB | After MiB | Delta MiB |",
        "|---|---:|---:|---:|",
    ]
    for category, values in sorted(by_category.items()):
        lines.append(
            f"| {category} | {values['before'] / 1048576:.2f} | "
            f"{values['after'] / 1048576:.2f} | {values['delta'] / 1048576:.2f} |"
        )
    lines.extend(["", "This is an import-policy estimate, not measured GPU residency.", ""])
    VRAM_MARKDOWN_PATH.write_text("\n".join(lines), encoding="utf-8")


def validate_latest(run_unity: bool, create_comparisons: bool) -> int:
    backup_manifest_path = latest_backup_manifest()
    backup_manifest = read_json(backup_manifest_path)
    inventory_payload = read_json(INVENTORY_JSON_PATH)
    inventory_by_path = {entry["assetPath"]: entry for entry in inventory_payload.get("textures", [])}
    operations = [
        value
        for value in backup_manifest.get("operations", [])
        if value.get("status") in {"UnityImported", "Applied"}
    ]
    results = [validate_operation(operation, inventory_by_path) for operation in operations]
    failures = sum(1 for result in results if result["status"] == "Fail")
    payload = {
        "schemaVersion": 1,
        "generatedAtUtc": utc_now_iso(),
        "backupManifestPath": str(backup_manifest_path),
        "validatedCount": len(results),
        "failureCount": failures,
        "warningCount": sum(1 for result in results if result["status"] == "PassWithWarnings"),
        "textures": results,
    }
    write_json_atomic(PYTHON_VALIDATION_PATH, payload)
    write_csv(
        VALIDATION_CSV_PATH,
        ["assetPath", "category", "status", "failures", "warnings", "sourceSize", "processedSize", "estimatedVramBytesBefore", "estimatedVramBytesAfter"],
        (
            {
                **result,
                "failures": " | ".join(result.get("failures", [])),
                "warnings": " | ".join(result.get("warnings", [])),
                "sourceSize": "x".join(str(value) for value in result.get("sourceSize", [])),
                "processedSize": "x".join(str(value) for value in result.get("processedSize", [])),
            }
            for result in results
        ),
    )
    write_vram_report(results)

    if create_comparisons:
        from create_comparison_sheets import create_all_comparisons

        create_all_comparisons(backup_manifest_path)
    if run_unity:
        unity = run_unity_method(
            "MSC.Editor.TextureUpscale.TextureUpscaleValidator.ValidateBatch",
            "texture_sample_validate.log",
        )
        if unity.returncode != 0:
            failures += 1
    return failures


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Validate the latest applied texture sample.")
    parser.add_argument("--skip-unity", action="store_true")
    parser.add_argument("--skip-comparisons", action="store_true")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    failures = validate_latest(not args.skip_unity, not args.skip_comparisons)
    print(f"Validation failures: {failures}; report: {PYTHON_VALIDATION_PATH}")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
