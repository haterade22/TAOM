#!/usr/bin/env python3
"""Unit tests for tools/oneoff/rivendell_tower_plan.py (pure Python, no Blender).

Run:  python -m unittest tools.tests.test_rivendell_tower_plan
"""
import importlib.util
import math
import os
import sys
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(TOOLS, "oneoff"))
_spec = importlib.util.spec_from_file_location(
    "rivendell_tower_plan", os.path.join(TOOLS, "oneoff", "rivendell_tower_plan.py"))
plan = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(plan)
import rivendell_joint_profile as jp  # noqa: E402

EPS = 1e-6


def overlap(a, b):
    """Overlap area of two (x0, x1, y0, y1) rectangles."""
    w = min(a[1], b[1]) - max(a[0], b[0])
    h = min(a[3], b[3]) - max(a[2], b[2])
    return w * h if w > 1e-9 and h > 1e-9 else 0.0


class PieceTests(unittest.TestCase):
    def test_piece_is_two_bays_and_the_stubs_are_the_pinned_length(self):
        self.assertAlmostEqual(plan.PIECE_LENGTH, 2 * jp.BAY, places=9)
        self.assertAlmostEqual(plan.PIECE_X1 - plan.BODY_X, 1.6667, places=3)

    def test_length_times_texel_density_is_whole_for_both_densities(self):
        for td in (0.45, 0.75, jp.TD):
            n = plan.PIECE_LENGTH * td
            self.assertAlmostEqual(n, round(n), places=9, msg=f"TD {td}")

    def test_outline_of_a_is_the_pinned_rectangle(self):
        xs = [p[0] for p in plan.outline("a")]
        ys = [p[1] for p in plan.outline("a")]
        self.assertEqual((min(xs), max(xs), min(ys), max(ys)), (-5.0, 5.0, -3.5, 5.5))

    def test_outline_of_b_has_the_back_part_and_a_half_drum(self):
        pts = plan.outline("b")
        self.assertAlmostEqual(min(p[1] for p in pts), -3.5)
        self.assertAlmostEqual(max(p[1] for p in pts), 0.5 + 5.0, places=6)
        self.assertAlmostEqual(max(p[0] for p in pts), 5.0)

    def test_interior_is_the_outline_less_the_wall_thickness(self):
        a = plan.interior_bounds("a")
        self.assertEqual(a, (-4.2, 4.2, -2.7, 4.7))


class FlightTests(unittest.TestCase):
    def test_every_flight_has_the_pinned_counts_and_runs(self):
        f = {x["name"]: x for x in plan.flights()}
        self.assertEqual([f[k]["risers"] for k in ("F1", "F2", "F3")], [24, 24, 20])
        self.assertAlmostEqual(plan.flight_run(f["F1"]), 6.72)
        self.assertAlmostEqual(plan.flight_run(f["F3"]), 5.6)
        self.assertAlmostEqual(plan.flight_x_top(f["F1"]), 3.36)
        self.assertAlmostEqual(plan.flight_x_top(f["F2"]), -3.22)

    def test_each_flight_climbs_exactly_one_storey(self):
        for f in plan.flights():
            self.assertAlmostEqual(plan.flight_z_top(f) - f["z0"], f["risers"] * plan.RISER)
        top = {x["name"]: plan.flight_z_top(x) for x in plan.flights()}
        self.assertEqual((top["F1"], top["F2"], top["F3"]), (6.0, 12.0, 17.0))

    def test_every_flight_lies_inside_the_interior_for_both_variants(self):
        for v in plan.VARIANTS:
            for f in plan.flights():
                self.assertTrue(plan.rect_in_interior(v, plan.flight_footprint(f)), f"{v} {f['name']}")

    def test_a_flight_has_room_at_its_foot_to_step_on(self):
        for f in plan.flights():
            x0, x1, _, _ = plan.flight_footprint(f)
            wall = plan.interior_bounds("a")
            foot = f["x_foot"]
            room = (wall[1] - foot) if f["direction"] < 0 else (foot - wall[0])
            self.assertGreaterEqual(room, 0.7 - EPS, f["name"])

    def test_treads_count_matches_risers_and_the_last_tread_is_at_the_upper_level(self):
        for f in plan.flights():
            t = plan.treads(f)
            self.assertEqual(len(t), f["risers"])
            self.assertAlmostEqual(t[-1]["z"], plan.flight_z_top(f))
            self.assertAlmostEqual(t[0]["z"], f["z0"] + plan.RISER)


class HeadroomTests(unittest.TestCase):
    def test_every_tread_has_2_3_m_of_headroom_for_both_variants(self):
        self.assertAlmostEqual(plan.HEADROOM, 2.3)
        for v in plan.VARIANTS:
            worst = plan.min_headroom(v)
            self.assertGreaterEqual(worst["headroom"], 2.3 - EPS, f"{v} {worst}")

    def test_without_a_hole_the_upper_treads_would_fail(self):
        bare = plan.min_headroom("a", with_holes=False)
        self.assertLess(bare["headroom"], plan.HEADROOM)

    def test_each_hole_is_eleven_treads_long_and_as_wide_as_its_flight(self):
        for fl in plan.floors("a"):
            for h in fl["holes"]:
                self.assertAlmostEqual(h[1] - h[0], 11 * plan.TREAD, places=6, msg=fl["name"])
                self.assertAlmostEqual(h[3] - h[2], plan.LANE_W, places=6, msg=fl["name"])

    def test_holes_end_where_their_flight_ends(self):
        f = {x["name"]: x for x in plan.flights()}
        fl = {x["name"]: x for x in plan.floors("a")}
        self.assertAlmostEqual(fl["floor6"]["holes"][0][1], plan.flight_x_top(f["F1"]))
        self.assertAlmostEqual(fl["floor12"]["holes"][0][0], plan.flight_x_top(f["F2"]))
        self.assertAlmostEqual(fl["platform"]["holes"][0][0], plan.flight_x_top(f["F3"]))


class DoorPathTests(unittest.TestCase):
    def test_door_path_is_clear_of_holes_rails_and_flights_for_both_variants(self):
        path = plan.door_path()
        self.assertEqual(path, (-plan.BODY_X, plan.BODY_X, -0.7, 0.7))
        for v in plan.VARIANTS:
            for fl in plan.floors(v):
                if abs(fl["z_top"] - 12.0) > EPS:
                    continue
                for r in fl["holes"] + fl["rails"]:
                    self.assertEqual(overlap(path, r), 0.0, f"{v} {fl['name']} {r}")
            for f in plan.flights():
                self.assertEqual(overlap(path, plan.flight_footprint(f)), 0.0, f"{v} {f['name']}")

    def test_doors_are_centred_on_y0_and_1_4_wide(self):
        d = plan.side_door()
        self.assertEqual((d["width"], d["y_centre"], d["z_sill"]), (1.4, 0.0, 12.0))
        self.assertGreaterEqual(d["jamb"], 2.2 - EPS)
        self.assertGreaterEqual(d["height"], 2.5)

    def test_ground_door_is_1_6_wide_with_straight_sides_to_2_2_and_an_apex_at_3_2(self):
        d = plan.ground_door()
        self.assertEqual((d["width"], d["jamb"], d["height"], d["x_centre"], d["y_face"]), (1.6, 2.2, 3.2, 0.0, -3.5))


class RailTests(unittest.TestCase):
    @staticmethod
    def rail_hits_flight(rail, level, f):
        """Does the solid of flight f (underside to tread) meet the rail box (level to level + RAIL_H)?"""
        fp = plan.flight_footprint(f)
        lo, hi = max(rail[0], fp[0]), min(rail[1], fp[1])
        if overlap(rail, fp) == 0.0:
            return False
        for i in range(11):
            x = lo + (hi - lo) * i / 10.0
            top, under = plan.tread_z(f, x), plan.underside_z(f, x)
            if top is not None and top > level + EPS and under < level + plan.RAIL_H - EPS:
                return True
        return False

    def test_rails_stand_outside_their_hole_and_flight(self):
        for v in plan.VARIANTS:
            for fl in plan.floors(v):
                for r in fl["rails"]:
                    for h in fl["holes"]:
                        self.assertEqual(overlap(r, h), 0.0, f"{v} {fl['name']}")
                    for f in plan.flights():
                        self.assertFalse(self.rail_hits_flight(r, fl["z_top"], f), f"{v} {fl['name']} {f['name']}")

    def test_each_hole_has_a_rail_across_its_foot_end(self):
        for fl in plan.floors("a"):
            if fl["holes"]:
                self.assertGreaterEqual(len(fl["rails"]), 2, fl["name"])


