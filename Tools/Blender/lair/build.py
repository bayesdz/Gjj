"""Floor 1 - the Goblin Lair. Builds the whole level in Blender and exports it for Unity.

    cd Tools/Blender
    python -m lair.build --out /tmp/lair --unity ../../Assets/_Game/Art/Levels/GoblinLair

Naming contract with Assets/_Game/Editor/LairBuilder.cs:
  Lair_Cave            cave mesh (rock + mud), mesh collider
  S_*  / I_*  / T_*    static props: both looks / illusion only / truth only
  Cage_Door, Shining_One, S_Cage
  Spawn_Player, Look_Player, Goblin_NN, Item_Toy, Item_Drawing, Exit,
  Zone__<narration key>__<sx>x<sy>x<sz>, Light_<kind>_NN   (empties)
"""
import argparse
import json
import math
import os
import random
import shutil
import sys
import time

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from lair import assets, cave, polyhaven, props, scene  # noqa: E402

T0 = time.time()
RNG = random.Random(11)


def log(*a):
    print(f"[{time.time() - T0:6.1f}s]", *a, flush=True)


# ---------------------------------------------------------------- Poly Haven models
TEMPLATES = {}
TRI_BUDGET = {"root_cluster_01": 6000, "root_cluster_02": 6000, "namaqualand_boulder_05": 1200,
              "namaqualand_boulder_02": 5000, "boulder_01": 5000, "rock_moss_set_01": 7000,
              "rock_moss_set_02": 7000, "treasure_chest": 9000, "spinning_wheel_01": 9000,
              "tree_stump_01": 5000, "dry_branches_medium_01": 3000}


def load_model(name):
    if name in TEMPLATES:
        return TEMPLATES[name]
    before = {o.name for o in bpy.data.objects}
    bpy.ops.import_scene.gltf(filepath=polyhaven.model(name))
    new = [o for o in bpy.data.objects if o.name not in before]
    new_names = [o.name for o in new]
    meshes = [o for o in new if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    ob = bpy.context.active_object
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    budget = TRI_BUDGET.get(name, 8000)
    if tris > budget:  # game-res LOD; the scanned detail stays in the normal map
        mod = ob.modifiers.new("dec", "DECIMATE")
        mod.ratio = budget / tris
        bpy.ops.object.modifier_apply(modifier="dec")
    for n in new_names:
        o = bpy.data.objects.get(n)
        if o is not None and o != ob:
            bpy.data.objects.remove(o)
    # origin at bottom centre
    co = np.array([v.co[:] for v in ob.data.vertices])
    lo, hi = co.min(0), co.max(0)
    from mathutils import Matrix
    ob.data.transform(Matrix.Translation((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2])))
    ob.location = (0, 0, -100)
    ob.name = "TPL_" + name
    ob.hide_render = True
    TEMPLATES[name] = ob
    return ob


PLACED = []


def place(model, layer, x, y, z_hint, yaw=None, scale=1.0, mode="floor", ray=None, z_off=0.0, name=None):
    tpl = load_model(model)
    if mode == "floor":
        hit, n = ray.floor(x, y, z_hint)
        z = (hit.z if hit else z_hint) + z_off
    else:  # ceiling
        hit = ray.cast((x, y, z_hint), (0, 0, 1), 15)[0]
        if hit is None:
            return None
        z = hit.z + 0.05
    ob = tpl.copy()
    ob.data = tpl.data
    prefix = {"both": "S", "illusion": "I", "truth": "T"}[layer]
    ob.name = f"{prefix}_{model}_{len(PLACED):03d}"
    ob["model"] = model
    ob.location = (x, y, z)
    ob.rotation_euler = (math.pi if mode == "ceiling" else 0, 0, RNG.uniform(0, 2 * math.pi) if yaw is None else yaw)
    ob.scale = (scale,) * 3
    ob.hide_render = False
    bpy.context.collection.objects.link(ob)
    PLACED.append(ob)
    return ob


def empty(name, loc, size=0.3):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = size
    e.location = loc
    bpy.context.collection.objects.link(e)
    return e


