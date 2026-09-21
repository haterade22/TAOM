#!/usr/bin/env python3
"""Pass 1 of the melee ladder fix (#631): narrow each weapon to its troop's tier band.

The melee ladder was flat. Measured 2026-09-20, the median best sustained damage per engine
tier ran 66, 64, 66, 67, 69, 69, 81, 82, 82, 69 for tiers 1 to 10, a climb of 1.04x against
vanilla's 2.23x over five tiers. The cause is assignment rather than stats: 68 weapons span
four or more tiers and reach 496 of the 732 armed troops, so the same sword arms the militiaman
and the capstone.

This is the roster half of the fix. It swaps the WEAPON a troop carries; it never touches the
Armory. That matters because the Armory is unversioned and a reinstall silently reverts it,
whereas `Main/_Module/ModuleData/troops/*.xml` is git-tracked. Pass 2 (restatting blade
`damage_factor` for the residue) comes after this one and after the anchors are re-derived,
because a weapon's tier anchor is its lowest wearer, so restatting first prices weapons against
a roster about to change. The armour side learned this the same way (`derive_armor_tiers.py`
then `rebalance_armor --tier-source roster-first`).

Dry-run by default. `--apply` writes through `fix_upgrade_armour_regressions.write_changes`,
which preserves the BOM and newline style and parses every file before writing any of them.
The rosters are git-tracked, so git is the backup.

Usage:
    python tools/fix_melee_ladder.py                    # report the plan
    python tools/fix_melee_ladder.py --culture gondor   # one culture
    python tools/fix_melee_ladder.py --apply
"""

from __future__ import annotations

import argparse
import collections
import sys
from dataclasses import dataclass
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "tools"))

import analyze_melee_ladder as report  # noqa: E402
import melee_catalogue as mc  # noqa: E402
import melee_ladder as ladder  # noqa: E402
from fix_upgrade_armour_regressions import write_changes  # noqa: E402

MODULES = report.MODULES
TROOPS = report.TROOPS

# Weapon slots. A troop's melee weapon can sit in any of them.
WEAPON_SLOTS = ("Item0", "Item1", "Item2", "Item3")

# A usage set carrying this flag cannot be paired with a shield. Handing such a polearm to a
# shield-carrying troop is the trap CLAUDE.md records as having shipped three times: the troop
# holds the weapon until combat starts and then never draws it. No error, no log.
NO_SHIELD_FLAG = "requires_no_shield"


class FixError(Exception):
    pass


@dataclass(frozen=True)
class Swap:
    troop: str
    file: str
    culture: str
    tier: int
    slot: str
    old: str
    new: str
    old_dps: float
    new_dps: float
    target: float
    reason: str

    @property
    def improvement(self) -> float:
        """How much closer to target the swap lands, in DPS. Negative would be a regression."""
        return abs(self.old_dps - self.target) - abs(self.new_dps - self.target)


def shield_forbidden(cat: mc.Catalogue, item: mc.Item) -> bool:
    """True when this weapon's PRIMARY usage refuses a shield.

    Reads the resolved usage set's flags, the same way `audit_polearm_shield_parity.py` does.
    The primary is what the engine wields by default, so it is the one that decides.
    """
    desc = mc.resolve_description(cat, item)
    if desc is None:
        return False
    usage = mc.usage_set_id(cat, item, desc)
    return NO_SHIELD_FLAG in cat.usage_flags.get(usage, frozenset())


def troop_carries_shield(troop: report.Troop, shields: set[str]) -> bool:
    return any(i in shields for i in troop.items)


def line_of(name: str) -> str:
    """The weapon's named line, e.g. `[Mordor] Uruk Sword` from `... Sword III`."""
    key = report.line_key(name)
    return key[0] if key else name


def family(item_id: str, depth: int = 2) -> str:
    """The id family a weapon belongs to, e.g. `wm_gondor` from `wm_gondor_sword_a01`.

    The `culture=` attribute on an Armory item is not usable for this: 45 items claim
    `Culture.khuzait`, 38 `Culture.empire` and 21 carry none, all inherited from whichever
    vanilla culture the file was cloned from. What a weapon actually belongs to is visible in
    its id prefix, and which prefixes a faction fields is visible in its rosters.
    """
    return "_".join(item_id.split("_")[:depth])


