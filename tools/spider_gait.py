#!/usr/bin/env python3
"""Generate the giant spider's locomotion clips on spider_skeleton itself (#1541).

Pure Python and numpy, no Blender. The June 2026 clips were retargeted from the animator's sp_skeleton, whose
bone axes differ from spider_skeleton's, and their clip records were byte-grafts of elephant records: the walk's
feet covered 2.93 m where its record claimed 3.60 m, the left and right front legs worked on opposite sides of
their rest points, and the run's third leg pair never left the ground (measured 2026-11-22 with
tools/check_gait_stride.py's reader). This tool does not retarget. It places every leg tip on a path and solves
the leg to reach it, so a planted tip cannot slip, the two sides mirror by construction, and the travel per loop
is a number the tool chose and reports.

Model space is the engine's: the spider faces +Y, +X is its right (the _r bones), Z is up, the ground is z = 0.

Gait      an alternating tetrapod: legs L1, R2, L3, R4 step together, and R1, L2, R3, L4 half a loop later,
          each pair a little after the pair in front of it (RIPPLE). A faster gait keeps that order and plants
          each tip for a smaller share of the loop.
Tip path  stance_q: through the stance the tip moves from the front of its sweep to the back at a steady rate,
          which is the ground's speed under a body that covers `stride` metres a loop. The swing carries it
          forward again and joins the stance at both ends with no step in speed. An in-place twin has the same
          timing and lift and no sweep: the engine blends it with the gait by speed, so a slow spider takes
          short steps at the gait's cadence (the vanilla horse's gait_* clips do this for the horse).
Leg       five bones: hip (on the body), then the leg's upper bone, the knee, a short bone and the tip. The
          tip bone's origin is the point on the ground. solve_leg bends the leg at the knee to the target's
          distance (two links: upper bone, and knee to tip as one stiff piece), then turns the whole leg about
          the upper bone's joint: first about the vertical, then in the vertical plane, so the knee stays up.
          A target beyond the leg's length is pulled in and reported.
Records   each report row carries what the clip record needs: Duration (the keys' real length at KEY_RATE),
          LoopDisplacement (the stride the tips cover), pace limits and the record to clone. A gait reaches full
          stride at its upper limit, and the next pace starts a little below that (the vanilla horse's walk:
          1.52 m in 0.8 s, upper limit 1.9). The gallop is the exception: it is the top pace, its upper limit is
          a typed 13.0, above its full-stride speed of 11.25 m/s, and above that speed the tips skate (two
          battles read 10.0 to 10.3 m/s at a charge). A twin's row says `usage: false` and has no limits; the
          idle's row clears the elephant template's eating sound.
          A build fails (exit 1) when a tip target is outside a leg's range or a tip ends off its target: a
          straight leg has no bend plane, and the solver would leave it where it was.
          tools/gen_elephant_anim_clips.ps1 writes the records from these rows; nobody types one.

    python tools/spider_gait.py --dump-rig <spider_correct_geo.tpac> --out <rig.json>
    python tools/spider_gait.py --rig <rig.json> --out <folder>      one <clip>.json per clip, and report.json
tools/elephant_write_masters.py --new turns each <clip>.json into a master. The game plays a clip only after
the Modding Kit has cooked its cache entry (tools/check_gait_stride.py --creature spider).
"""
import argparse
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from elephant_gait_retarget import rot_between, unit  # noqa: E402
from elephant_write_masters import RZ  # noqa: E402  (engine model space <-> the clip JSON's rig space: the writer's)

KEY_RATE = 60.0                        # keys per second of loop time in every clip this tool writes
TIP_TOLERANCE = 1e-4                   # metres: a tip farther than this from its target fails the build
LEG_BASE = {1: 40, 2: 34, 3: 28, 4: 22}            # leg number, front to back: its hip bone's joint number
LEGS = {side.upper() + str(n): ["joint%d_%s" % (base + k, side) for k in range(5)]
        for n, base in LEG_BASE.items() for side in ("l", "r")}
LATE = ("R1", "L2", "R3", "L4")        # these step half a loop after L1, R2, L3, R4
RIPPLE = 0.03                          # of a loop: each leg pair steps this long after the pair in front of it
CENTRE_Y = {1: 0.25, 2: 0.08, 3: -0.08, 4: -0.25}  # a tip's sweep centre from its rest point: front legs reach
PIVOT = np.array([0.0, -0.07, 0.0])    # the point a turn goes around: the middle of the eight rest tips
ABDOMEN, PALPS = "joint13_m", ("joint17_l", "joint17_r")


