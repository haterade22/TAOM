#!/usr/bin/env python3
"""Unit tests for tools/oneoff/rivendell_joint_profile.py (pure Python, no Blender).

Run:  python -m unittest tools.tests.test_rivendell_joint_profile
"""
import importlib.util
import math
import os
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location(
    "rivendell_joint_profile", os.path.join(TOOLS, "oneoff", "rivendell_joint_profile.py"))
jp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(jp)

# the polygons that tile the section without overlap: since version 4 that is every polygon of it (the
# parapet and the rail stand on the deck, and the overlaps at the end planes are gone)
TILING = ("foundation", "plinth", "body", "pier", "string_course", "collar", "inner_plinth_course",
          "inner_pilaster", "inner_string_course", "frieze", "parapet", "parapet_cap", "rail", "rail_cap")
INNER = ("inner_plinth_course", "inner_pilaster", "inner_string_course")


def area(poly):
    return 0.5 * sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1]
                     for i in range(len(poly)))


def inside(poly, p):
    y, z = p
    hit = False
    for i in range(len(poly)):
        (y0, z0), (y1, z1) = poly[i], poly[(i + 1) % len(poly)]
        if (z0 > z) != (z1 > z) and y < y0 + (z - z0) * (y1 - y0) / (z1 - z0):
            hit = not hit
    return hit


def bounds(poly):
    ys, zs = [p[0] for p in poly], [p[1] for p in poly]
    return min(ys), max(ys), min(zs), max(zs)


class SectionTests(unittest.TestCase):
    def setUp(self):
        self.sec = jp.section()

    def test_every_polygon_is_closed_counter_clockwise_and_not_degenerate(self):
        for name, poly in self.sec.items():
            self.assertGreaterEqual(len(poly), 4, name)
            self.assertNotEqual(poly[0], poly[-1], name + ": the closing point is implied")
            self.assertGreater(area(poly), 1e-4, name)

    def test_the_pinned_names_are_present(self):
        for name in ("foundation", "plinth", "body", "pier", "string_course", "parapet", "parapet_cap",
                     "rail", "rail_cap", "collar", "inner_plinth_course", "inner_pilaster",
                     "inner_string_course", "frieze"):
            self.assertIn(name, self.sec)

    def test_foundation_plinth_and_body_extents(self):
        self.assertEqual(bounds(self.sec["foundation"]), (-2.5, 3.1, -8.0, 0.0))
        self.assertEqual(bounds(self.sec["plinth"]), (-2.5, 3.1, 0.0, 3.0))
        self.assertEqual(bounds(self.sec["body"]), (-2.5, 2.5, 3.0, 12.0))

    def test_plinth_batters_from_3_1_at_the_ground_to_2_5_at_the_top(self):
        pts = self.sec["plinth"]
        self.assertIn((3.1, 0.0), pts)
        self.assertIn((2.5, 3.0), pts)

    def test_string_course_is_015_proud_between_z_10_5_and_10_9(self):
        y0, y1, z0, z1 = bounds(self.sec["string_course"])
        self.assertEqual((y0, z0, z1), (2.5, 10.5, 10.9))
        self.assertAlmostEqual(y1, 2.65)

    def test_plinth_cap_and_band_members(self):
        cap, band = jp.member("plinth_cap"), jp.member("band")
        self.assertEqual((bounds(cap)[2], bounds(cap)[3]), (3.0, 3.2))
        self.assertAlmostEqual(bounds(cap)[1], 2.6)
        self.assertEqual((bounds(band)[2], bounds(band)[3]), (3.4, 4.0))
        self.assertAlmostEqual(bounds(band)[1], 2.58)

    def test_pier_proud_at_the_ground_the_top_and_as_a_pilaster(self):
        self.assertAlmostEqual(jp.pier_front_y(0.0) - 2.5, 1.0)
        self.assertAlmostEqual(jp.pier_front_y(8.5) - 2.5, 0.5)
        y0, y1, z0, z1 = bounds(self.sec["pier"])
        self.assertAlmostEqual(z1, 10.5)
        pilaster = [p for p in self.sec["pier"] if p[1] > 8.5]
        self.assertTrue(all(abs(p[0] - 2.8) < 1e-9 or abs(p[0] - 2.5) < 1e-9 for p in pilaster))

    def test_pier_is_1_2_wide_so_a_joint_pier_gives_each_piece_0_6(self):
        self.assertAlmostEqual(jp.PIER_WIDTH, 1.2)
        self.assertAlmostEqual(jp.PIER_HALF * 2, jp.PIER_WIDTH)

    def test_a_whole_pier_has_the_niche_notch_in_its_front(self):
        pts = jp.pier_profile(niche=True)
        back = [p[1] for p in pts if p[0] == jp.NICHE_BACK_Y and p[1] > 0.0]
        self.assertEqual(back, [jp.NICHE_Z0, jp.NICHE_Z0 + jp.NICHE_H])

    def test_parapet_is_045_thick_flush_with_the_wall_face_and_cap_top_14_4(self):
        y0, y1, z0, z1 = bounds(self.sec["parapet"])
        self.assertAlmostEqual(y1 - y0, 0.45)
        self.assertAlmostEqual(y1, jp.Y_OUT)  # no lip: a lip at the deck line drew a dark hairline on the wall
        self.assertEqual(z0, jp.Z_DECK)
        self.assertEqual(bounds(self.sec["parapet_cap"])[3], 14.4)

    def test_rail_is_02_thick_flush_with_the_inner_face_and_top_12_9(self):
        y0, y1, z0, _ = bounds(self.sec["rail"])
        self.assertAlmostEqual(y1 - y0, 0.2)
        self.assertAlmostEqual(y0, jp.Y_IN)
        self.assertEqual(z0, jp.Z_DECK)
        self.assertEqual(bounds(self.sec["rail_cap"])[3], 12.9)

    def test_the_deck_walk_keeps_its_clear_width_of_version_3(self):
        self.assertAlmostEqual(jp.PARAPET_Y0 - jp.RAIL_Y1, 4.35)

    def test_no_two_polygons_of_the_section_overlap_so_no_end_plane_has_two_faces_in_one_plane(self):
        names = sorted(self.sec)
        self.assertEqual(sorted(TILING), names)
        for iy in range(-300, 400):
            for z in (-7.95, -0.5, 0.5, 2.9, 3.1, 3.5, 8.2, 8.6, 10.6, 11.95, 12.05, 12.3, 12.85, 13.0, 13.8,
                      13.99, 14.1, 14.3):
                p = (iy / 100.0 + 0.0031, z)
                hits = [n for n in names if inside(self.sec[n], p)]
                self.assertLessEqual(len(hits), 1, (p, hits))

    def test_the_body_polygons_tile_without_overlap(self):
        names = [n for n in TILING if n in self.sec]
        for iy in range(-30, 40):
            for iz in range(-90, 150):
                p = (iy / 10.0 + 0.0123, iz / 10.0 + 0.0137)
                hits = [n for n in names if inside(self.sec[n], p)]
                self.assertLessEqual(len(hits), 1, (p, hits))
        for iy in range(-300, 400):  # a fine pass over the thin members
            for z in (3.05, 3.15, 3.21, 8.2, 8.4, 8.6, 8.8, 10.55, 10.8, 13.7, 13.95):
                p = (iy / 100.0 + 0.0031, z)
                hits = [n for n in names if inside(self.sec[n], p)]
                self.assertLessEqual(len(hits), 1, (p, hits))

    def test_no_parapet_face_lies_in_a_wall_face_plane(self):
        wall_ys = {2.5, -2.5, 3.1}
        wall_zs = {12.0, 3.0, 0.0, -8.0}
        # the parapet and the rail themselves stand flush with the wall faces by design; the caps, whose faces
        # would z-fight with the wall below, must not have an edge in a wall plane (a corner alone is fine)
        for name in ("parapet_cap", "rail_cap"):
            poly = self.sec[name]
            for i in range(len(poly)):
                (y0, z0), (y1, z1) = poly[i], poly[(i + 1) % len(poly)]
                if abs(y0 - y1) < 1e-9:
                    self.assertNotIn(round(y0, 6), wall_ys, name)
                if abs(z0 - z1) < 1e-9:
                    self.assertNotIn(round(z0, 6), wall_zs, name)

    def test_plain_pier_profile_has_no_notch(self):
        plain = jp.pier_profile(niche=False)
        self.assertEqual(len(plain), len(jp.pier_profile(niche=True)) - 4)


class InnerFaceTests(unittest.TestCase):
    def setUp(self):
        self.sec = jp.section()

    def test_pilaster_is_09_wide_and_010_proud_behind_the_pier(self):
        self.assertAlmostEqual(jp.PILASTER_IN_W, 0.9)
        y0, y1, z0, z1 = bounds(self.sec["inner_pilaster"])
        self.assertAlmostEqual(y1, jp.Y_IN)
        self.assertAlmostEqual(y0, jp.Y_IN - 0.10)
        self.assertAlmostEqual(z0, jp.INNER_PLINTH_Z1)
        self.assertAlmostEqual(z1, jp.STRING_Z0)

    def test_inner_courses_sit_at_the_levels_of_the_outer_face(self):
        y0, y1, z0, z1 = bounds(self.sec["inner_plinth_course"])
        self.assertAlmostEqual(z0, jp.Z_PLINTH)
        self.assertAlmostEqual(y1, jp.Y_IN)
        self.assertGreater(jp.Y_IN - y0, 0.09)
        y0, y1, z0, z1 = bounds(self.sec["inner_string_course"])
        self.assertAlmostEqual(z0, jp.STRING_Z0)
        self.assertAlmostEqual(z1, jp.STRING_Z1)
        self.assertAlmostEqual(y1, jp.Y_IN)

    def test_the_inner_face_stays_calm_nothing_is_more_than_015_proud(self):
        for name in INNER:
            y0, _, _, _ = bounds(self.sec[name])
            self.assertLessEqual(jp.Y_IN - y0, 0.15 + 1e-9, name)

    def test_inner_members_stay_clear_of_the_rail(self):
        for name in INNER:
            self.assertLess(bounds(self.sec[name])[3], jp.RAIL_FOOT, name)

    def test_the_pilaster_stands_on_the_course_and_the_course_ends_under_it(self):
        pil = bounds(self.sec["inner_pilaster"])
        course = bounds(self.sec["inner_plinth_course"])
        self.assertAlmostEqual(pil[2], course[3])
        self.assertAlmostEqual(pil[3], bounds(self.sec["inner_string_course"])[2])


