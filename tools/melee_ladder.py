#!/usr/bin/env python3
"""The melee ladder spec, and the pure functions the gate, the report and the fixer all share.

One source of truth, the way `tools/ranged_ladder.py` is for bows. If the validator's
`MELEE_LADDER_INVERSION` and `tools/fix_melee_ladder.py` computed "on curve" separately they
would drift, and the gate would start passing rosters the fixer had not actually fixed.

The unit is sustained damage (DPS), not one blow, because a lighter blade that swings faster
can out-damage a heavier one. `tools/melee_damage.py` derives it; `docs/features/melee-damage-
model.md` records how and what its limits are.

Read-only. Nothing here writes.
"""

from __future__ import annotations

import json
import math
from dataclasses import dataclass
from pathlib import Path

DEFAULT_SPEC = Path(__file__).resolve().parent / "melee_ladders.json"

MAX_TIER = 10


class LadderError(Exception):
    """The spec is missing, malformed, or self-contradicting."""


@dataclass(frozen=True)
class Band:
    """The DPS window a troop of a given tier and culture should be armed inside."""

    tier: int
    culture: str
    target: float
    low: float
    high: float

    def verdict(self, dps: float) -> str:
        if dps < self.low:
            return "under"
        if dps > self.high:
            return "over"
        return "ok"


def load_spec(path: Path | str = DEFAULT_SPEC) -> dict:
    path = Path(path)
    if not path.is_file():
        raise LadderError(f"missing melee ladder spec: {path}")
    try:
        spec = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise LadderError(f"{path} is not valid JSON: {exc}") from exc
    problems = validate_spec(spec)
    if problems:
        raise LadderError(f"{path} is self-contradicting: " + "; ".join(problems))
    return spec


def validate_spec(spec: dict) -> list[str]:
    """Everything wrong with a spec, as a list. Empty means usable.

    A spec that declares nothing, or that contradicts itself, must be an error rather than a
    quiet pass: a gate reading a broken spec checks nothing and reports clean.
    """
    problems: list[str] = []

    curve = spec.get("curve")
    if not isinstance(curve, dict) or not curve:
        return ["no `curve`"]

    tiers = []
    for key, value in curve.items():
        try:
            tier = int(key)
        except (TypeError, ValueError):
            problems.append(f"curve key {key!r} is not a tier number")
            continue
        if not 0 <= tier <= MAX_TIER:
            problems.append(f"curve tier {tier} is outside 0..{MAX_TIER}")
        if not isinstance(value, (int, float)) or value <= 0:
            problems.append(f"curve tier {tier} has a non-positive target {value!r}")
        tiers.append(tier)

    missing = [t for t in range(MAX_TIER + 1) if t not in tiers]
    if missing:
        problems.append(
            "curve has no target for tier(s) " + ", ".join(str(t) for t in missing)
        )

    # The whole point of the spec is that the ladder climbs. A curve that does not is the
    # defect it was written to fix.
    ordered = [curve[str(t)] for t in sorted(tiers) if str(t) in curve]
    for lower, higher in zip(ordered, ordered[1:]):
        if higher <= lower:
            problems.append(f"curve is not strictly increasing ({lower} then {higher})")
            break

    band = spec.get("band")
    if not isinstance(band, (int, float)) or not 0 < band < 1:
        problems.append(f"`band` must be a fraction between 0 and 1, got {band!r}")

    anchor = spec.get("anchor_tier")
    if anchor is not None and (not isinstance(anchor, int) or not 0 <= anchor <= MAX_TIER):
        problems.append(f"`anchor_tier` {anchor!r} is outside 0..{MAX_TIER}")

    ceiling = spec.get("ceiling")
    if not isinstance(ceiling, (int, float)) or ceiling <= 0:
        problems.append(f"`ceiling` must be a positive number, got {ceiling!r}")
    elif ordered and ceiling < max(ordered):
        problems.append(
            f"`ceiling` {ceiling} is below the top of the curve {max(ordered)}, so every "
            "capstone weapon would breach it by construction"
        )

    offsets = spec.get("kingdom_offset", {})
    if not isinstance(offsets, dict):
        problems.append("`kingdom_offset` must be a mapping")
    else:
        for culture, shift in offsets.items():
            if not isinstance(shift, (int, float)) or not -0.5 < shift < 0.5:
                problems.append(f"kingdom_offset[{culture!r}] {shift!r} is not a sane fraction")

    for key in ("hero_blades",):
        if not isinstance(spec.get(key), list):
            problems.append(f"`{key}` must be a list")

    if not isinstance(spec.get("exempt_troops", {}), dict):
        problems.append("`exempt_troops` must be a mapping of troop id to reason")

    limits = spec.get("class_limits", {})
    if not isinstance(limits, dict):
        problems.append("`class_limits` must be a mapping of crafting template to limits")
    else:
        for template, limit in limits.items():
            if not isinstance(limit, dict):
                problems.append(f"class_limits[{template!r}] must be a mapping")
                continue
            for stat in LIMIT_STATS:
                if stat not in limit:
                    problems.append(f"class_limits[{template!r}] has no `{stat}` (use null for none)")
                    continue
                value = limit[stat]
                if value is not None and (not isinstance(value, (int, float)) or value <= 0):
                    problems.append(f"class_limits[{template!r}].{stat} {value!r} is not positive")

    problems += _validate_craft(spec)
    problems += _validate_tier_fraction(spec)
    problems += _validate_hero_table(spec)
    return problems


