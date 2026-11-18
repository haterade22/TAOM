#!/usr/bin/env python3
"""Generate the armour band clones and upgrades: a top-tier troop's armour never priced by a low troop.

WHY
---
The armour restat prices each Armory piece at the band of its lowest battle wearer
(rebalance_armor._roster_first_plan), so a low troop that wears the same piece as a troop of tier 7 to
10 drags that piece down for the top-tier troop: `mirkwood_body` went from body 25 to 8 on 2026-11-14
because two level 11 militia wear it beside the level 41 to 51 wardens, guardians and palace guards,
and three level 36 goblins wear `sk_md_orc_inf_chest_med_b` ("Mordor Orc Chain+Scale I") at the medium
band. The scope (the maintainer, 2026-11-17) is a piece worn in battle by a troop of level
rebalance_armor.TOP_TIER_LEVEL (36) or higher and by a troop of level 30 or lower
(rebalance_armor.shared_down_pieces). Every other piece keeps the restat's lowest-wearer price.

TWO TREATMENTS (treatment(), one function)
------------------------------------------
  A  DUPLICATE: the piece is an elite troop's own armour that low troops borrow (an `_elite_` or
     `_lord_` id, or a named piece with no tier token, such as the palace guard chest or "Gold Greaves
     III"). Each band of its low wearers wears a clone; the source keeps only elite-band wearers, so the
     restat prices it at the elite band with no change to its rule.
  B  UPGRADE: the piece is a low or mid piece (a `_light_`, `_med_` or `_heavy_` id token, or a name that
     ends in I or II or says Infantry, Militia or Levy). Its level 36+ wearers swap to a stronger piece of
     the same kingdom line and slot (pick_upgrade), and the piece keeps its price for its low wearers. No
     clone. When no piece qualifies, the piece falls back to A, and the dry run lists it.

WHAT A CLONE IS
---------------
A copy of one Armory armour item (mesh, culture, weight, appearance, flags, material_type, the name
with its key and English text, every other attribute and child) with:
  * the id taom_band_<lt|md|hv>_<source id> (rebalance_armor.band_clone_id; the code names the band
    without a tier token, so mesh_tier_of, kingdom_key and the lord-line token read it as its source);
  * is_merchandise="false";
  * armour and modifier_group at the band of its wearers, by the restat's own arithmetic
    (rebalance_armor.restat_armour; a generator-owned source as its generator prices it), never above
    the source at the elite band.
One clone per (source, band); a wearer's band is its anchor (a noble line a band up), with the ladder
exemptions of derive_armor_tiers.parse_rosters.

WHAT AN UPGRADE IS
------------------
For each level 36+ wearer (by anchor; exempt troops and pairs keep their piece) the piece is replaced in
the slot of every battle set by one replacement, the same for all of them. A candidate is a live Armory
piece of the same slot file, Type and kingdom line (rebalance_armor.kingdom_key). It is not civilian,
troll, hero-named, Mallorn (priced a band above by its generator) or a clone, and not lord armour (a
lord-line token with no tier or the lord tier, as fix_lord_kits.lord_pool marks it) unless the source is
of a lord line itself. The ladder allows its mesh tier for every such wearer (or it has none), the tier
is above the source's own, and no troop of level 30 or lower wears it (so it prices at the elite band and
is never in scope itself). On the values the restat will write (the replacement at the elite band, the
source at the band of its lowest wearer) it must be at least the source on every region and above it on
the primary, and no wearer may pass rebalance_armor.WORN_ARMOUR_CEILING with each slot drawn from any
battle set. The pick is the candidate of the longest id prefix shared with the source (its look family),
then a tier the ladder allows over one it does not, then the tier nearest the ladder (inside it the
lowest, the smallest step up; outside it the highest), then the id. A colour variant of the source (the same name without its colour) is never an upgrade.

EVERY OTHER WEARER (review 2026-11-17)
-------------------------------------
The restat raises an A source to the elite band, so any set below that band that still names it would
rise with it (the Mirkwood recruit enlistment kits from 23 to 90). Every set of ModuleData that names a
clone's source and is not a battle set of a level 36+ troop takes the clone of its own band (scan_text,
planned_for): a troop's civilian sets by the troop's rule; an NPC by its level; an enlistment kit by its
rank (enlist_rank_band); a wanderer kit, a character creation or child template, a career kit and any
unlevelled NPC at the light band. A lord (occupation Lord, the lord template files) keeps the source. The
clone bands of a source cover every such wearer (wearer_bands), and the validator warns
BAND_CLONE_SOURCE_WORN_LOW for any that still names the source (sources_worn_low).

WHAT IT WRITES (the unversioned Armory is only read)
---------------------------------------------------
  tools/armour_band_clones.json                                    the plan (the pin --check compares)
  Main/_Module/ModuleData/armour_band_clones/armour_band_clones.xml  one Item per clone, CRLF
  Main/_Module/SubModule.xml                                       one Items XmlNode, added once
  Main/_Module/ModuleData/troops/troops_*.xml, fords_of_war/fords_troops.xml
      battle sets (fix_upgrade_armour_regressions.write_changes), by armour_for_troop. A troop
      file a generator writes whole (fix_melee_ladder.GENERATED_TROOP_FILES) is not written here:
      realm_troops.py, generate_lothlorien_troops.py and generate_snow_orc_troops.py call
      armour_for_troop as they render, and the tool lists the swaps still owed until that generator runs
      with --apply. A Fords of War copy (fow_<campaign id>) follows its campaign troop at its own level.
  every other set the scan finds (write_worn_swaps): a troop's civilian sets, characters/*.xml,
      named_companions, equipmentsets/*.xml (the enlistment file too: generate_enlistment_rosters.py
      applies the same rule as it renders, kit_at_rank). Only the id in the slot changes.
A clone is never dropped: one the plan no longer needs stays in the file as `retired` (a save or a mod
that names it keeps the item), and its wearers go back to the source. The kit tools keep a clone as the
troop wears it; the class table classes every clone `named`.

USAGE
    python tools/generate_armour_band_clones.py            # dry run: the plan and the table of pieces
    python tools/generate_armour_band_clones.py --apply    # write
    python tools/generate_armour_band_clones.py --check    # exit 1 on drift

Exit codes: 0 clean or written; 1 drift (--check), a bad spec, or a piece of the shipped plan that is not live
armour of the Armory read (--apply writes nothing then); 2 no Armory install or no slot file in it (nothing
checked); 3 a crash. After --apply: the steps it prints under NEXT, then the sequence in
docs/features/armor-balance.md, "Armour band clones and upgrades".
"""
from __future__ import annotations

import argparse
import collections
import dataclasses
import functools
import json
import os
import re
import sys
import traceback
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "tools"))

import rebalance_armor as ra  # noqa: E402

