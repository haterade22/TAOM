"""Tests for tools/painter/trim_bake_check.py: the numbers that compare a Painter bake with a reference."""
import os
import struct
import sys
import tempfile
import unittest
import zlib

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "painter"))
import trim_bake_check as tb  # noqa: E402


def write_png(path, arr, color_type):
    """A minimal PNG writer for the tests: arr is uint8 or uint16, (h, w) or (h, w, c), filter 0."""
    depth = 16 if arr.dtype == np.uint16 else 8
    h, w = arr.shape[:2]
    rows = arr.reshape(h, -1)
    raw = b""
    for r in rows:
        data = r.astype(">u2").tobytes() if depth == 16 else r.astype(np.uint8).tobytes()
        raw += b"\x00" + data

    def chunk(tag, body):
        return struct.pack(">I", len(body)) + tag + body + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, depth, color_type, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw)))
        f.write(chunk(b"IEND", b""))


class ReadPng(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()

    def test_16_bit_rgb_keeps_all_bits(self):
        a = np.arange(4 * 3 * 3, dtype=np.uint16).reshape(4, 3, 3) * 1000 + 7
        p = os.path.join(self.dir, "a.png")
        write_png(p, a, 2)
        got = tb.read_png(p)
        self.assertEqual(got.dtype, np.uint16)
        self.assertTrue(np.array_equal(got, a))

    def test_8_bit_gray(self):
        a = (np.arange(20, dtype=np.uint8) * 12).reshape(4, 5)
        p = os.path.join(self.dir, "g.png")
        write_png(p, a, 0)
        got = tb.read_png(p)
        self.assertEqual(got.shape, (4, 5))
        self.assertTrue(np.array_equal(got, a))

    def test_16_bit_gray(self):
        a = np.array([[0, 65535], [12345, 40000]], dtype=np.uint16)
        p = os.path.join(self.dir, "g16.png")
        write_png(p, a, 0)
        self.assertTrue(np.array_equal(tb.read_png(p), a))

    def test_filters_are_undone(self):
        """A file written by PIL uses Sub, Up and Paeth filters: the reader must undo them."""
        from PIL import Image
        rng = np.random.default_rng(3)
        a = rng.integers(0, 255, (32, 40, 3), dtype=np.uint8)
        p = os.path.join(self.dir, "pil.png")
        Image.fromarray(a).save(p)
        self.assertTrue(np.array_equal(tb.read_png(p), a))

    def test_unit_scales_by_bit_depth(self):
        self.assertAlmostEqual(float(tb.unit(np.array([255], dtype=np.uint8))[0]), 1.0)
        self.assertAlmostEqual(float(tb.unit(np.array([65535], dtype=np.uint16))[0]), 1.0)


class Numbers(unittest.TestCase):
    def test_box_down_averages_blocks(self):
        a = np.array([[1, 3, 5, 7], [1, 3, 5, 7], [2, 2, 8, 8], [2, 2, 8, 8]], dtype=float)
        got = tb.box_down(a, 2)
        self.assertTrue(np.allclose(got, [[2, 6], [2, 8]]))

    def test_pearson_of_a_scaled_copy_is_one(self):
        a = np.random.default_rng(1).random((16, 16))
        self.assertAlmostEqual(tb.pearson(a, 3 * a + 2), 1.0, places=9)
        self.assertAlmostEqual(tb.pearson(a, -a), -1.0, places=9)

    def test_pearson_of_a_constant_is_zero_not_nan(self):
        self.assertEqual(tb.pearson(np.ones((4, 4)), np.arange(16.0).reshape(4, 4)), 0.0)

    def test_fit_scale_recovers_slope_and_offset(self):
        a = np.random.default_rng(2).random((16, 16))
        slope, offset = tb.fit_scale(a, 0.25 * a + 0.1)
        self.assertAlmostEqual(slope, 0.25, places=9)
        self.assertAlmostEqual(offset, 0.1, places=9)

    def test_wrap_ratio_is_one_for_a_wrapping_ramp_and_large_for_a_seam(self):
        n = 64
        wraps = np.tile(np.sin(2 * np.pi * np.arange(n) / n), (8, 1))
        self.assertLess(tb.wrap_ratio(wraps), 1.5)
        ramp = np.tile(np.arange(n, dtype=float), (8, 1))
        self.assertGreater(tb.wrap_ratio(ramp), 10.0)

    def test_orientation_search_finds_a_flipped_copy(self):
        rng = np.random.default_rng(5)
        ref = rng.random((20, 20))
        name, corr = tb.best_orientation(ref, ref[::-1])
        self.assertEqual(name, "flipud")
        self.assertAlmostEqual(corr, 1.0, places=9)
        name, _ = tb.best_orientation(ref, ref)
        self.assertEqual(name, "identity")


class Normals(unittest.TestCase):
    def tilted(self, tx, ty, size=8, green_down=False):
        n = np.array([tx, ty, 1.0])
        n = n / np.linalg.norm(n)
        rgb = np.empty((size, size, 3))
        rgb[...] = (n + 1) / 2
        if green_down:
            rgb[..., 1] = 1 - rgb[..., 1]
        return rgb

    def test_identical_maps_have_zero_angle(self):
        a = self.tilted(0.3, -0.2)
        rep = tb.normal_report(a, a)
        self.assertAlmostEqual(rep["mean_angle_deg"], 0.0, places=6)

    def test_green_sense_is_matched_before_comparing(self):
        up = self.tilted(0.3, -0.2)
        down = self.tilted(0.3, -0.2, green_down=True)
        rep = tb.normal_report(up, down, ref_green_down=False, test_green_down=True)
        self.assertAlmostEqual(rep["mean_angle_deg"], 0.0, places=6)
        wrong = tb.normal_report(up, down, ref_green_down=False, test_green_down=False)
        self.assertGreater(wrong["mean_angle_deg"], 10.0)

    def test_known_angle(self):
        a = self.tilted(0.0, 0.0)
        b = self.tilted(np.tan(np.radians(10.0)), 0.0)
        self.assertAlmostEqual(tb.normal_report(a, b)["mean_angle_deg"], 10.0, places=4)

    def test_channel_correlations_follow_the_content(self):
        rng = np.random.default_rng(7)
        n = rng.normal(size=(16, 16, 3)) * 0.3
        n[..., 2] = 1.0
        n /= np.linalg.norm(n, axis=-1, keepdims=True)
        rgb = (n + 1) / 2
        rep = tb.normal_report(rgb, rgb)
        self.assertAlmostEqual(rep["corr_r"], 1.0, places=9)
        self.assertAlmostEqual(rep["corr_g"], 1.0, places=9)

    def test_downsample_normals_renormalises(self):
        rgb = self.tilted(0.5, 0.0, size=4)
        d = tb.downsample_normal(rgb, 2)
        v = d * 2 - 1
        self.assertTrue(np.allclose(np.linalg.norm(v, axis=-1), 1.0, atol=1e-6))


class Margins(unittest.TestCase):
    def test_flat_margin_stats_use_rows_beside_the_strip_edges(self):
        h = np.zeros((40, 6))
        h[10:30] = 5.0
        stats = tb.margin_stats(h, {"A": (10, 30)}, inset=1, width=4)
        self.assertEqual(stats["A"]["min"], 5.0)
        self.assertEqual(stats["A"]["max"], 5.0)

    def test_margin_rows_skip_the_edge_rows(self):
        rows = tb.margin_rows(100, 200, inset=1, width=6)
        self.assertEqual(list(rows[:6]), [101, 102, 103, 104, 105, 106])
        self.assertEqual(list(rows[6:]), [193, 194, 195, 196, 197, 198])


class FaceOffset(unittest.TestCase):
    def test_offset_moves_the_face_median_to_the_wall_median(self):
        trim = np.zeros((10, 4, 3))
        trim[:] = (196, 190, 182)
        trim[5:] = (50, 50, 50)  # relief rows that must not count
        wall = np.zeros((6, 6, 3))
        wall[:] = (197, 188, 178)
        off = tb.face_offset(trim, np.arange(0, 5), wall)
        self.assertTrue(np.allclose(off, (1, -2, -4)))


if __name__ == "__main__":
    unittest.main()
