"""Create the deterministic Unity FBX and texture source from AXIS arms.

The purchased blend must be opened with --disable-autoexec.  Only expression
drivers are enabled after load; the AXIS online-updater/UI text blocks are never
executed.  The source blend is never saved.
"""

import hashlib
import json
import math
import os
import runpy
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


FPS = 60
# The player prefab owns a 120 degree horizontal FOV.  At the project's
# reference 16:9 framing that is 88.50716 degrees vertically.  Export against
# the actual gameplay lens instead of the old 72 degree diagnostic guess;
# otherwise Unity makes the viewmodel visibly larger than the approved pose
# sheets and the drink bottle appears to be jammed into the camera.
UNITY_GAMEPLAY_HORIZONTAL_FOV_DEGREES = 120.0
UNITY_GAMEPLAY_REFERENCE_ASPECT = 16.0 / 9.0
UNITY_GAMEPLAY_VERTICAL_FOV_DEGREES = math.degrees(
    2.0
    * math.atan(
        math.tan(
            math.radians(UNITY_GAMEPLAY_HORIZONTAL_FOV_DEGREES) * 0.5
        )
        / UNITY_GAMEPLAY_REFERENCE_ASPECT
    )
)
CLIP_LENGTHS = {
    "AxisArms_Drink": 11.0,
    "AxisArms_DrinkShort": 1.95,
    "AxisArms_DrinkSpray": 4.0,
    "AxisArms_DrinkThrow": 0.7333333333,
    "AxisArms_Wave": 2.8,
    "AxisArms_MiddleFinger": 1.4,
    "AxisArms_SmokeIn": 0.5,
    "AxisArms_SmokeLightUp": 0.5,
    "AxisArms_SmokeOut": 0.5,
    "AxisArms_SmokePutOff": 0.91683334,
    "AxisArms_SmokeReset": 1.0 / 60.0,
    "AxisArms_PushOn": 0.4,
    "AxisArms_PushOff": 0.41666666,
    "AxisArms_ThumbUp": 0.8,
}

os.environ["ARMS_KURWA_LIBRARY_ONLY"] = "1"
helpers = runpy.run_path(
    str(Path(__file__).with_name("author_axis_first_person_poses.py"))
)

side_name = helpers["side_name"]
reset_pose = helpers["reset_pose"]
pose_arm = helpers["pose_arm"]
pose_open_hand = helpers["pose_open_hand"]
pose_cigarette_grip = helpers["pose_cigarette_grip"]
pose_thumb_up = helpers["pose_thumb_up"]
pose_bottle_grip = helpers["pose_bottle_grip"]
pose_middle_finger = helpers["pose_middle_finger"]
pose_middle_finger_photo_reference = helpers[
    "pose_middle_finger_photo_reference"
]
update_scene = helpers["update_scene"]
clear_generated_preview = helpers["clear_generated_preview"]
configure_review_scene = helpers["configure_review_scene"]
drink_cycle_sample = helpers["drink_cycle_sample"]


def smoothstep(value):
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def smooth_range(value, start, end):
    if end <= start:
        return 1.0 if value >= end else 0.0
    return smoothstep((value - start) / (end - start))


def normalized_lerp(first, second, weight):
    return Vector(first).lerp(Vector(second), weight).normalized()


def blend_spec(first, second, weight):
    weight = smoothstep(weight)
    return {
        "side": first["side"],
        "wrist": Vector(first["wrist"]).lerp(Vector(second["wrist"]), weight),
        "pole": normalized_lerp(first["pole"], second["pole"], weight),
        "forward": normalized_lerp(
            first["forward"], second["forward"], weight
        ),
        "normal": normalized_lerp(first["normal"], second["normal"], weight),
        "hand": second["hand"] if weight >= 0.5 else first["hand"],
    }


