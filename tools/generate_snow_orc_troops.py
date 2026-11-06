#!/usr/bin/env python3
"""Generate Main/_Module/ModuleData/troops/troops_gundabad_snow.xml, Gundabad's Snow Orc tree.

The tree, kit pools and weapon guide are KEYforce's (lotraom-assets tools/gundabad_armors_and_troops.md,
"Snow Orc Armors and Troops"); tier T<n> is level 6 + (n-1)*5, so T1 is level 6 and T5 level 26.

    T1 Recruit -> T2 Savage   -> T3 Warrior    -> T4 Marauder   -> T5 Blackshield Infantry
                              -> T3 Spearman   -> T4 Reaver     -> T5 Blackshield Spearman
               -> T2 Huntsman -> T3 Skirmisher -> T4 Archer
                                               -> T4 Wolf Rider -> T5 Blackshield Wolf Rider

Kit: the snow orc chests and cape pauldrons (sk_gb_orc_*, tools/generate_snow_orc_armor.py) plus
Mordor's Morannon pool (sk_md_mor_*) for helmets, plain pauldrons, bracers and greaves. Infantry
and spearmen wear the inf chests, archers and wolf riders the arc chests, the recruit the tunic.
Cape pauldrons on infantry and wolf riders, plain Morannon pauldrons on archers and spearmen;
Morannon inf bracers on infantry and spearmen, arc bracers on archers and wolf riders. Pieces
follow rebalance_armor.MESH_TIER_LADDER (light at T1-T3, medium from T3, heavy only at T5) and
the Morannon elite helmets are left to the lords.

Weapons are Gundabad's own (swords, axes, maces, spears, shields, javelins), the wolf riders ride
the Fell Warg, and every bow is the Gundabad ranged-ladder cell (ladder_gundabad_bow_t<tier>).

Skills are written as 0 here and placed by the curve, which owns them:
    python tools/rebalance_troops.py --fix-monotonicity --restat <the ids this file defines>
The generator keeps a troop's existing skill values when the output file already exists, so a
regeneration does not undo that pass.

Usage:
    python tools/generate_snow_orc_troops.py            # dry run: tree, kit and armour totals
    python tools/generate_snow_orc_troops.py --apply
"""
import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import DEFAULT_GAME_ROOT, game_modules  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Main" / "_Module" / "ModuleData" / "troops" / "troops_gundabad_snow.xml"

TIER_LEVEL = {1: 6, 2: 11, 3: 16, 4: 21, 5: 26}
SKILLS = ("Athletics", "Riding", "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing")
ARMOUR_SLOTS = ("Head", "Cape", "Body", "Gloves", "Leg")

# --- weapons ------------------------------------------------------------------------------------

ARROWS = "steppe_arrows"


def bow(tier):
    return f"ladder_gundabad_bow_t{tier}"


def archer(tier, sidearms):
    """Bow in Item0, two quivers in Item1/Item2, sidearm in Item3, in every set."""
    return [{"Item0": bow(tier), "Item1": ARROWS, "Item2": ARROWS, "Item3": s} for s in sidearms]


def shield_line(mains, shield, extra=()):
    """Main weapon in Item0, shield in Item1, extras (javelins, sidearm) from Item2 on."""
    out = []
    for m in mains:
        eq = {"Item0": m, "Item1": shield}
        eq.update({f"Item{2 + i}": x for i, x in enumerate(extra)})
        out.append(eq)
    return out


WARG = {"Horse": "fell_warg", "HorseHarness": "warg_saddle"}

# --- troops -------------------------------------------------------------------------------------
# armour: {slot: [item per set]}; a one-item list is worn in every set. Sets = the longest list.


def T(tid, name, tier, group, weapons, armour, ups=(), horse=None, basic=False):
    return {"id": tid, "name": name, "tier": tier, "group": group, "weapons": weapons,
            "armour": armour, "ups": list(ups), "horse": horse, "basic": basic}


def gb(x):
    return f"sk_gb_orc_{x}"


def mor(x):
    return f"sk_md_mor_{x}"


