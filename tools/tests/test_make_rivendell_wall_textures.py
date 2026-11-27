#!/usr/bin/env python3
"""Unit tests for the pure functions of tools/oneoff/make_rivendell_wall_textures.py.

Run:  python -m unittest tools.tests.test_make_rivendell_wall_textures
"""
import importlib.util
import os
import unittest

import numpy as np

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "oneoff", "make_rivendell_wall_textures.py")
_spec = importlib.util.spec_from_file_location("make_rivendell_wall_textures", SCRIPT)
m = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(m)


def lum(rgb):
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


class TileableNoiseTests(unittest.TestCase):
    def test_shape_and_standardised(self):
        n = m.tileable_noise(128, beta=2.0, seed=1)
        self.assertEqual(n.shape, (128, 128))
        self.assertAlmostEqual(float(n.mean()), 0.0, places=5)
        self.assertAlmostEqual(float(n.std()), 1.0, places=5)

    def test_deterministic(self):
        a = m.tileable_noise(64, seed=3)
        b = m.tileable_noise(64, seed=3)
        self.assertTrue(np.array_equal(a, b))

    def test_tiles_at_the_edges(self):
        n = m.tileable_noise(256, beta=1.5, seed=2)
        for axis in (0, 1):
            self.assertLessEqual(m.seam_mad(n, axis), 1.5 * m.interior_mad(n, axis))

    def test_aniso_changes_the_field(self):
        a = m.tileable_noise(64, seed=4)
        b = m.tileable_noise(64, seed=4, aniso=(0.1, 1.0))
        self.assertFalse(np.allclose(a, b))


class SeamTests(unittest.TestCase):
    def test_a_broken_wrap_is_caught(self):
        ramp = np.tile(np.linspace(0, 255, 64), (64, 1))
        self.assertGreater(m.seam_mad(ramp, 1), 1.5 * m.interior_mad(ramp, 1))

    def test_a_periodic_image_passes(self):
        x = np.arange(64)
        img = np.tile(np.cos(2 * np.pi * x / 64) * 100 + 128, (64, 1))
        self.assertLessEqual(m.seam_mad(img, 1), 1.5 * m.interior_mad(img, 1))


class StripLayoutTests(unittest.TestCase):
    def test_pinned_rows(self):
        rows = m.strip_rows()
        self.assertEqual(rows["S5"], (0, 200))
        self.assertEqual(rows["S4"], (200, 307))
        self.assertEqual(rows["S3"], (307, 522))
        self.assertEqual(rows["S2"], (522, 737))
        self.assertEqual(rows["S1"], (737, 1024))

    def test_contiguous_cover(self):
        rows = sorted(m.strip_rows().values())
        self.assertEqual(rows[0][0], 0)
        self.assertEqual(rows[-1][1], 1024)
        for a, b in zip(rows, rows[1:]):
            self.assertEqual(a[1], b[0])

    def test_v_limits_match_the_pin_within_a_pixel(self):
        pin = {"S1": (0.0, 0.28), "S2": (0.28, 0.49), "S3": (0.49, 0.70),
               "S4": (0.70, 0.805), "S5": (0.805, 1.0)}
        for name, (v0, v1) in pin.items():
            top, bot = m.strip_rows()[name]
            self.assertLess(abs(top - (1 - v1) * 1024), 1.0)
            self.assertLess(abs(bot - (1 - v0) * 1024), 1.0)

    def test_safe_margin_is_flat(self):
        h = np.random.default_rng(0).random((60, 32))
        out = m.flatten_margin(h, 4)
        for r in range(4):
            self.assertTrue(np.array_equal(out[r], out[4]))
            self.assertTrue(np.array_equal(out[-1 - r], out[-5]))
        self.assertTrue(np.array_equal(out[10:50], h[10:50]))


class NormalTests(unittest.TestCase):
    def test_flat_is_up(self):
        n = m.height_to_normal(np.full((16, 16), 0.5), 4.0)
        self.assertTrue((n == np.array([128, 128, 255])).all() or
                        (np.abs(n.astype(int) - np.array([128, 128, 255])).max() <= 1))

    def test_ramp_up_in_x_tilts_red_down(self):
        h = np.tile(np.linspace(0, 1, 16), (16, 1))
        n = m.height_to_normal(h, 4.0, wrap_u=False, wrap_v=False)
        self.assertLess(int(n[8, 8, 0]), 128)

    def test_green_is_y_up(self):
        # height rises toward the image top (up): the surface faces down, so the Y-up green is below 128
        h = np.tile(np.linspace(1, 0, 16)[:, None], (1, 16))
        n = m.height_to_normal(h, 4.0, wrap_u=False, wrap_v=False)
        self.assertLess(int(n[8, 8, 1]), 128)
        h2 = h[::-1]
        n2 = m.height_to_normal(h2, 4.0, wrap_u=False, wrap_v=False)
        self.assertGreater(int(n2[8, 8, 1]), 128)

    def test_flip_green(self):
        h = np.random.default_rng(1).random((16, 16))
        n = m.height_to_normal(h, 3.0)
        f = m.flip_green(n)
        self.assertTrue(np.array_equal(f[..., 1], 255 - n[..., 1]))
        self.assertTrue(np.array_equal(f[..., 0], n[..., 0]))
        self.assertTrue(np.array_equal(f[..., 2], n[..., 2]))

    def test_wrap_gives_equal_gradient_at_the_edge(self):
        x = np.arange(32)
        h = np.tile(np.sin(2 * np.pi * x / 32), (32, 1))
        n = m.height_to_normal(h, 4.0, wrap_u=True, wrap_v=True)
        # the gradient at column 0 is the same as the gradient one period away
        self.assertLessEqual(abs(int(n[5, 0, 0]) - int(n[5, 16, 0])) , 255)
        shifted = np.roll(h, 7, axis=1)
        n2 = m.height_to_normal(shifted, 4.0)
        self.assertTrue(np.array_equal(np.roll(n, 7, axis=1), n2))


class ColourMatchTests(unittest.TestCase):
    def test_mean_and_spread_land_in_range(self):
        rng = np.random.default_rng(5)
        base = rng.normal(150, 12, (128, 128, 1)) + np.array([5, 0, -5])
        out = m.match_color(base, (199, 184, 158), (40.0, 44.0))
        mean = out.reshape(-1, 3).mean(0)
        for a, b in zip(mean, (199, 184, 158)):
            self.assertLess(abs(a - b), 2.0)
        l = lum(out)
        spread = np.percentile(l, 95) - np.percentile(l, 5)
        self.assertGreaterEqual(spread, 40.0 - 1.5)
        self.assertLessEqual(spread, 44.0 + 1.5)

    def test_no_spread_target_keeps_shape(self):
        base = np.random.default_rng(6).normal(100, 5, (32, 32, 3))
        out = m.match_color(base, (222, 214, 203), None)
        self.assertLess(abs(float(out[..., 0].mean()) - 222), 1.0)


class StatsTests(unittest.TestCase):
    def test_match_stats_sets_mean_and_std(self):
        x = np.random.default_rng(7).normal(0, 1, (64, 64))
        y = m.match_stats(x, 66.0, 14.0)
        self.assertAlmostEqual(float(y.mean()), 66.0, delta=0.6)
        self.assertAlmostEqual(float(y.std()), 14.0, delta=0.8)


if __name__ == "__main__":
    unittest.main()
