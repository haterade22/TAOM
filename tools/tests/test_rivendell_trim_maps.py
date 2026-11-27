#!/usr/bin/env python3
"""Unit tests for tools/oneoff/rivendell_trim_maps.py (numpy, no Blender).

Run:  python -m unittest tools.tests.test_rivendell_trim_maps
"""
import importlib.util
import os
import unittest

import numpy as np

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location(
    "rivendell_trim_maps", os.path.join(TOOLS, "oneoff", "rivendell_trim_maps.py"))
rtm = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(rtm)

N = 64


def bump(cx=32.0, cy=32.0, s=8.0, a=4.0):
    """A height field (row 0 at the top) with one round bump, periodic in neither axis."""
    r, c = np.mgrid[0:N, 0:N].astype(float)
    return a * np.exp(-((r - cy) ** 2 + (c - cx) ** 2) / (2 * s * s))


def normal_up_from_height(h, mm_per_px=1.0):
    """Green-up tangent normal as 0 to 1 RGB: n = (-dh/dx, dh/dy_up, 1) normalised; rows run down."""
    d_row, d_col = np.gradient(h, mm_per_px)
    n = np.stack([-d_col, d_row, np.ones_like(h)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


class SignTest(unittest.TestCase):
    def test_a_green_up_map_gives_opposite_signs(self):
        h = bump()
        cr, cg = rtm.sign_corr(h, normal_up_from_height(h))
        self.assertLess(cr, -0.9)
        self.assertGreater(cg, 0.9)
        self.assertEqual(rtm.green_sense(cr, cg), "green_up")

    def test_flipping_green_gives_the_same_sign_and_green_down(self):
        h = bump()
        down = rtm.green_flip(normal_up_from_height(h))
        cr, cg = rtm.sign_corr(h, down)
        self.assertLess(cr, -0.9)
        self.assertLess(cg, -0.9)
        self.assertEqual(rtm.green_sense(cr, cg), "green_down")

    def test_green_flip_twice_is_identity(self):
        n = normal_up_from_height(bump())
        self.assertTrue(np.allclose(rtm.green_flip(rtm.green_flip(n)), n))
        self.assertTrue(np.allclose(rtm.green_flip(n)[..., 0], n[..., 0]))
        self.assertTrue(np.allclose(rtm.green_flip(n)[..., 1], 1.0 - n[..., 1]))


class HeightTest(unittest.TestCase):
    def test_u16_puts_the_lowest_point_at_black_and_the_highest_at_white(self):
        h = np.array([[2.0, 5.0], [8.0, 22.0]])
        a, lo, hi = rtm.height_u16(h)
        self.assertEqual(a.dtype, np.uint16)
        self.assertEqual(a.min(), 0)
        self.assertEqual(a.max(), 65535)
        self.assertEqual((lo, hi), (2.0, 22.0))
        back = lo + a / 65535.0 * (hi - lo)
        self.assertTrue(np.allclose(back, h, atol=1e-3))


class WrapTest(unittest.TestCase):
    def test_a_periodic_map_wraps_and_a_ramp_does_not(self):
        x = np.arange(256)
        periodic = np.tile(np.sin(2 * np.pi * 3 * x / 256.0), (8, 1))
        ramp = np.tile(x / 255.0, (8, 1))
        self.assertLess(rtm.wrap_ratio(periodic), 1.5)
        self.assertGreater(rtm.wrap_ratio(ramp), 50)

    def test_the_ratio_uses_every_channel(self):
        x = np.arange(64)
        a = np.stack([np.tile(np.sin(2 * np.pi * x / 64.0), (4, 1))] * 3, axis=-1)
        self.assertLess(rtm.wrap_ratio(a), 1.5)


class CurvatureTest(unittest.TestCase):
    def test_convex_is_light_concave_is_dark_flat_is_mid_grey(self):
        h = bump(a=4.0)
        c = rtm.curvature(normal_up_from_height(h))
        self.assertGreater(c[32, 32], 0.52)  # the crown of the bump is convex
        self.assertLess(c[32, 32 + 12], 0.495)  # the skirt is concave
        self.assertAlmostEqual(float(c[2, 2]), 0.5, delta=0.01)
        v = rtm.curvature(normal_up_from_height(-bump(a=4.0)))
        self.assertLess(v[32, 32], 0.48)

    def test_the_range_is_zero_to_one(self):
        c = rtm.curvature(normal_up_from_height(bump(a=6.0)))
        self.assertGreaterEqual(float(c.min()), 0.0)
        self.assertLessEqual(float(c.max()), 1.0)


class ShadeTest(unittest.TestCase):
    def test_a_flat_open_surface_is_the_same_everywhere(self):
        n = np.zeros((8, 8, 3)) + [0.5, 0.5, 1.0]
        lit = rtm.shade(n, np.ones((8, 8)))
        self.assertTrue(np.allclose(lit, lit[0, 0]))
        self.assertGreater(float(lit[0, 0]), 0.3)
        self.assertLess(float(lit[0, 0]), 0.9)

    def test_a_face_that_looks_at_the_light_is_brighter_than_one_that_looks_away(self):
        h = bump(a=6.0)
        lit = rtm.shade(normal_up_from_height(h), np.ones((N, N)))
        # the upper left flank looks towards the upper left light
        self.assertGreater(float(lit[32 - 8, 32 - 8]), float(lit[32 + 8, 32 + 8]))

    def test_occlusion_darkens(self):
        n = np.zeros((4, 4, 3)) + [0.5, 0.5, 1.0]
        ao = np.ones((4, 4))
        ao[0, 0] = 0.5
        lit = rtm.shade(n, ao)
        self.assertLess(float(lit[0, 0]), float(lit[1, 1]))


class TileTest(unittest.TestCase):
    def test_three_by_one(self):
        a = np.arange(12).reshape(3, 4)
        t = rtm.tile_u(a, 3)
        self.assertEqual(t.shape, (3, 12))
        self.assertTrue(np.array_equal(t[:, 4:8], a))


class JointMapTest(unittest.TestCase):
    """The map of the joints, drawn from the strip functions, that the Painter texture uses as a mask."""

    @classmethod
    def setUpClass(cls):
        import rivendell_trim_strips as rts
        cls.rts = rts
        cls.size = 512
        cls.m = rtm.joint_weight_map(cls.size)
        k = cls.size / float(rts.SIZE)
        cls.rows = {n: (int(round(t * k)), int(round(b * k))) for n, (t, b) in rts.strip_rows().items()}

    def test_shape_and_range(self):
        self.assertEqual(self.m.shape, (self.size, self.size))
        self.assertGreaterEqual(float(self.m.min()), 0.0)
        self.assertLessEqual(float(self.m.max()), 1.0)

    def test_the_wide_joints_are_where_the_strips_say(self):
        rows = self.rows
        for name in ("S1", "S2", "S5"):
            t, b = rows[name]
            mid = (t + b) // 2
            for col in (0, self.size // 2 - 1, self.size // 2, self.size - 1):
                self.assertGreater(self.m[mid, col], 0.9, msg=(name, col))
            self.assertEqual(float(self.m[mid, self.size // 4]), 0.0, msg=name)

    def test_the_margins_and_the_gaps_between_joints_are_empty(self):
        rows = self.rows
        for name, (t, b) in rows.items():
            self.assertEqual(float(self.m[t:t + 2].max()), 0.0, msg=name)
            self.assertEqual(float(self.m[b - 2:b].max()), 0.0, msg=name)

    def test_s3_has_its_joints_only_in_the_beads(self):
        t, b = self.rows["S3"]
        mid = (t + b) // 2
        self.assertEqual(float(self.m[mid, 0]), 0.0)
        self.assertGreater(float(self.m[t:b, 0].max()), 0.9)
        beads = np.nonzero(self.m[t:b, 0] > 0.5)[0]
        self.assertGreater(len(beads), 6)
        self.assertTrue(np.all(np.abs(beads + t - mid) > 20))

    def test_s4_has_six_joints(self):
        t, b = self.rows["S4"]
        mid = (t + b) // 2
        row = self.m[mid]
        starts = np.nonzero((row > 0.5) & (np.roll(row, 1) <= 0.5))[0]
        self.assertEqual(len(starts), 6)

    def test_the_wide_joint_floor_is_thirteen_millimetres_wide(self):
        t, b = self.rows["S1"]
        row = self.m[(t + b) // 2]
        px_mm = self.rts.TILE_M / self.size * 1000.0
        width = float(np.sum(row[np.r_[0:40, self.size - 40:self.size]]))
        self.assertAlmostEqual(width * px_mm, 13.0, delta=4.4)

    def test_s4_floor_is_seven_and_a_half_millimetres_wide(self):
        t, b = self.rows["S4"]
        row = self.m[(t + b) // 2]
        px_mm = self.rts.TILE_M / self.size * 1000.0
        self.assertAlmostEqual(float(np.sum(row)) / 6.0 * px_mm, 7.5, delta=4.4)


class HaloMapTest(unittest.TestCase):
    """The contact shade of the wall stone (block_layout: 1 - (1 - J) * clip(2 * gauss(J), 0, 1))."""

    @staticmethod
    def band(size=128, c0=60, c1=72):
        j = np.zeros((size, size))
        j[:, c0:c1] = 1.0
        return j

    def test_the_halo_is_one_in_the_joint_and_far_from_it(self):
        h = rtm.halo_map(self.band(), 6.0)
        self.assertEqual(float(h[64, 66]), 1.0)
        self.assertAlmostEqual(float(h[64, 5]), 1.0, places=3)
        self.assertAlmostEqual(float(h[64, 120]), 1.0, places=3)

    def test_it_is_darkest_at_the_edge_of_the_joint_and_rises_with_distance(self):
        h = rtm.halo_map(self.band(), 6.0)
        row = h[64]
        self.assertLess(float(row[59]), 0.15)
        self.assertLess(float(row[72]), 0.15)
        for k in range(59, 30, -1):
            self.assertGreaterEqual(float(row[k - 1]), float(row[k]) - 1e-9)
        self.assertGreater(float(row[59 - 15]), 0.97)

    def test_it_matches_the_wall_values_at_the_same_sigma(self):
        """layout_halo.png of stone_d (sigma 6.45 px, a joint of 12 px): 56, 134, 193, 229, 246 of 255 at the 2nd, 5th, 8th,
        11th and 14th pixel outside the joint, read 2026-11-25. It agrees to 2 levels; 0.02 is allowed."""
        h = rtm.halo_map(self.band(256, 120, 132), 6.45)
        row = h[128]
        for k, want in enumerate((56, 134, 193, 229, 246)):
            self.assertAlmostEqual(float(row[118 - 3 * k]), want / 255.0, delta=0.02, msg=k)

    def test_it_wraps_in_u(self):
        j = np.zeros((32, 128))
        j[:, :6] = 1.0
        j[:, -6:] = 1.0
        h = rtm.halo_map(j, 5.0)
        self.assertAlmostEqual(float(h[10, 6]), float(h[10, 121]), places=6)


if __name__ == "__main__":
    unittest.main()
