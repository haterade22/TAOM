"""Write the Animalia elk and moose into the LIVE LOTRLOME_Armory (#646): Monsters, action sets, Horse items.

    python tools/apply_animalia_armory.py            # dry run: prints what would change, writes nothing
    python tools/apply_animalia_armory.py --apply    # Modding Kit and game CLOSED

The Armory is unversioned (CLAUDE.md "A fix in a dependency module"), so every edit here is recorded in
docs/reference/lotrlome-animalia-changes.md and pinned by TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs.
Built like the great elk (docs/reference/lotrlome-elk-changes.md) with one difference: these animals play their
OWN clips (tools/gen_animalia_anim_clips.ps1), so each gets its own action set, a child of as_horse.

  1. ModuleData/Monsters/LOTR/lotr_monster_animalia.xml (new): taom_animalia_elk / taom_animalia_moose,
     base_monster="horse", action_set as_animalia_elk / as_animalia_moose, taom_body_length (Mike, 2026-09-23,
     "the monster xml should control the size"; Main/Features/MonsterSize).
  2. SubModule.xml: one Monsters registration block after the great elk's.
  3. ModuleData/action_sets.xml: as_animalia_<animal> (base as_horse) overriding the actions the clips fill,
     plus _town_and_village and _map twins. _map is required: MobilePartyVisual looks up ActionSetCode + "_map"
     and MBGlobals.GetActionSet throws on a miss; _town_and_village mirrors vanilla's as_horse_town_and_village,
     which nothing in v1.5.3 appends. Each override copies the vanilla as_horse action's attributes
     (alternative_group) and changes only the animation. Turns, jumps, strafes and quick stops stay vanilla.
  4. ModuleData/LOTRLOME_items/LOTRAOM_horses.xml: taom_animalia_elk_a (mesh animalia_elk_08) and
     taom_animalia_moose_a (animalia_moose_big). Size is NOT authored here: TAOM's MonsterSizeService copies
     each Monster's taom_body_length into every Horse item naming that Monster at game init; these items keep
     body_length="100", the placeholder the engine's Items.xsd requires on <Horse>. They now have riders
     (mirkwood_rochenlas and the elk_rider career start the elk; Mirkwood lords and Thranduil the moose). Both
     are is_merchandise="false", which TAOM's CultureMarketplace does not read: the elk is guaranteed stock
     through its routing (culture_marketplace_config.xml, min_stock 1), and the Mirkwood culture pool can draw
     either animal into a Mirkwood market (Mike, 2026-09-23: the moose may be sold).
  5. The antler attacks TAOM's Main/Features/Animalia fires: act_animalia_elk_antler / act_animalia_moose_antler
     declared once each in ModuleData/action_types.xml as actt_kick (beside act_war_ram_butt; their own actions,
     because the horse usage set fires act_horse_kick itself), and bound in each animal's set to its attack clip.
     A pure function (antler_edits) folds this onto the TEXT steps 1-4 already produced, so it can run inside the
     same compute-everything-first, write-nothing-on-a-problem envelope as steps 1-4, rather than crashing past it.

Byte-faithful (tools/README.md "XML I/O convention", idiom B): BOM and line endings kept, each file parsed after
the edit and refused if it no longer parses, write-once backups beside each edited file with a non-.xml extension
(the engine globs *.xml). Idempotent: a file that already holds its block is left alone. Every animation this
binds must exist as a clip package under Assets/creature/elk/animations, or nothing is written. A step whose
output is partly present (one item of the two, a twin set missing, a Monsters file with one Monster) is a
problem, not "already present": the script inserts whole blocks and never merges into a half-edited file.

Refuses --apply while the game or the Modding Kit is running (tools/_gamedir.game_or_kit_running), before
touching anything. The game install is resolved through tools/_gamedir.py like every other Armory writer; pass
--game-dir to point the whole script at a synthetic tree (tools/tests/test_apply_animalia_armory.py does this).
"""
import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import ensure_exists, game_dir, game_or_kit_running  # noqa: E402

_DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
BACKUP_SUFFIX = ".bak-animalia-20260923"
ANTLER_BACKUP = ".bak-animalia-antler-20260923"


def resolve_paths(root):
    """Every path this script reads or writes, derived from one game-install root. A dict, not
    module globals, so a test can point the whole script at a synthetic tree via --game-dir."""
    root = str(root)
    armory = os.path.join(root, "Modules", "LOTRLOME_Armory")
    return {
        "armory": armory,
        "native_sets": os.path.join(root, "Modules", "Native", "ModuleData", "action_sets.xml"),
        "clips_dir": os.path.join(armory, "Assets", "creature", "elk", "animations"),
        "monsters": os.path.join(armory, "ModuleData", "Monsters", "LOTR", "lotr_monster_animalia.xml"),
        "submodule": os.path.join(armory, "SubModule.xml"),
        "action_sets": os.path.join(armory, "ModuleData", "action_sets.xml"),
        "action_types": os.path.join(armory, "ModuleData", "action_types.xml"),
        "horses": os.path.join(armory, "ModuleData", "LOTRLOME_items", "LOTRAOM_horses.xml"),
    }


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
  Size lives on the Monster's taom_body_length (Main/Features/MonsterSize, Mike, 2026-09-23: "the monster xml
  should control the size"); TAOM copies it into every Horse item naming this Monster at game init, so the
  Horse items keep only the placeholder body_length="100" Items.xsd requires. The rider is not resized.
-->
<Monsters>
	<Monster
		id="taom_animalia_elk"
		base_monster="horse"
		action_set="as_animalia_elk"
		weight="500"
		hit_points="250"
		taom_body_length="100" />
	<Monster
		id="taom_animalia_moose"
		base_monster="horse"
		action_set="as_animalia_moose"
		weight="600"
		hit_points="300"
		taom_body_length="150" />
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

ITEMS = """    <!-- #646 Animalia elk and moose: horse-skeleton reskins of the Fab packs, with their own clips. Their size
         lives on their Monsters (taom_body_length in lotr_monster_animalia.xml: the elk 100, the moose 150), which
         TAOM copies into these items at game init; their body_length="100" is only the placeholder Items.xsd
         requires. is_merchandise="false" keeps them out of vanilla loot, workshop output and tournament prizes, not
         out of TAOM's markets: the elk is guaranteed stock in Mirkwood towns (culture_marketplace_config.xml
         routing, min_stock 1), the Mirkwood culture pool can also draw either animal into a Mirkwood market, and a
         caravan can buy one there (vanilla caravans do not read the flag either). -->
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

# Every id a complete install holds, so a partly present step is caught instead of read as done.
MONSTER_IDS = ("taom_animalia_elk", "taom_animalia_moose")
ITEM_IDS = ("taom_animalia_elk_a", "taom_animalia_moose_a")
SET_IDS = tuple("as_animalia_%s%s" % (a, twin) for a in ("elk", "moose") for twin in ("", "_town_and_village", "_map"))

ANTLER = {"elk": ("act_animalia_elk_antler", "anim_animalia_elk_attack_front_low"),
          "moose": ("act_animalia_moose_antler", "anim_animalia_moose_attack_head_01")}


def read(path):
    raw = open(path, "rb").read()
    return raw.decode("utf-8")          # idiom B: a BOM survives inside the string


def write(path, text, apply, backup=True, suffix=BACKUP_SUFFIX):
    ET.fromstring(text.lstrip("\ufeff"))   # refuse a document that no longer parses
    if not apply:
        return
    if backup and os.path.exists(path) and not os.path.exists(path + suffix):
        with open(path + suffix, "wb") as fh:
            fh.write(open(path, "rb").read())
    with open(path, "wb") as fh:
        fh.write(text.encode("utf-8"))


def nl_of(text):
    """The file's dominant line ending, by majority rather than presence (tools/README.md "XML I/O
    convention"): one stray CRLF in an LF file must not flip every inserted block to CRLF."""
    crlf = text.count("\r\n")
    return "\r\n" if crlf > text.count("\n") - crlf else "\n"


