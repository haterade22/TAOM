#!/usr/bin/env python3
"""Lord kit: each culture's lord templates wear different lord armour, judged per whole set (#1271, after #609).

WHAT A LORD WEARS
-----------------
A hero wears ONE whole battle set. Hero.SetInitialValuesFromCharacter (v1.5.7
TaleWorlds.CampaignSystem/Hero.cs:2321 and 2341):

    List<Equipment> list = characterObject.BattleEquipments.Where((Equipment x) => !x.IsEmpty()).ToList();
    _battleEquipment = list.GetRandomElement().Clone();

and `_battleEquipment` is a [SaveableProperty(210)] (Hero.cs:212), so the set is picked once and kept
in the save. A lord's figure on a part (head, body, arm, leg, each summed over the five armour slots
as the WORN_ARMOUR_* gates add them) is therefore his WORST WHOLE SET. Troops draw each slot from its
own set (rebalance_armor.worn_armour_extremes_per_slot), so a troop's figure stays the per-slot worst.

THE REFERENCE (it informs; it is not a target)
----------------------------------------------
On each part, the best elite-band troop (rebalance_armor.level_to_band) of the lord's culture, of
his race when the culture has an elite troop of that race. A lord is short on a part when his worst
whole set is under it by more than TOLERANCE of it and by SHORT_MIN_POINTS or more (is_short), and
over when his best set passes the reference plus CEILING_MARGIN. A culture with no elite troop of
its own (Khand, the Abanissa) is judged against the culture of its generic templates (Dunland,
Harad). Only occupation Lord heroes are lords: companions and wanderer templates are not.

THE LORD ARMOUR POOL OF A CULTURE
---------------------------------
Pieces of the culture's own Armory folders (those holding at least FOLDER_SHARE of the armour its
troops and lords wear) whose id carries a lord-line token (rebalance_armor.is_lord_kit: lord, noble,
nbl, nob, king, captain) and whose look family the culture's troops or lords are seen in; plus the
hero-line pieces its lords already wear (no troop under level 41 wears them). Hero-named kit
(rebalance_armor.is_excluded: Elrond's, Saruman's) stays out. A family is the id less its slot words,
its tier words and its trailing variant tokens (a to j, a04, b1, lord3, silver); a lord-line piece
with a tier word below lord (`_noble_heavy_`) is a soldier grade and stays out. A piece fits a race when that race
is seen in the piece or its family; the Armory carries no race or gender usage flags, so nothing
else is known about the fit. A lord armour variant (`taom_lord_<source>`, a TAOM copy of an Armory piece
at the lord band, tools/generate_lord_armour_variants.py, #1333) is a pool piece of the culture
tools/lord_armour_variants.json names, whatever its folder, and has its source's family.

THE ASSIGNMENT, FIRST PASS (variety first, the maintainer's word of 2026-11-15)
-------------------------------------------------------------------------------
Within a culture the generic templates (`<family>_bat_template_medium_<x>`) wear different Body
pieces. A template keeps its set when its Body is a lord or hero piece no earlier template wears. A
template in a soldier's piece (worn by a troop under level 41, not in the pool), or sharing its
Body while an unused pool Body fits its race, takes an unused one: a piece no low troop wears first,
then the same family as its old Body, then the family used least. Its Head, Cape, Gloves and Leg
then come from the pool: a piece no template wears in the slot first, then the new Body's family,
else the nearest family; a lord piece already in a slot stays unless the new family has the slot.
Armour points do not decide. When the pool runs out the template keeps what it wears and is
reported.

THE ASSIGNMENT, SECOND PASS (tolerance)
---------------------------------------
Each set is walked slot by slot (TOLERANCE_ORDER); a slot changes only when its part is short or it
wears a soldier piece the pool has a variant for: (a) an unused pool piece; (b) a rule (b) piece
(_rule_b_pieces: an elite troop's piece of the culture's folders, a piece of those folders that shares
an elite piece's id stem, or one no troop, template or lord wears; never start kit), a piece no other
template wears first, a second use only when the own piece leaves the part more than REUSE_GAP under
and the reuse raises it by SHORT_MIN_POINTS or more; (c) else the piece stays and is listed. A swap
never lowers another part, never takes a part over the limit of any culture whose lords wear the
template, and holds on the restat's planned values too. A changed Body brings a Cape of its look
(same_look), the one its wearers wear with it first; templates that share a Body by a reuse take
different other slots where a piece of the Body's look allows. The Khamul pieces stay (KEEP_PIECE_RE).
Named rosters (`faramir_bat_equipment`) and the promoted clones' files are never written here; a
clone follows on promote_borrowed_cultures.py --only <culture> --apply. Civilian sets are not
touched; the report lists the templates whose battle and town kit differ in look, those whose Body
and Cape are of different families, and the templates that share a Body.

THE FALLBACK AND RULER ROSTERS
------------------------------
`taom_<culture>_(lord|ruler)_battle_(male|female)` in taom_lord_template_equipment.xml re-kit every
new AI ruler and the spawned `spc_<culture>_lord_N` lords. They mirror the five armour slots of their
source template _a (FALLBACK_SOURCES); weapons and horses are left alone. The mirror follows _a, so it
can lower a roster's part where the roster wore more than _a (on 2026-11-15 the Umbar rosters' leg
by 17, 38 to 21 against a limit of 19, and the Erebor and Eredluin rosters' head by 4, 54 to 50
against a reference of 54, while their body and leg rose to the reference). The file's
generator is unsafe (#637) and is never run: this tool edits the armour slots only.

Dry run by default. --apply writes byte-faithfully (BOM and newline style kept), re-reads every
planned slot through ElementTree before any file is written, and re-plans from disk.
--values planned judges on the values the armour restat would write (its dry run, in memory).
--pool-md <path> writes the lord armour pool as a markdown table.

Exit codes: 0 done or clean; 1 a change is still planned (--check, or after --apply); 2 nothing could
be read (no Armory, no troops, no lords); 3 a crash.
"""

import argparse
import codecs
import datetime
import os
import re
import statistics
import sys
import traceback
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import derive_armor_tiers as dat  # noqa: E402
import rebalance_armor as ra  # noqa: E402

GENERIC_TEMPLATE_RE = re.compile(r'^(?P<family>[a-z]+)_bat_template_medium_(?P<letter>[a-z])$')
FALLBACK_ROSTER_RE = re.compile(r'^taom_(?P<culture>[a-z]+)_(?P<rank>lord|ruler)_battle_(?P<sex>male|female)$')
ARMOUR_SLOTS = ra.MESH_LADDER_SLOTS
PARTS = tuple(ra._WORN_REGION_STATS)
PART_STAT = dict(ra._WORN_REGION_STATS)
SLOT_OF_TYPE = {'HeadArmor': 'Head', 'BodyArmor': 'Body', 'Cape': 'Cape', 'HandArmor': 'Gloves', 'LegArmor': 'Leg'}
CEILING_MARGIN = 5
# A part is short when it is under the reference by more than this share AND by at least
# SHORT_MIN_POINTS (2026-11-15: 3 points is no reason to trade a look).
TOLERANCE = 0.10
SHORT_MIN_POINTS = 3
# Rule (b) puts a piece on a second template only when the template's own piece leaves the part more
# than this share under the reference (the maintainer's word: variety first).
REUSE_GAP = 0.25
# The mesh ladder's lord tier (rebalance_armor.allowed_mesh_tiers, #609): lord armour is for level 41+
# troops or lords, so a piece a lower troop wears is a soldier's piece.
LORD_LEVEL = 41
# The validator's creature skip (Validator._WORN_ARMOUR_CREATURE_MARKERS, pinned by a test).
CREATURE_MARKERS = ('troll', 'creature')
# A folder belongs to a culture when it holds at least this share of the armour its people wear;
# Mordor's folder is worn a little by every orc realm and is not their lord pool.
FOLDER_SHARE = 0.10
# Roster culture -> the template family whose _a its fallback and ruler rosters mirror. The first
# eighteen are generate_lord_template_equipment.CULTURES; goblin, mistymountainorcs, lindon and
# bluecraig hold regions no generator checks. The promoted cultures are in PROMOTED_FALLBACKS.
FALLBACK_SOURCES = {
    'gondor': 'gondor', 'mordor': 'mordor', 'erebor': 'erebor', 'rivendell': 'rivendell',
    'lothlorien': 'lothlorien', 'mirkwood': 'mirkwood', 'isengard': 'isengard', 'gundabad': 'gundabad',
    'dolguldur': 'dolguldur', 'umbar': 'umbar', 'shaghana': 'harad', 'abanissa': 'harad',
    'vlandia': 'rohan', 'empire': 'dunland', 'aserai': 'harad', 'khuzait': 'rhun', 'sturgia': 'dale',
    'battania': 'harad', 'goblin': 'goblin', 'mistymountainorcs': 'mistymountainorcs',
    'lindon': 'lindon', 'bluecraig': 'bluecraig',
}
# The promoter writes these cultures' regions from their source block; checked, never written here.
PROMOTED_FALLBACKS = {c: c for c in ('arthedain', 'cardolan', 'rhudaur', 'eredluin', 'anduinvale',
                                     'angaladh', 'dorwinion', 'southrhovanion')}
XSL_NS = dat.XSL_NS


def _culture(value):
    return (value or '').replace('Culture.', '', 1) or None


def _race(value):
    return value or 'human'