class PlatformTests(unittest.TestCase):
    def test_catapult_rectangle_fits_outside_hatch_and_rail_for_both_variants(self):
        for v in plan.VARIANTS:
            for w, h in ((5.50, 5.72), (5.72, 5.50)):
                spot = plan.fit_rect(v, w, h)
                self.assertIsNotNone(spot, f"{v} {w}x{h}")

    def test_a_rectangle_too_big_does_not_fit(self):
        self.assertIsNone(plan.fit_rect("a", 9.0, 9.0))

    def test_variant_b_needs_its_corbel(self):
        self.assertIsNone(plan.fit_rect("b", 5.50, 5.72, corbel=0.0))
        self.assertIsNone(plan.fit_rect("b", 5.72, 5.50, corbel=0.0))

    def test_a_found_spot_stands_clear_of_the_raised_panels_on_the_inner_face_of_the_parapet(self):
        for v in plan.VARIANTS:
            for w, h in ((5.50, 5.72), (5.72, 5.50)):
                x, y = plan.fit_rect(v, w, h)
                clear = plan.platform_clear_rect(v)
                r = plan.PANEL_RELIEF_IN
                self.assertGreaterEqual(x, clear[0] + r - EPS, v)
                self.assertLessEqual(x + w, clear[1] - r + EPS, v)
                self.assertGreaterEqual(y, clear[2] + r - EPS, v)
                if v == "a":
                    self.assertLessEqual(y + h, clear[3] - r + EPS)
                else:
                    r_disk = plan.DRUM_R + plan.platform_offset(v) + plan.PARAPET_OUT - plan.PARAPET_T - r
                    for cx in (x, x + w):
                        self.assertLessEqual(math.hypot(cx, y + h - plan.DRUM_CY), r_disk + EPS)

    def test_a_found_spot_does_not_touch_an_obstacle(self):
        spot = plan.fit_rect("a", 5.50, 5.72)
        rect = (spot[0], spot[0] + 5.50, spot[1], spot[1] + 5.72)
        for o in plan.platform_obstacles("a"):
            self.assertEqual(overlap(rect, o), 0.0)


class ParapetTests(unittest.TestCase):
    def test_parts_of_a_face_tile_it_end_to_end(self):
        parts = plan.platform_parapet_parts("a")
        by_face = {}
        for p in parts:
            by_face.setdefault(p["face"], []).append(p)
        self.assertEqual(sorted(by_face), ["back", "east", "front", "west"])
        for face, items in by_face.items():
            items.sort(key=lambda p: p["x0"])
            for a, b in zip(items, items[1:]):
                self.assertAlmostEqual(a["x1"], b["x0"], places=9, msg=face)

    def test_every_part_is_long_enough_for_its_openings(self):
        for p in plan.platform_parapet_parts("a"):
            self.assertGreaterEqual(p["x1"] - p["x0"], 3.0)

    def test_variant_b_parts_are_straight_back_and_sides_then_five_arcs(self):
        parts = plan.platform_parapet_parts("b")
        faces = sorted({p["face"] for p in parts})
        self.assertEqual(faces, ["arc", "back", "east", "west"])
        arcs = sorted((p for p in parts if p["face"] == "arc"), key=lambda p: p["th0"])
        self.assertEqual(len(arcs), 5)
        self.assertAlmostEqual(arcs[0]["th0"], 0.0)
        self.assertAlmostEqual(arcs[-1]["th1"], 180.0)
        for a, b in zip(arcs, arcs[1:]):
            self.assertAlmostEqual(a["th1"], b["th0"], places=9)

    def test_variant_b_arc_parts_start_on_the_pier_angles(self):
        arcs = [p for p in plan.platform_parapet_parts("b") if p["face"] == "arc"]
        edges = sorted({p["th0"] for p in arcs} | {p["th1"] for p in arcs})
        self.assertEqual(edges, [0.0] + list(plan.PIER_ANGLES_B) + [180.0])

    def test_variant_b_every_part_is_long_enough_for_its_openings_on_the_inner_face(self):
        r_in = plan.DRUM_R + plan.platform_offset("b") + plan.PARAPET_OUT - plan.PARAPET_T
        for p in plan.platform_parapet_parts("b"):
            if p["face"] == "arc":
                length = math.radians(p["th1"] - p["th0"]) * r_in
            else:
                length = p["x1"] - p["x0"]
            self.assertGreaterEqual(length, 3.0, str(p))

    def test_variant_b_straight_parts_end_on_the_tangent_line(self):
        for p in plan.platform_parapet_parts("b"):
            if p["face"] in ("east", "west"):
                self.assertAlmostEqual(p["x1"] if p["face"] == "west" else p["x0"], 0.0, places=9)


class DrumTests(unittest.TestCase):
    def test_the_stub_of_b_ends_inside_the_wall_of_the_drum_at_every_height_of_its_face(self):
        x = plan.stub_inner_x("b")
        r_in, r_out = plan.DRUM_R - plan.WALL_T, plan.DRUM_R
        y = 0.5
        while y <= 2.65:
            self.assertLess(plan.drum_x(r_in, y), x, f"y {y}")
            self.assertGreater(plan.drum_x(r_out, y), x, f"y {y}")
            y += 0.05

    def test_the_stub_of_b_stays_inside_the_foundation_ring_where_it_leaves_the_drum_wall(self):
        r_foot = plan.DRUM_R + 0.6
        self.assertGreater(plan.drum_x(r_foot, jp.FOUNDATION_Y_OUT), plan.stub_inner_x("b"))

    def test_the_stub_of_a_ends_flush_on_the_side_face(self):
        self.assertEqual(plan.stub_inner_x("a"), plan.BODY_X)

    def test_the_drum_leaves_the_flat_wall_by_under_5_mm_at_the_door_jamb(self):
        y = plan.drum_join_y()
        self.assertAlmostEqual(y, 0.7)
        self.assertLess(plan.BODY_X - plan.drum_x(plan.DRUM_R, y), 0.005)
        r_in = plan.DRUM_R - plan.WALL_T
        self.assertLess(r_in - plan.drum_x(r_in, y), 0.005)

    def test_the_door_jamb_lies_in_the_flat_part_or_at_most_0_2_m_into_the_drum(self):
        self.assertLessEqual(plan.drum_join_y() - plan.DRUM_CY, 0.2 + EPS)

    def test_piers_are_36_degrees_apart_and_clear_of_the_stub_junction(self):
        a = plan.PIER_ANGLES_B
        self.assertEqual([round(b - c, 6) for c, b in zip(a, a[1:])], [36.0, 36.0, 36.0])
        half = math.degrees(jp.PIER_HALF / plan.DRUM_R)
        self.assertGreater(a[0] - half, plan.drum_junction_angle())
        self.assertLess(a[-1] + half, 180.0 - plan.drum_junction_angle())

    def test_junction_angle_is_where_the_stub_face_meets_the_drum(self):
        th = math.radians(plan.drum_junction_angle())
        self.assertAlmostEqual(plan.DRUM_CY + plan.DRUM_R * math.sin(th), jp.Y_OUT, places=6)

    def test_interior_outline_of_b_is_the_back_part_and_a_half_disk(self):
        pts = plan.interior_outline("b")
        self.assertAlmostEqual(min(p[1] for p in pts), -2.7)
        self.assertAlmostEqual(max(p[1] for p in pts), 0.5 + 4.2, places=6)
        self.assertAlmostEqual(max(p[0] for p in pts), 4.2)
        self.assertEqual(plan.interior_outline("a"), [(-4.2, -2.7), (4.2, -2.7), (4.2, 4.7), (-4.2, 4.7)])

    def test_floor_outline_of_b_covers_the_interior_and_ends_inside_the_wall(self):
        floor = plan.floor_outline("b")
        n = len(floor)
        for p in plan.interior_outline("b"):
            for i in range(n):
                a, b = floor[i], floor[(i + 1) % n]
                cross = (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])
                self.assertGreaterEqual(cross, -1e-9, f"{p} outside edge {i}")
        for x, y in floor:
            if y > plan.DRUM_CY + 1e-9:
                r = math.hypot(x, y - plan.DRUM_CY)
                self.assertGreater(r, plan.DRUM_R - plan.WALL_T - 1e-9)
                self.assertLess(r, plan.DRUM_R - plan.WALL_T + 0.02)
        self.assertEqual(plan.floor_outline("a"), plan.interior_outline("a"))

    def test_floor_outline_of_b_keeps_the_door_threshold_flat(self):
        floor = plan.floor_outline("b")
        edge = [p for p in floor if p[0] > 0 and abs(p[1] - plan.drum_join_y()) < 1e-9]
        self.assertEqual(edge, [(plan.interior_bounds("b")[1], plan.drum_join_y())])

    def test_every_flight_hole_and_rail_of_b_lies_on_the_floor(self):
        for fl in plan.floors("b"):
            if fl["name"] == "platform":
                continue
            for r in fl["holes"] + fl["rails"]:
                self.assertTrue(plan.rect_in_interior("b", r), f"{fl['name']} {r}")

    def test_platform_of_b_is_offset_by_the_corbel_on_every_side(self):
        self.assertEqual(plan.platform_offset("a"), 0.0)
        self.assertEqual(plan.platform_offset("b"), plan.CORBEL_B)
        rect = plan.platform_clear_rect("b")
        r_in = plan.DRUM_R + plan.CORBEL_B + plan.PARAPET_OUT - plan.PARAPET_T
        self.assertAlmostEqual(rect[1], r_in)
        self.assertAlmostEqual(-rect[0], r_in)
        self.assertAlmostEqual(rect[2], plan.BODY_Y0 - plan.CORBEL_B + plan.PARAPET_T - plan.PARAPET_OUT)

    def test_offset_outline_moves_every_edge_out_by_the_distance(self):
        base = plan.outline("b", steps=36)
        out = plan.offset_outline(base, 0.55)
        self.assertAlmostEqual(min(p[1] for p in out), -3.5 - 0.55, places=6)
        self.assertAlmostEqual(max(p[0] for p in out), 5.0 + 0.55, places=6)
        top = max(out, key=lambda p: p[1])
        self.assertAlmostEqual(top[1] - plan.DRUM_CY, 5.0 + 0.55 / math.cos(math.radians(2.5)), places=6)

    def test_outline_steps_are_five_degrees_with_36(self):
        pts = plan.outline("b", steps=36)
        self.assertEqual(len(pts), 2 + 37)
        self.assertEqual(pts[2], (5.0, 0.5))


