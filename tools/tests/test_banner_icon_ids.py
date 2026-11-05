"""Every icon and colour a shipped clan, kingdom or culture banner key names must exist (#951).

A banner key is groups of ten numbers: background, then one group per icon layer. The first number
of every group after the first is a mesh id from a banner_icons.xml, and the colours sit at offsets
1 and 2 of a group (offset 2 only counts when the layer's flag at offset 7 is 1). The engine draws
an unknown icon as nothing and an unknown colour as 0xDEADBEEF, so a donated key can render a blank
emblem or a garish one while ShippedHeraldryBannerKeyTests, which checks only the key's shape,
stays green. clan_mirkwood_5 and _6 shipped that way.

NavalDLC is deliberately not read: TAOM declares it an incompatible module (SubModule.xml), so an
id only it defines does not exist in any session TAOM runs in.
"""
import re
import sys
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
TOOLS = REPO / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from _gamedir import game_dir  # noqa: E402

MD = REPO / "Main" / "_Module" / "ModuleData"
GAME = Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))
NATIVE_ICONS = GAME / "Modules" / "Native" / "ModuleData" / "banner_icons.xml"
ICON_FILES = [NATIVE_ICONS, MD / "banner_icons.xml"]
KEY_FILES = [MD / "characters" / "clans.xml", MD / "spclans.xslt", MD / "taom_spkingdoms.xml",
             MD / "spkingdoms.xslt", MD / "taom_spcultures.xml"]

COMMENT_RE = re.compile(r"<!--.*?-->", re.S)
ICON_RE = re.compile(r'<Icon\s+id="(\d+)"')
# <Color id="12" hex="0xff..."> may break across lines between attributes (Native does).
COLOR_RE = re.compile(r'<Color\b[^>]*?\bid="(\d+)"')
# A rotation (the last number of a layer) can be negative.
NUM = r"-?\d+(?:\.-?\d+)*"
# A row carries banner_key="..." / faction_banner_key="..." (XML) or
# <xsl:attribute name="banner_key">...</xsl:attribute> (XSLT).
KEY_RE = re.compile(rf'banner_key="({NUM})"|name="(?:faction_)?banner_key"\s*>\s*({NUM})\s*<')


# Every place a key is declared, whether or not KEY_RE can read its value. An XSLT's
# local-name() != 'banner_key' exclusion filters are not declarations and use single quotes.
DECLARATION_RE = re.compile(r'banner_key\s*=\s*"|name\s*=\s*"(?:faction_)?banner_key"')


def read(path):
    # Blank a comment but keep its newlines, so a reported line number is the file's own.
    text = path.read_bytes().decode("utf-8-sig", errors="replace")
    return COMMENT_RE.sub(lambda m: "\n" * m.group(0).count("\n"), text)


def parse_keys(text):
    """[(key, line)] for every banner key in `text` (comments already stripped)."""
    return [(m.group(1) or m.group(2), text.count("\n", 0, m.start()) + 1) for m in KEY_RE.finditer(text)]


def unknown_icons(key, icons):
    parts = key.split(".")
    return [parts[i] for i in range(10, len(parts), 10) if parts[i] not in icons]


def unknown_colours(key, colours):
    """Colour ids a key names that no palette defines.

    Layer colour1 is offset i+1; colour2 (i+2) is read only when the layer's flag at i+7 is 1. The
    background's second colour is skipped: vanilla's own spcultures keys put 124 there, an id no
    palette defines, and it never draws."""
    parts = key.split(".")
    bad = []
    if len(parts) > 1 and parts[1] not in colours:
        bad.append(parts[1])
    for i in range(10, len(parts) - 7, 10):
        names = [parts[i + 1]] + ([parts[i + 2]] if parts[i + 7] == "1" else [])
        bad += [c for c in names if c not in colours]
    return bad


