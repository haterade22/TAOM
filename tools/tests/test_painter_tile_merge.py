#!/usr/bin/env python3
"""Unit tests for tools/painter/painter_tile_merge.py (pure Python and numpy, no Painter).

Run:  python -m unittest tools.tests.test_painter_tile_merge
"""
import importlib.util
import os
import unittest

import numpy as np

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location(
    "painter_tile_merge", os.path.join(TOOLS, "painter", "painter_tile_merge.py"))
mg = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(mg)


class SeedTests(unittest.TestCase):
    def test_identical_images_mean_inert(self):
        a = np.zeros((4, 4, 3), np.uint8)
        self.assertEqual(mg.mean_abs_diff(a, a.copy()), 0.0)
        self.assertFalse(mg.seed_effective(0.0, 0.0))

    def test_changed_image_means_effective(self):
        a = np.zeros((4, 4, 3), np.uint8)
        b = a.copy()
        b[0, 0] = 255
        self.assertGreater(mg.mean_abs_diff(a, b), 0.5)
        self.assertTrue(mg.seed_effective(mg.mean_abs_diff(a, b), 0.0))

    def test_threshold_ignores_rounding_noise(self):
        self.assertFalse(mg.seed_effective(0.01, 0.02))


class MergeTests(unittest.TestCase):
    def test_group_and_affects_are_added_by_identifier(self):
        cat = {"materials": {"m": {"parameters": {"tile": {"label": "Tile"}, "ghost": {"label": "G"}}}}}
        schemas = [{"identifier": "m", "params": [
            {"identifier": "tile", "group": "Shape", "affects": ["height", "normal"]}]}]
        out = mg.merge(cat, schemas, {})
        p = out["materials"]["m"]["parameters"]
        self.assertEqual(p["tile"]["group"], "Shape")
        self.assertEqual(p["tile"]["affects"], ["height", "normal"])
        self.assertNotIn("affects", p["ghost"])

    def test_seed_result_is_recorded(self):
        cat = {"materials": {"m": {"parameters": {}}, "n": {"parameters": {}}}}
        out = mg.merge(cat, [], {"m": {"base": 0.0, "normal": 0.0}})
        self.assertFalse(out["materials"]["m"]["seed_effective"])
        self.assertNotIn("seed_effective", out["materials"]["n"])

    def test_merge_does_not_change_its_input(self):
        cat = {"materials": {"m": {"parameters": {"tile": {}}}}}
        mg.merge(cat, [{"identifier": "m", "params": [{"identifier": "tile", "group": "G", "affects": []}]}], {})
        self.assertNotIn("group", cat["materials"]["m"]["parameters"]["tile"])


if __name__ == "__main__":
    unittest.main()
