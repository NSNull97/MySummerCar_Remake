"""Independent read-only verification of the saved presentation revision journal."""
import collections
import hashlib
import json
import re
import math
import struct
import sys
from pathlib import Path

ROOT = Path("Artifacts/VegetationRebuild/PresentationRevision")
EXPECTED_SPECIES = collections.Counter(Spruce=33455, Pine=23419, Birch=5018, Aspen=5018)


def validate_population_structure(plan, report):
    """Reject missing/duplicate zero-tree cells before an ID set can hide them."""
    errors = []
    plan_cells = [cell["cell"] for cell in plan["cells"]]
    report_cells = [cell["cell"] for cell in report["cells"]]
    tree_keys = [(tree["cell"], tree["id"]) for tree in plan["trees"]]
    planned_species = collections.Counter(tree["species"] for tree in plan["trees"])
    planned_per_cell = collections.Counter(tree["cell"] for tree in plan["trees"])
    if plan["treeCount"] != 66910 or len(tree_keys) != 66910 or len(set(tree_keys)) != len(tree_keys):
        errors.append("Population plan count/identity uniqueness is invalid")
    if planned_species != EXPECTED_SPECIES:
        errors.append("Population plan differs from approved exact species quotas")
    if plan["cellCount"] != 88 or len(plan_cells) != 88 or len(set(plan_cells)) != 88:
        errors.append("Population plan must contain exactly 88 unique cells")
    if collections.Counter(report_cells) != collections.Counter(plan_cells):
        errors.append("Full report cell identities are missing, duplicated or unexpected")
    if set(planned_per_cell) - set(plan_cells):
        errors.append("Population plan contains trees outside its declared cells")
    if any(cell["trees"] != planned_per_cell[cell["cell"]] for cell in plan["cells"]):
        errors.append("Population plan per-cell tree totals disagree")
    if not report["allCells"] or not report["passed"] or report["treeCount"] != 66910:
        errors.append("Full-map revision incomplete")
    if report["version"] != plan["version"]:
        errors.append("Full report and population plan versions disagree")
    return errors


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path):
    sha = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            sha.update(chunk)
    return sha.hexdigest()


def scalar(value):
    if value.startswith('"'):
        return json.loads(re.sub(r"\\x([0-9a-fA-F]{2})", r"\\u00\1", value))
    return value


def grass_count(path):
    needle = b"\n      - worldPosition:"
    count, tail = 0, b""
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            data = tail + block
            count += data.count(needle)
            tail = data[-(len(needle) - 1):]
    return count


def flat_yaml_list(path, section):
    """Only the existing flat Unity manifest/build list schema is supported."""
    records, current, reading = [], None, False
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if not reading:
            if line == "  " + section + ":":
                reading = True
            elif line == "  " + section + ": []":
                return []
            continue
        if line.startswith("  - "):
            if current is not None:
                records.append(current)
            key, value = line[4:].split(":", 1)
            current = {key: scalar(value.strip())}
        elif line.startswith("    ") and current is not None:
            key, value = line.strip().split(":", 1)
            current[key] = scalar(value.strip())
        elif line.strip():
            break
    if current is not None:
        records.append(current)
    return records


def verify_streaming_metadata(plan, errors):
    manifest = Path("Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset")
    build = Path("ProjectSettings/EditorBuildSettings.asset")
    baseline = Path("Artifacts/VegetationRebuild/Backups/20260831-163439")
    legacy = flat_yaml_list(manifest, "cells")
    globals_now = flat_yaml_list(manifest, "globalScenes")
    if len(legacy) != 49 or legacy != flat_yaml_list(baseline / manifest, "cells"):
        errors.append("Existing 49 legacy cell registrations changed")
    if globals_now != flat_yaml_list(baseline / manifest, "globalScenes"):
        errors.append("Existing global scene registrations changed")
    builds, original_builds = flat_yaml_list(build, "m_Scenes"), flat_yaml_list(baseline / build, "m_Scenes")
    if builds[:len(original_builds)] != original_builds:
        errors.append("Existing build-scene prefix changed")
    layers = flat_yaml_list(manifest, "cellLayers")
    vegetation = [layer for layer in layers if layer["layerId"] == "vegetation"]
    if ([layer for layer in layers if layer["layerId"] != "vegetation"] !=
            [layer for layer in flat_yaml_list(baseline / manifest, "cellLayers") if layer["layerId"] != "vegetation"]):
        errors.append("Unrelated optional scene layers changed")
    planned_paths = {cell["cell"]: cell["scenePath"] for cell in plan["cells"]}
    if collections.Counter(layer["cellId"] for layer in vegetation) != collections.Counter(planned_paths.keys()):
        errors.append("Vegetation layer cell registrations differ from saved population")
    historical = read(Path("Artifacts/VegetationRebuild/full-verification.json"))
    old_layers = {row["cellId"]: row for row in historical["vegetationBuildMappings"]}
    for layer in vegetation:
        previous = old_layers.get(layer["cellId"], {})
        if (int(layer["buildIndex"]) != previous.get("buildIndex") or
                int(layer["minimumLoadingRadiusCells"]) != previous.get("loadRadius") or
                int(layer["minimumUnloadingRadiusCells"]) != previous.get("unloadRadius") or
                layer["scenePath"] != planned_paths.get(layer["cellId"])):
            errors.append("Vegetation scene/radius registration changed: " + layer["cellId"])
    for entry in globals_now + legacy + vegetation:
        index = int(entry["buildIndex"])
        if not 0 <= index < len(builds) or builds[index].get("path") != entry["scenePath"] or builds[index].get("enabled") != "1":
            errors.append("Invalid/disabled scene build mapping: " + entry["scenePath"])
        else:
            guid = re.search(r"^guid: ([a-f0-9]{32})$", Path(entry["scenePath"] + ".meta").read_text(), re.M)
            if guid is None or builds[index].get("guid") != guid.group(1):
                errors.append("Scene GUID/build mapping differs: " + entry["scenePath"])
    return {"legacyCells": len(legacy), "vegetationLayers": len(vegetation),
            "originalBuildPrefixEntries": len(original_builds)}


