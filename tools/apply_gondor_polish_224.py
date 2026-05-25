#!/usr/bin/env python3
"""Gondor troop polish — equipment audit fixes + Pinnath Gelin cavalry branch.

Issue: #224
Follow-up to #212 (KEYforce troop tree revamp). Per-troop equipment surgery
discovered during visual review.

This script is DELTA-STYLE, not full-roster swap (unlike apply_gondor_troop_revamp.py
#99 or apply_mordor_troop_revamp.py #212). Each EQUIPMENT_DELTAS entry is a list
of (op, slot, args...) tuples that mutate only the slots named — every other slot
on the troop is preserved as-is.

Operations:
- ("set", slot, item_id) — set or insert <equipment slot="slot" id="Item.item_id"/>.
  If the slot exists, replaces the id. If not, inserts at end of roster.
- ("clear", slot) — remove the <equipment slot="slot" .../> line entirely.
- ("replace", slot, old_id, new_id) — swap one id for another in the given slot,
  but ONLY if the current value matches old_id (safety net against drift).

Adds 2 new NPCCharacter blocks: gondor_pg_cavalry (L26 T5) + gondor_pg_vet_cavalry (L31 T6).
Patches gondor_pg_spearman upgrade_target to include the new T5 cavalry.

Usage:
    python tools/apply_gondor_polish_224.py --dry-run
    python tools/apply_gondor_polish_224.py --apply
"""
import argparse
import os
import re
import sys
from typing import Dict, List, Tuple, Any

TROOPS_FILE = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Main", "_Module", "ModuleData", "troops", "troops_gondor.xml"
)

# Tier-matched 1h sword mapping for sidearm additions (L+5 = +1 tier).
# Slot used: Item3 (4th weapon slot, intentionally last so primary weapon + shield stay in Item0/Item1).
GONDOR_SWORD_TIER = {
    6:  "wm_gondor_sword_a01",
    11: "wm_gondor_sword_a02",
    16: "wm_gondor_sword_a03",
    21: "wm_gondor_sword_a04",
    26: "wm_gondor_sword_a05",
    31: "wm_gondor_sword_a06",
    36: "wm_gondor_sword_a07",
    41: "wm_gondor_sword_a08",
    46: "wm_gondor_sword_a09",
}

# Vanilla crossbow + bolt tier ladder (no LOTRLOME Gondor variants exist).
CROSSBOW_TIER = {
    16: "crossbow_b", 21: "crossbow_c", 26: "crossbow_d",
    31: "crossbow_e", 36: "crossbow_f", 41: "crossbow_g", 46: "crossbow_g",
}
BOLT_TIER = {
    16: "bolt_b", 21: "bolt_b", 26: "bolt_c",
    31: "bolt_c", 36: "bolt_d", 41: "bolt_d", 46: "bolt_d",
}

BOOTS = "sk_gd_ano_boots_a"
JAVELIN = "imperial_throwing_spear_1_t4"

