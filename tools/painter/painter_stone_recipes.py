"""Host side of the Painter stone work (round 2b): recipes, the run through Painter, the conversion and the numbers.

A recipe is the dict that tools/painter/painter_stone_build.py reads. `recipe(name, **over)` builds it for a
candidate; `run(name)` writes it, runs Painter, converts the export to the engine form with
tools/oneoff/convert_painter_stone_sets.py (diffuse without the occlusion multiply: ao_strength 0, because
the stack carries its own joint occlusion) and prints the stats beside the preview's.

Run:  python tools/painter/painter_stone_recipes.py run <name> [--set key=json ...]
      python tools/painter/painter_stone_recipes.py stats <name>
"""
import argparse
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "oneoff"))
import tile_compose as tc  # noqa: E402

WORK = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell"
V06 = WORK + "/textures/v06"
V07 = WORK + "/textures/v07"
LIB = "E:/repos/taom-kingdom-kits/tools/painter/painter_tile_lib.py"
BUILD = "E:/repos/taom-kingdom-kits/tools/painter/painter_stone_build.py"
SET_NAME = "t_rivendell_wall_stone_a"
MATERIAL = {"material": "smoothed_limestone_rock_surface",
            "params": {"normal_intensity": 0.1, "height_range": 0.3, "contrast": -0.6},
            "colors": {"color": [0.82, 0.80, 0.74]}}
TARGET = (195.0, 187.0, 178.0)


def lin(c):
    """sRGB 0..1 to linear light."""
    c = np.asarray(c, dtype=float)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def enc(c):
    """Linear light to sRGB 0..1."""
    c = np.asarray(c, dtype=float)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.maximum(c, 0) ** (1 / 2.4) - 0.055)


def add_colour(base_mean, target):
    """The sRGB colour of a fill that Painter adds (in linear light) to a base of mean `base_mean` (0..255 per
    channel) to bring it to `target`. A negative step is cut to 0 (use the subtract form then)."""
    step = lin(np.asarray(target, dtype=float) / 255.0) - lin(np.asarray(base_mean, dtype=float) / 255.0)
    return [round(float(v), 4) for v in enc(np.maximum(step, 0.0))]


def layer(name, channels, source, blend="Normal", opacity=1.0, mask=None):
    d = {"name": name, "channels": list(channels), "source": source, "blend": blend, "opacity": opacity}
    if mask:
        d["mask"] = mask
    return d


# The parity stack: the layers of alt_calm's painter_layers, with the opacities that Painter's linear
# blending needs (found by runs; see the journal).
PARITY = {
    "colour": [0.49, 0.45, 0.43], "tone": 0.32, "grain": 0.5, "cloud": 0.22, "stain": 0.08, "ao": 0.15, "edge": 0.20,
    "chip": 0.30, "relief": 0.40, "gloss_shift": 0.085, "joint_rough": 0.137, "chip_rough": 0.078,
    "ao_channel": 0.3, "joint": [0.915, 0.901, 0.878], "streak": None, "lower": None,
}


# Round 3 (smooth dressed stone): over PARITY. A lower material relief, a third of the streaks, a lower band of
# 0.08, clouds of 0.10, a darker warm joint with little joint occlusion, the full relief layer (its opacity
# cannot pass 1), and a warm ivory of luminance 190 with red 24 above blue.
ROUND3 = {"material_params": {"normal_intensity": 0.05, "height_range": 0.15},
          "streak": {"color": [0.5, 0.46, 0.4], "opacity": 0.1}, "lower": {"color": [0.5, 0.47, 0.42], "opacity": 0.08},
          "cloud": 0.10, "target": [198, 189, 174], "edge": 0.35, "ao": 0.05, "relief": 1.0,
          "joint": [0.80, 0.788, 0.768]}


