#!/usr/bin/env python3
"""Unit tests for the pure parts of tools/blender/transfer_hand_morphs.py.

Run:  python -m unittest tools.tests.test_transfer_hand_morphs

The script imports bpy, numpy and mathutils at module level, so bpy gets the stub module
test_add_mesh_lods.py uses and mathutils a minimal Vector shim; numpy is real, and without it (CI
installs nothing) every test here skips. The Blender side (the fit, Surface Deform, the round trip) is
proven by the report the script writes on every run and by `tools/audit_fbx_lods.py --diff`.
"""
import importlib.util
import os
import sys
import types
import unittest

try:
    import numpy
except ImportError:
    numpy = None

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "blender", "transfer_hand_morphs.py")
BASE = ["blender", "-b", "--"]
REQUIRED = ["--source", "s.fbx", "--source-mesh", "S", "--fbx", "t.fbx", "--target-mesh", "T"]


class _Vector(tuple):
    """Just enough of mathutils.Vector for the module to import."""

    def __new__(cls, values=(0.0, 0.0, 0.0)):
        return super().__new__(cls, (float(x) for x in values))

    def __add__(self, other):
        return _Vector(a + b for a, b in zip(self, other))

    def __mul__(self, k):
        return _Vector(a * k for a in self)


thm = None
if numpy is not None:
    if "bpy" not in sys.modules:
        sys.modules["bpy"] = types.ModuleType("bpy")
    if "mathutils" not in sys.modules:
        _mu = types.ModuleType("mathutils")
        _mu.Vector = _Vector
        sys.modules["mathutils"] = _mu
    _path = list(sys.path)        # the script puts tools/blender first on sys.path; keep that out of other tests
    try:
        _spec = importlib.util.spec_from_file_location("transfer_hand_morphs", SCRIPT)
        thm = importlib.util.module_from_spec(_spec)
        _spec.loader.exec_module(thm)
    finally:
        sys.path[:] = _path


def grid_strip(cols, rows):
    """Quads over a (rows + 1) x (cols + 1) vertex grid; vertex (r, c) is r * (cols + 1) + c."""
    vid = lambda r, c: r * (cols + 1) + c  # noqa: E731
    polys = [(vid(r, c), vid(r, c + 1), vid(r + 1, c + 1), vid(r + 1, c)) for r in range(rows) for c in range(cols)]
    return polys, vid


@unittest.skipUnless(numpy, "numpy is not installed")
class SeamWeightTests(unittest.TestCase):
    """A strip 8 quads wide and 12 long, rows 0 to 10 moved and rows 11 and 12 not: the open boundary is the
    outer ring, and row 10 meets the unmoved rows, so both are seam."""
    COLS, ROWS, MOVED_ROWS = 8, 12, 11

    def setUp(self):
        self.polys, self.vid = grid_strip(self.COLS, self.ROWS)
        self.moved = {self.vid(r, c) for r in range(self.MOVED_ROWS) for c in range(self.COLS + 1)}
        self.weights, self.seam = thm.seam_weights(self.polys, self.moved, 3)

    def test_the_open_boundary_is_pinned(self):
        border = [self.vid(0, c) for c in range(self.COLS + 1)] + \
                 [self.vid(r, c) for r in range(self.MOVED_ROWS) for c in (0, self.COLS)]
        self.assertEqual({self.weights[v] for v in border}, {0.0})

    def test_the_edge_of_the_moved_region_is_pinned(self):
        self.assertEqual({self.weights[self.vid(10, c)] for c in range(self.COLS + 1)}, {0.0})

    def test_the_weight_rises_over_the_rings(self):
        mid = self.COLS // 2
        self.assertAlmostEqual(self.weights[self.vid(1, mid)], 1 / 3.0)
        self.assertAlmostEqual(self.weights[self.vid(2, mid)], 2 / 3.0)
        self.assertEqual(self.weights[self.vid(3, mid)], 1.0)
        self.assertEqual(self.weights[self.vid(5, mid)], 1.0)
        self.assertAlmostEqual(self.weights[self.vid(5, 1)], 1 / 3.0)

    def test_nothing_outside_the_moved_set_is_weighted(self):
        self.assertEqual(set(self.weights), self.moved)

    def test_the_seam_holds_the_unmoved_boundary_too(self):
        self.assertIn(self.vid(12, 4), self.seam)
        self.assertNotIn(self.vid(5, 4), self.seam)

    def test_a_closed_island_with_no_seam_is_not_pinned(self):
        tetra = [(0, 1, 2), (0, 3, 1), (1, 3, 2), (2, 3, 0)]
        weights, seam = thm.seam_weights(tetra, {0, 1, 2, 3}, 3)
        self.assertEqual(seam, set())
        self.assertEqual(weights, {0: 1.0, 1: 1.0, 2: 1.0, 3: 1.0})

    def test_weigh_scales_every_channel(self):
        out = thm.weigh([{0: 2.0, 1: 3.0}, {0: 4.0, 1: 5.0}], {0: 0.0, 1: 0.5})
        self.assertEqual(out, [{0: 0.0, 1: 1.5}, {0: 0.0, 1: 2.5}])


