#!/usr/bin/env python3
"""Retarget bandit and caravan party templates onto a troop-POWER budget.

Why power and not headcount
---------------------------
The map AI decides whether a party runs by comparing `PartyBase.EstimatedStrength`, which is
`sum(healthy * GetDefaultTroopPower(troop)) * moraleFactor`, not by counting bodies. Raider
cultures differ sharply in troop tier, so a flat `max_value` produces wildly uneven warbands:
at 20 per stack the eight raider templates span 64.4 to 112.4 power, a 1.75x spread. Balancing
them means solving for power.

The flee decision is a step function, which is why this has to be precise. In
`DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy` the avoid term is

    num4 = ClampFloat((L < 1) ? ClampFloat(1/L, 0.05, 3) : 0, 0.05, 3)

so at L >= 1 it collapses to the 0.05 floor and `avoidScore` can never reach the 1.0 the engine
needs to switch behaviour. Below 1 it saturates at 3 almost immediately. A caravan that is
slightly stronger than the warband keeps trading; one that is slightly weaker runs, and one that
is eight times weaker runs no harder. Partial increases buy nothing; crossing 1.0 buys everything.

The power model
---------------
Tier is `clamp(ceil((level - 5) / 5), 0, MaxCharacterTier)` (`DefaultCharacterStatsModel.GetTier`),
a pure function of the `level=` attribute. A missing `level=` deserializes to 1
(`BasicCharacterObject.Deserialize`), which is tier 0.

`TaomMilitaryPowerModel` is in the path with `EnableCustomTroopPower = true` shipped. It keeps
vanilla's `(2 + tier) * (10 + tier) * 0.02` for tiers 0-6 (`OverrideVanillaTierPower = false`)
and applies a `MountedMultiplier` of 1.2 that vanilla does not have.

Two couplings to know before trusting this tool's numbers, both currently latent:

  - Tiers 7-10 do NOT come from `configs/battle_balance_config.json` at runtime.
    `CalculateTierPower`'s `tier >= 7` arm switches on `TaomSettings.Tier7Power..Tier10Power`,
    which are MCM-settable, and its `_ => config.GetTierPower(tier)` default is unreachable
    because `Tier` is clamped to `[0, 10]`. This tool reads the JSON, whose T7-T10 happen to
    equal those compiled defaults. No troop in the 50 templates it manages exceeds tier 5, so
    nothing is wrong today, but a player who moves a tier slider diverges from this tool.
  - `MOUNTED_MULTIPLIER` below is hardcoded, while the engine reads the settable
    `TaomSettings.MountedMultiplier`. They agree only by its compiled default. This one is
    live: the Gundabad, Harad and Rhun raider rosters do carry mounted troops. For a non-hero troop `IsMounted` is `_isMounted`, assigned from
`DefaultFormationClass.IsMounted()` in `Deserialize`, so it is decided purely by `default_group`.

Usage:
    python tools/rebalance_template_power.py                    # dry-run (default)
    python tools/rebalance_template_power.py --apply
    python tools/rebalance_template_power.py --scope bandits    # bandits | caravans | all
"""

import argparse
import json
import math
import re
import sys
import xml.etree.ElementTree as ET
from collections import OrderedDict
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
MODULE_DATA = REPO / "Main" / "_Module" / "ModuleData"
TARGET_FILE = MODULE_DATA / "taom_partyTemplates.xml"
BATTLE_BALANCE = MODULE_DATA / "configs" / "battle_balance_config.json"

MAX_CHARACTER_TIER = 10
MOUNTED_MULTIPLIER = 1.2  # TaomSettings.MountedMultiplier
MOUNTED_GROUPS = {"Cavalry", "HorseArcher"}

