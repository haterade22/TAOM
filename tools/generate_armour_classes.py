#!/usr/bin/env python3
"""Generate the armour acquisition class table (docs/features/armour-acquisition.md).

WHY THIS EXISTS
KEYforce's rule: markets and loot hand out light and medium armour freely. Heavy, elite and lord
pieces are sold only by a town whose armoury (its Barracks level) allows them, lord pieces can also
be forged or earned, and named hero kit never changes hands. The game needs one class per piece to apply that,
and none of TAOM's existing classifications can serve alone: 26% of Armory armour carries no
tier token in its id (all of Dale, Harad and Mirkwood, nearly all of Rohan and Rivendell), the
roster-derived map in tools/data is a tools artefact, and the engine's own ItemCategory is an
absolute tier that ignores the per-kingdom caps (#583).

WHAT IT WRITES
Main/_Module/ModuleData/armour_acquisition/armour_classes.xml, one row per character armour
piece in the LIVE LOTRLOME_Armory (the five slot files of every culture folder, the same set
derive_armor_tiers.py tiers):

    <Item id="sk_gd_fount_chest_med_a" class="medium" next="sk_gd_fount_chest_heavy_a" />

Class precedence:
  1. named     hero, boss or fixed kit (rebalance_armor.is_excluded): never sold, looted, upgraded
  2. lord      an id carrying the `_lord` token, whoever wears it (the artist's lord variant)
  3. civilian  `_civ` kit, off the combat ladder
  4. derive_armor_tiers' tier: the lowest battle wearer's band, else the id token
  5. the band whose kingdom-cap target is nearest the piece's own primary stat
  6. light, when nothing classes it (fail open: the piece stays obtainable)

`next` is the piece the armoury upgrades it into: the nearest class above it that has a
candidate, looked for first in the piece's own tokened line (same stem and slot file, the same
suffix preferred) and then among same-kit, same-slot pieces sharing the longest id prefix. A kit
is rebalance_armor.kingdom_key's kingdom, or the Armory folder for a culture with no kingdom cap:
several kits share one folder at different caps (mordor: orc/uruk/Black Numenorean; rhun: Dol
Guldur's Khamul line), so the fallback must not link across them. Lord is a target only from an
elite piece, and named or civilian kit is never one.

The table is sorted and carries no timestamp, so an unchanged Armory regenerates byte-identically
and the drift check compares text.

WIRING
    python tools/generate_armour_classes.py            # dry run: summary, nothing written
    python tools/generate_armour_classes.py --apply    # write the table
    python tools/generate_armour_classes.py --check    # exit 1 when the committed table is stale
    python tools/validate_moduledata.py                # emits ARMOUR_CLASS_TABLE_DRIFT (warning)

Without the live Armory the check is SKIPPED and exits 2, never faked as a pass. Re-run with
--apply after every Armory art drop (/armory-audit) and after any troop roster change that moves
an item's lowest wearer across a band.
"""
from __future__ import annotations

import argparse
import functools
import os
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from typing import Callable, Dict, Iterable, Optional, Tuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fix_armour_mesh_ladder as fml  # noqa: E402
import rebalance_armor as ra  # noqa: E402

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
TABLE_PATH = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'armour_acquisition', 'armour_classes.xml')

CLASS_ORDER = ('light', 'medium', 'heavy', 'elite', 'lord')
UPGRADABLE = ('light', 'medium', 'heavy', 'elite')
BANDS = ('light', 'medium', 'heavy', 'elite')

Row = Tuple[str, Optional[str]]


@dataclass(frozen=True)
class Piece:
    item_id: str
    cls: str
    folder: str
    slot: str


def classify(item_id: str, name: str, derived_tier: Optional[str], stat_band_tier: Optional[str]) -> str:
    """The acquisition class of one piece; the precedence is the module docstring's."""
    if ra.is_excluded(item_id, name):
        return 'named'
    token = ra.mesh_tier_of(item_id)
    if token == 'lord':
        return 'lord'
    if token == 'civilian' or derived_tier == 'civilian':
        return 'civilian'
    if derived_tier in CLASS_ORDER:
        return derived_tier
    if stat_band_tier in BANDS:
        return stat_band_tier
    return 'light'


def stat_band(primary: Optional[float], targets: Dict[str, float]) -> Optional[str]:
    """The band whose target is nearest `primary`; a tie goes to the lower band."""
    if primary is None or not targets:
        return None
    best = None
    for band in BANDS:
        if band not in targets or targets[band] is None:
            continue
        gap = abs(primary - targets[band])
        if best is None or gap < best[0]:
            best = (gap, band)
    return best[1] if best else None


