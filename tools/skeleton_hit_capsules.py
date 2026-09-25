#!/usr/bin/env python3
"""Fit a creature skeleton's per-bone HIT capsules to its skinned mesh, and patch them into the tpac.

Each bone of a Kit skeleton carries two capsules in the package's SkeletonUserData segment: a HIT capsule
(CollisionPosition1/2, CollisionRadius, CollisionMaxRadius: what weapons and missiles strike) and a RAGDOLL
capsule (the corpse's physics after death). Which is which is inferred from the field names and from each body's
BodyType zone, which matches the engine's BoneBodyPartType; the in-game hit test after a patch confirms it. A skeleton the Kit generated and nobody authored gets default hit
capsules along each bone at about a tenth of the bone's length in radius: the war elephant's neck was 0.03 m
wide against 0.6 m of neck, and only 48% of its skin sat inside any hit capsule (2026-09-18). This tool only
ever writes hit capsules; ragdoll capsules, the Monster body capsule (ModuleData XML) and every other
segment are left alone.

    show   list the skeleton's bodies                      --tpac X
    fit    fit capsules to a skin export (read-only)       --tpac X --skin skin.json --mesh NAME --out fit.json
    patch  write a fit's refitted capsules into the tpac   --tpac X --fit fit.json [--apply]

The skin export comes from Blender, headless (read-only on the FBX):
    blender -b -P tools/blender/export_skin_for_capsules.py -- <skin.json> <creature.fbx>

Fit, per body: the bone's skin is the vertices whose largest weight is that bone. The axis runs along the
skin's principal direction when it is elongated, else along the bone (toward the child lying most along the
skeleton's bone axis, which is detected: +X on human-style rigs, +Y on a Blender rig exported primary Y such as
troll_skeleton_a, where a fixed local x lay across every limb). --axis bone always takes the bone's line, for
a humanoid whose short, fat or cloth-weighted limbs skew the skin's. The axis passes through the
middle of the skin's cross-section; the ends sit at the p2..p98 extent along it, or pulled in by half or all
of the radius, whichever covers most of the bone's skin. The radius grows until it
covers --pct of the skin plus --margin, and stops early where the capsule would stand out more than --limit
from the skin (p90 over surface samples, signed by the nearest vertex normal). A bone keeps its capsule when
it has under --min-verts skin vertices or when its old capsule covered more of its own skin.

Patch: the container is read and written by tools/tpac_clone_metamesh.py (parse, segment_payload,
serialize: a byte-exact round trip, re-checked on the elephant package 2026-09-18). This tool decompresses
the SkeletonUserData segment, rewrites each refitted body's two ends (x, y, z; the vec4 padding float is
kept), radius and max radius in place, recompresses, and updates that segment's entry: storage size and the
xxHash64 (seed 0) of the uncompressed payload (the formula tpac_clone_metamesh.py documents; all 23 elephant
segments match it). serialize() then recomputes every data offset. --apply refuses while the game or the Kit
runs, or when the target segment's stored hash does not match its data (the format model would be wrong),
writes a timestamped .bak-hitcapsules-* copy first, and re-reads the result.

Exit codes: 0 done (or dry run), 1 refused or failed a check, 2 the game or the Kit runs (or the process list
could not be read). An unexpected error prints a traceback and exits 1.

After a patch, load the module in the Kit once so it re-cooks the package's RuntimeDataCache entry, the same
step as any other on-disk tpac edit. Needs numpy, scipy (fit only), lz4 and xxhash.
"""
import argparse
import datetime
import json
import os
import shutil
import struct
import sys
import uuid

import lz4.block
import numpy as np
import xxhash

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tpac_clone_metamesh as tcm  # noqa: E402  (the shared tpac container parser and writer)
from _gamedir import game_or_kit_running  # noqa: E402  (shared fail-closed process check)

SKELETON_ITEM_TYPE = uuid.UUID("c635a3d5-eabb-45dd-883e-aa57e4196113")
METAMESH_ITEM_TYPE = uuid.UUID("a08f8b97-197c-4bea-b95b-53846cae834e")
SKELETON_DEFINITION_TYPE = uuid.UUID("11d07d37-e720-406b-ab67-c846f96a8771")
SKELETON_USERDATA_TYPE = uuid.UUID("9b6ac06d-a546-40af-a555-40d301ab4b2f")
SKELETON_DEFINITION_TAG = SKELETON_DEFINITION_TYPE.bytes_le[:4]  # tcm.Segment.tag: first 4 bytes of the type
SKELETON_USERDATA_TAG = SKELETON_USERDATA_TYPE.bytes_le[:4]
MIN_CAPSULE_LENGTH = 0.01  # the shortest capsule the Kit itself wrote into elephant_skeleton


