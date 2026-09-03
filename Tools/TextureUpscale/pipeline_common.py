from __future__ import annotations

import csv
import fnmatch
import hashlib
import json
import math
import os
import re
import shutil
import subprocess
import tempfile
from contextlib import contextmanager
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Iterator, Sequence

import numpy as np
from PIL import Image


TOOL_ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = TOOL_ROOT.parents[1]
REPORT_ROOT = PROJECT_ROOT / "Reports" / "TextureUpscale"
COMPARISON_ROOT = REPORT_ROOT / "Comparisons"
PROCESSED_ROOT = REPORT_ROOT / "Processed"
BACKUP_ROOT = PROJECT_ROOT / "Backups" / "TextureUpscale"
CONFIG_PATH = TOOL_ROOT / "texture_upscale_config.yaml"
UNITY_EXPORT_PATH = REPORT_ROOT / "unity_texture_inventory.json"
INVENTORY_JSON_PATH = REPORT_ROOT / "texture_inventory.json"
INVENTORY_CSV_PATH = REPORT_ROOT / "texture_inventory.csv"
MANUAL_REVIEW_PATH = REPORT_ROOT / "manual_review.csv"
SAMPLE_PATH = REPORT_ROOT / "sample_selection.json"
PROCESS_MANIFEST_PATH = REPORT_ROOT / "process_manifest.json"
APPLY_REQUEST_PATH = REPORT_ROOT / "apply_request.json"
PYTHON_VALIDATION_PATH = REPORT_ROOT / "validation_python.json"

SUPPORTED_SOURCE_EXTENSIONS = {
    ".png",
    ".jpg",
    ".jpeg",
    ".tga",
    ".tif",
    ".tiff",
    ".bmp",
}
AUDITED_TEXTURE_EXTENSIONS = SUPPORTED_SOURCE_EXTENSIONS | {
    ".psd",
    ".exr",
    ".hdr",
    ".dds",
}

TEXTURE_PROPERTY_ROLES = {
    "_basecolormap": "Base Color / Albedo",
    "_basemap": "Base Color / Albedo",
    "_maintex": "Base Color / Albedo",
    "_albedomap": "Base Color / Albedo",
    "_diffusemap": "Base Color / Albedo",
    "_emissivecolormap": "Emission",
    "_emissionmap": "Emission",
    "_normalmap": "Normal Map",
    "_normalmapos": "Normal Map",
    "_bumpmap": "Normal Map",
    "_detailmap": "Normal Map",
    "_maskmap": "HDRP Mask Map",
    "_metallicglossmap": "Metallic",
    "_metallicmap": "Metallic",
    "_roughnessmap": "Roughness",
    "_smoothnessmap": "Smoothness",
    "_occlusionmap": "Ambient Occlusion",
    "_heightmap": "Height / Displacement",
    "_parallaxmap": "Height / Displacement",
    "_opacitymap": "Alpha Mask / Cutout",
    "_alphamap": "Alpha Mask / Cutout",
}


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def timestamp_id() -> str:
    return datetime.now().strftime("%Y%m%d_%H%M%S")


def ensure_report_directories() -> None:
    for path in (
        REPORT_ROOT,
        COMPARISON_ROOT,
        PROCESSED_ROOT,
        BACKUP_ROOT,
        TOOL_ROOT / "cache",
        TOOL_ROOT / "logs",
    ):
        path.mkdir(parents=True, exist_ok=True)


def project_relative(path: Path) -> str:
    return path.resolve().relative_to(PROJECT_ROOT.resolve()).as_posix()


def resolve_project_path(value: str | Path) -> Path:
    path = Path(value)
    return path if path.is_absolute() else PROJECT_ROOT / path


def load_config(path: Path = CONFIG_PATH) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        config = json.load(stream)
    if int(config.get("schemaVersion", 0)) != 1:
        raise ValueError(f"Unsupported config schema: {config.get('schemaVersion')}")
    return config


