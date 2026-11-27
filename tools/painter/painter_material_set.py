"""One downloaded Painter material as one tiling set of the Rivendell kit (round 4: the roof slate and the heap).

    python tools/painter/painter_material_set.py run <name> --cfg <set json> [--root <v08 folder>]
        [--set key=json ...] [--param key=json ...] [--colour key=json ...]

The set json holds one candidate:
    {"set": "t_rivendell_roof_slate_a", "material": "slate_tile", "params": {...}, "colors": {...},
     "sharpen": 0.5, "layers": [...], "layout": "", "target": [57, 68, 97], "gloss_mean": 130, "gloss_std": 14,
     "tilt": null, "ao_strength": 0.0, "targets": {"lum_mean": [60, 75]}, "ppm": 461, "cells": [10, 8, true]}
`layers` are the layers of tools/painter/painter_stone_build.py (a Raw map source reads <layout>/layout_<key>.png).

The steps: the recipe goes to Painter (texture set 2048, export at 1024, normal as Normal_DirectX); the export is
converted to the engine form <set>_d.png (each channel scaled to the target mean), <set>_n.png (green down) and
<set>_s.png (R 0, G gloss = 255 minus roughness, B occlusion); the numbers are measured and checked against the
ranges in "targets"; a 3 by 3 tiling and a lit 3 by 3 picture are written. Folders under --root: cfg, _export,
<name> (the set, a report and the pictures).
"""
import argparse
import copy
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "oneoff"))
import painter_stone_recipes as pr  # noqa: E402
import tile_compose as tc  # noqa: E402

DEFAULT_ROOT = pr.WORK + "/textures/v08"
TILE_PX = 1024


def parse_assignments(items):
    """["a=1", "b=[1,2]"] to {"a": 1, "b": [1, 2]}. The value is JSON; a bare word is an error."""
    out = {}
    for item in items:
        key, eq, val = item.partition("=")
        if not eq or not key:
            raise ValueError("expected key=json, got %r" % (item,))
        try:
            out[key] = json.loads(val)
        except json.JSONDecodeError as e:
            raise ValueError("the value of %s is not JSON: %r (%s)" % (key, val, e))
    return out


def apply_overrides(cfg, sets):
    """A copy of `cfg` with each key of `sets` replaced. A dotted key ("params.tile_x") reaches into a dict."""
    out = copy.deepcopy(cfg)
    for key, val in sets.items():
        node = out
        parts = key.split(".")
        for p in parts[:-1]:
            node = node.setdefault(p, {})
        node[parts[-1]] = val
    return out


def gain_to_mean(img, target, rounds=8):
    """Scale each channel of an RGB array (0..255) so that its mean is `target`; clipping at 255 is corrected
    by more rounds. A gain keeps the ratios inside a channel, so joints stay as dark as the face is bright."""
    out = np.asarray(img, dtype=float).copy()
    t = np.asarray(target, dtype=float)
    for _ in range(rounds):
        mean = np.clip(out, 0, 255).reshape(-1, 3).mean(0)
        out = np.clip(out * (t / np.maximum(mean, 1e-6)), 0, 255)
    return out


def build_recipe(cfg, tag, layout):
    """The recipe dict for tools/painter/painter_stone_build.py: the material as the base fill, then the
    sharpen filter on its base colour (a 2048 set exported at 1024 needs it), then the extra layers."""
    base = {"material": cfg["material"], "params": dict(cfg.get("params", {})),
            "colors": dict(cfg.get("colors", {}))}
    layers = []
    if cfg.get("sharpen"):
        layers.append({"name": "sharpen", "channels": ["BaseColor"], "filter": "sharpen/sharpen",
                       "params": {"intensity": cfg["sharpen"]}})
    layers += copy.deepcopy(cfg.get("layers", []))
    if cfg.get("uv"):
        base["uv"] = dict(cfg["uv"])
    return {"tag": tag, "layout": layout, "base": base, "layers": layers}


def build_script(cfg):
    """The Painter-side script for a candidate: the material builder when a layer is a second material or the
    base is turned, scaled or moved; the proven stone builder otherwise."""
    multi = bool(cfg.get("uv")) or any("material" in l for l in cfg.get("layers", []))
    return os.path.join(HERE, "painter_material_build.py" if multi else "painter_stone_build.py")


