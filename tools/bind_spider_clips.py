#!/usr/bin/env python3
"""Bind the giant spider's own locomotion clips (#1541) in the Armory and in the repo's snapshot.

Three files change together, the same change in the LIVE Armory and in docs/reference/lotrlome-armory-snapshot:

action_types.xml        declares the nine new actions (NEW), by name only, as the elephant's and the warg's are.
action_sets.xml
  as_spider             every action that played a June gait, turn or idle clip takes the spider_* clip of its role
                        (REBIND), and the nine new actions get their rows. #1550 slice A adds the hits, the falls and
                        deaths (each *_continue action holds its fall's last pose), the quick stop and the jump start;
                        the attacks keep their June clips until slice B. The eight act_horse_jump_* rows played the June walk,
                        so they take the new walk.
  as_spider_town_and_village   its idle row takes spider_idle.
  rider sets            every set that seats a rider on the spider (the partial as_human_warrior and the dwarf
                        copy: any set whose act_spider_run_forward row plays a rider_* clip) gets a rider row for
                        each new action. A walk, trot, canter or backward row takes the clip the warg's rider plays
                        for that role; the gallop's twin takes rider_warg_canter, the clip the June
                        act_spider_run_forward row has. A usage action with no rider row is the June "thrust loop"
                        (docs/features/spider.md, Rider-side bindings).
monster_usage_sets.xml  the spider's movement rows take the warg's and the elephant's layout: pace 0 is the backward
                        walk, pace 1 the stand, paces 2 to 5 walk, trot, canter and gallop. The direction="none" row
                        of paces 2 to 5 plays that gait's in-place twin, as the vanilla horse's gait_* clips do; pace
                        0 plays the backward walk's twin (#1550; until then the forward walk's). Until #1541 pace 0 played
                        the forward walk, the stand pace's row was an idle whose record carried the elephant
                        canter's limits, and trot, canter and gallop all played one run clip.
                        The upper-body overlay rows (OVERLAYS, #1559): each moving pace's two rows play that pace's
                        own gait action, and the set names no gallop_acceleration_head_action. The engine lays an
                        overlay's clip over the WHOLE body at up to full weight (measured in a battle on 2026-11-23:
                        the leg tips of a spider at 7 m/s moved 1.0 to 1.3 m/s against its body under the idle
                        clip, 2 to 4 under the turn-in-place clip, 6.5 to 6.8 in the gait). Since June the rows
                        had played the turn actions at every pace and the idle on the gallop's acceleration, so
                        the spiders slid in a charge. The warg's and the elephant's rows play a clip of the
                        pace's own rhythm, and the warg's set has no gallop acceleration action.

A row is upserted: a missing row is added, and a row that is there on another clip is put on the right one. Rows
inside an XML comment are neither edited nor counted (the live action_sets.xml holds hundreds of commented-out
rows). The edits are regex edits, because the files must stay byte for byte outside the rows; the RESULT is then
read as the engine reads it, with ElementTree, and planned_problems compares every pair this tool owns with what
the parse holds. A plan that does not read back refuses.

WHAT --check DOES NOT READ. It reads these three files, live and snapshot, and nothing else. The game builds its
action sets from every module's file, each run through that module's stylesheet first (the Armory's and
DualWield's action_sets.xslt today; both keep the spider's rows, checked with the game's own merge and transform
code on 2026-11-22). A stylesheet, or another module's XML, that drops or rebinds one of these rows is outside
this tool: its answer is "the three files carry the binding", never "the game plays it".

THE GATE. The game plays a clip from the RuntimeDataCache entry the Modding Kit cooks, and a clip bound with no
entry crashed Agent.SetActionSet in another modder's test (docs/reference/bannerlord-animation-system-map.md). So
nothing is written unless every clip this tool binds has a record, a master and a cache entry no older than either
(check_gait_stride.clip_problems).

Byte-faithful: binary read, BOM and CRLF kept, all six files planned and verified before the first write. Each
file is written beside its target under another name and then moved over it, so no file is ever half written:
declarations, then rows, then the usage table; the live module before the snapshot. The moves are not one
transaction. A run that dies after the live action_sets.xml went in and before monster_usage_sets.xml did leaves
the June movement rows on the new bindings: the stand pace then reads its movement data from act_spider_idle,
whose clip has no clip usage now, and tools/verify_mount_assets.py fails that state. DO NOT START THE GAME on it:
--check says which files still differ, and running --apply again finishes the job. One backup per changed LIVE file, outside the module (a .bak inside it breaks the distribution):
--backup-dir, default E:/LOTRAOMAssets/Spider/bind_backup_<stamp>. --apply refuses (exit 2) while the game or the
Kit runs.

    python tools/bind_spider_clips.py            # plan, writes nothing
    python tools/bind_spider_clips.py --apply
    python tools/bind_spider_clips.py --check    # exit 1 unless every file carries the binding
Exit 0 done or nothing to do, 1 refused or not bound, 2 cannot run (no Armory, the game runs, no backup folder).
Undo: copy the backups back over the three live files and `git checkout` the snapshot. Afterwards run
python tools/check_gait_stride.py --creature spider, python tools/verify_mount_assets.py spider,
python tools/audit_mount_parity.py and the dwarf-rider-rows gate (tools/data_gates.py).
"""
import argparse
import datetime as dt
import difflib
import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir, game_or_kit_running  # noqa: E402
from check_gait_stride import CREATURES, clip_problems  # noqa: E402

