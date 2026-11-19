#!/usr/bin/env python3
"""Gate: a quadruped gait clip's feet must cover the ground its AnimationClip claims (#1474).

The horse movement node plays a gait at (agent speed / agent scale) / LoopDisplacement loops per second
(docs/reviews/lessons/animation-skeleton.md, "Write a quad clip's LoopDisplacement at model size"). So the
master's planted feet must move back LoopDisplacement metres per loop, at a steady speed. When they cover less,
the body outruns the feet: on 2026-11-18 the elephant's canter claimed 3.6 m and stepped about 1.8 m, and both
the war elephant and the mumak skated. A tool that rewrites a master, a Kit reimport from an old FBX, or a hand
edit of LoopDisplacement can each bring that back without any error, so this reads the installed files:

  <clip>_anm.tpac   LoopDisplacement: the float after the quad_movement usage name and four zero bytes
  <clip>_geo.tpac   the SkeletalAnimation master's keys, by tools/elephant_write_masters.parse_anim
  the skeleton      rest frames, by tools/tpac_skeleton_dump.parse_skeleton_definition

and measures each foot's SOLE (the ground point under the wrist or ankle in the rest pose, fixed to the foot
bone) through one loop. A sole is planted while it is within CONTACT of the ground. Per clip:
  stride   median planted speed x frames in the loop, against LoopDisplacement (default tolerance 5%)
  slip     per foot, how far it moves against a ground running at LoopDisplacement (default limit 8 cm a stance)
  cache    the game does not play the master: it plays the keys the Modding Kit cooked into the clip's
           RuntimeDataCache entry (15 KB for the 61-frame walk, 11 KB for a 38-frame clip, read 2026-11-19). An
           entry older than its master holds the OLD keys, whatever the master says now; a clip with no entry
           cannot play. Either one fails a clip that an action set binds, and is a note on a clip nothing binds
           yet. The fix for both is one Kit start with the Armory loaded, never a byte patch.

    python tools/check_gait_stride.py [--creature elephant] [--tolerance 0.05] [--max-slip 0.08]
Exit 0 pass, 1 a clip fails, 2 the install or a file is missing (a laptop without the Armory: an environment gap).
"""
import argparse
import os
import re
import struct
import sys
import time
import uuid

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tpac_clone_metamesh as tcm  # noqa: E402
import tpac_skeleton_dump as tsd  # noqa: E402
from _gamedir import game_dir  # noqa: E402
from elephant_gait_retarget import quat_to_mat  # noqa: E402
from elephant_write_masters import SKELETAL_ANIMATION_TYPE, parse_anim  # noqa: E402

CONTACT = 0.03
CREATURES = {
    "elephant": {
        "module": "LOTRLOME_Armory", "clips": "Assets/creature/elephant/animations",
        "skeleton": ("Assets/creature/elephant/mesh/adod_elephant_geo.tpac", "elephant_skeleton"),
        "feet": {"FL": " L Hand_034", "FR": " R Hand_030", "BL": " L Foot_037", "BR": " R Foot_040"},
        "gaits": ["elephant_walk", "elephant_trot", "elephant_canter", "elephant_gallop", "elephant_walk_backwards",
                  "elephant_turn_left", "elephant_turn_right"]
                 + [p + g for p in ("war_elephant_", "mumakil_") for g in (
                     "walk", "trot", "canter", "gallop", "walk_backwards", "turn_left", "turn_right")],
        "action_sets": "ModuleData/action_sets.xml",
    },
}


def loop_displacement(anm_bytes):
    i = anm_bytes.find(b"quad_movement")
    if i < 0:
        return None
    return struct.unpack_from("<f", anm_bytes, i + 13 + 4)[0]


def measure(soles, claim):
    """soles: {foot: array [frames, 3]} in model space (z up), one loop, in place. claim: metres per loop.
    The feet travel along Y; the sign of the travel is taken from the data, so a backward walk measures the same."""
    n = len(next(iter(soles.values())))
    feet, speeds = {}, []
    for tag, p in soles.items():
        p = np.asarray(p, dtype=float)
        nxt = np.roll(p, -1, axis=0)
        planted = (p[:, 2] <= CONTACT) & (nxt[:, 2] <= CONTACT)
        step = (nxt - p)[planted]
        feet[tag] = {"duty": float(planted.mean()), "step": step}
        if len(step):
            speeds.append(float(np.median(step[:, 1])))
    if not speeds:
        return {"stride": 0.0, "slip": {t: float("inf") for t in soles}, "duty": {t: 0.0 for t in soles}, "frames": n}
    direction = 1.0 if np.median(speeds) >= 0 else -1.0
    belt = direction * abs(claim) / n
    return {
        "stride": abs(float(np.median(speeds))) * n,
        "slip": {t: float(np.abs(f["step"][:, 1] - belt).sum() + np.abs(f["step"][:, 0]).sum()) for t, f in feet.items()},
        "duty": {t: f["duty"] for t, f in feet.items()},
        "frames": n,
    }


def judge(m, claim, tolerance, max_slip):
    out = []
    if claim is None or claim <= 0:
        return ["the clip carries no quad_movement travel"]
    if abs(m["stride"] - claim) > tolerance * claim:
        out.append("feet cover %.2f m a loop, the clip claims %.2f m (%+.0f%%)" % (
            m["stride"], claim, 100 * (m["stride"] - claim) / claim))
    for t, s in m["slip"].items():
        if m["duty"][t] < 0.2:
            out.append("%s is planted for only %.0f%% of the loop" % (t, 100 * m["duty"][t]))
        elif s > max_slip:
            out.append("%s slips %.2f m in a stance" % (t, s))
    return out


