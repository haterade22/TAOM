#!/usr/bin/env python3
"""Declare and bind the two dwarf attack actions (#1087) in the Armory and in the repo's snapshot of it.

action_types.xml: act_dwarf_head_butt and act_dwarf_drop_kick, typed actt_kick (the vanilla kick's type), after
act_troll_brute_force. The type's one managed consumer is StandingPoint.DoesActionTypeStopUsingGameObject: a kick, a
jump or a weapon bash makes the agent stop using a machine, so a dwarf that starts an attack lets go of it. What
the native engine does with the type beyond that is UNVERIFIED (the in-game check in docs/features/dwarf-attacks.md).

action_sets.xml: both rows at the end of as_dwarf_warrior (skeleton dwarf_skeleton_a), on the clips
tools/gen_dwarf_attack_clips.ps1 writes. The female, hideout and villager dwarf sets inherit it through base_set,
so one binding covers them (tavern, child and map sets included; only the "dwarf" Monster runs the C# tree that
plays the clips).

Both files are edited in the LIVE Armory (LOTRLOME_Armory/ModuleData, unversioned: a reinstall drops the edit, and
--check is the gate that notices) and in docs/reference/lotrlome-armory-snapshot/, the same insertion in each.
Byte-faithful (tools/README.md XML I/O convention, idiom B): binary read, BOM and CRLF kept, the result parsed with
ElementTree before it is written, a backup <file>.bak-dwarf-attacks-<stamp> (never .xml: the folder is globbed).
Every planned output is parsed and checked as a tree (one declaration per action, one binding per action inside
as_dwarf_warrior, no root-level <action>) before anything is written, in the dry run and --check too. --apply
refuses (exit 2) while the game or the Modding Kit runs.
Idempotent: a file that already carries the rows is left alone; a file with only one of them, a wrong type or clip,
or a row outside as_dwarf_warrior is REFUSED, never repaired.

    python tools/bind_dwarf_attack_actions.py           # plan, writes nothing
    python tools/bind_dwarf_attack_actions.py --apply
    python tools/bind_dwarf_attack_actions.py --check   # exit 1 unless all four files carry the rows
After --apply run  python tools/audit_action_set_parity.py  and  python tools/validate_moduledata.py.
"""
import argparse
import datetime as dt
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_or_kit_running  # noqa: E402

GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"
ARMORY = os.path.join(GAME, "LOTRLOME_Armory", "ModuleData")
SNAPSHOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                        "docs", "reference", "lotrlome-armory-snapshot")
FILES = ("action_types.xml", "action_sets.xml")
SET_ID = "as_dwarf_warrior"
ACTION_TYPE = "actt_kick"
ANCHOR = '<action name="act_troll_brute_force" />'
# action -> clip (the clips are named after their masters, gen_dwarf_attack_clips.ps1)
ACTIONS = (("act_dwarf_head_butt", "dwarf_head_butt"), ("act_dwarf_drop_kick", "dwarf_drop_kick"))


class Refuse(Exception):
    pass


def _nl(text):
    crlf = text.count("\r\n")
    return "\r\n" if crlf > text.count("\n") - crlf else "\n"   # majority, tools/README.md XML I/O convention


def plan_types(text):
    """text with both action declarations, or text itself when they are already there exactly."""
    found = {}
    for m in re.finditer(r'<action\s+name="(act_dwarf_[a-z_]+)"([^>]*)/>', text):
        found.setdefault(m.group(1), []).append(m.group(2))
    want = [a for a, _ in ACTIONS]
    for name, attrs in found.items():
        if name not in want:
            continue
        if len(attrs) != 1 or re.search(r'\btype="%s"' % ACTION_TYPE, attrs[0]) is None:
            raise Refuse("action_types: %s is declared %d time(s) as %r, want once with type %s"
                         % (name, len(attrs), attrs, ACTION_TYPE))
    have = [a for a in want if a in found]
    if len(have) == len(want):
        return text
    if have:
        raise Refuse("action_types: only %s is declared; a hand edit? fix it by hand, then re-run" % have)
    i = text.find(ANCHOR)
    if i < 0:
        raise Refuse("action_types: anchor %s not found" % ANCHOR)
    nl = _nl(text)
    eol = text.index(nl, i) + len(nl)
    block = ("\t<!-- #1087 dwarf head butt and drop kick, played by TAOM Main/Features/DwarfAttacks from its behaviour tree."
             "%s\t     actt_kick, the vanilla kick's type (StandingPoint lets go of a machine on it). Clips from"
             "%s\t     tools/gen_dwarf_attack_clips.ps1; bound in as_dwarf_warrior by tools/bind_dwarf_attack_actions.py. -->%s"
             % (nl, nl, nl))
    block += "".join('\t<action name="%s" type="%s" />%s' % (a, ACTION_TYPE, nl) for a, _ in ACTIONS)
    return text[:eol] + block + text[eol:]


