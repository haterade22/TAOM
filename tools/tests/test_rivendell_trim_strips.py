#!/usr/bin/env python3
"""Unit tests for tools/oneoff/rivendell_trim_strips.py (pure Python, no Blender).

Run:  python -m unittest tools.tests.test_rivendell_trim_strips
"""
import importlib.util
import os
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location(
    "rivendell_trim_strips", os.path.join(TOOLS, "oneoff", "rivendell_trim_strips.py"))
rts = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(rts)

NAMES = ("S1", "S2", "S3", "S4", "S5")


def samples(name, nu=61, nv=47):
    """A grid of (u, s) over one tile and the whole strip, with the strip's own edges included."""
    h = rts.strip_height_m(name)
    return [(i / (nu - 1) * 1.0, j / (nv - 1) * h) for i in range(nu) for j in range(nv)]


class RowsTest(unittest.TestCase):
    def test_strips_come_from_the_joint_profile(self):
        self.assertEqual(tuple(rts.STRIPS), NAMES)
        self.assertEqual(rts.STRIPS["S1"], (0.0, 0.28))
        self.assertEqual(rts.STRIPS["S5"], (0.805, 1.0))

    def test_rows_cover_the_sheet(self):
        rows = rts.strip_rows()
        spans = sorted(rows.values())
        self.assertEqual(spans[0][0], 0)
        self.assertEqual(spans[-1][1], rts.SIZE)
        for (t0, b0), (t1, b1) in zip(spans, spans[1:]):
            self.assertEqual(b0, t1)
        for name, (t, b) in rows.items():
            self.assertLess(t, b)
            v0, v1 = rts.STRIPS[name]
            self.assertAlmostEqual((b - t) / rts.SIZE, v1 - v0, delta=1.0 / rts.SIZE)

    def test_s5_is_the_top_strip(self):
        rows = rts.strip_rows()
        self.assertEqual(rows["S5"][0], 0)
        self.assertEqual(rows["S1"][1], rts.SIZE)

    def test_strip_bounds_are_in_metres_on_the_sheet(self):
        for name, (v0, v1) in rts.STRIPS.items():
            y0, y1 = rts.strip_bounds_m(name)
            self.assertAlmostEqual(y0, v0 * rts.TILE_M)
            self.assertAlmostEqual(y1, v1 * rts.TILE_M)
            self.assertAlmostEqual(rts.strip_height_m(name), y1 - y0)

    def test_margin_is_eight_pixels(self):
        self.assertEqual(rts.MARGIN_PX, 8)
        self.assertAlmostEqual(rts.MARGIN, 8 * rts.TILE_M / rts.SIZE)


class HeightTest(unittest.TestCase):
    def test_every_function_is_periodic_in_u(self):
        for name in NAMES:
            for u, s in samples(name, 23, 29):
                h = rts.height(name, u, s)
                self.assertAlmostEqual(rts.height(name, u + 1.0, s), h, places=12, msg=(name, u, s))
                self.assertAlmostEqual(rts.height(name, u - 1.0, s), h, places=12, msg=(name, u, s))

    def test_the_tile_seam_is_continuous(self):
        for name in NAMES:
            for s in (0.1, 0.17, 0.23, 0.31):
                if s >= rts.strip_height_m(name):
                    continue
                a = rts.height(name, 1e-9, s)
                b = rts.height(name, 1.0 - 1e-9, s)
                self.assertAlmostEqual(a, b, delta=2e-5, msg=(name, s))

    def test_margins_are_flat_at_the_base_level(self):
        for name in NAMES:
            top = rts.strip_height_m(name)
            for i in range(40):
                u = i / 40.0
                for k in range(9):
                    s = rts.MARGIN * k / 8.0
                    self.assertEqual(rts.height(name, u, s), rts.Z_BASE, msg=(name, u, s))
                    self.assertEqual(rts.height(name, u, top - s), rts.Z_BASE, msg=(name, u, s))

    def test_heights_stay_inside_the_limit(self):
        for name in NAMES:
            lo, hi = min(rts.height(name, u, s) for u, s in samples(name, 120, 120)), None
            hi = max(rts.height(name, u, s) for u, s in samples(name, 120, 120))
            self.assertGreater(lo, 0.0002, msg=name)
            self.assertLessEqual(hi, rts.Z_LIMIT, msg=name)

    def test_the_lowest_face_is_the_base_and_nothing_is_lower_but_a_joint(self):
        for name in NAMES:
            vals = [rts.height(name, u, s) for u, s in samples(name, 120, 120)]
            floor = min(vals)
            if name in ("S1", "S2", "S5"):
                self.assertGreaterEqual(rts.Z_BASE - floor, rts.JOINT_DEPTH * 0.9)
            elif name == "S4":
                self.assertGreaterEqual(rts.Z_BASE - floor, rts.S4_JOINT_DEPTH * 0.9)
            else:
                self.assertAlmostEqual(floor, rts.Z_BASE, places=9, msg=name)  # a bead joint stops at the field

    def test_relief_is_shallow_but_present(self):
        floor = {"S1": 0.0010, "S2": 0.012, "S3": 0.008, "S4": 0.010, "S5": 0.010}
        for name in NAMES:
            vals = [rts.height(name, u, s) for u, s in samples(name, 120, 120)]
            self.assertGreaterEqual(max(vals) - rts.Z_BASE, floor[name], msg=name)

    def test_the_base_level_is_above_the_plane(self):
        self.assertGreater(rts.Z_BASE, 0.0)
        self.assertGreater(rts.Z_BASE - rts.JOINT_DEPTH, 0.0)
        self.assertGreater(rts.Z_BASE - rts.S4_JOINT_DEPTH, 0.0)
        self.assertLessEqual(rts.Z_BASE + 0.0185, rts.Z_LIMIT + 1e-12)  # the S2 bead still fits


