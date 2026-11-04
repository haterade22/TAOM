"""Write the ridden dire wolf into the LIVE LOTRLOME_Armory (#1081): Monster, action sets, Horse items, saddle, names.

    python tools/apply_dire_wolf_armory.py            # dry run: prints what would change, writes nothing
    python tools/apply_dire_wolf_armory.py --apply    # Modding Kit and game CLOSED

Sibling of tools/apply_wildmesh_armory.py, whose I/O helpers come from tools/apply_animalia_armory.py. The Armory is
unversioned, so every edit here is recorded in docs/reference/lotrlome-dire-wolf-changes.md.

  1. ModuleData/Monsters/LOTR/lotr_monster_dire_wolf.xml, written whole: the warg's behavior (monster_usage warg,
     family_type 1, flags, weight, hit points, sound class) on the wolf's own bones, as_dire_wolf and
     taom_body_length 240. Lengths are in model units, which the engine then scales by the agent's size: the heights and
     body capsule are the pack's own wolf Monster's, and the rider and rein values are the warg's over 2.37, the
     measured size ratio of the two rigs. Tune them in game. The Monster has the warg's 5 rein attributes, not 12
     (creature-mount-authoring.md, gotcha 18): docs/features/dire-wolf.md owes a ridden-death test in game.
  2. SubModule.xml and ModuleData/project.mbproj: one Monsters registration each, after the fell warg's.
  3. ModuleData/action_sets.xml: as_dire_wolf and its _town_and_village and _map twins, derived from the three warg sets
     as they stand, so no binding is left out: only the ids, the skeleton and each clip's warg_ prefix change. The action
     names stay act_warg_*, which is what the warg behavior tree, usage set and rider partial play. Every derived clip
     must exist (tools/gen_dire_wolf_anim_clips.ps1 wrote them). This stays in the one soln_action_sets file.
  4. ModuleData/LOTRLOME_items/LOTRAOM_horses.xml: one Horse item per skin at the warg's stats, the body_length
     placeholder Items.xsd requires (TAOM copies the Monster's size in at game init), and the dire_wolf_saddle harness.
     The eye stays the FBX's per skin. To change one in XML, tag that eye sub-mesh horse_body in the Kit and add
     <Materials> to the item's <ItemComponent><Horse> node.
  5. ModuleData/Languages/loc_LOTRAOM_horses.xml (the root English file only): one row per item.

Byte-faithful: BOM and line endings kept, every changed file parsed before anything is written, a write-once backup with
a non-.xml suffix (.bak-direwolf-<YYYYMMDD-HHMMSS>). Idempotent per step; a set family partly present is refused. A
missing anchor, a missing clip or a warg binding without the warg_ prefix refuses the whole run, exit 1. Refuses --apply
while the game or the Kit runs, exit 2. --game-dir points the script at a synthetic tree
(tools/tests/test_apply_dire_wolf_armory.py).
"""
import argparse
import datetime as dt
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import ensure_exists, game_dir, game_or_kit_running  # noqa: E402
from apply_animalia_armory import nl_of, read, write  # noqa: E402

_DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
BACKUP_PREFIX = ".bak-direwolf-"
SKELETON = "adod_wolf_armature_unused"
SET_PAIRS = (("as_warg", "as_dire_wolf"), ("as_warg_town_and_village", "as_dire_wolf_town_and_village"),
             ("as_warg_map", "as_dire_wolf_map"))
WARG_END_MARKER = "\t<!-- ============================== /WARG ============================== -->"
MONSTER_FILE = "ModuleData/Monsters/LOTR/lotr_monster_dire_wolf.xml"

# adod_wolf_armature_unused's bones that the Monster may name (read from the package, 2026-11-04).
WOLF_BONES = {"Pelvis", "Spine", "Spine1", "Spine2", "Neck", "Head", "L Clavicle", "R Clavicle", "L UpperArm",
              "R UpperArm", "L Finger0", "R Finger0", "L Thigh", "R Thigh"}

