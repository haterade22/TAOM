"""Faction screen emblems drawn from each kingdom's banner sigil.

Run:  python -m pytest tools/tests/test_make_faction_emblems.py -q

The faction screen shows fs_emblem_<faction key> and falls back to a star when the file is missing.
make_faction_emblems.py writes the emblem for a fixed list of factions from the first layer of
the kingdom's banner_key that has a TAOM sprite, coloured by the faction's side.
"""
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parent.parent.parent
TOOLS = REPO / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import make_faction_emblems as mfe  # noqa: E402


class SideColourTests(unittest.TestCase):
    def test_each_side_has_the_shipped_emblem_colour(self):
        self.assertEqual(mfe.side_colour("free"), (236, 228, 206))
        self.assertEqual(mfe.side_colour("neutral"), (214, 186, 112))
        self.assertEqual(mfe.side_colour("evil"), (206, 94, 82))

    def test_unknown_side_is_refused(self):
        with self.assertRaises(ValueError):
            mfe.side_colour("chaotic")


class SigilTests(unittest.TestCase):
    def test_sigil_is_the_layer_one_icon_of_the_banner_key(self):
        key = "11.2027.2027.1528.1528.764.764.1.0.0.31005.2004.2004.700.700.764.764.0.0.0"
        self.assertEqual(mfe.sigil_icon(key, sprites={"31005"}), "31005")

    def test_a_key_without_a_second_layer_is_refused(self):
        with self.assertRaises(ValueError):
            mfe.sigil_icon("11.2027.2027.1528.1528.764.764.1.0.0", sprites={"31005"})

    # A layered banner (72715c417, Lindon) puts vanilla shapes (icon 505) under the TAOM sigil.
    LAYERED = ("11.2017.2017.1528.1528.764.764.1.0.0.505.2017.2017.900.900.764.764.0.0.0."
               "505.2018.2018.800.800.764.764.0.0.0.15000.2018.2018.483.483.764.764.0.0.0")

    def test_the_sigil_is_the_first_layer_with_a_taom_sprite(self):
        self.assertEqual(mfe.sigil_icon(self.LAYERED, sprites={"15000"}), "15000")

    def test_a_key_with_no_taom_sprite_layer_is_refused(self):
        with self.assertRaises(ValueError):
            mfe.sigil_icon(self.LAYERED, sprites={"31005"})


class TargetTests(unittest.TestCase):
    def test_only_listed_factions_are_written(self):
        self.assertEqual(
            set(mfe.FACTIONS),
            {"high_kingdom_of_lindon", "kingdom_of_arthedain", "kingdom_of_cardolan",
             "kingdom_of_rhudaur", "kingdom_of_ered_duin"})

    def test_a_faction_outside_the_list_is_refused(self):
        with self.assertRaises(ValueError):
            mfe.build_emblems(only=["kingdom_of_gondor"])


class ShippedDataTests(unittest.TestCase):
    def test_every_listed_faction_resolves_to_a_256_emblem(self):
        emblems = mfe.build_emblems()
        self.assertEqual(set(emblems), set(mfe.FACTIONS))
        for key, image in emblems.items():
            self.assertEqual(image.size, (256, 256), key)
            self.assertEqual(image.mode, "RGBA", key)
            self.assertIsNotNone(image.getchannel("A").getbbox(), key)

    def test_the_shipped_emblems_match_the_shipped_banners(self):
        self.assertEqual(mfe.main(["--check"]), 0)

    def test_check_is_clean_after_apply(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp)
            self.assertNotEqual(mfe.main(["--check", "--out", str(out)]), 0)
            self.assertEqual(mfe.main(["--apply", "--out", str(out)]), 0)
            self.assertEqual(mfe.main(["--check", "--out", str(out)]), 0)
            sample = Image.open(out / "fs_emblem_kingdom_of_cardolan.png").convert("RGBA")
            opaque = [p for _, p in sample.getcolors(maxcolors=256 * 256) if p[3] == 255]
            self.assertTrue(opaque)
            self.assertTrue(all(p[:3] == (214, 186, 112) for p in opaque))


if __name__ == "__main__":
    unittest.main()
