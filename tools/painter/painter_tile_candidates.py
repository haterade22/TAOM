"""Build smooth-stone candidates for the Rivendell wall kit (Painter must be open on the plane project).

    python tools/painter/painter_tile_candidates.py <candidates.json> [name ...] [--steps wall,dressed,...]

Per candidate (a key of the JSON file): the wall stone set, the dressed stone (the same stone without
joints) and the trim sheet built from it, the plinth set, params.json and a flat sheet beside the Gondor
control at the same pixels per metre. Steps: layout, wall, dressed, trim, plinth, sheet.
The config form is shown in the tests (tools/tests/test_painter_tile_candidates.py) and in
painter_tile_build.py. Pure steps are tested; the Painter and Blender runs are not.
"""
import copy
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "oneoff"))
import convert_painter_stone_sets as conv  # noqa: E402

WORK = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell"
EXPORTS = WORK + "/painter_lab/exports04"
LAYOUTS = WORK + "/painter_lab/layouts"
OUT = WORK + "/textures/v04_candidates"
V03 = WORK + "/textures/v03"
OLD_EXPORTS = WORK + "/textures/v02_painter/exports"
GONDOR = ("E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/AssetSources/Scenes/Gondor/"
          "textures/t_gondor_bricks_large_a_dirty_d2.png")
WALL, PLINTH, TRIM = "t_rivendell_wall_stone_a", "t_rivendell_wall_rubble_a", "t_rivendell_wall_trim_a"
TILE_M = 1024 / 461.0  # one tile covers 2.222 m of wall


def wall_spec(name, cfg, exports=EXPORTS, layouts=LAYOUTS):
    spec = {"name": name, "out": "%s/%s/wall" % (exports, name), "size": 1024, "normal": "Normal_DirectX",
            "base": copy.deepcopy(cfg["base"]), "layout": None}
    if cfg.get("layout"):
        spec["layout"] = "%s/%s" % (layouts, cfg["layout"]["name"])
        for key in ("tone", "stain", "joint", "ao"):
            if key in cfg:
                spec[key] = copy.deepcopy(cfg[key])
    return spec


def dressed_spec(name, cfg, exports=EXPORTS):
    """The same stone without joints: the base alone, with the config's dressed_params merged in."""
    base = copy.deepcopy(cfg.get("dressed_base", cfg["base"]))
    base["params"] = {**base.get("params", {}), **cfg.get("dressed_params", {})}
    return {"name": name + "_dressed", "out": "%s/%s/dressed" % (exports, name), "size": 1024,
            "normal": "Normal_DirectX", "base": base, "layout": None}


def engine_trim(folder, name, gloss_mean):
    """The trim builder writes _n (green up) and _n_flipgreen (green down). Keep the engine form only
    and move the gloss of _s to the wall's."""
    flip = os.path.join(folder, name + "_n_flipgreen.png")
    if os.path.exists(flip):
        os.replace(flip, os.path.join(folder, name + "_n.png"))
    sp = os.path.join(folder, name + "_s.png")
    conv.save(conv.retarget_gloss(conv.load(sp, "RGB").astype(float), gloss_mean), sp)


def convert(cfg, export_dir, out, name, target, normal_form="down", gloss=None):
    g = gloss or cfg.get("gloss", {})
    return conv.convert_one(export_dir, out, name, target, g.get("mean", 100.0), g.get("std", 14.0), normal_form)


def flat_sheet(d_png, out_png, extent_m=3.0):
    """Our tile and the Gondor control side by side, 3 m x 3 m each, at the control's pixels per metre."""
    ctl = np.asarray(Image.open(GONDOR).convert("RGB"))
    ppm = ctl.shape[0] / extent_m
    tile_px = int(round(TILE_M * ppm))
    t = np.asarray(Image.open(d_png).convert("RGB").resize((tile_px, tile_px), Image.LANCZOS))
    n = int(round(extent_m * ppm))
    big = np.tile(t, (int(np.ceil(n / tile_px)) + 1,) * 2 + (1,))[:n, :n]
    gap = np.full((n, 24, 3), 255, np.uint8)
    sheet = np.concatenate([big, gap, ctl[:n, :n]], axis=1)
    Image.fromarray(sheet).save(out_png)
    stats = lambda a: {"mean": [round(float(v), 1) for v in a.reshape(-1, 3).mean(0)],
                       "std": [round(float(v), 1) for v in a.reshape(-1, 3).std(0)]}
    return {"px_per_m": round(ppm, 1), "ours": stats(big), "control": stats(ctl[:n, :n])}


