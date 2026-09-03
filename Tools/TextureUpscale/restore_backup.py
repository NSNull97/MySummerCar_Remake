from __future__ import annotations

import argparse
from pathlib import Path
from typing import Any, Sequence

from pipeline_common import (
    PROJECT_ROOT,
    atomic_copy,
    exclusive_lock,
    latest_backup_manifest,
    read_json,
    run_unity_method,
    sha256_file,
    utc_now_iso,
    write_json_atomic,
)


def restore_manifest(manifest_path: Path, force: bool, reimport: bool) -> int:
    manifest: dict[str, Any] = read_json(manifest_path)
    restored = 0
    with exclusive_lock("restore.lock"):
        for operation in reversed(manifest.get("operations", [])):
            destination = PROJECT_ROOT / operation["assetPath"]
            backup = Path(operation.get("backupPath", ""))
            backup_meta = Path(operation.get("backupMetaPath", ""))
            destination_meta = Path(str(destination) + ".meta")
            if not backup.exists():
                operation.setdefault("warnings", []).append("Backup payload is missing; restore skipped.")
                operation["restoreStatus"] = "MissingBackup"
                continue
            expected_current = operation.get("newSha256") or operation.get("processedSha256")
            if destination.exists() and expected_current and sha256_file(destination) != expected_current and not force:
                operation.setdefault("warnings", []).append("Current asset changed after apply; restore requires --force.")
                operation["restoreStatus"] = "ConcurrentChange"
                continue
            atomic_copy(backup, destination)
            if backup_meta.exists():
                atomic_copy(backup_meta, destination_meta)
            operation["restoreStatus"] = "Restored"
            operation["restoredSha256"] = sha256_file(destination)
            restored += 1
        manifest["restoreAtUtc"] = utc_now_iso()
        manifest["restoreStatus"] = "Complete" if restored else "NoFilesRestored"
        write_json_atomic(manifest_path, manifest)
    if reimport and restored:
        result = run_unity_method(
            "MSC.Editor.TextureUpscale.TextureUpscaleApply.ReimportBatch",
            "texture_restore_reimport.log",
        )
        if result.returncode != 0:
            raise RuntimeError("Files were restored, but Unity reimport failed. See texture_restore_reimport.log.")
    return restored


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Restore an applied texture backup manifest.")
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--skip-unity", action="store_true")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    manifest = args.manifest or latest_backup_manifest()
    restored = restore_manifest(manifest, force=args.force, reimport=not args.skip_unity)
    print(f"Restored {restored} textures from {manifest}")
    return 0 if restored else 2


if __name__ == "__main__":
    raise SystemExit(main())
