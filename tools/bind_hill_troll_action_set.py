#!/usr/bin/env python3
"""Write the body of the STANDALONE `as_hill_troll_warrior` in the LIVE LOTRLOME_Armory action_sets.xml.

The hill troll runs on its own `troll_skeleton_a` with no base_set, so the set must list every act_* code
itself. This tool regenerates the whole body from the vanilla human set's code list (Native/ModuleData/
action_sets.xml, the full definition: code names, their _left_stance twins and alternative_group values are the
engine's, not typed by hand), binding each code by the first rule that has a clip on disk:

  0. a melee attack-table code (releases, quick releases, blocked and quick blocked: MELEE_TABLE) plays the troll
     clip only when that clip is SELF-KEYED, and the vanilla clip otherwise. The engine keeps per-clip attack data in
     a map filled when a clip's package loads, keyed by clip index, and a clip gets a row only when its "Blends with
     animation" (the Modding Kit's clip inspector box; in-memory +0xD8) holds its own name, or through the ten blend
     children the Kit generates toward a "_balanced" twin (TaleWorlds.Native.dll 0x58BEA0 -> 0x5682B0; 175 vanilla
     clips are self-keyed, 107 twin-keyed). Every as_human_warrior action bound to a keyed clip (296) is in these four
     families. The generated troll clips had the box empty, so the lookup missed and read a null row (+0x6590B9,
     reading 0x8; keys 6511 and 6462 in the dumps were anim_hill_troll_release_overswing_2h and a quick-release twin,
     2026-09-25). tools/set_clip_balance_name.py self-keys a clip (own name in the box, "Blends with action" empty,
     the shape the Kit writes), and this rule reads the package (set_clip_balance_name.keyed_clips) so an unkeyed
     clip can never reach a table code. The "_balanced" codes stay vanilla: vanilla's own _balanced clips have no
     row either. Wind-ups, defends, guards, kicks, bashes and parries never reach that table and keep rule 2;
  1. the Fab clip the cave troll rules choose (tools/bind_troll_action_set.py: walk_forward, run_forward, idle,
     strike, death_fall), renamed anim_troll_* -> anim_hill_troll_* (the hill troll's 52, from
     tools/blender/fab_hill_troll_clip_names.json): the troll's own gait, idles, hit reactions and deaths;
  2. the human clip retargeted onto the troll, anim_hill_troll_<vanilla clip>, when a --clips-index lists it
     (read_anim_keyframes_tpac.ps1 -ByClip writes one per extraction; gen_troll_anim_clips.ps1 -CloneByName
     makes the clips): two-handed attacks, blocks, guards, staggers, falls, jumps, kicks;
  3. one of the troll's own Fab idles, REUSED, for the codes that would otherwise show the human clip where a
     player looks at the troll standing still: the party-screen and encyclopedia idle (act_inventory_idle*) and
     the map-conversation idles (act_conversation_*) play its relaxed idle, and the victory cheers (act_cheer*)
     alternate its two combat idles. Mike, 2026-09-25: reuse what exists, author nothing new for these;
  4. the human clip itself, inherited, for everything else: it plays on the re-framed rig with the human's rest
     relations, head up and wrists twisted (swimming, ladders, cutscenes, crafting).
The bodyguard pose set as_hill_troll_poses gets the same idle for its one override, act_stand_1 (POSE_BINDINGS).
The 83 other as_hill_troll_* sets are not rewritten; 48 of them carry their own human-clip overrides.

Byte-faithful (tools/README.md XML I/O convention): binary read, BOM and line endings preserved, backup to
action_sets.xml.bak-hilltroll-bind-<stamp> (never a .xml extension: the folder is globbed), the result parsed
with ElementTree before it is written, and only the body between this set's open and close tags and the pose
set's bound clip names are replaced, so re-running is idempotent and the cave troll's set (another session's)
is untouched. A code is bound to a clip only if its <clip>_anm.tpac is in --clips-dir, which defaults to the
hill troll's animations folder in the install and must exist and hold at least one Fab clip (an empty folder would
unbind every troll clip); at least one --clips-index is required for the same reason. Dry run by default; --apply
writes, and refuses while the game or the Kit runs, or when the process list cannot be read.

    python tools/bind_hill_troll_action_set.py --clips-index <human_json>/clips_index.json [--clips-index ...]
    python tools/bind_hill_troll_action_set.py --clips-index ... --apply
After --apply run  python tools/audit_action_set_parity.py  (the set must keep every Native code).
"""
import argparse
import datetime as dt
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bind_troll_action_set as cave  # noqa: E402  (the cave troll's rules, reused read-only)
import set_clip_balance_name as scbn  # noqa: E402  (which clips are self-keyed: rule 0)
from _gamedir import game_dir, game_or_kit_running  # noqa: E402

