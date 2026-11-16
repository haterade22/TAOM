#!/usr/bin/env python3
"""
Roster-derived armor tiering for TAOM (READ-ONLY) — Phase 2 of the armor rebalance.

The authoritative signal for an armor item's intended balance tier is NOT its name keyword
(brittle: a Dale "Archer Helmet A01" carries no tier word) but the LEVEL of the troop that
wears it. This tool joins every troop roster (Main/_Module/ModuleData/troops/troops_*.xml) to
the LIVE LOTRLOME_Armory item stats, keyed by item id, and for each item derives a tier from
its wearers.

Reuse-safe anchoring: armor items are deliberately reused across troops (not enough meshes per
soldier), so an item worn at several levels anchors its tier to its LOWEST wearer — it must never
over-arm the lower troops. Items worn across a wide level span are flagged as shared (the lower
troops get the right armor; the higher ones accept a compromise).

Tier signal precedence per item (anchor first since the kingdom-cap curve, #583, 2026-09-13; it
was keyword first before, which is how the Fountain Guard's `_heavy_` helmet, worn only at level
46, stayed at 33):
  1. roster anchor band (lowest BATTLE wearer level, ladder-exempt troops and exempt (troop, item)
     pairs never anchor; a noble-line wearer is recorded at rebalance_armor.noble_anchor_level,
     so `level` in the map can be above the troop's own)
  2. explicit tier keyword in the id (_light_/_med_/_heavy_/_elite_/_lord_, _civ_) for kit no troop wears
  3. unworn and keyword-less: roster cannot tier it (falls back to name/value detection)
The writer's --tier-source roster-first applies the same precedence, so the map's tier, target
and status columns describe what the restat does.

A hero (is_hero="true": the lords, the Harad lords, the named companions, the vanilla lords that
lords.xslt retags and hands battle templates, and the wanderer templates the game turns into heroes) never anchors a tier, but the map records `heroWorn: true`
on every item a hero's battle sets wear, so the restat can tell hero-only kit from merchandise
(rebalance_armor._roster_first_plan), and `lordWorn: true` when a lord (not only a named companion or
a wanderer) is among them, so a companion's piece is never priced at the lord band. Lords hold their kit as <EquipmentSet id=...> references into
equipmentsets/*.xml, resolved here, and a direct <equipment> child of <Equipments> replaces its slot in
every set of that hero, as BasicCharacterObject.Deserialize applies it.

This script NEVER writes armor XML. It writes the derived map (tools/data/armor_roster_tiers.json)
and a human report (tools/reports/armor-balance/ROSTER-TIERS.md). It computes the level-band
target as a REFERENCE — it does not decide whether an under-progressed line (e.g. Dale's flat
a-line) should be scaled up or accepted; that is a Phase-3 design call. See
docs/features/armor-balance.md.

Usage:
    python tools/derive_armor_tiers.py                 # write the map + report
    python tools/derive_armor_tiers.py --stdout        # also print the per-culture summary
    python tools/derive_armor_tiers.py --culture dale  # restrict the report to one armory folder
"""

