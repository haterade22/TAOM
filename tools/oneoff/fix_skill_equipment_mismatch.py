#!/usr/bin/env python3
"""One-off: fix skill-vs-equipment mismatches left by name-keyword weapon
detection in tools/rebalance_troops.py (pre-2026-07-13). Issues #340 / #341.

Three permutation classes, computed by the (now equipment-driven) generator:
  1. CROSSBOW-SWAP (12): troop carries a crossbow and no bow, but Bow > Crossbow
     (names like "Sharpshooter"/"Marksman" never matched the old keyword list).
  2. MELEE-SWAP (59): troop carries a two-hander and no polearm, but
     Polearm > TwoHanded (the polearm-biased baselines applied un-swapped to
     "Knight"/"Berserker"-named two-hander troops).
  3. NAFFATUN-UNSWAP (2): the old 'naffatun' keyword swapped Bow/Crossbow on
     javelin throwers that carry neither.

The script recomputes every troop with the fixed generator but WRITES ONLY the
frozen expected set below — every write is a pure value permutation inside the
existing <skills> block (verified per troop; anything else aborts). Divergence
between the computed set and the frozen set aborts too, so a drifted repo can't
silently widen the blast radius.

--strip-vestigial-arrows additionally removes arrow equipment lines from
crossbow troops that carry Arrows-class ammo with no bow to fire it
(7 expected, all in troops_gondor.xml).

Usage:
    python tools/oneoff/fix_skill_equipment_mismatch.py            # dry-run
    python tools/oneoff/fix_skill_equipment_mismatch.py --apply
    python tools/oneoff/fix_skill_equipment_mismatch.py --apply --strip-vestigial-arrows
"""
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import rebalance_troops as rb  # noqa: E402
import taom_schema as ts       # noqa: E402

# Frozen expected sets (computed 2026-07-13 against the fixed generator; the
# script ABORTS if the recomputed sets differ — the diff-audit contract).
EXPECTED_CROSSBOW_SWAP = {
    'dunland_dragon_firebolt', 'dunland_dragon_sniper',
    'ironpass_sharpshooter', 'iron_hills_noble_scout',
    'iron_hills_noble_sharpshooter', 'iron_hills_noble_veteran_sharpshooter',
    'gondor_tol_marksman', 'gondor_tol_sharpshooter',
    'urukhai_skirmisher', 'isengard_militia_archer',
    'isengard_militia_veteran_archer', 'umbar_elite_root101',
}
EXPECTED_MELEE_SWAP = {
    'dg_goblin_crawler', 'dg_goblin_harrier', 'dg_uruk_fell_fang',
    'dg_uruk_black_slayer', 'dg_khamul_veiled_reaper', 'dg_khamul_shadow_reaper',
    'dunland_bear_chosen', 'dunland_bear_berserker', 'dunland_bear_executioner',
    'dunland_lizard_horseman', 'dunland_lizard_outrider', 'dunland_lizard_noble_cavalry',
    'erebor_reg_mattock_warrior', 'iron_hills_reg_axe_warrior',
    'iron_hills_noble_hammer_guard', 'iron_hills_noble_anvilguard',
    'iron_hills_noble_ironbreaker',
    'gondor_loss_skirmisher', 'gondor_loss_axe_thrower',
    'gondor_lam_clansman', 'gondor_lam_footman', 'gondor_lam_hill_warden',
    'gondor_cal_heavy_swordsman', 'gondor_cal_sergeant', 'gondor_cal_vale_knight',
    'gondor_arn_cavalry', 'gondor_arn_knight', 'gondor_arn_vet_knight',
    'gondor_arn_hill_knight',
    'gundabad_berserker', 'gundabad_veteran_berserker',
    'urukhai_feller', 'urukhai_slayer', 'urukhai_champion', 'urukhai_reaver',
    'urukhai_berserker', 'urukhai_nazg_hai',
    'isengard_orc_berserker', 'isengard_orc_slayer', 'isengard_orc_butcher',
    'isengard_orc_ravager',
    'mordor_uruk_vanguard', 'mordor_uruk_captain',
    'loke_rim_maceman', 'loke_rim_gilded_champion',
    'dragon_wrath_ash_executioner', 'dragon_wrath_obsidian_war_reaver',
    'black_sun_executioner', 'black_sun_scourge',
    'imladris_horse_archer', 'imladris_blademaster', 'rivendell_gondolin_battlemaster',
    'rohan_westfold_2h_axeman', 'rohan_westfold_veteran_2h_axeman',
    'aux_basic', 'umbar_elite', 'umbar_elite_root0', 'umbar_elite_root001',
    'umbar_elite_root000',
}
EXPECTED_NAFFATUN_UNSWAP = {'sagarun_naffatun', 'sagarun_storm_helmed_naffatun'}