TROOPS = [
    T("gundabad_snow_recruit", "Snow Orc Recruit", 1, "Infantry",
      [{"Item0": "wm_gundabad_axe_a01"}, {"Item0": "wm_gundabad_mace_a01"}],
      {"Head": [mor("inf_helmet_light_a"), mor("pik_helmet_light_b")],
       "Body": [gb("arc_tunic_a"), gb("arc_tunic_b")], "Leg": [mor("grvs_light_a")]},
      ups=("gundabad_snow_savage", "gundabad_snow_huntsman"), basic=True),

    # ---------------- Infantry ----------------
    T("gundabad_snow_savage", "Snow Orc Savage", 2, "Infantry",
      shield_line(("wm_gundabad_sword_a01", "wm_gundabad_axe_a02"), "wm_gundabad_shield_a01"),
      {"Head": [mor("inf_helmet_light_b"), mor("pik_helmet_light_a")], "Cape": [gb("inf_pauld_cape_light_a")],
       "Body": [gb("inf_chest_light_a"), gb("inf_chest_light_b")],
       "Gloves": [mor("inf_bracer_light_a")], "Leg": [mor("grvs_light_b")]},
      ups=("gundabad_snow_warrior", "gundabad_snow_spearman")),
    T("gundabad_snow_warrior", "Snow Orc Warrior", 3, "Infantry",
      shield_line(("wm_gundabad_sword_a04", "wm_gundabad_mace_a02"), "wm_gundabad_shield_a02"),
      {"Head": [mor("inf_helmet_med_a"), mor("pik_helmet_med_b")], "Cape": [gb("inf_pauld_cape_light_b")],
       "Body": [gb("inf_chest_med_a"), gb("inf_chest_med_b")],
       "Gloves": [mor("inf_bracer_med_a")], "Leg": [mor("grvs_med_a")]},
      ups=("gundabad_snow_marauder",)),
    T("gundabad_snow_marauder", "Snow Orc Marauder", 4, "Infantry",
      shield_line(("wm_gundabad_sword_a02", "wm_gundabad_sword_a04"), "wm_gundabad_shield_a02",
                  ("wm_gundabad_javelin_a01",)),
      {"Head": [mor("inf_helmet_med_b"), mor("pik_helmet_med_c")], "Cape": [gb("inf_pauld_cape_med_a")],
       "Body": [gb("inf_chest_med_b"), gb("inf_chest_med_a")],
       "Gloves": [mor("inf_bracer_med_b")], "Leg": [mor("grvs_med_b")]},
      ups=("gundabad_blackshield_infantry",)),
    T("gundabad_blackshield_infantry", "Blackshield Infantry", 5, "Infantry",
      shield_line(("wm_gundabad_sword_a03", "wm_gundabad_sword_a02"), "wm_gundabad_shield_a03",
                  ("wm_gundabad_javelin_a02",)),
      {"Head": [mor("inf_helmet_heavy_a"), mor("pik_helmet_heavy_b")], "Cape": [gb("inf_pauld_cape_heavy_a")],
       "Body": [gb("inf_chest_heavy_a"), gb("inf_chest_heavy_b")],
       "Gloves": [mor("inf_bracer_heavy_a")], "Leg": [mor("grvs_heavy_a")]}),

    # ---------------- Spearmen ----------------
    T("gundabad_snow_spearman", "Snow Orc Spearman", 3, "Infantry",
      shield_line(("wm_gundabad_spear_a01",), "wm_gundabad_shield_a01"),
      {"Head": [mor("pik_helmet_med_c"), mor("inf_helmet_med_b")], "Cape": [mor("pik_pauld_med_b")],
       "Body": [gb("inf_chest_med_a"), gb("inf_chest_med_b")],
       "Gloves": [mor("inf_bracer_med_a")], "Leg": [mor("grvs_med_a")]},
      ups=("gundabad_snow_reaver",)),
    T("gundabad_snow_reaver", "Snow Orc Reaver", 4, "Infantry",
      shield_line(("wm_gundabad_spear_a02",), "wm_gundabad_shield_a02"),
      {"Head": [mor("pik_helmet_med_b"), mor("inf_helmet_med_a")], "Cape": [mor("pik_pauld_med_a")],
       "Body": [gb("inf_chest_med_b"), gb("inf_chest_med_a")],
       "Gloves": [mor("inf_bracer_med_b")], "Leg": [mor("grvs_med_c")]},
      ups=("gundabad_blackshield_spearman",)),
    T("gundabad_blackshield_spearman", "Blackshield Spearman", 5, "Infantry",
      shield_line(("wm_gundabad_spear_a03",), "wm_gundabad_shield_a03"),
      {"Head": [mor("pik_helmet_heavy_a"), mor("pik_helmet_heavy_c")], "Cape": [mor("pik_pauld_heavy_a")],
       "Body": [gb("inf_chest_heavy_b"), gb("inf_chest_heavy_a")],
       "Gloves": [mor("inf_bracer_heavy_b")], "Leg": [mor("grvs_heavy_b")]}),

    # ---------------- Archers ----------------
    T("gundabad_snow_huntsman", "Snow Orc Huntsman", 2, "Ranged",
      archer(2, ("wm_gundabad_axe_a01", "wm_gundabad_mace_a01")),
      {"Head": [mor("inf_helmet_light_c"), mor("pik_helmet_light_c")], "Cape": [mor("inf_pauld_light_a")],
       "Body": [gb("arc_chest_light_a"), gb("arc_chest_light_b")],
       "Gloves": [mor("arc_bracer_light_a")], "Leg": [mor("grvs_light_c")]},
      ups=("gundabad_snow_skirmisher",)),
    T("gundabad_snow_skirmisher", "Snow Orc Skirmisher", 3, "Ranged",
      archer(3, ("wm_gundabad_sword_a01", "wm_gundabad_axe_a02")),
      {"Head": [mor("pik_helmet_light_a"), mor("inf_helmet_light_b")], "Cape": [mor("pik_pauld_light_b")],
       "Body": [gb("arc_chest_light_b"), gb("arc_chest_light_a")],
       "Gloves": [mor("arc_bracer_light_b")], "Leg": [mor("grvs_light_a")]},
      ups=("gundabad_snow_archer", "gundabad_snow_wolf_rider")),
    T("gundabad_snow_archer", "Snow Orc Archer", 4, "Ranged",
      archer(4, ("wm_gundabad_sword_a04", "wm_gundabad_sword_a02")),
      {"Head": [mor("inf_helmet_med_a"), mor("pik_helmet_med_b")], "Cape": [mor("inf_pauld_med_a")],
       "Body": [gb("arc_chest_med_a"), gb("arc_chest_med_b")],
       "Gloves": [mor("arc_bracer_med_a")], "Leg": [mor("grvs_med_a")]}),

    # ---------------- Wolf riders ----------------
    T("gundabad_snow_wolf_rider", "Snow Orc Wolf Rider", 4, "Cavalry",
      shield_line(("wm_gundabad_sword_a02", "wm_gundabad_sword_a04"), "wm_gundabad_shield_a02"),
      {"Head": [mor("inf_helmet_med_b"), mor("pik_helmet_med_c")], "Cape": [gb("inf_pauld_cape_med_b")],
       "Body": [gb("arc_chest_med_b"), gb("arc_chest_med_a")],
       "Gloves": [mor("arc_bracer_med_b")], "Leg": [mor("grvs_med_b")]},
      ups=("gundabad_blackshield_wolf_rider",), horse=WARG),
    T("gundabad_blackshield_wolf_rider", "Blackshield Wolf Rider", 5, "Cavalry",
      shield_line(("wm_gundabad_spear_a03",), "wm_gundabad_shield_a03",
                  ("wm_gundabad_sword_a03", "wm_gundabad_javelin_a02")),
      {"Head": [mor("inf_helmet_heavy_b"), mor("pik_helmet_heavy_a")], "Cape": [gb("inf_pauld_cape_heavy_b")],
       "Body": [gb("arc_chest_heavy_a"), gb("arc_chest_heavy_b")],
       "Gloves": [mor("arc_bracer_heavy_a")], "Leg": [mor("grvs_heavy_c")]},
      horse=WARG),
]


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
    """{troop id: {skill: value}} from a previous run, so a regeneration keeps the curve's values.

    Read with an XML parser: attribute order and a BOM do not matter, and a troop whose id is
    new or renamed is simply absent (main() warns, because its skills are then written as 0).
    """
    if not path.exists():
        return {}
    root = ET.fromstring(path.read_bytes())
    return {n.get("id"): {s.get("id"): int(s.get("value")) for s in n.iter("skill")}
            for n in root.iter("NPCCharacter")}


