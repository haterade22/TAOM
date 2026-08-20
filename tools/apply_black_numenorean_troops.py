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


# ---------------------------------------------------------------------------
# Armour distribution (KEYforce, 2026-08-18)
# ---------------------------------------------------------------------------
# The Armory carries three chest weights per line (Medium / Heavy / Elite) plus a
# shared light chest, and each other slot walks its own ladder. Read each row as
# T9 down to T6; T5 is the shared pre-split root.
#
#   chest    T9 elite_a + elite_b  | T8 heavy_a   | T7 heavy_b | T6 med_a + med_b
#   helmet   T9 elite (4 hoods)    | T8 heavy (4) | T7 med (2) | T6 light (2)
#   pauldron T9 elite a+b          | T8 heavy a+b | T7 med     | T6 med
#   bracer   T9 elite              | T8 heavy     | T7 med     | T6 med
#   greaves  T9 elite              | T8 heavy     | T7 med     | T6 light
#
# Cavalry and Infantry have NO light helmet and only one med, so `*_helmet_med_a`
# serves T6 and T7, mirroring the T6/T7 repetition the distribution already has
# on pauldrons and bracers. Where a line has no plain `_b` at a tier, the "a + b"
# pair resolves to plain + cape, which is how the spec says cape and non-cape
# variants are worn: together as a mixed pool, not as alternates.
#
# Cavalry has no bracer pool and shares Infantry's. Infantry mixes the arc_ and
# inf_ bracer pools at the same tier. Both per the spec.
K = "sk_md_num_"
I = "sm_md_num_"


def _rosters(line, tier, weapons, mount=None):
    """Armour variants for one line at one tier."""
    grv = {6: I + "grvs_light_a", 7: I + "grvs_med_a",
           8: I + "grvs_heavy_a", 9: I + "grvs_elite_a"}[tier]
    brc = {"arc": {6: [K + "arc_bracer_med_a"], 7: [K + "arc_bracer_med_a"],
                   8: [K + "arc_bracer_heavy_a"], 9: [K + "arc_bracer_elite_a"]},
           "cav": {6: [K + "inf_bracer_med_a"], 7: [K + "inf_bracer_med_a"],
                   8: [K + "inf_bracer_heavy_a"], 9: [K + "inf_bracer_elite_a"]},
           "inf": {6: [K + "inf_bracer_med_a", K + "arc_bracer_med_a"],
                   7: [K + "inf_bracer_med_a", K + "arc_bracer_med_a"],
                   8: [K + "inf_bracer_heavy_a", K + "arc_bracer_heavy_a"],
                   9: [K + "inf_bracer_elite_a", K + "arc_bracer_elite_a"]}}[line][tier]
    if line == "arc":
        heads = {6: [K + "hood_light_a", K + "hood_light_b"],
                 7: [K + "hood_med_a", K + "hood_med_b"],
                 8: [K + "hood_heavy_" + v for v in "abcd"],
                 9: [K + "hood_elite_" + v for v in "abcd"]}[tier]
    else:
        heads = {6: [K + line + "_helmet_med_a"], 7: [K + line + "_helmet_med_a"],
                 8: [K + line + "_helmet_heavy_a", K + line + "_helmet_heavy_b"],
                 9: [K + line + "_helmet_elite_a", K + line + "_helmet_elite_b"]}[tier]
    bodies = {6: [I + line + "_chest_med_a", I + line + "_chest_med_b"],
              7: [I + line + "_chest_heavy_b"],
              8: [I + line + "_chest_heavy_a"],
              9: [I + line + "_chest_elite_a", I + line + "_chest_elite_b"]}[tier]
    capes = {
        "arc": {6: [I + "arc_pauld_med_a"], 7: [I + "arc_pauld_med_a"],
                8: [I + "arc_pauld_heavy_a", I + "arc_pauld_heavy_b"],
                9: [I + "arc_pauld_elite_a", I + "arc_pauld_elite_b"]},
        "cav": {6: [I + "cav_pauld_med_a"], 7: [I + "cav_pauld_med_a"],
                8: [I + "cav_pauld_heavy_a", I + "cav_pauld_cape_heavy_a"],
                9: [I + "cav_pauld_elite_a", I + "cav_pauld_cape_elite_a"]},
        "inf": {6: [I + "inf_pauld_med_a"], 7: [I + "inf_pauld_med_a"],
                8: [I + "inf_pauld_heavy_a", I + "inf_pauld_cape_heavy_a"],
                9: [I + "inf_pauld_elite_a", I + "inf_pauld_elite_b",
                    I + "inf_pauld_cape_elite_a", I + "inf_pauld_cape_elite_b"]}}[line][tier]
    n = max(len(heads), len(bodies), len(capes), len(brc), len(weapons))
    out = []
    for i in range(n):
        r = list(weapons[i % len(weapons)])
        r += [("Head", heads[i % len(heads)]), ("Body", bodies[i % len(bodies)]),
              ("Cape", capes[i % len(capes)]), ("Gloves", brc[i % len(brc)]),
              ("Leg", grv)]
        if mount:
            r += list(mount)
        out.append(r)
    return out


