"""Tests for the folder layout of tools/painter/painter_plinth_run.py (the --root option)."""
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "painter"))
sys.path.insert(0, os.path.join(HERE, "..", "oneoff"))
import painter_plinth_run as ppr  # noqa: E402
import painter_stone_recipes as pr  # noqa: E402


class PlinthPathsTest(unittest.TestCase):
    def test_default_root_is_round_two_b(self):
        p = ppr.plinth_paths(pr.V06, "d_2x3_tone")
        self.assertEqual(p["out"], pr.V06 + "/plinth_d_2x3_tone")
        self.assertEqual(p["raw"], pr.V06 + "/_export/plinth_d_2x3_tone_raw")
        self.assertEqual(p["cfg"], pr.V06 + "/cfg/plinth_d_2x3_tone_recipe.json")

    def test_a_root_moves_every_folder(self):
        p = ppr.plinth_paths(pr.V07, "a")
        for key, path in p.items():
            self.assertTrue(path.startswith(pr.V07 + "/"), msg=(key, path))
        self.assertEqual(p["out"], pr.V07 + "/plinth_a")
        self.assertEqual(p["rolled"], pr.V07 + "/_export/plinth_a")
        self.assertEqual(p["damp_export"], pr.V07 + "/_export/plinth_a_damp")
        self.assertEqual(p["damp_layout"], pr.V07 + "/layout_plinth_damp_a")


if __name__ == "__main__":
    unittest.main()