import argparse
import datetime
import functools
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rebalance_armor as ra  # noqa: E402

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..'))
MODULEDATA_DIR = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData')
TROOPS_DIR = os.path.join(MODULEDATA_DIR, 'troops')
# Where the heroes' battle kit lives: lords and the Harad lords in characters/, the named companions beside
# them, the wanderer templates, and the EquipmentSet templates every lord points at by id. The vanilla lords
# are not in characters/: lords.xslt retags them and is the only stylesheet that assigns them equipment sets
# (lord_template_rosters.xslt only strips flags).
CHARACTERS_DIR = os.path.join(MODULEDATA_DIR, 'characters')
NAMED_COMPANIONS_DIR = os.path.join(MODULEDATA_DIR, 'named_companions')
WANDERERS_XML = os.path.join(MODULEDATA_DIR, 'taom_wanderers.xml')
EQUIPMENTSETS_DIR = os.path.join(MODULEDATA_DIR, 'equipmentsets')
LORDS_XSLT = os.path.join(MODULEDATA_DIR, 'lords.xslt')
XSL_NS = '{http://www.w3.org/1999/XSL/Transform}'
DATA_DIR = os.path.join(REPO_ROOT, 'tools', 'data')
REPORT_DIR = os.path.join(REPO_ROOT, 'tools', 'reports', 'armor-balance')
MAP_JSON = os.path.join(DATA_DIR, 'armor_roster_tiers.json')
REPORT_MD = os.path.join(REPORT_DIR, 'ROSTER-TIERS.md')
# Lord armour variants (#1333): repo items that copy an Armory piece at the lord band for the lord
# templates (tools/generate_lord_armour_variants.py). Read here beside the Armory, in the source's
# folder, so the class table, the restat map and fix_lord_kits see them.
LORD_VARIANT_PREFIX = 'taom_lord_'
LORD_VARIANTS_SPEC = os.path.join(REPO_ROOT, 'tools', 'lord_armour_variants.json')
LORD_VARIANTS_XML = os.path.join(MODULEDATA_DIR, 'lord_armour', 'lord_armour_variants.xml')


def lord_variant_source(item_id, spec=None):
    """The Armory id a lord armour variant copies, or None for any other id. A variant is an id the
    spec lists (lord_variant_cultures), not any id with the prefix: taom_lord_intro_* are dialogue keys."""
    if item_id in lord_variant_cultures(spec):
        return item_id[len(LORD_VARIANT_PREFIX):]
    return None


def lord_variant_cultures(spec=None):
    """{variant id: the template culture whose lords wear it} from the spec (the shipped one, read
    once, when not given)."""
    if spec is None:
        return _shipped_variant_cultures()
    return {LORD_VARIANT_PREFIX + row['source']: culture
            for culture, rows in (spec.get('cultures') or {}).items() for row in rows}


@functools.lru_cache(maxsize=None)
def _shipped_variant_cultures():
    if not os.path.exists(LORD_VARIANTS_SPEC):
        return {}
    with open(LORD_VARIANTS_SPEC, encoding='utf-8') as f:
        return lord_variant_cultures(json.load(f))

# Bannerlord equipment slot -> armor curve slot. Item0-3/Horse/HorseHarness are not armor.
EQUIP_SLOT_MAP = {'Head': 'head', 'Body': 'body', 'Leg': 'leg', 'Gloves': 'arm', 'Cape': 'shoulder'}

SLOT_ORDER = ['head', 'body', 'arm', 'leg', 'shoulder']
TIER_ORDER = ['light', 'medium', 'heavy', 'elite', 'lord']

# Level -> tier band. TAOM troop levels run {1,6,11,16,21,26,31,36,41,46,51}. Calibrated against
# Dale ground truth (L6/L11 wear the light a01 chest; L16 medium; L21/26 heavy) and the project
# owner's decision (2026-06-30): ELITE TROOPS ARE DEFINED BY LEVELS 31-51. The armor 'lord' tier is
# therefore hero-only (named lords/heroes, excluded from rosters) and is never assigned from a troop
# level here. Tunable reference, not a verdict — see the module docstring.
# One source since the kingdom-cap curve (#583): the writer's --tier-source roster-first bands a
# worn item by the same function, so the map and the restat cannot disagree about a level.
level_to_tier = ra.level_to_band


# The tier an item id explicitly encodes, or None. One function with the writer, the mesh-tier
# ladder (#609) and the validator, so a token read differently in one place cannot happen.
id_keyword_tier = ra.mesh_tier_of


def line_suffix(item_id):
    """Dale-style light/heavy line suffix: _aNN -> 'a' (light line), _bNN -> 'b' (heavy line)."""
    m = re.search(r'_([ab])\d*$', item_id.lower())
    return m.group(1) if m else None


# =============================================================================
# Parsing
# =============================================================================

