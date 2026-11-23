"""Procedural Cycles materials (the "Substance Painter" layer). They are baked to
textures for Unity in bake.py, so only nodes that Cycles can bake are used."""
import bpy

ILLUSION_SKIN = dict(
    base_a=(0.060, 0.068, 0.045), base_b=(0.105, 0.112, 0.072), blotch=(0.045, 0.036, 0.026),
    flush=(0.16, 0.055, 0.040), dirt=(0.035, 0.028, 0.020), vein=(0.030, 0.018, 0.045),
    scar=(0.24, 0.12, 0.10), scar_k=1.0, rough=0.60, sss=0.08, cavity=0.6,
)
TRUTH_SKIN = dict(
    base_a=(0.175, 0.165, 0.085), base_b=(0.235, 0.215, 0.115), blotch=(0.16, 0.115, 0.065),
    flush=(0.36, 0.15, 0.090), dirt=(0.15, 0.115, 0.075), vein=(0.17, 0.14, 0.12),
    scar=(0.3, 0.2, 0.2), scar_k=0.0, rough=0.70, sss=0.18, cavity=0.4,
)


class _T:
    """Tiny node-graph helper."""

    def __init__(self, mat):
        mat.use_nodes = True
        self.nt = mat.node_tree
        self.nt.nodes.clear()
        self.x = 0

    def n(self, kind, **inputs):
        node = self.nt.nodes.new(kind)
        node.location = (self.x, 0)
        self.x += 200
        for k, v in inputs.items():
            if hasattr(node, k) and not k[0].isupper():
                setattr(node, k, v)
            else:
                self.set(node, k, v)
        return node

    def set(self, node, key, v):
        sock = node.inputs[key]
        if isinstance(v, bpy.types.NodeSocket):
            self.nt.links.new(v, sock)
        else:
            sock.default_value = (*v, 1.0) if isinstance(v, tuple) and len(v) == 3 else v

    def mix(self, a, b, fac, blend="MIX"):
        m = self.n("ShaderNodeMix", data_type="RGBA", blend_type=blend)
        self.set(m, 0, fac)
        self.set(m, 6, a)
        self.set(m, 7, b)
        return m.outputs[2]

    def mathop(self, op, a, b=0.0):
        m = self.n("ShaderNodeMath", operation=op)
        self.set(m, 0, a)
        self.set(m, 1, b)
        return m.outputs[0]


def skin(name, pal):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    t = _T(mat)
    tc = t.n("ShaderNodeTexCoord").outputs["Object"]
    attr = t.n("ShaderNodeAttribute", attribute_name="regions", attribute_type="GEOMETRY")
    sep = t.n("ShaderNodeSeparateColor")
    t.set(sep, 0, attr.outputs["Color"])
    flush, dirt, vein = sep.outputs[0], sep.outputs[1], sep.outputs[2]
    scar = attr.outputs["Alpha"]

    n1 = t.n("ShaderNodeTexNoise", Vector=tc, Scale=22.0, Detail=8.0, Roughness=0.6)
    base = t.mix(pal["base_a"], pal["base_b"], n1.outputs["Fac"])
    n2 = t.n("ShaderNodeTexNoise", Vector=tc, Scale=7.0, Detail=4.0, Roughness=0.55)
    bl = t.n("ShaderNodeMapRange", **{"From Min": 0.5, "From Max": 0.62})
    t.set(bl, "Value", n2.outputs["Fac"])
    col = t.mix(base, pal["blotch"], t.mathop("MULTIPLY", bl.outputs[0], 0.7))
    # freckles / pigment spots
    vor = t.n("ShaderNodeTexVoronoi", Vector=tc, Scale=140.0, Randomness=1.0)
    spots = t.n("ShaderNodeMapRange", **{"From Min": 0.12, "From Max": 0.04})
    t.set(spots, "Value", vor.outputs["Distance"])
    col = t.mix(col, pal["blotch"], t.mathop("MULTIPLY", spots.outputs[0], 0.35))
    col = t.mix(col, pal["flush"], t.mathop("MULTIPLY", flush, 0.75))
    col = t.mix(col, pal["vein"], t.mathop("MULTIPLY", vein, 0.6))
    col = t.mix(col, pal["dirt"], t.mathop("MULTIPLY", dirt, 0.8))
    col = t.mix(col, pal["scar"], t.mathop("MULTIPLY", scar, pal["scar_k"]))
    # cavity darkening (crevices, wrinkles)
    ao = t.n("ShaderNodeAmbientOcclusion", samples=8, Distance=0.006)
    cav = t.n("ShaderNodeMapRange", **{"From Min": 0.2, "From Max": 1.0, "To Min": 1 - pal["cavity"]})
    t.set(cav, "Value", ao.outputs["AO"])
    col = t.mix(col, cav.outputs[0], 1.0, "MULTIPLY")

    # roughness
    rn = t.n("ShaderNodeTexNoise", Vector=tc, Scale=40.0, Detail=3.0)
    rough = t.mathop("ADD", t.mathop("MULTIPLY", rn.outputs["Fac"], 0.2), pal["rough"] - 0.1)
    rough = t.mathop("ADD", rough, t.mathop("MULTIPLY", dirt, 0.3))
    rough = t.mathop("MINIMUM", rough, 0.95)

    # micro detail: pores + fine skin cells
    pores = t.n("ShaderNodeTexVoronoi", Vector=tc, Scale=1100.0, feature="F1")
    b1 = t.n("ShaderNodeBump", Strength=0.35, Distance=0.0004)
    t.set(b1, "Height", pores.outputs["Distance"])
    cells = t.n("ShaderNodeTexVoronoi", Vector=tc, Scale=420.0, feature="DISTANCE_TO_EDGE")
    cells_h = t.n("ShaderNodeMapRange", **{"From Min": 0.0, "From Max": 0.08})
    t.set(cells_h, "Value", cells.outputs["Distance"])
    b2 = t.n("ShaderNodeBump", Strength=0.25, Distance=0.0003)
    t.set(b2, "Height", cells_h.outputs[0])
    t.set(b2, "Normal", b1.outputs["Normal"])

    bsdf = t.n("ShaderNodeBsdfPrincipled")
    t.set(bsdf, "Base Color", col)
    t.set(bsdf, "Roughness", rough)
    t.set(bsdf, "Normal", b2.outputs["Normal"])
    bsdf.inputs["Specular IOR Level"].default_value = 0.35
    bsdf.inputs["Subsurface Weight"].default_value = pal["sss"]
    bsdf.inputs["Subsurface Radius"].default_value = (1.0, 0.35, 0.2)
    bsdf.inputs["Subsurface Scale"].default_value = 0.006
    out = t.n("ShaderNodeOutputMaterial")
    t.set(out, "Surface", bsdf.outputs[0])
    return mat