REPO = Path(__file__).resolve().parent.parent
SNAPSHOT = REPO / "docs" / "reference" / "lotrlome-armory-snapshot"
FILES = ("action_types.xml", "action_sets.xml", "monster_usage_sets.xml")   # the write order: see the header
CLIPS = CREATURES["spider"]["clips"]
TMP = ".spider-bind-tmp"    # never .xml: the folder is globbed
JUMPS = ["act_horse_jump_" + s for s in ("forward", "forward_end", "forward_end_hard", "forward_loop", "high",
                                         "high_end", "high_end_hard", "high_loop")]
# as_spider: action -> clip. A jump row keeps a clip with a quad_movement usage, as it has had since June.
REBIND = {
    "act_horse_forward_canter": "spider_canter", "act_horse_conversation_start": "spider_idle",
    **{a: "spider_walk" for a in JUMPS},
    "act_inventory_idle": "spider_idle", "act_inventory_idle_start": "spider_idle",
    "act_spider_idle": "spider_idle", "act_spider_idle_1": "spider_idle", "act_spider_idle_2": "spider_idle",
    "act_spider_idle_dg": "spider_idle", "act_spider_taunt": "spider_idle",
    "act_spider_walk_forward": "spider_walk", "act_spider_walk_left": "spider_strafe_left",
    "act_spider_walk_right": "spider_strafe_right", "act_spider_run_forward": "spider_gallop",
    "act_spider_turn_left": "spider_turn_left", "act_spider_turn_right": "spider_turn_right",
    # #1550 slice A: the hits, the falls and deaths with a held last pose for each *_continue, the stop, the jump
    "act_spider_quick_stop": "spider_quick_stop", "act_spider_quick_stop_when_fast": "spider_quick_stop",
    "act_spider_hit_front": "spider_hit_front", "act_spider_hit_back": "spider_hit_back",
    "act_spider_hit_left": "spider_hit_left", "act_spider_hit_right": "spider_hit_right",
    "act_spider_hit_object": "spider_hit_front", "act_spider_hit_object_while_falling": "spider_hit_front",
    "act_spider_rear_damaged": "spider_hit_front",
    "act_spider_death": "spider_death", "act_spider_death_2": "spider_death_right",
    **{"act_spider_fall_" + way + end: clip + side
       for way, side in (("roll", ""), ("backwards", ""), ("right", "_right"), ("slow_right", "_right"),
                         ("left", "_left"), ("slow_left", "_left"))
       for end, clip in (("", "spider_death"), ("_continue", "spider_dead"))},
    "act_spider_jump": "spider_jump_start",
}
# new action, its clip in as_spider, the rider's clip
NEW = [
    ("act_spider_stand", "spider_stand", "rider_warg_stand_for_movement"),
    ("act_spider_walk_backward", "spider_walk_backwards", "rider_warg_walkbackfast"),
    ("act_spider_walk_backward_stand", "spider_walk_backwards_inplace", "rider_warg_walkbackfast"),     # #1550
    ("act_spider_walk_stand", "spider_walk_inplace", "rider_warg_forward_walk"),
    ("act_spider_trot_forward", "spider_trot", "rider_warg_trot"),
    ("act_spider_trot_stand", "spider_trot_inplace", "rider_warg_trot"),
    ("act_spider_canter_forward", "spider_canter", "rider_warg_canter"),
    ("act_spider_canter_stand", "spider_canter_inplace", "rider_warg_canter"),
    ("act_spider_run_stand", "spider_gallop_inplace", "rider_warg_canter"),
]
TOWN = {"act_spider_idle": "spider_idle"}
WANT = {**REBIND, **{a: c for a, c, _ in NEW}}       # every as_spider row this tool owns
RIDER_WANT = {a: r for a, _, r in NEW}               # every rider row this tool owns
# (is_left_foot, pace, direction, turn_direction, action): the warg's layout with the spider's actions
MOVEMENTS = (
    [("False", 0, "front", "none", "act_spider_walk_backward"), ("False", 0, "right", "none", "act_spider_walk_right"),
     ("False", 0, "left", "none", "act_spider_walk_left"), ("False", 0, "none", "none", "act_spider_walk_backward_stand"),
     # the stand pace's none/none row is its LAST: listed first (until #1550) it hid the four rows after it
     ("False", 1, "none", "right", "act_spider_turn_right"), ("False", 1, "none", "left", "act_spider_turn_left"),
     ("False", 1, "right", "none", "act_spider_walk_right"), ("False", 1, "left", "none", "act_spider_walk_left"),
     ("False", 1, "none", "none", "act_spider_stand")]
    + [(foot, pace, direction, "none", {"front": front, "right": "act_spider_walk_right", "left": "act_spider_walk_left",
                                        "none": stand}[direction])
       for foot, pace, front, stand in (("False", 2, "act_spider_walk_forward", "act_spider_walk_stand"),
                                        ("False", 3, "act_spider_trot_forward", "act_spider_trot_stand"),
                                        ("False", 4, "act_spider_canter_forward", "act_spider_canter_stand"),
                                        ("False", 5, "act_spider_run_forward", "act_spider_run_stand"),
                                        ("True", 5, "act_spider_run_forward", "act_spider_run_stand"))
       for direction in ("front", "right", "left", "none")])
