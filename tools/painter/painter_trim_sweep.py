"""Host script: bake the trim sheet in the open Painter project with several settings and score each against
the Blender reference. One line of JSON per variant on stdout.

    python tools/painter/painter_trim_sweep.py <variants.json> --out <folder>

variants.json: {"base": {<painter_trim_bake.py args>}, "variants": {"name": {<args merged over base>}}}
Nested dicts (common, ao, curvature, height) are merged one level deep.
"""
import argparse
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def merged(base, extra):
    out = {k: (dict(v) if isinstance(v, dict) else v) for k, v in base.items()}
    for k, v in extra.items():
        if isinstance(v, dict) and isinstance(out.get(k), dict):
            out[k].update(v)
        else:
            out[k] = v
    return out


def run(cmd):
    p = subprocess.run([sys.executable] + cmd, capture_output=True, text=True)
    return p.returncode, p.stdout, p.stderr


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("variants")
    ap.add_argument("--out", required=True)
    ap.add_argument("--only", default=None, help="comma list of variant names")
    opts = ap.parse_args(argv)
    spec = json.load(open(opts.variants, encoding="utf-8"))
    names = opts.only.split(",") if opts.only else list(spec["variants"])
    for name in names:
        args = merged(spec["base"], spec["variants"][name])
        folder = os.path.join(opts.out, name).replace("\\", "/")
        args["out"] = folder
        code, out, err = run([os.path.join(HERE, "painter_run.py"), os.path.join(HERE, "painter_trim_bake.py"),
                              "--args-json", json.dumps(args), "--timeout", "1700"])
        if code != 0:
            print(json.dumps({"variant": name, "error": (out + err)[-600:]}))
            continue
        code, out, err = run([os.path.join(HERE, "trim_bake_check.py"), folder, "--flip-green",
                              "--out", os.path.join(folder, "report.json")])
        if code != 0:
            print(json.dumps({"variant": name, "check_error": (out + err)[-600:]}))
            continue
        r = json.load(open(os.path.join(folder, "report.json"), encoding="utf-8"))
        print(json.dumps({"variant": name, "ao_corr": round(r["ao"]["corr"], 4), "ao_mean": round(r["ao"]["mean_painter"], 4),
                          "ao_fit": [round(x, 3) for x in r["ao"]["fit_slope_offset"]],
                          "normal_angle": round(r["normal"]["mean_angle_deg"], 3),
                          "normal_corr": [round(r["normal"][k], 4) for k in ("corr_r", "corr_g", "corr_b")],
                          "height_corr": round(r["height"]["corr"], 5),
                          "height_fit": [round(x, 3) for x in r["height"]["fit_mm_per_unit_and_offset"]], "curv_corr": round(r["curvature"]["corr"], 4)}))
        sys.stdout.flush()


if __name__ == "__main__":
    main()
