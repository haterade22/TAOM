#!/usr/bin/env python3
"""
Fit a creature's clips to its body and its held weapon: measure, then correct (2026-09-30, hill troll first).

WHY
The hill troll's clips came from two sources (the Fab troll pack, built round a one-handed club, and vanilla human
clips retargeted onto troll proportions), and neither knows the troll's 4.2 m war hammer or its body. In game the
fingers miss the shaft, the off hand hangs or floats off it, and the hammer passes through the shoulder. This tool
measures those three things per clip, over the frames each clip actually plays, so a correction is a number
against a band read off vanilla, never an eyeballed pose. The math is tools/creature_fit_math.py.

    python tools/creature_fit.py index --anim-dir <Armory>/Assets/Race Test/Mordor/Trolls/animations \\
        --prefix anim_hill_troll_ --out <scratch>/troll_index.json

`index` joins every `<prefix>*_anm.tpac` clip to the master it plays (the clip's animation GUID against each
`*_geo.tpac` package's SkeletalAnimation item) and records the played range (Source1..Source2, either way round),
the hand-pose pair and the flags. A clip whose master is not in the folder is listed as an orphan, never guessed.
"""
import argparse
import glob
import json
import math
import os
import struct
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import set_clip_balance_name as scb  # noqa: E402  (the engine's clip metadata order, 0x58C500)

SKELETAL_ANIMATION = "07b0faba3f7e3f45bac6e7640043112b"


# ------------------------------------------------------------------------------------------------ packages
def read_clip_meta(buf):
    """An AnimationClip package -> name, duration (s), source1, source2, master GUID, hand poses, flags.
    After the metadata version come Duration, Source1, Source2 (floats), three more floats and the priority, the
    master's GUID, four step points, five strings (sound, voice, facial, blends with action, continue to action)
    and the two hand poses (measured on the troll's clips 2026-09-30; the order set_clip_balance_name walks)."""
    c = scb.parse(buf)
    p = c.m0 + 4
    duration, src1, src2 = struct.unpack_from("<3f", buf, p)
    guid = str(uuid.UUID(bytes_le=bytes(buf[p + 28:p + 44])))
    q = p + 28 + 16 + 16
    for _ in range(5):
        q += 4 + struct.unpack_from("<i", buf, q)[0]
    hands = list(struct.unpack_from("<ii", buf, q))
    return {"name": c.name, "duration": round(float(duration), 4), "source1": float(src1), "source2": float(src2),
            "master_guid": guid, "hand_poses": hands, "flags": list(c.flags)}


def played_frames(meta):
    """The master frames a clip plays, in playing order (a blocked clip runs its master backwards)."""
    a, b = int(round(meta["source1"])), int(round(meta["source2"]))
    return list(range(a, b + 1)) if a <= b else list(range(a, b - 1, -1))


def _items(buf):
    """(type GUID hex, item GUID, name) per item: the record walk of set_clip_balance_name.parse, repeated."""
    if buf[:4] != b"TPAC":
        raise ValueError("not a tpac package")
    p = 36
    out = []
    for _ in range(struct.unpack_from("<I", buf, 24)[0]):
        tg, ig = buf[p:p + 16].hex(), str(uuid.UUID(bytes_le=bytes(buf[p + 16:p + 32])))
        p += 36
        n = struct.unpack_from("<i", buf, p)[0]
        name = buf[p + 4:p + 4 + n].decode("utf-8", "replace")
        p += 4 + n
        p += 8 + struct.unpack_from("<q", buf, p)[0] + 8
        p += 4 + scb.SEGMENT * struct.unpack_from("<i", buf, p)[0]
        p += 4 + scb.USERDATA * struct.unpack_from("<i", buf, p)[0]
        out.append((tg, ig, name))
    return out


def master_item(buf):
    """(name, GUID) of a master package's SkeletalAnimation (the Kit adds an import record beside it)."""
    found = [(n, g) for t, g, n in _items(buf) if t == SKELETAL_ANIMATION]
    if len(found) != 1:
        raise ValueError("expected one SkeletalAnimation, found %d" % len(found))
    return found[0]


def build_index(anim_dir, prefix):
    masters = {}
    for fp in glob.glob(os.path.join(anim_dir, prefix + "*_geo.tpac")):
        with open(fp, "rb") as fh:
            name, guid = master_item(fh.read())
        masters[guid] = name
    clips, orphans = {}, []
    for fp in sorted(glob.glob(os.path.join(anim_dir, prefix + "*_anm.tpac"))):
        with open(fp, "rb") as fh:
            meta = read_clip_meta(fh.read())
        meta["master"] = masters.get(meta["master_guid"])
        if meta["master"] is None:
            orphans.append(meta["name"])
        clips[meta["name"]] = meta
    return {"anim_dir": anim_dir, "prefix": prefix, "masters": len(masters), "clips": clips, "orphans": orphans}


# ------------------------------------------------------------------------------------------------ fit data
def hand_pose_channel(left, right):
    """The hand-morph channel a clip's hand-pose pair selects: 5L + R + 1 of the 26 (channel 0 is the neutral).
    Checked on the hill troll's hands 2026-09-30: channel 0 moves nothing, the right hand's closure cycles with
    every channel and the left hand's every fifth."""
    if not (0 <= left <= 4 and 0 <= right <= 4):
        raise ValueError("hand poses run 0..4, got %r %r" % (left, right))
    return 5 * left + right + 1


class FitData:
    """The creature and weapon exported by tools/blender/creature_fit_blender.py --mode export, with the skeleton
    and the crafted layout: everything a measurement needs, in the engine's frame."""

    def __init__(self, spec_path, npz_path):
        import numpy as np
        import creature_fit_math as cfm
        with open(spec_path, encoding="utf-8") as fh:
            self.spec = json.load(fh)
        repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        with open(os.path.join(repo, self.spec["engine_skeleton"]), encoding="utf-8-sig") as fh:
            self.sk = cfm.Skeleton.from_spec(json.load(fh))
        self.rest_R, self.rest_p = self.sk.rest_world()
        d = np.load(npz_path)
        if list(d["bone_names"]) != self.sk.names:
            raise ValueError("the export's bones are not the engine skeleton's, in its order")
        self.meshes = {}
        for i, name in enumerate(d["mesh_names"]):
            m = {k: d["m%d_%s" % (i, k)] for k in ("v", "n", "tri", "idx", "w")}
            if "m%d_keys" % i in d:
                m["keys"] = d["m%d_keys" % i]
            m["dominant"] = m["idx"][:, 0]
            self.meshes[str(name)] = m
        self.layout = {lay["role"]: lay for lay in cfm.crafted_layout(self.spec["weapon"]["pieces"])}
        self.pieces = {}
        i = 0
        while "w%d_name" % i in d:
            self.pieces[str(d["w%d_name" % i])] = d["w%d_v" % i]
            i += 1
        handle = self.pieces[self.layout["handle"]["mesh"]]
        radial = np.hypot(handle[:, 0], handle[:, 1])
        self.shaft_radius = float(np.percentile(radial, 95))
        head = self.pieces[self.layout["head"]["mesh"]]
        self.head_half = (head.max(axis=0) - head.min(axis=0)) / 2

    def bone(self, name):
        return self.sk.index(name)

    def hand_rest(self, left, right):
        """The hand mesh at rest, closed by the hand-pose pair's channel."""
        m = self.meshes[self.spec["hand_mesh"]]
        return m["v"] + m["keys"][hand_pose_channel(left, right)]


