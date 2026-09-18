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
Exit code 1 when any package under the filter lacks an entry (a .rdc.rtemp does not count: it is
an unfinished write).
"""
import argparse
import os
import sys
import uuid

DEFAULT_MODULE = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory"


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
    missing, total = [], 0
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
                missing.append((os.path.relpath(p, os.path.join(args.module, "Assets")), g, g in pending))
    if not args.quiet:
        for rel, g, pend in sorted(missing):
            print("  NO RDC  %-70s %s%s" % (rel, g or "<not a tpac>", "  (rtemp pending)" if pend else ""))
    print("packages=%d without-rdc=%d rdc-entries=%d rtemp=%d  (%s)" % (total, len(missing), len(entries), len(pending), root))
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