def _line(stride, seconds, duty, lift, template, travel=(0.0, 1.0), **more):
    return dict(kind="line", stride=stride, seconds=seconds, duty=duty, lift=lift, template=template,
                travel=travel, **more)


def _twin(of):
    """A gait's in-place twin: the gait's own record with no clip usage, as the vanilla horse's gait_walkfast is
    beside horse_walkfast (read from Native animation_clips.tpac, 2026-11-22). A first set cloned elephant_stand_2,
    an idle record: its sound points and 0.8 s blend-out made the editor refuse all four twins."""
    return dict(CLIPS[of], stride=0.0, limits=None, twin_of=of)


# limits: (lower, upper). An upper of None is the full-stride speed, stride / seconds. `after` names the slower
# pace: the lower limit is then 0.9 times that pace's upper one, so the two paces overlap a little.
CLIPS = {
    "spider_stand": dict(kind="still", seconds=4.0, frames=1, template="elephant_stand_for_movement_data"),
    "spider_idle": dict(kind="idle", seconds=4.0, frames=120, template="mumakil_idle_1"),
    "spider_walk": _line(2.0, 0.9, 0.58, 0.30, "mumakil_walk", limits=(0.2, None)),
    "spider_trot": _line(2.3, 0.5, 0.48, 0.34, "mumakil_trot", after="spider_walk", limits=(None, None), dz=-0.03),
    "spider_canter": _line(2.8, 0.4, 0.40, 0.38, "mumakil_canter", after="spider_trot", limits=(None, None), dz=-0.06),
    # a longer gallop stride (3.8 m in a third of a second) drew the front legs out straight: 0.99 of their reach
    "spider_gallop": _line(3.0, 16 / 60.0, 0.36, 0.42, "mumakil_gallop", after="spider_canter", limits=(None, 13.0),
                           dz=-0.12),
    "spider_walk_backwards": _line(1.4, 0.9, 0.60, 0.26, "mumakil_walk_backwards", travel=(0.0, -1.0),
                                   limits="backward"),
    "spider_strafe_left": _line(1.2, 0.8, 0.56, 0.26, "mumakil_strafe", travel=(-1.0, 0.0)),
    "spider_strafe_right": _line(1.2, 0.8, 0.56, 0.26, "mumakil_strafe", travel=(1.0, 0.0)),
    "spider_turn_left": dict(kind="turn", degrees=40.0, sign=1.0, seconds=0.8, duty=0.56, lift=0.26,
                             template="mumakil_turn_left"),
    "spider_turn_right": dict(kind="turn", degrees=40.0, sign=-1.0, seconds=0.8, duty=0.56, lift=0.26,
                              template="mumakil_turn_right"),
}
for _name in ("spider_walk", "spider_trot", "spider_canter", "spider_gallop", "spider_walk_backwards"):
    CLIPS[_name + "_inplace"] = _twin(_name)


# ---------- the one-shot clips (#1550) ----------
# A keyed clip is a list of (time 0..1, pose). A pose names what moves away from the stand pose: dz (m), pitch (deg,
# nose up), roll (deg, right side down), yaw (deg, nose left), sx and sy (m, the body's shift), abdomen and palps
# (deg, raised), curl (0 to 1: each tip goes to its folded point near the body) and legs ({tag: (dx, dy, dz)}, a
# tip's move off its standing point). The abdomen's tip is 2.6 m behind the body's centre and 0.30 m off the
# ground: each degree of nose-up pitch lowers it about 4.5 cm, so a pitched pose raises the abdomen. The first key is the stand pose, so the clip blends in from any gait; a hit,
# the stop and the jump also end on it. Between keys every value eases with a smoothstep. The template is the
# record the clip's record is cloned from: its flags (lock_movement, client_prediction, make_bodyfall_sound) and
# its usages come with it (tools/gen_elephant_anim_clips.ps1).
GROUPS = {"front": ("L1", "R1"), "back": ("L4", "R4"), "left": ("L1", "L2", "L3", "L4"),
          "right": ("R1", "R2", "R3", "R4"), "all": ("L1", "L2", "L3", "L4", "R1", "R2", "R3", "R4")}
CURL_REACH = 1.3                       # metres from the hip to a folded tip: the hind legs cannot fold below 1.12
CURL_RISE = math.radians(30.0)         # a folded tip sits this far above the hip's level, seen from the hip
STILL_POSE = {}


