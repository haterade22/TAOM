"""
Dump per-instance placements from a merged level FBX so the assembled scenes
become kitbash prefab XMLs (taom_erebor_kitbash.xml pattern) instead of giant
duplicate-mesh-ID FBX imports the editor chokes on.

For every mesh object: match it back to a modular kit mesh (sm_rivendell_*,
names loaded from the _meshlists dir; trailing instance digits stripped only
when the strip produces a known name), and record matrix_world as
position / rotation_euler (XYZ, radians) / scale.

Works on either vintage: the normalized assembled_reference FBX (metres,
sm_rivendell_lvl_* names) or a raw UE level export (cm — the 100x guard
rescales; native importer handles UE light actors).

    blender-launcher.exe -b -P tools/oneoff/blender_dump_level_placements.py -- ^
        --src "E:/LOTRAOMAssets/_export/rivendell/assembled_reference/rivendell_house.fbx" ^
        --meshlists "<AssetSources>/Scenes/Rivendell/_meshlists" ^
        --out "E:/LOTRAOMAssets/_export/rivendell/placements/rivendell_house.json"

Launcher detaches: completion = the --out file (plus .log next to it).
"""

import argparse
import json
import math
import os
import re
import sys

import bpy

KIT = "rivendell"
LOG = []


def log(msg):
    LOG.append(msg)
    print(msg)


def sanitize(name):
    name = re.sub(r"\.\d{3}$", "", name)
    name = re.sub(r"^(SM_|sm_)", "", name, flags=re.IGNORECASE)
    name = re.sub(r"[^A-Za-z0-9]+", "_", name).strip("_").lower()
    return name


def load_known(meshlist_dir):
    """Index modular mesh names by underscore-free key: UE actor labels drop
    the underscores that asset filenames keep (Candle01Flame vs
    SM_Candle01_Flame)."""
    known = {}
    stripped_known = {}
    for f in os.listdir(meshlist_dir):
        if f.endswith(".txt"):
            for line in open(os.path.join(meshlist_dir, f), encoding="utf-8"):
                n = line.strip()
                if n.startswith(f"sm_{KIT}_"):
                    k = n.replace("_", "")
                    known[k] = n
                    # index also under trailing-digits-stripped form so
                    # 'drape_folded' finds 'sm_rivendell_drape_folded_01'
                    ks = re.sub(r"\d+$", "", k)
                    stripped_known.setdefault(ks, n)
    return known, stripped_known


def _try_key(base, known, stripped_known):
    key = f"sm{KIT}{base}".replace("_", "")
    while True:
        if key in known:
            return known[key]
        if key in stripped_known:
            return stripped_known[key]
        s = re.sub(r"\d+$", "", key)
        if s == key or len(s) <= len(f"sm{KIT}"):
            return None
        key = s


def match_mesh(obj_name, known, stripped_known):
    base = sanitize(obj_name)
    base = re.sub(rf"^{KIT}_lvl_", "", base)
    base = re.sub(r"^lvl_", "", base)
    base = re.sub(r"_?staticmeshcomponent\d*$", "", base)  # UE component suffix
    base = re.sub(r"^sm_", "", base)
    hit = _try_key(base, known, stripped_known)
    if hit:
        return hit
    # Blueprint actors prefix the real mesh (bp_sideboard11_sideboard_door_right)
    # and some labels double a token (vase_vase2): drop leading tokens one at a
    # time — every attempt still requires an exact index hit.
    tokens = base.split("_")
    for skip in range(1, min(4, len(tokens))):
        hit = _try_key("_".join(tokens[skip:]), known, stripped_known)
        if hit:
            return hit
    return None


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--meshlists", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args(argv)

    known, stripped_known = load_known(args.meshlists)
    log(f"[placements] {len(known)} known kit meshes")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=args.src)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=args.src)
    # matrix_world is stale right after import in background mode
    bpy.context.view_layer.update()

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    # raw-UE-export guard: cm -> m
    max_dim = 0.0
    for o in meshes:
        max_dim = max(max_dim, max(o.dimensions))
    scale_fix = 0.01 if max_dim > 500.0 else 1.0
    if scale_fix != 1.0:
        log(f"[placements] raw UE units detected (max dim {max_dim:.0f}) -> scaling 0.01")

    entries, unmatched = [], {}
    for o in meshes:
        n = o.name
        if n.upper().startswith("UCX") or n.lower().startswith("bo_"):
            continue
        target = match_mesh(n, known, stripped_known)
        m = o.matrix_world
        loc = [v * scale_fix for v in m.to_translation()]
        rot = list(m.to_euler("XYZ"))
        scl = list(m.to_scale())
        if target is None:
            unmatched[sanitize(n)] = unmatched.get(sanitize(n), 0) + 1
            continue
        entries.append({"mesh": target, "pos": [round(v, 4) for v in loc],
                        "rot": [round(v, 5) for v in rot],
                        "scale": [round(v, 4) for v in scl]})

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump({"source": args.src, "entries": entries, "unmatched": unmatched}, f, indent=1)
    log(f"[placements] {len(entries)} placed instances, {sum(unmatched.values())} unmatched "
        f"({len(unmatched)} distinct)")
    with open(args.out + ".log", "w", encoding="utf-8") as f:
        f.write("\n".join(LOG))


main()