RIGHT_HIDDEN = {
    "side": "R",
    "wrist": (-0.25, -0.12, 0.94),
    "pole": (-1.0, 0.4, -0.9),
    "forward": (1.0, 0.0, 0.0),
    "normal": (0.0, 1.0, 0.0),
    "hand": "grip",
}
RIGHT_READY = {
    "side": "R",
    "wrist": (-0.16, -0.385, 1.30),
    "pole": (-1.0, 0.45, -0.8),
    "forward": (1.0, 0.0, 0.0),
    "normal": (0.0, 1.0, 0.0),
    "hand": "grip",
}
RIGHT_MOUTH = {
    "side": "R",
    "wrist": (-0.07, -0.21, 1.40),
    "pole": (-1.0, 0.55, -0.5),
    "forward": (0.78, 0.35, 0.52),
    "normal": (-0.28, 0.93, -0.22),
    "hand": "grip",
}
RIGHT_SPRAY = {
    "side": "R",
    "wrist": (-0.06, -0.48, 1.33),
    "pole": (-1.0, 0.35, -0.7),
    "forward": (0.25, -0.92, 0.30),
    "normal": (0.08, 0.33, 0.94),
    "hand": "grip",
}
RIGHT_THROW = {
    "side": "R",
    "wrist": (0.02, -0.53, 1.47),
    "pole": (-0.8, 0.2, -0.8),
    "forward": (0.2, -0.85, 0.48),
    "normal": (-0.1, 0.48, 0.87),
    "hand": "grip",
}
RIGHT_WAVE_HIDDEN = {
    "side": "R",
    "wrist": (-0.13, -0.09, 0.93),
    "pole": (-1.0, 0.35, -0.95),
    "forward": (0.0, 0.05, 1.0),
    "normal": (0.0, -1.0, 0.05),
    "hand": "open",
}
LEFT_MIDDLE_HIDDEN = {
    "side": "L",
    "wrist": (0.16, -0.09, 0.94),
    "pole": (1.0, 0.35, -0.95),
    "forward": (-0.15, 0.03, 0.988),
    "normal": (0.0, 1.0, -0.03),
    "hand": "middle",
}
LEFT_MIDDLE_RAISED = {
    "side": "L",
    "wrist": (0.12, -0.27, 1.32),
    "pole": (1.0, 0.35, -0.9),
    "forward": (-0.15, 0.03, 0.988),
    "normal": (0.0, 1.0, -0.03),
    "hand": "middle",
}
RIGHT_SMOKE_HIDDEN = {
    "side": "R",
    "wrist": (-0.18, -0.08, 0.94),
    "pole": (-1.0, 0.35, -0.95),
    "forward": (0.22, 0.10, 0.97),
    "normal": (-0.08, -0.99, 0.12),
    "hand": "smoke",
}
RIGHT_SMOKE_READY = {
    "side": "R",
    "wrist": (-0.08, -0.31, 1.29),
    "pole": (-0.78, 0.36, -0.82),
    "forward": (0.29, 0.14, 0.95),
    "normal": (-0.10, -0.98, 0.16),
    "hand": "smoke",
}
RIGHT_SMOKE_MOUTH = {
    "side": "R",
    "wrist": (-0.035, -0.19, 1.39),
    "pole": (-0.72, 0.50, -0.56),
    "forward": (0.43, 0.20, 0.88),
    "normal": (-0.13, -0.96, 0.23),
    "hand": "smoke",
}
RIGHT_PUSH_HIDDEN = {
    "side": "R",
    "wrist": (-0.18, -0.08, 0.94),
    "pole": (-1.0, 0.35, -0.95),
    "forward": (0.0, 0.05, 1.0),
    "normal": (0.0, 1.0, -0.05),
    "hand": "open",
}
RIGHT_PUSH_EXTENDED = {
    "side": "R",
    "wrist": (-0.055, -0.36, 1.27),
    "pole": (-0.72, 0.22, -0.76),
    "forward": (0.03, -0.14, 0.99),
    "normal": (0.02, 0.99, 0.14),
    "hand": "open",
}
LEFT_LIKE_HIDDEN = {
    "side": "L",
    "wrist": (0.16, -0.08, 0.94),
    "pole": (1.0, 0.35, -0.95),
    "forward": (-1.0, 0.02, -0.07),
    "normal": (0.0, 1.0, -0.03),
    "hand": "like",
}
LEFT_LIKE_RAISED = {
    "side": "L",
    "wrist": (0.115, -0.29, 1.30),
    "pole": (0.90, 0.34, -0.86),
    # Rotate the palm around the forearm so the thumb, rather than the curled
    # knuckle row, is vertical in the first-person camera. This matches the
    # user's left-hand reference instead of reading as a sideways hitchhike.
    "forward": (-1.0, 0.02, -0.07),
    "normal": (0.03, 1.0, -0.02),
    "hand": "like",
}