GAME = os.path.join(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"), "Modules")
LIVE = os.path.join(GAME, "LOTRLOME_Armory", "ModuleData", "action_sets.xml")
NATIVE = os.path.join(GAME, "Native", "ModuleData", "action_sets.xml")
CLIPS_DIR = os.path.join(GAME, "LOTRLOME_Armory", "Assets", "Race Test", "Mordor", "Trolls", "animations")
SET_ID = "as_hill_troll_warrior"
POSES_ID = "as_hill_troll_poses"
PREFIX = "anim_hill_troll_"
CAVE_PREFIX = "anim_troll_"
FAB_NAMES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "blender", "fab_hill_troll_clip_names.json")
# an AnimationClip's Name is a fixed-size(64) engine string: the vanilla clip names that would run past 63 characters
# as anim_hill_troll_<clip> carry shorter troll names here, the same file gen_troll_anim_clips.ps1 -Renames reads
RENAMES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "blender", "hill_troll_clip_renames.json")


def load_renames(path):
    if not path:
        return {}
    with open(path, encoding="utf-8-sig") as fh:
        return {k: v for k, v in json.load(fh).items() if not k.startswith("_")}


def human_clip_name(vanilla, renames=None):
    """The troll clip name a retargeted vanilla clip carries."""
    return (renames or {}).get(vanilla) or PREFIX + vanilla

ACTION_RE = cave.ACTION_RE
ATTR_RE = cave.ATTR_RE


# TAOM's own actions, declared in the Armory's action_types.xml and absent from as_human_warrior: appended after the
# Native nodes, each bound only if its clip is on disk. act_troll_brute_force is the Brute Force smash the troll
# behaviour tree plays (#649); the hill troll swings the Fab heavy attack retargeted onto its own rig.
EXTRA_BINDINGS = (("act_troll_brute_force", "anim_hill_troll_attack1"),)

# Rule 0: the codes whose clips the engine looks up in its melee attack table; the troll clip only when self-keyed.
MELEE_TABLE = re.compile(r"^act_(quick_)?(release|blocked)_")

# Rule 3: codes that reuse one of the troll's own idles, tried after the Fab and retargeted-human rules so a clip
# authored later for one of these codes still wins. A numbered code picks from its tuple by number, so cheer_1 and
# cheer_2 play different idles.
REUSE = (
    (re.compile(r"^act_(inventory_idle|conversation_)"), ("anim_hill_troll_idle1",)),
    (re.compile(r"^act_cheer"), ("anim_hill_troll_combat_idle1", "anim_hill_troll_combat_idle2")),
)
# The overrides as_hill_troll_poses carries over the warrior set, rebound in place to the troll's idle.
POSE_BINDINGS = (("act_stand_1", "anim_hill_troll_idle1"),)


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


def available_clips(index_paths, fab_names_path, clips_dir=None, renames=None):
    """(human, fab): the anim_hill_troll_* names the clip indexes promise (through the renames) and the Fab name map
    defines. With clips_dir, a human clip counts only if its `<name>_anm.tpac` is on disk there: the generator
    refuses a clip whose vanilla range runs past its master (aserai_mp_guard_idle_2hperk, 2026-09-25), and a code
    bound to a clip that was never written plays nothing."""
    human = set()
    for p in index_paths:
        with open(p, encoding="utf-8-sig") as fh:
            human.update(human_clip_name(k, renames) for k in json.load(fh))
    with open(fab_names_path, encoding="utf-8-sig") as fh:
        fab = {v for k, v in json.load(fh).items() if not k.startswith("_")}
    if clips_dir:
        on_disk = {f[:-len("_anm.tpac")] for f in os.listdir(clips_dir) if f.endswith("_anm.tpac")}
        human &= on_disk
        fab &= on_disk
    return human, fab