def _line_of(text, needle):
    pos = text.find(needle)
    return text.count('\n', 0, pos) + 1 if pos >= 0 else 0


# =============================================================================
# Lords and troops
# =============================================================================

def _lord_record(lid, culture, race, path, line, container):
    """One lord: its inline battle sets, the battle template ids it names and the direct
    <equipment> children of <Equipments>, which replace their slot in every set
    (BasicCharacterObject.Deserialize, AddOverriddenEquipments)."""
    templates, inline, overrides = [], [], {}
    for roster in list(container.iter('EquipmentRoster')) + list(container.iter('EquipmentSet')):
        if dat._is_civilian_roster(roster):
            continue
        own = dat._equipment_slots(roster)
        if own:
            inline.append(own)
        if roster.tag == 'EquipmentSet' and roster.get('id'):
            templates.append(roster.get('id'))
    for equipments in container.iter('Equipments'):
        overrides.update(dat._equipment_slots(equipments, tag_lower=False))
    return {'id': lid, 'culture': _culture(culture), 'race': race, 'file': str(path), 'line': line,
            'templates': templates, 'inline_sets': inline, 'overrides': overrides}


def _parse(path, failures):
    try:
        text = path.read_text(encoding='utf-8-sig')
        return text, ET.fromstring(text)
    except (ET.ParseError, OSError) as exc:
        if failures is not None:
            failures.append((str(path), str(exc)))
        return None, None


def load_lords(moduledata, failures=None):
    """Every lord TAOM defines: an NPCCharacter with is_hero="true" and occupation="Lord" in
    characters/*.xml, and each lords.xslt template that writes an <Equipments> block with
    occupation Lord (the retagged vanilla lords; their id, culture and race are xsl:attributes).
    A file that does not parse is appended to `failures` as (path, error) when a list is given."""
    md = Path(moduledata)
    lords = []
    for path in sorted((md / 'characters').glob('*.xml')):
        text, root = _parse(path, failures)
        if root is None:
            continue
        for npc in root.iter('NPCCharacter'):
            if npc.get('is_hero') != 'true' or npc.get('occupation') != 'Lord':
                continue
            lid = npc.get('id', '')
            lords.append(_lord_record(lid, npc.get('culture'), npc.get('race'), path,
                                      _line_of(text, 'id="%s"' % lid), npc))
    xslt = md / 'lords.xslt'
    if xslt.is_file():
        text, root = _parse(xslt, failures)
        for template in (root.iter(XSL_NS + 'template') if root is not None else ()):
            if template.find('.//Equipments') is None:
                continue
            attrs = {a.get('name'): (a.text or '').strip() for a in template.iter(XSL_NS + 'attribute')}
            if attrs.get('occupation') != 'Lord' or not attrs.get('id'):
                continue
            lid = attrs['id']
            lords.append(_lord_record(lid, attrs.get('culture'), attrs.get('race') or None, xslt,
                                      _line_of(text, '>%s</xsl:attribute>' % lid), template))
    return lords


def lord_sets(lord, templates):
    """The lord's battle sets as {slot: item id}: inline sets, then each named template's sets
    ({roster id: [sets]}), each with the direct overrides applied."""
    sets = list(lord['inline_sets'])
    for rid in lord['templates']:
        sets.extend(templates.get(rid, ()))
    if not sets:
        return [dict(lord['overrides'])] if lord['overrides'] else []
    return [{**st, **lord['overrides']} for st in sets]


def own_sets(lord, templates):
    """lord_sets without the generic lord templates: what this lord wears of his own (inline sets and
    named rosters). A race is seen in a piece only here or on a troop; a lord in a shared template is
    no proof that its pieces fit him, which is the question the assignment asks."""
    return lord_sets(dict(lord, templates=[t for t in lord['templates'] if not GENERIC_TEMPLATE_RE.match(t)]),
                     templates)


def reference_troops(troops, exempt=()):
    """The troops a lord is held to and the pool is read from: troops/troops_*.xml entries with a
    level and battle sets, minus the exempt ids and the creatures."""
    out = {}
    for tid, rec in troops.items():
        fname = re.split(r'[\\/]', rec.get('file') or '')[-1]
        if not (fname.startswith('troops_') and fname.endswith('.xml')):
            continue
        if rec.get('level') is None or not rec.get('sets') or tid in exempt:
            continue
        if any(m in tid.lower() for m in CREATURE_MARKERS):
            continue
        out[tid] = rec
    return out


# =============================================================================
# The measure
# =============================================================================

def _stat(items, iid, part):
    return items.get(iid, {}).get('stats', {}).get(PART_STAT[part], 0) if iid else 0


def set_totals(st, items):
    """{part: the sum over the five armour slots of one whole set}; an unfilled slot adds 0."""
    return {p: sum(_stat(items, st.get(s), p) for s in ARMOUR_SLOTS) for p in PARTS}


def whole_set_extremes(sets, items):
    """(worst, best) {part: total} over whole battle sets: a hero wears one set entire."""
    totals = [set_totals(st, items) for st in sets]
    if not totals:
        return {p: 0 for p in PARTS}, {p: 0 for p in PARTS}
    return ({p: min(t[p] for t in totals) for p in PARTS}, {p: max(t[p] for t in totals) for p in PARTS})


def _elite_pool(troops, culture, race):
    elite = sorted(tid for tid, t in troops.items()
                   if t.get('culture') == culture and t.get('sets') and t.get('level') is not None
                   and ra.level_to_band(int(t['level'])) == 'elite')
    same = [tid for tid in elite if _race(troops[tid].get('race')) == _race(race)]
    return same or elite


def culture_reference(troops, items, culture, race=None):
    """{part: (value, troop id)}: on each part the elite-band troop of the culture (its race's when
    the culture has one) whose per-slot worst case is highest, the lowest id on a tie; None for a
    culture with no elite troop."""
    pool = _elite_pool(troops, culture, race)
    if not pool:
        return None
    worst = {tid: ra.worn_armour_extremes_per_slot(troops[tid], items)[0] for tid in pool}
    out = {}
    for p in PARTS:
        tid = min(pool, key=lambda t: (-worst[t][p], t))
        out[p] = (worst[tid][p], tid)
    return out


def is_short(value, reference):
    """Under the reference by MORE than TOLERANCE of it and by at least SHORT_MIN_POINTS (2026-11-15):
    a part inside the tolerance is never a reason to change a piece or to warn."""
    gap = reference - value
    return gap > TOLERANCE * reference and gap >= SHORT_MIN_POINTS


def judge_lords(lords, templates, troops, items, template_cultures=None):
    """[{lord, culture, judged_as, race, file, line, generic, worst, best, ref, under, short, over}] for
    every judged lord. generic is the tuple of his templates when generic lord templates alone dress him
    (no inline set, no override), else None: every lord with the same tuple wears the same sets. under is {part: (worst set, reference, troop)} where the worst whole set is under the
    reference, short the same where it is under by more than TOLERANCE; over is {part: (best set,
    limit)} where the best whole set passes the reference plus CEILING_MARGIN. A lord of a culture
    with no elite troop is judged against the culture of his generic templates (`template_cultures`,
    {roster id: culture}): Khand wears Dunland's, the Abanissa Harad's. Without one he is left out."""
    template_cultures = template_cultures or {}
    refs, rows = {}, []
    for lord in lords:
        culture = lord['culture']
        key = (culture, _race(lord['race']))
        if key not in refs:
            refs[key] = culture_reference(troops, items, *key)
        ref = refs[key]
        if ref is None:
            worn = Counter(template_cultures[t] for t in lord['templates'] if t in template_cultures)
            if worn:
                culture = worn.most_common(1)[0][0]
                key = (culture, _race(lord['race']))
                if key not in refs:
                    refs[key] = culture_reference(troops, items, *key)
                ref = refs[key]
        if ref is None:
            continue
        worst, best = whole_set_extremes(lord_sets(lord, templates), items)
        under = {p: (worst[p], ref[p][0], ref[p][1]) for p in PARTS if worst[p] < ref[p][0]}
        generic = (tuple(lord['templates']) if lord['templates'] and not lord['inline_sets']
                   and not lord['overrides'] and all(GENERIC_TEMPLATE_RE.match(t) for t in lord['templates'])
                   else None)
        rows.append({
            'lord': lord['id'], 'culture': lord['culture'], 'judged_as': culture, 'race': lord['race'],
            'file': lord['file'], 'line': lord['line'], 'generic': generic, 'worst': worst, 'best': best,
            'ref': ref,
            'under': under, 'short': {p: v for p, v in under.items() if is_short(v[0], v[1])},
            'over': {p: (best[p], ref[p][0] + CEILING_MARGIN) for p in PARTS
                     if best[p] > ref[p][0] + CEILING_MARGIN},
        })
    return rows


# =============================================================================
# Families and the lord armour pool
# =============================================================================

SLOT_WORDS = frozenset(
    'helmet helm hlm hood hat cap coif crown chest torso body armor armour arm tunic coat jerkin robe '
    'cuirass hauberk pauldron pauldrons pauld shoulder shoulders cape cloak mantle gorg gorget bracer '
    'bracers gauntlet gauntlets gloves glove glv vambrace vambraces boots boot bts greaves grvs shoes '
    'shoe legs leg'.split())