# Troops whose kit is off the ladder by design (the light Ithilien ranger at level 51, the troll,
# the Harad mount riders): they wear their kit, but they do not ANCHOR it. One source, the
# validator's allowlist, so the map and the CROSS_CULTURE_ARMOUR_INVERSION gate agree. A noble-line
# troop anchors a band up (rebalance_armor.noble_anchor_level); an exempt (troop, item) pair does
# not anchor that one item. No fallback copy: taom_schema imports only the stdlib, and a silent
# fallback would let every exempt and noble troop anchor on the next restat.
import taom_schema as _ts  # noqa: E402
LADDER_EXEMPT_TROOPS = frozenset(_ts.Validator._ARMOUR_LADDER_EXEMPT)
NOBLE_TROOPS = frozenset(_ts.Validator._NOBLE_LINE_TROOPS)
LADDER_EXEMPT_ITEMS = frozenset(_ts.Validator._ARMOUR_LADDER_EXEMPT_ITEMS)


def _is_civilian_roster(elem):
    return elem.get('civilian') == 'true' or elem.get('equipmentType') == 'Civilian'


def parse_rosters():
    """Return wearers: item_id -> list of {troop, culture, level, slot}.

    BATTLE sets only: a dress in a level-46 dwarf's civilian set anchored 'Civilian Female Dress'
    at the elite band on 2026-09-13 and the dry run would have written 70 on it. Ladder-exempt
    troops are read but never anchor."""
    wearers = defaultdict(list)
    if not os.path.isdir(TROOPS_DIR):
        return wearers
    for fn in sorted(os.listdir(TROOPS_DIR)):
        if not (fn.startswith('troops_') and fn.endswith('.xml')):
            continue
        culture = fn[len('troops_'):-len('.xml')]
        try:
            root = ET.parse(os.path.join(TROOPS_DIR, fn)).getroot()
        except ET.ParseError:
            continue
        for npc in root.findall('.//NPCCharacter'):
            tid = npc.get('id', '')
            if tid in LADDER_EXEMPT_TROOPS:
                continue
            try:
                level = int(npc.get('level', '0'))
            except ValueError:
                level = 0
            seen = set()  # de-dupe (item, slot) across a troop's multiple rosters
            for roster in list(npc.iter('EquipmentRoster')) + list(npc.iter('EquipmentSet')):
                if _is_civilian_roster(roster):
                    continue
                for eq in roster.findall('equipment'):
                    slot = EQUIP_SLOT_MAP.get(eq.get('slot', ''))
                    if not slot:
                        continue
                    raw = eq.get('id', '')
                    item_id = raw.split('.', 1)[1] if raw.startswith('Item.') else raw
                    if not item_id or (item_id, slot) in seen or (tid, item_id) in LADDER_EXEMPT_ITEMS:
                        continue
                    seen.add((item_id, slot))
                    anchor = ra.noble_anchor_level(level, item_id) if tid in NOBLE_TROOPS else level
                    wearers[item_id].append({'troop': tid, 'culture': culture, 'level': anchor, 'slot': slot})
    return wearers


def _equipment_slots(elem, tag_lower=True):
    """{slot: item id} an equipment container lists, any slot. The tag is `equipment` or `Equipment`
    alike, or exactly `equipment` with tag_lower=False (what Deserialize reads under <Equipments>)."""
    out = {}
    for eq in elem:
        if (eq.tag.lower() if tag_lower else eq.tag) == 'equipment':
            raw = eq.get('id', '')
            item_id = raw.split('.', 1)[1] if raw.startswith('Item.') else raw
            if item_id:
                out[eq.get('slot', '')] = item_id
    return out