def _allowed_targets(cls: str) -> Tuple[str, ...]:
    if cls not in UPGRADABLE:
        return ()
    above = CLASS_ORDER[CLASS_ORDER.index(cls) + 1:]
    return tuple(c for c in above if c != 'lord' or cls == 'elite')


def _common_prefix(a: str, b: str) -> int:
    n = 0
    for x, y in zip(a, b):
        if x != y:
            break
        n += 1
    return n


@functools.lru_cache(maxsize=None)
def _line_key(item_id: str):
    """(stem, suffix) of a tokened id, or None. The mesh-tier ladder's own split (#609), so a line
    means the same thing to the ladder fixer and to the armoury. Memoised: it is a pure function
    of the id, called about 267,000 times over the Armory's 2,860 pieces."""
    parts = fml.split_id(item_id)
    return None if parts is None else (parts[0], parts[2])


def _kit(p: Piece) -> str:
    """The kit `p` belongs to: its kingdom (rebalance_armor.kingdom_key), or its Armory folder for
    a culture with no kingdom cap. Several kits share one Armory folder at different kingdom caps
    (mordor holds orc, uruk and Black Numenorean kit; rhun holds Dol Guldur's Khamul line), so the
    untokened-line fallback below must match kits, not folders, or an upgrade link crosses into
    another kingdom's armour curve (77 of 2,107 links did, before this filter)."""
    return ra.kingdom_key(p.item_id, p.folder) or p.folder


def pick_next(source: Piece, pieces: Iterable[Piece]) -> Optional[str]:
    """The id `source` upgrades into, or None (see the module docstring for the rule)."""
    targets = _allowed_targets(source.cls)
    if not targets:
        return None
    pool = [p for p in pieces if p.item_id != source.item_id and p.slot == source.slot and p.cls in targets]
    if not pool:
        return None
    line = _line_key(source.item_id)
    source_kit = _kit(source)
    for cls in targets:
        at_class = [p for p in pool if p.cls == cls]
        if not at_class:
            continue
        if line is not None:
            stem, suffix = line
            in_line = [p for p in at_class if (_line_key(p.item_id) or (None, None))[0] == stem]
            if in_line:
                same_suffix = sorted(p.item_id for p in in_line if _line_key(p.item_id)[1] == suffix)
                return same_suffix[0] if same_suffix else sorted(p.item_id for p in in_line)[0]
        in_kit = [p for p in at_class if _kit(p) == source_kit]
        if in_kit:
            return min(in_kit, key=lambda p: (-_common_prefix(p.item_id, source.item_id), p.item_id)).item_id
    return None


TargetsFn = Callable[[str, str, str], Dict[str, float]]


def stat_targets(item_id: str, slot: str, folder: str) -> Dict[str, float]:
    """Each band's kingdom-cap primary-stat target for this piece (rebalance_armor's curve).

    A culture with no kingdom cap still gets a target from calculate_stats' SLOT_BASELINES
    fallback (CULTURAL_MODS.get defaults an unmapped culture to neutral); nothing here can raise,
    so a real failure is left to propagate rather than read as "no target".
    """
    return {band: ra._get_primary_stat(ra.calculate_stats(band, slot, folder, item_id=item_id), slot)
            for band in BANDS}


def build_rows(records: Dict[str, dict], targets: TargetsFn = stat_targets) -> Dict[str, Row]:
    """{item id: (class, next id or None)} for derive_armor_tiers-shaped records."""
    pieces = []
    for item_id, rec in records.items():
        derived = rec.get('tier')
        band = None
        if derived is None and ra.mesh_tier_of(item_id) is None:
            band = stat_band(rec.get('current'), targets(item_id, rec.get('slot'), rec.get('culture')))
        cls = classify(item_id, rec.get('name', ''), derived, band)
        pieces.append(Piece(item_id=item_id, cls=cls, folder=rec.get('culture'), slot=rec.get('slot')))
    # pick_next only ever matches within one slot; grouping first means each piece scans its own
    # slot's candidates instead of every piece in the Armory (about 2,860 for one run).
    by_slot: Dict[str, list] = {}
    for p in pieces:
        by_slot.setdefault(p.slot, []).append(p)
    return {p.item_id: (p.cls, pick_next(p, by_slot[p.slot])) for p in pieces}


HEADER = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    '<!--\n'
    '  GENERATED by tools/generate_armour_classes.py from the live LOTRLOME_Armory and the repo troop\n'
    '  rosters. Do not hand-edit: re-run the generator with apply after an Armory art drop.\n'
    '  One row per character armour piece: its acquisition class (light, medium, heavy, elite, lord,\n'
    '  civilian, named) and the piece the armoury upgrades it into. docs/features/armour-acquisition.md\n'
    '-->\n'
)


