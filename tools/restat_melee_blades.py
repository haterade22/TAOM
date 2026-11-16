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
anchor-to-the-weakest rule the rest of the ladder uses. "Weakest" is read off the carriers
themselves: each non-exempt troop that carries a weapon on the blade brings its own tier and
craft row, and the blade takes the lowest target among them, not the row of
whichever troop loaded first.

A blade that backs both one handed and two handed weapons is NOT priced by tier: the weakest
wearer is a one handed troop, and its target would take the two handed weapons down with it. Such a
blade moves only when a weapon on it is above its CLASS limit, and then only as far as that limit
needs. Every one is listed by name with each weapon, its class and its displayed damage, so a
later change can clone the blades.

HOW THE NUMBER IS DERIVED
-------------------------
Displayed damage is `physics_magnitude * damage_factor` (`Crafting.cs:374`, `:380`) and the
attack cycle does not depend on the factor, so **DPS is exactly linear in `damage_factor`**.
The multiplier for a weapon is therefore `target_dps / current_dps`, with no search needed.
Both the swing and thrust factors are scaled by the same ratio, which moves the weapon's power
without changing its character.

A weapon's target is the ladder band's target for its ANCHOR tier, the lowest-tier troop that
carries it (`melee_ladder.target`), the rule `derive_armor_tiers.py` established for armour. That
target is per class: the weapon's crafting template, its attacks and the wearer's
craft row decide it, so each class spreads over all ten tiers. A polearm whose thrust DPS beats
its swing DPS is a spear and takes its class top from the thrust (`ladder.dominant_flags`). The
wearer's craft row comes from `ladder.craft_key`, so a Black Numenorean is not rated as an orc.

The LIMIT is read off every carrier: each factor is held to the tier limit (`ladder.tier_limit`,
blow and DPS) of every non-exempt carrier of every weapon on the blade, and a weapon nobody
carries is held at the blade's weakest carrier. A blade shared with an elite is therefore no
stronger than its weakest wearer may carry. A blade whose wearers span many tiers leaves its
upper wearers light (R2). The cure is a cloned blade for the upper wearers, as
generate_placeholder_weapons.py makes; this tool does not clone.

Each attack follows the weapon's PRIMARY attack, the one with the higher DPS. When the primary is
held by its own limit, the other attack takes the primary's realized ratio and is never raised on
its own to chase a target the primary cannot reach. Without that a swing held at its blow limit
left the free thrust rising on every run. Every written factor is rounded DOWN to the two decimals
the XML keeps, because the tier limit's DPS is the tier target and rounding to nearest could leave a
weapon just above the limit that set the factor. A move under TOLERANCE, or under one point of
displayed damage, is not planned; it is judged on the factor as written, after the rounding down, so a
second run on its own output plans nothing.

A hero blade has no tier target. One above its hero limit (class limit plus its maker group's
bonus) is LOWERED to it; one below is left alone.

`--limits-only` lowers only the weapons above a class or hero limit. It raises nothing and ignores
the tier target and the ceiling, so older ladder drift does not ride along with a limit fix.

Usage:
    python tools/restat_melee_blades.py                  # report the plan
    python tools/restat_melee_blades.py --culture gondor    # a craft row (ladder.craft_key), not an engine culture
    python tools/restat_melee_blades.py --limits-only    # lower what breaks a limit, nothing else
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


def class_ratios(p: mc.Priced, spec: dict, tier: int | None = None,
                 culture: str | None = None) -> tuple[float, float]:
    """(swing, thrust): the largest multiplier of each factor inside the weapon's limits.

    Each attack has its own `damage_factor`, so each is held by its own limits. One ratio for
    both would take swing damage off a sword because its thrust is at the thrust limit. A hero
    blade is held to its hero limit.

    With a `tier` and the wearer's craft key (`ladder.craft_key`) an ordinary weapon is held to
    the tier limit instead of the class limit: the blade's anchor tier earns only a share of the
    class's blow, so a recruit's sword cannot carry an elite's. Without them the class limit
    holds, which is what `--limits-only` and the unworn pass want.
    """
    limit = ladder.limit_for(p.blade_piece, p.class_template, spec, tier, culture)
    return (ladder.max_factor(limit, p.swing_damage, 0, p.swing_dps, 0.0),
            ladder.max_factor(limit, 0, p.thrust_damage, 0.0, p.thrust_dps))


def dps_after(p: mc.Priced, ratio_s: float, ratio_t: float) -> float:
    return max(p.swing_dps * ratio_s, p.thrust_dps * ratio_t)


