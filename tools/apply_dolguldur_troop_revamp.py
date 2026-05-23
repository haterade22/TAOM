#!/usr/bin/env python3
"""Apply Dol Guldur troop tree revamp per spec.

Issue: #212
Source-of-truth: E:\\repos\\lotraom-assets\\tools\\dol_guldur_armors_and_troops.txt

Per spec, Dol Guldur has THREE unit groups touched by this revamp:
  Uruk Infantry:   Foul -> Fighter -> Warrior -> Swordsman
                                         |          |
                                         |          +-> Fell Infantry -> Black Guard
                                         |          +-> Fell Fang     -> Black Slayer
                                         +-> Skirmisher -> Bowman -> Fell Archer -> Black Sharpshooter
  Fell Warg (merc): Tracker -> Rider -> Ravager -> Fang -> Fell Ravager
  (Khamul human line — already complete in file, untouched)

Strategy: repurpose existing IDs to spec roles (preserves save compat).
Deletes 12 redundant/superseded troops (old Khamul stubs + Berserker line).

Usage:
    python tools/apply_dolguldur_troop_revamp.py --dry-run
    python tools/apply_dolguldur_troop_revamp.py --apply
"""
import argparse
import os
import re
import sys
from typing import Dict, List, Tuple

TROOPS_FILE = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Main", "_Module", "ModuleData", "troops", "troops_dolguldur.xml"
)

DELETE_IDS = {
    # Old Khamul stubs (superseded by dg_khamul_* line which is already in the file)
    "dg_initiate", "dg_disciple", "dg_infantry", "dg_warden",
    "dg_archer", "dg_marksman",
    # Berserker line (not in spec)
    "dg_uruk_spawn", "dg_uruk_gnawer", "dg_uruk_butcher",
    "dg_uruk_berserker", "dg_uruk_black_howler", "dg_uruk_rage_bearer",
}

# Display name + level updates (preserves IDs for save compat).
# Some entries also bump default_group (warg_skirmisher → Cavalry).
RENAMES: Dict[str, Dict[str, str]] = {
    # Uruk infantry line
    "dg_uruk_foul":               {"name": "Uruk Foul",               "level": "11"},
    "dg_uruk_warrior":            {"name": "Uruk Fighter",            "level": "13"},
    "dg_uruk_veteran_warrior":    {"name": "Uruk Warrior",            "level": "16"},
    "dg_uruk_swordsman":          {"name": "Uruk Swordsman",          "level": "21"},
    "dg_uruk_fell_infantry":      {"name": "Uruk Fell Infantry",      "level": "26"},
    "dg_uruk_black_guard":        {"name": "Uruk Black Guard",        "level": "31"},
    "dg_uruk_fell_fang":          {"name": "Uruk Fell Fang",          "level": "26"},
    "dg_uruk_black_slayer":       {"name": "Uruk Black Slayer",       "level": "31"},
    "dg_uruk_skirmisher":         {"name": "Uruk Skirmisher",         "level": "16"},
    "dg_uruk_bowman":             {"name": "Uruk Bowman",             "level": "21"},
    "dg_uruk_fell_archer":        {"name": "Uruk Fell Archer",        "level": "26"},
    "dg_uruk_black_sharpshooter": {"name": "Uruk Black Sharpshooter", "level": "31"},
    # Fell Warg Rider line
    "dg_warg_scout":              {"name": "Warg Tracker",            "level": "16"},
    "dg_warg_raider":             {"name": "Warg Rider",              "level": "21"},
    "dg_warg_skirmisher":         {"name": "Warg Ravager",            "level": "26"},
    "dg_warg_red_fang":           {"name": "Warg Fang",               "level": "31"},
    "dg_fell_warg_rider":         {"name": "Fell Ravager",            "level": "36"},
}

# default_group changes (warg_skirmisher: HorseArcher -> Cavalry per spec)
DEFAULT_GROUPS: Dict[str, str] = {
    "dg_warg_skirmisher": "Cavalry",
}

