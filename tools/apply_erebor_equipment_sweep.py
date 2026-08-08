#!/usr/bin/env python3
"""Spread unused/underused dwarf gear across Erebor + Iron Hills troop rosters.

The Erebor tree authored plenty of variety it never used: of 432 dwarf items in
LOTRLOME_Armory, a large share are referenced nowhere, and 11 of the 27 two-handed
weapons sit on exactly one troop while the two spears carry most of the polearm slots.
Meanwhile individual troops repeat the same helmet or the same axe across several of
their own equipment rosters, so the randomisation the rosters exist to provide is wasted.

This script fixes that in place, without touching roster structure.

Objective
---------
Within a single troop, when the same item fills the same slot in more than one of its
battle rosters, replace the duplicate with a sibling item (same Armory "stem", same
item type, comparable armour value), preferring siblings that are used least often
across the whole file - a globally unused item wins outright.

Why this is safe
----------------
Only the SECOND and later occurrence of an item within one troop is ever rewritten.
The first occurrence always stays, so the replaced item is still present in that troop
and its global reference count can never reach zero. Coverage strictly increases.

What it never does
------------------
- Add, remove or reorder <EquipmentRoster> elements.
- Touch civilian rosters.
- Touch anything outside <Equipments> - no id, level, skills or upgrade_targets.
- Introduce an item id that does not already ship in LOTRLOME_Armory. Authoring a new
  <CraftedItem> crashes new-campaign load when its meshes are not in the compiled
  AssetPackages (see commit 436a1d05, the reverted Gondor poleaxe).

Usage
-----
    python tools/apply_erebor_equipment_sweep.py            # dry run, prints the plan
    python tools/apply_erebor_equipment_sweep.py --apply    # rewrite the XML
"""

from __future__ import annotations

import argparse
import collections
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from _gamedir import game_dir

