#!/usr/bin/env python3
"""Generate Main/_Module/ModuleData/troops/troops_lothlorien.xml, Lothlorien's own troop trees.

Until this file Lothlorien fielded Rivendell's tree whole. The trees and the armour roles are
KEYforce's (2026-11-04); tier T<n> is level 6 + (n-1)*5, so T4 is level 21 and T9 level 46.

Regular Lorien (the light kit, sk_ll_rg_*; the culture's basic troop):
    T4 Scout -> T5 Spearman -> T6 Infantry -> T7 Warden
                            -> T6 Rider    -> T7 Lancer
             -> T5 Bowman   -> T6 Archer   -> T7 Longbowman
Marchwardens (the middle kit, sk_ll_mw_*), the border guard of Talas Duiren (castle_L2), held by
the Wardens of the Naith (clan_lothlorien_2). Archer helmets on archers and horse archers, the
heavier infantry helmets on the skirmisher line; the `_lord_` helmets are left to the lords:
    T5 Sentry -> T6 Archer     -> T7 Ranger   -> T8 High Ranger
                               -> T7 Outrider -> T8 Dawnrider     (horse archers)
              -> T6 Skirmisher -> T7 Ambusher -> T8 Waystalker    (sword, shield and javelins)
Galadhrim (the noble kit). T6-T8 wear only Galadhrim pieces (sk_ll_gl_*), the T9s and the lords
only Mallorn pieces (sk_ll_ml_*): a troop never mixes the two kits (KEYforce 2026-11-05). Helmet
variant a (open) on infantry, b (hood) on archers, c (hood and mask) on the blades; Mallorn heavy
helmets on infantry and blades, medium on archers; Mallorn elite chests and helmets are left to the
lords:
    T6 Warrior -> T7 Bladeguard -> T8 Blademaster -> T9 Highblade
               -> T7 Infantry   -> T8 Sentinel    -> T9 Mallorn Defender
               -> T7 Archer     -> T8 Marksman    -> T9 Bowmaster

Weapons are the existing elven kit until KEYforce's Lorien weapons land: Mirkwood swords and
the high-elf sword and spear, vanilla javelins (no elven ones exist), Rivendell silver swords, spears and shields, the elven
shields and arrows. Every bow is the line's ranged-ladder cell (ladder_lothlorien_bow_t<tier>,
tools/ranged_ladders.json), never a raw bow.

Skills are written as 0 here and placed by the curve, which owns them:
    python tools/rebalance_troops.py --fix-monotonicity --restat <the ids this file defines>
    python tools/rebalance_ranged_ladders.py --apply
The generator keeps a troop's existing skill values when the output file already exists, so a
regeneration does not undo that pass.

Usage:
    python tools/generate_lothlorien_troops.py            # dry run: tree, kit and armour totals
    python tools/generate_lothlorien_troops.py --apply
"""
import argparse
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Main" / "_Module" / "ModuleData" / "troops" / "troops_lothlorien.xml"
ARMOUR_DIR_HINT = "LOTRLOME_items/lothlorien (tools/generate_lothlorien_armor.py)"

TIER_LEVEL = {4: 21, 5: 26, 6: 31, 7: 36, 8: 41, 9: 46}
SKILLS = ("Athletics", "Riding", "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing")
ARMOUR_SLOTS = ("Head", "Cape", "Body", "Gloves", "Leg")

# --- weapons ------------------------------------------------------------------------------------

ARROWS_A = ("wm_elven_arrow_v1_a", "wm_elven_arrow_q1v3")
ARROWS_B = ("wm_elven_arrow_v2_a", "wm_elven_arrow_q2v3")
ARROWS_C = ("wm_elven_arrow_v3_a", "wm_elven_arrow_q3v3")
ARROWS_D = ("wm_elven_arrow_v4_a", "wm_elven_arrow_q4v3")


def bow(tier):
    return f"ladder_lothlorien_bow_t{tier}"


