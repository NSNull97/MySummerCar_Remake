"""Audit the licensed AXIS mesh for a safe first-person forearm cut.

The purchased source is opened read-only with auto-execution disabled.  This
script prints topology and skin-weight statistics only; it never saves or
mutates the source blend.
"""

from collections import Counter, defaultdict
import json

import bpy
from mathutils import Vector


mesh_object = bpy.data.objects["Neutral_Arms"]
mesh = mesh_object.data
group_names = {
    group.index: group.name for group in mesh_object.vertex_groups
}


def category(name):
    if not name:
        return "unassigned"
    side = "R" if name.endswith(".R") else "L" if name.endswith(".L") else "C"
    lowered = name.lower()
    distal = any(
        token in lowered
        for token in (
            "forearm",
            "hand",
            "palm",
            "thumb",
            "f_index",
            "f_middle",
            "f_ring",
            "f_pinky",
        )
    )
    upper = "upper_arm" in lowered or "shoulder" in lowered
    if distal:
        region = "distal"
    elif upper:
        region = "upper"
    else:
        region = "central"
    return f"{side}_{region}"


weights_by_vertex = []
dominant_groups = Counter()
category_weight_totals = Counter()
category_vertex_counts = Counter()
category_bounds = {}
for vertex in mesh.vertices:
    weights = [
        (group_names.get(membership.group, ""), membership.weight)
        for membership in vertex.groups
        if membership.weight > 0.0
    ]
    weights_by_vertex.append(weights)
    if weights:
        dominant_groups[max(weights, key=lambda item: item[1])[0]] += 1
    seen_categories = set()
    for name, weight in weights:
        label = category(name)
        category_weight_totals[label] += weight
        seen_categories.add(label)
    for label in seen_categories:
        category_vertex_counts[label] += 1
        point = mesh_object.matrix_world @ vertex.co
        if label not in category_bounds:
            category_bounds[label] = [point.copy(), point.copy()]
        else:
            lower, upper = category_bounds[label]
            lower.x = min(lower.x, point.x)
            lower.y = min(lower.y, point.y)
            lower.z = min(lower.z, point.z)
            upper.x = max(upper.x, point.x)
            upper.y = max(upper.y, point.y)
            upper.z = max(upper.z, point.z)


parents = list(range(len(mesh.vertices)))


def find(value):
    while parents[value] != value:
        parents[value] = parents[parents[value]]
        value = parents[value]
    return value


def union(first, second):
    first_root = find(first)
    second_root = find(second)
    if first_root != second_root:
        parents[second_root] = first_root


for polygon in mesh.polygons:
    vertices = list(polygon.vertices)
    for index in range(1, len(vertices)):
        union(vertices[0], vertices[index])

components = defaultdict(list)
for vertex_index in range(len(mesh.vertices)):
    components[find(vertex_index)].append(vertex_index)


def side_weights(vertex_index, side):
    distal = 0.0
    upper = 0.0
    opposite = 0.0
    central = 0.0
    for name, weight in weights_by_vertex[vertex_index]:
        label = category(name)
        if label == f"{side}_distal":
            distal += weight
        elif label == f"{side}_upper":
            upper += weight
        elif label.startswith(("L_" if side == "R" else "R_")):
            opposite += weight
        else:
            central += weight
    return distal, upper, opposite, central


cuts = {}
for side in ("R", "L"):
    side_result = {}
    for threshold in (
        0.00001,
        0.0001,
        0.001,
        0.005,
        0.01,
        0.025,
        0.05,
        0.1,
        0.2,
        0.35,
        0.5,
        0.65,
        0.8,
    ):
        selected_vertices = set()
        for vertex_index in range(len(mesh.vertices)):
            distal, _upper, opposite, _central = side_weights(
                vertex_index, side
            )
            if distal > threshold and distal > opposite:
                selected_vertices.add(vertex_index)
        retained = 0
        boundary_edges = Counter()
        retained_vertices = set()
        retained_parents = {}

        def retained_find(value):
            retained_parents.setdefault(value, value)
            while retained_parents[value] != value:
                retained_parents[value] = retained_parents[
                    retained_parents[value]
                ]
                value = retained_parents[value]
            return value

        def retained_union(first, second):
            first_root = retained_find(first)
            second_root = retained_find(second)
            if first_root != second_root:
                retained_parents[second_root] = first_root

        for polygon in mesh.polygons:
            vertices = list(polygon.vertices)
            if all(vertex in selected_vertices for vertex in vertices):
                retained += 1
                retained_vertices.update(vertices)
                for index in range(1, len(vertices)):
                    retained_union(vertices[0], vertices[index])
                for index, first in enumerate(vertices):
                    second = vertices[(index + 1) % len(vertices)]
                    boundary_edges[tuple(sorted((first, second)))] += 1
        open_edges = sum(1 for count in boundary_edges.values() if count == 1)
        retained_components = Counter(
            retained_find(vertex) for vertex in retained_vertices
        )
        retained_points = [
            mesh_object.matrix_world @ mesh.vertices[index].co
            for index in retained_vertices
        ]
        side_result[str(threshold)] = {
            "candidate_vertices": len(selected_vertices),
            "retained_vertices": len(retained_vertices),
            "retained_polygons": retained,
            "open_boundary_edges": open_edges,
            "component_vertex_counts": sorted(
                retained_components.values(), reverse=True
            ),
            "bounds_min": [
                round(min(point[axis] for point in retained_points), 6)
                for axis in range(3)
            ] if retained_points else [],
            "bounds_max": [
                round(max(point[axis] for point in retained_points), 6)
                for axis in range(3)
            ] if retained_points else [],
        }
    cuts[side] = side_result


