from __future__ import annotations

import argparse
import json
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Callable, Sequence

from pipeline_common import (
    AUDITED_TEXTURE_EXTENSIONS,
    BACKUP_ROOT,
    INVENTORY_CSV_PATH,
    INVENTORY_JSON_PATH,
    MANUAL_REVIEW_PATH,
    PROJECT_ROOT,
    REPORT_ROOT,
    SAMPLE_PATH,
    UNITY_EXPORT_PATH,
    apply_config_rules,
    choose_scale,
    ensure_report_directories,
    estimate_vram_bytes,
    get_dirty_asset_paths,
    infer_category,
    infer_domains,
    is_repeat_texture,
    load_config,
    normalize_material_uses,
    parse_meta_guid,
    project_relative,
    read_json,
    read_source_image_info,
    run_unity_method,
    sha256_file,
    utc_now_iso,
    write_csv,
    write_json_atomic,
)


INVENTORY_FIELDS = [
    "assetPath",
    "guid",
    "extension",
    "sourceBytes",
    "sourceSha256",
    "duplicateGroup",
    "width",
    "height",
    "mode",
    "bitDepth",
    "hasAlpha",
    "alphaIsBinary",
    "textureType",
    "sRGB",
    "wrapMode",
    "wrapU",
    "wrapV",
    "wrapW",
    "filterMode",
    "anisoLevel",
    "mipmaps",
    "mipmapFilter",
    "mipMapsPreserveCoverage",
    "maxTextureSize",
    "textureCompression",
    "compressionQuality",
    "crunchedCompression",
    "platformOverrides",
    "spriteMode",
    "spritePixelsPerUnit",
    "spriteCount",
    "spriteRects",
    "spriteBorders",
    "materialUses",
    "category",
    "domains",
    "isTileable",
    "textureSetKey",
    "scalePolicy",
    "estimatedVramBytesBefore",
    "estimatedVramBytesAfterPolicy",
    "excludedByConfig",
    "dirtyWorkingTree",
    "manualReview",
    "manualReviewReasons",
    "readableByPipeline",
    "sourceReadError",
]


def export_unity_inventory() -> None:
    result = run_unity_method(
        "MSC.Editor.TextureUpscale.TextureInventoryExporter.ExportBatch",
        "texture_inventory_export.log",
    )
    if result.returncode != 0 or not UNITY_EXPORT_PATH.exists():
        raise RuntimeError(
            "Unity texture inventory export failed. "
            f"Exit code={result.returncode}; see Tools/TextureUpscale/logs/texture_inventory_export.log"
        )


def fallback_texture_records() -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for path in (PROJECT_ROOT / "Assets").rglob("*"):
        if not path.is_file() or path.suffix.lower() not in AUDITED_TEXTURE_EXTENSIONS:
            continue
        asset_path = project_relative(path)
        meta_path = Path(str(path) + ".meta")
        records.append(
            {
                "assetPath": asset_path,
                "guid": parse_meta_guid(meta_path),
                "textureType": "Unknown",
                "sRGB": None,
                "wrapMode": "Unknown",
                "wrapU": "Unknown",
                "wrapV": "Unknown",
                "wrapW": "Unknown",
                "filterMode": "Unknown",
                "anisoLevel": 0,
                "mipmaps": False,
                "mipmapFilter": "Unknown",
                "mipMapsPreserveCoverage": False,
                "maxTextureSize": 0,
                "textureCompression": "Unknown",
                "compressionQuality": 0,
                "crunchedCompression": False,
                "platformOverrides": [],
                "spriteMode": "None",
                "spritePixelsPerUnit": 0,
                "spriteCount": 0,
                "spriteRects": [],
                "spriteBorders": [],
                "materialUses": [],
                "unityExportUnavailable": True,
            }
        )
    return records


def infer_texture_set_key(asset_path: str) -> str:
    name = Path(asset_path).stem.lower()
    suffixes = (
        "_basecolor",
        "_albedo",
        "_diffuse",
        "_normal",
        "_nrm",
        "_maskmap",
        "_mask",
        "_emission",
        "_emissive",
        "_metallic",
        "_roughness",
        "_smoothness",
        "_ao",
        "_height",
    )
    for suffix in suffixes:
        if name.endswith(suffix):
            name = name[: -len(suffix)]
            break
    return (Path(asset_path).parent.as_posix() + "/" + name).lower()