# Upgrade chain rewires per spec tree
UPGRADE_TARGETS: Dict[str, List[str]] = {
    # Uruk infantry line
    "dg_uruk_foul":               ["dg_uruk_warrior"],
    "dg_uruk_warrior":            ["dg_uruk_veteran_warrior"],
    "dg_uruk_veteran_warrior":    ["dg_uruk_swordsman", "dg_uruk_skirmisher"],
    "dg_uruk_swordsman":          ["dg_uruk_fell_infantry", "dg_uruk_fell_fang"],
    "dg_uruk_fell_infantry":      ["dg_uruk_black_guard"],
    "dg_uruk_fell_fang":          ["dg_uruk_black_slayer"],
    "dg_uruk_black_guard":        [],
    "dg_uruk_black_slayer":       [],
    "dg_uruk_skirmisher":         ["dg_uruk_bowman"],
    "dg_uruk_bowman":             ["dg_uruk_fell_archer"],
    "dg_uruk_fell_archer":        ["dg_uruk_black_sharpshooter"],
    "dg_uruk_black_sharpshooter": [],
    # Fell Warg Rider line
    "dg_warg_scout":              ["dg_warg_raider"],
    "dg_warg_raider":             ["dg_warg_skirmisher"],
    "dg_warg_skirmisher":         ["dg_warg_red_fang"],
    "dg_warg_red_fang":           ["dg_fell_warg_rider"],
    "dg_fell_warg_rider":         [],
}