def archer(tier, swords, arrows):
    """Bow in Item0, two quivers in Item1/Item2, sword in Item3, in every set."""
    return [{"Item0": bow(tier), "Item1": arrows[0], "Item2": arrows[1], "Item3": s} for s in swords]


def shield_line(spear, shield, swords):
    return [{"Item0": spear, "Item1": shield, "Item2": s} for s in swords]


def two_hander(main, swords):
    return [{"Item0": main, "Item1": s} for s in swords]


def skirmisher(swords, shield, javelins):
    """Sword in Item0, shield in Item1, one javelin stack per Item2.. slot."""
    out = []
    for s in swords:
        eq = {"Item0": s, "Item1": shield}
        eq.update({f"Item{2 + i}": j for i, j in enumerate(javelins)})
        out.append(eq)
    return out


LIGHT_HORSE = {"Horse": "noble_horse_southern", "HorseHarness": "saddle_of_aeneas"}
GOOD_HORSE = {"Horse": "noble_horse_southern", "HorseHarness": "fortunas_choice"}

# --- troops -------------------------------------------------------------------------------------
# armour: {slot: [item per set]}; a one-item list is worn in every set. Sets = the longest list.


def T(tid, name, tier, group, weapons, armour, ups=(), horse=None, basic=False):
    return {"id": tid, "name": name, "tier": tier, "group": group, "weapons": weapons,
            "armour": armour, "ups": list(ups), "horse": horse, "basic": basic}


def rg(x):
    return f"sk_ll_rg_{x}"


def mw(x):
    return f"sk_ll_mw_{x}"


def gl(x):
    return f"sk_ll_gl_{x}"


def ml(x):
    return f"sk_ll_ml_{x}"


