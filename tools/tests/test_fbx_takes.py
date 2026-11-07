#!/usr/bin/env python3
"""Unit tests for tools/blender/fbx_takes.py (no Blender needed).

Run:  python -m unittest discover -s tools/tests -t . -p test_fbx_takes.py
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "blender"))
import fbx_takes  # noqa: E402


def _prop(name, cls):
    body = name.encode() + b"\x00\x01" + cls.encode()
    return b"S" + len(body).to_bytes(4, "little") + body


class TakeNamesTests(unittest.TestCase):
    def test_a_take_name_with_a_bar_is_read_whole(self):
        raw = b"junk" + _prop("adod_wolf_armature_unused|adod_wolf_gallop", "AnimStack") + b"tail"
        self.assertEqual(fbx_takes.take_names(raw), ["adod_wolf_armature_unused|adod_wolf_gallop"])

    def test_every_take_is_read_in_order(self):
        raw = _prop("a|one", "AnimStack") + b"\x00" * 8 + _prop("Scene", "AnimStack")
        self.assertEqual(fbx_takes.take_names(raw), ["a|one", "Scene"])

    def test_other_classes_are_not_takes(self):
        raw = _prop("wolf", "Model") + _prop("BaseLayer", "AnimLayer")
        self.assertEqual(fbx_takes.take_names(raw), [])

    def test_a_name_too_long_to_read_raises_instead_of_vanishing(self):
        with self.assertRaises(ValueError):
            fbx_takes.take_names(_prop("x" * 1100, "AnimStack"))

    def test_a_file_without_takes_reads_empty(self):
        self.assertEqual(fbx_takes.take_names(b"Kaydara FBX Binary  \x00"), [])


if __name__ == "__main__":
    unittest.main()