PREFIX_WORDS = frozenset(('sk', 'sm', 'wm'))
COLOUR_WORDS = frozenset('silver gold silvergold bronze black white red blue green brown grey gray yellow'.split())
# A tier word names the variant's grade, not its look: a line's heavy and lord chainmail are one family.
TIER_WORDS = frozenset(('light', 'med', 'medium', 'heavy', 'elite', 'lord'))
_VARIANT_RE = re.compile(r'^(?:[a-z]{1,2}\d*|\d+[a-z]?)$')
_LORD_DIGIT_RE = re.compile(r'^(lord|noble|captain)(\d+)$')


def _split(iid):
    """(family tokens, variant tokens) of an armour id; a lord armour variant's are its source's."""
    tokens = (dat.lord_variant_source(iid) or iid or '').lower().split('_')
    while tokens and tokens[0] in PREFIX_WORDS:
        tokens = tokens[1:]
    variant = []
    while len(tokens) > 1 and (_VARIANT_RE.match(tokens[-1]) or tokens[-1] in COLOUR_WORDS):
        variant.insert(0, tokens.pop())
    family = []
    for tok in tokens:
        m = _LORD_DIGIT_RE.match(tok)
        if m:
            family.append(m.group(1))
            variant.insert(0, m.group(2))
        elif tok not in SLOT_WORDS and tok not in TIER_WORDS:
            family.append(tok)
    return family, variant


def _canon(token):
    # An artist's transposition (`chivlary` beside `chivalry`) keeps one family.
    return ''.join(sorted(token)) if len(token) >= 5 else token


def family_key(iid):
    """The look family of an armour id: its tokens less the slot words and trailing variants."""
    return tuple(_canon(t) for t in _split(iid)[0])


def variant_of(iid):
    return '_'.join(_split(iid)[1])


def similarity(a, b):
    """Shared family tokens of two ids (a multiset count)."""
    ca, cb = Counter(family_key(a)), Counter(family_key(b))
    return sum((ca & cb).values())


def _slot_of(rec):
    return SLOT_OF_TYPE.get(rec.get('type'))


def lowest_troop_wearers(troops):
    """{item id: (level, troop id)}: the lowest troop that wears each armour piece in a battle set."""
    out = {}
    for tid, t in troops.items():
        if t.get('level') is None:
            continue
        for st in t.get('sets') or ():
            for slot in ARMOUR_SLOTS:
                iid = st.get(slot)
                if iid and (iid not in out or (int(t['level']), tid) < out[iid]):
                    out[iid] = (int(t['level']), tid)
    return out


def lord_pool(culture, items, troops, lord_battle_sets, template_sets=(), lowest=None, variants=None):
    """{'folders', 'slots': {slot: [ids]}, 'fits': {id: {races}}, 'lowest': {id: (level, troop)}}.
    `lord_battle_sets` is [(race, set)] over the culture's lords' own sets (own_sets); `template_sets`
    the culture's generic template sets (read for the folders and for kit lords already wear).
    `variants` is {lord armour variant id: culture} (default: tools/lord_armour_variants.json): a
    variant is a pool piece of its own culture, whatever its folder (#1333)."""
    lowest = lowest if lowest is not None else lowest_troop_wearers(troops)
    variants = dat.lord_variant_cultures() if variants is None else variants
    worn = [(_race(t.get('race')), st) for t in troops.values() if t.get('culture') == culture
            for st in t['sets']] + [(_race(r), st) for r, st in lord_battle_sets]
    folders = Counter()
    fam_races, piece_races, lord_worn = defaultdict(set), defaultdict(set), set()
    for race, st in worn:
        for slot in ARMOUR_SLOTS:
            iid = st.get(slot)
            if iid and iid in items:
                folders[items[iid].get('folder')] += 1
                fam_races[family_key(iid)].add(race)
                piece_races[iid].add(race)
    for _race_, st in lord_battle_sets:
        lord_worn.update(st.get(s) for s in ARMOUR_SLOTS if st.get(s))
    for st in template_sets:
        for slot in ARMOUR_SLOTS:
            iid = st.get(slot)
            if iid and iid in items:
                folders[items[iid].get('folder')] += 1
                lord_worn.add(iid)
    total = sum(n for f, n in folders.items() if f)
    own = sorted(f for f, n in folders.items() if f and total and n / total >= FOLDER_SHARE)
    slots = defaultdict(list)
    for iid in sorted(items):
        rec = items[iid]
        slot = _slot_of(rec)
        if slot and iid in variants:
            if variants[iid] == culture:
                slots[slot].append(iid)
            continue
        if not slot or rec.get('folder') not in own or ra.is_monster_kit(iid):
            continue
        if ra.mesh_tier_of(iid) == 'civilian' or ra.is_excluded(iid, rec.get('name')):
            continue
        # A tiered noble piece (`_noble_heavy_`) is a soldier grade of a noble line, not lord armour.
        marked = (ra.is_lord_kit(iid) and ra.mesh_tier_of(iid) in (None, 'lord')
                  and family_key(iid) in fam_races)
        hero_line = iid in lord_worn and (iid not in lowest or lowest[iid][0] >= LORD_LEVEL)
        if marked or hero_line:
            slots[slot].append(iid)
    members = {i for ids in slots.values() for i in ids}
    return {'folders': own, 'slots': dict(slots),
            'fits': {i: piece_races.get(i, set()) | fam_races.get(family_key(i), set()) for i in members},
            'lowest': {i: lowest[i] for i in members if i in lowest}}


# =============================================================================
# The assignment
# =============================================================================

def _template_race(users, culture_lords):
    races = Counter(_race(l['race']) for l in users) or Counter(_race(l['race']) for l in culture_lords)
    return races.most_common(1)[0][0] if races else 'human'


def plan_assignments(templates, lords, troops, items, clones=None, tolerance=True, elite_wearers=None,
                     planned=None):
    """(changes, notes) for the generic lord templates in `templates` ({roster id: {'file',
    'culture', 'sets'}}). A change is {roster, template, file, set, slot, old, new, rule}; a note
    says what could not be given (a pool that ran out, a race no lord piece is seen on).
    `elite_wearers` (default `troops`) are the troops whose elite pieces rule (b) may give: the full
    pool, since a troop left off the reference (Harad's mumak riders) still wears its line's pieces.
    `planned` (items with the other value set: the restat's planned values on a live run) must also
    accept every swap of the second pass."""
    clones = clones or {}
    resolve = {rid: t['sets'] for rid, t in templates.items()}
    lowest = lowest_troop_wearers(troops)
    by_culture = defaultdict(list)
    for rid in sorted(templates):
        if GENERIC_TEMPLATE_RE.match(rid):
            by_culture[templates[rid]['culture']].append(rid)
    changes, notes = [], []
    wearers = elite_wearers or troops
    # Worn: by a troop, a generic template or a lord's own sets, as read; rule (b)'s "unworn" is none.
    worn = set(lowest_troop_wearers(wearers))
    worn.update(i for t in templates.values() for st in t['sets'] for i in st.values())
    worn.update(i for l in lords for st in l.get('inline_sets') or () for i in st.values())
    worn.update(i for l in lords for i in (l.get('overrides') or {}).values())
    ctx = {'wearers': wearers, 'planned': planned, 'worn': worn,
           'fam_races': defaultdict(set), 'cape_with': cape_pairs(wearers, lords)}
    for t in wearers.values():
        for st in t.get('sets') or ():
            for slot in ARMOUR_SLOTS:
                if st.get(slot):
                    ctx['fam_races'][family_key(st[slot])].add(_race(t.get('race')))
    for culture in sorted(by_culture, key=str):
        rids = by_culture[culture]
        culture_lords = [l for l in lords if l['culture'] == culture]
        users = {rid: [l for l in lords if {rid, *clones.get(rid, ())} & set(l['templates'])] for rid in rids}
        pool = lord_pool(culture, items, troops,
                         [(l['race'], st) for l in culture_lords for st in own_sets(l, resolve)],
                         [st for rid in rids for st in templates[rid]['sets']], lowest)
        bodies = set(pool['slots'].get('Body', ()))
        taken, slot_uses, need = Counter(), Counter(), []
        for rid in rids:
            for index, st in enumerate(templates[rid]['sets']):
                b = st.get('Body')
                soldier = bool(b) and b not in bodies and b in lowest and lowest[b][0] < LORD_LEVEL
                if b and not soldier and taken[b] == 0:
                    taken[b] += 1
                    for slot in ARMOUR_SLOTS:
                        if st.get(slot):
                            slot_uses[(slot, st[slot])] += 1
                else:
                    need.append((rid, index, st, soldier))
        for rid, index, st, soldier in need:
            race = _template_race(users[rid], culture_lords)
            b = st.get('Body')
            fit = [p for p in sorted(bodies) if race in pool['fits'].get(p, ())]
            unused = [p for p in fit if taken[p] == 0]
            if soldier:
                # A lord piece a lower troop wears is priced at that troop's band, so trading a soldier
                # piece for it buys no lord look and costs armour; the template keeps its own.
                floor = lowest[b][0]
                lower = [p for p in unused if p in pool['lowest'] and pool['lowest'][p][0] < floor]
                unused = [p for p in unused if p not in lower]
                if lower and not unused:
                    notes.append('%s set %d: the only unused %s lord Body (%s) is worn from a level under '
                                 'the %d of its own %s, so it keeps that piece'
                                 % (rid, index, culture, ', '.join(lower), floor, b))
                    taken[b] += 1
                    continue
            if not unused:
                # No reuse: putting one lord Body on several templates in place of their own pieces
                # would cost the culture looks, which the maintainer ranks above armour points.
                if not fit:
                    notes.append('%s set %d: no %s lord Body piece is seen on a %s; keeps %s'
                                 % (rid, index, culture, race, b or '(empty)'))
                else:
                    notes.append('%s set %d: every %s lord Body that fits a %s is in use, so the pool runs '
                                 'out and this template keeps %s (a reuse)' % (rid, index, culture, race, b))
                if b:
                    taken[b] += 1
                continue
            fam_uses = Counter()
            for p, n in taken.items():
                fam_uses[family_key(p)] += n
            # A lord piece a low troop also wears is priced at that troop's band until the troop moves
            # off it, so a piece no low troop wears comes first.
            new = min(unused, key=lambda p: (p in pool['lowest'] and pool['lowest'][p][0] < LORD_LEVEL,
                                             family_key(p) != family_key(b or ''), -similarity(p, b or ''),
                                             fam_uses[family_key(p)], p))
            taken[new] += 1
            rule = 'soldier piece' if soldier else ('empty Body' if not b else 'shared Body')
            changes.append(_change(rid, templates[rid]['file'], index, 'Body', b, new, rule))
            for slot in ('Head', 'Cape', 'Gloves', 'Leg'):
                fit_s = [p for p in pool['slots'].get(slot, ()) if race in pool['fits'].get(p, ())]
                if not fit_s:
                    continue
                # A piece no template wears comes before a second use of the family match, so two
                # templates of one family do not share a helmet while the pool has others.
                pick = min(fit_s, key=lambda p: (slot_uses[(slot, p)], family_key(p) != family_key(new),
                                                 -similarity(p, new), variant_of(p) != variant_of(new), p))
                cur = st.get(slot)
                same = family_key(pick) == family_key(new)
                # A lord piece already in the slot stays unless the new Body's own family has the slot.
                if cur == pick or (cur in fit_s and (not same or family_key(cur) == family_key(new))):
                    slot_uses[(slot, cur)] += 1
                    continue
                slot_uses[(slot, pick)] += 1
                changes.append(_change(rid, templates[rid]['file'], index, slot, cur, pick,
                                       'family of the new Body' if same else 'nearest family'))
        if not tolerance:
            continue   # the variety pass alone (its tests)
        culture_changes = [c for c in changes if c['template'] in set(rids)]
        changes = [c for c in changes if c['template'] not in set(rids)]
        changes += _tolerance_pass(culture, rids, templates, culture_changes, users, culture_lords, pool,
                                   troops, items, lowest, notes, ctx)
    return changes, notes