def _validate_craft(spec: dict) -> list[str]:
    """The per-class target keys. Absent altogether is the legacy spec; half present is a mistake."""
    present = [key for key in CRAFT_KEYS if key in spec]
    if not present:
        return []
    problems = [f"`{key}` is missing while {', '.join(present)} is set" for key in CRAFT_KEYS
                if key not in spec]

    def check_row(label: str, row) -> None:
        if not isinstance(row, dict):
            problems.append(f"{label} must be a mapping of {', '.join(CRAFT_COLUMNS)} to a factor")
            return
        for column in CRAFT_COLUMNS:
            value = row.get(column)
            if not isinstance(value, (int, float)) or not 0 < value <= 1:
                problems.append(f"{label} `{column}` {value!r} must be a factor in (0, 1]")

    if "craft_default" in spec:
        check_row("craft_default", spec["craft_default"])
    if isinstance(spec.get("craft"), dict):
        for culture, row in spec["craft"].items():
            check_row(f"craft[{culture!r}]", row)
    elif "craft" in spec:
        problems.append("`craft` must be a mapping of culture id to its columns")

    columns = spec.get("craft_column")
    if isinstance(columns, dict):
        for template, column in columns.items():
            if column not in CRAFT_COLUMNS:
                problems.append(f"craft_column[{template!r}] {column!r} is not one of {CRAFT_COLUMNS}")
        for template in spec.get("class_limits", {}):
            if template not in columns:
                problems.append(f"craft_column has no column for the class-limited template {template!r}")
    elif "craft_column" in spec:
        problems.append("`craft_column` must be a mapping of crafting template to a column")

    if "class_band" in spec:
        width = spec["class_band"]
        if not isinstance(width, (int, float)) or not 0 < width < 1:
            problems.append(f"`class_band` must be a fraction between 0 and 1, got {width!r}")
    if "under_tolerance_tiers" in spec:
        tol = spec["under_tolerance_tiers"]
        if isinstance(tol, bool) or not isinstance(tol, int) or not 0 <= tol <= 3:
            problems.append(f"`under_tolerance_tiers` must be a whole number from 0 to 3, got {tol!r}")
    return problems