component_report = []
for vertex_indices in sorted(components.values(), key=len, reverse=True):
    points = [mesh_object.matrix_world @ mesh.vertices[index].co for index in vertex_indices]
    lower = Vector((
        min(point.x for point in points),
        min(point.y for point in points),
        min(point.z for point in points),
    ))
    upper = Vector((
        max(point.x for point in points),
        max(point.y for point in points),
        max(point.z for point in points),
    ))
    dominant = Counter()
    for vertex_index in vertex_indices:
        weights = weights_by_vertex[vertex_index]
        if weights:
            dominant[category(max(weights, key=lambda item: item[1])[0])] += 1
    component_report.append(
        {
            "vertices": len(vertex_indices),
            "bounds_min": [round(value, 6) for value in lower],
            "bounds_max": [round(value, 6) for value in upper],
            "dominant_categories": dominant.most_common(),
        }
    )


def membership_weight(vertex_index, group_name):
    for name, weight in weights_by_vertex[vertex_index]:
        if name == group_name:
            return weight
    return 0.0


mask_groups = ("Arm_hair", "Hand_hair", "Nails")
mask_report = {}
for mask_group in mask_groups:
    members = {
        vertex_index
        for vertex_index in range(len(mesh.vertices))
        if membership_weight(vertex_index, mask_group) > 0.5
    }
    polygons = []
    touched_polygons = []
    used_vertices = set()
    touched_vertices = set()
    for polygon in mesh.polygons:
        vertices = set(polygon.vertices)
        if vertices and vertices.issubset(members):
            polygons.append(polygon.index)
            used_vertices.update(vertices)
        elif vertices & members:
            touched_polygons.append(polygon.index)
            touched_vertices.update(vertices)
    mask_report[mask_group] = {
        "members_over_half": len(members),
        "fully_owned_polygons": len(polygons),
        "fully_owned_vertices": len(used_vertices),
        "mixed_polygons": len(touched_polygons),
        "mixed_vertices": len(touched_vertices),
    }


material_report = []
for material_index, material in enumerate(mesh.materials):
    polygon_indices = [
        polygon.index
        for polygon in mesh.polygons
        if polygon.material_index == material_index
    ]
    vertex_indices = {
        vertex_index
        for polygon_index in polygon_indices
        for vertex_index in mesh.polygons[polygon_index].vertices
    }
    material_report.append(
        {
            "index": material_index,
            "name": material.name if material else "<none>",
            "polygons": len(polygon_indices),
            "vertices": len(vertex_indices),
            "dominant_groups": Counter(
                max(weights_by_vertex[index], key=lambda item: item[1])[0]
                for index in vertex_indices
                if weights_by_vertex[index]
            ).most_common(12),
        }
    )


report = {
    "vertices": len(mesh.vertices),
    "polygons": len(mesh.polygons),
    "components": component_report,
    "materials": material_report,
    "mask_groups": mask_report,
    "dominant_groups": dominant_groups.most_common(),
    "category_vertex_counts": dict(category_vertex_counts),
    "category_weight_totals": {
        key: round(value, 4) for key, value in category_weight_totals.items()
    },
    "category_bounds": {
        key: {
            "min": [round(value, 6) for value in bounds[0]],
            "max": [round(value, 6) for value in bounds[1]],
        }
        for key, bounds in category_bounds.items()
    },
    "candidate_distal_cuts": cuts,
}

print("===AXIS_VIEWMODEL_CUT_AUDIT===")
print(json.dumps(report, ensure_ascii=False, indent=2))
print("===END_AXIS_VIEWMODEL_CUT_AUDIT===")