# The part a slot is judged on (a Cape's body armour is its main stat, a Gloves piece's the arm).
SLOT_PART = {'Head': 'head', 'Body': 'body', 'Cape': 'body', 'Gloves': 'arm', 'Leg': 'leg'}
TOLERANCE_ORDER = ('Body', 'Head', 'Cape', 'Gloves', 'Leg')
# The maintainer keeps the Khamul pieces on the Rhun lords (2026-11-15), so no swap takes them off.
KEEP_PIECE_RE = re.compile(r'_khml_')


def _closeness(value, reference):
    """Closest to the reference from below first, then the least over it: lords do not all converge
    on the single strongest piece."""
    return (0, reference - value) if value <= reference else (1, value - reference)


def _elite_pieces(culture, race, slot, troops, items, folders):
    """Pieces of the culture's own Armory folders that its elite-band troops of this race wear in
    this slot. A vanilla piece (no folder), monster, civilian or hero-named kit never qualifies."""
    out = set()
    for t in troops.values():
        if t.get('culture') != culture or t.get('level') is None or ra.level_to_band(int(t['level'])) != 'elite':
            continue
        if _race(t.get('race')) != _race(race):
            continue
        for st in t['sets']:
            iid = st.get(slot)
            rec = items.get(iid) if iid else None
            if not rec or rec.get('folder') not in folders or _slot_of(rec) != slot:
                continue
            if ra.is_monster_kit(iid) or ra.mesh_tier_of(iid) == 'civilian' or ra.is_excluded(iid, rec.get('name')):
                continue
            out.add(iid)
    return out


_STEM_RE = re.compile(r'_(?:[a-z]\d*|\d+[a-z]?)$')
# The player's start kit (generate_starter_armor.py) is worn by no troop, but it is his look, not a lord's.
START_KIT_PREFIX = 'starter_'


def _stem(iid):
    """The id less its trailing variant letter or number (`sk_hr_st_helmet_heavy_c` ->
    `sk_hr_st_helmet_heavy`)."""
    return _STEM_RE.sub('', iid)


def _colours(iid):
    return {t for t in _split(iid)[1] if t in COLOUR_WORDS}


def same_look(body, other, worn_with=()):
    """Whether a piece goes with a Body: a troop or lord wears the two together (`worn_with`), or one
    family of two or more tokens starts the other (a line's chainmail and its pauldron) in the same colour. A second line of
    the culture, or a silver chest over plain pauldrons, is a broken pair."""
    if other in worn_with:
        return True
    fb, fo = family_key(body), family_key(other)
    n = min(len(fb), len(fo))
    # A one-token family (the culture's bare prefix) starts every id of the culture, so it must match whole.
    same = fb[:n] == fo[:n] if n >= 2 else fb == fo
    return same and _colours(body) == _colours(other)


def cape_pairs(wearers, lords):
    """{Body: Counter of the Capes worn with it} over the troops' sets and the lords' own sets."""
    out = defaultdict(Counter)
    for st in [st for t in wearers.values() for st in t.get('sets') or ()] + [
            st for l in lords for st in l.get('inline_sets') or ()]:
        if st.get('Body') and st.get('Cape'):
            out[st['Body']][st['Cape']] += 1
    return out


def _rule_b_pieces(culture, race, slot, ctx, items, folders):
    """Rule (b)'s candidates: the elite pieces (_elite_pieces over every troop), plus (i) a piece of the
    culture's own folders that shares an elite piece's stem and (ii) a piece of those folders no troop
    wears, in the slot. Never a vanilla, monster, civilian, hero-named or start-kit piece, nor a piece
    whose family is seen only on another race."""
    elite = _elite_pieces(culture, race, slot, ctx['wearers'], items, folders)
    stems = {_stem(i) for i in elite}
    out = set(elite)
    for iid, rec in items.items():
        if rec.get('folder') not in folders or _slot_of(rec) != slot or iid in out:
            continue
        if ra.is_monster_kit(iid) or ra.mesh_tier_of(iid) == 'civilian' or ra.is_excluded(iid, rec.get('name')):
            continue
        if iid.startswith(START_KIT_PREFIX):
            continue
        races = ctx['fam_races'].get(family_key(iid))
        if races and _race(race) not in races:
            continue
        if _stem(iid) in stems or iid not in ctx['worn']:
            out.add(iid)
    return out