def _validate_tier_fraction(spec: dict) -> list[str]:
    """`class_tier_fraction` and `craft_troop_prefix`: both optional, both checked when present."""
    problems: list[str] = []
    if "class_tier_fraction" in spec:
        fraction = spec["class_tier_fraction"]
        if not isinstance(fraction, dict):
            problems.append("`class_tier_fraction` must be a mapping of tier to a share of the class top")
        else:
            ordered = []
            for tier in range(MAX_TIER + 1):
                value = fraction.get(str(tier))
                if not isinstance(value, (int, float)) or isinstance(value, bool) or not 0 < value <= 1:
                    problems.append(
                        f"class_tier_fraction tier {tier} {value!r} must be a share in (0, 1]")
                else:
                    ordered.append(value)
            for key in fraction:
                if not str(key).isdigit() or int(key) > MAX_TIER:
                    problems.append(f"class_tier_fraction key {key!r} is not a tier in 0..{MAX_TIER}")
            if len(ordered) == MAX_TIER + 1:
                for lower, higher in zip(ordered, ordered[1:]):
                    if higher <= lower:
                        problems.append(
                            f"class_tier_fraction is not strictly increasing ({lower} then {higher})")
                        break
                if ordered[-1] != 1.0:
                    problems.append(
                        f"class_tier_fraction must end at 1.0 on tier {MAX_TIER} so the best craft "
                        f"reaches the class top, got {ordered[-1]!r}")
    if "craft_troop_prefix" in spec:
        prefixes = spec["craft_troop_prefix"]
        if not isinstance(prefixes, dict):
            problems.append("`craft_troop_prefix` must be a mapping of troop id prefix to a craft row")
        else:
            rows = spec.get("craft", {})
            for prefix, key in prefixes.items():
                if key not in rows:
                    problems.append(f"craft_troop_prefix[{prefix!r}] names {key!r}, which `craft` lacks")
    return problems


def _validate_hero_table(spec: dict) -> list[str]:
    makers, bonus = spec.get("hero_makers"), spec.get("hero_bonus")
    if makers is None and bonus is None:
        return []
    if not isinstance(makers, dict) or not isinstance(bonus, dict):
        return ["`hero_makers` and `hero_bonus` must both be mappings"]
    problems = []
    for group, value in bonus.items():
        if not isinstance(value, (int, float)) or value <= 0:
            problems.append(f"hero_bonus[{group!r}] {value!r} is not positive")
    if "other" not in bonus:
        problems.append("`hero_bonus` has no `other` group, which a hero blade with no maker takes")
    for blade, group in makers.items():
        if blade not in spec.get("hero_blades", []):
            problems.append(f"hero_makers names {blade!r}, which is not in `hero_blades`")
        if group not in bonus:
            problems.append(f"hero_makers[{blade!r}] names group {group!r}, which `hero_bonus` lacks")
    return problems


# --- class limits: one blow and sustained damage, per crafting template --------------------

LIMIT_STATS = ("swing", "thrust", "swing_dps", "thrust_dps")

# The per-class tier target. `craft` replaces `kingdom_offset` wherever a template
# is known, and `class_band` replaces `band`. A spec with none of these keeps the legacy target.
CRAFT_KEYS = ("craft", "craft_default", "craft_column", "class_band")
CRAFT_COLUMNS = ("sword", "axe", "polearm")


def class_limit(template: str, spec: dict) -> dict | None:
    """The limits of a crafting template, or None when the spec has none for it."""
    return spec.get("class_limits", {}).get(template)


def max_factor(limit: dict | None, swing: int, thrust: int,
               swing_dps: float, thrust_dps: float) -> float:
    """The largest `damage_factor` multiplier that keeps a weapon inside its class limits.

    Blow and DPS are both linear in `damage_factor` (Crafting.cs:374, :380), so each limit is a
    ratio and the smallest one binds. `swing` and `thrust` are displayed damage, already rounded
    to a whole number, so the blow ratios keep half a point on each side: the unrounded blow
    then cannot display above the limit. DPS moves in steps of one displayed damage point, so
    its cap is the largest whole damage inside the DPS limit, with the same half point. A stat
    of 0 is an attack the weapon does not have.
    """
    if not limit:
        return float("inf")
    caps = []
    for stat, value in (("swing", swing), ("thrust", thrust)):
        if value > 0 and limit.get(stat):
            caps.append((limit[stat] - 0.5) / (value + 0.5))
    for stat, value, damage in (("swing_dps", swing_dps, swing), ("thrust_dps", thrust_dps, thrust)):
        if value > 0 and damage > 0 and limit.get(stat):
            top = math.floor(limit[stat] * damage / value + 1e-9)
            caps.append((top + 0.5 - 1e-9) / (damage + 0.5))
    return min(caps) if caps else float("inf")