# (pace, direction, turn_direction or None, action): the upper-body overlay rows. A moving pace's rows play the
# pace's own front action; the stand pace keeps the idles it has had since June.
OVERLAYS = ([(pace, "none", turn, action)
             for pace, action in ((0, "act_spider_walk_backward"), (2, "act_spider_walk_forward"), (3, "act_spider_trot_forward"),
                                  (4, "act_spider_canter_forward"), (5, "act_spider_run_forward"))
             for turn in ("right", "left")]
            + [(1, "front", None, "act_spider_idle_1"), (1, "back", None, "act_spider_idle"), (1, "none", None, "act_spider_idle")])
HEAD_ACTION = "gallop_acceleration_head_action"      # the spider's set names none: see the header
MARK = "#1541, written by tools/bind_spider_clips.py"
DIRECTIONS = ("left", "right", "front", "front_left", "front_right", "back", "back_left", "back_right")


def hidden_rows(rows):
    """The movement rows the engine never stores. Its loader (0x621E40, native read 2026-11-23) files a row under
    foot | pace | direction | turn and keeps the first row of a key. direction="none" is filed under "none" and
    all eight directions, turn_direction="none" under "none" and both turns, is_left_foot="True" under both feet.
    So a none/none row listed before a pace's turn and strafe rows takes their keys, and they are hidden."""
    held, out = set(), []
    for row in rows:
        foot, pace, direction, turn, _ = row
        keys = {(f, pace, d, t)
                for f in (("False", "True") if foot == "True" else ("False",))
                for d in ((("none",) + DIRECTIONS) if direction == "none" else (direction,))
                for t in (("none", "left", "right") if turn == "none" else (turn,))}
        if keys <= held:
            out.append(row)
        held |= keys
    return out
