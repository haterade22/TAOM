#!/usr/bin/env python3
"""
Add a gold band and a red band to the end of the `sauron` race's eye colour slider in the live
LOTRLOME_Armory skins.xml (Mike, 2026-09-28: "Put in Sauron", so Sauron can be remade on the elf head with red or
gold eyes, the way Saruman's gradient ends in red).

Only the `sauron` race is touched, never `elf`: the eye slider is stored per character as a position along the
gradient, so points added to the elf race would likely shift every existing elf's eye colour. Each colour is added
twice, as Saruman's red is, so the slider's end holds a flat band of it rather than a single point.

Dry run by default (prints what it would add per skin); `--apply` writes a `.bak-sauron-eyes-<stamp>` backup (never
an .xml extension: the folder is globbed), then the file, byte-faithful (LF kept, no BOM added), after checking
the result parses. Idempotent: a skin whose gradient already ends in these points is skipped.

    python tools/oneoff/add_sauron_eye_colours.py [--skins <skins.xml>] [--apply]
"""
import argparse
import datetime
import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET

DEFAULT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\skins.xml"
RACE = "sauron"
GOLD = "1.00, 0.72, 0.08"
RED = "1.00, 0.01, 0.014"
ADDED = [("Gold", GOLD), ("Gold", GOLD), ("Red", RED), ("Red", RED)]
CLOSE = "</eye_color_gradient_points>"


def race_span(text, race):
    """(start, end) offsets of the <race> element whose id is `race`."""
    m = re.search(r'<race\s+id="%s"\s*>' % re.escape(race), text)
    if not m:
        raise SystemExit("no <race id=%r> in the file" % race)
    end = text.find("</race>", m.end())
    if end < 0:
        raise SystemExit("race %r is not closed" % race)
    return m.start(), end


def block(indent, nl):
    lines = []
    for name, value in ADDED:
        lines.append("%s<!-- %s -->%s" % (indent, name, nl))
        lines.append('%s<eye_color_gradient_point point="%s" />%s' % (indent, value, nl))
    return "".join(lines)


def already(segment):
    """True when this gradient already ends with the added points."""
    pts = re.findall(r'point="([^"]+)"', segment)
    return pts[-len(ADDED):] == [v for _, v in ADDED]


def plan(text, race=RACE):
    """The new text and the number of skins changed and skipped."""
    start, end = race_span(text, race)
    nl = "\r\n" if "\r\n" in text[:4096] else "\n"
    out, pos, changed, skipped = [], 0, 0, 0
    region = text[start:end]
    for m in re.finditer(r"<eye_color_gradient_points>(.*?)([ \t]*)" + re.escape(CLOSE), region, re.S):
        seg = m.group(1)
        if already(seg):
            skipped += 1
            continue
        insert_at = start + m.start(2)
        indent = m.group(2) + "\t"
        out.append(text[pos:insert_at])
        out.append(block(indent, nl))
        pos = insert_at
        changed += 1
    out.append(text[pos:])
    return "".join(out), changed, skipped


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--skins", default=DEFAULT)
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args(argv)
    raw = open(args.skins, "rb").read()
    text = raw.decode("utf-8")
    new, changed, skipped = plan(text)
    print("race %s: %d skin gradient(s) to extend, %d already extended" % (RACE, changed, skipped))
    if not changed:
        return 0
    ET.fromstring(new.lstrip("\ufeff").encode("utf-8"))
    if not args.apply:
        print("dry run; pass --apply to write")
        return 0
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    backup = "%s.bak-sauron-eyes-%s" % (args.skins, stamp)
    shutil.copy2(args.skins, backup)
    open(args.skins, "wb").write(new.encode("utf-8"))
    print("wrote %s (backup %s)" % (args.skins, os.path.basename(backup)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
