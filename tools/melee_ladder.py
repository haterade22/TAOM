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

    return problems


def target(tier: int, culture: str, spec: dict) -> float:
    """The DPS a troop of this tier and culture should be armed at."""
    tier = max(0, min(int(tier), MAX_TIER))
    base = spec["curve"][str(tier)]
    shift = spec.get("kingdom_offset", {}).get(culture, 0.0)
    return base * (1.0 + shift)


def band(tier: int, culture: str, spec: dict) -> Band:
    value = target(tier, culture, spec)
    width = spec["band"]
    return Band(tier, culture, value, value * (1 - width), value * (1 + width))


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
        return (
            f"{self.troop} (tier {self.tier}, {self.culture}) is {direction}: its best melee "
            f"weapon {self.item} sustains {self.dps:.0f} DPS against a band of "
            f"{self.low:.0f} to {self.high:.0f}"
        )


def findings(troop_kits, spec: dict, min_gap: float = 0.0) -> list[Finding]:
    """Every troop armed outside its band, worst first.

    `troop_kits` is an iterable of (troop id, culture, tier, item id, dps, blade piece). The
    caller supplies it, so this stays a pure function over plain data and the gate, the report
    and the fixer can all feed it from whatever they already have loaded.

    Exempt troops are skipped; a weapon on a hero blade is skipped for the ceiling check only,
    because a hero weapon in a line troop's hands is still a ladder problem.
    """
    ceiling = spec["ceiling"]
    out: list[Finding] = []
    for troop_id, culture, tier, item, dps, blade in troop_kits:
        if is_exempt_troop(troop_id, spec):
            continue
        window = band(tier, culture, spec)
        verdict = window.verdict(dps)
        if verdict != "ok":
            finding = Finding(
                verdict, troop_id, culture, tier, item, dps,
                window.target, window.low, window.high,
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