# Power budgets, in EstimatedStrength units.
#
# raider 78 puts every warband at 76-79 power, which falls out at 56-80 bodies depending on the
# culture's tier mix. That is the "around 80" the balance decision asked for, stated in the unit
# the engine actually compares.
#
# The caravan floors are the point of the whole exercise: `floor_power` is the power of the
# WEAKEST roster the template can spawn, so every caravan clears the strongest warband even on an
# unlucky draw. 94 is 1.2x the raider budget, and the 20% margin is deliberate headroom for the
# morale term (`MBMath.Map(Morale, 20, 40, 0.7, 1)`) which scales each side independently and can
# move the comparison by up to 30%.
# `min_frac` is the bandit floor, as a fraction of the template's own max SUM. Vanilla gives a
# land bandit party the ratio `(0.4 + 0.8 * PlayerProgress) * U(0.2, 0.8)`, so at PlayerProgress 0
# it spans only 0.08 to 0.32; with `spawn = min + (max - min) * r` the early game is therefore a
# narrow band sitting just above `min`. Cutting the ceiling from 200 to ~78 power squeezed that
# band twice over, so the floor comes down with it. This cannot restore the old spread, which
# needs the old ceiling, but it puts the early floor back in vanilla territory (vanilla looters
# run 4 to 36) instead of pinning every early warband near 21 men.
DEFAULT_BUDGETS = {
    "raider": {"power": 78.0, "min_frac": 0.125},
    "boss": {"power": 105.0, "min_frac": 0.125},
    "caravan": {"floor_power": 94.0, "spread": 0.15},
    "elite_caravan": {"floor_power": 110.0, "spread": 0.15},
}

# The composition every caravan template resolves to, given as relative weights (the tool then
# scales them onto the power budget). Fourteen of the seventeen cultures already ship exactly
# these numbers; Rohan, Dale and Dunland carry a 1/1 armed_trader stack instead of 12/15 and are
# therefore about half the bodies for no recorded reason. Normalising here rather than scaling
# each template's own shape means the asymmetry cannot survive a run, and cannot creep back in
# through a hand-edit, because the tool is the only thing that writes these numbers.
CANONICAL_CARAVAN_SHAPE = {
    "caravan": {"armed_trader": 15, "caravan_guard": 9, "veteran_caravan_guard": 5},
    "elite_caravan": {"armed_trader": 18, "caravan_guard": 14, "veteran_caravan_guard": 17},
}

# Longest first: "veteran_caravan_guard_rohan" also starts with nothing useful but ENDS with
# "caravan_guard_rohan", so a careless match classifies every veteran stack as a plain guard.
CARAVAN_ROLES = ("veteran_caravan_guard", "caravan_guard", "armed_trader")

BANDIT_KINDS = ("raider", "boss")
CARAVAN_KINDS = ("caravan", "elite_caravan")

# Both caravan patterns are fully anchored (^...$), so `caravan_template_` cannot swallow an
# `elite_caravan_template_` id regardless of the order they are tried in. The elite entry is
# listed first for readability only. Keep the anchors if you ever loosen these.
TEMPLATE_KIND_RES = (
    ("elite_caravan", re.compile(r"^elite_caravan_template_[a-z_]+$")),
    ("caravan", re.compile(r"^caravan_template_[a-z_]+$")),
    ("raider", re.compile(r"^[a-z_]+_raider_party_template$")),
    ("boss", re.compile(r"^[a-z_]+_boss_party_template$")),
)

TEMPLATE_OPEN_RE = re.compile(r'<MBPartyTemplate\s+id="([^"]+)"')
TEMPLATE_CLOSE_RE = re.compile(r"</MBPartyTemplate>")
STACK_RE = re.compile(
    r'<PartyTemplateStack\s+min_value="(\d+)"\s+max_value="(\d+)"\s+troop="NPCCharacter\.([^"]+)"'
)


def tier_for_level(level):
    """CharacterObject.Tier is clamp(ceil((level - 5) / 5), 0, MaxCharacterTier)."""
    return max(0, min(MAX_CHARACTER_TIER, math.ceil((level - 5) / 5)))


def load_power_table(config_path=None):
    """Replicate TaomMilitaryPowerModel.CalculateTierPower at the shipped settings.

    Tiers 0-6 use vanilla's closed form because `OverrideVanillaTierPower` ships false; tiers
    7-10 come from the config, where TAOM compresses them below vanilla.
    """
    path = Path(config_path) if config_path else BATTLE_BALANCE
    tier_power = json.loads(path.read_text(encoding="utf-8-sig"))["TroopPower"]["TierPower"]
    table = {}
    for tier in range(0, MAX_CHARACTER_TIER + 1):
        if tier >= 7:
            table[tier] = float(tier_power["T%d" % tier])
        else:
            table[tier] = (2.0 + tier) * (10.0 + tier) * 0.02
    return table


