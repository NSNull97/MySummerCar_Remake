"""Print the AXIS controls needed for first-person arm authoring."""

import json
import os

import bpy


rig = bpy.data.objects["AXIS_Rig"]


def rounded(values):
    return [round(value, 6) for value in values]


def serializable_properties(pose_bone):
    return {
        key: pose_bone[key]
        for key in pose_bone.keys()
        if key != "_RNA_UI"
        and isinstance(pose_bone[key], (bool, int, float, str))
    }


interesting_names = {
    "root",
    "torso",
    "spine_fk.003",
    "shoulder.R",
    "upper_arm_parent.R",
    "upper_arm_fk.R",
    "forearm_fk.R",
    "hand_fk.R",
    "upper_arm_ik.R",
    "upper_arm_ik_target.R",
    "upper_arm_ik_pole.R",
    "hand_ik.R",
    "palm.R",
    "DEF-shoulder.R",
    "DEF-upper_arm.R",
    "DEF-upper_arm.R.001",
    "DEF-forearm.R",
    "DEF-forearm.R.001",
    "DEF-hand.R",
    "r_upperarmtwist1",
    "r_upperarmtwist2",
    "r_forearmtwist1",
    "r_forearmtwist2",
    "ORG-upper_arm.R",
    "ORG-forearm.R",
    "ORG-hand.R",
    "MCH-upper_arm_ik.R",
    "MCH-forearm_ik.R",
    "MCH-upper_arm_parent.R",
}

for prefix in (
    "thumb.01_master.R",
    "f_index.01_master.R",
    "f_middle.01_master.R",
    "f_ring.01_master.R",
    "f_pinky.01_master.R",
    "thumb.01.R",
    "thumb.02.R",
    "thumb.03.R",
    "f_index.01.R",
    "f_index.02.R",
    "f_index.03.R",
    "f_middle.01.R",
    "f_middle.02.R",
    "f_middle.03.R",
    "f_ring.01.R",
    "f_ring.02.R",
    "f_ring.03.R",
    "f_pinky.01.R",
    "f_pinky.02.R",
    "f_pinky.03.R",
    "DEF-thumb.01.R",
    "DEF-thumb.02.R",
    "DEF-thumb.03.R",
    "DEF-f_index.01.R",
    "DEF-f_index.02.R",
    "DEF-f_index.03.R",
    "DEF-f_middle.01.R",
    "DEF-f_middle.02.R",
    "DEF-f_middle.03.R",
    "DEF-f_ring.01.R",
    "DEF-f_ring.02.R",
    "DEF-f_ring.03.R",
    "DEF-f_pinky.01.R",
    "DEF-f_pinky.02.R",
    "DEF-f_pinky.03.R",
):
    interesting_names.add(prefix)

report = []
name_filter = os.environ.get("AXIS_CONTROL_FILTER", "").strip()
for name in sorted(interesting_names):
    if name_filter and name_filter not in name:
        continue
    pose_bone = rig.pose.bones.get(name)
    if pose_bone is None:
        report.append({"name": name, "missing": True})
        continue

    report.append(
        {
            "name": name,
            "parent": pose_bone.parent.name if pose_bone.parent else None,
            "head": rounded(pose_bone.head),
            "tail": rounded(pose_bone.tail),
            "location": rounded(pose_bone.location),
            "rotation_mode": pose_bone.rotation_mode,
            "rotation_euler": rounded(pose_bone.rotation_euler),
            "rotation_quaternion": rounded(pose_bone.rotation_quaternion),
            "scale": rounded(pose_bone.scale),
            "custom_properties": serializable_properties(pose_bone),
            "constraints": [
                {
                    "name": constraint.name,
                    "type": constraint.type,
                    "target": getattr(
                        getattr(constraint, "target", None), "name", None
                    ),
                    "subtarget": getattr(constraint, "subtarget", None),
                    "influence": round(constraint.influence, 4),
                    "mute": constraint.mute,
                    "rotation_limits": (
                        {
                            "use_x": constraint.use_limit_x,
                            "min_x": round(constraint.min_x, 6),
                            "max_x": round(constraint.max_x, 6),
                            "use_y": constraint.use_limit_y,
                            "min_y": round(constraint.min_y, 6),
                            "max_y": round(constraint.max_y, 6),
                            "use_z": constraint.use_limit_z,
                            "min_z": round(constraint.min_z, 6),
                            "max_z": round(constraint.max_z, 6),
                        }
                        if constraint.type == "LIMIT_ROTATION"
                        else None
                    ),
                }
                for constraint in pose_bone.constraints
            ],
        }
    )

arm_parent = rig.pose.bones.get("upper_arm_parent.R")
arm_properties = (
    serializable_properties(arm_parent) if arm_parent is not None else {}
)

print("===AXIS_RIGHT_ARM_CONTROLS===")
print(
    json.dumps(
        {
            "upper_arm_parent_properties": arm_properties,
            "controls": report,
        },
        ensure_ascii=False,
        indent=2,
    )
)
print("===END_AXIS_RIGHT_ARM_CONTROLS===")
