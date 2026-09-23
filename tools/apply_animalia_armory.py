"""Write the Animalia elk and moose into the LIVE LOTRLOME_Armory (#646): Monsters, action sets, Horse items.

    python tools/apply_animalia_armory.py            # dry run: prints what would change, writes nothing
    python tools/apply_animalia_armory.py --apply    # Modding Kit and game CLOSED

The Armory is unversioned (CLAUDE.md "A fix in a dependency module"), so every edit here is recorded in
docs/reference/lotrlome-animalia-changes.md and pinned by TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs.
Built like the great elk (docs/reference/lotrlome-elk-changes.md) with one difference: these animals play their
OWN clips (tools/gen_animalia_anim_clips.ps1), so each gets its own action set, a child of as_horse.

  1. ModuleData/Monsters/LOTR/lotr_monster_animalia.xml (new): taom_animalia_elk / taom_animalia_moose,
     base_monster="horse", action_set as_animalia_elk / as_animalia_moose.
  2. SubModule.xml: one Monsters registration block after the great elk's.
  3. ModuleData/action_sets.xml: as_animalia_<animal> (base as_horse) overriding the actions the clips fill,
     plus _town_and_village and _map twins (MobilePartyVisual asks for ActionSetCode + "_map";
     MBGlobals.GetActionSet throws on a miss). Each override copies the vanilla as_horse action's attributes
     (alternative_group) and changes only the animation. Turns, jumps, strafes and quick stops stay vanilla.
  4. ModuleData/LOTRLOME_items/LOTRAOM_horses.xml: taom_animalia_elk_a (mesh animalia_elk_08) and
     taom_animalia_moose_a (animalia_moose_big), body_length 100 (the authored horse size), not merchandise yet.

Byte-faithful (tools/README.md "XML I/O convention", idiom B): BOM and line endings kept, each file parsed after
the edit and refused if it no longer parses, write-once backups beside each edited file with a non-.xml extension
(the engine globs *.xml). Idempotent: a file that already holds its block is left alone. Every animation this
binds must exist as a clip package under Assets/creature/elk/animations, or nothing is written.
"""
import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET

GAME = os.environ.get("BANNERLORD_GAME_DIR") or r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
ARMORY = os.path.join(GAME, "Modules", "LOTRLOME_Armory")
NATIVE_SETS = os.path.join(GAME, "Modules", "Native", "ModuleData", "action_sets.xml")
CLIPS_DIR = os.path.join(ARMORY, "Assets", "creature", "elk", "animations")
BACKUP_SUFFIX = ".bak-animalia-20260923"