class CollarTests(unittest.TestCase):
    def test_collar_stands_on_the_pier_ledge_in_front_of_the_pilaster(self):
        poly = jp.section()["collar"]
        y0, y1, z0, z1 = bounds(poly)
        self.assertGreater(y1, jp.Y_OUT + jp.PIER_PROUD_TOP)
        self.assertGreaterEqual(y0, jp.Y_OUT + jp.PILASTER_PROUD - 1e-9)
        self.assertLess(z0, jp.PIER_TOP_Z)
        self.assertGreater(z1, jp.PIER_TOP_Z)
        self.assertLess(z1, jp.STRING_Z0)

    def test_collar_is_wider_than_the_pier_by_a_little(self):
        self.assertGreater(jp.COLLAR_HALF_W, jp.PIER_HALF)
        self.assertLessEqual(jp.COLLAR_HALF_W - jp.PIER_HALF, 0.1)


class FriezeTests(unittest.TestCase):
    def test_frieze_is_a_flat_band_005_proud_under_the_cap(self):
        y0, y1, z0, z1 = bounds(jp.section()["frieze"])
        self.assertAlmostEqual(y0, jp.PARAPET_Y1)
        self.assertAlmostEqual(y1 - y0, 0.05)
        self.assertAlmostEqual(z1, jp.PARAPET_BODY_TOP)
        self.assertGreaterEqual(z1 - z0, 0.38)  # the S5 strip is 0.43 m tall at one tile per 2.222 m

    def test_the_openings_stay_below_the_frieze(self):
        apex = jp.Z_DECK + jp.OPEN_SILL + jp.OPEN_H
        self.assertLess(apex, bounds(jp.section()["frieze"])[2])

    def test_strip_s5_is_the_arcade(self):
        self.assertEqual(jp.STRIPS["S5"], (0.805, 1.0))

    def test_four_metre_part_has_two_openings_at_a_quarter_and_three_quarters(self):
        self.assertEqual(jp.parapet_openings(4.0), [1.0, 3.0])

    def test_short_parts_get_one_opening_from_1_m_up(self):
        self.assertEqual(jp.parapet_openings(1.0), [0.5])
        self.assertEqual(len(jp.parapet_openings(1.9)), 1)

    def test_a_part_shorter_than_1_m_is_refused(self):
        with self.assertRaises(ValueError):
            jp.parapet_openings(0.99)

    def test_every_length_keeps_each_opening_clear_of_the_ends_and_of_its_neighbour(self):
        for n in range(0, 161):
            length = 1.0 + n * 0.05
            cs = jp.parapet_openings(length)
            self.assertGreaterEqual(len(cs), 1)
            self.assertGreaterEqual(cs[0] - jp.OPEN_W / 2, 0.2 - 1e-9, length)
            self.assertGreaterEqual(length - cs[-1] - jp.OPEN_W / 2, 0.2 - 1e-9, length)
            for a, b in zip(cs, cs[1:]):
                self.assertGreaterEqual(b - a - jp.OPEN_W, 0.4 - 1e-9, length)

    def test_openings_are_symmetric(self):
        for length in (1.0, 2.5, 3.85, 4.0, 5.3):
            cs = jp.parapet_openings(length)
            self.assertEqual([round(length - c, 9) for c in cs[::-1]], [round(c, 9) for c in cs])


class ArchTests(unittest.TestCase):
    def test_pointed_arch_stands_on_zero_and_reaches_its_height(self):
        pts = jp.pointed_arch(0.6, 2.4, 6)
        self.assertEqual(pts[0], (-0.3, 0.0))
        self.assertEqual(pts[-1], (0.3, 0.0))
        self.assertAlmostEqual(max(v for _, v in pts), 2.4, places=6)

    def test_half_arch_runs_from_the_apex_to_the_right_foot(self):
        pts = jp.half_arch(0.6, 2.4, 6)
        self.assertAlmostEqual(pts[0][0], 0.0, places=6)
        self.assertAlmostEqual(pts[0][1], 2.4, places=6)
        self.assertEqual(pts[-1], (0.3, 0.0))

    def test_signed_arc_length_is_zero_at_the_apex_and_odd_about_it(self):
        pts = jp.pointed_arch(0.6, 2.4, 6)
        apex = max(range(len(pts)), key=lambda i: pts[i][1])
        s = jp.signed_arc_lengths(pts, apex)
        self.assertEqual(s[apex], 0.0)
        self.assertLess(s[0], 0.0)
        self.assertGreater(s[-1], 0.0)
        self.assertAlmostEqual(s[0], -s[-1], places=6)
        for a, b in zip(s, s[1:]):
            self.assertGreater(b, a)


class ArcLengthAtTests(unittest.TestCase):
    def test_vertices_and_midpoints_get_their_own_length(self):
        pts = jp.pointed_arch(0.6, 2.4, 6)
        apex = 7
        s = jp.signed_arc_lengths(pts, apex)
        for i, (p, v) in enumerate(zip(pts, s)):
            self.assertAlmostEqual(jp.arc_length_at(pts, s, p[0], p[1]), v, places=9)
        mid = ((pts[2][0] + pts[3][0]) / 2.0, (pts[2][1] + pts[3][1]) / 2.0)
        self.assertAlmostEqual(jp.arc_length_at(pts, s, *mid), (s[2] + s[3]) / 2.0, places=9)

    def test_a_point_a_little_off_the_line_takes_the_nearest(self):
        pts = [(0.0, 0.0), (0.0, 1.0), (0.0, 2.0)]
        s = jp.signed_arc_lengths(pts, 1)
        self.assertAlmostEqual(jp.arc_length_at(pts, s, 0.01, 1.5), 0.5, places=9)


class UvTests(unittest.TestCase):
    def test_density_is_045_and_a_tile_is_2_222_m(self):
        self.assertEqual(jp.TD, 0.45)
        self.assertAlmostEqual(1.0 / jp.TD, 2.2222222, places=6)

    def test_outer_and_inner_faces_start_u_at_the_pieces_low_end(self):
        self.assertEqual(jp.world_uv((0, 1, 0), (-10.0, 2.5, 4.0), -10.0), (0.0, 0.45 * 4.0))
        self.assertEqual(jp.world_uv((0, -1, 0), (10.0, -2.5, 4.0), -10.0), (0.45 * 20.0, 0.45 * 4.0))

    def test_a_20_m_wall_holds_9_tiles_a_gatehouse_12_and_a_tower_6(self):
        self.assertAlmostEqual(jp.TD * 20.0, 9.0)
        self.assertAlmostEqual(jp.TD * (80.0 / 3.0), 12.0)
        self.assertAlmostEqual(jp.TD * (40.0 / 3.0), 6.0)

    def test_every_piece_length_is_a_whole_number_of_tiles(self):
        for bays in (2, 3, 4):
            tiles = jp.TD * bays * jp.BAY
            self.assertAlmostEqual(tiles, round(tiles), places=6)

    def test_end_faces_use_y(self):
        self.assertEqual(jp.world_uv((1, 0, 0), (10.0, 1.5, 6.0), -10.0), (0.45 * 1.5, 0.45 * 6.0))

    def test_horizontal_faces_use_x_from_the_low_end_and_y(self):
        self.assertEqual(jp.world_uv((0, 0, 1), (-9.0, 1.0, 12.0), -10.0), (0.45, 0.45))

    def test_courses_run_through_a_joint(self):
        a = jp.world_uv((0, 1, 0), (10.0, 2.5, 5.0), -10.0)
        b = jp.world_uv((0, 1, 0), (10.0, 2.5, 5.0), 10.0)
        self.assertAlmostEqual(a[1], b[1])
        self.assertAlmostEqual((a[0] - b[0]) % 1.0, 0.0)

    def test_battered_face_keeps_density_along_the_slope(self):
        slant = math.hypot(0.6, 3.0)
        n = (0.0, 3.0 / slant, 0.6 / slant)
        a = jp.world_uv(n, (0.0, 3.1, 0.0), 0.0)
        b = jp.world_uv(n, (0.0, 2.5, 3.0), 0.0)
        self.assertAlmostEqual(abs(b[1] - a[1]), 0.45 * slant, places=6)

    def test_trim_runs_u_along_the_member_from_the_low_end(self):
        lo, hi = (-10.0, 2.5, 10.5), (10.0, 2.65, 10.9)
        a = jp.trim_uv((0, 1, 0), (-10.0, 2.65, 10.5), "S2", 0, lo, hi, -10.0)
        b = jp.trim_uv((0, 1, 0), (10.0, 2.65, 10.9), "S2", 0, lo, hi, -10.0)
        self.assertAlmostEqual(a[0], 0.0)
        self.assertAlmostEqual(b[0], 9.0)
        self.assertAlmostEqual(a[1], 0.28)
        self.assertAlmostEqual(b[1], 0.49)

    def test_strips_are_the_pinned_ones(self):
        self.assertEqual(jp.STRIPS["S1"], (0.0, 0.28))
        self.assertEqual(jp.STRIPS["S5"], (0.805, 1.0))

    def test_the_old_copy_of_the_rules_with_0_7_is_gone(self):
        self.assertFalse(os.path.exists(os.path.join(TOOLS, "oneoff", "rivendell_wall_uv.py")))