def evaluate_drink(time, length):
    if time <= 0.5:
        return blend_spec(RIGHT_HIDDEN, RIGHT_READY, time / 0.5)
    if time <= 1.45:
        return blend_spec(RIGHT_READY, RIGHT_MOUTH, (time - 0.5) / 0.95)
    lower_start = max(1.45, length - 1.15)
    lower_end = max(1.7, length - 0.45)
    if time <= lower_start:
        return RIGHT_MOUTH
    if time <= lower_end:
        return blend_spec(
            RIGHT_MOUTH,
            RIGHT_READY,
            (time - lower_start) / (lower_end - lower_start),
        )
    return blend_spec(
        RIGHT_READY,
        RIGHT_HIDDEN,
        (time - lower_end) / max(0.001, length - lower_end),
    )


def evaluate_drink_short(time, length):
    if time <= 0.55:
        return blend_spec(RIGHT_READY, RIGHT_MOUTH, time / 0.55)
    return blend_spec(
        RIGHT_MOUTH,
        RIGHT_READY,
        (time - 0.55) / max(0.001, length - 0.55),
    )


def evaluate_wave(time, length):
    entry_end = 0.48
    exit_start = length - 0.48
    entry = smooth_range(time, 0.0, entry_end)
    exit_weight = smooth_range(time, exit_start, length)
    visibility = min(entry, 1.0 - exit_weight)
    active_duration = max(0.001, exit_start - entry_end)
    phase = max(0.0, min(1.0, (time - entry_end) / active_duration))
    # The user reference performs three compact waves.  The forearm moves only
    # a little, while the hand adds a softer, slightly delayed radial deviation.
    # Keeping the two oscillations separate avoids both the rigid-paddle look
    # and the exaggerated full-arm windscreen-wiper motion.
    arm_swing = math.sin(phase * math.pi * 6.0) * visibility
    hand_swing = math.sin(phase * math.pi * 6.0 - 0.18) * visibility
    raised = {
        "side": "R",
        "wrist": (
            -0.03 + 0.014 * arm_swing,
            -0.27,
            1.36 + 0.003 * abs(arm_swing),
        ),
        "pole": (-0.50 + 0.055 * arm_swing, 0.32, -0.88),
        "forward": (0.30 + 0.145 * hand_swing, 0.03, 1.0),
        "normal": (0.0, -1.0, 0.03),
        "hand": "open",
    }
    return blend_spec(RIGHT_WAVE_HIDDEN, raised, visibility)


def evaluate_middle(time, length):
    visibility = min(
        smooth_range(time, 0.0, 0.20),
        1.0 - smooth_range(time, length - 0.20, length),
    )
    return blend_spec(LEFT_MIDDLE_HIDDEN, LEFT_MIDDLE_RAISED, visibility)


def evaluate_smoke_in(time, length):
    return blend_spec(
        RIGHT_SMOKE_HIDDEN,
        RIGHT_SMOKE_READY,
        smooth_range(time, 0.0, length),
    )


def evaluate_smoke_lightup(time, length):
    return blend_spec(
        RIGHT_SMOKE_READY,
        RIGHT_SMOKE_MOUTH,
        smooth_range(time, 0.0, length),
    )