def grip_fit(fd, left=3, right=3, zone_half=0.08, reach=0.45):
    """Each fist's cavity (closed by the pair's channel) read in its grip bone's rest frame, against the shaft."""
    import numpy as np
    import creature_fit_math as cfm
    v = fd.hand_rest(left, right)
    dom = fd.meshes[fd.spec["hand_mesh"]]["dominant"]
    out = {"hand_poses": [left, right], "channel": hand_pose_channel(left, right),
           "shaft_radius_m": round(fd.shaft_radius, 4)}
    for side, bone in (("right", fd.spec["weapon"]["grip_bone"]), ("left", fd.spec["weapon"]["offhand_bone"])):
        prefix = bone.split("_")[0] + "_"
        mine = np.array([fd.sk.names[b].startswith(prefix) for b in dom])
        bi = fd.bone(bone)
        c = cfm.fist_cavity(v[mine], fd.rest_R[bi], fd.rest_p[bi], zone_half=zone_half, reach=reach)
        local = (v[mine] - fd.rest_p[bi]) @ fd.rest_R[bi]
        near = local[np.hypot(local[:, 0], local[:, 1]) <= reach]
        out[side] = {
            "grip_bone": bone,
            "cavity_centre_m": None if c["centre"] is None else [round(float(x), 4) for x in c["centre"]],
            "cavity_offset_m": None if c["centre"] is None else round(float(np.linalg.norm(c["centre"])), 4),
            "cavity_radius_m": None if c["radius"] is None else round(c["radius"], 4),
            "coverage": round(c["coverage"], 3), "points": c["n"],
            "fist_extent_along_z_m": [round(float(near[:, 2].min()), 4), round(float(near[:, 2].max()), 4)]}
    return out


# ------------------------------------------------------------------------------------------------ off hand
def offhand_metrics(sk, R_local, p_local, grip, off):
    """Per frame: the off grip's distance from the main grip's Z line, its position along it, and the angle
    between the two grips' Z axes (0 when the thumbs face the same way along the shaft)."""
    import numpy as np
    import creature_fit_math as cfm
    Rw, pw = cfm.fk(sk, R_local, p_local)
    g, o = sk.index(grip), sk.index(off)
    Z = Rw[:, g, :, 2]
    d = pw[:, o] - pw[:, g]
    along = np.einsum("fi,fi->f", d, Z)
    perp = np.linalg.norm(d - along[:, None] * Z, axis=1)
    ang = np.degrees(np.arccos(np.clip(np.einsum("fi,fi->f", Rw[:, o, :, 2], Z), -1.0, 1.0)))
    return perp, along, ang


def hold_weights(fd, vanilla_spec, frames, cfg):
    """Per master frame, 1 where the vanilla source's off hand holds the shaft (within hold_perp_m of the line,
    along inside hold_along_m), eased over ramp_frames either side; 0 where it lets go."""
    import numpy as np
    import creature_fit_math as cfm
    vsk = cfm.Skeleton.from_spec(vanilla_spec)
    last = cfm.clip_length(vanilla_spec) - 1
    vt = [float(min(max(t - cfg["vanilla_frame_offset"], 0), last)) for t in frames]
    R, p = cfm.sample_clip(vsk, vanilla_spec, vt)
    perp, along, _ = offhand_metrics(vsk, R, p, fd.spec["weapon"]["grip_bone"], fd.spec["weapon"]["offhand_bone"])
    lo, hi = cfg["hold_along_m"]
    raw = ((perp <= cfg["hold_perp_m"]) & (along >= lo) & (along <= hi)).astype(float)
    k = int(cfg["ramp_frames"])
    if k > 0 and len(raw) > 1:
        pad = np.pad(raw, k, mode="edge")
        raw = np.convolve(pad, np.ones(2 * k + 1) / (2 * k + 1), mode="valid")
    return np.clip(raw, 0.0, 1.0)


def _summary(perp, along, ang, sel):
    import numpy as np
    if not sel.any():
        return None
    return {"perp_median_m": round(float(np.median(perp[sel])), 4), "perp_max_m": round(float(perp[sel].max()), 4),
            "along_median_m": round(float(np.median(along[sel])), 4), "angle_median_deg": round(float(np.median(ang[sel])), 1)}


def locals_to_json(spec, R):
    """A keyframe JSON like read_anim_keyframes_tpac.ps1 writes, rotations keyed on every frame of R."""
    import numpy as np
    import creature_fit_math as cfm
    out = {k: spec[k] for k in spec if k != "boneAnims"}
    anims = {a["bone"]: a for a in spec.get("boneAnims", [])}
    out["boneAnims"] = []
    for bi, b in enumerate(spec["bones"]):
        prev = None
        keys = []
        for t in range(R.shape[0]):
            q = cfm.mat_to_quat(R[t, bi])
            if prev is not None and float(np.dot(q, prev)) < 0:
                q = -q
            prev = q
            keys.append({"t": float(t), "x": float(q[1]), "y": float(q[2]), "z": float(q[3]), "w": float(q[0])})
        a = anims.get(b["name"], {})
        out["boneAnims"].append({"bone": b["name"], "i": bi, "rot": keys, "pos": a.get("pos") or []})
    return out


def vanilla_source(fd, master):
    """The vanilla master a retargeted one came from, or None (the Fab clips have none). The retarget swapped a
    vanilla master's "anim_" for the creature prefix (anim_guard_up_twohanded -> anim_hill_troll_guard_up_twohanded);
    masters with no "anim_" (stand_right_twohanded) kept their names."""
    d = fd.spec["offhand"]["vanilla_json_dir"]
    stem = master[len(fd.spec["clip_prefix"]):]
    for s in ("anim_" + stem, stem):
        path = os.path.join(d, s + ".json")
        if os.path.exists(path):
            with open(path, encoding="utf-8-sig") as fh:
                return json.load(fh)
    return None