MODULEDATA = REPO / "Main" / "_Module" / "ModuleData"
SPEC = REPO / "tools" / "armour_band_clones.json"
OUT_FILE = MODULEDATA / "armour_band_clones" / "armour_band_clones.xml"
FORDS_TROOPS = MODULEDATA / "fords_of_war" / "fords_troops.xml"
FORDS_PREFIX = "fow_"
SUBMODULE = REPO / "Main" / "_Module" / "SubModule.xml"
SUBMODULE_ANCHOR = '      <XmlName id="Items" path="lord_armour/lord_armour_variants"/>'
SUBMODULE_PATH = "armour_band_clones/armour_band_clones"
GAME_TYPES = ("Campaign", "CampaignStoryMode", "CustomGame", "EditorGame")
ARMOUR_STATS = ("head_armor", "body_armor", "arm_armor", "leg_armor")
MALLORN_PREFIX = "sk_ll_ml_"
LOC_BACKLOG = REPO / "tools" / "loc_inline_backlog.json"
LOC_BACKLOG_KEY = "armour_band_clones/armour_band_clones.xml"
HEADER = ("<!-- GENERATED by tools/generate_armour_band_clones.py from tools/armour_band_clones.json and the live"
          " LOTRLOME_Armory. Do not edit by hand. Each item copies an Armory piece that a tier 7 to 10 troop wears"
          " (id taom_band_<lt|md|hv>_<source>) at the band of the low troops that wear it:"
          " docs/features/armor-balance.md, Armour band clones and upgrades -->")
DESCRIPTION = [
    "Armour band clones and upgrades (2026-11-17): tools/generate_armour_band_clones.py writes this file. Each",
    "piece here is an Armory armour piece that a troop of level 36 or higher and a troop of level 30 or lower",
    "wear in battle. clones: source -> the bands of its low wearers, which wear taom_band_<lt|md|hv>_<source>.",
    "upgrades: source -> the stronger piece its level 36+ wearers (troops) wear instead. fallback: B pieces",
    "(upgrade by the classifier) that found no stronger piece and are cloned instead. retired: clones no set",
    "wears any more, kept in the clone file. Do not edit by hand: --apply rewrites it from the rosters, and",
    "--check fails when it is stale. docs/features/armor-balance.md.",
]


class CloneError(Exception):
    pass


class NoArmory(CloneError):
    """No Armory slot file could be read: nothing was checked (exit 2)."""


# =============================================================================
# The classifier
# =============================================================================

_LOW_NUMERAL_RE = re.compile(r"(?:^|[\s\-])(?:I|II)$")
_LOW_WORD_RE = re.compile(r"\b(?:infantry|militia|levy)\b", re.IGNORECASE)


def bare_name(name):
    """A display name without its [culture] tag and colour suffix: the colour variants of one piece share it."""
    return ra.COLOR_VARIANT_RE.sub("", re.sub(r"^\[[^\]]*\]\s*", "", name or "")).strip()


def treatment(item_id, name):
    """'A' (duplicate for the low wearers) or 'B' (upgrade the level 36+ wearers) for an in-scope piece.
    The id's mesh tier decides first: `_elite_` or `_lord_` is A, `_light_`, `_med_` or `_heavy_` is B.
    A piece with no token is B when its name, colour suffix and culture tag aside, ends in the numeral I or
    II or says Infantry, Militia or Levy, and A otherwise (a named piece)."""
    tier = ra.mesh_tier_of(item_id)
    if tier in ("elite", "lord"):
        return "A"
    if tier in ("light", "medium", "heavy"):
        return "B"
    bare = bare_name(name)
    return "B" if _LOW_NUMERAL_RE.search(bare) or _LOW_WORD_RE.search(bare) else "A"


# =============================================================================
# The spec
# =============================================================================

def check_spec(spec):
    """Raise CloneError unless the spec is {"clones": {source: [band, ...]}, "upgrades": {source: {"to": id,
    "troops": [ids]}}, "fallback": [sources]} with known, distinct bands and no source in both tables."""
    clones = spec.get("clones") if isinstance(spec, dict) else None
    upgrades = spec.get("upgrades", {}) if isinstance(spec, dict) else None
    if not isinstance(clones, dict) or not isinstance(upgrades, dict):
        raise CloneError("the spec needs a 'clones' and an 'upgrades' table")
    for source, bands in clones.items():
        if not isinstance(bands, list) or not bands:
            raise CloneError(f"{source}: a source needs a list of one band or more")
        if len(set(bands)) != len(bands):
            raise CloneError(f"{source}: a band is listed twice")
        for band in bands:
            if band not in ra.BAND_CLONE_CODES:
                raise CloneError(f"{source}: {band!r} is no clone band ({', '.join(ra.BAND_CLONE_CODES)})")
    for source, row in upgrades.items():
        if source in clones:
            raise CloneError(f"{source} is both cloned and upgraded")
        if not isinstance(row, dict) or not row.get("to") or not row.get("troops") or row["to"] == source:
            raise CloneError(f"{source}: an upgrade is {{\"to\": another piece, \"troops\": [ids]}}")
    for source in spec.get("fallback", []):
        if source not in clones:
            raise CloneError(f"{source}: a fallback must be cloned")
    planned = set(clone_table(spec).values())
    for cid in spec.get("retired", []):
        if not ra.band_clone_parts(cid) or cid in planned:
            raise CloneError(f"{cid}: a retired entry is a clone id the plan no longer has")
    return spec


def clone_table(spec):
    """{(source, band): clone id} of a checked spec."""
    return {(src, band): ra.band_clone_id(src, band) for src, bands in spec["clones"].items() for band in bands}


def upgrade_table(spec):
    """{(troop id, source): replacement} of a checked spec."""
    return {(tid, src): row["to"] for src, row in spec.get("upgrades", {}).items() for tid in row["troops"]}


@functools.lru_cache(maxsize=1)
def load_spec():
    if not SPEC.exists():
        return {"clones": {}, "upgrades": {}}
    try:
        return check_spec(json.loads(SPEC.read_bytes().decode("utf-8")))
    except (UnicodeDecodeError, ValueError) as exc:
        raise CloneError(f"the spec {SPEC} cannot be read: {exc}") from exc


@functools.lru_cache(maxsize=1)
def spec_table():
    """{(source, band): clone id} of the shipped spec (cached with load_spec: never change the dict)."""
    return clone_table(load_spec())


@functools.lru_cache(maxsize=1)
def spec_upgrades():
    """{(troop id, source): replacement} of the shipped spec."""
    return upgrade_table(load_spec())


def spec_text(spec):
    """The spec as written: one source per line, so a roster change shows as a line in the diff."""
    clones = [f"    {json.dumps(s)}: {json.dumps(b)}" for s, b in spec["clones"].items()]
    upgrades = [f"    {json.dumps(s)}: {json.dumps(r)}" for s, r in spec["upgrades"].items()]
    return ("{\n  \"description\": [\n" + ",\n".join(f"    {json.dumps(d)}" for d in spec["description"])
            + "\n  ],\n  \"clones\": {\n" + ",\n".join(clones) + "\n  },\n  \"upgrades\": {\n" + ",\n".join(upgrades)
            + "\n  },\n  \"fallback\": " + json.dumps(spec["fallback"])
            + ",\n  \"retired\": " + json.dumps(spec.get("retired", [])) + "\n}\n")


# =============================================================================
# The swap rule
# =============================================================================

@functools.lru_cache(maxsize=1)
def _rule_sets():
    """(ladder-exempt troops, noble-line troops, exempt (troop, item) pairs): the validator's lists, which
    derive_armor_tiers.parse_rosters reads too."""
    import taom_schema as ts
    return (frozenset(ts.Validator._ARMOUR_LADDER_EXEMPT), frozenset(ts.Validator._NOBLE_LINE_TROOPS),
            frozenset(ts.Validator._ARMOUR_LADDER_EXEMPT_ITEMS))


def rule_id(troop_id):
    """The campaign troop whose rule a troop follows: a Fords of War copy (fow_<id>) follows its campaign troop."""
    return troop_id[len(FORDS_PREFIX):] if troop_id.startswith(FORDS_PREFIX) else troop_id


