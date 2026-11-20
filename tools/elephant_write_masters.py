#!/usr/bin/env python3
"""Write tools/elephant_gait_retarget.py clips into Kit-compiled SkeletalAnimation masters (#1474).

The Kit's FBX import is the only tool that CREATES a master. This one replaces the keyframes of an existing
master in place: the package, the item, its GUIDs and its metadata stay the Kit's, and only the animation data
segment changes, by the same recipe tools/skeleton_hit_capsules.py uses on a skeleton (new blob, its storage
size, its xxHash64, then tpac_clone_metamesh.serialize). --new writes a NEW master the same way, cloned from a
Kit-compiled one under a new name (clone_master).

THE GAME DOES NOT READ A MASTER. It plays each clip from its RuntimeDataCache entry, which holds the keys the
Kit cooked from the master. So nothing this tool writes shows in the game until the Modding Kit starts once
with the module loaded and cooks the clips again (tools/check_gait_stride.py fails a bound clip whose entry is
older than its master). The 2026-11-18 run of this tool was reported as live and was not.

Animation data segment (decoded 2026-11-18 on elephant_walk_geo.tpac, sizes matched to the byte):
    sized name, int32 bone count,
    per bone: rotation track, position track
    root position track, root scale track
    track = uint32 0, uint32 16, int32 key count, key times (float32 each), then 16 bytes per key:
            rotation (w, x, y, z); position (x, y, z, 0)
A rotation key is the bone's rotation relative to its parent (the root: in model space), the plain
column-vector quaternion of that matrix: checked against the rest frames of five bones, where the conjugate
is 0.65 to 0.98 off. The engine's model space is the Blender rig turned 180 degrees about Z.
The root position track is the pelvis's offset from its rest position. The Kit writes it relative to the
FBX's first frame; this tool writes it relative to REST, so a clip that rides lower than rest stays lower.

    python tools/elephant_write_masters.py --clip <clip.json> --master <x_geo.tpac> [--out <file>] [--apply]
    python tools/elephant_write_masters.py --clip <clip.json> --master <template_geo.tpac> --new --out <folder>
    python tools/elephant_write_masters.py --root-z 10:0.006 --master <adod_elephant_geo.tpac> [--out <file>] [--apply]
        (set_root_z: one height change on every key but the first, the rest pose)
Dry run by default: it builds and verifies the new package in memory. --out writes it to a new file.
--apply replaces --master, after a one-time backup beside it (<name>.bak-pre1474, never overwritten), and
refuses while the game or the Kit runs. The clip must have the master's key count unless --resize is given;
--resize also rewrites the item's Duration and checksum, and then the AnimationClip records that play the
master need new Source2 values and a Kit start for their RuntimeDataCache entries.
"""
import argparse
import json
import os
import shutil
import struct
import subprocess
import sys
import uuid

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tpac_clone_metamesh as tcm  # noqa: E402
from elephant_gait_retarget import mat_to_quat  # noqa: E402

SKELETAL_ANIMATION_TYPE = bytes.fromhex("07b0faba3f7e3f45bac6e7640043112b")
RZ = np.diag([-1.0, -1.0, 1.0, 1.0])   # Blender rig space -> engine model space
BACKUP_SUFFIX = ".bak-pre1474"


def _track(buf, off):
    u1, u2, n = struct.unpack_from("<IIi", buf, off)
    off += 12
    times = list(struct.unpack_from("<%df" % n, buf, off))
    off += 4 * n
    vals = np.frombuffer(buf, dtype="<f4", count=4 * n, offset=off).reshape(n, 4).copy()
    return {"u": (u1, u2), "t": times, "v": vals}, off + 16 * n


def parse_anim(buf):
    n = struct.unpack_from("<i", buf, 0)[0]
    name = buf[4:4 + n].decode("ascii")
    off = 4 + n
    bones = struct.unpack_from("<i", buf, off)[0]
    off += 4
    rot, pos = [], []
    for _ in range(bones):
        r, off = _track(buf, off)
        p, off = _track(buf, off)
        rot.append(r)
        pos.append(p)
    root_pos, off = _track(buf, off)
    root_scale, off = _track(buf, off)
    if off != len(buf):
        raise ValueError("animation data: %d bytes parsed of %d" % (off, len(buf)))
    return {"name": name, "rot": rot, "pos": pos, "root_pos": root_pos, "root_scale": root_scale}


def _pack_track(tr):
    n = len(tr["t"])
    return (struct.pack("<IIi", tr["u"][0], tr["u"][1], n) + struct.pack("<%df" % n, *tr["t"])
            + np.asarray(tr["v"], dtype="<f4").reshape(n, 4).tobytes())


