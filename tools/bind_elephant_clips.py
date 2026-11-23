#!/usr/bin/env python3
"""Bind the war elephant's and the mumak's own gait and idle clips (#1474) in the Armory and in the repo's snapshot.

action_sets.xml
  as_elephant   every action that plays a gait, a turn or one of the first two idles takes the war_elephant_* clip
                of the same role (CLIP_OF). The trot actions, which have always played the walk clip, take the trot,
                and the two standing-pace strafe actions take the strafe clip (STRAFE).
  as_mumakil    NEW: as_elephant's own rows with the mumakil_* clips, a root set as as_dire_wolf is (a child set of
                a child set is not a layout any creature here uses), plus as_mumakil_town_and_village and
                as_mumakil_map, which copy the elephant's two child sets onto it.
Monsters/LOTR/lotr_monster_mumakil.xml (live only; the snapshot has no mumak): action_set="as_mumakil".

THE GATE. The game plays a clip from the RuntimeDataCache entry the Modding Kit cooks, and a clip bound with no
entry crashed Agent.SetActionSet in another modder's test (docs/reference/bannerlord-animation-system-map.md). So
nothing is written unless every clip this tool binds has a record, a master and a cache entry no older than the
master (check_gait_stride.cache_state). Start the Kit once with the Armory loaded, save, close, then run this.

The files are edited in the LIVE Armory (unversioned: --check is the gate that notices a reinstall) and in
docs/reference/lotrlome-armory-snapshot/action_sets.xml, the same change in each. Byte-faithful: binary read, BOM and
CRLF kept, the result parsed with ElementTree before it is written, one backup <file>.bak-elephant-bind-<stamp> per
changed file (never .xml: the folder is globbed). --apply refuses (exit 2) while the game or the Kit runs.
Idempotent: a file that already carries the binding is left alone.

    python tools/bind_elephant_clips.py            # plan, writes nothing
    python tools/bind_elephant_clips.py --apply
    python tools/bind_elephant_clips.py --check    # exit 1 unless every file carries the binding
Undo: copy the backups back. Afterwards run  python tools/verify_mount_assets.py elephant  (and mumakil),
python tools/check_gait_stride.py  and  python tools/validate_moduledata.py.
"""
import argparse
import datetime as dt
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir, game_or_kit_running  # noqa: E402
from check_gait_stride import CREATURES, PREFIXES  # noqa: E402
from check_gait_stride import clip_problems as _clip_problems  # noqa: E402

REPO = Path(__file__).resolve().parent.parent
SNAPSHOT = REPO / "docs" / "reference" / "lotrlome-armory-snapshot" / "action_sets.xml"
CLIPS = CREATURES["elephant"]["clips"]
# today's clip -> the role it plays
CLIP_OF = {"elephant_walk": "walk", "elephant_canter": "canter", "elephant_gallop": "gallop",
           "elephant_walk_backwards": "walk_backwards", "elephant_turn_left": "turn_left",
           "elephant_turn_right": "turn_right"}
IDLE_OF = {"act_elephant_stand_1": "idle_1", "act_elephant_stand_2": "idle_2"}
# The standing pace plays a strafe clip in 0.7 to 1.3 times its record's Duration (check_gait_stride.py), so these
# two rows take a clip of their own: the walk's keys under a record that states their real length. The gate checks
# the same two rows, so the names are its.
STRAFE = CREATURES["elephant"]["pace_actions"]["strafe"]
ROLES = sorted(set(CLIP_OF.values()) | set(IDLE_OF.values()) | {"trot", "strafe"})
ROW = re.compile(r'(<action\s+type="([^"]+)"\s+animation=")([^"]+)(")')
MUMAK_BEGIN = "<!-- MUMAKIL (#1474): as_elephant's rows on the mumak's own clips. Written by tools/bind_elephant_clips.py -->"


class BindError(Exception):
    pass


def role_of(action, clip):
    """The role a row plays, or None for a row this tool leaves alone. Works on today's clips and on bound ones."""
    if action in STRAFE:
        return "strafe"
    for prefix in PREFIXES:
        if clip.startswith(prefix) and clip[len(prefix):] in ROLES:
            return clip[len(prefix):]
    if action in IDLE_OF:
        return IDLE_OF[action]
    if clip == "elephant_walk" and "_trot" in action:
        return "trot"
    return CLIP_OF.get(clip)


def rebind(block, prefix):
    """The rows of an action set block with every gait, turn and idle row on `prefix`'s clips."""
    def one(m):
        role = role_of(m.group(2), m.group(3))
        return m.group(0) if role is None else m.group(1) + prefix + role + m.group(4)
    return ROW.sub(one, block)


def _set_span(text, set_id):
    """(start of the opening tag's line, end of the closing tag's line) of one action set."""
    m = re.search(r'<action_set id="%s"[^>]*>' % re.escape(set_id), text)
    if not m:
        return None
    start = m.start()                       # back to the line's indent: a leading [ \t]* in the pattern makes the
    while start and text[start - 1] in " \t":   # search try every position of a 4.5 MB file
        start -= 1
    if m.group(0).endswith("/>"):
        end = m.end()
    else:
        close = text.find("</action_set>", m.end())
        if close < 0:
            raise BindError("action set %s is not closed" % set_id)
        end = close + len("</action_set>")
    eol = text.find("\n", end)
    return start, (eol + 1 if eol >= 0 else len(text))


