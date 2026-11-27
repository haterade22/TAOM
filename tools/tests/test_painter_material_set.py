"""Tests for the pure steps of tools/painter/painter_material_set.py (one material as one tiling set)."""
import os
import sys
import unittest

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "painter"))
sys.path.insert(0, os.path.join(HERE, "..", "oneoff"))
import painter_material_set as pms  # noqa: E402


class AssignmentsTest(unittest.TestCase):
    def test_values_are_json(self):
        self.assertEqual(pms.parse_assignments(["a=1", "b=[1, 2]", "c=\"x\""]), {"a": 1, "b": [1, 2], "c": "x"})

    def test_a_bare_word_is_an_error(self):
        with self.assertRaises(ValueError):
            pms.parse_assignments(["a=abc"])

    def test_a_missing_equals_is_an_error(self):
        with self.assertRaises(ValueError):
            pms.parse_assignments(["abc"])

    def test_dotted_key_reaches_into_a_dict(self):
        cfg = {"params": {"tile_x": 6}, "target": [1, 2, 3]}
        out = pms.apply_overrides(cfg, {"params.tile_x": 8, "params.new": 1, "target": [4, 5, 6]})
        self.assertEqual(out, {"params": {"tile_x": 8, "new": 1}, "target": [4, 5, 6]})
        self.assertEqual(cfg["params"]["tile_x"], 6, "the input is not changed")


class GainTest(unittest.TestCase):
    def test_each_channel_reaches_its_mean(self):
        rng = np.random.default_rng(1)
        img = rng.integers(20, 90, (64, 64, 3)).astype(float)
        out = pms.gain_to_mean(img, (57, 68, 97))
        for c, t in enumerate((57, 68, 97)):
            self.assertAlmostEqual(float(out[..., c].mean()), t, delta=0.3)

    def test_ratios_inside_a_channel_stay(self):
        img = np.zeros((2, 2, 3))
        img[0, 0] = (10, 10, 10)
        img[1, 1] = (30, 30, 30)
        out = pms.gain_to_mean(img, (30, 30, 30))
        self.assertAlmostEqual(out[1, 1, 0] / out[0, 0, 0], 3.0, places=3)

    def test_clipping_is_corrected(self):
        img = np.full((4, 4, 3), 200.0)
        img[0, 0] = 250.0
        out = pms.gain_to_mean(img, (230, 230, 230))
        self.assertLessEqual(float(out.max()), 255.0)
        self.assertAlmostEqual(float(out[..., 0].mean()), 230.0, delta=0.6)


class RecipeTest(unittest.TestCase):
    CFG = {"set": "t_x_a", "material": "slate_tile", "params": {"tile_x": 8}, "colors": {"color": [0.3, 0.3, 0.4]},
           "sharpen": 0.5, "layers": [{"name": "extra", "channels": ["BaseColor"], "blend": "Multiply",
                                       "opacity": 0.5, "source": {"color": [0.9, 0.9, 0.9]}}]}

    def test_base_carries_material_params_and_colours(self):
        r = pms.build_recipe(self.CFG, "slate_a", "")
        self.assertEqual(r["base"], {"material": "slate_tile", "params": {"tile_x": 8},
                                     "colors": {"color": [0.3, 0.3, 0.4]}})
        self.assertEqual(r["tag"], "slate_a")

    def test_sharpen_comes_first_as_a_filter_on_the_base_colour(self):
        r = pms.build_recipe(self.CFG, "slate_a", "")
        self.assertEqual(r["layers"][0]["filter"], "sharpen/sharpen")
        self.assertEqual(r["layers"][0]["channels"], ["BaseColor"])
        self.assertEqual(r["layers"][0]["params"], {"intensity": 0.5})
        self.assertEqual(r["layers"][1]["name"], "extra")

    def test_no_sharpen_means_no_filter(self):
        cfg = dict(self.CFG, sharpen=0)
        self.assertEqual([l["name"] for l in pms.build_recipe(cfg, "t", "")["layers"]], ["extra"])

    def test_the_base_uv_is_carried_into_the_recipe(self):
        cfg = dict(self.CFG, uv={"rotation": 90})
        self.assertEqual(pms.build_recipe(cfg, "t", "")["base"]["uv"], {"rotation": 90})

    def test_missing_material_is_an_error(self):
        with self.assertRaises(KeyError):
            pms.build_recipe({"set": "x"}, "t", "")


