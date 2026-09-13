#!/usr/bin/env python3
"""The ranged range ladder: the one place that knows how a troop's reach is ordered (#582).

WHY
---
An archer's reach is its launcher's `missile_speed` and nothing else. Verified in the 1.4.8
decompile: `Mission.cs:4943` takes the wielded bow's `GetModifiedMissileSpeedForCurrentUsage()`
(the arrow's own `missile_speed="10"` is a dead field), `SandboxAgentStatCalculateModel.cs:978`
pins `MissileSpeedMultiplier` at 1 for bows (only Throwing perks and wet weather move it), and
`AgentStatCalculateModel.cs:159-223` shows Bow skill feeding accuracy, shot cadence, lead error
and, mounted only, the fraction of max range the AI opens fire at. Nothing in the skill is range.

On 2026-09-12 the 227 troops carrying a bow or crossbow had no order at all: six trees handed a
higher tier a slower launcher, seven kingdoms' militia carried vanilla `noble_long_bow` over
their own regulars, and Dol Guldur outranged Gondor.

THE TWO RULES (per launcher class, Bow and Crossbow never compared with each other)
-----------------------------------------------------------------------------------
1. Inside a LINE, a lower tier is never faster than a higher tier.
2. Inside a BAND, a better-ranked line is never slower than a worse-ranked one.

Both come from one grid: `speed = band_base[band] + rank_step * (n_lines - rank)`. A band is a
range of engine tiers (E T0-2, R T3-4, V T5-6, X T7-8, C T9-10); a line is a troop file, or an
id prefix inside one, in kingdom rank order; every cell is a generated item
`ladder_<line>_<bow|xbow>_<band>` cloned from the line's own donor bow. The spec is
tools/ranged_ladders.json. This module is imported by the two tools and by the validator gate
(`RANGED_LADDER_INVERSION` in tools/taom_schema.py), so all three read the same functions and
cannot disagree.

No CLI. Reports and writes live in tools/rebalance_ranged_ladders.py and
tools/generate_ranged_ladder_items.py.
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
BAND_NUMERAL = {"E": "I", "R": "II", "V": "III", "X": "IV", "C": "V"}
# TAOM's TaomCharacterStatsModel.MaxCharacterTier override (Main/Features/TroopProgression), not
# an engine constant: vanilla DefaultCharacterStatsModel caps the same formula at 6.
MAX_TIER = 10
ID_PREFIX = "ladder_"

# The engine's missile flight, from managed_core_parameters.xml (AirFrictionArrow, shared by
# bows and crossbows per ItemObject.GetAirFrictionConstant) and MBGlobals.Gravity. The native
# GetMissileRange is not readable, so this is an estimate for reports, never a gate input.
GRAVITY = 9.806
AIR_FRICTION_ARROW = 0.003

_ITEM_REF_RE = re.compile(r'^Item\.')
_LAUNCHER_HINT_RE = re.compile(rb'weapon_class\s*=\s*"(?:Bow|Crossbow)"')


class LadderError(Exception):
    """A condition a run must not paper over: a spec that contradicts itself, a troop whose
    line declares no donor for the class it carries, an id the index cannot see."""


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


@dataclass
class RangedTroop:
    id: str
    file: str           # troops_<culture>.xml basename
    culture: str        # the <culture> token of the file name
    level: int
    tier: int
    group: str          # default_group
    sets: list          # battle sets, each {slot: item id} over Item0..Item3 only
    upgrades: list
    skills: dict = field(default_factory=dict)


@dataclass(frozen=True)
class Inversion:
    kind: str           # "tier" | "rank"
    cls: str
    scope: str          # the line (tier rule) or the band (rank rule) the pair sits in
    low: str            # the troop that should be slower (lower tier, or worse rank)
    low_tier: int
    low_line: str
    low_speed: int
    high: str           # the troop that should be at least as fast
    high_tier: int
    high_line: str
    high_speed: int

    @property
    def gap(self) -> int:
        return self.low_speed - self.high_speed


@dataclass
class Group:
    kind: str
    cls: str
    scope: str
    count: int
    worst: Inversion


@dataclass(frozen=True)
class LadderItem:
    id: str
    line: str
    cls: str
    band: str
    speed: int
    donor: str
    folder: str


@dataclass(frozen=True)
class Edit:
    file: str           # absolute path of the troop file
    troop: str
    slot: str
    old: str
    new: str
    cls: str
    band: str
    line: str


# --------------------------------------------------------------------------- #
# Tiers, bands, grid                                                            #
# --------------------------------------------------------------------------- #
def engine_tier(level: int) -> int:
    """clamp(ceil((level - 5) / 5), 0, MaxCharacterTier), as CharacterObject.Tier computes it
    and as taom_schema.Validator._troop_tier mirrors it (a test pins the two together)."""
    raw = -((5 - int(level)) // 5)
    return max(0, min(raw, MAX_TIER))


def load_spec(path: Path | str = DEFAULT_SPEC) -> dict:
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def band_order(spec: dict) -> list[str]:
    """Bands sorted by their first tier."""
    return sorted(spec["bands"], key=lambda b: spec["bands"][b][0])


def band_of(tier: int, spec: dict) -> str:
    for band, (lo, hi) in spec["bands"].items():
        if lo <= tier <= hi:
            return band
    raise LadderError(f"tier {tier} is in no band of the spec")


def rank_of(line_id: str, spec: dict) -> int:
    """1 is the best archers."""
    for i, line in enumerate(spec["lines"]):
        if line["id"] == line_id:
            return i + 1
    raise LadderError(f"line {line_id!r} is not in the spec")


def grid_speed(line_id: str, band: str, spec: dict) -> int:
    n = len(spec["lines"])
    return int(spec["band_base"][band]) + int(spec["rank_step"]) * (n - rank_of(line_id, spec))


def ladder_id(line_id: str, cls: str, band: str) -> str:
    return f"{ID_PREFIX}{line_id}_{CLASS_TOKEN[cls]}_{band.lower()}"


def line_spec(line_id: str, spec: dict) -> dict:
    for line in spec["lines"]:
        if line["id"] == line_id:
            return line
    raise LadderError(f"line {line_id!r} is not in the spec")


def donor_for(line: dict, cls: str, band: str) -> str | None:
    by_band = (line.get("donor_by_band") or {}).get(cls) or {}
    return by_band.get(band) or (line.get("donor") or {}).get(cls)


def validate_spec(spec: dict, launchers: dict | None = None, cultures: set | None = None) -> list[str]:
    """Every way the spec can contradict itself, as one string each. With a launcher index the
    donors are checked too (present, and of the class they are cloned for); with the set of
    troop-file culture tokens actually on disk, every `files` token must name one of them (a
    typo like `rhun` for `rhun_new` is otherwise a line with no troops, which reads as clean)."""
    problems: list[str] = []
    bands = spec.get("bands") or {}
    base = spec.get("band_base") or {}
    if set(bands) != set(base):
        problems.append(f"bands {sorted(bands)} and band_base {sorted(base)} name different bands")
    covered: dict[int, str] = {}
    for band, rng in bands.items():
        lo, hi = int(rng[0]), int(rng[1])
        for t in range(lo, hi + 1):
            if t in covered:
                problems.append(f"tier {t} is in both band {covered[t]} and band {band}")
            covered[t] = band
    for t in range(0, MAX_TIER + 1):
        if t not in covered:
            problems.append(f"tier {t} is in no band")
    order = sorted(bands, key=lambda b: int(bands[b][0])) if bands else []
    try:
        bases = {b: int(base[b]) for b in base}
    except (TypeError, ValueError):
        bases = {}
        problems.append(f"band_base values must be integers: {base}")
    for lo_band, hi_band in zip(order, order[1:]):
        if lo_band in bases and hi_band in bases and bases[hi_band] <= bases[lo_band]:
            problems.append(f"band_base is not increasing: {lo_band}={base[lo_band]} >= {hi_band}={base[hi_band]}")
    try:
        if int(spec.get("rank_step", 0)) <= 0:
            problems.append("rank_step must be a positive integer")
    except (TypeError, ValueError):
        problems.append("rank_step must be a positive integer")
    seen: set[str] = set()
    claimed_files: dict[str, str] = {}
    prefixes_seen: list[tuple[str, str]] = []   # (prefix, line id)
    for line in spec.get("lines") or []:
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
            # A file may be claimed by several lines only when the earlier claims carry
            # prefixes (the specials carve troops out of a file another line owns whole).
            owner = claimed_files.get(f)
            if owner is not None and not line_spec(owner, spec).get("prefixes"):
                problems.append(f"file {f!r} is claimed whole by both {owner!r} and {lid!r}")
            if not line.get("prefixes"):
                claimed_files[f] = lid
        donors = line.get("donor") or {}
        if not donors:
            problems.append(f"line {lid!r} declares no donor")
        for cls, donor in donors.items():
            if cls not in CLASSES:
                problems.append(f"line {lid!r} declares donor class {cls!r}; only Bow and Crossbow are ladders")
            elif launchers is not None:
                _check_donor(lid, cls, donor, launchers, problems)
        for cls, per_band in (line.get("donor_by_band") or {}).items():
            if cls not in donors:
                problems.append(f"line {lid!r} has donor_by_band for {cls!r} but no default donor")
            for band, donor in per_band.items():
                if band not in bands:
                    problems.append(f"line {lid!r} donor_by_band names unknown band {band!r}")
                elif launchers is not None and cls in CLASSES:
                    _check_donor(lid, cls, donor, launchers, problems)
    return problems


def _check_donor(lid, cls, donor, launchers, problems):
    rec = launchers.get(donor)
    if rec is None:
        problems.append(f"line {lid!r}: donor {donor!r} is not in the launcher index")
    elif rec.cls != cls:
        problems.append(f"line {lid!r}: donor {donor!r} is a {rec.cls}, cloned for {cls}")


# --------------------------------------------------------------------------- #
# Lines                                                                         #
# --------------------------------------------------------------------------- #
def line_of(troop: RangedTroop, spec: dict) -> str | None:
    """Prefix claims first, in rank order, then whole-file claims. None when nothing claims
    the troop, which the gate reports rather than drops."""
    for line in spec["lines"]:
        if any(troop.id.startswith(p) for p in line.get("prefixes") or []):
            if not line.get("files") or troop.culture in line["files"]:
                return line["id"]
    for line in spec["lines"]:
        if line.get("prefixes"):
            continue
        if troop.culture in (line.get("files") or []):
            return line["id"]
    return None


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
                            file=path.name)
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


def _is_civilian(elem) -> bool:
    return elem.get("civilian") == "true" or elem.get("equipmentType") == "Civilian"


def load_ranged_troops(moduledata=MODULEDATA_DIR) -> dict[str, RangedTroop]:
    """Every NPCCharacter in troops/troops_*.xml with its battle sets restricted to the four
    weapon slots. Villagers (characters/npcs_*.xml) are not on the ladder. Civilian sets never
    cross-draw with battle sets and are skipped."""
    import xml.etree.ElementTree as ET
    md = Path(moduledata)
    troops: dict[str, RangedTroop] = {}
    for path in sorted((md / "troops").glob("troops_*.xml")):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        culture = path.name[len("troops_"):-len(".xml")]
        for npc in root.iter("NPCCharacter"):
            tid = npc.get("id")
            if not tid:
                continue
            sets = []
            for es in list(npc.iter("EquipmentRoster")) + list(npc.iter("EquipmentSet")):
                if _is_civilian(es):
                    continue
                eqs = es.findall("equipment")
                if not eqs:
                    continue
                slots = {}
                for eq in eqs:
                    slot = eq.get("slot", "")
                    if slot in LAUNCHER_SLOTS:
                        slots[slot] = _ITEM_REF_RE.sub("", eq.get("id") or "")
                sets.append(slots)
            level = int(npc.get("level", "0") or 0)
            skills = {}
            for sk in npc.iter("skill"):
                try:
                    skills[sk.get("id", "")] = int(sk.get("value", "0") or 0)
                except ValueError:
                    pass
            troops[tid] = RangedTroop(
                id=tid, file=str(path), culture=culture, level=level, tier=engine_tier(level),
                group=npc.get("default_group", "") or "", sets=sets,
                upgrades=[_ITEM_REF_RE.sub("", (u.get("id") or "")).replace("NPCCharacter.", "", 1)
                          for u in npc.findall("./upgrade_targets/upgrade_target")],
                skills=skills)
    return troops


def troop_launchers(troop: RangedTroop, launchers: dict) -> dict[str, set[str]]:
    """{class: {launcher ids}} over the troop's battle sets."""
    out: dict[str, set[str]] = {}
    for st in troop.sets:
        for slot in LAUNCHER_SLOTS:
            rec = launchers.get(st.get(slot, ""))
            if rec is not None:
                out.setdefault(rec.cls, set()).add(rec.id)
    return out


