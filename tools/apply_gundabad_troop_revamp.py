#!/usr/bin/env python3
"""Apply KEYforce Gundabad troop tree revamp per spec.

Issue: #212
Source-of-truth: E:\\repos\\lotraom-assets\\tools\\gundabad_armors_and_troops.txt

Per spec, Gundabad has ONE unit group with 4 branches:
  Infantry: Warrior -> Brawler -> Raider -> Ravager -> Infantry -> Mountain Guard -> Bolg's Ironfang
  Shock/Crusher: (from Brawler) -> Mauler -> Bone Breaker -> Skull Crusher
  Spear/Pike: (from Brawler) -> Impaler -> Spearman -> Pike-Reaver -> War-Pike
  Wolf Rider: (from Spearman) -> Wolf Rider -> Fang Rider -> Azog's Defiler

Strategy: repurpose existing IDs to spec roles (preserves save compat for
in-flight campaigns). Only Bolg's Ironfang (T8) is genuinely new.

Deletes 4 redundant troops (duplicates of repurposed roles).

Usage:
    python tools/apply_gundabad_troop_revamp.py --dry-run
    python tools/apply_gundabad_troop_revamp.py --apply
"""
import argparse
import os
import re
import sys
from typing import Dict, List, Tuple

TROOPS_FILE = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Main", "_Module", "ModuleData", "troops", "troops_gundabad.xml"
)

DELETE_IDS = {
    "gundabad_champion",              # T8 Azog's Champion — duplicates Bolg's Ironfang
    "gundabad_pike_warrior",          # duplicate of Spearman → Pike-Reaver chain
    "gundabad_veteran_pike_warrior",  # duplicate of Pike-Reaver role
    "gundabad_warg_warrior",          # collapses 4-tier warg chain to spec's 3-tier
}

# Display name + level updates (rename + repurpose in-place; preserves IDs for save compat)
RENAMES: Dict[str, Dict[str, str]] = {
    "gundabad_snaga":                    {"name": "Pale Uruk Warrior",       "level": "11"},
    "gundabad_grunt":                    {"name": "Pale Uruk Brawler",       "level": "16"},
    "gundabad_fighter":                  {"name": "Pale Uruk Raider",        "level": "21"},
    "gundabad_brute":                    {"name": "Pale Uruk Ravager",       "level": "26"},
    "gundabad_veteran_sword_warrior":    {"name": "Pale Uruk Infantry",      "level": "31"},
    "gundabad_chosen_of_tharzog":        {"name": "Pale Uruk Mountain Guard","level": "36"},
    "gundabad_sword_warrior":            {"name": "Pale Uruk Mauler",        "level": "26"},
    "gundabad_berserker":                {"name": "Pale Uruk Bone Breaker",  "level": "31"},
    "gundabad_veteran_berserker":        {"name": "Pale Uruk Skull Crusher", "level": "36"},
    "gundabad_guard":                    {"name": "Pale Uruk Impaler",       "level": "21"},
    "gundabad_spear_warrior":            {"name": "Pale Uruk Spearman",      "level": "26"},
    "gundabad_veteran_spear_warrior":    {"name": "Pale Uruk Pike-Reaver",   "level": "31"},
    "gundabad_guardian_of_the_tower":    {"name": "Pale Uruk War-Pike",      "level": "36"},
    "gundabad_warg_tamer":               {"name": "Pale Uruk Wolf Rider",    "level": "31"},
    "gundabad_tracker":                  {"name": "Pale Uruk Fang Rider",    "level": "36"},
    "gundabad_dread_rider_of_the_tower": {"name": "Azog's Defiler",          "level": "41"},
}