def plan_action_sets(text):
    """The file's text with the binding, and what changed. Raises BindError on a layout it does not know."""
    spans = {s: _set_span(text, s) for s in ("as_elephant", "as_elephant_town_and_village", "as_elephant_map")}
    if None in spans.values():
        raise BindError("as_elephant or one of its two child sets is missing")
    a, b = spans["as_elephant"]
    elephant = text[a:b]
    bound = rebind(elephant, "war_elephant_")
    rows = sum(1 for m in ROW.finditer(elephant) if role_of(m.group(2), m.group(3)))
    if rows < 30:
        raise BindError("as_elephant holds %d gait, turn and idle rows; this tool expects at least 30" % rows)
    nl = "\r\n" if "\r\n" in elephant else "\n"
    mumak = rebind(elephant, "mumakil_").replace('id="as_elephant"', 'id="as_mumakil"', 1)
    for child in ("as_elephant_town_and_village", "as_elephant_map"):
        c0, c1 = spans[child]
        mumak += (text[c0:c1].replace('id="%s"' % child, 'id="%s"' % child.replace("as_elephant", "as_mumakil"), 1)
                  .replace('base_set="as_elephant"', 'base_set="as_mumakil"', 1))
    mumak = "\t" + MUMAK_BEGIN + nl + mumak
    end_children = max(spans["as_elephant_town_and_village"][1], spans["as_elephant_map"][1])
    old = _set_span(text, "as_mumakil")
    if old:
        m0 = text.rfind(MUMAK_BEGIN, 0, old[0])
        last = max(_set_span(text, s)[1] for s in ("as_mumakil", "as_mumakil_town_and_village", "as_mumakil_map")
                   if _set_span(text, s))
        start = text.rfind("\n", 0, m0) + 1 if m0 >= 0 else old[0]
        out = text[:a] + bound + text[b:start] + mumak + text[last:]
    else:
        out = text[:a] + bound + text[b:end_children] + mumak + text[end_children:]
    return out, {"rows": rows, "changed": out != text}


def clips_bound(text):
    """Every war_elephant_* and mumakil_* clip the planned file binds."""
    return sorted({c for c in re.findall(r'animation="([^"]+)"', text) if c.startswith(PREFIXES)})


def check_tree(text):
    root = ET.fromstring(text.encode("utf-8").lstrip(b"\xef\xbb\xbf"))
    ids = [s.get("id") for s in root.iter("action_set")]
    for need in ("as_elephant", "as_mumakil", "as_mumakil_town_and_village", "as_mumakil_map"):
        if ids.count(need) != 1:
            raise BindError("%s appears %d times in the planned file" % (need, ids.count(need)))
    sets = {s.get("id"): s for s in root.iter("action_set")}
    a, m = sets["as_elephant"], sets["as_mumakil"]
    if [x.get("type") for x in a] != [x.get("type") for x in m]:
        raise BindError("as_mumakil does not carry as_elephant's actions in order")
    if any(x.tag == "action" for x in root):
        raise BindError("a root-level <action> (it kills a dedicated server)")


def plan_monster(text):
    new = re.sub(r'(<Monster\s+id="taom_mumakil"\s+action_set=")as_elephant(")', r"\1as_mumakil\2", text, count=1)
    if 'action_set="as_mumakil"' not in new:
        raise BindError("the taom_mumakil Monster does not name action_set=\"as_elephant\" or \"as_mumakil\"")
    return new


def clip_problems(module_root, clips):
    return _clip_problems(module_root, CLIPS, clips)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--game", default=None)
    a = ap.parse_args(argv)
    module = os.path.join(a.game or game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"), "Modules", "LOTRLOME_Armory")
    live_sets = Path(module) / "ModuleData" / "action_sets.xml"
    monster = Path(module) / "ModuleData" / "Monsters" / "LOTR" / "lotr_monster_mumakil.xml"
    targets = [(live_sets, plan_action_sets), (SNAPSHOT, plan_action_sets), (monster, plan_monster)]
    for path, _ in targets:
        if not path.exists():
            print("cannot run: %s not found" % path)
            return 2
    plans, bound = [], []
    try:
        for path, fn in targets:
            raw = path.read_bytes()
            text = raw.decode("utf-8")
            if fn is plan_action_sets:
                new, info = fn(text)
                check_tree(new)
                if path == live_sets:
                    bound = clips_bound(new)
                print("%-9s %s: %d rows on the new clips, as_mumakil and its two child sets" % (
                    "change" if info["changed"] else "current", path, info["rows"]))
            else:
                new = fn(text)
                ET.fromstring(new.encode("utf-8").lstrip(b"\xef\xbb\xbf"))
                print("%-9s %s: action_set=\"as_mumakil\"" % ("change" if new != text else "current", path))
            plans.append((path, raw, new.encode("utf-8")))
    except (BindError, ET.ParseError) as e:
        print("REFUSED: %s" % e)
        return 1
    stale = clip_problems(module, bound)
    for s in stale:
        print("NOT READY %s" % s)
    changed = [p for p in plans if p[1] != p[2]]
    if a.check:
        ok = not changed and not stale
        print("bound and every clip cooked" if ok else "NOT bound, or a bound clip is not cooked")
        return 0 if ok else 1
    if stale:
        print("REFUSED: %d of %d clips have no current cache entry; start the Modding Kit once with the Armory "
              "loaded, save, close, and run again" % (len(stale), len(bound)))
        return 1
    if not a.apply:
        print("dry run: %d file(s) would change, %d clips bound" % (len(changed), len(bound)))
        return 0
    run = game_or_kit_running()
    if run:
        print("REFUSED: the game or the Modding Kit is running; close it and run again")
        return 2
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    for path, raw, new in changed:
        Path(str(path) + ".bak-elephant-bind-" + stamp).write_bytes(raw)
        path.write_bytes(new)
        print("wrote %s" % path)
    print("%d file(s) written, %d clips bound" % (len(changed), len(bound)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
