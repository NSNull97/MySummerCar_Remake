"""Read-only hierarchy selectors for externally staged plaintext Unity scenes.

Prints provenance metadata only. It neither imports Unity objects nor translates
donor state machines into runnable code. No output files are written.
"""
import argparse
import json
import re
from pathlib import Path


def records(path):
    kind, identity, lines = None, None, []
    with path.open(encoding="utf-8-sig") as source:
        for line in source:
            if line.startswith("--- !u!"):
                if kind in (1, 4):
                    yield kind, identity, "".join(lines)
                match = re.match(r"--- !u!(\d+) &(\d+)", line)
                kind, identity = map(int, match.groups()) if match else (None, None)
                lines = []
            elif kind in (1, 4):
                lines.append(line)
        if kind in (1, 4):
            yield kind, identity, "".join(lines)


def inspect(path, names, root):
    objects, transforms = {}, {}
    for kind, identity, body in records(path):
        if kind == 1:
            name = re.search(r"^  m_Name: (.*)$", body, re.M)
            objects[identity] = {
                "name": name.group(1) if name else "",
                "components": [int(value) for value in re.findall(
                    r"^  - \d+: \{fileID: (\d+)\}", body, re.M)],
            }
        else:
            fields = {}
            for key in ("m_GameObject", "m_Father", "m_LocalPosition", "m_LocalRotation", "m_LocalScale"):
                fields[key] = re.search(r"^  " + key + r": (.*)$", body, re.M).group(1)
            fields["gameObject"] = int(re.search(r"\d+", fields["m_GameObject"]).group())
            fields["parent"] = int(re.search(r"\d+", fields["m_Father"]).group())
            transforms[identity] = fields
    for identity, transform in transforms.items():
        obj = objects[transform["gameObject"]]
        if not re.search(names, obj["name"], re.I):
            continue
        chain, ancestor, visited = [], identity, set()
        while ancestor and ancestor in transforms and ancestor not in visited:
            visited.add(ancestor)
            node = transforms[ancestor]
            chain.append(objects[node["gameObject"]]["name"])
            ancestor = node["parent"]
        hierarchy = "/".join(reversed(chain))
        if root and root not in hierarchy:
            continue
        yield dict(transformId=identity, path=hierarchy, **obj, **transform)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scene", type=Path)
    parser.add_argument("--names", required=True, help="Regular expression on object name")
    parser.add_argument("--root", default="", help="Required hierarchy substring")
    args = parser.parse_args()
    print(json.dumps(list(inspect(args.scene, args.names, args.root)), indent=2, ensure_ascii=False))
