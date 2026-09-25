#!/usr/bin/env python3
"""Copy a humanoid skeleton's physics onto another skeleton with the same bone names, carried through the rest
pose into the target's own bone frames and proportions, and write it into the target's tpac.

    python tools/tpac_skeleton_copy_physics.py --tpac <hill_troll_a_geo.tpac> --skeleton troll_skeleton_a
        [--fit fit.json] [--donor <human.tpac>] [--donor-skeleton human_skeleton] [--usage human] [--apply]

The physics is the skeleton's SkeletonUserData: Usage, one body per bone (ragdoll capsule, hit capsule, mass,
body type and zone) and the d6 ragdoll and ik joints. A Kit import leaves Usage 'other', empty bodies and no
joints (docs/ai-includes/creature-mount-authoring.md gotcha 8). The dwarf fills them by copying human_skeleton's
verbatim (28 of 28 bodies, 34 of 34 joints), which works because dwarf_skeleton_a keeps the human's bone axes. A
skeleton authored in Blender does not: troll_skeleton_a (2026-09-24) runs every bone along +Y instead of +X,
rolled 180 to 280 deg off the human's and 1.8 to 4.9 times as long, so a verbatim copy would lay each capsule
across its limb and hinge each joint about the wrong axis.

Mapping, per bone (both skeletons in engine space, +Z up, facing +Y; checked on the pelvis, head, feet and toes,
refused otherwise):
  axis   the local axis most parent-to-child offsets lie along, detected per skeleton (+X human, +Y troll);
  d      the world direction from the bone to its axis child (the donor child lying most along the donor's axis,
         found by name in the target), or the bone's own axis for a leaf;
  A      the world rotation taking the donor's (d, forward) frame onto the target's, so bone direction lands on
         bone direction and the front of the limb on its front (up replaces forward where either d is within
         37 deg of forward, the toes);
  M      Bt^T A Bd, donor bone-local to target bone-local (B: the bone's world basis as columns).
A point in donor bone-local space (a capsule end, a joint origin) is scaled, the part along d by the bone's length
ratio (its axis child's offset, target over donor; a leaf takes the height ratio) and the rest by the height ratio
(head bone over the lowest bone), then turned by M. Radii and max radii scale by the height ratio; -1 (no capsule)
stays. Masses, types, zones, blend flags, joint names, limits and lock modes copy unchanged: the dwarf keeps the
human's masses too.

Thickness does not follow height: the hill troll's skin needs hit capsules of 0.68 m on the pelvis and 0.76 on
the spine where the height ratio gives 0.27 and 0.24, so those copied capsules held 5.8% of its skin and a corpse
on ragdoll capsules that thin would sink into the ground. --fit takes a tools/skeleton_hit_capsules.py fit made
for the target skeleton (read-only: export the skin, then `fit ... --axis bone`) and writes, for every body it
sized, the fit's hit capsule (both ends and the radius as radius and max radius, as its own patch does) and a
ragdoll radius of the donor's ragdoll-to-max-hit-radius ratio times the fitted radius (human pelvis 0.08 of
0.17, thigh 0.07 of 0.13), so the corpse keeps the human's proportion to its body. "Sized" covers a body the
fit kept because the package's capsule already covered more of its skin: over a package fitted before, most
bodies are kept, and writing the copy there would undo that fit (2026-09-24, after the grip bones: 26 of 28 kept).
A kept body with no capsule (-1) takes the copy. Ragdoll capsule ends stay copied; a bone the donor gives no
ragdoll capsule keeps none.

A joint's frame is a quaternion in TaleWorlds.Library.Quaternion's field order (W, X, Y, Z) that turns the CHILD
bone's (bone1's) axes into the joint's, the way Quaternion.Mat3FromQuaternion builds a frame. The human knee and
elbow fix the direction: read this way their cone centres lean backward and forward, the side each one flexes to;
the conjugate reading bends both the wrong way. So the target's joint is quat(M) * q (Hamilton product).

Bodies follow the target's own body list (one per bone, in its order). A donor body or joint naming a bone the
target lacks is dropped and listed (human_skeleton's l_finger0 and r_finger0 carry no capsule and no joint). The
header keeps the target's bounding box, padding, strings, GUID and trailing int, as the dwarf's does; only Usage
changes.

Write: the userdata segment is rebuilt, LZ4-compressed, and its entry's actual size, storage size and xxHash64 of
the uncompressed data are updated through tools/tpac_clone_metamesh.py (the byte-exact container round trip,
re-checked on this file before any edit). Refused when a userdata block does not parse and rebuild to its own
bytes, when a segment hash of the target is already stale, or when the result does not read back to what was
meant. Dry run by default; --apply refuses while Bannerlord or the Kit runs, copies <tpac>.bak-physics-<time>
first, and re-reads the file. Afterwards load the module in the Kit once so it re-cooks the package's
RuntimeDataCache entry. Needs numpy, lz4 and xxhash.

--missing-bones OUT.json writes, instead, the donor bones the target lacks under a parent it has, carried through
that parent's map (offset and axes relative to the parent, as on the donor): the grip bones l_finger0 and
r_finger0 that a Monster's main_hand_item_bone / off_hand_item_bone hold items on, which troll_skeleton_a was
authored without. tools/blender/export_rig_for_kit.py --bone-frames adds them to the rig before a Kit import, so
an ordinary weapon, made with its grip at its origin, sits in the troll's hand as in a human's.
--reframe OUT.json writes every bone of the target turned to the donor's anatomical axes (new axes B_target * M,
position kept) plus the carried bones: on that rig every map is the identity, so the donor's clips, which store
joint rotations, bend it as they bend the donor. Human clips on troll_skeleton_a's own rolls twisted its arms,
shoulders and head (guard_up_2h in the Kit, 2026-09-24); Mike chose the re-frame over retargeting every clip.
--bone-frames re-orients the rig's bones from that JSON before export. After the Kit import, re-run the physics
with --reframed <that JSON>: bodies and joints are stored in bone-local coordinates, and on the re-framed rig
every map is the identity by construction. The mode checks each bone of the package against the record first. It
exists because the axis-child rule cannot see the re-frame: the troll's hands were leaves when re-framed and now
aim at grip children placed 0.22 m off their axis, which would turn the wrist joints 24 to 30 deg. --offset NAME=DX,DY,DZ
(engine metres, repeatable) moves a carried bone and keeps its axes, for an artist's placement where the donor's
proportions do not hold: the troll's fist hangs far below its wrist, so the carried grip (the human's 8.5 cm scaled
2.2 times) sat 17 to 19 cm above the centre of the hand's own skin, and KEYForce placed both grips 0.22 m lower
(`--offset r_finger0=0,0,-0.22 --offset l_finger0=0,0,-0.22`, 2026-09-24).

Exit codes: 0 done (or dry run), 1 refused or a check failed, 2 the game or the Kit runs.
"""
import argparse
import datetime
import json
import os
import shutil
import struct
import sys