def reuse_clip(code, fab_clips):
    """The troll idle a REUSE rule gives this code, or None when no rule matches or its clip is not available."""
    for pattern, clips in REUSE:
        if pattern.match(code):
            number = re.search(r"(\d+)$", code)
            clip = clips[(int(number.group(1)) - 1) % len(clips)] if number else clips[0]
            return clip if clip in fab_clips else None
    return None


def bind_hill(code, attrs, human_clips, fab_clips, renames=None, keyed=frozenset()):
    """(clip, source) for an act_* code: source is 'melee-keyed', 'melee-table', 'fab', 'human', 'reuse' or
    'inherited'. `keyed` is the self-keyed troll clips on disk (set_clip_balance_name.keyed_clips)."""
    if MELEE_TABLE.match(code):
        vanilla = attrs.get("animation", "")
        troll = human_clip_name(vanilla, renames) if vanilla else ""
        if troll in keyed and troll in human_clips:
            return troll, "melee-keyed"
        return vanilla, "melee-table"
    fab = cave.bind(code, attrs)
    if fab:
        name = PREFIX + fab[len(CAVE_PREFIX):] if fab.startswith(CAVE_PREFIX) else fab
        if name in fab_clips:
            return name, "fab"
    vanilla = attrs.get("animation", "")
    if vanilla:
        name = human_clip_name(vanilla, renames)
        if name in human_clips:
            return name, "human"
    reused = reuse_clip(code, fab_clips)
    if reused:
        return reused, "reuse"
    return vanilla, "inherited"


def build_body(actions, human_clips, fab_clips, indent="\t\t", renames=None, keyed=frozenset()):
    """One <action/> line per active Native node (alternative-group twins included), its other attributes
    verbatim, animation rebound."""
    lines, counts = [], {}
    for a in actions:
        code = a.get("type")
        if not code:
            continue
        clip, source = bind_hill(code, a, human_clips, fab_clips, renames, keyed)
        attrs = ['type="%s"' % code, 'animation="%s"' % clip]
        attrs += ['%s="%s"' % (k, v) for k, v in a.items() if k not in ("type", "animation")]
        lines.append("%s<action %s />" % (indent, " ".join(attrs)))
        counts[source] = counts.get(source, 0) + 1
    native_codes = {a.get("type") for a in actions}
    for code, clip in EXTRA_BINDINGS:
        if code in native_codes:
            raise SystemExit("%s is a Native code; an EXTRA_BINDINGS entry must be TAOM's own" % code)
        if clip in fab_clips or clip in human_clips:
            lines.append('%s<action type="%s" animation="%s" />' % (indent, code, clip))
            counts["extra"] = counts.get("extra", 0) + 1
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
              "%s\t\t     clips_index lists them; the troll's idles reused for inventory, conversation and cheers;"
              "%s\t\t     the human clip itself (bends the hunched rest wrong) for the rest."
              "%s\t\t     The release, quick release, blocked and quick blocked codes keep the vanilla clip unless"
              " the troll clip is self-keyed"
              "%s\t\t     (its Blends with animation holds its own name: tools/set_clip_balance_name.py): a troll clip"
              " with no melee attack table row crashes the first swing. -->"
              % (nl, nl, nl, nl, nl, nl, nl))
    new_body = header + nl + nl.join(lines) + nl + "\t"
    return text[:open_m.end()] + new_body + text[close_i:], len(ACTION_RE.findall(old_body))


