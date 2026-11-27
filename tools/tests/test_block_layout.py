"""Tests for tools/painter/block_layout.py: the block layout of a tiling ashlar wall, drawn by script
(block id, height with a bevel, edge mask, joint occlusion, a seamless stain map)."""
import os
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "painter"))
import block_layout as bl  # noqa: E402

SIZE = 256


def small_layout(seed=1, heights=(64, 64, 64, 64), lengths=(64, 128), stagger=16):
    return bl.plan_blocks(heights, lengths, SIZE, seed, stagger)


class PlanTests(unittest.TestCase):
    def test_courses_must_sum_to_the_size(self):
        with self.assertRaises(ValueError):
            bl.plan_blocks((64, 64), (64,), SIZE, 1, 8)

    def test_every_course_is_filled_exactly(self):
        blocks = small_layout()
        for y0 in {b["y0"] for b in blocks}:
            row = sorted((b for b in blocks if b["y0"] == y0), key=lambda b: b["x0"])
            self.assertEqual(sum(b["x1"] - b["x0"] for b in row), SIZE)
            for a, b in zip(row, row[1:]):
                self.assertEqual(a["x1"] % SIZE, b["x0"])
            self.assertEqual(row[-1]["x1"] % SIZE, row[0]["x0"])

    def test_block_lengths_come_from_the_choices(self):
        for b in small_layout():
            self.assertIn(b["x1"] - b["x0"], (64, 128))

    def test_vertical_joints_are_staggered_between_courses_and_across_the_wrap(self):
        blocks = small_layout(seed=3, stagger=24)
        rows = {}
        for b in blocks:
            rows.setdefault(b["y0"], set()).add(b["x0"])
        keys = sorted(rows)
        for i, k in enumerate(keys):
            nxt = rows[keys[(i + 1) % len(keys)]]
            for x in rows[k]:
                for y in nxt:
                    d = abs(x - y)
                    self.assertGreaterEqual(min(d, SIZE - d), 24)

    def test_same_seed_same_layout_other_seed_other_layout(self):
        self.assertEqual(small_layout(1), small_layout(1))
        self.assertNotEqual(small_layout(1), small_layout(2))

    def test_impossible_stagger_raises(self):
        with self.assertRaises(RuntimeError):
            bl.plan_blocks((128, 128), (128,), SIZE, 1, 100, attempts=20)