import lz4.block
import numpy as np
import xxhash

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import skeleton_hit_capsules as shc  # noqa: E402  (skeleton item and segment lookup, bone parser, world matrices)
import tpac_clone_metamesh as tcm  # noqa: E402  (the shared tpac container parser and writer)
from _gamedir import game_dir, game_or_kit_running  # noqa: E402

GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
HUMAN_TPAC = os.path.join(GAME, "Modules", "Native", "EmAssetPackages", "human", "human.tpac")
UP = np.array([0.0, 0.0, 1.0])
FORWARD = np.array([0.0, 1.0, 0.0])
NEAR_FORWARD = 0.8  # |cos| above which forward cannot orient a bone, about 37 deg


class Refused(Exception):
    pass


def parse_userdata(data):
    """SkeletonUserData, every field kept so build_userdata writes the same bytes back."""
    r = shc._Reader(data)
    u = {"bb_padding": r.take("<f"), "bb_min": r.take("<4f"), "bb_max": r.take("<4f"), "usage": r.sstr(),
         "unknown_str": r.sstr(), "guid": r.take("<16s"), "bodies": [], "constraints": []}
    for _ in range(r.take("<i")):
        u["bodies"].append({"bone": r.sstr(), "blend": r.take("<B"), "type": r.sstr(), "zone": r.sstr(),
                            "mass": r.take("<f"), "rp1": r.take("<4f"), "rp2": r.take("<4f"), "rr": r.take("<f"),
                            "cp1": r.take("<4f"), "cp2": r.take("<4f"), "cr": r.take("<f"), "cmax": r.take("<f")})
    u["unknown_int"] = r.take("<i")
    for _ in range(r.take("<i")):
        c = {"skipped": r.take("<I"), "type": r.sstr(), "name": r.sstr(), "bone1": r.sstr(), "bone2": r.sstr(),
             "rot": r.take("<4f"), "pos": r.take("<4f")}
        if c["type"] == "d6":
            c["locks"] = [r.sstr() for _ in range(6)]
            c["limits"] = r.take("<5f")
        elif c["type"] == "ik":
            c["ik_uint"] = r.take("<I")
            c["limits"] = r.take("<4f")
        elif c["type"] == "hinge":
            c["limits"] = r.take("<2f")
        else:
            raise Refused("constraint %r has type %r, which this parser does not know" % (c["name"], c["type"]))
        u["constraints"].append(c)
    if r.pos != len(data):
        raise Refused("SkeletonUserData has %d bytes after the last constraint" % (len(data) - r.pos))
    return u