class GroundDoorTests(unittest.TestCase):
    def test_outline_is_1_6_wide_up_to_2_2_and_has_its_apex_at_3_2(self):
        d = plan.ground_door()
        pts = plan.door_outline(d["width"], d["jamb"], d["height"], 6)
        self.assertEqual(pts[0], (-0.8, 0.0))
        self.assertEqual(pts[-1], (0.8, 0.0))
        self.assertAlmostEqual(max(v for _, v in pts), 3.2, places=9)
        for u, v in pts:
            if v <= 2.2 + EPS:
                self.assertAlmostEqual(abs(u), 0.8, places=9, msg=str((u, v)))
            else:
                self.assertLess(abs(u), 0.8)

    def test_the_apex_is_in_the_middle_and_the_head_is_pointed(self):
        pts = plan.door_outline(1.6, 2.2, 3.2, 6)
        apex = max(pts, key=lambda p: p[1])
        self.assertAlmostEqual(apex[0], 0.0, places=9)
        near = [p for p in pts if p[1] > 3.0 and p != apex]
        self.assertTrue(all(abs(p[0]) > 0.0 for p in near))

    def test_the_outline_is_mirror_symmetric(self):
        pts = plan.door_outline(1.6, 2.2, 3.2, 6)
        for (u0, v0), (u1, v1) in zip(pts, pts[::-1]):
            self.assertAlmostEqual(u0, -u1, places=9)
            self.assertAlmostEqual(v0, v1, places=9)

    def test_a_mould_moves_every_edge_out_by_the_mould_with_the_same_point_count(self):
        inner = plan.door_outline(1.6, 2.2, 3.2, 6)
        outer = plan.door_outline(1.6, 2.2, 3.2, 6, mould=0.14)
        self.assertEqual(len(inner), len(outer))
        self.assertAlmostEqual(outer[0][0], -0.94, places=9)
        self.assertAlmostEqual(outer[1][1], 2.2, places=9)
        for (u0, v0), (u1, v1) in zip(inner[1:-1], outer[1:-1]):
            self.assertGreaterEqual(math.hypot(u1 - u0, v1 - v0), 0.14 - 1e-6)
            self.assertLessEqual(math.hypot(u1 - u0, v1 - v0), 0.14 + 0.03)

    def test_the_cut_at_the_plinth_line_has_crossings_on_the_outline(self):
        lo_l, head, lo_r = plan.door_cut(1.6, 2.2, 3.2, 3.0, 6)
        self.assertAlmostEqual(lo_l[-1][1], 3.0)
        self.assertAlmostEqual(head[0][1], 3.0)
        self.assertAlmostEqual(head[-1][1], 3.0)
        self.assertAlmostEqual(lo_r[0][1], 3.0)
        self.assertAlmostEqual(lo_l[-1][0], -lo_r[0][0], places=9)
        self.assertTrue(all(v <= 3.0 + EPS for _, v in lo_l + lo_r))
        self.assertAlmostEqual(max(v for _, v in head), 3.2)
        self.assertEqual(lo_l[0], (-0.8, 0.0))
        self.assertEqual(lo_r[-1], (0.8, 0.0))

    def test_the_cut_rejects_an_apex_that_does_not_reach_the_plinth_line(self):
        with self.assertRaises(ValueError):
            plan.door_cut(1.6, 2.2, 2.9, 3.0, 6)

    def test_the_door_with_its_frame_stays_under_the_wall_arcade(self):
        d = plan.ground_door()
        self.assertLess(d["height"] + 0.14, 3.95)


class StairRailTests(unittest.TestCase):
    def test_an_open_side_gets_a_rail_and_a_side_on_a_wall_does_not(self):
        f = {x["name"]: x for x in plan.flights()}
        for v in plan.VARIANTS:
            self.assertEqual(plan.flight_rail_sides(v, f["F1"]), [(0.8, 1.0), (2.3, 2.5)])
            self.assertEqual(plan.flight_rail_sides(v, f["F2"]), [(-1.4, -1.2)])
            self.assertEqual(plan.flight_rail_sides(v, f["F3"]), [(-1.4, -1.2)])

    def test_a_rail_stands_outside_the_clear_width_of_its_flight(self):
        for v in plan.VARIANTS:
            for f in plan.flights():
                for y0, y1 in plan.flight_rail_sides(v, f):
                    lane = f["lane"]
                    self.assertTrue(y1 <= lane[0] + EPS or y0 >= lane[1] - EPS, f"{v} {f['name']}")
                    self.assertAlmostEqual(y1 - y0, plan.RAIL_T)

    def test_every_rail_lies_inside_the_room_for_both_variants(self):
        for v in plan.VARIANTS:
            for f in plan.flights():
                x0, x1, _, _ = plan.flight_footprint(f)
                for y0, y1 in plan.flight_rail_sides(v, f):
                    self.assertTrue(plan.rect_in_interior(v, (x0, x1, y0, y1)), f"{v} {f['name']} {y0}")

    def test_the_profile_is_one_metre_over_the_nosing_line_and_stops_under_the_slab(self):
        for f in plan.flights():
            prof = plan.stair_rail_profile(f)
            sb = plan.flight_z_top(f) - plan.SLAB_T
            self.assertAlmostEqual(max(z for _, z in prof), sb, places=9)
            for t in plan.treads(f):
                near = t["x0"] if f["direction"] > 0 else t["x1"]
                z_line = min(plan.stair_rail_top(f, near), sb)
                self.assertGreaterEqual(z_line - t["z"], min(plan.RAIL_H, sb - t["z"]) - EPS, f"{f['name']} {t['k']}")

    def test_the_rail_is_one_metre_over_every_tread_that_has_room_for_it(self):
        for f in plan.flights():
            sb = plan.flight_z_top(f) - plan.SLAB_T
            for t in plan.treads(f):
                if t["z"] + plan.RAIL_H <= sb:
                    near = t["x0"] if f["direction"] > 0 else t["x1"]
                    self.assertGreaterEqual(plan.stair_rail_top(f, near) - t["z"], plan.RAIL_H - EPS)

    def test_the_profile_is_a_simple_polygon_that_clears_every_floor_slab(self):
        for f in plan.flights():
            prof = plan.stair_rail_profile(f)
            area = 0.0
            for (x0, z0), (x1, z1) in zip(prof, prof[1:] + prof[:1]):
                area += x0 * z1 - x1 * z0
            self.assertGreater(abs(area) / 2.0, 1.0, f["name"])
            for x, z in prof:
                for s in plan.floors("a"):
                    self.assertFalse(s["z_bottom"] + EPS < z < s["z_top"] - EPS, f"{f['name']} {z}")
            self.assertLessEqual(max(z for _, z in prof), plan.flight_z_top(f) - plan.SLAB_T + EPS)

    def test_the_rail_ends_on_the_flight_ends(self):
        for f in plan.flights():
            xs = [x for x, _ in plan.stair_rail_profile(f)]
            self.assertAlmostEqual(min(xs), min(f["x_foot"], plan.flight_x_top(f)))
            self.assertAlmostEqual(max(xs), max(f["x_foot"], plan.flight_x_top(f)))