def _troop_xml_files():
    for sub in ("troops", "characters"):
        for path in sorted((MODULE_DATA / sub).glob("*.xml")):
            yield path


def load_troop_levels():
    """id -> level, read from the shipped troop and character XML."""
    levels = {}
    for path in _troop_xml_files():
        try:
            root = ET.parse(str(path)).getroot()
        except ET.ParseError:
            continue
        for node in root.iter("NPCCharacter"):
            tid = node.get("id")
            if not tid or tid in levels:
                continue
            # A missing level= deserializes to 1 in the engine, not 0.
            levels[tid] = int(node.get("level") or 1)
    return levels


def load_hero_troops():
    """Troop ids flagged `is_hero`, which the engine costs on a different curve entirely."""
    heroes = set()
    for path in _troop_xml_files():
        try:
            root = ET.parse(str(path)).getroot()
        except ET.ParseError:
            continue
        for node in root.iter("NPCCharacter"):
            tid = node.get("id")
            if tid and (node.get("is_hero") or "").lower() == "true":
                heroes.add(tid)
    return heroes


def load_mounted_troops():
    """The set of troop ids the engine treats as mounted, i.e. default_group decides it."""
    mounted = set()
    for path in _troop_xml_files():
        try:
            root = ET.parse(str(path)).getroot()
        except ET.ParseError:
            continue
        for node in root.iter("NPCCharacter"):
            tid = node.get("id")
            if tid and (node.get("default_group") or "") in MOUNTED_GROUPS:
                mounted.add(tid)
    return mounted


def troop_power(troop_id, levels, power_table, mounted=None, heroes=None):
    """GetDefaultTroopPower for one troop, or None when it cannot be costed.

    A hero-flagged troop returns None rather than a number. The engine costs a hero on a
    completely different curve (`TaomMilitaryPowerModel` uses `Level / 4 + 1` for the tier and
    a 1.5 hero multiplier, never the mounted one), so applying the troop formula to one would
    silently mis-budget its whole template. None of the 50 templates carries a hero today, and
    treating it as unresolved means a future edit that adds one gets the loud skip-and-report
    path instead of a quiet wrong answer.
    """
    if troop_id not in levels:
        return None
    if heroes and troop_id in heroes:
        return None
    power = power_table[tier_for_level(levels[troop_id])]
    if mounted and troop_id in mounted:
        power *= MOUNTED_MULTIPLIER
    return power


def power_of(counts, powers):
    """EstimatedStrength of a roster, before the morale factor."""
    return sum(c * p for c, p in zip(counts, powers))


def scale_to_power(shape, powers, budget, floors):
    """Scale `shape` so its power-weighted sum lands on `budget`, holding the shape's proportions.

    `floors` is the per-stack lower bound, normally the template's own min_value. A stack that
    carries any shape at all is additionally floored at 1: rounding one to zero deletes the troop
    from the template outright and no later retarget can restore it, because every future scale
    multiplies a spread that is now zero. That defect shipped on 2026-09-04 and removed six Black
    Numenorean troop types from fourteen Mordor templates. See
    docs/reference/party-template-sizing.md.
    """
    unit = power_of(shape, powers)
    if unit <= 0:
        raise ValueError(
            "cannot solve a roster with no power: shape=%r powers=%r. "
            "Every troop in it is tier 0, which usually means a missing level= attribute."
            % (list(shape), list(powers))
        )
    scale = budget / unit
    out = []
    for count, floor in zip(shape, floors):
        value = int(round(count * scale))
        hard_floor = max(floor, 1 if count > 0 else 0)
        out.append(max(value, hard_floor))
    return out


