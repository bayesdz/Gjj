"""Preview renders (Cycles) to judge the sculpt and materials."""
import math
import bpy
from mathutils import Vector


def clay_material():
    m = bpy.data.materials.get("clay") or bpy.data.materials.new("clay")
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.36, 0.33, 0.30, 1)
    b.inputs["Roughness"].default_value = 0.55
    return m


def _look_at(ob, target):
    d = Vector(target) - ob.location
    ob.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def setup_studio(mood="clay"):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 96
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 900
    sc.render.resolution_y = 1100
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Punchy" if mood != "clay" else "None"
    w = bpy.data.worlds.get("W") or bpy.data.worlds.new("W")
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    sc.world = w
    for o in [o for o in bpy.data.objects if o.get("preview")]:
        bpy.data.objects.remove(o)

    def light(name, kind, loc, energy, color, size=0.3):
        ld = bpy.data.lights.new(name, kind)
        ld.energy = energy
        ld.color = color
        if kind == "AREA":
            ld.size = size
        else:
            ld.shadow_soft_size = size
        o = bpy.data.objects.new(name, ld)
        o.location = loc
        o["preview"] = True
        sc.collection.objects.link(o)
        _look_at(o, (0, 0, 0.9))
        return o

    if mood == "clay":
        bg.inputs["Color"].default_value = (0.05, 0.05, 0.055, 1)
        bg.inputs["Strength"].default_value = 1.0
        light("key", "AREA", (1.2, -1.6, 1.8), 45, (1, 0.97, 0.92), 1.0)
        light("fill", "AREA", (-1.6, -1.2, 1.2), 8, (0.85, 0.9, 1), 1.5)
        light("rim", "AREA", (0.2, 1.6, 1.6), 40, (1, 1, 1), 0.8)
    elif mood == "illusion":
        # dungeon: almost black, cold rim from behind, low hand-lantern from the player
        bg.inputs["Color"].default_value = (0.002, 0.003, 0.004, 1)
        light("lantern", "POINT", (0.45, -1.0, 0.95), 30, (1.0, 0.62, 0.30), 0.05)
        light("rim", "AREA", (-0.6, 1.2, 1.5), 70, (0.45, 0.6, 1.0), 0.6)
        light("under", "POINT", (0.0, -0.4, 0.2), 1.5, (0.6, 0.15, 0.1), 0.05)
    elif mood == "truth":
        bg.inputs["Color"].default_value = (0.02, 0.015, 0.01, 1)
        light("hearth", "AREA", (0.9, -1.2, 0.9), 32, (1.0, 0.70, 0.45), 1.2)
        light("fill", "AREA", (-1.4, -1.0, 1.4), 7, (1.0, 0.85, 0.7), 1.5)
        light("rim", "AREA", (0.2, 1.5, 1.5), 18, (1.0, 0.9, 0.8), 0.8)

    cam = bpy.data.cameras.new("cam")
    cam.lens = 85
    co = bpy.data.objects.new("cam", cam)
    co["preview"] = True
    sc.collection.objects.link(co)
    sc.camera = co
    return co


def shoot(co, path, azimuth_deg, target=(0, 0, 0.66), dist=3.4, height=0.75, lens=85):
    a = math.radians(azimuth_deg)
    co.data.lens = lens
    co.location = (math.sin(a) * dist, -math.cos(a) * dist, height)
    _look_at(co, target)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