def _sstr(s):
    b = s.encode("utf-8")
    return struct.pack("<i", len(b)) + b


def build_userdata(u):
    out = [struct.pack("<f4f4f", u["bb_padding"], *u["bb_min"], *u["bb_max"]), _sstr(u["usage"]),
           _sstr(u["unknown_str"]), u["guid"], struct.pack("<i", len(u["bodies"]))]
    for b in u["bodies"]:
        out += [_sstr(b["bone"]), struct.pack("<B", b["blend"]), _sstr(b["type"]), _sstr(b["zone"]),
                struct.pack("<f4f4ff4f4fff", b["mass"], *b["rp1"], *b["rp2"], b["rr"], *b["cp1"], *b["cp2"],
                            b["cr"], b["cmax"])]
    out.append(struct.pack("<ii", u["unknown_int"], len(u["constraints"])))
    for c in u["constraints"]:
        out += [struct.pack("<I", c["skipped"]), _sstr(c["type"]), _sstr(c["name"]), _sstr(c["bone1"]),
                _sstr(c["bone2"]), struct.pack("<4f4f", *c["rot"], *c["pos"])]
        if c["type"] == "d6":
            out += [_sstr(s) for s in c["locks"]] + [struct.pack("<5f", *c["limits"])]
        elif c["type"] == "ik":
            out.append(struct.pack("<I4f", c["ik_uint"], *c["limits"]))
        else:
            out.append(struct.pack("<2f", *c["limits"]))
    return b"".join(out)


def quat_from_matrix(m):
    """(w, x, y, z) of a rotation matrix acting on column vectors."""
    t = np.trace(m)
    if t > 0:
        s = 2.0 * np.sqrt(t + 1.0)
        q = [0.25 * s, (m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s]
    else:
        i = int(np.argmax(np.diag(m)))
        j, k = (i + 1) % 3, (i + 2) % 3
        s = 2.0 * np.sqrt(1.0 + m[i, i] - m[j, j] - m[k, k])
        q = [0.0] * 4
        q[0] = (m[k, j] - m[j, k]) / s
        q[1 + i] = 0.25 * s
        q[1 + j] = (m[j, i] + m[i, j]) / s
        q[1 + k] = (m[k, i] + m[i, k]) / s
    q = np.array(q)
    return q / np.linalg.norm(q)


def quat_matrix(q):
    """Quaternion.Mat3FromQuaternion: its s, f, u rows are this matrix's columns."""
    w, x, y, z = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y)],
                     [2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x)],
                     [2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y)]])


def quat_mul(a, b):
    """Hamilton product, the same as TaleWorlds' Quaternion operator *."""
    aw, ax, ay, az = a
    bw, bx, by, bz = b
    return np.array([aw * bw - ax * bx - ay * by - az * bz, aw * bx + ax * bw + ay * bz - az * by,
                     aw * by - ax * bz + ay * bw + az * bx, aw * bz + ax * by - ay * bx + az * bw])


def _frame(d, ref):
    r = ref - np.dot(ref, d) * d
    r /= np.linalg.norm(r)
    return np.column_stack([d, r, np.cross(d, r)])


def _origin(sk, name):
    return sk["world"][sk["index"][name]][3, :3]