def verify_billboard_repair(errors):
    """Independently decode the tiny owned mesh, not merely its repair flag."""
    report_path = Path("Artifacts/VegetationRebuild/TreePresentationAudit/billboard-winding-repair.json")
    if not report_path.exists():
        errors.append("Shared billboard winding repair has not been executed")
        return None
    repair = read(report_path)
    expected_path = "Assets/Game/Presentation/Vegetation/Generated/Phase1/Meshes/EngelmannSpruce/EngelmannSpruce_TieredCrossBillboard.asset"
    if repair["path"] != expected_path or repair["guid"] != "2d2e3267396235c4183d9a22bb630b50":
        errors.append("Unexpected billboard repair target")
        return None
    mesh, backup = Path(expected_path), Path(repair["backup"])
    for path, key in [(mesh, "afterSha256"), (backup, "beforeSha256"), (Path(str(mesh) + ".meta"), "metaSha256")]:
        if digest(path) != repair[key]:
            errors.append("Billboard repair evidence differs: " + str(path))

    def decode(path):
        text = path.read_text(encoding="utf-8-sig")
        if not re.search(r"^  m_IndexFormat: 0$", text, re.M):
            raise ValueError("Billboard index format is not UInt16")
        indices = bytes.fromhex(re.search(r"^  m_IndexBuffer: ([0-9a-f]+)$", text, re.M).group(1))
        vertices = bytes.fromhex(re.search(r"^\s+_typelessdata: ([0-9a-f]+)$", text, re.M).group(1))
        channels = re.search(r"    m_Channels:\n(.*?)    m_DataSize:", text, re.S).group(1)
        if len(indices) != 252 or len(vertices) != 84 * 48:
            raise ValueError("Billboard no longer has the audited 84-vertex/42-triangle layout")
        return struct.unpack("<126H", indices), vertices, channels

    before, before_vertices, before_channels = decode(backup)
    after, after_vertices, after_channels = decode(mesh)
    if before_vertices != after_vertices or before_channels != after_channels:
        errors.append("Billboard repair altered vertex/normal/tangent/UV data")
    expected = tuple(value for offset in range(0, 126, 3) for value in
                     (before[offset], before[offset + 2], before[offset + 1])) if repair["changed"] else before
    if after != expected:
        errors.append("Billboard repair changed more than triangle winding")
    minimum_dot = 1.0
    for offset in range(0, 126, 3):
        rows = [struct.unpack_from("<6f", after_vertices, index * 48) for index in after[offset:offset + 3]]
        a, b = [rows[1][i] - rows[0][i] for i in range(3)], [rows[2][i] - rows[0][i] for i in range(3)]
        face = (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
        normal = [sum(row[i + 3] for row in rows) for i in range(3)]
        denominator = math.sqrt(sum(v*v for v in face) * sum(v*v for v in normal))
        dot = sum(face[i] * normal[i] for i in range(3)) / denominator if denominator else -1.0
        if not math.isfinite(dot) or dot < .999:
            errors.append("Billboard face/normal disagreement at triangle " + str(offset // 3))
        minimum_dot = min(minimum_dot, dot)
    return {"triangles": 42, "minimumFaceNormalDot": minimum_dot, "meshSha256": repair["afterSha256"]}


def main():
    if not (ROOT / "report.json").exists():
        print('{"status":"PENDING","reason":"No completed full revision report"}')
        return 2
    plan, report = read(ROOT / "plan.json"), read(ROOT / "report.json")
    errors = validate_population_structure(plan, report)
    if errors:
        print(json.dumps({"status": "FAIL", "errors": errors}, ensure_ascii=False, indent=2))
        return 1
    audit = read(Path("Artifacts/VegetationRebuild/TreePresentationAudit/prefab-lod-and-mix.json"))
    species = {p["guid"]: p["species"] for p in audit["prefabs"]}
    source_colliders = {}
    for prefab in audit["prefabs"]:
        if digest(Path(prefab["path"])) != prefab["sha256"]:
            errors.append("Source prefab differs from audited immutable input: " + prefab["path"])
        enabled = []
        for block in re.split(r"^--- ", Path(prefab["path"]).read_text(encoding="utf-8-sig"), flags=re.M):
            collider = re.match(r"!u!(?:64|65|135|136|143|146) &(-?\d+)", block)
            if collider and re.search(r"^  m_Enabled: 1$", block, re.M):
                enabled.append(collider.group(1))
        source_colliders[prefab["guid"]] = enabled
    choices = {(p["cell"], p["id"]): p["species"] for p in plan["trees"]}
    cell_inputs = {p["cell"]: p for p in plan["cells"]}
    counts, seen = collections.Counter(), set()
    total_grass = 0
    for cell in report["cells"]:
        identity = cell["cell"]
        journal = ROOT / (identity + ".json")
        if not journal.exists() or read(journal) != cell:
            errors.append("Full report disagrees with per-cell checkpoint: " + identity)
        if cell["sourceFingerprint"] != report["sourceFingerprint"]:
            errors.append("Mixed source/ground evidence fingerprints: " + identity)
        cell_counts = collections.Counter()
        scene = Path(cell_inputs[identity]["scenePath"])
        data = Path("Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/Data") / identity
        for path, key in [(scene, "sceneSha256"), (data / "GrassCell.asset", "grassSha256"),
                          (data / "Catalog.asset", "catalogSha256"), (data / "Density.asset", "densitySha256")]:
            if digest(path) != cell[key]:
                errors.append("Saved output differs from validated journal: " + str(path))
        if not cell["passed"] or not cell["positionsPreserved"] or not cell["collisionPreserved"]:
            errors.append("Cell preservation gate failed: " + identity)
        if cell["presentationHash"] != report["presentationHash"] or cell["settingsHash"] != report["settingsHash"]:
            errors.append("Mixed revision input hashes: " + identity)
        actual_grass = grass_count(data / "GrassCell.asset")
        total_grass += actual_grass
        if actual_grass != cell["grassAfter"] or not 0 <= actual_grass <= cell["grassBefore"]:
            errors.append("Grass record count disagrees or new positions were added: " + identity)
        for block in re.split(r"^--- ", scene.read_text(encoding="utf-8-sig"), flags=re.M):
            if not block.startswith("!u!1001 "):
                continue
            source = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([a-f0-9]{32})", block)
            if source is None or source.group(1) not in species:
                continue
            names = re.findall(r"propertyPath: m_Name\s*\n\s*value: ([^\r\n]*)", block)
            if len(names) != 1:
                errors.append("Ambiguous tree identity: " + identity)
                continue
            key = (identity, scalar(names[0]))
            value = species[source.group(1)]
            for collider in source_colliders[source.group(1)]:
                disabled_values = re.findall(r"- target: \{fileID: " + re.escape(collider) +
                    r", guid: " + source.group(1) + r", type: 3\}\s*\n\s*propertyPath: m_Enabled\s*\n\s*value: ([^\r\n]*)", block)
                if disabled_values != ["0"]:
                    errors.append("Authored collider was not disabled: " + str(key) + "/" + collider)
            if key in seen or choices.get(key) != value:
                errors.append("Duplicate, unexpected or wrongly bound tree: " + str(key))
            seen.add(key)
            counts[value] += 1
            cell_counts[value] += 1
        if sum(cell_counts.values()) != cell["trees"] or cell["trees"] != cell_inputs[identity]["trees"]:
            errors.append("Serialized tree count disagrees with plan/checkpoint: " + identity)
        if any(cell_counts[name] != cell[name.lower()] for name in EXPECTED_SPECIES):
            errors.append("Serialized species counts disagree with checkpoint: " + identity)
    if seen != choices.keys():
        errors.append("Saved tree identities differ from original population plan")
    expected = collections.Counter(p["species"] for p in plan["trees"])
    if counts != expected or counts != EXPECTED_SPECIES or sum(counts.values()) != report["treeCount"]:
        errors.append("Species quotas/population mismatch")
    if total_grass != report["grassAfter"] or sum(cell["grassBefore"] for cell in report["cells"]) != report["grassBefore"]:
        errors.append("Full report grass totals disagree with its checkpoints/serialized records")
    streaming = verify_streaming_metadata(plan, errors)
    billboard = verify_billboard_repair(errors)
    result = {"status": "PASS" if not errors else "FAIL", "cells": len(report["cells"]),
              "trees": sum(counts.values()), "species": counts, "grass": total_grass,
              "grassBefore": report["grassBefore"], "planSha256": digest(ROOT / "plan.json"),
              "reportSha256": digest(ROOT / "report.json"), "streaming": streaming,
              "billboardWinding": billboard, "errors": errors,
              "limits": "Verifies saved journals, hashes, species/identities and actual grass records. Position/collision/LOD checks are executed by the Unity migration before journaling; no FPS or whole-map visual claim."}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return bool(errors)


if __name__ == "__main__":
    sys.exit(main())