def build_anim(a):
    out = [tcm._sized(a["name"]), struct.pack("<i", len(a["rot"]))]
    for r, p in zip(a["rot"], a["pos"]):
        out += [_pack_track(r), _pack_track(p)]
    out += [_pack_track(a["root_pos"]), _pack_track(a["root_scale"])]
    return b"".join(out)


def clip_keys(clip):
    """(rotation keys [bone][frame] as w,x,y,z, root offsets [frame] as x,y,z) in engine space, frames 1..N."""
    names, parents = clip["bones"], clip["parents"]
    idx = {n: i for i, n in enumerate(names)}
    mats = np.array(clip["mats"]).reshape(len(clip["mats"]), len(names), 4, 4)
    eng = RZ @ mats                     # every frame, every bone
    rest_root = eng[0, 0, :3, 3]
    if parents[0] is not None:
        raise ValueError("bone 0 (%r) is not the root" % names[0])
    n = clip["frames"]
    rot = np.zeros((len(names), n, 4))
    for b, par in enumerate(parents):
        for f in range(n):
            m = eng[f + 1, b, :3, :3]
            if par is not None:
                m = eng[f + 1, idx[par], :3, :3].T @ m
            q = mat_to_quat(m)
            if f and np.dot(q, rot[b, f - 1]) < 0:
                q = -q
            rot[b, f] = q
        if np.dot(rot[b, 0], rot[b, n - 1]) < 0 and n > 1:
            pass   # a sign flip across the seam is the same rotation; keys never blend across it out of order
    root = eng[1:, 0, :3, 3] - rest_root
    return rot, root


def new_payload(old, clip, resize=False, name=None):
    a = parse_anim(old)
    if name:
        a["name"] = name
    rot, root = clip_keys(clip)
    bones, n = rot.shape[0], rot.shape[1]
    if bones != len(a["rot"]):
        raise ValueError("clip has %d bones, master %d" % (bones, len(a["rot"])))
    counts = {len(t["t"]) for t in a["rot"]} | {len(a["root_pos"]["t"])}
    if len(counts) != 1:
        raise ValueError("master tracks have different key counts %s; this tool writes uniform tracks" % sorted(counts))
    have = counts.pop()
    if have != n and not resize:
        raise ValueError("clip has %d frames, master has %d keys per track: rebuild the clip at %d frames or pass "
                         "--resize" % (n, have, have))
    if any(len(p["t"]) for p in a["pos"]) or len(a["root_scale"]["t"]):
        raise ValueError("master carries bone position or root scale keys; this tool would drop them")
    times = a["rot"][0]["t"] if have == n else [float(i + 1) for i in range(n)]
    for b in range(bones):
        a["rot"][b] = {"u": a["rot"][b]["u"], "t": list(times), "v": rot[b]}
    a["root_pos"] = {"u": a["root_pos"]["u"], "t": list(times), "v": np.column_stack([root, np.zeros(n)])}
    return build_anim(a), n, have


def _store(item, seg, data):
    """Put `data` in the item's one data segment: the blob, its two sizes and its hash. Returns the stored blob."""
    import lz4.block
    import xxhash
    blob = lz4.block.compress(data, mode="high_compression", store_size=False) if seg.is_compressed else data
    if seg.is_compressed and len(blob) == len(data):
        raise ValueError("the recompressed segment is exactly its raw size; the parser would read it back as raw")
    item.blobs[0] = blob
    seg.storage, seg.actual = len(blob), len(data)
    struct.pack_into("<QQ", item.toc, seg.entry_pos + 8, len(data), len(blob))
    struct.pack_into("<Q", item.toc, seg.entry_pos + 56, xxhash.xxh64_intdigest(data))
    return blob


def rewrite(raw, clip, resize=False):
    pkg = tcm.parse(raw)
    items = [it for it in pkg.items if it.type_guid == SKELETAL_ANIMATION_TYPE]
    if len(items) != 1 or len(items[0].segments) != 1:
        raise ValueError("expected one SkeletalAnimation item with one data segment")
    item = items[0]
    seg = item.segments[0]
    data, n, have = new_payload(tcm.segment_payload(item, seg), clip, resize)
    blob = _store(item, seg, data)
    if n != have:
        start, end = tcm._metadata_span(item)
        bone_num, duration, tail = struct.unpack_from("<iii", item.toc, end - 12)
        if (bone_num, duration, tail) != (len(clip["bones"]), have, 0):
            raise ValueError("metadata tail is %s, expected (%d, %d, 0)" % ((bone_num, duration, tail), len(clip["bones"]), have))
        struct.pack_into("<i", item.toc, end - 8, n)
        item.toc[end:end + 8] = tcm.expected_checksum(item)
    out = tcm.serialize(pkg.package_guid, pkg.items, pkg.version)
    verify(out, data, raw)
    return out, {"item": item.name, "frames": n, "was": have, "payload": len(data), "stored": len(blob)}