def build_pool(
    priced: dict[str, mc.Priced], troops: list[report.Troop]
) -> dict[str, dict[str, list[mc.Priced]]]:
    """culture -> template -> the weapons that culture may be given.

    **Culture is a hard constraint, not a preference.** An earlier pass of this tool ranked it
    as a tiebreak, and when a faction owned nothing in band the ranking fell through and handed
    Gondor's Swan Knights Uruk-hai halberds and Erebor's royal wardens orc spears. In a Middle
    earth total conversion that is not a rebalance, it is a different mod. A faction with no
    in-band weapon of a class is an UNRESOLVED case for the restat pass, never an excuse to
    cross the line.

    A culture's pool is every weapon whose id family appears anywhere in that culture's own
    rosters, which pulls in unworn stock from the same families without inventing kinship.
    """
    families: dict[str, set[str]] = collections.defaultdict(set)
    for troop in troops:
        for iid in troop.items:
            if iid in priced:
                families[troop.culture].add(family(iid))

    pool: dict[str, dict[str, list[mc.Priced]]] = collections.defaultdict(
        lambda: collections.defaultdict(list)
    )
    for culture, fams in families.items():
        for iid, p in priced.items():
            if p.best_dps > 0 and family(iid) in fams:
                pool[culture][p.item.template].append(p)
    return pool


def pick_substitute(
    current: mc.Priced,
    window: ladder.Band,
    culture_pool: dict[str, list[mc.Priced]],
    cat: mc.Catalogue,
    needs_shield_safe: bool,
    spec: dict,
) -> tuple[mc.Priced | None, str]:
    """The best replacement for `current`, or (None, why not).

    Preference order, most important first. Each step only narrows, so a weapon is never
    chosen from a worse class just because its number fits:

      1. The troop's OWN culture. A hard filter applied before anything else, by the caller.
      2. Same crafting template. A troop's skills, animations and holster all follow the class,
         so swapping a sword for a mace is a different troop, not a rebalance.
      3. Shield-safe when the troop carries a shield. Non-negotiable, see NO_SHIELD_FLAG.
      4. Inside the tier band.
      5. Same named line as the weapon it replaces, so `[Gondor] Sword II` becomes
         `[Gondor] Sword IV` rather than a different Gondorian family.
      6. Then closest to the band's target.
    """
    candidates = culture_pool.get(current.item.template, [])
    if not candidates:
        return None, f"this culture fields no other {current.item.template}"

    usable = [c for c in candidates if window.low <= c.best_dps <= window.high]
    if not usable:
        return None, (
            f"this culture owns no {current.item.template} in "
            f"{window.low:.0f}-{window.high:.0f} DPS"
        )

    if needs_shield_safe:
        safe = [c for c in usable if not shield_forbidden(cat, c.item)]
        if not safe:
            return None, (
                f"every in-band {current.item.template} this culture owns is "
                f"{NO_SHIELD_FLAG} and the troop carries a shield"
            )
        usable = safe

    # Never trade a hero weapon in as a line troop's kit.
    usable = [c for c in usable if not ladder.is_hero_blade(c.blade_piece, spec)] or usable

    same_line = line_of(current.item.name)
    same_family = family(current.item.id)

    def rank(c: mc.Priced):
        return (
            0 if line_of(c.item.name) == same_line else 1,
            0 if family(c.item.id) == same_family else 1,
            abs(c.best_dps - window.target),
        )

    best = min(usable, key=rank)
    if best.item.id == current.item.id:
        return None, "already the best in-band choice"
    why = (
        "same line" if line_of(best.item.name) == same_line
        else ("same family" if family(best.item.id) == same_family else "same culture")
    )
    return best, why


