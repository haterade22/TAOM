"""Which of TAOM's banner icon atlases do the campaign's banners use? (read-only)

Usage: banner_atlas_use.py <TAOM repo root> [<extra ModuleData dir>...]

Reads every banner key in TAOM's ModuleData (and any extra ModuleData dirs, for example the live
TAOM_Map), both banner_key="..." attributes and XSLT <xsl:attribute name="banner_key"> overrides, splits each key into its 10-number layers (mesh id, two colours, size, position, stroke, mirror,
rotation), and maps every icon layer's mesh id to its atlas through TAOM's banner_icons.xml. Prints, per
source file, how many keys and which atlases they need, and the union: the atlases a campaign that shows
all of these banners must load, at 64 MiB each today (banner-atlases.md).
"""
import collections
import glob
import os
import re
import sys

ROOT = sys.argv[1]
DATA_DIRS = [os.path.join(ROOT, "Main", "_Module", "ModuleData")] + sys.argv[2:]
ICONS = os.path.join(ROOT, "Main", "_Module", "ModuleData", "banner_icons.xml")
ATLAS_MIB = 64

icon_atlas = {}
for m in re.finditer(r'<Icon\s+id="(\d+)"\s+material_name="([^"]+)"', open(ICONS, encoding="utf-8").read()):
    icon_atlas[int(m.group(1))] = m.group(2)
atlases = sorted(set(icon_atlas.values()))
print(f"banner_icons.xml: {len(icon_atlas)} icons on {len(atlases)} atlases")

per_file = collections.OrderedDict()
union = collections.Counter()
unknown_icons = collections.Counter()
for d in DATA_DIRS:
    for path in sorted(glob.glob(os.path.join(d, "**", "*.xml"), recursive=True)
                       + glob.glob(os.path.join(d, "**", "*.xslt"), recursive=True)):
        text = open(path, encoding="utf-8", errors="replace").read()
        # Both forms: the XML attribute, and an XSLT override's <xsl:attribute name="banner_key">. A key's
        # numbers can be negative (a rotation of -44).
        keys = re.findall(r'banner_key="([0-9.\-]+)"', text) + re.findall(
            r'<xsl:attribute\s+name="banner_key"\s*>\s*([0-9.\-]+)\s*</xsl:attribute>', text)
        carriers = len(re.findall(r'banner_key="', text)) + len(
            re.findall(r'<xsl:attribute\s+name="banner_key"', text))
        if carriers != len(keys):
            print(f"WARNING {path}: {carriers} banner_key carriers, {len(keys)} parsed")
        if not keys:
            continue
        need = collections.Counter()
        for key in keys:
            nums = [int(x) for x in key.split(".") if x]
            for layer in range(1, len(nums) // 10):
                mesh = nums[layer * 10]
                if mesh in icon_atlas:
                    need[icon_atlas[mesh]] += 1
                elif mesh >= 10000:
                    unknown_icons[mesh] += 1
        name = os.path.relpath(path, d)
        per_file[(d, name)] = (len(keys), need)
        union.update(need)

for (d, name), (n, need) in per_file.items():
    print(f"\n{os.path.basename(os.path.dirname(d)) or d}/{name}: {n} banner keys, {len(need)} TAOM atlases")
    for atlas, c in need.most_common():
        print(f"   {c:4d}  {atlas}")
print(f"\nUnion: {len(union)} of {len(atlases)} atlases, {len(union) * ATLAS_MIB} MiB at today's format "
      f"({len(union) * 16} MiB as 4096 BC7, {len(union) * 4} MiB as 2048 BC7)")
print("Never used by any banner key here:", [a for a in atlases if a not in union])
if unknown_icons:
    print("Icon ids >= 10000 with no banner_icons.xml entry:", dict(unknown_icons.most_common(10)))