def read_json(path: Path, default: Any | None = None) -> Any:
    if not path.exists():
        if default is not None:
            return default
        raise FileNotFoundError(path)
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def write_json_atomic(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + f".{os.getpid()}.tmp")
    with temporary.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2, sort_keys=False)
        stream.write("\n")
    os.replace(temporary, path)


def write_csv(path: Path, fieldnames: Sequence[str], rows: Iterable[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + f".{os.getpid()}.tmp")
    with temporary.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)
    os.replace(temporary, path)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def parse_meta_guid(meta_path: Path) -> str:
    if not meta_path.exists():
        return ""
    text = meta_path.read_text(encoding="utf-8", errors="replace")
    match = re.search(r"(?m)^guid:\s*([0-9a-fA-F]{32})\s*$", text)
    return match.group(1).lower() if match else ""


def get_dirty_asset_paths() -> set[str]:
    result = subprocess.run(
        ["git", "status", "--porcelain=v1", "--untracked-files=all", "--", "Assets"],
        cwd=PROJECT_ROOT,
        text=True,
        encoding="utf-8",
        errors="replace",
        capture_output=True,
        check=False,
    )
    dirty: set[str] = set()
    for line in result.stdout.splitlines():
        if len(line) < 4:
            continue
        value = line[3:]
        if " -> " in value:
            value = value.split(" -> ", 1)[1]
        dirty.add(value.strip('"').replace("\\", "/"))
    return dirty


def read_source_image_info(path: Path) -> dict[str, Any]:
    info: dict[str, Any] = {
        "readableByPipeline": False,
        "width": 0,
        "height": 0,
        "mode": "",
        "bitDepth": 0,
        "hasAlpha": False,
        "alphaIsBinary": False,
        "sourceReadError": "",
    }
    try:
        with Image.open(path) as image:
            image.load()
            bands = image.getbands()
            has_alpha = "A" in bands or "transparency" in image.info
            bit_depth = 8
            if image.mode.startswith("I;16"):
                bit_depth = 16
            elif image.mode in {"I", "F"}:
                bit_depth = 32
            alpha_binary = False
            if "A" in bands:
                alpha = np.asarray(image.getchannel("A"))
                unique = np.unique(alpha)
                alpha_binary = bool(unique.size <= 2 and set(unique.tolist()).issubset({0, 255}))
            info.update(
                {
                    "readableByPipeline": path.suffix.lower() in SUPPORTED_SOURCE_EXTENSIONS,
                    "width": int(image.width),
                    "height": int(image.height),
                    "mode": image.mode,
                    "bitDepth": bit_depth,
                    "hasAlpha": has_alpha,
                    "alphaIsBinary": alpha_binary,
                }
            )
    except Exception as exc:  # Pillow support is intentionally conservative.
        info["sourceReadError"] = f"{type(exc).__name__}: {exc}"
    return info


def normalize_material_uses(record: dict[str, Any]) -> list[dict[str, str]]:
    values = record.get("materialUses") or []
    result: list[dict[str, str]] = []
    for value in values:
        if not isinstance(value, dict):
            continue
        result.append(
            {
                "materialPath": str(value.get("materialPath", "")),
                "propertyName": str(value.get("propertyName", "")),
                "shaderName": str(value.get("shaderName", "")),
            }
        )
    return result


def infer_domains(asset_path: str, material_uses: Sequence[dict[str, str]]) -> list[str]:
    haystack = " ".join(
        [asset_path]
        + [value.get("materialPath", "") for value in material_uses]
        + [value.get("shaderName", "") for value in material_uses]
    ).lower()
    domains: list[str] = []
    tests = (
        ("UI", ("/ui/", "canvas", "sprite", "hud", "menu")),
        ("Vehicle", ("/vehicle/", "/vehicles/", "satsuma", "carbody", "dashboard", "tire", "wheel")),
        ("Character", ("/character", "/characters/", "/npc/", "skin", "face", "body")),
        ("Vegetation", ("vegetation", "foliage", "grass", "tree", "leaf", "leaves", "branch", "bush")),
        ("World", ("/world/", "terrain", "road", "asphalt", "ground", "building", "map")),
        ("Decal", ("decal",)),
        ("Prop", ("/prop", "/items/", "/item/", "object")),
    )
    for name, needles in tests:
        if any(needle in haystack for needle in needles):
            domains.append(name)
    return domains or ["Unassigned"]