# Upgrade chain rewires per spec tree
UPGRADE_TARGETS: Dict[str, List[str]] = {
    "gundabad_snaga":                     ["gundabad_grunt"],
    "gundabad_grunt":                     ["gundabad_fighter", "gundabad_guard"],
    "gundabad_fighter":                   ["gundabad_brute", "gundabad_sword_warrior"],
    "gundabad_brute":                     ["gundabad_veteran_sword_warrior"],
    "gundabad_veteran_sword_warrior":     ["gundabad_chosen_of_tharzog"],
    "gundabad_chosen_of_tharzog":         ["gundabad_bolgs_ironfang"],
    "gundabad_bolgs_ironfang":            [],
    "gundabad_sword_warrior":             ["gundabad_berserker"],
    "gundabad_berserker":                 ["gundabad_veteran_berserker"],
    "gundabad_veteran_berserker":         [],
    "gundabad_guard":                     ["gundabad_spear_warrior"],
    "gundabad_spear_warrior":             ["gundabad_warg_tamer", "gundabad_veteran_spear_warrior"],
    "gundabad_veteran_spear_warrior":     ["gundabad_guardian_of_the_tower"],
    "gundabad_guardian_of_the_tower":     [],
    "gundabad_warg_tamer":                ["gundabad_tracker"],
    "gundabad_tracker":                   ["gundabad_dread_rider_of_the_tower"],
    "gundabad_dread_rider_of_the_tower":  [],
}

