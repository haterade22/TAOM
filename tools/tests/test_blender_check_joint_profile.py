#!/usr/bin/env python3
"""Unit tests for the pure parts of tools/oneoff/blender_check_joint_profile.py (no Blender needed:
bpy is imported only inside the Blender-side functions).

Run:  python -m unittest tools.tests.test_blender_check_joint_profile
"""
import importlib.util
import os
import tempfile
import unittest
import zlib

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "oneoff", "blender_check_joint_profile.py")

_spec = importlib.util.spec_from_file_location("blender_check_joint_profile", SCRIPT)
cj = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cj)

CELL = 0.02


def rect(y0, z0, y1, z1):
    """The four segments of a rectangle outline, as (y1, z1, y2, z2)."""
    return [(y0, z0, y1, z0), (y1, z0, y1, z1), (y1, z1, y0, z1), (y0, z1, y0, z0)]


def grid(y1=1.0, z1=1.0):
    return cj.make_grid(0.0, y1, 0.0, z1, cell=CELL, pad=0.0)


class FillTests(unittest.TestCase):
    def test_rectangle_area(self):
        g = grid()
        rows = cj.fill_outline(rect(0.2, 0.2, 0.6, 0.8), g)
        self.assertAlmostEqual(cj.mask_area(rows, CELL), 0.4 * 0.6, places=6)

    def test_hole_by_even_odd(self):
        g = grid()
        rows = cj.fill_outline(rect(0.2, 0.2, 0.8, 0.8) + rect(0.4, 0.4, 0.6, 0.6), g)
        self.assertAlmostEqual(cj.mask_area(rows, CELL), 0.36 - 0.04, places=6)

    def test_triangle_area_within_a_cell_row(self):
        g = grid()
        tri = [(0.0, 0.0, 1.0, 0.0), (1.0, 0.0, 0.0, 1.0), (0.0, 1.0, 0.0, 0.0)]
        self.assertAlmostEqual(cj.mask_area(cj.fill_outline(tri, g), CELL), 0.5, delta=0.03)

    def test_overlapping_components_are_a_union_not_a_xor(self):
        g = grid()
        a = rect(0.1, 0.1, 0.6, 0.6)
        b = rect(0.4, 0.4, 0.9, 0.9)
        union = cj.fill_components([a, b], g)
        self.assertAlmostEqual(cj.mask_area(union, CELL), 0.25 + 0.25 - 0.04, places=6)
        # one outline holding both loops would XOR the overlap away
        self.assertAlmostEqual(cj.mask_area(cj.fill_outline(a + b, g), CELL), 0.25 + 0.25 - 0.08, places=6)


class ComponentTests(unittest.TestCase):
    def test_two_loops_are_two_components(self):
        segs = rect(0, 0, 1, 1) + rect(5, 5, 6, 6)
        comps = cj.connected_components(segs)
        self.assertEqual(sorted(len(c) for c in comps), [4, 4])

    def test_loops_that_share_a_corner_are_one_component(self):
        comps = cj.connected_components(rect(0, 0, 1, 1) + rect(1, 1, 2, 2))
        self.assertEqual(len(comps), 1)

    def test_open_nodes_counts_dangling_ends(self):
        self.assertEqual(cj.open_nodes(rect(0, 0, 1, 1)), 0)
        self.assertEqual(cj.open_nodes(rect(0, 0, 1, 1)[:3]), 2)