class PlinthUvTests(unittest.TestCase):
    """The plinth texture has a bed joint at V phase 0.3767, the top edge of the plinth (Z 3.0)."""

    SLANT = math.hypot(0.6, 3.0)
    OUTER = (0.0, 3.0 / math.hypot(0.6, 3.0), 0.6 / math.hypot(0.6, 3.0))

    def test_the_constants_come_from_the_section(self):
        self.assertAlmostEqual(jp.PLINTH_SLANT, 3.0594117, places=6)
        self.assertAlmostEqual(jp.PLINTH_V_TOP, 1.3767, places=4)
        self.assertAlmostEqual(jp.PLINTH_V_TOP % 1.0, 0.3767, places=4)

    def test_the_battered_outer_face_has_v_1_3767_at_the_top_edge_for_every_x_and_y(self):
        for x in (-10.0, -3.3, 0.0, 7.0):
            v = jp.plinth_uv(self.OUTER, (x, 2.5, 3.0), -10.0)[1]
            self.assertAlmostEqual(v, jp.PLINTH_V_TOP, places=9)
            self.assertAlmostEqual(v, 1.3767, places=4)

    def test_the_battered_outer_face_runs_v_along_the_slope_from_0_at_the_ground_line(self):
        self.assertAlmostEqual(jp.plinth_uv(self.OUTER, (0.0, 3.1, 0.0), 0.0)[1], 0.0, places=9)
        for z in (0.5, 1.0, 2.0, 2.9):
            y = 3.1 - 0.2 * z
            self.assertAlmostEqual(jp.plinth_uv(self.OUTER, (0.0, y, z), 0.0)[1],
                                   jp.TD * math.hypot(3.1 - y, z), places=9)

    def test_the_battered_outer_face_keeps_the_density_along_the_slope_and_across(self):
        p = [(0.0, 3.1, 0.0), (2.0, 3.1, 0.0), (2.0, 2.5, 3.0)]
        uvs = [jp.plinth_uv(self.OUTER, c, 0.0) for c in p]
        lo, hi = jp.uv_scale_axes(p, uvs)
        self.assertAlmostEqual(lo, jp.TD, places=6)
        self.assertAlmostEqual(hi, jp.TD, places=6)

    def test_a_vertical_plinth_face_has_v_1_3767_at_z_3(self):
        for n in ((0.0, -1.0, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0), (-1.0, 0.0, 0.0)):
            v = jp.plinth_uv(n, (0.0, -2.5, 3.0), 0.0)[1]
            self.assertAlmostEqual(v, jp.PLINTH_V_TOP, places=9, msg=str(n))

    def test_a_vertical_plinth_face_runs_v_at_the_density_and_the_rule_goes_on_below_the_ground(self):
        for z in (-8.0, -3.0, 0.0, 1.0, 3.0):
            v = jp.plinth_uv((0.0, 1.0, 0.0), (0.0, 3.1, z), 0.0)[1]
            self.assertAlmostEqual(v, jp.PLINTH_V_TOP + jp.TD * (z - 3.0), places=9)

    def test_u_is_the_walls_u_from_the_pieces_low_end(self):
        self.assertAlmostEqual(jp.plinth_uv(self.OUTER, (4.0, 2.8, 1.0), -10.0)[0], jp.TD * 14.0)
        self.assertAlmostEqual(jp.plinth_uv((0.0, -1.0, 0.0), (-10.0, -2.5, 1.0), -10.0)[0], 0.0)
        self.assertAlmostEqual(jp.plinth_uv((1.0, 0.0, 0.0), (10.0, 1.5, 1.0), -10.0)[0], jp.TD * 1.5)

    def test_a_horizontal_face_keeps_the_box_rule(self):
        self.assertEqual(jp.plinth_uv((0.0, 0.0, 1.0), (-9.0, 1.0, 3.0), -10.0), (jp.TD, jp.TD))

    def test_a_cone_face_slope_tangent_is_used_for_any_batter(self):
        # a face leaning twice as hard: the slope length from the ground to Z 3 is longer
        slant = math.hypot(1.2, 3.0)
        n = (0.0, 3.0 / slant, 1.2 / slant)
        self.assertAlmostEqual(jp.plinth_uv(n, (0.0, 2.5, 3.0), 0.0)[1], jp.TD * slant, places=9)
        self.assertAlmostEqual(jp.plinth_v_slope(3.0, 3.0 / slant), jp.TD * slant, places=9)

    def test_the_pier_front_stone_courses_are_level_with_the_wall_face(self):
        # the stone rule on a face battered 0.5 m over 8.5 m: V follows the slope from the ground line
        slant = math.hypot(0.5, 8.5)
        n = (0.0, 8.5 / slant, 0.5 / slant)
        for z in (0.0, 2.0, 4.0, 8.5):
            v = jp.world_uv(n, (0.0, 3.5 - 0.5 * z / 8.5, z), 0.0)[1]
            self.assertLess(abs(v - jp.TD * z), 0.01, z)
        v_top = jp.world_uv(n, (0.0, 3.0, 8.5), 0.0)[1]
        self.assertAlmostEqual(v_top, jp.TD * slant, places=9)


class HeapUvTests(unittest.TestCase):
    def test_a_heap_face_takes_a_box_projection_by_its_dominant_direction(self):
        self.assertEqual(jp.heap_uv((0.0, 1.0, 0.0), (-9.0, 2.0, 5.0), -10.0), (jp.TD * 1.0, jp.TD * 5.0))
        self.assertEqual(jp.heap_uv((1.0, 0.0, 0.0), (3.0, 2.0, 5.0), -10.0), (jp.TD * 2.0, jp.TD * 5.0))
        self.assertEqual(jp.heap_uv((0.0, 0.0, 1.0), (-9.0, 2.0, 5.0), -10.0), (jp.TD * 1.0, jp.TD * 2.0))

    def test_the_dominant_direction_picks_the_largest_component(self):
        n = (0.3, 0.9, 0.3)
        self.assertEqual(jp.heap_uv(n, (1.0, 2.0, 3.0), 0.0), (jp.TD * 1.0, jp.TD * 3.0))
        n = (0.9, 0.3, 0.3)
        self.assertEqual(jp.heap_uv(n, (1.0, 2.0, 3.0), 0.0), (jp.TD * 2.0, jp.TD * 3.0))

    def test_a_battered_face_is_not_mapped_along_its_slope_a_heap_has_no_courses(self):
        slant = math.hypot(0.6, 3.0)
        n = (0.0, 3.0 / slant, 0.6 / slant)
        self.assertEqual(jp.heap_uv(n, (0.0, 2.5, 3.0), 0.0), (0.0, jp.TD * 3.0))

    def test_a_flat_face_of_a_heap_has_the_pinned_density(self):
        p = [(0, 2.5, 0), (2, 2.5, 0), (2, 2.5, 3)]
        uvs = [jp.heap_uv((0, 1, 0), c, 0.0) for c in p]
        lo, hi = jp.uv_scale_axes(p, uvs)
        self.assertAlmostEqual(lo, jp.TD, places=6)
        self.assertAlmostEqual(hi, jp.TD, places=6)


class RoofUvTests(unittest.TestCase):
    """U along the eave (horizontal), V up the slope, at the pinned density."""

    def gable(self, side):
        """Normal of a gable face of pitch 30 degrees; side +1 faces +Y, -1 faces -Y."""
        a = math.radians(30.0)
        return (0.0, side * math.sin(a), math.cos(a))

    def test_a_gable_face_runs_u_along_x_from_the_low_end(self):
        for side in (1, -1):
            n = self.gable(side)
            a = jp.roof_uv(n, (-8.0, 1.0, 20.0), -10.0)
            b = jp.roof_uv(n, (-6.0, 1.0, 20.0), -10.0)
            self.assertAlmostEqual(b[0] - a[0], jp.TD * 2.0, places=9)
            self.assertAlmostEqual(b[1], a[1], places=9)

    def test_a_gable_face_runs_v_up_the_slope_at_the_density(self):
        for side in (1, -1):
            n = self.gable(side)
            up = (0.0, -side * math.cos(math.radians(30.0)) * 1.0, math.sin(math.radians(30.0)))
            # a point 2 m up the slope from the eave point
            p0 = (0.0, 5.0, 10.0)
            p1 = (0.0, 5.0 + 2.0 * up[1], 10.0 + 2.0 * up[2])
            v0 = jp.roof_uv(n, p0, 0.0)[1]
            v1 = jp.roof_uv(n, p1, 0.0)[1]
            self.assertAlmostEqual(v1 - v0, jp.TD * 2.0, places=9)

    def test_a_gable_face_has_the_pinned_density_both_ways(self):
        n = self.gable(1)
        up = (0.0, -math.cos(math.radians(30.0)), math.sin(math.radians(30.0)))
        p = [(0.0, 0.0, 10.0), (3.0, 0.0, 10.0), (3.0, 2.0 * up[1], 10.0 + 2.0 * up[2])]
        uvs = [jp.roof_uv(n, c, 0.0) for c in p]
        lo, hi = jp.uv_scale_axes(p, uvs)
        self.assertAlmostEqual(lo, jp.TD, places=6)
        self.assertAlmostEqual(hi, jp.TD, places=6)

    def test_a_flat_roof_face_falls_back_to_the_horizontal_box(self):
        self.assertEqual(jp.roof_uv((0.0, 0.0, 1.0), (-9.0, 1.0, 12.0), -10.0), (jp.TD, jp.TD))

    def spire(self, n_sides=8, apothem=2.0, height=6.0, z0=20.0, cx=3.0, cy=-1.0):
        """Facets of a pyramid on a regular polygon: (normal, eave vertex a, eave vertex b, apex)."""
        r = apothem / math.cos(math.pi / n_sides)
        ring = [(cx + r * math.cos(2 * math.pi * (k + 0.5) / n_sides), cy + r * math.sin(2 * math.pi * (k + 0.5) / n_sides))
                for k in range(n_sides)]
        out = []
        for k in range(n_sides):
            a, b = ring[k], ring[(k + 1) % n_sides]
            pa, pb, pc = (a[0], a[1], z0), (b[0], b[1], z0), (cx, cy, z0 + height)
            u = [pb[i] - pa[i] for i in range(3)]
            w = [pc[i] - pa[i] for i in range(3)]
            nrm = [u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0]]
            ln = math.sqrt(sum(c * c for c in nrm))
            out.append((tuple(c / ln for c in nrm), pa, pb, pc))
        return out, 2.0 * n_sides * apothem * math.tan(math.pi / n_sides)

    def test_u_is_continuous_round_the_facets_of_a_spire_at_the_eave(self):
        facets, perimeter = self.spire()
        kw = dict(axis=(3.0, -1.0), eave=perimeter, seam=math.radians(20.0))
        for k in range(len(facets) - 1):
            n1, a1, b1, _ = facets[k]
            n2, a2, b2, _ = facets[k + 1]
            u_end = jp.roof_uv(n1, b1, 0.0, **kw)[0]
            u_start = jp.roof_uv(n2, a2, 0.0, **kw)[0]
            self.assertAlmostEqual(u_end, u_start, places=9, msg=str(k))

    def test_u_runs_on_by_the_eave_length_of_a_facet_from_one_facet_to_the_next(self):
        facets, perimeter = self.spire()
        kw = dict(axis=(3.0, -1.0), eave=perimeter)
        n1, a1, b1, _ = facets[2]
        side = math.dist(a1, b1)
        self.assertAlmostEqual(jp.roof_uv(n1, b1, 0.0, **kw)[0] - jp.roof_uv(n1, a1, 0.0, **kw)[0],
                               jp.TD * side, places=9)

    def test_a_spire_facet_has_the_pinned_density_both_ways(self):
        facets, perimeter = self.spire()
        kw = dict(axis=(3.0, -1.0), eave=perimeter)
        for n, a, b, c in facets:
            p = [a, b, c]
            uvs = [jp.roof_uv(n, q, 0.0, **kw) for q in p]
            lo, hi = jp.uv_scale_axes(p, uvs)
            self.assertAlmostEqual(lo, jp.TD, places=6)
            self.assertAlmostEqual(hi, jp.TD, places=6)

    def test_v_is_equal_on_every_facet_at_the_same_height(self):
        facets, perimeter = self.spire()
        kw = dict(axis=(3.0, -1.0), eave=perimeter)
        vs = {round(jp.roof_uv(n, a, 0.0, **kw)[1], 9) for n, a, b, c in facets}
        self.assertEqual(len(vs), 1)
        mid = [(a[0] * 0.5 + c[0] * 0.5, a[1] * 0.5 + c[1] * 0.5, a[2] * 0.5 + c[2] * 0.5) for n, a, b, c in facets]
        vm = {round(jp.roof_uv(n, q, 0.0, **kw)[1], 9) for (n, a, b, c), q in zip(facets, mid)}
        self.assertEqual(len(vm), 1)

    def test_v_grows_up_the_slope(self):
        facets, perimeter = self.spire()
        n, a, b, c = facets[3]
        kw = dict(axis=(3.0, -1.0), eave=perimeter)
        self.assertGreater(jp.roof_uv(n, c, 0.0, **kw)[1], jp.roof_uv(n, a, 0.0, **kw)[1])

    def test_the_seam_is_one_cut_of_u_by_the_whole_eave(self):
        facets, perimeter = self.spire()
        kw = dict(axis=(3.0, -1.0), eave=perimeter)
        us = [jp.roof_uv(n, a, 0.0, **kw)[0] for n, a, b, c in facets]
        jumps = [i for i in range(len(us) - 1) if abs(us[i + 1] - us[i]) > jp.TD * perimeter / 2.0]
        self.assertLessEqual(len(jumps), 1)
        self.assertAlmostEqual(max(us) - min(us), jp.TD * perimeter * (len(us) - 1) / len(us), places=6)