# Per-troop equipment replacement.
# Armor IDs verified against LOTRLOME_Armory/dol_guldur/*.xml (all sk_dg_uruk_*).
# Weapons: wm_dol_goldur_1h_sword_a01-a03, wm_dol_goldur_1h_mace_a01-a03, wm_dol_goldur_2h_mace_a01-a04,
#          wm_dol_goldur_axe_a01-a04 (these are 2H per crafting_template="TwoHandedAxe"),
#          wm_dol_goldur_halberd_a01-a05.
# Bow: wm_isengard_bow_a02 (existing pattern in file), arrows: piercing_arrows.
# Shield: wm_gundabad_shield_a01-a03 (existing pattern; no sk_dg_uruk_* shield exists).
# Warg mount: warg_brown / warg_dark, harness: warg_saddle.
EQUIPMENT: Dict[str, List[Tuple[str, str]]] = {
    # ===== URUK INFANTRY LINE =====
    # T1 Foul — sword only, light helmet/chest
    "dg_uruk_foul": [
        ("Item0", "wm_dol_goldur_1h_sword_a03"),
        ("Head",  "sk_dg_uruk_helmet_light_a"),
        ("Body",  "sk_dg_uruk_chest_light_a"),
        ("Leg",   "sk_dg_uruk_boots_light_a"),
    ],
    # T2 Fighter — sword only, light gear
    "dg_uruk_warrior": [
        ("Item0", "wm_dol_goldur_1h_sword_a03"),
        ("Head",  "sk_dg_uruk_helmet_light_a"),
        ("Body",  "sk_dg_uruk_chest_light_b"),
        ("Leg",   "sk_dg_uruk_boots_light_a"),
    ],
    # T3 Warrior (demoted from T5) — sword + shield, medium gear, pauldron/bracer introduced
    "dg_uruk_veteran_warrior": [
        ("Item0",  "wm_dol_goldur_1h_sword_a02"),
        ("Item1",  "wm_gundabad_shield_a01"),
        ("Head",   "sk_dg_uruk_helmet_med_a"),
        ("Body",   "sk_dg_uruk_chest_med_a"),
        ("Cape",   "sk_dg_uruk_pauldron_med_a"),
        ("Gloves", "sk_dg_uruk_bracer_med_a"),
        ("Leg",    "sk_dg_uruk_boots_med_a"),
    ],
    # T4 Swordsman — sword + shield, medium-heavy
    "dg_uruk_swordsman": [
        ("Item0",  "wm_dol_goldur_1h_sword_a02"),
        ("Item1",  "wm_gundabad_shield_a02"),
        ("Head",   "sk_dg_uruk_helmet_med_b"),
        ("Body",   "sk_dg_uruk_chest_med_b"),
        ("Cape",   "sk_dg_uruk_pauldron_heavy_a"),
        ("Gloves", "sk_dg_uruk_bracer_heavy_a"),
        ("Leg",    "sk_dg_uruk_boots_heavy_a"),
    ],
    # T5 Fell Infantry — mace + shield, heavy + cape pauldron
    "dg_uruk_fell_infantry": [
        ("Item0",  "wm_dol_goldur_1h_mace_a02"),
        ("Item1",  "wm_gundabad_shield_a02"),
        ("Head",   "sk_dg_uruk_helmet_heavy_a"),
        ("Body",   "sk_dg_uruk_chest_heavy_a"),
        ("Cape",   "sk_dg_uruk_pauldron_heavy_b"),
        ("Gloves", "sk_dg_uruk_bracer_heavy_b"),
        ("Leg",    "sk_dg_uruk_boots_heavy_b"),
    ],
    # T6 Black Guard — mace + shield, elite + cape pauldron
    "dg_uruk_black_guard": [
        ("Item0",  "wm_dol_goldur_1h_mace_a01"),
        ("Item1",  "wm_gundabad_shield_a03"),
        ("Head",   "sk_dg_uruk_helmet_heavy_b"),
        ("Body",   "sk_dg_uruk_chest_elite_a"),
        ("Cape",   "sk_dg_uruk_pauldron_elite_cape_a"),
        ("Gloves", "sk_dg_uruk_bracer_elite_a"),
        ("Leg",    "sk_dg_uruk_boots_elite_a"),
    ],
    # T5 Fell Fang — 2H mace, NO shield, heavy gear, plain pauldron (no cape per spec)
    "dg_uruk_fell_fang": [
        ("Item0",  "wm_dol_goldur_2h_mace_a02"),
        ("Head",   "sk_dg_uruk_helmet_heavy_a"),
        ("Body",   "sk_dg_uruk_chest_heavy_a"),
        ("Cape",   "sk_dg_uruk_pauldron_heavy_a"),
        ("Gloves", "sk_dg_uruk_bracer_heavy_a"),
        ("Leg",    "sk_dg_uruk_boots_heavy_a"),
    ],
    # T6 Black Slayer — 2H axe, NO shield, elite gear, plain pauldron (no cape per spec)
    "dg_uruk_black_slayer": [
        ("Item0",  "wm_dol_goldur_axe_a01"),
        ("Head",   "sk_dg_uruk_helmet_elite_b"),
        ("Body",   "sk_dg_uruk_chest_elite_b"),
        ("Cape",   "sk_dg_uruk_pauldron_elite_a"),
        ("Gloves", "sk_dg_uruk_bracer_elite_b"),
        ("Leg",    "sk_dg_uruk_boots_elite_b"),
    ],
    # ===== URUK ARCHER LINE =====
    # T3 Skirmisher — sword + bow, no pauldron/bracer per spec
    "dg_uruk_skirmisher": [
        ("Item0", "wm_isengard_bow_a02"),
        ("Item1", "piercing_arrows"),
        ("Item2", "piercing_arrows"),
        ("Item3", "wm_dol_goldur_1h_sword_a03"),
        ("Head",  "sk_dg_uruk_helmet_light_c"),
        ("Body",  "sk_dg_uruk_chest_med_e"),
        ("Leg",   "sk_dg_uruk_boots_med_b"),
    ],
    # T4 Bowman — sword + bow, pauldron/bracer introduced
    "dg_uruk_bowman": [
        ("Item0",  "wm_isengard_bow_a02"),
        ("Item1",  "piercing_arrows"),
        ("Item2",  "piercing_arrows"),
        ("Item3",  "wm_dol_goldur_1h_sword_a02"),
        ("Head",   "sk_dg_uruk_helmet_med_e"),
        ("Body",   "sk_dg_uruk_chest_med_f"),
        ("Cape",   "sk_dg_uruk_pauldron_med_b"),
        ("Gloves", "sk_dg_uruk_bracer_med_c"),
        ("Leg",    "sk_dg_uruk_boots_heavy_c"),
    ],
    # T5 Fell Archer — sword + bow, heavy + cape pauldron
    "dg_uruk_fell_archer": [
        ("Item0",  "wm_isengard_bow_a02"),
        ("Item1",  "piercing_arrows"),
        ("Item2",  "piercing_arrows"),
        ("Item3",  "wm_dol_goldur_1h_sword_a02"),
        ("Head",   "sk_dg_uruk_helmet_med_f"),
        ("Body",   "sk_dg_uruk_chest_heavy_e"),
        ("Cape",   "sk_dg_uruk_pauldron_elite_cape_d"),
        ("Gloves", "sk_dg_uruk_bracer_heavy_e"),
        ("Leg",    "sk_dg_uruk_boots_heavy_d"),
    ],
    # T6 Black Sharpshooter — sword + bow, elite + cape pauldron
    "dg_uruk_black_sharpshooter": [
        ("Item0",  "wm_isengard_bow_a02"),
        ("Item1",  "piercing_arrows"),
        ("Item2",  "piercing_arrows"),
        ("Item3",  "wm_dol_goldur_1h_sword_a01"),
        ("Head",   "sk_dg_uruk_helmet_heavy_e"),
        ("Body",   "sk_dg_uruk_chest_elite_e"),
        ("Cape",   "sk_dg_uruk_pauldron_elite_cape_e"),
        ("Gloves", "sk_dg_uruk_bracer_elite_g"),
        ("Leg",    "sk_dg_uruk_boots_elite_c"),
    ],
    # ===== FELL WARG RIDER LINE (mercenary) =====
    # T3 Warg Tracker — 1H sword only, light, on warg_brown
    "dg_warg_scout": [
        ("Item0",        "wm_dol_goldur_1h_sword_a03"),
        ("Head",         "sk_dg_uruk_helmet_light_d"),
        ("Body",         "sk_dg_uruk_chest_light_c"),
        ("Gloves",       "sk_dg_uruk_bracer_med_b"),
        ("Leg",          "sk_dg_uruk_boots_med_c"),
        ("Horse",        "warg_brown"),
        ("HorseHarness", "warg_saddle"),
    ],
    # T4 Warg Rider — 1H sword + shield + 2H halberd, on warg_brown
    "dg_warg_raider": [
        ("Item0",        "wm_dol_goldur_1h_sword_a02"),
        ("Item1",        "wm_gundabad_shield_a02"),
        ("Item2",        "wm_dol_goldur_halberd_a02"),
        ("Head",         "sk_dg_uruk_helmet_med_g"),
        ("Body",         "sk_dg_uruk_chest_med_c"),
        ("Cape",         "sk_dg_uruk_pauldron_med_a"),
        ("Gloves",       "sk_dg_uruk_bracer_heavy_c"),
        ("Leg",          "sk_dg_uruk_boots_heavy_e"),
        ("Horse",        "warg_brown"),
        ("HorseHarness", "warg_saddle"),
    ],
    # T5 Warg Ravager — 1H sword + shield + 2H halberd, on warg_dark
    "dg_warg_skirmisher": [
        ("Item0",        "wm_dol_goldur_1h_sword_a02"),
        ("Item1",        "wm_gundabad_shield_a02"),
        ("Item2",        "wm_dol_goldur_halberd_a01"),
        ("Head",         "sk_dg_uruk_helmet_med_h"),
        ("Body",         "sk_dg_uruk_chest_med_d"),
        ("Cape",         "sk_dg_uruk_pauldron_heavy_a"),
        ("Gloves",       "sk_dg_uruk_bracer_heavy_d"),
        ("Leg",          "sk_dg_uruk_boots_heavy_f"),
        ("Horse",        "warg_dark"),
        ("HorseHarness", "warg_saddle"),
    ],
    # T6 Warg Fang — 1H sword + shield + 2H halberd, heavy gear, on warg_dark
    "dg_warg_red_fang": [
        ("Item0",        "wm_dol_goldur_1h_sword_a01"),
        ("Item1",        "wm_gundabad_shield_a03"),
        ("Item2",        "wm_dol_goldur_halberd_a01"),
        ("Head",         "sk_dg_uruk_helmet_heavy_g"),
        ("Body",         "sk_dg_uruk_chest_heavy_c"),
        ("Cape",         "sk_dg_uruk_pauldron_heavy_b"),
        ("Gloves",       "sk_dg_uruk_bracer_elite_d"),
        ("Leg",          "sk_dg_uruk_boots_elite_e"),
        ("Horse",        "warg_dark"),
        ("HorseHarness", "warg_saddle"),
    ],
    # T7 Fell Ravager — 1H sword + shield + 2H halberd, elite gear, on warg_dark (fell warg proxy)
    "dg_fell_warg_rider": [
        ("Item0",        "wm_dol_goldur_1h_sword_a01"),
        ("Item1",        "wm_gundabad_shield_a03"),
        ("Item2",        "wm_dol_goldur_halberd_a01"),
        ("Head",         "sk_dg_uruk_helmet_elite_g"),
        ("Body",         "sk_dg_uruk_chest_elite_c"),
        ("Cape",         "sk_dg_uruk_pauldron_heavy_c"),
        ("Gloves",       "sk_dg_uruk_bracer_elite_e"),
        ("Leg",          "sk_dg_uruk_boots_lord_e"),
        ("Horse",        "warg_dark"),
        ("HorseHarness", "warg_saddle"),
    ],
}

