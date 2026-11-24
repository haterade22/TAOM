#!/usr/bin/env python3
"""Bind the warg's and the dire wolf's walk to their own in-place twins (#1565), in the Armory and in the repo's
snapshot of it.

action_sets.xml: in as_warg, act_warg_forward_walk_stand plays warg_walk_stand; in as_dire_wolf it plays
dire_wolf_walk_stand. Each played the record of the walk's front row (warg_walkfast, dire_wolf_walkfast). The
mount's movement node takes a pace's loop time from two Durations, the "none" row's clip at the pace's lower limit
and the front row's at the upper (docs/features/elephant.md, "What the engine does with a clip record"), so one
record on both rows gave the walk one loop time at every speed. The trot, the canter and the gallop always had a
twin. tools/repoint_anim_clip.py makes the two twins from tools/warg_gait_plan.json and
tools/dire_wolf_gait_plan.json. The fell warg plays as_warg too.

THE GATE. The game plays a clip from the RuntimeDataCache entry the Modding Kit cooks, and a clip bound with no
entry crashed Agent.SetActionSet in another modder's test (docs/reference/bannerlord-animation-system-map.md). So
--apply writes nothing unless each twin it would bind is there, holds its plan's values and is cooked as it is now
(repoint_anim_clip.cook_fault). The order: repoint_anim_clip.py --apply for each plan, start the Kit once and close
it, then this.

The rows are edited in the LIVE Armory (unversioned: a reinstall drops them, and --check is the gate that notices)
and in docs/reference/lotrlome-armory-snapshot/action_sets.xml, the same change in each. Byte-faithful: binary read,
BOM and CRLF kept, the result parsed with ElementTree before it is written, one backup <file>.bak-warg-bind-<stamp>
per changed file (never .xml: the folder is globbed). A row on any clip but the old or the new one, a missing row
and a doubled row are REFUSED, never repaired, and one refused file stops the write of the other.
A missing snapshot is a refusal (it is a tracked file), and so is a file that changed between this tool's read and
its write: other sessions edit the live action_sets.xml.
Exit codes: 0 done; 1 a refusal, or with --check a file that does not carry the bindings; 2 not run (no Armory, no
xxhash, or --apply while the game or the Kit runs).

    python tools/bind_warg_clips.py            # plan, writes nothing
    python tools/bind_warg_clips.py --apply
    python tools/bind_warg_clips.py --check    # exit 1 unless both files carry both bindings
Undo: copy the two backups back. The records may stay: an unbound record plays nowhere.
"""
import argparse
import datetime as dt
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import namedtuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import repoint_anim_clip as rp  # noqa: E402
import set_clip_balance_name as scb  # noqa: E402
from _gamedir import game_or_kit_running  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SNAPSHOT = os.path.join(REPO, "docs", "reference", "lotrlome-armory-snapshot")
PLANS = os.path.join(REPO, "tools")
ACTION = "act_warg_forward_walk_stand"
ROW = re.compile(r'(<action\s+type="%s"\s+animation=")([^"]*)(")' % re.escape(ACTION))
# set, the clip the row played (the walk's front record), the twin, the plan that makes the twin, its clips folder
Binding = namedtuple("Binding", "set_id front twin plan under")
BINDINGS = (
    Binding("as_warg", "warg_walkfast", "warg_walk_stand", "warg_gait_plan.json", "creature/warg/animations"),
    Binding("as_dire_wolf", "dire_wolf_walkfast", "dire_wolf_walk_stand", "dire_wolf_gait_plan.json",
            "creature/direwolf/animations"),
)


class Refuse(Exception):
    pass


def _row(text, b):
    """The one row of the set that binds the walk's "none" action, as a regex match."""
    opened = re.search(r'<action_set\s+id="%s"[^>]*>' % re.escape(b.set_id), text)
    if not opened:
        raise Refuse("%s not found" % b.set_id)
    close = text.find("</action_set>", opened.end())
    if close < 0:
        raise Refuse("%s is not closed" % b.set_id)
    rows = list(ROW.finditer(text, opened.end(), close))
    if len(rows) != 1:
        raise Refuse("%s binds %s %d time(s), want once" % (b.set_id, ACTION, len(rows)))
    if rows[0].group(2) not in (b.front, b.twin):
        raise Refuse("%s plays %s in %s, not %s or %s: a hand edit? decide it by hand, then re-run"
                     % (ACTION, rows[0].group(2), b.set_id, b.front, b.twin))
    return rows[0]


def unbound(text):
    """The bindings the text does not carry yet. Refuse on a set or a row this tool does not understand."""
    return [b for b in BINDINGS if _row(text, b).group(2) != b.twin]


def rebind(text):
    """text with each set's walk "none" row on its twin, or text itself when both are there already."""
    for b in unbound(text):
        row = _row(text, b)
        text = text[:row.start(2)] + b.twin + text[row.end(2):]
    return text


