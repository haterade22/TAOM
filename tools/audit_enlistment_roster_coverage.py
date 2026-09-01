#!/usr/bin/env python3
"""Audit the enlistment service-kit rosters (#375 Phase 4 gate, rewritten for #525).

Checks Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml, whose ids are
`enlist_{runtimeCultureId}_{assignment}_{rank}` plus 16 `enlist_default_{assignment}_{rank}`
fallbacks.

WHAT THIS CHECKS, AND WHAT IT DELIBERATELY DOES NOT
---------------------------------------------------
It checks three things, all of which can be decided from the file plus the live item data:

  1. STRUCTURE. Slot allowlist (weapons Item0..Item3, armour Head/Body/Leg/Gloves/Cape), the
     culture attribute agreeing with the id, no empty roster, no duplicate id.
  2. CONTENT, per assignment, derived from the ITEM CLASS of what is actually in the roster
     rather than from the generator's donor heuristic. An `_archer_` roster with no ranged
     weapon is the failure this catches, and `default_group` cannot catch it: shipped data has
     carried a troop tagged HorseArcher with a sword, a halberd and no bow
     (lessons/data-content-cultures.md).
  3. The 16 mandatory defaults exist.

It does NOT check that every (culture, assignment, rank) cell has a roster of its own, because
that is not the property that matters and asserting it here would be wrong twice over. A cell is
absent by design whenever its donor pool has nothing within one band of the rank, and what the
player actually needs is that EnlistmentRosterResolver reaches SOMETHING for every cell. That is
a property of the resolver, so it is pinned where the real resolver can be called:
TAOM.Tests/Features/Enlistment/EnlistmentRosterCultureCoverageTests. Re-implementing the fallback chain
in Python would be a mirror of production free to drift from it, which is the defect
lessons/testing-qa.md describes as "a comment is a claim".

NO MOUNTS. Horse and HorseHarness fail here for every assignment, cavalry included, and so does
Item4, which the installed engine calls ExtraWeaponSlot (a banner is one eligible occupant, not
the slot's name); GetBattleSetItemIds reads slots 0..11, so anything there would be issued. If a mount is ever wanted, the decision belongs with taom_schema.WAR_RAM_MOUNT_IDS and
the MOUNTED_DWARF rule in .claude/rules/moduledata-validation.md, not with a quiet edit here:
the roster is keyed on the COMMANDER's culture, so it cannot know the player's race.

EXIT CODES
----------
    0  clean
    1  a finding
    2  bad input / no game install (weapon classes cannot be resolved without it)

Usage:
    python tools/audit_enlistment_roster_coverage.py
"""
from __future__ import annotations

import argparse
import os
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import derive_armor_tiers as dat  # noqa: E402  (armour index carries each item's source folder)
import taom_schema as ts  # noqa: E402
from _gamedir import ENV_VAR, game_modules  # noqa: E402
# IMPORTED, not restated. lothlorien and battania own no troops_*.xml but bind to another
# culture's tree, so they are legitimate roster cultures; a second copy of that list here would
# be a mirror free to drift from the generator that acts on it.
from generate_enlistment_rosters import TREE_ALIASES  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
MODULEDATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
TROOPS_DIR = MODULEDATA / "troops"
DEFAULT_XML = MODULEDATA / "equipmentsets" / "taom_enlistment_equipment.xml"
DEFAULT_GAME_ROOT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

RANKS = ["recruit", "soldier", "veteran", "sergeant"]
ASSIGNMENTS = ["infantry", "archer", "cavalry", "support"]
ARMOR_SLOTS = {"Head", "Body", "Leg", "Gloves", "Cape"}
WEAPON_SLOTS = {"Item0", "Item1", "Item2", "Item3"}
ALLOWED_SLOTS = ARMOR_SLOTS | WEAPON_SLOTS
DEFAULT_CULTURE = "neutral_culture"

RANGED_CLASSES = {"Bow", "Crossbow", "Throwing"}
MELEE_CLASSES = {"OneHanded", "TwoHanded", "Polearm"}
AMMO_FOR = {"Bow": "Arrows", "Crossbow": "Bolts"}