def _height(sk):
    return _origin(sk, "head")[2] - min(w[3, 2] for w in sk["world"])


def check_facing(sk, label):
    """Both skeletons must stand +Z up and face +Y, the frame A is built in."""
    need = ("pelvis", "head", "l_foot", "l_toe0", "r_foot", "r_toe0")
    missing = [n for n in need if n not in sk["index"]]
    if missing:
        raise Refused("%s has no %s; this tool maps humanoid skeletons by the human bone names" % (label, missing))
    if _origin(sk, "head")[2] <= _origin(sk, "pelvis")[2]:
        raise Refused("%s: the head is not above the pelvis (+Z up)" % label)
    for side in ("l", "r"):
        if _origin(sk, side + "_toe0")[1] <= _origin(sk, side + "_foot")[1]:
            raise Refused("%s: %s_toe0 is not ahead of %s_foot along +Y" % (label, side, side))


def bone_maps(donor, target, identity=False):
    """{bone: (M, S)}: M turns donor bone-local into target bone-local, S scales donor bone-local first.
    identity: the target was re-framed to the donor's axes (--reframe), so M is the identity by construction. The
    rule below cannot see that: a hand re-framed as a leaf that later gained a grip child placed off its axis (the
    troll's, lowered 0.22 m) would be aimed at that child, 24 to 30 deg away."""
    dax, dsign, _ = donor["axis"]
    tax, tsign, _ = target["axis"]
    g = _height(target) / _height(donor)
    maps, report = {}, []
    for i, b in enumerate(donor["bones"]):
        name = b["name"]
        if name not in target["index"]:
            continue
        ti = target["index"][name]
        Wd, Wt = donor["world"][i], target["world"][ti]
        Bd, Bt = Wd[:3, :3].T, Wt[:3, :3].T
        kids = [(np.dot(c["rest"][3, :3], np.eye(3)[dax] * dsign) / max(np.linalg.norm(c["rest"][3, :3]), 1e-9), c)
                for c in donor["bones"] if c["parent"] == i and c["name"] in target["index"]]
        kids = [k for k in kids if np.linalg.norm(k[1]["rest"][3, :3]) > 1e-6]
        if kids:
            child = max(kids, key=lambda k: k[0])[1]["name"]
            vd = _origin(donor, child) - Wd[3, :3]
            vt = _origin(target, child) - Wt[3, :3]
            s = np.linalg.norm(vt) / np.linalg.norm(vd)
            dd, dt = vd / np.linalg.norm(vd), vt / np.linalg.norm(vt)
        else:
            child, s = None, g
            dd, dt = Bd[:, dax] * dsign, Bt[:, tax] * tsign
        ref = UP if max(abs(np.dot(dd, FORWARD)), abs(np.dot(dt, FORWARD))) > NEAR_FORWARD else FORWARD
        A = _frame(dt, ref) @ _frame(dd, ref).T
        M = np.eye(3) if identity else Bt.T @ A @ Bd
        if abs(np.linalg.det(M) - 1.0) > 1e-3 or np.abs(M @ M.T - np.eye(3)).max() > 1e-3:
            raise Refused("%s: the bone frames are not rotations (det %.4f); a rest frame carries scale" % (
                name, np.linalg.det(M)))
        u = Bd.T @ dd
        S = g * np.eye(3) + (s - g) * np.outer(u, u)
        maps[name] = (M, S)
        report.append((name, child, s))
    return maps, g, report