# Weapons per line and tier, unchanged from the shipped set.
_W = {
    ("cav", 6): [[("Item0", I + "sword_1h_a"), ("Item1", I + "cav_shield_med_a"), ("Item2", I + "lance_a")],
                 [("Item0", I + "sword_1h_b"), ("Item1", I + "cav_shield_med_a"), ("Item2", I + "lance_a")]],
    ("cav", 7): [[("Item0", I + "sword_1h_b"), ("Item1", I + "cav_shield_med_a"), ("Item2", I + "lance_a")],
                 [("Item0", I + "sword_1h_c"), ("Item1", I + "cav_shield_med_a"), ("Item2", I + "lance_a")]],
    ("cav", 8): [[("Item0", I + "sword_1h_c"), ("Item1", I + "cav_shield_heavy_a"), ("Item2", I + "lance_a")]],
    ("cav", 9): [[("Item0", I + "sword_1h_c"), ("Item1", I + "cav_shield_heavy_a"), ("Item2", I + "lance_a")]],
    ("inf", 6): [[("Item0", I + "sword_1h_a"), ("Item1", I + "inf_shield_med_a"), ("Item2", I + "sword_2h_a")],
                 [("Item0", I + "sword_1h_b"), ("Item1", I + "inf_shield_med_b"), ("Item2", I + "sword_2h_a")]],
    ("inf", 7): [[("Item0", I + "sword_1h_b"), ("Item1", I + "inf_shield_med_a"), ("Item2", I + "sword_2h_b")],
                 [("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_med_b"), ("Item2", I + "sword_2h_b")]],
    ("inf", 8): [[("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_heavy_a"), ("Item2", I + "sword_2h_c")],
                 [("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_heavy_b"), ("Item2", I + "sword_2h_c")]],
    ("inf", 9): [[("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_heavy_a"), ("Item2", I + "sword_2h_c")],
                 [("Item0", I + "sword_1h_c"), ("Item1", I + "inf_shield_heavy_b"), ("Item2", I + "sword_2h_c")]],
    ("arc", 6): [[("Item0", "sm_dg_khml_longbow_a"), ("Item1", "bodkin_arrows_a"), ("Item2", I + "sword_2h_a")]],
    ("arc", 7): [[("Item0", "sm_dg_khml_longbow_a"), ("Item1", "bodkin_arrows_a"), ("Item2", I + "sword_2h_b")]],
    ("arc", 8): [[("Item0", "sm_rh_loke_longbow_a"), ("Item1", "bodkin_arrows_a"), ("Item2", I + "sword_2h_c")]],
    ("arc", 9): [[("Item0", "sm_rh_loke_longbow_a"), ("Item1", "bodkin_arrows_a"), ("Item2", I + "sword_2h_c")]],
}

# Mount ladder. charger was dropped: it is slower and weaker-charging than
# t2_empire_horse (48/22 against 50/26), so a T8 promotion downgraded the rider.
_MOUNT = {6: [("Horse", "t2_empire_horse"), ("HorseHarness", "lrd_horse_armour_5")],
          7: [("Horse", "noble_horse_imperial"), ("HorseHarness", "lrd_horse_armour_5")],
          8: [("Horse", "noble_horse_imperial"), ("HorseHarness", "lrd_horse_armour_4")],
          9: [("Horse", "noble_horse_imperial"), ("HorseHarness", "mordor_horse_armour_a")]}