# A comment is matched first, whole, so a row inside one is never group 2: it is neither edited nor counted.
ROW = re.compile(r'<!--.*?-->|(<action\s+type="([^"]+)"\s+animation=")([^"]+)(")', re.S)
RIDER = re.compile(r'<action\s+type="act_spider_run_forward"\s+animation="rider_[^"]+"')


class BindError(Exception):
    pass


def _nl(text):
    return "\r\n" if "\r\n" in text else "\n"


def _mask(text):
    """`text` with every character inside an XML comment blanked, line ends kept: the same length, so an offset
    found in it is an offset in `text`."""
    return re.sub(r"<!--.*?-->", lambda m: re.sub(r"[^\r\n]", " ", m.group(0)), text, flags=re.S)


def _set_span(text, set_id):
    """(start, end) of the action set's element, `</action_set>` included. A set inside a comment is not one."""
    masked = _mask(text)
    m = re.search(r'<action_set\s+id="%s"[^>]*>' % re.escape(set_id), masked)
    if not m or m.group(0).endswith("/>"):
        raise BindError("no action set %s with rows" % set_id)
    end = masked.find("</action_set>", m.end())
    if end < 0:
        raise BindError("action set %s is not closed" % set_id)
    return m.start(), end + len("</action_set>")


def _put(block, mapping):
    """`block` with every live row of an action in `mapping` on that action's clip, and the actions seen."""
    seen = set()

    def swap(m):
        if m.group(2) in mapping:
            seen.add(m.group(2))
            return m.group(1) + mapping[m.group(2)] + m.group(4)
        return m.group(0)

    return ROW.sub(swap, block), seen


def _rebind(block, mapping, set_id):
    out, seen = _put(block, mapping)
    gone = sorted(set(mapping) - seen)
    if gone:
        raise BindError("%s has no row for %s" % (set_id, ", ".join(gone)))
    return out


def _add_rows(block, rows, indent, nl, after=None):
    """`block` with a row for each (action, clip): one that is there is put on its clip, one that is not is added
    after the row of action `after`, or before the set closes."""
    block, have = _put(block, dict(rows))
    lines = ['%s<action type="%s" animation="%s" />' % (indent, a, c) for a, c in rows if a not in have]
    if not lines:
        return block
    new = nl.join(["%s<!-- %s -->" % (indent, MARK)] + lines) + nl
    if after:
        at = next((m for m in ROW.finditer(block) if m.group(2) == after), None)
        end = block.find("\n", at.end()) + 1 if at else 0
        if not end:
            raise BindError("no %s row to add the new rows after" % after)
        return block[:end] + new + block[end:]
    close = block.rfind("</action_set>")
    start = block.rfind("\n", 0, close) + 1
    return block[:start] + new + block[start:]


def plan_action_sets(text):
    nl = _nl(text)
    s, e = _set_span(text, "as_spider")
    block = _add_rows(_rebind(text[s:e], REBIND, "as_spider"), [(a, c) for a, c, _ in NEW], "\t\t", nl)
    text = text[:s] + block + text[e:]
    s, e = _set_span(text, "as_spider_town_and_village")
    text = text[:s] + _rebind(text[s:e], TOWN, "as_spider_town_and_village") + text[e:]
    riders, at = 0, 0
    while True:      # every set that seats a rider on the spider
        masked = _mask(text)
        m = RIDER.search(masked, at)
        if not m:
            break
        s = masked.rfind("<action_set", 0, m.start())
        e = masked.find("</action_set>", m.start()) + len("</action_set>")
        block = _add_rows(text[s:e], [(a, r) for a, _, r in NEW], "\t\t", nl, after="act_spider_run_forward")
        text = text[:s] + block + text[e:]
        riders, at = riders + 1, s + len(block)
    if riders < 2:
        raise BindError("found %d rider set(s) with an act_spider_run_forward row; the partial as_human_warrior and "
                        "the dwarf copy make two" % riders)
    return text


