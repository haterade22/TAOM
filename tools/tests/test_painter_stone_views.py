"""Tests for the pure part of tools/painter/painter_stone_views.py: the arguments of the wall view."""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "painter"))
import painter_stone_views as pv  # noqa: E402


class ViewArgsTests(unittest.TestCase):
    def test_the_old_wall_gets_the_masonry_scale(self):
        a = pv.view_args("old", "T", "O")
        self.assertIn("--masonry-uv-scale", a)
        self.assertEqual(a[a.index("--masonry-uv-scale") + 1], "0.6")
        self.assertIn("a=" + pv.OLD_WALL, a)

    def test_the_new_wall_has_no_masonry_scale(self):
        a = pv.view_args("new", "T", "O")
        self.assertNotIn("--masonry-uv-scale", a)
        self.assertIn("a=" + pv.NEW_WALL, a)
        self.assertIn("b=" + pv.NEW_WALL, a)

    def test_both_walls_get_green_down_a_low_sun_and_the_folders(self):
        for wall in ("old", "new"):
            a = pv.view_args(wall, "TEX", "OUT")
            self.assertEqual(a[a.index("--tex") + 1], "TEX")
            self.assertEqual(a[a.index("--out") + 1], "OUT")
            self.assertEqual(a[a.index("--normal-green") + 1], "down")
            self.assertEqual(a[a.index("--gap") + 1], "0")

    def test_the_new_wall_is_the_version_3_file(self):
        self.assertIn("/build/v03/wall_solid/", pv.NEW_WALL)


if __name__ == "__main__":
    unittest.main()