class MaterialSlotTests(unittest.TestCase):
    def test_darken_hex_takes_a_share_off_every_channel(self):
        self.assertEqual(jp.darken_hex("877a68", 0.15), "736858")
        self.assertEqual(jp.darken_hex("ffffff", 0.0), "ffffff")
        self.assertEqual(jp.darken_hex("c7b89e", 1.0), "000000")

    def test_the_heap_colour_is_the_plinth_colour_15_percent_darker(self):
        self.assertEqual(jp.HEAP_COLOUR, jp.darken_hex(jp.RUBBLE_COLOUR, 0.15))
        self.assertEqual(jp.RUBBLE_COLOUR, "877a68")
        self.assertEqual(jp.ROOF_COLOUR, "34405f")

    def test_the_two_new_kinds_have_a_tone_in_range(self):
        for kind in ("roof", "heap"):
            for x in (0.0, 1.3, 5.0):
                for z in (-1.0, 0.0, 2.0, 12.0, 25.0):
                    for v in jp.vertex_tone(kind, x, z, 0.0):
                        self.assertGreaterEqual(v, jp.TONE_MIN - 1e-12)
                        self.assertLessEqual(v, 1.0 + 1e-12)

    def test_a_heap_is_as_damp_as_rubble(self):
        self.assertEqual(jp.vertex_tone("heap", 1.0, 0.0), jp.vertex_tone("rubble", 1.0, 0.0))


class FanApexTests(unittest.TestCase):
    """The apex of a fan that triangulates a polygon without a zero-area triangle."""

    def signed(self, pts, n):
        s = 0.0
        for i in range(len(pts)):
            a, b = pts[i], pts[(i + 1) % len(pts)]
            c = (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
            s += sum(c[j] * n[j] for j in range(3))
        return s

    def fan_ok(self, pts, n, k):
        sgn = 1.0 if self.signed(pts, n) > 0 else -1.0
        m = len(pts)
        for i in range(1, m - 1):
            a, b, c = pts[k], pts[(k + i) % m], pts[(k + i + 1) % m]
            u = [b[j] - a[j] for j in range(3)]
            w = [c[j] - a[j] for j in range(3)]
            cr = (u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0])
            if sgn * sum(cr[j] * n[j] for j in range(3)) <= 1e-9:
                return False
        return True

    def test_a_thin_triangle_with_points_on_one_edge_fans_from_the_far_vertex(self):
        pts = [(-9.914, 3.1, 3.24), (-10.0, 3.1, 0.9), (-10.0, 3.1, 1.05), (-10.0, 3.1, 2.1), (-10.0, 3.1, 3.15),
               (-10.0, 3.1, 3.3)]
        n = (0.0, 1.0, 0.0)
        k = jp.fan_apex(pts, n)
        self.assertEqual(k, 0)
        self.assertTrue(self.fan_ok(pts, n, k))

    def test_the_winding_does_not_matter(self):
        pts = [(-9.914, 3.1, 3.24), (-10.0, 3.1, 0.9), (-10.0, 3.1, 1.05), (-10.0, 3.1, 2.1), (-10.0, 3.1, 3.15),
               (-10.0, 3.1, 3.3)][::-1]
        n = (0.0, 1.0, 0.0)
        k = jp.fan_apex(pts, n)
        self.assertEqual(pts[k], (-9.914, 3.1, 3.24))

    def test_a_clean_quad_has_a_fan(self):
        pts = [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)]
        n = (0.0, -1.0, 0.0)
        self.assertTrue(self.fan_ok(pts, n, jp.fan_apex(pts, n)))

    def test_a_square_with_a_point_in_the_middle_of_each_edge_has_a_fan(self):
        pts = [(0, 0, 0), (0.5, 0, 0), (1, 0, 0), (1, 0, 0.5), (1, 0, 1), (0.5, 0, 1), (0, 0, 1), (0, 0, 0.5)]
        n = (0.0, -1.0, 0.0)
        k = jp.fan_apex(pts, n)
        self.assertIsNotNone(k)
        self.assertTrue(self.fan_ok(pts, n, k))

    def test_a_concave_polygon_that_one_vertex_sees_whole_has_a_fan(self):
        pts = [(0, 0, 0), (2, 0, 0), (2, 0, 1), (1, 0, 1), (1, 0, 2), (0, 0, 2)]
        n = (0.0, -1.0, 0.0)
        k = jp.fan_apex(pts, n)
        self.assertIsNotNone(k)
        self.assertTrue(self.fan_ok(pts, n, k))

    def test_a_polygon_with_all_points_on_a_line_has_none(self):
        self.assertIsNone(jp.fan_apex([(0, 0, 0), (1, 0, 0), (2, 0, 0), (3, 0, 0)], (0.0, 1.0, 0.0)))

    def test_the_run_test_finds_three_collinear_corners(self):
        self.assertTrue(jp.has_collinear_run([(0, 0, 0), (1, 0, 0), (2, 0, 0), (2, 0, 1)]))
        self.assertTrue(jp.has_collinear_run([(0, 0, 1), (0, 0, 0), (1, 0, 0), (2, 0, 0)]))  # across the start
        self.assertFalse(jp.has_collinear_run([(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)]))
        self.assertFalse(jp.has_collinear_run([(0, 0, 0), (1, 0, 0), (0, 0, 1)]))


class UvScaleTests(unittest.TestCase):
    """uv_scale_axes moved here from rivendell_wall_uv.py, with its tests."""

    def test_scale_check_reads_exactly_the_pinned_density(self):
        p = [(0, 2.5, 0), (2, 2.5, 0), (2, 2.5, 3)]
        n = (0, 1, 0)
        uvs = [jp.world_uv(n, c, 0.0) for c in p]
        lo, hi = jp.uv_scale_axes(p, uvs)
        self.assertAlmostEqual(lo, jp.TD, places=6)
        self.assertAlmostEqual(hi, jp.TD, places=6)

    def test_scale_check_catches_a_stretch(self):
        p = [(0, 0, 0), (1, 0, 0), (1, 0, 1)]
        uvs = [(0, 0), (0.45, 0), (0.45, 0.9)]
        lo, hi = jp.uv_scale_axes(p, uvs)
        self.assertAlmostEqual(lo, 0.45, places=6)
        self.assertAlmostEqual(hi, 0.9, places=6)

    def test_degenerate_triangle_gives_zero(self):
        self.assertEqual(jp.uv_scale_axes([(0, 0, 0), (0, 0, 0), (1, 0, 0)], [(0, 0), (0, 0), (1, 1)]), (0.0, 0.0))