class BannerKeyHelperTests(unittest.TestCase):
    """Pure helpers: run with or without the game install."""

    KEY = "11.1.1.1528.1528.764.764.1.0.0.505.1.1.1.1.1.1.0.0.0.99999.1.1.1.1.1.1.0.0.0"

    def test_unknown_icons_reports_only_missing_layers(self):
        self.assertEqual(unknown_icons(self.KEY, {"505"}), ["99999"])

    def test_negative_rotation_key_is_parsed_in_xml_and_xslt_form(self):
        key = "11.1.1.1528.1528.764.764.1.0.0.505.1.1.1.1.1.1.0.0.-45"
        xml = f'<Faction id="a" banner_key="{key}" />\n<Culture id="b" faction_banner_key="{key}" />'
        xslt = f'<xsl:attribute name="banner_key">{key}</xsl:attribute>\n<xsl:attribute name="banner_key">\n  {key}\n</xsl:attribute>'
        self.assertEqual([k for k, _ in parse_keys(xml)], [key, key])
        self.assertEqual([k for k, _ in parse_keys(xslt)], [key, key])
        self.assertEqual([line for _, line in parse_keys(xml)], [1, 2])

    def test_unknown_colours_checks_layer_colour1_and_flagged_colour2(self):
        # Layer at offset 10: icon 505, colour1 7, colour2 8, flag 0 -> colour2 never read.
        key = "11.1.99.1528.1528.764.764.1.0.0.505.7.8.1.1.1.1.0.0.0"
        self.assertEqual(unknown_colours(key, {"1", "7"}), [])
        # The same layer with flag 1 reads colour2.
        flagged = "11.1.99.1528.1528.764.764.1.0.0.505.7.8.1.1.1.1.1.0.0"
        self.assertEqual(unknown_colours(flagged, {"1", "7"}), ["8"])
        self.assertEqual(unknown_colours(flagged, {"1", "8"}), ["7"])

    def test_background_second_colour_is_not_checked(self):
        self.assertEqual(unknown_colours("11.1.124.1528.1528.764.764.1.0.0", {"1"}), [])
        self.assertEqual(unknown_colours("11.2.124.1528.1528.764.764.1.0.0", {"1"}), ["2"])


@unittest.skipUnless(NATIVE_ICONS.is_file(), f"Native banner_icons.xml not found under {GAME}")
class ShippedBannerDataTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.icons, cls.colours = set(), set()
        for f in ICON_FILES:
            if f.is_file():
                text = read(f)
                cls.icons |= set(ICON_RE.findall(text))
                cls.colours |= set(COLOR_RE.findall(text))
        cls.texts = {f: read(f) for f in KEY_FILES}
        cls.keys = {f: parse_keys(t) for f, t in cls.texts.items()}

    def test_registries_are_not_suspiciously_small(self):
        self.assertGreater(len(self.icons), 500, "icon registry suspiciously small: the parse found too few icons")
        self.assertGreater(len(self.colours), 100, "colour registry suspiciously small: the parse found too few colours")

    def test_every_banner_key_occurrence_is_parsed(self):
        for f, text in self.texts.items():
            with self.subTest(file=f.name):
                self.assertEqual(len(self.keys[f]), len(DECLARATION_RE.findall(text)),
                                 f"{f.name}: a banner_key the pattern does not parse would pass unchecked")

    def test_enough_keys_are_checked(self):
        self.assertGreater(sum(len(k) for k in self.keys.values()), 100,
                           "found too few banner keys: the key pattern no longer matches the data")

    def test_every_shipped_banner_icon_exists(self):
        bad = [f"{f.relative_to(REPO)}:{line} unknown icon(s) {sorted(set(missing))}"
               for f, keys in self.keys.items() for key, line in keys
               if (missing := unknown_icons(key, self.icons))]
        self.assertEqual(bad, [], "banner keys naming icons no banner_icons.xml defines:\n" + "\n".join(bad))

    def test_every_shipped_banner_colour_exists(self):
        bad = [f"{f.relative_to(REPO)}:{line} unknown colour(s) {sorted(set(missing))}"
               for f, keys in self.keys.items() for key, line in keys
               if (missing := unknown_colours(key, self.colours))]
        self.assertEqual(bad, [], "banner keys naming colours no banner_icons.xml defines:\n" + "\n".join(bad))


if __name__ == "__main__":
    unittest.main()
