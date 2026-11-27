"""Add sbsar schema facts and seed results to catalogue.json (no Painter needed).

    python tools/painter/painter_tile_merge.py --cat <catalogue folder>

Reads <cat>/catalogue.json, <cat>/sbsar_schemas.json (sbsar_schema.py, one entry per material)
and, when a material has a <material>/seed_probe/ export, compares its base colour and normal
with the default export. Writes catalogue.json back with, per parameter, the "group" and the
"affects" list (the outputs that the parameter changes), and per material "seed_effective":
false when a changed seed gave pixel-identical maps. A seed parameter exists on every graph,
so its mere presence says nothing.
"""
import argparse
import json
import os

import numpy as np
from PIL import Image

PREFIX = "rivendell_tile_"
SEED_LIMIT = 0.05


def mean_abs_diff(a, b):
    return float(np.abs(a.astype(np.float64) - b.astype(np.float64)).mean())


def seed_effective(base_diff, normal_diff):
    """True when the maps changed by more than rounding noise (mean of 0 to 255 values)."""
    return max(base_diff, normal_diff) > SEED_LIMIT


def merge(catalogue, schemas, seed_diffs):
    """Return a copy of catalogue with group, affects and seed_effective added."""
    out = json.loads(json.dumps(catalogue))
    by_id = {}
    for s in schemas:
        by_id[s["identifier"]] = s
        if s.get("source"):
            by_id.setdefault(os.path.splitext(os.path.basename(s["source"]))[0], s)
    for name, mat in out["materials"].items():
        schema = by_id.get(name)
        if schema:
            facts = {p["identifier"]: p for p in schema["params"]}
            for pname, param in mat.get("parameters", {}).items():
                if pname in facts:
                    param["group"] = facts[pname].get("group")
                    param["affects"] = facts[pname].get("affects", [])
        if name in seed_diffs:
            d = seed_diffs[name]
            mat["seed_effective"] = seed_effective(d["base"], d["normal"])
            mat["seed_probe_mean_abs_diff"] = {"base": round(d["base"], 3), "normal": round(d["normal"], 3)}
    return out


def read(path):
    return np.asarray(Image.open(path).convert("RGB"))


def collect_seed_diffs(cat, names):
    diffs = {}
    for n in names:
        probe = os.path.join(cat, n, "seed_probe")
        if not os.path.isdir(probe):
            continue
        diffs[n] = {ch: mean_abs_diff(read(os.path.join(cat, n, PREFIX + ch + ".png")),
                                      read(os.path.join(probe, PREFIX + ch + ".png")))
                    for ch in ("baseColor", "normal")}
        diffs[n] = {"base": diffs[n]["baseColor"], "normal": diffs[n]["normal"]}
    return diffs


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--cat", required=True)
    a = ap.parse_args(argv)
    with open(os.path.join(a.cat, "catalogue.json"), encoding="utf-8") as fh:
        catalogue = json.load(fh)
    with open(os.path.join(a.cat, "sbsar_schemas.json"), encoding="utf-8") as fh:
        schemas = json.load(fh)
    merged = merge(catalogue, schemas, collect_seed_diffs(a.cat, catalogue["materials"]))
    with open(os.path.join(a.cat, "catalogue.json"), "w", encoding="utf-8") as fh:
        json.dump(merged, fh, indent=1)
    print(json.dumps({"seed_checked": sorted(n for n, m in merged["materials"].items() if "seed_effective" in m)}))


if __name__ == "__main__":
    main()
