"""Tests for round 4 of the Rivendell stone: the measures of tile_compose.py (dark tail, low-frequency spread,
block means, cloud contrast, face tilt median), the dressed-ashlar face relief of block_layout.py (a tilted
plane and a cushion per block, a soft undulation), the planned courses of the plinth and the joint-at-row rule
of plinth_tools.py."""
import os
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "painter"))
import block_layout as bl  # noqa: E402
import plinth_tools as pt  # noqa: E402
import tile_compose as tc  # noqa: E402

SIZE = 256
PPM = 128.0  # 2 m per tile
HEIGHTS = (96, 64, 96)
FACE = dict(tilt_deg=1.5, tilt_cap_mm=4.0, pillow_mm=0.0, wave_mm=0.0)


def blocks_a(seed=5):
    return bl.plan_blocks(HEIGHTS, (96, 128), SIZE, seed, 16)


def render(face=None, face_level=0.55, blocks=None, **kw):
    args = dict(joint=4, radius=8, seed=3, radius_jitter=0.0, chip_frac=0.0, px_scale=0.5, face_level=face_level,
                face=face, ppm=PPM)
    args.update(kw)
    return bl.render_v2(blocks or blocks_a(), SIZE, **args)


class DarkTailTests(unittest.TestCase):
    def test_it_is_the_median_minus_the_fifth_percentile(self):
        lum = np.full((100, 100), 130.0)
        lum[:10] = 100.0
        self.assertAlmostEqual(tc.dark_tail(lum), 30.0, places=6)

    def test_a_flat_image_has_no_tail(self):
        self.assertEqual(tc.dark_tail(np.full((8, 8), 90.0)), 0.0)

    def test_the_stats_carry_it(self):
        rgb = np.full((64, 64, 3), 150.0)
        rgb[:8] = 100.0
        st = tc.tile_stats(rgb, 128.0)
        self.assertIn("dark_tail", st)
        self.assertGreater(st["dark_tail"], 40.0)


