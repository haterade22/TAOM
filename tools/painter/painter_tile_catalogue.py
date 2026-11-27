"""Build catalogue.json from the exports, params.json and the hand-written descriptions.

    python tools/painter/painter_tile_catalogue.py --cat <catalogue folder>

Inputs in the folder: params.json (painter_tile_params.py), one subfolder per material with the
default export, and <material>/tests/effects.json when tests ran. The descriptions come from
painter_tile_descriptions.json beside this script. A measured smoothness class is added from the
mean normal tilt of the default export (default normal_intensity), next to the hand-written one.
"""
import argparse
import importlib.util
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def tunable(params):
    """Parameter names a worker may tune: everything except the output channel toggles."""
    return [k for k in params if not k.startswith("channel_")]


def unknown_names(expect, params):
    """Names listed in an expect block that the material does not have."""
    return sorted({n for names in expect.values() for n in names if n not in params})


def smooth_class(tilt):
    """Class from the mean normal tilt of the default export: below 0.2 smooth, below 0.35 medium."""
    return "smooth" if tilt < 0.2 else "medium" if tilt < 0.35 else "rough"


def load_measure():
    spec = importlib.util.spec_from_file_location("pt", os.path.join(HERE, "painter_tile_param_tests.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod.measure


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--cat", required=True)
    a = ap.parse_args(argv)
    measure = load_measure()
    params = json.load(open(os.path.join(a.cat, "params.json"), encoding="utf-8"))
    desc = json.load(open(os.path.join(HERE, "painter_tile_descriptions.json"), encoding="utf-8"))
    out = {}
    problems = []
    for name, mat in sorted(params.items()):
        folder = os.path.join(a.cat, name)
        d = desc[name]
        m = measure(folder)
        files = sorted(f for f in os.listdir(folder) if f.endswith(".png"))
        entry = {"gui_name": mat["gui_name"], "category": mat["category"], "tags": mat["tags"],
                 "kind": d["kind"], "default_look": d["look"],
                 "physical_size_cm": mat["physicalSize"], "presets": mat["presets"],
                 "outputs": mat["outputs"], "has_seed": mat["has_seed"],
                 "folder": folder.replace("\\", "/"), "files": files,
                 "measured": m, "parameters": {k: mat["params"][k] for k in tunable(mat["params"])}}
        if d["kind"] in ("stone wall", "stone floor", "plain stone"):
            entry["smooth_or_rough"] = d.get("smooth")
            entry["smooth_measured"] = smooth_class(m["tilt"])
            entry["has_joints"] = d.get("joints")
            entry["expected_controls"] = d.get("expect", {})
            problems += [f"{name}: {n}" for n in unknown_names(d.get("expect", {}), mat["params"])]
        eff = os.path.join(folder, "tests", "effects.json")
        if os.path.exists(eff):
            entry["tests"] = json.load(open(eff, encoding="utf-8"))
        out[name] = entry
    with open(os.path.join(a.cat, "catalogue.json"), "w", encoding="utf-8") as fh:
        json.dump({"count": len(out), "materials": out}, fh, indent=1)
    print(json.dumps({"count": len(out), "unknown_expected_names": problems}))


if __name__ == "__main__":
    main()