def depth_profile(name, s, j=0.0, ref=None, ref_du=0.1, span=0.03, n=3001):
    """(offsets in metres from the joint at U = j, depth below the face along U at height s). The face is `ref`,
    or the height ref_du of a tile away from the joint."""
    xs = [(-span + 2.0 * span * i / (n - 1)) for i in range(n)]
    face = rts.height(name, j + ref_du, s) if ref is None else ref
    return xs, [face - rts.height(name, j + x / rts.TILE_M, s) for x in xs]


def width_at_half(xs, ds):
    top = max(ds)
    inside = [x for x, d in zip(xs, ds) if d >= top / 2.0]
    return top, max(inside) - min(inside)


class JointTest(unittest.TestCase):
    def test_joint_positions(self):
        for name in ("S1", "S2", "S3", "S5"):
            self.assertEqual(rts.joint_positions(name), (0.0, 0.5), msg=name)
        self.assertEqual(rts.joint_positions("S4"), tuple(k / 6.0 for k in range(6)))

    def test_the_wide_joint_has_the_wall_floor_and_round_and_is_five_to_six_mm_deep(self):
        # the wall stone_d: a 13 mm floor, a 13 mm round-over (12 px at 2048), joint.json of 2026-11-25
        self.assertGreaterEqual(rts.JOINT_FLOOR, 0.012)
        self.assertLessEqual(rts.JOINT_FLOOR, 0.0135)
        self.assertGreaterEqual(rts.JOINT_ROUND, 0.012)
        self.assertLessEqual(rts.JOINT_ROUND, 0.0135)
        self.assertGreaterEqual(rts.JOINT_DEPTH, 0.005)
        self.assertLessEqual(rts.JOINT_DEPTH, 0.006)
        s = rts.strip_height_m("S1") / 2.0
        for j in (0.0, 0.5):
            xs, ds = depth_profile("S1", s, j, ref=rts.Z_BASE)
            top, _ = width_at_half(xs, ds)
            self.assertAlmostEqual(top, rts.JOINT_DEPTH, delta=0.0003, msg=j)
            flat = [x for x, d in zip(xs, ds) if d >= top * 0.985]  # the S1 cushion lifts the floor by 0.05 mm
            self.assertAlmostEqual(max(flat) - min(flat), rts.JOINT_FLOOR, delta=0.0008, msg=j)

    def test_the_joint_is_a_round_over_on_both_sides_like_the_wall(self):
        """The wall's height next to a joint is sqrt(1 - (1 - t)^2) of the depth above the floor, t from 0 at
        the edge of the floor to 1 at the end of the round (layout_height.png of stone_d, read 2026-11-25)."""
        s = rts.strip_height_m("S1") / 2.0
        xs, ds = depth_profile("S1", s, ref=rts.Z_BASE, n=6001)
        edge = rts.JOINT_FLOOR / 2.0
        for sign in (1, -1):
            for t in (0.1, 0.25, 0.5, 0.75, 0.9):
                x = sign * (edge + t * rts.JOINT_ROUND)
                got = min(zip(xs, ds), key=lambda p: abs(p[0] - x))[1]
                want = rts.JOINT_DEPTH * (1.0 - (1.0 - (1.0 - t) ** 2) ** 0.5)
                self.assertAlmostEqual(got, want, delta=0.0003, msg=(sign, t))
        for x, d in list(zip(xs, ds))[::50]:
            mirror = min(zip(xs, ds), key=lambda p: abs(p[0] + x))[1]
            self.assertAlmostEqual(d, mirror, delta=2e-4)

    def test_the_wide_joint_has_the_same_width_on_every_strip_that_carries_it(self):
        want = rts.JOINT_FLOOR + 2.0 * rts.JOINT_ROUND * (1.0 - 0.75 ** 0.5)  # the width at half depth of the round
        cases = (("S1", rts.strip_height_m("S1") * 0.5, 0.1), ("S2", rts.strip_height_m("S2") * 0.45, 0.1),
                 ("S5", rts.S5_SILL - 0.010, 0.030 / rts.TILE_M))  # below the shaft, 30 mm off the joint
        for name, s, ref_du in cases:
            xs, ds = depth_profile(name, s, 0.5, ref=None if name != "S1" else rts.Z_BASE, ref_du=ref_du, span=0.029, n=3001)
            top, width = width_at_half(xs, ds)
            self.assertAlmostEqual(width, want, delta=0.0008, msg=name)
            self.assertGreater(top, 0.004, msg=name)

    def test_s1_joints_are_grooves_and_the_stone_is_a_slight_cushion(self):
        s = rts.strip_height_m("S1") / 2.0
        centre = rts.height("S1", 0.25, s)
        for j in (0.0, 0.5):
            self.assertLess(rts.height("S1", j, s), rts.Z_BASE - 0.005)
        self.assertGreater(centre, rts.Z_BASE + 0.0008)
        self.assertLessEqual(centre, rts.Z_BASE + 0.0021)

    def test_s2_joints_cut_the_moulding(self):
        s = rts.strip_height_m("S2") * 0.45
        for j in (0.0, 0.5):
            self.assertLess(rts.height("S2", j, s), rts.height("S2", j + 0.05, s) - 0.004)

    def test_s5_has_joints_in_the_piers(self):
        s = rts.S5_SILL + 0.03
        for j in (0.0, 0.5):
            self.assertLess(rts.height("S5", j, s), rts.height("S5", j + 0.006, s) - 0.0015)

    def test_joints_stop_inside_the_margin(self):
        for name in NAMES:
            for j in (0.0, 0.5):
                self.assertEqual(rts.height(name, j, rts.MARGIN), rts.Z_BASE, msg=name)

    def test_s4_has_six_real_joints_eight_to_nine_mm_wide_with_a_small_round(self):
        self.assertEqual(rts.S4_JOINTS, 6)
        self.assertGreaterEqual(rts.S4_JOINT_ROUND, 0.002)
        self.assertLessEqual(rts.S4_JOINT_ROUND, 0.004)
        s = rts.strip_height_m("S4") * 0.5
        self.assertEqual(len(rts.joint_positions("S4")), 6)
        for k in range(6):
            j = k / 6.0
            self.assertLess(rts.height("S4", j, s), rts.height("S4", j + 1.0 / 12.0, s) - 0.003, msg=k)
        xs, ds = depth_profile("S4", s, 1.0 / 6.0, ref_du=1.0 / 12.0, span=0.02, n=4001)
        top, width = width_at_half(xs, ds)
        self.assertGreaterEqual(width, 0.008)
        self.assertLessEqual(width, 0.009)
        self.assertAlmostEqual(top, rts.S4_JOINT_DEPTH, delta=0.0003)
        flat = [x for x, d in zip(xs, ds) if d >= top * 0.999]
        self.assertAlmostEqual(max(flat) - min(flat), rts.S4_JOINT_FLOOR, delta=0.0006)

    def test_s4_is_the_same_between_two_joints(self):
        s = rts.strip_height_m("S4") * 0.5
        vals = {round(rts.height("S4", 1.0 / 12.0 + u * 0.0001, s), 12) for u in range(-30, 30)}
        self.assertEqual(len(vals), 1)

    def test_s3_has_no_joint_through_the_interlace_but_one_in_each_bead(self):
        top = rts.strip_height_m("S3")
        for j in (0.0, 0.5):
            for frac in (0.40, 0.5, 0.58):
                s = top * frac
                self.assertAlmostEqual(rts.height("S3", j, s), rts.height("S3", j + 0.25, s), places=12)
            for s in (rts.MARGIN + rts.S3_BEAD_CENTRE, top - rts.MARGIN - rts.S3_BEAD_CENTRE):
                self.assertLess(rts.height("S3", j, s), rts.height("S3", j + 0.25, s) - 0.004, msg=(j, s))

    def test_joint_weight_marks_the_joints_for_the_texture(self):
        mid = {n: rts.strip_height_m(n) * 0.5 for n in NAMES}
        mid["S2"] = rts.strip_height_m("S2") * 0.45
        for name in ("S1", "S2", "S5"):
            for j in (0.0, 0.5):
                self.assertAlmostEqual(rts.joint_weight(name, j, mid[name]), 1.0, places=6, msg=name)
            self.assertEqual(rts.joint_weight(name, 0.25, mid[name]), 0.0, msg=name)
        self.assertAlmostEqual(rts.joint_weight("S4", 1.0 / 6.0, mid["S4"]), 1.0, places=6)
        self.assertEqual(rts.joint_weight("S4", 1.0 / 12.0, mid["S4"]), 0.0)
        self.assertEqual(rts.joint_weight("S3", 0.0, mid["S3"]), 0.0)
        self.assertAlmostEqual(rts.joint_weight("S3", 0.0, rts.MARGIN + rts.S3_BEAD_CENTRE), 1.0, places=6)
        self.assertEqual(rts.joint_weight("S1", 0.0, 0.0), 0.0)
        for name in NAMES:
            for u, s in samples(name, 31, 29):
                self.assertTrue(0.0 <= rts.joint_weight(name, u, s) <= 1.0, msg=(name, u, s))


