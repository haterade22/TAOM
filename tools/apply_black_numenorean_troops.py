#!/usr/bin/env python3
"""Append the Black Numenorean troop tree to troops_mordor.xml.

13 troops, T5 to T9, three branches off a shared Initiate:

    mordor_num_initiate (T5)
      |- mordor_num_cavalry  -> vet_cavalry  -> knight   -> temple_knight
      |- mordor_num_infantry -> vet_infantry -> warden    -> temple_guard
      |- mordor_num_archer   -> vet_archer   -> marksman  -> shadowbow

Design decisions this file encodes, each verified rather than assumed:

* NO `race=` attribute. Black Numenoreans are corrupted Men.
  BasicCharacterObject.Deserialize sets Race = 0 when the attribute is absent,
  and Native loads first so index 0 is `human`. TAOM has zero `race="human"`
  occurrences; every human troop is simply unmarked. The armor meshes rig to
  the standard humanoid skeleton (source FBX carry spine/spine1/spine2/
  l_clavicle/l_thigh/l_calf), which is what makes this safe.

* level = 5T + 1, so T5..T9 is 26/31/36/41/46.
  DefaultCharacterStatsModel.GetTier is ceiling((level - 5) / 5), and
  TaomCharacterStatsModel raises MaxCharacterTier to 10.

* Standalone elite line. taom_spcultures.xml is NOT touched: elite_basic_troop
  stays mordor_uruk_warrior and no troop here is is_basic_troop. They reach the
  field through lord party templates, not notable recruitment.

* Mounts sit INSIDE the EquipmentRoster, never as <Equipments>-level children.
  MBEquipmentRoster.AddOverriddenEquipments stamps roster-level <equipment>
  onto every roster including the civilian one. Vanilla uses the in-roster form
  exclusively and so do 392 of TAOM's 480 horse slots.

* Skills are anchored on the Khamul line in troops_dolguldur.xml, TAOM's
  existing "corrupted Men serving a Nazgul" three-branch human tree at exactly
  T8 and T9, scaled down for T5 to T7.

Usage:
    python tools/apply_black_numenorean_troops.py --dry-run
    python tools/apply_black_numenorean_troops.py --apply
"""
import argparse
import os
import sys
from datetime import date

TROOPS_XML = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                          "Main", "_Module", "ModuleData", "troops", "troops_mordor.xml")

ANCHOR = "    <!-- ===== TAVERN MERCENARIES ====="
MARKER = "KEYforce Black Numenorean line"
BODY_PROPERTY = "BodyProperty.fighter_gondor"

HEADER = """    <!-- ============================================================== -->
    <!--  KEYforce Black Numenorean line (corrupted Men, T5->T9)         -->
    <!--  Spec: lotraom-assets/tools/mordor_armor_and_troops.md          -->
    <!--  NO race attribute: absent == human (index 0). The sk_md_num_ /  -->
    <!--  sm_md_num_ meshes rig to the standard humanoid skeleton.        -->
    <!--  Tree: Initiate -> Cavalry / Infantry / Archer, each 4 deep.     -->
    <!--  Standalone elite line: NOT the culture's elite_basic_troop and  -->
    <!--  not in any volunteer pool. Reached via lord party templates.    -->
    <!-- ============================================================== -->
"""

# id, level, group, display, [upgrade targets], skills, [equipment rosters]
# Each roster is a list of (slot, item id).
SKILLS_ORDER = ["Athletics", "Riding", "OneHanded", "TwoHanded",
                "Polearm", "Bow", "Crossbow", "Throwing"]

I = "sm_md_num_"          # weapon / chest / pauldron / greave prefix
K = "sk_md_num_"          # helmet / hood / bracer prefix


def roster(*pairs):
    return list(pairs)