_BONES = (
    ("rider_sit_bone", "Spine1"), ("rein_handle_bone", "Spine2"), ("rein_collision_1_bone", "Spine1"),
    ("rein_collision_2_bone", "Spine1"), ("neck_root_bone", "Neck"), ("pelvis_bone", "Pelvis"),
    ("right_upper_arm_bone", "R UpperArm"), ("left_upper_arm_bone", "L UpperArm"),
    ("terrain_decal_bone_0", "L Finger0"), ("terrain_decal_bone_1", "R Finger0"),
    ("bones_to_modify_on_sloping_ground_0", "Spine2"), ("bones_to_modify_on_sloping_ground_1", "L Clavicle"),
    ("bones_to_modify_on_sloping_ground_2", "R Clavicle"), ("bones_to_modify_on_sloping_ground_3", "L Thigh"),
    ("bones_to_modify_on_sloping_ground_4", "R Thigh"), ("body_rotation_reference_bone", "Spine"),
    ("head_look_direction_bone", "Head"), ("thorax_look_direction_bone", "Spine2"),
)
_ATTRIBUTES = (
    ("id", "dire_wolf"), ("action_set", "as_dire_wolf"), ("monster_usage", "warg"), ("weight", "500"),
    ("hit_points", "80"), ("absorbed_damage_ratio", "1.0"), ("num_paces", "6"), ("jump_acceleration", "8"),
    ("sound_and_collision_info_class", "warg"), ("rider_camera_height_adder", "-0.21"),
    ("rider_body_capsule_height_adder", "0"), ("rider_body_capsule_forward_adder", "0"),
    ("standing_chest_height", "0.64"), ("standing_pelvis_height", "0.64"), ("standing_eye_height", "0.88"),
    ("rider_eye_height_adder", "0.72"), ("eye_offset_wrt_head", "0.07, -0.05, 0.0"), ("jump_speed_limit", "4.5"),
    ("relative_speed_limit_for_charge", "4"), ("family_type", "1"), ("taom_body_length", "240"),
) + _BONES + (
    ("front_bone_to_detect_ground_slope_index", "2"), ("back_bone_to_detect_ground_slope_index", "4"),
    ("rein_handle_left_local_pos", "0,-0.42,0.06"), ("rein_handle_right_local_pos", "0,-0.42,-0.06"),
)

SKINS = (("grey", "gundabad"), ("white", "gundabad"), ("black", "goblin"), ("brown", "goblin"))
ITEM_NAMES = {
    "dire_wolf_grey": "[Gundabad] Grey Dire Wolf",
    "dire_wolf_white": "[Gundabad] White Dire Wolf",
    "dire_wolf_black": "[Goblin] Black Dire Wolf",
    "dire_wolf_brown": "[Goblin] Brown Dire Wolf",
    "dire_wolf_saddle": "[Gundabad] Dire Wolf Saddle",
}


def monster_bones():
    return {value for name, value in _BONES}


def backup_suffix():
    return BACKUP_PREFIX + dt.datetime.now().strftime("%Y%m%d-%H%M%S")


def resolve_paths(root):
    armory = os.path.join(root, "Modules", "LOTRLOME_Armory")
    return {
        "armory": armory,
        "monster": os.path.join(armory, *MONSTER_FILE.split("/")),
        "submodule": os.path.join(armory, "SubModule.xml"),
        "project": os.path.join(armory, "ModuleData", "project.mbproj"),
        "action_sets": os.path.join(armory, "ModuleData", "action_sets.xml"),
        "horses": os.path.join(armory, "ModuleData", "LOTRLOME_items", "LOTRAOM_horses.xml"),
        "loc": os.path.join(armory, "ModuleData", "Languages", "loc_LOTRAOM_horses.xml"),
        "clips_dir": os.path.join(armory, "Assets", "creature", "direwolf", "animations"),
    }


