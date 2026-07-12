"""Re-culture specific lords (general culture-vs-faction fixes).

Driven by an explicit {lord_id: (from_culture, to_culture)} map. Applies to:
  - lords.xslt: the lord's <xsl:template> block (replaces Culture.<from> -> Culture.<to>)
  - characters/lords.xml: the lord's standalone <NPCCharacter> line (culture attr)

Only blocks/lines that currently hold Culture.<from> are touched; a target that is
already <to> (or absent) is reported and skipped.

Usage:
  python tools/reculture_lords.py --dry-run
  python tools/reculture_lords.py --apply
"""
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LORDS_XSLT = ROOT / "Main" / "_Module" / "ModuleData" / "lords.xslt"
LORDS_XML = ROOT / "Main" / "_Module" / "ModuleData" / "characters" / "lords.xml"

# lord_id -> (from_culture, to_culture)
RECULTURE = {
    # Khamul (Dol Guldur king) + his 3 Nazgul sub-lords: mordor -> dolguldur.
    "lord_1_48": ("mordor", "dolguldur"),
    "lord_1_48_1": ("mordor", "dolguldur"),
    "lord_1_48_2": ("mordor", "dolguldur"),
    "lord_1_48_3": ("mordor", "dolguldur"),
    # Strays carrying Culture.empire in non-Dunland clans (copy-paste errors).
    "lord_5_6": ("empire", "battania"),      # Alcaea - Khand clan
    "lord_WE9_l_1": ("empire", "gondor"),    # Duilin, Lord of Morthond - Gondor clan
}


def patch_xslt(text):
    changes = []
    for lid, (src, dst) in RECULTURE.items():
        m = re.search(
            r"(<xsl:template match=\"NPCCharacter\[@id='" + re.escape(lid) + r"'\]\">.*?</xsl:template>)",
            text, re.DOTALL,
        )
        if not m:
            continue  # not in this file; fine
        block = m.group(1)
        token = f"Culture.{src}"
        n = block.count(token)
        if n == 0:
            changes.append((lid, f"lords.xslt: no Culture.{src} (skipped)", 0))
            continue
        text = text[: m.start(1)] + block.replace(token, f"Culture.{dst}") + text[m.end(1):]
        changes.append((lid, f"lords.xslt: Culture.{src} -> Culture.{dst}", n))
    return text, changes


def patch_xml(text):
    changes = []
    lines = text.splitlines(keepends=True)
    for i, line in enumerate(lines):
        for lid, (src, dst) in RECULTURE.items():
            if f'id="{lid}"' in line and f'culture="Culture.{src}"' in line:
                lines[i] = line.replace(f'culture="Culture.{src}"', f'culture="Culture.{dst}"')
                changes.append((lid, f"characters/lords.xml: culture {src} -> {dst}", 1))
    return "".join(lines), changes


def main():
    ap = argparse.ArgumentParser()
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--dry-run", action="store_true")
    g.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    xslt = LORDS_XSLT.read_text(encoding="utf-8")
    xml = LORDS_XML.read_text(encoding="utf-8")
    new_xslt, c1 = patch_xslt(xslt)
    new_xml, c2 = patch_xml(xml)

    for lid, msg, n in c1 + c2:
        flag = "  " if n else "!!"
        print(f"  {flag} {lid:16s} {msg}")

    # Report any target not touched in EITHER file.
    touched = {lid for lid, _, n in c1 + c2 if n}
    missing = [lid for lid in RECULTURE if lid not in touched]
    total = sum(n for _, _, n in c1 + c2)
    print(f"\nTotal culture replacements: {total}")
    if missing:
        print(f"WARNING: no change applied for: {', '.join(missing)}")

    if args.apply:
        LORDS_XSLT.write_text(new_xslt, encoding="utf-8")
        LORDS_XML.write_text(new_xml, encoding="utf-8")
        print("APPLIED.")
    else:
        print("DRY RUN - no files written.")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