def checked(text):
    """rebind(text), parsed and read back as a tree; Refuse or ET.ParseError when it is not what we mean to write."""
    new = rebind(text)
    root = ET.fromstring(new.encode("utf-8"))
    for b in BINDINGS:
        bound = [a.get("animation") for s in root.iter("action_set") if s.get("id") == b.set_id
                 for a in s.findall("action") if a.get("type") == ACTION]
        if bound != [b.twin]:
            raise Refuse("after the edit %s binds %s as %r, want [%r]" % (b.set_id, ACTION, bound, b.twin))
    return new


def twin_fault(module, plans, b):
    """None when the twin is there, holds the plan's values and is cooked as it is now; else why it must not be bound."""
    clips = os.path.join(module, "Assets", *b.under.split("/"))
    plan_path = os.path.join(plans, b.plan)
    with open(plan_path, encoding="utf-8-sig") as fh:
        wanted = [r for r in json.load(fh) if r.get("clip") == b.twin]
    if len(wanted) != 1:
        return "%s names %s %d time(s), want once" % (plan_path, b.twin, len(wanted))
    row = rp.plan(wanted, clips)[0]
    again = "run repoint_anim_clip.py --plan tools/%s --under %s --apply" % (b.plan, b.under)
    if row.buf is None:
        return "%s is not there: %s" % (b.twin, again)
    if row.changed:
        return "%s does not hold the plan's values: %s" % (b.twin, again)
    fault = rp.cook_fault(module, row.buf, os.path.join(clips, row.master + "_geo.tpac"))
    return fault and "%s: %s. Start the Modding Kit once with the Armory loaded, then close it" % (b.twin, fault)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true")
    mode.add_argument("--check", action="store_true")
    ap.add_argument("--armory", default=scb.MODULE, help="the module root, which holds ModuleData/ and Assets/")
    ap.add_argument("--snapshot", default=SNAPSHOT)
    ap.add_argument("--plans", default=PLANS, help="the folder that holds the two gait plans")
    args = ap.parse_args(argv)

    live = os.path.join(args.armory, "ModuleData", "action_sets.xml")
    snapshot = os.path.join(args.snapshot, "action_sets.xml")
    bad = 0
    results, owed = {}, []
    for path in (live, snapshot):
        if not os.path.isfile(path):
            if path == snapshot:            # a tracked file of the repo: its absence is a fault, never "not run"
                print("REFUSED  %s: not there" % path)
                bad += 1
            continue
        with open(path, "rb") as fh:
            raw = fh.read()
        try:
            text = raw.decode("utf-8")      # a BOM stays inside the string
            results[path] = (raw, text, checked(text))
            owed += [b for b in unbound(text) if b not in owed]
        except (Refuse, ET.ParseError, UnicodeDecodeError) as exc:
            print("REFUSED  %s: %s" % (path, exc))
            bad += 1
            continue
        print("%-8s %s" % ("ok" if results[path][2] == text else "PLAN", path))
    todo = [p for p, (_, text, new) in results.items() if new != text]
    if args.check:
        for path in todo:
            print("check: %s does not bind %s to %s" % (
                path, ACTION, " and ".join(b.twin for b in unbound(results[path][1]))))
        if bad or todo:
            return 1
        if not os.path.isfile(live):
            print("NOT RUN: no %s (the snapshot carries the bindings)" % live)
            return 2
        return 0
    if bad:
        return 1
    if not os.path.isfile(live):
        print("NOT RUN: no %s" % live)
        return 2
    faults = []
    for b in owed:
        try:
            fault = twin_fault(args.armory, args.plans, b)
        except ModuleNotFoundError as exc:
            if exc.name != "xxhash":
                raise
            print("NOT RUN: %s (the item checksum needs xxhash)" % exc)
            return 2
        except (OSError, ValueError, rp.RepointError) as exc:
            fault = "%s: its plan could not be read against the Armory: %s" % (b.twin, exc)
        if fault:
            faults.append(fault)
    for fault in faults:
        print("REFUSED: %s" % fault)
    if faults:
        return 1
    if not args.apply:
        print("DRY RUN: %d file(s) would change; nothing written" % len(todo))
        return 0
    if todo and game_or_kit_running():
        print("REFUSED: the game or the Modding Kit is running; close it first", file=sys.stderr)
        return 2
    # Other sessions edit the live file. Each write is built from the bytes read above, so a file that changed
    # since then is refused before anything is written: its new content would be lost, from the backup too.
    for path in todo:
        with open(path, "rb") as fh:
            if fh.read() != results[path][0]:
                print("REFUSED: %s changed since it was read; nothing is written. Run this again" % path)
                return 1
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    for path in todo:
        raw, _, new = results[path]
        with open(path + ".bak-warg-bind-" + stamp, "xb") as fh:
            fh.write(raw)
        with open(path, "wb") as fh:
            fh.write(new.encode("utf-8"))
        with open(path, "rb") as fh:
            again = fh.read().decode("utf-8")
        if checked(again) != again:
            with open(path, "wb") as fh:
                fh.write(raw)
            print("WRITE FAILED %s: the bindings were not there after the write; the file is put back" % path)
            return 1
        print("written  %s (backup .bak-warg-bind-%s)" % (path, stamp))
    return 0


if __name__ == "__main__":
    sys.exit(main())