def monster_text(nl):
    lines = ['\ufeff<?xml version="1.0" encoding="utf-8"?>', "<Monsters>",
             "\t<!-- TAOM dire wolf (#1081): the warg's behavior on the purchased ADOD_Beasts wolf rig.",
             "\t     Written by tools/apply_dire_wolf_armory.py; size lives on taom_body_length. -->",
             "\t<Monster"]
    lines += ['\t\t%s="%s"' % pair for pair in _ATTRIBUTES]
    lines[-1] += ">"
    lines += ["\t\t<Capsules>", "\t\t\t<body_capsule", '\t\t\t\tradius="0.20"', '\t\t\t\tpos1="0, 0.55, 0.5"',
              '\t\t\t\tpos2="0, -0.25, 0.5" />', "\t\t</Capsules>", "\t\t<Flags", '\t\t\tMountable="true"',
              '\t\t\tCanRear="true"', '\t\t\tRunsAwayWhenHit="true"', '\t\t\tCanCharge="true"',
              '\t\t\tCanWander="true" />', "\t</Monster>", "</Monsters>", ""]
    return nl.join(lines)


def _set_block(text, set_id):
    pattern = r'[ \t]*<action_set\s+id="%s"[^>]*>.*?</action_set>\r?\n' % re.escape(set_id)
    found = re.findall(pattern, text, flags=re.S)
    return found[0] if len(found) == 1 else None


def derive_sets(text, problems):
    """The three wolf set blocks and the clips they bind, derived from the warg sets in the file."""
    blocks, clips = [], set()
    for warg, wolf in SET_PAIRS:
        block = _set_block(text, warg)
        if block is None:
            problems.append("ANCHOR: action_sets.xml has no single action_set %s" % warg)
            continue
        for clip in re.findall(r'animation="([^"]*)"', block):
            if not clip.startswith("warg_"):
                problems.append("BINDING: %s binds %s, which has no warg_ prefix" % (warg, clip))
                continue
            clips.add("dire_wolf_" + clip[len("warg_"):])
        block = block.replace('id="%s"' % warg, 'id="%s"' % wolf, 1)
        block = block.replace('skeleton="skeleton_warg"', 'skeleton="%s"' % SKELETON, 1)
        block = block.replace('base_set="as_warg"', 'base_set="as_dire_wolf"', 1)
        block = block.replace('animation="warg_', 'animation="dire_wolf_')
        try:
            root = ET.fromstring(block)
        except ET.ParseError as exc:
            problems.append("DERIVED: %s is not one action_set: %s" % (wolf, exc))
            continue
        if root.get("id") != wolf or root.get("skeleton") != SKELETON or root.get("base_set") not in (None, "as_dire_wolf"):
            problems.append("DERIVED: %s came out as %s" % (wolf, dict(root.attrib)))
            continue
        blocks.append(block)
    return blocks, clips


def item_xml(nl):
    lines = ["\t<!-- ===== DIRE WOLF (#1081, tools/apply_dire_wolf_armory.py) ===== -->"]
    for skin, culture in SKINS:
        item = "dire_wolf_" + skin
        lines += ['\t<Item id="%s"' % item, '\t\tname="{=%s}%s"' % (item, ITEM_NAMES[item]), '\t\tmesh="%s"' % item,
                  '\t\tsubtype="horse"', '\t\titem_category="war_horse"', '\t\tculture="Culture.%s"' % culture,
                  '\t\tis_merchandise="true"', '\t\tvalue="1220"', '\t\tweight="500"', '\t\tdifficulty="20"',
                  '\t\tappearance="1"', '\t\tType="Horse">', "\t\t<ItemComponent>", "\t\t\t<Horse",
                  '\t\t\t\tmonster="Monster.dire_wolf"', '\t\t\t\tmaneuver="70"', '\t\t\t\tspeed="55"',
                  '\t\t\t\tcharge_damage="15"', '\t\t\t\tbody_length="100"', '\t\t\t\tis_mountable="true" />',
                  "\t\t</ItemComponent>", "\t</Item>"]
    lines += ['\t<Item id="dire_wolf_saddle"', '\t\tname="{=dire_wolf_saddle}%s"' % ITEM_NAMES["dire_wolf_saddle"],
              '\t\tmesh="dire_wolf_saddle"', '\t\tis_merchandise="true"', '\t\tculture="Culture.gundabad"',
              '\t\tweight="16"', '\t\tappearance="0.5"', '\t\tType="HorseHarness">', "\t\t<ItemComponent>",
              "\t\t\t<Armor", '\t\t\t\tbody_armor="12"', '\t\t\t\tmane_cover_type="none"', '\t\t\t\tfamily_type="1"',
              '\t\t\t\tmaterial_type="Leather" />', "\t\t</ItemComponent>", '\t\t<Flags Civilian="true" />', "\t</Item>",
              "\t<!-- ===== /DIRE WOLF ===== -->"]
    return nl.join(lines) + nl


