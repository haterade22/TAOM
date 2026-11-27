"""Tests for round 3 of the Rivendell stone: the measures that judge a smooth dressed stone (face tilt, course
and band means, edge straightness) in tile_compose.py, and the straight crisp edge, the balanced tone per
block and the side-dependent edge light in block_layout.py."""
import os
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "painter"))
import block_layout as bl  # noqa: E402
import tile_compose as tc  # noqa: E402

SIZE = 256
HEIGHTS = (80, 48, 80, 48)


def blocks_a(seed=5):
    return bl.plan_blocks(HEIGHTS, (64, 96, 128), SIZE, seed, 16)


def maps_a(**kw):
    args = dict(joint=4, radius=8, seed=3, radius_jitter=0.0, chip_frac=0.0, px_scale=0.5)
    args.update(kw)
    return bl.render_v2(blocks_a(), SIZE, **args)


class MeasureTests(unittest.TestCase):
    def test_inner_mask_keeps_pixels_away_from_every_edge(self):
        blocks = [{"x0": 0, "x1": 8, "y0": 0, "y1": 8, "id": 0}, {"x0": 8, "x1": 16, "y0": 0, "y1": 8, "id": 1},
                  {"x0": 0, "x1": 16, "y0": 8, "y1": 16, "id": 2}]
        m = tc.inner_mask(blocks, 16, 16, 2)
        self.assertTrue(m[3:5, 3:5].all())
        self.assertFalse(m[0, 0] or m[7, 7] or m[3, 0] or m[3, 7] or m[3, 8])
        self.assertEqual(int(m[:8, :8].sum()), 16)

    def test_inner_mask_wraps_a_block_over_the_tile_edge(self):
        blocks = [{"x0": 12, "x1": 20, "y0": 0, "y1": 16, "id": 0}, {"x0": 4, "x1": 12, "y0": 0, "y1": 16, "id": 1}]
        m = tc.inner_mask(blocks, 16, 16, 1)
        self.assertTrue(m[8, 15] and m[8, 0])
        self.assertFalse(m[8, 4])

    def test_inner_mask_scales_to_the_output_size(self):
        blocks = [{"x0": 0, "x1": 16, "y0": 0, "y1": 16, "id": 0}]
        m = tc.inner_mask(blocks, 16, 8, 1)
        self.assertEqual(m.shape, (8, 8))
        self.assertEqual(int(m.sum()), 36)

    def test_face_tilt_ignores_the_edges(self):
        blocks = [{"x0": 0, "x1": 32, "y0": 0, "y1": 32, "id": 0}]
        n = np.dstack([np.full((32, 32), 127.5), np.full((32, 32), 127.5), np.full((32, 32), 255.0)])
        n[0:3, :, 0] = 255.0
        t = tc.face_tilt(n, blocks, 32, 4)
        self.assertAlmostEqual(t, 0.0, places=2)
        self.assertGreater(tc.mean_tilt(n), 0.01)

    def test_course_means_weighs_each_course_band_by_its_own_rows(self):
        blocks = [{"x0": 0, "x1": 8, "y0": 0, "y1": 4, "id": 0}, {"x0": 0, "x1": 8, "y0": 4, "y1": 8, "id": 1}]
        lum = np.zeros((8, 8))
        lum[:4] = 180.0
        lum[4:] = 190.0
        self.assertEqual(tc.course_means(lum, blocks, 8), [180.0, 190.0])

    def test_course_means_scale_to_a_smaller_output(self):
        blocks = [{"x0": 0, "x1": 16, "y0": 0, "y1": 8, "id": 0}, {"x0": 0, "x1": 16, "y0": 8, "y1": 16, "id": 1}]
        lum = np.zeros((8, 8))
        lum[:4] = 100.0
        lum[4:] = 110.0
        self.assertEqual(tc.course_means(lum, blocks, 16), [100.0, 110.0])

    def test_band_means_split_the_columns_in_equal_parts(self):
        lum = np.zeros((4, 8))
        lum[:, :2], lum[:, 2:4], lum[:, 4:6], lum[:, 6:] = 10.0, 20.0, 30.0, 40.0
        self.assertEqual(tc.band_means(lum, 4), [10.0, 20.0, 30.0, 40.0])

    def test_spread_is_the_largest_minus_the_smallest(self):
        self.assertEqual(tc.spread([180.0, 191.5, 187.0]), 11.5)