def troop_speed(troop: RangedTroop, launchers: dict, cls: str) -> int | None:
    """The troop's reach for one class: the MAX missile_speed over its battle sets, because the
    engine draws each set independently and the fastest one is what the player can meet."""
    ids = troop_launchers(troop, launchers).get(cls)
    if not ids:
        return None
    return max(launchers[i].speed for i in ids)


# --------------------------------------------------------------------------- #
# The two rules                                                                 #
# --------------------------------------------------------------------------- #
def _records(troops: dict, launchers: dict, spec: dict):
    """(troop, line, band, cls, speed) for every assigned troop and class it carries."""
    out = []
    for tid in sorted(troops):
        troop = troops[tid]
        line = line_of(troop, spec)
        if line is None:
            continue
        for cls in CLASSES:
            speed = troop_speed(troop, launchers, cls)
            if speed is not None:
                out.append((troop, line, band_of(troop.tier, spec), cls, speed))
    return out


def unassigned(troops: dict, launchers: dict, spec: dict) -> list[RangedTroop]:
    """Troops carrying a launcher that no line claims. A gate that dropped them would read
    as clean for a whole new troop file."""
    return [troops[t] for t in sorted(troops)
            if line_of(troops[t], spec) is None and troop_launchers(troops[t], launchers)]


