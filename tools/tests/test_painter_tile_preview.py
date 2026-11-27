#!/usr/bin/env python3
"""Unit tests for tools/painter/painter_tile_preview.py (pure numpy and PIL, no Painter).

Run:  python -m unittest tools.tests.test_painter_tile_preview
"""
import importlib.util
import os
import unittest

import numpy as np

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location(
    "painter_tile_preview", os.path.join(TOOLS, "painter", "painter_tile_preview.py"))
pv = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pv)


class LitTests(unittest.TestCase):
    def test_flat_normal_gives_uniform_output(self):
        base = np.full((4, 4, 3), 128, np.uint8)
        normal = np.zeros((4, 4, 3), np.uint8)
        normal[...] = (128, 128, 255)
        out = pv.lit(base, normal)
        self.assertEqual(out.shape, (4, 4, 3))
        self.assertEqual(len(np.unique(out.reshape(-1, 3), axis=0)), 1)

    def test_normal_facing_light_is_brighter_than_facing_away(self):
        base = np.full((1, 2, 3), 128, np.uint8)
        toward = pv.LIGHT
        away = np.array([-toward[0], -toward[1], toward[2]])
        normal = np.zeros((1, 2, 3), np.uint8)
        normal[0, 0] = np.clip((toward + 1) * 127.5, 0, 255)
        normal[0, 1] = np.clip((away + 1) * 127.5, 0, 255)
        out = pv.lit(base, normal)
        self.assertGreater(int(out[0, 0, 0]), int(out[0, 1, 0]))


class TileTests(unittest.TestCase):
    def test_tile_2x2_doubles_each_side(self):
        a = np.arange(12, dtype=np.uint8).reshape(2, 2, 3)
        t = pv.tile2x2(a)
        self.assertEqual(t.shape, (4, 4, 3))
        self.assertTrue((t[2:, 2:] == a).all())


class SheetTests(unittest.TestCase):
    def test_sheet_size_and_cell_count(self):
        cells = [("a", np.zeros((8, 8, 3), np.uint8)), ("b", np.zeros((8, 8, 3), np.uint8)),
                 ("c", np.zeros((8, 8, 3), np.uint8))]
        sheet = pv.contact_sheet(cells, cols=2, cell=16, label_h=10)
        self.assertEqual(sheet.size, (32, 2 * 26))

    def test_split_pages(self):
        self.assertEqual(pv.pages(list(range(7)), 3), [[0, 1, 2], [3, 4, 5], [6]])


if __name__ == "__main__":
    unittest.main()