def set_root_z(raw, ref_time, z, from_time=1.0):
    """Move the root's height by one amount on every key from `from_time` on, so that the key at `ref_time` is `z`.

    For a master that rides at one wrong height through all its clips: ADOD's elephant_anims_all holds the rest
    pose on its first key and stands 0.13 m above it from the second key to the last (stand, attacks, hit, rear,
    death), so a beast rose off the ground each time it stopped. Naming the height the reference key must have,
    not the amount to move, makes a second run a no-op: so the reference key must be one that moves. Rotations,
    times and every other item stay as they are (verify)."""
    pkg = tcm.parse(raw)
    items = [it for it in pkg.items if it.type_guid == SKELETAL_ANIMATION_TYPE]
    if len(items) != 1 or len(items[0].segments) != 1:
        raise ValueError("expected one SkeletalAnimation item with one data segment (found %d)" % len(items))
    if ref_time < from_time - 1e-6:
        raise ValueError("the reference key at time %g is before the first key that moves (%g): it would stay "
                         "where it is, and each further run would move the other keys again" % (ref_time, from_time))
    item = items[0]
    seg = item.segments[0]
    old = tcm.segment_payload(item, seg)
    a = parse_anim(old)
    times = np.asarray(a["root_pos"]["t"], dtype=float)
    at = np.flatnonzero(np.abs(times - ref_time) < 1e-6)
    if len(at) != 1:
        raise ValueError("the root track has no key at time %g" % ref_time)
    vals = np.array(a["root_pos"]["v"], dtype="<f4").reshape(len(times), 4)
    dz = np.float32(z) - vals[at[0], 2]
    moved = times >= from_time - 1e-6
    info = {"item": item.name, "dz": float(dz), "keys": int(moved.sum()), "of": len(times)}
    if abs(float(dz)) < 1e-6:
        return raw, dict(info, dz=0.0)
    vals[moved, 2] += dz
    a["root_pos"] = dict(a["root_pos"], v=vals)
    data = build_anim(a)
    if len(data) != len(old):
        raise ValueError("the animation data changed size (%d to %d)" % (len(old), len(data)))
    _store(item, seg, data)
    out = tcm.serialize(pkg.package_guid, pkg.items, pkg.version)
    verify(out, data, raw)
    return out, info


def _guid(kind, name):
    """A GUID that depends only on the master's name: a rebuild keeps it, so the clips that name it stay valid."""
    return uuid.uuid5(uuid.NAMESPACE_URL, "taom:anim-master:%s:%s" % (kind, name)).bytes_le


def _item(old, guid, name, meta_body, seg_guid, version):
    """An item's table entry under a new GUID, name and metadata; its one segment entry takes `seg_guid`."""
    _, end = tcm._metadata_span(old)
    meta = struct.pack("<q", len(meta_body)) + meta_body
    import xxhash
    toc = bytearray(bytes(old.toc[:16]) + guid + bytes(old.toc[32:36]) + tcm._sized(name) + meta
                    + struct.pack("<Q", xxhash.xxh64_intdigest(meta)) + bytes(old.toc[end + 8:]))
    item = tcm._reparse(toc, old.blobs, version)
    seg = item.segments[0]
    item.toc[seg.entry_pos + 24:seg.entry_pos + 40] = seg_guid
    return tcm._reparse(item.toc, old.blobs, version)


