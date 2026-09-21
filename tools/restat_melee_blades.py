#!/usr/bin/env python3
"""Pass 2 of the melee ladder fix (#631): restat blade pieces onto the ladder curve.

Pass 1 (`fix_melee_ladder.py`) narrowed each troop to a weapon its own faction already owns at
the right power. It could only reach a third of the problem, because **36 of 63 culture/class
pairs own fewer distinct weapons than the tiers they field**: Rohan fields one-handed axes
across seven tiers and owns exactly one axe. No roster swap fixes that. The weapon itself has
to move, which is this pass.

WHAT IT WRITES, AND WHY THAT IS DIFFERENT FROM PASS 1
-----------------------------------------------------
Damage lives on the BLADE PIECE, never on the item: a `<CraftedItem>` carries no stats at all,
it names pieces, and the engine simulates the rest. So this writes `damage_factor` on
`<CraftingPiece>` elements in `LOTRLOME_crafting_pieces.xml`, inside the **unversioned**
LOTRLOME_Armory. There is no git backup there, so:

  * dry-run is the default and `--apply` is explicit,
  * every written file is backed up first to `<name>.bak-<tag>` (never `*.xml`: these folders
    are globbed by the engine and an `.xml` backup injects duplicate item ids),
  * the transformed text is parsed before anything is written,
  * the write is byte-faithful (BOM and newline style preserved),
  * and re-running is idempotent, because the target is absolute rather than a multiplier
    applied to whatever is already there. `rebalance_weapons.py` has the opposite shape and
    compounds on a second run; do not copy it.

A blade shared by several weapons moves all of them at once, so the plan reports the fan-out
and prices a shared blade at the LOWEST target of the weapons on it, which is the same
anchor-to-the-weakest rule the rest of the ladder uses.

HOW THE NUMBER IS DERIVED
-------------------------
Displayed damage is `physics_magnitude * damage_factor` (`Crafting.cs:374`, `:380`) and the
attack cycle does not depend on the factor, so **DPS is exactly linear in `damage_factor`**.
The multiplier for a weapon is therefore `target_dps / current_dps`, with no search needed.
Both the swing and thrust factors are scaled by the same ratio, which moves the weapon's power
without changing its character.

A weapon's target is the ladder band's target for its ANCHOR tier, the lowest-tier troop that
carries it (`melee_ladder.target`), the rule `derive_armor_tiers.py` established for armour.

Usage:
    python tools/restat_melee_blades.py                  # report the plan
    python tools/restat_melee_blades.py --culture gondor
    python tools/restat_melee_blades.py --apply
"""

from __future__ import annotations

import argparse
import codecs
import collections
import re
import shutil
import sys
import time
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "tools"))

import analyze_melee_ladder as report  # noqa: E402
import melee_catalogue as mc  # noqa: E402
import melee_ladder as ladder  # noqa: E402

MODULES = report.MODULES
ARMORY_DATA = MODULES / report.ARMORY_MODULE / "ModuleData"
PIECES_XML = ARMORY_DATA / "LOTRLOME_crafting_pieces.xml"

# A factor below this is a weapon that cannot hurt anything; above it, one that ignores armour.
# They bound the restat so a bad target cannot produce a nonsense piece.
MIN_FACTOR = 0.5
MAX_FACTOR = 12.0

# Do not rewrite a factor that is already within this fraction of its target. Keeps the diff to
# the pieces that actually need to move and makes a re-run a no-op.
TOLERANCE = 0.02


# A weapon that can be couched is priced wrongly by the DPS model and must not be restatted
# on it. `ComputeBlowMagnitudeMelee` feeds the attacker's closing speed into
# `CalculateStrikeMagnitudeForThrust` as `extraLinearSpeed`, where it is ADDED to the thrust
# speed and the sum is SQUARED (CombatStatCalculator.cs:40-48). A couched lance on a charging
# horse therefore already lands several times the standing-thrust magnitude this model
# computes, and multiplying its damage_factor to fix an apparent DPS shortfall would multiply
# that amplified hit too.
#
# The guard is therefore ASYMMETRIC, and that asymmetry is the whole point: RAISING a couchable
# weapon is refused, because the model cannot see what it would really do on a charge; LOWERING
# one is allowed, because a weapon that is too strong standing still is also too strong couched.
# A symmetric guard protected `wm_gundabad_spear_a02` at 164 DPS in the hands of tier-3 militia,
# which is the single worst weapon the ladder audit found.
COUCH_FEATURES = {"couch", "bracing"}