GAITS = {
    "act_horse_forward_walk": "loco_walk", "act_horse_forward_walk_stand": "loco_walk_stand",
    "act_horse_forward_trot": "loco_trot", "act_horse_forward_trot_stand": "loco_trot_stand",
    "act_horse_forward_canter": "loco_run", "act_horse_forward_canter_stand": "loco_run_stand",
    "act_horse_forward_gallop_right_foot": "loco_sprint", "act_horse_forward_gallop_right_foot_stand": "loco_sprint_stand",
    "act_horse_forward_gallop_left_foot": "loco_sprint", "act_horse_forward_gallop_left_foot_stand": "loco_sprint_stand",
    "act_horse_backward_walk": "loco_walkback", "act_horse_backward_walk_stand": "loco_walkback_stand",
}
OVERRIDES = {
    "elk": dict(GAITS, **{
        "act_horse_stand_for_movement_data": "stand_01_movement",
        "act_horse_stand_1": "stand_01", "act_horse_stand_2": "stand_02", "act_horse_stand_3": "stand_03",
        "act_horse_stand_4": "alert_looking_l",
        "act_horse_idle_1": "stand_01", "act_horse_idle_2": "alert_looking_r", "act_horse_idle_3": "stand_02",
        "act_horse_idle_4": "stand_03",
        "act_horse_riderless_idle_1": "stand_eating_01", "act_horse_riderless_idle_2": "stand_eating_02",
        "act_horse_riderless_idle_3": "stand_drinking_01", "act_horse_riderless_idle_4": "vocalization_bugling",
        "act_horse_rear": "attack_front_high", "act_horse_kick": "attack_hind",
        "act_horse_strike_front": "hit_chestl_heavy", "act_horse_strike_back": "hit_pelvisl_heavy",
        "act_horse_fall_left": "death_stand_l", "act_horse_fall_left_continue": "death_stand_l_pose",
        "act_horse_fall_right": "death_stand_r", "act_horse_fall_right_continue": "death_stand_r_pose",
    }),
    "moose": dict(GAITS, **{
        "act_horse_stand_for_movement_data": "stand_00_movement",
        "act_horse_stand_1": "stand_00", "act_horse_stand_2": "stand_01", "act_horse_stand_3": "stand_02",
        "act_horse_stand_4": "stand_00",
        "act_horse_idle_1": "stand_00", "act_horse_idle_2": "stand_01", "act_horse_idle_3": "stand_02",
        "act_horse_idle_4": "stand_01",
        "act_horse_riderless_idle_1": "eating_01", "act_horse_riderless_idle_2": "eating_02",
        "act_horse_riderless_idle_3": "drinking_01", "act_horse_riderless_idle_4": "eating_03",
        "act_horse_kick": "attack_legs_01",
        "act_horse_fall_left": "death_l", "act_horse_fall_left_continue": "death_l_pose",
        "act_horse_fall_right": "death_r", "act_horse_fall_right_continue": "death_r_pose",
    }),
}

MONSTER_FILE = """<?xml version="1.0" encoding="utf-8"?>
<!--
  TAOM Animalia elk and moose Monsters (issue #646, TAOM docs/features/animalia-elk-moose.md).

  Two Fab "Animalia" packs reskinned onto the STOCK VANILLA HORSE SKELETON by TAOM's
  tools/blender/reskin_animalia_to_horse.py, so each Monster is the vanilla horse shape (base_monster="horse"
  inherits Flags, family_type, monster_usage="horse", num_paces, every bone, the slope block and the rein
  attributes, as the war ram's and the great elk's do). Unlike those two, these animals play their OWN clips,
  retargeted onto horse_skeleton, so each has its own action set, a child of as_horse in action_sets.xml.

  The Animalia elk is a separate animal from the great elk (taom_elk, #636); the moose is Mirkwood's.
  weight / hit_points: the elk as the great elk (500 / 250), the moose heavier (600 / 300).
  Size lives on the Horse item (body_length); the rider is not resized.
-->
<Monsters>
	<Monster
		id="taom_animalia_elk"
		base_monster="horse"
		action_set="as_animalia_elk"
		weight="500"
		hit_points="250" />
	<Monster
		id="taom_animalia_moose"
		base_monster="horse"
		action_set="as_animalia_moose"
		weight="600"
		hit_points="300" />
</Monsters>
"""

SUBMODULE_BLOCK = """
			<!-- #646 Animalia elk and moose: horse-skeleton reskins with their own action sets
			     (as_animalia_elk / as_animalia_moose in action_sets.xml). -->
			<XmlNode>
				<XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_animalia"/>
				<IncludedGameTypes>
					<GameType value = "Campaign"/>
					<GameType value = "CampaignStoryMode"/>
					<GameType value = "CustomGame"/>
					<GameType value = "EditorGame"/>
				</IncludedGameTypes>
			</XmlNode>"""

