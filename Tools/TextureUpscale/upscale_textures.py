from __future__ import annotations

import argparse
import json
import math
import os
import shutil
import subprocess
import tempfile
from pathlib import Path
from typing import Any, Sequence

import numpy as np
from PIL import Image, ImageFilter

from pipeline_common import (
    APPLY_REQUEST_PATH,
    BACKUP_ROOT,
    INVENTORY_JSON_PATH,
    PROCESS_MANIFEST_PATH,
    PROCESSED_ROOT,
    PROJECT_ROOT,
    SAMPLE_PATH,
    TOOL_ROOT,
    apply_config_rules,
    atomic_copy,
    calculate_comparison_metrics,
    choose_scale,
    ensure_report_directories,
    exclusive_lock,
    filter_entries,
    find_real_esrgan,
    latest_backup_manifest,
    load_config,
    parse_meta_guid,
    project_relative,
    read_json,
    run_unity_method,
    save_image_preserving_extension,
    sha256_file,
    sha256_text,
    timestamp_id,
    utc_now_iso,
    write_json_atomic,
)
from scan_textures import scan, select_sample


def _resize_float_channel(channel: np.ndarray, size: tuple[int, int], resample: Image.Resampling) -> np.ndarray:
    source = Image.fromarray(channel.astype(np.float32), mode="F")
    return np.asarray(source.resize(size, resample), dtype=np.float32)


def _resize_uint8_channel(channel: np.ndarray, size: tuple[int, int], resample: Image.Resampling) -> np.ndarray:
    source = Image.fromarray(channel.astype(np.uint8), mode="L")
    return np.asarray(source.resize(size, resample), dtype=np.uint8)


def _wrapped_pad(image: Image.Image, padding: int) -> Image.Image:
    if padding <= 0:
        return image.copy()
    array = np.asarray(image)
    if array.ndim == 2:
        padded = np.pad(array, ((padding, padding), (padding, padding)), mode="wrap")
    else:
        padded = np.pad(array, ((padding, padding), (padding, padding), (0, 0)), mode="wrap")
    return Image.fromarray(padded, mode=image.mode)


def _crop_scaled_padding(image: Image.Image, padding: int, scale: int, target_size: tuple[int, int]) -> Image.Image:
    if padding <= 0:
        return image.resize(target_size, Image.Resampling.LANCZOS) if image.size != target_size else image
    offset = padding * scale
    cropped = image.crop((offset, offset, offset + target_size[0], offset + target_size[1]))
    return cropped


def _preserve_rgb_mean(source: Image.Image, processed: Image.Image) -> Image.Image:
    source_rgba = np.asarray(source.convert("RGBA"), dtype=np.float32)
    result = np.asarray(processed.convert("RGBA"), dtype=np.float32).copy()
    source_mask = source_rgba[:, :, 3] > 8
    result_mask = result[:, :, 3] > 8
    if not np.any(source_mask) or not np.any(result_mask):
        return processed
    source_mean = source_rgba[:, :, :3][source_mask].mean(axis=0)
    result_mean = result[:, :, :3][result_mask].mean(axis=0)
    result[:, :, :3][result_mask] += source_mean - result_mean
    result = np.clip(np.rint(result), 0, 255).astype(np.uint8)
    return Image.fromarray(result, mode="RGBA")


def conservative_resize(image: Image.Image, target_size: tuple[int, int], scale: int, deblock: bool) -> Image.Image:
    working = image.convert("RGBA") if "A" in image.getbands() else image.convert("RGB")
    if deblock:
        working = working.filter(ImageFilter.GaussianBlur(radius=0.28))
    resized = working.resize(target_size, Image.Resampling.LANCZOS)
    percent = 28 if scale <= 1 else 38
    radius = 0.65 if scale <= 1 else min(1.25, 0.55 * scale)
    return resized.filter(ImageFilter.UnsharpMask(radius=radius, percent=percent, threshold=3))