def pass_offhand(fd, R, p, w, report):
    """Put the off grip on the shaft where the vanilla hand holds it (w: the per-frame hold weight)."""
    import creature_fit_math as cfm
    cfg = fd.spec["offhand"]
    grip, off = fd.spec["weapon"]["grip_bone"], fd.spec["weapon"]["offhand_bone"]
    if w is None:
        report["offhand"] = "skipped: no vanilla source"
        return R
    R2, rep = cfm.solve_offhand(fd.sk, R, p, grip, cfg["arm"], cfg["along_m"], w)
    sel = w > 0.5
    report["offhand"] = {"held_frames": int(sel.sum()),
                         "before": _summary(*offhand_metrics(fd.sk, R, p, grip, off), sel),
                         "after": _summary(*offhand_metrics(fd.sk, R2, p, grip, off), sel),
                         "ik_miss_max_m": round(float(rep["miss_m"].max()), 4)}
    return R2


# A head turn is shared between the neck and the head so no one joint bends hard. Never spine2: the collarbones
# hang from it, so turning it swings both arms and the weapon along with the head.
HEAD_SPLIT = (("neck", 0.6), ("head", 0.4))


def split_turn(pw_neck, pw_head, rv):
    """The neck's and the head's world rotations for a shared turn rv: the neck turns first, then the head about
    its moved joint. Returns (R_neck, R_head, head joint after the neck's turn)."""
    import creature_fit_math as cfm
    (_, s1), (_, s2) = HEAD_SPLIT
    R1 = cfm.axis_angle(rv * s1)
    return R1, cfm.axis_angle(rv * s2), pw_neck + R1 @ (pw_head - pw_neck)


def head_turn(fd, Rw, pw, hands, margin, max_deg, directions=8):
    """The smallest turn of the head and neck (a world rotation vector, applied HEAD_SPLIT across spine2 and neck)
    that brings every arm vertex `margin` out of the head's skin. Tried about `directions` axes square to the
    upper spine in its own frame (head back, forward, to either side and between), each bisected up to max_deg:
    the contact can sit at the head's very centre (a shoulder joint inside the skull), where no single push
    direction exists. Returns (rotation vector, depth after; zeros and the depth when already clear)."""
    import numpy as np
    import creature_fit_math as cfm
    head, sp2 = fd.bone("head"), fd.bone("spine2")
    skull = fd.meshes["hill_troll_a_head"]["dominant"] == head
    hv, hn = posed(fd, "hill_troll_a_head", Rw, pw, hands)
    hv, hn = hv[skull], hn[skull]
    arm = _bone_set(fd, ARM)
    pts = []
    for mesh in [m for m in fd.spec["body_meshes"] + [fd.spec["hand_mesh"]] if m != "hill_troll_a_head"]:
        v, _ = posed(fd, mesh, Rw, pw, hands)
        pts.append(v[np.isin(fd.meshes[mesh]["dominant"], list(arm))])
    pts = np.vstack(pts)
    d0 = float(cfm.surface_depth(pts, hv, hn, reach=0.2).max())
    if d0 <= -margin:
        return np.zeros(3), d0
    p_neck, p_head = pw[fd.bone("neck")], pw[head]

    def depth(rv):
        R1, R2, ph = split_turn(p_neck, p_head, rv)
        v = p_neck + (hv - p_neck) @ R1.T
        v = ph + (v - ph) @ R2.T
        return float(cfm.surface_depth(pts, v, hn @ R1.T @ R2.T, reach=0.2).max())
    Y, Z = Rw[sp2][:, 1], Rw[sp2][:, 2]
    best = (None, None)
    cap = math.radians(max_deg)
    for k in range(directions):
        a = 2 * math.pi * k / directions
        axis = math.cos(a) * Y + math.sin(a) * Z
        if depth(axis * cap) > -margin:
            continue
        lo, hi = 0.0, cap
        for _ in range(10):
            mid = (lo + hi) / 2
            if depth(axis * mid) > -margin:
                lo = mid
            else:
                hi = mid
        if best[0] is None or hi < np.linalg.norm(best[0]):
            best = (axis * hi, depth(axis * hi))
    if best[0] is None:
        return np.zeros(3), d0                # nothing within the cap clears it: left for another pass
    return best


def pass_head(fd, R, p, played, hands, report, margin=0.02, max_deg=50.0, lead=4, sigma=2.0, directions=8):
    """Turn the neck and head, on the frames that need it, so no arm vertex is under the head's skin; eased in
    and out."""
    import numpy as np
    import creature_fit_math as cfm
    Rw, pw = cfm.fk(fd.sk, R, p)
    need = np.zeros((len(R), 3))
    for t in played:
        need[t] = head_turn(fd, Rw[t], pw[t], hands, margin, max_deg, directions)[0]
    if not need.any():
        report["head"] = "clear"
        return R
    eased = np.zeros_like(need)
    eased[played] = cfm.ease_series(need[played], lead, sigma)
    R2 = R.copy()
    neck, head = fd.bone("neck"), fd.bone("head")
    spine = fd.sk.parents[neck]
    for t in played:
        R1, Rh, _ = split_turn(pw[t, neck], pw[t, head], eased[t])
        neck_w = R1 @ Rw[t, neck]
        R2[t, neck] = Rw[t, spine].T @ neck_w
        R2[t, head] = neck_w.T @ (Rh @ R1 @ Rw[t, head])
    turned = np.degrees(np.linalg.norm(eased[played], axis=1))
    report["head"] = {"frames_turned": int((turned > 0.5).sum()), "max_turn_deg": round(float(turned.max()), 1)}
    return R2


def arm_head_contact(fd, Rw, pw, hands, margin):
    """(deepest arm vertex under the head's skin, the centroid of the arm vertices within `margin` of it or
    under it, the skull's centre) at one frame."""
    import numpy as np
    import creature_fit_math as cfm
    skull = fd.meshes["hill_troll_a_head"]["dominant"] == fd.bone("head")
    hv, hn = posed(fd, "hill_troll_a_head", Rw, pw, hands)
    hv, hn = hv[skull], hn[skull]
    arm = _bone_set(fd, ARM)
    pts = []
    for mesh in [m for m in fd.spec["body_meshes"] + [fd.spec["hand_mesh"]] if m != "hill_troll_a_head"]:
        v, _ = posed(fd, mesh, Rw, pw, hands)
        pts.append(v[np.isin(fd.meshes[mesh]["dominant"], list(arm))])
    pts = np.vstack(pts)
    d = cfm.surface_depth(pts, hv, hn, reach=0.2)
    near = pts[d > -margin]
    return float(d.max()), (near.mean(axis=0) if len(near) else None), hv.mean(axis=0)