TROOPS = [
    # ---------------- Regular Lorien ----------------
    T("lorien_scout", "Lórien Scout", 4, "Ranged",
      archer(4, ("wm_rivendell_sword_a02_silver", "wm_gf_knight_broadsword"), ARROWS_A),
      {"Head": [rg("helmet_light_a")], "Body": [rg("chest_light_a"), rg("chest_light_b")],
       "Leg": [rg("boots_light_a")]},
      ups=("lorien_spearman", "lorien_bowman"), basic=True),
    T("lorien_spearman", "Lórien Spearman", 5, "Infantry",
      shield_line("wm_rivendell_spear_a01", "wm_elven_shield_a", ("mirkwood_sword_a01", "he_sword")),
      {"Head": [rg("helmet_med_a")], "Cape": [rg("pauld_light_a")], "Body": [rg("chest_med_a")],
       "Leg": [rg("boots_light_a")]},
      ups=("lorien_infantry", "lorien_rider")),
    T("lorien_infantry", "Lórien Infantry", 6, "Infantry",
      shield_line("wm_rivendell_spear_a01", "wm_elven_shield_a", ("mirkwood_sword_a01", "he_sword")),
      {"Head": [rg("helmet_med_a")], "Cape": [rg("pauld_med_a")], "Body": [rg("chest_med_a")],
       "Gloves": [rg("bracer_med_a")], "Leg": [rg("grvs_med_a")]},
      ups=("lorien_warden",)),
    T("lorien_warden", "Lórien Warden", 7, "Infantry",
      shield_line("wm_rivendell_spear_a01_silver", "wm_elven_shield_b", ("wm_rivendell_sword_a01_silver", "he_sword")),
      {"Head": [rg("helmet_med_a")], "Cape": [rg("pauld_med_a")], "Body": [rg("chest_heavy_a")],
       "Gloves": [rg("bracer_med_a")], "Leg": [rg("grvs_med_a")]}),
    T("lorien_rider", "Lórien Rider", 6, "Cavalry",
      shield_line("he_spear", "wm_elven_shield_b", ("mirkwood_sword_a01", "he_sword")),
      {"Head": [rg("helmet_med_b")], "Cape": [rg("pauld_med_a")], "Body": [rg("chest_med_a")],
       "Gloves": [rg("bracer_med_a")], "Leg": [rg("grvs_med_a")]},
      ups=("lorien_lancer",), horse=LIGHT_HORSE),
    T("lorien_lancer", "Lórien Lancer", 7, "Cavalry",
      shield_line("he_spear", "wm_rivendell_shield_a02_silver", ("wm_rivendell_sword_a01_silver", "he_sword")),
      {"Head": [rg("helmet_med_b")], "Cape": [rg("pauld_med_a")], "Body": [rg("chest_heavy_a")],
       "Gloves": [rg("bracer_med_a")], "Leg": [rg("grvs_med_a")]},
      horse=GOOD_HORSE),
    T("lorien_bowman", "Lórien Bowman", 5, "Ranged",
      archer(5, ("wm_rivendell_sword_a02_silver", "wm_rivendell_sword_a01_black"), ARROWS_A),
      {"Head": [rg("helmet_light_a")], "Cape": [rg("pauld_light_a")],
       "Body": [rg("chest_light_b"), rg("chest_light_a")], "Leg": [rg("boots_light_a")]},
      ups=("lorien_archer",)),
    T("lorien_archer", "Lórien Archer", 6, "Ranged",
      archer(6, ("wm_rivendell_sword_a01_black", "wm_rivendell_sword_a02_silver"), ARROWS_B),
      {"Head": [rg("helmet_med_b")], "Cape": [rg("pauld_light_a")], "Body": [rg("chest_med_a")],
       "Gloves": [rg("bracer_med_a")], "Leg": [rg("boots_light_a")]},
      ups=("lorien_longbowman",)),
    T("lorien_longbowman", "Lórien Longbowman", 7, "Ranged",
      archer(7, ("wm_rivendell_sword_a02_black", "wm_rivendell_sword_a01_black"), ARROWS_C),
      {"Head": [rg("helmet_med_b")], "Cape": [rg("pauld_med_a")], "Body": [rg("chest_med_a")],
       "Gloves": [rg("bracer_med_a")], "Leg": [rg("grvs_med_a")]}),

    # ---------------- Marchwardens (Talas Duiren) ----------------
    T("lorien_mw_sentry", "Marchwarden Sentry", 5, "Ranged",
      archer(5, ("he_sword", "mirkwood_sword_a01"), ARROWS_B),
      {"Head": [mw("helmet_arc_med_a"), mw("helmet_arc_med_b")],
       "Body": [mw("chest_med_a"), mw("chest_med_b")],
       "Gloves": [mw("bracer_med_a")], "Leg": [mw("grvs_med_a")]},
      ups=("lorien_mw_archer", "lorien_mw_skirmisher"), basic=True),
    T("lorien_mw_archer", "Marchwarden Archer", 6, "Ranged",
      archer(6, ("he_sword", "mirkwood_sword_a01"), ARROWS_B),
      {"Head": [mw("helmet_arc_med_a"), mw("helmet_arc_med_b")], "Cape": [mw("pauld_med_a")],
       "Body": [mw("chest_med_a"), mw("chest_med_b")],
       "Gloves": [mw("bracer_med_a")], "Leg": [mw("grvs_med_a")]},
      ups=("lorien_mw_ranger", "lorien_mw_outrider")),
    T("lorien_mw_ranger", "Marchwarden Ranger", 7, "Ranged",
      archer(7, ("he_sword", "wm_rivendell_sword_a01_silver"), ARROWS_C),
      {"Head": [mw("helmet_arc_heavy_a"), mw("helmet_arc_heavy_b")], "Cape": [mw("pauld_med_a")],
       "Body": [mw("chest_heavy_a"), mw("chest_heavy_b")],
       "Gloves": [mw("bracer_heavy_a")], "Leg": [mw("grvs_med_a")]},
      ups=("lorien_mw_high_ranger",)),
    T("lorien_mw_high_ranger", "Marchwarden High Ranger", 8, "Ranged",
      archer(8, ("wm_gf_knight_longsword", "wm_rivendell_sword_a01_silver"), ARROWS_D),
      {"Head": [mw("helmet_arc_heavy_a"), mw("helmet_arc_heavy_b")], "Cape": [mw("pauld_heavy_a")],
       "Body": [mw("chest_heavy_a"), mw("chest_heavy_b")],
       "Gloves": [mw("bracer_heavy_a")], "Leg": [mw("grvs_heavy_a")]}),
    T("lorien_mw_outrider", "Marchwarden Outrider", 7, "HorseArcher",
      archer(7, ("he_sword", "mirkwood_sword_a01"), ARROWS_C),
      {"Head": [mw("helmet_arc_heavy_a"), mw("helmet_heavy_a")], "Cape": [mw("pauld_med_a")],
       "Body": [mw("chest_med_a"), mw("chest_med_b")],
       "Gloves": [mw("bracer_med_a")], "Leg": [mw("grvs_med_a")]},
      ups=("lorien_mw_dawnrider",), horse=LIGHT_HORSE),
    T("lorien_mw_dawnrider", "Marchwarden Dawnrider", 8, "HorseArcher",
      archer(8, ("wm_gf_knight_longsword", "wm_rivendell_sword_a01_silver"), ARROWS_D),
      {"Head": [mw("helmet_arc_heavy_b"), mw("helmet_heavy_b")], "Cape": [mw("pauld_heavy_a")],
       "Body": [mw("chest_heavy_a"), mw("chest_heavy_b")],
       "Gloves": [mw("bracer_heavy_a")], "Leg": [mw("grvs_heavy_a")]},
      horse=GOOD_HORSE),
    T("lorien_mw_skirmisher", "Marchwarden Skirmisher", 6, "Infantry",
      skirmisher(("wm_rivendell_sword_a01_black", "wm_rivendell_sword_a02_silver"), "wm_elven_shield_a",
                 ("northern_javelin_2_t3",)),
      {"Head": [mw("helmet_heavy_a"), mw("helmet_heavy_b")], "Cape": [mw("pauld_med_a")],
       "Body": [mw("chest_med_a"), mw("chest_med_b")],
       "Gloves": [mw("bracer_med_a")], "Leg": [mw("grvs_med_a")]},
      ups=("lorien_mw_ambusher",)),
    T("lorien_mw_ambusher", "Marchwarden Ambusher", 7, "Infantry",
      skirmisher(("wm_rivendell_sword_a02_black", "wm_rivendell_sword_a01_black"), "wm_elven_shield_b",
                 ("northern_javelin_3_t4", "northern_javelin_2_t3")),
      {"Head": [mw("helmet_heavy_a"), mw("helmet_heavy_b")], "Cape": [mw("pauld_heavy_a")],
       "Body": [mw("chest_heavy_a"), mw("chest_heavy_b")],
       "Gloves": [mw("bracer_heavy_a")], "Leg": [mw("grvs_med_a")]},
      ups=("lorien_mw_waystalker",)),
    T("lorien_mw_waystalker", "Marchwarden Waystalker", 8, "Infantry",
      skirmisher(("wm_rivendell_sword_a02_black", "wm_rivendell_sword_a01_black"), "wm_rivendell_shield_a02_silver",
                 ("northern_javelin_3_t4", "northern_javelin_3_t4")),
      {"Head": [mw("helmet_elite_a"), mw("helmet_elite_b")], "Cape": [mw("pauld_elite_a")],
       "Body": [mw("chest_heavy_a"), mw("chest_heavy_b")],
       "Gloves": [mw("bracer_heavy_a")], "Leg": [mw("grvs_heavy_a")]}),

    # ---------------- Galadhrim (nobles) ----------------
    T("lorien_gl_warrior", "Galadhrim Warrior", 6, "Infantry",
      shield_line("wm_rivendell_spear_a01_silver", "wm_elven_shield_b", ("wm_rivendell_sword_a02_silver", "he_sword")),
      {"Head": [gl("helmet_med_a")], "Cape": [gl("pauld_heavy_a")],
       "Body": [gl("chest_med_a"), gl("chest_med_b")],
       "Gloves": [gl("bracer_heavy_a")], "Leg": [gl("grvs_med_a")]},
      ups=("lorien_gl_bladeguard", "lorien_gl_infantry", "lorien_gl_archer"), basic=True),
    T("lorien_gl_bladeguard", "Galadhrim Bladeguard", 7, "Infantry",
      two_hander("wm_gf_knight_longsword", ("he_sword", "wm_rivendell_sword_a02_silver")),
      {"Head": [gl("helmet_heavy_c")], "Cape": [gl("pauld_heavy_a")],
       "Body": [gl("chest_heavy_a"), gl("chest_heavy_b")],
       "Gloves": [gl("bracer_heavy_a")], "Leg": [gl("grvs_heavy_a")]},
      ups=("lorien_gl_blademaster",)),
    T("lorien_gl_blademaster", "Galadhrim Blademaster", 8, "Infantry",
      two_hander("wm_gf_knight_longsword", ("he_sword", "wm_rivendell_sword_a02_silver")),
      {"Head": [gl("helmet_heavy_c")], "Cape": [gl("pauld_cape_heavy_a")],
       "Body": [gl("chest_elite_a"), gl("chest_elite_b")],
       "Gloves": [gl("bracer_heavy_a")], "Leg": [gl("grvs_heavy_a")]},
      ups=("lorien_gl_highblade",)),
    T("lorien_gl_highblade", "Galadhrim Highblade", 9, "Infantry",
      two_hander("wm_gf_knight_longsword", ("wm_rivendell_sword_a02_silver", "he_sword")),
      {"Head": [ml("helmet_heavy_c")], "Cape": [ml("pauld_cape_heavy_a")],
       "Body": [ml("chest_heavy_a"), ml("chest_heavy_b")],
       "Gloves": [ml("bracer_heavy_a")], "Leg": [ml("grvs_heavy_a")]}),
    T("lorien_gl_infantry", "Galadhrim Infantry", 7, "Infantry",
      shield_line("wm_rivendell_spear_a01_silver", "wm_elven_shield_b", ("wm_rivendell_sword_a02_silver", "he_sword")),
      {"Head": [gl("helmet_heavy_a")], "Cape": [gl("pauld_heavy_a")],
       "Body": [gl("chest_heavy_a"), gl("chest_heavy_b")],
       "Gloves": [gl("bracer_heavy_a")], "Leg": [gl("grvs_heavy_a")]},
      ups=("lorien_gl_sentinel",)),
    T("lorien_gl_sentinel", "Galadhrim Sentinel", 8, "Infantry",
      shield_line("wm_rivendell_spear_a01_silver", "wm_rivendell_shield_a02_silver", ("wm_rivendell_sword_a02_silver", "he_sword")),
      {"Head": [gl("helmet_heavy_a")], "Cape": [gl("pauld_heavy_a")],
       "Body": [gl("chest_elite_a"), gl("chest_elite_b")],
       "Gloves": [gl("bracer_heavy_a")], "Leg": [gl("grvs_heavy_a")]},
      ups=("lorien_gl_mallorn_defender",)),
    # T9 sidearms: the strongest one-handers the elves own today (melee band 86-143 DPS at tier 9).
    T("lorien_gl_mallorn_defender", "Mallorn Defender", 9, "Infantry",
      shield_line("wm_rivendell_spear_a01_silver", "wm_rivendell_shield_a02_silver", ("wm_rivendell_sword_a02_black", "wm_rivendell_sword_a01_black")),
      {"Head": [ml("helmet_heavy_a")], "Cape": [ml("pauld_heavy_a")],
       "Body": [ml("chest_heavy_a"), ml("chest_heavy_b")],
       "Gloves": [ml("bracer_heavy_a")], "Leg": [ml("grvs_heavy_a")]}),
    T("lorien_gl_archer", "Galadhrim Archer", 7, "Ranged",
      archer(7, ("he_sword", "he_sword"), ARROWS_C),
      {"Head": [gl("helmet_heavy_b")], "Cape": [gl("pauld_med_a")],
       "Body": [gl("chest_heavy_a"), gl("chest_heavy_b")],
       "Gloves": [gl("bracer_med_a")], "Leg": [gl("grvs_med_a")]},
      ups=("lorien_gl_marksman",)),
    T("lorien_gl_marksman", "Galadhrim Marksman", 8, "Ranged",
      archer(8, ("wm_gf_knight_longsword", "he_sword"), ARROWS_D),
      {"Head": [gl("helmet_heavy_b")], "Cape": [gl("pauld_cape_heavy_a")],
       "Body": [gl("chest_elite_a"), gl("chest_elite_b")],
       "Gloves": [gl("bracer_heavy_a")], "Leg": [gl("grvs_heavy_a")]},
      ups=("lorien_gl_bowmaster",)),
    T("lorien_gl_bowmaster", "Galadhrim Bowmaster", 9, "Ranged",
      archer(9, ("wm_gf_knight_longsword", "wm_gf_knight_longsword"), ARROWS_D),
      {"Head": [ml("helmet_med_b")], "Cape": [ml("pauld_cape_heavy_a")],
       "Body": [ml("chest_heavy_a"), ml("chest_heavy_b")],
       "Gloves": [ml("bracer_med_a")], "Leg": [ml("grvs_med_a")]}),
]

