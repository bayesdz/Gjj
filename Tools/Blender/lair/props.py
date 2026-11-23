"""Hand-built (procedural) props for the lair: timber supports, torches, straw beds, bones,
cage with door, the Shining One, wall drawings. All placed directly in level space."""
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

from goblin.details import fbm

R = random.Random(42)


def _obj(name, bm, mat, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob


def _box_uv(ob, tile):
    """World-scale box UV for a placed prop."""
    me = ob.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uvl = me.uv_layers[0]
    mw = ob.matrix_world
    for p in me.polygons:
        n = p.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in p.loop_indices:
            co = mw @ me.vertices[me.loops[li].vertex_index].co
            u, v = [(co.y, co.z), (co.x, co.z), (co.x, co.y)][ax]
            uvl.data[li].uv = (u / tile, v / tile)


def log(name, a, b, r, mat, tile=1.5, segs=10):
    """Rough wooden log between points a and b (slightly irregular)."""
    a, b = Vector(a), Vector(b)
    bm = bmesh.new()
    axis = (b - a)
    L = axis.length
    rot = axis.to_track_quat("Z", "Y").to_matrix().to_4x4()
    rings = max(2, int(L / 0.25))
    verts = []
    for i in range(rings + 1):
        t = i / rings
        row = []
        for j in range(segs):
            ang = 2 * math.pi * j / segs
            rr = r * (1 + 0.08 * math.sin(ang * 3 + i) + 0.05 * R.uniform(-1, 1))
            p = Vector((math.cos(ang) * rr, math.sin(ang) * rr, t * L))
            row.append(bm.verts.new(a + rot @ p))
        verts.append(row)
    for i in range(rings):
        for j in range(segs):
            k = (j + 1) % segs
            bm.faces.new((verts[i][j], verts[i][k], verts[i + 1][k], verts[i + 1][j]))
    bm.faces.new(verts[0][::-1])
    bm.faces.new(verts[-1])
    ob = _obj(name, bm, mat)
    _box_uv(ob, tile)
    return ob


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.active_object
    ob.name = ob.data.name = name
    return ob


# ---------------------------------------------------------------- tunnel support
def support(name, ray, center, tangent, floor_z, mats):
    """Mine-style timber frame (two posts + cap) fitted to the tunnel by raycasts."""
    t = Vector(tangent).normalized()
    side = Vector((-t.y, t.x, 0)).normalized()
    c = Vector(center)
    h = 1.0
    hl = ray.cast((c.x, c.y, floor_z + h), -side, 6)[0]
    hr = ray.cast((c.x, c.y, floor_z + h), side, 6)[0]
    up = ray.cast((c.x, c.y, floor_z + 1.0), (0, 0, 1), 8)[0]
    if hl is None or hr is None or up is None:
        return None
    width = (hr - hl).length
    if width > 4.2:
        return None
    top = min(up.z - 0.15, floor_z + 2.5)
    pl = hl + side * 0.28
    pr = hr - side * 0.28
    lean = R.uniform(-0.05, 0.05)
    parts = [
        log(name + "_l", (pl.x, pl.y, floor_z - 0.2), (pl.x + side.x * 0.08, pl.y + side.y * 0.08 + lean, top), 0.11, mats["Lair_Wood"]),
        log(name + "_r", (pr.x, pr.y, floor_z - 0.2), (pr.x - side.x * 0.08, pr.y - side.y * 0.08 + lean, top), 0.11, mats["Lair_Wood"]),
        log(name + "_cap", (pl.x - side.x * 0.15, pl.y - side.y * 0.15, top - 0.05),
            (pr.x + side.x * 0.15, pr.y + side.y * 0.15, top + R.uniform(-0.08, 0.08)), 0.12, mats["Lair_Wood"]),
    ]
    return join(parts, name)


# ---------------------------------------------------------------- torch
def torch(name, pos, mats):
    """Wall torch: stick in an iron ring; flame/light are added in Unity at the top."""
    p = Vector(pos)
    stick = log(name + "_s", p + Vector((0, 0, -0.35)), p + Vector((0, 0, 0.25)), 0.028, mats["Lair_Wood"], 0.5, 8)
    head = log(name + "_h", p + Vector((0, 0, 0.18)), p + Vector((0, 0, 0.36)), 0.05, mats["Lair_Cloth"], 0.3, 10)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=False, segments=12, radius1=0.045, radius2=0.045, depth=0.05)
    ring = _obj(name + "_ring", bm, mats["Lair_Rust"])
    ring.location = p
    _box_uv(ring, 0.3)
    return join([stick, head, ring], name)


