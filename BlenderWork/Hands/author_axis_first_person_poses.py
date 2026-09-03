"""Author and render anatomical first-person proof poses on the AXIS arm rig.

This script is intentionally source-safe: it operates on the opened purchased
blend in memory, renders local review images, and never saves over the source.
The same pose helpers are later reused by the deterministic Unity FBX export.
"""

import math
import os
from pathlib import Path

import bmesh
import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Matrix, Vector


RIG_NAME = "AXIS_Rig"
MESH_NAME = "Neutral_Arms"

# The purchased file marks two UI text blocks for auto-run.  They are not
# needed for authoring, and the AXIS panel contains an online updater, so the
# file is always opened with --disable-autoexec.  Enabling expression drivers
# after load is sufficient for Rigify/AXIS constraint switching and does not
# execute either embedded text block.
bpy.context.preferences.filepaths.use_scripts_auto_execute = True


def side_name(base, side):
    return f"{base}.{side}"


def update_scene():
    bpy.context.view_layer.update()
    bpy.context.evaluated_depsgraph_get().update()


def reset_pose(rig):
    for pose_bone in rig.pose.bones:
        pose_bone.matrix_basis.identity()
    for side in ("L", "R"):
        arm_parent = rig.pose.bones.get(side_name("upper_arm_parent", side))
        if arm_parent is not None:
            arm_parent["IK_FK"] = 0.0
            arm_parent["IK_Stretch"] = 0.0
    update_scene()


def rotate_bone_around_head(pose_bone, delta):
    head = pose_bone.head.copy()
    pose_bone.matrix = (
        Matrix.Translation(head)
        @ delta.to_matrix().to_4x4()
        @ Matrix.Translation(-head)
        @ pose_bone.matrix
    )


def aim_bone(pose_bone, desired_direction):
    current = (pose_bone.tail - pose_bone.head).normalized()
    desired = Vector(desired_direction).normalized()
    rotate_bone_around_head(pose_bone, current.rotation_difference(desired))
    update_scene()


def solve_elbow(shoulder, wrist, upper_length, forearm_length, pole):
    shoulder = Vector(shoulder)
    wrist = Vector(wrist)
    span = wrist - shoulder
    distance = span.length
    maximum = upper_length + forearm_length - 0.002
    minimum = abs(upper_length - forearm_length) + 0.002
    distance = min(max(distance, minimum), maximum)
    direction = span.normalized()
    wrist = shoulder + direction * distance
    along = (
        upper_length * upper_length
        - forearm_length * forearm_length
        + distance * distance
    ) / (2.0 * distance)
    height = math.sqrt(max(0.0, upper_length * upper_length - along * along))
    projected_pole = Vector(pole) - direction * Vector(pole).dot(direction)
    if projected_pole.length_squared < 1e-8:
        projected_pole = direction.cross(Vector((0.0, 0.0, 1.0)))
    bend_direction = projected_pole.normalized()
    return shoulder + direction * along + bend_direction * height, wrist


def palm_frame(rig, side):
    hand = rig.pose.bones[side_name("DEF-hand", side)]
    finger_bases = [
        rig.pose.bones[side_name(f"DEF-f_{digit}.01", side)].head.copy()
        for digit in ("index", "middle", "ring", "pinky")
    ]
    forward = sum((point - hand.head for point in finger_bases), Vector()).normalized()
    across = (finger_bases[3] - finger_bases[0]).normalized()
    normal = forward.cross(across).normalized()
    if side == "L":
        normal.negate()
    across = normal.cross(forward).normalized()
    return forward, across, normal


def frame_matrix(forward, normal):
    forward = Vector(forward).normalized()
    normal = Vector(normal) - forward * Vector(normal).dot(forward)
    normal.normalize()
    across = normal.cross(forward).normalized()
    normal = forward.cross(across).normalized()
    return Matrix((across, forward, normal)).transposed()


def orient_hand(rig, side, desired_forward, desired_normal, use_ik=False):
    control_name = "hand_ik" if use_ik else "hand_fk"
    control = rig.pose.bones[side_name(control_name, side)]
    current_forward, _, current_normal = palm_frame(rig, side)
    current = frame_matrix(current_forward, current_normal)
    desired = frame_matrix(desired_forward, desired_normal)
    rotate_bone_around_head(control, (desired @ current.inverted()).to_quaternion())
    update_scene()


def pose_arm(rig, side, wrist, pole, palm_forward, palm_normal):
    upper = rig.pose.bones[side_name("upper_arm_fk", side)]
    forearm = rig.pose.bones[side_name("forearm_fk", side)]
    shoulder = upper.head.copy()
    elbow, reachable_wrist = solve_elbow(
        shoulder,
        wrist,
        upper.length,
        forearm.length,
        pole,
    )
    arm_parent = rig.pose.bones[side_name("upper_arm_parent", side)]
    # AXIS exposes this slider with 0 = IK and 1 = FK.
    arm_parent["IK_FK"] = 0.0
    arm_parent["IK_Stretch"] = 0.0
    arm_parent["pole_vector"] = True
    rig.update_tag(refresh={"OBJECT"})
    bpy.context.scene.frame_set(bpy.context.scene.frame_current)
    hand_control = rig.pose.bones[side_name("hand_ik", side)]
    hand_matrix = hand_control.matrix.copy()
    hand_matrix.translation = reachable_wrist
    hand_control.matrix = hand_matrix
    pole_control = rig.pose.bones[side_name("upper_arm_ik_target", side)]
    bend_direction = (elbow - (shoulder + reachable_wrist) * 0.5).normalized()
    pole_matrix = pole_control.matrix.copy()
    pole_matrix.translation = elbow + bend_direction * 0.35
    pole_control.matrix = pole_matrix
    update_scene()
    orient_hand(
        rig,
        side,
        palm_forward,
        palm_normal,
        use_ik=True,
    )
    return {
        "shoulder": shoulder,
        "elbow": forearm.head.copy(),
        "wrist": rig.pose.bones[side_name("DEF-hand", side)].head.copy(),
        "requested_wrist": Vector(wrist),
        "hand_ik_head": hand_control.head.copy(),
        "hand_ik_matrix_translation": hand_control.matrix.translation.copy(),
        "org_upper_head": rig.pose.bones[
            side_name("ORG-upper_arm", side)
        ].head.copy(),
        "org_forearm_head": rig.pose.bones[
            side_name("ORG-forearm", side)
        ].head.copy(),
        "ik_target": rig.pose.bones[
            side_name("MCH-upper_arm_ik_target", side)
        ].head.copy(),
        "ik_upper_tail": rig.pose.bones[
            side_name("upper_arm_ik", side)
        ].tail.copy(),
        "ik_forearm_tail": rig.pose.bones[
            side_name("MCH-forearm_ik", side)
        ].tail.copy(),
        "org_upper_constraints": [
            (constraint.name, constraint.influence, constraint.mute)
            for constraint in rig.pose.bones[
                side_name("ORG-upper_arm", side)
            ].constraints
        ],
        "ik_fk": arm_parent["IK_FK"],
    }


def pose_arm_fk(
    rig,
    side,
    upper_direction,
    forearm_direction,
    palm_forward,
    palm_normal,
):
    """Pose an arm by its shoulder and elbow hinges without IK singularities."""
    arm_parent = rig.pose.bones[side_name("upper_arm_parent", side)]
    arm_parent["IK_FK"] = 1.0
    arm_parent["IK_Stretch"] = 0.0
    rig.update_tag(refresh={"OBJECT"})
    bpy.context.scene.frame_set(bpy.context.scene.frame_current)

    upper = rig.pose.bones[side_name("upper_arm_fk", side)]
    forearm = rig.pose.bones[side_name("forearm_fk", side)]
    aim_bone(upper, upper_direction)
    aim_bone(forearm, forearm_direction)
    hand_control = rig.pose.bones[side_name("hand_fk", side)]
    # This purchased rig ships an animator convenience clamp on the FK wrist.
    # It prevents matching the already approved grip after an FK elbow swing;
    # the final deform pose is still anatomically authored and baked explicitly.
    for constraint in hand_control.constraints:
        if constraint.type == "LIMIT_ROTATION":
            constraint.mute = True
    orient_hand(
        rig,
        side,
        palm_forward,
        palm_normal,
        use_ik=False,
    )
    update_scene()
    return {
        "shoulder": upper.head.copy(),
        "elbow": forearm.head.copy(),
        "wrist": rig.pose.bones[side_name("DEF-hand", side)].head.copy(),
        "requested_wrist": forearm.tail.copy(),
        "hand_ik_head": Vector((0.0, 0.0, 0.0)),
        "hand_ik_matrix_translation": Vector((0.0, 0.0, 0.0)),
        "org_upper_head": rig.pose.bones[
            side_name("ORG-upper_arm", side)
        ].head.copy(),
        "org_forearm_head": rig.pose.bones[
            side_name("ORG-forearm", side)
        ].head.copy(),
        "ik_target": Vector((0.0, 0.0, 0.0)),
        "ik_upper_tail": upper.tail.copy(),
        "ik_forearm_tail": forearm.tail.copy(),
        "org_upper_constraints": [],
        "ik_fk": arm_parent["IK_FK"],
    }


def set_joint(rig, name, side, x=0.0, y=0.0, z=0.0):
    bone = rig.pose.bones[side_name(name, side)]
    bone.rotation_euler = (
        math.radians(x),
        math.radians(y),
        math.radians(z),
    )


def pose_open_hand(rig, side):
    set_joint(rig, "thumb.01", side, x=-8, y=-10, z=8 if side == "R" else -8)
    set_joint(rig, "thumb.02", side, x=5)
    set_joint(rig, "thumb.03", side, x=4)
    for digit, splay in (
        ("index", -5),
        ("middle", -1),
        ("ring", 3),
        ("pinky", 8),
    ):
        if side == "L":
            splay = -splay
        set_joint(rig, f"f_{digit}.01", side, x=-4, z=splay)
        set_joint(rig, f"f_{digit}.02", side, x=4)
        set_joint(rig, f"f_{digit}.03", side, x=3)
    update_scene()


def pose_cigarette_grip(rig, side):
    """Relaxed right-hand cigarette hold copied from the user photo.

    The cigarette sits between index and middle fingers.  Ring and pinky hang
    loosely instead of closing into a weapon grip; the thumb rests in front of
    the palm without pinching an imaginary cylinder.
    """
    side_sign = 1 if side == "R" else -1
    set_joint(rig, "thumb.01", side, x=-10, y=-8, z=8 * side_sign)
    set_joint(rig, "thumb.02", side, x=18, y=-2)
    set_joint(rig, "thumb.03", side, x=10)
    curls = {
        "index": (20, 28, 10),
        "middle": (32, 42, 16),
        "ring": (50, 64, 24),
        "pinky": (62, 76, 30),
    }
    splays = {"index": -5, "middle": 1, "ring": 5, "pinky": 9}
    for digit, values in curls.items():
        set_joint(
            rig,
            f"f_{digit}.01",
            side,
            x=values[0],
            z=splays[digit] * side_sign,
        )
        set_joint(rig, f"f_{digit}.02", side, x=values[1])
        set_joint(rig, f"f_{digit}.03", side, x=values[2])
    update_scene()


def pose_thumb_up(rig, side):
    """Natural left-hand thumbs-up: tucked fingers and a straight thumb."""
    side_sign = 1 if side == "R" else -1
    set_joint(rig, "thumb.01", side, x=-34, y=-5, z=9 * side_sign)
    set_joint(rig, "thumb.02", side, x=-8, y=1)
    set_joint(rig, "thumb.03", side, x=2)
    curls = {
        "index": (76, 88, 36),
        "middle": (78, 94, 40),
        "ring": (82, 98, 44),
        "pinky": (86, 102, 48),
    }
    splays = {"index": -3, "middle": -1, "ring": 3, "pinky": 7}
    for digit, values in curls.items():
        set_joint(
            rig,
            f"f_{digit}.01",
            side,
            x=values[0],
            z=splays[digit] * side_sign,
        )
        set_joint(rig, f"f_{digit}.02", side, x=values[1])
        set_joint(rig, f"f_{digit}.03", side, x=values[2])
    update_scene()