def inversions(troops: dict, launchers: dict, spec: dict) -> list[Inversion]:
    """Every pair that breaks a rule. Pure: the validator and the tool both call this."""
    recs = _records(troops, launchers, spec)
    found: list[Inversion] = []
    for a_troop, a_line, a_band, a_cls, a_speed in recs:
        for b_troop, b_line, b_band, b_cls, b_speed in recs:
            if a_cls != b_cls or a_troop.id == b_troop.id:
                continue
            # Rule 1: inside a line, a lower tier is never faster.
            if a_line == b_line and a_troop.tier < b_troop.tier and a_speed > b_speed:
                found.append(Inversion("tier", a_cls, a_line,
                                       a_troop.id, a_troop.tier, a_line, a_speed,
                                       b_troop.id, b_troop.tier, b_line, b_speed))
            # Rule 2: inside a band, a better rank is never slower.
            if (a_band == b_band and rank_of(a_line, spec) > rank_of(b_line, spec)
                    and a_speed > b_speed):
                found.append(Inversion("rank", a_cls, a_band,
                                       a_troop.id, a_troop.tier, a_line, a_speed,
                                       b_troop.id, b_troop.tier, b_line, b_speed))
    found.sort(key=lambda f: (-f.gap, f.kind, f.cls, f.scope, f.low, f.high))
    return found


