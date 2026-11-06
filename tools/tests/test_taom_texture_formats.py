#!/usr/bin/env python3
"""Pins the compiled form of TAOM's own textures (plans/121; ports of #913 and #1047).

Reads the repository's Main/_Module/Assets descriptors and GUI/TAOMSpriteData.xml with the decoders
in tools/audit_map_scene_memory.py: never a pixel, never the game install. The repo never tracks
pack0.tpac, so a published pack is checked by pointing TAOM_PACK0 at it; those tests skip otherwise.

Run:   python -m unittest discover -s tools/tests -t . -p "test_taom_texture_formats.py" -v
Pack:  TAOM_PACK0="<publish folder>/TAOM/AssetPackages/pack0.tpac" python -m unittest discover -s tools/tests -t . -p "test_taom_texture_formats.py" -v
"""
import glob
import os
import re
import sys
import unittest
import xml.etree.ElementTree as ET

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)

import audit_map_scene_memory as amsm  # noqa: E402

MODULE = os.path.join(os.path.dirname(TOOLS), "Main", "_Module")
PACK0 = os.environ.get("TAOM_PACK0", "").strip()
SHEET = re.compile(r"(.+)_(\d+)")

# Expected (format, mips) per UI sheet. docs/reference/banner-icon-generation.md Step 4b keeps the
# banner icon sprite sheets uncompressed with one mip, for a clean white sigil alpha.
UI_FORMATS = {
    "ui_taom_bannericons_1": ("R8G8B8A8_UNORM", 1),
    "ui_taom_bannericons_2": ("R8G8B8A8_UNORM", 1),
}


def decode(paths, kind="texture"):
    """{item name: TextureInfo (textures) or None (other kinds)} for every item of `kind`."""
    found = {}
    for path in paths:
        for item in amsm.iter_tpac_items(path, "TAOM"):
            if item.kind == kind:
                found[item.name] = amsm.parse_texture_meta(item.meta) if kind == "texture" else None
    return found


def textures_in(folder):
    return decode(sorted(glob.glob(os.path.join(MODULE, "Assets", folder, "*_tex.tpac"))))


def repo_items(pattern, kind):
    return decode(sorted(glob.glob(os.path.join(MODULE, "Assets", "**", pattern), recursive=True)), kind)


def declared_sheets():
    root = ET.parse(os.path.join(MODULE, "GUI", "TAOMSpriteData.xml")).getroot()
    return {c.findtext("Name"): int(c.findtext("SpriteSheetCount")) for c in root.iter("SpriteCategory")}


def is_declared(stem, counts):
    m = SHEET.fullmatch(stem)
    return bool(m) and 1 <= int(m.group(2)) <= counts.get(m.group(1), 0)


def ui_problems(tex):
    out = []
    for name, (fmt, mips) in sorted(UI_FORMATS.items()):
        t = tex.get(name)
        if t is None:
            out.append(f"{name}: missing")
        elif (t.fmt, t.mips) != (fmt, mips):
            out.append(f"{name}: {t.fmt} with {t.mips} mips, want {fmt} with {mips}")
    return out


class SpriteSheetFileTests(unittest.TestCase):
    """v1.4.8 SpriteCategory.Load asks for sheets 1 to SpriteSheetCount and no others, so any other
    sheet file is dead weight in every pack the Kit publishes (#1047 found 39)."""

    maxDiff = None

    def test_every_gauntlet_sheet_file_is_a_declared_sheet(self):
        counts = declared_sheets()
        stray = []
        for folder, suffix in (("Assets", "_tex.tpac"), ("AssetSources", ".png")):
            root = os.path.join(MODULE, folder, "GauntletUI")
            for name in sorted(os.listdir(root)):
                if not (name.endswith(suffix) and is_declared(name[: -len(suffix)], counts)):
                    stray.append(f"{folder}/GauntletUI/{name}")
        self.assertEqual([], stray, "files TAOMSpriteData.xml does not declare as a sheet")


class UiSheetFormatTests(unittest.TestCase):
    maxDiff = None

    def test_ui_sheets_keep_their_pinned_format(self):
        self.assertEqual([], ui_problems(textures_in("GauntletUI")))


@unittest.skipUnless(PACK0, "set TAOM_PACK0 to a published pack0.tpac to check it")
class PublishedPackTests(unittest.TestCase):
    """What players load. A publish from an install whose descriptors and RuntimeDataCache disagree,
    or that still holds a removed sheet, shows up here and nowhere else."""

    maxDiff = None

    @classmethod
    def setUpClass(cls):
        cls.textures = decode([PACK0])
        cls.materials = decode([PACK0], kind="material")

    def test_pack_holds_exactly_the_repo_textures_and_materials(self):
        self.assertEqual(sorted(repo_items("*_tex.tpac", "texture")), sorted(self.textures))
        self.assertEqual(sorted(repo_items("*_mtl.tpac", "material")), sorted(self.materials))

    def test_pack_ui_sheets_keep_their_pinned_format(self):
        self.assertEqual([], ui_problems(self.textures))

    def test_pack_has_no_undeclared_sheet(self):
        counts = declared_sheets()
        self.assertEqual([], sorted(n for n in self.textures if n.startswith("ui_") and not is_declared(n, counts)))


if __name__ == "__main__":
    unittest.main()