def _tolerance_pass(culture, rids, templates, base_changes, users, culture_lords, pool, troops, items, lowest,
                    notes, ctx):
    """The variety changes of one culture plus, per set, the slots whose part is short: (a) an unused
    lord-pool piece, the Body's family first; (b) when no pool piece fits, a rule (b) piece
    (_rule_b_pieces), distinct across the templates, and a second use only when the own piece leaves
    the part more than REUSE_GAP under and the reuse raises it by SHORT_MIN_POINTS or more; (c) else the piece stays and is listed. A swap never lowers
    another part, never takes a part over the limit of any culture whose lords wear the template, holds
    on the planned values too, and prefers the candidate closest to the reference from below. A changed
    Body brings the Cape its wearers wear with it; templates that came to share a Body by a reuse take
    different other slots where a piece allows. It runs to a fixed point, so a second run plans
    nothing."""
    cur = simulate({rid: templates[rid]['sets'] for rid in rids}, base_changes)
    origin = {(c['template'], c['set'], c['slot']): c['old'] for c in base_changes}
    rules = {(c['template'], c['set'], c['slot']): c['rule'] for c in base_changes}
    vsets = [items] + ([ctx['planned']] if ctx.get('planned') else [])
    cache, refs, kept, held = {}, {}, {}, {}

    def uses_by_others(slot, iid, rid):
        return sum(1 for r in rids if r != rid for st in cur[r] if st.get(slot) == iid)

    def b_pieces(race, slot):
        if (race, slot) not in cache:
            cache[(race, slot)] = _rule_b_pieces(culture, race, slot, ctx, items, pool['folders'])
        return cache[(race, slot)]

    def ref_and_limits(rid):
        race = _template_race(users.get(rid, ()), culture_lords)
        if race not in refs:
            refs[race] = culture_reference(troops, items, culture, race)
        ref = refs[race]
        if ref is None:
            return race, None, None
        # A lord of another culture on the template (Bluecraig on goblin) is held to his own limit.
        limits = {q: ref[q][0] + CEILING_MARGIN for q in PARTS}
        for other in sorted({l['culture'] for l in users.get(rid, ())} - {culture}, key=str):
            if (other, race) not in refs:
                refs[(other, race)] = culture_reference(troops, items, other, race)
            if refs[(other, race)]:
                for q in PARTS:
                    limits[q] = min(limits[q], refs[(other, race)][q][0] + CEILING_MARGIN)
        return race, ref, limits

    def check(st, slot, iid, ref, limits, own):
        """The new live totals when the swap holds on every value set, else None. `own` says what the
        slot's part may do: 'raise' (a short part must rise), 'keep' (never fall) or 'stay' (may fall
        while it is not short)."""
        p = SLOT_PART[slot]
        result = None
        for k, vals in enumerate(vsets):
            old = set_totals(st, vals)
            new = set_totals(dict(st, **{slot: iid}), vals)
            for q in PARTS:
                if new[q] > limits[q] and new[q] > old[q]:
                    return None
                if q != p and new[q] < old[q]:
                    return None
            if k == 0:
                if own == 'raise' and new[p] <= old[p]:
                    return None
                if own == 'keep' and new[p] < old[p]:
                    return None
                if own == 'stay' and new[p] < old[p] and is_short(new[p], ref[p][0]):
                    return None
                result = new
            elif new[p] < old[p] and own != 'stay':
                return None
        return result

    def put(rid, index, slot, iid, rule):
        k = (rid, index, slot)
        origin.setdefault(k, cur[rid][index].get(slot))
        rules[k] = rule
        cur[rid][index][slot] = iid
        kept.pop(k, None)

    for _round in range(8):
        moved = False
        for rid in rids:
            race, ref, limits = ref_and_limits(rid)
            if ref is None:
                continue
            for index, st in enumerate(cur[rid]):
                for slot in TOLERANCE_ORDER:
                    p = SLOT_PART[slot]
                    have = st.get(slot)
                    body = st.get('Body')
                    if (slot == 'Cape' and body and origin.get((rid, index, 'Body'), body) != body
                            and (not have or not same_look(body, have, ctx['cape_with'].get(body, ())))):
                        # A changed Body brings the Cape its wearers wear with it, else one of its look.
                        pairs = [c for c, _n in ctx['cape_with'].get(body, Counter()).most_common()]
                        pairs += sorted(c for c in b_pieces(race, 'Cape') | set(pool['slots'].get('Cape', ()))
                                        if same_look(body, c))
                        for c in dict.fromkeys(pairs):
                            if c != have and check(st, 'Cape', c, ref, limits, 'keep') is not None:
                                put(rid, index, 'Cape', c, 'pair: the Cape of the new Body')
                                have, moved = c, True
                                break
                    old = set_totals(st, items)
                    short = is_short(old[p], ref[p][0])
                    soldier = (have and have not in pool['slots'].get(slot, ()) and have in lowest
                               and lowest[have][0] < LORD_LEVEL)
                    if not short and not soldier:
                        continue
                    if have and KEEP_PIECE_RE.search(have):
                        if short:
                            held[(rid, index, slot)] = (have, old[p], ref[p][0])
                        continue
                    fits = [i for i in pool['slots'].get(slot, ()) if race in pool['fits'].get(i, ())]
                    body_fam = family_key(body or '')
                    if not short:
                        # As in the variety pass: a piece a lower troop wears buys no lord look.
                        fits = [i for i in fits if not (i in lowest and lowest[i][0] < lowest[have][0])]
                    own = 'raise' if short else 'stay'
                    unused = [(i, check(st, slot, i, ref, limits, own)) for i in fits
                              if i != have and uses_by_others(slot, i, rid) == 0]
                    unused = [(i, n) for i, n in unused if n is not None]
                    pick, rule, why = None, None, 'no lord or rule (b) piece of the culture raises it within the limits'
                    if unused:
                        pick = min(unused, key=lambda c: (family_key(c[0]) != body_fam,
                                                          _closeness(c[1][p], ref[p][0]), c[0]))[0]
                        rule = 'a: unused lord piece'
                    elif short:
                        cands = [(i, check(st, slot, i, ref, limits, 'raise')) for i in b_pieces(race, slot)
                                 if i != have]
                        cands = [(i, n) for i, n in cands if n is not None]
                        fresh = [(i, n) for i, n in cands if uses_by_others(slot, i, rid) == 0]
                        if not fresh and cands and ref[p][0] - old[p] <= REUSE_GAP * ref[p][0]:
                            cands = []
                            why = ('every piece that raises it is on another template, and a reuse needs the '
                                   'own piece more than %d percent under' % round(REUSE_GAP * 100))
                        elif not fresh and cands:
                            # A reuse costs a look; one that buys less than a short gap is not worth it.
                            cands = [(i, n) for i, n in cands if n[p] - old[p] >= SHORT_MIN_POINTS]
                            if not cands:
                                why = ('every piece that raises it is on another template, and a reuse would '
                                       'raise it by less than %d points' % SHORT_MIN_POINTS)
                        if cands:
                            pick = min(cands, key=lambda c: (uses_by_others(slot, c[0], rid),
                                                             family_key(c[0]) != body_fam,
                                                             _closeness(c[1][p], ref[p][0]), c[0]))[0]
                            rule = 'b: rule (b) piece' if fresh else 'b: rule (b) piece, a reuse'
                    if pick is None:
                        if short:
                            kept[(rid, index, slot)] = (have, old[p], ref[p][0], why)
                        continue
                    put(rid, index, slot, pick, rule)
                    moved = True
        # Templates that came to share a Body by a reuse take different other slots where a piece allows.
        sharing = defaultdict(list)
        for rid in rids:
            for index, st in enumerate(cur[rid]):
                if st.get('Body'):
                    sharing[st['Body']].append((rid, index))
        for body, members in sorted(sharing.items()):
            if len(members) < 2 or not any('reuse' in rules.get((r, i, 'Body'), '') for r, i in members):
                continue
            # The templates that reused the Body come last, so they are the ones that change.
            members = sorted(members, key=lambda m: ('reuse' in rules.get((m[0], m[1], 'Body'), ''), m))
            for slot in ('Head', 'Cape', 'Gloves', 'Leg'):
                seen = set()
                for rid, index in members:
                    st = cur[rid][index]
                    have = st.get(slot)
                    if not have or have not in seen or (have and KEEP_PIECE_RE.search(have)):
                        seen.add(have)
                        continue
                    race, ref, limits = ref_and_limits(rid)
                    taken = {cur[r][i].get(slot) for r, i in members}
                    pieces = (set(i for i in pool['slots'].get(slot, ()) if race in pool['fits'].get(i, ()))
                              | b_pieces(race, slot)) - taken
                    if slot == 'Cape':
                        # Variety never breaks a Body and Cape pair.
                        pieces = {c for c in pieces
                                  if same_look(body, c, ctx['cape_with'].get(body, ()))}
                    cands = [(i, check(st, slot, i, ref, limits, 'stay')) for i in pieces]
                    cands = [(i, n) for i, n in cands if n is not None]
                    if cands:
                        p = SLOT_PART[slot]
                        pick = min(cands, key=lambda c: (not same_look(body, c[0]),
                                                         uses_by_others(slot, c[0], rid),
                                                         _closeness(c[1][p], ref[p][0]), c[0]))[0]
                        put(rid, index, slot, pick, 'variety: differs on a shared Body')
                        have, moved = pick, True
                    seen.add(have)
        if not moved:
            break
    for (rid, index, slot), (have, value, reference) in sorted(held.items()):
        notes.append('(kept) %s set %d %s: %s stays (a Khamul piece), %s %d against %d'
                     % (rid, index, slot, have, SLOT_PART[slot], value, reference))
    for (rid, index, slot), (have, value, reference, why) in sorted(kept.items()):
        notes.append('(c) %s set %d %s: keeps %s, %s %d against %d; %s'
                     % (rid, index, slot, have or '(empty)', SLOT_PART[slot], value, reference, why))
    out = []
    for (rid, index, slot), old in sorted(origin.items()):
        new = cur[rid][index].get(slot)
        if new != old:
            out.append(_change(rid, templates[rid]['file'], index, slot, old, new, rules[(rid, index, slot)]))
    return out


def _change(rid, path, index, slot, old, new, rule):
    return {'roster': rid, 'template': rid, 'file': path, 'set': index, 'slot': slot, 'old': old,
            'new': new, 'rule': rule}


def plan_fallback_sync(rosters, templates, sources):
    """Changes that make each fallback or ruler roster's armour slots equal its source template _a's
    first set. `rosters` is {roster id: {'file', 'culture', 'sets'}}, `templates` {roster id: [sets]},
    `sources` {roster culture: template family}."""
    changes = []
    for rid in sorted(rosters):
        m = FALLBACK_ROSTER_RE.match(rid)
        fam = sources.get(m.group('culture')) if m else None
        src = templates.get('%s_bat_template_medium_a' % fam) if fam else None
        if not src:
            continue
        for index, st in enumerate(rosters[rid]['sets']):
            for slot in ARMOUR_SLOTS:
                if st.get(slot) != src[0].get(slot):
                    changes.append(_change(rid, rosters[rid]['file'], index, slot, st.get(slot), src[0].get(slot),
                                           'mirror %s_bat_template_medium_a' % fam))
    return changes


def simulate(templates, changes):
    """{roster id: [sets]} with the changes applied (a new None removes the slot)."""
    out = {rid: [dict(s) for s in sets] for rid, sets in templates.items()}
    for c in changes:
        st = out[c['roster']][c['set']]
        if c['new'] is None:
            st.pop(c['slot'], None)
        else:
            st[c['slot']] = c['new']
    return out


# =============================================================================
# Reading and writing rosters
# =============================================================================

def _battle_sets(roster):
    """The battle sets of an <EquipmentRoster> element in file order, as derive_armor_tiers reads them."""
    return [dat._equipment_slots(st) for st in roster.findall('EquipmentSet')
            if not dat._is_civilian_roster(st) and dat._equipment_slots(st)]