def floor_factor(value: float) -> float:
    """`value` rounded DOWN to the two decimals a `damage_factor` keeps, never up.

    A factor rounded to nearest can end a weapon above the limit that set it. The epsilon keeps
    3.52 from reading 3.51 after float error.
    """
    return math.floor(value * 100 + 1e-9) / 100


def limit_breaches(limit: dict | None, swing: int, thrust: int,
                   swing_dps: float, thrust_dps: float) -> list[tuple[str, float, float]]:
    """(stat, value, limit) for each stat above its class limit, in LIMIT_STATS order."""
    if not limit:
        return []
    values = dict(zip(LIMIT_STATS, (swing, thrust, swing_dps, thrust_dps)))
    # DPS is a float the reports print rounded; half a point of slack keeps the gate and the
    # printed number in agreement.
    slack = {"swing": 0.0, "thrust": 0.0, "swing_dps": 0.5, "thrust_dps": 0.5}
    return [
        (stat, values[stat], limit[stat])
        for stat in LIMIT_STATS
        if limit.get(stat) and values[stat] > limit[stat] + slack[stat]
    ]


def hero_group(blade: str, spec: dict) -> str:
    """The maker group of a hero blade; a blade with no entry is `other`."""
    return spec.get("hero_makers", {}).get(blade, "other")


def hero_limit(blade: str, template: str, spec: dict) -> dict | None:
    """The class limit of a hero blade: each DPS raised by the group's bonus, each blow in proportion.

    One handed sword at +10: 83 DPS becomes 93 and the 95 swing becomes 106. A null limit stays
    null. None when the template has no class limit.
    """
    limit = class_limit(template, spec)
    if not limit:
        return None
    bonus = spec.get("hero_bonus", {}).get(hero_group(blade, spec), 0)
    out = dict(limit)
    for blow, dps in (("swing", "swing_dps"), ("thrust", "thrust_dps")):
        if limit.get(dps):
            out[dps] = limit[dps] + bonus
            if limit.get(blow):
                out[blow] = round(limit[blow] * out[dps] / limit[dps])
    return out


def limit_for(blade: str, template: str, spec: dict, tier: int | None = None,
              craft: str | None = None) -> dict | None:
    """The limit a weapon on this blade is held to.

    A hero blade takes its hero limit at every tier. Any other blade takes the tier limit of the
    carrier's `tier` and `craft` row when a tier is given, else the plain class limit.
    """
    if is_hero_blade(blade, spec):
        return hero_limit(blade, template, spec)
    if tier is not None:
        return tier_limit(template, tier, craft, spec)
    return class_limit(template, spec)


def class_top(template: str, spec: dict, swings: bool = True, thrusts: bool = True) -> float | None:
    """The DPS a tier-10 weapon of this class reaches: the larger usable attack's DPS limit.

    A thrust-only spear of template TwoHandedPolearm therefore tops at the thrust DPS (45), a
    swinging polearm at the swing DPS (138). None when the class has no usable limit.
    """
    limit = class_limit(template, spec) or {}
    tops = [limit[stat] for stat, can in (("swing_dps", swings), ("thrust_dps", thrusts))
            if can and limit.get(stat)]
    return max(tops) if tops else None


def tier_share(tier: int, spec: dict) -> float:
    """The share of a class top that a tier may reach.

    `class_tier_fraction` when the spec has it, else the shared curve's own shape,
    curve[tier] / curve[10], which is what the per-class target used before.
    """
    tier = max(0, min(int(tier), MAX_TIER))
    fraction = spec.get("class_tier_fraction")
    if fraction:
        return fraction[str(tier)]
    return spec["curve"][str(tier)] / spec["curve"][str(MAX_TIER)]