class FrontBayTests(unittest.TestCase):
    def test_the_front_has_a_pier_in_the_middle_and_one_on_each_corner(self):
        self.assertEqual(plan.FRONT_PIERS, (-5.0, 0.0, 5.0))

    def test_the_half_bay_is_half_the_pier_spacing_and_the_ribs_of_neighbours_meet(self):
        self.assertAlmostEqual(plan.FRONT_HALF_BAY, 2.5)
        for spec in (jp.RIB_LONG, jp.RIB_LOW):
            end = jp.rib_curve(1, spec, 0.0, 12, half_bay=plan.FRONT_HALF_BAY)[-1]
            self.assertAlmostEqual(end[0], plan.FRONT_HALF_BAY, places=9)

    def test_one_arch_stands_in_the_middle_of_each_bay_and_clears_the_piers(self):
        self.assertEqual(plan.FRONT_ARCH_X, (-2.5, 2.5))
        lo = 0.6 + 0.1
        hi = plan.BODY_X - 0.6 - 0.1
        for cx in plan.FRONT_ARCH_X:
            self.assertGreater(abs(cx) - plan.FRONT_ARCH_W / 2.0, lo)
            self.assertLess(abs(cx) + plan.FRONT_ARCH_W / 2.0, hi)


class LedgeUvTests(unittest.TestCase):
    R_REF = plan.DRUM_R + plan.CORBEL_B + 0.15

    def test_the_arc_rule_runs_u_along_the_arc_at_the_texel_density(self):
        th0, th1 = math.radians(40.0), math.radians(41.0)
        a = plan.ledge_uv(self.R_REF * math.cos(th0), plan.DRUM_CY + self.R_REF * math.sin(th0), True, self.R_REF)
        b = plan.ledge_uv(self.R_REF * math.cos(th1), plan.DRUM_CY + self.R_REF * math.sin(th1), True, self.R_REF)
        self.assertAlmostEqual(abs(a[0] - b[0]), jp.TD * self.R_REF * math.radians(1.0), places=9)
        self.assertAlmostEqual(a[1], 0.0, places=9)

    def test_v_runs_across_the_ledge_at_the_texel_density(self):
        th = math.radians(60.0)
        a = plan.ledge_uv(self.R_REF * math.cos(th), plan.DRUM_CY + self.R_REF * math.sin(th), True, self.R_REF)
        r = self.R_REF + 0.2
        b = plan.ledge_uv(r * math.cos(th), plan.DRUM_CY + r * math.sin(th), True, self.R_REF)
        self.assertAlmostEqual(b[1] - a[1], jp.TD * 0.2, places=9)

    def test_u_is_equal_on_the_two_sides_of_the_front_centre(self):
        for deg in (10.0, 33.0, 71.0):
            th, tm = math.radians(deg), math.radians(180.0 - deg)
            a = plan.ledge_uv(self.R_REF * math.cos(th), plan.DRUM_CY + self.R_REF * math.sin(th), True, self.R_REF)
            b = plan.ledge_uv(self.R_REF * math.cos(tm), plan.DRUM_CY + self.R_REF * math.sin(tm), True, self.R_REF)
            self.assertAlmostEqual(a[0], b[0], places=9)
            self.assertAlmostEqual(a[1], b[1], places=9)

    def test_the_arc_rule_starts_where_the_flat_side_ends(self):
        a = plan.ledge_uv(self.R_REF, plan.DRUM_CY, True, self.R_REF)
        self.assertAlmostEqual(a[0], jp.TD * plan.DRUM_CY, places=9)

    def test_the_flat_rule_is_the_wall_rule_for_a_horizontal_face(self):
        u, v = plan.ledge_uv(1.5, -4.0, False, self.R_REF, x_low=plan.PIECE_X0)
        self.assertEqual((u, v), jp.world_uv((0.0, 0.0, 1.0), (1.5, -4.0, 17.0), plan.PIECE_X0))


class RoomTests(unittest.TestCase):
    def test_a_point_on_the_wall_faces_of_the_room_is_in_the_room(self):
        self.assertTrue(plan.in_room("a", 4.2, 1.0))
        self.assertTrue(plan.in_room("a", -4.2, -2.7))
        self.assertTrue(plan.in_room("a", 0.0, 4.7))

    def test_a_point_in_a_wall_or_a_door_reveal_is_not_in_the_room(self):
        self.assertFalse(plan.in_room("a", 4.6, 0.7))
        self.assertFalse(plan.in_room("a", 0.0, -3.0))
        self.assertFalse(plan.in_room("a", 0.0, 5.0))

    def test_variant_b_has_a_round_end(self):
        self.assertTrue(plan.in_room("b", 0.0, 0.5 + 4.2))
        self.assertTrue(plan.in_room("b", 4.2, 0.5))
        self.assertFalse(plan.in_room("b", 3.5, 0.5 + 3.0))
        self.assertTrue(plan.in_room("b", 3.0, 0.5 + 2.9))
        self.assertFalse(plan.in_room("b", 0.0, 0.5 + 4.6))


class BackCourseTests(unittest.TestCase):
    """The inner plinth course of the back face stops where the ground door and its frame stand."""

    def test_half_width_is_the_door_half_width_up_to_the_jamb_and_zero_at_the_apex(self):
        self.assertAlmostEqual(plan.door_half_width_at(1.6, 2.2, 3.2, 0.0), 0.8, places=9)
        self.assertAlmostEqual(plan.door_half_width_at(1.6, 2.2, 3.2, 2.2), 0.8, places=9)
        self.assertAlmostEqual(plan.door_half_width_at(1.6, 2.2, 3.2, 3.2), 0.0, places=9)
        self.assertAlmostEqual(plan.door_half_width_at(1.6, 2.2, 3.2, 0.5, mould=0.14), 0.94, places=9)

    def test_half_width_matches_every_point_of_the_outline(self):
        for mould in (0.0, 0.14):
            pts = plan.door_outline(1.6, 2.2, 3.2, 6, mould=mould)
            for u, v in pts[2:-1]:
                if abs(u) < 1e-9:
                    continue
                self.assertAlmostEqual(plan.door_half_width_at(1.6, 2.2, 3.2, v, mould=mould), abs(u), places=6)

    def test_half_width_narrows_along_the_head(self):
        zs = [2.2 + 0.05 * i for i in range(21)]
        ws = [plan.door_half_width_at(1.6, 2.2, 3.2, z) for z in zs]
        self.assertTrue(all(a >= b for a, b in zip(ws, ws[1:])))

    def test_half_width_above_the_apex_is_zero_and_the_mould_raises_the_apex(self):
        self.assertEqual(plan.door_half_width_at(1.6, 2.2, 3.2, 3.5), 0.0)
        self.assertGreater(plan.door_half_width_at(1.6, 2.2, 3.2, 3.25, mould=0.14), 0.0)

    def test_the_course_runs_up_to_the_frame_on_each_side_of_the_door(self):
        d = plan.ground_door()
        spans = plan.back_course_spans(-4.2, 4.2, 0.14)
        w = plan.door_half_width_at(d["width"], d["jamb"], d["height"], jp.Z_PLINTH, mould=0.14)
        self.assertEqual(len(spans), 2)
        self.assertEqual(spans[0][0], -4.2)
        self.assertEqual(spans[1][1], 4.2)
        self.assertAlmostEqual(spans[0][1], d["x_centre"] - w, places=9)
        self.assertAlmostEqual(spans[1][0], d["x_centre"] + w, places=9)

    def test_no_course_stands_in_front_of_the_door_opening(self):
        d = plan.ground_door()
        hole = plan.door_half_width_at(d["width"], d["jamb"], d["height"], jp.Z_PLINTH)
        for a, b in plan.back_course_spans(-4.2, 4.2, 0.14):
            self.assertFalse(a < d["x_centre"] + hole and b > d["x_centre"] - hole)

class ParapetCornerTests(unittest.TestCase):
    """A parapet part that ends on a corner of the platform gets a short block of cap there, so that the cap of the
    side part (which overhangs its own face) does not stop in a step beside the end of the front or back part."""

    def test_variant_a_front_and_back_end_on_the_four_corners_and_the_sides_on_none(self):
        parts = plan.platform_parapet_parts("a")
        flags = [(p["face"], plan.corner_ends("a", p)) for p in parts]
        for face, (lo, hi) in flags:
            if face in ("east", "west"):
                self.assertEqual((lo, hi), (False, False), face)
        corners = sum(int(lo) + int(hi) for face, (lo, hi) in flags if face in ("front", "back"))
        self.assertEqual(corners, 4)

    def test_variant_a_the_middle_part_of_a_face_ends_on_no_corner(self):
        parts = [p for p in plan.platform_parapet_parts("a") if p["face"] == "front"]
        parts.sort(key=lambda p: p["x0"])
        self.assertEqual(plan.corner_ends("a", parts[0]), (True, False))
        self.assertEqual(plan.corner_ends("a", parts[1]), (False, False))
        self.assertEqual(plan.corner_ends("a", parts[-1]), (False, True))

    def test_variant_b_only_the_back_face_has_corners_and_arcs_and_sides_have_none(self):
        parts = plan.platform_parapet_parts("b")
        flags = [(p["face"], plan.corner_ends("b", p)) for p in parts]
        for face, ends in flags:
            if face != "back":
                self.assertEqual(ends, (False, False), face)
        self.assertEqual(sum(int(lo) + int(hi) for face, (lo, hi) in flags if face == "back"), 2)

    def test_the_block_is_the_overhang_of_the_cap_less_3_mm_so_that_no_face_is_coplanar_with_the_side_cap(self):
        self.assertAlmostEqual(plan.CORNER_CAP_LENGTH, jp.PARAPET_CAP_OVER - 0.003, places=9)
        self.assertGreater(plan.CORNER_CAP_LENGTH, 0.0)