EXPECTED_VESTIGIAL_ARROW_TROOPS = 7  # all in troops_gondor.xml

# The pairs a legitimate fix is allowed to permute.
SWAP_PAIRS = (('Bow', 'Crossbow'), ('Polearm', 'TwoHanded'))


def is_pure_pair_permutation(old, new):
    """True iff new differs from old only by swapping Bow<->Crossbow and/or
    Polearm<->TwoHanded (compared over the skills present in old)."""
    changed = {s for s in old if old.get(s, 0) != new.get(s, 0)}
    if not changed:
        return False
    for pair in SWAP_PAIRS:
        a, b = pair
        if a in changed or b in changed:
            if old.get(a, 0) != new.get(b, 0) or old.get(b, 0) != new.get(a, 0):
                return False
            changed -= {a, b}
    return not changed


def classify(old, weapon_classes):
    if ('Crossbow' in weapon_classes and 'Bow' not in weapon_classes
            and old.get('Bow', 0) > old.get('Crossbow', 0)):
        return 'crossbow'
    if ('TwoHanded' in weapon_classes and 'Polearm' not in weapon_classes
            and old.get('Polearm', 0) > old.get('TwoHanded', 0)):
        return 'melee'
    if 'Crossbow' not in weapon_classes and old.get('Crossbow', 0) > old.get('Bow', 0):
        return 'naffatun'
    return None


def strip_vestigial_arrows(item_classes, apply_changes):
    """Remove Arrows-class equipment lines from troops that carry no bow."""
    stripped = []
    gondor_only = True
    for fp in sorted(glob.glob(os.path.join(rb.TROOPS_DIR, 'troops_*.xml'))):
        fn = os.path.basename(fp)
        root = ET.parse(fp).getroot()
        targets = []  # (troop_id, [arrow item ids])
        for npc in root.findall('.//NPCCharacter'):
            tid = npc.get('id', '')
            wc = rb.troop_weapon_classes(npc, item_classes)
            if 'Bow' in wc or 'Arrows' not in wc:
                continue
            arrows = []
            for eq in npc.findall('.//equipment'):
                iid = (eq.get('id') or '').replace('Item.', '', 1)
                if eq.get('slot', '').startswith('Item') and item_classes.get(iid) == 'Arrows':
                    arrows.append(iid)
            if arrows:
                targets.append((tid, arrows))
        if not targets:
            continue
        if fn != 'troops_gondor.xml':
            gondor_only = False
        with open(fp, 'r', encoding='utf-8') as f:
            content = f.read()
        for tid, arrows in targets:
            block_re = re.compile(r'id="' + re.escape(tid) + r'".*?</NPCCharacter>', re.DOTALL)
            m = block_re.search(content)
            if not m:
                print(f'  ERROR: could not locate block for {tid} in {fn}')
                sys.exit(1)
            block = m.group(0)
            new_block = block
            for iid in arrows:
                line_re = re.compile(r'[ \t]*<equipment\s+slot="Item\d"\s+id="Item\.'
                                     + re.escape(iid) + r'"\s*/>\r?\n')
                new_block, n = line_re.subn('', new_block)
                if n != 1:
                    print(f'  ERROR: expected 1 arrow line for {tid}/{iid}, matched {n}')
                    sys.exit(1)
            content = content[:m.start()] + new_block + content[m.end():]
            stripped.append((fn, tid, arrows))
        if apply_changes:
            with open(fp, 'w', encoding='utf-8') as f:
                f.write(content)
    if len(stripped) != EXPECTED_VESTIGIAL_ARROW_TROOPS or not gondor_only:
        print(f'\nABORT: vestigial-arrow set diverged from expectation '
              f'({len(stripped)} troops, gondor_only={gondor_only}, expected '
              f'{EXPECTED_VESTIGIAL_ARROW_TROOPS} in troops_gondor.xml only). Nothing else written.')
        sys.exit(1)
    verb = 'Stripped' if apply_changes else 'Would strip'
    print(f'\n{verb} vestigial arrows from {len(stripped)} troops:')
    for fn, tid, arrows in stripped:
        print(f'  {fn:24s} {tid:42s} {", ".join(arrows)}')


