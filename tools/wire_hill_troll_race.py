#!/usr/bin/env python3
"""Point the hill troll race at troll_skeleton_a and its hill_troll_a meshes in the LIVE LOTRLOME_Armory, the
dwarf's layout (docs/features/troll-race.md "Hill troll moved onto its own skeleton").

    python tools/wire_hill_troll_race.py            # dry run: what would change in each file
    python tools/wire_hill_troll_race.py --apply    # write; refused while the game or the Kit runs
    python tools/wire_hill_troll_race.py --check    # the reinstall gate; writes nothing
    then: python tools/patch_dwarf_action_parity.py --target <Armory>/ModuleData/action_sets.xml
              --set-id as_hill_troll_warrior --apply
          python tools/bind_hill_troll_action_set.py --clips-index <human_json_batch1>/clips_index.json --apply

skins.xml, inside <race id="hill_troll"> only: every skin (adult, teen, child, toddler; the dwarf puts all ten on its
own skeleton) gets skeleton troll_skeleton_a, the hill_troll_a body / shoulder / legs / hands / head meshes and no
underwear meshes; every skin's hair, eyebrow and beard lists become the adult male's (bald, no brow, clean-shaven),
because a human hair mesh on this skeleton would hang at a human head's height (an empty `<beard_meshes />` stays);
the children's default_hair_meshes / default_beard_meshes (human hair and beards shown under a helmet) go, as
neither the dwarf nor the adult male troll has them; every face texture AND every mouth texture outside a comment
names the troll head material (the engine puts the skin's mouth material on the head's face_mouth_mesh sub-mesh,
which carries the head material in KEYForce's model; the old `t_hilltroll_mouth` never existed in the Kit and
warned "Unable to find material" every session, 2026-09-25).
monsters.xml: Monster hill_troll takes the sizes measured from the 3.6 m model (eye centre 3.58 m; the eyes in
the head bone's frame; arm length 0.9 x the shoulder-to-wrist ratio 3.10; crouch x the height ratio 2.215; body
capsules of radius 1.2, half the LOD0 mesh's 2.43 m shoulder width, over the extent the human's capsule scaled by
2.215 gave, standing 0.95 to 4.25 m and crouched 0.51 to 4.25 m: the scaled human radius 0.82 let neighbours press
into each other's shoulders, Mike 2026-09-26), CanRide off; its four variants get the <race>_<suffix> names FaceGen.GetMonsterWithSuffix looks up
(TaleWorlds.MountAndBlade/FaceGen.cs:44; `troll_settlement` handed a settlement or conversation spawn null), the
child's sizes scaled like the adult's. main_hand_item_bone stays r_finger0: the export adds the grip bones.
action_sets.xml: as_hill_troll_warrior becomes standalone on troll_skeleton_a (bipedal), as as_dwarf_warrior is,
because an agent's skeleton comes from its action set (MBActionSet.GetSkeletonName) and no Armory set that
inherits a base set changes its skeleton. The set is empty until patch_dwarf_action_parity.py fills it from
Native's as_human_warrior: run that next, before any load.

Each edit is computed on the file's own text and applied back to front, byte-faithful otherwise (BOM, CRLF,
comments, other races and sets untouched); the result must parse and pass a read-back check of every value, or
nothing is written. Idempotent; a missing anchor is refused. --apply writes <file>.bak-hilltroll-race-<time> first.
--check also requires the set's body the two follow-up steps write: at least one action, at least one
anim_hill_troll_* clip and exactly one act_troll_brute_force binding (audit_action_set_parity.py covers the codes);
and it fails any as_hill_troll_* set that binds a release, quick release, blocked or quick blocked code (the
binder's MELEE_TABLE) to an anim_* clip, because the engine's melee attack table has rows only for vanilla clips
and a troll clip there crashes the first swing (TaleWorlds.Native.dll +0x6590B9).
Exit codes: 0 done (or dry run, or wired), 1 refused (or --check found drift or an unfilled set), 2 the game or the
Kit runs (or a file is missing).
"""
import argparse
import datetime
import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir, game_or_kit_running  # noqa: E402
from bind_hill_troll_action_set import MELEE_TABLE  # noqa: E402  (the binder's rule 0, one pattern for both)
import set_clip_balance_name as scbn  # noqa: E402  (which troll clips are self-keyed)

