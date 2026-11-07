#!/usr/bin/env python3
"""Bundle the wolf skeleton into the dire wolf's grey mesh tpac, with Usage 'horse' (#1081).

    python tools/apply_dire_wolf_skeleton.py --tpac <Armory>/Assets/creature/direwolf/mesh/dire_wolf_grey_geo.tpac
        --source <ADOD_Beasts>/Assets/wolves/adod_wolf_armature_unused_geo.tpac [--apply]

The Kit writes no Skeleton item into the mesh tpacs, and a mount without one spawns riderless with no crash
(creature-mount-authoring.md, gotcha 17). Every shipping creature bundles its skeleton with its mesh, so this adds the
pack's item to the grey package, which also holds the saddle. A grey re-import drops the item, so run this again after
one, then re-fit the hit capsules. A re-import can also keep the old item (2026-11-06): --replace drops a skeleton
item at another size or Usage and bundles the pack's afresh.

The source's bodies and constraints stay: it has 57 and 56, authored by the pack, and only Usage changes, because the
pack leaves it 'other' and skeleton_warg has 'horse'. Item and segment GUIDs stay the pack's too, so a module that loads
the pack's own wolf package beside the Armory would collide. TAOM's module set does not include the pack.

--scale (default RIG_SCALE, 2.08) resizes the skeleton as it joins: each bone's rest translation, the capsule ends
and radii, the joint origins and the bounding box. Rest axes, masses and joint limits stay. --back (default RIG_BACK,
0.52 model units) then moves the root bone toward the tail (model -y); the bodies and joints are bone-local, so they
stay. The meshes and the animation masters take the same scale and move (tools/blender/prep_dire_wolf_meshes.py,
scale_dire_wolf_anims.py), so the wolf's saddle sits where the warg's rider clip seats the rider: the dire wolf uses
the warg's rider rows (monster_usage warg), and the engine seats a rider from that clip, not on rider_sit_bone
(docs/features/dire-wolf.md "Seat"). A package that holds the skeleton at another size or place is refused.

Dry run by default. --apply refuses while the game or the Kit runs and writes <tpac>.bak-skeleton-<time> first. A
second run writes nothing. Refused when either package does not round-trip through tpac_clone_metamesh.py, when a
segment hash is already stale, or when the result changes any other segment. Afterwards load the module in the Kit once
so it re-cooks the package's RuntimeDataCache entry.

The animation masters name the skeleton by its item GUID, which is why that GUID stays. They are Kit imports of the
scaled FBX (tools/blender/scale_dire_wolf_anims.py): the pack's own masters are pack size, so this tool no longer copies
them (2026-11-06).

Exit codes: 0 done (or dry run), 1 refused, 2 the game or the Kit runs, or bad arguments.
"""
import argparse
import datetime
import os
import shutil
import struct
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import skeleton_hit_capsules as shc  # noqa: E402
import tpac_clone_metamesh as tcm  # noqa: E402
import tpac_skeleton_copy_physics as cp  # noqa: E402
from _gamedir import game_or_kit_running  # noqa: E402

SKELETON = "adod_wolf_armature_unused"
USAGE = "horse"
RIG_SCALE = 2.08
RIG_BACK = 0.52


def _checked(raw, label):
    pkg = tcm.parse(raw)
    if tcm.serialize(pkg.package_guid, pkg.items, pkg.version) != raw:
        raise cp.Refused("the %s package does not round-trip through tpac_clone_metamesh" % label)
    if shc.stale_segment_hashes(raw):
        raise cp.Refused("the %s package has stale segment hashes: %s" % (label, shc.stale_segment_hashes(raw)))
    return pkg


def scale_definition(data, factor, back=0.0):
    """The skeleton definition with every bone's rest translation times factor, each root bone's then moved back
    model units toward the tail (-y), and every other byte unchanged."""
    out = bytearray(data)
    r = shc._Reader(data)
    r.sstr()
    for _ in range(r.take("<i")):
        r.sstr()
        parent = r.take("<i")
        at = r.pos + 12 * 4  # row 3 of the row-vector rest frame is the translation
        x, y, z = struct.unpack_from("<3f", data, at)
        y = y * factor - (back if parent < 0 else 0.0)
        struct.pack_into("<3f", out, at, x * factor, y, z * factor)
        r.pos += 16 * 4
    if r.pos != len(data):
        raise cp.Refused("the skeleton definition has %d bytes after the last bone" % (len(data) - r.pos))
    return bytes(out)