# No new troops — Khamul human line is already complete in the file.


def find_npc_block(content: str, troop_id: str) -> Tuple[int, int]:
    # Match any leading whitespace on the open/close tags (DG file is 4-space,
    # but fall back to flexible whitespace so future re-indents don't break this).
    pattern = re.compile(
        r'(?m)^([ \t]*)<NPCCharacter\s+id="' + re.escape(troop_id) + r'"'
        r'.*?'
        r'^\1</NPCCharacter>',
        re.DOTALL,
    )
    m = pattern.search(content)
    if not m:
        import sys as _sys
        print(f"  WARN: NPCCharacter block not found for '{troop_id}' (indent mismatch?)", file=_sys.stderr)
        return -1, -1
    end = m.end()
    if content[end:end+2] == '\n\n':
        end += 2
    elif content[end:end+1] == '\n':
        end += 1
    return m.start(), end


# EquipmentSet blocks in the DG file span multiple lines:
#     <EquipmentSet
#         id="..."
#         equipmentType="Civilian" />
# Capture the whole block as a unit so we preserve it across the rewrite.
EQUIPMENT_SET_RE = re.compile(
    r'[ \t]*<EquipmentSet\b[^>]*?/>',
    re.DOTALL,
)


def build_equipment_xml(slots: List[Tuple[str, str]], indent: str) -> str:
    inner = "\n".join(
        f'{indent}    <equipment slot="{slot}" id="Item.{item_id}" />'
        for slot, item_id in slots
    )
    return f'{indent}<EquipmentRoster>\n{inner}\n{indent}</EquipmentRoster>'


