#!/usr/bin/env python3
"""Convert one reviewed AssetRipper GLB mesh to a deterministic OBJ staging file."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import trimesh
from trimesh.exchange.obj import export_obj


TOOL_ID = "msc-glb-to-obj"
TOOL_VERSION = "1.0.0"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--metadata", type=Path, required=True)
    args = parser.parse_args()

    input_path = args.input.resolve(strict=True)
    output_path = args.output.resolve()
    metadata_path = args.metadata.resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    metadata_path.parent.mkdir(parents=True, exist_ok=True)

    loaded = trimesh.load(input_path, file_type="glb", force="scene", process=False)
    mesh = loaded.to_geometry() if isinstance(loaded, trimesh.Scene) else loaded
    if not isinstance(mesh, trimesh.Trimesh) or mesh.vertices.size == 0:
        raise ValueError(f"No triangle mesh found in {input_path}")

    obj_text = export_obj(
        mesh,
        include_normals=False,
        include_color=False,
        include_texture=False,
        digits=8,
    )
    output_path.write_text(obj_text.rstrip() + "\n", encoding="utf-8", newline="\n")
    metadata = {
        "toolId": TOOL_ID,
        "toolVersion": TOOL_VERSION,
        "trimeshVersion": trimesh.__version__,
        "inputFile": input_path.name,
        "inputSha256": sha256(input_path),
        "outputFile": output_path.name,
        "outputSha256": sha256(output_path),
        "vertexCount": int(len(mesh.vertices)),
        "faceCount": int(len(mesh.faces)),
        "boundsMin": [float(value) for value in mesh.bounds[0]],
        "boundsMax": [float(value) for value in mesh.bounds[1]],
        "units": "Unity donor local units; assumed metres for controlled comparison",
        "coordinateNote": "Geometry is exported in the local coordinates supplied by AssetRipper.",
        "normalNote": "Normals are intentionally recalculated by the Unity reference importer.",
    }
    metadata_path.write_text(
        json.dumps(metadata, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(metadata, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