def _legs(**moves):
    """{tag: (dx, dy, dz)} from groups (front, back, left, right, all) or tags; a later name adds to an earlier."""
    out = {}
    for name, move in moves.items():
        for tag in GROUPS.get(name, (name,)):
            out[tag] = tuple(a + b for a, b in zip(out.get(tag, (0.0, 0.0, 0.0)), move))
    return out


def _mirror(keys):
    """The same keys with left and right swapped: a fall to the other side."""
    out = []
    for t, pose in keys:
        p = dict(pose)
        for k in ("roll", "yaw", "sx"):
            if k in p:
                p[k] = -p[k]
        if "legs" in p:
            p["legs"] = {("R" if tag[0] == "L" else "L") + tag[1]: (-d[0], d[1], d[2]) for tag, d in p["legs"].items()}
        out.append((t, p))
    return out


def _hit(sx=0.0, sy=0.0, pitch=0.0, roll=0.0):
    return [(0.0, STILL_POSE), (0.3, dict(sx=sx, sy=sy, pitch=pitch, roll=roll, dz=-0.04, palps=8.0, abdomen=4.0)),
            (1.0, STILL_POSE)]


DEATH = [(0.0, STILL_POSE), (0.2, dict(dz=0.04, pitch=6.0, palps=14.0, abdomen=6.0, curl=0.1)),
         (0.55, dict(dz=-0.12, curl=0.45, abdomen=2.0)),
         (1.0, dict(dz=-0.24, pitch=-3.0, curl=0.85, abdomen=4.0, palps=-12.0))]
FALL_RIGHT = [(0.0, STILL_POSE), (0.2, dict(dz=0.03, roll=-4.0, palps=12.0, curl=0.1)),
              (0.55, dict(dz=-0.08, roll=10.0, sx=0.12, curl=0.4, legs=_legs(right=(0.12, 0.0, 0.0)))),
              (1.0, dict(dz=-0.22, roll=16.0, sx=0.22, curl=0.8, abdomen=3.0, palps=-10.0,
                         legs=_legs(right=(0.15, 0.0, 0.0))))]


def _one_shot(keys, seconds, template):
    return dict(kind="keyed", keys=keys, seconds=seconds, template=template)


def _hold(of, template, seconds=2.0):
    """The last pose of a death, one key long, for the *_continue actions: the June rows replayed the whole fall."""
    return dict(kind="hold", of=of, seconds=seconds, frames=1, template=template)


CLIPS.update({
    "spider_hit_front": _one_shot(_hit(sy=-0.12, pitch=5.0), 0.6, "elephant_hit"),
    "spider_hit_back": _one_shot(_hit(sy=0.12, pitch=-4.0), 0.6, "elephant_hit"),
    "spider_hit_left": _one_shot(_hit(sx=0.12, roll=5.0), 0.6, "elephant_hit"),
    "spider_hit_right": _one_shot(_hit(sx=-0.12, roll=-5.0), 0.6, "elephant_hit"),
    "spider_death": _one_shot(DEATH, 1.5, "warg_death_backward"),
    "spider_death_right": _one_shot(FALL_RIGHT, 1.5, "warg_death_right_side"),
    "spider_death_left": _one_shot(_mirror(FALL_RIGHT), 1.5, "warg_death_left_side"),
    "spider_dead": _hold("spider_death", "warg_death_backward_continue"),
    "spider_dead_right": _hold("spider_death_right", "warg_death_right_side_continue"),
    "spider_dead_left": _hold("spider_death_left", "warg_death_left_side_continue"),
    # the warg's stop length: the vanilla horse's 2.4 s stops are for a heavier beast
    "spider_quick_stop": _one_shot([(0.0, STILL_POSE),
                                    (0.1, dict(pitch=4.0, sy=-0.04, abdomen=4.0, legs=_legs(front=(0.0, 0.12, 0.22)))),
                                    (0.25, dict(pitch=8.0, dz=-0.06, sy=-0.1, palps=10.0, abdomen=9.0, legs=_legs(front=(0.0, 0.25, 0.0)))),
                                    (0.6, dict(pitch=-3.0, dz=-0.03, sy=-0.04, legs=_legs(front=(0.0, 0.25, 0.0)))),
                                    (0.8, dict(legs=_legs(front=(0.0, 0.12, 0.2)))),
                                    (1.0, STILL_POSE)], 1.5, "warg_quick_stop"),
    # in place: the engine lifts the agent; the clip crouches, springs and reaches with the front legs
    "spider_jump_start": _one_shot([(0.0, STILL_POSE),
                                    (0.45, dict(dz=-0.2, pitch=-4.0, abdomen=-5.0)),
                                    (0.75, dict(dz=0.12, pitch=10.0, palps=12.0, abdomen=10.0, legs=_legs(front=(0.0, 0.3, 0.5)))),
                                    (0.9, dict(dz=0.08, pitch=6.0, abdomen=6.0, legs=_legs(front=(0.0, 0.2, 0.3)))),
                                    (1.0, STILL_POSE)], 1.25, "horse_jump_start"),
})