def infer_category(record: dict[str, Any], image_info: dict[str, Any]) -> str:
    asset_path = str(record.get("assetPath", ""))
    lower_path = asset_path.lower()
    importer_type = str(record.get("textureType", "")).lower()
    sprite_mode = str(record.get("spriteMode", "")).lower()
    material_uses = normalize_material_uses(record)
    properties = [value["propertyName"].lower() for value in material_uses]

    if "normal" in importer_type:
        return "Normal Map"
    if any(prop in {"_maskmap"} for prop in properties):
        return "HDRP Mask Map"
    for property_name in properties:
        if property_name in TEXTURE_PROPERTY_ROLES:
            return TEXTURE_PROPERTY_ROLES[property_name]

    if "sprite" in importer_type or sprite_mode not in {"", "none", "0"}:
        if sprite_mode in {"multiple", "2"} or int(record.get("spriteCount", 0) or 0) > 1:
            return "Multi-Sprite Atlas"
        return "UI Sprite"
    if "cubemap" in importer_type or lower_path.endswith((".hdr", ".exr")):
        return "Cubemap"
    if "lightmap" in lower_path or "lightingdata" in lower_path:
        return "Lightmap"
    if "font" in lower_path and ("atlas" in lower_path or "sdf" in lower_path):
        return "Font Atlas"

    name = Path(lower_path).stem
    filename_rules = (
        (("_normal", "_nrm", "_norm", "_n"), "Normal Map"),
        (("_mask", "maskmap"), "HDRP Mask Map"),
        (("_metal", "metallic"), "Metallic"),
        (("_rough", "roughness"), "Roughness"),
        (("_smooth", "smoothness"), "Smoothness"),
        (("_ao", "occlusion"), "Ambient Occlusion"),
        (("_height", "heightmap", "displacement"), "Height / Displacement"),
        (("_alpha", "opacity"), "Alpha Mask / Cutout"),
        (("_emission", "emissive"), "Emission"),
    )
    for needles, category in filename_rules:
        if any(needle in name for needle in needles):
            return category

    domains = infer_domains(asset_path, material_uses)
    if image_info.get("hasAlpha") and "Vegetation" in domains:
        return "Alpha Mask / Cutout"
    if material_uses:
        return "Base Color / Albedo"
    return "Unknown / Manual Review"


def is_repeat_texture(record: dict[str, Any]) -> bool:
    values = {
        str(record.get("wrapMode", "")),
        str(record.get("wrapU", "")),
        str(record.get("wrapV", "")),
    }
    return any(value.lower() in {"repeat", "0"} for value in values)


def match_rule(entry: dict[str, Any], match: dict[str, Any]) -> bool:
    path = str(entry.get("assetPath", ""))
    for key, expected in match.items():
        if key == "path":
            if not fnmatch.fnmatch(path.lower(), str(expected).lower()):
                return False
        elif key == "name":
            if not fnmatch.fnmatch(Path(path).name.lower(), str(expected).lower()):
                return False
        elif key == "category":
            if str(entry.get("category", "")).lower() != str(expected).lower():
                return False
        elif key == "folder":
            if not path.lower().startswith(str(expected).lower().rstrip("/") + "/"):
                return False
        else:
            return False
    return True


def apply_config_rules(entry: dict[str, Any], config: dict[str, Any]) -> dict[str, Any]:
    result = dict(entry)
    result["excludedByConfig"] = any(
        str(result.get("assetPath", "")).lower().startswith(str(folder).lower())
        for folder in config.get("excludeFolders", [])
    )
    result.setdefault("manualReview", False)
    result.setdefault("manualReviewReasons", [])
    for pattern in config.get("manualReviewPatterns", []):
        if fnmatch.fnmatch(str(result.get("assetPath", "")).lower(), str(pattern).lower()):
            result["manualReview"] = True
            result["manualReviewReasons"].append(f"Matched manual-review pattern: {pattern}")
    for rule in config.get("rules", []):
        if not match_rule(result, rule.get("match", {})):
            continue
        for key in ("scale", "maxSize", "disableAI", "tileable", "preserveAlpha"):
            if key in rule:
                result[key] = rule[key]
        if "category" in rule:
            result["category"] = rule["category"]
        if rule.get("manualReview"):
            result["manualReview"] = True
        if rule.get("reason"):
            result["manualReviewReasons"].append(str(rule["reason"]))
    if result.get("category") in set(config.get("excludeCategories", [])):
        result["excludedByConfig"] = True
    return result


