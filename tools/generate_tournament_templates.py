#!/usr/bin/env python3
"""Generate each town-holding culture's own tournament team templates (#422, part 1).

Vanilla's FightTournamentGame opens the arena mission with the settlement's current culture, and
TournamentFightMissionController.GetTeamWeaponEquipmentList picks one of that culture's
tournament_team_templates_{one,two,four}_participant NPCCharacters at random and gives roster i of it
to team member i (BattleEquipments only; slots 5-9 come from the fighter's own armour, GetParticipantArmor).
So a template is a list of weapon rosters, one per team member, and the mission needs at least as many
rosters as the team has members.

Every TAOM culture used to list vanilla's tournament_template_empire_* (or the aserai, sturgia, vlandia
twin), and the six XSLT-renamed vanilla cultures still list vanilla's Calradian ones. This script derives
each culture's rosters from the weapons its own troops carry (troops/*.xml), so a Gondor tournament uses
Gondor swords, shields, bows and horses, and writes one file of templates:

    Main/_Module/ModuleData/characters/taom_tournament_templates.xml

Shape, mirrored from vanilla: per culture 3 one-participant sets (1 roster each), 3 two-participant sets
(2 rosters) and 3 four-participant sets (4 rosters); each roster is weapon slots only (Item0..Item3, plus
Horse and HorseHarness for a rider); a civilian EquipmentSet reference; level 6, the vanilla skill template.

Choosing the weapons. A troop roster is an observed, working kit (a shield troop never carries a polearm
the shield audit forbids, a dwarf carries no horse, an orc no elven bow), so a template roster is a copy of
one, never a new mix. Each battle roster of each troop of the culture is classed by shape:
    mounted  has a Horse            archer   has a bow or crossbow and its ammunition
    onehand  has a shield, or only one-handed melee weapons (sword and board, a lone axe)
    twohand  any other melee roster: a two-hander or a polearm without a shield
Within a class the kits are ranked by how many troops carry them (the common kit is the culture's
everyday weapon), then spread so the first few differ in their main weapon. The plans below hand the kits
out round-robin, and a class the culture lacks falls back to another (a dwarf culture has no mounted
sets: its riders become two-hand or one-hand kits). Arrows and bolts become vanilla's blunt tournament_arrows and
tournament_bolts, as in every vanilla set. Melee weapons stay the troop's own: the Armory ships no blunt
sparring twin of them (crafted items).

Usage:
    python tools/generate_tournament_templates.py            # dry run, prints the summary
    python tools/generate_tournament_templates.py --apply    # write the file
    python tools/generate_tournament_templates.py --check    # exit 1 when the file is stale

Exit codes: 0 ok, 1 stale (--check), 2 no Armory, 3 a culture has no usable kit, 4 no LOTRLOME_items folder.
Follow-up gate after --apply: python tools/audit_polearm_shield_parity.py (no shield beside a weapon the AI drops).

Needs the installed game (the item classes come from the Armory and SandBoxCore): BANNERLORD_GAME_DIR.
"""
from __future__ import annotations

import argparse
import collections
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple

REPO_ROOT = Path(__file__).resolve().parent.parent
MODULE_DATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
TROOPS_DIR = MODULE_DATA / "troops"
SPCULTURES = MODULE_DATA / "taom_spcultures.xml"
OUTPUT_FILE = MODULE_DATA / "characters" / "taom_tournament_templates.xml"

DEFAULT_GAME_ROOT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

DWARF_CULTURES = ("erebor", "eredluin")

# The six cultures spcultures.xslt renames keep the vanilla id.
RENAMED = ("empire", "aserai", "vlandia", "khuzait", "sturgia", "battania")

# Culture id -> the troop cultures whose kits it uses, where its own troops are missing or shared. Khand
# (battania) recruits the Rhun roster (spcultures.xslt, "Khand still has no roster of its own"); Abanissa
# recruits harad_* (Culture.aserai); the Blue Craig and Misty Mountain orcs recruit goblin_* (troops_goblin.xml).
KIT_SOURCES: Dict[str, Tuple[str, ...]] = {
    "battania": ("khuzait",),
    "abanissa": ("aserai",),   # Harad troops are Culture.aserai; generate_enlistment_rosters.py TREE_ALIASES agrees
    "bluecraig": ("goblin", "bluecraig"),
    "mistymountainorcs": ("goblin", "mistymountainorcs"),
}

# The vanilla culture whose civilian set, skill template and body key the old templates used, so nothing
# but the weapons changes. Anything not listed was on the empire templates.
VANILLA_TWIN: Dict[str, str] = {
    "shaghana": "aserai", "abanissa": "aserai",
    "anduinvale": "sturgia", "angaladh": "sturgia", "dorwinion": "sturgia",
    "southrhovanion": "vlandia",
    "empire": "empire", "aserai": "aserai", "vlandia": "vlandia",
    "khuzait": "khuzait", "sturgia": "sturgia", "battania": "battania",
}