class CarvedBandTest(unittest.TestCase):
    def test_s3_period_count_is_two_or_four(self):
        self.assertIn(rts.S3_PERIODS, (2, 4))

    def test_s3_repeats_every_period_and_not_more_often(self):
        s = rts.strip_height_m("S3") * 0.5 + 0.03
        p = 1.0 / rts.S3_PERIODS
        diffs = [abs(rts.height("S3", u / 50.0 * p, s) - rts.height("S3", u / 50.0 * p + p, s)) for u in range(50)]
        self.assertLess(max(diffs), 1e-9)
        half = [abs(rts.height("S3", u / 50.0 * p, s) - rts.height("S3", u / 50.0 * p + p / 2, s))
                for u in range(50)]
        self.assertGreater(max(half), 1e-3)

    def test_s3_strands_go_over_and_under(self):
        # along each strand the height at the strand centre swings between an over and an under level
        for k in range(3):
            tops = []
            for i in range(200):
                u = i / 200.0 / rts.S3_PERIODS
                tops.append(rts.strand_top(k, u))
            self.assertGreater(max(tops) - min(tops), 0.004)
            self.assertLessEqual(max(tops), 0.013)
            self.assertGreaterEqual(min(tops), 0.004)

    def test_s3_every_crossing_has_one_strand_over_the_other(self):
        n = 4000
        found = 0
        for i in range(n):
            u = i / n / rts.S3_PERIODS
            for a, b in ((0, 1), (1, 2), (2, 0)):
                u2 = (i + 1) / n / rts.S3_PERIODS
                d1 = rts.strand_y(a, u) - rts.strand_y(b, u)
                d2 = rts.strand_y(a, u2) - rts.strand_y(b, u2)
                if d1 * d2 < 0:
                    found += 1
                    self.assertGreater(abs(rts.strand_top(a, u) - rts.strand_top(b, u)), 0.002)
        self.assertEqual(found, 6)  # three pairs, two crossings each, per period

    def test_s3_strictly_alternates_over_and_under_along_a_strand(self):
        n = 6000
        for a in range(3):
            order = []
            for i in range(n):
                u = i / n / rts.S3_PERIODS
                u2 = (i + 1) / n / rts.S3_PERIODS
                for b in range(3):
                    if b == a:
                        continue
                    d1 = rts.strand_y(a, u) - rts.strand_y(b, u)
                    d2 = rts.strand_y(a, u2) - rts.strand_y(b, u2)
                    if d1 * d2 < 0:
                        order.append(rts.strand_top(a, u) > rts.strand_top(b, u))
            self.assertEqual(len(order), 4)
            self.assertEqual(sum(order), 2)
            self.assertTrue(all(order[i] != order[(i + 1) % 4] for i in range(4)), msg=(a, order))

    def test_s3_beads_sit_at_both_edges(self):
        s0 = rts.MARGIN + 0.016
        top = rts.strip_height_m("S3") - s0
        self.assertGreater(rts.height("S3", 0.3, s0), rts.Z_BASE + 0.004)
        self.assertGreater(rts.height("S3", 0.3, top), rts.Z_BASE + 0.004)


