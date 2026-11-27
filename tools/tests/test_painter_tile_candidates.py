"""Tests for tools/painter/painter_tile_candidates.py: the pure steps of the candidate pipeline."""
import os
import sys
import tempfile
import unittest

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "painter"))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import painter_tile_candidates as pc  # noqa: E402

CFG = {
    "base": {"material": "smoothed_limestone_rock_surface", "params": {"normal_intensity": 0.1}},
    "layout": {"name": "LA_6c", "heights": [512, 512], "lengths": [512], "seed": 1, "stagger": 64},
    "tone": {"opacity": 0.2}, "joint": {"color": [0.3, 0.3, 0.3], "opacity": 0.6, "depth": 0.3}, "ao": True,
    "dressed_params": {"contrast": -0.5},
}


class SpecTests(unittest.TestCase):
    def test_wall_spec_carries_the_layers_and_the_layout_folder(self):
        s = pc.wall_spec("c1", CFG, "E:/x/exp", "E:/x/layouts")
        self.assertEqual(s["layout"], "E:/x/layouts/LA_6c")
        self.assertEqual(s["out"], "E:/x/exp/c1/wall")
        self.assertEqual(s["normal"], "Normal_DirectX")
        self.assertEqual(s["joint"]["depth"], 0.3)
        self.assertTrue(s["ao"])

    def test_dressed_spec_has_no_layout_and_merges_its_params(self):
        s = pc.dressed_spec("c1", CFG, "E:/x/exp")
        self.assertIsNone(s["layout"])
        self.assertEqual(s["base"]["params"], {"normal_intensity": 0.1, "contrast": -0.5})
        self.assertEqual(s["out"], "E:/x/exp/c1/dressed")
        self.assertEqual(CFG["base"]["params"], {"normal_intensity": 0.1})  # the config is not changed

    def test_a_tuned_candidate_has_no_layout(self):
        cfg = {"base": {"material": "cinderblock_wall", "params": {"tile": [3, 6]}}}
        s = pc.wall_spec("c2", cfg, "E:/x/exp", "E:/x/layouts")
        self.assertIsNone(s["layout"])
        self.assertNotIn("joint", s)


class TrimTests(unittest.TestCase):
    def test_engine_trim_turns_the_flipgreen_into_the_normal_and_follows_the_gloss(self):
        with tempfile.TemporaryDirectory() as t:
            up = np.zeros((4, 4, 3), np.uint8)
            up[..., 1] = 55
            down = np.zeros((4, 4, 3), np.uint8)
            down[..., 1] = 200
            Image.fromarray(up).save(os.path.join(t, "set_n.png"))
            Image.fromarray(down).save(os.path.join(t, "set_n_flipgreen.png"))
            sp = np.zeros((4, 4, 3), np.uint8)
            sp[..., 1] = 40
            sp[..., 2] = 230
            Image.fromarray(sp).save(os.path.join(t, "set_s.png"))
            pc.engine_trim(t, "set", 100.0)
            self.assertFalse(os.path.exists(os.path.join(t, "set_n_flipgreen.png")))
            g = np.asarray(Image.open(os.path.join(t, "set_n.png")))[0, 0, 1]
            self.assertEqual(int(g), 200)
            s = np.asarray(Image.open(os.path.join(t, "set_s.png"))).astype(float)
            self.assertAlmostEqual(s[..., 1].mean(), 100.0, delta=1.0)
            self.assertTrue((s[..., 2] == 230).all())


if __name__ == "__main__":
    unittest.main()