def troop_band(source, troop_id, level):
    """The clone band a troop of this level wears `source` at (its anchor: a noble line a band up), or None for a
    ladder-exempt troop or (troop, item) pair, which keeps the source."""
    rid = rule_id(troop_id)
    exempt, noble, pairs = _rule_sets()
    if rid in exempt or (rid, source) in pairs:
        return None
    return ra.wearer_band(int(level), source, rid in noble)


def armour_for_band(item_id, band, table=None):
    """The armour a wearer at `band` takes for `item_id`: the clone of its source at that band, else the source
    (an elite band, a lord, or a band the plan has no clone for). A clone is read as its source first."""
    source = ra.band_clone_source(item_id)
    if band not in ra.BAND_CLONE_CODES:
        return source
    return (spec_table() if table is None else table).get((source, band), source)


def armour_for_troop(item_id, troop_id, level, table=None, upgrades=None, civilian=False):
    """The armour a troop of this level wears for `item_id`: in a battle set the upgrade the spec names for this
    troop, else the clone of its band (troop_band), else the source (a clone the troop wears is read as its
    source first). A civilian set takes the clone and never the upgrade. The one swap rule for a troop: this
    tool and every generator of a whole troop file call it."""
    source = ra.band_clone_source(item_id)
    band = troop_band(source, troop_id, level)
    if band is None:
        return source   # a ladder-exempt troop or pair keeps the source
    if not civilian:
        swap = (spec_upgrades() if upgrades is None else upgrades).get((rule_id(troop_id), source))
        if swap:
            return swap
    return armour_for_band(source, band, table)


def _read_back(troop_id, old, upgrades, applied):
    """The source a roster item stands for: an applied upgrade's replacement is read as its source (the shipped
    spec, `applied`). Two sources on one replacement that the new plan parts cannot be told apart in the roster:
    a CloneError (restore the rosters from git and plan again)."""
    rid = rule_id(troop_id)
    srcs = sorted(src for (t, src), to in applied.items() if t == rid and to == old)
    if len(srcs) > 1 and len({upgrades.get((rid, s), s) for s in srcs}) > 1:
        raise CloneError(f"{troop_id} wears {old} for {', '.join(srcs)}, which the new plan parts; "
                         f"restore its roster (git checkout) and plan again")
    return srcs[0] if srcs else old


def plan_swaps(troops, table=None, upgrades=None, applied=None):
    """[{file, troop, slot, old, new}] for each battle slot whose item differs from armour_for_troop, once per
    (troop, slot, old item), in troop order. An item an applied upgrade put there (`applied`, the shipped spec's
    upgrade table) is judged as its source, so a changed or dropped pick reaches the roster."""
    upgrades = spec_upgrades() if upgrades is None else upgrades
    applied = applied or {}
    out, seen = [], set()
    for tid in sorted(troops):
        rec = troops[tid]
        if rec.get("level") is None:
            continue
        for st in rec.get("sets") or ():
            for slot in ra.MESH_LADDER_SLOTS:
                old = st.get(slot)
                if not old or (tid, slot, old) in seen:
                    continue
                seen.add((tid, slot, old))
                new = armour_for_troop(_read_back(tid, old, upgrades, applied), tid, rec["level"], table, upgrades)
                if new != old:
                    out.append({"file": rec["file"], "troop": tid, "slot": slot, "old": old, "new": new})
    return out


# =============================================================================
# Every wearer outside the troop battle sets (review 2026-11-17)
# =============================================================================

ENLIST_FILE = "taom_enlistment_equipment.xml"
_ENLIST_RE = re.compile(r"^enlist_.+_(recruit|soldier|veteran|sergeant)$")
# Lord kit: fix_lord_kits.py owns these templates, and a lord wears his piece at the lord band.
LORD_SET_FILE_RE = re.compile(r"^(?:taom_equipment_sets_.+|taom_lord_template_equipment)\.xml$")
# Folders that define items, not sets, and the language files.
SCAN_SKIP_DIRS = frozenset({"armour_band_clones", "lord_armour", "placeholder_weapons", "Languages"})
_TAG_RE = re.compile(r"<!--.*?-->|<(/?)(NPCCharacter|EquipmentRoster|EquipmentSet|equipment|Equipment)\b([^>]*?)(/?)>",
                     re.S)


@dataclasses.dataclass(frozen=True)
class Worn:
    """One armour slot of one set: `file` relative to ModuleData ('/'), `container` the NPCCharacter or the
    outermost EquipmentRoster id, `troop` for troops/troops_*.xml and the Fords of War copies (armour_for_troop's
    rule), `band` the clone band the wearer takes its piece at (a troop by troop_band, any other wearer by
    _wearer_band; None: it keeps the source, as a lord, an unlevelled troop or a ladder-exempt troop does)."""
    file: str
    container: str
    civilian: bool
    slot: str
    item: str
    troop: bool
    level: int | None
    band: str | None
    line: int
    start: int
    end: int


@functools.lru_cache(maxsize=None)
def _attr_re(name):
    return re.compile(r'\b%s="([^"]*)"' % re.escape(name))


def _attr(attrs, name):
    m = _attr_re(name).search(attrs or "")
    return m.group(1) if m else None


def _is_civilian(attrs):
    return _attr(attrs, "civilian") == "true" or _attr(attrs, "equipmentType") == "Civilian"


def is_troop_file(rel):
    return (rel.startswith("troops/troops_") and rel.endswith(".xml")) or rel == "fords_of_war/fords_troops.xml"


@functools.lru_cache(maxsize=None)
def enlist_rank_band(rank):
    """The band of an enlistment rank: the band of the lowest level generate_enlistment_rosters.RANK_BANDS gives it."""
    import generate_enlistment_rosters as er
    return ra.level_to_band(er.RANK_BANDS[rank][0])


def _wearer_band(rel, npc, roster):
    """The clone band of a set outside the troop files, or None when its wearer keeps the source (a lord)."""
    if npc is not None:
        if _attr(npc, "occupation") == "Lord":
            return None
        level = _attr(npc, "level")
        return ra.level_to_band(int(level)) if level and level.isdigit() else "light"
    name = rel.rsplit("/", 1)[-1]
    if rel.startswith("equipmentsets/") and LORD_SET_FILE_RE.match(name):
        return None
    m = _ENLIST_RE.match(_attr(roster, "id") or "")
    if name == ENLIST_FILE and m:
        return enlist_rank_band(m.group(1))
    return "light"


def scan_text(text, rel):
    """[Worn] for every armour <equipment> (either case) of a file's text, comments skipped. `rel` is the path
    under ModuleData, which decides the rule: a troop file's sets follow their troop, any other set the band of
    its wearer (_wearer_band)."""
    out, stack = [], []
    troop = is_troop_file(rel)
    pos, line = 0, 1   # a running line count: one pass over the text
    for m in _TAG_RE.finditer(text):
        if m.group(0).startswith("<!--"):
            continue
        close, name, attrs, selfclose = m.group(1), m.group(2), m.group(3), m.group(4)
        if close:
            while stack and stack.pop()[0] != name:
                pass
            continue
        if name not in ("equipment", "Equipment"):
            if not selfclose:
                stack.append((name, attrs))
            continue
        slot, item = _attr(attrs, "slot"), (_attr(attrs, "id") or "").replace("Item.", "", 1)
        npc = next((a for n, a in stack if n == "NPCCharacter"), None)
        roster = next((a for n, a in stack if n == "EquipmentRoster"), None)
        container = _attr(npc if npc is not None else roster, "id")
        if slot not in ra.MESH_LADDER_SLOTS or not item or not container:
            continue
        level = _attr(npc, "level") if npc is not None else None
        level = int(level) if level and level.isdigit() else None
        if troop:
            band = None if level is None else troop_band(ra.band_clone_source(item), container, level)
        else:
            band = _wearer_band(rel, npc, roster)
        line += text.count("\n", pos, m.start())
        pos = m.start()
        out.append(Worn(file=rel, container=container,
                        civilian=any(n != "NPCCharacter" and _is_civilian(a) for n, a in stack),
                        slot=slot, item=item, troop=troop, level=level, band=band,
                        line=line, start=m.start(), end=m.end()))
    return out