# Tavern mercenaries (docs/features/tavern-mercenaries.md): a leaf copy `<source>_merc` with
# occupation Mercenary of the rarest entry of the culture's volunteer pool
# (VolunteerRecruitmentService.Lothlorien.cs), bound in taom_spcultures.xml <basic_mercenary_troops>.
# The copy wears the source's kit and skills.
MERCENARY_SOURCES = ("lorien_mw_sentry",)


# --- rendering ----------------------------------------------------------------------------------


def sets_of(t):
    n = max([len(t["weapons"])] + [len(v) for v in t["armour"].values()])
    out = []
    for i in range(n):
        eq = dict(t["weapons"][i % len(t["weapons"])])
        for slot in ARMOUR_SLOTS:
            items = t["armour"].get(slot)
            if items:
                eq[slot] = items[i % len(items)]
        if t["horse"]:
            eq.update(t["horse"])
        out.append(eq)
    return out


SLOT_ORDER = ("Item0", "Item1", "Item2", "Item3", "Head", "Cape", "Body", "Gloves", "Leg", "Horse", "HorseHarness")


def existing_skills(path):
    """{troop id: {skill: value}} from a previous run, so a regeneration keeps the curve's values."""
    if not path.exists():
        return {}
    text = path.read_text(encoding="utf-8")
    out = {}
    for m in re.finditer(r'<NPCCharacter\s+id="([^"]+)".*?</NPCCharacter>', text, re.S):
        out[m.group(1)] = {s: int(v) for s, v in re.findall(r'<skill id="([^"]+)" value="(\d+)"', m.group(0))}
    return out


