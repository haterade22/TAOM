#!/usr/bin/env python3
"""Bundle the wolf skeleton into the dire wolf's grey mesh tpac, with Usage 'horse' (#1081).

    python tools/apply_dire_wolf_skeleton.py --tpac <Armory>/Assets/creature/direwolf/mesh/dire_wolf_grey_geo.tpac
        --source <ADOD_Beasts>/Assets/wolves/adod_wolf_armature_unused_geo.tpac [--apply]

The Kit writes no Skeleton item into the mesh tpacs, and a mount without one spawns riderless with no crash
(creature-mount-authoring.md, gotcha 17). Every shipping creature bundles its skeleton with its mesh, so this adds the
pack's item to the grey package, which also holds the saddle. A grey re-import drops the item, so run this again after
one, then re-fit the hit capsules.

The source's bodies and constraints stay: it has 57 and 56, authored by the pack, and only Usage changes, because the
pack leaves it 'other' and skeleton_warg has 'horse'. Item and segment GUIDs stay the pack's too, so a module that loads
the pack's own wolf package beside the Armory would collide. TAOM's module set does not include the pack.

Dry run by default. --apply refuses while the game or the Kit runs and writes <tpac>.bak-skeleton-<time> first. A
second run writes nothing. Refused when either package does not round-trip through tpac_clone_metamesh.py, when a
segment hash is already stale, or when the result changes any other segment. Afterwards load the module in the Kit once
so it re-cooks the package's RuntimeDataCache entry.

--masters-from <ADOD_Beasts>/Assets/wolves --masters-to <Armory>/Assets/creature/direwolf/animations copies the pack's
animation masters instead. Clips play them by GUID, and they name the skeleton by its item GUID, which is why that GUID
stays. The pack's armature package also holds a Skeleton item, a second copy that a bundled skeleton must not meet, so
the copy drops it. It never overwrites. The Kit writes no RuntimeDataCache entry for a master, so none is owed.

Exit codes: 0 done (or dry run), 1 refused, 2 the game or the Kit runs, or bad arguments.
"""
import argparse
import datetime
import os
import shutil
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import skeleton_hit_capsules as shc  # noqa: E402
import tpac_clone_metamesh as tcm  # noqa: E402
import tpac_skeleton_copy_physics as cp  # noqa: E402
from _gamedir import game_or_kit_running  # noqa: E402

SKELETON = "adod_wolf_armature_unused"
USAGE = "horse"
MASTERS = ("adod_wolf_animations_turn_geo.tpac", "adod_wolf_animations_unused_geo.tpac",
           "adod_wolf_armature_unused_geo.tpac", "adod_wolf_gallop_geo.tpac", "adod_wolf_trot_geo.tpac",
           "adod_wolf_walk_geo.tpac")


def _checked(raw, label):
    pkg = tcm.parse(raw)
    if tcm.serialize(pkg.package_guid, pkg.items, pkg.version) != raw:
        raise cp.Refused("the %s package does not round-trip through tpac_clone_metamesh" % label)
    if shc.stale_segment_hashes(raw):
        raise cp.Refused("the %s package has stale segment hashes: %s" % (label, shc.stale_segment_hashes(raw)))
    return pkg


def bundle(target_raw, source_raw, name):
    """The target package's bytes with the skeleton in it, or the same bytes when it is already there and finished."""
    target = _checked(target_raw, "target")
    if any(it.name == name for it in target.items):
        usage = cp.load_skeleton(target_raw, name)["userdata"]["usage"]
        if usage != USAGE:
            raise cp.Refused("the target already holds %s with Usage %r; restore the Kit's package first" % (name, usage))
        return target_raw

    source = _checked(source_raw, "source")
    try:
        item = shc._skeleton_item(source, name)
    except ValueError as exc:
        raise cp.Refused("source: %s" % exc)
    target.items.append(item)
    joined = tcm.serialize(target.package_guid, target.items, target.version)

    userdata = cp.load_skeleton(joined, name)["userdata"]
    data = cp.build_userdata(dict(userdata, usage=USAGE))
    out = cp.replace_userdata(joined, name, data)
    cp.verify_output(joined, out, name, data)

    before = tcm.parse(target_raw)
    after = {it.name: it for it in tcm.parse(out).items}
    for old in before.items:
        new = after[old.name]
        if [tcm.segment_payload(new, s) for s in new.segments] != [tcm.segment_payload(old, s) for s in old.segments]:
            raise cp.Refused("item %r changed" % old.name)
    return out