def evaluate_smoke_out(time, length):
    return blend_spec(
        RIGHT_SMOKE_MOUTH,
        RIGHT_SMOKE_READY,
        smooth_range(time, 0.0, length),
    )


def evaluate_smoke_put_off(time, length):
    return blend_spec(
        RIGHT_SMOKE_READY,
        RIGHT_SMOKE_HIDDEN,
        smooth_range(time, 0.0, length),
    )


def evaluate_push_on(time, length):
    return blend_spec(
        RIGHT_PUSH_HIDDEN,
        RIGHT_PUSH_EXTENDED,
        smooth_range(time, 0.0, length),
    )


def evaluate_push_off(time, length):
    return blend_spec(
        RIGHT_PUSH_EXTENDED,
        RIGHT_PUSH_HIDDEN,
        smooth_range(time, 0.0, length),
    )


def evaluate_thumb_up(time, length):
    visibility = min(
        smooth_range(time, 0.0, 0.18),
        1.0 - smooth_range(time, length - 0.18, length),
    )
    return blend_spec(LEFT_LIKE_HIDDEN, LEFT_LIKE_RAISED, visibility)


def apply_spec(rig, spec):
    side = spec["side"]
    pose_arm(
        rig,
        side,
        spec["wrist"],
        spec["pole"],
        spec["forward"],
        spec["normal"],
    )
    if spec["hand"] == "grip":
        pose_bottle_grip(rig, side)
    elif spec["hand"] == "open":
        pose_open_hand(rig, side)
    elif spec["hand"] == "middle":
        pose_middle_finger_photo_reference(rig, side)
    elif spec["hand"] == "smoke":
        pose_cigarette_grip(rig, side)
    elif spec["hand"] == "like":
        pose_thumb_up(rig, side)
    else:
        raise ValueError(f"Unsupported hand pose: {spec['hand']}")


def keyframe_pose(rig, side, frame):
    group = f"AXIS {side} first-person controls"
    arm_parent = rig.pose.bones[side_name("upper_arm_parent", side)]
    for property_name in ("IK_FK", "IK_Stretch", "pole_vector"):
        arm_parent.keyframe_insert(
            data_path=f'["{property_name}"]', frame=frame, group=group
        )
    for control_name in ("hand_ik", "upper_arm_ik_target"):
        control = rig.pose.bones[side_name(control_name, side)]
        control.keyframe_insert("location", frame=frame, group=group)
        control.keyframe_insert(
            "rotation_quaternion", frame=frame, group=group
        )
        control.keyframe_insert("scale", frame=frame, group=group)
    for digit in ("thumb", "f_index", "f_middle", "f_ring", "f_pinky"):
        for joint in ("01", "02", "03"):
            control = rig.pose.bones[side_name(f"{digit}.{joint}", side)]
            control.keyframe_insert("rotation_euler", frame=frame, group=group)


def keyframe_rig_transform(rig, frame):
    """Bake the camera-space composition turn into the exported action."""
    group = "AXIS first-person composition"
    rig.keyframe_insert("location", frame=frame, group=group)
    if rig.rotation_mode == "QUATERNION":
        rig.keyframe_insert("rotation_quaternion", frame=frame, group=group)
    else:
        rig.keyframe_insert("rotation_euler", frame=frame, group=group)
    rig.keyframe_insert("scale", frame=frame, group=group)


def apply_preview_composition_to_rig(rig, diagnostics):
    """Apply the same rigid wrist-pivot turn used by the approved renders."""
    degrees = diagnostics.get("preview_composition_rotation_degrees", 0.0)
    if abs(degrees) <= 1e-6:
        return
    camera = bpy.context.scene.camera
    camera_forward = (
        camera.matrix_world.to_quaternion() @ Vector((0.0, 0.0, -1.0))
    ).normalized()
    pivot = Vector(
        diagnostics.get("preview_composition_pivot", diagnostics["wrist"])
    )
    rigid_rotation = (
        Matrix.Translation(pivot)
        @ Matrix.Rotation(math.radians(degrees), 4, camera_forward)
        @ Matrix.Translation(-pivot)
    )
    rig.matrix_world = rigid_rotation @ rig.matrix_world
    update_scene()


