#!/usr/bin/env python3
"""Pass 1 of the melee ladder fix (#631): swap a troop's weapon for one of the same category in its tier band.

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
    python tools/fix_melee_ladder.py --culture gondor   # one engine culture id (a troop's culture attribute)
    python tools/fix_melee_ladder.py --apply
"""

from __future__ import annotations

import argparse
import collections
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "tools"))

import analyze_melee_ladder as report  # noqa: E402
import generate_new_faction_kingdoms as kingdoms  # noqa: E402
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
NO_SHIELD_FLAG = mc.NO_SHIELD_FLAG


# Troop files a generator writes whole. A swap written into one is undone by the generator's next
# --apply, so a weapon there is changed in the generator. What catches a hand edit: the realm files,
# test_generator_regions_in_sync; troops_lothlorien.xml, test_generated_files (the shipped file is
# the generator's exact output); troops_gundabad_snow.xml, nothing (its generator has no --check).
GENERATED_TROOP_FILES = {
    **{f"troops_{realm}.xml": f"generate_{realm}_troops.py" for realm in kingdoms.REGENERATABLE},
    "troops_lothlorien.xml": "generate_lothlorien_troops.py",
    "troops_gundabad_snow.xml": "generate_snow_orc_troops.py",
}
GENERATED_REASON = "its troop file is written by "
# Troop generators whose output is NOT the shipped file: each scaffolded a tree that was then
# rostered by hand, so a swap in that troop file is safe. A new generate_*_troops.py must be named
# in one of the two tables (RosterFixerGeneratedFileTests globs for them).
SCAFFOLDERS = {
    "generate_dale_troops.py": "troops_dale.xml moved past it (skill restats, slot passes); it has not run since 60f873f32",
    "generate_gondor_troops.py": "scaffolder; troops_gondor.xml was re-rostered by hand (its STATUS note)",
    "generate_rhun_troops.py": "scaffolder; it writes troops_rhun.xml, the shipped tree is troops_rhun_new.xml (its STATUS note)",
}


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


def weapon_category(cat: mc.Catalogue, p: mc.Priced, couchable: frozenset[str], spec: dict) -> tuple:
    """The weapon category a troop is built for: a swap never leaves it.

    The maintainer's rule (2026-11-14, docs/features/melee-damage-model.md "A troop keeps its
    weapon category"): sword and shield, two handed sword or axe, spear and shield, spear that
    lances, polearms, pikes, bows. Read here as the crafting template; for a template in the
    polearm craft column, whether it swings or thrusts (`ladder.dominant_flags`, the reading the
    bands use); whether it can be couched; and whether its primary usage refuses a shield. All
    polearms share one template, which is how a poleaxe once became a short spear.
    """
    attack = None
    if spec.get("craft_column", {}).get(p.item.template) == "polearm":
        swings, _thrusts = ladder.dominant_flags(p.item.template, p.swing_damage, p.thrust_damage,
                                                 p.swing_dps, p.thrust_dps, spec)
        attack = "swing" if swings else "thrust"
    return (p.item.template, attack, p.item.id in couchable, shield_forbidden(cat, p.item))


def load_upgrade_targets(root: Path) -> dict[str, tuple[str, ...]]:
    """troop id -> the troop ids it upgrades to, read from `<upgrade_target id="NPCCharacter.x"/>`."""
    out: dict[str, tuple[str, ...]] = {}
    for path in sorted(root.glob("troops_*.xml")):
        try:
            tree = ET.parse(path).getroot()
        except (ET.ParseError, OSError):
            continue
        for node in tree.iter("NPCCharacter"):
            targets = tuple(
                ref.split(".", 1)[-1]
                for up in node.iter("upgrade_target") if (ref := up.get("id"))
            )
            if node.get("id") and targets:
                out[node.get("id")] = targets
    return out