ARMORY = os.path.join(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"),
                      "Modules", "LOTRLOME_Armory", "ModuleData")
RACE = "hill_troll"
SKIN_ATTRS = {"skeleton": "troll_skeleton_a", "body_meta_mesh": "hill_troll_a_body",
              "body_meta_mesh_shoulders": "hill_troll_a_shoulder", "legs_mesh": "hill_troll_a_legs",
              "hands_mesh": "hill_troll_a_hands", "face_meta_mesh": "hill_troll_a_head",
              "underwear_bottom_mesh": "", "underwear_top_mesh": ""}
FACE_MATERIAL = "t_tr_hill_troll_head_a"
HEAD_LISTS = ("hair_meshes", "eyebrow_meshes", "beard_meshes")
HELMET_DEFAULTS = ("default_hair_meshes", "default_beard_meshes")  # human hair shown under a helmet
MONSTER_ATTRS = {"standing_eye_height": "3.58", "crouch_eye_height": "2.32",
                 "eye_offset_wrt_head": "-0.069, 0.421, 0.0",
                 "first_person_camera_offset_wrt_head": "-0.069, 0.434, 0.0", "arm_length": "2.79"}
CAPSULES = {"body_capsule": {"radius": "1.2", "pos1": "0.0, 0.0, 3.05", "pos2": "0.0, 0, 2.15"},
            "crouched_body_capsule": {"radius": "1.2", "pos1": "0.0, 0.0, 3.05", "pos2": "0.0, 0, 1.71"}}
RENAMES = {"troll_child": "hill_troll_child", "troll_settlement": "hill_troll_settlement",
           "troll_settlement_slow": "hill_troll_settlement_slow",
           "troll_settlement_fast": "hill_troll_settlement_fast"}
CHILD_ATTRS = {"standing_eye_height": "2.53", "crouch_eye_height": "1.47", "arm_length": "1.86"}
NO_RIDING = ("hill_troll", "hill_troll_settlement")
ACTION_SET = "as_hill_troll_warrior"
BRUTE_FORCE_ACTION = "act_troll_brute_force"   # TrollBruteForceConfig.ActionName, bound by the binder (#649)
ACTION_SET_HEADER = '<action_set id="as_hill_troll_warrior" skeleton="troll_skeleton_a" movement_system="bipedal">'


class Refused(Exception):
    pass


def _comments(text):
    return [m.span() for m in re.finditer(r"<!--.*?-->", text, re.S)]


def _outside(pos, comments):
    return not any(a <= pos < b for a, b in comments)


def _set_attr(tag, attr, value):
    """One attribute's value in a start tag; the attribute must be there exactly once."""
    pat = re.compile(r'(\s%s=")[^"]*(")' % re.escape(attr))
    if len(pat.findall(tag)) != 1:
        raise Refused("attribute %s is not in %s exactly once" % (attr, tag[:80]))
    return pat.sub(lambda m: m.group(1) + value + m.group(2), tag)


def _apply(text, edits):
    """edits: (start, end, new) on text, disjoint; applied back to front. -> (text, number that changed)."""
    edits = sorted(e for e in edits if text[e[0]:e[1]] != e[2])
    for (a, b, _), (c, _, _) in zip(edits, edits[1:]):
        if c < b:
            raise Refused("two edits overlap at %d" % c)
    for a, b, new in reversed(edits):
        text = text[:a] + new + text[b:]
    return text, len(edits)


def _parse(text):
    try:
        return ET.fromstring(text.lstrip("\ufeff").encode("utf-8"))
    except ET.ParseError as exc:
        raise Refused("the edited file no longer parses: %s" % exc)


