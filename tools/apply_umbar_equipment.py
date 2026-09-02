#!/usr/bin/env python3
"""Dress Umbar: replace the naked civilian rosters and re-point the battle kits.

Umbar troops looked unarmoured and no gate could see why. Nothing was a broken
reference: every id resolved and every mesh was live. Two separate defects:

  1. 15 of 16 troops carried an inline civilian roster holding nothing but
     `bandit_envelope_dress_v1` and `wrapped_shoes` -- peasant rags, with Head,
     Cape and Gloves absent entirely. `troops_umbar.xml` is the only troop file
     in the repo using that item (15 hits; every other culture 0), and the only
     one hand-rolling inline civilian blocks instead of naming a shared roster.

     NOT the reason, though it was the first diagnosis and it was wrong:
     `covers_body="false"` does NOT mean "renders naked". `ArmorComponent`
     (v1.4.8) does `bool flag = attr != null && Convert.ToBoolean(attr.Value);
     if (!flag) MeshesMask |= SkinMask.BodyVisible;` -- an ABSENT attribute
     short-circuits to the same false, so "false" and "not stated" are one
     behaviour, and both mean skin shows ALONGSIDE the garment, which is correct
     for a sleeveless dress. 34 singleplayer vanilla items declare it (54 if the
     multiplayer set counts). The troops were badly dressed, not mesh-less.

  2. The battle kits were Gondor Anorien plus vanilla Calradian, while the Umbar
     LORDS wore Haradrim. Neither population matched the culture.

Both are fixed against Mordor's Black Numenorean set, which is lore-exact (the
Corsairs of Umbar descend from Black Numenoreans), ships 79 items that are all
verified live, and is already organised on TAOM's own tier/class convention. No
new art and no new item definitions are needed.

DESIGN NOTES, each of which is load-bearing:

  * Equipment sets are mixed PER SLOT, not chosen whole
    (`.claude/rules/troops.md`). `Equipment.GetRandomEquipmentElements` loops the
    12 slots and draws each from an independently chosen set, starting at TWO
    sets. So a delta is applied to EVERY battle roster of a troop, never to one.
    A per-set edit reproduces the blind spot in
    `docs/reviews/rca-troop-equipment-slot-mixing-2026-09-01.md`.

  * The civilian rosters are appended to the EXISTING
    `taom_equipment_sets_umbar.xml`, never to a new file. A brand-new XML in a
    registered directory is only globbed at process launch, which is exactly how
    `generate_starter_armor.py` shipped 12 naked cultures on 2026-06-30 with
    every gate green.

  * `equipmentType="Civilian"` is REQUIRED on a standalone civilian
    `<EquipmentSet>` (`.claude/rules/xml-data.md`). A roster id containing `_civ`
    does not classify it; without the attribute the engine treats it as battle
    gear, which is the Faramir/Boromir wrong-outfit bug.

  * The two files use DIFFERENT conventions and both are preserved verbatim:
    `troops_umbar.xml` uses lowercase `<equipment>` with multi-line attributes
    and 4-space indent; `taom_equipment_sets_umbar.xml` uses capitalised
    `<Equipment>` on one line with TAB indent.

XML I/O follows `tools/README.md`: BOM sniffed and re-emitted, the file's own
line terminator captured and reused, the result parsed through ElementTree before
any write, and dry-run is the default.

Usage:
    python tools/apply_umbar_equipment.py              # dry run, prints a diff summary
    python tools/apply_umbar_equipment.py --apply
    python tools/apply_umbar_equipment.py --check      # exit 1 if work remains
"""
from __future__ import annotations

import argparse
import re
import zlib
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
TROOPS = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "troops" / "troops_umbar.xml"
ROSTERS = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "equipmentsets" / "taom_equipment_sets_umbar.xml"

# The vanilla peasant tunic that dressed every Umbar troop in town. Not a
# rendering defect (see the docstring), just the wrong clothes on a whole culture.
NAKED_BODY = "bandit_envelope_dress_v1"