def action_sets_block(nl, native_sets_path):
    native = ET.parse(native_sets_path).getroot()
    horse = next(s for s in native.iter("action_set") if s.get("id") == "as_horse")
    vanilla = {a.get("type"): a.attrib for a in horse.iter("action")}
    lines = ["", "\t<!-- ============================== ANIMALIA ELK + MOOSE (#646, 2026-09-23) ============================== -->",
             "\t<!-- Horse-skeleton reskins of the Fab Animalia packs that play their OWN clips (anim_animalia_*, written by",
             "\t     TAOM tools/gen_animalia_anim_clips.ps1 beside the masters in Assets/creature/elk/animations). Each action",
             "\t     copies the vanilla as_horse action's attributes and changes only the clip. Turns, jumps, strafes and quick",
             "\t     stops stay the horse's. The _map twin is required: MobilePartyVisual looks up ActionSetCode + \"_map\",",
             "\t     and MBGlobals.GetActionSet throws on a miss. The _town_and_village twin mirrors vanilla's",
             "\t     as_horse_town_and_village, which nothing in v1.5.3 appends. -->"]
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


def antler_edits(types_text, sets_text, clips):
    """5. Declare each antler action once (actt_kick, beside act_war_ram_butt) and bind it in its
    animal's set. A PURE function: takes the action_types.xml and action_sets.xml TEXT steps 1-4
    already produced (or the untouched originals, when steps 1-4 found their own blocks already
    present) and `clips`, the set of clip stems that exist on disk. Returns (types_after, sets_after,
    problems). No file I/O, so it can run inside the same compute-everything-first envelope as steps
    1-4, instead of asserting past it: on a pristine Armory the previous version crashed with an
    AssertionError here, because it re-read files from disk rather than the in-memory result of steps
    1-4. Every anchor miss becomes a problem line naming the anchor and its ledger, never an exception.
    Idempotent: an already-bound action is left alone."""
    problems = []
    for _, clip in ANTLER.values():
        if clip not in clips:
            problems.append("CLIP MISSING: %s (refusing the antler step)" % clip)
    if problems:
        return types_text, sets_text, problems

    nl = nl_of(types_text)
    missing = [a for a, _ in ANTLER.values() if 'name="%s"' % a not in types_text]
    types_after = types_text
    if missing:
        anchor = '<action name="act_war_ram_butt" type="actt_kick" />'
        if types_text.count(anchor) != 1:
            problems.append(
                "action_types.xml: anchor %r not found exactly once (the war ram's declaration; "
                "see docs/reference/lotrlome-war-ram-changes.md)" % anchor)
        else:
            i = types_text.index(anchor) + len(anchor)
            block = (nl + "\t<!-- #646 Animalia elk and moose antler attacks, fired by TAOM Main/Features/Animalia (AnimaliaConfig)."
                     + nl + "\t     Typed actt_kick like act_war_ram_butt; their own actions, because the horse usage set fires"
                     + nl + "\t     act_horse_kick itself as its kick_action. -->")
            for a in missing:
                block += nl + '\t<action name="%s" type="actt_kick" />' % a
            types_after = types_text[:i] + block + types_text[i:]

    snl = nl_of(sets_text)
    sets_after = sets_text
    for animal, (action, clip) in ANTLER.items():
        sid = "as_animalia_" + animal
        m = re.search(r'<action_set id="%s" [^>]*>(.*?)</action_set>' % sid, sets_after, re.S)
        if not m:
            problems.append("action_sets.xml: %s not found (run steps 1 to 4 first)" % sid)
            continue
        if 'type="%s"' % action in m.group(1):
            continue  # already bound
        pos = sets_after.rfind("\t</action_set>", m.start(), m.end())
        if pos <= m.start():
            problems.append("action_sets.xml: closing tag of %s not found" % sid)
            continue
        sets_after = sets_after[:pos] + '\t\t<action type="%s" animation="%s" />' % (action, clip) + snl + sets_after[pos:]
    return types_after, sets_after, problems


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--game-dir", default=None,
                    help="Bannerlord install root (default: $BANNERLORD_GAME_DIR or the desktop path)")
    args = ap.parse_args(argv)

    if args.game_dir is not None and not args.game_dir.strip():
        # An unset variable passed as --game-dir "" must not fall back to the live install.
        print('ERROR: --game-dir is empty; pass the Bannerlord install root or leave --game-dir out')
        return 2
    if args.apply and game_or_kit_running():
        print("REFUSED: Bannerlord or the Modding Kit is running; close it and re-run")
        return 2

    root = args.game_dir if args.game_dir is not None else game_dir(_DEFAULT_GAME_DIR)
    paths = resolve_paths(root)
    ensure_exists(paths["armory"], "the LOTRLOME_Armory module")  # a wrong root exits 2, not a traceback

    report, problems = [], []

    clips = set()
    for _, _, files in os.walk(paths["clips_dir"]):
        clips.update(f[:-len("_anm.tpac")] for f in files if f.endswith("_anm.tpac"))
    wanted = {"anim_animalia_%s_%s" % (a, s) for a, o in OVERRIDES.items() for s in o.values()}
    for a in sorted(wanted - clips):
        problems.append("CLIP MISSING: %s" % a)

    # 1. Monsters file
    mon = paths["monsters"]
    if os.path.exists(mon):
        present = read(mon)
        gone = [m for m in MONSTER_IDS if 'id="%s"' % m not in present]
        if gone:
            problems.append("Monsters: lotr_monster_animalia.xml exists but lacks %s; restore it from its backup "
                            "or delete it so the script writes it whole" % ", ".join(gone))
        else:
            report.append("Monsters: lotr_monster_animalia.xml already present, left alone")
        mon_text = None
    else:
        mon_text = MONSTER_FILE
        report.append("Monsters: new lotr_monster_animalia.xml (taom_animalia_elk, taom_animalia_moose)")

    # 2. SubModule.xml
    sub_path = paths["submodule"]
    sub = read(sub_path)
    if "Monsters/LOTR/lotr_monster_animalia" in sub:
        report.append("SubModule.xml: already registered")
        sub_new = None
    else:
        anchor = '<XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_elk"/>'
        i = sub.find(anchor)
        if i < 0:
            problems.append('SubModule.xml: anchor %r not found (the great elk\'s registration; '
                            'see docs/reference/lotrlome-elk-changes.md)' % anchor)
            sub_new = None
        else:
            j = sub.index("</XmlNode>", i) + len("</XmlNode>")
            sub_new = sub[:j] + SUBMODULE_BLOCK.replace("\n", nl_of(sub)) + sub[j:]
            report.append("SubModule.xml: Monsters block inserted after the great elk's")

    # 3. action_sets.xml
    as_path = paths["action_sets"]
    sets = read(as_path)
    present_sets = [s for s in SET_IDS if 'id="%s"' % s in sets]
    if len(present_sets) == len(SET_IDS):
        report.append("action_sets.xml: Animalia sets already present")
        sets_after14 = sets
    elif present_sets:
        problems.append("action_sets.xml: the Animalia sets are half present (missing %s); restore the file from "
                        "its backup rather than inserting a second block" % ", ".join(s for s in SET_IDS if s not in present_sets))
        sets_after14 = sets
    else:
        block, bad = action_sets_block(nl_of(sets), paths["native_sets"])
        for b in bad:
            problems.append("NOT AN ACTION: %s" % b)
        anchor = "\t<!-- ============================== /WARG ============================== -->"
        if sets.count(anchor) != 1:
            problems.append("action_sets.xml: anchor for the /WARG marker not found exactly once "
                            "(expected right after the war ram's action sets)")
            sets_after14 = sets
        else:
            k = sets.index(anchor)
            sets_after14 = sets[:k] + block.lstrip("\r\n") + nl_of(sets) + sets[k:]
            report.append("action_sets.xml: as_animalia_elk (%d actions), as_animalia_moose (%d actions) + twins, before the /WARG marker"
                          % (len(OVERRIDES["elk"]), len(OVERRIDES["moose"])))

    # 4. horses
    hp = paths["horses"]
    horses = read(hp)
    present_items = [i for i in ITEM_IDS if 'id="%s"' % i in horses]
    if len(present_items) == len(ITEM_IDS):
        report.append("LOTRAOM_horses.xml: items already present")
        horses_new = None
    elif present_items:
        problems.append("LOTRAOM_horses.xml: only %s present (missing %s); restore the file from its backup"
                        % (", ".join(present_items), ", ".join(i for i in ITEM_IDS if i not in present_items)))
        horses_new = None
    else:
        m = re.search(r'<Item\s+id="taom_elk_saddle_a".*?</Item>', horses, re.S)
        if not m:
            problems.append('LOTRAOM_horses.xml: anchor "taom_elk_saddle_a" not found (the great elk\'s '
                            'saddle item; see docs/reference/lotrlome-elk-changes.md)')
            horses_new = None
        else:
            pos = m.end()
            nl = nl_of(horses)
            horses_new = horses[:pos] + nl + ITEMS.rstrip("\n").replace("\n", nl) + horses[pos:]
            report.append("LOTRAOM_horses.xml: taom_animalia_elk_a + taom_animalia_moose_a after taom_elk_saddle_a")

    # 5. antler: pure, folds onto whatever steps 1-4 produced in memory (never re-reads from disk)
    at_path = paths["action_types"]
    types = read(at_path)
    types_after, sets_final, antler_problems = antler_edits(types, sets_after14, clips)
    problems.extend(antler_problems)
    # Report what the files WILL hold. antler_edits returns its input unchanged when it has a problem, so
    # "unchanged" is not "already there": a refused run must not claim the actions exist.
    undeclared = [a for a, _ in ANTLER.values() if 'name="%s"' % a not in types]
    if not undeclared:
        report.append("action_types.xml: antler actions already declared")
    elif types_after != types:
        report.append("action_types.xml: declares %s as actt_kick after act_war_ram_butt" % ", ".join(undeclared))
    else:
        report.append("action_types.xml: %s NOT declared (see the problems below)" % ", ".join(undeclared))
    for animal, (action, _) in ANTLER.items():
        sid = "as_animalia_" + animal
        tag = 'type="%s"' % action
        state = "already binds" if tag in sets_after14 else "binds" if tag in sets_final else "does NOT bind"
        report.append("action_sets.xml: %s %s %s" % (sid, state, action))

    for line in report:
        print(line)
    if problems:
        for p in problems:
            print(p)
        print("refusing to write: every anchor must be found, every bound clip must exist, and every action must be an as_horse action")
        return 1

    # Everything computed; parse every changed document before writing anything.
    changed = []
    if mon_text is not None:
        changed.append((mon, mon_text, BACKUP_SUFFIX))
    if sets_final != sets:
        suffix = BACKUP_SUFFIX if sets_after14 != sets else ANTLER_BACKUP
        changed.append((as_path, sets_final, suffix))
    if types_after != types:
        changed.append((at_path, types_after, ANTLER_BACKUP))
    if horses_new is not None:
        changed.append((hp, horses_new, BACKUP_SUFFIX))
    if sub_new is not None:
        changed.append((sub_path, sub_new, BACKUP_SUFFIX))

    for path, text, suffix in changed:
        write(path, text, apply=False, suffix=suffix)

    names = ", ".join(os.path.basename(path) for path, _, _ in changed)
    if not changed:
        print("nothing to write: every step is already present")
    elif args.apply:
        for path, text, suffix in changed:
            write(path, text, apply=True, suffix=suffix)
        print("APPLIED %d file(s): %s (backups: *%s, antler-only changes *%s)" % (len(changed), names, BACKUP_SUFFIX, ANTLER_BACKUP))
    else:
        print("dry run: %d file(s) would change (%s), all parse; nothing written (--apply to write, Kit and game closed)"
              % (len(changed), names))
    return 0


if __name__ == "__main__":
    sys.exit(main())