def solve_flat(mins, maxes, powers, budget):
    """Solve a bandit template for the single `max_value` its flat stacks share.

    Bandit templates are flat by construction: a raider is N stacks on one shared max, a boss is
    that plus a pinned `1/1` hero stack. Solving for the shared count makes the answer a function
    of the budget and the troop tiers alone, so it does not depend on what the previous run wrote.

    Scaling each stack from its own current value instead is not a fixed point. When the budget
    falls between two reachable values the tool oscillates: `gundabad_raiders_boss_party_template`
    flipped between 18 and 19 per stack on alternate runs, which is a silently churning diff.

    A stack with `min == max` is pinned and is returned untouched. A template whose unpinned
    stacks are NOT already uniform is refused rather than flattened, because flattening it would
    rewrite a composition somebody chose on purpose.
    """
    pinned = [i for i, (mn, mx) in enumerate(zip(mins, maxes)) if mn == mx]
    free = [i for i in range(len(maxes)) if i not in pinned]
    if not free:
        return list(maxes)

    distinct = {maxes[i] for i in free}
    if len(distinct) > 1:
        raise ValueError(
            "expected a flat bandit template but the unpinned stacks carry %r. Flattening it "
            "would rewrite a composition that was chosen deliberately; retune it by hand or "
            "give it its own budget." % sorted(distinct))

    pinned_power = sum(maxes[i] * powers[i] for i in pinned)
    free_power = sum(powers[i] for i in free)
    if free_power <= 0:
        raise ValueError(
            "cannot solve a bandit template whose free stacks have no power: powers=%r. "
            "Every troop in them is tier 0, which usually means a missing level= attribute."
            % list(powers))

    shared = int(round((budget - pinned_power) / free_power))
    # Never below a stack's own min_value: a max under its min drives the stack below its floor,
    # because the engine fills to `min + (max - min) * r`.
    shared = max(shared, 1, max(mins[i] for i in free))

    out = list(maxes)
    for i in free:
        out[i] = shared
    return out


def scale_mins(mins, maxes, min_frac):
    """Scale a bandit template's mins so their sum lands near `min_frac` of the max sum.

    Relative proportions are preserved, so the stack shape still carries the same meaning. Three
    guards, in priority order:

      - A stack already pinned (`min == max`) is returned untouched. That is the `1/1` boss hero
        stack, which must stay exactly one.
      - No stack drops below 1. A zero min is survivable in a way a zero MAX is not (a 0/0 stack
        is unspawnable and unrecoverable, which is the 2026-09-04 defect), but a stack that could
        always field a body should keep doing so.
      - No min exceeds its own max. The engine fills to `min + (max - min) * r`, so an inverted
        stack runs backwards from its own floor.
    """
    total_max = sum(maxes)
    target = max(1.0, total_max * min_frac)
    current = sum(mn for mn, mx in zip(mins, maxes) if mn != mx)
    if current <= 0:
        return list(mins)

    pinned_sum = sum(mn for mn, mx in zip(mins, maxes) if mn == mx)
    scale = max(0.0, target - pinned_sum) / current

    out = []
    for mn, mx in zip(mins, maxes):
        if mn == mx:
            out.append(mn)
            continue
        out.append(min(mx, max(1, int(round(mn * scale)))))
    return out


def solve_band(shape, powers, floor_power, spread):
    """Return (mins, maxes) for a caravan template.

    A caravan is solved as a band rather than a ceiling because the engine draws one uniform
    ratio per party and fills every stack to `min + (max - min) * r`
    (`FindAppropriateInitialRosterForMobileParty`). The MIN is therefore the roster an unlucky
    caravan actually gets, and it is the number that has to clear the warband, so `floor_power`
    is applied to the min and the max sits `spread` above it. Solving the midpoint instead would
    leave half of all caravans below the line and still parked.

    Deriving both ends from the same shape is what guarantees min <= max per stack.

    NOT a fixed point in isolation, unlike `solve_flat`. Feeding this function's own output
    back in as `shape` drifts the mins by one on some inputs, which is the same class of
    oscillation `solve_flat` was rewritten to eliminate. It is safe here only because
    `rewrite_text` always passes `canonical_shape_for(...)`, a fixed table keyed on troop ROLE
    and never on a stored count. **Never call this with a shape derived from the file.**
    """
    seed_floors = [1 if s > 0 else 0 for s in shape]
    mins = scale_to_power(shape, powers, floor_power, seed_floors)
    maxes = scale_to_power(shape, powers, floor_power * (1.0 + spread), mins)
    return mins, maxes


