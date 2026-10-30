#!/usr/bin/env python3
"""Write the owners and cultures pinned in tools/settlement_assignments.json into the LIVE map.

The LIVE `<game>/Modules/TAOM_Map/ModuleData/settlements.xml` is unversioned, so a reinstall, or a
Modding Kit save from a copy loaded before an edit, puts an old owner or culture back.
`validate_moduledata.py` reports that as SETTLEMENT_ASSIGNMENT_DRIFT; this tool repairs it. The spec
is the single source: this tool writes from it and the validator reads it.

For each pinned town or castle it sets `owner="Faction.<owner>"` and `culture="Culture.<culture>"`
on the opening tag, and the same culture on every village whose `<Village bound=...>` names it
(villages carry no owner; they follow the fortification). Nothing else in the file changes: bytes
are read and written as-is (BOM and CRLF kept, tools/README.md "XML I/O convention"), the result
must parse before it is written, and a backup without an `.xml` extension is taken first.

Usage:
    python tools/apply_settlement_assignments.py            # dry run: list what would change
    python tools/apply_settlement_assignments.py --apply    # write (after a backup)
"""
import argparse
import json
import re
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

from _gamedir import game_dir

GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
DEFAULT_SETTLEMENTS = Path(GAME) / "Modules" / "TAOM_Map" / "ModuleData" / "settlements.xml"
DEFAULT_SPEC = Path(__file__).resolve().parent / "settlement_assignments.json"


class AssignmentError(Exception):
    """The map does not match the spec's assumptions; nothing may be written."""


def _parse(text: str) -> ET.Element:
    return ET.fromstring(text.lstrip("\ufeff").encode("utf-8"))


def _set_attr(tag: str, attr: str, value: str, sid: str) -> str:
    pattern = re.compile(r'(\s%s=")[^"]*(")' % attr)
    if len(pattern.findall(tag)) != 1:
        raise AssignmentError(f"{sid}: opening tag has no single {attr} attribute")
    return pattern.sub(lambda m: m.group(1) + value + m.group(2), tag)


def _edit_tag(text: str, sid: str, attrs: dict) -> str:
    # The closing quote after the id keeps castle_K4 from matching castle_K40.
    tags = list(re.finditer(r'<Settlement\s+id="%s"[^>]*>' % re.escape(sid), text))
    if len(tags) != 1:
        raise AssignmentError(f"{sid}: {len(tags)} opening tags in the map, expected 1")
    m = tags[0]
    tag = m.group(0)
    for attr, value in attrs.items():
        tag = _set_attr(tag, attr, value, sid)
    return text[:m.start()] + tag + text[m.end():]


def plan(text: str, pins: dict) -> tuple:
    """Return (new_text, changed_ids). Raises AssignmentError when a pin cannot be applied."""
    root = _parse(text)
    fortifications, villages_of = set(), {}
    for s in root.iter("Settlement"):
        if s.find(".//Town") is not None:
            fortifications.add(s.get("id"))
        village = s.find(".//Village")
        if village is not None:
            bound = (village.get("bound") or "").replace("Settlement.", "", 1)
            villages_of.setdefault(bound, []).append(s.get("id"))

    new_text, changed = text, []
    for sid, pin in sorted(pins.items()):
        if sid not in fortifications:
            raise AssignmentError(f"{sid}: not a town or castle in the map")
        targets = [(sid, {"owner": f"Faction.{pin['owner']}", "culture": f"Culture.{pin['culture']}"})]
        targets += [(vid, {"culture": f"Culture.{pin['culture']}"}) for vid in villages_of.get(sid, [])]
        for tid, attrs in targets:
            edited = _edit_tag(new_text, tid, attrs)
            if edited != new_text:
                changed.append(tid)
            new_text = edited
    _parse(new_text)  # refuse to hand back a document that no longer parses
    return new_text, changed


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--settlements", default=str(DEFAULT_SETTLEMENTS),
                    help="the LIVE TAOM_Map settlements.xml")
    ap.add_argument("--spec", default=str(DEFAULT_SPEC))
    ap.add_argument("--apply", action="store_true", help="write the file (default is a dry run)")
    args = ap.parse_args(argv)

    path = Path(args.settlements)
    if not path.exists():
        print(f"FAIL: settlements file not found: {path}")
        return 1
    pins = json.loads(Path(args.spec).read_text(encoding="utf-8-sig")).get("settlements") or {}
    if not pins:
        print(f"FAIL: {args.spec} pins nothing")
        return 1

    raw = path.read_bytes()
    text = raw.decode("utf-8")
    try:
        new_text, changed = plan(text, pins)
    except AssignmentError as exc:
        print(f"FAIL: {exc}. Nothing written.")
        return 1

    print(f"file: {path}")
    print(f"pinned: {len(pins)}   settlements to change: {len(changed)}")
    for sid in changed:
        print(f"  {sid}")
    if not changed:
        print("nothing to do (the map matches the spec)")
        return 0
    if not args.apply:
        print("dry run; re-run with --apply to write")
        return 0
    backup = path.with_name(path.name + time.strftime(".bak_assignments_%Y%m%d-%H%M%S"))
    backup.write_bytes(raw)
    path.write_bytes(new_text.encode("utf-8"))
    print(f"backup: {backup}")
    print(f"WROTE: {len(changed)} settlements")
    return 0


if __name__ == "__main__":
    sys.exit(main())