ITEMS = """    <!-- #646 Animalia elk and moose: horse-skeleton reskins of the Fab packs, with their own clips. body_length 100
         is the authored size (both meshes were fitted to the horse); not merchandise until they have riders. -->
    <Item
        id="taom_animalia_elk_a"
        name="{=taom_animalia_elk_a}Mirkwood Elk"
        mesh="animalia_elk_08"
        culture="Culture.mirkwood"
        subtype="horse"
        item_category="war_horse"
        value="1400"
        weight="450"
        difficulty="0"
        is_merchandise="false"
        Type="Horse">
        <ItemComponent>
            <Horse
                monster="Monster.taom_animalia_elk"
                maneuver="74"
                speed="62"
                charge_damage="50"
                body_length="100"
                is_mountable="true"
                extra_health="20" />
        </ItemComponent>
        <Flags Civilian="true" />
    </Item>
    <Item
        id="taom_animalia_moose_a"
        name="{=taom_animalia_moose_a}Mirkwood Moose"
        mesh="animalia_moose_big"
        culture="Culture.mirkwood"
        subtype="horse"
        item_category="war_horse"
        value="1800"
        weight="550"
        difficulty="0"
        is_merchandise="false"
        Type="Horse">
        <ItemComponent>
            <Horse
                monster="Monster.taom_animalia_moose"
                maneuver="60"
                speed="56"
                charge_damage="65"
                body_length="100"
                is_mountable="true"
                extra_health="40" />
        </ItemComponent>
        <Flags Civilian="true" />
    </Item>
"""


def read(path):
    raw = open(path, "rb").read()
    return raw.decode("utf-8")          # idiom B: a BOM survives inside the string


def write(path, text, apply, backup=True):
    ET.fromstring(text.lstrip("\ufeff"))   # refuse a document that no longer parses
    if not apply:
        return
    if backup and os.path.exists(path) and not os.path.exists(path + BACKUP_SUFFIX):
        with open(path + BACKUP_SUFFIX, "wb") as fh:
            fh.write(open(path, "rb").read())
    with open(path, "wb") as fh:
        fh.write(text.encode("utf-8"))


def nl_of(text):
    return "\r\n" if "\r\n" in text else "\n"


