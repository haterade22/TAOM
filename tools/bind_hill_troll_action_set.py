#!/usr/bin/env python3
"""Write the body of the STANDALONE `as_hill_troll_warrior` in the LIVE LOTRLOME_Armory action_sets.xml.

The hill troll runs on its own `troll_skeleton_a` with no base_set, so the set must list every act_* code
itself. This tool regenerates the whole body from the vanilla human set's code list (Native/ModuleData/
action_sets.xml, the full definition: code names, their _left_stance twins and alternative_group values are the
engine's, not typed by hand), binding each code by the first rule that has a clip on disk:

  1. the Fab clip the cave troll rules choose (tools/bind_troll_action_set.py: walk_forward, run_forward, idle,
     strike, death_fall), renamed anim_troll_* -> anim_hill_troll_* (the hill troll's 52, from
     tools/blender/fab_hill_troll_clip_names.json): the troll's own gait, idles, hit reactions and deaths;
  2. the human clip retargeted onto the troll, anim_hill_troll_<vanilla clip>, when a --clips-index lists it
     (read_anim_keyframes_tpac.ps1 -ByClip writes one per extraction; gen_troll_anim_clips.ps1 -CloneByName
     makes the clips): two-handed attacks, blocks, guards, staggers, falls, jumps, kicks;
  3. the human clip itself, inherited, for everything else (swimming, ladders, cutscenes): it plays on the
     re-framed rig with the human's rest relations, head up and wrists twisted, so a code that matters gets
     its master extracted and retargeted rather than left here.

Byte-faithful (tools/README.md XML I/O convention): binary read, BOM and line endings preserved, backup to
action_sets.xml.bak-hilltroll-bind-<stamp> (never a .xml extension: the folder is globbed), the result parsed
with ElementTree before it is written, and only the body between this set's open and close tags is replaced,
so re-running is idempotent and the cave troll's set (another session's) is untouched. Dry run by default;
--apply writes, and refuses while the game or the Kit runs.

    python tools/bind_hill_troll_action_set.py --clips-index <human_json>/clips_index.json [--clips-index ...]
    python tools/bind_hill_troll_action_set.py --clips-index ... --apply
After --apply run  python tools/audit_action_set_parity.py  (the set must keep every Native code).
"""
import argparse
import datetime as dt
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bind_troll_action_set as cave  # noqa: E402  (the cave troll's rules, reused read-only)

GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"
LIVE = os.path.join(GAME, "LOTRLOME_Armory", "ModuleData", "action_sets.xml")
NATIVE = os.path.join(GAME, "Native", "ModuleData", "action_sets.xml")
SET_ID = "as_hill_troll_warrior"
PREFIX = "anim_hill_troll_"
CAVE_PREFIX = "anim_troll_"
FAB_NAMES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "blender", "fab_hill_troll_clip_names.json")

ACTION_RE = cave.ACTION_RE
ATTR_RE = cave.ATTR_RE


def human_actions(native_text):
    """Every ACTIVE <action/> node of Native's as_human_warrior, in order, as {attr: value}. Comments are stripped
    first: TaleWorlds disables actions by commenting them out, and a regex that reads through them binds 124 dead
    codes (4,824 against the 4,700 the parity tool wrote). Alternative-group twins of one type are separate nodes
    and all kept, as the parity tool keeps them."""
    m = re.search(r'<action_set\s+id="as_human_warrior"[^>]*>', native_text)
    if not m:
        raise SystemExit("as_human_warrior not found in Native action_sets.xml")
    end = native_text.index("</action_set>", m.end())
    block = re.sub(r"<!--.*?-->", "", native_text[m.end():end], flags=re.S)
    return [dict(ATTR_RE.findall(am.group(1))) for am in ACTION_RE.finditer(block)]


def available_clips(index_paths, fab_names_path):
    """(human, fab): the anim_hill_troll_* names the clip indexes promise and the Fab name map defines."""
    human = set()
    for p in index_paths:
        with open(p, encoding="utf-8-sig") as fh:
            human.update(PREFIX + k for k in json.load(fh))
    with open(fab_names_path, encoding="utf-8-sig") as fh:
        fab = {v for k, v in json.load(fh).items() if not k.startswith("_")}
    return human, fab


def bind_hill(code, attrs, human_clips, fab_clips):
    """(clip, source) for an act_* code: source is 'fab', 'human' or 'inherited'."""
    fab = cave.bind(code, attrs)
    if fab:
        name = PREFIX + fab[len(CAVE_PREFIX):] if fab.startswith(CAVE_PREFIX) else fab
        if name in fab_clips:
            return name, "fab"
    vanilla = attrs.get("animation", "")
    if vanilla and PREFIX + vanilla in human_clips:
        return PREFIX + vanilla, "human"
    return vanilla, "inherited"


