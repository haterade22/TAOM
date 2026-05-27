#!/usr/bin/env python3
"""
One-shot find-and-replace for v1.3 → v1.4.5 broken item ID mappings discovered
by tools/audit_item_refs.py. Targets the top-10 highest-reference broken IDs
(covers ~210 ref sites of the 359 total audit findings).

Each mapping is `Item.<old> → Item.<new>` where the new ID has been verified
to exist in the v1.4.5 item registry (SandBoxCore or LOTRLOME_Armory).

Usage:  python tools/fix_v1_4_5_item_ids.py [--dry-run|--apply]
"""
import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TAOM_MODULEDATA = ROOT / "Main/_Module/ModuleData"

# v1.3 broken ID  →  v1.4.5 equivalent (item ID, no `Item.` prefix)
MAPPINGS = [
    # Vanilla v1.3 items renamed/removed in v1.4.5 — replacements selected by
    # role/culture match in SandBoxCore body_armors.xml / weapons.xml.
    ("thick_padded_leather",  "padded_leather_overcoat"),   # 38 refs — padded body armor
    ("khuzait_civil_coat_a",  "khuzait_civil_coat"),        # 31 refs — drop _a suffix
    ("sturgia_civil_a",       "nordic_padded_cloth"),       # 29 refs — sturgia civilian equiv
    ("highland_jerkin",       "highland_cloth"),            # 29 refs — battanian highland equiv
    ("khuzait_civil_b",       "khuzait_civil_coat_b"),      # 27 refs — add coat_ infix
    ("long_bow",              "noble_long_bow"),            # 14 refs — direct rename
    ("fur_dress",             "fur_skirt"),                 # 11 refs — fur civilian equiv

    # LOTRLOME missing `wm_` prefix typos — items exist with prefix.
    ("rivendell_sword_a01",   "wm_rivendell_sword_a01"),    # 11 refs
    ("rivendell_spear_a01",   "wm_rivendell_spear_a01"),    # 11 refs
    ("rivendell_shield_a01",  "wm_rivendell_shield_a02"),   # 11 refs — shield only ships _a02

    # Vanilla v1.3 second-tier items (lower ref counts but still real breakage)
    ("empire_horseman_tunic", "empire_horseman_armor"),     # 6 refs — drop _tunic suffix
    ("empire_formal_armor",   "empire_plate_vest_armor"),   # 3 refs — closest formal equiv
    ("padded_leather_coat",   "padded_leather_overcoat"),   # 3 refs — _coat → _overcoat

    # === Round 2 (2026-05-27) — 138 → ? after this pass ===

    # LOTRLOME head→helmet rename (dol guldur uruk).
    ("sk_dg_uruk_head_elite_a", "sk_dg_uruk_helmet_elite_a"),  # 2 refs
    ("sk_dg_uruk_head_elite_b", "sk_dg_uruk_helmet_elite_b"),  # 4 refs
    ("sk_dg_uruk_head_heavy_a", "sk_dg_uruk_helmet_heavy_a"),  # 2 refs
    ("sk_dg_uruk_head_heavy_b", "sk_dg_uruk_helmet_heavy_b"),  # 4 refs

    # LOTRLOME tier-suffix insert (sk_uruk_mordor).
    ("sk_uruk_mordor_helmet_a1", "sk_uruk_mordor_helmet_medium_a1"),  # 4 refs
    ("sk_uruk_mordor_helmet_a2", "sk_uruk_mordor_helmet_medium_a2"),  # 2 refs
    ("sk_uruk_mordor_bracer_a1", "sk_uruk_mordor_bracer_medium_a"),   # 6 refs — drops the trailing 1

    # LOTRLOME prefix add (wm_) — verified items exist with prefix.
    # Note: wm_rivendell_glaive_a01 and wm_rivendell_sword_b01 do NOT exist;
    # map glaive/sword_b → existing rivendell spear/sword_a01.
    ("rivendell_glaive_a01",  "wm_rivendell_spear_a01"),   # 6 refs — glaive→spear (closest polearm)
    ("rivendell_sword_b01",   "wm_rivendell_sword_a01"),   # 5 refs — only _a01 exists

    # LOTRLOME tier-suffix insert (dunland_caerdh).
    ("dunland_caerdh_shield_a",                "dunland_caerdh_shield_light_a"),     # 5 refs
    ("dunland_caerdh_bracer_elite_a",          "dunland_caerdh_bracer_heavy_a"),     # 2 refs — no elite tier
    ("dunland_caerdh_chainmail_medium_a",      "dunland_caerdh_chainmail_heavy_a"),  # 1 ref — no medium tier
    ("dunland_caerdh_pauldron_heavy_cape_b_cape", "dunland_caerdh_pauldron_heavy_cape_b"),  # 1 ref — drop trailing _cape

    # LOTRLOME items entirely removed → v1.4.5 vanilla/dolguldur fallback.
    ("sk_dg_uruk_1h_sword_a",  "wm_dol_goldur_1h_sword_a01"),  # 6 refs
    ("sk_dg_uruk_2h_sword_a",  "wm_dol_goldur_halberd_a01"),   # 3 refs — no 2h sword; halberd fits
    ("sk_dg_uruk_1h_axe_a",    "wm_dol_goldur_axe_a01"),       # 1 ref
    ("sk_dg_uruk_polearm_a",   "wm_dol_goldur_halberd_a01"),   # 5 refs
    ("sk_dg_uruk_shield_a",    "battered_kite_shield"),        # 5 refs — vanilla fallback
    ("gondor_armor",           "sk_gd_ano_chainmail_half_a"),  # 1 ref — closest gondor body
    ("rhun_1h_sword_a_t1",     "khuzait_sword_1_t2"),          # 29 refs — vanilla khuzait fallback
    ("rhun_round_shield_a",    "battered_kite_shield"),        # 3 refs
    ("rhun_round_shield_b",    "battered_kite_shield"),        # 3 refs
    ("rhun_round_shield_c",    "battered_kite_shield"),        # 8 refs
    ("rhun_polearm_a",         "khuzait_lance_1_t3"),          # 2 refs
    ("rhun_polearm_b",         "khuzait_lance_2_t4"),          # 1 ref
    ("rhun_lance_a",           "khuzait_lance_1_t3"),          # 1 ref
    ("rhun_lance_b",           "khuzait_lance_2_t4"),          # 1 ref
    ("rhun_lance_c",           "khuzait_lance_3_t5"),          # 2 refs

    # Vanilla v1.3 → v1.4.5 (smaller-volume but real breakage).
    ("javelin_1_t3",           "eastern_javelin_2_t3"),        # 4 refs
    ("javelin_2_t4",           "eastern_javelin_3_t4"),        # 1 ref
    ("aserai_lance_1_t3",      "khuzait_lance_1_t3"),          # 3 refs — no aserai lance below t5
    ("aserai_lance_2_t4",      "khuzait_lance_2_t4"),          # 2 refs
    ("empire_spear_1_t3",      "imperial_spear_t2"),           # 2 refs — closest empire spear
    ("fine_glaive_t4",         "eastern_spear_3_t3"),          # 2 refs — no glaive in v1.4.5
    ("throwing_spear",         "eastern_throwing_spear_1_t3"), # 2 refs
    ("khuzait_charger",        "khuzait_horse"),               # 3 refs — drop _charger suffix
    ("khuzait_war_horse",      "khuzait_horse"),               # 1 ref — same
    ("khuzait_leather_boots",  "khuzait_curved_boots"),        # 2 refs
    ("khuzait_civil_boots",    "khuzait_curved_boots"),        # 1 ref
]