def plan_action_types(text):
    nl = _nl(text)
    masked = _mask(text)
    have = set(re.findall(r'<action\s+name="([^"]+)"', masked))
    lines = ['\t<action name="%s" />' % a for a, _, _ in NEW if a not in have]
    if not lines:
        return text
    rows = list(re.finditer(r'[ \t]*<action\s+name="act_spider_[^"]+"[^>]*/>\r?\n', masked))
    if not rows:
        raise BindError("action_types.xml declares no act_spider_ action to add the new ones after")
    at = rows[-1].end()
    return text[:at] + nl.join(["\t<!-- %s -->" % MARK] + lines) + nl + text[at:]


def movement_rows(nl, indent="\t\t\t"):
    out = ["%s<!-- %s. Pace 0 backward, 1 stand, 2 walk, 3 trot, 4 canter, 5 gallop (both feet). The" % (indent, MARK),
           "%s     direction=\"none\" row of paces 2 to 5 is that gait's in-place twin, and so is pace 0's." % indent,
           "%s     Every pace keeps a none row: a missing one is a native division by zero (2026-06-04). A none row" % indent,
           "%s     fills every direction and turn no earlier row holds, so a pace's none/none row is its last. -->" % indent]
    out += ['%s<monster_usage_movement is_left_foot="%s" pace="%d" direction="%s" turn_direction="%s" action="%s" />' % (
        (indent,) + row) for row in MOVEMENTS]
    return nl + nl.join(out) + nl + indent[:-1]


def overlay_rows(nl, indent="\t\t\t"):
    out = ["%s<!-- #1550, written by tools/bind_spider_clips.py. The engine lays an overlay row's clip over the whole" % indent,
           "%s     body, so each moving pace's rows play that pace's own gait. The turn-in-place actions here, and" % indent,
           "%s     the idle as the gallop's acceleration action, made the spiders slide (2026-11-23). -->" % indent]
    for pace, direction, turn, action in OVERLAYS:
        out.append('%s<monster_usage_upper_body_movement pace="%d" direction="%s"%s action="%s" />' % (
            indent, pace, direction, ' turn_direction="%s"' % turn if turn else "", action))
    return nl + nl.join(out) + nl + indent[:-1]


def _spider_usage(text):
    """(start of the spider usage set's opening tag, end of that tag, start of its closing tag) in `text`."""
    masked = _mask(text)
    m = re.search(r'<monster_usage_set\b[^>]*?\bid="spider"', masked)
    if not m:
        raise BindError("no monster usage set spider")
    return m.start(), masked.index(">", m.end()) + 1, masked.find("</monster_usage_set>", m.end())


def _block(text, tag, rows):
    """`text` with the content of the spider usage set's <tag> block replaced by `rows`."""
    _, opened, close = _spider_usage(text)
    masked = _mask(text)
    s = masked.find("<%s>" % tag, opened)
    e = masked.find("</%s>" % tag, s)
    if s < 0 or e < 0 or not s < e < close:
        raise BindError("the spider usage set has no %s block" % tag)
    return text[:s + len(tag) + 2] + rows + text[e:]


def plan_usage(text):
    nl = _nl(text)
    start, opened, _ = _spider_usage(text)
    tag = re.sub(r'\s+%s="[^"]*"' % HEAD_ACTION, "", text[start:opened])
    text = text[:start] + tag + text[opened:]
    text = _block(text, "monster_usage_upper_body_movements", overlay_rows(nl))
    return _block(text, "monster_usage_movements", movement_rows(nl))


PLANS = {"action_types.xml": plan_action_types, "action_sets.xml": plan_action_sets, "monster_usage_sets.xml": plan_usage}


def bound_clips():
    return sorted(set(WANT.values()) | set(TOWN.values()))


def _pairs(element, wanted, where):
    """What `element`'s rows hold against `wanted` ({action: clip}): a missing row, or a row on another clip."""
    out = []
    for action, clip in wanted.items():
        got = [a.get("animation") for a in element.findall("action") if a.get("type") == action]
        if not got:
            out.append("%s has no %s row" % (where, action))
        out += ["%s plays %s on %s, not %s" % (where, action, g, clip) for g in sorted(set(got)) if g != clip]
    return out


