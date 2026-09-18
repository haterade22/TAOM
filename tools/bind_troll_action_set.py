#!/usr/bin/env python3
"""Bind the anim_troll_* clips into `as_cave_troll_warrior` in the LIVE LOTRLOME_Armory action_sets.xml.

The cave troll runs on human_skeleton with base_set="as_human_warrior", so every act_* code it does not
override plays the human clip. This tool writes the override body of <action_set id="as_cave_troll_warrior">
from the vanilla human set's code list (Native/ModuleData/action_sets.xml, the full definition; the
Armory's own as_human_warrior at its line 15 is only the rider partial), so the code names, their
_left_stance twins and the idle alternative_group values are the engine's, not typed by hand.

Binding rules (clip names from tools/blender/fab_cave_troll_clip_names.json):
  act_walk_forward_*   -> anim_troll_combat_walk1, unarmed variants anim_troll_walk1
  act_run_forward_*    -> anim_troll_run1
  act_idle_*           -> unarmed: anim_troll_idle1; armed: combat_idle1 / combat_idle2 alternating by the
                          code's number, alternative_group kept from vanilla
  act_strike_*         -> anim_troll_combat_hit_<dir>1, dir = the last of front/back/left/right in the code
                          after stripping _left_stance; the knock-down-and-rise, ladder, bent_over and
                          _continue reactions are left on the human clips (no troll equivalent)
  act_death_fall_*     -> back -> anim_troll_death1 (the backward fall), front/left -> death2, front_heavy/
                          right -> death3; _continue and the by_arrow / by_fire / ladder / rider deaths stay human
  the _adder codes (additive overlays), turns, strafes, backward walks, attacks, everything else: inherited from as_human_warrior. Melee attacks are
  engine pose-blends, so the anim_troll_attack* clips are not bound here.

Byte-faithful (tools/README.md XML I/O convention): binary read, BOM and line endings preserved, backup
to action_sets.xml.bak-troll-bind-<stamp> (never a .xml extension: the folder is globbed), the result is
parsed with ElementTree before it is written, and the body between the set's open and close tags is
REPLACED, so re-running is idempotent. Dry run by default; --apply writes.

    python tools/bind_troll_action_set.py            # plan
    python tools/bind_troll_action_set.py --apply
After --apply run  python tools/audit_action_set_parity.py  (root-level <action> kills a dedicated server).
"""
import argparse
import datetime as dt
import os
import re
import sys
import xml.etree.ElementTree as ET

GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"
LIVE = os.path.join(GAME, "LOTRLOME_Armory", "ModuleData", "action_sets.xml")
NATIVE = os.path.join(GAME, "Native", "ModuleData", "action_sets.xml")
SET_ID = "as_cave_troll_warrior"

ACTION_RE = re.compile(r"<action\b([^>]*?)/>", re.S)
ATTR_RE = re.compile(r'(\w+)\s*=\s*"([^"]*)"')
DIRS = ("front", "back", "left", "right")


def human_actions(native_text: str):
    """Ordered list of {attr: value} for every <action/> inside Native's full as_human_warrior."""
    m = re.search(r'<action_set\s+id="as_human_warrior"[^>]*>', native_text)
    if not m:
        raise SystemExit("as_human_warrior not found in " + NATIVE)
    end = native_text.index("</action_set>", m.end())
    out = []
    for am in ACTION_RE.finditer(native_text, m.end(), end):
        out.append(dict(ATTR_RE.findall(am.group(1))))
    return out


def direction(code_stem: str):
    stem = code_stem[:-len("_left_stance")] if code_stem.endswith("_left_stance") else code_stem
    toks = stem.split("_")
    dirs = [t for t in toks if t in DIRS]
    return dirs[-1] if dirs else None


