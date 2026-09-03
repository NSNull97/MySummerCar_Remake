from pathlib import Path

import bpy
import bmesh


OBJECT_NAME = "MSC_UnifiedBaseTerrain_4m"
OUTPUT_FBX = Path(
    r"E:\GAYmDev_Studio\MySummerCar_Remake\BlenderWork\MapEditing"
    r"\MSC_UnifiedBaseTerrain_4m_v006_Unity.fbx"
)


terrain = bpy.context.scene.objects.get(OBJECT_NAME)
if terrain is None or terrain.type != "MESH":
    raise RuntimeError(f"Required mesh object not found: {OBJECT_NAME}")

# Blender's FBX handedness conversion used for the first Unity export mirrored
# the donor world's east/west axis. Compensate in the temporary export mesh so
# Unity receives X unchanged while the intended Blender Y -> Unity -Z mapping
# remains intact. Reverse the faces after the reflection to keep normals facing
# upward. The .blend source is never saved by this batch export.
mesh = terrain.data
edit_mesh = bmesh.new()
edit_mesh.from_mesh(mesh)
for vertex in edit_mesh.verts:
    vertex.co.x = -vertex.co.x
bmesh.ops.reverse_faces(edit_mesh, faces=list(edit_mesh.faces))
edit_mesh.to_mesh(mesh)
edit_mesh.free()

# Unity must receive one shared smooth heightfield, not face-split flat-shaded
# vertices for every quad.
for polygon in mesh.polygons:
    polygon.use_smooth = True
mesh.update()

bpy.ops.object.select_all(action="DESELECT")
terrain.select_set(True)
bpy.context.view_layer.objects.active = terrain

OUTPUT_FBX.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=str(OUTPUT_FBX),
    check_existing=False,
    use_selection=True,
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    use_space_transform=True,
    bake_space_transform=False,
    object_types={"MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    use_mesh_edges=False,
    use_triangles=True,
    use_custom_props=True,
    bake_anim=False,
    path_mode="AUTO",
    axis_forward="-Z",
    axis_up="Y",
)

print(
    "MSC_UNIFIED_TERRAIN_UNITY_FBX_PASS "
    f"path={OUTPUT_FBX} vertices={len(terrain.data.vertices)} "
    f"polygons={len(terrain.data.polygons)}",
    flush=True,
)
