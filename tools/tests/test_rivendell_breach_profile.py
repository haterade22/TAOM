#!/usr/bin/env python3
"""Unit tests for tools/oneoff/rivendell_breach_profile.py (pure Python, no Blender).

Run:  python -m unittest tools.tests.test_rivendell_breach_profile
"""
import importlib.util
import math
import os
import random
import sys
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(TOOLS, "oneoff"))  # the module imports rivendell_joint_profile
_spec = importlib.util.spec_from_file_location(
    "rivendell_breach_profile", os.path.join(TOOLS, "oneoff", "rivendell_breach_profile.py"))
bp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bp)

TD = 0.45
MORTAR = 0.0065   # a cut plane lies in the mortar line: within this of the joint centre


def area(poly):
    return 0.5 * sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1]
                     for i in range(len(poly)))


def tri_area3(a, b, c):
    u = [b[k] - a[k] for k in range(3)]
    w = [c[k] - a[k] for k in range(3)]
    n = (u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0])
    return 0.5 * math.sqrt(sum(c * c for c in n))


def tri_area2(a, b, c):
    return 0.5 * abs((b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1]))


class EdgeAtTests(unittest.TestCase):
    def test_edge_at_reads_a_stair(self):
        pts = [(3.0, 0.5), (3.0, 3.7), (4.0, 3.7), (4.0, 8.0), (6.0, 8.0), (6.0, 16.0)]
        self.assertEqual(bp.edge_at(pts, 1.0), 3.0)
        self.assertEqual(bp.edge_at(pts, 5.0), 4.0)
        self.assertEqual(bp.edge_at(pts, 12.0), 6.0)
        self.assertIsNone(bp.edge_at(pts, 0.2))

    def test_gap_polygon_is_a_closed_outline(self):
        left = [(3.0, 0.5), (3.0, 3.7), (6.0, 3.7), (6.0, 16.0)]
        right = [(3.2, 0.5), (3.2, 3.7), (5.8, 3.7), (5.8, 16.0)]
        poly = bp.gap_polygon(left, right)
        self.assertEqual(len(poly), 8)
        expected = (3.0 + 3.2) * 3.2 + (6.0 + 5.8) * 12.3
        self.assertAlmostEqual(abs(area(poly)), expected, places=6)

    def test_gap_polygon_rejects_sides_with_different_floors(self):
        with self.assertRaises(ValueError):
            bp.gap_polygon([(3.0, 0.5), (3.0, 16.0)], [(3.0, 0.6), (3.0, 16.0)])

    def test_gap_polygon_rejects_a_side_that_runs_down(self):
        with self.assertRaises(ValueError):
            bp.gap_polygon([(3.0, 0.5), (3.0, 3.0), (4.0, 2.0)], [(3.0, 0.5), (3.0, 3.0)])