def _smooth(u):
    return u * u * (3.0 - 2.0 * u)


def pose_at(keys, phase):
    """The eased pose at `phase` (0 to 1) of a key list."""
    for (t0, a), (t1, b) in zip(keys, keys[1:]):
        if phase <= t1 or (t1, b) == keys[-1]:
            u = _smooth(min(1.0, max(0.0, (phase - t0) / (t1 - t0))))
            out = {k: (1 - u) * a.get(k, 0.0) + u * b.get(k, 0.0)
                   for k in set(a) | set(b) if k != "legs"}
            la, lb = a.get("legs", {}), b.get("legs", {})
            out["legs"] = {tag: tuple((1 - u) * p + u * q for p, q in zip(la.get(tag, (0.0,) * 3), lb.get(tag, (0.0,) * 3)))
                           for tag in set(la) | set(lb)}
            return out
    return dict(keys[0][1], legs={})


def keyed_pose(spec, phase):
    """A keyed clip's pose at `phase`; a hold's is its death's last."""
    if spec["kind"] == "hold":
        return pose_at(CLIPS[spec["of"]]["keys"], 1.0)
    return pose_at(spec["keys"], phase)


# ---------- small maths (unit and rot_between are the elephant retarget's) ----------
def axis_angle(axis, angle):
    k = unit(np.asarray(axis, dtype=float))
    K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
    return np.eye(3) + math.sin(angle) * K + (1 - math.cos(angle)) * (K @ K)


# ---------- the rig ----------
class Rig:
    """spider_skeleton's rest pose. bones: [{name, parent_idx, rest_frame: 16 floats, column-major local 4x4}],
    parents before children (tools/tpac_skeleton_dump.parse_skeleton_definition)."""

    def __init__(self, bones):
        self.names = [b["name"] for b in bones]
        self.parent = [b["parent_idx"] for b in bones]
        self.idx = {n: i for i, n in enumerate(self.names)}
        self.local = [np.array(b["rest_frame"], dtype=float).reshape(4, 4).T for b in bones]
        self.rest = self.world([m[:3, :3] for m in self.local], np.zeros(3))
        kids = {i: [] for i in range(len(bones))}
        for i, p in enumerate(self.parent):
            if p >= 0:
                kids[p].append(i)
        self._kids = kids

    def world(self, local_rots, root_offset):
        """World 4x4 of every bone from its local rotation; bone offsets are the rest pose's."""
        out = []
        for i, p in enumerate(self.parent):
            m = self.local[i].copy()
            m[:3, :3] = local_rots[i]
            if p < 0:
                m[:3, 3] = m[:3, 3] + root_offset
                out.append(m)
            else:
                out.append(out[p] @ m)
        return out

    def subtree(self, name):
        todo, out = [self.idx[name]], []
        while todo:
            i = todo.pop()
            out.append(i)
            todo += self._kids[i]
        return out

    def pose(self, world_rots, root_offset):
        """World 4x4s for the given world rotations (3x3 per bone)."""
        local = [world_rots[i] if p < 0 else world_rots[p].T @ world_rots[i] for i, p in enumerate(self.parent)]
        return self.world(local, root_offset)


# ---------- the tip's path and the leg ----------
def stance_q(phase, duty):
    """(q, lift): where the tip is in its sweep, +0.5 at the front and -0.5 at the back, and how far up, 0 to 1.
    q falls at a steady rate through the stance (phase 0 to duty). In the swing it returns on a quintic that
    leaves and meets the stance at the stance's own rate with no acceleration, so the tip is well off the ground
    before it leaves the ground's speed (a cubic scuffed it 6 cm at each end of the walk's swing). The lift is a
    sine squared: no speed at either end."""
    phase %= 1.0
    if phase < duty:
        return 0.5 - phase / duty, 0.0
    u = (phase - duty) / (1.0 - duty)
    m = -(1.0 - duty) / duty
    q = -0.5 + m * u + (10 - 10 * m) * u ** 3 + (15 * m - 15) * u ** 4 + (6 - 6 * m) * u ** 5
    return q, math.sin(math.pi * u) ** 2