def carry_missing_bones(donor, target, offsets=None):
    """Donor bones the target lacks, under a parent it has, carried through that parent's map: the grip bones
    (l_finger0, r_finger0) that hold a Monster's items, for a rig authored without them. Offset and axes are the
    donor's relative to its parent, in the parent's target frame, so an item sits in the hand as on the donor.
    [{name, parent, rest, world}]; rest and world are 16 floats, row convention (a tpac rest frame).
    offsets: {name: (dx, dy, dz)} in engine world metres, added to a carried bone's position (its axes kept), for an
    artist's placement where the donor's proportions do not hold."""
    maps, _, _ = bone_maps(donor, target)
    offsets = offsets or {}
    out = []
    for b in donor["bones"]:
        if b["name"] in target["index"] or b["parent"] < 0:
            continue
        parent = donor["bones"][b["parent"]]["name"]
        if parent not in maps:
            continue
        M, S = maps[parent]
        rest = np.eye(4)
        rest[:3, :3] = (M @ b["rest"][:3, :3].T).T  # rows are the bone's axes in its parent's frame
        rest[3, :3] = M @ (S @ b["rest"][3, :3])
        parent_world = target["world"][target["index"][parent]]
        world = rest @ parent_world
        if b["name"] in offsets:
            world[3, :3] += np.array(offsets[b["name"]], dtype=float)
            rest = world @ np.linalg.inv(parent_world)
        out.append({"name": b["name"], "parent": parent, "rest": [float(x) for x in rest.reshape(-1)],
                    "world": [float(x) for x in world.reshape(-1)]})
    return out


def reframe_bones(donor, target, offsets=None):
    """Every target bone turned to the donor's anatomical axes, its position kept, plus the donor bones the target
    lacks carried through their parent (carry_missing_bones). A bone's new axes are B_target * M (so its map from
    the donor becomes the identity), and a donor clip, which stores joint rotations, then bends the rig as it bends
    the donor: troll_skeleton_a's own rolls twisted human_skeleton's guard_up_2h in the Kit (2026-09-24). The mesh
    and weights need nothing: at rest a bone's orientation does not move its skin.
    [{name, parent, rest, world}] as carry_missing_bones, rest relative to the parent's new frame."""
    maps, _, _ = bone_maps(donor, target)
    world = {}
    for b, W in zip(target["bones"], target["world"]):
        if b["name"] not in maps:
            raise Refused("the donor has no bone %r to take axes from" % b["name"])
        new = np.eye(4)
        new[:3, :3] = maps[b["name"]][0].T @ W[:3, :3]  # rows are axes: (B_target M)^T
        new[3, :3] = W[3, :3]
        world[b["name"]] = new
    out = []

    def add(name, parent, w):
        rest = w @ np.linalg.inv(world[parent]) if parent else w.copy()
        out.append({"name": name, "parent": parent, "rest": [float(x) for x in rest.reshape(-1)],
                    "world": [float(x) for x in w.reshape(-1)]})

    for b in target["bones"]:
        add(b["name"], target["bones"][b["parent"]]["name"] if b["parent"] >= 0 else None, world[b["name"]])
    for c in carry_missing_bones(donor, target, offsets):
        add(c["name"], c["parent"], np.array(c["world"]).reshape(4, 4))
    return out


def _point(ms, p):
    M, S = ms
    q = M @ (S @ np.array(p[:3]))
    return (float(q[0]), float(q[1]), float(q[2]), p[3])


def _radius(r, g):
    return r * g if r > 0 else r


def transfer(donor, target, usage, fit=None, identity=False):
    """The target's new userdata dict and what was dropped. donor/target: see load_skeleton; fit: a
    skeleton_hit_capsules.py fit for the target skeleton, or None; identity: see bone_maps."""
    maps, g, report = bone_maps(donor, target, identity)
    du, tu = donor["userdata"], target["userdata"]
    by_bone = {b["bone"]: b for b in du["bodies"]}
    # refit, or kept because the package's capsule already covered more of its skin (a re-run over a package fitted
    # before); a kept body with no capsule (-1) takes the copy
    fitted = {b["bone"].strip(): b["new"] for b in (fit or {}).get("bodies", []) if b["new"]["cr"] > 0}
    bodies = []
    for tb in tu["bodies"]:
        db = by_bone.get(tb["bone"])
        if db is None:
            raise Refused("the donor has no body for the target's bone %r" % tb["bone"])
        ms = maps[tb["bone"]]
        body = dict(db, bone=tb["bone"], rp1=_point(ms, db["rp1"]), rp2=_point(ms, db["rp2"]),
                    rr=_radius(db["rr"], g), cp1=_point(ms, db["cp1"]), cp2=_point(ms, db["cp2"]),
                    cr=_radius(db["cr"], g), cmax=_radius(db["cmax"], g))
        f = fitted.get(tb["bone"].strip())
        if f:
            body.update(cp1=tuple(f["cp1"]) + (body["cp1"][3],), cp2=tuple(f["cp2"]) + (body["cp2"][3],),
                        cr=f["cr"], cmax=f["cr"])
            if db["rr"] > 0 and db["cmax"] > 0:
                body["rr"] = db["rr"] / db["cmax"] * f["cr"]
        bodies.append(body)
    target_bodies = {b["bone"] for b in tu["bodies"]}
    dropped = [b["bone"] for b in du["bodies"] if b["bone"] not in target_bodies]
    constraints = []
    for c in du["constraints"]:
        if c["bone1"] not in maps or c["bone2"] not in maps:
            dropped.append(c["name"])
            continue
        ms = maps[c["bone1"]]
        m = quat_from_matrix(ms[0])
        if np.abs(quat_matrix(m) - ms[0]).max() > 1e-5:
            raise Refused("%s: the frame change does not convert to a quaternion cleanly" % c["name"])
        q = quat_mul(m, np.array(c["rot"]))
        q /= np.linalg.norm(q)
        if q[0] < 0:
            q = -q
        constraints.append(dict(c, rot=tuple(float(x) for x in q), pos=_point(ms, c["pos"])))
    new = dict(tu, usage=usage, bodies=bodies, constraints=constraints)
    return new, dropped, g, report