class CompareTests(unittest.TestCase):
    def test_identical_masks_have_no_mismatch(self):
        g = grid()
        m = cj.fill_outline(rect(0.2, 0.2, 0.6, 0.8), g)
        r = cj.compare_masks(m, list(m), CELL)
        self.assertEqual(r["mismatch"], 0.0)
        self.assertAlmostEqual(r["both"], 0.24, places=6)

    def test_only_a_only_b_and_sum(self):
        g = grid()
        a = cj.fill_outline(rect(0.2, 0.2, 0.6, 0.6), g)  # 0.16
        b = cj.fill_outline(rect(0.4, 0.2, 0.8, 0.6), g)  # 0.16, overlap 0.2 x 0.4 = 0.08
        r = cj.compare_masks(a, b, CELL)
        self.assertAlmostEqual(r["area_a"], 0.16, places=6)
        self.assertAlmostEqual(r["area_b"], 0.16, places=6)
        self.assertAlmostEqual(r["only_a"], 0.08, places=6)
        self.assertAlmostEqual(r["only_b"], 0.08, places=6)
        self.assertAlmostEqual(r["mismatch"], 0.16, places=6)

    def test_empty_against_full(self):
        g = grid()
        m = cj.fill_outline(rect(0.2, 0.2, 0.6, 0.6), g)
        r = cj.compare_masks([0] * len(m), m, CELL)
        self.assertEqual(r["only_a"], 0.0)
        self.assertAlmostEqual(r["only_b"], 0.16, places=6)


class FlatnessTests(unittest.TestCase):
    def test_all_on_plane(self):
        r = cj.flatness([10.0, 10.0, 9.5, 3.0], 10.0)
        self.assertEqual((r["near"], r["off"]), (2, 0))

    def test_a_vertex_near_but_off_the_plane_counts(self):
        r = cj.flatness([10.0, 9.995, 9.985, 9.9], 10.0)
        self.assertEqual((r["near"], r["off"]), (3, 2))
        self.assertAlmostEqual(r["worst"], 0.015, places=9)

    def test_tolerance_is_inclusive(self):
        self.assertEqual(cj.flatness([10.0, 9.9995, 9.999], 10.0)["off"], 0)

    def test_low_end(self):
        r = cj.flatness([-3.0, -3.0, -2.99, -2.5], -3.0)
        self.assertEqual((r["near"], r["off"]), (3, 1))

    def test_empty(self):
        self.assertEqual(cj.flatness([], 0.0)["near"], 0)


class PhaseTests(unittest.TestCase):
    def test_wraps_around_one(self):
        self.assertAlmostEqual(cj.phase_diff(0.999, 0.001), 0.002, places=9)

    def test_whole_tiles_do_not_count(self):
        self.assertAlmostEqual(cj.phase_diff(3.25, 7.25), 0.0, places=9)

    def test_half_tile(self):
        self.assertAlmostEqual(cj.phase_diff(0.0, 0.5), 0.5, places=9)

    def test_extrapolate_to_plane(self):
        # U grows 0.75 per metre: samples 0.01 and 0.03 m inside the end give the value at 0
        self.assertAlmostEqual(cj.extrapolate_to_plane(0.5 - 0.0075, 0.5 - 0.0225, 0.01, 0.03), 0.5, places=9)


def good_report():
    return {"mismatch_m2": 0.0, "flatness": {"a": {"off": 0}, "b": {"off": 0}},
            "section_open_nodes": {"a": 0, "b": 0},
            "open_share": {"a": 0.0, "b": 0.0},
            "uv_phase": {"checked": True, "max_u": 0.0, "max_v": 0.0, "faces": {
                "outer": {"checked": True, "max_u": 0.0, "max_v": 0.0, "points": 10, "skipped": 0},
                "inner": {"checked": True, "max_u": 0.0, "max_v": 0.0, "points": 10, "skipped": 0},
                "deck": {"checked": True, "max_u": 0.0, "max_v": 0.0, "points": 10, "skipped": 0}}},
            "vertex_colour": {"checked": True, "max_levels": 0.0, "points": 30}}