def craft_key(troop_id: str, culture: str, spec: dict) -> str:
    """The `craft` row a troop is rated by: a troop id prefix first, else its culture.

    TAOM files the Black Numenoreans under the mordor culture, but their smithing is not orc
    smithing. `craft_troop_prefix` lets the id prefix pick the row, so the culture id stays what
    the engine reads. Every caller that prices a troop's weapon must route through this.
    """
    for prefix, key in spec.get("craft_troop_prefix", {}).items():
        if troop_id.startswith(prefix):
            return key
    return culture


def dominant_flags(template: str, swing_damage: float, thrust_damage: float,
                   swing_dps: float, thrust_dps: float, spec: dict) -> tuple[bool, bool]:
    """(swings, thrusts) as `target`, `band` and `class_top` should read a weapon's attacks.

    A polearm that can do both takes its class top from the swing only when its swing DPS is at
    least its thrust DPS. One whose thrust DPS is higher is a spear in the hand, and pricing it
    from the 138 DPS halberd swing would arm a spear at three times the thrust it can use. Every
    caller (restat, fixer, placeholders, validator) calls this one function so they cannot drift.
    """
    swings, thrusts = swing_damage > 0, thrust_damage > 0
    if (swings and thrusts and spec.get("craft_column", {}).get(template) == "polearm"
            and thrust_dps > swing_dps):
        swings = False
    return swings, thrusts


def _tier_factor(template: str | None, tier: int, culture: str, spec: dict) -> float | None:
    """Tier share x the culture's craft for the template's column, or None without a per-class target.

    `tier_limit` and `_class_target` both multiply by this one number, so the DPS limit of a
    swinging weapon equals its DPS target exactly and a restat lands on it.
    """
    column = spec.get("craft_column", {}).get(template)
    if column is None or any(key not in spec for key in CRAFT_KEYS):
        return None
    return tier_share(tier, spec) * spec["craft"].get(culture, spec["craft_default"])[column]


def tier_limit(template: str, tier: int, culture: str, spec: dict) -> dict | None:
    """The class limit scaled to a tier: class limit x tier share x the culture's craft.

    The class limits are the TIER 10 numbers. A tier 3 troop's weapon is held to the share of
    them that its tier earns, so a blow that only an elite may swing cannot sit in a recruit's
    hand. A hero blade does not use this, see `limit_for`. Without a per-class target for the
    template the plain class limit is returned.
    """
    limit = class_limit(template, spec)
    factor = _tier_factor(template, tier, culture, spec)
    if not limit or factor is None:
        return limit
    return {stat: (value * factor if value else value) for stat, value in limit.items()}


def _class_target(tier: int, culture: str, spec: dict, template: str | None,
                  swings: bool, thrusts: bool) -> float | None:
    """The per-class target, or None when the legacy one applies."""
    factor = _tier_factor(template, tier, culture, spec)
    top = class_top(template, spec, swings, thrusts) if template is not None else None
    if factor is None or top is None:
        return None
    return top * factor


def target(tier: int, culture: str, spec: dict, template: str | None = None,
           swings: bool = True, thrusts: bool = True) -> float:
    """The DPS a troop of this tier and culture should be armed at.

    With a template that has a class limit, the target is the class's own curve: the class top
    times the tier's share (`class_tier_fraction`) times the culture's craft for the template's
    column. That spreads each class over all ten tiers; the shared curve passed the one handed
    limits near tier 6. Without one it is the shared curve times `kingdom_offset`.
    """
    tier = max(0, min(int(tier), MAX_TIER))
    per_class = _class_target(tier, culture, spec, template, swings, thrusts)
    if per_class is not None:
        return per_class
    base = spec["curve"][str(tier)]
    shift = spec.get("kingdom_offset", {}).get(culture, 0.0)
    return base * (1.0 + shift)