class _Reader:
    def __init__(self, raw, pos=0):
        self.raw, self.pos = raw, pos

    def take(self, fmt):
        v = struct.unpack_from(fmt, self.raw, self.pos)
        self.pos += struct.calcsize(fmt)
        return v if len(v) > 1 else v[0]

    def guid(self):
        v = uuid.UUID(bytes_le=bytes(self.raw[self.pos:self.pos + 16]))
        self.pos += 16
        return v

    def sstr(self):
        n = self.take("<i")
        s = bytes(self.raw[self.pos:self.pos + n]).decode("utf-8")
        self.pos += n
        return s


def _skeleton_item(pkg, name=None):
    skels = [it for it in pkg.items if it.type_guid == SKELETON_ITEM_TYPE.bytes_le and (name is None or it.name == name)]
    if len(skels) != 1:
        raise ValueError("expected one skeleton item%s, found %d" % (" named " + name if name else "", len(skels)))
    return skels[0]


def _segment(item, tag):
    segs = [s for s in item.segments if s.tag == tag]
    if len(segs) != 1:
        raise ValueError("skeleton %r has %d segments tagged %s" % (item.name, len(segs), tag.hex()))
    return segs[0]


def stale_segment_hashes(raw):
    """[(item name, segment index)] whose stored hash is not the xxHash64 of the segment's payload."""
    pkg = tcm.parse(raw)
    return [(it.name, i) for it in pkg.items for i, seg in enumerate(it.segments)
            if xxhash.xxh64_intdigest(tcm.segment_payload(it, seg)) != tcm.segment_hash(it, seg)]


def userdata_payload(raw, skeleton=None):
    item = _skeleton_item(tcm.parse(raw), skeleton)
    return tcm.segment_payload(item, _segment(item, SKELETON_USERDATA_TAG))


def parse_bones(data):
    r = _Reader(data)
    name = r.sstr()
    bones = []
    for _ in range(r.take("<i")):
        bname = r.sstr()
        parent = r.take("<i")
        bones.append({"name": bname, "parent": parent, "rest": np.array(r.take("<16f"), dtype=float).reshape(4, 4)})
    return name, bones


def world_matrices(bones):
    """TpacTool SkeletonDefinitionData.CreateBoneMatrices: world = rest * parent world (row vectors). The fourth
    column is taken as (0, 0, 0, 1): Kit output stores that, but TaleWorlds' own human.tpac stores 0 under the
    translation (and stray denormals above it), which a plain 4x4 product reads as "drop the parent's position"."""
    world = []
    for b in bones:
        m = b["rest"].copy()
        m[:, 3] = (0.0, 0.0, 0.0, 1.0)
        world.append(m @ world[b["parent"]] if b["parent"] >= 0 else m)
    return world


def parse_bodies(data):
    """SkeletonUserData.ReadData, recording where each hit-capsule field sits in the uncompressed segment."""
    r = _Reader(data)
    r.take("<f")
    r.take("<4f")
    r.take("<4f")
    r.sstr()
    r.sstr()
    r.guid()
    bodies = []
    for _ in range(r.take("<i")):
        b = {"bone": r.sstr(), "blend": bool(r.take("<B")), "type": r.sstr(), "zone": r.sstr(), "mass": r.take("<f")}
        b["rp1"], b["rp2"] = r.take("<4f")[:3], r.take("<4f")[:3]
        b["rr"] = r.take("<f")
        off = {"cp1": r.pos}
        b["cp1"] = r.take("<4f")[:3]
        off["cp2"] = r.pos
        b["cp2"] = r.take("<4f")[:3]
        off["cr"] = r.pos
        b["cr"] = r.take("<f")
        off["cmax"] = r.pos
        b["cmax"] = r.take("<f")
        b["off"] = off
        bodies.append(b)
    return bodies


def read_skeleton(raw, skeleton=None):
    item = _skeleton_item(tcm.parse(raw), skeleton)
    name, bones = parse_bones(tcm.segment_payload(item, _segment(item, SKELETON_DEFINITION_TAG)))
    bodies = parse_bodies(tcm.segment_payload(item, _segment(item, SKELETON_USERDATA_TAG)))
    return {"name": name, "item": item.name, "bones": bones, "world": world_matrices(bones), "bodies": bodies}


