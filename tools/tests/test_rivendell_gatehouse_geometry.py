#!/usr/bin/env python3
"""Unit tests for tools/oneoff/rivendell_gatehouse_geometry.py (pure Python, no Blender).

Run:  python -m unittest tools.tests.test_rivendell_gatehouse_geometry
"""
import importlib.util
import math
import os
import sys
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ONEOFF = os.path.join(TOOLS, "oneoff")
sys.path.insert(0, ONEOFF)


def load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(ONEOFF, name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


jp = load("rivendell_joint_profile")
gg = load("rivendell_gatehouse_geometry")


def area(poly):
    return 0.5 * sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1]
                     for i in range(len(poly)))


class WidthTests(unittest.TestCase):
    def test_gatehouse_is_26_667_wide_with_a_half_bay_stub(self):
        self.assertAlmostEqual(gg.XE, 40.0 / 3.0, places=9)
        self.assertAlmostEqual(gg.XE - gg.XB, jp.HALF_BAY, places=9)
        self.assertAlmostEqual(2 * gg.XE, 26.6666667, places=6)

    def test_uv_phase_of_the_ends_is_a_whole_tile(self):
        # U = TD (x - x_low): at the high end the U is TD * 26.667 = 12 tiles of 1 UV unit
        self.assertAlmostEqual(jp.TD * 2 * gg.XE, 12.0, places=9)

    def test_stub_starts_at_the_drum_plane_and_holds_no_pier(self):
        self.assertAlmostEqual(gg.STUB_X_IN, gg.XB, places=9)


class PathTests(unittest.TestCase):
    def test_mitre_of_a_straight_path_is_the_left_unit_normal(self):
        n = gg.mitre_normals([(0, 0), (1, 0), (2, 0)])
        for a in n:
            self.assertAlmostEqual(a[0], 0.0)
            self.assertAlmostEqual(a[1], 1.0)

    def test_mitre_of_a_right_angle_keeps_the_offset_distance(self):
        # heading +X then -Y (a right turn): offset d at the corner stays d from both edges
        n = gg.mitre_normals([(0, 0), (1, 0), (1, -1)])
        self.assertAlmostEqual(abs(n[1][0]), 1.0)
        self.assertAlmostEqual(abs(n[1][1]), 1.0)

    def test_end_normals_can_be_forced(self):
        n = gg.mitre_normals([(0, 0), (1, 1), (2, 1)], first_n=(0.0, 1.0))
        self.assertEqual(n[0], (0.0, 1.0))

    def test_front_path_runs_from_tangent_to_tangent(self):
        p = gg.front_path()
        self.assertAlmostEqual(p[0][0], -gg.XB)
        self.assertAlmostEqual(p[0][1], gg.DRUM_CY)
        self.assertAlmostEqual(p[-1][0], gg.XB)
        self.assertAlmostEqual(p[-1][1], gg.DRUM_CY)

    def test_front_path_is_mirror_symmetric(self):
        p = gg.front_path()
        self.assertEqual(len(p) % 2, 0)
        for a, b in zip(p, p[::-1]):
            self.assertAlmostEqual(a[0], -b[0], places=9)
            self.assertAlmostEqual(a[1], b[1], places=9)

    def test_front_path_outward_normal_points_to_plus_y_on_the_facade(self):
        p = gg.front_path()
        n = gg.mitre_normals(p)
        k = min(range(len(p)), key=lambda i: abs(p[i][0] - 0.0) + abs(p[i][1] - gg.HALF_D))
        self.assertGreater(n[k][1], 0.9)

    def test_right_drum_path_ends_on_the_stub_line_and_starts_at_the_facade(self):
        d = gg.drum_path(1)
        self.assertAlmostEqual(d[0][1], gg.FACADE_Y, places=9)
        self.assertAlmostEqual(d[-2][0], gg.XB, places=9)       # the tangent point
        self.assertAlmostEqual(d[-2][1], gg.DRUM_CY, places=9)
        self.assertLess(d[-1][1], jp.Y_OUT)                     # carried into the stub's mass
        self.assertAlmostEqual(d[-1][0], gg.XB, places=9)

    def test_left_drum_path_is_the_mirror_in_travel_order(self):
        r, l = gg.drum_path(1), gg.drum_path(-1)
        self.assertEqual(len(r), len(l))
        for a, b in zip(r, l[::-1]):
            self.assertAlmostEqual(a[0], -b[0], places=9)
            self.assertAlmostEqual(a[1], b[1], places=9)


class DrumTailTests(unittest.TestCase):
    def test_a_member_can_stop_on_the_face_of_the_stub_member_it_meets(self):
        for side in (-1, 1):
            d = gg.drum_path(side, tail_y=jp.Y_OUT + jp.STRING_PROUD)
            tail = d[-1] if side > 0 else d[0]
            self.assertAlmostEqual(tail[0], side * gg.XB, places=9)
            self.assertAlmostEqual(tail[1], jp.Y_OUT + jp.STRING_PROUD, places=9)
            self.assertEqual(len(d), len(gg.drum_path(side)))

    def test_the_default_tail_is_still_buried_in_the_stub(self):
        self.assertAlmostEqual(gg.drum_path(1)[-1][1], gg.STUB_TAIL_Y, places=9)


class OutlineTests(unittest.TestCase):
    def test_core_outline_is_simple_and_encloses_the_drums(self):
        o = gg.core_outline()
        self.assertGreater(abs(area(o)), 20.0 * 12.4 - 10.0)
        xs = [p[0] for p in o]
        self.assertAlmostEqual(min(xs), -gg.XB)
        self.assertAlmostEqual(max(xs), gg.XB)
        ys = [p[1] for p in o]
        self.assertAlmostEqual(min(ys), -gg.HALF_D)
        self.assertAlmostEqual(max(ys), gg.HALF_D, places=6)

    def test_foundation_outline_stands_out_by_the_batter_on_the_front_only(self):
        f = gg.foundation_outline()
        c = gg.core_outline()
        self.assertGreater(abs(area(f)), abs(area(c)))
        ys = [p[1] for p in f]
        self.assertAlmostEqual(min(ys), -gg.HALF_D)               # the inner face has no batter
        self.assertAlmostEqual(max(ys), gg.HALF_D + gg.BATTER, places=6)

    def test_drum_circle_is_tangent_to_the_stub_plane(self):
        self.assertAlmostEqual(gg.DRUM_CX + gg.DRUM_R, gg.XB, places=9)


class CylinderUvTests(unittest.TestCase):
    def test_density_is_td_along_the_arc_and_up(self):
        r = gg.DRUM_R
        u0, v0 = gg.cyl_uv(1, gg.DRUM_CX + r * math.cos(1.0), gg.DRUM_CY + r * math.sin(1.0), 5.0, r)
        u1, v1 = gg.cyl_uv(1, gg.DRUM_CX + r * math.cos(1.1), gg.DRUM_CY + r * math.sin(1.1), 6.0, r)
        self.assertAlmostEqual(u1 - u0, jp.TD * r * 0.1, places=9)
        self.assertAlmostEqual(v1 - v0, jp.TD * 1.0, places=9)

    def test_mirrored_drum_gets_the_same_uv(self):
        r = gg.DRUM_R
        a = gg.cyl_uv(1, gg.DRUM_CX + r * math.cos(0.7), gg.DRUM_CY + r * math.sin(0.7), 4.0, r)
        b = gg.cyl_uv(-1, -(gg.DRUM_CX + r * math.cos(0.7)), gg.DRUM_CY + r * math.sin(0.7), 4.0, r)
        self.assertAlmostEqual(a[0], b[0], places=9)
        self.assertAlmostEqual(a[1], b[1], places=9)


class RunTests(unittest.TestCase):
    def test_runs_are_the_maximal_true_stretches(self):
        self.assertEqual(gg.runs([False, True, True, False, True, False, True, True]), [(1, 2), (4, 4), (6, 7)])

    def test_no_runs(self):
        self.assertEqual(gg.runs([False, False]), [])
        self.assertEqual(gg.runs([]), [])

    def test_a_run_may_reach_both_ends(self):
        self.assertEqual(gg.runs([True, True, True]), [(0, 2)])


def convex(poly):
    n = len(poly)
    for i in range(n):
        a, b, c = poly[i], poly[(i + 1) % n], poly[(i + 2) % n]
        if (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0]) < -1e-12:
            return False
    return True


class StubRibTests(unittest.TestCase):
    def test_the_arch_is_centred_on_the_blind_arch_of_the_stub(self):
        self.assertAlmostEqual(gg.STUB_ARCH_X, 11.8, places=9)
        self.assertAlmostEqual(gg.XE - gg.STUB_RIB_REACH, gg.STUB_ARCH_X, places=9)
        self.assertAlmostEqual(gg.STUB_CORBEL_X + gg.STUB_RIB_REACH, gg.STUB_ARCH_X, places=9)

    def test_both_limbs_meet_at_the_same_apex_x(self):
        for spec in (jp.RIB_LONG, jp.RIB_LOW):
            a = jp.rib_curve(-1, spec, 0.0, 16, half_bay=gg.STUB_RIB_REACH)
            b = jp.rib_curve(1, spec, 0.0, 16, half_bay=gg.STUB_RIB_REACH)
            self.assertAlmostEqual(gg.XE + a[-1][0], gg.STUB_CORBEL_X + b[-1][0], places=9)
            self.assertAlmostEqual(gg.XE + a[-1][0], gg.STUB_ARCH_X, places=9)
            self.assertAlmostEqual(a[-1][1], b[-1][1], places=9)

    def test_the_arch_is_smaller_than_a_bay_and_the_foot_stands_clear_of_the_drum(self):
        for spec in (jp.RIB_LONG, jp.RIB_LOW):
            self.assertGreater(gg.STUB_RIB_REACH, spec[7] + 0.1)
            foot = gg.STUB_CORBEL_X + spec[7] - jp.rib_width(spec[3], spec[4], 0.0) / 2.0
            self.assertGreater(foot, gg.XB + 0.1)

    def test_no_limb_reaches_past_the_string_course(self):
        top = max(jp.RIB_LONG[1], jp.RIB_LOW[1]) + 0.15
        self.assertLess(top, jp.STRING_Z0)

    def test_the_limb_stands_between_the_blind_arch_and_the_pier_or_corbel(self):
        arch_half = 0.8
        self.assertGreater(gg.STUB_ARCH_X - arch_half, gg.STUB_CORBEL_X + jp.COLLAR_HALF_W - 0.4)
        self.assertLess(gg.STUB_ARCH_X + arch_half, gg.XE - jp.COLLAR_HALF_W)


class CorbelTests(unittest.TestCase):
    def test_corbel_is_a_convex_counter_clockwise_bracket(self):
        c = gg.corbel_profile()
        self.assertGreater(area(c), 0.2)
        self.assertTrue(convex(c))

    def test_corbel_is_buried_in_the_wall_and_holds_the_rib_foot(self):
        c = gg.corbel_profile()
        ys = [p[0] for p in c]
        zs = [p[1] for p in c]
        self.assertLess(min(ys), jp.Y_OUT)
        front = max(ys)
        for spec in (jp.RIB_LONG, jp.RIB_LOW):
            self.assertGreater(front, jp.Y_OUT + spec[5] + 0.01)
            self.assertGreater(spec[0], min(zs))
            self.assertLess(spec[0] + 0.05, max(zs))


class StubParapetTests(unittest.TestCase):
    def test_span_starts_on_the_drums_tangent_plane_and_ends_on_the_joint(self):
        # the ring parapet ends on that plane, so the two meet with no slit between them
        a = gg.stub_parapet_span(1)
        self.assertAlmostEqual(a[0], gg.XB)
        self.assertAlmostEqual(a[1], gg.XE)
        b = gg.stub_parapet_span(-1)
        self.assertAlmostEqual(b[0], -gg.XE)
        self.assertAlmostEqual(b[1], -gg.XB)

    def test_no_opening_meets_the_ring(self):
        for side in (-1, 1):
            x0, x1 = sorted(gg.stub_parapet_span(side))
            centres = [x0 + c for c in jp.parapet_openings(x1 - x0)]
            ring_edge = side * (gg.XB + 0.10)   # the ring and its frieze end at the outside of the path
            for c in centres:
                self.assertGreater(abs(c - ring_edge) - jp.OPEN_W / 2.0, 0.4)
                self.assertGreater(abs(c - side * gg.XE) - jp.OPEN_W / 2.0, 0.4)


class RingTailTests(unittest.TestCase):
    def test_each_member_of_the_drum_ring_stops_on_the_front_face_of_the_stub_member_it_meets(self):
        sec = jp.section()
        self.assertAlmostEqual(gg.ring_tail_y("slab"), max(p[0] for p in sec["parapet"]), places=9)
        self.assertAlmostEqual(gg.ring_tail_y("frieze"), max(p[0] for p in sec["frieze"]), places=9)
        self.assertAlmostEqual(gg.ring_tail_y("cap"), max(p[0] for p in sec["parapet_cap"]), places=9)

    def test_the_slab_closes_the_corner_with_the_stubs_slab_without_a_slit(self):
        # the ring slab ends in the plane of the stub slab's outer face, so the two share only an edge
        self.assertAlmostEqual(gg.ring_tail_y("slab"), jp.PARAPET_Y1, places=9)