def load_applied_output_hashes() -> dict[str, str]:
    applied: dict[str, str] = {}
    for manifest_path in sorted(BACKUP_ROOT.glob("*/manifest.json")):
        try:
            manifest = read_json(manifest_path)
        except (OSError, ValueError, json.JSONDecodeError):
            continue
        for operation in manifest.get("operations", []):
            if operation.get("status") not in {"Applied", "UnityImported"}:
                continue
            output_hash = str(operation.get("newSha256", ""))
            if output_hash:
                applied[output_hash] = str(manifest_path)
    return applied


def enrich_inventory(records: Sequence[dict[str, Any]], config: dict[str, Any]) -> list[dict[str, Any]]:
    dirty_paths = get_dirty_asset_paths()
    applied_output_hashes = load_applied_output_hashes()
    enriched: list[dict[str, Any]] = []
    hash_groups: dict[str, list[str]] = defaultdict(list)

    for unity_record in sorted(records, key=lambda value: str(value.get("assetPath", "")).lower()):
        asset_path = str(unity_record.get("assetPath", "")).replace("\\", "/")
        absolute_path = PROJECT_ROOT / asset_path
        if not absolute_path.exists() or absolute_path.suffix.lower() not in AUDITED_TEXTURE_EXTENSIONS:
            continue
        image_info = read_source_image_info(absolute_path)
        source_hash = sha256_file(absolute_path)
        material_uses = normalize_material_uses(unity_record)
        entry: dict[str, Any] = dict(unity_record)
        entry.update(image_info)
        entry.update(
            {
                "assetPath": asset_path,
                "guid": str(unity_record.get("guid") or parse_meta_guid(Path(str(absolute_path) + ".meta"))).lower(),
                "extension": absolute_path.suffix.lower(),
                "sourceBytes": absolute_path.stat().st_size,
                "sourceSha256": source_hash,
                "materialUses": material_uses,
                "domains": infer_domains(asset_path, material_uses),
                "dirtyWorkingTree": asset_path in dirty_paths or asset_path + ".meta" in dirty_paths,
                "textureSetKey": infer_texture_set_key(asset_path),
            }
        )
        entry["category"] = infer_category(entry, image_info)
        entry["isTileable"] = is_repeat_texture(entry)
        entry = apply_config_rules(entry, config)
        if source_hash in applied_output_hashes:
            entry["alreadyProcessed"] = True
            entry["previousUpscaleManifest"] = applied_output_hashes[source_hash]
            entry["scale"] = 1
            entry["disableAI"] = True
            entry["manualReview"] = True
            entry.setdefault("manualReviewReasons", []).append(
                "Already matches a previously applied texture-pipeline output; automatic re-upscale is disabled."
            )
        if not entry.get("readableByPipeline"):
            entry["manualReview"] = True
            entry.setdefault("manualReviewReasons", []).append(
                entry.get("sourceReadError") or f"Unsupported processing extension: {entry['extension']}"
            )
        if int(entry.get("bitDepth", 0)) > 8:
            entry["manualReview"] = True
            entry.setdefault("manualReviewReasons", []).append(
                f"{entry['bitDepth']}-bit source requires precision-specific review."
            )
        if entry["category"] == "Unknown / Manual Review":
            entry["manualReview"] = True
            entry.setdefault("manualReviewReasons", []).append("No authoritative Unity/material classification was found.")
        if entry["dirtyWorkingTree"]:
            entry["manualReview"] = True
            entry.setdefault("manualReviewReasons", []).append("Asset or .meta has a concurrent working-tree change.")

        scale = choose_scale(entry, config)
        entry["scalePolicy"] = scale
        compression = str(entry.get("textureCompression", "Unknown"))
        before = estimate_vram_bytes(
            int(entry.get("width", 0)),
            int(entry.get("height", 0)),
            bool(entry.get("hasAlpha")),
            bool(entry.get("mipmaps")),
            compression,
        )
        after = estimate_vram_bytes(
            int(entry.get("width", 0)) * scale,
            int(entry.get("height", 0)) * scale,
            bool(entry.get("hasAlpha")),
            bool(entry.get("mipmaps")),
            compression,
        )
        entry["estimatedVramBytesBefore"] = before
        entry["estimatedVramBytesAfterPolicy"] = after
        enriched.append(entry)
        hash_groups[source_hash].append(asset_path)

    for entry in enriched:
        group = hash_groups[entry["sourceSha256"]]
        entry["duplicateGroup"] = entry["sourceSha256"][:16] if len(group) > 1 else ""
        entry["duplicatePaths"] = group if len(group) > 1 else []
    return enriched