def load_skeleton(raw, name):
    item = shc._skeleton_item(tcm.parse(raw), name)
    sname, bones = shc.parse_bones(tcm.segment_payload(item, shc._segment(item, shc.SKELETON_DEFINITION_TAG)))
    data = tcm.segment_payload(item, shc._segment(item, shc.SKELETON_USERDATA_TAG))
    u = parse_userdata(data)
    if build_userdata(u) != data:
        raise Refused("%s: the userdata parser does not rebuild the stored bytes; its model of the format is wrong"
                      % sname)
    sk = {"name": sname, "bones": bones, "world": shc.world_matrices(bones), "userdata": u,
          "index": {b["name"]: i for i, b in enumerate(bones)}}
    sk["axis"] = shc.bone_axis(bones, sk["world"])
    return sk


def replace_userdata(raw, name, data):
    pkg = tcm.parse(raw)
    item = shc._skeleton_item(pkg, name)
    seg = shc._segment(item, shc.SKELETON_USERDATA_TAG)
    blob = lz4.block.compress(data, mode="high_compression", store_size=False) if seg.is_compressed else data
    if seg.is_compressed and len(blob) == len(data):
        raise Refused("the recompressed segment is exactly its raw size; tcm would read it back as raw")
    item.blobs[item.segments.index(seg)] = blob
    seg.actual, seg.storage = len(data), len(blob)
    struct.pack_into("<QQ", item.toc, seg.entry_pos + 8, len(data), len(blob))
    struct.pack_into("<Q", item.toc, seg.entry_pos + 56, xxhash.xxh64_intdigest(data))
    return tcm.serialize(pkg.package_guid, pkg.items, pkg.version)


def verify_output(raw, out, name, data):
    """The new file holds exactly the new userdata and every other byte of payload unchanged."""
    before, after = tcm.parse(raw), tcm.parse(out)
    if tcm.serialize(after.package_guid, after.items, after.version) != out:
        raise Refused("the written package does not round-trip through the container parser")
    if shc.stale_segment_hashes(out):
        raise Refused("stale segment hashes after the edit: %s" % shc.stale_segment_hashes(out))
    seg_after = shc._segment(shc._skeleton_item(after, name), shc.SKELETON_USERDATA_TAG)
    if tcm.segment_payload(shc._skeleton_item(after, name), seg_after) != data:
        raise Refused("the userdata segment does not read back as written")
    if [it.name for it in before.items] != [it.name for it in after.items]:
        raise Refused("the item list changed")
    for a, b in zip(before.items, after.items):
        for sa, sb in zip(a.segments, b.segments):
            if sa.tag == shc.SKELETON_USERDATA_TAG and a.name == shc._skeleton_item(before, name).name:
                continue
            if tcm.segment_payload(a, sa) != tcm.segment_payload(b, sb):
                raise Refused("segment %s of %r changed" % (sa.tag.hex(), a.name))