def plan_monster(path, report, problems, changed):
    if os.path.exists(path):
        try:
            ids = [m.get("id") for m in ET.parse(path).getroot().iter("Monster")]
        except ET.ParseError as exc:
            problems.append("MONSTER: %s does not parse: %s" % (os.path.basename(path), exc))
            return
        if "dire_wolf" not in ids:
            problems.append("MONSTER: %s holds no Monster id=\"dire_wolf\"" % os.path.basename(path))
            return
        report.append("monster file: already present")
        return
    changed.append((path, monster_text("\r\n")))
    report.append("monster file: write %s" % os.path.basename(path))


def plan_submodule(path, report, problems, changed):
    text = read(path)
    if "Monsters/LOTR/lotr_monster_dire_wolf" in text:
        report.append("SubModule.xml: already registered")
        return
    nl = nl_of(text)
    found = re.findall(r'[ \t]*<XmlNode>\s*<XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_fell_warg"\s*/>.*?</XmlNode>\r?\n',
                       text, flags=re.S)
    if len(found) != 1:
        problems.append("ANCHOR: SubModule.xml has no single fell warg Monsters block")
        return
    block = found[0].replace("lotr_monster_fell_warg", "lotr_monster_dire_wolf")
    changed.append((path, text.replace(found[0], found[0] + nl + block, 1)))
    report.append("SubModule.xml: add the Monsters registration")


def plan_project(path, report, problems, changed):
    text = read(path)
    if "lotr_monster_dire_wolf.xml" in text:
        report.append("project.mbproj: already registered")
        return
    found = re.findall(r'[ \t]*<file id="soln_monsters" name="ModuleData/Monsters/LOTR/lotr_monster_fell_warg\.xml" type="monster" />\r?\n', text)
    if len(found) != 1:
        problems.append("ANCHOR: project.mbproj has no single fell warg Monsters row")
        return
    changed.append((path, text.replace(found[0], found[0] + found[0].replace("fell_warg", "dire_wolf"), 1)))
    report.append("project.mbproj: add the Monsters row")


def plan_action_sets(path, clips_dir, report, problems, changed):
    text = read(path)
    present = [wolf for _, wolf in SET_PAIRS if 'id="%s"' % wolf in text]
    if len(present) == len(SET_PAIRS):
        report.append("action_sets.xml: already present")
        return
    if present:
        problems.append("action_sets.xml: the as_dire_wolf family is partly present: %s" % present)
        return
    if text.count(WARG_END_MARKER) != 1:
        problems.append("ANCHOR: action_sets.xml has no single /WARG end marker")
        return
    blocks, clips = derive_sets(text, problems)
    on_disk = set()
    if os.path.isdir(clips_dir):
        on_disk = {f[:-len("_anm.tpac")] for f in os.listdir(clips_dir) if f.endswith("_anm.tpac")}
    for clip in sorted(clips - on_disk):
        problems.append("CLIP MISSING: %s" % clip)
    nl = nl_of(text)
    head = ("\t<!-- ============================== DIRE WOLF (#1081) ==============================" + nl +
            "\t     Derived from the warg sets by tools/apply_dire_wolf_armory.py: same actions, wolf clips, wolf skeleton. -->" + nl)
    tail = "\t<!-- ============================== /DIRE WOLF ============================== -->" + nl
    changed.append((path, text.replace(WARG_END_MARKER, head + "".join(blocks) + tail + WARG_END_MARKER, 1)))
    report.append("action_sets.xml: add %d sets binding %d clips" % (len(blocks), len(clips)))