def patch_bodies(raw, changes, skeleton=None):
    """changes: {bone name (spaces stripped): (cp1, cp2, radius)} in bone-local space. Returns the new file."""
    pkg = tcm.parse(raw)
    item = _skeleton_item(pkg, skeleton)
    seg = _segment(item, SKELETON_USERDATA_TAG)
    data = bytearray(tcm.segment_payload(item, seg))
    bodies = parse_bodies(bytes(data))
    by_name = {b["bone"].strip(): b for b in bodies}
    if len(by_name) != len(bodies):
        raise ValueError("two bodies share a bone name once spaces are stripped; refusing to guess which to patch")
    for bone, (cp1, cp2, radius) in changes.items():
        if bone.strip() not in by_name:
            raise KeyError("no body for bone %r" % bone)
        off = by_name[bone.strip()]["off"]
        struct.pack_into("<3f", data, off["cp1"], *cp1)
        struct.pack_into("<3f", data, off["cp2"], *cp2)
        struct.pack_into("<f", data, off["cr"], radius)
        struct.pack_into("<f", data, off["cmax"], radius)
    data = bytes(data)
    blob = lz4.block.compress(data, mode="high_compression", store_size=False) if seg.is_compressed else data
    if seg.is_compressed and len(blob) == len(data):
        raise ValueError("the recompressed segment is exactly its raw size; tcm would read it back as raw")
    item.blobs[item.segments.index(seg)] = blob
    seg.storage = len(blob)
    struct.pack_into("<Q", item.toc, seg.entry_pos + 16, len(blob))
    struct.pack_into("<Q", item.toc, seg.entry_pos + 56, xxhash.xxh64_intdigest(data))
    return tcm.serialize(pkg.package_guid, pkg.items, pkg.version)


def bone_axis(bones, world):
    """(axis index, sign, share): the bone-local axis most parent-to-child offsets lie along. +X on the Kit's
    human-style rigs (human_skeleton), +Y on a rig exported from Blender with primary bone axis Y
    (troll_skeleton_a). A skeleton with no offset to vote gets +X, the Kit's default capsule axis."""
    votes = {}
    for b, m in zip(bones, world):
        if b["parent"] < 0:
            continue
        pm = world[b["parent"]]
        off = pm[:3, :3] @ (m[3, :3] - pm[3, :3])  # rows of a world matrix are the bone's axes
        if np.linalg.norm(off) < 1e-6:
            continue
        i = int(np.argmax(np.abs(off)))
        key = (i, 1.0 if off[i] > 0 else -1.0)
        votes[key] = votes.get(key, 0) + 1
    if not votes:
        return 0, 1.0, 0.0
    key = max(votes, key=votes.get)
    return key[0], key[1], votes[key] / sum(votes.values())


def bone_directions(bones, world):
    """{bone name, stripped: unit world direction}: toward the child lying most along the skeleton's bone axis, or
    along that axis for a leaf. The line a limb's capsule follows, whatever roll convention the rig uses."""
    ax, sign, _ = bone_axis(bones, world)
    out = {}
    for i, (b, m) in enumerate(zip(bones, world)):
        axis = m[ax, :3] * sign / np.linalg.norm(m[ax, :3])
        best = None
        for c, cm in zip(bones, world):
            v = cm[3, :3] - m[3, :3]
            if c["parent"] != i or np.linalg.norm(v) < 1e-6:
                continue
            cos = float(v @ axis) / np.linalg.norm(v)
            if best is None or cos > best[0]:
                best = (cos, v / np.linalg.norm(v))
        out[b["name"].strip()] = best[1] if best else axis
    return out


def find_axis_map(blender_heads, engine_heads, tolerance=0.01):
    """The signed axis permutation taking Blender world positions to engine positions, from shared bones."""
    names = sorted(set(blender_heads) & set(engine_heads))
    if len(names) < 3:
        raise ValueError("need at least three shared bones to find the axis map, have %d" % len(names))
    b = np.array([blender_heads[n] for n in names], dtype=float)
    e = np.array([engine_heads[n] for n in names], dtype=float)
    best = None
    import itertools
    for perm in itertools.permutations(range(3)):
        for signs in itertools.product((1, -1), repeat=3):
            err = float(np.abs(b[:, perm] * np.array(signs) - e).max())
            if best is None or err < best[2]:
                best = (perm, signs, err)
    if best[2] > tolerance:
        raise ValueError("no axis map puts the skin's bones on the skeleton (best error %.3f m): wrong FBX?" % best[2])
    return best


