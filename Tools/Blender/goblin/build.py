"""Goblin character pipeline (Blender 5.x, runs headless via the `bpy` module).

    pip install bpy==5.3.0
    cd Tools/Blender
    python -m goblin.build --out <dir> [--res 2048] [--unity ../../Assets/_Game/Art/Characters/Goblin]

Steps: MakeHuman CC0 base -> goblin morphs/sculpt -> humanoid rig -> eyes, tusks, clothes
-> high-poly detail -> procedural materials -> bake (normal, AO, albedo x2, smoothness x2)
-> preview renders -> FBX + PNG textures for Unity.
"""
import argparse
import os
import shutil
import sys
import time

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from goblin import bake, details, goblin, materials, pose, preview  # noqa: E402

T0 = time.time()


def log(*a):
    print(f"[{time.time() - T0:6.1f}s]", *a, flush=True)


def build_model():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    base, rig, meshes = goblin.build_base()
    goblin.goblin_sculpt(base, rig, meshes)
    goblin.rename_bones(rig)
    goblin.build_eyes(rig, meshes)
    goblin.build_tusks(rig, meshes)
    goblin.build_clothes(rig, meshes)
    log("model built:", {k: len(v.data.vertices) for k, v in meshes.items()})
    return rig, meshes


def setup_procedural_clothes(meshes):
    """Fabrics per clothing piece (face attribute 'piece' -> material index)."""
    rags = meshes["goblin_clothes_illusion"]
    truth = meshes["goblin_clothes_truth"]
    for ob in (rags, truth):
        ob.data.materials.clear()
    rags.data.materials.append(materials.fabric(
        "fab_rags", (0.040, 0.032, 0.024), (0.075, 0.058, 0.040), (0.06, 0.008, 0.005), 0.9, 300, 0.8))
    rags.data.materials.append(materials.fabric(
        "fab_leather_dark", (0.030, 0.020, 0.014), (0.060, 0.040, 0.026), (0.02, 0.015, 0.01), 0.55, leather=True))
    truth.data.materials.append(materials.fabric(
        "fab_linen", (0.33, 0.29, 0.21), (0.40, 0.35, 0.26), (0.24, 0.20, 0.13), 0.9, 420, 0.45))
    truth.data.materials.append(materials.fabric(
        "fab_wool", (0.17, 0.11, 0.07), (0.23, 0.15, 0.09), (0.12, 0.08, 0.05), 0.9, 260, 0.3))
    truth.data.materials.append(materials.fabric(
        "fab_rope", (0.33, 0.26, 0.15), (0.42, 0.34, 0.20), (0.25, 0.2, 0.12), 0.8, 120, 0.2))
    for ob in (rags, truth):
        piece = ob.data.attributes.get("piece")
        vals = [0] * len(ob.data.polygons)
        piece.data.foreach_get("value", vals)
        for p, v in zip(ob.data.polygons, vals):
            p.material_index = v


def bake_all(rig, meshes, res, out):
    tex = os.path.join(out, "Textures")
    os.makedirs(tex, exist_ok=True)
    body = meshes["goblin_body"]
    hp = details.build_highpoly(body, rig, meshes["goblin_eyes"])
    log("high-poly:", len(hp.data.vertices), "verts")
    bake.setup()

    body.data.materials.clear()
    body.data.materials.append(materials.simple("goblin_body_bake", (0.5, 0.5, 0.5)))
    skin_i = materials.skin("skin_illusion", materials.ILLUSION_SKIN)
    skin_t = materials.skin("skin_truth", materials.TRUTH_SKIN)
    hp.data.materials.append(skin_i)
    inner = [meshes[k] for k in ("goblin_eyes", "goblin_teeth", "goblin_tongue")]

    maps = {}
    maps["normal"] = bake.bake(body, bake.new_image("Goblin_Body_Normal", res, non_color=True),
                               "NORMAL", source=hp, samples=4, visible=inner)
    log("normal baked")
    maps["ao"] = bake.bake(body, bake.new_image("Goblin_Body_AO", res, non_color=True),
                           "AO", source=hp, samples=48, visible=inner)
    log("AO baked")
    for look, mat in (("Illusion", skin_i), ("Truth", skin_t)):
        hp.data.materials[0] = mat
        maps[f"albedo_{look}"] = bake.bake(body, bake.new_image(f"Goblin_Body_{look}_Albedo", res),
                                           "DIFFUSE", source=hp, samples=12, filt={"COLOR"},
                                           visible=inner)
        maps[f"rough_{look}"] = bake.bake(body, bake.new_image(f"Goblin_Body_{look}_Rough", res, non_color=True),
                                          "ROUGHNESS", source=hp, samples=1, visible=inner)
        bake.metallic_smoothness(maps[f"rough_{look}"], f"Goblin_Body_{look}_MetallicSmoothness")
        log(look, "skin baked")
    hp.hide_render = True
    hp.hide_set(True)

    cres = res // 2
    for look in ("Illusion", "Truth"):
        ob = meshes[f"goblin_clothes_{look.lower()}"]
        n = bake.bake(ob, bake.new_image(f"Goblin_Clothes_{look}_Normal", cres, non_color=True), "NORMAL", samples=4)
        a = bake.bake(ob, bake.new_image(f"Goblin_Clothes_{look}_Albedo", cres), "DIFFUSE", samples=12,
                      filt={"COLOR"}, visible=[body])
        r = bake.bake(ob, bake.new_image(f"Goblin_Clothes_{look}_Rough", cres, non_color=True), "ROUGHNESS",
                      samples=1)
        bake.metallic_smoothness(r, f"Goblin_Clothes_{look}_MetallicSmoothness")
        maps[f"cloth_{look}"] = (a, n, r)
        log(look, "clothes baked")

    bake.skin_detail_normal("Goblin_Skin_Detail_Normal")
    for look in ("Illusion", "Truth"):
        maps[f"eye_{look}"] = bake.paint_eye(f"Goblin_Eye_{look}", 512, look == "Illusion")

    for img in list(bpy.data.images):
        if img.name.startswith("Goblin_") and not img.name.endswith("_Rough"):
            bake.save(img, os.path.join(tex, img.name + ".png"))
    log("textures saved")
    return maps