def choose_scale(entry: dict[str, Any], config: dict[str, Any], override: int | None = None) -> int:
    if override is not None:
        return max(1, int(override))
    if "scale" in entry:
        return max(1, int(entry["scale"]))
    maximum_dimension = max(int(entry.get("width", 0)), int(entry.get("height", 0)))
    scale = 1
    for policy in config.get("scalePolicy", []):
        if maximum_dimension <= int(policy["maxDimension"]):
            scale = int(policy["scale"])
            break
    maximum_size = int(entry.get("maxSize") or config.get("defaultMaxSize", 4096))
    while scale > 1 and maximum_dimension * scale > maximum_size:
        scale //= 2
    return max(1, scale)


def estimate_vram_bytes(width: int, height: int, has_alpha: bool, mipmaps: bool, compression: str) -> int:
    pixels = max(1, width) * max(1, height)
    compression_lower = compression.lower()
    if "uncompressed" in compression_lower or compression_lower in {"none", "0", ""}:
        bits_per_pixel = 32 if has_alpha else 24
    elif any(name in compression_lower for name in ("bc7", "dxt5", "bc5", "compressedhq", "compressed")):
        bits_per_pixel = 8
    else:
        bits_per_pixel = 8
    value = pixels * bits_per_pixel / 8.0
    if mipmaps:
        value *= 4.0 / 3.0
    return int(math.ceil(value))


def image_to_rgb_array(image: Image.Image) -> np.ndarray:
    # Hidden RGB below zero alpha is allowed to change during correct
    # premultiply/unpremultiply processing and must not dominate similarity
    # metrics. Compare the visible result over a neutral background instead;
    # this still exposes alpha-edge halos and silhouette changes.
    rgba = np.asarray(image.convert("RGBA"), dtype=np.float32)
    alpha = rgba[:, :, 3:4] / 255.0
    return rgba[:, :, :3] * alpha + 127.5 * (1.0 - alpha)


def alpha_coverage(image: Image.Image) -> float:
    if "A" not in image.getbands():
        return 1.0
    alpha = np.asarray(image.convert("RGBA"), dtype=np.uint8)[:, :, 3]
    return float(np.mean(alpha > 127))


