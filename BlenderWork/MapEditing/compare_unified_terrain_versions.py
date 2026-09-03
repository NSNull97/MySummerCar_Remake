from pathlib import Path

import bpy
import numpy as np


PREVIOUS_BLEND = Path(
    r"E:\GAYmDev_Studio\MySummerCar_Remake\BlenderWork\MapEditing"
    r"\MSC_UnifiedBaseTerrain_4m_v004.blend"
)
OBJECT_NAME = "MSC_UnifiedBaseTerrain_4m"


current = bpy.context.scene.objects.get(OBJECT_NAME)
if current is None or current.type != "MESH":
    raise RuntimeError(f"Current terrain object was not found: {OBJECT_NAME}")

with bpy.data.libraries.load(str(PREVIOUS_BLEND), link=False) as (source, target):
    target.objects = [name for name in source.objects if name == OBJECT_NAME]

previous = next(
    (obj for obj in target.objects if obj is not None and obj.type == "MESH"),
    None,
)
if previous is None:
    raise RuntimeError(f"Previous terrain object was not found in {PREVIOUS_BLEND}")
if len(previous.data.vertices) != len(current.data.vertices):
    raise RuntimeError("Terrain topology differs; vertex comparison is invalid")

current_coordinates = np.empty(len(current.data.vertices) * 3, dtype=np.float32)
previous_coordinates = np.empty(len(previous.data.vertices) * 3, dtype=np.float32)
current.data.vertices.foreach_get("co", current_coordinates)
previous.data.vertices.foreach_get("co", previous_coordinates)
current_coordinates = current_coordinates.reshape((-1, 3))
previous_coordinates = previous_coordinates.reshape((-1, 3))

difference = current_coordinates[:, 2] - previous_coordinates[:, 2]
changed = np.flatnonzero(np.abs(difference) > 0.01)
largest = np.argsort(np.abs(difference))[-20:][::-1]

print(
    "UNIFIED_TERRAIN_VERSION_DIFFERENCE "
    f"changed_over_1cm={len(changed)} "
    f"minimum_delta_m={float(np.min(difference)):.6f} "
    f"maximum_delta_m={float(np.max(difference)):.6f}",
    flush=True,
)
for index in largest:
    x, y, z = current_coordinates[index]
    print(
        "UNIFIED_TERRAIN_CHANGED_VERTEX "
        f"index={int(index)} x={float(x):.3f} y={float(y):.3f} "
        f"new_z={float(z):.3f} delta_m={float(difference[index]):.6f}",
        flush=True,
    )
