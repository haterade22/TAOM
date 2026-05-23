#!/usr/bin/env python3
"""Add new KEYforce troop-revamp troops to kingdom_hero_party templates.

Inserts PartyTemplateStack lines before </stacks> of each culture's primary
hero party template, ensuring new troops appear in lord armies.

Mordor: +21 troops (10 orc + 6 warg + 5 black uruk additions)
Isengard: +13 new orc troops (Section 1 entire)
Erebor: +13 new Iron Hills Noble troops
Dol Guldur: no new troops (Khamul already wired in existing template)
Gundabad: bolgs_ironfang already added manually

Usage:
    python tools/expand_party_templates_212.py --dry-run
    python tools/expand_party_templates_212.py --apply
"""
import argparse
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEMPLATES = os.path.join(REPO, "Main", "_Module", "ModuleData", "taom_partyTemplates.xml")

# Per-culture new troops to insert into kingdom_hero_party_<culture>_template.
# Format: (troop_id, min, max).
ADDITIONS = {
    "mordor": [
        # Orc line T1-T5 (race=orc)
        ("mordor_orc_recruit",     5, 12),
        ("mordor_orc_lackey",      4, 10),
        ("mordor_orc_fighter",     3, 8),
        ("mordor_orc_warrior",     2, 6),
        ("mordor_orc_infantry",    2, 5),
        ("mordor_orc_impaler",     1, 4),
        ("mordor_orc_reaver",      1, 3),
        ("mordor_orc_hunter",      3, 8),
        ("mordor_orc_scout",       2, 5),
        ("mordor_orc_archer",      1, 4),
        # Nurn Warg Riders T1-T6 (race=orc)
        ("mordor_warg_tamer",      2, 5),
        ("mordor_warg_rider",      2, 4),
        ("mordor_warg_raider",     1, 3),
        ("mordor_warg_reaver",     1, 3),
        ("mordor_warg_ravager",    1, 2),
        ("mordor_warg_beastmaster",1, 2),
        # New Black Uruk additions (race=uruk)
        ("mordor_uruk_fighter",      2, 5),
        ("mordor_uruk_skirmisher",   2, 5),
        ("mordor_uruk_heavy_archer", 1, 3),
        ("mordor_uruk_crossbow",     2, 5),
        ("mordor_uruk_heavy_crossbow",1, 3),
    ],
    "isengard": [
        # Section 1 orc line (race=orc) — all 13 new
        ("isengard_orc_grunt",         5, 12),
        ("isengard_orc_brawler",       4, 10),
        ("isengard_orc_warrior",       3, 8),
        ("isengard_orc_marauder",      2, 6),
        ("isengard_orc_reaver",        1, 4),
        ("isengard_orc_berserker",     3, 8),
        ("isengard_orc_ravager",       2, 6),
        ("isengard_orc_butcher",       1, 4),
        ("isengard_orc_slayer",        1, 3),
        ("isengard_orc_warg_scout_v2", 2, 5),
        ("isengard_orc_warg_rider_v2", 2, 4),
        ("isengard_orc_warg_raider_v2",1, 3),
        ("isengard_orc_warg_ravager_v2",1, 2),
    ],
    "erebor": [
        # Iron Hills Noble line (race=dwarf) — all 13 new
        ("iron_hills_noble",                       4, 10),
        ("iron_hills_noble_scout",                 3, 8),
        ("iron_hills_noble_sharpshooter",          2, 6),
        ("iron_hills_noble_veteran_sharpshooter",  1, 4),
        ("iron_hills_noble_swordsman",             3, 8),
        ("iron_hills_noble_infantry",              2, 6),
        ("iron_hills_noble_guard",                 2, 5),
        ("iron_hills_noble_shield_guard",          1, 4),
        ("iron_hills_noble_gate_warden",           1, 3),
        ("iron_hills_noble_royal_warden",          1, 2),
        ("iron_hills_noble_hammer_guard",          2, 5),
        ("iron_hills_noble_anvilguard",            1, 4),
        ("iron_hills_noble_ironbreaker",           1, 2),
    ],
}


def expand(dry_run: bool):
    with open(TEMPLATES, "r", encoding="utf-8") as f:
        content = f.read()
    orig_len = len(content)

    for culture, additions in ADDITIONS.items():
        # Find the kingdom_hero_party_<culture>_template block and insert before </stacks>
        template_re = re.compile(
            r'(<MBPartyTemplate id="kingdom_hero_party_' + re.escape(culture) +
            r'_template">.*?<stacks>.*?)(\s+</stacks>)',
            re.DOTALL
        )
        m = template_re.search(content)
        if not m:
            print(f"  WARN: kingdom_hero_party_{culture}_template not found")
            continue

        # Check which additions are already in the template; skip those
        existing_block = m.group(1)
        new_stacks_lines = []
        added_count = 0
        skipped_count = 0
        for troop_id, mn, mx in additions:
            ref = f'troop="NPCCharacter.{troop_id}"'
            if ref in existing_block:
                skipped_count += 1
                continue
            new_stacks_lines.append(
                f'\t\t\t<PartyTemplateStack min_value="{mn}" max_value="{mx}" troop="NPCCharacter.{troop_id}" />'
            )
            added_count += 1

        if not new_stacks_lines:
            print(f"  {culture}: all {len(additions)} additions already present")
            continue

        insertion = "\n" + "\n".join(new_stacks_lines)
        new_block = m.group(1) + insertion + m.group(2)
        # Splice by position (not .replace) — guarantees we mutate only THIS template,
        # never any later template that happens to contain identical text.
        content = content[:m.start()] + new_block + content[m.end():]
        print(f"  {culture}: added {added_count} new stacks (skipped {skipped_count} already present)")

    print(f"\nFile size: {orig_len:,} -> {len(content):,} bytes")

    if dry_run:
        print("(dry-run — no file written)")
        return

    with open(TEMPLATES, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Wrote: {TEMPLATES}")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--dry-run", action="store_true")
    p.add_argument("--apply", action="store_true")
    args = p.parse_args()
    if args.dry_run:
        expand(dry_run=True)
    elif args.apply:
        expand(dry_run=False)
    else:
        p.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
