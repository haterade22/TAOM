# -*- coding: utf-8 -*-
"""Rename 5 genuine-Mordor lords to orc names across the full loc pipeline.

By-id replacement (keyed on the localization id), so it works even though the
12 language files hold transliterations rather than the Latin default.

Touched per lord:
  - lords.xslt                         <xsl:attribute name="name">{=KEY}Old</...>
  - taom_xslt_strings.xml              <string id="KEY" text="{=KEY}Old" />
  - characters/lords.xml (children)    name="{=KEY}Old"   (single-line NPCCharacter)
  - Languages/*/std_taom_xslt_strings_*.xml  <string id="KEY" text="<transliteration>" />

Non-Latin language files are set to the new Latin name (orc proper nouns); re-run
tools/translate_with_claude.py later if transliterations are wanted.

Usage:
  python tools/rename_mordor_lords.py --dry-run
  python tools/rename_mordor_lords.py --apply
"""
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MD = ROOT / "Main" / "_Module" / "ModuleData"
LORDS_XSLT = MD / "lords.xslt"
XSLT_STRINGS = MD / "taom_xslt_strings.xml"
LORDS_XML = MD / "characters" / "lords.xml"
LANG_FILES = sorted((MD / "Languages").glob("*/std_taom_xslt_strings_*.xml"))

# lord_id -> new name
RENAMES = {
    "lord_1_68": "Gorthak",
    "lord_1_69": "Grukhash",
    "lord_1_74": "Bûrznak",   # Burznak
    "lord_1_30_2": "Mogra",
    "lord_1_30_3": "Snaga",
}
CHILDREN_IN_XML = {"lord_1_30_2", "lord_1_30_3"}  # also have a standalone NPCCharacter


def key(lid):
    return f"aom_{lid}_name"


def sub_count(pattern, repl, text):
    new, n = re.subn(pattern, lambda m: m.group(1) + repl + m.group(2), text)
    return new, n


def main():
    ap = argparse.ArgumentParser()
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--dry-run", action="store_true")
    g.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    edits = []  # (path, text)
    report = []

    # lords.xslt: <xsl:attribute name="name">{=KEY}Old</xsl:attribute>
    t = LORDS_XSLT.read_text(encoding="utf-8")
    for lid, new in RENAMES.items():
        k = re.escape(key(lid))
        t, n = sub_count(r'(\{=' + k + r'\})[^<]*(</xsl:attribute>)', new, t)
        report.append((LORDS_XSLT.name, lid, new, n))
    edits.append((LORDS_XSLT, t))

    # taom_xslt_strings.xml: <string id="KEY" text="{=KEY}Old" />
    t = XSLT_STRINGS.read_text(encoding="utf-8")
    for lid, new in RENAMES.items():
        k = re.escape(key(lid))
        t, n = sub_count(r'(id="' + k + r'" text="\{=' + k + r'\})[^"]*(")', new, t)
        report.append((XSLT_STRINGS.name, lid, new, n))
    edits.append((XSLT_STRINGS, t))

    # characters/lords.xml: name="{=KEY}Old" (children only)
    t = LORDS_XML.read_text(encoding="utf-8")
    for lid in CHILDREN_IN_XML:
        new = RENAMES[lid]
        k = re.escape(key(lid))
        t, n = sub_count(r'(name="\{=' + k + r'\})[^"]*(")', new, t)
        report.append((LORDS_XML.name, lid, new, n))
    edits.append((LORDS_XML, t))

    # language files: <string id="KEY" text="<value>" />
    for lf in LANG_FILES:
        t = lf.read_text(encoding="utf-8")
        for lid, new in RENAMES.items():
            k = re.escape(key(lid))
            t, n = sub_count(r'(id="' + k + r'" text=")[^"]*(")', new, t)
            report.append((f"Languages/{lf.parent.name}/{lf.name}", lid, new, n))
        edits.append((lf, t))

    # Summarize
    total = sum(r[3] for r in report)
    missing = [(f, lid) for (f, lid, _, n) in report if n == 0]
    by_file = {}
    for f, lid, new, n in report:
        by_file.setdefault(f, 0)
        by_file[f] += n
    for f in sorted(by_file):
        print(f"  {by_file[f]:2d}  {f}")
    print(f"\nTotal name replacements: {total} (expected {len(RENAMES) * (3 + len(LANG_FILES)) - (len(RENAMES) - len(CHILDREN_IN_XML))})")
    if missing:
        print("MISSING (id not found in file):")
        for f, lid in missing:
            # characters/lords.xml legitimately lacks the 3 parent lords
            if not (f == LORDS_XML.name and lid not in CHILDREN_IN_XML):
                print(f"  !! {f}  {lid}")

    if args.apply:
        for path, text in edits:
            path.write_text(text, encoding="utf-8")
        print("APPLIED.")
    else:
        print("DRY RUN - no files written.")


if __name__ == "__main__":
    sys.exit(main())
