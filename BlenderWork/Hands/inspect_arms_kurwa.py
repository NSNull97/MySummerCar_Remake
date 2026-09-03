"""Print a compact, deterministic inventory of the currently opened arm source.

Run with Blender in background mode and with auto-execution disabled.  The
script deliberately does not mutate or save the purchased source file.
"""

import json

import bpy


def rounded(values):
    return [round(value, 6) for value in values]


inventory = {
    "blender_version": bpy.app.version_string,
    "scene": bpy.context.scene.name,
    "collections": [collection.name for collection in bpy.data.collections],
    "libraries": [library.filepath for library in bpy.data.libraries],
    "objects": [],
    "armatures": [],
    "actions": [],
    "images": [],
    "scene_settings": {
        "fps": bpy.context.scene.render.fps,
        "unit_system": bpy.context.scene.unit_settings.system,
        "unit_scale": bpy.context.scene.unit_settings.scale_length,
    },
}

for obj in sorted(bpy.data.objects, key=lambda candidate: candidate.name):
    entry = {
        "name": obj.name,
        "type": obj.type,
        "parent": obj.parent.name if obj.parent else None,
        "location": rounded(obj.location),
        "rotation_mode": obj.rotation_mode,
        "rotation_euler": rounded(obj.rotation_euler),
        "scale": rounded(obj.scale),
        "hidden_viewport": obj.hide_viewport,
        "hidden_render": obj.hide_render,
    }
    if obj.type == "MESH":
        mesh = obj.data
        mesh.calc_loop_triangles()
        entry.update(
            {
                "mesh": mesh.name,
                "vertices": len(mesh.vertices),
                "triangles": len(mesh.loop_triangles),
                "uv_layers": [layer.name for layer in mesh.uv_layers],
                "vertex_groups": len(obj.vertex_groups),
                "shape_keys": (
                    [key.name for key in mesh.shape_keys.key_blocks]
                    if mesh.shape_keys
                    else []
                ),
                "modifiers": [
                    {
                        "name": modifier.name,
                        "type": modifier.type,
                        "target": getattr(
                            getattr(modifier, "object", None), "name", None
                        ),
                    }
                    for modifier in obj.modifiers
                ],
                "materials": [
                    material.name if material else None
                    for material in mesh.materials
                ],
            }
        )
    inventory["objects"].append(entry)

    if obj.name == "Neutral_Arms" and obj.type == "MESH":
        group_usage = []
        for group in obj.vertex_groups:
            vertex_count = 0
            weight_sum = 0.0
            for vertex in obj.data.vertices:
                for membership in vertex.groups:
                    if membership.group == group.index:
                        vertex_count += 1
                        weight_sum += membership.weight
                        break
            group_usage.append(
                {
                    "name": group.name,
                    "vertices": vertex_count,
                    "weight_sum": round(weight_sum, 4),
                }
            )
        inventory["neutral_arms_vertex_groups"] = group_usage

for armature_object in sorted(
    (obj for obj in bpy.data.objects if obj.type == "ARMATURE"),
    key=lambda candidate: candidate.name,
):
    bones = list(armature_object.data.bones)
    inventory["armatures"].append(
        {
            "object": armature_object.name,
            "data": armature_object.data.name,
            "bone_count": len(bones),
            "deform_bones": [bone.name for bone in bones if bone.use_deform],
            "non_deform_count": sum(1 for bone in bones if not bone.use_deform),
            "pose_constraint_count": sum(
                len(pose_bone.constraints)
                for pose_bone in armature_object.pose.bones
            ),
            "custom_shape_count": sum(
                1
                for pose_bone in armature_object.pose.bones
                if pose_bone.custom_shape
            ),
            "driver_count": (
                len(armature_object.animation_data.drivers)
                if armature_object.animation_data
                else 0
            ),
            "action": (
                armature_object.animation_data.action.name
                if armature_object.animation_data
                and armature_object.animation_data.action
                else None
            ),
            "right_arm_bones": [
                {
                    "name": bone.name,
                    "parent": bone.parent.name if bone.parent else None,
                    "use_deform": bone.use_deform,
                    "head_local": rounded(bone.head_local),
                    "tail_local": rounded(bone.tail_local),
                    "length": round(bone.length, 6),
                    "inherit_scale": bone.inherit_scale,
                }
                for bone in bones
                if bone.name.endswith(".R")
                or bone.name.startswith("r_")
                or bone.name in {"root", "torso", "spine_fk.003"}
            ],
            "right_arm_controls": [
                {
                    "name": pose_bone.name,
                    "parent": (
                        pose_bone.parent.name if pose_bone.parent else None
                    ),
                    "rotation_mode": pose_bone.rotation_mode,
                    "custom_properties": {
                        key: pose_bone[key]
                        for key in pose_bone.keys()
                        if key != "_RNA_UI"
                        and isinstance(pose_bone[key], (bool, int, float, str))
                    },
                    "constraints": [
                        {
                            "name": constraint.name,
                            "type": constraint.type,
                            "target": getattr(
                                getattr(constraint, "target", None),
                                "name",
                                None,
                            ),
                            "subtarget": getattr(
                                constraint, "subtarget", None
                            ),
                            "influence": round(constraint.influence, 4),
                            "mute": constraint.mute,
                        }
                        for constraint in pose_bone.constraints
                    ],
                }
                for pose_bone in armature_object.pose.bones
                if pose_bone.name.endswith(".R")
                and (
                    "arm" in pose_bone.name.lower()
                    or "hand" in pose_bone.name.lower()
                    or "thumb" in pose_bone.name.lower()
                    or "index" in pose_bone.name.lower()
                    or "middle" in pose_bone.name.lower()
                    or "ring" in pose_bone.name.lower()
                    or "pinky" in pose_bone.name.lower()
                    or "palm" in pose_bone.name.lower()
                    or "shoulder" in pose_bone.name.lower()
                )
            ],
        }
    )

shape_keys = bpy.data.shape_keys.get("Key")
if shape_keys is not None and shape_keys.animation_data is not None:
    inventory["shape_key_drivers"] = [
        {
            "data_path": driver.data_path,
            "expression": driver.driver.expression,
            "variables": [
                {
                    "name": variable.name,
                    "type": variable.type,
                    "targets": [
                        {
                            "id": target.id.name if target.id else None,
                            "bone_target": target.bone_target,
                            "transform_type": target.transform_type,
                            "transform_space": target.transform_space,
                            "data_path": target.data_path,
                        }
                        for target in variable.targets
                    ],
                }
                for variable in driver.driver.variables
            ],
        }
        for driver in shape_keys.animation_data.drivers
    ]

for action in sorted(bpy.data.actions, key=lambda candidate: candidate.name):
    slots = getattr(action, "slots", [])
    inventory["actions"].append(
        {
            "name": action.name,
            "frame_range": rounded(action.frame_range),
            "slots": [
                getattr(slot, "name_display", getattr(slot, "name", ""))
                for slot in slots
            ],
            "fcurves": len(getattr(action, "fcurves", [])),
        }
    )

for image in sorted(bpy.data.images, key=lambda candidate: candidate.name):
    inventory["images"].append(
        {
            "name": image.name,
            "filepath": image.filepath,
            "packed": image.packed_file is not None,
            "size": list(image.size),
        }
    )

print("===ARMS_KURWA_INVENTORY===")
print(json.dumps(inventory, ensure_ascii=False, indent=2))
print("===END_ARMS_KURWA_INVENTORY===")