def render(troops, skills_by_id):
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        "<!-- Generated by tools/generate_lothlorien_troops.py; edit the generator, not this file. -->",
        "<NPCCharacters>",
    ]
    by_id = {t["id"]: t for t in troops}
    mercs = [dict(by_id[s], id=f"{s}_merc", name=f'Hired {by_id[s]["name"]}', ups=[], basic=False,
                  merc_of=s) for s in MERCENARY_SOURCES]
    for t in list(troops) + mercs:
        basic = '\n      is_basic_troop="true"' if t["basic"] else ""
        src = t.get("merc_of")
        key = f"aom_merc_{src}_name" if src else f'aom_{t["id"]}_name'
        lines += [
            "  <NPCCharacter",
            f'      id="{t["id"]}"',
            '      race="elf"',
            f'      default_group="{t["group"]}"',
            f'      level="{TIER_LEVEL[t["tier"]]}"',
            f'      name="{{={key}}}[Lothlórien] {t["name"]}"',
            f'      occupation="{"Mercenary" if src else "Soldier"}"',
            f'      culture="Culture.lothlorien"{basic}>',
            "    <face>",
            '      <face_key_template value="BodyProperty.fighter_rivendell" />',
            "    </face>",
            "    <skills>",
        ]
        have = skills_by_id.get(src or t["id"], {})
        lines += [f'      <skill id="{s}" value="{have.get(s, 0)}" />' for s in SKILLS]
        lines.append("    </skills>")
        if t["ups"]:
            lines.append("    <upgrade_targets>")
            lines += [f'      <upgrade_target id="NPCCharacter.{u}" />' for u in t["ups"]]
            lines.append("    </upgrade_targets>")
        lines.append("    <Equipments>")
        for eq in sets_of(t):
            lines.append("      <EquipmentRoster>")
            lines += [f'        <equipment slot="{s}" id="Item.{eq[s]}" />' for s in SLOT_ORDER if s in eq]
            lines.append("      </EquipmentRoster>")
        lines += ["    </Equipments>", "  </NPCCharacter>"]
    lines.append("</NPCCharacters>")
    return "\r\n".join(lines) + "\r\n"


