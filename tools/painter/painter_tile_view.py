"""Show a candidate on the Rivendell wall in headless Blender and wait for the pictures.

    python tools/painter/painter_tile_view.py <candidate name> [<candidate name> ...]
Runs tools/oneoff/blender_view_textured.py (two wall pieces, green down, masonry UV scale 0.6) and waits
for <out>/_report/DONE.txt, then lists the pictures.
"""
import os
import subprocess
import sys
import time

WORK = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell"
WALL = WORK + "/build/v02/wall_solid/rivendell_castle_wall_20m_a.fbx"
OUT = WORK + "/build/v02/wall_solid/compare"
TEX = WORK + "/textures/v04_candidates"
VIEWER = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff", "blender_view_textured.py")


def view_args(name):
    out = "%s/v04_%s" % (OUT, name)
    return out, ["--piece", "a=" + WALL, "--piece", "b=" + WALL, "--gap", "0", "--tex", "%s/%s" % (TEX, name),
                 "--masonry-uv-scale", "0.6", "--normal-green", "down", "--out", out,
                 "--save-blend", "%s/%s/VIEW_v04_%s.blend" % (TEX, name, name)]


def view(name, timeout=600):
    out, args = view_args(name)
    done = out + "/_report/DONE.txt"
    if os.path.exists(done):
        os.remove(done)
    exe = os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe")
    subprocess.Popen([exe, "-b", "--factory-startup", "-P", os.path.abspath(VIEWER), "--"] + args)
    t0 = time.time()
    while not os.path.exists(done) and time.time() - t0 < timeout:
        time.sleep(4)
    return out, os.path.exists(done)


if __name__ == "__main__":
    for n in sys.argv[1:]:
        out, ok = view(n)
        print(n, "OK" if ok else "TIMEOUT", out)