class EdgeVTests(unittest.TestCase):
    BAND = [(0.0, 3.4), (0.08, 3.4), (0.08, 4.0), (0.0, 4.0)]

    def test_the_front_of_the_band_takes_the_whole_strip(self):
        v0, v1 = jp.STRIPS["S3"]
        a, b = gg.edge_v(self.BAND, 1, v0, v1)
        self.assertAlmostEqual(a, v0)
        self.assertAlmostEqual(b, v1)

    def test_the_top_and_bottom_take_the_whole_strip_across_their_depth(self):
        v0, v1 = jp.STRIPS["S3"]
        a, b = gg.edge_v(self.BAND, 0, v0, v1)
        self.assertAlmostEqual(a, v0)
        self.assertAlmostEqual(b, v1)
        a, b = gg.edge_v(self.BAND, 2, v0, v1)
        self.assertAlmostEqual(a, v1)
        self.assertAlmostEqual(b, v0)

    def test_a_member_with_several_faces_shares_one_box_like_the_wall(self):
        cap = [(0.0, 3.0), (0.1, 3.0), (0.1, 3.1), (0.04, 3.16), (0.04, 3.2), (0.0, 3.2)]
        v0, v1 = jp.STRIPS["S2"]
        a, b = gg.edge_v(cap, 1, v0, v1)    # the vertical front, z 3.0 to 3.1 of 3.0 to 3.2
        self.assertAlmostEqual(a, v0)
        self.assertAlmostEqual(b, v0 + (v1 - v0) * 0.5)
        a, b = gg.edge_v(cap, 3, v0, v1)    # the vertical upper face, z 3.16 to 3.2
        self.assertAlmostEqual(a, v0 + (v1 - v0) * 0.8)
        self.assertAlmostEqual(b, v1)

    def test_values_stay_in_the_strip(self):
        v0, v1 = jp.STRIPS["S2"]
        cap = [(0.0, 3.0), (0.1, 3.0), (0.1, 3.1), (0.04, 3.16), (0.04, 3.2), (0.0, 3.2)]
        for k in range(len(cap)):
            for v in gg.edge_v(cap, k, v0, v1):
                self.assertGreaterEqual(v, v0 - 1e-12)
                self.assertLessEqual(v, v1 + 1e-12)


class KitUvTests(unittest.TestCase):
    def test_density_is_td_along_u_and_v_stays_inside_the_strip(self):
        v0, v1 = jp.STRIPS["S1"]
        n = (0.0, 1.0, 0.0)
        pts = [(1.0, 5.0, 2.0), (1.3, 5.0, 2.0), (1.3, 5.0, 2.2), (1.0, 5.0, 2.2)]
        uv = gg.kit_face_uv(n, pts, "S1", x_low=-13.0)
        self.assertAlmostEqual(uv[1][0] - uv[0][0], jp.TD * 0.3, places=9)
        for u, v in uv:
            self.assertGreaterEqual(v, v0)
            self.assertLessEqual(v, v1)
        self.assertAlmostEqual(uv[0][0], jp.TD * 14.0, places=9)

    def test_v_follows_the_height_at_td_for_a_wall_face(self):
        n = (0.0, -1.0, 0.0)
        pts = [(0.0, 0.0, 1.0), (0.2, 0.0, 1.0), (0.2, 0.0, 1.2)]
        uv = gg.kit_face_uv(n, pts, "S1")
        self.assertAlmostEqual(uv[2][1] - uv[1][1], jp.TD * 0.2, places=9)

    def test_a_floor_face_runs_v_along_y(self):
        n = (0.0, 0.0, 1.0)
        pts = [(0.0, 0.0, 2.0), (0.2, 0.0, 2.0), (0.2, 0.2, 2.0)]
        uv = gg.kit_face_uv(n, pts, "S1")
        self.assertAlmostEqual(uv[2][1] - uv[1][1], jp.TD * 0.2, places=9)

    def test_a_face_taller_than_the_strip_is_clamped_not_spilled(self):
        v0, v1 = jp.STRIPS["S1"]
        n = (1.0, 0.0, 0.0)
        pts = [(0.0, 0.0, 0.0), (0.0, 0.3, 0.0), (0.0, 0.3, 5.0), (0.0, 0.0, 5.0)]
        for u, v in gg.kit_face_uv(n, pts, "S1"):
            self.assertGreaterEqual(v, v0)
            self.assertLessEqual(v, v1)

    def test_the_face_is_centred_on_the_strip_middle(self):
        v0, v1 = jp.STRIPS["S1"]
        pts = [(0.0, 0.0, 3.0), (0.2, 0.0, 3.0), (0.2, 0.0, 3.1)]
        uv = gg.kit_face_uv((0.0, 1.0, 0.0), pts, "S1")
        mid = sum(p[1] for p in uv) / 3.0
        self.assertAlmostEqual(mid, (v0 + v1) / 2.0, delta=jp.TD * 0.1)


class PolylineTests(unittest.TestCase):
    L = [(0.0, 0.0), (2.0, 0.0), (2.0, 2.0)]    # 2 m along X, then 2 m up

    def test_a_point_on_the_line_has_its_arc_length_and_no_distance(self):
        s, d = gg.nearest_on_polyline(self.L, 1.0, 0.0)
        self.assertAlmostEqual(s, 1.0)
        self.assertAlmostEqual(d, 0.0)
        s, d = gg.nearest_on_polyline(self.L, 2.0, 1.5)
        self.assertAlmostEqual(s, 3.5)
        self.assertAlmostEqual(d, 0.0)

    def test_a_point_off_the_line_takes_the_nearest_foot(self):
        s, d = gg.nearest_on_polyline(self.L, 1.0, 0.7)
        self.assertAlmostEqual(s, 1.0)
        self.assertAlmostEqual(d, 0.7)

    def test_the_ends_are_clamped(self):
        s, d = gg.nearest_on_polyline(self.L, -1.0, 0.0)
        self.assertAlmostEqual(s, 0.0)
        self.assertAlmostEqual(d, 1.0)

    def test_the_door_opening_is_mirror_symmetric_about_its_apex(self):
        pts = gg.door_opening_outline()
        apex = max(range(len(pts)), key=lambda i: pts[i][1])
        self.assertAlmostEqual(pts[apex][0], 0.0)
        s0, _ = gg.nearest_on_polyline(pts, 0.0, pts[apex][1])
        sl, _ = gg.nearest_on_polyline(pts, -1.9, 8.0)
        sr, _ = gg.nearest_on_polyline(pts, 1.9, 8.0)
        self.assertAlmostEqual(sl - s0, -(sr - s0), places=6)


class PolygonTests(unittest.TestCase):
    SQ = [(0.0, 0.0), (4.0, 0.0), (4.0, 4.0), (0.0, 4.0)]

    def test_inside_and_outside(self):
        self.assertTrue(gg.point_in_polygon((2.0, 2.0), self.SQ))
        self.assertFalse(gg.point_in_polygon((5.0, 2.0), self.SQ))
        self.assertFalse(gg.point_in_polygon((2.0, -0.1), self.SQ))

    def test_distance_to_the_edge(self):
        self.assertAlmostEqual(gg.edge_distance((1.0, 2.0), self.SQ), 1.0)
        self.assertAlmostEqual(gg.edge_distance((2.0, 3.5), self.SQ), 0.5)

    def test_the_core_outline_holds_the_drum_centre_and_not_the_outside_corner(self):
        o = gg.core_outline()
        self.assertTrue(gg.point_in_polygon((gg.DRUM_CX, gg.DRUM_CY), o))
        self.assertFalse(gg.point_in_polygon((gg.XB - 0.3, gg.FACADE_Y + 0.9), o))


class PavilionFitTests(unittest.TestCase):
    # a platform: a disc of radius 2.6 round the floor centre, with a spur of stairs reaching 3.9 along +x
    DISC = [(2.6 * math.cos(a * math.pi / 12), 2.6 * math.sin(a * math.pi / 12)) for a in range(24)]
    SPUR = [(3.9, 0.0), (3.2, 1.2), (3.2, -1.2)]

    def centre(self, side):
        return (side * gg.DRUM_CX, gg.DRUM_CY)

    def test_the_spur_points_toward_the_deck(self):
        for side in (-1, 1):
            psi = gg.pavilion_rotation(side, self.DISC + self.SPUR, self.centre(side), 1.0, gg.core_outline(), 0.05)
            self.assertIsNotNone(psi)
            tip = (side * gg.DRUM_CX + 3.9 * math.cos(psi), gg.DRUM_CY + 3.9 * math.sin(psi))
            self.assertLess(abs(tip[0]), abs(self.centre(side)[0]))   # nearer the middle of the piece
            self.assertLess(tip[1], gg.DRUM_CY)                       # and toward the inner side

    def test_every_point_stands_inside_the_core_with_the_margin(self):
        for side in (-1, 1):
            pts = self.DISC + self.SPUR
            psi = gg.pavilion_rotation(side, pts, self.centre(side), 1.0, gg.core_outline(), 0.05)
            for pt in pts:
                p = gg.place_point(pt, self.centre(side), 1.0, psi)
                self.assertTrue(gg.point_in_polygon(p, gg.core_outline()))
                self.assertGreaterEqual(gg.edge_distance(p, gg.core_outline()), 0.05 - 1e-9)

    def test_the_two_sides_are_mirror_images(self):
        a = gg.pavilion_rotation(-1, self.DISC + self.SPUR, self.centre(-1), 1.0, gg.core_outline(), 0.05)
        b = gg.pavilion_rotation(1, self.DISC + self.SPUR, self.centre(1), 1.0, gg.core_outline(), 0.05)
        self.assertAlmostEqual(math.sin(a), math.sin(b), places=6)
        self.assertAlmostEqual(math.cos(a), -math.cos(b), places=6)

    def test_a_platform_that_cannot_fit_gives_none(self):
        big = [(9.0 * math.cos(a * math.pi / 12), 9.0 * math.sin(a * math.pi / 12)) for a in range(24)]
        self.assertIsNone(gg.pavilion_rotation(1, big, self.centre(1), 1.0, gg.core_outline(), 0.05))

    def test_the_old_placing_with_the_spur_outward_fails_the_test(self):
        # v02 turned the floor offset toward the deck, so the spur pointed away from it
        side = 1
        psi = math.atan2(1.0, side)   # outward: (+x, +y)
        bad = [gg.place_point(p, self.centre(side), 1.0, psi) for p in self.SPUR]
        self.assertTrue(any(not gg.point_in_polygon(p, gg.core_outline()) or gg.edge_distance(p, gg.core_outline()) < 0.05
                            for p in bad))


class PassageSootTests(unittest.TestCase):
    def test_outside_the_passage_nothing_changes(self):
        self.assertEqual(gg.passage_soot(8.0, 0.0, 6.0), (1.0, 1.0, 1.0))
        self.assertEqual(gg.passage_soot(0.0, 0.0, 11.5), (1.0, 1.0, 1.0))      # over the vault
        self.assertEqual(gg.passage_soot(0.0, gg.HALF_D, 5.0), (1.0, 1.0, 1.0))  # the rim of the door

    def test_the_vault_is_darker_than_the_walls(self):
        low = gg.passage_soot(4.2, 0.0, 3.0)
        high = gg.passage_soot(0.0, 0.0, 9.5)
        self.assertLess(sum(high), sum(low))

    def test_the_soot_can_be_seen(self):
        # at least a quarter off the red channel in the middle of the vault
        self.assertLess(gg.passage_soot(0.0, 0.0, 9.5)[0], 0.75)

    def test_the_middle_of_the_hall_is_darker_than_its_ends(self):
        mid = gg.passage_soot(0.0, 0.0, 9.5)
        end = gg.passage_soot(0.0, gg.HALL_Y, 9.5)
        self.assertLess(sum(mid), sum(end))

    def test_the_door_tunnel_fades_out_toward_the_rim(self):
        a = gg.passage_soot(0.0, 5.0, 6.0)
        b = gg.passage_soot(0.0, 5.8, 6.0)
        self.assertLess(sum(a), sum(b))
        self.assertLessEqual(sum(b), 3.0)

    def test_damp_at_the_floor_leans_green(self):
        r, g, b = gg.passage_soot(4.2, 0.0, 0.0)
        self.assertLess(r, g)
        self.assertLess(b, g)

    def test_channels_stay_in_range_and_the_passage_is_mirror_symmetric(self):
        for x in (-4.2, -2.0, 0.0, 3.0, 4.2):
            for y in (-6.0, -3.0, 0.0, 4.0, 5.5):
                for z in (0.0, 2.0, 6.0, 9.6):
                    p = gg.passage_soot(x, y, z)
                    for c in p:
                        self.assertGreaterEqual(c, gg.SOOT_MIN)
                        self.assertLessEqual(c, 1.0)
                    q = gg.passage_soot(-x, -y, z)
                    for c, d in zip(p, q):
                        self.assertAlmostEqual(c, d, places=9)


# ---- variant b: the spired gatehouse -----------------------------------------------------------------
class TurretTests(unittest.TestCase):
    def test_the_octagon_has_flats_on_the_axes_and_the_given_apothem(self):
        o = gg.octagon(1.0, 2.0, 2.5)
        self.assertEqual(len(o), 8)
        self.assertGreater(area(o), 0.0)                       # counter-clockwise
        self.assertAlmostEqual(max(p[0] for p in o), 1.0 + 2.5, places=9)
        self.assertAlmostEqual(min(p[0] for p in o), 1.0 - 2.5, places=9)
        self.assertAlmostEqual(max(p[1] for p in o), 2.0 + 2.5, places=9)
        self.assertAlmostEqual(area(o), 8 * 2.5 ** 2 * math.tan(math.pi / 8), places=9)

    def test_the_outer_flat_lies_in_the_stub_plane_and_starts_at_the_stub_face(self):
        o = gg.turret_outline(1)
        flat = [p for p in o if abs(p[0] - gg.XB) < 1e-9]
        self.assertEqual(len(flat), 2)
        self.assertAlmostEqual(min(p[1] for p in flat), jp.Y_OUT, places=9)
        self.assertAlmostEqual(gg.TUR_CX + gg.TUR_A, gg.XB, places=9)

    def test_the_two_turrets_are_mirror_images_and_both_counter_clockwise(self):
        r, l = gg.turret_outline(1), gg.turret_outline(-1)
        self.assertGreater(area(r), 0.0)
        self.assertGreater(area(l), 0.0)
        for a, b in zip(sorted(r), sorted((-x, y) for x, y in l)):
            self.assertAlmostEqual(a[0], b[0], places=9)
            self.assertAlmostEqual(a[1], b[1], places=9)

    def test_the_turret_stands_proud_of_the_facade_and_clear_of_the_hall(self):
        o = gg.turret_outline(1)
        self.assertGreater(max(p[1] for p in o), gg.FACADE_Y + 0.4)
        self.assertGreater(min(p[0] for p in o), gg.HALL_HALF_W + 0.6)

    def test_the_facade_meets_the_chamfer_between_two_vertices(self):
        o = gg.turret_outline(1)
        corner = (gg.TUR_CX - gg.TUR_A, gg.TUR_CY + gg.TUR_A * math.tan(math.pi / 8))   # the upper vertex of the inner flat
        lo = min(o, key=lambda p: math.hypot(p[0] - corner[0], p[1] - corner[1]))
        self.assertAlmostEqual(math.hypot(lo[0] - corner[0], lo[1] - corner[1]), 0.0, places=9)
        self.assertLess(lo[1], gg.FACADE_Y)
        self.assertGreater(gg.FACADE_JOIN_X, lo[0])
        self.assertAlmostEqual(gg.FACADE_JOIN_X - lo[0], gg.FACADE_Y - lo[1], places=9)   # a 45 degree edge


