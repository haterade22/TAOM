#!/usr/bin/env python3
"""Unit tests for tools/painter/painter_probe_metrics.py.

Run:  python -m unittest tools.tests.test_painter_probe_metrics

The helpers compare exported maps: the variance of the Laplacian as a sharpness number, a crop by UV
fraction so that two sizes show the same region, a box downsample to bring two sizes to one scale, and a
count of distinct levels along a line (a stair-step test).
"""
import importlib.util
import os
import unittest

import numpy as np

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "painter", "painter_probe_metrics.py")
_spec = importlib.util.spec_from_file_location("painter_probe_metrics", SCRIPT)
m = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(m)


class LaplacianVariance(unittest.TestCase):
    def test_flat_image_is_zero(self):
        self.assertEqual(m.laplacian_variance(np.full((16, 16), 0.5)), 0.0)

    def test_linear_ramp_is_zero(self):
        ramp = np.tile(np.arange(16, dtype=float), (16, 1))
        self.assertAlmostEqual(m.laplacian_variance(ramp), 0.0)

    def test_sharp_edge_beats_soft_edge(self):
        sharp = np.zeros((32, 32))
        sharp[:, 16:] = 1.0
        soft = np.tile(np.clip((np.arange(32) - 12) / 8.0, 0, 1), (32, 1))
        self.assertGreater(m.laplacian_variance(sharp), m.laplacian_variance(soft))

    def test_checkerboard_value(self):
        board = (np.indices((8, 8)).sum(axis=0) % 2).astype(float)
        # every interior texel has four neighbours of the other value: laplacian = +-4
        self.assertAlmostEqual(m.laplacian_variance(board), 16.0)


class CropUv(unittest.TestCase):
    def test_quarter_crop_size(self):
        a = np.arange(64 * 64).reshape(64, 64)
        self.assertEqual(m.crop_uv(a, 0.25, 0.5, 0.75, 1.0).shape, (32, 32))

    def test_same_region_at_two_sizes(self):
        small = np.arange(8, dtype=float)[None, :].repeat(8, 0)
        big = np.kron(small, np.ones((2, 2)))
        c_small = m.crop_uv(small, 0.5, 0.0, 1.0, 0.5)
        c_big = m.crop_uv(big, 0.5, 0.0, 1.0, 0.5)
        self.assertEqual(c_big.shape, (8, 8))
        self.assertTrue(np.array_equal(m.box_down(c_big, 2), c_small))


class BoxDown(unittest.TestCase):
    def test_mean_of_blocks(self):
        a = np.array([[0.0, 2.0], [4.0, 6.0]])
        self.assertTrue(np.array_equal(m.box_down(a, 2), np.array([[3.0]])))

    def test_factor_one_is_identity(self):
        a = np.random.RandomState(1).rand(4, 4)
        self.assertTrue(np.array_equal(m.box_down(a, 1), a))

    def test_rejects_uneven_factor(self):
        with self.assertRaises(ValueError):
            m.box_down(np.zeros((5, 5)), 2)


class DistinctLevels(unittest.TestCase):
    def test_counts_unique_values_on_a_row(self):
        a = np.zeros((4, 16), dtype=np.uint8)
        a[2, :] = np.repeat(np.arange(4, dtype=np.uint8), 4)
        self.assertEqual(m.distinct_levels(a, row=2, c0=0, c1=16), 4)

    def test_smooth_16_px_ramp_in_8_bit_has_few_levels(self):
        ramp = np.round(np.linspace(100, 104, 16)).astype(np.uint8)[None, :]
        self.assertLessEqual(m.distinct_levels(ramp, 0, 0, 16), 5)


class StepRuns(unittest.TestCase):
    def test_runs_of_equal_values(self):
        line = np.array([1, 1, 1, 2, 2, 3, 3, 3, 3])
        self.assertEqual(m.run_lengths(line), [3, 2, 4])


class StepProfile(unittest.TestCase):
    def test_smooth_steps_start_and_end_values(self):
        row = m.step_profile(64, 0.5, [(16, 16, 0.25)])
        self.assertAlmostEqual(row[0], 0.5)
        self.assertAlmostEqual(row[15], 0.5)
        self.assertAlmostEqual(row[32], 0.75)
        self.assertAlmostEqual(row[63], 0.75)

    def test_steps_are_monotonic_and_smooth(self):
        row = m.step_profile(64, 0.0, [(8, 32, 1.0)])
        self.assertTrue(np.all(np.diff(row) >= 0))
        self.assertLess(np.diff(row).max(), 0.1)

    def test_two_steps_add(self):
        row = m.step_profile(64, 0.2, [(4, 8, 0.1), (32, 16, 0.2)])
        self.assertAlmostEqual(row[-1], 0.5)

    def test_image_is_16_bit_and_constant_down_the_rows(self):
        img = m.step_image_16(32, 0.25, [(8, 8, 0.25)])
        self.assertEqual(img.dtype, np.uint16)
        self.assertEqual(img.shape, (32, 32))
        self.assertTrue(np.all(img == img[0]))
        self.assertEqual(int(img[0, 0]), round(0.25 * 65535))
        self.assertEqual(int(img[0, -1]), round(0.5 * 65535))


class Fwhm(unittest.TestCase):
    def test_box_dip_of_four_pixels(self):
        line = np.full(32, 1.0)
        line[10:14] = 0.0
        self.assertAlmostEqual(m.fwhm(line), 4.0, places=3)

    def test_bump_works_the_same_as_a_dip(self):
        line = np.zeros(32)
        line[10:16] = 1.0
        self.assertAlmostEqual(m.fwhm(line), 6.0, places=3)

    def test_triangle_dip(self):
        line = np.array([1, 1, 1, 0.5, 0, 0.5, 1, 1, 1], dtype=float)
        self.assertAlmostEqual(m.fwhm(line), 2.0, places=3)

    def test_flat_line_has_no_width(self):
        self.assertEqual(m.fwhm(np.full(16, 0.3)), 0.0)


class MirrorAsymmetry(unittest.TestCase):
    def test_symmetric_about_the_wrap(self):
        line = np.zeros(64)
        line[[62, 63, 0, 1]] = [0.2, 1.0, 1.0, 0.2]
        self.assertEqual(m.mirror_asymmetry(line, 8), 0.0)

    def test_one_sided_fade_is_flagged(self):
        line = np.zeros(64)
        line[[62, 63, 0, 1]] = [0.0, 1.0, 1.0, 0.6]
        self.assertAlmostEqual(m.mirror_asymmetry(line, 8), 0.6)


if __name__ == "__main__":
    unittest.main()