def pass_push(fd, R, p, played, hands, w_hold, report, margin=0.03, max_push=0.45, lead=4, sigma=2.0):
    """Where an arm is under the head's skin, move the weapon away from the head by the smallest distance that
    clears it by `margin`: the main arm reaches the moved grip (same grip rotation) by IK and the off hand
    re-solves onto the moved shaft. The push is eased in and out over the frames around the contact."""
    import numpy as np
    import creature_fit_math as cfm
    cfg = fd.spec["offhand"]
    main, g = cfg["main_arm"], fd.bone(fd.spec["weapon"]["grip_bone"])
    Rw, pw = cfm.fk(fd.sk, R, p)

    def solved(t, shift, count=1):
        Rt, pt = R[t:t + count], p[t:t + count]
        tp = pw[t:t + count, g] + shift
        R1, _ = cfm.solve_arm(fd.sk, Rt, pt, main, tp, Rw[t:t + count, g], np.ones(count))
        if w_hold is not None:
            R1, _ = cfm.solve_offhand(fd.sk, R1, pt, fd.spec["weapon"]["grip_bone"], cfg["arm"], cfg["along_m"],
                                      w_hold[t:t + count])
        return R1

    need = np.zeros((len(R), 3))
    for t in played:
        d0, c, head_c = arm_head_contact(fd, Rw[t], pw[t], hands, margin)
        if d0 <= -margin or c is None:
            continue
        u = c - head_c
        u /= np.linalg.norm(u)

        def depth(s):
            R1 = solved(t, s * u)
            Rw1, pw1 = cfm.fk(fd.sk, R1, p[t:t + 1])
            return arm_head_contact(fd, Rw1[0], pw1[0], hands, margin)[0]
        lo, hi = 0.0, max_push
        if depth(hi) > -margin:
            need[t] = hi * u
            continue
        for _ in range(10):
            mid = (lo + hi) / 2
            if depth(mid) > -margin:
                lo = mid
            else:
                hi = mid
        need[t] = hi * u
    if not need.any():
        report["push"] = "clear"
        return R
    eased = np.zeros_like(need)
    eased[played] = cfm.ease_series(need[played], lead, sigma)
    R2 = R.copy()
    moved = [t for t in played if np.linalg.norm(eased[t]) > 1e-4]
    for t in moved:
        R2[t] = solved(t, eased[t])[0]
    dist = np.linalg.norm(eased[played], axis=1)
    report["push"] = {"frames_moved": len(moved), "max_push_m": round(float(dist.max()), 3),
                      "at_cap_frames": int((np.linalg.norm(need[played], axis=1) >= max_push - 1e-6).sum())}
    return R2


def pass_twohand(fd, R, p, played, report, bend=0.97, sigma=1.5, min_forearm_deg=60.0):
    """Make a one-handed swing two-handed, weapon first: each frame the main grip moves the shortest distance
    into a place both arms can reach, the off grip riding the shaft `along_m` below it, then both arms are solved
    onto the hammer. The grip's roll stays the clip's; the shift is smoothed over time. `bend` keeps each arm
    short of straight (1.0 would lock the elbows). A one-handed club may lie along the forearm; a two-handed shaft
    crosses the palm, so where the shaft comes within `min_forearm_deg` of the main forearm's line the weapon turns
    about the fist by the difference (the hill troll's butt reaches 0.8 m below the fist and ran up the forearm)."""
    import numpy as np
    import creature_fit_math as cfm
    cfg = fd.spec["offhand"]
    main, off, along = cfg["main_arm"], cfg["arm"], cfg["along_m"]
    rg, lg = fd.bone(main["grip"]), fd.bone(off["grip"])

    def reach(arm):
        s, e, w = (fd.rest_p[fd.bone(arm[k])] for k in ("upper", "fore", "hand"))
        return bend * (np.linalg.norm(e - s) + np.linalg.norm(w - e))
    Lr, Ll = reach(main), reach(off)
    Rw, pw = cfm.fk(fd.sk, R, p)
    weapon_R = Rw[:, rg].copy()
    turned = np.zeros(len(R))
    fa, ha = fd.bone(main["fore"]), fd.bone(main["hand"])
    for t in played:
        F = pw[t, ha] - pw[t, fa]
        F /= np.linalg.norm(F)
        Z = weapon_R[t][:, 2]
        ang = math.degrees(math.acos(max(-1.0, min(1.0, float(Z @ F)))))
        if ang < min_forearm_deg:
            n = np.cross(F, Z)
            if np.linalg.norm(n) < 1e-9:
                n = np.cross(F, [0.0, 0.0, 1.0])
            n /= np.linalg.norm(n)
            weapon_R[t] = cfm.axis_angle(n * math.radians(min_forearm_deg - ang)) @ weapon_R[t]
            turned[t] = min_forearm_deg - ang
    shift = np.zeros((len(R), 3))
    for t in played:
        Rg = weapon_R[t]
        Z = Rg[:, 2]
        c_r = -(Rg @ R[t, rg].T) @ p[t, rg]
        R_lt = cfm.rotation_between(Rw[t, lg][:, 2], Z) @ Rw[t, lg]
        c_l = along * Z - (R_lt @ R[t, lg].T) @ p[t, lg]
        centres = [pw[t, fd.bone(main["upper"])] - c_r, pw[t, fd.bone(off["upper"])] - c_l]
        shift[t] = cfm.project_into_balls(pw[t, rg], centres, [Lr, Ll]) - pw[t, rg]
    smooth = np.zeros_like(shift)
    smooth[played] = cfm.ease_series(shift[played], 0, sigma)
    w = np.zeros(len(R))
    w[played] = 1.0
    R2, rep_r = cfm.solve_arm(fd.sk, R, p, main, pw[:, rg] + smooth, weapon_R, w)
    R3, rep_l = cfm.solve_offhand(fd.sk, R2, p, fd.spec["weapon"]["grip_bone"], off, along, w)
    moved = np.linalg.norm(smooth[played], axis=1)
    report["twohand"] = {"forearm_turn_max_deg": round(float(turned.max()), 1),
                         "forearm_turned_frames": int((turned > 0).sum()),
                         "grip_shift_max_m": round(float(moved.max()), 3),
                         "grip_shift_median_m": round(float(np.median(moved)), 3),
                         "main_miss_max_m": round(float(rep_r["miss_m"].max()), 3),
                         "off_miss_max_m": round(float(rep_l["miss_m"].max()), 3)}
    return R3


def runs(mask):
    """[(start, end_exclusive)] of the True stretches of a boolean array."""
    out, start = [], None
    for i, m in enumerate(list(mask) + [False]):
        if m and start is None:
            start = i
        elif not m and start is not None:
            out.append((start, i))
            start = None
    return out