def alpha_aware_resize(image: Image.Image, target_size: tuple[int, int], binary_alpha: bool) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.float32)
    alpha = rgba[:, :, 3] / 255.0
    premultiplied = rgba[:, :, :3] * alpha[:, :, None]
    resized_premultiplied = np.stack(
        [_resize_float_channel(premultiplied[:, :, channel], target_size, Image.Resampling.LANCZOS) for channel in range(3)],
        axis=2,
    )
    alpha_resample = Image.Resampling.NEAREST if binary_alpha else Image.Resampling.LANCZOS
    resized_alpha = _resize_float_channel(alpha, target_size, alpha_resample)
    if binary_alpha:
        resized_alpha = (resized_alpha >= 0.5).astype(np.float32)
    resized_alpha = np.clip(resized_alpha, 0.0, 1.0)
    denominator = np.maximum(resized_alpha[:, :, None], 1.0 / 255.0)
    rgb = resized_premultiplied / denominator
    transparent = resized_alpha < (0.5 / 255.0)
    if np.any(transparent):
        rgb[transparent] = 0.0
    output = np.concatenate((np.clip(rgb, 0, 255), resized_alpha[:, :, None] * 255.0), axis=2)
    return Image.fromarray(np.clip(np.rint(output), 0, 255).astype(np.uint8), mode="RGBA")


def normal_map_resize(image: Image.Image, target_size: tuple[int, int]) -> Image.Image:
    source = np.asarray(image.convert("RGBA"), dtype=np.float32)
    vector = source[:, :, :3] / 127.5 - 1.0
    resized = np.stack(
        [_resize_float_channel(vector[:, :, channel], target_size, Image.Resampling.BICUBIC) for channel in range(3)],
        axis=2,
    )
    lengths = np.linalg.norm(resized, axis=2, keepdims=True)
    resized /= np.maximum(lengths, 1e-6)
    rgb = np.clip(np.rint((resized + 1.0) * 127.5), 0, 255).astype(np.uint8)
    alpha = _resize_uint8_channel(source[:, :, 3], target_size, Image.Resampling.BICUBIC)
    return Image.fromarray(np.dstack((rgb, alpha)), mode="RGBA")


def _is_discrete_channel(channel: np.ndarray) -> bool:
    unique = np.unique(channel)
    if unique.size <= 4:
        return True
    extreme_fraction = float(np.mean((channel <= 3) | (channel >= 252)))
    return extreme_fraction >= 0.985


def mask_map_resize(image: Image.Image, target_size: tuple[int, int]) -> Image.Image:
    source = np.asarray(image.convert("RGBA"), dtype=np.uint8)
    channels: list[np.ndarray] = []
    for index in range(4):
        channel = source[:, :, index]
        resample = Image.Resampling.NEAREST if _is_discrete_channel(channel) else Image.Resampling.LANCZOS
        resized = _resize_uint8_channel(channel, target_size, resample)
        if resample == Image.Resampling.NEAREST and np.unique(channel).size <= 2:
            low, high = int(channel.min()), int(channel.max())
            midpoint = (low + high) / 2.0
            resized = np.where(resized >= midpoint, high, low).astype(np.uint8)
        channels.append(resized)
    return Image.fromarray(np.stack(channels, axis=2), mode="RGBA")


def run_real_esrgan(
    image: Image.Image,
    scale: int,
    config: dict[str, Any],
    tile_size: int,
    device: str,
) -> Image.Image:
    executable, model_directory = find_real_esrgan(config)
    if not executable or not model_directory:
        raise FileNotFoundError("Approved Real-ESRGAN NCNN backend/model is unavailable.")
    backend = config.get("backend", {})
    with tempfile.TemporaryDirectory(prefix="msc-texture-upscale-") as temporary_directory:
        temporary_root = Path(temporary_directory)
        input_path = temporary_root / "input.png"
        output_path = temporary_root / "output.png"
        image.convert("RGB").save(input_path, format="PNG")
        command = [
            str(executable),
            "-i",
            str(input_path),
            "-o",
            str(output_path),
            "-n",
            str(backend.get("modelName", "realesrgan-x4plus")),
            "-s",
            str(scale),
            "-t",
            str(tile_size),
            "-m",
            str(model_directory),
        ]
        if device.lower() == "cpu":
            command.extend(["-g", "-1"])
        result = subprocess.run(command, text=True, encoding="utf-8", errors="replace", capture_output=True, check=False)
        if result.returncode != 0 or not output_path.exists():
            raise RuntimeError(f"Real-ESRGAN failed with exit code {result.returncode}: {result.stderr[-1000:]}")
        with Image.open(output_path) as output:
            return output.convert("RGB").copy()