def resize_for_comparison(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    return image.convert("RGBA").resize(size, Image.Resampling.LANCZOS)


def block_ssim(reference: np.ndarray, candidate: np.ndarray, block: int = 8) -> float:
    if reference.shape != candidate.shape:
        raise ValueError("SSIM inputs must have identical shapes.")
    reference = reference.astype(np.float64)
    candidate = candidate.astype(np.float64)
    height = reference.shape[0] - reference.shape[0] % block
    width = reference.shape[1] - reference.shape[1] % block
    if height < block or width < block:
        return float(global_ssim(reference, candidate))
    reference = reference[:height, :width]
    candidate = candidate[:height, :width]
    if reference.ndim == 2:
        reference = reference[:, :, None]
        candidate = candidate[:, :, None]
    channels = reference.shape[2]
    ref_blocks = reference.reshape(height // block, block, width // block, block, channels)
    can_blocks = candidate.reshape(height // block, block, width // block, block, channels)
    axes = (1, 3)
    mu_x = ref_blocks.mean(axis=axes)
    mu_y = can_blocks.mean(axis=axes)
    var_x = ref_blocks.var(axis=axes)
    var_y = can_blocks.var(axis=axes)
    covariance = ((ref_blocks - mu_x[:, None, :, None, :]) * (can_blocks - mu_y[:, None, :, None, :])).mean(axis=axes)
    c1 = (0.01 * 255.0) ** 2
    c2 = (0.03 * 255.0) ** 2
    score = ((2 * mu_x * mu_y + c1) * (2 * covariance + c2)) / (
        (mu_x * mu_x + mu_y * mu_y + c1) * (var_x + var_y + c2)
    )
    return float(np.clip(np.mean(score), -1.0, 1.0))


def global_ssim(reference: np.ndarray, candidate: np.ndarray) -> float:
    reference = reference.astype(np.float64)
    candidate = candidate.astype(np.float64)
    mu_x = reference.mean()
    mu_y = candidate.mean()
    var_x = reference.var()
    var_y = candidate.var()
    covariance = ((reference - mu_x) * (candidate - mu_y)).mean()
    c1 = (0.01 * 255.0) ** 2
    c2 = (0.03 * 255.0) ** 2
    return float(((2 * mu_x * mu_y + c1) * (2 * covariance + c2)) / ((mu_x**2 + mu_y**2 + c1) * (var_x + var_y + c2)))


def psnr(reference: np.ndarray, candidate: np.ndarray) -> float:
    error = float(np.mean((reference.astype(np.float64) - candidate.astype(np.float64)) ** 2))
    if error <= 1e-12:
        return 99.0
    return 20.0 * math.log10(255.0 / math.sqrt(error))


def histogram_l1(reference: np.ndarray, candidate: np.ndarray) -> float:
    scores: list[float] = []
    channels = 1 if reference.ndim == 2 else reference.shape[2]
    for channel in range(channels):
        ref = reference if channels == 1 else reference[:, :, channel]
        can = candidate if channels == 1 else candidate[:, :, channel]
        ref_hist = np.histogram(ref, bins=64, range=(0, 256), density=True)[0]
        can_hist = np.histogram(can, bins=64, range=(0, 256), density=True)[0]
        scores.append(float(np.abs(ref_hist - can_hist).sum() * 4.0 / 2.0))
    return float(np.mean(scores))


def seam_score(image: Image.Image) -> dict[str, float]:
    pixels = image_to_rgb_array(image)
    horizontal = float(np.abs(pixels[:, 0] - pixels[:, -1]).mean())
    vertical = float(np.abs(pixels[0] - pixels[-1]).mean())
    normal_horizontal = float(np.abs(pixels[:, 1:] - pixels[:, :-1]).mean())
    normal_vertical = float(np.abs(pixels[1:] - pixels[:-1]).mean())
    return {
        "horizontal": horizontal,
        "vertical": vertical,
        "normalHorizontal": normal_horizontal,
        "normalVertical": normal_vertical,
        "normalized": float((horizontal / max(normal_horizontal, 1e-6) + vertical / max(normal_vertical, 1e-6)) * 0.5),
    }


def calculate_comparison_metrics(original: Image.Image, processed: Image.Image) -> dict[str, Any]:
    downsampled = resize_for_comparison(processed, original.size)
    reference = image_to_rgb_array(original)
    candidate = image_to_rgb_array(downsampled)
    source_coverage = alpha_coverage(original)
    processed_coverage = alpha_coverage(downsampled)
    return {
        "ssimBlock8": block_ssim(reference, candidate),
        "psnr": psnr(reference, candidate),
        "meanColorOriginal": reference.mean(axis=(0, 1)).tolist(),
        "meanColorProcessedDownsampled": candidate.mean(axis=(0, 1)).tolist(),
        "meanColorDelta": float(np.abs(reference.mean(axis=(0, 1)) - candidate.mean(axis=(0, 1))).mean()),
        "histogramL1": histogram_l1(reference, candidate),
        "alphaCoverageOriginal": source_coverage,
        "alphaCoverageProcessedDownsampled": processed_coverage,
        "alphaCoverageDelta": abs(source_coverage - processed_coverage),
        "seamOriginal": seam_score(original),
        "seamProcessed": seam_score(processed),
    }


def save_image_preserving_extension(image: Image.Image, destination: Path, source_extension: str) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(destination.stem + f".{os.getpid()}.tmp" + destination.suffix)
    extension = source_extension.lower()
    if extension in {".jpg", ".jpeg"}:
        image.convert("RGB").save(temporary, quality=96, subsampling=0, optimize=True)
    elif extension == ".png":
        image.save(temporary, optimize=True, compress_level=6)
    elif extension in {".tif", ".tiff"}:
        image.save(temporary, compression="tiff_lzw")
    else:
        image.save(temporary)
    os.replace(temporary, destination)


def atomic_copy(source: Path, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=destination.name + ".", suffix=".tmp", dir=destination.parent)
    os.close(handle)
    temporary = Path(temporary_name)
    try:
        shutil.copy2(source, temporary)
        os.replace(temporary, destination)
    finally:
        if temporary.exists():
            temporary.unlink()


@contextmanager
def exclusive_lock(name: str) -> Iterator[Path]:
    path = TOOL_ROOT / "cache" / name
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        descriptor = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError as exc:
        raise RuntimeError(f"Texture pipeline lock already exists: {path}") from exc
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            stream.write(f"pid={os.getpid()}\ncreated={utc_now_iso()}\n")
        yield path
    finally:
        path.unlink(missing_ok=True)


def unity_executable() -> Path:
    local_config = PROJECT_ROOT / "Config" / "DonorPaths.local.json"
    if local_config.exists():
        with local_config.open("r", encoding="utf-8") as stream:
            value = json.load(stream).get("UnityEditorExecutable", "")
        if value:
            path = Path(value)
            if path.exists():
                return path
    raise FileNotFoundError("Unity Editor executable is not configured or does not exist.")


def run_unity_method(method: str, log_name: str, extra_args: Sequence[str] | None = None, timeout: int = 1800) -> subprocess.CompletedProcess[str]:
    ensure_report_directories()
    log_path = TOOL_ROOT / "logs" / log_name
    command = [
        str(unity_executable()),
        "-batchmode",
        "-quit",
        "-projectPath",
        str(PROJECT_ROOT),
        "-executeMethod",
        method,
        "-logFile",
        str(log_path),
    ]
    if extra_args:
        command.extend(extra_args)
    return subprocess.run(
        command,
        cwd=PROJECT_ROOT,
        text=True,
        encoding="utf-8",
        errors="replace",
        capture_output=True,
        timeout=timeout,
        check=False,
    )


def filter_entries(
    entries: Sequence[dict[str, Any]],
    category: str | None = None,
    includes: Sequence[str] | None = None,
    excludes: Sequence[str] | None = None,
) -> list[dict[str, Any]]:
    includes = list(includes or [])
    excludes = list(excludes or [])
    result: list[dict[str, Any]] = []
    for entry in entries:
        path = str(entry.get("assetPath", ""))
        if category and str(entry.get("category", "")).lower() != category.lower():
            continue
        if includes and not any(fnmatch.fnmatch(path.lower(), pattern.lower()) for pattern in includes):
            continue
        if excludes and any(fnmatch.fnmatch(path.lower(), pattern.lower()) for pattern in excludes):
            continue
        result.append(entry)
    return result


def latest_backup_manifest() -> Path:
    manifests = sorted(BACKUP_ROOT.glob("*/manifest.json"), key=lambda value: value.parent.name, reverse=True)
    if not manifests:
        raise FileNotFoundError(f"No backup manifest found under {BACKUP_ROOT}")
    return manifests[0]


def find_real_esrgan(config: dict[str, Any]) -> tuple[Path | None, Path | None]:
    backend = config.get("backend", {})
    executable = resolve_project_path(str(backend.get("executable", ""))) if backend.get("executable") else None
    model_directory = resolve_project_path(str(backend.get("modelDirectory", ""))) if backend.get("modelDirectory") else None
    if executable and executable.exists() and model_directory and model_directory.exists():
        return executable, model_directory
    detected = shutil.which("realesrgan-ncnn-vulkan") or shutil.which("realesrgan-ncnn-vulkan.exe")
    return (Path(detected), model_directory) if detected else (None, model_directory)