def tree_cultures() -> list[str]:
    """Runtime culture StringIds owning at least one line troop in troops_*.xml.

    Iterates EVERY NPCCharacter, not just the first. The previous version read
    `root.find(".//NPCCharacter")`, the FIRST character in each file, and so reported 16 cultures
    against the 20 the data actually carries: eight troop files hold more than one culture and
    troops_goblin.xml holds three (bluecraig, goblin, mistymountainorcs). Four cultures' rosters
    were therefore audited by nothing at all.

    "Line troop" means non-hero and occupation Soldier. Six further culture tokens appear in
    these files carrying nothing but occupation="Bandit" troops -- dunland_raiders,
    erebor_warriors, gondor_soldiers, gundabad_raiders, harad_raiders, mirkwood_stalkers,
    rhun_raiders, umbar_corsairs. They are bandit factions, nobody enlists under them, and
    counting them would inflate the denominator by 96 cells that must never exist.
    """
    cultures = set()
    for path in sorted(TROOPS_DIR.glob("troops_*.xml")):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as e:
            print(f"WARN: parse error in {path.name}: {e}", file=sys.stderr)
            continue
        for npc in root.iter("NPCCharacter"):
            raw = npc.get("culture", "")
            if not raw.startswith("Culture."):
                continue
            if npc.get("is_hero") == "true" or npc.get("occupation", "Soldier") != "Soldier":
                continue
            cultures.add(raw.split(".", 1)[1])
    return sorted(cultures)


def split_id(roster_id: str):
    """`enlist_{culture}_{assignment}_{rank}` -> (culture, assignment, rank), or None.

    Splits from the RIGHT, because a culture token may itself contain an underscore while the
    assignment and rank tokens never do.
    """
    if not roster_id.startswith("enlist_"):
        return None
    body = roster_id[len("enlist_"):]
    rank = next((r for r in RANKS if body.endswith("_" + r)), None)
    if rank is None:
        return None
    body = body[: -(len(rank) + 1)]
    assignment = next((a for a in ASSIGNMENTS if body.endswith("_" + a)), None)
    if assignment is None:
        return None
    culture = body[: -(len(assignment) + 1)]
    return (culture, assignment, rank) if culture else None