def flatten_for_csv(entry: dict[str, Any]) -> dict[str, Any]:
    result = dict(entry)
    for field in (
        "platformOverrides",
        "spriteRects",
        "spriteBorders",
        "materialUses",
        "domains",
        "manualReviewReasons",
    ):
        result[field] = json.dumps(result.get(field, []), ensure_ascii=False, separators=(",", ":"))
    return result


def write_inventory(entries: Sequence[dict[str, Any]], unity_authoritative: bool) -> None:
    payload = {
        "schemaVersion": 1,
        "generatedAtUtc": utc_now_iso(),
        "projectRoot": str(PROJECT_ROOT),
        "unityAuthoritative": unity_authoritative,
        "textureCount": len(entries),
        "duplicatePayloadCount": sum(1 for entry in entries if entry.get("duplicateGroup")),
        "categoryCounts": dict(Counter(str(entry["category"]) for entry in entries)),
        "domainCounts": dict(Counter(domain for entry in entries for domain in entry.get("domains", []))),
        "textures": list(entries),
    }
    write_json_atomic(INVENTORY_JSON_PATH, payload)
    write_csv(INVENTORY_CSV_PATH, INVENTORY_FIELDS, (flatten_for_csv(entry) for entry in entries))

    review_rows: list[dict[str, Any]] = []
    for entry in entries:
        if not (entry.get("manualReview") or entry.get("excludedByConfig")):
            continue
        review_rows.append(
            {
                "assetPath": entry["assetPath"],
                "guid": entry.get("guid", ""),
                "category": entry["category"],
                "domains": ";".join(entry.get("domains", [])),
                "excludedByConfig": entry.get("excludedByConfig", False),
                "dirtyWorkingTree": entry.get("dirtyWorkingTree", False),
                "reasons": " | ".join(entry.get("manualReviewReasons", [])),
            }
        )
    write_csv(
        MANUAL_REVIEW_PATH,
        ["assetPath", "guid", "category", "domains", "excludedByConfig", "dirtyWorkingTree", "reasons"],
        review_rows,
    )


def _eligible(entry: dict[str, Any], used_paths: set[str], used_hashes: set[str]) -> bool:
    return bool(
        entry.get("readableByPipeline")
        and not entry.get("excludedByConfig")
        and not entry.get("manualReview")
        and not entry.get("dirtyWorkingTree")
        and entry.get("assetPath") not in used_paths
        and entry.get("sourceSha256") not in used_hashes
        and int(entry.get("width", 0)) > 0
        and int(entry.get("height", 0)) > 0
    )


