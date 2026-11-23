"""Bake high-poly + procedural materials to game textures (the "Marmoset/Substance bake")."""
import os

import bpy
import numpy as np


def new_image(name, res, non_color=False, alpha=False):
    img = bpy.data.images.get(name)
    if img:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, res, res, alpha=alpha, float_buffer=False)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    return img


def _target_nodes(ob, img):
    """Put an active Image node holding `img` in every material of `ob`."""
    for slot in ob.material_slots:
        nt = slot.material.node_tree
        node = nt.nodes.get("__bake__") or nt.nodes.new("ShaderNodeTexImage")
        node.name = "__bake__"
        node.image = img
        nt.nodes.active = node


def _clear_target_nodes(ob):
    for slot in ob.material_slots:
        nt = slot.material.node_tree
        n = nt.nodes.get("__bake__")
        if n:
            nt.nodes.remove(n)


def _select(active, others=()):
    bpy.ops.object.select_all(action="DESELECT")
    for o in others:
        o.hide_render = False
        o.select_set(True)
    active.select_set(True)
    bpy.context.view_layer.objects.active = active


def setup(samples=8, margin=8):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.render.bake.margin = margin
    sc.render.bake.margin_type = "EXTEND"


def bake(target, img, kind, source=None, samples=8, cage=0.006, ray=0.02, filt=None, visible=()):
    """Bake into img. Only target, source and `visible` objects take part (AO, shadows)."""
    sc = bpy.context.scene
    sc.cycles.samples = samples
    keep = {target, source, *visible}
    hidden = {o: o.hide_render for o in sc.objects}
    for o in sc.objects:
        if o.type == "MESH":
            o.hide_render = o not in keep
    _target_nodes(target, img)
    if source is not None:
        _select(target, [source])
        sc.render.bake.use_selected_to_active = True
        sc.render.bake.cage_extrusion = cage
        sc.render.bake.max_ray_distance = ray
    else:
        _select(target)
        sc.render.bake.use_selected_to_active = False
    kw = {}
    if filt:
        kw["pass_filter"] = filt
    bpy.ops.object.bake(type=kind, **kw)
    _clear_target_nodes(target)
    for o, h in hidden.items():
        o.hide_render = h
    return img


def save(img, path):
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return path


def pixels(img):
    a = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)


def put(img, arr):
    img.pixels.foreach_set(arr.astype(np.float32).ravel())
    img.update()


def metallic_smoothness(rough_img, name):
    """URP Lit: RGB = metallic (0), A = smoothness."""
    r = pixels(rough_img)
    out = new_image(name, rough_img.size[0], non_color=True, alpha=True)
    arr = np.zeros_like(r)
    arr[..., 3] = 1.0 - r[..., 0]
    put(out, arr)
    return out