@unittest.skipUnless(numpy, "numpy is not installed")
class SeamGateTests(unittest.TestCase):
    def test_the_torn_uruk_arms_transfer_fails(self):
        """The first cut moved the troll's wrist seam 0.089 against a 0.552 peak: 16%."""
        seam_max, allowed, diff = thm.seam_gate([0.0, 0.041, 0.089], [0.1162, 0.552, 0.4981])
        self.assertEqual(seam_max, 0.089)
        self.assertAlmostEqual(allowed, 0.01104)
        self.assertIn("the wrist tears", diff)

    def test_a_pinned_seam_passes(self):
        self.assertIsNone(thm.seam_gate([0.0, 0.0, 0.0], [0.1162, 0.552, 0.4981])[2])

    def test_the_limit_itself_passes(self):
        self.assertIsNone(thm.seam_gate([0.01], [0.5])[2])
        self.assertIsNotNone(thm.seam_gate([0.0101], [0.5])[2])


@unittest.skipUnless(numpy, "numpy is not installed")
class MirrorTests(unittest.TestCase):
    """The torn run's palm normals, hands along X."""
    LEFT, RIGHT = (-0.826, 0.48, -0.296), (0.806, 0.434, -0.403)

    def test_mirror_image_palms(self):
        self.assertGreater(thm.mirror_dot(self.LEFT, self.RIGHT, (-1.0, 0.0, 0.0), (1.0, 0.0, 0.0)), 0.95)

    def test_a_flipped_palm_reading(self):
        flipped = tuple(-x for x in self.RIGHT)
        self.assertLess(thm.mirror_dot(self.LEFT, flipped, (-1.0, 0.0, 0.0), (1.0, 0.0, 0.0)),
                        thm.MIRROR_MIN)


@unittest.skipUnless(numpy, "numpy is not installed")
class ParseArgsTests(unittest.TestCase):
    def test_defaults(self):
        a = thm.parse_args(BASE + REQUIRED)
        self.assertEqual((a["channels"], a["seam_rings"], a["smooth"], a["falloff"]), (26, 3, 2, 4.0))
        self.assertFalse(a["apply"])
        self.assertIsNone(a["preview"])

    def test_channels_and_seam_rings(self):
        a = thm.parse_args(BASE + REQUIRED + ["--channels", "24", "--seam-rings", "5", "--smooth", "6", "--apply"])
        self.assertEqual((a["channels"], a["seam_rings"], a["smooth"]), (24, 5, 6))
        self.assertTrue(a["apply"])

    def test_the_seam_is_always_pinned(self):
        with self.assertRaises(SystemExit):
            thm.parse_args(BASE + REQUIRED + ["--seam-rings", "0"])
        with self.assertRaises(SystemExit):
            thm.parse_args(BASE + REQUIRED + ["--channels", "0"])

    def test_required_and_bad_arguments(self):
        with self.assertRaises(SystemExit) as cm:
            thm.parse_args(BASE + REQUIRED[2:])
        self.assertIn("--source is required", str(cm.exception))
        with self.assertRaises(SystemExit):
            thm.parse_args(BASE + REQUIRED + ["--bogus"])
        with self.assertRaises(SystemExit):
            thm.parse_args(BASE + REQUIRED + ["--preview", "d", "--apply"])


if __name__ == "__main__":
    unittest.main()
