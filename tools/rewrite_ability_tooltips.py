#!/usr/bin/env python3
"""
Rewrite career-ability tooltips to include actual archetype-specific numbers
matching taom_ability_tuning.xml. Preserves the lore flavor lead-in for each
ability; replaces the generic "for a short duration" suffix with specifics.

Targets:
  Main/_Module/ModuleData/career_system/taom_ability_templates.xml   (engine-side fallback)
  Main/_Module/ModuleData/taom_career_strings.xml                    (localization registry)

Archetype tuning (from taom_ability_tuning.xml):
  Infantry  — +15% melee damage and +10% damage reduction to allies within 50m
  Ranged    — +20% ranged damage, +20% draw speed, +15% movement speed (self)
  Cavalry   — +25% charge damage, +20% mount speed, +10% melee damage (self+mount)

Duration is 8s for every template except olog_hai_warchief which is 10s.

Usage:  python tools/rewrite_ability_tooltips.py [--apply|--dry-run]
"""
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TEMPLATES = ROOT / "Main/_Module/ModuleData/career_system/taom_ability_templates.xml"
STRINGS   = ROOT / "Main/_Module/ModuleData/taom_career_strings.xml"

INFANTRY = "+15% melee damage and +10% damage reduction to allies within 50m"
RANGED   = "+20% ranged damage, +20% draw speed, and +15% movement speed"
CAVALRY  = "+25% charge damage, +20% mount speed, and +10% melee damage"

# (career_id, archetype, lore_lead_in)
# Lore lead-in preserves the existing flavor; numeric clause + duration appended below.
ABILITIES = [
    # Gondor
    ("ranger_of_ithilien",      "ranged",   "Spring a deadly ambush"),
    ("captain_of_osgiliath",    "infantry", "Rally your soldiers with iron discipline"),
    ("knight_of_belfalas",      "cavalry",  "Thunder forward with an unstoppable cavalry charge"),
    # Mordor
    ("black_uruk_captain",      "infantry", "Enter a blood-fuelled rage, inspiring nearby Black Uruks"),
    ("mulkerhili_cultist",      "ranged",   "Channel the One Ring's whispered dread, empowering your cursed arrows"),
    ("snaga_rider",             "cavalry",  "Dart through the shadows of the enemy flanks"),
    ("olog_hai_warchief",       "infantry", "Bring your war-hammer crashing down with titanic force"),
    # Rohan
    ("marksman_of_aldburg",     "ranged",   "Nock a flight of swift light-fletch arrows"),
    ("eotheod_windrider",       "cavalry",  "Raise the ancient warcry of Eorl the Young"),
    ("watchman_of_stangard",    "infantry", "Dig in with iron resolve"),
    # Dunland
    ("avanc_luth_raider",       "infantry", "Howl a Dunlending war-cry into the fray"),
    ("wolfskin_hunter",         "ranged",   "Mark a target through the mists"),
    ("clanguard_rider",         "cavalry",  "Lead your warpack in a coordinated charge"),
    # Khand
    ("blademaster_of_ren",      "infantry", "Execute a precise twin-blade sequence"),
    ("steppe_bowmaster",        "ranged",   "Loose a sustained storm of arrows from the saddle-edge"),
    ("chariot_warlord",         "cavalry",  "Drive scythe-bladed chariots through the enemy line"),
    # Harad
    ("tribesman_of_jelut",      "infantry", "Coat your weapons in desert toxins"),
    ("pezarsani_javelineer",    "ranged",   "Unleash a blazing javelin barrage"),
    ("mahud_beast_rider",       "cavalry",  "Channel the ancient fear of Mûmakan"),
    ("far_harad_halftroll",     "infantry", "Enter a state of savage brutality"),
    # Rhûn
    ("codyan_legionaire",       "infantry", "Lock shields in legionary formation"),
    ("lokhas_drus_marksman",    "ranged",   "Loose specially-hardened Rhûnish arrows"),
    ("balchoth_kan",            "cavalry",  "Invoke the iron discipline of the Balchoth"),
    # Dale
    ("dale_guardsman",          "infantry", "Plant your feet and hold the gate"),
    ("dale_marksman",           "ranged",   "Channel the legacy of Bard the Bowman"),
    ("dale_outrider",           "cavalry",  "Spur your horse along familiar roads"),
    # Erebor
    ("ironguard",               "infantry", "Invoke the strength of dwarven craft"),
    ("crossbow_master",         "ranged",   "Load masterwork dwarven bolts"),
    ("ram_rider",               "cavalry",  "Drive your war-ram forward"),
    # Rivendell
    ("blade_dancer",            "infantry", "Enter a graceful Noldor combat trance"),
    ("elven_archer",            "ranged",   "Focus ancient elven perception"),
    ("rivendell_knight",        "cavalry",  "Ride with the valor of the Last Alliance"),
    # Lothlórien
    ("warden",                  "infantry", "Invoke the waters of Nimrodel"),
    ("galadhrim_archer",        "ranged",   "Loose a volley of silver-tipped arrows"),
    ("sentinel",                "cavalry",  "Channel the grace of the Golden Wood"),
    # Mirkwood
    ("shadow_walker",           "infantry", "Blend into the forest shadows"),
    ("silvan_archer",           "ranged",   "Fire from an elevated woodland vantage"),
    ("elk_rider",               "cavalry",  "Lower the great antlers and charge"),
    # Isengard
    ("uruk_berserker",          "infantry", "Enter an Uruk-hai berserk frenzy"),
    ("uruk_crossbow",           "ranged",   "Unleash a barrage of Orthanc-forged bolts"),
    ("warg_scout",              "cavalry",  "Unleash a terrifying warg-howl"),
    # Gundabad
    ("cave_troll_master",       "infantry", "Whip your trolls into a frenzy"),
    ("goblin_sniper",           "ranged",   "Coat your bolts in cave fungus toxin"),
    ("warg_pack_leader",        "cavalry",  "Assert dominance with a thundering pack-howl"),
    # Dol Guldur
    ("shadow_warrior",          "infantry", "Draw on the shadows of Dol Guldur"),
    ("necromancer_acolyte",     "ranged",   "Channel necromantic energy through your shots"),
    ("fell_rider",              "cavalry",  "Charge wreathed in shadow"),
    # Umbar
    ("corsair_boarder",         "infantry", "Launch a brutal boarding assault"),
    ("corsair_crossbow",        "ranged",   "Fire from a stable corsair stance"),
    ("corsair_captain",         "cavalry",  "Rally your crew with promises of plunder"),
]

