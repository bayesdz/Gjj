"""High-poly detail pass (the "ZBrush" layer): subdivide the game mesh and displace
wrinkles, folds, knuckle creases and veins. Also writes a 'regions' colour attribute
(R flush, G dirt, B veins, A scars) that the skin shaders read.
"""
import numpy as np
import bpy

# ------------------------------------------------------------------ vectorised noise
_rng = np.random.default_rng(1234)
_PERM = np.concatenate([_rng.permutation(256)] * 2)
_GRAD = _rng.normal(size=(256, 3))
_GRAD /= np.linalg.norm(_GRAD, axis=1, keepdims=True)


def gnoise(p):
    """3D gradient noise, roughly in [-1, 1]. p: (N,3)."""
    pi = np.floor(p).astype(np.int64)
    pf = p - pi
    pi &= 255
    w = pf * pf * pf * (pf * (pf * 6 - 15) + 10)
    out = 0.0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                h = _PERM[_PERM[_PERM[pi[:, 0] + dx] + pi[:, 1] + dy] + pi[:, 2] + dz]
                g = _GRAD[h]
                d = pf - np.array([dx, dy, dz])
                dot = (g * d).sum(1)
                wx = w[:, 0] if dx else 1 - w[:, 0]
                wy = w[:, 1] if dy else 1 - w[:, 1]
                wz = w[:, 2] if dz else 1 - w[:, 2]
                out = out + dot * wx * wy * wz
    return out * 1.8


def fbm(p, octaves=4, lac=2.1, gain=0.5):
    a, s, out = 1.0, 1.0, 0.0
    for i in range(octaves):
        out = out + a * gnoise(p * s + i * 17.3)
        a *= gain
        s *= lac
    return out


def crease_lines(t, width=0.12):
    """Periodic sharp valleys at integer t (1 at the valley, 0 elsewhere)."""
    d = np.abs(t - np.round(t))
    return np.exp(-(d / width) ** 2)


def sphere(co, c, r):
    d = np.linalg.norm(co - np.asarray(c), axis=1) / r
    w = np.clip(1 - d, 0, 1)
    return w * w * (3 - 2 * w)


def capsule(co, a, b, r):
    a, b = np.asarray(a), np.asarray(b)
    ab = b - a
    t = np.clip(((co - a) @ ab) / (ab @ ab), 0, 1)
    d = np.linalg.norm(co - (a + t[:, None] * ab), axis=1) / r
    w = np.clip(1 - d, 0, 1)
    return w * w * (3 - 2 * w)


# ------------------------------------------------------------------ helpers
def get(ob, attr="co"):
    a = np.empty(len(ob.data.vertices) * 3)
    ob.data.vertices.foreach_get(attr, a)
    return a.reshape(-1, 3)


def B(rig, name):
    b = rig.data.bones[name]
    return np.array(b.head_local), np.array(b.tail_local)