class PathBTests(unittest.TestCase):
    def test_the_front_path_runs_from_the_stub_line_to_the_stub_line(self):
        p = gg.front_path_b()
        self.assertEqual((round(p[0][0], 9), round(p[0][1], 9)), (-gg.XB, gg.STUB_TAIL_Y))
        self.assertEqual((round(p[-1][0], 9), round(p[-1][1], 9)), (gg.XB, gg.STUB_TAIL_Y))
        for a, b in zip(p, p[::-1]):
            self.assertAlmostEqual(a[0], -b[0], places=9)
            self.assertAlmostEqual(a[1], b[1], places=9)

    def test_the_path_keeps_the_outside_on_the_left(self):
        p = gg.front_path_b()
        n = gg.mitre_normals(p)
        k = min(range(len(p)), key=lambda i: abs(p[i][0] - gg.PORTAL_HALF_W - 1.0) + abs(p[i][1] - gg.FACADE_Y))
        self.assertGreater(n[k][1], 0.9)                       # on the facade the normal is +Y
        self.assertAlmostEqual(n[0][0], -1.0)                   # the left end flat faces -X
        self.assertAlmostEqual(n[-1][0], 1.0)                   # the right end flat faces +X

    def test_the_core_outline_is_counter_clockwise_and_has_no_three_collinear_points(self):
        o = gg.core_outline_b()
        self.assertGreater(area(o), 20.0 * 11.6)                # the box to the facade, plus the frame and the turrets
        n = len(o)
        for i in range(n):
            a, b, c = o[i - 1], o[i], o[(i + 1) % n]
            cross = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0])
            self.assertGreater(abs(cross), 1e-6, f"collinear at {b}")
        self.assertAlmostEqual(max(p[0] for p in o), gg.XB)
        self.assertAlmostEqual(min(p[0] for p in o), -gg.XB)
        self.assertAlmostEqual(min(p[1] for p in o), -gg.HALF_D)
        self.assertAlmostEqual(max(p[1] for p in o), gg.HALF_D, places=9)   # the door frame is the outermost

    def test_the_end_flat_is_one_straight_edge(self):
        o = gg.core_outline_b()
        right = [p for p in o if abs(p[0] - gg.XB) < 1e-9]
        self.assertEqual(len(right), 2)

    def test_the_foundation_stands_out_by_the_batter_on_the_front_and_not_inside(self):
        f = gg.foundation_outline_b()
        self.assertGreater(abs(area(f)), area(gg.core_outline_b()))
        self.assertAlmostEqual(min(p[1] for p in f), -gg.HALF_D)
        self.assertAlmostEqual(max(p[1] for p in f), gg.HALF_D + gg.BATTER, places=9)
        n = len(f)
        for i in range(n):
            a, b, c = f[i - 1], f[i], f[(i + 1) % n]
            cross = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0])
            self.assertGreater(abs(cross), 1e-6, f"collinear at {b}")

    def test_no_three_foundation_points_share_a_line_across_the_stubs(self):
        # three collinear corners make the tessellator emit zero-area triangles (the first build had four of them)
        f = gg.foundation_outline_b()
        for y in {round(p[1], 6) for p in f if abs(p[0]) >= gg.XB - 1e-9 and p[1] < 3.0}:
            self.assertLessEqual(sum(1 for p in f if abs(p[1] - y) < 1e-6 and p[1] > -gg.HALF_D + 1e-6), 2, f"y = {y}")

    def test_the_plinth_paths_leave_the_door_frame_unbattered(self):
        r, l = gg.plinth_path_b(1), gg.plinth_path_b(-1)
        self.assertTrue(all(q[0] >= gg.PORTAL_HALF_W - 1e-9 for q in r))
        self.assertAlmostEqual(r[0][0], gg.PORTAL_HALF_W)
        self.assertAlmostEqual(r[0][1], gg.FACADE_Y)
        self.assertAlmostEqual(r[-1][0], gg.XB)
        self.assertAlmostEqual(r[-1][1], gg.STUB_TAIL_Y)
        self.assertEqual(len(r), len(l))
        for a, b in zip(r, l[::-1]):                            # the left path is the mirror, in travel order
            self.assertAlmostEqual(a[0], -b[0], places=9)
            self.assertAlmostEqual(a[1], b[1], places=9)


class DiagonalUvTests(unittest.TestCase):
    def test_a_chamfer_keeps_the_density_along_the_face_and_up(self):
        n = (math.sqrt(0.5), math.sqrt(0.5), 0.0)
        t = (-n[1], n[0])
        p = [(3.0, 5.0, 2.0), (3.0 + t[0], 5.0 + t[1], 2.0), (3.0, 5.0, 3.0)]
        uv = [gg.diag_uv(n, c) for c in p]
        lo, hi = jp.uv_scale_axes(p, uv)
        self.assertAlmostEqual(lo, jp.TD, places=9)
        self.assertAlmostEqual(hi, jp.TD, places=9)

    def test_face_uv_leaves_an_axis_face_to_the_pinned_world_rule(self):
        for n, co in (((0.0, 1.0, 0.0), (2.0, 5.0, 3.0)), ((1.0, 0.0, 0.0), (10.0, 3.0, 3.0))):
            self.assertEqual(gg.face_uv_b(n, co, -gg.XE), jp.world_uv(n, co, -gg.XE))

    def test_face_uv_sends_a_chamfer_to_the_diagonal_rule(self):
        n = (-math.sqrt(0.5), math.sqrt(0.5), 0.0)
        co = (6.0, 5.0, 4.0)
        self.assertEqual(gg.face_uv_b(n, co, -gg.XE), gg.diag_uv(n, co))


class PlanarUvTests(unittest.TestCase):
    def test_a_sloped_face_keeps_the_density_both_ways(self):
        for n in ((0.6, 0.0, 0.8), (0.0, -0.6, 0.8), (math.sqrt(0.5) * 0.8, math.sqrt(0.5) * 0.8, 0.6)):
            a = (1.0, 2.0, 3.0)
            e1 = (0.0, 0.0, 1.0)
            d = sum(x * y for x, y in zip(e1, n))
            e1 = tuple(x - d * y for x, y in zip(e1, n))
            ln = math.sqrt(sum(x * x for x in e1))
            e1 = tuple(x / ln for x in e1)
            e2 = (n[1] * e1[2] - n[2] * e1[1], n[2] * e1[0] - n[0] * e1[2], n[0] * e1[1] - n[1] * e1[0])
            p = [a, tuple(x + y for x, y in zip(a, e1)), tuple(x + y for x, y in zip(a, e2))]
            uv = [gg.planar_uv(n, c) for c in p]
            lo, hi = jp.uv_scale_axes(p, uv)
            self.assertAlmostEqual(lo, jp.TD, places=7)
            self.assertAlmostEqual(hi, jp.TD, places=7)

    def test_u_is_level_and_v_follows_the_slope(self):
        n = (0.0, 0.8, 0.6)                                    # a face that leans back
        u0, v0 = gg.planar_uv(n, (0.0, 0.0, 0.0))
        u1, v1 = gg.planar_uv(n, (1.0, 0.0, 0.0))
        self.assertAlmostEqual(abs(u1 - u0), jp.TD, places=9)
        self.assertAlmostEqual(v1, v0, places=9)
        _u2, v2 = gg.planar_uv(n, (0.0, -0.6, 0.8))            # up the slope
        self.assertAlmostEqual(abs(v2 - v0), jp.TD, places=9)

    def test_a_flat_face_takes_x_and_y(self):
        u, v = gg.planar_uv((0.0, 0.0, 1.0), (2.0, 3.0, 4.0))
        self.assertAlmostEqual(abs(u), jp.TD * 2.0, places=9)
        self.assertAlmostEqual(abs(v), jp.TD * 3.0, places=9)


class PolyEdgeTests(unittest.TestCase):
    def test_an_edge_gives_its_middle_direction_outward_normal_and_length(self):
        sq = [(0.0, 0.0), (2.0, 0.0), (2.0, 2.0), (0.0, 2.0)]
        mid, d, n, ln = gg.poly_edges(sq)[0]
        self.assertEqual(mid, (1.0, 0.0))
        self.assertEqual(d, (1.0, 0.0))
        self.assertEqual(n, (0.0, -1.0))                       # outward on a counter-clockwise polygon
        self.assertEqual(ln, 2.0)

    def test_every_turret_normal_points_away_from_the_centre(self):
        for side in (-1, 1):
            c = (side * gg.TUR_CX, gg.TUR_CY)
            for mid, d, n, ln in gg.poly_edges(gg.turret_outline(side)):
                self.assertGreater((mid[0] - c[0]) * n[0] + (mid[1] - c[1]) * n[1], gg.TUR_A - 1e-9)
                self.assertAlmostEqual(math.hypot(*n), 1.0, places=9)
                self.assertAlmostEqual(ln, 2 * gg.TUR_A * math.tan(math.pi / 8), places=9)


class ParapetBTests(unittest.TestCase):
    def test_the_stub_parapet_runs_into_the_turret_and_ends_on_the_joint(self):
        a = gg.stub_parapet_span_b(1)
        self.assertAlmostEqual(a[1], gg.XE)
        self.assertLess(a[0], gg.XB)
        b = gg.stub_parapet_span_b(-1)
        self.assertAlmostEqual(b[0], -a[1])
        self.assertAlmostEqual(b[1], -a[0])

    def test_the_end_of_a_stub_parapet_part_is_buried_in_the_turret(self):
        po = jp.PARAPET_CAP_OVER
        y0, y1 = jp.PARAPET_Y0 - po, jp.PARAPET_Y1 + po       # the cap is the widest member
        for side in (-1, 1):
            x0, x1 = gg.stub_parapet_span_b(side)
            xe = x1 if side < 0 else x0
            outline = gg.turret_outline(side)
            for y in (y0, y1):
                self.assertTrue(gg.point_in_polygon((xe, y), outline), f"end of the part at {(xe, y)}")

    def test_no_stub_opening_is_cut_by_the_turret(self):
        for side in (-1, 1):
            x0, x1 = sorted(gg.stub_parapet_span_b(side))
            centres = [x0 + c for c in jp.parapet_openings(x1 - x0)]
            for c in centres:
                self.assertGreater(abs(c) - jp.OPEN_W / 2.0 - gg.XB, 0.1)
                self.assertGreater(gg.XE - abs(c) - jp.OPEN_W / 2.0, 0.4)

    def test_the_wing_runs_from_the_door_frame_into_the_turret(self):
        po = jp.PARAPET_CAP_OVER
        dy = gg.FACADE_Y - jp.Y_OUT
        ys = (jp.PARAPET_Y0 - po + dy, jp.PARAPET_Y1 + po + dy)
        outline = gg.turret_outline(1)
        for y in ys:
            self.assertTrue(gg.point_in_polygon((gg.WING_END_X, y), outline))
        self.assertGreater(gg.WING_END_X - gg.PORTAL_HALF_W, 1.0)
        self.assertEqual(len(jp.parapet_openings(gg.WING_END_X - gg.PORTAL_HALF_W)), 1)


class SpireTests(unittest.TestCase):
    def test_the_profile_narrows_from_the_eave_to_a_point(self):
        prof = gg.spire_profile()
        self.assertEqual(prof[-1][1], 0.0)
        for (z0, a0), (z1, a1) in zip(prof, prof[1:]):
            self.assertGreater(z1, z0)
            self.assertLess(a1, a0)

    def test_the_eave_overhangs_the_lantern_and_the_tip_is_slender(self):
        prof = gg.spire_profile()
        self.assertGreater(prof[0][1], gg.LANTERN_A + 0.3)
        self.assertGreaterEqual(prof[0][0], gg.LANTERN_Z1)
        self.assertGreater(prof[-1][0] - prof[0][0], 5.0)       # a tall spire
        self.assertGreater((prof[-1][0] - prof[0][0]) / (2 * prof[0][1]), 1.5)   # taller than a cone of 1.5 times its width
        self.assertLess(prof[-1][0], 30.0)

    def test_the_lantern_columns_leave_room_for_an_arch_between_them(self):
        a = gg.LANTERN_A
        chord = 2.0 * a * math.tan(math.pi / 8)
        self.assertGreater(chord - 2 * gg.LANTERN_COL_R, gg.LANTERN_ARCH_W + 0.1)
        self.assertGreater(gg.LANTERN_Z1 - gg.LANTERN_Z0, gg.LANTERN_ARCH_H)

    def test_the_lantern_stands_inside_the_shaft_and_on_its_cornice(self):
        self.assertLess(gg.LANTERN_A + gg.LANTERN_COL_R * 1.5, gg.TUR_A)
        self.assertGreaterEqual(gg.LANTERN_Z0, gg.SHAFT_TOP_Z)