# Roster classes handed out per set, one entry per team member. Three sets per team size.
PLANS: Dict[str, List[List[str]]] = {
    "one": [["onehand"], ["twohand"], ["archer"]],
    "two": [["onehand", "onehand"], ["onehand", "archer"], ["twohand", "mounted"]],
    "four": [["onehand", "twohand", "archer", "mounted"],
             ["onehand", "onehand", "twohand", "mounted"],
             ["twohand", "archer", "onehand", "mounted"]],
}
TEAM_SIZE = {"one": 1, "two": 2, "four": 4}

# A class the culture has no kit of falls back to these, in order.
FALLBACKS: Dict[str, Tuple[str, ...]] = {
    "onehand": ("twohand", "archer", "mounted"),
    "twohand": ("onehand", "archer", "mounted"),
    "archer": ("onehand", "twohand", "mounted"),
    "mounted": ("twohand", "onehand", "archer"),
}

MELEE_CLASSES = ("OneHanded", "TwoHanded", "Polearm")
WEAPON_SLOTS = ("Item0", "Item1", "Item2", "Item3")
AMMO = {"Arrows": "Item.tournament_arrows", "Bolts": "Item.tournament_bolts"}
LAUNCHER_AMMO = {"Bow": "Arrows", "Crossbow": "Bolts"}
MAX_VARIANTS = 6

Kit = Tuple[Tuple[Tuple[str, str], ...], str]   # ((slot, item id), ...), class


def bare(item_ref: str) -> str:
    return item_ref[len("Item."):] if item_ref.startswith("Item.") else item_ref


def troop_rosters(npc: ET.Element) -> List[List[Tuple[str, str]]]:
    """The battle rosters of one NPCCharacter as lists of (slot, item ref), the shared mount slots added."""
    equipments = npc.find("Equipments")
    if equipments is None:
        return []
    shared = [(e.get("slot"), e.get("id")) for e in equipments.findall("equipment")
              if e.get("slot") in ("Horse", "HorseHarness")]
    rosters = []
    for roster in equipments.findall("EquipmentRoster"):
        if roster.get("civilian") == "true":
            continue
        rows = [(e.get("slot"), e.get("id")) for e in roster.findall("equipment")]
        rows += [row for row in shared if row[0] not in {r[0] for r in rows}]
        rosters.append(rows)
    return rosters


def classify(rows: Sequence[Tuple[str, str]], classes: Dict[str, str],
             banned_mounts: frozenset = frozenset()) -> Optional[Kit]:
    """The (slot items, class) of one roster, or None when it is no usable tournament kit.

    Weapon slots only; arrows and bolts become the tournament ones, a second quiver is dropped. A roster
    holding an item the class registry does not know in a weapon slot (a banner, a tool) is no kit, and
    neither is one whose mount is banned (a creature, or a dwarf ram a rider cannot sit).
    """
    slots = []
    seen_ammo = set()
    kinds = set()
    horse = None
    harness = None
    for slot, ref in rows:
        item = bare(ref)
        if slot == "Horse":
            horse = ref
        elif slot == "HorseHarness":
            harness = ref
        elif slot in WEAPON_SLOTS:
            kind = classes.get(item)
            if kind is None:
                return None
            kinds.add(kind)
            if kind in AMMO:
                if kind in seen_ammo:
                    continue
                seen_ammo.add(kind)
                ref = AMMO[kind]
            slots.append((slot, ref))
    if not slots:
        return None
    if horse is not None and bare(horse) in banned_mounts:
        return None
    if "Shield" in kinds and "TwoHanded" in kinds:
        return None   # a two-hander has no one-handed mode: the AI drops it beside a shield
    launchers = [k for k in kinds if k in LAUNCHER_AMMO]
    if any(LAUNCHER_AMMO[k] not in seen_ammo for k in launchers):
        return None
    if not (kinds & set(MELEE_CLASSES) or launchers):
        return None
    if horse is not None:
        cls = "mounted"
        slots.append(("Horse", horse))
        if harness is not None:
            slots.append(("HorseHarness", harness))
    elif launchers:
        cls = "archer"
    elif "Shield" in kinds or not (kinds & {"TwoHanded", "Polearm"}):
        cls = "onehand"
    else:
        cls = "twohand"
    # keep the weapon slots contiguous from Item0, as the engine and vanilla expect
    ordered = sorted((s for s in slots if s[0] in WEAPON_SLOTS), key=lambda s: s[0])
    renumbered = [(WEAPON_SLOTS[i], ref) for i, (_, ref) in enumerate(ordered)]
    renumbered += [s for s in slots if s[0] not in WEAPON_SLOTS]
    return tuple(renumbered), cls


