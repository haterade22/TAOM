"""Tests for tools/painter/tile_compose.py: the host-side composition of a stone set from Painter exports
and layout maps (diffuse, normal with green down, specular, height) and the numbers that judge it."""
import os
import sys
import tempfile
import unittest

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "painter"))
import block_layout as bl  # noqa: E402
import tile_compose as tc  # noqa: E402

SIZE = 128


def flat(v, shape=(SIZE, SIZE)):
    return np.full(shape, v, dtype=float)


def synthetic_base(seed=0, size=SIZE):
    rng = np.random.default_rng(seed)
    grain = bl.smooth_noise(size, 1.5, seed)
    colour = np.stack([158 + 4 * grain, 156 + 4 * grain, 150 + 4 * grain], axis=-1)
    return {"color": np.clip(colour, 0, 255).astype(np.uint8),
            "height": np.full((size, size), 120, np.uint8),
            "normal": np.dstack([np.full((size, size), 127), np.full((size, size), 128),
                                 np.full((size, size), 255)]).astype(np.uint8),
            "rough": np.clip(176 + 6 * rng.normal(size=(size, size)), 0, 255).astype(np.uint8),
            "ao": np.full((size, size), 250, np.uint8)}


def synthetic_layout(size=SIZE, seed=2):
    blocks = bl.plan_blocks((64, 64), (64, 128), size, seed, 8)
    maps = bl.render_v2(blocks, size, joint=3, radius=5, seed=seed, radius_jitter=0.0, chip_frac=0.0,
                        cloud_px=24)
    return blocks, maps


class ColourTests(unittest.TestCase):
    def test_luminance_and_saturation(self):
        rgb = np.array([[[200.0, 100.0, 50.0], [100.0, 100.0, 100.0]]])
        lum = tc.luminance(rgb)
        self.assertAlmostEqual(float(lum[0, 0]), 0.299 * 200 + 0.587 * 100 + 0.114 * 50, places=6)
        sat = tc.saturation(rgb)
        self.assertAlmostEqual(float(sat[0, 0]), 0.75, places=6)
        self.assertEqual(float(sat[0, 1]), 0.0)

    def test_overlay_is_neutral_at_half_grey_and_scales_with_opacity(self):
        base = np.full((2, 2, 3), 0.6)
        np.testing.assert_allclose(tc.overlay(base, np.full((2, 2), 0.5), 1.0), base)
        full = tc.overlay(base, np.full((2, 2), 0.9), 1.0)
        half = tc.overlay(base, np.full((2, 2), 0.9), 0.5)
        self.assertAlmostEqual(float(full[0, 0, 0]), 1 - 2 * 0.4 * 0.1, places=9)
        self.assertAlmostEqual(float(half[0, 0, 0]), 0.6 + 0.5 * (full[0, 0, 0] - 0.6), places=9)
        dark = tc.overlay(np.full((1, 1, 3), 0.25), np.full((1, 1), 0.2), 1.0)
        self.assertAlmostEqual(float(dark[0, 0, 0]), 2 * 0.25 * 0.2, places=9)

    def test_screen_and_multiply_with_a_mask(self):
        base = np.full((2, 2, 3), 0.5)
        mask = np.array([[1.0, 0.0], [0.5, 0.0]])
        scr = tc.screen_mask(base, (1.0, 1.0, 1.0), mask)
        self.assertAlmostEqual(float(scr[0, 0, 0]), 1.0, places=9)
        self.assertAlmostEqual(float(scr[0, 1, 0]), 0.5, places=9)
        self.assertAlmostEqual(float(scr[1, 0, 0]), 0.75, places=9)
        mul = tc.multiply_mask(base, (0.8, 0.8, 0.8), mask)
        self.assertAlmostEqual(float(mul[0, 0, 0]), 0.4, places=9)
        self.assertAlmostEqual(float(mul[0, 1, 0]), 0.5, places=9)
        self.assertAlmostEqual(float(mul[1, 0, 0]), 0.45, places=9)

    def test_retarget_hits_the_mean_and_limits_saturation(self):
        rng = np.random.default_rng(1)
        img = np.clip(150 + 20 * rng.normal(size=(64, 64, 3)), 0, 255)
        out = tc.retarget_colour(img, (185, 182, 176), sat_max=0.05)
        np.testing.assert_allclose(out.reshape(-1, 3).mean(0), (185, 182, 176), atol=0.6)
        self.assertLessEqual(float(tc.saturation(out).mean()), 0.05 + 0.01)

    def test_retarget_without_a_limit_keeps_the_pixel_chroma_spread(self):
        img = np.zeros((4, 4, 3))
        img[..., 0], img[..., 1], img[..., 2] = 100, 110, 120
        out = tc.retarget_colour(img, (200, 190, 180))
        np.testing.assert_allclose(out[0, 0], (200, 190, 180), atol=1.0)

    def test_scale_to_luminance(self):
        t = tc.scale_to_luminance((195, 187, 178), 150.0)
        self.assertAlmostEqual(float(tc.luminance(np.array(t))), 150.0, places=6)
        self.assertAlmostEqual(t[0] / t[2], 195 / 178.0, places=6)


