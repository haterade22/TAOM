#!/usr/bin/env python3
"""Unit tests for the pure parts of tools/blender/harness.py (the creature refine toolkit).

Run:  python -m unittest tools.tests.test_blender_harness_gait

The harness is exec'd inside Blender, imports bpy and mathutils at module level and creates its
render folder on load, so the stubs go in before it loads and os.makedirs is patched out. Blender-side
behaviour (sampling frames, the rest pose) is proven in a live session; these cover the stance-height
verdict that analyze_gait reports per foot.
"""
import importlib.util
import os
import sys
import types
import unittest
from unittest import mock

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "blender", "harness.py")

# The stubs live only for the load: installed in sys.modules for good, they changed which shim the
# sibling test_transfer_hand_morphs.py got, and the outcome depended on import order.
_mu = types.ModuleType("mathutils")
_mu.Vector = _mu.Euler = _mu.Quaternion = object
_spec = importlib.util.spec_from_file_location("taom_blender_harness", SCRIPT)
harness = importlib.util.module_from_spec(_spec)
with mock.patch.dict(sys.modules, {"bpy": types.ModuleType("bpy"), "mathutils": _mu}), \
        mock.patch("os.makedirs"):
    _spec.loader.exec_module(harness)


class StanceHeightTests(unittest.TestCase):
    """A planted foot should sit at its rest-pose height. Measuring against rest, not against
    z = 0, cancels the offset between the foot bone's tail and the sole of the mesh."""

    REST = 0.12  # a foot bone tail 12 cm above the sole, as rigs commonly have

    def test_planted_at_rest_height_is_ok(self):
        z = [self.REST, self.REST + 0.004, 0.40, 0.55, self.REST - 0.003]
        r = harness.stance_height(z, [0, 1, 4], self.REST)
        self.assertEqual(r["contact"], "ok")
        self.assertAlmostEqual(r["rel_min"], -0.003, places=4)

    def test_planted_three_cm_high_is_float(self):
        z = [self.REST + 0.03, self.REST + 0.031, 0.5, self.REST + 0.029]
        self.assertEqual(harness.stance_height(z, [0, 1, 3], self.REST)["contact"], "float")

    def test_planted_three_cm_low_is_penetration(self):
        z = [self.REST - 0.03, self.REST - 0.01, 0.5]
        self.assertEqual(harness.stance_height(z, [0, 1], self.REST)["contact"], "penetration")

    def test_no_planted_frames_is_unknown_not_ok(self):
        r = harness.stance_height([0.4, 0.5], [], self.REST)
        self.assertIsNone(r["contact"])

    def test_tolerance_is_the_argument(self):
        z = [self.REST + 0.015] * 3
        self.assertEqual(harness.stance_height(z, [0, 1, 2], self.REST)["contact"], "ok")
        self.assertEqual(harness.stance_height(z, [0, 1, 2], self.REST, tol=0.01)["contact"], "float")


class EarFlapPortTests(unittest.TestCase):
    """add_ear_flap lived only in the E: copy of the toolkit until 2026-09-29; the repo copy is now
    the one the skill runs, so it must carry it."""

    def test_ear_flap_is_in_the_repo_copy(self):
        self.assertTrue(callable(getattr(harness, "add_ear_flap", None)))
        self.assertEqual(len(harness.EAR_PAIRS), 2)


if __name__ == "__main__":
    unittest.main()