def recipe(name, layout, p):
    """The recipe dict for parameters `p` (see PARITY for the keys) and a layout folder. Optional keys:
    material_params (a dict that replaces single parameters of the base material), edge_colour, chip_colour."""
    B = ["BaseColor"]
    base = dict(MATERIAL, params=dict(MATERIAL["params"], **p.get("material_params", {})))
    layers = [layer("colour", B, {"color": p["colour"]}, "LinearDodge")]
    if p.get("grain"):
        layers.append({"name": "grain", "channels": B, "filter": "sharpen/sharpen",
                       "params": {"intensity": p["grain"]}})
    layers += [layer("tone", B, {"map": "id"}, "Overlay", p["tone"]),
               layer("cloud", B, {"map": "cloud"}, "Overlay", p["cloud"])]
    if p.get("streak"):
        layers.append(layer("streaks", B, {"color": p["streak"]["color"]}, "Normal", p["streak"]["opacity"], "streak"))
    if p.get("lower"):
        layers.append(layer("lower edge", B, {"color": p["lower"]["color"]}, "Normal", p["lower"]["opacity"], "lower"))
    layers += [layer("stain", B, {"color": [0.55, 0.5, 0.44]}, "Normal", p["stain"], "stain"),
               layer("joint", B, {"color": p["joint"]}, "Multiply", 1.0, "joint"),
               layer("joint ao", B, {"map": "ao"}, "Multiply", p["ao"])]
    if p.get("halo"):
        layers.append(layer("halo", B, {"map": "halo"}, "Multiply", p["halo"]))
    layers += [layer("edge light", B, {"color": p.get("edge_colour", [1.0, 0.97, 0.9])}, "Screen", p["edge"],
                     "edgelight"),
               layer("chip light", B, {"color": p.get("chip_colour", [1.0, 0.97, 0.9])}, "Screen", p["chip"],
                     "chip"),
               layer("relief", ["Height"], {"map": "height"}, "LinearDodge", p["relief"])]
    if p.get("face_map"):
        layers.append(layer("face relief", ["Height"], {"map": "face"}, "LinearDodge", 1.0))
    layers += [layer("gloss shift", ["Roughness"], {"gray": p["gloss_shift"]}, "Subtract"),
               layer("joint rough", ["Roughness"], {"gray": p["joint_rough"]}, "LinearDodge", 1.0, "joint"),
               layer("chip rough", ["Roughness"], {"gray": p["chip_rough"]}, "LinearDodge", 1.0, "chip"),
               layer("occlusion", ["AO"], {"map": "ao"}, "Multiply", p["ao_channel"])]
    return {"tag": name, "layout": layout, "base": base, "layers": layers}


def run_painter(script, args, timeout=3000):
    cmd = [sys.executable, os.path.join(HERE, "painter_run.py"), script]
    for k, v in args.items():
        cmd += ["--arg", "%s=%s" % (k, v if isinstance(v, str) else json.dumps(v))]
    cmd += ["--timeout", str(timeout)]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode:
        raise RuntimeError((r.stdout + r.stderr)[-1500:])
    return json.loads(r.stdout)


def convert(export, out, target=TARGET, gloss_mean=100.0, gloss_std=14.0):
    import convert_painter_stone_sets as cv
    hexs = "#%02x%02x%02x" % tuple(int(round(v)) for v in target)
    return cv.convert_one(export, out, SET_NAME, hexs, gloss_mean, gloss_std, "down", ao_strength=0.0)


def stats(folder, ppm=461.0):
    d = np.asarray(Image.open("%s/%s_d.png" % (folder, SET_NAME)).convert("RGB"))
    n = np.asarray(Image.open("%s/%s_n.png" % (folder, SET_NAME)).convert("RGB"))
    s = np.asarray(Image.open("%s/%s_s.png" % (folder, SET_NAME)).convert("RGB"))
    st = tc.tile_stats(d, ppm)
    st["red_minus_blue"] = round(st["mean"][0] - st["mean"][2], 1)
    st["normal_tilt"] = round(tc.mean_tilt(n), 3)
    st["gloss_mean"] = round(float(s[..., 1].mean()), 1)
    return st


