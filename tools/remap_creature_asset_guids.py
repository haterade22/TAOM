#!/usr/bin/env python3
"""Repoint a creature's copied materials and animation clips at a re-imported asset tree.

WHY THIS EXISTS
When a creature is absorbed from a donor module, its `_mtl` materials and `_anm` clips are
worth copying verbatim: they carry authored options that are tedious and error-prone to
recreate (two_sided / bumpmap / skinning on materials, quad_movement / cyclic /
make_walk_sound / step points on clips). But the meshes and textures normally get
re-imported through the Modding Kit so they live under the new module's own AssetSources,
and **the Kit mints a fresh asset guid for every item it imports**.

The copied materials and clips still reference the DONOR's guids. Nothing errors at load,
so it is easy to miss; what you get instead is:

    CONTENT WARNING: Unable to find DiffuseMap of material <name>
    CONTENT WARNING: Unable to find NormalMap of material <name>
    Unable to find item to add dependency(depender <clip name>)

and a creature that renders untextured. Measured on the warg absorption, 2026-08-28: all 21
texture guids changed, each of the 12 materials embedded exactly 3 stale ones (Diffuse,
Normal, Specular), giving 36 warnings, plus 28 stale `_geo` references inside the clips.

WHAT IT DOES
Matches items between the donor tree and the new tree BY FILENAME, builds an old -> new guid
map, and substitutes inside the `_mtl` / `_anm` files. Asset guids are fixed 16-byte values,
so every substitution is length-preserving and no offset or size field in the container
moves. Only referenced ids change; every authored option is untouched.

NOT a substitute for re-authoring in the Kit. It is the cheap, exact repair when the donor's
options are the ones you want and only the ids are wrong.

Usage:
  python tools/remap_creature_asset_guids.py --new <new-creature-dir> --old <donor-dir> [--apply]

  --new   the module's Assets/creature/<name>/ tree (expects mesh/, textures/, animations/)
  --old   the donor's flat tree (loose files at the root, clips under animations/)

Defaults to a dry run. Writes a `.bak-guidremap-<date>` sibling before touching anything.
"""
import argparse
import binascii
import collections
import datetime
import glob
import os
import shutil
import sys

# Item 0 of a tpac: type_guid at 0x24, item_guid at 0x34 (confirmed against
# tools/tpac_skeleton_scan.py, which reports item_offset=0x24 for single-item packages).
ITEM0_GUID = slice(0x34, 0x44)


def item0_guid(path):
    with open(path, "rb") as f:
        head = f.read(0x44)
    return head[ITEM0_GUID] if len(head) >= 0x44 else None


def fmt_guid(g):
    return "%s-%s-%s-%s-%s" % (
        binascii.hexlify(g[3::-1]).decode(), binascii.hexlify(g[5:3:-1]).decode(),
        binascii.hexlify(g[7:5:-1]).decode(), binascii.hexlify(g[8:10]).decode(),
        binascii.hexlify(g[10:16]).decode())


def build_map(new_root, old_root):
    """old_guid -> (new_guid, filename), for every item present in both trees under one name."""
    mapping = {}
    pairs = [
        (os.path.join(new_root, "textures", "*_tex.tpac"), old_root),
        (os.path.join(new_root, "animations", "*_geo.tpac"), os.path.join(old_root, "animations")),
        (os.path.join(new_root, "mesh", "*_geo.tpac"), old_root),
    ]
    for new_glob, old_dir in pairs:
        for p in sorted(glob.glob(new_glob)):
            q = os.path.join(old_dir, os.path.basename(p))
            if not os.path.exists(q):
                continue
            old_g, new_g = item0_guid(q), item0_guid(p)
            if old_g and new_g and old_g != new_g:
                mapping[old_g] = (new_g, os.path.basename(p))
    return mapping


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--new", required=True, help="new module's Assets/creature/<name>/")
    ap.add_argument("--old", required=True, help="donor module's creature asset dir")
    ap.add_argument("--apply", action="store_true", help="write changes (default: dry run)")
    args = ap.parse_args()

    for d in (args.new, args.old):
        if not os.path.isdir(d):
            print("ERROR: not a directory: %s" % d, file=sys.stderr)
            return 2

    mapping = build_map(args.new, args.old)
    print("guid remaps available (item present in both trees, guid differs): %d" % len(mapping))
    if not mapping:
        print("Nothing to do: no filename matched with a differing guid.")
        return 0

    targets = (sorted(glob.glob(os.path.join(args.new, "textures", "*_mtl.tpac")))
               + sorted(glob.glob(os.path.join(args.new, "animations", "*_anm.tpac"))))
    print("candidate files (_mtl + _anm): %d" % len(targets))

    suffix = ".bak-guidremap-%s" % datetime.date.today().strftime("%Y%m%d")
    changed = subs_total = 0
    per_asset = collections.Counter()

    for p in targets:
        data = open(p, "rb").read()
        original_len = len(data)
        subs = 0
        for old_g, (new_g, name) in mapping.items():
            hits = data.count(old_g)
            if hits:
                data = data.replace(old_g, new_g)
                subs += hits
                per_asset[name] += hits
        if not subs:
            continue
        if len(data) != original_len:
            print("ERROR: length changed on %s, aborting" % p, file=sys.stderr)
            return 1
        changed += 1
        subs_total += subs
        if args.apply:
            bak = p + suffix
            if not os.path.exists(bak):
                shutil.copy2(p, bak)
            open(p, "wb").write(data)

    verb = "repointed" if args.apply else "WOULD repoint"
    print("%s %d guid references across %d files" % (verb, subs_total, changed))
    for name, count in per_asset.most_common(10):
        print("   %-36s %d" % (name, count))

    if args.apply:
        stale = sum(open(p, "rb").read().count(o) for p in targets for o in mapping)
        print("stale references remaining: %d" % stale)
        return 1 if stale else 0
    print("\n(dry run, nothing written -- pass --apply)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