def summarize(found: list[Inversion]) -> list[Group]:
    """One group per (kind, class, scope) with its worst pair, worst group first."""
    groups: dict[tuple, Group] = {}
    for f in found:
        key = (f.kind, f.cls, f.scope)
        g = groups.get(key)
        if g is None:
            groups[key] = Group(f.kind, f.cls, f.scope, 1, f)
        else:
            g.count += 1
            if f.gap > g.worst.gap:
                g.worst = f
    return sorted(groups.values(), key=lambda g: (-g.worst.gap, g.kind, g.cls, g.scope))


# --------------------------------------------------------------------------- #
# Planning                                                                      #
# --------------------------------------------------------------------------- #
def planned_items(spec: dict) -> list[LadderItem]:
    """Every band of every (line, class) the spec declares a donor for: a pure function of
    the spec, so the item set never depends on which troops happen to exist today."""
    items: list[LadderItem] = []
    for line in spec["lines"]:
        for cls in CLASSES:
            if cls not in (line.get("donor") or {}):
                continue
            for band in band_order(spec):
                items.append(LadderItem(
                    id=ladder_id(line["id"], cls, band), line=line["id"], cls=cls, band=band,
                    speed=grid_speed(line["id"], band, spec),
                    donor=donor_for(line, cls, band), folder=line["folder"]))
    return items


def planned_edits(troops: dict, launchers: dict, spec: dict) -> list[Edit]:
    """{(troop, slot, old) -> new} for every launcher slot of every battle set whose item is
    not already the troop's cell. Ammo slots are never touched, a class never changes. A troop
    whose line declares no donor for a class it carries is an error, not a skip."""
    edits: list[Edit] = []
    for tid in sorted(troops):
        troop = troops[tid]
        line = line_of(troop, spec)
        if line is None:
            continue
        band = band_of(troop.tier, spec)
        seen: set[tuple] = set()
        for st in troop.sets:
            for slot in LAUNCHER_SLOTS:
                old = st.get(slot, "")
                rec = launchers.get(old)
                if rec is None:
                    continue
                if rec.cls not in (line_spec(line, spec).get("donor") or {}):
                    raise LadderError(
                        f"{tid} carries a {rec.cls} ({old}) but line {line!r} declares no {rec.cls} donor")
                new = ladder_id(line, rec.cls, band)
                if old == new or (slot, old) in seen:
                    continue
                seen.add((slot, old))
                edits.append(Edit(troop.file, tid, slot, old, new, rec.cls, band, line))
    return edits


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
