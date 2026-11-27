"""Tests for tools/oneoff/convert_painter_stone_sets.py: the pure array steps that turn a Painter export
into the Rivendell set form the engine reads: diffuse mean, _s as R metallic, G gloss, B ambient
occlusion, and a normal map with green down (the DirectX form). There is no _n_flipgreen file."""
import json
import os
import sys
import tempfile
import unittest

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import convert_painter_stone_sets as c  # noqa: E402


class DiffuseTests(unittest.TestCase):
    def test_match_mean_hits_target_per_channel(self):
        rng = np.random.default_rng(1)
        img = rng.uniform(60, 160, (32, 32, 3))
        out = c.match_mean(img, (199, 184, 158))
        np.testing.assert_allclose(out.reshape(-1, 3).mean(0), (199, 184, 158), atol=1.0)

    def test_ao_multiply_darkens_only_where_occluded(self):
        base = np.full((2, 2, 3), 200.0)
        ao = np.array([[255.0, 255.0], [0.0, 255.0]])
        out = c.apply_ao(base, ao, 0.4)
        self.assertEqual(out[0, 0, 0], 200.0)
        self.assertAlmostEqual(out[1, 0, 0], 120.0)


class SpecTests(unittest.TestCase):
    def test_packing_is_metallic_gloss_occlusion(self):
        rough = np.array([[0.0, 100.0], [200.0, 255.0]])
        ao = np.array([[255.0, 255.0], [40.0, 0.0]])
        s = c.spec_from_maps(rough, ao)
        self.assertTrue((s[..., 0] == 0).all())
        np.testing.assert_allclose(s[..., 1], 255 - rough)
        np.testing.assert_allclose(s[..., 2], ao)

    def test_blue_is_the_occlusion_map_not_roughness(self):
        rng = np.random.default_rng(3)
        rough = rng.uniform(100, 200, (32, 32))
        ao = np.full((32, 32), 255.0)
        ao[:, 10] = 30.0
        s = c.spec_from_maps(rough, ao)
        self.assertTrue((s[:, 10, 2] == 30).all())
        self.assertTrue((s[:, 11, 2] == 255).all())

    def test_gloss_target_moves_the_mean_and_keeps_the_spread(self):
        rng = np.random.default_rng(4)
        rough = rng.normal(180, 10, (64, 64)).clip(0, 255)
        ao = np.full((64, 64), 240.0)
        plain = c.spec_from_maps(rough, ao)
        s = c.spec_from_maps(rough, ao, gloss_mean=105.0)
        self.assertAlmostEqual(s[..., 1].mean(), 105.0, delta=0.6)
        self.assertAlmostEqual(s[..., 1].std(), plain[..., 1].std(), delta=0.5)

    def test_gloss_stays_in_byte_range(self):
        rough = np.zeros((8, 8))
        s = c.spec_from_maps(rough, np.full((8, 8), 255.0), gloss_mean=250.0)
        self.assertLessEqual(s[..., 1].max(), 255.0)
        self.assertGreaterEqual(s[..., 1].min(), 0.0)

    def test_gloss_std_limit_reduces_a_wide_spread(self):
        rng = np.random.default_rng(5)
        rough = rng.normal(150, 40, (64, 64)).clip(0, 255)
        s = c.spec_from_maps(rough, np.full((64, 64), 255.0), gloss_mean=100.0, gloss_std=12.0)
        self.assertAlmostEqual(s[..., 1].std(), 12.0, delta=1.0)
        self.assertAlmostEqual(s[..., 1].mean(), 100.0, delta=0.6)

    def test_retarget_gloss_changes_only_green(self):
        s = np.zeros((4, 4, 3))
        s[..., 1] = 40.0
        s[..., 2] = 200.0
        out = c.retarget_gloss(s, 100.0)
        self.assertAlmostEqual(out[..., 1].mean(), 100.0, delta=0.5)
        self.assertTrue((out[..., 0] == 0).all())
        self.assertTrue((out[..., 2] == 200).all())


class NormalTests(unittest.TestCase):
    def test_flip_green_inverts_only_g(self):
        n = np.array([[[10, 20, 30]]], dtype=np.uint8)
        out = c.flip_green(n)
        self.assertEqual(tuple(out[0, 0]), (10, 235, 30))

    def test_engine_normal_flips_an_opengl_export_and_keeps_a_directx_one(self):
        n = np.array([[[10, 20, 30]]], dtype=np.uint8)
        self.assertEqual(tuple(c.engine_normal(n, "down")[0, 0]), (10, 20, 30))
        self.assertEqual(tuple(c.engine_normal(n, "up")[0, 0]), (10, 235, 30))

    def test_engine_normal_rejects_a_wrong_name(self):
        with self.assertRaises(ValueError):
            c.engine_normal(np.zeros((1, 1, 3), np.uint8), "left")


class SeamTests(unittest.TestCase):
    def test_seam_ratio_is_about_one_on_a_wrapped_tile_and_high_on_a_cut(self):
        x = np.linspace(0, 2 * np.pi, 64, endpoint=False)
        tile = (np.sin(x)[None, :] + np.cos(2 * x)[:, None]) * 40 + 128
        self.assertLess(c.seam_ratio(tile), 2.5)
        cut = np.tile(np.linspace(0, 255, 64)[None, :], (64, 1))
        self.assertGreater(c.seam_ratio(cut), 10)


class ConvertTests(unittest.TestCase):
    def _export(self, d, size=32):
        os.makedirs(d)
        rng = np.random.default_rng(7)
        base = rng.integers(90, 170, (size, size, 3)).astype(np.uint8)
        Image.fromarray(base).save(os.path.join(d, "rivendell_tile_baseColor.png"))
        n = np.zeros((size, size, 3), np.uint8)
        n[..., 0], n[..., 1], n[..., 2] = 120, 100, 250
        Image.fromarray(n).save(os.path.join(d, "rivendell_tile_normal.png"))
        Image.fromarray(np.full((size, size), 170, np.uint8)).save(os.path.join(d, "rivendell_tile_roughness.png"))
        ao = np.full((size, size), 250, np.uint8)
        ao[:, 5] = 60
        Image.fromarray(ao).save(os.path.join(d, "rivendell_tile_ambientOcclusion.png"))

    def test_convert_one_writes_four_files_and_no_flipgreen(self):
        with tempfile.TemporaryDirectory() as t:
            exp = os.path.join(t, "exp")
            out = os.path.join(t, "out")
            self._export(exp)
            rep = c.convert_one(exp, out, "t_x_wall", "c7b89e", gloss_mean=100.0)
            names = sorted(os.listdir(out))
            self.assertEqual(names, ["t_x_wall_d.png", "t_x_wall_n.png", "t_x_wall_s.png"])
            s = np.asarray(Image.open(os.path.join(out, "t_x_wall_s.png")).convert("RGB")).astype(float)
            self.assertTrue((s[..., 0] == 0).all())
            self.assertAlmostEqual(s[..., 1].mean(), 100.0, delta=1.0)
            self.assertEqual(s[0, 5, 2], 60.0)
            n = np.asarray(Image.open(os.path.join(out, "t_x_wall_n.png")).convert("RGB"))
            self.assertEqual(int(n[0, 0, 1]), 100)  # a DirectX export keeps its green
            self.assertIn("spec_g", rep)
            json.dumps(rep)


if __name__ == "__main__":
    unittest.main()