def load_battle_templates(directory=None):
    """{roster id: [{slot: item id}, ...]} over equipmentsets/*.xml, one dict per battle set: the
    <EquipmentSet id=...> reference a lord uses points at an <EquipmentRoster id=...> there, and the
    hero gets all of that roster's sets. Civilian rosters and sets are skipped. `directory` defaults
    to this checkout's equipmentsets/ (the validator passes the ModuleData it was given)."""
    directory = directory or EQUIPMENTSETS_DIR
    out = {}
    if not os.path.isdir(directory):
        return out
    for fn in sorted(os.listdir(directory)):
        if not fn.endswith('.xml'):
            continue
        try:
            root = ET.parse(os.path.join(directory, fn)).getroot()
        except ET.ParseError:
            continue
        for roster in root.iter('EquipmentRoster'):
            rid = roster.get('id')
            if not rid or _is_civilian_roster(roster):
                continue
            sets = out.setdefault(rid, [])
            for st in roster.findall('EquipmentSet'):
                slots = {} if _is_civilian_roster(st) else _equipment_slots(st)
                if slots:
                    sets.append(slots)
    return out


def _hero_elements(lords_only=False):
    """(hero id, element holding its <Equipments>) over every place a hero's kit is defined: an
    NPCCharacter with is_hero="true" in characters/ or named_companions/, each wanderer template of
    taom_wanderers.xml, and each lords.xslt template that writes an <Equipments> block (the retagged
    vanilla lords, id from its xsl:attribute name="id").

    A wanderer template says is_hero="false", but CompanionsCampaignBehavior turns every template with
    occupation Wanderer into a hero (HeroCreator.CreateSpecialHero), and Hero takes its battle equipment
    from the template's roster.

    With `lords_only`, only the lords: an occupation Lord hero of characters/ and the lords.xslt
    templates (none says another occupation). Named companions and wanderers are left out, because
    companions stay light (2026-11-15) and their kit must not be priced at the lord band."""
    for directory in ((CHARACTERS_DIR,) if lords_only else (CHARACTERS_DIR, NAMED_COMPANIONS_DIR)):
        if not os.path.isdir(directory):
            continue
        for fn in sorted(os.listdir(directory)):
            if not fn.endswith('.xml'):
                continue
            try:
                root = ET.parse(os.path.join(directory, fn)).getroot()
            except ET.ParseError:
                continue
            for npc in root.iter('NPCCharacter'):
                if npc.get('is_hero') == 'true' and (not lords_only or npc.get('occupation') == 'Lord'):
                    yield npc.get('id', ''), npc
    if os.path.isfile(WANDERERS_XML) and not lords_only:
        try:
            root = ET.parse(WANDERERS_XML).getroot()
        except ET.ParseError:
            root = None
        for npc in (root.iter('NPCCharacter') if root is not None else ()):
            if npc.get('is_template') == 'true' and npc.get('occupation') == 'Wanderer':
                yield npc.get('id', ''), npc
    if os.path.isfile(LORDS_XSLT):
        try:
            root = ET.parse(LORDS_XSLT).getroot()
        except ET.ParseError:
            return
        for template in root.iter(XSL_NS + 'template'):
            if template.find('.//Equipments') is None:
                continue
            ids = [a.text for a in template.iter(XSL_NS + 'attribute') if a.get('name') == 'id' and a.text]
            occupation = [a.text for a in template.iter(XSL_NS + 'attribute') if a.get('name') == 'occupation']
            if lords_only and occupation and (occupation[0] or '').strip() != 'Lord':
                continue
            yield (ids[0] if ids else template.get('match', '')), template


def _hero_battle_items(container, templates):
    """The item ids one hero wears in battle. Every battle set it holds (inline, or a template roster by
    id) is one set; the direct <equipment> children of <Equipments> then replace their slot in each of
    them (AddOverriddenEquipments), so a replaced template item is not worn by this hero."""
    sets = []
    for roster in list(container.iter('EquipmentRoster')) + list(container.iter('EquipmentSet')):
        if _is_civilian_roster(roster):
            continue
        own = _equipment_slots(roster)
        if own:
            sets.append(own)
        if roster.tag == 'EquipmentSet' and roster.get('id'):
            sets.extend(templates.get(roster.get('id'), ()))
    overrides = {}
    for equipments in container.iter('Equipments'):
        overrides.update(_equipment_slots(equipments, tag_lower=False))
    if not sets:
        return set(overrides.values())
    return {item for s in sets for item in {**s, **overrides}.values()}


