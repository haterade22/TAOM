#!/usr/bin/env python3
"""Retarget a real elephant walk onto TAOM's elephant_skeleton and derive the faster ambles (#1474).

Pure Python and numpy, no bpy. Inputs are two JSON files that headless Blender writes:
  --pack  tools/blender/elephant_pack_sample.py: the source rig's deform bones per frame, world space
          (head, tail, 3x3 axes), with root motion
  --rig   tools/blender/elephant_rig_dump.py: elephant_skeleton's rest bones (name, parent, head, matrix_local)
Output, one JSON per gait in --out: for frame 0 (rest) and frames 1..N, every bone's armature-space 4x4, plus
the numbers the clip metadata needs (stride per loop = LoopDisplacement, plant fractions = StepPoints).
tools/blender/elephant_apply_clip.py keys those matrices on the rig, renders the check sheet and exports the FBX.

Both rigs face -Y in Blender space; the engine's model space is the same rig turned 180 degrees about Z.

Method
  treadmill   the source walks forward at a constant speed; subtracting speed x time leaves an in-place
              cycle in which a planted foot moves back at exactly that speed.
  body        the pelvis follows the midpoint and line of the source's hip joints; spine, neck and head take
              the source's world-space rotation change from its rest pose; trunk, ears and tail take the
              change from the source's mean pose, so they swing about our own rest shape.
  legs        wrist and ankle targets are rest + scale x (source joint - source rest joint); the shoulder
              follows the source's by turning the clavicle; a 2-bone solve places elbow and knee in the
              source limb's bend plane. A target beyond the leg's length is pulled in and reported.
  gaits       an elephant never leaves the ground; it runs with the same footfall order and a shorter
              stance share. --gait name:q:frames re-times each leg about its own plant: the stance keeps
              its path and takes 1/q of its old share of the cycle, so the stride is q x the walk's.
              The stance stays linear in time (no slip); the swing eases in and out of the stance rate.
"""
import argparse
import json
import math
import os

import numpy as np

FWD = np.array([0.0, -1.0, 0.0])   # both rigs face -Y in Blender space
SIDES = ("L", "R")

# TAOM bone <- source bone, world rotation change from the source REST pose
REST_MAP = {
    " Spine_04": "DEF_Spine.003", " Spine1_05": "DEF_Spine.007",
    " Neck_06": "DEF_Neck", " Neck1_07": "DEF_Neck.002", " Head_08": "DEF_Head",
}
# TAOM chain <- source chain, world rotation change from the source MEAN pose, matched by position along the chain
TRUNK = [" Queue de cheval 1_09", " Queue de cheval 11_010", " Queue de cheval 12_011", " Queue de cheval 13_012",
         " Queue de cheval 14_00", " Queue de cheval 15_013", " Queue de cheval 16_014", " Queue de cheval 17_015",
         " Queue de cheval 18_016", " Queue de cheval 19_017", " Queue de cheval 110_018", " Queue de cheval 111_019",
         " Queue de cheval 112_020", " Queue de cheval 113_021"]
SRC_TRUNK = ["DEF_Nose"] + ["DEF_Nose.%03d" % i for i in range(1, 16)]
TAIL = [" Tail_041", " Tail1_042", " Tail2_043", " Tail3_044", " Tail4_045", " Tail5_046", " Tail6_047"]
SRC_TAIL = ["DEF_Tail"] + ["DEF_Tail.%03d" % i for i in range(1, 13)]
EARS = {"ear_L_1_023": "DEF_Ear.L", "ear_L_2_024": "DEF_Ear.003.L", "ear_R_1_025": "DEF_Ear.R", "ear_R_2_026": "DEF_Ear.003.R"}

EAR_SWING = 0.55   # the source fans its ears to the vertical in every stride; our ear is two bones, so less reads better
CARRIAGE = (" Neck_06", " Neck1_07", " Head_08")

# TAOM spine bone: (the TAOM points it carries, the source point they follow)
# The chest follows the midpoint of the source's SHOULDER JOINTS, not its neck base: the source shoulder blade
# slides down by up to 18 cm in a stride, and our short clavicle cannot make that up, so a chest held at the
# neck base's height leaves the wrist out of reach. The third field lowers the target by a named solver margin.
AIM = {
    " Spine_04": ((" Spine1_05",), ("DEF_Spine.005",), None),
    " Spine1_05": ((" L UpperArm_032", " R UpperArm_028"), ("DEF_Arm.L", "DEF_Arm.R"), "front_drop"),
}

LEGS = {  # tag: (TAOM girdle or None, upper, lower, foot), (source root joint, mid joint, end joint, foot bone, pad)
    "FL": ((" L Clavicle_031", " L UpperArm_032", " L Forearm_033", " L Hand_034"),
           ("DEF_Arm.L", "DEF_Forearm.L", "DEF_Finger_c.L", "DEF_Finger_c.L", "DEF_FootPads.Fr.L")),
    "FR": ((" R Clavicle_027", " R UpperArm_028", " R Forearm_029", " R Hand_030"),
           ("DEF_Arm.R", "DEF_Forearm.R", "DEF_Finger_c.R", "DEF_Finger_c.R", "DEF_FootPads.Fr.R")),
    "BL": ((None, " L Thigh_035", " L Calf_036", " L Foot_037"),
           ("DEF_Thigh.002.L", "DEF_Shin.L", "DEF_Toe_b.L", "DEF_Toe_b.L", "DEF_FootPads.Bk.L")),
    "BR": ((None, " R Thigh_038", " R Calf_039", " R Foot_040"),
           ("DEF_Thigh.002.R", "DEF_Shin.R", "DEF_Toe_b.R", "DEF_Toe_b.R", "DEF_FootPads.Bk.R")),
}
STANCE_SPEED = 0.004   # a source pad slower than this (m per frame, world) is planted
MIN_STANCE = 6


