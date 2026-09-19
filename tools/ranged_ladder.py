#!/usr/bin/env python3
"""The ranged ladder: the one place that knows how an archer's reach, damage, accuracy and skill
are ordered (#582, per-tier and ranked since #617).

WHY
---
Three things decide what an arrow does, and all three were verified in the decompile:

- Reach is the launcher's `missile_speed` and nothing else. `Mission.OnAgentShootMissile` takes
  the wielded bow's `GetModifiedMissileSpeedForCurrentUsage()` as the launch speed and
  `SandboxAgentStatCalculateModel` pins `MissileSpeedMultiplier` at 1 for bows. The arrow's own
  `missile_speed` sets neither the launch speed nor its tier or price
  (`DefaultItemValueModel.CalculateAmmoTier` reads damage and stack size); managed code reads it
  only for the inventory tooltip and a tournament auto-resolve, and native receives it with the
  ammo's stats on every shot, use unverified. In a campaign mission each troop slot also rolls a
  random ItemModifier from the item's `modifier_group` (vanilla `bow`: none 45 of 102, splintered
  -15 damage, cracked -8 damage and -6 speed, up to legendary +7 / +4; `crossbow`: none 45 of 102,
  cracked -10 / -6 up to legendary +4 / +15), so one battle can invert adjacent tiers; the gates
  compare base values. A Custom Battle rolls none.
- Damage is `(v_hit / v_launch)^2 x (bow thrust_damage + ammo thrust_damage) x (1 + 0.0011 x Bow)`
  (`SandboxStrikeMagnitudeModel.CalculateStrikeMagnitudeForMissile`, `GetWeaponDamageMultiplier`,
  `DefaultSkillEffects.BowDamage`); crossbows get no skill factor. So the launcher's damage is
  almost all of it: Bow 200 adds 22%.
- Spread is `(100 - accuracy) x (1 - 0.0009 x Bow) x 0.001`, crossbows `0.0005 x Crossbow`
  (`GetWeaponInaccuracy`). The item's accuracy dominates; accuracy 100 is no spread at any skill.
  The AI's aim and lead error and its fire rate scale with `1 - skill / 312.5` on Realistic
  combat AI (`AgentStatCalculateModel.SetAiRelatedProperties`).

Until #617 the generator cloned each kingdom's donor bow and changed only `missile_speed`, so
every tier of a line carried the donor's damage and accuracy: a Rivendell T2 militia archer held
a 105-damage, accuracy-100 Noldor longbow, where vanilla T2 bows are 40 to 61 at accuracy 85.

THE MODEL (tools/ranged_ladders.json)
-------------------------------------
Every line ranks on three lists, position 1 best, ties allowed: `overall` (drives speed and the
troop's skill), `damage` and `accuracy`. Every (line, class, tier) the line lists in `tiers` is a
cell, a generated item `ladder_<line>_<bow|xbow>_t<tier>` cloned from the line's donor for that
tier's band (so tiers in a band share a look), carrying:

  speed    = speed.tier_base[t] + speed.rank_step * (worst overall rank - overall rank)
  damage   = round(damage[cls][t] * (1 + damage.step[t] * (anchor_rank - damage rank)))
  accuracy = min(max, accuracy.top[t] - rank_step * (accuracy rank - 1) (+ crossbow_bonus))

and the troop's own Bow or Crossbow is
  skill    = max(min, skill.anchor[t] + skill.step[t] * (anchor_rank - overall rank)).

TWO RULES, per launcher class and per stat (speed, damage, accuracy, skill)
---------------------------------------------------------------------------
1. Inside a LINE, a lower tier never beats a higher tier.
2. At the same TIER, a better-ranked line is never worse than a worse-ranked one (the stat's own
   rank list; equal ranks are not compared).

`validate_spec` refuses a spec whose cells break rule 1, so the grid cannot express an inversion;
`inversions` checks what the rosters actually field, and backs the RANGED_LADDER_INVERSION gate.

No CLI. Reports and writes live in tools/rebalance_ranged_ladders.py,
tools/generate_ranged_ladder_items.py and tools/restat_ranged_donors.py.
"""
from __future__ import annotations

import json
import math
import re
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
MODULEDATA_DIR = REPO_ROOT / "Main" / "_Module" / "ModuleData"
DEFAULT_SPEC = REPO_ROOT / "tools" / "ranged_ladders.json"

CLASSES = ("Bow", "Crossbow")
CLASS_TOKEN = {"Bow": "bow", "Crossbow": "xbow"}
LAUNCHER_SLOTS = ("Item0", "Item1", "Item2", "Item3")
TIER_NUMERAL = {0: "0", 1: "I", 2: "II", 3: "III", 4: "IV", 5: "V", 6: "VI", 7: "VII", 8: "VIII", 9: "IX", 10: "X"}
# The inverse: a trailing tier numeral written as its own word (tier 0 names none).
TIER_NUMERAL_RE = re.compile(r"\s+(?:%s)\s*$" % "|".join(v for t, v in sorted(TIER_NUMERAL.items(), reverse=True) if t))


# TAOM's TaomCharacterStatsModel.MaxCharacterTier override (Main/Features/TroopProgression), not
# an engine constant: vanilla DefaultCharacterStatsModel caps the same formula at 6.
MAX_TIER = 10
ID_PREFIX = "ladder_"
RANK_KEYS = ("overall", "damage", "accuracy")
STATS = ("speed", "damage", "accuracy", "skill")
STAT_RANK = {"speed": "overall", "damage": "damage", "accuracy": "accuracy", "skill": "overall"}

# The engine's missile flight, from managed_core_parameters.xml (AirFrictionArrow, shared by
# bows and crossbows per ItemObject.GetAirFrictionConstant) and MBGlobals.Gravity. The native
# GetMissileRange is not readable, so this is an estimate for reports, never a gate input.
GRAVITY = 9.806
AIR_FRICTION_ARROW = 0.003

_ITEM_REF_RE = re.compile(r'^Item\.')
_LAUNCHER_HINT_RE = re.compile(rb'weapon_class\s*=\s*"(?:Bow|Crossbow)"')
_AMMO_HINT_RE = re.compile(rb'weapon_class\s*=\s*"(?:Arrow|Bolt)"')
AMMO_CLASSES = {"Bow": "Arrow", "Crossbow": "Bolt"}