def caravan_role(troop_id):
    """Which of the three caravan roles a troop id fills, or None."""
    for role in CARAVAN_ROLES:
        if troop_id.startswith(role + "_"):
            return role
    return None


def canonical_shape_for(kind, troop_ids):
    """Relative weights for a caravan template's stacks, in the order they appear."""
    shape = []
    for troop_id in troop_ids:
        role = caravan_role(troop_id)
        if role is None or role not in CANONICAL_CARAVAN_SHAPE[kind]:
            raise ValueError(
                "%s is not one of the three caravan roles %r, so template kind %r cannot be "
                "normalised. Add the role deliberately rather than letting it count as zero."
                % (troop_id, list(CARAVAN_ROLES), kind))
        shape.append(CANONICAL_CARAVAN_SHAPE[kind][role])
    return shape


def kind_of(template_id):
    for kind, pattern in TEMPLATE_KIND_RES:
        if pattern.match(template_id):
            return kind
    return None


def _scoped_kinds(scope):
    if scope == "bandits":
        return set(BANDIT_KINDS)
    if scope == "caravans":
        return set(CARAVAN_KINDS)
    return set(BANDIT_KINDS) | set(CARAVAN_KINDS)


def rewrite_text(text, levels, power_table, budgets, mounted=None, scope="all", heroes=None):
    """Return (new_text, rows, unknown_troops).

    Line-oriented so the document's own byte shape survives: only the two numeric attributes on a
    stack line are touched, so the BOM, the indentation and every line ending are carried through
    untouched. The result is parsed before it is returned, because a transform that produces
    well-shaped nonsense is the failure mode this repo has actually shipped.
    """
    kinds = _scoped_kinds(scope)
    lines = text.splitlines(keepends=True)

    templates = OrderedDict()
    unknown = set()
    current = None
    for lineno, line in enumerate(lines):
        m_open = TEMPLATE_OPEN_RE.search(line)
        if m_open:
            tid = m_open.group(1)
            kind = kind_of(tid)
            current = tid if (kind in kinds) else None
            if current:
                templates[current] = {"kind": kind, "stacks": []}
            continue
        if TEMPLATE_CLOSE_RE.search(line):
            current = None
            continue
        if current and "<PartyTemplateStack" in line:
            m = STACK_RE.search(line)
            if not m:
                raise ValueError("line %d: unparsable stack in %s: %s"
                                 % (lineno + 1, current, line.strip()))
            templates[current]["stacks"].append(
                (lineno, int(m.group(1)), int(m.group(2)), m.group(3)))

    new_lines = list(lines)
    rows = []
    for tid, info in templates.items():
        stacks = info["stacks"]
        if not stacks:
            continue
        powers = [troop_power(s[3], levels, power_table, mounted, heroes) for s in stacks]
        missing = [s[3] for s, p in zip(stacks, powers) if p is None]
        if missing:
            # Counting an uncostable troop as zero power would quietly inflate the rest of the
            # template to cover a budget it was never meant to carry alone. Covers both an
            # unknown id and a hero-flagged one (see troop_power).
            unknown.update(missing)
            continue

        mins = [s[1] for s in stacks]
        maxes = [s[2] for s in stacks]
        budget = budgets[info["kind"]]

        if info["kind"] in CARAVAN_KINDS:
            # Normalised shape, not the template's own: see CANONICAL_CARAVAN_SHAPE.
            shape = canonical_shape_for(info["kind"], [s[3] for s in stacks])
            new_mins, new_maxes = solve_band(
                shape, powers, budget["floor_power"], budget["spread"])
        else:
            new_maxes = solve_flat(mins, maxes, powers, budget["power"])
            # Widen after the ceiling is known, because the floor is a fraction OF that ceiling.
            new_mins = scale_mins(mins, new_maxes, budget["min_frac"])

        changed = 0
        for (lineno, mn, mx, _troop), new_mn, new_mx in zip(stacks, new_mins, new_maxes):
            if new_mn == mn and new_mx == mx:
                continue
            line = new_lines[lineno]
            line = line.replace('min_value="%d"' % mn, 'min_value="%d"' % new_mn, 1)
            line = line.replace('max_value="%d"' % mx, 'max_value="%d"' % new_mx, 1)
            new_lines[lineno] = line
            changed += 1

        rows.append({
            "id": tid,
            "kind": info["kind"],
            "old_min_men": sum(mins), "old_max_men": sum(maxes),
            "new_min_men": sum(new_mins), "new_max_men": sum(new_maxes),
            "old_min_power": power_of(mins, powers), "old_max_power": power_of(maxes, powers),
            "new_min_power": power_of(new_mins, powers),
            "new_max_power": power_of(new_maxes, powers),
            "changed": changed,
        })

    out = "".join(new_lines)
    try:
        ET.fromstring(out.lstrip("﻿"))
    except ET.ParseError as exc:
        raise ValueError("transform produced XML that no longer parses: %s" % exc)
    return out, rows, sorted(unknown)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="write changes (default: dry-run)")
    parser.add_argument("--scope", choices=("bandits", "caravans", "all"), default="all")
    args = parser.parse_args()

    if not TARGET_FILE.exists():
        print("ERROR: target file not found: %s" % TARGET_FILE)
        return 1

    levels = load_troop_levels()
    if not levels:
        print("ERROR: the troop level index is empty. A renamed folder would do this, and every "
              "check below would then pass against nothing.")
        return 1
    mounted = load_mounted_troops()
    heroes = load_hero_troops()
    power_table = load_power_table()

    # Idiom B, binary round trip: the file carries a UTF-8 BOM and CRLF endings and both must
    # survive (.claude/rules/moduledata-validation.md, XML I/O convention).
    text = TARGET_FILE.read_bytes().decode("utf-8")
    new_text, rows, unknown = rewrite_text(
        text, levels, power_table, DEFAULT_BUDGETS, mounted, args.scope, heroes)

    print("%-46s %-14s %11s %11s %11s %11s %4s"
          % ("template", "kind", "men (old)", "men (new)", "pow (old)", "pow (new)", "chg"))
    for r in rows:
        print("%-46s %-14s %5d-%-5d %5d-%-5d %5.0f-%-5.0f %5.0f-%-5.0f %4d"
              % (r["id"], r["kind"], r["old_min_men"], r["old_max_men"],
                 r["new_min_men"], r["new_max_men"], r["old_min_power"], r["old_max_power"],
                 r["new_min_power"], r["new_max_power"], r["changed"]))

    print()
    print("templates retargeted: %d" % len(rows))
    print("stacks changed:       %d" % sum(r["changed"] for r in rows))
    if unknown:
        print()
        print("WARNING: %d troop id(s) could not be costed, so their templates were SKIPPED:"
              % len(unknown))
        for tid in unknown:
            print("  %s" % tid)
        print("An id here is either undefined or flagged is_hero. Either way the template it")
        print("belongs to was left untouched, which is why this run exits non-zero.")

    # Raiders only. A boss party is created by `BanditSpawnCampaignBehavior.AddBossParty`, which
    # calls `.Ai.DisableAi()` on it, so it sits in its hideout and never roams: a caravan cannot
    # meet one on the road, and including it here would understate the parity margin.
    worst_raider = max((r["new_max_power"] for r in rows if r["kind"] == "raider"),
                       default=0.0)
    weakest_caravan = min((r["new_min_power"] for r in rows if r["kind"] in CARAVAN_KINDS),
                          default=None)
    if weakest_caravan is not None and worst_raider:
        print()
        print("parity: weakest caravan roster %.1f vs strongest roaming warband %.1f  ->  L = %.2f"
              % (weakest_caravan, worst_raider, weakest_caravan / worst_raider))
        if weakest_caravan <= worst_raider:
            print("WARNING: a caravan can be weaker than a warband, so it will still flee.")

    print()
    if not args.apply:
        print("DRY-RUN: re-run with --apply to write.")
        return 0

    TARGET_FILE.write_bytes(new_text.encode("utf-8"))
    print("APPLIED: %s" % TARGET_FILE)
    # Non-zero when anything was skipped: a harness checking only the exit status must not
    # read "some templates were silently left alone" as a clean, complete run.
    return 2 if unknown else 0


if __name__ == "__main__":
    sys.exit(main())
