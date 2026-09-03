import math
import time
import json
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


SOURCE_OBJ = Path(
    r"E:\GAYmDev_Studio\MySummerCar_Remake\BlenderWork\BaseMap_SourceExport"
    r"\20260803_162611_222Z\Scene\Ground_All.obj"
)
ROAD_SOURCE_OBJ = SOURCE_OBJ.with_name("Roads_All.obj")
SOURCE_MANIFEST = SOURCE_OBJ.parents[1] / "Metadata" / "manifest.json"
AIRPORT_RECORD_ID = "288890f0f2b2577c"
OUTPUT_BLEND = Path(
    r"E:\GAYmDev_Studio\MySummerCar_Remake\BlenderWork\MapEditing"
    r"\MSC_UnifiedBaseTerrain_4m_v006.blend"
)
OUTPUT_REPORT = OUTPUT_BLEND.with_suffix(".txt")

OBJECT_NAME = "MSC_UnifiedBaseTerrain_4m"
MATERIAL_NAME = "MSC_UnifiedBaseTerrain_Material"
GRID_STEP_METERS = 4.0
SMOOTH_ITERATIONS = 2
SMOOTH_STRENGTH = 0.24
SMOOTH_MAXIMUM_DISPLACEMENT_METERS = 0.25
RAY_MARGIN_METERS = 50.0
EXCLUDED_OBJECT_MARKERS = ("__StaticProp___",)
ROAD_INCLUDED_PREFIXES = ("RoadAsphalt__", "RoadDirtOrGravel__")
RAILWAY_INCLUDED_PREFIX = "RoadStructure__Road___RAILROAD"
ROAD_EXCLUDED_MARKERS = ("___Roadside___",)
ROAD_CLEARANCE_METERS = 0.30
ROAD_SHOULDER_BLEND_METERS = 12.0
ROAD_SUPPORT_MARGIN_METERS = 3.0
ROAD_ELEVATED_SEPARATION_METERS = 1.5
ROAD_MINIMUM_ABSOLUTE_NORMAL_Z = 0.65
NARROW_DEPRESSION_HALF_WIDTH_CELLS = 4
NARROW_DEPRESSION_MINIMUM_DEPTH_METERS = 8.0
NARROW_DEPRESSION_MAXIMUM_SHOULDER_DELTA_METERS = 6.0
ROAD_REFERENCE_COLLECTION = "ROAD_REFERENCE_DO_NOT_EXPORT"
ROAD_ALWAYS_GROUND_MARKERS = (
    "RoadAirport__",
)


def log(message):
    print(f"[MSC terrain] {message}", flush=True)


def parse_ground_obj(filepath):
    """Read Unity Y-up OBJ and convert coordinates to Blender Z-up."""
    vertices = []
    polygons = []
    object_names = []
    current_object_is_included = True

    with filepath.open("r", encoding="utf-8", errors="replace") as stream:
        for line in stream:
            if line.startswith("v "):
                parts = line.split()
                unity_x = float(parts[1])
                unity_y = float(parts[2])
                unity_z = float(parts[3])
                vertices.append((unity_x, -unity_z, unity_y))
            elif line.startswith("f "):
                if not current_object_is_included:
                    continue
                indices = []
                for token in line.split()[1:]:
                    raw_index = int(token.split("/", 1)[0])
                    if raw_index < 0:
                        index = len(vertices) + raw_index
                    else:
                        index = raw_index - 1
                    indices.append(index)
                if len(indices) >= 3:
                    polygons.append(tuple(indices))
            elif line.startswith("o "):
                object_name = line[2:].strip()
                current_object_is_included = not any(
                    marker in object_name for marker in EXCLUDED_OBJECT_MARKERS
                )
                if current_object_is_included:
                    object_names.append(object_name)

    if not vertices or not polygons:
        raise RuntimeError(f"No mesh data found in {filepath}")

    return vertices, polygons, object_names


def parse_road_obj(filepath):
    """Read road-bed faces and keep their source object identity."""
    vertices = []
    polygons = []
    object_names = []
    current_object = ""
    current_object_is_included = False

    with filepath.open("r", encoding="utf-8", errors="replace") as stream:
        for line in stream:
            if line.startswith("v "):
                parts = line.split()
                unity_x = float(parts[1])
                unity_y = float(parts[2])
                unity_z = float(parts[3])
                vertices.append((unity_x, -unity_z, unity_y))
            elif line.startswith("f "):
                if not current_object_is_included:
                    continue
                indices = []
                for token in line.split()[1:]:
                    raw_index = int(token.split("/", 1)[0])
                    if raw_index < 0:
                        index = len(vertices) + raw_index
                    else:
                        index = raw_index - 1
                    indices.append(index)
                if len(indices) >= 3:
                    polygons.append((tuple(indices), current_object))
            elif line.startswith("o "):
                current_object = line[2:].strip()
                current_object_is_included = (
                    current_object.startswith(ROAD_INCLUDED_PREFIXES)
                    and not any(
                        marker in current_object
                        for marker in ROAD_EXCLUDED_MARKERS
                    )
                ) or current_object.startswith(RAILWAY_INCLUDED_PREFIX)
                if current_object_is_included:
                    object_names.append(current_object)

    if not vertices or not polygons:
        raise RuntimeError(f"No road-bed mesh data found in {filepath}")

    return vertices, polygons, object_names


