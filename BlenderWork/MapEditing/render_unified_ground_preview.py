import math
from pathlib import Path

import bpy
from mathutils import Vector


OUTPUT = Path(
    r"E:\GAYmDev_Studio\MySummerCar_Remake\BlenderWork\MapEditing"
    r"\MSC_UnifiedBaseTerrain_4m_v006_preview.png"
)


terrain = bpy.context.scene.objects.get("MSC_UnifiedBaseTerrain_4m")
if terrain is None:
    raise RuntimeError("MSC_UnifiedBaseTerrain_4m was not found")

preview_material = terrain.data.materials[0]
preview_material.use_nodes = True
preview_bsdf = preview_material.node_tree.nodes.get("Principled BSDF")
preview_bsdf.inputs["Base Color"].default_value = (0.055, 0.24, 0.045, 1.0)
preview_bsdf.inputs["Roughness"].default_value = 0.92

world_corners = [terrain.matrix_world @ Vector(corner) for corner in terrain.bound_box]
minimum = Vector((
    min(v.x for v in world_corners),
    min(v.y for v in world_corners),
    min(v.z for v in world_corners),
))
maximum = Vector((
    max(v.x for v in world_corners),
    max(v.y for v in world_corners),
    max(v.z for v in world_corners),
))
center = (minimum + maximum) * 0.5
width = maximum.x - minimum.x
depth = maximum.y - minimum.y

camera_data = bpy.data.cameras.new("PreviewCamera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = max(width, depth) * 1.16
camera_data.clip_start = 1.0
camera_data.clip_end = 50000.0
camera = bpy.data.objects.new("PreviewCamera", camera_data)
bpy.context.scene.collection.objects.link(camera)
camera.location = center + Vector((4500.0, -5200.0, 6100.0))
camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
bpy.context.scene.camera = camera

sun_data = bpy.data.lights.new("PreviewSun", type="SUN")
sun_data.energy = 3.0
sun_data.angle = math.radians(15.0)
sun = bpy.data.objects.new("PreviewSun", sun_data)
bpy.context.scene.collection.objects.link(sun)
sun.rotation_euler = (math.radians(38.0), math.radians(-18.0), math.radians(-32.0))

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(OUTPUT)
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("PreviewWorld")
scene.world.use_nodes = True
world_nodes = scene.world.node_tree.nodes
world_nodes.clear()
background = world_nodes.new("ShaderNodeBackground")
world_output = world_nodes.new("ShaderNodeOutputWorld")
scene.world.node_tree.links.new(background.outputs["Background"], world_output.inputs["Surface"])
background.inputs["Color"].default_value = (0.025, 0.035, 0.045, 1.0)
background.inputs["Strength"].default_value = 0.20

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.render.render(write_still=True)
print(f"PREVIEW_PASS {OUTPUT}")
