"""Tests for tools/painter/painter_rubble_layout.py: the layout maps of a heap of broken stone with no courses."""
import os
import sys
import unittest

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "painter"))
import painter_rubble_layout as rl  # noqa: E402

SIZE = 256
PPM = 115.0  # a 256 px tile of 2.222 m


class SeedsTest(unittest.TestCase):
    def test_points_keep_their_radii_apart_across_the_wrap(self):
        rng = np.random.default_rng(1)
        pts, radii = rl.seed_points(SIZE, PPM, (0.10, 0.30), rng, tries=4000)
        self.assertGreater(len(pts), 15)
        for i in range(len(pts)):
            d = np.abs(pts - pts[i])
            d = np.minimum(d, SIZE - d)
            dist = np.sqrt((d ** 2).sum(1))
            dist[i] = 1e9
            self.assertTrue((dist >= 0.5 * (radii + radii[i]) - 1e-6).all(), msg=i)

    def test_the_same_seed_gives_the_same_points(self):
        a = rl.seed_points(SIZE, PPM, (0.10, 0.30), np.random.default_rng(5), tries=1000)[0]
        b = rl.seed_points(SIZE, PPM, (0.10, 0.30), np.random.default_rng(5), tries=1000)[0]
        self.assertTrue((a == b).all())


class LabelsTest(unittest.TestCase):
    def setUp(self):
        self.rng = np.random.default_rng(2)
        self.pts, self.radii = rl.seed_points(SIZE, PPM, (0.10, 0.30), self.rng, tries=4000)
        self.labels = rl.voronoi_labels(self.pts, SIZE, 0.45 * self.radii)

    def test_every_pixel_has_a_cell_and_every_cell_is_used(self):
        self.assertEqual(self.labels.shape, (SIZE, SIZE))
        self.assertEqual(len(np.unique(self.labels)), len(self.pts))

    def test_the_labels_wrap(self):
        # A shifted copy of the points gives the shifted labels.
        shift = np.array([37.0, 91.0])
        moved = rl.voronoi_labels((self.pts + shift) % SIZE, SIZE, 0.45 * self.radii)
        back = np.roll(self.labels, (91, 37), axis=(0, 1))
        self.assertTrue((moved == back).mean() > 0.999)

    def test_without_weights_it_is_the_plain_nearest_seed(self):
        pts = np.array([[10.0, 10.0], [50.0, 10.0]])
        lab = rl.voronoi_labels(pts, 64)
        self.assertEqual(lab[10, 12], 0)
        self.assertEqual(lab[10, 48], 1)

    def test_a_heavier_seed_takes_more_room(self):
        pts = np.array([[16.0, 32.0], [48.0, 32.0]])
        plain = (rl.voronoi_labels(pts, 64) == 0).sum()
        heavy = (rl.voronoi_labels(pts, 64, np.array([14.0, 2.0])) == 0).sum()
        self.assertGreater(heavy, plain)

    def test_pieces_vary_in_size(self):
        areas = np.bincount(self.labels.ravel()).astype(float)
        self.assertGreater(areas.max() / areas.min(), 3.0)


class EdgeDistanceTest(unittest.TestCase):
    def test_distance_is_zero_on_a_boundary_and_grows_inside(self):
        labels = np.zeros((32, 32), int)
        labels[:, 16:] = 1
        d = rl.boundary_distance(labels)
        self.assertEqual(d[:, 15].max(), 0.0)
        self.assertGreater(d[10, 8], 6.0)

    def test_the_wrap_edge_counts_as_a_boundary(self):
        labels = np.zeros((32, 32), int)
        labels[:, 16:] = 1
        d = rl.boundary_distance(labels)
        self.assertEqual(d[:, 0].max(), 0.0)
        self.assertEqual(d[:, 31].max(), 0.0)