def solve_leg(joint, knee, tip, target):
    """(aim, bend, short): `bend` turns the knee-to-tip piece about the knee, then `aim` turns the whole leg about
    `joint`, and the tip is on `target`. short is how far the target was beyond the leg's reach (0 in reach)."""
    u, v = joint - knee, tip - knee
    l1, l2 = np.linalg.norm(u), np.linalg.norm(v)
    want = target - joint
    far = np.linalg.norm(want)
    d = min(max(far, abs(l1 - l2) * 1.02 + 1e-6), (l1 + l2) * 0.995)
    was = math.acos(max(-1.0, min(1.0, float(np.dot(u, v)) / (l1 * l2))))
    now = math.acos(max(-1.0, min(1.0, (l1 * l1 + l2 * l2 - d * d) / (2 * l1 * l2))))
    bend = axis_angle(np.cross(u, v), now - was)
    reach = (knee + bend @ v) - joint
    yaw = axis_angle((0.0, 0.0, 1.0), math.atan2(want[1], want[0]) - math.atan2(reach[1], reach[0]))
    aim = rot_between(yaw @ reach, want) @ yaw
    return aim, bend, float(abs(far - d)) if abs(far - d) > 1e-9 else 0.0


def leg_phase(tag, phase):
    return phase - (0.5 if tag in LATE else 0.0) - RIPPLE * (int(tag[1]) - 1)


def tip_target(rig, tag, spec, phase):
    rest_tip = rig.rest[rig.idx[LEGS[tag][4]]][:3, 3]
    centre = rest_tip + np.array([0.0, CENTRE_Y[int(tag[1])], 0.0])
    if spec["kind"] in ("still", "idle"):
        return centre
    q, up = stance_q(leg_phase(tag, phase), spec["duty"])
    if spec["kind"] == "turn":
        sweep = spec["sign"] * math.radians(spec["degrees"]) * spec["duty"]
        out = PIVOT + axis_angle((0.0, 0.0, 1.0), sweep * q) @ (centre - PIVOT)
    else:
        out = centre + np.array([spec["travel"][0], spec["travel"][1], 0.0]) * (spec["stride"] * spec["duty"] * q)
    return out + np.array([0.0, 0.0, spec["lift"] * up])


def body_motion(spec, phase):
    """(rotation about a point over PIVOT, height change, abdomen pitch, left palp pitch, right palp pitch)."""
    w = 2 * math.pi * phase
    if spec["kind"] == "still":
        return np.eye(3), 0.0, 0.0, 0.0, 0.0
    if spec["kind"] == "idle":
        return (np.eye(3), -0.03 * (1 - math.cos(w)) / 2, math.radians(2.5) * math.sin(w),
                math.radians(5.0) * math.sin(2 * w), math.radians(5.0) * math.sin(2 * w + 1.3))
    turn = axis_angle((0.0, 0.0, 1.0), math.radians(1.2) * math.sin(w)) @ axis_angle((0.0, 1.0, 0.0), math.radians(0.8) * math.cos(w))
    return (turn, spec.get("dz", 0.0) - 0.012 * math.cos(2 * w), math.radians(2.0) * math.sin(2 * w + 0.9),
            math.radians(3.0) * math.sin(2 * w), math.radians(3.0) * math.sin(2 * w))


def keyed_motion(pose):
    """(rotation about a point over PIVOT, height change, abdomen pitch, left palp pitch, right palp pitch, shift)."""
    turn = (axis_angle((0.0, 0.0, 1.0), math.radians(pose.get("yaw", 0.0)))
            @ axis_angle((1.0, 0.0, 0.0), math.radians(pose.get("pitch", 0.0)))
            @ axis_angle((0.0, 1.0, 0.0), math.radians(pose.get("roll", 0.0))))
    palps = math.radians(pose.get("palps", 0.0))
    # about the side axis a positive angle lowers whatever is behind the bone: a raised abdomen is the negative one
    return (turn, pose.get("dz", 0.0), -math.radians(pose.get("abdomen", 0.0)), palps, palps,
            np.array([pose.get("sx", 0.0), pose.get("sy", 0.0), 0.0]))