def planned_problems(roots):
    """What the three planned files, read as the engine reads them (parsed: comments dropped, sets as elements),
    hold that differs from this tool's tables. roots: {file name: the parsed root}."""
    out = []
    declared = {a.get("name") for a in roots["action_types.xml"].iter("action")}
    out += ["%s is not declared in action_types.xml" % a for a, _, _ in NEW if a not in declared]
    sets = list(roots["action_sets.xml"].iter("action_set"))
    spider = [s for s in sets if s.get("id") == "as_spider"]
    if len(spider) != 1:
        return out + ["%d action sets are named as_spider" % len(spider)]
    out += _pairs(spider[0], WANT, "as_spider")
    for town in (s for s in sets if s.get("id") == "as_spider_town_and_village"):
        out += _pairs(town, TOWN, "as_spider_town_and_village")
    riders = [s for s in sets if any(a.get("type") == "act_spider_run_forward" and (a.get("animation") or "").startswith("rider_")
                                     for a in s.findall("action"))]
    if len(riders) < 2:
        out.append("%d set(s) seat a rider on the spider; the partial as_human_warrior and the dwarf copy make two" % len(riders))
    for n, rider in enumerate(riders, 1):
        out += _pairs(rider, RIDER_WANT, "rider set %d (%s)" % (n, rider.get("id")))
        has = {a.get("type") for a in rider.findall("action")}
        out += ["rider set %d (%s) has no row for %s, which an overlay row plays: the rider would have no clip for it" % (
            n, rider.get("id"), action) for action in sorted({o[3] for o in OVERLAYS} - has)]
    usage = [u for u in roots["monster_usage_sets.xml"].iter("monster_usage_set") if u.get("id") == "spider"]
    block = usage[0].find("monster_usage_movements") if len(usage) == 1 else None
    if block is None:
        return out + ["no single spider usage set with a movements block"]
    moves = [(r.get("is_left_foot"), int(r.get("pace")), r.get("direction"), r.get("turn_direction"), r.get("action"))
             for r in block.findall("monster_usage_movement")]
    if moves != list(MOVEMENTS):
        out.append("the spider's movement rows are not the table this tool writes (%d rows, %d wanted)" % (len(moves), len(MOVEMENTS)))
    out += ["the engine never stores the movement row %s: an earlier row of pace %d holds every key it files" % (
        " ".join(map(str, row)), row[1]) for row in hidden_rows(moves)]
    upper = usage[0].find("monster_usage_upper_body_movements")
    overlays = [(int(r.get("pace")), r.get("direction"), r.get("turn_direction"), r.get("action"))
                for r in (upper.findall("monster_usage_upper_body_movement") if upper is not None else [])]
    if overlays != list(OVERLAYS):
        out.append("the spider's upper-body overlay rows are not the table this tool writes (%d rows, %d wanted): the engine "
                   "lays a row's clip over the whole body, and one that is not its pace's gait takes the legs out of the gait" % (
                       len(overlays), len(OVERLAYS)))
    if usage[0].get(HEAD_ACTION) is not None:
        out.append("the spider usage set names %s=%s: its clip plays over the whole body while the spider gallops" % (
            HEAD_ACTION, usage[0].get(HEAD_ACTION)))
    bound = {a.get("type") for a in spider[0].findall("action")}
    named = {m[4] for m in moves} | {o[3] for o in overlays}
    out += ["the usage set names %s and as_spider has no row for it" % a for a in sorted(named - bound)]
    # The engine merges every block of one id and lets a child set override its base, so a row for an owned
    # action ANYWHERE else counts: another partial as_human_warrior block, or a race set, on another clip.
    base_of = {}
    for s in sets:
        base_of.setdefault(s.get("id"), s.get("base_set"))

    def descends_from_spider(set_id):
        seen = set()
        while set_id and set_id not in seen:       # a child of a child of as_spider is the spider's too
            if set_id == "as_spider":
                return True
            seen.add(set_id)
            set_id = base_of.get(set_id)
        return False

    for other in sets:
        if other is spider[0] or other in riders:
            continue
        of_spider = descends_from_spider(other.get("base_set"))
        wanted = WANT if of_spider else RIDER_WANT
        for row in other.findall("action"):
            action, clip = row.get("type"), row.get("animation")
            if action in wanted and clip != wanted[action]:
                out.append("%s (%s) plays %s on %s, not %s: it overrides the binding this tool owns" % (
                    other.get("id"), "a descendant of as_spider" if of_spider else "merged with or laid over a rider set",
                    action, clip, wanted[action]))
    return out