def _start_tags(text, name, start, end, comments):
    return [m for m in re.finditer(r"<%s\b[^>]*>" % name, text[start:end])
            if _outside(start + m.start(), comments)]


def edit_skins(text):
    race = re.search(r'<race\s+id="%s"\s*>' % RACE, text)
    if not race:
        raise Refused('no <race id="%s"> in skins.xml' % RACE)
    start, end = race.end(), text.find("</race>", race.end())
    comments = _comments(text)
    skins = _start_tags(text, "skin", start, end, comments)
    if not skins:
        raise Refused("the %s race has no skin" % RACE)
    edits, spans = [], []
    for i, m in enumerate(skins):
        a = start + m.start()
        b = start + skins[i + 1].start() if i + 1 < len(skins) else end
        tag = m.group(0)
        for attr, value in SKIN_ATTRS.items():
            tag = _set_attr(tag, attr, value)
        edits.append((a, a + len(m.group(0)), tag))
        spans.append((a, b, tag))
    man = [s for s in spans if 'gender="0"' in s[2] and 'name="man"' in s[2]]
    if len(man) != 1:
        raise Refused("expected one adult male skin in the %s race, found %d" % (RACE, len(man)))

    def lists(a, b):
        found = {}
        for name in HEAD_LISTS:
            ms = _start_tags(text, name, a, b, comments)
            if len(ms) > 1:
                raise Refused("a %s skin has %d %s lists" % (RACE, len(ms), name))
            if ms and not ms[0].group(0).endswith("/>"):  # <beard_meshes />: an empty list, left alone
                inner_start = a + ms[0].end()
                inner_end = text.find("</%s>" % name, inner_start)
                if inner_end == -1 or inner_end > b:
                    raise Refused("an unclosed %s in the %s race" % (name, RACE))
                found[name] = (inner_start, inner_end)
        return found

    source = lists(man[0][0], man[0][1])
    missing = [n for n in HEAD_LISTS if n not in source]
    if missing:
        raise Refused("the adult male skin has no %s to copy" % missing)
    bald = {n: text[s:e] for n, (s, e) in source.items()}
    for a, b, _ in spans:
        for name, (s, e) in lists(a, b).items():
            edits.append((s, e, bald[name]))
    # the face AND the mouth: the engine puts a skin's <mouth_texture> material on the head's face_mouth_mesh sub-mesh,
    # and the old troll's `t_hilltroll_mouth` never existed in the Kit ("Unable to find material", every session);
    # the new mouth sub-mesh carries the head material, as KEYForce set it, so both lists name that one
    for name in ("face_texture", "mouth_texture"):
        for m in _start_tags(text, name, start, end, comments):
            tag = _set_attr(_set_attr(m.group(0), "name", FACE_MATERIAL), "lod_material", FACE_MATERIAL)
            edits.append((start + m.start(), start + m.end(), tag))
    for name in HELMET_DEFAULTS:
        for m in _start_tags(text, name, start, end, comments):
            if not m.group(0).endswith("/>"):
                raise Refused("%s in the %s race is not self-closing" % (name, RACE))
            # delete the tag's whole line, its own terminator included, so a CRLF (or doubled-CR) file keeps every
            # other line's ending; a tag sharing its line with other markup loses only the tag
            tag_start, tag_end = start + m.start(), start + m.end()
            line_start = text.rfind("\n", 0, tag_start) + 1
            eol = re.compile(r"[ \t]*\r*\n").match(text, tag_end)
            if text[line_start:tag_start].strip() or not eol:
                edits.append((tag_start, tag_end, ""))
            else:
                edits.append((line_start, eol.end(), ""))
    new, changes = _apply(text, edits)
    _check_skins(new)
    return new, {"changes": changes, "skins": len(skins)}