def keyed_target(rig, tag, pose, body):
    """Where a keyed clip puts a tip: its standing point moved by the pose's leg move, then drawn toward its folded
    point by the pose's curl. The folded point rides with the body (`body` maps a rest point to its posed place)."""
    hip = rig.rest[rig.idx[LEGS[tag][1]]][:3, 3]
    rest_tip = rig.rest[rig.idx[LEGS[tag][4]]][:3, 3]
    ground = rest_tip + np.array([0.0, CENTRE_Y[int(tag[1])], 0.0]) + np.array(pose["legs"].get(tag, (0.0, 0.0, 0.0)))
    out = unit(np.array([rest_tip[0] - hip[0], rest_tip[1] - hip[1], 0.0]))
    folded = body(hip + CURL_REACH * (math.cos(CURL_RISE) * out + np.array([0.0, 0.0, math.sin(CURL_RISE)])))
    curl = pose.get("curl", 0.0)
    return (1.0 - curl) * ground + curl * folded


def frame_pose(rig, spec, phase):
    """(world 4x4 per bone, worst tip error, worst shortfall, worst reach) of one frame."""
    keyed = spec["kind"] in ("keyed", "hold")
    pose = keyed_pose(spec, phase) if keyed else None
    if keyed:
        turn, dz, abdomen, palp_l, palp_r, shift = keyed_motion(pose)
    else:
        (turn, dz, abdomen, palp_l, palp_r), shift = body_motion(spec, phase), np.zeros(3)
    rots = [turn @ m[:3, :3] for m in rig.rest]
    side = turn @ np.array([1.0, 0.0, 0.0])
    for name, angle in ((ABDOMEN, abdomen), (PALPS[0], palp_l), (PALPS[1], palp_r)):
        extra = axis_angle(side, angle)
        for i in rig.subtree(name):
            rots[i] = extra @ rots[i]
    root = rig.rest[0][:3, 3]
    centre = np.array([PIVOT[0], PIVOT[1], root[2]])
    offset = centre + turn @ (root - centre) - root + np.array([0.0, 0.0, dz]) + shift

    def body(point):
        return centre + turn @ (point - centre) + np.array([0.0, 0.0, dz]) + shift

    world = rig.pose(rots, offset)
    short = reach = 0.0
    targets, missed = {}, set()
    for tag, leg in LEGS.items():
        i41, i42, i43, i44 = (rig.idx[b] for b in leg[1:])
        joint, knee, tip = world[i41][:3, 3], world[i42][:3, 3], world[i44][:3, 3]
        targets[tag] = keyed_target(rig, tag, pose, body) if keyed else tip_target(rig, tag, spec, phase)
        aim, bend, s = solve_leg(joint, knee, tip, targets[tag])
        rots[i41] = aim @ rots[i41]
        for i in (i42, i43, i44):
            rots[i] = aim @ bend @ rots[i]
        short = max(short, s)
        if s:
            missed.add(tag)
        reach = max(reach, float(np.linalg.norm(targets[tag] - joint) / (np.linalg.norm(knee - joint) + np.linalg.norm(tip - knee))))
    world = rig.pose(rots, offset)
    error = max((float(np.linalg.norm(world[rig.idx[LEGS[tag][4]]][:3, 3] - targets[tag])) for tag in LEGS if tag not in missed),
                default=0.0)      # a leg whose target was out of reach is reported as short, the others still count
    return world, error, short, reach


# ---------- a clip and its record row ----------
def limits_of(name):
    """(lower, upper) pace limits of a clip's record, or None when its record keeps its template's."""
    spec = CLIPS[name]
    lim = spec.get("limits")
    if lim is None:
        return None
    full = spec["stride"] / spec["seconds"]
    if lim == "backward":
        return [round(-full, 2), -0.2]
    lower = lim[0] if lim[0] is not None else round(0.9 * limits_of(spec["after"])[1], 2)
    return [lower, lim[1] if lim[1] is not None else round(full, 2)]