class ParapetFriezeCornerTests(unittest.TestCase):
    """The frieze of an east or west part runs on over the end face of the front or back part to the corner, so that
    it does not stop in a step 0.5 m short of it."""

    def test_variant_a_a_side_face_has_its_two_corners_at_its_two_outer_ends_and_front_and_back_have_none(self):
        for face in ("east", "west"):
            parts = sorted((p for p in plan.platform_parapet_parts("a") if p["face"] == face), key=lambda p: p["x0"])
            self.assertEqual([plan.frieze_ends("a", p) for p in parts], [(True, False), (False, True)], face)
        for p in plan.platform_parapet_parts("a"):
            if p["face"] in ("front", "back"):
                self.assertEqual(plan.frieze_ends("a", p), (False, False), p["face"])

    def test_variant_b_the_side_parts_end_on_the_back_corners_only(self):
        parts = {p["face"]: p for p in plan.platform_parapet_parts("b") if p["face"] in ("east", "west")}
        self.assertEqual(plan.frieze_ends("b", parts["east"]), (False, True))
        self.assertEqual(plan.frieze_ends("b", parts["west"]), (True, False))
        for p in plan.platform_parapet_parts("b"):
            if p["face"] not in ("east", "west"):
                self.assertEqual(plan.frieze_ends("b", p), (False, False), p["face"])

    def test_the_frieze_block_is_as_long_as_the_body_of_the_parapet(self):
        self.assertAlmostEqual(plan.CORNER_FRIEZE_LENGTH, jp.PARAPET_Y1 - jp.PARAPET_Y0, places=9)


class ParapetFollowsTheSectionTests(unittest.TestCase):
    """The wall's parapet is 0.45 thick with its outer face in the plane of the wall face. The platform parapet of
    the towers is the same part, so its numbers come from the section, not from a copy."""

    def test_the_thickness_and_the_proud_come_from_the_section(self):
        self.assertAlmostEqual(plan.PARAPET_T, jp.PARAPET_Y1 - jp.PARAPET_Y0, places=9)
        self.assertAlmostEqual(plan.PARAPET_PROUD, jp.PARAPET_Y1 - jp.Y_OUT, places=9)
        self.assertAlmostEqual(plan.PARAPET_PROUD, 0.0, places=9)

    def test_the_inner_face_of_the_parapet_does_not_move(self):
        # the clear deck of the platform, and so the catapult rectangle, is as in version 3
        self.assertAlmostEqual(plan.BODY_Y1 - plan.platform_clear_rect("a")[3], 0.45, places=9)
        self.assertAlmostEqual(plan.platform_clear_rect("a")[1], plan.BODY_X - 0.45, places=9)

    def test_variant_a_front_and_back_parts_end_on_the_plane_of_the_side_faces(self):
        parts = [p for p in plan.platform_parapet_parts("a") if p["face"] == "front"]
        self.assertAlmostEqual(min(p["x0"] for p in parts), -plan.BODY_X, places=9)
        self.assertAlmostEqual(max(p["x1"] for p in parts), plan.BODY_X, places=9)

    def test_variant_a_the_side_parts_run_between_the_inner_faces_of_front_and_back(self):
        east = [p for p in plan.platform_parapet_parts("a") if p["face"] == "east"]
        y_lo = plan.FACE_CY - max(p["x1"] for p in east)
        y_hi = plan.FACE_CY - min(p["x0"] for p in east)
        self.assertAlmostEqual(y_lo, plan.BODY_Y0 + plan.PARAPET_T, places=9)
        self.assertAlmostEqual(y_hi, plan.BODY_Y1 - plan.PARAPET_T, places=9)


class CellTests(unittest.TestCase):
    """The vertex colour cells: fine on every shell that reaches an end plane (the seam test compares them with the
    wall) and on the plinth (the damp gradient lives there), coarse on the rest of the body."""

    def test_the_body_cell_is_a_whole_multiple_of_the_wall_cell_and_b_is_coarser_than_a(self):
        self.assertAlmostEqual(plan.CELL_BODY["a"], 2.0 * jp.CELL_MAX, places=9)
        self.assertAlmostEqual(plan.CELL_BODY["b"], 8.0 * jp.CELL_MAX, places=9)

    def test_a_shell_inside_the_body_takes_the_body_cell_of_its_variant(self):
        for v in ("a", "b"):
            self.assertEqual(plan.cell_max_for(v, -plan.BODY_X, plan.BODY_X, 16.7), plan.CELL_BODY[v])
            self.assertEqual(plan.cell_max_for(v, -1.0, 3.0, 16.7), plan.CELL_BODY[v])

    def test_a_shell_that_reaches_past_the_body_takes_the_wall_cell(self):
        for v in ("a", "b"):
            self.assertEqual(plan.cell_max_for(v, plan.BODY_X - 0.6, plan.PIECE_X1, 12.0), jp.CELL_MAX)
            self.assertEqual(plan.cell_max_for(v, plan.PIECE_X0, -plan.BODY_X + 0.6, 12.0), jp.CELL_MAX)
            self.assertEqual(plan.cell_max_for(v, plan.PIECE_X0, plan.PIECE_X1, 12.0), jp.CELL_MAX)

    def test_variant_b_stub_starts_inside_the_drum_and_is_still_fine(self):
        self.assertEqual(plan.cell_max_for("b", plan.stub_inner_x("b"), plan.PIECE_X1, 12.0), jp.CELL_MAX)

    def test_a_shell_in_the_plinth_zone_takes_the_plinth_cell_that_the_mesh_has_already(self):
        for v in ("a", "b"):
            self.assertEqual(plan.cell_max_for(v, -5.6, 5.6, jp.Z_PLINTH), plan.PLINTH_CELL)
            self.assertEqual(plan.cell_max_for(v, -5.6, 5.6, jp.Z_PLINTH + 0.1), plan.CELL_BODY[v])

    def test_the_plinth_cell_is_half_the_plinth_height_and_the_damp_is_gone_by_then(self):
        self.assertAlmostEqual(plan.PLINTH_CELL, 0.5 * jp.Z_PLINTH, places=9)
        self.assertEqual(jp.damp(plan.PLINTH_CELL), 0.0)

    def test_the_tone_of_the_plinth_rows_is_within_5_levels_of_the_wall_rows(self):
        # the wall has vertices at Z 0, 1, 2, 3; the tower plinth at Z 0, 1.5, 3
        def tone(z):
            return jp.vertex_tone("rubble", 0.0, z)[1]
        for z in (0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 2.5):
            wall_rows = [0.0, 1.0, 2.0, 3.0]
            lo = max(r for r in wall_rows if r <= z)
            hi = min(r for r in wall_rows if r >= z)
            w = tone(z) if hi == lo else tone(lo) + (tone(hi) - tone(lo)) * (z - lo) / (hi - lo)
            tw = tone(0.0) + (tone(1.5) - tone(0.0)) * z / 1.5 if z <= 1.5 else tone(1.5) + (tone(3.0) - tone(1.5)) * (z - 1.5) / 1.5
            self.assertLess(abs(w - tw) * 255.0, 5.0, msg=str(z))

    def test_a_cell_never_exceeds_its_limit(self):
        for lo, hi in ((0.0, 3.0), (-5.0, 5.0), (3.0, 13.7)):
            for cell in plan.CELL_BODY.values():
                edges = [lo] + jp.even_cuts(lo, hi, cell) + [hi]
                self.assertLessEqual(max(y - x for x, y in zip(edges, edges[1:])), cell + 1e-9)


