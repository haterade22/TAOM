"""The sprite-to-icon half of the banner audit (#1274), without the image and hash libraries.

pack_banner_sheets imports without numpy, Pillow and xxhash (main() reports them), so the orphan rule and the
repo's own sprite data are checked here even where test_pack_banner_sheets.py skips.

Run:  python -m unittest tools.tests.test_banner_sprite_orphans
"""
import sys
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parent.parent
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import pack_banner_sheets as pbs  # noqa: E402


class OrphanSpritesTests(unittest.TestCase):
    def test_every_sprite_an_icon_names_is_no_finding(self):
        self.assertEqual(([], []), pbs.orphan_sprites({"1", "2"}, {"1", "2"}, frozenset()))

    def test_a_sprite_no_icon_names_is_an_orphan(self):
        self.assertEqual((["3"], []), pbs.orphan_sprites({"1", "2"}, {"1", "2", "3"}, frozenset()))

    def test_an_icon_without_a_sprite_is_not_an_orphan(self):
        # missing_sprite reports that side.
        self.assertEqual(([], []), pbs.orphan_sprites({"1", "2"}, {"1"}, frozenset()))

    def test_a_known_orphan_is_held_back_while_it_is_an_orphan(self):
        self.assertEqual(([], []), pbs.orphan_sprites({"1"}, {"1", "3"}, frozenset({"3"})))

    def test_a_known_orphan_that_is_no_orphan_any_more_is_stale(self):
        self.assertEqual(([], ["3"]), pbs.orphan_sprites({"1"}, {"1"}, frozenset({"3"})))
        self.assertEqual(([], ["1"]), pbs.orphan_sprites({"1"}, {"1"}, frozenset({"1"})))

    def test_ids_come_back_sorted(self):
        self.assertEqual((["10", "9"], []), pbs.orphan_sprites(set(), {"9", "10"}, frozenset()))


class ShippedSpriteDataTests(unittest.TestCase):
    """The repo's TAOMSpriteData.xml against banner_icons.xml. PNGs are left to the full audit, since a sparse
    checkout may hold none of them."""

    def test_the_known_orphans_are_exactly_the_repo_sprite_parts_no_icon_names(self):
        icons = {i for i, _, _ in pbs.read_icons((pbs.REPO_MODULE / pbs.XML).read_bytes())}
        parts = pbs.sprite_parts(pbs.REPO_MODULE)
        self.assertGreater(len(parts), 100, "the banner sprite parts did not load")
        self.assertEqual(sorted(pbs.KNOWN_ORPHAN_SPRITES), sorted(parts - icons))

    def test_the_known_orphan_list_is_pinned(self):
        # 22004: a picker sprite with no icon row since March 2026; drop it when the Kit regeneration removes it.
        self.assertEqual(frozenset({"22004"}), pbs.KNOWN_ORPHAN_SPRITES)


if __name__ == "__main__":
    unittest.main()
