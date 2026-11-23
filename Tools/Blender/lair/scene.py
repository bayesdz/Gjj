"""Blender-side helpers for the lair: cave object, PBR materials, raycasts, previews."""
import math
import os

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

from lair import assets, cave, polyhaven


# ---------------------------------------------------------------- materials
def pbr_material(name, ph_id, tint=(1, 1, 1), rough_mul=1.0):
    """Poly Haven texture set -> Principled BSDF (diffuse, GL normal, ARM)."""
    tex = polyhaven.texture(ph_id)
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(b.outputs[0], out.inputs[0])
    uv = nt.nodes.new("ShaderNodeTexCoord").outputs["UV"]

    def img(path, non_color=False):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(path, check_existing=True)
        if non_color:
            n.image.colorspace_settings.name = "Non-Color"
        nt.links.new(uv, n.inputs["Vector"])
        return n

    d = img(tex["Diffuse"])
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 1.0
    nt.links.new(d.outputs["Color"], mix.inputs[6])
    mix.inputs[7].default_value = (*tint, 1)
    nt.links.new(mix.outputs[2], b.inputs["Base Color"])
    arm = img(tex["arm"], True)
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(arm.outputs["Color"], sep.inputs[0])
    rm = nt.nodes.new("ShaderNodeMath")
    rm.operation = "MULTIPLY"
    rm.inputs[1].default_value = rough_mul
    nt.links.new(sep.outputs[1], rm.inputs[0])
    nt.links.new(rm.outputs[0], b.inputs["Roughness"])
    nt.links.new(sep.outputs[2], b.inputs["Metallic"])
    nrm = img(tex["nor_gl"], True)
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(nrm.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs[0], b.inputs["Normal"])
    m["ph_id"] = ph_id
    m["tint"] = list(tint)
    m["rough_mul"] = rough_mul
    return m


MATS = {}


def lair_materials():
    # Metro-like palette: desaturated, damp. Tints compensate the bright source scans.
    MATS["Lair_Rock"] = pbr_material("Lair_Rock", "rock_wall_10", (0.62, 0.60, 0.58), 0.9)
    MATS["Lair_Mud"] = pbr_material("Lair_Mud", "brown_mud_03", (0.70, 0.66, 0.60), 0.8)
    MATS["Lair_Wood"] = pbr_material("Lair_Wood", "rough_wood", (0.55, 0.48, 0.42))
    MATS["Lair_Straw"] = pbr_material("Lair_Straw", "reed_roof_04", (0.75, 0.68, 0.55))
    MATS["Lair_Rust"] = pbr_material("Lair_Rust", "rusty_metal_03", (0.6, 0.55, 0.5))
    MATS["Lair_Cloth"] = pbr_material("Lair_Cloth", "hessian_230", (0.55, 0.48, 0.40))
    return MATS


# ---------------------------------------------------------------- cave object
def build_cave(verts, faces, decimate=0.6):
    me = bpy.data.meshes.new("Lair_Cave")
    me.from_pydata(verts.tolist(), [], faces.tolist())
    me.validate()
    ob = bpy.data.objects.new("Lair_Cave", me)
    bpy.context.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    if decimate < 1.0:
        m = ob.modifiers.new("dec", "DECIMATE")
        m.ratio = decimate
        bpy.ops.object.modifier_apply(modifier="dec")
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(MATS["Lair_Rock"])
    me.materials.append(MATS["Lair_Mud"])
    # floor faces -> mud
    n = np.empty(len(me.polygons) * 3)
    me.polygons.foreach_get("normal", n)
    n = n.reshape(-1, 3)
    mi = (n[:, 2] > 0.6).astype(np.int32)
    me.polygons.foreach_set("material_index", mi)
    # world-space box UVs, per loop using the face normal (no stretching inside a face)
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    lv = np.empty(len(me.loops), dtype=np.int64)
    me.loops.foreach_get("vertex_index", lv)
    lp = np.empty(len(me.loops), dtype=np.int64)
    for p in me.polygons:
        lp[p.loop_start:p.loop_start + p.loop_total] = p.index
    tile = np.where(mi[lp] == 1, assets.TEXTURES["Lair_Mud"][1], assets.TEXTURES["Lair_Rock"][1])
    uv = cave.box_uv(co[lv], n[lp], 1.0) / tile[:, None]
    uvl = me.uv_layers.new(name="UVMap")
    uvl.data.foreach_set("uv", uv.ravel())
    return ob


class Ray:
    def __init__(self, ob):
        me = ob.data
        co = [ob.matrix_world @ v.co for v in me.vertices]
        self.bvh = BVHTree.FromPolygons(co, [p.vertices[:] for p in me.polygons])

    def cast(self, origin, direction, dist=50.0):
        return self.bvh.ray_cast(Vector(origin), Vector(direction).normalized(), dist)

    def floor(self, x, y, z_hint):
        hit, n, _, _ = self.cast((x, y, z_hint + 2.0), (0, 0, -1), 10.0)
        return hit, n


# ---------------------------------------------------------------- preview
def look_at(ob, target):
    d = Vector(target) - ob.location
    ob.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def point_light(name, loc, energy, color, radius=0.05):
    ld = bpy.data.lights.new(name, "POINT")
    ld.energy = energy
    ld.color = color
    ld.shadow_soft_size = radius
    o = bpy.data.objects.new(name, ld)
    o.location = loc
    bpy.context.collection.objects.link(o)
    return o


def setup_render(samples=64, res=(1280, 720)):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Punchy"
    sc.view_settings.exposure = 1.3
    w = bpy.data.worlds.get("W") or bpy.data.worlds.new("W")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.0, 0.0, 0.0, 1)
    sc.world = w
    # volumetric dust/smoke in the air (the Metro look)
    w_out = w.node_tree.nodes["World Output"]
    vol = w.node_tree.nodes.new("ShaderNodeVolumePrincipled")
    vol.inputs["Density"].default_value = 0.025
    vol.inputs["Color"].default_value = (0.8, 0.75, 0.7, 1)
    w.node_tree.links.new(vol.outputs[0], w_out.inputs["Volume"])
    sc.cycles.volume_step_rate = 4.0
    cam = bpy.data.cameras.new("cam")
    cam.lens = 24
    co = bpy.data.objects.new("cam", cam)
    sc.collection.objects.link(co)
    sc.camera = co
    return co


def shoot(cam, path, eye, target, lantern=True):
    cam.location = eye
    look_at(cam, target)
    lan = None
    if lantern:  # the hero's hand lantern
        lan = point_light("lantern", Vector(eye) + Vector((0.25, 0, -0.35)), 25, (1.0, 0.75, 0.45), 0.04)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    if lan:
        bpy.data.objects.remove(lan)