def select_sample(entries: Sequence[dict[str, Any]], config: dict[str, Any]) -> list[dict[str, Any]]:
    used_paths: set[str] = set()
    used_hashes: set[str] = set()
    selected: list[dict[str, Any]] = []

    bucket_specs: list[tuple[str, int, Callable[[dict[str, Any]], bool]]] = [
        ("Multi-Sprite Atlas", 1, lambda entry: entry["category"] == "Multi-Sprite Atlas"),
        ("HDRP Mask Map", 3, lambda entry: entry["category"] == "HDRP Mask Map"),
        ("Normal Map", 3, lambda entry: entry["category"] == "Normal Map"),
        ("Vegetation/Cutout", 3, lambda entry: "Vegetation" in entry.get("domains", []) and entry.get("hasAlpha")),
        ("UI Sprite", 3, lambda entry: entry["category"] == "UI Sprite"),
        ("Repeat/Tiling", 3, lambda entry: bool(entry.get("isTileable"))),
        ("Vehicle", 2, lambda entry: "Vehicle" in entry.get("domains", [])),
        (
            "Character",
            1,
            lambda entry: "Character" in entry.get("domains", [])
            and any(
                token in Path(str(entry.get("assetPath", ""))).stem.lower()
                for token in ("char_", "character", "face", "skin", "head", "body", "hand")
            ),
        ),
        (
            "Large World/Map",
            1,
            lambda entry: "World" in entry.get("domains", []) and max(int(entry.get("width", 0)), int(entry.get("height", 0))) >= 1024,
        ),
        ("Albedo", 5, lambda entry: entry["category"] == "Base Color / Albedo"),
    ]

    def quality_key(entry: dict[str, Any]) -> tuple[int, int, int, str]:
        maximum = max(int(entry.get("width", 0)), int(entry.get("height", 0)))
        useful_scale = 0 if choose_scale(entry, config) > 1 else 1
        has_material = 0 if entry.get("materialUses") else 1
        return useful_scale, has_material, maximum, str(entry["assetPath"]).lower()

    shortages: dict[str, int] = {}
    for bucket, required, predicate in bucket_specs:
        candidates = sorted(
            (entry for entry in entries if _eligible(entry, used_paths, used_hashes) and predicate(entry)),
            key=quality_key,
        )
        chosen = candidates[:required]
        if len(chosen) < required:
            shortages[bucket] = required - len(chosen)
        for entry in chosen:
            sample = dict(entry)
            sample["sampleBucket"] = bucket
            sample["selectedScale"] = choose_scale(sample, config)
            selected.append(sample)
            used_paths.add(str(entry["assetPath"]))
            used_hashes.add(str(entry["sourceSha256"]))

    # A repository can legitimately have no source textures for one of the
    # requested semantic buckets (the current vehicle prototype, for example,
    # uses flat-colour materials). Never relabel unrelated assets just to make
    # the coverage table look complete. Keep the shortage as audit evidence,
    # then fill the bounded sample with other safe, unique project textures so
    # processing, backup and validation still exercise 25 real assets.
    if len(selected) < 25:
        fallback_candidates = sorted(
            (
                entry
                for entry in entries
                if _eligible(entry, used_paths, used_hashes)
                and entry.get("category")
                not in {"Unknown / Manual Review", "Lightmap", "Cubemap", "Font Atlas"}
            ),
            key=quality_key,
        )
        for entry in fallback_candidates[: 25 - len(selected)]:
            sample = dict(entry)
            sample["sampleBucket"] = "Coverage Fallback"
            sample["selectedScale"] = choose_scale(sample, config)
            selected.append(sample)
            used_paths.add(str(entry["assetPath"]))
            used_hashes.add(str(entry["sourceSha256"]))

    payload = {
        "schemaVersion": 1,
        "generatedAtUtc": utc_now_iso(),
        "requiredCount": 25,
        "selectedCount": len(selected),
        "categoryCoverageComplete": not shortages,
        "shortages": shortages,
        "shortagePolicy": (
            "Missing semantic categories are reported and filled with other eligible textures; "
            "assets are never falsely relabelled."
        ),
        "textures": selected,
    }
    write_json_atomic(SAMPLE_PATH, payload)
    return selected


def scan(run_unity: bool = True, allow_fallback: bool = False) -> list[dict[str, Any]]:
    ensure_report_directories()
    config = load_config()
    unity_authoritative = False
    if run_unity:
        export_unity_inventory()
    if UNITY_EXPORT_PATH.exists():
        unity_payload = read_json(UNITY_EXPORT_PATH)
        records = unity_payload.get("textures", [])
        unity_authoritative = True
    elif allow_fallback:
        records = fallback_texture_records()
    else:
        raise FileNotFoundError(
            f"Unity export is absent: {UNITY_EXPORT_PATH}. Run without --skip-unity or pass --allow-fallback explicitly."
        )
    entries = enrich_inventory(records, config)
    write_inventory(entries, unity_authoritative)
    return entries


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Export and enrich the Unity texture inventory.")
    parser.add_argument("--skip-unity", action="store_true", help="Use an existing Unity export.")
    parser.add_argument("--allow-fallback", action="store_true", help="Allow filesystem-only inventory if Unity export is unavailable.")
    parser.add_argument("--sample", action="store_true", help="Create the bounded 25-texture sample selection.")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    entries = scan(run_unity=not args.skip_unity, allow_fallback=args.allow_fallback)
    selected = select_sample(entries, load_config()) if args.sample else []
    print(f"Inventory: {len(entries)} textures -> {INVENTORY_JSON_PATH}")
    if args.sample:
        print(f"Sample: {len(selected)}/25 textures -> {SAMPLE_PATH}")
        shortages = read_json(SAMPLE_PATH).get("shortages", {})
        if shortages:
            print("Sample shortages: " + json.dumps(shortages, ensure_ascii=False))
            print("The 25-texture sample uses explicitly labelled coverage fallbacks; semantic shortages remain open.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