def band(tier: int, culture: str, spec: dict, template: str | None = None,
         swings: bool = True, thrusts: bool = True) -> Band:
    value = _class_target(max(0, min(int(tier), MAX_TIER)), culture, spec, template, swings, thrusts)
    if value is None:
        value = target(tier, culture, spec)
        width = spec["band"]
    else:
        width = spec["class_band"]
    return Band(tier, culture, value, value * (1 - width), value * (1 + width))


def judge(tier: int, culture: str, spec: dict, dps: float, template: str | None = None,
          swings: bool = True, thrusts: bool = True) -> str:
    """"ok", "under" or "over" for a weapon of `dps` in a troop of `tier`.

    A weapon under its carrier's band is still "ok" when it sits inside the band of a tier up to
    `under_tolerance_tiers` below: weapon skill adds damage and attack speed every tier, so a troop
    one or two tiers up already fights harder with the same weapon (melee-damage-model.md, "Tier
    tolerance"). Over is never excused.
    """
    verdict = band(tier, culture, spec, template, swings, thrusts).verdict(dps)
    if verdict != "under":
        return verdict
    return "ok" if dps >= tolerance_floor(tier, culture, spec, template, swings, thrusts) else "under"


def tolerance_floor(tier: int, culture: str, spec: dict, template: str | None = None,
                    swings: bool = True, thrusts: bool = True) -> float:
    """The lowest DPS `judge` accepts for a troop of `tier`: the lowest band floor from the
    carrier's tier down `under_tolerance_tiers` tiers."""
    lowest = max(0, int(tier) - spec.get("under_tolerance_tiers", 0))
    return min(band(t, culture, spec, template, swings, thrusts).low
               for t in range(lowest, int(tier) + 1))


def is_exempt_troop(troop_id: str, spec: dict) -> bool:
    return troop_id in spec.get("exempt_troops", {})


def is_hero_blade(blade_piece: str, spec: dict) -> bool:
    return blade_piece in set(spec.get("hero_blades", []))


def stale_exemptions(troop_ids: set[str], spec: dict) -> list[str]:
    """Exempt troops that no longer exist.

    A stale allowlist entry is invisible: the gate keeps skipping a troop that is gone while
    reporting clean. Same reasoning as `_LANDLESS_BY_DESIGN` and friends in the validator.
    """
    return sorted(t for t in spec.get("exempt_troops", {}) if t not in troop_ids)


@dataclass(frozen=True)
class Finding:
    """One troop armed outside its band, or one weapon over the ceiling."""

    kind: str  # "under" | "over" | "ceiling"
    troop: str
    culture: str
    tier: int
    item: str
    dps: float
    target: float
    low: float
    high: float
    # The lowest DPS judge accepts (tolerance_floor); set on an "under" finding only.
    floor: float | None = None

    @property
    def gap(self) -> float:
        """How far outside the band it sits, in DPS. Always positive."""
        if self.kind == "under":
            return self.low - self.dps
        if self.kind == "over":
            return self.dps - self.high
        return self.dps - self.high

    def describe(self) -> str:
        if self.kind == "ceiling":
            return (
                f"{self.item} sustains {self.dps:.0f} DPS, above the ladder ceiling "
                f"{self.high:.0f}; reachable by {self.troop}"
            )
        direction = "under-armed" if self.kind == "under" else "over-armed"
        floor = (f"; accepted from {self.floor:.0f} (the tier tolerance)"
                 if self.floor is not None and self.floor < self.low else "")
        return (
            f"{self.troop} (tier {self.tier}, {self.culture}) is {direction}: its best melee "
            f"weapon {self.item} sustains {self.dps:.0f} DPS against a band of "
            f"{self.low:.0f} to {self.high:.0f}{floor}"
        )