def _load_rosters(paths, wanted):
    out = {}
    for path in paths:
        try:
            root = ET.parse(str(path)).getroot()
        except (ET.ParseError, OSError):
            continue
        for roster in root.iter('EquipmentRoster'):
            rid = roster.get('id') or ''
            if wanted.match(rid) and not dat._is_civilian_roster(roster):
                out[rid] = {'file': str(path), 'culture': _culture(roster.get('culture')),
                            'sets': _battle_sets(roster)}
    return out


def load_generic_templates(paths):
    """{roster id: {'file', 'culture', 'sets'}} over the generic lord templates in these files."""
    return _load_rosters(paths, GENERIC_TEMPLATE_RE)


def load_fallback_rosters(path):
    """{roster id: {'file', 'culture', 'sets'}} over taom_<culture>_(lord|ruler)_battle_<sex>."""
    return _load_rosters([path], FALLBACK_ROSTER_RE)


_ROSTER_RE = re.compile(r'(<EquipmentRoster\b[^>]*?\bid="([^"]+)"[^>]*>)(.*?)(</EquipmentRoster>)', re.S)
_SET_RE = re.compile(r'<EquipmentSet\b((?:[^>/]|/(?!>))*)>(.*?)</EquipmentSet>', re.S)
_EQ_RE = re.compile(r'<([Ee]quipment)\b[^>]*?/>')


def _slot_pat(slot, iid=None):
    tail = r'Item\.%s"' % re.escape(iid) if iid else r'[^"]*"'
    return r'<[Ee]quipment\b[^>]*?\bslot="%s"[^>]*?\bid="%s[^>]*?/>' % (slot, tail)


def _rewrite_roster(body, edits, rid):
    """Apply [(set index, slot, old, new)] to one roster body: replace, delete (new None), then
    append (old None) after the set's last equipment element, cloning its tag and indentation."""
    by_set = defaultdict(list)
    for index, slot, old, new in edits:
        by_set[index].append((slot, old, new))
    counter, done = [-1], []

    def fix_set(m):
        attrs, inner = m.group(1), m.group(2)
        if 'civilian="true"' in attrs or 'equipmentType="Civilian"' in attrs or not _EQ_RE.search(inner):
            return m.group(0)
        counter[0] += 1
        edits_here = by_set.get(counter[0], ())
        for slot, old, new in sorted(edits_here, key=lambda e: e[1] is None):
            if old is not None:
                pat = re.compile(r'(\n[ \t]*)?' + _slot_pat(slot, old))
                if new is None:
                    inner, n = pat.subn('', inner, count=1)
                else:
                    inner, n = pat.subn(lambda mm: mm.group(0).replace('Item.%s"' % old, 'Item.%s"' % new), inner, count=1)
                if n != 1:
                    raise RuntimeError('%s set %d: slot %s does not hold %s, so nothing was written'
                                       % (rid, counter[0], slot, old))
            else:
                if re.search(_slot_pat(slot), inner):
                    raise RuntimeError('%s set %d: slot %s is already filled, so nothing was written'
                                       % (rid, counter[0], slot))
                last = None
                for em in _EQ_RE.finditer(inner):
                    last = em
                indent_m = re.search(r'\n([ \t]*)$', inner[:last.start()])
                indent = indent_m.group(1) if indent_m else ''
                elem = '\n%s<%s slot="%s" id="Item.%s" />' % (indent, last.group(1), slot, new)
                inner = inner[:last.end()] + elem + inner[last.end():]
            done.append((counter[0], slot))
        return m.group(0)[:m.start(2) - m.start(0)] + inner + m.group(0)[m.end(2) - m.start(0):]

    out = _SET_RE.sub(fix_set, body)
    missing = [(i, s) for i, s, _o, _n in edits if (i, s) not in done]
    if missing:
        raise RuntimeError('%s: no battle set %s, so nothing was written' % (rid, missing))
    return out


def write_changes(changes):
    """Write the planned slot changes. Every file is rewritten, parsed and each planned slot re-read
    through ElementTree (the reader the plan came from) before any file is written; the BOM and the
    newline style each file arrived with are kept. Returns the number of files written."""
    by_file = defaultdict(lambda: defaultdict(list))
    for c in changes:
        by_file[c['file']][c['roster']].append((c['set'], c['slot'], c['old'], c['new']))
    pending = []
    for fp, per_roster in by_file.items():
        with open(fp, 'rb') as fh:
            raw = fh.read()
        bom = raw.startswith(codecs.BOM_UTF8)
        text = raw.decode('utf-8-sig' if bom else 'utf-8')
        newline = '\r\n' if '\r\n' in text else '\n'
        text = text.replace('\r\n', '\n')
        seen = set()

        def fix_roster(m):
            rid = m.group(2)
            if rid not in per_roster:
                return m.group(0)
            seen.add(rid)
            return m.group(1) + _rewrite_roster(m.group(3), per_roster[rid], rid) + m.group(4)
        new_text = _ROSTER_RE.sub(fix_roster, text)
        if set(per_roster) - seen:
            raise RuntimeError('%s holds no roster %s, so nothing was written'
                               % (os.path.basename(fp), sorted(set(per_roster) - seen)))
        try:
            root = ET.fromstring(new_text.encode('utf-8'))
        except ET.ParseError as exc:
            raise RuntimeError('%s would no longer be well-formed XML, so nothing was written: %s'
                               % (os.path.basename(fp), exc))
        read = {r.get('id'): _battle_sets(r) for r in root.iter('EquipmentRoster') if r.get('id') in per_roster}
        for rid, edits in per_roster.items():
            for index, slot, _old, new in edits:
                sets = read.get(rid) or []
                got = sets[index].get(slot) if index < len(sets) else '(no set)'
                if got != new:
                    raise RuntimeError('%s set %d %s would read %s, not %s, so nothing was written'
                                       % (rid, index, slot, got, new))
        out = new_text.replace('\n', newline).encode('utf-8')
        pending.append((fp, codecs.BOM_UTF8 + out if bom else out))
    for fp, out in pending:
        with open(fp, 'wb') as fh:
            fh.write(out)
    return len(pending)


# =============================================================================
# The command line
# =============================================================================

def load_troop_pool(moduledata):
    """{troop id: {'file', 'level', 'sets', 'culture', 'race'}} over troops/troops_*.xml battle sets."""
    import fix_upgrade_armour_regressions as fu
    troops = fu.load_troops(str(moduledata))
    for path in sorted((Path(moduledata) / 'troops').glob('troops_*.xml')):
        try:
            root = ET.parse(str(path)).getroot()
        except ET.ParseError:
            continue
        for npc in root.iter('NPCCharacter'):
            rec = troops.get(npc.get('id', ''))
            if rec is not None:
                rec['culture'] = _culture(npc.get('culture'))
                rec['race'] = npc.get('race')
                if not rec.get('has_level', True):
                    rec['level'] = None
    return troops


def promoted_clones(source_ids):
    """({source template id: [clone ids]}, {generated equipment-set file names}) from
    promote_borrowed_cultures.TARGETS: a target whose standalone files include its equipment sets
    clones the source culture's lord templates that match its rosters_kept."""
    import promote_borrowed_cultures as pbc
    clones, generated = defaultdict(list), set()
    for target, cfg in pbc.TARGETS.items():
        rel = 'equipmentsets/taom_equipment_sets_{c}.xml'
        if rel not in (cfg.get('standalone') or ()):
            continue
        generated.add(rel.format(c=target).split('/')[-1])
        src = cfg['src']
        for rid in source_ids:
            if rid.startswith(src + '_'):
                clone = target + rid[len(src):]
                if re.match(cfg.get('rosters_kept') or r'.*', clone):
                    clones[rid].append(clone)
    return dict(clones), generated


def clone_sets(clone_id, sets):
    """A promoter clone's sets from its source's: the target's item map applied when its template file is
    item-mapped (Arthedain and Rhudaur wear Arnor pieces), else the source's pieces as they are."""
    import promote_borrowed_cultures as pbc
    cfg = pbc.TARGETS.get(clone_id.split('_bat_template_')[0], {})
    if 'equipmentsets/taom_equipment_sets_{c}.xml' not in (cfg.get('item_map_files') or ()):
        return sets
    m = cfg.get('item_map') or {}
    return [{slot: m.get(iid, iid) for slot, iid in st.items()} for st in sets]


def planned_items(items):
    """(items with the values the armour restat would write, the restat's changed rows): its dry run
    (roster-first, weights and material kept) over a map derived in memory from this checkout."""
    recs, _ = dat.derive()
    out = {k: dict(v, stats=dict(v.get('stats', {}))) for k, v in items.items()}
    changed = []
    for folder in sorted(os.listdir(ra.ARMORY_DIR)):
        fpath = os.path.join(ra.ARMORY_DIR, folder)
        if not os.path.isdir(fpath):
            continue
        for fn, slot in ra.SLOT_TYPES.items():
            fp = os.path.join(fpath, fn)
            if not os.path.exists(fp):
                continue
            for c in ra.process_file(fp, slot, dry_run=True, tier_source='roster-first', roster_map=recs,
                                     keep_weights=True, keep_material_type=True):
                if c['status'] == 'CHANGED':
                    changed.append(dict(c, folder=folder))
                    if c['id'] in out:
                        out[c['id']]['stats'].update(c['new_values'])
    return out, changed


