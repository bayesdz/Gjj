"""Preview poses (game animations come from the Animator in Unity).

Poses are described by world-space directions per bone ("aim"), which is independent
of bone roll, and finger curls about the knuckle line.
"""
import bpy
from mathutils import Matrix, Vector


def reset(rig):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    rig.location = (0, 0, 0)
    bpy.context.view_layer.update()


def aim(rig, bone, direction):
    pb = rig.pose.bones.get(bone)
    if pb is None:
        return
    bpy.context.view_layer.update()
    m = pb.matrix.copy()
    cur = (m.to_3x3() @ Vector((0, 1, 0))).normalized()
    r = cur.rotation_difference(Vector(direction).normalized()).to_matrix().to_4x4()
    head = m.translation.copy()
    pb.matrix = Matrix.Translation(head) @ r @ Matrix.Translation(-head) @ m


def turn(rig, bone, axis, angle):
    """Rotate a posed bone about a world axis through its head."""
    pb = rig.pose.bones.get(bone)
    if pb is None:
        return
    bpy.context.view_layer.update()
    m = pb.matrix.copy()
    head = m.translation.copy()
    r = Matrix.Rotation(angle, 4, Vector(axis))
    pb.matrix = Matrix.Translation(head) @ r @ Matrix.Translation(-head) @ m


def curl(rig, side, amount, thumb=0.3):
    s = 1 if side == "Left" else -1
    bpy.context.view_layer.update()
    P = rig.pose.bones
    a = P[f"{side}HandIndex1"].matrix.translation
    b = P[f"{side}HandPinky1"].matrix.translation
    across = (b - a).normalized()
    for f in ("Index", "Middle", "Ring", "Pinky"):
        for j, k in ((1, 0.8), (2, 1.0), (3, 0.8)):
            name = f"{side}Hand{f}{j}"
            bpy.context.view_layer.update()
            pb = P[name]
            d = (pb.matrix.to_3x3() @ Vector((0, 1, 0))).normalized()
            # pick the sign that moves the finger toward the palm (palm normal ~ d x across)
            palm = d.cross(across) * s
            ang = amount * k
            test = Matrix.Rotation(ang, 3, across) @ d
            if (test - d).dot(palm) < 0:
                ang = -ang
            turn(rig, name, across, ang)
    for j in (2, 3):
        name = f"{side}HandThumb{j}"
        bpy.context.view_layer.update()
        pb = P[name]
        d = (pb.matrix.to_3x3() @ Vector((0, 1, 0))).normalized()
        turn(rig, name, d.cross(Vector((0, 0, 1))).normalized(), thumb * 0.5)


def ground(rig, meshes):
    """Move the rig so the lowest vertex touches z=0."""
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    body = meshes["goblin_body"]
    ev = body.evaluated_get(dg)
    zmin = min((body.matrix_world @ v.co).z for v in ev.data.vertices)
    rig.location.z -= zmin
    bpy.context.view_layer.update()


def menace(rig, meshes):
    """Crouching, hunched, claws forward, mouth open: what the Voice wants you to see."""
    reset(rig)
    for b, d in (("Spine", (0, -0.25, 1)), ("Spine1", (0, -0.5, 0.86)), ("Spine2", (0, -0.55, 0.83)),
                 ("Neck", (0, -0.75, 0.66)), ("Head", (0.08, -0.12, 1))):
        aim(rig, b, d)
    turn(rig, "Jaw", (1, 0, 0), 0.3)
    for s, side in ((1, "Left"), (-1, "Right")):
        aim(rig, f"{side}UpLeg", (0.30 * s, -0.55, -0.78))
        aim(rig, f"{side}Leg", (0.05 * s, 0.40, -0.92))
        aim(rig, f"{side}Foot", (0.15 * s, -0.95, -0.35))
        aim(rig, f"{side}Arm", (0.55 * s, -0.45, -0.70))
        aim(rig, f"{side}ForeArm", (0.30 * s, -0.92, -0.05))
        aim(rig, f"{side}Hand", (0.15 * s, -0.85, -0.45))
        curl(rig, side, 0.45)
    ground(rig, meshes)


def cower(rig, meshes):
    """Frightened villager shielding itself: what the Eye of Truth shows."""
    reset(rig)
    for b, d in (("Spine", (0, -0.12, 1)), ("Spine1", (0, -0.25, 0.97)), ("Spine2", (0, -0.30, 0.95)),
                 ("Neck", (0, -0.30, 0.95)), ("Head", (-0.25, -0.30, 0.92))):
        aim(rig, b, d)
    for s, side in ((1, "Left"), (-1, "Right")):
        aim(rig, f"{side}UpLeg", (0.10 * s, -0.25, -0.96))
        aim(rig, f"{side}Leg", (0.0, 0.25, -0.97))
        aim(rig, f"{side}Foot", (0.10 * s, -0.95, -0.30))
    # left hand raised in front of the face, right hand clutching the chest
    aim(rig, "LeftArm", (0.65, -0.55, -0.15))
    aim(rig, "LeftForeArm", (-0.05, -0.40, 0.91))
    aim(rig, "LeftHand", (-0.1, -0.25, 0.96))
    aim(rig, "RightArm", (-0.20, -0.30, -0.93))
    aim(rig, "RightForeArm", (0.75, -0.62, 0.12))
    aim(rig, "RightHand", (0.8, -0.4, 0.2))
    curl(rig, "Left", 0.15)
    curl(rig, "Right", 0.35)
    ground(rig, meshes)