class EdgeStraightnessTests(unittest.TestCase):
    def test_a_plain_bevel_is_straight_to_a_fraction_of_a_pixel(self):
        m = maps_a()
        r = tc.edge_straightness(m["height"], blocks_a())
        self.assertLess(r["max_dev_px"], 0.1)
        self.assertEqual(r["bad_edges"], 0)
        self.assertEqual(r["edges"], 4 * len(blocks_a()))

    def test_a_jittered_radius_is_not_straight(self):
        m = maps_a(radius_jitter=0.55, radius_detail=0.7)
        r = tc.edge_straightness(m["height"], blocks_a())
        self.assertGreater(r["max_dev_px"], 0.5)

    def test_a_chip_breaks_its_block_and_only_its_block(self):
        m = maps_a(chip_frac=0.2, chip_px=(6, 8), chip_depth=0.9)
        chipped = {c["block"] for c in m["info"]["chips"]}
        self.assertTrue(chipped)
        r = tc.edge_straightness(m["height"], blocks_a())
        r2 = tc.edge_straightness(m["height"], blocks_a(), skip=chipped)
        self.assertGreaterEqual(r["bad_edges"], 1)
        self.assertEqual(r2["bad_edges"], 0)


class BevelTests(unittest.TestCase):
    def test_round_profile_is_the_default_and_a_chamfer_is_linear(self):
        rnd = maps_a()["height"]
        cham = maps_a(profile="chamfer")["height"]
        b = blocks_a()[0]
        y = (b["y0"] + b["y1"]) // 2
        x0 = b["x0"] % SIZE
        row_r = np.array([rnd[y, (x0 + 2 + i) % SIZE] for i in range(10)])
        row_c = np.array([cham[y, (x0 + 2 + i) % SIZE] for i in range(10)])
        self.assertEqual(row_r.max(), 1.0)
        self.assertEqual(row_c.max(), 1.0)
        steps = np.diff(row_c[:8])
        self.assertTrue(np.allclose(steps, steps[0], atol=1e-6))
        self.assertGreater(row_r[1] - row_r[0], row_r[5] - row_r[4])

    def test_the_bevel_is_as_wide_as_the_radius_and_the_joint_is_flat_zero(self):
        h = maps_a()["height"]
        b = blocks_a()[0]
        y = (b["y0"] + b["y1"]) // 2
        x0 = b["x0"] % SIZE
        self.assertEqual(h[y, x0], 0.0)
        self.assertEqual(h[y, (x0 + 1) % SIZE], 0.0)
        self.assertLess(h[y, (x0 + 4) % SIZE], 1.0)
        self.assertEqual(h[y, (x0 + 2 + 8) % SIZE], 1.0)

    def test_a_shallow_chip_cuts_the_height_by_chip_depth_at_most(self):
        whole = maps_a(chip_frac=0.2, chip_px=(6, 8), chip_depth=0.0)["height"]
        m = maps_a(chip_frac=0.2, chip_px=(6, 8), chip_depth=0.25)
        self.assertTrue((m["height"] >= 0.75 * whole - 1e-9).all())
        self.assertLess(float(m["height"][m["chip"] > 0.99].mean()), float(whole[m["chip"] > 0.99].mean()))