def _present_ids(text):
    """The dire wolf ids in the file's live markup: a commented-out row is not installed."""
    live = re.sub(r"<!--.*?-->", "", text, flags=re.S)
    return [i for i in ITEM_NAMES if 'id="%s"' % i in live]


def plan_items(path, report, problems, changed):
    text = read(path)
    present = _present_ids(text)
    if len(present) == len(ITEM_NAMES):
        report.append("LOTRAOM_horses.xml: already present")
        return
    if present:
        problems.append("LOTRAOM_horses.xml: the dire wolf items are partly present: %s" % present)
        return
    if text.count("</Items>") != 1:
        problems.append("ANCHOR: LOTRAOM_horses.xml has no single </Items>")
        return
    changed.append((path, text.replace("</Items>", item_xml(nl_of(text)) + "</Items>", 1)))
    report.append("LOTRAOM_horses.xml: add %d items" % len(ITEM_NAMES))


def plan_loc(path, report, problems, changed):
    text = read(path)
    present = _present_ids(text)
    if len(present) == len(ITEM_NAMES):
        report.append("loc_LOTRAOM_horses.xml: already present")
        return
    if present:
        problems.append("loc_LOTRAOM_horses.xml: the dire wolf rows are partly present: %s" % present)
        return
    found = re.findall(r'[ \t]*<string id="fell_warg"[^>]*/>\r?\n', text)
    if len(found) != 1:
        problems.append("ANCHOR: loc_LOTRAOM_horses.xml has no single fell_warg row")
        return
    nl = nl_of(text)
    rows = "".join('    <string id="%s" text="%s"/>%s' % (i, n, nl) for i, n in ITEM_NAMES.items())
    changed.append((path, text.replace(found[0], found[0] + rows, 1)))
    report.append("loc_LOTRAOM_horses.xml: add %d rows" % len(ITEM_NAMES))


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--game-dir", default=None, help="Bannerlord install root (default: $BANNERLORD_GAME_DIR or the desktop path)")
    args = ap.parse_args(argv)
    if args.game_dir is not None and not args.game_dir.strip():
        print("ERROR: --game-dir is empty; pass the Bannerlord install root or leave --game-dir out")
        return 2
    if args.apply and game_or_kit_running():
        print("REFUSED: Bannerlord or the Modding Kit is running; close it and re-run")
        return 2
    paths = resolve_paths(args.game_dir or game_dir(_DEFAULT_GAME_DIR))
    for key in ("armory", "submodule", "project", "action_sets", "horses", "loc"):
        ensure_exists(paths[key], "the Armory's %s" % key)
    suffix = backup_suffix()

    report, problems, changed = [], [], []
    plan_monster(paths["monster"], report, problems, changed)
    plan_submodule(paths["submodule"], report, problems, changed)
    plan_project(paths["project"], report, problems, changed)
    plan_action_sets(paths["action_sets"], paths["clips_dir"], report, problems, changed)
    plan_items(paths["horses"], report, problems, changed)
    plan_loc(paths["loc"], report, problems, changed)

    for path, text in changed:  # parse every document before writing any
        try:
            write(path, text, apply=False, suffix=suffix)
        except ET.ParseError as exc:
            problems.append("PARSE FAILED: %s: %s" % (os.path.basename(path), exc))
    for line in report:
        print(line)
    if problems:
        for problem in problems:
            print(problem)
        print("refusing to write: every anchor must be found, every bound clip must exist, every file must parse")
        return 1
    names = ", ".join(os.path.basename(p) for p, _ in changed)
    if not changed:
        print("nothing to write: every step is already present")
    elif args.apply:
        for path, text in changed:
            write(path, text, apply=True, suffix=suffix)
        print("APPLIED %d file(s): %s (backups: *%s)" % (len(changed), names, suffix))
    else:
        print("dry run: %d file(s) would change (%s), all parse; nothing written (--apply to write, Kit and game closed)"
              % (len(changed), names))
    return 0


if __name__ == "__main__":
    sys.exit(main())