class RestatError(Exception):
    pass


def is_couchable(cat: mc.Catalogue, item: mc.Item) -> bool:
    """True when any of the item's usage modes is a couch or brace mode."""
    for desc in mc.matching_descriptions(cat, item):
        if COUCH_FEATURES & set(desc.item_usage_features):
            return True
    return False


@dataclass
class BladePlan:
    blade: str
    items: list[str]
    anchor_tier: int
    culture: str
    target_dps: float
    current_dps: float
    old_swing: float
    new_swing: float
    old_thrust: float
    new_thrust: float
    wearers: int
    clamped: str = ""

    @property
    def ratio(self) -> float:
        return self.target_dps / self.current_dps if self.current_dps else 1.0


def build_plans(
    priced: dict[str, mc.Priced],
    cat: mc.Catalogue,
    troops: list[report.Troop],
    spec: dict,
    cultures: set[str] | None = None,
) -> tuple[list[BladePlan], list[str]]:
    """(plans, notes). One plan per blade piece that needs to move."""
    placements = report.place(priced, troops)
    troop_culture = {}
    for t in troops:
        for iid in t.items:
            troop_culture.setdefault(iid, t.culture)

    # A weapon reachable only by ladder-exempt troops is off the ladder by decision: the cave
    # troll's club is scenery-scale and deliberately slow, and restatting it to a tier-10
    # target would quadruple a monster's damage to fix a number nobody was reading.
    def only_exempt(iid: str) -> bool:
        wearers = placements[iid].wearers
        return bool(wearers) and all(ladder.is_exempt_troop(t.id, spec) for t in wearers)

    # blade -> EVERY weapon built on it, and separately the ones troops carry.
    # The ratio must be computed against every weapon on the blade, because the edit moves all
    # of them: pricing from the worn ones alone sent an unworn Erebor axe sharing a blade to
    # 186 DPS while its worn sibling landed on target.
    all_on_blade: dict[str, list[str]] = collections.defaultdict(list)
    for iid, p in priced.items():
        if p.blade_piece:
            all_on_blade[p.blade_piece].append(iid)
    by_blade: dict[str, list[str]] = collections.defaultdict(list)
    for iid, p in priced.items():
        if p.blade_piece and iid in placements:
            by_blade[p.blade_piece].append(iid)

    plans: list[BladePlan] = []
    notes: list[str] = []
    for blade, items in sorted(by_blade.items()):
        if ladder.is_hero_blade(blade, spec):
            continue
        if all(only_exempt(i) for i in items):
            notes.append(f"{blade}: only ladder-exempt troops carry it, left alone")
            continue
        # Price the blade for the weakest troop that can reach any weapon on it.
        worst = None
        for iid in items:
            pl = placements[iid]
            culture = troop_culture.get(iid, "")
            if cultures and culture not in cultures:
                continue
            tgt = ladder.target(pl.anchor_tier, culture, spec)
            if worst is None or tgt < worst[0]:
                worst = (tgt, pl.anchor_tier, culture, iid)
        if worst is None:
            continue
        target_dps, anchor, culture, _iid = worst

        # The worn weapons decide the TARGET; every weapon on the blade decides the RATIO.
        worn_dps = max(priced[i].best_dps for i in items)
        siblings = all_on_blade[blade]
        current = max(priced[i].best_dps for i in siblings)
        if worn_dps <= 0 or current <= 0:
            notes.append(f"{blade}: every weapon on it has no usable attack, skipped")
            continue

        ratio = target_dps / worn_dps
        # Never let the strongest weapon on the blade breach the ceiling.
        ceiling_ratio = spec["ceiling"] / current
        if ratio > ceiling_ratio:
            ratio = ceiling_ratio
            notes.append(
                f"{blade}: capped at the ceiling because it also backs "
                f"{max(siblings, key=lambda i: priced[i].best_dps)}"
            )
        couched = [i for i in items if is_couchable(cat, priced[i].item)]
        if couched and ratio > 1.0:
            notes.append(
                f"{blade}: {couched[0]} can be couched, so its standing-thrust DPS understates "
                "it and RAISING it would multiply an already speed-amplified hit. Left alone; "
                "price it by hand if it really is too weak"
            )
            continue
        if abs(ratio - 1.0) <= TOLERANCE:
            continue

        piece = cat.pieces.get(blade)
        if piece is None or piece.blade is None:
            notes.append(f"{blade}: not a blade piece in the loaded Armory, skipped")
            continue

        old_s, old_t = piece.blade.swing_factor, piece.blade.thrust_factor
        new_s, new_t = old_s * ratio, old_t * ratio
        clamped = ""
        for name, old, new in (("swing", old_s, new_s), ("thrust", old_t, new_t)):
            if old > 0 and not (MIN_FACTOR <= new <= MAX_FACTOR):
                clamped = f"{name} factor clamped into [{MIN_FACTOR}, {MAX_FACTOR}]"
        new_s = min(max(new_s, MIN_FACTOR), MAX_FACTOR) if old_s > 0 else old_s
        new_t = min(max(new_t, MIN_FACTOR), MAX_FACTOR) if old_t > 0 else old_t

        plans.append(
            BladePlan(
                blade=blade,
                items=sorted(siblings),
                anchor_tier=anchor,
                culture=culture,
                target_dps=target_dps,
                current_dps=current,
                old_swing=round(old_s, 2),
                new_swing=round(new_s, 2),
                old_thrust=round(old_t, 2),
                new_thrust=round(new_t, 2),
                wearers=sum(len(placements[i].wearers) for i in items),
                clamped=clamped,
            )
        )
    # Weapons no troop carries still reach the player through merchants and loot, so they get
    # no tier target but they do get the ceiling. Without this a 153 DPS two-handed axe sits in
    # every Rohan market, above anything vanilla fields, and no roster-driven pass ever sees it.
    ceiling = spec["ceiling"]
    planned = {p.blade for p in plans}
    unworn: dict[str, list[str]] = collections.defaultdict(list)
    for iid, p in priced.items():
        if iid not in placements and p.blade_piece and p.best_dps > ceiling:
            unworn[p.blade_piece].append(iid)
    for blade, items in sorted(unworn.items()):
        if blade in planned or ladder.is_hero_blade(blade, spec):
            continue
        piece = cat.pieces.get(blade)
        if piece is None or piece.blade is None:
            continue
        current = max(priced[i].best_dps for i in items)
        ratio = ceiling / current
        old_s, old_t = piece.blade.swing_factor, piece.blade.thrust_factor
        plans.append(
            BladePlan(
                blade=blade, items=sorted(items), anchor_tier=-1, culture="(unworn)",
                target_dps=ceiling, current_dps=current,
                old_swing=round(old_s, 2),
                new_swing=round(min(max(old_s * ratio, MIN_FACTOR), MAX_FACTOR), 2) if old_s > 0 else old_s,
                old_thrust=round(old_t, 2),
                new_thrust=round(min(max(old_t * ratio, MIN_FACTOR), MAX_FACTOR), 2) if old_t > 0 else old_t,
                wearers=0, clamped="",
            )
        )

    plans.sort(key=lambda p: -abs(p.ratio - 1.0))
    return plans, notes