class LowFrequencySpreadTests(unittest.TestCase):
    def test_a_constant_image_has_none(self):
        self.assertEqual(tc.lowfreq_std(np.full((64, 64), 100.0)), 0.0)

    def test_fine_noise_is_blurred_away(self):
        rng = np.random.default_rng(1)
        self.assertLess(tc.lowfreq_std(rng.normal(100, 10, (128, 128)), sigma=24.0, periodic=True), 0.5)

    def test_a_slow_wave_keeps_its_std(self):
        x = np.arange(256)[None, :] * np.ones((256, 1))
        lum = 100.0 + 10.0 * np.sin(2 * np.pi * x / 256.0)
        kept = np.exp(-2.0 * np.pi ** 2 * 24.0 ** 2 / 256.0 ** 2)  # the blur's gain at this wavelength
        self.assertAlmostEqual(tc.lowfreq_std(lum, sigma=24.0, periodic=True), 10.0 / np.sqrt(2.0) * kept, delta=0.05)

    def test_the_default_is_the_judges_pil_blur_of_the_8_bit_luminance(self):
        from PIL import Image, ImageFilter
        rng = np.random.default_rng(2)
        rgb = rng.integers(60, 200, (96, 96, 3)).astype(float)
        lum = tc.luminance709(rgb)
        blurred = Image.fromarray(np.clip(lum, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(24))
        self.assertAlmostEqual(tc.lowfreq_std(lum), float(np.asarray(blurred, dtype=np.float64).std()), places=9)

    def test_the_luminance_weights_are_rec_709(self):
        self.assertAlmostEqual(float(tc.luminance709(np.array([100.0, 100.0, 100.0]))), 100.0, places=9)
        self.assertAlmostEqual(float(tc.luminance709(np.array([0.0, 255.0, 0.0]))), 0.7152 * 255.0, places=9)

    def test_the_stats_carry_it(self):
        st = tc.tile_stats(np.full((64, 64, 3), 150.0), 128.0)
        self.assertIn("lowfreq_std", st)
        self.assertIn("lowfreq_std_wrapped", st)


class BlockMeanTests(unittest.TestCase):
    def test_each_block_gets_the_mean_of_its_inside(self):
        blocks = [{"x0": 0, "x1": 8, "y0": 0, "y1": 16, "id": 0}, {"x0": 8, "x1": 16, "y0": 0, "y1": 16, "id": 1}]
        lum = np.zeros((16, 16))
        lum[:, :8] = 100.0
        lum[:, 8:] = 120.0
        lum[0, 0] = 0.0  # an edge pixel must not count
        m = tc.block_means(lum, blocks, 16, 1)
        self.assertEqual([round(v, 6) for v in m], [100.0, 120.0])

    def test_a_block_over_the_tile_edge_is_whole(self):
        blocks = [{"x0": 12, "x1": 20, "y0": 0, "y1": 16, "id": 0}, {"x0": 4, "x1": 12, "y0": 0, "y1": 16, "id": 1}]
        lum = np.full((16, 16), 90.0)
        lum[:, 12:] = 150.0
        lum[:, :4] = 150.0
        self.assertEqual([round(v, 6) for v in tc.block_means(lum, blocks, 16, 1)], [150.0, 90.0])


class CloudContrastTests(unittest.TestCase):
    def test_block_tone_alone_is_not_cloud(self):
        blocks = blocks_a()
        lum = np.zeros((SIZE, SIZE))
        for b in blocks:
            for x in range(b["x0"], b["x1"]):
                lum[b["y0"]:b["y1"], x % SIZE] = 100.0 + 5.0 * b["id"]
        self.assertLess(tc.cloud_contrast(lum, blocks, SIZE, sigma=4.0, margin_px=20), 0.01)

    def test_a_soft_field_inside_the_blocks_shows(self):
        blocks = blocks_a()
        field = 3.0 * bl.smooth_noise(SIZE, 14.0, 4)
        c = tc.cloud_contrast(100.0 + field, blocks, SIZE, sigma=4.0, margin_px=20)
        self.assertGreater(c, 1.5)
        self.assertLess(c, 3.5)


class FaceTiltMedianTests(unittest.TestCase):
    def test_it_is_the_median_inside_the_blocks(self):
        blocks = [{"x0": 0, "x1": 16, "y0": 0, "y1": 16, "id": 0}]
        n = np.zeros((16, 16, 3), dtype=np.uint8)
        n[..., 2] = 255
        n[..., :2] = 128
        n[..., 1] = 127
        n[2:5, 2:5, 0] = 255  # a few steep pixels do not move a median
        self.assertAlmostEqual(tc.face_tilt_median(n, blocks, 16, 1), float(np.hypot(128 / 127.5 - 1.0, 127 / 127.5 - 1.0)), places=6)


class FaceReliefTests(unittest.TestCase):
    """The face relief is a second height map, "face": 0.5 where the joint is and 0.5 plus the plane of the block
    and the undulation on the faces. Painter adds it to the height of the profile (it does not clamp above 1)."""

    def interior(self, b, margin=18):
        ys = slice(b["y0"] + margin, b["y1"] - margin)
        xs = [x % SIZE for x in range(b["x0"] + margin, b["x1"] - margin)]
        return ys, xs

    def test_default_render_is_unchanged(self):
        a = bl.render_v2(blocks_a(), SIZE, joint=4, radius=8, seed=3, radius_jitter=0.0, chip_frac=0.0,
                         px_scale=0.5)
        b = render(face=None, face_level=1.0)
        self.assertTrue(np.array_equal(a["height"], b["height"]))
        self.assertNotIn("face", b)

    def test_the_face_level_scales_the_face_and_not_the_joint(self):
        m = render(face=None, face_level=0.85)
        h = m["height"]
        self.assertEqual(float(h.min()), 0.0)
        self.assertAlmostEqual(float(h.max()), 0.85, places=6)

    def test_the_profile_keeps_its_straight_arris_with_a_face_relief(self):
        plain = render(face=None, face_level=1.0)["height"]
        m = render(face=dict(FACE), face_level=1.0)
        self.assertTrue(np.array_equal(plain, m["height"]))

    def test_each_face_is_a_plane_when_only_the_tilt_is_on(self):
        h = render(face=dict(FACE), face_level=1.0)["face"]
        for b in blocks_a():
            ys, xs = self.interior(b)
            patch = h[ys][:, xs]
            yy, xx = np.mgrid[0:patch.shape[0], 0:patch.shape[1]]
            design = np.stack([xx.ravel(), yy.ravel(), np.ones(xx.size)], axis=1)
            coef, *_ = np.linalg.lstsq(design, patch.ravel(), rcond=None)
            resid = patch.ravel() - design @ coef
            self.assertLess(float(np.abs(resid).max()), 1e-6)

    def test_the_slope_of_a_plane_stays_under_the_tilt_angle(self):
        h = render(face=dict(FACE), face_level=1.0, face_depth_mm=22.0)["face"]
        limit = np.tan(np.radians(1.5))
        worst = 0.0
        for b in blocks_a():
            ys, xs = self.interior(b)
            patch = h[ys][:, xs]
            gx = np.abs(np.diff(patch, axis=1)).max()
            gy = np.abs(np.diff(patch, axis=0)).max()
            worst = max(worst, float(np.hypot(gx, gy)) * 22.0 / (1000.0 / PPM))
        self.assertLessEqual(worst, limit * 1.001)
        self.assertGreater(worst, limit * 0.2)

    def test_no_corner_of_a_plane_moves_more_than_the_cap(self):
        cap = 3.0
        h = render(face=dict(tilt_deg=1.5, tilt_cap_mm=cap, wave_mm=0.0), face_level=1.0)["face"]
        self.assertLessEqual(float(np.abs(h - 0.5).max()) * 22.0, cap * 1.001)

    def test_the_planes_differ_from_block_to_block(self):
        h = render(face=dict(FACE), face_level=1.0)["face"]
        slopes = []
        for b in blocks_a():
            ys, xs = self.interior(b)
            patch = h[ys][:, xs]
            slopes.append((round(float(np.diff(patch, axis=1).mean()), 7), round(float(np.diff(patch, axis=0).mean()), 7)))
        self.assertGreater(len(set(slopes)), len(slopes) // 2)

    def test_a_block_over_the_tile_edge_has_no_step_there(self):
        blocks = blocks_a()
        crossing = [b for b in blocks if b["x1"] > SIZE]
        self.assertTrue(crossing, "the test layout needs a block that crosses the tile edge")
        h = render(face=dict(FACE), face_level=1.0, blocks=blocks)["face"]
        b = crossing[0]
        row = (b["y0"] + b["y1"]) // 2
        line = h[row]
        step = abs(float(line[0]) - float(line[-1]))
        inner = float(np.abs(np.diff(line[2:30])).max())
        self.assertLessEqual(step, inner + 1e-9)

    def test_a_shifted_layout_gives_the_same_planes_shifted(self):
        blocks = blocks_a()
        shift = 37
        moved = [dict(b, x0=b["x0"] + shift, x1=b["x1"] + shift) for b in blocks]
        a = render(face=dict(FACE), face_level=1.0, blocks=blocks)["face"]
        b = render(face=dict(FACE), face_level=1.0, blocks=moved)["face"]
        self.assertLess(float(np.abs(np.roll(a, shift, axis=1) - b).max()), 1e-9)

    def test_the_joint_stays_flat_with_any_relief(self):
        m = render(face=dict(tilt_deg=1.5, tilt_cap_mm=4.0, pillow_mm=1.0, wave_mm=2.0), face_level=1.0)
        self.assertEqual(float(m["height"].min()), 0.0)
        joint = m["joint"] > 0.99
        self.assertTrue(np.all(m["height"][joint] == 0.0))
        self.assertTrue(np.all(m["face"][joint] == 0.5))

    def test_the_face_map_stays_inside_the_range(self):
        m = render(face=dict(tilt_deg=1.5, tilt_cap_mm=4.0, pillow_mm=1.0, wave_mm=2.0), face_level=1.0)
        self.assertGreaterEqual(float(m["face"].min()), 0.0)
        self.assertLessEqual(float(m["face"].max()), 1.0)
        lo, hi = m["info"]["face_range"]
        self.assertGreaterEqual(lo, 0.0)
        self.assertLessEqual(hi, 1.0)

    def test_a_relief_that_leaves_the_range_is_refused(self):
        with self.assertRaises(ValueError):
            render(face=dict(tilt_deg=1.5, tilt_cap_mm=4.0, pillow_mm=0.0, wave_mm=8.0), face_level=1.0)

    def test_the_cushion_lowers_the_face_toward_the_edge_and_not_the_middle(self):
        flat = render(face=dict(tilt_deg=0.0, pillow_mm=0.0, wave_mm=0.0), face_level=1.0)["height"]
        cushion = render(face=dict(tilt_deg=0.0, pillow_mm=1.0, pillow_width_mm=100.0, wave_mm=0.0),
                         face_level=1.0)["height"]
        b = max(blocks_a(), key=lambda q: (q["x1"] - q["x0"]) * (q["y1"] - q["y0"]))
        yc = (b["y0"] + b["y1"]) // 2
        xc = ((b["x0"] + b["x1"]) // 2) % SIZE
        self.assertAlmostEqual(float(cushion[yc, xc]), float(flat[yc, xc]), places=9)
        edge = (b["x0"] + 12) % SIZE
        self.assertLess(float(cushion[yc, edge]), float(flat[yc, edge]) - 0.002)
        self.assertGreaterEqual(float(cushion[yc, edge]), 0.0)

    def test_the_wave_has_the_wanted_size_and_wavelength(self):
        m = render(face=dict(tilt_deg=0.0, pillow_mm=0.0, wave_mm=2.0, wave_m=(0.3, 0.9)), face_level=1.0,
                   face_depth_mm=20.0)
        w = m["wave"]
        self.assertAlmostEqual(float(w.std()) * 20.0, 2.0, delta=0.05)
        spec = np.abs(np.fft.fft2(w)) ** 2
        fy = np.fft.fftfreq(SIZE)[:, None] * PPM
        fx = np.fft.fftfreq(SIZE)[None, :] * PPM
        f = np.sqrt(fx ** 2 + fy ** 2)
        inside = spec[(f >= 1 / 1.2) & (f <= 1 / 0.2)].sum()
        self.assertGreater(float(inside / spec.sum()), 0.9)

    def test_the_wave_wraps_and_is_the_same_for_the_same_seed(self):
        a = render(face=dict(tilt_deg=0.0, wave_mm=2.0), face_level=1.0)["wave"]
        b = render(face=dict(tilt_deg=0.0, wave_mm=2.0), face_level=1.0)["wave"]
        self.assertTrue(np.array_equal(a, b))
        step = float(np.abs(a[:, 0] - a[:, -1]).max())
        self.assertLessEqual(step, float(np.abs(np.diff(a, axis=1)).max()) + 1e-9)

    def test_the_wave_shows_in_the_face_map_on_the_faces(self):
        m = render(face=dict(tilt_deg=0.0, wave_mm=2.0), face_level=1.0)
        b = blocks_a()[0]
        ys, xs = self.interior(b)
        got = m["face"][ys][:, xs] - 0.5
        self.assertLess(float(np.abs(got - m["wave"][ys][:, xs]).max()), 1e-9)

    def test_the_straight_edge_survives_the_relief(self):
        blocks = blocks_a()
        m = render(face=dict(tilt_deg=1.5, tilt_cap_mm=4.0, pillow_mm=1.0, wave_mm=1.0), face_level=1.0,
                   blocks=blocks)
        out = tc.edge_straightness(m["height"], blocks, corner=6, reach=16, level=0.5 * m["info"]["face_level"])
        self.assertLess(out["max_dev_px"], 1.0)

    def test_write_keeps_the_face_map_at_16_bit(self):
        import tempfile
        from PIL import Image
        m = render(face=dict(FACE), face_level=1.0)
        with tempfile.TemporaryDirectory() as d:
            files = bl.write(d, m, blocks_a())
            self.assertIn("face", files)
            self.assertNotIn("wave", files)
            img = Image.open(files["face"])
            self.assertEqual(np.asarray(img).dtype, np.uint16)


class HaloTests(unittest.TestCase):
    def test_no_halo_map_unless_asked(self):
        self.assertNotIn("halo", render(face=None, face_level=1.0))

    def test_the_halo_is_clear_in_the_joint_dark_at_its_edge_and_gone_far_away(self):
        m = render(face=None, face_level=1.0, halo_mm=20.0)
        halo, joint = m["halo"], m["joint"] > 0.5
        self.assertTrue(np.all(halo[joint] == 1.0))
        near = np.zeros_like(joint)
        near[:, 1:] |= joint[:, :-1]
        near &= ~joint
        self.assertLess(float(halo[near].mean()), 0.5)
        self.assertGreater(float((halo > 0.999).mean()), 0.3)
        self.assertGreaterEqual(float(halo.min()), 0.0)

    def test_a_wider_halo_darkens_more_pixels(self):
        small = render(face=None, face_level=1.0, halo_mm=10.0)["halo"]
        large = render(face=None, face_level=1.0, halo_mm=40.0)["halo"]
        self.assertGreater(float((large < 0.9).mean()), float((small < 0.9).mean()))

    def test_the_halo_wraps_like_the_tile(self):
        m = render(face=None, face_level=1.0, halo_mm=20.0)
        self.assertLess(abs(float(m["halo"][0, 5]) - float(m["halo"][-1, 5])), 0.5)


class PlanCoursesTests(unittest.TestCase):
    def test_the_plinth_courses_hold_two_blocks_each_and_bond(self):
        blocks = bl.plan_courses(461.0, 1024, [341, 297, 386], (0.95, 1.30), 0.3, seed=2)
        rows = {}
        for b in blocks:
            rows.setdefault((b["y0"], b["y1"]), []).append(b)
        self.assertEqual(sorted(rows), [(0, 341), (341, 638), (638, 1024)])
        for course in rows.values():
            self.assertEqual(len(course), 2)
            self.assertEqual(sum(b["x1"] - b["x0"] for b in course), 1024)
            for b in course:
                metres = (b["x1"] - b["x0"]) / 461.0
                self.assertTrue(0.95 - 1e-9 <= metres <= 1.30 + 1e-9, metres)
        keys = sorted(rows)
        cuts = [[b["x0"] % 1024 for b in rows[k]] for k in keys]
        for i in range(3):
            for x in cuts[i]:
                for y in cuts[(i + 1) % 3]:
                    d = abs(x - y)
                    self.assertGreaterEqual(min(d, 1024 - d), int(round(0.3 * 461.0)) - 1)

    def test_the_same_seed_gives_the_same_plan(self):
        a = bl.plan_courses(461.0, 1024, [341, 297, 386], (0.95, 1.30), 0.3, seed=4)
        b = bl.plan_courses(461.0, 1024, [341, 297, 386], (0.95, 1.30), 0.3, seed=4)
        self.assertEqual(a, b)


class JointAtRowTests(unittest.TestCase):
    def test_the_plinth_top_row_is_a_course_boundary(self):
        row = pt.row_for_height(3.0594, 1.0 / 0.45, 1024)
        self.assertEqual(row, 638)
        blocks = bl.plan_courses(461.0, 1024, [341, 297, 386], (0.95, 1.30), 0.3, seed=2)
        self.assertTrue(pt.joint_at_row(blocks, row, 1024))

    def test_a_row_inside_a_course_is_no_joint(self):
        blocks = [{"x0": 0, "x1": 8, "y0": 0, "y1": 8, "id": 0}, {"x0": 0, "x1": 8, "y0": 8, "y1": 16, "id": 1}]
        self.assertTrue(pt.joint_at_row(blocks, 8, 16))
        self.assertTrue(pt.joint_at_row(blocks, 0, 16))
        self.assertFalse(pt.joint_at_row(blocks, 4, 16))

    def test_the_courses_below_the_top_row_are_the_wanted_heights_in_metres(self):
        heights = pt.course_heights_below(638, [341, 297, 386], 1024, 1.0 / 0.45)
        self.assertEqual(len(heights), 4)
        for got, want in zip(heights, [0.837, 0.740, 0.645, 0.837]):
            self.assertAlmostEqual(got, want, delta=0.002)


if __name__ == "__main__":
    unittest.main()


class PlanFolderTests(unittest.TestCase):
    def test_plan_writes_a_layout_folder_that_build_can_read(self):
        import json
        import tempfile
        import painter_stone_layout as psl
        with tempfile.TemporaryDirectory() as d:
            info = psl.plan(d, [341, 297, 386], (0.95, 1.30), 0.3, 2)
            meta = json.load(open(os.path.join(d, "layout.json")))
            self.assertEqual(meta["blocks"], bl.plan_courses(461.0, 1024, [341, 297, 386], (0.95, 1.30), 0.3, 2))
            self.assertEqual(info["courses_px"], [341, 297, 386])
            self.assertEqual(meta["info"]["size"], 1024)


class JudgeTests(unittest.TestCase):
    def test_judge_reports_the_round_4_numbers(self):
        import json
        import tempfile
        from PIL import Image
        import painter_stone_recipes as pr
        blocks = [{"x0": 0, "x1": 32, "y0": 0, "y1": 32, "id": 0}, {"x0": 32, "x1": 64, "y0": 0, "y1": 32, "id": 1},
                  {"x0": 16, "x1": 48, "y0": 32, "y1": 64, "id": 2}, {"x0": 48, "x1": 80, "y0": 32, "y1": 64, "id": 3}]
        with tempfile.TemporaryDirectory() as d:
            json.dump({"blocks": blocks, "info": {"size": 64}}, open(d + "/layout.json", "w"))
            lum = np.zeros((64, 64, 3), dtype=np.uint8)
            lum[:32, :32] = 100
            lum[:32, 32:] = 110
            lum[32:, 16:48] = 120
            lum[32:, :16] = 130
            lum[32:, 48:] = 130
            Image.fromarray(lum).save("%s/%s_d.png" % (d, pr.SET_NAME))
            n = np.zeros((64, 64, 3), dtype=np.uint8)
            n[..., :2] = 128
            n[..., 2] = 255
            Image.fromarray(n).save("%s/%s_n.png" % (d, pr.SET_NAME))
            out = pr.judge(d, d, margin_px=2)
        for key in ("face_tilt", "face_tilt_median", "tile_tilt", "block_means", "block_spread", "cloud_contrast"):
            self.assertIn(key, out)
        self.assertEqual(out["block_means"], [100.0, 110.0, 120.0, 130.0])
        self.assertEqual(out["block_spread"], 30.0)


class RecipeHaloTests(unittest.TestCase):
    def test_the_halo_layer_stands_after_the_joint_occlusion_only_when_set(self):
        import painter_stone_recipes as pr
        p = dict(pr.PARITY, **pr.ROUND3)
        names = [l["name"] for l in pr.recipe("t", "x", p)["layers"]]
        self.assertNotIn("halo", names)
        p["halo"] = 0.3
        rec = pr.recipe("t", "x", p)
        names = [l["name"] for l in rec["layers"]]
        self.assertEqual(names.index("halo"), names.index("joint ao") + 1)
        halo = rec["layers"][names.index("halo")]
        self.assertEqual((halo["blend"], halo["opacity"], halo["source"]), ("Multiply", 0.3, {"map": "halo"}))


class RecipeFaceTests(unittest.TestCase):
    def test_the_face_layer_adds_to_the_height_after_the_relief_only_when_set(self):
        import painter_stone_recipes as pr
        p = dict(pr.PARITY, **pr.ROUND3)
        names = [l["name"] for l in pr.recipe("t", "x", p)["layers"]]
        self.assertNotIn("face relief", names)
        p["face_map"] = True
        rec = pr.recipe("t", "x", p)
        names = [l["name"] for l in rec["layers"]]
        self.assertEqual(names.index("face relief"), names.index("relief") + 1)
        face = rec["layers"][names.index("face relief")]
        self.assertEqual((face["channels"], face["blend"], face["opacity"], face["source"]),
                         (["Height"], "LinearDodge", 1.0, {"map": "face"}))


class AssembleSetTests(unittest.TestCase):
    def make(self, folder, name):
        from PIL import Image
        os.makedirs(folder, exist_ok=True)
        for kind in "dns":
            Image.fromarray(np.full((4, 4, 3), ord(kind) % 255, dtype=np.uint8)).save("%s/%s_%s.png" % (folder, name, kind))

    def test_the_three_sets_land_under_their_own_names(self):
        import tempfile
        import painter_stone_views as pv
        import painter_stone_recipes as pr
        with tempfile.TemporaryDirectory() as d:
            self.make(d + "/stone", pr.SET_NAME)
            self.make(d + "/plinth", pr.SET_NAME)
            self.make(d + "/trim", tc.TRIM)
            self.make(d + "/trim", tc.WALL)  # a donor folder holds other sets too: they are not taken
            pv.assemble_set(d + "/out", d + "/stone", d + "/plinth", d + "/trim")
            got = sorted(os.listdir(d + "/out"))
            want = sorted("%s_%s.png" % (n, k) for n in (tc.WALL, tc.PLINTH, tc.TRIM) for k in "dns")
            self.assertEqual(got, want)

    def test_the_plinth_files_are_the_plinth_working_folder_renamed(self):
        import tempfile
        from PIL import Image
        import painter_stone_views as pv
        import painter_stone_recipes as pr
        with tempfile.TemporaryDirectory() as d:
            self.make(d + "/stone", pr.SET_NAME)
            self.make(d + "/plinth", pr.SET_NAME)
            self.make(d + "/trim", tc.TRIM)
            Image.fromarray(np.full((4, 4, 3), 7, dtype=np.uint8)).save("%s/plinth/%s_d.png" % (d, pr.SET_NAME))
            pv.assemble_set(d + "/out", d + "/stone", d + "/plinth", d + "/trim")
            self.assertEqual(int(np.asarray(Image.open("%s/out/%s_d.png" % (d, tc.PLINTH)))[0, 0, 0]), 7)
            self.assertEqual(int(np.asarray(Image.open("%s/out/%s_d.png" % (d, tc.WALL)))[0, 0, 0]), ord("d") % 255)

    def test_a_missing_file_stops_the_copy(self):
        import tempfile
        import painter_stone_views as pv
        import painter_stone_recipes as pr
        with tempfile.TemporaryDirectory() as d:
            self.make(d + "/stone", pr.SET_NAME)
            self.make(d + "/trim", tc.TRIM)
            with self.assertRaises(FileNotFoundError):
                pv.assemble_set(d + "/out", d + "/stone", d + "/plinth", d + "/trim")


class SoftLimitTests(unittest.TestCase):
    def test_a_small_value_passes_almost_unchanged_and_a_large_one_stops_at_the_limit(self):
        x = np.array([-5.0, -0.1, 0.0, 0.05, 0.1, 0.3, 5.0])
        y = bl.soft_limit(x, 0.48)
        self.assertLess(float(np.abs(y[1:5] - x[1:5]).max()), 0.002)
        self.assertLessEqual(float(np.abs(y).max()), 0.48)
        self.assertTrue(np.all(np.diff(y) > 0))

    def test_the_face_limit_keeps_a_strong_relief_inside_the_range(self):
        face = dict(tilt_deg=1.5, tilt_cap_mm=4.0, pillow_mm=0.0, wave_mm=3.4, limit=0.48)
        m = render(face=face, face_level=1.0)
        self.assertGreaterEqual(float(m["face"].min()), 0.02)
        self.assertLessEqual(float(m["face"].max()), 0.98)
        free = dict(face, limit=None)
        with self.assertRaises(ValueError):
            render(face=free, face_level=1.0)

    def test_the_limit_leaves_the_planes_alone_when_they_are_small(self):
        face = dict(FACE, limit=0.48)
        a = render(face=face, face_level=1.0)["face"]
        b = render(face=dict(FACE), face_level=1.0)["face"]
        self.assertLess(float(np.abs(a - b).max()), 0.01)


class CornerBoxTests(unittest.TestCase):
    def test_the_box_is_centred_on_the_block_corner_nearest_the_middle_and_inside_the_tile(self):
        import painter_stone_views as pv
        blocks = [{"x0": 0, "x1": 30, "y0": 0, "y1": 30, "id": 0}, {"x0": 30, "x1": 100, "y0": 0, "y1": 30, "id": 1},
                  {"x0": 10, "x1": 60, "y0": 30, "y1": 100, "id": 2}, {"x0": 60, "x1": 110, "y0": 30, "y1": 100, "id": 3}]
        x, y = pv.corner_box(blocks, 100, 100, 20)
        self.assertTrue(0 <= x <= 80 and 0 <= y <= 80)
        self.assertEqual((x + 10, y + 10), (60, 30))

    def test_the_box_scales_to_the_output_size(self):
        import painter_stone_views as pv
        blocks = [{"x0": 0, "x1": 50, "y0": 0, "y1": 50, "id": 0}, {"x0": 50, "x1": 100, "y0": 0, "y1": 50, "id": 1},
                  {"x0": 25, "x1": 75, "y0": 50, "y1": 100, "id": 2}, {"x0": 75, "x1": 125, "y0": 50, "y1": 100, "id": 3}]
        x, y = pv.corner_box(blocks, 100, 200, 40)
        self.assertEqual((x + 20, y + 20), (100, 100))