# Per-troop equipment replacement (preserves Horse/HorseHarness for cavalry)
EQUIPMENT: Dict[str, List[Tuple[str, str]]] = {
    # ---------- SHARED BASE (T2-T3) ----------
    "gundabad_snaga": [
        ("Item0", "wm_gundabad_axe_a03"),
        ("Head",  "sk_gb_uruk_helmet_light_a"),
        ("Body",  "sk_gb_uruk_chest_light_a"),
        ("Leg",   "sk_gb_uruk_boots_light_a"),
    ],
    "gundabad_grunt": [
        ("Item0", "wm_gundabad_axe_a02"),
        ("Head",  "sk_gb_uruk_helmet_light_a"),
        ("Body",  "sk_gb_uruk_chest_light_c"),
        ("Cape",  "sk_gb_uruk_cape_a"),
        ("Leg",   "sk_gb_uruk_boots_light_b"),
    ],
    # ---------- INFANTRY LINE (T4-T8) ----------
    "gundabad_fighter": [
        ("Item0",  "wm_gundabad_axe_a02"),
        ("Item1",  "wm_gundabad_shield_a01"),
        ("Head",   "sk_gb_uruk_helmet_med_a"),
        ("Body",   "sk_gb_uruk_chest_med_b"),
        ("Cape",   "sk_gb_uruk_pauldron_med_a"),
        ("Gloves", "sk_gb_uruk_bracer_med_a"),
        ("Leg",    "sk_gb_uruk_boots_med_b"),
    ],
    "gundabad_brute": [
        ("Item0",  "wm_gundabad_axe_a01"),
        ("Item1",  "wm_gundabad_shield_a02"),
        ("Head",   "sk_gb_uruk_helmet_heavy_a"),
        ("Body",   "sk_gb_uruk_chest_heavy_b"),
        ("Cape",   "sk_gb_uruk_pauldron_heavy_a"),
        ("Gloves", "sk_gb_uruk_bracer_heavy_a"),
        ("Leg",    "sk_gb_uruk_boots_heavy_c"),
    ],
    "gundabad_veteran_sword_warrior": [
        ("Item0",  "wm_gundabad_sword_a02"),
        ("Item1",  "wm_gundabad_shield_a02"),
        ("Head",   "sk_gb_uruk_helmet_heavy_b"),
        ("Body",   "sk_gb_uruk_chest_heavy_e"),
        ("Cape",   "sk_gb_uruk_pauldron_heavy_b"),
        ("Gloves", "sk_gb_uruk_bracer_heavy_b"),
        ("Leg",    "sk_gb_uruk_boots_heavy_d"),
    ],
    "gundabad_chosen_of_tharzog": [
        ("Item0",  "wm_gundabad_mace_a01"),
        ("Item1",  "wm_gundabad_shield_a03"),
        ("Head",   "sk_gb_uruk_helmet_elite_a"),
        ("Body",   "sk_gb_uruk_chest_elite_b"),
        ("Cape",   "sk_gb_uruk_pauldron_elite_a"),
        ("Gloves", "sk_gb_uruk_bracer_elite_a"),
        ("Leg",    "sk_gb_uruk_boots_elite_b"),
    ],
    # ---------- SHOCK/CRUSHER LINE (T5-T7) ----------
    "gundabad_sword_warrior": [
        ("Item0",  "wm_gundabad_mace_b01"),
        ("Head",   "sk_gb_uruk_helmet_med_f"),
        ("Body",   "sk_gb_uruk_chest_med_a"),
        ("Cape",   "sk_gb_uruk_pauldron_med_a"),
        ("Gloves", "sk_gb_uruk_bracer_med_b"),
        ("Leg",    "sk_gb_uruk_boots_med_a"),
    ],
    "gundabad_berserker": [
        ("Item0",  "wm_gundabad_mace_b01"),
        ("Item1",  "wm_gundabad_javelin_a01"),
        ("Head",   "sk_gb_uruk_helmet_heavy_j"),
        ("Body",   "sk_gb_uruk_chest_heavy_a"),
        ("Cape",   "sk_gb_uruk_pauldron_heavy_a"),
        ("Gloves", "sk_gb_uruk_bracer_heavy_c"),
        ("Leg",    "sk_gb_uruk_boots_heavy_a"),
    ],
    "gundabad_veteran_berserker": [
        ("Item0",  "wm_gundabad_mace_b01"),
        ("Item1",  "wm_gundabad_javelin_a02"),
        ("Item2",  "wm_gundabad_javelin_a02"),
        ("Head",   "sk_gb_uruk_helmet_elite_f"),
        ("Body",   "sk_gb_uruk_chest_elite_a"),
        ("Cape",   "sk_gb_uruk_pauldron_cape_elite_a"),
        ("Gloves", "sk_gb_uruk_bracer_elite_b"),
        ("Leg",    "sk_gb_uruk_boots_elite_a"),
    ],
    # ---------- SPEAR/PIKE LINE (T4-T7) ----------
    "gundabad_guard": [
        ("Item0",  "wm_gundabad_spear_a03"),
        ("Head",   "sk_gb_uruk_helmet_light_c"),
        ("Body",   "sk_gb_uruk_chest_med_b"),
        ("Cape",   "sk_gb_uruk_cape_b"),
        ("Gloves", "sk_gb_uruk_bracer_med_a"),
        ("Leg",    "sk_gb_uruk_boots_med_b"),
    ],
    "gundabad_spear_warrior": [
        ("Item0",  "wm_gundabad_spear_a02"),
        ("Head",   "sk_gb_uruk_helmet_med_c"),
        ("Body",   "sk_gb_uruk_chest_heavy_b"),
        ("Cape",   "sk_gb_uruk_cape_c"),
        ("Gloves", "sk_gb_uruk_bracer_heavy_a"),
        ("Leg",    "sk_gb_uruk_boots_heavy_c"),
    ],
    "gundabad_veteran_spear_warrior": [
        ("Item0",  "isengard_pike_a"),
        ("Head",   "sk_gb_uruk_helmet_heavy_g"),
        ("Body",   "sk_gb_uruk_chest_heavy_e"),
        ("Cape",   "sk_gb_uruk_pauldron_heavy_b"),
        ("Gloves", "sk_gb_uruk_bracer_heavy_b"),
        ("Leg",    "sk_gb_uruk_boots_heavy_d"),
    ],
    "gundabad_guardian_of_the_tower": [
        ("Item0",  "isengard_pike_b"),
        ("Head",   "sk_gb_uruk_helmet_elite_e"),
        ("Body",   "sk_gb_uruk_chest_elite_b"),
        ("Cape",   "sk_gb_uruk_pauldron_elite_b"),
        ("Gloves", "sk_gb_uruk_bracer_elite_a"),
        ("Leg",    "sk_gb_uruk_boots_elite_b"),
    ],
    # ---------- WOLF RIDER LINE (T6-T8) — all Fell Warg ----------
    "gundabad_warg_tamer": [
        ("Item0",        "wm_gundabad_spear_a02"),
        ("Item1",        "wm_gundabad_shield_a02"),
        ("Head",         "sk_gb_uruk_helmet_heavy_d"),
        ("Body",         "sk_gb_uruk_chest_heavy_c"),
        ("Cape",         "sk_gb_uruk_pauldron_heavy_c"),
        ("Gloves",       "sk_gb_uruk_bracer_heavy_d"),
        ("Leg",          "sk_gb_uruk_boots_heavy_e"),
        ("Horse",        "warg_brown"),
        ("HorseHarness", "warg_saddle"),
    ],
    "gundabad_tracker": [
        ("Item0",        "wm_gundabad_spear_a01"),
        ("Item1",        "wm_gundabad_shield_a03"),
        ("Item2",        "wm_gundabad_axe_a01"),
        ("Head",         "sk_gb_uruk_helmet_heavy_e"),
        ("Body",         "sk_gb_uruk_chest_elite_c"),
        ("Cape",         "sk_gb_uruk_pauldron_elite_c"),
        ("Gloves",       "sk_gb_uruk_bracer_elite_c"),
        ("Leg",          "sk_gb_uruk_boots_elite_c"),
        ("Horse",        "warg_dark"),
        ("HorseHarness", "warg_saddle"),
    ],
    "gundabad_dread_rider_of_the_tower": [
        ("Item0",        "wm_gundabad_spear_a01"),
        ("Item1",        "wm_gundabad_shield_a03"),
        ("Item2",        "wm_gundabad_axe_a01"),
        ("Head",         "sk_gb_uruk_helmet_elite_d"),
        ("Body",         "sk_gb_uruk_chest_lord_c"),
        ("Cape",         "sk_gb_uruk_pauldron_cape_elite_c"),
        ("Gloves",       "sk_gb_uruk_bracer_elite_c"),
        ("Leg",          "sk_gb_uruk_boots_elite_c"),
        ("Horse",        "warg_albino"),
        ("HorseHarness", "warg_saddle"),
    ],
}