def rebind_poses(text, fab_clips):
    """The text with each POSE_BINDINGS code in POSES_ID pointed at its troll clip when that clip is available;
    returns (new_text, rebound count). Only the animation value inside the matching node changes, so the set's
    layout and every other node are kept byte for byte."""
    open_m = re.search(r'<action_set\s+id="%s"[^>]*>' % re.escape(POSES_ID), text)
    if not open_m:
        raise SystemExit("%s not found" % POSES_ID)
    close_i = text.index("</action_set>", open_m.end())
    body, rebound = text[open_m.end():close_i], 0
    for code, clip in POSE_BINDINGS:
        if clip not in fab_clips:
            continue
        for am in ACTION_RE.finditer(body):
            if dict(ATTR_RE.findall(am.group(1))).get("type") != code:
                continue
            node = am.group(0)
            new_node = re.sub(r'(\banimation\s*=\s*")[^"]*(")', r"\g<1>%s\g<2>" % clip, node, count=1)
            if new_node != node:
                body = body[:am.start()] + new_node + body[am.end():]
                rebound += 1
            break
    return text[:open_m.end()] + body + text[close_i:], rebound


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--live", default=LIVE)
    ap.add_argument("--native", default=NATIVE)
    ap.add_argument("--clips-index", action="append", required=True,
                    help="clips_index.json from read_anim_keyframes_tpac.ps1 -ByClip (repeatable, at least one); its "
                         "keys are the vanilla clips retargeted as anim_hill_troll_<clip>. Required: without one every "
                         "retargeted clip would unbind in a run that reads as clean")
    ap.add_argument("--fab-names", default=FAB_NAMES, help="the hill troll's Fab clip name map")
    ap.add_argument("--clips-dir", default=CLIPS_DIR,
                    help="the folder holding the <clip>_anm.tpac files; a clip is bound only if it is on disk there "
                         "(default: the hill troll's animations folder in the install)")
    ap.add_argument("--renames", default=RENAMES,
                    help="JSON of vanilla clip name to shorter troll clip name for names over the engine's 63 characters "
                         "(the generator's -Renames file); '' for none")
    args = ap.parse_args(argv)

    if not os.path.isdir(args.clips_dir):
        # without the folder every code would fall back to the human clip and read as a clean run
        print("ERROR: clips folder not found: %s (pass --clips-dir)" % args.clips_dir, file=sys.stderr)
        return 2
    renames = load_renames(args.renames)
    human_clips, fab_clips = available_clips(args.clips_index, args.fab_names, args.clips_dir, renames)
    if not fab_clips:
        # an empty or wrong folder would unbind every troll clip and put the whole set back on the human clips
        print("ERROR: no Fab anim_hill_troll_* clip (<clip>_anm.tpac from %s) in %s; refusing to rebind the set "
              "(pass the hill troll's animations folder as --clips-dir)" % (os.path.basename(args.fab_names),
                                                                             args.clips_dir), file=sys.stderr)
        return 2
    for p in args.clips_index:
        with open(p, encoding="utf-8-sig") as fh:
            listed = {human_clip_name(k, renames) for k in json.load(fh)}
        print("clips index %s: %d listed, %d on disk" % (p, len(listed), len(listed & human_clips)))
    if not human_clips:
        # an index for another folder or skeleton would unbind every retargeted clip in a run that reads as clean
        print("ERROR: no retargeted clip the --clips-index lists is on disk in %s; refusing to rebind the set"
              % args.clips_dir, file=sys.stderr)
        return 2
    native_text = open(args.native, "rb").read().decode("utf-8-sig")
    actions = human_actions(native_text)
    candidates = {human_clip_name(a["animation"], renames) for a in actions
                  if MELEE_TABLE.match(a.get("type", "")) and a.get("animation")} & human_clips
    keyed = scbn.keyed_clips(args.clips_dir, candidates)
    lines, counts = build_body(actions, human_clips, fab_clips, renames=renames, keyed=keyed)
    print("human codes read: %d; set body planned: %d codes  (%s)" % (
        len(actions), len(lines),
        ", ".join("%s %d" % (k, counts[k]) for k in ("melee-keyed", "melee-table", "fab", "human", "reuse",
                                                     "inherited", "extra") if k in counts)))

    raw = open(args.live, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    nl = "\r\n" if "\r\n" in text else "\n"
    new_text, old_count = replace_body(text, lines, nl)
    new_text, poses = rebind_poses(new_text, fab_clips)
    if new_text == text:
        print("no change: the sets already carry exactly these bindings")
        return 0
    print("actions in the set: %d -> %d; %s overrides rebound: %d" % (old_count, len(lines), POSES_ID, poses))
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
