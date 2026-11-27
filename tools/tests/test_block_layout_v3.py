"""Tests for the round 2b additions of tools/painter/block_layout.py: a layout redrawn at twice the size,
weathering maps (streaks under the bed joints, a darker lower edge), and a rougher edge radius."""
import os
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "painter"))
import block_layout as bl  # noqa: E402

SIZE = 256
JOINT = 4
PPM = 128.0  # 1 px = 7.8 mm


def blocks_a():
    return bl.plan_blocks((64, 64, 64, 64), (64, 128), SIZE, 5, 16)


def weather(blocks, **kw):
    args = dict(joint=JOINT, ppm=PPM, seed=3)
    args.update(kw)
    return bl.weathering(blocks, SIZE, **args)


class ScaleTests(unittest.TestCase):
    def test_scale_blocks_doubles_every_coordinate_and_keeps_ids(self):
        b = blocks_a()
        big = bl.scale_blocks(b, 2)
        for s, t in zip(b, big):
            self.assertEqual((t["x0"], t["x1"], t["y0"], t["y1"], t["id"]),
                             (2 * s["x0"], 2 * s["x1"], 2 * s["y0"], 2 * s["y1"], s["id"]))

    def test_make_layout_takes_given_blocks_and_their_size(self):
        small, _, _ = bl.make_layout(461.0, 1024, courses_m=(0.625, 0.486), seed=23)
        big, maps, info = bl.make_layout(922.0, 2048, blocks=bl.scale_blocks(small, 2), seed=23, joint_mm=11,
                                         radius_mm=17)
        self.assertEqual(maps["height"].shape, (2048, 2048))
        self.assertEqual(info["joint_px"], 10)
        self.assertEqual(info["radius_px"], 16)
        self.assertEqual(len(big), len(small))
        self.assertEqual(sum(info["courses_px"]), 2048)

    def test_same_blocks_give_the_same_tone_per_block(self):
        small, m1, _ = bl.make_layout(461.0, 1024, courses_m=(0.625, 0.486), seed=23)
        big, m2, _ = bl.make_layout(922.0, 2048, blocks=bl.scale_blocks(small, 2), seed=23)
        self.assertEqual(m1["info"]["tones"], m2["info"]["tones"])


class StreakTests(unittest.TestCase):
    def setUp(self):
        self.blocks = blocks_a()
        self.m = weather(self.blocks)

    def face_pixels(self, b):
        cols = [x % SIZE for x in range(b["x0"], b["x1"])]
        return cols

    def test_maps_exist_in_range_and_zero_in_joints(self):
        full = bl.render_v2(self.blocks, SIZE, joint=JOINT, radius=5, seed=3, radius_jitter=0.0)
        for k in ("streak", "lower"):
            a = self.m[k]
            self.assertEqual(a.shape, (SIZE, SIZE))
            self.assertGreaterEqual(float(a.min()), 0.0)
            self.assertLessEqual(float(a.max()), 1.0)
            self.assertEqual(float(a[full["height"] == 0.0].max()), 0.0, k)
            self.assertGreater(float(a.max()), 0.3, k)

    def test_streaks_start_under_the_bed_joint_and_fade_downward(self):
        for b in self.blocks:
            cols = self.face_pixels(b)
            top = self.m["streak"][b["y0"] + JOINT:b["y0"] + JOINT + 6][:, cols].mean()
            low = self.m["streak"][b["y1"] - 12:b["y1"] - JOINT][:, cols].mean()
            self.assertGreater(float(top), float(low), b["id"])

    def test_streak_is_gone_beyond_the_longest_fade_length(self):
        far = int(0.150 * PPM) + JOINT + 2
        for b in self.blocks:
            if b["y1"] - b["y0"] - JOINT <= far + 2:
                continue
            cols = self.face_pixels(b)
            self.assertEqual(float(self.m["streak"][b["y0"] + far:b["y1"] - JOINT][:, cols].max()), 0.0)

    def test_streaks_do_not_cross_a_joint_into_the_block_above(self):
        for b in self.blocks:
            cols = self.face_pixels(b)
            above = self.m["streak"][(b["y0"] - JOINT - 1) % SIZE, cols]
            below = self.m["streak"][b["y0"] + JOINT + 1, cols]
            self.assertLessEqual(float(above.max()), float(below.max()) + 1e-9)

    def test_streaks_are_vertical_not_blotches(self):
        c = self.m["streak"]
        down = np.abs(np.diff(c, axis=0))[JOINT + 2:60].mean()
        across = np.abs(np.diff(c, axis=1))[JOINT + 2:60].mean()
        self.assertLess(float(down), float(across))

    def test_streak_is_sparse(self):
        face = self.m["streak"] > 0.05
        self.assertLess(float(face.mean()), 0.35)
        self.assertGreater(float(face.mean()), 0.02)

    def test_wraps_horizontally(self):
        k = 37
        shifted = [dict(b, x0=b["x0"] + k, x1=b["x1"] + k) for b in self.blocks]
        a = weather(self.blocks)
        s = weather(shifted)
        for key in ("streak", "lower"):
            self.assertTrue(np.allclose(np.roll(a[key], k, axis=1), s[key], atol=1e-9), key)