class SillTests(unittest.TestCase):
    """The sill under a blind arch frame on the back face of tower a."""

    def test_the_sill_ends_flush_with_the_outer_faces_of_the_outer_legs(self):
        """Version 6 (Mike, item 3): a band under a group of arches is as wide as the group."""
        lo, hi = plan.sill_x_range(0.9, 1.6)
        self.assertAlmostEqual(lo, 0.9 - 0.8, delta=plan.FLUSH_INSET + 1e-9)
        self.assertAlmostEqual(hi, 0.9 + 0.8, delta=plan.FLUSH_INSET + 1e-9)
        self.assertEqual(plan.SILL_OVER, 0.0)
        self.assertGreater(lo, 0.9 - 0.8)  # inside the leg: the end face of the sill is not in the plane of its outer face

    def test_the_frame_foot_stands_inside_the_sill(self):
        prof = plan.sill_profile(3.95, 2.4, 2.62)
        zs = [z for _, z in prof]
        self.assertGreater(max(zs), 3.95 + 0.01)
        self.assertLess(min(zs), 3.95 - 0.10)

    def test_the_sill_reaches_past_the_front_of_the_frame_and_back_into_the_wall(self):
        prof = plan.sill_profile(3.95, 2.4, 2.62)
        ys = [y for y, _ in prof]
        self.assertAlmostEqual(min(ys), 2.4, places=9)
        self.assertAlmostEqual(max(ys), 2.62 + plan.SILL_PROUD, places=9)

    def test_the_top_slopes_out_so_the_rain_runs_off(self):
        prof = plan.sill_profile(3.95, 2.4, 2.62)
        top_back = max(z for y, z in prof if y <= 2.4 + 1e-9)
        top_front = max(z for y, z in prof if y >= 2.62 + plan.SILL_PROUD - 1e-9)
        self.assertGreater(top_back, top_front)

    def test_the_profile_is_a_simple_polygon_of_positive_area(self):
        prof = plan.sill_profile(3.95, 2.4, 2.62)
        n = len(prof)
        area = 0.5 * sum(prof[i][0] * prof[(i + 1) % n][1] - prof[(i + 1) % n][0] * prof[i][1] for i in range(n))
        self.assertGreater(abs(area), 0.01)
        self.assertEqual(len(set(prof)), n)


class DrumUvTests(unittest.TestCase):
    """The UV rule of a stone or plinth face on the drum (the rule of blender_rivendell_tower_drum.stone_uv)."""

    R = plan.DRUM_R
    CY = plan.DRUM_CY

    def face(self, deg, r, z, batter=False):
        a = math.radians(deg)
        co = (r * math.cos(a), self.CY + r * math.sin(a), z)
        if batter:
            slant = math.hypot(0.6, 3.0)
            n_rad, n_z = 3.0 / slant, 0.6 / slant
        else:
            n_rad, n_z = 1.0, 0.0
        n = (n_rad * math.cos(a), n_rad * math.sin(a), n_z)
        return n, co, (co[0], co[1])

    def test_a_vertical_stone_face_has_v_equal_td_z(self):
        for deg in (20.0, 90.0, 140.0):
            for z in (3.5, 8.0, 16.0):
                n, co, mid = self.face(deg, self.R, z)
                self.assertAlmostEqual(plan.drum_uv(n, co, mid, plan.PIECE_X0)[1], jp.TD * z, places=9)

    def test_a_battered_plinth_face_has_the_bed_joint_at_the_top_edge(self):
        for deg in (20.0, 70.0, 90.0, 110.0, 160.0):
            n, co, mid = self.face(deg, self.R, 3.0, batter=True)
            v = plan.drum_uv(n, co, mid, plan.PIECE_X0, plinth=True)[1]
            self.assertAlmostEqual(v, jp.PLINTH_V_TOP, places=4, msg=str(deg))

    def test_a_battered_plinth_face_starts_at_v_zero_on_the_ground_line(self):
        n, co, mid = self.face(90.0, self.R + 0.6, 0.0, batter=True)
        self.assertAlmostEqual(plan.drum_uv(n, co, mid, plan.PIECE_X0, plinth=True)[1], 0.0, places=9)

    def test_the_plinth_drum_v_equals_the_wall_plinth_v_at_the_same_height(self):
        n, co, mid = self.face(90.0, self.R + 0.3, 1.5, batter=True)
        wall_v = jp.plinth_uv((0.0, 0.98058, 0.19612), (0.0, 2.8, 1.5), 0.0)[1]
        self.assertAlmostEqual(plan.drum_uv(n, co, mid, plan.PIECE_X0, plinth=True)[1], wall_v, places=4)

    def test_a_vertical_plinth_face_below_the_ground_runs_on_at_td(self):
        n, co, mid = self.face(90.0, self.R, -2.0)
        self.assertAlmostEqual(plan.drum_uv(n, co, mid, plan.PIECE_X0, plinth=True)[1],
                               jp.plinth_v_vertical(-2.0), places=9)

    def test_a_battered_stone_face_has_v_along_the_slope_from_the_ground_line(self):
        n, co, mid = self.face(90.0, self.R, 2.0, batter=True)
        tz = 3.0 / math.hypot(0.6, 3.0)
        self.assertAlmostEqual(plan.drum_uv(n, co, mid, plan.PIECE_X0)[1], jp.TD * 2.0 / tz, places=9)

    def test_u_is_mirrored_about_the_front_centre_and_runs_at_td_along_the_arc(self):
        n1, co1, mid1 = self.face(60.0, self.R, 8.0)
        n2, co2, mid2 = self.face(120.0, self.R, 8.0)
        u1 = plan.drum_uv(n1, co1, mid1, plan.PIECE_X0)[0]
        u2 = plan.drum_uv(n2, co2, mid2, plan.PIECE_X0)[0]
        self.assertAlmostEqual(u1, u2, places=9)
        n3, co3, mid3 = self.face(50.0, self.R, 8.0)
        u3 = plan.drum_uv(n3, co3, mid3, plan.PIECE_X0)[0]
        self.assertAlmostEqual(abs(u1 - u3), jp.TD * self.R * math.radians(10.0), places=9)

    def test_a_face_on_the_flat_side_takes_the_wall_rule(self):
        n, co = (1.0, 0.0, 0.0), (5.0, -1.0, 6.0)
        self.assertEqual(plan.drum_uv(n, co, (5.0, -1.0), plan.PIECE_X0), jp.world_uv(n, co, plan.PIECE_X0))
        self.assertEqual(plan.drum_uv(n, co, (5.0, -1.0), plan.PIECE_X0, plinth=True),
                         jp.plinth_uv(n, co, plan.PIECE_X0))

    def test_a_horizontal_face_takes_the_wall_rule(self):
        n, co = (0.0, 0.0, 1.0), (1.0, 3.0, 17.0)
        self.assertEqual(plan.drum_uv(n, co, (1.0, 3.0), plan.PIECE_X0), jp.world_uv(n, co, plan.PIECE_X0))
        self.assertEqual(plan.drum_uv(n, co, (1.0, 3.0), plan.PIECE_X0, plinth=True),
                         jp.plinth_uv(n, co, plan.PIECE_X0))

    def test_a_face_on_a_radius_runs_u_along_the_radius(self):
        r = math.hypot(4.0, 2.0)
        n, co = (-2.0 / r, 4.0 / r, 0.0), (4.0, self.CY + 2.0, 6.0)
        u, v = plan.drum_uv(n, co, (4.0, self.CY + 2.0), plan.PIECE_X0)
        self.assertAlmostEqual(u, jp.TD * r, places=9)
        self.assertAlmostEqual(v, jp.TD * 6.0, places=9)


class EndShellTests(unittest.TestCase):
    def test_a_stub_shell_is_an_end_shell_and_the_body_and_the_platform_slab_are_not(self):
        self.assertTrue(plan.is_end_shell(plan.BODY_X - 0.6, plan.PIECE_X1))
        self.assertTrue(plan.is_end_shell(plan.PIECE_X0, -plan.stub_inner_x("b")))
        self.assertFalse(plan.is_end_shell(-plan.BODY_X, plan.BODY_X))
        self.assertFalse(plan.is_end_shell(-5.9, 5.9))  # the platform slab of variant b, with its corbel

    def test_an_end_shell_is_cut_along_z_only(self):
        self.assertFalse(plan.wants_x_cuts(plan.BODY_X - 0.6, plan.PIECE_X1))
        self.assertTrue(plan.wants_x_cuts(-plan.BODY_X, plan.BODY_X))