def rank_variants(kit_counts: "collections.Counter[Tuple[Tuple[Tuple[str, str], ...], str]]") -> Dict[str, List[tuple]]:
    """{class: kits}, most carried first, then spread so the first kits differ in their main weapon."""
    by_class: Dict[str, List[Tuple[int, tuple]]] = collections.defaultdict(list)
    for (slots, cls), count in kit_counts.items():
        by_class[cls].append((count, slots))
    ranked: Dict[str, List[tuple]] = {}
    for cls, rows in by_class.items():
        rows.sort(key=lambda r: (-r[0], r[1]))
        ordered = [slots for _, slots in rows]
        first, rest, seen = [], [], set()
        for slots in ordered:
            main = slots[0][1]
            (rest if main in seen else first).append(slots)
            seen.add(main)
        ranked[cls] = (first + rest)[:MAX_VARIANTS]
    return ranked


class Chooser:
    """Hands the ranked kits of a culture out round-robin, per class, with the class fallbacks."""

    def __init__(self, variants: Dict[str, List[tuple]]):
        self.variants = variants
        self.next_index = collections.Counter()
        if not any(variants.values()):
            raise ValueError("no usable kit")

    def pick(self, cls: str) -> tuple:
        for candidate in (cls,) + FALLBACKS[cls]:
            pool = self.variants.get(candidate)
            if pool:
                kit = pool[self.next_index[candidate] % len(pool)]
                self.next_index[candidate] += 1
                return kit
        raise ValueError("no usable kit")


def culture_kits(culture: str, troops_by_culture: Dict[str, List[ET.Element]], classes: Dict[str, str],
                 banned_mounts: frozenset = frozenset()) -> "collections.Counter":
    counts: collections.Counter = collections.Counter()
    for source in KIT_SOURCES.get(culture, (culture,)):
        for npc in troops_by_culture.get(source, []):
            for rows in troop_rosters(npc):
                kit = classify(rows, classes, banned_mounts)
                if kit is not None:
                    counts[kit] += 1
    return counts


def build_templates(culture: str, variants: Dict[str, List[tuple]]) -> List[Tuple[str, List[tuple]]]:
    """[(template id, rosters)] for one culture: three sets per team size."""
    chooser = Chooser(variants)
    out = []
    for size in ("one", "two", "four"):
        for number, plan in enumerate(PLANS[size], start=1):
            out.append((template_id(culture, size, number), [chooser.pick(cls) for cls in plan]))
    return out


def template_id(culture: str, size: str, number: int) -> str:
    return f"taom_tournament_{culture}_{size}_participant_set_v{number}"


def render(cultures: Dict[str, List[Tuple[str, List[tuple]]]]) -> str:
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        '<!-- GENERATED by tools/generate_tournament_templates.py (#422): do not edit by hand, run the script.',
        '     Each town-holding culture\'s tournament team templates: weapon rosters copied from the kits its own',
        '     troops carry, in vanilla\'s shape (3 sets each of 1, 2 and 4 participants; roster i is team member i).',
        '     taom_spcultures.xml lists them for the TAOM cultures; spcultures.xslt for the six renamed vanilla',
        '     ones. Arrows and bolts are vanilla\'s blunt tournament ones. The name is never shown (a template is no troop);',
        '     the shared {=!} key keeps the name-key check to one English text. See docs/features/tournament-templates.md. -->',
        '<NPCCharacters>',
    ]
    for culture, templates in cultures.items():
        twin = VANILLA_TWIN.get(culture, "empire")
        lines.append(f'  <!-- {culture} -->')
        for tid, rosters in templates:
            lines += [
                '  <NPCCharacter',
                f'      id="{tid}"',
                '      default_group="Infantry"',
                '      level="6"',
                '      occupation="NotAssigned"',
                '      name="{=!}Tournament Fighter"',
                f'      culture="Culture.{culture}"',
                *(['      race="dwarf"'] if culture in DWARF_CULTURES else []),
                '      skill_template="SkillSet.infantry_heavyinfantry_level6_template_skills">',
                '    <face>',
                f'      <face_key_template value="BodyProperty.fighter_{twin}" />',
                '    </face>',
                '    <upgrade_targets></upgrade_targets>',
                '    <Equipments>',
            ]
            for slots in rosters:
                lines.append('      <EquipmentRoster>')
                for slot, ref in slots:
                    lines.append(f'        <equipment slot="{slot}" id="{ref}" />')
                lines.append('      </EquipmentRoster>')
            if culture in DWARF_CULTURES:
                # a vanilla civilian set is a human's clothes (#1431): a dwarf wears an inline dwarf tunic
                lines += [
                    '      <EquipmentRoster civilian="true">',
                    '        <equipment slot="Body" id="Item.sk_dwarf_tunic_normal_a" />',
                    '        <equipment slot="Leg" id="Item.sk_dwarf_erebor_boots_light_a" />',
                    '      </EquipmentRoster>',
                ]
            else:
                lines.append(f'      <EquipmentSet id="npc_wanderer_equipment_template_{twin}_civilian" equipmentType="Civilian" />')
            lines += [
                '    </Equipments>',
                '  </NPCCharacter>',
            ]
    lines += ['</NPCCharacters>', '']
    return "\n".join(lines)


