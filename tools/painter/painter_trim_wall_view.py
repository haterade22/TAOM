"""Host script: show the baked trim on the Rivendell wall in headless Blender.

    python tools/painter/painter_trim_wall_view.py [--folder <candidate folder>] [--out <picture folder>]

Builds the candidate folder (the stone set of v06/alt_calm_p3, the plinth of v05_preview/alt_calm, the baked trim of
v06/trim_baked), then runs tools/oneoff/blender_view_textured.py as painter_stone_views.py does (two wall pieces, green
down, masonry UV scale 0.6, the low sun as well as the default) and waits for <out>/_report/DONE.txt.
"""
import argparse
import os
import shutil
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell"
WALL_FBX = WORK + "/build/v02/wall_solid/rivendell_castle_wall_20m_a.fbx"
VIEWER = os.path.join(HERE, "..", "oneoff", "blender_view_textured.py")


def build_folder(folder):
    os.makedirs(folder, exist_ok=True)
    sources = [(WORK + "/textures/v06/alt_calm_p3", "t_rivendell_wall_stone_a"),
               (WORK + "/textures/v05_preview/alt_calm", "t_rivendell_wall_rubble_a"),
               (WORK + "/textures/v06/trim_baked", "t_rivendell_wall_trim_a")]
    copied = []
    for src, prefix in sources:
        for f in sorted(os.listdir(src)):
            if f.startswith(prefix) and f.endswith(".png"):
                shutil.copy2(os.path.join(src, f), os.path.join(folder, f))
                copied.append(f)
    return copied


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--folder", default=WORK + "/textures/v06/with_baked_trim")
    ap.add_argument("--out", default=WORK + "/build/v02/wall_solid/compare/v06_with_baked_trim")
    o = ap.parse_args(argv)
    print("copied", build_folder(o.folder))
    done = o.out + "/_report/DONE.txt"
    if os.path.exists(done):
        os.remove(done)
    args = ["--piece", "a=" + WALL_FBX, "--piece", "b=" + WALL_FBX, "--gap", "0", "--tex", o.folder,
            "--masonry-uv-scale", "0.6", "--normal-green", "down", "--sun", "low", "--out", o.out]
    exe = os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe")
    subprocess.Popen([exe, "-b", "--factory-startup", "-P", os.path.abspath(VIEWER), "--"] + args)
    t0 = time.time()
    while not os.path.exists(done) and time.time() - t0 < 900:
        time.sleep(4)
    print("DONE" if os.path.exists(done) else "TIMEOUT", o.out)


if __name__ == "__main__":
    main()
