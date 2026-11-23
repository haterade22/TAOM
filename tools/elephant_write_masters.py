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

KEY TIMES (2026-11-23). A clip record plays the keys from its Source1 to its Source2, and the editor accepts a
track only when its first key time is at or under the lower source and its last at or over the upper one
(FUN_180b8b810 in the wEditor TaleWorlds.Native.dll; each track that fails prints "... ipo does not fit with ...
anim_clip's sources"). The Kit's own masters key from 0, and a Kit loop's last source key repeats its first
(warg_canter: keys 0 to 17, sources 1 to 17, key 17 the pose of key 1). So this tool writes frame f at key time
f, and a clip that loops (the default; a one-shot says "loop": false) gets one more key, at time N, that repeats
frame 0: sources 0 to N then hold exactly N steps. Until that day it keyed 1 to N under sources 0 to N, as
ADOD's elephant masters do: one warning per bone per clip, and the first key held for one key time each loop.
--rekey brings a master of that form to this one without its clip file. It changes the key times every record of
the master addresses, so it takes the record's sources and migrates one form only: keys 1 to N, played whole by
one record with sources 0 to N. An in-place --clip rewrite of the same key count keeps the master's own key
times, for the same reason: the records that play it name those times.

    python tools/elephant_write_masters.py --clip <clip.json> --master <x_geo.tpac> [--out <file>] [--apply]
    python tools/elephant_write_masters.py --clip <clip.json> --master <template_geo.tpac> --new --out <folder>
    python tools/elephant_write_masters.py --root-z 10:0.006 --master <adod_elephant_geo.tpac> [--out <file>] [--apply]
        (set_root_z: one height change on every key but the first, the rest pose)
    python tools/elephant_write_masters.py --rekey loop|shot --sources 0:<N> --master <x_geo.tpac> [--apply --backup-dir <folder>]
        (rekey: the keys as they are, timed from 0; a loop gets its closing key. --sources is its record's Source1
        and Source2. The backup goes outside the module, and never beside the master)
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


def close_loop(rot, root):
    """The keys with one more at the end that repeats the first: (rot [bone][key][4], root [key][k]). Each bone's
    closing rotation takes the sign nearest its last key's, so the blend into it goes the short way."""
    last = rot[:, :1].copy()
    last[np.einsum("bk,bk->b", last[:, 0], rot[:, -1]) < 0] *= -1.0
    return np.concatenate([rot, last], axis=1), np.vstack([root, root[:1]])


def clip_keys(clip):
    """(rotation keys [bone][frame] as w,x,y,z, root offsets [frame] as x,y,z) in engine space, one per frame."""
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


def new_payload(old, clip, resize=False, name=None, keep_times=False):
    a = parse_anim(old)
    if name:
        a["name"] = name
    rot, root = clip_keys(clip)
    if clip.get("loop", True):
        rot, root = close_loop(rot, root)
    bones, n = rot.shape[0], rot.shape[1]
    if bones != len(a["rot"]):
        raise ValueError("clip has %d bones, master %d" % (bones, len(a["rot"])))
    counts = {len(t["t"]) for t in a["rot"]} | {len(a["root_pos"]["t"])}
    if len(counts) != 1:
        raise ValueError("master tracks have different key counts %s; this tool writes uniform tracks" % sorted(counts))
    have = counts.pop()
    if have != n and not resize:
        raise ValueError("clip makes %d keys (a loop has one more than its frames), master has %d per track: run "
                         "--rekey on the master first, or pass --resize and then write its records again "
                         "(gen_elephant_anim_clips.ps1)" % (n, have))
    if any(len(p["t"]) for p in a["pos"]) or len(a["root_scale"]["t"]):
        raise ValueError("master carries bone position or root scale keys; this tool would drop them")
    # keep_times: an in-place rewrite of the same key count, whose records name the master's own key times. A new
    # master never keeps its template's (the first spider trot took the template's 0 to 29 under sources 0 to 30).
    times = [float(t) for t in a["rot"][0]["t"]] if keep_times and have == n else [float(i) for i in range(n)]
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
    # without --resize the records name the master's own key times; with it the master is written anew, from 0
    data, n, have = new_payload(tcm.segment_payload(item, seg), clip, resize, keep_times=not resize)
    blob = _store(item, seg, data)
    if n != have:
        _set_key_count(item, len(clip["bones"]), have, n)
    out = tcm.serialize(pkg.package_guid, pkg.items, pkg.version)
    verify(out, data, raw)
    return out, {"item": item.name, "frames": n, "was": have, "payload": len(data), "stored": len(blob)}


def _set_key_count(item, bones, have, n):
    """The item's key count in its metadata, and the checksum over it."""
    start, end = tcm._metadata_span(item)
    bone_num, duration, tail = struct.unpack_from("<iii", item.toc, end - 12)
    if (bone_num, duration, tail) != (bones, have, 0):
        raise ValueError("metadata tail is %s, expected (%d, %d, 0)" % ((bone_num, duration, tail), bones, have))
    struct.pack_into("<i", item.toc, end - 8, n)
    item.toc[end:end + 8] = tcm.expected_checksum(item)


def rekey(raw, loop, sources=None):
    """A master's keys as they are, timed from 0, and for a loop closed (KEY TIMES in the header): the migration
    of a master this tool wrote before 2026-11-23, when no clip file is at hand. sources: (Source1, Source2) of the
    record that plays the master.

    Only one form is migrated: keys 1 to N on every track, under a record whose sources are 0 to N. The sources
    are the caller's statement that one record plays the whole master. This function sees no record: a second
    record on the same master with another range would be shifted, and nothing here can know of it. Every master
    this tool and gen_elephant_anim_clips.ps1 write has one record. A master that keys from 0 is left as it is (a
    second run changes nothing), and it is refused when it does not fit what was asked: a loop that does not end
    on its first pose, or sources that reach past its keys. A one-key pose becomes two keys."""
    pkg = tcm.parse(raw)
    items = [it for it in pkg.items if it.type_guid == SKELETAL_ANIMATION_TYPE]
    if len(items) != 1 or len(items[0].segments) != 1:
        raise ValueError("expected one SkeletalAnimation item with one data segment (found %d)" % len(items))
    item = items[0]
    seg = item.segments[0]
    old = tcm.segment_payload(item, seg)
    a = parse_anim(old)
    tracks = a["rot"] + [a["root_pos"]]
    times = [float(t) for t in a["rot"][0]["t"]]
    if any([float(t) for t in tr["t"]] != times for tr in tracks):
        raise ValueError("the tracks have different key times; this tool writes uniform tracks")
    if any(len(p["t"]) for p in a["pos"]) or len(a["root_scale"]["t"]):
        raise ValueError("master carries bone position or root scale keys; this tool would drop them")
    have = len(times)
    if not have or any(abs(b - a_ - 1.0) > 1e-6 for a_, b in zip(times, times[1:])):
        raise ValueError("the keys are not one run of whole key times (%s...): a master that holds several clips "
                         "is not this tool's to rekey" % times[:4])
    rot = np.array([np.asarray(tr["v"], dtype=np.float64).reshape(have, 4) for tr in a["rot"]])
    root = np.asarray(a["root_pos"]["v"], dtype=np.float64).reshape(have, 4)
    # a closing key is a copy of the first, or its negative: compared exactly, because the stored rotations are
    # not unit length to the last digit and a test by their dot product took a closed loop for an open one
    same = (rot[:, -1] == rot[:, 0]).all(axis=1) | (rot[:, -1] == -rot[:, 0]).all(axis=1)
    closed = have > 1 and times[0] == 0.0 and bool(np.array_equal(root[0], root[-1])) and bool(same.all())
    if sources is None:
        raise ValueError("the record's sources are not given (--sources LO:HI): without them a master is neither "
                         "migrated nor reported as done")
    if times[0] == 0.0:                      # keyed from 0 already: nothing to migrate, only to check
        if loop and not closed:
            raise ValueError("the master keys from 0 and does not end on its first pose: it is not a loop this tool "
                             "wrote before 2026-11-23. Pass shot for a one-shot, or write the clip again")
        if not (times[0] <= sources[0] and sources[1] <= times[-1]):
            raise ValueError("the master keys from 0 and its record plays %g to %g, past its keys %g to %g: not a "
                             "master this tool migrated" % (sources[0], sources[1], times[0], times[-1]))
        return raw, {"item": item.name, "keys": have, "was": have, "times_were": (times[0], times[-1])}
    if times[0] != 1.0 or (float(sources[0]), float(sources[1])) != (0.0, float(have)):
        raise ValueError("only a master keyed 1 to N under a record with sources 0 to N is migrated; this one keys "
                         "%g to %g and its record's sources are %g to %g. A master that holds several clips is not "
                         "this tool's to rekey" % (times[0], times[-1], sources[0], sources[1]))
    if loop:
        rot, root = close_loop(rot, root)
    n = rot.shape[1]
    new_times = [float(i) for i in range(n)]
    for b in range(len(a["rot"])):
        a["rot"][b] = {"u": a["rot"][b]["u"], "t": list(new_times), "v": rot[b]}
    a["root_pos"] = {"u": a["root_pos"]["u"], "t": list(new_times), "v": root}
    data = build_anim(a)
    info = {"item": item.name, "keys": n, "was": have, "times_were": (times[0], times[-1])}
    if data == old:
        return raw, info
    _store(item, seg, data)
    if n != have:
        _set_key_count(item, len(a["rot"]), have, n)
    out = tcm.serialize(pkg.package_guid, pkg.items, pkg.version)
    verify(out, data, raw)
    return out, info


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
    ap.add_argument("--rekey", choices=("loop", "shot"),
                    help="instead of --clip: time the master's own keys from 0, and close a loop (rekey)")
    ap.add_argument("--sources", metavar="LO:HI", help="with --rekey: Source1 and Source2 of the record that plays the master")
    ap.add_argument("--backup-dir", help="with --rekey --apply: the folder, outside the module, for the one backup")
    ap.add_argument("--master", required=True)
    ap.add_argument("--out")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--resize", action="store_true")
    ap.add_argument("--new", action="store_true",
                    help="write a NEW master named after the clip into the folder --out, with --master as the "
                         "template; an existing file is replaced only by a rebuild of the same master")
    a = ap.parse_args(argv)
    if sum(map(bool, (a.clip, a.root_z, a.rekey))) != 1:
        ap.error("pass --clip, --root-z or --rekey, one of them")
    raw = open(a.master, "rb").read()
    if a.rekey:
        if a.new or a.resize or a.out:
            ap.error("--rekey takes --apply and --backup-dir only")
        try:
            sources = tuple(float(v) for v in a.sources.split(":")) if a.sources else None
            if sources is not None and len(sources) != 2:
                raise ValueError("--sources is LO:HI")
            out, info = rekey(raw, a.rekey == "loop", sources)
        except ValueError as err:
            print("REFUSED: %s: %s" % (os.path.basename(a.master), err))
            return 1
        print("%s: keys %g to %g (%d) become 0 to %d (%d), as a %s; %s" % (
            info["item"], info["times_were"][0], info["times_were"][1], info["was"], info["keys"] - 1, info["keys"],
            "loop" if a.rekey == "loop" else "one-shot", "unchanged" if out == raw else "changed"))
        if not a.apply or out == raw:
            return 0
        if not a.backup_dir:
            ap.error("--rekey --apply needs --backup-dir, a folder outside the module")
        run = game_or_kit_running()
        if run:
            print("REFUSED: %s is running; close it and run again" % ", ".join(run))
            return 2
        if (os.sep + "modules" + os.sep) in os.path.abspath(a.backup_dir).lower() + os.sep:
            print("REFUSED: --backup-dir %s is inside a module: the game would load the copy as a second package "
                  "of the same GUIDs" % a.backup_dir)
            return 2
        bak = os.path.join(a.backup_dir, os.path.basename(a.master))
        if os.path.normcase(os.path.abspath(bak)) == os.path.normcase(os.path.abspath(a.master)):
            print("REFUSED: --backup-dir %s is the master's own folder: the backup would be the file about to be "
                  "replaced" % a.backup_dir)
            return 2
        os.makedirs(a.backup_dir, exist_ok=True)
        if not os.path.exists(bak):
            shutil.copy2(a.master, bak)
        tmp = a.master + ".rekey-tmp"          # written beside the master and moved over it: never half a package
        with open(tmp, "wb") as fh:
            fh.write(out)
        os.replace(tmp, a.master)
        print("applied to", a.master, "(backup %s)" % bak)
        return 0
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
        print("%s: %d keys (was %d), data %d bytes, stored %d; stride %.3f m; %s" % (
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