class JointWeightWidenTest(unittest.TestCase):
    def test_the_mask_is_the_floor_of_the_joint_and_widen_adds_to_it(self):
        s = rts.strip_height_m("S1") * 0.5
        half = rts.JOINT_FLOOR / 2.0
        inside = (half - 0.0015) / rts.TILE_M
        outside = (half + 0.0015) / rts.TILE_M
        self.assertEqual(rts.joint_weight("S1", inside, s), 1.0)
        self.assertEqual(rts.joint_weight("S1", outside, s), 0.0)
        self.assertEqual(rts.joint_weight("S1", -inside, s), 1.0)
        self.assertEqual(rts.joint_weight("S1", 0.5 - outside, s), 0.0)
        self.assertEqual(rts.joint_weight("S1", outside, s, widen=0.006), 1.0)
        self.assertEqual(rts.joint_weight("S1", 0.25, s, widen=0.006), 0.0)
        self.assertEqual(rts.joint_weight("S1", 0.0, s, widen=0.0), rts.joint_weight("S1", 0.0, s))

    def test_the_edge_of_the_mask_is_soft_over_about_a_millimetre(self):
        s = rts.strip_height_m("S1") * 0.5
        half = rts.JOINT_FLOOR / 2.0
        mid = rts.joint_weight("S1", half / rts.TILE_M, s)
        self.assertAlmostEqual(mid, 0.5, delta=0.01)

    def test_s4_mask_is_the_s4_floor(self):
        s = rts.strip_height_m("S4") * 0.5
        half = rts.S4_JOINT_FLOOR / 2.0
        self.assertEqual(rts.joint_weight("S4", (1.0 / 6.0) + (half - 0.0015) / rts.TILE_M, s), 1.0)
        self.assertEqual(rts.joint_weight("S4", (1.0 / 6.0) + (half + 0.0015) / rts.TILE_M, s), 0.0)