def _seg_dist(p, a, c):
    ab = c - a
    t = np.clip(((p - a) @ ab) / max(float(ab @ ab), 1e-12), 0.0, 1.0)
    return np.linalg.norm(p - (a + np.outer(t, ab)), axis=1)


def fit_capsules(skel, verts, normals, owners, limit=0.20, pct=95.0, margin=0.03, min_verts=15, seed=7,
                 axis="skin"):
    """Engine-space skin (verts, normals, owning bone per vertex, names stripped) -> per-body fit + coverage.
    axis "skin": the skin's principal direction where it is elongated, else the bone's; "bone": always the bone's."""
    from scipy.spatial import cKDTree
    tree = cKDTree(verts)
    rng = np.random.default_rng(seed)
    dirs = rng.normal(size=(700, 3))
    dirs /= np.linalg.norm(dirs, axis=1)[:, None]
    along = rng.uniform(0, 1, size=(700, 1))

    def protrusion_p90(a, c, r):
        pts = a + (c - a) * along + dirs * r
        dist, idx = tree.query(pts)
        outside = np.einsum("ij,ij->i", pts - verts[idx], normals[idx]) > 0
        return float(np.percentile(np.where(outside, dist, 0.0), 90))

    world = {b["name"].strip(): np.asarray(m) for b, m in zip(skel["bones"], skel["world"])}
    bone_dir = bone_directions(skel["bones"], [np.asarray(m) for m in skel["world"]])
    bodies, old_caps, new_caps = [], [], []
    for body in skel["bodies"]:
        name = body["bone"].strip()
        m = world[name]
        a0 = (np.append(body["cp1"], 1.0) @ m)[:3]
        c0 = (np.append(body["cp2"], 1.0) @ m)[:3]
        r0 = float(body["cr"])
        old_caps.append((a0, c0, r0))
        own = verts[owners == name]
        row = {"bone": body["bone"], "zone": body.get("zone", ""), "verts": int(len(own)), "action": "kept",
               "old": {"cp1": list(map(float, body["cp1"])), "cp2": list(map(float, body["cp2"])), "cr": r0}}
        row["new"] = dict(row["old"])
        if len(own) < min_verts:
            bodies.append(row)
            new_caps.append((a0, c0, r0))
            continue
        centre = own.mean(axis=0)
        ev, evec = np.linalg.eigh(np.cov((own - centre).T))
        u = evec[:, 2] if axis == "skin" and ev[2] > 1.5 * ev[1] else bone_dir[name]
        e1 = np.cross(u, [0.0, 0.0, 1.0]) if abs(u[2]) < 0.9 else np.cross(u, [1.0, 0.0, 0.0])
        e1 /= np.linalg.norm(e1)
        e2 = np.cross(u, e1)
        q1, q2, s = (own - centre) @ e1, (own - centre) @ e2, (own - centre) @ u
        origin = centre + e1 * (np.percentile(q1, 5) + np.percentile(q1, 95)) / 2 \
            + e2 * (np.percentile(q2, 5) + np.percentile(q2, 95)) / 2
        s_lo, s_hi = np.percentile(s, 2), np.percentile(s, 98)
        best, cov_new = None, -1.0
        # How far the ends sit inside the skin's extent, as a share of the radius. A bone's skin is cut flat
        # where the next bone takes over: ends pulled in by the full radius leave that rim uncovered and the
        # radius then inflates to reach it; ends at the extent cover it, standing out only at free ends
        # (feet, tail tip), which the stand-out limit already prices. Each bone keeps its best-covering choice.
        for pull in (0.0, 0.5, 1.0):
            cand = None
            for r in np.arange(0.03, 1.40, 0.01):
                lo, hi = s_lo + pull * r, s_hi - pull * r
                if hi - lo < MIN_CAPSULE_LENGTH:
                    mid = (s_lo + s_hi) / 2
                    lo, hi = mid - MIN_CAPSULE_LENGTH / 2, mid + MIN_CAPSULE_LENGTH / 2
                a, c = origin + lo * u, origin + hi * u
                if protrusion_p90(a, c, r) > limit:
                    break
                cand = (a, c, float(r))
                if np.percentile(_seg_dist(own, a, c), pct) + margin <= r:
                    break
            if cand is not None:
                cov = float((_seg_dist(own, cand[0], cand[1]) <= cand[2]).mean())
                if cov > cov_new + 1e-9 or (abs(cov - cov_new) <= 1e-9 and cand[2] < best[2]):
                    best, cov_new = cand, cov
        cov_old = float((_seg_dist(own, a0, c0) <= r0).mean())
        row["own_coverage_old"] = cov_old
        if best is None:
            bodies.append(row)
            new_caps.append((a0, c0, r0))
            continue
        a, c, r = best
        if cov_old >= cov_new:
            bodies.append(row)
            new_caps.append((a0, c0, r0))
            continue
        inv = np.linalg.inv(m)
        row.update(action="refit", own_coverage_new=cov_new, protrusion_p90=protrusion_p90(a, c, r),
                   new={"cp1": [float(x) for x in (np.append(a, 1.0) @ inv)[:3]],
                        "cp2": [float(x) for x in (np.append(c, 1.0) @ inv)[:3]], "cr": r})
        bodies.append(row)
        new_caps.append((a, c, r))

    def union(caps):
        inside = np.zeros(len(verts), bool)
        for a, c, r in caps:
            inside |= _seg_dist(verts, a, c) <= r
        return float(inside.mean())

    return {"params": {"limit": limit, "pct": pct, "margin": margin, "min_verts": min_verts, "axis": axis},
            "coverage": {"old": union(old_caps), "new": union(new_caps)}, "bodies": bodies}


