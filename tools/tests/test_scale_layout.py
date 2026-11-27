"""Tests for tools/painter/scale_layout.py: the fish-scale slate layout of round 5.

The tile is small (256 px for 2.222 m) so a test runs in a second. Rows are numbered from the eave up. The
maps are in image order: the eave is at the bottom of the image, the rounded lower end of a scale points down.
"""
import os
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "painter"))
import scale_layout as sl  # noqa: E402

SIZE = 256
PPM = SIZE / 2.222
ROWS, COLS = 12, 10


def build(**kw):
    args = dict(size=SIZE, ppm=PPM, rows=ROWS, cols=COLS, seed=3)
    args.update(kw)
    return sl.build_maps(**args)


def lower_edge_pixel(owner):
    """(y, x) of the last image row of a scale on the column through the centre of an odd row's scale, where
    the next image row down belongs to a lower row (owner = row * cols + col, row 0 at the eave)."""
    size = owner.shape[0]
    x = int(round(3.0 * size / COLS))
    rows = owner[:, x] // COLS
    for y in range(size // 4, size - size // 4):
        if rows[y] == rows[y + 1] + 1 and rows[y] % 2 == 1:
            return y, x
    raise AssertionError("no lower edge on the column")


class ScaleShapeTests(unittest.TestCase):
    def test_the_visible_part_is_a_shield_with_a_round_lower_end(self):
        w = SIZE / COLS
        d = sl.shield_depth(w, aspect=1.0)
        self.assertAlmostEqual(d, w / 2.0, places=6)
        half = sl.shield_half_width(np.array([0.0, d / 2.0, d, d + 5.0]), w / 2.0, d)
        self.assertAlmostEqual(float(half[0]), 0.0, places=6)
        self.assertAlmostEqual(float(half[2]), w / 2.0, places=6)
        self.assertAlmostEqual(float(half[3]), w / 2.0, places=6)
        self.assertAlmostEqual(float(half[1]), w / 2.0 * np.sqrt(0.75), places=6)

    def test_an_aspect_above_one_makes_a_longer_point(self):
        w = SIZE / COLS
        self.assertGreater(sl.shield_depth(w, aspect=1.3), sl.shield_depth(w, aspect=1.0))


class ScaleLayoutTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.m = build()
        cls.info = cls.m["info"]

    def test_every_scale_of_the_tile_is_visible(self):
        owner = self.m["owner"]
        self.assertGreaterEqual(len(np.unique(owner)), int(ROWS * COLS * 0.98))
        self.assertEqual(self.info["scale_count"], ROWS * COLS)

    def test_the_maps_have_the_size_of_the_tile(self):
        for key in ("id", "height", "gap", "ao", "edgelight", "owner"):
            self.assertEqual(self.m[key].shape, (SIZE, SIZE), key)

    def test_the_step_at_a_lower_edge_is_the_edge_height_asked_for(self):
        m = build(edge_mm=7.5, unit_mm=22.0)
        h = m["height"] * 22.0
        # a column a quarter of a width off the centre of an even row's scale: no side groove of a lower row on it
        w = SIZE / COLS
        x = int(round(3.25 * w))
        owner = m["owner"][:, x]
        steps = []
        for y in range(1, SIZE - 1):
            below, here = owner[y + 1], owner[y]
            if here != below:
                # the image row y + 1 is lower on the roof than y when the tip of a scale points down
                steps.append(h[y, x] - h[y + 1, x])
        # the edge between a scale and the row under it: the upper scale is higher by about edge_mm
        good = [s for s in steps if s > 3.0]
        self.assertTrue(good)
        self.assertLess(abs(float(np.median(good)) - 7.5), 1.0)

    def test_the_lower_end_points_to_the_bottom_of_the_image(self):
        # in the column through a scale's centre the first image row of a scale (from above) is its covered top
        # end and the last one (the lowest) is its tip: the id changes to the row below one pitch lower
        w = SIZE / COLS
        x = int(round(3.5 * w))
        owner = self.m["owner"][:, x]
        runs = []
        start = 0
        for y in range(1, SIZE + 1):
            if y == SIZE or owner[y] != owner[start]:
                runs.append((start, y - 1))
                start = y
        pitch = SIZE / ROWS
        # a run on the centre line is a scale lower end up to the next row's lower end: about 0.296 m long
        long_runs = [b - a + 1 for a, b in runs if b - a + 1 > 0.5 * pitch]
        self.assertTrue(long_runs)
        self.assertGreater(max(long_runs), 1.3 * pitch)

    def test_rows_are_staggered_by_half_a_scale(self):
        size = 1024
        m = build(size=size, ppm=size / 2.222)
        gap = m["gap"]
        w = size / COLS
        pitch = size / ROWS
        y_up = int(round(0.15 * size / 2.222))
        phases = []
        for k in (4, 5):
            y_image = int(round(size - (k * pitch + y_up) - 1))
            xs = np.nonzero(gap[y_image] > 0.5)[0]
            self.assertGreaterEqual(len(xs), COLS)
            phases.append(np.angle(np.exp(2j * np.pi * xs / w).mean()))
        diff = abs(phases[1] - phases[0]) % (2 * np.pi)
        diff = min(diff, 2 * np.pi - diff)
        self.assertLess(abs(diff - np.pi), 0.08 * 2 * np.pi)

    def test_the_gap_between_neighbours_is_two_to_three_millimetres(self):
        m = build(size=1024, ppm=1024 / 2.222, gap_mm=2.5)
        pitch = 1024 / ROWS
        y_up = int(round(0.15 * 1024 / 2.222))
        y_image = int(round(1024 - (4 * pitch + y_up) - 1))
        line = m["gap"][y_image]
        runs = 0
        total = 0.0
        prev = 0.0
        for v in line:
            if v > 0.5 and prev <= 0.5:
                runs += 1
            prev = v
        total = float(line.sum())
        width_mm = total / runs * (2.222 / 1024 * 1000.0)
        self.assertTrue(2.0 <= width_mm <= 3.2, width_mm)

    def test_the_contact_shade_lies_on_the_lower_scale_not_on_the_upper_one(self):
        m = build(size=1024, ppm=1024 / 2.222, shade_mm=15.0, shade_strength=0.3)
        ao = m["ao"]
        owner = m["owner"]
        y, x = lower_edge_pixel(owner)
        upper = ao[y - 2, x]
        lower = ao[y + 1, x]
        self.assertGreater(upper, 0.97)
        self.assertLess(lower, 0.85)
        self.assertGreater(ao.min(), 0.6)

    def test_the_shade_is_about_the_width_asked_for(self):
        m = build(size=1024, ppm=1024 / 2.222, shade_mm=15.0, shade_strength=0.3)
        ao = m["ao"]
        owner = m["owner"]
        y, x = lower_edge_pixel(owner)
        run = 0
        while y + 1 + run < 1024 and ao[y + 1 + run, x] < 0.99 and run < 60:
            run += 1
        mm = run * 2.222 / 1024 * 1000.0
        self.assertTrue(8.0 <= mm <= 30.0, mm)

    def test_the_tone_per_scale_stays_close_and_a_few_scales_are_outliers(self):
        ids = self.m["id"]
        owner = self.m["owner"]
        vals = np.array([float(ids[owner == o].mean()) for o in np.unique(owner)])
        self.assertLess(np.abs(vals - 0.5).max(), 0.5 * 0.35 + 1e-3)
        self.assertEqual(self.info["outlier_count"], round(ROWS * COLS / 12))
        flagged = {int(o) for o in np.unique(owner[(self.m["outdark"] + self.m["outlight"]) > 0.5])}
        self.assertEqual(flagged, set(self.info["outlier_ids"]))
        dark = {int(o) for o in np.unique(owner[self.m["outdark"] > 0.5])}
        light = {int(o) for o in np.unique(owner[self.m["outlight"] > 0.5])}
        self.assertFalse(dark & light)

    def test_the_layout_wraps_without_a_seam(self):
        # the wrap row pair is a pair of the pattern like any other: no pair of rows (or columns) it has is
        # farther apart than the largest pair inside the tile
        h = self.m["height"]
        inner = np.abs(np.diff(h, axis=0)).mean(axis=1).max()
        self.assertLessEqual(float(np.abs(h[0] - h[-1]).mean()), inner * 1.05)
        inner_x = np.abs(np.diff(h, axis=1)).mean(axis=0).max()
        self.assertLessEqual(float(np.abs(h[:, 0] - h[:, -1]).mean()), inner_x * 1.05)

    def test_it_is_deterministic_and_the_seed_changes_the_tones(self):
        a, b, c = build(seed=3), build(seed=3), build(seed=4)
        self.assertTrue(np.array_equal(a["id"], b["id"]))
        self.assertFalse(np.array_equal(a["id"], c["id"]))
        self.assertTrue(np.array_equal(a["height"], c["height"]))


class CoverTests(unittest.TestCase):
    def test_the_cover_map_is_one_on_the_tile_border_and_zero_in_the_middle(self):
        c = sl.border_cover(256, band_px=12.0, feather_px=6.0)
        self.assertEqual(c.shape, (256, 256))
        self.assertAlmostEqual(float(c[0, 100]), 1.0, places=6)
        self.assertAlmostEqual(float(c[100, 255]), 1.0, places=6)
        self.assertAlmostEqual(float(c[128, 128]), 0.0, places=6)
        self.assertGreater(float(c[10, 100]), 0.9)
        self.assertLess(float(c[30, 100]), 0.01)

    def test_the_cover_wraps(self):
        c = sl.border_cover(256, band_px=12.0, feather_px=6.0)
        self.assertTrue(np.allclose(c[1, :], c[-1, :]))
        self.assertTrue(np.allclose(c[:, 1], c[:, -1]))


class JointCoverTests(unittest.TestCase):
    def test_holes_sit_where_the_shifted_copy_has_its_own_joint(self):
        b, c = sl.joint_cover_maps(256, band_px=12.0, feather_px=6.0, hole_px=30.0, hole_feather_px=12.0)
        # the shifted copy has its joint along the middle lines: B is off and C is on where the border band
        # meets the middle line
        self.assertAlmostEqual(float(b[128, 1]), 0.0, places=6)
        self.assertAlmostEqual(float(c[128, 1]), 1.0, places=6)
        self.assertAlmostEqual(float(b[1, 128]), 0.0, places=6)
        self.assertAlmostEqual(float(c[1, 128]), 1.0, places=6)
        # elsewhere on the border B is on and C is off
        self.assertAlmostEqual(float(b[1, 40]), 1.0, places=6)
        self.assertAlmostEqual(float(c[1, 40]), 0.0, places=6)
        self.assertAlmostEqual(float(b[128, 128]), 0.0, places=6)

    def test_c_never_covers_the_corner_of_the_tile_or_the_middle(self):
        b, c = sl.joint_cover_maps(256, band_px=12.0, feather_px=6.0, hole_px=30.0, hole_feather_px=12.0)
        self.assertEqual(float(c[0, 0]), 0.0)
        self.assertEqual(float(c[128, 128]), 0.0)

    def test_both_maps_wrap(self):
        b, c = sl.joint_cover_maps(256, band_px=12.0, feather_px=6.0, hole_px=30.0, hole_feather_px=12.0)
        for m in (b, c):
            self.assertTrue(np.allclose(m[0, :], m[-1, :]))
            self.assertTrue(np.allclose(m[:, 0], m[:, -1]))


class MeasureTests(unittest.TestCase):
    def test_the_tone_measures_read_calm_spread_and_outlier_offset(self):
        m = build(size=512, ppm=512 / 2.222)
        owner = m["owner"]
        lum = np.zeros(owner.shape)
        for o in np.unique(owner):
            lum[owner == o] = 100.0 + 10.0 * (m["info"]["tone_per_scale"][int(o)] - 0.5) * 2.0
        for o, dark in zip(m["info"]["outlier_ids"], m["info"]["outlier_dark"]):
            lum[owner == o] += -12.0 if dark else 12.0
        r = sl.measure_scales(lum, owner, m["info"]["outlier_ids"], m["ao"], margin_px=2)
        self.assertLess(r["calm_spread"], 10.0 * 2 * 0.36 * 1.0 + 1e-6)
        self.assertLess(r["calm_spread"], r["outlier_min"])
        self.assertGreater(r["outlier_min"], 8.0)
        self.assertEqual(r["outliers"], len(m["info"]["outlier_ids"]))

    def test_the_shade_levels_are_the_drop_in_the_darkest_part_of_the_shade_zone(self):
        m = build(size=512, ppm=512 / 2.222, shade_strength=0.3)
        lum = 100.0 * m["ao"]
        r = sl.measure_scales(lum, m["owner"], m["info"]["outlier_ids"], m["ao"], margin_px=2)
        self.assertTrue(10.0 < r["shade_levels"] < 31.0, r["shade_levels"])


class WriteTests(unittest.TestCase):
    def test_the_files_are_written_with_the_names_the_recipes_read(self):
        import tempfile
        m = build()
        with tempfile.TemporaryDirectory() as d:
            files = sl.write_maps(m, d)
            for key in ("id", "height", "gap", "ao", "edgelight"):
                self.assertTrue(os.path.exists(os.path.join(d, "layout_%s.png" % key)), key)
            self.assertTrue(os.path.exists(os.path.join(d, "layout_owner.png")))
            self.assertTrue(os.path.exists(os.path.join(d, "layout.json")))
            self.assertIn("height", files)


if __name__ == "__main__":
    unittest.main()