# Civilian rosters authored into the EXISTING roster file.
#
# Deliberately light. The first version put `sm_md_num_chest_light_a` and
# `sm_md_num_inf_chest_med_a` on townsfolk -- the literal Body items of the L6
# and L11/16 INFANTRY BATTLE kits, material_type="Plate" -- which made a
# civilian at 182 armour better protected than the faction's own front-line
# infantry at 168. These are Harad cloth and leather: 56 / 87 / 94, monotonic,
# and under every battle tier.
#
# Every body item is verified covers_body="true" and every leg item
# covers_legs="true" at run time by the item check in main(), not assumed here.
CIVILIAN_ROSTERS = [
    (
        "umbar_troop_civilian_template_t1",
        [
            ("Item0", "peasant_2haxe_1_t1"),
            ("Item2", "throwing_stone"),
            ("Head", "haradrim_head"),
            ("Body", "haradrim_torso"),
            ("Leg", "haradrim_boots"),
        ],
    ),
    (
        "umbar_troop_civilian_template_t2",
        [
            ("Item0", "aserai_sword_3_t3"),
            ("Item2", "throwing_stone"),
            ("Head", "haradrim02_head"),
            ("Body", "haradrim02_toso"),
            ("Gloves", "haradrim02_gloves"),
            ("Leg", "haradrim02_boots"),
        ],
    ),
    (
        "umbar_troop_civilian_template_t3",
        [
            ("Item0", "aserai_sword_5_t4"),
            ("Head", "harad03_helmet"),
            ("Body", "harad03_torso"),
            ("Gloves", "harad03_glove"),
            ("Leg", "harad03_boots"),
        ],
    ),
]

# The kit grid, rebuilt 2026-09-01 after a balance review measured the first
# attempt at the 100th percentile of its level cohort at EVERY tier.
#
# Two facts drove the rebuild, both measured rather than assumed:
#
#   * A full 5-slot Black Numenorean kit FLOORS AT 160 armour. It is Mordor's
#     L26-L46 elite art, so it physically cannot dress a level-6 troop at cohort
#     weight -- the first version put a level-6 bandit in 168, against a level-6
#     cohort max of 71. Harad carries the rank and file instead; Black
#     Numenorean is kept for the L31 capstone, the boss and the lords, which is
#     also where "descended from Black Numenoreans" actually reads.
#
#   * `_b` IS NOT A PROMOTION OVER `_a`. `sm_md_num_inf_chest_heavy_b` is 85
#     total armour against `_heavy_a`'s 89 (arm_armor 26 vs 30); the `_b` items
#     are art variants, not a higher tier. Promoting `_a` -> `_b` cost 4 armour
#     on 5 upgrade edges. The lesson "never select a game item by what its name
#     implies" (docs/reviews/lessons/data-content-cultures.md) recurring exactly.
#
# Also flat in the source art and worth knowing before retuning: every hood and
# helmet from `med` up is 47, and greaves are 41 at med, heavy AND elite. Only
# Body and Cape actually climb.
#
# Totals: L6 71, L11 100, L16 111-121, L21 149, L26 174-178, L31 186-194.
# Cohort maxima: 71 / 169 / 169 / 174 / 205 / 214. Every tier is at or under.
BATTLE_KITS = {
    ("Infantry", 6): {
        "Head": "haradrim_head", "Body": "haradrim_torso",
        "Gloves": "haradrim_gloves", "Leg": "haradrim_boots"},
    ("Infantry", 11): {
        "Head": "haradrim_head", "Body": "haradrim02_toso",
        "Cape": "haradrim02_pauldrons", "Gloves": "haradrim_gloves",
        "Leg": "haradrim_boots"},
    ("Infantry", 16): {
        "Head": "harad03_helmet", "Body": "harad03_torso", "Cape": "harad03_cape",
        "Gloves": "harad03_glove", "Leg": "harad03_boots"},
    ("Ranged", 16): {
        "Head": "haradrim02a_head", "Body": "haradrim02a_toso",
        "Cape": "haradrim02_pauldrons", "Gloves": "haradrim02a_gloves",
        "Leg": "haradrim02_boots"},
    ("Infantry", 21): {
        "Head": "harad05_v1_helmet", "Body": "harad05_v1_torso",
        "Cape": "harad05_v1_cape", "Gloves": "harad05_v1_gloves",
        "Leg": "harad05_v1_boots"},
    ("Ranged", 21): {
        "Head": "harad05_v1_helmet", "Body": "harad05_v2_torso",
        "Cape": "harad05_v2_cape", "Gloves": "harad05_v1_gloves",
        "Leg": "harad05_v2_boots"},
    ("Infantry", 26): {
        "Head": "harad06_v1_helmet", "Body": "harad06_v1_torso",
        "Cape": "harad06_v1_cape", "Gloves": "harad06_v1_glove",
        "Leg": "harad06_v1_boots"},
    ("Ranged", 26): {
        "Head": "harad07_helmet", "Body": "harad07_torso", "Cape": "harad07_cape",
        "Gloves": "harad07_glove", "Leg": "harad07_boots"},
    ("Infantry", 31): {
        "Head": "sk_md_num_hood_light_a", "Body": "sm_md_num_inf_chest_med_a",
        "Cape": "sm_md_num_inf_pauld_med_a", "Gloves": "sk_md_num_inf_bracer_med_a",
        "Leg": "sm_md_num_grvs_light_a"},
    ("Cavalry", 31): {
        "Head": "sk_md_num_cav_helmet_med_a", "Body": "sm_md_num_cav_chest_med_a",
        "Cape": "sm_md_num_cav_pauld_med_a", "Gloves": "sk_md_num_inf_bracer_med_a",
        "Leg": "sm_md_num_grvs_light_a"},
}