def action_sets_block(nl):
    native = ET.parse(NATIVE_SETS).getroot()
    horse = next(s for s in native.iter("action_set") if s.get("id") == "as_horse")
    vanilla = {a.get("type"): a.attrib for a in horse.iter("action")}
    lines = ["", "\t<!-- ============================== ANIMALIA ELK + MOOSE (#646, 2026-09-23) ============================== -->",
             "\t<!-- Horse-skeleton reskins of the Fab Animalia packs that play their OWN clips (anim_animalia_*, written by",
             "\t     TAOM tools/gen_animalia_anim_clips.ps1 beside the masters in Assets/creature/elk/animations). Each action",
             "\t     copies the vanilla as_horse action's attributes and changes only the clip. Turns, jumps, strafes and quick",
             "\t     stops stay the horse's. The _map and _town_and_village twins exist because the engine derives them by",
             "\t     suffix from the Monster's action_set; they inherit the horse variants. -->"]
    missing = []
    for animal in ("elk", "moose"):
        sid = "as_animalia_" + animal
        lines.append('\t<action_set id="%s" skeleton="horse_skeleton" base_set="as_horse">' % sid)
        for atype, stem in OVERRIDES[animal].items():
            if atype not in vanilla:
                missing.append("%s: %s is not an as_horse action" % (sid, atype))
                continue
            attrs = dict(vanilla[atype])
            attrs["animation"] = "anim_animalia_%s_%s" % (animal, stem)
            ordered = ["type", "animation"] + [k for k in attrs if k not in ("type", "animation")]
            lines.append("\t\t<action " + " ".join('%s="%s"' % (k, attrs[k]) for k in ordered) + " />")
        lines.append("\t</action_set>")
        lines.append('\t<action_set id="%s_town_and_village" skeleton="horse_skeleton" base_set="as_horse_town_and_village" />' % sid)
        lines.append('\t<action_set id="%s_map" skeleton="horse_skeleton" base_set="as_horse_map" />' % sid)
    lines.append("\t<!-- ============================== /ANIMALIA ============================== -->")
    return nl.join(lines), missing


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()
    report = []

    clips = set()
    for root, _, files in os.walk(CLIPS_DIR):
        clips.update(f[:-len("_anm.tpac")] for f in files if f.endswith("_anm.tpac"))
    wanted = {"anim_animalia_%s_%s" % (a, s) for a, o in OVERRIDES.items() for s in o.values()}
    absent = sorted(wanted - clips)

    # 1. Monsters file
    mon = os.path.join(ARMORY, "ModuleData", "Monsters", "LOTR", "lotr_monster_animalia.xml")
    if os.path.exists(mon):
        report.append("Monsters: lotr_monster_animalia.xml already present, left alone")
        mon_text = None
    else:
        mon_text = MONSTER_FILE
        report.append("Monsters: new lotr_monster_animalia.xml (taom_animalia_elk, taom_animalia_moose)")

    # 2. SubModule.xml
    sub_path = os.path.join(ARMORY, "SubModule.xml")
    sub = read(sub_path)
    if "Monsters/LOTR/lotr_monster_animalia" in sub:
        report.append("SubModule.xml: already registered")
        sub_new = None
    else:
        anchor = '<XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_elk"/>'
        i = sub.index(anchor)
        j = sub.index("</XmlNode>", i) + len("</XmlNode>")
        sub_new = sub[:j] + SUBMODULE_BLOCK.replace("\n", nl_of(sub)) + sub[j:]
        report.append("SubModule.xml: Monsters block inserted after the great elk's")

    # 3. action_sets.xml
    as_path = os.path.join(ARMORY, "ModuleData", "action_sets.xml")
    sets = read(as_path)
    if 'id="as_animalia_elk"' in sets:
        report.append("action_sets.xml: Animalia sets already present")
        sets_new, bad = None, []
    else:
        block, bad = action_sets_block(nl_of(sets))
        anchor = "\t<!-- ============================== /WARG ============================== -->"
        assert sets.count(anchor) == 1, "anchor after the war ram sets not found exactly once"
        k = sets.index(anchor)
        sets_new = sets[:k] + block.lstrip("\r\n") + nl_of(sets) + sets[k:]
        report.append("action_sets.xml: as_animalia_elk (%d actions), as_animalia_moose (%d actions) + twins, before the /WARG marker"
                      % (len(OVERRIDES["elk"]), len(OVERRIDES["moose"])))

    # 4. horses
    hp = os.path.join(ARMORY, "ModuleData", "LOTRLOME_items", "LOTRAOM_horses.xml")
    horses = read(hp)
    if 'id="taom_animalia_elk_a"' in horses:
        report.append("LOTRAOM_horses.xml: items already present")
        horses_new = None
    else:
        m = re.search(r'<Item\s+id="taom_elk_saddle_a".*?</Item>', horses, re.S)
        assert m, "taom_elk_saddle_a not found: the items go after it"
        pos = m.end()
        nl = nl_of(horses)
        horses_new = horses[:pos] + nl + ITEMS.rstrip("\n").replace("\n", nl) + horses[pos:]
        report.append("LOTRAOM_horses.xml: taom_animalia_elk_a + taom_animalia_moose_a after taom_elk_saddle_a")

    for line in report:
        print(line)
    if bad or absent:
        for b in bad:
            print("NOT AN ACTION:", b)
        for a in absent:
            print("CLIP MISSING:", a)
        print("refusing to write: every bound clip must exist and every action must be an as_horse action")
        return 1
    # parse everything before writing anything
    for path, text in ((mon, mon_text), (sub_path, sub_new), (as_path, sets_new), (hp, horses_new)):
        if text is not None:
            write(path, text, apply=False)
    if args.apply:
        for path, text in ((sub_path, sub_new), (as_path, sets_new), (hp, horses_new)):
            if text is not None:
                write(path, text, apply=True)
        if mon_text is not None:
            with open(mon, "wb") as fh:
                fh.write(mon_text.encode("utf-8"))
        print("APPLIED (backups: *%s)" % BACKUP_SUFFIX)
    else:
        print("dry run: all four documents parse; nothing written (--apply to write, Kit and game closed)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