def town_cultures(spcultures: Path) -> List[str]:
    """The TAOM cultures that carry tournament team templates today, then the six renamed ones."""
    root = ET.parse(str(spcultures)).getroot()
    own = [c.get("id") for c in root.iter("Culture") if c.find("tournament_team_templates_one_participant") is not None]
    return own + list(RENAMED)


def load_troops(troops_dir: Path) -> Dict[str, List[ET.Element]]:
    by_culture: Dict[str, List[ET.Element]] = collections.defaultdict(list)
    for path in sorted(troops_dir.glob("*.xml")):
        for npc in ET.parse(str(path)).getroot().iter("NPCCharacter"):
            culture = (npc.get("culture") or "").replace("Culture.", "")
            if culture:
                by_culture[culture].append(npc)
    return by_culture


class NoUsableKit(Exception):
    def __init__(self, culture: str):
        super().__init__(f"{culture}: no usable kit")
        self.culture = culture


def template_count(text: str) -> int:
    return text.count("<NPCCharacter\n")


def generate(troops_by_culture, classes, cultures: Sequence[str], banned_mounts: frozenset = frozenset()) -> str:
    """The templates file text; raises NoUsableKit naming the first culture none of whose troops give a kit."""
    result = {}
    for culture in cultures:
        variants = rank_variants(culture_kits(culture, troops_by_culture, classes, banned_mounts))
        try:
            result[culture] = build_templates(culture, variants)
        except ValueError as exc:
            raise NoUsableKit(culture) from exc
    text = render(result)
    ET.fromstring(text.encode("utf-8"))   # never write a file the engine cannot parse
    return text


def banned_mount_ids(modules: Path) -> frozenset:
    """Creature mounts a rider cannot use plus the dwarf ram family (Patch46 strips a dwarf's mount)."""
    import generate_enlistment_rosters as ger
    import taom_schema as ts
    return frozenset(ger.creature_mount_item_ids(str(modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items"))) \
        | frozenset(ts.DWARF_MOUNT_IDS)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--apply", action="store_true", help="write the templates file")
    ap.add_argument("--check", action="store_true", help="exit 1 when the file differs from a fresh run")
    ap.add_argument("--game-modules", help="the Modules folder (default: $BANNERLORD_GAME_DIR)")
    args = ap.parse_args(argv)

    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from _gamedir import game_modules
    import taom_schema as ts
    modules = Path(args.game_modules) if args.game_modules else game_modules(DEFAULT_GAME_ROOT)
    if not (modules / "LOTRLOME_Armory").is_dir():
        print(f"ERROR: no Armory under {modules} (set BANNERLORD_GAME_DIR)", file=sys.stderr)
        return 2
    if not (modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items").is_dir():
        print(f"ERROR: no LOTRLOME_items folder under {modules / 'LOTRLOME_Armory' / 'ModuleData'} "
              "(the creature mounts cannot be told apart)", file=sys.stderr)
        return 4
    classes = ts.build_item_class_registry(MODULE_DATA, modules)
    cultures = town_cultures(SPCULTURES)
    try:
        text = generate(load_troops(TROOPS_DIR), classes, cultures, banned_mount_ids(modules))
    except NoUsableKit as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 3

    if args.check:
        current = OUTPUT_FILE.read_text(encoding="utf-8") if OUTPUT_FILE.exists() else ""
        if current.replace("\r\n", "\n") != text:
            print(f"STALE: {OUTPUT_FILE} differs from a fresh run; run with --apply", file=sys.stderr)
            return 1
        print(f"OK: {OUTPUT_FILE.name} is current ({len(cultures)} cultures)")
        return 0
    if args.apply:
        OUTPUT_FILE.write_text(text, encoding="utf-8", newline="\n")
        print(f"wrote {OUTPUT_FILE} ({len(cultures)} cultures, {template_count(text)} templates)")
    else:
        print(f"dry run: {len(cultures)} cultures, {template_count(text)} templates (use --apply)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