_PIECE_RE = re.compile(r"<CraftingPiece\b[^>]*(?:/>|>.*?</CraftingPiece>)", re.S)


def rewrite(text: str, edits: dict[str, tuple[float, float]]) -> tuple[str, int]:
    """Apply {blade id: (swing factor, thrust factor)} to the pieces XML text.

    Attribute-in-place, not a re-emit: the Armory writes every attribute on its own line and a
    re-serialised document would rewrite thousands of lines the change did not ask for.
    """
    changed = 0

    def fix_piece(m: re.Match) -> str:
        nonlocal changed
        block = m.group(0)
        head = block[: block.find(">") + 1]
        idm = re.search(r'\bid="([^"]*)"', head)
        if not idm or idm.group(1) not in edits:
            return block
        swing, thrust = edits[idm.group(1)]

        def set_factor(chunk: str, tag: str, value: float) -> str:
            # Only inside the named <Swing>/<Thrust> element of this piece's <BladeData>.
            pattern = re.compile(r"(<" + tag + r"\b[^>]*?damage_factor=\")([^\"]*)(\")", re.S)
            return pattern.sub(lambda mm: mm.group(1) + f"{value:.2f}" + mm.group(3), chunk, count=1)

        new_block = set_factor(block, "Swing", swing)
        new_block = set_factor(new_block, "Thrust", thrust)
        if new_block != block:
            changed += 1
        return new_block

    return _PIECE_RE.sub(fix_piece, text), changed