class RibTests(unittest.TestCase):
    SPECS = ("RIB_LONG", "RIB_LOW")

    def test_width_tapers_in_a_straight_line_and_never_grows(self):
        for name in self.SPECS:
            spec = getattr(jp, name)
            ws = [jp.rib_width(spec[3], spec[4], i / 20.0) for i in range(21)]
            for a, b in zip(ws, ws[1:]):
                self.assertLessEqual(b, a + 1e-12, name)
            self.assertGreaterEqual(ws[0], 0.36, name)  # a firm foot on the collar
            self.assertLessEqual(ws[-1], 0.25, name)
            steps = [a - b for a, b in zip(ws, ws[1:])]
            self.assertLess(max(steps) - min(steps), 1e-9, name)  # no flare: the taper is one straight rate

    def test_proud_never_grows(self):
        for name in self.SPECS:
            spec = getattr(jp, name)
            ps = [jp.rib_proud(spec[5], spec[6], i / 20.0) for i in range(21)]
            for a, b in zip(ps, ps[1:]):
                self.assertLessEqual(b, a + 1e-12, name)

    def test_the_limb_springs_from_inside_the_collar_and_does_not_stand_proud_of_it(self):
        for name in self.SPECS:
            spec = getattr(jp, name)
            self.assertGreaterEqual(spec[0], jp.COLLAR_Z0 + 0.1, name)
            self.assertLess(spec[0], jp.PIER_TOP_Z, name)
            self.assertLessEqual(spec[5], jp.COLLAR_PROUD, name)
            foot = jp.rib_width(spec[3], spec[4], 0.0)
            self.assertLessEqual(spec[7] + foot / 2.0, jp.COLLAR_HALF_W + 1e-9, name)

    def test_proud_tapers_in_a_straight_line_like_the_width(self):
        for name in self.SPECS:
            spec = getattr(jp, name)
            ps = [jp.rib_proud(spec[5], spec[6], i / 20.0) for i in range(21)]
            steps = [a - b for a, b in zip(ps, ps[1:])]
            self.assertLess(max(steps) - min(steps), 1e-9, name)

    def test_curve_leaves_the_pier_going_up_and_reaches_the_bay_centre_at_the_apex(self):
        spec = jp.RIB_LONG
        pts = jp.rib_curve(1, spec, 0.0, 16)
        self.assertEqual(len(pts), 17)
        dx, z, tx, tz = pts[0]
        self.assertAlmostEqual(dx, spec[7])
        self.assertAlmostEqual(z, spec[0])
        self.assertAlmostEqual(tx, 0.0)
        dx, z, _, _ = pts[-1]
        self.assertAlmostEqual(dx, jp.HALF_BAY)
        self.assertAlmostEqual(z, spec[1])

    def test_rib_t_runs_from_0_at_the_foot_to_1_at_the_apex(self):
        for name in self.SPECS:
            spec = getattr(jp, name)
            self.assertAlmostEqual(jp.rib_t(spec, spec[0]), 0.0)
            self.assertAlmostEqual(jp.rib_t(spec, spec[1]), 1.0)
            ts = [jp.rib_t(spec, spec[0] + (spec[1] - spec[0]) * i / 10.0) for i in range(11)]
            self.assertEqual(ts, sorted(ts))
            self.assertAlmostEqual(jp.rib_t(spec, spec[0] - 1.0), 0.0)
            self.assertAlmostEqual(jp.rib_t(spec, spec[1] + 1.0), 1.0)

    def test_the_foot_keeps_clear_of_the_joint_plane_by_more_than_the_seam_tools_band(self):
        """A vertex within 0.02 m of an end plane but not on it fails the seam test."""
        for name in self.SPECS:
            spec = getattr(jp, name)
            half = jp.rib_width(spec[3], spec[4], 0.0) / 2.0
            self.assertGreaterEqual(spec[7] - half, 0.02, name)

    def test_no_three_points_of_the_collar_profile_are_collinear(self):
        """The ear clipper made an empty triangle of three points on one level (the end faces had 7 of them)."""
        poly = jp.collar_profile()
        n = len(poly)
        for i in range(n):
            for j in range(i + 1, n):
                for k in range(j + 1, n):
                    a, b, c = poly[i], poly[j], poly[k]
                    cross = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
                    self.assertGreater(abs(cross), 1e-6, (i, j, k))

    def test_sign_mirrors_the_curve(self):
        a = jp.rib_curve(1, jp.RIB_LOW, 2.0, 8)
        b = jp.rib_curve(-1, jp.RIB_LOW, 2.0, 8)
        for p, q in zip(a, b):
            self.assertAlmostEqual(p[0], -q[0])
            self.assertAlmostEqual(p[1], q[1])
            self.assertAlmostEqual(p[2], -q[2])

    def test_explicit_half_bay_moves_the_end(self):
        pts = jp.rib_curve(1, jp.RIB_LONG, 0.0, 12, half_bay=2.0)
        self.assertAlmostEqual(pts[-1][0], 2.0)
        self.assertAlmostEqual(pts[-1][1], jp.RIB_LONG[1])

    def test_default_half_bay_is_read_when_called_not_when_defined(self):
        saved = jp.HALF_BAY
        try:
            jp.HALF_BAY = 2.5
            self.assertAlmostEqual(jp.rib_curve(1, jp.RIB_LONG, 0.0, 6)[-1][0], 2.5)
        finally:
            jp.HALF_BAY = saved

    def test_clip_ends_the_centre_line_exactly_at_the_offset(self):
        spec = jp.RIB_LONG
        for clip in (0.34, 0.5, 1.0, 2.0, 3.0, 3.3):
            pts = jp.rib_curve(1, spec, 0.0, 10, clip_off=clip)
            self.assertEqual(len(pts), 11)
            self.assertAlmostEqual(pts[-1][0], clip, places=9)
            xs = [p[0] for p in pts]
            self.assertEqual(xs, sorted(xs))

    def test_clip_past_the_end_changes_nothing(self):
        spec = jp.RIB_LONG
        a = jp.rib_curve(1, spec, 0.0, 10)
        b = jp.rib_curve(1, spec, 0.0, 10, clip_off=jp.HALF_BAY + 1.0)
        self.assertEqual(a, b)

    def test_a_clip_at_or_before_the_foot_centre_gives_no_curve(self):
        spec = jp.RIB_LONG
        for clip in (0.0, spec[7]):
            self.assertEqual(jp.rib_curve(1, spec, 0.0, 10, clip_off=clip), [], clip)

    def test_a_plane_that_cuts_the_foot_gives_no_rib_centre(self):
        spec = jp.RIB_LONG
        self.assertGreater(jp.rib_clip_min(spec), spec[7] + jp.rib_width(spec[3], spec[4], 0.0) / 2.0)
        for off in (0.0, 0.3, spec[7] + 0.1, jp.rib_clip_min(spec) - 0.001):
            self.assertIsNone(jp.rib_clip_centre(spec, off), off)

    def test_the_centre_is_chosen_so_that_no_section_crosses_the_plane(self):
        """The far edge of the last section (centre + half the width times the section's x part) lies on the plane
        and the far edge of no earlier section, the foot included, is beyond it."""
        for spec in (jp.RIB_LONG, jp.RIB_LOW):
            for off in (jp.rib_clip_min(spec), 0.8, 1.2, 2.0, 3.0):
                e = jp.rib_clip_centre(spec, off)
                self.assertIsNotNone(e, (spec, off))
                self.assertLess(e, off)
                far = []
                for c in (e * k / 20.0 + spec[7] * (1 - k / 20.0) + 1e-9 for k in range(0, 21)):
                    pts = jp.rib_curve(1, spec, 0.0, 1, clip_off=c)
                    _, z, tx, tz = pts[-1]
                    hw = jp.rib_width(spec[3], spec[4], jp.rib_t(spec, z)) / 2.0
                    far.append(c + hw * abs(tz) / math.hypot(tx, tz))
                self.assertAlmostEqual(far[-1], off, places=6)
                self.assertLessEqual(max(far), off + 1e-6)

    def test_far_from_the_foot_the_centre_is_close_to_the_plane(self):
        e = jp.rib_clip_centre(jp.RIB_LONG, 3.0)
        self.assertGreater(e, 2.9)

    def test_clip_works_for_the_other_sign(self):
        pts = jp.rib_curve(-1, jp.RIB_LOW, 0.0, 10, clip_off=1.5)
        self.assertAlmostEqual(pts[-1][0], -1.5, places=9)

    def test_clip_offset_from_an_absolute_plane(self):
        self.assertAlmostEqual(jp.clip_offset(1, 3.0, 4.5), 1.5)
        self.assertAlmostEqual(jp.clip_offset(-1, 3.0, 1.5), 1.5)

    def test_cut_direction_never_runs_parallel_to_the_plane(self):
        for tan_x, sign in ((0.0, 1), (0.1, 1), (-0.3, -1), (0.9, 1)):
            w = jp.cut_direction(tan_x, 1.0, sign)
            self.assertGreaterEqual(abs(w[0]), 0.4)
            self.assertEqual(math.copysign(1.0, w[0]), sign)
            self.assertAlmostEqual(math.hypot(*w), 1.0)


class CutGridTests(unittest.TestCase):
    def test_no_cell_is_wider_than_the_limit_and_the_planes_are_inside(self):
        for lo, hi in ((0.0, 3.0), (-10.0, 10.0), (3.0, 12.0), (11.9, 14.0), (0.0, 10.5), (0.0, 1.1), (0.0, 1.11)):
            cuts = jp.even_cuts(lo, hi, jp.CELL_MAX)
            edges = [lo] + cuts + [hi]
            self.assertTrue(all(lo < c < hi for c in cuts))
            for a, b in zip(edges, edges[1:]):
                self.assertLessEqual(b - a, jp.CELL_MAX + 1e-9, (lo, hi))

    def test_cells_are_equal_and_as_few_as_possible(self):
        cuts = jp.even_cuts(-10.0, 10.0, 1.1)
        self.assertEqual(len(cuts) + 1, 19)  # 20 / 1.1 = 18.2
        edges = [-10.0] + cuts + [10.0]
        widths = {round(b - a, 9) for a, b in zip(edges, edges[1:])}
        self.assertEqual(len(widths), 1)

    def test_a_span_that_fits_one_cell_is_not_cut(self):
        self.assertEqual(jp.even_cuts(0.0, 1.1, 1.1), [])
        self.assertEqual(jp.even_cuts(0.0, 0.4, 1.1), [])

    def test_the_parapet_slab_needs_no_cut_across_and_three_along_the_columns(self):
        self.assertEqual(jp.even_cuts(jp.PARAPET_FOOT, jp.PARAPET_BODY_TOP, jp.CELL_MAX), [])
        self.assertEqual(len(jp.column_cuts(-10.0, -6.0, -10.0)), 3)

    def test_the_rows_are_3_m_so_the_wall_stays_inside_its_triangle_budget(self):
        """Version 6: the wash is weak (about 3 levels), so the cells are coarse: rows of at most 3 m. The wall body,
        Z 0 to 12, is four rows."""
        self.assertEqual(jp.CELL_MAX, 3.0)
        self.assertEqual(len(jp.even_cuts(0.0, jp.Z_DECK, jp.CELL_MAX)), 3)

    def test_a_pier_face_is_too_narrow_to_cut_across(self):
        self.assertLess(jp.PIER_WIDTH, jp.CELL_SKIP)

    def test_trim_members_are_cut_at_a_coarser_pitch(self):
        self.assertGreater(jp.CELL_TRIM, jp.CELL_MAX)
        cuts = jp.even_cuts(-10.0, 10.0, jp.CELL_TRIM)
        self.assertEqual(len(cuts) + 1, 6)


class ToneTests(unittest.TestCase):
    KINDS = ("stone", "rubble", "trim")

    def test_range_is_080_to_100_everywhere(self):
        for kind in self.KINDS:
            for ix in range(0, 400):
                for z in (-3.0, 0.0, 0.5, 1.0, 1.5, 3.0, 6.0, 9.5, 10.2, 12.5, 13.5, 14.4):
                    for rec in (0.0, 1.0):
                        c = jp.vertex_tone(kind, ix * 0.05 - 3.0, z, rec)
                        self.assertEqual(len(c), 3)
                        for v in c:
                            self.assertGreaterEqual(v, 0.80 - 1e-12)
                            self.assertLessEqual(v, 1.0 + 1e-12)

    def test_is_periodic_in_the_bay_so_both_sides_of_a_joint_agree(self):
        for kind in self.KINDS:
            for x in (0.0, 0.3, 1.7, 3.3, 5.5, 6.6):
                for z in (0.2, 5.0, 10.0, 13.0):
                    a = jp.vertex_tone(kind, x, z)
                    b = jp.vertex_tone(kind, x + jp.BAY, z)
                    c = jp.vertex_tone(kind, x + 3 * jp.BAY, z)
                    for p, q, r in zip(a, b, c):
                        self.assertLess(abs(p - q), 1e-9)
                        self.assertLess(abs(p - r), 1e-9)

    def test_a_piece_end_at_20_m_equals_its_start_to_half_a_level(self):
        for kind in self.KINDS:
            for z in [i * 0.25 for i in range(0, 58)]:
                a = jp.vertex_tone(kind, 0.0, z)
                b = jp.vertex_tone(kind, 20.0, z)
                for p, q in zip(a, b):
                    self.assertLess(abs(p - q) * 255.0, 0.5)

    def test_the_foot_is_darker_and_damp_tinted_green(self):
        foot = jp.vertex_tone("stone", 1.0, 0.0)
        mid = jp.vertex_tone("stone", 1.0, 6.0)
        self.assertLess(sum(foot), sum(mid))
        self.assertGreater(foot[1], foot[2])  # green above blue
        self.assertGreaterEqual(foot[1], foot[0])

    def test_damp_fades_out_by_1_5_m(self):
        self.assertAlmostEqual(jp.damp(1.5), 0.0)
        self.assertAlmostEqual(jp.damp(2.5), 0.0)
        self.assertAlmostEqual(jp.damp(0.0), 1.0)
        self.assertGreater(jp.damp(0.7), 0.0)
        self.assertLess(jp.damp(0.7), 1.0)

    def test_rain_streak_under_the_string_course_is_darker_than_beside_it(self):
        drip = jp.DRIP_STRING[0]
        gap = (jp.DRIP_STRING[0] + jp.DRIP_STRING[1]) / 2.0  # between two drips
        under = jp.streak(drip * jp.BAY, jp.STRING_Z0 - 0.5)
        beside = jp.streak(gap * jp.BAY, jp.STRING_Z0 - 0.5)
        above = jp.streak(drip * jp.BAY, jp.STRING_Z1 + 0.5)
        self.assertGreater(under, beside)
        self.assertGreater(under, above)

    def test_rain_streak_under_the_cap_and_under_an_arch(self):
        cap = jp.DRIP_CAP[0]
        self.assertGreater(jp.streak(cap * jp.BAY, jp.FRIEZE_Z0 - 0.5),
                           jp.streak((cap + 0.08) * jp.BAY, jp.FRIEZE_Z0 - 0.5))
        arch = jp.DRIP_ARCH[0]
        self.assertGreater(jp.streak(arch * jp.BAY, 7.0), 0.0)
        self.assertGreater(jp.streak(arch * jp.BAY, 7.0), jp.streak((arch + 0.08) * jp.BAY, 7.0) - 1e-12)

    def test_streak_fades_with_distance_below_its_source(self):
        drip = jp.DRIP_STRING[0] * jp.BAY
        near, far = jp.streak(drip, jp.STRING_Z0 - 0.5), jp.streak(drip, jp.STRING_Z0 - 2.5)
        self.assertGreater(near, far)

    def test_recess_darkens(self):
        a = jp.vertex_tone("stone", 2.0, 5.0, 0.0)
        b = jp.vertex_tone("stone", 2.0, 5.0, 1.0)
        for p, q in zip(a, b):
            self.assertLess(q, p)

    def test_cast_is_slight(self):
        for z in (0.0, 0.8, 5.0, 10.0):
            c = jp.vertex_tone("stone", 1.3, z)
            self.assertLess(max(c) - min(c), 0.08)

    def test_broad_wash_changes_across_the_bay(self):
        tones = {round(jp.vertex_tone("stone", x * 0.5, 5.0)[1], 4) for x in range(14)}
        self.assertGreater(len(tones), 3)


