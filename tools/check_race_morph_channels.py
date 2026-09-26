#!/usr/bin/env python3
"""Reinstall gate: the LIVE LOTRLOME_Armory race FBX sources still carry the morph channels the engine needs.

    python tools/check_race_morph_channels.py                 # the install's Armory ($BANNERLORD_GAME_DIR honoured)
    python tools/check_race_morph_channels.py --armory <dir>  # another LOTRLOME_Armory folder

Read-only. The channels live in the FBX the Modding Kit imports, in the unversioned Armory, so an Armory reinstall
or an export_rig_for_kit.py re-export from the .blend drops them and nothing else in the repo notices until a
battle crashes or a troll fights with open hands. The spec is SPEC below; each mesh must carry EXACTLY that many
channels (the engine takes them by order). LODs carry none and are not checked.

Exit: 0 every spec mesh matches; 1 a mesh is missing, has the wrong count, or its FBX cannot be read (a truncated
file included); 2 SKIPPED, the install or an FBX is absent, so the gate did not run for it, which is not a pass (the
repo's other install gates exit 2 the same way).
"""
import argparse
import os
import struct
import sys
import zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir  # noqa: E402
from audit_fbx_lods import read_fbx_meshes  # noqa: E402

ARMORY = os.path.join(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"),
                      "Modules", "LOTRLOME_Armory")

# (FBX under the Armory, LOD0 mesh, channels). Why each count:
#  101: a race head's LOD0 head, .eye and .mouth need the engine's facegen channels; without them the static face
#       morph reads a null morph buffer and the race's first spawn crashes (TaleWorlds.Native.dll +0x57070C, the hill
#       troll, 2026-09-25). Repair: tools/blender/add_face_morph_channels.py --head <head> --apply.
#   26: the hand-pose channels on the LOD0 hands or arms; the human skeleton has no finger bones past finger0, so
#       without them the fingers never close around a weapon. Repair: tools/blender/transfer_hand_morphs.py.
# The dwarf is the working reference race; the hill troll is the one these were added to by hand.
TROLL_FBX = "AssetSources/Race Test/Mordor/Trolls/hill_troll_a/hill_troll_a.fbx"
DWARF_FBX = "AssetSources/Race Test/dwarf/sk_dwarf_bm_f1.fbx"
SPEC = (
    (TROLL_FBX, "hill_troll_a_head", 101),
    (TROLL_FBX, "hill_troll_a_head.eye", 101),
    (TROLL_FBX, "hill_troll_a_head.mouth", 101),
    (TROLL_FBX, "hill_troll_a_hands", 26),
    (DWARF_FBX, "sk_dwarf_bm_f1_head", 101),
    (DWARF_FBX, "sk_dwarf_bm_f1_head.eye", 101),
    (DWARF_FBX, "sk_dwarf_bm_f1_head.mouth", 101),
    (DWARF_FBX, "sk_dwarf_bm_f1_arms", 26),
)
WHY = {101: "the race's first spawn crashes (+0x57070C); tools/blender/add_face_morph_channels.py",
       26: "the hands cannot grip; tools/blender/transfer_hand_morphs.py"}


def check(armory, spec=SPEC, reader=read_fbx_meshes):
    """(mismatches, skipped): one line per spec mesh that is missing or has the wrong count, and one per FBX that
    is not on disk. An FBX the reader cannot parse is a mismatch, never a skip."""
    mismatches, skipped, by_fbx = [], [], {}
    for rel, mesh, need in spec:
        by_fbx.setdefault(rel, []).append((mesh, need))
    for rel, wanted in by_fbx.items():
        path = os.path.join(armory, *rel.split("/"))
        name = os.path.basename(rel)
        if not os.path.isfile(path):
            skipped.append("%s not found at %s; its %d meshes were not checked" % (name, path, len(wanted)))
            continue
        try:
            counts = {}
            for m in reader(path):
                counts.setdefault(m.name, len(m.channels))
        except (OSError, ValueError, KeyError, IndexError, struct.error, zlib.error) as exc:
            mismatches.append("%s cannot be read (%s): %s" % (name, type(exc).__name__, exc))
            continue
        for mesh, need in wanted:
            have = counts.get(mesh)
            if have is None:
                mismatches.append("%s: no mesh %s (needs %d morph channels; without them %s)"
                                  % (name, mesh, need, WHY[need]))
            elif have != need:
                mismatches.append("%s: %s has %d morph channels, needs %d (%s)" % (name, mesh, have, need, WHY[need]))
    return mismatches, skipped


def main(argv=None, reader=read_fbx_meshes):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--armory", default=ARMORY, help="the LOTRLOME_Armory module folder (not its ModuleData)")
    args = ap.parse_args(argv)
    if not os.path.isdir(args.armory):
        print("SKIPPED: no LOTRLOME_Armory at %s; the race morph channel gate did not run (set $BANNERLORD_GAME_DIR "
              "or pass --armory)" % args.armory)
        return 2
    mismatches, skipped = check(args.armory, reader=reader)
    for line in skipped:
        print("SKIPPED: %s; the gate did not run for it" % line)
    for line in mismatches:
        print("MISMATCH: %s" % line)
    if mismatches:
        return 1
    if skipped:
        return 2
    print("OK: %d race meshes carry their morph channels (%s)"
          % (len(SPEC), ", ".join(sorted({os.path.basename(r) for r, _, _ in SPEC}))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