def floor_at(ray, x, y, hint):
    hit = ray.floor(x, y, hint)[0]
    return hit.z if hit else hint


# ---------------------------------------------------------------- level dressing
def wall_scatter(ray, node, count, models, seed, z_up=0.4, inset=0.35, scale=(0.22, 0.42)):
    """Rocks at the foot of a chamber's walls (hides the floor/wall seam, like real caves)."""
    rr = random.Random(seed)
    x, y, fz, w, h = cave.NODES[node]
    for i in range(count):
        a = 2 * math.pi * (i + rr.uniform(-0.3, 0.3)) / count
        d = Vector((math.cos(a), math.sin(a), 0))
        hit = ray.cast((x, y, fz + z_up), d, 30)[0]
        # skip openings: a ray that runs far away went down a tunnel
        if hit is None or (hit.xy - Vector((x, y))).length > w * 1.35:
            continue
        p = hit - d * inset
        place(rr.choice(models), "both", p.x, p.y, fz, scale=rr.uniform(*scale), ray=ray, z_off=-0.12)


def floor_clutter(ray, node, count, seed, radius, keep_clear=1.2):
    """Pebbles and twigs on the floor so it never looks swept (keeps the centre walkable)."""
    rr = random.Random(seed)
    x, y, fz, w, h = cave.NODES[node]
    for i in range(count):
        a = rr.uniform(0, 2 * math.pi)
        r = rr.uniform(keep_clear, radius)
        px, py = x + math.cos(a) * r, y + math.sin(a) * r
        m = rr.choice(["namaqualand_boulder_05", "namaqualand_boulder_05", "dry_branches_medium_01"])
        s = rr.uniform(0.08, 0.2) if "boulder" in m else rr.uniform(0.4, 0.7)
        o = place(m, "both", px, py, fz, scale=s, ray=ray, z_off=-0.02)
        if o and "branches" in m:
            o.rotation_euler = (math.pi / 2, 0, rr.uniform(0, 2 * math.pi))  # lying on the ground


def ceiling_roots(ray, node, count, seed, spread):
    rr = random.Random(seed)
    x, y, fz, w, h = cave.NODES[node]
    for i in range(count):
        place(rr.choice(["root_cluster_01", "root_cluster_02"]), "both",
              x + rr.uniform(-spread, spread), y + rr.uniform(-spread, spread), fz + 1.5,
              mode="ceiling", ray=ray, scale=rr.uniform(0.3, 0.5))