def main():
    ap = argparse.ArgumentParser()
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--apply", action="store_true")
    g.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    total_replacements = 0
    files_touched = set()

    for xml in TAOM_MODULEDATA.rglob("*.xml"):
        try:
            text = xml.read_text(encoding="utf-8", errors="ignore")
        except Exception as e:
            print(f"WARN read failed {xml}: {e}", file=sys.stderr)
            continue
        original = text
        file_replacements = 0
        for old_id, new_id in MAPPINGS:
            old_ref = f'"Item.{old_id}"'
            new_ref = f'"Item.{new_id}"'
            count = text.count(old_ref)
            if count:
                text = text.replace(old_ref, new_ref)
                file_replacements += count
        if text != original:
            files_touched.add(xml)
            total_replacements += file_replacements
            if args.apply:
                xml.write_text(text, encoding="utf-8")
                print(f"  {xml.relative_to(ROOT)}: {file_replacements} replacements")
            else:
                print(f"  WOULD {xml.relative_to(ROOT)}: {file_replacements} replacements")

    print()
    print(f"Total: {total_replacements} replacements across {len(files_touched)} files")
    print(f"Mappings applied: {len(MAPPINGS)}")
    if not args.apply:
        print("Run with --apply to write.")


if __name__ == "__main__":
    main()