TROOPS = [
    # ---------------- shared root ----------------
    ("mordor_num_initiate", 26, "Infantry", "Initiate",
     ["mordor_num_cavalry", "mordor_num_infantry", "mordor_num_archer"],
     dict(Athletics=120, Riding=35, OneHanded=190, TwoHanded=130,
          Polearm=150, Bow=25, Crossbow=20, Throwing=60),
     [roster(("Item0", I + "sword_1h_a"), ("Item1", I + "inf_shield_med_a"),
             ("Head", K + "hood_med_a"), ("Body", I + "inf_chest_med_a"),
             ("Gloves", K + "arc_bracer_med_a"), ("Leg", I + "grvs_med_a")),
      roster(("Item0", I + "sword_1h_b"), ("Item1", I + "inf_shield_med_b"),
             ("Head", K + "hood_med_b"), ("Body", I + "inf_chest_med_b"),
             ("Gloves", K + "inf_bracer_med_a"), ("Leg", I + "grvs_med_a")),
      # The Initiate is pre-split, so it can wear any branch's med kit. These two
      # rosters exist so the cav and arc med pieces are worn by somebody: from T6
      # on, every branch sits on the heavy row or above.
      roster(("Item0", I + "sword_1h_a"), ("Item1", I + "inf_shield_med_a"),
             ("Head", K + "cav_helmet_med_a"), ("Body", I + "cav_chest_med_a"),
             ("Gloves", K + "inf_bracer_med_a"), ("Leg", I + "grvs_med_a")),
      roster(("Item0", I + "sword_1h_b"), ("Item1", I + "inf_shield_med_b"),
             ("Head", K + "inf_helmet_med_a"), ("Body", I + "arc_chest_med_a"),
             ("Gloves", K + "arc_bracer_med_a"), ("Leg", I + "grvs_med_a"))]),

    # ---------------- cavalry branch ----------------
    ("mordor_num_cavalry", 31, "Cavalry", "Cavalry",
     ["mordor_num_vet_cavalry"],
     dict(Athletics=140, Riding=200, OneHanded=225, TwoHanded=145,
          Polearm=235, Bow=30, Crossbow=20, Throwing=65),
     [roster(("Item0", I + "sword_1h_a"), ("Item1", I + "cav_shield_med_a"),
             ("Item2", I + "lance_a"),
             ("Head", K + "cav_helmet_heavy_a"), ("Body", I + "cav_chest_heavy_a"),
             ("Cape", I + "cav_pauld_med_a"), ("Gloves", K + "inf_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a"),
             ("Horse", "t2_empire_horse"), ("HorseHarness", "lrd_horse_armour_5")),
      roster(("Item0", I + "sword_1h_b"), ("Item1", I + "cav_shield_med_a"),
             ("Item2", I + "lance_a"),
             ("Head", K + "cav_helmet_heavy_b"), ("Body", I + "cav_chest_heavy_b"),
             ("Cape", I + "cav_pauld_med_a"), ("Gloves", K + "inf_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a"),
             ("Horse", "t2_empire_horse"), ("HorseHarness", "lrd_horse_armour_5"))]),

    ("mordor_num_vet_cavalry", 36, "Cavalry", "Veteran Cavalry",
     ["mordor_num_knight"],
     dict(Athletics=160, Riding=255, OneHanded=270, TwoHanded=165,
          Polearm=295, Bow=35, Crossbow=25, Throwing=70),
     [roster(("Item0", I + "sword_1h_b"), ("Item1", I + "cav_shield_med_a"),
             ("Item2", I + "lance_a"),
             ("Head", K + "cav_helmet_heavy_a"), ("Body", I + "cav_chest_heavy_a"),
             ("Cape", I + "cav_pauld_heavy_a"), ("Gloves", K + "inf_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a"),
             ("Horse", "noble_horse_imperial"), ("HorseHarness", "lrd_horse_armour_5")),
      roster(("Item0", I + "sword_1h_c"), ("Item1", I + "cav_shield_med_a"),
             ("Item2", I + "lance_a"),
             ("Head", K + "cav_helmet_heavy_b"), ("Body", I + "cav_chest_heavy_b"),
             ("Cape", I + "cav_pauld_heavy_a"), ("Gloves", K + "inf_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a"),
             ("Horse", "noble_horse_imperial"), ("HorseHarness", "lrd_horse_armour_5"))]),

    ("mordor_num_knight", 41, "Cavalry", "Knight",
     ["mordor_num_temple_knight"],
     dict(Athletics=180, Riding=310, OneHanded=320, TwoHanded=185,
          Polearm=350, Bow=40, Crossbow=30, Throwing=75),
     [roster(("Item0", I + "sword_1h_c"), ("Item1", I + "cav_shield_heavy_a"),
             ("Item2", I + "lance_a"),
             ("Head", K + "cav_helmet_elite_a"), ("Body", I + "cav_chest_elite_a"),
             ("Cape", I + "cav_pauld_heavy_a"), ("Gloves", K + "inf_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a"),
             ("Horse", "noble_horse_imperial"), ("HorseHarness", "lrd_horse_armour_4")),
      roster(("Item0", I + "sword_1h_c"), ("Item1", I + "cav_shield_heavy_a"),
             ("Item2", I + "lance_a"),
             ("Head", K + "cav_helmet_elite_b"), ("Body", I + "cav_chest_elite_b"),
             ("Cape", I + "cav_pauld_cape_heavy_a"), ("Gloves", K + "inf_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a"),
             ("Horse", "noble_horse_imperial"), ("HorseHarness", "lrd_horse_armour_4"))]),

    ("mordor_num_temple_knight", 46, "Cavalry", "Temple Knight",
     [],
     dict(Athletics=195, Riding=350, OneHanded=350, TwoHanded=200,
          Polearm=380, Bow=45, Crossbow=35, Throwing=80),
     [roster(("Item0", I + "sword_1h_c"), ("Item1", I + "cav_shield_heavy_a"),
             ("Item2", I + "lance_a"),
             ("Head", K + "cav_helmet_elite_a"), ("Body", I + "cav_chest_elite_a"),
             ("Cape", I + "cav_pauld_elite_a"), ("Gloves", K + "inf_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a"),
             ("Horse", "noble_horse_imperial"), ("HorseHarness", "mordor_horse_armour_a")),
      roster(("Item0", I + "sword_1h_c"), ("Item1", I + "cav_shield_heavy_a"),
             ("Item2", I + "lance_a"),
             ("Head", K + "cav_helmet_elite_b"), ("Body", I + "cav_chest_elite_b"),
             ("Cape", I + "cav_pauld_cape_elite_a"), ("Gloves", K + "inf_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a"),
             ("Horse", "noble_horse_imperial"), ("HorseHarness", "mordor_horse_armour_a"))]),

    # ---------------- infantry branch ----------------
    ("mordor_num_infantry", 31, "Infantry", "Infantry",
     ["mordor_num_vet_infantry"],
     dict(Athletics=140, Riding=30, OneHanded=235, TwoHanded=225,
          Polearm=150, Bow=25, Crossbow=20, Throwing=85),
     [roster(("Item0", I + "sword_1h_a"), ("Item1", I + "inf_shield_med_a"),
             ("Item2", I + "sword_2h_a"),
             ("Head", K + "inf_helmet_heavy_a"), ("Body", I + "inf_chest_heavy_a"),
             ("Cape", I + "inf_pauld_med_a"), ("Gloves", K + "inf_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a")),
      roster(("Item0", I + "sword_1h_b"), ("Item1", I + "inf_shield_med_b"),
             ("Item2", I + "sword_2h_a"),
             ("Head", K + "inf_helmet_heavy_b"), ("Body", I + "inf_chest_heavy_b"),
             ("Cape", I + "inf_pauld_med_a"), ("Gloves", K + "arc_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a"))]),

    ("mordor_num_vet_infantry", 36, "Infantry", "Veteran Infantry",
     ["mordor_num_warden"],
     dict(Athletics=155, Riding=30, OneHanded=280, TwoHanded=270,
          Polearm=190, Bow=30, Crossbow=20, Throwing=95),
     [roster(("Item0", I + "sword_1h_b"), ("Item1", I + "inf_shield_med_a"),
             ("Item2", I + "sword_2h_b"),
             ("Head", K + "inf_helmet_heavy_a"), ("Body", I + "inf_chest_heavy_a"),
             ("Cape", I + "inf_pauld_heavy_a"), ("Gloves", K + "inf_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a")),
      roster(("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_med_b"),
             ("Item2", I + "sword_2h_b"),
             ("Head", K + "inf_helmet_heavy_b"), ("Body", I + "inf_chest_heavy_b"),
             ("Cape", I + "inf_pauld_cape_heavy_a"), ("Gloves", K + "arc_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a"))]),

    ("mordor_num_warden", 41, "Infantry", "Warden",
     ["mordor_num_temple_guard"],
     dict(Athletics=170, Riding=30, OneHanded=320, TwoHanded=310,
          Polearm=230, Bow=30, Crossbow=20, Throwing=105),
     [roster(("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_heavy_a"),
             ("Item2", I + "sword_2h_c"),
             ("Head", K + "inf_helmet_elite_a"), ("Body", I + "inf_chest_elite_a"),
             ("Cape", I + "inf_pauld_heavy_a"), ("Gloves", K + "inf_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a")),
      roster(("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_heavy_b"),
             ("Item2", I + "sword_2h_c"),
             ("Head", K + "inf_helmet_elite_b"), ("Body", I + "inf_chest_elite_b"),
             ("Cape", I + "inf_pauld_cape_heavy_a"), ("Gloves", K + "arc_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a"))]),

    ("mordor_num_temple_guard", 46, "Infantry", "Temple Guard",
     [],
     dict(Athletics=185, Riding=35, OneHanded=345, TwoHanded=335,
          Polearm=260, Bow=35, Crossbow=25, Throwing=110),
     [roster(("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_heavy_a"),
             ("Item2", I + "sword_2h_c"),
             ("Head", K + "inf_helmet_elite_a"), ("Body", I + "inf_chest_elite_a"),
             ("Cape", I + "inf_pauld_elite_a"), ("Gloves", K + "inf_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a")),
      roster(("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_heavy_b"),
             ("Item2", I + "sword_2h_c"),
             ("Head", K + "inf_helmet_elite_b"), ("Body", I + "inf_chest_elite_b"),
             ("Cape", I + "inf_pauld_cape_elite_a"), ("Gloves", K + "arc_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a")),
      # Third roster exists to put the spare elite pauldron variants on the
      # field; without it inf_pauld_elite_b and _cape_elite_b ship unused.
      roster(("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_heavy_a"),
             ("Item2", I + "sword_2h_b"),
             ("Head", K + "inf_helmet_elite_a"), ("Body", I + "inf_chest_elite_b"),
             ("Cape", I + "inf_pauld_elite_b"), ("Gloves", K + "inf_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a")),
      roster(("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_heavy_b"),
             ("Item2", I + "sword_2h_c"),
             ("Head", K + "inf_helmet_elite_b"), ("Body", I + "inf_chest_elite_a"),
             ("Cape", I + "inf_pauld_cape_elite_b"), ("Gloves", K + "arc_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a"))]),

    # ---------------- archer branch ----------------
    # No Black Numenorean bow mesh shipped, so the line borrows Rhun bows:
    # Khamul's longbow at T6/T7, the Loke longbow (higher damage) at T8/T9.
    ("mordor_num_archer", 31, "Ranged", "Archer",
     ["mordor_num_vet_archer"],
     dict(Athletics=135, Riding=30, OneHanded=165, TwoHanded=150,
          Polearm=110, Bow=200, Crossbow=40, Throwing=70),
     [roster(("Item0", "sm_dg_khml_longbow_a"), ("Item1", "bodkin_arrows_a"),
             ("Item2", I + "sword_2h_a"),
             ("Head", K + "hood_heavy_a"), ("Body", I + "arc_chest_heavy_a"),
             ("Cape", I + "arc_pauld_med_a"), ("Gloves", K + "arc_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a")),
      roster(("Item0", "sm_dg_khml_longbow_a"), ("Item1", "bodkin_arrows_a"),
             ("Item2", I + "sword_2h_a"),
             ("Head", K + "hood_heavy_b"), ("Body", I + "arc_chest_heavy_b"),
             ("Cape", I + "arc_pauld_med_a"), ("Gloves", K + "arc_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a"))]),

    ("mordor_num_vet_archer", 36, "Ranged", "Veteran Archer",
     ["mordor_num_marksman"],
     dict(Athletics=150, Riding=30, OneHanded=200, TwoHanded=175,
          Polearm=130, Bow=240, Crossbow=45, Throwing=80),
     [roster(("Item0", "sm_dg_khml_longbow_a"), ("Item1", "bodkin_arrows_a"),
             ("Item2", I + "sword_2h_b"),
             ("Head", K + "hood_heavy_a"), ("Body", I + "arc_chest_heavy_a"),
             ("Cape", I + "arc_pauld_heavy_a"), ("Gloves", K + "arc_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a")),
      roster(("Item0", "sm_dg_khml_longbow_a"), ("Item1", "bodkin_arrows_a"),
             ("Item2", I + "sword_2h_b"),
             ("Head", K + "hood_heavy_b"), ("Body", I + "arc_chest_heavy_b"),
             ("Cape", I + "arc_pauld_heavy_b"), ("Gloves", K + "arc_bracer_heavy_a"),
             ("Leg", I + "grvs_heavy_a"))]),

    ("mordor_num_marksman", 41, "Ranged", "Marksman",
     ["mordor_num_shadowbow"],
     dict(Athletics=162, Riding=30, OneHanded=235, TwoHanded=200,
          Polearm=155, Bow=275, Crossbow=50, Throwing=90),
     [roster(("Item0", "sm_rh_loke_longbow_a"), ("Item1", "bodkin_arrows_a"),
             ("Item2", I + "sword_2h_c"),
             ("Head", K + "hood_heavy_c"), ("Body", I + "arc_chest_elite_a"),
             ("Cape", I + "arc_pauld_heavy_a"), ("Gloves", K + "arc_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a")),
      roster(("Item0", "sm_rh_loke_longbow_a"), ("Item1", "bodkin_arrows_a"),
             ("Item2", I + "sword_2h_c"),
             ("Head", K + "hood_heavy_d"), ("Body", I + "arc_chest_elite_b"),
             ("Cape", I + "arc_pauld_heavy_b"), ("Gloves", K + "arc_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a"))]),

    ("mordor_num_shadowbow", 46, "Ranged", "Temple Shadowbow",
     [],
     dict(Athletics=175, Riding=35, OneHanded=255, TwoHanded=220,
          Polearm=175, Bow=295, Crossbow=55, Throwing=95),
     [roster(("Item0", "sm_rh_loke_longbow_a"), ("Item1", "bodkin_arrows_a"),
             ("Item2", I + "sword_2h_c"),
             ("Head", K + "hood_elite_a"), ("Body", I + "arc_chest_elite_a"),
             ("Cape", I + "arc_pauld_elite_a"), ("Gloves", K + "arc_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a")),
      roster(("Item0", "sm_rh_loke_longbow_a"), ("Item1", "bodkin_arrows_a"),
             ("Item2", I + "sword_2h_c"),
             ("Head", K + "hood_elite_c"), ("Body", I + "arc_chest_elite_b"),
             ("Cape", I + "arc_pauld_elite_b"), ("Gloves", K + "arc_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a")),
      # Four elite hoods ship; two rosters would leave hood_elite_b and _d unused.
      roster(("Item0", "sm_rh_loke_longbow_a"), ("Item1", "bodkin_arrows_a"),
             ("Item2", I + "sword_2h_b"),
             ("Head", K + "hood_elite_b"), ("Body", I + "arc_chest_elite_a"),
             ("Cape", I + "arc_pauld_elite_a"), ("Gloves", K + "arc_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a")),
      roster(("Item0", "sm_rh_loke_longbow_a"), ("Item1", "bodkin_arrows_a"),
             ("Item2", I + "sword_2h_c"),
             ("Head", K + "hood_elite_d"), ("Body", I + "arc_chest_elite_b"),
             ("Cape", I + "arc_pauld_elite_b"), ("Gloves", K + "arc_bracer_elite_a"),
             ("Leg", I + "grvs_elite_a"))]),
]


