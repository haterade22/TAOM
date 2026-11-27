"""The flat sheet beside the Gondor control, the stats and the wall pictures of stone candidates.

    python tools/painter/painter_stone_views.py <candidate> [<candidate> ...] [--no-blender] [--round v06|v07]

Round v06 (round 2b): a candidate folder is <WORK>/textures/v06/<name>; the plinth and trim sets of
v05_preview/alt_calm are copied into it and the OLD wall (masonry UV scale 0.6) is shown.
Round v07 (round 3): a candidate folder is <WORK>/textures/v07/<name>. The three sets (stone, plinth, trim; the
last two from textures/v06/set_final for now) go into <WORK>/build/v03_tex/v07_<name>, and the NEW wall of
version 3 is shown (its UVs have the right density, so no masonry scale). Pictures go to <tex>/views.
It writes flat_vs_gondor.png and stats.json in the candidate folder, starts one headless Blender per candidate
(two wall pieces, green down, both suns) and waits for <out>/_report/DONE.txt.
"""
import json
import os
import shutil
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import painter_stone_recipes as pr  # noqa: E402
import tile_compose as tc  # noqa: E402

OLD_WALL = pr.WORK + "/build/v02/wall_solid/rivendell_castle_wall_20m_a.fbx"
NEW_WALL = pr.WORK + "/build/v03/wall_solid/rivendell_castle_wall_20m_a.fbx"
OLD_OUT = pr.WORK + "/build/v02/wall_solid/compare"
TEX_ROOT = pr.WORK + "/build/v03_tex"
DONOR = {"v06": pr.WORK + "/textures/v05_preview/alt_calm", "v07": pr.V06 + "/set_final"}
VIEWER = os.path.join(HERE, "..", "oneoff", "blender_view_textured.py")


def view_args(wall, tex, out):
    """The arguments of blender_view_textured.py: two pieces of `wall` ("old" or "new"), the texture folder,
    the output folder. The old wall's UVs need the masonry scale 0.6; the new wall's do not."""
    fbx = OLD_WALL if wall == "old" else NEW_WALL
    args = ["--piece", "a=" + fbx, "--piece", "b=" + fbx, "--gap", "0", "--tex", tex]
    if wall == "old":
        args += ["--masonry-uv-scale", "0.6"]
    return args + ["--normal-green", "down", "--sun", "low", "--out", out]


def prepare(name, rnd="v06"):
    """Copy the donor sets, write the flat sheet and stats. Returns the texture folder, the stats and the
    candidate folder."""
    root = pr.V06 if rnd == "v06" else pr.V07
    folder = "%s/%s" % (root, name)
    tex = folder
    if rnd == "v07":
        tex = "%s/v07_%s" % (TEX_ROOT, name)
        os.makedirs(tex, exist_ok=True)
        for f in os.listdir(folder):
            if f.startswith(pr.SET_NAME) and f.endswith(".png"):
                shutil.copy2("%s/%s" % (folder, f), "%s/%s" % (tex, f))
    for f in os.listdir(DONOR[rnd]):
        if f.startswith((tc.PLINTH, tc.TRIM)) and f.endswith(".png"):
            shutil.copy2("%s/%s" % (DONOR[rnd], f), "%s/%s" % (tex, f))
    tc.flat_sheet("%s/%s_d.png" % (folder, pr.SET_NAME), 461.0, folder + "/flat_vs_gondor.png")
    st = {"ours": pr.stats(folder), "control": tc.control_stats(461.0)}
    json.dump(st, open(folder + "/stats.json", "w"), indent=1)
    return tex, st


def start_blender(name, tex, rnd="v06"):
    out = "%s/v06_%s" % (OLD_OUT, name) if rnd == "v06" else tex + "/views"
    done = out + "/_report/DONE.txt"
    if os.path.exists(done):
        os.remove(done)
    args = view_args("old" if rnd == "v06" else "new", tex, out)
    exe = os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe")
    subprocess.Popen([exe, "-b", "--factory-startup", "-P", os.path.abspath(VIEWER), "--"] + args)
    return out


V04_TEX = pr.WORK + "/build/v04_tex"
V07_TRIM = pr.V07 + "/set_final"


def corner_box(blocks, layout_size, out_size, crop):
    """The top left (x, y) of a `crop` px square in an `out_size` px tile, centred on the block corner that lies
    nearest the middle of the tile (a corner where joints meet) and pushed inside the tile."""
    k = out_size / float(layout_size)
    mid = out_size / 2.0
    best = None
    for b in blocks:
        for cx in (b["x0"], b["x1"]):
            for cy in (b["y0"], b["y1"]):
                x, y = (cx % layout_size) * k, (cy % layout_size) * k
                d = (x - mid) ** 2 + (y - mid) ** 2
                if best is None or d < best[0]:
                    best = (d, x, y)
    _, x, y = best
    lim = out_size - crop
    return int(min(max(round(x - crop / 2.0), 0), lim)), int(min(max(round(y - crop / 2.0), 0), lim))