class PierTopTests(unittest.TestCase):
    """Version 5: the pilaster above the collar dies into the wall in a taper (no flat top), so that no pier
    stands prouder than the string course where it reaches it."""

    def setUp(self):
        self.pts = jp.pier_profile(niche=False)

    def edges(self):
        return [(self.pts[i], self.pts[(i + 1) % len(self.pts)]) for i in range(len(self.pts))]

    def test_no_flat_top_above_the_collar_ledge(self):
        for a, b in self.edges():
            if abs(a[1] - b[1]) < 1e-9:
                self.assertLessEqual(a[1], jp.PIER_TOP_Z + 1e-9, (a, b))

    def test_the_taper_starts_on_the_pilaster_face_and_ends_on_the_wall_face_at_the_string_course(self):
        self.assertIn((jp.Y_OUT + jp.PILASTER_PROUD, jp.PILASTER_TAPER_Z0), self.pts)
        self.assertIn((jp.Y_OUT, jp.STRING_Z0), self.pts)
        self.assertEqual(max(p[1] for p in self.pts), jp.STRING_Z0)
        self.assertLess(jp.PIER_TOP_Z, jp.PILASTER_TAPER_Z0)
        self.assertLess(jp.PILASTER_TAPER_Z0, jp.STRING_Z0)

    def test_the_taper_starts_at_the_top_of_the_collar(self):
        """Version 6: no proud stretch stands between the collar and the taper."""
        self.assertAlmostEqual(jp.PILASTER_TAPER_Z0, jp.COLLAR_Z1)
        face = [p for p in self.pts if abs(p[0] - (jp.Y_OUT + jp.PILASTER_PROUD)) < 1e-9 and p[1] > jp.PIER_TOP_Z]
        self.assertEqual(max(p[1] for p in face), jp.COLLAR_Z1)

    def test_the_taper_is_one_straight_slope_from_the_collar_top_to_the_string_course(self):
        a = (jp.Y_OUT + jp.PILASTER_PROUD, jp.COLLAR_Z1)
        b = (jp.Y_OUT, jp.STRING_Z0)
        between = [p for p in self.pts if a[1] < p[1] < b[1]]
        self.assertEqual(between, [])
        for z in (9.0, 9.5, 10.0):
            self.assertAlmostEqual(jp.pilaster_proud(z), jp.PILASTER_PROUD * (jp.STRING_Z0 - z) / (jp.STRING_Z0 - jp.COLLAR_Z1))

    def test_proud_of_the_pilaster_never_grows_going_up(self):
        above = [p for p in self.pts if p[1] >= jp.PIER_TOP_Z and jp.Y_OUT - 1e-9 < p[0] < jp.Y_OUT + 0.4]
        above.sort(key=lambda p: p[1])
        proud = [p[0] - jp.Y_OUT for p in above]
        for a, b in zip(proud, proud[1:]):
            self.assertLessEqual(b, a + 1e-9)

    def test_the_taper_is_a_gentle_weathering_slope(self):
        run = jp.STRING_Z0 - jp.PILASTER_TAPER_Z0
        deg = math.degrees(math.atan2(jp.PILASTER_PROUD, run))
        self.assertGreaterEqual(deg, 8.0)
        self.assertLessEqual(deg, 20.0)

    def test_no_pier_member_stands_prouder_than_the_trim_it_meets_from_below(self):
        sec = jp.section()
        top = max(p[1] for p in sec["pier"])
        proud_at_top = max(p[0] for p in sec["pier"] if abs(p[1] - top) < 1e-9) - jp.Y_OUT
        self.assertLess(proud_at_top, jp.STRING_PROUD)
        # inside: the pilaster stands under the inner string course and is less proud than it
        self.assertLess(jp.PILASTER_IN_PROUD, jp.INNER_STRING_PROUD)

    def test_no_limb_is_cut_by_the_pier_strip_it_stands_well_in_front_of_it(self):
        """Where a limb is over the strip's width, its front stands at least 0.10 m proud of the strip."""
        for spec in (jp.RIB_LONG, jp.RIB_LOW):
            for dx, z, tx, tz in jp.rib_curve(1, spec, 0.0, 60):
                t = jp.rib_t(spec, z)
                hw = jp.rib_width(spec[3], spec[4], t) / 2.0
                near = dx - hw * abs(tz) / math.hypot(tx, tz)
                if near < jp.PIER_HALF and z > jp.PIER_TOP_Z:
                    self.assertGreaterEqual(jp.rib_proud(spec[5], spec[6], t) - jp.pilaster_proud(z), 0.10, (spec, z))

    def test_the_pier_reaches_no_higher_than_the_foot_of_the_string_course(self):
        sec = jp.section()
        for p in sec["pier"]:
            if p[1] > jp.STRING_Z0 - 1e-9:
                self.assertAlmostEqual(p[0], jp.Y_OUT)


class ArchGroupTests(unittest.TestCase):
    """Version 5: the band under the blind arches ends flush with the outer faces of the outer legs."""

    def test_the_arch_constants_are_the_ones_of_version_4(self):
        self.assertEqual((jp.ARCH_Z0, jp.ARCH_W, jp.ARCH_H, jp.ARCH_MOULD, jp.ARCH_PROUD, jp.ARCH_STEP),
                         (3.95, 1.6, 4.55, 0.14, 0.12, 1.8))

    def test_a_bay_group_is_three_arches_5_2_m_wide_and_inside_the_pier_faces(self):
        half = jp.ARCH_STEP + jp.ARCH_W / 2.0
        self.assertAlmostEqual(2.0 * half, 5.2)
        self.assertLess(half, jp.HALF_BAY - jp.PIER_HALF)

    def test_a_whole_bay_has_one_span_from_leg_to_leg(self):
        mid = -jp.BAY / 2.0
        cs = [mid - jp.ARCH_STEP, mid, mid + jp.ARCH_STEP]
        spans = jp.arch_band_spans(cs, -jp.BAY, 0.0, (-jp.BAY, 0.0))
        self.assertEqual(len(spans), 1)
        self.assertAlmostEqual(spans[0][0], mid - 2.6)
        self.assertAlmostEqual(spans[0][1], mid + 2.6)

    def test_two_bays_give_two_spans(self):
        cs = []
        for mid in (-jp.BAY / 2.0, jp.BAY / 2.0):
            cs += [mid - jp.ARCH_STEP, mid, mid + jp.ARCH_STEP]
        spans = jp.arch_band_spans(cs, -jp.BAY, jp.BAY, (-jp.BAY, 0.0, jp.BAY))
        self.assertEqual(len(spans), 2)
        self.assertLess(spans[0][1], spans[1][0])

    def test_a_cut_end_that_is_not_a_pier_runs_the_band_to_the_cut(self):
        # a stub from x 10 to the pier at 13.333: one arch at 11.8 (the bay middle is at 10)
        pier = 40.0 / 3.0
        spans = jp.arch_band_spans([11.8], 10.0, pier, (pier,))
        self.assertEqual(len(spans), 1)
        self.assertAlmostEqual(spans[0][0], 10.0)
        self.assertAlmostEqual(spans[0][1], 11.8 + jp.ARCH_W / 2.0)

    def test_the_end_at_a_pier_stops_at_the_leg(self):
        pier = 40.0 / 3.0
        lo, hi = jp.arch_band_spans([11.8], 10.0, pier, (pier,))[0]
        self.assertLess(hi, pier - jp.PIER_HALF)

    def test_no_arches_gives_no_span(self):
        self.assertEqual(jp.arch_band_spans([], 0.0, 4.0, ()), [])


class ColumnCutTests(unittest.TestCase):
    def test_six_columns_a_bay_from_the_pier_centre(self):
        cuts = jp.column_cuts(-10.0, 10.0, -10.0)
        self.assertEqual(len(cuts), 17)
        self.assertAlmostEqual(cuts[0], -10.0 + jp.BAY / 6.0)
        self.assertTrue(all(-10.0 < c < 10.0 for c in cuts))

    def test_a_joint_plane_gets_no_cut_and_a_pier_centre_in_the_run_is_a_cut(self):
        cuts = jp.column_cuts(-10.0, 10.0, -10.0)
        self.assertTrue(any(abs(c - (-10.0 + jp.BAY)) < 1e-9 for c in cuts))
        self.assertFalse(any(abs(c - 10.0) < 1e-9 or abs(c + 10.0) < 1e-9 for c in cuts))

    def test_a_short_part_gets_the_cuts_that_fall_inside_it(self):
        self.assertEqual(len(jp.column_cuts(-10.0, -6.0, -10.0)), 3)

    def test_the_columns_follow_the_low_end_of_the_piece(self):
        a = jp.column_cuts(10.0, 13.0, 10.0)
        self.assertAlmostEqual(a[0], 10.0 + jp.BAY / 6.0)