def planned_for(w, table=None, upgrades=None):
    """The item the plan puts in this slot: a troop by armour_for_troop, any other wearer by its band."""
    if w.troop:
        if w.level is None:
            return w.item
        return armour_for_troop(w.item, w.container, w.level, table, upgrades, civilian=w.civilian)
    return armour_for_band(w.item, w.band, table)


def _read_text(path):
    raw = Path(path).read_bytes()
    try:
        return raw.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise CloneError(f"{path} is not UTF-8 ({exc})") from exc


def scan_wearers(ids, moduledata=MODULEDATA):
    """[Worn] of every ModuleData file that names one of `ids` or a band clone (item folders and language files
    skipped)."""
    pat = re.compile(r'id="Item\.(?:%s|%s)' % ("|".join(re.escape(i) + '"' for i in sorted(ids)) or "(?!)",
                                               re.escape(ra.BAND_CLONE_PREFIX)))
    out = []
    root = Path(moduledata)
    for path in sorted(root.rglob("*.xml")):
        rel = path.relative_to(root).as_posix()
        if rel.split("/", 1)[0] in SCAN_SKIP_DIRS:
            continue
        text = _read_text(path)
        if pat.search(text):
            out += scan_text(text, rel)
    return out


def wearer_bands(worn):
    """{source: {band}}: the clone bands the wearers below the elite band need, a clone read as its source."""
    out = collections.defaultdict(set)
    for w in worn:
        if w.band in ra.BAND_CLONE_CODES:
            out[ra.band_clone_source(w.item)].add(w.band)
    return dict(out)


def worn_swaps(worn, table, upgrades):
    """[{file, container, civilian, slot, old, new}] for each set outside a troop's battle sets whose item differs
    from planned_for, once per (file, container, civilian, slot, old)."""
    out, seen = [], set()
    for w in worn:
        if w.troop and not w.civilian:
            continue   # plan_swaps
        key = (w.file, w.container, w.civilian, w.slot, w.item)
        new = planned_for(w, table, upgrades)
        if new != w.item and key not in seen:
            seen.add(key)
            out.append({"file": w.file, "container": w.container, "civilian": w.civilian, "slot": w.slot,
                        "old": w.item, "new": new})
    return out


def rewrite_text(text, rel, edits):
    """The text with each slot of `edits` {(container, civilian, slot, old): new} rewritten in place: only the id
    of the <equipment> element changes, so newlines, indentation and every other byte stay."""
    parts, pos = [], 0
    for w in scan_text(text, rel):
        new = edits.get((w.container, w.civilian, w.slot, w.item))
        if new is None:
            continue
        elem = text[w.start:w.end]
        parts += [text[pos:w.start], elem.replace(f'id="Item.{w.item}"', f'id="Item.{new}"', 1)]
        pos = w.end
    return "".join(parts) + text[pos:]


def band_text(text, rel):
    """The text with every set outside a troop's battle sets at the piece the shipped spec plans for it
    (planned_for). A tool that writes such sets whole (promote_borrowed_cultures.py) runs it on its output, or
    its next --apply hands a low wearer the elite-priced source again."""
    edits = {}
    for w in scan_text(text, rel):
        if w.troop and not w.civilian:
            continue
        new = planned_for(w)
        if new != w.item:
            edits[(w.container, w.civilian, w.slot, w.item)] = new
    return rewrite_text(text, rel, edits) if edits else text


def write_worn_swaps(swaps, moduledata=MODULEDATA):
    """Write worn_swaps into their files (BOM and newlines kept; every file parsed before any is written). The
    files written, relative to ModuleData."""
    by_file = collections.defaultdict(dict)
    for s in swaps:
        by_file[s["file"]][(s["container"], s["civilian"], s["slot"], s["old"])] = s["new"]
    pending = []
    for rel, edits in sorted(by_file.items()):
        path = Path(moduledata) / rel
        raw = path.read_bytes()
        bom = raw.startswith(b"\xef\xbb\xbf")
        text = _read_text(path)
        new = rewrite_text(text, rel, edits)
        if new == text:
            continue
        try:
            ET.fromstring(new.encode("utf-8"))
        except ET.ParseError as exc:
            raise CloneError(f"{rel} would not parse after the swap ({exc}); nothing written") from exc
        pending.append((rel, path, (b"\xef\xbb\xbf" if bom else b"") + new.encode("utf-8")))
    for _rel, path, data in pending:
        path.write_bytes(data)
    return [rel for rel, _p, _d in pending]


def sources_worn_low(moduledata=MODULEDATA, sources=None):
    """[Worn] for each set below the elite band that names a clone's source itself (`sources`, the shipped spec's
    by default): the source is priced at the elite band, so that wearer would wear elite armour. The validator's
    BAND_CLONE_SOURCE_WORN_LOW."""
    sources = set(load_spec()["clones"]) if sources is None else set(sources)
    if not sources:
        return []
    return [w for w in scan_wearers(sources, moduledata)
            if w.item in sources and w.band in ra.BAND_CLONE_CODES]


# =============================================================================
# Values
# =============================================================================

def band_values(item_id, slot, culture, band, declared, shares=None):
    """({stat: value}, modifier_group) the restat writes for an item at `band`: rebalance_armor.restat_armour
    (`shares`: its line_shares, the live Armory's by default), or for a generator-owned item the calculate_stats
    row its generator writes (a Mallorn piece a band up) over the stats the item declares."""
    if ra.is_generator_owned(item_id):
        if item_id.startswith(MALLORN_PREFIX):
            import generate_lothlorien_armor as gla
            band = gla.ML_BAND_UP[band]
        row = ra.calculate_stats(band, slot, culture, item_id=item_id)
        return {s: row[s] for s in declared if s in row}, row["modifier_group"]
    return ra.restat_armour(slot, band, culture, item_id, declared, line_shares=shares)


def clone_values(source_id, slot, culture, band, declared, shares=None):
    """band_values at `band` of the source as the restat leaves it (its values at the elite band, where it prices
    once its low troops wear clones), no stat above those. Priced from that and not from the live values, a clone
    is the same before and after the restat apply, so --check stays clean across it; a stat the restat adds to the
    source (rebalance_armor.UNDECLARED_RAISED_SECONDARIES) is in it both times."""
    top, top_group = band_values(source_id, slot, culture, "elite", declared, shares)
    if band == "elite":
        return top, top_group
    after = dict(declared, **top)
    values, group = band_values(source_id, slot, culture, band, after, shares)
    return {s: min(v, top.get(s, v)) for s, v in values.items()}, group


# =============================================================================
# The Armory sources and the troops
# =============================================================================

_ITEM_RE = re.compile(r'<Item\b[^>]*?\bid="([^"]+)"[\s\S]*?</Item>')


