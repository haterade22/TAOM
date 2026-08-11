#!/usr/bin/env python3
"""Retag the Blue Craig and Lindon kingdoms, clans, lords, heroes and settlements onto the cultures
promoted by tools/promote_borrowed_cultures.py.

This is step 2 of the promotion and it MUST follow step 1, never precede it. A culture that owns no
settlement makes vanilla SpawnLordParty's unguarded `Settlement.All.First(x => x.Culture == ...)`
throw on the daily clan tick, so the window between "culture exists" and "culture owns land" is a
CTD waiting to happen (#374, LANDLESS_CULTURE). Running the two steps in the right order closes it
in one pass.

FOUR of the five targets are in this repo. The fifth is not:

    <game>/Modules/TAOM_Map/ModuleData/settlements.xml

is the LIVE settlement file and the authoritative one — the repo's own
Main/_Module/ModuleData/settlements.xml is a stale shadow whose edits never reach the game. The live
file is unversioned, so a module reinstall silently reverts anything written here. That is why
`LANDLESS_CULTURE` in tools/validate_moduledata.py is the gate that matters: it fails in-repo if the
external edit is missing or has been reverted, which is the only thing that makes an unversioned
edit safe to depend on.

A timestamped `.bak-*` copy is written before the live file is touched. NEVER use a `.xml`
extension for that backup — registered ModuleData directories are globbed `*.xml`, so an `.xml`
backup is loaded as real data and injects duplicate settlement ids.

Run:  python tools/retag_promoted_cultures.py                       # dry run
      python tools/retag_promoted_cultures.py --apply
      python tools/retag_promoted_cultures.py --apply --game-modules "<path>/Modules"
"""
import argparse
import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MD = ROOT / "Main" / "_Module" / "ModuleData"
DEFAULT_MODULES = Path(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules")

# target culture -> (culture it is being taken off, kingdom id, entity id patterns)
RETAG = {
    "lindon": {
        "from": "rivendell",
        "kingdom": "lindon",
        "clans": r"clan_lindon_\d+",
        "heroes": r"lord_LN\d+_\d+",
        # Settlement ids for Lindon: town_LN1 and village_LN1_N.
        "settlements": r"(?:town|village|castle|castle_village)_LN\d+(?:_\d+)?",
    },
    "bluecraig": {
        "from": "goblin",
        "kingdom": "bluecraig",
        "clans": r"clan_bluecraig_\d+",
        "heroes": r"lord_BC\d+_\d+",
        "settlements": r"(?:town|village|castle|castle_village)_GBC\d+(?:_\d+)?",
    },
}


def read(path):
    raw = Path(path).read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig" if bom else "utf-8")
    return text.replace("\r\n", "\n"), ("\r\n" if "\r\n" in text else "\n"), bom


def write(path, text, newline, bom):
    Path(path).write_bytes((b"\xef\xbb\xbf" if bom else b"") + text.replace("\n", newline).encode("utf-8"))


def retag_elements(text, id_pattern, old_culture, new_culture, element):
    """Rewrite culture="Culture.<old>" to the new culture, but ONLY inside elements whose id matches.

    Scoped by element rather than done as a global replace, because the files being edited hold
    every other faction's data too — a bare replace of `Culture.rivendell` in lords.xml would drag
    Rivendell's own lords across with them.

    `element` is REQUIRED, and that is the whole safety property. An earlier version defaulted to a
    generic `[A-Za-z]+`, which let the paired-element pattern match the ROOT element: the root
    contains a matching id somewhere, so every `Culture.<old>` in the file was rewritten. It
    reported 102 retags on lords.xml — precisely Rivendell's 22 plus Goblin-town's 80, i.e. every
    lord of both host cultures, rather than the 50 that belong to the two new ones.
    """
    if not element or not element.isalpha():
        raise SystemExit(f"retag_elements needs a concrete element name, got {element!r}")
    tag = element
    count = 0

    def fix(m):
        nonlocal count
        block = m.group(0)
        if not re.search(rf'\bid="{id_pattern}"', block):
            return block
        new_block, n = re.subn(rf'culture="Culture\.{re.escape(old_culture)}"',
                               f'culture="Culture.{new_culture}"', block)
        count += n
        return new_block

    # Self-closing and paired elements both occur across these files.
    text = re.sub(rf"<{tag}\b[^>]*?/>", fix, text, flags=re.DOTALL)
    text = re.sub(rf"<({tag})\b[^>]*?>.*?</\1>", fix, text, flags=re.DOTALL)
    return text, count


def process(path, jobs, label, results):
    if not Path(path).exists():
        results.append((label, "MISSING", 0))
        return None
    text, nl, bom = read(path)
    total = 0
    for id_pattern, old, new, element in jobs:
        text, n = retag_elements(text, id_pattern, old, new, element)
        total += n
    results.append((label, "ok", total))
    return (path, text, nl, bom, total)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--game-modules", default=os.environ.get("BANNERLORD_MODULES", str(DEFAULT_MODULES)))
    args = ap.parse_args()

    live = Path(args.game_modules) / "TAOM_Map" / "ModuleData" / "settlements.xml"
    results, pending = [], []

    kingdom_jobs = [(c["kingdom"], c["from"], t, "Kingdom") for t, c in RETAG.items()]
    clan_jobs = [(c["clans"], c["from"], t, "Faction") for t, c in RETAG.items()]
    # lords.xml defines <NPCCharacter>. heroes.xml defines <Hero>, which carries only `faction=` and
    # no `culture=` at all — a hero's culture comes from its clan — so that file is expected to
    # report 0 retags. Zero there is correct; zero on lords.xml would not be.
    lord_jobs = [(c["heroes"], c["from"], t, "NPCCharacter") for t, c in RETAG.items()]
    hero_jobs = [(c["heroes"], c["from"], t, "Hero") for t, c in RETAG.items()]
    settlement_jobs = [(c["settlements"], c["from"], t, "Settlement") for t, c in RETAG.items()]

    for rel, jobs, label in [
        ("taom_spkingdoms.xml", kingdom_jobs, "kingdoms"),
        ("characters/clans.xml", clan_jobs, "clans"),
        ("characters/lords.xml", lord_jobs, "lords"),
        ("characters/heroes.xml", hero_jobs, "heroes"),
    ]:
        got = process(MD / rel, jobs, label, results)
        if got:
            pending.append(got)

    got = process(live, settlement_jobs, "settlements (LIVE, EXTERNAL)", results)
    if got:
        pending.append(got)

    print(f"{'target':32s} {'status':8s} {'retagged'}")
    for label, status, n in results:
        print(f"  {label:30s} {status:8s} {n}")
    total = sum(n for _, _, n in results)
    print(f"  {'TOTAL':30s} {'':8s} {total}")

    if total == 0:
        print("\nNothing to retag — already done, or the culture promotion has not run yet.")
        return 0

    if not args.apply:
        print("\nDRY RUN — re-run with --apply to write.")
        print(f"Live file that WILL be modified outside git: {live}")
        return 0

    for path, text, nl, bom, n in pending:
        if not n:
            continue
        external = ROOT not in Path(path).parents
        if external:
            # Backup first, and never as *.xml — registered dirs are globbed and would load it.
            stamp = Path(path).stat().st_mtime_ns
            backup = Path(str(path) + f".bak-{stamp}")
            shutil.copy2(path, backup)
            print(f"  backup: {backup.name}")
        write(path, text, nl, bom)
        ET.parse(path)
        print(f"  retagged + well-formed ({n}): {path}")

    print("\nNow run:")
    print('  python tools/validate_moduledata.py --game-modules "<path>/Modules"')
    print("LANDLESS_CULTURE must be clean. If it is not, a settlement retag did not land.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