class WeatherTests(unittest.TestCase):
    """Version 5: a wash of 6 to 10 levels across a bay field and the upper wall, run-off streaks of 8 to 12
    levels under the string course, the cap and the arch heads, none of them at a joint plane."""

    def level(self, kind, x, z, rec=0.0):
        return jp.vertex_tone(kind, x, z, rec)[1] * 255.0

    def row_spread(self, z, kind="stone", first=0):
        """Spread, in levels, of the wash alone on the columns of one bay at height z (the streaks are tested apart)."""
        vals = [jp.wash(k * jp.BAY / jp.TONE_COLUMNS, z) * 255.0 for k in range(first, jp.TONE_COLUMNS)]
        return max(vals) - min(vals)

    def test_wash_and_streaks_together_never_spread_more_than_16_levels_on_a_row(self):
        for z in [4.0 + 0.25 * i for i in range(0, 41)]:
            vals = [self.level("stone", k * jp.BAY / jp.TONE_COLUMNS, z) for k in range(jp.TONE_COLUMNS)]
            self.assertLessEqual(max(vals) - min(vals), 16.0, z)

    def test_the_wash_spreads_6_to_10_levels_across_the_bay_field(self):
        # the field is the columns between the pier faces (k 1 to 5); the pier column is left out of the second pass
        for z in (4.5, 5.5, 6.0, 7.0, 9.0, 9.5):
            for first in (0, 1):
                self.assertGreaterEqual(self.row_spread(z, first=first), 6.0, (z, first))
                self.assertLessEqual(self.row_spread(z, first=first), 10.0, (z, first))

    def test_the_wash_spreads_6_to_10_levels_across_the_upper_wall(self):
        for z in (11.0, 11.4, 12.0, 12.5):
            for first in (0, 1):
                self.assertGreaterEqual(self.row_spread(z, first=first), 6.0, (z, first))
                self.assertLessEqual(self.row_spread(z, first=first), 10.0, (z, first))

    def test_the_streaks_peak_at_8_to_12_levels_on_the_row_below_each_source(self):
        for z, phase in ((10.0, jp.DRIP_STRING[0]), (jp.TONE_ROWS[0], jp.DRIP_CAP[0]), (8.0, jp.DRIP_ARCH[0]),
                         (3.0, jp.DRIP_SILL[0])):
            peak = jp.streak(phase * jp.BAY, z) * 255.0
            self.assertGreaterEqual(peak, 8.0, z)
            self.assertLessEqual(peak, 12.0, z)

    def test_on_the_columns_a_streak_is_a_full_drip_or_nothing(self):
        drips = {round(ph * jp.TONE_COLUMNS) for ph in jp.DRIP_STRING + jp.DRIP_CAP + jp.DRIP_ARCH + jp.DRIP_SILL}
        for k in range(jp.TONE_COLUMNS):
            if k in drips:
                continue
            for z in (10.0, jp.TONE_ROWS[0], 8.0, 3.2):
                self.assertEqual(jp.streak(k * jp.BAY / jp.TONE_COLUMNS, z), 0.0, (k, z))

    def test_every_drip_is_on_a_column_and_off_the_joint_plane(self):
        for ph in jp.DRIP_STRING + jp.DRIP_CAP + jp.DRIP_ARCH + jp.DRIP_SILL:
            k = ph * jp.TONE_COLUMNS
            self.assertAlmostEqual(k, round(k), places=9)
            self.assertGreater(round(k), 0)
            self.assertLess(round(k), jp.TONE_COLUMNS)

    def test_no_streak_at_a_joint_plane(self):
        for z in [i * 0.25 for i in range(0, 58)]:
            for n in range(0, 4):
                self.assertEqual(jp.streak(n * jp.BAY, z), 0.0)

    def test_a_streak_never_darkens_a_carved_member(self):
        a = jp.vertex_tone("trim", jp.DRIP_STRING[0] * jp.BAY, 10.0)
        b = jp.vertex_tone("trim", (jp.DRIP_STRING[0] + 0.5 / jp.TONE_COLUMNS) * jp.BAY, 10.0)
        self.assertLess(abs(a[1] - b[1]) * 255.0, 4.0)

    def test_the_wash_is_never_dirty_the_stone_above_the_foot_stays_within_40_levels_of_white(self):
        for ix in range(0, 120):
            for z in (3.2, 6.0, 8.0, 10.0, 12.0, 14.0):
                self.assertGreaterEqual(self.level("stone", ix * jp.BAY / 36.0, z), 255.0 - 40.0)

    def test_the_damp_foot_is_as_it_was_in_version_4(self):
        self.assertAlmostEqual(jp.damp(0.0), 1.0)
        foot = self.level("rubble", 1.0, 0.0)
        self.assertLess(foot, self.level("rubble", 1.0, 3.0) - 20.0)
        self.assertGreaterEqual(foot, 255.0 * jp.TONE_MIN)

    def test_the_parapet_slab_has_a_row_just_under_the_frieze_where_the_cap_streak_starts(self):
        self.assertAlmostEqual(jp.TONE_ROWS[0], jp.FRIEZE_Z0 - 0.05)
        cuts = jp.even_cuts(jp.PARAPET_FOOT, jp.PARAPET_BODY_TOP, jp.CELL_MAX)
        self.assertTrue(all(abs(c - jp.TONE_ROWS[0]) > 0.4 for c in cuts))  # no sliver beside the extra row
        self.assertGreater(jp.TONE_ROWS[0], jp.PARAPET_FOOT)
        self.assertLess(jp.TONE_ROWS[0], jp.PARAPET_BODY_TOP)

    def test_the_tone_at_a_joint_plane_changes_slowly_with_height(self):
        # two pieces may sample a joint on different rows: the tone must be near-linear over a metre
        for z in [i * 0.5 for i in range(6, 26)]:
            a, b, c = (self.level("stone", 0.0, h) for h in (z - 0.5, z, z + 0.5))
            self.assertLess(abs(b - (a + c) / 2.0), 1.0, z)


class EndSectionTests(unittest.TestCase):
    """Version 6 (Mike, picture c1): the half pier at a joint plane has no niche, so a free end is plain and straight."""

    def front_y(self, poly, z):
        ys = []
        for i in range(len(poly)):
            (y0, z0), (y1, z1) = poly[i], poly[(i + 1) % len(poly)]
            if (z0 - z) * (z1 - z) < 0:
                ys.append(y0 + (z - z0) * (y1 - y0) / (z1 - z0))
        return max(ys)

    def test_the_joint_section_pier_is_the_plain_pier(self):
        pier = jp.section()["pier"]
        self.assertEqual(pier, jp.pier_profile(niche=False))
        self.assertFalse(any(p[0] == jp.NICHE_BACK_Y and 0.0 < p[1] < 4.0 for p in pier))

    def test_the_end_front_is_the_battered_pier_front_at_every_height_from_the_ground_to_the_collar(self):
        pier = jp.section()["pier"]
        for z in (0.2, 0.5, 0.9, 1.5, 2.4, 3.3, 4.0, 6.0, 8.0, 8.4):
            self.assertAlmostEqual(self.front_y(pier, z), jp.pier_front_y(z), places=9, msg=z)

    def test_the_niche_notch_is_still_in_the_profile_of_a_whole_pier(self):
        notch = jp.pier_profile(niche=True)
        self.assertEqual(len(notch), len(jp.pier_profile(niche=False)) + 4)
        self.assertAlmostEqual(self.front_y(notch, 2.0), jp.NICHE_BACK_Y)


class PilasterProudTests(unittest.TestCase):
    def test_the_strip_is_0_3_proud_to_the_collar_top_then_slopes_to_nothing_at_the_string_course(self):
        self.assertAlmostEqual(jp.pilaster_proud(8.6), jp.PILASTER_PROUD)
        self.assertAlmostEqual(jp.pilaster_proud(jp.COLLAR_Z1), jp.PILASTER_PROUD)
        self.assertAlmostEqual(jp.pilaster_proud(jp.STRING_Z0), 0.0)
        self.assertAlmostEqual(jp.pilaster_proud(11.0), 0.0)

    def test_it_never_grows_going_up_from_the_collar_top(self):
        zs = [jp.COLLAR_Z1 + 0.05 * i for i in range(0, 40)]
        vals = [jp.pilaster_proud(z) for z in zs]
        for a, b in zip(vals, vals[1:]):
            self.assertLessEqual(b, a + 1e-12)

    def test_below_the_pier_top_it_is_the_battered_pier_front(self):
        self.assertAlmostEqual(jp.pilaster_proud(4.0), jp.pier_front_y(4.0) - jp.Y_OUT)


def chord_turns(pts):
    """Angles in degrees between neighbour chords of the (x, z) polyline pts[i][0], pts[i][1]."""
    dirs = []
    for a, b in zip(pts, pts[1:]):
        dx, dz = b[0] - a[0], b[1] - a[1]
        n = math.hypot(dx, dz)
        dirs.append((dx / n, dz / n))
    return [math.degrees(math.acos(max(-1.0, min(1.0, u[0] * v[0] + u[1] * v[1])))) for u, v in zip(dirs, dirs[1:])]


UP_RIB = (12.6, 15.0, 62.0, 0.46, 0.28, 0.40, 0.20, 0.33)  # the tower builder's spec of a rib that branches near the top


