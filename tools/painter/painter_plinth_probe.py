"""Probe run for the plinth stone: the material medieval_castle_wall alone, with parameters from the command line.

    python tools/painter/painter_plinth_probe.py <name> key=json ... [--colors key=[r,g,b] ...]
Writes the raw Painter export (1024 px, texture set 2048) to <v06>/_export/plinth_<name> and prints the stats.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import painter_stone_recipes as pr  # noqa: E402


def main(argv):
    name = argv[0]
    params, colors = {}, {}
    for kv in argv[1:]:
        k, _, v = kv.partition("=")
        if k.startswith("c:"):
            colors[k[2:]] = json.loads(v)
        else:
            params[k] = json.loads(v)
    rec = {"tag": "pl_" + name, "layout": "", "layers": [],
           "base": {"material": "medieval_castle_wall", "params": params, "colors": colors}}
    path = "%s/cfg/plinth_%s_recipe.json" % (pr.V06, name)
    os.makedirs(pr.V06 + "/cfg", exist_ok=True)
    json.dump(rec, open(path, "w"), indent=1)
    exp = "%s/_export/plinth_%s" % (pr.V06, name)
    res = pr.run_painter(os.path.join(HERE, "painter_stone_build.py"),
                         {"lib": pr.LIB, "recipe": path, "out": exp, "size": 1024, "work": 2048})
    print(json.dumps(res["status"]), exp)


if __name__ == "__main__":
    main(sys.argv[1:])