def replace_equipment(npc_block: str, slots: List[Tuple[str, str]]) -> str:
    """Replace the <Equipments> container's content: collapse all
    <EquipmentRoster> blocks into a single new one, preserve any
    <EquipmentSet ... /> (civilian template etc.) verbatim."""
    # Match the full container including open + close tag indentation
    container_re = re.compile(
        r'(?P<lead>\n?)(?P<indent>[ \t]*)<Equipments>'
        r'(?P<inner>.*?)'
        r'[ \t]*</Equipments>',
        re.DOTALL,
    )
    m = container_re.search(npc_block)
    if not m:
        raise RuntimeError("No <Equipments> container found")

    outer_indent = m.group("indent")       # spaces before <Equipments> (e.g. 8 spaces)
    inner_indent = outer_indent + "    "    # one level deeper (12 spaces)
    inner = m.group("inner")

    # Preserve any <EquipmentSet ... /> blocks (may span multiple lines)
    set_blocks = EQUIPMENT_SET_RE.findall(inner)

    new_roster = build_equipment_xml(slots, inner_indent)

    parts = [new_roster]
    for set_block in set_blocks:
        # Re-indent the set block to inner_indent (collapse multi-line to canonical form)
        # Easiest: strip + rewrap with new indent
        normalized = re.sub(r'\s+', ' ', set_block.strip())
        parts.append(inner_indent + normalized)

    new_container = (
        m.group("lead")
        + outer_indent + "<Equipments>\n"
        + "\n".join(parts) + "\n"
        + outer_indent + "</Equipments>"
    )
    return npc_block[:m.start()] + new_container + npc_block[m.end():]


NAME_RE = re.compile(r'(name="\{=[^}]+\})[^"]*(")')
LEVEL_RE = re.compile(r'(level=")[^"]*(")')
DEFAULT_GROUP_RE = re.compile(r'(default_group=")[^"]*(")')