class BeadEdgeTest(unittest.TestCase):
    """The lower and upper edge of the interlace band. Round 2b had a bead with 73 degree ends and a grid of
    3.3 mm: one pixel of cliff at 1024, which showed as a stepped line on the wall."""

    def test_no_edge_of_the_bead_is_steeper_than_forty_five_degrees(self):
        top = rts.strip_height_m("S3")
        step = 0.0002
        for u in (0.3, 0.37):
            for base, sign in ((rts.MARGIN, 1.0), (top - rts.MARGIN, -1.0)):
                worst = 0.0
                for i in range(260):
                    s = base + sign * i * step
                    worst = max(worst, abs(rts.height("S3", u, s + sign * step) - rts.height("S3", u, s)) / step)
                self.assertLessEqual(worst, 1.0, msg=(u, sign))

    def test_the_bead_keeps_its_peak_and_its_place(self):
        top = rts.strip_height_m("S3")
        for s in (rts.MARGIN + rts.S3_BEAD_CENTRE, top - rts.MARGIN - rts.S3_BEAD_CENTRE):
            self.assertAlmostEqual(rts.height("S3", 0.3, s), rts.Z_BASE + rts.S3_BEAD_PEAK, places=9)
        s0 = rts.MARGIN + rts.S3_BEAD_CENTRE - rts.S3_BEAD_HALF
        self.assertAlmostEqual(rts.height("S3", 0.3, s0), rts.Z_BASE, places=9)

    def test_the_edge_climbs_by_no_more_than_1_6_mm_in_the_first_pixel_of_a_2048_bake(self):
        px = rts.TILE_M / 2048.0
        s0 = rts.MARGIN + rts.S3_BEAD_CENTRE - rts.S3_BEAD_HALF
        rise = [rts.height("S3", 0.3, s0 + k * px) - rts.Z_BASE for k in range(0, 9)]
        self.assertTrue(all(b > a for a, b in zip(rise, rise[1:])))
        self.assertLess(rise[1], 0.0016)

    def test_the_mesh_has_a_line_every_one_and_a_half_millimetres_across_the_beads(self):
        sl = rts.s_lines("S3")
        top = rts.strip_height_m("S3")
        for c in (rts.MARGIN + rts.S3_BEAD_CENTRE, top - rts.MARGIN - rts.S3_BEAD_CENTRE):
            inside = [s for s in sl if c - rts.S3_BEAD_HALF - 1e-9 <= s <= c + rts.S3_BEAD_HALF + 1e-9]
            self.assertGreaterEqual(len(inside), 19)
            self.assertLessEqual(max(b - a for a, b in zip(inside, inside[1:])), 0.00151)