def _blocks(layout):
    meta = json.load(open(layout + "/layout.json"))
    return meta["blocks"], meta.get("info") or {}


def judge(folder, layout, ppm=461.0, margin_px=10):
    """The numbers of a set against the folder of its layout: the face tilt (mean and median normal tilt inside
    the blocks, `margin_px` of the 1024 px tile away from every edge), the tilt of the whole tile, the mean
    luminance of each course band and of four vertical bands, with their spreads (largest minus smallest), the
    mean luminance of each block (round 4: its inside 20 px from the edges) with its spread, and the contrast of
    the soft tone inside the blocks (the clouds, in levels)."""
    blocks, info = _blocks(layout)
    d = np.asarray(Image.open("%s/%s_d.png" % (folder, SET_NAME)).convert("RGB"))
    n = np.asarray(Image.open("%s/%s_n.png" % (folder, SET_NAME)).convert("RGB"))
    size = int(info.get("size") or d.shape[0])
    lum = tc.luminance(d)
    cm, bm = tc.course_means(lum, blocks, size), tc.band_means(lum, 4)
    blm = tc.block_means(lum, blocks, size, 20 * d.shape[0] / 1024.0)
    return {"face_tilt": round(tc.face_tilt(n, blocks, size, margin_px), 3),
            "face_tilt_median": round(tc.face_tilt_median(n, blocks, size, margin_px), 3),
            "tile_tilt": round(tc.mean_tilt(n), 3),
            "course_means": [round(v, 1) for v in cm], "course_spread": round(tc.spread(cm), 1),
            "band_means": [round(v, 1) for v in bm], "band_spread": round(tc.spread(bm), 1),
            "block_means": [round(v, 1) for v in blm], "block_spread": round(tc.spread(blm), 1),
            "cloud_contrast": round(tc.cloud_contrast(lum, blocks, size, 24.0 * d.shape[0] / 1024.0,
                                                      48 * d.shape[0] / 1024.0), 2)}


def layout_edge(layout, **kw):
    """How straight the arris of every block is on the layout height map (chipped blocks left out), and how
    many blocks carry a chip. kw goes to tile_compose.edge_straightness (tol, corner, reach)."""
    blocks, info = _blocks(layout)
    h = np.asarray(Image.open(layout + "/layout_height.png")).astype(float)
    h = h / (65535.0 if h.max() > 255 else 255.0)
    chipped = {c["block"] for c in info.get("chips", [])}
    kw.setdefault("level", 0.5 * float(info.get("face_level", 1.0)))
    out = tc.edge_straightness(h, blocks, skip=chipped, **kw)
    out.update({"blocks": len(blocks), "chipped_blocks": len(chipped)})
    return out


