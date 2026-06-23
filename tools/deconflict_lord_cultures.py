"""Deconflict Group A lords: change Culture.mordor -> Culture.empire (Dunland).

These ~21 lords carry Culture.mordor but sit in clan_empire_north_* (Dunland)
clans with Northman/barbarian names (Astrid Bearclaw, Fenrik the Red Wolf, ...).
The fix (per the culture-vs-faction audit) is to re-culture them to Culture.empire
so culture matches their Dunland clans. They stay in their existing clans.

Targets:
  - lords.xslt: per-template culture attribute (canonical lord_1_* parents + the
    two family children lord_1_56_1 / lord_1_56_2).
  - characters/lords.xml: the two children also have a standalone <NPCCharacter>.

Rustica (lord_1_56_2) ALSO needs a clan move (south_4 -> Dunland) handled
separately; this script only touches culture.

Usage:
  python tools/deconflict_lord_cultures.py --dry-run
  python tools/deconflict_lord_cultures.py --apply
"""
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LORDS_XSLT = ROOT / "Main" / "_Module" / "ModuleData" / "lords.xslt"
LORDS_XML = ROOT / "Main" / "_Module" / "ModuleData" / "characters" / "lords.xml"

# Group A: mordor-culture lords verified in clan_empire_north_* (Dunland).
GROUP_A = [
    "lord_1_20", "lord_1_21", "lord_1_22",
    "lord_1_31", "lord_1_32", "lord_1_33",
    "lord_1_41", "lord_1_411", "lord_1_42", "lord_1_422", "lord_1_43",
    "lord_1_50", "lord_1_51",
    "lord_1_56", "lord_1_56_1", "lord_1_56_2",
    "lord_1_58", "lord_1_64", "lord_1_66", "lord_1_67", "lord_1_70",
]
# Children that also have a standalone <NPCCharacter> in characters/lords.xml.
IN_LORDS_XML = ["lord_1_56_1", "lord_1_56_2"]

OLD = "Culture.mordor"
NEW = "Culture.empire"


def patch_xslt(text):
    changes = []
    for lid in GROUP_A:
        # Isolate this lord's template block: from its match line to the next </xsl:template>.
        m = re.search(
            r"(<xsl:template match=\"NPCCharacter\[@id='" + re.escape(lid) + r"'\]\">.*?</xsl:template>)",
            text, re.DOTALL,
        )
        if not m:
            changes.append((lid, "NOT FOUND in lords.xslt", 0))
            continue
        block = m.group(1)
        n = block.count(OLD)
        if n == 0:
            changes.append((lid, f"no '{OLD}' in block (already {NEW}?)", 0))
            continue
        new_block = block.replace(OLD, NEW)
        text = text[: m.start(1)] + new_block + text[m.end(1):]
        changes.append((lid, f"{OLD} -> {NEW}", n))
    return text, changes


def patch_xml(text):
    changes = []
    lines = text.splitlines(keepends=True)
    for i, line in enumerate(lines):
        for lid in IN_LORDS_XML:
            if f'id="{lid}"' in line and 'culture="Culture.mordor"' in line:
                lines[i] = line.replace('culture="Culture.mordor"', 'culture="Culture.empire"')
                changes.append((lid, "culture mordor -> empire", 1))
    return "".join(lines), changes


def main():
    ap = argparse.ArgumentParser()
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--dry-run", action="store_true")
    g.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    xslt = LORDS_XSLT.read_text(encoding="utf-8")
    xml = LORDS_XML.read_text(encoding="utf-8")

    new_xslt, xslt_changes = patch_xslt(xslt)
    new_xml, xml_changes = patch_xml(xml)

    print(f"== lords.xslt ({LORDS_XSLT}) ==")
    for lid, msg, n in xslt_changes:
        flag = "  " if n else "!!"
        print(f"  {flag} {lid:14s} {msg}")
    print(f"== characters/lords.xml ({LORDS_XML}) ==")
    for lid, msg, n in xml_changes:
        print(f"     {lid:14s} {msg}")

    total = sum(n for _, _, n in xslt_changes) + sum(n for _, _, n in xml_changes)
    missing = [lid for lid, _, n in xslt_changes if n == 0]
    print(f"\nTotal culture replacements: {total}")
    if missing:
        print(f"WARNING: no change for: {', '.join(missing)}")

    if args.apply:
        LORDS_XSLT.write_text(new_xslt, encoding="utf-8")
        LORDS_XML.write_text(new_xml, encoding="utf-8")
        print("APPLIED.")
    else:
        print("DRY RUN - no files written.")

    # Non-zero exit if any XSLT target was missing (safety signal).
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
