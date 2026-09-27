"""Tests for tools/check_eye_follow.py: an eyeball left still while its socket moves is a failure."""
import math
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import check_eye_follow as cef  # noqa: E402


def ring(cx, n, r):
    return [(cx, r * math.cos(2 * math.pi * k / n), 1.4 + r * math.sin(2 * math.pi * k / n)) for k in range(n)]


EYE = ring(-0.03, 8, 0.01) + ring(0.03, 8, 0.01)
HEAD = ring(-0.03, 12, 0.013) + ring(0.03, 12, 0.013) + [(0.0, 0.2, 1.0)]


def shifted(points, idx, dy):
    return [(p[0], p[1] + dy, p[2]) if i in idx else p for i, p in enumerate(points)]


LEFT_SOCKET = set(range(0, 12))
LEFT_EYE = set(range(0, 8))


class VerdictTests(unittest.TestCase):
    def test_a_still_eye_in_a_moving_socket_fails(self):
        sides = cef.analyse(HEAD, [shifted(HEAD, LEFT_SOCKET, 0.004)], EYE, [list(EYE)])
        bad = cef.verdict(sides)
        self.assertEqual(len(bad), 1)
        self.assertIn("channel 0", bad[0])

    def test_an_eye_that_follows_passes(self):
        sides = cef.analyse(HEAD, [shifted(HEAD, LEFT_SOCKET, 0.004)], EYE, [shifted(EYE, LEFT_EYE, 0.004)])
        self.assertEqual(cef.verdict(sides), [])

    def test_a_head_whose_channels_move_nothing_passes(self):
        sides = cef.analyse(HEAD, [list(HEAD), list(HEAD)], EYE, [list(EYE), list(EYE)])
        self.assertEqual(cef.verdict(sides), [])

    def test_a_socket_below_the_threshold_passes(self):
        sides = cef.analyse(HEAD, [shifted(HEAD, LEFT_SOCKET, 0.0005)], EYE, [list(EYE)])
        self.assertEqual(cef.verdict(sides), [])

    def test_mismatched_channel_counts_are_refused(self):
        with self.assertRaises(ValueError):
            cef.analyse(HEAD, [list(HEAD)], EYE, [])


if __name__ == "__main__":
    unittest.main()
