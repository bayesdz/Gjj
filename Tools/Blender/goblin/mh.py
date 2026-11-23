"""MakeHuman CC0 base mesh loader: obj + morph targets + default skeleton + skin weights.

Assets come from github.com/makehumancommunity/makehuman (data/, released CC0 in 2020).
Everything is converted to Blender space: metres, Z up, character facing -Y.
"""
import json
import os
import urllib.request

import bpy
import numpy as np
from mathutils import Vector

MH_RAW = "https://raw.githubusercontent.com/makehumancommunity/makehuman/master/makehuman/data/"
CACHE = os.environ.get("MH_CACHE", os.path.join(os.path.dirname(__file__), "_mh_cache"))


def fetch(rel):
    path = os.path.join(CACHE, rel)
    if not os.path.exists(path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        print("download", rel)
        urllib.request.urlretrieve(MH_RAW + rel, path)
    return path


# ------------------------------------------------------------------ obj
class Base:
    def __init__(self):
        self.co = None          # (N,3) MakeHuman space (Y up, decimetres)
        self.uv = []
        self.faces = {}         # group -> list of [(vi, uvi), ...]

    @classmethod
    def load(cls):
        b = cls()
        vs, group = [], "body"
        for line in open(fetch("3dobjs/base.obj")):
            if line.startswith("v "):
                vs.append([float(x) for x in line.split()[1:4]])
            elif line.startswith("vt "):
                b.uv.append([float(x) for x in line.split()[1:3]])
            elif line.startswith("g "):
                group = line.split()[1]
            elif line.startswith("f "):
                f = []
                for tok in line.split()[1:]:
                    p = tok.split("/")
                    f.append((int(p[0]) - 1, int(p[1]) - 1))
                b.faces.setdefault(group, []).append(f)
        b.co = np.array(vs, dtype=np.float64)
        return b

    def apply_target(self, rel, weight=1.0):
        if weight == 0:
            return
        for line in open(fetch("targets/" + rel + ".target")):
            if not line.strip() or line.startswith("#"):
                continue
            p = line.split()
            self.co[int(p[0])] += weight * np.array([float(p[1]), float(p[2]), float(p[3])])

    def target_indices(self, rel):
        out = []
        for line in open(fetch("targets/" + rel + ".target")):
            if line.strip() and not line.startswith("#"):
                out.append(int(line.split()[0]))
        return out

    def target_mask(self, rel):
        """Per-vertex |delta| of a target, normalised to 0..1 (a free falloff mask)."""
        out = {}
        for line in open(fetch("targets/" + rel + ".target")):
            if line.strip() and not line.startswith("#"):
                p = line.split()
                out[int(p[0])] = (float(p[1]) ** 2 + float(p[2]) ** 2 + float(p[3]) ** 2) ** 0.5
        m = max(out.values()) or 1.0
        return {k: v / m for k, v in out.items()}

    def blender_co(self):
        c = self.co * 0.1
        return np.stack([c[:, 0], -c[:, 2], c[:, 1]], axis=1)

    def make_object(self, name, groups, weights=None):
        """Mesh from the given obj groups. Keeps original vertex index as attribute 'mh_index'."""
        bco = self.blender_co()
        faces = [f for g in groups for f in self.faces.get(g, [])]
        used = sorted({vi for f in faces for vi, _ in f})
        remap = {vi: i for i, vi in enumerate(used)}
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(bco[v]) for v in used], [], [[remap[vi] for vi, _ in f] for f in faces])
        uvl = me.uv_layers.new(name="UVMap")
        k = 0
        for f in faces:
            for _, ti in f:
                uvl.data[k].uv = self.uv[ti]
                k += 1
        attr = me.attributes.new("mh_index", "INT", "POINT")
        attr.data.foreach_set("value", used)
        me.validate()
        for p in me.polygons:
            p.use_smooth = True
        ob = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(ob)
        if weights:
            for bone, lst in weights.items():
                vg = None
                for vi, w in lst:
                    if vi in remap:
                        if vg is None:
                            vg = ob.vertex_groups.new(name=bone)
                        vg.add([remap[vi]], w, "REPLACE")
        return ob, remap


# ------------------------------------------------------------------ skeleton
def load_skeleton():
    return json.load(open(fetch("rigs/default.mhskel")))


def load_weights():
    return json.load(open(fetch("rigs/default_weights.mhw")))["weights"]


def build_armature(base, skel, name="goblin_rig"):
    bco = base.blender_co()
    joints = {k: Vector(bco[v].mean(axis=0)) for k, v in skel["joints"].items()}
    arm = bpy.data.armatures.new(name)
    ob = bpy.data.objects.new(name, arm)
    bpy.context.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    bones = skel["bones"]
    for bn, bd in bones.items():
        eb = arm.edit_bones.new(bn)
        eb.head = joints[bd["head"]]
        eb.tail = joints[bd["tail"]]
        if (eb.tail - eb.head).length < 1e-4:
            eb.tail = eb.head + Vector((0, 0, 0.01))
    for bn, bd in bones.items():
        if bd["parent"]:
            arm.edit_bones[bn].parent = arm.edit_bones[bd["parent"]]
            if (arm.edit_bones[bn].head - arm.edit_bones[bd["parent"]].tail).length < 1e-4:
                arm.edit_bones[bn].use_connect = True
    bpy.ops.object.mode_set(mode="OBJECT")
    return ob