def parse_transformed_instance_obj(export_root, manifest_path, record_id):
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    entry = next(
        (candidate for candidate in manifest["entries"] if candidate["recordId"] == record_id),
        None,
    )
    if entry is None:
        raise RuntimeError(f"Manifest record was not found: {record_id}")
    geometry_path = export_root / entry["geometryFile"]
    matrix = entry["worldMatrix"]["values"]
    vertices = []
    polygons = []
    object_name = "RoadAirport__StaticProp___AIRPORT___MAP___MESH"
    with geometry_path.open("r", encoding="utf-8", errors="replace") as stream:
        for line in stream:
            if line.startswith("v "):
                parts = line.split()
                local_x = float(parts[1])
                local_y = float(parts[2])
                local_z = float(parts[3])
                unity_x = (
                    matrix[0] * local_x + matrix[1] * local_y
                    + matrix[2] * local_z + matrix[3]
                )
                unity_y = (
                    matrix[4] * local_x + matrix[5] * local_y
                    + matrix[6] * local_z + matrix[7]
                )
                unity_z = (
                    matrix[8] * local_x + matrix[9] * local_y
                    + matrix[10] * local_z + matrix[11]
                )
                vertices.append((unity_x, -unity_z, unity_y))
            elif line.startswith("f "):
                indices = []
                for token in line.split()[1:]:
                    raw_index = int(token.split("/", 1)[0])
                    index = len(vertices) + raw_index if raw_index < 0 else raw_index - 1
                    indices.append(index)
                if len(indices) >= 3:
                    polygons.append((tuple(indices), object_name))
    if not vertices or not polygons:
        raise RuntimeError(f"Airport geometry was empty: {geometry_path}")
    return vertices, polygons, object_name


def calculate_bounds(vertices):
    coordinates = np.asarray(vertices, dtype=np.float64)
    minimum = coordinates.min(axis=0)
    maximum = coordinates.max(axis=0)

    min_x = math.floor(minimum[0] / GRID_STEP_METERS) * GRID_STEP_METERS
    min_y = math.floor(minimum[1] / GRID_STEP_METERS) * GRID_STEP_METERS
    max_x = math.ceil(maximum[0] / GRID_STEP_METERS) * GRID_STEP_METERS
    max_y = math.ceil(maximum[1] / GRID_STEP_METERS) * GRID_STEP_METERS

    return minimum, maximum, min_x, min_y, max_x, max_y


def sample_heightfield(bvh, min_x, min_y, max_x, max_y, source_min_z, source_max_z):
    x_values = np.arange(min_x, max_x + GRID_STEP_METERS * 0.5, GRID_STEP_METERS)
    y_values = np.arange(min_y, max_y + GRID_STEP_METERS * 0.5, GRID_STEP_METERS)
    heights = np.full((len(y_values), len(x_values)), np.nan, dtype=np.float64)

    ray_origin_z = source_max_z + RAY_MARGIN_METERS
    ray_distance = (source_max_z - source_min_z) + RAY_MARGIN_METERS * 2.0
    ray_direction = Vector((0.0, 0.0, -1.0))

    started = time.perf_counter()
    for row_index, y in enumerate(y_values):
        for column_index, x in enumerate(x_values):
            location, _normal, _face_index, _distance = bvh.ray_cast(
                Vector((float(x), float(y), ray_origin_z)),
                ray_direction,
                ray_distance,
            )
            if location is not None:
                heights[row_index, column_index] = location.z

        if row_index % 50 == 0 or row_index == len(y_values) - 1:
            elapsed = time.perf_counter() - started
            log(f"sampled row {row_index + 1}/{len(y_values)} ({elapsed:.1f}s)")

    return x_values, y_values, heights


def fill_missing_heights(heights):
    original_valid = np.isfinite(heights)
    if not original_valid.any():
        raise RuntimeError("Vertical sampling did not hit the source ground")

    filled = heights.copy()
    x_indices = np.arange(filled.shape[1])
    y_indices = np.arange(filled.shape[0])

    # First bridge road slots and other horizontal gaps. np.interp also extends
    # the nearest known edge value into unsupported portions of a row.
    for row_index in range(filled.shape[0]):
        valid = np.isfinite(filled[row_index])
        if valid.any():
            filled[row_index] = np.interp(
                x_indices,
                x_indices[valid],
                filled[row_index, valid],
            )

    # Then fill completely unsupported rows from neighbouring supported rows.
    for column_index in range(filled.shape[1]):
        valid = np.isfinite(filled[:, column_index])
        if valid.any():
            filled[:, column_index] = np.interp(
                y_indices,
                y_indices[valid],
                filled[valid, column_index],
            )

    if not np.isfinite(filled).all():
        fallback = float(np.nanmedian(heights))
        filled[~np.isfinite(filled)] = fallback

    return filled, original_valid


def smooth_heightfield(heights):
    original = heights.copy()
    smoothed = heights.copy()
    for _iteration in range(SMOOTH_ITERATIONS):
        neighbour_average = (
            smoothed[:-2, 1:-1]
            + smoothed[2:, 1:-1]
            + smoothed[1:-1, :-2]
            + smoothed[1:-1, 2:]
        ) * 0.25
        center = smoothed[1:-1, 1:-1]
        local_range = np.maximum.reduce((
            np.abs(smoothed[:-2, 1:-1] - center),
            np.abs(smoothed[2:, 1:-1] - center),
            np.abs(smoothed[1:-1, :-2] - center),
            np.abs(smoothed[1:-1, 2:] - center),
        ))
        slope_protection = 1.0 / (1.0 + local_range * 1.5)
        candidate = center + (
            neighbour_average - center
        ) * SMOOTH_STRENGTH * slope_protection
        smoothed[1:-1, 1:-1] = np.clip(
            candidate,
            original[1:-1, 1:-1] - SMOOTH_MAXIMUM_DISPLACEMENT_METERS,
            original[1:-1, 1:-1] + SMOOTH_MAXIMUM_DISPLACEMENT_METERS,
        )
    return smoothed


