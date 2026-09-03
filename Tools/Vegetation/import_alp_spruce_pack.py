#!/usr/bin/env python3
"""Import the reviewed subset of ALP Spruce Trees Pack from a unitypackage.

The source archive remains external.  Assets are copied with their package GUIDs
into a project-owned third-party source root so prefab dependency links survive.
Demo scenes, vendor scripts, sky, post processing, wind and MapMagic data are
deliberately outside the whitelist.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import tarfile
from collections import deque
from pathlib import Path, PurePosixPath


PACKAGE_ROOT = "Assets/ALP_Assets/Spruce Trees Pack/"
DESTINATION_ROOT = PurePosixPath(
    "Assets/Game/Presentation/Vegetation/ThirdParty/ALPSpruceTreesPack/Source"
)

TREE_VARIANTS = (
    "ConiferTreeBig01_Optimized.prefab",
    "ConiferTreeBig02_Optimized.prefab",
    "ConiferTreeBig04_Optimized.prefab",
    # Small02 has two unresolved texture GUIDs in v1.1 and is intentionally omitted.
    "ConiferTreeSmall03_Optimized.prefab",
    "ConiferTreeSmall04_Optimized.prefab",
)

SEED_PATHS = tuple(
    PACKAGE_ROOT + "_Models/OptimizedVegetation/" + name for name in TREE_VARIANTS
) + (
    PACKAGE_ROOT + "_Models/TreeCreator/Bush01.prefab",
    PACKAGE_ROOT + "_Models/TreeCreator/Bush02.prefab",
    PACKAGE_ROOT + "Prefabs/Rock01_Pref.prefab",
    PACKAGE_ROOT + "Prefabs/Rock02_pref.prefab",
    PACKAGE_ROOT + "Prefabs/Rock03_pref.prefab",
    PACKAGE_ROOT + "Prefabs/stone01.prefab",
    PACKAGE_ROOT + "Prefabs/stone02.prefab",
    PACKAGE_ROOT + "GroundTextures/DryGrass01Layer.terrainlayer",
    PACKAGE_ROOT + "GroundTextures/DryGrass03Layer.terrainlayer",
    PACKAGE_ROOT + "GroundTextures/Rock01Layer.terrainlayer",
    PACKAGE_ROOT + "GroundTextures/Rock02Layer.terrainlayer",
    PACKAGE_ROOT + "GrassTextures/Grass01.tga",
    PACKAGE_ROOT + "GrassTextures/Grass01_Dry.tga",
    PACKAGE_ROOT + "GrassTextures/Flower01.tga",
    PACKAGE_ROOT + "GrassTextures/Flower02.tga",
)

GUID_PATTERN = re.compile(rb"guid:\s*([0-9a-fA-F]{32})")
BUILTIN_GUID_PREFIXES = ("0000000000000000",)


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--package", required=True, type=Path)
    parser.add_argument("--project", required=True, type=Path)
    return parser.parse_args()


def safe_destination(project: Path, source_path: str) -> tuple[str, Path]:
    if not source_path.startswith(PACKAGE_ROOT):
        raise RuntimeError(f"Dependency escapes reviewed package root: {source_path}")
    relative = PurePosixPath(source_path[len(PACKAGE_ROOT) :])
    if relative.is_absolute() or ".." in relative.parts:
        raise RuntimeError(f"Unsafe package path: {source_path}")
    destination_asset = DESTINATION_ROOT / relative
    destination = project.joinpath(*destination_asset.parts)
    resolved_project = project.resolve()
    resolved_destination = destination.resolve()
    if resolved_project not in resolved_destination.parents:
        raise RuntimeError(f"Destination escapes project: {destination}")
    return destination_asset.as_posix(), destination


def safe_project_asset_destination(project: Path, asset_path: str) -> Path:
    normalized = PurePosixPath(asset_path)
    if normalized.is_absolute() or ".." in normalized.parts:
        raise RuntimeError(f"Unsafe previous destination: {asset_path}")
    if normalized != DESTINATION_ROOT and DESTINATION_ROOT not in normalized.parents:
        raise RuntimeError(f"Previous destination escapes owned root: {asset_path}")
    destination = project.joinpath(*normalized.parts)
    resolved_root = project.joinpath(*DESTINATION_ROOT.parts).resolve()
    resolved_destination = destination.resolve()
    if resolved_destination != resolved_root and resolved_root not in resolved_destination.parents:
        raise RuntimeError(f"Previous destination escapes owned root: {asset_path}")
    return destination


def main() -> int:
    args = parse_args()
    package = args.package.resolve()
    project = args.project.resolve()
    if not package.is_file():
        raise FileNotFoundError(package)
    if not (project / "Assets").is_dir() or not (project / "ProjectSettings").is_dir():
        raise RuntimeError(f"Not a Unity project: {project}")

    report_path = project / "Artifacts/VegetationImport/ALPSpruceTreesPack.json"
    previous_destinations: set[str] = set()
    if report_path.is_file():
        previous_report = json.loads(report_path.read_text(encoding="utf-8"))
        previous_destinations = {
            str(asset["destinationPath"])
            for asset in previous_report.get("assets", [])
            if isinstance(asset, dict) and "destinationPath" in asset
        }

    with tarfile.open(package, "r:*") as archive:
        entries: dict[str, dict[str, tarfile.TarInfo]] = {}
        for member in archive.getmembers():
            normalized = member.name.removeprefix("./")
            parts = normalized.split("/")
            if len(parts) < 2 or not re.fullmatch(r"[0-9a-fA-F]{32}", parts[0]):
                continue
            entries.setdefault(parts[0].lower(), {})[parts[-1]] = member

        guid_to_path: dict[str, str] = {}
        path_to_guid: dict[str, str] = {}
        for guid, record in entries.items():
            pathname = record.get("pathname")
            if pathname is None:
                continue
            source_path = archive.extractfile(pathname).read().decode("utf-8-sig").strip()
            guid_to_path[guid] = source_path
            path_to_guid[source_path] = guid

        missing_seeds = sorted(path for path in SEED_PATHS if path not in path_to_guid)
        if missing_seeds:
            raise RuntimeError("Missing reviewed seeds:\n" + "\n".join(missing_seeds))

        queue = deque(path_to_guid[path] for path in SEED_PATHS)
        closure: set[str] = set()
        unresolved: set[str] = set()
        while queue:
            guid = queue.popleft()
            if guid in closure:
                continue
            closure.add(guid)
            record = entries[guid]
            payload = b""
            for name in ("asset", "asset.meta"):
                member = record.get(name)
                if member is not None:
                    payload += archive.extractfile(member).read()
            for dependency in GUID_PATTERN.findall(payload):
                dependency_guid = dependency.decode("ascii").lower()
                if dependency_guid in entries:
                    queue.append(dependency_guid)
                elif not dependency_guid.startswith(BUILTIN_GUID_PREFIXES):
                    unresolved.add(dependency_guid)

        if unresolved:
            details = [
                f"{guid} referenced by reviewed dependency closure"
                for guid in sorted(unresolved)
            ]
            raise RuntimeError("Unresolved non-builtin package dependencies:\n" + "\n".join(details))

        imported: list[dict[str, object]] = []
        for guid in sorted(closure, key=lambda value: guid_to_path[value].casefold()):
            source_path = guid_to_path[guid]
            destination_asset, destination = safe_destination(project, source_path)
            record = entries[guid]
            asset_member = record.get("asset")
            meta_member = record.get("asset.meta")
            if asset_member is None or meta_member is None:
                raise RuntimeError(f"Incomplete unitypackage entry: {source_path}")
            asset_bytes = archive.extractfile(asset_member).read()
            meta_bytes = archive.extractfile(meta_member).read()
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(asset_bytes)
            Path(str(destination) + ".meta").write_bytes(meta_bytes)
            imported.append(
                {
                    "guid": guid,
                    "sourcePath": source_path,
                    "destinationPath": destination_asset,
                    "bytes": len(asset_bytes),
                    "sha256": sha256_bytes(asset_bytes),
                }
            )

    current_destinations = {str(asset["destinationPath"]) for asset in imported}
    pruned: list[str] = []
    for stale_asset_path in sorted(previous_destinations - current_destinations):
        stale_asset = safe_project_asset_destination(project, stale_asset_path)
        for candidate in (stale_asset, Path(str(stale_asset) + ".meta")):
            if candidate.is_file() or candidate.is_symlink():
                candidate.unlink()
        pruned.append(stale_asset_path)

    report = {
        "schemaVersion": 1,
        "classification": "ThirdPartyPrivatePhase1Presentation",
        "productionReady": False,
        "productionBlocker": "Purchase receipt or other licence evidence is not stored with this archive.",
        "packagePath": str(package),
        "packageSha256": sha256_file(package),
        "sourcePackageVersion": "Spruce Trees Pack v1.1",
        "excludedKnownBrokenVariant": "ConiferTreeSmall02_Optimized.prefab",
        "seedCount": len(SEED_PATHS),
        "importedAssetCount": len(imported),
        "prunedPreviousAssetCount": len(pruned),
        "prunedPreviousAssets": pruned,
        "unresolvedExternalGuids": [],
        "assets": imported,
    }
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(
        "ALP_SPRUCE_PACKAGE_IMPORT_OK "
        f"assets={len(imported)} packageSha256={report['packageSha256']} report={report_path}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
