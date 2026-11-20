#!/usr/bin/env python3
"""Gate: a quadruped gait clip's feet must cover the ground its AnimationClip claims (#1474).

The horse movement node (rglAnim_node_horse_movement; TaleWorlds.Native.dll v1.5.7: tick 0x74E9C0, loop time
0x749B10, pace choice 0x74E890, standing-pace strafe 0x749C20, read 2026-11-19) takes a pace's loop time from
the clip records: the Duration of the pace's direction="none" clip at PaceSwitchLimitMin, the Duration of its
front clip at PaceSwitchLimitMax, a straight line between. It then weights the front clip by
speed / (k x LoopDisplacement / loop time), capped at 1, against the "none" clip at the same phase (k is 1 when
the node's fourth input is 1; its writer is not read). At weight 1 the
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
           yet. The fix for both is one Kit start with the Armory loaded, never a byte patch. The Kit cooks an
           entry again when the clip RECORD changes too (2026-11-19: a limits-only rewrite at 11:17, cooked at
           13:27), so the entry is compared with the newer of the record and the master.
Per action set named in "sets" (record_findings), the record values the same node reads. A named set, a row
below or its record that is not there is a finding, so the gate cannot pass on what it could not read:
  backward the clip on the backward walk action needs a PaceSwitchLimitMax below 0 (the vanilla horse: -1.25 to
           -0.1). The node leaves the backward pace only above that value, so with 2.5 a beast that stepped back
           once played the reversed walk while it walked forward (Mike's report, 2026-11-19).
  strafe   at the standing pace the node plays a strafe clip at LoopDisplacement / (Duration x sideways speed)
           seconds a loop, held between 0.7 and 1.3 times its Duration. A record that says 1.0 s for 76 keys
           (2.53 s at KEY_RATE) ran the mumak's walk cycle up to 3.6 times too fast each time it started to
           move. A Duration far above the keys' length plays the cycle as much too slowly.
  faster   the node steps down a pace only while the speed is below the CURRENT pace's PaceSwitchLimitMin. A
           faster pace whose minimum is not above the walk's keeps the beast in it down to that speed: the trot
           records cloned the walk's 0.2 to 2.5, so after one charge a beast trotted at walking speed
           (review, 2026-11-20). Only the walk is judged; ADOD's gallop minimum (4.3) is under its canter's
           (4.8), which skips the canter on the way down and waits for the fast-pace rebuild.
Per clip the named sets bind (stand_findings):
  stand    the pose on the stand_for_movement_data action is what a beast stands in. Its lowest sole must be
           within CONTACT of the ground. ADOD's master rode 0.13 m above its own rest frame on every key but the
           first, so the mumak stood 0.4 m in the air after each stop (tools/elephant_write_masters.py --root-z).
  cache    a clip whose master is in the skeleton's package (the ADOD clips: one --root-z write moves all of
           them) needs an entry no older than that package, or the game still plays the keys from before.

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
PREFIXES = ("war_elephant_", "mumakil_")   # one beast's clips start with one of these
CREATURES = {
    "elephant": {
        "module": "LOTRLOME_Armory", "clips": "Assets/creature/elephant/animations",
        "skeleton": ("Assets/creature/elephant/mesh/adod_elephant_geo.tpac", "elephant_skeleton"),
        "feet": {"FL": " L Hand_034", "FR": " R Hand_030", "BL": " L Foot_037", "BR": " R Foot_040"},
        "gaits": ["elephant_walk", "elephant_trot", "elephant_canter", "elephant_gallop", "elephant_walk_backwards",
                  "elephant_turn_left", "elephant_turn_right"]
                 + [p + g for p in PREFIXES for g in (
                     "walk", "trot", "canter", "gallop", "walk_backwards", "turn_left", "turn_right", "strafe")],
        "action_sets": "ModuleData/action_sets.xml",
        "sets": ("as_elephant", "as_mumakil"),
        "pace_actions": {"backward": "act_elephant_backward_walk",
                         "strafe": ("act_elephant_strafe_right", "act_elephant_strafe_left"),
                         "walk": "act_elephant_forward_walk",
                         "faster": ("act_elephant_forward_trot", "act_elephant_forward_canter",
                                    "act_elephant_forward_gallop_right_foot", "act_elephant_forward_gallop_left_foot")},
        "stand_action": "act_elephant_stand_for_movement_data",
    },
}
KEY_RATE = 30.0        # keys per second in a master: the pack's frame rate
MIN_DURATION = 0.75    # of a strafe clip's real length; the vanilla horse records run from 0.8 to 1.19


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


def clip_record(anm_bytes):
    """Duration, key count and quad_movement usage of a single-clip package (the name is the sized string at 0x48)."""
    end = 0x4C + struct.unpack_from("<i", anm_bytes, 0x48)[0]
    duration, first, last = struct.unpack_from("<3f", anm_bytes, end + 12)
    rec = {"duration": duration, "first": first, "keys": last - first, "animation": bytes(anm_bytes[end + 40:end + 56]),
           "loop": None, "limit_min": None, "limit_max": None}
    i = anm_bytes.find(b"quad_movement")
    if i >= 0:
        rec["loop"], rec["limit_min"], rec["limit_max"] = struct.unpack_from("<3f", anm_bytes, i + 13 + 4)
    return rec


def set_rows(action_sets_text):
    """{action set id: {action type: clip name}} of every set that has a body (a self-closing set has no rows)."""
    out = {}
    for head, body in re.findall(r"<action_set\b([^>]*)(?<!/)>(.*?)</action_set>", action_sets_text, re.S):
        set_id = re.search(r'\bid="([^"]+)"', head)
        rows = {}
        for tag in re.findall(r"<action\b[^>]*>", body):
            kind, clip = re.search(r'\btype="([^"]+)"', tag), re.search(r'\banimation="([^"]+)"', tag)
            if kind and clip:
                rows[kind.group(1)] = clip.group(1)
        if set_id:
            out[set_id.group(1)] = rows
    return out


def record_findings(rows, records, c, key_rate=KEY_RATE):
    """(findings, rows read): why the engine's movement node misplays a pace row of a set named in c["sets"].
    rows: set_rows(); records: {clip: clip_record()} of the clip folder. A named set, a pace row or its record
    that is not there is a finding."""
    acts, out, read = c["pace_actions"], [], 0
    for set_id in c["sets"]:
        if set_id not in rows:
            out.append("no action set %s with rows" % set_id)
            continue
        got, gone = {}, set()
        for act in (acts["backward"], *acts["strafe"], acts["walk"], *acts["faster"], c["stand_action"]):
            clip = rows[set_id].get(act)
            if clip is None:
                out.append("%s has no %s row" % (set_id, act))
            elif clip not in records:
                gone.add(clip)
                out.append("%s binds %s to %s, which has no clip record in the clip folder" % (set_id, act, clip))
            else:
                got[act] = (clip, records[clip])
        read += len(got)
        for act, clip in sorted(rows[set_id].items()):   # every other row: once per clip with no record
            if clip not in records and clip not in gone:
                gone.add(clip)
                out.append("%s binds %s to %s, which has no clip record in the clip folder" % (set_id, act, clip))
        for act in (acts["backward"], acts["walk"], *acts["faster"]):
            if act in got and got[act][1]["limit_min"] is None:
                out.append("%s binds %s to %s, which has no quad_movement usage: the engine has no pace limits for it" % (
                    set_id, act, got.pop(act)[0]))
        if acts["backward"] in got:
            clip, rec = got[acts["backward"]]
            if rec["limit_max"] >= 0:
                out.append("%s binds %s to %s, whose PaceSwitchLimitMax is %.2f: the engine leaves the backward pace "
                           "only above that speed, so after one step back the beast plays this clip while it walks "
                           "forward" % (set_id, acts["backward"], clip, rec["limit_max"]))
        for act in acts["strafe"]:
            if act not in got:
                continue
            clip, rec = got[act]
            real = rec["keys"] / key_rate
            if not MIN_DURATION * real <= rec["duration"] <= real / MIN_DURATION:
                out.append("%s binds %s to %s, whose Duration is %.2f s for %.2f s of keys: at the standing pace the "
                           "engine plays one loop in 0.7 to 1.3 times the Duration" % (set_id, act, clip, rec["duration"], real))
        for act in acts["faster"]:
            if act in got and acts["walk"] in got and got[act][1]["limit_min"] <= got[acts["walk"]][1]["limit_min"]:
                out.append("%s binds %s to %s, whose PaceSwitchLimitMin is %.2f, not above the walk's %.2f: the engine "
                           "steps down a pace only below that speed, so after one burst the beast stays in this "
                           "pace and does not walk" % (set_id, act, got[act][0], got[act][1]["limit_min"],
                                                       got[acts["walk"]][1]["limit_min"]))
    return out, read


def stand_finding(soles):
    """Why a standing pose is wrong, or None. soles: {foot: [the pose's keys, 3]}, model space, z up."""
    low = min(float(np.median(p[:, 2])) for p in soles.values())
    if abs(low) > CONTACT:
        return "its lowest sole stands %+.3f m from the ground" % low
    return None


def stand_findings(rows, records, c, module_root, bones):
    """(findings, cache entries read, poses measured) for the clips the sets named in c["sets"] bind: a clip whose
    master is in the skeleton's package needs a cache entry no older than it, and the pose on the stand action
    must have its lowest sole on the ground."""
    out, cached, poses = [], 0, 0
    folder = os.path.join(module_root, c["clips"])
    skel = os.path.join(module_root, c["skeleton"][0])
    with open(skel, "rb") as fh:
        skel_raw = fh.read()
    named = [rows[s] for s in c["sets"] if s in rows]
    for clip in sorted({v for acts in named for v in acts.values()} & set(records)):
        if records[clip]["animation"] not in skel_raw:
            continue
        cached += 1
        stale = cache_state(module_root, os.path.join(folder, clip + "_anm.tpac"), skel)
        if stale:
            out.append("%s plays keys of %s, and %s: the game plays the old keys" % (clip, os.path.basename(skel), stale))
    masters = [skel] + sorted(os.path.join(folder, f) for f in os.listdir(folder) if f.endswith("_geo.tpac"))
    for clip in sorted({acts.get(c["stand_action"]) for acts in named} & set(records)):
        rec = records[clip]
        found = master_by_guid(masters, rec["animation"])
        if not found:
            out.append("%s (on %s): no master with its Animation GUID under %s" % (clip, c["stand_action"], c["clips"]))
            continue
        path, anim = found
        times = np.asarray(anim["rot"][0]["t"])
        keys = np.flatnonzero((times >= rec["first"] - 1e-6) & (times <= rec["first"] + rec["keys"] + 1e-6))
        if not len(keys):
            out.append("%s (on %s): its key range holds no key of %s" % (clip, c["stand_action"], os.path.basename(path)))
            continue
        pose = {"rot": [{"t": times[keys], "v": np.asarray(r["v"])[keys]} for r in anim["rot"]],
                "root_pos": {"v": np.asarray(anim["root_pos"]["v"])[keys]}}
        poses += 1
        why = stand_finding(soles_of(pose, bones, c["feet"]))
        if why:
            out.append("%s, the standing pose on %s (keys %d to %d of %s): %s, so the beast stands that far, times its "
                       "size, off the ground each time it stops" % (
                           clip, c["stand_action"], rec["first"], rec["first"] + rec["keys"], os.path.basename(path), why))
    return out, cached, poses


def master_by_guid(paths, guid):
    """(file, parsed animation) of the SkeletalAnimation item with this GUID in one of `paths`, or None."""
    for path in paths:
        with open(path, "rb") as fh:
            raw = fh.read()
        if guid not in raw:
            continue
        for it in tcm.parse(raw).items:
            if it.type_guid == SKELETAL_ANIMATION_TYPE and it.item_guid == guid:
                return path, parse_anim(tcm.segment_payload(it, it.segments[0]))
    return None


def cache_state(module_root, anm_path, geo_path):
    """None when the clip's RuntimeDataCache entry is at least as new as its clip record and its master, else why
    it is not. The signal is the file dates: the Kit writes the entry, no tool here does."""
    with open(anm_path, "rb") as fh:
        guid = str(uuid.UUID(bytes_le=fh.read(24)[8:24]))
    entry = os.path.join(module_root, "RuntimeDataCache", guid + ".rdc")
    if not os.path.exists(entry):
        return "it has no cache entry"
    def when(path):
        return time.strftime("%Y-%m-%d %H:%M", time.localtime(os.path.getmtime(path)))

    for what, path in (("master", geo_path), ("clip record", anm_path)):
        if os.path.getmtime(entry) < os.path.getmtime(path):
            return "its cache entry is older than its %s (cooked %s, %s written %s)" % (what, when(entry), what, when(path))
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
    sets_text = open(sets_path, encoding="utf-8", errors="replace").read()
    bound, rows = bound_clips(sets_text), set_rows(sets_text)
    named = {clip for s in c["sets"] for clip in rows.get(s, {}).values()}
    records = {}
    for clip in sorted(named | set(c["gaits"])):
        anm = os.path.join(root, c["clips"], clip + "_anm.tpac")
        if os.path.exists(anm):
            with open(anm, "rb") as fh:
                records[clip] = clip_record(fh.read())
    # the listed gaits, and any other clip a named set binds that claims travel: none escapes the stride check
    gaits = c["gaits"] + sorted(k for k in named - set(c["gaits"]) if k in records and records[k]["loop"])
    bad = notes = 0
    for clip in gaits:
        anm = os.path.join(root, c["clips"], clip + "_anm.tpac")
        geo = os.path.join(root, c["clips"], clip + "_geo.tpac")
        if not (clip in records and os.path.exists(geo)):
            if clip in bound:
                bad += 1
                print("FAIL %-28s bound   an action set binds it, and its clip record or its master is missing" % clip)
            else:
                notes += 1
                print("--   %-28s unbound not installed (no clip record or no master)" % clip)
            continue
        claim = records[clip]["loop"]
        m = measure(soles_of(load_anim(geo), bones, c["feet"]), claim or 0.0)
        why = judge(m, claim, a.tolerance, a.max_slip)
        stale, note = cache_state(root, anm, geo), ""
        if stale and clip in bound:
            why.append("an action set binds it and %s: the game plays what the Kit cooked before" % stale)
        elif stale:
            notes += 1
            note = "\n       note: no action set binds it yet, and %s" % stale
        bad += bool(why)
        print("%-4s %-28s %s claims %.2f m, feet cover %.2f m in %d frames, worst slip %.3f m%s%s" % (
            "FAIL" if why else "ok", clip, "bound  " if clip in bound else "unbound", claim or 0.0, m["stride"],
            m["frames"], max(m["slip"].values()), "".join("\n       " + w for w in why), note))
    pace, n_rows = record_findings(rows, records, c)
    stand, n_cached, n_poses = stand_findings(rows, records, c, root, bones)
    for why in pace:
        print("FAIL pace: %s" % why)
    for why in stand:
        print("FAIL bound clip: %s" % why)
    print("%s: read %d pace rows of %d named sets, %d cache entries of clips on the skeleton's package, %d standing "
          "pose(s): %d finding(s)" % (a.creature, n_rows, len(c["sets"]), n_cached, n_poses, len(pace) + len(stand)))
    print("%s: %d of %d gait clips fail" % (a.creature, bad, len(gaits)) if bad else
          "%s: all %d gait clips cover the ground they claim%s" % (
              a.creature, len(gaits), "; %d wait for a Kit start before an action set can bind them" % notes if notes else ""))
    return 1 if bad or pace or stand else 0


if __name__ == "__main__":
    sys.exit(main())