def run_painter(spec):
    import painter_tile_run_spec as rs
    if rs.run_spec(spec) != 0:
        raise RuntimeError("Painter run failed for " + spec["name"])


def build(name, cfg, steps):
    cdir = "%s/%s" % (OUT, name)
    os.makedirs(cdir, exist_ok=True)
    log = {}
    if "layout" in steps and cfg.get("layout"):
        import block_layout as bl
        lay = cfg["layout"]
        blocks = bl.plan_blocks(lay["heights"], lay["lengths"], 1024, lay["seed"], lay["stagger"])
        bl.write("%s/%s" % (LAYOUTS, lay["name"]),
                 bl.render(blocks, 1024, lay.get("joint", 2), lay.get("arris", 2), lay["seed"]), blocks)
    wall_target, trim_target = cfg.get("target", "d0c9ba"), cfg.get("trim_target", "dad4c8")
    if "wall" in steps:
        run_painter(wall_spec(name, cfg))
        log[WALL] = convert(cfg, "%s/%s/wall" % (EXPORTS, name), cdir, WALL, wall_target)
    if "dressed" in steps:
        run_painter(dressed_spec(name, cfg))
        log["dressed"] = convert(cfg, "%s/%s/dressed" % (EXPORTS, name), "%s/%s/_dressed" % (EXPORTS, name),
                                 "dressed", trim_target)
    if "trim" in steps:
        surf = "%s/%s/_dressed/dressed_d.png" % (EXPORTS, name)
        subprocess.run([sys.executable, os.path.join(HERE, "..", "oneoff", "make_tileable_texture_set.py"),
                        "--out", cdir, "--only", TRIM, "--trim-surface", surf, "--trim-target", "#" + trim_target,
                        "--trim-border-rows", str(cfg.get("border_rows", 0))], check=True)
        engine_trim(cdir, TRIM, cfg.get("gloss", {}).get("mean", 100.0))
    if "plinth" in steps:
        p = cfg.get("plinth", "v03")
        if p == "v03":
            log[PLINTH] = conv.convert_one(OLD_EXPORTS + "/stacked_stone_wall", cdir, PLINTH, "877a68", 62.0, 14.0, "up")
        else:
            spec = {"name": name + "_plinth", "out": "%s/%s/plinth" % (EXPORTS, name), "size": 1024,
                    "normal": "Normal_DirectX", "base": p["base"], "layout": None}
            run_painter(spec)
            log[PLINTH] = conv.convert_one("%s/%s/plinth" % (EXPORTS, name), cdir, PLINTH,
                                           p.get("target", "9d9484"), p.get("gloss", 70.0), 14.0, "down")
    if "sheet" in steps:
        log["flat_sheet"] = flat_sheet("%s/%s_d.png" % (cdir, WALL), cdir + "/flat_vs_gondor.png")
    prev = {}
    if os.path.exists(cdir + "/params.json"):
        prev = json.load(open(cdir + "/params.json"))
    prev.update({"name": name, "config": cfg, "wall_spec": wall_spec(name, cfg), "dressed_spec": dressed_spec(name, cfg)})
    prev.setdefault("report", {}).update(log)
    json.dump(prev, open(cdir + "/params.json", "w"), indent=1)
    return log


def main(argv=None):
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("config")
    ap.add_argument("names", nargs="*")
    ap.add_argument("--steps", default="layout,wall,dressed,trim,plinth,sheet")
    a = ap.parse_args(argv)
    cfgs = json.load(open(a.config, encoding="utf-8"))
    for name in a.names or list(cfgs):
        print(name, json.dumps(build(name, cfgs[name], a.steps.split(",")), indent=1)[:1200], flush=True)


if __name__ == "__main__":
    main()
