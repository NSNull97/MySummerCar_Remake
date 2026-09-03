#!/usr/bin/env python3
"""Selectively update the reviewed Finnish-biome NatureManufacture sources.

The importer preserves package GUIDs, backs up every replaced payload and never
imports demo scenes, old HDRP conversion packs or vendor terrain duplicates.
Runtime bindings are built later with project-owned Unity 6 HDRP materials.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import tarfile
from collections import deque
from pathlib import Path, PurePosixPath


SOURCE_ROOT = "Assets/NatureManufacture Assets/"
GUID_PATTERN = re.compile(rb"guid:\s*([0-9a-fA-F]{32})")
BUILTIN_GUID_PREFIXES = ("0000000000000000",)


FOREST_PATTERNS = (
    # A single visually neutral fallen-branch cluster. The source mesh is named
    # for the vendor's beech environment, but contains bare dead wood only; the
    # conspicuous beech leaf/log/root families remain outside the subset.
    re.compile(r".*/Details/Prefabs/prefab_detail_branches_01\.prefab$", re.I),
    re.compile(r".*/Foliage and Grass/Prefabs/prefab_fern_01_[1-4]\.prefab$", re.I),
    re.compile(r".*/Foliage and Grass/Prefabs/prefab_grass_01_[1-4]\.prefab$", re.I),
    re.compile(r".*/Foliage and Grass/Prefabs/prefab_grass_02_[1-3]\.prefab$", re.I),
    re.compile(r".*/Foliage and Grass/Prefabs/prefab_grass_03_[1-3]\.prefab$", re.I),
    re.compile(r".*/Foliage and Grass/Prefabs/prefab_lily_valley_01_[1-2]\.prefab$", re.I),
    re.compile(r".*/Details/Prefabs/prefab_detail_moss_01_[1-2]\.prefab$", re.I),
    re.compile(r".*/Mushrooms/Prefabs/prefab_Mushroom_Armillaria_0[12]\.prefab$", re.I),
    re.compile(r".*/Mushrooms/Prefabs/prefab_Mushroom_Russula_02\.prefab$", re.I),
)

MEADOW_PATTERNS = (
    # Vendor shaders include these files by path rather than by serialized GUID,
    # so Unity's dependency closure cannot discover them automatically.
    re.compile(r"Assets/NatureManufacture Assets/Foliage Shaders/(?:NMWind|NMWindTouchRect|NMWindNoShiver|NM_indirect)\.cginc$", re.I),
    re.compile(r".*/Bushes/Prefabs/prefab_grey_willow_0[1-4]\.prefab$", re.I),
    # Populus leaf litter is a restrained northern-deciduous accent. This is a
    # static ground-detail prefab, not the vendor's particle system.
    re.compile(r".*/Details/Prefabs/prefab_detail_poplar_leaves_01_1\.prefab$", re.I),
    re.compile(r".*/Grass/Prefabs Grass/prefab_grass_meadow_01_(?:[1-6]|cross_[1-3]|detailed_[1-2])\.prefab$", re.I),
    re.compile(r".*/Grass/Prefabs Grass/prefab_grass_meadow_02_(?:[2-6]|cross_[1-3]|detailed_[1-2])\.prefab$", re.I),
    re.compile(r".*/Grass/Prefabs Grass/prefab_grass_meadow_03_[1-4]\.prefab$", re.I),
    re.compile(r".*/Details/Prefabs/prefab_detail_meadow_clover_0[12]\.prefab$", re.I),
    re.compile(r".*/Details/Prefabs/prefab_Simple_detail_meadow_clover_01\.prefab$", re.I),
    re.compile(r".*/Details/Prefabs/prefab_detail_meadow_daisy_01\.prefab$", re.I),
    re.compile(r".*/Details/Prefabs/prefab_detail_meadow_dead_grass_0[1-3]\.prefab$", re.I),
    re.compile(r".*/Details/Prefabs/prefab_detail_meadow_grass_01\.prefab$", re.I),
    re.compile(r".*/Grass/Prefabs Flowers/prefab_flower_brownray_knapweed_01_(?:[12]|cross_1|detailed_[12])\.prefab$", re.I),
    re.compile(r".*/Grass/Prefabs Flowers/prefab_flower_common_Saint_John's_wort_01_(?:[12]|cross_1|detailed_1)\.prefab$", re.I),
)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--forest-package", required=True, type=Path)
    parser.add_argument("--meadow-package", required=True, type=Path)
    parser.add_argument("--project", required=True, type=Path)
    return parser.parse_args()


def safe_project_path(project: Path, asset_path: str) -> Path:
    pure = PurePosixPath(asset_path)
    if not asset_path.startswith(SOURCE_ROOT) or pure.is_absolute() or ".." in pure.parts:
        raise RuntimeError(f"Unsafe package path: {asset_path}")
    destination = project.joinpath(*pure.parts).resolve()
    if project.resolve() not in destination.parents:
        raise RuntimeError(f"Destination escapes project: {destination}")
    return destination


def stage_package(package: Path, staging: Path) -> dict[str, dict[str, Path]]:
    """Expand required unitypackage records once, avoiding repeated gzip seeks."""
    staging_base = staging.parent.resolve()
    resolved_staging = staging.resolve()
    if staging_base not in resolved_staging.parents:
        raise RuntimeError(f"Unsafe staging path: {resolved_staging}")
    if resolved_staging.exists():
        shutil.rmtree(resolved_staging)
    resolved_staging.mkdir(parents=True)
    entries: dict[str, dict[str, Path]] = {}
    # These vendor archives contain a gzip layout which Python's stream reader
    # rejects, while the normal seekable reader handles it correctly. Iterating
    # lazily still keeps extraction sequential; do not call getmembers here.
    with tarfile.open(package, "r:*") as archive:
        for member in archive:
            normalized = member.name.removeprefix("./")
            parts = normalized.split("/")
            if (
                len(parts) != 2
                or not re.fullmatch(r"[0-9a-fA-F]{32}", parts[0])
                or parts[1] not in {"asset", "asset.meta", "pathname"}
                or not member.isfile()
            ):
                continue
            guid = parts[0].lower()
            destination = resolved_staging / guid / parts[1]
            destination.parent.mkdir(parents=True, exist_ok=True)
            source = archive.extractfile(member)
            if source is None:
                raise RuntimeError(f"Could not stage {normalized}")
            with destination.open("wb") as output:
                shutil.copyfileobj(source, output, length=1024 * 1024)
            entries.setdefault(guid, {})[parts[1]] = destination
    return entries


def import_package(
    package: Path,
    project: Path,
    patterns: tuple[re.Pattern[str], ...],
    label: str,
) -> dict[str, object]:
    package = package.resolve()
    package_hash = sha256_file(package)
    backup_root = project / "Artifacts/VegetationImport/Backups" / package_hash[:16]
    staging_base = (project / "Artifacts/VegetationImport/PackageStaging").resolve()
    staging = (staging_base / package_hash[:16]).resolve()
    staging_base.mkdir(parents=True, exist_ok=True)
    entries = stage_package(package, staging)
    try:
        guid_to_path: dict[str, str] = {}
        path_to_guid: dict[str, str] = {}
        for guid, record in entries.items():
            member = record.get("pathname")
            if member is None:
                continue
            pathname_lines = member.read_bytes().decode("utf-8-sig").splitlines()
            if not pathname_lines:
                continue
            # These two packages append a second `00` line to pathname records.
            # Only the first line is the Unity asset path.
            path = pathname_lines[0].strip()
            guid_to_path[guid] = path
            path_to_guid[path] = guid

        seeds = sorted(
            path for path in path_to_guid
            if path.startswith(SOURCE_ROOT) and any(pattern.fullmatch(path) for pattern in patterns)
        )
        unmatched = [pattern.pattern for pattern in patterns if not any(pattern.fullmatch(path) for path in seeds)]
        if unmatched:
            raise RuntimeError(f"{label} whitelist patterns matched nothing:\n" + "\n".join(unmatched))

        queue = deque(path_to_guid[path] for path in seeds)
        closure: set[str] = set()
        unresolved: set[str] = set()
        while queue:
            guid = queue.popleft()
            if guid in closure:
                continue
            closure.add(guid)
            payload = b""
            for name in ("asset", "asset.meta"):
                member = entries[guid].get(name)
                if member is not None:
                    payload += member.read_bytes()
            for raw_guid in GUID_PATTERN.findall(payload):
                dependency = raw_guid.decode("ascii").lower()
                if dependency in entries:
                    queue.append(dependency)
                elif not dependency.startswith(BUILTIN_GUID_PREFIXES):
                    unresolved.add(dependency)

        imported: list[dict[str, object]] = []
        overwritten = 0
        for guid in sorted(closure, key=lambda item: guid_to_path[item].casefold()):
            asset_path = guid_to_path[guid]
            if not asset_path.startswith(SOURCE_ROOT):
                raise RuntimeError(f"{label} dependency escapes NatureManufacture root: {asset_path}")
            destination = safe_project_path(project, asset_path)
            record = entries[guid]
            asset_member = record.get("asset")
            meta_member = record.get("asset.meta")
            if asset_member is None or meta_member is None:
                raise RuntimeError(f"Incomplete package entry: {asset_path}")
            asset_bytes = asset_member.read_bytes()
            meta_bytes = meta_member.read_bytes()
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination_meta = Path(str(destination) + ".meta")
            asset_existed = destination.exists()
            meta_existed = destination_meta.exists()
            asset_changed = not asset_existed or destination.read_bytes() != asset_bytes
            meta_changed = not meta_existed or destination_meta.read_bytes() != meta_bytes
            changed = asset_changed or meta_changed
            if (asset_existed or meta_existed) and changed:
                backup = backup_root.joinpath(*PurePosixPath(asset_path).parts)
                backup.parent.mkdir(parents=True, exist_ok=True)
                if asset_existed:
                    shutil.copy2(destination, backup)
                if meta_existed:
                    shutil.copy2(destination_meta, Path(str(backup) + ".meta"))
                overwritten += 1
            destination.write_bytes(asset_bytes)
            destination_meta.write_bytes(meta_bytes)
            imported.append(
                {
                    "guid": guid,
                    "assetPath": asset_path,
                    "bytes": len(asset_bytes),
                    "sha256": hashlib.sha256(asset_bytes).hexdigest(),
                    "overwroteExistingAssetOrMeta": changed and (asset_existed or meta_existed),
                    "assetPayloadChanged": asset_changed,
                    "metaPayloadChanged": meta_changed,
                }
            )
    finally:
        if staging.exists():
            if staging_base not in staging.resolve().parents:
                raise RuntimeError(f"Refusing unsafe staging cleanup: {staging}")
            shutil.rmtree(staging)

    return {
        "label": label,
        "packagePath": str(package),
        "packageSha256": package_hash,
        "seedCount": len(seeds),
        "dependencyClosureCount": len(imported),
        "overwrittenAssetCount": overwritten,
        "backupRoot": str(backup_root),
        "unresolvedExternalGuids": sorted(unresolved),
        "seeds": seeds,
        "assets": imported,
    }


def main() -> int:
    args = parse_args()
    project = args.project.resolve()
    if not (project / "Assets").is_dir() or not (project / "ProjectSettings").is_dir():
        raise RuntimeError(f"Not a Unity project: {project}")
    results = [
        import_package(args.forest_package, project, FOREST_PATTERNS, "Forest Environment v1.8.8"),
        import_package(args.meadow_package, project, MEADOW_PATTERNS, "Meadow Environment v2.9.3"),
    ]
    report = {
        "schemaVersion": 1,
        "classification": "LicensedThirdPartyPhase1Presentation",
        "productionReady": False,
        "productionNote": "Unity 6 HDRP runtime uses project-owned material bindings; package shaders are source dependencies only.",
        "packages": results,
    }
    report_path = project / "Artifacts/VegetationImport/NatureManufactureFinnishSubset.json"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(
        "NATUREMANUFACTURE_FINNISH_SUBSET_IMPORT_OK "
        f"forestSeeds={results[0]['seedCount']} meadowSeeds={results[1]['seedCount']} report={report_path}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
