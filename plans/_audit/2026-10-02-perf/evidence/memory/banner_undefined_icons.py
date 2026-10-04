"""List every banner key whose icon layers name an id that no module's banner_icons.xml defines (read-only).

Usage: banner_undefined_icons.py <TAOM repo root> <game Modules dir>

The engine resolves an unknown id to an empty icon record and skips that layer when it builds the
banner (v1.5.3 BannerManager.GetIconDataFromIconId returns default; BannerVisual.cs:120-122 draws a layer
only when its material resolves), so such a banner draws without that emblem, silently.
"""
import glob
import os
import re
import sys

ROOT, MODS = sys.argv[1], sys.argv[2]
DATA = os.path.join(ROOT, "Main", "_Module", "ModuleData")

defined = set()
for p in glob.glob(os.path.join(MODS, "*", "ModuleData", "banner_icons.xml")) + [os.path.join(DATA, "banner_icons.xml")]:
    text = open(p, encoding="utf-8-sig", errors="replace").read()
    defined |= {int(m) for m in re.findall(r"<(?:Icon|Background)\s[^>]*?\bid=\"(\d+)\"", text, re.S)}
print(f"defined icon and background ids across modules: {len(defined)}")

KEY = re.compile(r'banner_key="([0-9.\-]+)"|<xsl:attribute\s+name="banner_key"\s*>\s*([0-9.\-]+)\s*<')
OWNER = re.compile(r'\bid="([^"]+)"|@id\s*=\s*\'([^\']+)\'|@id\s*=\s*"([^"]+)"')
for path in sorted(glob.glob(os.path.join(DATA, "**", "*.xml"), recursive=True)
                   + glob.glob(os.path.join(DATA, "**", "*.xslt"), recursive=True)):
    text = open(path, encoding="utf-8", errors="replace").read()
    for m in KEY.finditer(text):
        nums = [int(x) for x in (m.group(1) or m.group(2)).split(".") if x]
        layers = [nums[i * 10] for i in range(1, len(nums) // 10)]
        missing = [x for x in layers if x not in defined]
        if not missing:
            continue
        owners = OWNER.findall(text[max(0, m.start() - 3000):m.start()])
        owner = next((a or b or c for a, b, c in reversed(owners)), "?")
        line = text.count("\n", 0, m.start()) + 1
        print(f"{os.path.relpath(path, DATA)}:{line} {owner}: {len(missing)} of {len(layers)} icon layers "
              f"undefined {sorted(set(missing))}")