def read_sources(armory_items, wanted=None):
    """{item id: {folder, slot, culture, attr_culture, type, name, xml, declared}} for the live character armour
    items (commented copies skipped) of the five slot files of every Armory folder; only `wanted` when given.
    `culture` is the line folder the restat reads, `attr_culture` the item's own culture attribute. NoArmory when
    no slot file holds an item."""
    out, files = {}, 0
    for folder in sorted(os.listdir(armory_items)):
        for fn, slot in ra.SLOT_TYPES.items():
            path = Path(armory_items) / folder / fn
            if not path.is_file():
                continue
            try:
                text = path.read_bytes().decode("utf-8-sig")
            except UnicodeDecodeError as exc:
                raise CloneError(f"{path} is not UTF-8 ({exc}); nothing read from it") from exc
            files += 1 if "<Item" in text else 0
            for m in _ITEM_RE.finditer(text):
                iid = m.group(1)
                if (wanted is not None and iid not in wanted) or iid in out or ra._inside_comment(text, m.start()):
                    continue
                xml = m.group(0).replace("\r\n", "\n")
                try:
                    elem = ET.fromstring(xml)
                except ET.ParseError:
                    continue
                armor = elem.find(".//Armor")
                if armor is None or elem.get("Type") not in ra.CHARACTER_ARMOUR_TYPES:
                    continue
                name = ra.get_display_name(elem.get("name", ""))
                out[iid] = {"folder": folder, "slot": slot, "culture": ra.detect_culture(str(path), name),
                            "attr_culture": elem.get("culture"), "type": elem.get("Type"), "name": name, "xml": xml,
                            "declared": ra.parse_current_values(armor, slot)}
    if not files:
        raise NoArmory(f"no Armory slot file with an item under {armory_items}; nothing was checked")
    missing = sorted(set(wanted or ()) - set(out))
    if missing:
        raise CloneError(f"{len(missing)} source(s) are not live armour items of the Armory: {', '.join(missing[:10])}")
    return out


def check_shipped_pieces(spec, sources):
    """Raise CloneError when a piece the shipped plan names (a clone's source, an upgrade's source or replacement,
    a retired clone's source) is not live armour of the Armory read: a stale or partial Armory would otherwise
    drop its clones and upgrades from the plan (review 2026-11-17, tooling F3)."""
    pieces = set(spec.get("clones", {})) | {ra.band_clone_source(c) for c in spec.get("retired", [])}
    for src, row in spec.get("upgrades", {}).items():
        pieces |= {src, row["to"]}
    missing = sorted(pieces - set(sources))
    if missing:
        raise CloneError(f"{len(missing)} piece(s) of the shipped plan are not live armour of the Armory at "
                         f"{ra.ARMORY_DIR}: {', '.join(missing[:10])}. Point BANNERLORD_GAME_DIR at the live install; "
                         f"when the Armory dropped a piece, take it out of the spec and its rosters by hand. "
                         f"Nothing was written")


def _battle_sets(npc):
    sets = []
    for es in list(npc.iter("EquipmentRoster")) + list(npc.iter("EquipmentSet")):
        if es.get("civilian") == "true" or es.get("equipmentType") == "Civilian":
            continue
        slots = {}
        for eq in es.findall("equipment"):
            if eq.get("slot") in ra.MESH_LADDER_SLOTS:
                slots[eq.get("slot")] = (eq.get("id") or "").replace("Item.", "", 1)
        if slots:
            sets.append(slots)
    return sets


def load_troops():
    """(campaign troops, Fords of War copies), each {id: {level, file, sets, upgrades}}: the battle sets of
    troops/troops_*.xml (the troops the restat anchors on) and of fords_of_war/fords_troops.xml."""
    campaign = {}
    for path in sorted((MODULEDATA / "troops").glob("troops_*.xml")):
        for npc in ET.parse(path).getroot().iter("NPCCharacter"):
            if npc.get("id"):
                campaign[npc.get("id")] = {"level": int(npc.get("level", "0") or 0), "file": str(path),
                                           "sets": _battle_sets(npc), "upgrades": [
                                               (u.get("id") or "").split(".")[-1] for u in npc.iter("upgrade_target")]}
    fords = {}
    if FORDS_TROOPS.exists():
        for npc in ET.parse(FORDS_TROOPS).getroot().iter("NPCCharacter"):
            if npc.get("id") and npc.get("level"):
                fords[npc.get("id")] = {"level": int(npc.get("level")), "file": str(FORDS_TROOPS),
                                        "sets": _battle_sets(npc)}
    return campaign, fords


def as_planned_from(troops, upgrades):
    """The troops with each upgrade the spec names read back as its source, so a plan made after an --apply
    is the plan made before it. When two sources of one troop went to the same piece, the first takes its
    place and each other source is read in an extra set of that slot alone, kept apart in `extra_sets`: the
    scope and the worn ceiling read it (all_sets), the slot means of an upgrade edge do not."""
    out = {}
    for tid, rec in troops.items():
        back = collections.defaultdict(list)
        for (t, src), to in sorted(upgrades.items()):
            if t == tid:
                back[to].append(src)
        if not back:
            out[tid] = rec
            continue
        sets, seen = [], set()
        for st in rec["sets"]:
            sets.append({k: back[v][0] if v in back else v for k, v in st.items()})
            seen.update((k, v) for k, v in st.items() if v in back)
        extra = [{slot: src} for slot, to in sorted(seen) for src in back[to][1:]]
        out[tid] = dict(rec, sets=sets, extra_sets=extra)
    return out


def all_sets(rec):
    """A troop's battle sets with the read-back extra sets (as_planned_from)."""
    return list(rec.get("sets") or ()) + list(rec.get("extra_sets") or ())


def eligible_piece(iid, index):
    """True for armour the restat prices on the troop curve: an Armory item of the derive_armor_tiers index,
    no civilian-token or troll kit, no clone or lord variant."""
    import derive_armor_tiers as dat
    return (iid in index and not ra.band_clone_parts(iid) and not dat.lord_variant_source(iid)
            and ra.mesh_tier_of(iid) != "civilian" and not ra.is_monster_kit(iid))


def plan_scope(campaign, index):
    """rebalance_armor.shared_down_pieces over the campaign troops, read as the restat reads them, a clone
    counted as its source."""
    exempt, noble, pairs = _rule_sets()
    troops = {tid: dict(rec, sets=all_sets(rec)) for tid, rec in campaign.items()}
    return ra.shared_down_pieces(troops, exempt=exempt, noble=noble, exempt_items=pairs,
                                 eligible=lambda i: eligible_piece(i, index), source_of=ra.band_clone_source)


def lowest_anchor(campaign):
    """{item id: the lowest anchor level of a non-exempt battle wearer}, a clone read as its source."""
    exempt, noble, pairs = _rule_sets()
    low = {}
    for tid, rec in campaign.items():
        if tid in exempt or rec.get("level") is None:
            continue
        for st in all_sets(rec):
            for iid in st.values():
                iid = ra.band_clone_source(iid)
                if (tid, iid) in pairs:
                    continue
                a = ra.noble_anchor_level(rec["level"], iid) if tid in noble else rec["level"]
                low[iid] = min(low.get(iid, a), a)
    return low


# =============================================================================
# The upgrade pick
# =============================================================================

def _prefix(a, b):
    n = 0
    for x, y in zip(a, b):
        if x != y:
            break
        n += 1
    return n


def _stats(values):
    return {s: values.get(s, 0) for s in ARMOUR_STATS}