class LadderError(Exception):
    """A condition a run must not paper over: a spec that contradicts itself, a troop whose
    line declares no donor or no cell for what it carries, an id the index cannot see."""


# --------------------------------------------------------------------------- #
# Records                                                                       #
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class Launcher:
    id: str
    cls: str            # "Bow" | "Crossbow"
    speed: int          # missile_speed
    accuracy: int
    damage: int         # thrust_damage
    name: str           # display name, localisation tag stripped
    file: str           # defining file's basename
    usage: str = ""    # item_usage; a usage set flagged requires_no_mount cannot be drawn mounted


@dataclass(frozen=True)
class MountConflict:
    troop: str
    launcher: str
    usage: str


@dataclass(frozen=True)
class Ammo:
    id: str
    cls: str            # "Arrow" | "Bolt"
    damage: int         # thrust_damage; the missile leaves with the launcher's plus this (Mission.OnAgentShootMissile)
    stack: int          # stack_amount
    name: str


@dataclass
class RangedTroop:
    id: str
    file: str           # troops_<culture>.xml path
    culture: str        # the <culture> token of the file name
    level: int
    tier: int
    group: str          # default_group
    sets: list          # battle sets, each {slot: item id} over Item0..Item3 only
    upgrades: list
    skills: dict = field(default_factory=dict)
    name: str = ""     # display name, localisation tag stripped
    mounted: bool = False  # HorseArcher/Cavalry group, or a Horse slot in any battle set
    templated: bool = False  # skill_template=: on 1.5.3 the template's skills, inline rows laid over them


@dataclass(frozen=True)
class Cell:
    speed: int
    damage: int
    accuracy: int


@dataclass(frozen=True)
class Inversion:
    kind: str           # "tier" | "rank"
    stat: str           # "speed" | "damage" | "accuracy" | "skill"
    cls: str
    scope: str          # the line (tier rule) or "T<n>" (rank rule) the pair sits in
    low: str            # the troop that should not be better (lower tier, or worse rank)
    low_tier: int
    low_line: str
    low_value: int
    high: str           # the troop that should be at least as good
    high_tier: int
    high_line: str
    high_value: int

    @property
    def gap(self) -> int:
        return self.low_value - self.high_value


@dataclass
class Group:
    kind: str
    stat: str
    cls: str
    scope: str
    count: int
    worst: Inversion


@dataclass(frozen=True)
class LadderItem:
    id: str
    line: str
    cls: str
    tier: int
    band: str
    speed: int
    damage: int
    accuracy: int
    donor: str
    folder: str
    usage: str | None = None   # the line's item_usage override for the class, else the donor's


@dataclass(frozen=True)
class Edit:
    file: str           # absolute path of the troop file
    troop: str
    slot: str
    old: str
    new: str
    cls: str
    tier: int
    line: str


@dataclass(frozen=True)
class SkillEdit:
    file: str
    troop: str
    skill: str          # "Bow" | "Crossbow"
    old: int
    new: int
    reason: str         # "cell" (the ladder's value) | "clamp" (raised to its upgrade source)


