#!/usr/bin/env python3
"""Repair source-culture flavour text left behind when `bluecraig` and `lindon` were cloned.

The clone scripts each carried a substitution table, and each also carried a contamination gate
that scanned for leftovers. The gate never fired, because its forbidden-word list was DERIVED FROM
the substitution table — so it could only detect words somebody had already thought to remap. Every
word nobody thought of ("Elrond", "Last Homely House", "Trollshaws", the entire Fëanorian name pool)
was invisible to both halves at once. A check built from the same assumption as the thing it checks
is not a check.

This script fixes the shipped data and, unlike those tables, works from an INDEPENDENT list of the
source cultures' identity words — names, places and epithets that belong to Rivendell or Goblin-town
and cannot be true of the culture carved out of it. That list is also what the generators' gates now
use, so the circularity is gone in both directions.

What is wrong, concretely:
  - Lindon's culture-selection description reads "Lindon, the Last Homely House ... led by Lord
    Elrond. Nestled in the Misty Mountains" — three claims that are Rivendell's and false for
    Círdan's coastal haven, in the blurb the player reads when choosing the culture.
  - Lindon's clan names are the Ñoldorin royal houses (Fëanor, Fingolfin, Finarfin, Turgon) and
    Eregion's smith-guilds; its male-name pool ends with Fëanor and all seven of his sons.
  - Troops shipped as "[Lindon] Nõldorin Lancer" and "[Lindon] Rider of Himring".
  - Blue Craig, in the Ered Luin, has a career that hunts "through the Misty Mountains" and another
    themed entirely on the High Pass — both a continent away, next to Goblin-town.

Scoped: only strings that belong to the two promoted cultures are touched. A Rivendell or
Goblin-town string keeps its own names.

Run:  python tools/fix_promoted_culture_flavor.py            # dry run, prints every change
      python tools/fix_promoted_culture_flavor.py --apply
"""
import argparse
import glob
import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MD = ROOT / "Main" / "_Module" / "ModuleData"

# Independent identity-word lists. Derived from what the SOURCE culture is in Tolkien, never from
# the substitution tables below — that separation is the whole point.
FORBIDDEN = {
    "lindon": [
        "Elrond", "Imladris", "Rivendell", "Last Homely House", "hidden valley", "Hidden Valley",
        "Trollshaws", "Noldor", "Nõldor", "Ñoldor", "Feanor", "Fëanor", "Fingolfin", "Finarfin",
        "Turgon", "Celebrimbor", "Gwaith-i-Mirdain", "Lambengolmor", "Glorfindel", "Erestor",
        "Maedhros", "Maglor", "Curufin", "Caranthir", "Celegorm", "Amras", "Amrod", "Finwe",
        "Himring", "Gondolin", "Arwen", "Lindir",
    ],
    "bluecraig": [
        "Goblin-town", "High Pass", "Misty Mountains", "Moria", "Gundabad", "Bolg",
        "Cirith Ungol", "Angmar", "Carn Dûm", "Iron Hills",
    ],
}