class RoofTests(unittest.TestCase):
    def test_the_roof_stands_clear_of_the_turrets_and_over_the_deck(self):
        self.assertLess(gg.ROOF_HX, gg.TUR_CX - gg.TUR_A)
        self.assertGreater(gg.ROOF_EAVE_Z - jp.Z_DECK, 3.0)     # the walk path has more than 1.9 m
        self.assertGreater(gg.ROOF_RIDGE_Z, gg.ROOF_EAVE_Z)
        self.assertLess(gg.ROOF_HY, gg.HALF_D)

    def test_columns_stand_clear_of_the_hole_and_the_throwers(self):
        for x, y in gg.ROOF_COLUMNS:
            self.assertGreater(abs(x) - gg.ROOF_COL_HALF, gg.HOLE_X + 1.0)   # no column within a metre of a thrower
        self.assertEqual(len(gg.ROOF_COLUMNS), len(set(gg.ROOF_COLUMNS)))
        for x, y in gg.ROOF_COLUMNS:
            self.assertIn((-x, y), gg.ROOF_COLUMNS)              # mirror symmetric

    def test_a_throw_ray_in_the_plane_of_the_throwers_meets_no_column(self):
        for _x, y in gg.ROOF_COLUMNS:
            self.assertGreater(abs(y - gg.THROW_Y), 0.5)

    def test_columns_stand_inside_the_roof_and_clear_of_the_parapet(self):
        wing_inner = jp.PARAPET_Y0 + gg.FACADE_Y - jp.Y_OUT - jp.PARAPET_CAP_OVER   # inner face of the wing's cap
        for x, y in gg.ROOF_COLUMNS:
            self.assertLess(abs(x) + gg.ROOF_COL_HALF, gg.ROOF_HX)
            self.assertLess(abs(y) + gg.ROOF_COL_HALF, wing_inner)


def dist_to_polyline(p, pts):
    best = None
    for (ax, az), (bx, bz) in zip(pts, pts[1:]):
        dx, dz = bx - ax, bz - az
        seg = dx * dx + dz * dz
        t = 0.0 if seg < 1e-18 else min(max(((p[0] - ax) * dx + (p[1] - az) * dz) / seg, 0.0), 1.0)
        d = math.hypot(p[0] - (ax + t * dx), p[1] - (az + t * dz))
        best = d if best is None else min(best, d)
    return best


