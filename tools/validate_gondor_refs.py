#!/usr/bin/env python3
"""Validate every sk_gd_* / sk_dg_* item reference in troops_gondor.xml resolves
to an id in the LOTRLOME_Armory gondor folder.

Issue: #99 — gates the underwear bug.
"""
import os
import re
import sys
import glob
from _gamedir import game_dir

TROOPS_FILE = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Main", "_Module", "ModuleData", "troops", "troops_gondor.xml"
)

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
# (tools/.env.example also documents a narrower TAOM_ARMORY_BASE, but only
# cleanup_deleted_gondor_items.py reads it; the game root is the consistent knob here.)
ARMORY_BASE = os.path.join(
    game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"),
    "Modules", "LOTRLOME_Armory", "ModuleData", "LOTRLOME_items", "gondor",
)

REF_RE = re.compile(r'Item\.((?:sk_gd|sk_dg)_[A-Za-z0-9_]+)')
ID_RE  = re.compile(r'id="((?:sk_gd|sk_dg)_[A-Za-z0-9_]+)"')


def collect_refs() -> set:
    with open(TROOPS_FILE, "r", encoding="utf-8") as f:
        return set(REF_RE.findall(f.read()))


def collect_ids() -> set:
    ids = set()
    for path in glob.glob(os.path.join(ARMORY_BASE, "*.xml")):
        with open(path, "r", encoding="utf-8") as f:
            ids.update(ID_RE.findall(f.read()))
    return ids


def main():
    refs = collect_refs()
    ids = collect_ids()
    missing = sorted(refs - ids)
    unused = sorted(ids - refs)

    print(f"References in troops_gondor.xml: {len(refs)}")
    print(f"IDs in Armory gondor/*.xml:      {len(ids)}")
    print(f"\nMissing (referenced by troop but not in Armory): {len(missing)}")
    for m in missing:
        print(f"  - {m}")
    print(f"\nUnused (in Armory but no troop reference): {len(unused)}")
    if len(unused) <= 30:
        for u in unused:
            print(f"  . {u}")
    else:
        print(f"  ({len(unused)} items — list suppressed; not a failure)")

    if missing:
        print("\nFAIL: missing references will cause underwear bug in-game.")
        sys.exit(1)
    print("\nPASS: all sk_gd_* / sk_dg_* references resolve.")


if __name__ == "__main__":
    main()
