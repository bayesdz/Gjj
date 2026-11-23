"""Goblin character: MakeHuman base -> morphs -> bone proportions -> goblin sculpt."""
import math

import bpy
import numpy as np
from mathutils import Vector

from goblin import mh

HEIGHT = 1.15  # metres

TARGETS = [
    ("macrodetails/universal-male-old-minmuscle-minweight", 1.0),
    ("stomach/stomach-pregnant-incr", 0.35),
    ("torso/torso-vshape-decr", 0.7),
    ("neck/neck-scale-vert-decr", 0.5),
    ("head/head-scale-depth-incr", 0.5),
    ("nose/nose-trans-forward", 0.7),
    ("nose/nose-scale-vert-incr", 0.6),
    ("nose/nose-hump-incr", 1.4),
    ("nose/nose-point-down", 1.4),
    ("nose/nose-scale-depth-incr", 1.0),
    ("nose/nose-point-width-decr", 0.4),
    ("nose/nose-nostrils-width-incr", 0.7),
    ("eyebrows/eyebrows-trans-forward", 1.8),
    ("eyebrows/eyebrows-angle-down", 1.2),
    ("chin/chin-prominent-incr", 1.3),
    ("mouth/mouth-scale-horiz-incr", 0.9),
    ("mouth/mouth-lowerlip-volume-decr", 1.0),
    ("mouth/mouth-upperlip-volume-decr", 1.0),
]
for side in "lr":
    TARGETS += [
        (f"ears/{side}-ear-shape-pointed", 1.6),
        (f"ears/{side}-ear-scale-incr", 1.3),
        (f"ears/{side}-ear-wing-incr", 0.5),
        (f"ears/{side}-ear-rot-backward", 0.6),
        (f"cheek/{side}-cheek-bones-incr", 0.8),
        (f"cheek/{side}-cheek-inner-decr", 1.5),
        (f"eyes/{side}-eye-scale-incr", 0.9),
        (f"armslegs/{side}-hand-fingers-length-incr", 1.0),
    ]

# Bone proportions: name -> (uniform scale) or (x, y, z) local scale. Y = along bone.
PROPORTIONS = {
    "head": 1.36,
    "neck01": (1, 0.75, 1), "neck02": (1, 0.75, 1), "neck03": (1, 0.75, 1),
}
for s in "LR":
    PROPORTIONS.update({
        f"upperarm01.{s}": (0.9, 1.12, 0.9), f"upperarm02.{s}": (0.9, 1.12, 0.9),
        f"lowerarm01.{s}": (0.9, 1.16, 0.9), f"lowerarm02.{s}": (0.9, 1.16, 0.9),
        f"wrist.{s}": 1.28,
        f"upperleg01.{s}": (1, 0.86, 1), f"upperleg02.{s}": (1, 0.86, 1),
        f"lowerleg01.{s}": (0.9, 0.86, 0.9), f"lowerleg02.{s}": (0.9, 0.86, 0.9),
        f"foot.{s}": 1.25,
    })

PARTS = {
    "goblin_body": ["body"],
    "goblin_eyes": ["helper-l-eye", "helper-r-eye"],
    "goblin_teeth": ["helper-upper-teeth", "helper-lower-teeth"],
    "goblin_tongue": ["helper-tongue"],
}