def diff_sheet(ref_folder, new_folder, out_png, layout_json=None):
    """Reference d, new d, |difference| x 4, and the same for the normal's red channel, in one picture.
    Returns the correlation of the block mean luminance (when a layout.json is given)."""
    rd = np.asarray(Image.open("%s/%s_d.png" % (ref_folder, SET_NAME)).convert("RGB"))
    nd = np.asarray(Image.open("%s/%s_d.png" % (new_folder, SET_NAME)).convert("RGB"))
    rn = np.asarray(Image.open("%s/%s_n.png" % (ref_folder, SET_NAME)).convert("RGB"))
    nn = np.asarray(Image.open("%s/%s_n.png" % (new_folder, SET_NAME)).convert("RGB"))
    dd = np.clip(np.abs(rd.astype(float) - nd) * 4, 0, 255).astype(np.uint8)
    dn = np.clip(np.abs(rn.astype(float) - nn) * 8, 0, 255).astype(np.uint8)
    sheet = np.concatenate([np.concatenate([rd, nd, dd], axis=1), np.concatenate([rn, nn, dn], axis=1)], axis=0)
    Image.fromarray(sheet).resize((sheet.shape[1] // 2, sheet.shape[0] // 2), Image.LANCZOS).save(out_png)
    out = {"mean_abs_diff_d": round(float(np.abs(rd.astype(float) - nd).mean()), 2),
           "mean_abs_diff_n": round(float(np.abs(rn.astype(float) - nn).mean()), 2)}
    if layout_json:
        blocks = json.load(open(layout_json))["blocks"]
        la, lb = tc.luminance(rd), tc.luminance(nd)
        size = la.shape[0]
        ma, mb = [], []
        for b in blocks:
            cols = [x % size for x in range(b["x0"] + 20, b["x1"] - 20)]
            ma.append(la[b["y0"] + 20:b["y1"] - 20][:, cols].mean())
            mb.append(lb[b["y0"] + 20:b["y1"] - 20][:, cols].mean())
        out["block_mean_lum_correlation"] = round(float(np.corrcoef(ma, mb)[0, 1]), 3)
    return out


PARITY_LIMITS = {"lum_mean": 2.0, "red_minus_blue": 2.0, "joint_contrast": 4.0, "joint_width_px": 1.0,
                 "lum_p5_p95": 4.0}


def parity(ours, ref):
    """Each parity quantity as (ours, reference, limit, ok)."""
    return {k: {"ours": ours[k], "ref": ref[k], "limit": lim, "ok": abs(ours[k] - ref[k]) <= lim}
            for k, lim in PARITY_LIMITS.items()}


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=("run", "stats", "dressed"))
    ap.add_argument("name")
    ap.add_argument("--layout", default=None)
    ap.add_argument("--root", default=V06, help="the textures folder of the round (v06 or v07)")
    ap.add_argument("--round3", action="store_true", help="start from the round 3 values (ROUND3)")
    ap.add_argument("--set", action="append", default=[])
    a = ap.parse_args(argv)
    p = dict(PARITY)
    if a.round3:
        p.update(ROUND3)
    for kv in a.set:
        k, _, v = kv.partition("=")
        p[k] = json.loads(v)
    folder = "%s/%s" % (a.root, a.name)
    layout = a.layout or "%s/layout_%s" % (a.root, a.name.split("_")[-1])
    cfg = a.root + "/cfg"
    os.makedirs(cfg, exist_ok=True)
    if a.cmd == "dressed":
        # The stone with no joint layers: the material, the colour step and the grain, exported at 1024.
        rec = recipe(a.name, layout, p)
        rec["layers"] = [l for l in rec["layers"] if l["name"] in ("colour", "grain")]
        path = "%s/%s_dressed_recipe.json" % (cfg, a.name)
        json.dump(rec, open(path, "w"), indent=1)
        print(json.dumps(run_painter(os.path.join(HERE, "painter_stone_build.py"),
                                     {"lib": LIB, "recipe": path, "out": folder, "size": 1024, "work": 2048})["status"]))
        return
    if a.cmd == "run":
        rec = recipe(a.name, layout, p)
        path = "%s/%s_recipe.json" % (cfg, a.name)
        json.dump(rec, open(path, "w"), indent=1)
        exp = "%s/_export/%s" % (a.root, a.name)
        print(json.dumps(run_painter(os.path.join(HERE, "painter_stone_build.py"),
                                     {"lib": LIB, "recipe": path, "out": exp, "size": 1024, "work": 2048})["status"]))
        print(json.dumps(convert(exp, folder, tuple(p.get("target", TARGET)), p.get("gloss_mean", 100.0),
                                 p.get("gloss_std", 14.0))["diffuse_mean"]))
    out = {"stats": stats(folder)}
    if os.path.exists(layout + "/layout.json"):
        out["judge"] = judge(folder, layout)
        out["edge"] = layout_edge(layout)
    print(json.dumps(out, indent=1))


if __name__ == "__main__":
    main()