class FilterTests(unittest.TestCase):
    def test_blur_keeps_the_mean_and_wraps(self):
        a = np.zeros((32, 32))
        a[0, 0] = 1.0
        b = tc.gauss_blur(a, 2.0)
        self.assertAlmostEqual(float(b.sum()), 1.0, places=6)
        self.assertGreater(float(b[-1, -1]), 0.001)

    def test_blur_without_wrap_does_not_leak_across_the_edge(self):
        a = np.zeros((32, 32))
        a[0, 0] = 1.0
        b = tc.gauss_blur(a, 2.0, periodic=False)
        self.assertEqual(b.shape, a.shape)
        self.assertLess(float(b[-1, -1]), 1e-6)

    def test_unsharp_with_zero_gain_is_identity_and_raises_the_grain(self):
        rng = np.random.default_rng(3)
        img = np.clip(0.5 + 0.02 * rng.normal(size=(64, 64, 3)), 0, 1)
        np.testing.assert_allclose(tc.unsharp(img, 2.0, 0.0), img)
        self.assertGreater(float(tc.unsharp(img, 2.0, 1.5).std()), 1.3 * float(img.std()))

    def test_highpass_removes_low_frequency_tone(self):
        yy = np.linspace(0, 2 * np.pi, 64, endpoint=False)
        img = np.repeat((0.5 + 0.1 * np.sin(yy))[:, None, None], 3, axis=2) * np.ones((64, 64, 3))
        out = tc.highpass(img, 4.0, 1.0)
        self.assertLess(float(out.std()), 0.2 * float(img.std()))
        self.assertAlmostEqual(float(out.mean()), float(img.mean()), places=6)


class NormalTests(unittest.TestCase):
    def flat_normal(self):
        n = np.zeros((SIZE, SIZE, 3), np.uint8)
        n[..., 0], n[..., 1], n[..., 2] = 128, 128, 255
        return n

    def test_tilt_of_a_flat_map_is_near_zero(self):
        self.assertLess(tc.mean_tilt(self.flat_normal()), 0.01)

    def test_green_down_sign_for_a_ramp(self):
        h = np.tile(np.linspace(0, 30, SIZE)[None, :], (SIZE, 1))
        n = tc.combine_normal(self.flat_normal(), h)
        self.assertLess(float(n[64, 64, 0]), 120)
        self.assertAlmostEqual(float(n[64, 64, 1]), 128, delta=3)
        v = np.tile(np.linspace(0, 30, SIZE)[:, None], (1, SIZE))
        n = tc.combine_normal(self.flat_normal(), v)
        self.assertLess(float(n[64, 64, 1]), 120)
        self.assertAlmostEqual(float(n[64, 64, 0]), 128, delta=3)

    def test_combined_normal_is_unit_length_and_adds_the_base_tilt(self):
        base = self.flat_normal()
        base[..., 0] = 160
        n = tc.combine_normal(base, np.zeros((SIZE, SIZE)))
        v = n.astype(float) / 127.5 - 1
        np.testing.assert_allclose(np.linalg.norm(v, axis=2), 1.0, atol=0.02)
        self.assertGreater(float(n[0, 0, 0]), 150)

    def test_scale_normal_to_a_tilt(self):
        rng = np.random.default_rng(4)
        n = np.clip(128 + 20 * rng.normal(size=(SIZE, SIZE, 3)), 0, 255).astype(np.uint8)
        n[..., 2] = 255
        out = tc.scale_normal_to_tilt(n, 0.17)
        self.assertAlmostEqual(tc.mean_tilt(out), 0.17, delta=0.01)

    def test_flip_green(self):
        n = np.zeros((2, 2, 3), np.uint8)
        n[..., 1] = 10
        self.assertEqual(int(tc.flip_green(n)[0, 0, 1]), 245)