def assign_final(meshes, maps, look):
    """Materials built from the baked maps, i.e. what Unity will show (for previews)."""
    body = meshes["goblin_body"]
    body.data.materials.clear()
    body.data.materials.append(materials.textured(
        f"Goblin_Body_{look}", maps[f"albedo_{look}"], maps["normal"], maps[f"rough_{look}"],
        ao=maps["ao"], sss=0.1 if look == "Truth" else 0.05))
    eyes = meshes["goblin_eyes"]
    eyes.data.materials.clear()
    alb, emi = maps[f"eye_{look}"]
    m = materials.textured(f"Goblin_Eye_{look}", alb, emission=emi, emission_strength=6.0 if emi else 0.0)
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.08
    bsdf.inputs["Coat Weight"].default_value = 1.0
    eyes.data.materials.append(m)
    for name in ("Illusion", "Truth"):
        ob = meshes[f"goblin_clothes_{name.lower()}"]
        a, n, r = maps[f"cloth_{name}"]
        ob.data.materials.clear()
        ob.data.materials.append(materials.textured(f"Goblin_Clothes_{name}", a, n, r))
        ob.hide_render = name != look
    teeth_col = (0.30, 0.22, 0.10) if look == "Illusion" else (0.55, 0.48, 0.34)
    for k in ("goblin_teeth", "goblin_tusks"):
        meshes[k].data.materials.clear()
        meshes[k].data.materials.append(materials.simple(f"Goblin_Teeth_{look}", teeth_col, 0.35))
    meshes["goblin_tusks"].hide_render = look != "Illusion"
    meshes["goblin_tongue"].data.materials.clear()
    meshes["goblin_tongue"].data.materials.append(materials.simple("Goblin_Tongue", (0.25, 0.06, 0.06), 0.3))


def render_previews(rig, meshes, maps, out):
    for look, posefn in (("Illusion", pose.menace), ("Truth", pose.cower)):
        assign_final(meshes, maps, look)
        posefn(rig, meshes)
        head = rig.matrix_world @ rig.pose.bones["Head"].matrix.translation
        face = head + Vector((0, -0.07, 0.03))
        cam = preview.setup_studio(look.lower())
        bpy.context.scene.cycles.samples = 64
        preview.shoot(cam, os.path.join(out, f"{look}_full.png"), 20, target=(0, 0, 0.5), dist=3.6, height=0.8)
        preview.shoot(cam, os.path.join(out, f"{look}_face.png"), 25 if look == "Illusion" else 340,
                      target=tuple(face), dist=1.1, height=face.z + 0.05)
        log(look, "rendered")
    pose.reset(rig)


UNITY_NAMES = {
    "goblin_body": "Goblin_Body", "goblin_eyes": "Goblin_Eyes", "goblin_teeth": "Goblin_Teeth",
    "goblin_tusks": "Goblin_Tusks", "goblin_tongue": "Goblin_Tongue",
    "goblin_clothes_illusion": "Goblin_Clothes_Illusion", "goblin_clothes_truth": "Goblin_Clothes_Truth",
}


def export_unity(rig, meshes, out, unity_dir):
    os.makedirs(unity_dir, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for k, ob in meshes.items():
        ob.hide_render = False
        ob.hide_set(False)
        ob.name = UNITY_NAMES[k]
        ob.data.materials.clear()
        ob.data.materials.append(materials.simple(UNITY_NAMES[k], (0.5, 0.5, 0.5)))
        for an in ("mh_index", "piece"):
            if an in ob.data.attributes:
                ob.data.attributes.remove(ob.data.attributes[an])
        ob.select_set(True)
    rig.name = "Goblin"
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    path = os.path.join(unity_dir, "Goblin.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        mesh_smooth_type="FACE", use_tspace=True, bake_anim=False, use_armature_deform_only=True,
        use_mesh_modifiers=False)
    tdst = os.path.join(unity_dir, "Textures")
    os.makedirs(tdst, exist_ok=True)
    for f in sorted(os.listdir(os.path.join(out, "Textures"))):
        shutil.copy(os.path.join(out, "Textures", f), tdst)
    log("exported", path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--res", type=int, default=2048)
    ap.add_argument("--unity", default=None)
    ap.add_argument("--no-render", action="store_true")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    rig, meshes = build_model()
    setup_procedural_clothes(meshes)
    maps = bake_all(rig, meshes, a.res, a.out)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(a.out, "goblin.blend"))
    if not a.no_render:
        render_previews(rig, meshes, maps, a.out)
    if a.unity:
        export_unity(rig, meshes, a.out, a.unity)


if __name__ == "__main__":
    main()
