"""Tests for tools/blender/eye_follow.py: eyeballs follow their socket rings through the face channels."""
import math
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "blender"))
import eye_follow as ef  # noqa: E402


def ball(cx, n=8, r=0.01):
    """A ring of eyeball vertices around (cx, 0, 1.4), in the y-z plane, plus a front point."""
    pts = [(cx, r * math.cos(2 * math.pi * k / n), 1.4 + r * math.sin(2 * math.pi * k / n)) for k in range(n)]
    return pts + [(cx + r, 0.0, 1.4)]


def socket(cx, n=12, r=0.013):
    """A socket ring of head vertices around the same centre, a little wider than the eyeball."""
    return [(cx, r * math.cos(2 * math.pi * k / n), 1.4 + r * math.sin(2 * math.pi * k / n)) for k in range(n)]


LEFT, RIGHT = -0.03, 0.03
EYE = ball(LEFT) + ball(RIGHT)
FAR = [(0.0, 0.2, 1.0), (0.1, 0.1, 1.2)]  # head vertices nowhere near an eye
HEAD = socket(LEFT) + socket(RIGHT) + FAR
LEFT_RING = range(0, 12)
RIGHT_RING = range(12, 24)


def moved(points, idx, delta):
    out = list(points)
    for i in idx:
        out[i] = (out[i][0] + delta[0], out[i][1] + delta[1], out[i][2] + delta[2])
    return out


def close(a, b, tol=1e-9):
    return all(abs(x - y) <= tol for x, y in zip(a, b))


class SplitTests(unittest.TestCase):
    def test_two_eyes_split_on_the_axis_that_separates_them(self):
        low, high = ef.split_eyes(EYE)
        self.assertEqual(low, list(range(0, 9)))
        self.assertEqual(high, list(range(9, 18)))

    def test_one_vertex_is_refused(self):
        with self.assertRaises(ValueError):
            ef.split_eyes([(0.0, 0.0, 0.0)])


class RingTests(unittest.TestCase):
    def test_the_ring_is_the_socket_around_that_eye_only(self):
        rings = ef.rings_for(HEAD, EYE)
        self.assertEqual(rings[0], list(LEFT_RING))
        self.assertEqual(rings[1], list(RIGHT_RING))


class FitTests(unittest.TestCase):
    def test_a_translation_fits_with_scale_one(self):
        pts = socket(0.0)
        t, a = ef.fit_similarity(pts, [(p[0] + 0.002, p[1], p[2] - 0.004) for p in pts], (0.0, 0.0, 1.4))
        self.assertAlmostEqual(a, 1.0)
        self.assertTrue(close(t, (0.002, 0.0, -0.004)))

    def test_a_uniform_scale_about_the_centre_fits_with_no_translation(self):
        c = (0.0, 0.0, 1.4)
        pts = socket(0.0)
        grown = [(c[0] + 1.2 * (p[0] - c[0]), c[1] + 1.2 * (p[1] - c[1]), c[2] + 1.2 * (p[2] - c[2])) for p in pts]
        t, a = ef.fit_similarity(pts, grown, c)
        self.assertAlmostEqual(a, 1.2)
        self.assertTrue(close(t, (0.0, 0.0, 0.0)))

    def test_the_scale_is_clamped(self):
        c = (0.0, 0.0, 1.4)
        pts = socket(0.0)
        blown = [(c[0] + 9 * (p[0] - c[0]), c[1] + 9 * (p[1] - c[1]), c[2] + 9 * (p[2] - c[2])) for p in pts]
        _, a = ef.fit_similarity(pts, blown, c)
        self.assertEqual(a, ef.SCALE_LIMITS[1])


class FollowTests(unittest.TestCase):
    def test_a_still_head_leaves_the_eyes_still(self):
        self.assertEqual(ef.follow(HEAD, list(HEAD), EYE), EYE)

    def test_each_eye_follows_its_own_socket(self):
        key = moved(HEAD, LEFT_RING, (0.0, 0.005, 0.0))
        out = ef.follow(HEAD, key, EYE)
        for i in range(0, 9):
            self.assertTrue(close(out[i], (EYE[i][0], EYE[i][1] + 0.005, EYE[i][2])), i)
        for i in range(9, 18):
            self.assertTrue(close(out[i], EYE[i]), i)

    def test_a_growing_socket_grows_its_eye_about_the_eye_centre(self):
        c = ef._mean([EYE[i] for i in range(9, 18)])
        key = list(HEAD)
        for i in RIGHT_RING:
            p = HEAD[i]
            key[i] = (c[0] + 1.1 * (p[0] - c[0]), c[1] + 1.1 * (p[1] - c[1]), c[2] + 1.1 * (p[2] - c[2]))
        out = ef.follow(HEAD, key, EYE)
        for i in range(9, 18):
            want = tuple(c[k] + 1.1 * (EYE[i][k] - c[k]) for k in range(3))
            self.assertTrue(close(out[i], want, 1e-6), i)

    def test_an_eye_with_no_socket_near_it_is_refused(self):
        with self.assertRaises(ValueError):
            ef.follow(FAR, list(FAR), EYE)


if __name__ == "__main__":
    unittest.main()