def only_exempt(iid: str, placements: dict, spec: dict) -> bool:
    """True when every troop that carries the weapon is ladder-exempt."""
    wearers = placements[iid].wearers
    return bool(wearers) and all(ladder.is_exempt_troop(t.id, spec) for t in wearers)


def exempt_only_blade(items: list[str], placements: dict, spec: dict) -> bool:
    """True when a blade has worn weapons and every troop that carries any of them is ladder-exempt.

    `items` is every weapon on the blade. Editing such a blade changes an exempt troop's weapon, so
    no pass may touch it, whatever sibling made it look worth lowering.
    """
    worn = [i for i in items if i in placements]
    return bool(worn) and all(only_exempt(i, placements, spec) for i in worn)


def blade_edit(blade: str, items: list[str], priced: dict[str, mc.Priced], cat: mc.Catalogue,
               spec: dict, label: str, ceiling: float | None) -> BladePlan | None:
    """A plan that only LOWERS the blade: each factor down to its own limits, and to `ceiling`.

    `items` is every weapon the edit moves. None when the blade is not a blade piece in the
    loaded Armory. The caller decides whether the plan is worth making.
    """
    piece = cat.pieces.get(blade)
    if piece is None or piece.blade is None:
        return None
    current = max(priced[i].best_dps for i in items)
    ratio = 1.0 if ceiling is None else min(1.0, ceiling / current)
    ratio_s = min(ratio, min(class_ratios(priced[i], spec)[0] for i in items))
    ratio_t = min(ratio, min(class_ratios(priced[i], spec)[1] for i in items))
    old_s, old_t = piece.blade.swing_factor, piece.blade.thrust_factor
    return BladePlan(
        blade=blade, items=sorted(items), anchor_tier=-1, culture=label,
        target_dps=max(dps_after(priced[i], ratio_s, ratio_t) for i in items),
        current_dps=current,
        old_swing=round(old_s, 2),
        new_swing=ladder.floor_factor(clamp_factor(old_s, old_s * ratio_s)) if old_s > 0 else old_s,
        old_thrust=round(old_t, 2),
        new_thrust=ladder.floor_factor(clamp_factor(old_t, old_t * ratio_t)) if old_t > 0 else old_t,
        wearers=0, clamped="",
    )


def breaks_a_limit(items: list[str], priced: dict[str, mc.Priced], spec: dict) -> bool:
    """True when some weapon on the blade is above a class or hero limit by more than TOLERANCE."""
    return any(min(class_ratios(priced[i], spec)) < 1.0 - TOLERANCE for i in items)


def lower_to_limit(blade: str, items: list[str], priced: dict[str, mc.Priced], cat: mc.Catalogue,
                   spec: dict, placements: dict, label: str, notes: list[str],
                   ceiling: float | None = None) -> BladePlan | None:
    """The plan that lowers a blade to its limits (and to `ceiling`), or None.

    `items` is every weapon on the blade, so the plan lists them all and counts every wearer. A
    blade only ladder-exempt troops carry is left alone with a note, in every pass.
    """
    if exempt_only_blade(items, placements, spec):
        note = f"{blade}: only ladder-exempt troops carry it, left alone"
        if note not in notes:
            notes.append(note)
        return None
    plan = blade_edit(blade, items, priced, cat, spec, label, ceiling)
    if plan is not None:
        plan.wearers = sum(len(placements[i].wearers) for i in items if i in placements)
    return plan


def blades_to_weapons(priced: dict[str, mc.Priced]) -> dict[str, list[str]]:
    """blade piece -> EVERY weapon built on it, since an edit moves all of them."""
    out: dict[str, list[str]] = collections.defaultdict(list)
    for iid, p in priced.items():
        if p.blade_piece:
            out[p.blade_piece].append(iid)
    return out


def build_limit_plans(
    priced: dict[str, mc.Priced],
    cat: mc.Catalogue,
    troops: list[report.Troop],
    spec: dict,
) -> tuple[list[BladePlan], list[str]]:
    """(plans, notes) for `--limits-only`: lower every blade above a class or hero limit.

    Raises nothing and ignores the tier target and the ceiling. A couchable weapon is lowered
    like any other, because lowering is safe (see COUCH_FEATURES). Troops matter only for the
    ladder-exempt rule: a blade carried only by exempt troops is left alone.
    """
    placements = report.place(priced, troops)
    plans: list[BladePlan] = []
    notes: list[str] = []
    for blade, items in sorted(blades_to_weapons(priced).items()):
        if not breaks_a_limit(items, priced, spec):
            continue
        plan = lower_to_limit(blade, items, priced, cat, spec, placements, "(limits)", notes)
        if plan is not None:
            plans.append(plan)
    plans.sort(key=lambda p: -abs(p.ratio - 1.0))
    return plans, notes