def repair_catastrophic_narrow_depressions(heights):
    """Close only deep, narrow source-mesh tears while retaining real ravines."""
    source = heights.copy()
    repaired = heights.copy()
    changed = np.zeros(heights.shape, dtype=bool)
    half_width = NARROW_DEPRESSION_HALF_WIDTH_CELLS

    center = source[half_width:-half_width, :]
    negative_side = source[:-2 * half_width, :]
    positive_side = source[2 * half_width:, :]
    candidate = (negative_side + positive_side) * 0.5
    repair = (
        np.minimum(negative_side, positive_side) - center
        >= NARROW_DEPRESSION_MINIMUM_DEPTH_METERS
    ) & (
        np.abs(negative_side - positive_side)
        <= NARROW_DEPRESSION_MAXIMUM_SHOULDER_DELTA_METERS
    )
    repaired_center = repaired[half_width:-half_width, :]
    repaired_center[repair] = np.maximum(repaired_center[repair], candidate[repair])
    changed[half_width:-half_width, :] |= repair

    center = source[:, half_width:-half_width]
    negative_side = source[:, :-2 * half_width]
    positive_side = source[:, 2 * half_width:]
    candidate = (negative_side + positive_side) * 0.5
    repair = (
        np.minimum(negative_side, positive_side) - center
        >= NARROW_DEPRESSION_MINIMUM_DEPTH_METERS
    ) & (
        np.abs(negative_side - positive_side)
        <= NARROW_DEPRESSION_MAXIMUM_SHOULDER_DELTA_METERS
    )
    repaired_center = repaired[:, half_width:-half_width]
    repaired_center[repair] = np.maximum(repaired_center[repair], candidate[repair])
    changed[:, half_width:-half_width] |= repair

    deltas = repaired - source
    return repaired, {
        "repaired_point_count": int(np.count_nonzero(changed)),
        "maximum_raise_m": float(np.max(deltas)),
    }


def sample_grid_height(heights, min_x, min_y, x, y):
    grid_x = (x - min_x) / GRID_STEP_METERS
    grid_y = (y - min_y) / GRID_STEP_METERS
    column = int(math.floor(grid_x))
    row = int(math.floor(grid_y))
    column = min(max(column, 0), heights.shape[1] - 2)
    row = min(max(row, 0), heights.shape[0] - 2)
    tx = min(max(grid_x - column, 0.0), 1.0)
    ty = min(max(grid_y - row, 0.0), 1.0)
    lower_left = heights[row, column]
    lower_right = heights[row, column + 1]
    upper_left = heights[row + 1, column]
    upper_right = heights[row + 1, column + 1]
    # Match the actual LL-LR-UR / LL-UR-UL triangle split produced from the
    # Blender quad by the FBX exporter. A bilinear sample can miss the diagonal
    # protrusions that were visible in Unity as alternating covered road strips.
    if tx >= ty:
        return float(
            lower_left * (1.0 - tx)
            + lower_right * (tx - ty)
            + upper_right * ty
        )
    return float(
        lower_left * (1.0 - ty)
        + upper_right * tx
        + upper_left * (ty - tx)
    )


def triangulate_road_polygons(vertices, polygons):
    triangles = []
    vertical_rejected = 0
    for polygon, object_name in polygons:
        for index in range(1, len(polygon) - 1):
            a = np.asarray(vertices[polygon[0]], dtype=np.float64)
            b = np.asarray(vertices[polygon[index]], dtype=np.float64)
            c = np.asarray(vertices[polygon[index + 1]], dtype=np.float64)
            normal = np.cross(b - a, c - a)
            magnitude = float(np.linalg.norm(normal))
            if magnitude <= 1e-8 or abs(float(normal[2])) / magnitude < ROAD_MINIMUM_ABSOLUTE_NORMAL_Z:
                vertical_rejected += 1
                continue
            triangles.append((a, b, c, object_name))
    return triangles, vertical_rejected


def nearest_segment_height(point_x, point_y, a, b):
    dx = float(b[0] - a[0])
    dy = float(b[1] - a[1])
    denominator = dx * dx + dy * dy
    if denominator <= 1e-12:
        t = 0.0
    else:
        t = min(max(
            ((point_x - float(a[0])) * dx + (point_y - float(a[1])) * dy)
            / denominator,
            0.0,
        ), 1.0)
    nearest_x = float(a[0]) + dx * t
    nearest_y = float(a[1]) + dy * t
    distance = math.hypot(point_x - nearest_x, point_y - nearest_y)
    height = float(a[2]) + float(b[2] - a[2]) * t
    return distance, height


def triangle_distance_and_height(point_x, point_y, a, b, c):
    denominator = (
        (float(b[1]) - float(c[1])) * (float(a[0]) - float(c[0]))
        + (float(c[0]) - float(b[0])) * (float(a[1]) - float(c[1]))
    )
    if abs(denominator) > 1e-12:
        weight_a = (
            (float(b[1]) - float(c[1])) * (point_x - float(c[0]))
            + (float(c[0]) - float(b[0])) * (point_y - float(c[1]))
        ) / denominator
        weight_b = (
            (float(c[1]) - float(a[1])) * (point_x - float(c[0]))
            + (float(a[0]) - float(c[0])) * (point_y - float(c[1]))
        ) / denominator
        weight_c = 1.0 - weight_a - weight_b
        if weight_a >= -1e-7 and weight_b >= -1e-7 and weight_c >= -1e-7:
            height = (
                weight_a * float(a[2])
                + weight_b * float(b[2])
                + weight_c * float(c[2])
            )
            return 0.0, height

    candidates = (
        nearest_segment_height(point_x, point_y, a, b),
        nearest_segment_height(point_x, point_y, b, c),
        nearest_segment_height(point_x, point_y, c, a),
    )
    return min(candidates, key=lambda candidate: candidate[0])