# ------------------------------------------------------------------ painted eye textures
def paint_eye(name, res, illusion):
    """Polar eye texture: pupil at UV centre, sclera toward the edge (see goblin.build_eyes)."""
    rng = np.random.default_rng(3 if illusion else 4)
    y, x = np.mgrid[0:res, 0:res] / res - 0.5
    r = np.sqrt(x * x + y * y)
    a = np.arctan2(y, x)
    iris_r, pupil_r = 0.125, 0.045
    fib = 0.5 + 0.5 * np.sin(a * 90 + np.sin(a * 13) * 2) * np.sin(a * 37 + 1.3)
    fib = fib * 0.5 + 0.5 * np.cos(r * 300 + np.sin(a * 21))
    if illusion:
        sclera = np.array([0.42, 0.30, 0.10])
        vein_col = np.array([0.35, 0.02, 0.01])
        iris_in, iris_out = np.array([1.0, 0.55, 0.08]), np.array([0.55, 0.02, 0.0])
        pupil = (np.abs(x) < pupil_r * 0.28 * np.sqrt(np.clip(1 - (y / (iris_r * 0.95)) ** 2, 0, 1))) & (r < iris_r)
    else:
        sclera = np.array([0.80, 0.76, 0.66])
        vein_col = np.array([0.6, 0.25, 0.22])
        iris_in, iris_out = np.array([0.42, 0.28, 0.10]), np.array([0.16, 0.09, 0.04])
        pupil = r < pupil_r
    col = np.ones((res, res, 3)) * sclera
    # sclera veins
    veins = np.zeros((res, res))
    for _ in range(40):
        a0 = rng.uniform(-np.pi, np.pi)
        wig = rng.uniform(5, 15)
        da = np.angle(np.exp(1j * (a - a0 - 0.08 * np.sin(r * wig * 20))))
        veins = np.maximum(veins, np.exp(-(da / 0.012) ** 2) * np.clip((r - 0.16) / 0.25, 0, 1))
    col = col * (1 - veins[..., None] * 0.7) + vein_col * veins[..., None] * 0.7
    # darker edge toward the back of the eye
    col *= np.clip(1.2 - r * 1.2, 0.3, 1)[..., None]
    # iris
    t = np.clip((r - pupil_r) / (iris_r - pupil_r), 0, 1)[..., None]
    iris = iris_in * (1 - t) + iris_out * t
    iris = iris * (0.65 + 0.5 * fib[..., None])
    limbal = np.exp(-((r - iris_r) / 0.01) ** 2)[..., None]
    m_iris = (r < iris_r)[..., None]
    col = np.where(m_iris, iris, col)
    col = col * (1 - limbal * 0.8)
    col = np.where(pupil[..., None], np.array([0.01, 0.005, 0.003]), col)
    rgba = np.concatenate([col, np.ones((res, res, 1))], -1)
    img = new_image(name + "_Albedo", res)
    put(img, rgba)
    emi = None
    if illusion:
        glow = np.where(m_iris & ~pupil[..., None], iris * 1.2, 0)
        emi = new_image(name + "_Emission", res)
        put(emi, np.concatenate([glow, np.ones((res, res, 1))], -1))
    return img, emi


def skin_detail_normal(name, res=512, strength=2.5, seed=11):
    """Tileable micro-detail normal map (pores + skin cells) for URP Lit 'Detail Inputs'."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:res, 0:res].astype(np.float64)
    h = np.zeros((res, res))
    # skin cells: distance to the nearest edge of a periodic voronoi diagram
    pts = rng.uniform(0, res, size=(90, 2))
    d1 = np.full((res, res), 1e9)
    d2 = np.full((res, res), 1e9)
    for px, py in pts:
        dx = np.abs(x - px)
        dx = np.minimum(dx, res - dx)
        dy = np.abs(y - py)
        dy = np.minimum(dy, res - dy)
        d = np.sqrt(dx * dx + dy * dy)
        d2 = np.where(d < d1, d1, np.minimum(d2, d))
        d1 = np.minimum(d1, d)
    edge = d2 - d1
    h += np.clip(edge / 6.0, 0, 1) * 0.6
    # pores: small periodic dimples
    for px, py in rng.uniform(0, res, size=(1400, 2)):
        r = rng.uniform(1.2, 2.6)
        dx = (x - px + res / 2) % res - res / 2
        dy = (y - py + res / 2) % res - res / 2
        h -= np.exp(-(dx * dx + dy * dy) / (r * r)) * rng.uniform(0.4, 1.0)
    # fine noise via filtered white noise (periodic by FFT)
    f = np.fft.fft2(rng.normal(size=(res, res)))
    fy, fx = np.meshgrid(np.fft.fftfreq(res), np.fft.fftfreq(res), indexing="ij")
    f *= np.exp(-(fx * fx + fy * fy) / (2 * 0.08 ** 2))
    h += np.real(np.fft.ifft2(f)) * 0.4
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * strength
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * strength
    n = np.stack([-gx, -gy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    rgba = np.concatenate([n * 0.5 + 0.5, np.ones((res, res, 1))], -1)
    img = new_image(name, res, non_color=True)
    put(img, rgba)
    return img