# Replacements, longest phrase FIRST so a substring never double-substitutes.
#
# Lindon is Círdan's realm on the Gulf of Lune: Falathrim Sindar, shipwrights and mariners, the
# Grey Havens at Mithlond with Forlond and Harlond on either arm of the firth, the Tower Hills
# (Emyn Beraid) inland, and Balar and the Falas behind them in the First Age. Gil-galad ruled here,
# so he stays. Everything Ñoldorin, Eregionic or valley-bound goes.
SUBS = {
    "lindon": [
        ("Lindon, the Last Homely House", "Lindon, the Grey Havens"),
        ("the Last Homely House", "the Grey Havens"),
        ("Last Homely House", "Grey Havens"),
        ("Nestled in the Misty Mountains", "Set on the shores of the Gulf of Lune"),
        ("valleys of the Misty Mountains", "shores of the Gulf of Lune"),
        ("the Misty Mountains", "the Ered Luin"),
        ("Gwaith-i-Mirdain", "Gwaith-i-Falath"),
        ("Lambengolmor", "Cirdanath"),
        ("House of Elrond", "House of Cirdan"),
        ("Elrond's loremasters", "Cirdan's shipwrights"),
        ("Elrond's healers", "Cirdan's healers"),
        ("Elrond's household", "Cirdan's household"),
        ("Elrond's halls", "Cirdan's halls"),
        ("halls of Elrond", "halls of Cirdan"),
        ("Lord Elrond", "Cirdan the Shipwright"),
        ("Elrond", "Cirdan"),
        ("the hidden valley", "the Grey Havens"),
        ("hidden valley", "grey havens"),
        ("Hidden Valley", "Grey Havens"),
        ("Trollshaws", "Tower Hills"),
        ("Imladris", "Mithlond"),
        ("Rivendell", "Lindon"),
        ("Nõldorin", "Falathrim"),
        ("Ñoldorin", "Falathrim"),
        ("Noldorin", "Falathrim"),
        ("Nõldor", "Falathrim"),
        ("Ñoldor", "Falathrim"),
        ("Noldor", "Falathrim"),
        # First Age Ñoldorin houses and princes -> Falathrim / Sindar of the Havens.
        ("House of Feanor", "House of Falathar"),
        ("House of Fingolfin", "House of Galdor"),
        ("House of Finarfin", "House of Aerandir"),
        ("House of Turgon", "House of Erellont"),
        ("House of Celebrimbor", "House of Nimros"),
        ("House of Glorfindel", "House of Belegon"),
        ("House of Erestor", "House of Elmoth"),
        ("House of Arwen", "House of Nimloth"),
        ("House of Lindir", "House of Salmar"),
        ("House of Gildor", "House of Gildor"),   # Gildor Inglorion wandered Lindon; keep.
        ("Rider of Himring", "Rider of Balar"),
        ("Himring", "Balar"),
        ("Gondolin", "Falas"),
        ("Maedhros", "Falathar"),
        ("Maglor", "Aerandir"),
        ("Curufin", "Erellont"),
        ("Caranthir", "Belegon"),
        ("Celegorm", "Nimros"),
        ("Amras", "Salmar"),
        ("Amrod", "Elmoth"),
        ("Finwe", "Nowe"),          # Nowë was Círdan's birth-name.
        ("Feanor", "Falathar"),
        ("Glorfindel", "Belegon"),
        ("Erestor", "Elmoth"),
        ("Arwen", "Nimloth"),
        ("Lindir", "Salmar"),
    ],
    # Blue Craig sits in the western spurs of the Ered Luin above the Gulf of Lune, cut off from
    # Goblin-town by the whole width of Eriador. Its neighbours are Dwarves and the Grey Havens.
    "bluecraig": [
        ("Goblin-town", "Blue Craig"),
        ("the High Pass", "the Ered Luin"),
        ("High Pass", "Ered Luin"),
        ("the Misty Mountains", "the Ered Luin"),
        ("Misty Mountains", "Ered Luin"),
        ("Moria", "Nogrod"),
        ("Gundabad", "Blue Craig"),
        ("Bolg's", "Skarnak's"),
        ("Bolg", "Skarnak"),
        ("Cirith Ungol", "Lune-mouth"),
        ("Carn Dûm", "Ered Luin"),
        ("Angmar's Children", "Craig-spawn"),
        ("Angmar", "Forlindon"),
        ("Iron Hills", "Blue Mountains"),
    ],
}


def remap(value, culture):
    for old, new in SUBS[culture]:
        value = value.replace(old, new)
    return value


def leftovers(value, culture):
    return [w for w in FORBIDDEN[culture] if w in value]


def read(path):
    raw = Path(path).read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig" if bom else "utf-8")
    return text.replace("\r\n", "\n"), ("\r\n" if "\r\n" in text else "\n"), bom


def write(path, text, nl, bom):
    Path(path).write_bytes((b"\xef\xbb\xbf" if bom else b"") + text.replace("\n", nl).encode("utf-8"))


