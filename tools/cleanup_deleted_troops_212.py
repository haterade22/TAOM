#!/usr/bin/env python3
"""Clean up references to troops deleted in Issue #212 troop revamp.

For each deleted ID across all 5 cultures:
- Remove `<PartyTemplateStack troop="NPCCharacter.<id>" .../>` from `taom_partyTemplates.xml`
- Remove `<TroopWeight id="<id>" .../>` from `troop_weights.xml`
- Remove `<Troop id="<id>" .../>` from `troop_resource_costs.xml`
- Replace mappings in spec-canonical replacements where troops were "demoted" but mapping makes sense.

Usage:
    python tools/cleanup_deleted_troops_212.py --dry-run
    python tools/cleanup_deleted_troops_212.py --apply
"""
import argparse
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

DELETED_IDS = [
    # Mordor (14)
    "mordor_uruk_feller", "mordor_uruk_ravager", "mordor_uruk_executioner",
    "mordor_uruk_darkblade", "mordor_uruk_halberdier", "mordor_uruk_reaper",
    "mordor_uruk_deathwarden", "mordor_uruk_marksman", "mordor_uruk_raider",
    "mordor_uruk_scout",
    "mordor_orc_warbrute", "mordor_orc_blackguard",  # mordor_orc_warrior renamed/re-id'd
    "mordor_black_numenorean",
    # Dol Guldur (12)
    "dg_initiate", "dg_disciple", "dg_infantry", "dg_warden",
    "dg_archer", "dg_marksman",
    "dg_uruk_spawn", "dg_uruk_gnawer", "dg_uruk_butcher",
    "dg_uruk_berserker", "dg_uruk_black_howler", "dg_uruk_rage_bearer",
    # Gundabad (4) — already cleaned manually
    # "gundabad_champion", "gundabad_pike_warrior",
    # "gundabad_veteran_pike_warrior", "gundabad_warg_warrior",
]

TARGETS = {
    "taom_partyTemplates": "Main/_Module/ModuleData/taom_partyTemplates.xml",
    "troop_weights":       "Main/_Module/ModuleData/TroopWeights/troop_weights.xml",
    "resource_costs":      "Main/_Module/ModuleData/special_resources/troop_resource_costs.xml",
}


def remove_party_template_stack(content: str, troop_id: str) -> tuple:
    """Remove `<PartyTemplateStack ... troop="NPCCharacter.<id>" ... />` line."""
    pattern = re.compile(
        r'\s*<PartyTemplateStack[^/>]*troop="NPCCharacter\.' + re.escape(troop_id) + r'"[^/>]*/>',
        re.DOTALL
    )
    new_content, n = pattern.subn('', content)
    return new_content, n


def remove_troop_weight(content: str, troop_id: str) -> tuple:
    """Remove `<TroopWeight id="<id>" .../>` line."""
    pattern = re.compile(
        r'\s*<TroopWeight\s+id="' + re.escape(troop_id) + r'"[^/>]*/>',
        re.DOTALL
    )
    new_content, n = pattern.subn('', content)
    return new_content, n


def remove_troop_resource_cost(content: str, troop_id: str) -> tuple:
    """Remove `<Troop id="<id>" .../>` line."""
    pattern = re.compile(
        r'\s*<Troop\s+id="' + re.escape(troop_id) + r'"[^/>]*/>',
        re.DOTALL
    )
    new_content, n = pattern.subn('', content)
    return new_content, n


CLEANUP = {
    "taom_partyTemplates": remove_party_template_stack,
    "troop_weights":       remove_troop_weight,
    "resource_costs":      remove_troop_resource_cost,
}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    if not (args.dry_run or args.apply):
        parser.print_help()
        sys.exit(1)

    total_removed = 0
    for key, rel_path in TARGETS.items():
        path = os.path.join(REPO, rel_path)
        if not os.path.exists(path):
            print(f"  SKIP: {rel_path} not found")
            continue
        with open(path, "r", encoding="utf-8") as f:
            content = f.read()
        orig = content
        per_file = 0
        for troop_id in DELETED_IDS:
            content, n = CLEANUP[key](content, troop_id)
            per_file += n
        if per_file > 0:
            print(f"  {rel_path}: removed {per_file} refs to deleted troops")
            total_removed += per_file
            if args.apply:
                with open(path, "w", encoding="utf-8") as f:
                    f.write(content)
        else:
            print(f"  {rel_path}: 0 refs")

    print(f"\nTotal removed: {total_removed}")
    if args.dry_run:
        print("(dry-run — no files written)")


if __name__ == "__main__":
    main()