def parse_hero_wearers():
    """Return {item_id: [hero id]} for the armour a hero's BATTLE sets wear (every slot is read; the
    caller only looks up armour items). A hero is an NPCCharacter with is_hero="true" in characters/
    or named_companions/, or a lord lords.xslt hands a template (_hero_elements). Its kit is an inline
    <EquipmentRoster>/<EquipmentSet> or an <EquipmentSet id=...> reference to a template in
    equipmentsets/, with direct <equipment> overrides applied; civilian sets are skipped. Ladder-
    exempt heroes are read like the rest: this map never anchors a tier."""
    return _wearers(_hero_elements())


def parse_lord_wearers():
    """Return {item_id: [lord id]}: parse_hero_wearers over the lords only (_hero_elements with
    lords_only), so a piece only a companion or wanderer wears is not lord kit."""
    return _wearers(_hero_elements(lords_only=True))


def _wearers(heroes):
    templates = load_battle_templates()
    wearers = defaultdict(list)
    for hero, container in heroes:
        for item_id in sorted(_hero_battle_items(container, templates)):
            wearers[item_id].append(hero)
    return wearers


def _index_entry(item, folder, slot_type):
    armor = item.find('.//Armor')
    if armor is None or not item.get('id') or item.get('Type') not in ra.CHARACTER_ARMOUR_TYPES:
        return None
    values = ra.parse_current_values(armor, slot_type)
    try:
        weight = float(item.get('weight')) if item.get('weight') is not None else None
    except (TypeError, ValueError):
        weight = None
    return {
        'folder': folder,
        'slot': slot_type,
        'primary': ra._get_primary_stat(values, slot_type),
        # (head, body, arm, leg): generate_armour_classes.py never offers an upgrade that only lowers them
        'armour': tuple(values.get(k, 0) for k in ('head_armor', 'body_armor', 'arm_armor', 'leg_armor')) if values else None,
        'weight': weight,
        'name': ra.get_display_name(item.get('name', '')),
        # the item's own Type, kept for the shipped-table test
        'type': item.get('Type'),
    }


def build_armory_index():
    """Return item_id -> {folder, slot, primary, armour, weight, name, type}: the Armory's five slot
    files of every folder, then the repo's lord armour variants in the folder and slot of their source."""
    index = {}
    armory_dir = ra.ARMORY_DIR
    if not os.path.isdir(armory_dir):
        return index, armory_dir
    for folder in sorted(os.listdir(armory_dir)):
        fpath = os.path.join(armory_dir, folder)
        if not os.path.isdir(fpath):
            continue
        for armor_file, slot_type in ra.SLOT_TYPES.items():
            filepath = os.path.join(fpath, armor_file)
            if not os.path.exists(filepath):
                continue
            try:
                root = ET.parse(filepath).getroot()
            except ET.ParseError:
                continue
            for item in root.findall('.//Item'):
                entry = _index_entry(item, folder, slot_type)
                if entry is not None:
                    index[item.get('id')] = entry
    if os.path.exists(LORD_VARIANTS_XML):
        try:
            root = ET.parse(LORD_VARIANTS_XML).getroot()
        except ET.ParseError:
            root = None
        for item in (root.findall('.//Item') if root is not None else ()):
            source = index.get(lord_variant_source(item.get('id', '')))
            entry = _index_entry(item, source['folder'], source['slot']) if source else None
            if entry is not None:
                index[item.get('id')] = entry
    return index, armory_dir


# =============================================================================
# Derivation
# =============================================================================