# Per-troop deltas. Tuple format: (op, slot, args...).
EQUIPMENT_DELTAS: Dict[str, List[Tuple[Any, ...]]] = {
    # === Boots fixes (T1 entry-point troops missing leg armour) ===
    # Note: gondor_leb_militia's boots are wired in the Lebennin block below (combined with sword).
    "gondor_anf_levy":       [("set", "Leg", BOOTS)],
    "gondor_bel_recruit":    [("set", "Leg", BOOTS)],
    "gondor_pg_volunteer":   [("set", "Leg", BOOTS)],

    # === Anfalas Footman — sword replaces eastern_mace ===
    "gondor_anf_footman":    [("set", "Item0", "wm_gondor_sword_a03")],

    # === Anorien militia/infantry chainmail tier fix (post-#224 follow-up 2026-05-25) ===
    # Peasant + Militia + Archer Militia: Half Chainmail A/B (low-tier rabble line).
    # Footman: Half Chainmail B chest + med greaves (transitional tier).
    # Skirmisher: Full Chainmail A (light skirmish-style infantry, distinct silhouette).
    # Bowman / Archer + Anorien Infantry / Vet Infantry / Cavalry / Heavy Cavalry / Knight:
    # standard Anorien chest_med_a (T4 ranged) / chest_med_b (T5+ infantry/cavalry).
    # User direction: standardise the Anorien line on med_b chest across mid-to-elite tier
    # (replaces previous heavy_a/heavy_b assignments on infantry + cavalry + knight).
    "gondor_ano_peasant":        [("set", "Body", "sk_gd_ano_chainmail_half_a")],
    "gondor_ano_militia":        [("set", "Body", "sk_gd_ano_chainmail_half_b")],
    "gondor_ano_footman":        [("set", "Body", "sk_gd_ano_inf_chest_med_b"),
                                  ("set", "Leg",  "sk_gd_ano_grvs_inf_med_a")],
    "gondor_ano_infantry":       [("set", "Body", "sk_gd_ano_inf_chest_med_b")],
    "gondor_ano_vet_infantry":   [("set", "Body", "sk_gd_ano_inf_chest_med_b")],

    # === Anorien archer line — add tier-matched 1h sword as sidearm (Item3) ===
    "gondor_ano_archer_militia": [("set", "Item3", GONDOR_SWORD_TIER[11]),
                                  ("set", "Body", "sk_gd_ano_chainmail_half_b")],
    "gondor_ano_skirmisher":     [("set", "Item3", GONDOR_SWORD_TIER[16]),
                                  ("set", "Body", "sk_gd_ano_chainmail_full_a")],
    "gondor_ano_bowman":         [("set", "Item3", GONDOR_SWORD_TIER[21]),
                                  ("set", "Body", "sk_gd_ano_inf_chest_med_a")],
    "gondor_ano_archer":         [("set", "Item3", GONDOR_SWORD_TIER[26]),
                                  ("set", "Body", "sk_gd_ano_inf_chest_med_b")],

    # === Cair Andros Spearman — add sidearm sword (Item2 — Item0=sword, Item1=shield, leaves room) ===
    # Existing Item0 already wm_gondor_sword_a05. Add a second sword to Item2 as backup if missing.
    "gondor_ca_spearman":        [("set", "Item2", GONDOR_SWORD_TIER[26])],

    # === Ithil Guard line — add 1h sword to Item3 (archer slot pattern) ===
    "gondor_ith_longbowman":     [("set", "Item3", GONDOR_SWORD_TIER[36])],
    "gondor_ith_sharpshooter":   [("set", "Item3", GONDOR_SWORD_TIER[41])],
    "gondor_ith_moon_guard":     [("set", "Item3", GONDOR_SWORD_TIER[46])],

    # === Methir archer line — add 1h sword ===
    "gondor_met_archer":               [("set", "Item3", GONDOR_SWORD_TIER[26])],
    "gondor_met_vet_archer":           [("set", "Item3", GONDOR_SWORD_TIER[31])],
    "gondor_met_composite_archer":     [("set", "Item3", GONDOR_SWORD_TIER[36])],

    # === Lebennin — switch to Lebennin (wm_pelargir_sword) ===
    "gondor_leb_militia":     [("set", "Leg", BOOTS), ("set", "Item0", "wm_pelargir_sword_a01")],  # boots + sword combined
    "gondor_leb_skirmisher":  [("set", "Item0", "wm_pelargir_sword_a01")],
    "gondor_leb_archer":      [("set", "Item3", "wm_pelargir_sword_a02")],  # sidearm slot (primary is bow)
    "gondor_leb_vet_archer":  [("set", "Item3", "wm_pelargir_sword_a02")],
    "gondor_leb_longbowman":  [("set", "Item3", "wm_pelargir_sword_a02")],
    "gondor_leb_infantry":    [("set", "Item0", "wm_pelargir_sword_a02")],
    "gondor_leb_vet_infantry": [("set", "Item0", "wm_pelargir_sword_a02")],
    "gondor_leb_sea_guard":   [("set", "Item0", "wm_pelargir_sword_a02")],

    # === Lamedon — use Lamedon 2h swords, clear shield ===
    "gondor_lam_clansman":     [("set", "Item0", "wm_gondor_lamedon_2h_sword_a"), ("clear", "Item1")],
    "gondor_lam_footman":      [("set", "Item0", "wm_gondor_lamedon_2h_sword_a"), ("clear", "Item1")],
    "gondor_lam_swordman":     [("set", "Item0", "wm_gondor_lamedon_2h_sword_b"), ("clear", "Item1")],
    "gondor_lam_vet_swordman": [("set", "Item0", "wm_gondor_lamedon_2h_sword_c"), ("clear", "Item1")],
    "gondor_lam_hill_warden":  [("set", "Item0", "wm_gondor_lamedon_2h_sword_d"), ("clear", "Item1")],

    # === Anorien cavalry chain — banner spears + canonical Gondor horse armours + med_b chest ===
    "gondor_ano_mt_cavalry":        [("set", "Item0", "wm_gondor_spear_b"),
                                     ("set", "HorseHarness", "gondor_horse_armor_2"),
                                     ("set", "Body", "sk_gd_ano_inf_chest_med_b")],
    "gondor_ano_mt_heavy_cavalry":  [("set", "Item0", "wm_gondor_gondorknight_speara"),
                                     ("set", "HorseHarness", "gondor_horse_armor_1"),
                                     ("set", "Body", "sk_gd_ano_inf_chest_med_b")],
    "gondor_ano_mt_knight":         [("set", "Item0", "wm_gondor_gondorknight_spearb"),
                                     ("set", "HorseHarness", "gondor_horse_armor_1"),
                                     ("set", "Body", "sk_gd_ano_inf_chest_med_b")],

    # === Arndir cavalry — Numenorean 2h swords, drop shield ===
    "gondor_arn_cavalry":      [("set", "Item0", "numenorean_sword_2h_d"), ("clear", "Item1")],
    "gondor_arn_knight":       [("set", "Item0", "numenorean_sword_2h_f"), ("clear", "Item1")],
    "gondor_arn_vet_knight":   [("set", "Item0", "numenorean_sword_2h_i"), ("clear", "Item1")],
    "gondor_arn_hill_knight":  [("set", "Item0", "numenorean_sword_2h_l"), ("clear", "Item1")],

    # === Calembel — drop shield on 2h-sword users ===
    "gondor_cal_heavy_swordsman": [("clear", "Item1")],
    "gondor_cal_sergeant":        [("clear", "Item1")],
    "gondor_cal_vale_knight":     [("clear", "Item1")],

    # === Dol Amroth — horse armour + spear corrections + 1h sword ===
    "gondor_da_squire":     [("set", "HorseHarness", "gondor_horse_armor_3"),
                             ("set", "Item0", "wm_gondor_swanknight_spearb"),
                             ("set", "Item2", GONDOR_SWORD_TIER[26])],
    "gondor_da_cavalry":    [("set", "HorseHarness", "gondor_horse_armor_1_da"),
                             ("set", "Item0", "wm_gondor_swanknight_spearb"),
                             ("set", "Item2", GONDOR_SWORD_TIER[31])],
    "gondor_da_knight":     [("set", "HorseHarness", "gondor_horse_armor_1_da"),
                             ("set", "Item0", "wm_gondor_swanknight_speara"),
                             ("set", "Item2", GONDOR_SWORD_TIER[36])],
    "gondor_da_vet_knight": [("set", "HorseHarness", "gondor_swan_horse_armor_1"),
                             ("set", "Item0", "wm_gondor_swanknight_speara"),
                             ("set", "Item2", "wm_swan_knight_swordb")],
    "gondor_da_swan_knight": [("set", "Item0", "wm_swan_knight_lance_b"),
                              ("set", "Item2", "wm_swan_knight_sworda")],

    # === Pelargir — javelins across the chain ===
    "gondor_pel_skirmisher":  [("set", "Item2", JAVELIN)],
    "gondor_pel_veteran":     [("set", "Item2", JAVELIN)],
    "gondor_pel_infantry":    [("set", "Item2", JAVELIN), ("set", "Item3", JAVELIN)],
    "gondor_pel_vet_infantry": [("set", "Item2", JAVELIN), ("set", "Item3", JAVELIN)],
    "gondor_pel_anchor_guard": [("set", "Item2", JAVELIN), ("set", "Item3", JAVELIN)],

    # === Lond-Galen crossbow troops — crossbow + bolt + sidearm ===
    "gondor_lg_crossbowman":        [("set", "Item0", CROSSBOW_TIER[26]),
                                     ("set", "Item1", BOLT_TIER[26]),
                                     ("set", "Item3", GONDOR_SWORD_TIER[26])],
    "gondor_lg_pavise_crossbowman": [("set", "Item0", CROSSBOW_TIER[31]),
                                     ("set", "Item1", BOLT_TIER[31]),
                                     ("set", "Item3", GONDOR_SWORD_TIER[31])],

    # === Tolfalas arbalest line — crossbow + bolt + sidearm ===
    "gondor_tol_arbalest":         [("set", "Item0", CROSSBOW_TIER[16]),
                                    ("set", "Item1", BOLT_TIER[16]),
                                    ("set", "Item3", GONDOR_SWORD_TIER[16])],
    "gondor_tol_crossbowman":      [("set", "Item0", CROSSBOW_TIER[21]),
                                    ("set", "Item1", BOLT_TIER[21]),
                                    ("set", "Item3", GONDOR_SWORD_TIER[21])],
    "gondor_tol_vet_crossbowman":  [("set", "Item0", CROSSBOW_TIER[26]),
                                    ("set", "Item1", BOLT_TIER[26]),
                                    ("set", "Item3", GONDOR_SWORD_TIER[26])],
    "gondor_tol_marksman":         [("set", "Item0", CROSSBOW_TIER[31]),
                                    ("set", "Item1", BOLT_TIER[31]),
                                    ("set", "Item3", GONDOR_SWORD_TIER[31])],
    "gondor_tol_sharpshooter":     [("set", "Item0", CROSSBOW_TIER[36]),
                                    ("set", "Item1", BOLT_TIER[36]),
                                    ("set", "Item3", GONDOR_SWORD_TIER[36])],

    # === Lossarnach Axe Thrower line — keep throwing axe + 2h axe; remove 1h ===
    # Layout per roster: Item0=throwing axe, Item1=1h axe (DROP), Item2=2h axe (KEEP).
    # apply_clear removes ALL roster occurrences of Item1 per troop (6 rosters each).
    # Original #224 commit wrongly cleared Item2/Item3 and used count=1 — RCA at
    # docs/reviews/rca-gondor-polish-224-deep-review-2026-05-25.md.
    "gondor_loss_axe_thrower":     [("clear", "Item1")],
    "gondor_loss_skirmisher":      [("clear", "Item1")],
    "gondor_loss_vet_axe_thrower": [("clear", "Item1")],
}