class ComposeTests(unittest.TestCase):
    def setUp(self):
        self.blocks, self.maps = synthetic_layout()
        self.base = synthetic_base()
        self.info = {"radius_px": 5, "joint_px": 3, "ppm": 461.0}
        self.spec = tc.merge_spec({"d": {"target": [195, 187, 178], "sat_max": 0.12, "tone": 0.12,
                                         "cloud": 0.2, "joint": {"color": [0.82, 0.8, 0.77]}}})
        self.out = tc.compose(self.base, self.maps, self.spec, self.info)

    def face_and_joint(self):
        h = self.maps["height"]
        return h == 1.0, h == 0.0

    def test_outputs_are_8_bit_with_the_shape(self):
        for k, c in (("d", 3), ("n", 3), ("s", 3), ("h", None)):
            a = self.out[k]
            self.assertEqual(a.dtype, np.uint8, k)
            self.assertEqual(a.shape, (SIZE, SIZE, c) if c else (SIZE, SIZE), k)

    def test_diffuse_mean_is_the_target(self):
        np.testing.assert_allclose(self.out["d"].reshape(-1, 3).mean(0), (195, 187, 178), atol=1.5)

    def test_joints_are_darkened_stone_a_little_under_the_face(self):
        face, joint = self.face_and_joint()
        lum = tc.luminance(self.out["d"].astype(float))
        gap = lum[face].mean() - lum[joint].mean()
        self.assertGreater(gap, 20)
        self.assertLess(gap, 60)
        d = self.out["d"].astype(float)
        self.assertGreater(d[joint][:, 0].mean() - d[joint][:, 2].mean(), 5)

    def test_tone_per_block_changes_the_block_means(self):
        spec0 = tc.merge_spec({"d": {"target": [195, 187, 178], "tone": 0.0, "cloud": 0.0}})
        a = tc.compose(self.base, self.maps, spec0, self.info)["d"]
        face, _ = self.face_and_joint()
        ids = (self.maps["id"] * 1000).round()
        means_a = [tc.luminance(a.astype(float))[face & (ids == v)].mean() for v in np.unique(ids[face])]
        means_b = [tc.luminance(self.out["d"].astype(float))[face & (ids == v)].mean() for v in np.unique(ids[face])]
        self.assertGreater(np.std(means_b), 2 * np.std(means_a) + 0.5)

    def test_normal_dips_at_joint_sides_with_green_down_signs(self):
        n = self.out["n"].astype(float)
        h = self.maps["height"]
        y = 32
        row = h[y]
        starts = [x for x in range(SIZE) if row[x] == 0 and row[(x - 1) % SIZE] > 0]
        self.assertTrue(starts)
        x0 = starts[0]
        left_side = n[y, (x0 - 3) % SIZE, 0]
        right_side = n[y, (x0 + 3 + 2) % SIZE, 0]
        self.assertGreater(left_side, 140)
        self.assertLess(right_side, 115)

    def test_normal_has_no_blue_dependence_but_is_a_unit_vector(self):
        v = self.out["n"].astype(float) / 127.5 - 1
        self.assertAlmostEqual(float(np.linalg.norm(v, axis=2).mean()), 1.0, delta=0.03)

    def test_specular_packing(self):
        s = self.out["s"]
        self.assertEqual(int(s[..., 0].max()), 0)
        face, joint = self.face_and_joint()
        self.assertAlmostEqual(float(s[..., 1].astype(float).mean()), self.spec["s"]["gloss_mean"], delta=1.5)
        self.assertLess(float(s[..., 1][joint].mean()), float(s[..., 1][face].mean()) - 10)
        self.assertLess(float(s[..., 2][joint].mean()), float(s[..., 2][face].mean()) - 20)

    def test_height_map_puts_joints_lowest(self):
        face, joint = self.face_and_joint()
        h = self.out["h"].astype(float)
        self.assertLess(h[joint].mean(), h[face].mean() - 100)

    def test_report_carries_the_judging_numbers(self):
        rep = tc.report(self.out["d"], 461.0)
        for k in ("mean", "lum_mean", "saturation", "lum_p5_p95", "fine_grain", "joint_contrast",
                  "joint_width_px", "joint_width_mm"):
            self.assertIn(k, rep)


