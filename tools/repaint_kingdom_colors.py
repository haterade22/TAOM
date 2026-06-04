#!/usr/bin/env python3
"""Repaint the 8 vanilla-renamed (XSLT) kingdoms' troop-tint color/color2 to a
LOTR-lore palette in Main/_Module/ModuleData/spkingdoms.xslt.

Background: a campaign troop's grayscale-armor cloth tint = its Team color =
party.MapFaction.Color, and for a kingdom-bound clan MapFaction resolves to the
KINGDOM (verified Mission.cs:4422 -> PartyBase.PrimaryColorPair -> Clan.MapFaction).
So a kingdom's color/color2 is the real lever for that faction's troop armor.
The 8 vanilla kingdom ids below are renamed to LOTR factions by spkingdoms.xslt;
this script repaints only their color/color2 (NOT banner colors / banner_key).

Usage:
    python tools/repaint_kingdom_colors.py            # dry-run (default)
    python tools/repaint_kingdom_colors.py --apply    # writes file + .bak
"""
import argparse
import os
import re
import sys

XSLT = os.path.join(os.path.dirname(__file__), "..", "Main", "_Module", "ModuleData", "spkingdoms.xslt")

# kingdom_id -> (color, color2, faction, rationale)
# color  = primary armor cloth tint (dominant)
# color2 = secondary accent tint
PALETTE = {
    "empire":   ("FF5A3D28", "FF8C3A2C", "Dunland", "earthen brown + rust-red (hill-men furs/leather, blood-feud)"),
    "empire_w": ("FFB4B9C2", "FF1C1C22", "Gondor",  "steel-silver + black (silver plate, black surcoat / White Tree)"),
    "empire_s": ("FF1A1717", "FF7E1518", "Mordor",  "black + blood-red (black armour, Red Eye of Sauron)"),
    "sturgia":  ("FF1E3A6E", "FFD4A53A", "Dale",    "deep blue + gold (Esgaroth / Bard's Dale)"),
    "aserai":   ("FF8E1C1C", "FFC9A227", "Harad",   "crimson + gold (Haradrim scarlet, Serpent banners)"),
    "vlandia":  ("FF35632F", "FFE0D6A8", "Rohan",   "green + straw-cream (the Mark, white horse on green)"),
    "battania": ("FF8A5A1E", "FF5A1E18", "Khand",   "bronze + dark-red (Variag horse-lords of Khand)"),
    "khuzait":  ("FFC0962E", "FF5A1A14", "Rhun",    "gold + dark-crimson (golden Easterling armour)"),
}


def repaint(text, kid, c1, c2):
    """Replace color/color2 inside the OUTPUT <Kingdom ... id="kid" ...> start tag only."""
    # The start tag runs from '<Kingdom' to the first '>' (attributes span many lines, no '>' until close).
    tag_re = re.compile(r'(<Kingdom\b[^>]*?\bid="%s"[^>]*?>)' % re.escape(kid), re.S)

    def fix(m):
        tag = m.group(1)
        before = (re.search(r'\bcolor="([^"]+)"', tag), re.search(r'\bcolor2="([^"]+)"', tag))
        tag = re.sub(r'\bcolor="[^"]+"', 'color="%s"' % c1, tag, count=1)
        tag = re.sub(r'\bcolor2="[^"]+"', 'color2="%s"' % c2, tag, count=1)
        fix.old = (before[0].group(1) if before[0] else "?", before[1].group(1) if before[1] else "?")
        return tag

    new_text, n = tag_re.subn(fix, text)
    if n != 1:
        raise SystemExit("ERROR: kingdom id=%r matched %d output start-tags (expected 1)" % (kid, n))
    return new_text, fix.old


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="write changes (+ .bak); default is dry-run")
    args = ap.parse_args()

    path = os.path.abspath(XSLT)
    with open(path, "r", encoding="utf-8-sig", newline="") as f:
        text = f.read()
    original = text

    print("Repainting 8 XSLT kingdom troop-tint colors (color / color2):\n")
    for kid, (c1, c2, faction, why) in PALETTE.items():
        text, (old1, old2) = repaint(text, kid, c1, c2)
        print("  %-9s %-8s color  %s -> %s" % (kid, faction, old1, c1))
        print("  %-9s %-8s color2 %s -> %s   (%s)" % ("", "", old2, c2, why))
    print()

    if not args.apply:
        print("DRY-RUN. Re-run with --apply to write %s" % path)
        return

    with open(path + ".bak", "w", encoding="utf-8-sig", newline="") as f:
        f.write(original)
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        f.write(text)
    print("WROTE %s (backup: %s.bak)" % (path, path))


if __name__ == "__main__":
    main()