def _set_span(text):
    m = re.search(r'<action_set\s+id="%s"[^>]*>' % re.escape(SET_ID), text)
    if not m:
        raise Refuse("action_sets: %s not found" % SET_ID)
    return m.end(), text.index("</action_set>", m.end())


def plan_sets(text):
    """text with both rows at the end of as_dwarf_warrior, or text itself when they are already there exactly."""
    start, close = _set_span(text)
    body = text[start:close]
    rows = {}
    for a, clip in ACTIONS:
        anywhere = len(re.findall(r'type="%s"' % a, text))
        here = re.findall(r'<action\s+type="%s"\s+animation="([^"]*)"\s*/>' % a, body)
        if anywhere != len(here):
            raise Refuse("action_sets: %s appears outside %s" % (a, SET_ID))
        if len(here) > 1 or (here and here[0] != clip):
            raise Refuse("action_sets: %s is bound to %r in %s, want %s once" % (a, here, SET_ID, clip))
        rows[a] = bool(here)
    if all(rows.values()):
        return text
    if any(rows.values()):
        raise Refuse("action_sets: only %s is bound; a hand edit? fix it by hand, then re-run"
                     % [a for a, v in rows.items() if v])
    nl = _nl(text)
    line_start = text.rindex(nl, 0, close) + len(nl)   # the closing tag's own line, so its indent stays
    block = ("\t\t<!-- #1087 dwarf attacks (TAOM Main/Features/DwarfAttacks); written by tools/bind_dwarf_attack_actions.py -->%s"
             % nl)
    block += "".join('\t\t<action type="%s" animation="%s" />%s' % (a, clip, nl) for a, clip in ACTIONS)
    return text[:line_start] + block + text[line_start:]


def validate_types(root):
    for name, _ in ACTIONS:
        hits = [a for a in root.iter("action") if a.get("name") == name]
        if len(hits) != 1 or hits[0].get("type") != ACTION_TYPE:
            raise Refuse("action_types: %s is declared %d time(s) (types %r), want once with type %s"
                         % (name, len(hits), [h.get("type") for h in hits], ACTION_TYPE))


def validate_sets(root):
    loose = root.findall("action")
    if loose:
        raise Refuse("action_sets: %d <action> element(s) directly under the root, outside any set" % len(loose))
    for name, clip in ACTIONS:
        hits = [(s.get("id"), a.get("animation")) for s in root.iter("action_set")
                for a in s.findall("action") if a.get("type") == name]
        if hits != [(SET_ID, clip)]:
            raise Refuse("action_sets: %s is bound %r, want exactly [(%r, %r)]" % (name, hits, SET_ID, clip))


PLAN = {"action_types.xml": plan_types, "action_sets.xml": plan_sets}
VALIDATE = {"action_types.xml": validate_types, "action_sets.xml": validate_sets}


def plan_checked(name, text):
    """The planned text, parsed and checked as a tree; Refuse or ET.ParseError when it is not what we mean to write."""
    new = PLAN[name](text)
    VALIDATE[name](ET.fromstring(new.encode("utf-8")))
    return new


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true")
    mode.add_argument("--check", action="store_true")
    ap.add_argument("--armory", default=ARMORY)
    ap.add_argument("--snapshot", default=SNAPSHOT)
    args = ap.parse_args(argv)

    bad = 0
    results = {}
    for root in (args.armory, args.snapshot):
        for name in FILES:
            path = os.path.join(root, name)
            if not os.path.isfile(path):
                print("MISSING  %s" % path)
                bad += 1
                continue
            raw = open(path, "rb").read()
            text = raw.decode("utf-8")   # idiom B: a BOM stays inside the string
            try:
                new = plan_checked(name, text)
            except (Refuse, ET.ParseError) as e:
                print("REFUSED  %s: %s" % (path, e))
                bad += 1
                continue
            results[path] = (raw, text, new)
            print("%-8s %s" % ("ok" if new == text else "PLAN", path))

    if args.check:
        missing = sum(1 for _, t, n in results.values() if n != t)
        print("check: %d file(s) carry the rows, %d lack them, %d refused or missing" % (len(results) - missing, missing, bad))
        return 1 if bad or missing else 0
    if bad:
        return 1
    if not args.apply:
        print("DRY RUN: nothing written")
        return 0
    if game_or_kit_running():
        print("REFUSED: the game or the Modding Kit is running; close it first", file=sys.stderr)
        return 2
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    for path, (raw, text, new) in results.items():
        if new == text:
            continue
        open(path + ".bak-dwarf-attacks-" + stamp, "wb").write(raw)
        open(path, "wb").write(new.encode("utf-8"))
        again = open(path, "rb").read().decode("utf-8")
        if plan_checked(os.path.basename(path), again) != again:
            print("WRITE FAILED %s: the rows are not there after the write" % path)
            return 1
        print("written  %s (backup .bak-dwarf-attacks-%s)" % (path, stamp))
    return 0


if __name__ == "__main__":
    sys.exit(main())