def smoothstep01(value):
    value = min(max(value, 0.0), 1.0)
    return value * value * (3.0 - 2.0 * value)


def lower_sample_for_road_clearance(heights, min_x, min_y, x, y, allowed):
    grid_x = (x - min_x) / GRID_STEP_METERS
    grid_y = (y - min_y) / GRID_STEP_METERS
    column = int(math.floor(grid_x))
    row = int(math.floor(grid_y))
    if column < 0 or row < 0 or column >= heights.shape[1] - 1 or row >= heights.shape[0] - 1:
        return 0
    current = sample_grid_height(heights, min_x, min_y, x, y)
    excess = current - allowed
    if excess <= 0.0:
        return 0
    tx = min(max(grid_x - column, 0.0), 1.0)
    ty = min(max(grid_y - row, 0.0), 1.0)
    if tx >= ty:
        weighted_vertices = (
            (row, column, 1.0 - tx),
            (row, column + 1, tx - ty),
            (row + 1, column + 1, ty),
        )
    else:
        weighted_vertices = (
            (row, column, 1.0 - ty),
            (row + 1, column + 1, tx),
            (row + 1, column, ty - tx),
        )
    denominator = sum(weight * weight for _row, _column, weight in weighted_vertices)
    correction = excess + 0.002
    for vertex_row, vertex_column, weight in weighted_vertices:
        if weight > 0.0:
            heights[vertex_row, vertex_column] -= correction * weight / denominator
    return 1