def build_body(actions, human_clips, fab_clips, indent="\t\t"):
    """One <action/> line per active Native node (alternative-group twins included), its other attributes
    verbatim, animation rebound."""
    lines, counts = [], {}
    for a in actions:
        code = a.get("type")
        if not code:
            continue
        clip, source = bind_hill(code, a, human_clips, fab_clips)
        attrs = ['type="%s"' % code, 'animation="%s"' % clip]
        attrs += ['%s="%s"' % (k, v) for k, v in a.items() if k not in ("type", "animation")]
        lines.append("%s<action %s />" % (indent, " ".join(attrs)))
        counts[source] = counts.get(source, 0) + 1
    return lines, counts


def replace_body(text, lines, nl):
    """The text with SET_ID's body replaced (header comment + lines); returns (new_text, old action count)."""
    open_m = re.search(r'<action_set\s+id="%s"[^>]*>' % re.escape(SET_ID), text)
    if not open_m:
        raise SystemExit("%s not found" % SET_ID)
    close_i = text.index("</action_set>", open_m.end())
    old_body = text[open_m.end():close_i]
    header = ("%s\t\t<!-- Standalone set on troll_skeleton_a: every as_human_warrior code, GENERATED by"
              "%s\t\t     tools/bind_hill_troll_action_set.py, which owns this body: edit the rules there, not here."
              "%s\t\t     Fab clips for gait, idles, hits and deaths; human clips retargeted onto the troll where a"
              "%s\t\t     clips_index lists them; the human clip itself (bends the hunched rest wrong) for the rest. -->"
              % (nl, nl, nl, nl))
    new_body = header + nl + nl.join(lines) + nl + "\t"
    return text[:open_m.end()] + new_body + text[close_i:], len(ACTION_RE.findall(old_body))


def game_or_kit_running():
    try:
        out = subprocess.run(["tasklist"], capture_output=True, text=True, timeout=30).stdout.lower()
    except Exception:
        return False
    return "taleworlds" in out or "bannerlord" in out


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--live", default=LIVE)
    ap.add_argument("--native", default=NATIVE)
    ap.add_argument("--clips-index", action="append", default=[],
                    help="clips_index.json from read_anim_keyframes_tpac.ps1 -ByClip (repeatable); its keys are the "
                         "vanilla clips retargeted as anim_hill_troll_<clip>")
    ap.add_argument("--fab-names", default=FAB_NAMES, help="the hill troll's Fab clip name map")
    args = ap.parse_args(argv)

    human_clips, fab_clips = available_clips(args.clips_index, args.fab_names)
    native_text = open(args.native, "rb").read().decode("utf-8-sig")
    actions = human_actions(native_text)
    lines, counts = build_body(actions, human_clips, fab_clips)
    print("human codes read: %d; set body planned: %d codes  (%s)" % (
        len(actions), len(lines), ", ".join("%s %d" % (k, counts[k]) for k in ("fab", "human", "inherited") if k in counts)))

    raw = open(args.live, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    nl = "\r\n" if "\r\n" in text else "\n"
    new_text, old_count = replace_body(text, lines, nl)
    if new_text == text:
        print("no change: the set already carries exactly this body")
        return 0
    print("actions in the set: %d -> %d" % (old_count, len(lines)))
    ET.fromstring(new_text.encode("utf-8"))
    if not args.apply:
        print("DRY RUN: nothing written. Sample:")
        for ln in lines[:3] + ["\t\t..."] + lines[-2:]:
            print(ln)
        return 0
    if game_or_kit_running():
        print("REFUSED: the game or the Modding Kit is running; close it first", file=sys.stderr)
        return 2
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    bak = args.live + ".bak-hilltroll-bind-" + stamp
    open(bak, "wb").write(raw)
    open(args.live, "wb").write((b"\xef\xbb\xbf" if bom else b"") + new_text.encode("utf-8"))
    check = open(args.live, "rb").read().decode("utf-8-sig")
    ET.fromstring(check.encode("utf-8"))
    om = re.search(r'<action_set\s+id="%s"[^>]*>' % re.escape(SET_ID), check)
    body = check[om.end():check.index("</action_set>", om.end())]
    print("written: %d actions now in %s; backup %s" % (len(ACTION_RE.findall(body)), SET_ID, os.path.basename(bak)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
