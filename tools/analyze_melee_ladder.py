#!/usr/bin/env python3
"""Price every Armory melee weapon and report where it sits in the troop tree.

Read-only. This tool has no `--apply` and no write path into any ModuleData. It answers four
questions with measured numbers:

  1. What damage does each melee weapon actually do?
  2. Which troops carry it, and at what tier?
  3. Does a numbered line (`[Mordor] Uruk Sword I..IX`) actually get better as it climbs,
     and does a higher-tier troop ever carry a weaker weapon than a lower-tier one?
  4. Where does TAOM sit against vanilla, so "no weapon too OP" has a number behind it?

Usage:
    python tools/analyze_melee_ladder.py                 # write the report
    python tools/analyze_melee_ladder.py --stdout        # print it instead
    python tools/analyze_melee_ladder.py --top 40        # longer per-section listings

Output lands in `tools/reports/melee-balance/`, beside the armour and troop reports.

Reads the LIVE install, because the Armory is unversioned and is not in git: the repo has no
copy of `LOTRAOM_weapons.xml` or `LOTRLOME_crafting_pieces.xml` to read instead. Without the
install this tool reports that it cannot run; it never prints a clean report over no data.
"""

from __future__ import annotations

import argparse
import collections
import csv
import hashlib
import re
import statistics
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "tools"))

import melee_catalogue as mc  # noqa: E402
import melee_damage as md  # noqa: E402
from ranged_ladder import MAX_TIER, TIER_NUMERAL, TIER_NUMERAL_RE, engine_tier  # noqa: E402
import taom_schema as _ts  # noqa: E402

# Noble-line troops are judged and anchor one tier up (Mike, 2026-09-25): nobles carry better kit
# than regular troops of their level. The validator's set, so armour and melee agree on who is noble.
NOBLE_TROOPS = frozenset(_ts.Validator._NOBLE_LINE_TROOPS)

try:
    from lxml import etree as LET
except ImportError:
    LET = None

