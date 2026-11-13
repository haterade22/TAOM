#!/usr/bin/env python3
"""Unit tests for the pure parts of tools/blender/qa_views.py.

Run:  python -m unittest tools.tests.test_qa_views

The script imports bpy only inside its Blender-side functions, so the view set, the camera frame
and the object filter load here without stubs. The renders are proven by reading them.
"""
import importlib.util
import math
import os
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "blender", "qa_views.py")

_spec = importlib.util.spec_from_file_location("qa_views", SCRIPT)
qv = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(qv)


class ViewSetTests(unittest.TestCase):
    def test_six_named_views_with_unit_directions(self):
        names = [name for name, _d in qv.VIEWS]
        self.assertEqual(len(names), 6)
        self.assertEqual(len(set(names)), 6)
        for _name, d in qv.VIEWS:
            self.assertAlmostEqual(math.sqrt(sum(c * c for c in d)), 1.0, places=6)

    def test_the_two_upper_three_quarter_views_are_opposite_in_plan(self):
        views = dict(qv.VIEWS)
        a, b = views["3q_front_left"], views["3q_back_right"]
        self.assertAlmostEqual(a[0], -b[0])
        self.assertAlmostEqual(a[1], -b[1])
        self.assertGreater(a[2], 0)
        self.assertGreater(b[2], 0)

    def test_one_view_looks_from_below(self):
        self.assertLess(dict(qv.VIEWS)["3q_below"][2], 0)

    def test_top_view_looks_straight_down(self):
        self.assertGreater(dict(qv.VIEWS)["top"][2], 0.99)


class CameraFrameTests(unittest.TestCase):
    def test_camera_sits_outside_the_box_on_the_view_direction(self):
        loc, scale, clip_end = qv.camera_frame((0, 0, 0), (2, 4, 6), (1, 0, 0))
        self.assertEqual(loc, (1 + 6 * qv.DISTANCE, 2.0, 3.0))
        self.assertAlmostEqual(scale, math.sqrt(2 * 2 + 4 * 4 + 6 * 6) * qv.MARGIN)
        self.assertGreater(clip_end, 6 * qv.DISTANCE + 6)

    def test_the_whole_diagonal_fits_from_every_direction(self):
        diag = math.sqrt(3)
        for _name, d in qv.VIEWS:
            _loc, scale, _clip = qv.camera_frame((0, 0, 0), (1, 1, 1), d)
            self.assertGreaterEqual(scale, diag)

    def test_the_picture_is_square_so_the_diagonal_fits_in_height_too(self):
        # an orthographic scale spans the longer side of the picture: on a 4:3 picture a tall mesh lost
        # its top and bottom (a 1 x 1 x 10 tower needs 10.0 and got 8.3)
        self.assertEqual(qv.render_size(960), (960, 960))

    def test_a_flat_box_still_gets_a_frame(self):
        _loc, scale, clip_end = qv.camera_frame((0, 0, 0), (0, 0, 0), (0, 0, 1))
        self.assertGreater(scale, 0)
        self.assertGreater(clip_end, 0)


class ShownNamesTests(unittest.TestCase):
    NAMES = ["sm_gd_crate_001", "sm_gd_crate_001.lod1", "sm_gd_crate_001_lod2", "bo_sm_gd_crate_001",
             "sm_gd_house.roof"]

    def test_only_visual_lod0_is_shown(self):
        self.assertEqual(qv.shown_names(self.NAMES), ["sm_gd_crate_001", "sm_gd_house.roof"])

    def test_only_filter_keeps_the_named_metameshes(self):
        self.assertEqual(qv.shown_names(self.NAMES, only=["SM_GD_House"]), ["sm_gd_house.roof"])

    def test_lod_zero_spelled_out_is_shown_and_a_duplicated_lod_is_not(self):
        names = ["sm_x_lod0", "sm_x_lod1", "sm_y", "sm_y.lod1.001"]
        self.assertEqual(qv.shown_names(names), ["sm_x_lod0", "sm_y"])

    def test_bodies_view_shows_the_collision_bodies_instead(self):
        self.assertEqual(qv.shown_names(self.NAMES, bodies=True), ["bo_sm_gd_crate_001"])


class ParseArgsTests(unittest.TestCase):
    def test_defaults(self):
        a = qv.parse_args(["--fbx", "a.fbx", "--out", "o"])
        self.assertEqual((a.fbx, a.blend, a.out, a.res, a.only, a.bodies), ("a.fbx", None, "o", 960, [], False))

    def test_options(self):
        a = qv.parse_args(["--blend", "a.blend", "--out", "o", "--res", "1280", "--only", "x", "--only", "y",
                           "--bodies"])
        self.assertEqual((a.blend, a.res, a.only, a.bodies), ("a.blend", 1280, ["x", "y"], True))

    def test_one_source_is_required(self):
        with self.assertRaises(SystemExit):
            qv.parse_args(["--out", "o"])


if __name__ == "__main__":
    unittest.main()