class PlinthTests(unittest.TestCase):
    def setUp(self):
        self.base = synthetic_base(seed=3)
        h = np.tile(np.linspace(0, 20, SIZE)[None, :], (SIZE, 1))
        up = tc.combine_normal(self.base["normal"], h)
        self.base["normal"] = tc.flip_green(up)
        rng = np.random.default_rng(1)
        self.base["color"] = np.clip(np.array([200, 184, 159]) + 25 * rng.normal(size=(SIZE, SIZE, 3)),
                                     0, 255).astype(np.uint8)

    def test_plinth_has_the_value_the_hue_family_and_the_tilt(self):
        target = tc.scale_to_luminance((195, 187, 178), 150.0)
        out = tc.compose_plinth(self.base, target, 0.09, "up", 0.17, 70.0)
        lum = float(tc.luminance(out["d"].astype(float)).mean())
        self.assertAlmostEqual(lum, 150.0, delta=1.5)
        self.assertLessEqual(float(tc.saturation(out["d"].astype(float)).mean()), 0.11)
        self.assertAlmostEqual(tc.mean_tilt(out["n"]), 0.17, delta=0.01)
        self.assertAlmostEqual(float(out["s"][..., 1].astype(float).mean()), 70.0, delta=1.5)
        self.assertEqual(int(out["s"][..., 0].max()), 0)

    def test_up_normal_is_turned_to_green_down(self):
        target = (150, 145, 140)
        out = tc.compose_plinth(self.base, target, 0.09, "up", None, 70.0)
        self.assertTrue(np.array_equal(out["n"], tc.flip_green(self.base["normal"])))
        out = tc.compose_plinth(self.base, target, 0.09, "down", None, 70.0)
        self.assertTrue(np.array_equal(out["n"], self.base["normal"]))


class TrimTests(unittest.TestCase):
    def test_each_strip_is_moved_to_the_face_luminance(self):
        from make_rivendell_wall_textures import strip_rows
        img = np.full((1024, 1024, 3), 150, np.uint8)
        for i, (t, b) in enumerate(strip_rows().values()):
            img[t:b] = 150 + 12 * i
        out = tc.equalise_strips(img, 190.0)
        for t, b in strip_rows().values():
            self.assertAlmostEqual(float(tc.luminance(out[t:b].astype(float)).mean()), 190.0, delta=0.6)