def tunnel_supports(ray, mats):
    """Timber frames every ~6 m along tunnels; every other one carries a torch."""
    n = 0
    torches = []
    for a, b in cave.EDGES:
        A, B = Vector(cave.NODES[a][:3]), Vector(cave.NODES[b][:3])
        L = (B.xy - A.xy).length
        steps = int(L / 6.0)
        for k in range(1, steps + 1):
            t = k / (steps + 1)
            p = A.lerp(B, t)
            # skip the inside of chambers
            if any((p.xy - Vector(cave.NODES[c][:2])).length < cave.NODES[c][3] * 0.9 for c in cave.CHAMBERS):
                continue
            fz = floor_at(ray, p.x, p.y, p.z)
            s = props.support(f"S_Support_{n:02d}", ray, (p.x, p.y, fz), B - A, fz, mats)
            if s is None:
                continue
            if n % 2 == 0:
                tan = (B - A).normalized()
                side = Vector((-tan.y, tan.x, 0)).normalized() * (1 if (n // 2) % 2 else -1)
                hit = ray.cast((p.x, p.y, fz + 1.7), side, 6)[0]
                if hit:
                    tp = hit - side * 0.45 + tan * 0.25
                    torches.append(tp)
            n += 1
    return torches


def wall_torch(ray, node, angle_deg, z=1.75):
    x, y, fz, w, h = cave.NODES[node]
    a = math.radians(angle_deg)
    d = Vector((math.cos(a), math.sin(a), 0))
    hit = ray.cast((x, y, fz + z), d, 30)[0]
    return hit - d * 0.22 if hit else None


def build_level(ray, mats):
    N = cave.NODES
    bone_mat = bpy.data.materials.get("Lair_Bone") or bpy.data.materials.new("Lair_Bone")
    bone_mat.use_nodes = True
    bone_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.30, 0.26, 0.19, 1)
    bone_mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.7

    # --- A: entrance tunnel. Collapsed rock behind the player.
    x, y, fz, _, _ = N["A0"]
    for i, (dx, dy, s) in enumerate([(-0.8, -1.3, 1.3), (0.7, -1.6, 1.5), (0.0, -2.2, 1.8), (1.2, -0.6, 0.8)]):
        place(RNG.choice(["boulder_01", "namaqualand_boulder_02", "namaqualand_boulder_05"]), "both",
              x + dx, y + dy, fz, scale=s * 0.7, ray=ray, z_off=-0.2)
    empty("Spawn_Player", (x, y + 1.2, floor_at(ray, x, y + 1.2, fz) + 1.0))
    empty("Look_Player", (N["A1"][0], N["A1"][1], N["A1"][2] + 1.6))
    place("dry_branches_medium_01", "both", x - 0.6, y + 3.5, fz, ray=ray)
    place("wooden_bucket_02", "both", x + 0.9, y + 6.0, fz, ray=ray)

    torches = tunnel_supports(ray, mats)

    # --- B: guard chamber, first goblin
    x, y, fz, w, h = N["B"]
    place("wooden_crate_01", "both", x + 3.0, y + 2.2, fz, yaw=0.3, ray=ray)
    place("wooden_crate_02", "both", x + 3.4, y + 1.0, fz, yaw=1.1, ray=ray)
    place("wooden_barrels_01", "both", x + 3.6, y - 1.6, fz, ray=ray)
    place("wooden_stool_02", "both", x - 0.8, y + 1.6, fz, ray=ray)
    lan = place("wooden_lantern_01", "both", x + 3.0, y + 2.2, fz, ray=ray)
    lan.location.z += 0.6
    empty("Light_Lantern_00", lan.location + Vector((0, 0, 0.25)))
    place("wooden_bucket_01", "both", x - 3.0, y + 2.3, fz, ray=ray)
    props.bone_pile("I_Bones_B", (x - 2.6, y - 2.2, fz), ray, 9, bone_mat, 0.5, 1)
    place("wicker_basket_02", "truth", x - 2.6, y - 2.2, fz, ray=ray)
    place("wooden_bowl_02", "truth", x - 2.0, y - 1.6, fz, ray=ray)
    place("hatchet", "illusion", x - 0.8, y + 1.9, fz, ray=ray, z_off=0.45)
    empty("Goblin_01", (x + 0.5, y + 1.0, floor_at(ray, x + 0.5, y + 1.0, fz)))
    wall_scatter(ray, "B", 16, ["rock_moss_set_01", "rock_moss_set_02", "boulder_01"], 2)
    ceiling_roots(ray, "B", 4, 3, 3.5)
    for ang in (100, 280):
        t = wall_torch(ray, "B", ang)
        if t:
            torches.append(t)

    # --- D: main hall
    x, y, fz, w, h = N["D"]
    fire = place("stone_fire_pit", "both", x, y, fz, yaw=0.0, ray=ray)
    empty("Light_Fire_00", fire.location + Vector((0, 0, 0.45)))
    place("dry_branches_medium_01", "both", x + 1.4, y - 2.4, fz, ray=ray)
    place("tree_stump_01", "both", x + 1.9, y + 1.3, fz, ray=ray, scale=0.4)
    place("wooden_stool_02", "both", x - 1.8, y + 1.4, fz, ray=ray)
    place("wooden_stool_02", "both", x + 1.4, y - 1.9, fz, ray=ray)
    # sleeping place (west)
    beds = [(-6.0, -3.0, 0.3), (-6.6, 0.6, -0.2), (-6.0, 4.0, 0.4), (-4.4, 6.6, 1.2)]
    for i, (dx, dy, yaw) in enumerate(beds):
        bz = floor_at(ray, x + dx, y + dy, fz)
        props.mound(f"S_Bed_{i}", (x + dx, y + dy, bz - 0.05), (1.05, 0.65), 0.28, mats["Lair_Straw"], 1.2, seed=i)
        if i < 3:  # truth: woven blankets on the beds
            props.mound(f"T_Blanket_{i}", (x + dx + 0.15, y + dy, bz + 0.12), (0.75, 0.6), 0.2,
                        mats["Lair_Cloth"], 0.8, seed=10 + i, flat_top=True)
    props.bone_pile("I_Bones_D1", (x - 5.0, y - 1.3, fz), ray, 14, bone_mat, 0.6, 2)
    props.bone_pile("I_Bones_D2", (x - 3.8, y + 3.0, fz), ray, 10, bone_mat, 0.5, 3)
    place("wicker_basket_01", "truth", x - 4.6, y - 1.2, fz, ray=ray)
    # the toy (item 1) next to the smallest bed
    tz = floor_at(ray, x - 4.7, y + 2.0, fz)
    place("carved_wooden_elephant", "truth", x - 4.7, y + 2.0, fz, yaw=1.0, ray=ray, scale=1.0)
    props.bone_pile("I_Toy", (x - 4.7, y + 2.0, tz), ray, 5, bone_mat, 0.15, 4)
    empty("Item_Toy", (x - 4.7, y + 2.0, tz + 0.15))
    # the family drawing (item 2) on the west wall
    dpath = os.path.join(TEX_OUT, "Lair_Drawing_Truth.png")
    ipath = os.path.join(TEX_OUT, "Lair_Drawing_Illusion.png")
    props.paint_drawing(dpath, True)
    props.paint_drawing(ipath, False)
    origin = (x - 3.0, y + 2.5, fz + 1.45)
    props.drawing("T_Drawing", ray, origin, (-1, 0.15, 0), (1.4, 1.05), dpath)
    props.drawing("I_Drawing", ray, origin, (-1, 0.15, 0), (1.4, 1.05), ipath)
    hit = ray.cast(origin, (-1, 0.15, 0), 10)[0]
    empty("Item_Drawing", hit + Vector((0.35, 0, 0)) if hit else origin)
    # kitchen corner (north)
    place("wooden_crate_02", "both", x + 1.0, y + 7.5, fz, yaw=0.1, ray=ray)
    top = floor_at(ray, x + 1.0, y + 7.5, fz) + 0.62
    for m, dx, dy in (("wooden_bowl_01", -0.2, 0.1), ("jug_01", 0.25, -0.15), ("yellow_onion", 0.05, -0.25),
                      ("sweet_potato", -0.25, -0.2), ("wooden_cutting_board", 0.0, 0.2), ("wooden_spoon", 0.2, 0.25)):
        o = place(m, "truth", x + 1.0 + dx, y + 7.5 + dy, fz, ray=ray)
        o.location.z = top
    for m, dx, dy in (("machete", 0.0, 0.0), ("hatchet", 0.2, -0.2)):
        o = place(m, "illusion", x + 1.0 + dx, y + 7.5 + dy, fz, ray=ray)
        o.location.z = top
    place("spinning_wheel_01", "truth", x - 1.6, y + 8.0, fz, yaw=2.6, ray=ray)
    place("ceramic_pot", "truth", x + 2.4, y + 7.0, fz, ray=ray)
    place("treasure_chest", "illusion", x - 1.6, y + 8.0, fz, yaw=2.6, ray=ray)
    place("wooden_axe", "illusion", x + 2.4, y + 7.0, fz, ray=ray, z_off=0.02)
    # storage (east)
    place("wooden_barrels_01", "both", x + 6.5, y - 3.0, fz, ray=ray)
    place("wooden_crate_01", "both", x + 7.2, y - 0.4, fz, yaw=0.2, ray=ray)
    c2 = place("wooden_crate_02", "both", x + 7.2, y - 0.4, fz, yaw=0.9, ray=ray)
    c2.location.z += 0.62
    place("wooden_crate_02", "both", x + 6.4, y + 1.5, fz, yaw=0.5, ray=ray)
    place("wooden_bucket_01", "both", x + 4.6, y - 4.6, fz, ray=ray)
    place("wooden_bucket_02", "both", x + 5.2, y + 3.6, fz, ray=ray)
    lan = place("wooden_lantern_01", "both", x + 6.4, y + 1.5, fz, ray=ray)
    lan.location.z += 0.62
    empty("Light_Lantern_01", lan.location + Vector((0, 0, 0.25)))
    empty("Goblin_02", (x + 2.2, y - 2.2, floor_at(ray, x + 2.2, y - 2.2, fz)))
    empty("Goblin_03", (x + 5.0, y + 1.0, floor_at(ray, x + 5.0, y + 1.0, fz)))
    floor_clutter(ray, "D", 40, 21, 9.0, keep_clear=2.5)
    floor_clutter(ray, "B", 18, 22, 5.0)
    for n in ("A1", "A2", "C1", "C2", "F1", "F2"):
        floor_clutter(ray, n, 6, hash(n) % 100, 1.4, keep_clear=0.8)
    wall_scatter(ray, "D", 26, ["rock_moss_set_01", "rock_moss_set_02", "boulder_01", "namaqualand_boulder_02"], 5)
    ceiling_roots(ray, "D", 8, 6, 7.0)
    for ang in (20, 150, 200, 330):
        t = wall_torch(ray, "D", ang)
        if t:
            torches.append(t)

    # --- E: the cage with the Shining One
    x, y, fz, w, h = N["E"]
    cx, cy = x + 0.8, y + 0.3
    cz = floor_at(ray, cx, cy, fz)
    body, door = props.cage("S_Cage", (cx, cy), cz, -math.pi / 2, mats)
    door.name = door.data.name = "Cage_Door"
    sh = props.shining_one("Shining_One", (cx, cy, cz), -math.pi / 2)
    empty("Light_Shining_00", (cx, cy, cz + 1.0))
    empty("Item_Cage", (cx - 1.1, cy, cz + 1.0))
    wall_scatter(ray, "E", 8, ["rock_moss_set_02"], 7, scale=(0.15, 0.25))
    t = wall_torch(ray, "E", 80)
    if t:
        torches.append(t)

    # --- F: exit, deeper down
    x, y, fz, w, h = N["F3"]
    empty("Exit", (x, y, floor_at(ray, x, y, fz) + 1.0))
    place("boulder_01", "both", x + 1.2, y + 1.0, fz, ray=ray, scale=0.6)

    # --- torches
    for i, p in enumerate(torches):
        props.torch(f"S_Torch_{i:02d}", p, mats)
        empty(f"Light_Torch_{i:02d}", p + Vector((0, 0, 0.45)))

    # --- narration zones (Glas). Size in metres.
    def zone(key, node, size, dx=0.0, dy=0.0):
        x, y, fz, _, _ = N[node]
        empty(f"Zone__{key}__{size[0]}x{size[1]}x{size[2]}", (x + dx, y + dy, floor_at(ray, x + dx, y + dy, fz) + 1.5))
    zone("floor1.intro", "A0", (4, 4, 3), dy=1.0)
    zone("floor1.tutorial_move", "A1", (4, 3, 3))
    zone("floor1.tutorial_attack", "A2", (4, 3, 3), dy=4.0)
    zone("floor1.hall", "C2", (4, 3, 3), dy=3.0)
    zone("floor1.cage_seen", "E", (3, 5, 3), dx=-3.5)
    zone("floor1.exit_near", "F2", (4, 3, 3))
    log("level dressed:", len(PLACED), "scanned props,", len(torches), "torches")


# ---------------------------------------------------------------- export
def export_textures(out_dir):
    """Write Unity-ready textures + a manifest: diffuse, GL normal, mask (R metal, G AO, A smooth)."""
    from PIL import Image
    tdir = os.path.join(out_dir, "Textures")
    os.makedirs(tdir, exist_ok=True)
    manifest = {}

    def mask_from_arm(src, dst, sz):
        im = Image.open(src).convert("RGB")
        if max(im.size) > sz:
            im = im.resize((sz, sz), Image.LANCZOS)
        a = np.array(im).astype(np.float32) / 255
        m = np.zeros(a.shape[:2] + (4,), np.float32)
        m[..., 0] = a[..., 2]
        m[..., 1] = a[..., 0]
        m[..., 3] = 1 - a[..., 1]
        Image.fromarray((m * 255).astype(np.uint8), "RGBA").save(dst)

    used = {s.material for o in bpy.data.objects if o.type == "MESH" and not o.name.startswith("TPL_")
            for s in o.material_slots if s.material}
    used |= {s.material for o in TEMPLATES.values() for s in o.material_slots if s.material}
    for m in used:
        name = m.name.replace(".", "_")
        entry = {}
        if "ph_id" in m:  # lair texture sets
            tex = polyhaven.texture(m["ph_id"])
            diff, nor, arm = tex["Diffuse"], tex["nor_gl"], tex["arm"]
            entry["tint"] = list(m["tint"])
            entry["smooth_mul"] = 1.0 / max(m["rough_mul"], 0.1) * 0.8
        else:
            imgs = {}
            if m.use_nodes:
                for n in m.node_tree.nodes:
                    if n.type == "TEX_IMAGE" and n.image and n.image.filepath:
                        imgs[n.image.filepath] = n
            diff = nor = arm = None
            for path in imgs:
                p = bpy.path.abspath(path)
                low = os.path.basename(p).lower()
                if "_diff" in low:
                    diff = p
                elif "_nor" in low:
                    nor = p
                elif "_arm" in low:
                    arm = p
                elif "drawing" in low:
                    diff = p
            b = m.node_tree.nodes.get("Principled BSDF") if m.use_nodes else None
            if b is not None and diff is None:
                entry["color"] = list(b.inputs["Base Color"].default_value)[:3]
                entry["smoothness"] = 1 - b.inputs["Roughness"].default_value
            if b is not None and b.inputs["Emission Strength"].default_value > 0:
                entry["emission"] = list(b.inputs["Emission Color"].default_value)[:3]
                entry["emission_strength"] = b.inputs["Emission Strength"].default_value
        big = name in ("Lair_Rock", "Lair_Mud")
        size = 2048 if big else 1024

        def fit(src, dst, sz):
            im = Image.open(src)
            if max(im.size) > sz:
                im = im.resize((sz, sz), Image.LANCZOS)
            if dst.endswith(".jpg"):
                im.convert("RGB").save(dst, quality=88)
            else:
                im.save(dst)
        if diff:
            ext = os.path.splitext(diff)[1]
            dst = os.path.join(tdir, f"{name}_diff{ext}")
            if os.path.abspath(diff) != os.path.abspath(dst):
                fit(diff, dst, size)
            entry["diff"] = os.path.basename(dst)
            if "drawing" in diff.lower():
                entry["cutout"] = True
        if nor:
            fit(nor, os.path.join(tdir, f"{name}_nor.jpg"), size)
            entry["nor"] = f"{name}_nor.jpg"
        if arm:
            mask_from_arm(arm, os.path.join(tdir, f"{name}_mask.png"), 1024 if big else 512)
            entry["mask"] = f"{name}_mask.png"
        entry["name"] = name
        manifest[name] = entry
        m.name = name
    json.dump({"items": list(manifest.values())}, open(os.path.join(out_dir, "materials.json"), "w"), indent=1)
    log("textures:", len(manifest), "materials")


def export_props(out_dir):
    """One FBX per scanned model (Props/<model>.fbx), origin at the bottom centre."""
    pdir = os.path.join(out_dir, "Props")
    os.makedirs(pdir, exist_ok=True)
    for name, t in TEMPLATES.items():
        t.location = (0, 0, 0)
        t.name = name
        bpy.ops.object.select_all(action="DESELECT")
        t.hide_set(False)
        t.select_set(True)
        bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(pdir, name + ".fbx"), use_selection=True, object_types={"MESH"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
            mesh_smooth_type="FACE", use_tspace=True, bake_anim=False, path_mode="STRIP")
    log("props exported:", len(TEMPLATES))


def export_fbx(out_dir):
    # scanned prop instances -> empties "<L>_P_<model>_<NNN>" (Unity instantiates Props/<model>.fbx)
    for ob in PLACED:
        model = ob["model"]
        e = bpy.data.objects.new(ob.name.replace(f"_{model}_", f"_P_{model}_", 1), None)
        e.matrix_world = ob.matrix_world.copy()
        bpy.context.collection.objects.link(e)
        bpy.data.objects.remove(ob, do_unlink=True)
    for t in TEMPLATES.values():
        bpy.data.objects.remove(t, do_unlink=True)
    for o in bpy.data.objects:
        if o.type in ("LIGHT", "CAMERA"):
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="SELECT")
    path = os.path.join(out_dir, "GoblinLair.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        mesh_smooth_type="FACE", use_tspace=True, use_mesh_modifiers=True, bake_anim=False,
        path_mode="STRIP")
    log("exported", path, f"{os.path.getsize(path) / 1e6:.1f} MB")