def handedness(template: str) -> str:
    """"two handed" for the two handed and pike templates, else "one handed" (dagger and mace too)."""
    return "two handed" if template.startswith("TwoHanded") or template == "Pike" else "one handed"


@dataclass(frozen=True)
class MixedBlade:
    """A blade piece that backs both one handed and two handed weapons, with what each shows now."""

    blade: str
    weapons: tuple[tuple[str, str, int, int], ...]  # (item, template, swing, thrust) as displayed

    def describe(self) -> str:
        shown = "; ".join(
            f"{item} ({template}, {handedness(template)}) shows swing {swing} thrust {thrust}"
            for item, template, swing, thrust in self.weapons
        )
        return f"{self.blade}: {shown}"


def mixed_blades(priced: dict[str, mc.Priced]) -> list[MixedBlade]:
    """Every blade piece whose weapons span one handed and two handed templates.

    One blade cannot be right for both: a one handed wearer's tier limit is far under what a two
    handed weapon may swing, so pricing the blade for its weakest wearer took the Erebor two handed
    axes down to their one handed twins' target. The restat leaves these blades alone and
    reports them, so a later change can clone one blade per class.
    """
    out = []
    for blade, items in sorted(blades_to_weapons(priced).items()):
        if len({handedness(priced[i].item.template) for i in items}) < 2:
            continue
        out.append(MixedBlade(blade, tuple(
            (i, priced[i].item.template, round(priced[i].swing_damage), round(priced[i].thrust_damage))
            for i in sorted(items))))
    return out


def within_tolerance(ratio: float, damage: float) -> bool:
    """True when a factor move is under TOLERANCE, or under one point of the displayed damage.
    An attack no weapon on the blade can use (damage 0) never counts.

    Damage shows as a whole number, so a move under one point changes nothing a player can see,
    and chasing it would leave every second run with work to do.
    """
    if damage <= 0:
        return True  # no weapon on the blade can use this attack, so its factor moves nothing
    return abs(ratio - 1.0) <= max(TOLERANCE, 1.0 / damage)


def clamp_factor(old: float, new: float) -> float:
    """`new` inside [MIN_FACTOR, MAX_FACTOR], but never on the far side of `old` from `new`.

    A piece already below MIN_FACTOR that must come down used to be pulled UP to MIN_FACTOR.
    """
    return min(max(new, min(MIN_FACTOR, old)), max(MAX_FACTOR, old))


@dataclass(frozen=True)
class Cap:
    """The largest multiplier of one attack's factor, and the weapon and carrier that set it."""

    ratio: float = float("inf")
    by: str = ""


def carrier_contexts(placements: dict, spec: dict,
                     shields: frozenset[str] = frozenset()) -> dict[str, list[tuple[int, str, str, bool]]]:
    """weapon -> its non-exempt carriers as (tier, craft key, a troop id, carries a shield), one per
    tier, craft row and shield use. A carrier with a shield wields the weapon in the modes a shield
    allows (`held`), as tools/fix_melee_ladder.py and the validator judge it."""
    out: dict[str, list[tuple[int, str, str, bool]]] = {}
    for iid, placement in placements.items():
        seen: dict[tuple[int, str, bool], str] = {}
        for t in sorted(placement.wearers, key=lambda w: w.id):
            if not ladder.is_exempt_troop(t.id, spec):
                shielded = any(i in shields for i in t.items)
                seen.setdefault((t.tier, ladder.craft_key(t.id, t.culture, spec), shielded), t.id)
        out[iid] = [(tier, craft, who, sh) for (tier, craft, sh), who in sorted(seen.items())]
    return out


def as_wielded(p: mc.Priced, shielded: bool) -> mc.Priced:
    """The weapon as a carrier wields it: behind a shield, only the modes a shield allows."""
    return p.with_shield() if shielded else p