def join_weights(ok, min_run=6, gap=3, ramp=5):
    """Per frame 0..1: where the off hand holds. Accepted stretches shorter than min_run are dropped (no
    flicker), gaps of `gap` frames or less inside a hold are filled, and the hand eases on and off over `ramp`
    frames at each end of a hold (smoothstep), inside the accepted stretch."""
    import numpy as np
    ok = np.asarray(ok, dtype=bool).copy()
    for a, b in runs(~ok):
        if 0 < a and b < len(ok) and b - a <= gap:
            ok[a:b] = True
    w = np.zeros(len(ok))
    for a, b in runs(ok):
        if b - a < min_run:
            continue
        for i in range(a, b):
            k = min(i - a + 1, b - i, ramp) / float(ramp)
            w[i] = k * k * (3 - 2 * k)
    return w


def slide_along(fd, R, p, played, lo=(-0.78, -0.55), hi=(0.55, 2.6), sigma=3.0):
    """Per frame, where on the shaft the off hand holds: the handle point nearest its shoulder, below the main fist
    (lo, metres along the grip's Z) or above it (hi), never within the fists' overlap; smoothed so the hand
    slides, not jumps. A heavy hammer's top hand slides the same way."""
    import numpy as np
    import creature_fit_math as cfm
    Rw, pw = cfm.fk(fd.sk, R, p)
    g = fd.bone(fd.spec["weapon"]["grip_bone"])
    S = pw[:, fd.bone(fd.spec["offhand"]["arm"]["upper"])]
    Z = Rw[:, g, :, 2]
    a = np.einsum("fi,fi->f", S - pw[:, g], Z)
    below, above = np.clip(a, *lo), np.clip(a, *hi)
    best = np.where(np.abs(a - below) <= np.abs(a - above), below, above)
    out = np.full(len(R), fd.spec["offhand"]["along_m"], dtype=float)
    out[played] = cfm.ease_series(best[played], 0, sigma)
    return out


def pass_offhand_join(fd, R, p, played, hands, report, miss_max=0.03, worse_max=0.03, elbow_max=150.0,
                      wrist_turn_max=75.0, slide=True):
    """Keep the clip's main arm and weapon; the off hand grips the shaft only on frames where that is clean: the
    IK reaches (miss <= miss_max), the off arm is no deeper in the head or the torso than the clip's own off arm
    by more than worse_max, the elbow bends no more than elbow_max and the wrist turns no more than
    wrist_turn_max from its rest relation. With `slide` the hold point moves along the shaft (slide_along);
    without, it stays at the spec's along_m. See join_weights for how the accepted frames become a hold."""
    import numpy as np
    import creature_fit_math as cfm
    cfg = fd.spec["offhand"]
    grip = fd.spec["weapon"]["grip_bone"]
    full = np.zeros(len(R))
    full[played] = 1.0
    along = slide_along(fd, R, p, played) if slide else cfg["along_m"]
    Rj, rep = cfm.solve_offhand(fd.sk, R, p, grip, cfg["arm"], along, full)
    Rw0, pw0 = cfm.fk(fd.sk, R, p)
    Rw1, pw1 = cfm.fk(fd.sk, Rj, p)
    _, wrist_rest = arm_shape(fd, fd.rest_R, fd.rest_p, "l_")
    ok = np.zeros(len(R), dtype=bool)
    why = {"miss": 0, "head": 0, "torso": 0, "elbow": 0, "wrist": 0}
    for t in played:
        if rep["miss_m"][t] > miss_max:
            why["miss"] += 1
            continue
        e, wr = arm_shape(fd, Rw1[t], pw1[t], "l_")
        if e > elbow_max:
            why["elbow"] += 1
            continue
        if abs(wr - wrist_rest) > wrist_turn_max:
            why["wrist"] += 1
            continue
        h0, b0 = arm_depths(fd, Rw0[t], pw0[t], hands, "l_")
        h1, b1 = arm_depths(fd, Rw1[t], pw1[t], hands, "l_")
        if h1 > max(h0, -0.02) + worse_max:
            why["head"] += 1
            continue
        if b1 > max(b0, 0.0) + worse_max:
            why["torso"] += 1
            continue
        ok[t] = True
    w = np.zeros(len(R))
    w[played] = join_weights(ok[played])
    R2, _ = cfm.solve_offhand(fd.sk, R, p, grip, cfg["arm"], along, w)
    held = [(played[a], played[b - 1]) for a, b in runs(w[played] > 0.999)]
    alongs = np.broadcast_to(np.asarray(along, dtype=float), (len(R),))[w > 0.999]
    report["offhand_join"] = {"clean_frames": int(ok.sum()), "held_frames": int((w > 0.999).sum()),
                              "holds": held, "rejected": why,
                              "along_m": [round(float(alongs.min()), 2), round(float(alongs.max()), 2)]
                              if len(alongs) else None}
    return R2


def body_frames(fd, Rw, pw):
    """Per frame: the body's yaw frame (columns x right, y forward, z up; forward is the pelvis's local +Y laid
    flat) and the midpoint of the two shoulder joints."""
    import numpy as np
    pel = fd.bone("pelvis")
    B, mid = [], []
    for f in range(len(Rw)):
        fwd = Rw[f, pel][:, 1].copy()
        fwd[2] = 0.0
        fwd /= np.linalg.norm(fwd)
        up = np.array([0.0, 0.0, 1.0])
        B.append(np.stack([np.cross(fwd, up), fwd, up], axis=1))
        mid.append((pw[f, fd.bone("l_upperarm_twist")] + pw[f, fd.bone("r_upperarm_twist")]) / 2)
    return np.array(B), np.array(mid)


