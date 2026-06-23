# -*- coding: utf-8 -*-
"""Fix leftover Mordor-flavored descriptions on re-themed (now-Dunland) lords.

Sweep of all 21 Group A hero descriptions found exactly ONE with Mordor flavor:
lord_1_56_2 (Rustica) "...serves in the armies of darkness." -> Dunland-appropriate.

By-id replacement (keyed on TAOM_hero_<id>) across:
  - heroes.xslt              <xsl:attribute name="text">{=KEY}Old</xsl:attribute>
  - taom_xslt_strings.xml    <string id="KEY" text="{=KEY}Old" />
  - Languages/*/std_taom_xslt_strings_*.xml  <string id="KEY" text="<translation>" />

Non-Latin language files are set to the new English text (re-run translate_with_claude.py
for proper translations).

Usage:
  python tools/fix_dunland_descriptions.py --dry-run
  python tools/fix_dunland_descriptions.py --apply
"""
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MD = ROOT / "Main" / "_Module" / "ModuleData"
HEROES_XSLT = MD / "heroes.xslt"
XSLT_STRINGS = MD / "taom_xslt_strings.xml"
LANG_FILES = sorted((MD / "Languages").glob("*/std_taom_xslt_strings_*.xml"))

# key -> new English description
DESC = {
    "TAOM_hero_1_56_2": "Rustica rides to war alongside her father Tormund, a fierce young warrior of the clan.",
}


def sub_first(pattern, repl, text):
    return re.subn(pattern, lambda m: m.group(1) + repl + m.group(2), text)


def main():
    ap = argparse.ArgumentParser()
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--dry-run", action="store_true")
    g.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    edits = []
    report = []

    # heroes.xslt: {=KEY}Old</xsl:attribute>
    t = HEROES_XSLT.read_text(encoding="utf-8")
    for key, new in DESC.items():
        k = re.escape(key)
        t, n = sub_first(r'(\{=' + k + r'\})[^<]*(</xsl:attribute>)', new, t)
        report.append((HEROES_XSLT.name, key, n))
    edits.append((HEROES_XSLT, t))

    # taom_xslt_strings.xml: id="KEY" text="{=KEY}Old"
    t = XSLT_STRINGS.read_text(encoding="utf-8")
    for key, new in DESC.items():
        k = re.escape(key)
        t, n = sub_first(r'(id="' + k + r'" text="\{=' + k + r'\})[^"]*(")', new, t)
        report.append((XSLT_STRINGS.name, key, n))
    edits.append((XSLT_STRINGS, t))

    # language files: id="KEY" text="<value>"
    for lf in LANG_FILES:
        t = lf.read_text(encoding="utf-8")
        for key, new in DESC.items():
            k = re.escape(key)
            t, n = sub_first(r'(id="' + k + r'" text=")[^"]*(")', new, t)
            report.append((f"Languages/{lf.parent.name}", key, n))
        edits.append((lf, t))

    total = sum(n for *_, n in report)
    missing = [(f, key) for f, key, n in report if n == 0]
    for f, key, n in report:
        flag = "  " if n else "!!"
        print(f"  {flag} {f:28s} {key} x{n}")
    print(f"\nTotal description replacements: {total} (expected {len(DESC) * (2 + len(LANG_FILES))})")
    if missing:
        print("MISSING:", missing)

    if args.apply:
        for path, text in edits:
            path.write_text(text, encoding="utf-8")
        print("APPLIED.")
    else:
        print("DRY RUN - no files written.")


if __name__ == "__main__":
    sys.exit(main())