# ---------------------------------------------------------------- straw bed / blanket
def mound(name, center, size, height, mat, tile, seed=0, flat_top=False):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=1.0)
    rng = np.random.default_rng(seed)
    off = rng.normal(size=3) * 10
    for v in bm.verts:
        co = np.array(v.co[:])
        n = fbm((co + off)[None] * 2.0, 3)[0]
        v.co.x *= size[0] * (1 + 0.15 * n)
        v.co.y *= size[1] * (1 + 0.15 * n)
        z = v.co.z
        v.co.z = (max(z, -0.05) * height) * (1 + 0.25 * n)
        if flat_top:
            v.co.z = min(v.co.z, height * 0.75)
    ob = _obj(name, bm, mat)
    ob.location = center
    bpy.context.view_layer.update()
    _box_uv(ob, tile)
    return ob


# ---------------------------------------------------------------- bones
def bone(name, a, b, r, mat):
    a, b = Vector(a), Vector(b)
    shaft = log(name + "_s", a, b, r, mat, 0.4, 8)
    parts = [shaft]
    for e in (a, b):
        for k in (-1, 1):
            bm = bmesh.new()
            bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=r * 1.6)
            s = _obj(name + "_k", bm, mat)
            side = (b - a).cross(Vector((0, 0, 1)))
            side = side.normalized() if side.length > 1e-6 else Vector((1, 0, 0))
            s.location = e + side * r * 0.9 * k
            parts.append(s)
    for p in parts[1:]:
        _box_uv(p, 0.4)
    return join(parts, name)


def bone_pile(name, center, ray, count, mat, spread=0.6, seed=0):
    rr = random.Random(seed)
    c = Vector(center)
    parts = []
    for i in range(count):
        p = c + Vector((rr.uniform(-spread, spread), rr.uniform(-spread, spread), 0))
        hit = ray.floor(p.x, p.y, c.z)[0]
        z = (hit.z if hit else c.z) + 0.03 + rr.uniform(0, 0.08) * (i / count)
        L = rr.uniform(0.18, 0.38)
        ang = rr.uniform(0, math.pi)
        d = Vector((math.cos(ang), math.sin(ang), rr.uniform(-0.15, 0.15))) * L / 2
        parts.append(bone(f"{name}_{i}", Vector((p.x, p.y, z)) - d, Vector((p.x, p.y, z)) + d,
                          rr.uniform(0.010, 0.016), mat))
    return join(parts, name)


# ---------------------------------------------------------------- cage
def cage(name, center, floor_z, yaw, mats):
    """Wooden cage 1.8 x 1.6 x 2.0 m with rusty bands. Returns (cage, door)."""
    c = Vector((center[0], center[1], floor_z))
    rot = Matrix.Rotation(yaw, 4, "Z")
    W, D, H = 1.8, 1.6, 2.0
    parts = []

    def P(x, y, z):
        return c + rot @ Vector((x, y, z))
    corners = [(-W / 2, -D / 2), (W / 2, -D / 2), (W / 2, D / 2), (-W / 2, D / 2)]
    for i, (x, y) in enumerate(corners):
        parts.append(log(f"{name}_post{i}", P(x, y, -0.15), P(x, y, H + 0.1), 0.07, mats["Lair_Wood"]))
    for z in (0.08, H):
        for i in range(4):
            x0, y0 = corners[i]
            x1, y1 = corners[(i + 1) % 4]
            parts.append(log(f"{name}_rail{i}_{z}", P(x0, y0, z), P(x1, y1, z), 0.05, mats["Lair_Wood"]))
    # bars on three sides; the front (-y) side is the door
    for i in range(1, 4):
        x0, y0 = corners[i]
        x1, y1 = corners[(i + 1) % 4]
        n = 6
        for k in range(1, n):
            t = k / n
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            parts.append(log(f"{name}_bar{i}_{k}", P(x, y, 0.05), P(x, y, H), 0.025, mats["Lair_Wood"], 0.8, 6))
    # roof planks
    for k in range(7):
        y = -D / 2 + D * (k + 0.5) / 7
        parts.append(log(f"{name}_roof{k}", P(-W / 2 - 0.1, y, H + 0.12), P(W / 2 + 0.1, y, H + 0.12), 0.06, mats["Lair_Wood"]))
    body = join(parts, name)
    # door: hinge on the left front post; origin at the hinge so Unity can rotate it
    dparts = []
    hx, hy = -W / 2 + 0.08, -D / 2
    for k in range(1, 6):
        x = hx + (W - 0.16) * k / 6
        dparts.append(log(f"{name}_dbar{k}", P(x, hy, 0.12), P(x, hy, H - 0.08), 0.025, mats["Lair_Wood"], 0.8, 6))
    for z in (0.3, H - 0.3):
        dparts.append(log(f"{name}_dr{z}", P(hx, hy, z), P(W / 2 - 0.08, hy, z), 0.04, mats["Lair_Rust"], 0.5, 8))
    door = join(dparts, name + "_Door")
    hinge = P(hx, hy, 0)
    door.data.transform(Matrix.Translation(-hinge))
    door.location = hinge
    return body, door