class LowerEdgeTests(unittest.TestCase):
    def test_darker_band_hugs_the_lower_edge_of_each_block(self):
        blocks = blocks_a()
        m = weather(blocks)
        for b in blocks:
            cols = [x % SIZE for x in range(b["x0"] + 8, b["x1"] - 8)]
            near = m["lower"][b["y1"] - JOINT - 4:b["y1"] - JOINT][:, cols].mean()
            mid = m["lower"][b["y0"] + 30:b["y0"] + 34][:, cols].mean()
            self.assertGreater(float(near), 0.15)
            self.assertGreater(float(near), 3 * float(mid) + 0.05)

    def test_band_width_is_about_the_asked_millimetres(self):
        blocks = blocks_a()
        m = weather(blocks, lower_mm=50.0)
        b = blocks[0]
        cols = [x % SIZE for x in range(b["x0"] + 8, b["x1"] - 8)]
        prof = m["lower"][:, cols].mean(axis=1)
        below = b["y1"] - JOINT - 1
        width = int(0.05 * PPM)
        self.assertGreater(float(prof[below]), 0.1)
        self.assertLess(float(prof[below - 2 * width]), 0.02)


class RoughEdgeTests(unittest.TestCase):
    def test_radius_detail_makes_the_edge_radius_vary_more_along_the_edge(self):
        blocks = bl.plan_blocks((64,) * 4, (128,), 256, 3, 16)
        calm = bl.render_v2(blocks, 256, joint=3, radius=8, seed=4, radius_jitter=0.3, chip_frac=0.0)
        rough = bl.render_v2(blocks, 256, joint=3, radius=8, seed=4, radius_jitter=0.5, radius_detail=0.6,
                             chip_frac=0.0)
        b = blocks[0]
        x = (b["x0"] + 64) % 256

        def width_along(maps):
            col = maps["height"][b["y0"] + 2:b["y0"] + 30, x]
            return float((col < 0.95).sum())

        def spread(maps):
            vals = []
            for dx in range(-28, 28):
                col = maps["height"][b["y0"] + 2:b["y0"] + 30, (b["x0"] + 64 + dx) % 256]
                vals.append(float((col < 0.95).sum()))
            return float(np.std(vals))

        self.assertGreater(spread(rough), spread(calm))
        self.assertGreater(width_along(rough), 0.0)

    def test_default_radius_detail_changes_nothing(self):
        blocks = bl.plan_blocks((64,) * 4, (128,), 256, 3, 16)
        a = bl.render_v2(blocks, 256, joint=3, radius=8, seed=4)
        b = bl.render_v2(blocks, 256, joint=3, radius=8, seed=4, radius_detail=0.0)
        self.assertTrue(np.array_equal(a["height"], b["height"]))


if __name__ == "__main__":
    unittest.main()