def author_swing(fd, body_spec, swing, along=None):
    """A two-handed swing: the body from `body_spec` (every bone but the arms), the main grip on the keyed
    hammer path (see tools/fit_specs/hill_troll_swings.json for the units), the off grip `along` down the shaft,
    both elbows poled as the spec says. Returns (R_local, p_local, report)."""
    import numpy as np
    import creature_fit_math as cfm
    cfg = fd.spec["offhand"]
    along = cfg["along_m"] if along is None else along
    n = cfm.clip_length(body_spec)
    R, p = cfm.sample_clip(fd.sk, body_spec, [float(t) for t in range(n)])
    hold = swing.get("head_hold")
    if hold:
        # The Fab raise throws the head back over the shoulders, where a two-handed off arm has to pass: keep the
        # neck and head as they sit on the chest at frame `from` (the stance), eased in and out over `ramp`.
        a0, b0, ramp = hold["frames"][0], hold["frames"][1], hold.get("ramp", 6)
        for t in range(max(0, a0 - ramp), min(n, b0 + ramp + 1)):
            k = min(1.0, (t - (a0 - ramp)) / float(ramp), ((b0 + ramp) - t) / float(ramp))
            k = k * k * (3 - 2 * k)
            for bn in ("neck", "head"):
                bi = fd.bone(bn)
                R[t, bi] = cfm.quat_to_mat(cfm.slerp(cfm.mat_to_quat(R[t, bi]), cfm.mat_to_quat(R[hold["from"], bi]), k))
    Rw, pw = cfm.fk(fd.sk, R, p)
    B, mid = body_frames(fd, Rw, pw)
    a, b = swing["played"]
    frames = list(range(a, b + 1))
    keys = swing["keys"]
    kt = [k["f"] for k in keys]
    fist = cfm.catmull_rom(kt, np.array([k["fist"] for k in keys]), frames)
    dirs = cfm.catmull_rom(kt, np.array([k["dir"] for k in keys]), frames)
    target_p = pw[:, fd.bone(fd.spec["weapon"]["grip_bone"])].copy()
    target_R = Rw[:, fd.bone(fd.spec["weapon"]["grip_bone"])].copy()
    heads = []
    for i, t in enumerate(frames):
        world = mid[t] + B[t] @ np.array([fist[i, 0], fist[i, 1], 0.0])
        world[2] = fist[i, 2]
        Z = B[t] @ dirs[i]
        Z /= np.linalg.norm(Z)
        target_p[t] = world
        heads.append(world + fd.layout["head"]["z_centre"] * Z)
        target_R[t][:, 2] = Z
    heads = np.array(heads)
    X_prev = None
    for i, t in enumerate(frames):         # the striking face (-X) leads the hammer head's motion
        Z = target_R[t][:, 2]
        v = heads[min(i + 1, len(frames) - 1)] - heads[max(i - 1, 0)]
        v = v - (v @ Z) * Z
        if np.linalg.norm(v) > 1e-4:
            X = -v / np.linalg.norm(v)
            if X_prev is not None and X @ X_prev < -0.5 and np.linalg.norm(v) < 0.05:
                X = X_prev                  # a near-stop: keep the face rather than flip it
        else:
            X = X_prev if X_prev is not None else np.cross(B[t][:, 0], Z)
        X = X - (X @ Z) * Z
        X /= np.linalg.norm(X)
        target_R[t] = np.stack([X, np.cross(Z, X), Z], axis=1)
        X_prev = X
    w = np.zeros(n)
    w[frames] = 1.0
    poles = swing.get("poles", {})
    pole_main = np.array([B[t] @ np.array(poles.get("main", [0.9, -0.2, -0.5])) for t in range(n)])
    pole_off = np.array([B[t] @ np.array(poles.get("off", [-0.9, -0.2, -0.5])) for t in range(n)])
    R2, rep_m = cfm.solve_arm(fd.sk, R, p, cfg["main_arm"], target_p, target_R, w, pole_dir=pole_main)
    R3, rep_o = cfm.solve_offhand(fd.sk, R2, p, fd.spec["weapon"]["grip_bone"], cfg["arm"], along, w,
                                  pole_dir=pole_off)
    report = {"frames": [a, b], "main_miss_max_m": round(float(rep_m["miss_m"].max()), 3),
              "off_miss_max_m": round(float(rep_o["miss_m"].max()), 3),
              "off_miss_frames": [frames[i] for i in range(len(frames)) if rep_o["miss_m"][frames[i]] > 0.03],
              "main_miss_frames": [frames[i] for i in range(len(frames)) if rep_m["miss_m"][frames[i]] > 0.03],
              "hammer_head_min_z_m": round(float(heads[:, 2].min() - fd.head_half[0] * 0), 3)}
    return R3, p, report


def correct_masters(fd, index, masters, json_dir, out_dir, passes, head_max_deg=50.0, offhand_always=False):
    """Run the passes over each master's played frames and write the corrected keyframe JSON."""
    import numpy as np
    import creature_fit_math as cfm
    os.makedirs(out_dir, exist_ok=True)
    report = {}
    for master in masters:
        with open(os.path.join(json_dir, master + ".json"), encoding="utf-8-sig") as fh:
            spec = json.load(fh)
        clips = [c for c in index.values() if c.get("master") == master]
        played = sorted({t for c in clips for t in played_frames(c)})
        rep = report[master] = {"frames": cfm.clip_length(spec), "played": [played[0], played[-1]] if played else None}
        if not played:
            rep["skipped"] = "no clip plays it"
            continue
        hands = tuple(clips[0]["hand_poses"])
        R, p = cfm.sample_clip(fd.sk, spec, [float(t) for t in range(rep["frames"])])
        R0 = R.copy()
        vspec = vanilla_source(fd, master)
        w_hold = None
        if vspec is not None:
            w_hold = np.zeros(len(R))
            w_hold[played] = hold_weights(fd, vspec, played, fd.spec["offhand"])
        elif offhand_always:                   # a clip with no vanilla source (the Fab set): both hands throughout
            w_hold = np.zeros(len(R))
            w_hold[played] = 1.0
        for name in passes:
            if name == "offhand":
                R = pass_offhand(fd, R, p, w_hold, rep)
            elif name == "push":
                R = pass_push(fd, R, p, played, hands, w_hold, rep)
            elif name == "twohand":
                R = pass_twohand(fd, R, p, played, rep)
            elif name == "join":
                R = pass_offhand_join(fd, R, p, played, hands, rep)
            elif name == "head":
                R = pass_head(fd, R, p, played, hands, rep, max_deg=head_max_deg,
                              directions=16 if head_max_deg > 50 else 8)
            else:
                raise ValueError("unknown pass %r" % name)
        rep["max_turn_deg"] = {fd.sk.names[b]: round(max(cfm.angle_deg(R0[t, b].T @ R[t, b]) for t in played), 1)
                               for b in range(len(fd.sk.names)) if not np.allclose(R0[:, b], R[:, b])}
        with open(os.path.join(out_dir, master + ".json"), "w", encoding="utf-8") as fh:
            json.dump(locals_to_json(spec, R), fh)
    return report


# ------------------------------------------------------------------------------------------------ clearance
# The arm below the shoulder joint. The collarbone is left out: its skin meets the neck where the head mesh ends, so
# it reads "inside the head" in every clip, the calm idles included (2026-09-30: 6 to 11 cm on all eight clips).
ARM = ("upperarm_twist", "upperarm_twist1", "foretwist", "foretwist1", "hand", "finger0")
WRIST = ("foretwist1", "hand", "finger0")


def _bone_set(fd, names, sides=("l_", "r_")):
    return {fd.bone(s + n) for s in sides for n in names if (s + n) in fd.sk._index}


def posed(fd, mesh, Rw, pw, hands=(3, 3)):
    """A mesh's vertices and normals at one frame's world bone frames (the hand mesh closed by the pose pair)."""
    import creature_fit_math as cfm
    m = fd.meshes[mesh]
    v = fd.hand_rest(*hands) if mesh == fd.spec["hand_mesh"] else m["v"]
    return (cfm.skin(v, m["idx"], m["w"], fd.rest_R, fd.rest_p, Rw, pw),
            cfm.skin_normals(m["n"], m["idx"], m["w"], fd.rest_R, Rw))