def ogee_outer(w, spring, h, steps=14):
    """The outer outline of the domed gatehouse's door ring: the ogee of the builder (feet on Z 0)."""
    p = [(-w, spring), (-w, spring + 0.80 * h), (-0.31 * w, spring + 0.62 * h), (0.0, spring + h)]
    half = []
    for i in range(steps + 1):
        t = i / steps
        a, b, c, d = (1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t ** 2, t ** 3
        half.append((a * p[0][0] + b * p[1][0] + c * p[2][0] + d * p[3][0],
                     a * p[0][1] + b * p[1][1] + c * p[2][1] + d * p[3][1]))
    return [(-w, 0.0)] + half + [(-x, y) for x, y in reversed(half[:-1])] + [(w, 0.0)]


def lancet_outer(w, spring, rise):
    return [(-w, 0.0)] + [(u, spring + v) for u, v in gg.lancet(w, rise)] + [(w, 0.0)]


class DoorRingTests(unittest.TestCase):
    SPRING = 7.0

    def pairs(self, kind="ogee"):
        inner = gg.opening_loop(gg.DOOR_HALF_W, self.SPRING, 3.0, 0.0)
        outer = (ogee_outer(gg.DOOR_HALF_W + 0.55, self.SPRING, 3.9) if kind == "ogee"
                 else lancet_outer(gg.DOOR_HALF_W + 0.55, self.SPRING, 3.55))
        return inner, outer, gg.ring_pairs(inner, outer, self.SPRING)

    def test_pairs_run_from_foot_to_foot_and_are_mirror_symmetric(self):
        for kind in ("ogee", "lancet"):
            _i, _o, p = self.pairs(kind)
            self.assertEqual(len(p) % 2, 1)
            self.assertEqual(p[0][0], (-gg.DOOR_HALF_W, 0.0))
            self.assertAlmostEqual(p[0][1][0], -(gg.DOOR_HALF_W + 0.55))
            for a, b in zip(p, p[::-1]):
                for k in (0, 1):
                    self.assertAlmostEqual(a[k][0], -b[k][0], places=9)
                    self.assertAlmostEqual(a[k][1], b[k][1], places=9)

    def test_the_middle_pair_is_the_apex_of_both_outlines(self):
        inner, outer, p = self.pairs()
        mid = p[len(p) // 2]
        self.assertAlmostEqual(mid[0][0], 0.0, places=9)
        self.assertAlmostEqual(mid[1][0], 0.0, places=9)
        self.assertAlmostEqual(mid[0][1], max(z for _x, z in inner), places=9)
        self.assertAlmostEqual(mid[1][1], max(z for _x, z in outer), places=9)

    def test_every_inner_point_lies_on_the_opening_and_every_outer_point_is_a_vertex_of_the_outline(self):
        for kind in ("ogee", "lancet"):
            inner, outer, p = self.pairs(kind)
            for a, b in p:
                self.assertLess(dist_to_polyline(a, inner), 1e-9)
                self.assertLess(min(math.hypot(b[0] - q[0], b[1] - q[1]) for q in outer), 1e-9)

    def test_the_spring_line_pairs_have_the_same_height(self):
        _i, _o, p = self.pairs()
        zs = [(a[1], b[1]) for a, b in p]
        self.assertIn((self.SPRING, self.SPRING), [(round(a, 9), round(b, 9)) for a, b in zs])

    def test_no_quad_between_two_pairs_is_collapsed_or_folded(self):
        for kind in ("ogee", "lancet"):
            _i, _o, p = self.pairs(kind)
            signs = set()
            for (a0, b0), (a1, b1) in zip(p, p[1:]):
                quad = [a0, a1, b1, b0]
                ar = area(quad)
                self.assertGreater(abs(ar), 1e-4)
                signs.add(ar > 0)
            self.assertEqual(len(signs), 1)

    def test_u_is_the_distance_from_the_apex_on_both_sides(self):
        for kind in ("ogee", "lancet"):
            _i, _o, p = self.pairs(kind)
            u = gg.ring_u(p)
            m = len(u) // 2
            self.assertEqual(len(u), len(p))
            self.assertAlmostEqual(u[m], 0.0, places=9)
            for a, b in zip(u, u[::-1]):
                self.assertAlmostEqual(a, b, places=9)
            for a, b in zip(u[:m], u[1:m + 1]):
                self.assertGreater(a, b + 1e-6)

    def test_u_runs_the_straight_jamb_at_one_metre_per_metre(self):
        _i, _o, p = self.pairs()
        u = gg.ring_u(p)
        self.assertAlmostEqual(u[0] - u[1], self.SPRING, places=9)

    def test_u_is_the_mean_of_the_two_edges_so_neither_edge_is_stretched_much(self):
        inner, outer, p = self.pairs()
        u = gg.ring_u(p)
        for i in range(len(p) // 2):
            li = math.dist(p[i][0], p[i + 1][0])
            lo = math.dist(p[i][1], p[i + 1][1])
            du = u[i] - u[i + 1]
            self.assertGreater(du, 0.0)
            self.assertLessEqual(du, max(li, lo) + 1e-9)
            self.assertGreaterEqual(du, min(li, lo) - 1e-9)


class SweepEdgeVTests(unittest.TestCase):
    CONE = [(-0.1, 0.0), (gg.BATTER, 0.0), (0.0, jp.Z_PLINTH), (-0.1, jp.Z_PLINTH)]

    def test_the_plinth_cone_has_the_bed_joint_at_the_top_edge_and_zero_at_the_ground_line(self):
        va, vb = gg.sweep_edge_v(self.CONE, 1, True)
        self.assertAlmostEqual(va, 0.0, places=9)
        self.assertAlmostEqual(vb, jp.PLINTH_V_TOP, places=9)

    def test_a_vertical_plinth_edge_takes_the_vertical_rule(self):
        va, vb = gg.sweep_edge_v(self.CONE, 3, True)      # (-0.1, 3) to (-0.1, 0)
        self.assertAlmostEqual(va, jp.plinth_v_vertical(3.0), places=9)
        self.assertAlmostEqual(vb, jp.plinth_v_vertical(0.0), places=9)

    def test_a_horizontal_edge_runs_across_the_member_at_td(self):
        va, vb = gg.sweep_edge_v(self.CONE, 0, True)      # (-0.1, 0) to (0.6, 0)
        self.assertAlmostEqual(vb - va, jp.TD * 0.7, places=9)

    def test_a_stone_slab_takes_v_from_z_like_the_wall(self):
        slab = [(-0.45, 12.0), (0.0, 12.0), (0.0, 14.0), (-0.45, 14.0)]
        va, vb = gg.sweep_edge_v(slab, 1, False)
        self.assertAlmostEqual(va, jp.TD * 12.0, places=9)
        self.assertAlmostEqual(vb, jp.TD * 14.0, places=9)

    def test_a_battered_stone_edge_takes_the_slope_rule_of_the_wall(self):
        va, vb = gg.sweep_edge_v(self.CONE, 1, False)
        tz = jp.slope_tz((0.0, 3.0, 0.6))
        self.assertAlmostEqual(vb, jp.TD * 3.0 / tz, places=9)


class FoundationUvTests(unittest.TestCase):
    def test_a_diagonal_vertical_face_is_isometric_at_td(self):
        n = (0.37, 0.93, 0.0)
        h = math.hypot(n[0], n[1])
        n = (n[0] / h, n[1] / h, 0.0)
        t = (-n[1], n[0])
        pts = [(1.0, 2.0, -5.0), (1.0 + 2.0 * t[0], 2.0 + 2.0 * t[1], -5.0), (1.0 + 2.0 * t[0], 2.0 + 2.0 * t[1], -3.0)]
        uvs = [gg.foundation_uv(n, p, -13.0) for p in pts]
        lo, hi = jp.uv_scale_axes(pts, uvs)
        self.assertAlmostEqual(lo, jp.TD, places=9)
        self.assertAlmostEqual(hi, jp.TD, places=9)

    def test_a_diagonal_vertical_face_holds_the_bed_joint_rule(self):
        n = (0.6, 0.8, 0.0)
        u0, v0 = gg.foundation_uv(n, (4.0, 4.0, 3.0), -13.0)
        self.assertAlmostEqual(v0, jp.PLINTH_V_TOP, places=9)

    def test_a_diagonal_stone_face_is_isometric_and_takes_v_from_z(self):
        n = (0.79, -0.62, 0.0)
        h = math.hypot(n[0], n[1])
        n = (n[0] / h, n[1] / h, 0.0)
        t = (-n[1], n[0])
        pts = [(4.0, 5.0, 12.0), (4.0 + t[0], 5.0 + t[1], 12.0), (4.0 + t[0], 5.0 + t[1], 13.0)]
        uvs = [gg.foundation_uv(n, p, -13.0, False) for p in pts]
        self.assertAlmostEqual(uvs[0][1], jp.TD * 12.0, places=9)
        lo, hi = jp.uv_scale_axes(pts, uvs)
        self.assertAlmostEqual(lo, jp.TD, places=9)
        self.assertAlmostEqual(hi, jp.TD, places=9)

    def test_an_axis_stone_face_keeps_the_wall_rule(self):
        for n, co in (((0.0, 1.0, 0.0), (3.0, 6.0, 5.0)), ((1.0, 0.0, 0.0), (10.0, 3.0, 5.0)),
                      ((0.0, 0.8, 0.6), (3.0, 3.0, 1.0)), ((0.0, 0.0, 1.0), (1.0, 1.0, 12.0))):
            self.assertEqual(gg.foundation_uv(n, co, -13.0, False), jp.world_uv(n, co, -13.0))

    def test_an_axis_face_keeps_the_plinth_rule(self):
        for n, co in (((0.0, 1.0, 0.0), (3.0, 6.0, -2.0)), ((1.0, 0.0, 0.0), (10.0, 3.0, -2.0)),
                      ((0.0, 0.8, 0.6), (3.0, 3.0, 1.0)), ((0.0, 0.0, 1.0), (1.0, 1.0, 0.0))):
            self.assertEqual(gg.foundation_uv(n, co, -13.0), jp.plinth_uv(n, co, -13.0))


class FacetUvTests(unittest.TestCase):
    """The faces of a battered cone are mapped one by one: isometric, with the bed joint where the plinth rule puts it,
    and the U of neighbouring facets agreeing along their shared edge."""
    R, TOP, H, BATTER = 3.3, 3.0, 3.0, 0.6
    N = 24
    ARC = 2.28

    def facet(self, i):
        """The i-th quad of a cone round (0, 0): its four corners, its outward unit normal, the centre and the distance
        travelled to it. Travel keeps the outside on the left, so it runs clockwise round the axis and the
        distance is the mid ring's arc length with the sign of that direction."""
        r_t, r_b = self.R, self.R + self.BATTER
        th0, th1 = self.ARC * i / self.N, self.ARC * (i + 1) / self.N
        def pt(r, th, z):
            return (r * math.cos(th), r * math.sin(th), z)
        corners = [pt(r_b, th0, 0.0), pt(r_b, th1, 0.0), pt(r_t, th1, self.H), pt(r_t, th0, self.H)]
        mid = (th0 + th1) / 2
        rm = (r_t + r_b) / 2
        # outward unit normal: the radial direction tilted up by the batter
        slope = math.hypot(self.BATTER, self.H)
        n = (self.H / slope * math.cos(mid), self.H / slope * math.sin(mid), self.BATTER / slope)
        c = tuple(sum(p[k] for p in corners) / 4 for k in range(3))
        return corners, n, c, -rm * (th0 + th1) / 2

    def test_a_facet_is_isometric_at_td_with_the_plinth_bed_joint_on_the_top_edge(self):
        corners, n, c, s_c = self.facet(5)
        uvs = [gg.facet_uv(n, p, c, s_c, True) for p in corners]
        lo, hi = jp.uv_scale_axes(corners[:3], uvs[:3])
        self.assertAlmostEqual(lo, jp.TD, places=3)    # the corners lie on a chord of the cone, a hair off the mid normal's plane
        self.assertAlmostEqual(hi, jp.TD, places=3)
        self.assertAlmostEqual(uvs[0][1], 0.0, places=9)                 # the ground line
        self.assertAlmostEqual(uvs[3][1], jp.PLINTH_V_TOP, places=6)     # the top edge, Z 3.0

    def test_a_stone_facet_takes_v_by_the_slope_rule_of_the_wall(self):
        corners, n, c, s_c = self.facet(2)
        uvs = [gg.facet_uv(n, p, c, s_c, False) for p in corners]
        tz = jp.slope_tz((0.0, self.H, self.BATTER))
        self.assertAlmostEqual(uvs[3][1], jp.TD * self.H / tz, places=6)

    def test_a_vertical_facet_takes_the_vertical_rule(self):
        n = (1.0, 0.0, 0.0)
        u, v = gg.facet_uv(n, (3.0, 1.0, 3.0), (3.0, 1.0, 2.0), 4.0, True)
        self.assertAlmostEqual(v, jp.PLINTH_V_TOP, places=9)
        u2, v2 = gg.facet_uv(n, (3.0, 1.0, 3.0), (3.0, 1.0, 2.0), 4.0, False)
        self.assertAlmostEqual(v2, jp.TD * 3.0, places=9)

    def test_u_grows_in_the_travel_direction_and_is_the_centre_length_at_the_centre(self):
        corners, n, c, s_c = self.facet(7)
        u, _v = gg.facet_uv(n, c, c, s_c, True)
        self.assertAlmostEqual(u, jp.TD * s_c, places=9)
        # travel is clockwise, so the corner at the lower angle has the larger U
        ua = gg.facet_uv(n, corners[0], c, s_c, True)[0]
        ub = gg.facet_uv(n, corners[1], c, s_c, True)[0]
        self.assertGreater(ua, ub)

    def test_neighbouring_facets_agree_on_u_along_their_shared_edge_to_a_few_centimetres(self):
        for i in range(self.N - 1):
            ca, na, cc_a, sa = self.facet(i)
            cb, nb, cc_b, sb = self.facet(i + 1)
            for ka, kb in ((1, 0), (2, 3)):       # the shared edge: the corners at the higher angle of i, the lower of i + 1
                ua = gg.facet_uv(na, ca[ka], cc_a, sa, True)[0]
                ub = gg.facet_uv(nb, cb[kb], cc_b, sb, True)[0]
                self.assertLess(abs(ua - ub), jp.TD * 0.05)


class CorbelTierTests(unittest.TestCase):
    def test_the_head_is_the_collar_and_carries_the_rib_feet(self):
        tiers = gg.corbel_tiers()
        hw, head = tiers[0]
        self.assertAlmostEqual(hw, jp.COLLAR_HALF_W, places=9)
        self.assertEqual(head, gg.corbel_profile())

    def test_two_or_three_steps_hang_under_the_head(self):
        self.assertIn(len(gg.corbel_tiers()) - 1, (2, 3))

    def test_every_tier_is_a_convex_counter_clockwise_profile_buried_in_the_wall(self):
        for _hw, prof in gg.corbel_tiers():
            self.assertGreater(area(prof), 0.01)
            self.assertTrue(convex(prof))
            self.assertLess(min(p[0] for p in prof), jp.Y_OUT)

    def test_the_tiers_stack_down_the_wall_with_no_gap(self):
        tiers = gg.corbel_tiers()
        for (_a, upper), (_b, lower) in zip(tiers, tiers[1:]):
            self.assertAlmostEqual(min(p[1] for p in upper), max(p[1] for p in lower), places=9)

    def test_each_step_is_narrower_and_less_proud_than_the_one_above(self):
        tiers = gg.corbel_tiers()
        for (wa, upper), (wb, lower) in zip(tiers, tiers[1:]):
            self.assertLess(wb, wa - 0.05)
            self.assertLess(max(p[0] for p in lower), max(p[0] for p in upper) - 0.05)

    def test_the_lowest_step_comes_down_to_the_wall(self):
        tiers = gg.corbel_tiers()
        last = tiers[-1][1]
        self.assertLess(max(p[0] for p in last) - jp.Y_OUT, 0.2)

    def test_a_step_has_a_chamfered_underside_so_it_is_moulded_not_a_box(self):
        for _hw, prof in gg.corbel_tiers()[1:]:
            self.assertGreaterEqual(len(prof), 5)

    def test_no_tier_hangs_lower_than_the_pier_collar_stands_over_the_door_springing(self):
        low = min(p[1] for _hw, prof in gg.corbel_tiers() for p in prof)
        self.assertGreater(low, 7.0)


class RailReturnTests(unittest.TestCase):
    def test_the_return_abuts_the_inner_rail_and_the_stub_rail_and_overlaps_neither(self):
        inner_rail_y1 = -gg.HALF_D + (jp.RAIL_Y1 - jp.RAIL_Y0)
        y0, y1 = gg.rail_return_y()
        self.assertAlmostEqual(y0, inner_rail_y1, places=9)
        self.assertAlmostEqual(y1, jp.RAIL_Y1, places=9)
        for s in (-1, 1):
            x0, x1 = gg.rail_return_x(s)
            self.assertAlmostEqual(max(abs(x0), abs(x1)), gg.XB, places=9)
            self.assertAlmostEqual(x1 - x0, jp.RAIL_Y1 - jp.RAIL_Y0, places=9)

    def test_the_return_cap_is_flush_on_the_stub_side_and_overhangs_the_other(self):
        for s in (-1, 1):
            x0, x1 = gg.rail_return_x(s)
            cap = gg.rail_return_cap(s)
            xs = [p[0] for p in cap]
            near = max(xs) if s > 0 else min(xs)
            far = min(xs) if s > 0 else max(xs)
            self.assertAlmostEqual(near, s * gg.XB, places=9)
            self.assertAlmostEqual(abs(far - (x0 if s > 0 else x1)), jp.RAIL_CAP_OVER, places=9)
            self.assertAlmostEqual(max(p[1] for p in cap), jp.RAIL_TOP, places=9)
            self.assertAlmostEqual(min(p[1] for p in cap), jp.RAIL_BODY_TOP, places=9)


class ParapetReturnTests(unittest.TestCase):
    def test_the_slab_fits_exactly_between_two_parts_and_the_cap_is_flush_with_it(self):
        for xc in (-2.75, 2.75):
            slab, cap = gg.parapet_return_profiles(xc)
            self.assertAlmostEqual(min(p[0] for p in slab), xc - 0.25, places=9)
            self.assertAlmostEqual(max(p[0] for p in slab), xc + 0.25, places=9)
            self.assertAlmostEqual(min(p[0] for p in cap), xc - 0.25, places=9)
            self.assertAlmostEqual(max(p[0] for p in cap), xc + 0.25, places=9)
            self.assertAlmostEqual(max(p[1] for p in cap), jp.PARAPET_TOP, places=9)
            self.assertAlmostEqual(min(p[1] for p in cap), jp.PARAPET_BODY_TOP, places=9)

    def test_the_cap_is_wide_enough_in_y_to_meet_the_caps_of_both_neighbours(self):
        dy_wing, dy_door = gg.FACADE_Y - jp.Y_OUT, gg.HALF_D - jp.Y_OUT
        y0, y1 = gg.parapet_return_y(dy_wing, dy_door)
        self.assertAlmostEqual(y0, jp.PARAPET_Y0 + dy_wing - jp.PARAPET_CAP_OVER, places=9)
        self.assertAlmostEqual(y1, jp.PARAPET_Y1 + dy_door + jp.PARAPET_CAP_OVER, places=9)


class PileUvTests(unittest.TestCase):
    def test_a_loose_stone_face_is_isometric_at_td_whatever_its_tilt(self):
        n = (0.3, -0.5, 0.81)
        h = math.sqrt(sum(c * c for c in n))
        n = tuple(c / h for c in n)
        th = (-n[1], n[0], 0.0)
        hh = math.hypot(th[0], th[1])
        th = (th[0] / hh, th[1] / hh, 0.0)
        ts = (n[1] * th[2] - n[2] * th[1], n[2] * th[0] - n[0] * th[2], n[0] * th[1] - n[1] * th[0])
        c = (12.0, 3.0, 12.4)
        pts = [c, tuple(c[i] + 0.2 * th[i] for i in range(3)), tuple(c[i] + 0.2 * ts[i] for i in range(3))]
        uvs = [gg.planar_uv(n, p) for p in pts]
        lo, hi = jp.uv_scale_axes(pts, uvs)
        self.assertAlmostEqual(lo, jp.TD, places=6)
        self.assertAlmostEqual(hi, jp.TD, places=6)


class ClosedMitreTests(unittest.TestCase):
    def test_a_clockwise_octagon_gives_outward_normals_that_hold_the_offset_from_both_edges(self):
        path = gg.ring_path(1.0, 2.0, 2.5)
        norms = gg.mitre_normals_closed(path)
        self.assertEqual(len(norms), 8)
        for d in (-0.3, 0.3):
            ring = [(p[0] + n[0] * d, p[1] + n[1] * d) for p, n in zip(path, norms)]
            for i in range(8):
                (ax, ay), (bx, by) = ring[i], ring[(i + 1) % 8]
                ln = math.hypot(bx - ax, by - ay)
                nx, ny = -(by - ay) / ln, (bx - ax) / ln                 # the left-hand normal of the travel direction
                # the clockwise path keeps the outside on the left: every edge of the offset ring is 2.5 + d from the centre
                self.assertAlmostEqual((ax - 1.0) * nx + (ay - 2.0) * ny, 2.5 + d, places=9)

    def test_the_ring_path_is_the_octagon_run_clockwise(self):
        path = gg.ring_path(0.0, 0.0, 2.5)
        self.assertLess(area(path), 0.0)
        self.assertAlmostEqual(abs(area(path)), area(gg.octagon(0.0, 0.0, 2.5)), places=9)

    def test_the_closed_mitre_wraps_round_the_ends(self):
        sq = [(0.0, 0.0), (0.0, 2.0), (2.0, 2.0), (2.0, 0.0)]            # clockwise
        n = gg.mitre_normals_closed(sq)
        self.assertAlmostEqual(n[0][0], -1.0, places=9)                    # the corner (0, 0) points out to (-1, -1)
        self.assertAlmostEqual(n[0][1], -1.0, places=9)


class CorniceTests(unittest.TestCase):
    def test_the_shaft_ends_where_the_cornice_starts_and_the_lantern_starts_where_it_ends(self):
        prof = gg.cornice_profile()
        self.assertAlmostEqual(min(z for _d, z in prof), gg.CORNICE_Z0, places=9)
        self.assertAlmostEqual(max(z for _d, z in prof), gg.SHAFT_TOP_Z, places=9)
        self.assertLess(gg.CORNICE_Z0, gg.SHAFT_TOP_Z)

    def test_the_cornice_ring_ends_on_the_edge_of_the_lantern_floor_so_no_slot_opens_between_them(self):
        d_in = min(d for d, _z in gg.cornice_profile())
        self.assertAlmostEqual(gg.TUR_A + d_in, gg.LANTERN_FLOOR_A, places=9)
        self.assertGreater(gg.LANTERN_FLOOR_A, gg.LANTERN_A + gg.LANTERN_COL_R * 1.5 + 0.1)

    def test_the_cornice_overhangs_the_shaft_by_no_more_than_before(self):
        self.assertAlmostEqual(max(d for d, _z in gg.cornice_profile()), 0.30, places=9)

    def test_the_profile_is_a_simple_polygon_with_the_top_narrower_than_the_foot(self):
        prof = gg.cornice_profile()
        top = [d for d, z in prof if abs(z - gg.SHAFT_TOP_Z) < 1e-9]
        foot = [d for d, z in prof if abs(z - gg.CORNICE_Z0) < 1e-9]
        self.assertLess(max(top), max(foot))
        self.assertGreater(abs(area(prof)), 0.1)


class SpireMapTests(unittest.TestCase):
    def test_the_eave_ring_length_of_a_regular_octagon(self):
        self.assertAlmostEqual(gg.ngon_eave_length(8, 2.45), 2 * 8 * 2.45 * math.tan(math.pi / 8), places=9)
        self.assertAlmostEqual(gg.ngon_eave_length(4, 1.0), 8.0, places=9)

    def test_the_seam_falls_between_two_facets_that_face_the_back(self):
        # the facet normals of the octagon are at 45 degree steps; U jumps between the facet before the seam and the next
        seam = gg.SPIRE_SEAM
        azimuths = [math.radians(45.0 * k) for k in range(8)]
        before = [a for a in azimuths if math.atan2(math.sin(a), math.cos(a)) <= seam + 1e-9]
        self.assertEqual(len(before), 1)                                 # exactly one facet stays before the seam
        for a in (before[0], before[0] + math.radians(45.0)):
            self.assertLess(math.sin(a), -0.5)                           # both face -Y, away from the outer face

    def test_the_seam_does_not_split_a_front_facet(self):
        seam = gg.SPIRE_SEAM
        self.assertLess(math.sin(seam), 0.0)
        self.assertGreater(abs(math.sin(seam)), 0.5)


class RoofShapeTests(unittest.TestCase):
    def test_the_top_line_runs_from_the_eave_to_the_ridge(self):
        self.assertAlmostEqual(gg.roof_top_z(gg.ROOF_HX), gg.ROOF_EAVE_Z, places=9)
        self.assertAlmostEqual(gg.roof_top_z(0.0), gg.ROOF_RIDGE_Z, places=9)
        self.assertAlmostEqual(gg.roof_top_z(-2.0), gg.roof_top_z(2.0), places=9)
        self.assertAlmostEqual(gg.roof_under_z(1.7), gg.roof_top_z(1.7) - gg.ROOF_T, places=9)

    def test_the_band_of_a_slab_is_a_parallelogram_with_vertical_ends(self):
        for side in (-1, 1):
            band = gg.roof_band(side, gg.ROOF_HX, 0.0, 0.0)
            self.assertEqual(len(band), 4)
            xs = sorted(round(p[0], 9) for p in band)
            self.assertEqual(xs, sorted([0.0, 0.0, round(side * gg.ROOF_HX, 9), round(side * gg.ROOF_HX, 9)]))
            for x, z in band:
                if abs(x) < 1e-9:
                    self.assertIn(round(z, 9), (round(gg.ROOF_RIDGE_Z, 9), round(gg.ROOF_RIDGE_Z - gg.ROOF_T, 9)))
            self.assertGreater(abs(area(band)), 0.5 * gg.ROOF_T * gg.ROOF_HX)

    def test_the_two_bands_are_mirror_images(self):
        a = gg.roof_band(1, 4.6, 0.12, 0.1)
        b = gg.roof_band(-1, 4.6, 0.12, 0.1)
        self.assertEqual(sorted((round(-x, 9), round(z, 9)) for x, z in a), sorted((round(x, 9), round(z, 9)) for x, z in b))

    def test_the_slab_does_not_reach_the_turrets(self):
        self.assertLess(gg.ROOF_HX + 0.1, gg.TUR_CX - gg.TUR_A)

    def test_the_band_axis_runs_down_the_middle_of_the_band(self):
        for side in (-1, 1):
            p0, p1, width = gg.band_axis(side, gg.ROOF_HX, 0.12, 0.10)
            self.assertAlmostEqual(p0[0], 0.0, places=9)
            self.assertAlmostEqual(p1[0], side * gg.ROOF_HX, places=9)
            top = gg.roof_top_z(0.0) + 0.12
            bot = gg.roof_under_z(0.0) - 0.10
            self.assertAlmostEqual(p0[1], (top + bot) / 2.0, places=9)
            cos_t = 1.0 / math.sqrt(1.0 + gg.ROOF_SLOPE ** 2)
            self.assertAlmostEqual(width, (top - bot) * cos_t, places=9)


class RafterTests(unittest.TestCase):
    def test_the_axes_are_unit_vectors_and_the_normal_points_up_and_out(self):
        for side in (-1, 1):
            (tx, tz), (nx, nz) = gg.roof_axes(side)
            self.assertAlmostEqual(math.hypot(tx, tz), 1.0, places=9)
            self.assertAlmostEqual(math.hypot(nx, nz), 1.0, places=9)
            self.assertAlmostEqual(tx * nx + tz * nz, 0.0, places=9)
            self.assertGreater(nz, 0.0)
            self.assertGreater(nx * side, 0.0)
            self.assertLess(tx * side, 0.0)                              # from the eave toward the ridge

    def test_a_rafter_hangs_under_the_slab_with_its_top_face_just_inside_it(self):
        for side in (-1, 1):
            (p0, p1) = gg.rafter_axis(side)
            _t, (nx, nz) = gg.roof_axes(side)
            for p in (p0, p1):
                x = abs(p[0])
                under = (side * x, gg.roof_under_z(x))
                dist_down = -((p[0] - under[0]) * nx + (p[1] - under[1]) * nz)     # perpendicular distance below the underside
                self.assertAlmostEqual(dist_down, gg.RAFTER_W / 2.0 - gg.RAFTER_EMBED, places=9)

    def test_the_rafter_runs_from_inside_the_eave_to_inside_the_ridge_beam(self):
        for side in (-1, 1):
            p0, p1 = gg.rafter_axis(side)
            self.assertLess(abs(p0[0]), gg.ROOF_HX)
            self.assertGreater(abs(p0[0]), gg.ROOF_HX - 0.4)
            self.assertLess(abs(p1[0]), gg.RIDGE_BEAM_HALF)

    def test_the_rafters_stand_between_the_two_gable_ends_and_are_evenly_spaced(self):
        ys = gg.RAFTER_YS
        self.assertEqual(list(ys), sorted(ys))
        self.assertEqual([round(-y, 9) for y in ys][::-1], [round(y, 9) for y in ys])
        steps = {round(b - a, 9) for a, b in zip(ys, ys[1:])}
        self.assertEqual(len(steps), 1)
        self.assertLess(max(ys) + gg.RAFTER_T / 2.0, gg.TYMP_Y_IN)

    def test_every_rafter_is_well_above_the_walk(self):
        for side in (-1, 1):
            p0, _p1 = gg.rafter_axis(side)
            (_t, (nx, nz)) = gg.roof_axes(side)
            low = p0[1] - (gg.RAFTER_W / 2.0) * nz - 0.2
            self.assertGreater(low - jp.Z_DECK, 2.4)


class GableEndTests(unittest.TestCase):
    def test_the_gable_stands_under_the_slab_ends_and_leaves_an_overhang(self):
        self.assertLess(gg.TYMP_Y_OUT, gg.ROOF_HY)
        self.assertGreater(gg.ROOF_HY - gg.TYMP_Y_OUT, 0.1)
        self.assertGreater(gg.TYMP_Y_OUT - gg.TYMP_Y_IN, 0.3)

    def test_the_stone_triangle_rests_on_the_tie_course_and_touches_the_underside_at_the_ridge(self):
        tri = gg.tympanum_outline()
        self.assertEqual(len(tri), 3)
        zs = sorted(p[1] for p in tri)
        self.assertAlmostEqual(zs[0], gg.TIE_Z1, places=9)
        self.assertAlmostEqual(zs[2], gg.roof_under_z(0.0), places=9)
        for x, z in tri:
            if z > gg.TIE_Z1 + 1e-9:
                self.assertAlmostEqual(x, 0.0, places=9)
            else:
                self.assertAlmostEqual(z, gg.roof_under_z(abs(x)), places=9)            # the base corners are on the underside

    def test_the_tie_course_is_wider_than_the_plate_in_x_and_stands_clear_of_its_end_plane(self):
        plate_edge = max(abs(x) for x in gg.plate_x_range(1))
        self.assertGreater(gg.TIE_HALF_X, plate_edge + 0.01)
        self.assertLess(gg.TIE_HALF_X, gg.ROOF_HX)
        self.assertNotAlmostEqual(gg.TIE_Z0, gg.ROOF_EAVE_Z - 0.35, places=3)           # the plate's bottom is another plane

    def test_the_opening_is_a_pointed_arch_that_fits_in_the_stone_with_a_sill(self):
        op = gg.gable_opening(8)
        zs = [p[1] for p in op]
        self.assertGreater(min(zs), gg.TIE_Z1 + 0.2)
        for x, z in op:
            self.assertLess(z, gg.roof_under_z(abs(x)) - 0.6)                      # stone above the whole opening
        xs = [p[0] for p in op]
        self.assertAlmostEqual(min(xs), -max(xs), places=9)
        apex = max(op, key=lambda p: p[1])
        self.assertAlmostEqual(apex[0], 0.0, places=9)

    def test_the_cut_is_two_centimetres_inside_the_moulding_so_the_two_share_no_plane(self):
        op = gg.gable_opening(8)
        half = max(p[0] for p in op)
        w_in = gg.GABLE_ARCH[2] - 2 * gg.GABLE_ARCH_MOULD
        self.assertAlmostEqual(half, w_in / 2.0 - gg.GABLE_ARCH_LEDGE, places=9)
        self.assertAlmostEqual(gg.GABLE_ARCH_LEDGE, 0.02, places=9)

    def test_the_ridge_beam_ends_inside_the_stone_gable(self):
        self.assertGreater(gg.RIDGE_BEAM_Y, gg.TYMP_Y_IN)
        self.assertLess(gg.RIDGE_BEAM_Y, gg.TYMP_Y_OUT)

    def test_the_fascia_clears_the_slab_end_and_the_ridge_cap_stands_beyond_it(self):
        self.assertGreater(gg.FASCIA_Y1 - gg.FASCIA_Y0, 0.05)
        self.assertAlmostEqual(gg.FASCIA_Y0, gg.ROOF_HY, places=9)
        self.assertGreater(gg.RIDGE_CAP_Y - gg.FASCIA_Y1, 0.01)

    def test_the_cap_sits_on_the_ridge(self):
        cap = gg.ridge_cap_profile()
        self.assertAlmostEqual(max(z for _x, z in cap) - gg.ROOF_RIDGE_Z, 0.17, places=9)
        for x, z in cap:
            self.assertLess(abs(x), 0.31)
        self.assertEqual(sorted(round(x, 9) for x, _z in cap), sorted(round(-x, 9) for x, _z in cap))


class BarUvTests(unittest.TestCase):
    def setUp(self):
        self.p0, self.p1 = (0.0, 2.0), (3.0, 6.0)             # a bar 5 m long, rising 4 over 3
        self.w = 0.2
        self.t = (0.6, 0.8)
        self.perp = (-0.8, 0.6)
        v0, v1 = jp.STRIPS["S1"]
        self.v0, self.v1 = v0, v1

    def at(self, s, w, y=1.0):
        return (self.p0[0] + self.t[0] * s + self.perp[0] * w, y, self.p0[1] + self.t[1] * s + self.perp[1] * w)

    def uv(self, n, co):
        return gg.bar_uv(n, co, self.p0, self.p1, self.w, 0.9, 1.1, "S1")

    def test_u_runs_along_the_bar_at_td(self):
        n = (0.0, 1.0, 0.0)
        u0, _ = self.uv(n, self.at(1.0, 0.0))
        u1, _ = self.uv(n, self.at(3.5, 0.0))
        self.assertAlmostEqual(u1 - u0, jp.TD * 2.5, places=9)

    def test_a_broad_face_takes_the_whole_strip_across_the_bar(self):
        n = (0.0, 1.0, 0.0)
        _, va = self.uv(n, self.at(2.0, -self.w / 2.0))
        _, vb = self.uv(n, self.at(2.0, self.w / 2.0))
        self.assertAlmostEqual(va, self.v0, places=9)
        self.assertAlmostEqual(vb, self.v1, places=9)

    def test_a_long_face_takes_the_strip_across_the_thickness(self):
        n = (self.perp[0], 0.0, self.perp[1])
        _, va = self.uv(n, self.at(2.0, self.w / 2.0, y=0.9))
        _, vb = self.uv(n, self.at(2.0, self.w / 2.0, y=1.1))
        self.assertAlmostEqual(va, self.v0, places=9)
        self.assertAlmostEqual(vb, self.v1, places=9)

    def test_an_end_face_takes_u_from_y_and_v_across_the_width(self):
        n = (self.t[0], 0.0, self.t[1])
        ua, va = self.uv(n, self.at(5.0, -self.w / 2.0, y=0.9))
        ub, vb = self.uv(n, self.at(5.0, self.w / 2.0, y=1.1))
        self.assertAlmostEqual(ub - ua, jp.TD * 0.2, places=9)
        self.assertAlmostEqual(vb - va, self.v1 - self.v0, places=9)

    def test_values_stay_inside_the_strip(self):
        for n in ((0.0, 1.0, 0.0), (self.perp[0], 0.0, self.perp[1]), (self.t[0], 0.0, self.t[1])):
            for s in (0.0, 2.5, 5.0):
                for w in (-0.3, 0.0, 0.3):
                    for y in (0.5, 1.0, 1.5):
                        _u, v = self.uv(n, self.at(s, w, y))
                        self.assertGreaterEqual(v, self.v0 - 1e-9)
                        self.assertLessEqual(v, self.v1 + 1e-9)


class SlopedTests(unittest.TestCase):
    def test_an_axis_face_and_a_flat_face_are_not_sloped(self):
        self.assertFalse(gg.is_sloped((0.0, 1.0, 0.0)))
        self.assertFalse(gg.is_sloped((0.0, 0.0, -1.0)))
        self.assertFalse(gg.is_sloped((1.0, 0.0, 0.0)))

    def test_a_leaning_face_is_sloped(self):
        self.assertTrue(gg.is_sloped((0.6, 0.0, 0.8)))
        self.assertTrue(gg.is_sloped((0.0, -0.6, -0.8)))


class TurretMemberTailTests(unittest.TestCase):
    def test_the_default_path_still_ends_inside_the_stub(self):
        right = gg.plinth_path_b(1)
        self.assertEqual(right[-1], (gg.XB, gg.STUB_TAIL_Y))

    def test_a_member_can_stop_on_the_front_face_of_the_stub_member_it_meets(self):
        for side in (-1, 1):
            path = gg.plinth_path_b(side, tail_y=jp.Y_OUT + 0.15)
            tail = path[-1] if side > 0 else path[0]
            self.assertAlmostEqual(abs(tail[0]), gg.XB, places=9)
            self.assertAlmostEqual(tail[1], jp.Y_OUT + 0.15, places=9)
            rest = gg.plinth_path_b(side)
            self.assertEqual(path[1:] if side < 0 else path[:-1], rest[1:] if side < 0 else rest[:-1])


# ---- version 6 -------------------------------------------------------------------------------------------
class FineDrumTests(unittest.TestCase):
    def test_a_drum_segment_turns_at_most_four_degrees(self):
        self.assertLessEqual(math.degrees(gg.THETA_FRONT) / gg.ARC_STEPS, 4.0)
        for side in (-1, 1):
            self.assertLessEqual(max(gg.path_turns_deg(gg.drum_path(side))), 4.0)

    def test_the_turn_function_reads_a_right_angle(self):
        self.assertAlmostEqual(gg.path_turns_deg([(0, 0), (1, 0), (1, 1)])[0], 90.0, places=9)

    def test_the_door_arcs_turn_at_most_four_degrees(self):
        loop = gg.opening_loop(gg.DOOR_HALF_W, 7.0, 3.0, 0.0, steps=gg.DOOR_STEPS)
        turns = gg.path_turns_deg(loop)
        apex = len(loop) // 2 - 1
        self.assertLessEqual(max(t for i, t in enumerate(turns) if i not in (0, apex, len(turns) - 1)), 4.0)


class DrumArchLayoutTests(unittest.TestCase):
    def half_deg(self):
        return math.degrees(jp.ARCH_W / 2.0 / gg.DRUM_R)

    def test_every_arch_is_seen_whole_from_the_front_at_10_to_20_m(self):
        for d in (10.0, 12.0, 15.0, 20.0):
            limit = gg.drum_visible_from_deg((0.0, gg.HALF_D + d))
            for c in gg.DRUM_ARCH_DEG:
                self.assertGreaterEqual(c - self.half_deg(), limit, (d, c))

    def test_the_visible_limit_is_the_tangent_from_the_viewer(self):
        self.assertAlmostEqual(gg.drum_visible_from_deg((gg.DRUM_CX, 1.0e6)), 0.0, delta=0.01)
        self.assertGreater(gg.drum_visible_from_deg((0.0, 16.2)), 30.0)

    def test_plain_margin_between_the_facade_end_and_the_nearest_arch(self):
        edge = max(gg.DRUM_ARCH_DEG) + self.half_deg()
        arc = math.radians(math.degrees(gg.THETA_FRONT) - edge) * gg.DRUM_R
        self.assertGreaterEqual(arc, 0.5 - 1e-9)

    def test_plain_gap_between_neighbour_arches(self):
        c = sorted(gg.DRUM_ARCH_DEG)
        for a, b in zip(c, c[1:]):
            self.assertGreaterEqual(math.radians(b - a - 2 * self.half_deg()) * gg.DRUM_R, 0.5 - 1e-9)

    def test_arches_stay_off_the_tangent_point_and_the_facade_corner(self):
        c = sorted(gg.DRUM_ARCH_DEG)
        self.assertGreater(c[0] - self.half_deg(), 0.0)
        self.assertLess(c[-1] + self.half_deg(), math.degrees(gg.THETA_FRONT))

    def test_there_are_two_arches_on_a_drum(self):
        self.assertEqual(len(gg.DRUM_ARCH_DEG), 2)

    def test_the_nearest_arch_leaves_plain_drum_beside_the_statue_pedestal(self):
        th = math.radians(max(gg.DRUM_ARCH_DEG) + self.half_deg())
        leg_x = gg.DRUM_CX + gg.DRUM_R * math.cos(th)
        ped_edge = gg.STATUE_X + gg.PEDESTAL_CAP_W / 2.0
        self.assertGreaterEqual(leg_x - ped_edge, 0.5)


class StatueLayoutTests(unittest.TestCase):
    def test_clearance_round_the_pedestal_and_the_niche(self):
        corner = gg.arc_right()[0][0]
        for half, need in ((gg.PEDESTAL_CAP_W / 2.0, 0.2), (gg.NICHE_HALF_W, 0.25)):
            self.assertGreaterEqual(gg.STATUE_X - half - gg.PORTAL_HALF_W, need)
            self.assertGreaterEqual(corner - (gg.STATUE_X + half), need)

    def test_the_statue_stands_on_its_pedestal_with_a_margin(self):
        self.assertGreaterEqual(gg.PEDESTAL_W, gg.STATUE_SOURCE_W * gg.STATUE_SCALE + 0.1)
        self.assertGreaterEqual(gg.PEDESTAL_D, gg.STATUE_SOURCE_D * gg.STATUE_SCALE + 0.1)

    def test_the_pedestal_stays_inside_the_door_frame_front(self):
        self.assertLessEqual(gg.STATUE_Y + gg.PEDESTAL_D / 2.0 + 0.08, gg.HALF_D + 0.1)


class RingV6Tests(unittest.TestCase):
    SPRING, RISE = 7.0, 3.0

    def outer(self):
        return gg.ring_outline(gg.DOOR_HALF_W, self.SPRING, self.RISE, jp.STRING_Z0, gg.DOOR_STEPS)

    def test_the_outer_arc_is_concentric_with_the_opening(self):
        w = gg.DOOR_HALF_W
        r = (w * w + self.RISE ** 2) / (2 * w)
        c = r - w
        m = gg.ring_moulding(w, self.SPRING, self.RISE, jp.STRING_Z0)
        curve = [p for p in self.outer() if p[1] > self.SPRING + 1e-9 and p[0] < -1e-9]
        self.assertGreater(len(curve), 10)
        for x, z in curve:
            self.assertAlmostEqual(math.hypot(x - c, z - self.SPRING), r + m, places=9)

    def test_the_ring_tip_stops_on_the_underside_of_the_string_course(self):
        self.assertAlmostEqual(max(z for _x, z in self.outer()), jp.STRING_Z0, places=9)
        self.assertAlmostEqual(gg.RING_TIP_Z, jp.STRING_Z0, places=9)

    def test_the_moulding_is_about_half_a_metre_wide(self):
        m = gg.ring_moulding(gg.DOOR_HALF_W, self.SPRING, self.RISE, jp.STRING_Z0)
        self.assertGreater(m, 0.45)
        self.assertLess(m, 0.6)
        self.assertAlmostEqual(gg.DOOR_RING_M, m, places=12)

    def test_the_outline_is_a_mirror_pair_and_its_arcs_turn_at_most_four_degrees(self):
        o = self.outer()
        for a, b in zip(o, o[::-1]):
            self.assertAlmostEqual(a[0], -b[0], places=9)
            self.assertAlmostEqual(a[1], b[1], places=9)
        turns = gg.path_turns_deg(o)
        apex = len(o) // 2 - 1
        arc = [t for i, t in enumerate(turns) if 1 < i < len(turns) - 2 and i != apex]
        self.assertGreater(len(arc), 10)
        self.assertLessEqual(max(arc), 4.0)

    def test_every_quad_of_the_ring_strip_has_area(self):
        inner = gg.opening_loop(gg.DOOR_HALF_W, self.SPRING, self.RISE, 0.0, steps=gg.DOOR_STEPS)
        pairs = gg.ring_pairs(inner, self.outer(), self.SPRING)
        for (a0, b0), (a1, b1) in zip(pairs, pairs[1:]):
            self.assertGreater(abs(area([a0, a1, b1, b0])), 1e-4)

    def test_the_ring_is_no_prouder_than_the_string_courses_it_meets(self):
        self.assertLess(gg.RING_PROUD, jp.STRING_PROUD)
        self.assertLess(gg.RING_PROUD, jp.INNER_STRING_PROUD)

    def test_the_opening_keeps_four_point_seven_by_seven(self):
        self.assertAlmostEqual(2 * gg.DOOR_HALF_W, 4.7, places=9)
        loop = gg.opening_loop(gg.DOOR_HALF_W, 7.0, 3.0, 0.0, steps=gg.DOOR_STEPS)
        jamb_tops = [z for x, z in loop if abs(abs(x) - gg.DOOR_HALF_W) < 1e-9 and z > 0.0]
        self.assertGreaterEqual(min(jamb_tops), 7.0 - 1e-9)


class FrontCourseTests(unittest.TestCase):
    TAIL = jp.Y_OUT + jp.STRING_PROUD

    def test_the_path_runs_from_stub_to_stub_over_the_door_frame(self):
        p = gg.front_course_path(self.TAIL)
        self.assertEqual(p[0], (-gg.XB, self.TAIL))
        self.assertEqual(p[-1], (gg.XB, self.TAIL))
        for a, b in zip(p, p[::-1]):
            self.assertAlmostEqual(a[0], -b[0], places=9)
            self.assertAlmostEqual(a[1], b[1], places=9)
        for pt in ((-gg.PORTAL_HALF_W, gg.FACADE_Y), (-gg.PORTAL_HALF_W, gg.HALF_D),
                   (gg.PORTAL_HALF_W, gg.HALF_D), (gg.PORTAL_HALF_W, gg.FACADE_Y), (0.0, gg.HALF_D)):
            self.assertIn(pt, p)
        self.assertEqual(p.index((0.0, gg.HALF_D)), len(p) // 2)

    def test_the_only_corners_are_the_drum_joints_and_the_door_frame(self):
        p = gg.front_course_path(self.TAIL)
        sharp = gg.sharp_rings(p, 12.0)
        self.assertEqual(len(sharp), 6)
        turns = gg.path_turns_deg(p)
        for i in sharp:
            self.assertGreater(turns[i - 1], 30.0)

    def test_smooth_rings_turn_less_than_the_threshold(self):
        p = gg.front_course_path(self.TAIL)
        sharp = set(gg.sharp_rings(p, 12.0))
        turns = gg.path_turns_deg(p)
        for i in range(1, len(p) - 1):
            if i not in sharp:
                self.assertLessEqual(turns[i - 1], 4.0)

    def test_mirrored_u_is_the_distance_from_the_middle(self):
        u = gg.mirrored_u([0.0, 1.0, 3.0, 4.0, 8.0])
        self.assertAlmostEqual(u[0], 4.0, places=9)
        self.assertAlmostEqual(u[-1], 4.0, places=9)
        self.assertAlmostEqual(u[1], 3.0, places=9)
        self.assertAlmostEqual(u[3], 0.0, places=9)

    def test_the_course_lies_on_the_drums_string_course_line(self):
        self.assertAlmostEqual(gg.FRONT_COURSE_Z0, jp.STRING_Z0)
        self.assertAlmostEqual(gg.FRONT_COURSE_Z1, jp.STRING_Z1)


class CorbelTaperTests(unittest.TestCase):
    def test_the_taper_follows_the_pier_strip_from_the_collar_top_to_the_string_course(self):
        prof = gg.corbel_taper_profile()
        front = [p for p in prof if p[0] > jp.Y_OUT - 1e-9]
        self.assertEqual(front[0], (jp.Y_OUT + jp.PILASTER_PROUD, jp.COLLAR_Z1))
        self.assertEqual(front[-1], (jp.Y_OUT, jp.STRING_Z0))
        for z in (8.8, 9.3, 9.9, 10.5):
            y = front[0][0] + (front[-1][0] - front[0][0]) * (z - front[0][1]) / (front[-1][1] - front[0][1])
            self.assertAlmostEqual(y - jp.Y_OUT, jp.pilaster_proud(z), places=9)

    def test_the_taper_is_not_prouder_than_the_string_course_it_reaches(self):
        prof = gg.corbel_taper_profile()
        self.assertLessEqual(max(p[0] for p in prof if p[1] >= jp.STRING_Z0 - 1e-9), jp.Y_OUT + 1e-9)

    def test_the_taper_is_convex_counter_clockwise_and_buried_at_the_back(self):
        prof = gg.corbel_taper_profile()
        self.assertGreater(area(prof), 0.0)
        self.assertLess(min(p[0] for p in prof), jp.Y_OUT)


class DrumBandTests(unittest.TestCase):
    def test_the_band_ends_flush_with_the_outer_leg_of_the_nearest_arch(self):
        half = math.degrees(jp.ARCH_W / 2.0 / gg.DRUM_R)
        over = math.degrees(gg.DRUM_BAND_OVER / gg.DRUM_R)
        self.assertGreater(gg.DRUM_BAND_OVER, 0.01)
        self.assertLess(gg.DRUM_BAND_OVER, 0.03)
        self.assertAlmostEqual(gg.drum_band_end_deg(), max(gg.DRUM_ARCH_DEG) + half + over, places=9)
        first = gg.drum_band_path(1)[0]
        th = math.atan2(first[1] - gg.DRUM_CY, first[0] - gg.DRUM_CX)
        self.assertAlmostEqual(math.degrees(th), gg.drum_band_end_deg(), places=9)
        self.assertAlmostEqual(math.hypot(first[0] - gg.DRUM_CX, first[1] - gg.DRUM_CY), gg.DRUM_R, places=9)

    def test_the_band_stops_before_the_facade_corner(self):
        self.assertLess(gg.drum_band_end_deg(), math.degrees(gg.THETA_FRONT))

    def test_the_band_path_ends_on_the_stub_and_turns_at_most_four_degrees(self):
        for side in (-1, 1):
            p = gg.drum_band_path(side, tail_y=jp.Y_OUT + 0.08)
            tail = p[-1] if side > 0 else p[0]
            self.assertAlmostEqual(tail[0], side * gg.XB, places=9)
            self.assertAlmostEqual(tail[1], jp.Y_OUT + 0.08, places=9)
            self.assertLessEqual(max(gg.path_turns_deg(p)), 4.0)

    def test_the_left_band_path_is_the_mirror_in_travel_order(self):
        r, l = gg.drum_band_path(1), gg.drum_band_path(-1)
        self.assertEqual(len(r), len(l))
        for a, b in zip(r, l[::-1]):
            self.assertAlmostEqual(a[0], -b[0], places=9)
            self.assertAlmostEqual(a[1], b[1], places=9)


class SpiredFrontTests(unittest.TestCase):
    """Variant b, version 6: one string course across the whole front, the band flush with its arches, the arches seen whole."""

    def test_the_front_course_path_runs_from_stub_to_stub_in_mirror_order(self):
        tail = jp.Y_OUT + 0.15
        p = gg.front_course_path_b(tail)
        self.assertEqual(p[0], (-gg.XB, tail))
        self.assertEqual(p[-1], (gg.XB, tail))
        for a, b in zip(p, p[::-1]):
            self.assertAlmostEqual(a[0], -b[0], places=9)
            self.assertAlmostEqual(a[1], b[1], places=9)

    def test_the_front_course_goes_round_the_door_frame_with_two_right_angles(self):
        p = gg.front_course_path_b(jp.Y_OUT + 0.15)
        self.assertIn((-gg.PORTAL_HALF_W, gg.HALF_D), p)
        self.assertIn((gg.PORTAL_HALF_W, gg.HALF_D), p)
        self.assertIn((0.0, gg.HALF_D), p)
        turns = gg.path_turns_deg(p)
        self.assertEqual(sum(1 for t in turns if abs(t - 90.0) < 1e-6), 4)
        self.assertEqual(sum(1 for t in turns if abs(t - 45.0) < 1e-6), 8)

    def test_the_course_is_the_plinth_path_of_each_side_with_the_door_front_between(self):
        tail = jp.Y_OUT + 0.15
        p = gg.front_course_path_b(tail)
        want = gg.plinth_path_b(-1, tail_y=tail) + [(-gg.PORTAL_HALF_W, gg.HALF_D), (0.0, gg.HALF_D),
                                                    (gg.PORTAL_HALF_W, gg.HALF_D)] + gg.plinth_path_b(1, tail_y=tail)
        self.assertEqual(p, want)

    def test_the_band_path_ends_flush_with_the_outer_leg_of_each_arch(self):
        for side in (-1, 1):
            p = gg.band_path_b(side)
            xs = [abs(q[0]) for q in p]
            self.assertAlmostEqual(min(xs), gg.FACADE_ARCH_X - gg.FACADE_ARCH[2] / 2.0 - gg.BAND_OVER_B, places=9)
            self.assertAlmostEqual(max(xs), gg.TUR_CX + gg.LOW_ARCH[2] / 2.0 + gg.BAND_OVER_B, places=9)
            door_end = p[0] if side > 0 else p[-1]
            self.assertAlmostEqual(door_end[1], gg.FACADE_Y, places=9)

    def test_the_band_stays_on_the_front_flat_of_the_turret(self):
        o = gg.octagon(gg.TUR_CX, gg.TUR_CY, gg.TUR_A)
        for side in (-1, 1):
            p = gg.band_path_b(side)
            last = p[-1] if side > 0 else p[0]
            self.assertAlmostEqual(last[1], o[1][1], places=9)
            self.assertLess(abs(last[0]), abs(o[1][0]))

    def test_the_left_band_path_is_the_mirror_in_travel_order(self):
        r, l = gg.band_path_b(1), gg.band_path_b(-1)
        self.assertEqual(len(r), len(l))
        for a, b in zip(r, l[::-1]):
            self.assertAlmostEqual(a[0], -b[0], places=9)
            self.assertAlmostEqual(a[1], b[1], places=9)

    def test_one_facade_arch_and_one_front_pair_on_each_side_and_none_elsewhere(self):
        for side in (-1, 1):
            kinds = sorted(a["kind"] for a in gg.arch_layout_b(side))
            self.assertEqual(kinds, ["facade", "front_high", "front_low"])

    def test_every_arch_is_seen_whole_from_10_to_20_m_in_front_of_the_door(self):
        for side in (-1, 1):
            for a in gg.arch_layout_b(side):
                for viewer in gg.view_points():
                    self.assertGreaterEqual(gg.view_cos(a["mid"], a["n"], viewer), gg.FACE_MIN_COS, (side, a["kind"], viewer))
                    self.assertTrue(gg.arch_sight_clear(a, viewer), (side, a["kind"], viewer))

    def test_the_facade_arch_has_half_a_metre_of_stone_to_the_door_frame(self):
        for side in (-1, 1):
            a = [x for x in gg.arch_layout_b(side) if x["kind"] == "facade"][0]
            gap = abs(a["mid"][0]) - a["width"] / 2.0 - gg.PORTAL_HALF_W
            self.assertGreaterEqual(gap, gg.FRAME_CLEAR)

    def test_every_arch_stands_on_its_face_with_room_at_the_sides(self):
        o = gg.octagon(gg.TUR_CX, gg.TUR_CY, gg.TUR_A)
        flat = abs(o[1][0] - o[2][0])
        self.assertGreaterEqual((flat - gg.LOW_ARCH[2]) / 2.0, 0.3)
        self.assertGreaterEqual((gg.FACADE_JOIN_X - gg.PORTAL_HALF_W - gg.FACADE_ARCH[2]) / 2.0, gg.FRAME_CLEAR)

    def test_the_arches_clear_the_band_the_course_and_the_cornice(self):
        for z0, h, _w in (gg.LOW_ARCH, gg.FACADE_ARCH):
            self.assertEqual(z0, jp.ARCH_Z0)                      # the legs stand on the band like the wall's
            self.assertLessEqual(z0 + h, jp.STRING_Z0 - 0.5)
        z0, h, _w = gg.HIGH_ARCH
        self.assertGreaterEqual(z0, jp.Z_DECK + 0.3)
        self.assertLessEqual(z0 + h, gg.CORNICE_Z0 - 0.2)

    def test_the_facade_arch_x_is_the_middle_of_the_flat_facade(self):
        self.assertAlmostEqual(gg.FACADE_ARCH_X, (gg.PORTAL_HALF_W + gg.FACADE_JOIN_X) / 2.0, places=12)


class SpireMappingTests(unittest.TestCase):
    """The slate of a spire: V is the distance up the profile, so it has no step where the slope changes."""

    def test_v_is_zero_at_the_eave_and_the_slant_length_at_the_tip(self):
        prof = gg.spire_profile()
        total = sum(math.hypot(a1 - a0, z1 - z0) for (z0, a0), (z1, a1) in zip(prof, prof[1:]))
        self.assertAlmostEqual(gg.spire_slant(prof[0][0]), 0.0, places=9)
        self.assertAlmostEqual(gg.spire_slant(prof[-1][0]), total, places=9)

    def test_the_slant_rises_at_the_rate_of_each_band_and_has_no_step_at_a_ring(self):
        prof = gg.spire_profile()
        for (z0, a0), (z1, a1) in zip(prof, prof[1:]):
            rate = math.hypot(a1 - a0, z1 - z0) / (z1 - z0)
            for k in range(1, 9):
                za, zb = z0 + (z1 - z0) * (k - 1) / 8.0, z0 + (z1 - z0) * k / 8.0
                self.assertAlmostEqual((gg.spire_slant(zb) - gg.spire_slant(za)) / (zb - za), rate, places=9)
        for z, _a in prof[1:-1]:
            self.assertAlmostEqual(gg.spire_slant(z - 1e-9), gg.spire_slant(z + 1e-9), places=6)

    def test_v_of_a_vertex_depends_on_its_height_only(self):
        prof = gg.spire_profile()
        for z, a in prof[1:-1]:
            r = a / math.cos(math.pi / 8.0)
            for phi in (22.5, 112.5, 247.5):
                co = (r * math.cos(math.radians(phi)), r * math.sin(math.radians(phi)), z)
                self.assertAlmostEqual(gg.spire_uv_v(co), jp.TD * gg.spire_slant(z), places=12)

    def test_the_slate_keeps_its_density_up_the_slope(self):
        prof = gg.spire_profile()
        for (z0, a0), (z1, a1) in zip(prof, prof[1:]):
            slant = math.hypot(a1 - a0, z1 - z0)
            self.assertAlmostEqual((gg.spire_uv_v((0.0, 0.0, z1)) - gg.spire_uv_v((0.0, 0.0, z0))) / slant, jp.TD, places=9)

    def test_the_arris_rib_is_full_size_low_down_and_ends_in_a_point(self):
        prof = gg.spire_profile()
        self.assertEqual(gg.spire_rib_section(prof[0][1]), (gg.SPIRE_RIB_HALF_W, gg.SPIRE_RIB_SINK, gg.SPIRE_RIB_OUT))
        self.assertEqual(gg.spire_rib_section(0.0), (0.0, 0.0, 0.0))
        sizes = [gg.spire_rib_section(a)[0] for _z, a in prof]
        for x, y in zip(sizes, sizes[1:]):
            self.assertGreaterEqual(x, y)

    def test_neighbouring_arris_ribs_never_touch(self):
        for _z, a in gg.spire_profile():
            half, _sink, _out = gg.spire_rib_section(a)
            r = a / math.cos(math.pi / 8.0)
            self.assertLessEqual(2.0 * half, 2.0 * r * math.sin(math.pi / 8.0) * 0.8 + 1e-12)


class RoofMirrorUvTests(unittest.TestCase):
    """The two slopes of the roof carry the same UV at mirror points."""

    def test_the_soffit_rule_gives_a_mirror_pair_the_same_uv(self):
        for side in (-1, 1):
            n = (side * 0.45, 0.0, -0.893)
            m = (-n[0], n[1], n[2])
            for p in ((1.0, 2.0, 18.0), (3.3, -4.0, 16.0), (0.0, 5.0, 19.0)):
                q = (-p[0], p[1], p[2])
                a, b = gg.planar_uv_sym(n, p), gg.planar_uv_sym(m, q)
                self.assertAlmostEqual(a[0], b[0], places=12)
                self.assertAlmostEqual(a[1], b[1], places=12)

    def test_the_soffit_rule_keeps_the_density(self):
        n = (0.45, 0.0, -0.893)
        t = (0.893, 0.0, 0.45)
        pts = [(1.0, 2.0, 18.0), (1.0 + t[0], 2.0, 18.0 + t[2]), (1.0, 3.0, 18.0)]
        uvs = [gg.planar_uv_sym(n, p) for p in pts]
        lo, hi = jp.uv_scale_axes(pts, uvs)
        self.assertAlmostEqual(lo, jp.TD, places=9)
        self.assertAlmostEqual(hi, jp.TD, places=9)

    def test_a_bar_on_each_slope_maps_the_same_at_mirror_points(self):
        for side in (-1, 1):
            p0, p1 = gg.rafter_axis(side)
            q0, q1 = (-p0[0], p0[1]), (-p1[0], p1[1])

            def at(t, dz):
                return (p0[0] + t * (p1[0] - p0[0]), 1.0, p0[1] + t * (p1[1] - p0[1]) + dz)
            for n, co in (((0.0, 1.0, 0.0), at(0.3, 0.05)), ((0.45 * side, 0.0, 0.893), at(0.5, 0.1)),
                          ((0.45 * side, 0.0, -0.893), at(0.7, -0.1))):
                a = gg.bar_uv(n, co, p0, p1, gg.RAFTER_W, 0.9, 1.1, "S1")
                b = gg.bar_uv((-n[0], n[1], n[2]), (-co[0], co[1], co[2]), q0, q1, gg.RAFTER_W, 0.9, 1.1, "S1")
                self.assertAlmostEqual(a[0], b[0], places=9)
                self.assertAlmostEqual(a[1], b[1], places=9)

    def test_the_gable_opening_turns_at_most_four_degrees_between_segments_except_at_the_apex(self):
        turns = gg.path_turns_deg(gg.gable_opening(gg.GABLE_STEPS))
        turns.pop(gg.GABLE_STEPS)                      # the point of the pointed arch
        self.assertLessEqual(max(turns), 4.0)


class PedestalClearanceTests(unittest.TestCase):
    """G2: the pedestal at the door keeps the same small clear distance to the door frame and to the foot of the drum, and never
    touches either at any height."""

    def test_the_foot_of_the_drum_is_wider_than_the_drum_at_the_facade(self):
        self.assertLess(gg.drum_foot_x(), gg.WING_X1)
        self.assertAlmostEqual(gg.drum_cap_edge_x(0.0), gg.drum_foot_x(), places=12)
        self.assertAlmostEqual(gg.drum_cap_edge_x(jp.Z_PLINTH), gg.WING_X1, places=9)

    def test_the_same_small_distance_on_both_sides_where_each_neighbour_is_widest(self):
        cap_to_door = gg.STATUE_X - gg.PEDESTAL_CAP_W / 2.0 - gg.PORTAL_HALF_W
        foot_to_drum = gg.drum_foot_x() - (gg.STATUE_X + gg.PEDESTAL_W / 2.0)
        self.assertAlmostEqual(cap_to_door, gg.STATUE_CLEAR, places=9)
        self.assertAlmostEqual(foot_to_drum, gg.STATUE_CLEAR, places=9)

    def test_no_contact_at_any_height_and_never_closer_than_the_clear_distance(self):
        for k in range(0, 13):
            z = k * 0.1
            door, drum = gg.pedestal_clearances(z)
            self.assertGreaterEqual(door, gg.STATUE_CLEAR - 1e-9, z)
            self.assertGreaterEqual(drum, gg.STATUE_CLEAR - 1e-9, z)

    def test_the_clearances_at_the_foot_the_middle_and_the_top(self):
        foot, mid, top = (gg.pedestal_clearances(z) for z in (0.0, gg.PEDESTAL_H / 2.0, gg.PEDESTAL_H))
        self.assertAlmostEqual(foot[0], gg.STATUE_CLEAR + gg.PEDESTAL_CAP_OVER, places=9)   # the body, the door frame is vertical
        self.assertAlmostEqual(foot[1], gg.STATUE_CLEAR, places=9)                           # the body against the drum's foot
        self.assertAlmostEqual(mid[0], foot[0], places=9)
        self.assertGreater(mid[1], foot[1])                                                  # the drum leans away from the foot
        self.assertAlmostEqual(top[0], gg.STATUE_CLEAR, places=9)                            # the cap
        self.assertGreater(top[1], gg.STATUE_CLEAR)

    def test_the_pedestal_is_a_mirror_pair_about_the_door(self):
        for side in (-1, 1):
            lo, hi = sorted((side * (gg.STATUE_X - gg.PEDESTAL_W / 2.0), side * (gg.STATUE_X + gg.PEDESTAL_W / 2.0)))
            self.assertAlmostEqual(abs(lo + hi) / 2.0, gg.STATUE_X, places=9)

    def test_the_niche_leaves_stone_beside_the_door_frame(self):
        self.assertGreaterEqual(gg.STATUE_X - gg.NICHE_HALF_W - gg.PORTAL_HALF_W, 0.25)


class CorbelCollarTests(unittest.TestCase):
    """G3: the end of a stub corbel that points into the bay lies on the inner edge of the rib foot where the foot meets the
    collar top."""

    def test_the_end_is_at_the_inner_edge_of_the_widest_limb_at_the_collar_top(self):
        offs = [gg.rib_foot_inner_offset(spec, gg.STUB_RIB_REACH) for spec in (jp.RIB_LONG, jp.RIB_LOW)]
        self.assertAlmostEqual(gg.STUB_COLLAR_IN, max(offs), places=9)
        for off in offs:
            self.assertLessEqual(off, gg.STUB_COLLAR_IN + 1e-9)

    def test_the_foot_sits_whole_on_the_collar_and_the_collar_is_shorter_than_before(self):
        foot_in = jp.RIB_LONG[7] + jp.RIB_LONG[3] / 2.0          # the inner edge of the foot where it starts, Z 8.45
        self.assertGreaterEqual(gg.STUB_COLLAR_IN, foot_in)
        self.assertLess(gg.STUB_COLLAR_IN, jp.COLLAR_HALF_W)

    def test_the_two_limbs_end_within_3_cm_of_each_other(self):
        offs = [gg.rib_foot_inner_offset(spec, gg.STUB_RIB_REACH) for spec in (jp.RIB_LONG, jp.RIB_LOW)]
        self.assertLess(max(offs) - min(offs), 0.03)


if __name__ == "__main__":
    unittest.main()