def findings(troop_kits, spec: dict, min_gap: float = 0.0) -> list[Finding]:
    """Every troop armed outside its band, worst first.

    `troop_kits` is an iterable of (troop id, culture, tier, item id, dps, blade piece), which
    may carry (crafting template, swings, thrusts) after it. With them the troop is judged by its
    weapon class's own band; the short row keeps the shared curve. The caller supplies the rows,
    so this stays a pure function over plain data and the gate, the report and the fixer can
    all feed it from whatever they already have loaded.

    Exempt troops are skipped; a weapon on a hero blade is skipped for the ceiling check only,
    because a hero weapon in a line troop's hands is still a ladder problem.
    """
    ceiling = spec["ceiling"]
    out: list[Finding] = []
    for troop_id, culture, tier, item, dps, blade, *kind in troop_kits:
        if is_exempt_troop(troop_id, spec):
            continue
        template, swings, thrusts = kind or (None, True, True)
        window = band(tier, culture, spec, template, swings, thrusts)
        verdict = judge(tier, culture, spec, dps, template, swings, thrusts)
        if verdict != "ok":
            finding = Finding(
                verdict, troop_id, culture, tier, item, dps,
                window.target, window.low, window.high,
                tolerance_floor(tier, culture, spec, template, swings, thrusts)
                if verdict == "under" else None,
            )
            if finding.gap >= min_gap:
                out.append(finding)
        if dps > ceiling and not is_hero_blade(blade, spec):
            out.append(
                Finding("ceiling", troop_id, culture, tier, item, dps,
                        ceiling, ceiling, ceiling)
            )
    out.sort(key=lambda f: -f.gap)
    return out


@dataclass(frozen=True)
class BlowFinding:
    """One weapon above its limits, and the troops that carry it."""

    item: str
    template: str
    breaches: tuple[tuple[str, float, float], ...]
    troops: tuple[str, ...]
    hero: bool = False
    tiered: bool = False

    def describe(self) -> str:
        what = ", ".join(f"{stat.replace('_', ' ')} {value:.0f} above {top:g}"
                         for stat, value, top in self.breaches)
        shown = ", ".join(self.troops[:4]) + (f" and {len(self.troops) - 4} more"
                                              if len(self.troops) > 4 else "")
        which = "hero limit" if self.hero else ("tier limit" if self.tiered else "class limit")
        return (f"{self.item} is over the {which} of {self.template}: {what}; "
                f"carried by {shown}")


def blow_findings(carried, spec: dict) -> list[BlowFinding]:
    """Every carried weapon above its limits, once each, worst blow first.

    `carried` is an iterable of (troop id, item id, template, blade piece, swing, thrust,
    swing dps, thrust dps), which may end with (carrier tier, carrier craft key). With the tier
    and craft key an ordinary weapon is judged against `tier_limit` for that carrier; without
    them against the plain class limit. A hero blade is judged against its hero limit
    (`limit_for`) either way. Ladder-exempt troops are skipped, as they are for the ceiling.

    A weapon carried at several tiers is reported once, with the breach of the carrier it
    overshoots most; every troop that breaches is listed.
    """
    seen: dict[str, tuple] = {}
    troops: dict[str, set[str]] = {}
    for troop_id, item, template, blade, swing, thrust, swing_dps, thrust_dps, *carrier in carried:
        if is_exempt_troop(troop_id, spec):
            continue
        hero = is_hero_blade(blade, spec)
        tiered = bool(carrier) and not hero
        limit = limit_for(blade, template, spec, *carrier)
        over = limit_breaches(limit, swing, thrust, swing_dps, thrust_dps)
        if not over:
            continue
        worst = max(v / top for _s, v, top in over)
        if item not in seen or worst > seen[item][0]:
            seen[item] = (worst, template, tuple(over), hero, tiered)
        troops.setdefault(item, set()).add(troop_id)
    out = [BlowFinding(item, template, over, tuple(sorted(troops[item])), hero, tiered)
           for item, (_worst, template, over, hero, tiered) in seen.items()]
    out.sort(key=lambda f: (-max(v / top for _s, v, top in f.breaches), f.item))
    return out