class StatsTests(unittest.TestCase):
    def striped(self, depth=30, width=5, period=100, size=400, noise=0.0, seed=0):
        lum = np.full((size, size), 190.0)
        for y in range(period // 2, size, period):
            lum[y:y + width] -= depth
        if noise:
            lum += noise * np.random.default_rng(seed).normal(size=lum.shape)
        return np.clip(np.repeat(lum[..., None], 3, axis=2), 0, 255).astype(np.uint8)

    def test_joint_profile_finds_depth_width_and_count(self):
        p = tc.joint_profile(tc.luminance(self.striped().astype(float)), 400.0, periodic=True)
        self.assertEqual(p["count"], 4)
        self.assertAlmostEqual(p["contrast"], 30, delta=2)
        self.assertAlmostEqual(p["width_px"], 5, delta=1.2)

    def test_joint_profile_survives_grain_and_works_without_wrap(self):
        p = tc.joint_profile(tc.luminance(self.striped(noise=5.0).astype(float)), 400.0, periodic=False)
        self.assertEqual(p["count"], 4)
        self.assertAlmostEqual(p["contrast"], 30, delta=4)

    def test_fine_grain_reads_the_noise_and_ignores_a_few_lines(self):
        rng = np.random.default_rng(5)
        lum = 180 + 8 * rng.normal(size=(300, 300))
        base = tc.fine_grain(lum, 461.0)
        self.assertAlmostEqual(base, 8.0, delta=1.2)
        lum[100:104] -= 40
        lum[:, 200:204] -= 40
        self.assertAlmostEqual(tc.fine_grain(lum, 461.0), base, delta=1.0)

    def test_stats_of_a_known_image(self):
        img = self.striped()
        st = tc.tile_stats(img, 400.0)
        self.assertAlmostEqual(st["lum_mean"], 188.5, delta=1.5)
        self.assertEqual(st["saturation"], 0.0)
        self.assertGreater(st["lum_p5_p95"], -1)
        self.assertAlmostEqual(st["joint_width_mm"], st["joint_width_px"] / 0.4, places=1)

    def test_control_stats_use_the_same_function_at_the_asked_density(self):
        base = np.clip(120 + 10 * np.random.default_rng(1).normal(size=(300, 300, 3)), 0, 255).astype(np.uint8)
        a = tc.resample_to_ppm(base, 100.0, 50.0)
        self.assertEqual(a.shape[:2], (150, 150))


class FilesTests(unittest.TestCase):
    def test_write_and_read_back_a_set(self):
        out = {"d": np.zeros((8, 8, 3), np.uint8), "n": np.zeros((8, 8, 3), np.uint8),
               "s": np.zeros((8, 8, 3), np.uint8), "h": np.zeros((8, 8), np.uint8)}
        with tempfile.TemporaryDirectory() as d:
            files = tc.write_set(d, "t_x", out)
            for k in ("d", "n", "s", "h"):
                self.assertTrue(os.path.exists(files[k]))
            self.assertTrue(files["h"].endswith("t_x_h.png"))
            self.assertEqual(Image.open(files["h"]).mode, "L")

    def test_load_layout_reads_height_as_16_bit(self):
        blocks, maps = synthetic_layout()
        with tempfile.TemporaryDirectory() as d:
            bl.write(d, maps, blocks)
            lay, info = tc.load_layout(d)
            self.assertAlmostEqual(float(np.abs(lay["height"] - maps["height"]).max()), 0.0, places=4)
            self.assertAlmostEqual(float(np.abs(lay["cloud"] - maps["cloud"]).max()), 0.0, delta=1 / 255.0)
            self.assertEqual(info["radius"], 5)


class DetailNormalTests(unittest.TestCase):
    def test_detail_map_tilt_wrap_and_shape(self):
        rng = np.random.default_rng(2)
        src = np.clip(128 + 25 * bl.smooth_noise(256, 1.2, 4), 0, 255)
        n = tc.detail_normal(src, size=128, tilt=0.065, tool_marks=0.4, seed=3)
        self.assertEqual(n.shape, (128, 128, 3))
        self.assertAlmostEqual(tc.mean_tilt(n), 0.065, delta=0.008)
        for ax in (0, 1):
            edge = np.abs(np.take(n[..., :2].astype(float), 0, axis=ax) - np.take(n[..., :2].astype(float), -1, axis=ax)).mean()
            inner = np.abs(np.diff(n[..., :2].astype(float), axis=ax)).mean()
            self.assertLess(edge, 2.5 * inner)
        self.assertGreater(float(rng.random()), -1)


if __name__ == "__main__":
    unittest.main()