# ---------------------------------------------------------------- the Shining One
def shining_one(name, pos, yaw):
    """Kneeling radiant figure from the MakeHuman CC0 base (no rig, static pose)."""
    from goblin import goblin, mh, pose
    base = mh.Base.load()
    for rel, w in (("macrodetails/universal-female-young-averagemuscle-averageweight", 1.0),):
        base.apply_target(rel, w)
    skel = mh.load_skeleton()
    weights = mh.load_weights()
    rig = mh.build_armature(base, skel, name + "_rig")
    body, _ = base.make_object(name, ["body"], weights)
    m = body.modifiers.new("rig", "ARMATURE")
    m.object = rig
    goblin.rename_bones(rig)
    pose.reset(rig)
    # kneeling, head bowed, hands clasped in front of the chest
    for s, side in ((1, "Left"), (-1, "Right")):
        pose.aim(rig, f"{side}UpLeg", (0.10 * s, -0.25, -0.97))
        pose.aim(rig, f"{side}Leg", (0.0, 0.99, -0.12))
        pose.aim(rig, f"{side}Foot", (0.0, 0.95, -0.3))
        pose.aim(rig, f"{side}Arm", (0.25 * s, -0.25, -0.93))
        pose.aim(rig, f"{side}ForeArm", (-0.55 * s, -0.75, 0.35))
    pose.aim(rig, "Spine1", (0, -0.15, 1))
    pose.aim(rig, "Neck", (0, -0.35, 0.94))
    pose.aim(rig, "Head", (0, -0.55, 0.84))
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.modifier_apply(modifier="rig")
    body.vertex_groups.clear()
    bpy.data.objects.remove(rig)
    if "mh_index" in body.data.attributes:
        body.data.attributes.remove(body.data.attributes["mh_index"])
    # put on the floor at pos, scale to 1.7 m standing height (MakeHuman is ~1.7 already)
    co = np.array([v.co[:] for v in body.data.vertices])
    body.data.transform(Matrix.Translation((0, 0, -co[:, 2].min())))
    body.data.transform(Matrix.Rotation(yaw, 4, "Z"))
    body.location = pos
    mat = bpy.data.materials.get("Lair_Shining") or bpy.data.materials.new("Lair_Shining")
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (1.0, 0.92, 0.75, 1)
    b.inputs["Emission Color"].default_value = (1.0, 0.85, 0.55, 1)
    b.inputs["Emission Strength"].default_value = 4.0
    b.inputs["Roughness"].default_value = 0.3
    body.data.materials.clear()
    body.data.materials.append(mat)
    return body