def clone_master(raw, clip, name):
    """A NEW master package named `name`, built from a Kit-compiled one (`raw`) and the clip's keys.

    A Kit master holds two items: the import record `<name>.fbx` (the source file's path and the animation's
    GUID) and the SkeletalAnimation (the record's GUID, the skeleton's GUID, bone count, key count). The clone
    keeps that layout with new package, item and segment GUIDs, both names changed, and the two cross
    references repointed. The import record's path names `<name>.fbx` beside the template's source; no such
    file is written, so the Kit has nothing to reimport over these keys."""
    import lz4.block
    import xxhash
    pkg = tcm.parse(raw)
    anims = [it for it in pkg.items if it.type_guid == SKELETAL_ANIMATION_TYPE]
    recs = [it for it in pkg.items if it.type_guid != SKELETAL_ANIMATION_TYPE]
    if len(anims) != 1 or len(recs) != 1 or len(anims[0].segments) != 1 or len(recs[0].segments) != 1:
        raise ValueError("expected one import record and one SkeletalAnimation, one segment each")
    anim, rec = anims[0], recs[0]
    old_name = anim.name
    if rec.name != old_name + ".fbx":
        raise ValueError("import record is %r, expected %r" % (rec.name, old_name + ".fbx"))
    for it in (anim, rec):
        if it.segments[0].guid != it.item_guid:
            raise ValueError("%r: its segment does not carry the item's GUID" % it.name)
    g_anim, g_rec = _guid("animation", name), _guid("record", name)

    s, e = tcm._metadata_span(rec)
    body = bytes(rec.toc[s + 8:e])
    one, n = struct.unpack_from("<ii", body, 0)
    path = body[8:8 + n].decode("ascii")
    tail = body[8 + n:]                                   # 8 bytes, int32 1, type GUID, animation GUID, int32 0
    if one != 1 or not path.endswith("/" + old_name + ".fbx") or len(tail) != 48 or tail[28:44] != anim.item_guid:
        raise ValueError("import record metadata is not the layout this tool knows")
    new_path = path[:-len(old_name + ".fbx")] + name + ".fbx"
    rec2 = _item(rec, g_rec, name + ".fbx", struct.pack("<i", 1) + tcm._sized(new_path) + tail[:28] + g_anim + tail[44:],
                 g_rec, pkg.version)

    s, e = tcm._metadata_span(anim)
    body = bytes(anim.toc[s + 8:e])
    if len(body) != 49 or body[4:20] != rec.item_guid:
        raise ValueError("SkeletalAnimation metadata is not the layout this tool knows")
    bones, have, zero = struct.unpack_from("<iii", body, 37)
    data, n, _ = new_payload(tcm.segment_payload(anim, anim.segments[0]), clip, True, name)
    if (bones, zero) != (len(clip["bones"]), 0):
        raise ValueError("metadata tail is %s" % ((bones, have, zero),))
    anim2 = _item(anim, g_anim, name, body[:4] + g_rec + body[20:37] + struct.pack("<iii", bones, n, 0), g_anim, pkg.version)
    blob = lz4.block.compress(data, mode="high_compression", store_size=False)
    if len(blob) == len(data):
        raise ValueError("the compressed segment is exactly its raw size; the parser would read it back as raw")
    seg = anim2.segments[0]
    anim2.blobs[0] = blob
    seg.storage, seg.actual = len(blob), len(data)
    struct.pack_into("<QQ", anim2.toc, seg.entry_pos + 8, len(data), len(blob))
    struct.pack_into("<Q", anim2.toc, seg.entry_pos + 56, xxhash.xxh64_intdigest(data))
    items = [rec2 if it is rec else anim2 for it in pkg.items]
    out = tcm.serialize(_guid("package", name), items, pkg.version)

    back = tcm.parse(out)                                   # read it back as a stranger would
    b_anim = [it for it in back.items if it.type_guid == SKELETAL_ANIMATION_TYPE][0]
    got = parse_anim(tcm.segment_payload(b_anim, b_anim.segments[0]))
    if (b_anim.name, got["name"], len(got["rot"][0]["t"]), b_anim.item_guid) != (name, name, n, g_anim):
        raise ValueError("the cloned master does not read back")
    for it in back.items:
        # The import record's segment keeps the template's bytes and hash field (the Kit's own value there is
        # not the hash of those bytes), so only the animation segment is held to the formula.
        if it is b_anim and xxhash.xxh64_intdigest(tcm.segment_payload(it, it.segments[0])) != tcm.segment_hash(it, it.segments[0]):
            raise ValueError("item %r: stale segment hash" % it.name)
        if bytes(it.toc[tcm._metadata_span(it)[1]:][:8]) != tcm.expected_checksum(it):
            raise ValueError("item %r: stale checksum" % it.name)
    return out, {"item": name, "frames": n, "was": have, "payload": len(data), "stored": len(blob),
                 "guid": str(uuid.UUID(bytes_le=g_anim))}