def bound_clips(action_sets_text):
    """Every clip name an action set binds."""
    return set(re.findall(r'animation="([^"]+)"', action_sets_text))


def cache_state(module_root, anm_path, geo_path):
    """None when the clip's RuntimeDataCache entry is at least as new as its master, else why it is not."""
    with open(anm_path, "rb") as fh:
        guid = str(uuid.UUID(bytes_le=fh.read(24)[8:24]))
    entry = os.path.join(module_root, "RuntimeDataCache", guid + ".rdc")
    if not os.path.exists(entry):
        return "it has no cache entry"
    if os.path.getmtime(entry) < os.path.getmtime(geo_path):
        return "its cache entry is older than its master (cooked %s, master written %s)" % (
            time.strftime("%Y-%m-%d", time.localtime(os.path.getmtime(entry))),
            time.strftime("%Y-%m-%d", time.localtime(os.path.getmtime(geo_path))))
    return None


def rest_world(bones):
    """Per bone: (parent index, local 4x4 column-major, world 4x4)."""
    local = [np.array(b["rest_frame"], dtype=float).reshape(4, 4).T for b in bones]
    world = []
    for i, b in enumerate(bones):
        world.append(world[b["parent_idx"]] @ local[i] if b["parent_idx"] >= 0 else local[i])
    return local, world


def soles_of(anim, bones, feet):
    local, rest = rest_world(bones)
    names = [b["name"] for b in bones]
    idx = {tag: names.index(bone) for tag, bone in feet.items()}
    n = len(anim["rot"][0]["t"])
    out = {tag: np.zeros((n, 3)) for tag in feet}
    for f in range(n):
        world = []
        for i, b in enumerate(bones):
            m = local[i].copy()
            w, x, y, z = anim["rot"][i]["v"][f]
            m[:3, :3] = quat_to_mat(np.array([w, x, y, z], dtype=float))
            if b["parent_idx"] < 0:
                m[:3, 3] += anim["root_pos"]["v"][f][:3]
                world.append(m)
            else:
                world.append(world[b["parent_idx"]] @ m)
        for tag, i in idx.items():
            under = np.array([0.0, 0.0, -rest[i][2, 3]])           # from the joint straight down to the ground at rest
            out[tag][f] = world[i][:3, 3] + world[i][:3, :3] @ (rest[i][:3, :3].T @ under)
    return out


def load_anim(path):
    pkg = tcm.parse(open(path, "rb").read())
    item = [it for it in pkg.items if it.type_guid == SKELETAL_ANIMATION_TYPE][0]
    return parse_anim(tcm.segment_payload(item, item.segments[0]))


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--creature", default="elephant", choices=sorted(CREATURES))
    ap.add_argument("--tolerance", type=float, default=0.05)
    ap.add_argument("--max-slip", type=float, default=0.08)
    ap.add_argument("--game", default=None)
    a = ap.parse_args(argv)
    c = CREATURES[a.creature]
    root = os.path.join(a.game or game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"), "Modules", c["module"])
    skel_path = os.path.join(root, c["skeleton"][0])
    if not os.path.exists(skel_path):
        print("cannot run: %s not found" % skel_path)
        return 2
    _, seg, _ = tsd.find_skeleton(skel_path, c["skeleton"][1])
    if seg is None:
        print("cannot run: no skeleton %s in %s" % (c["skeleton"][1], skel_path))
        return 2
    bones = tsd.parse_skeleton_definition(tsd.read_segment_data(skel_path, seg))["bones"]
    sets_path = os.path.join(root, c["action_sets"])
    if not os.path.exists(sets_path):
        print("cannot run: %s not found" % sets_path)
        return 2
    bound = bound_clips(open(sets_path, encoding="utf-8", errors="replace").read())
    bad = notes = 0
    for clip in c["gaits"]:
        anm = os.path.join(root, c["clips"], clip + "_anm.tpac")
        geo = os.path.join(root, c["clips"], clip + "_geo.tpac")
        if not (os.path.exists(anm) and os.path.exists(geo)):
            if clip in bound:
                bad += 1
                print("FAIL %-28s bound   an action set binds it, and its clip record or its master is missing" % clip)
            else:
                notes += 1
                print("--   %-28s unbound not installed (no clip record or no master)" % clip)
            continue
        claim = loop_displacement(open(anm, "rb").read())
        m = measure(soles_of(load_anim(geo), bones, c["feet"]), claim or 0.0)
        why = judge(m, claim, a.tolerance, a.max_slip)
        stale, note = cache_state(root, anm, geo), ""
        if stale and clip in bound:
            why.append("an action set binds it and %s: the game does not play these keys" % stale)
        elif stale:
            notes += 1
            note = "\n       note: no action set binds it yet, and %s" % stale
        bad += bool(why)
        print("%-4s %-28s %s claims %.2f m, feet cover %.2f m in %d frames, worst slip %.3f m%s%s" % (
            "FAIL" if why else "ok", clip, "bound  " if clip in bound else "unbound", claim or 0.0, m["stride"],
            m["frames"], max(m["slip"].values()), "".join("\n       " + w for w in why), note))
    print("%s: %d of %d gait clips fail" % (a.creature, bad, len(c["gaits"])) if bad else
          "%s: all %d gait clips cover the ground they claim%s" % (
              a.creature, len(c["gaits"]), "; %d wait for a Kit start before an action set can bind them" % notes if notes else ""))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