# ---------- small maths ----------
def unit(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-12 else v * 0.0


def frame_of(axis, normal):
    """Right-handed orthonormal 3x3 with first column along `axis` and second along `normal` made square to it."""
    x = unit(axis)
    n = unit(normal - x * np.dot(normal, x))
    return np.column_stack([x, n, np.cross(x, n)])


def rot_between(a, b):
    """Smallest rotation that turns direction a onto direction b."""
    a, b = unit(a), unit(b)
    v = np.cross(a, b)
    s, c = np.linalg.norm(v), float(np.dot(a, b))
    if s < 1e-9:
        return np.eye(3)
    k = v / s
    K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
    return np.eye(3) + s * K + (1 - c) * (K @ K)


def mat_to_quat(m):
    t = m[0, 0] + m[1, 1] + m[2, 2]
    if t > 0:
        s = math.sqrt(t + 1.0) * 2
        q = [0.25 * s, (m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s]
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        q = [(m[2, 1] - m[1, 2]) / s, 0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s]
    elif m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        q = [(m[0, 2] - m[2, 0]) / s, (m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s]
    else:
        s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        q = [(m[1, 0] - m[0, 1]) / s, (m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s]
    return np.array(q)


def quat_to_mat(q):
    w, x, y, z = q / np.linalg.norm(q)
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def ortho(m):
    """Nearest rotation to m (the source bones carry scale from stretch constraints)."""
    u, _, vt = np.linalg.svd(m)
    r = u @ vt
    if np.linalg.det(r) < 0:
        u[:, -1] *= -1
        r = u @ vt
    return r


def mean_rot(mats):
    qs = [mat_to_quat(m) for m in mats]
    ref = qs[0]
    acc = np.zeros(4)
    for q in qs:
        acc += q if np.dot(q, ref) >= 0 else -q
    return quat_to_mat(acc)


def scale_rot(r, w):
    """The rotation r taken a fraction w of the way from identity."""
    if w == 1.0:
        return r
    q = mat_to_quat(r)
    if q[0] < 0:
        q = -q
    ang = 2 * math.acos(max(-1.0, min(1.0, q[0])))
    if ang < 1e-9:
        return np.eye(3)
    ax = q[1:] / math.sin(ang / 2)
    h = ang * w / 2
    return quat_to_mat(np.array([math.cos(h), *(ax * math.sin(h))]))


def two_bone(root, end, a, b, normal, side):
    """Mid joint of a 2-bone limb. `side` (+1 or -1) picks which way it bends in the plane square to `normal`.
    Returns (mid, end actually reached, how far the target was out of reach)."""
    d = end - root
    c = float(np.linalg.norm(d))
    lim = (a + b) * 0.9995
    lo = abs(a - b) + 1e-4
    over = max(0.0, c - lim)
    c2 = min(max(c, lo), lim)
    x = unit(d)
    n = unit(normal - x * np.dot(normal, x))
    u = np.cross(n, x)
    cosa = (a * a + c2 * c2 - b * b) / (2 * a * c2)
    cosa = max(-1.0, min(1.0, cosa))
    sina = math.sqrt(1 - cosa * cosa)
    mid = root + a * (cosa * x + side * sina * u)
    return mid, root + x * c2, over


# ---------- the source cycle ----------
class Source:
    def __init__(self, sample, f0=None, period=None):
        self.S = sample
        self.fps = sample["fps"]
        self.rest = sample["rest"]
        self._detect(f0, period)
        self.cache = {}

    def raw_head(self, n, f):
        return np.array(self.S["frames"][str(f)][n][0:3])

    def raw_rot(self, n, f):
        return np.array(self.S["frames"][str(f)][n][6:15]).reshape(3, 3)

    def _detect(self, f0, period):
        S = self.S
        fa, fb = S["f0"], S["f1"]
        runs = {}
        for tag, (_, src) in LEGS.items():
            pad = src[4]
            sp = [np.linalg.norm(self.raw_head(pad, f + 1) - self.raw_head(pad, f)) for f in range(fa, fb)]
            rs, i = [], 0
            while i < len(sp):
                if sp[i] < STANCE_SPEED:
                    j = i
                    while j < len(sp) and sp[j] < STANCE_SPEED:
                        j += 1
                    if j - i >= MIN_STANCE:
                        rs.append((fa + i, fa + j))
                    i = j
                else:
                    i += 1
            runs[tag] = rs
        self.runs = runs
        if period is None:
            gaps = [b[0] - a[0] for rs in runs.values() for a, b in zip(rs, rs[1:])]
            period = int(round(float(np.median(gaps))))
        if f0 is None:
            # start at the first front-left plant that leaves a whole cycle, and whole stances, inside the take
            f0 = next(r[0] for r in runs["FL"] if r[0] > fa and r[0] + period <= fb)
        self.f0, self.T = f0, period
        hip = "DEF_Thigh.002.L"
        self.vel = (self.raw_head(hip, f0 + period) - self.raw_head(hip, f0)) / period
        self.vel[2] = 0.0
        self.stride = float(np.linalg.norm(self.vel) * period)
        # centre the in-place cycle: over one loop the hip midpoint averages its rest position (x and y)
        hips = ("DEF_Thigh.002.L", "DEF_Thigh.002.R")
        mean = np.mean([(self.raw_head(hips[0], f0 + i) + self.raw_head(hips[1], f0 + i)) / 2 - self.vel * i
                        for i in range(period)], axis=0)
        rest_mid = (np.array(self.rest[hips[0]]["head"]) + np.array(self.rest[hips[1]]["head"])) / 2
        self.centre = mean - rest_mid
        self.centre[2] = 0.0
        # per leg: plant and lift phases inside the cycle
        self.plant, self.duty = {}, {}
        for tag, rs in runs.items():
            best = None
            for a, b in rs:
                for shift in (-period, 0, period):
                    a2, b2 = a + shift, b + shift
                    if f0 <= a2 < f0 + period and (b2 - a2) < period:
                        if best is None or (b2 - a2) > best[1] - best[0]:
                            best = (a2, b2)
            if best is None:
                raise ValueError("no stance of leg %s starts inside the cycle %d..%d" % (tag, f0, f0 + period))
            self.plant[tag] = (best[0] - f0) / period
            self.duty[tag] = (best[1] - best[0]) / period

    def _series(self, n, kind):
        key = (n, kind)
        if key not in self.cache:
            out = []
            for i in range(self.T + 1):
                f = self.f0 + i
                if kind == "pos":
                    out.append(self.raw_head(n, f) - self.vel * i - self.centre)
                else:
                    out.append(mat_to_quat(ortho(self.raw_rot(n, f))))
            if kind == "rot":
                for i in range(1, len(out)):
                    if np.dot(out[i], out[i - 1]) < 0:
                        out[i] = -out[i]
            arr = np.array(out)
            # close the loop: spread the seam error over the cycle so frame T equals frame 0
            if kind == "pos":
                err = arr[-1] - arr[0]
                arr = arr - np.outer(np.linspace(0, 1, self.T + 1), err)
            else:
                if np.dot(arr[-1], arr[0]) < 0:
                    arr[-1] = -arr[-1]
                err = arr[-1] - arr[0]
                arr = arr - np.outer(np.linspace(0, 1, self.T + 1), err)
            self.cache[key] = arr
            self.cache[("seam",) + key] = float(np.linalg.norm(err))
        return self.cache[key]

    def pos(self, n, ph):
        a = self._series(n, "pos")
        x = (ph % 1.0) * self.T
        i = int(math.floor(x))
        t = x - i
        return a[i] * (1 - t) + a[i + 1] * t

    def rot(self, n, ph):
        a = self._series(n, "rot")
        x = (ph % 1.0) * self.T
        i = int(math.floor(x))
        t = x - i
        return quat_to_mat(a[i] * (1 - t) + a[i + 1] * t)

    def rest_pos(self, n):
        return np.array(self.rest[n]["head"])

    def mid(self, names, ph):
        return sum(self.pos(n, ph) for n in names) / len(names)

    def rest_mid(self, names):
        return sum(self.rest_pos(n) for n in names) / len(names)

    def rest_rot(self, n):
        return ortho(np.array(self.rest[n]["rot"]).reshape(3, 3))

    def mean_rot(self, n):
        key = ("mean", n)
        if key not in self.cache:
            self.cache[key] = mean_rot([self.rot(n, i / self.T) for i in range(self.T)])
        return self.cache[key]


class Take(Source):
    """A stretch of a source scene in which the animal stands in one place: an idle. Phase 0..1 runs from frame
    fa to frame fb and does not wrap. `ref` picks the frame the head, trunk, ears and tail swing about ("first"
    or "last": the frame where the animal stands neutral), where a gait uses its mean pose. A take in which the
    animal travels (the pack's stop scene walks on a curve) needs its path and its turn taken out first; this
    class does not do that."""

    def __init__(self, sample, fa, fb, ref="first"):
        self.S = sample
        self.fps = sample["fps"]
        self.rest = sample["rest"]
        if not (sample["f0"] <= fa < fb <= sample["f1"]):
            raise ValueError("take %d..%d is outside the sample %d..%d" % (fa, fb, sample["f0"], sample["f1"]))
        self.f0, self.T = fa, fb - fa
        self.ref = 0 if ref == "first" else self.T
        self.cache = {}
        hips = ("DEF_Thigh.002.L", "DEF_Thigh.002.R")
        mid = np.array([(self.raw_head(hips[0], fa + i) + self.raw_head(hips[1], fa + i)) / 2 for i in range(self.T + 1)])
        rest_mid = (np.array(self.rest[hips[0]]["head"]) + np.array(self.rest[hips[1]]["head"])) / 2
        self.offset = mid[self.ref] - rest_mid
        self.offset[2] = 0.0
        self.travel = float(np.linalg.norm(mid[-1, :2] - mid[0, :2]))

    def _series(self, n, kind):
        key = (n, kind)
        if key not in self.cache:
            if kind == "pos":
                arr = np.array([self.raw_head(n, self.f0 + i) - self.offset for i in range(self.T + 1)])
            else:
                out = [mat_to_quat(ortho(self.raw_rot(n, self.f0 + i))) for i in range(self.T + 1)]
                for i in range(1, len(out)):
                    if np.dot(out[i], out[i - 1]) < 0:
                        out[i] = -out[i]
                arr = np.array(out)
            self.cache[key] = arr
        return self.cache[key]

    def _at(self, a, ph):
        x = min(max(ph, 0.0), 1.0) * self.T
        i = min(int(math.floor(x)), self.T - 1)
        t = x - i
        return a[i] * (1 - t) + a[i + 1] * t

    def pos(self, n, ph):
        return self._at(self._series(n, "pos"), ph)

    def rot(self, n, ph):
        return quat_to_mat(self._at(self._series(n, "rot"), ph))

    def mean_rot(self, n):
        return quat_to_mat(self._series(n, "rot")[self.ref])

    def rest_pos(self, n):
        # A foot pad is measured from where it stands at the reference frame, not from the source's rest pose:
        # the animal stands with its feet staggered, and ours must stand on its own rest footprint there, or
        # the feet glide to the stagger each time the engine blends into the clip.
        if n.startswith("DEF_FootPads"):
            return self._series(n, "pos")[self.ref]
        return np.array(self.rest[n]["head"])


def build_take(solver, name, keys, pingpong=False):
    """Keys of a Take. `pingpong` plays it forward and then back, so the last key leads into the first and the
    clip loops; the take should start and end where the animal is still. The planted soles are put on the
    ground first: one pass measures each leg's sole height while its source pad stands still."""
    src, rig = solver.src, solver.rig
    phases = [i / (keys - 1) for i in range(keys)]
    if pingpong:
        phases = phases + phases[-2:0:-1]

    def run():
        mats, soles, overs = [], {t: [] for t in LEGS}, {t: 0.0 for t in LEGS}
        for ph in phases:
            R, P, info = solver.pose(ph, {t: ph for t in LEGS})
            row = []
            for n in rig.names:
                m = np.eye(4)
                m[:3, :3] = R[n]
                m[:3, 3] = P[n]
                row.append(m)
            mats.append(row)
            for t in LEGS:
                soles[t].append(info["sole"][t])
                overs[t] = max(overs[t], info["over"].get(t, 0.0))
        return mats, {t: np.array(v) for t, v in soles.items()}, overs

    def planted(tag):
        pad = LEGS[tag][1][4]
        p = np.array([src.pos(pad, ph) for ph in phases[:keys]])
        still = np.linalg.norm(np.diff(p, axis=0), axis=1) < STANCE_SPEED * src.T / (keys - 1)
        low = p[:, 2] < p[:, 2].min() + 0.02          # a foot held still in the air is not planted
        return [i for i in range(keys - 1) if still[i] and low[i]]

    solver.lift = {}
    _, soles, _ = run()
    down = {t: planted(t) for t in LEGS}
    solver.lift = {t: -float(np.median(soles[t][down[t], 2])) if down[t] else 0.0 for t in LEGS}
    mats, soles, overs = run()
    rep = {}
    for t in LEGS:
        idx = down[t]
        p = soles[t]
        drift = float(np.linalg.norm(p[idx, :2] - np.median(p[idx, :2], axis=0), axis=1).max()) if idx else 0.0
        rep[t] = {"planted_keys": len(idx), "sole_z_planted_min": round(float(p[idx, 2].min()), 4) if idx else None,
                  "sole_z_planted_max": round(float(p[idx, 2].max()), 4) if idx else None,
                  "sole_z_min": round(float(p[:, 2].min()), 4), "planted_drift_m": round(drift, 4),
                  "overreach_m": round(overs[t], 4)}
    pel = np.array([m[0][:3, 3] for m in mats])
    return {
        "clip": name, "frames": len(mats), "take": [src.f0, src.f0 + src.T], "pingpong": pingpong,
        "source_seconds": round(src.T / src.fps * (2 if pingpong else 1), 3), "source_travel_m": round(src.travel, 3),
        "stride_m": 0.0, "loop_displacement": 0.0, "step_points": [],
        "pelvis": {"bob_m": round(float(pel[:, 2].max() - pel[:, 2].min()), 4),
                   "sway_m": round(float(pel[:, 0].max() - pel[:, 0].min()), 4),
                   "fore_aft_m": round(float(pel[:, 1].max() - pel[:, 1].min()), 4),
                   "mean_height_vs_rest_m": round(float(pel[:, 2].mean() - rig.head[rig.names[0]][2]), 4)},
        "legs": rep, "bones": rig.names, "parents": [rig.parent[n] for n in rig.names],
        "mats": [[m.reshape(-1).round(6).tolist() for m in row] for row in [[rig.M0[n] for n in rig.names]] + mats],
    }


# ---------- leg re-timing ----------
def leg_warp(duty, q, ease=0.15):
    """Map a leg's new phase (0 = plant) to its source phase. The stance, [0, duty/q) in the new cycle, plays the
    source stance [0, duty) at a constant rate. The swing starts and ends at that same rate, easing over the
    first and last `ease` of its length to a slower plateau, so the foot's speed has no step at lift or plant.
    Returns (function, new duty)."""
    if abs(q - 1.0) < 1e-9:
        return (lambda x: x % 1.0), duty
    d2 = duty / q
    rs = duty / d2                      # stance rate
    mean_swing = (1 - duty) / (1 - d2)  # average rate the swing needs
    n = 2000
    u = (np.arange(n) + 0.5) / n
    edge = np.minimum(u, 1 - u) / ease
    bump = np.where(edge >= 1, 1.0, edge * edge * (3 - 2 * edge))   # smoothstep up, plateau, smoothstep down
    rm = rs + (mean_swing - rs) / float(bump.mean())
    if rm <= 0.05:
        raise ValueError("q=%.2f leaves the swing no time (duty %.2f)" % (q, duty))
    rate = rs + (rm - rs) * bump
    cum = np.concatenate([[0.0], np.cumsum(rate) / n])   # cum[-1] is the mean swing rate

    def f(x):
        x = x % 1.0
        if x < d2:
            return x * rs
        v = (x - d2) / (1 - d2) * n
        i = min(int(v), n - 1)
        return duty + (cum[i] + (cum[i + 1] - cum[i]) * (v - i)) * (1 - d2)
    return f, d2


# ---------- the target rig ----------
class Rig:
    def __init__(self, dump):
        arm = dump["armatures"]["elephant_skeleton"]
        self.names = [b["name"] for b in arm["bones"]]
        self.parent = {b["name"]: b["parent"] for b in arm["bones"]}
        self.head = {b["name"]: np.array(b["head"]) for b in arm["bones"]}
        self.R0 = {b["name"]: np.array(b["m"])[:3, :3] for b in arm["bones"]}
        self.M0 = {b["name"]: np.array(b["m"]) for b in arm["bones"]}

    def children(self, n):
        return [c for c in self.names if self.parent[c] == n]


class Solver:
    def __init__(self, rig, src, scale=None, drop=0.0, foot_pitch=1.0, sway=1.0, secondary=1.0, front_drop=0.0,
                 stride_scale=1.0, carriage=0.3):
        self.rig, self.src = rig, src
        self.drop, self.foot_pitch, self.sway, self.secondary = drop, foot_pitch, sway, secondary
        self.front_drop = front_drop
        self.carriage = carriage
        r = rig
        # one scale for every position change: leg height of our rig over the source's, front and rear averaged
        kf = (r.head[" L UpperArm_032"][2] - r.head[" L Hand_034"][2]) / (
            src.rest_pos("DEF_Arm.L")[2] - src.rest_pos("DEF_Finger_c.L")[2])
        kr = (r.head[" L Thigh_035"][2] - r.head[" L Foot_037"][2]) / (
            src.rest_pos("DEF_Thigh.002.L")[2] - src.rest_pos("DEF_Toe_b.L")[2])
        self.k = scale if scale else min(kf, kr)
        self.kf, self.kr = kf, kr
        # A real elephant's shoulder joint slides about 0.4 m along the ribcage in a stride; ours is fixed to a
        # short clavicle. So the full source stride needs bent legs here. `stride_scale` shortens the fore-aft
        # travel of every tracked point (and so the stride) until the legs stay close to straight.
        self.K = np.array([self.k, self.k * stride_scale, self.k])
        self.stride_scale = stride_scale
        self.leg = {}
        for tag, (dst, s) in LEGS.items():
            girdle, up, lo, foot = dst
            a = np.linalg.norm(r.head[lo] - r.head[up])
            b = np.linalg.norm(r.head[foot] - r.head[lo])
            n0 = np.cross(r.head[lo] - r.head[up], r.head[foot] - r.head[lo])
            ns = np.cross(src.rest_pos(s[1]) - src.rest_pos(s[0]), src.rest_pos(s[2]) - src.rest_pos(s[1]))
            x0 = unit(r.head[foot] - r.head[up])
            n0u = unit(n0 - x0 * np.dot(n0, x0))
            side = 1.0 if np.dot(r.head[lo] - r.head[up], np.cross(n0u, x0)) > 0 else -1.0
            self.leg[tag] = {
                "a": a, "b": b, "n0": unit(n0), "flip": 1.0 if np.dot(n0, ns) > 0 else -1.0, "side": side,
                "F_up0": frame_of(r.head[lo] - r.head[up], n0), "F_lo0": frame_of(r.head[foot] - r.head[lo], n0),
                # the ground point under the wrist or ankle in the rest pose, fixed to the foot bone
                "sole0": np.array([r.head[foot][0], r.head[foot][1], 0.0]) - r.head[foot],
            }
        hl, hr = src.rest_pos("DEF_Thigh.002.L"), src.rest_pos("DEF_Thigh.002.R")
        self.hip0 = (hl + hr) / 2
        self.lift = {}   # per leg: metres added to the wrist or ankle target so the planted sole sits on z = 0

    def foot_rot(self, tag, lph):
        """World rotation of a leg's foot bone at source phase lph."""
        foot, sfoot = LEGS[tag][0][3], LEGS[tag][1][3]
        D = scale_rot(self.src.rot(sfoot, lph) @ self.src.rest_rot(sfoot).T, self.foot_pitch)
        return D @ self.rig.R0[foot]

    def end_target(self, tag, lph):
        """Where the wrist or ankle of a leg goes at source phase lph. The SOLE is the driven point: it follows
        the source's foot pad, which stands still on the ground through the stance, and the wrist is placed
        from it through the foot bone's rotation. Driving the wrist instead lets the foot's roll move the sole."""
        foot, spad = LEGS[tag][0][3], LEGS[tag][1][4]
        r = self.rig
        sole_rest = r.head[foot] + self.leg[tag]["sole0"]
        sole = sole_rest + self.K * (self.src.pos(spad, lph) - self.src.rest_pos(spad))
        sole[2] += self.lift.get(tag, 0.0)
        return sole - self.foot_rot(tag, lph) @ r.R0[foot].T @ self.leg[tag]["sole0"]

    def pelvis_delta(self, ph):
        s = self.src
        hl, hr = s.pos("DEF_Thigh.002.L", ph), s.pos("DEF_Thigh.002.R", ph)
        up = (s.rot("DEF_Spine", ph) @ s.rest_rot("DEF_Spine").T) @ np.array([0, 0, 1.0])
        x = unit(hl - hr)
        y = unit(np.cross(up, x))
        z = np.cross(x, y)
        D = np.column_stack([x, y, z])   # the rest frame is the identity: the source rig is symmetric
        return scale_rot(D, self.sway), (hl + hr) / 2 - self.hip0

    def pose(self, ph, leg_phase):
        """World (armature-space) rotation and head position of every bone at body phase ph.
        leg_phase[tag] is the SOURCE phase that leg plays."""
        r, s, k = self.rig, self.src, self.K
        R, P = {}, {}
        stored = {}
        info = {"over": {}, "sole": {}, "angle": {}}
        for n in r.names:
            par = r.parent[n]
            if par is None:
                D, dp = self.pelvis_delta(ph)
                dp = dp * k
                dp[0] *= self.sway
                P[n] = r.head[n] + dp - np.array([0, 0, self.drop])
                R[n] = D @ r.R0[n]
                continue
            P[n] = P[par] + R[par] @ r.R0[par].T @ (r.head[n] - r.head[par])
            rest_follow = R[par] @ r.R0[par].T @ r.R0[n]
            if n in stored:
                R[n] = stored.pop(n)
            elif n in REST_MAP:
                sb = REST_MAP[n]
                if n in CARRIAGE:
                    # the source walks with its head lower than its rest pose; keep OUR head carriage and take
                    # the source's motion about its mean, plus a share (`carriage`) of that lowering
                    D = s.rot(sb, ph) @ s.mean_rot(sb).T @ scale_rot(s.mean_rot(sb) @ s.rest_rot(sb).T, self.carriage)
                else:
                    D = s.rot(sb, ph) @ s.rest_rot(sb).T
                R[n] = D @ r.R0[n]
                if n in AIM:
                    # keep that orientation, then turn the bone the least amount that puts its tracked point
                    # where the source's is: the chest and the shoulder girdle must not drift off the legs
                    pts, sb2, margin = AIM[n]
                    p0 = sum(r.head[b] for b in pts) / len(pts)
                    low = self.drop + (getattr(self, margin) if margin else 0.0)
                    want = p0 + k * (s.mid(sb2, ph) - s.rest_mid(sb2)) - np.array([0, 0, low])
                    cur = R[n] @ r.R0[n].T @ (p0 - r.head[n])
                    R[n] = rot_between(cur, want - P[n]) @ R[n]
            elif n in EARS or n in TRUNK or n in TAIL:
                # the swing of the source segment about its own mean pose, measured relative to the source bone
                # it hangs from, then applied on top of OUR anchor bone: the part swings about our rest shape
                if n in EARS:
                    sb, base, anchor = EARS[n], "DEF_Head", " Head_08"
                else:
                    chain, sch = (TRUNK, SRC_TRUNK) if n in TRUNK else (TAIL, SRC_TAIL)
                    sb = sch[int(round(chain.index(n) * (len(sch) - 1) / (len(chain) - 1)))]
                    base, anchor = ("DEF_Head", " Head_08") if n in TRUNK else ("DEF_Spine", " Pelvis_03")
                D = s.rot(sb, ph) @ s.mean_rot(sb).T
                Db = s.rot(base, ph) @ s.mean_rot(base).T
                rel = scale_rot(Db.T @ D, self.secondary * (EAR_SWING if n in EARS else 1.0))
                A = R[anchor] @ r.R0[anchor].T
                R[n] = A @ rel @ r.R0[n]
            else:
                tag = next((t for t, (dst, _) in LEGS.items() if n in dst), None)
                if tag is None:
                    R[n] = rest_follow
                    continue
                girdle, up, lo, foot = LEGS[tag][0]
                sroot, smid, send, sfoot, _ = LEGS[tag][1]
                L = self.leg[tag]
                lph = leg_phase[tag]
                if n == girdle:
                    # turn the clavicle so the shoulder follows the source shoulder (a body channel: phase ph)
                    want = r.head[up] + k * (s.pos(sroot, ph) - s.rest_pos(sroot)) - np.array([0, 0, self.drop + self.front_drop])
                    cur = rest_follow @ r.R0[n].T @ (r.head[up] - r.head[n])
                    end = self.end_target(tag, lph)
                    L_c = np.linalg.norm(cur)
                    d_follow = unit(want - P[n])
                    d_reach = unit(end - P[n])
                    lim = (L["a"] + L["b"]) * 0.995
                    w = 0.0
                    if np.linalg.norm(end - (P[n] + L_c * d_follow)) > lim:
                        lo_w, hi_w = 0.0, 1.0
                        for _ in range(20):
                            w = (lo_w + hi_w) / 2
                            d = unit(d_follow * (1 - w) + d_reach * w)
                            if np.linalg.norm(end - (P[n] + L_c * d)) > lim:
                                lo_w = w
                            else:
                                hi_w = w
                        w = hi_w
                    R[n] = rot_between(cur, unit(d_follow * (1 - w) + d_reach * w)) @ rest_follow
                elif n == up:
                    end = self.end_target(tag, lph)
                    nrm = L["flip"] * np.cross(s.pos(smid, lph) - s.pos(sroot, lph), s.pos(send, lph) - s.pos(smid, lph))
                    if np.linalg.norm(nrm) < 1e-4 or np.dot(unit(nrm), L["n0"]) < 0.2:
                        nrm = (R[par] @ r.R0[par].T) @ L["n0"]
                    mid, end2, over = two_bone(P[n], end, L["a"], L["b"], nrm, L["side"])
                    info["over"][tag] = over
                    c = float(np.linalg.norm(end2 - P[n]))
                    cosm = (L["a"] ** 2 + L["b"] ** 2 - c * c) / (2 * L["a"] * L["b"])
                    info["angle"][tag] = math.degrees(math.acos(max(-1.0, min(1.0, cosm))))
                    R[n] = frame_of(mid - P[n], nrm) @ L["F_up0"].T @ r.R0[n]
                    stored[lo] = frame_of(end2 - mid, nrm) @ L["F_lo0"].T @ r.R0[lo]
                    stored[foot] = self.foot_rot(tag, lph)
                    info["reach_shift"] = max(info.get("reach_shift", 0.0), float(np.linalg.norm(end2 - end)))
                else:
                    R[n] = rest_follow
            if n in (LEGS["FL"][0][3], LEGS["FR"][0][3], LEGS["BL"][0][3], LEGS["BR"][0][3]):
                tag = next(t for t, (dst, _) in LEGS.items() if dst[3] == n)
                info["sole"][tag] = P[n] + R[n] @ r.R0[n].T @ self.leg[tag]["sole0"]
        return R, P, info


def ground_legs(solver):
    """Set solver.lift from one pass over the plain walk: the median height of each planted sole becomes zero."""
    solver.lift = {}
    g = build_gait(solver, "_probe", 1.0, solver.src.T)
    solver.lift = {t: -g["legs"][t]["sole_z_stance_median"] for t in LEGS}
    return solver.lift


def build_gait(solver, name, q, frames, reverse=False, shift=0.0):
    """`shift` moves the loop's start: every plant lands that fraction of a cycle later, so the clip's first
    frame can line up with the footstep sound points an existing AnimationClip already carries."""
    src, rig = solver.src, solver.rig
    warps, duty2 = {}, {}
    for tag in LEGS:
        warps[tag], duty2[tag] = leg_warp(src.duty[tag], q)
    mats, soles, overs = [], {t: [] for t in LEGS}, {t: 0.0 for t in LEGS}
    angles = {t: [] for t in LEGS}
    order = range(frames)
    for i in order:
        ph = (i / frames - shift) % 1.0
        if reverse:
            ph = (1.0 - ph) % 1.0
        lph = {t: src.plant[t] + warps[t]((ph - src.plant[t]) % 1.0) for t in LEGS}
        R, P, info = solver.pose(ph, lph)
        row = []
        for n in rig.names:
            m = np.eye(4)
            m[:3, :3] = R[n]
            m[:3, 3] = P[n]
            row.append(m)
        mats.append(row)
        for t in LEGS:
            soles[t].append(info["sole"][t])
            overs[t] = max(overs[t], info["over"].get(t, 0.0))
            angles[t].append(info["angle"][t])
    stride = src.stride * solver.K[1] * q
    belt = stride / frames            # metres per frame the ground moves back under the body (+Y in this space)
    sgn = -1.0 if reverse else 1.0
    rep = {}
    for t in LEGS:
        p = np.array(soles[t])
        d2 = duty2[t]
        start = ((1.0 - src.plant[t] - d2) % 1.0 if reverse else src.plant[t])
        start = (start + shift) % 1.0
        idx = [i for i in range(frames) if ((i / frames - start) % 1.0) < d2 - 1.0 / frames]
        # slip: how far the sole moves against the ground during its stance (ground speed = belt along +Y)
        step = [p[(i + 1) % frames] - p[i] - sgn * np.array([0, belt, 0]) for i in idx]
        slip_fwd = float(sum(abs(v[1]) for v in step))
        slip_lat = float(sum(abs(v[0]) for v in step))
        zs = p[:, 2]
        rep[t] = {"plant": round(start, 4), "duty": round(d2, 3), "stance_frames": len(idx),
                  "slip_forward_m": round(slip_fwd, 4), "slip_lateral_m": round(slip_lat, 4),
                  "sole_z_stance_min": round(float(min(zs[idx])), 4), "sole_z_stance_max": round(float(max(zs[idx])), 4),
                  "sole_z_stance_median": float(np.median(zs[idx])),
                  "sole_z_min": round(float(zs.min()), 4), "lift_m": round(float(zs.max() - zs.min()), 3),
                  "overreach_m": round(overs[t], 4),
                  # the elbow or knee angle while planted: 180 is a straight leg
                  "stance_angle_min": round(min(angles[t][i] for i in idx), 1),
                  "stance_angle_mean": round(float(np.mean([angles[t][i] for i in idx])), 1),
                  "swing_angle_min": round(min(angles[t]), 1)}
    rest_row = [rig.M0[n] for n in rig.names]
    pel = np.array([m[0][:3, 3] for m in mats])
    return {
        "clip": name, "frames": frames, "q": q, "reverse": reverse,
        "stride_m": round(stride, 4), "loop_displacement": round(stride, 3),
        "step_points": sorted(round(rep[t]["plant"], 3) for t in LEGS),
        "plant_order": sorted(LEGS, key=lambda t: rep[t]["plant"]),
        "pelvis": {"bob_m": round(float(pel[:, 2].max() - pel[:, 2].min()), 4),
                   "sway_m": round(float(pel[:, 0].max() - pel[:, 0].min()), 4),
                   "fore_aft_m": round(float(pel[:, 1].max() - pel[:, 1].min()), 4),
                   "mean_height_vs_rest_m": round(float(pel[:, 2].mean() - rig.head[rig.names[0]][2]), 4)},
        "legs": rep, "bones": rig.names, "parents": [rig.parent[n] for n in rig.names],
        # frame 0 is the rest pose (the Kit stores the root track relative to it); frames 1..N are the loop
        "mats": [[m.reshape(-1).round(6).tolist() for m in row] for row in [rest_row] + mats],
    }


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--pack", required=True)
    ap.add_argument("--rig", required=True)
    ap.add_argument("--out", required=True, help="folder for <clip>.json and report.json")
    ap.add_argument("--take", action="append", default=[], metavar="NAME:F0:F1:KEYS[:pingpong|once[:first|last[:DROP]]]",
                    help="a standing idle: source frames F0..F1 as KEYS keys. pingpong plays it forward then back so "
                         "it loops; first or last names the frame where the animal stands neutral")
    ap.add_argument("--gait", action="append", default=[], metavar="NAME:FRAMES:STRIDE:Q[:reverse|forward[:SHIFT[:DROP]]]",
                    help="STRIDE in metres per loop at model size (the clip's LoopDisplacement); Q is how much of "
                         "it comes from a shorter stance (1 = the source's own timing), the rest from step length. "
                         "DROP overrides --drop for one gait: a shortened stance needs a lower body, or the rear "
                         "legs cannot reach and the foot slips (canter at -0.05: 7 cm short; at 0.0: under 2 cm)")
    ap.add_argument("--f0", type=int)
    ap.add_argument("--period", type=int)
    ap.add_argument("--scale", type=float)
    ap.add_argument("--drop", type=float, default=-0.05, help="lower the pelvis by this many metres (negative raises)")
    ap.add_argument("--front-drop", type=float, default=0.03, help="lower the shoulders by this many metres more")
    ap.add_argument("--carriage", type=float, default=0.3, help="share of the source's head lowering to keep")
    ap.add_argument("--foot-pitch", type=float, default=1.0, help="share of the source foot's rotation to keep")
    ap.add_argument("--sway", type=float, default=1.0)
    ap.add_argument("--secondary", type=float, default=1.0, help="trunk, ear and tail swing scale")
    a = ap.parse_args(argv)
    if not a.gait and not a.take:
        ap.error("give at least one --gait or --take")
    sample = json.load(open(a.pack))
    rig = Rig(json.load(open(a.rig)))
    os.makedirs(a.out, exist_ok=True)
    report = {"source": {}, "gaits": [], "takes": []}
    for t in a.take:
        parts = t.split(":")
        name, fa, fb, keys = parts[0], int(parts[1]), int(parts[2]), int(parts[3])
        take = Take(sample, fa, fb, parts[5] if len(parts) > 5 else "first")
        sol = Solver(rig, take, a.scale, float(parts[6]) if len(parts) > 6 else a.drop, a.foot_pitch, a.sway,
                     a.secondary, a.front_drop, 1.0, a.carriage)
        clip = build_take(sol, name, keys, len(parts) > 4 and parts[4] == "pingpong")
        clip["scale"] = round(sol.k, 4)
        json.dump(clip, open(os.path.join(a.out, name + ".json"), "w"))
        report["takes"].append({k: v for k, v in clip.items() if k not in ("mats", "bones", "parents")})
    if not a.gait:
        json.dump(report, open(os.path.join(a.out, "report.json"), "w"), indent=1)
        return report
    src = Source(sample, a.f0, a.period)
    report["source"] = {"f0": src.f0, "period": src.T, "stride_m": round(src.stride, 4), "plant": src.plant,
                        "duty": src.duty, "runs": src.runs}
    for g in a.gait:
        parts = g.split(":")
        name, frames, stride, q = parts[0], int(parts[1]), float(parts[2]), float(parts[3])
        probe = Solver(rig, src, a.scale)
        drop = float(parts[6]) if len(parts) > 6 else a.drop
        sol = Solver(rig, src, a.scale, drop, a.foot_pitch, a.sway, a.secondary, a.front_drop,
                     stride / (src.stride * probe.k * q), a.carriage)
        ground_legs(sol)
        clip = build_gait(sol, name, q, frames, len(parts) > 4 and parts[4] == "reverse",
                          float(parts[5]) if len(parts) > 5 else 0.0)
        clip["stride_scale"] = round(sol.stride_scale, 4)
        clip["scale"] = round(sol.k, 4)
        json.dump(clip, open(os.path.join(a.out, name + ".json"), "w"))
        report["gaits"].append({k: v for k, v in clip.items() if k not in ("mats", "bones", "parents")})
    json.dump(report, open(os.path.join(a.out, "report.json"), "w"), indent=1)
    return report


if __name__ == "__main__":
    rep = main()
    for g in rep["takes"]:
        print("%-24s keys %-3d source %d..%d (%.1f s, travel %.2f m)  bob %.3f sway %.3f height %+.3f" % (
            g["clip"], g["frames"], g["take"][0], g["take"][1], g["source_seconds"], g["source_travel_m"],
            g["pelvis"]["bob_m"], g["pelvis"]["sway_m"], g["pelvis"]["mean_height_vs_rest_m"]))
        for t, r in g["legs"].items():
            print("   %s planted keys %d  sole z planted %s..%s lowest %.3f  drift %.3f  over %.3f" % (
                t, r["planted_keys"], r["sole_z_planted_min"], r["sole_z_planted_max"], r["sole_z_min"],
                r["planted_drift_m"], r["overreach_m"]))
    if rep["source"]:
        print("source: f0 %d period %d stride %.3f m" % (rep["source"]["f0"], rep["source"]["period"], rep["source"]["stride_m"]))
    for g in rep["gaits"]:
        print("%-24s frames %-3d stride %.3f m (step scale %.2f, q %.2f)  order %s  bob %.3f sway %.3f height %+.3f" % (
            g["clip"], g["frames"], g["stride_m"], g["stride_scale"], g["q"], " ".join(g["plant_order"]),
            g["pelvis"]["bob_m"], g["pelvis"]["sway_m"], g["pelvis"]["mean_height_vs_rest_m"]))
        for t, r in g["legs"].items():
            print("   %s plant %.3f duty %.2f slip fwd %.3f lat %.3f  sole z stance %.3f..%.3f lift %.3f over %.3f  angle stance min %.0f mean %.0f swing min %.0f" % (
                t, r["plant"], r["duty"], r["slip_forward_m"], r["slip_lateral_m"], r["sole_z_stance_min"],
                r["sole_z_stance_max"], r["lift_m"], r["overreach_m"], r["stance_angle_min"],
                r["stance_angle_mean"], r["swing_angle_min"]))