def load_skin(path, mesh_names, skel):
    """Blender skin export -> engine-space arrays for the named meshes, using the axis map found from bones."""
    with open(path, encoding="utf-8") as fh:
        skin = json.load(fh)
    engine_heads = {b["name"].strip(): np.asarray(m)[3, :3] for b, m in zip(skel["bones"], skel["world"])}
    blender_heads = {k.strip(): np.array(v[0]) for k, v in skin["bones"].items()}
    perm, signs, err = find_axis_map(blender_heads, engine_heads)
    sgn = np.array(signs, dtype=float)
    missing = set(mesh_names) - {m["name"] for m in skin["meshes"]}
    if missing:
        raise ValueError("meshes not in the export: %s; it has %s" % (sorted(missing), [m["name"] for m in skin["meshes"]]))
    verts, normals, owners = [], [], []
    for mesh in skin["meshes"]:
        if mesh["name"] not in mesh_names:
            continue
        for x, y, z, nx, ny, nz, weights in mesh["verts"]:
            verts.append(np.array([x, y, z])[list(perm)] * sgn)
            normals.append(np.array([nx, ny, nz])[list(perm)] * sgn)
            owners.append(max(weights, key=lambda w: w[1])[0].strip())
    if not verts:
        raise ValueError("no vertices from meshes %s; the export has %s" % (mesh_names, [m["name"] for m in skin["meshes"]]))
    return np.array(verts), np.array(normals), np.array(owners), (perm, signs, err)


def _read(path):
    with open(path, "rb") as fh:
        return fh.read()


def _cmd_show(args):
    sk = read_skeleton(_read(args.tpac), args.skeleton)
    print("skeleton %s (item %r): %d bones, %d bodies" % (sk["name"], sk["item"], len(sk["bones"]), len(sk["bodies"])))
    for b in sk["bodies"]:
        length = float(np.linalg.norm(np.subtract(b["cp2"], b["cp1"])))
        print("  %-30s %-14s hit r %.3f len %.2f | ragdoll r %.3f" % (b["bone"].strip(), b["zone"], b["cr"], length, b["rr"]))
    return 0


def _cmd_fit(args):
    sk = read_skeleton(_read(args.tpac), args.skeleton)
    verts, normals, owners, (perm, signs, err) = load_skin(args.skin, set(args.mesh), sk)
    res = fit_capsules(sk, verts, normals, owners, args.limit, args.pct, args.margin, args.min_verts, axis=args.axis)
    res.update(tpac=os.path.abspath(args.tpac), skeleton=sk["name"], meshes=args.mesh,
               axis_map={"perm": list(perm), "signs": list(signs), "max_error_m": err})
    with open(args.out, "w", encoding="utf-8") as fh:
        json.dump(res, fh, indent=1)
    print("axis map blender->engine perm %s signs %s (bone error %.4f m)" % (list(perm), list(signs), err))
    print("skin vertices inside some hit capsule: %.1f%% -> %.1f%%" % (100 * res["coverage"]["old"], 100 * res["coverage"]["new"]))
    for b in res["bodies"]:
        if b["action"] == "refit":
            print("  refit %-26s verts %4d  r %.2f -> %.2f  own skin covered %3.0f%% -> %3.0f%%  stands out p90 %.2f m" % (
                b["bone"].strip(), b["verts"], b["old"]["cr"], b["new"]["cr"], 100 * b["own_coverage_old"],
                100 * b["own_coverage_new"], b["protrusion_p90"]))
    print("wrote %s (%d refit, %d kept)" % (args.out, sum(b["action"] == "refit" for b in res["bodies"]),
                                           sum(b["action"] == "kept" for b in res["bodies"])))
    return 0