class LayoutTests(unittest.TestCase):
    def test_the_bed_joints_alternate_a_tall_and_a_low_course(self):
        zs = bp.bed_joints(3.0, 15.0)
        gaps = [round(b - a, 3) for a, b in zip(zs, zs[1:])]
        self.assertEqual(set(gaps), {0.625, 0.486})
        self.assertEqual(gaps[0::2], gaps[0::2][:1] * len(gaps[0::2]))

    def test_the_courses_of_the_layout_are_625_486_625_486_mm(self):
        self.assertEqual([round(bp.course_height(c), 3) for c in range(4)], [0.625, 0.486, 0.625, 0.486])

    def test_a_height_lies_in_the_course_between_two_bed_joints(self):
        for z in (3.5, 6.1, 9.9, 12.0, 13.2):
            c = bp.course_at(z)
            below = max(j for j in bp.bed_joints(z - 1.0, z))
            above = min(j for j in bp.bed_joints(z, z + 1.0))
            self.assertAlmostEqual(above - below, bp.course_height(c), places=6, msg=z)

    def test_the_deck_lies_in_the_course_of_the_stone_that_the_parapet_stands_on(self):
        self.assertEqual(bp.course_at(12.0), 2)

    def test_head_joints_lie_on_the_block_edges_of_the_texture(self):
        for course in range(4):
            edges = bp.block_edges(course)
            for side in ("left", "right"):
                for e in bp.head_joints(course, side, 2.0, 9.0):
                    x = e if side == "right" else -e
                    u = (TD * (x - bp.X_LOW)) % 1.0
                    self.assertLess(min(abs(u - b) for b in edges + [e + 1 for e in edges]), 1e-9, (course, e))

    def test_head_joints_of_a_course_are_a_block_apart(self):
        for course in range(4):
            es = bp.head_joints(course, "right", 0.0, 12.0)
            gaps = [b - a for a, b in zip(es, es[1:])]
            self.assertTrue(all(0.79 <= g <= 1.41 for g in gaps), (course, gaps))

    def test_head_joints_respect_the_window(self):
        for e in bp.head_joints(1, "left", 4.0, 6.0):
            self.assertTrue(4.0 <= e <= 6.0)

    def test_block_sizes_are_the_blocks_of_the_texture(self):
        sizes = {(round(bp.block_dims(c, k)[0], 2)) for c in range(4) for k in (0, 1)}
        self.assertTrue(all(0.8 <= s <= 1.4 for s in sizes), sizes)
        a, b = bp.block_dims(0, 0)[0], bp.block_dims(0, 1)[0]
        self.assertAlmostEqual(a + b, 1.0 / TD, places=9)

    def test_a_block_rectangle_starts_at_its_own_corner(self):
        u0, v0 = bp.block_rect(0, 0)
        self.assertAlmostEqual(u0, 850 / 2048)
        self.assertAlmostEqual(v0 + bp.course_height(0) * TD, (1.0 - 0 / 2048 - bp.MORTAR_V) % 1.0 or 1.0, places=9)


class TearTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tear = bp.make_tear()
        cls.edges = [(skin, side, cls.tear["skins"][skin][side]) for skin in ("outer", "inner") for side in ("left", "right")]
        cls.joint_zs = [bp.step_z(z) for z in bp.bed_joints(2.9, 16.0)] + [
            bp.step_z(bp.jp.Z_PLINTH - j * bp.PLINTH_COURSE) for j in range(0, 4)] + [
            bp.step_z(bp.jp.PARAPET_BODY_TOP), bp.step_z(bp.jp.PARAPET_TOP)]

    def test_same_seed_same_tear(self):
        self.assertEqual(bp.make_tear(), bp.make_tear())

    def test_every_edge_runs_from_the_floor_to_the_top_and_never_down(self):
        for _, _, pts in self.edges:
            self.assertEqual(pts[0][1], bp.FLOOR)
            self.assertEqual(pts[-1][1], bp.TOP)
            zs = [z for _, z in pts]
            self.assertEqual(zs, sorted(zs))

    def test_no_edge_comes_nearer_the_centre_than_the_path_allows(self):
        for _, _, pts in self.edges:
            self.assertGreaterEqual(min(e for e, _ in pts), bp.E_MIN - 1e-9)
        self.assertGreaterEqual(bp.E_MIN, 2.4)

    def test_every_step_sits_on_a_bed_joint_or_a_plinth_joint(self):
        for skin, side, pts in self.edges:
            for z, _, _ in bp.steps_of(pts):
                self.assertLessEqual(min(abs(z - j) for j in self.joint_zs), 1e-9, (skin, side, z))

    def test_every_stone_step_ends_on_a_head_joint_of_its_course(self):
        deck_top = bp.bands()[bp.deck_band()][1]
        for skin, side, pts in self.edges:
            for z, e0, e1 in bp.steps_of(pts):
                if not (3.0 < z < deck_top - 0.01):
                    continue
                for e, zz in ((e0, z - 0.1), (e1, z + 0.1)):
                    if not (3.0 < zz < 14.0):
                        continue
                    c = bp.course_at(zz)
                    near = min(abs(e - j) for j in bp.head_joints(c, side, 2.0, 11.0))
                    self.assertLessEqual(near, MORTAR, (skin, side, z, e))

    def test_a_step_is_a_quarter_to_a_block_and_a_half_long(self):
        deck_top = bp.bands()[bp.deck_band()][1]
        for skin, side, pts in self.edges:
            for z, e0, e1 in bp.steps_of(pts):
                if abs(z - deck_top) < 0.01:
                    continue   # the first step of the parapet: it may stand up to PARAPET_SET behind the deck edge
                self.assertGreaterEqual(abs(e1 - e0), bp.STEP_MIN - 1e-6, (skin, side, z))
                self.assertLessEqual(abs(e1 - e0), bp.STEP_MAX + 1e-6, (skin, side, z))

    def test_the_edge_is_no_regular_stair(self):
        d = bp.deck_band()
        bs = bp.bands()
        for skin, side, pts in self.edges:
            below = [(z, e0, e1) for z, e0, e1 in bp.steps_of(pts) if z < bs[d][0] + 1e-6]
            out = [s for s in below if s[2] > s[1]]
            inward = [s for s in below if s[2] < s[1]]
            self.assertGreaterEqual(len(out), 3, (skin, side))
            self.assertGreaterEqual(len(inward), 3, (skin, side))
            sizes = [abs(e1 - e0) for _, e0, e1 in below]
            mean = sum(sizes) / len(sizes)
            self.assertGreater(math.sqrt(sum((s - mean) ** 2 for s in sizes) / len(sizes)), 0.15)

    def test_the_two_skins_do_not_break_alike(self):
        for side in ("left", "right"):
            a, b = self.tear["skins"]["outer"][side], self.tear["skins"]["inner"][side]
            diff = sum(abs(bp.edge_at(a, z) - bp.edge_at(b, z)) for z in [1.0 + 0.25 * i for i in range(40)]) / 40
            self.assertGreater(diff, 0.3)

    def test_the_edge_follows_the_trend_of_the_tear(self):
        for _, _, pts in self.edges:
            for z in (1.0, 3.5, 6.0, 9.0, 11.5):
                self.assertLessEqual(abs(bp.edge_at(pts, z) - bp.trend(z)), bp.TREND_BAND + 1e-6, z)

    def test_the_deck_gap_is_eleven_to_twelve_and_a_half(self):
        for y in (-3.0, -2.4, -1.0, -0.2, 0.0, 1.0, 1.2, 2.2, 2.3, 3.0, 4.0):
            left, right, width = bp.deck_gap(self.tear, y)
            self.assertGreaterEqual(width, 11.0, y)
            self.assertLessEqual(width, 12.5, y)
            self.assertGreaterEqual(10.0 - max(left, right), 3.3, y)

    def test_the_parapet_ends_behind_the_deck_edge_and_only_moves_out(self):
        d = bp.deck_band()
        for skin, side, pts in self.edges:
            above = [bp.edge_at(pts, 0.5 * (a + b)) for a, b, _ in bp.bands()[d + 1:-1]]
            self.assertEqual(above, sorted(above), (skin, side))
            deck = bp.edge_at(pts, 11.9)
            self.assertGreater(min(above), deck - 1e-9)
            if skin == "outer":
                self.assertGreaterEqual(min(above), bp.PARAPET_MIN - 1e-9)

    def test_one_side_of_the_outer_parapet_loses_the_panel_next_to_the_gap(self):
        side = self.tear["gone"]
        other = "right" if side == "left" else "left"
        z = 12.5
        self.assertGreaterEqual(bp.edge_at(self.tear["skins"]["outer"][side], z), bp.GONE_LO - 1e-9)
        self.assertLess(bp.edge_at(self.tear["skins"]["outer"][other], z), bp.GONE_LO)

    def test_cut_planes_miss_the_planes_of_the_wall(self):
        for _, _, pts in self.edges:
            for z, _, _ in bp.steps_of(pts):
                for plane in bp.AVOID_Z:
                    self.assertGreater(abs(z - plane), 0.002, (z, plane))
            for e, _ in pts:
                for plane in bp.AVOID_X:
                    self.assertGreater(abs(e - abs(plane)), 0.002, (e, plane))

    def test_every_band_is_a_course_or_a_plinth_course_or_the_cap(self):
        bs = bp.bands()
        self.assertEqual(bs[0][0], bp.FLOOR)
        self.assertEqual(bs[-1][1], bp.TOP)
        for (a, b, _), (c, d, _) in zip(bs, bs[1:]):
            self.assertEqual(b, c)
        stone = [b - a for a, b, course in bs if course is not None]
        self.assertTrue(all(h >= bp.BAND_MIN - 1e-9 for h in stone))
        self.assertTrue(all(h <= 0.68 for h in stone))


class CoreTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tear = bp.make_tear()
        cls.grid = bp.core_grid(cls.tear)

    def test_the_core_lies_behind_every_tooth(self):
        for side in ("left", "right"):
            for z in [bp.FLOOR + 0.05 * k for k in range(0, 230)]:
                skin = max(bp.edge_at(self.tear["skins"][s][side], z) for s in ("outer", "inner"))
                for y in (-1.9, -0.7, 0.0, 0.9, 1.9):
                    self.assertGreaterEqual(bp.core_edge(self.tear, side, y, z), skin + bp.SET_MIN - 1e-9, (side, z, y))

    def test_the_core_sits_a_tenth_to_over_half_a_metre_behind_the_teeth_in_most_places(self):
        sets = []
        for side in ("left", "right"):
            for z in [bp.FLOOR + 0.1 * k for k in range(0, 115)]:
                skin = max(bp.edge_at(self.tear["skins"][s][side], z) for s in ("outer", "inner"))
                for y in (-1.5, 0.0, 1.5):
                    sets.append(bp.core_edge(self.tear, side, y, z) - skin)
        self.assertGreaterEqual(min(sets), bp.SET_MIN - 1e-9)
        within = sum(1 for s in sets if s <= bp.SET_MIN + bp.SET_RANGE + bp.ROUGH + 1e-9) / len(sets)
        self.assertGreaterEqual(within, 0.6)
        self.assertGreater(sorted(sets)[len(sets) // 2], 0.15)

    def test_the_core_leans_back_toward_the_standing_wall(self):
        for side in ("left", "right"):
            low = sum(bp.core_edge(self.tear, side, y, 1.5) for y in (-1.5, 0, 1.5)) / 3
            high = sum(bp.core_edge(self.tear, side, y, 11.0) for y in (-1.5, 0, 1.5)) / 3
            self.assertGreater(high, low + 0.8)

    def test_the_grid_is_eleven_to_thirteen_metres_tall_and_closed_at_both_ends(self):
        g = self.grid
        self.assertLess(g["zs"][0], bp.FLOOR)
        self.assertEqual(g["zs"][-1], bp.CORE_TOP)
        self.assertEqual(len(g["xl"]), len(g["zs"]))
        self.assertTrue(all(len(r) == bp.CORE_COLS for r in g["xl"] + g["xr"] + g["ys"]))

    def test_the_back_of_a_skin_is_no_single_plane(self):
        ys = {round(row[-1], 4) for row in self.grid["ys"]}
        self.assertGreater(len(ys), len(self.grid["zs"]) // 2)
        for row in self.grid["ys"]:
            self.assertTrue(1.9 <= row[-1] <= 2.0 and -2.0 <= row[0] <= -1.9)

    def test_the_lumpy_face_has_no_flat_plane_over_half_a_square_metre(self):
        for side in ("left", "right"):
            tris = bp.surface_triangles(self.grid, side)
            self.assertLess(bp.flat_patch_area(tris), 0.5, side)

    def test_the_flat_patch_finder_finds_a_plane(self):
        a, b, c, d = (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)
        self.assertAlmostEqual(bp.flat_patch_area([(a, b, c), (a, c, d)]), 1.0)
        tilted = (a, b, (1, 1, 1))   # 45 degrees off the first: a plane of its own, the larger of the two
        self.assertAlmostEqual(bp.flat_patch_area([(a, b, c), tilted]), 0.5 * math.sqrt(2.0), places=6)

    def test_the_flat_patch_finder_says_where_the_plane_is(self):
        a, b, c, d = (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)
        e, f = (3, 0, 5), (4, 0, 5)
        area_, centre, normal = bp.largest_flat_patch([(a, b, c), (a, c, d), (e, f, (3, 1, 5))])
        self.assertAlmostEqual(area_, 1.0)
        self.assertAlmostEqual(centre[0], 0.5)
        self.assertAlmostEqual(centre[2], 0.0)
        self.assertAlmostEqual(abs(normal[2]), 1.0)
        self.assertEqual(bp.largest_flat_patch([]), (0.0, None, None))

    def test_the_lumps_are_a_smooth_noise_between_zero_and_one(self):
        vals = [bp.lump(y * 0.07, z * 0.07) for y in range(-30, 30) for z in range(0, 170)]
        self.assertGreaterEqual(min(vals), 0.0)
        self.assertLessEqual(max(vals), 1.0)
        self.assertGreater(max(vals) - min(vals), 0.6)
        for y, z in ((0.3, 2.0), (-1.2, 7.7)):
            self.assertLess(abs(bp.lump(y + 0.02, z) - bp.lump(y, z)), 0.1)
        self.assertEqual(bp.lump(0.4, 3.0), bp.lump(0.4, 3.0))

    def test_the_gap_at_the_deck_follows_the_core_in_the_middle(self):
        widths = [bp.deck_gap(self.tear, y)[2] for y in (-1.5, 0.0, 1.5)]
        self.assertTrue(all(11.0 <= w <= 12.5 for w in widths), widths)


class ClipTests(unittest.TestCase):
    def test_clip_keeps_a_rectangle_slice(self):
        rect = [(0.0, 0.0), (1.0, 0.0), (1.0, 4.0), (0.0, 4.0)]
        out = bp.clip_polygon_z(rect, 1.0, 3.0)
        self.assertAlmostEqual(area(out), 2.0)

    def test_clip_never_repeats_a_vertex_on_the_plane(self):
        poly = [(0.0, -1.0), (1.0, -1.0), (1.0, 3.0), (0.5, 3.0), (0.5, 8.0), (0.0, 8.0)]
        out = bp.clip_polygon_z(poly, 0.0, 3.0)
        for i, p in enumerate(out):
            self.assertNotEqual(p, out[(i + 1) % len(out)])
        self.assertAlmostEqual(abs(area(out)), 3.0)


class HeapTests(unittest.TestCase):
    def setUp(self):
        self.heap = bp.Heap()

    def test_crest_in_the_gap_is_four_to_five(self):
        crest = max(self.heap.z.values())
        self.assertTrue(4.0 <= crest <= 5.0, crest)

    def test_reach_past_the_wall_face_is_six_and_a_half_to_seven_and_a_half(self):
        reach = max(abs(self.heap.ys[j]) for (i, j), z in self.heap.nodes() if z > 0.0) - 2.5
        self.assertTrue(6.5 <= reach <= 7.5, reach)

    def test_walk_path_is_under_32_degrees(self):
        stats = bp.path_stats(self.heap.at)
        self.assertLess(stats["steepest_deg"], 32.0)
        self.assertEqual(stats["path_half_width"], 1.5)

    def test_the_four_metre_path_is_under_30_degrees_on_the_bare_heap(self):
        stats = bp.path_stats(self.heap.at, half=2.0)
        self.assertLess(stats["steepest_deg"], 30.0)

    def test_the_bare_heap_has_no_step_on_the_path(self):
        self.assertLess(bp.step_stats(self.heap.at)["max_step"], 0.32)

    def test_step_stats_reads_a_known_step(self):
        out = bp.step_stats(lambda x, y: 0.4 if y > 1.0 else 0.0, half=1.0, reach=3.0)
        self.assertAlmostEqual(out["max_step"], 0.4)

    def test_toe_outline_is_irregular(self):
        reach = []
        for i, x in enumerate(self.heap.xs):
            if abs(x) < 2.5:
                continue
            ys = [self.heap.ys[j] for j in range(len(self.heap.ys)) if self.heap.z[(i, j)] > 0.05 and self.heap.ys[j] > 0]
            if ys:
                reach.append(max(ys))
        self.assertGreater(max(reach) - min(reach), 0.8)

    def test_the_field_runs_on_below_the_ground_to_a_floor(self):
        values = list(self.heap.z.values())
        self.assertAlmostEqual(min(values), bp.HEAP_FLOOR)
        self.assertGreater(sum(1 for v in values if v < 0.0), 100)

    def test_the_mound_is_lumpy_outside_the_path_and_smooth_on_it(self):
        h = self.heap
        base = lambda i, j: bp.base_height(h.xs[i], h.ys[j])
        on_path = max(abs(h.z[(i, j)] - base(i, j)) for i, x in enumerate(h.xs) for j, y in enumerate(h.ys)
                      if abs(x) <= 2.0 and abs(y) < 8.0)
        off_path = max(abs(h.z[(i, j)] - base(i, j)) for i, x in enumerate(h.xs) for j, y in enumerate(h.ys)
                       if abs(x) >= 3.5 and abs(y) < 6.0 and base(i, j) > 0.8)
        self.assertLess(on_path, 0.06)
        self.assertGreater(off_path, 0.3)

    def test_no_bump_reaches_the_clear_path(self):
        for cx, cy, r, h in bp.make_bumps(random.Random(11)):
            self.assertGreaterEqual(abs(cx) - r, 2.1 - 1e-9)

    def test_same_seed_same_field(self):
        self.assertEqual(bp.Heap().z, bp.Heap().z)

    def test_at_is_bilinear_between_nodes(self):
        h = self.heap
        i, j = len(h.xs) // 2, len(h.ys) // 2
        mid = h.at(0.5 * (h.xs[i] + h.xs[i + 1]), h.ys[j])
        self.assertAlmostEqual(mid, 0.5 * (h.z[(i, j)] + h.z[(i + 1, j)]))


class SlopeTests(unittest.TestCase):
    def test_flat_ground_is_zero_degrees(self):
        self.assertEqual(bp.path_stats(lambda x, y: 1.0)["steepest_deg"], 0.0)

    def test_a_known_ramp_reads_its_angle(self):
        stats = bp.path_stats(lambda x, y: math.tan(math.radians(20.0)) * y)
        self.assertAlmostEqual(stats["steepest_deg"], 20.0, places=4)


class EarClipTests(unittest.TestCase):
    def check(self, pts):
        tris = bp.ear_clip(pts)
        self.assertEqual(len(tris), len(pts) - 2)
        total = sum(0.5 * ((pts[b][0] - pts[a][0]) * (pts[c][1] - pts[a][1]) - (pts[c][0] - pts[a][0]) * (pts[b][1] - pts[a][1]))
                    for a, b, c in tris)
        self.assertAlmostEqual(total, abs(area(pts)), places=9)

    def test_a_square_with_collinear_points_gives_no_zero_area_triangle(self):
        self.check([(0.0, 0.0), (0.5, 0.0), (1.0, 0.0), (1.5, 0.0), (2.0, 0.0), (2.0, 0.5), (2.0, 1.0),
                    (1.0, 1.0), (0.0, 1.0), (0.0, 0.5)])

    def test_a_dented_outline(self):
        self.check([(0.0, 0.0), (4.0, 0.0), (4.0, 3.0), (3.0, 3.0), (3.0, 1.0), (1.0, 1.0), (1.0, 3.0), (0.0, 3.0)])


class AreaUvTests(unittest.TestCase):
    def density(self, pts):
        uv = bp.area_uv(pts)
        return math.sqrt(tri_area2(*uv) / tri_area3(*pts))

    def test_a_flat_face_has_the_exact_density(self):
        self.assertAlmostEqual(self.density([(0, 0, 0), (1, 0, 0), (0, 1, 0)]), TD, places=9)
        self.assertAlmostEqual(self.density([(0, 0, 0), (1, 0, 0), (0, 0, 2)]), TD, places=9)

    def test_a_leaning_face_has_the_exact_density(self):
        rng = random.Random(1)
        for _ in range(200):
            pts = [(rng.uniform(-3, 3), rng.uniform(-3, 3), rng.uniform(0, 4)) for _ in range(3)]
            if tri_area3(*pts) < 1e-3:
                continue
            self.assertAlmostEqual(self.density(pts), TD, places=9)

    def test_a_polygon_of_four_corners_keeps_the_density(self):
        pts = [(0, 0, 0), (2, 0, 0.4), (2, 1.5, 0.4), (0, 1.5, 0)]
        uv = bp.area_uv(pts)
        a_uv = 0.5 * abs(sum(p[0] * q[1] - q[0] * p[1] for p, q in zip(uv, uv[1:] + uv[:1])))
        a_xyz = bp.polygon_normal_area(pts)[1]
        self.assertAlmostEqual(math.sqrt(a_uv / a_xyz), TD, places=9)

    def test_neighbouring_faces_of_a_slope_differ_by_millimetres_of_texture(self):
        a = bp.area_uv([(0, 0, 0), (0.4, 0, 0.1), (0, 0.4, 0.2)])
        b = bp.area_uv([(0.4, 0, 0.1), (0.4, 0.4, 0.3), (0, 0.4, 0.2)])
        shared = [(a[1], b[0]), (a[2], b[2])]
        for p, q in shared:
            self.assertLess(math.dist(p, q), 0.01)

    def test_a_face_of_no_area_gives_zero_uv(self):
        self.assertEqual(bp.area_uv([(0, 0, 0), (1, 0, 0), (2, 0, 0)]), [(0.0, 0.0)] * 3)

    def test_axes_and_dominant_axis(self):
        self.assertEqual(bp.plane_coords(2, (1.0, 2.0, 3.0)), (1.0, 2.0))
        self.assertEqual(bp.plane_coords(0, (1.0, 2.0, 3.0)), (2.0, 3.0))
        self.assertEqual(bp.plane_coords(1, (1.0, 2.0, 3.0)), (1.0, 3.0))
        self.assertEqual(bp.dominant_axis((0.1, 0.2, 0.97)), 2)
        self.assertEqual(bp.dominant_axis((-0.9, 0.1, 0.2)), 0)


class StoneTests(unittest.TestCase):
    def test_an_angular_stone_has_corners_inside_its_size(self):
        rng = random.Random(2)
        for _ in range(50):
            pts = bp.stone_points(rng, ((0.3, 0.9), (0.25, 0.8), (0.2, 0.6)))
            self.assertEqual(len(pts), 11)
            ext = [max(p[k] for p in pts) - min(p[k] for p in pts) for k in range(3)]
            self.assertTrue(0.2 <= ext[0] <= 1.1 and 0.15 <= ext[1] <= 1.0 and 0.1 <= ext[2] <= 0.8, ext)

    def test_the_same_draw_gives_the_same_stone(self):
        self.assertEqual(bp.stone_points(random.Random(5), ((0.3, 0.9),) * 3), bp.stone_points(random.Random(5), ((0.3, 0.9),) * 3))


class RockTests(unittest.TestCase):
    def test_a_rock_is_a_cloud_of_points_on_a_lumpy_ellipsoid(self):
        rng = random.Random(4)
        for _ in range(50):
            pts = bp.stone_points(rng, ((0.3, 0.9), (0.25, 0.8), (0.2, 0.6)), style="rock")
            self.assertEqual(len(pts), bp.ROCK_POINTS)
            ext = [max(p[k] for p in pts) - min(p[k] for p in pts) for k in range(3)]
            self.assertTrue(0.15 <= ext[0] <= 1.0 and 0.12 <= ext[1] <= 0.9 and 0.08 <= ext[2] <= 0.7, ext)

    def test_a_rock_is_less_boxy_than_a_cut_box(self):
        # the share of the bounding box that the points fill at the corners: a cut box has corner points, a rock has few
        def corner_share(pts):
            lo = [min(p[k] for p in pts) for k in range(3)]
            hi = [max(p[k] for p in pts) for k in range(3)]
            n = 0
            for p in pts:
                if all(min(p[k] - lo[k], hi[k] - p[k]) < 0.18 * (hi[k] - lo[k]) for k in range(3)):
                    n += 1
            return n / len(pts)
        rng = random.Random(8)
        box = [corner_share(bp.stone_points(rng, ((0.4, 0.9),) * 3)) for _ in range(80)]
        rock = [corner_share(bp.stone_points(rng, ((0.4, 0.9),) * 3, style="rock")) for _ in range(80)]
        self.assertLess(sum(rock) / len(rock), 0.6 * sum(box) / len(box))


class ChipTests(unittest.TestCase):
    def setUp(self):
        self.tear = bp.make_tear()
        self.pts = self.tear["skins"]["outer"]["right"]

    def test_chips_keep_the_ends_and_never_run_down(self):
        out = bp.chip_edge(self.pts, random.Random(1))
        self.assertEqual(out[0], self.pts[0])
        self.assertEqual(out[-1], self.pts[-1])
        zs = [z for _, z in out]
        self.assertEqual(zs, sorted(zs))
        bp.gap_polygon(out, out)

    def test_chips_cut_the_corners_of_the_teeth_by_a_few_centimetres(self):
        out = bp.chip_edge(self.pts, random.Random(2), share=1.0)
        added = len(out) - len(self.pts)
        self.assertGreaterEqual(added, len(bp.steps_of(self.pts)) // 2)
        self.assertLessEqual(added, len(bp.steps_of(self.pts)))
        for z in [0.7 + 0.1 * k for k in range(100)]:
            a, b = bp.edge_at(self.pts, z), bp.edge_at(out, z)
            self.assertGreaterEqual(b, a - 1e-9)       # a chip only takes wall away
            self.assertLessEqual(b - a, bp.CHIP_MAX + 1e-9)

    def test_a_share_of_zero_chips_nothing(self):
        self.assertEqual(bp.chip_edge(self.pts, random.Random(3), share=0.0), self.pts)

    def test_the_same_draw_gives_the_same_chips(self):
        self.assertEqual(bp.chip_edge(self.pts, random.Random(5)), bp.chip_edge(self.pts, random.Random(5)))


class DustTests(unittest.TestCase):
    def test_a_torn_face_is_darker(self):
        base = (0.95, 0.95, 0.9)
        out = bp.dust_tone(base, 0.0)
        for a, b in zip(out, base):
            self.assertLess(a, b)
        self.assertGreater(out[0], out[2])

    def test_the_dust_fades_out_with_the_distance(self):
        base = (0.95, 0.95, 0.9)
        values = [bp.dust_tone(base, d)[1] for d in (0.0, 0.2, 0.4, 0.6, 0.8)]
        self.assertEqual(values, sorted(values))
        self.assertEqual(bp.dust_tone(base, bp.DUST_REACH), base)

    def test_heap_tone_is_a_mottle_between_88_and_100_percent(self):
        values = [bp.heap_tone(x * 0.37, y * 0.41, 0.0) for x in range(-20, 20) for y in range(-25, 25)]
        self.assertGreaterEqual(min(values), 0.88 - 1e-9)
        self.assertLessEqual(max(values), 1.0 + 1e-9)


class PlacementTests(unittest.TestCase):
    def test_rest_offset_sits_the_lowest_point_just_in_the_ground(self):
        pts = [(0.0, 0.0, 0.0), (1.0, 0.0, 0.3), (0.0, 1.0, 0.1)]
        dz = bp.rest_offset(pts, lambda x, y: 2.0, sink=0.05, floor_z=-0.28)
        self.assertAlmostEqual(min(p[2] + dz for p in pts), 2.0 - 0.05)

    def test_rest_offset_never_goes_below_the_floor(self):
        pts = [(0.0, 0.0, 0.0), (1.0, 0.0, 0.2)]
        dz = bp.rest_offset(pts, lambda x, y: -1.0, sink=0.05, floor_z=-0.28)
        self.assertGreaterEqual(min(p[2] + dz for p in pts), -0.28 - 1e-9)

    def test_outside_path(self):
        self.assertTrue(bp.outside_path(3.0, 0.6))
        self.assertFalse(bp.outside_path(2.5, 0.6))
        self.assertTrue(bp.outside_path(-2.6, 0.3))


if __name__ == "__main__":
    unittest.main()