GAME = Path(r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord")
MODULES = GAME / "Modules"
TROOPS = REPO / "Main" / "_Module" / "ModuleData" / "troops"
OUT_DIR = REPO / "tools" / "reports" / "melee-balance"

ARMORY_MODULE = "LOTRLOME_Armory"
VANILLA_MODULES = {"SandBoxCore", "SandBox"}

# Numeral -> rank, the inverse of ranged_ladder.TIER_NUMERAL. Shared so the melee lines and
# the ranged ladder agree on what "II" means.
NUMERAL_RANK = {v: k for k, v in TIER_NUMERAL.items() if k}

# Sustained damage below this gap is a rounding difference, not a step down. The DPS numbers
# run roughly 15 to 170, so half a point is well inside the noise.
DPS_TOLERANCE = 0.5

# Hero and named weapons are the sanctioned "specific use-case": they are meant to sit above
# the curve. Carried over verbatim from tools/rebalance_weapons.py so the two agree on who is
# exempt. Keyed by BLADE PIECE, because that is what a restat pass edits.
HERO_BLADE_IDS = {
    "wm_anduril_sword_blade",
    "wm_glamdring_blade",
    "wm_strider_sword_blade",
    "wm_boromir_sword_blade",
    "wm_faramir_sword_blade",
    "wm_theoden_sword_blade",
    "wm_eomer_blade",
    "wm_eowyn_blade",
    "wm_witch_king_sword_blade",
    "wm_nazgul_sword_blade",
    "wm_sauron_mace_blade",
    "wm_galadriel_sword_blade",
    "wm_aranruth_sword_blade",
    "wm_aranruth_sword_ruby_blade",
    "wm_aranruth_sword_topaz_blade",
    "wm_legolas_sword_blade",
    "wm_thranduil_sword_blade",
    "wm_tuors_axe_blade",
}


class AnalysisError(Exception):
    pass


# --- troops -------------------------------------------------------------------------------


@dataclass(frozen=True)
class Troop:
    id: str
    name: str
    level: int
    culture: str
    file: str
    items: frozenset[str]
    path: str = ""
    # item id -> EVERY equipment slot it sits in. A roster swap is keyed on (slot, old item),
    # so recording only the first slot would half-apply a swap for the six melee weapons that
    # sit in two different slots on one troop (e.g. erebor_reg_skirmisher's axe in Item0 and
    # Item2), leaving the troop carrying both the old and the new weapon.
    slots_of: dict[str, frozenset[str]] = field(default_factory=dict)

    @property
    def tier(self) -> int:
        tier = engine_tier(self.level)
        return min(tier + 1, MAX_TIER) if self.id in NOBLE_TROOPS else tier


def load_troops(root: Path) -> list[Troop]:
    """Every `<NPCCharacter>` with its BATTLE equipment item ids.

    Civilian sets are skipped: a townsfolk dagger is not a statement about the troop tree.

    Both `<equipment>` and `<Equipment>` casings are matched. That is not cosmetic. Inline
    troop rosters spell the child lowercase and standalone roster files spell it uppercase,
    so matching one casing makes a gate structurally blind to a whole category while printing
    a pass (the lesson `audit_polearm_shield_parity.rosters` records).
    """
    if LET is None:
        raise AnalysisError("lxml is required")
    if not root.is_dir():
        raise AnalysisError(f"no troop directory at {root}")
    out: list[Troop] = []
    for path in sorted(root.glob("troops_*.xml")):
        try:
            tree = LET.parse(str(path), LET.XMLParser(recover=True, huge_tree=True)).getroot()
        except (LET.XMLSyntaxError, OSError):
            continue
        if tree is None:
            continue
        for node in tree.iter("NPCCharacter"):
            tid = node.get("id")
            if not tid:
                continue
            raw_level = node.get("level")
            try:
                level = int(raw_level) if raw_level else 0
            except ValueError:
                level = 0
            items: set[str] = set()
            slots: dict[str, set[str]] = {}
            for block in node.iter("Equipments"):
                for roster in block.iter("EquipmentRoster"):
                    if (roster.get("equipmentType") or "") == "Civilian":
                        continue
                    for eq in roster.iter("equipment", "Equipment"):
                        ref = eq.get("id")
                        if not ref:
                            continue
                        item_id = ref.split(".", 1)[-1]
                        items.add(item_id)
                        slot = eq.get("slot")
                        if slot:
                            slots.setdefault(item_id, set()).add(slot)
            out.append(
                Troop(
                    id=tid,
                    name=mc.strip_loc(node.get("name") or tid),
                    level=level,
                    culture=(node.get("culture") or "").replace("Culture.", ""),
                    file=path.name,
                    items=frozenset(items),
                    path=str(path),
                    slots_of={k: frozenset(v) for k, v in slots.items()},
                )
            )
    return out


# --- naming lines -------------------------------------------------------------------------


@dataclass(frozen=True)
class LineMember:
    rank: int
    numeral: str
    item: str
    name: str
    swing: int
    thrust: int
    best: int
    id_suffix: str
    swing_dps: float = 0.0
    thrust_dps: float = 0.0
    best_dps: float = 0.0


ID_SUFFIX_RE = re.compile(r"_([a-z])(\d*)$")


def line_key(name: str) -> tuple[str, str] | None:
    """Split `[Mordor] Uruk Sword III` into (`[Mordor] Uruk Sword`, `III`).

    Uses the ranged ladder's own trailing-numeral regex so both systems read the convention
    the same way.
    """
    match = TIER_NUMERAL_RE.search(name)
    if not match:
        return None
    numeral = match.group(0).strip()
    return name[: match.start()].strip(), numeral


def build_lines(priced: dict[str, mc.Priced]) -> dict[str, list[LineMember]]:
    lines: dict[str, list[LineMember]] = collections.defaultdict(list)
    for iid, p in priced.items():
        key = line_key(p.item.name)
        if key is None:
            continue
        base, numeral = key
        rank = NUMERAL_RANK.get(numeral)
        if rank is None:
            continue
        suffix = ID_SUFFIX_RE.search(iid)
        lines[base].append(
            LineMember(
                rank=rank,
                numeral=numeral,
                item=iid,
                name=p.item.name,
                swing=p.swing_damage,
                thrust=p.thrust_damage,
                best=p.best_damage,
                id_suffix=suffix.group(1) if suffix else "",
                swing_dps=p.swing_dps,
                thrust_dps=p.thrust_dps,
                best_dps=p.best_dps,
            )
        )
    for base in lines:
        lines[base].sort(key=lambda m: m.rank)
    return dict(lines)


@dataclass(frozen=True)
class LineBreak:
    line: str
    from_member: LineMember
    to_member: LineMember
    stat: str
    delta: int
    dps_from: float = 0.0
    dps_to: float = 0.0

    @property
    def dps_delta(self) -> float:
        return self.dps_to - self.dps_from

    @property
    def real(self) -> bool:
        """True when the step is worse on sustained damage too, not only on one blow.

        A step that loses damage but holds DPS is a weapon trading weight for speed, which is a
        legitimate design and not a ladder fault. Only a step that falls on BOTH is a defect.
        """
        return self.dps_to < self.dps_from - DPS_TOLERANCE


def line_regressions(lines: dict[str, list[LineMember]]) -> list[LineBreak]:
    """Every step in a numbered line where the next weapon is WEAKER than the one before it.

    Checked per attack, not on a combined number: a spear line that trades swing for thrust
    is a design choice, but a line whose thrust falls as the numeral rises is a defect.
    """
    breaks: list[LineBreak] = []
    for base, members in lines.items():
        for prev, nxt in zip(members, members[1:]):
            for stat in ("swing", "thrust"):
                a = getattr(prev, stat)
                b = getattr(nxt, stat)
                # Only compare where BOTH have the attack; 0 means "cannot do it at all".
                if a > 0 and b > 0 and b < a:
                    dps_attr = f"{stat}_dps"
                    breaks.append(
                        LineBreak(
                            base, prev, nxt, stat, b - a,
                            dps_from=getattr(prev, dps_attr),
                            dps_to=getattr(nxt, dps_attr),
                        )
                    )
    breaks.sort(key=lambda x: x.delta)
    return breaks


def id_order_conflicts(lines: dict[str, list[LineMember]]) -> list[tuple[str, str]]:
    """Lines whose id-letter order disagrees with the displayed numeral order.

    A later rename has to key on one of the two. Where they already disagree, someone has to
    decide which is the truth before anything automated touches the line.
    """
    out = []
    for base, members in lines.items():
        lettered = [m for m in members if m.id_suffix]
        if len(lettered) < 2:
            continue
        by_rank = [m.id_suffix for m in sorted(lettered, key=lambda m: m.rank)]
        if by_rank != sorted(by_rank):
            out.append((base, " ".join(f"{m.numeral}={m.id_suffix}" for m in lettered)))
    return sorted(out)


# --- roster placement ---------------------------------------------------------------------


@dataclass
class Placement:
    item: str
    wearers: list[Troop]

    @property
    def min_level(self) -> int:
        return min(t.level for t in self.wearers)

    @property
    def max_level(self) -> int:
        return max(t.level for t in self.wearers)

    @property
    def anchor_tier(self) -> int:
        """The tier the weapon is priced at: its LOWEST wearer.

        Same rule `tools/derive_armor_tiers.py` established for armour. A weapon handed to one
        level-6 militiaman is a tier-1 weapon however many elites also carry it.
        """
        return min(t.tier for t in self.wearers)

    @property
    def span(self) -> int:
        return max(t.tier for t in self.wearers) - self.anchor_tier


def place(priced: dict[str, mc.Priced], troops: list[Troop]) -> dict[str, Placement]:
    out: dict[str, Placement] = {}
    for troop in troops:
        for iid in troop.items:
            if iid in priced:
                out.setdefault(iid, Placement(iid, [])).wearers.append(troop)
    return out


@dataclass(frozen=True)
class Inversion:
    culture: str
    template: str
    high: Troop
    high_item: str
    high_dps: float
    high_damage: int
    low: Troop
    low_item: str
    low_dps: float
    low_damage: int

    @property
    def deficit(self) -> float:
        """How much sustained damage the higher-tier troop gives up.

        Ranked on DPS rather than on one blow, because a heavy slow weapon and a light fast one
        are only comparable over time.
        """
        return self.low_dps - self.high_dps

    @property
    def damage_deficit(self) -> int:
        return self.low_damage - self.high_damage


def tier_inversions(
    priced: dict[str, mc.Priced], troops: list[Troop], min_gap: int = 2, min_deficit: int = 1
) -> list[Inversion]:
    """A troop out-gunned by a troop at least `min_gap` tiers below it, in the same class.

    Compared within a culture and within a weapon template, because a mace and a spear are
    not competing and Gondor is not meant to field Mordor's gear. `min_gap` of 2 keeps
    adjacent-tier noise out: one tier apart is often a deliberate sidegrade.

    One row per (troop, class): the troop's WORST inversion. Without that collapse a single
    badly-armed capstone generates a row against every lower tier it loses to, and a tier-9
    unit alone produced five identical-looking rows that hid the rest of the list.

    Returned worst-first, so the top of the report is the biggest problem rather than the
    smallest rounding difference.
    """
    # best weapon each troop carries, per template, ranked on sustained damage
    best: dict[tuple[str, str, int], list[tuple[float, Troop, str]]] = collections.defaultdict(list)
    for troop in troops:
        per_template: dict[str, tuple[float, str]] = {}
        for iid in troop.items:
            p = priced.get(iid)
            if p is None:
                continue
            current = per_template.get(p.item.template)
            if current is None or p.best_dps > current[0]:
                per_template[p.item.template] = (p.best_dps, iid)
        for template, (dps, iid) in per_template.items():
            best[(troop.culture, template, troop.tier)].append((dps, troop, iid))

    worst: dict[tuple[str, str], Inversion] = {}
    keys = {(c, t) for c, t, _tier in best}
    for culture, template in sorted(keys):
        tiers = sorted(tier for c, t, tier in best if c == culture and t == template)
        for high_tier in tiers:
            for low_tier in tiers:
                if high_tier - low_tier < min_gap:
                    continue
                low_best = max(best[(culture, template, low_tier)], key=lambda x: x[0])
                for dps, troop, iid in best[(culture, template, high_tier)]:
                    if low_best[0] - dps < min_deficit:
                        continue
                    candidate = Inversion(
                        culture=culture,
                        template=template,
                        high=troop,
                        high_item=iid,
                        high_dps=dps,
                        high_damage=priced[iid].best_damage,
                        low=low_best[1],
                        low_item=low_best[2],
                        low_dps=low_best[0],
                        low_damage=priced[low_best[2]].best_damage,
                    )
                    key = (troop.id, template)
                    if key not in worst or candidate.deficit > worst[key].deficit:
                        worst[key] = candidate
    return sorted(worst.values(), key=lambda x: -x.deficit)


# --- vanilla envelope ---------------------------------------------------------------------


@dataclass
class Envelope:
    template: str
    count: int
    low: int
    median: int
    high: int


def envelope(priced: dict[str, mc.Priced], metric: str = "best_damage") -> dict[str, Envelope]:
    by: dict[str, list[int]] = collections.defaultdict(list)
    for p in priced.values():
        value = getattr(p, metric)
        if value > 0:
            by[p.item.template].append(int(round(value)))
    return {
        t: Envelope(t, len(v), min(v), int(statistics.median(v)), max(v))
        for t, v in sorted(by.items())
    }


# --- restat worksheet ---------------------------------------------------------------------


@dataclass
class BladeFanOut:
    blade: str
    items: list[str]
    troops: set[str]
    tiers: set[int]

    @property
    def fan(self) -> int:
        return len(self.items)


def blade_fan_out(
    priced: dict[str, mc.Priced], placements: dict[str, Placement]
) -> dict[str, BladeFanOut]:
    """Blade piece -> every weapon built on it, and every troop those weapons reach.

    The constraint that governs a restat pass. Damage lives on the blade piece, not the item,
    so editing one blade moves every weapon using it at once. A blade with a fan-out of one is
    a free edit; a blade carrying nine weapons across six tiers is a decision.
    """
    out: dict[str, BladeFanOut] = {}
    for iid, p in priced.items():
        if not p.blade_piece:
            continue
        entry = out.setdefault(p.blade_piece, BladeFanOut(p.blade_piece, [], set(), set()))
        entry.items.append(iid)
        placement = placements.get(iid)
        if placement:
            for troop in placement.wearers:
                entry.troops.add(troop.id)
                entry.tiers.add(troop.tier)
    for entry in out.values():
        entry.items.sort()
    return out


# --- report -------------------------------------------------------------------------------


def digest(path: Path) -> str:
    try:
        return hashlib.md5(path.read_bytes()).hexdigest()[:12]
    except OSError:
        return "unreadable"


def provenance_rows() -> list[tuple[str, str, str]]:
    rows = []
    for path in [
        MODULES / ARMORY_MODULE / "ModuleData" / "LOTRLOME_crafting_pieces.xml",
        MODULES / ARMORY_MODULE / "ModuleData" / "LOTRLOME_items" / "LOTRAOM_weapons.xml",
        MODULES / ARMORY_MODULE / "ModuleData" / "weapon_descriptions.xslt",
        MODULES / ARMORY_MODULE / "ModuleData" / "crafting_templates.xslt",
        MODULES / "Native" / "ModuleData" / "crafting_pieces.xml",
        MODULES / "Native" / "ModuleData" / "crafting_templates.xml",
        MODULES / "Native" / "ModuleData" / "item_usage_sets.xml",
        MODULES / "SandBoxCore" / "ModuleData" / "items" / "weapons.xml",
    ]:
        if path.is_file():
            stamp = time.strftime("%Y-%m-%d %H:%M", time.localtime(path.stat().st_mtime))
            rows.append((str(path), digest(path), stamp))
        else:
            rows.append((str(path), "MISSING", "-"))
    return rows


def fmt_table(headers: list[str], rows: list[list[str]]) -> list[str]:
    if not rows:
        return ["_none_", ""]
    out = ["| " + " | ".join(headers) + " |", "|" + "|".join("---" for _ in headers) + "|"]
    out += ["| " + " | ".join(r) + " |" for r in rows]
    out.append("")
    return out


def build_report(top: int, min_deficit: int) -> tuple[list[str], list[list], int]:
    if not MODULES.is_dir():
        raise AnalysisError(
            f"no Bannerlord install at {MODULES}. The Armory is unversioned and not in git, "
            "so there is no in-repo copy to fall back to. See "
            "docs/reference/development-machines.md."
        )

    cat = mc.load(MODULES, item_modules={ARMORY_MODULE})
    priced, failures = mc.price_all(cat)

    vanilla_cat = mc.load(MODULES, item_modules=VANILLA_MODULES)
    vanilla_priced, vanilla_failures = mc.price_all(vanilla_cat)

    troops = load_troops(TROOPS)
    placements = place(priced, troops)
    lines = build_lines(priced)
    breaks = line_regressions(lines)
    conflicts = id_order_conflicts(lines)
    inversions = tier_inversions(priced, troops, min_deficit=min_deficit)
    # The same pass at a 1-point floor, purely to state how much of the total is noise.
    all_inversions = tier_inversions(priced, troops, min_deficit=1)
    taom_env = envelope(priced)
    van_env = envelope(vanilla_priced)
    taom_dps_env = envelope(priced, "best_dps")
    van_dps_env = envelope(vanilla_priced, "best_dps")
    fan = blade_fan_out(priced, placements)

    L: list[str] = []
    A = L.append

    A("# Melee weapon ladder report")
    A("")
    A(f"Generated {time.strftime('%Y-%m-%d %H:%M')} by `tools/analyze_melee_ladder.py`.")
    A("Read-only: this tool writes no ModuleData.")
    A("")
    A("Damage here is computed, not read: Bannerlord stores no damage on a crafted weapon, it")
    A("simulates it from the pieces every load. The model is `tools/melee_damage.py`, a port of")
    A("the v1.5.3 engine. See `docs/features/melee-damage-model.md`.")
    A("")

    # 1. provenance
    A("## 1. Provenance")
    A("")
    A(
        fmt_line := f"Priced **{len(priced)}** Armory melee weapons "
        f"({len(failures)} could not be priced) and **{len(vanilla_priced)}** vanilla ones "
        f"({len(vanilla_failures)} skipped), against **{len(troops)}** troops."
    )
    del fmt_line
    A("")
    A(
        f"Pieces loaded: {len(cat.pieces)}. Templates: {len(cat.templates)}. "
        f"Weapon descriptions: {len(cat.descriptions)}. Usage sets: {len(cat.usage_sets)}."
    )
    A("")
    A("Sources, with the hash and timestamp they carried when this ran:")
    A("")
    L.extend(
        fmt_table(
            ["file", "md5 (12)", "modified"],
            [[f"`{Path(p).name}`", h, t] for p, h, t in provenance_rows()],
        )
    )
    if failures:
        A("**Weapons that could not be priced** (every one is listed; a report that quietly")
        A("skipped some would read exactly like a clean run):")
        A("")
        L.extend(fmt_table(["item", "reason"], [[f"`{i}`", r] for i, r in failures]))

    # 2. headline
    swingable = sum(1 for p in priced.values() if p.swing_usage)
    thrustable = sum(1 for p in priced.values() if p.thrust_usage)
    multimode = sum(1 for p in priced.values() if len(p.usages) > 1)
    unworn = [i for i in priced if i not in placements]
    A("## 2. The catalogue at a glance")
    A("")
    L.extend(
        fmt_table(
            ["measure", "value"],
            [
                ["melee weapons priced", str(len(priced))],
                ["equipped by at least one troop", str(len(placements))],
                ["equipped by nobody", str(len(unworn))],
                ["can swing (in some usable mode)", str(swingable)],
                ["can thrust (in some usable mode)", str(thrustable)],
                ["carry more than one usage mode", str(multimode)],
                ["numbered lines (`... I`, `... II`)", str(len(lines))],
                ["weapons inside a numbered line", str(sum(len(v) for v in lines.values()))],
            ],
        )
    )

    # 3. numbered lines
    A("## 3. Numbered lines: does the ladder actually climb?")
    A("")
    A("A line named `[Kingdom] Thing I / II / III` declares a progression. This checks whether")
    A("the weapons agree.")
    A("")
    A("Judged on **sustained damage**, not on one blow. A lighter blade that swings faster can")
    A("beat a heavier one that hits harder, so a step down in damage is only a fault when output")
    A("over time falls too. Each attack is checked separately, since a line trading swing for")
    A("thrust is a design choice; a `-` means the weapon has no such attack in any usable mode")
    A("and is not compared.")
    A("")
    real = [b for b in breaks if b.real]
    trades = [b for b in breaks if not b.real]
    A(
        f"**{len({b.line for b in real})} of {len(lines)} numbered lines genuinely regress.** "
        f"{len(real)} steps fall on BOTH one blow and sustained damage."
    )
    A("")
    A(
        f"A further **{len(trades)} steps lose damage but hold DPS**. Those are not faults: the "
        "weapon is trading weight for speed, and a lighter blade that swings faster keeps its "
        "output up. Ranking on one blow alone would have called all "
        f"{len(breaks)} of them defects."
    )
    A("")
    A(f"### Genuine regressions, worst first (top {top})")
    A("")
    L.extend(
        fmt_table(
            ["line", "step", "stat", "dmg from", "dmg to", "DPS from", "DPS to", "DPS delta"],
            [
                [
                    b.line,
                    f"{b.from_member.numeral} -> {b.to_member.numeral}",
                    b.stat,
                    str(getattr(b.from_member, b.stat)),
                    str(getattr(b.to_member, b.stat)),
                    f"{b.dps_from:.0f}",
                    f"{b.dps_to:.0f}",
                    f"{b.dps_delta:.0f}",
                ]
                for b in sorted(real, key=lambda x: x.dps_delta)[:top]
            ],
        )
    )
    A("### Damage falls but sustained output does not (speed trades, not faults)")
    A("")
    L.extend(
        fmt_table(
            ["line", "step", "stat", "dmg from", "dmg to", "DPS from", "DPS to"],
            [
                [
                    b.line,
                    f"{b.from_member.numeral} -> {b.to_member.numeral}",
                    b.stat,
                    str(getattr(b.from_member, b.stat)),
                    str(getattr(b.to_member, b.stat)),
                    f"{b.dps_from:.0f}",
                    f"{b.dps_to:.0f}",
                ]
                for b in sorted(trades, key=lambda x: -x.dps_delta)[:top]
            ],
        )
    )
    A("### Every numbered line, in full")
    A("")
    for base in sorted(lines):
        members = lines[base]
        broken = {(b.from_member.item, b.stat) for b in real if b.line == base}
        traded = {(b.from_member.item, b.stat) for b in trades if b.line == base}
        mark = "BREAKS" if broken else ("speed trade" if traded else "ok")
        A(f"**{base}** ({len(members)} steps, {mark})")
        A("")
        L.extend(
            fmt_table(
                ["numeral", "item", "swing", "sw DPS", "thrust", "th DPS", "best DPS"],
                [
                    [
                        m.numeral,
                        f"`{m.item}`",
                        str(m.swing) if m.swing else "-",
                        f"{m.swing_dps:.0f}" if m.swing_dps else "-",
                        str(m.thrust) if m.thrust else "-",
                        f"{m.thrust_dps:.0f}" if m.thrust_dps else "-",
                        f"{m.best_dps:.0f}",
                    ]
                    for m in members
                ],
            )
        )
    if conflicts:
        A("### Lines whose id order disagrees with the numeral order")
        A("")
        A("A rename has to key on one or the other. Decide which is the truth here first.")
        A("")
        L.extend(fmt_table(["line", "numeral = id letter"], [[a, b] for a, b in conflicts]))

    # 4. roster placement
    A("## 4. Where each weapon sits in the troop tree")
    A("")
    A("A weapon is anchored to its LOWEST wearer, the rule `derive_armor_tiers.py` uses for")
    A("armour: a weapon handed to one level-6 militiaman is a tier-1 weapon however many")
    A("elites also carry it. `span` is how many engine tiers it stretches across.")
    A("")
    widest = sorted(placements.values(), key=lambda p: -p.span)[:top]
    L.extend(
        fmt_table(
            ["item", "best dmg", "best DPS", "wearers", "levels", "anchor tier", "span"],
            [
                [
                    f"`{p.item}`",
                    str(priced[p.item].best_damage),
                    f"{priced[p.item].best_dps:.0f}",
                    str(len(p.wearers)),
                    f"{p.min_level}-{p.max_level}",
                    str(p.anchor_tier),
                    str(p.span),
                ]
                for p in widest
            ],
        )
    )

    # 5. tier inversions
    A("## 5. Tier inversions: higher-tier troops carrying weaker weapons")
    A("")
    A("Within one culture and one weapon class, a troop whose best weapon is beaten by a troop")
    A("at least two engine tiers below it. Two tiers, not one, because an adjacent-tier")
    A("sidegrade is usually deliberate. One row per troop and class, showing its worst case.")
    A("")
    A("Ranked on sustained damage, not on one blow: a light fast weapon and a heavy slow one are")
    A("only comparable over time. The damage column is kept beside it so a case where the two")
    A("disagree is visible.")
    A("")
    A(
        f"**{len(inversions)} inversions at {min_deficit} DPS or worse** "
        f"(of {len(all_inversions)} at any margin, so "
        f"{len(all_inversions) - len(inversions)} sit within {min_deficit} DPS and are "
        "noise rather than a ladder fault). Worst first."
    )
    A("")
    L.extend(
        fmt_table(
            ["culture", "class", "higher troop", "T", "its weapon", "DPS", "dmg", "beaten by", "T", "weapon", "DPS", "dmg", "DPS deficit"],
            [
                [
                    inv.culture,
                    inv.template,
                    f"`{inv.high.id}`",
                    str(inv.high.tier),
                    f"`{inv.high_item}`",
                    f"{inv.high_dps:.0f}",
                    str(inv.high_damage),
                    f"`{inv.low.id}`",
                    str(inv.low.tier),
                    f"`{inv.low_item}`",
                    f"{inv.low_dps:.0f}",
                    str(inv.low_damage),
                    f"{inv.deficit:.0f}",
                ]
                for inv in inversions[:top]
            ],
        )
    )

    # 6. vanilla envelope
    A("## 6. Against vanilla")
    A("")
    A("Vanilla weapons are crafted items too, so the same model prices them. This is what")
    A('"stay within vanilla standards" means numerically.')
    A("")
    L.extend(
        fmt_table(
            ["class", "n van/TAOM", "vanilla dmg low/med/high", "TAOM dmg low/med/high", "vanilla DPS low/med/high", "TAOM DPS low/med/high", "TAOM over vanilla DPS max"],
            [
                [
                    t,
                    f"{van_env[t].count if t in van_env else 0}/{e.count}",
                    f"{van_env[t].low}/{van_env[t].median}/{van_env[t].high}" if t in van_env else "-",
                    f"{e.low}/{e.median}/{e.high}",
                    f"{van_dps_env[t].low}/{van_dps_env[t].median}/{van_dps_env[t].high}" if t in van_dps_env else "-",
                    f"{taom_dps_env[t].low}/{taom_dps_env[t].median}/{taom_dps_env[t].high}" if t in taom_dps_env else "-",
                    str(
                        sum(
                            1
                            for p in priced.values()
                            if p.item.template == t
                            and t in van_dps_env
                            and p.best_dps > van_dps_env[t].high
                        )
                    )
                    if t in van_dps_env
                    else "?",
                ]
                for t, e in taom_env.items()
            ],
        )
    )
    over = [
        p
        for p in priced.values()
        if p.item.template in van_dps_env and p.best_dps > van_dps_env[p.item.template].high
    ]
    heroes = [p for p in over if p.blade_piece in HERO_BLADE_IDS]
    rest = sorted([p for p in over if p.blade_piece not in HERO_BLADE_IDS], key=lambda p: -p.best_dps)
    A(
        f"**{len(over)} weapons out-sustain every vanilla weapon of their class.** "
        f"{len(heroes)} are named hero weapons, which are meant to. {len(rest)} are not."
    )
    A("")
    A(f"### Above the vanilla DPS ceiling and not a hero weapon (top {top})")
    A("")
    L.extend(
        fmt_table(
            ["item", "class", "best DPS", "vanilla DPS max", "over by", "best dmg", "equipped by"],
            [
                [
                    f"`{p.item.id}`",
                    p.item.template,
                    f"{p.best_dps:.0f}",
                    str(van_dps_env[p.item.template].high),
                    f"{p.best_dps - van_dps_env[p.item.template].high:.0f}",
                    str(p.best_damage),
                    str(len(placements[p.item.id].wearers)) if p.item.id in placements else "0",
                ]
                for p in rest[:top]
            ],
        )
    )

    # 7. orphans
    A("## 7. Weapons no troop equips")
    A("")
    A(
        f"**{len(unworn)} of {len(priced)} melee weapons reach no troop.** They are merchant and"
    )
    A("loot stock. They are also the stock a roster pass can draw on without authoring anything.")
    A("")
    L.extend(
        fmt_table(
            ["item", "class", "best dmg", "best DPS", "name"],
            [
                [f"`{i}`", priced[i].item.template, str(priced[i].best_damage),
                 f"{priced[i].best_dps:.0f}", priced[i].item.name]
                for i in sorted(unworn, key=lambda i: -priced[i].best_dps)[:top]
            ],
        )
    )

    # 8. restat worksheet
    A("## 8. Restat worksheet: what one blade edit moves")
    A("")
    A("Damage lives on the blade piece, never on the item, so editing a blade moves every")
    A("weapon built on it at once. A fan-out of 1 is a free edit. A blade carrying nine weapons")
    A("across six tiers is a decision, and this is the table that says which is which.")
    A("")
    shared = sorted([f for f in fan.values() if f.fan > 1], key=lambda f: -f.fan)
    A(
        f"**{len(fan)} blade pieces back {len(priced)} weapons.** {len(shared)} are shared by more"
        f" than one weapon."
    )
    A("")
    L.extend(
        fmt_table(
            ["blade piece", "weapons", "troops reached", "tiers", "hero?"],
            [
                [
                    f"`{f.blade}`",
                    f"{f.fan} (" + ", ".join(f"`{i}`" for i in f.items[:4]) + ("...)" if f.fan > 4 else ")"),
                    str(len(f.troops)),
                    ",".join(str(t) for t in sorted(f.tiers)) or "-",
                    "yes" if f.blade in HERO_BLADE_IDS else "",
                ]
                for f in shared[:top]
            ],
        )
    )

    # 9. where the two metrics disagree
    A("## 9. Where one blow and sustained damage disagree")
    A("")
    A("The cases that make DPS worth computing. Each row is a pair in the same weapon class")
    A("where the weapon with the bigger blow is the WORSE weapon over time, because the other")
    A("one swings enough faster to make up the gap. Ranking a ladder on one blow gets these")
    A("backwards.")
    A("")
    upsets: list[tuple[float, mc.Priced, mc.Priced]] = []
    by_class: dict[str, list[mc.Priced]] = collections.defaultdict(list)
    for p in priced.values():
        if p.best_dps > 0:
            by_class[p.item.template].append(p)
    for template, group in by_class.items():
        for a in group:
            for b in group:
                # a hits harder per blow, b wins on sustained output
                if a.best_damage > b.best_damage and b.best_dps > a.best_dps:
                    upsets.append((b.best_dps - a.best_dps, a, b))
    upsets.sort(key=lambda x: -x[0])
    A(f"**{len(upsets)} such pairs.** Top {top} by how far the lighter weapon pulls ahead.")
    A("")
    L.extend(
        fmt_table(
            ["class", "bigger blow", "dmg", "DPS", "loses to", "dmg", "DPS", "DPS gap"],
            [
                [
                    a.item.template,
                    f"`{a.item.id}`",
                    str(a.best_damage),
                    f"{a.best_dps:.0f}",
                    f"`{b.item.id}`",
                    str(b.best_damage),
                    f"{b.best_dps:.0f}",
                    f"{gap:.0f}",
                ]
                for gap, a, b in upsets[:top]
            ],
        )
    )
    A("**How the cycle time is modelled.** `raw_speed = CONST / simulated_time` by construction")
    A("(`Crafting.cs:330`, `:360`), so attack time inverts straight back out of the speed stat and")
    A("the RATIO between two weapons is exact. Added to it is the attacker's recovery from")
    A("`managed_core_parameters.xml`: **0.1 s after a swing, 0.67 s after a thrust**, which is why")
    A("a thrust-only weapon gives up so much sustained damage. The absolute seconds assume the")
    A("native attack animation runs for the time the muscle simulation produced, which is not")
    A("established in managed code, so read DPS as a relative index. Comparing within one attack")
    A("type is robust; comparing a swing against a thrust leans on those two constants.")
    A("")
    A("Excluded on purpose, because none of it is a property of the weapon: the agent's")
    A("`SwingSpeedMultiplier` (skill scales every weapon alike, so it cancels), where in the arc")
    A("the blow lands (`SpeedGraphFunction`), movement bonuses, and the target's armour.")
    A("")

    # CSV rows
    csv_rows: list[list] = []
    for iid in sorted(priced):
        p = priced[iid]
        pl = placements.get(iid)
        key = line_key(p.item.name)
        csv_rows.append(
            [
                iid,
                p.item.name,
                p.item.culture,
                p.item.template,
                p.primary.description,
                p.swing_usage.description if p.swing_usage else "",
                p.swing_damage,
                p.stats.swing_type,
                p.swing_speed,
                p.thrust_usage.description if p.thrust_usage else "",
                p.thrust_damage,
                p.stats.thrust_type,
                p.thrust_speed,
                round(p.swing_dps, 1),
                round(p.thrust_dps, 1),
                round(p.best_dps, 1),
                p.best_damage,
                round(p.stats.reach * 100),
                p.stats.weight,
                p.stats.swing_factor,
                p.stats.thrust_factor,
                p.blade_piece,
                len(fan[p.blade_piece].items) if p.blade_piece in fan else 0,
                p.tier,
                key[0] if key else "",
                key[1] if key else "",
                len(pl.wearers) if pl else 0,
                pl.min_level if pl else "",
                pl.max_level if pl else "",
                pl.anchor_tier if pl else "",
                pl.span if pl else "",
                "yes" if p.blade_piece in HERO_BLADE_IDS else "",
            ]
        )
    return L, csv_rows, len(failures)


CSV_HEADERS = [
    "item_id", "name", "culture", "template", "primary_usage",
    "swing_usage", "swing_damage", "swing_type", "swing_speed",
    "thrust_usage", "thrust_damage", "thrust_type", "thrust_speed",
    "swing_dps", "thrust_dps", "best_dps", "best_damage", "reach_cm", "weight", "swing_factor", "thrust_factor",
    "blade_piece", "blade_fan_out", "piece_tier",
    "line", "numeral", "wearers", "min_level", "max_level", "anchor_tier", "tier_span", "hero",
]


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--stdout", action="store_true", help="print the report instead of writing it")
    ap.add_argument("--top", type=int, default=25, help="rows per listing (default 25)")
    ap.add_argument(
        "--min-deficit",
        type=int,
        default=5,
        help="ignore tier inversions closer than this many damage (default 5)",
    )
    args = ap.parse_args()

    try:
        lines, csv_rows, failures = build_report(args.top, args.min_deficit)
    except (AnalysisError, mc.CatalogueError, md.MeleeDamageError) as exc:
        print(f"SKIPPED: {exc}", file=sys.stderr)
        return 2

    text = "\n".join(lines) + "\n"
    if args.stdout:
        print(text)
        return 0

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    report = OUT_DIR / "LADDER.md"
    report.write_text(text, encoding="utf-8")
    table = OUT_DIR / "melee_weapons.csv"
    with table.open("w", newline="", encoding="utf-8") as fh:
        writer = csv.writer(fh)
        writer.writerow(CSV_HEADERS)
        writer.writerows(csv_rows)

    print(f"wrote {report} ({len(lines)} lines)")
    print(f"wrote {table} ({len(csv_rows)} weapons)")
    if failures:
        print(f"NOTE: {failures} weapon(s) could not be priced; they are listed in the report.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