# Pinnath Gelin cavalry — 2 new NPCs branching off gondor_pg_spearman upgrade_target.
NEW_TROOPS_XML = """\

  <NPCCharacter
      id="gondor_pg_cavalry"
      default_group="Cavalry"
      level="26"
      name="{=aom_gondor_pg_cavalry_name}[Gondor] Pinnath Gelin Light Horseman"
      occupation="Soldier"
      culture="Culture.gondor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="115" />
      <skill id="Riding" value="135" />
      <skill id="OneHanded" value="135" />
      <skill id="TwoHanded" value="70" />
      <skill id="Polearm" value="160" />
      <skill id="Bow" value="20" />
      <skill id="Crossbow" value="15" />
      <skill id="Throwing" value="65" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.gondor_pg_vet_cavalry" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_gondor_pg_speara" />
        <equipment slot="Item1" id="Item.wm_gondor_sword_a05" />
        <equipment slot="Item2" id="Item.gond_shield_three_green" />
        <equipment slot="Item3" id="Item.imperial_throwing_spear_1_t4" />
        <equipment slot="Head" id="Item.sk_gd_pin_inf_helmet_med_a" />
        <equipment slot="Body" id="Item.sk_gd_pin_inf_chest_med_b" />
        <equipment slot="Cape" id="Item.sk_gd_osg_pauld_cape_inf_elite_a" />
        <equipment slot="Gloves" id="Item.sk_gd_ano_bracer_inf_med_a" />
        <equipment slot="Leg" id="Item.sk_gd_ano_grvs_inf_med_a" />
      </EquipmentRoster>
      <EquipmentSet id="battania_troop_civilian_template_t2" equipmentType="Civilian" />
      <equipment slot="Horse" id="Item.empire_horse" />
      <equipment slot="HorseHarness" id="Item.gondor_horse_armor_4" />
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="gondor_pg_vet_cavalry"
      default_group="Cavalry"
      level="31"
      name="{=aom_gondor_pg_vet_cavalry_name}[Gondor] Pinnath Gelin Veteran Horseman"
      occupation="Soldier"
      culture="Culture.gondor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="135" />
      <skill id="Riding" value="175" />
      <skill id="OneHanded" value="180" />
      <skill id="TwoHanded" value="85" />
      <skill id="Polearm" value="210" />
      <skill id="Bow" value="25" />
      <skill id="Crossbow" value="20" />
      <skill id="Throwing" value="80" />
    </skills>
    <upgrade_targets />
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_gondor_pg_spearb" />
        <equipment slot="Item1" id="Item.wm_gondor_sword_a06" />
        <equipment slot="Item2" id="Item.gond_shield_three_green" />
        <equipment slot="Item3" id="Item.imperial_throwing_spear_1_t4" />
        <equipment slot="Head" id="Item.sk_gd_pin_spear_helmet_heavy_a" />
        <equipment slot="Body" id="Item.sk_gd_pin_inf_chest_heavy_a" />
        <equipment slot="Cape" id="Item.sk_gd_ano_pauld_inf_heavy_a" />
        <equipment slot="Gloves" id="Item.sk_gd_ano_bracer_inf_med_a" />
        <equipment slot="Leg" id="Item.sk_gd_ano_grvs_inf_heavy_a" />
      </EquipmentRoster>
      <EquipmentSet id="battania_troop_civilian_template_t2" equipmentType="Civilian" />
      <equipment slot="Horse" id="Item.t2_empire_horse" />
      <equipment slot="HorseHarness" id="Item.gondor_horse_armor_1_pg" />
    </Equipments>
  </NPCCharacter>
"""

