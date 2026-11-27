#!/usr/bin/env python3
"""Unit tests for tools/oneoff/texture_stats.py.

Run:  python -m pytest tools/tests/test_texture_stats.py

Every image is synthetic and built in memory or in a temp folder, so the tests need no game files.
"""
import json
import os
import sys
import tempfile
import unittest

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "oneoff"))

import texture_stats as ts  # noqa: E402


class ColourStatsTests(unittest.TestCase):
    def test_solid_colour(self):
        r = ts.image_stats(Image.new("RGB", (8, 4), (200, 100, 50)), kind="color")
        self.assertEqual(r["size"], [8, 4])
        self.assertEqual(r["mode"], "RGB")
        self.assertEqual(r["mean_hex"], "#c86432")
        self.assertAlmostEqual(r["channels"]["R"]["mean"], 200.0, places=3)
        self.assertAlmostEqual(r["channels"]["B"]["std"], 0.0, places=3)

    def test_two_halves_mean_and_std(self):
        img = Image.new("RGB", (4, 2), (0, 0, 0))
        img.paste((255, 255, 255), (0, 0, 2, 2))
        r = ts.image_stats(img, kind="color")
        self.assertAlmostEqual(r["channels"]["G"]["mean"], 127.5, places=3)
        self.assertAlmostEqual(r["channels"]["G"]["std"], 127.5, places=3)

    def test_luminance_percentiles_on_gradient(self):
        img = Image.new("L", (256, 1))
        img.putdata(list(range(256)))
        r = ts.image_stats(img, kind="color")
        self.assertAlmostEqual(r["luminance"]["p5"], 12.75, delta=0.5)
        self.assertAlmostEqual(r["luminance"]["p50"], 127.5, delta=0.5)
        self.assertAlmostEqual(r["luminance"]["p95"], 242.25, delta=0.5)

    def test_saturation(self):
        grey = ts.image_stats(Image.new("RGB", (4, 4), (90, 90, 90)), kind="color")
        red = ts.image_stats(Image.new("RGB", (4, 4), (255, 0, 0)), kind="color")
        self.assertAlmostEqual(grey["mean_saturation"], 0.0, places=4)
        self.assertAlmostEqual(red["mean_saturation"], 1.0, places=4)

    def test_dominant_colours_and_shares(self):
        img = Image.new("RGB", (10, 10), (255, 0, 0))
        img.paste((0, 0, 255), (0, 0, 10, 3))
        r = ts.image_stats(img, kind="color")
        top = r["dominant"][0]
        self.assertEqual(top["hex"], "#ff0000")
        self.assertAlmostEqual(top["share"], 0.7, places=2)
        self.assertEqual(r["dominant"][1]["hex"], "#0000ff")
        self.assertAlmostEqual(sum(d["share"] for d in r["dominant"]), 1.0, places=3)
        self.assertLessEqual(len(r["dominant"]), 5)

    def test_alpha_zero_pixels_do_not_count(self):
        img = Image.new("RGBA", (4, 1), (255, 0, 0, 255))
        img.putpixel((2, 0), (0, 255, 0, 0))
        img.putpixel((3, 0), (0, 255, 0, 0))
        r = ts.image_stats(img, kind="color")
        self.assertEqual(r["mean_hex"], "#ff0000")
        self.assertEqual(r["counted_pixels"], 2)
        self.assertEqual(r["dominant"][0]["hex"], "#ff0000")

    def test_palette_and_grey_modes_load(self):
        r = ts.image_stats(Image.new("P", (2, 2), 0).convert("P"), kind="color")
        self.assertEqual(r["size"], [2, 2])
        g = ts.image_stats(Image.new("L", (2, 2), 128), kind="color")
        self.assertEqual(g["mean_hex"], "#808080")


class DataMapTests(unittest.TestCase):
    def test_data_map_reports_per_channel_percentiles(self):
        img = Image.new("RGB", (256, 1))
        img.putdata([(i, 255 - i, 10) for i in range(256)])
        r = ts.image_stats(img, kind="data")
        self.assertNotIn("dominant", r)
        self.assertNotIn("mean_hex", r)
        self.assertAlmostEqual(r["channels"]["R"]["p50"], 127.5, delta=0.5)
        self.assertAlmostEqual(r["channels"]["G"]["mean"], 127.5, places=3)
        self.assertAlmostEqual(r["channels"]["B"]["p5"], 10.0, places=3)
        self.assertAlmostEqual(r["channels"]["R"]["p95"], 242.25, delta=0.5)

    def test_kind_from_name(self):
        self.assertEqual(ts.kind_from_name("t_stone_a_d.png"), "color")
        self.assertEqual(ts.kind_from_name("T_Stone_A_S.png"), "data")
        self.assertEqual(ts.kind_from_name("t_stone_a_n.png"), "data")
        self.assertEqual(ts.kind_from_name("rock_normal.tga"), "data")
        self.assertEqual(ts.kind_from_name("rock_specular.tga"), "data")
        self.assertEqual(ts.kind_from_name("rock_albedo.tga"), "color")


class CommandLineTests(unittest.TestCase):
    def test_folder_in_json_out(self):
        with tempfile.TemporaryDirectory() as d:
            src = os.path.join(d, "in")
            os.mkdir(src)
            Image.new("RGB", (4, 4), (10, 20, 30)).save(os.path.join(src, "a_d.png"))
            Image.new("RGB", (4, 4), (1, 2, 3)).save(os.path.join(src, "a_s.png"))
            open(os.path.join(src, "note.txt"), "w").write("x")
            out = os.path.join(d, "out.json")
            code = ts.main([src, "--out", out])
            self.assertEqual(code, 0)
            data = json.load(open(out))
            names = sorted(os.path.basename(e["path"]) for e in data["files"])
            self.assertEqual(names, ["a_d.png", "a_s.png"])
            by = {os.path.basename(e["path"]): e for e in data["files"]}
            self.assertEqual(by["a_d.png"]["kind"], "color")
            self.assertEqual(by["a_s.png"]["kind"], "data")

    def test_bad_file_is_reported_not_fatal(self):
        with tempfile.TemporaryDirectory() as d:
            bad = os.path.join(d, "bad_d.png")
            open(bad, "wb").write(b"not an image")
            good = os.path.join(d, "good_d.png")
            Image.new("RGB", (2, 2), (5, 5, 5)).save(good)
            out = os.path.join(d, "o.json")
            ts.main([bad, good, "--out", out])
            data = json.load(open(out))
            by = {os.path.basename(e["path"]): e for e in data["files"]}
            self.assertIn("error", by["bad_d.png"])
            self.assertNotIn("error", by["good_d.png"])


if __name__ == "__main__":
    unittest.main()
