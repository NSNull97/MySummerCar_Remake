"""Read-only measurements of ignored Unity mesh-audit CSVs; no source/asset edits."""
import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path
import numpy as np


def rings(path):
    vertices = np.loadtxt(path, delimiter=',')
    triangles = np.loadtxt(str(path) + '.tri', dtype=np.int32).reshape(-1, 3)
    _, representatives, inverse = np.unique(np.round(vertices, 5), axis=0, return_index=True, return_inverse=True)
    points = vertices[representatives]
    edges = Counter()
    for triangle in inverse[triangles]:
        for a, b in zip(triangle, np.roll(triangle, -1)):
            if a != b:
                edges[tuple(sorted((int(a), int(b))))] += 1
    adjacency = defaultdict(set)
    for (a, b), count in edges.items():
        if count == 1:
            adjacency[a].add(b)
            adjacency[b].add(a)
    groups = []
    seen = set()
    for first in sorted(adjacency):
        if first in seen:
            continue
        queue, members = [first], []
        while queue:
            current = queue.pop()
            if current in seen:
                continue
            seen.add(current)
            members.append(current)
            queue.extend(adjacency[current] - seen)
        cloud = points[members]
        center = cloud.mean(axis=0)
        _, singular, axes = np.linalg.svd(cloud-center, full_matrices=False)
        groups.append(dict(count=len(members), center=center.tolist(), minimum=cloud.min(axis=0).tolist(),
                           maximum=cloud.max(axis=0).tolist(), normal=axes[-1].tolist(),
                           singular=singular.tolist(), vertexIndices=np.where(np.isin(inverse,members))[0].tolist()))
    return dict(mesh=Path(path).name, vertices=len(vertices), welded=len(points), boundaryRings=groups)


def flat_patches(path):
    vertices = np.loadtxt(path, delimiter=',')
    triangles = np.loadtxt(str(path) + '.tri', dtype=np.int32).reshape(-1, 3)
    clouds = vertices[triangles]
    normal = np.cross(clouds[:, 1]-clouds[:, 0], clouds[:, 2]-clouds[:, 0])
    area = np.linalg.norm(normal, axis=1)
    good = area > 1e-12
    normal[good] /= area[good, None]
    offset = (normal * clouds[:, 0]).sum(axis=1)
    keys = np.concatenate((np.round(normal, 3), np.round(offset[:, None], 4)), axis=1)
    groups = defaultdict(list)
    for index, key in enumerate(keys):
        if good[index]:
            groups[tuple(key)].append(index)
    result = []
    for key, indices in groups.items():
        if len(indices) < 4:
            continue
        cloud = np.unique(np.round(clouds[indices].reshape(-1, 3), 6), axis=0)
        result.append(dict(triangles=len(indices), vertices=len(cloud), center=cloud.mean(axis=0).tolist(),
                           minimum=cloud.min(axis=0).tolist(), maximum=cloud.max(axis=0).tolist(), normal=list(key[:3])))
    return dict(mesh=Path(path).name, flatPatches=result)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('mesh', nargs='+')
    parser.add_argument('--patches', action='store_true')
    args = parser.parse_args()
    for mesh in args.mesh:
        print(json.dumps((flat_patches if args.patches else rings)(mesh), separators=(',', ':')))