def carrier_caps(items: list[str], priced: dict[str, mc.Priced], spec: dict,
                 contexts: dict[str, list[tuple[int, str, str, bool]]],
                 fallback: tuple[int, str, str, bool]) -> tuple[Cap, Cap]:
    """(swing, thrust): the tightest tier limit over EVERY non-exempt carrier of EVERY weapon on the blade.

    A weapon nobody non-exempt carries is held to the limit of `fallback`, the blade's weakest
    carrier, so an unworn sibling is no stronger than the weakest wearer may carry, in the modes that
    wearer holds. A carrier with a shield is held in the modes it wields (`as_wielded`); the weapon's
    other modes still reach a player or a troop without a shield, so they stay inside their class
    limit. When the weakest wearer has a shield, an unworn sibling's other modes are therefore held to
    their class limit only, not to a tier limit.
    """
    swing, thrust = Cap(), Cap()
    for iid in sorted(items):
        p = priced[iid]
        for tier, craft, who, shielded in contexts.get(iid) or [fallback]:
            v = as_wielded(p, shielded)
            checks = [(class_ratios(v, spec, tier, craft), f"{iid} ({v.class_template}) at tier {tier} for {who}")]
            if v is not p:
                checks.append((class_ratios(p, spec), f"{iid} ({p.class_template}) at its class limit"))
            for (ratio_s, ratio_t), where in checks:
                if ratio_s < swing.ratio:
                    swing = Cap(ratio_s, where)
                if ratio_t < thrust.ratio:
                    thrust = Cap(ratio_t, where)
    return swing, thrust


