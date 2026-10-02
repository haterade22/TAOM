#!/usr/bin/env python3
"""Lock the troll gear away from players in a LOTRLOME_Armory (docs/features/troll-race.md).

Troll weapons and armour must never be bought, smithed or won (Mike, 2026-10-02). In the Armory that is two
attributes: is_merchandise="false" on each item (shops, workshops, loot, tournament prizes) and
is_hidden="true" on each troll crafting piece (the smithy's designer and its research unlocks). The Armory is
unversioned, so a reinstall reverts them; CREATURE_GEAR_OBTAINABLE in validate_moduledata.py notices, and this
script puts them back. It is also how the fix reaches a release channel before the next editor package:

    python tools/lock_creature_gear.py                                   # dry run on the dev install
    python tools/lock_creature_gear.py --apply
    python tools/lock_creature_gear.py --modules "E:/LOTRAOM_Releases/patreon/Modules" --apply
    python tools/validate_moduledata.py --game-modules "E:/LOTRAOM_Releases/patreon/Modules" --code CREATURE_GEAR_OBTAINABLE

A new troll item the gate names goes in ITEMS or PIECES below. Dry run by default; --apply writes, after a
backup to <file>.bak-<stamp> (never *.xml: the item folders are globbed). Bytes round-trip exactly (tools/README.md
"XML I/O convention", idiom B), every result is parsed before it is written, and a second run changes nothing.
The lotraom-assets mirror is never touched.
"""
import argparse
import re
import shutil
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

from _gamedir import ensure_exists, game_modules, game_or_kit_running

DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

# (file under ModuleData, element tag, item id)
ITEMS = [
    ("LOTRLOME_items/LOTRAOM_weapons.xml", "CraftedItem", "wm_cave_troll_1h_mace_a"),
    ("LOTRLOME_items/LOTRAOM_shields.xml", "Item", "wm_cave_troll_shield_a01"),
    ("LOTRLOME_items/troll/arm_armors.xml", "Item", "lotr_troll_bracers"),
]
PIECES_FILE = "LOTRLOME_crafting_pieces.xml"
PIECES = [
    "wm_cave_troll_1h_mace_a01_head", "wm_cave_troll_1h_mace_a02_head", "wm_cave_troll_1h_mace_a03_head",
    "wm_cave_troll_2h_mace_head", "wm_hill_troll_2h_hammer_head", "wm_cave_troll_spear_a01_blade",
    "wm_cave_troll_1h_mace_a01_handle", "wm_cave_troll_1h_mace_a02_handle", "wm_cave_troll_1h_mace_a03_handle",
    "wm_cave_troll_2h_mace_handle", "wm_hill_troll_2h_hammer_handle", "wm_cave_troll_spear_a01_handle",
]


class LockError(Exception):
    """The file is not shaped as expected; decide by hand rather than guess."""


def _head(text, tag, item_id):
    """(start, end) of the one opening tag <tag ... id="item_id" ...>."""
    pat = re.compile(r'<%s\b[^>]*?\bid="%s"[^>]*>' % (tag, re.escape(item_id)), re.S)
    hits = list(pat.finditer(text))
    if len(hits) != 1:
        raise LockError(f"{item_id}: expected one <{tag}> head, found {len(hits)}")
    return hits[0].start(), hits[0].end()


def lock_item(text, tag, item_id, nl):
    """Return (text, changed) with is_merchandise="false" on the item."""
    s, e = _head(text, tag, item_id)
    head = text[s:e]
    if 'is_merchandise="false"' in head:
        return text, False
    if "is_merchandise=" in head:
        new = re.sub(r'is_merchandise="[^"]*"', 'is_merchandise="false"', head, count=1)
    else:
        # After culture=, keeping the files' one-attribute-per-line layout.
        m = re.search(r'(\n([ \t]+)culture="[^"]*")', head)
        if not m:
            raise LockError(f"{item_id}: no culture= line to place is_merchandise after")
        new = head[:m.end()] + f'{nl}{m.group(2)}is_merchandise="false"' + head[m.end():]
    return text[:s] + new + text[e:], True


def hide_piece(text, piece_id, nl):
    """Return (text, changed) with is_hidden="true" on the crafting piece."""
    s, e = _head(text, "CraftingPiece", piece_id)
    head = text[s:e]
    if 'is_hidden="true"' in head:
        return text, False
    if "is_hidden=" in head:
        raise LockError(f'{piece_id}: has an is_hidden that is not "true"; decide by hand')
    m = re.search(r'(\n([ \t]+)tier="[^"]*")', head)
    if not m:
        raise LockError(f"{piece_id}: no tier= line to place is_hidden after")
    new = head[:m.end()] + f'{nl}{m.group(2)}is_hidden="true"' + head[m.end():]
    return text[:s] + new + text[e:], True


def plan_file(path, edits):
    """New text for one file after `edits` (each `(text, nl) -> (text, changed)`), or None when unchanged.
    Raises LockError before anything is written when the file is not shaped as expected."""
    raw = path.read_bytes().decode("utf-8")
    nl = "\r\n" if "\r\n" in raw else "\n"
    text, changed = raw, False
    for edit in edits:
        text, c = edit(text, nl)
        changed |= c
    if not changed:
        return None
    try:
        ET.fromstring(text.lstrip("\ufeff").encode("utf-8"))
    except ET.ParseError as exc:
        raise LockError(f"{path}: the edited document does not parse ({exc})")
    return text


def lock(armory_moduledata, apply, stamp):
    """Lock every troll item and piece under one Armory ModuleData; returns the count of files changed (or
    that would change). Every file is planned and parsed before any is written, so a file shaped unexpectedly
    leaves the whole Armory untouched."""
    root = Path(armory_moduledata)
    edits = {}
    for rel, tag, item_id in ITEMS:
        edits.setdefault(root / rel, []).append(
            lambda t, nl, tag=tag, item_id=item_id: lock_item(t, tag, item_id, nl))
    edits[root / PIECES_FILE] = [lambda t, nl, pid=pid: hide_piece(t, pid, nl) for pid in PIECES]

    planned = {path: plan_file(path, file_edits) for path, file_edits in edits.items()}
    for path, text in planned.items():
        if text is None:
            print(f"  unchanged    {path}")
            continue
        print(f"  {'WRITE' if apply else 'would write'}  {path}")
        if apply:
            backup = path.with_name(f"{path.name}.bak-{stamp}")
            if not backup.exists():
                shutil.copy2(path, backup)
            path.write_bytes(text.encode("utf-8"))
    return sum(1 for text in planned.values() if text is not None)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--modules", help="a Modules folder (default: the dev install's)")
    ap.add_argument("--apply", action="store_true", help="write (default: dry run)")
    args = ap.parse_args(argv)

    live = game_modules(DEFAULT_GAME)
    modules = Path(args.modules) if args.modules else live
    armory = ensure_exists(modules / "LOTRLOME_Armory" / "ModuleData", "the LOTRLOME_Armory ModuleData")
    if args.apply and modules.resolve() == live.resolve() and game_or_kit_running():
        print("ERROR: Bannerlord or the Modding Kit is running; close it before writing the live Armory",
              file=sys.stderr)
        return 2
    print(f"{'Applying' if args.apply else 'Dry run'} on {armory}")
    try:
        changed = lock(armory, args.apply, time.strftime("%Y%m%d-%H%M%S"))
    except LockError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    print(f"{changed} file(s) {'changed' if args.apply else 'to change'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