class RibFootTests(unittest.TestCase):
    """Version 6 (Mike, picture c2): each limb stands whole on the collar, the same on both sides of every bay."""
    SPECS = (jp.RIB_LONG, jp.RIB_LOW)
    MARGIN = 0.02

    def edges(self, spec, half_bay=None, steps=200):
        """(z, x of the bay side edge, x of the pier side edge, z of those edges) per ring of the limb on its +1 side."""
        out = []
        for dx, z, tx, tz in jp.rib_curve(1, spec, 0.0, steps, half_bay=half_bay):
            t = jp.rib_t(spec, z)
            hw = jp.rib_width(spec[3], spec[4], t) / 2.0
            n = math.hypot(tx, tz)
            ex, ez = -tz / n, tx / n  # the extrados normal: up and toward the pier
            out.append((z, dx - hw * ex, dx + hw * ex, z - hw * ez, z + hw * ez))
        return out

    def test_the_foot_stands_inside_the_collar_with_a_margin_on_both_sides(self):
        for spec in self.SPECS:
            w = jp.rib_width(spec[3], spec[4], 0.0)
            self.assertLessEqual(spec[7] + w / 2.0, jp.COLLAR_HALF_W - self.MARGIN, spec)
            self.assertGreaterEqual(spec[7] - w / 2.0, 0.05, spec)

    def test_no_part_of_a_limb_hangs_over_the_collar_edge_while_it_is_beside_the_collar(self):
        for hb in (None, 2.5, 1.5333):
            for spec in self.SPECS:
                for z, far, near, z_far, z_near in self.edges(spec, hb):
                    if z_far <= jp.COLLAR_Z1:
                        self.assertLessEqual(far, jp.COLLAR_HALF_W - self.MARGIN, (spec, hb, z))
                    if z_near <= jp.COLLAR_Z1:
                        self.assertGreaterEqual(near, 0.04, (spec, hb, z))

    def test_the_two_ribs_of_a_pier_spring_from_one_foot(self):
        long_, low = jp.RIB_LONG, jp.RIB_LOW
        self.assertAlmostEqual(long_[7], low[7], places=9)
        self.assertAlmostEqual(long_[3], low[3], places=9)
        self.assertAlmostEqual(long_[0], low[0], places=9)
        self.assertAlmostEqual(long_[5], low[5], places=9)

    def test_the_foot_starts_below_the_front_top_edge_of_the_collar_so_it_floats_nowhere(self):
        for spec in self.SPECS:
            self.assertGreater(spec[0], jp.COLLAR_Z0 + 0.13)
            self.assertLess(spec[0], jp.COLLAR_Z0 + 0.13 + 0.3)
            self.assertLessEqual(spec[0], jp.PIER_TOP_Z - 0.04)  # clear of the pier top plane, no plane shared

    def test_the_foot_front_stays_inside_the_collar_front(self):
        for spec in self.SPECS:
            self.assertLess(spec[5], jp.COLLAR_PROUD)


class RibSamplingTests(unittest.TestCase):
    SPECS = (jp.RIB_LONG, jp.RIB_LOW, UP_RIB)

    def test_the_limit_is_4_degrees_and_the_target_is_a_little_under_it(self):
        self.assertEqual(jp.RIB_TURN_LIMIT_DEG, 4.0)
        self.assertLessEqual(jp.RIB_MAX_TURN_DEG, 3.8)

    def test_no_two_neighbour_segments_turn_more_than_4_degrees(self):
        for spec in self.SPECS:
            for hb in (None, 2.5, 1.5333):
                for steps in (4, 8, 16):
                    pts = jp.rib_curve(1, spec, 0.0, steps, half_bay=hb, max_turn_deg=jp.RIB_MAX_TURN_DEG)
                    self.assertGreaterEqual(len(pts) - 1, steps)
                    self.assertLessEqual(max(chord_turns(pts)), jp.RIB_TURN_LIMIT_DEG, (spec, hb, steps))

    def test_the_turn_is_even_along_the_limb(self):
        pts = jp.rib_curve(1, jp.RIB_LOW, 0.0, 4, max_turn_deg=jp.RIB_MAX_TURN_DEG)
        turns = chord_turns(pts)
        self.assertLess(max(turns) - min(turns), 0.6)

    def test_a_limb_has_about_twenty_segments(self):
        for spec in (jp.RIB_LONG, jp.RIB_LOW):
            n = len(jp.rib_curve(1, spec, 0.0, 4, max_turn_deg=jp.RIB_MAX_TURN_DEG)) - 1
            self.assertGreaterEqual(n, 18)
            self.assertLessEqual(n, 26)

    def test_the_sampled_curve_starts_and_ends_where_the_plain_curve_does(self):
        for spec in self.SPECS:
            a = jp.rib_curve(1, spec, 0.0, 12)
            b = jp.rib_curve(1, spec, 0.0, 4, max_turn_deg=jp.RIB_MAX_TURN_DEG)
            for k in (0, 1, 2, 3):
                self.assertAlmostEqual(a[0][k], b[0][k], places=9)
                self.assertAlmostEqual(a[-1][k], b[-1][k], places=9)

    def test_every_sample_lies_on_the_curve(self):
        spec = jp.RIB_LONG
        for dx, z, tx, tz in jp.rib_curve(1, spec, 0.0, 4, max_turn_deg=jp.RIB_MAX_TURN_DEG):
            a = (jp.HALF_BAY - spec[7]) / (1.0 - math.cos(math.radians(spec[2])))
            b = (spec[1] - spec[0]) / math.sin(math.radians(spec[2]))
            self.assertAlmostEqual(((dx - spec[7] - a) / a) ** 2 + ((z - spec[0]) / b) ** 2, 1.0, places=9)

    def test_a_clipped_limb_is_sampled_the_same_way(self):
        for clip in (0.8, 1.5, 3.0):
            pts = jp.rib_curve(1, jp.RIB_LONG, 0.0, 4, clip_off=clip, max_turn_deg=jp.RIB_MAX_TURN_DEG)
            self.assertAlmostEqual(pts[-1][0], clip, places=9)
            self.assertLessEqual(max(chord_turns(pts)) if len(pts) > 2 else 0.0, jp.RIB_TURN_LIMIT_DEG)

    def test_the_old_call_is_unchanged(self):
        pts = jp.rib_curve(1, jp.RIB_LONG, 0.0, 10)
        self.assertEqual(len(pts), 11)

    def test_the_tangent_is_continuous_along_the_limb(self):
        pts = jp.rib_curve(1, jp.RIB_LOW, 0.0, 4, max_turn_deg=jp.RIB_MAX_TURN_DEG)
        angles = [math.degrees(math.atan2(p[3], p[2])) for p in pts]
        for a, b in zip(angles, angles[1:]):
            self.assertLessEqual(abs(a - b), jp.RIB_TURN_LIMIT_DEG)


class RibSectionTests(unittest.TestCase):
    def test_the_section_is_a_hexagon_with_a_chamfer_that_keeps_its_share_of_the_width(self):
        for w in (0.2, 0.3, 0.48):
            sec = jp.rib_section(w, 3.0, 2.4)
            self.assertEqual(len(sec), 6)
            es = sorted({round(p[0], 9) for p in sec})
            self.assertAlmostEqual(es[-1] - es[0], w)
            ch = jp.RIB_CHAMFER_SHARE * w
            self.assertAlmostEqual(sec[2][1], 3.0 - ch)
            self.assertAlmostEqual(sec[3][0], -w / 2.0 + ch)
            self.assertAlmostEqual(sec[4][0], w / 2.0 - ch)

    def test_the_section_is_symmetric_about_its_middle(self):
        sec = jp.rib_section(0.4, 3.0, 2.4)
        for a, b in ((0, 1), (2, 5), (3, 4)):
            self.assertAlmostEqual(sec[a][0], -sec[b][0])
            self.assertAlmostEqual(sec[a][1], sec[b][1])

    def test_it_is_convex(self):
        sec = jp.rib_section(0.4, 3.0, 2.4)
        n = len(sec)
        signs = set()
        for i in range(n):
            a, b, c = sec[i], sec[(i + 1) % n], sec[(i + 2) % n]
            signs.add(math.copysign(1.0, (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0])))
        self.assertEqual(len(signs), 1)

    def test_u_runs_from_the_apex_end_at_the_kits_density_on_both_limbs(self):
        self.assertAlmostEqual(jp.rib_u(0.0, 3.4), jp.TD * 3.4)
        self.assertAlmostEqual(jp.rib_u(3.4, 3.4), 0.0)
        self.assertAlmostEqual(jp.rib_u(1.0, 3.4) - jp.rib_u(2.0, 3.4), jp.TD)


class RibFaceVTests(unittest.TestCase):
    """The V table of the six faces of a limb (face m runs from ring vertex m to m + 1): the two sides and the two
    chamfers of the section carry the same V at the same place, and the back and the front carry the same V on the same
    side, so the limb and its mirror (built from the same table) show the same texels at mirror points."""

    def v(self, m, k):
        return jp.RIB_FACE_V[m][k]

    def test_six_pairs_inside_the_strips(self):
        self.assertEqual(len(jp.RIB_FACE_V), 6)
        for m, (a, b) in enumerate(jp.RIB_FACE_V):
            strip = "S1" if m in (0, 3) else "S4"
            lo, hi = jp.STRIPS[strip]
            self.assertEqual(sorted((a, b)), [lo, hi], m)

    def test_the_two_sides_carry_the_same_v_at_the_back_and_at_the_front(self):
        self.assertEqual(self.v(1, 0), self.v(5, 1))  # the back corner of each side
        self.assertEqual(self.v(1, 1), self.v(5, 0))  # the front corner of each side

    def test_the_two_chamfers_carry_the_same_v_at_the_side_and_at_the_middle(self):
        self.assertEqual(self.v(2, 0), self.v(4, 1))  # the side end of each chamfer
        self.assertEqual(self.v(2, 1), self.v(4, 0))  # the middle end of each chamfer

    def test_back_and_front_carry_the_same_v_on_the_extrados_and_on_the_intrados(self):
        self.assertEqual(self.v(0, 0), self.v(3, 1))  # extrados: ring vertex 0 at the back, 4 at the front
        self.assertEqual(self.v(0, 1), self.v(3, 0))  # intrados: ring vertex 1 at the back, 3 at the front


class ArchFrameStepTests(unittest.TestCase):
    def test_the_fine_step_count_keeps_every_segment_of_an_arch_within_4_degrees(self):
        self.assertLessEqual(60.0 / jp.ARCH_STEPS_FINE, 4.0)
        outline = jp.pointed_arch(jp.ARCH_W, jp.ARCH_H, jp.ARCH_STEPS_FINE)
        turns = chord_turns(outline)
        # the apex is the one corner of the pointed arch (60 degrees by its shape); every other turn is a smooth step
        apex = max(range(len(turns)), key=lambda i: turns[i])
        self.assertAlmostEqual(turns[apex], 60.0 + 60.0 / jp.ARCH_STEPS_FINE, delta=0.5)
        rest = [t for i, t in enumerate(turns) if i != apex]
        self.assertLessEqual(max(rest), 4.0)

    def test_the_outline_is_a_mirror_about_the_axis(self):
        outline = jp.pointed_arch(jp.ARCH_W, jp.ARCH_H, jp.ARCH_STEPS_FINE)
        n = len(outline)
        for i in range(n):
            self.assertAlmostEqual(outline[i][0], -outline[n - 1 - i][0], places=12)
            self.assertAlmostEqual(outline[i][1], outline[n - 1 - i][1], places=12)

    def test_the_apex_is_one_point_on_the_axis_at_the_height_of_the_arch(self):
        outline = jp.pointed_arch(jp.ARCH_W, jp.ARCH_H, jp.ARCH_STEPS_FINE)
        top = max(outline, key=lambda p: p[1])
        self.assertAlmostEqual(top[0], 0.0, places=12)
        self.assertAlmostEqual(top[1], jp.ARCH_H, places=12)
        self.assertEqual(sum(1 for p in outline if abs(p[1] - jp.ARCH_H) < 1e-9), 1)


if __name__ == "__main__":
    unittest.main()