class VerdictTests(unittest.TestCase):
    def test_pass(self):
        v, reasons = cj.decide(good_report())
        self.assertEqual((v, reasons), ("PASS", []))

    def test_mismatch_limit_is_inclusive(self):
        r = good_report()
        r["mismatch_m2"] = cj.MISMATCH_LIMIT
        self.assertEqual(cj.decide(r)[0], "PASS")
        r["mismatch_m2"] = cj.MISMATCH_LIMIT + 0.0001
        self.assertEqual(cj.decide(r)[0], "FAIL")

    def test_each_failure_gives_a_reason(self):
        r = good_report()
        r["mismatch_m2"] = 3.0
        r["flatness"]["b"]["off"] = 4
        r["open_share"]["a"] = 0.1
        r["uv_phase"]["max_u"] = 0.2
        r["uv_phase"]["faces"]["outer"]["max_u"] = 0.2
        r["section_open_nodes"]["a"] = 6
        r["vertex_colour"]["max_levels"] = 9.0
        v, reasons = cj.decide(r)
        self.assertEqual(v, "FAIL")
        self.assertEqual(len(reasons), 6)

    def test_open_nodes_in_a_section_fail_the_joint(self):
        r = good_report()
        r["section_open_nodes"]["b"] = 2
        v, reasons = cj.decide(r)
        self.assertEqual(v, "FAIL")
        self.assertIn("open", reasons[0])

    def test_a_report_without_open_nodes_is_read_as_zero_so_old_reports_still_decide(self):
        r = good_report()
        del r["section_open_nodes"]
        self.assertEqual(cj.decide(r)[0], "PASS")

    def test_colour_may_differ_by_two_levels_not_three(self):
        r = good_report()
        r["vertex_colour"]["max_levels"] = 2.0
        self.assertEqual(cj.decide(r)[0], "PASS")
        r["vertex_colour"]["max_levels"] = 2.5
        self.assertEqual(cj.decide(r)[0], "FAIL")

    def test_a_uv_jump_on_the_inner_face_or_the_deck_fails_by_name(self):
        for face in ("inner", "deck"):
            r = good_report()
            r["uv_phase"]["faces"][face]["max_v"] = 0.3
            r["uv_phase"]["max_v"] = 0.3
            v, reasons = cj.decide(r)
            self.assertEqual(v, "FAIL")
            self.assertTrue(any(face in x for x in reasons), reasons)

    def test_unchecked_uv_does_not_fail(self):
        r = good_report()
        r["uv_phase"] = {"checked": False}
        self.assertEqual(cj.decide(r)[0], "PASS")

    def test_unchecked_colour_does_not_fail_but_is_listed_as_not_seen(self):
        r = good_report()
        r["vertex_colour"] = {"checked": False, "note": "a piece has no colour attribute"}
        self.assertEqual(cj.decide(r)[0], "PASS")
        self.assertTrue(any("colour" in x for x in cj.blind_spots(r)))


class SlitTests(unittest.TestCase):
    """A 1 cm slit 2 m long must fail."""

    @staticmethod
    def with_slit(y_slit):
        a = rect(0.0, 0.0, 1.0, 2.0)
        b = rect(0.0, 0.0, y_slit, 2.0) + rect(y_slit + 0.01, 0.0, 1.0, 2.0)
        return a, b

    def mismatch(self, cell, y_slit):
        g = cj.make_grid(0.0, 1.0, 0.0, 2.0, cell=cell, pad=0.0)
        a, b = self.with_slit(y_slit)
        return cj.compare_masks(cj.fill_components([a], g), cj.fill_components([b], g), cell)["mismatch"]

    def test_the_limit_is_below_the_area_of_the_slit(self):
        self.assertLess(cj.MISMATCH_LIMIT, 0.01 * 2.0)

    def test_the_fine_grid_sees_the_slit_wherever_it_falls(self):
        for k in range(0, 40):
            y = 0.3 + k * 0.00137
            m = self.mismatch(cj.FINE_CELL, y)
            self.assertGreater(m, cj.MISMATCH_LIMIT, y)

    def test_the_old_coarse_grid_could_miss_it(self):
        misses = [self.mismatch(0.02, 0.3 + k * 0.001) for k in range(40)]
        self.assertTrue(any(m == 0.0 for m in misses))

    def test_a_slit_of_that_size_fails_the_verdict(self):
        r = good_report()
        r["mismatch_m2"] = self.mismatch(cj.FINE_CELL, 0.5)
        self.assertEqual(cj.decide(r)[0], "FAIL")

    def test_identical_sections_have_no_mismatch_on_the_fine_grid(self):
        g = cj.make_grid(0.0, 1.0, 0.0, 2.0, cell=cj.FINE_CELL, pad=0.0)
        a = rect(0.0, 0.0, 1.0, 2.0)
        m = cj.compare_masks(cj.fill_components([a], g), cj.fill_components([list(a)], g), cj.FINE_CELL)
        self.assertEqual(m["mismatch"], 0.0)