def write_pieces(path: Path, edits: dict[str, tuple[float, float]], tag: str) -> int:
    raw = path.read_bytes()
    bom = raw.startswith(codecs.BOM_UTF8)
    text = raw.decode("utf-8-sig" if bom else "utf-8")
    newline = "\r\n" if "\r\n" in text else "\n"
    body = text.replace("\r\n", "\n")

    new_body, changed = rewrite(body, edits)
    if not changed:
        return 0

    # Parse before writing. A transform that breaks the document must never reach disk.
    try:
        ET.fromstring(new_body.encode("utf-8"))
    except ET.ParseError as exc:
        raise RestatError(
            f"{path.name} would no longer be well-formed XML after the restat, so nothing was "
            f"written: {exc}"
        ) from exc

    # Back up first. NEVER a .xml extension: the engine globs these folders and an .xml backup
    # injects duplicate crafting piece ids.
    backup = path.with_suffix(path.suffix + f".bak-{tag}")
    shutil.copy2(path, backup)

    out = new_body.replace("\n", newline).encode("utf-8")
    if bom:
        out = codecs.BOM_UTF8 + out
    path.write_bytes(out)
    return changed


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--apply", action="store_true", help="write the Armory (default: report)")
    ap.add_argument("--culture", action="append", help="limit to these cultures (repeatable)")
    ap.add_argument("--top", type=int, default=30)
    ap.add_argument("--pieces", default=str(PIECES_XML), help="crafting pieces XML to write")
    args = ap.parse_args(argv)

    if not MODULES.is_dir():
        print(f"SKIPPED: no Bannerlord install at {MODULES}", file=sys.stderr)
        return 2
    pieces_path = Path(args.pieces)
    if not pieces_path.is_file():
        print(f"SKIPPED: no crafting pieces XML at {pieces_path}", file=sys.stderr)
        return 2

    spec = ladder.load_spec()
    cat = mc.load(MODULES, item_modules={report.ARMORY_MODULE})
    priced, failures = mc.price_all(cat)
    troops = report.load_troops(report.TROOPS)
    cultures = set(args.culture) if args.culture else None

    plans, notes = build_plans(priced, cat, troops, spec, cultures)

    print(f"melee blade restat (#631 pass 2): {len(priced)} weapons, {len(troops)} troops")
    if failures:
        print(f"  {len(failures)} weapon(s) could not be priced and were skipped")
    print(f"  {len(plans)} blade piece(s) to restat, reaching "
          f"{len({i for p in plans for i in p.items})} weapons and "
          f"{sum(p.wearers for p in plans)} troop slots")
    shared = [p for p in plans if len(p.items) > 1]
    print(f"  {len(shared)} of those blades back more than one weapon, so the edit moves all of them")
    clamped = [p for p in plans if p.clamped]
    if clamped:
        print(f"  {len(clamped)} hit the factor bounds and were clamped (listed below)")
    for n in notes[:10]:
        print(f"  note: {n}")
    print()
    print(f"{'blade piece':38s} {'cult':10s} {'T':>2} {'now':>6} {'target':>7} {'swing':>13} {'thrust':>13} {'wpns':>4}")
    for p in plans[: args.top]:
        print(f"{p.blade:38s} {p.culture:10s} {p.anchor_tier:>2} {p.current_dps:6.0f} "
              f"{p.target_dps:7.0f} {p.old_swing:6.2f}->{p.new_swing:<6.2f} "
              f"{p.old_thrust:6.2f}->{p.new_thrust:<6.2f} {len(p.items):>4}"
              + (f"  [{p.clamped}]" if p.clamped else ""))
    if len(plans) > args.top:
        print(f"... and {len(plans) - args.top} more")

    if not args.apply:
        print("\n(dry run; pass --apply to write the Armory)")
        return 0

    edits = {p.blade: (p.new_swing, p.new_thrust) for p in plans}
    tag = time.strftime("%Y%m%d-%H%M%S")
    written = write_pieces(pieces_path, edits, tag)
    print(f"\nrewrote {written} crafting piece(s) in {pieces_path}")
    print(f"backup: {pieces_path.name}.bak-{tag}")
    print("Next: re-run tools/analyze_melee_ladder.py and tools/validate_moduledata.py, then "
          "RESTART Bannerlord before believing anything.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