def candidates(src, sources, low_anchor, wearer_levels):
    """The pieces `src` may be upgraded to, before the value checks (see the module docstring), best first.
    `wearer_levels` is [(level, noble)] of the level 36+ wearers."""
    me = sources[src]
    line = ra.kingdom_key(src, me["culture"])
    own_tier = ra.mesh_tier_of(src)
    allowed = [ra.allowed_mesh_tiers(lv, nb) for lv, nb in wearer_levels]
    top = min(max(ra.MESH_TIER_ORDER.index(t) for t in a) for a in allowed)
    out = []
    for iid, rec in sources.items():
        if iid == src or rec["slot"] != me["slot"] or rec["type"] != me["type"]:
            continue
        if rec.get("attr_culture") != me.get("attr_culture"):
            continue   # the same culture attribute: two lines can share a prefix (sk_dg_ is Rhun and Dol Guldur)
        if line is None or ra.kingdom_key(iid, rec["culture"]) != line:
            continue
        tier = ra.mesh_tier_of(iid)
        lord_armour = ra.is_lord_kit(iid) and tier in (None, "lord") and not ra.is_lord_kit(src)
        if (tier == "civilian" or ra.is_monster_kit(iid) or ra.is_excluded(iid, rec["name"]) or lord_armour
                or iid.startswith(MALLORN_PREFIX) or ra.band_clone_parts(iid)):
            continue
        if low_anchor.get(iid, 99) <= 30 or bare_name(rec["name"]) == bare_name(me["name"]):
            continue   # worn low (in scope itself), or the same piece in another colour
        if tier is not None and ra.MESH_TIER_ORDER.index(tier) > top:
            continue
        if tier is not None and own_tier is not None and ra.MESH_TIER_ORDER.index(tier) <= ra.MESH_TIER_ORDER.index(own_tier):
            continue
        inside = tier is None or all(tier in a for a in allowed)
        rank = ra.MESH_TIER_ORDER.index(tier) if tier else -1
        out.append(((-_prefix(src, iid), not inside, rank if inside else -rank, iid), iid))
    return [iid for _, iid in sorted(out)]


def planned_item_values(iid, sources, live):
    """What a level 36+ troop's piece carries after the plan: an Armory piece on the troop curve at the elite
    band (every piece such a troop keeps is priced there once the plan is applied), anything else at its live
    values."""
    if iid in sources and ra.mesh_tier_of(iid) != "civilian" and not ra.is_monster_kit(iid):
        rec = sources[iid]
        return _stats(band_values(iid, rec["slot"], rec["culture"], "elite", rec["declared"])[0])
    return _stats(live.get(iid, {}).get("stats", {}))


def _planned_total(troop_id, sets, children_sets, sources, live):
    """{child id: (the troop's total, the child's total)} over the slots the upgrade gate compares
    (fix_upgrade_armour_regressions.compared_slots): per slot the mean over battle sets of the item's summed
    planned armour, added over the slots, as the validator's UPGRADE_ARMOUR_REGRESSION adds it."""
    import fix_upgrade_armour_regressions as fu

    def total(sets_, slots):
        per = []
        for s in slots:
            vals = [sum(planned_item_values(st[s], sources, live).values()) if st.get(s) else 0 for st in sets_]
            per.append(sum(vals) / len(vals) if vals else 0.0)
        return sum(per)

    out = {}
    for cid, csets in children_sets.items():
        slots = fu.compared_slots({"id": troop_id}, {"id": cid})
        out[cid] = (total(sets, slots), total(csets, slots))
    return out


def pick_upgrade(src, scope_rec, sources, low_anchor, campaign, live, swapped, noble, militia=frozenset()):
    """(replacement, its elite values, the source's planned values) for the level 36+ wearers of a B piece,
    or (None, reason, source values). `swapped` {troop: {source: replacement}} holds the upgrades already picked, so
    the ceiling and the upgrade edges are judged on the whole plan. A pick may not leave a wearer above a troop
    it upgrades into (its `upgrades`, an edge between two `militia` troops aside) by more than the fallback, the
    source at the elite band, would: B is never worse than A on the upgrade gate."""
    me = sources[src]
    low_band = next(iter(scope_rec["low"]))
    mine = _stats(band_values(src, me["slot"], me["culture"], low_band, me["declared"])[0])
    tops = scope_rec["top"]
    cands = candidates(src, sources, low_anchor, [(campaign[t]["level"], t in noble) for t in tops])
    if not cands:
        return None, "no piece of its line and slot qualifies", mine
    primary = ra.GOVERNED_STATS[me["slot"]][0]
    reasons = []
    for r in cands:
        theirs = planned_item_values(r, sources, live)
        if any(theirs[s] < mine[s] for s in ARMOUR_STATS) or theirs[primary] <= mine[primary]:
            reasons.append(f"{r} lower on a region")
            continue
        over = []
        for t in tops:
            done = dict(swapped.get(t, {}), **{src: r})
            sets = [{k: done.get(v, v) for k, v in st.items()} for st in all_sets(campaign[t])]
            items = {i: {"stats": planned_item_values(i, sources, live)} for st in sets for i in st.values()}
            _worst, best = ra.worn_armour_extremes_per_slot({"sets": sets}, items)
            over += [f"{t} {reg} {best[reg]}" for reg, lim in ra.WORN_ARMOUR_CEILING.items() if best[reg] > lim]
        if over:
            reasons.append(f"{r} passes the ceiling ({', '.join(over[:2])})")
            continue
        above = []
        for t in tops:
            # a child that is a top wearer of the source takes the same pick
            kids = {c: [{k: (r if v == src and c in tops else swapped.get(c, {}).get(v, v)) for k, v in st.items()}
                        for st in campaign[c]["sets"]]
                    for c in campaign[t].get("upgrades", ()) if c in campaign and campaign[c]["sets"]
                    and not (t in militia and c in militia)}
            if not kids:
                continue
            mine_sets = [{k: swapped.get(t, {}).get(v, v) for k, v in st.items()} for st in campaign[t]["sets"]]
            with_r = [{k: (r if v == src else v) for k, v in st.items()} for st in mine_sets]
            after = _planned_total(t, with_r, kids, sources, live)
            fallback = _planned_total(t, mine_sets, kids, sources, live)
            above += [f"{t} {a:.0f} over its upgrade {c} {b:.0f}" for c, (a, b) in after.items()
                      if a > max(b, fallback[c][0]) + 1e-9]
        if above:
            reasons.append(f"{r} puts {above[0]}")
            continue
        return r, theirs, mine
    return None, "; ".join(reasons[:3]), mine


# =============================================================================
# Rendering
# =============================================================================

def _set_attr(xml, name, value):
    """Set attribute `name` on the first match; add it after the last armour stat when absent."""
    pat = re.compile(r'(\b%s=")[^"]*(")' % re.escape(name))
    if pat.search(xml):
        return pat.sub(lambda m: m.group(1) + str(value) + m.group(2), xml, count=1)
    anchors = list(re.finditer(r'(\s+)(?:%s)="[^"]*"' % "|".join(ARMOUR_STATS), xml))
    if not anchors:
        raise CloneError(f"no place to write {name}")
    last = anchors[-1]
    return xml[:last.end()] + f'{last.group(1)}{name}="{value}"' + xml[last.end():]


