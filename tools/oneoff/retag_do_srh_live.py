#!/usr/bin/env python3
"""One-off: retag the Dorwinion and South Rhovanion rows of the LIVE TAOM_Map settlements.xml from their
interim Dale/Rohan values to their own cultures and clans (#1062, merge sitting).

The 13 fortifications and 48 villages were added on `Culture.sturgia` / `Culture.vlandia` (the fortifications
owned by clan_sturgia_1 / clan_vlandia_1) so the shipped map stayed loadable before the branch build was
deployed. This changes only the `culture` attribute of the 61 rows and the `owner` attribute of the 13
fortifications. The final values are read from the repo's own tables (add_map_fortifications.FORTS for each
fortification's owner and culture, add_map_villages.VILLAGES for each village's culture), never restated here,
so the script and `add_map_*.py --check` cannot disagree.

TARGET IS THE LIVE FILE (the repo's settlements.xml is a stale shadow). Save-compat as in
retag_khand_to_variag.py: `Settlement.Culture` is re-read from XML on every load.

Refuses, writing nothing, unless every table row exists in the file and sits on its interim culture (and the
fortifications on their interim owner), and the table counts are 61 rows and 13 fortifications. The result is
parsed before anything is written, the BOM and the CRLF line endings are kept byte for byte, and the old file
is copied to `settlements.xml.bak_retag_do_srh_<stamp>` first (a non-.xml name, so the game never loads it).

`--apply` also refuses unless the DEPLOYED TAOM ModuleData defines both cultures and every final owner
clan: a live map ahead of the deployed build makes a new campaign throw at notable spawn (RCA
docs/reviews/rca-dorwinion-south-rhovanion-2026-10-29.md row 1). Run `./build.ps1` first.

Usage:  python tools/oneoff/retag_do_srh_live.py [--apply] [--settlements PATH] [--deployed MODULEDATA]
        (dry-run by default)
"""
import argparse
import difflib
import os
import re
import shutil
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import add_map_fortifications as amf  # noqa: E402
import add_map_villages as amv  # noqa: E402

DEFAULT_SETTLEMENTS = Path(
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\TAOM_Map\ModuleData\settlements.xml"
)

# realm culture -> (interim culture attribute, interim fortification owner attribute)
INTERIM = {
    "dorwinion": ("Culture.sturgia", "Faction.clan_sturgia_1"),
    "southrhovanion": ("Culture.vlandia", "Faction.clan_vlandia_1"),
}
EXPECTED_ROWS, EXPECTED_OWNERS = 61, 13
DEFAULT_DEPLOYED = Path(
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\ModuleData"
)


def planned_rows():
    """(settlement id, realm culture, final owner attribute or None) for every table row of the two realms."""
    rows = [(f.id, f.culture, f"Faction.{f.owner}") for f in amf.FORTS if f.culture in INTERIM]
    rows += [(v.id, v.culture, None) for v in amv.VILLAGES if v.culture in INTERIM]
    return rows


def deployed_gaps(rows, moduledata):
    """The cultures and owner clans the retag names that the deployed ModuleData does not define."""
    cultures = (moduledata / "taom_spcultures.xml").read_text(encoding="utf-8-sig")
    clans = (moduledata / "characters" / "clans.xml").read_text(encoding="utf-8-sig")
    gaps = [c for c in INTERIM if not re.search(r'<Culture\b[^>]*\bid="%s"' % c, cultures)]
    owners = sorted({owner.split(".", 1)[1] for _, _, owner in rows if owner})
    return gaps + [o for o in owners if f'id="{o}"' not in clans]


def retag(text, rows):
    """The retagged text and the count of fortifications whose owner changed. Exits on any refusal."""
    out, n_owner = text, 0
    for sid, culture, owner in rows:
        interim_culture, interim_owner = INTERIM[culture]
        found = list(re.finditer(r'<Settlement\b[^>]*\bid="%s"[^>]*>' % re.escape(sid), out))
        if len(found) != 1:
            sys.exit(f"refusing: {sid} has {len(found)} <Settlement> rows in the file, expected 1")
        head = found[0].group(0)
        if f'culture="{interim_culture}"' not in head:
            sys.exit(f"refusing: {sid} is not on its interim culture {interim_culture}")
        new = head.replace(f'culture="{interim_culture}"', f'culture="Culture.{culture}"', 1)
        if owner:
            if f'owner="{interim_owner}"' not in head:
                sys.exit(f"refusing: {sid} is not on its interim owner {interim_owner}")
            new = new.replace(f'owner="{interim_owner}"', f'owner="{owner}"', 1)
            n_owner += 1
        out = out[:found[0].start()] + new + out[found[0].end():]
    return out, n_owner


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write the live file (default: dry run)")
    ap.add_argument("--settlements", type=Path, default=DEFAULT_SETTLEMENTS)
    ap.add_argument("--deployed", type=Path, default=DEFAULT_DEPLOYED,
                    help="the deployed TAOM ModuleData that must define both cultures before --apply")
    args = ap.parse_args()

    rows = planned_rows()
    n_expected_owner = sum(1 for r in rows if r[2])
    if (len(rows), n_expected_owner) != (EXPECTED_ROWS, EXPECTED_OWNERS):
        sys.exit(f"refusing: the tables give {len(rows)} rows and {n_expected_owner} fortifications, "
                 f"expected {EXPECTED_ROWS} and {EXPECTED_OWNERS}")
    raw = args.settlements.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    out, n_owner = retag(text, rows)
    ET.fromstring(out.encode("utf-8"))  # refuse to write a document that no longer parses
    changed = [line for line in difflib.unified_diff(text.splitlines(), out.splitlines(), lineterm="", n=0)
               if line.startswith(("+", "-")) and not line.startswith(("+++", "---"))]
    print(f"{len(rows)} rows retagged, {n_owner} owners; {len(changed) // 2} lines changed")
    gaps = deployed_gaps(rows, args.deployed)
    if gaps:
        print(f"the deployed build at {args.deployed} lacks: {', '.join(gaps)}")
    if not args.apply:
        print("dry run: nothing written (pass --apply)")
        return
    if gaps:
        sys.exit("refusing: deploy the build that defines them first (./build.ps1)")
    backup = args.settlements.with_name(args.settlements.name + f".bak_retag_do_srh_{time.strftime('%Y%m%d_%H%M%S')}")
    shutil.copy2(args.settlements, backup)
    args.settlements.write_bytes((b"\xef\xbb\xbf" if bom else b"") + out.encode("utf-8"))
    ET.parse(args.settlements)
    print(f"backup {backup}; written")


if __name__ == "__main__":
    main()