def pose_bottle_grip(
    rig,
    side,
    thumb_variant="base",
    curl_scale=1.0,
    curl_scales=None,
    finger_curls=None,
):
    side_sign = 1 if side == "R" else -1
    variants = {
        "base": (18, -28, 12 * side_sign, 38, 24),
        "y_flip": (18, 28, 12 * side_sign, 38, 24),
        "z_flip": (18, -28, -12 * side_sign, 38, 24),
        "yz_flip": (18, 28, -12 * side_sign, 38, 24),
        # User photo: the right thumb stays fairly straight and lies across the
        # camera-facing side of the cylinder instead of disappearing behind it.
        "photo": (-8, -10, 8 * side_sign, 12, 8),
        "photo_grip_a": (1, -16, 9 * side_sign, 21, 14),
        "photo_grip_b": (5, -19, 10 * side_sign, 25, 16),
        "photo_grip_c": (9, -22, 11 * side_sign, 29, 18),
        "photo_contact_a": (15, 10, -30 * side_sign, 25, 16),
        "photo_contact_b": (22, 25, -40 * side_sign, 30, 20),
        "photo_contact_c": (28, 35, -50 * side_sign, 34, 22),
        # Surface-fit variants for the corrected bottle placement.  The thumb
        # root opens the web of the hand while the last two phalanges stay on
        # the camera-facing surface instead of cutting through the cylinder.
        "photo_surface_a": (-22.1, -12.0, -30 * side_sign, 25.0, 21.8),
        "photo_surface_b": (-20.6, -8.4, -30 * side_sign, 26.9, 43.1),
        "photo_surface_c": (-14.8, -0.9, -30 * side_sign, 62.5, 47.6),
        "photo_surface_wide": (-27.5, 15.0, -30 * side_sign, 25.0, 22.0),
        "debug_x_pos": (45, -10, 8 * side_sign, 12, 8),
        "debug_x_neg": (-45, -10, 8 * side_sign, 12, 8),
        "debug_y_pos": (-8, 50, 8 * side_sign, 12, 8),
        "debug_y_neg": (-8, -60, 8 * side_sign, 12, 8),
        "debug_z_pos": (-8, -10, 50 * side_sign, 12, 8),
        "debug_z_neg": (-8, -10, -50 * side_sign, 12, 8),
    }
    thumb_x, thumb_y, thumb_z, thumb_middle, thumb_tip = variants[
        thumb_variant
    ]
    set_joint(rig, "thumb.01", side, x=thumb_x, y=thumb_y, z=thumb_z)
    set_joint(rig, "thumb.02", side, x=thumb_middle)
    set_joint(rig, "thumb.03", side, x=thumb_tip)
    base_scale, middle_scale, tip_scale = curl_scales or (
        curl_scale,
        curl_scale,
        curl_scale,
    )
    for digit, base, middle, tip, splay in (
        ("index", 42, 68, 42, -4),
        ("middle", 48, 76, 48, -1),
        ("ring", 54, 82, 52, 2),
        ("pinky", 60, 88, 56, 5),
    ):
        if finger_curls and digit in finger_curls:
            base, middle, tip = finger_curls[digit]
        else:
            base *= base_scale
            middle *= middle_scale
            tip *= tip_scale
        if side == "L":
            splay = -splay
        set_joint(rig, f"f_{digit}.01", side, x=base, z=splay)
        set_joint(rig, f"f_{digit}.02", side, x=middle)
        set_joint(rig, f"f_{digit}.03", side, x=tip)
    update_scene()


def pose_middle_finger(rig, side, thumb_variant="relaxed"):
    side_sign = 1 if side == "R" else -1
    if thumb_variant == "neutral":
        set_joint(rig, "thumb.01", side, x=-8, y=-10, z=side_sign * 8)
        set_joint(rig, "thumb.02", side, x=8)
        set_joint(rig, "thumb.03", side, x=6)
    elif thumb_variant == "relaxed":
        # Keep the thumb on the flank of the fist.  The previous pose folded it
        # across the palm and destroyed the characteristic original silhouette.
        set_joint(rig, "thumb.01", side, x=3, y=-14, z=side_sign * 7)
        set_joint(rig, "thumb.02", side, x=20, y=-4)
        set_joint(rig, "thumb.03", side, x=12)
    elif thumb_variant == "donor":
        set_joint(rig, "thumb.01", side, x=1, y=-13, z=side_sign * 7)
        set_joint(rig, "thumb.02", side, x=18, y=-3)
        set_joint(rig, "thumb.03", side, x=11)
    elif thumb_variant == "clamp":
        set_joint(rig, "thumb.01", side, x=18, y=-24, z=side_sign * 10)
        set_joint(rig, "thumb.02", side, x=44, y=-8)
        set_joint(rig, "thumb.03", side, x=28)
    else:
        raise ValueError(f"Unknown thumb variant: {thumb_variant}")
    curls = {
        # A fist is not three semicircular sausages.  Most closure happens at
        # MCP/PIP; the distal joint only tucks each fingertip into the palm.
        "index": (68, 88, 34),
        "middle": (-3, 2, 2),
        "ring": (74, 94, 38),
        "pinky": (78, 98, 42),
    }
    splays = {"index": -4, "middle": 0, "ring": 3, "pinky": 7}
    for digit, values in curls.items():
        splay = splays[digit] * side_sign
        set_joint(rig, f"f_{digit}.01", side, x=values[0], z=splay)
        set_joint(rig, f"f_{digit}.02", side, x=values[1])
        set_joint(rig, f"f_{digit}.03", side, x=values[2])
    update_scene()


def pose_middle_finger_photo_reference(rig, side):
    """Pose copied from the user's 2026-08-11 left-hand photo reference."""
    side_sign = 1 if side == "R" else -1
    # The thumb stays open but rises diagonally beside the curled index, as in
    # the user's second left-hand reference.  This is deliberately not the
    # sideways "L" silhouette from the first rough pass.
    set_joint(rig, "thumb.01", side, x=14.2, y=9.7, z=side_sign * -28.8)
    set_joint(rig, "thumb.02", side, x=-9.9, y=0.6)
    set_joint(rig, "thumb.03", side, x=0.0)
    curls = {
        # Keep the photographed thumb and extended middle finger, but reuse
        # the validated compact curl from thumbs-up for the other three digits.
        "index": (72, 88, 35),
        "middle": (-2, 3, 2),
        "ring": (91, 42, 0),
        "pinky": (95, 48, 0),
    }
    splays = {"index": -4, "middle": -1, "ring": 3, "pinky": 7}
    for digit, values in curls.items():
        set_joint(
            rig,
            f"f_{digit}.01",
            side,
            x=values[0],
            z=splays[digit] * side_sign,
        )
        set_joint(rig, f"f_{digit}.02", side, x=values[1])
        set_joint(rig, f"f_{digit}.03", side, x=values[2])
    update_scene()


def side_vertex_indices(mesh_object, side):
    opposite = "L" if side == "R" else "R"
    side_groups = {
        group.index
        for group in mesh_object.vertex_groups
        if group.name.endswith(f".{side}")
    }
    opposite_groups = {
        group.index
        for group in mesh_object.vertex_groups
        if group.name.endswith(f".{opposite}")
    }
    keep = set()
    for vertex in mesh_object.data.vertices:
        selected = sum(
            membership.weight
            for membership in vertex.groups
            if membership.group in side_groups
        )
        rejected = sum(
            membership.weight
            for membership in vertex.groups
            if membership.group in opposite_groups
        )
        if selected > rejected and selected > 0.001:
            keep.add(vertex.index)
    return keep


def create_static_side(mesh_object, side):
    for modifier in mesh_object.modifiers:
        if modifier.type == "SUBSURF":
            modifier.show_viewport = False
            modifier.show_render = False
    keep = side_vertex_indices(mesh_object, side)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh_object.evaluated_get(depsgraph)
    static_mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    static_object = bpy.data.objects.new(f"PreviewArm_{side}", static_mesh)
    bpy.context.scene.collection.objects.link(static_object)
    edit_mesh = bmesh.new()
    edit_mesh.from_mesh(static_mesh)
    rejected_vertices = [
        vertex for vertex in edit_mesh.verts if vertex.index not in keep
    ]
    bmesh.ops.delete(edit_mesh, geom=rejected_vertices, context="VERTS")
    edit_mesh.to_mesh(static_mesh)
    edit_mesh.free()
    for material in mesh_object.data.materials:
        static_mesh.materials.append(material)
    return static_object


def add_bottle_proxy(location, direction=(0.0, 0.0, 1.0)):
    # Exact donor mesh bounds: diameter 0.056268 m, height 0.170230 m.  A lathed
    # silhouette makes grip reviews meaningful; the old full-height cylinder
    # looked like a drain pipe and concealed finger contact.
    profile = (
        (0.0235, -0.085115),
        (0.028134, -0.0810),
        (0.028134, 0.0400),
        (0.0260, 0.0480),
        (0.0190, 0.0590),
        (0.0120, 0.0690),
        (0.0110, 0.0810),
        (0.0120, 0.085115),
    )
    segments = 48
    vertices = []
    for radius, z in profile:
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            vertices.append((radius * math.cos(angle), radius * math.sin(angle), z))
    faces = []
    for ring in range(len(profile) - 1):
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            a = ring * segments + segment
            b = ring * segments + next_segment
            c = (ring + 1) * segments + next_segment
            d = (ring + 1) * segments + segment
            faces.append((a, b, c, d))
    faces.append(tuple(reversed(tuple(range(segments)))))
    top = tuple((len(profile) - 1) * segments + index for index in range(segments))
    faces.append(top)
    bottle_mesh = bpy.data.meshes.new("BeerBottleProxyMesh")
    bottle_mesh.from_pydata(vertices, [], faces)
    bottle_mesh.update()
    bottle = bpy.data.objects.new("BeerBottleProxy", bottle_mesh)
    bpy.context.scene.collection.objects.link(bottle)
    bottle.name = "BeerBottleProxy"
    bottle.location = Vector(location)
    bottle.rotation_mode = "QUATERNION"
    bottle.rotation_quaternion = Vector((0.0, 0.0, 1.0)).rotation_difference(
        Vector(direction).normalized()
    )
    material = bpy.data.materials.get("Bottle Proxy") or bpy.data.materials.new(
        "Bottle Proxy"
    )
    material.use_nodes = True
    material.diffuse_color = (0.12, 0.018, 0.004, 1.0)
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = (
            0.12,
            0.018,
            0.004,
            1.0,
        )
        principled.inputs["Roughness"].default_value = 0.28
    bottle.data.materials.append(material)
    return bottle


def add_gripped_bottle_proxy(
    rig,
    side,
    forward,
    normal,
    palm_offset=0.050,
    normal_offset=0.006,
    axial_offset=0.020,
    top_left_tilt_degrees=0.0,
    tilt_local_direction=None,
    world_offset=(0.0, 0.0, 0.0),
    axis_reference=None,
):
    hand = rig.pose.bones[side_name("DEF-hand", side)]
    forward = Vector(forward).normalized()
    normal = Vector(normal).normalized()
    bottle_axis = -normal.cross(forward).normalized()
    if axis_reference is not None:
        if bottle_axis.dot(Vector(axis_reference).normalized()) < 0.0:
            bottle_axis.negate()
    elif bottle_axis.z < 0.0:
        bottle_axis.negate()
    if abs(top_left_tilt_degrees) > 1e-6:
        if tilt_local_direction is None:
            # Review-only fallback: tip the neck toward screen-left.
            tilt_direction = Vector((-1.0, 0.0, 0.0))
            tilt_direction -= bottle_axis * tilt_direction.dot(bottle_axis)
        else:
            # Production path: store the approved tilt in the hand basis so
            # the bottle cannot slide or change angle while moving to the mouth.
            local_forward, local_normal = tilt_local_direction
            tilt_direction = (
                forward * local_forward + normal * local_normal
            )
        tilt_direction.normalize()
        angle = math.radians(top_left_tilt_degrees)
        bottle_axis = (
            bottle_axis * math.cos(angle)
            + tilt_direction * math.sin(angle)
        ).normalized()
    # Put the bottle body against the palm, not out at the fingertips.
    grip = (
        hand.head
        + forward * palm_offset
        + normal * normal_offset
        + Vector(world_offset)
    )
    return add_bottle_proxy(grip + bottle_axis * axial_offset, bottle_axis)


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat(
        "-Z", "Y"
    ).to_euler()