# The bandit boss is a named L21 capstone. 168 against a peer-boss range of
# 131-180, and Black Numenorean because he is the one corsair a player meets who
# should look like fallen Numenorean nobility.
BOSS_KIT = {
    "umbar_corsairs_boss": {
        "Head": "sk_md_num_hood_light_b", "Body": "sm_md_num_chest_light_a",
        "Cape": "sm_md_num_inf_pauld_med_a", "Gloves": "sk_md_num_inf_bracer_med_a",
        "Leg": "sm_md_num_grvs_light_a"},
}

ARMOUR_SLOTS = ("Head", "Body", "Cape", "Gloves", "Leg")


def kit_for(troop_id: str, group: str, level: int) -> dict[str, str] | None:
    if troop_id in BOSS_KIT:
        return BOSS_KIT[troop_id]
    return BATTLE_KITS.get((group, level))


# level -> civilian tier. Umbar's tree is 6/11 | 16/21 | 26/31.
def civilian_tier_for(level: int) -> str:
    if level <= 11:
        return "umbar_troop_civilian_template_t1"
    if level <= 21:
        return "umbar_troop_civilian_template_t2"
    return "umbar_troop_civilian_template_t3"


# ---------------------------------------------------------------- byte-faithful I/O

class XmlFile:
    """Round-trips BOM and the file's own line terminator."""

    def __init__(self, path: Path):
        self.path = path
        raw = path.read_bytes()
        self.bom = raw.startswith(b"\xef\xbb\xbf")
        self.text = raw[3:].decode("utf-8") if self.bom else raw.decode("utf-8")
        crlf = self.text.count("\r\n")
        lf = self.text.count("\n")
        self.nl = "\r\n" if crlf > (lf - crlf) else "\n"

    def write(self, new_text: str) -> None:
        ET.fromstring(new_text.encode("utf-8"))  # refuse to write a broken document
        self.path.write_bytes(
            (b"\xef\xbb\xbf" if self.bom else b"") + new_text.encode("utf-8")
        )


# ---------------------------------------------------------------- roster authoring

def render_rosters(nl: str) -> str:
    """Emit the civilian rosters in the roster file's own style: tabs, capitalised
    <Equipment>, one per line."""
    out = []
    out.append("\t<!-- ==================== UMBAR TROOP CIVILIAN EQUIPMENT ==================== -->")
    out.append("\t<!-- Replaces 15 inline rosters that put every Umbar troop in vanilla peasant")
    out.append("\t     rags (bandit_envelope_dress_v1 + wrapped_shoes) with Head, Cape and Gloves")
    out.append("\t     absent. Harad cloth, 56 / 87 / 94 armour, under every battle tier. -->")
    out.append("")
    for rid, slots in CIVILIAN_ROSTERS:
        out.append(f'\t<EquipmentRoster id="{rid}" culture="Culture.umbar">')
        out.append('\t\t<EquipmentSet equipmentType="Civilian">')
        for slot, item in slots:
            out.append(f'\t\t\t<Equipment slot="{slot}" id="Item.{item}" />')
        out.append("\t\t</EquipmentSet>")
        out.append("\t</EquipmentRoster>")
        out.append("")
    return nl.join(out)