def verify(raw, want_payload, original):
    """The written package parses, the animation data reads back, every other item is byte for byte the Kit's,
    and the animation item's checksum is right wherever the original's was (the Kit's own Geometry item in
    these packages carries a checksum the metadata formula does not give, so only what this tool wrote is held
    to the formula)."""
    import xxhash
    pkg, old = tcm.parse(raw), tcm.parse(original)
    if len(pkg.items) != len(old.items):
        raise ValueError("item count changed")
    for it, was in zip(pkg.items, old.items):
        if it.type_guid != SKELETAL_ANIMATION_TYPE:
            if it.blobs != was.blobs or len(it.toc) != len(was.toc):
                raise ValueError("item %r changed" % it.name)
            continue
        start, end = tcm._metadata_span(it)
        _, was_end = tcm._metadata_span(was)
        was_ok = bytes(was.toc[was_end:was_end + 8]) == tcm.expected_checksum(was)
        if was_ok and bytes(it.toc[end:end + 8]) != tcm.expected_checksum(it):
            raise ValueError("item %r: stale checksum" % it.name)
        for seg in it.segments:
            if xxhash.xxh64_intdigest(tcm.segment_payload(it, seg)) != tcm.segment_hash(it, seg):
                raise ValueError("item %r: stale segment hash" % it.name)
        got = tcm.segment_payload(it, it.segments[0])
        if got != want_payload:
            raise ValueError("the written animation data does not read back")
        parse_anim(got)


def game_or_kit_running():
    out = subprocess.run(["tasklist", "/FO", "CSV", "/NH"], capture_output=True, text=True).stdout.lower()
    return [n for n in ("bannerlord.exe", "bannerlord.native.exe", "taleworlds.mountandblade.launcher.exe") if n in out]


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--clip")
    ap.add_argument("--root-z", metavar="KEY:Z",
                    help="instead of --clip: move the root's height on every key but the first (the rest pose), by "
                         "the one amount that puts the key at time KEY at height Z (set_root_z)")
    ap.add_argument("--master", required=True)
    ap.add_argument("--out")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--resize", action="store_true")
    ap.add_argument("--new", action="store_true",
                    help="write a NEW master named after the clip into the folder --out, with --master as the "
                         "template; an existing file is replaced only by a rebuild of the same master")
    a = ap.parse_args(argv)
    if bool(a.clip) == bool(a.root_z):
        ap.error("pass --clip or --root-z, one of them")
    raw = open(a.master, "rb").read()
    if a.root_z:
        if a.new or a.resize:
            ap.error("--root-z takes neither --new nor --resize")
        key, z = a.root_z.split(":")
        out, info = set_root_z(raw, float(key), float(z))
        print("%s: root height %+.4f m on %d of %d keys; %s" % (
            info["item"], info["dz"], info["keys"], info["of"], "unchanged" if out == raw else "changed"))
        clip = None
    else:
        clip = json.load(open(a.clip))
    if a.new:
        if not a.out or a.apply:
            ap.error("--new needs --out <folder> and takes no --apply")
        run = game_or_kit_running()
        if run:
            print("REFUSED: %s is running; close it and run again" % ", ".join(run))
            return 2
        out, info = clone_master(raw, clip, clip["clip"])
        path = os.path.join(a.out, clip["clip"] + "_geo.tpac")
        state = "new"
        if os.path.exists(path):
            was = open(path, "rb").read()
            if was[8:24] != out[8:24]:
                print("REFUSED: %s exists and is another package" % path)
                return 1
            state = "unchanged" if was == out else "rebuilt: its clips' cache entries are now stale"
        open(path, "wb").write(out)
        print("%s: %d keys, data %d bytes, stored %d; animation GUID %s; %s" % (
            info["item"], info["frames"], info["payload"], info["stored"], info["guid"], state))
        return 0
    if clip is not None:
        out, info = rewrite(raw, clip, a.resize)
        print("%s: %d frames (was %d), data %d bytes, stored %d; stride %.3f m; %s" % (
            info["item"], info["frames"], info["was"], info["payload"], info["stored"], clip["stride_m"],
            "unchanged" if out == raw else "changed"))
    if a.out:
        os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
        open(a.out, "wb").write(out)
        print("wrote", a.out)
    if a.apply:
        run = game_or_kit_running()
        if run:
            print("REFUSED: %s is running; close it and run again" % ", ".join(run))
            return 2
        if out == raw:
            print("already applied")
            return 0
        bak = a.master + BACKUP_SUFFIX
        if not os.path.exists(bak):
            shutil.copy2(a.master, bak)
        open(a.master, "wb").write(out)
        print("applied to", a.master, "(backup %s)" % bak)
    return 0


if __name__ == "__main__":
    sys.exit(main())