def troop_xml(tid, level, group, display, upgrades, skills, rosters):
    sk = "\n".join(
        f'      <skill id="{n}" value="{skills[n]}" />'
        for n in SKILLS_ORDER if n in skills)
    if upgrades:
        up = ("    <upgrade_targets>\n"
              + "\n".join(f'      <upgrade_target id="NPCCharacter.{u}" />' for u in upgrades)
              + "\n    </upgrade_targets>")
    else:
        up = "    <upgrade_targets />"
    eq = "\n".join(
        "      <EquipmentRoster>\n"
        + "\n".join(f'        <equipment slot="{s}" id="Item.{i}" />' for s, i in r)
        + "\n      </EquipmentRoster>"
        for r in rosters)
    return (
        f'  <NPCCharacter\n'
        f'      id="{tid}"\n'
        f'      default_group="{group}"\n'
        f'      level="{level}"\n'
        f'      name="{{=aom_{tid}_name}}[Mordor] Black Numenorean {display}"\n'
        f'      occupation="Soldier"\n'
        f'      culture="Culture.mordor">\n'
        f'    <face>\n'
        f'      <face_key_template value="{BODY_PROPERTY}" />\n'
        f'    </face>\n'
        f'    <skills>\n{sk}\n    </skills>\n'
        f'{up}\n'
        f'    <Equipments>\n{eq}\n    </Equipments>\n'
        f'  </NPCCharacter>'
    )


