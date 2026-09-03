from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


OUTPUT = Path(
    r"E:\GAYmDev_Studio\MySummerCar_Remake\BlenderWork\MapEditing"
    r"\MSC_UnifiedBaseTerrain_4m_v005_tunnel_diagnostic.txt"
)
TERRAIN_NAME = "MSC_UnifiedBaseTerrain_4m"
SOURCE_DIRECTORY = Path(
    r"E:\GAYmDev_Studio\MySummerCar_Remake\BlenderWork\BaseMap_SourceExport"
    r"\20260803_162611_222Z\Scene"
)


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = tuple(min(corner[index] for corner in corners) for index in range(3))
    maximum = tuple(max(corner[index] for corner in corners) for index in range(3))
    return minimum, maximum


terrain = bpy.context.scene.objects.get(TERRAIN_NAME)
if terrain is None or terrain.type != "MESH":
    raise RuntimeError(f"Terrain object not found: {TERRAIN_NAME}")

coordinates = np.empty(len(terrain.data.vertices) * 3, dtype=np.float32)
terrain.data.vertices.foreach_get("co", coordinates)
coordinates = coordinates.reshape((-1, 3))

lines = []


def scan_obj_vertex_groups(filepath, predicate):
    groups = []
    current_name = ""
    current_vertices = []

    def flush():
        if current_vertices and predicate(current_name, current_vertices):
            values = np.asarray(current_vertices, dtype=np.float32)
            groups.append((current_name, values.min(axis=0), values.max(axis=0)))

    with filepath.open("r", encoding="utf-8", errors="replace") as stream:
        for line in stream:
            if line.startswith("o "):
                flush()
                current_name = line[2:].strip()
                current_vertices = []
            elif line.startswith("v "):
                parts = line.split()
                current_vertices.append(
                    (float(parts[1]), -float(parts[3]), float(parts[2]))
                )
    flush()
    return groups


rail_groups = scan_obj_vertex_groups(
    SOURCE_DIRECTORY / "Roads_All.obj",
    lambda name, _vertices: "RAILROAD" in name.upper(),
)
for name, minimum, maximum in rail_groups:
    lines.append(
        f"SOURCE_RAIL {name}\n"
        f"  bounds_min={tuple(float(value) for value in minimum)}\n"
        f"  bounds_max={tuple(float(value) for value in maximum)}\n"
    )


def touches_defect(_name, vertices):
    values = np.asarray(vertices, dtype=np.float32)
    for center_x, center_y in ((188.0, 2460.0), (144.0, 2488.0)):
        distance = np.hypot(values[:, 0] - center_x, values[:, 1] - center_y)
        if np.any(distance <= 80.0):
            return True
    return False


ground_groups = scan_obj_vertex_groups(
    SOURCE_DIRECTORY / "Ground_All.obj",
    touches_defect,
)
for name, minimum, maximum in ground_groups:
    lines.append(
        f"SOURCE_GROUND_NEAR_DEFECT {name}\n"
        f"  bounds_min={tuple(float(value) for value in minimum)}\n"
        f"  bounds_max={tuple(float(value) for value in maximum)}\n"
    )

for obj in sorted(bpy.context.scene.objects, key=lambda item: item.name):
    upper_name = obj.name.upper()
    if (
        "RAILROAD" not in upper_name
        and "RAILWAY" not in upper_name
        and "TUNNEL" not in upper_name
    ):
        continue
    minimum, maximum = world_bounds(obj)
    lines.append(
        f"OBJECT {obj.name}\n"
        f"  bounds_min={minimum}\n"
        f"  bounds_max={maximum}\n"
    )

# The largest v004/v005 differences identified by compare_unified_terrain_versions.py
# are grouped here to expose the local heightfield shape around the reported defect.
for center_x, center_y in ((188.0, 2460.0), (144.0, 2488.0)):
    distance = np.hypot(coordinates[:, 0] - center_x, coordinates[:, 1] - center_y)
    local = coordinates[distance <= 64.0]
    lines.append(
        f"REGION center=({center_x}, {center_y}) radius=64m "
        f"min_z={float(local[:, 2].min()):.6f} "
        f"max_z={float(local[:, 2].max()):.6f} "
        f"median_z={float(np.median(local[:, 2])):.6f}\n"
    )

for axis, fixed, start, end in (
    ("x", 2460.0, 80.0, 280.0),
    ("y", 188.0, 2360.0, 2580.0),
):
    if axis == "x":
        mask = np.isclose(coordinates[:, 1], fixed, atol=0.01)
        ordered = coordinates[mask & (coordinates[:, 0] >= start) & (coordinates[:, 0] <= end)]
        ordered = ordered[np.argsort(ordered[:, 0])]
        pairs = [(float(row[0]), float(row[2])) for row in ordered]
    else:
        mask = np.isclose(coordinates[:, 0], fixed, atol=0.01)
        ordered = coordinates[mask & (coordinates[:, 1] >= start) & (coordinates[:, 1] <= end)]
        ordered = ordered[np.argsort(ordered[:, 1])]
        pairs = [(float(row[1]), float(row[2])) for row in ordered]
    lines.append(f"PROFILE axis={axis} fixed={fixed} values={pairs}\n")

for portal_x, portal_y in ((54.0259, 2550.1034), (2651.7832, 768.6986)):
    distance = np.hypot(coordinates[:, 0] - portal_x, coordinates[:, 1] - portal_y)
    anomalous = coordinates[(distance <= 600.0) & (coordinates[:, 2] < -5.0)]
    if len(anomalous):
        lines.append(
            f"LOW_REGION_NEAR_PORTAL portal=({portal_x}, {portal_y}) count={len(anomalous)} "
            f"x=({float(anomalous[:, 0].min()):.3f}, {float(anomalous[:, 0].max()):.3f}) "
            f"y=({float(anomalous[:, 1].min()):.3f}, {float(anomalous[:, 1].max()):.3f}) "
            f"z=({float(anomalous[:, 2].min()):.3f}, {float(anomalous[:, 2].max()):.3f})\n"
        )

report = "".join(lines)
OUTPUT.write_text(report, encoding="utf-8")
print(report, flush=True)