# --- checks -------------------------------------------------------------------------------------


def armour_values():
    """Primary armour per item from generate_lothlorien_armor (same curve), for the totals check."""
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import generate_lothlorien_armor as gen
    vals = {}
    for item_id, slot, _ in gen.PIECES:
        st = gen.stats_for(item_id, slot, gen.priced_band(item_id))
        vals[item_id] = sum(v for k, v in st.items() if k.endswith("_armor"))
    return vals


def check(troops):
    ids = {t["id"] for t in troops}
    errors = []
    vals = armour_values()
    total = {}
    for t in troops:
        for u in t["ups"]:
            if u not in ids:
                errors.append(f'{t["id"]}: upgrade target {u} is not defined')
        sets = sets_of(t)
        for slot in ARMOUR_SLOTS:
            filled = [slot in s for s in sets]
            if any(filled) and not all(filled):
                errors.append(f'{t["id"]}: slot {slot} filled in some sets only')
        for s in sets:
            for slot in ARMOUR_SLOTS:
                if slot in s and s[slot] not in vals:
                    errors.append(f'{t["id"]}: {s[slot]} is not an item of the armour generator')
        total[t["id"]] = sum(sum(vals.get(s[slot], 0) for slot in ARMOUR_SLOTS if slot in s) for s in sets) / len(sets)
    for t in troops:
        for u in t["ups"]:
            if u in total and total[u] < total[t["id"]]:
                errors.append(f'{t["id"]} -> {u}: armour total falls {total[t["id"]]:.0f} -> {total[u]:.0f}')
    return errors, total


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--apply", action="store_true", help="write troops_lothlorien.xml (default: dry run)")
    p.add_argument("--output", default=str(OUTPUT))
    args = p.parse_args()

    errors, total = check(TROOPS)
    for t in TROOPS:
        print(f'T{t["tier"]} L{TIER_LEVEL[t["tier"]]:<3} {t["id"]:30} {t["group"]:12} armour {total[t["id"]]:5.0f}'
              f'  -> {", ".join(t["ups"]) or "-"}')
    if errors:
        print("\n".join("ERROR: " + e for e in errors))
        return 1
    out = Path(args.output)
    xml = render(TROOPS, existing_skills(out))
    print(f"{len(TROOPS)} troops")
    if not args.apply:
        print("dry run; pass --apply to write")
        return 0
    out.write_bytes(xml.encode("utf-8"))
    print(f"wrote {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