class EdgeLightTests(unittest.TestCase):
    def line(self, m, side):
        """The edge-light values along one side of block 0, in the middle of the edge."""
        b = blocks_a()[0]
        cy, x0, x1, y0, y1 = (b["y0"] + b["y1"]) // 2, b["x0"], b["x1"], b["y0"], b["y1"]
        cx = (x0 + x1) // 2
        e = m["edgelight"]
        if side == "top":
            return e[y0:y0 + 14, cx % SIZE]
        if side == "bottom":
            return e[y1 - 14:y1, cx % SIZE]
        if side == "left":
            return np.array([e[cy, (x0 + i) % SIZE] for i in range(14)])
        return np.array([e[cy, (x1 - 14 + i) % SIZE] for i in range(14)])

    def test_the_top_edge_is_lighter_than_the_other_three(self):
        m = maps_a(edge_gain=(1.0, 0.3, 0.2, 0.3), line_px=4.0)
        top = self.line(m, "top").max()
        for side in ("right", "bottom", "left"):
            self.assertLess(self.line(m, side).max(), 0.5 * top)
        self.assertGreater(self.line(m, "right").max(), 0.1)

    def test_the_line_is_continuous_along_the_edge(self):
        m = maps_a(edge_gain=(1.0, 0.3, 0.2, 0.3), line_px=4.0)
        b = blocks_a()[0]
        x0 = b["x0"] + 12
        x1 = b["x1"] - 12
        peak = np.array([m["edgelight"][b["y0"]:b["y0"] + 14, x % SIZE].max() for x in range(x0, x1)])
        self.assertLess(peak.max() - peak.min(), 0.02)
        self.assertGreater(peak.min(), 0.9)

    def test_the_line_is_about_line_px_wide(self):
        m = maps_a(edge_gain=(1.0, 1.0, 1.0, 1.0), line_px=4.0)
        b = blocks_a()[0]
        col = m["edgelight"][b["y0"]:b["y0"] + 20, ((b["x0"] + b["x1"]) // 2) % SIZE]
        width = float((col >= 0.5 * col.max()).sum())
        self.assertGreaterEqual(width, 3.0)
        self.assertLessEqual(width, 5.0)

    def test_the_line_is_zero_in_the_joint(self):
        m = maps_a(edge_gain=(1.0, 1.0, 1.0, 1.0), line_px=4.0)
        b = blocks_a()[0]
        self.assertLess(m["edgelight"][b["y0"], ((b["x0"] + b["x1"]) // 2) % SIZE], 0.01)


class BalancedToneTests(unittest.TestCase):
    def setUp(self):
        self.blocks = blocks_a()
        bid = np.zeros((SIZE, SIZE), dtype=int)
        yy, xx = np.mgrid[0:SIZE, 0:SIZE]
        for b in self.blocks:
            sel = (yy >= b["y0"]) & (yy < b["y1"]) & (((xx - b["x0"]) % SIZE) < b["x1"] - b["x0"])
            bid[sel] = b["id"]
        self.pairs = bl._neighbour_pairs(bid)

    def tones(self, seed=1, **kw):
        return bl.balanced_tones(self.blocks, self.pairs, SIZE, np.random.default_rng(seed), **kw)

    def test_every_course_has_a_zero_mean_weighted_by_block_length(self):
        for seed in range(6):
            z, _ = self.tones(seed)
            rows = {}
            for b in self.blocks:
                rows.setdefault((b["y0"], b["y1"]), []).append(b)
            for bs in rows.values():
                num = sum(z[b["id"]] * (b["x1"] - b["x0"]) for b in bs)
                self.assertAlmostEqual(num, 0.0, places=6)

    def test_tones_stay_in_minus_one_to_one(self):
        z, _ = self.tones(2)
        self.assertLessEqual(max(abs(v) for v in z.values()), 1.0 + 1e-9)

    def test_no_two_touching_blocks_share_an_extreme_tone(self):
        for seed in range(6):
            z, info = self.tones(seed, extreme=0.7)
            for a, b in self.pairs:
                self.assertFalse(abs(z[a]) >= 0.7 and abs(z[b]) >= 0.7 and np.sign(z[a]) == np.sign(z[b]))
            self.assertEqual(info["extreme_pairs"], 0)

    def test_the_tones_are_not_all_equal(self):
        z, _ = self.tones(3)
        self.assertGreater(np.std(list(z.values())), 0.3)

    def test_the_four_column_bands_have_nearly_equal_means(self):
        z, info = self.tones(4, tries=800)
        self.assertLess(info["band_spread"], 0.35)

    def test_render_puts_the_tone_in_the_id_map_around_one_half(self):
        m = maps_a(tone_amp=0.1)
        ids = m["id"]
        self.assertLessEqual(float(np.abs(ids - 0.5).max()), 0.1 + 1e-9)
        rows = {}
        for b in self.blocks:
            rows.setdefault((b["y0"], b["y1"]), []).append(b)
        for (y0, y1), bs in rows.items():
            band = ids[y0:y1]
            self.assertAlmostEqual(float(band.mean() - 0.5), 0.0, delta=0.004)

    def test_the_same_seed_gives_the_same_tones(self):
        a, _ = self.tones(7)
        b, _ = self.tones(7)
        self.assertEqual(a, b)


if __name__ == "__main__":
    unittest.main()