# New troops — only Bolg's Ironfang T8 is genuinely new
NEW_TROOPS_XML = """
  <NPCCharacter
      id="gundabad_bolgs_ironfang"
      race="pale_uruk"
      default_group="Infantry"
      level="36"
      name="{=aom_gundabad_bolgs_ironfang_name}[Gundabad] Bolg's Ironfang"
      occupation="Soldier"
      culture="Culture.gundabad">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="150" />
      <skill id="Riding" value="25" />
      <skill id="OneHanded" value="280" />
      <skill id="TwoHanded" value="240" />
      <skill id="Polearm" value="240" />
      <skill id="Bow" value="20" />
      <skill id="Crossbow" value="15" />
      <skill id="Throwing" value="95" />
    </skills>
    <upgrade_targets />
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_gundabad_mace_a01" />
        <equipment slot="Item1" id="Item.wm_gundabad_shield_a03" />
        <equipment slot="Head" id="Item.sk_gb_uruk_helmet_elite_b" />
        <equipment slot="Body" id="Item.sk_gb_uruk_chest_lord_b" />
        <equipment slot="Cape" id="Item.sk_gb_uruk_pauldron_cape_elite_b" />
        <equipment slot="Gloves" id="Item.sk_gb_uruk_bracer_elite_c" />
        <equipment slot="Leg" id="Item.sk_gb_uruk_boots_elite_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>
"""


def find_npc_block(content: str, troop_id: str) -> Tuple[int, int]:
    pattern = re.compile(
        r'  <NPCCharacter\s+id="' + re.escape(troop_id) + r'"'
        r'.*?'
        r'  </NPCCharacter>',
        re.DOTALL,
    )
    m = pattern.search(content)
    if not m:
        return -1, -1
    end = m.end()
    if content[end:end+2] == '\n\n':
        end += 2
    elif content[end:end+1] == '\n':
        end += 1
    return m.start(), end


EQUIP_ROSTER_RE = re.compile(
    r'(\s+)<EquipmentRoster>(.*?)</EquipmentRoster>',
    re.DOTALL,
)


def extract_horse_lines(equip_block: str) -> List[str]:
    lines = []
    for line in equip_block.splitlines():
        stripped = line.strip()
        if stripped.startswith('<equipment slot="Horse"') or \
           stripped.startswith('<equipment slot="HorseHarness"'):
            lines.append(line)
    return lines


def build_equipment_xml(slots: List[Tuple[str, str]], indent: str) -> str:
    inner = "\n".join(
        f'{indent}  <equipment slot="{slot}" id="Item.{item_id}" />'
        for slot, item_id in slots
    )
    return f'{indent}<EquipmentRoster>\n{inner}\n{indent}</EquipmentRoster>'


