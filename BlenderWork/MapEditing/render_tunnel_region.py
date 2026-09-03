import math
from pathlib import Path

import bpy
from mathutils import Vector


OUTPUT_DIRECTORY = Path(
    r"E:\GAYmDev_Studio\MySummerCar_Remake\BlenderWork\MapEditing"
)
TERRAIN_NAME = "MSC_UnifiedBaseTerrain_4m"
TARGET = Vector((135.0, 2495.0, 2.0))


terrain = bpy.context.scene.objects.get(TERRAIN_NAME)
if terrain is None or terrain.type != "MESH":
    raise RuntimeError(f"Terrain object not found: {TERRAIN_NAME}")
OUTPUT = OUTPUT_DIRECTORY / (Path(bpy.data.filepath).stem + "_tunnel_region.png")

for obj in bpy.context.scene.objects:
    if obj.type == "MESH" and obj != terrain and obj.name != "RAILWAY_REFERENCE":
        obj.hide_render = True

terrain_material = terrain.data.materials[0]
terrain_material.use_nodes = True
terrain_bsdf = terrain_material.node_tree.nodes.get("Principled BSDF")
terrain_bsdf.inputs["Base Color"].default_value = (0.16, 0.32, 0.10, 1.0)
terrain_bsdf.inputs["Roughness"].default_value = 0.92

railway = bpy.context.scene.objects.get("RAILWAY_REFERENCE")
if railway is not None:
    railway.hide_render = False
    material = railway.data.materials[0]
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.65, 0.08, 0.02, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.72

camera_data = bpy.data.cameras.new("TunnelDiagnosticCamera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = 430.0
camera_data.clip_start = 0.5
camera_data.clip_end = 5000.0
camera = bpy.data.objects.new("TunnelDiagnosticCamera", camera_data)
bpy.context.scene.collection.objects.link(camera)
camera.location = TARGET + Vector((300.0, -330.0, 260.0))
camera.rotation_euler = (TARGET - camera.location).to_track_quat("-Z", "Y").to_euler()
bpy.context.scene.camera = camera

sun_data = bpy.data.lights.new("TunnelDiagnosticSun", type="SUN")
sun_data.energy = 4.0
sun_data.angle = math.radians(8.0)
sun = bpy.data.objects.new("TunnelDiagnosticSun", sun_data)
bpy.context.scene.collection.objects.link(sun)
sun.rotation_euler = (math.radians(35.0), math.radians(-25.0), math.radians(-25.0))

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(OUTPUT)
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("TunnelDiagnosticWorld")
scene.world.use_nodes = True
background = scene.world.node_tree.nodes.get("Background")
if background is not None:
    background.inputs["Color"].default_value = (0.055, 0.075, 0.10, 1.0)
    background.inputs["Strength"].default_value = 0.35

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.render.render(write_still=True)
print(f"TUNNEL_REGION_PREVIEW_PASS {OUTPUT}", flush=True)