def clearance(fd, R_local, p_local, hands=(3, 3)):
    """Per frame: how deep any arm or shoulder vertex is under the head's skin, and how deep any body vertex is
    inside the held weapon (the shaft's capsule, the head's box), wrists and fists left out since they hold it.
    Returns arrays of depth (m, negative = clear) and the bone owning the deepest vertex."""
    import numpy as np
    import creature_fit_math as cfm
    Rw, pw = cfm.fk(fd.sk, R_local, p_local)
    arm = _bone_set(fd, ARM)
    wrist = _bone_set(fd, WRIST)
    g = fd.bone(fd.spec["weapon"]["grip_bone"])
    hl, dl = fd.layout["handle"], fd.layout["head"]
    names = fd.sk.names
    F = len(R_local)
    out = {"arm_head": np.full(F, -1.0), "arm_head_bone": [""] * F,
           "weapon_body": np.full(F, -1.0), "weapon_body_bone": [""] * F}
    arm_meshes = [m for m in fd.spec["body_meshes"] + [fd.spec["hand_mesh"]] if m != "hill_troll_a_head"]
    skull = fd.meshes["hill_troll_a_head"]["dominant"] == fd.bone("head")   # not the neck seam
    for f in range(F):
        head_v, head_n = posed(fd, "hill_troll_a_head", Rw[f], pw[f], hands)
        pts, owners = [], []
        body_pts, body_owner = [], []
        for mesh in arm_meshes:
            v, _ = posed(fd, mesh, Rw[f], pw[f], hands)
            dom = fd.meshes[mesh]["dominant"]
            sel = np.isin(dom, list(arm))
            pts.append(v[sel])
            owners.append(dom[sel])
            if mesh != fd.spec["hand_mesh"]:
                keep = ~np.isin(dom, list(wrist))
                body_pts.append(v[keep])
                body_owner.append(dom[keep])
        pts, owners = np.vstack(pts), np.concatenate(owners)
        d = cfm.surface_depth(pts, head_v[skull], head_n[skull], reach=0.2)
        i = int(np.argmax(d))
        out["arm_head"][f], out["arm_head_bone"][f] = float(d[i]), names[owners[i]]
        bp = np.vstack(body_pts + [head_v])
        bo = np.concatenate(body_owner + [fd.meshes["hill_troll_a_head"]["dominant"]])
        Z = Rw[f, g][:, 2]
        a, b = pw[f, g] + hl["z_min"] * Z, pw[f, g] + hl["z_max"] * Z
        dep = np.maximum(cfm.capsule_depth(bp, a, b, fd.shaft_radius),
                         cfm.box_depth(bp, pw[f, g] + dl["z_centre"] * Z, Rw[f, g], fd.head_half))
        j = int(np.argmax(dep))
        out["weapon_body"][f], out["weapon_body_bone"][f] = float(dep[j]), names[bo[j]]
    return out


TORSO = ("pelvis", "spine", "spine1", "spine2")


def arm_depths(fd, Rw, pw, hands, side, shoulder_clear=0.35):
    """One arm at one frame: (depth under the head's skin, depth under the torso's skin). The arm is its
    upper-arm-to-grip skin (the hand mesh closed by the pose pair); for the torso test the part within
    `shoulder_clear` of the shoulder joint is left out, since the armpit meets the chest by design."""
    import numpy as np
    import creature_fit_math as cfm
    arm = {fd.bone(side + n) for n in ARM}
    torso = {fd.bone(n) for n in TORSO}
    skull = fd.meshes["hill_troll_a_head"]["dominant"] == fd.bone("head")
    hv, hn = posed(fd, "hill_troll_a_head", Rw, pw, hands)
    tv, tn, pts = [], [], []
    for mesh in [m for m in fd.spec["body_meshes"] + [fd.spec["hand_mesh"]] if m != "hill_troll_a_head"]:
        v, n = posed(fd, mesh, Rw, pw, hands)
        dom = fd.meshes[mesh]["dominant"]
        pts.append(v[np.isin(dom, list(arm))])
        keep = np.isin(dom, list(torso))
        tv.append(v[keep])
        tn.append(n[keep])
    pts, tv, tn = np.vstack(pts), np.vstack(tv), np.vstack(tn)
    head = float(cfm.surface_depth(pts, hv[skull], hn[skull], reach=0.2).max())
    far = pts[np.linalg.norm(pts - pw[fd.bone(side + "upperarm_twist")], axis=1) > shoulder_clear]
    body = float(cfm.surface_depth(far, tv, tn, reach=0.2).max()) if len(far) else -1.0
    return head, body


def arm_shape(fd, Rw, pw, side):
    """(elbow bend, wrist bend) in degrees at one frame: the angle between the upper arm and the forearm, and
    between the forearm and the hand's own axis."""
    import numpy as np
    S, E, W = (pw[fd.bone(side + n)] for n in ("upperarm_twist", "foretwist", "hand"))
    u, f = E - S, W - E
    h = Rw[fd.bone(side + "hand")][:, 0]

    def ang(a, b):
        return math.degrees(math.acos(max(-1.0, min(1.0, float(a @ b) / (np.linalg.norm(a) * np.linalg.norm(b))))))
    return ang(u, f), ang(f, h)


def measure_clip(fd, clip_json, meta):
    import numpy as np
    import creature_fit_math as cfm
    frames = played_frames(meta)
    R, p = cfm.sample_clip(fd.sk, clip_json, [float(t) for t in frames])
    c = clearance(fd, R, p, tuple(meta["hand_poses"]))
    perp, along, ang = offhand_metrics(fd.sk, R, p, fd.spec["weapon"]["grip_bone"], fd.spec["weapon"]["offhand_bone"])

    def worst(key):
        i = int(np.argmax(c[key]))
        return {"max_depth_m": round(float(c[key][i]), 3), "frame": frames[i], "bone": c[key + "_bone"][i],
                "frames_over_2cm": int((c[key] > 0.02).sum())}
    Rw, pw = cfm.fk(fd.sk, R, p)
    torso = {}
    for side in ("l_", "r_"):
        d = np.array([arm_depths(fd, Rw[i], pw[i], tuple(meta["hand_poses"]), side)[1] for i in range(len(frames))])
        i = int(np.argmax(d))
        torso[side[0]] = {"max_depth_m": round(float(d[i]), 3), "frame": frames[i],
                          "frames_over_2cm": int((d > 0.02).sum())}
    return {"frames": [frames[0], frames[-1]], "arm_head": worst("arm_head"), "weapon_body": worst("weapon_body"),
            "arm_torso": torso,
            "offhand_perp_median_m": round(float(np.median(perp)), 3), "offhand_perp_max_m": round(float(perp.max()), 3),
            "offhand_along_median_m": round(float(np.median(along)), 3)}