def derive():
    wearers = parse_rosters()
    hero_wearers = parse_hero_wearers()
    lord_wearers = parse_lord_wearers()
    index, armory_dir = build_armory_index()

    records = {}
    for item_id, info in index.items():
        slot = info['slot']
        culture = info['folder']
        ws = wearers.get(item_id, [])
        levels = sorted({w['level'] for w in ws})
        anchor = levels[0] if levels else None
        kw_tier = id_keyword_tier(item_id)

        if kw_tier == 'civilian':
            tier, source = 'civilian', 'id-keyword'      # off the combat curve, whoever wears it
        elif anchor is not None:
            tier, source = level_to_tier(anchor), f'roster(L{anchor})'
        elif kw_tier:
            tier, source = kw_tier, 'id-keyword'
        else:
            tier, source = None, 'unworn'

        target = ra._get_primary_stat(ra.calculate_stats(tier, slot, culture, item_id=item_id), slot) if tier else None
        current = info['primary']
        delta = (current - target) if (current is not None and target is not None) else None
        if ra.is_generator_owned(item_id):
            status = 'generator-owned'                   # the restat skips it, so under/over would mislead
        elif delta is None:
            status = 'unworn' if anchor is None else 'no-target'
        elif abs(delta) <= 2:
            status = 'match'
        elif delta < 0:
            status = 'under'
        else:
            status = 'over'

        span = (max(levels) - anchor) if levels else 0
        records[item_id] = {
            'culture': culture,
            'slot': slot,
            'name': info['name'],
            'current': current,
            'armour': info.get('armour'),
            'weight': info['weight'],
            'anchorLevel': anchor,
            'maxLevel': max(levels) if levels else None,
            'levelSpan': span,
            'wearerCount': len(ws),
            'heroWorn': item_id in hero_wearers,         # a hero's kit; never moves the anchor or the tier
            'lordWorn': item_id in lord_wearers,         # a lord's kit (not only a companion's or wanderer's)
            'tier': tier,
            'tierSource': source,
            'target': target,
            'delta': delta,
            'status': status,
            'line': line_suffix(item_id),
            'shared': span >= 15,                # worn across >=3 tier bands -> reuse compromise
            'wearers': [{'troop': w['troop'], 'level': w['level']} for w in sorted(ws, key=lambda x: x['level'])],
        }
    return records, armory_dir


# =============================================================================
# Reporting
# =============================================================================

def render_markdown(records, armory_dir, generated_at):
    by_culture = defaultdict(list)
    for iid, r in records.items():
        by_culture[r['culture']].append((iid, r))

    lines = ["# TAOM Roster-Derived Armor Tiers (read-only, Phase 2)\n"]
    lines.append(f"_Generated {generated_at}. Joins troop rosters to the live armory; an item's tier "
                 "is anchored to its LOWEST wearer (reuse-safe). Target = baseline+cultural-mod for that "
                 "tier (REFERENCE — scale-vs-accept is a Phase-3 call)._\n")

    # Summary
    lines.append("## Per-culture summary\n")
    lines.append("| Culture | Items | Worn | Unworn | Match | Under | Over | Shared (reuse) |")
    lines.append("|---------|------:|-----:|-------:|------:|------:|-----:|---------------:|")
    for culture in sorted(by_culture):
        rs = [r for _, r in by_culture[culture]]
        worn = sum(1 for r in rs if r['anchorLevel'] is not None)
        lines.append(f"| {culture} | {len(rs)} | {worn} | {len(rs) - worn} "
                     f"| {sum(1 for r in rs if r['status'] == 'match')} "
                     f"| {sum(1 for r in rs if r['status'] == 'under')} "
                     f"| {sum(1 for r in rs if r['status'] == 'over')} "
                     f"| {sum(1 for r in rs if r['shared'])} |")
    lines.append("")

    # Per-culture detail: the actionable under/over re-stat list
    for culture in sorted(by_culture):
        rs = sorted(by_culture[culture], key=lambda x: (x[1]['slot'], x[1]['anchorLevel'] or 999, x[0]))
        flagged = [(i, r) for i, r in rs if r['status'] in ('under', 'over')]
        unworn = [(i, r) for i, r in rs if r['status'] == 'unworn']
        lines.append(f"## {culture}\n")
        if not flagged and not unworn:
            lines.append("_All roster-tiered items on target._\n")
            continue
        if flagged:
            lines.append("**Re-stat candidates (current vs roster-tier target):**\n")
            lines.append("| Item | Slot | Anchor L | Tier (src) | Cur→Tgt | Δ | Line | Shared | Wearers |")
            lines.append("|------|------|---------:|------------|:-------:|--:|:----:|:------:|---------|")
            for iid, r in flagged:
                wstr = ', '.join(f"{w['troop']}(L{w['level']})" for w in r['wearers'][:4])
                if len(r['wearers']) > 4:
                    wstr += '…'
                lines.append(f"| `{iid}` | {r['slot']} | {r['anchorLevel']} | {r['tier']} ({r['tierSource']}) "
                             f"| {r['current']}→{r['target']} | {r['delta']:+d} | {r['line'] or ''} "
                             f"| {'yes' if r['shared'] else ''} | {wstr} |")
            lines.append("")
        if unworn:
            lines.append(f"**Unworn by any roster ({len(unworn)}) — roster cannot tier; name/value fallback or hero/display:**\n")
            lines.append("`" + "`, `".join(i for i, _ in unworn[:30]) + ("` …" if len(unworn) > 30 else "`"))
            lines.append("")
    return "\n".join(lines)