def apply_road_constraints(heights, x_values, y_values, road_vertices, road_polygons):
    triangles, vertical_rejected = triangulate_road_polygons(
        road_vertices,
        road_polygons,
    )
    accepted = []
    elevated_rejected = 0
    min_x = float(x_values[0])
    min_y = float(y_values[0])
    for a, b, c, object_name in triangles:
        centroid = (a + b + c) / 3.0
        ground_height = sample_grid_height(
            heights,
            min_x,
            min_y,
            float(centroid[0]),
            float(centroid[1]),
        )
        always_ground = object_name.startswith(ROAD_ALWAYS_GROUND_MARKERS)
        vertical_separation = float(centroid[2]) - ground_height
        is_railway = object_name.startswith(RAILWAY_INCLUDED_PREFIX)
        is_tunnel = "___RAILROAD_TUNNEL___" in object_name
        if (
            is_tunnel
            or (
                is_railway
                and abs(vertical_separation) > ROAD_ELEVATED_SEPARATION_METERS
            )
            or (
                not is_railway
                and not always_ground
                and vertical_separation > ROAD_ELEVATED_SEPARATION_METERS
            )
        ):
            elevated_rejected += 1
            continue
        accepted.append((a, b, c, object_name))

    constraint_distance = np.full(heights.shape, np.inf, dtype=np.float32)
    constraint_height = np.full(heights.shape, np.nan, dtype=np.float32)
    started = time.perf_counter()
    for triangle_index, (a, b, c, _object_name) in enumerate(accepted):
        minimum_x = min(float(a[0]), float(b[0]), float(c[0])) - ROAD_SHOULDER_BLEND_METERS
        maximum_x = max(float(a[0]), float(b[0]), float(c[0])) + ROAD_SHOULDER_BLEND_METERS
        minimum_y = min(float(a[1]), float(b[1]), float(c[1])) - ROAD_SHOULDER_BLEND_METERS
        maximum_y = max(float(a[1]), float(b[1]), float(c[1])) + ROAD_SHOULDER_BLEND_METERS
        column_min = max(0, int(math.floor((minimum_x - min_x) / GRID_STEP_METERS)))
        column_max = min(
            heights.shape[1] - 1,
            int(math.ceil((maximum_x - min_x) / GRID_STEP_METERS)),
        )
        row_min = max(0, int(math.floor((minimum_y - min_y) / GRID_STEP_METERS)))
        row_max = min(
            heights.shape[0] - 1,
            int(math.ceil((maximum_y - min_y) / GRID_STEP_METERS)),
        )
        for row in range(row_min, row_max + 1):
            point_y = float(y_values[row])
            for column in range(column_min, column_max + 1):
                point_x = float(x_values[column])
                distance, road_height = triangle_distance_and_height(
                    point_x,
                    point_y,
                    a,
                    b,
                    c,
                )
                if distance > ROAD_SHOULDER_BLEND_METERS:
                    continue
                current_distance = float(constraint_distance[row, column])
                current_height = float(constraint_height[row, column])
                if (
                    distance < current_distance - 1e-6
                    or abs(distance - current_distance) <= 1e-6
                    and (not math.isfinite(current_height) or road_height > current_height)
                ):
                    constraint_distance[row, column] = distance
                    constraint_height[row, column] = road_height

        if triangle_index % 10000 == 0 or triangle_index == len(accepted) - 1:
            elapsed = time.perf_counter() - started
            log(
                f"road constraints {triangle_index + 1}/{len(accepted)} "
                f"({elapsed:.1f}s)"
            )

    constrained = np.isfinite(constraint_height)
    for row, column in np.argwhere(constrained):
        distance = float(constraint_distance[row, column])
        if distance <= 0.0:
            weight = 1.0
        else:
            weight = 1.0 - smoothstep01(
                distance / ROAD_SHOULDER_BLEND_METERS
            )
        target = float(constraint_height[row, column]) - ROAD_CLEARANCE_METERS
        current = float(heights[row, column])
        blended = current * (1.0 - weight) + target * weight
        if distance <= ROAD_SUPPORT_MARGIN_METERS:
            # A 4 m grid can miss a narrow strip completely. Allow a bounded
            # three-metre support band to raise nearby vertices so the route
            # edge meets the terrain, without filling wider donor ravines.
            heights[row, column] = blended
        elif target < current:
            # Outside the footprint, remove only terrain that would cover the
            # route. Never raise a donor depression: that preserves roadside
            # ditches and the small ravines around the Peräjärvi exit.
            heights[row, column] = blended

    pre_clearance_maximum_protrusion = 0.0
    pre_clearance_maximum_gap = 0.0
    for a, b, c, _object_name in accepted:
        centroid = (a + b + c) / 3.0
        terrain_height = sample_grid_height(
            heights,
            min_x,
            min_y,
            float(centroid[0]),
            float(centroid[1]),
        )
        difference = terrain_height - (float(centroid[2]) - ROAD_CLEARANCE_METERS)
        pre_clearance_maximum_protrusion = max(
            pre_clearance_maximum_protrusion,
            difference,
        )
        pre_clearance_maximum_gap = max(pre_clearance_maximum_gap, -difference)
    log(
        "road grid constraints before continuous clearance: "
        f"protrusion={pre_clearance_maximum_protrusion:.6f} m, "
        f"gap={pre_clearance_maximum_gap:.6f} m"
    )

    clearance_cell_adjustments = 0
    for a, b, c, _object_name in accepted:
        # Project only the three heightfield vertices that actually contribute
        # to each sample. This satisfies clearance without repeatedly lowering
        # all four cell corners and creating a visible floating-road gap.
        samples = (a, b, c, (a + b + c) / 3.0)
        for sample in samples:
            allowed = float(sample[2]) - ROAD_CLEARANCE_METERS - 0.01
            clearance_cell_adjustments += lower_sample_for_road_clearance(
                heights,
                min_x,
                min_y,
                float(sample[0]),
                float(sample[1]),
                allowed,
            )

    maximum_protrusion = 0.0
    maximum_gap = 0.0
    maximum_gap_location = (0.0, 0.0, 0.0)
    maximum_gap_object = ""
    maximum_protrusion_location = (0.0, 0.0, 0.0)
    maximum_protrusion_object = ""
    object_clearance_stats = {}
    for a, b, c, object_name in accepted:
        for sample in (a, b, c, (a + b + c) / 3.0):
            terrain_height = sample_grid_height(
                heights,
                min_x,
                min_y,
                float(sample[0]),
                float(sample[1]),
            )
            difference = terrain_height - (float(sample[2]) - ROAD_CLEARANCE_METERS)
            object_stats = object_clearance_stats.setdefault(
                object_name,
                {
                    "maximum_protrusion_m": 0.0,
                    "maximum_gap_m": 0.0,
                    "maximum_gap_location": (0.0, 0.0, 0.0),
                    "gap_samples": [],
                },
            )
            object_stats["maximum_protrusion_m"] = max(
                object_stats["maximum_protrusion_m"],
                difference,
            )
            gap = max(0.0, -difference)
            object_stats["gap_samples"].append(gap)
            if gap > object_stats["maximum_gap_m"]:
                object_stats["maximum_gap_m"] = gap
                object_stats["maximum_gap_location"] = tuple(
                    float(value) for value in sample
                )
            if difference > maximum_protrusion:
                maximum_protrusion = difference
                maximum_protrusion_location = tuple(float(value) for value in sample)
                maximum_protrusion_object = object_name
            if -difference > maximum_gap:
                maximum_gap = -difference
                maximum_gap_location = tuple(float(value) for value in sample)
                maximum_gap_object = object_name

    for object_stats in object_clearance_stats.values():
        gap_samples = np.asarray(object_stats.pop("gap_samples"), dtype=np.float64)
        object_stats["mean_gap_m"] = float(np.mean(gap_samples))
        object_stats["p95_gap_m"] = float(np.percentile(gap_samples, 95.0))

    return heights, {
        "source_triangle_count": len(triangles) + vertical_rejected,
        "accepted_triangle_count": len(accepted),
        "vertical_rejected_count": vertical_rejected,
        "elevated_rejected_count": elevated_rejected,
        "constrained_grid_point_count": int(constrained.sum()),
        "pre_clearance_maximum_protrusion_m": pre_clearance_maximum_protrusion,
        "pre_clearance_maximum_gap_m": pre_clearance_maximum_gap,
        "clearance_cell_adjustment_count": clearance_cell_adjustments,
        "maximum_protrusion_m": maximum_protrusion,
        "maximum_protrusion_location": maximum_protrusion_location,
        "maximum_protrusion_object": maximum_protrusion_object,
        "maximum_gap_m": maximum_gap,
        "maximum_gap_location": maximum_gap_location,
        "maximum_gap_object": maximum_gap_object,
        "object_clearance_stats": object_clearance_stats,
    }


