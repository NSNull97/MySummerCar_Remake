"""Render neutral reference views from the purchased AXIS arms source.

The opened source is never saved.  Output is written to the directory supplied
through ARMS_KURWA_OUTPUT so the contact images can remain local and ignored.
"""

import math
import os
from pathlib import Path

import bpy
from mathutils import Vector


def look_at(camera, target):
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


output_root = Path(os.environ["ARMS_KURWA_OUTPUT"])
output_root.mkdir(parents=True, exist_ok=True)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.look = "AgX - Medium High Contrast"

for obj in list(scene.objects):
    if obj.type in {"CAMERA", "LIGHT"}:
        bpy.data.objects.remove(obj, do_unlink=True)

world = scene.world or bpy.data.worlds.new("Arms Preview World")
scene.world = world
world.use_nodes = True
background = world.node_tree.nodes.get("Background")
background.inputs["Color"].default_value = (0.035, 0.045, 0.06, 1.0)
background.inputs["Strength"].default_value = 0.35

camera_data = bpy.data.cameras.new("Arms Preview Camera")
camera = bpy.data.objects.new("Arms Preview Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera_data.lens = 62

for name, location, energy, size in (
    ("Key", (-1.8, -2.2, 3.0), 1150, 3.0),
    ("Fill", (2.2, -1.0, 1.8), 850, 2.4),
    ("Rim", (0.0, 1.8, 2.3), 1000, 2.0),
):
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light)
    light.location = location
    look_at(light, (0.0, 0.0, 1.25))

mesh = bpy.data.objects["Neutral_Arms"]
for modifier in mesh.modifiers:
    if modifier.type == "SUBSURF":
        modifier.show_render = False

views = (
    ("front", (0.0, -2.35, 1.32), (0.0, 0.0, 1.28), 58),
    ("front_close", (0.0, -1.45, 1.28), (0.0, 0.0, 1.25), 54),
    ("three_quarter", (1.65, -1.75, 1.55), (0.0, 0.0, 1.25), 60),
    ("top", (0.0, -0.18, 3.15), (0.0, 0.0, 1.18), 55),
)

for name, location, target, lens in views:
    camera.location = location
    camera_data.lens = lens
    look_at(camera, target)
    scene.render.filepath = str(output_root / f"neutral_arms_{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"ARMS_RENDER={scene.render.filepath}")