def _cmd_patch(args):
    with open(args.fit, encoding="utf-8") as fh:
        fit = json.load(fh)
    changes = {b["bone"].strip(): (b["new"]["cp1"], b["new"]["cp2"], b["new"]["cr"])
               for b in fit["bodies"] if b["action"] == "refit"}
    raw = _read(args.tpac)
    item = _skeleton_item(tcm.parse(raw), args.skeleton)
    fitted_for = fit.get("skeleton")
    package_skeleton = read_skeleton(raw, args.skeleton)["name"]
    if fitted_for is None:
        print("WARNING: the fit records no skeleton name (hand-made or older fit); not checked against %r"
              % package_skeleton)
    elif fitted_for != package_skeleton:
        print("REFUSED: the fit is for skeleton %r; this package holds %r" % (fitted_for, package_skeleton))
        return 1
    seg = _segment(item, SKELETON_USERDATA_TAG)
    stale = stale_segment_hashes(raw)
    if (item.name, item.segments.index(seg)) in stale:
        print("REFUSED: the skeleton data's stored hash is not the xxHash64 of its data; this tool's model of the"
              " format does not hold for %s" % args.tpac)
        return 1
    out = patch_bodies(raw, changes, args.skeleton)
    if stale_segment_hashes(out) != stale:
        print("REFUSED: the patch changed which segments carry a stale hash: before %s, after %s" % (
            stale, stale_segment_hashes(out)))
        return 1
    check = {b["bone"].strip(): b for b in read_skeleton(out, args.skeleton)["bodies"]}
    for bone, (cp1, cp2, r) in changes.items():
        got = list(check[bone]["cp1"]) + list(check[bone]["cp2"]) + [check[bone]["cr"], check[bone]["cmax"]]
        want = list(cp1) + list(cp2) + [r, r]
        if any(abs(g - w) > 1e-5 for g, w in zip(got, want)):
            print("REFUSED: %s reads back %s, wanted %s" % (bone, got, want))
            return 1
    seg_after = _segment(_skeleton_item(tcm.parse(out), args.skeleton), SKELETON_USERDATA_TAG).storage
    print("%d capsules to write; segment %d -> %d bytes compressed; file %d -> %d bytes" % (
        len(changes), seg.storage, seg_after, len(raw), len(out)))
    if not args.apply:
        print("dry run: nothing written (add --apply)")
        return 0
    if game_or_kit_running():
        print("REFUSED: Bannerlord or the Modding Kit is running; close it and re-run")
        return 2
    backup = "%s.bak-hitcapsules-%s" % (args.tpac, datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
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
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("show", "fit", "patch"):
        p = sub.add_parser(name)
        p.add_argument("--tpac", required=True)
        p.add_argument("--skeleton", default=None, help="skeleton item name, when a package holds more than one")
        if name == "fit":
            p.add_argument("--skin", required=True, help="JSON from tools/blender/export_skin_for_capsules.py")
            p.add_argument("--mesh", required=True, action="append", help="mesh object to fit to (repeatable)")
            p.add_argument("--out", required=True)
            p.add_argument("--limit", type=float, default=0.20, help="max p90 stand-out past the skin, m")
            p.add_argument("--pct", type=float, default=95.0, help="percent of a bone's skin to enclose")
            p.add_argument("--margin", type=float, default=0.03, help="extra radius past that, m")
            p.add_argument("--min-verts", type=int, default=15)
            p.add_argument("--axis", choices=("skin", "bone"), default="skin",
                           help="capsule axis: the skin's principal direction where elongated (skin), or the bone's")
        if name == "patch":
            p.add_argument("--fit", required=True)
            p.add_argument("--apply", action="store_true")
    args = ap.parse_args(argv)
    return {"show": _cmd_show, "fit": _cmd_fit, "patch": _cmd_patch}[args.cmd](args)


if __name__ == "__main__":
    sys.exit(main())