def create_heightfield_mesh(x_values, y_values, heights):
    row_count, column_count = heights.shape
    vertex_count = row_count * column_count
    quad_count = (row_count - 1) * (column_count - 1)

    grid_x, grid_y = np.meshgrid(x_values, y_values)
    coordinates = np.empty((vertex_count, 3), dtype=np.float32)
    coordinates[:, 0] = grid_x.ravel().astype(np.float32)
    coordinates[:, 1] = grid_y.ravel().astype(np.float32)
    coordinates[:, 2] = heights.ravel().astype(np.float32)

    cell_rows, cell_columns = np.indices((row_count - 1, column_count - 1))
    lower_left = (cell_rows * column_count + cell_columns).ravel().astype(np.int32)
    loops = np.empty((quad_count, 4), dtype=np.int32)
    loops[:, 0] = lower_left
    loops[:, 1] = lower_left + 1
    loops[:, 2] = lower_left + column_count + 1
    loops[:, 3] = lower_left + column_count

    mesh = bpy.data.meshes.new(OBJECT_NAME + "_Mesh")
    mesh.vertices.add(vertex_count)
    mesh.vertices.foreach_set("co", coordinates.ravel())

    loop_count = quad_count * 4
    mesh.loops.add(loop_count)
    mesh.loops.foreach_set("vertex_index", loops.ravel())
    mesh.polygons.add(quad_count)
    mesh.polygons.foreach_set(
        "loop_start",
        np.arange(0, loop_count, 4, dtype=np.int32),
    )
    mesh.polygons.foreach_set(
        "loop_total",
        np.full(quad_count, 4, dtype=np.int32),
    )
    mesh.update(calc_edges=True)

    terrain = bpy.data.objects.new(OBJECT_NAME, mesh)
    bpy.context.scene.collection.objects.link(terrain)
    return terrain, vertex_count, quad_count


def assign_material(terrain):
    material = bpy.data.materials.new(MATERIAL_NAME)
    material.diffuse_color = (0.28, 0.34, 0.22, 1.0)
    material.roughness = 0.95
    material.metallic = 0.0
    terrain.data.materials.append(material)


def create_reference_object(name, source_vertices, polygons, material_color, collection):
    used_indices = sorted({index for polygon, _object_name in polygons for index in polygon})
    remap = {source_index: local_index for local_index, source_index in enumerate(used_indices)}
    vertices = [source_vertices[index] for index in used_indices]
    faces = [tuple(remap[index] for index in polygon) for polygon, _object_name in polygons]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    reference = bpy.data.objects.new(name, mesh)
    collection.objects.link(reference)
    material = bpy.data.materials.new(name + "_Material")
    material.diffuse_color = material_color
    material.roughness = 0.9
    mesh.materials.append(material)
    reference["reference_only"] = True
    reference["unity_export"] = False
    return reference


def create_road_references(source_vertices, source_polygons):
    collection = bpy.data.collections.new(ROAD_REFERENCE_COLLECTION)
    bpy.context.scene.collection.children.link(collection)
    groups = {
        "ROAD_ASPHALT_REFERENCE": (
            [entry for entry in source_polygons if entry[1].startswith("RoadAsphalt__") and "___Roadside___" not in entry[1]],
            (0.035, 0.04, 0.045, 1.0),
        ),
        "ROAD_DIRT_GRAVEL_REFERENCE": (
            [entry for entry in source_polygons if entry[1].startswith("RoadDirtOrGravel__")],
            (0.18, 0.095, 0.035, 1.0),
        ),
        "ROAD_AIRPORT_REFERENCE": (
            [entry for entry in source_polygons if entry[1].startswith("RoadAirport__")],
            (0.12, 0.13, 0.15, 1.0),
        ),
        "RAILWAY_REFERENCE": (
            [entry for entry in source_polygons if entry[1].startswith(RAILWAY_INCLUDED_PREFIX)],
            (0.13, 0.09, 0.055, 1.0),
        ),
    }
    references = {}
    for name, (polygons, color) in groups.items():
        if polygons:
            references[name] = create_reference_object(
                name,
                source_vertices,
                polygons,
                color,
                collection,
            )
    return references


def configure_scene(terrain):
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    bpy.ops.object.select_all(action="DESELECT")
    terrain.select_set(True)
    bpy.context.view_layer.objects.active = terrain