def _close(a, b):
    """Equal up to float32 rounding (the built dict holds doubles, the bytes hold float32)."""
    if isinstance(a, dict):
        return isinstance(b, dict) and a.keys() == b.keys() and all(_close(a[k], b[k]) for k in a)
    if isinstance(a, (list, tuple)):
        return isinstance(b, (list, tuple)) and len(a) == len(b) and all(_close(x, y) for x, y in zip(a, b))
    if isinstance(a, float) or isinstance(b, float):
        return abs(float(a) - float(b)) <= 1e-6 * max(1.0, abs(float(b)))
    return a == b


def _read(path):
    with open(path, "rb") as fh:
        return fh.read()


def run(args):
    donor_raw, raw = _read(args.donor), _read(args.tpac)
    for label, blob in (("donor", donor_raw), ("target", raw)):
        pkg = tcm.parse(blob)
        if tcm.serialize(pkg.package_guid, pkg.items, pkg.version) != blob:
            raise Refused("the %s package does not round-trip through tpac_clone_metamesh" % label)
    if shc.stale_segment_hashes(raw):
        raise Refused("the target already has stale segment hashes: %s" % shc.stale_segment_hashes(raw))
    donor, target = load_skeleton(donor_raw, args.donor_skeleton), load_skeleton(raw, args.skeleton)
    for label, sk in (("donor", donor), ("target", target)):
        check_facing(sk, "%s %s" % (label, sk["name"]))
        ax, sign, share = sk["axis"]
        if share < 0.6:
            raise Refused("%s %s: only %.0f%% of its offsets share one axis" % (label, sk["name"], 100 * share))
        print("%s %s: %d bones, %d bodies, %d joints, Usage %r, bones along %s%s (%.0f%% of offsets)" % (
            label, sk["name"], len(sk["bones"]), len(sk["userdata"]["bodies"]), len(sk["userdata"]["constraints"]),
            sk["userdata"]["usage"], "+" if sign > 0 else "-", "XYZ"[ax], 100 * share))
    out_json = args.reframe or args.missing_bones
    if out_json:
        offsets = {}
        for spec in args.offset:
            name, _, xyz = spec.partition("=")
            try:
                offsets[name] = tuple(float(v) for v in xyz.split(","))
            except ValueError:
                raise Refused("--offset %r is not NAME=DX,DY,DZ" % spec)
            if len(offsets[name]) != 3:
                raise Refused("--offset %r is not NAME=DX,DY,DZ" % spec)
        carried = carry_missing_bones(donor, target, offsets)
        unknown = set(offsets) - {c["name"] for c in carried}
        if unknown:
            raise Refused("--offset names no carried bone: %s" % sorted(unknown))
        frames = reframe_bones(donor, target, offsets) if args.reframe else carried
        with open(out_json, "w", encoding="utf-8") as fh:
            json.dump({"skeleton": target["name"], "donor": donor["name"],
                       "mode": "reframe" if args.reframe else "missing-bones", "bones": frames,
                       "target_world": {b["name"]: [float(x) for x in w.reshape(-1)]
                                        for b, w in zip(target["bones"], target["world"])}}, fh, indent=1)
        for c in carried:
            print("  %s under %s at %s" % (c["name"], c["parent"], [round(x, 3) for x in c["world"][12:15]]))
        print("wrote %s (%d bones%s); the package is not touched" % (
            out_json, len(frames), ", every bone turned to the donor's axes" if args.reframe else ""))
        return 0
    fit = None
    if args.fit:
        with open(args.fit, encoding="utf-8") as fh:
            fit = json.load(fh)
        if fit.get("skeleton") != target["name"]:
            raise Refused("the fit is for skeleton %r, the target is %r" % (fit.get("skeleton"), target["name"]))
        print("fit %s: %d refitted hit capsules, axis %s" % (os.path.basename(args.fit), sum(
            b["action"] == "refit" for b in fit["bodies"]), fit.get("params", {}).get("axis", "skin")))
    identity = False
    if args.reframed:
        with open(args.reframed, encoding="utf-8") as fh:
            record = json.load(fh)
        if record.get("mode") != "reframe" or record.get("skeleton") != target["name"]:
            raise Refused("%s is not a --reframe record for %s" % (args.reframed, target["name"]))
        recorded = {b["name"]: np.array(b["world"]).reshape(4, 4) for b in record["bones"]}
        if set(recorded) != set(target["index"]):
            raise Refused("the record's bones differ from the package's: %s" % sorted(set(recorded) ^ set(target["index"])))
        worst = (0.0, 0.0)
        for name, W in recorded.items():
            K = target["world"][target["index"][name]]
            turn = np.degrees(np.arccos(np.clip((np.trace(W[:3, :3] @ K[:3, :3].T) - 1) / 2, -1, 1)))
            move = float(np.linalg.norm(W[3, :3] - K[3, :3]))
            if turn > 0.5 or move > 0.001:
                raise Refused("%s in the package is %.2f deg / %.4f m off the re-frame record: not the re-framed rig"
                              % (name, turn, move))
            worst = (max(worst[0], turn), max(worst[1], move))
        identity = True
        print("reframed: the package matches %s (worst %.3f deg, %.5f m); every map is the identity" % (
            os.path.basename(args.reframed), worst[0], worst[1]))
    new, dropped, g, report = transfer(donor, target, args.usage, fit, identity)
    print("height ratio %.3f (radii, cross-sections, leaves)" % g)
    for name, child, s in report:
        print("  %-18s length ratio %.2f %s" % (name, s, "to " + child if child else "(leaf, height ratio)"))
    print("dropped (no such bone in the target): %s" % (", ".join(dropped) or "none"))
    data = build_userdata(new)
    back = parse_userdata(data)
    if build_userdata(back) != data or not _close(back, new):
        raise Refused("the new userdata does not parse back to what was built")
    out = replace_userdata(raw, args.skeleton, data)
    verify_output(raw, out, args.skeleton, data)
    print("new userdata: Usage %r, %d bodies, %d joints (%d d6, %d ik); %d -> %d bytes; file %d -> %d bytes" % (
        new["usage"], len(new["bodies"]), len(new["constraints"]),
        sum(c["type"] == "d6" for c in new["constraints"]), sum(c["type"] == "ik" for c in new["constraints"]),
        len(build_userdata(target["userdata"])), len(data), len(raw), len(out)))
    if not args.apply:
        print("dry run: nothing written (add --apply)")
        return 0
    if game_or_kit_running():
        print("REFUSED: Bannerlord or the Modding Kit is running; close it and re-run")
        return 2
    backup = "%s.bak-physics-%s" % (args.tpac, datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
    shutil.copy2(args.tpac, backup)
    with open(args.tpac, "wb") as fh:
        fh.write(out)
    if _read(args.tpac) != out:
        print("ERROR: the file on disk differs from what was written; restore %s" % backup)
        return 1
    print("wrote %s (backup %s). Load the module in the Kit once so it re-cooks the package's .rdc." % (
        args.tpac, os.path.basename(backup)))
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--tpac", required=True, help="the package holding the target skeleton (written with --apply)")
    ap.add_argument("--skeleton", required=True, help="the target skeleton item's name")
    ap.add_argument("--donor", default=HUMAN_TPAC, help="the package holding the donor skeleton (read only)")
    ap.add_argument("--donor-skeleton", default="human_skeleton")
    ap.add_argument("--usage", default="human", choices=("human", "horse", "other"))
    ap.add_argument("--fit", help="a skeleton_hit_capsules.py fit for the target: its hit capsules, and ragdoll radii")
    ap.add_argument("--missing-bones", help="write the donor bones the target lacks (grip bones), carried through "
                                            "their parent, to this JSON for export_rig_for_kit.py --bone-frames; "
                                            "the package is not touched")
    ap.add_argument("--reframe", help="write EVERY bone turned to the donor's axes (positions kept) plus the "
                                      "missing ones, to this JSON for export_rig_for_kit.py --bone-frames, so the "
                                      "donor's clips bend the rig right; the package is not touched")
    ap.add_argument("--reframed", help="the --reframe JSON this package was exported from: checked bone by bone "
                                       "(0.5 deg, 1 mm), then every map is the identity")
    ap.add_argument("--offset", action="append", default=[],
                    help="with --missing-bones: NAME=DX,DY,DZ in engine metres added to a carried bone's position")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args(argv)
    try:
        return run(args)
    except (Refused, ValueError, KeyError, tcm.CloneError) as exc:
        print("REFUSED: %s" % exc)
        return 1


if __name__ == "__main__":
    sys.exit(main())