def fallback_lords(rosters, lords):
    """The fallback and ruler rosters as lord records, judged at the most common race of the lords
    of their culture (the race the spawned lords and new rulers of that culture mostly are)."""
    races = defaultdict(Counter)
    for l in lords:
        races[l['culture']][l['race']] += 1
    out = []
    for rid, r in sorted(rosters.items()):
        race = races[r['culture']].most_common(1)[0][0] if races[r['culture']] else None
        out.append({'id': rid, 'culture': r['culture'], 'race': race, 'file': r['file'], 'line': 0,
                    'templates': [rid], 'inline_sets': [], 'overrides': {}})
    return out


def _table(rows, lords, title):
    print('\n%s' % title)
    print('  %-18s %-5s %5s %6s %6s %6s %5s' % ('culture', 'part', 'lords', 'median', 'under', 'u>10%', 'over'))
    by_c = defaultdict(list)
    for r in rows:
        by_c[r['culture']].append(r)
    judged = {r['lord'] for r in rows}
    unjudged = Counter(l['culture'] for l in lords if l['id'] not in judged)
    tot = Counter()
    for culture in sorted(by_c, key=str):
        for p in PARTS:
            rs = by_c[culture]
            under = sum(1 for r in rs if p in r['under'])
            far = sum(1 for r in rs if p in r['short'])
            over = sum(1 for r in rs if p in r['over'])
            tot.update({(p, 'under'): under, (p, 'far'): far, (p, 'over'): over})
            print('  %-18s %-5s %5d %6g %6d %6d %5d' % (culture, p, len(rs), statistics.median(r['worst'][p] for r in rs),
                                                          under, far, over))
    print('  not judged (no elite troop of their own or of their templates\' culture): %s'
          % dict(sorted(unjudged.items(), key=str)))
    borrowed = Counter((r['culture'], r['judged_as']) for r in rows if r['judged_as'] != r['culture'])
    if borrowed:
        print('  judged against the culture of their templates: %s' % ', '.join(
            '%s as %s (%d)' % (c, j, n) for (c, j), n in sorted(borrowed.items())))
    print('  all judged lords: %d; ' % len(rows) + ', '.join(
        '%s under %d (short, more than 10%%: %d), over %d' % (p, tot[(p, 'under')], tot[(p, 'far')], tot[(p, 'over')])
        for p in PARTS))


def _fmt(items, iid):
    if not iid:
        return '(empty)'
    s = items.get(iid, {}).get('stats', {})
    return '%s %d/%d/%d/%d' % (iid, s.get('head_armor', 0), s.get('body_armor', 0), s.get('arm_armor', 0),
                               s.get('leg_armor', 0))


def _pool_md(pools, items, users_by_piece, path, values='live', low_rows=()):
    source = ('the restat dry run (planned values)' if values == 'planned' else 'the live Armory (live values)')
    lines = ['# Lord armour pool per culture', '',
             'Written on %s by `python tools/fix_lord_kits.py --pool-md <path> --values %s` (lord kit, #1271). '
             'Armour is head/body/arm/leg from %s when it ran; "family" is the look family the assignment '
             'groups variants by; "lowest troop" is the lowest reference troop that wears the piece in a '
             'battle set (a troop the reference leaves out, such as a Harad mumak rider, is not counted); '
             '"templates" are the generic lord templates that wear it, the promoter clones left out (each '
             'clone wears the pieces of its source template, or those its promoter item map sends them to).'
             % (datetime.date.today().isoformat(), values, source), '']
    for culture in sorted(pools, key=str):
        pool = pools[culture]
        fams = {family_key(i) for ids in pool['slots'].values() for i in ids}
        lines += ['## %s' % culture, '',
                  'Folders: %s. Families: %d. Pieces per slot: %s.' % (
                      ', '.join(pool['folders']) or 'none', len(fams),
                      ', '.join('%s %d' % (s, len(pool['slots'].get(s, ()))) for s in ARMOUR_SLOTS)), '']
        if not fams:
            continue
        lines += ['| Slot | Family | Piece | Armour | Lowest troop | Templates |', '|---|---|---|---|---|---|']
        for slot in ARMOUR_SLOTS:
            for iid in sorted(pool['slots'].get(slot, ()), key=lambda i: (family_key(i), i)):
                low = pool['lowest'].get(iid)
                lines.append('| %s | %s | `%s` | %s | %s | %s |' % (
                    slot, '_'.join(family_key(iid)), iid, _fmt(items, iid).split(' ', 1)[1],
                    '%s L%d' % (low[1], low[0]) if low else 'none',
                    ', '.join(sorted(users_by_piece.get(iid, ()))) or '-'))
        lines.append('')
        low = sorted({(r[4], r[1], r[2]) for r in low_rows if r[0] == culture})
        if low:
            lines += ['Pool pieces a troop under level %d wears, so the restat prices them at that troop\'s band '
                      '(no mesh ladder substitute: the ids carry no tier token):' % LORD_LEVEL, '',
                      '| Piece | Troop | Level |', '|---|---|---:|']
            lines += ['| `%s` | %s | %d |' % row for row in low]
            lines.append('')
    with open(path, 'w', encoding='utf-8', newline='\r\n') as fh:
        fh.write('\n'.join(lines) + '\n')


