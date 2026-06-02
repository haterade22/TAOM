#!/usr/bin/env python3
"""Transforms for the Goblin / Misty Mountain Orc factions (cloned from Gundabad):

  remap_orc_armor(text)    — Gundabad armor (sk_gb_uruk_*) -> ORC armor only
                             (sk_md_orc_*, sk_gn_orc_*). NO sk_uruk_mordor_* — those are
                             Black-Uruk-sized and too big for regular orcs/goblins.
  remap_orc_weapons(text)  — mix 1h sword/axe/mace across Gundabad + Mordor + Dol Guldur
                             weapon sets (role-preserving; spear/shield/javelin/bows stay).
  strip_cavalry(text)      — remove Cavalry/HorseArcher troops + upgrade_target refs to them
                             (orcs are infantry + archer lines only). Returns (text, dropped_ids).
  strip_cavalry_stacks(text, ids) — remove party-template stacks referencing dropped troops.

Every target id below is verified present in Mordor / Dol Guldur data (no broken refs).
"""
import re

# ---- ARMOR: orc-only pool (sk_md_orc_*, sk_gn_orc_*). Gundabad slot+tier -> orc items. ----
# Orc armor maxes at "heavy" (chest/helmet) and "med" (boots/bracer/pauldron); higher Gundabad
# tiers (elite/lord) map down to the strongest orc piece available.
M = {
    ("helmet", "light"): ["sk_gn_orc_helmet_light_a", "sk_gn_orc_helmet_light_b", "sk_md_orc_rdr_helmet_light_a"],
    ("helmet", "med"):   ["sk_gn_orc_inf_helmet_med_a", "sk_md_orc_inf_helmet_med_a", "sk_gn_orc_arc_helmet_med_a", "sk_gn_orc_sct_helmet_med_a", "sk_gn_orc_vgd_helmet_med_a", "sk_md_orc_rdr_helmet_med_a"],
    ("helmet", "heavy"): ["sk_md_orc_inf_helmet_heavy_a", "sk_md_orc_arc_helmet_heavy_a", "sk_md_orc_brt_helmet_heavy_a", "sk_md_orc_vgd_helmet_heavy_a"],
    ("helmet", "elite"): ["sk_md_orc_inf_helmet_heavy_a", "sk_md_orc_vgd_helmet_heavy_a", "sk_md_orc_brt_helmet_heavy_a"],
    ("chest", "light"):  ["sk_md_orc_inf_chainmail_light_a", "sk_md_orc_arc_chest_light_a", "sk_md_orc_arc_chest_light_b", "sk_md_orc_arc_chest_light_c"],
    ("chest", "med"):    ["sk_md_orc_inf_chest_med_a", "sk_md_orc_inf_chest_med_b", "sk_md_orc_inf_chainmail_med_a", "sk_md_orc_arc_chest_med_a"],
    ("chest", "heavy"):  ["sk_md_orc_inf_chest_heavy_a", "sk_md_orc_inf_chest_heavy_e", "sk_md_orc_arc_chest_heavy_a"],
    ("chest", "elite"):  ["sk_md_orc_inf_chest_heavy_a", "sk_md_orc_inf_chest_heavy_e"],
    ("chest", "lord"):   ["sk_md_orc_inf_chest_heavy_a", "sk_md_orc_inf_chest_heavy_e"],
    ("boots", "light"):  ["sk_md_orc_boots_light_a", "sk_md_orc_boots_light_b", "sk_md_orc_boots_a"],
    ("boots", "med"):    ["sk_md_orc_boots_med_a", "sk_md_orc_boots_med_b"],
    ("boots", "heavy"):  ["sk_md_orc_boots_med_a", "sk_md_orc_boots_med_b"],
    ("boots", "elite"):  ["sk_md_orc_boots_med_a", "sk_md_orc_boots_med_b"],
    ("bracer", "med"):   ["sk_md_orc_bracer_med_a", "sk_md_orc_bracer_med_b"],
    ("bracer", "heavy"): ["sk_md_orc_bracer_med_a", "sk_md_orc_bracer_med_b"],
    ("bracer", "elite"): ["sk_md_orc_bracer_med_a", "sk_md_orc_bracer_med_b"],
    ("pauldron", "light"): ["sk_md_orc_pauldron_light_a", "sk_md_orc_pauldron_light_b"],
    ("pauldron", "med"):   ["sk_md_orc_pauldron_med_a", "sk_md_orc_pauldron_med_b"],
    ("pauldron", "heavy"): ["sk_md_orc_pauldron_med_a", "sk_md_orc_pauldron_med_b"],
    ("pauldron", "elite"): ["sk_md_orc_pauldron_med_a", "sk_md_orc_pauldron_med_b"],
    ("pauldron_cape", "elite"): ["sk_md_orc_pauldron_med_a", "sk_md_orc_pauldron_med_b"],
    ("cape", None): ["sk_md_orc_pauldron_light_a", "sk_md_orc_pauldron_light_b"],
}

