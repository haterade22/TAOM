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

Every gait's master is the SkeletalAnimation item whose GUID its record names, in any master package under the
creature's asset folder: a bought master carries the pack's name (Warg_Slow_run) and serves several records, so a
file named after the clip is not looked for. A creature whose clips were bought, not made here (the warg, #1565)
may set these keys in its CREATURES entry:
  limits   the keys "max_slip" and "min_duty" (LIMITS holds the defaults, the elephant's). The pack's paws slip
           up to 0.91 m in a stance (the gallop's left hind paw) and a trot's fore paw is down for 3 of 16 keys.
           The warg's values sit just above what its clips have today, so a clip that gets worse fails. They are
           not a standard for a new clip: no tool here writes the warg's masters.
  cooked   "stamp": an entry is held to the checksum of its record (stamp_state), not to the files' dates. Of the
           warg's own masters 32 are dated 2026-08-31 09:01 (Warg_Trot: 2026-08-28 20:39), and every record no
           tool has rewritten 2026-08-28 20:33: the dates of copies. All but one are later than the entry the Kit
           cooked on 2026-08-28 (at 18:08 and 18:09), so the date rule calls those entries stale. The five
           masters the Kit cooked again in November gave the August payload byte for byte (read 2026-11-24).
           What the stamp does not see is in stamp_state. Any other value of "cooked" is a finding.
  known    {clip: (metres in the record, metres the feet cover, why with its issue)}: a gait that fails the
           stride judgement and that nobody has settled yet. The list excuses the stride line alone, for
           exactly those two numbers, compared at two decimals. The clip is measured and printed on every run.
           It fails when its stride starts to equal its travel (until it leaves the list), when either number
           is no longer the listed one, when a paw slips or does not plant (judged against the ground the feet
           cover, not the travel), when its entry is not cooked, and when no named set binds it or the run
           does not measure it. The warg's two are its side steps. For those the stride is not the node's
           measure: at the standing pace the node plays one loop in LoopDisplacement / (Duration x its
           sideways input) seconds, and it takes the rate of a turn in place from the turn-right row, which is
           the right side step's record (0x749C20 and 0x74E9C0, read 2026-11-24). So the list pins what the
           two records hold, and #1591 decides their travel and how this gate judges such a row.
  pending  {clip: why}: a clip that carries travel and has no stride to measure (a moving pace's strafe record,
           which plays the pace's forward keys; the node divides the sideways input by that travel for the
           weight of the sideways blend, 0x74E9C0). A pace action bound to one is a finding (record_findings),
           and so is a listed clip that no named set binds.
A gait whose animation is in two master files fails: the gate cannot tell which one the game plays.

    python tools/check_gait_stride.py [--creature elephant|spider|warg] [--tolerance 0.05] [--max-slip 0.08]
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
import set_clip_balance_name as scb  # noqa: E402
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
    # #1541: eight feet (the tip bone of each leg, front to back), the strafes travel along X, a turn clip goes
    # round a point and has no straight stride, and tools/spider_gait.py writes 60 keys a second.
    "spider": {
        "module": "LOTRLOME_Armory", "clips": "Assets/creature/spider/animations",
        "skeleton": ("Assets/creature/spider/animations/spider_correct_geo.tpac", "spider_skeleton"),
        "feet": {side.upper() + n: "joint%s_%s" % (bone, side)
                 for n, bone in (("1", "44"), ("2", "38"), ("3", "32"), ("4", "26")) for side in ("l", "r")},
        "gaits": ["spider_walk", "spider_trot", "spider_canter", "spider_gallop", "spider_walk_backwards",
                  "spider_strafe_left", "spider_strafe_right"],
        "axis": {"spider_strafe_left": 0, "spider_strafe_right": 0},
        "turns": ("spider_turn_left", "spider_turn_right"),
        "key_rate": 60.0,
        "action_sets": "ModuleData/action_sets.xml",
        "sets": ("as_spider",),
        "pace_actions": {"backward": "act_spider_walk_backward",
                         "strafe": ("act_spider_walk_right", "act_spider_walk_left"),
                         "walk": "act_spider_walk_forward",
                         "faster": ("act_spider_trot_forward", "act_spider_canter_forward", "act_spider_run_forward")},
        "stand_action": "act_spider_stand",
    },
    # #1565: the pack's own masters, found by GUID. tools/warg_gait_plan.json sets the forward gaits' records, and
    # tools/tests/test_warg_gait_plan.py measures the same paws.
    "warg": {
        "module": "LOTRLOME_Armory", "clips": "Assets/creature/warg/animations",
        "skeleton": ("Assets/creature/warg/mesh/Warg_Rig_V5_geo.tpac", "skeleton_warg"),
        "feet": {"FL": "Fingers1_L", "FR": "Fingers1_R", "BL": "Toes1_L", "BR": "Toes1_R"},
        "gaits": ["warg_walkfast", "warg_trot", "warg_canter", "warg_forward_gallop_left_foot",
                  "warg_walkbackfast", "warg_walkbackfast_straight",
                  "warg_strafe_left_faster", "warg_strafe_right_faster"],
        "axis": {"warg_strafe_left_faster": 0, "warg_strafe_right_faster": 0},
        "max_slip": 1.0, "min_duty": 0.15, "cooked": "stamp",
        "known": {clip: (1.6, covers, "a side step and a turn in place: the node takes a loop time from this "
                                      "travel and the Duration, not a stride, so no travel is set yet (#1591)")
                  for clip, covers in (("warg_strafe_left_faster", 0.73), ("warg_strafe_right_faster", 1.0))},
        "pending": {clip: "a moving pace's strafe record: it plays the pace's forward keys, which do not move "
                          "sideways" for clip in ("warg_walk_strafe", "warg_trot_strafe", "warg_canter_strafe",
                                                  "warg_forward_gallop_left_foot_strafe")},
        "action_sets": "ModuleData/action_sets.xml",
        "sets": ("as_warg",),
        "pace_actions": {"backward": "act_warg_backward_walk",
                         "strafe": ("act_warg_strafe_right", "act_warg_strafe_left"),
                         "walk": "act_warg_forward_walk",
                         "faster": ("act_warg_forward_trot", "act_warg_forward_canter",
                                    "act_warg_forward_gallop_right_foot", "act_warg_forward_gallop_left_foot")},
        "stand_action": "act_warg_stand_for_movement_data",
    },
}
KEY_RATE = 30.0        # keys per second in a master: the pack's frame rate
MIN_DURATION = 0.75    # of a strafe clip's real length; the vanilla horse records run from 0.8 to 1.19
# tolerance: stride against the claim, as a part of the claim. max_slip: metres a planted foot may leave the ground's
# speed in one stance. min_duty: the part of a loop a foot must be planted, under which it is named as not planting.
LIMITS = {"tolerance": 0.05, "max_slip": 0.08, "min_duty": 0.2}


def limits(creature, tolerance=None, max_slip=None):
    """The three limits a creature's gaits are judged with: an argument given on the command line, else the
    creature's own value, else LIMITS."""
    given = {"tolerance": tolerance, "max_slip": max_slip}
    return {k: given[k] if given.get(k) is not None else creature.get(k, default) for k, default in LIMITS.items()}


def measure(soles, claim, axis=1):
    """soles: {foot: array [frames, 3]} in model space (z up), one loop, in place. claim: metres per loop.
    The feet travel along `axis` (1 is Y, forward; 0 is X, a sidestep); the sign of the travel is taken from the
    data, so a backward walk measures the same."""
    n = len(next(iter(soles.values())))
    feet, speeds = {}, []
    for tag, p in soles.items():
        p = np.asarray(p, dtype=float)
        nxt = np.roll(p, -1, axis=0)
        planted = (p[:, 2] <= CONTACT) & (nxt[:, 2] <= CONTACT)
        step = (nxt - p)[planted]
        feet[tag] = {"duty": float(planted.mean()), "step": step}
        if len(step):
            speeds.append(float(np.median(step[:, axis])))
    if not speeds:
        return {"stride": 0.0, "slip": {t: float("inf") for t in soles}, "duty": {t: 0.0 for t in soles}, "frames": n}
    direction = 1.0 if np.median(speeds) >= 0 else -1.0
    belt = direction * abs(claim) / n
    return {
        "stride": abs(float(np.median(speeds))) * n,
        "slip": {t: float(np.abs(f["step"][:, axis] - belt).sum() + np.abs(f["step"][:, 1 - axis]).sum())
                 for t, f in feet.items()},
        "duty": {t: f["duty"] for t, f in feet.items()},
        "frames": n,
    }


def stride_finding(m, claim, tolerance):
    """The line for a stride that misses the claim (above 0) by more than the tolerance, or None."""
    if abs(m["stride"] - claim) > tolerance * claim:
        return "feet cover %.2f m a loop, the clip claims %.2f m (%+.0f%%)" % (
            m["stride"], claim, 100 * (m["stride"] - claim) / claim)
    return None


def judge(m, claim, tolerance, max_slip, min_duty=LIMITS["min_duty"]):
    out = []
    if claim is None or claim <= 0:
        return ["the clip carries no quad_movement travel"]
    miss = stride_finding(m, claim, tolerance)
    if miss:
        out.append(miss)
    for t, s in m["slip"].items():
        if m["duty"][t] < min_duty:
            out.append("%s is planted for only %.0f%% of the loop" % (t, 100 * m["duty"][t]))
        elif s > max_slip:
            out.append("%s slips %.2f m in a stance" % (t, s))
    return out


def bound_clips(action_sets_text):
    """Every clip name an action set binds."""
    return set(re.findall(r'animation="([^"]+)"', action_sets_text))


def clip_name(anm_bytes):
    """The clip's own name in a single-clip package: the sized string at 0x48."""
    return bytes(anm_bytes[0x4C:0x4C + struct.unpack_from("<i", anm_bytes, 0x48)[0]]).decode("utf-8", "replace")


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


def records_in(folder, clips):
    """({clip: clip_record}, {clip: its record file}, findings) for `clips`, each found by the clip's OWN name in
    the folder's record files: the Kit names a file new_animation_clip_N. Two files that hold one clip name are a
    finding, whichever of them has the clip's name as its file name; the gate cannot tell which the game plays. A
    record file that cannot be read is a finding too, with its name."""
    held, twice = {}, []
    for f in sorted(os.listdir(folder)):
        if f.endswith("_anm.tpac"):
            try:
                with open(os.path.join(folder, f), "rb") as fh:
                    raw = fh.read()
                clip_record(raw)                       # a file that is not a whole record is named, not a traceback
                held.setdefault(clip_name(raw), []).append((f, raw))
            except (OSError, struct.error, UnicodeError) as err:
                twice.append("%s cannot be read as a clip record (%s)" % (f, err))
    records, anm_of = {}, {}
    for clip in sorted(clips):
        files = held.get(clip, [])
        if len(files) > 1:
            twice.append("%s is the clip of %d record files: %s" % (clip, len(files), ", ".join(f for f, _ in files)))
        own = [x for x in files if x[0] == clip + "_anm.tpac"] or files
        if own:
            records[clip], anm_of[clip] = clip_record(own[0][1]), os.path.join(folder, own[0][0])
    return records, anm_of, twice


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
    skipped = set(c.get("turns", ())) | set(c.get("pending", {}))
    for set_id in c["sets"]:
        if set_id not in rows:
            out.append("no action set %s with rows" % set_id)
            continue
        got, gone = {}, set()
        for act in (acts["backward"], *acts["strafe"], acts["walk"], *acts["faster"], c["stand_action"]):
            clip = rows[set_id].get(act)
            if clip in skipped:     # the lists are keyed by clip, so a pace row on one would escape the stride check
                out.append("%s binds %s to %s, a clip the stride check skips (a turn, or one listed as pending): "
                           "a pace action must play a measured gait" % (set_id, act, clip))
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


def stand_findings(rows, records, c, module_root, bones, masters):
    """(findings, cache entries read, poses measured) for the clips the sets named in c["sets"] bind: a clip whose
    master is in the skeleton's package needs a current cache entry (cook_state), and the pose on the stand action
    must have its lowest sole on the ground. masters: the packages a pose's master is looked for in (report gives
    every one under the creature's folder)."""
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
        stale = cook_state(c, module_root, os.path.join(folder, clip + "_anm.tpac"), skel)
        if stale:
            out.append("%s plays keys of %s, and %s: the game plays the old keys" % (clip, os.path.basename(skel), stale))
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


def range_finding(times, rec):
    """Why a clip record's sources do not fit its master's key times, or None. The rule is the editor's own
    (FUN_180b8b810 in the wEditor TaleWorlds.Native.dll, read 2026-11-23): a track fits when its first key time is
    at or under the lower source and its last at or over the upper. A record that fails it draws one "ipo does not
    fit" line per bone when the Kit cooks the clip, and the game holds a key where the record names none: the
    first writer keyed 1 to N under sources 0 to N, and every loop stood still for one key time."""
    lo, hi = rec["first"], rec["first"] + rec["keys"]
    if not len(times):
        return "its record plays keys %g to %g and its master has no key" % (lo, hi)
    if times[0] <= lo + 1e-6 and hi <= times[-1] + 1e-6:
        return None
    return "its record plays keys %g to %g and its master holds keys %g to %g" % (lo, hi, times[0], times[-1])


def key_span(anim):
    """[latest first key time, earliest last key time] over every track that has keys: the editor holds each
    track to the record's sources, not the first bone's alone. [] when no track has a key."""
    tracks = [t["t"] for t in list(anim["rot"]) + list(anim.get("pos", ())) + [anim["root_pos"]]
              + ([anim["root_scale"]] if "root_scale" in anim else []) if len(t["t"])]
    if not tracks:
        return []
    if not all(np.isfinite(t[0]) and np.isfinite(t[-1]) for t in tracks):
        return [float("nan"), float("nan")]         # no number fits a range: range_finding reports it
    return [max(t[0] for t in tracks), min(t[-1] for t in tracks)]


def played_keys(anim, rec):
    """The master's keys that one loop of the record steps through: from its first source up to, not including,
    its last, which in a loop repeats the first (tools/elephant_write_masters.py, KEY TIMES)."""
    times = np.asarray(anim["rot"][0]["t"], dtype=float)
    keep = np.flatnonzero((times >= rec["first"] - 1e-6) & (times < rec["first"] + rec["keys"] - 1e-6))
    return {"rot": [{"t": times[keep], "v": np.asarray(r["v"])[keep]} for r in anim["rot"]],
            "root_pos": {"v": np.asarray(anim["root_pos"]["v"])[keep]}}


def range_findings(clips, records, masters):
    """(findings, masters read): range_finding for each clip that has a record, against its master among
    `masters`, on every track (key_span). A clip with a record and no master there is a finding: the gate says so,
    it does not pass what it could not read. A clip with no record is another gate's (record_findings)."""
    out, read = [], 0
    for clip in clips:
        if clip not in records:
            continue
        found = master_by_guid(masters, records[clip]["animation"])
        if not found:
            out.append("%s: no master with its Animation GUID under the creature's assets, so its key range is not checked" % clip)
            continue
        read += 1
        why = range_finding(key_span(found[1]), records[clip])
        if why:
            out.append("%s: %s (%s): the Kit warns on every bone and the game has no key for the rest" % (
                clip, why, os.path.basename(found[0])))
    return out, read


def masters_by_guid(paths, guid):
    """Yield (file, parsed animation) for each SkeletalAnimation item with this GUID in `paths`, in their order."""
    for path in paths:
        with open(path, "rb") as fh:
            raw = fh.read()
        if guid not in raw:
            continue
        for it in tcm.parse(raw).items:
            if it.type_guid == SKELETAL_ANIMATION_TYPE and it.item_guid == guid:
                yield path, parse_anim(tcm.segment_payload(it, it.segments[0]))


def master_by_guid(paths, guid):
    """(file, parsed animation) of the first SkeletalAnimation item with this GUID in `paths`, or None."""
    return next(masters_by_guid(paths, guid), None)


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


def stamp_state(module_root, anm_path):
    """None when the clip's RuntimeDataCache entry carries the stored checksum of its record, else why not. The
    Kit writes that checksum into the entry when it cooks, so the stamp ties an entry to the record it was cooked
    from whatever the files' dates say. Two things it does not see, which the dates did (cache_state):
      a master whose keys changed under one GUID, so this is the rule of a creature whose masters no tool writes;
      a record edited without a new stored checksum (a hex edit). The gate cannot recompute the checksum to find
      one: set_clip_balance_name.checksum_ok is false for 30 of the 41 records of as_warg, every one the Kit
      wrote and no tool here rewrote (read 2026-11-24). An edited travel still fails the stride check itself."""
    with open(anm_path, "rb") as fh:
        buf = fh.read()
    try:
        clip = scb.parse(buf)
    except (scb.ClipError, struct.error, IndexError, ValueError) as err:
        return "its record cannot be read (%s)" % err
    entry = scb.rdc_path(module_root, clip.package_guid)
    if not os.path.isfile(entry):
        return "it has no cache entry"
    with open(entry, "rb") as fh:
        if not scb.rdc_current(fh.read(), clip, buf):
            return "its cache entry does not carry its record's checksum: the record changed after the Kit cooked it"
    return None


COOK_RULES = (None, "stamp")       # what a creature's "cooked" may say; None is the date rule


def cook_state(c, module_root, anm_path, geo_path):
    """Why the clip's cache entry is not current by the creature's rule, or None: stamp_state where the creature
    says cooked "stamp", else the dates (cache_state)."""
    if c.get("cooked") == "stamp":
        return stamp_state(module_root, anm_path)
    return cache_state(module_root, anm_path, geo_path)


def known_verdict(entry, soles, axis, claim, m, why, lim):
    """(label, the measurement to print, lines) for a gait in a creature's known list. entry: (claimed metres,
    covered metres, why). m, why: the gait's measurement against its claim and judge's lines for it. The list
    excuses one thing, that record on those feet: the label is KNOWN only for it."""
    claims, covers, reason = entry
    if claim is None or claim <= 0:
        return "FAIL", m, why
    miss = stride_finding(m, claim, lim["tolerance"])
    if not miss:
        return "FAIL", m, why + ["it is in the known list (%s) and its feet now cover its travel: remove it from "
                                 "the creature's known list" % reason]
    if (round(claim, 2), round(m["stride"], 2)) != (claims, covers):
        return "FAIL", m, why + ["the known list holds %.2f m claimed and %.2f m covered (%s): the record or the "
                                 "master changed, so correct it or the list" % (claims, covers, reason)]
    # the slip and the stance against the ground the feet do cover: against the claim every stance of a short
    # stride reads as a slip
    own = measure(soles, m["stride"], axis=axis)
    rest = judge(own, m["stride"], lim["tolerance"], lim["max_slip"], lim["min_duty"])
    if rest:
        return "FAIL", own, [miss] + rest + ["the known list covers the stride alone (%s)" % reason]
    return "KNOWN", own, [miss, "not counted: %s" % reason]


def clip_problems(module_root, folder, clips):
    """Why a clip in `folder` (under the module root) is not ready to be bound: no clip record or no master, or
    cache_state's reason. A clip bound with no cache entry crashed Agent.SetActionSet in another modder's test, so
    both binders (bind_elephant_clips.py, bind_spider_clips.py) refuse on a non-empty answer."""
    out = []
    for clip in clips:
        anm = os.path.join(module_root, folder, clip + "_anm.tpac")
        geo = os.path.join(module_root, folder, clip + "_geo.tpac")
        if not (os.path.exists(anm) and os.path.exists(geo)):
            out.append("%s: no clip record or no master" % clip)
            continue
        why = cache_state(module_root, anm, geo)
        if why:
            out.append("%s: %s" % (clip, why))
    return out


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


def report(name, c, root, bones, sets_text, tolerance=None, max_slip=None):
    """Print the creature's report and return the exit code: 1 on a finding, else 0. root: the module folder;
    bones: the skeleton's; sets_text: the action sets file. The two arguments go over the creature's own limits."""
    if c.get("cooked") not in COOK_RULES:
        print("FAIL table: cooked is %r, and the only rule a creature may name is \"stamp\"" % (c.get("cooked"),))
        return 1
    lim = limits(c, tolerance, max_slip)
    skel_path = os.path.join(root, c["skeleton"][0])
    bound, rows = bound_clips(sets_text), set_rows(sets_text)
    named = {clip for s in c["sets"] for clip in rows.get(s, {}).values()}
    records, anm_of, twice = records_in(os.path.join(root, c["clips"]), named | set(c["gaits"]))
    # the listed gaits, and any other clip a named set binds that claims travel: none escapes the stride check
    turns, pending, known = set(c.get("turns", ())), c.get("pending", {}), c.get("known", {})
    gaits = c["gaits"] + sorted(k for k in named - set(c["gaits"]) - turns - set(pending)
                                if k in records and records[k]["loop"])
    # every master package under the creature's own asset folder, each once: ADOD keeps two elephant masters
    # beside the mesh, and a bought master carries the pack's name, not its clip's
    creature = os.path.dirname(os.path.join(root, c["clips"]))
    masters = sorted({os.path.normpath(p) for p in [skel_path] + [
        os.path.join(dp, f) for dp, _, fs in os.walk(creature) for f in fs if f.endswith("_geo.tpac")]})
    bad, notes, excused = len(twice), 0, 0
    for why in twice:
        print("FAIL record files: %s" % why)
    for clip in sorted(known):
        why = ("is also in the pending list" if clip in pending else "is also listed as a turn" if clip in turns else
               "is listed and no named set binds it" if clip not in named else
               "is listed and is not a gait this run measures" if clip not in gaits else None)
        if why:
            bad += 1
            print("FAIL known list: %s %s; a known failure is a bound gait that the run measures" % (clip, why))
    for clip in sorted(set(pending) - named):
        bad += 1
        print("FAIL pending list: %s is listed and no named set binds it: take it off the list" % clip)
    for clip in sorted(turns & set(records)):
        print("--   %-28s a turn: it goes round a point, so it has no straight stride to check" % clip)
    for clip in sorted(set(pending) & named):
        print("--   %-28s not checked: %s" % (clip, pending[clip]))
    for clip in gaits:
        found = list(masters_by_guid(masters, records[clip]["animation"])) if clip in records else []
        if len(found) > 1:
            bad += 1
            print("FAIL %-28s %d master files hold the animation its record names (%s): the gate cannot tell which "
                  "one the game plays" % (clip, len(found), ", ".join(os.path.basename(p) for p, _ in found)))
            continue
        if not found:
            if clip in bound:
                bad += 1
                print("FAIL %-28s bound   an action set binds it, and its clip record is missing or no master under "
                      "%s holds the animation its record names" % (clip, os.path.relpath(creature, root)))
            else:
                notes += 1
                print("--   %-28s unbound not installed (no clip record or no master)" % clip)
            continue
        (geo, anim), anm = found[0], anm_of[clip]
        claim = records[clip]["loop"]
        played = played_keys(anim, records[clip])
        if not len(played["rot"][0]["t"]):
            bad += 1
            print("FAIL %-28s its record plays no key of its master" % clip)
            continue
        soles, axis = soles_of(played, bones, c["feet"]), c.get("axis", {}).get(clip, 1)
        m = measure(soles, claim or 0.0, axis=axis)
        why = judge(m, claim, lim["tolerance"], lim["max_slip"], lim["min_duty"])
        label = "FAIL" if why else "ok"
        if clip in known:
            label, m, why = known_verdict(known[clip], soles, axis, claim, m, why, lim)
        stale, note = cook_state(c, root, anm, geo), ""
        if stale and clip in bound:
            label = "FAIL"
            why.append("an action set binds it and %s: the game plays what the Kit cooked before" % stale)
        elif stale:
            notes += 1
            note = "\n       note: no action set binds it yet, and %s" % stale
        bad += label == "FAIL"
        excused += label == "KNOWN"
        print("%-4s %-28s %s claims %.2f m, feet cover %.2f m in %d frames, worst slip %.3f m%s%s" % (
            label, clip, "bound  " if clip in bound else "unbound", claim or 0.0, m["stride"],
            m["frames"], max(m["slip"].values()), "".join("\n       " + w for w in why), note))
    pace, n_rows = record_findings(rows, records, c, c.get("key_rate", KEY_RATE))
    stand, n_cached, n_poses = stand_findings(rows, records, c, root, bones, masters)
    ranges, n_ranges = range_findings(sorted(named), records, masters)
    for why in pace:
        print("FAIL pace: %s" % why)
    for why in stand:
        print("FAIL bound clip: %s" % why)
    for why in ranges:
        print("FAIL key range: %s" % why)
    print("%s: read %d pace rows of %d named sets, %d cache entries of clips on the skeleton's package, %d standing "
          "pose(s), the key range of %d bound clip(s): %d finding(s)" % (
              name, n_rows, len(c["sets"]), n_cached, n_poses, n_ranges, len(pace) + len(stand) + len(ranges)))
    wait = "; %d wait for a Kit start before an action set can bind them" % notes if notes else ""
    if bad:
        print("%s: %d finding(s) in the record files, the lists and the %d gait clips" % (name, bad, len(gaits)))
    elif excused:
        print("%s: %d gait clips, %d of them held at the numbers the known list gives and not judged by stride; the "
              "others cover the ground they claim%s" % (name, len(gaits), excused, wait))
    else:
        print("%s: all %d gait clips cover the ground they claim%s" % (name, len(gaits), wait))
    return 1 if bad or pace or stand or ranges else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--creature", default="elephant", choices=sorted(CREATURES))
    ap.add_argument("--tolerance", type=float, default=None,
                    help="stride against the claim, as a part of it (default %s)" % LIMITS["tolerance"])
    ap.add_argument("--max-slip", type=float, default=None,
                    help="metres in one stance (default: the creature's, else %s)" % LIMITS["max_slip"])
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
    with open(sets_path, encoding="utf-8", errors="replace") as fh:
        sets_text = fh.read()
    return report(a.creature, c, root, bones, sets_text, a.tolerance, a.max_slip)


if __name__ == "__main__":
    sys.exit(main())