def render(rows: Dict[str, Row]) -> str:
    lines = [HEADER, '<ArmourClasses>\n']
    for item_id in sorted(rows):
        cls, nxt = rows[item_id]
        attrs = f'id="{item_id}" class="{cls}"' + (f' next="{nxt}"' if nxt else '')
        lines.append(f'  <Item {attrs} />\n')
    lines.append('</ArmourClasses>\n')
    return ''.join(lines)


def parse(text: str) -> Dict[str, Row]:
    root = ET.fromstring(text.replace('\r\n', '\n').encode('utf-8'))
    return {el.get('id'): (el.get('class'), el.get('next')) for el in root.findall('Item')}


def diff(old: Dict[str, Row], new: Dict[str, Row]) -> Dict[str, list]:
    return {
        'added': sorted(set(new) - set(old)),
        'removed': sorted(set(old) - set(new)),
        'changed': sorted(k for k in set(old) & set(new) if old[k] != new[k]),
    }


def generate() -> Optional[Dict[str, Row]]:
    """Rows for the live Armory, or None when it is not installed."""
    import derive_armor_tiers as dat
    if not os.path.isdir(ra.ARMORY_DIR):
        return None
    records, _ = dat.derive()
    if not records:
        return None
    return build_rows(records)


def read_committed(path: Optional[str] = None) -> Optional[str]:
    """The committed table's text (CRLF normalised to LF), or None when the file is missing.

    Resolves TABLE_PATH at CALL time: `path: str = TABLE_PATH` as a default argument binds once,
    at import, so a caller that patches the module-level TABLE_PATH afterwards (every test that
    exercises --apply/--check against a temp file) would silently keep reading the old path.
    Decodes with errors='replace' so a merge conflict or a stray corrupt byte in the committed
    file is text to summarise or overwrite, never a crash.
    """
    path = path or TABLE_PATH
    if not os.path.exists(path):
        return None
    with open(path, 'rb') as fh:
        return fh.read().decode('utf-8', errors='replace').replace('\r\n', '\n')


def drift_summary(committed_text: Optional[str], rows: Dict[str, Row]) -> Optional[str]:
    """None when the committed table matches `rows`, else a one-line description of the drift."""
    if committed_text is not None and committed_text == render(rows):
        return None
    if committed_text is None:
        return f'the table is missing; {len(rows)} pieces would be classed'
    try:
        d = diff(parse(committed_text), rows)
    except ET.ParseError as exc:
        return f'the committed table does not parse ({exc}); {len(rows)} pieces would be classed'
    parts = []
    for key in ('added', 'removed', 'changed'):
        if d[key]:
            shown = ', '.join(d[key][:5]) + (f', +{len(d[key]) - 5} more' if len(d[key]) > 5 else '')
            parts.append(f'{len(d[key])} {key} ({shown})')
    return '; '.join(parts) if parts else 'the text differs (formatting only)'


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    group = ap.add_mutually_exclusive_group()
    group.add_argument('--apply', action='store_true', help='write the table')
    group.add_argument('--check', action='store_true', help='exit 1 when the committed table is stale')
    args = ap.parse_args(argv)

    rows = generate()
    if rows is None:
        print(f'SKIPPED: no live LOTRLOME_Armory at {ra.ARMORY_DIR}; nothing was checked.', file=sys.stderr)
        return 2

    counts = {}
    for cls, _ in rows.values():
        counts[cls] = counts.get(cls, 0) + 1
    upgradable = sum(1 for cls, nxt in rows.values() if cls in UPGRADABLE)
    linked = sum(1 for cls, nxt in rows.values() if cls in UPGRADABLE and nxt)
    order = CLASS_ORDER + ('civilian', 'named')
    print(f'{len(rows)} pieces: ' + ', '.join(f'{c} {counts.get(c, 0)}' for c in order))
    print(f'{linked} of {upgradable} upgradable pieces have an upgrade target')

    drift = drift_summary(read_committed(), rows)
    if args.check:
        if drift is None:
            print('The committed table is current.')
            return 0
        print(f'STALE: {drift}. Re-run: python tools/generate_armour_classes.py --apply', file=sys.stderr)
        return 1
    if args.apply:
        os.makedirs(os.path.dirname(TABLE_PATH), exist_ok=True)
        text = render(rows)
        ET.fromstring(text.encode('utf-8'))  # refuse to write a document that does not parse
        with open(TABLE_PATH, 'wb') as fh:
            fh.write(text.encode('utf-8'))
        print(f'Wrote {TABLE_PATH}' + ('' if drift is None else f' ({drift})'))
        return 0
    print('Dry run: nothing written.' + ('' if drift is None else f' The committed table is stale: {drift}.'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
