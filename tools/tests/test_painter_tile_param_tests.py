#!/usr/bin/env python3
"""Unit tests for the metrics in tools/painter/painter_tile_param_tests.py (no Painter).

Run:  python -m unittest tools.tests.test_painter_tile_param_tests
"""
import importlib.util
import os
import unittest

import numpy as np

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location(
    "painter_tile_param_tests", os.path.join(TOOLS, "painter", "painter_tile_param_tests.py"))
pt = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pt)


class MetricTests(unittest.TestCase):
    def test_flat_normal_has_zero_tilt(self):
        n = np.zeros((4, 4, 3), np.uint8)
        n[...] = (128, 128, 255)
        self.assertLess(pt.tilt(n), 0.01)

    def test_tilted_normal_has_more_tilt(self):
        flat = np.zeros((4, 4, 3), np.uint8)
        flat[...] = (128, 128, 255)
        tilted = flat.copy()
        tilted[..., 0] = 200
        self.assertGreater(pt.tilt(tilted), pt.tilt(flat) + 0.3)

    def test_base_stats_mean_and_contrast(self):
        b = np.zeros((2, 2, 3), np.uint8)
        b[0] = 100
        b[1] = 200
        mean, std = pt.base_stats(b)
        self.assertAlmostEqual(mean, 150.0, places=3)
        self.assertAlmostEqual(std, 50.0, places=3)

    def test_spec_names_are_unique_and_safe(self):
        for mat, specs in pt.TESTS.items():
            names = [s["name"] for s in specs]
            self.assertEqual(len(names), len(set(names)), mat)
            for n in names:
                self.assertRegex(n, r"^[a-z0-9_.\-]+$")


if __name__ == "__main__":
    unittest.main()