def _apply_mods(ob):
    bpy.context.view_layer.objects.active = ob
    for m in list(ob.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def build_base():
    base = mh.Base.load()
    for rel, w in TARGETS:
        base.apply_target(rel, w)
    skel = mh.load_skeleton()
    weights = mh.load_weights()
    rig = mh.build_armature(base, skel)
    meshes = {}
    for name, groups in PARTS.items():
        ob, _ = base.make_object(name, groups, weights)
        m = ob.modifiers.new("rig", "ARMATURE")
        m.object = rig
        meshes[name] = ob

    # -- proportions via pose, then bake pose into rest
    nonuniform = {k for k, v in PROPORTIONS.items() if not isinstance(v, (int, float))}
    for b in rig.data.bones:
        if b.parent and b.parent.name in nonuniform:
            b.inherit_scale = "NONE"
    for bn, s in PROPORTIONS.items():
        pb = rig.pose.bones[bn]
        pb.scale = (s, s, s) if isinstance(s, (int, float)) else s
    bpy.context.view_layer.update()
    for ob in meshes.values():
        _apply_mods(ob)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for b in rig.data.bones:
        b.inherit_scale = "FULL"

    # -- normalise height, feet on the ground
    body = meshes["goblin_body"]
    zs = np.array([v.co.z for v in body.data.vertices])
    k = HEIGHT / (zs.max() - zs.min())
    for ob in [rig] + list(meshes.values()):
        ob.scale = (k, k, k)
        ob.location.z = -zs.min() * k
    bpy.ops.object.select_all(action="DESELECT")
    for ob in [rig] + list(meshes.values()):
        ob.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for ob in meshes.values():
        ob.parent = rig
        m = ob.modifiers.new("rig", "ARMATURE")
        m.object = rig
    return base, rig, meshes


# ------------------------------------------------------------------ goblin sculpt
def _get_co(ob):
    co = np.empty(len(ob.data.vertices) * 3)
    ob.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def _set_co(ob, co):
    ob.data.vertices.foreach_set("co", co.ravel())
    ob.data.update()


def _mask(base, ob, rel):
    m = base.target_mask(rel)
    idx = np.empty(len(ob.data.vertices), dtype=np.int64)
    ob.data.attributes["mh_index"].data.foreach_get("value", idx)
    return np.array([m.get(i, 0.0) for i in idx])


def _sphere(co, center, radius):
    d = np.linalg.norm(co - np.asarray(center), axis=1) / radius
    w = np.clip(1 - d, 0, 1)
    return w * w * (3 - 2 * w)


def _unit(*v):
    v = np.array(v, dtype=float)
    return v / np.linalg.norm(v)


def goblin_sculpt(base, rig, meshes):
    body = meshes["goblin_body"]
    co = _get_co(body)

    # long ears swept up and back: stretch along D, more toward the tip
    for side, sgn in (("l", 1), ("r", -1)):
        m = _mask(base, body, f"ears/{side}-ear-scale-incr")
        wing = _mask(base, body, f"ears/{side}-ear-wing-incr")   # 0 at the skull attachment
        w = (m ** 1.2) * (wing ** 0.8)
        w /= w.max()
        co += (w ** 1.3)[:, None] * _unit(0.75 * sgn, 0.38, 0.55) * 0.095
        co[:, 2] -= (w ** 4) * 0.010          # tip droops a little
    # long hooked nose
    m = _mask(base, body, "nose/nose-scale-depth-incr")
    co += (m ** 1.3)[:, None] * np.array([0, -0.022, -0.004])
    co[:, 2] -= (m ** 3.5) * 0.020
    # heavy brow ridge
    m = _mask(base, body, "eyebrows/eyebrows-trans-forward")
    co += m[:, None] * np.array([0, -0.006, 0.0015])
    # grim mouth: corners pulled down
    m = _mask(base, body, "mouth/mouth-scale-horiz-incr")
    co[:, 2] -= (m ** 2) * 0.006
    # flatten the chest (base mesh is androgynous)
    for s in ("L", "R"):
        b = rig.data.bones[f"breast.{s}"]
        w = _sphere(co, b.tail_local, 0.075)
        co[:, 1] += w * 0.03 * (co[:, 1] < b.tail_local.y + 0.04)
    _set_co(body, co)


# ------------------------------------------------------------------ Unity humanoid names
# MakeHuman bones -> Mixamo-style names (Unity auto-maps them to Humanoid,
# so Mixamo / any humanoid animation retargets onto the goblin).
HUMANOID = {
    "root": "Hips", "spine05": "Spine", "spine04": "Spine_x1", "spine03": "Spine1",
    "spine02": "Spine_x2", "spine01": "Spine2", "neck01": "Neck", "neck02": "Neck_x1",
    "neck03": "Neck_x2", "head": "Head", "jaw": "Jaw",
}
for s, side in (("L", "Left"), ("R", "Right")):
    HUMANOID.update({
        f"clavicle.{s}": f"{side}Shoulder", f"shoulder01.{s}": f"{side}Shoulder_x",
        f"upperarm01.{s}": f"{side}Arm", f"upperarm02.{s}": f"{side}ArmTwist",
        f"lowerarm01.{s}": f"{side}ForeArm", f"lowerarm02.{s}": f"{side}ForeArmTwist",
        f"wrist.{s}": f"{side}Hand",
        f"pelvis.{s}": f"{side}HipJoint", f"upperleg01.{s}": f"{side}UpLeg",
        f"upperleg02.{s}": f"{side}UpLegTwist", f"lowerleg01.{s}": f"{side}Leg",
        f"lowerleg02.{s}": f"{side}LegTwist", f"foot.{s}": f"{side}Foot",
        f"toe3-1.{s}": f"{side}ToeBase", f"eye.{s}": f"{side}Eye",
    })
    for i, f in enumerate(["Thumb", "Index", "Middle", "Ring", "Pinky"], start=1):
        for j in (1, 2, 3):
            HUMANOID[f"finger{i}-{j}.{s}"] = f"{side}Hand{f}{j}"


def rename_bones(rig):
    for old, new in HUMANOID.items():
        b = rig.data.bones.get(old)
        if b:
            b.name = new  # vertex groups of skinned meshes follow automatically


# ------------------------------------------------------------------ props
def _new_mesh_obj(name, verts, faces, rig=None, group=None):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.validate()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    if rig:
        ob.parent = rig
        m = ob.modifiers.new("rig", "ARMATURE")
        m.object = rig
        if group:
            vg = ob.vertex_groups.new(name=group)
            vg.add(list(range(len(verts))), 1.0, "REPLACE")
    return ob


def build_eyes(rig, meshes):
    """Replace helper eyes with UV spheres whose UV centre is the pupil (painted later)."""
    old = meshes["goblin_eyes"]
    co = _get_co(old)
    verts, faces, uvs = [], [], []
    seg, rings = 32, 16
    for sgn in (1, -1):
        e = co[co[:, 0] * sgn > 0]
        c = (e.max(0) + e.min(0)) / 2
        r = (e.max(0) - e.min(0)).max() / 2 * 0.97
        o = len(verts)
        for i in range(rings + 1):
            th = math.pi * i / rings                 # 0 = front (-Y)
            for j in range(seg + 1):
                ph = 2 * math.pi * j / seg
                d = np.array([math.sin(th) * math.cos(ph), -math.cos(th), math.sin(th) * math.sin(ph)])
                # slight cornea bulge
                rr = r * (1 + 0.06 * max(0.0, math.cos(th)) ** 6)
                verts.append(tuple(c + d * rr))
                rad = th / math.pi * 0.5               # polar UV: pupil at (0.5, 0.5)
                uvs.append((0.5 + rad * math.cos(ph), 0.5 + rad * math.sin(ph)))
        for i in range(rings):
            for j in range(seg):
                a = o + i * (seg + 1) + j
                faces.append((a, a + 1, a + seg + 2, a + seg + 1))
    bpy.data.objects.remove(old)
    ob = _new_mesh_obj("goblin_eyes", verts, faces, rig, "Head")
    uvl = ob.data.uv_layers.new(name="UVMap")
    for p in ob.data.polygons:
        for li in p.loop_indices:
            uvl.data[li].uv = uvs[ob.data.loops[li].vertex_index]
    meshes["goblin_eyes"] = ob
    return ob


def build_tusks(rig, meshes):
    """Two lower tusks jutting over the upper lip, skinned to the jaw."""
    teeth = meshes["goblin_teeth"]
    tc = _get_co(teeth)
    lower = tc[tc[:, 2] < np.median(tc[:, 2])]
    front_y = lower[:, 1].min()
    zt = lower[:, 2].max()
    verts, faces = [], []
    seg, rings = 10, 12
    for sgn in (1, -1):
        o = len(verts)
        base = np.array([0.017 * sgn, front_y + 0.006, zt - 0.006])
        for i in range(rings + 1):
            t = i / rings
            # curve: up, forward, outward
            p = base + np.array([0.008 * sgn * t, -0.010 * t - 0.006 * t * t, 0.034 * t])
            r = 0.0052 * (1 - t) ** 0.7 + 0.0004
            for j in range(seg):
                a = 2 * math.pi * j / seg
                verts.append(tuple(p + np.array([math.cos(a) * r, math.sin(a) * r * 0.8, 0])))
        for i in range(rings):
            for j in range(seg):
                a = o + i * seg + j
                b = o + i * seg + (j + 1) % seg
                faces.append((a, b, b + seg, a + seg))
        tip = len(verts)
        verts.append(tuple(base + np.array([0.0085 * sgn, -0.0175, 0.036])))
        for j in range(seg):
            faces.append((o + rings * seg + j, o + rings * seg + (j + 1) % seg, tip))
    tusks = _new_mesh_obj("goblin_tusks", verts, faces, rig, "Jaw")
    meshes["goblin_tusks"] = tusks
    return tusks


# ------------------------------------------------------------------ clothes
def _cloth_from_body(body, name, face_pred, offset, thickness, jag=None, piece=0):
    """Copy body faces selected by face_pred(centroid) -> offset shell with rag edge."""
    import bmesh
    ob = body.copy()
    ob.data = body.data.copy()
    ob.name = name
    ob.data.name = name
    ob.data.materials.clear()
    for m in list(ob.modifiers):
        ob.modifiers.remove(m)
    bpy.context.collection.objects.link(ob)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        keep = face_pred(np.array(c))
        if keep and jag is not None:
            keep = jag(np.array(c))
        if not keep:
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * offset
    bm.to_mesh(ob.data)
    bm.free()
    m = ob.modifiers.new("solid", "SOLIDIFY")
    m.thickness = thickness
    m.offset = 1.0
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier="solid")
    a = ob.data.attributes.new("piece", "INT", "FACE")
    a.data.foreach_set("value", [piece] * len(ob.data.polygons))
    return ob