def update_name_and_level(npc_block: str, new_name: str, new_level: str) -> str:
    npc_block = NAME_RE.sub(rf'\g<1>[Dol Guldur] {new_name}\g<2>', npc_block, count=1)
    npc_block = LEVEL_RE.sub(rf'\g<1>{new_level}\g<2>', npc_block, count=1)
    return npc_block


def update_default_group(npc_block: str, new_group: str) -> str:
    return DEFAULT_GROUP_RE.sub(rf'\g<1>{new_group}\g<2>', npc_block, count=1)


UPGRADE_BLOCK_RE = re.compile(
    r'<upgrade_targets\s*/?>'
    r'(?:.*?</upgrade_targets>)?',
    re.DOTALL,
)


def update_upgrade_targets(npc_block: str, targets: List[str]) -> str:
    if not targets:
        new_block = '<upgrade_targets />'
    else:
        lines = '\n'.join(
            f'            <upgrade_target id="NPCCharacter.{t}" />'
            for t in targets
        )
        new_block = f'<upgrade_targets>\n{lines}\n        </upgrade_targets>'
    return UPGRADE_BLOCK_RE.sub(new_block, npc_block, count=1)


def apply(dry_run: bool = False):
    if not os.path.exists(TROOPS_FILE):
        print(f"ERROR: {TROOPS_FILE} not found", file=sys.stderr)
        sys.exit(1)

    with open(TROOPS_FILE, "r", encoding="utf-8") as f:
        content = f.read()
    orig_len = len(content)

    # 1. Apply equipment + name + level + default_group + upgrade_targets
    refit = 0
    missing = []
    for troop_id, slots in EQUIPMENT.items():
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            missing.append(troop_id)
            continue
        npc_block = content[start:end]
        npc_block = replace_equipment(npc_block, slots)
        if troop_id in RENAMES:
            npc_block = update_name_and_level(
                npc_block,
                RENAMES[troop_id]["name"],
                RENAMES[troop_id]["level"],
            )
        if troop_id in DEFAULT_GROUPS:
            npc_block = update_default_group(npc_block, DEFAULT_GROUPS[troop_id])
        if troop_id in UPGRADE_TARGETS:
            npc_block = update_upgrade_targets(npc_block, UPGRADE_TARGETS[troop_id])
        content = content[:start] + npc_block + content[end:]
        refit += 1

    # 2. Delete redundant troops + remove orphan upgrade_target refs
    deleted = 0
    not_found = []
    for troop_id in DELETE_IDS:
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            not_found.append(troop_id)
            continue
        content = content[:start] + content[end:]
        deleted += 1

    removed_refs = 0
    for troop_id in DELETE_IDS:
        pattern = re.compile(
            r'\s*<upgrade_target\s+id="NPCCharacter\.' + re.escape(troop_id) + r'"\s*/>'
        )
        content, n = pattern.subn('', content)
        removed_refs += n

    # If an <upgrade_targets> ended up empty after ref removal, collapse to self-close
    content = re.sub(
        r'<upgrade_targets>\s*</upgrade_targets>',
        '<upgrade_targets />',
        content,
    )

    print(f"\nDol Guldur troop revamp:")
    print(f"  Equipment refits:        {refit}/{len(EQUIPMENT)}")
    print(f"  Troops deleted:          {deleted}/{len(DELETE_IDS)}")
    print(f"  Upgrade refs removed:    {removed_refs}")
    print(f"  File size: {orig_len:,} -> {len(content):,} bytes")
    if missing:
        print(f"  MISSING (no NPCCharacter found): {missing}")
    if not_found:
        print(f"  Not found for delete (already gone): {not_found}")

    if dry_run:
        print("\n(dry-run — no file written)")
        return

    with open(TROOPS_FILE, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"\nWrote: {TROOPS_FILE}")


def main():
    p = argparse.ArgumentParser(description="Apply Dol Guldur troop revamp (issue #212)")
    p.add_argument("--dry-run", action="store_true")
    p.add_argument("--apply", action="store_true")
    args = p.parse_args()
    if args.dry_run:
        apply(dry_run=True)
    elif args.apply:
        apply(dry_run=False)
    else:
        p.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