def apply_camera_local_export_space(rig, camera_inverse_world):
    """Convert the authored world-space pose into Unity camera-local space.

    The pose helpers intentionally work in a stable Blender review scene whose
    coordinates are roughly human height.  The Unity viewmodel prefab is,
    however, instantiated directly below the first-person camera.  Exporting
    those review-scene coordinates unchanged therefore placed every arm more
    than a metre above the player's eyes.  Baking the inverse review-camera
    transform here preserves the approved screen-space composition while making
    the FBX safe to parent at camera-local identity.
    """
    # FBX's Blender-to-Unity conversion maps staged Blender coordinates as
    # (-X, Z, -Y).  A raw camera inverse leaves its local -Z forward axis in
    # Blender Z, which the importer then mistakes for Unity Y.  Re-stage the
    # view coordinates as (-viewX, viewZ, viewY) so the imported result is the
    # expected Unity (viewX, viewY, -viewZ).
    unity_camera_staging = Matrix(
        (
            (-1.0, 0.0, 0.0, 0.0),
            (0.0, 0.0, 1.0, 0.0),
            (0.0, 1.0, 0.0, 0.0),
            (0.0, 0.0, 0.0, 1.0),
        )
    )
    rig.matrix_world = (
        unity_camera_staging @ camera_inverse_world @ rig.matrix_world
    )
    update_scene()


def apply_gameplay_fov_depth_compensation(rig, side, reference_camera):
    """Preserve the approved review framing at the gameplay camera FOV.

    The pose lab uses a narrow vertical review camera while the Unity player
    camera uses a 120 degree horizontal lens (88.507 degrees vertically at
    16:9). Exporting the same camera-local metre coordinates unchanged makes
    every viewmodel too small. Move the complete sampled rig rigidly toward the
    lens; do not scale the mesh or its bones, because that would change
    hand/bottle proportions.

    After FBX conversion staged Blender ``-Y`` becomes Unity ``+Z``.  Therefore
    increasing staged Y decreases the positive Unity camera depth.
    """
    reference_tangent = math.tan(reference_camera.data.angle_y * 0.5)
    gameplay_tangent = math.tan(
        math.radians(UNITY_GAMEPLAY_VERTICAL_FOV_DEGREES) * 0.5
    )
    depth_fraction = reference_tangent / gameplay_tangent
    palm = rig.pose.bones[side_name("DEF-hand", side)]
    palm_staged = rig.matrix_world @ palm.head
    unity_depth = -palm_staged.y
    if unity_depth <= 0.0:
        raise ValueError(
            f"AXIS {side} palm is behind the Unity camera: {unity_depth}"
        )
    staged_y_offset = unity_depth * (1.0 - depth_fraction)
    rig.matrix_world = (
        Matrix.Translation((0.0, staged_y_offset, 0.0)) @ rig.matrix_world
    )
    update_scene()
    return depth_fraction


def author_action(
    rig,
    name,
    length,
    evaluator,
    base_matrix_world,
    camera_inverse_world,
):
    action = bpy.data.actions.new(name)
    rig.animation_data_create()
    rig.animation_data.action = action
    end_frame = int(math.ceil(length * FPS))
    previous_action = None
    for frame in range(0, end_frame + 1):
        time = min(length, frame / FPS)
        # Never evaluate the partially authored action while producing its next
        # absolute frame.  Otherwise Blender composes the previous root sample
        # into the new one and a two-second wave walks tens of metres away.
        rig.animation_data.action = None
        bpy.context.scene.frame_set(frame)
        rig.matrix_world = base_matrix_world.copy()
        reset_pose(rig)
        spec = evaluator(time, length)
        apply_spec(rig, spec)
        apply_camera_local_export_space(rig, camera_inverse_world)
        apply_gameplay_fov_depth_compensation(
            rig,
            spec["side"],
            bpy.context.scene.camera,
        )
        rig.animation_data.action = action
        keyframe_pose(rig, spec["side"], frame)
        keyframe_rig_transform(rig, frame)
        previous_action = action
    rig.animation_data.action = previous_action
    rig.matrix_world = base_matrix_world.copy()
    action.use_fake_user = True
    print(f"AXIS_ACTION={name}|frames=0-{end_frame}|seconds={length:.10f}")
    return action