NEW_TROOP_IDS = ["gondor_pg_cavalry", "gondor_pg_vet_cavalry"]


def find_npc_block(content: str, troop_id: str) -> Tuple[int, int]:
    """Return (start, end) of the NPCCharacter block for the given id, or (-1, -1)."""
    pattern = re.compile(
        r'(?m)^([ \t]*)<NPCCharacter\s'
        r'(?:[^>]|\n)*?'
        r'id="' + re.escape(troop_id) + r'"'
        r'(?:[^>]|\n)*?>'
        r'.*?'
        r'^\1</NPCCharacter>',
        re.DOTALL,
    )
    m = pattern.search(content)
    if not m:
        return -1, -1
    return m.start(), m.end()


# Non-roster slot names: these live in <Equipments> directly, not inside <EquipmentRoster>.
# When inserting a slot that doesn't currently exist, these go before </Equipments>.
NON_ROSTER_SLOTS = {"Horse", "HorseHarness"}


def apply_set(block: str, slot: str, item_id: str) -> Tuple[str, bool]:
    """Set or insert the slot line. Returns (new_block, changed).

    Multi-roster note: this only touches the FIRST matching slot in the block.
    For per-roster slot edits, the existing slot will be replaced. If the slot
    is missing entirely, the new line is inserted at an appropriate location.
    """
    # Match existing slot line with any whitespace/closing style.
    slot_re = re.compile(
        r'<equipment\s+slot="' + re.escape(slot) + r'"\s+id="Item\.[^"]+"\s*/>',
        re.IGNORECASE,
    )
    new_line = f'<equipment slot="{slot}" id="Item.{item_id}" />'
    if slot_re.search(block):
        new_block = slot_re.sub(new_line, block, count=1)
        return new_block, new_block != block
    # Slot not present — find insertion target.
    # Find indent from a sibling equipment line.
    sibling = re.search(r'^([ \t]+)<equipment\s+slot="', block, re.MULTILINE)
    indent = sibling.group(1) if sibling else "        "
    insert_line = f'{indent}{new_line}\n'
    if slot in NON_ROSTER_SLOTS:
        # Horse + HorseHarness live in <Equipments> directly, not inside <EquipmentRoster>.
        eq_close = block.find("</Equipments>")
        if eq_close < 0:
            return block, False
        new_block = block[:eq_close] + insert_line + block[eq_close:]
        return new_block, True
    # Weapon/armor slots live inside <EquipmentRoster>. Insert before the first </EquipmentRoster>.
    roster_close = block.find("</EquipmentRoster>")
    if roster_close >= 0:
        new_block = block[:roster_close] + insert_line + block[roster_close:]
    else:
        eq_close = block.find("</Equipments>")
        if eq_close < 0:
            return block, False
        new_block = block[:eq_close] + insert_line + block[eq_close:]
    return new_block, True