def build_plans(
    priced: dict[str, mc.Priced],
    cat: mc.Catalogue,
    troops: list[report.Troop],
    spec: dict,
    cultures: set[str] | None = None,
    shields: frozenset[str] = frozenset(),
) -> tuple[list[BladePlan], list[str]]:
    """(plans, notes). One plan per blade piece that needs to move. `shields` are the shield ids:
    a carrier with one is priced on the modes a shield allows."""
    placements = report.place(priced, troops)
    contexts = carrier_contexts(placements, spec, frozenset(shields))

    # A weapon reachable only by ladder-exempt troops is off the ladder by decision (`only_exempt`):
    # the cave troll's club is scenery-scale and deliberately slow, and restatting it to a
    # tier-10 target would quadruple a monster's damage to fix a number nobody was reading.

    # blade -> EVERY weapon built on it, and separately the ones troops carry.
    # The ratio must be computed against every weapon on the blade, because the edit moves all
    # of them: pricing from the worn ones alone sent an unworn Erebor axe sharing a blade to
    # 186 DPS while its worn sibling landed on target.
    all_on_blade = blades_to_weapons(priced)
    by_blade: dict[str, list[str]] = collections.defaultdict(list)
    for iid, p in priced.items():
        if p.blade_piece and iid in placements:
            by_blade[p.blade_piece].append(iid)

    # A blade that backs one handed and two handed weapons is never priced by tier: its weakest
    # wearer would set a target the other class cannot live with. It is lowered only when a weapon
    # on it breaks its CLASS limit, and only as far as that limit needs.
    mixed = {m.blade for m in mixed_blades(priced) if not ladder.is_hero_blade(m.blade, spec)}

    plans: list[BladePlan] = []
    notes: list[str] = []
    for blade in sorted(mixed):
        items = all_on_blade[blade]
        if not breaks_a_limit(items, priced, spec):
            continue
        plan = lower_to_limit(blade, items, priced, cat, spec, placements, "(class limit)", notes)
        if plan is None:
            continue
        plans.append(plan)
        bound = min(items, key=lambda i: (min(class_ratios(priced[i], spec)), i))
        notes.append(
            f"{blade}: one handed and two handed weapons share it, so it is not priced by tier; "
            f"lowered only to the class limit of {bound} ({priced[bound].item.template})"
        )

    for blade, items in sorted(by_blade.items()):
        if ladder.is_hero_blade(blade, spec) or blade in mixed:
            continue
        if exempt_only_blade(items, placements, spec):
            notes.append(f"{blade}: only ladder-exempt troops carry it, left alone")
            continue
        # The weapons a non-exempt troop carries decide the TARGET and the primary attack; every
        # weapon on the blade decides the ratio and the limits.
        owned = [i for i in items if contexts[i]]
        # Price the blade for the weakest non-exempt carrier of any weapon on it, by its own tier
        # and craft row (M2): the first troop loaded no longer decides either.
        worst = None
        for iid in owned:
            for tier, craft, who, shielded in contexts[iid]:
                if cultures and craft not in cultures:
                    continue
                v = as_wielded(priced[iid], shielded)
                flags = ladder.dominant_flags(v.class_template, v.swing_damage, v.thrust_damage,
                                              v.swing_dps, v.thrust_dps, spec)
                tgt = ladder.target(tier, craft, spec, v.class_template, *flags)
                if worst is None or tgt < worst[0]:
                    worst = (tgt, tier, craft, who, shielded)
        if worst is None:
            continue
        target_dps, anchor, culture, anchor_troop, anchor_shield = worst

        siblings = all_on_blade[blade]
        # The strongest worn weapon, as the anchor wields it, lands on the anchor's target.
        worn_best = max(owned, key=lambda i: as_wielded(priced[i], anchor_shield).best_dps)
        worn_dps = as_wielded(priced[worn_best], anchor_shield).best_dps
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
        # Nor past the tier limit of ANY carrier of ANY weapon on the blade: the class limit scaled
        # to the carrier's tier and craft row, so a one handed blade is never raised to an elite's
        # blow for a recruit.
        cap_s, cap_t = carrier_caps(siblings, priced, spec, contexts,
                                    (anchor, culture, anchor_troop, anchor_shield))
        # The weapon's primary attack sets the common multiplier. When a raise is held by the
        # primary's own limit, the other attack follows the primary's realized ratio and is never
        # raised on its own to chase a target the primary cannot reach: that raised the free thrust
        # again on every run. A primary that must come down does not drag the other attack
        # below the ratio the target asks of it.
        best = as_wielded(priced[worn_best], anchor_shield)
        primary = cap_s if best.swing_dps >= best.thrust_dps else cap_t
        common = max(1.0, min(ratio, primary.ratio)) if ratio > 1.0 else ratio
        ratio_s = min(common, cap_s.ratio)
        ratio_t = min(common, cap_t.ratio)
        piece = cat.pieces.get(blade)
        if piece is None or piece.blade is None:
            notes.append(f"{blade}: not a blade piece in the loaded Armory, skipped")
            continue
        old_s, old_t = piece.blade.swing_factor, piece.blade.thrust_factor
        # An attack the blade does not have has no factor to move and must not keep a plan alive.
        ratio_s = ratio_s if old_s > 0 else 1.0
        ratio_t = ratio_t if old_t > 0 else 1.0
        if ratio_s < ratio or ratio_t < ratio:
            target_dps = max(dps_after(priced[i], ratio_s, ratio_t) for i in siblings)
            held = [
                f"{name} held at the class limit set by {cap.by if r == cap.ratio else primary.by}"
                for name, r, cap, old in (("swing", ratio_s, cap_s, old_s), ("thrust", ratio_t, cap_t, old_t))
                if old > 0 and r < ratio
            ]
            notes.append(f"{blade}: " + "; ".join(held) + ", under its tier target")
        couched = [i for i in items if is_couchable(cat, priced[i].item)]
        if couched and max(ratio_s, ratio_t) > 1.0:
            # Raising is refused, lowering is not (see COUCH_FEATURES). A plan that raises one
            # attack and lowers the other keeps the lowering: dropping it whole left a couchable
            # spear's thrust above its limit.
            ratio_s, ratio_t = min(ratio_s, 1.0), min(ratio_t, 1.0)
            target_dps = max(dps_after(priced[i], ratio_s, ratio_t) for i in siblings)
            notes.append(
                f"{blade}: {couched[0]} can be couched, so its standing-thrust DPS understates "
                "it and RAISING it would multiply an already speed-amplified hit. Only lowered; "
                "price it by hand if it really is too weak"
            )
        new_s, new_t = old_s * ratio_s, old_t * ratio_t
        # Judge the move that gets written. Its factor is rounded down, which shrinks a small raise,
        # so a run on its own output finds the weapon inside the tolerance and plans nothing.
        if (within_tolerance(ladder.floor_factor(new_s) / old_s if old_s > 0 else 1.0,
                             max(priced[i].swing_damage for i in siblings))
                and within_tolerance(ladder.floor_factor(new_t) / old_t if old_t > 0 else 1.0,
                                     max(priced[i].thrust_damage for i in siblings))):
            continue
        clamped = ""
        for name, old, new in (("swing", old_s, new_s), ("thrust", old_t, new_t)):
            if old > 0 and clamp_factor(old, new) != new:
                clamped = f"{name} factor clamped into [{MIN_FACTOR}, {MAX_FACTOR}]"
        new_s = ladder.floor_factor(clamp_factor(old_s, new_s)) if old_s > 0 else old_s
        new_t = ladder.floor_factor(clamp_factor(old_t, new_t)) if old_t > 0 else old_t

        plans.append(
            BladePlan(
                blade=blade,
                items=sorted(siblings),
                anchor_tier=anchor,
                culture=culture,
                target_dps=target_dps,
                current_dps=current,
                old_swing=round(old_s, 2),
                new_swing=new_s,
                old_thrust=round(old_t, 2),
                new_thrust=new_t,
                wearers=sum(len(placements[i].wearers) for i in items),
                clamped=clamped,
            )
        )
    # Weapons no troop carries still reach the player through merchants and loot, so they get
    # no tier target but they do get the ceiling. Without this a 153 DPS two-handed axe sits in
    # every Rohan market, above anything vanilla fields, and no roster-driven pass ever sees it.
    ceiling = spec["ceiling"]
    unworn_over = {
        p.blade_piece for iid, p in priced.items()
        if iid not in placements and p.blade_piece
        and (p.best_dps > ceiling or min(class_ratios(p, spec)) < 1.0 - TOLERANCE)}
    skip = {p.blade for p in plans} | mixed
    for blade in sorted(unworn_over - skip):
        if ladder.is_hero_blade(blade, spec):
            continue
        # This pass only lowers: each factor comes down to the ceiling and to its own limits, and
        # the edit moves every weapon on the blade, so the plan covers them all.
        plan = lower_to_limit(blade, all_on_blade[blade], priced, cat, spec, placements, "(unworn)",
                              notes, ceiling)
        if plan is not None:
            plans.append(plan)
    # A hero blade has no tier target, but it has a limit: the class limit plus its maker's
    # bonus. One above it comes down to it, worn or not; one below is left alone, never raised.
    for blade, items in sorted(all_on_blade.items()):
        if not ladder.is_hero_blade(blade, spec) or not breaks_a_limit(items, priced, spec):
            continue
        plan = lower_to_limit(blade, items, priced, cat, spec, placements, "(hero)", notes)
        if plan is not None:
            plans.append(plan)

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
    ap.add_argument("--culture", action="append",
                    help="limit the tier pass to these craft rows (repeatable): a troop's "
                         "ladder.craft_key, which is its culture except for Black Numenoreans "
                         "(mordor_numenorean). It is not the engine culture id; "
                         "fix_melee_ladder.py takes that")
    ap.add_argument("--limits-only", action="store_true",
                    help="only lower weapons above a class or hero limit; raise nothing")
    ap.add_argument("--top", type=int, default=30)
    ap.add_argument("--pieces", default=str(PIECES_XML), help="crafting pieces XML to write")
    args = ap.parse_args(argv)
    if args.limits_only and args.culture:
        ap.error("--limits-only judges every blade; it cannot be combined with --culture")

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

    if args.limits_only:
        plans, notes = build_limit_plans(priced, cat, troops, spec)
    else:
        plans, notes = build_plans(priced, cat, troops, spec, cultures,
                                   frozenset(mc.load_shield_ids(MODULES)))

    print(f"melee blade restat (#631 pass 2{', limits only' if args.limits_only else ''}): "
          f"{len(priced)} weapons, {len(troops)} troops")
    if failures:
        print(f"  {len(failures)} weapon(s) could not be priced and were skipped")
    print(f"  {len(plans)} blade piece(s) to restat, reaching "
          f"{len({i for p in plans for i in p.items})} weapons and "
          f"{sum(p.wearers for p in plans)} troop slots")
    raised = [p for p in plans if p.new_swing > p.old_swing or p.new_thrust > p.old_thrust]
    print(f"  {len(raised)} raise a factor, {len(plans) - len(raised)} only lower")
    shared = [p for p in plans if len(p.items) > 1]
    print(f"  {len(shared)} of those blades back more than one weapon, so the edit moves all of them")
    clamped = [p for p in plans if p.clamped]
    if clamped:
        print(f"  {len(clamped)} hit the factor bounds and were clamped (listed below)")
    for n in notes[:10]:
        print(f"  note: {n}")
    mixed = mixed_blades(priced)
    if mixed:
        moved = {p.blade for p in plans}
        print(f"  {len(mixed)} blade(s) back both one handed and two handed weapons; none is priced "
              "by tier, so a later change can clone them:")
        for m in mixed:
            print(f"    {m.describe()}" + ("  [lowered to a class or hero limit]" if m.blade in moved else ""))
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
