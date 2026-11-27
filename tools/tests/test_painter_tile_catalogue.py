#!/usr/bin/env python3
"""Unit tests for tools/painter/painter_tile_catalogue.py (no Painter).

Run:  python -m unittest tools.tests.test_painter_tile_catalogue
"""
import importlib.util
import os
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location(
    "painter_tile_catalogue", os.path.join(TOOLS, "painter", "painter_tile_catalogue.py"))
cat = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cat)


class ParamTests(unittest.TestCase):
    def test_visible_params_drop_channel_toggles(self):
        params = {"channel_height": {}, "normal_intensity": {}, "$randomseed": {}}
        self.assertEqual(sorted(cat.tunable(params)), ["$randomseed", "normal_intensity"])

    def test_unknown_expected_name_is_reported(self):
        params = {"a": {}, "b": {}}
        bad = cat.unknown_names({"joints": ["a", "zzz"], "chips": ["b"]}, params)
        self.assertEqual(bad, ["zzz"])

    def test_smoothness_class_thresholds(self):
        self.assertEqual(cat.smooth_class(0.1), "smooth")
        self.assertEqual(cat.smooth_class(0.3), "medium")
        self.assertEqual(cat.smooth_class(0.5), "rough")


if __name__ == "__main__":
    unittest.main()