def paths(root, name):
    return {"cfg": "%s/cfg/%s_set.json" % (root, name), "recipe": "%s/cfg/%s_recipe.json" % (root, name),
            "export": "%s/_export/%s" % (root, name), "out": "%s/%s" % (root, name)}


def engine_files(set_name):
    return ["%s_%s.png" % (set_name, k) for k in ("d", "n", "s")]


def check_targets(stats, targets):
    """One row per named range: {"name", "value", "lo", "hi", "met"}. A missing value is not met."""
    rows = []
    for name, (lo, hi) in targets.items():
        v = stats.get(name)
        rows.append({"name": name, "value": v, "lo": lo, "hi": hi, "met": v is not None and lo <= v <= hi})
    return rows


def tiled(img, k=3):
    return np.tile(np.asarray(img), (k, k, 1))


def cell_tone_spread(d, rows, cols, stagger=False, margin=0.2):
    """Largest minus smallest mean luminance of the cells of a rows x cols grid over the tile (the tone per
    slate). With `stagger` the odd rows are shifted by half a cell (wrapped). The outer `margin` of each cell
    is left out, so a joint does not count."""
    lum = tc.luminance(np.asarray(d, dtype=float))
    h, w = lum.shape
    ch, cw = h / rows, w / cols
    means = []
    for r in range(rows):
        row = np.roll(lum, int(round(cw / 2)) if (stagger and r % 2) else 0, axis=1)
        y0, y1 = int(round(r * ch + margin * ch)), max(int(round((r + 1) * ch - margin * ch)), int(round(r * ch)) + 1)
        for c in range(cols):
            x0, x1 = int(round(c * cw + margin * cw)), max(int(round((c + 1) * cw - margin * cw)), int(round(c * cw)) + 1)
            means.append(float(row[y0:y1, x0:x1].mean()))
    return round(max(means) - min(means), 1)


def direction_ratio(d):
    """How directional a diffuse is: the larger over the smaller of the mean absolute luminance step along x and
    along y. About 1 for a field with no direction; courses or strata push it up."""
    lum = tc.luminance(np.asarray(d, dtype=float))
    gx, gy = float(np.abs(np.diff(lum, axis=1)).mean()), float(np.abs(np.diff(lum, axis=0)).mean())
    return max(gx, gy) / max(min(gx, gy), 1e-6)


def extra_measures(d, n, s):
    """Numbers that the tile statistics do not carry: blue minus red, the whole-tile normal tilt, the gloss
    and occlusion means, the share of texels that tilt more than 0.2."""
    mean = np.asarray(d, dtype=float).reshape(-1, 3).mean(0)
    tilt = tc.tilt_map(n)
    return {"blue_minus_red": round(float(mean[2] - mean[0]), 1), "green_minus_red": round(float(mean[1] - mean[0]), 1), "red_minus_blue": round(float(mean[0] - mean[2]), 1),
            "normal_tilt": round(float(tilt.mean()), 3), "tilt_over_0_2": round(float((tilt > 0.2).mean()), 3),
            "gloss_mean": round(float(np.asarray(s)[..., 1].mean()), 1), "direction_ratio": round(direction_ratio(d), 2),
            "ao_mean": round(float(np.asarray(s)[..., 2].mean()), 1)}


def convert_set(export, out, set_name, target, gloss_mean, gloss_std=14.0, tilt=None, ao_strength=0.0):
    """One export folder to the engine form in `out`. Returns the three arrays (d, n, s) as uint8."""
    import convert_painter_stone_sets as cv
    os.makedirs(out, exist_ok=True)

    def f(m):
        return os.path.join(export, "rivendell_tile_%s.png" % m)

    base = cv.load(f("baseColor"), "RGB").astype(float)
    ao = cv.load(f("ambientOcclusion"), "L")
    d = gain_to_mean(cv.apply_ao(base, ao, ao_strength), target)
    n = cv.load(f("normal"), "RGB")
    if tilt:
        n = tc.scale_normal_to_tilt(n, tilt)
    s = cv.spec_from_maps(cv.load(f("roughness"), "L"), ao, gloss_mean, gloss_std)
    arrays = [np.clip(np.rint(a), 0, 255).astype(np.uint8) for a in (d, n, s)]
    for name, a in zip(engine_files(set_name), arrays):
        Image.fromarray(a).save(os.path.join(out, name))
    return tuple(arrays)


