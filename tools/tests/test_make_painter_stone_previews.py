"""Tests for tools/oneoff/make_painter_stone_previews.py: the lit preview and the metre-scale tiling."""
import os
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import make_painter_stone_previews as m  # noqa: E402


class LitTests(unittest.TestCase):
    def test_flat_normal_gives_a_uniform_scaled_colour(self):
        base = np.full((4, 4, 3), 200.0)
        n = np.zeros((4, 4, 3)) + (127.5, 127.5, 255)
        out = m.lit(base, n, None)
        self.assertLess(np.ptp(out), 1.0)
        self.assertLess(out.mean(), 200.0)

    def test_a_slope_toward_the_light_is_brighter_than_away(self):
        base = np.full((2, 2, 3), 150.0)
        toward = np.zeros((2, 2, 3)) + (127.5 + 60, 127.5 + 60, 255)
        away = np.zeros((2, 2, 3)) + (127.5 - 60, 127.5 - 60, 255)
        self.assertGreater(m.lit(base, toward, None).mean(), m.lit(base, away, None).mean())


class TileTests(unittest.TestCase):
    def test_tile_to_metres_crops_to_the_requested_size(self):
        tile = np.zeros((300, 300, 3))
        out = m.tile_to_metres(tile, tile_m=1.5, px_per_m=100, size_m=4.0)
        self.assertEqual(out.shape, (400, 400, 3))


if __name__ == "__main__":
    unittest.main()