def add_civilian_rosters(f: XmlFile) -> tuple[str, int]:
    if CIVILIAN_ROSTERS[0][0] in f.text:
        return f.text, 0
    close = "</EquipmentRosters>"
    idx = f.text.rindex(close)
    return f.text[:idx] + render_rosters(f.nl) + f.text[idx:], len(CIVILIAN_ROSTERS)


# ---------------------------------------------------------------- troop rewrite

_NPC_RE = re.compile(r"<NPCCharacter\b.*?</NPCCharacter>", re.S)
_CIV_RE = re.compile(r"[ \t]*<EquipmentRoster\s+\n?\s*civilian=\"true\">.*?</EquipmentRoster>\s*?\n", re.S)


def replace_civilian_blocks(f: XmlFile) -> tuple[str, list[tuple[str, str]]]:
    """Swap each inline civilian roster for a named, tier-appropriate reference.

    Collected front-to-back and applied BACK-TO-FRONT, because every replacement
    changes the text length and any offset recorded before an earlier edit would
    be invalidated.
    """
    text = f.text
    edits, done = [], []
    for npc in _NPC_RE.finditer(text):
        block = npc.group(0)
        tid = re.search(r'\bid="([^"]+)"', block).group(1)
        lvl_m = re.search(r'\blevel="(\d+)"', block)
        if not lvl_m:
            continue
        civ = _CIV_RE.search(block)
        if not civ:
            continue
        if NAKED_BODY not in civ.group(0):
            continue
        roster_id = civilian_tier_for(int(lvl_m.group(1)))
        indent = re.match(r"[ \t]*", civ.group(0)).group(0)
        repl = f'{indent}<EquipmentSet id="{roster_id}" equipmentType="Civilian" />{f.nl}'
        edits.append((npc.start() + civ.start(), npc.start() + civ.end(), repl))
        done.append((tid, roster_id))
    for start, end, repl in sorted(edits, reverse=True):
        text = text[:start] + repl + text[end:]
    return text, done


_ROSTER_RE = re.compile(r"<EquipmentRoster\b(?![^>]*civilian)[^>]*>(.*?)</EquipmentRoster>", re.S)
# Matches both spellings: troop and character files use lowercase <equipment>,
# the equipmentsets files use capitalised <Equipment>. A case-sensitive matcher
# reads one of them as "no equipment here", which is a silent no-op rather than
# an error.
_EQ_ELEM_RE = re.compile(r"[ \t]*<(equipment|Equipment)\b.*?/>\s*?\n", re.S)