def write_report(
    terrain,
    source_names,
    source_vertex_count,
    source_face_count,
    source_minimum,
    source_maximum,
    x_values,
    y_values,
    original_valid,
    depression_repair_stats,
    vertex_count,
    base_quad_count,
    output_polygon_count,
    output_triangle_count,
    integrated_asphalt_triangle_count,
    integrated_dirt_gravel_triangle_count,
    road_names,
    road_face_count,
    road_stats,
    elapsed_seconds,
):
    sampled_count = int(original_valid.sum())
    filled_count = int(original_valid.size - sampled_count)
    lines = [
        "MSC unified base terrain build",
        "",
        f"Output: {OUTPUT_BLEND}",
        f"Object: {terrain.name}",
        f"Source: {SOURCE_OBJ}",
        f"Source objects: {len(source_names)}",
        f"Source vertices: {source_vertex_count}",
        f"Source faces: {source_face_count}",
        f"Source bounds min XYZ: {source_minimum.tolist()}",
        f"Source bounds max XYZ: {source_maximum.tolist()}",
        f"Grid columns x rows: {len(x_values)} x {len(y_values)}",
        f"Grid step: {GRID_STEP_METERS} m",
        f"Output vertices: {vertex_count}",
        f"Base heightfield quads: {base_quad_count}",
        f"Output base-terrain polygons: {output_polygon_count}",
        f"Output base-terrain triangles: {output_triangle_count}",
        f"Integrated asphalt triangles: {integrated_asphalt_triangle_count}",
        f"Integrated dirt/gravel triangles: {integrated_dirt_gravel_triangle_count}",
        f"Directly sampled grid points: {sampled_count}",
        f"Filled grid points: {filled_count}",
        f"Smoothing: {SMOOTH_ITERATIONS} slope-protected iterations at {SMOOTH_STRENGTH}",
        f"Maximum smoothing displacement: {SMOOTH_MAXIMUM_DISPLACEMENT_METERS} m",
        f"Catastrophic narrow-depression repaired points: {depression_repair_stats['repaired_point_count']}",
        f"Catastrophic narrow-depression maximum raise: {depression_repair_stats['maximum_raise_m']:.6f} m",
        f"Narrow-depression repair minimum depth: {NARROW_DEPRESSION_MINIMUM_DEPTH_METERS} m",
        f"Narrow-depression repair maximum width: {NARROW_DEPRESSION_HALF_WIDTH_CELLS * GRID_STEP_METERS * 2.0} m",
        f"Road source objects: {len(road_names)}",
        f"Road source faces: {road_face_count}",
        f"Road source triangles: {road_stats['source_triangle_count']}",
        f"Accepted near-ground road triangles: {road_stats['accepted_triangle_count']}",
        f"Rejected elevated/separated route triangles: {road_stats['elevated_rejected_count']}",
        f"Rejected near-vertical road triangles: {road_stats['vertical_rejected_count']}",
        f"Road-constrained grid points: {road_stats['constrained_grid_point_count']}",
        f"Pre-clearance maximum terrain protrusion at road centroids: {road_stats['pre_clearance_maximum_protrusion_m']:.6f} m",
        f"Pre-clearance maximum terrain gap at road centroids: {road_stats['pre_clearance_maximum_gap_m']:.6f} m",
        f"Continuous-clearance cell adjustments: {road_stats['clearance_cell_adjustment_count']}",
        f"Maximum terrain protrusion at accepted road centroids: {road_stats['maximum_protrusion_m']:.6f} m",
        f"Maximum protrusion location XYZ: {road_stats['maximum_protrusion_location']}",
        f"Maximum protrusion source object: {road_stats['maximum_protrusion_object']}",
        f"Maximum terrain gap below accepted road centroids: {road_stats['maximum_gap_m']:.6f} m",
        f"Maximum gap location XYZ: {road_stats['maximum_gap_location']}",
        f"Maximum gap source object: {road_stats['maximum_gap_object']}",
        f"Road clearance: {ROAD_CLEARANCE_METERS} m",
        f"Road and shoulder blend: {ROAD_SHOULDER_BLEND_METERS} m",
        f"Road support margin: {ROAD_SUPPORT_MARGIN_METERS} m",
        f"Build duration: {elapsed_seconds:.1f} seconds",
        "Exact asphalt and dirt/gravel render meshes included as Unity terrain FBX submeshes: no",
        "Exact asphalt and dirt/gravel kept as separate active Unity road objects: yes",
        "Original hard-edged Roadside geometry integrated into base-terrain mesh: no",
        "Road shoulder transition generated by the 12 m terrain blend: yes",
        "Existing donor depressions beyond the 3 m road support margin remain unchanged except deterministic narrow source tears deeper than 8 m: yes",
        "Airport and railway reference meshes included in Blender master: yes",
        "Railway tunnel excluded from terrain deformation: yes",
        "Asphalt, pavement, gravel, dirt road, airport and railway used as height constraints: yes",
        "Buildings included in output: no",
        "Vegetation objects included in output: no",
        "StaticProp/4tie used as elevation evidence: no",
        "Ground-labelled Grass1/Grass2 used only as elevation evidence: yes",
        "Interior holes: none (regular complete quad grid)",
        "Outer boundary: open, as expected for a terrain surface",
        "",
        "Source ground objects:",
    ]
    lines.extend(f"- {name}" for name in source_names)
    lines.append("")
    lines.append("Source road objects:")
    lines.extend(f"- {name}" for name in road_names)
    lines.append("")
    lines.append("Road clearance by source object:")
    for object_name in sorted(road_stats["object_clearance_stats"]):
        object_stats = road_stats["object_clearance_stats"][object_name]
        lines.append(
            f"- {object_name}: protrusion "
            f"{object_stats['maximum_protrusion_m']:.6f} m, gap "
            f"max {object_stats['maximum_gap_m']:.6f} m, "
            f"p95 {object_stats['p95_gap_m']:.6f} m, "
            f"mean {object_stats['mean_gap_m']:.6f} m, "
            f"max location {object_stats['maximum_gap_location']}"
        )
    report = "\n".join(lines) + "\n"

    text_block = bpy.data.texts.new("UNIFIED_BASE_TERRAIN_BUILD_REPORT")
    text_block.write(report)
    OUTPUT_REPORT.write_text(report, encoding="utf-8")

    terrain["grid_step_m"] = GRID_STEP_METERS
    terrain["grid_columns"] = len(x_values)
    terrain["grid_rows"] = len(y_values)
    terrain["filled_point_count"] = filled_count
    terrain["direct_sample_point_count"] = sampled_count
    terrain["smooth_iterations"] = SMOOTH_ITERATIONS
    terrain["smooth_strength"] = SMOOTH_STRENGTH
    terrain["smooth_maximum_displacement_m"] = SMOOTH_MAXIMUM_DISPLACEMENT_METERS
    terrain["narrow_depression_repaired_point_count"] = depression_repair_stats["repaired_point_count"]
    terrain["narrow_depression_maximum_raise_m"] = depression_repair_stats["maximum_raise_m"]
    terrain["source_obj"] = str(SOURCE_OBJ)
    terrain["road_source_obj"] = str(ROAD_SOURCE_OBJ)
    terrain["road_clearance_m"] = ROAD_CLEARANCE_METERS
    terrain["road_shoulder_blend_m"] = ROAD_SHOULDER_BLEND_METERS
    terrain["road_support_margin_m"] = ROAD_SUPPORT_MARGIN_METERS
    terrain["road_constrained_grid_points"] = road_stats["constrained_grid_point_count"]
    terrain["maximum_road_protrusion_m"] = road_stats["maximum_protrusion_m"]
    terrain["integrated_asphalt_triangle_count"] = integrated_asphalt_triangle_count
    terrain["integrated_dirt_gravel_triangle_count"] = integrated_dirt_gravel_triangle_count
    terrain["intended_role"] = "Continuous editing master; tile for Unity streaming"