class RailCapTests(unittest.TestCase):
    """The cap of a rail is CAP_OVER wider than the rail. Where the rail across the foot of a hole meets a side rail,
    their caps would cross and share a plane over a 4 cm by 24 cm square, so the cap of the cross rail stops at the
    cap of the side rail."""

    def rails(self, variant, name):
        return [f for f in plan.floors(variant) if f["name"] == name][0]["rails"]

    def test_a_rail_along_x_is_longer_in_x_and_the_foot_rail_is_longer_in_y(self):
        for v in plan.VARIANTS:
            for fl in plan.floors(v):
                for r in fl["rails"]:
                    self.assertEqual(plan.rail_is_along_x(r), (r[1] - r[0]) >= (r[3] - r[2]))

    def test_the_cap_of_a_side_rail_is_wider_in_y_only(self):
        r = (-2.0, 1.0, 3.0, 3.2)
        c = plan.CAP_OVER
        self.assertEqual(plan.rail_cap_rect(r, [r]), (-2.0, 1.0, 3.0 - c, 3.2 + c))

    def test_the_cap_of_a_foot_rail_with_a_side_rail_at_each_end_stops_at_their_caps(self):
        side_lo, side_hi, foot = (-2.0, 1.2, 1.0, 1.2), (-2.0, 1.2, 3.0, 3.2), (1.0, 1.2, 1.2, 3.0)
        c = plan.CAP_OVER
        rect = plan.rail_cap_rect(foot, [side_lo, side_hi, foot])
        self.assertEqual(rect, (1.0 - c, 1.2 + c, 1.2 + c, 3.0 - c))

    def test_the_cap_of_a_foot_rail_with_no_side_rail_reaches_the_end_of_the_rail(self):
        foot = (1.0, 1.2, 1.2, 3.0)
        c = plan.CAP_OVER
        self.assertEqual(plan.rail_cap_rect(foot, [foot]), (1.0 - c, 1.2 + c, 1.2, 3.0))

    def test_no_two_caps_of_the_rails_of_one_floor_overlap_in_plan(self):
        for v in plan.VARIANTS:
            for fl in plan.floors(v):
                rects = [plan.rail_cap_rect(r, fl["rails"]) for r in fl["rails"]]
                for i in range(len(rects)):
                    for j in range(i + 1, len(rects)):
                        self.assertEqual(overlap(rects[i], rects[j]), 0.0, msg=f"{v} {fl['name']} {i} {j}")


class LedgeRingTests(unittest.TestCase):
    """The ring of the platform slab of variant b that stands out past the parapet: its U runs along the arc at the
    radius of the middle of the ring, so the density holds within 5 percent only when that radius is the middle."""

    def test_the_ring_runs_from_the_slab_edge_in_to_under_the_parapet(self):
        outer, inner = plan.ledge_offsets("b")
        self.assertAlmostEqual(outer, plan.CORBEL_B + plan.SLAB_PROUD, places=9)
        self.assertAlmostEqual(inner, plan.CORBEL_B + plan.PARAPET_OUT - plan.LEDGE_UNDER, places=9)
        self.assertGreater(outer, inner)

    def test_the_reference_radius_is_the_middle_of_the_ring(self):
        outer, inner = plan.ledge_offsets("b")
        self.assertAlmostEqual(plan.ledge_mid_radius(), plan.DRUM_R + 0.5 * (outer + inner), places=9)

    def test_the_density_over_the_ring_stays_within_5_percent(self):
        outer, inner = plan.ledge_offsets("b")
        mid = plan.ledge_mid_radius()
        for r in (plan.DRUM_R + inner, plan.DRUM_R + outer):
            self.assertLess(abs(mid / r - 1.0), 0.05)


class PierTaperTests(unittest.TestCase):
    """Version 6 (Mike, item 1): a pier, corner pier, quoin or drum pilaster starts its taper at the top of the collar
    and dies into the wall in one straight slope at the foot of the string course. Nothing of it reaches the cornice."""

    def parts(self):
        return {"front corner": plan.corner_pier_parts(True), "back quoin": plan.corner_pier_parts(False),
                "drum pier": plan.drum_pier_parts()}

    def test_each_run_of_parts_is_one_continuous_solid(self):
        for name, parts in self.parts().items():
            for a, b in zip(parts, parts[1:]):
                self.assertAlmostEqual(a[3], b[1], places=9, msg=name)
                if abs(a[3] - jp.PIER_TOP_Z) > 1e-9:  # the one step is the ledge under the collar, where the pier front ends
                    self.assertAlmostEqual(a[2], b[0], places=9, msg=name)

    def test_nothing_of_a_pier_stands_above_the_foot_of_the_string_course(self):
        for name, parts in self.parts().items():
            self.assertAlmostEqual(max(z1 for _, _, _, z1 in parts), jp.STRING_Z0, places=9, msg=name)

    def test_the_last_part_is_the_taper_and_it_starts_where_the_collar_ends(self):
        for name, parts in self.parts().items():
            p0, z0, p1, z1 = parts[-1]
            self.assertAlmostEqual(z0, jp.PILASTER_TAPER_Z0, places=9, msg=name)
            self.assertAlmostEqual(p0, jp.PILASTER_PROUD, places=9, msg=name)
            self.assertLessEqual(p1, 0.0, msg=name)

    def test_the_taper_never_grows_going_up_and_has_no_flat_ledge(self):
        for name, parts in self.parts().items():
            for p0, z0, p1, z1 in parts:
                self.assertLess(z0, z1, msg=name)
                if z0 >= jp.PILASTER_TAPER_Z0 - 1e-9:
                    self.assertLess(p1, p0, msg=name)

    def test_a_flat_faced_corner_follows_the_wall_strip_at_every_height(self):
        for front in (True, False):
            for p0, z0, p1, z1 in plan.corner_pier_parts(front):
                if z0 < jp.PIER_TOP_Z - 1e-9:
                    continue
                for k in range(0, 11):
                    z = z0 + (z1 - z0) * k / 10.0
                    p = p0 + (p1 - p0) * k / 10.0
                    self.assertAlmostEqual(p, jp.pilaster_proud(z), places=9, msg=(front, z))

    def test_a_drum_pilaster_dies_a_little_inside_the_wall_so_its_flat_front_leaves_no_ledge(self):
        """The front of a drum pilaster is flat and 1.2 m wide, so its edges stand out of the round wall by 3.6 mm at
        zero proud. It ends 4 cm inside the wall: both its middle and its edges die before the string course."""
        p_end = plan.drum_pier_parts()[-1][2]
        edge_out = plan.DRUM_R - math.sqrt(plan.DRUM_R ** 2 - jp.PIER_HALF ** 2)
        self.assertLess(p_end, -edge_out)
        self.assertGreater(p_end, -0.06)

    def test_the_drum_pier_below_the_taper_is_the_wall_pier(self):
        parts = plan.drum_pier_parts()
        self.assertEqual(parts[:3], plan.corner_pier_parts(True)[:3])


class WrapPathTests(unittest.TestCase):
    """The string course wraps the corners of the body in one mitred sweep (version 6, item 1)."""

    def test_the_front_path_runs_from_stub_to_stub_round_both_front_corners(self):
        front, back = plan.string_course_corner_paths()
        self.assertEqual(front[0], (plan.BODY_X, jp.Y_OUT + jp.STRING_PROUD))
        self.assertEqual(front[1:3], [(plan.BODY_X, plan.BODY_Y1), (-plan.BODY_X, plan.BODY_Y1)])
        self.assertEqual(front[3], (-plan.BODY_X, jp.Y_OUT + jp.STRING_PROUD))
        self.assertEqual(back[1:3], [(-plan.BODY_X, plan.BODY_Y0), (plan.BODY_X, plan.BODY_Y0)])
        self.assertEqual(back[0][1], back[3][1])
        self.assertAlmostEqual(back[0][1], jp.Y_IN - jp.INNER_STRING_PROUD, places=9)

    def test_a_corner_has_the_mitre_vector_and_an_end_has_the_edge_normal(self):
        pts = plan.wrap_path(plan.string_course_corner_paths()[0], 100.0)
        self.assertEqual(len(pts), 4)
        want = ((1.0, 0.0), (1.0, 1.0), (-1.0, 1.0), (-1.0, 0.0))
        for p, w in zip(pts, want):
            self.assertEqual(tuple(round(c, 9) for c in p[2:]), w)

    def test_the_back_path_points_outward_to_minus_y(self):
        pts = plan.wrap_path(plan.string_course_corner_paths()[1], 100.0)
        self.assertEqual(tuple(round(c, 9) for c in pts[1][2:]), (-1.0, -1.0))
        self.assertEqual(tuple(round(c, 9) for c in pts[2][2:]), (1.0, -1.0))

    def test_a_long_run_gets_sections_at_most_max_len_apart_and_keeps_its_corners(self):
        front = plan.string_course_corner_paths()[0]
        pts = plan.wrap_path(front, 2.5)
        for a, b in zip(pts, pts[1:]):
            self.assertLessEqual(math.hypot(b[0] - a[0], b[1] - a[1]), 2.5 + 1e-9)
        xy = [(round(p[0], 9), round(p[1], 9)) for p in pts]
        for c in front:
            self.assertIn((round(c[0], 9), round(c[1], 9)), xy)

    def test_the_sections_of_a_run_are_evenly_spaced(self):
        pts = plan.wrap_path([(0.0, 0.0), (10.0, 0.0)], 2.5)
        self.assertEqual(len(pts), 5)
        self.assertAlmostEqual(pts[1][0], 2.5, places=9)