def _check_skins(text):
    race = [r for r in _parse(text).iter("race") if r.get("id") == RACE][0]
    skins = race.findall("skin")
    man = [s for s in skins if s.get("gender") == "0" and s.get("name") == "man"][0]
    for s in skins:
        for attr, value in SKIN_ATTRS.items():
            if s.get(attr) != value:
                raise Refused("read-back: skin %s %s=%r" % (s.get("name"), attr, s.get(attr)))
        for name in HEAD_LISTS:
            el = s.find(name)
            if el is not None and len(el) and [ET.tostring(c) for c in el] != [ET.tostring(c) for c in man.find(name)]:
                raise Refused("read-back: skin %s %s is not the adult male's" % (s.get("name"), name))
        for tag in ("face_texture", "mouth_texture"):
            for t in s.iter(tag):
                if (t.get("name"), t.get("lod_material")) != (FACE_MATERIAL, FACE_MATERIAL):
                    raise Refused("read-back: skin %s %s %s" % (s.get("name"), tag, t.get("name")))
        for name in HELMET_DEFAULTS:
            if s.find(name) is not None:
                raise Refused("read-back: skin %s still has %s" % (s.get("name"), name))


def _monster_tag(text, mid, comments):
    ms = [m for m in re.finditer(r'<Monster\s+id="%s"[^>]*>' % re.escape(mid), text) if _outside(m.start(), comments)]
    if len(ms) > 1:
        raise Refused("Monster %s is defined %d times" % (mid, len(ms)))
    return ms[0] if ms else None


def _element_end(text, tag_match):
    return tag_match.end() if tag_match.group(0).endswith("/>") else text.find("</Monster>", tag_match.end())


def edit_monsters(text):
    comments = _comments(text)
    edits = []
    main = _monster_tag(text, "hill_troll", comments)
    if not main:
        raise Refused("no Monster hill_troll in monsters.xml")
    tag = main.group(0)
    for attr, value in MONSTER_ATTRS.items():
        tag = _set_attr(tag, attr, value)
    edits.append((main.start(), main.end(), tag))
    end = _element_end(text, main)
    for cap, attrs in CAPSULES.items():
        found = _start_tags(text, cap, main.end(), end, comments)
        if len(found) != 1:
            raise Refused("Monster hill_troll has %d %s" % (len(found), cap))
        t = found[0].group(0)
        for attr, value in attrs.items():
            t = _set_attr(t, attr, value)
        edits.append((main.end() + found[0].start(), main.end() + found[0].end(), t))
    variants = {}
    for old, new in RENAMES.items():
        old_m, new_m = _monster_tag(text, old, comments), _monster_tag(text, new, comments)
        if old_m and new_m:
            # renaming would define the new id twice; the read-back's dict would hide the duplicate
            raise Refused("both Monster %s and %s exist; remove one by hand first" % (old, new))
        m = old_m or new_m
        if not m:
            raise Refused("neither Monster %s nor %s exists" % (old, new))
        t = _set_attr(m.group(0), "id", new)
        if new == "hill_troll_child":
            for attr, value in CHILD_ATTRS.items():
                t = _set_attr(t, attr, value)
        edits.append((m.start(), m.end(), t))
        variants[new] = m
    for mid in NO_RIDING:
        m = main if mid == "hill_troll" else variants[mid]
        for f in _start_tags(text, "Flags", m.end(), _element_end(text, m), comments):
            if re.search(r'\sCanRide="', f.group(0)):
                edits.append((m.end() + f.start(), m.end() + f.end(), _set_attr(f.group(0), "CanRide", "false")))
    new_text, changes = _apply(text, edits)
    _check_monsters(new_text)
    return new_text, {"changes": changes}