def main():
    started = time.perf_counter()
    if not SOURCE_OBJ.is_file():
        raise FileNotFoundError(SOURCE_OBJ)
    if not ROAD_SOURCE_OBJ.is_file():
        raise FileNotFoundError(ROAD_SOURCE_OBJ)
    if not SOURCE_MANIFEST.is_file():
        raise FileNotFoundError(SOURCE_MANIFEST)

    log("reading donor ground OBJ")
    vertices, polygons, source_names = parse_ground_obj(SOURCE_OBJ)
    log(f"source: {len(vertices)} vertices, {len(polygons)} faces, {len(source_names)} objects")
    log("reading donor road OBJ")
    road_vertices, road_polygons, road_names = parse_road_obj(ROAD_SOURCE_OBJ)
    airport_vertices, airport_polygons, airport_name = parse_transformed_instance_obj(
        SOURCE_OBJ.parents[1],
        SOURCE_MANIFEST,
        AIRPORT_RECORD_ID,
    )
    airport_index_offset = len(road_vertices)
    road_vertices.extend(airport_vertices)
    road_polygons.extend(
        (
            tuple(index + airport_index_offset for index in polygon),
            object_name,
        )
        for polygon, object_name in airport_polygons
    )
    road_names.append(airport_name)
    log(
        f"roads: {len(road_vertices)} vertices, {len(road_polygons)} faces, "
        f"{len(road_names)} objects"
    )

    source_minimum, source_maximum, min_x, min_y, max_x, max_y = calculate_bounds(vertices)
    log(f"grid bounds X {min_x:.1f}..{max_x:.1f}, Y {min_y:.1f}..{max_y:.1f}")

    log("building source BVH")
    bvh = BVHTree.FromPolygons(vertices, polygons, all_triangles=False, epsilon=0.0001)
    x_values, y_values, heights = sample_heightfield(
        bvh,
        min_x,
        min_y,
        max_x,
        max_y,
        float(source_minimum[2]),
        float(source_maximum[2]),
    )
    del bvh

    log("filling road slots and all remaining unsupported grid points")
    filled_heights, original_valid = fill_missing_heights(heights)
    log(
        f"direct hits {int(original_valid.sum())}/{original_valid.size}; "
        f"filled {int(original_valid.size - original_valid.sum())}"
    )

    log("smoothing heightfield")
    smoothed_heights = smooth_heightfield(filled_heights)

    log("repairing catastrophic narrow source-mesh depressions")
    repaired_heights, depression_repair_stats = repair_catastrophic_narrow_depressions(
        smoothed_heights
    )
    log(
        "narrow-depression repair: "
        f"points={depression_repair_stats['repaired_point_count']}, "
        f"maximum_raise={depression_repair_stats['maximum_raise_m']:.6f} m"
    )

    log("applying near-ground road, airport and railway constraints")
    final_heights, road_stats = apply_road_constraints(
        repaired_heights,
        x_values,
        y_values,
        road_vertices,
        road_polygons,
    )
    log(
        "road validation: "
        f"protrusion={road_stats['maximum_protrusion_m']:.6f} m, "
        f"gap={road_stats['maximum_gap_m']:.6f} m"
    )
    if road_stats["maximum_protrusion_m"] > 0.015:
        raise RuntimeError(
            "Terrain still protrudes above accepted road surfaces by "
            f"{road_stats['maximum_protrusion_m']:.6f} m"
        )

    log("creating single quad mesh")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    terrain, _base_vertex_count, base_quad_count = create_heightfield_mesh(
        x_values,
        y_values,
        final_heights,
    )
    assign_material(terrain)
    create_road_references(road_vertices, road_polygons)
    integrated_asphalt_triangle_count = 0
    integrated_dirt_gravel_triangle_count = 0
    vertex_count = len(terrain.data.vertices)
    output_polygon_count = len(terrain.data.polygons)
    output_triangle_count = sum(
        max(0, len(polygon.vertices) - 2)
        for polygon in terrain.data.polygons
    )
    configure_scene(terrain)

    elapsed = time.perf_counter() - started
    write_report(
        terrain,
        source_names,
        len(vertices),
        len(polygons),
        source_minimum,
        source_maximum,
        x_values,
        y_values,
        original_valid,
        depression_repair_stats,
        vertex_count,
        base_quad_count,
        output_polygon_count,
        output_triangle_count,
        integrated_asphalt_triangle_count,
        integrated_dirt_gravel_triangle_count,
        road_names,
        len(road_polygons),
        road_stats,
        elapsed,
    )

    OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    log(f"saving {OUTPUT_BLEND}")
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND), check_existing=False)
    log(
        f"PASS in {elapsed:.1f}s; {vertex_count} vertices, "
        f"{output_triangle_count} triangles"
    )


main()