# Default duration; one override for olog_hai_warchief (10s in the XML).
DURATIONS = {"olog_hai_warchief": 10}

def build_tooltip(career_id: str, archetype: str, lore: str) -> str:
    arch_text = {"infantry": INFANTRY, "ranged": RANGED, "cavalry": CAVALRY}[archetype]
    duration = DURATIONS.get(career_id, 8)
    return f"{lore} — boosts {arch_text} for {duration}s."

def main():
    ap = argparse.ArgumentParser()
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--apply", action="store_true")
    g.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    # Sanity: we should have exactly 50 abilities.
    assert len(ABILITIES) == 50, f"expected 50 abilities, got {len(ABILITIES)}"
    # And no duplicates.
    assert len({a[0] for a in ABILITIES}) == 50, "duplicate career_id in ABILITIES list"

    changes = 0
    for path in (TEMPLATES, STRINGS):
        text = path.read_text(encoding="utf-8")
        original = text
        for career_id, archetype, lore in ABILITIES:
            key = f"taom_ability_{career_id}_tt"
            new_tooltip = build_tooltip(career_id, archetype, lore)
            # Match {=key}any text up to the next quote
            pattern = re.compile(r"(\{=" + re.escape(key) + r"\})[^\"]*")
            new_text, n = pattern.subn(lambda m: m.group(1) + new_tooltip, text)
            if n == 0:
                print(f"  WARN  no match for {key} in {path.name}", file=sys.stderr)
            text = new_text
        # Count total replacements as diff in length-changing regions
        if text != original:
            changes += 1
            if args.apply:
                path.write_text(text, encoding="utf-8")
                print(f"WROTE {path.name}")
            else:
                print(f"WOULD WRITE {path.name}")
                # Show one example diff
                for career_id, archetype, lore in ABILITIES[:1]:
                    key = f"taom_ability_{career_id}_tt"
                    new_tooltip = build_tooltip(career_id, archetype, lore)
                    print(f"  e.g. {key} -> {new_tooltip!r}")
        else:
            print(f"NO CHANGE {path.name}")

    if not args.apply and changes:
        print("\nRun with --apply to write.")

if __name__ == "__main__":
    main()