def replace_equipment(npc_block: str, slots: List[Tuple[str, str]]) -> str:
    m = EQUIP_ROSTER_RE.search(npc_block)
    if not m:
        raise RuntimeError("No <EquipmentRoster> found")
    indent_after_newline = m.group(1).lstrip("\n") or "      "
    new_roster = build_equipment_xml(slots, indent_after_newline)
    return npc_block[:m.start()] + "\n" + new_roster + npc_block[m.end():]


NAME_RE = re.compile(r'(name="\{=[^}]+\})[^"]*(")')
LEVEL_RE = re.compile(r'(level=")[^"]*(")')


def update_name_and_level(npc_block: str, troop_id: str, new_name: str, new_level: str) -> str:
    npc_block = NAME_RE.sub(rf'\g<1>[Gundabad] {new_name}\g<2>', npc_block, count=1)
    npc_block = LEVEL_RE.sub(rf'\g<1>{new_level}\g<2>', npc_block, count=1)
    return npc_block


UPGRADE_BLOCK_RE = re.compile(
    r'<upgrade_targets\s*/?>'
    r'(?:.*?</upgrade_targets>)?',
    re.DOTALL,
)


def update_upgrade_targets(npc_block: str, targets: List[str]) -> str:
    if not targets:
        new_block = '<upgrade_targets />'
    else:
        lines = '\n'.join(f'      <upgrade_target id="NPCCharacter.{t}" />' for t in targets)
        new_block = f'<upgrade_targets>\n{lines}\n    </upgrade_targets>'
    return UPGRADE_BLOCK_RE.sub(new_block, npc_block, count=1)


def apply(dry_run: bool = False):
    if not os.path.exists(TROOPS_FILE):
        print(f"ERROR: {TROOPS_FILE} not found", file=sys.stderr)
        sys.exit(1)

    with open(TROOPS_FILE, "r", encoding="utf-8") as f:
        content = f.read()
    orig_len = len(content)

    # 1. Apply equipment + name + level + upgrade_targets to existing troops
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
                npc_block, troop_id,
                RENAMES[troop_id]["name"],
                RENAMES[troop_id]["level"],
            )
        if troop_id in UPGRADE_TARGETS:
            npc_block = update_upgrade_targets(npc_block, UPGRADE_TARGETS[troop_id])
        content = content[:start] + npc_block + content[end:]
        refit += 1

    # 2. Add new troops (Bolg's Ironfang) — insert before </NPCCharacters>
    added = 0
    if 'id="gundabad_bolgs_ironfang"' not in content:
        content = content.replace(
            '</NPCCharacters>',
            NEW_TROOPS_XML.rstrip() + '\n</NPCCharacters>'
        )
        added = 1

    # 3. Delete redundant troops + remove any upgrade_target refs to them
    deleted = 0
    for troop_id in DELETE_IDS:
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            print(f"  WARN: delete target {troop_id} not found", file=sys.stderr)
            continue
        content = content[:start] + content[end:]
        deleted += 1
    # Remove orphan upgrade_target refs
    removed_refs = 0
    for troop_id in DELETE_IDS:
        pattern = re.compile(
            r'\s*<upgrade_target id="NPCCharacter\.' + re.escape(troop_id) + r'" />'
        )
        content, n = pattern.subn('', content)
        removed_refs += n

    print(f"\nGundabad troop revamp:")
    print(f"  Equipment refits:     {refit}/{len(EQUIPMENT)}")
    print(f"  New troops added:     {added}/1")
    print(f"  Troops deleted:       {deleted}/{len(DELETE_IDS)}")
    print(f"  Upgrade refs removed: {removed_refs}")
    print(f"  File size: {orig_len:,} -> {len(content):,} bytes")
    if missing:
        print(f"  MISSING (no NPCCharacter found): {missing}")

    if dry_run:
        print("\n(dry-run — no file written)")
        return

    with open(TROOPS_FILE, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"\nWrote: {TROOPS_FILE}")


def main():
    p = argparse.ArgumentParser(description="Apply Gundabad troop revamp (issue #212)")
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