def fabric(name, color_a, color_b, stain, rough=0.85, weave=380.0, stain_amt=0.5, leather=False):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    t = _T(mat)
    tc = t.n("ShaderNodeTexCoord").outputs["Object"]
    n1 = t.n("ShaderNodeTexNoise", Vector=tc, Scale=9.0, Detail=6.0, Roughness=0.6)
    col = t.mix(color_a, color_b, n1.outputs["Fac"])
    n2 = t.n("ShaderNodeTexNoise", Vector=tc, Scale=4.0, Detail=5.0)
    st = t.n("ShaderNodeMapRange", **{"From Min": 0.52, "From Max": 0.66})
    t.set(st, "Value", n2.outputs["Fac"])
    col = t.mix(col, stain, t.mathop("MULTIPLY", st.outputs[0], stain_amt))
    ao = t.n("ShaderNodeAmbientOcclusion", samples=8, Distance=0.01)
    col = t.mix(col, ao.outputs["Color"], 0.5, "MULTIPLY")
    bsdf = t.n("ShaderNodeBsdfPrincipled", Roughness=rough)
    t.set(bsdf, "Base Color", col)
    if leather:
        h = t.n("ShaderNodeTexVoronoi", Vector=tc, Scale=260.0, feature="DISTANCE_TO_EDGE").outputs["Distance"]
        strength = 0.3
    else:
        w1 = t.n("ShaderNodeTexWave", Vector=tc, Scale=weave, wave_type="BANDS", bands_direction="X")
        w2 = t.n("ShaderNodeTexWave", Vector=tc, Scale=weave, wave_type="BANDS", bands_direction="Z")
        h = t.mathop("MULTIPLY", w1.outputs["Fac"], w2.outputs["Fac"])
        strength = 0.45
    fray = t.n("ShaderNodeTexNoise", Vector=tc, Scale=60.0, Detail=4.0)
    hh = t.mathop("ADD", h, t.mathop("MULTIPLY", fray.outputs["Fac"], 0.6))
    bump = t.n("ShaderNodeBump", Strength=strength, Distance=0.001)
    t.set(bump, "Height", hh)
    t.set(bsdf, "Normal", bump.outputs["Normal"])
    out = t.n("ShaderNodeOutputMaterial")
    t.set(out, "Surface", bsdf.outputs[0])
    return mat


def simple(name, color, rough=0.5, emission=None, strength=0.0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    if emission:
        b.inputs["Emission Color"].default_value = (*emission, 1)
        b.inputs["Emission Strength"].default_value = strength
    return mat


def textured(name, albedo, normal=None, rough_img=None, emission=None, emission_strength=0.0,
             ao=None, sss=0.0):
    """Material that uses the baked maps (used for the final preview renders)."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    t = _T(mat)
    uv = t.n("ShaderNodeTexCoord").outputs["UV"]
    a = t.n("ShaderNodeTexImage", image=albedo)
    t.set(a, "Vector", uv)
    bsdf = t.n("ShaderNodeBsdfPrincipled")
    col = a.outputs["Color"]
    if ao is not None:
        ai = t.n("ShaderNodeTexImage", image=ao)
        ai.image.colorspace_settings.name = "Non-Color"
        col = t.mix(col, ai.outputs["Color"], 0.8, "MULTIPLY")
    t.set(bsdf, "Base Color", col)
    if normal is not None:
        ni = t.n("ShaderNodeTexImage", image=normal)
        ni.image.colorspace_settings.name = "Non-Color"
        nm = t.n("ShaderNodeNormalMap")
        t.set(nm, "Color", ni.outputs["Color"])
        t.set(bsdf, "Normal", nm.outputs["Normal"])
    if rough_img is not None:
        ri = t.n("ShaderNodeTexImage", image=rough_img)
        ri.image.colorspace_settings.name = "Non-Color"
        t.set(bsdf, "Roughness", ri.outputs["Color"])
    if emission is not None:
        ei = t.n("ShaderNodeTexImage", image=emission)
        t.set(bsdf, "Emission Color", ei.outputs["Color"])
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    bsdf.inputs["Specular IOR Level"].default_value = 0.35
    if sss:
        bsdf.inputs["Subsurface Weight"].default_value = sss
        bsdf.inputs["Subsurface Radius"].default_value = (1.0, 0.35, 0.2)
        bsdf.inputs["Subsurface Scale"].default_value = 0.006
    out = t.n("ShaderNodeOutputMaterial")
    t.set(out, "Surface", bsdf.outputs[0])
    return mat
