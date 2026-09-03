"""Render a local side-only first-person preview from the authored AXIS action.

The purchased source is opened with --disable-autoexec and is never saved.
This script is review tooling only; Unity consumes the FBX written by the
separate deterministic exporter.
"""

import os
import runpy
from pathlib import Path

import bpy


script_root = Path(__file__).parent
exporter = runpy.run_path(str(script_root / "export_axis_neutral_arms_unity.py"))
helpers = exporter["helpers"]

configure_review_scene = helpers["configure_review_scene"]
create_static_side = helpers["create_static_side"]

preview_root = Path(os.environ["ARMS_KURWA_PREVIEW_DIR"])
preview_root.mkdir(parents=True, exist_ok=True)
action_name = os.environ.get("ARMS_KURWA_PREVIEW_ACTION", "AxisArms_Wave")
side = os.environ.get("ARMS_KURWA_PREVIEW_SIDE", "R")
sample_step = int(os.environ.get("ARMS_KURWA_PREVIEW_FRAME_STEP", "2"))

scene = configure_review_scene()
scene.render.resolution_x = 848
scene.render.resolution_y = 464
scene.render.resolution_percentage = 100

rig = bpy.data.objects[helpers["RIG_NAME"]]
mesh = bpy.data.objects[helpers["MESH_NAME"]]
action = bpy.data.actions[action_name]
rig.animation_data_create()
rig.animation_data.action = action

start = int(action.frame_range[0])
end = int(action.frame_range[1])
mesh.hide_render = True
rig.hide_render = True
rendered = 0
for frame in range(start, end + 1, sample_step):
    scene.frame_set(frame)
    static = create_static_side(mesh, side)
    static.name = f"PreviewFrame_{frame:03d}"
    scene.render.filepath = str(preview_root / f"frame_{rendered:03d}.png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(static, do_unlink=True)
    rendered += 1

mesh.hide_render = False
rig.hide_render = False
print(
    f"AXIS_PREVIEW={action_name}|side={side}|frames={rendered}|{preview_root}"
)