# ---- WEAPONS: 1h sword/axe/mace mixed across Gundabad + Mordor + Dol Guldur. ----
W = {
    "sword": ["wm_gundabad_sword_a01", "wm_mordor_set1_sword_a01", "wm_dol_goldur_1h_sword_a01",
              "wm_mordor_set1_sword_a02", "wm_dol_goldur_1h_sword_a02", "wm_gundabad_sword_a02"],
    "axe":   ["wm_gundabad_axe_a01", "wm_mordor_set1_axe_a01", "wm_dol_goldur_axe_a04",
              "wm_mordor_set1_axe_a02", "wm_gundabad_axe_a02"],
    "mace":  ["wm_gundabad_mace_a01", "wm_mordor_set1_mace_a01", "wm_dol_goldur_1h_mace_a02", "wm_gundabad_mace_a02"],
}

CAV_GROUPS = ("Cavalry", "HorseArcher")


def _vidx(letter):
    return (ord(letter) - ord('a')) if letter and letter.isalpha() else 0


def remap_orc_armor(text):
    text = re.sub(r'sk_gb_uruk_pauldron_cape_(elite)_([a-z])',
                  lambda m: M[("pauldron_cape", m.group(1))][_vidx(m.group(2)) % len(M[("pauldron_cape", m.group(1))])], text)
    text = re.sub(r'sk_gb_uruk_cape_([a-z])',
                  lambda m: M[("cape", None)][_vidx(m.group(1)) % len(M[("cape", None)])], text)

    def rep(m):
        key = (m.group(1), m.group(2))
        if key not in M:
            return m.group(0)
        lst = M[key]
        return lst[_vidx(m.group(3)) % len(lst)]
    return re.sub(r'sk_gb_uruk_(helmet|chest|boots|bracer|pauldron)_(light|med|heavy|elite|lord)_([a-z])', rep, text)


def remap_orc_weapons(text):
    def rep(m):
        lst = W[m.group(1)]
        return lst[sum(ord(c) for c in m.group(2)) % len(lst)]
    return re.sub(r'wm_gundabad_(sword|axe|mace)_([a-z0-9]+)', rep, text)


def strip_cavalry(text):
    """Remove Cavalry/HorseArcher NPCCharacter blocks + upgrade_target refs. Returns (text, dropped_ids)."""
    dropped = []
    for m in re.finditer(r'<NPCCharacter\b([^>]*)>', text):
        attrs = m.group(1)
        dg = re.search(r'default_group="([^"]+)"', attrs)
        idm = re.search(r'id="([^"]+)"', attrs)
        if dg and idm and dg.group(1) in CAV_GROUPS:
            dropped.append(idm.group(1))
    for cid in dropped:
        text = re.sub(r'[ \t]*<NPCCharacter\b[^>]*\bid="' + re.escape(cid) + r'".*?</NPCCharacter>\s*\n',
                      '', text, flags=re.DOTALL)
        # upgrade_target is multiline: <upgrade_target\n  id="NPCCharacter.X" />
        text = re.sub(r'[ \t]*<upgrade_target\s+id="NPCCharacter\.' + re.escape(cid) + r'"\s*/>\s*\n', '', text)
    return text, dropped


def strip_cavalry_stacks(text, dropped_ids):
    for cid in dropped_ids:
        text = re.sub(r'[ \t]*<PartyTemplateStack[^>]*troop="NPCCharacter\.' + re.escape(cid) + r'"[^>]*/>\s*\n', '', text)
    return text


if __name__ == "__main__":
    import os
    MD = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Main", "_Module", "ModuleData")
    src = "".join(open(os.path.join(MD, p), encoding="utf-8").read() for p in [
        "troops/troops_mordor.xml", "equipmentsets/taom_equipment_sets_mordor.xml",
        "troops/troops_dolguldur.xml", "troops/troops_gundabad.xml"])
    valid = set(re.findall(r'Item\.([a-zA-Z0-9_]+)', src))
    armor_targets = {i for lst in M.values() for i in lst}
    weapon_targets = {i for lst in W.values() for i in lst}
    bad = sorted((armor_targets | weapon_targets) - valid)
    print("armor: orc-only (no sk_uruk_mordor):", not any("sk_uruk_mordor" in i for i in armor_targets))
    print("remap targets NOT found in game data:", bad or "none — all valid")
