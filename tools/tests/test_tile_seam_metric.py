#!/usr/bin/env python3
"""Unit tests for tools/painter/tile_seam_metric.py.

Run:  python -m unittest tools.tests.test_tile_seam_metric

The metric rolls the image by half its size, so the wrap sits in the middle, and compares the step
across the wrap with the 95th percentile of the steps in a window around it. A seamless tile gives a
ratio near 1 or below, a tile with a step at the wrap gives a ratio well above 1. A sharp feature
exactly at the wrap (a joint, a square wave) must not count as a seam. Synthetic images stand in for
exported textures.
"""
import importlib.util
import os
import unittest

import numpy as np

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "painter", "tile_seam_metric.py")
_spec = importlib.util.spec_from_file_location("tile_seam_metric", SCRIPT)
tsm = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(tsm)

N = 128


def periodic():
    x = np.arange(N) / N
    return 0.5 + 0.4 * np.sin(2 * np.pi * (x[None, :] * 3 + x[:, None] * 2))


def ramp():
    return np.tile(np.linspace(0.0, 1.0, N)[None, :], (N, 1))


class SeamRatioTests(unittest.TestCase):
    def test_periodic_pattern_is_seamless_both_ways(self):
        r = tsm.seam_ratios(periodic())
        self.assertLess(r["horizontal"], 1.3)
        self.assertLess(r["vertical"], 1.3)

    def test_ramp_has_a_horizontal_seam_only(self):
        r = tsm.seam_ratios(ramp())
        self.assertGreater(r["horizontal"], 20.0)
        self.assertLess(r["vertical"], 1.3)

    def test_vertical_ramp_has_a_vertical_seam_only(self):
        r = tsm.seam_ratios(ramp().T)
        self.assertGreater(r["vertical"], 20.0)
        self.assertLess(r["horizontal"], 1.3)

    def test_noise_is_seamless(self):
        rng = np.random.default_rng(3)
        r = tsm.seam_ratios(rng.random((N, N)))
        self.assertLess(r["horizontal"], 1.3)
        self.assertLess(r["vertical"], 1.3)

    def test_colour_image_uses_every_channel(self):
        img = np.stack([periodic(), periodic(), ramp()], axis=2)
        r = tsm.seam_ratios(img)
        self.assertGreater(r["horizontal"], 3.0)

    def test_sharp_edge_exactly_at_the_wrap_is_not_a_seam(self):
        x = np.arange(N)
        square = np.tile(((x // 8) % 2).astype(float)[None, :], (N, 1))
        r = tsm.seam_ratios(square)
        self.assertLess(r["horizontal"], 1.3)
        self.assertLess(r["vertical"], 1.3)

    def test_step_hidden_in_a_noisy_texture_is_still_found(self):
        rng = np.random.default_rng(5)
        img = 0.1 * rng.random((N, N))
        img[:, N // 2:] += 0.5
        img = np.roll(img, N // 2, axis=1)  # the jump now sits on the wrap
        self.assertGreater(tsm.seam_ratios(img)["horizontal"], 3.0)

    def test_flat_image_has_no_seam(self):
        r = tsm.seam_ratios(np.full((N, N), 0.4))
        self.assertEqual(r, {"horizontal": 1.0, "vertical": 1.0})

    def test_verdict_names_the_bad_axis(self):
        self.assertEqual(tsm.verdict({"horizontal": 1.1, "vertical": 1.0}), "no step at the wrap")
        self.assertEqual(tsm.verdict({"horizontal": 9.0, "vertical": 1.0}), "check the wrap on horizontal")
        self.assertEqual(tsm.verdict({"horizontal": 9.0, "vertical": 8.0}),
                         "check the wrap on horizontal and vertical")

    def test_tiny_image_is_refused(self):
        with self.assertRaises(ValueError):
            tsm.seam_ratios(np.zeros((2, 2)))


class TileTests(unittest.TestCase):
    def test_tiling_repeats_the_image(self):
        img = np.arange(12, dtype=float).reshape(3, 4)
        t = tsm.tile(img, 2)
        self.assertEqual(t.shape, (6, 8))
        self.assertTrue(np.array_equal(t[:3, :4], img))
        self.assertTrue(np.array_equal(t[3:, 4:], img))


if __name__ == "__main__":
    unittest.main()