class RenderTests(unittest.TestCase):
    def setUp(self):
        self.blocks = small_layout()
        self.maps = bl.render(self.blocks, SIZE, joint=2, arris=2, seed=5)

    def test_joint_pixels_are_two_wide_and_low(self):
        h = self.maps["height"]
        b = self.blocks[0]
        y = b["y0"] + 32
        x0 = b["x0"]
        self.assertEqual(h[y, x0], 0.0)
        self.assertEqual(h[y, (x0 - 1) % SIZE], 0.0)
        self.assertGreater(h[y, (x0 + 2) % SIZE], 0.0)
        self.assertGreater(h[y, (x0 - 3) % SIZE], 0.0)

    def test_block_interior_is_flat_and_the_bevel_ramps(self):
        h = self.maps["height"]
        b = self.blocks[0]
        y = b["y0"] + 32
        self.assertEqual(h[y, (b["x0"] + 30) % SIZE], 1.0)
        self.assertLess(h[y, (b["x0"] + 2) % SIZE], 1.0)
        ramp = [h[y, (b["x0"] + i) % SIZE] for i in range(6)]
        self.assertTrue((np.diff(ramp) >= 0).all())

    def test_edge_mask_is_the_inverse_of_the_height(self):
        np.testing.assert_allclose(self.maps["edge"], 1.0 - self.maps["height"])

    def test_one_id_per_block_and_ids_differ(self):
        ids = self.maps["id"]
        for b in self.blocks:
            cols = [x % SIZE for x in range(b["x0"], b["x1"])]
            region = ids[b["y0"]:b["y1"]][:, cols]
            self.assertEqual(region.min(), region.max())
        self.assertGreater(len(np.unique(ids)), len(self.blocks) // 2)

    def test_ao_is_dark_only_near_joints(self):
        ao = self.maps["ao"]
        b = self.blocks[0]
        self.assertLess(ao[b["y0"] + 32, b["x0"]], 0.8)
        self.assertGreater(ao[b["y0"] + 32, (b["x0"] + 32) % SIZE], 0.99)
        self.assertGreaterEqual(ao.min(), 0.0)
        self.assertLessEqual(ao.max(), 1.0)

    def test_maps_have_the_size(self):
        for k in ("id", "height", "edge", "ao", "stain"):
            self.assertEqual(self.maps[k].shape, (SIZE, SIZE))

    def test_stain_map_wraps_and_spans_zero_to_one(self):
        s = self.maps["stain"]
        self.assertAlmostEqual(float(s.min()), 0.0, places=5)
        self.assertAlmostEqual(float(s.max()), 1.0, places=5)
        step_edge = np.abs(s[:, 0] - s[:, -1]).mean()
        step_inside = np.abs(np.diff(s, axis=1)).mean()
        self.assertLess(step_edge, 3 * step_inside)


class FilesTests(unittest.TestCase):
    def test_to_u8_and_u16_ranges(self):
        a = np.array([[0.0, 0.5, 1.0]])
        self.assertEqual(bl.to_u8(a).tolist(), [[0, 128, 255]])
        self.assertEqual(bl.to_u16(a).tolist(), [[0, 32768, 65535]])


V2_SIZE = 256


def v2_blocks(seed=4):
    return bl.plan_blocks((64, 64, 64, 64), (64, 96, 128), V2_SIZE, seed, 16)


def v2_maps(blocks=None, **kw):
    args = dict(joint=5, radius=8, seed=3, radius_jitter=0.0)
    args.update(kw)
    return bl.render_v2(blocks or v2_blocks(), V2_SIZE, **args)


MAP_KEYS = ("id", "height", "edge", "ao", "stain", "joint", "edgelight", "chip", "cloud")


class CourseTests(unittest.TestCase):
    def test_alternate_pattern_fills_the_tile(self):
        h = bl.course_heights(1024, 461.0, (0.625, 0.486))
        self.assertEqual(h, [288, 224, 288, 224])

    def test_single_height_gives_equal_courses(self):
        h = bl.course_heights(1024, 461.0, (0.44,))
        self.assertEqual(sum(h), 1024)
        self.assertEqual(len(h), 5)
        self.assertLessEqual(max(h) - min(h), 1)

    def test_other_density_changes_the_course_count(self):
        h = bl.course_heights(1024, 307.0, (0.555,))
        self.assertEqual(len(h), 6)
        self.assertEqual(sum(h), 1024)

    def test_length_choices_stay_in_the_metre_range(self):
        ls = bl.length_choices(461.0, 0.8, 1.4, step=16)
        self.assertGreaterEqual(min(ls) / 461.0, 0.8)
        self.assertLessEqual(max(ls) / 461.0, 1.4)
        self.assertTrue(all(isinstance(v, int) for v in ls))

    def test_scaled_px_follows_the_density(self):
        self.assertEqual(bl.scaled_px(8, 461.0), 8)
        self.assertEqual(bl.scaled_px(8, 307.0), 5)
        self.assertGreaterEqual(bl.scaled_px(0.1, 461.0), 1)


class JointAndRoundTests(unittest.TestCase):
    def setUp(self):
        self.blocks = v2_blocks()
        self.maps = v2_maps(self.blocks)

    def test_all_v2_maps_exist_with_the_size_and_range(self):
        for k in MAP_KEYS:
            m = self.maps[k]
            self.assertEqual(m.shape, (V2_SIZE, V2_SIZE), k)
            self.assertGreaterEqual(float(m.min()), 0.0, k)
            self.assertLessEqual(float(m.max()), 1.0, k)

    def test_joint_width_is_exact_for_odd_and_even_widths(self):
        for width in (4, 5, 6, 7):
            m = bl.render_v2(self.blocks, V2_SIZE, joint=width, radius=6, seed=3, radius_jitter=0.0)
            y = self.blocks[0]["y0"] + 32
            row = (m["height"][y] == 0.0).tolist()
            start = row.index(False)
            row = row[start:] + row[:start]
            groups, n = [], 0
            for v in row + [False]:
                if v:
                    n += 1
                elif n:
                    groups.append(n)
                    n = 0
            self.assertTrue(groups)
            self.assertTrue(all(g == width for g in groups), (width, groups))

    def test_height_rises_in_a_quarter_round_from_the_joint(self):
        m = v2_maps(self.blocks, joint=5, radius=8)
        b = self.blocks[0]
        y = b["y0"] + 32
        h = np.array([m["height"][y, (b["x0"] + i) % V2_SIZE] for i in range(0, 30)])
        jw = int(np.sum(h[:6] == 0.0))
        self.assertEqual(jw, 3)
        ramp = h[jw:jw + 8]
        self.assertTrue((np.diff(ramp) >= 0).all())
        self.assertEqual(h[jw + 8], 1.0)
        centre = (np.arange(8) + 0.5) / 8.0
        np.testing.assert_allclose(ramp, np.sqrt(1 - (1 - centre) ** 2), atol=1e-9)
        self.assertGreater(float(ramp[3]), 0.8)
        self.assertLess(float(ramp[0]), 0.5)

    def test_block_interior_is_flat_at_one(self):
        b = self.blocks[0]
        self.assertEqual(self.maps["height"][b["y0"] + 32, (b["x0"] + 32) % V2_SIZE], 1.0)

    def test_edge_is_one_minus_height(self):
        np.testing.assert_allclose(self.maps["edge"], 1.0 - self.maps["height"])

    def test_joint_mask_marks_the_joint_pixels(self):
        j = self.maps["joint"]
        h = self.maps["height"]
        self.assertGreater(float(j[h == 0.0].min()), 0.8)
        self.assertLess(float(j[h > 0.6].max()), 0.1)

    def test_radius_jitter_makes_the_round_vary_along_an_edge(self):
        m = v2_maps(self.blocks, radius_jitter=0.35)
        b = self.blocks[0]
        col = (b["x0"] + 5 + 3) % V2_SIZE
        vals = m["height"][b["y0"] + 10:b["y1"] - 10, col]
        self.assertGreater(float(vals.std()), 0.01)

    def test_structure_maps_wrap_in_x(self):
        blocks = [dict(b) for b in self.blocks]
        dx = 37
        shifted = [dict(b, x0=(b["x0"] + dx) % V2_SIZE, x1=(b["x0"] + dx) % V2_SIZE + b["x1"] - b["x0"])
                   for b in blocks]
        kw = dict(joint=5, radius=8, seed=3, radius_jitter=0.0, chip_frac=0.0)
        a = bl.render_v2(blocks, V2_SIZE, **kw)
        s = bl.render_v2(shifted, V2_SIZE, **kw)
        for k in ("id", "height", "joint", "ao"):
            np.testing.assert_allclose(s[k], np.roll(a[k], dx, axis=1), atol=1e-9, err_msg=k)

    def test_joint_runs_across_the_y_wrap(self):
        h = self.maps["height"]
        self.assertTrue((h[-2:] == 0.0).all())
        self.assertTrue((h[:3] == 0.0).all())

    def test_noise_maps_and_a_wrapped_block_cloud_are_seamless(self):
        s = self.maps["stain"]
        for ax in (0, 1):
            self.assertLess(np.abs(np.take(s, 0, axis=ax) - np.take(s, -1, axis=ax)).mean(),
                            3 * np.abs(np.diff(s, axis=ax)).mean())
        blocks = [{"x0": 200, "x1": 330, "y0": 0, "y1": 128, "id": 0},
                  {"x0": 74, "x1": 200, "y0": 0, "y1": 128, "id": 1},
                  {"x0": 20, "x1": 150, "y0": 128, "y1": 256, "id": 2},
                  {"x0": 150, "x1": 276, "y0": 128, "y1": 256, "id": 3}]
        m = bl.render_v2(blocks, V2_SIZE, joint=3, radius=5, seed=1, cloud_px=40, cloud_std=0.1,
                         radius_jitter=0.0)
        c = m["cloud"][40:90]
        step = np.abs(c[:, 0] - c[:, -1]).mean()
        inner = np.abs(np.diff(c[:, 210:250], axis=1)).mean()
        self.assertLess(step, 3 * inner)


class EdgeLightTests(unittest.TestCase):
    def test_light_sits_just_inside_the_edge_and_not_in_the_face_or_joint(self):
        blocks = v2_blocks()
        m = v2_maps(blocks, joint=5, radius=8)
        b = blocks[0]
        y = b["y0"] + 32
        row = np.array([m["edgelight"][y, (b["x0"] + i) % V2_SIZE] for i in range(0, 40)])
        self.assertEqual(float(row[:3].max()), 0.0)
        peak = int(np.argmax(row))
        self.assertTrue(3 + 1 <= peak <= 3 + 8, peak)
        self.assertLess(float(row[30]), 0.01)
        self.assertGreater(float(row.max()), 0.2)

    def test_light_band_is_a_few_px_wide(self):
        blocks = v2_blocks()
        m = v2_maps(blocks, joint=5, radius=8)
        b = blocks[0]
        row = np.array([m["edgelight"][b["y0"] + 32, (b["x0"] + i) % V2_SIZE] for i in range(0, 40)])
        self.assertLessEqual(int((row > 0.2).sum()), 10)


class ChipTests(unittest.TestCase):
    def test_about_six_percent_of_blocks_get_a_chip(self):
        blocks = bl.plan_blocks((32,) * 8, (32, 64), V2_SIZE, 7, 8)
        m = bl.render_v2(blocks, V2_SIZE, joint=3, radius=5, seed=2, chip_frac=0.06, radius_jitter=0.0)
        n = len(m["info"]["chips"])
        self.assertEqual(n, max(1, round(0.06 * len(blocks))))
        self.assertEqual(len({c["block"] for c in m["info"]["chips"]}), n)

    def test_chips_are_three_to_eight_px_and_show_in_the_map(self):
        m = v2_maps(chip_frac=0.5, chip_px=(3, 8))
        self.assertGreater(len(m["info"]["chips"]), 0)
        for c in m["info"]["chips"]:
            self.assertGreaterEqual(c["r"], 3)
            self.assertLessEqual(c["r"], 8)
            self.assertGreater(float(m["chip"][c["cy"] % V2_SIZE, c["cx"] % V2_SIZE]), 0.2)

    def test_chip_map_is_zero_in_joints_and_cuts_the_height(self):
        m = v2_maps(chip_frac=0.5)
        self.assertEqual(float(m["chip"][m["height"] == 0.0].max()), 0.0)
        self.assertGreater(float(m["chip"].max()), 0.5)
        clean = v2_maps(chip_frac=0.0)
        self.assertLess(float(m["height"].sum()), float(clean["height"].sum()))

    def test_no_chips_when_the_fraction_is_zero(self):
        m = v2_maps(chip_frac=0.0)
        self.assertEqual(float(m["chip"].max()), 0.0)
        self.assertEqual(m["info"]["chips"], [])


class CloudTests(unittest.TestCase):
    def test_smooth_noise_wraps_and_has_unit_std(self):
        n = bl.smooth_noise(128, 6.0, 1)
        self.assertAlmostEqual(float(n.std()), 1.0, places=2)
        self.assertLess(np.abs(n[:, 0] - n[:, -1]).mean(), 3 * np.abs(np.diff(n, axis=1)).mean())

    def test_smooth_noise_feature_size_follows_sigma(self):
        sigma = 8.0
        n = bl.smooth_noise(256, sigma, 2)
        ac = np.real(np.fft.ifft2(np.abs(np.fft.fft2(n)) ** 2))[0] / (n ** 2).sum()
        lag = int(np.argmax(ac < 0.5))
        self.assertAlmostEqual(lag / (1.665 * sigma), 1.0, delta=0.25)

    def test_blocks_get_different_clouds_and_the_map_is_soft(self):
        blocks = bl.plan_blocks((64, 64, 64, 64), (128,), V2_SIZE, 3, 16)
        m = bl.render_v2(blocks, V2_SIZE, joint=3, radius=5, seed=9, cloud_px=16, cloud_std=0.1,
                         radius_jitter=0.0)
        c = m["cloud"]
        pats = []
        for b in blocks:
            cols = [(b["x0"] + i) % V2_SIZE for i in range(8, 120)]
            pats.append(c[b["y0"] + 8:b["y1"] - 8][:, cols].ravel())
        cors = [abs(np.corrcoef(pats[i], pats[j])[0, 1]) for i in range(len(pats)) for j in range(i + 1, len(pats))]
        self.assertLess(float(np.mean(cors)), 0.35)
        b = blocks[0]
        cols = [(b["x0"] + i) % V2_SIZE for i in range(8, 120)]
        self.assertLess(float(np.abs(np.diff(c[b["y0"] + 20, cols])).max()), 0.08)

    def test_cloud_is_neutral_in_joints_and_has_the_asked_spread(self):
        m = v2_maps(cloud_std=0.15)
        h = m["height"]
        self.assertAlmostEqual(float(m["cloud"][h == 0.0].mean()), 0.5, places=3)
        inner = m["cloud"][h == 1.0]
        self.assertAlmostEqual(float(inner.std()), 0.15, delta=0.06)


class ToneTests(unittest.TestCase):
    def test_tones_are_spread_in_range_with_no_landmark_block(self):
        blocks = v2_blocks()
        m = v2_maps(blocks, tone_range=(0.2, 0.8))
        tones = np.array(sorted(m["info"]["tones"].values()))
        self.assertGreaterEqual(float(tones.min()), 0.2 - 1e-9)
        self.assertLessEqual(float(tones.max()), 0.8 + 1e-9)
        z = np.abs(tones - tones.mean()) / tones.std()
        self.assertLess(float(z.max()), 1.8)
        self.assertEqual(m["info"]["landmarks"], [])

    def test_touching_blocks_differ_in_tone(self):
        m = v2_maps()
        self.assertGreater(m["info"]["min_neighbour_step"], 0.03)

    def test_id_map_carries_one_tone_per_block(self):
        blocks = v2_blocks()
        m = v2_maps(blocks)
        for b in blocks:
            cols = [x % V2_SIZE for x in range(b["x0"], b["x1"])]
            region = m["id"][b["y0"]:b["y1"]][:, cols]
            self.assertLess(float(region.max() - region.min()), 1.5 / 255.0 + 1e-9)

    def test_landmark_finder_flags_a_lone_outlier(self):
        tones = {i: 0.5 + 0.01 * (i % 3) for i in range(10)}
        tones[4] = 0.95
        self.assertEqual(bl.landmark_blocks(tones, z=1.8), [4])


class WriteV2Tests(unittest.TestCase):
    def test_write_puts_every_map_on_disk(self):
        import tempfile
        from PIL import Image
        blocks = v2_blocks()
        m = v2_maps(blocks)
        with tempfile.TemporaryDirectory() as d:
            files = bl.write(d, m, blocks)
            for k in MAP_KEYS:
                self.assertTrue(os.path.exists(files[k]), k)
            self.assertEqual(Image.open(files["height"]).mode, "I;16")
            self.assertEqual(Image.open(files["cloud"]).mode, "L")


class MakeLayoutTests(unittest.TestCase):
    def test_density_sets_courses_joint_and_radius(self):
        blocks, maps, info = bl.make_layout(ppm=461.0, size=1024, courses_m=(0.44,), lengths_m=(0.8, 1.4),
                                            joint_mm=11, radius_mm=17, seed=5, stagger_m=0.15)
        self.assertEqual(info["joint_px"], 5)
        self.assertEqual(info["radius_px"], 8)
        self.assertEqual(len({b["y0"] for b in blocks}), 5)
        for b in blocks:
            self.assertGreaterEqual((b["x1"] - b["x0"]) / 461.0, 0.8 - 1e-9)
            self.assertLessEqual((b["x1"] - b["x0"]) / 461.0, 1.4 + 1e-9)
        self.assertEqual(maps["height"].shape, (1024, 1024))

    def test_larger_tile_scales_joint_and_radius_down(self):
        _, _, info = bl.make_layout(ppm=307.0, size=1024, courses_m=(0.555,), lengths_m=(0.8, 1.6),
                                    joint_mm=11, radius_mm=17, seed=5, stagger_m=0.2)
        self.assertEqual(info["joint_px"], 3)
        self.assertEqual(info["radius_px"], 5)
        self.assertAlmostEqual(info["tile_m"], 1024 / 307.0, places=3)


if __name__ == "__main__":
    unittest.main()