class ColourTests(unittest.TestCase):
    def test_srgb_level_of_a_linear_value(self):
        self.assertAlmostEqual(cj.srgb_level(1.0), 255.0, places=6)
        self.assertAlmostEqual(cj.srgb_level(0.0), 0.0, places=6)
        self.assertAlmostEqual(cj.srgb_level(0.6038275361061096), 204.0, places=3)

    def test_difference_in_levels_is_the_largest_channel(self):
        self.assertAlmostEqual(cj.colour_levels_diff((204, 230, 255), (205, 227, 255)), 3.0)
        self.assertEqual(cj.colour_levels_diff((1, 2, 3), (1, 2, 3)), 0.0)

    def test_sample_positions_cover_the_range(self):
        ps = cj.sample_positions(0.0, 1.0, 0.25)
        self.assertEqual(ps, [0.125, 0.375, 0.625, 0.875])


class BlindSpotTests(unittest.TestCase):
    def test_the_static_list_names_what_the_tool_cannot_see(self):
        spots = cj.blind_spots(good_report())
        text = " ".join(spots)
        for word in ("inside a piece", "0.01 m", "niche", "Collision"):
            self.assertIn(word, text)

    def test_a_face_with_no_comparable_point_is_listed(self):
        r = good_report()
        r["uv_phase"]["faces"]["deck"] = {"checked": False, "note": "no points", "skipped": 12}
        self.assertTrue(any("deck" in x for x in cj.blind_spots(r)))

    def test_objects_without_a_colour_attribute_are_named(self):
        r = good_report()
        r["vertex_colour"]["without_colour"] = ["pavilion_l", "statue_r"]
        spots = cj.blind_spots(r)
        self.assertTrue(any("pavilion_l" in x and "2 objects" in x for x in spots), spots)

    def test_skipped_sample_points_are_counted(self):
        r = good_report()
        r["uv_phase"]["faces"]["outer"]["skipped"] = 40
        self.assertTrue(any("40" in x for x in cj.blind_spots(r)))


class PictureTests(unittest.TestCase):
    def test_colours_and_size(self):
        g = grid(0.4, 0.4)
        a = cj.fill_outline(rect(0.0, 0.0, 0.2, 0.4), g)
        b = cj.fill_outline(rect(0.1, 0.0, 0.4, 0.4), g)
        w, h, px = cj.diff_picture(a, b, g, px=1)
        self.assertEqual(len(px), w * h)
        found = set(px)
        self.assertIn(cj.GREY, found)
        self.assertIn(cj.RED, found)
        self.assertIn(cj.BLUE, found)

    def test_png_round_trip_header(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = os.path.join(tmp, "x.png")
            cj.write_png(p, 2, 1, [(255, 0, 0), (0, 0, 255)])
            with open(p, "rb") as f:
                data = f.read()
        self.assertEqual(data[:8], b"\x89PNG\r\n\x1a\n")
        idat = data[data.index(b"IDAT") + 4:data.index(b"IEND") - 8]
        self.assertEqual(zlib.decompress(idat), b"\x00\xff\x00\x00\x00\x00\xff")


if __name__ == "__main__":
    unittest.main()