class BandFlushTests(unittest.TestCase):
    """Version 6 (Mike, item 3): where blind arches stand on a band, the band is as wide as their outer faces."""

    def outer_width(self, w):
        pts = jp.pointed_arch(w, 4.0, 8)
        return min(p[0] for p in pts), max(p[0] for p in pts)

    def test_the_front_bands_end_at_the_outer_faces_of_the_front_arch(self):
        lo, hi = self.outer_width(plan.FRONT_ARCH_W)
        spans = plan.band_spans("front")
        self.assertEqual(len(spans), len(plan.FRONT_ARCH_X))
        for (a, b), cx in zip(spans, plan.FRONT_ARCH_X):
            self.assertAlmostEqual(a, cx + lo, delta=plan.FLUSH_INSET + 1e-9)
            self.assertAlmostEqual(b, cx + hi, delta=plan.FLUSH_INSET + 1e-9)
            self.assertGreater(a, cx + lo)
            self.assertLess(b, cx + hi)

    def test_the_side_bands_end_at_the_outer_faces_of_the_side_arch(self):
        lo, hi = self.outer_width(jp.ARCH_W)
        for face in ("east", "west"):
            (a, b), = plan.band_spans(face)
            cx = plan.SIDE_ARCH_X[face]
            self.assertAlmostEqual(a, cx + lo, delta=plan.FLUSH_INSET + 1e-9)
            self.assertAlmostEqual(b, cx + hi, delta=plan.FLUSH_INSET + 1e-9)

    def test_the_side_arch_stands_between_the_corner_pier_and_the_stub(self):
        for face in ("east", "west"):
            (a, b), = plan.band_spans(face)
            corner = plan.BODY_Y1 - plan.FACE_CY  # the front corner pier is at local x -corner (east) or +corner (west)
            d0, d1 = sorted((abs(a + corner) if face == "east" else abs(corner - b),
                             abs(b + corner) if face == "east" else abs(corner - a)))
            self.assertGreater(d0, jp.COLLAR_HALF_W)
            self.assertLess(d1, plan.BODY_Y1 - (jp.Y_OUT + jp.STRING_PROUD) + 1e-9)

    def test_the_drum_band_is_as_wide_as_the_arch_of_its_bay(self):
        half = plan.drum_band_half_angle()
        self.assertAlmostEqual(half * plan.DRUM_R, plan.BAY_TIER1_W / 2.0, delta=plan.FLUSH_INSET + 1e-9)
        self.assertLess(half * plan.DRUM_R, plan.BAY_TIER1_W / 2.0)
        self.assertEqual(plan.drum_bay_centres(), [54.0, 90.0, 126.0])

    def test_the_sill_of_the_back_arches_is_the_width_of_the_group(self):
        lo, hi = plan.sill_x_range(-2.7, jp.ARCH_W)[0], plan.sill_x_range(2.7, jp.ARCH_W)[1]
        self.assertAlmostEqual(lo, -2.7 - jp.ARCH_W / 2.0, delta=plan.FLUSH_INSET + 1e-9)
        self.assertAlmostEqual(hi, 2.7 + jp.ARCH_W / 2.0, delta=plan.FLUSH_INSET + 1e-9)


class FineCurveTests(unittest.TestCase):
    """Version 6 (Mike, item 2): a curve reads as a curve from 3 m: at most 4 degrees between neighbour segments."""

    def turns(self, pts):
        out = []
        for a, b, c in zip(pts, pts[1:], pts[2:]):
            u, v = (b[0] - a[0], b[1] - a[1]), (c[0] - b[0], c[1] - b[1])
            cos = (u[0] * v[0] + u[1] * v[1]) / (math.hypot(*u) * math.hypot(*v))
            out.append(math.degrees(math.acos(max(-1.0, min(1.0, cos)))))
        return out

    def test_the_fine_door_outline_turns_at_most_4_degrees_outside_the_springing_and_the_apex(self):
        d = plan.ground_door()
        n = plan.door_fine_steps(d["width"], d["jamb"], d["height"])
        pts = plan.door_outline(d["width"], d["jamb"], d["height"], n)
        t = self.turns(pts)
        arc = t[1:-1]  # the first turn is the springing (the straight jamb meets the arc), the last the one at the other foot
        inside = [x for i, x in enumerate(arc) if i != n - 1]  # the turn at the apex is the point of the arch
        self.assertLessEqual(max(inside), plan.FINE_TURN_DEG + 0.3)
        self.assertGreater(n, 6)

    def test_a_flatter_head_needs_fewer_steps(self):
        a = plan.door_fine_steps(1.6, 2.2, 3.2)
        b = plan.door_fine_steps(1.6, 2.2, 3.6)
        self.assertGreater(a, 0)
        self.assertNotEqual(a, b)

    def test_the_stub_ribs_of_a_end_inside_the_body_and_those_of_b_inside_the_drum(self):
        self.assertAlmostEqual(plan.stub_rib_clip_x("a"), plan.BODY_X - 0.05, places=9)
        x = plan.stub_rib_clip_x("b")
        front = jp.Y_OUT + 0.52 + 0.05  # the highest the rib of a stub stands: its front, with a margin
        self.assertLess(x, plan.stub_inner_x("b"))
        self.assertGreater(plan.DRUM_CY + math.sqrt(plan.DRUM_R ** 2 - x ** 2), front)


class UpperTierTests(unittest.TestCase):
    """Version 6: the upper tier stands on the string course, and the upper ribs of the drum stand whole on a collar."""

    def collar_front(self):
        prof = plan.upper_collar_profile()
        return [z for p, z in prof if abs(p - jp.COLLAR_PROUD) < 1e-9]

    def test_the_frames_of_the_upper_tier_stand_in_the_top_of_the_string_course(self):
        self.assertGreater(plan.UPPER_TIER_Z0, jp.STRING_Z1 - 0.1)
        self.assertLess(plan.UPPER_TIER_Z0, jp.STRING_Z1)

    def test_the_upper_collar_has_no_flat_ledge_and_dies_into_the_wall_at_both_ends(self):
        prof = plan.upper_collar_profile()
        for (p0, z0), (p1, z1) in zip(prof, prof[1:]):
            self.assertLess(z0, z1)
        self.assertLessEqual(prof[0][0], -0.05)
        self.assertLessEqual(prof[-1][0], -0.05)
        self.assertAlmostEqual(max(p for p, _ in prof), jp.COLLAR_PROUD, places=9)

    def test_the_upper_collar_stands_above_the_string_course(self):
        self.assertGreater(plan.upper_collar_profile()[0][1], jp.STRING_Z1)

    def test_the_foot_of_an_upper_rib_is_inside_the_flat_front_of_its_collar(self):
        front = self.collar_front()
        self.assertGreater(plan.UP_RIB[0], min(front))
        self.assertLess(plan.UP_RIB[0], max(front))
        self.assertLess(plan.UP_RIB[5], jp.COLLAR_PROUD)

    def test_an_upper_rib_stays_inside_the_collar_while_it_is_beside_it(self):
        top = plan.upper_collar_profile()[-1][1]
        spec = plan.UP_RIB
        for dx, z, tx, tz in jp.rib_curve(1, spec, 0.0, 400, half_bay=plan.drum_half_bay()):
            t = jp.rib_t(spec, z)
            hw = jp.rib_width(spec[3], spec[4], t) / 2.0
            n = math.hypot(tx, tz)
            ex, ez = -tz / n, tx / n
            if z - hw * ez <= top:
                self.assertLessEqual(dx - hw * ex, jp.COLLAR_HALF_W - 0.02, msg=z)
            if z + hw * ez <= top:
                self.assertGreaterEqual(dx + hw * ex, 0.04, msg=z)

    def test_the_lower_ribs_of_the_drum_stand_whole_on_their_collar(self):
        for spec in (jp.RIB_LONG, jp.RIB_LOW):
            for dx, z, tx, tz in jp.rib_curve(1, spec, 0.0, 400, half_bay=plan.drum_half_bay()):
                t = jp.rib_t(spec, z)
                hw = jp.rib_width(spec[3], spec[4], t) / 2.0
                n = math.hypot(tx, tz)
                ex, ez = -tz / n, tx / n
                if z - hw * ez <= jp.COLLAR_Z1:
                    self.assertLessEqual(dx - hw * ex, jp.COLLAR_HALF_W - 0.02, msg=(spec, z))

    def test_the_drum_half_bay_is_half_the_arc_between_two_piers(self):
        a = plan.PIER_ANGLES_B
        self.assertAlmostEqual(plan.drum_half_bay(), math.radians((a[1] - a[0]) / 2.0) * plan.DRUM_R, places=9)

    def test_the_upper_rib_ends_under_the_cornice(self):
        self.assertLess(plan.UP_RIB[1] + plan.UP_RIB[3] / 2.0, 15.95)


if __name__ == "__main__":
    unittest.main()