def measure(d, n, s, cfg):
    st = tc.tile_stats(d, cfg.get("ppm", 461.0))
    st.update(extra_measures(d, n, s))
    if cfg.get("cells"):
        rows, cols, stagger = cfg["cells"]
        st["slate_tone_spread"] = cell_tone_spread(d, rows, cols, stagger)
    if cfg.get("scale_layout"):
        import json as _json
        import scale_layout as sl
        folder = cfg["scale_layout"]
        owner = np.asarray(Image.open(folder + "/layout_owner.png"))
        ao = np.asarray(Image.open(folder + "/layout_ao.png")) / 255.0
        info = _json.load(open(folder + "/layout.json"))["info"]
        ms = sl.measure_scales(tc.luminance(np.asarray(d, dtype=float)), owner, info["outlier_ids"], ao)
        st.update({"slate_" + k: v for k, v in ms.items()})
    import convert_painter_stone_sets as cv
    st["seam_ratio_d"] = round(cv.seam_ratio(d), 2)
    st["seam_ratio_n"] = round(cv.seam_ratio(n), 2)
    return st


def write_pictures(out, name, d, n, s):
    """The 3 by 3 tiling of the diffuse and the lit 3 by 3 picture, both at 1536 px wide."""
    pics = {"tiling_3x3": tiled(d, 3), "lit_3x3": tc.lit_view(d, n, s, 3)}
    files = {}
    for key, a in pics.items():
        files[key] = "%s/%s_%s.png" % (out, name, key)
        Image.fromarray(a).resize((1536, 1536), Image.LANCZOS).save(files[key])
    return files


def run(name, cfg, root):
    p = paths(root, name)
    os.makedirs(os.path.dirname(p["cfg"]), exist_ok=True)
    json.dump(cfg, open(p["cfg"], "w"), indent=1)
    rec = build_recipe(cfg, name, cfg.get("layout", ""))
    json.dump(rec, open(p["recipe"], "w"), indent=1)
    r = pr.run_painter(build_script(cfg), {"lib": pr.LIB, "recipe": p["recipe"], "out": p["export"], "size": TILE_PX,
                                  "work": 2048})
    if r["status"] != "Success":
        raise RuntimeError(json.dumps(r))
    if r.get("mask_params"):
        print("mask parameters:", json.dumps(r["mask_params"]), file=sys.stderr)
    d, n, s = convert_set(p["export"], p["out"], cfg["set"], cfg["target"], cfg.get("gloss_mean", 100.0),
                          cfg.get("gloss_std", 14.0), cfg.get("tilt"), cfg.get("ao_strength", 0.0))
    st = measure(d, n, s, cfg)
    rows = check_targets(st, cfg.get("targets", {}))
    pics = write_pictures(p["out"], name, d, n, s)
    rep = {"name": name, "set": cfg["set"], "stats": st, "targets": rows, "pictures": pics, "cfg": p["cfg"]}
    json.dump(rep, open(p["out"] + "/report.json", "w"), indent=1)
    return rep


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=("run",))
    ap.add_argument("name")
    ap.add_argument("--cfg", required=True)
    ap.add_argument("--root", default=DEFAULT_ROOT)
    ap.add_argument("--set", action="append", default=[], help="key=json, a dotted key reaches into a dict")
    ap.add_argument("--param", action="append", default=[], help="a material parameter: key=json")
    ap.add_argument("--colour", action="append", default=[], help="a material colour parameter: key=[r,g,b] (sRGB 0..1)")
    a = ap.parse_args(argv)
    cfg = json.load(open(a.cfg))
    sets = parse_assignments(a.set)
    sets.update({"params." + k: v for k, v in parse_assignments(a.param).items()})
    sets.update({"colors." + k: v for k, v in parse_assignments(a.colour).items()})
    rep = run(a.name, apply_overrides(cfg, sets), a.root)
    print(json.dumps(rep, indent=1))


if __name__ == "__main__":
    main()
