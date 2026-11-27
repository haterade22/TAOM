"""Tests for tools/oneoff/make_smooth_sets.py (the joint-free sets of the Rivendell kit)."""
import os
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import make_smooth_sets as ms  # noqa: E402


class PatchColumnsTests(unittest.TestCase):
    def _ramp(self, w=64, h=4):
        return np.tile(np.arange(w, dtype=float)[None, :, None], (h, 1, 1))

    def test_the_core_takes_the_columns_at_the_offset(self):
        img = self._ramp()
        out = ms.patch_columns(img, 20, 3, 10, 2)
        self.assertTrue(np.allclose(out[0, 17:24, 0], np.arange(27, 34)))

    def test_outside_the_feather_nothing_changes(self):
        img = self._ramp()
        out = ms.patch_columns(img, 20, 3, 10, 2)
        self.assertTrue(np.allclose(out[0, :15, 0], img[0, :15, 0]))
        self.assertTrue(np.allclose(out[0, 26:, 0], img[0, 26:, 0]))

    def test_the_feather_blends(self):
        img = self._ramp()
        out = ms.patch_columns(img, 20, 3, 10, 2)
        self.assertGreater(out[0, 16, 0], img[0, 16, 0])
        self.assertLess(out[0, 16, 0], img[0, 26, 0])

    def test_only_the_given_rows_change(self):
        img = self._ramp()
        out = ms.patch_columns(img, 20, 3, 10, 2, rows=slice(1, 3))
        self.assertTrue(np.allclose(out[0], img[0]))
        self.assertFalse(np.allclose(out[1], img[1]))

    def test_it_wraps_at_the_tile_edge_and_keeps_the_input(self):
        img = self._ramp()
        out = ms.patch_columns(img, 0, 2, 10, 1)
        self.assertAlmostEqual(out[0, 63, 0], 9.0)
        self.assertAlmostEqual(out[0, 0, 0], 10.0)
        self.assertEqual(img[0, 0, 0], 0.0)


class HairlineTests(unittest.TestCase):
    BLOCKS = [{"x0": 100, "x1": 1100, "y0": 0, "y1": 600, "id": 0},
              {"x0": 1100, "x1": 2148, "y0": 0, "y1": 600, "id": 1},
              {"x0": 400, "x1": 2448, "y0": 600, "y1": 2048, "id": 2}]

    def test_bed_joints_run_the_whole_width(self):
        m = ms.hairline_mask(self.BLOCKS, 2048, 1024)
        self.assertEqual(m.shape, (1024, 1024))
        self.assertTrue(np.all(m[0] == 1.0))
        self.assertTrue(np.all(m[300] == 1.0))
        self.assertLess(m[150].sum(), 10)

    def test_head_joints_stay_inside_their_course(self):
        m = ms.hairline_mask(self.BLOCKS, 2048, 1024)
        self.assertEqual(m[150, 50], 1.0)
        self.assertEqual(m[150, 550], 1.0)
        self.assertEqual(m[500, 50], 0.0)
        self.assertEqual(m[500, 200], 1.0)

    def test_a_joint_past_the_edge_wraps(self):
        blocks = [{"x0": 2100, "x1": 4148, "y0": 0, "y1": 2048, "id": 0}]
        m = ms.hairline_mask(blocks, 2048, 1024)
        self.assertEqual(m[500, 26], 1.0)


class DarkenTests(unittest.TestCase):
    def test_only_the_mask_gets_darker(self):
        img = np.full((4, 4, 3), 200.0)
        mask = np.zeros((4, 4))
        mask[1, 2] = 1.0
        out = ms.darken(img, mask, 0.05)
        self.assertAlmostEqual(out[1, 2, 0], 190.0)
        self.assertAlmostEqual(out[0, 0, 0], 200.0)


class ToneTests(unittest.TestCase):
    def test_scaled_target_keeps_the_hue(self):
        t = ms.scaled_target((198.0, 189.0, 174.0), 178.0)
        lum = 0.2126 * t[0] + 0.7152 * t[1] + 0.0722 * t[2]
        self.assertAlmostEqual(lum, 178.0, places=3)
        self.assertAlmostEqual(t[0] / t[2], 198.0 / 174.0, places=6)

    def test_luminance(self):
        self.assertAlmostEqual(ms.luminance((100.0, 100.0, 100.0)), 100.0)


class WearTests(unittest.TestCase):
    def test_stain_gain_has_mean_one_and_a_limit(self):
        rng = np.random.default_rng(3)
        mask = rng.normal(128.0, 30.0, (64, 64))
        g = ms.stain_gain(mask, 0.02, limit=2.5)
        self.assertAlmostEqual(float(g.mean()), 1.0, places=2)
        self.assertLessEqual(float(g.max()), 1.0 + 0.02 * 2.5 + 1e-9)
        self.assertGreaterEqual(float(g.min()), 1.0 - 0.02 * 2.5 - 1e-9)

    def test_a_flat_mask_gives_no_stain(self):
        g = ms.stain_gain(np.full((8, 8), 90.0), 0.05)
        self.assertTrue(np.allclose(g, 1.0))

    def test_pit_field_is_repeatable_and_in_range(self):
        a = ms.pit_field(64, 20, seed=5, sigma=1.0)
        b = ms.pit_field(64, 20, seed=5, sigma=1.0)
        self.assertTrue(np.array_equal(a, b))
        self.assertEqual(a.shape, (64, 64))
        self.assertAlmostEqual(float(a.max()), 1.0)
        self.assertGreaterEqual(float(a.min()), 0.0)
        self.assertLess(float(a.mean()), 0.2)

    def test_another_seed_moves_the_pits(self):
        self.assertFalse(np.array_equal(ms.pit_field(64, 20, seed=5, sigma=1.0), ms.pit_field(64, 20, seed=6, sigma=1.0)))

    def test_worn_diffuse_is_darker_in_a_pit_and_keeps_its_mean(self):
        d = np.full((32, 32, 3), 190.0)
        pits = np.zeros((32, 32))
        pits[10, 10] = 1.0
        out = ms.worn_diffuse(d, np.ones((32, 32)), pits, pit_dark=0.1)
        self.assertAlmostEqual(out[10, 10, 0], 171.0)
        self.assertAlmostEqual(out[0, 0, 0], 190.0)
        self.assertGreater(float(out.mean()), 189.0)


if __name__ == "__main__":
    unittest.main()
