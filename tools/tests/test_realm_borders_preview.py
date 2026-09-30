"""realm_borders_preview: the territory model and border geometry behind the realm-borders previews.

Synthetic grids only, so the rules are proven without the game install: walls split territory, head
starts win ties, far land stays wild, enclosed pockets join the realm around them, chains follow one
realm pair and meet at junctions, and smoothing keeps junction endpoints and closed loops intact.
"""
import os
import sys
import unittest

try:
    import numpy as np
    import scipy  # noqa: F401
except ImportError as exc:  # CI's python-tests job installs no packages: skip, never error
    raise unittest.SkipTest("realm_borders_preview needs numpy and scipy (%s)" % exc)

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import realm_borders_preview as rb  # noqa: E402


class RdpTests(unittest.TestCase):
    def test_collinear_points_collapse_to_endpoints(self):
        p = np.array([[0, 0], [1, 0], [2, 0], [3, 0]], dtype=float)
        np.testing.assert_array_equal(rb.rdp(p, 0.1), [[0, 0], [3, 0]])

    def test_corner_beyond_tolerance_is_kept(self):
        p = np.array([[0, 0], [2, 0], [2, 2]], dtype=float)
        np.testing.assert_array_equal(rb.rdp(p, 0.5), p)

    def test_stair_steps_become_a_diagonal(self):
        stairs = np.array([[i // 2 + (i % 2), i // 2] for i in range(9)], dtype=float)
        self.assertEqual(len(rb.rdp(stairs, 0.9)), 2)


class ChaikinTests(unittest.TestCase):
    def test_open_curve_keeps_endpoints(self):
        p = np.array([[0, 0], [1, 1], [2, 0]], dtype=float)
        out = rb.chaikin(p, 3)
        np.testing.assert_array_equal(out[0], p[0])
        np.testing.assert_array_equal(out[-1], p[-1])

    def test_closed_curve_doubles_each_iteration(self):
        square = np.array([[0, 0], [1, 0], [1, 1], [0, 1]], dtype=float)
        self.assertEqual(len(rb.chaikin(square, 2, closed=True)), 16)


class PartitionTests(unittest.TestCase):
    def grid(self, n=20):
        return np.ones((n, n)), np.zeros((n, n), dtype=bool)

    def test_a_wall_splits_territory_between_seeds(self):
        cost, blocked = self.grid()
        blocked[:, 10] = True
        out = rb.partition(cost, blocked, [(0, 10, 2, 0.0), (1, 10, 17, 0.0)], 1.0, 1e9)
        self.assertTrue((out[:, :10] == 0).all())
        self.assertTrue((out[:, 11:] == 1).all())
        self.assertTrue((out[:, 10] == -1).all())

    def test_head_start_wins_the_cell_between_two_seeds(self):
        cost, blocked = self.grid(21)
        out = rb.partition(cost, blocked, [(0, 10, 0, 0.0), (1, 10, 20, 5.0)], 1.0, 1e9)
        # 5 of head start moves the border 2.5 cells toward the other seed
        self.assertEqual(out[10, 8], 1)
        self.assertEqual(out[10, 7], 0)

    def test_land_beyond_the_claim_stays_wild(self):
        cost, blocked = self.grid(30)
        out = rb.partition(cost, blocked, [(0, 0, 0, 0.0)], 1.0, 10.0)
        self.assertEqual(out[0, 5], 0)
        self.assertEqual(out[29, 29], -1)

    def test_seeds_sharing_a_label_form_one_province(self):
        cost, blocked = self.grid()
        out = rb.partition(cost, blocked, [(3, 2, 2, 0.0), (3, 17, 17, 0.0)], 1.0, 1e9)
        self.assertTrue((out == 3).all())

    def test_enclosed_pocket_joins_the_realm_around_it(self):
        cost, blocked = self.grid()
        blocked[8, 8:12] = blocked[11, 8:12] = True
        blocked[8:12, 8] = blocked[8:12, 11] = True
        seeds = [(0, 2, 2, 0.0)]
        self.assertEqual(rb.partition(cost, blocked, seeds, 1.0, 1e9)[9, 9], -1)
        # the pocket counts its ring of walls: 4 open cells + 12 wall cells
        self.assertEqual(rb.partition(cost, blocked, seeds, 1.0, 1e9, pocket_cells=10)[9, 9], -1)
        self.assertEqual(rb.partition(cost, blocked, seeds, 1.0, 1e9, pocket_cells=20)[9, 9], 0)

    def test_water_never_joins_a_pocket(self):
        cost, blocked = self.grid()
        water = np.zeros_like(blocked)
        water[8:12, 8:12] = True
        blocked |= water
        out = rb.partition(cost, blocked, [(0, 2, 2, 0.0)], 1.0, 1e9, pocket_cells=50, water=water)
        self.assertTrue((out[8:12, 8:12] == -1).all())

    def test_higher_cost_moves_the_border_toward_the_costly_side(self):
        cost, blocked = self.grid(21)
        cost[:, 11:] = 3.0
        out = rb.partition(cost, blocked, [(0, 10, 0, 0.0), (1, 10, 20, 0.0)], 1.0, 1e9)
        self.assertEqual(out[10, 12], 0)


class BorderChainTests(unittest.TestCase):
    def test_two_realms_share_one_chain(self):
        k = np.zeros((4, 6), dtype=int)
        k[:, 3:] = 1
        chains = rb.border_chains(k)
        self.assertEqual(len(chains), 1)
        pts, pair = chains[0]
        self.assertEqual(pair, (0, 1))
        self.assertEqual(len(pts), 5)
        self.assertTrue((pts[:, 1] == 3).all())

    def test_unclaimed_land_draws_no_realm_border(self):
        k = np.full((4, 6), -1)
        k[:, :3] = 0
        self.assertEqual(rb.border_chains(k), [])

    def test_three_realms_meet_at_one_junction(self):
        k = np.zeros((6, 6), dtype=int)
        k[:, 3:] = 1
        k[3:, :] = 2
        chains = rb.border_chains(k)
        self.assertEqual(sorted(pair for _, pair in chains), [(0, 1), (0, 2), (1, 2)])
        ends = [tuple(p) for pts, _ in chains for p in (pts[0], pts[-1])]
        self.assertEqual(ends.count((3.0, 3.0)), 3)

    def test_enclave_is_a_closed_ring(self):
        k = np.zeros((6, 6), dtype=int)
        k[2:4, 2:4] = 1
        (pts, pair), = rb.border_chains(k)
        self.assertEqual(pair, (0, 1))
        np.testing.assert_array_equal(pts[0], pts[-1])


class SmoothChainTests(unittest.TestCase):
    def test_open_chain_keeps_its_junction_endpoints(self):
        pts = np.array([[0, 0], [0, 1], [1, 1], [1, 2], [2, 2], [2, 3]], dtype=float)
        sm = rb.smooth_chain(pts)
        np.testing.assert_array_equal(sm[0], pts[0])
        np.testing.assert_array_equal(sm[-1], pts[-1])

    def test_closed_ring_stays_closed(self):
        ring = np.array([[2, 2], [2, 3], [2, 4], [3, 4], [4, 4], [4, 3], [4, 2], [3, 2], [2, 2]], dtype=float)
        sm = rb.smooth_chain(ring)
        np.testing.assert_array_equal(sm[0], sm[-1])
        self.assertGreater(len(sm), 8)


class NearestSeedGridTests(unittest.TestCase):
    def test_matches_brute_force(self):
        rng = np.random.default_rng(3)
        xs, ys = rng.uniform(0, 100, 30), rng.uniform(0, 80, 30)
        labels = rng.integers(0, 4, 30)
        g = rb.nearest_seed_grid(xs, ys, labels, 0.0, 0.0, 100.0, 80.0, 25)
        i, j = 7, 19
        x, y = i / 24 * 100.0, j / 24 * 80.0
        self.assertEqual(g[i, j], labels[np.argmin((xs - x) ** 2 + (ys - y) ** 2)])


if __name__ == "__main__":
    unittest.main()