def fix_string_tables(changes):
    """taom_module_strings.xml + taom_cc_strings.xml — scoped by culture appearing in the string id."""
    for rel in ("taom_module_strings.xml", "taom_cc_strings.xml"):
        path = MD / rel
        text, nl, bom = read(path)

        def repl(m):
            sid, val = m.group(1), m.group(2)
            for culture in FORBIDDEN:
                if culture in sid.lower():
                    new = remap(val, culture)
                    if new != val:
                        changes.append((rel, culture, sid, val, new))
                        return f'<string id="{sid}" text="{new}" />'
            return m.group(0)

        text = re.sub(r'<string id="([^"]+)" text="([^"]*)" />', repl, text)
        yield path, text, nl, bom


def fix_menu_json(changes):
    """The four culture-scoped narrative menus — scoped by the entry's own culture_id."""
    for path in sorted(glob.glob(str(MD / "charactercreation" / "*_menu.json"))):
        text, nl, bom = read(path)
        data = json.loads(text)
        touched = False
        for e in data:
            culture = e.get("culture_id")
            if culture not in SUBS:
                continue
            for key in ("text", "description"):
                if isinstance(e.get(key), str):
                    new = remap(e[key], culture)
                    if new != e[key]:
                        changes.append((os.path.basename(path), culture, e.get("string_id", "?"),
                                        e[key], new))
                        e[key] = new
                        touched = True
        if touched:
            # Re-serialise in the file's existing canonical shape (4-space indent, inline arrays),
            # which is what these files already are.
            yield path, json.dumps(data, indent=4, ensure_ascii=False) + "\n", nl, bom


def fix_scoped_xml(changes, rel, scope_pattern, only_culture=None):
    """Rewrite `{=key}value` display text inside a culture-scoped region of an XML file.

    `only_culture` pins the culture for whole-file targets (troops_<culture>.xml), where the scope
    is the file itself rather than a matched region — without it the loop would apply Blue Craig's
    substitutions to Lindon's troops and vice versa.
    """
    path = MD / rel
    text, nl, bom = read(path)
    out = text
    for culture in ([only_culture] if only_culture else list(SUBS)):
        for m in re.finditer(scope_pattern.format(culture=re.escape(culture)), text, re.S):
            block = m.group(0)
            new_block = block

            def repl(mm):
                val = mm.group(2)
                new = remap(val, culture)
                if new != val:
                    changes.append((rel, culture, mm.group(1)[:40], val, new))
                return f"{mm.group(1)}{new}\""

            new_block = re.sub(r'(\{=[A-Za-z0-9_.]+\})([^"]*)"', repl, new_block)
            if new_block != block:
                out = out.replace(block, new_block)
    if out != text:
        return path, out, nl, bom
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    changes, writes = [], []
    writes += list(fix_string_tables(changes))
    writes += list(fix_menu_json(changes))

    # Culture blocks, the two new troop files, and the six cloned careers.
    got = fix_scoped_xml(changes, "taom_spcultures.xml", r'<Culture\b[^>]*\bid="{culture}".*?</Culture>')
    if got:
        writes.append(got)
    for culture in SUBS:
        got = fix_scoped_xml(changes, f"troops/troops_{culture}.xml", r"\A.*\Z")
        if got:
            writes.append(got)
    got = fix_scoped_xml(changes, "career_system/taom_careers.xml",
                         r'<Career id="(?:craig|falathrim)_[^"]+".*?</Career>')
    if got:
        writes.append(got)

    print(f"{len(changes)} string(s) to repair across {len(writes)} file(s)\n")
    for rel, culture, key, old, new in changes[:14]:
        print(f"  [{culture}] {rel} :: {key}")
        print(f"      - {old[:110]}")
        print(f"      + {new[:110]}")
    if len(changes) > 14:
        print(f"  ... and {len(changes) - 14} more")

    if not args.apply:
        print("\nDRY RUN — re-run with --apply to write")
        return 0

    for path, text, nl, bom in writes:
        write(path, text, nl, bom)
        if str(path).endswith(".xml"):
            import xml.etree.ElementTree as ET
            ET.parse(path)
        else:
            json.loads(Path(path).read_text(encoding="utf-8-sig"))
        print(f"  repaired + well-formed: {Path(path).relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