def render(troops, skills_by_id):
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        "<!-- Generated by tools/generate_snow_orc_troops.py; edit the generator, not this file. -->",
        "<NPCCharacters>",
    ]
    for t in troops:
        basic = '\r\n      is_basic_troop="true"' if t["basic"] else ""
        lines += [
            "  <NPCCharacter",
            f'      id="{t["id"]}"',
            '      race="orc"',
            f'      default_group="{t["group"]}"',
            f'      level="{TIER_LEVEL[t["tier"]]}"',
            f'      name="{{=aom_{t["id"]}_name}}[Gundabad] {t["name"]}"',
            '      occupation="Soldier"',
            f'      culture="Culture.gundabad"{basic}>',
            "    <face>",
            '      <face_key_template value="BodyProperty.fighter_orc_mordor" />',
            "    </face>",
            "    <skills>",
        ]
        have = skills_by_id.get(t["id"], {})
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
    """Total armour per item: the snow orc pieces from their generator (the file may not be written
    yet), every other piece (the Morannon pool) from the live Armory."""
    vals = {}
    items_dir = game_modules(DEFAULT_GAME_ROOT) / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items"
    for f in items_dir.rglob("*.xml"):
        try:
            root = ET.parse(f).getroot()
        except ET.ParseError:
            continue
        for it in root.iter("Item"):
            a = it.find(".//Armor")
            if a is not None:
                vals[it.get("id")] = sum(int(v) for k, v in a.attrib.items() if k.endswith("_armor"))
    import generate_snow_orc_armor as gen
    for item_id, slot, _ in gen.PIECES:
        st = gen.stats_for(item_id, slot)
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
                    errors.append(f'{t["id"]}: {s[slot]} is not an armour item')
        total[t["id"]] = sum(sum(vals.get(s[slot], 0) for slot in ARMOUR_SLOTS if slot in s) for s in sets) / len(sets)
    for t in troops:
        for u in t["ups"]:
            if u in total and total[u] < total[t["id"]]:
                errors.append(f'{t["id"]} -> {u}: armour total falls {total[t["id"]]:.0f} -> {total[u]:.0f}')
    return errors, total


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--apply", action="store_true", help="write troops_gundabad_snow.xml (default: dry run)")
    p.add_argument("--output", default=str(OUTPUT))
    args = p.parse_args()

    errors, total = check(TROOPS)
    for t in TROOPS:
        print(f'T{t["tier"]} L{TIER_LEVEL[t["tier"]]:<3} {t["id"]:32} {t["group"]:9} armour {total[t["id"]]:5.0f}'
              f'  -> {", ".join(t["ups"]) or "-"}')
    if errors:
        print("\n".join("ERROR: " + e for e in errors))
        return 1
    out = Path(args.output)
    skills = existing_skills(out)
    xml = render(TROOPS, skills)
    print(f"{len(TROOPS)} troops")
    unplaced = [t["id"] for t in TROOPS if t["id"] not in skills]
    if unplaced:
        print("WARNING: no placed skills yet for " + ", ".join(unplaced) + ". They are written as 0 until: "
              "python tools/rebalance_troops.py --fix-monotonicity --restat " + ",".join(unplaced))
    try:
        ET.fromstring(xml.encode("utf-8"))
    except ET.ParseError as exc:
        print(f"ERROR: the rendered XML does not parse ({exc}); nothing written")
        return 1
    if not args.apply:
        print("dry run; pass --apply to write")
        return 0
    out.write_bytes(xml.encode("utf-8"))
    print(f"wrote {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
