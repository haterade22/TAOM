"""Tests for the pure parts of tools/oneoff/blender_render_concept_pair.py (no Blender needed)."""
import math
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "oneoff"))

import blender_render_concept_pair as pair  # noqa: E402


class ConceptPairTests(unittest.TestCase):
    def test_srgb_hex_black_and_white_map_to_the_ends(self):
        self.assertEqual(pair.srgb_hex_to_linear("#000000"), (0.0, 0.0, 0.0, 1.0))
        r, g, b, a = pair.srgb_hex_to_linear("ffffff")
        self.assertEqual((round(r, 6), round(g, 6), round(b, 6), a), (1.0, 1.0, 1.0, 1.0))

    def test_srgb_hex_mid_grey_is_darker_in_linear(self):
        r, g, b, _ = pair.srgb_hex_to_linear("#808080")
        self.assertTrue(r == g == b)
        self.assertTrue(0.21 < r < 0.22)  # sRGB 128 is about 0.2158 linear

    def test_srgb_hex_rejects_a_bad_string(self):
        with self.assertRaises(ValueError):
            pair.srgb_hex_to_linear("#12345")

    def test_layout_puts_the_control_left_and_the_concept_right_of_the_gap(self):
        control = ((-10.0, -3.0, -20.0), (10.0, 4.0, 18.0))
        concept = ((-10.0, -3.0, -8.0), (10.0, 3.5, 14.5))
        dx_control, dx_concept = pair.layout_offsets(control, concept, gap=6.0)
        self.assertAlmostEqual(control[1][0] + dx_control, -3.0, places=6)   # control's right edge
        self.assertAlmostEqual(concept[0][0] + dx_concept, 3.0, places=6)    # concept's left edge

    def test_layout_handles_pieces_that_are_not_centred(self):
        control = ((2.0, 0.0, 0.0), (18.4, 1.0, 1.0))
        concept = ((-9.0, 0.0, 0.0), (11.0, 1.0, 1.0))
        dx_control, dx_concept = pair.layout_offsets(control, concept, gap=4.0)
        self.assertAlmostEqual(control[1][0] + dx_control, -2.0, places=6)
        self.assertAlmostEqual(concept[0][0] + dx_concept, 2.0, places=6)

    def test_above_ground_box_clamps_the_floor_to_zero(self):
        lo, hi = pair.above_ground(((-1.0, -2.0, -20.0), (1.0, 2.0, 15.0)))
        self.assertEqual(lo, (-1.0, -2.0, 0.0))
        self.assertEqual(hi, (1.0, 2.0, 15.0))

    def test_above_ground_keeps_a_box_that_starts_above_zero(self):
        lo, _ = pair.above_ground(((0.0, 0.0, 2.5), (1.0, 1.0, 3.0)))
        self.assertEqual(lo[2], 2.5)

    def test_union_box_covers_both(self):
        lo, hi = pair.union_box(((-5, 0, 0), (0, 1, 2)), ((1, -3, 0), (4, 2, 9)))
        self.assertEqual(lo, (-5, -3, 0))
        self.assertEqual(hi, (4, 2, 9))

    def test_perspective_distance_fits_the_sphere_of_the_box(self):
        # a box with a 10 m half diagonal, seen with a 60 degree field, must stand back 10 / sin(30)
        d = pair.perspective_distance(half_diagonal=10.0, fov_radians=math.radians(60), margin=1.0)
        self.assertAlmostEqual(d, 20.0, places=6)

    def test_scaled_box_scales_about_the_origin_then_lifts(self):
        lo, hi = pair.transformed_box(((-8.2, -6.2, -5.0), (8.2, 6.2, 15.0)), scale=(0.5, 0.5, 0.5), lift=3.0)
        for got, want in zip(lo, (-4.1, -3.1, 0.5)):
            self.assertAlmostEqual(got, want, places=6)
        for got, want in zip(hi, (4.1, 3.1, 10.5)):
            self.assertAlmostEqual(got, want, places=6)


if __name__ == "__main__":
    unittest.main()