# ---------------------------------------------------------------- previews
def previews(out, look, samples):
    for o in bpy.data.objects:
        if o.type != "MESH" or o.name.startswith("TPL_"):
            continue
        if o.name.startswith("I_"):
            o.hide_render = look != "illusion"
        elif o.name.startswith("T_"):
            o.hide_render = look != "truth"
    for o in [o for o in bpy.data.objects if o.type == "LIGHT"]:
        bpy.data.objects.remove(o)
    warm = (1.0, 0.45, 0.18) if look == "illusion" else (1.0, 0.70, 0.42)
    for e in [o for o in bpy.data.objects if o.type == "EMPTY" and o.name.startswith("Light_")]:
        kind = e.name.split("_")[1]
        energy, col, rad = {"Torch": (70, warm, 0.08), "Fire": (260, (1.0, 0.5, 0.2), 0.3),
                            "Lantern": (25, (1.0, 0.65, 0.35), 0.05), "Shining": (60, (1.0, 0.9, 0.7), 0.4)}[kind]
        scene.point_light("L_" + e.name, e.location, energy, col, rad)
    cam = bpy.context.scene.camera or scene.setup_render(samples)
    bpy.context.scene.cycles.samples = samples
    N = cave.NODES

    def eye(n, dx=0, dy=0, up=1.65):
        x, y, fz, _, _ = N[n]
        return Vector((x + dx, y + dy, fz + up))
    shots = [("entrance", eye("A0", 0, 1.5), eye("A1", 0, 0, 1.4)),
             ("guard", eye("B", -1.0, -5.5), eye("B", 1.0, 1.5, 0.9)),
             ("hall", eye("D", 4.0, -7.0, 2.2), eye("D", -2.5, 2.0, 0.3)),
             ("beds", eye("D", -1.5, -1.0, 1.9), eye("D", -5.8, 2.0, 0.2)),
             ("kitchen", eye("D", 3.0, 3.5, 1.8), eye("D", 0.0, 7.8, 0.5)),
             ("cage", eye("E", -3.2, -0.6, 1.7), eye("E", 0.8, 0.3, 0.9))]
    for name, e, t in shots:
        scene.shoot(cam, os.path.join(out, f"{look}_{name}.png"), e, t)
    log(look, "previews rendered")


TEX_OUT = None


def main():
    global TEX_OUT
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--unity", default=None)
    ap.add_argument("--samples", type=int, default=48)
    ap.add_argument("--no-render", action="store_true")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    stage = a.unity or os.path.join(a.out, "unity")
    os.makedirs(stage, exist_ok=True)
    TEX_OUT = os.path.join(stage, "Textures")
    os.makedirs(TEX_OUT, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = scene.lair_materials()
    verts, faces = cave.mesh()
    log("cave field:", len(faces), "triangles")
    cave_ob = scene.build_cave(verts, faces, 0.6)
    log("cave mesh:", len(cave_ob.data.polygons), "triangles")
    ray = scene.Ray(cave_ob)
    build_level(ray, mats)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(a.out, "goblin_lair.blend"))
    if not a.no_render:
        scene.setup_render(a.samples)
        previews(a.out, "illusion", a.samples)
        previews(a.out, "truth", a.samples)
    export_textures(stage)
    export_props(stage)
    export_fbx(stage)


if __name__ == "__main__":
    main()
