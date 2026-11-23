"""Download CC0 assets from Poly Haven (polyhaven.com) into a local cache.

Textures: diffuse, OpenGL normal, ARM (AO/Rough/Metal) and displacement as JPG.
Models:   glTF with their textures.
"""
import json
import os
import urllib.request

CACHE = os.environ.get("PH_CACHE", os.path.join(os.path.dirname(os.path.dirname(__file__)), "_ph_cache"))
API = "https://api.polyhaven.com/files/"
UA = {"User-Agent": "gamejam-asset-pipeline"}


def _get(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=120) as r, open(path + ".part", "wb") as f:
        f.write(r.read())
    os.replace(path + ".part", path)
    return path


def _files(asset):
    p = os.path.join(CACHE, "_api", asset + ".json")
    if not os.path.exists(p):
        os.makedirs(os.path.dirname(p), exist_ok=True)
        req = urllib.request.Request(API + asset, headers=UA)
        with urllib.request.urlopen(req, timeout=60) as r:
            open(p, "wb").write(r.read())
    return json.load(open(p))


def texture(asset, res="2k", maps=("Diffuse", "nor_gl", "arm", "Displacement")):
    """Returns {map: local_path}."""
    d = _files(asset)
    out = {}
    for m in maps:
        if m not in d:
            continue
        entry = d[m][res]["jpg"] if "jpg" in d[m][res] else d[m][res]["png"]
        url = entry["url"]
        out[m] = _get(url, os.path.join(CACHE, "tex", asset, os.path.basename(url)))
    return out


def model(asset, res="1k"):
    """Returns the local .gltf path (textures downloaded next to it)."""
    d = _files(asset)
    g = d["gltf"][res]["gltf"]
    base = os.path.join(CACHE, "models", asset)
    path = _get(g["url"], os.path.join(base, os.path.basename(g["url"])))
    for rel, inc in g.get("include", {}).items():
        _get(inc["url"], os.path.join(base, rel))
    return path


if __name__ == "__main__":
    import sys
    for a in sys.argv[1:]:
        print(a, model(a))
