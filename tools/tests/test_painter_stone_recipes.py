"""Tests for the pure parts of tools/painter/painter_stone_recipes.py (round 2b)."""
import json
import os
import sys
import tempfile
import unittest

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "painter"))
import painter_stone_recipes as pr  # noqa: E402


def tc_lum(r, g, b):
    return 0.299 * r + 0.587 * g + 0.114 * b


class ColourTests(unittest.TestCase):
    def test_add_colour_reaches_the_target_when_added_in_linear_light(self):
        base, target = (160.0, 158.0, 152.0), (195.0, 187.0, 178.0)
        c = pr.add_colour(base, target)
        got = pr.enc(pr.lin(np.array(base) / 255.0) + pr.lin(np.array(c))) * 255.0
        self.assertTrue(np.allclose(got, target, atol=0.6), got)

    def test_a_darker_target_gives_no_negative_step(self):
        self.assertEqual(pr.add_colour((200, 200, 200), (100, 100, 100)), [0.0, 0.0, 0.0])

    def test_lin_and_enc_are_inverse(self):
        x = np.linspace(0, 1, 11)
        self.assertTrue(np.allclose(pr.enc(pr.lin(x)), x, atol=1e-9))


class RecipeTests(unittest.TestCase):
    def names(self, **over):
        p = dict(pr.PARITY, **over)
        return [l["name"] for l in pr.recipe("x", "E:/layout", p)["layers"]]

    def test_stack_order_is_colour_grain_tone_cloud_then_marks_then_maps(self):
        n = self.names()
        self.assertEqual(n[:4], ["colour", "grain", "tone", "cloud"])
        self.assertLess(n.index("stain"), n.index("joint"))
        self.assertLess(n.index("joint"), n.index("joint ao"))
        self.assertEqual(n[-1], "occlusion")

    def test_weathering_layers_sit_after_the_cloud_and_only_when_asked(self):
        self.assertNotIn("streaks", self.names())
        n = self.names(streak={"color": [0.5, 0.5, 0.5], "opacity": 0.3},
                       lower={"color": [0.5, 0.5, 0.5], "opacity": 0.2})
        self.assertEqual(n[n.index("cloud") + 1:n.index("cloud") + 3], ["streaks", "lower edge"])

    def test_every_map_and_mask_is_a_layout_key_the_painter_script_can_load(self):
        keys = {"id", "cloud", "streak", "lower", "stain", "joint", "ao", "edgelight", "chip", "height"}
        p = dict(pr.PARITY, streak={"color": [0.5] * 3, "opacity": 0.3}, lower={"color": [0.5] * 3, "opacity": 0.2})
        for l in pr.recipe("x", "E:/layout", p)["layers"]:
            if l.get("source", {}).get("map"):
                self.assertIn(l["source"]["map"], keys)
            if l.get("mask"):
                self.assertIn(l["mask"], keys)

    def test_grain_is_a_filter_entry_on_base_colour_only(self):
        g = [l for l in pr.recipe("x", "L", dict(pr.PARITY))["layers"] if l["name"] == "grain"][0]
        self.assertEqual(g["filter"], "sharpen/sharpen")
        self.assertEqual(g["channels"], ["BaseColor"])


class ParityTests(unittest.TestCase):
    def test_parity_reports_each_quantity_against_its_limit(self):
        ref = {"lum_mean": 188.4, "red_minus_blue": 17.0, "joint_contrast": 31.7, "joint_width_px": 5.0,
               "lum_p5_p95": 40.4}
        ours = dict(ref, lum_mean=191.0, joint_contrast=33.0)
        out = pr.parity(ours, ref)
        self.assertFalse(out["lum_mean"]["ok"])
        self.assertTrue(out["joint_contrast"]["ok"])
        self.assertTrue(out["lum_p5_p95"]["ok"])


class MaterialTests(unittest.TestCase):
    def test_material_params_override_the_base_material_and_keep_the_rest(self):
        p = dict(pr.PARITY, material_params={"normal_intensity": 0.05, "height_range": 0.15})
        rec = pr.recipe("t", "L", p)
        self.assertEqual(rec["base"]["params"]["normal_intensity"], 0.05)
        self.assertEqual(rec["base"]["params"]["height_range"], 0.15)
        self.assertEqual(rec["base"]["params"]["contrast"], -0.6)
        self.assertEqual(pr.MATERIAL["params"]["normal_intensity"], 0.1)

    def test_a_missing_override_leaves_the_round_2b_material(self):
        self.assertEqual(pr.recipe("t", "L", dict(pr.PARITY))["base"], pr.MATERIAL)

    def test_the_edge_and_chip_light_colours_come_from_the_parameters(self):
        p = dict(pr.PARITY, edge_colour=[1.0, 0.95, 0.85])
        rec = pr.recipe("t", "L", p)
        edge = [l for l in rec["layers"] if l["name"] == "edge light"][0]
        self.assertEqual(edge["source"]["color"], [1.0, 0.95, 0.85])


