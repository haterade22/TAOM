#!/usr/bin/env python3
"""List tpac packages in a module that have no RuntimeDataCache/<package GUID>.rdc entry.

Why: the shipping client renders and registers items only from packages that have an .rdc entry,
and only the Modding Kit writes those (docs/reviews/lessons/animation-skeleton.md, 2026-09-17,
#616). A package written or patched outside the Kit, or imported into the Kit without a module
save afterwards, is skipped whole: no log line, nothing on screen. Run this before any in-game
test of new assets; a listed file means "open the Armory in the Kit and save", not "fix bytes".

Usage:
    python tools/check_rdc_entries.py                       # LOTRLOME_Armory, all Assets/**/*.tpac
    python tools/check_rdc_entries.py --under creature/troll
    python tools/check_rdc_entries.py --module "<path to a module>" [--types geo,anm | all]
Exit code 1 when the folder is missing or holds no package of the checked types (a gate that checked nothing
is not a pass), or when any package under the filter lacks an entry (a .rdc.rtemp does not count: it is
an unfinished write).

Skeletal-animation MASTERS are not failures. The Kit never writes an entry for a package whose items
are a SkeletalAnimation (plus its source Geometry) and no Metamesh, and the game plays them anyway:
measured 2026-09-18, warg 56/56, elephant 31/31, chariot 3/3 and spider 24/26 masters have no entry,
while every one of those creatures' clips (_anm) has one. Such packages are counted as
animation-masters-without-entry and listed with --show-masters. Detection reads the item type GUIDs
(SkeletalAnimation 07b0faba-..., Metamesh 978b8fa0-...), only for packages that already lack an entry.
"""
import argparse
import os
import sys
import uuid

DEFAULT_MODULE = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory"


# Item type GUIDs as stored in a tpac item header (bytes_le), read off known packages 2026-09-18:
# act_war_ram_butt (SkeletalAnimation), troll_free_idle_0.fbx (Geometry), LOME_troll's lotr_troll_feet (Metamesh).
SKELETAL_ANIMATION_TYPE = bytes.fromhex("07b0faba3f7e3f45bac6e7640043112b")
METAMESH_TYPE = bytes.fromhex("978b8fa07c19ea4bb95b53846cae834e")


def is_animation_master(path: str) -> bool:
    """A package carrying a SkeletalAnimation item and no Metamesh: the Kit writes no .rdc for these."""
    with open(path, "rb") as fh:
        data = fh.read()
    return SKELETAL_ANIMATION_TYPE in data and METAMESH_TYPE not in data


def package_guid(path: str) -> str:
    with open(path, "rb") as fh:
        head = fh.read(24)
    if head[:4] != b"TPAC" or len(head) < 24:
        return ""
    return str(uuid.UUID(bytes_le=head[8:24])).lower()


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--module", default=DEFAULT_MODULE)
    ap.add_argument("--under", default="", help="subfolder of Assets to restrict to, e.g. creature/troll")
    ap.add_argument("--quiet", action="store_true", help="print only the summary line")
    ap.add_argument("--show-masters", action="store_true",
                    help="also list the skeletal-animation masters without an entry (normal, never a failure)")
    ap.add_argument("--types", default="geo,anm",
                    help="package suffixes to check (before .tpac). Materials (_mtl) and textures render "
                         "without an entry: 925 of them have none in the Armory. Use 'all' to check every tpac")
    args = ap.parse_args(argv)
    suffixes = None if args.types == "all" else tuple("_%s.tpac" % t.strip() for t in args.types.split(","))

    rdc_dir = os.path.join(args.module, "RuntimeDataCache")
    if not os.path.isdir(rdc_dir):
        print("no RuntimeDataCache folder under %s" % args.module)
        return 1
    entries = {f[:-4].lower() for f in os.listdir(rdc_dir) if f.lower().endswith(".rdc")}
    pending = {f[:-10].lower() for f in os.listdir(rdc_dir) if f.lower().endswith(".rdc.rtemp")}

    root = os.path.join(args.module, "Assets", args.under.replace("/", os.sep))
    if not os.path.isdir(root):
        print("no folder %s: nothing checked" % root)
        return 1
    missing, masters, total = [], [], 0
    for dirpath, _, files in os.walk(root):
        for f in files:
            if not f.lower().endswith(".tpac"):
                continue
            if suffixes and not f.lower().endswith(suffixes):
                continue
            p = os.path.join(dirpath, f)
            g = package_guid(p)
            total += 1
            if not g or g not in entries:
                row = (os.path.relpath(p, os.path.join(args.module, "Assets")), g, g in pending)
                (masters if g and is_animation_master(p) else missing).append(row)
    if not args.quiet:
        for rel, g, pend in sorted(missing):
            print("  NO RDC  %-70s %s%s" % (rel, g or "<not a tpac>", "  (rtemp pending)" if pend else ""))
        if args.show_masters:
            for rel, g, _ in sorted(masters):
                print("  ANIM MASTER (no entry needed)  %-50s %s" % (rel, g))
    print("packages=%d without-rdc=%d animation-masters-without-entry=%d rdc-entries=%d rtemp=%d  (%s)"
          % (total, len(missing), len(masters), len(entries), len(pending), root))
    if total == 0:
        print("no %s packages under %s: nothing checked" % (args.types, root))
        return 1
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