def render_item(source_xml, source_id, band, values, modifier_group):
    """The clone's <Item>: the source's text with the clone id, is_merchandise="false", and the band's armour
    and modifier_group on its Armor element. The name, key and every other attribute stay the source's."""
    cid = ra.band_clone_id(source_id, band)
    xml = source_xml.replace(f'id="{source_id}"', f'id="{cid}"', 1)
    if re.search(r'\bis_merchandise="', xml):
        xml = re.sub(r'(\bis_merchandise=")[^"]*(")', r'\g<1>false\g<2>', xml, count=1)
    else:
        xml = xml.replace(f'id="{cid}"', f'id="{cid}" is_merchandise="false"', 1)
    head, sep, tail = xml.partition("<Armor")
    if not sep:
        raise CloneError(f"{source_id} has no Armor element")
    for stat in ARMOUR_STATS:
        if stat in values:
            tail = _set_attr(tail, stat, values[stat])
    tail = _set_attr(tail, "modifier_group", modifier_group)
    return head + sep + tail


def render_file(blocks, newline="\r\n"):
    """blocks: [(clone id, comment, item xml)] in order."""
    body = [f"    <!-- {cid}: {comment} -->\n    {xml.lstrip()}" for cid, comment, xml in blocks]
    text = '<?xml version="1.0" encoding="utf-8"?>\n' + HEADER + "\n<Items>\n" + "\n\n".join(body) + "\n</Items>\n"
    return text.replace("\r\n", "\n").replace("\n", newline)


def submodule_text(text):
    if f'path="{SUBMODULE_PATH}"' in text:
        return text
    if SUBMODULE_ANCHOR not in text:
        raise CloneError("SubModule.xml anchor (the lord armour variants' Items node) not found")
    types = "".join(f'        <GameType value="{g}"/>\n' for g in GAME_TYPES)
    block = ("    <!-- Armour band clones (tools/generate_armour_band_clones.py): ids start with taom_band_ -->\n"
             f'    <XmlNode>\n      <XmlName id="Items" path="{SUBMODULE_PATH}"/>\n'
             f"      <IncludedGameTypes>\n{types}      </IncludedGameTypes>\n    </XmlNode>\n")
    nl = "\r\n" if "\r\n" in text else "\n"
    i = text.index(SUBMODULE_ANCHOR)
    j = text.index("</XmlNode>", i) + len("</XmlNode>")
    return text[:j] + nl + block.replace("\n", nl).rstrip(nl) + text[j:]


def _fmt(values):
    return "/".join(str(values.get(k, "-")) for k in ARMOUR_STATS)


def item_blocks(text):
    """{item id: its <Item> text, newlines made LF} of a clone file, commented copies skipped."""
    return {m.group(1): m.group(0).replace("\r\n", "\n")
            for m in _ITEM_RE.finditer(text or "") if not ra._inside_comment(text, m.start())}


def shipped_clone_ids(spec, text):
    """Every clone id the repo has shipped: the spec's clones and retired ids, and the clone file's items."""
    return set(clone_table(spec).values()) | set(spec.get("retired", [])) | set(item_blocks(text))


def retired_blocks(old_text, shipped, planned):
    """[(clone id, comment, item xml)] for each shipped clone the plan no longer has, its block as the file holds
    it: a shipped id is never dropped (a save or a mod that names it would find the slot empty, review
    2026-11-17). A CloneError when the file has lost one (restore the file from git)."""
    blocks = item_blocks(old_text)
    gone = sorted(set(shipped) - set(planned))
    lost = [cid for cid in gone if cid not in blocks]
    if lost:
        raise CloneError(f"shipped clone(s) missing from {OUT_FILE.name}: {', '.join(lost[:10])}; restore the file "
                         f"from git, a shipped clone id is never dropped")
    return [(cid, "retired: no set wears it; kept so a save or a mod that names it still finds the item",
             blocks[cid]) for cid in gone]


def name_keys(text):
    """The distinct {=KEY} name keys of a rendered file, comments skipped (InlineDataKeyTests counts them)."""
    return len(set(re.findall(r"\{=([^}]+)\}", re.sub(r"<!--.*?-->", "", text, flags=re.S))))


def loc_backlog_keys():
    """The key count tools/loc_inline_backlog.json allows the clone file, or None when it lists none."""
    try:
        files = json.loads(LOC_BACKLOG.read_text(encoding="utf-8"))["files"]
    except (OSError, ValueError, KeyError):
        return None
    return (files.get(LOC_BACKLOG_KEY) or {}).get("keys")


def _on_disk(path):
    """A text file's content with its newlines made LF, or None when it is missing: core.autocrlf checks a tracked
    file out CRLF, and that is no drift (review 2026-11-17, tooling F1)."""
    path = Path(path)
    return path.read_bytes().decode("utf-8-sig").replace("\r\n", "\n") if path.exists() else None


def _stale(path, text):
    return _on_disk(path) != text.replace("\r\n", "\n")


def _rel(path):
    try:
        return Path(path).resolve().relative_to(REPO).as_posix()
    except ValueError:
        return str(path)


# =============================================================================
# Main
# =============================================================================

def build(armory_items, game_modules):
    """The whole plan from the rosters and the live Armory: {spec, pieces, text, swaps, worn_swaps, keys}. A piece
    row is {source, name, folder, slot, treatment, how, ...} for the report table; `swaps` are the troop battle
    sets (plan_swaps), `worn_swaps` every other set (worn_swaps), `keys` the clone file's name keys."""
    import derive_armor_tiers as dat
    import fix_upgrade_armour_regressions as fu
    shipped = load_spec()
    applied = upgrade_table(shipped)
    campaign_raw, fords = load_troops()
    campaign = as_planned_from(campaign_raw, applied)
    index, _ = dat.build_armory_index()
    scope = plan_scope(campaign, index)
    sources = read_sources(armory_items)
    check_shipped_pieces(shipped, sources)
    missing = sorted(set(scope) - set(sources))
    if missing:
        raise CloneError(f"in-scope pieces missing from the Armory slot files: {', '.join(missing[:10])}")
    live = fu.load_item_armour(str(game_modules))
    low_anchor = lowest_anchor(campaign)
    _exempt, noble, _pairs = _rule_sets()
    import rebalance_troops as rb
    militia = frozenset(rb.militia_troop_ids())
    # Every set that names a piece in scope or a clone, troop files included: a clone covers the band of each.
    worn = scan_wearers(set(scope) | set(shipped["clones"]))
    bands = wearer_bands(worn)
    clones, upgrades, fallback, pieces, swapped, picks = {}, {}, [], [], {}, {}
    # The B picks, in id order; then each piece that found none is judged again against the whole plan until a
    # pass adds no pick (a pick only raises its own wearers, whose edges and ceiling the retry checks).
    pending = [src for src in sorted(scope) if treatment(src, sources[src]["name"]) == "B"]
    while pending:
        for src in pending:
            picks[src] = pick_upgrade(src, scope[src], sources, low_anchor, campaign, live, swapped, noble, militia)
            for t in (scope[src]["top"] if picks[src][0] else ()):
                swapped.setdefault(t, {})[src] = picks[src][0]
        still = [src for src in pending if not picks[src][0]]
        pending = still if len(still) < len(pending) else []
    for src in sorted(scope):
        me, rec = sources[src], scope[src]
        kind = treatment(src, me["name"])
        row = {"source": src, "name": me["name"], "folder": me["folder"], "slot": me["slot"], "treatment": kind,
               "live": _stats(me["declared"]), "top": rec["top"], "low": rec["low"]}
        if kind == "B":
            to, theirs, mine = picks[src]
            row["planned"] = mine
            if to:
                upgrades[src] = {"to": to, "troops": rec["top"]}
                row.update(how="B", to=to, to_name=sources[to]["name"], to_values=theirs)
                pieces.append(row)
                continue
            row["why"] = theirs
            fallback.append(src)
        elite, _ = clone_values(src, me["slot"], me["culture"], "elite", me["declared"])
        row.update(how="fallback" if kind == "B" else "A", elite=_stats(elite), clones={})
        clones[src] = [b for b in ra.TIERS if b in rec["low"] or b in bands.get(src, ())]
        for band in clones[src]:
            row["clones"][band] = clone_values(src, me["slot"], me["culture"], band, me["declared"])
        pieces.append(row)
    spec = {"description": DESCRIPTION, "clones": clones, "upgrades": upgrades, "fallback": fallback}
    planned = set(clone_table(spec).values())
    old_text = OUT_FILE.read_bytes().decode("utf-8-sig") if OUT_FILE.exists() else ""
    retired = retired_blocks(old_text, shipped_clone_ids(shipped, old_text), planned)
    spec = check_spec(dict(spec, retired=[cid for cid, _c, _x in retired]))
    blocks = []
    for row in pieces:
        for band, (values, group) in row.get("clones", {}).items():
            src, me = row["source"], sources[row["source"]]
            comment = (f"{me['folder']} {me['slot']} {src} at the {band} band, {_fmt(values)} "
                       f"(head/body/arm/leg); the source at the elite band {_fmt(row['elite'])}")
            blocks.append((ra.band_clone_id(src, band), comment, render_item(me["xml"], src, band, values, group)))
    text = render_file(blocks + retired)
    ET.fromstring(text.encode("utf-8"))
    table, ups = clone_table(spec), upgrade_table(spec)
    return {"spec": spec, "pieces": pieces, "text": text, "keys": name_keys(text),
            "swaps": plan_swaps({**campaign_raw, **fords}, table, ups, applied),
            "worn_swaps": worn_swaps(worn, table, ups)}


