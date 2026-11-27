"""Tests for the pure geometry of tools/oneoff/rivendell_roof_test_piece.py (a wall with a gable roof, a test
piece for the roof slate set)."""
import math
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "oneoff"))
import rivendell_roof_test_piece as rp  # noqa: E402


def dist(a, b):
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(a, b)))


class RoofSlopeTest(unittest.TestCase):
    def setUp(self):
        self.slope = rp.roof_slope(x0=-4.0, x1=4.0, y_eave=3.1, y_ridge=2.0, z_eave=6.0, pitch_deg=40.0)

    def test_four_corners_and_one_quad(self):
        self.assertEqual(len(self.slope["verts"]), 4)
        self.assertEqual(len(self.slope["uvs"]), 4)

    def test_the_ridge_is_higher_than_the_eave_by_the_pitch(self):
        z = [v[2] for v in self.slope["verts"]]
        self.assertAlmostEqual(max(z) - min(z), (3.1 - 2.0) * math.tan(math.radians(40.0)), places=6)

    def test_uv_density_is_0_45_per_metre_along_and_up_the_slope(self):
        v = self.slope["verts"]
        uv = self.slope["uvs"]
        eave_len = dist(v[0], v[1])
        self.assertAlmostEqual(abs(uv[1][0] - uv[0][0]) / eave_len, 0.45, places=6)
        up_len = dist(v[0], v[3])
        self.assertAlmostEqual(abs(uv[3][1] - uv[0][1]) / up_len, 0.45, places=6)

    def test_u_runs_along_the_eave_and_v_up_the_slope(self):
        uv = self.slope["uvs"]
        self.assertAlmostEqual(uv[0][1], uv[1][1], places=9)
        self.assertAlmostEqual(uv[0][0], uv[3][0], places=9)
        self.assertGreater(uv[3][1], uv[0][1])

    def test_the_front_slope_faces_up_and_toward_plus_y(self):
        n = self.slope["normal"]
        self.assertGreater(n[2], 0.0)
        self.assertGreater(n[1], 0.0)


class BackSlopeTest(unittest.TestCase):
    def test_the_back_slope_leans_the_other_way(self):
        back = rp.roof_slope(-4.0, 4.0, 0.9, 2.0, 6.0, 40.0)
        self.assertLess(back["normal"][1], 0.0)
        self.assertGreater(back["normal"][2], 0.0)


class BoxUvTest(unittest.TestCase):
    def test_the_front_face_takes_x_and_z(self):
        u, v = rp.box_uv((2.0, 5.0, 3.0), (0.0, 1.0, 0.0), 0.45)
        self.assertAlmostEqual(u, 0.9)
        self.assertAlmostEqual(v, 1.35)

    def test_a_side_face_takes_y_and_z(self):
        u, v = rp.box_uv((2.0, 5.0, 3.0), (1.0, 0.0, 0.0), 0.45)
        self.assertAlmostEqual(u, 2.25)
        self.assertAlmostEqual(v, 1.35)


if __name__ == "__main__":
    unittest.main()