def process_one(
    entry: dict[str, Any],
    config: dict[str, Any],
    scale_override: int | None,
    max_size_override: int | None,
    tile_size: int,
    device: str,
    force: bool,
    sample_scope: bool,
) -> dict[str, Any]:
    source_path = PROJECT_ROOT / entry["assetPath"]
    scale = choose_scale(entry, config, scale_override)
    maximum_size = int(max_size_override or entry.get("maxSize") or config.get("defaultMaxSize", 4096))
    while scale > 1 and max(int(entry["width"]), int(entry["height"])) * scale > maximum_size:
        scale //= 2
    destination = PROCESSED_ROOT / entry["assetPath"]
    result: dict[str, Any] = {
        "assetPath": entry["assetPath"],
        "guid": entry.get("guid", ""),
        "category": entry["category"],
        "domains": entry.get("domains", []),
        "sampleBucket": entry.get("sampleBucket", ""),
        "sourceSha256": entry["sourceSha256"],
        "sourceMetaSha256": sha256_file(Path(str(source_path) + ".meta")) if Path(str(source_path) + ".meta").exists() else "",
        "sourceSize": [int(entry["width"]), int(entry["height"])],
        "scaleFactor": scale,
        "targetSize": [int(entry["width"]) * scale, int(entry["height"]) * scale],
        "processedPath": project_relative(destination),
        "backend": "",
        "modelName": "",
        "modelSha256": "",
        "parameters": {
            "tileSize": tile_size,
            "tileOverlap": int(config.get("tileOverlap", 32)),
            "device": device,
            "periodicPadding": bool(entry.get("isTileable")),
        },
        "status": "Pending",
        "warnings": [],
        "metrics": {},
    }
    if (entry.get("manualReview") or entry.get("excludedByConfig")) and not force:
        result["status"] = "ManualReview"
        result["warnings"].extend(entry.get("manualReviewReasons", []))
        if entry.get("excludedByConfig"):
            result["warnings"].append("Excluded by configuration.")
        return result
    if source_path.exists() and sha256_file(source_path) != entry.get("sourceSha256") and not force:
        result["status"] = "ManualReview"
        result["warnings"].append("Source hash changed after inventory/sample selection; rescan before processing.")
        return result
    if not entry.get("readableByPipeline"):
        result["status"] = "ManualReview"
        result["warnings"].append("Source format is not safely writable by the local pipeline.")
        return result
    if int(entry.get("bitDepth", 8)) > 8:
        result["status"] = "ManualReview"
        result["warnings"].append("Precision-preserving 16/32-bit path requires an explicit asset rule.")
        return result

    with Image.open(source_path) as opened:
        original = opened.copy()
    category = str(entry["category"])
    target_size = (int(entry["width"]) * scale, int(entry["height"]) * scale)
    padding = 0
    working = original
    if entry.get("isTileable"):
        padding = min(int(config.get("tileOverlap", 32)), max(2, original.width // 4), max(2, original.height // 4))
        working = _wrapped_pad(original, padding)
    padded_target = (working.width * scale, working.height * scale)

    backend_executable, model_directory = find_real_esrgan(config)
    ai_eligible = (
        category in {"Base Color / Albedo", "Emission", "Decal"}
        and not entry.get("disableAI")
        and not entry.get("hasAlpha")
        and scale in {2, 4}
        and backend_executable is not None
        and model_directory is not None
    )
    if (
        not sample_scope
        and category in {"Base Color / Albedo", "Emission", "Decal"}
        and scale > 1
        and not entry.get("disableAI")
        and not ai_eligible
    ):
        result["status"] = "ManualReview"
        result["warnings"].append(
            "Mass RGB upscale withheld: an approved local Real-ESRGAN executable/model is unavailable "
            "or this alpha-bearing asset needs a reviewed RGB/alpha AI path."
        )
        return result
    if category == "Normal Map":
        processed = normal_map_resize(working, padded_target)
        result["backend"] = "vector-normal-resample"
    elif category in {
        "HDRP Mask Map",
        "Metallic",
        "Roughness",
        "Smoothness",
        "Ambient Occlusion",
        "Height / Displacement",
        "Alpha Mask / Cutout",
    } and category != "Alpha Mask / Cutout":
        processed = mask_map_resize(working, padded_target)
        result["backend"] = "channel-safe-mask-resample"
    elif entry.get("hasAlpha") or category in {"UI Sprite", "Multi-Sprite Atlas", "Alpha Mask / Cutout"}:
        processed = alpha_aware_resize(working, padded_target, bool(entry.get("alphaIsBinary")))
        processed = processed.filter(ImageFilter.UnsharpMask(radius=0.55 if scale == 1 else 0.85, percent=24, threshold=3))
        result["backend"] = "alpha-aware-conservative-pillow"
    elif ai_eligible:
        processed = run_real_esrgan(working, scale, config, tile_size, device)
        result["backend"] = "realesrgan-ncnn-vulkan"
        result["modelName"] = str(config.get("backend", {}).get("modelName", ""))
        model_files = sorted(model_directory.glob(result["modelName"] + "*"))
        result["modelSha256"] = sha256_text("|".join(sha256_file(path) for path in model_files if path.is_file()))
    else:
        deblock = source_path.suffix.lower() in {".jpg", ".jpeg"}
        processed = conservative_resize(working, padded_target, scale, deblock=deblock)
        result["backend"] = "conservative-pillow"
        if category in {"Base Color / Albedo", "Emission", "Decal"} and scale > 1:
            result["warnings"].append("Approved AI model unavailable; used deterministic conservative fallback, not AI.")

    processed = _crop_scaled_padding(processed, padding, scale, target_size)
    if category in {"Base Color / Albedo", "Emission", "Decal"}:
        processed = _preserve_rgb_mean(original, processed)
    if not entry.get("hasAlpha") and category != "HDRP Mask Map":
        processed = processed.convert("RGB")

    save_image_preserving_extension(processed, destination, source_path.suffix)
    result["processedSha256"] = sha256_file(destination)
    result["processedBytes"] = destination.stat().st_size
    result["metrics"] = calculate_comparison_metrics(original, processed)
    validation = config.get("validation", {})
    metrics = result["metrics"]
    seam_regression = float(metrics["seamProcessed"]["normalized"] - metrics["seamOriginal"]["normalized"])
    result["metrics"]["seamRegression"] = seam_regression
    accepted = (
        metrics["ssimBlock8"] >= float(validation.get("minimumSsim", 0.86))
        and metrics["psnr"] >= float(validation.get("minimumPsnr", 24.0))
        and metrics["meanColorDelta"] <= float(validation.get("maximumMeanColorDelta", 5.0))
        and metrics["histogramL1"] <= float(validation.get("maximumHistogramL1", 0.14))
        and metrics["alphaCoverageDelta"] <= float(validation.get("maximumAlphaCoverageDelta", 0.04))
        and (not entry.get("isTileable") or seam_regression <= float(validation.get("maximumSeamRegression", 2.5)))
    )
    result["status"] = "Accepted" if accepted else "ManualReview"
    if not accepted:
        result["warnings"].append("Similarity, color, histogram or seam thresholds require manual review.")
    return result


def load_selection(sample: bool) -> list[dict[str, Any]]:
    if sample:
        if not SAMPLE_PATH.exists():
            inventory = read_json(INVENTORY_JSON_PATH).get("textures", [])
            select_sample(inventory, load_config())
        return read_json(SAMPLE_PATH).get("textures", [])
    return read_json(INVENTORY_JSON_PATH).get("textures", [])


def process_entries(
    entries: Sequence[dict[str, Any]],
    config: dict[str, Any],
    args: argparse.Namespace,
) -> dict[str, Any]:
    ensure_report_directories()
    filtered = filter_entries(entries, args.category, args.include, args.exclude)
    results = [
        process_one(
            entry,
            config,
            args.scale,
            args.max_size,
            args.tile_size or int(config.get("tileSize", 512)),
            args.device or str(config.get("device", "auto")),
            args.force,
            bool(args.sample),
        )
        for entry in filtered
    ]
    manifest = {
        "schemaVersion": 1,
        "generatedAtUtc": utc_now_iso(),
        "sampleOnly": bool(args.sample),
        "massApplyExplicitlyAuthorized": bool(args.allow_mass_apply),
        "configSha256": sha256_file(TOOL_ROOT / "texture_upscale_config.yaml"),
        "resultCount": len(results),
        "acceptedCount": sum(1 for result in results if result["status"] == "Accepted"),
        "manualReviewCount": sum(1 for result in results if result["status"] == "ManualReview"),
        "textures": results,
    }
    write_json_atomic(PROCESS_MANIFEST_PATH, manifest)
    return manifest


def _restore_applied_operations(manifest: dict[str, Any], force: bool = True) -> None:
    for operation in reversed(manifest.get("operations", [])):
        if operation.get("status") not in {"Applied", "UnityImported", "BackedUp"}:
            continue
        destination = PROJECT_ROOT / operation["assetPath"]
        backup_path = Path(operation["backupPath"])
        if backup_path.exists():
            atomic_copy(backup_path, destination)
        backup_meta = Path(operation.get("backupMetaPath", ""))
        destination_meta = Path(str(destination) + ".meta")
        if backup_meta.exists():
            atomic_copy(backup_meta, destination_meta)
        operation["status"] = "RestoredAfterFailure"


def apply_processed(args: argparse.Namespace) -> Path:
    if not args.sample and not args.allow_mass_apply:
        raise RuntimeError(
            "Mass apply requires the explicit --allow-mass-apply guard and separate user confirmation. "
            "Use --sample --apply for the bounded sample."
        )
    process_manifest = read_json(PROCESS_MANIFEST_PATH)
    if bool(process_manifest.get("sampleOnly")) != bool(args.sample):
        expected_scope = "sample" if args.sample else "full inventory"
        raise RuntimeError(
            f"The staged process manifest does not match the requested {expected_scope} apply scope. "
            "Run --process again with the same scope before applying."
        )
    accepted = filter_entries(
        [entry for entry in process_manifest.get("textures", []) if entry.get("status") == "Accepted"],
        args.category,
        args.include,
        args.exclude,
    )
    if not accepted:
        raise RuntimeError("No accepted processed sample textures are available to apply.")
    backup_directory = BACKUP_ROOT / timestamp_id()
    backup_directory.mkdir(parents=True, exist_ok=False)
    manifest_path = backup_directory / "manifest.json"
    manifest: dict[str, Any] = {
        "schemaVersion": 1,
        "createdAtUtc": utc_now_iso(),
        "sampleOnly": bool(args.sample),
        "projectRoot": str(PROJECT_ROOT),
        "status": "Applying",
        "operations": [],
    }
    write_json_atomic(manifest_path, manifest)

    with exclusive_lock("apply.lock"):
        try:
            for entry in accepted:
                source = PROJECT_ROOT / entry["assetPath"]
                source_meta = Path(str(source) + ".meta")
                processed = PROJECT_ROOT / entry["processedPath"]
                operation: dict[str, Any] = {
                    "assetPath": entry["assetPath"],
                    "guid": entry.get("guid", ""),
                    "category": entry["category"],
                    "sampleBucket": entry.get("sampleBucket", ""),
                    "sourceSha256": entry["sourceSha256"],
                    "sourceMetaSha256": entry.get("sourceMetaSha256", ""),
                    "processedSha256": entry["processedSha256"],
                    "sourceSize": entry["sourceSize"],
                    "newSize": entry["targetSize"],
                    "scaleFactor": entry["scaleFactor"],
                    "backend": entry["backend"],
                    "modelName": entry.get("modelName", ""),
                    "modelSha256": entry.get("modelSha256", ""),
                    "parameters": entry.get("parameters", {}),
                    "warnings": list(entry.get("warnings", [])),
                    "backupPath": str(backup_directory / entry["assetPath"]),
                    "backupMetaPath": str(backup_directory / (entry["assetPath"] + ".meta")),
                    "status": "Pending",
                }
                manifest["operations"].append(operation)
                if sha256_file(source) != entry["sourceSha256"] and not args.force:
                    operation["status"] = "SkippedConcurrentChange"
                    operation["warnings"].append("Source hash changed after processing.")
                    write_json_atomic(manifest_path, manifest)
                    continue
                backup_path = Path(operation["backupPath"])
                backup_meta_path = Path(operation["backupMetaPath"])
                atomic_copy(source, backup_path)
                if source_meta.exists():
                    atomic_copy(source_meta, backup_meta_path)
                operation["status"] = "BackedUp"
                write_json_atomic(manifest_path, manifest)
                atomic_copy(processed, source)
                operation["newSha256"] = sha256_file(source)
                operation["status"] = "Applied"
                write_json_atomic(manifest_path, manifest)

            applied_operations = [value for value in manifest["operations"] if value["status"] == "Applied"]
            apply_request = {
                "schemaVersion": 1,
                "backupManifestPath": str(manifest_path),
                "textures": [
                    {
                        "assetPath": value["assetPath"],
                        "expectedGuid": value["guid"],
                        "category": value["category"],
                        "scaleFactor": value["scaleFactor"],
                        "newWidth": value["newSize"][0],
                        "newHeight": value["newSize"][1],
                        "updateSpritePpu": value["category"] == "UI Sprite" and value["scaleFactor"] > 1,
                        "updateSpriteSheet": value["category"] == "Multi-Sprite Atlas" and value["scaleFactor"] > 1,
                    }
                    for value in applied_operations
                ],
            }
            write_json_atomic(APPLY_REQUEST_PATH, apply_request)
            if not applied_operations:
                raise RuntimeError("All candidate sources changed concurrently; nothing was applied.")
            unity = run_unity_method(
                "MSC.Editor.TextureUpscale.TextureUpscaleApply.ApplyBatch",
                "texture_sample_apply.log",
            )
            if unity.returncode != 0:
                _restore_applied_operations(manifest)
                manifest["status"] = "RolledBackAfterUnityFailure"
                write_json_atomic(manifest_path, manifest)
                run_unity_method(
                    "MSC.Editor.TextureUpscale.TextureUpscaleApply.ReimportBatch",
                    "texture_sample_rollback_reimport.log",
                )
                raise RuntimeError(
                    "Unity reimport failed; sample files were restored. "
                    "See Tools/TextureUpscale/logs/texture_sample_apply.log"
                )
            for operation in applied_operations:
                current_meta = Path(str(PROJECT_ROOT / operation["assetPath"]) + ".meta")
                operation["newMetaSha256"] = sha256_file(current_meta) if current_meta.exists() else ""
                operation["currentGuid"] = parse_meta_guid(current_meta)
                operation["status"] = "UnityImported"
            manifest["status"] = "Applied"
            write_json_atomic(manifest_path, manifest)
        except Exception:
            raise
    return manifest_path


def print_dry_run(entries: Sequence[dict[str, Any]], config: dict[str, Any], args: argparse.Namespace) -> None:
    filtered = filter_entries(entries, args.category, args.include, args.exclude)
    for entry in filtered:
        scale = choose_scale(entry, config, args.scale)
        print(
            f"{entry.get('sampleBucket', entry['category']):<22} "
            f"{entry['assetPath']} {entry['width']}x{entry['height']} -> "
            f"{entry['width'] * scale}x{entry['height'] * scale} "
            f"repeat={entry.get('isTileable')} manual={entry.get('manualReview')}"
        )
    print(f"Planned textures: {len(filtered)}")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Local reversible Unity texture-upscale pipeline.")
    parser.add_argument("--scan", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--sample", action="store_true")
    parser.add_argument("--process", action="store_true")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--validate", action="store_true")
    parser.add_argument("--restore", action="store_true")
    parser.add_argument("--force", action="store_true")
    parser.add_argument(
        "--allow-mass-apply",
        action="store_true",
        help="Explicit guard for a non-sample apply; do not use without separate user approval.",
    )
    parser.add_argument("--category")
    parser.add_argument("--include", action="append", default=[])
    parser.add_argument("--exclude", action="append", default=[])
    parser.add_argument("--max-size", type=int)
    parser.add_argument("--scale", type=int, choices=(1, 2, 4))
    parser.add_argument("--device", default=None)
    parser.add_argument("--tile-size", type=int, default=None)
    parser.add_argument("--skip-unity-scan", action="store_true")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    if not any((args.scan, args.dry_run, args.process, args.apply, args.validate, args.restore)):
        raise SystemExit("Specify at least one command: --scan, --dry-run, --process, --apply, --validate or --restore.")
    config = load_config()
    ensure_report_directories()

    if args.scan:
        entries = scan(run_unity=not args.skip_unity_scan, allow_fallback=False)
        if args.sample:
            selected = select_sample(entries, config)
            if len(selected) != 25:
                raise RuntimeError(f"Sample selection is incomplete: {len(selected)}/25. Review {SAMPLE_PATH}.")

    if args.restore:
        from restore_backup import restore_manifest

        manifest = latest_backup_manifest()
        restore_manifest(manifest, force=args.force, reimport=True)
        print(f"Restored: {manifest}")

    entries: list[dict[str, Any]] = []
    if args.dry_run or args.process or args.apply:
        entries = load_selection(args.sample)
        if args.sample and len(entries) != 25:
            raise RuntimeError(f"Expected a 25-texture sample, found {len(entries)}.")
    if args.dry_run:
        print_dry_run(entries, config, args)
    if args.process:
        manifest = process_entries(entries, config, args)
        print(
            f"Processed {manifest['resultCount']} textures: accepted={manifest['acceptedCount']}, "
            f"manualReview={manifest['manualReviewCount']} -> {PROCESS_MANIFEST_PATH}"
        )
    if args.apply:
        backup_manifest = apply_processed(args)
        print(f"Applied sample with backup: {backup_manifest}")
    if args.validate:
        from validate_textures import validate_latest

        failures = validate_latest(run_unity=True, create_comparisons=True)
        print(f"Validation failures: {failures}")
        return 1 if failures else 0
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