def _bone(rig, name):
    return rig.data.bones[name]


def build_clothes(rig, meshes):
    body = meshes["goblin_body"]
    hips = _bone(rig, "Hips").head_local
    knee = _bone(rig, "LeftLeg").head_local
    neck = _bone(rig, "Neck").head_local
    elbow = _bone(rig, "LeftForeArm").head_local
    shoulder = _bone(rig, "LeftArm").head_local
    hz = hips.z

    def rag_edge(z0, amp, freq, seed):
        def f(c):
            a = math.atan2(c[1], c[0] if abs(c[0]) > 1e-6 else 1e-6)
            n = math.sin(a * freq + seed) * 0.5 + math.sin(a * freq * 2.7 + seed * 3) * 0.3 \
                + math.sin(c[0] * 90 + seed) * 0.2
            return c[2] > z0 + amp * n
        return f

    # ---- illusion: ragged loin wrap, belt, chest strap
    leg_x = abs(_bone(rig, "LeftUpLeg").head_local.x)
    wrap = _cloth_from_body(
        body, "goblin_rags", lambda c: hz - 0.17 < c[2] < hz + 0.07 and abs(c[0]) < leg_x + 0.08,
        0.006, 0.003, jag=rag_edge(hz - 0.12, 0.035, 5, 1.3))
    belt = _cloth_from_body(body, "goblin_belt_i", lambda c: hz + 0.045 < c[2] < hz + 0.075, 0.010, 0.004, piece=1)
    p0 = np.array([shoulder.x * 0.9, shoulder.y, shoulder.z + 0.01])
    p1 = np.array([-leg_x - 0.03, hips.y, hz + 0.06])
    axis = (p1 - p0) / np.linalg.norm(p1 - p0)
    nrm = np.cross(axis, [0, 1, 0])
    nrm /= np.linalg.norm(nrm)
    strap = _cloth_from_body(
        body, "goblin_strap",
        lambda c: abs(np.dot(c - p0, nrm)) < 0.016 and hz + 0.05 < c[2] < shoulder.z + 0.03
        and abs(c[0]) < shoulder.x + 0.02, 0.008, 0.004, piece=1)
    rags = _join([wrap, belt, strap], "goblin_clothes_illusion")

    # ---- truth: linen shirt with short sleeves, trousers, rope belt
    def shirt(c):
        if c[2] > neck.z - 0.005 or c[2] < hz - 0.05:
            return False
        if abs(c[0]) > shoulder.x - 0.005:   # sleeve: along the upper arm until 60 % to the elbow
            t = (abs(c[0]) - shoulder.x) / (abs(elbow.x) - shoulder.x)
            return t < 0.6 and c[2] > elbow.z - 0.05
        return True
    shirt_ob = _cloth_from_body(body, "goblin_shirt", shirt, 0.009, 0.003,
                                jag=rag_edge(hz - 0.04, 0.006, 3, 0.4))
    trousers = _cloth_from_body(body, "goblin_trousers",
                                lambda c: knee.z - 0.06 < c[2] < hz + 0.04 and abs(c[0]) < leg_x + 0.09,
                                0.006, 0.003, jag=rag_edge(knee.z - 0.05, 0.01, 4, 2.0), piece=1)
    rope = _cloth_from_body(body, "goblin_rope", lambda c: hz + 0.015 < c[2] < hz + 0.035, 0.016, 0.005, piece=2)
    truth = _join([shirt_ob, trousers, rope], "goblin_clothes_truth")

    for ob in (rags, truth):
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.pack_islands(margin=0.004, rotate=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        ob.parent = rig
        for m in list(ob.modifiers):
            ob.modifiers.remove(m)
        m = ob.modifiers.new("rig", "ARMATURE")
        m.object = rig
    meshes["goblin_clothes_illusion"] = rags
    meshes["goblin_clothes_truth"] = truth
    return rags, truth


def _join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.active_object
    ob.name = name
    ob.data.name = name
    return ob