def report(pieces):
    """The table of every in-scope piece (markdown), for the maintainer's check."""
    lines = ["| Piece | Name | Slot | Treatment | Live h/b/a/l | Result |", "|---|---|---|---|---|---|"]
    for p in pieces:
        if p["how"] == "B":
            result = (f"top-tier wearers ({len(p['top'])}) to `{p['to']}` ({p['to_name']}) at {_fmt(p['to_values'])}; "
                      f"the piece stays at {_fmt(p['planned'])} for its low wearers")
        else:
            clones = "; ".join(f"{b} {_fmt(v)}" for b, (v, _g) in p["clones"].items())
            result = (f"original at elite {_fmt(p['elite'])}; clones {clones}"
                      + (f" (no upgrade: {p['why']})" if p["how"] == "fallback" else ""))
        lines.append(f"| `{p['source']}` | {p['name']} | {p['slot']} | {p['how']} | {_fmt(p['live'])} | {result} |")
    return "\n".join(lines)


def main(argv=None):
    import fix_melee_ladder as fml
    import rebalance_troops as rb
    from fix_upgrade_armour_regressions import write_changes
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--apply", action="store_true", help="write the spec, the clone file, SubModule.xml and the rosters")
    ap.add_argument("--check", action="store_true", help="exit 1 when a file or a roster differs from the plan")
    ap.add_argument("--report", metavar="PATH", help="also write the table of in-scope pieces (markdown) to PATH")
    args = ap.parse_args(argv)
    armory_items = Path(ra.ARMORY_DIR)
    if not armory_items.is_dir():
        print(f"SKIPPED: no LOTRLOME_Armory items at {armory_items}; nothing was checked.")
        return 2
    plan = build(armory_items, rb.DEFAULT_GAME_MODULES)
    spec, pieces, text = plan["spec"], plan["pieces"], plan["text"]
    mine, owed = fml.split_generated(plan["swaps"])
    worn_mine, worn_owed = fml.split_generated(plan["worn_swaps"])

    hows = collections.Counter(p["how"] for p in pieces)
    n_clones = sum(len(b) for b in spec["clones"].values())
    print(f"Armour shared down (a level 36+ and a level 30- battle wearer): {len(pieces)} piece(s); "
          f"A {hows['A']}, B {hows['B']}, fallback {hows['fallback']}; {n_clones} clone(s), "
          f"{len(spec.get('retired', []))} retired; slot swaps {len(plan['swaps'])} in troop battle sets, "
          f"{len(plan['worn_swaps'])} in other sets")
    print(report(pieces))
    if args.report:
        Path(args.report).write_text(report(pieces) + "\n", encoding="utf-8")

    spec_new = spec_text(spec)
    sub_raw = SUBMODULE.read_bytes()
    sub_bom = sub_raw.startswith(b"\xef\xbb\xbf")
    sub_old = sub_raw.decode("utf-8-sig")
    sub_new = submodule_text(sub_old)
    ET.fromstring(sub_new.encode("utf-8"))
    files = [(path, data) for path, data, stale in (
        (SPEC, spec_new.encode("utf-8"), _stale(SPEC, spec_new)),
        (OUT_FILE, text.encode("utf-8"), _stale(OUT_FILE, text)),
        (SUBMODULE, (b"\xef\xbb\xbf" if sub_bom else b"") + sub_new.encode("utf-8"), sub_new != sub_old)) if stale]
    stale = [_rel(path) for path, _data in files]
    if mine:
        stale.append(f"{len(mine)} troop battle slot(s) in {len({s['file'] for s in mine})} file(s)")
    if worn_mine:
        stale.append(f"{len(worn_mine)} slot(s) of other sets in {len({s['file'] for s in worn_mine})} file(s)")
    nxt = []
    if owed or worn_owed:
        owners = collections.Counter(fml.GENERATED_TROOP_FILES[Path(s["file"]).name] for s in owed + worn_owed)
        nxt.append(f"{len(owed) + len(worn_owed)} troop slot(s) in generated files: run "
                   + ", ".join(f"python tools/{g} --apply ({n})" for g, n in sorted(owners.items())))
    backlog = loc_backlog_keys()
    if backlog != plan["keys"]:
        nxt.append(f"the clone file holds {plan['keys']} name key(s): set \"keys\" of {LOC_BACKLOG_KEY} in "
                   f"tools/loc_inline_backlog.json to {plan['keys']} (it says {backlog}; InlineDataKeyTests)")
    if args.check:
        print("check:", "OK" if not stale and not nxt else "DRIFT: " + "; ".join(stale + nxt))
        return 1 if stale or nxt else 0
    if not args.apply:
        print("(dry run; would write: " + ("; ".join(stale) or "nothing") + ")")
        for line in nxt:
            print("NEXT:", line)
        return 0
    written = []
    # The rosters first and the spec last: the spec records what the rosters hold (plan_swaps reads it back).
    if mine:
        write_changes(mine)
        written += sorted({_rel(s["file"]) for s in mine})
    if worn_mine:
        written += [_rel(MODULEDATA / rel) for rel in write_worn_swaps(worn_mine)]
    OUT_FILE.parent.mkdir(parents=True, exist_ok=True)
    for path, data in sorted(files, key=lambda f: f[0] == SPEC):
        path.write_bytes(data)
        written.append(_rel(path))
    print("written:", "; ".join(written) or "nothing")
    for line in nxt:
        print("NEXT:", line)
    return 0


def entry(argv=None):
    """main() with a crash kept apart from drift: a traceback exits 3, a spec or source error 1."""
    try:
        return main(argv)
    except NoArmory as exc:
        print(f"SKIPPED: {exc}", file=sys.stderr)
        return 2
    except CloneError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    except Exception:
        traceback.print_exc()
        return 3


if __name__ == "__main__":
    sys.exit(entry())
