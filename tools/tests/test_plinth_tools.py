"""Tests for the pure parts of tools/painter/plinth_tools.py (the plinth stone of the Rivendell wall)."""
import os
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "painter"))
import plinth_tools as pt  # noqa: E402


class DampMapTests(unittest.TestCase):
    def test_it_is_constant_along_x_and_in_range(self):
        m = pt.damp_map(64, centre=0.0, half_width=0.2)
        self.assertEqual(m.shape, (64, 64))
        self.assertTrue((m == m[:, :1]).all())
        self.assertTrue(0.0 <= m.min() and m.max() <= 1.0)

    def test_the_peak_is_at_the_centre_row_and_it_wraps(self):
        m = pt.damp_map(100, centre=0.0, half_width=0.2)[:, 0]
        self.assertAlmostEqual(float(m[0]), 1.0, places=6)
        self.assertAlmostEqual(float(m[1]), float(m[99]), places=6)
        self.assertEqual(float(m[50]), 0.0)

    def test_it_is_zero_beyond_the_half_width_and_falls_smoothly(self):
        m = pt.damp_map(100, centre=0.5, half_width=0.1)[:, 0]
        self.assertEqual(float(m[:35].max()), 0.0)
        self.assertEqual(float(m[65:].max()), 0.0)
        self.assertTrue(np.all(np.diff(m[40:51]) >= 0))
        self.assertLess(abs(float(m[41]) - float(m[40])), 0.2)


class JointRowTests(unittest.TestCase):
    def profile(self, rows, size=300, width=3):
        lum = np.full((size, size), 150.0)
        for r in rows:
            for k in range(-width // 2, width // 2 + 1):
                lum[(r + k) % size] = 110.0
        return lum

    def test_it_finds_the_joint_rows(self):
        rows = pt.joint_rows(self.profile([0, 100, 200]), min_gap=30)
        self.assertEqual(sorted(rows), [0, 100, 200])

    def test_a_joint_on_the_wrap_edge_is_one_row(self):
        rows = pt.joint_rows(self.profile([299, 100]), min_gap=30)
        self.assertEqual(len(rows), 2)

    def test_shift_brings_the_nearest_joint_to_the_wanted_row(self):
        shift = pt.shift_for_joint([0, 100, 200], 300, want_row=130)
        self.assertEqual((100 + shift) % 300, 130)

    def test_shift_picks_the_smaller_move(self):
        self.assertEqual(pt.shift_for_joint([0, 100, 200], 300, want_row=190), -10)

    def test_roll_rows_keeps_the_image_and_moves_by_shift(self):
        a = np.arange(12).reshape(4, 3)
        self.assertTrue((pt.roll_rows(a, 1)[1] == a[0]).all())
        self.assertTrue((pt.roll_rows(pt.roll_rows(a, 3), -3) == a).all())


class HeightRowTests(unittest.TestCase):
    def test_wall_height_to_row_from_the_top(self):
        # V = 0 at Z = 0 is the bottom row of the image; 3.0 m of a 2.222 m tile is 0.35 of the next tile.
        row = pt.row_for_height(3.0, 2.2222, 1024)
        self.assertEqual(row, round((1 - (3.0 % 2.2222) / 2.2222) * 1024))

    def test_the_bottom_row_is_the_edge(self):
        self.assertEqual(pt.row_for_height(0.0, 2.2222, 1024) % 1024, 0)


class SlantTests(unittest.TestCase):
    def test_a_battered_face_is_longer_than_its_rise(self):
        self.assertAlmostEqual(pt.slant_length(3.0, 0.6), 3.0594, places=3)

    def test_no_batter_gives_the_rise(self):
        self.assertEqual(pt.slant_length(3.0, 0.0), 3.0)


if __name__ == "__main__":
    unittest.main()