def author_approved_drink_action(
    rig,
    base_matrix_world,
    camera_inverse_world,
):
    """Bake the user-approved 11 second drink cycle at the final 60 Hz."""
    name = "AxisArms_Drink"
    length = CLIP_LENGTHS[name]
    action = bpy.data.actions.new(name)
    rig.animation_data_create()
    rig.animation_data.action = action
    end_frame = int(round(length * FPS))
    previous_action = None
    for frame in range(0, end_frame + 1):
        time = min(length, frame / FPS)
        # Blender 5 may leave the previously sampled action evaluation cached
        # while the same action is being authored. Detach it before resetting
        # controls so each frame is an absolute sample, not the approved pose
        # composed on top of the previous one.
        rig.animation_data.action = None
        bpy.context.scene.frame_set(frame)
        rig.matrix_world = base_matrix_world.copy()
        reset_pose(rig)
        clear_generated_preview()
        diagnostics = drink_cycle_sample(rig, "R", time / length)
        apply_preview_composition_to_rig(rig, diagnostics)
        apply_camera_local_export_space(rig, camera_inverse_world)
        apply_gameplay_fov_depth_compensation(
            rig,
            "R",
            bpy.context.scene.camera,
        )
        rig.animation_data.action = action
        keyframe_pose(rig, "R", frame)
        keyframe_rig_transform(rig, frame)
        previous_action = action
    clear_generated_preview()
    rig.animation_data.action = previous_action
    rig.matrix_world = base_matrix_world.copy()
    action.use_fake_user = True
    print(f"AXIS_ACTION={name}|frames=0-{end_frame}|seconds={length:.10f}")
    return action


def write_packed_image(image_name, output_path):
    image = bpy.data.images[image_name]
    output_path.parent.mkdir(parents=True, exist_ok=True)
    image.filepath_raw = str(output_path)
    image.file_format = "PNG"
    image.save()
    digest = sha256_file(output_path)
    print(f"AXIS_TEXTURE={image_name}|{output_path}|sha256={digest}")
    return digest


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


fbx_path = Path(os.environ["ARMS_KURWA_FBX"])
texture_root = Path(os.environ["ARMS_KURWA_TEXTURE_DIR"])
fbx_path.parent.mkdir(parents=True, exist_ok=True)
texture_root.mkdir(parents=True, exist_ok=True)

scene = bpy.context.scene
configure_review_scene()
update_scene()
scene.render.fps = FPS
scene.frame_start = 0
scene.frame_end = max(int(math.ceil(value * FPS)) for value in CLIP_LENGTHS.values())

rig = bpy.data.objects["AXIS_Rig"]
mesh = bpy.data.objects["Neutral_Arms"]
rig.rotation_mode = "QUATERNION"
base_matrix_world = rig.matrix_world.copy()
camera_inverse_world = scene.camera.matrix_world.inverted().copy()
reference_vertical_fov_degrees = math.degrees(scene.camera.data.angle_y)
depth_fraction = math.tan(scene.camera.data.angle_y * 0.5) / math.tan(
    math.radians(UNITY_GAMEPLAY_VERTICAL_FOV_DEGREES) * 0.5
)
print(
    "AXIS_FOV_COMPENSATION="
    f"review_vertical={reference_vertical_fov_degrees:.6f}|"
    f"gameplay_vertical={UNITY_GAMEPLAY_VERTICAL_FOV_DEGREES:.6f}|"
    f"depth_fraction={depth_fraction:.9f}"
)
for modifier in mesh.modifiers:
    if modifier.type == "SUBSURF":
        modifier.show_viewport = False
        modifier.show_render = False