def build_clip(rig, name, spec):
    """(the clip JSON tools/elephant_write_masters.py reads, the record row)."""
    one_shot = spec["kind"] == "keyed"     # its last key is its end, not the start of the next loop
    n = spec.get("frames") or int(round(spec["seconds"] * KEY_RATE)) + (1 if one_shot else 0)
    mats, error, short, reach = [[(RZ @ m).reshape(16).tolist() for m in rig.rest]], 0.0, 0.0, 0.0
    for f in range(n):
        world, e, s, r = frame_pose(rig, spec, f / (n - 1) if one_shot else f / n)
        mats.append([(RZ @ m).reshape(16).tolist() for m in world])
        error, short, reach = max(error, e), max(short, s), max(reach, r)
    stride = float(spec.get("stride", 0.0))
    twin = spec.get("twin_of")
    limits = limits_of(name) if spec["kind"] == "line" else None     # a twin's record has no usage, so no limits
    if spec["kind"] == "turn":       # the engine's turn rate is in radians a second, so in one loop the body turns
                                     # Duration x this value: the clip's own angle (docs/features/elephant.md)
        loop = round(math.radians(spec["degrees"]) / spec["seconds"], 3)
    else:
        loop = stride
    plants = sorted(p % 1.0 for p in (0.0, RIPPLE, 0.5, 0.5 + RIPPLE)) if spec["kind"] in ("line", "turn") else [-1.0] * 4
    loops = not one_shot       # the master writer closes a loop with one more key that repeats frame 0
    clip = {"clip": name, "bones": rig.names, "parents": [rig.names[p] if p >= 0 else None for p in rig.parent],
            "frames": n, "mats": mats, "stride_m": stride, "loop": loops}
    row = {"clip": name, "frames": n, "loop": loops, "keys": n + (1 if loops else 0), "duration": round(n / KEY_RATE if spec["kind"] in ("line", "turn") else spec["seconds"], 4),
           "loop_displacement": loop, "limits": limits, "step_points": [round(p, 3) for p in plants],
           "template": spec["template"], "kind": spec["kind"], "twin_of": twin,
           "usage": None if spec["kind"] in ("keyed", "hold") else not twin,     # None: the template's usages stay
           "sound_code": "" if spec["kind"] == "idle" else None,
           "tip_error_m": error, "short_m": short, "reach": round(reach, 3)}
    return clip, row


def dump_rig(tpac, skeleton="spider_skeleton"):
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import tpac_skeleton_dump as tsd
    _, seg, _ = tsd.find_skeleton(tpac, skeleton)
    if seg is None:
        raise SystemExit("no skeleton %s in %s" % (skeleton, tpac))
    bones = tsd.parse_skeleton_definition(tsd.read_segment_data(tpac, seg))["bones"]
    return {"skeleton": skeleton, "bones": [{"name": b["name"], "parent_idx": b["parent_idx"],
                                             "rest_frame": [float(x) for x in b["rest_frame"]]} for b in bones]}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dump-rig", metavar="TPAC", help="write the rest skeleton in this package to --out and stop")
    ap.add_argument("--rig", help="the rig JSON --dump-rig wrote")
    ap.add_argument("--out", required=True, help="a folder for the clips, or the rig JSON's path with --dump-rig")
    a = ap.parse_args(argv)
    if a.dump_rig:
        with open(a.out, "w") as fh:
            json.dump(dump_rig(a.dump_rig), fh, indent=0)
        print("wrote", a.out)
        return 0
    if not a.rig:
        ap.error("pass --rig, or --dump-rig")
    with open(a.rig) as fh:
        rig = Rig(json.load(fh)["bones"])
    os.makedirs(a.out, exist_ok=True)
    rows, bad = [], 0
    for name, spec in CLIPS.items():       # always all of them: report.json is what the record writer plans from
        clip, row = build_clip(rig, name, spec)
        with open(os.path.join(a.out, name + ".json"), "w") as fh:
            json.dump(clip, fh)
        rows.append(row)
        miss = row["tip_error_m"] > TIP_TOLERANCE
        bad += bool(row["short_m"]) or miss
        print("%-24s %3d keys  %.3f s  travel %.3f  limits %-14s reach %.2f%s%s" % (
            name, row["frames"], row["duration"], row["loop_displacement"], row["limits"], row["reach"],
            "  A TARGET IS %.3f m OUTSIDE THE LEG'S RANGE" % row["short_m"] if row["short_m"] else "",
            "  A TIP MISSES ITS TARGET BY %.4f m" % row["tip_error_m"] if miss else ""))
    with open(os.path.join(a.out, "report.json"), "w") as fh:
        json.dump({"gaits": rows}, fh, indent=1)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