def main():
    ap = argparse.ArgumentParser(description="Black Numenorean troop tree")
    ap.add_argument("--dry-run", action="store_true", help="Print the block only (default)")
    ap.add_argument("--apply", action="store_true", help="Insert into troops_mordor.xml")
    ap.add_argument("--path", default=TROOPS_XML)
    args = ap.parse_args()

    blocks = [troop_xml(*t) for t in TROOPS]

    with open(args.path, "r", encoding="utf-8", newline="") as f:
        original = f.read()
    # Majority, not presence: one stray CRLF must not flip an LF file.
    _crlf = original.count("\r\n")
    eol = "\r\n" if _crlf > original.count("\n") - _crlf else "\n"

    if MARKER in original:
        print(f"Already applied ({MARKER} present). Nothing to do.")
        return
    present = [t[0] for t in TROOPS if f'id="{t[0]}"' in original]
    if present:
        print(f"ERROR: these ids already exist: {present}", file=sys.stderr)
        sys.exit(1)
    if ANCHOR not in original:
        print(f"ERROR: anchor not found: {ANCHOR!r}", file=sys.stderr)
        sys.exit(1)

    body = (HEADER + "\n" + "\n\n".join(blocks) + "\n\n")
    body = body.replace("\r\n", "\n").replace("\n", eol)

    if not args.apply:
        print(body[:3000])
        print(f"... [{len(blocks)} troops, {len(body)} chars]")
        print("\n*** dry run, nothing written ***")
        return

    updated = original.replace(ANCHOR, body + ANCHOR, 1)
    stamp = date.today().strftime("%Y%m%d")
    bak = f"{args.path}.bak-blacknum-{stamp}"
    # Never overwrite an existing same-day backup: a partial re-run would replace
    # the pristine copy with already-modified content. Matches the sibling scripts
    # and tools/README.md.
    if not os.path.exists(bak):
        with open(bak, "w", encoding="utf-8", newline="") as f:
            f.write(original)
    with open(args.path, "w", encoding="utf-8", newline="") as f:
        f.write(updated)
    print(f"Inserted {len(blocks)} troops into {args.path}")
    print(f"Backup: {bak}")


if __name__ == "__main__":
    main()