def describe_category(category: tuple) -> str:
    template, attack, couch, no_shield = category
    words = [template]
    if attack:
        words.append("swinging" if attack == "swing" else "thrusting")
    words.append("couchable" if couch else "not couchable")
    if no_shield:
        words.append("two handed only")
    return ", ".join(words)


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
    band_of: Callable[[mc.Priced], ladder.Band],
    fits_limit: Callable[[mc.Priced], bool],
    culture_pool: dict[str, list[mc.Priced]],
    cat: mc.Catalogue,
    needs_shield_safe: bool,
    spec: dict,
    taken: frozenset[str] = frozenset(),
    couchable: frozenset[str] = frozenset(),
    ceiling: float | None = None,
) -> tuple[mc.Priced | None, str]:
    """The best replacement for `current`, or (None, why not).

    Preference order, most important first. Each step only narrows, so a weapon is never
    chosen from a worse class just because its number fits:

      1. The troop's OWN culture. A hard filter applied before anything else, by the caller.
      2. Same weapon category (`weapon_category`): same crafting template, and for a polearm the
         same main attack, couch usage and shield use. A troop's skills, animations and holster
         follow the category, so a sword for a mace or a poleaxe for a spear is a different troop.
      3. Shield-safe when the troop carries a shield. Non-negotiable, see NO_SHIELD_FLAG.
      4. Inside the tier band of the CANDIDATE's own class (`band_of`), and not a hero blade
         unless nothing else is in band. A halberd and a spear share a template but not a curve.
      5. Inside the carrier's tier limit (`fits_limit`). A band is a DPS window, and a slow heavy
         weapon sits in it while showing a blow the troop's tier may not carry.
      5b. Not above `ceiling`: the best DPS the troop's upgrade targets carry on the same band
         (template and main attack).
         A swap that makes a troop stronger than what it upgrades to inverts its tree.
      6. Not already in the troop's kit (`taken`).
      7. Same named line as the weapon it replaces, so `[Gondor] Sword II` becomes
         `[Gondor] Sword IV` rather than a different Gondorian family.
      8. Then closest to the band's target.

    `taken` holds every id the troop keeps (non-weapons included, which never match a
    candidate) plus each replacement already chosen for it. A candidate in it is skipped, so
    two different weapons never collapse into one id: the Barad-Dur Guard's spear and spare
    halberd both became Halberd IX that way, and every Black Uruk Grunt set became the same
    lance. The check is per troop across all its sets, because the writer applies one swap to
    every set; a weapon left with no free candidate is reported unresolved for pass 2.
    """
    candidates = culture_pool.get(current.item.template, [])
    if not candidates:
        return None, f"this culture fields no other {current.item.template}"
    category = weapon_category(cat, current, couchable, spec)
    candidates = [c for c in candidates if weapon_category(cat, c, couchable, spec) == category]
    if not candidates:
        return None, f"this culture fields no other weapon of its category ({describe_category(category)})"

    usable = [c for c in candidates if band_of(c).verdict(c.best_dps) == "ok"]
    if not usable:
        window = band_of(current)
        return None, (
            f"this culture owns no {current.item.template} in "
            f"{window.low:.0f}-{window.high:.0f} DPS"
        )

    within = [c for c in usable if fits_limit(c)]
    if not within:
        return None, (
            f"every in-band {current.item.template} this culture owns is over the tier limit "
            "of this troop's tier and craft"
        )
    usable = within

    if ceiling is not None:
        below = [c for c in usable if c.best_dps <= ceiling]
        if not below:
            return None, (
                f"every in-band {current.item.template} this culture owns beats the "
                "weapon of the same template and main attack that the troop's upgrade carries"
            )
        usable = below

    if needs_shield_safe:
        safe = [c for c in usable if not shield_forbidden(cat, c.item)]
        if not safe:
            return None, (
                f"every in-band {current.item.template} this culture owns is "
                f"{NO_SHIELD_FLAG} and the troop carries a shield"
            )
        usable = safe

    # Never trade a hero weapon in as a line troop's kit. This runs before the kit filter, so a
    # kept weapon can never leave a hero blade as the only free pick.
    usable = [c for c in usable if not ladder.is_hero_blade(c.blade_piece, spec)] or usable

    free = [c for c in usable if c.item.id not in taken]
    if not free:
        return None, (
            f"every in-band {current.item.template} candidate is already in this troop's kit"
        )
    usable = free

    same_line = line_of(current.item.name)
    same_family = family(current.item.id)

    def rank(c: mc.Priced):
        return (
            0 if line_of(c.item.name) == same_line else 1,
            0 if family(c.item.id) == same_family else 1,
            abs(c.best_dps - band_of(c).target),
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
    upgrade_targets: dict[str, tuple[str, ...]] | None = None,
) -> tuple[list[Swap], list[tuple[report.Troop, str, str]]]:
    """(swaps to make, troops that could not be fixed with a reason each).

    Troops are planned from the highest level down, so each troop's upgrade targets already hold
    their final weapons when its own ceiling is read (`upgrade_targets`: troop id -> target ids).
    """
    pool = build_pool(priced, troops)
    couchable = frozenset(mc.couchable_ids(priced))
    upgrade_targets = upgrade_targets or {}
    by_id = {t.id: t for t in troops}
    carried: dict[str, set[str]] = {}   # troop id -> its items after its own swaps
    swaps: list[Swap] = []
    unresolved: list[tuple[report.Troop, str, str]] = []

    def category_of(p: mc.Priced) -> tuple:
        return weapon_category(cat, p, couchable, spec)

    # The ceiling compares template and main attack only (the band a weapon is judged on): a two
    # handed only halberd that upgrades to a shield-safe winged spear still swings on the same band.
    def band_key(p: mc.Priced) -> tuple:
        return category_of(p)[:2]

    def held(p: mc.Priced, items) -> mc.Priced:
        """The weapon as a troop with these items wields it: behind a shield, only the modes a
        shield allows (`Priced.with_shield`), so a shield spearman's spear is judged by its thrust."""
        return p.with_shield() if any(i in shields for i in items) else p

    def best_carried(tid: str, key: tuple) -> float | None:
        items = carried.get(tid)
        if items is None:
            items = by_id[tid].items if tid in by_id else ()
        views = [held(priced[i], items) for i in items if i in priced]
        dps = [v.best_dps for v in views if v.best_dps > 0 and band_key(v) == key]
        return max(dps) if dps else None

    def ceiling_for(troop: report.Troop, p: mc.Priced) -> float | None:
        category = band_key(p)
        caps = [b for tid in upgrade_targets.get(troop.id, ())
                if (b := best_carried(tid, category)) is not None]
        return min(caps) if caps else None

    for troop in sorted(troops, key=lambda t: -t.level):
        if ladder.is_exempt_troop(troop.id, spec):
            continue
        if cultures and troop.culture not in cultures:
            continue
        generator = GENERATED_TROOP_FILES.get(Path(troop.file).name)
        carries_shield = troop_carries_shield(troop, shields)

        # A weapon is judged by its own class's band (melee_ladder.target), so the band follows
        # the weapon and not the troop alone. The craft row comes from craft_key (a Black
        # Numenorean is not an orc) and a thrust-dominant polearm is read as a spear
        # (dominant_flags); the restat and the validator route through the same two functions.
        craft = ladder.craft_key(troop.id, troop.culture, spec)

        def band_of(p: mc.Priced) -> ladder.Band:
            return ladder.band(troop.tier, craft, spec, p.class_template,
                               *ladder.dominant_flags(p.class_template, p.swing_damage,
                                                      p.thrust_damage, p.swing_dps,
                                                      p.thrust_dps, spec))

        def fits_limit(p: mc.Priced) -> bool:
            limit = ladder.tier_limit(p.class_template, troop.tier, craft, spec)
            return not ladder.limit_breaches(limit, p.swing_damage, p.thrust_damage,
                                             p.swing_dps, p.thrust_dps)

        def out_of_band(iid: str) -> bool:
            p = priced.get(iid)
            p = held(p, troop.items) if p is not None else None
            return p is not None and p.best_dps > 0 and ladder.judge(
                troop.tier, craft, spec, p.best_dps, p.class_template,
                *ladder.dominant_flags(p.class_template, p.swing_damage, p.thrust_damage,
                                       p.swing_dps, p.thrust_dps, spec)) != "ok"

        # What the troop keeps, plus each replacement as it is chosen: never hand out a copy.
        taken = {iid for iid in troop.items if not out_of_band(iid)}
        final = set(troop.items)
        carried[troop.id] = final

        for iid in sorted(troop.items):
            if not out_of_band(iid):
                continue
            p = held(priced[iid], troop.items)
            if generator:
                unresolved.append((troop, iid, f"{GENERATED_REASON}{generator}: change the weapon there"))
                continue
            culture_pool = pool.get(troop.culture, {})
            if carries_shield:
                culture_pool = {k: [c.with_shield() for c in v] for k, v in culture_pool.items()}
            replacement, why = pick_substitute(
                p, band_of, fits_limit, culture_pool, cat, carries_shield, spec,
                frozenset(taken), couchable, ceiling_for(troop, p),
            )
            if replacement is None:
                unresolved.append((troop, iid, why))
                continue
            taken.add(replacement.item.id)
            slots = troop.slots_of.get(iid)
            if not slots:
                unresolved.append((troop, iid, "could not locate the item's equipment slot"))
                continue
            final.discard(iid)
            final.add(replacement.item.id)
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
                        target=band_of(replacement).target,
                        reason=why,
                    )
                )
    return swaps, unresolved


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--apply", action="store_true", help="write the rosters (default: report)")
    ap.add_argument("--culture", action="append",
                    help="limit to these engine culture ids (repeatable): a troop's culture "
                         "attribute. restat_melee_blades.py takes craft rows instead")
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
    swaps, unresolved = plan(priced, cat, troops, spec, shields, cultures,
                             load_upgrade_targets(TROOPS))

    print(f"melee ladder roster pass (#631): {len(priced)} weapons, {len(troops)} troops")
    if failures:
        print(f"  {len(failures)} weapon(s) could not be priced and were skipped")
    print(f"  {len(swaps)} swaps planned across {len({s.troop for s in swaps})} troops")
    print(f"  {len(unresolved)} weapon slots could not be resolved")
    refused = sum(1 for _t, _i, why in unresolved if "over the tier limit" in why)
    print(f"  {refused} of them because every in-band candidate is over the carrier's tier limit")

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
        generated = collections.Counter(
            why.split(GENERATED_REASON, 1)[1].split(":", 1)[0]
            for _t, _i, why in unresolved if GENERATED_REASON in why)
        if generated:
            print(f"  {sum(generated.values()):4d}  in troop files a generator writes (change the "
                  f"generator): {', '.join(f'{g} {n}' for g, n in sorted(generated.items()))}")
        seen = collections.Counter(why for _t, _i, why in unresolved if GENERATED_REASON not in why)
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
