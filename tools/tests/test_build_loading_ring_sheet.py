"""build_loading_ring_sheet: the One Ring inscription sheet that replaces the loading-screen horse.

Synthetic masters only (an off-centre ring of gold dots with one big marker dot at twelve o'clock, on
black), so the sheet's contract is proven without the art: the layout the engine's `loading_bar`
material plays (8 x 8 cells, row by row, 64 frames; TAOM ships it at 1K, 128 px cells), black keyed to transparent, the
inscription re-centred so it turns without wobbling, a clockwise turn of 360/64 degrees per frame that
wraps seamlessly, clear cell margins, and no dark colour under transparent texels.
"""
import math
import os
import sys
import unittest

try:
    import numpy as np
    from PIL import Image
except ImportError as exc:  # CI's python-tests job installs no packages: skip, never error
    raise unittest.SkipTest("build_loading_ring_sheet needs numpy and Pillow (%s)" % exc)

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import build_loading_ring_sheet as ring  # noqa: E402

GOLD = (230, 180, 60)
MID = 63.5  # the centre of a 128 px cell in pixel-index coordinates


def synthetic_master(size=600, centre=(320, 280), radius=200, marker=True):
    """Gold dots on a circle around an off-centre point, black elsewhere; a big dot at 12 o'clock."""
    img = np.zeros((size, size, 3), dtype=np.uint8)
    yy, xx = np.mgrid[0:size, 0:size]
    cx, cy = centre
    for k in range(48):
        a = 2 * math.pi * k / 48
        px, py = cx + radius * math.sin(a), cy - radius * math.cos(a)
        r = 14 if (marker and k == 0) else 5
        img[(xx - px) ** 2 + (yy - py) ** 2 <= r * r] = GOLD
    return img


def blob_centroid(alpha, min_area):
    """Centroid of the largest connected bright region (the marker), by flood fill."""
    mask = alpha > 128
    seen = np.zeros_like(mask)
    best = None
    for y, x in zip(*np.nonzero(mask)):
        if seen[y, x]:
            continue
        stack, pts = [(y, x)], []
        seen[y, x] = True
        while stack:
            cy, cx = stack.pop()
            pts.append((cy, cx))
            for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                if 0 <= ny < mask.shape[0] and 0 <= nx < mask.shape[1] and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(pts) >= min_area and (best is None or len(pts) > len(best)):
            best = pts
    ys, xs = zip(*best)
    return float(np.mean(xs)), float(np.mean(ys))


class KeyAlphaTests(unittest.TestCase):
    def test_black_is_transparent_and_gold_keeps_its_colour(self):
        rgba = ring.key_alpha(synthetic_master())
        self.assertEqual(rgba[0, 0, 3], 0.0)
        cy, cx = 280 - 200, 320  # the marker dot's centre
        self.assertAlmostEqual(rgba[cy, cx, 3], 1.0, places=2)
        np.testing.assert_allclose(rgba[cy, cx, :3] * 255, GOLD, atol=2)

    def test_dim_glow_becomes_partial_alpha_with_the_ink_colour(self):
        img = np.zeros((4, 4, 3), dtype=np.uint8)
        img[0, 0] = GOLD
        img[1, 1] = (115, 90, 30)  # the same gold at half brightness: its glow
        rgba = ring.key_alpha(img)
        self.assertAlmostEqual(rgba[0, 0, 3], 1.0, places=2)
        self.assertAlmostEqual(rgba[1, 1, 3], 0.5, delta=0.01)
        np.testing.assert_allclose(rgba[1, 1, :3] * 255, GOLD, atol=2)


class FitCircleTests(unittest.TestCase):
    def test_finds_an_off_centre_ring(self):
        cx, cy, r = ring.fit_circle(ring.key_alpha(synthetic_master(marker=False))[:, :, 3])
        self.assertAlmostEqual(cx, 320, delta=1.0)
        self.assertAlmostEqual(cy, 280, delta=1.0)
        self.assertAlmostEqual(r, 200, delta=2.0)


class SheetTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.frames = ring.build_frames(synthetic_master())
        cls.sheet = ring.pack_sheet(cls.frames)

    def test_layout_matches_the_loading_bar_material(self):
        self.assertEqual(len(self.frames), ring.FRAMES)
        self.assertEqual((ring.COLUMNS, ring.ROWS, ring.FRAMES), (8, 8, 64))
        self.assertEqual(ring.CELL, 128)
        self.assertEqual(self.sheet.size, (1024, 1024))
        self.assertEqual(self.sheet.mode, "RGBA")

    def test_cells_are_packed_row_by_row(self):
        sheet = np.asarray(self.sheet)
        for i in (0, 1, 8, 63):
            r, c = divmod(i, 8)
            np.testing.assert_array_equal(sheet[r * 128:(r + 1) * 128, c * 128:(c + 1) * 128],
                                          np.asarray(self.frames[i]))

    def test_cell_margins_are_fully_transparent(self):
        m = ring.MIN_MARGIN
        for f in self.frames:
            a = np.asarray(f)[:, :, 3]
            for band in (a[:m], a[-m:], a[:, :m], a[:, -m:]):
                self.assertEqual(int(band.max()), 0)

    def test_inscription_is_centred_in_every_cell(self):
        for f in self.frames[::7]:
            a = np.asarray(f)[:, :, 3].astype(float) / 255
            cx, cy, _ = ring.fit_circle(a)
            self.assertAlmostEqual(cx, MID, delta=1.0)
            self.assertAlmostEqual(cy, MID, delta=1.0)

    @staticmethod
    def marker_angle(f):
        x, y = blob_centroid(np.asarray(f)[:, :, 3], min_area=12)
        return math.degrees(math.atan2(x - MID, MID - y)) % 360  # 0 at 12 o'clock, clockwise positive

    def test_turns_clockwise_by_one_step_per_frame(self):
        self.assertAlmostEqual(self.marker_angle(self.frames[0]), 0, delta=2)
        self.assertAlmostEqual(self.marker_angle(self.frames[8]), 45, delta=2)
        self.assertAlmostEqual(self.marker_angle(self.frames[16]), 90, delta=2)

    def test_one_turn_spans_all_64_frames_and_wraps_seamlessly(self):
        self.assertAlmostEqual(self.marker_angle(self.frames[32]), 180, delta=2)  # not a repeat at 32
        self.assertAlmostEqual(self.marker_angle(self.frames[63]), 360 - 360 / 64, delta=2)
        f = [np.asarray(x).astype(float) for x in self.frames]
        step = np.abs(f[1] - f[0]).mean()
        self.assertAlmostEqual(np.abs(f[0] - f[63]).mean(), step, delta=step * 0.25)

    def test_transparent_texels_carry_the_ink_colour_not_black(self):
        px = np.asarray(self.frames[0]).reshape(-1, 4)
        clear = px[px[:, 3] == 0][:, :3]
        self.assertGreater(len(clear), 0)
        np.testing.assert_allclose(clear.mean(axis=0), GOLD, atol=3)

    def test_reverse_turns_counter_clockwise(self):
        rev = ring.build_frames(synthetic_master(), clockwise=False)
        x, y = blob_centroid(np.asarray(rev[8])[:, :, 3], min_area=12)
        self.assertLess(x, MID)  # 45 degrees counter-clockwise from 12 o'clock is up and to the left
        self.assertLess(y, MID)


class CliTests(unittest.TestCase):
    def test_writes_sheet_and_preview(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            master = os.path.join(d, "master.png")
            Image.fromarray(synthetic_master()).save(master)
            out, gif = os.path.join(d, "sheet.png"), os.path.join(d, "preview.gif")
            self.assertEqual(ring.main([master, out, "--preview", gif]), 0)
            self.assertEqual(Image.open(out).size, (1024, 1024))
            with Image.open(gif) as g:
                self.assertEqual(g.n_frames, 64)

    def test_refuses_to_overwrite_without_force(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            master = os.path.join(d, "master.png")
            Image.fromarray(synthetic_master()).save(master)
            out = os.path.join(d, "sheet.png")
            Image.new("RGBA", (4, 4)).save(out)
            self.assertEqual(ring.main([master, out]), 2)
            self.assertEqual(Image.open(out).size, (4, 4))


if __name__ == "__main__":
    unittest.main()