class ArcadeTest(unittest.TestCase):
    def test_s5_arch_count_is_four_or_eight(self):
        self.assertIn(rts.S5_ARCHES, (4, 8))

    def test_s5_repeats_per_arch(self):
        p = 1.0 / rts.S5_ARCHES
        for s in (0.08, 0.15, 0.22, 0.31):
            for i in range(20):
                u = (0.1 + 0.8 * i / 20.0) * p  # the joints at U 0 and 0.5 break the repeat, so keep clear of them
                self.assertAlmostEqual(rts.height("S5", u, s), rts.height("S5", u + p, s), places=12)

    def test_s5_niche_is_lower_than_the_pier(self):
        p = 1.0 / rts.S5_ARCHES
        s = rts.S5_SILL + 0.05
        niche = rts.height("S5", 0.5 * p, s)
        pier = rts.height("S5", 0.0, s)
        self.assertLess(niche, pier - 0.004)

    def test_s5_arch_is_pointed(self):
        sd = rts.arch_sd
        w = rts.S5_ARCH_WIDTH
        apex = rts.S5_SILL + rts.S5_RISE + rts.arch_rise()
        self.assertLess(sd(0.0, apex - 0.002), 0.0)
        self.assertGreater(sd(0.0, apex + 0.002), 0.0)
        # a round arch of the same width would still be inside at a quarter of the width and this height
        self.assertGreater(sd(w * 0.45, apex - 0.01), 0.0)


class GridTest(unittest.TestCase):
    def test_lines_include_the_ends_and_the_joints(self):
        for name in NAMES:
            ul = rts.u_lines(name)
            sl = rts.s_lines(name)
            self.assertEqual(ul[0], 0.0)
            self.assertAlmostEqual(ul[-1], 1.0)
            self.assertEqual(sl[0], 0.0)
            self.assertAlmostEqual(sl[-1], rts.strip_height_m(name))
            self.assertEqual(ul, sorted(ul))
            self.assertEqual(sl, sorted(sl))
            self.assertTrue(all(b - a > 1e-6 for a, b in zip(ul, ul[1:])))
            self.assertTrue(all(b - a > 1e-6 for a, b in zip(sl, sl[1:])))
        self.assertIn(0.5, rts.u_lines("S1"))
        for name in NAMES:
            floor, _, rnd = rts.joint_spec(name)
            lines = rts.u_lines(name)
            wanted = [floor / 2.0, floor / 2.0 + rnd]
            self.assertLessEqual(min(rts.ROUND_T), 0.03)  # the round is vertical at the floor: lines close together
            self.assertLessEqual(max(b - a for a, b in zip(rts.ROUND_T, rts.ROUND_T[1:])), 0.21)
            wanted += [floor / 2.0 + t * rnd for t in rts.ROUND_T]
            for j in rts.joint_positions(name):
                for edge in wanted:
                    for sign in (1, -1):
                        want = (j + sign * edge / rts.TILE_M) % 1.0
                        self.assertTrue(any(abs(l - want) < 1e-6 or abs(abs(l - want) - 1.0) < 1e-6 for l in lines),
                                        msg=(name, j, edge, sign))
        self.assertTrue(any(abs(s - rts.MARGIN) < 1e-9 for s in rts.s_lines("S1")))

    def test_the_budget_is_under_the_limit(self):
        total = rts.triangle_count()
        self.assertLessEqual(total, 1000000)
        self.assertGreater(total, 100000)


if __name__ == "__main__":
    unittest.main()