def _check_monsters(text):
    by_id = {m.get("id"): m for m in _parse(text).iter("Monster")}
    m = by_id.get("hill_troll")
    if m is None or any(m.get(a) != v for a, v in MONSTER_ATTRS.items()):
        raise Refused("read-back: Monster hill_troll sizes")
    for cap, attrs in CAPSULES.items():
        if any(m.find("Capsules/" + cap).get(a) != v for a, v in attrs.items()):
            raise Refused("read-back: Monster hill_troll %s" % cap)
    for old, new in RENAMES.items():
        if new not in by_id or old in by_id:
            raise Refused("read-back: Monster %s / %s" % (old, new))
    for mid in NO_RIDING:
        f = by_id[mid].find("Flags")
        if f is not None and f.get("CanRide") not in (None, "false"):
            raise Refused("read-back: %s can still ride" % mid)


def edit_action_sets(text):
    comments = _comments(text)
    old = [m for m in re.finditer(r'<action_set\s+id="%s"\s+base_set="as_human_warrior"\s*>' % ACTION_SET, text)
           if _outside(m.start(), comments)]
    done = re.search(r'<action_set\s+id="%s"\s+skeleton="troll_skeleton_a"\s+movement_system="bipedal"\s*>'
                     % ACTION_SET, text)
    if len(old) == 1:
        new, changes = _apply(text, [(old[0].start(), old[0].end(), ACTION_SET_HEADER)])
    elif not old and done:
        new, changes = text, 0
    else:
        raise Refused("%s is neither the inheriting set nor the standalone one (%d inheriting headers)"
                      % (ACTION_SET, len(old)))
    root = _parse(new)
    s = [a for a in root.iter("action_set") if a.get("id") == ACTION_SET]
    if len(s) != 1 or s[0].get("skeleton") != "troll_skeleton_a" or s[0].get("base_set") is not None:
        raise Refused("read-back: %s is not standalone on troll_skeleton_a" % ACTION_SET)
    actions = s[0].findall("action")
    # the crash invariant, in every as_hill_troll_* set: a melee attack-table code (release, quick release, blocked,
    # quick blocked) on a custom clip. Native binds none of those codes to an anim_* clip, so that prefix marks a
    # troll clip there without reading Native; unfilled() then clears the self-keyed ones, which have a row
    melee = [a.get("animation") for st in root.iter("action_set") if (st.get("id") or "").startswith("as_hill_troll_")
             for a in st.findall("action")
             if MELEE_TABLE.match(a.get("type") or "") and (a.get("animation") or "").startswith("anim_")]
    # what the two fill steps leave behind, for --check: the parity tool fills the body with human clips, the
    # binder puts the troll's own clips and the Brute Force binding in
    return new, {"changes": changes, "actions_now": len(actions),
                 "troll_clips": sum(1 for a in actions if (a.get("animation") or "").startswith("anim_hill_troll_")),
                 "brute_force": sum(1 for a in actions if a.get("type") == BRUTE_FORCE_ACTION),
                 "melee_troll_clips": len(melee), "_melee_clips": tuple(melee)}