def build_highpoly(body, rig, eyes, levels=2):
    hp = body.copy()
    hp.data = body.data.copy()
    hp.name = hp.data.name = "goblin_body_hp"
    hp.parent = None
    for m in list(hp.modifiers):
        hp.modifiers.remove(m)
    hp.vertex_groups.clear()
    bpy.context.collection.objects.link(hp)
    m = hp.modifiers.new("sub", "SUBSURF")
    m.levels = m.render_levels = levels
    m.uv_smooth = "PRESERVE_CORNERS"
    bpy.context.view_layer.objects.active = hp
    bpy.ops.object.modifier_apply(modifier="sub")

    co = get(hp)
    n = get(hp, "normal")
    disp = np.zeros(len(co))
    ecol = get(eyes)
    eyeL = ecol[ecol[:, 0] > 0].mean(0)
    eyeR = ecol[ecol[:, 0] < 0].mean(0)
    head_h, head_t = B(rig, "Head")
    neck_h, _ = B(rig, "Neck")
    front = np.clip(-n[:, 1], 0, 1)

    # 1. lumpy, uneven skin everywhere
    disp += fbm(co * 35, 3) * 0.0006
    # 2. leathery micro-crease network (anisotropic: mostly horizontal folds)
    q = co * np.array([140, 140, 260])
    net = crease_lines(fbm(q, 2) * 2.5, 0.10)
    disp -= net * 0.00028
    # 3. forehead: deep horizontal furrows that bend with the brow
    brow_c = (eyeL + eyeR) / 2 + np.array([0, -0.005, 0.035])
    m_fh = sphere(co, brow_c, 0.06) * front
    t = co[:, 2] * 190 + fbm(co * 30, 2) * 1.2 + (co[:, 0] ** 2) * 900
    disp -= crease_lines(t, 0.11) * m_fh * 0.0014
    # frown lines between the brows
    m_fr = sphere(co, brow_c + np.array([0, -0.004, -0.018]), 0.02) * front
    disp -= crease_lines(co[:, 0] * 160 + 0.5, 0.12) * m_fr * 0.0012
    # 4. crow's feet + bags under the eyes
    for e, s in ((eyeL, 1), (eyeR, -1)):
        corner = e + np.array([0.022 * s, 0.004, 0.0])
        v = co - corner
        ang = np.arctan2(v[:, 2], v[:, 0] * s)
        m_cf = sphere(co, corner + np.array([0.01 * s, 0, 0]), 0.03)
        disp -= crease_lines(ang * 5 + fbm(co * 40, 1) * 0.5, 0.12) * m_cf * 0.0008
        bag = e + np.array([0.0, -0.006, -0.02])
        r = np.linalg.norm((co - bag) * np.array([0.8, 1.6, 1.4]), axis=1)
        disp += sphere(co, bag, 0.022) * 0.0018
        disp -= crease_lines(r * 220, 0.1) * sphere(co, bag, 0.026) * 0.0008
    # 5. neck rings and wattle
    m_nk = capsule(co, neck_h, head_h + np.array([0, -0.03, -0.01]), 0.06)
    disp -= crease_lines(co[:, 2] * 160 + fbm(co * 25, 2) * 1.5, 0.12) * m_nk * 0.0012

    # 6. joint creases: perpendicular rings around each joint
    joints = []
    for side in ("Left", "Right"):
        for f in ("Thumb", "Index", "Middle", "Ring", "Pinky"):
            for j in (1, 2, 3):
                joints.append((f"{side}Hand{f}{j}", 0.012, 900, 0.0004))
        joints += [(f"{side}ForeArm", 0.04, 260, 0.0010), (f"{side}Hand", 0.03, 380, 0.0007),
                   (f"{side}Leg", 0.05, 220, 0.0010), (f"{side}Foot", 0.04, 300, 0.0007)]
    for name, rad, dens, amp in joints:
        if name not in rig.data.bones:
            continue
        h, tl = B(rig, name)
        ax = (tl - h) / (np.linalg.norm(tl - h) + 1e-9)
        m = sphere(co, h, rad)
        t = (co - h) @ ax * dens + fbm(co * 60, 1) * 0.6
        disp -= crease_lines(t, 0.13) * m * amp
        disp += m * amp * 0.6  # knobbly knuckle
    # belly folds
    hips, _ = B(rig, "Hips")
    m_b = sphere(co, hips + np.array([0, -0.08, 0.06]), 0.09) * front
    disp -= crease_lines(co[:, 2] * 95 + fbm(co * 20, 2), 0.1) * m_b * 0.0012

    # 7. veins: thin ridges along noise level-sets on forearms, hands, temples
    m_v = np.zeros(len(co))
    for side in ("Left", "Right"):
        a, _ = B(rig, f"{side}ForeArm")
        h, _ = B(rig, f"{side}Hand")
        m_v = np.maximum(m_v, capsule(co, a, h, 0.05))
        m_v = np.maximum(m_v, sphere(co, h, 0.05))
    for s in (1, -1):
        m_v = np.maximum(m_v, sphere(co, head_h + np.array([0.07 * s, -0.04, 0.10]), 0.045))
    m_v = np.maximum(m_v, m_nk * 0.7)
    warp = co * np.array([60, 60, 25])
    vn = np.abs(gnoise(warp + fbm(co * 20, 2)[:, None] * 0.6))
    veins = np.exp(-(vn / 0.06) ** 2) * m_v
    disp += veins * 0.0008

    disp *= 2.2  # sculpt intensity (mid-frequency forms must survive a 2K bake)
    co += n * disp[:, None]
    hp.data.vertices.foreach_set("co", co.ravel())
    hp.data.update()

    # ---- regions attribute for shaders
    flush = np.zeros(len(co))
    for s in (1, -1):  # ear tips, nose, eye rims
        e = eyeL if s > 0 else eyeR
        hm = co[:, 2] > neck_h[2]
        tip = co[hm][np.argmax(co[hm][:, 0] * s)]
        flush = np.maximum(flush, sphere(co, tip, 0.09))
        flush = np.maximum(flush, sphere(co, e, 0.022) * 0.8)
    nose = co[np.argmin(co[:, 1])]
    flush = np.maximum(flush, sphere(co, nose, 0.035))
    for side in ("Left", "Right"):
        h, _ = B(rig, f"{side}Hand")
        flush = np.maximum(flush, sphere(co, h, 0.09) * 0.5)
    dirt = np.clip((0.35 - co[:, 2]) / 0.3, 0, 1) ** 1.5
    for side in ("Left", "Right"):
        h, _ = B(rig, f"{side}Hand")
        dirt = np.maximum(dirt, sphere(co, h + np.array([0, 0, -0.06]), 0.09))
        k, _ = B(rig, f"{side}Leg")
        dirt = np.maximum(dirt, sphere(co, k, 0.07) * 0.7)
    dirt *= np.clip(0.6 + 0.6 * fbm(co * 12, 3), 0, 1)
    # scars: a few long thin gashes (illusion only)
    scar = np.zeros(len(co))
    rng = np.random.default_rng(5)
    chest, _ = B(rig, "Spine2")
    centers = [head_h + np.array([0.03, -0.09, 0.07]), chest + np.array([0.05, -0.08, -0.02]),
               chest + np.array([-0.06, -0.08, -0.08]), B(rig, "LeftForeArm")[0],
               B(rig, "RightArm")[1] + np.array([0, -0.02, 0])]
    for c in centers:
        d = rng.normal(size=3)
        d /= np.linalg.norm(d)
        L = rng.uniform(0.03, 0.07)
        scar = np.maximum(scar, capsule(co, c - d * L, c + d * L, 0.006))
    hp.data.vertices.foreach_set("co", (co - n * (scar * 0.0008)[:, None]).ravel())
    attr = hp.data.color_attributes.new("regions", "FLOAT_COLOR", "POINT")
    rgba = np.stack([flush, dirt, np.clip(veins * 1.5, 0, 1), scar], 1)
    attr.data.foreach_set("color", rgba.ravel())
    return hp
