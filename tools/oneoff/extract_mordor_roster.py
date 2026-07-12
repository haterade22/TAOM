# -*- coding: utf-8 -*-
"""Extract the CURRENT Mordor roster (culture=Culture.mordor) from live data.

Reads lords.xslt (canonical lord_1_* templates) + characters/lords.xml
(children, M/SE lords). Prints id, name, race (default 'human' when no race
attribute) for every Mordor-cultured lord. Used to regenerate mordor-lords.html
without hand-counting.
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MD = ROOT / "Main" / "_Module" / "ModuleData"

roster = {}  # id -> (name, race)

# lords.xslt: per-template blocks
xslt = (MD / "lords.xslt").read_text(encoding="utf-8")
for block in re.findall(r"<xsl:template match=\"NPCCharacter\[@id='[^']+'\]\">.*?</xsl:template>", xslt, re.DOTALL):
    if 'Culture.mordor' not in block:
        continue
    lid = re.search(r"name=\"id\">([^<]+)</", block)
    name = re.search(r"name=\"name\">(?:\{=[^}]+\})?([^<]+)</", block)
    race = re.search(r"name=\"race\">([^<]+)</", block)
    if not lid:
        continue
    roster[lid.group(1)] = (name.group(1).strip() if name else "?", race.group(1).strip() if race else "human")

# characters/lords.xml: single-line NPCCharacter
xml = (MD / "characters" / "lords.xml").read_text(encoding="utf-8")
for line in xml.splitlines():
    if 'culture="Culture.mordor"' not in line:
        continue
    lid = re.search(r'id="([^"]+)"', line)
    name = re.search(r'name="(?:\{=[^}]+\})?([^"]+)"', line)
    race = re.search(r'race="([^"]+)"', line)
    if not lid:
        continue
    # characters/lords.xml is the later registration and carries the AUTHORED race
    # (lords.xslt rebuilds the same child id without a race attr); honor the authored value.
    roster[lid.group(1)] = (name.group(1).strip() if name else "?", race.group(1).strip() if race else "human")

by_race = {}
for lid, (name, race) in roster.items():
    by_race.setdefault(race, []).append((lid, name))

print(f"TOTAL Mordor lords: {len(roster)}")
for race in sorted(by_race):
    rows = sorted(by_race[race])
    print(f"\n== {race} ({len(rows)}) ==")
    for lid, name in rows:
        print(f"  {lid:16s} {name}")