def assemble_set(dst, stone_folder, plinth_folder, trim_folder):
    """The three sets of a wall in `dst`, each as _d, _n and _s: the stone from `stone_folder` and the plinth
    from `plinth_folder` (both are working folders that name their files t_rivendell_wall_stone_a; the plinth
    is renamed t_rivendell_wall_rubble_a) and the trim from `trim_folder`, which may hold other sets too."""
    os.makedirs(dst, exist_ok=True)
    plan = [(stone_folder, pr.SET_NAME, tc.WALL), (plinth_folder, pr.SET_NAME, tc.PLINTH),
            (trim_folder, tc.TRIM, tc.TRIM)]
    for folder, src_name, dst_name in plan:
        for kind in "dns":
            shutil.copy2("%s/%s_%s.png" % (folder, src_name, kind), "%s/%s_%s.png" % (dst, dst_name, kind))


def main_round4(argv):
    """python painter_stone_views.py round4 <name> --stone <folder> --plinth <folder> [--trim <folder>] [--control]
    Sets of the wall go to build/v04_tex/v08_<name>/tex, the pictures to build/v04_tex/v08_<name>, from the wall of
    version 3 (two pieces, green down, both suns). Waits for _report/DONE.txt."""
    name = argv[0]
    opt = {argv[i]: argv[i + 1] for i in range(1, len(argv) - 1, 2) if argv[i].startswith("--")}
    out = "%s/v08_%s" % (V04_TEX, name)
    tex = out + "/tex"
    assemble_set(tex, opt["--stone"], opt["--plinth"], opt.get("--trim", V07_TRIM))
    done = out + "/_report/DONE.txt"
    if os.path.exists(done):
        os.remove(done)
    args = view_args("new", tex, out)
    exe = os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe")
    subprocess.Popen([exe, "-b", "--factory-startup", "-P", os.path.abspath(VIEWER), "--"] + args)
    t0 = time.time()
    while not os.path.exists(done) and time.time() - t0 < 900:
        time.sleep(4)
    print(name, "DONE" if os.path.exists(done) else "TIMEOUT", out)


def make_sheets(folder, layout, crop=256, zoom=4):
    """For a stone folder: crop_corner_lit.png (a `crop` px square of the lit tile at the block corner nearest the
    middle, enlarged `zoom` times, no smoothing), flat_vs_gondor.png and stats.json (the numbers of the set and of
    the control). `layout` is the layout folder of the set."""
    import numpy as np
    from PIL import Image
    d, n, sp = (np.asarray(Image.open("%s/%s_%s.png" % (folder, pr.SET_NAME, k)).convert("RGB")) for k in "dns")
    meta = json.load(open(layout + "/layout.json"))
    size = int(meta["info"]["size"])
    x, y = corner_box(meta["blocks"], size, d.shape[0], crop)
    lit = tc.lit_view(d, n, sp, k=1)
    Image.fromarray(lit[y:y + crop, x:x + crop]).resize((crop * zoom, crop * zoom), Image.NEAREST).save(
        folder + "/crop_corner_lit.png")
    tc.flat_sheet("%s/%s_d.png" % (folder, pr.SET_NAME), 461.0, folder + "/flat_vs_gondor.png")
    st = {"ours": pr.stats(folder), "judge": pr.judge(folder, layout), "edge": pr.layout_edge(layout),
          "control": tc.control_stats(461.0), "crop_box_xy": [x, y]}
    json.dump(st, open(folder + "/stats.json", "w"), indent=1)
    return st


def main(argv):
    if argv and argv[0] == "sheets":
        make_sheets(argv[1], argv[2])
        return None
    if argv and argv[0] == "round4":
        return main_round4(argv[1:])
    rnd = argv[argv.index("--round") + 1] if "--round" in argv else "v06"
    names = [a for a in argv if not a.startswith("--") and a not in ("v06", "v07")]
    outs = {}
    for n in names:
        tex, st = prepare(n, rnd)
        print(n, json.dumps(st["ours"]))
        if "--no-blender" not in argv:
            outs[n] = start_blender(n, tex, rnd)
    t0 = time.time()
    while outs and time.time() - t0 < 900:
        for n, out in list(outs.items()):
            if os.path.exists(out + "/_report/DONE.txt"):
                print(n, "DONE", out)
                del outs[n]
        time.sleep(4)
    for n, out in outs.items():
        print(n, "TIMEOUT", out)


if __name__ == "__main__":
    main(sys.argv[1:])