def apply_clear(block: str, slot: str) -> Tuple[str, bool]:
    """Remove ALL occurrences of the slot across every roster in the troop block.

    Earlier (#224 initial apply) this used count=1, which only removed the first
    occurrence in multi-roster troops (Lossarnach axe-throwers have 6 rosters).
    Per-troop deltas should apply uniformly across all rosters — if you want to
    drop a slot from one specific roster, that needs a different operation type.
    """
    slot_re = re.compile(
        r'[ \t]*<equipment\s+slot="' + re.escape(slot) + r'"\s+id="Item\.[^"]+"\s*/>\s*\n',
        re.IGNORECASE,
    )
    new_block = slot_re.sub("", block)  # remove ALL matches
    return new_block, new_block != block


def apply_replace(block: str, slot: str, old_id: str, new_id: str) -> Tuple[str, bool]:
    """Swap old_id → new_id only if current value matches old_id."""
    pattern = re.compile(
        r'<equipment\s+slot="' + re.escape(slot) +
        r'"\s+id="Item\.' + re.escape(old_id) + r'"\s*/>',
        re.IGNORECASE,
    )
    if pattern.search(block):
        new_line = f'<equipment slot="{slot}" id="Item.{new_id}" />'
        new_block = pattern.sub(new_line, block, count=1)
        return new_block, new_block != block
    return block, False