def plan(
    priced: dict[str, mc.Priced],
    cat: mc.Catalogue,
    troops: list[report.Troop],
    spec: dict,
    shields: set[str],
    cultures: set[str] | None = None,
) -> tuple[list[Swap], list[tuple[report.Troop, str, str]]]:
    """(swaps to make, troops that could not be fixed with a reason each)."""
    pool = build_pool(priced, troops)
    swaps: list[Swap] = []
    unresolved: list[tuple[report.Troop, str, str]] = []

    for troop in troops:
        if ladder.is_exempt_troop(troop.id, spec):
            continue
        if cultures and troop.culture not in cultures:
            continue
        window = ladder.band(troop.tier, troop.culture, spec)
        carries_shield = troop_carries_shield(troop, shields)

        for iid in sorted(troop.items):
            p = priced.get(iid)
            if p is None or p.best_dps <= 0:
                continue
            if window.verdict(p.best_dps) == "ok":
                continue
            replacement, why = pick_substitute(
                p, window, pool.get(troop.culture, {}), cat, carries_shield, spec
            )
            if replacement is None:
                unresolved.append((troop, iid, why))
                continue
            slots = troop.slots_of.get(iid)
            if not slots:
                unresolved.append((troop, iid, "could not locate the item's equipment slot"))
                continue
            # One swap per slot: the same weapon sits in two slots on six troops, and a swap
            # keyed on (slot, old) would otherwise replace one and leave the other behind.
            for slot in sorted(slots):
                swaps.append(
                    Swap(
                        troop=troop.id,
                        file=troop.path,
                        culture=troop.culture,
                        tier=troop.tier,
                        slot=slot,
                        old=iid,
                        new=replacement.item.id,
                        old_dps=p.best_dps,
                        new_dps=replacement.best_dps,
                        target=window.target,
                        reason=why,
                    )
                )
    return swaps, unresolved


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--apply", action="store_true", help="write the rosters (default: report)")
    ap.add_argument("--culture", action="append", help="limit to these cultures (repeatable)")
    ap.add_argument("--top", type=int, default=30, help="rows to print (default 30)")
    args = ap.parse_args(argv)

    if not MODULES.is_dir():
        print(f"SKIPPED: no Bannerlord install at {MODULES}", file=sys.stderr)
        return 2

    spec = ladder.load_spec()
    cat = mc.load(MODULES, item_modules={report.ARMORY_MODULE})
    priced, failures = mc.price_all(cat)
    troops = report.load_troops(TROOPS)
    shields = mc.load_shield_ids(MODULES)

    stale = ladder.stale_exemptions({t.id for t in troops}, spec)
    if stale:
        print(f"WARNING: exempt troops that no longer exist: {', '.join(stale)}")

    cultures = set(args.culture) if args.culture else None
    swaps, unresolved = plan(priced, cat, troops, spec, shields, cultures)

    print(f"melee ladder roster pass (#631): {len(priced)} weapons, {len(troops)} troops")
    if failures:
        print(f"  {len(failures)} weapon(s) could not be priced and were skipped")
    print(f"  {len(swaps)} swaps planned across {len({s.troop for s in swaps})} troops")
    print(f"  {len(unresolved)} weapon slots could not be resolved")

    regressions = [s for s in swaps if s.improvement <= 0]
    if regressions:
        print(f"  REFUSING: {len(regressions)} swaps would not move closer to target")
        for s in regressions[:5]:
            print(f"    {s.troop} {s.old} -> {s.new}")
        return 1

    by_dir = collections.Counter("over" if s.old_dps > s.target else "under" for s in swaps)
    print(f"  direction: {by_dir['over']} over-armed, {by_dir['under']} under-armed")
    print()
    print(f"{'troop':34s} {'T':>2} {'slot':6s} {'old':28s} {'DPS':>5} -> {'new':28s} {'DPS':>5} {'tgt':>5}  why")
    for s in sorted(swaps, key=lambda s: -abs(s.old_dps - s.target))[: args.top]:
        print(
            f"{s.troop:34s} {s.tier:>2} {s.slot:6s} {s.old:28s} {s.old_dps:5.0f} -> "
            f"{s.new:28s} {s.new_dps:5.0f} {s.target:5.0f}  {s.reason}"
        )
    if len(swaps) > args.top:
        print(f"... and {len(swaps) - args.top} more")

    if unresolved:
        print(f"\nUnresolved ({len(unresolved)}), these need a hand decision or pass 2:")
        seen = collections.Counter(why for _t, _i, why in unresolved)
        for why, n in seen.most_common(10):
            print(f"  {n:4d}  {why}")

    if not args.apply:
        print("\n(dry run; pass --apply to write)")
        return 0

    changes = [
        {"file": s.file, "troop": s.troop, "slot": s.slot, "old": s.old, "new": s.new}
        for s in swaps
    ]
    written = write_changes(changes)
    print(f"\nwrote {written} roster file(s)")
    print("Next: re-run tools/analyze_melee_ladder.py, then pass 2 (restat the residue).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