def write_set(folder, lum_rows, tilt_rows=None):
    """A 16 px set: the diffuse is grey by row band, the normal is flat except in the rows of tilt_rows."""
    os.makedirs(folder, exist_ok=True)
    d = np.zeros((16, 16, 3), np.uint8)
    for (a, b), v in lum_rows.items():
        d[a:b] = v
    n = np.dstack([np.full((16, 16), 128), np.full((16, 16), 128), np.full((16, 16), 255)]).astype(np.uint8)
    for (a, b), v in (tilt_rows or {}).items():
        n[a:b, :, 0] = v
    s = np.dstack([np.zeros((16, 16)), np.full((16, 16), 100), np.full((16, 16), 255)]).astype(np.uint8)
    for k, arr in (("d", d), ("n", n), ("s", s)):
        Image.fromarray(arr).save("%s/%s_%s.png" % (folder, pr.SET_NAME, k))


def write_layout(folder, size=32, chips=()):
    os.makedirs(folder, exist_ok=True)
    blocks = [{"x0": 0, "x1": 16, "y0": 0, "y1": 16, "id": 0}, {"x0": 16, "x1": 32, "y0": 0, "y1": 16, "id": 1},
              {"x0": 0, "x1": 32, "y0": 16, "y1": 32, "id": 2}]
    h = np.zeros((size, size))
    for b in blocks:
        h[b["y0"] + 3:b["y1"] - 3, b["x0"] + 3:b["x1"] - 3] = 1.0
    Image.fromarray(np.rint(h * 65535).astype(np.uint16)).save(folder + "/layout_height.png")
    json.dump({"blocks": blocks, "files": {}, "info": {"size": size, "chips": [{"block": c} for c in chips]}},
              open(folder + "/layout.json", "w"))


class JudgeTests(unittest.TestCase):
    def test_course_and_band_numbers_of_a_set(self):
        with tempfile.TemporaryDirectory() as t:
            write_set(t + "/s", {(0, 8): 180, (8, 16): 190})
            write_layout(t + "/l")
            j = pr.judge(t + "/s", t + "/l", margin_px=1)
            self.assertEqual([round(v) for v in j["course_means"]], [180, 190])
            self.assertEqual(j["course_spread"], 10.0)
            self.assertEqual(j["band_spread"], 0.0)

    def test_face_tilt_looks_only_inside_the_blocks(self):
        with tempfile.TemporaryDirectory() as t:
            write_set(t + "/s", {(0, 16): 150}, {(0, 2): 255})
            write_layout(t + "/l")
            j = pr.judge(t + "/s", t + "/l", margin_px=3)
            self.assertAlmostEqual(j["face_tilt"], 0.0, delta=0.01)
            self.assertGreater(j["tile_tilt"], 0.05)


class LayoutEdgeTests(unittest.TestCase):
    def test_a_straight_layout_reports_no_bad_edge_and_the_chip_count(self):
        with tempfile.TemporaryDirectory() as t:
            write_layout(t, chips=(1,))
            e = pr.layout_edge(t, corner=2, reach=8)
            self.assertEqual(e["bad_edges"], 0)
            self.assertEqual(e["chipped_blocks"], 1)
            self.assertEqual(e["blocks"], 3)
            self.assertEqual(e["edges"], 8)


class Round3Tests(unittest.TestCase):
    def test_round3_values_calm_the_streaks_the_lower_band_and_the_clouds(self):
        p = dict(pr.PARITY, **pr.ROUND3)
        rec = pr.recipe("t", "L", p)
        by = {l["name"]: l for l in rec["layers"] if "name" in l}
        self.assertLessEqual(by["streaks"]["opacity"], 0.10)
        self.assertLessEqual(by["lower edge"]["opacity"], 0.08)
        self.assertAlmostEqual(by["cloud"]["opacity"], 0.10)
        self.assertLessEqual(by["relief"]["opacity"], 1.0)

    def test_round3_lowers_the_material_relief_below_round_2b(self):
        base = pr.recipe("t", "L", dict(pr.PARITY, **pr.ROUND3))["base"]["params"]
        self.assertLess(base["normal_intensity"], pr.MATERIAL["params"]["normal_intensity"])
        self.assertLess(base["height_range"], pr.MATERIAL["params"]["height_range"])

    def test_round3_target_is_a_warm_ivory_of_luminance_190(self):
        r, g, b = pr.ROUND3["target"]
        self.assertAlmostEqual(tc_lum(r, g, b), 190.0, delta=1.5)
        self.assertGreaterEqual(r - b, 23)


if __name__ == "__main__":
    unittest.main()
