"""Tests for tools/blender/hair_follow.py: hair and beard follow the head's morph channels by their roots."""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "blender"))
import hair_follow as hf  # noqa: E402


class RootTests(unittest.TestCase):
    def test_a_strand_takes_its_contact_vertex_as_root(self):
        # 0 touches the head, 1..3 hang below it on one strand
        near = [0.001, 0.05, 0.10, 0.15]
        self.assertEqual(hf.roots(4, [(0, 1), (1, 2), (2, 3)], near), [0, 0, 0, 0])

    def test_each_vertex_takes_its_nearest_root_by_hops(self):
        # a cap touching the head at both ends: 0 and 4 are roots
        near = [0.001, 0.02, 0.03, 0.02, 0.001]
        self.assertEqual(hf.roots(5, [(0, 1), (1, 2), (2, 3), (3, 4)], near), [0, 0, 0, 4, 4])

    def test_an_island_off_the_head_uses_its_nearest_vertex(self):
        near = [0.001, 0.02, 0.05, 0.03]
        self.assertEqual(hf.roots(4, [(0, 1), (2, 3)], near), [0, 0, 3, 3])


class FollowTests(unittest.TestCase):
    def test_a_strand_translates_with_its_anchor(self):
        head_base = [(0.0, 0.0, 1.0), (1.0, 0.0, 1.0)]
        head_frame = [(0.0, 0.0, 1.04), (1.0, 0.0, 1.0)]
        base = [(0.0, 0.0, 1.0), (0.0, 0.0, 0.8)]
        out = hf.follow(base, [0, 0], {0: [(0, 1.0)]}, head_base, head_frame)
        self.assertAlmostEqual(out[0][2], 1.04)
        self.assertAlmostEqual(out[1][2], 0.84)

    def test_weights_are_inverse_distance_and_an_exact_hit_wins(self):
        w = hf.weights([0.01, 0.03])
        self.assertAlmostEqual(w[0], 0.75)
        self.assertAlmostEqual(sum(w), 1.0)
        self.assertEqual(hf.weights([0.02, 0.0]), [0.0, 1.0])

    def test_a_still_head_leaves_the_mesh_still(self):
        head = [(0.0, 0.0, 1.0)]
        base = [(0.0, 0.0, 1.0), (0.1, 0.0, 0.5)]
        out = hf.follow(base, [0, 0], {0: [(0, 1.0)]}, head, head)
        self.assertEqual(hf.max_motion(base, out), 0.0)


if __name__ == "__main__":
    unittest.main()