def armoury_item_ids() -> set[str]:
    """Every item id the Armoury and vanilla define. Used to refuse a kit that
    names something that does not exist, which is the underwear bug."""
    base = Path(r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules")
    roots = [base / m / "ModuleData" for m in ("LOTRLOME_Armory", "SandBoxCore", "Native", "SandBox")]
    ids: set[str] = set()
    for root in roots:
        if not root.exists():
            continue
        for f in root.rglob("*.xml"):
            if ".bak" in f.name:
                continue
            try:
                txt = f.read_text(encoding="utf-8-sig", errors="ignore")
            except OSError:
                continue
            # <CraftedItem> as well as <Item>: vanilla swords such as
            # aserai_sword_3_t3 are piece-built and carry no <Item> element.
            ids.update(re.findall(r'<(?:Crafted)?Item\b[^>]*?\bid="([^"]+)"', txt))
    return ids


def apply_battle_kits(f: XmlFile) -> tuple[str, list[tuple[str, str, int]]]:
    """Rewrite the five armour slots of EVERY battle roster of each troop.

    Armour elements are stripped and re-emitted so all of a troop's battle sets
    carry an identical armour block. That is not tidiness: the engine draws each
    slot from an independently chosen set, so a slot filled in one set and empty
    in another produces a combination nobody authored
    (`.claude/rules/troops.md`). Weapons, Horse and HorseHarness are never
    touched.
    """
    text = f.text
    edits, done = [], []
    for npc in _NPC_RE.finditer(text):
        block = npc.group(0)
        tid = re.search(r'\bid="([^"]+)"', block).group(1)
        lvl_m = re.search(r'\blevel="(\d+)"', block)
        grp_m = re.search(r'\bdefault_group="(\w+)"', block)
        if not lvl_m:
            continue
        kit = kit_for(tid, grp_m.group(1) if grp_m else "Infantry", int(lvl_m.group(1)))
        if not kit:
            continue
        n_rosters = 0
        for roster in _ROSTER_RE.finditer(block):
            body = roster.group(1)
            new_body = rewrite_roster_slots(body, kit, f.nl)
            if new_body == body:
                continue
            edits.append((npc.start() + roster.start(1), npc.start() + roster.end(1), new_body))
            n_rosters += 1
        if n_rosters:
            done.append((tid, f"{grp_m.group(1) if grp_m else '?'}/L{lvl_m.group(1)}", n_rosters))
    for start, end, repl in sorted(edits, reverse=True):
        text = text[:start] + repl + text[end:]
    return text, done


# ---------------------------------------------------------------- lords and notables

LORD_TEMPLATES = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "equipmentsets" / "taom_lord_template_equipment.xml"
NOTABLES = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "characters" / "npcs_umbar.xml"

# Umbar lords fought on foot: not one of the 10 named-lord rosters nor any of the
# 6 lord templates carried a mount, while Gondor and Harad carry one on all of
# theirs, including the civilian and teen templates. Matching Harad's pair.
MOUNT = {"HorseHarness": "chain_horse_harness", "Horse": "charger"}

LORD_BATTLE = {
    "Head": "sk_md_num_inf_helmet_lord_a", "Body": "ar_ardunian_elite_armour",
    "Cape": "sm_md_num_inf_pauld_cape_elite_a", "Gloves": "sk_md_num_inf_bracer_lord_a",
    "Leg": "sm_md_num_grvs_elite_a"}
LORD_CIVILIAN = {
    "Head": "sk_md_num_hood_lord_a", "Body": "sm_md_num_inf_chest_med_a",
    "Cape": "sm_md_num_inf_pauld_med_a", "Gloves": "sk_md_num_inf_bracer_med_a",
    "Leg": "sm_md_num_grvs_med_a"}
LORD_TEEN = {
    "Head": "sk_md_num_hood_light_a", "Body": "sm_md_num_chest_light_a",
    "Cape": "sm_md_num_inf_pauld_med_a", "Gloves": "sk_md_num_inf_bracer_med_a",
    "Leg": "sm_md_num_grvs_light_a"}

# The five named-lord battle rosters and their civilian twins. Variants keep the
# 10 lords visually distinct rather than cloning one kit ten times.
LORD_ROSTER_KITS = {
    "umbar_bat_template_medium_a": {"Head": "sk_md_num_inf_helmet_lord_a", "Body": "sm_md_num_inf_chest_elite_a", "Cape": "sm_md_num_inf_pauld_cape_elite_a", "Gloves": "sk_md_num_inf_bracer_lord_a", "Leg": "sm_md_num_grvs_elite_a"},
    "umbar_bat_template_medium_b": {"Head": "sk_md_num_inf_helmet_lord_b", "Body": "sm_md_num_inf_chest_elite_b", "Cape": "sm_md_num_inf_pauld_cape_elite_b", "Gloves": "sk_md_num_inf_bracer_lord_a", "Leg": "sm_md_num_grvs_elite_a"},
    "umbar_bat_template_medium_c": {"Head": "sk_md_num_cav_helmet_lord_a", "Body": "sm_md_num_cav_chest_elite_a", "Cape": "sm_md_num_cav_pauld_cape_elite_a", "Gloves": "sk_md_num_inf_bracer_lord_a", "Leg": "sm_md_num_grvs_elite_a"},
    "umbar_bat_template_medium_d": {"Head": "sk_md_num_cav_helmet_lord_b", "Body": "sm_md_num_cav_chest_elite_b", "Cape": "sm_md_num_cav_pauld_cape_elite_a", "Gloves": "sk_md_num_inf_bracer_lord_a", "Leg": "sm_md_num_grvs_elite_a"},
    "umbar_bat_template_medium_e": {"Head": "sk_md_num_hood_lord_b", "Body": "ar_ardunian_elite_armour", "Cape": "sm_md_num_inf_pauld_cape_elite_b", "Gloves": "sk_md_num_arc_bracer_lord_a", "Leg": "sm_md_num_grvs_elite_a"},
    "umbar_civ_template_default_a": dict(LORD_CIVILIAN, Head="sk_md_num_hood_lord_a"),
    "umbar_civ_template_default_b": dict(LORD_CIVILIAN, Head="sk_md_num_hood_lord_b"),
    "umbar_civ_template_default_c": dict(LORD_CIVILIAN, Head="sk_md_num_hood_elite_a"),
    "umbar_civ_template_default_d": dict(LORD_CIVILIAN, Head="sk_md_num_hood_elite_b"),
    "umbar_civ_template_default_e": dict(LORD_CIVILIAN, Head="sk_md_num_hood_elite_c"),
}

# 26 notables plus 2 generic lords. Civilian townsfolk, so Harad cloth only --
# no plate, nothing from a battle kit.
#
# Rebuilt after a review measured the first version COLLAPSING 18 distinct
# notable looks into 4, using `idx % 4` over file order. Two defects in one
# line: too few variants, and an index-based key, so inserting one
# <NPCCharacter> at the top of the file reshuffled all 26. The key is now a
# stable hash of the notable's own id, so a notable keeps its look forever and
# the pool can grow without re-rolling anyone.
NOTABLE_VARIANTS = [
    {"Head": "haradrim_head", "Body": "haradrim_torso", "Cape": "haradrim02_pauldrons",
     "Gloves": "haradrim_gloves", "Leg": "haradrim_boots"},
    {"Head": "haradrim02_head", "Body": "haradrim02_toso", "Cape": "harad03_cape",
     "Gloves": "haradrim02_gloves", "Leg": "haradrim02_boots"},
    {"Head": "haradrim02a_head", "Body": "haradrim02a_toso", "Cape": "haradrim02_pauldrons",
     "Gloves": "haradrim02a_gloves", "Leg": "haradrim_boots"},
    {"Head": "haradrim02b_head", "Body": "haradrim02b_toso", "Cape": "harad04_cape",
     "Gloves": "haradrim02b_gloves", "Leg": "harad03_boots"},
    {"Head": "haradrim02c_head", "Body": "haradrim02c_toso", "Cape": "harad03_cape",
     "Gloves": "haradrim02c_gloves", "Leg": "harad04_boots"},
    {"Head": "harad03_helmet", "Body": "harad03_torso", "Cape": "harad03_cape",
     "Gloves": "harad03_glove", "Leg": "harad03_boots"},
    {"Head": "harad04_helmet", "Body": "harad04_body", "Cape": "harad04_cape",
     "Gloves": "harad04_glove", "Leg": "harad04_boots"},
    {"Head": "harad08_helmet", "Body": "harad08_torso", "Cape": "harad03_cape",
     "Gloves": "harad08_gloves", "Leg": "harad08_boots"},
]


def notable_variant(notable_id: str) -> dict:
    """Stable per-notable look. Keyed on the id, never on file position, so
    adding a notable does not re-dress the other 26. zlib.crc32 rather than
    hash() because hash() is salted per process and would re-roll every run."""
    return NOTABLE_VARIANTS[zlib.crc32(notable_id.encode("utf-8")) % len(NOTABLE_VARIANTS)]


def rewrite_roster_slots(block: str, kit: dict[str, str], nl: str,
                         slots: tuple[str, ...] = ARMOUR_SLOTS) -> str:
    """Replace the given slots inside ONE roster body, in place.

    Everything that is not an <equipment>/<Equipment> element is preserved
    byte-for-byte: the <EquipmentSet> wrapper, <Flags IsLordTemplate="true" />,
    comments and whitespace. That is not tidiness. `MBEquipmentRoster` reads ONLY
    `EquipmentSet` children, so a rewrite that drops the wrapper leaves a
    perfectly well-formed document whose rosters all load EMPTY -- the bug that
    put 111 Gondor lords into battle naked. An earlier version of this function
    rebuilt the body from its equipment elements alone and did exactly that to 20
    rosters; the XML parsed, validate_moduledata passed, and only the C# test
    ChildGenerationCultures_HaveChildTeenAndLordEquipmentTemplates caught it,
    because the <Flags> element went with the wrapper.

    Edits are collected front-to-back and applied BACK-TO-FRONT, since every
    splice changes the length and invalidates any later offset.
    """
    found = list(_EQ_ELEM_RE.finditer(block))
    if not found:
        return block
    tag = re.search(r"<(equipment|Equipment)\b", found[0].group(0)).group(1)
    ind = re.match(r"[ \t]*", found[0].group(0)).group(0)
    multiline = "\n" in found[0].group(0).strip()

    def render(slot: str) -> str:
        if multiline:
            return (f"{ind}<{tag}{nl}{ind}    slot=\"{slot}\"{nl}"
                    f"{ind}    id=\"Item.{kit[slot]}\" />{nl}")
        return f"{ind}<{tag} slot=\"{slot}\" id=\"Item.{kit[slot]}\" />{nl}"

    edits, seen = [], set()
    anchor = None
    for e in found:
        m = re.search(r'slot="(\w+)"', e.group(0))
        if not m or m.group(1) not in slots:
            continue
        slot = m.group(1)
        anchor = e.end() if anchor is None else anchor
        if slot in kit and slot not in seen:
            edits.append((e.start(), e.end(), render(slot)))   # replace in place
            seen.add(slot)
        else:
            edits.append((e.start(), e.end(), ""))             # drop a stale slot
    # Slots the roster did not already have go in after the last existing
    # equipment element, so they land inside the same <EquipmentSet>.
    tail = "".join(render(s) for s in slots if s in kit and s not in seen)
    if tail:
        edits.append((found[-1].end(), found[-1].end(), tail))

    out = block
    for start, end, repl in sorted(edits, key=lambda x: x[0], reverse=True):
        out = out[:start] + repl + out[end:]
    return out


def apply_lord_templates(f: XmlFile) -> tuple[str, list[str]]:
    """The 10 taom_umbar_{lord,ruler}_* templates: Black Numenorean kit, the
    missing Cape, and the mount every one of them lacked.

    `ruler` matters as much as `lord` and is easy to miss: matching only
    `taom_umbar_lord_` leaves the four templates that dress the KINGDOM RULER
    capeless and on foot, which is the most visible lord in the faction. Gondor
    carries Cape and mount on all four of its ruler templates.
    """
    text, done = f.text, []
    edits = []
    for m in re.finditer(r'<EquipmentRoster id="(taom_umbar_(?:lord|ruler)_[^"]+)"[^>]*>(.*?)</EquipmentRoster>', text, re.S):
        rid, body = m.group(1), m.group(2)
        base = LORD_TEEN if "_teen_" in rid else (LORD_CIVILIAN if "_civilian_" in rid else LORD_BATTLE)
        if "_ruler_" in rid:
            # the ruler is the faction's capstone: the lord kit even in civilian dress
            base = LORD_BATTLE if "_battle_" in rid else dict(LORD_CIVILIAN, Body="ar_ardunian_elite_armour")
        kit = dict(base, **MOUNT)
        new = rewrite_roster_slots(body, kit, f.nl, ARMOUR_SLOTS + ("HorseHarness", "Horse"))
        if new != body:
            edits.append((m.start(2), m.end(2), new))
            done.append(rid)
    for s, e, r in sorted(edits, reverse=True):
        text = text[:s] + r + text[e:]
    return text, done


def apply_lord_rosters(f: XmlFile) -> tuple[str, list[str]]:
    """The 10 named-lord rosters in taom_equipment_sets_umbar.xml."""
    text, done = f.text, []
    edits = []
    for m in re.finditer(r'<EquipmentRoster id="([^"]+)"[^>]*>(.*?)</EquipmentRoster>', text, re.S):
        rid, body = m.group(1), m.group(2)
        if rid not in LORD_ROSTER_KITS:
            continue
        kit = dict(LORD_ROSTER_KITS[rid], **MOUNT)
        new = rewrite_roster_slots(body, kit, f.nl, ARMOUR_SLOTS + ("HorseHarness", "Horse"))
        if new != body:
            edits.append((m.start(2), m.end(2), new))
            done.append(rid)
    for s, e, r in sorted(edits, reverse=True):
        text = text[:s] + r + text[e:]
    return text, done


def apply_notables(f: XmlFile) -> tuple[str, list[str]]:
    """Give the 26 bare-headed notables a Head and Cape, applying the SAME kit to
    the civilian roster and its battle twin so the #295 pairing stays a pair."""
    text, done = f.text, []
    edits = []
    for idx, npc in enumerate(_NPC_RE.finditer(text)):
        block = npc.group(0)
        nid = re.search(r'\bid="([^"]+)"', block).group(1)
        kit = notable_variant(nid)
        touched = False
        for roster in re.finditer(r"<EquipmentRoster\b[^>]*>(.*?)</EquipmentRoster>", block, re.S):
            body = roster.group(1)
            new = rewrite_roster_slots(body, kit, f.nl)
            if new != body:
                edits.append((npc.start() + roster.start(1), npc.start() + roster.end(1), new))
                touched = True
        if touched:
            done.append(nid)
    for s, e, r in sorted(edits, reverse=True):
        text = text[:s] + r + text[e:]
    return text, done


# ---------------------------------------------------------------- main

def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write the changes (default is a dry run)")
    ap.add_argument("--check", action="store_true", help="exit 1 if any work remains")
    args = ap.parse_args()

    for p in (TROOPS, ROSTERS):
        if not p.exists():
            print(f"ERROR: missing {p}", file=sys.stderr)
            return 2

    # Refuse to ship a kit naming an item that does not exist. A dangling id is
    # the underwear bug, and it is cheaper to catch here than in game.
    known = armoury_item_ids()
    if known:
        all_kits = (list(BATTLE_KITS.values()) + list(BOSS_KIT.values())
                    + list(LORD_ROSTER_KITS.values()) + NOTABLE_VARIANTS
                    + [LORD_BATTLE, LORD_CIVILIAN, LORD_TEEN, MOUNT])
        wanted = {i for kit in all_kits for i in kit.values()}
        wanted |= {i for _, slots in CIVILIAN_ROSTERS for _, i in slots}
        missing = sorted(wanted - known)
        if missing:
            print("ERROR: kit names items that do not exist:", file=sys.stderr)
            for m in missing:
                print(f"  {m}", file=sys.stderr)
            return 2
        print(f"  item check: all {len(wanted)} kit items resolve")
    else:
        print("  item check SKIPPED: game install not found")

    rosters = XmlFile(ROSTERS)
    roster_text, n_added = add_civilian_rosters(rosters)

    troops = XmlFile(TROOPS)
    troop_text, swapped = replace_civilian_blocks(troops)
    troops.text = troop_text          # chain the battle pass onto the civilian result
    troop_text, dressed = apply_battle_kits(troops)

    remaining = troop_text.count(NAKED_BODY)

    print(f"  {ROSTERS.name}: +{n_added} civilian roster(s)"
          f"{' (already present)' if n_added == 0 else ''}")
    print(f"  {TROOPS.name}: {len(swapped)} inline civilian roster(s) -> named reference")
    for tid, rid in swapped:
        print(f"     {tid:<28} -> {rid}")
    print(f"  {TROOPS.name}: {len(dressed)} troop(s) re-armoured across their battle sets")
    for tid, band, n in dressed:
        print(f"     {tid:<28} {band:<16} {n} battle roster(s)")
    print(f"  {NAKED_BODY} references remaining: {remaining}")

    # lord rosters live in the same file as the civilian ones, so chain onto it
    rosters.text = roster_text
    roster_text, lord_rosters = apply_lord_rosters(rosters)
    print(f"  {ROSTERS.name}: {len(lord_rosters)} named-lord roster(s) re-kitted + mounted")

    templates = XmlFile(LORD_TEMPLATES)
    template_text, lord_templates = apply_lord_templates(templates)
    print(f"  {LORD_TEMPLATES.name}: {len(lord_templates)} lord template(s) re-kitted + mounted")

    notables = XmlFile(NOTABLES)
    notable_text, dressed_notables = apply_notables(notables)
    print(f"  {NOTABLES.name}: {len(dressed_notables)} notable(s) given a Head and Cape")

    pending = [
        (ROSTERS, rosters, roster_text),
        (TROOPS, troops, troop_text),
        (LORD_TEMPLATES, templates, template_text),
        (NOTABLES, notables, notable_text),
    ]
    changed = [p for p, _, new in pending if new != XmlFile(p).text]

    if args.check:
        return 1 if changed or remaining else 0

    if not args.apply:
        print(f"\n  dry run. {len(changed)} file(s) would change. re-run with --apply to write.")
        return 0

    for path, handle, new in pending:
        if new != XmlFile(path).text:
            handle.write(new)
            print(f"  wrote {path}")
    if not changed:
        print("  nothing to do.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