BOM = b"\xef\xbb\xbf"

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TROOPS = os.path.join(REPO, "Main", "_Module", "ModuleData", "troops", "troops_erebor.xml")
MODULEDATA = os.path.join(REPO, "Main", "_Module", "ModuleData")
ARMORY = (
    game_dir(r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord")
    + r"/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items"
)

DWARF_CULTURE = "Culture.erebor"

# Slots that hold a weapon/shield rather than armour. Armour-value proximity is
# meaningless for these, so the tolerance check is skipped.
WEAPON_SLOTS = {"Item0", "Item1", "Item2", "Item3"}

# A sibling may not differ from the item it replaces by more than this fraction of
# armour value, so a mid-tier trooper cannot inherit elite plate off a name match.
# Same-stem siblings are already the same class, so they get the looser band; the
# relaxed pass crosses classes (heavy -> elite) and is held to a tighter one.
ARMOR_TOLERANCE = 0.25
ARMOR_TOLERANCE_RELAXED = 0.15

# Reach / hitting-power band for weapons and shields.
WEAPON_TOLERANCE = 0.10

# Items whose ids end in these tokens are clan/lord heraldic colourways, Dain's personal
# set, or arena-only gear. They are deliberately never placed on line troops.
RESERVED_SUFFIXES = ("_blue", "_green", "_red")
RESERVED_TOKENS = ("_dain", "_lord", "tournament", "starter_")

# Ranged gear is tier-ordered on purpose: crossbow_heavy_b does 130 thrust to _a's 120,
# and the a/b split IS the progression from arbalest to sharpshooter. Swapping for variety
# would let a tier-2 crossbowman outshoot the tier-4 one - the Dale yew-bow/longbow
# inversion that .claude/rules/troops.md was written about. Left to hand-authoring.
TIERED_RANGED_TYPES = {"Bow", "Crossbow", "Arrows", "Bolts"}


def is_reserved(item_id: str) -> bool:
    return item_id.endswith(RESERVED_SUFFIXES) or any(t in item_id for t in RESERVED_TOKENS)


def stem(item_id: str) -> str:
    """Strip trailing variant tokens so siblings share a stem.

    sk_dwarf_iron_helmet_elite_a1 -> sk_dwarf_iron_helmet_elite
    sm_dwarf_erebor_axe_2h_c2     -> sm_dwarf_erebor_axe_2h
    """
    return re.sub(r"(_[a-z]\d*){1,2}$", "", item_id)


def family(item_id: str) -> str:
    """The visual family an item belongs to - Erebor gear and Iron Hills gear look
    different, so the relaxed pass may cross item classes but never crosses this."""
    for prefix in ("sk_dwarf_erebor", "sk_dwarf_iron", "sm_dwarf_erebor", "sm_dwarf_iron", "sm_iron"):
        if item_id.startswith(prefix):
            return prefix
    return item_id


def load_crafting_pieces(moduledata: str) -> dict:
    """piece id -> (blade_length, swing damage_factor).

    Crafted melee weapons carry NO <Weapon> element - their reach and damage come from the
    blade piece. Without this, every axe in a stem looks identical and the sweep will hand a
    20-unit stub blade to a tier-5 specialist while a tier-2 crossbowman gets the 43-unit one.
    """
    pieces = {}
    for path in glob.glob(moduledata + "/**/*.xml", recursive=True):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        for piece in root.iter("CraftingPiece"):
            blade = piece.find(".//BladeData")
            if blade is None:
                continue
            swing = blade.find("Swing")
            pieces[piece.get("id")] = (
                float(blade.get("blade_length") or 0),
                float(swing.get("damage_factor") or 0) if swing is not None else 0.0,
            )
    return pieces


def load_armory() -> dict:
    """id -> dict(kind, armor, material, reach, power).

    `reach`/`power` are the weapon-side analogue of `armor`: they let the selection rule
    reject a swap that keeps the item class but changes how hard it hits or how far it
    reaches. `material` blocks a same-armour-value Chainmail -> Plate visual jump.
    """
    moduledata = os.path.dirname(ARMORY.rstrip("/"))
    pieces = load_crafting_pieces(moduledata)

    meta = {}
    for path in glob.glob(ARMORY + "/**/*.xml", recursive=True):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        for tag in ("Item", "CraftedItem"):
            for it in root.iter(tag):
                if it.get("culture") != DWARF_CULTURE:
                    continue
                armor = it.find(".//Armor")
                value, material = 0, None
                if armor is not None:
                    value = sum(
                        int(armor.get(k, 0))
                        for k in ("head_armor", "body_armor", "leg_armor", "arm_armor")
                    )
                    material = armor.get("material_type")

                reach = power = 0.0
                weapon = it.find(".//Weapon")
                if weapon is not None:  # shields, bows: stats live on the element
                    reach = float(weapon.get("weapon_length") or 0)
                    power = float(weapon.get("hit_points") or weapon.get("swing_damage") or 0)
                for piece in it.iter("Piece"):  # crafted melee: stats live on the blade
                    stats = pieces.get(piece.get("id"))
                    if stats and stats[0]:
                        reach, power = stats

                meta[it.get("id")] = {
                    "kind": it.get("Type") or it.get("crafting_template") or "?",
                    "armor": value,
                    "material": material,
                    "reach": reach,
                    "power": power,
                }
    return meta


def moduledata_usage() -> collections.Counter:
    """Reference count for every Item.X across ALL of ModuleData.

    Wider than the troop file alone: lord equipment sets, career starting gear and
    civilian rosters all count as "used", so this does not mistake Dain's axe or a
    starter tunic for dead content.
    """
    counts = collections.Counter()
    for path in glob.glob(MODULEDATA + "/**/*.xml", recursive=True):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        for el in root.iter():
            value = el.get("id") or ""
            if value.startswith("Item."):
                counts[value[5:]] += 1
    return counts


def collect_occurrences(root) -> list:
    """Every battle-roster equipment slot, in document order.

    Index into this list lines up with the Nth `id="Item.` line in the raw file,
    which is how edits are applied without reserialising (and reformatting) the XML.
    """
    occurrences = []
    index = 0
    for npc in root.iter("NPCCharacter"):
        troop = npc.get("id")
        level = int(npc.get("level") or 0)
        for roster_index, roster in enumerate(npc.iter("EquipmentRoster")):
            civilian = (roster.get("civilian") or "").lower() == "true"
            for eq in roster.iter("equipment"):
                occurrences.append(
                    {
                        "index": index,
                        "troop": troop,
                        "level": level,
                        "roster": roster_index,
                        "civilian": civilian,
                        "slot": eq.get("slot"),
                        "item": (eq.get("id") or "")[5:],
                    }
                )
                index += 1
    return occurrences


def tier_floor(occurrences, meta) -> dict:
    """item id -> lowest troop level that already wears it.

    This is the sweep's tier signal. The armoury has no tier field, and names lie
    (`sk_dwarf_iron_chest_heavy_e` outranks `..._elite_f`), so the only trustworthy
    statement of "how good is this item" is which troops the authors put it on.

    An item nobody wears has no floor of its own, so it inherits one from the closest
    sibling in its stem by strength. Without this, "prefer the least-used item" reaches
    straight for end-tier exclusives - they are rare BECAUSE they are end-tier - and drops
    royal-warden plate onto a level-11 recruit.
    """
    floor = {}
    for occ in occurrences:
        if occ["civilian"] or occ["item"] not in meta:
            continue
        item = occ["item"]
        floor[item] = min(floor.get(item, 10**6), occ["level"])

    def strength(item_id):
        info = meta[item_id]
        return info["armor"] or (info["reach"] * info["power"])

    for item_id in meta:
        if item_id in floor:
            continue
        siblings = [
            s
            for s in floor
            if s in meta and stem(s) == stem(item_id) and meta[s]["kind"] == meta[item_id]["kind"]
        ]
        if siblings:
            nearest = min(siblings, key=lambda s: abs(strength(s) - strength(item_id)))
            floor[item_id] = floor[nearest]
    return floor


def build_plan(occurrences, meta, usage, floor):
    """Return [(occurrence index, old item, new item, troop, slot)] and a live usage map."""
    # Sibling pools. The strict pool is same-stem (same item class); the relaxed pool is
    # the whole visual family for that slot type, used only when the strict pool is spent.
    siblings = collections.defaultdict(list)
    relatives = collections.defaultdict(list)
    for item_id, info in meta.items():
        kind = info["kind"]
        if is_reserved(item_id):
            continue
        siblings[(stem(item_id), kind)].append(item_id)
        relatives[(family(item_id), kind)].append(item_id)

    live = collections.Counter(usage)
    plan = []

    by_troop = collections.defaultdict(list)
    for occ in occurrences:
        if not occ["civilian"]:
            by_troop[occ["troop"]].append(occ)

    for troop in sorted(by_troop):
        # Per slot, walk this troop's rosters in order; the first sighting of an item is
        # kept, later repeats become candidates for replacement.
        seen_in_slot = collections.defaultdict(set)
        for occ in sorted(by_troop[troop], key=lambda o: (o["slot"], o["roster"])):
            slot, current = occ["slot"], occ["item"]
            if current not in meta:
                continue  # vanilla item (e.g. southern_throwing_axe_1_t4) - leave alone
            if current not in seen_in_slot[slot]:
                seen_in_slot[slot].add(current)
                continue

            info = meta[current]
            kind, value = info["kind"], info["armor"]
            if kind in TIERED_RANGED_TYPES:
                continue

            wants_cape = "_cape" in current
            level = occ["level"]

            def within(candidate, attribute, tolerance):
                have, want = meta[candidate][attribute], info[attribute]
                if not want:
                    return True
                return abs(have - want) <= tolerance * want

            def candidates(source, tolerance):
                pool = [c for c in source if c not in seen_in_slot[slot]]
                # Never push an item onto a troop lower than the lowest troop already
                # trusted with it. This is what keeps the tier ladder intact.
                pool = [c for c in pool if level >= floor.get(c, 0)]
                if slot in WEAPON_SLOTS:
                    # Reach and hitting power stand in for the armour band, which cannot
                    # see weapons. Blocks a 43-unit blade becoming a 20-unit stub, and a
                    # 2.66 damage factor becoming 2.96.
                    pool = [
                        c for c in pool
                        if within(c, "reach", WEAPON_TOLERANCE)
                        and within(c, "power", WEAPON_TOLERANCE)
                    ]
                elif value > 0:
                    pool = [c for c in pool if within(c, "armor", tolerance)]
                    # Equal armour value does not mean equal look: mail and plate can score
                    # the same and read completely differently on the model.
                    pool = [c for c in pool if meta[c]["material"] == info["material"]]
                # A pauldron with a cloak and one without occupy the same slot but are not
                # interchangeable - swapping across would silently strip a troop's cloak.
                return [c for c in pool if ("_cape" in c) == wants_cape]

            # Same-class siblings, plus the wider visual family on a tighter armour band.
            # Both go in one pool: scarcity decides first, so a never-used elite helmet
            # beats an already-common heavy one, but among equally-scarce options the
            # same-class pick wins.
            strict = set(candidates(siblings.get((stem(current), kind), []), ARMOR_TOLERANCE))
            if slot in WEAPON_SLOTS:
                # Weapons and shields carry their stats outside <Armor>, so the armour-value
                # guard cannot police them. Restrict to same-stem siblings so a tower shield
                # can only become another tower shield, never a leather one.
                pool = list(strict)
            else:
                wider = candidates(relatives.get((family(current), kind), []), ARMOR_TOLERANCE_RELAXED)
                pool = list(strict.union(wider))
            if not pool:
                continue

            pool.sort(key=lambda c: (live[c], 0 if c in strict else 1, abs(meta[c]["armor"] - value), c))
            chosen = pool[0]
            # Scarcity ORDERS the pool but must not veto the swap. Replacing a troop's
            # second copy of an item always buys within-troop variety, which is the whole
            # point, even when the replacement is no rarer globally - `axe_2h_d` appearing
            # twice on one troop is worth breaking up even though `axe_2h_a` is just as
            # common elsewhere. Only reject a replacement that is strictly MORE common,
            # which would concentrate the distribution rather than spread it.
            if live[chosen] > live[current]:
                continue

            plan.append((occ["index"], current, chosen, troop, slot))
            live[current] -= 1
            live[chosen] += 1
            seen_in_slot[slot].add(chosen)

    return plan, live


def build_plan_to_fixpoint(occurrences, meta, usage, floor, max_rounds=10):
    """Run build_plan until it stops finding work, then return one merged plan.

    A single pass is not a fixpoint: build_plan decrements `live` as it walks troops in
    sorted order, so a troop considered early sees higher usage counts than the same troop
    would see at the end. Re-running therefore finds a few more swaps. Iterating here means
    one invocation lands the file in its final state and a re-run is a genuine no-op.
    """
    working = [dict(occ) for occ in occurrences]
    merged = {}
    live = collections.Counter(usage)

    for _ in range(max_rounds):
        plan, live = build_plan(working, meta, live, floor)
        if not plan:
            break
        for index, old, new, troop, slot in plan:
            if index in merged:
                merged[index][1] = new  # keep the ORIGINAL old; apply_plan verifies it
            else:
                merged[index] = [old, new, troop, slot]
            working[index]["item"] = new
    else:
        raise SystemExit(f"ABORT: substitutions did not converge in {max_rounds} rounds.")

    final = [
        (index, old, new, troop, slot)
        for index, (old, new, troop, slot) in sorted(merged.items())
        if old != new
    ]
    return final, live


def apply_plan(plan, occurrences) -> None:
    """Rewrite only the id="Item.X" text on the affected lines, preserving formatting."""
    raw = Path(TROOPS).read_bytes()
    had_bom = raw.startswith(BOM)
    text = raw.decode("utf-8-sig")

    line_positions = [m for m in re.finditer(r'id="Item\.([^"]+)"', text)]

    # The plan indexes into the ElementTree walk; the write indexes into a regex scan.
    # Those two orderings agree today, but ElementTree drops comments and the regex does
    # not, so a single commented-out <equipment> line would desynchronise them and land
    # substitutions in the wrong roster - legal item ids, so no validator would catch it.
    # Prove the correspondence over the whole file rather than spot-checking the planned
    # indices, which cannot detect a drift onto another copy of the same id.
    if len(line_positions) != len(occurrences):
        raise SystemExit(
            f"ABORT: {len(line_positions)} regex matches vs {len(occurrences)} parsed "
            "equipment elements. A comment or stray element desynchronised them."
        )
    drifted = [
        i for i, m in enumerate(line_positions) if m.group(1) != occurrences[i]["item"]
    ]
    if drifted:
        raise SystemExit(
            f"ABORT: regex/parse order diverges at {len(drifted)} position(s), "
            f"first at index {drifted[0]}."
        )

    # The engine globs *.xml in registered ModuleData dirs, so the backup must not use a
    # .xml extension or it gets loaded and injects duplicate ids.
    backup = TROOPS + ".bak-ereborsweep"
    Path(backup).write_bytes(raw)
    print(f"backup written: {os.path.relpath(backup, REPO)}")

    replacements = {index: (old, new) for index, old, new, _t, _s in plan}

    out, cursor = [], 0
    for i, match in enumerate(line_positions):
        if i not in replacements:
            continue
        old, new = replacements[i]
        if match.group(1) != old:
            raise SystemExit(
                f"ABORT: occurrence {i} is '{match.group(1)}', expected '{old}'. "
                "The XML changed under the script - re-run the dry run."
            )
        out.append(text[cursor:match.start()])
        out.append(f'id="Item.{new}"')
        cursor = match.end()
    out.append(text[cursor:])

    # Write bytes, re-prepending the BOM only if the source had one. Opening with
    # encoding="utf-8-sig" would emit a BOM unconditionally - harmless here, but 11 of the
    # 16 files in troops/ have no BOM, and this script is an obvious template to retarget.
    Path(TROOPS).write_bytes((BOM if had_bom else b"") + "".join(out).encode("utf-8"))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="write changes (default: dry run)")
    parser.add_argument("--verbose", action="store_true", help="list every substitution")
    args = parser.parse_args()

    meta = load_armory()
    if not meta:
        print(f"ERROR: no dwarf items found under {ARMORY}", file=sys.stderr)
        return 1

    usage = moduledata_usage()
    root = ET.parse(TROOPS).getroot()
    occurrences = collect_occurrences(root)

    before_unplaced = {
        i for i in meta if usage[i] == 0 and not is_reserved(i)
    }

    floor = tier_floor(occurrences, meta)
    plan, live = build_plan_to_fixpoint(occurrences, meta, usage, floor)
    after_unplaced = {i for i in before_unplaced if live[i] == 0}

    dropped = [i for i in meta if usage[i] > 0 and live[i] == 0]

    print(f"dwarf items in Armory:            {len(meta)}")
    print(f"placeable items unused before:    {len(before_unplaced)}")
    print(f"placeable items still unused:     {len(after_unplaced)}")
    print(f"substitutions planned:            {len(plan)}")
    print(f"items that would lose coverage:   {len(dropped)}  (must be 0)")

    if dropped:
        print("\nABORT - these would drop to zero references:", file=sys.stderr)
        for item in sorted(dropped):
            print(f"  {item}", file=sys.stderr)
        return 1

    newly = sorted(before_unplaced - after_unplaced)
    print(f"\nnewly placed ({len(newly)}):")
    for item in newly:
        print(f"  + {meta[item]['kind']:15s} {item}")

    if after_unplaced:
        print(f"\nstill unplaced ({len(after_unplaced)}) - no same-stem duplicate to displace:")
        for item in sorted(after_unplaced):
            print(f"  - {meta[item]['kind']:15s} {item}")

    if args.verbose:
        print("\nsubstitutions:")
        for _i, old, new, troop, slot in plan:
            print(f"  {troop:42s} {slot:6s} {old}  ->  {new}")

    if args.apply:
        apply_plan(plan, occurrences)
        print(f"\nAPPLIED {len(plan)} substitutions to {os.path.relpath(TROOPS, REPO)}")
        print("Restart the game (or start a new campaign) to see the change - troop XML is "
              "read at campaign start, not hot-reloaded.")
    else:
        print("\n(dry run - pass --apply to write)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