# =============================================================================
# Main
# =============================================================================

def main():
    parser = argparse.ArgumentParser(description="Roster-derived armor tiering for TAOM (read-only).")
    parser.add_argument('--stdout', action='store_true', help='Print the per-culture summary')
    parser.add_argument('--culture', default='', help='Restrict the printed summary to one armory folder')
    args = parser.parse_args()

    records, armory_dir = derive()
    if not records:
        print(f"ERROR: no armory items found under {armory_dir} (set $BANNERLORD_GAME_DIR / --armory-path?)")
        sys.exit(1)

    generated_at = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    os.makedirs(DATA_DIR, exist_ok=True)
    os.makedirs(REPORT_DIR, exist_ok=True)
    with open(MAP_JSON, 'w', encoding='utf-8') as f:
        json.dump({'generatedAt': generated_at, 'armoryDir': armory_dir, 'items': records}, f, indent=2)
    with open(REPORT_MD, 'w', encoding='utf-8') as f:
        f.write(render_markdown(records, armory_dir, generated_at))

    worn = sum(1 for r in records.values() if r['anchorLevel'] is not None)
    under = sum(1 for r in records.values() if r['status'] == 'under')
    over = sum(1 for r in records.values() if r['status'] == 'over')
    print(f"Derived tiers for {len(records)} items: {worn} roster-worn, {len(records) - worn} unworn. "
          f"{under} under-target, {over} over-target.")
    print(f"Map:    {MAP_JSON}")
    print(f"Report: {REPORT_MD}")

    if args.stdout:
        by_culture = defaultdict(list)
        for r in records.values():
            if not args.culture or r['culture'] == args.culture:
                by_culture[r['culture']].append(r)
        print(f"\n{'Culture':<14}{'Items':>6}{'Worn':>6}{'Unworn':>7}{'Under':>6}{'Over':>5}{'Shared':>7}")
        print('-' * 51)
        for culture in sorted(by_culture):
            rs = by_culture[culture]
            worn_c = sum(1 for r in rs if r['anchorLevel'] is not None)
            print(f"{culture:<14}{len(rs):>6}{worn_c:>6}{len(rs) - worn_c:>7}"
                  f"{sum(1 for r in rs if r['status'] == 'under'):>6}"
                  f"{sum(1 for r in rs if r['status'] == 'over'):>5}"
                  f"{sum(1 for r in rs if r['shared']):>7}")


if __name__ == '__main__':
    main()
