#!/usr/bin/env python3
"""Unit tests for the pure functions of tools/oneoff/make_tileable_texture_set.py.

Run:  python -m unittest tools.tests.test_make_tileable_texture_set
"""
import importlib.util
import os
import unittest

import numpy as np

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "oneoff", "make_tileable_texture_set.py")
_spec = importlib.util.spec_from_file_location("make_tileable_texture_set", SCRIPT)
m = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(m)


def brick_image(n=256, seed=3):
    """Masonry-like image that is NOT periodic: dark joints, a brightness ramp and random block tones."""
    rng = np.random.default_rng(seed)
    img = np.full((n, n, 3), 190.0)
    ch = 64
    for r in range(n // ch):
        off = (r * 37) % 100
        for x in range(n):
            if (x + off) % 100 < 3:
                img[r * ch:(r + 1) * ch, x] = 60.0
        img[r * ch:r * ch + 3] = 60.0
    img += rng.normal(0, 6, (n, n, 1))
    ramp = np.linspace(-40, 40, n)[None, :, None]
    return np.clip(img + ramp, 0, 255)


class FlattenTests(unittest.TestCase):
    def test_removes_a_brightness_ramp(self):
        n = 256
        ramp = np.linspace(0.6, 1.4, n)[None, :, None] * np.ones((n, n, 3)) * 150.0
        before = ramp.mean(axis=2)
        out = m.flatten_light(ramp, sigma=n / 8.0)
        lum = out.mean(axis=2)
        left, right = lum[:, : n // 8].mean(), lum[:, -n // 8:].mean()
        self.assertLess(abs(left - right), 0.15 * lum.mean())
        self.assertLess(abs(left - right), 0.25 * abs(before[:, : n // 8].mean() - before[:, -n // 8:].mean()))

    def test_keeps_the_mean_luminance(self):
        img = brick_image()
        out = m.flatten_light(img, sigma=64.0)
        self.assertAlmostEqual(float(m.lum_of(out).mean()), float(m.lum_of(img).mean()), delta=6.0)


class PathTests(unittest.TestCase):
    def test_follows_a_cheap_column(self):
        cost = np.ones((40, 30))
        cost[:, 17] = 0.0
        path = m.min_cost_path(cost)
        self.assertEqual(path.shape, (40,))
        self.assertTrue((path == 17).all())

    def test_steps_at_most_one(self):
        rng = np.random.default_rng(1)
        path = m.min_cost_path(rng.random((60, 25)))
        self.assertLessEqual(int(np.abs(np.diff(path)).max()), 1)

    def test_periodic_path_closes(self):
        rng = np.random.default_rng(2)
        path = m.min_cost_path(rng.random((64, 20)), periodic=True)
        self.assertLessEqual(abs(int(path[0]) - int(path[-1])), 1)


class TileableTests(unittest.TestCase):
    def test_seam_is_no_worse_than_interior(self):
        img = brick_image()
        out = m.make_tileable(img, overlap=60)
        self.assertEqual(out.shape[0], out.shape[1])
        self.assertLess(out.shape[0], img.shape[0])
        for axis in (0, 1):
            seam = m.seam_mad(out, axis)
            inner = m.interior_mad(out, axis)
            self.assertLessEqual(seam, 1.5 * inner + 1e-9, "axis %d seam %.2f interior %.2f" % (axis, seam, inner))

    def test_beats_a_plain_wrap(self):
        img = brick_image()
        plain = m.seam_mad(img, 1)
        out = m.make_tileable(img, overlap=60)
        self.assertLess(m.seam_mad(out, 1), plain)

    def test_pixels_come_from_the_source(self):
        img = brick_image()
        out = m.make_tileable(img, overlap=64, feather=0)
        vals = set(np.unique(img.astype(np.float32)))
        self.assertTrue(set(np.unique(out.astype(np.float32))) <= vals)


class ResizeTests(unittest.TestCase):
    def test_resize_wraps_and_returns_target_size(self):
        img = np.clip(brick_image(192), 0, 255).astype(np.uint8)
        out = m.resize_wrapped(img, 96)
        self.assertEqual(out.shape, (96, 96, 3))
        self.assertEqual(out.dtype, np.uint8)


class ColourTests(unittest.TestCase):
    def test_mean_lands_on_target_and_variation_stays(self):
        img = brick_image()
        target = (199, 184, 158)
        out = m.match_colour(img, target)
        mean = out.reshape(-1, 3).mean(0)
        for c in range(3):
            self.assertLessEqual(abs(mean[c] - target[c]), 3.0)
        self.assertGreater(float(m.lum_of(out).std()), 0.5 * float(m.lum_of(img).std()) * 0.5)

    def test_hex(self):
        self.assertEqual(m.hex_rgb("#c7b89e"), (199, 184, 158))


class HeightTests(unittest.TestCase):
    def test_ignores_low_frequency_stain(self):
        n = 256
        yy, xx = np.mgrid[0:n, 0:n]
        stain = 40.0 * np.sin(2 * np.pi * xx / n) * np.cos(2 * np.pi * yy / n)
        lum = 180.0 + stain
        h = m.height_from_luminance(lum)
        self.assertLess(float(h.std()), 0.15)

    def test_joints_are_low(self):
        n = 256
        lum = np.full((n, n), 190.0)
        lum[:, 120:124] = 50.0
        h = m.height_from_luminance(lum)
        self.assertLess(float(h[:, 121:123].mean()), float(h[:, 40:80].mean()) - 0.5)

    def test_wraps_without_a_seam(self):
        n = 128
        lum = np.full((n, n), 190.0)
        lum[:, 0:3] = 50.0
        h = m.height_from_luminance(lum)
        self.assertLess(float(h[:, -1].mean()), float(h[:, 60].mean()) - 0.5)


class NormalTests(unittest.TestCase):
    def test_flat_height_gives_blue(self):
        n = m.normal_from_height(np.zeros((16, 16)), 4.0)
        self.assertTrue((n[..., 0] == 128).all() or (n[..., 0] == 127).all())
        self.assertTrue((n[..., 2] == 255).all())

    def test_ramp_up_to_the_right_leans_left(self):
        h = np.tile(np.arange(32, dtype=float), (32, 1))
        n = m.normal_from_height(h, 1.0)
        self.assertLess(int(n[16, 16, 0]), 128)

    def test_y_up(self):
        # height rising towards the top of the image: the surface faces down, so green is below 128
        h = np.tile(np.arange(32, dtype=float)[::-1, None], (1, 32))
        n = m.normal_from_height(h, 1.0)
        self.assertLess(int(n[16, 16, 1]), 128)

    def test_flip_green(self):
        n = np.zeros((2, 2, 3), np.uint8)
        n[..., 1] = 10
        self.assertTrue((m.flip_green(n)[..., 1] == 245).all())


class SpecTests(unittest.TestCase):
    def test_channel_stats_hit_the_control_after_clipping(self):
        rng = np.random.default_rng(5)
        h = rng.normal(size=(128, 128))
        stain = rng.normal(size=(128, 128))
        s = m.make_spec(h, stain, {"g": (66.0, 14.0), "b": (222.0, 27.0)})
        self.assertEqual(s.dtype, np.uint8)
        self.assertTrue((s[..., 0] == 0).all())
        self.assertAlmostEqual(float(s[..., 1].mean()), 66.0, delta=1.0)
        self.assertAlmostEqual(float(s[..., 2].mean()), 222.0, delta=1.0)
        self.assertAlmostEqual(float(s[..., 2].std()), 27.0, delta=3.0)

    def test_joints_are_duller(self):
        rng = np.random.default_rng(6)
        h = rng.normal(size=(128, 128))
        s = m.make_spec(h, np.zeros((128, 128)), {"g": (66.0, 14.0), "b": (222.0, 27.0)})
        low = h < -1.0
        self.assertLess(float(s[..., 2][low].mean()), float(s[..., 2][~low].mean()))
        self.assertLess(float(s[..., 1][low].mean()), float(s[..., 1][~low].mean()))


class TrimLayoutTests(unittest.TestCase):
    def test_strips_cover_the_sheet(self):
        rows = m.strip_rows()
        self.assertEqual(rows["S5"][0], round((1 - 1.0) * 1024))
        self.assertEqual(rows["S1"][1], 1024)


class MultibandTests(unittest.TestCase):
    def test_wide_tone_blend_removes_the_step_but_keeps_the_edges_adjacent(self):
        rng = np.random.default_rng(8)
        n = 200
        ramp = np.linspace(60, 220, n)[None, :, None] * np.ones((n, n, 3))
        img = ramp + rng.normal(0, 2, (n, n, 1))
        hard = m.make_tileable(img, overlap=80, feather=4, low_feather=0)
        soft = m.make_tileable(img, overlap=80, feather=4, low_feather=60)
        step_hard = float(np.abs(np.diff(hard[:, :80], axis=1)).max())
        step_soft = float(np.abs(np.diff(soft[:, :80], axis=1)).max())
        self.assertLess(step_soft, 0.5 * step_hard)
        for axis in (0, 1):
            self.assertLessEqual(m.seam_mad(soft, axis), 1.5 * m.interior_mad(soft, axis) + 1e-9)

    def test_detail_still_comes_from_one_side(self):
        img = brick_image()
        out = m.make_tileable(img, overlap=60, feather=2, low_feather=24)
        self.assertEqual(out.shape, (196, 196, 3))


class ContrastTests(unittest.TestCase):
    def test_contrast_widens_the_spread_and_keeps_the_mean(self):
        img = brick_image()
        a = m.match_colour(img, (150, 140, 120), contrast=1.0)
        b = m.match_colour(img, (150, 140, 120), contrast=2.0)
        self.assertGreater(float(m.lum_of(b).std()), 1.6 * float(m.lum_of(a).std()))
        self.assertAlmostEqual(float(b.reshape(-1, 3).mean(0)[0]), 150.0, delta=3.0)

    def test_relief_gain_scales_the_band_not_the_pits(self):
        n = 128
        rng = np.random.default_rng(4)
        lum = 180.0 + rng.normal(0, 8, (n, n))
        full = m.height_from_luminance(lum, relief=1.0)
        half = m.height_from_luminance(lum, relief=0.5)
        self.assertLess(float(half.std()), 0.7 * float(full.std()))


class BorderRowTests(unittest.TestCase):
    """A one-block stone tile has a joint along its top and bottom edge; a trim strip must not show it."""

    def tile_with_border_joint(self):
        img = np.full((128, 128, 3), 200.0)
        img[:6] = 40.0
        img[-6:] = 40.0
        img[:, 40:44] = 120.0  # a mark that runs top to bottom: it must stay at its column
        return img

    def test_the_border_rows_go_and_the_size_stays(self):
        out = m.drop_border_rows(self.tile_with_border_joint(), 10)
        self.assertEqual(out.shape, (128, 128, 3))
        self.assertGreater(float(out[0].mean()), 150.0)
        self.assertGreater(float(out[-1].mean()), 150.0)

    def test_columns_do_not_move_so_the_tile_still_wraps_in_u(self):
        out = m.drop_border_rows(self.tile_with_border_joint(), 10)
        self.assertAlmostEqual(float(out[64, 41, 0]), 120.0, delta=2.0)
        self.assertAlmostEqual(float(out[64, 80, 0]), 200.0, delta=2.0)

    def test_zero_rows_changes_nothing(self):
        img = self.tile_with_border_joint()
        np.testing.assert_array_equal(m.drop_border_rows(img, 0), img)


if __name__ == "__main__":
    unittest.main()
