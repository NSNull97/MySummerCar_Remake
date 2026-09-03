"""Re-import and validate the generated Unity FBX in a clean Blender scene."""

import json
import os

import bpy


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=os.environ["ARMS_KURWA_FBX"])

report = {"objects": [], "armatures": [], "actions": []}
for obj in sorted(bpy.data.objects, key=lambda candidate: candidate.name):
    entry = {"name": obj.name, "type": obj.type, "parent": obj.parent.name if obj.parent else None}
    if obj.type == "MESH":
        obj.data.calc_loop_triangles()
        entry.update(
            {
                "vertices": len(obj.data.vertices),
                "triangles": len(obj.data.loop_triangles),
                "shape_keys": (
                    [key.name for key in obj.data.shape_keys.key_blocks]
                    if obj.data.shape_keys
                    else []
                ),
                "vertex_groups": [group.name for group in obj.vertex_groups],
                "modifiers": [modifier.type for modifier in obj.modifiers],
            }
        )
    report["objects"].append(entry)
    if obj.type == "ARMATURE":
        report["armatures"].append(
            {
                "name": obj.name,
                "bones": [bone.name for bone in obj.data.bones],
                "bone_count": len(obj.data.bones),
            }
        )

for action in sorted(bpy.data.actions, key=lambda candidate: candidate.name):
    report["actions"].append(
        {
            "name": action.name,
            "frame_range": [round(value, 5) for value in action.frame_range],
            "fcurves": len(getattr(action, "fcurves", [])),
            "slots": [
                getattr(slot, "name_display", getattr(slot, "name", ""))
                for slot in getattr(action, "slots", [])
            ],
        }
    )

print("===AXIS_UNITY_FBX_AUDIT===")
print(json.dumps(report, ensure_ascii=False, indent=2))
print("===END_AXIS_UNITY_FBX_AUDIT===")
