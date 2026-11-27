"""Build one plinth variant in Painter: the material medieval_castle_wall on a fill at 2048, exported at 1024.

    python tools/painter/painter_plinth_run.py <name> [--set key=json ...] [--damp depth] [--target r,g,b]
        [--tilt 0.2] [--gloss 70]

The steps:
 1. The material alone is exported; the bed joints are read from its height; the rows are rolled so that a
    joint lies at Z 3.0 m (the string course of the wall; one tile is 2.222 m, V 0 at Z 0).
 2. With --damp, a second run adds a darkening band at the foot of the tile (a layer with a mask that wraps in
    both axes, centred where Z 0 falls after the roll).
 3. The export is rolled, converted to the engine form (R 0, G gloss, B occlusion) and measured. The
    set lands in <root>/plinth_<name> (root: --root, default v06) as t_rivendell_wall_rubble_a_d/_n/_s.png.
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "oneoff"))
import painter_stone_recipes as pr  # noqa: E402
import plinth_tools as pt  # noqa: E402
import tile_compose as tc  # noqa: E402

PLINTH = "t_rivendell_wall_rubble_a"
MAPS = ("baseColor", "normal", "height", "roughness", "ambientOcclusion", "metallic")
TILE_M = 2.2222
BASE = {"blocks_horizontal_amount": 2, "blocks_vertical_amount": 3, "moss_amount": 0, "lichen_amount": 0,
        "wall_flowers_amount": 1, "blocks_cracked_amount": 0.1}


def plinth_paths(root, name):
    """Every folder and file of one plinth variant under the textures folder `root` (v06 or v07)."""
    return {"cfg": "%s/cfg/plinth_%s_recipe.json" % (root, name), "raw": "%s/_export/plinth_%s_raw" % (root, name),
            "damp_layout": "%s/layout_plinth_damp_%s" % (root, name),
            "damp_export": "%s/_export/plinth_%s_damp" % (root, name),
            "rolled": "%s/_export/plinth_%s" % (root, name), "out": "%s/plinth_%s" % (root, name)}


def run(name, params, layers, layout, exp_dir, root=pr.V06):
    rec = {"tag": "pl_" + name, "layout": layout, "layers": layers,
           "base": {"material": "medieval_castle_wall", "params": params.get("p", {}), "colors": params.get("c", {})}}
    path = plinth_paths(root, name)["cfg"]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    json.dump(rec, open(path, "w"), indent=1)
    r = pr.run_painter(os.path.join(HERE, "painter_stone_build.py"),
                       {"lib": pr.LIB, "recipe": path, "out": exp_dir, "size": 1024, "work": 2048})
    if r["status"] != "Success":
        raise RuntimeError(json.dumps(r))


def roll_export(src, dst, shift):
    os.makedirs(dst, exist_ok=True)
    for m in MAPS:
        f = "rivendell_tile_%s.png" % m
        if os.path.exists(src + "/" + f):
            a = np.asarray(Image.open(src + "/" + f))
            Image.fromarray(pt.roll_rows(a, shift)).save(dst + "/" + f)


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("name")
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--damp", type=float, default=0.0)
    ap.add_argument("--damp-width", type=float, default=0.2)
    ap.add_argument("--target", default="157,149,140")
    ap.add_argument("--tilt", type=float, default=None)
    ap.add_argument("--top-z", type=float, default=pt.slant_length(3.0, 0.6),
                    help="height along the face where the joint goes: the plinth rises 3.0 m and leans back 0.6 m")
    ap.add_argument("--gloss", type=float, default=70.0)
    ap.add_argument("--root", default=pr.V06, help="the textures folder of the round (v06 or v07)")
    ap.add_argument("--sharpen", type=float, default=0.0, help="intensity of sharpen/sharpen on the base colour (0 = off)")
    a = ap.parse_args(argv)
    params = dict(BASE)
    colors = {}
    for kv in a.set:
        k, _, v = kv.partition("=")
        if k.startswith("c:"):
            colors[k[2:]] = json.loads(v)
        else:
            params[k] = json.loads(v)
    spec = {"p": params, "c": colors}
    paths = plinth_paths(a.root, a.name)
    base_exp = paths["raw"]
    base_layers = []
    if a.sharpen:
        base_layers.append({"name": "sharpen", "channels": ["BaseColor"], "filter": "sharpen/sharpen",
                            "params": {"intensity": a.sharpen}})
    run(a.name, spec, base_layers, "", base_exp, a.root)
    height = np.asarray(Image.open(base_exp + "/rivendell_tile_height.png").convert("L")).astype(float)
    rows = pt.joint_rows(height, min_gap=100)
    want = pt.row_for_height(a.top_z, TILE_M, 1024)
    shift = pt.shift_for_joint(rows, 1024, want)
    info = {"joint_rows": sorted(rows), "want_row": want, "shift": shift}
    src = base_exp
    if a.damp:
        layout = paths["damp_layout"]
        os.makedirs(layout, exist_ok=True)
        m = pt.damp_map(2048, centre=((-shift) % 1024) / 1024.0, half_width=a.damp_width)
        Image.fromarray(np.rint(m * 255).astype(np.uint8)).save(layout + "/layout_damp.png")
        layers = [pr.layer("damp", ["BaseColor"], {"color": [0.30, 0.27, 0.23]}, "Normal", a.damp, "damp"),
                  pr.layer("damp gloss", ["Roughness"], {"gray": 0.12}, "Subtract", 1.0, "damp")]
        src = paths["damp_export"]
        run(a.name, spec, base_layers + layers, layout, src, a.root)
    rolled = paths["rolled"]
    roll_export(src, rolled, shift)
    out = paths["out"]
    t = [int(x) for x in a.target.split(",")]
    import convert_painter_stone_sets as cv
    rep = cv.convert_one(rolled, out, PLINTH, "#%02x%02x%02x" % tuple(t), a.gloss, 14.0, "down")
    if a.tilt:
        n = np.asarray(Image.open("%s/%s_n.png" % (out, PLINTH)).convert("RGB"))
        Image.fromarray(tc.scale_normal_to_tilt(n, a.tilt)).save("%s/%s_n.png" % (out, PLINTH))
    d = np.asarray(Image.open("%s/%s_d.png" % (out, PLINTH)).convert("RGB"))
    n = np.asarray(Image.open("%s/%s_n.png" % (out, PLINTH)).convert("RGB"))
    s = np.asarray(Image.open("%s/%s_s.png" % (out, PLINTH)).convert("RGB"))
    st = tc.tile_stats(d, 461.0)
    st["red_minus_blue"] = round(st["mean"][0] - st["mean"][2], 1)
    st["normal_tilt"] = round(tc.mean_tilt(n), 3)
    st["gloss_mean"] = round(float(s[..., 1].mean()), 1)
    st["ao_mean"] = round(float(s[..., 2].mean()), 1)
    st["seam_ratio_d"] = round(cv.seam_ratio(d), 2)
    st["seam_ratio_n"] = round(cv.seam_ratio(n), 2)
    res = {"name": a.name, "sharpen": a.sharpen, "params": params, "colors": colors, "damp": a.damp, "roll": info, "stats": st}
    json.dump(res, open(out + "/plinth_report.json", "w"), indent=1)
    print(json.dumps(res))


if __name__ == "__main__":
    main(sys.argv[1:])