_SK = {
    ("cav", 6): dict(Athletics=140, Riding=200, OneHanded=225, TwoHanded=145, Polearm=235, Bow=30, Crossbow=20, Throwing=65),
    ("cav", 7): dict(Athletics=160, Riding=255, OneHanded=270, TwoHanded=165, Polearm=295, Bow=35, Crossbow=25, Throwing=70),
    ("cav", 8): dict(Athletics=180, Riding=310, OneHanded=320, TwoHanded=185, Polearm=350, Bow=40, Crossbow=30, Throwing=75),
    ("cav", 9): dict(Athletics=195, Riding=350, OneHanded=350, TwoHanded=200, Polearm=380, Bow=45, Crossbow=35, Throwing=80),
    ("inf", 6): dict(Athletics=140, Riding=30, OneHanded=235, TwoHanded=225, Polearm=150, Bow=25, Crossbow=20, Throwing=85),
    ("inf", 7): dict(Athletics=155, Riding=30, OneHanded=280, TwoHanded=270, Polearm=190, Bow=30, Crossbow=20, Throwing=95),
    ("inf", 8): dict(Athletics=170, Riding=30, OneHanded=320, TwoHanded=310, Polearm=230, Bow=30, Crossbow=20, Throwing=105),
    ("inf", 9): dict(Athletics=185, Riding=35, OneHanded=345, TwoHanded=335, Polearm=260, Bow=35, Crossbow=25, Throwing=110),
    ("arc", 6): dict(Athletics=135, Riding=30, OneHanded=165, TwoHanded=150, Polearm=110, Bow=200, Crossbow=40, Throwing=70),
    ("arc", 7): dict(Athletics=150, Riding=30, OneHanded=200, TwoHanded=175, Polearm=130, Bow=240, Crossbow=45, Throwing=80),
    ("arc", 8): dict(Athletics=162, Riding=30, OneHanded=235, TwoHanded=200, Polearm=155, Bow=275, Crossbow=50, Throwing=90),
    ("arc", 9): dict(Athletics=175, Riding=35, OneHanded=255, TwoHanded=220, Polearm=175, Bow=295, Crossbow=55, Throwing=95),
}
_NAME = {("cav", 6): ("mordor_num_cavalry", "Cavalry"), ("cav", 7): ("mordor_num_vet_cavalry", "Veteran Cavalry"),
         ("cav", 8): ("mordor_num_knight", "Knight"), ("cav", 9): ("mordor_num_temple_knight", "Temple Knight"),
         ("inf", 6): ("mordor_num_infantry", "Infantry"), ("inf", 7): ("mordor_num_vet_infantry", "Veteran Infantry"),
         ("inf", 8): ("mordor_num_warden", "Warden"), ("inf", 9): ("mordor_num_temple_guard", "Temple Guard"),
         ("arc", 6): ("mordor_num_archer", "Archer"), ("arc", 7): ("mordor_num_vet_archer", "Veteran Archer"),
         ("arc", 8): ("mordor_num_marksman", "Marksman"), ("arc", 9): ("mordor_num_shadowbow", "Temple Shadowbow")}
_GROUP = {"cav": "Cavalry", "inf": "Infantry", "arc": "Ranged"}
_LEVEL = {5: 26, 6: 31, 7: 36, 8: 41, 9: 46}


def _build_troops():
    out = [(
        "mordor_num_initiate", 26, "Infantry", "Initiate",
        ["mordor_num_cavalry", "mordor_num_infantry", "mordor_num_archer"],
        dict(Athletics=120, Riding=35, OneHanded=190, TwoHanded=130,
             Polearm=150, Bow=25, Crossbow=20, Throwing=60),
        # Pre-split root: the shared light chest, light hoods, light greaves.
        # No pauldron and no bracer, per the distribution.
        [[("Item0", I + "sword_1h_a"), ("Item1", I + "inf_shield_med_a"),
          ("Head", K + "hood_light_a"), ("Body", I + "chest_light_a"),
          ("Leg", I + "grvs_light_a")],
         [("Item0", I + "sword_1h_b"), ("Item1", I + "inf_shield_med_b"),
          ("Head", K + "hood_light_b"), ("Body", I + "chest_light_a"),
          ("Leg", I + "grvs_light_a")]],
    )]
    for line in ("cav", "inf", "arc"):
        for tier in (6, 7, 8, 9):
            tid, disp = _NAME[(line, tier)]
            nxt = [_NAME[(line, tier + 1)][0]] if tier < 9 else []
            out.append((tid, _LEVEL[tier], _GROUP[line], disp, nxt, _SK[(line, tier)],
                        _rosters(line, tier, _W[(line, tier)],
                                 _MOUNT[tier] if line == "cav" else None)))
    return out


TROOPS = _build_troops()



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