def configure_review_scene():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -3.1
    world = scene.world or bpy.data.worlds.new("Pose Review World")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.025, 0.035, 0.05, 1.0)
    background.inputs["Strength"].default_value = 0.12
    mesh = bpy.data.objects.get(MESH_NAME)
    if mesh is not None:
        for material in mesh.data.materials:
            if material is None or not material.use_nodes:
                continue
            principled = material.node_tree.nodes.get("Principled BSDF")
            if principled is None:
                continue
            if principled.inputs.get("Subsurface Weight") is not None:
                principled.inputs["Subsurface Weight"].default_value = 0.12
            principled.inputs["Roughness"].default_value = 0.62
            if principled.inputs.get("Coat Weight") is not None:
                principled.inputs["Coat Weight"].default_value = 0.0
    for obj in list(scene.objects):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)
    camera_data = bpy.data.cameras.new("First Person Camera")
    camera_data.lens = 38
    camera_data.clip_start = 0.025
    camera = bpy.data.objects.new("First Person Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.location = (0.0, 0.30, 1.56)
    look_at(camera, (0.0, -0.75, 1.31))
    for name, location, energy, size in (
        ("Key", (-0.75, -0.35, 2.15), 70, 1.5),
        ("Fill", (0.65, -0.15, 1.45), 24, 1.25),
        ("Rim", (0.0, 0.25, 1.9), 42, 1.0),
    ):
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        scene.collection.objects.link(light)
        light.location = location
        look_at(light, (0.0, -0.35, 1.25))
    return scene


def clear_generated_preview():
    for obj in list(bpy.data.objects):
        if obj.name.startswith("PreviewArm_") or obj.name.startswith(
            "BeerBottleProxy"
        ):
            bpy.data.objects.remove(obj, do_unlink=True)


def render_pose(output_root, pose_name, side, pose_callback):
    rig = bpy.data.objects[RIG_NAME]
    mesh = bpy.data.objects[MESH_NAME]
    clear_generated_preview()
    reset_pose(rig)
    diagnostics = pose_callback(rig, side)
    update_scene()
    actual_palm_forward, _, actual_palm_normal = palm_frame(rig, side)
    diagnostics["actual_palm_forward"] = actual_palm_forward
    diagnostics["actual_palm_normal"] = actual_palm_normal
    static = create_static_side(mesh, side)
    preview_translation = diagnostics.get("preview_translation")
    if preview_translation is not None:
        static.location += Vector(preview_translation)
    camera = bpy.context.scene.camera
    composition_rotation = diagnostics.get(
        "preview_composition_rotation_degrees", 0.0
    )
    if abs(composition_rotation) > 1e-6:
        pivot = Vector(
            diagnostics.get("preview_composition_pivot", diagnostics["wrist"])
        )
        # `look_at` writes Euler rotation while the camera remains in XYZ mode;
        # reading `rotation_quaternion` here therefore returns a stale identity
        # quaternion.  Derive the real view axis from the evaluated world matrix.
        camera_forward = (
            camera.matrix_world.to_quaternion() @ Vector((0.0, 0.0, -1.0))
        ).normalized()
        rigid_rotation = (
            Matrix.Translation(pivot)
            @ Matrix.Rotation(
                math.radians(composition_rotation), 4, camera_forward
            )
            @ Matrix.Translation(-pivot)
        )
        static.matrix_world = rigid_rotation @ static.matrix_world
        for preview_object in bpy.data.objects:
            if preview_object.name.startswith("BeerBottleProxy"):
                preview_object.matrix_world = (
                    rigid_rotation @ preview_object.matrix_world
                )
    corners = [static.matrix_world @ Vector(corner) for corner in static.bound_box]
    if diagnostics.get("review_camera_location") is not None:
        camera.location = Vector(diagnostics["review_camera_location"])
        look_at(camera, diagnostics["review_camera_target"])
        camera.data.lens = diagnostics.get("review_camera_lens", 38)
    diagnostics["static_bounds"] = {
        "min": Vector(
            tuple(min(corner[axis] for corner in corners) for axis in range(3))
        ),
        "max": Vector(
            tuple(max(corner[axis] for corner in corners) for axis in range(3))
        ),
    }
    diagnostics["wrist_view"] = world_to_camera_view(
        bpy.context.scene,
        camera,
        diagnostics["wrist"],
    )
    bottle = next(
        (
            obj
            for obj in bpy.data.objects
            if obj.name.startswith("BeerBottleProxy")
        ),
        None,
    )
    if bottle is not None:
        bottle_neck = bottle.matrix_world @ Vector((0.0, 0.0, 0.085115))
        diagnostics["bottle_neck"] = bottle_neck
        diagnostics["bottle_neck_view"] = world_to_camera_view(
            bpy.context.scene,
            camera,
            bottle_neck,
        )
    mesh.hide_render = True
    rig.hide_render = True
    path = output_root / f"axis_pose_{pose_name}.png"
    bpy.context.scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    mesh.hide_render = False
    rig.hide_render = False
    print(f"AXIS_POSE={pose_name}|{diagnostics}|{path}")
    bpy.data.objects.remove(static, do_unlink=True)


def drink_ready(rig, side):
    forward = (1.0, 0.0, 0.0)
    normal = (0.0, 1.0, 0.0)
    diagnostics = pose_arm(
        rig,
        side,
        (-0.16, -0.385, 1.30),
        (-1.0, 0.45, -0.8),
        forward,
        normal,
    )
    pose_bottle_grip(rig, side)
    add_gripped_bottle_proxy(rig, side, forward, normal)
    return diagnostics


def drink_ready_hand_debug(rig, side):
    forward = (1.0, 0.0, 0.0)
    normal = (0.0, 1.0, 0.0)
    diagnostics = pose_arm(
        rig,
        side,
        (-0.16, -0.385, 1.30),
        (-1.0, 0.45, -0.8),
        forward,
        normal,
    )
    pose_bottle_grip(rig, side)
    return diagnostics


def drink_ready_dorsal_variant(
    rig,
    side,
    thumb_variant="photo",
    show_bottle=True,
    normal_override=None,
):
    # User reference: camera sees the upper/dorsal side of the grip while the
    # palm faces the bottle.  The +Y draft exposed the palm too strongly; -Y is
    # the required 180-degree axial orientation and keeps the bottle vertical.
    forward = (1.0, 0.0, 0.0)
    normal = normal_override or (0.0, -1.0, 0.0)
    diagnostics = pose_arm(
        rig,
        side,
        (-0.16, -0.385, 1.30),
        (-1.0, 0.45, -0.8),
        forward,
        normal,
    )
    pose_bottle_grip(rig, side, thumb_variant=thumb_variant)
    if show_bottle:
        add_gripped_bottle_proxy(
            rig,
            side,
            forward,
            normal,
            palm_offset=0.055,
            normal_offset=0.025,
        )
    return diagnostics


def drink_ready_dorsal(rig, side):
    return drink_ready_photo_reference(rig, side)


def forearm_aligned_hand_frame(rig, side, wrist, pole, roll_degrees=0.0):
    """Return a straight wrist frame with the palm facing the bottle side."""
    upper = rig.pose.bones[side_name("upper_arm_fk", side)]
    forearm = rig.pose.bones[side_name("forearm_fk", side)]
    elbow, reachable_wrist = solve_elbow(
        upper.head.copy(),
        wrist,
        upper.length,
        forearm.length,
        pole,
    )
    forward = (reachable_wrist - elbow).normalized()
    # Horizontal radial normal: for the right hand this points screen-left,
    # into the bottle.  It is perpendicular to the forearm, so this sets roll
    # without introducing flexion or sideways wrist deviation.
    normal = Vector((-forward.y, forward.x, 0.0)).normalized()
    if normal.x < 0.0:
        normal.negate()
    if abs(roll_degrees) > 1e-6:
        normal = (
            Matrix.Rotation(math.radians(roll_degrees), 3, forward) @ normal
        ).normalized()
    return tuple(forward), tuple(normal)


def drink_ready_photo_reference(
    rig,
    side,
    show_bottle=True,
    thumb_variant="photo_contact_b",
    roll_degrees=0.0,
    curl_scales=(0.62, 0.96, 0.90),
    finger_curls=None,
    bottle_palm_offset=0.055,
    bottle_normal_offset=0.025,
    bottle_axial_offset=0.020,
    bottle_top_left_tilt_degrees=0.0,
    bottle_tilt_local_direction=None,
    bottle_world_offset=(0.0, 0.0, 0.0),
):
    wrist = (-0.16, -0.385, 1.30)
    pole = (-1.0, 0.45, -0.8)
    forward, normal = forearm_aligned_hand_frame(
        rig, side, wrist, pole, roll_degrees=roll_degrees
    )
    diagnostics = pose_arm(rig, side, wrist, pole, forward, normal)
    pose_bottle_grip(
        rig,
        side,
        thumb_variant=thumb_variant,
        curl_scales=curl_scales,
        finger_curls=finger_curls,
    )
    if show_bottle:
        add_gripped_bottle_proxy(
            rig,
            side,
            forward,
            normal,
            palm_offset=bottle_palm_offset,
            normal_offset=bottle_normal_offset,
            axial_offset=bottle_axial_offset,
            top_left_tilt_degrees=bottle_top_left_tilt_degrees,
            tilt_local_direction=bottle_tilt_local_direction,
            world_offset=bottle_world_offset,
        )
    diagnostics["grip_forward"] = Vector(forward)
    diagnostics["grip_normal"] = Vector(normal)
    return diagnostics


def drink_ready_locked_composition(rig, side, rotation_degrees=-10.0):
    """User-approved grip and bottle placement with a rigid view-space turn."""
    diagnostics = drink_ready_photo_reference(
        rig,
        side,
        thumb_variant="photo_surface_wide",
        finger_curls={
            "index": (36, 78, 33),
            "middle": (38, 82, 32),
            "ring": (34, 72, 26),
            "pinky": (20, 48, 18),
        },
        # Equivalent to the approved 0.065/0.015 offsets plus the former
        # (+0.01, +0.01, 0) world correction, resolved into the hand basis.
        bottle_palm_offset=0.0593870527,
        bottle_normal_offset=0.0279216045,
        bottle_axial_offset=0.0212357036,
        bottle_top_left_tilt_degrees=4.0,
        bottle_tilt_local_direction=(-0.3513697, -0.9362368),
    )
    diagnostics["preview_composition_rotation_degrees"] = rotation_degrees
    diagnostics["preview_composition_pivot"] = diagnostics["wrist"]
    return diagnostics


def drink_ready_fk_validation(rig, side):
    """Rebuild the approved ready pose with FK before authoring the elbow lift."""
    upper = rig.pose.bones[side_name("upper_arm_fk", side)]
    forearm = rig.pose.bones[side_name("forearm_fk", side)]
    ready_wrist = Vector((-0.16, -0.385, 1.30))
    ready_pole = Vector((-1.0, 0.45, -0.8))
    ready_elbow, reachable_wrist = solve_elbow(
        upper.head.copy(),
        ready_wrist,
        upper.length,
        forearm.length,
        ready_pole,
    )
    ready_forward, ready_normal = forearm_aligned_hand_frame(
        rig, side, ready_wrist, ready_pole, roll_degrees=0.0
    )
    diagnostics = pose_arm_fk(
        rig,
        side,
        ready_elbow - upper.head,
        reachable_wrist - ready_elbow,
        ready_forward,
        ready_normal,
    )
    pose_bottle_grip(
        rig,
        side,
        thumb_variant="photo_surface_wide",
        finger_curls={
            "index": (36, 78, 33),
            "middle": (38, 82, 32),
            "ring": (34, 72, 26),
            "pinky": (20, 48, 18),
        },
    )
    add_gripped_bottle_proxy(
        rig,
        side,
        ready_forward,
        ready_normal,
        palm_offset=0.0593870527,
        normal_offset=0.0279216045,
        axial_offset=0.0212357036,
        top_left_tilt_degrees=4.0,
        tilt_local_direction=(-0.3513697, -0.9362368),
    )
    diagnostics["grip_forward"] = Vector(ready_forward)
    diagnostics["grip_normal"] = Vector(ready_normal)
    diagnostics["approved_elbow_target"] = ready_elbow
    diagnostics["approved_wrist_target"] = reachable_wrist
    diagnostics["preview_composition_rotation_degrees"] = 10.0
    diagnostics["preview_composition_pivot"] = diagnostics["wrist"]
    return diagnostics


def drink_mouth_elbow_fk(
    rig,
    side,
    elbow_swing_fraction=0.55,
    wrist_bend_degrees=6.0,
    mouth_forward_distance=0.12,
    mouth_down_offset=0.08,
    mouth_screen_x_offset=0.0,
):
    """Fold the approved forearm toward the face around a fixed elbow hinge."""
    upper = rig.pose.bones[side_name("upper_arm_fk", side)]
    forearm = rig.pose.bones[side_name("forearm_fk", side)]
    ready_wrist = Vector((-0.16, -0.385, 1.30))
    ready_pole = Vector((-1.0, 0.45, -0.8))
    ready_elbow, reachable_wrist = solve_elbow(
        upper.head.copy(),
        ready_wrist,
        upper.length,
        forearm.length,
        ready_pole,
    )
    ready_forward, ready_normal = forearm_aligned_hand_frame(
        rig, side, ready_wrist, ready_pole, roll_degrees=0.0
    )
    ready_forward = Vector(ready_forward).normalized()
    ready_normal = Vector(ready_normal).normalized()

    camera = bpy.context.scene.camera
    camera_rotation = camera.matrix_world.to_quaternion()
    camera_forward = (
        camera_rotation @ Vector((0.0, 0.0, -1.0))
    ).normalized()
    camera_down = (
        camera_rotation @ Vector((0.0, -1.0, 0.0))
    ).normalized()
    camera_right = (
        camera_rotation @ Vector((1.0, 0.0, 0.0))
    ).normalized()
    mouth_target = (
        camera.location
        + camera_forward * mouth_forward_distance
        + camera_down * mouth_down_offset
        + camera_right * mouth_screen_x_offset
    )

    target_forearm = (mouth_target - ready_elbow).normalized()
    elbow_swing = ready_forward.rotation_difference(target_forearm)
    partial_elbow_swing = Matrix.Rotation(
        elbow_swing.angle * elbow_swing_fraction,
        3,
        elbow_swing.axis,
    )
    forearm_direction = (
        partial_elbow_swing @ ready_forward
    ).normalized()
    wrist = ready_elbow + forearm_direction * forearm.length

    # Carry the approved palm/bottle frame rigidly with the elbow first.
    forward = forearm_direction.copy()
    normal = (partial_elbow_swing @ ready_normal).normalized()

    # Then add only a small wrist correction: tip the bottle neck toward the
    # mouth target while leaving the finger packing and bottle offsets locked.
    bottle_axis = -normal.cross(forward).normalized()
    if bottle_axis.z < 0.0:
        bottle_axis.negate()
    approved_tilt_direction = (
        forward * -0.3513697 + normal * -0.9362368
    ).normalized()
    bottle_axis = (
        bottle_axis * math.cos(math.radians(4.0))
        + approved_tilt_direction * math.sin(math.radians(4.0))
    ).normalized()
    bottle_center = (
        wrist
        + forward * 0.0593870527
        + normal * 0.0279216045
        + bottle_axis * 0.0212357036
    )
    desired_bottle_axis = (mouth_target - bottle_center).normalized()
    wrist_axis = bottle_axis.cross(desired_bottle_axis)
    if wrist_axis.length_squared > 1e-8 and wrist_bend_degrees > 1e-6:
        wrist_angle = min(
            math.radians(wrist_bend_degrees),
            bottle_axis.angle(desired_bottle_axis),
        )
        wrist_rotation = Matrix.Rotation(
            wrist_angle,
            3,
            wrist_axis.normalized(),
        )
        forward = (wrist_rotation @ forward).normalized()
        normal = (wrist_rotation @ normal).normalized()

    diagnostics = pose_arm_fk(
        rig,
        side,
        ready_elbow - upper.head,
        forearm_direction,
        forward,
        normal,
    )
    pose_bottle_grip(
        rig,
        side,
        thumb_variant="photo_surface_wide",
        finger_curls={
            "index": (36, 78, 33),
            "middle": (38, 82, 32),
            "ring": (34, 72, 26),
            "pinky": (20, 48, 18),
        },
    )
    add_gripped_bottle_proxy(
        rig,
        side,
        forward,
        normal,
        palm_offset=0.0593870527,
        normal_offset=0.0279216045,
        axial_offset=0.0212357036,
        top_left_tilt_degrees=4.0,
        tilt_local_direction=(-0.3513697, -0.9362368),
    )
    diagnostics["grip_forward"] = forward
    diagnostics["grip_normal"] = normal
    diagnostics["approved_elbow_target"] = ready_elbow
    diagnostics["approved_wrist_target"] = reachable_wrist
    diagnostics["mouth_target"] = mouth_target
    diagnostics["elbow_swing_fraction"] = elbow_swing_fraction
    diagnostics["elbow_swing_degrees"] = math.degrees(
        elbow_swing.angle * elbow_swing_fraction
    )
    diagnostics["wrist_bend_degrees"] = wrist_bend_degrees
    diagnostics["preview_composition_rotation_degrees"] = 10.0
    diagnostics["preview_composition_pivot"] = diagnostics["wrist"]
    return diagnostics


def drink_mouth_bottle_pull_fk(
    rig,
    side,
    pull_distance=0.15,
    wrist_bend_degrees=6.0,
    pull_screen_down_ratio=0.12,
):
    """Pull the approved bottle toward the player, with the elbow following."""
    upper = rig.pose.bones[side_name("upper_arm_fk", side)]
    forearm = rig.pose.bones[side_name("forearm_fk", side)]
    ready_wrist = Vector((-0.16, -0.385, 1.30))
    ready_pole = Vector((-1.0, 0.45, -0.8))
    ready_elbow, reachable_wrist = solve_elbow(
        upper.head.copy(),
        ready_wrist,
        upper.length,
        forearm.length,
        ready_pole,
    )
    ready_forward, ready_normal = forearm_aligned_hand_frame(
        rig, side, ready_wrist, ready_pole, roll_degrees=0.0
    )
    ready_forward = Vector(ready_forward).normalized()
    ready_normal = Vector(ready_normal).normalized()

    camera = bpy.context.scene.camera
    camera_rotation = camera.matrix_world.to_quaternion()
    camera_forward = (
        camera_rotation @ Vector((0.0, 0.0, -1.0))
    ).normalized()
    camera_down = (
        camera_rotation @ Vector((0.0, -1.0, 0.0))
    ).normalized()
    ready_bottle_axis = -ready_normal.cross(ready_forward).normalized()
    if ready_bottle_axis.z < 0.0:
        ready_bottle_axis.negate()
    ready_tilt_direction = (
        ready_forward * -0.3513697 + ready_normal * -0.9362368
    ).normalized()
    ready_bottle_axis = (
        ready_bottle_axis * math.cos(math.radians(4.0))
        + ready_tilt_direction * math.sin(math.radians(4.0))
    ).normalized()
    ready_bottle_center = (
        ready_wrist
        + ready_forward * 0.0593870527
        + ready_normal * 0.0279216045
        + ready_bottle_axis * 0.0212357036
    )
    ready_bottle_neck = ready_bottle_center + ready_bottle_axis * 0.085115

    # Pull parallel to the player's view ray instead of converging on the lens
    # centre.  The latter caused a large sideways screen-space sweep; this keeps
    # the bottle on its approved side while it grows toward the player.
    bottle_pull_direction = (
        -camera_forward + camera_down * pull_screen_down_ratio
    ).normalized()
    mouth_target = ready_bottle_neck + bottle_pull_direction
    translated_wrist = ready_wrist + bottle_pull_direction * pull_distance
    forearm_direction = (translated_wrist - ready_elbow).normalized()
    wrist = ready_elbow + forearm_direction * forearm.length

    elbow_swing = ready_forward.rotation_difference(forearm_direction)
    elbow_rotation = Matrix.Rotation(
        elbow_swing.angle,
        3,
        elbow_swing.axis,
    )
    forward = forearm_direction.copy()
    normal = (elbow_rotation @ ready_normal).normalized()

    bottle_axis = -normal.cross(forward).normalized()
    if bottle_axis.z < 0.0:
        bottle_axis.negate()
    tilt_direction = (
        forward * -0.3513697 + normal * -0.9362368
    ).normalized()
    bottle_axis = (
        bottle_axis * math.cos(math.radians(4.0))
        + tilt_direction * math.sin(math.radians(4.0))
    ).normalized()
    bottle_center = (
        wrist
        + forward * 0.0593870527
        + normal * 0.0279216045
        + bottle_axis * 0.0212357036
    )
    desired_bottle_axis = bottle_pull_direction
    wrist_axis = bottle_axis.cross(desired_bottle_axis)
    if wrist_axis.length_squared > 1e-8 and wrist_bend_degrees > 1e-6:
        wrist_angle = min(
            math.radians(wrist_bend_degrees),
            bottle_axis.angle(desired_bottle_axis),
        )
        wrist_rotation = Matrix.Rotation(
            wrist_angle,
            3,
            wrist_axis.normalized(),
        )
        forward = (wrist_rotation @ forward).normalized()
        normal = (wrist_rotation @ normal).normalized()

    diagnostics = pose_arm_fk(
        rig,
        side,
        ready_elbow - upper.head,
        forearm_direction,
        forward,
        normal,
    )
    pose_bottle_grip(
        rig,
        side,
        thumb_variant="photo_surface_wide",
        finger_curls={
            "index": (36, 78, 33),
            "middle": (38, 82, 32),
            "ring": (34, 72, 26),
            "pinky": (20, 48, 18),
        },
    )
    add_gripped_bottle_proxy(
        rig,
        side,
        forward,
        normal,
        palm_offset=0.0593870527,
        normal_offset=0.0279216045,
        axial_offset=0.0212357036,
        top_left_tilt_degrees=4.0,
        tilt_local_direction=(-0.3513697, -0.9362368),
    )
    diagnostics["grip_forward"] = forward
    diagnostics["grip_normal"] = normal
    diagnostics["approved_elbow_target"] = ready_elbow
    diagnostics["approved_wrist_target"] = reachable_wrist
    diagnostics["ready_bottle_neck"] = ready_bottle_neck
    diagnostics["mouth_target"] = mouth_target
    diagnostics["bottle_pull_direction"] = bottle_pull_direction
    diagnostics["pull_distance"] = pull_distance
    diagnostics["elbow_swing_degrees"] = math.degrees(elbow_swing.angle)
    diagnostics["wrist_bend_degrees"] = wrist_bend_degrees
    diagnostics["preview_composition_rotation_degrees"] = 10.0
    diagnostics["preview_composition_pivot"] = diagnostics["wrist"]
    return diagnostics


def drink_mouth_bottle_lead_ik(
    rig,
    side,
    pull_distance=0.14,
    bottle_aim_fraction=0.55,
    mouth_forward_distance=0.04,
    mouth_down_offset=0.08,
    neck_target_fraction=None,
    grip_relax_fraction=0.0,
    bottle_normal_clearance=0.0,
    composition_rotation_degrees=10.0,
    sip_bottom_lift_degrees=0.0,
    neck_arc_down_offset=0.0,
):
    """Drive the drink lift from the bottle, then let the arm follow by IK.

    The previous fixed-elbow experiment projected a translated wrist back onto
    the forearm sphere.  That turned a depth pull into a broad screen-space arm
    sweep.  Here the bottle centre moves directly toward the player, its neck
    turns toward a point just below the camera, and the approved wrist/grip is
    reconstructed from that target bottle transform.
    """
    ready_wrist = Vector((-0.16, -0.385, 1.30))
    ready_pole = Vector((-1.0, 0.45, -0.8))
    ready_forward, ready_normal = forearm_aligned_hand_frame(
        rig, side, ready_wrist, ready_pole, roll_degrees=0.0
    )
    ready_forward = Vector(ready_forward).normalized()
    ready_normal = Vector(ready_normal).normalized()

    palm_offset = 0.0593870527
    normal_offset = 0.0279216045
    axial_offset = 0.0212357036
    tilt_radians = math.radians(4.0)
    tilt_local_forward = -0.3513697
    tilt_local_normal = -0.9362368

    ready_bottle_axis = -ready_normal.cross(ready_forward).normalized()
    if ready_bottle_axis.z < 0.0:
        ready_bottle_axis.negate()
    ready_tilt_direction = (
        ready_forward * tilt_local_forward
        + ready_normal * tilt_local_normal
    ).normalized()
    ready_bottle_axis = (
        ready_bottle_axis * math.cos(tilt_radians)
        + ready_tilt_direction * math.sin(tilt_radians)
    ).normalized()
    ready_bottle_center = (
        ready_wrist
        + ready_forward * palm_offset
        + ready_normal * normal_offset
        + ready_bottle_axis * axial_offset
    )

    camera = bpy.context.scene.camera
    camera_rotation = camera.matrix_world.to_quaternion()
    camera_forward = (
        camera_rotation @ Vector((0.0, 0.0, -1.0))
    ).normalized()
    camera_down = (
        camera_rotation @ Vector((0.0, -1.0, 0.0))
    ).normalized()
    preview_rotation = Matrix.Rotation(
        math.radians(composition_rotation_degrees), 3, camera_forward
    )
    inverse_preview_rotation = preview_rotation.inverted()

    # Work in the final approved (+10 degree) review composition.  This avoids
    # the old sign error where the post-pose composition turn sent the neck
    # outward over the shoulder even though the unrendered vector was correct.
    ready_forward_view = (preview_rotation @ ready_forward).normalized()
    ready_normal_view = (preview_rotation @ ready_normal).normalized()
    ready_bottle_axis_view = (
        preview_rotation @ ready_bottle_axis
    ).normalized()
    ready_bottle_center_view = (
        ready_wrist
        + ready_forward_view * palm_offset
        + ready_normal_view * normal_offset
        + ready_bottle_axis_view * axial_offset
    )
    ready_bottle_neck_view = (
        ready_bottle_center_view + ready_bottle_axis_view * 0.085115
    )
    mouth_target = (
        camera.location
        + camera_forward * mouth_forward_distance
        + camera_down * mouth_down_offset
    )
    pull_direction = (mouth_target - ready_bottle_neck_view).normalized()
    if neck_target_fraction is None:
        target_bottle_neck_view = (
            ready_bottle_neck_view + pull_direction * pull_distance
        )
    else:
        target_bottle_neck_view = ready_bottle_neck_view.lerp(
            mouth_target,
            neck_target_fraction,
        )
    target_bottle_neck_view += camera_down * neck_arc_down_offset
    bottle_turn = ready_bottle_axis_view.rotation_difference(pull_direction)
    bottle_rotation_view = Matrix.Rotation(
        bottle_turn.angle * bottle_aim_fraction,
        3,
        bottle_turn.axis,
    )
    forward_view = (
        bottle_rotation_view @ ready_forward_view
    ).normalized()
    normal_view = (
        bottle_rotation_view @ ready_normal_view
    ).normalized()
    target_bottle_axis_view = (
        bottle_rotation_view @ ready_bottle_axis_view
    ).normalized()

    # During the swallow the neck stays planted at the mouth while the player
    # gradually raises the bottom.  Rotate the complete hand/bottle frame toward
    # screen-down around the neck: because the bottle axis points from its body
    # to the neck, this moves the body upward without sliding the neck sideways.
    actual_sip_bottom_lift_degrees = 0.0
    if abs(sip_bottom_lift_degrees) > 1e-6:
        sip_lift_axis = target_bottle_axis_view.cross(camera_down)
        if sip_lift_axis.length_squared > 1e-8:
            sip_lift_radians = min(
                math.radians(abs(sip_bottom_lift_degrees)),
                target_bottle_axis_view.angle(camera_down),
            )
            if sip_bottom_lift_degrees < 0.0:
                sip_lift_radians = -sip_lift_radians
            sip_lift_rotation_view = Matrix.Rotation(
                sip_lift_radians,
                3,
                sip_lift_axis.normalized(),
            )
            forward_view = (
                sip_lift_rotation_view @ forward_view
            ).normalized()
            normal_view = (
                sip_lift_rotation_view @ normal_view
            ).normalized()
            target_bottle_axis_view = (
                sip_lift_rotation_view @ target_bottle_axis_view
            ).normalized()
            actual_sip_bottom_lift_degrees = math.degrees(
                sip_lift_radians
            )
    target_bottle_center_view = (
        target_bottle_neck_view - target_bottle_axis_view * 0.085115
    )

    # Invert the approved bottle-to-wrist offsets.  This makes the bottle the
    # actual end effector instead of guessing a wrist path and hoping the prop
    # lands in the right place afterward.
    target_normal_offset = normal_offset + bottle_normal_clearance
    target_wrist = (
        target_bottle_center_view
        - forward_view * palm_offset
        - normal_view * target_normal_offset
        - target_bottle_axis_view * axial_offset
    )
    # render_pose applies the approved composition turn after baking the mesh,
    # so pose the wrist frame in its inverse space here.
    forward = (inverse_preview_rotation @ forward_view).normalized()
    normal = (inverse_preview_rotation @ normal_view).normalized()
    diagnostics = pose_arm(
        rig,
        side,
        target_wrist,
        ready_pole,
        forward,
        normal,
    )
    firm_finger_curls = {
        "index": (36, 78, 33),
        "middle": (38, 82, 32),
        "ring": (34, 72, 26),
        "pinky": (20, 48, 18),
    }
    relaxed_finger_curls = {
        "index": (32, 70, 27),
        "middle": (34, 74, 27),
        "ring": (31, 66, 22),
        "pinky": (18, 44, 15),
    }
    finger_curls = {
        digit: tuple(
            firm + (relaxed - firm) * grip_relax_fraction
            for firm, relaxed in zip(
                firm_finger_curls[digit],
                relaxed_finger_curls[digit],
            )
        )
        for digit in firm_finger_curls
    }
    pose_bottle_grip(
        rig,
        side,
        thumb_variant="photo_surface_wide",
        finger_curls=finger_curls,
    )
    add_gripped_bottle_proxy(
        rig,
        side,
        forward,
        normal,
        palm_offset=palm_offset,
        normal_offset=target_normal_offset,
        axial_offset=axial_offset,
        top_left_tilt_degrees=4.0,
        tilt_local_direction=(
            tilt_local_forward,
            tilt_local_normal,
        ),
        axis_reference=(
            inverse_preview_rotation @ target_bottle_axis_view
        ).normalized(),
    )
    diagnostics["grip_forward"] = forward
    diagnostics["grip_normal"] = normal
    diagnostics["ready_bottle_center"] = ready_bottle_center
    diagnostics["ready_bottle_neck_view_target"] = ready_bottle_neck_view
    diagnostics["target_bottle_center_view"] = target_bottle_center_view
    diagnostics["target_bottle_neck_view_target"] = target_bottle_neck_view
    diagnostics["target_bottle_axis_view"] = target_bottle_axis_view
    diagnostics["mouth_target"] = mouth_target
    diagnostics["pull_direction"] = pull_direction
    diagnostics["pull_distance"] = (
        target_bottle_neck_view - ready_bottle_neck_view
    ).length
    diagnostics["neck_target_fraction"] = neck_target_fraction
    diagnostics["bottle_aim_fraction"] = bottle_aim_fraction
    diagnostics["grip_relax_fraction"] = grip_relax_fraction
    diagnostics["bottle_normal_clearance"] = bottle_normal_clearance
    diagnostics["sip_bottom_lift_degrees"] = (
        actual_sip_bottom_lift_degrees
    )
    diagnostics["neck_arc_down_offset"] = neck_arc_down_offset
    diagnostics["bottle_turn_degrees"] = math.degrees(
        bottle_turn.angle * bottle_aim_fraction
    )
    diagnostics["preview_composition_rotation_degrees"] = (
        composition_rotation_degrees
    )
    diagnostics["preview_composition_pivot"] = diagnostics["wrist"]
    return diagnostics


def smoothstep01(value):
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def smootherstep01(value):
    value = max(0.0, min(1.0, value))
    return value * value * value * (
        value * (value * 6.0 - 15.0) + 10.0
    )


DRINK_CYCLE_DURATION_SECONDS = 11.0


def cumulative_smooth_step(time_seconds, start_seconds, duration_seconds):
    """Persistent 0 -> 1 step with zero velocity at both ends."""
    return smootherstep01(
        (time_seconds - start_seconds) / duration_seconds
    )


def drink_cycle_sample(rig, side, normalized_time):
    """Sample the approved ready -> swallow -> ready presentation cycle.

    The donor timing keeps a deliberate pause before the lift and a longer
    drinking hold.  Only the bottle-led interpolation is sampled here; the two
    approved endpoint poses and their grip measurements remain unchanged.
    """
    time = max(0.0, min(1.0, normalized_time))
    time_seconds = time * DRINK_CYCLE_DURATION_SECONDS
    lift_start = 0.08
    swallow_start = 0.73
    return_start = 9.70
    return_end = 10.60
    if time_seconds < lift_start:
        neck_weight = 0.0
        aim_weight = 0.0
        composition_weight = 0.0
        grip_weight = 0.0
        phase = "ready"
    elif time_seconds < swallow_start:
        lift_progress = (
            (time_seconds - lift_start) / (swallow_start - lift_start)
        )
        # Stagger the channels so the prop starts the action, the wrist follows,
        # and the grip only loosens once the bottle is already near the face.
        neck_weight = smootherstep01(lift_progress)
        aim_weight = smootherstep01((lift_progress - 0.08) / 0.92)
        composition_weight = smootherstep01(
            (lift_progress - 0.15) / 0.85
        )
        grip_weight = smootherstep01((lift_progress - 0.38) / 0.62)
        phase = "lift"
    elif time_seconds < return_start:
        neck_weight = 1.0
        aim_weight = 1.0
        composition_weight = 1.0
        grip_weight = 1.0
        phase = "swallow"
    elif time_seconds < return_end:
        return_progress = (
            (time_seconds - return_start) / (return_end - return_start)
        )
        neck_weight = 1.0 - smootherstep01(return_progress)
        # The bottle leaves the mouth first; its wrist orientation and relaxed
        # fingers settle a fraction later instead of all snapping together.
        aim_weight = 1.0 - smootherstep01(
            (return_progress - 0.05) / 0.95
        )
        composition_weight = 1.0 - smootherstep01(
            (return_progress - 0.10) / 0.90
        )
        grip_weight = 1.0 - smootherstep01(
            (return_progress - 0.18) / 0.70
        )
        phase = "return"
    else:
        neck_weight = 0.0
        aim_weight = 0.0
        composition_weight = 0.0
        grip_weight = 0.0
        phase = "ready"

    # The neck follows a shallow downward arc on the way in/out rather than a
    # ruler-straight line.  The offset is exactly zero at both approved poses.
    neck_arc_down_offset = (
        math.sin(math.pi * neck_weight) * 0.012
        if 0.0 < neck_weight < 1.0
        else 0.0
    )

    # Drink at the accepted mouth pose first, then raise the bottle bottom in
    # four small *persistent* steps, about two seconds apart.  The first step is
    # the requested initial top-up and the following three finish the drink.
    # Unlike the discarded pulse version, the bottle never rocks back down
    # between sips.
    sip_steps = (
        (2.20, 0.32, 1.8),
        (4.20, 0.32, 1.7),
        (6.20, 0.32, 1.5),
        (8.20, 0.32, 1.3),
    )
    accumulated_sip_lift_degrees = sum(
        degrees
        * cumulative_smooth_step(
            time_seconds,
            start_seconds,
            duration_seconds,
        )
        for start_seconds, duration_seconds, degrees in sip_steps
    )
    if time_seconds < return_start:
        sip_bottom_lift_degrees = accumulated_sip_lift_degrees
    elif time_seconds < return_end:
        sip_bottom_lift_degrees = (
            accumulated_sip_lift_degrees * neck_weight
        )
    else:
        sip_bottom_lift_degrees = 0.0

    diagnostics = drink_mouth_bottle_lead_ik(
        rig,
        side,
        bottle_aim_fraction=0.70 * aim_weight,
        mouth_forward_distance=0.34,
        mouth_down_offset=0.10,
        neck_target_fraction=neck_weight,
        grip_relax_fraction=0.65 * grip_weight,
        bottle_normal_clearance=0.004 * grip_weight,
        # The approved holding pose is the straighter -10 degree composition;
        # the accepted swallow pose is +10 degrees.  Turn the complete
        # composition gradually while the bottle rises instead of starting
        # from the discarded +10 degree FK validation pose.
        composition_rotation_degrees=-10.0 + 20.0 * composition_weight,
        sip_bottom_lift_degrees=sip_bottom_lift_degrees,
        neck_arc_down_offset=neck_arc_down_offset,
    )
    diagnostics["drink_cycle_normalized_time"] = time
    diagnostics["drink_cycle_time_seconds"] = time_seconds
    diagnostics["drink_cycle_phase"] = phase
    diagnostics["drink_cycle_swallow_weight"] = neck_weight
    diagnostics["drink_cycle_neck_weight"] = neck_weight
    diagnostics["drink_cycle_aim_weight"] = aim_weight
    diagnostics["drink_cycle_composition_weight"] = composition_weight
    diagnostics["drink_cycle_grip_weight"] = grip_weight
    diagnostics["drink_cycle_accumulated_sip_lift_degrees"] = (
        accumulated_sip_lift_degrees
    )
    return diagnostics


def drink_mouth_locked_variant(
    rig,
    side,
    roll_degrees=0.0,
    hand_pitch_degrees=0.0,
    hand_screen_rotation_degrees=0.0,
    wrist=(-0.07, -0.21, 1.40),
    pole=(-1.0, 0.55, -0.5),
    composition_rotation_degrees=10.0,
):
    """Move the approved rigid grip toward the mouth without repacking fingers."""
    forward, normal = forearm_aligned_hand_frame(
        rig, side, wrist, pole, roll_degrees=roll_degrees
    )
    if abs(hand_pitch_degrees) > 1e-6:
        forward_vector = Vector(forward).normalized()
        normal_vector = Vector(normal).normalized()
        hand_side = forward_vector.cross(normal_vector).normalized()
        hand_pitch = Matrix.Rotation(
            math.radians(hand_pitch_degrees), 3, hand_side
        )
        forward = tuple((hand_pitch @ forward_vector).normalized())
        normal = tuple((hand_pitch @ normal_vector).normalized())
    if abs(hand_screen_rotation_degrees) > 1e-6:
        camera = bpy.context.scene.camera
        camera_forward = (
            camera.matrix_world.to_quaternion() @ Vector((0.0, 0.0, -1.0))
        ).normalized()
        screen_rotation = Matrix.Rotation(
            math.radians(hand_screen_rotation_degrees), 3, camera_forward
        )
        forward = tuple(
            (screen_rotation @ Vector(forward)).normalized()
        )
        normal = tuple((screen_rotation @ Vector(normal)).normalized())
    diagnostics = pose_arm(rig, side, wrist, pole, forward, normal)
    pose_bottle_grip(
        rig,
        side,
        thumb_variant="photo_surface_wide",
        finger_curls={
            "index": (36, 78, 33),
            "middle": (38, 82, 32),
            "ring": (34, 72, 26),
            "pinky": (20, 48, 18),
        },
    )
    add_gripped_bottle_proxy(
        rig,
        side,
        forward,
        normal,
        palm_offset=0.0593870527,
        normal_offset=0.0279216045,
        axial_offset=0.0212357036,
        top_left_tilt_degrees=4.0,
        tilt_local_direction=(-0.3513697, -0.9362368),
    )
    diagnostics["grip_forward"] = Vector(forward)
    diagnostics["grip_normal"] = Vector(normal)
    diagnostics["preview_composition_rotation_degrees"] = (
        composition_rotation_degrees
    )
    diagnostics["preview_composition_pivot"] = diagnostics["wrist"]
    return diagnostics


def drink_mouth_toward_camera(
    rig,
    side,
    wrist_bend_fraction=0.5,
    camera_low_offset=0.10,
):
    """Swing the approved ready grip from its elbow toward the player's mouth."""
    upper = rig.pose.bones[side_name("upper_arm_fk", side)]
    forearm = rig.pose.bones[side_name("forearm_fk", side)]

    ready_wrist = Vector((-0.16, -0.385, 1.30))
    ready_pole = Vector((-1.0, 0.45, -0.8))
    ready_elbow, _ = solve_elbow(
        upper.head.copy(),
        ready_wrist,
        upper.length,
        forearm.length,
        ready_pole,
    )
    ready_forward, ready_normal = forearm_aligned_hand_frame(
        rig, side, ready_wrist, ready_pole, roll_degrees=0.0
    )
    ready_forward = Vector(ready_forward).normalized()
    ready_normal = Vector(ready_normal).normalized()

    camera = bpy.context.scene.camera
    forearm_aim = camera.location + Vector((0.0, 0.0, -camera_low_offset))
    forearm_direction = (forearm_aim - ready_elbow).normalized()
    wrist = ready_elbow + forearm_direction * forearm.length

    # Swing the whole approved grip at the elbow first.  This preserves the
    # bottle-to-finger relationship instead of repacking the hand at the mouth.
    elbow_swing = ready_forward.rotation_difference(forearm_direction)
    forward = (elbow_swing @ ready_forward).normalized()
    normal = (elbow_swing @ ready_normal).normalized()

    # The forearm aims slightly below the lens; the wrist only eases a few
    # degrees farther toward the camera, as in the user's reference motion.
    hand_aim = camera.location + Vector((0.0, 0.0, -0.03))
    hand_direction = (hand_aim - wrist).normalized()
    bend_axis = forward.cross(hand_direction)
    if bend_axis.length_squared > 1e-8 and wrist_bend_fraction > 1e-6:
        bend_angle = forward.angle(hand_direction) * wrist_bend_fraction
        wrist_bend = Matrix.Rotation(
            bend_angle, 3, bend_axis.normalized()
        )
        forward = (wrist_bend @ forward).normalized()
        normal = (wrist_bend @ normal).normalized()

    # Derive the pole direction that reproduces the approved elbow exactly.
    span = wrist - upper.head
    span_direction = span.normalized()
    span_length = span.length
    along = (
        upper.length * upper.length
        - forearm.length * forearm.length
        + span_length * span_length
    ) / (2.0 * span_length)
    elbow_base = upper.head + span_direction * along
    fixed_elbow_pole = (ready_elbow - elbow_base).normalized()

    diagnostics = pose_arm(
        rig,
        side,
        wrist,
        fixed_elbow_pole,
        forward,
        normal,
    )
    pose_bottle_grip(
        rig,
        side,
        thumb_variant="photo_surface_wide",
        finger_curls={
            "index": (36, 78, 33),
            "middle": (38, 82, 32),
            "ring": (34, 72, 26),
            "pinky": (20, 48, 18),
        },
    )
    add_gripped_bottle_proxy(
        rig,
        side,
        forward,
        normal,
        palm_offset=0.0593870527,
        normal_offset=0.0279216045,
        axial_offset=0.0212357036,
        top_left_tilt_degrees=4.0,
        tilt_local_direction=(-0.3513697, -0.9362368),
    )
    diagnostics["grip_forward"] = Vector(forward)
    diagnostics["grip_normal"] = Vector(normal)
    diagnostics["approved_elbow_target"] = ready_elbow
    diagnostics["forearm_aim"] = forearm_aim
    diagnostics["preview_composition_rotation_degrees"] = 10.0
    diagnostics["preview_composition_pivot"] = diagnostics["wrist"]
    return diagnostics


def drink_mouth_pull_camera(
    rig,
    side,
    pull_distance=0.25,
    wrist_bend_degrees=5.0,
    pose_swing_fraction=1.0,
    bottle_aim_degrees=0.0,
    camera_low_offset=0.08,
):
    """Pull the approved ready pose toward a point just below the camera."""
    ready_wrist = Vector((-0.16, -0.385, 1.30))
    ready_pole = Vector((-1.0, 0.45, -0.8))
    camera = bpy.context.scene.camera
    mouth_target = camera.location + Vector((0.0, 0.0, -camera_low_offset))
    pull_direction = (mouth_target - ready_wrist).normalized()
    wrist = ready_wrist + pull_direction * pull_distance

    upper = rig.pose.bones[side_name("upper_arm_fk", side)]
    forearm = rig.pose.bones[side_name("forearm_fk", side)]
    ready_forward, ready_normal = forearm_aligned_hand_frame(
        rig, side, ready_wrist, ready_pole, roll_degrees=0.0
    )
    elbow, reachable_wrist = solve_elbow(
        upper.head.copy(),
        wrist,
        upper.length,
        forearm.length,
        ready_pole,
    )
    forearm_direction = (reachable_wrist - elbow).normalized()
    ready_forward = Vector(ready_forward).normalized()
    ready_normal = Vector(ready_normal).normalized()
    pose_swing = ready_forward.rotation_difference(forearm_direction)
    partial_swing = Matrix.Rotation(
        pose_swing.angle * pose_swing_fraction,
        3,
        pose_swing.axis,
    )
    forward = (partial_swing @ ready_forward).normalized()
    normal = (partial_swing @ ready_normal).normalized()

    hand_aim = (mouth_target - wrist).normalized()
    bend_axis = forward.cross(hand_aim)
    if bend_axis.length_squared > 1e-8 and wrist_bend_degrees > 1e-6:
        bend_angle = min(
            math.radians(wrist_bend_degrees),
            forward.angle(hand_aim),
        )
        wrist_bend = Matrix.Rotation(
            bend_angle, 3, bend_axis.normalized()
        )
        forward = (wrist_bend @ forward).normalized()
        normal = (wrist_bend @ normal).normalized()

    bottle_axis = -normal.cross(forward).normalized()
    if bottle_axis.z < 0.0:
        bottle_axis.negate()
    bottle_aim = (mouth_target - wrist).normalized()
    bottle_aim_axis = bottle_axis.cross(bottle_aim)
    if bottle_aim_axis.length_squared > 1e-8 and bottle_aim_degrees > 1e-6:
        bottle_aim_angle = min(
            math.radians(bottle_aim_degrees),
            bottle_axis.angle(bottle_aim),
        )
        bottle_aim_rotation = Matrix.Rotation(
            bottle_aim_angle, 3, bottle_aim_axis.normalized()
        )
        forward = (bottle_aim_rotation @ forward).normalized()
        normal = (bottle_aim_rotation @ normal).normalized()

    diagnostics = pose_arm(
        rig,
        side,
        wrist,
        ready_pole,
        forward,
        normal,
    )
    pose_bottle_grip(
        rig,
        side,
        thumb_variant="photo_surface_wide",
        finger_curls={
            "index": (36, 78, 33),
            "middle": (38, 82, 32),
            "ring": (34, 72, 26),
            "pinky": (20, 48, 18),
        },
    )
    add_gripped_bottle_proxy(
        rig,
        side,
        forward,
        normal,
        palm_offset=0.0593870527,
        normal_offset=0.0279216045,
        axial_offset=0.0212357036,
        top_left_tilt_degrees=4.0,
        tilt_local_direction=(-0.3513697, -0.9362368),
    )
    diagnostics["grip_forward"] = Vector(forward)
    diagnostics["grip_normal"] = Vector(normal)
    diagnostics["mouth_target"] = mouth_target
    diagnostics["pull_distance"] = pull_distance
    diagnostics["preview_composition_rotation_degrees"] = 10.0
    diagnostics["preview_composition_pivot"] = diagnostics["wrist"]
    return diagnostics


def drink_mouth(rig, side):
    forward = (0.78, 0.35, 0.52)
    normal = (-0.28, 0.93, -0.22)
    diagnostics = pose_arm(
        rig,
        side,
        (-0.07, -0.21, 1.40),
        (-1.0, 0.55, -0.5),
        forward,
        normal,
    )
    pose_bottle_grip(rig, side)
    add_gripped_bottle_proxy(rig, side, forward, normal)
    return diagnostics


def wave(rig, side):
    diagnostics = pose_arm(
        rig,
        side,
        (-0.14, -0.36, 1.40),
        (-1.0, 0.3, -0.85),
        (0.0, 0.05, 1.0),
        (0.0, 1.0, -0.05),
    )
    pose_open_hand(rig, side)
    return diagnostics


def wave_user_reference(rig, side):
    """Raised right-hand pose measured from video_2026-08-11_18-03-02."""
    diagnostics = pose_arm(
        rig,
        side,
        (-0.03, -0.27, 1.36),
        (-0.50, 0.32, -0.88),
        (0.30, 0.03, 1.0),
        (0.0, -1.0, 0.03),
    )
    pose_open_hand(rig, side)
    return diagnostics


def middle_finger(rig, side):
    x = 0.20 if side == "L" else -0.20
    pole_x = 1.0 if side == "L" else -1.0
    diagnostics = pose_arm(
        rig,
        side,
        (x, -0.37, 1.40),
        (pole_x, 0.35, -0.9),
        (0.0, 0.03, 1.0),
        (0.0, 1.0, -0.03),
    )
    pose_middle_finger(rig, side)
    return diagnostics


def middle_finger_user_reference(rig, side):
    """Left-hand framing measured from the user's second photo reference."""
    diagnostics = pose_arm(
        rig,
        side,
        (0.12, -0.27, 1.32),
        (1.0, 0.35, -0.9),
        (-0.15, 0.03, 0.988),
        (0.0, 1.0, -0.03),
    )
    pose_middle_finger_photo_reference(rig, side)
    return diagnostics


def middle_finger_clamped_thumb(rig, side):
    x = 0.20 if side == "L" else -0.20
    pole_x = 1.0 if side == "L" else -1.0
    diagnostics = pose_arm(
        rig,
        side,
        (x, -0.37, 1.40),
        (pole_x, 0.35, -0.9),
        (0.0, 0.03, 1.0),
        (0.0, 1.0, -0.03),
    )
    pose_middle_finger(rig, side, thumb_variant="clamp")
    return diagnostics


def middle_finger_neutral_thumb(rig, side):
    x = 0.20 if side == "L" else -0.20
    pole_x = 1.0 if side == "L" else -1.0
    diagnostics = pose_arm(
        rig,
        side,
        (x, -0.37, 1.40),
        (pole_x, 0.35, -0.9),
        (0.0, 0.03, 1.0),
        (0.0, 1.0, -0.03),
    )
    pose_middle_finger(rig, side, thumb_variant="donor")
    return diagnostics


def middle_finger_anatomical_left(rig, side):
    # AXIS names this anatomical side .R.  The donor gesture is a left palm
    # (thumb on the viewer's right), presented from the lower-left of frame.
    diagnostics = pose_arm(
        rig,
        side,
        (-0.0865, -0.37, 1.40),
        (1.0, 0.35, -0.9),
        (0.0, 0.03, 1.0),
        (0.0, 1.0, -0.03),
    )
    pose_middle_finger(rig, side, thumb_variant="neutral")
    diagnostics["preview_translation"] = Vector((0.2865, 0.0, 0.0))
    return diagnostics


def middle_finger_anatomical_left_oblique(rig, side):
    diagnostics = middle_finger_anatomical_left(rig, side)
    diagnostics["review_camera_location"] = Vector((0.38, 0.24, 1.59))
    diagnostics["review_camera_target"] = Vector((0.16, -0.37, 1.43))
    diagnostics["review_camera_lens"] = 46
    return diagnostics


def thumb_up_reference(rig, side, palm_forward):
    """Left-hand thumbs-up pose lab matching the user's camera reference."""
    diagnostics = pose_arm(
        rig,
        side,
        (0.115, -0.29, 1.30),
        (0.90, 0.34, -0.86),
        palm_forward,
        (0.03, 1.0, -0.02),
    )
    pose_thumb_up(rig, side)
    return diagnostics


if os.environ.get("ARMS_KURWA_LIBRARY_ONLY") != "1":
    output_root = Path(os.environ["ARMS_KURWA_OUTPUT"])
    output_root.mkdir(parents=True, exist_ok=True)
    configure_review_scene()
    bpy.context.scene.render.resolution_percentage = int(
        os.environ.get("ARMS_KURWA_RENDER_PERCENT", "100")
    )
    poses = {
        "drink_ready_right": ("R", drink_ready),
        "drink_ready_hand_debug_right": ("R", drink_ready_hand_debug),
        "drink_ready_dorsal_right": ("R", drink_ready_dorsal),
        "drink_ready_photo_reference_right": (
            "R",
            drink_ready_photo_reference,
        ),
        "drink_ready_photo_reference_hand_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig, side, show_bottle=False
            ),
        ),
        "drink_straight_roll_p30_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig, side, show_bottle=False, roll_degrees=30.0
            ),
        ),
        "drink_straight_roll_n30_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig, side, show_bottle=False, roll_degrees=-30.0
            ),
        ),
        "drink_straight_roll_p60_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig, side, show_bottle=False, roll_degrees=60.0
            ),
        ),
        "drink_straight_roll_n60_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig, side, show_bottle=False, roll_degrees=-60.0
            ),
        ),
        "drink_straight_roll_180_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig, side, show_bottle=False, roll_degrees=180.0
            ),
        ),
        "drink_straight_thumb_x_pos_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig,
                side,
                show_bottle=False,
                thumb_variant="debug_x_pos",
            ),
        ),
        "drink_straight_thumb_x_neg_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig,
                side,
                show_bottle=False,
                thumb_variant="debug_x_neg",
            ),
        ),
        "drink_straight_thumb_y_pos_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig,
                side,
                show_bottle=False,
                thumb_variant="debug_y_pos",
            ),
        ),
        "drink_straight_thumb_y_neg_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig,
                side,
                show_bottle=False,
                thumb_variant="debug_y_neg",
            ),
        ),
        "drink_straight_thumb_z_pos_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig,
                side,
                show_bottle=False,
                thumb_variant="debug_z_pos",
            ),
        ),
        "drink_straight_thumb_z_neg_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig,
                side,
                show_bottle=False,
                thumb_variant="debug_z_neg",
            ),
        ),
        "drink_contact_a_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig, side, thumb_variant="photo_contact_a"
            ),
        ),
        "drink_contact_b_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig, side, thumb_variant="photo_contact_b"
            ),
        ),
        "drink_contact_c_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig, side, thumb_variant="photo_contact_c"
            ),
        ),
        "drink_quality_a_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig,
                side,
                thumb_variant="photo_surface_a",
                finger_curls={
                    "index": (36, 78, 45),
                    "middle": (38, 82, 48),
                    "ring": (34, 72, 38),
                    "pinky": (-5, 30, 60),
                },
                bottle_palm_offset=0.065,
                bottle_normal_offset=0.015,
                bottle_top_left_tilt_degrees=4.0,
            ),
        ),
        "drink_quality_shift_1cm_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig,
                side,
                thumb_variant="photo_surface_wide",
                finger_curls={
                    # Widen only the outer arc after moving the bottle.  MCP/PIP
                    # keep the cylindrical wrap; DIP angles back off just enough
                    # for the skinned fingertips to sit outside the surface.
                    "index": (36, 78, 33),
                    "middle": (38, 82, 32),
                    "ring": (34, 72, 26),
                    "pinky": (20, 48, 18),
                },
                bottle_palm_offset=0.065,
                bottle_normal_offset=0.015,
                bottle_top_left_tilt_degrees=4.0,
                # Blender camera axes are mirrored relative to the user's
                # first-person reading of the review image: +X/+Y is the
                # requested one-centimetre forward-left correction.
                bottle_world_offset=(0.01, 0.01, 0.0),
            ),
        ),
        "drink_composition_n08_right": (
            "R",
            lambda rig, side: drink_ready_locked_composition(
                rig, side, rotation_degrees=-8.0
            ),
        ),
        "drink_composition_n10_right": (
            "R",
            lambda rig, side: drink_ready_locked_composition(
                rig, side, rotation_degrees=-10.0
            ),
        ),
        "drink_composition_n12_right": (
            "R",
            lambda rig, side: drink_ready_locked_composition(
                rig, side, rotation_degrees=-12.0
            ),
        ),
        "drink_composition_p10_right": (
            "R",
            lambda rig, side: drink_ready_locked_composition(
                rig, side, rotation_degrees=10.0
            ),
        ),
        "drink_ready_approved_right": (
            "R",
            drink_ready_locked_composition,
        ),
        "drink_ready_approved_fk_right": (
            "R",
            drink_ready_fk_validation,
        ),
        "drink_mouth_elbow_fk_42_right": (
            "R",
            lambda rig, side: drink_mouth_elbow_fk(
                rig, side, elbow_swing_fraction=0.42
            ),
        ),
        "drink_mouth_elbow_fk_55_right": (
            "R",
            lambda rig, side: drink_mouth_elbow_fk(
                rig, side, elbow_swing_fraction=0.55
            ),
        ),
        "drink_mouth_elbow_fk_68_right": (
            "R",
            lambda rig, side: drink_mouth_elbow_fk(
                rig, side, elbow_swing_fraction=0.68
            ),
        ),
        "drink_mouth_elbow_fk_78_right": (
            "R",
            lambda rig, side: drink_mouth_elbow_fk(
                rig, side, elbow_swing_fraction=0.78
            ),
        ),
        "drink_mouth_elbow_fk_68_center06_right": (
            "R",
            lambda rig, side: drink_mouth_elbow_fk(
                rig,
                side,
                elbow_swing_fraction=0.68,
                mouth_screen_x_offset=-0.06,
            ),
        ),
        "drink_mouth_elbow_fk_68_center12_right": (
            "R",
            lambda rig, side: drink_mouth_elbow_fk(
                rig,
                side,
                elbow_swing_fraction=0.68,
                mouth_screen_x_offset=-0.12,
            ),
        ),
        "drink_mouth_elbow_fk_68_center18_right": (
            "R",
            lambda rig, side: drink_mouth_elbow_fk(
                rig,
                side,
                elbow_swing_fraction=0.68,
                mouth_screen_x_offset=-0.18,
            ),
        ),
        "drink_mouth_elbow_fk_center_wrist10_right": (
            "R",
            lambda rig, side: drink_mouth_elbow_fk(
                rig,
                side,
                elbow_swing_fraction=0.68,
                wrist_bend_degrees=10.0,
                mouth_screen_x_offset=-0.18,
            ),
        ),
        "drink_mouth_elbow_fk_center_wrist14_right": (
            "R",
            lambda rig, side: drink_mouth_elbow_fk(
                rig,
                side,
                elbow_swing_fraction=0.68,
                wrist_bend_degrees=14.0,
                mouth_screen_x_offset=-0.18,
            ),
        ),
        "drink_mouth_elbow_fk_center_wrist18_right": (
            "R",
            lambda rig, side: drink_mouth_elbow_fk(
                rig,
                side,
                elbow_swing_fraction=0.68,
                wrist_bend_degrees=18.0,
                mouth_screen_x_offset=-0.18,
            ),
        ),
        "drink_mouth_bottle_pull_10_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_pull_fk(
                rig, side, pull_distance=0.10
            ),
        ),
        "drink_mouth_bottle_pull_15_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_pull_fk(
                rig, side, pull_distance=0.15
            ),
        ),
        "drink_mouth_bottle_pull_20_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_pull_fk(
                rig, side, pull_distance=0.20
            ),
        ),
        "drink_mouth_bottle_pull_24_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_pull_fk(
                rig, side, pull_distance=0.24
            ),
        ),
        "drink_mouth_bottle_lead_10_30_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                pull_distance=0.10,
                bottle_aim_fraction=0.30,
            ),
        ),
        "drink_mouth_bottle_lead_15_50_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                pull_distance=0.15,
                bottle_aim_fraction=0.50,
            ),
        ),
        "drink_mouth_bottle_lead_20_70_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                pull_distance=0.20,
                bottle_aim_fraction=0.70,
            ),
        ),
        "drink_mouth_bottle_lead_30_80_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                pull_distance=0.30,
                bottle_aim_fraction=0.80,
            ),
        ),
        "drink_mouth_bottle_lead_40_92_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                pull_distance=0.40,
                bottle_aim_fraction=0.92,
            ),
        ),
        "drink_mouth_bottle_lead_48_100_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                pull_distance=0.48,
                bottle_aim_fraction=1.0,
            ),
        ),
        "drink_mouth_center_low_03_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                bottle_aim_fraction=1.0,
                mouth_forward_distance=0.34,
                mouth_down_offset=0.03,
                neck_target_fraction=1.0,
                grip_relax_fraction=1.0,
            ),
        ),
        "drink_mouth_center_low_04_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                bottle_aim_fraction=1.0,
                mouth_forward_distance=0.34,
                mouth_down_offset=0.04,
                neck_target_fraction=1.0,
                grip_relax_fraction=1.0,
            ),
        ),
        "drink_mouth_center_low_05_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                bottle_aim_fraction=1.0,
                mouth_forward_distance=0.34,
                mouth_down_offset=0.05,
                neck_target_fraction=1.0,
                grip_relax_fraction=1.0,
            ),
        ),
        "drink_swallow_angle_70_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                bottle_aim_fraction=0.70,
                mouth_forward_distance=0.34,
                mouth_down_offset=0.10,
                neck_target_fraction=1.0,
                grip_relax_fraction=0.65,
                bottle_normal_clearance=0.004,
            ),
        ),
        "drink_swallow_angle_80_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                bottle_aim_fraction=0.80,
                mouth_forward_distance=0.34,
                mouth_down_offset=0.10,
                neck_target_fraction=1.0,
                grip_relax_fraction=0.65,
                bottle_normal_clearance=0.004,
            ),
        ),
        "drink_swallow_angle_90_right": (
            "R",
            lambda rig, side: drink_mouth_bottle_lead_ik(
                rig,
                side,
                bottle_aim_fraction=0.90,
                mouth_forward_distance=0.34,
                mouth_down_offset=0.10,
                neck_target_fraction=1.0,
                grip_relax_fraction=0.65,
                bottle_normal_clearance=0.004,
            ),
        ),
        "drink_mouth_locked_n60_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig, side, roll_degrees=-60.0
            ),
        ),
        "drink_mouth_locked_n120_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig, side, roll_degrees=-120.0
            ),
        ),
        "drink_mouth_locked_n90_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig, side, roll_degrees=-90.0
            ),
        ),
        "drink_mouth_locked_n30_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig, side, roll_degrees=-30.0
            ),
        ),
        "drink_mouth_locked_0_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig, side, roll_degrees=0.0
            ),
        ),
        "drink_mouth_locked_p30_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig, side, roll_degrees=30.0
            ),
        ),
        "drink_mouth_locked_p60_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig, side, roll_degrees=60.0
            ),
        ),
        "drink_mouth_locked_p90_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig, side, roll_degrees=90.0
            ),
        ),
        "drink_mouth_locked_p120_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig, side, roll_degrees=120.0
            ),
        ),
        "drink_mouth_locked_p150_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig, side, roll_degrees=150.0
            ),
        ),
        "drink_mouth_reference_a_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=140.0,
                wrist=(-0.11, -0.12, 1.33),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_reference_b_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                wrist=(-0.11, -0.12, 1.33),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_reference_c_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=160.0,
                wrist=(-0.11, -0.12, 1.33),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_composition_06_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=6.0,
            ),
        ),
        "drink_mouth_composition_14_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=14.0,
            ),
        ),
        "drink_mouth_composition_22_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_pitch_n50_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                hand_pitch_degrees=-50.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_pitch_n25_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                hand_pitch_degrees=-25.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_pitch_0_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                hand_pitch_degrees=0.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_pitch_p25_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                hand_pitch_degrees=25.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_pitch_p50_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                hand_pitch_degrees=50.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_screen_n25_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                hand_screen_rotation_degrees=-25.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_screen_n40_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                hand_screen_rotation_degrees=-40.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_screen_n55_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                hand_screen_rotation_degrees=-55.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_roll130_screen_n35_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=130.0,
                hand_screen_rotation_degrees=-35.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_roll140_screen_n35_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=140.0,
                hand_screen_rotation_degrees=-35.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_roll150_screen_n35_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=150.0,
                hand_screen_rotation_degrees=-35.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_roll160_screen_n35_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=160.0,
                hand_screen_rotation_degrees=-35.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_roll180_screen_n35_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=180.0,
                hand_screen_rotation_degrees=-35.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_roll210_screen_n35_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=210.0,
                hand_screen_rotation_degrees=-35.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_roll240_screen_n35_right": (
            "R",
            lambda rig, side: drink_mouth_locked_variant(
                rig,
                side,
                roll_degrees=240.0,
                hand_screen_rotation_degrees=-35.0,
                wrist=(-0.04, -0.12, 1.395),
                composition_rotation_degrees=22.0,
            ),
        ),
        "drink_mouth_camera_bend_0_right": (
            "R",
            lambda rig, side: drink_mouth_toward_camera(
                rig, side, wrist_bend_fraction=0.0
            ),
        ),
        "drink_mouth_camera_bend_05_right": (
            "R",
            lambda rig, side: drink_mouth_toward_camera(
                rig, side, wrist_bend_fraction=0.5
            ),
        ),
        "drink_mouth_camera_bend_1_right": (
            "R",
            lambda rig, side: drink_mouth_toward_camera(
                rig, side, wrist_bend_fraction=1.0
            ),
        ),
        "drink_mouth_pull_18_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig, side, pull_distance=0.18, wrist_bend_degrees=5.0
            ),
        ),
        "drink_mouth_pull_25_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig, side, pull_distance=0.25, wrist_bend_degrees=5.0
            ),
        ),
        "drink_mouth_pull_32_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig, side, pull_distance=0.32, wrist_bend_degrees=5.0
            ),
        ),
        "drink_mouth_pull_swing_0_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.25,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.0,
            ),
        ),
        "drink_mouth_pull_swing_04_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.25,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.4,
            ),
        ),
        "drink_mouth_pull_swing_07_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.25,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.7,
            ),
        ),
        "drink_mouth_camera_aim_05_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.28,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.0,
                bottle_aim_degrees=5.0,
            ),
        ),
        "drink_mouth_camera_aim_10_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.28,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.0,
                bottle_aim_degrees=10.0,
            ),
        ),
        "drink_mouth_camera_aim_15_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.28,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.0,
                bottle_aim_degrees=15.0,
            ),
        ),
        "drink_mouth_depth_32_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.32,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.0,
                bottle_aim_degrees=10.0,
            ),
        ),
        "drink_mouth_depth_36_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.36,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.0,
                bottle_aim_degrees=10.0,
            ),
        ),
        "drink_mouth_depth_40_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.40,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.0,
                bottle_aim_degrees=10.0,
            ),
        ),
        "drink_mouth_low_08_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.36,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.0,
                bottle_aim_degrees=10.0,
                camera_low_offset=0.08,
            ),
        ),
        "drink_mouth_low_16_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.36,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.0,
                bottle_aim_degrees=10.0,
                camera_low_offset=0.16,
            ),
        ),
        "drink_mouth_low_24_right": (
            "R",
            lambda rig, side: drink_mouth_pull_camera(
                rig,
                side,
                pull_distance=0.36,
                wrist_bend_degrees=0.0,
                pose_swing_fraction=0.0,
                bottle_aim_degrees=10.0,
                camera_low_offset=0.24,
            ),
        ),
        "drink_quality_b_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig,
                side,
                thumb_variant="photo_surface_b",
                finger_curls={
                    "index": (36, 78, 45),
                    "middle": (38, 82, 48),
                    "ring": (34, 72, 38),
                    "pinky": (-5, 30, 60),
                },
                bottle_palm_offset=0.065,
                bottle_normal_offset=0.015,
                bottle_top_left_tilt_degrees=4.0,
            ),
        ),
        "drink_quality_c_right": (
            "R",
            lambda rig, side: drink_ready_photo_reference(
                rig,
                side,
                thumb_variant="photo_surface_c",
                finger_curls={
                    "index": (36, 78, 45),
                    "middle": (38, 82, 48),
                    "ring": (34, 72, 38),
                    "pinky": (-5, 30, 60),
                },
                bottle_palm_offset=0.065,
                bottle_normal_offset=0.015,
                bottle_top_left_tilt_degrees=4.0,
            ),
        ),
        "drink_ready_dorsal_photo_hand_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="photo", show_bottle=False
            ),
        ),
        "drink_photo_grip_a_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="photo_grip_a"
            ),
        ),
        "drink_photo_grip_b_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="photo_grip_b"
            ),
        ),
        "drink_photo_grip_c_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="photo_grip_c"
            ),
        ),
        "drink_orientation_45_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig,
                side,
                show_bottle=False,
                normal_override=(0.0, 0.7071, 0.7071),
            ),
        ),
        "drink_orientation_90_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, show_bottle=False, normal_override=(0.0, 0.0, 1.0)
            ),
        ),
        "drink_orientation_135_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig,
                side,
                show_bottle=False,
                normal_override=(0.0, -0.7071, 0.7071),
            ),
        ),
        "drink_orientation_180_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, show_bottle=False, normal_override=(0.0, -1.0, 0.0)
            ),
        ),
        "drink_orientation_225_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig,
                side,
                show_bottle=False,
                normal_override=(0.0, -0.7071, -0.7071),
            ),
        ),
        "drink_orientation_270_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, show_bottle=False, normal_override=(0.0, 0.0, -1.0)
            ),
        ),
        "drink_orientation_315_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig,
                side,
                show_bottle=False,
                normal_override=(0.0, 0.7071, -0.7071),
            ),
        ),
        "drink_thumb_debug_x_pos_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="debug_x_pos", show_bottle=False
            ),
        ),
        "drink_thumb_debug_x_neg_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="debug_x_neg", show_bottle=False
            ),
        ),
        "drink_thumb_debug_y_pos_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="debug_y_pos", show_bottle=False
            ),
        ),
        "drink_thumb_debug_y_neg_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="debug_y_neg", show_bottle=False
            ),
        ),
        "drink_thumb_debug_z_pos_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="debug_z_pos", show_bottle=False
            ),
        ),
        "drink_thumb_debug_z_neg_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="debug_z_neg", show_bottle=False
            ),
        ),
        "drink_ready_dorsal_yflip_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="y_flip"
            ),
        ),
        "drink_ready_dorsal_zflip_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="z_flip"
            ),
        ),
        "drink_ready_dorsal_yzflip_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="yz_flip"
            ),
        ),
        "drink_ready_dorsal_hand_base_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="base", show_bottle=False
            ),
        ),
        "drink_ready_dorsal_hand_yflip_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="y_flip", show_bottle=False
            ),
        ),
        "drink_ready_dorsal_hand_zflip_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="z_flip", show_bottle=False
            ),
        ),
        "drink_ready_dorsal_hand_yzflip_right": (
            "R",
            lambda rig, side: drink_ready_dorsal_variant(
                rig, side, thumb_variant="yz_flip", show_bottle=False
            ),
        ),
        "drink_mouth_right": ("R", drink_mouth),
        "wave_right": ("R", wave),
        "wave_user_reference_right": ("R", wave_user_reference),
        "middle_finger_left": ("L", middle_finger),
        "middle_finger_user_reference_left": (
            "L",
            middle_finger_user_reference,
        ),
        "middle_finger_clamp_left": ("L", middle_finger_clamped_thumb),
        "middle_finger_neutral_left": ("L", middle_finger_neutral_thumb),
        "middle_finger_anatomical_left": ("R", middle_finger_anatomical_left),
        "middle_finger_anatomical_left_oblique": (
            "R",
            middle_finger_anatomical_left_oblique,
        ),
        "thumb_up_forward_z_left": (
            "L",
            lambda rig, side: thumb_up_reference(
                rig, side, (-0.07, 0.02, 1.0)
            ),
        ),
        "thumb_up_forward_x_left": (
            "L",
            lambda rig, side: thumb_up_reference(
                rig, side, (1.0, 0.02, 0.07)
            ),
        ),
        "thumb_up_forward_neg_x_left": (
            "L",
            lambda rig, side: thumb_up_reference(
                rig, side, (-1.0, 0.02, -0.07)
            ),
        ),
    }
    sequence_frame_count = int(
        os.environ.get("ARMS_KURWA_DRINK_SEQUENCE_FRAMES", "0")
    )
    if sequence_frame_count > 0:
        if sequence_frame_count < 2:
            raise ValueError("Drink sequence preview needs at least two frames")
        for frame_index in range(sequence_frame_count):
            normalized_time = frame_index / (sequence_frame_count - 1)
            render_pose(
                output_root,
                f"drink_cycle_{frame_index:03d}_right",
                "R",
                lambda rig, side, sample_time=normalized_time: drink_cycle_sample(
                    rig, side, sample_time
                ),
            )
    else:
        requested_poses = {
            name.strip()
            for name in os.environ.get("ARMS_KURWA_POSES", "").split(",")
            if name.strip()
        }
        for pose_name, (side, callback) in poses.items():
            if requested_poses and pose_name not in requested_poses:
                continue
            render_pose(output_root, pose_name, side, callback)