# --------------------------------------------------------------------------- #
# Tiers, bands, spec                                                            #
# --------------------------------------------------------------------------- #
def engine_tier(level: int) -> int:
    """clamp(ceil((level - 5) / 5), 0, MaxCharacterTier), as CharacterObject.Tier computes it
    and as taom_schema.Validator._troop_tier mirrors it (a test pins the two together)."""
    raw = -((5 - int(level)) // 5)
    return max(0, min(raw, MAX_TIER))


def load_spec(path: Path | str = DEFAULT_SPEC) -> dict:
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def band_of(tier: int, spec: dict) -> str:
    for band, (lo, hi) in spec["bands"].items():
        if lo <= tier <= hi:
            return band
    raise LadderError(f"tier {tier} is in no band of the spec")


def line_spec(line_id: str, spec: dict) -> dict:
    for line in spec["lines"]:
        if line["id"] == line_id:
            return line
    raise LadderError(f"line {line_id!r} is not in the spec")


def rank(line_id: str, stat: str, spec: dict) -> int:
    """The line's position on the stat's rank list; `stat` is a stat or a rank key."""
    key = STAT_RANK.get(stat, stat)
    return int(line_spec(line_id, spec)["ranks"][key])


def worst_rank(spec: dict) -> int:
    return max(int(ln["ranks"]["overall"]) for ln in spec["lines"])


def tiers_for(line_id: str, cls: str, spec: dict) -> list[int]:
    return [int(t) for t in (line_spec(line_id, spec).get("tiers") or {}).get(cls) or []]


def _at(table: dict, tier: int, what: str):
    try:
        return table[str(tier)]
    except (KeyError, TypeError):
        raise LadderError(f"{what} has no value for tier {tier}") from None


def cell(line_id: str, cls: str, tier: int, spec: dict) -> Cell:
    stats = spec["stats"]
    anchor = int(stats["anchor_rank"])
    dmg = stats["damage"]
    damage = round(_at(dmg[cls], tier, f"stats.damage.{cls}")
                   * (1 + _at(dmg["step"], tier, "stats.damage.step") * (anchor - rank(line_id, "damage", spec))))
    acc = stats["accuracy"]
    accuracy = (_at(acc["top"], tier, "stats.accuracy.top")
                - int(acc["rank_step"]) * (rank(line_id, "accuracy", spec) - 1)
                + (int(acc.get("crossbow_bonus", 0)) if cls == "Crossbow" else 0))
    accuracy = min(int(acc.get("max", 100)), accuracy)
    spd = stats["speed"]
    speed = (_at(spd["tier_base"], tier, "stats.speed.tier_base")
             + int(spd["rank_step"]) * (worst_rank(spec) - rank(line_id, "speed", spec)))
    return Cell(speed=int(speed), damage=int(damage), accuracy=int(accuracy))


def skill_cell(line_id: str, tier: int, spec: dict) -> int:
    sk = spec["stats"]["skill"]
    anchor = int(spec["stats"]["anchor_rank"])
    value = (_at(sk["anchor"], tier, "stats.skill.anchor")
             + _at(sk["step"], tier, "stats.skill.step") * (anchor - rank(line_id, "skill", spec)))
    return int(max(int(sk.get("min", 0)), value))


def ladder_id(line_id: str, cls: str, tier: int) -> str:
    return f"{ID_PREFIX}{line_id}_{CLASS_TOKEN[cls]}_t{int(tier)}"


def usage_for(line: dict, cls: str) -> str | None:
    """The line's item_usage override for a class, or None to keep the donor's. The Armory's
    own pattern for a bow a rider can draw: wm_mirkwood_bow_a02 "LongBow II - Horse" is the
    a01 mesh with item_usage="bow" (Native's long_bow is base_set="bow" plus the flags
    requires_no_mount and requires_no_shield, nothing else)."""
    return (line.get("usage") or {}).get(cls)


def donor_for(line: dict, cls: str, band: str) -> str | None:
    by_band = (line.get("donor_by_band") or {}).get(cls) or {}
    return by_band.get(band) or (line.get("donor") or {}).get(cls)


def _is_int(v) -> bool:
    return isinstance(v, int) and not isinstance(v, bool)


def _is_num(v) -> bool:
    return isinstance(v, (int, float)) and not isinstance(v, bool) and math.isfinite(v)


def validate_spec(spec: dict, launchers: dict | None = None, cultures: set | None = None) -> list[str]:
    """Every way the spec can contradict itself, as one string each. With a launcher index the
    donors are checked too (present, and of the class they are cloned for); with the set of
    troop-file culture tokens actually on disk, every `files` token must name one of them (a
    typo like `rhun` for `rhun_new` is otherwise a line with no troops, which reads as clean)."""
    problems: list[str] = []
    bands = spec.get("bands") or {}
    covered: dict[int, str] = {}
    for band, rng in bands.items():
        try:
            lo, hi = int(rng[0]), int(rng[1])
        except (TypeError, ValueError, IndexError):
            problems.append(f"band {band!r} is not a [low, high] tier pair: {rng}")
            continue
        for t in range(lo, hi + 1):
            if t in covered:
                problems.append(f"tier {t} is in both band {covered[t]} and band {band}")
            covered[t] = band
    for t in range(0, MAX_TIER + 1):
        if t not in covered:
            problems.append(f"tier {t} is in no band")

    stats_ok = _validate_stats(spec.get("stats"), problems)
    ceiling = spec.get("hero_ceiling") or {}
    for cls, v in ceiling.items():
        if cls not in CLASSES or not _is_int(v) or v <= 0:
            problems.append(f"hero_ceiling {cls}: {v!r} must be a positive integer for Bow or Crossbow")

    seen: set[str] = set()
    claimed_files: dict[str, str] = {}
    prefixes_seen: list[tuple[str, str]] = []   # (prefix, line id)
    for line in spec.get("lines") or []:
        before = len(problems)       # a line with a structural problem does not get its cells judged
        lid = line.get("id")
        if not lid:
            problems.append("a line has no id")
            continue
        if lid in seen:
            problems.append(f"duplicate line id {lid!r}")
        seen.add(lid)
        if not line.get("folder"):
            problems.append(f"line {lid!r} names no folder")
        if not line.get("files") and not line.get("prefixes"):
            problems.append(f"line {lid!r} claims no files and no prefixes")
        for pfx in line.get("prefixes") or []:
            # Two prefixes that can match one id would be decided by list order alone.
            for other, other_line in prefixes_seen:
                if other_line != lid and (pfx.startswith(other) or other.startswith(pfx)):
                    problems.append(f"prefix {pfx!r} of line {lid!r} and prefix {other!r} of line "
                                    f"{other_line!r} overlap; an id matching both is decided by order")
            prefixes_seen.append((pfx, lid))
        for f in line.get("files") or []:
            if cultures is not None and f not in cultures:
                problems.append(f"line {lid!r} claims {f!r} but no troops_{f}.xml is present; the line has no troops")
            # A file may be claimed WHOLE by one line only; prefix lines carve troops out of it.
            owner = claimed_files.get(f)
            if not line.get("prefixes"):
                if owner is not None:
                    problems.append(f"file {f!r} is claimed whole by both {owner!r} and {lid!r}")
                claimed_files[f] = lid
        ranks = line.get("ranks") or {}
        for key in RANK_KEYS:
            v = ranks.get(key)
            if not _is_int(v) or v < 1:
                problems.append(f"line {lid!r} rank {key!r} is {v!r}; ranks are positive integers, 1 is best")
        donors = line.get("donor") or {}
        if not donors:
            problems.append(f"line {lid!r} declares no donor")
        for cls, donor in donors.items():
            if cls not in CLASSES:
                problems.append(f"line {lid!r} declares donor class {cls!r}; only Bow and Crossbow are ladders")
            elif launchers is not None:
                _check_donor(lid, cls, donor, launchers, problems)
        tiers = line.get("tiers") or {}
        if set(tiers) != {c for c in donors if c in CLASSES}:
            problems.append(f"line {lid!r} lists tiers for {sorted(tiers)} but donors for {sorted(donors)}; "
                            "each ladder class needs both")
        for cls, ts in tiers.items():
            if cls not in CLASSES:
                continue
            if not isinstance(ts, list) or not ts or not all(_is_int(t) and 0 <= t <= MAX_TIER for t in ts):
                problems.append(f"line {lid!r} {cls} tiers {ts!r} must be a non-empty list of tiers 0 to {MAX_TIER}")
                continue
            if len(set(ts)) != len(ts):
                problems.append(f"line {lid!r} {cls} tiers {ts!r} repeat a tier")
            if stats_ok and len(problems) == before:
                _check_cells(spec, lid, cls, sorted(set(ts)), problems)
        for cls, usage in (line.get("usage") or {}).items():
            if cls not in CLASSES:
                problems.append(f"line {lid!r} overrides item_usage for {cls!r}; only Bow and Crossbow are ladders")
            elif not isinstance(usage, str) or not usage.strip():
                problems.append(f"line {lid!r} item_usage override for {cls!r} is empty")
        for cls, per_band in (line.get("donor_by_band") or {}).items():
            if cls not in donors:
                problems.append(f"line {lid!r} has donor_by_band for {cls!r} but no default donor")
            for band, donor in per_band.items():
                if band not in bands:
                    problems.append(f"line {lid!r} donor_by_band names unknown band {band!r}")
                elif launchers is not None and cls in CLASSES:
                    _check_donor(lid, cls, donor, launchers, problems)
    return problems + validate_restat_tables(spec)


def validate_restat_tables(spec: dict) -> list[str]:
    """Problems in `donor_stats` / `ammo_stats`, the only part of the spec restat_ranged_donors.py
    reads. A row with no recognised stat would match its weapon, set nothing and verify OK."""
    problems = []
    for iid, row in (spec.get("donor_stats") or {}).items():
        if not isinstance(row, dict) or not row or set(row) - {"damage", "accuracy"} \
                or not all(_is_int(v) and v > 0 for v in row.values()):
            problems.append(f"donor_stats {iid!r}: {row!r} must set damage and/or accuracy to positive integers")
    for iid, v in (spec.get("ammo_stats") or {}).items():
        if not _is_int(v) or v < 0:
            problems.append(f"ammo_stats {iid!r}: {v!r} must be a non-negative integer")
    for iid in sorted(set(spec.get("donor_stats") or {}) & set(spec.get("ammo_stats") or {})):
        problems.append(f"{iid!r} is in both donor_stats and ammo_stats; an item is a launcher or ammo")
    return problems


def _validate_stats(stats, problems: list[str]) -> bool:
    if not isinstance(stats, dict):
        problems.append("stats is missing; the ladder has no curves")
        return False
    before = len(problems)
    if not _is_int(stats.get("anchor_rank")) or stats.get("anchor_rank", 0) < 1:
        problems.append(f"stats.anchor_rank {stats.get('anchor_rank')!r} must be a positive integer")

    def table(path, t, allow_zero=False):
        if not isinstance(t, dict) or not t:
            problems.append(f"{path} must be a {{tier: value}} table")
            return
        for k, v in t.items():
            if not (str(k).isdigit() and 0 <= int(k) <= MAX_TIER):
                problems.append(f"{path} key {k!r} is not a tier 0 to {MAX_TIER}")
            if not _is_num(v) or v < 0 or (v == 0 and not allow_zero):
                problems.append(f"{path}[{k}] is {v!r}; it must be a positive number")

    dmg = stats.get("damage") or {}
    for cls in CLASSES:
        table(f"stats.damage.{cls}", dmg.get(cls))
    table("stats.damage.step", dmg.get("step"), allow_zero=True)
    acc = stats.get("accuracy") or {}
    table("stats.accuracy.top", acc.get("top"))
    for k in ("rank_step", "max"):
        if not _is_int(acc.get(k)) or acc.get(k) < 0:
            problems.append(f"stats.accuracy.{k} {acc.get(k)!r} must be a non-negative integer")
    if not _is_int(acc.get("crossbow_bonus", 0)):
        problems.append(f"stats.accuracy.crossbow_bonus {acc.get('crossbow_bonus')!r} must be an integer")
    spd = stats.get("speed") or {}
    table("stats.speed.tier_base", spd.get("tier_base"))
    if not _is_int(spd.get("rank_step")) or spd.get("rank_step", 0) <= 0:
        problems.append("stats.speed.rank_step must be a positive integer")
    sk = stats.get("skill") or {}
    table("stats.skill.anchor", sk.get("anchor"), allow_zero=True)
    table("stats.skill.step", sk.get("step"), allow_zero=True)
    if not _is_int(sk.get("min", 0)) or sk.get("min", 0) < 0:
        problems.append("stats.skill.min must be a non-negative integer")
    return len(problems) == before


def _check_cells(spec: dict, lid: str, cls: str, tiers: list[int], problems: list[str]) -> None:
    """Every listed tier has a value in every curve, and each stat rises strictly with tier, so
    the spec itself can never express a tier inversion."""
    values = []
    for t in tiers:
        try:
            c = cell(lid, cls, t, spec)
            values.append((t, c.speed, c.damage, c.accuracy, skill_cell(lid, t, spec)))
        except LadderError as exc:
            problems.append(f"line {lid!r} {cls} tier {t}: {exc}")
            return
    for (t0, *a), (t1, *b) in zip(values, values[1:]):
        for name, x, y in zip(("speed", "damage", "accuracy", "skill"), a, b):
            if y <= x:
                problems.append(f"line {lid!r} {cls}: {name} does not rise from tier {t0} ({x}) to tier {t1} ({y})")


def _check_donor(lid, cls, donor, launchers, problems):
    rec = launchers.get(donor)
    if rec is None:
        problems.append(f"line {lid!r}: donor {donor!r} is not in the launcher index")
    elif rec.cls != cls:
        problems.append(f"line {lid!r}: donor {donor!r} is a {rec.cls}, cloned for {cls}")


# --------------------------------------------------------------------------- #
# Lines                                                                         #
# --------------------------------------------------------------------------- #
def line_for(troop_id: str, culture: str, spec: dict) -> str | None:
    """Prefix claims first, then whole-file claims. None when nothing claims the troop, which the
    gate reports rather than drops."""
    for line in spec["lines"]:
        if any(troop_id.startswith(p) for p in line.get("prefixes") or []):
            if not line.get("files") or culture in line["files"]:
                return line["id"]
    for line in spec["lines"]:
        if line.get("prefixes"):
            continue
        if culture in (line.get("files") or []):
            return line["id"]
    return None


def line_of(troop: RangedTroop, spec: dict) -> str | None:
    return line_for(troop.id, troop.culture, spec)


# --------------------------------------------------------------------------- #
# Indexes                                                                       #
# --------------------------------------------------------------------------- #
def display_name(name_attr: str | None) -> str:
    name = name_attr or ""
    return name.split("}", 1)[1] if name.startswith("{=") and "}" in name else name


def index_launchers(roots, failures: list | None = None) -> dict[str, Launcher]:
    """id -> Launcher over every `*.xml` (never `*.xml.bak-*`) under each root, for every
    <Item> whose <Weapon> is a Bow or a Crossbow. A file that does not parse is recorded in
    `failures`, never silently dropped: its bows would then read as missing donors. First
    definition wins, as the engine's registry does."""
    import xml.etree.ElementTree as ET
    index: dict[str, Launcher] = {}
    for root in roots:
        root = Path(root)
        if not root.exists():
            continue
        for path in sorted(root.rglob("*.xml")):
            # Only a file that mentions a launcher class is parsed: the validator walks eight
            # module roots on every run and parsing every armour file cost it a second.
            raw = path.read_bytes()
            if not _LAUNCHER_HINT_RE.search(raw):
                continue
            try:
                tree = ET.fromstring(raw)
            except ET.ParseError as exc:
                if failures is not None:
                    failures.append(f"{path}: not well-formed, its launchers are invisible ({exc})")
                continue
            for item in tree.iter("Item"):
                iid = item.get("id")
                if not iid or iid in index:
                    continue
                for weapon in item.iter("Weapon"):
                    cls = weapon.get("weapon_class")
                    if cls in CLASSES:
                        index[iid] = Launcher(
                            id=iid, cls=cls,
                            speed=int(weapon.get("missile_speed", "0") or 0),
                            accuracy=int(weapon.get("accuracy", "0") or 0),
                            damage=int(weapon.get("thrust_damage", "0") or 0),
                            name=display_name(item.get("name")),
                            file=path.name, usage=weapon.get("item_usage", "") or "")
                        break
    return index


def mount_barred_usages(game_modules) -> set | None:
    """The item_usage ids whose usage set carries `requires_no_mount` (Native's long_bow, for
    one), read from every Modules/*/ModuleData/item_usage_sets.xml. None when no such file could
    be read: the callers then skip the mount check and say so, never guess a list."""
    import xml.etree.ElementTree as ET
    if not game_modules:
        return None
    barred: set[str] = set()
    seen = False
    for path in sorted(Path(game_modules).glob("*/ModuleData/item_usage_sets.xml")):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        seen = True
        for us in root.iter("item_usage_set"):
            if any(f.get("name") == "requires_no_mount" for f in us.iter("flag")):
                barred.add(us.get("id", ""))
    return barred if seen else None


def index_ammo(roots, failures: list | None = None) -> dict[str, Ammo]:
    """id -> Ammo over every Arrow and Bolt <Item> under the roots: a missile's damage at launch
    is the bow's thrust_damage plus the ammo's."""
    import xml.etree.ElementTree as ET
    index: dict[str, Ammo] = {}
    for root in roots:
        root = Path(root)
        if not root.exists():
            continue
        for path in sorted(root.rglob("*.xml")):
            raw = path.read_bytes()
            if not _AMMO_HINT_RE.search(raw):
                continue
            try:
                tree = ET.fromstring(raw)
            except ET.ParseError as exc:
                if failures is not None:
                    failures.append(f"{path}: not well-formed, its ammo is invisible ({exc})")
                continue
            for item in tree.iter("Item"):
                iid = item.get("id")
                if not iid or iid in index:
                    continue
                for weapon in item.iter("Weapon"):
                    cls = weapon.get("weapon_class")
                    if cls in ("Arrow", "Bolt"):
                        index[iid] = Ammo(
                            id=iid, cls=cls,
                            damage=int(weapon.get("thrust_damage", "0") or 0),
                            stack=int(weapon.get("stack_amount", "0") or 0),
                            name=display_name(item.get("name")))
                        break
    return index


def default_item_roots(game_modules, moduledata=MODULEDATA_DIR) -> list[Path]:
    """The three data modules a troop's launcher can come from."""
    roots = []
    if game_modules:
        gm = Path(game_modules)
        roots.append(gm / "LOTRLOME_Armory" / "ModuleData")
        roots.append(gm / "SandBoxCore" / "ModuleData" / "items")
    roots.append(Path(moduledata))
    return roots


def troop_file_cultures(moduledata=MODULEDATA_DIR) -> set[str]:
    """The culture tokens of the troop files on disk, for validate_spec's `files` check."""
    return {p.name[len("troops_"):-len(".xml")] for p in (Path(moduledata) / "troops").glob("troops_*.xml")}


def is_civilian(elem) -> bool:
    return elem.get("civilian") == "true" or elem.get("equipmentType") == "Civilian"


def battle_sets(npc):
    """Every EquipmentRoster / EquipmentSet under a troop that is not civilian: the sets it can
    spawn with in battle. The one reading of "which sets count" for the ladder and the level curve."""
    for es in list(npc.iter("EquipmentRoster")) + list(npc.iter("EquipmentSet")):
        if not is_civilian(es):
            yield es


def load_ranged_troops(moduledata=MODULEDATA_DIR, failures: list | None = None) -> dict[str, RangedTroop]:
    """Every NPCCharacter in troops/troops_*.xml with its battle sets restricted to the four
    weapon slots. Villagers (characters/npcs_*.xml) are not on the ladder. Civilian sets never
    cross-draw with battle sets and are skipped. A file that does not parse is recorded in
    `failures`: its troops would otherwise vanish from every report and gate without a word."""
    import xml.etree.ElementTree as ET
    md = Path(moduledata)
    troops: dict[str, RangedTroop] = {}
    for path in sorted((md / "troops").glob("troops_*.xml")):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as exc:
            if failures is not None:
                failures.append(f"{path}: not well-formed, its troops are invisible ({exc})")
            continue
        culture = path.name[len("troops_"):-len(".xml")]
        for npc in root.iter("NPCCharacter"):
            tid = npc.get("id")
            if not tid:
                continue
            sets = []
            horse = False
            for es in battle_sets(npc):
                eqs = es.findall("equipment")
                if not eqs:
                    continue
                slots = {}
                for eq in eqs:
                    slot = eq.get("slot", "")
                    if slot in LAUNCHER_SLOTS:
                        slots[slot] = _ITEM_REF_RE.sub("", eq.get("id") or "")
                    elif slot == "Horse" and (eq.get("id") or "").strip():
                        horse = True
                sets.append(slots)
            level = int(npc.get("level", "0") or 0)
            troops[tid] = RangedTroop(
                id=tid, file=str(path), culture=culture, level=level, tier=engine_tier(level),
                group=npc.get("default_group", "") or "", sets=sets,
                upgrades=_upgrades(npc), skills=_skills(npc), name=display_name(npc.get("name")),
                mounted=horse or (npc.get("default_group", "") in ("HorseArcher", "Cavalry")),
                templated=bool(npc.get("skill_template")))
    return troops


def load_upgrade_sources(moduledata=MODULEDATA_DIR) -> dict[str, RangedTroop]:
    """The characters/npcs_*.xml entries that upgrade INTO the troop files (the villager_<culture>
    recruits): read-only sources for the skill clamp, never written."""
    import xml.etree.ElementTree as ET
    out: dict[str, RangedTroop] = {}
    for path in sorted((Path(moduledata) / "characters").glob("npcs_*.xml")):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        for npc in root.iter("NPCCharacter"):
            ups = _upgrades(npc)
            if npc.get("id") and ups:
                level = int(npc.get("level", "0") or 0)
                out[npc.get("id")] = RangedTroop(
                    id=npc.get("id"), file=str(path), culture="", level=level, tier=engine_tier(level),
                    group=npc.get("default_group", "") or "", sets=[], upgrades=ups, skills=_skills(npc),
                    templated=bool(npc.get("skill_template")))
    return out


def _upgrades(npc) -> list[str]:
    return [_ITEM_REF_RE.sub("", (u.get("id") or "")).replace("NPCCharacter.", "", 1)
            for u in npc.findall("./upgrade_targets/upgrade_target")]


def _skills(npc) -> dict:
    skills = {}
    for sk in npc.findall("./skills/skill"):
        try:
            skills[sk.get("id", "")] = int(sk.get("value", "0") or 0)
        except ValueError:
            pass
    return skills


def troop_launchers(troop: RangedTroop, launchers: dict) -> dict[str, set[str]]:
    """{class: {launcher ids}} over the troop's battle sets."""
    out: dict[str, set[str]] = {}
    for st in troop.sets:
        for slot in LAUNCHER_SLOTS:
            rec = launchers.get(st.get(slot, ""))
            if rec is not None:
                out.setdefault(rec.cls, set()).add(rec.id)
    return out


# What a ladder cell id has looked like: the #582 band letters and the #617 tiers.
LADDER_ID_RE = re.compile(r"^" + ID_PREFIX + r"(?P<line>.+)_(?P<cls>bow|xbow)_(?P<cell>[a-z]|t\d+)$")


def retired_ladder_launchers(troops: dict, launchers: dict) -> dict[str, Launcher]:
    """Placeholders for ladder ids a roster still names but no loaded file defines: a band id from
    before #617 (`ladder_gondor_bow_r`), or a tier a spec change dropped. Regenerating the items
    removes them before the rosters are repointed, so without these the roster tool would no
    longer see those slots as launchers and would leave them naming items that do not exist.
    Zero stats: callers add them to the index for planning only."""
    token_cls = {v: k for k, v in CLASS_TOKEN.items()}
    out: dict[str, Launcher] = {}
    for troop in troops.values():
        for st in troop.sets:
            for slot in LAUNCHER_SLOTS:
                iid = st.get(slot, "")
                m = LADDER_ID_RE.match(iid)
                if m and iid not in launchers and iid not in out:
                    out[iid] = Launcher(id=iid, cls=token_cls[m.group("cls")], speed=0, accuracy=0, damage=0,
                                        name="(retired ladder id)", file="(none)")
    return out


def troop_values(troop: RangedTroop, launchers: dict, cls: str, stat: str) -> tuple[int, int] | None:
    """(worst, best) value of a stat over the launchers of the class the troop can spawn with;
    for skill, the troop's own Bow or Crossbow (one value). None when it carries no such launcher.
    The rules hold the troop that should be better to its WORST set, so an alternate set with a
    weaker bow cannot hide behind a stronger one (Codex review, 2026-09-13)."""
    ids = troop_launchers(troop, launchers).get(cls)
    if not ids:
        return None
    if stat == "skill":
        v = int(troop.skills.get(cls, 0))
        return v, v
    vals = [getattr(launchers[i], stat) for i in ids]
    return min(vals), max(vals)


def mount_conflicts(troops: dict, launchers: dict, barred: set) -> list[MountConflict]:
    """Every mounted troop holding, in any battle set, a launcher whose item_usage is barred
    on horseback. Pure; the planner refuses on it and the validator reports it."""
    out: list[MountConflict] = []
    for tid in sorted(troops):
        troop = troops[tid]
        if not troop.mounted:
            continue
        seen: set[str] = set()
        for st in troop.sets:
            for slot in LAUNCHER_SLOTS:
                rec = launchers.get(st.get(slot, ""))
                if rec is not None and rec.usage in barred and rec.id not in seen:
                    seen.add(rec.id)
                    out.append(MountConflict(tid, rec.id, rec.usage))
    return out


# --------------------------------------------------------------------------- #
# The two rules                                                                 #
# --------------------------------------------------------------------------- #
def unassigned(troops: dict, launchers: dict, spec: dict) -> list[RangedTroop]:
    """Troops carrying a launcher that no line claims. A gate that dropped them would read
    as clean for a whole new troop file."""
    return [troops[t] for t in sorted(troops)
            if line_of(troops[t], spec) is None and troop_launchers(troops[t], launchers)]


def unlisted(troops: dict, launchers: dict, spec: dict) -> list[tuple[RangedTroop, str]]:
    """(troop, class) for every troop whose line has no cell at its tier for a class it carries:
    the generator makes no item there, so the roster tool cannot place it."""
    out = []
    for tid in sorted(troops):
        t = troops[tid]
        line = line_of(t, spec)
        if line is None:
            continue
        for cls in sorted(troop_launchers(t, launchers)):
            if t.tier not in tiers_for(line, cls, spec):
                out.append((t, cls))
    return out


def inversions(troops: dict, launchers: dict, spec: dict, stats=STATS) -> list[Inversion]:
    """Every pair that breaks a rule, per stat. Pure: the validator and the tool both call this."""
    recs = []
    for tid in sorted(troops):
        troop = troops[tid]
        line = line_of(troop, spec)
        if line is None:
            continue
        for cls in CLASSES:
            vals = {s: troop_values(troop, launchers, cls, s) for s in stats}
            if vals[stats[0]] is not None:
                recs.append((troop, line, cls, vals))
    found: list[Inversion] = []
    for stat in stats:
        for a_troop, a_line, a_cls, a_vals in recs:
            a_max = a_vals[stat][1]
            for b_troop, b_line, b_cls, b_vals in recs:
                if a_cls != b_cls or a_troop.id == b_troop.id:
                    continue
                b_min = b_vals[stat][0]
                if a_max <= b_min:
                    continue
                # Rule 1: inside a line, a lower tier never beats a higher tier.
                if a_line == b_line and a_troop.tier < b_troop.tier:
                    found.append(Inversion("tier", stat, a_cls, a_line,
                                           a_troop.id, a_troop.tier, a_line, a_max,
                                           b_troop.id, b_troop.tier, b_line, b_min))
                # Rule 2: at the same tier, a better rank is never worse on the stat.
                if (a_troop.tier == b_troop.tier and a_line != b_line
                        and rank(a_line, stat, spec) > rank(b_line, stat, spec)):
                    found.append(Inversion("rank", stat, a_cls, f"T{a_troop.tier}",
                                           a_troop.id, a_troop.tier, a_line, a_max,
                                           b_troop.id, b_troop.tier, b_line, b_min))
    found.sort(key=lambda f: (STATS.index(f.stat), -f.gap, f.kind, f.cls, f.scope, f.low, f.high))
    return found


def summarize(found: list[Inversion]) -> list[Group]:
    """One group per (kind, stat, class, scope) with its worst pair, worst group first."""
    groups: dict[tuple, Group] = {}
    for f in found:
        key = (f.kind, f.stat, f.cls, f.scope)
        g = groups.get(key)
        if g is None:
            groups[key] = Group(f.kind, f.stat, f.cls, f.scope, 1, f)
        else:
            g.count += 1
            if f.gap > g.worst.gap:
                g.worst = f
    return sorted(groups.values(), key=lambda g: (STATS.index(g.stat), -g.worst.gap, g.kind, g.cls, g.scope))


# --------------------------------------------------------------------------- #
# Planning                                                                      #
# --------------------------------------------------------------------------- #
def planned_items(spec: dict) -> list[LadderItem]:
    """Every tier the spec lists for every (line, class): a pure function of the spec, so the item
    set never depends on which troops happen to exist today (the gate reports a troop at a tier
    its line does not list)."""
    items: list[LadderItem] = []
    for line in spec["lines"]:
        for cls in CLASSES:
            if cls not in (line.get("donor") or {}):
                continue
            for tier in tiers_for(line["id"], cls, spec):
                band = band_of(tier, spec)
                c = cell(line["id"], cls, tier, spec)
                items.append(LadderItem(
                    id=ladder_id(line["id"], cls, tier), line=line["id"], cls=cls, tier=tier, band=band,
                    speed=c.speed, damage=c.damage, accuracy=c.accuracy,
                    donor=donor_for(line, cls, band), folder=line["folder"], usage=usage_for(line, cls)))
    return items


def planned_edits(troops: dict, launchers: dict, spec: dict, barred: set | None = None) -> list[Edit]:
    """One edit per launcher slot of every battle set whose item is not already the troop's cell.
    Ammo slots are never touched, a class never changes. A troop whose line declares no donor for
    a class it carries is an error, not a skip; so is a troop at a tier its line lists no cell
    for, and a mounted troop whose cell's usage (the line's override, else the donor's) is in
    `barred` (requires_no_mount): the item would spawn on the horse and never be drawn. Pass
    `barred=None` only when the install could not be read."""
    edits: list[Edit] = []
    for tid in sorted(troops):
        troop = troops[tid]
        line = line_of(troop, spec)
        if line is None:
            continue
        ls = line_spec(line, spec)
        band = band_of(troop.tier, spec)
        carried = troop_launchers(troop, launchers)
        for cls in carried:
            if cls not in (ls.get("donor") or {}):
                raise LadderError(f"{tid} carries a {cls} but line {line!r} declares no {cls} donor")
            if troop.tier not in tiers_for(line, cls, spec):
                raise LadderError(
                    f"{tid} is tier {troop.tier} but line {line!r} lists no {cls} cell there "
                    f"(tiers {tiers_for(line, cls, spec)}); add the tier to the line in the spec")
            if barred and troop.mounted:
                donor = launchers.get(donor_for(ls, cls, band) or "")
                usage = usage_for(ls, cls) or (donor.usage if donor is not None else None)
                if usage in barred:
                    raise LadderError(
                        f"{tid} is mounted but its cell {ladder_id(line, cls, troop.tier)} would carry "
                        f"item_usage {usage} (requires_no_mount) from donor {donor.id if donor else '?'}; "
                        f"give line {line!r} a donor the troop can draw from the saddle, or a "
                        f"\"usage\" override ({{\"{cls}\": \"bow\"}}) in the spec")
        seen: set[tuple] = set()
        for st in troop.sets:
            for slot in LAUNCHER_SLOTS:
                old = st.get(slot, "")
                rec = launchers.get(old)
                if rec is None:
                    continue
                new = ladder_id(line, rec.cls, troop.tier)
                if old == new or (slot, old) in seen:
                    continue
                seen.add((slot, old))
                edits.append(Edit(troop.file, tid, slot, old, new, rec.cls, troop.tier, line))
    return edits


def planned_skill_edits(troops: dict, launchers: dict, spec: dict, militia=frozenset(),
                        exempt_edges=None, sources=None) -> list[SkillEdit]:
    """The troop's Bow or Crossbow for every class it carries, set to its cell; then the clamp
    that keeps the UPGRADE_SKILL_REGRESSION rule: a child below its upgrade source on that skill
    is raised to it (clamp only ever raises), except that a LADDER troop is never raised off its
    cell, which is an error. Militia-to-militia edges are flat by design and skipped, as are the
    `exempt_edges` {(source, target): {skills}} (rebalance_troops.RESPECIALIZATION_EXEMPT_EDGES).
    `sources` are read-only upgrade sources outside the troop files (the villagers). An edge with
    a skill_template on either side is skipped, as UPGRADE_SKILL_REGRESSION skips it: that side's
    real skills are the template's values with its inline rows laid over them, and the template is
    not resolved here. A templated ladder troop still takes its cell: on 1.5.3 the inline row wins
    over the template (BasicCharacterObject.Deserialize; 1.4.8 ignored the inline block)."""
    exempt_edges = exempt_edges or {}
    new: dict[tuple[str, str], int] = {}
    cells: set[tuple[str, str]] = set()
    for tid in sorted(troops):
        t = troops[tid]
        line = line_of(t, spec)
        if line is None:
            continue
        for cls in troop_launchers(t, launchers):
            if t.tier in tiers_for(line, cls, spec):
                new[(tid, cls)] = skill_cell(line, t.tier, spec)
                cells.add((tid, cls))
    everyone = dict(sources or {})
    everyone.update(troops)

    def value(tid, cls):
        return new.get((tid, cls), int(everyone[tid].skills.get(cls, 0)))

    changed = True
    passes = 0
    while changed:
        changed = False
        passes += 1
        if passes > len(everyone) + 2:
            raise LadderError("the upgrade graph did not settle; it is probably cyclic")
        for src_id in sorted(everyone):
            src = everyone[src_id]
            for tgt_id in src.upgrades:
                if tgt_id not in troops or (src_id in militia and tgt_id in militia):
                    continue
                if src.templated or troops[tgt_id].templated:
                    continue
                for cls in CLASSES:
                    if cls in exempt_edges.get((src_id, tgt_id), ()):
                        continue
                    want, have = value(src_id, cls), value(tgt_id, cls)
                    if have >= want:
                        continue
                    if (tgt_id, cls) in cells:
                        raise LadderError(
                            f"upgrading {src_id} ({cls} {want}) into {tgt_id} would LOWER {cls} to its cell "
                            f"{have} (UPGRADE_SKILL_REGRESSION); rank or tier the two lines so the target's "
                            f"cell is at least the source's")
                    new[(tgt_id, cls)] = want
                    changed = True
    edits = []
    for (tid, cls), v in sorted(new.items()):
        old = int(troops[tid].skills.get(cls, 0))
        if v != old or cls not in troops[tid].skills:
            edits.append(SkillEdit(troops[tid].file, tid, cls, old, v,
                                   "cell" if (tid, cls) in cells else "clamp"))
    return edits


# --------------------------------------------------------------------------- #
# Heroes: what a lord or wanderer can carry                                     #
# --------------------------------------------------------------------------- #
_STRIP_REF_RE = re.compile(r"^(?:Item|EquipmentRoster)\.")
# Rosters the game applies to the player at runtime, which no NPCCharacter names: character
# creation, the career start, and the enlistment quartermaster (EnlistmentRosterResolver).
PLAYER_ROSTER_PREFIXES = ("player_char_creation_", "player_career_", "enlist_")


def hero_launchers(moduledata, launchers: dict) -> dict[str, list[str]]:
    """{launcher id: [character ids]} for every hero-class character (a Lord, a Wanderer or any
    is_hero NPCCharacter outside troops/) that can carry the launcher: its own equipment, the
    battle EquipmentRosters it names by EquipmentSet id, the templates lords.xslt hands to the
    vanilla lords it retags ("lords.xslt" as the character), and the player's start and career
    rosters (the roster id as the character). Civilian sets are skipped. The `Equipment` tag is
    matched in either case: the equipment-set files use `<Equipment>`, the characters `<equipment>`."""
    import xml.etree.ElementTree as ET
    md = Path(moduledata)

    def ids_in(elem):
        return {_STRIP_REF_RE.sub("", e.get("id") or "") for e in elem.iter()
                if e.tag.lower() == "equipment" and (e.get("slot") or "").startswith("Item")}

    def battle_ids(roster):
        # Civilian is marked on the roster or on an inner set; neither is carried into battle.
        if is_civilian(roster):
            return set()
        inner = roster.findall("EquipmentSet")
        if not inner:
            return ids_in(roster)
        return set().union(*(ids_in(s) for s in inner if not is_civilian(s)))

    rosters: dict[str, set] = {}
    docs = []
    out: dict[str, set] = {}
    for path in sorted(md.rglob("*.xml")):
        # Only a file holding a roster or a character can matter; the rest (two thirds of them
        # language files) are skipped unparsed. iter() matches these tags exact-case, so the
        # byte hint is the same predicate.
        raw = path.read_bytes()
        if b"<EquipmentRoster" not in raw and b"<NPCCharacter" not in raw:
            continue
        try:
            root = ET.fromstring(raw)
        except ET.ParseError:
            continue
        docs.append((path, root))
        for r in root.iter("EquipmentRoster"):
            rid = r.get("id")
            if rid:
                rosters[rid] = battle_ids(r)
                # The player's start and career kits go onto a hero at runtime and no
                # NPCCharacter names them (CareerStartingEquipmentService builds the id).
                if rid.startswith(PLAYER_ROSTER_PREFIXES):
                    for iid in rosters[rid]:
                        if iid in launchers:
                            out.setdefault(iid, set()).add(rid)
    for path, root in docs:
        if path.parent.name == "troops":
            continue
        for c in root.iter("NPCCharacter"):
            if not (c.get("occupation") in ("Lord", "Wanderer") or c.get("is_hero") == "true"):
                continue
            carried = ids_in(c)
            for es in c.iter("EquipmentSet"):
                if es.get("equipmentType") != "Civilian" and es.get("civilian") != "true":
                    carried |= rosters.get(_STRIP_REF_RE.sub("", es.get("id") or ""), set())
            for iid in carried:
                if iid in launchers:
                    out.setdefault(iid, set()).add(c.get("id"))
    xslt = md / "lords.xslt"
    if xslt.exists():
        text = xslt.read_text(encoding="utf-8-sig")
        for rid in set(re.findall(r'<EquipmentSet\s+id="([^"]+)"(?![^>]*Civilian)', text)):
            for iid in rosters.get(rid, ()):
                if iid in launchers:
                    out.setdefault(iid, set()).add("lords.xslt")
    return {k: sorted(v) for k, v in out.items()}


def ceiling_breaches(spec: dict, launchers: dict, heroes: dict[str, list[str]]) -> list[tuple[Launcher, int, list[str]]]:
    """(launcher, ceiling, carriers) for every hero-reachable launcher above spec.hero_ceiling."""
    ceiling = spec.get("hero_ceiling") or {}
    out = []
    for iid in sorted(heroes):
        rec = launchers.get(iid)
        cap = ceiling.get(rec.cls) if rec else None
        if rec is not None and cap is not None and rec.damage > int(cap):
            out.append((rec, int(cap), heroes[iid]))
    return out


# --------------------------------------------------------------------------- #
# Reach estimate (reports only)                                                 #
# --------------------------------------------------------------------------- #
_range_cache: dict[int, float] = {}


def flight_range(speed: int, dt: float = 0.002) -> float:
    """Metres a missile launched at `speed` flies on flat ground at its best elevation, under
    the engine's drag (speed -= k * speed^2 * dt) and gravity. An estimate for reports: the
    native GetMissileRange is closed, so treat these as relative."""
    speed = int(speed)
    if speed in _range_cache:
        return _range_cache[speed]
    best = 0.0
    for deg in range(25, 50):
        ang = math.radians(deg)
        vx, vz = speed * math.cos(ang), speed * math.sin(ang)
        x = z = 0.0
        while z >= 0:
            s = math.hypot(vx, vz)
            d = AIR_FRICTION_ARROW * s * dt
            vx -= vx * d
            vz -= vz * d
            vz -= GRAVITY * dt
            x += vx * dt
            z += vz * dt
        best = max(best, x)
    _range_cache[speed] = best
    return best
