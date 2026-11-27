"""Change one parameter at a time on a shelf material, export base colour and normal, measure.

    python tools/painter/painter_tile_param_tests.py --cat <catalogue folder> [--only name ...]

Every test is one parameter (or the fill's UV scale) set to one value; two values per parameter.
Painter is driven through painter_run.py with painter_tile_export.py. The exports go to
<cat>/<material>/tests/<test name>/, with a lit 2 x 2 preview, and <cat>/<material>/tests/effects.json
holds the numbers: mean base colour, base contrast (std of the channel mean) and normal tilt
(mean of sqrt(nx^2 + ny^2)), each beside the default export. A colour is [r, g, b] sRGB 0 to 1.
"""
import argparse
import importlib.util
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PREFIX = "rivendell_tile_"


def _pair(param, a, b, key="p"):
    return [{"name": f"{param}_{a}", key: {param: a}}, {"name": f"{param}_{b}", key: {param: b}}]


def _colour(param, label_a, rgb_a, label_b, rgb_b):
    return [{"name": f"{param}_{label_a}", "c": {param: rgb_a}},
            {"name": f"{param}_{label_b}", "c": {param: rgb_b}}]


UV = [{"name": "uvscale_0.5", "uv": [0.5, 0.5]}, {"name": "uvscale_2", "uv": [2, 2]}]
COMMON = (_pair("normal_intensity", 0.1, 0.9) + _pair("height_range", 0.3, 0.6)
          + _pair("contrast", -0.6, 0.6))
PALE, DARK = [0.8, 0.78, 0.72], [0.25, 0.24, 0.22]

# "p" are numeric parameters, "c" colour parameters, "uv" the fill's UV scale.
TESTS = {
    "smoothed_limestone_rock_surface": COMMON + _pair("luminosity", 0.3, 0.7)
    + _pair("saturation", 0.1, 0.9) + _pair("hue_shift", 0.25, 0.5)
    + _colour("color", "pale", PALE, "dark", DARK) + _pair("color_weight", 0.2, 0.6)
    + [{"name": "seed_1", "p": {"$randomseed": 1}}, {"name": "seed_7", "p": {"$randomseed": 7}}] + UV,
    "slick_stone_wall": COMMON + _colour("color_1", "pale", PALE, "dark", DARK) + [{"name": "seed_7", "p": {"$randomseed": 7}}] + UV,
    "large_cliff_smooth_chalk": _pair("normal_intensity", 0.1, 0.9) + _pair("cuts", 0, 1)[:1]
    + _pair("cuts_amount", 10, 240) + _pair("dust_spread", 0.0, 1.0) + _pair("height_range", 0.3, 0.6)
    + _colour("base_color", "sand", [0.75, 0.68, 0.55], "grey", [0.5, 0.5, 0.5]) + [{"name": "seed_7", "p": {"$randomseed": 7}}] + UV,
    "cinderblock_wall": _pair("brick_edge_width", 0.05, 0.6)
    + [{"name": "tile_2_4", "p": {"tile": [2, 4]}}, {"name": "tile_8_16", "p": {"tile": [8, 16]}}]
    + _pair("surface_noise_intensity", 0.0, 0.6) + _pair("mortar_color_opacity", 0.0, 1.0)
    + _pair("normal_intensity", 0.1, 0.9) + _colour("brick_color", "pale", PALE, "dark", DARK) + UV[1:],
    "rectangle_stone_tiles_wall": COMMON + _colour("color_1", "pale", PALE, "dark", DARK) + UV,
    "limestone_floor": COMMON + _colour("color_1", "pale", PALE, "dark", DARK) + UV,
    "walkway_stone_tiles_03": COMMON + _colour("color_1", "pale", PALE, "dark", DARK) + [{"name": "seed_7", "p": {"$randomseed": 7}}] + UV,
}


def tilt(normal):
    """Mean sqrt(nx^2 + ny^2) of an OpenGL normal map (uint8 RGB): 0 flat, larger rougher."""
    n = normal.astype(np.float64) / 127.5 - 1
    return float(np.sqrt(n[..., 0] ** 2 + n[..., 1] ** 2).mean())


def base_stats(base):
    """(mean, std) of the per-pixel channel mean of a base colour map (uint8 RGB)."""
    m = base.astype(np.float64).mean(axis=2)
    return float(m.mean()), float(m.std())


def measure(folder):
    base = np.asarray(Image.open(os.path.join(folder, PREFIX + "baseColor.png")).convert("RGB"))
    normal = np.asarray(Image.open(os.path.join(folder, PREFIX + "normal.png")).convert("RGB"))
    mean, std = base_stats(base)
    return {"base_mean": round(mean, 2), "base_std": round(std, 2), "tilt": round(tilt(normal), 4),
            "rgb_mean": [round(float(x), 1) for x in base.reshape(-1, 3).mean(0)]}


def run_export(name, spec, cat):
    args = [sys.executable, os.path.join(HERE, "painter_run.py"), os.path.join(HERE, "painter_tile_export.py"),
            "--arg", "lib=" + os.path.join(HERE, "painter_tile_lib.py").replace("\\", "/"),
            "--arg", "names=" + json.dumps([name]), "--arg", "out=" + cat.replace("\\", "/"),
            "--arg", "sub=tests/" + spec["name"], "--arg", "channels=cn",
            "--arg", "params=" + json.dumps({name: spec.get("p", {})}),
            "--arg", "colors=" + json.dumps({name: spec.get("c", {})})]
    if "uv" in spec:
        args += ["--arg", "uvscale=" + json.dumps({name: spec["uv"]})]
    done = subprocess.run(args, capture_output=True, text=True)
    return done.returncode == 0 and "Success" in done.stdout, done.stdout + done.stderr


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--cat", required=True)
    ap.add_argument("--only", nargs="*")
    a = ap.parse_args(argv)
    spec_pv = importlib.util.spec_from_file_location("pv", os.path.join(HERE, "painter_tile_preview.py"))
    pv = importlib.util.module_from_spec(spec_pv)
    spec_pv.loader.exec_module(pv)
    for name, specs in TESTS.items():
        if a.only and name not in a.only:
            continue
        effects = {"default": measure(os.path.join(a.cat, name)), "tests": {}}
        for spec in specs:
            ok, log = run_export(name, spec, a.cat)
            folder = os.path.join(a.cat, name, "tests", spec["name"])
            if not ok:
                effects["tests"][spec["name"]] = {"error": log[-300:]}
                continue
            pv.preview(folder)
            m = measure(folder)
            base = effects["default"]
            m["d_base_mean"] = round(m["base_mean"] - base["base_mean"], 2)
            m["d_base_std"] = round(m["base_std"] - base["base_std"], 2)
            m["d_tilt"] = round(m["tilt"] - base["tilt"], 4)
            effects["tests"][spec["name"]] = m
        with open(os.path.join(a.cat, name, "tests", "effects.json"), "w") as fh:
            json.dump(effects, fh, indent=1)
        print(name, len(effects["tests"]))


if __name__ == "__main__":
    main()