for action in list(bpy.data.actions):
    bpy.data.actions.remove(action)

author_approved_drink_action(
    rig,
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_DrinkShort",
    CLIP_LENGTHS["AxisArms_DrinkShort"],
    evaluate_drink_short,
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_DrinkSpray",
    CLIP_LENGTHS["AxisArms_DrinkSpray"],
    lambda time, length: blend_spec(
        RIGHT_READY, RIGHT_SPRAY, smooth_range(time, 0.0, min(0.7, length))
    ),
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_DrinkThrow",
    CLIP_LENGTHS["AxisArms_DrinkThrow"],
    lambda time, length: blend_spec(
        RIGHT_READY, RIGHT_THROW, smooth_range(time, 0.0, length)
    ),
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_Wave",
    CLIP_LENGTHS["AxisArms_Wave"],
    evaluate_wave,
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_MiddleFinger",
    CLIP_LENGTHS["AxisArms_MiddleFinger"],
    evaluate_middle,
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_SmokeIn",
    CLIP_LENGTHS["AxisArms_SmokeIn"],
    evaluate_smoke_in,
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_SmokeLightUp",
    CLIP_LENGTHS["AxisArms_SmokeLightUp"],
    evaluate_smoke_lightup,
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_SmokeOut",
    CLIP_LENGTHS["AxisArms_SmokeOut"],
    evaluate_smoke_out,
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_SmokePutOff",
    CLIP_LENGTHS["AxisArms_SmokePutOff"],
    evaluate_smoke_put_off,
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_SmokeReset",
    CLIP_LENGTHS["AxisArms_SmokeReset"],
    lambda time, length: RIGHT_SMOKE_HIDDEN,
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_PushOn",
    CLIP_LENGTHS["AxisArms_PushOn"],
    evaluate_push_on,
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_PushOff",
    CLIP_LENGTHS["AxisArms_PushOff"],
    evaluate_push_off,
    base_matrix_world,
    camera_inverse_world,
)
author_action(
    rig,
    "AxisArms_ThumbUp",
    CLIP_LENGTHS["AxisArms_ThumbUp"],
    evaluate_thumb_up,
    base_matrix_world,
    camera_inverse_world,
)

used_bones = {
    group.name for group in mesh.vertex_groups if rig.data.bones.get(group.name)
}
for bone in rig.data.bones:
    bone.use_deform = bone.name in used_bones

bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
mesh.select_set(True)
bpy.context.view_layer.objects.active = rig

bpy.ops.export_scene.fbx(
    filepath=str(fbx_path),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    use_space_transform=True,
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=False,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=True,
    armature_nodetype="NULL",
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,
    path_mode="AUTO",
    embed_textures=False,
    use_custom_props=False,
)

texture_hashes = {
    "diffuse": write_packed_image(
        "Diffuse.png", texture_root / "AxisNeutralArms_Diffuse.png"
    ),
    "normal": write_packed_image(
        "Normal.png", texture_root / "AxisNeutralArms_Normal.png"
    ),
    "roughness": write_packed_image(
        "Roughness.png", texture_root / "AxisNeutralArms_Roughness.png"
    ),
}
fbx_hash = sha256_file(fbx_path)
print("===AXIS_UNITY_EXPORT===")
print(
    json.dumps(
        {
            "fbx": str(fbx_path),
            "fbx_sha256": fbx_hash,
            "textures": texture_hashes,
            "source_mesh_vertices": len(mesh.data.vertices),
            "source_mesh_polygons": len(mesh.data.polygons),
            "export_deform_bones": sorted(used_bones),
            "clip_lengths": CLIP_LENGTHS,
        },
        ensure_ascii=False,
        indent=2,
    )
)
print("===END_AXIS_UNITY_EXPORT===")