class BuilderChoiceTest(unittest.TestCase):
    def test_a_plain_material_uses_the_stone_builder(self):
        self.assertTrue(pms.build_script({"material": "m"}).endswith("painter_stone_build.py"))

    def test_a_second_material_needs_the_material_builder(self):
        cfg = {"material": "m", "layers": [{"name": "b", "material": {"material": "n"}}]}
        self.assertTrue(pms.build_script(cfg).endswith("painter_material_build.py"))

    def test_a_uv_turn_of_the_base_needs_the_material_builder(self):
        self.assertTrue(pms.build_script({"material": "m", "uv": {"rotation": 90}}).endswith("painter_material_build.py"))


class PathsTest(unittest.TestCase):
    def test_every_path_is_under_the_root(self):
        p = pms.paths("E:/r/v08", "slate_a")
        self.assertEqual(p["out"], "E:/r/v08/slate_a")
        self.assertEqual(p["export"], "E:/r/v08/_export/slate_a")
        self.assertEqual(p["cfg"], "E:/r/v08/cfg/slate_a_set.json")
        self.assertEqual(p["recipe"], "E:/r/v08/cfg/slate_a_recipe.json")

    def test_engine_file_names(self):
        self.assertEqual(pms.engine_files("t_rivendell_roof_slate_a"),
                         ["t_rivendell_roof_slate_a_d.png", "t_rivendell_roof_slate_a_n.png",
                          "t_rivendell_roof_slate_a_s.png"])


class CheckTest(unittest.TestCase):
    def test_a_value_inside_its_range_is_met(self):
        rows = pms.check_targets({"lum_mean": 68.0, "blue_minus_red": 40.0}, {"lum_mean": [60, 75],
                                                                            "blue_minus_red": [30, 45]})
        self.assertTrue(all(r["met"] for r in rows))

    def test_a_value_outside_is_not_met_and_named(self):
        rows = pms.check_targets({"lum_mean": 90.0}, {"lum_mean": [60, 75]})
        self.assertEqual(rows, [{"name": "lum_mean", "value": 90.0, "lo": 60, "hi": 75, "met": False}])

    def test_a_missing_value_is_not_met(self):
        rows = pms.check_targets({}, {"lum_mean": [60, 75]})
        self.assertFalse(rows[0]["met"])


class MeasureTest(unittest.TestCase):
    def test_blue_minus_red_and_red_minus_blue_follow_the_mean(self):
        d = np.zeros((8, 8, 3), np.uint8)
        d[..., 0], d[..., 1], d[..., 2] = 50, 60, 90
        m = pms.extra_measures(d, np.full((8, 8, 3), (128, 128, 255), np.uint8), np.zeros((8, 8, 3), np.uint8))
        self.assertEqual(m["blue_minus_red"], 40.0)
        self.assertEqual(m["red_minus_blue"], -40.0)

    def test_slate_tone_spread_is_the_range_of_cell_means(self):
        d = np.zeros((8, 8, 3), np.uint8)
        d[:, :4] = 60
        d[:, 4:] = 66
        self.assertEqual(pms.cell_tone_spread(d, 2, 2), 6.0)


class DirectionTest(unittest.TestCase):
    def test_stripes_have_a_high_ratio_and_noise_has_about_one(self):
        stripes = np.tile(np.array([0, 255], np.uint8)[:, None, None], (16, 32, 3))
        self.assertGreater(pms.direction_ratio(stripes), 20.0)
        rng = np.random.default_rng(3)
        noise = rng.integers(0, 255, (64, 64, 3)).astype(np.uint8)
        self.assertLess(pms.direction_ratio(noise), 1.15)

    def test_the_ratio_is_never_below_one(self):
        cols = np.tile(np.array([0, 255], np.uint8)[None, :, None], (32, 16, 3))
        self.assertGreater(pms.direction_ratio(cols), 20.0)


class StaggerTest(unittest.TestCase):
    def _tile(self):
        d = np.zeros((2, 8, 3), np.uint8)
        d[0] = 40
        d[1, 2:6] = 80
        d[1, 6:] = 100
        d[1, :2] = 100
        return d

    def test_a_staggered_row_is_read_in_its_own_cells(self):
        self.assertEqual(pms.cell_tone_spread(self._tile(), 2, 2, stagger=True, margin=0.0), 60.0)

    def test_a_plain_grid_mixes_the_cells_of_a_staggered_row(self):
        self.assertEqual(pms.cell_tone_spread(self._tile(), 2, 2, stagger=False, margin=0.0), 50.0)


class TilingTest(unittest.TestCase):
    def test_three_by_three_is_nine_tiles(self):
        t = np.arange(4 * 4 * 3, dtype=np.uint8).reshape(4, 4, 3)
        out = pms.tiled(t, 3)
        self.assertEqual(out.shape, (12, 12, 3))
        self.assertTrue((out[4:8, 8:12] == t).all())


if __name__ == "__main__":
    unittest.main()
