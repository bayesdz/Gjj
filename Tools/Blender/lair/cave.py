"""Procedural cave for Floor 1 (Goblin Lair).

Signed distance field (tunnels + chambers, rocky noise, walkable floor) -> marching cubes
-> world-scale box-projected UVs, floor/wall materials.
Coordinates: metres, Z up, the level runs from south (y=0) to north.
"""
import math

import numpy as np

from goblin.details import fbm, gnoise

# ---------------------------------------------------------------- layout
# Path nodes: name -> (x, y, floor_z, half_width, height)
NODES = {
    "A0": (0.0, 0.0, 0.0, 1.7, 3.0),     # entrance (dead end behind the player: collapsed rock)
    "A1": (2.0, 9.0, -0.3, 1.6, 2.8),
    "A2": (-1.5, 18.0, -0.7, 1.7, 3.0),
    "B": (1.0, 29.0, -1.0, 6.0, 4.8),    # guard chamber: first goblin
    "C1": (6.0, 39.0, -1.4, 1.8, 3.0),
    "C2": (10.0, 49.0, -1.9, 1.8, 3.2),
    "D": (8.0, 64.0, -2.2, 10.5, 7.5),   # main hall
    "E": (20.5, 67.0, -2.1, 3.8, 3.8),   # cage alcove
    "F1": (6.0, 78.0, -2.6, 1.8, 3.0),
    "F2": (3.0, 87.0, -3.6, 1.7, 3.0),
    "F3": (3.5, 95.0, -5.2, 1.9, 3.4),   # exit: down to floor 2
}
EDGES = [("A0", "A1"), ("A1", "A2"), ("A2", "B"), ("B", "C1"), ("C1", "C2"), ("C2", "D"),
         ("D", "E"), ("D", "F1"), ("F1", "F2"), ("F2", "F3")]
CHAMBERS = {"B", "D", "E"}

VOXEL = 0.2
BOUNDS = ((-10.0, 33.0), (-5.0, 100.0), (-8.0, 7.0))


def _seg_dist(p, a, b, wa, wb, ha, hb):
    """Distance to an elliptic capsule whose width/height interpolate along the segment."""
    ab = b[:2] - a[:2]
    L2 = float(ab @ ab)
    t = np.clip(((p[:, :2] - a[:2]) @ ab) / L2, 0, 1)
    c = a + t[:, None] * (b - a)
    w = wa + (wb - wa) * t
    h = ha + (hb - ha) * t
    # cross-section centre sits at floor + 0.45*h, vertical semi-axis 0.6*h
    zc = c[:, 2] + 0.45 * h
    d = p.copy()
    d[:, :2] -= c[:, :2]
    d[:, 2] = (p[:, 2] - zc) * (w / (0.6 * h))
    return np.linalg.norm(d, axis=1) - w, c[:, 2], t


def _chamber_dist(p, n):
    x, y, fz, w, h = NODES[n]
    c = np.array([x, y, fz + 0.4 * h])
    s = np.array([1.0, 0.85, w / (0.65 * h)])
    return np.linalg.norm((p - c) * s, axis=1) - w


def _smin(a, b, k=1.2):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


def field():
    (x0, x1), (y0, y1), (z0, z1) = BOUNDS
    xs = np.arange(x0, x1, VOXEL)
    ys = np.arange(y0, y1, VOXEL)
    zs = np.arange(z0, z1, VOXEL)
    X, Y, Z = np.meshgrid(xs, ys, zs, indexing="ij")
    p = np.stack([X.ravel(), Y.ravel(), Z.ravel()], 1).astype(np.float64)
    del X, Y, Z

    d = np.full(len(p), 1e3)
    floor = np.full(len(p), 1e3)
    best = np.full(len(p), 1e3)
    for a, b in EDGES:
        A, B = np.array(NODES[a][:3]), np.array(NODES[b][:3])
        wa, ha = NODES[a][3], NODES[a][4]
        wb, hb = NODES[b][3], NODES[b][4]
        # tunnels into chambers use the tunnel size, the chamber body is added separately
        if a in CHAMBERS:
            wa, ha = min(wa, 1.9), min(ha, 3.2)
        if b in CHAMBERS:
            wb, hb = min(wb, 1.9), min(hb, 3.2)
        ds, fz, _ = _seg_dist(p, A, B, wa, wb, ha, hb)
        d = _smin(d, ds, 0.8)
        closer = ds < best
        best = np.where(closer, ds, best)
        floor = np.where(closer, fz, floor)
    for n in CHAMBERS:
        dc = _chamber_dist(p, n)
        d = _smin(d, dc, 2.5)
        closer = dc < best
        best = np.where(closer, dc, best)
        floor = np.where(closer, NODES[n][2], floor)

    # rocky walls: noise only near the surface (cheap)
    near = np.abs(d) < 1.6
    q = p[near]
    rock = fbm(q * 0.35, 4) * 0.55 + np.abs(gnoise(q * 1.3)) * 0.18
    # horizontal strata
    rock += np.sin(q[:, 2] * 3.1 + gnoise(q * 0.5) * 2.0) * 0.06
    d[near] += rock

    # walkable floor: slightly uneven, never flat-looking
    shape = (len(xs), len(ys), len(zs))
    gx, gy = np.meshgrid(xs, ys, indexing="ij")
    g2 = np.stack([gx.ravel(), gy.ravel(), np.zeros(gx.size)], 1)
    fnoise = (fbm(g2 * 0.5, 3) * 0.10 + fbm(g2 * 3.0, 2) * 0.02).reshape(len(xs), len(ys))
    fl = floor.reshape(shape) + fnoise[:, :, None]
    sdf = np.maximum(d.reshape(shape), fl - p[:, 2].reshape(shape))
    return sdf.astype(np.float32), (x0, y0, z0)


def mesh():
    from skimage.measure import marching_cubes
    f, origin = field()
    verts, faces, normals, _ = marching_cubes(f, 0.0, spacing=(VOXEL,) * 3, gradient_direction="ascent",
                                              step_size=1, allow_degenerate=False)
    verts += np.array(origin)
    return verts, faces


def box_uv(co, n, tile):
    """World-space box projection; returns per-vertex UV for the dominant axis of n."""
    ax = np.argmax(np.abs(n), axis=1)
    uv = np.empty((len(co), 2))
    m = ax == 0
    uv[m] = co[m][:, [1, 2]]
    m = ax == 1
    uv[m] = co[m][:, [0, 2]]
    m = ax == 2
    uv[m] = co[m][:, [0, 1]]
    return uv / tile


def floor_height(x, y):
    """Approximate walkable floor at (x, y) from the layout (used before raycasts)."""
    best, fz = 1e9, 0.0
    for a, b in EDGES:
        A, B = np.array(NODES[a][:2]), np.array(NODES[b][:2])
        ab = B - A
        t = float(np.clip(((np.array([x, y]) - A) @ ab) / (ab @ ab), 0, 1))
        c = A + t * ab
        dd = math.dist(c, (x, y))
        if dd < best:
            best = dd
            fz = NODES[a][2] + (NODES[b][2] - NODES[a][2]) * t
    return fz