# ---------------------------------------------------------------- wall drawings
def paint_drawing(path, truth, res=1024):
    """Charcoal family drawing (truth) or blood-red cult runes (illusion) on a stone slab."""
    from PIL import Image, ImageDraw, ImageFilter
    rng = random.Random(7)
    img = Image.new("RGBA", (res, int(res * 0.75)), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    W, H = img.size
    if truth:
        ink = (25, 20, 18, 235)
        # fire in the middle
        fx, fy = W // 2, int(H * 0.72)
        for k in range(9):
            a = math.pi * (0.15 + 0.7 * k / 8)
            d.line([(fx, fy), (fx + math.cos(a) * 60, fy - math.sin(a) * 90)], fill=(140, 60, 20, 230), width=9)
        d.line([(fx - 80, fy + 10), (fx + 80, fy + 10)], fill=ink, width=10)
        # family: two big goblins, three small ones, holding hands (pointed ears!)
        xs = [W * 0.16, W * 0.30, W * 0.66, W * 0.79, W * 0.90]
        sizes = [1.0, 0.95, 0.55, 0.5, 0.42]
        for x, s in zip(xs, sizes):
            hgt = H * 0.55 * s
            top = fy + 20 - hgt
            r = 34 * s + 10
            d.ellipse([x - r, top, x + r, top + 2 * r], outline=ink, width=8)
            d.polygon([(x - r, top + r * 0.8), (x - r * 2.6, top - r * 0.6), (x - r * 0.7, top + r * 0.4)], outline=ink, width=7)
            d.polygon([(x + r, top + r * 0.8), (x + r * 2.6, top - r * 0.6), (x + r * 0.7, top + r * 0.4)], outline=ink, width=7)
            d.ellipse([x - r * 0.4, top + r * 0.7, x - r * 0.15, top + r * 0.95], fill=ink)
            d.ellipse([x + r * 0.15, top + r * 0.7, x + r * 0.4, top + r * 0.95], fill=ink)
            d.arc([x - r * 0.5, top + r * 1.0, x + r * 0.5, top + r * 1.6], 20, 160, fill=ink, width=5)
            body_top = top + 2 * r
            d.line([(x, body_top), (x, fy - 10)], fill=ink, width=9)
            d.line([(x, fy - 10), (x - 30 * s, fy + 20)], fill=ink, width=8)
            d.line([(x, fy - 10), (x + 30 * s, fy + 20)], fill=ink, width=8)
            d.line([(x - 55 * s, body_top + 40 * s), (x + 55 * s, body_top + 40 * s)], fill=ink, width=8)
        # sun / moon and hand prints
        d.ellipse([W * 0.06, H * 0.06, W * 0.15, H * 0.18], outline=ink, width=7)
        for k in range(3):
            x, y = W * (0.45 + 0.1 * k), H * 0.12
            d.ellipse([x, y, x + 40, y + 50], fill=(110, 70, 40, 160))
    else:
        ink = (120, 8, 4, 235)
        for k in range(7):
            x = W * (0.1 + 0.13 * k) + rng.uniform(-15, 15)
            y = H * 0.25 + rng.uniform(-30, 30)
            d.line([(x, y), (x, y + 260)], fill=ink, width=12)
            for j in range(3):
                yy = y + 50 + j * 70
                d.line([(x, yy), (x + rng.choice([-50, 50]), yy + rng.uniform(-40, 40))], fill=ink, width=10)
        d.ellipse([W * 0.38, H * 0.1, W * 0.62, H * 0.9], outline=ink, width=14)
        d.line([(W * 0.38, H * 0.5), (W * 0.62, H * 0.5)], fill=ink, width=10)
        for k in range(30):  # drips
            x = rng.uniform(W * 0.05, W * 0.95)
            y = rng.uniform(H * 0.2, H * 0.6)
            d.line([(x, y), (x, y + rng.uniform(30, 150))], fill=(90, 5, 3, 200), width=rng.randint(3, 7))
    img = img.filter(ImageFilter.GaussianBlur(1.6))
    # rough edges / chalky breaks
    a = np.array(img).astype(np.float32)
    noise = np.random.default_rng(3).random(a.shape[:2])
    a[..., 3] *= (noise > 0.18)
    Image.fromarray(a.astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)).save(path)
    return path


def drawing(name, ray, origin, direction, size, tex_path):
    """Decal quad on the wall where the ray hits."""
    hit, n, _, _ = ray.cast(origin, direction, 10)
    if hit is None:
        return None
    n = Vector(n)
    up = Vector((0, 0, 1))
    right = up.cross(n).normalized()
    up = n.cross(right).normalized()
    c = hit + n * 0.04
    w, h = size
    bm = bmesh.new()
    vs = [bm.verts.new(c + right * x * w / 2 + up * y * h / 2) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    f = bm.faces.new(vs)
    uvl = bm.loops.layers.uv.new("UVMap")
    for loop, uv in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        loop[uvl].uv = uv
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    im = nt.nodes.new("ShaderNodeTexImage")
    im.image = bpy.data.images.load(tex_path, check_existing=True)
    nt.links.new(im.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(im.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.9
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    return _obj(name, bm, mat, smooth=False)