class MapsTest(unittest.TestCase):
    def setUp(self):
        self.m = rl.build_maps(SIZE, PPM, seed=3, piece_m=(0.10, 0.30), gap_mm=(8.0, 30.0))

    def test_the_maps_exist_and_are_in_range(self):
        for key in ("id", "height", "dust", "ao", "edgelight"):
            a = self.m[key]
            self.assertEqual(a.shape, (SIZE, SIZE), msg=key)
            self.assertGreaterEqual(float(a.min()), 0.0, msg=key)
            self.assertLessEqual(float(a.max()), 1.0, msg=key)

    def test_dust_covers_a_modest_share(self):
        share = float((self.m["dust"] > 0.5).mean())
        self.assertGreater(share, 0.04)
        self.assertLess(share, 0.35)

    def test_the_pieces_stand_above_the_dust(self):
        dust = self.m["dust"] > 0.9
        self.assertGreater(float(self.m["height"][~(self.m["dust"] > 0.1)].mean()),
                           float(self.m["height"][dust].mean()) + 0.2)

    def test_the_maps_tile(self):
        for key in ("height", "id"):
            a = self.m[key]
            step_in = np.abs(np.diff(a, axis=1)).mean()
            step_wrap = np.abs(a[:, 0] - a[:, -1]).mean()
            self.assertLess(step_wrap, 4.0 * step_in + 0.05, msg=key)

    def test_the_tone_is_one_value_per_piece(self):
        # Inside a piece the id map is flat: the share of pixels equal to a neighbour is high.
        a = self.m["id"]
        self.assertGreater(float((np.abs(np.diff(a, axis=1)) < 1e-6).mean()), 0.85)

    def test_the_piece_sizes_fall_in_the_asked_range(self):
        st = self.m["info"]
        self.assertGreaterEqual(st["piece_count"], 12)
        self.assertGreaterEqual(st["median_diameter_m"], 0.10)
        self.assertLessEqual(st["median_diameter_m"], 0.40)

    def test_the_same_seed_is_the_same_layout(self):
        again = rl.build_maps(SIZE, PPM, seed=3, piece_m=(0.10, 0.30), gap_mm=(8.0, 30.0))
        self.assertTrue((again["height"] == self.m["height"]).all())


class MergeTest(unittest.TestCase):
    def test_merging_pairs_gives_fewer_and_larger_pieces(self):
        plain = rl.build_maps(SIZE, PPM, seed=4, piece_m=(0.08, 0.30), gap_mm=(8.0, 30.0), merge=0.0)["info"]
        merged = rl.build_maps(SIZE, PPM, seed=4, piece_m=(0.08, 0.30), gap_mm=(8.0, 30.0), merge=0.6)["info"]
        self.assertLess(merged["piece_count"], plain["piece_count"])
        self.assertGreater(merged["median_diameter_m"], plain["median_diameter_m"])

    def test_no_piece_joins_more_than_one_other(self):
        labels = np.arange(16).reshape(4, 4)
        roots = rl.merge_roots(labels, 16, 1.0, np.random.default_rng(1))
        counts = np.bincount(roots, minlength=16)
        self.assertLessEqual(int(counts.max()), 2)
        self.assertLess(len(np.unique(roots)), 16)

    def test_zero_probability_changes_nothing(self):
        labels = np.arange(16).reshape(4, 4)
        roots = rl.merge_roots(labels, 16, 0.0, np.random.default_rng(1))
        self.assertTrue((roots == np.arange(16)).all())


class WriteTest(unittest.TestCase):
    def test_files_are_layout_png_names_with_a_16_bit_height(self):
        import tempfile
        from PIL import Image
        m = rl.build_maps(64, 32.0, seed=1, piece_m=(0.2, 0.6), gap_mm=(10.0, 30.0))
        with tempfile.TemporaryDirectory() as d:
            files = rl.write_maps(m, d)
            self.assertTrue(os.path.exists(os.path.join(d, "layout_height.png")))
            self.assertTrue(os.path.exists(os.path.join(d, "layout_dust.png")))
            self.assertEqual(Image.open(files["height"]).mode, "I;16")
            self.assertEqual(Image.open(files["dust"]).mode, "L")


if __name__ == "__main__":
    unittest.main()