def differing_lines(was, now, limit=6):
    """The first lines a planned file would change, for a reader of --check."""
    lines = [d for d in difflib.unified_diff(was.splitlines(), now.splitlines(), lineterm="", n=0)
             if d[:1] in "+-" and d[:3] not in ("+++", "---")]
    return ["    " + d.replace("\t", " ").strip()[:150] for d in lines[:limit]] + (
        ["    ... %d more" % (len(lines) - limit)] if len(lines) > limit else [])


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--game", default=None)
    ap.add_argument("--backup-dir", default=None)
    a = ap.parse_args(argv)
    root = Path(a.game or game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")) / "Modules" / "LOTRLOME_Armory"
    live = root / "ModuleData"
    if not (live / "action_sets.xml").exists():
        print("cannot run: %s not found (a machine without the Armory: an environment gap)" % (live / "action_sets.xml"))
        return 2
    problems = clip_problems(str(root), CLIPS, bound_clips())
    for p in problems:
        print("NOT READY %s" % p)
    planned, changed = [], 0
    try:
        for folder in (live, SNAPSHOT):
            roots = {}
            for name in FILES:
                path = folder / name
                was = path.read_bytes().decode("utf-8")
                now = PLANS[name](was)
                roots[name] = ET.fromstring(now.encode("utf-8"))
                planned.append((path, was, now))
                changed += was != now
                print("%-9s %s" % ("change" if was != now else "bound", path))
                if was != now:
                    print("\n".join(differing_lines(was, now)))
            for p in planned_problems(roots):
                raise BindError("%s: %s" % (folder, p))
    except (BindError, ET.ParseError, OSError) as err:
        print("REFUSED: %s" % err)
        return 1
    if a.check:
        ok = not changed and not problems
        print("spider: the three files carry the binding and every clip is cooked" if ok else "spider: NOT bound (%d file(s) differ, %d clip problem(s))" % (changed, len(problems)))
        return 0 if ok else 1
    if not a.apply:
        print("plan: %d file(s) would change, %d clip problem(s); --apply writes" % (changed, len(problems)))
        return 0
    if problems:
        print("REFUSED: a clip is not cooked; start the Modding Kit once with the Armory loaded, save, close, run again")
        return 1
    if not changed:
        print("already applied")
        return 0
    if game_or_kit_running():
        print("REFUSED: the game or the Modding Kit is running; close it and run again")
        return 2
    backup = Path(a.backup_dir or r"E:\LOTRAOMAssets\Spider\bind_backup_%s" % dt.datetime.now().strftime("%Y%m%d-%H%M%S"))
    todo = [(path, now) for path, was, now in planned if was != now]
    made = []
    try:
        for path, now in todo:             # every new file is whole on disk before the first one is moved in
            if live in path.parents:
                backup.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, backup / (path.name + ".before-spider-bind"))
            made.append(path.with_name(path.name + TMP))
            made[-1].write_bytes(now.encode("utf-8"))
    except OSError as err:
        for tmp in made:
            tmp.unlink(missing_ok=True)
        print("cannot run: %s; no file of the module or the snapshot was changed" % err)
        return 2
    try:
        for path, _ in todo:               # FILES order, live first; the header names the one unsafe middle state
            os.replace(path.with_name(path.name + TMP), path)
            print("wrote %s" % path)
    except OSError as err:
        print("STOPPED: %s; run --apply again, the plan is idempotent (--check lists what still differs)" % err)
        return 2
    print("applied; backups of the live files: %s" % backup)
    return 0


if __name__ == "__main__":
    sys.exit(main())