def bind(code: str, attrs: dict):
    """Return the troll clip name for an act_* code, or None to inherit the human clip."""
    if code.endswith("_adder"):
        return None                       # run_adder / walk_adder are additive overlay layers, not gait clips
    if code.startswith("act_walk_forward_"):
        return "anim_troll_walk1" if "unarmed" in code else "anim_troll_combat_walk1"
    if code.startswith("act_run_forward_"):
        return "anim_troll_run1"
    if code.startswith("act_idle_"):
        if "unarmed" in code:
            return "anim_troll_idle1"
        m = re.search(r"_(\d+)(_left_stance)?$", code)
        n = int(m.group(1)) if m else 1
        return "anim_troll_combat_idle1" if n % 2 else "anim_troll_combat_idle2"
    if code.startswith("act_strike_"):
        rest = code[len("act_strike_"):]
        if any(k in rest for k in ("back_rise", "ladder", "bent_over", "continue")):
            return None
        d = direction(rest)
        return "anim_troll_combat_hit_%s1" % d if d else None
    if code.startswith("act_death_fall_"):
        rest = code[len("act_death_fall_"):]
        if "continue" in rest:
            return None
        d = direction(rest)
        heavy = "heavy" in rest
        if d == "back":
            return "anim_troll_death1"
        if d == "front":
            return "anim_troll_death3" if heavy else "anim_troll_death2"
        if d == "left":
            return "anim_troll_death2"
        if d == "right":
            return "anim_troll_death3"
        return None
    return None


def build_body(actions, indent="\t\t"):
    lines = []
    counts = {}
    seen = set()
    for a in actions:
        code = a.get("type")
        if not code or code in seen:
            continue
        clip = bind(code, a)
        if clip is None:
            continue
        seen.add(code)
        extra = ""
        if a.get("alternative_group"):
            extra = ' alternative_group="%s"' % a["alternative_group"]
        lines.append('%s<action type="%s" animation="%s"%s />' % (indent, code, clip, extra))
        counts[clip] = counts.get(clip, 0) + 1
    return lines, counts


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--live", default=LIVE)
    ap.add_argument("--native", default=NATIVE)
    args = ap.parse_args(argv)

    native_text = open(args.native, "rb").read().decode("utf-8-sig")
    actions = human_actions(native_text)
    lines, counts = build_body(actions)
    print("human codes read: %d; troll overrides planned: %d" % (len(actions), len(lines)))
    for clip in sorted(counts):
        print("  %-30s %3d codes" % (clip, counts[clip]))

    raw = open(args.live, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    nl = "\r\n" if "\r\n" in text else "\n"
    open_m = re.search(r'<action_set\s+id="%s"[^>]*>' % re.escape(SET_ID), text)
    if not open_m:
        raise SystemExit("%s not found in %s" % (SET_ID, args.live))
    close_i = text.index("</action_set>", open_m.end())
    old_body = text[open_m.end():close_i]
    old_count = len(ACTION_RE.findall(old_body))
    header = ("%s\t\t<!-- Fab Cave Troll Lightweight clips retargeted onto human_skeleton (2026-09-17/18); GENERATED by"
              "%s\t\t     tools/bind_troll_action_set.py, which owns this body: edit the rules there, not here. Turns, strafes,"
              "%s\t\t     backward walks and attacks inherit as_human_warrior (melee is engine pose-blend). -->" % (nl, nl, nl))
    new_body = header + nl + nl.join(lines) + nl + "\t"
    new_text = text[:open_m.end()] + new_body + text[close_i:]
    if new_text == text:
        print("no change: the set already carries exactly this body")
        return 0
    print("existing overrides in the set: %d -> %d" % (old_count, len(lines)))
    # must still parse
    ET.fromstring(new_text.encode("utf-8"))
    if not args.apply:
        print("DRY RUN: nothing written. Sample:")
        for ln in lines[:4] + ["\t\t..."] + lines[-2:]:
            print(ln)
        return 0
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    bak = args.live + ".bak-troll-bind-" + stamp
    open(bak, "wb").write(raw)
    open(args.live, "wb").write((b"\xef\xbb\xbf" if bom else b"") + new_text.encode("utf-8"))
    # re-read and prove
    check = open(args.live, "rb").read().decode("utf-8-sig")
    ET.fromstring(check.encode("utf-8"))
    om = re.search(r'<action_set\s+id="%s"[^>]*>' % re.escape(SET_ID), check)
    body = check[om.end():check.index("</action_set>", om.end())]
    print("written: %s overrides now in %s; backup %s" % (len(ACTION_RE.findall(body)), SET_ID, os.path.basename(bak)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