def main():
    apply_changes = '--apply' in sys.argv[1:]
    do_arrows = '--strip-vestigial-arrows' in sys.argv[1:]

    if not os.path.isdir(rb.DEFAULT_GAME_MODULES):
        print(f'ERROR: game install not found: {rb.DEFAULT_GAME_MODULES}')
        sys.exit(1)
    item_classes = ts.build_item_class_registry(rb.MODULEDATA_DIR, rb.DEFAULT_GAME_MODULES)
    print(f'Item-class registry: {len(item_classes):,} weapon-classed items')

    found = {'crossbow': set(), 'melee': set(), 'naffatun': set()}
    per_file = {}   # filepath -> {troop_id: new_skills}
    details = []
    residuals = []

    for fp in sorted(glob.glob(os.path.join(rb.TROOPS_DIR, 'troops_*.xml'))):
        fn = os.path.basename(fp)
        fc = fn.replace('troops_', '').replace('.xml', '')
        root = ET.parse(fp).getroot()
        for npc in root.findall('.//NPCCharacter'):
            tid = npc.get('id', '')
            if tid in rb.SKIP_TROOP_IDS:
                continue
            name = rb.get_display_name(npc.get('name', ''))
            level = int(npc.get('level', '0'))
            group = npc.get('default_group', 'Infantry')
            culture = rb.detect_culture(tid, fc)
            old = {}
            se = npc.find('skills')
            if se is not None:
                for s in se.findall('skill'):
                    old[s.get('id')] = int(s.get('value', '0'))
            if not old:
                continue
            wc = rb.troop_weapon_classes(npc, item_classes)
            new = rb.calculate_skills(culture, level, group, tid, name, wc)
            if new is None or all(old.get(s, 0) == new.get(s, 0) for s in old):
                continue
            cls = classify(old, wc)
            if cls is None:
                residuals.append((fn, tid))
                continue
            if not is_pure_pair_permutation(old, new):
                print(f'ABORT: {tid} ({fn}) classified as {cls} but the fix is not a '
                      f'pure pair permutation — hand-tuning suspected. Nothing written.')
                sys.exit(1)
            found[cls].add(tid)
            per_file.setdefault(fp, {})[tid] = new
            changed = {s: (old.get(s, 0), new[s]) for s in old if old.get(s, 0) != new.get(s, 0)}
            details.append((cls, fn, tid, level, changed))

    ok = (found['crossbow'] == EXPECTED_CROSSBOW_SWAP
          and found['melee'] == EXPECTED_MELEE_SWAP
          and found['naffatun'] == EXPECTED_NAFFATUN_UNSWAP)
    if not ok:
        print('ABORT: computed set diverged from the frozen expected set. Nothing written.')
        for cls, exp in (('crossbow', EXPECTED_CROSSBOW_SWAP),
                         ('melee', EXPECTED_MELEE_SWAP),
                         ('naffatun', EXPECTED_NAFFATUN_UNSWAP)):
            extra, missing = found[cls] - exp, exp - found[cls]
            if extra or missing:
                print(f'  {cls}: unexpected={sorted(extra)} missing={sorted(missing)}')
        sys.exit(1)

    for cls, label in (('crossbow', 'CROSSBOW-SWAP'), ('melee', 'MELEE-SWAP'),
                       ('naffatun', 'NAFFATUN-UNSWAP')):
        rows = [d for d in details if d[0] == cls]
        print(f'\n=== {label}: {len(rows)} ===')
        for _, fn, tid, level, changed in rows:
            print(f'  {fn:24s} {tid:42s} L{level:<3d} {changed}')
    if residuals:
        print(f'\nResiduals NOT written (off-formula, hand-tuned): '
              + ', '.join(tid for _, tid in residuals))

    if apply_changes:
        for fp, troop_map in per_file.items():
            rb.apply_skills_via_regex(fp, troop_map)
        print(f'\nApplied skill fixes to {sum(len(m) for m in per_file.values())} troops '
              f'across {len(per_file)} files.')
    else:
        print(f'\nDRY RUN — no files written. Re-run with --apply to fix '
              f'{sum(len(m) for m in per_file.values())} troops.')

    if do_arrows:
        strip_vestigial_arrows(item_classes, apply_changes)


if __name__ == '__main__':
    main()