def main(argv=None):
    import fix_armour_mesh_ladder as fam
    import fix_upgrade_armour_regressions as fu
    import rebalance_troops as rb
    import taom_schema as ts
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--apply', action='store_true', help='write the lord templates and fallback rosters')
    ap.add_argument('--check', action='store_true', help='exit 1 when a change is planned')
    ap.add_argument('--values', choices=('live', 'planned'), default='live',
                    help='judge on the live Armory or on the values the armour restat would write')
    ap.add_argument('--pool-md', help='write the lord armour pool as markdown to this path')
    ap.add_argument('--game-modules', default=rb.DEFAULT_GAME_MODULES,
                    help='.../Mount & Blade II Bannerlord/Modules (item stats come from the install)')
    args = ap.parse_args(argv)
    if not (Path(args.game_modules) / 'LOTRLOME_Armory' / 'ModuleData').is_dir():
        print('SKIPPED: no LOTRLOME_Armory under %s; nothing was checked.' % args.game_modules)
        return 2
    items = fu.load_item_armour(args.game_modules)
    if not any(rec.get('folder') for rec in items.values()):
        print('SKIPPED: the Armory under %s holds no armour; nothing was checked.' % args.game_modules)
        return 2
    md = Path(dat.MODULEDATA_DIR)
    exempt = set(ts.Validator._ARMOUR_LADDER_EXEMPT) | set(ts.Validator._BODYLESS_BY_DESIGN)
    all_troops = load_troop_pool(md)
    troops = reference_troops(all_troops, exempt)
    failures = []
    lords = load_lords(md, failures)
    if not troops or not lords:
        print('SKIPPED: %d reference troops and %d lords read; nothing was checked.' % (len(troops), len(lords)))
        return 2
    for path, err in failures:
        print('WARNING: %s did not parse and its lords were not read: %s' % (path, err))
    # Every swap must hold on the live values and on the values the restat would write (an unworn
    # keyword piece is priced by its keyword), so both sets are read on every run.
    live = items
    planned_vals, restat = planned_items(live)
    items, other = (planned_vals, live) if args.values == 'planned' else (live, planned_vals)
    all_templates = dat.load_battle_templates()
    paths = sorted((md / 'equipmentsets').glob('taom_equipment_sets_*.xml'))
    clones, generated = promoted_clones([rid for rid in all_templates if GENERIC_TEMPLATE_RE.match(rid)])
    generic = load_generic_templates([p for p in paths if p.name not in generated])
    lt_path = md / 'equipmentsets' / 'taom_lord_template_equipment.xml'
    rosters = load_fallback_rosters(lt_path)
    template_cultures = {rid: t['culture'] for rid, t in load_generic_templates(paths).items()}
    print('Items: %s (%s values). Reference troops: %d. Lords: %d. Generic lord templates: %d (%d more are '
          'promoter clones). Fallback and ruler rosters: %d.' % (
              format(len(items), ','), args.values, len(troops), len(lords), len(generic),
              sum(len(v) for v in clones.values()), len(rosters)))
    if restat and args.values == 'planned':
        # Only the armour values reach the judgement; a loot table or weight change is counted, not listed.
        armour = [c for c in restat if c['old_values'] != c['new_values']]
        print('\nThe armour restat dry run would change %d item(s), %d of them in armour value:'
              % (len(restat), len(armour)))
        for c in armour:
            print('  %-14s %-44s %-6s %s -> %s' % (c['folder'], c['id'], c['tier'], c['old_values'], c['new_values']))

    pools, users_by_piece = {}, defaultdict(set)
    lowest = lowest_troop_wearers(troops)
    for rid, t in generic.items():
        for st in t['sets']:
            for slot in ARMOUR_SLOTS:
                if st.get(slot):
                    users_by_piece[st[slot]].add(rid)
    for culture in sorted({t['culture'] for t in generic.values()}, key=str):
        rids = [r for r in generic if generic[r]['culture'] == culture]
        cl = [l for l in lords if l['culture'] == culture]
        pools[culture] = lord_pool(culture, items, troops, [(l['race'], st) for l in cl for st in own_sets(l, all_templates)],
                                   [st for r in rids for st in generic[r]['sets']], lowest)
    print('\nLord armour pool (pieces per slot H/B/C/G/L, families):')
    for culture, pool in sorted(pools.items(), key=lambda kv: str(kv[0])):
        fams = {family_key(i) for ids in pool['slots'].values() for i in ids}
        print('  %-18s %-24s %s  %d families' % (culture, ','.join(pool['folders']), '/'.join(
            str(len(pool['slots'].get(s, ()))) for s in ARMOUR_SLOTS), len(fams)))

    index = fam.build_line_index(items)
    anchors = fam.line_anchors(fam.in_scope(all_troops))
    low_rows = []
    for culture, pool in pools.items():
        members = {i for ids in pool['slots'].values() for i in ids}
        for tid, t in sorted(troops.items()):
            if t.get('level') is None or int(t['level']) >= LORD_LEVEL:
                continue
            for st in t['sets']:
                for slot in ARMOUR_SLOTS:
                    if st.get(slot) in members:
                        pick = fam.pick_substitute(st[slot], int(t['level']), items, index, anchors,
                                                   tid in fam.NOBLE_TROOPS)
                        if pick and pick[0] == st[slot]:
                            continue   # the ladder allows this tier at this level (a noble troop)
                        low_rows.append((culture, tid, int(t['level']), slot, st[slot], pick[0] if pick else None))
    low_rows = sorted(set(low_rows))
    print('\nLord pool pieces a troop under level %d wears: %d pair(s); with a mesh ladder substitute: %d' % (
        LORD_LEVEL, len(low_rows), sum(1 for r in low_rows if r[5])))
    for culture, tid, level, slot, iid, sub in low_rows:
        print('  %-18s %-44s L%-3d %-6s %-44s -> %s' % (culture, tid, level, slot, iid, sub or '(no substitute)'))
    if args.pool_md:
        _pool_md(pools, items, users_by_piece, args.pool_md, values=args.values, low_rows=low_rows)
        print('  written: %s' % args.pool_md)

    before = judge_lords(lords + fallback_lords(rosters, lords), all_templates, troops, items, template_cultures)
    _table(before, lords, 'Lords and fallback rosters, worst whole set per part (%s values):' % args.values)

    changes, notes = plan_assignments(generic, lords, troops, items, clones, elite_wearers=all_troops,
                                      planned=other)
    sim = simulate(all_templates, changes)
    for c in changes:
        for cid in clones.get(c['roster'], ()):
            sim[cid] = clone_sets(cid, sim[c['roster']])
    fb_sources = dict(FALLBACK_SOURCES)
    fallback = plan_fallback_sync({r: v for r, v in rosters.items()
                                   if FALLBACK_ROSTER_RE.match(r).group('culture') in fb_sources}, sim, fb_sources)
    sim = simulate(sim, fallback)
    print('\nPlanned template changes: %d slot(s) in %d template(s)' % (len(changes), len({c['roster'] for c in changes})))
    for c in changes:
        print('  %-36s set %d %-6s %-52s -> %-52s %s' % (c['roster'], c['set'], c['slot'], _fmt(items, c['old']),
                                                        _fmt(items, c['new']), c['rule']))
    for n in notes:
        print('  NOTE %s' % n)
    print('\nDistinct pieces per template culture, Body/Head/Gloves/Leg (now -> after the plan):')
    for culture in sorted(pools, key=str):
        rids = sorted(r for r in generic if generic[r]['culture'] == culture)

        def distinct(src):
            return '/'.join(str(len({st.get(sl) for r in rids for st in src.get(r, ())} - {None}))
                            for sl in ('Body', 'Head', 'Gloves', 'Leg'))
        print('  %-18s %d templates: %s -> %s' % (culture, len(rids), distinct(all_templates), distinct(sim)))
    civ = []
    for rid in sorted({c['roster'] for c in changes if c['slot'] == 'Body'}):
        civ_id = rid.replace('_bat_template_medium_', '_civ_template_default_')
        civ_sets = [st for st in _civilian_sets(generic[rid]['file'], civ_id)]
        new_body = sim[rid][0].get('Body')
        if civ_sets and civ_sets[0].get('Body') and family_key(civ_sets[0]['Body']) != family_key(new_body):
            civ.append((rid, new_body, civ_id, civ_sets[0]['Body']))
    print('\nTemplates whose battle and town kit now differ in look (civilian sets are not touched): %d' % len(civ))
    for rid, nb, cid, cb in civ:
        print('  %-36s battle %-40s town %s %s' % (rid, nb, cid, cb))
    worn_with = cape_pairs(all_troops, lords)

    def broken_pair(st):
        return st.get('Body') and st.get('Cape') and not same_look(st['Body'], st['Cape'],
                                                                     worn_with.get(st['Body'], ()))
    broken = [(rid, i, st['Body'], st['Cape'], broken_pair(all_templates[rid][i]))
              for rid in sorted(generic) for i, st in enumerate(sim.get(rid, ())) if broken_pair(st)]
    print('\nTemplates whose Body and Cape are of different families after the plan: %d (%d of them before this run too)'
          % (len(broken), sum(1 for b in broken if b[4])))
    for rid, i, b, c, before in broken:
        print('  %-36s set %d Body %-40s Cape %-40s%s' % (rid, i, b, c, ' (before this run too)' if before else ''))
    print('\nTemplates sharing a Body after the plan, and the other slots two of them still share (Head/Cape/Gloves/Leg):')
    for culture in sorted(pools, key=str):
        groups = defaultdict(list)
        for rid in sorted(r for r in generic if generic[r]['culture'] == culture):
            for i, st in enumerate(sim.get(rid, ())):
                if st.get('Body'):
                    groups[st['Body']].append((rid, i, st))
        for body, members in sorted(groups.items()):
            if len(members) < 2:
                continue
            # A slot is listed when two of the templates wear one piece in it.
            same = [sl for sl in ('Head', 'Cape', 'Gloves', 'Leg')
                    if len([st.get(sl) for _r, _i, st in members]) > len({st.get(sl) for _r, _i, st in members})]
            at_head = all(all_templates[r][i].get('Body') == body for r, i, _st in members)
            print('  %-18s %-40s %s (%s); two or more share: %s' % (
                culture, body, ', '.join(r.split('_bat_template_medium_')[1] for r, _i, _st in members),
                'shared before this run' if at_head else 'by this plan', ', '.join(same) or 'nothing'))
    print('\nFallback and ruler roster changes: %d slot(s) in %d roster(s)' % (len(fallback), len({c['roster'] for c in fallback})))
    for c in fallback:
        print('  %-40s %-6s %-44s -> %s' % (c['roster'], c['slot'], c['old'] or '(empty)', c['new'] or '(removed)'))
    stale = plan_fallback_sync({r: v for r, v in rosters.items()
                                if FALLBACK_ROSTER_RE.match(r).group('culture') in PROMOTED_FALLBACKS},
                               sim, PROMOTED_FALLBACKS)
    print('Promoted cultures\' rosters that differ from their own _a (the promoter regenerates them): %d slot(s) in %s'
          % (len(stale), sorted({c['roster'].split('_')[1] for c in stale})))

    after = judge_lords(lords + fallback_lords(rosters, lords), sim, troops, items, template_cultures)
    _table(after, lords, 'After the plan (%s values; a clone shown with its source pieces):' % args.values)
    left = [r for r in after if r['short'] or r['over']]
    by_t = Counter(next(iter(l['templates']), '(inline)') for l in lords + fallback_lords(rosters, lords)
                   if l['id'] in {r['lord'] for r in left})
    print('\nLords and rosters short (more than 10%% under) or over on some part after the plan: %d. By battle '
          'template:' % len(left))
    for rid, n in sorted(by_t.items(), key=lambda kv: (-kv[1], kv[0])):
        print('  %-44s %d' % (rid, n))

    planned = changes + fallback
    if args.check:
        return 1 if planned else 0
    if not args.apply:
        print('\n(dry run; pass --apply to write)')
        return 0
    if planned:
        print('\nWrote %d file(s).' % write_changes(planned))
    generic_after = load_generic_templates([p for p in paths if p.name not in generated])
    again, _ = plan_assignments(generic_after, lords, troops, items, clones, elite_wearers=all_troops,
                                planned=other)
    rosters_after = load_fallback_rosters(lt_path)
    again += plan_fallback_sync({r: v for r, v in rosters_after.items()
                                 if FALLBACK_ROSTER_RE.match(r).group('culture') in fb_sources},
                                dat.load_battle_templates(), fb_sources)
    if again:
        print('ERROR: %d change(s) still planned after the write.' % len(again))
        return 1
    print('Re-plan from disk: nothing left to write.')
    print('  then: python tools/promote_borrowed_cultures.py --only <culture> --check for every promoter target, '
          'and --apply where it reports drift')
    print('  then: python tools/derive_armor_tiers.py and python tools/fix_lord_kits.py --values planned')
    return 0


def _civilian_sets(path, roster_id):
    try:
        root = ET.parse(str(path)).getroot()
    except (ET.ParseError, OSError):
        return []
    for roster in root.iter('EquipmentRoster'):
        if roster.get('id') == roster_id:
            return [dat._equipment_slots(st) for st in roster.findall('EquipmentSet') if dat._equipment_slots(st)]
    return []


def entry(argv=None):
    """main() with a crash kept apart from 'a change is planned': a traceback exits 3."""
    try:
        return main(argv)
    except Exception:
        traceback.print_exc()
        return 3


if __name__ == '__main__':
    sys.exit(entry())