def scale_userdata(u, factor):
    """Bodies and joints at factor times the size. Capsule ends, radii, joint origins and the bounding box are lengths;
    -1 (no capsule) stays. Masses stay, and so do joint frames and limits: the wolf's 56 joints are d6 with every
    linear axis locked, so their limits are angles."""
    def point(p):
        return (p[0] * factor, p[1] * factor, p[2] * factor, p[3])

    def radius(r):
        return r if r < 0 else r * factor

    bodies = [dict(b, rp1=point(b["rp1"]), rp2=point(b["rp2"]), rr=radius(b["rr"]), cp1=point(b["cp1"]),
                   cp2=point(b["cp2"]), cr=radius(b["cr"]), cmax=radius(b["cmax"])) for b in u["bodies"]]
    return dict(u, bb_padding=u["bb_padding"] * factor, bb_min=point(u["bb_min"]), bb_max=point(u["bb_max"]),
                bodies=bodies, constraints=[dict(c, pos=point(c["pos"])) for c in u["constraints"]])


def _at_size(target_raw, source_raw, name, factor, back=0.0):
    got, want = cp.load_skeleton(target_raw, name)["bones"], cp.load_skeleton(source_raw, name)["bones"]
    move = np.array([0.0, -back, 0.0])
    return len(got) == len(want) and all(
        np.allclose(g["rest"][3, :3], factor * w["rest"][3, :3] + (move if w["parent"] < 0 else 0.0), rtol=1e-5,
                    atol=1e-6) for g, w in zip(got, want))


def bundle(target_raw, source_raw, name, scale=1.0, replace=False, back=0.0):
    """The target package's bytes with the skeleton in it at scale times the source's size and its root moved back
    toward the tail, or the same bytes when it is already there and finished. replace drops a skeleton item that is not
    finished, instead of refusing."""
    target = _checked(target_raw, "target")
    source = _checked(source_raw, "source")
    if any(it.name == name for it in target.items):
        usage = cp.load_skeleton(target_raw, name)["userdata"]["usage"]
        if usage == USAGE and _at_size(target_raw, source_raw, name, scale, back):
            return target_raw
        if not replace:
            if usage != USAGE:
                raise cp.Refused("the target already holds %s with Usage %r; restore the Kit's package first, or pass "
                                 "--replace" % (name, usage))
            raise cp.Refused("the target already holds %s at another size or place than %g times the source, %g back; "
                             "restore the Kit's package first, or pass --replace" % (name, scale, back))
        target_raw = strip_skeleton(target_raw, name)
        target = _checked(target_raw, "target")

    try:
        item = shc._skeleton_item(source, name)
    except ValueError as exc:
        raise cp.Refused("source: %s" % exc)
    target.items.append(item)
    joined = tcm.serialize(target.package_guid, target.items, target.version)

    if scale != 1.0 or back:
        item = shc._skeleton_item(tcm.parse(joined), name)
        definition = scale_definition(
            tcm.segment_payload(item, shc._segment(item, shc.SKELETON_DEFINITION_TAG)), scale, back)
        resized = cp.replace_segment(joined, name, shc.SKELETON_DEFINITION_TAG, definition)
        cp.verify_segment(joined, resized, name, shc.SKELETON_DEFINITION_TAG, definition)
        joined = resized

    userdata = cp.load_skeleton(joined, name)["userdata"]
    data = cp.build_userdata(scale_userdata(dict(userdata, usage=USAGE), scale))
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


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--tpac", help="the grey mesh package (written with --apply)")
    ap.add_argument("--source", help="the pack's skeleton package (read only)")
    ap.add_argument("--skeleton", default=SKELETON)
    ap.add_argument("--scale", type=float, default=RIG_SCALE, help="size factor on the pack's skeleton")
    ap.add_argument("--back", type=float, default=RIG_BACK, help="model units the root moves toward the tail")
    ap.add_argument("--replace", action="store_true",
                    help="drop a skeleton item at another size or Usage (a mesh re-import can keep the old one)")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args(argv)

    if not (args.tpac and args.source):
        ap.error("pass --tpac and --source")

    try:
        raw = _read(args.tpac)
        out = bundle(raw, _read(args.source), args.skeleton, args.scale, args.replace, args.back)
    except (OSError, struct.error, cp.Refused, ValueError, KeyError, tcm.CloneError) as exc:
        print("REFUSED: %s" % exc)
        return 1
    if out == raw:
        print("%s already holds %s with Usage %r: nothing to do" % (os.path.basename(args.tpac), args.skeleton, USAGE))
        return 0
    finished = cp.load_skeleton(out, args.skeleton)
    print("%s at %g times the pack's size, %g back: %d bones, %d bodies, %d constraints, Usage %r; file %d -> %d bytes" % (
        args.skeleton, args.scale, args.back, len(finished["bones"]), len(finished["userdata"]["bodies"]),
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