# ------------------------------------------------------------------------------------------------ CLI
def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[1] if __doc__ else None)
    sub = ap.add_subparsers(dest="cmd", required=True)
    ix = sub.add_parser("index", help="join clips to masters, played ranges, hand poses, flags")
    ix.add_argument("--anim-dir", required=True)
    ix.add_argument("--prefix", required=True)
    ix.add_argument("--out", required=True)
    gp = sub.add_parser("grip", help="each fist's cavity against the shaft, per hand-pose pair")
    gp.add_argument("--spec", required=True)
    gp.add_argument("--mesh", required=True, help="the .npz from creature_fit_blender.py --mode export")
    gp.add_argument("--poses", default="3,3", help="left,right hand poses (0..4)")
    co = sub.add_parser("correct", help="solve the off hand onto the shaft; write corrected keyframe JSON")
    co.add_argument("--spec", required=True)
    co.add_argument("--mesh", required=True)
    co.add_argument("--index", required=True)
    co.add_argument("--json-dir", required=True)
    co.add_argument("--out-dir", required=True)
    co.add_argument("--clips", nargs="+", required=True, help="clip names; each one's master is corrected once")
    co.add_argument("--passes", default="offhand,head", help="in order: offhand (grip on the shaft), head (clear "
                                                             "the arms), push (move the weapon off the head)")
    co.add_argument("--head-max-deg", type=float, default=50.0, help="the largest head turn the head pass may use")
    co.add_argument("--offhand-always", action="store_true",
                    help="a master with no vanilla source gets the off hand on the shaft on every played frame")
    au = sub.add_parser("author", help="build a two-handed swing: Fab body, keyed hammer path, both arms solved")
    au.add_argument("--spec", required=True)
    au.add_argument("--mesh", required=True)
    au.add_argument("--index", required=True)
    au.add_argument("--json-dir", required=True)
    au.add_argument("--swings", required=True, help="tools/fit_specs/<creature>_swings.json")
    au.add_argument("--swing", required=True, help="the swing's key in that file")
    au.add_argument("--out-dir", required=True)
    me = sub.add_parser("measure", help="per clip: arm into head, weapon into body, off hand vs shaft")
    me.add_argument("--spec", required=True)
    me.add_argument("--mesh", required=True)
    me.add_argument("--index", required=True)
    me.add_argument("--json-dir", required=True, help="keyframe JSON per master (original or corrected)")
    me.add_argument("--clips", nargs="+", required=True)
    me.add_argument("--out", default=None, help="write the per-clip results here as JSON")
    args = ap.parse_args(argv)
    if args.cmd == "author":
        with open(args.swings, encoding="utf-8") as fh:
            swing = json.load(fh)[args.swing]
        fd = FitData(args.spec, args.mesh)
        with open(os.path.join(args.json_dir, swing["body"] + ".json"), encoding="utf-8-sig") as fh:
            body = json.load(fh)
        R, p, rep = author_swing(fd, body, swing)
        name = "%s_%s_2h" % (swing["body"], args.swing)
        os.makedirs(args.out_dir, exist_ok=True)
        with open(os.path.join(args.out_dir, name + ".json"), "w", encoding="utf-8") as fh:
            json.dump(locals_to_json(body, R), fh)
        # an index beside it, so measure and the Blender scene can play the new master as a clip
        with open(args.index, encoding="utf-8") as fh:
            idx = json.load(fh)
        a, b = swing["played"]
        idx["clips"][name] = {"name": name, "master": name, "source1": float(a), "source2": float(b),
                              "hand_poses": [3, 3], "flags": [], "duration": round((b - a + 1) / 30.0, 2)}
        with open(os.path.join(args.out_dir, "index.json"), "w", encoding="utf-8") as fh:
            json.dump(idx, fh)
        print(json.dumps({"clip": name, **rep}, indent=1))
        return 0
    if args.cmd == "measure":
        with open(args.index, encoding="utf-8") as fh:
            index = json.load(fh)["clips"]
        fd = FitData(args.spec, args.mesh)
        res = {}
        for clip in args.clips:
            meta = index.get(clip)
            if not meta or not meta.get("master"):
                res[clip] = {"error": "not in the index"}
                continue
            with open(os.path.join(args.json_dir, meta["master"] + ".json"), encoding="utf-8-sig") as fh:
                res[clip] = measure_clip(fd, json.load(fh), meta)
            r = res[clip]
            print("%-30s arm>head %+.3f @%-4s %-17s | arm>torso L %+.3f @%-4s R %+.3f @%-4s | weapon>body %+.3f @%-4s "
                  "%-12s | offhand %.2f (max %.2f)" % (
                      clip.replace(fd.spec["clip_prefix"], ""), r["arm_head"]["max_depth_m"], r["arm_head"]["frame"],
                      r["arm_head"]["bone"], r["arm_torso"]["l"]["max_depth_m"], r["arm_torso"]["l"]["frame"],
                      r["arm_torso"]["r"]["max_depth_m"], r["arm_torso"]["r"]["frame"],
                      r["weapon_body"]["max_depth_m"], r["weapon_body"]["frame"], r["weapon_body"]["bone"],
                      r["offhand_perp_median_m"], r["offhand_perp_max_m"]))
        if args.out:
            with open(args.out, "w", encoding="utf-8") as fh:
                json.dump(res, fh, indent=1)
        return 0
    if args.cmd == "correct":
        with open(args.index, encoding="utf-8") as fh:
            index = json.load(fh)["clips"]
        fd = FitData(args.spec, args.mesh)
        masters = sorted({index[c]["master"] for c in args.clips if index.get(c, {}).get("master")})
        rep = correct_masters(fd, index, masters, args.json_dir, args.out_dir, args.passes.split(","),
                              head_max_deg=args.head_max_deg, offhand_always=args.offhand_always)
        with open(os.path.join(args.out_dir, "correct_report.json"), "w", encoding="utf-8") as fh:
            json.dump(rep, fh, indent=1)
        print(json.dumps(rep, indent=1))
        return 0
    if args.cmd == "grip":
        left, right = (int(x) for x in args.poses.split(","))
        print(json.dumps(grip_fit(FitData(args.spec, args.mesh), left, right), indent=1))
        return 0
    if args.cmd == "index":
        idx = build_index(args.anim_dir, args.prefix)
        with open(args.out, "w", encoding="utf-8") as fh:
            json.dump(idx, fh, indent=1)
        print("%d clips, %d masters, %d orphans -> %s" % (len(idx["clips"]), idx["masters"], len(idx["orphans"]),
                                                         args.out))
        return 1 if idx["orphans"] else 0
    return 2


if __name__ == "__main__":
    sys.exit(main())
