"""Tests for tools/painter/trim_joint_check.py: the joint numbers of a trim sheet diffuse."""
import os
import sys
import unittest

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "painter"))
sys.path.insert(0, os.path.join(HERE, "..", "oneoff"))
import trim_joint_check as tj  # noqa: E402
import rivendell_trim_strips as rts  # noqa: E402


def sheet(joint_cols, level=190.0, dip=40.0, half=2, strip="S1"):
    """A 1024 px grey sheet with dark columns of 2 * half + 1 px in the rows of one strip."""
    lum = np.full((1024, 1024), level)
    t, b = rts.strip_rows()[strip]
    for c in joint_cols:
        for k in range(-half, half + 1):
            lum[t:b, (c + k) % 1024] -= dip
    return lum


class RowSpanTest(unittest.TestCase):
    def test_spans_lie_inside_their_strip_and_clear_of_the_margin(self):
        for name, (t, b) in rts.strip_rows().items():
            a, z = tj.joint_row_span(name)
            self.assertGreaterEqual(a, t + rts.MARGIN_PX, msg=name)
            self.assertLessEqual(z, b - rts.MARGIN_PX, msg=name)
            self.assertGreater(z - a, 5, msg=name)


class StatsTest(unittest.TestCase):
    def test_two_wide_joints_on_s1(self):
        st = tj.strip_joint_stats(sheet([0, 512]), "S1")
        self.assertEqual(st["count"], 2)
        self.assertAlmostEqual(st["contrast"], 40.0, delta=1.0)
        self.assertAlmostEqual(st["width_px"], 5.0, delta=1.0)
        self.assertAlmostEqual(st["width_mm"], 5.0 * rts.TILE_M * 1000.0 / 1024.0, delta=2.5)

    def test_six_fine_joints_on_s4(self):
        st = tj.strip_joint_stats(sheet([int(round(k * 1024 / 6)) for k in range(6)], strip="S4"), "S4")
        self.assertEqual(st["count"], 6)

    def test_a_sheet_with_no_joint_has_none(self):
        st = tj.strip_joint_stats(sheet([], strip="S2"), "S2")
        self.assertEqual(st["count"], 0)
        self.assertEqual(st["contrast"], 0.0)

    def test_the_joint_on_the_seam_wraps(self):
        st = tj.strip_joint_stats(sheet([1023]), "S1")
        self.assertEqual(st["count"], 1)


class WindowStatsTest(unittest.TestCase):
    def test_reads_the_joints_at_their_places(self):
        rng = np.random.default_rng(3)
        lum = sheet([0, 512], strip="S3") + rng.normal(0, 6.0, (1024, 1024))
        st = tj.window_joint_stats(lum, "S3")
        self.assertEqual(st["count"], 2)
        self.assertAlmostEqual(st["contrast"], 40.0, delta=6.0)
        self.assertAlmostEqual(st["width_px"], 5.0, delta=1.5)


class FaceGapTest(unittest.TestCase):
    def test_a_trim_two_levels_darker_than_the_wall(self):
        trim = np.zeros((1024, 1024, 3))
        trim[:] = (196, 187, 172)
        wall = np.zeros((16, 16, 3))
        wall[:] = (198, 189, 174)
        g = tj.face_gap(trim, wall)
        self.assertAlmostEqual(g["lum_diff"], -2.0, delta=0.05)
        self.assertAlmostEqual(g["red_minus_blue_diff"], 0.0, delta=1e-9)


if __name__ == "__main__":
    unittest.main()