def strip_skeleton(raw, name):
    """The package's bytes without its Skeleton item, or the same bytes when it holds none."""
    pkg = _checked(raw, "master")
    kept = [it for it in pkg.items if not (it.name == name and it.type_guid == shc.SKELETON_ITEM_TYPE.bytes_le)]
    if len(kept) == len(pkg.items):
        return raw
    return tcm.serialize(pkg.package_guid, kept, pkg.version)


def _read(path):
    with open(path, "rb") as fh:
        return fh.read()


def copy_masters(args):
    try:
        planned = {name: strip_skeleton(_read(os.path.join(args.masters_from, name)), args.skeleton)
                   for name in args.masters}
    except (OSError, struct.error, cp.Refused, ValueError, KeyError, tcm.CloneError) as exc:
        print("REFUSED: %s" % exc)
        return 1
    todo = {n: b for n, b in planned.items() if not os.path.exists(os.path.join(args.masters_to, n))}
    print("%d masters, %d already present, %d to write" % (len(planned), len(planned) - len(todo), len(todo)))
    if not args.apply:
        print("dry run: nothing written (add --apply)")
        return 0
    if game_or_kit_running():
        print("REFUSED: Bannerlord or the Modding Kit is running; close it and re-run")
        return 2
    os.makedirs(args.masters_to, exist_ok=True)
    for name, blob in todo.items():
        with open(os.path.join(args.masters_to, name), "wb") as fh:
            fh.write(blob)
    print("wrote %d masters to %s." % (len(todo), args.masters_to))
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--tpac", help="the grey mesh package (written with --apply)")
    ap.add_argument("--source", help="the pack's skeleton package (read only)")
    ap.add_argument("--skeleton", default=SKELETON)
    ap.add_argument("--masters-from", help="the pack's folder holding the animation masters (read only)")
    ap.add_argument("--masters-to", help="the Armory folder the masters go to")
    ap.add_argument("--masters", nargs="+", default=MASTERS, help="master package file names")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args(argv)

    if args.masters_from and args.masters_to:
        return copy_masters(args)
    if not (args.tpac and args.source):
        ap.error("pass --tpac and --source, or --masters-from and --masters-to")

    try:
        raw = _read(args.tpac)
        out = bundle(raw, _read(args.source), args.skeleton)
    except (OSError, struct.error, cp.Refused, ValueError, KeyError, tcm.CloneError) as exc:
        print("REFUSED: %s" % exc)
        return 1
    if out == raw:
        print("%s already holds %s with Usage %r: nothing to do" % (os.path.basename(args.tpac), args.skeleton, USAGE))
        return 0
    finished = cp.load_skeleton(out, args.skeleton)
    print("%s: %d bones, %d bodies, %d constraints, Usage %r; file %d -> %d bytes" % (
        args.skeleton, len(finished["bones"]), len(finished["userdata"]["bodies"]),
        len(finished["userdata"]["constraints"]), finished["userdata"]["usage"], len(raw), len(out)))
    if not args.apply:
        print("dry run: nothing written (add --apply)")
        return 0
    if game_or_kit_running():
        print("REFUSED: Bannerlord or the Modding Kit is running; close it and re-run")
        return 2
    backup = "%s.bak-skeleton-%s" % (args.tpac, datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
    shutil.copy2(args.tpac, backup)
    with open(args.tpac, "wb") as fh:
        fh.write(out)
    if _read(args.tpac) != out:
        print("ERROR: the file on disk differs from what was written; restore %s" % backup)
        return 1
    print("wrote %s (backup %s). Load the module in the Kit once so it re-cooks the package's .rdc." % (
        args.tpac, os.path.basename(backup)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