def patch_pg_spearman_upgrade(content: str) -> Tuple[str, bool]:
    """Append gondor_pg_cavalry to gondor_pg_spearman's upgrade_targets list."""
    # Find the gondor_pg_spearman block.
    start, end = find_npc_block(content, "gondor_pg_spearman")
    if start < 0:
        return content, False
    block = content[start:end]
    if "NPCCharacter.gondor_pg_cavalry" in block:
        return content, False  # already patched
    # Insert a second <upgrade_target> line inside the existing <upgrade_targets> element.
    pattern = re.compile(
        r'(<upgrade_targets>\s*\n\s*<upgrade_target id="NPCCharacter\.gondor_pg_vet_spearman"\s*/>)',
    )
    m = pattern.search(block)
    if not m:
        return content, False
    new_targets = (
        m.group(1)
        + '\n      <upgrade_target id="NPCCharacter.gondor_pg_cavalry" />'
    )
    new_block = block[:m.start()] + new_targets + block[m.end():]
    return content[:start] + new_block + content[end:], True


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    if not (args.dry_run or args.apply):
        parser.print_help()
        sys.exit(1)

    with open(TROOPS_FILE, "r", encoding="utf-8") as f:
        content = f.read()
    orig_len = len(content)

    ops_total = 0
    ops_applied = 0
    ops_failed = []
    troops_touched = 0
    troops_missing = []

    # 1. Apply equipment deltas per troop
    for troop_id, deltas in EQUIPMENT_DELTAS.items():
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            troops_missing.append(troop_id)
            continue
        block = content[start:end]
        block_changed = False
        for delta in deltas:
            ops_total += 1
            op = delta[0]
            if op == "set":
                _, slot, item_id = delta
                new_block, changed = apply_set(block, slot, item_id)
            elif op == "clear":
                _, slot = delta
                new_block, changed = apply_clear(block, slot)
            elif op == "replace":
                _, slot, old_id, new_id = delta
                new_block, changed = apply_replace(block, slot, old_id, new_id)
            else:
                ops_failed.append(f"{troop_id}: unknown op {op}")
                continue
            if changed:
                ops_applied += 1
                block = new_block
                block_changed = True
            else:
                # No-op (already in desired state, or clear-target didn't exist).
                # Not a failure for idempotency.
                pass
        if block_changed:
            content = content[:start] + block + content[end:]
            troops_touched += 1

    # 2. Add new troops (PG cavalry pair) if not already present
    new_added = 0
    for tid in NEW_TROOP_IDS:
        if f'id="{tid}"' in content:
            continue
        # Only inject the per-troop block, not the whole NEW_TROOPS_XML at once,
        # so re-running after a partial apply is safe.
        m = re.search(
            r'(\s*<NPCCharacter\s+id="' + re.escape(tid) + r'".*?</NPCCharacter>)',
            NEW_TROOPS_XML,
            re.DOTALL,
        )
        if not m:
            ops_failed.append(f"NEW_TROOPS_XML missing block for {tid}")
            continue
        content = content.replace(
            "</NPCCharacters>",
            m.group(1) + "\n</NPCCharacters>",
            1,
        )
        new_added += 1

    # 3. Patch upgrade_target of gondor_pg_spearman
    content, upgrade_patched = patch_pg_spearman_upgrade(content)

    # Summary
    print(f"Gondor polish (issue #224):")
    print(f"  Troops touched:        {troops_touched}/{len(EQUIPMENT_DELTAS)}")
    print(f"  Ops applied:           {ops_applied}/{ops_total}")
    print(f"  New troops added:      {new_added}/{len(NEW_TROOP_IDS)}")
    print(f"  Upgrade-target patch:  {'YES' if upgrade_patched else 'no-op (already patched or pattern missing)'}")
    if troops_missing:
        print(f"  MISSING troops:        {troops_missing}")
    if ops_failed:
        print(f"  Op FAILURES:           {ops_failed}")
    print(f"  File size: {orig_len:,} -> {len(content):,} bytes")

    if args.dry_run:
        print("(dry-run — no file written)")
        return

    with open(TROOPS_FILE, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Wrote: {TROOPS_FILE}")


if __name__ == "__main__":
    main()