def check_content(roster_id, assignment, item_ids, classes, failures):
    """Content rules, decided from the item classes actually present."""
    present = [classes[i] for i in item_ids if i in classes]

    # THE LOWER BOUND, and the reason this gate exists at all. #525 was "the kit has no weapons",
    # and the first fix for it shipped 15 rosters that still had none: support_kit() returned an
    # empty weapon map when the donor carried no OneHanded item, and the cell was emitted anyway.
    # Every gate passed, because every gate asked what the kit must NOT contain and none asked what
    # it MUST. A present-but-weaponless roster is worse than an absent one: the resolver probes
    # existence, so it ends the walk and shadows the armed kit the player would have fallen back to.
    if not any(c in MELEE_CLASSES | RANGED_CLASSES for c in present):
        failures.append(f"{roster_id}: carries NO weapon (classes present: "
                        f"{sorted(set(present)) or 'none'}). This is the #525 defect itself. A cell "
                        "with no usable weapon must be ABSENT so the resolver falls back to an "
                        "armed kit, never emitted armour-only.")

    if assignment == "archer":
        if not any(c in RANGED_CLASSES for c in present):
            failures.append(f"{roster_id}: an archer kit with no ranged weapon "
                            f"(classes present: {sorted(set(present)) or 'none'})")
        for launcher, ammo in AMMO_FOR.items():
            if launcher in present and ammo not in present:
                failures.append(f"{roster_id}: carries a {launcher} but no {ammo} -- "
                                "an unusable weapon is the defect this file exists to fix")
        for ammo in AMMO_FOR.values():
            if ammo in present and not any(l in present for l, a in AMMO_FOR.items() if a == ammo):
                failures.append(f"{roster_id}: carries loose {ammo} with no launcher")

    if assignment == "cavalry" and not any(c in MELEE_CLASSES for c in present):
        failures.append(f"{roster_id}: a cavalry kit with no melee weapon "
                        f"(classes present: {sorted(set(present)) or 'none'})")

    if assignment == "support":
        weapons = [c for c in present if c in MELEE_CLASSES | RANGED_CLASSES]
        if "Bow" in present or "Crossbow" in present:
            failures.append(f"{roster_id}: support carries a launcher; the baggage train gets a "
                            "melee sidearm, not a missile weapon")
        if len(weapons) > 1:
            failures.append(f"{roster_id}: support carries {len(weapons)} weapons ({weapons}); "
                            "the rear-echelon assignment gets one sidearm, or the choice between "
                            "Support and Infantry means nothing")
        if "Shield" in present:
            failures.append(f"{roster_id}: support carries a shield")


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--xml", type=Path, default=DEFAULT_XML,
                    help=f"roster file (default: {DEFAULT_XML.relative_to(REPO_ROOT)})")
    ap.add_argument("--game-modules", help="the Modules folder to read item classes from")
    args = ap.parse_args(argv)

    if not args.xml.exists():
        print(f"ERROR: roster file not found: {args.xml}", file=sys.stderr)
        return 2
    try:
        root = ET.fromstring(args.xml.read_bytes().decode("utf-8-sig"))
    except ET.ParseError as e:
        print(f"ERROR: {args.xml.name} is not well-formed XML: {e}", file=sys.stderr)
        return 2

    modules = Path(args.game_modules) if args.game_modules else game_modules(DEFAULT_GAME_ROOT)
    if not modules.is_dir():
        # environment-failures.md: a wrong root is the caller's to fix. Exiting 0 here would be a
        # false PASS on the content rules, which are the half of this gate that needs the install.
        print(f"ERROR: Bannerlord Modules folder not found: {modules}", file=sys.stderr)
        print(f"       Set ${ENV_VAR}. The per-assignment content rules need item classes.",
              file=sys.stderr)
        return 2
    classes = ts.build_item_class_registry(MODULEDATA, modules)

    rosters = [r for r in root.findall("EquipmentRoster") if r.get("id")]
    cultures = sorted(set(tree_cultures()) | set(TREE_ALIASES))
    print("=== audit_enlistment_roster_coverage.py ===")
    print(f"Roster file:    {args.xml}")
    print(f"Tree-cultures ({len(cultures)}): {', '.join(cultures)}")
    print(f"Rosters found:  {len(rosters)}")
    print(f"Weapon classes: {len(classes):,}")

    failures: list[str] = []

    duplicates = [rid for rid, n in Counter(r.get("id") for r in rosters).items() if n > 1]
    for rid in sorted(duplicates):
        failures.append(f"DUPLICATE roster id: {rid} (the engine silently keeps one)")

    seen_cells = set()
    for roster in rosters:
        rid = roster.get("id")
        parts = split_id(rid)
        if parts is None:
            failures.append(f"BAD id (not enlist_{{culture}}_{{assignment}}_{{rank}}): {rid}")
            continue
        culture, assignment, rank = parts
        seen_cells.add((culture, assignment, rank))

        expected = DEFAULT_CULTURE if culture == "default" else culture
        culture_attr = roster.get("culture") or ""
        if culture_attr != f"Culture.{expected}":
            failures.append(f"CULTURE MISMATCH: {rid} carries culture={culture_attr!r}, "
                            f"expected 'Culture.{expected}'")

        equipment = list(roster.iter("Equipment"))
        if not equipment:
            failures.append(f"EMPTY roster: {rid}")
            continue

        for node in equipment:
            slot = node.get("slot")
            if slot not in ALLOWED_SLOTS:
                failures.append(
                    f"FORBIDDEN slot in {rid}: {slot!r}. Allowed: {sorted(ALLOWED_SLOTS)}. "
                    "Horse/HorseHarness are excluded at every assignment (see the module "
                    "docstring); Item4 is the banner slot and would be issued.")

        item_ids = [(node.get("id") or "").split(".", 1)[-1] for node in equipment]
        check_content(rid, assignment, [i for i in item_ids if i], classes, failures)

    for assignment in ASSIGNMENTS:
        for rank in RANKS:
            if ("default", assignment, rank) not in seen_cells:
                failures.append(f"MISSING default: enlist_default_{assignment}_{rank} "
                                "(mandatory -- the resolver's last resort)")

    authored = sorted(c for c, _a, _r in seen_cells if c != "default")
    # ADVISORY, not a failure: an armour piece from a folder this culture otherwise barely uses.
    # This is the #427/#431 defect class ("the quartermaster gives me gondor gloves and I'm
    # enlisted under Theoden") seen at the kit level. The generator faithfully copies whatever the
    # donor troop wears, so a cross-culture item in a troop tree becomes a cross-culture item in a
    # player's kit with nothing in between to notice.
    #
    # The expected folders are DERIVED per culture from that culture's own kits, not from a
    # hand-written runtime-id-to-folder map. A hand map would be both a maintenance mirror and a
    # walk straight into the #1 TAOM data trap: Armory folders carry LORE names (harad, rohan,
    # dunland) while roster ids carry RUNTIME StringIds (aserai, vlandia, empire), so a naive
    # equality test reports 774 false positives and is worth nothing. Comparing each culture
    # against its own dominant folders needs no map and cannot go stale.
    armory, _armory_dir = dat.build_armory_index()
    by_culture_folders: dict[str, Counter] = {}
    pieces: dict[str, list] = {}
    for roster in rosters:
        rid = roster.get("id")
        parts = split_id(rid or "")
        if parts is None or parts[0] == "default":
            continue
        culture = parts[0]
        for node in roster.iter("Equipment"):
            if node.get("slot") not in ARMOR_SLOTS:
                continue
            item = (node.get("id") or "").split(".", 1)[-1]
            folder = (armory.get(item) or {}).get("folder")
            if not folder:
                continue
            by_culture_folders.setdefault(culture, Counter())[folder] += 1
            pieces.setdefault(culture, []).append((rid, node.get("slot"), item, folder))

    outliers = []
    for culture, counts in sorted(by_culture_folders.items()):
        total = sum(counts.values())
        # Rare means BOTH a small share and a small absolute count. A share test alone missed a
        # Rohan chest that is 3 of Umbar's 42 pieces (7%, over a 5% line) while being obviously
        # foreign; a count test alone would flag a small culture's whole wardrobe.
        rare = {f for f, n in counts.items() if n <= 4 and n / total < 0.20}
        for rid, slot, item, folder in pieces.get(culture, []):
            if folder in rare:
                outliers.append(f"{rid}: {slot}={item} (folder '{folder}', "
                                f"{counts[folder]}/{total} of this culture's armour)")
    if outliers:
        print(f"\nNOTE: {len(outliers)} armour piece(s) come from a folder their culture otherwise "
              "barely uses. Not a failure (donor trees share items and some sharing is deliberate, "
              "e.g. Umbar wears the Anorien set by design), but this is where #427/#431 reappears:")
        for line in outliers:
            print(f"  {line}")

    covered = len({(c, a, r) for c, a, r in seen_cells if c != "default"})
    total = len(cultures) * len(ASSIGNMENTS) * len(RANKS)
    print(f"\nCulture cells authored: {covered}/{total} "
          f"across {len(set(authored))} culture(s); "
          f"{sum(1 for a in ASSIGNMENTS for r in RANKS if ('default', a, r) in seen_cells)}"
          f"/{len(ASSIGNMENTS) * len(RANKS)} defaults.")
    print("An absent cell is not a finding here: the resolver walks culture, then assignment, "
          "then rank,\nand that walk is pinned in EnlistmentRosterCultureCoverageTests where the real "
          "resolver can be called.")

    unknown = sorted({c for c in authored if c not in cultures})
    if unknown:
        # Not fatal: a roster for a culture nobody can enlist under is dead weight.
        print(f"\nNOTE: {len(unknown)} roster culture(s) have neither a troop tree nor an alias: "
              f"{', '.join(unknown)}")

    if failures:
        print(f"\nFAIL: {len(failures)} problem(s):")
        for line in failures:
            print(f"  {line}")
        return 1

    print("\nPASS: structure, slot allowlist, culture attributes and per-assignment content "
          "rules all hold.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