def unfilled(report, keyed=frozenset()):
    """The --check findings on the action sets: the standalone set empty, never bound by the binder, or without
    exactly one Brute Force binding (a reinstall that kept the wiring but lost the binder's work reads as wired
    otherwise), and any as_hill_troll_* set binding a melee attack-table code to a troll clip that is not self-keyed
    (a crash). `keyed` is the self-keyed clips on disk (set_clip_balance_name.keyed_clips): those have a row."""
    found = []
    unkeyed = sum(1 for clip in report.get("_melee_clips", ()) if clip not in keyed)
    if not report["actions_now"]:
        found.append("the set has no actions: run patch_dwarf_action_parity.py, then bind_hill_troll_action_set.py")
    else:
        if not report["troll_clips"]:
            found.append("the set binds no anim_hill_troll_* clip: run bind_hill_troll_action_set.py --apply")
        if report["brute_force"] != 1:
            found.append("%s is bound %d times, not once: run bind_hill_troll_action_set.py --apply"
                         % (BRUTE_FORCE_ACTION, report["brute_force"]))
    if unkeyed:
        found.append("%d release, quick release, blocked or quick blocked code(s) in the as_hill_troll_* sets play a "
                     "troll clip that is not self-keyed: it has no row in the engine's melee attack table, so the "
                     "first swing crashes (TaleWorlds.Native.dll +0x6590B9). Self-key the clip "
                     "(tools/set_clip_balance_name.py), or run bind_hill_troll_action_set.py --apply, which keeps "
                     "the vanilla clip there in %s; in any other set put the vanilla clip back by hand"
                     % (unkeyed, ACTION_SET))
    return found


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--armory", default=ARMORY, help="the LOTRLOME_Armory ModuleData folder")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--clips-dir", default=None,
                    help="the hill troll clip packages, read by --check to clear self-keyed clips on melee codes "
                         "(default: <armory>/../Assets/Race Test/Mordor/Trolls/animations)")
    ap.add_argument("--check", action="store_true",
                    help="exit 1 if the live Armory is not wired (a reinstall reverts it), 0 if it is, 2 if a "
                         "file is missing; writes nothing")
    args = ap.parse_args(argv)
    if args.clips_dir is None:
        args.clips_dir = os.path.join(os.path.dirname(os.path.abspath(args.armory)), "Assets", "Race Test", "Mordor",
                                      "Trolls", "animations")
    plan = []
    try:
        for name, fn in (("skins.xml", edit_skins), ("monsters.xml", edit_monsters),
                         ("action_sets.xml", edit_action_sets)):
            path = os.path.join(args.armory, name)
            if not os.path.isfile(path):
                print("MISSING: %s (is --armory the LOTRLOME_Armory ModuleData folder?)" % path)
                return 2
            with open(path, "rb") as fh:
                raw = fh.read()
            new, report = fn(raw.decode("utf-8"))
            plan.append((path, raw, new.encode("utf-8"), report))
            print("%-16s %s" % (name, {k: v for k, v in report.items() if not k.startswith("_")}))
    except Refused as exc:
        print("REFUSED: %s" % exc)
        return 1
    if args.check:
        drift = [(os.path.basename(p), r["changes"]) for p, _, _, r in plan if r["changes"]]
        for name, changes in drift:
            print("DRIFT: %s needs %d change(s); the hill troll race is not wired (re-run with --apply)"
                  % (name, changes))
        report = plan[-1][3]
        keyed = scbn.keyed_clips(args.clips_dir, set(report.get("_melee_clips", ())))
        if keyed:
            print("self-keyed troll clips on melee attack-table codes (they have a row): %d" % len(keyed))
        gaps = [] if drift else unfilled(report, keyed)
        for gap in gaps:
            print("UNFILLED: %s" % gap)
        if not drift and not gaps:
            print("OK: the hill troll race, Monster and standalone set are wired, and the set is bound")
        return 1 if drift or gaps else 0
    if not args.apply:
        print("dry run: nothing written (add --apply)")
        return 0
    if game_or_kit_running():
        print("REFUSED: Bannerlord or the Modding Kit is running; close it and re-run")
        return 2
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    for path, raw, new, _ in plan:
        if new == raw:
            continue
        shutil.copy2(path, "%s.bak-hilltroll-race-%s" % (path, stamp))
        with open(path, "wb") as fh:
            fh.write(new)
        with open(path, "rb") as fh:
            if fh.read() != new:
                print("ERROR: %s differs from what was written; restore its .bak-hilltroll-race-%s" % (path, stamp))
                return 1
        print("wrote %s (backup .bak-hilltroll-race-%s)" % (path, stamp))
    print("NEXT, before any load: python tools/patch_dwarf_action_parity.py --target \"%s\" --set-id %s --apply"
          % (os.path.join(args.armory, "action_sets.xml"), ACTION_SET))
    print("  then python tools/bind_hill_troll_action_set.py --clips-index <human_json_batch1>/clips_index.json "
          "--apply, and this tool's --check")
    return 0


if __name__ == "__main__":
    sys.exit(main())
