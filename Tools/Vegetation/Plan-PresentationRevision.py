"""Read-only plan for rebinding the saved forest, never regenerating positions.

Run from the Unity project root. The ignored plan is consumed by the bounded
Editor migration. Input scenes, vendor assets and the donor are never modified.
"""
import collections
import hashlib
import json
import re
from pathlib import Path

ROOT = Path("Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild")
OUT = Path("Artifacts/VegetationRebuild/PresentationRevision")
AUDIT = Path("Artifacts/VegetationRebuild/TreePresentationAudit/prefab-lod-and-mix.json")


def scalar(value):
    if value.startswith('"'):
        value = re.sub(r"\\x([0-9a-fA-F]{2})", r"\\u00\1", value)
        return json.loads(value)
    return value


def main():
    if (OUT / "plan.json").exists():
        raise RuntimeError("A baseline plan already exists; it must not be overwritten after migration.")
    audit = json.loads(AUDIT.read_text(encoding="utf-8-sig"))
    tree_guids = {p["guid"] for p in audit["prefabs"]}
    rows, cells, keys = [], [], set()
    for path in sorted((ROOT / "Scenes").glob("World_Cell_*_Vegetation.unity")):
        raw = path.read_bytes()
        text = raw.decode("utf-8-sig")
        cell = "cell_" + path.stem.removeprefix("World_Cell_").removesuffix("_Vegetation")
        count = 0
        for block in re.split(r"^--- ", text, flags=re.M):
            if not block.startswith("!u!1001 "):
                continue
            source = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([a-f0-9]{32})", block)
            if source is None or source.group(1) not in tree_guids:
                continue
            names = re.findall(r"propertyPath: m_Name\s*\n\s*value: ([^\r\n]*)", block)
            if len(names) != 1:
                raise RuntimeError(f"Expected one saved root name in {path}, found {len(names)}")
            identity = scalar(names[0])
            key = cell + "|" + identity
            if key in keys:
                raise RuntimeError("Duplicate saved tree identity: " + key)
            keys.add(key)
            rows.append({"cell": cell, "id": identity, "species": "",
                         "rank": hashlib.sha256(key.encode()).hexdigest()})
            count += 1
        cells.append({"cell": cell, "scenePath": path.as_posix(),
                      "baselineSha256": hashlib.sha256(raw).hexdigest(), "trees": count})
    if len(rows) != audit["treeCount"] or len(cells) != audit["sceneCount"]:
        raise RuntimeError("Saved population disagrees with the independent audit")
    rows.sort(key=lambda row: (row["rank"], row["cell"], row["id"]))
    # Largest remainder rounds integer counts without changing the population.
    weights = [("Spruce", 50), ("Pine", 35), ("Birch", 7.5), ("Aspen", 7.5)]
    quotas = [int(len(rows) * w / 100) for _, w in weights]
    residual = len(rows) - sum(quotas)
    rounding = sorted(range(4), key=lambda i: (-(len(rows) * weights[i][1] / 100 - quotas[i]), i))
    for i in rounding[:residual]:
        quotas[i] += 1
    cursor = 0
    for (species, _), count in zip(weights, quotas):
        for row in rows[cursor:cursor + count]:
            row["species"] = species
            del row["rank"]
        cursor += count
    OUT.mkdir(parents=True, exist_ok=True)
    result = {"version": "msc.vegetation-presentation-revision.v1", "treeCount": len(rows),
              "cellCount": len(cells), "cells": cells, "trees": rows}
    (OUT / "plan.json").write_text(json.dumps(result, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(json.dumps({"treeCount": len(rows), "cellCount": len(cells),
                      "species": dict(zip((s for s, _ in weights), quotas)),
                      "planSha256": hashlib.sha256((OUT / "plan.json").read_bytes()).hexdigest()}, indent=2))


if __name__ == "__main__":
    main()
